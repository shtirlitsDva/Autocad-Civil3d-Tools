using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using MoreLinq;

using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.ComponentSchedule;
using static IntersectUtilities.DynamicBlocks.PropertyReader;
using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using IntersectUtilities.UtilsCommon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities
{
    public class PipelineSizeArray
    {
        public SizeEntry[] SizeArray;
        public int Length { get => SizeArray.Length; }
        public PipelineSizesDirection Direction { get; }
        public int StartingDn { get; }
        public SizeEntry this[int index] { get => SizeArray[index]; }
        public int MaxDn { get => SizeArray.MaxBy(x => x.DN).FirstOrDefault().DN; }
        public int MinDn { get => SizeArray.MinBy(x => x.DN).FirstOrDefault().DN; }
        /// <summary>
        /// SizeArray listing sizes, station ranges and jacket diameters.
        /// Use empty brs collection or omit it to force size table based on curves.
        /// </summary>
        /// <param name="al">Current alignment.</param>
        /// <param name="brs">All transitions belonging to the current alignment.</param>
        /// <param name="curves">All pipline curves belonging to the current alignment.</param>
        public PipelineSizeArray(Alignment al, HashSet<Curve> curves, HashSet<BlockReference> brs = default)
        {
            #region Read CSV
            System.Data.DataTable dynBlocks = default;
            try
            {
                dynBlocks = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
            }
            catch (System.Exception ex)
            {
                prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                prdDbg(ex);
                throw;
            }
            if (dynBlocks == default)
            {
                prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                throw new System.Exception("Failed to read FJV Dynamiske Komponenter.csv");
            }
            #endregion

            #region Direction
            ////Determine pipe size direction
            #region Old direction method
            ////This is a flawed method using only curves, see below
            //int maxDn = PipeSchedule.GetPipeDN(curves.MaxBy(x => PipeSchedule.GetPipeDN(x)).FirstOrDefault());
            //int minDn = PipeSchedule.GetPipeDN(curves.MinBy(x => PipeSchedule.GetPipeDN(x)).FirstOrDefault());

            //HashSet<(Curve curve, double dist)> curveDistTuples =
            //                new HashSet<(Curve curve, double dist)>();

            //Point3d samplePoint = al.GetPointAtDist(0);

            //foreach (Curve curve in curves)
            //{
            //    if (curve.GetDistanceAtParameter(curve.EndParam) < 0.0001) continue;
            //    Point3d closestPoint = curve.GetClosestPointTo(samplePoint, false);
            //    if (closestPoint != default)
            //        curveDistTuples.Add(
            //            (curve, samplePoint.DistanceHorizontalTo(closestPoint)));
            //}

            //Curve closestCurve = curveDistTuples.MinBy(x => x.dist).FirstOrDefault().curve;

            //StartingDn = PipeSchedule.GetPipeDN(closestCurve); 
            #endregion

            HashSet<Entity> entities = new HashSet<Entity>();
            entities.UnionWith(curves);
            if (brs != default) entities.UnionWith(brs);

            var sortedByStation = entities.OrderBy(x => GetStation(al, x)).ToList();
            var maxDn = entities.Max(x => GetDn(x, dynBlocks));
            var minDn = entities.Min(x => GetDn(x, dynBlocks));
            StartingDn = GetDn(sortedByStation[0], dynBlocks);

            //2023.04.12: A case discovered where there's a reducer after which there's only blocks
            //till the alignment's end. This confuses the code to think that the last size
            //don't exists, as it looks only at polylines present.
            //So, we need to check for presence of reducers to definitely rule out one size case.
            var reducers = brs?.Where(
                x => x.ReadDynamicCsvProperty(DynamicProperty.Type, dynBlocks, false) == "Reduktion");
            if (reducers != null && reducers.Count() != 0)
            {
                List<int> sizes = new List<int>();
                foreach (var reducer in reducers)
                {
                    sizes.Add(
                        ReadComponentDN1Int(reducer, dynBlocks));
                    sizes.Add(
                        ReadComponentDN2Int(reducer, dynBlocks));
                }
                string name = al.Name;
                minDn = sizes.Min();
                maxDn = sizes.Max();
            }

            if (maxDn == minDn) Direction = PipelineSizesDirection.OneSize;
            else if (StartingDn == minDn) Direction = PipelineSizesDirection.SmallToLargeAscending;
            else if (StartingDn == maxDn) Direction = PipelineSizesDirection.LargeToSmallDescending;
            else Direction = PipelineSizesDirection.Unknown;

            if (Direction == PipelineSizesDirection.Unknown)
                throw new System.Exception($"Alignment {al.Name} could not determine pipeline sizes direction!");
            #endregion

            //Filter brs
            if (brs != default)
                brs = brs.Where(x =>
                    IsTransition(x, dynBlocks) ||
                    IsXModel(x, dynBlocks)
                    ).ToHashSet();

            //Dispatcher constructor
            if (brs == default || brs.Count == 0 || Direction == PipelineSizesDirection.OneSize)
                SizeArray = ConstructWithCurves(al, curves);
            else SizeArray = ConstructWithBlocks(al, curves, brs, dynBlocks);
        }
        public PipelineSizeArray(SizeEntry[] sizeArray) { SizeArray = sizeArray; }
        public PipelineSizeArray GetPartialSizeArrayForPV(ProfileView pv)
        {
            var list = this.GetIndexesOfSizesAppearingInProfileView(pv.StationStart, pv.StationEnd);
            SizeEntry[] partialAr = new SizeEntry[list.Count];
            for (int i = 0; i < list.Count; i++) partialAr[i] = this[list[i]];
            return new PipelineSizeArray(partialAr);
        }
        public SizeEntry GetSizeAtStation(double station)
        {
            for (int i = 0; i < SizeArray.Length; i++)
            {
                SizeEntry curEntry = SizeArray[i];
                //(stations are END stations!)
                if (station < curEntry.EndStation) return curEntry;
            }
            return default;
        }
        private int GetDn(Entity entity, System.Data.DataTable dynBlocks)
        {
            if (entity is Polyline pline)
                return PipeSchedule.GetPipeDN(pline);
            else if (entity is BlockReference br)
            {
                if (br.ReadDynamicCsvProperty(DynamicProperty.Type, dynBlocks, false) == "Afgreningsstuds")
                    return ReadComponentDN2Int(br, dynBlocks);
                else return ReadComponentDN1Int(br, dynBlocks);
            }
                
            else throw new System.Exception("Invalid entity type");
        }
        private double GetStation(Alignment alignment, Entity entity)
        {
            double station = 0;
            double offset = 0;

            switch (entity)
            {
                case Polyline pline:
                    double l = pline.Length;
                    Point3d p = pline.GetPointAtDist(l / 2);
                    alignment.StationOffset(p.X, p.Y, 5.0, ref station, ref offset);
                    break;
                case BlockReference block:
                    alignment.StationOffset(block.Position.X, block.Position.Y, 5.0, ref station, ref offset);
                    break;
                default:
                    throw new Exception("Invalid entity type");
            }
            return station;
        }
        public override string ToString()
        {
            string output = "";
            for (int i = 0; i < SizeArray.Length; i++)
            {
                output +=
                    $"{SizeArray[i].DN.ToString("D3")} || " +
                    $"{SizeArray[i].StartStation.ToString("0000.00")} - {SizeArray[i].EndStation.ToString("0000.00")} || " +
                    $"{SizeArray[i].Kod.ToString("0")}\n";
            }

            return output;
        }
        private List<int> GetIndexesOfSizesAppearingInProfileView(double pvStationStart, double pvStationEnd)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < SizeArray.Length; i++)
            {
                SizeEntry curEntry = SizeArray[i];
                if (pvStationStart < curEntry.EndStation &&
                    curEntry.StartStation < pvStationEnd) indexes.Add(i);
            }
            return indexes;
        }
        private SizeEntry[] ConstructWithCurves(Alignment al, HashSet<Curve> curves)
        {
            List<SizeEntry> sizes = new List<SizeEntry>();
            double stepLength = 0.1;
            double alLength = al.Length;
            int nrOfSteps = (int)(alLength / stepLength);
            int previousDn = 0;
            int currentDn = 0;
            double previousKod = 0;
            double currentKod = 0;
            for (int i = 0; i < nrOfSteps + 1; i++)
            {
                double curStationBA = stepLength * i;
                Point3d curSamplePoint = default;
                try { curSamplePoint = al.GetPointAtDist(curStationBA); }
                catch (System.Exception) { continue; }

                HashSet<(Curve curve, double dist, double kappeOd)> curveDistTuples =
                    new HashSet<(Curve curve, double dist, double kappeOd)>();

                foreach (Curve curve in curves)
                {
                    //if (curve.GetDistanceAtParameter(curve.EndParam) < 1.0) continue;
                    Point3d closestPoint = curve.GetClosestPointTo(curSamplePoint, false);
                    if (closestPoint != default)
                        curveDistTuples.Add(
                            (curve, curSamplePoint.DistanceHorizontalTo(closestPoint),
                                PipeSchedule.GetPipeKOd(curve)));
                }
                var result = curveDistTuples.MinBy(x => x.dist).FirstOrDefault();
                //Detect current dn and kod
                currentDn = PipeSchedule.GetPipeDN(result.curve);
                currentKod = result.kappeOd;
                if (currentDn != previousDn || !currentKod.Equalz(previousKod, 1e-6))
                {
                    //Set the previous segment end station unless there's 0 segments
                    if (sizes.Count != 0)
                    {
                        SizeEntry toUpdate = sizes[sizes.Count - 1];
                        sizes[sizes.Count - 1] = new SizeEntry(toUpdate.DN, toUpdate.StartStation, curStationBA, toUpdate.Kod);
                    }
                    //Add the new segment; remember, 0 is because the station will be set next iteration
                    //see previous line
                    if (i == 0) sizes.Add(new SizeEntry(currentDn, 0, 0, result.kappeOd));
                    else sizes.Add(new SizeEntry(currentDn, sizes[sizes.Count - 1].EndStation, 0, result.kappeOd));
                }
                //Hand over DN to cache in "previous" variable
                previousDn = currentDn;
                previousKod = currentKod;
                if (i == nrOfSteps)
                {
                    SizeEntry toUpdate = sizes[sizes.Count - 1];
                    sizes[sizes.Count - 1] = new SizeEntry(toUpdate.DN, toUpdate.StartStation, al.Length, toUpdate.Kod);
                }
            }

            return sizes.ToArray();
        }
        /// <summary>
        /// Construct SizeArray based on blocks.
        /// </summary>
        /// <param name="al">Current alignment.</param>
        /// <param name="curves">Curves are only here to provide sizes to F- and Y-Models.</param>
        /// <param name="brs">Size changing blocks and transitions (F- and Y-Models).</param>
        /// <param name="dt">Dynamic block datatable.</param>
        /// <returns>SizeArray with sizes for current alignment.</returns>
        private SizeEntry[] ConstructWithBlocks(Alignment al, HashSet<Curve> curves, HashSet<BlockReference> brs, System.Data.DataTable dt)
        {
            BlockReference[] brsArray = default;
            //Old ordering
            //if (Direction == PipelineSizesDirection.SmallToLargeAscending)
            //    brsArray = brs.OrderBy(x => ReadComponentDN2Int(x, dt)).ToArray();
            //else if (Direction == PipelineSizesDirection.LargeToSmallDescending)
            //    brsArray = brs.OrderByDescending(x => ReadComponentDN2Int(x, dt)).ToArray();
            //else brs.ToArray();

            //New ordering based on station on alignment
            //prdDbg("Using new SizeArray ordering method! Beware!");
            brsArray = brs.OrderBy(x => al.StationAtPoint(x)).ToArray();

            List<SizeEntry> sizes = new List<SizeEntry>();
            double alLength = al.Length;

            int dn = 0;
            double start = 0.0;
            double end = 0.0;
            double kod = 0.0;

            for (int i = 0; i < brsArray.Length; i++)
            {
                BlockReference curBr = brsArray[i];

                if (i == 0)
                {
                    start = 0.0;
                    end = al.StationAtPoint(curBr);

                    //First iteration case
                    if (PipelineSizeArray.IsTransition(curBr, dt))
                    {
                        dn = GetDirectionallyCorrectDn(curBr, Side.Left, dt);
                        //Point3d p3d = al.GetClosestPointTo(curBr.Position, false);
                        //al.StationOffset(p3d.X, p3d.Y, ref end, ref offset);
                        kod = GetDirectionallyCorrectKod(curBr, Side.Left, dt);
                    }
                    else
                    {//F-Model og Y-Model
                        var ends = curBr.GetAllEndPoints();
                        //Determine connected curves
                        var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                        //Find the curves earlier up the alignment
                        var minCurve = query.MinBy(
                            x => al.StationAtPoint(
                                x.GetPointAtDist(
                                    x.GetDistAtPoint(x.EndPoint) / 2.0)))
                            .FirstOrDefault();

                        dn = PipeSchedule.GetPipeDN(minCurve);
                        kod = PipeSchedule.GetPipeKOd(minCurve);
                    }

                    sizes.Add(new SizeEntry(dn, start, end, kod));

                    //Only one member array case
                    //This is an edge case of first iteration
                    if (brsArray.Length == 1)
                    {
                        start = end;
                        end = alLength;

                        if (PipelineSizeArray.IsTransition(curBr, dt))
                        {
                            dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                            kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                        }
                        else
                        {//F-Model og Y-Model
                            var ends = curBr.GetAllEndPoints();
                            //Determine connected curves
                            var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                            //Find the curves further down the alignment
                            var maxCurve = query.MaxBy(
                                x => al.StationAtPoint(
                                    x.GetPointAtDist(
                                        x.GetDistAtPoint(x.EndPoint) / 2.0)))
                                .FirstOrDefault();

                            dn = PipeSchedule.GetPipeDN(maxCurve);
                            kod = PipeSchedule.GetPipeKOd(maxCurve);
                        }

                        sizes.Add(new SizeEntry(dn, start, end, kod));
                        //This guards against executing further code
                        continue;
                    }
                }

                //General case
                if (i != brsArray.Length - 1)
                {
                    BlockReference nextBr = brsArray[i + 1];
                    start = end;
                    end = al.StationAtPoint(nextBr);

                    if (PipelineSizeArray.IsTransition(curBr, dt))
                    {
                        dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                        //Point3d p3d = al.GetClosestPointTo(nextBr.Position, false);
                        //al.StationOffset(p3d.X, p3d.Y, ref end, ref offset);
                        kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                    }
                    else
                    {
                        var ends = curBr.GetAllEndPoints();
                        //Determine connected curves
                        var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                        //Find the curves further down the alignment
                        var maxCurve = query.MaxBy(
                            x => al.StationAtPoint(
                                x.GetPointAtDist(
                                    x.GetDistAtPoint(x.EndPoint) / 2.0)))
                            .FirstOrDefault();

                        dn = PipeSchedule.GetPipeDN(maxCurve);
                        kod = PipeSchedule.GetPipeKOd(maxCurve);
                    }

                    sizes.Add(new SizeEntry(dn, start, end, kod));
                    //This guards against executing further code
                    continue;
                }

                //And here ends the last iteration
                start = end;
                end = alLength;

                if (PipelineSizeArray.IsTransition(curBr, dt))
                {
                    dn = GetDirectionallyCorrectDn(curBr, Side.Right, dt);
                    kod = GetDirectionallyCorrectKod(curBr, Side.Right, dt);
                }
                else
                {
                    var ends = curBr.GetAllEndPoints();
                    //Determine connected curves
                    var query = curves.Where(x => ends.Any(y => y.IsOnCurve(x, 0.005)));
                    //Find the curves further down the alignment
                    var maxCurve = query.MaxBy(
                        x => al.StationAtPoint(
                            x.GetPointAtDist(
                                x.GetDistAtPoint(x.EndPoint) / 2.0)))
                        .FirstOrDefault();

                    dn = PipeSchedule.GetPipeDN(maxCurve);
                    kod = PipeSchedule.GetPipeKOd(maxCurve);
                }

                sizes.Add(new SizeEntry(dn, start, end, kod));
            }

            return sizes.ToArray();
        }
        private int GetDirectionallyCorrectDn(BlockReference br, Side side, System.Data.DataTable dt)
        {
            switch (Direction)
            {
                case PipelineSizesDirection.SmallToLargeAscending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN2Int(br, dt);
                        case Side.Right:
                            return ReadComponentDN1Int(br, dt);
                    }
                    break;
                case PipelineSizesDirection.LargeToSmallDescending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN1Int(br, dt);
                        case Side.Right:
                            return ReadComponentDN2Int(br, dt);
                    }
                    break;
            }
            return 0;
        }
        private double GetDirectionallyCorrectKod(BlockReference br, Side side, System.Data.DataTable dt)
        {
            switch (Direction)
            {
                case PipelineSizesDirection.SmallToLargeAscending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN2KodDouble(br, dt);
                        case Side.Right:
                            return ReadComponentDN1KodDouble(br, dt);
                    }
                    break;
                case PipelineSizesDirection.LargeToSmallDescending:
                    switch (side)
                    {
                        case Side.Left:
                            return ReadComponentDN1KodDouble(br, dt);
                        case Side.Right:
                            return ReadComponentDN2KodDouble(br, dt);
                    }
                    break;
            }
            return 0;
        }
        private static bool IsTransition(BlockReference br, System.Data.DataTable dynBlocks)
        {
            string type = ReadStringParameterFromDataTable(br.RealName(), dynBlocks, "Type", 0);

            if (type == null) throw new System.Exception($"Block with name {br.RealName()} does not exist " +
                $"in Dynamiske Komponenter!");

            return type == "Reduktion";
        }
        ///Determines whether the block is an F- or Y-Model
        private static bool IsXModel(BlockReference br, System.Data.DataTable dynBlocks)
        {
            string type = ReadStringParameterFromDataTable(br.RealName(), dynBlocks, "Type", 0);

            if (type == null) throw new System.Exception($"Block with name {br.RealName()} does not exist " +
                $"in Dynamiske Komponenter!");

            HashSet<string> transitionTypes = new HashSet<string>()
            {
                "Y-Model",
                "F-Model"
            };

            return transitionTypes.Contains(type);
        }
        internal void Reverse()
        {
            Array.Reverse(this.SizeArray);
        }
        /// <summary>
        /// Unknown - Should throw an exception
        /// OneSize - Cannot be constructed with blocks
        /// SmallToLargeAscending - Small sizes first, blocks preferred
        /// LargeToSmallAscending - Large sizes first, blocks preferred
        /// </summary>
        public enum PipelineSizesDirection
        {
            Unknown, //Should throw an exception
            OneSize, //Cannot be constructed with blocks
            SmallToLargeAscending, //Blocks preferred
            LargeToSmallDescending //Blocks preferred
        }
        private enum Side
        {
            //Left means towards the start of alignment
            Left,
            //Right means towards the end of alignment
            Right
        }
    }
    public struct SizeEntry
    {
        public readonly int DN;
        public readonly double StartStation;
        public readonly double EndStation;
        public readonly double Kod;

        public SizeEntry(int dn, double startStation, double endStation, double kod)
        {
            DN = dn; StartStation = startStation; EndStation = endStation; Kod = kod;
        }
    }
}
