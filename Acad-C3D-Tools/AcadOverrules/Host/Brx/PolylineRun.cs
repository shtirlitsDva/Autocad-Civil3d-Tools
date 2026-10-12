using Teigha.Colors;
using Teigha.GraphicsInterface;

using Polyline = Teigha.DatabaseServices.Polyline;

namespace AcadOverrules
{
    /// <summary>
    /// Draws a contiguous run of a polyline's segments with the traits the
    /// <see cref="WorldDraw"/> holds now, as AutoCAD's <c>Geometry.Polyline(pline, from, count)</c>
    /// does (Host\Acad).
    ///
    /// BricsCAD's managed <c>WorldGeometry.Polyline(Polyline, int, int)</c> is a stub that
    /// returns false and draws nothing (TD_Mgd V26.2), so the run is copied into a temporary
    /// polyline and drawn as a nested drawable. The copy is not in a database, so no overrule
    /// applies to it (every overrule here requires <c>pline.Database</c>). A nested drawable
    /// sets its own traits when it is drawn, so the current traits are put on the copy.
    /// </summary>
    internal static class PolylineRun
    {
        public static void Draw(WorldDraw wd, Polyline pline, int fromIndex, int count)
        {
            int vertexCount = pline.NumberOfVertices;
            if (count <= 0 || vertexCount < 2) return;

            using var run = new Polyline(count + 1);
            run.Normal = pline.Normal;
            run.Elevation = pline.Elevation;

            //A run never wraps, but the closing segment of a closed polyline ends at vertex 0.
            for (int k = 0; k <= count; k++)
            {
                int vertex = (fromIndex + k) % vertexCount;
                bool endVertex = k == count;
                run.AddVertexAt(k, pline.GetPoint2dAt(vertex),
                    endVertex ? 0.0 : pline.GetBulgeAt(vertex),
                    endVertex ? 0.0 : pline.GetStartWidthAt(vertex),
                    endVertex ? 0.0 : pline.GetEndWidthAt(vertex));
            }

            run.Plinegen = pline.Plinegen;
            run.LinetypeScale = pline.LinetypeScale;

            SubEntityTraits traits = wd.SubEntityTraits;
            run.LayerId = pline.LayerId;
            run.Color = Color.FromEntityColor(traits.TrueColor);
            run.LineWeight = traits.LineWeight;
            if (!traits.LineType.IsNull) run.LinetypeId = traits.LineType;
            run.Transparency = traits.Transparency;

            wd.Geometry.Draw(run);
        }
    }
}
