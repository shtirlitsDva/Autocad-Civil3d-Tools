using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;

using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Where a marker draws itself. Each marker picks its own shape (a ring for
/// what is not connected, a leader for a connection to check), so the drawing
/// side never asks a marker what kind it is.
/// </summary>
internal interface IMarkerCanvas
{
    /// <summary>A circle around the place with the note beside it.</summary>
    void Ring(Point2d at, string note);
    /// <summary>A leader pointing at the place with the note at its end.</summary>
    void Leader(Point2d at, string note);
}

/// <summary>One marker for the drafter; <see cref="Note"/> is English.</summary>
internal abstract record ImportMarker(Point2d At, string Note)
{
    public abstract void DrawOn(IMarkerCanvas canvas);
}

/// <summary>Not connected: a yellow circle around the place, with the note beside it.</summary>
internal sealed record NotConnectedMarker(Point2d At, string Note) : ImportMarker(At, Note)
{
    public override void DrawOn(IMarkerCanvas canvas) => canvas.Ring(At, Note);
}

/// <summary>
/// Connected, with something the drafter must check - corrected by more than
/// the silent limit, or not reading back as it was asked for: an MLeader
/// pointing at the port.
/// </summary>
internal sealed record ConnectionMarker(Point2d At, string Note) : ImportMarker(At, Note)
{
    public override void DrawOn(IMarkerCanvas canvas) => canvas.Leader(At, Note);
}

/// <summary>Places the import's markers in the working drawing.</summary>
internal interface IImportMarkers
{
    /// <summary>Places every marker; returns how many were placed.</summary>
    int Place(IReadOnlyList<ImportMarker> markers);
}

/// <summary>
/// The markers as plain AutoCAD objects (Circle, MText, MLeader) on the layer
/// NDH-IMPORT-NOTE - one layer the drafter can isolate, freeze or erase in one
/// go. The objects take the layer's colour. Every run makes the layer yellow,
/// on and thawed, so a marker the drafter hid last time is seen this time.
/// Every size is set here, never taken from the drawing's current styles (an
/// MLeader style made for 1:1000 sheets would draw a 4 m arrow). Written in
/// one transaction inside the NDHFROMFJV command, so the command's UNDO takes
/// them back with everything else.
/// </summary>
internal sealed class AcadImportMarkers(Database db) : IImportMarkers
{
    public const string Layer = "NDH-IMPORT-NOTE";
    private const short Yellow = 2;
    //Drawing units are metres. The leader's arrow, landing gap and dogleg are
    //sized to the note's text, as a drafted note of that height would be.
    private const double CircleRadius = 1.5;
    private const double TextHeight = 0.4;
    private const double NoteWidth = 12.0;
    private const double ArrowSize = TextHeight;
    private const double LandingGap = TextHeight / 4.0;
    private const double DoglegLength = TextHeight * 2.0;
    private static readonly Vector3d LeaderOffset = new Vector3d(4.0, 4.0, 0.0);

    public int Place(IReadOnlyList<ImportMarker> markers)
    {
        if (markers.Count == 0) return 0;

        using Transaction tx = db.TransactionManager.StartTransaction();
        EnsureLayer(tx);
        BlockTableRecord space = (BlockTableRecord)tx.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

        Canvas canvas = new Canvas(db, tx, space);
        foreach (ImportMarker m in markers) m.DrawOn(canvas);
        tx.Commit();
        return markers.Count;
    }

    /// <summary>The marker layer, created if missing, and yellow, on and thawed whatever the drafter did to it.</summary>
    private void EnsureLayer(Transaction tx)
    {
        db.CheckOrCreateLayer(Layer, Yellow);
        LayerTable lt = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        LayerTableRecord ltr = (LayerTableRecord)tx.GetObject(lt[Layer], OpenMode.ForWrite);
        ltr.IsOff = false;
        ltr.IsFrozen = false;
    }

    private sealed class Canvas(Database db, Transaction tx, BlockTableRecord space) : IMarkerCanvas
    {
        public void Ring(Point2d at, string note)
        {
            Point3d centre = new Point3d(at.X, at.Y, 0.0);
            Circle circle = new Circle(centre, Vector3d.ZAxis, CircleRadius);
            circle.SetDatabaseDefaults(db);
            Append(circle);
            Append(Note(note, centre + new Vector3d(CircleRadius * 1.2, CircleRadius, 0.0)));
        }

        public void Leader(Point2d at, string note)
        {
            Point3d arrow = new Point3d(at.X, at.Y, 0.0);
            Point3d textAt = arrow + LeaderOffset;
            MLeader leader = new MLeader();
            leader.SetDatabaseDefaults(db);
            leader.ContentType = ContentType.MTextContent;
            leader.MText = Note(note, textAt);
            //Scale first: every size below is multiplied by it.
            leader.Scale = 1.0;
            leader.TextHeight = TextHeight;
            leader.ArrowSize = ArrowSize;
            leader.EnableLanding = true;
            leader.LandingGap = LandingGap;
            leader.EnableDogleg = true;
            leader.DoglegLength = DoglegLength;
            int cluster = leader.AddLeader();
            int line = leader.AddLeaderLine(cluster);
            leader.AddFirstVertex(line, arrow);
            leader.AddLastVertex(line, textAt);
            Append(leader);
        }

        private MText Note(string text, Point3d location)
        {
            MText mtext = new MText();
            mtext.SetDatabaseDefaults(db);
            mtext.Contents = text;
            mtext.TextHeight = TextHeight;
            mtext.Width = NoteWidth;
            mtext.Location = location;
            mtext.Attachment = AttachmentPoint.TopLeft;
            return mtext;
        }

        /// <summary>Appends an entity already set to the database's defaults, on the marker layer.</summary>
        private void Append(Entity e)
        {
            e.Layer = Layer;
            e.ColorIndex = 256;
            space.AppendEntity(e);
            tx.AddNewlyCreatedDBObject(e, true);
        }
    }
}
