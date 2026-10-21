using Autodesk.AutoCAD.Runtime;

namespace IntersectUtilities;

public partial class Intersect
{
    /// <command>LERCOMPARE</command>
    /// <summary>Compare model-space LER polylines and Property Sets with an older DWG.</summary>
    /// <category>LER</category>
    [CommandMethod("LERCOMPARE", CommandFlags.Modal)]
    public void LerCompare() => global::IntersectUtilities.LerCompare.LerCompareRuntime.Show();

    // The modeless palette queues a normal command so reads execute in document context.
    [CommandMethod("LERCOMPARE_RUN", CommandFlags.Modal)]
    public void LerCompareRun() => global::IntersectUtilities.LerCompare.LerCompareRuntime.Run();
}
