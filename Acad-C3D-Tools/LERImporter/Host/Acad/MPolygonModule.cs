using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace LERImporter.Host.Acad;

/// <summary>
/// The MPolygon class on Civil 3D lives in its own object enabler, loaded once
/// before the first graveforespoergsel polygon is drawn. Its BricsCAD twin
/// (Host\Brx\MPolygonModule.cs) has nothing to load.
/// </summary>
internal static class MPolygonModule
{
    public static void Load() =>
        SystemObjects.DynamicLinker.LoadModule(
            "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
}
