using System;
using Autodesk.AutoCAD.ApplicationServices;

namespace NSLOAD
{
    /// <summary>
    /// Where NSLOAD reports a failure it has caught but must not rethrow, so a
    /// swallowed exception still reaches the command line.
    /// </summary>
    internal static class NsLoadDiagnostics
    {
        public static void Report(string where, Exception ex)
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\nNSLOAD: {where} failed: {ex.Message}");
        }
    }
}
