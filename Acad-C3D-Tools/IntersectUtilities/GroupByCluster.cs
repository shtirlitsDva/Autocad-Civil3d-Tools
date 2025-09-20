using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GroupByCluster
{
    public static class EnumerableExtensions
    {
        class ClusterGrouping<T> : IGrouping<T, T>
        {
            public T Key { get; private set; }
            public IEnumerator<T> GetEnumerator() 
            {
                return items.GetEnumerator();
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
            internal readonly List<T> items = new List<T>();
            internal ClusterGrouping(T t)
            {
                Key = t;
                items.Add(t);
            }

            internal ClusterGrouping(IEnumerable<ClusterGrouping<T>> containingGroups)
            {
                Key = containingGroups.First().Key;
                items.AddRange(containingGroups.SelectMany(g => g));
            }

            internal void Include(T t)
            {
                items.Add(t);
            }
        }

        public static IEnumerable<IGrouping<T, T>> GroupByCluster<T>(this IEnumerable<T> source, Func<T, T, double> distance, double eps)
        {
            var result = new HashSet<ClusterGrouping<T>>();
            foreach (T t in source)
            {
                // need to materialize, as we are changing the result
                var containingGroups = result.Where(g => g.Any(gt => distance(t, gt) < eps)).ToList();
                switch (containingGroups.Count)
                {
                    case 0:
                        result.Add(new ClusterGrouping<T>(t));
                        break;
                    case 1:
                        containingGroups[0].Include(t);
                        break;
                    default:
                        result.Add(new ClusterGrouping<T>(containingGroups));
                        foreach (var g in containingGroups)
                            result.Remove(g);
                        break;
                }
            }
            return result;
        }
    }
}