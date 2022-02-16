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
        private HashSet<string> excludeProperties = new HashSet<string>()
        {
            "geometri"
        };
        internal string TranslateGml(object element)
        {
            string currentType = "";

            if (element == null || element is ValueType || element is string)
            {
                sb.AppendLine(this.FormatValue(element));
            }
            else
            {
                var objectType = element.GetType();
                if (!typeof(IEnumerable).GetTypeInfo().IsAssignableFrom(objectType.GetTypeInfo()))
                {
                    string[] split;

                    sb.Append(GetClassName(element));
                    XmlRootAttribute xmlRoot = objectType.GetCustomAttribute<XmlRootAttribute>();
                    if (xmlRoot != null)
                    {
                        sb.AppendLine($" <- {xmlRoot.ElementName}");
                        currentType = xmlRoot.ElementName;
                    }
                    //Split the name of class by the keyword Type as it seems to be present everywhere
                    //Don't know if it will be true for everything
                    else if ((split = GetClassName(element).Split(new string[] { "Type" }, StringSplitOptions.RemoveEmptyEntries)).Length > 0)
                    {
                        sb.AppendLine($" <- {split.Last()}");
                        currentType = split.Last();
                    }
                    else
                    {
                        sb.Append("\n");
                    }
                    //objectType.CustomAttributes.ToList().ForEach(a => sb.AppendLine(a.ToString()));
                }

                var enumerableElement = element as IEnumerable;
                if (enumerableElement != null)
                {
                    foreach (var item in enumerableElement) this.TranslateGml(item);
                }
                else
                {
                    var publicFields = element.GetType().GetRuntimeFields().Where(f => !f.IsPrivate);
                    foreach (var fieldInfo in publicFields)
                    {
                        var value = TryGetValue(fieldInfo, element);

                        if (fieldInfo.FieldType.GetTypeInfo().IsValueType || fieldInfo.FieldType == typeof(string))
                        {
                            sb.AppendLine($"{fieldInfo.Name}: {this.FormatValue(value)}");
                        }
                        else
                        {
                            var isEnumerable = typeof(IEnumerable).GetTypeInfo()
                                .IsAssignableFrom(fieldInfo.FieldType.GetTypeInfo());
                            sb.AppendLine($"{fieldInfo.Name}: {(isEnumerable ? "..." : (value != null ? "{ field }" : "null"))}");

                            if (value != null) this.TranslateGml(value);
                        }
                    }

                    //Iterate properties
                    var properties = element.GetType().GetRuntimeProperties()
                        .Where(p => p.GetMethod != null && p.GetMethod.IsPublic && p.GetMethod.IsStatic == false)
                        .ToList();

                    properties = properties
                        .Where(p => !this.excludeProperties.Contains(p.Name))
                        .ToList();

                    foreach (var propertyInfo in properties)
                    {
                        var type = propertyInfo.PropertyType;
                        var value = TryGetValue(propertyInfo, element);

                        if (type.GetTypeInfo().IsValueType || type == typeof(string))
                        {
                            sb.AppendLine($"{currentType}-{propertyInfo.Name}: {this.FormatValue(value)}");
                        }
                        else
                        {
                            var isEnumerable = typeof(IEnumerable).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo());
                            sb.AppendLine($"{currentType}-{propertyInfo.Name}: {(isEnumerable ? "..." : (value != null ? "{ property }" : "null"))}");

                            if (value != null) this.TranslateGml(value);
                        }
                    }
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
