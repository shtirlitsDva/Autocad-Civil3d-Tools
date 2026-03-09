using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using Dreambuild.AutoCAD;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Viewport;

public class SetLayerColorInViewportOp : OperationBase
{
    public override string TypeId => "Viewport.SetLayerColor";
    public override string DisplayName => "Set Layer Color in Viewport";
    public override string Description =>
        "Sets a viewport color override on layers matching the specified patterns.";
    public override string Category => "Viewport";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "xrefName",
            "Xref Name",
            ParameterType.String,
            "Name of the xref whose layers to color"),
        new ParameterDescriptor(
            "layerPatterns",
            "Layer Patterns",
            ParameterType.String,
            "Semicolon-delimited layer name patterns, e.g. Bygning;Vejkant"),
        new ParameterDescriptor(
            "colorName",
            "Color Name",
            ParameterType.String,
            "Color name (e.g. grey, red, blue)",
            defaultValue: "grey"),
        new ParameterDescriptor(
            "viewportCenterX",
            "Viewport Center X",
            ParameterType.Int,
            "Viewport center X coordinate (integer)"),
        new ParameterDescriptor(
            "viewportCenterY",
            "Viewport Center Y",
            ParameterType.Int,
            "Viewport center Y coordinate (integer)")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string xrefName = GetStringParam(parameterValues, "xrefName");
        string layerPatterns = GetStringParam(parameterValues, "layerPatterns");
        string colorName = GetParamOrDefault(parameterValues, "colorName", "grey");
        int X = GetIntParam(parameterValues, "viewportCenterX");
        int Y = GetIntParam(parameterValues, "viewportCenterY");
        var xTx = context.Database.TransactionManager.TopTransaction;

        string[] patterns = layerPatterns.Split(';', StringSplitOptions.RemoveEmptyEntries);

        ObjectIdCollection idsToColor = new ObjectIdCollection();
        LayerTable lt = context.Database.LayerTableId.Go<LayerTable>(xTx);
        foreach (Oid id in lt)
        {
            LayerTableRecord ltr = id.Go<LayerTableRecord>(xTx);
            if (!ltr.Name.StartsWith(xrefName)) continue;
            if (patterns.Any(p => ltr.Name.Contains(p)))
                idsToColor.Add(id);
        }

        if (idsToColor.Count == 0) return new Result();

        DBDictionary layoutDict = context.Database.LayoutDictionaryId.Go<DBDictionary>(xTx);
        foreach (DBDictionaryEntry item in layoutDict)
        {
            if (item.Key == "Model") continue;
            Layout layout = item.Value.Go<Layout>(xTx);
            BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(xTx);

            foreach (Oid vpid in layBlock)
            {
                if (vpid.IsDerivedFrom<Autodesk.AutoCAD.DatabaseServices.Viewport>())
                {
                    var vp = vpid.Go<Autodesk.AutoCAD.DatabaseServices.Viewport>(xTx, OpenMode.ForWrite);
                    int centerX = (int)vp.CenterPoint.X;
                    int centerY = (int)vp.CenterPoint.Y;
                    if (centerX == X && centerY == Y)
                    {
                        foreach (Oid ltrid in idsToColor)
                        {
                            LayerTableRecord ltr = ltrid.Go<LayerTableRecord>(xTx);
                            ltr.UpgradeOpen();
                            LayerViewportProperties lvp = ltr.GetViewportOverrides(vpid);
                            lvp.Color = ColorByName(colorName);
                        }
                    }
                    vp.UpdateDisplay();
                }
            }
        }

        return new Result();
    }
}
