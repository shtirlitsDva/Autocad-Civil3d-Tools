using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.GraphModel
{
    internal class MetaNode<T>
    {
        private T _value;
        internal T Value => _value;
        private List<MetaNode<T>> _children = new List<MetaNode<T>>();
        internal IEnumerable<MetaNode<T>> Children => _children;
        private MetaNode<T>? _parent;
        internal MetaNode<T>? Parent => _parent;
        public MetaNode(T value)
        {
            _value = value;
        }
        internal void AddChild(MetaNode<T> child)
        {
            _children.Add(child);
            child._parent = this;
        }
    }
}
