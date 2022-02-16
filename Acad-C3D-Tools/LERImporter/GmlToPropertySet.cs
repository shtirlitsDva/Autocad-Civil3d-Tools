using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Xml.Serialization;

namespace LERImporter
{
    internal class GmlToPropertySet
    {
        private StringBuilder sb = new StringBuilder();
        
        internal string TestTranslateGml(object element)
        {
            sb.AppendLine("<----- My Dump starts here ----->");

            var properties = element.GetType().GetRuntimeProperties()
                .Where(p => p.GetMethod != null && p.GetMethod.IsPublic && p.GetMethod.IsStatic == false)
                .ToList();

            foreach (var propertyInfo in properties)
            {
                bool include = propertyInfo.CustomAttributes.Any(x => x.AttributeType == typeof(Schema.PsInclude));

                if (include)
                {
                    var value = TryGetValue(propertyInfo, element);
                    sb.AppendLine($"{propertyInfo.Name}: {value}");
                }
            }
            
            return sb.ToString();
        }
        internal static object TryGetValue(PropertyInfo property, object element)
        {
            object value;
            try
            {
                value = property.GetValue(element);
            }
            catch (Exception ex)
            {
                value = $"{{{ex.Message}}}";
            }

            return value;
        }
        internal static object TryGetValue(FieldInfo field, object element)
        {
            object value;
            try
            {
                value = field.GetValue(element);
            }
            catch (Exception ex)
            {
                value = $"{{{ex.Message}}}";
            }

            return value;
        }
        private static string GetClassName(object element)
        {
            var type = element.GetType();
            var className = GetFormattedName(type, useFullName: true);
            return $"{{{className}}}";
        }
        internal static string GetFormattedName(Type type, bool useFullName = false)
        {
            var typeName = useFullName ? type.FullName : type.Name;

            var typeInfo = type.GetTypeInfo();

            TryGetInnerElementType(ref typeInfo, out var arrayBrackets);

            if (!typeInfo.IsGenericType)
            {
                return typeName;
            }

            string genericTypeParametersString;
            if (typeInfo.IsGenericTypeDefinition)
            {
                // Used for open generic types
                genericTypeParametersString = $"{string.Join(",", typeInfo.GenericTypeParameters.Select(t => string.Empty))}";
            }
            else
            {
                // Used for regular generic types
                genericTypeParametersString = $"{string.Join(", ", typeInfo.GenericTypeArguments.Select(t => GetFormattedName(t, useFullName)))}";
            }

            int iBacktick = typeName.IndexOf('`');
            if (iBacktick > 0)
            {
                typeName = typeName.Remove(iBacktick);
            }

            return $"{typeName}<{genericTypeParametersString}>{arrayBrackets}";
        }
        private static void TryGetInnerElementType(ref TypeInfo type, out string arrayBrackets)
        {
            arrayBrackets = null;
            if (!type.IsArray) return;
            do
            {
                arrayBrackets += "[" + new string(',', type.GetArrayRank() - 1) + "]";
                type = type.GetElementType().GetTypeInfo();
            }
            while (type.IsArray);
        }
        private string FormatValue(object o)
        {
            if (o == null)
            {
                return "null";
            }

            if (o is string)
            {
                return $"\"{o}\"";
            }

            if (o is char && (char)o == '\0')
            {
                return string.Empty;
            }

            if (o is ValueType)
            {
                return o.ToString();
            }

            if (o is CultureInfo)
            {
                return o.ToString();
            }

            if (o is IEnumerable)
            {
                return "...";
            }

            return "{ }";
        }
    }
}
