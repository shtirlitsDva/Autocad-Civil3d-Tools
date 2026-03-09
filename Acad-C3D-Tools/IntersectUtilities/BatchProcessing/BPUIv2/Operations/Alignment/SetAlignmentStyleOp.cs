using Autodesk.AutoCAD.DatabaseServices;
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

public class SetAlignmentStyleOp : OperationBase
{
    public override string TypeId => "Alignment.SetStyle";
    public override string DisplayName => "Set Alignment Style";
    public override string Description => "Sets the style for all alignments in the drawing.";
    public override string Category => "Alignment";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "styleName",
            "Style Name",
            ParameterType.String,
            "Name of the alignment style to apply",
            supportsSampling: true)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string styleName = GetStringParam(parameterValues, "styleName");

        var xTx = context.Database.TransactionManager.TopTransaction;
        var cDoc = CivilDocument.GetCivilDocument(context.Database);

        Oid alStyle = cDoc.Styles.AlignmentStyles[styleName];

        HashSet<Autodesk.Civil.DatabaseServices.Alignment> als =
            context.Database.HashSetOfType<Autodesk.Civil.DatabaseServices.Alignment>(xTx);

        foreach (Autodesk.Civil.DatabaseServices.Alignment al in als)
        {
            al.CheckOrOpenForWrite();
            al.StyleId = alStyle;
        }

        return new Result();
    }
}
