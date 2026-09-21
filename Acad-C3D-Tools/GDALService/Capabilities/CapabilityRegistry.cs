using GDALService.Common;

namespace GDALService.Capabilities;

// Every registered capability by its wire type. Two capabilities claiming the
// same type is a wiring bug, refused when the service starts rather than
// resolved by whichever was registered last.
internal sealed class CapabilityRegistry
{
    private readonly IReadOnlyDictionary<string, ICapability> _byType;

    private CapabilityRegistry(IReadOnlyDictionary<string, ICapability> byType) { _byType = byType; }

    public static Result<CapabilityRegistry> Create(IEnumerable<ICapability> capabilities)
    {
        var all = capabilities.ToList();
        var twice = all.GroupBy(c => c.Type, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        return twice.Count > 0
            ? new Fault(FaultKind.Internal, "capability type registered more than once: " + string.Join(", ", twice))
            : new Ok<CapabilityRegistry>(new CapabilityRegistry(all.ToDictionary(c => c.Type, StringComparer.Ordinal)));
    }

    public Result<ICapability> Find(string type) =>
        _byType.TryGetValue(type, out var capability)
            ? new Ok<ICapability>(capability)
            : new Fault(FaultKind.InvalidArgs, $"Unknown type '{type}'");
}
