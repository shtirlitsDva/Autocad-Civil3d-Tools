using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private void GatherEdges(Node<T> node, StringBuilder edges)
        {
            foreach (var child in node.Children)
            {
                edges.AppendLine($"\"{_nameSelector(node.Value)}\" -> \"{_nameSelector(child.Value)}\"");
                GatherEdges(child, edges);  // Recursive call to gather edges of children
            }
        }
        public string NodesToDot()
        {
            var nodes = new StringBuilder();
            GatherNodes(Root, nodes);
            return nodes.ToString();
        }
        private void GatherNodes(Node<T> node, StringBuilder nodes)
        {
            string color = "";
            if (node.Parent == null) color = " color = red";
            nodes.AppendLine($"\"{_nameSelector(node.Value)}\" [label={_labelSelector(node.Value)}{color}]");
            foreach (var child in node.Children)
            {
                GatherNodes(child, nodes);  // Recursive call to gather nodes of children
            }
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
