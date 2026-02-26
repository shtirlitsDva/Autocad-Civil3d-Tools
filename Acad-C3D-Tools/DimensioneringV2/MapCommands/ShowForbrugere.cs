using DimensioneringV2.GraphFeatures;
using DimensioneringV2.UI.Forbrugere;

using Mapsui;

using NorsynHydraulicCalc;

using System.Collections.Generic;
using System.Linq;

namespace DimensioneringV2.MapCommands
{
    internal class ShowForbrugere
    {
        internal void Execute(IEnumerable<IFeature> features)
        {
            var rows = features
                .Cast<AnalysisFeature>()
                .Where(f => f.SegmentType == SegmentType.Stikledning)
                .Select(f => new ForbrugerRow
                {
                    Adresse = f.Adresse,
                    Type = f.BygningsAnvendelseNyTekst,
                    BBRAreal = f["BeregningsAreal"] as int? ?? 0,
                    Effekt = f.Effekt,
                    Aarsforbrug = f.HeatingDemandConnected,
                    Stiklaengde = f.Length,
                    DN = f.Dim.DimName,
                    Tryktab = f.PressureLossBAR,
                    NødvendigtDisponibeltTryk = f.RequiredDifferentialPressure
                })
                .ToList();

            try
            {
                var window = new ForbrugereWindow(rows);
                window.Show();
            }
            catch (System.Exception ex)
            {
                Utils.prtDbg(ex);
            }
        }
    }
}
