using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.MatchBBR
{
    // What the Excel tab remembers about one workbook.
    internal sealed class MatchBbrSettings
    {
        public string WorkbookPath { get; set; } = string.Empty;

        public string? SheetName { get; set; }

        public List<CompareRule> Rules { get; set; } = new List<CompareRule>();

        public string? DistrictColumn { get; set; }

        // Column letter -> the values left ticked in that column's header filter. Only narrowed
        // columns appear; a column absent from the dictionary is unfiltered.
        public Dictionary<string, List<string>> ColumnFilters { get; set; } =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    }

    // Persists the rule set and district filter per workbook, so returning to a familiar file
    // does not mean re-declaring the same mapping every time.
    //
    // Keyed by a hash of the full workbook path rather than by the file name, so two workbooks
    // called "Forbrug.xlsx" in different project folders keep separate settings.
    internal static class MatchBbrRuleStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private static string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IntersectUtilities",
                "MatchBBR");

        public static MatchBbrSettings? TryLoad(string workbookPath)
        {
            try
            {
                string file = PathFor(workbookPath);
                if (!File.Exists(file))
                {
                    return null;
                }

                MatchBbrSettings? settings =
                    JsonSerializer.Deserialize<MatchBbrSettings>(File.ReadAllText(file), SerializerOptions);

                // A rule set with no usable key rule cannot join anything; treat it as absent
                // rather than restoring a configuration that silently produces zero rows. This
                // also discards settings written by an older layout whose columns no longer
                // deserialize, so a stale file falls back to fresh auto-detection.
                if (settings is null || !settings.Rules.Any(r => r.IsKey && r.ExcelColumns.Count > 0))
                {
                    return null;
                }

                return settings;
            }
            catch (System.Exception ex)
            {
                // Settings are a convenience. A corrupt or unreadable file must never stop the
                // tool from opening — the caller falls back to auto-detected defaults. Logged so
                // "my rules keep resetting" is diagnosable instead of invisible.
                prdDbg($"MatchBBR: kunne ikke læse gemte regler for '{workbookPath}'.");
                prdDbg(ex);
                return null;
            }
        }

        public static void TrySave(MatchBbrSettings settings)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settings.WorkbookPath))
                {
                    return;
                }

                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(
                    PathFor(settings.WorkbookPath),
                    JsonSerializer.Serialize(settings, SerializerOptions),
                    Encoding.UTF8);
            }
            catch (System.Exception ex)
            {
                // Same reasoning as above: never let a settings write break the session, but say
                // so, or silently unsaved rules look like the tool forgetting them.
                prdDbg($"MatchBBR: kunne ikke gemme regler for '{settings.WorkbookPath}'.");
                prdDbg(ex);
            }
        }

        private static string PathFor(string workbookPath)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(workbookPath.ToUpperInvariant()));
            string name = string.Concat(hash.Take(10).Select(b => b.ToString("x2")));
            return Path.Combine(SettingsDirectory, $"{name}.json");
        }
    }
}
