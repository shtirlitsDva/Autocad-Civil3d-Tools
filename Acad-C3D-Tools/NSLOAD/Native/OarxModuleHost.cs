using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Autodesk.AutoCAD.Runtime;

using Exception = System.Exception;

namespace NSLOAD.Native
{
    /// <summary>
    /// Raised when a native module refuses to load or unload. Carries a message
    /// written for the drafter reading the command line, not a status code.
    /// </summary>
    public class OarxModuleException : PluginRefusedException
    {
        public OarxModuleException(string message) : base(message) { }
        public OarxModuleException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// The only place in NSLOAD that touches AutoCAD's dynamic linker.
    /// </summary>
    /// <remarks>
    /// COPIED from DevReload's <c>DevReload.Oarx.OarxModuleHost</c> (DevReload
    /// 932eba6), without the build-time parts (the writability probe and the
    /// "why is it still locked" report exist there to guard a rebuild, which
    /// NSLOAD never does). The two copies are maintained by hand: a fix to one
    /// belongs in the other too.
    ///
    /// The behaviour was measured against Civil 3D 2025 (DevReload
    /// <c>docs/oarx-port/research.md</c>, findings F1-F8). Three findings are
    /// load-bearing and easy to undo by accident:
    ///
    /// <list type="number">
    /// <item><b>F1</b> — <c>UnloadModule</c>'s second argument MUST be false.
    /// Passing true throws InvalidOperationException for every module, in every
    /// calling context.</item>
    /// <item><b>F2</b> — the unload is synchronous. The module is unregistered
    /// and, unless something still imports from it, unmapped before the call
    /// returns.</item>
    /// <item><b>F4</b> — load and unload APIs are paired. A module loaded through
    /// <c>LoadModule</c> is invisible to the ADS application table, so LISP
    /// <c>arxunload</c> cannot unload it, and vice versa. Do not mix.</item>
    /// </list>
    /// </remarks>
    internal static class OarxModuleHost
    {
        // Scopes the native DLL search path around a load so a module's
        // dependencies resolve out of its own folder. Deliberately NOT
        // AddDllDirectory: that returns a cookie only RemoveDllDirectory
        // releases, and a reload loop calling it per cycle accumulates
        // process-wide search entries that are never reclaimed.
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectoryW(string? lpPathName);

        private static DynamicLinker Linker => SystemObjects.DynamicLinker;

        /// <summary>Is this module registered with the dynamic linker right now?
        /// Takes the module FILE NAME with extension ("Foo.arx"), matched
        /// case-insensitively — not a path (F6).</summary>
        public static bool IsLoaded(string moduleFileName)
        {
            if (string.IsNullOrWhiteSpace(moduleFileName)) return false;
            try
            {
                return Linker.IsModuleLoaded(moduleFileName);
            }
            catch (Exception ex)
            {
                // Report, do not rethrow. "Not loaded" is the safe answer, but a
                // linker that cannot answer is worth knowing about: it makes a
                // group look unloaded when it may not be.
                NsLoadDiagnostics.Report($"OarxModuleHost.IsLoaded({moduleFileName})", ex);
                return false;
            }
        }

        /// <summary>
        /// Load one module by FULL PATH (F6), resolving its dependencies from its
        /// own folder. Throws <see cref="OarxModuleException"/> with a usable
        /// message rather than letting the linker's bare InvalidOperationException
        /// escape.
        /// </summary>
        public static void Load(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new OarxModuleException("Cannot load a native module with no path.");
            if (!File.Exists(fullPath))
                throw new OarxModuleException(
                    $"Native module not found: {fullPath}. OneDrive may still be syncing it.");

            string dir = Path.GetDirectoryName(fullPath)!;
            bool scoped = SetDllDirectoryW(dir);
            try
            {
                // printit:false keeps the linker quiet — NSLOAD reports load
                // results itself. asCmdrArg:false: this is not an ARX-command
                // argument.
                Linker.LoadModule(fullPath, false, false);
            }
            catch (Exception ex)
            {
                throw new OarxModuleException(
                    $"AutoCAD refused to load '{Path.GetFileName(fullPath)}'. " +
                    "The usual causes are a missing dependency next to the module, " +
                    "a module built against a different ObjectARX/AutoCAD version, " +
                    $"or a mismatched platform. Dependencies were searched in: {dir}", ex);
            }
            finally
            {
                if (scoped) SetDllDirectoryW(null);
            }

            string name = Path.GetFileName(fullPath);
            if (!IsLoaded(name))
                throw new OarxModuleException(
                    $"'{name}' reported no error but is not registered with the dynamic linker.");
        }

        /// <summary>
        /// Unload one module by FILE NAME (F6). Returns without throwing when the
        /// module is not loaded — unloading nothing is a success, not an error.
        /// </summary>
        public static void Unload(string moduleFileName)
        {
            if (string.IsNullOrWhiteSpace(moduleFileName)) return;
            if (!IsLoaded(moduleFileName)) return;

            try
            {
                // F1: the second argument MUST be false. True throws for every
                // module in every context. Do not "tidy" this to true.
                Linker.UnloadModule(moduleFileName, false);
            }
            catch (Exception ex)
            {
                throw new OarxModuleException(
                    $"AutoCAD refused to unload '{moduleFileName}'. " +
                    "The module is locked (its entry point never called " +
                    "unlockApplication) or something still depends on it.", ex);
            }

            if (IsLoaded(moduleFileName))
                throw new OarxModuleException(
                    $"'{moduleFileName}' reported no error but is still registered " +
                    "with the dynamic linker.");
        }

        /// <summary>
        /// The full path a module of this file name is mapped from in THIS
        /// process, or null when none is. After a successful unload a non-null
        /// answer proves the image did NOT leave: a module another module imports
        /// from stays mapped (and its file stays locked) even though the linker
        /// has released it.
        /// </summary>
        public static string? MappedPathInThisProcess(string moduleFileName)
        {
            try
            {
                using var self = Process.GetCurrentProcess();
                return self.Modules
                    .Cast<ProcessModule>()
                    .FirstOrDefault(m => string.Equals(
                        m.ModuleName, moduleFileName, StringComparison.OrdinalIgnoreCase))
                    ?.FileName;
            }
            catch (Exception ex)
            {
                NsLoadDiagnostics.Report(
                    $"OarxModuleHost: module-table probe for {moduleFileName}", ex);
                return null;
            }
        }
    }
}
