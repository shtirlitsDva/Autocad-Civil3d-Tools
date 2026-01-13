using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;
using System.Linq;

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
        /// Pre-computed concatenated string of all searchable values.
        /// Built once at load time for O(1) search instead of O(n) dictionary iteration.
        /// </summary>
        public string SearchableText { get; private set; } = string.Empty;

        /// <summary>
        /// Gets a property value by name, or empty string if not found.
        /// </summary>
        public string GetProperty(string propertyName)
        {
            return Properties.TryGetValue(propertyName, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Builds the searchable text index from EntityType and all property values.
        /// Call this after populating Properties.
        /// </summary>
        public void BuildSearchIndex()
        {
            // Concatenate EntityType and all property values with space separator
            var parts = new List<string> { EntityType };
            parts.AddRange(Properties.Values.Where(v => !string.IsNullOrEmpty(v)));
            SearchableText = string.Join(" ", parts);
        }

        /// <summary>
        /// Checks if this row matches a search term.
        /// Uses pre-computed SearchableText for O(1) lookup instead of iterating properties.
        /// </summary>
        public bool MatchesSearch(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return true;

            // Single Contains() on pre-computed string - no dictionary iteration
            return SearchableText.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
        }
    }
}
