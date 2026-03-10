using Autodesk.AutoCAD.ApplicationServices;

using DimensioneringV2.Models;
using DimensioneringV2.StateMachine;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services;

internal class DocumentHnState
{
    public HydraulicNetwork? ActiveNetwork { get; set; }
    public List<HydraulicNetwork> CalculatedNetworks { get; } = new();
    public CalcCounter Counter { get; set; } = new();
    public StateMachine<HnState, HnEvent>? Fsm { get; set; }
}

internal class DocumentStateStore
{
    private readonly Dictionary<string, DocumentHnState> _states = new();

    public DocumentHnState GetOrCreate(string docKey)
    {
        if (!_states.TryGetValue(docKey, out var state))
        {
            state = new DocumentHnState();
            _states[docKey] = state;
        }
        return state;
    }

    public void Remove(string docKey) => _states.Remove(docKey);

    public bool HasUnsavedNetworks(string docKey)
    {
        if (!_states.TryGetValue(docKey, out var state)) return false;
        return state.CalculatedNetworks.Any(hn => !hn.IsSaved);
    }

    public static string GetDocKey(Document? doc)
    {
        if (doc == null) return "__null__";
        return !string.IsNullOrEmpty(doc.Name)
            ? doc.Name
            : doc.GetHashCode().ToString();
    }
}
