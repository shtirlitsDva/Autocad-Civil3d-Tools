using Autodesk.AutoCAD.ApplicationServices;

using static IntersectUtilities.UtilsCommon.Utils;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.MPE.MapConnections;

/// <summary>
/// Process-wide holder for the MAPCONNECTIONS palette and the snapshot it shows. The snapshot belongs to the
/// document it was read from; clicks are refused once another drawing is active, until "Opdater" rereads.
/// </summary>
internal static class MapConnectionsRuntime
{
    private static MapConnectionsPalette? _palette;
    private static Document? _owner;
    private static LpNetworkSnapshot? _snapshot;

    internal static MapConnectionsPalette Palette => _palette ??= new MapConnectionsPalette();

    /// <summary>Rereads the active drawing. Safe from the palette (takes the document lock itself).</summary>
    internal static void Refresh(Document? document = null)
    {
        document ??= AcadApp.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            Palette.Show(null, "Ingen aktiv tegning.");
            return;
        }

        try
        {
            LpNetworkSnapshot snapshot;
            using (document.LockDocument())
            {
                snapshot = LpConnectionResolver.Build(document.Database);
            }

            _owner = document;
            _snapshot = snapshot;
            int views = snapshot.Lines.Values.Count(l => l.View is not null);
            int links = snapshot.Connections.Count(c => c.Kind != LpConnectionKind.Na);
            Palette.Show(snapshot, $"{views} længdeprofiler, {links} forbindelser.");
        }
        catch (System.Exception ex)
        {
            prdDbg(ex);
            Palette.Show(null, $"Kunne ikke læse tegningen: {ex.Message}");
        }
    }

    internal static void ZoomToLp(string lp) => WithView(lp, (document, view) =>
    {
        LpViewJumper.ZoomToView(document.Editor, view);
        return $"Zoomet til {lp}.";
    });

    internal static void JumpTo(string lp, double station) => WithView(lp, (document, view) =>
    {
        LpViewJumper.JumpTo(document.Editor, view, station);
        return $"{lp} st. {station:0.00}";
    });

    private static void WithView(string lp, Func<Document, LpProfileView, string> action)
    {
        Document? document = AcadApp.DocumentManager.MdiActiveDocument;
        if (_snapshot is null || _owner is null || document is null || !ReferenceEquals(document, _owner))
        {
            Palette.SetStatus("Aktiv tegning er skiftet — tryk Opdater.");
            return;
        }

        if (!_snapshot.Lines.TryGetValue(lp, out LpLine? line) || line.View is null)
        {
            Palette.SetStatus($"Længdeprofil {lp} findes ikke i tegningen.");
            return;
        }

        try
        {
            string status;
            using (document.LockDocument())
            {
                status = action(document, line.View);
            }

            Palette.SetStatus(status);
        }
        catch (System.Exception ex)
        {
            prdDbg(ex);
            Palette.SetStatus($"Fejl: {ex.Message}");
        }
    }

    // Called from IExtensionApplication.Terminate so the palette doesn't survive an unload/reload cycle.
    internal static void Reset()
    {
        _snapshot = null;
        _owner = null;
        _palette?.Dispose();
        _palette = null;
    }
}
