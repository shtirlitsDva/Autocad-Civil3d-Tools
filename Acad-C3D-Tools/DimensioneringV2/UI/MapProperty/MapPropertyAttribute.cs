using System;

namespace DimensioneringV2.UI.MapProperty
{
    [AttributeUsage(AttributeTargets.Property)]
    class MapPropertyAttribute : Attribute
    {
        public MapPropertyEnum Property { get; }

        /// <summary>
        /// Text shown in the theme dropdown.
        /// Replaces [Description] on MapPropertyEnum.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Whether this property uses gradient or category theming.
        /// None means it participates in the attribute system but has no theme.
        /// </summary>
        public ThemeKind Theme { get; set; } = ThemeKind.None;

        /// <summary>
        /// Title shown in the legend panel. Use \n for line breaks.
        /// </summary>
        public string LegendTitle { get; set; } = "";

        /// <summary>
        /// For category themes: values that receive plain "basic" style.
        /// Stored as strings; converted to the property type at runtime.
        /// Examples: "false", "", "0", "NA 000"
        /// </summary>
        public string[] BasicStyleValues { get; set; } = [];

        /// <summary>
        /// A dot-separated sub-property path applied after GetDisplayValue().
        /// If empty, GetDisplayValue() result is used directly.
        /// Example: "DimName" resolves f.Dim.DimName for the Pipe property.
        /// </summary>
        public string DisplayValuePath { get; set; } = "";

        /// <summary>
        /// For category themes with DisplayValuePath: a property path on the
        /// parent object to use for ordering.
        /// Example: "OrderingPriority" for Dim.OrderingPriority.
        /// </summary>
        public string OrderingPropertyPath { get; set; } = "";

        /// <summary>
        /// Label formatting strategy.
        /// Default = type-based (F2 for double, as-is for int/string).
        /// Custom = escape hatch for genuinely custom logic.
        /// </summary>
        public LabelFormat LabelFormat { get; set; } = LabelFormat.Default;

        /// <summary>
        /// Legend label formatting strategy for category themes.
        /// </summary>
        public LegendLabelFormat LegendLabel { get; set; } = LegendLabelFormat.Default;

        /// <summary>
        /// Template string for legend labels.
        /// For BoolTrueFalse: "TrueLabel|FalseLabel"
        /// For Template: "Sub-graph {0}"
        /// </summary>
        public string LegendLabelTemplate { get; set; } = "";

        /// <summary>
        /// Fallback text for empty/null/zero legend values.
        /// Used with ShowOrFallback format.
        /// </summary>
        public string LegendLabelFallback { get; set; } = "";

        public MapPropertyAttribute(MapPropertyEnum property)
        {
            Property = property;
        }
    }
}
