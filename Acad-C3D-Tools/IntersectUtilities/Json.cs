using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon
{
    public static class Json
    {
        // Deserialize a single T object from a file
        public static T Deserialize<T>(string filePath)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };

            using (FileStream s = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                return JsonSerializer.Deserialize<T>(s, options);
            }
        }
    }
}