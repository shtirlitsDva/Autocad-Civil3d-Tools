using Autodesk.AutoCAD.DatabaseServices;
using Dreambuild.AutoCAD;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.Xref;

public class SetDrawOrderOp : OperationBase
{
    public override string TypeId => "Xref.SetDrawOrder";
    public override string DisplayName => "Set Draw Order";
    public override string Description => "Moves one xref above or below another in the draw order.";
    public override string Category => "Xref";

    public override IReadOnlyList<ParameterDescriptor> Parameters => new[]
    {
        new ParameterDescriptor(
            "entityXrefName",
            "Entity Xref Name",
            ParameterType.String,
            "Xref to move in draw order"),
        new ParameterDescriptor(
            "referenceXrefName",
            "Reference Xref Name",
            ParameterType.String,
            "Xref to position relative to"),
        new ParameterDescriptor(
            "orderType",
            "Order Type",
            ParameterType.EnumChoice,
            "Position the entity xref over or under the reference xref",
            enumChoices: new[] { "Over", "Under" },
            defaultValue: "Under")
    };

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        string entityName = GetStringParam(parameterValues, "entityXrefName");
        string refName = GetStringParam(parameterValues, "referenceXrefName");
        string orderType = GetParamOrDefault<string>(parameterValues, "orderType", "Under");

        if (string.IsNullOrWhiteSpace(refName)) return new Result();

        Transaction xTx = context.Database.TransactionManager.TopTransaction;
        BlockTableRecord ms = context.Database.GetModelspaceForWrite();
        DrawOrderTable dot = ms.DrawOrderTableId.Go<DrawOrderTable>(xTx, OpenMode.ForWrite);

        Oid entityBrId = Oid.Null;
        Oid refBrId = Oid.Null;

        foreach (Oid oid in ms)
        {
            var br = oid.Go<BlockReference>(xTx);
            if (br == null) continue;
            BlockTableRecord btr = xTx.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
            if (!btr.IsFromExternalReference) continue;
            if (btr.Name == entityName) entityBrId = br.Id;
            if (btr.Name == refName) refBrId = br.Id;
        }

        if (entityBrId != Oid.Null && refBrId != Oid.Null)
        {
            ObjectIdCollection idCol = new ObjectIdCollection(new Oid[] { entityBrId });
            if (orderType == "Under") dot.MoveBelow(idCol, refBrId);
            else dot.MoveAbove(idCol, refBrId);
        }

        return new Result();
    }
}
