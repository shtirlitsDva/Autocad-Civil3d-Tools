using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

using AcadOverrules.ViewFrameGripOverrule;
using AcadOverrules.VertexCircles.UI;
using System;
using System.Collections.Generic;

[assembly: CommandClass(typeof(AcadOverrules.NoCommands))]

namespace AcadOverrules
{
    public class Commands : IExtensionApplication
    {
        #region Interface members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\n(ノ◕ヮ◕)ノ*:・゚✧ AcadOverrules loaded! ✧゚・:*ヽ(◕ヮ◕ヽ)\n");
        }

        public void Terminate()
        {
            OverruleRegistry.DisableAll();
        }
        #endregion

        /// <summary>
        /// Toggles an overrule of type <typeparamref name="T"/> that targets
        /// <see cref="Polyline"/> on or off, then regenerates the active view.
        /// All registration bookkeeping lives in <see cref="OverruleRegistry"/>.
        /// </summary>
        private static void Toggle<T>() where T : Overrule, new()
        {
            OverruleRegistry.Toggle<T>(typeof(Polyline));
            RegenActiveDocument();
        }

        /// <summary>
        /// Whether the overrule is currently registered. Lets a settings UI show the same
        /// state as the TOGGLE command that owns it.
        /// </summary>
        internal static bool IsOverruleActive<T>() where T : Overrule =>
            OverruleRegistry.IsActive<T>();

        /// <summary>
        /// Turns an overrule on or off explicitly, as opposed to <see cref="Toggle{T}"/>.
        /// </summary>
        internal static void SetOverruleActive<T>(bool active) where T : Overrule, new()
        {
            if (active == OverruleRegistry.IsActive<T>()) return;
            Toggle<T>();
        }

        internal static void RegenActiveDocument() =>
            Application.DocumentManager.MdiActiveDocument?.Editor.Regen();

        /// <command>TOGGLEFJVLABEL</command>
        /// <summary>
        /// Labels pipe with system prefix, size and type, ie. DN50-T.
        /// T - Twin, E - Enkelt.
        /// Labels arcs with radius and marking for buerør, in-situ buk
        /// and impossible radius.
        /// Marks small angle deviations between pl segments.
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGLEFJVLABEL")]
        public static void togglefjvlabeloverrule() => Toggle<FjvPolylineLabel>();

        /// <command>TOGGLEPOLYDIR</command>
        /// <summary>
        /// Creates arrows for all polylines that visualise the direction of the polyline.
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGLEPOLYDIR")]
        public static void togglepolydiroverrule() => Toggle<PolylineDirection>();

        /// <command>TOGGLEFJVDIR</command>
        /// <summary>
        /// This applies only to polylines that resides in layers that correspond to pipe systems.
        /// Creates arrows for all these polylines that visualise the direction of the polyline.
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGLEFJVDIR")]
        public static void togglefjvdiroverrule() => Toggle<PolylineDirFjv>();

        /// <command>TOGGLEPOLYARCS</command>
        /// <summary>
        /// Highlights arc segments of polylines with cyan color overlay.
        /// Straight segments are not affected. Flags non-tangent arc junctions
        /// with an orange warning sign and labels non-collinear line-line
        /// junctions with the deviation angle.
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGLEPOLYARCS")]
        public static void togglepolyarcs() => Toggle<PolylineArcHighlight>();

        /// <command>TOGGLEDRAFTVERTICES</command>
        /// <summary>
        /// Draws small circles at each vertex of polylines on layer '0-FJV-PROFILE-DRAFT'
        /// with red color. Circles are drawn in white/black (ACI 7).
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGLEDRAFTVERTICES")]
        public static void toggledraftvertices() => Toggle<DraftPolylineVerticeMark>();

        /// <command>TOGGLEPOLYVERTICES</command>
        /// <summary>
        /// Draws a circle at every vertex of every polyline in the drawing.
        /// The circle diameter is 0.01 larger than the polyline's width, so it always
        /// sits just outside the drawn band, with a 0.05 floor for zero width polylines.
        /// It is drawn in a vivid marker colour on the complementary hue of the polyline
        /// (magenta for white/black/grey polylines) with twice the polyline's lineweight.
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGLEPOLYVERTICES")]
        public static void togglepolyvertices() => Toggle<PolylineVertexCircles>();

        /// <command>TOGGLEPOLYVERTICESSETTINGS</command>
        /// <summary>
        /// Opens the settings window for TOGGLEPOLYVERTICES: circle radius, which layers the
        /// overrule applies to (layer names and AutoCAD wildcard masks), marker colour and
        /// lineweight factor. Settings are grouped in named profiles; the active profile is
        /// saved when the window closes. Edits preview live in the drawing.
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGLEPOLYVERTICESSETTINGS")]
        public static void togglepolyverticessettings()
        {
            try
            {
                new VertexCirclesSettingsWindow().ShowDialog();
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    $"\nFailed to open the vertex circles settings: {ex}\n");
            }
        }

        /// <command>TOGGELPOLYVERTICESSETTINGS</command>
        /// <summary>
        /// Alias for TOGGLEPOLYVERTICESSETTINGS, kept because the command was first asked for
        /// under this spelling.
        /// </summary>
        /// <category>Overrules</category>
        [CommandMethod("TOGGELPOLYVERTICESSETTINGS")]
        public static void toggelpolyverticessettings() => togglepolyverticessettings();

#if DEBUG
        [CommandMethod("TOGGLEGRIPOR")]
        public static void togglegripoverrule() => Toggle<GripVectorOverrule>();

        [CommandMethod("TOGGLEVIEWFRAMESOVERRULE")]
        public static void toggleviewframeoverrule() => Toggle<ViewFrameCentreGripOverrule>();
#endif
    }

    /// <summary>
    /// Tracks the overrules this module has enabled so they can all be removed and
    /// disposed on <see cref="Commands.Terminate"/>. One entry per overrule type:
    /// toggling on registers it, toggling off removes and disposes it.
    /// </summary>
    internal static class OverruleRegistry
    {
        private static readonly Dictionary<Type, (RXClass TargetClass, Overrule Instance)> _active =
            new Dictionary<Type, (RXClass, Overrule)>();

        /// <summary>True while <typeparamref name="T"/> is registered.</summary>
        public static bool IsActive<T>() where T : Overrule =>
            _active.ContainsKey(typeof(T));

        /// <summary>
        /// Enables <typeparamref name="T"/> against <paramref name="targetType"/> if it is
        /// not already active, otherwise disables and disposes the active instance.
        /// </summary>
        public static void Toggle<T>(Type targetType) where T : Overrule, new()
        {
            if (_active.TryGetValue(typeof(T), out var entry))
            {
                Overrule.RemoveOverrule(entry.TargetClass, entry.Instance);
                entry.Instance.Dispose();
                _active.Remove(typeof(T));
                return;
            }

            RXClass targetClass = RXObject.GetClass(targetType);
            T overrule = new T();
            Overrule.AddOverrule(targetClass, overrule, false);
            Overrule.Overruling = true;
            _active[typeof(T)] = (targetClass, overrule);
        }

        /// <summary>
        /// Removes and disposes every active overrule. Safe to call when none are active.
        /// </summary>
        public static void DisableAll()
        {
            foreach (var entry in _active.Values)
            {
                try
                {
                    Overrule.RemoveOverrule(entry.TargetClass, entry.Instance);
                    entry.Instance.Dispose();
                }
                catch
                {
                    // Best-effort teardown on unload; keep going for the rest.
                }
            }
            _active.Clear();
        }
    }

    public class NoCommands { }
}
