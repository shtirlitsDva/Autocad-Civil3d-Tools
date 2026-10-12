using Autodesk.AutoCAD.GraphicsInterface;

namespace AcadOverrules
{
    /// <summary>
    /// The base of every overrule in this plugin; BricsCAD's twin is Host\Brx.
    ///
    /// Whether an overrule's draw is safe on any thread is what BricsCAD needs to be told.
    /// AutoCAD's managed DrawableOverrule has no regen-threading hook, so on AutoCAD the
    /// claim changes nothing.
    /// </summary>
    public abstract class HostDrawableOverrule : DrawableOverrule
    {
        protected HostDrawableOverrule(bool drawsOnAnyThread)
        {
        }
    }
}
