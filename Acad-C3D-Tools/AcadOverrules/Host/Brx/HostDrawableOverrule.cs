using Teigha.GraphicsInterface;
using Teigha.Runtime;

namespace AcadOverrules
{
    /// <summary>
    /// The base of every overrule in this plugin; AutoCAD's twin is Host\Acad.
    ///
    /// BricsCAD regenerates on several threads by default (MTFLAGS) and calls an overrule's
    /// WorldDraw from those threads unless the overrule says otherwise (BRX PortingManual,
    /// "AcGiDrawableOverrule ... supports overruling multi-threading support"). An overrule
    /// whose draw touches the database, shared state or the editor passes
    /// <c>drawsOnAnyThread: false</c> and answers <see cref="AdvancedSupportFlags.None"/>, so
    /// BricsCAD draws the entities it overrules on the main thread only, as Bricsys's sample
    /// ForceSingleThreadedDrawOverrule does.
    /// </summary>
    public abstract class HostDrawableOverrule : DrawableOverrule
    {
        private readonly bool _drawsOnAnyThread;

        protected HostDrawableOverrule(bool drawsOnAnyThread)
        {
            _drawsOnAnyThread = drawsOnAnyThread;
        }

        public override AdvancedSupportFlags RegenSupportFlags(Drawable drawable) =>
            _drawsOnAnyThread ? base.RegenSupportFlags(drawable) : AdvancedSupportFlags.None;
    }
}
