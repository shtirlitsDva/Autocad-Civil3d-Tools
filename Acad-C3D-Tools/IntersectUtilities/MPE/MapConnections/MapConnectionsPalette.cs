using Autodesk.AutoCAD.Windows;

using IntersectUtilities.MPE.MapConnections.Views;

namespace IntersectUtilities.MPE.MapConnections;

internal sealed class MapConnectionsPalette : IDisposable
{
    private static readonly Guid PaletteGuid = new("5E0B7C2A-94D1-4B3F-9A6E-2C81F0D4A7B9");
    private static readonly System.Drawing.Size DefaultPaletteSize = new(900, 700);

    private readonly PaletteSet _paletteSet;
    private readonly MapConnectionsView _view;
    private bool _sizedOnce;

    public MapConnectionsPalette()
    {
        _view = new MapConnectionsView();
        _paletteSet = new PaletteSet("Forbindelser", "MAPCONNECTIONS", PaletteGuid)
        {
            Style = PaletteSetStyles.ShowCloseButton
                  | PaletteSetStyles.ShowAutoHideButton
                  | PaletteSetStyles.ShowTabForSingle,
            KeepFocus = false,
        };
        _paletteSet.AddVisual("Forbindelser", _view);
    }

    public void Show()
    {
        _paletteSet.Visible = true;
        if (!_sizedOnce)
        {
            _sizedOnce = true;
            IntersectUtilities.MPE.Shared.PaletteSizing.ApplyDefault(_paletteSet, DefaultPaletteSize);
        }
    }

    public void Show(LpNetworkSnapshot? snapshot, string status)
    {
        _view.Load(snapshot);
        _view.SetStatus(status);
    }

    public void SetStatus(string status) => _view.SetStatus(status);

    public void Dispose()
    {
        _paletteSet.Visible = false;
        _paletteSet.Dispose();
    }
}
