using Result = IntersectUtilities.UtilsCommon.Result;

namespace IntersectUtilities.BatchProcessing.BPUIv2.Core;

public abstract class OperationBase : IOperation
{
    public abstract string TypeId { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public abstract IReadOnlyList<ParameterDescriptor> Parameters { get; }
    public virtual IReadOnlyList<OutputDescriptor> Outputs => [];

    public abstract Result Execute(
        OperationContext context,
        IReadOnlyDictionary<string, object> parameterValues);

    protected T GetParam<T>(IReadOnlyDictionary<string, object> values, string name)
    {
        if (!values.TryGetValue(name, out var value))
            throw new KeyNotFoundException(
                $"Required parameter '{name}' not found for operation '{TypeId}'.");

        if (value is T typed)
            return typed;

        throw new InvalidCastException(
            $"Parameter '{name}' for operation '{TypeId}' expected type {typeof(T).Name} " +
            $"but got {value?.GetType().Name ?? "null"}.");
    }

    protected T GetParamOrDefault<T>(
        IReadOnlyDictionary<string, object> values,
        string name,
        T defaultValue)
    {
        if (!values.TryGetValue(name, out var value))
            return defaultValue;

        if (value is T typed)
            return typed;

        return defaultValue;
    }

    protected string GetStringParam(IReadOnlyDictionary<string, object> values, string name)
        => GetParam<string>(values, name);

    protected int GetIntParam(IReadOnlyDictionary<string, object> values, string name)
        => GetParam<int>(values, name);

    protected void SetOutput(OperationContext context, string name, object value)
        => context.StepOutputs[name] = value;

    protected Counter GetCounter(OperationContext context)
    {
        if (!context.SharedState.TryGetValue("_counter", out var counterObj))
            throw new InvalidOperationException(
                $"Counter not found in SharedState for operation '{TypeId}'. " +
                "Ensure a Counter parameter is configured in the sequence.");

        if (counterObj is Counter counter)
            return counter;

        throw new InvalidCastException(
            $"SharedState['_counter'] expected type Counter but got {counterObj.GetType().Name}.");
    }
}
