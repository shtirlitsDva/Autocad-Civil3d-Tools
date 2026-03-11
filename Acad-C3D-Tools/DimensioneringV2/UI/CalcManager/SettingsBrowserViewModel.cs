using System.Collections.ObjectModel;
using System.Globalization;

using NorsynHydraulicCalc;

namespace DimensioneringV2.UI.CalcManager;

internal class SettingsBrowserViewModel
{
    private static readonly CultureInfo Da = CultureInfo.GetCultureInfo("da-DK");

    public ObservableCollection<SettingDisplayItem> GeneralSettings { get; }
    public ObservableCollection<SettingDisplayItem> RoughnessSettings { get; }
    public ObservableCollection<SettingDisplayItem> CalculationSettings { get; }
    public ObservableCollection<SettingDisplayItem> PipeSettings { get; }

    public SettingsBrowserViewModel(HydraulicSettings settings)
    {
        GeneralSettings = BuildGeneralSettings(settings);
        RoughnessSettings = BuildRoughnessSettings(settings);
        CalculationSettings = BuildCalculationSettings(settings);
        PipeSettings = BuildPipeSettings(settings);
    }

    private static string Fmt(double v) => v.ToString("G", Da);
    private static string Fmt(int v) => v.ToString("N0", Da);
    private static string JaNej(bool v) => v ? "Ja" : "Nej";

    private static ObservableCollection<SettingDisplayItem> BuildGeneralSettings(HydraulicSettings s)
    {
        return new ObservableCollection<SettingDisplayItem>
        {
            new("Medietype", s.MedieType.ToString()),
            new("Afk\u00f8ling brugsvand [\u00b0C]", Fmt(s.Afk\u00f8lingBrugsvand)),
            new("Brugsvandsprioritering", JaNej(s.UseBrugsvandsprioritering)),
            new("Faktor till\u00e6g opvarmning", Fmt(s.FactorTill\u00e6gForOpvarmningUdenBrugsvandsprioritering)),
            new("Freml\u00f8bstemperatur [\u00b0C]", Fmt(s.TempFrem)),
            new("Afk\u00f8ling varme [\u00b0C]", Fmt(s.Afk\u00f8lingVarme)),
            new("Faktor varmt vands till\u00e6g", Fmt(s.FactorVarmtVandsTill\u00e6g)),
            new("Min. diff. tryk over hovedhaner [bar]", Fmt(s.MinDifferentialPressureOverHovedHaner)),
        };
    }

    private static ObservableCollection<SettingDisplayItem> BuildRoughnessSettings(HydraulicSettings s)
    {
        return new ObservableCollection<SettingDisplayItem>
        {
            new("St\u00e5l [mm]", Fmt(s.RuhedSteel)),
            new("PertFlextra [mm]", Fmt(s.RuhedPertFlextra)),
            new("AluPEX [mm]", Fmt(s.RuhedAluPEX)),
            new("Kobber [mm]", Fmt(s.RuhedCu)),
            new("PE [mm]", Fmt(s.RuhedPe)),
            new("AquaTherm11 [mm]", Fmt(s.RuhedAquaTherm11)),
        };
    }

    private static ObservableCollection<SettingDisplayItem> BuildCalculationSettings(HydraulicSettings s)
    {
        return new ObservableCollection<SettingDisplayItem>
        {
            new("Procenttill\u00e6g til tryktab [%]", Fmt(s.ProcentTill\u00e6gTilTryktab)),
            new("Till\u00e6g til holdetryk MVS [m]", Fmt(s.Till\u00e6gTilHoldetrykMVS)),
            new("Tid til Steiner-tree [s]", Fmt(s.TimeToSteinerTreeEnumeration)),
            new("Beregningstype", s.CalculationType.ToString()),
            new("Rapport\u00e9r til konsol", JaNej(s.ReportToConsole)),
            new("Cache resultater", JaNej(s.CacheResults)),
            new("Cache pr\u00e6cision", Fmt(s.CachePrecision)),
            new("Systemnyttetimer (1 forbruger)", Fmt(s.SystemnyttetimerVed1Forbruger)),
            new("Systemnyttetimer (50+ forbrugere)", Fmt(s.SystemnyttetimerVed50PlusForbrugere)),
            new("Bygningsnyttetimer standard", Fmt(s.BygningsnyttetimerDefault)),
            new("Max tryktab stik [bar]", Fmt(s.MaxPressureLossStikSL)),
        };
    }

    private static ObservableCollection<SettingDisplayItem> BuildPipeSettings(HydraulicSettings s)
    {
        var items = new ObservableCollection<SettingDisplayItem>();

        foreach (var kvp in s.AllPipeConfigsFL)
        {
            items.Add(new SettingDisplayItem($"FL {kvp.Key}", ""));
            foreach (var p in kvp.Value.Priorities)
                items.Add(new SettingDisplayItem(
                    $"  Pri {p.Priority}",
                    $"{p.PipeType} DN{p.MinDn}-{p.MaxDn}"));
        }

        foreach (var kvp in s.AllPipeConfigsSL)
        {
            items.Add(new SettingDisplayItem($"SL {kvp.Key}", ""));
            foreach (var p in kvp.Value.Priorities)
                items.Add(new SettingDisplayItem(
                    $"  Pri {p.Priority}",
                    $"{p.PipeType} DN{p.MinDn}-{p.MaxDn}"));
        }

        return items;
    }
}
