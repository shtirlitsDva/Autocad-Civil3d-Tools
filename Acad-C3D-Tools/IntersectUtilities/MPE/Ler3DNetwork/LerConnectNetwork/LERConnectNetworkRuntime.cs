using Autodesk.AutoCAD.ApplicationServices;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork
{
    // Process-wide holder for the single LERConnectNetwork palette and the
    // current per-document state. Unlike PipePlanRuntime this deliberately does
    // NOT subscribe to DocumentManager events: there is no IExtensionApplication
    // hook available inside the MPE scope to unsubscribe on unload, so instead
    // every operation validates against MdiActiveDocument and the command
    // re-gathers on each invocation. The palette itself is app-scoped and
    // survives drawing switches by AutoCAD's design.
    internal static class LERConnectNetworkRuntime
    {
        private static LERConnectNetworkPalette? _palette;
        private static LERConnectNetworkState? _state;

        internal static LERConnectNetworkPalette Palette => _palette ??= new LERConnectNetworkPalette();

        internal static LERConnectNetworkState StateFor(Document document)
        {
            if (_state == null || _state.Owner != document)
            {
                _state?.Dispose();
                _state = new LERConnectNetworkState(document);
            }
            return _state;
        }

        internal static void NotifyPaletteStatus(string message, LerStatusKind kind)
        {
            _palette?.SetStatus(message, kind);
        }
    }
}
