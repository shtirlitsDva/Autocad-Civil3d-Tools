using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Common
{
    internal sealed class JsonProgressReporter : IProgressReporter
    {
        private readonly string _reqId;
        private readonly int _every;
        private int _next;

        public JsonProgressReporter(string reqId, int every)
        {
            _reqId = reqId ?? "(none)";
            _every = Math.Max(1, every);
            _next = _every;
        }

        public void MaybeReport(int done, int total)
        {
            if (done < _next) return;

            double pct = total > 0 ? (done * 100.0 / total) : 0.0;

            // Emit structured progress on stderr
            // NOTE: keep it one-line JSON
            var obj = new
            {
                id = _reqId,
                type = "PROGRESS",
                done,
                total,
                pct
            };

            // Serialize without allocations frenzy; uses your Protocol.Json.Options
            Hosting.StdIo.WriteErr(System.Text.Json.JsonSerializer.Serialize(obj, Protocol.Json.Options));

            // schedule next emit
            _next += _every;
        }
    }
}
