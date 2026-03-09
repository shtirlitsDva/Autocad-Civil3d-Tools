using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public interface IOperation
{
    string TypeId { get; }
    string DisplayName { get; }
    string Description { get; }
    string Category { get; }
    IReadOnlyList<ParameterDescriptor> Parameters { get; }
    Result Execute(OperationContext context, IReadOnlyDictionary<string, object> parameterValues);
}
