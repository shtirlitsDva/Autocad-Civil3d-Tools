using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using static IntersectUtilities.UtilsCommon.Utils;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.GraphWriteV2
{
    /// <summary>
    /// One inspectable error object: the handle that identifies it, its ObjectId (for select/highlight),
    /// the geometric extents to zoom to, and a short description of what is wrong with it. Extents and
    /// ObjectId are value types, so the containing list can be captured inside a transaction and then
    /// consumed by the interactive loop after that transaction has been committed/aborted.
    /// </summary>
    internal readonly record struct GraphErrorItem(
        string Handle, ObjectId Id, Extents3d Extents, string Info);

    /// <summary>
    /// A command-line, keyword-driven "zoom to problematic object" loop, modelled on the interactive
    /// inspection section of CHECKALLPOLYLINERADII: the user picks a handle from a keyword list, the
    /// object is selected + highlighted, and the view zooms to it with padding. Runs entirely outside
    /// any transaction — <see cref="Interaction.HighlightObjects"/> and <see cref="Interaction.ZoomView"/>
    /// each open their own transaction, so it is fed pre-captured <see cref="GraphErrorItem"/>s.
    /// </summary>
    internal static class GraphErrorInspector
    {
        public static void Run(Document doc, string title, IReadOnlyList<GraphErrorItem> items)
        {
            if (doc == null || items == null || items.Count == 0) return;

            var ed = doc.Editor;

            // Legend so the per-object detail (which doesn't fit in the keyword labels) is visible.
            prdDbg(title);
            string[] hdrs = ["Handle", "Info"];
            var rows = items.Select(x => new object[] { x.Handle, x.Info });
            PrintTable(hdrs, rows);

            // Map handle -> item so the chosen keyword resolves back to its object.
            var byHandle = items
                .GroupBy(x => x.Handle, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var highlighted = new List<ObjectId>();
            try
            {
                while (true)
                {
                    var opts = new PromptKeywordOptions(
                        $"\n{title} — vælg objekt at zoome til (Exit = afslut):")
                    {
                        AllowNone = true
                    };
                    // Use the handle itself as the keyword so the user can type or click it.
                    foreach (var it in items) opts.Keywords.Add(it.Handle);
                    opts.Keywords.Add("Exit");
                    opts.Keywords.Default = "Exit";

                    var res = ed.GetKeywords(opts);
                    if (res.Status != PromptStatus.OK || res.StringResult == "Exit")
                        break;

                    if (!byHandle.TryGetValue(res.StringResult, out var sel)) continue;

                    // Select + highlight the chosen object (mirrors the SBH workflow).
                    // Guard: the shared helper throws on an empty collection.
                    if (highlighted.Count > 0) Interaction.UnhighlightObjects(highlighted);
                    highlighted = sel.Id.IsValid
                        ? new List<ObjectId> { sel.Id }
                        : new List<ObjectId>();
                    if (highlighted.Count > 0)
                    {
                        ed.SetImpliedSelection(highlighted.ToArray());
                        Interaction.HighlightObjects(highlighted);
                    }

                    prdDbg($"Handle {sel.Handle}: {sel.Info}");
                    ZoomPadded(sel.Extents, 0.3);
                }
            }
            finally
            {
                if (highlighted.Count > 0) Interaction.UnhighlightObjects(highlighted);
            }
        }

        // Zoom to an extent with padding (factor of the larger side) so the object doesn't fill the
        // viewport edge-to-edge. Same approach as CHECKALLPOLYLINERADII's ZoomPadded.
        private static void ZoomPadded(Extents3d e, double factor)
        {
            double dx = e.MaxPoint.X - e.MinPoint.X;
            double dy = e.MaxPoint.Y - e.MinPoint.Y;
            double pad = Math.Max(Math.Max(dx, dy), 1.0) * factor;
            var view = new Extents3d(
                new Point3d(e.MinPoint.X - pad, e.MinPoint.Y - pad, 0),
                new Point3d(e.MaxPoint.X + pad, e.MaxPoint.Y + pad, 0));
            Interaction.ZoomView(view);
        }
    }
}
