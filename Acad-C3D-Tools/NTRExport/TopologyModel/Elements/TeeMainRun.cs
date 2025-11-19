using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;
using NTRExport.SoilModel;

using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using static IntersectUtilities.UtilsCommon.Utils;
using static NTRExport.Utils.Utils;

namespace NTRExport.TopologyModel
{
    internal abstract class TeeMainRun : TFitting
    {
        protected TeeMainRun(Handle source, PipelineElementType kind)
            : base(source, kind)
        {
            OffsetMain = ComputeTwinOffsets(System, Type, DnM);
        }

        public override string DotLabelForTest()
        {
            return $"{Source.ToString()} / {this.GetType().Name}\n{DnLabel()}";
        }
        public override string DnLabel() => $"{DnM.ToString()}/{DnB.ToString()}";

        public override int DN => DnM;
        protected int DnM =>
            _entity switch
            {
                BlockReference br => Convert.ToInt32(
                    br.ReadDynamicCsvProperty(DynamicProperty.DN1)
                ),
                _ => throw new InvalidOperationException(
                    $"Entity {Source} is not a BlockReference!"
                ),
            };
        protected int DnB =>
            _entity switch
            {
                BlockReference br => Convert.ToInt32(
                    br.ReadDynamicCsvProperty(DynamicProperty.DN2)
                ),
                _ => throw new InvalidOperationException(
                    $"Entity {Source} is not a BlockReference!"
                ),
            };
        protected TPort MainPort1 => Ports.First(p => p.Role == PortRole.Main);
        protected TPort MainPort2 => Ports.Last(p => p.Role == PortRole.Main);
        protected Point2d MidPoint => MainPort1.Node.Pos.To2d().MidPoint(MainPort2.Node.Pos.To2d());
        protected TPort BranchPort => Ports.First(p => p.Role == PortRole.Branch);
        protected (double zUp, double zLow) OffsetMain;

        protected List<(TPort exitPort, double exitZ, double exitSlope)> RouteMain(
            RoutedGraph g,
            Topology topo,
            RouterContext ctx,
            TPort? entryPort,
            double entryZ,
            double entrySlope,
            FlowRole bondedFlowRole = FlowRole.Unknown)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();

            // Emit main run
            var ltgMain = LTGMain(Source);

            if (!Variant.IsTwin)
            {
                var mainFlow = bondedFlowRole != FlowRole.Unknown
                    ? bondedFlowRole
                    : ResolveBondedFlowRole(topo);
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = MainPort1.Node.Pos.Z(entryZ),
                        B = MainPort2.Node.Pos.Z(entryZ),
                        DN = DnM,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = mainFlow,
                        LTG = ltgMain,
                    }
                    );
            }
            else
            {
                var (zUp, zLow) = OffsetMain;
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = MainPort1.Node.Pos.Z(entryZ + zUp),
                        B = MainPort2.Node.Pos.Z(entryZ + zUp),
                        DN = DnM,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Return,
                        LTG = ltgMain,
                    }
                );
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = MainPort1.Node.Pos.Z(entryZ + zLow),
                        B = MainPort2.Node.Pos.Z(entryZ + zLow),
                        DN = DnM,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Supply,
                        LTG = ltgMain,
                    }
                );
            }

            // Propagate elevation/slope to main ports
            if (entryPort == null)
            {
                // Entry from branch: emit both main ports as exits
                exits.Add((MainPort1, entryZ, entrySlope));
                exits.Add((MainPort2, entryZ, entrySlope));
            }
            else
            {
                // Entry from a main port: emit only the other main port as exit
                bool entryIsMain1 = ReferenceEquals(entryPort, MainPort1);
                TPort otherMain = entryIsMain1 ? MainPort2 : MainPort1;
                exits.Add((otherMain, entryZ, entrySlope));
            }

            return exits;
        }
    }

    internal sealed class TeeFormstykke : TeeMainRun
    {
        public TeeFormstykke(Handle source, PipelineElementType kind)
            : base(source, kind) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Svejsetee);
            allowed.Add(PipelineElementType.PreskoblingTee);
            allowed.Add(PipelineElementType.Muffetee);
        }
    }

    internal sealed class AfgreningMedSpring : TeeMainRun
    {
        public AfgreningMedSpring(Handle source)
            : base(source, PipelineElementType.AfgreningMedSpring) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.AfgreningMedSpring);
        }

        internal override void AttachPropertySet()
        {
            var ntr = new NtrData(_entity);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, 
            TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();

            // Ports by role - AfgreningMedSpring must have exactly 2 Main ports and 1 Branch port
            var mains = Ports.Where(p => p.Role == PortRole.Main).ToArray();
            var branch = Ports.FirstOrDefault(p => p.Role == PortRole.Branch);
            if (mains.Length != 2 || branch == null)
            {
                throw new System.Exception(
                    $"AfgreningMedSpring {Source}: invalid port configuration. Expected 2 Main + 1 Branch, " +
                    $"found {mains.Length} Main and {(branch == null ? "0" : "1")} Branch ports.");
            }

            var isUp = IsUp();

            int dnBranch = DnB;
            int dnMain = DnM;

            var sd = LookupSpringData(Series, dnMain, dnBranch);

            // Calculate deltaZ = h = (D/2 + s + D1/2) in meters
            double deltaZ = (sd.D / 2.0 + sd.s + sd.D1 / 2.0) / 1000.0;
            double signedDz = isUp ? deltaZ : -deltaZ;

            // Determine entry port type
            bool entryIsMain = mains.Contains(entryPort);

            // Calculate elevations
            double zMain = entryIsMain ? entryZ : entryZ - signedDz;
            double zBranch = entryIsMain ? entryZ + signedDz : entryZ;

            // Emit main run via base class (always, using correct elevation)
            // If entry is from branch, pass null to RouteMain to emit both main ports as exits
            TPort? mainEntryPort = entryIsMain ? entryPort : null;
            double mainEntryZ = entryIsMain ? entryZ : zMain;
            var mainFlow = Variant.IsTwin ? FlowRole.Unknown : ResolveBondedFlowRole(topo);
            var mainExits = RouteMain(g, topo, ctx, mainEntryPort, mainEntryZ, entrySlope, mainFlow);
            exits.AddRange(mainExits);

            // Emit branch geometry: 45° bend + horizontal section
            // Branch direction: from branch port towards MidPoint (center of main run)
            var branchStart2d = BranchPort.Node.Pos.To2d();
            var branchStart3d = BranchPort.Node.Pos.Z(zBranch);
            var midPoint2d = MidPoint;
            var branchDir2d = (branchStart2d - midPoint2d).GetNormal();

            // Convert L1 from mm to meters
            double l1Meters = sd.L1 / 1000.0;

            // Compute 45° section from MidPoint: length = sqrt(2) * h, with XY projection = h
            double hMeters = Math.Abs(signedDz);
            var midPoint3d = midPoint2d.To3d(zMain);
            var end45_2d = midPoint2d + branchDir2d.MultiplyBy(hMeters);
            var end45_3d = end45_2d.To3d(zMain + signedDz);

            // Prepare fillet between lines MidPoint↔End45 and Branch↔End45 with 5D radius (branch DN)
            var a2 = midPoint2d;           // start of first straight (MidPoint)
            var b2 = branchStart2d;        // end straight (Branch port)
            var t2 = end45_2d;             // 45° intersection point (tangent point)
            var va = a2 - t2; var vb = b2 - t2;
            var ltgBranch = LTGBranch(Source);
            if (ltgBranch.IsNoE()) ltgBranch = LTGMain(Source);
            var branchFlow = Variant.IsTwin ? FlowRole.Unknown : ResolveBondedFlowRole(topo, branch);
            double r = Geometry.GetBogRadius5D(DnB) / 1000.0;

            bool TrySolveFillet3D(out Point3d aPrime3, out Point3d bPrime3, out Point3d tPoint)
            {
                aPrime3 = default; bPrime3 = default; tPoint = default;
                var v1 = end45_3d - midPoint3d;   // Mid → End45
                var v2 = branchStart3d - end45_3d; // End45 → Branch
                if (v1.Length < 1e-9 || v2.Length < 1e-9) return false;
                var u1 = v1.GetNormal();
                var u2 = v2.GetNormal();
                // Interior angle at corner between directions d1 (towards Mid) and d2 (towards Branch)
                var d1 = (-u1).GetNormal();
                var d2 = u2;
                var dot = Math.Max(-1.0, Math.Min(1.0, d1.DotProduct(d2)));
                var theta = Math.Acos(dot);
                var half = theta * 0.5;
                var tanHalf = Math.Tan(half);
                var sinHalf = Math.Sin(half);
                if (tanHalf < 1e-9 || sinHalf < 1e-9) return false; // degenerate
                var l = r / tanHalf; // fixed radius fillet
                var len1 = v1.Length; var len2 = v2.Length;
                if (l >= len1 - 1e-9 || l >= len2 - 1e-9) return false; // cannot fit fixed R
                aPrime3 = end45_3d + d1.MultiplyBy(l);
                bPrime3 = end45_3d + d2.MultiplyBy(l);
                // Tangent point for bend definition: use the corner (intersection of untrimmed straights)
                tPoint = end45_3d;
                return true;
            }

            if (!Variant.IsTwin)
            {
                // Solve fillet trimming points (3D)
                if (!TrySolveFillet3D(out var aPrime3, out var bPrime3, out var tPoint))
                {
                    // Fallback: emit simple straight 45° and straight to branch (no fillet)
                    g.Members.Add(new RoutedStraight(Source, this)
                    {
                        A = midPoint3d,
                        B = end45_3d,
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = branchFlow,
                        LTG = ltgBranch,
                    });
                    g.Members.Add(new RoutedStraight(Source, this)
                    {
                        A = end45_3d,
                        B = branchStart3d,
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = branchFlow,
                        LTG = ltgBranch,
                    });
                }
                else
                {
                    var aZ = midPoint3d;
                    var bZ = branchStart3d;
                    var tZ = tPoint;

                    // MidPoint → a'
                    g.Members.Add(new RoutedStraight(Source, this)
                    {
                        A = aZ,
                        B = aPrime3,
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = branchFlow,
                        LTG = ltgBranch,
                    });

                    // Bend a' → b' with PT at corner tangent point
                    g.Members.Add(new RoutedBend(Source, this)
                    {
                        A = aPrime3,
                        B = bPrime3,
                        T = tPoint,
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = branchFlow,
                        LTG = ltgBranch,
                    });

                    // b' → Branch
                    g.Members.Add(new RoutedStraight(Source, this)
                    {
                        A = bPrime3,
                        B = bZ,
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = branchFlow,
                        LTG = ltgBranch,
                    });
                }
            }
            else
            {
                var (zUpBranch, zLowBranch) = ComputeTwinOffsets(System, Type, DnB);
                // Emit twin branch geometry (similar to above but with offsets)
                // TODO: Implement twin branch geometry if needed
            }

            // Calculate exit elevations and slopes
            // Main exits already added by base.Route() above
            if (entryIsMain)
            {
                // Entry from main: add branch exit
                exits.Add((branch, zBranch, entrySlope));
            }
            else
            {
                // Entry from branch: add branch exit
                exits.Add((branch, entryZ, entrySlope));
            }

            return exits;
        }

        private bool IsUp()
        {
            var ntr = new NtrData(_entity);
            return ntr.AfgreningMedSpringDir == "Up";
        }

        private static SpringData LookupSpringData(PipeSeriesEnum series, int dnMain, int dnBranch)
        {
            return SpringCatalogLookup.Lookup(series, dnMain, dnBranch);
        }

        // Lookup service for Afgrening med spring catalog tables (serie 2 and serie 3)
        private struct SpringData
        {
            /// <summary>
            /// Main jacket diameter in millimeters.
            /// </summary>
            public double D;

            /// <summary>
            /// Branch jacket diameter in millimeters.
            /// </summary>
            public double D1;

            /// <summary>
            /// Distance between main jacket upper tangent and branch jacket lower tangent in millimeters.
            /// Constant value: 70 mm.
            /// </summary>
            public double s;

            /// <summary>
            /// Length of main run part of tee in millimeters.
            /// </summary>
            public double L;

            /// <summary>
            /// Horizontal projection length of branch from pipe centre in millimeters.
            /// </summary>
            public double L1;
        }
        private static class SpringCatalogLookup
        {
            // CSV-backed maps (mainDN, branchDN) -> SpringData
            private static readonly object _csvLock = new object();
            private static bool _serie1Loaded = false;
            private static bool _serie2Loaded = false;
            private static bool _serie3Loaded = false;
            private static bool _allLoaded => _serie1Loaded && _serie2Loaded && _serie3Loaded;
            private static readonly Dictionary<(int mainDN, int branchDN), SpringData> _serie1 =
                new Dictionary<(int mainDN, int branchDN), SpringData>();
            private static readonly Dictionary<(int mainDN, int branchDN), SpringData> _serie2 =
                new Dictionary<(int mainDN, int branchDN), SpringData>();
            private static readonly Dictionary<(int mainDN, int branchDN), SpringData> _serie3 =
                new Dictionary<(int mainDN, int branchDN), SpringData>();

            private static Dictionary<(int mainDN, int branchDN), SpringData> GetMap(PipeSeriesEnum series) =>
                series switch
                {
                    PipeSeriesEnum.S1 => _serie1,
                    PipeSeriesEnum.S2 => _serie2,
                    PipeSeriesEnum.S3 => _serie3,
                    _ => throw new System.Exception($"SpringCatalogLookup: Unsupported series '{series}'")
                };

            private static void EnsureLoaded(PipeSeriesEnum series)
            {
                lock (_csvLock)
                {
                    if ((series == PipeSeriesEnum.S1 && _serie1Loaded) ||
                        (series == PipeSeriesEnum.S2 && _serie2Loaded) ||
                        (series == PipeSeriesEnum.S3 && _serie3Loaded) ||
                        _allLoaded)
                        return;

                    var assembly = Assembly.GetExecutingAssembly();
                    var file = "spring_tables.csv";

                    var resourceName =
                        assembly.GetManifestResourceNames()
                            .FirstOrDefault(n => n.EndsWith(file, StringComparison.OrdinalIgnoreCase));

                    if (resourceName == null)
                        throw new System.Exception($"SpringCatalogLookup: Embedded resource not found: '*{file}'");

                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream == null)
                        throw new System.Exception($"SpringCatalogLookup: Unable to open embedded resource stream: {resourceName}");

                    using var reader = new StreamReader(stream);

                    string? header = reader.ReadLine();
                    if (header == null)
                        throw new System.Exception($"SpringCatalogLookup: Embedded CSV empty: {resourceName}");

                    var sep = DetectSeparator(header);
                    // We read a single file containing three blocks: S1, S2, S3 separated by empty lines
                    _serie1.Clear();
                    _serie2.Clear();
                    _serie3.Clear();

                    string? line;
                    int lineIdx = 1; // already consumed header
                    int currentSection = 1; // 1 => S1, 2 => S2, 3 => S3
                    bool hasDataInCurrentSection = false;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineIdx++;
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            if (hasDataInCurrentSection)
                            {
                                currentSection++;
                                hasDataInCurrentSection = false;
                            }
                            continue;
                        }
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("#")) continue;
                        var parts = trimmed.Split(sep);
                        if (parts.Length < 7)
                            throw new System.Exception($"SpringCatalogLookup: expected 7 columns, got {parts.Length} at line {lineIdx} in {resourceName}.");

                        int mainDN = ParseInt(parts[0], "mainDN", lineIdx, resourceName);
                        int branchDN = ParseInt(parts[1], "branchDN", lineIdx, resourceName);
                        double D = ParseDouble(parts[2], "D", lineIdx, resourceName);
                        double D1 = ParseDouble(parts[3], "D1", lineIdx, resourceName);
                        double s = ParseDouble(parts[4], "s", lineIdx, resourceName);
                        double L = ParseDouble(parts[5], "L", lineIdx, resourceName);
                        double L1 = ParseDouble(parts[6], "L1", lineIdx, resourceName);

                        var key = (mainDN, branchDN);
                        var targetMap =
                            currentSection switch
                            {
                                1 => _serie1,
                                2 => _serie2,
                                3 => _serie3,
                                _ => null
                            };
                        if (targetMap == null)
                            throw new System.Exception($"SpringCatalogLookup: Unexpected additional section (#{currentSection}) in {resourceName} at line {lineIdx}.");

                        if (targetMap.ContainsKey(key))
                            throw new System.Exception($"SpringCatalogLookup: duplicate key (mainDN={mainDN}, branchDN={branchDN}) in section {currentSection} at line {lineIdx} in {resourceName}.");

                        targetMap[key] = new SpringData { D = D, D1 = D1, s = s, L = L, L1 = L1 };
                        hasDataInCurrentSection = true;
                    }

                    _serie1Loaded = true;
                    _serie2Loaded = true;
                    _serie3Loaded = true;
                }
            }

            private static char DetectSeparator(string header)
            {
                if (header.Contains(';')) return ';';
                return ',';
            }

            private static int ParseInt(string v, string name, int lineIdx, string file)
            {
                if (!int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x))
                    throw new System.Exception($"SpringCatalogLookup: cannot parse int {name}='{v}' at line {lineIdx + 1} in {file}.");
                return x;
            }
            private static double ParseDouble(string v, string name, int lineIdx, string file)
            {
                if (!double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                    throw new System.Exception($"SpringCatalogLookup: cannot parse double {name}='{v}' at line {lineIdx + 1} in {file}.");
                return x;
            }

            public static SpringData Lookup(PipeSeriesEnum series, int dnMain, int dnBranch)
            {
                EnsureLoaded(series);
                var map = GetMap(series);
                var key = (dnMain, dnBranch);
                if (!map.TryGetValue(key, out var data))
                    throw new System.Exception(
                        $"SpringCatalogLookup: No catalog row found for main DN {dnMain} / branch DN {dnBranch} in series {series}.");
                return data;
            }
        }
    }

    internal sealed class AfgreningParallel : TeeMainRun
    {
        public AfgreningParallel(Handle source)
            : base(source, PipelineElementType.AfgreningParallel) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.AfgreningParallel);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            // TODO: implement macro; placeholder no-op
            return base.Route(g, topo, ctx, entryPort, entryZ, entrySlope);
        }
    }

    internal sealed class LigeAfgrening : TeeMainRun
    {
        public LigeAfgrening(Handle source)
            : base(source, PipelineElementType.LigeAfgrening) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.LigeAfgrening);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();

            // Emit main run via RouteMain
            // Determine if entry is from main or branch
            bool entryIsMain = ReferenceEquals(entryPort, MainPort1) || ReferenceEquals(entryPort, MainPort2);
            TPort? mainEntryPort = entryIsMain ? entryPort : null;
            var mainFlow = Variant.IsTwin ? FlowRole.Unknown : ResolveBondedFlowRole(topo);
            var mainExits = RouteMain(g, topo, ctx, mainEntryPort, entryZ, entrySlope, mainFlow);
            exits.AddRange(mainExits);

            var offsetBranch = ComputeTwinOffsets(System, Type, DnB);

            if (!Variant.IsTwin)
            {
                var branchFlow = ResolveBondedFlowRole(topo, BranchPort);
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = BranchPort.Node.Pos,
                        B = MidPoint.To3d(),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = branchFlow,
                        LTG = LTGBranch(Source),
                    }
                );
            }
            else if (DnM == DnB)
            {
                var firstStraight = new RoutedStraight(Source, this)
                {
                    A = BranchPort.Node.Pos.Z(OffsetMain.zUp),
                    B = MidPoint.To3d().Z(OffsetMain.zUp),
                    DN = DnB,
                    Material = Material,
                    DnSuffix = Variant.DnSuffix,
                    FlowRole = Variant.IsTwin
                        ? FlowRole.Return
                        : Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return,
                    LTG = LTGMain(Source),
                };
                g.Members.Add(firstStraight);

                if (Variant.IsTwin)
                {
                    var secondStraight = new RoutedStraight(Source, this)
                    {
                        A = BranchPort.Node.Pos.Z(OffsetMain.zLow),
                        B = MidPoint.To3d().Z(OffsetMain.zLow),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = FlowRole.Supply,
                        LTG = LTGMain(Source),
                    };
                    g.Members.Add(secondStraight);

                    var midpointFirst = firstStraight.A.MidPoint(firstStraight.B);
                    var midpointSecond = secondStraight.A.MidPoint(secondStraight.B);

                    g.Members.Add(
                        new RoutedRigid(Source, this)
                        {
                            P1 = midpointFirst,
                            P2 = midpointSecond,
                            Material = Material,
                        });
                }
            }
            else //Solve twin geometry with bends
            {
                //Project system to 2D plane through branch port,
                //mid point and plane normal is main run
                var branchOrigin = BranchPort.Node.Pos.To2d();
                var toMid = MidPoint - branchOrigin;
                var branchDistance = toMid.Length;
                if (branchDistance < 1e-9)
                {
                    prdDbg($"LigeAfgrening.Route: branch and main coincide for {Source}.");
                    return exits;
                }

                var branchDirPlan = toMid.GetNormal();

                Point2d branchStartUp = new Point2d(0.0, offsetBranch.zUp);
                Point2d branchEndUp = new Point2d(branchDistance, offsetBranch.zUp);
                Point2d mainCentreUp = new Point2d(branchDistance, OffsetMain.zUp);

                Point2d branchStartLow = new Point2d(0.0, offsetBranch.zLow);
                Point2d branchEndLow = new Point2d(branchDistance, offsetBranch.zLow);
                Point2d mainCentreLow = new Point2d(branchDistance, OffsetMain.zLow);

                double mainStubLength = PipeScheduleV2.GetPipeOd(System, DnM) / 2000.0 + 0.01;
                double bendOd = Geometry.GetBogRadius5D(DnB) / 1000.0;

                var filletReturn = Geometry.SolveBranchFillet(
                    branchStartUp,
                    branchEndUp,
                    mainCentreUp,
                    bendOd,
                    mainStubLength
                );

                if (filletReturn is null)
                {
                    prdDbg($"LigeAfgrening.Route: unable to solve return branch fillet for {Source}.");
                    return exits;
                }

                var filletSupply = Geometry.SolveBranchFillet(
                    branchStartLow,
                    branchEndLow,
                    mainCentreLow,
                    bendOd,
                    mainStubLength
                );

                if (filletSupply is null)
                {
                    prdDbg($"LigeAfgrening.Route: unable to solve supply branch fillet for {Source}.");
                    return exits;
                }

                Point3d ToWorld(Point2d local)
                {
                    var plan = branchOrigin + branchDirPlan.MultiplyBy(local.X);
                    return new Point3d(plan.X, plan.Y, local.Y);
                }

                (RoutedStraight stub, RoutedBend bend, RoutedStraight branch) EmitTwinBranch(
                    Geometry.BranchFilletSolution fillet,
                    Point2d branchStartLocal,
                    Point2d mainCentreLocal,
                    FlowRole flowRole)
                {
                    var branchStartWorld = ToWorld(branchStartLocal);
                    var branchTangentWorld = ToWorld(fillet.BranchTangent);
                    var mainTangentWorld = ToWorld(fillet.MainTangent);
                    var tangentIntersectionWorld = ToWorld(fillet.TangentIntersection);
                    var mainCentreWorld = ToWorld(mainCentreLocal);

                    var branch = new RoutedStraight(Source, this)
                    {
                        A = branchStartWorld.ModZ(entryZ),
                        B = branchTangentWorld.ModZ(entryZ),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flowRole,
                        LTG = LTGBranch(Source),
                    };

                    var bend = new RoutedBend(Source, this)
                    {
                        A = branchTangentWorld.ModZ(entryZ),
                        B = mainTangentWorld.ModZ(entryZ),
                        T = tangentIntersectionWorld.ModZ(entryZ),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flowRole,
                        LTG = LTGBranch(Source),
                    };

                    var stub = new RoutedStraight(Source, this)
                    {
                        A = mainTangentWorld.ModZ(entryZ),
                        B = mainCentreWorld.ModZ(entryZ),
                        DN = DnB,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = flowRole,
                        LTG = LTGBranch(Source),
                    };

                    return (stub, bend, branch);
                }

                var r1 = EmitTwinBranch(filletReturn.Value, branchStartUp, mainCentreUp, FlowRole.Return);
                var r2 = EmitTwinBranch(filletSupply.Value, branchStartLow, mainCentreLow, FlowRole.Supply);

                g.Members.Add(r1.stub);
                g.Members.Add(r1.bend);
                g.Members.Add(r1.branch);
                g.Members.Add(r2.stub);
                g.Members.Add(r2.bend);
                g.Members.Add(r2.branch);

                var midpointReturn = r1.branch.A.MidPoint(r1.branch.B);
                var midpointSupply = r2.branch.A.MidPoint(r2.branch.B);

                g.Members.Add(
                    new RoutedRigid(Source, this)
                    {
                        P1 = midpointReturn,
                        P2 = midpointSupply,
                        Material = Material,
                    });
            }

            // Add branch exit
            exits.Add((BranchPort, entryZ, entrySlope));

            return exits;
        }
    }

    internal sealed class Stikafgrening : TeeMainRun
    {
        public Stikafgrening(Handle source)
            : base(source, PipelineElementType.Stikafgrening) { }

        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Stikafgrening);
        }

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double exitSlope)
        {
            // TODO: implement macro; placeholder no-op
            return base.Route(g, topo, ctx, entryPort, entryZ, exitSlope);
        }
    }
}

