using MessagePack;

using NorsynHydraulicCalc;

using System.Collections.Generic;

namespace DimensioneringV2.Serialization.Binary;

[MessagePackObject]
public class HydraulicSettingsMsgDto
{
    [Key(0)] public int Version { get; set; }
    [Key(1)] public MediumTypeEnum MedieType { get; set; }
    [Key(2)] public double AfkolingBrugsvand { get; set; }
    [Key(3)] public bool UseBrugsvandsprioritering { get; set; }
    [Key(4)] public double FactorTillaegForOpvarmningUdenBrugsvandsprioritering { get; set; }
    [Key(5)] public double TempFrem { get; set; }
    [Key(6)] public double AfkolingVarme { get; set; }
    [Key(7)] public double FactorVarmtVandsTillaeg { get; set; }
    [Key(8)] public double MinDifferentialPressureOverHovedHaner { get; set; }
    [Key(9)] public double RuhedSteel { get; set; }
    [Key(10)] public double RuhedPertFlextra { get; set; }
    [Key(11)] public double RuhedAluPEX { get; set; }
    [Key(12)] public double RuhedCu { get; set; }
    [Key(13)] public double RuhedPe { get; set; }
    [Key(14)] public double RuhedAquaTherm11 { get; set; }
    [Key(15)] public int ProcentTillaegTilTryktab { get; set; }
    [Key(16)] public double TillaegTilHoldetrykMVS { get; set; }
    [Key(17)] public int TimeToSteinerTreeEnumeration { get; set; }
    [Key(18)] public CalcType CalculationType { get; set; }
    [Key(19)] public bool ReportToConsole { get; set; }
    [Key(20)] public bool CacheResults { get; set; }
    [Key(21)] public int CachePrecision { get; set; }
    [Key(22)] public int SystemnyttetimerVed1Forbruger { get; set; }
    [Key(23)] public int SystemnyttetimerVed50PlusForbrugere { get; set; }
    [Key(24)] public int BygningsnyttetimerDefault { get; set; }
    [Key(25)] public Dictionary<MediumTypeEnum, PipeTypeConfiguration> PipeConfigsFL { get; set; } = new();
    [Key(26)] public Dictionary<MediumTypeEnum, PipeTypeConfiguration> PipeConfigsSL { get; set; } = new();
    [Key(27)] public double MaxPressureLossStikSL { get; set; }
    [Key(28)] public bool FilterEl { get; set; }
    [Key(29)] public bool FilterNaturgas { get; set; }
    [Key(30)] public bool FilterVarmepumpe { get; set; }
    [Key(31)] public bool FilterFastBraendsel { get; set; }
    [Key(32)] public bool FilterOlie { get; set; }
    [Key(33)] public bool FilterFjernvarme { get; set; }
    [Key(34)] public bool FilterAndetIngenUdgaar { get; set; }

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
