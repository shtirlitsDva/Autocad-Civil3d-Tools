using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphUtilities
{
    internal static class TypeTreeSerializer
    {
        /// <summary>
        /// Generates an HTML report of the type tree and its serialization status.
        /// </summary>
        /// <param name="rootObject">The root object to analyze.</param>
        /// <param name="filePath">The output file path for the HTML report.</param>
        public static void GenerateTypeTreeReport(object rootObject, string filePath)
        {
            if (rootObject == null) throw new ArgumentNullException(nameof(rootObject));

            Type rootType = rootObject.GetType();
            StringBuilder html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='en'><head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("<title>Type Tree & Serialization Report</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine(".serializable { color: green; }");
            html.AppendLine(".non-serializable { color: red; font-weight: bold; }");
            html.AppendLine("ul { list-style-type: none; }");
            html.AppendLine("</style></head><body>");
            html.AppendLine("<h1>Type Tree & Serialization Report</h1>");
            html.AppendLine("<ul>");

            // Recursively inspect the type tree
            InspectType(rootType, new HashSet<Type>(), html);

            html.AppendLine("</ul>");
            html.AppendLine("</body></html>");

            // Write HTML to file
            File.WriteAllText(filePath, html.ToString());
        }

        /// <summary>
        /// Recursively inspects the type tree and appends formatted HTML.
        /// </summary>
        private static void InspectType(Type type, HashSet<Type> visited, StringBuilder html)
        {
            if (type == null || visited.Contains(type)) return; // Prevent infinite recursion

            visited.Add(type);
            bool isSerializable = CheckSerialization(type);
            string cssClass = isSerializable ? "serializable" : "non-serializable";
            string typeName = type.IsGenericType ? type.GetGenericTypeDefinition().Name + 
                "&lt;" + string.Join(", ", type.GetGenericArguments().ToList()) + "&gt;" : type.Name;

            html.AppendLine($"<li><span class='{cssClass}'>{typeName} (Serializable: {isSerializable})</span><ul>");

            if (type.IsGenericType)
            {
                foreach (Type genericArg in type.GetGenericArguments())
                {
                    InspectType(genericArg, visited, html);
                }
            }

            if (!type.IsPrimitive && !type.IsEnum && type != typeof(string))
            {
                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    InspectType(prop.PropertyType, visited, html);
                }
            }

            html.AppendLine("</ul></li>");
        }

        /// <summary>
        /// Checks if a type is serializable by attempting to serialize a default instance.
        /// </summary>
        private static bool CheckSerialization(Type type)
        {
            try
            {
                object? instance = GetDefaultInstance(type);
                JsonSerializer.Serialize(instance);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Attempts to create a default instance of a given type.
        /// </summary>
        private static object? GetDefaultInstance(Type type)
        {
            if (type.IsValueType) return Activator.CreateInstance(type);
            if (type == typeof(string)) return string.Empty;
            if (typeof(IEnumerable).IsAssignableFrom(type)) return Activator.CreateInstance(typeof(List<>).MakeGenericType(type.GenericTypeArguments));

            ConstructorInfo? ctor = type.GetConstructor(Type.EmptyTypes);
            return ctor != null ? Activator.CreateInstance(type) : null;
        }
    }

}