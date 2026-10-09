namespace NSLOAD
{
    /// <summary>
    /// What NSLOAD needs to know about the CAD it was built for: its names in
    /// messages, and the register and config it reads. The BricsCAD build has its
    /// own copy of this file; the two must declare the same members.
    /// </summary>
    internal static class HostInfo
    {
        /// <summary>The CAD as named in a refusal ("AutoCAD refused to load ...").</summary>
        public const string AppName = "AutoCAD";

        /// <summary>The program a drafter restarts to get a clean process.</summary>
        public const string RestartName = "Civil";

        /// <summary>What a native module is built against, for a refused load.</summary>
        public const string NativeSdk = "ObjectARX/AutoCAD";

        /// <summary>Appended when the dynamic linker still holds a module it
        /// said it unloaded. AutoCAD unloads under live objects, so it has
        /// nothing to add.</summary>
        public const string StillLoadedHint = "";

        /// <summary>The shared register of company plugins built for this host.</summary>
        public const string RegisterCsvPath =
            @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Register-2025.csv";

        /// <summary>The per-user config under %APPDATA%\NSLOAD. One file per host:
        /// a plugin binary is built against one host's API, so the plugin paths a
        /// drafter stores are this host's only.</summary>
        public const string ConfigFileName = "config.json";
    }
}
