#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace EventManager
{
    /// <summary>
    /// The unsubscribe callbacks a plugin has parked against a key, so that everything hooked
    /// against one thing is released together when that thing goes away. The manager keys it on
    /// <c>Document</c>.
    /// </summary>
    /// <typeparam name="TKey">
    /// What the callbacks are tracked against. Generic only so this type stays free of the AutoCAD
    /// assemblies and can be exercised without them.
    /// </typeparam>
    /// <remarks>Not thread safe. Expected to be used on the AutoCAD main thread.</remarks>
    internal sealed class SubscriptionLedger<TKey> where TKey : notnull
    {
        private readonly Dictionary<TKey, List<Action>> _entries = new();
        private readonly EventManagerTrace _trace;

        internal SubscriptionLedger(EventManagerTrace trace)
            => _trace = trace ?? throw new ArgumentNullException(nameof(trace));

        internal void Track(TKey key, Action unsubscribe)
        {
            if (unsubscribe == null) throw new ArgumentNullException(nameof(unsubscribe));

            if (!_entries.TryGetValue(key, out var list))
            {
                list = new List<Action>();
                _entries[key] = list;
            }
            list.Add(unsubscribe);
        }

        internal bool Has(TKey key) => _entries.ContainsKey(key);

        internal int CountFor(TKey key)
            => _entries.TryGetValue(key, out var list) ? list.Count : 0;

        internal IReadOnlyDictionary<TKey, int> Counts()
            => _entries.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

        /// <summary>
        /// Runs and forgets every callback tracked against one key. Never throws.
        /// </summary>
        /// <remarks>
        /// Every callback runs, whatever the ones before it did. They detach from something
        /// AutoCAD is already tearing down, so any one of them can throw -- and letting that abort
        /// the loop would strand the remaining hooks and, because the entry would never be
        /// removed, pin the dead key for the ledger's life. The entry is dropped in a
        /// <c>finally</c> for the same reason.
        /// <para>
        /// The callbacks run off a snapshot, so one that re-enters <see cref="Track"/> against the
        /// same key cannot invalidate the iteration. Such a registration is discarded with the
        /// rest: whatever it would be tracked against is going away.
        /// </para>
        /// </remarks>
        internal void Release(TKey key)
        {
            if (!_entries.TryGetValue(key, out var list)) return;

            var due = list.ToArray();
            try
            {
                foreach (var unsubscribe in due)
                    _trace.Guard("a tracked unsubscribe threw during cleanup", unsubscribe);
            }
            finally
            {
                _entries.Remove(key);
            }
        }

        /// <summary>Releases every key, then empties the ledger. Never throws.</summary>
        internal void ReleaseAll()
        {
            foreach (var key in _entries.Keys.ToList()) Release(key);

            // Belt and braces: a callback that re-tracked during its own release would otherwise
            // leave the key behind, and with it a reference to a thing that is going away.
            _entries.Clear();
        }
    }
}
