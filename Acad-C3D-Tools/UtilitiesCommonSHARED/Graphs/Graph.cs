using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon.Graphs
{
    public class Graph<T> : IReadOnlyCollection<Node<T>>
    {
        public Node<T> Root { get; private set; }
        private Func<T, string> _nameSelector;
        private Func<T, string> _labelSelector;
        public Graph(Node<T> root, Func<T, string> nameSelector, Func<T, string> labelSelector)
        {
            Root = root;
            _nameSelector = nameSelector;
            _labelSelector = labelSelector;
        }
        public int Count => Dfs().Count();

        public string EdgesToDot()
        {
            var edges = new StringBuilder();
            GatherEdges(Root, edges);
            return edges.ToString();
        }
        public string EdgesToDot(Func<Node<T>, Node<T>, string?>? edgeAttrSelector)
        {
            var edges = new StringBuilder();
            GatherEdgesWithAttributes(Root, edges, edgeAttrSelector);
            return edges.ToString();
        }
        private void GatherEdges(Node<T> node, StringBuilder edges)
        {
            foreach (var child in node.Children)
            {
                edges.AppendLine($"\"{_nameSelector(node.Value)}\" -> \"{_nameSelector(child.Value)}\"");
                GatherEdges(child, edges);  // Recursive call to gather edges of children
            }
        }
        private void GatherEdgesWithAttributes(
            Node<T> node,
            StringBuilder edges,
            Func<Node<T>, Node<T>, string?>? edgeAttrSelector)
        {
            foreach (var child in node.Children)
            {
                var attr = edgeAttrSelector?.Invoke(node, child);
                if (!string.IsNullOrWhiteSpace(attr))
                    edges.AppendLine($"\"{_nameSelector(node.Value)}\" -> \"{_nameSelector(child.Value)}\" {attr}");
                else
                    edges.AppendLine($"\"{_nameSelector(node.Value)}\" -> \"{_nameSelector(child.Value)}\"");
                GatherEdgesWithAttributes(child, edges, edgeAttrSelector);
            }
        }
        public string NodesToDot(Func<T, string?>? clusterSelector = null)
        {
            var nodes = new StringBuilder();
            var allNodes = Dfs().ToList();

            if (clusterSelector is null)
            {
                foreach (var node in allNodes)
                {
                    AppendNode(nodes, node);
                }
                return nodes.ToString();
            }

            var clusterMap = new Dictionary<string, List<Node<T>>>(StringComparer.Ordinal);
            var unclustered = new List<Node<T>>();

            foreach (var node in allNodes)
            {
                var clusterKey = clusterSelector(node.Value);
                if (string.IsNullOrWhiteSpace(clusterKey))
                {
                    unclustered.Add(node);
                    continue;
                }

                if (!clusterMap.TryGetValue(clusterKey, out var list))
                {
                    list = new List<Node<T>>();
                    clusterMap.Add(clusterKey, list);
                }

                list.Add(node);
            }

            foreach (var node in unclustered)
            {
                AppendNode(nodes, node);
            }

            foreach (var cluster in clusterMap.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var clusterId = SanitizeClusterId(cluster.Key);
                nodes.AppendLine($"subgraph cluster_{clusterId} {{");
                nodes.AppendLine($"label=\"{EscapeForLabel(cluster.Key)}\";");
                foreach (var node in cluster.Value)
                {
                    AppendNode(nodes, node);
                }
                nodes.AppendLine("}");
            }

            return nodes.ToString();
        }

        public string NodesToDot(
            Func<T, string?>? clusterSelector,
            Func<T, string?>? nodeAttrSelector,
            Func<string, string?>? clusterAttrsSelector)
        {
            var nodes = new StringBuilder();
            var allNodes = Dfs().ToList();

            if (clusterSelector is null)
            {
                foreach (var node in allNodes)
                {
                    AppendNode(nodes, node, nodeAttrSelector);
                }
                return nodes.ToString();
            }

            var clusterMap = new Dictionary<string, List<Node<T>>>(StringComparer.Ordinal);
            var unclustered = new List<Node<T>>();

            foreach (var node in allNodes)
            {
                var clusterKey = clusterSelector(node.Value);
                if (string.IsNullOrWhiteSpace(clusterKey))
                {
                    unclustered.Add(node);
                    continue;
                }

                if (!clusterMap.TryGetValue(clusterKey, out var list))
                {
                    list = new List<Node<T>>();
                    clusterMap.Add(clusterKey, list);
                }

                list.Add(node);
            }

            foreach (var node in unclustered)
            {
                AppendNode(nodes, node, nodeAttrSelector);
            }

            foreach (var cluster in clusterMap.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var clusterId = SanitizeClusterId(cluster.Key);
                nodes.AppendLine($"subgraph cluster_{clusterId} {{");
                nodes.AppendLine($"label=\"{EscapeForLabel(cluster.Key)}\";");
                var extra = clusterAttrsSelector?.Invoke(cluster.Key);
                if (!string.IsNullOrWhiteSpace(extra))
                {
                    foreach (var line in extra.Split(new[] { '\\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        nodes.AppendLine(line.EndsWith(";") ? line : line + ";");
                    }
                }
                foreach (var node in cluster.Value)
                {
                    AppendNode(nodes, node, nodeAttrSelector);
                }
                nodes.AppendLine("}");
            }

            return nodes.ToString();
        }

        private void AppendNode(StringBuilder nodes, Node<T> node)
        {
            var color = node.Parent == null ? " color = red" : string.Empty;
            nodes.AppendLine($"\"{_nameSelector(node.Value)}\" [label={_labelSelector(node.Value)}{color}]");
        }
        private void AppendNode(StringBuilder nodes, Node<T> node, Func<T, string?>? nodeAttrSelector)
        {
            var color = node.Parent == null ? " color = red" : string.Empty;
            var extra = nodeAttrSelector?.Invoke(node.Value);
            if (!string.IsNullOrWhiteSpace(extra))
                nodes.AppendLine($"\"{_nameSelector(node.Value)}\" [label={_labelSelector(node.Value)}{color} {extra}]");
            else
                nodes.AppendLine($"\"{_nameSelector(node.Value)}\" [label={_labelSelector(node.Value)}{color}]");
        }

        private static string SanitizeClusterId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "ungrouped";
            var sanitized = Regex.Replace(value, @"[^A-Za-z0-9_]+", "_");
            sanitized = sanitized.Trim('_');
            if (string.IsNullOrEmpty(sanitized)) sanitized = "cluster";
            if (char.IsDigit(sanitized[0])) sanitized = "_" + sanitized;
            return sanitized;
        }

        private static string EscapeForLabel(string value)
        {
            return value.Replace("\"", "\\\"");
        }

        /// <summary>
        /// Breadth-first traversal starting at Root (or an optional start node).
        /// </summary>
        public IEnumerable<Node<T>> Bfs(Node<T>? start = null)
        {
            var root = start ?? Root;
            if (root is null) yield break;

            var q = new Queue<Node<T>>();
            var seen = new HashSet<Node<T>>();

            q.Enqueue(root);
            seen.Add(root);

            while (q.Count > 0)
            {
                var n = q.Dequeue();
                yield return n;

                foreach (var c in n.Children)
                {
                    if (c is not null && seen.Add(c))
                        q.Enqueue(c);
                }
            }
        }

        /// <summary>
        /// Depth-first (pre-order) traversal starting at Root (or an optional start node).
        /// </summary>
        public IEnumerable<Node<T>> Dfs(Node<T>? start = null)
        {
            var st = new Stack<Node<T>>();
            var seen = new HashSet<Node<T>>();
            var root = start ?? Root;
            st.Push(root); seen.Add(root);

            while (st.Count > 0)
            {
                var n = st.Pop();
                yield return n;

                for (int i = n.Children.Count() - 1; i >= 0; i--)
                {
                    var c = n.Children[i];
                    if (seen.Add(c)) st.Push(c);
                }
            }
        }

        public IEnumerator<Node<T>> GetEnumerator()
        {
            return Dfs().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
