#if BRICSCAD
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;
#else
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
#endif

using System;

using AcadOverrules.VertexCircles;

namespace AcadOverrules
{
    /// <summary>
    /// Makes the tangent intersections that <see cref="PolylineVertexCircles"/> draws
    /// snappable. Object snap does not read an overrule's graphics - it asks the entity for
    /// its snap points - so drawing the corner is not enough; this adds every corner of the
    /// polyline to the Center and Node snaps. Center because the corner is drawn as a circle,
    /// Node because the corner is a point.
    ///
    /// Applies to the same polylines as the draw overrule and only while the active profile
    /// shows tangent intersections. TOGGLEPOLYVERTICES switches the two together.
    /// </summary>
    public class PolylineTangentIntersectionSnap : OsnapOverrule
    {
        private static VertexCirclesSettings Settings =>
            VertexCirclesSettingsService.Instance.Current;

        public PolylineTangentIntersectionSnap()
        {
            base.SetCustomFilter();
        }

        public override bool IsApplicable(RXObject overruledSubject)
        {
            if (overruledSubject is Polyline pline)
            {
                if (pline.ObjectId.IsNull) return false;
                if (pline.NumberOfVertices < 2) return false;

                VertexCirclesSettings settings = Settings;
                if (!settings.TangentIntersections.Show) return false;

                return LayerFilterMatcher.Matches(settings.LayerFilters, pline.Layer);
            }
            return false;
        }

        public override void GetObjectSnapPoints(
            Entity entity,
            ObjectSnapModes snapMode,
            IntPtr gsSelectionMark,
            Point3d pickPoint,
            Point3d lastPoint,
            Matrix3d viewTransform,
            Point3dCollection snapPoints,
            IntegerCollection geometryIds)
        {
            base.GetObjectSnapPoints(
                entity, snapMode, gsSelectionMark, pickPoint, lastPoint,
                viewTransform, snapPoints, geometryIds);

            if (snapMode != ObjectSnapModes.ModeCenter && snapMode != ObjectSnapModes.ModeNode)
                return;

            foreach (ArcCorner corner in TangentIntersections.Find((Polyline)entity))
                snapPoints.Add(corner.Corner);
        }
    }
}
