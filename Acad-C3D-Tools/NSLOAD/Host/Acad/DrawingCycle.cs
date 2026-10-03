using System;
using System.Collections.Generic;

namespace NSLOAD.Native
{
    /// <summary>
    /// The drawings closed around a native group's unload and reopened after its
    /// next load. On AutoCAD there are none: it unloads a module under its live
    /// objects and keeps them as stand-ins, so the drawings stay open. The
    /// BricsCAD build has its own copy of this file; the two must declare the
    /// same members.
    /// </summary>
    internal sealed class DrawingCycle
    {
        /// <summary>A cycle that closed nothing.</summary>
        public static readonly DrawingCycle None = new();

        private DrawingCycle() { }

        /// <summary>The drawings this cycle closed, by full path.</summary>
        public IReadOnlyList<string> Closed => Array.Empty<string>();

        /// <summary>Closes nothing: AutoCAD unloads a module with its drawings open.</summary>
        public static DrawingCycle Close(Action<string> say) => None;

        /// <summary>Nothing was closed, so nothing reopens.</summary>
        public void Reopen(Action<string> say) { }

        /// <summary>Nothing: AutoCAD never converts a stand-in in a drawing that is
        /// already open, so there is no half-converted drawing to repair. A drawing
        /// read before the load shows its objects once it is opened again.</summary>
        public static void ReopenStandInHolders(string group, Action<string> say) { }
    }
}
