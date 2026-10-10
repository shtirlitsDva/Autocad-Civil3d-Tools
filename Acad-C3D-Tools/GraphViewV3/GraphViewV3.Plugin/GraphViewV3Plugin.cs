using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

using EventManager;

[assembly: ExtensionApplication(typeof(GraphViewV3.GraphViewV3Plugin))]

namespace GraphViewV3;

/// <summary>
/// Plugin lifecycle. All cleanup state is STATIC because DevReload calls Terminate()
/// on a different instance than the one that ran Initialize() (see the agentic-dev
/// skill). Terminate() must unpin everything that roots this assembly: the palette and
/// the event manager.
/// </summary>
public class GraphViewV3Plugin : IExtensionApplication
{
    internal static AcadEventManager? Events { get; private set; }
    private static GraphViewPalette? _palette;

    public void Initialize()
    {
        Events = new AcadEventManager();
    }

    public void Terminate()
    {
        if (_palette != null)
        {
            _palette.Dispose();
            _palette = null;
        }
        Events?.Dispose();
        Events = null;
    }

    /// <summary>Show (creating once) the live graph palette. Runs on the AutoCAD main thread.</summary>
    internal static void ShowPalette()
    {
        _palette ??= new GraphViewPalette();
        _palette.Visible = true;
    }
}
