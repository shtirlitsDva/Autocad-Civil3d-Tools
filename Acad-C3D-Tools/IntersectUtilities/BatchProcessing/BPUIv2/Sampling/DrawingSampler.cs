using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using CivilDocument = Autodesk.Civil.ApplicationServices.CivilDocument;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Sampling;

public class DrawingSampler
{
    public static SampleResult SampleFromDrawing(string dwgPath)
    {
        var result = new SampleResult();

        using var db = new Database(false, true);
        db.ReadDwgFile(dwgPath, System.IO.FileShare.Read, true, null);

        using var tr = db.TransactionManager.StartTransaction();

        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        result.LayerNames = lt.Cast<ObjectId>()
            .Select(id => ((LayerTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name)
            .OrderBy(n => n)
            .ToArray();

        var st = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
        result.TextStyleNames = st.Cast<ObjectId>()
            .Select(id => ((TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name)
            .OrderBy(n => n)
            .ToArray();

        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        result.BlockNames = bt.Cast<ObjectId>()
            .Select(id => ((BlockTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name)
            .Where(n => !n.StartsWith("*"))
            .OrderBy(n => n)
            .ToArray();

        var ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        result.LinetypeNames = ltt.Cast<ObjectId>()
            .Select(id => ((LinetypeTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name)
            .OrderBy(n => n)
            .ToArray();

        try
        {
            var cdoc = CivilDocument.GetCivilDocument(db);

            result.AlignmentStyleNames = cdoc.Styles.AlignmentStyles
                .Cast<ObjectId>()
                .Select(id => ((AlignmentStyle)tr.GetObject(id, OpenMode.ForRead)).Name)
                .OrderBy(n => n)
                .ToArray();

            result.ProfileStyleNames = cdoc.Styles.ProfileStyles
                .Cast<ObjectId>()
                .Select(id => ((ProfileStyle)tr.GetObject(id, OpenMode.ForRead)).Name)
                .OrderBy(n => n)
                .ToArray();

            result.ProfileViewStyleNames = cdoc.Styles.ProfileViewStyles
                .Cast<ObjectId>()
                .Select(id => ((ProfileViewStyle)tr.GetObject(id, OpenMode.ForRead)).Name)
                .OrderBy(n => n)
                .ToArray();

            result.ProfileViewBandSetStyleNames = cdoc.Styles.ProfileViewBandSetStyles
                .Cast<ObjectId>()
                .Select(id => ((ProfileViewBandSetStyle)tr.GetObject(id, OpenMode.ForRead)).Name)
                .OrderBy(n => n)
                .ToArray();
        }
        catch { }

        tr.Commit();
        return result;
    }
}

public class SampleResult
{
    public string[] LayerNames { get; set; } = Array.Empty<string>();
    public string[] TextStyleNames { get; set; } = Array.Empty<string>();
    public string[] BlockNames { get; set; } = Array.Empty<string>();
    public string[] LinetypeNames { get; set; } = Array.Empty<string>();
    public string[] AlignmentStyleNames { get; set; } = Array.Empty<string>();
    public string[] ProfileStyleNames { get; set; } = Array.Empty<string>();
    public string[] ProfileViewStyleNames { get; set; } = Array.Empty<string>();
    public string[] ProfileViewBandSetStyleNames { get; set; } = Array.Empty<string>();
}
