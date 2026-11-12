#if DEBUG
using Autodesk.AutoCAD.Runtime;

// AutoCAD's automatic command registrations survive collectible plugin reloads.
// In Debug, let DevReload register/unregister the real commands itself.
[assembly: CommandClass(typeof(IntersectUtilities.DevReloadCommandClass))]

namespace IntersectUtilities;

public sealed class DevReloadCommandClass
{
}
#endif
