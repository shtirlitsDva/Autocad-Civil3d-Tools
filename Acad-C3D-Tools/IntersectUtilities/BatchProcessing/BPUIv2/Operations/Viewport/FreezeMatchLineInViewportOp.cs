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

public class FreezeMatchLineInViewportOp : OperationBase
{
    public override string TypeId => "Viewport.FreezeMatchLine";
    public override string DisplayName => "Freeze Match Line in Viewport";
    public override string Description =>
        "Finds layers ending with '_VF|C-ANNO-MTCH' and freezes them in the viewport matching the specified center coordinates.";
    public override string Category => "Viewport";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
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
        int X = GetIntParam(parameterValues, "viewportCenterX");
        int Y = GetIntParam(parameterValues, "viewportCenterY");
        var xTx = context.Database.TransactionManager.TopTransaction;

        ObjectIdCollection oids = new ObjectIdCollection();

        try
        {
            LayerTable lt = context.Database.LayerTableId.Go<LayerTable>(xTx);
            foreach (Oid loid in lt)
            {
                LayerTableRecord ltr = loid.Go<LayerTableRecord>(xTx);
                if (ltr.Name.EndsWith("_VF|C-ANNO-MTCH"))
                {
                    oids.Add(loid);
                }
            }

            DBDictionary layoutDict = context.Database.LayoutDictionaryId.Go<DBDictionary>(xTx);
            foreach (DBDictionaryEntry item in layoutDict)
            {
                if (item.Key == "Model") continue;
                Layout layout = item.Value.Go<Layout>(xTx);
                BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(xTx);

                foreach (Oid id in layBlock)
                {
                    if (id.IsDerivedFrom<Autodesk.AutoCAD.DatabaseServices.Viewport>())
                    {
                        var vp = id.Go<Autodesk.AutoCAD.DatabaseServices.Viewport>(xTx);
                        int centerX = (int)vp.CenterPoint.X;
                        int centerY = (int)vp.CenterPoint.Y;
                        if (centerX == X && centerY == Y)
                        {
                            ObjectIdCollection notFrozenIds = new ObjectIdCollection();
                            foreach (Oid oid in oids)
                            {
                                if (vp.IsLayerFrozenInViewport(oid)) continue;
                                notFrozenIds.Add(oid);
                            }

                            if (notFrozenIds.Count == 0) continue;

                            vp.CheckOrOpenForWrite();
                            vp.FreezeLayersInViewport(notFrozenIds.GetEnumerator());
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            return new Result(ResultStatus.FatalError, ex.ToString());
        }

        return new Result();
    }
}
