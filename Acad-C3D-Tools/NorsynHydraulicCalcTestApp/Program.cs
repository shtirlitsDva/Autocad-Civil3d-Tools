// See https://aka.ms/new-console-template for more information
using NorsynHydraulicCalc;

HydraulicCalc hc = new HydraulicCalc(
    "Fordelingsledning",
    50, //Total heating demand
    1, //Number of clients
    1, //Number of units
    50, //Hot water return temp
    0.6, //FactorTillægForOpvarmningUdenBrugsvandsprioritering
    0.5, //MinDifferentialPressureOverHovedHaner

    110, //TempFremFL
    60, //TempReturFL
    1, //FactorVarmtVandsTillægFL
    2000, //NyttetimerOneUserFL
    2800, //Nyttetimer50PlusUsersFL
    1.5, //AcceptVelocity20_150FL
    2.5, //AcceptVelocity200_300FL
    100, //AcceptPressureGradient20_150FL
    100, //AcceptPressureGradient200_300FL
    true, //UsePertFlextraFL
    75, //PertFlextraMaxDnFL

    110, //TempFremSL
    60, //TempReturSL
    1, //FactorVarmtVandsTillægSL
    2000, //NyttetimerOneUserSL
    "AluPEX", //PipeTypeSL
    1, //AcceptVelocityFlexibleSL
    1.5, //AcceptVelocity20_150SL
    600, //AcceptPressureGradientFlexibleSL
    600, //AcceptPressureGradient20_150SL
    0.3 //MaxPressureLossStikSL
    );

hc.Calculate();