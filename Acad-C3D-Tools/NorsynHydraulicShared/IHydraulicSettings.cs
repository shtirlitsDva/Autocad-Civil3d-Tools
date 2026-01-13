using NorsynHydraulicCalc;

public interface IHydraulicSettings
{
    // General
    MediumTypeEnum MedieType { get; set; }
    double AfkølingBrugsvand { get; set; }
    double FactorTillægForOpvarmningUdenBrugsvandsprioritering { get; set; }
    double MinDifferentialPressureOverHovedHaner { get; set; }

    double TempFrem { get; set; }
    double AfkølingVarme { get; set; }

    double FactorVarmtVandsTillæg { get; set; }

    double RuhedSteel { get; set; }
    double RuhedPertFlextra { get; set; }
    double RuhedAluPEX { get; set; }
    double RuhedCu { get; set; }
    double RuhedPe { get; set; }
    double RuhedAquaTherm11 { get; set; }
    CalcType CalculationType { get; set; }
    bool ReportToConsole { get; set; }

    // Nyttetimer Settings (consolidated from FL/SL)
    /// <summary>
    /// System nyttetimer for 1 consumer calculation.
    /// </summary>
    int SystemnyttetimerVed1Forbruger { get; set; }

    /// <summary>
    /// System nyttetimer for 50+ consumers calculation.
    /// </summary>
    int SystemnyttetimerVed50PlusForbrugere { get; set; }

    /// <summary>
    /// Default building nyttetimer when anvendelseskode is unknown or not found.
    /// </summary>
    int BygningsnyttetimerDefault { get; set; }

    // Supply Lines (FL)
    double AcceptVelocity20_150FL { get; set; }
    double AcceptVelocity200_300FL { get; set; }
    double AcceptVelocity350PlusFL { get; set; }
    int AcceptPressureGradient20_150FL { get; set; }
    int AcceptPressureGradient200_300FL { get; set; }
    int AcceptPressureGradient350PlusFL { get; set; }
    PipeType PipeTypeFL { get; set; }
    bool UsePertFlextraFL { get; set; }
    int PertFlextraMaxDnFL { get; set; }

    // Service Lines (SL)
    PipeType PipeTypeSL { get; set; }
    double AcceptVelocityFlexibleSL { get; set; }
    double AcceptVelocity20_150SL { get; set; }
    int AcceptPressureGradientFlexibleSL { get; set; }
    int AcceptPressureGradient20_150SL { get; set; }
    double MaxPressureLossStikSL { get; set; }
}