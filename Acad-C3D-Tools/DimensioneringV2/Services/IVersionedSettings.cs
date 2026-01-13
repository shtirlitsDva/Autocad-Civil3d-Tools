namespace DimensioneringV2.Services
{
    /// <summary>
    /// Interface for settings classes that support versioning and migration.
    /// </summary>
    public interface IVersionedSettings
    {
        /// <summary>
        /// The version number of the settings schema.
        /// </summary>
        int Version { get; set; }
    }

    /// <summary>
    /// Interface for a single migration step between versions.
    /// </summary>
    /// <typeparam name="T">The settings type being migrated.</typeparam>
    public interface ISettingsMigration<T> where T : IVersionedSettings
    {
        /// <summary>
        /// The version this migration migrates FROM.
        /// </summary>
        int FromVersion { get; }

        /// <summary>
        /// The version this migration migrates TO.
        /// </summary>
        int ToVersion { get; }

        /// <summary>
        /// Performs the migration on the settings object.
        /// </summary>
        /// <param name="settings">The settings to migrate.</param>
        /// <returns>The migrated settings.</returns>
        T Migrate(T settings);
    }
}
