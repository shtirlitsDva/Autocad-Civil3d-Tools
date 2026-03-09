using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IntersectUtilities.BatchProcessing.BPUIv2.Core;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Registry;

/// <summary>
/// Singleton that discovers and registers all IOperation implementations
/// via reflection at startup.
/// </summary>
public class OperationRegistry
{
    private static readonly Lazy<OperationRegistry> _instance = new(() => new OperationRegistry());
    public static OperationRegistry Instance => _instance.Value;

    private readonly Dictionary<string, IOperation> _operations;
    private readonly List<OperationCatalogEntry> _catalog;

    private OperationRegistry()
    {
        _operations = new Dictionary<string, IOperation>(StringComparer.OrdinalIgnoreCase);
        _catalog = new List<OperationCatalogEntry>();

        DiscoverOperations();
    }

    private void DiscoverOperations()
    {
        var assembly = Assembly.GetAssembly(typeof(OperationRegistry));
        if (assembly is null) return;

        var operationTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract
                        && !t.IsInterface
                        && typeof(IOperation).IsAssignableFrom(t));

        foreach (var type in operationTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is not IOperation operation)
                    continue;

                _operations[operation.TypeId] = operation;

                _catalog.Add(new OperationCatalogEntry(
                    typeId: operation.TypeId,
                    displayName: operation.DisplayName,
                    description: operation.Description,
                    category: operation.Category,
                    parameters: operation.Parameters,
                    outputs: operation.Outputs));
            }
            catch
            {
                // Skip types that cannot be instantiated.
            }
        }
    }

    /// <summary>
    /// Lookup an operation by its TypeId.
    /// </summary>
    public IOperation? GetOperation(string typeId)
    {
        return _operations.TryGetValue(typeId, out var op) ? op : null;
    }

    /// <summary>
    /// All discovered operation catalog entries.
    /// </summary>
    public IReadOnlyList<OperationCatalogEntry> Catalog => _catalog.AsReadOnly();

    /// <summary>
    /// Distinct category names, sorted alphabetically.
    /// </summary>
    public IEnumerable<string> Categories =>
        _catalog.Select(e => e.Category).Distinct().OrderBy(c => c);
}
