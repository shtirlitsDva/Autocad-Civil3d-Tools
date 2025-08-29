using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    /// <summary>
    /// Handles BUEROR1/BUEROR2 blocks. Computes mid station and length using nested MuffeIntern blocks.
    /// </summary>
    public sealed class BueRorDetailer : BlockDetailerBase
    {
        public override bool CanHandle(BlockReference sourceBlock, BlockDetailingContext context)
        {
            string name = RealName(sourceBlock);
            return name == "BUEROR1" || name == "BUEROR2";
        }

        public override void Detail(BlockReference sourceBlock, BlockDetailingContext context)
        {
            List<Point3d> locations = FindMuffeInternWorldPositions(sourceBlock, context.Transaction);
            if (locations.Count == 0)
                return;
            if (locations.Count > 2)
            {
                // Keep parity with original behavior: warn but proceed with first/last
                UtilsCommon.Utils.prdDbg($"Block: {sourceBlock.Handle} have more than two locations!");
            }

            // First/Last stations
            double firstStation = 0, secondStation = 0, offset = 0;
            Point3d pos = default;
            pos = locations.First();
            var res1 = context.Alignment.GetStationOffset(pos);
            firstStation = res1.Station; offset = res1.Offset;
            pos = locations.Last();
            var res2 = context.Alignment.GetStationOffset(pos);
            secondStation = res2.Station; offset = res2.Offset;

            double station = firstStation > secondStation
                ? secondStation + (firstStation - secondStation) / 2.0
                : firstStation + (secondStation - firstStation) / 2.0;

            double bueRorLength = Math.Abs(firstStation - secondStation);

            if (!(station > context.ProfileViewStartStation && station < context.ProfileViewEndStation))
                return;

            Point3d insertion = ComputeInsertionPoint(station, context);
            BlockReference target = CreateBlock(context.Database, context.BueRorBlockName, insertion);

            // Set dynamic length property
            DynamicBlockReferencePropertyCollection dbrpc = target.DynamicBlockReferencePropertyCollection;
            foreach (DynamicBlockReferenceProperty dbrp in dbrpc)
            {
                if (dbrp.PropertyName == "Length")
                {
                    dbrp.Value = Math.Abs(bueRorLength);
                }
            }

            SetAttribute(target, "LGD", Math.Abs(bueRorLength).ToString("0.0") + " m");

            // Augmented type text derived via ComponentSchedule.ReadComponentType(sourceBlock, dt)
            string augmentedType = ComponentSchedule.ReadComponentType(sourceBlock, context.ComponentDataTable);
            SetAttribute(target, "TEXT", augmentedType);

            WriteSourceReference(target, context, sourceBlock.Handle.ToString(), station);
        }

        // No reflection here; station/offset comes from context.ComputeStationOffset

        private static List<Point3d> FindMuffeInternWorldPositions(BlockReference br, Transaction tx)
        {
            var btr = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
            var points = new List<Point3d>();
            foreach (ObjectId id in btr)
            {
                if (!id.ObjectClass.IsDerivedFrom(RXClass.GetClass(typeof(BlockReference))))
                    continue;
                BlockReference nested = (BlockReference)tx.GetObject(id, OpenMode.ForRead);
                if (!nested.Name.Contains("MuffeIntern"))
                    continue;
                Point3d wPt = nested.Position.TransformBy(br.BlockTransform);
                points.Add(wPt);
            }
            return points;
        }
    }
}


