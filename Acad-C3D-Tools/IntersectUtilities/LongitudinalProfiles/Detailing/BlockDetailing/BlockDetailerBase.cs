using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    /// <summary>
    /// Shared behavior for block detailers.
    /// </summary>
    public abstract class BlockDetailerBase : IBlockDetailer
    {
        public abstract bool CanHandle(BlockReference sourceBlock, BlockDetailingContext context);

        public abstract void Detail(BlockReference sourceBlock, BlockDetailingContext context);

        protected bool IsWithinProfileView(double station, BlockDetailingContext context)
        {
            return station >= context.ProfileViewStartStation && station <= context.ProfileViewEndStation;
        }

        protected (double station, double offset) GetStationOffset(BlockReference br, BlockDetailingContext context)
        {
            var location = context.AlignmentPolyline.GetClosestPointTo(br.Position, false);
            StationOffsetResult res = context.Alignment.GetStationOffset(location);
            return (res.Station, res.Offset);
        }

        protected Point3d ComputeInsertionPoint(double station, BlockDetailingContext context)
        {
            double sampledMidElevation = context.SampleElevationAtStation(station);
            double x = context.OriginX + station - context.ProfileViewStartStation;
            // Expecting profileViewStyle.GraphStyle.VerticalExaggeration via dynamic
            double verticalExaggeration = 1.0;
            try
            {
                dynamic pvs = context.ProfileViewStyle;
                verticalExaggeration = (double)pvs.GraphStyle.VerticalExaggeration;
            }
            catch
            {
                verticalExaggeration = 1.0;
            }

            double y = context.OriginY + (sampledMidElevation - context.ProfileViewBottomElevation) * verticalExaggeration;
            return new Point3d(x, y, 0);
        }

        protected BlockReference CreateBlock(Database db, string name, Point3d insertionPoint) =>
            db.CreateBlockWithAttributes(name, insertionPoint);

        protected void SetAttribute(BlockReference br, string tag, string value) =>
            br.SetAttributeStringValue(tag, value ?? string.Empty);

        protected string RealName(BlockReference br) => br.RealName();

        protected void WriteSourceReference(BlockReference target, BlockDetailingContext context, string sourceHandle, double station)
        {
            // dynamic psmSourceReference.WritePropertyString(target, key, value)
            // dynamic psmSourceReference.WritePropertyObject(target, key, value)
            context.SourceReference.WritePropertyString(target, context.SourceReferenceKeys.SourceEntityHandle, sourceHandle);
            context.SourceReference.WritePropertyObject(target, context.SourceReferenceKeys.AlignmentStation, station);
        }
    }
}


