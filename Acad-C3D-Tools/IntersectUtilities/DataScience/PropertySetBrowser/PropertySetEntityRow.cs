using Autodesk.AutoCAD.DatabaseServices;

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
        /// </summary>
        public bool MatchesSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return true;

            var term = searchTerm.ToLowerInvariant();

            // Check entity type
            if (EntityType.ToLowerInvariant().Contains(term))
                return true;

            // Check all property values
            foreach (var kvp in Properties)
            {
                if (kvp.Value?.ToLowerInvariant().Contains(term) == true)
                    return true;
            }

            return false;
        }
    }
}
