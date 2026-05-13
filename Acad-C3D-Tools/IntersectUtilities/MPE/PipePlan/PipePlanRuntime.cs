namespace IntersectUtilities.MPE.PipePlan;

internal static class PipePlanRuntime
{
    static PipePlanRuntime()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => State.Dispose();
    }

    internal static PipePlanState State { get; } = new();
}
