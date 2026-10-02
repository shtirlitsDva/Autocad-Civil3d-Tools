using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

using Bricscad.ApplicationServices;

using Exception = System.Exception;

namespace NSLOAD.Native
{
    /// <summary>
    /// The drawings closed around a native group's unload and reopened after its
    /// next load. The AutoCAD build has its own copy of this file; the two must
    /// declare the same members.
    /// </summary>
    /// <remarks>
    /// BricsCAD will not unload a native module while an open drawing holds
    /// objects of its classes. AutoCAD unloads and keeps the objects as
    /// stand-ins; BricsCAD sends kUnloadAppMsg, and a module without its own
    /// guard tears itself down under the live objects and the next regen crashes.
    /// The NDH dbx guards itself (it refuses while it has live objects), so here
    /// the unload would simply be refused. Which drawings hold a module's objects
    /// cannot be asked generically, so every NAMED drawing is closed before the
    /// unload is tried, and the next successful load of the group opens them again.
    ///
    /// <para>Nothing is ever discarded. A drawing with unsaved changes stops the
    /// cycle before anything is closed, and the drafter is told which to save.</para>
    ///
    /// <para>One drawing always stays open: BricsCAD on its Start tab has no
    /// document to run anything in. An unnamed drawing with no unsaved changes
    /// (it holds no objects) is kept; without one, a blank drawing is added.</para>
    ///
    /// <para>Documents close and open only in application context. NSLOADMGR's
    /// buttons run there; a typed command (NSLOAD, a plugin's own name) runs in
    /// its drawing, so a reopen asked for from one waits on the main thread's
    /// dispatcher until the command has ended. A posted wait, not
    /// Application.Idle, which BricsCAD may never raise.</para>
    ///
    /// <para>ADAPTED from DevReload's <c>DevReload.Oarx.OarxDrawingCycle</c>
    /// (DevReload dd05a34), with only its refuse policy: a drafter's drawings are
    /// never saved or discarded for them.</para>
    /// </remarks>
    internal sealed class DrawingCycle
    {
        private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(100);

        /// <summary>A cycle that closed nothing.</summary>
        public static readonly DrawingCycle None = new(new List<string>(), null);

        private readonly List<string> _closed;
        private readonly string? _active;

        private DrawingCycle(List<string> closed, string? active)
        {
            _closed = closed;
            _active = active;
        }

        /// <summary>The drawings this cycle closed, by full path, in the order they were open.</summary>
        public IReadOnlyList<string> Closed => _closed;

        /// <summary>
        /// Closes every named drawing, or refuses with an
        /// <see cref="OarxModuleException"/> saying why. A refusal closes nothing.
        /// </summary>
        public static DrawingCycle Close(Action<string> say)
        {
            var docs = Application.DocumentManager;
            if (!docs.IsApplicationContext)
                throw new OarxModuleException(
                    "BricsCAD closes the open drawings to unload a native group, and it " +
                    "cannot while a command is running. Finish the command, then unload " +
                    "again from NSLOADMGR. Nothing was closed or unloaded.");

            var all = docs.Cast<Document>().ToList();
            var unsaved = all.Where(d => !IsSaved(d)).ToList();
            if (unsaved.Count > 0)
                throw new OarxModuleException(
                    $"Save {Names(unsaved)} first. BricsCAD will not unload a native module " +
                    "while a drawing holds its objects, so NSLOAD closes the drawings and " +
                    "reopens them after the next load. Nothing was closed or unloaded.");

            string? active = docs.MdiActiveDocument?.Name;
            var keep = all.FirstOrDefault(d => !d.IsNamedDrawing) ?? docs.Add(string.Empty);

            var closed = new List<string>();
            foreach (var d in all.Where(d => d.IsNamedDrawing))
            {
                string path = d.Name;
                // Every drawing is saved (checked above), so discarding loses nothing.
                d.CloseAndDiscard();
                closed.Add(path);
                say($"closed {path}");
            }
            docs.MdiActiveDocument = keep;
            return new DrawingCycle(closed, active);
        }

        /// <summary>Opens the closed drawings again, in their order, and makes the
        /// one that was active active again. A drawing that will not open is
        /// reported, and the rest still open. Inside a command, waits for it to end.</summary>
        public void Reopen(Action<string> say)
        {
            if (_closed.Count == 0) return;
            ReopenWhenFree(Dispatcher.CurrentDispatcher, say);
        }

        private void ReopenWhenFree(Dispatcher main, Action<string> say)
        {
            if (Application.DocumentManager.IsApplicationContext)
            {
                OpenAll(say);
                return;
            }

            // Runs on the main thread's dispatcher and stops after one tick; a
            // drawing still in a command starts the next wait.
            _ = new DispatcherTimer(RetryInterval, DispatcherPriority.Background, (sender, _) =>
            {
                ((DispatcherTimer)sender!).Stop();
                ReopenWhenFree(main, say);
            }, main);
        }

        private void OpenAll(Action<string> say)
        {
            var docs = Application.DocumentManager;
            foreach (string path in _closed)
            {
                if (IsOpen(path)) continue;
                try
                {
                    docs.Open(path, false);
                    say($"reopened {path}");
                }
                catch (Exception ex)
                {
                    say($"could NOT reopen {path}: {ex.Message}");
                }
            }
            if (_active == null) return;
            var active = docs.Cast<Document>().FirstOrDefault(d =>
                string.Equals(d.Name, _active, StringComparison.OrdinalIgnoreCase));
            if (active != null) docs.MdiActiveDocument = active;
        }

        private static bool IsOpen(string path) =>
            Application.DocumentManager.Cast<Document>().Any(d =>
                string.Equals(d.Name, path, StringComparison.OrdinalIgnoreCase));

        /// <summary>Has the drawing no unsaved changes? The COM document's Saved
        /// flag answers without making the drawing active (the managed Document has
        /// no such property). If it cannot be read, the drawing counts as unsaved:
        /// the cycle then refuses rather than risk the drafter's work.</summary>
        private static bool IsSaved(Document d)
        {
            try
            {
                dynamic com = d.AcadDocument;
                return (bool)com.Saved;
            }
            catch (Exception ex)
            {
                NsLoadDiagnostics.Report($"reading whether {d.Name} is saved", ex);
                return false;
            }
        }

        private static string Names(IEnumerable<Document> docs) =>
            string.Join(", ", docs.Select(d => $"'{d.Name}'"));
    }
}
