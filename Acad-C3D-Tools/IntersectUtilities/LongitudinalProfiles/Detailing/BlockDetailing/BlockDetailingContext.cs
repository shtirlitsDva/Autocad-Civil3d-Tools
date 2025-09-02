using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    /// <summary>
    /// Encapsulates all services and data required to place detailing blocks for components.
    /// </summary>
    public sealed class BlockDetailingContext
    {
        public Database Database { get; }
        public Transaction Transaction { get; }
        public Alignment Alignment { get; }
        public Polyline AlignmentPolyline { get; }
        public Profile SurfaceProfile { get; }
        public ProfileViewStyle ProfileViewStyle { get; }
        public double OriginX { get; }
        public double OriginY { get; }
        public double ProfileViewStartStation { get; }
        public double ProfileViewEndStation { get; }
        public double ProfileViewBottomElevation { get; }
        public string ComponentBlockName { get; }
        public string BueRorBlockName { get; }
        public System.Data.DataTable ComponentDataTable { get; }
        public PropertySetManager PipelineData { get; }
        public PSetDefs.DriPipelineData PipelineDataKeys { get; }
        public PropertySetManager SourceReference { get; }
        public PSetDefs.DriSourceReference SourceReferenceKeys { get; }

        /// <summary>
        /// Samples the mid alignment profile elevation at a given station.
        /// </summary>
        public Func<double, double> SampleElevationAtStation { get; }        

        public BlockDetailingContext(
            Database database,
            Transaction transaction,
            Alignment alignment,
            Polyline alignmentPolyline,
            Profile surfaceProfile,
            ProfileViewStyle profileViewStyle,
            double originX,
            double originY,
            double profileViewStartStation,
            double profileViewEndStation,
            double profileViewBottomElevation,
            string componentBlockName,
            string bueRorBlockName,
            System.Data.DataTable componentDataTable,
            PropertySetManager pipelineData,
            PSetDefs.DriPipelineData pipelineDataKeys,
            PropertySetManager sourceReference,
            PSetDefs.DriSourceReference sourceReferenceKeys,
            Func<double, double> sampleElevationAtStation,
            bool preliminary = false
            )
        {
            Database = database;
            Transaction = transaction;
            Alignment = alignment;
            AlignmentPolyline = alignmentPolyline;
            SurfaceProfile = surfaceProfile;
            ProfileViewStyle = profileViewStyle;
            OriginX = originX;
            OriginY = originY;
            ProfileViewStartStation = profileViewStartStation;
            ProfileViewEndStation = profileViewEndStation;
            ProfileViewBottomElevation = profileViewBottomElevation;
            ComponentBlockName = componentBlockName;
            BueRorBlockName = bueRorBlockName;
            ComponentDataTable = componentDataTable;
            PipelineData = pipelineData;
            PipelineDataKeys = pipelineDataKeys;
            SourceReference = sourceReference;
            SourceReferenceKeys = sourceReferenceKeys;
            SampleElevationAtStation = sampleElevationAtStation;
        }
    }
}