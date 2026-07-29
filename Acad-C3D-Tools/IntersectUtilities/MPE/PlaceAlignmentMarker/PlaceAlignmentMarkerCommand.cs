using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.MPE.PlaceAlignmentMarker;
using IntersectUtilities.UtilsCommon;

using static IntersectUtilities.UtilsCommon.Utils;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <summary>
    /// Look-ahead/behind used to read the alignment's tangent direction at the marker station. Kept
    /// short so tight curves still give a direction close to the true tangent.
    /// </summary>
    private const double AlignmentMarkerTangentDelta = 0.5;

    /// <command>PLACEALIGNMENTMARKER</command>
    /// <summary>
    /// Places an X marker at a given station on a Civil 3D alignment. The alignment is chosen by name
    /// from a list covering the drawing and all its xrefs (like FINDALIGNMENT), so xreffed alignments
    /// work too. Type the station and a block reference (two crossing polylines) is inserted in the
    /// host drawing at that station on layer 0-ALIGNMENT-MARKER, rotated to the alignment's tangent so
    /// its legs straddle it at ±45°. The block definition is created in the drawing on first use.
    /// Stations are raw alignment stations — station equations are not applied.
    /// </summary>
    /// <category>MPE</category>
    [CommandMethod("PLACEALIGNMENTMARKER", CommandFlags.Modal)]
    public void PlaceAlignmentMarker()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecutePlaceAlignmentMarker(document);
        }
        catch (System.Exception exception)
        {
            prdDbg(exception);
            document.Editor.WriteMessage($"\nPLACEALIGNMENTMARKER failed: {exception.Message}");
        }
    }

    private static void ExecutePlaceAlignmentMarker(Document document)
    {
        Editor editor = document.Editor;
        Database db = document.Database;

        AlignmentSource? source = AlignmentPicker.Pick(db, out string pickMessage);
        if (source is null)
        {
            editor.WriteMessage($"\n{pickMessage}");
            return;
        }

        (double startStation, double endStation) = ReadAlignmentStationRange(source);

        if (!TryPromptMarkerStation(editor, source.DisplayName, startStation, endStation, out double station))
        {
            return;
        }

        (Point3d position, double rotation) = ResolveMarkerPlacement(source, station, startStation, endStation);

        InsertAlignmentMarker(db, position, rotation);

        editor.WriteMessage(
            $"\nMarkør placeret på {source.DisplayName} station {station:0.###} " +
            $"({position.X:0.###}, {position.Y:0.###}) på lag {AlignmentMarkerBlock.LayerName}.");
    }

    private static (double Start, double End) ReadAlignmentStationRange(AlignmentSource source)
    {
        using Transaction tx = source.Database.TransactionManager.StartTransaction();
        Alignment alignment = (Alignment)tx.GetObject(source.AlignmentId, OpenMode.ForRead);
        double start = alignment.StartingStation;
        double end = alignment.EndingStation;
        tx.Commit();
        return (start, end);
    }

    /// <summary>
    /// Resolves the marker's host-WCS position and rotation. The alignment is queried in its own
    /// database — which for an xref means xref-local coordinates — so both the station point and the
    /// tangent chord are mapped through <see cref="AlignmentSource.TransformToHost"/> before use.
    /// </summary>
    private static (Point3d Position, double Rotation) ResolveMarkerPlacement(
        AlignmentSource source,
        double station,
        double startStation,
        double endStation)
    {
        using Transaction tx = source.Database.TransactionManager.StartTransaction();
        Alignment alignment = (Alignment)tx.GetObject(source.AlignmentId, OpenMode.ForRead);

        Point3d position = AlignmentPointAtStation(alignment, station)
            .TransformBy(source.TransformToHost);

        double behind = Math.Max(startStation, station - AlignmentMarkerTangentDelta);
        double ahead = Math.Min(endStation, station + AlignmentMarkerTangentDelta);

        double rotation = 0.0;
        if (ahead - behind > 1e-9)
        {
            Point3d from = AlignmentPointAtStation(alignment, behind).TransformBy(source.TransformToHost);
            Point3d to = AlignmentPointAtStation(alignment, ahead).TransformBy(source.TransformToHost);
            Vector3d direction = to - from;
            if (direction.Length > 1e-9)
            {
                rotation = Math.Atan2(direction.Y, direction.X);
            }
        }

        tx.Commit();
        return (position, rotation);
    }

    /// <summary>
    /// Inserts the marker block into the host drawing's model space. The block's legs sit at ±45°, so
    /// rotating to the tangent makes the marker read as an X straddling the alignment.
    /// </summary>
    private static void InsertAlignmentMarker(Database db, Point3d position, double rotation)
    {
        using Transaction tx = db.TransactionManager.StartTransaction();
        try
        {
            db.CheckOrCreateLayer(AlignmentMarkerBlock.LayerName, AlignmentMarkerBlock.LayerColorIndex);
            Oid btrId = AlignmentMarkerBlock.EnsureDefinition(db);

            BlockReference marker = new(position, btrId)
            {
                Rotation = rotation,
                Layer = AlignmentMarkerBlock.LayerName,
            };

            BlockTableRecord modelSpace = db.GetModelspaceForWrite();
            modelSpace.AppendEntity(marker);
            tx.AddNewlyCreatedDBObject(marker, true);

            tx.Commit();
        }
        catch
        {
            tx.Abort();
            throw;
        }
    }

    /// <summary>
    /// Prompts for the marker station, re-asking until the value lies within the alignment's station
    /// range. Returns false when the user cancels.
    /// </summary>
    private static bool TryPromptMarkerStation(
        Editor editor,
        string alignmentName,
        double startStation,
        double endStation,
        out double station)
    {
        station = 0.0;
        editor.WriteMessage(
            $"\n{alignmentName}: station {startStation:0.###} - {endStation:0.###}.");

        while (true)
        {
            PromptDoubleOptions options = new("\nStation for markør: ")
            {
                AllowNegative = true,
                AllowZero = true,
                AllowNone = false,
            };

            PromptDoubleResult result = editor.GetDouble(options);
            if (result.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nPLACEALIGNMENTMARKER annulleret.");
                return false;
            }

            if (result.Value < startStation || result.Value > endStation)
            {
                editor.WriteMessage(
                    $"\nStation {result.Value:0.###} er udenfor alignmentens område " +
                    $"({startStation:0.###} - {endStation:0.###}). Prøv igen.");
                continue;
            }

            station = result.Value;
            return true;
        }
    }

    private static Point3d AlignmentPointAtStation(Alignment alignment, double station)
    {
        double x = 0.0;
        double y = 0.0;
        alignment.PointLocation(station, 0.0, ref x, ref y);
        return new Point3d(x, y, 0.0);
    }
}
