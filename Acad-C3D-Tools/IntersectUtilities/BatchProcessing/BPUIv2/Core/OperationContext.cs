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

    /// <summary>
    /// Clears per-drawing transient state (keys starting with "_detached_")
    /// but preserves cross-drawing state (keys starting with "_counter", "_dro").
    /// </summary>
    public void ClearTransientState()
    {
        var keysToRemove = SharedState.Keys
            .Where(k => k.StartsWith("_detached_"))
            .ToList();

        foreach (var key in keysToRemove)
            SharedState.Remove(key);
    }
}
