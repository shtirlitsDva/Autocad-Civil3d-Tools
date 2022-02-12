using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Visualizer
{
    /// <summary>
    /// Utility functions to visualize a graph of <see cref="object"/>
    /// </summary>
    /// 
    /// <author>
    /// Jackson Dunstan, https://JacksonDunstan.com/articles/5034
    /// </author>
    /// 
    /// <license>
    /// MIT
    /// </license>
    public static class ObjectGraphVisualizer
    {
        /// <summary>
        /// A node of the graph
        /// </summary>
        private sealed class Node
        {
            /// <summary>
            /// Type of object the node represents
            /// </summary>
            public readonly string TypeName;

            /// <summary>
            /// Links from the node to other nodes. Keys are field names. Values are
            /// node IDs.
            /// </summary>
            public readonly Dictionary<string, int> Links;

            /// <summary>
            /// ID of the node. Unique to its graph.
            /// </summary>
            public readonly int Id;

            /// <summary>
            /// Create a node
            /// </summary>
            /// 
            /// <param name="typeName">
            /// Type of object the node represents
            /// </param>
            /// 
            /// <param name="id">
            /// ID of the node. Must be unique to its graph.
            /// </param>
            public Node(string typeName, int id)
            {
                TypeName = typeName;
                Links = new Dictionary<string, int>(16);
                Id = id;
            }
        }

        /// <summary>
        /// Add a node to a graph to represent an object
        /// </summary>
        /// 
        /// <returns>
        /// The added node or the existing node if one already exists for the object
        /// </returns>
        /// 
        /// <param name="nodes">
        /// Graph to add to
        /// </param>
        /// 
        /// <param name="obj">
        /// Object to add a node for
        /// </param>
        /// 
        /// <param name="tempBuilder">
        /// String builder to use only temporarily
        /// </param>
        /// 
        /// <param name="nextNodeId">
        /// ID to assign to the next node. Incremented after assignment.
        /// </param>
        private static Node AddObject(
            Dictionary<object, Node> nodes,
            object obj,
            StringBuilder tempBuilder,
            ref int nextNodeId)
        {
            // Check if there is already a node for the object
            Node node;
            if (nodes.TryGetValue(obj, out node))
            {
                return node;
            }

            // Add a node for the object
            Type objType = obj.GetType();
            node = new Node(objType.Name, nextNodeId);
            nextNodeId++;
            nodes.Add(obj, node);

            // Add linked nodes for all fields
            foreach (FieldInfo fieldInfo in EnumerateInstanceFieldInfos(objType))
            {
                // Only add reference types
                Type fieldType = fieldInfo.FieldType;
                if (!fieldType.IsPointer && !IsUnmanagedType(fieldType))
                {
                    object field = fieldInfo.GetValue(obj);
                    if (fieldType.IsArray)
                    {
                        LinkArray(
                            nodes,
                            node,
                            (Array)field,
                            fieldInfo.Name,
                            tempBuilder,
                            ref nextNodeId);
                    }
                    else
                    {
                        LinkNode(
                            nodes,
                            node,
                            field,
                            fieldInfo.Name,
                            tempBuilder,
                            ref nextNodeId);
                    }
                }
            }
            return node;
        }

        /// <summary>
        /// Add new linked nodes for the elements of an array
        /// </summary>
        /// 
        /// <param name="nodes">
        /// Graph to add to
        /// </param>
        /// 
        /// <param name="node">
        /// Node to link from
        /// </param>
        /// 
        /// <param name="array">
        /// Array whose elements should be linked
        /// </param>
        /// 
        /// <param name="arrayName">
        /// Name of the array field
        /// </param>
        /// 
        /// <param name="tempBuilder">
        /// String builder to use only temporarily
        /// </param>
        /// 
        /// <param name="nextNodeId">
        /// ID to assign to the next node. Incremented after assignment.
        /// </param>
        private static void LinkArray(
            Dictionary<object, Node> nodes,
            Node node,
            Array array,
            string arrayName,
            StringBuilder tempBuilder,
            ref int nextNodeId)
        {
            // Don't link null arrays
            if (ReferenceEquals(array, null))
            {
                return;
            }

            // Create an array of lengths of each rank
            int rank = array.Rank;
            int[] lengths = new int[rank];
            for (int i = 0; i < lengths.Length; ++i)
            {
                lengths[i] = array.GetLength(i);
            }

            // Create an array of indices into each rank
            int[] indices = new int[rank];
            indices[rank - 1] = -1;

            // Iterate over all elements of all ranks
            while (true)
            {
                // Increment the indices
                for (int i = rank - 1; i >= 0; --i)
                {
                    indices[i]++;

                    // No overflow, so we can link
                    if (indices[i] < lengths[i])
                    {
                        goto link;
                    }

                    // Overflow, so carry.
                    indices[i] = 0;
                }
                break;

            link:
                // Build the field name: "name[1, 2, 3]"
                tempBuilder.Length = 0;
                tempBuilder.Append(arrayName);
                tempBuilder.Append('[');
                for (int i = 0; i < indices.Length; ++i)
                {
                    tempBuilder.Append(indices[i]);
                    if (i != indices.Length - 1)
                    {
                        tempBuilder.Append(", ");
                    }
                }
                tempBuilder.Append(']');

                // Link the element as a node
                object element = array.GetValue(indices);
                string elementName = tempBuilder.ToString();
                LinkNode(
                    nodes,
                    node,
                    element,
                    elementName,
                    tempBuilder,
                    ref nextNodeId);
            }
        }

        /// <summary>
        /// Add a new linked node
        /// </summary>
        /// 
        /// <param name="nodes">
        /// Graph to add to
        /// </param>
        /// 
        /// <param name="node">
        /// Node to link from
        /// </param>
        /// 
        /// <param name="obj">
        /// Object to link a node for
        /// </param>
        /// 
        /// <param name="name">
        /// Name of the object
        /// </param>
        /// 
        /// <param name="tempBuilder">
        /// String builder to use only temporarily
        /// </param>
        /// 
        /// <param name="nextNodeId">
        /// ID to assign to the next node. Incremented after assignment.
        /// </param>
        private static void LinkNode(
            Dictionary<object, Node> nodes,
            Node node,
            object obj,
            string name,
            StringBuilder tempBuilder,
            ref int nextNodeId)
        {
            // Don't link null objects
            if (ReferenceEquals(obj, null))
            {
                return;
            }

            // Add a node for the object
            Node linkedNode = AddObject(nodes, obj, tempBuilder, ref nextNodeId);
            node.Links[name] = linkedNode.Id;
        }

        /// <summary>
        /// Check if a type is unmanaged, i.e. isn't and contains no managed types
        /// at any level of nesting.
        /// </summary>
        /// 
        /// <returns>
        /// Whether the given type is unmanaged or not
        /// </returns>
        /// 
        /// <param name="type">
        /// Type to check
        /// </param>
        private static bool IsUnmanagedType(Type type)
        {
            if (!type.IsValueType)
            {
                return false;
            }
            if (type.IsPrimitive || type.IsEnum)
            {
                return true;
            }
            foreach (FieldInfo field in EnumerateInstanceFieldInfos(type))
            {
                if (!IsUnmanagedType(field.FieldType))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Enumerate the instance fields of a type and all its base types
        /// </summary>
        /// 
        /// <returns>
        /// The fields of the given type and all its base types
        /// </returns>
        /// 
        /// <param name="type">
        /// Type to enumerate
        /// </param>
        private static IEnumerable<FieldInfo> EnumerateInstanceFieldInfos(Type type)
        {
            const BindingFlags bindingFlags =
                BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.Public;
            while (type != null)
            {
                foreach (FieldInfo fieldInfo in type.GetFields(bindingFlags))
                {
                    yield return fieldInfo;
                }
                type = type.BaseType;
            }
        }

        /// <summary>
        /// Visualize the given object by generating DOT which can be rendered with
        /// GraphViz.
        /// </summary>
        /// 
        /// <example>
        /// // 0) Install Graphviz if not already installed
        /// 
        /// // 1) Generate a DOT file for an object
        /// File.WriteAllText("object.dot", ObjectGraphVisualizer.Visualize(obj));
        /// 
        /// // 2) Generate a graph for the object
        /// dot -Tpng object.dot -o object.png
        /// 
        /// // 3) View the graph by opening object.png
        /// </example>
        /// 
        /// <returns>
        /// DOT, which can be rendered with GraphViz
        /// </returns>
        /// 
        /// <param name="obj">
        /// Object to visualize
        /// </param>
        public static string Visualize(object obj)
        {
            // Build the graph
            Dictionary<object, Node> nodes = new Dictionary<object, Node>(1024);
            int nextNodeId = 1;
            StringBuilder output = new StringBuilder(1024 * 64);
            AddObject(nodes, obj, output, ref nextNodeId);

            // Write the header
            output.Length = 0;
            output.Append("digraph\n");
            output.Append("{\n");

            // Write the mappings from ID to label
            foreach (Node node in nodes.Values)
            {
                output.Append("    ");
                output.Append(node.Id);
                output.Append(" [ label=\"");
                output.Append(node.TypeName);
                output.Append("\" ];\n");
            }

            // Write the node connections
            foreach (Node node in nodes.Values)
            {
                foreach (KeyValuePair<string, int> pair in node.Links)
                {
                    output.Append("    ");
                    output.Append(node.Id);
                    output.Append(" -> ");
                    output.Append(pair.Value);
                    output.Append(" [ label=\"");
                    output.Append(pair.Key);
                    output.Append("\" ];\n");
                }
            }

            // Write the footer
            output.Append("}\n");

            return output.ToString();
        }
    }
}