using GDALService.Common;

using OSGeo.GDAL;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Infrastructure.Gdal
{
    internal class DatasetCache : IDisposable
    {
        internal sealed class CurrentContext
        {
            public string ProjectId { get; init; } = "";
            public string BasePath { get; init; } = "";
            public string ElevationsDir { get; init; } = "";
            public string VrtPath { get; init; } = "";
            public Dataset Dataset { get; init; } = null!;
        }

        private readonly object _lock = new();
        private CurrentContext? _current;

        public Result<CurrentContext> SetCurrent(string projectId, string basePath, VrtManager mgr)
        {
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(basePath))
                return Result<CurrentContext>.Fail(StatusCode.InvalidArgs, "projectId/basePath empty.");

            string requestedBase = Path.GetFullPath(basePath);

            lock (_lock)
            {
                if (_current != null &&
                    _current.ProjectId.Equals(projectId, StringComparison.OrdinalIgnoreCase) &&
                    _current.BasePath.Equals(requestedBase, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(_current.VrtPath))
                {
                    return Result<CurrentContext>.Success(_current);
                }

                _current?.Dataset.Dispose();
                _current = null;

                var open = mgr.EnsureOpen(projectId, requestedBase);
                if (!open.Ok)
                    return Result<CurrentContext>.Fail(open.Status, open.Error);

                _current = new CurrentContext
                {
                    ProjectId = projectId,
                    BasePath = requestedBase,
                    ElevationsDir = open.Value!.ElevationsDir,
                    VrtPath = open.Value.VrtPath,
                    Dataset = open.Value.Dataset
                };
                return Result<CurrentContext>.Success(_current);
            }
        }

        public Result<CurrentContext> GetCurrent()
        {
            lock (_lock)
            {
                if (_current == null || !File.Exists(_current.VrtPath))
                    return Result<CurrentContext>.Fail(StatusCode.NotInitialized, "No project initialized.");
                return Result<CurrentContext>.Success(_current);
            }
        }

        public bool HasCurrent()
        {
            lock (_lock) { return _current != null && File.Exists(_current.VrtPath); }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _current?.Dataset.Dispose();
            }
        }
    }
}
