using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.MPE.Ler3DNetwork;

using static IntersectUtilities.UtilsCommon.Utils;

using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities;

public partial class Intersect
{
    // Persistent (toggle) overlay of every alignment's travel direction. Held statically so a second
    // NSALIGNMENTDIRECTIONS run can switch it off. Transients are DirectShortTerm and the manager's
    // Application.Idle hook keeps them alive through pan/zoom AND regen (rescaling to the view). The
    // static is disposed in Intersect.Terminate() so an unload→reload never leaves orphaned arrows.
    private static LerSlopeArrowManager? _alignmentDirectionOverlay;

    // Arrowheads spaced along each alignment (model units); min two per alignment so even short ones
    // read as directed.
    private const double AlignmentArrowSpacing = 20.0;

    /// <command>NSALIGNMENTDIRECTIONS</command>
    /// <summary>
    /// Toggles a direction overlay on ALL Civil 3D alignments in the drawing: green arrowheads spaced
    /// along each alignment pointing from station 0 toward the end, so you can read every alignment's
    /// direction at a glance (like the slope arrows in the LER analyse tools). Run once to show, again
    /// to hide. The arrows keep a constant screen size on zoom and persist through regen.
    /// </summary>
    /// <category>NSAlignmentCrawl</category>
    [CommandMethod("NSALIGNMENTDIRECTIONS")]
    public void NSAlignmentDirections()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            Editor editor = document.Editor;

            // Toggle OFF if already shown.
            if (_alignmentDirectionOverlay is not null)
            {
                _alignmentDirectionOverlay.Dispose();
                _alignmentDirectionOverlay = null;
                editor.WriteMessage("\nAlignment-retningspile slukket.");
                return;
            }

            List<LerSlopeAnchor> anchors = BuildAlignmentDirectionAnchors(document);
            if (anchors.Count == 0)
            {
                editor.WriteMessage("\nIngen alignments fundet i tegningen.");
                return;
            }

            LerSlopeArrowManager overlay = new(CadColor.FromRgb(0, 255, 0));
            overlay.Show(document, anchors);
            _alignmentDirectionOverlay = overlay;
            editor.WriteMessage($"\nAlignment-retningspile tændt ({anchors.Count} pile). Kør igen for at slukke.");
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "NSALIGNMENTDIRECTIONS", exception);
        }
    }

    // Disposed from Intersect.Terminate() so the overlay's transients + Application.Idle hook never
    // survive an unload→reload cycle as orphaned, uncollectable arrows.
    internal static void ResetAlignmentDirectionOverlay()
    {
        _alignmentDirectionOverlay?.Dispose();
        _alignmentDirectionOverlay = null;
    }

    private static List<LerSlopeAnchor> BuildAlignmentDirectionAnchors(Document document)
    {
        List<LerSlopeAnchor> anchors = [];
        CivilDocument civilDoc = CivilApplication.ActiveDocument;

        using Transaction tr = document.Database.TransactionManager.StartTransaction();
        foreach (ObjectId id in civilDoc.GetAlignmentIds())
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not Alignment alignment)
            {
                continue;
            }

            double length = alignment.Length;
            if (length <= 1e-6)
            {
                continue;
            }

            int count = Math.Max(2, (int)Math.Floor(length / AlignmentArrowSpacing));
            double delta = Math.Min(0.5, length * 0.25); // small look-ahead to read travel direction

            for (int i = 0; i < count; i++)
            {
                // Mid-spaced samples (stay off the exact endpoints). Distance increases station 0 → end,
                // so the look-ahead vector is the alignment's travel direction.
                double dist = (i + 0.5) / count * length;
                try
                {
                    Point3d p = alignment.GetPointAtDist(dist);
                    Point3d ahead = alignment.GetPointAtDist(Math.Min(length, dist + delta));
                    Vector3d dir = ahead - p;
                    double len = dir.Length;
                    if (len <= 1e-9)
                    {
                        continue;
                    }

                    anchors.Add(new LerSlopeAnchor(p.X, p.Y, 0.0, dir.X / len, dir.Y / len));
                }
                catch
                {
                    // Skip a degenerate sample; the remaining arrows still convey the direction.
                }
            }
        }

        tr.Commit();
        return anchors;
    }
}
