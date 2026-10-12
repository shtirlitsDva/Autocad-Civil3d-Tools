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
    ///
    /// An overrule that draws a nested drawable (<see cref="PolylineRun"/>) passes
    /// <c>drawsNested: true</c> and adds <see cref="AttributesFlags.DrawableUsesNesting"/>,
    /// as Bricsys's sample PointReadableElevDrawOverrule does for its nested entities.
    /// </summary>
    public abstract class HostDrawableOverrule : DrawableOverrule
    {
        private readonly bool _drawsOnAnyThread;
        private readonly bool _drawsNested;

        protected HostDrawableOverrule(bool drawsOnAnyThread, bool drawsNested)
        {
            _drawsOnAnyThread = drawsOnAnyThread;
            _drawsNested = drawsNested;
        }

        public override AdvancedSupportFlags RegenSupportFlags(Drawable drawable) =>
            _drawsOnAnyThread ? base.RegenSupportFlags(drawable) : AdvancedSupportFlags.None;

        public override int SetAttributes(Drawable drawable, DrawableTraits traits)
        {
            int flags = base.SetAttributes(drawable, traits);
            return _drawsNested ? flags | (int)AttributesFlags.DrawableUsesNesting : flags;
        }
    }
}
