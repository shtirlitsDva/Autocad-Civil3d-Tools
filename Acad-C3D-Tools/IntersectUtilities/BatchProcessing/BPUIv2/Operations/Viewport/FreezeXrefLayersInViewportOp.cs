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

public class FreezeXrefLayersInViewportOp : OperationBase
{
    public override string TypeId => "Viewport.FreezeXrefLayers";
    public override string DisplayName => "Freeze Xref Layers in Viewport";
    public override string Description =>
        "Freezes all layers starting with the xref name in the viewport, except those containing 'Bygning' or 'Vejkant'.";
    public override string Category => "Viewport";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "xrefName",
            "Xref Name",
            ParameterType.String,
            "Name of the xref whose layers to freeze"),
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
        int X = GetIntParam(parameterValues, "viewportCenterX");
        int Y = GetIntParam(parameterValues, "viewportCenterY");
        var xTx = context.Database.TransactionManager.TopTransaction;

        ObjectIdCollection oids = new ObjectIdCollection();
        LayerTable lt = context.Database.LayerTableId.Go<LayerTable>(xTx);
        foreach (Oid id in lt)
        {
            LayerTableRecord ltr = id.Go<LayerTableRecord>(xTx);
            if (ltr.Name.StartsWith(xrefName)) oids.Add(id);
        }

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
                        ObjectIdCollection notFrozenIds = new ObjectIdCollection();
                        foreach (Oid oid in oids)
                        {
                            LayerTableRecord ltr = oid.Go<LayerTableRecord>(xTx);
                            if (ltr.Name.Contains("Bygning") || ltr.Name.Contains("Vejkant"))
                                continue;
                            if (vp.IsLayerFrozenInViewport(oid)) continue;
                            notFrozenIds.Add(oid);
                        }

                        if (notFrozenIds.Count != 0)
                            vp.FreezeLayersInViewport(notFrozenIds.GetEnumerator());
                    }
                    vp.UpdateDisplay();
                }
            }
        }

        return new Result();
    }
}
