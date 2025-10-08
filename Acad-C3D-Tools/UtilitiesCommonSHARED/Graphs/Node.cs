using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.UtilsCommon.Graphs
{
    public class Node<T>
    {
        private readonly List<Node<T>> _children = new();
        public IReadOnlyList<Node<T>> Children => _children;

        public Node<T>? Parent { get; private set; }
        public T Value { get; }
        public Node(T value)
        {
            Value = value;
        }

        public void AddChild(Node<T> child)
        {
            if (child is null) throw new ArgumentNullException(nameof(child));
            if (ReferenceEquals(child, this)) throw new InvalidOperationException("A node cannot be its own child.");
            if (child.Parent != null) child.Parent._children.Remove(child);
            child.Parent = this;
            _children.Add(child);
        }

        public bool RemoveChild(Node<T> child)
        {
            if (child is null) return false;
            if (_children.Remove(child))
            {
                child.Parent = null;
                return true;
            }
            return false;
        }
    }
}
