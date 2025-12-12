using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace AcadOverrules
{
    /// <summary>
    /// Overrule that highlights arc segments of polylines by drawing them
    /// with a cyan color overlay. Straight segments are not affected.
    /// </summary>
    public class PolylineArcHighlight : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        // Cyan color (ACI index 4)
        private const short CyanColor = 4;

        public PolylineArcHighlight()
        {
            base.SetCustomFilter();
        }

        public override bool IsApplicable(RXObject overruledSubject)
        {
            if (overruledSubject == null) return false;
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;
                if (pline.NumberOfVertices < 2) return false;
                if (pline.Length < 0.1) return false;
                // Only apply to polylines that have arc segments
                if (!pline.HasBulges) return false;
                return true;
            }
            return false;
        }

        public override bool WorldDraw(
            Autodesk.AutoCAD.GraphicsInterface.Drawable drawable,
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw wd)
        {
            // First, draw the original polyline
            base.WorldDraw(drawable, wd);

            Polyline pline = (Polyline)drawable;

            // Set color to cyan for arc segments
            wd.SubEntityTraits.Color = CyanColor;

            // Iterate through all segments
            int segmentCount = pline.NumberOfVertices - 1;
            if (pline.Closed) segmentCount = pline.NumberOfVertices;

            for (int i = 0; i < segmentCount; i++)
            {
                // Check if this segment is an arc (bulge != 0)
                double bulge = pline.GetBulgeAt(i);
                if (bulge == 0) continue;

                // Draw the arc segment on top with cyan color
                // Using Polyline method which respects the polyline's width
                wd.Geometry.Polyline(pline, i, 1);
            }

            return true;
        }
    }
}

