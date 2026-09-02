namespace AcadOverrules.VertexCircles
{
    /// <summary>
    /// The single point where the overrule and the settings window meet.
    /// The overrule reads <see cref="Current"/> on every draw, the window pushes edits in
    /// through <see cref="SetConfig"/> for a live preview and only touches the disk on
    /// <see cref="Save"/>, so typing in the window does not hammer the file system.
    /// </summary>
    internal sealed class VertexCirclesSettingsService
    {
        private static readonly VertexCirclesSettingsService _instance =
            new VertexCirclesSettingsService();

        public static VertexCirclesSettingsService Instance => _instance;

        private VertexCirclesConfig _config;

        private VertexCirclesSettingsService()
        {
            _config = VertexCirclesSettingsStore.Load();
        }

        public VertexCirclesConfig Config => _config;

        /// <summary>The settings of the active profile - what the overrule draws with.</summary>
        public VertexCirclesSettings Current => _config.ActiveProfile.Settings;

        /// <summary>Replaces the in-memory config without writing to disk.</summary>
        public void SetConfig(VertexCirclesConfig config)
        {
            if (config == null) return;
            _config = config;
        }

        /// <summary>Persists the current in-memory config.</summary>
        public void Save() => VertexCirclesSettingsStore.Save(_config);
    }
}
