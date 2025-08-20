using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset
{
    internal class VejkantOffsetSettings(string grundkort, string fjernvarmeDim)
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public string Grundkort { get; set; } = grundkort;
        public string FjernvarmeDim { get; set; } = fjernvarmeDim;
        public double MaxAngleDeg { get; set; } = 7.5;
        public double Width { get; set; } = 2.0;
        public PipeSeriesEnum Series { get; set; } = PipeSeriesEnum.S3;
        public double OffsetSupplement { get; set; } = 0.0;
        [JsonIgnore]
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
