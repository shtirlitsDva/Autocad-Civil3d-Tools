using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DataShortcuts;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager;
using Dreambuild.AutoCAD;
using static IntersectUtilities.UtilsCommon.Utils;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Operations.DataShortcut;

public class CreateSurfaceProfileReferenceOp : OperationBase
{
    public override string TypeId => "DataShortcut.CreateSurfaceProfileRef";
    public override string DisplayName => "Create Surface Profile Reference";
    public override string Description => "Creates data shortcut references for surface profiles matching alignments in the drawing.";
    public override string Category => "DataShortcut";

    public override IReadOnlyList<ParameterDescriptor> Parameters => Array.Empty<ParameterDescriptor>();

    public override Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues)
    {
        var xTx = context.Database.TransactionManager.TopTransaction;
        var als = context.Database.HashSetOfType<Autodesk.Civil.DatabaseServices.Alignment>(xTx);

        System.Text.RegularExpressions.Regex reg1 = new(@"(?<number>\d{2,3})");

        bool isValidCreation = false;
        DataShortcuts.DataShortcutManager sm =
            DataShortcuts.CreateDataShortcutManager(ref isValidCreation);

        if (!isValidCreation)
        {
            return new Result(ResultStatus.FatalError, "DataShortcutManager failed to be created!");
        }

        int publishedCount = sm.GetPublishedItemsCount();

        foreach (Autodesk.Civil.DatabaseServices.Alignment al in als)
        {
            string number = reg1.Match(al.Name).Groups["number"].Value;
            prdDbg($"{al.Name} -> {number}");

            for (int i = 0; i < publishedCount; i++)
            {
                var item = sm.GetPublishedItemAt(i);

                if (item.DSEntityType == DataShortcutEntityType.Alignment &&
                    item.Name.StartsWith(number))
                {
                    var items = GetItemsByPipelineNumber(sm, number);

                    foreach (int idx in items)
                    {
                        var entity = sm.GetPublishedItemAt(idx);

                        if (entity.DSEntityType == DataShortcutEntityType.Alignment) continue;

                        if (entity.Name.Contains("surface"))
                        {
                            sm.CreateReference(idx, context.Database);
                        }
                    }
                }
            }
        }

        sm.Dispose();

        return new Result();
    }

    static IEnumerable<int> GetItemsByPipelineNumber(
        DataShortcuts.DataShortcutManager dsMan, string pipelineNumber)
    {
        int count = dsMan.GetPublishedItemsCount();

        for (int j = 0; j < count; j++)
        {
            string name = dsMan.GetPublishedItemAt(j).Name;
            if (name.StartsWith(pipelineNumber)) yield return j;
        }
    }
}
