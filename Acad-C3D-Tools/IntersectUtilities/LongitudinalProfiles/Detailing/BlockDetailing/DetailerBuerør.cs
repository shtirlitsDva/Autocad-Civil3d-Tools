using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    /// <summary>
    /// Handles BUEROR1/BUEROR2 blocks. Computes mid station and length using nested MuffeIntern blocks.
    /// </summary>
    public sealed class DetailerBuerør : BlockDetailerBase, IBlockDetailer
    {
        public void Detail(BlockReference sourceBlock, BlockDetailingContext context, UtilsCommon.Enums.PipelineElementType elementType)
        {
            List<Point3d> locations = UtilsCommon.ComponentPorts.Read(sourceBlock, context.Transaction)
                .Select(p => p.Position)
                .ToList();
            if (locations.Count == 0) return;
            if (locations.Count > 2)
                UtilsCommon.Utils.prdDbg($"Block: {sourceBlock.Handle} have more than two locations!");

            // First/Last stations
            double firstStation = 0, secondStation = 0, offset = 0;
            Point3d pos = default;
            pos = locations.First();
            if (!TryGetProjectedStationOffset(context, sourceBlock, pos, out StationOffsetResult res1))
                return;
            firstStation = res1.Station; offset = res1.Offset;
            pos = locations.Last();
            if (!TryGetProjectedStationOffset(context, sourceBlock, pos, out StationOffsetResult res2))
                return;
            secondStation = res2.Station; offset = res2.Offset;

            double station = firstStation > secondStation
                ? secondStation + (firstStation - secondStation) / 2.0
                : firstStation + (secondStation - firstStation) / 2.0;

            double bueRorLength = Math.Abs(firstStation - secondStation);

            if (!IsWithinProfileView(station, context))
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

            string augmentedType = ComponentSchedule.ReadDynamicCsvProperty(
                sourceBlock, UtilsCommon.Enums.DynamicProperty.Type);
            SetAttribute(target, "TEXT", augmentedType);

            WriteSourceReference(target, context, sourceBlock.Handle.ToString(), station);
        }

        private static bool TryGetProjectedStationOffset(
            BlockDetailingContext context,
            BlockReference sourceBlock,
            Point3d point,
            out StationOffsetResult result)
        {
            result = default;
            try
            {
                Point3d location = context.AlignmentPolyline.GetClosestPointTo(point, false);
                result = context.Alignment.GetStationOffset(location);
                return true;
            }
            catch (Autodesk.Civil.PointNotOnEntityException)
            {
                UtilsCommon.Utils.prdDbg(
                    $"Skipping Buerør block {sourceBlock.Handle}: MuffeIntern point {point} cannot be stationed on the alignment.");
                return false;
            }
        }
    }
}


