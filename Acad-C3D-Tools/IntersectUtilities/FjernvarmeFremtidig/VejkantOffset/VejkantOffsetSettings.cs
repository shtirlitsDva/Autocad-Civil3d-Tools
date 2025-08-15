using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset
{
    internal class VejkantOffsetSettings(string grundkort, string fjernvarmeDim)
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public string Grundkort { get; set; } = grundkort;
        public string FjernvarmeDim { get; set; } = fjernvarmeDim;
        public double MaxAngleDeg { get; set; } = 7.5;
        public double Width { get; set; } = 2.0;
        public double Supplement { get; set; } = 0.05;
        public double OffsetSupplement { get; set; } = 0.0;

        public bool IsValid => File.Exists(Grundkort) && File.Exists(FjernvarmeDim);

        public static void SerializeToFile(VejkantOffsetSettings settings, string filePath)
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        public static VejkantOffsetSettings DeserializeFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new VejkantOffsetSettings(string.Empty, string.Empty);
            
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<VejkantOffsetSettings>(json, JsonOptions) ?? new VejkantOffsetSettings(string.Empty, string.Empty);
        }

        public void SerializeToFile(string filePath)
        {
            SerializeToFile(this, filePath);
        }
    }
}
