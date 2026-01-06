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
    CalcType CalculationType { get; set; }
    bool ReportToConsole { get; set; }

    // Supply Lines (FL)
    int NyttetimerOneUserFL { get; set; }
    int Nyttetimer50PlusUsersFL { get; set; }
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
    int NyttetimerOneUserSL { get; set; }
    PipeType PipeTypeSL { get; set; }
    double AcceptVelocityFlexibleSL { get; set; }
    double AcceptVelocity20_150SL { get; set; }
    int AcceptPressureGradientFlexibleSL { get; set; }
    int AcceptPressureGradient20_150SL { get; set; }
    double MaxPressureLossStikSL { get; set; }
}