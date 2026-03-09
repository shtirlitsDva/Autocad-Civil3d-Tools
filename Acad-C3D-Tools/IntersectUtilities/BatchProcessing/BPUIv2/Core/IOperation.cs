using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public interface IOperation
{
    string TypeId { get; }
    string DisplayName { get; }
    string Description { get; }
    string Category { get; }
    IReadOnlyList<ParameterDescriptor> Parameters { get; }
    IReadOnlyList<OutputDescriptor> Outputs { get; }
    Result Execute(OperationContext context, IReadOnlyDictionary<string, object> parameterValues);
}
