using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Dreambuild.AutoCAD;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Xref;

public class AttachXrefOp : OperationBase
{
    public override string TypeId => "Xref.Attach";
    public override string DisplayName => "Attach Xref";
    public override string Description => "Attaches an external reference (xref) DWG file on the specified layer.";
    public override string Category => "Xref";

    public override IReadOnlyList<ParameterDescriptor> Parameters =>
    [
        new ParameterDescriptor(
            "filePath",
            "File Path",
            ParameterType.String,
            "Full path to .dwg file to attach as xref"),
        new ParameterDescriptor(
            "layerName",
            "Layer Name",
            ParameterType.String,
            "Layer to place the xref on")
    ];

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string path = GetStringParam(parameterValues, "filePath");
        string layer = GetStringParam(parameterValues, "layerName");

        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return new Result(ResultStatus.FatalError, $"Xref file not found: {path}");
        if (string.IsNullOrEmpty(layer))
            return new Result(ResultStatus.FatalError, "Layer name is empty!");

        using (Transaction nestedTx = context.Database.TransactionManager.StartTransaction())
        {
            try
            {
                Oid xrefId = context.Database.AttachXref(path, System.IO.Path.GetFileNameWithoutExtension(path));
                if (xrefId == Oid.Null)
                {
                    nestedTx.Abort();
                    return new Result(ResultStatus.FatalError, $"Could not attach xref: {path}");
                }
                Point3d insPt = new Point3d(0, 0, 0);
                using (BlockReference br = new BlockReference(insPt, xrefId))
                {
                    BlockTableRecord ms = context.Database.GetModelspaceForWrite();
                    ms.AppendEntity(br);
                    nestedTx.AddNewlyCreatedDBObject(br, true);
                    context.Database.CheckOrCreateLayer(layer);
                    br.Layer = layer;
                }
                nestedTx.Commit();
            }
            catch (System.Exception ex)
            {
                nestedTx.Abort();
                return new Result(ResultStatus.FatalError, ex.ToString());
            }
        }
        return new Result();
    }
}
