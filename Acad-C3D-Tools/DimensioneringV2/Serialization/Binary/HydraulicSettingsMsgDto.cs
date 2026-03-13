using MessagePack;

using NorsynHydraulicCalc;

using System.Collections.Generic;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
internal partial class HydraulicSettingsMsgDto
{
    [Key(0)] internal int Version { get; set; }
    [Key(1)] internal MediumTypeEnum MedieType { get; set; }
    [Key(2)] internal double AfkolingBrugsvand { get; set; }
    [Key(3)] internal bool UseBrugsvandsprioritering { get; set; }
    [Key(4)] internal double FactorTillaegForOpvarmningUdenBrugsvandsprioritering { get; set; }
    [Key(5)] internal double TempFrem { get; set; }
    [Key(6)] internal double AfkolingVarme { get; set; }
    [Key(7)] internal double FactorVarmtVandsTillaeg { get; set; }
    [Key(8)] internal double MinDifferentialPressureOverHovedHaner { get; set; }
    [Key(9)] internal double RuhedSteel { get; set; }
    [Key(10)] internal double RuhedPertFlextra { get; set; }
    [Key(11)] internal double RuhedAluPEX { get; set; }
    [Key(12)] internal double RuhedCu { get; set; }
    [Key(13)] internal double RuhedPe { get; set; }
    [Key(14)] internal double RuhedAquaTherm11 { get; set; }
    [Key(15)] internal int ProcentTillaegTilTryktab { get; set; }
    [Key(16)] internal double TillaegTilHoldetrykMVS { get; set; }
    [Key(17)] internal int TimeToSteinerTreeEnumeration { get; set; }
    [Key(18)] internal CalcType CalculationType { get; set; }
    [Key(19)] internal bool ReportToConsole { get; set; }
    [Key(20)] internal bool CacheResults { get; set; }
    [Key(21)] internal int CachePrecision { get; set; }
    [Key(22)] internal int SystemnyttetimerVed1Forbruger { get; set; }
    [Key(23)] internal int SystemnyttetimerVed50PlusForbrugere { get; set; }
    [Key(24)] internal int BygningsnyttetimerDefault { get; set; }
    [Key(25)] internal Dictionary<MediumTypeEnum, PipeTypeConfiguration> PipeConfigsFL { get; set; } = new();
    [Key(26)] internal Dictionary<MediumTypeEnum, PipeTypeConfiguration> PipeConfigsSL { get; set; } = new();
    [Key(27)] internal double MaxPressureLossStikSL { get; set; }
    [Key(28)] internal bool FilterEl { get; set; }
    [Key(29)] internal bool FilterNaturgas { get; set; }
    [Key(30)] internal bool FilterVarmepumpe { get; set; }
    [Key(31)] internal bool FilterFastBraendsel { get; set; }
    [Key(32)] internal bool FilterOlie { get; set; }
    [Key(33)] internal bool FilterFjernvarme { get; set; }
    [Key(34)] internal bool FilterAndetIngenUdgaar { get; set; }

    internal static HydraulicSettingsMsgDto FromDomain(HydraulicSettings settings)
    {
        return new HydraulicSettingsMsgDto
        {
            Version = settings.Version,
            MedieType = settings.MedieType,
            AfkolingBrugsvand = settings.Afk\u00f8lingBrugsvand,
            UseBrugsvandsprioritering = settings.UseBrugsvandsprioritering,
            FactorTillaegForOpvarmningUdenBrugsvandsprioritering = settings.FactorTill\u00e6gForOpvarmningUdenBrugsvandsprioritering,
            TempFrem = settings.TempFrem,
            AfkolingVarme = settings.Afk\u00f8lingVarme,
            FactorVarmtVandsTillaeg = settings.FactorVarmtVandsTill\u00e6g,
            MinDifferentialPressureOverHovedHaner = settings.MinDifferentialPressureOverHovedHaner,
            RuhedSteel = settings.RuhedSteel,
            RuhedPertFlextra = settings.RuhedPertFlextra,
            RuhedAluPEX = settings.RuhedAluPEX,
            RuhedCu = settings.RuhedCu,
            RuhedPe = settings.RuhedPe,
            RuhedAquaTherm11 = settings.RuhedAquaTherm11,
            ProcentTillaegTilTryktab = settings.ProcentTill\u00e6gTilTryktab,
            TillaegTilHoldetrykMVS = settings.Till\u00e6gTilHoldetrykMVS,
            TimeToSteinerTreeEnumeration = settings.TimeToSteinerTreeEnumeration,
            CalculationType = settings.CalculationType,
            ReportToConsole = settings.ReportToConsole,
            CacheResults = settings.CacheResults,
            CachePrecision = settings.CachePrecision,
            SystemnyttetimerVed1Forbruger = settings.SystemnyttetimerVed1Forbruger,
            SystemnyttetimerVed50PlusForbrugere = settings.SystemnyttetimerVed50PlusForbrugere,
            BygningsnyttetimerDefault = settings.BygningsnyttetimerDefault,
            PipeConfigsFL = settings.AllPipeConfigsFL,
            PipeConfigsSL = settings.AllPipeConfigsSL,
            MaxPressureLossStikSL = settings.MaxPressureLossStikSL,
            FilterEl = settings.FilterEl,
            FilterNaturgas = settings.FilterNaturgas,
            FilterVarmepumpe = settings.FilterVarmepumpe,
            FilterFastBraendsel = settings.FilterFastBr\u00e6ndsel,
            FilterOlie = settings.FilterOlie,
            FilterFjernvarme = settings.FilterFjernvarme,
            FilterAndetIngenUdgaar = settings.FilterAndetIngenUdg\u00e5r,
        };
    }

    internal HydraulicSettings ToDomain()
    {
        var settings = new HydraulicSettings();
        settings.Version = Version;
        settings.MedieType = MedieType;
        settings.Afk\u00f8lingBrugsvand = AfkolingBrugsvand;
        settings.UseBrugsvandsprioritering = UseBrugsvandsprioritering;
        settings.FactorTill\u00e6gForOpvarmningUdenBrugsvandsprioritering = FactorTillaegForOpvarmningUdenBrugsvandsprioritering;
        settings.TempFrem = TempFrem;
        settings.Afk\u00f8lingVarme = AfkolingVarme;
        settings.FactorVarmtVandsTill\u00e6g = FactorVarmtVandsTillaeg;
        settings.MinDifferentialPressureOverHovedHaner = MinDifferentialPressureOverHovedHaner;
        settings.RuhedSteel = RuhedSteel;
        settings.RuhedPertFlextra = RuhedPertFlextra;
        settings.RuhedAluPEX = RuhedAluPEX;
        settings.RuhedCu = RuhedCu;
        settings.RuhedPe = RuhedPe;
        settings.RuhedAquaTherm11 = RuhedAquaTherm11;
        settings.ProcentTill\u00e6gTilTryktab = ProcentTillaegTilTryktab;
        settings.Till\u00e6gTilHoldetrykMVS = TillaegTilHoldetrykMVS;
        settings.TimeToSteinerTreeEnumeration = TimeToSteinerTreeEnumeration;
        settings.CalculationType = CalculationType;
        settings.ReportToConsole = ReportToConsole;
        settings.CacheResults = CacheResults;
        settings.CachePrecision = CachePrecision;
        settings.SystemnyttetimerVed1Forbruger = SystemnyttetimerVed1Forbruger;
        settings.SystemnyttetimerVed50PlusForbrugere = SystemnyttetimerVed50PlusForbrugere;
        settings.BygningsnyttetimerDefault = BygningsnyttetimerDefault;
        settings.AllPipeConfigsFL = PipeConfigsFL;
        settings.AllPipeConfigsSL = PipeConfigsSL;
        settings.MaxPressureLossStikSL = MaxPressureLossStikSL;
        settings.FilterEl = FilterEl;
        settings.FilterNaturgas = FilterNaturgas;
        settings.FilterVarmepumpe = FilterVarmepumpe;
        settings.FilterFastBr\u00e6ndsel = FilterFastBraendsel;
        settings.FilterOlie = FilterOlie;
        settings.FilterFjernvarme = FilterFjernvarme;
        settings.FilterAndetIngenUdg\u00e5r = FilterAndetIngenUdgaar;
        settings.EnsureInitialized();
        return settings;
    }
}
