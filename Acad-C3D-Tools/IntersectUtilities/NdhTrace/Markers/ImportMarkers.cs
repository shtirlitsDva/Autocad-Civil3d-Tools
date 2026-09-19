using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;

using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

internal enum ImportMarkerKind
{
    /// <summary>Not connected: a yellow circle around the place, with the note beside it.</summary>
    NotConnected,
    /// <summary>Connected, but corrected by more than the silent limit: an MLeader pointing at the port.</summary>
    Corrected,
}

/// <summary>One marker for the drafter; <paramref name="Note"/> is English.</summary>
internal readonly record struct ImportMarker(ImportMarkerKind Kind, Point2d At, string Note);

/// <summary>Places the import's markers in the working drawing.</summary>
internal interface IImportMarkers
{
    void Place(IReadOnlyList<ImportMarker> markers);
}

/// <summary>
/// The markers as plain AutoCAD objects (Circle, MText, MLeader) on the layer
/// NDH-IMPORT-NOTE, yellow, created if it is missing - one layer the drafter
/// can isolate, freeze or erase in one go. The objects take the layer's colour.
/// Written in one transaction inside the NDHFROMFJV command, so the command's
/// UNDO takes them back with everything else.
/// </summary>
internal sealed class AcadImportMarkers(Database db) : IImportMarkers
{
    public const string Layer = "NDH-IMPORT-NOTE";
    private const short Yellow = 2;
    //Drawing units are metres.
    private const double CircleRadius = 1.5;
    private const double TextHeight = 0.4;
    private const double NoteWidth = 12.0;
    private static readonly Vector3d LeaderOffset = new Vector3d(4.0, 4.0, 0.0);

    public void Place(IReadOnlyList<ImportMarker> markers)
    {
        if (markers.Count == 0) return;

        using Transaction tx = db.TransactionManager.StartTransaction();
        db.CheckOrCreateLayer(Layer, Yellow);
        BlockTableRecord space = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

        foreach (ImportMarker m in markers)
        {
            Point3d at = new Point3d(m.At.X, m.At.Y, 0.0);
            if (m.Kind == ImportMarkerKind.NotConnected)
            {
                Circle circle = new Circle(at, Vector3d.ZAxis, CircleRadius);
                circle.SetDatabaseDefaults(db);
                Append(space, tx, circle);
                Append(space, tx, Note(m.Note, at + new Vector3d(CircleRadius * 1.2, CircleRadius, 0.0)));
            }
            else Append(space, tx, Leader(m.Note, at));
        }
        tx.Commit();
    }

    private MText Note(string text, Point3d location)
    {
        MText note = new MText();
        note.SetDatabaseDefaults(db);
        note.Contents = text;
        note.TextHeight = TextHeight;
        note.Width = NoteWidth;
        note.Location = location;
        note.Attachment = AttachmentPoint.TopLeft;
        return note;
    }

    private MLeader Leader(string text, Point3d arrow)
    {
        Point3d textAt = arrow + LeaderOffset;
        MLeader leader = new MLeader();
        leader.SetDatabaseDefaults(db);
        leader.ContentType = ContentType.MTextContent;
        leader.MText = Note(text, textAt);
        int cluster = leader.AddLeader();
        int line = leader.AddLeaderLine(cluster);
        leader.AddFirstVertex(line, arrow);
        leader.AddLastVertex(line, textAt);
        return leader;
    }

    /// <summary>Appends an entity already set to the database's defaults, on the marker layer.</summary>
    private static void Append(BlockTableRecord space, Transaction tx, Entity e)
    {
        e.Layer = Layer;
        e.ColorIndex = 256;
        space.AppendEntity(e);
        tx.AddNewlyCreatedDBObject(e, true);
    }
}
