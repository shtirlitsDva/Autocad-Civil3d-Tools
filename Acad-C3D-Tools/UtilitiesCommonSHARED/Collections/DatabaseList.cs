using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon.Collections
{
    public class DatabaseList : List<Database>, IDisposable
    {
        private bool disposed = false;

        public DatabaseList() : base() { }
        public DatabaseList(int capacity) : base(capacity) { }
        public DatabaseList(IEnumerable<Database> collection) : base(collection) { }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            foreach (var db in this)
            {
                try { db?.Dispose(); }
                catch { }
            }
            Clear();
        }
    }
}
