using System;

#if BRICSCAD
using Bricscad.ApplicationServices;
#else
using Autodesk.AutoCAD.ApplicationServices;
#endif

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
