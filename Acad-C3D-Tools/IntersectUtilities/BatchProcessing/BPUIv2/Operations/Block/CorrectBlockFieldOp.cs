using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.Civil.DatabaseServices;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using Dreambuild.AutoCAD;
using static IntersectUtilities.UtilsCommon.Utils;
using static IntersectUtilities.Utils;
using static IntersectUtilities.HelperMethods;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Block;

public class CorrectBlockFieldOp : OperationBase
{
    public override string TypeId => "Block.CorrectField";
    public override string DisplayName => "Correct Block Field";
    public override string Description =>
        "Corrects a field in a block's attribute definition and all attribute references.";
    public override string Category => "Block";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "blockName",
            "Block Name",
            ParameterType.String,
            "Name of the block to correct",
            defaultValue: "Tegningsskilt"),
        new ParameterDescriptor(
            "attributeTag",
            "Attribute Tag",
            ParameterType.String,
            "Tag of the attribute to correct",
            defaultValue: "SAG2"),
        new ParameterDescriptor(
            "fieldCode",
            "Field Code",
            ParameterType.String,
            "Field code to set on the attribute",
            defaultValue: "%<\\AcSm SheetSet.Description \\f \"%tc1\">%")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string blockName = GetParamOrDefault<string>(parameterValues, "blockName", "Tegningsskilt");
        string tag = GetParamOrDefault<string>(parameterValues, "attributeTag", "SAG2");
        string fieldCode = GetParamOrDefault<string>(
            parameterValues, "fieldCode", "%<\\AcSm SheetSet.Description \\f \"%tc1\">%");
        var xTx = context.Database.TransactionManager.TopTransaction;

        HashSet<BlockReference> brs = context.Database.GetBlockReferenceByName(blockName);
        if (brs.Count == 0) return new Result();

        BlockTableRecord btr = brs.First().BlockTableRecord.Go<BlockTableRecord>(xTx);
        foreach (Oid oid in btr)
        {
            if (!oid.IsDerivedFrom<AttributeDefinition>()) continue;
            AttributeDefinition attDef = oid.Go<AttributeDefinition>(xTx);
            if (attDef?.Tag != tag) continue;

            attDef.CheckOrOpenForWrite();
            attDef.TextString = fieldCode;
        }

        foreach (var br in brs)
        {
            foreach (Oid oid in br.AttributeCollection)
            {
                AttributeReference ar = oid.Go<AttributeReference>(xTx);
                if (ar.Tag == tag)
                {
                    ar.CheckOrOpenForWrite();
                    ar.TextString = fieldCode;
                }
            }
        }

        return new Result();
    }
}
