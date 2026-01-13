using NorsynHydraulicCalc;

public interface IHydraulicSettings
{
    #region General Settings
    /// <summary>
    /// The medium type (Water, Water72Ipa28, etc.)
    /// </summary>
    MediumTypeEnum MedieType { get; set; }

    /// <summary>
    /// Cooling for hot water in degrees.
    /// </summary>
    double AfkølingBrugsvand { get; set; }

    /// <summary>
    /// Factor for heating without hot water priority.
    /// </summary>
    double FactorTillægForOpvarmningUdenBrugsvandsprioritering { get; set; }

    /// <summary>
    /// Minimum differential pressure over main valves at consumer in bar.
    /// </summary>
    double MinDifferentialPressureOverHovedHaner { get; set; }

    /// <summary>
    /// Supply temperature in degrees.
    /// </summary>
    double TempFrem { get; set; }

    /// <summary>
    /// Cooling for heating in degrees.
    /// </summary>
    double AfkølingVarme { get; set; }

    /// <summary>
    /// Factor for hot water supplement.
    /// </summary>
    double FactorVarmtVandsTillæg { get; set; }
    #endregion

    #region Roughness Settings
    double RuhedSteel { get; set; }
    double RuhedPertFlextra { get; set; }
    double RuhedAluPEX { get; set; }
    double RuhedCu { get; set; }
    double RuhedPe { get; set; }
    double RuhedAquaTherm11 { get; set; }
    #endregion

    #region Calculation Settings
    CalcType CalculationType { get; set; }
    bool ReportToConsole { get; set; }
    #endregion

    #region Nyttetimer Settings
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
    #endregion

    #region Pipe Type Configuration
    /// <summary>
    /// Pipe type configuration for supply lines (Fordelingsledninger).
    /// Contains prioritized list of pipe types with per-DN accept criteria.
    /// </summary>
    PipeTypeConfiguration PipeConfigFL { get; }

    /// <summary>
    /// Pipe type configuration for service lines (Stikledninger).
    /// Contains prioritized list of pipe types with per-DN accept criteria.
    /// </summary>
    PipeTypeConfiguration PipeConfigSL { get; }

    /// <summary>
    /// Maximum allowed pressure loss in service lines (bar).
    /// </summary>
    double MaxPressureLossStikSL { get; set; }
    #endregion
}
