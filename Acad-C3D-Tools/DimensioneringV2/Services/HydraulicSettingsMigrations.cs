using NorsynHydraulicCalc;

namespace DimensioneringV2.Services
{
    /// <summary>
    /// Migration from v1 (legacy format without version property) to v2 (PipeTypePriority format).
    /// </summary>
    public class MigrationV1ToV2 : ISettingsMigration<HydraulicSettings>
    {
        public int FromVersion => 1;
        public int ToVersion => 2;

        public HydraulicSettings Migrate(HydraulicSettings settings)
        {
            // For legacy files, generate default PipeTypeConfiguration based on medium type
            var pipeTypes = settings.GetPipeTypes();
            settings.PipeConfigFL = DefaultPipeConfigFactory.CreateDefaultFL(settings.MedieType, pipeTypes);
            settings.PipeConfigSL = DefaultPipeConfigFactory.CreateDefaultSL(settings.MedieType, pipeTypes);
            settings.Version = 2;

            Utils.prtDbg($"Migrated settings from v1 to v2. Generated default pipe configurations for medium: {settings.MedieType}");
            
            return settings;
        }
    }

    /// <summary>
    /// Factory for creating the HydraulicSettings migration service.
    /// </summary>
    public static class HydraulicSettingsMigrationFactory
    {
        /// <summary>
        /// Current version of the HydraulicSettings schema.
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>
        /// Creates a migration service configured with all available migrations.
        /// </summary>
        public static SettingsMigrationService<HydraulicSettings> Create()
        {
            var migrations = new ISettingsMigration<HydraulicSettings>[]
            {
                new MigrationV1ToV2()
            };

            return new SettingsMigrationService<HydraulicSettings>(migrations, CurrentVersion);
        }
    }
}
