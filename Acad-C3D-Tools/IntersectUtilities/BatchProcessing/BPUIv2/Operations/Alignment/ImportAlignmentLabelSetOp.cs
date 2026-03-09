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

public class ImportAlignmentLabelSetOp : OperationBase
{
    public override string TypeId => "Alignment.ImportLabelSet";
    public override string DisplayName => "Import Alignment Label Set";
    public override string Description => "Imports a label set for all alignments in the drawing.";
    public override string Category => "Alignment";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "labelSetName",
            "Label Set Name",
            ParameterType.String,
            "Name of the alignment label set to import",
            supportsSampling: true)
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string labelSetName = GetStringParam(parameterValues, "labelSetName");

        var xTx = context.Database.TransactionManager.TopTransaction;
        var cDoc = CivilDocument.GetCivilDocument(context.Database);

        Oid labelSetStyle = cDoc.Styles.LabelSetStyles.AlignmentLabelSetStyles[labelSetName];

        HashSet<Autodesk.Civil.DatabaseServices.Alignment> als =
            context.Database.HashSetOfType<Autodesk.Civil.DatabaseServices.Alignment>(xTx);

        foreach (Autodesk.Civil.DatabaseServices.Alignment al in als)
        {
            al.CheckOrOpenForWrite();
            al.ImportLabelSet(labelSetStyle);
        }

        return new Result();
    }
}
