using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;

namespace DevReload
{
    public static class VsInstanceFinder
    {
        [DllImport("ole32.dll")]
        private static extern uint GetRunningObjectTable(uint reserved, out IRunningObjectTable rot);

        [DllImport("ole32.dll")]
        private static extern uint CreateBindCtx(uint reserved, out IBindCtx ctx);

        public static IDictionary<string, _DTE> GetRunningVSInstances()
        {
            IDictionary<string, object> runningObjects = GetRunningObjectTable();
            IDictionary<string, _DTE> vsInstances = new Dictionary<string, _DTE>();

            foreach (var entry in runningObjects)
            {
                if (!entry.Key.StartsWith("!VisualStudio.DTE"))
                    continue;

                _DTE? ide = entry.Value as _DTE;
                if (ide == null)
                    continue;

                vsInstances.Add(entry.Key, ide);
            }

            return vsInstances;
        }

        private static IDictionary<string, object> GetRunningObjectTable()
        {
            IDictionary<string, object> rotTable = new Dictionary<string, object>();

            IRunningObjectTable runningObjectTable;
            IEnumMoniker monikerEnumerator;
            IMoniker[] monikers = new IMoniker[1];

            GetRunningObjectTable(0, out runningObjectTable);
            runningObjectTable.EnumRunning(out monikerEnumerator);
            monikerEnumerator.Reset();

            IntPtr numberFetched = IntPtr.Zero;

            while (monikerEnumerator.Next(1, monikers, numberFetched) == 0)
            {
                IBindCtx ctx;
                CreateBindCtx(0, out ctx);

                string runningObjectName;
                monikers[0].GetDisplayName(ctx, null, out runningObjectName);
                Marshal.ReleaseComObject(ctx);

                object runningObjectValue;
                runningObjectTable.GetObject(monikers[0], out runningObjectValue);

                if (!rotTable.ContainsKey(runningObjectName))
                    rotTable.Add(runningObjectName, runningObjectValue);
            }

            return rotTable;
        }
    }
}
