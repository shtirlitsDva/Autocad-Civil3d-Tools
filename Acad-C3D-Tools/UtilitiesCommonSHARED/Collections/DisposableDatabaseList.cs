using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon.Collections
{
    public class DisposableDatabaseList : List<Database>, IDisposable
    {
        private bool disposed = false;

        public DisposableDatabaseList() : base() { }
        public DisposableDatabaseList(int capacity) : base(capacity) { }
        public DisposableDatabaseList(IEnumerable<Database> collection) : base(collection) { }

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
