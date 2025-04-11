﻿using CommunityToolkit.Mvvm.ComponentModel;

using NorsynHydraulicCalc;

public partial class HydraulicSettings : ObservableObject, IHydraulicSettings
{
    // General
    [ObservableProperty]
    private MedieTypeEnum medieType = MedieTypeEnum.Water;

    [ObservableProperty]
    private int hotWaterReturnTemp = 75;

    [ObservableProperty]
    private double factorTillægForOpvarmningUdenBrugsvandsprioritering = 0.6;

    [ObservableProperty]
    private double minDifferentialPressureOverHovedHaner = 0.5;

    [ObservableProperty]
    private double ruhedSteel = 0.1;

    [ObservableProperty]
    private double ruhedPertFlextra = 0.01;

    [ObservableProperty]
    private double ruhedAluPEX = 0.01;

    [ObservableProperty]
    private double ruhedCu = 0.01;

    [ObservableProperty]
    private double ruhedPe = 0.01;

    [ObservableProperty]
    private CalcType calculationType = CalcType.CW; // "CW" or "TM"

    [ObservableProperty]
    private bool reportToConsole = false;

    // Supply Lines (FL)
    [ObservableProperty]
    private int tempFremFL = 110;

    [ObservableProperty]
    private int tempReturFL = 75;

    [ObservableProperty]
    private double factorVarmtVandsTillægFL = 1.0;

    [ObservableProperty]
    private int nyttetimerOneUserFL = 2000;

    [ObservableProperty]
    private int nyttetimer50PlusUsersFL = 2800;

    [ObservableProperty]
    private double acceptVelocity20_150FL = 1.5;

    [ObservableProperty]
    private double acceptVelocity200_300FL = 2.5;

    [ObservableProperty]
    private double acceptVelocity350PlusFL = 3.0;

    [ObservableProperty]
    private int acceptPressureGradient20_150FL = 100;

    [ObservableProperty]
    private int acceptPressureGradient200_300FL = 100;

    [ObservableProperty]
    private int acceptPressureGradient350PlusFL = 120;

    [ObservableProperty]
    private PipeType pipeTypeFL = PipeType.Stål;

    [ObservableProperty]
    private bool usePertFlextraFL = true;

    [ObservableProperty]
    private int pertFlextraMaxDnFL = 75; // Dropdown: 75, 63, 50, 40, 32, 25

    // Service Lines (SL)
    [ObservableProperty]
    private int tempFremSL = 110;

    [ObservableProperty]
    private int tempReturSL = 75;

    [ObservableProperty]
    private double factorVarmtVandsTillægSL = 1.0;

    [ObservableProperty]
    private int nyttetimerOneUserSL = 2000;

    [ObservableProperty]
    private PipeType pipeTypeSL = PipeType.AluPEX; // Dropdown: AluPEX, Kobber, Stål, PertFlextra, Pe

    [ObservableProperty]
    private double acceptVelocityFlexibleSL = 1.0;

    [ObservableProperty]
    private double acceptVelocity20_150SL = 1.5;

    [ObservableProperty]
    private int acceptPressureGradientFlexibleSL = 600;

    [ObservableProperty]
    private int acceptPressureGradient20_150SL = 600;

    [ObservableProperty]
    private double maxPressureLossStikSL = 0.3;    
}