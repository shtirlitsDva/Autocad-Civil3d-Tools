using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    public sealed class DetailerAfgrening : BlockDetailerBase, IBlockDetailer
    {
        public void Detail(BlockReference sourceBlock, BlockDetailingContext context, PipelineElementType elementType)
        {
            var (station, _) = GetStationOffset(sourceBlock, context);
            if (!IsWithinProfileView(station, context)) return;

            Point3d insertion = ComputeInsertionPoint(station, context);
            BlockReference target = CreateBlock(context.Database, context.ComponentBlockName, insertion);

            string type = sourceBlock.ReadDynamicCsvProperty(DynamicProperty.Type) ?? string.Empty;
            SetAttribute(target, "LEFTSIZE", type);

            string right = context.PipelineData.ReadPropertyString(sourceBlock, context.PipelineDataKeys.BranchesOffToAlignment);
            SetAttribute(target, "RIGHTSIZE", right);

            WriteSourceReference(target, context, sourceBlock.Handle.ToString(), station);
        }
    }
}