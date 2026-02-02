using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections;
using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon.Collections
{
    public sealed class DatabaseList : IReadOnlyList<Database>, IDisposable
    {
        private readonly List<Database> _databases;
        private bool _disposed;

        public DatabaseList(IEnumerable<Database> databases)
        {
            _databases = [.. databases];
        }

        public Database this[int index] => _databases[index];
        public int Count => _databases.Count;

        public IEnumerator<Database> GetEnumerator() => _databases.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            foreach (var db in _databases)
            {
                try { db?.Dispose(); }
                catch { }
            }
            _databases.Clear();
        }
    }
}
