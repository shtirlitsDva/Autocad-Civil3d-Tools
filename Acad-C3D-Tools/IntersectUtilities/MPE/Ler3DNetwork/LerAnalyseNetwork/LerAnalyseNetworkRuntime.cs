using Autodesk.AutoCAD.ApplicationServices;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork
{
    // Process-wide holder for the single LerAnalyseNetwork palette and the current
    // per-document state. Like LerConnectNetworkRuntime it deliberately does NOT
    // subscribe to DocumentManager events: every operation validates against
    // MdiActiveDocument and the command re-binds on each invocation.
    internal static class LerAnalyseNetworkRuntime
    {
        private static LerAnalyseNetworkPalette? _palette;
        private static LerAnalyseNetworkState? _state;

        internal static LerAnalyseNetworkPalette Palette => _palette ??= new LerAnalyseNetworkPalette();

        internal static LerAnalyseNetworkState StateFor(Document document)
        {
            if (_state == null || _state.Owner != document)
            {
                _state?.Dispose();
                _state = new LerAnalyseNetworkState(document);
            }
            return _state;
        }

        internal static void NotifyPaletteStatus(string message, LerStatusKind kind)
        {
            _palette?.SetStatus(message, kind);
        }
    }
}
