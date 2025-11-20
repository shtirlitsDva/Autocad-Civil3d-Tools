using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using NTRExport.Enums;

namespace NTRExport.SoilModel
{
    internal enum SoilHintKind
    {
        Cushion,
    }

    internal sealed class SoilHint
    {
        public Point3d AnchorPoint { get; }
        public FlowRole FlowRole { get; }
        public double ReachMeters { get; }
        public SoilHintKind Kind { get; }
        public Handle SourceHandle { get; }
        public bool IncludeAnchorMember { get; }
        public string? Description { get; }

        public SoilHint(
            Point3d anchorPoint,
            FlowRole flowRole,
            double reachMeters,
            SoilHintKind kind,
            Handle sourceHandle,
            bool includeAnchorMember = false,
            string? description = null)
        {
            AnchorPoint = anchorPoint;
            FlowRole = flowRole;
            ReachMeters = reachMeters;
            Kind = kind;
            SourceHandle = sourceHandle;
            IncludeAnchorMember = includeAnchorMember;
            Description = description;
        }
    }
}

