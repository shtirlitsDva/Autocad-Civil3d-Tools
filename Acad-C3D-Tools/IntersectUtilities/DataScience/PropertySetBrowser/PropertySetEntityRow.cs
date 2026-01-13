using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.DataScience.PropertySetBrowser
{
    /// <summary>
    /// Data Transfer Object representing an entity row in the PropertySet browser.
    /// Holds the entity handle and all property values as strings for display.
    /// </summary>
    public class PropertySetEntityRow
    {
        /// <summary>
        /// The AutoCAD entity handle (for selecting the entity later).
        /// </summary>
        public Handle EntityHandle { get; set; }

        /// <summary>
        /// The entity type name (e.g., "BlockReference", "Polyline").
        /// </summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>
        /// Dictionary of property name to property value (as string).
        /// </summary>
        public Dictionary<string, string> Properties { get; set; } = new();

        /// <summary>
        /// Gets a property value by name, or empty string if not found.
        /// </summary>
        public string GetProperty(string propertyName)
        {
            return Properties.TryGetValue(propertyName, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Checks if this row matches a search term across all properties.
        /// Uses OrdinalIgnoreCase for fast comparison without string allocations.
        /// </summary>
        public bool MatchesSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return true;

            // Check entity type - no string allocation with OrdinalIgnoreCase
            if (EntityType.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check all property values
            foreach (var kvp in Properties)
            {
                if (kvp.Value?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true)
                    return true;
            }

            return false;
        }
    }
}
