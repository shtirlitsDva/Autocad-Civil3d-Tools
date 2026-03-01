using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.ApplicationServices;

namespace EventManager
{
    public class AcadEventManager : IDisposable
    {
        private readonly Dictionary<Document, List<Action>> _subscriptions = new();
        private bool _disposed;

        public AcadEventManager()
        {
            Application.DocumentManager.DocumentToBeDestroyed += OnDocToBeDestroyed;
        }

        public void Track(Document doc, Action unsubscribe)
        {
            if (!_subscriptions.TryGetValue(doc, out var list))
            {
                list = new List<Action>();
                _subscriptions[doc] = list;
            }
            list.Add(unsubscribe);
        }

        public IReadOnlyDictionary<Document, int> GetSubscriptions()
            => _subscriptions.ToDictionary(kv => kv.Key, kv => kv.Value.Count);

        public bool HasSubscriptions(Document doc)
            => _subscriptions.ContainsKey(doc);

        public int GetSubscriptionCount(Document doc)
            => _subscriptions.TryGetValue(doc, out var list) ? list.Count : 0;

        private void OnDocToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            CleanupDocument(e.Document);
        }

        private void CleanupDocument(Document doc)
        {
            if (!_subscriptions.TryGetValue(doc, out var list)) return;
            foreach (var unsub in list) unsub();
            _subscriptions.Remove(doc);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var doc in _subscriptions.Keys.ToList())
                CleanupDocument(doc);
            Application.DocumentManager.DocumentToBeDestroyed -= OnDocToBeDestroyed;
        }
    }
}
