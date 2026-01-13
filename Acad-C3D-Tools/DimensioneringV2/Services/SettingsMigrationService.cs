using System;
using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.Services
{
    /// <summary>
    /// Service that executes a chain of migrations to bring settings up to the current version.
    /// </summary>
    /// <typeparam name="T">The settings type being migrated.</typeparam>
    public class SettingsMigrationService<T> where T : IVersionedSettings, new()
    {
        private readonly List<ISettingsMigration<T>> _migrations;
        private readonly int _currentVersion;

        /// <summary>
        /// Creates a new migration service with the specified migrations and target version.
        /// </summary>
        /// <param name="migrations">List of available migrations.</param>
        /// <param name="currentVersion">The current (target) version of the settings schema.</param>
        public SettingsMigrationService(IEnumerable<ISettingsMigration<T>> migrations, int currentVersion)
        {
            _migrations = migrations.ToList();
            _currentVersion = currentVersion;
        }

        /// <summary>
        /// Migrates settings from their current version to the current schema version.
        /// </summary>
        /// <param name="settings">The settings to migrate.</param>
        /// <returns>The migrated settings at the current version.</returns>
        public T MigrateToCurrentVersion(T settings)
        {
            while (settings.Version < _currentVersion)
            {
                var migration = _migrations.FirstOrDefault(m => m.FromVersion == settings.Version);
                
                if (migration == null)
                {
                    throw new InvalidOperationException(
                        $"No migration found from version {settings.Version}. " +
                        $"Available migrations: {string.Join(", ", _migrations.Select(m => $"{m.FromVersion}->{m.ToVersion}"))}");
                }

                Utils.prtDbg($"Migrating settings from v{migration.FromVersion} to v{migration.ToVersion}");
                settings = migration.Migrate(settings);
            }

            return settings;
        }

        /// <summary>
        /// Checks if migration is needed for the given settings.
        /// </summary>
        public bool NeedsMigration(T settings)
        {
            return settings.Version < _currentVersion;
        }

        /// <summary>
        /// Checks if the settings version is newer than the current schema version.
        /// This might happen if settings were saved with a newer version of the software.
        /// </summary>
        public bool IsNewerThanCurrent(T settings)
        {
            return settings.Version > _currentVersion;
        }
    }
}
