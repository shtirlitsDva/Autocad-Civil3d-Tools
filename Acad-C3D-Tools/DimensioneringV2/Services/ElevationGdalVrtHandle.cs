using OSGeo.GDAL;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Services
{
    internal sealed class ElevationGdalVrtHandle : IDisposable
    {
        public string BaseName { get; }
        public string VrtPath { get; }
        public ImmutableArray<string> Sources { get; }
        public Dataset Dataset { get; }
        public DateTime BuiltAtUtc { get; }

        public ElevationGdalVrtHandle(string baseName, string vrtPath, IEnumerable<string> sources, Dataset ds)
        {
            BaseName = baseName;
            VrtPath = vrtPath;
            Sources = sources.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToImmutableArray();
            Dataset = ds ?? throw new ArgumentNullException(nameof(ds));
            BuiltAtUtc = DateTime.UtcNow;
        }

        public void Dispose() => Dataset?.Dispose();
    }
}
