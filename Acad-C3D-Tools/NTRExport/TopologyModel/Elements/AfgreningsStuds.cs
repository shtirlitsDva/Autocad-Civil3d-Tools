using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System.Linq;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;

using static IntersectUtilities.UtilsCommon.Utils;
using static NTRExport.Utils.Utils;

namespace NTRExport.TopologyModel
{
    internal class AfgreningsStuds : TFitting
    {
        public AfgreningsStuds(Handle source)
            : base(source, PipelineElementType.Afgreningsstuds) { }
        public TPort Main => Ports.First(x => x.Role == PortRole.Main);
        public TPort Branch => Ports.First(x => x.Role == PortRole.Branch);
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
        protected override void ConfigureAllowedKinds(HashSet<PipelineElementType> allowed)
        {
            allowed.Clear();
            allowed.Add(PipelineElementType.Afgreningsstuds);
        }

        public override string DotLabelForTest()
        {
            return $"{Source.ToString()} / {this.GetType().Name}\n{DnLabel()}";
        }
        public override string DnLabel() => $"{DnM.ToString()}/{DnB.ToString()}";

        public override List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>
            {
                (Ports.Where(x => x != entryPort).First(), entryZ, entrySlope)
            };

            var offsetBranch = ComputeTwinOffsets(System, Type, DnB);
            var offsetMain = ComputeTwinOffsets(System, Type, DnM);

            if (!Variant.IsTwin)
            {
                g.Members.Add(
                    new RoutedStraight(Source, this)
                    {
                        A = Main.Node.Pos.Z(entryZ),
                        B = Branch.Node.Pos.Z(entryZ),
                        DN = DN,
                        Material = Material,
                        DnSuffix = Variant.DnSuffix,
                        FlowRole = Type == PipeTypeEnum.Frem ? FlowRole.Supply : FlowRole.Return,
                        LTG = LTGBranch(Source),
                    }
                );
            }
            else if (DnM == DnB)
            {
                var firstStraight = new RoutedStraight(Source, this)
                {
                    A = Branch.Node.Pos.Z(offsetMain.zUp + entryZ),
                    B = Main.Node.Pos.Z(offsetMain.zUp + entryZ),
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
                        A = Branch.Node.Pos.Z(offsetMain.zLow + entryZ),
                        B = Main.Node.Pos.Z(offsetMain.zLow + entryZ),
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
                var branchOrigin = Branch.Node.Pos.To2d();
                var toMid = Main.Node.Pos.To2d() - branchOrigin;
                var branchDistance = toMid.Length;
                if (branchDistance < 1e-9)
                {
                    prdDbg($"LigeAfgrening.Route: branch and main coincide for {Source}.");
                    return exits;
                }

                var branchDirPlan = toMid.GetNormal();

                Point2d branchStartUp = new Point2d(0.0, offsetBranch.zUp);
                Point2d branchEndUp = new Point2d(branchDistance, offsetBranch.zUp);
                Point2d mainCentreUp = new Point2d(branchDistance, offsetMain.zUp);

                Point2d branchStartLow = new Point2d(0.0, offsetBranch.zLow);
                Point2d branchEndLow = new Point2d(branchDistance, offsetBranch.zLow);
                Point2d mainCentreLow = new Point2d(branchDistance, offsetMain.zLow);

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

            return exits;
        }
    }
}

