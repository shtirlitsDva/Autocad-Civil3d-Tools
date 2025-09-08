using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Common
{
    internal sealed class BatchProgressReporter : IProgressReporter
    {
        private readonly int _every;
        private int _next;
        public BatchProgressReporter(int every) { _every = Math.Max(1, every); _next = _every; }
        public void MaybeReport(int done, int total)
        {
            if (done >= _next)
            {
                Hosting.StdIo.WriteErr($"PROGRESS {done}/{total} ({done * 100.0 / Math.Max(1, total):0.0}%)");
                _next += _every;
            }
        }
    }
}
