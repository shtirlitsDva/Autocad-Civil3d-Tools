using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace AcadOverrules
{
    /// <summary>
    /// Overrule that draws small circles at each vertex of polylines
    /// that are on layer '0-FJV-PROFILE-DRAFT' and have red color (ACI 1).
    /// </summary>
    public class DraftPolylineVerticeMark : Autodesk.AutoCAD.GraphicsInterface.DrawableOverrule
    {
        // White/Black color (ACI index 7) - displays as white on dark background, black on light
        private const short WhiteBlackColor = 7;

        // Red color (ACI index 1)
        private const short RedColor = 1;

        // Target layer name
        private const string TargetLayer = "0-FJV-PROFILE-DRAFT";

        // Circle diameter
        private const double CircleDiameter = 0.05;

        public DraftPolylineVerticeMark()
        {
            base.SetCustomFilter();
        }

        public override bool IsApplicable(RXObject overruledSubject)
        {
            if (overruledSubject == null) return false;
            if (overruledSubject is Polyline pline)
            {
                if (pline.Database == null) return false;
                if (pline.NumberOfVertices < 1) return false;

                // Check layer first
                if (pline.Layer != TargetLayer) return false;

                // Then check if color is red (ACI 1)
                if (pline.ColorIndex != RedColor) return false;

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

            // Set color to white/black for vertex circles
            wd.SubEntityTraits.Color = WhiteBlackColor;

            double radius = CircleDiameter / 2.0;

            // Draw a circle at each vertex
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                Point3d vertex = pline.GetPoint3dAt(i);

                // Draw circle at vertex position
                // Using CircularArc with full 360 degrees to create a circle
                wd.Geometry.Circle(vertex, radius, Vector3d.ZAxis);
            }

            return true;
        }
    }
}

