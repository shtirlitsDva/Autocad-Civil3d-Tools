using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using Dreambuild.AutoCAD;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Alignment;

public class CenterViewFrameLabelsOp : OperationBase
{
    public override string TypeId => "Alignment.CenterViewFrameLabels";
    public override string DisplayName => "Center View Frame Labels";
    public override string Description => "Centers all text components in the Basic view frame label style.";
    public override string Category => "Alignment";

    public override IReadOnlyList<ParameterDescriptor> Parameters => Array.Empty<ParameterDescriptor>();

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        var xTx = context.Database.TransactionManager.TopTransaction;
        var cDoc = CivilDocument.GetCivilDocument(context.Database);

        var root = cDoc.Styles.LabelStyles.ViewFrameLabelStyles;

        if (!root.LabelStyles.Contains("Basic"))
            return new Result(ResultStatus.FatalError, "No Basic viewframe label style found!");

        var styleId = root.LabelStyles["Basic"];
        var style = (LabelStyle)xTx.GetObject(styleId, OpenMode.ForWrite);

        foreach (Oid compId in style.GetComponents(LabelStyleComponentType.Text))
        {
            var txt = (LabelStyleTextComponent)xTx.GetObject(compId, OpenMode.ForWrite);
            txt.Text.Attachment.Value = LabelTextAttachmentType.MiddleCenter;
            txt.Text.XOffset.Value = 0.0;
            txt.Text.YOffset.Value = 0.0;
        }

        return new Result();
    }
}
