using Autodesk.AutoCAD.Runtime;

namespace PipePlan.Plugin;

public sealed class PipePlanPlugin : IExtensionApplication
{
    internal static PipePlanState State { get; } = new();

    public void Initialize()
    {
    }

    public void Terminate()
    {
        State.Dispose();
    }
}
