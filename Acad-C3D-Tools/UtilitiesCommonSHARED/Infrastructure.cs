using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon
{
    public static class Infrastructure
    {
        private static readonly Lazy<Infra> _lazy = new Lazy<Infra>(Load);

        public static string ADFS_URL => _lazy.Value.ADFS_URL;
        public static string USER_NAME_LONG => _lazy.Value.USER_NAME_LONG;
        public static string USER_NAME_SHORT => _lazy.Value.USER_NAME_SHORT;
        public static string PASSWORD => _lazy.Value.PASSWORD;
        public static string REALM => _lazy.Value.REALM;

        private static Infra Load()
        {
            string path = @"X:\AutoCAD DRI - 01 Civil 3D\NetloadV2\Infrastructure\Infra.json";

            if (!File.Exists(path))
                throw new FileNotFoundException($"Secrets file not found: {path}");

            string json = File.ReadAllText(path);
            var secrets = JsonSerializer.Deserialize<Infra>(json);

            if (secrets == null)
                throw new InvalidOperationException("Failed to parse secrets JSON.");

            return secrets;
        }

        private class Infra
        {
            public string ADFS_URL { get; set; } = string.Empty;
            public string USER_NAME_LONG { get; set; } = string.Empty;
            public string USER_NAME_SHORT { get; set; } = string.Empty;
            public string PASSWORD { get; set; } = string.Empty;
            public string REALM { get; set; } = string.Empty;
        }
    }
}
