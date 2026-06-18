using Autodesk.AutoCAD.Runtime;

namespace GraphViewV3;

public class GraphViewCommands
{
    [CommandMethod("GRAPHVIEWV3")]
    public void GraphViewV3()
    {
        GraphViewV3Plugin.ShowPalette();
    }
}
