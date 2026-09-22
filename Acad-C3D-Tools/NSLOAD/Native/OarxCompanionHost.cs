using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using Exception = System.Exception;

namespace NSLOAD.Native
{
    /// <summary>
    /// The companions a native group carries besides its modules: pinned native
    /// DLLs and managed assemblies loaded through AutoCAD's extension loader.
    /// Everything here is load-only — companions are never unloaded, which is
    /// precisely why they are separate from the module lifecycle in
    /// <see cref="OarxModuleHost"/>.
    /// </summary>
    /// <remarks>
    /// COPIED from DevReload's <c>DevReload.Oarx.OarxCompanionHost</c> (DevReload
    /// 932eba6); the progress HUD is replaced by a plain line writer. Since then
    /// this copy compares paths by file identity and warns about a managed
    /// companion already loaded from another file; DevReload's copy still owes
    /// both fixes. The two copies are maintained by hand: a fix to one belongs
    /// in the other too.
    ///
    /// Two lessons are load-bearing:
    ///
    /// <list type="number">
    /// <item><b>The pin.</b> Windows maps two DLLs sharing a base name when they
    /// come from different folders — a native import resolves the copy adjacent
    /// to its module while a .NET [DllImport] resolves another — and a DLL
    /// holding process-wide state (a log hub) then exists twice. Mapping the
    /// canonical copy by FULL PATH before any module loads makes every later
    /// base-name reference bind to it. If a NON-canonical copy is already
    /// resident the split cannot be undone without a restart, so it is warned
    /// about loudly rather than silently tolerated.</item>
    /// <item><b>Order.</b> Preloads run BEFORE the group's modules (a trace UI
    /// must be listening before a dbx logs during load).</item>
    /// </list>
    /// </remarks>
    internal static class OarxCompanionHost
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryW(string lpLibFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetModuleFileNameW(
            IntPtr hModule, System.Text.StringBuilder lpFilename, uint nSize);

        /// <summary>
        /// Map one native DLL by full path so later base-name references bind to
        /// it. Idempotent; warns loudly (and returns) when a same-named module is
        /// already mapped from a DIFFERENT path — that split is unfixable without
        /// a restart and must not pass silently. Never throws: a missing pin is
        /// reported and the load continues.
        /// </summary>
        public static void PinNative(string fullPath, Action<string> say)
        {
            string baseName = Path.GetFileName(fullPath);
            IntPtr existing = GetModuleHandleW(baseName);
            if (existing != IntPtr.Zero)
            {
                var mapped = new System.Text.StringBuilder(1024);
                if (GetModuleFileNameW(existing, mapped, 1024) != 0 &&
                    !FileIdentity.Same(mapped.ToString(), fullPath))
                {
                    say($"WARNING: {baseName} is already mapped from a NON-canonical path: " +
                        $"{mapped} (canonical: {fullPath}). Process-wide state in it is " +
                        "split; restart AutoCAD to bind everything to one copy.");
                }
                return;
            }

            if (!File.Exists(fullPath))
            {
                say($"WARNING: pinned native module not found: {fullPath}");
                return;
            }
            if (LoadLibraryW(fullPath) == IntPtr.Zero)
                say($"WARNING: could not pin {fullPath} (GetLastError={Marshal.GetLastWin32Error()})");
        }

        /// <summary>
        /// Load one managed assembly through AutoCAD's extension loader — the
        /// NETLOAD-equivalent path, so IExtensionApplication.Initialize runs and
        /// [CommandMethod]s register. Default ALC, never unloaded, so this is
        /// idempotent by assembly simple name. When the assembly already came
        /// from ANOTHER file (the other release folder, earlier this session),
        /// that copy stays in use and the drafter is told. Never throws: the
        /// failure is reported and the load continues.
        /// </summary>
        public static void LoadManaged(string fullPath, Action<string> say)
        {
            string simpleName = Path.GetFileNameWithoutExtension(fullPath);
            var existing = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
                string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                // Location is empty for an assembly loaded from a stream; there is
                // then no file to compare, and nothing to say.
                if (!string.IsNullOrEmpty(existing.Location) && !FileIdentity.Same(existing.Location, fullPath))
                    say($"WARNING: {simpleName} is already loaded from {existing.Location}, so that " +
                        $"copy stays in use instead of {fullPath}. Restart AutoCAD to use this one.");
                return;
            }

            if (!File.Exists(fullPath))
            {
                say($"WARNING: managed companion not found: {fullPath}");
                return;
            }

            try
            {
                Autodesk.AutoCAD.Runtime.ExtensionLoader.Load(fullPath);
            }
            catch (Exception ex)
            {
                say($"WARNING: companion {Path.GetFileName(fullPath)} failed to load: {ex.Message}");
            }
        }
    }
}
