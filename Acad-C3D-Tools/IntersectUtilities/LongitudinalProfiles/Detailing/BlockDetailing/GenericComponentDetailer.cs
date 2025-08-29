using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    /// <summary>
    /// Handles all standard component blocks except reductions, welds and bue rør.
    /// Responsible for LEFTSIZE/RIGHTSIZE attributes and source refs.
    /// </summary>
    public sealed class GenericComponentDetailer : BlockDetailerBase
    {
        private static readonly string[] RightSizeFromBranchTypes = new[]
        {
            "Parallelafgrening",
            "Lige afgrening",
            "Afgrening med spring",
            "Afgrening, parallel",
            "Svejsetee",
            "Preskobling tee",
            "Stikafgrening",
        };

        public override bool CanHandle(BlockReference sourceBlock, BlockDetailingContext context)
        {
            string type = sourceBlock.ReadDynamicCsvProperty(DynamicProperty.Type);
            if (type == null)
                return false;

            if (RealName(sourceBlock) == "BUEROR1" || RealName(sourceBlock) == "BUEROR2")
                return false;

            if (type == "Reduktion" || type == "Svejsning")
                return false;

            return true;
        }

        public override void Detail(BlockReference sourceBlock, BlockDetailingContext context)
        {
            var (station, _) = GetStationOffset(sourceBlock, context);
            if (!IsWithinProfileView(station, context))
                return;

            Point3d insertion = ComputeInsertionPoint(station, context);
            BlockReference target = CreateBlock(context.Database, context.ComponentBlockName, insertion);

            string type = sourceBlock.ReadDynamicCsvProperty(DynamicProperty.Type) ?? string.Empty;
            SetAttribute(target, "LEFTSIZE", type);

            if (RightSizeFromBranchTypes.Contains(type))
            {
                string right = context.PipelineData.ReadPropertyString(sourceBlock, context.PipelineDataKeys.BranchesOffToAlignment);
                SetAttribute(target, "RIGHTSIZE", right);
            }
            else if (type == "Afgreningsstuds" || type == "Svanehals")
            {
                string right = context.PipelineData.ReadPropertyString(sourceBlock, context.PipelineDataKeys.BelongsToAlignment);
                SetAttribute(target, "RIGHTSIZE", right);
            }
            else
            {
                SetAttribute(target, "RIGHTSIZE", "");
            }

            WriteSourceReference(target, context, sourceBlock.Handle.ToString(), station);
        }
    }
}


