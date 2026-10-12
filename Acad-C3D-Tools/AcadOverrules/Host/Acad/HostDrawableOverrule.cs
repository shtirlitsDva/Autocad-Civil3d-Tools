using Autodesk.AutoCAD.GraphicsInterface;

namespace AcadOverrules
{
    /// <summary>
    /// The base of every overrule in this plugin; BricsCAD's twin is Host\Brx.
    ///
    /// The two claims an overrule makes here (whether its draw is safe on any thread, whether
    /// it draws a nested drawable) are what BricsCAD needs to be told. AutoCAD's managed
    /// DrawableOverrule has no regen-threading hook, and its <see cref="PolylineRun"/> draws no
    /// nested drawable, so on AutoCAD both claims change nothing.
    /// </summary>
    public abstract class HostDrawableOverrule : DrawableOverrule
    {
        protected HostDrawableOverrule(bool drawsOnAnyThread, bool drawsNested)
        {
        }
    }
}
