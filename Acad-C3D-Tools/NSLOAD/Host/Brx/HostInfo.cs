namespace NSLOAD
{
    /// <summary>
    /// What NSLOAD needs to know about the CAD it was built for: its names in
    /// messages, and the register and config it reads. The AutoCAD build has its
    /// own copy of this file; the two must declare the same members.
    /// </summary>
    internal static class HostInfo
    {
        /// <summary>The CAD as named in a refusal ("BricsCAD refused to load ...").</summary>
        public const string AppName = "BricsCAD";

        /// <summary>The program a drafter restarts to get a clean process.</summary>
        public const string RestartName = "BricsCAD";

        /// <summary>What a native module is built against, for a refused load.</summary>
        public const string NativeSdk = "BRX/BricsCAD";

        /// <summary>Appended when the dynamic linker still holds a module it
        /// said it unloaded: BricsCAD keeps a module while an open drawing holds
        /// its objects (see <see cref="Native.DrawingCycle"/>), and the drawings NSLOAD
        /// does not close are an unnamed one or one opened since.</summary>
        public const string StillLoadedHint =
            " BricsCAD does not unload a module while an open drawing holds its objects. " +
            "Close every drawing that holds them, then unload again.";

        /// <summary>The shared register of company plugins built for this host.</summary>
        public const string RegisterCsvPath =
            @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Register-BricsCAD-26.csv";

        /// <summary>The per-user config under %APPDATA%\NSLOAD. One file per host:
        /// a plugin binary is built against one host's API, so the plugin paths a
        /// drafter stores are this host's only.</summary>
        public const string ConfigFileName = "config-BricsCAD-26.json";
    }
}
