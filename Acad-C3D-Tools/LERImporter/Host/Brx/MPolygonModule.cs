namespace LERImporter.Host.Brx;

/// <summary>
/// MPolygon is part of BricsCAD itself (Teigha.DatabaseServices.MPolygon over
/// the core's AcDbMPolygon), so there is no module to load. Its Civil 3D twin
/// (Host\Acad\MPolygonModule.cs) loads the object enabler.
/// </summary>
internal static class MPolygonModule
{
    public static void Load() { }
}
