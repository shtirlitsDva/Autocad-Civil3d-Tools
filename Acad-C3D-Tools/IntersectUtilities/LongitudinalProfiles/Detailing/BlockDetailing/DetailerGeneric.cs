using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    /// <summary>
    /// Handles general components. RIGHTSIZE policy is applied outside via enum grouping (factory decides handler).
    /// </summary>
    public sealed class DetailerGeneric : BlockDetailerBase, IBlockDetailer
    {
        public void Detail(BlockReference sourceBlock, BlockDetailingContext context, PipelineElementType elementType)
        {
            var (station, _) = GetStationOffset(sourceBlock, context);
            if (!IsWithinProfileView(station, context)) return;

            Point3d insertion = ComputeInsertionPoint(station, context);
            BlockReference target = CreateBlock(context.Database, context.ComponentBlockName, insertion);

            string type = sourceBlock.ReadDynamicCsvProperty(DynamicProperty.Type) ?? string.Empty;
            SetAttribute(target, "LEFTSIZE", type);
            SetAttribute(target, "RIGHTSIZE", "");

            WriteSourceReference(target, context, sourceBlock.Handle.ToString(), station);
        }
    }
}


