using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

using Autodesk.AutoCAD.Geometry;

namespace IntersectUtilities.MPE.SplitStik
{
    /// <summary>
    /// Reads a DimensioneringV2 .d2r result file without referencing DimensioneringV2.
    /// The file is plain indented UTF-8 JSON written by D2rNetworkCodec.Serialize with
    /// ReferenceHandler.Preserve, so it needs three things handled by hand:
    ///
    /// 1. Arrays are wrapped as {"$id":..,"$values":[..]} — except "Graphs", which is bare.
    /// 2. "$id" scopes are NESTED, not global: the graph converter and the feature converter
    ///    each start a fresh JsonSerializer, so ids restart at "1" inside every graph and
    ///    again inside every PipeSegment. All "$ref"s are Source/Target and resolve only
    ///    against the enclosing graph's Vertices, so that is the only place ids are recorded.
    /// 3. NumberHandling.AllowNamedFloatingPointLiterals means a numeric field may legally
    ///    arrive as the JSON string "NaN" / "Infinity" / "-Infinity".
    /// </summary>
    internal static class D2rReader
    {
        internal const int SupportedFormatVersion = 6;

        internal static D2rNetwork Read(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(
                    "This is not a DimensioneringV2 result file. A .d2r holding a bare graph "
                        + "array is a pre-V1 file and is not supported.");

            // A missing or non-numeric FormatVersion means this is not a .d2r at all. Reported
            // separately from a version mismatch: telling the user to re-save a file that was never
            // a result file sends them off in the wrong direction entirely.
            if (!root.TryGetProperty("FormatVersion", out JsonElement versionElement)
                || versionElement.ValueKind != JsonValueKind.Number
                || !versionElement.TryGetInt32(out int version))
                throw new InvalidDataException(
                    "This file has no numeric \"FormatVersion\" field, so it is not a "
                        + $"DimensioneringV2 result file: \"{path}\".");

            if (version > SupportedFormatVersion)
                throw new InvalidDataException(
                    $"The file has FormatVersion {version}, but this command understands at most "
                        + $"{SupportedFormatVersion}. It was written by a newer DimensioneringV2 — "
                        + "update IntersectUtilities.");

            if (version < SupportedFormatVersion)
                throw new InvalidDataException(
                    $"The file has FormatVersion {version}, but this command requires "
                        + $"{SupportedFormatVersion}. Open it in DimensioneringV2 and save it again "
                        + "to upgrade it, then retry.");

            List<D2rGraph> graphs = new();
            if (root.TryGetProperty("Graphs", out JsonElement graphsElement))
                foreach (JsonElement graph in ArrayOf(graphsElement))
                    graphs.Add(ReadGraph(graph));

            return new D2rNetwork(
                version,
                GetString(root, "Id"),
                GetString(root, "CalculatedAt"),
                graphs);
        }

        private static D2rGraph ReadGraph(JsonElement graph)
        {
            List<D2rNode> vertices = new();
            Dictionary<string, int> idToIndex = new(StringComparer.Ordinal);

            if (graph.TryGetProperty("Vertices", out JsonElement verticesElement))
            {
                foreach (JsonElement vertex in ArrayOf(verticesElement))
                {
                    int index = vertices.Count;

                    // The ONLY place "$id" is recorded. Ids inside a PipeSegment belong to a
                    // different scope and would clobber these if they were added too.
                    if (TryGetIdString(vertex, "$id", out string? id))
                        idToIndex[id] = index;

                    Point2d location = default;
                    if (vertex.TryGetProperty("Location", out JsonElement loc)
                        && loc.ValueKind == JsonValueKind.Object)
                        location = new Point2d(ReadDouble(loc, "X"), ReadDouble(loc, "Y"));

                    vertices.Add(
                        new D2rNode(
                            index,
                            location,
                            GetBool(vertex, "IsRootNode"),
                            GetBool(vertex, "IsBuildingNode"),
                            GetInt(vertex, "Degree"),
                            GetString(vertex, "NodeId") ?? string.Empty));
                }
            }

            List<D2rEdge> edges = new();
            if (graph.TryGetProperty("Edges", out JsonElement edgesElement))
            {
                foreach (JsonElement edge in ArrayOf(edgesElement))
                {
                    if (!edge.TryGetProperty("PipeSegment", out JsonElement segment)
                        || segment.ValueKind != JsonValueKind.Object)
                        continue;

                    int index = edges.Count;
                    edges.Add(
                        new D2rEdge(
                            index,
                            ResolveVertex(edge, "Source", idToIndex, index),
                            ResolveVertex(edge, "Target", idToIndex, index),
                            ReadFeature(segment)));
                }
            }

            return new D2rGraph(vertices, edges);
        }

        /// <summary>
        /// A Source/Target is normally {"$ref":"3"} because Vertices are written first, but an
        /// inline {"$id":..} form is accepted too. A miss is fatal rather than skipped: it would
        /// mean the reference-scoping assumption above is wrong, and silently dropping edges
        /// would be far worse than stopping.
        /// </summary>
        private static int ResolveVertex(
            JsonElement edge,
            string name,
            Dictionary<string, int> idToIndex,
            int edgeIndex)
        {
            if (!edge.TryGetProperty(name, out JsonElement end)
                || end.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Edge {edgeIndex} has no {name} vertex.");

            if (!TryGetIdString(end, "$ref", out string? key)
                && !TryGetIdString(end, "$id", out key))
                throw new InvalidDataException(
                    $"Edge {edgeIndex} has a {name} vertex with neither $ref nor $id.");

            if (!idToIndex.TryGetValue(key, out int index))
                throw new InvalidDataException(
                    $"Edge {edgeIndex} {name} reference \"{key}\" does not resolve to a vertex in "
                        + "its own graph. The .d2r reference scoping has changed.");

            return index;
        }

        private static D2rFeature ReadFeature(JsonElement segment)
        {
            D2rDim? dim = null;
            int subGraphId = 0;
            double length = 0d;

            // Sparse bag: an absent key means the CLR default, not missing data. In particular
            // an absent "Dim" is the legitimate "never sized" case, not a malformed file.
            if (segment.TryGetProperty("Attributes", out JsonElement attributes)
                && attributes.ValueKind == JsonValueKind.Object)
            {
                if (attributes.TryGetProperty("Dim", out JsonElement dimElement)
                    && dimElement.ValueKind == JsonValueKind.Object)
                    dim = ReadDim(dimElement);

                subGraphId = GetInt(attributes, "SubGraphId");
                length = ReadDouble(attributes, "Length");
            }

            return new D2rFeature(
                GetString(segment, "Type") ?? string.Empty,
                ReadGeometry(segment),
                dim,
                subGraphId,
                length);
        }

        private static D2rDim ReadDim(JsonElement dim)
        {
            string family = string.Empty;
            if (dim.TryGetProperty("Family", out JsonElement familyElement)
                && familyElement.ValueKind == JsonValueKind.Object)
                family = GetString(familyElement, "Name") ?? string.Empty;

            return new D2rDim(
                family,
                GetString(dim, "Size") ?? string.Empty,
                GetString(dim, "DimName") ?? string.Empty,
                GetInt(dim, "NominalDiameter"));
        }

        private static Point2d[] ReadGeometry(JsonElement segment)
        {
            if (!segment.TryGetProperty("Geometry25832", out JsonElement geometry))
                return [];

            JsonElement array = Unwrap(geometry);
            if (array.ValueKind != JsonValueKind.Array)
                return [];

            List<Point2d> points = new(array.GetArrayLength());
            foreach (JsonElement point in array.EnumerateArray())
            {
                if (point.ValueKind != JsonValueKind.Array || point.GetArrayLength() < 2)
                    continue;
                points.Add(new Point2d(ReadDouble(point[0]), ReadDouble(point[1])));
            }

            return points.ToArray();
        }

        #region JSON helpers

        /// <summary>
        /// "Graphs" is a bare array while "Vertices" and "Edges" are $values-wrapped, so both
        /// shapes have to be tolerated at every array site.
        /// </summary>
        private static JsonElement Unwrap(JsonElement element) =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("$values", out JsonElement values)
                ? values
                : element;

        private static JsonElement.ArrayEnumerator ArrayOf(JsonElement element)
        {
            JsonElement array = Unwrap(element);
            return array.ValueKind == JsonValueKind.Array
                ? array.EnumerateArray()
                : default;
        }

        private static bool TryGetIdString(JsonElement element, string name, out string value)
        {
            if (element.TryGetProperty(name, out JsonElement id)
                && id.ValueKind == JsonValueKind.String)
            {
                string? text = id.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    value = text;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static string? GetString(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static bool GetBool(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.True;

        private static int GetInt(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int result)
                ? result
                : 0;

        private static double ReadDouble(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value) ? ReadDouble(value) : 0d;

        private static double ReadDouble(JsonElement element) =>
            element.ValueKind switch
            {
                JsonValueKind.Number => element.GetDouble(),
                // AllowNamedFloatingPointLiterals writes NaN/Infinity/-Infinity as JSON strings.
                // double.TryParse recognises those symbols for the invariant culture.
                JsonValueKind.String => double.TryParse(
                    element.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double parsed)
                    ? parsed
                    : 0d,
                _ => 0d,
            };

        #endregion
    }
}
