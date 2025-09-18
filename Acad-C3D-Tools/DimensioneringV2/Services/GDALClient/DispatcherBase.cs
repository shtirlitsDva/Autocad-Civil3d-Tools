using DimensioneringV2.Common;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DimensioneringV2.Services.GDALClient
{
    internal class DispatcherBase
    {
        protected IRpcTransport Rpc => GdalRpcTransport.Instance;
        /// <summary>Ensures GDAL service is ready and a project is set based on current DWG + ElevationSettings.</summary>
        protected async Task<OpResult<bool>> EnsureReadyAndProjectAsync(CancellationToken ct)            
        {
            // Load settings (project id)
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var settings = SettingsSerializer<ElevationSettings>.Load(doc);
            if (string.IsNullOrWhiteSpace(settings?.BaseFileName))
                return OpResult<bool>.Fail(
                    "Ingen terræn-projekt-id for denne tegning. Hent først terrændata.");

            string projectId = settings!.BaseFileName!;
            // Resolve base path from drawing filename
            var dbFile = doc.Database.Filename;
            if (string.IsNullOrWhiteSpace(dbFile))
                return OpResult<bool>.Fail("Kan ikke finde DWG-filen på disk.");

            string basePath = Path.GetDirectoryName(dbFile)!;

            try
            {
                await Rpc.EnsureServerAsync(ct).ConfigureAwait(false);

                // HELLO
                var hello = await Rpc.CallAsync("HELLO", new { }, ct);
                if (hello.status != 0)
                    return OpResult<bool>.Fail("GDALService HELLO failed: " + (hello.error ?? "(unknown)"));
                
                var set = await Rpc.CallAsync("SET_PROJECT", new { projectId, basePath }, ct);
                if (set.status != 0)
                    return OpResult<bool>.Fail("SET_PROJECT failed: " + (set.error ?? "(unknown)"));

                return OpResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return OpResult<bool>.Fail(
                    $"GDALService kunne ikke initialiseres: {ex.Message}");
            }
        }
    }
}
