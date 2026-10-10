using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.LER2;

/// <summary>
/// Civil 3D's side of the property set copier seam (<see cref="IPropertySetCopier"/>,
/// IntersectUtilitiesCOMMON): the sets copied through the AEC API, by
/// PropertySetManager.CopyAllProperties. BricsCAD's twin is NorsynDrawingToolsManaged's
/// AecPropertySetCopier. LER2SPLIT's shared body copies the sets onto its pieces through it.
/// </summary>
internal sealed class CivilPropertySetCopier : IPropertySetCopier
{
    public void CopyAll(Entity source, Entity target) =>
        PropertySetManager.CopyAllProperties(source, target);
}
