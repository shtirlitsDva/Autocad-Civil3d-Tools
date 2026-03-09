using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using IntersectUtilities.UtilsCommon.DataManager;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public class OperationContext
{
    public Database Database { get; }
    public Transaction Transaction { get; }
    public CivilDocument CivilDocument { get; }
    public Dictionary<string, object> SharedState { get; }
    public DataReferencesOptions? DataReferences { get; set; }
    public Dictionary<string, object> StepOutputs { get; } = new();

    public OperationContext(
        Database database,
        Transaction transaction,
        CivilDocument civilDocument,
        Dictionary<string, object> sharedState)
    {
        Database = database;
        Transaction = transaction;
        CivilDocument = civilDocument;
        SharedState = sharedState;
    }

}
