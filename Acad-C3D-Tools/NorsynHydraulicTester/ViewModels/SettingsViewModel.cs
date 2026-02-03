using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;
using NorsynHydraulicShared;

namespace NorsynHydraulicTester.ViewModels;

public partial class SettingsViewModel : ObservableObject, IHydraulicSettings
{
    private readonly PipeTypes _pipeTypes;

    [ObservableProperty] private MediumTypeEnum medieType = MediumTypeEnum.Water;
    [ObservableProperty] private double afkølingBrugsvand = 35;
    [ObservableProperty] private double factorTillægForOpvarmningUdenBrugsvandsprioritering = 0.6;
    [ObservableProperty] private double minDifferentialPressureOverHovedHaner = 0.5;
    [ObservableProperty] private double tempFrem = 80;
    [ObservableProperty] private double afkølingVarme = 35;
    [ObservableProperty] private double factorVarmtVandsTillæg = 1.0;

    [ObservableProperty] private double ruhedSteel = 0.0001;
    [ObservableProperty] private double ruhedPertFlextra = 0.00001;
    [ObservableProperty] private double ruhedAluPEX = 0.00001;
    [ObservableProperty] private double ruhedCu = 0.00015;
    [ObservableProperty] private double ruhedPe = 0.00001;
    [ObservableProperty] private double ruhedAquaTherm11 = 0.00001;

    [ObservableProperty] private CalcType calculationType = CalcType.CW;
    [ObservableProperty] private bool reportToConsole = true;

    public bool IsColebrookWhite
    {
        get => CalculationType == CalcType.CW;
        set { if (value) CalculationType = CalcType.CW; }
    }

    public bool IsTkachenkoMileikovskyi
    {
        get => CalculationType == CalcType.TM;
        set { if (value) CalculationType = CalcType.TM; }
    }

    partial void OnCalculationTypeChanged(CalcType value)
    {
        OnPropertyChanged(nameof(IsColebrookWhite));
        OnPropertyChanged(nameof(IsTkachenkoMileikovskyi));
    }

    [ObservableProperty] private int systemnyttetimerVed1Forbruger = 2000;
    [ObservableProperty] private int systemnyttetimerVed50PlusForbrugere = 2800;
    [ObservableProperty] private int bygningsnyttetimerDefault = 2000;

    [ObservableProperty] private double maxPressureLossStikSL = 0.3;

    public ObservableCollection<MediumTypeEnum> AvailableMediumTypes { get; } = new(Enum.GetValues<MediumTypeEnum>());

    public ObservableCollection<PipeType> AvailablePipeTypesFL { get; } = new();
    public ObservableCollection<PipeType> AvailablePipeTypesSL { get; } = new();

    [ObservableProperty] private PipeType selectedPipeTypeFL;
    [ObservableProperty] private PipeType selectedPipeTypeSL;

    private PipeTypeConfiguration _pipeConfigFL;
    private PipeTypeConfiguration _pipeConfigSL;

    public PipeTypeConfiguration PipeConfigFL
    {
        get => _pipeConfigFL;
        set => SetProperty(ref _pipeConfigFL, value);
    }

    public PipeTypeConfiguration PipeConfigSL
    {
        get => _pipeConfigSL;
        set => SetProperty(ref _pipeConfigSL, value);
    }

    public SettingsViewModel()
    {
        _pipeTypes = new PipeTypes(this);

        RefreshAvailablePipeTypes();

        _pipeConfigFL = CreateSinglePipeConfig(SegmentType.Fordelingsledning, SelectedPipeTypeFL);
        _pipeConfigSL = CreateSinglePipeConfig(SegmentType.Stikledning, SelectedPipeTypeSL);
    }

    partial void OnMedieTypeChanged(MediumTypeEnum value)
    {
        RefreshAvailablePipeTypes();
        RebuildPipeConfigurations();
    }

    partial void OnSelectedPipeTypeFLChanged(PipeType value)
    {
        PipeConfigFL = CreateSinglePipeConfig(SegmentType.Fordelingsledning, value);
    }

    partial void OnSelectedPipeTypeSLChanged(PipeType value)
    {
        PipeConfigSL = CreateSinglePipeConfig(SegmentType.Stikledning, value);
    }

    private void RefreshAvailablePipeTypes()
    {
        var flTypes = MediumPipeTypeRules.GetValidPipeTypesForSupply(MedieType).ToList();
        var slTypes = MediumPipeTypeRules.GetValidPipeTypesForService(MedieType).ToList();

        AvailablePipeTypesFL.Clear();
        foreach (var pt in flTypes)
            AvailablePipeTypesFL.Add(pt);

        AvailablePipeTypesSL.Clear();
        foreach (var pt in slTypes)
            AvailablePipeTypesSL.Add(pt);

        if (flTypes.Count > 0 && !flTypes.Contains(SelectedPipeTypeFL))
            SelectedPipeTypeFL = flTypes[0];

        if (slTypes.Count > 0 && !slTypes.Contains(SelectedPipeTypeSL))
            SelectedPipeTypeSL = slTypes[0];
    }

    private void RebuildPipeConfigurations()
    {
        PipeConfigFL = CreateSinglePipeConfig(SegmentType.Fordelingsledning, SelectedPipeTypeFL);
        PipeConfigSL = CreateSinglePipeConfig(SegmentType.Stikledning, SelectedPipeTypeSL);
    }

    private PipeTypeConfiguration CreateSinglePipeConfig(SegmentType segmentType, PipeType pipeType)
    {
        var config = new PipeTypeConfiguration(segmentType);

        int minDn = DefaultPipeConfigFactory.GetMinDn(pipeType, _pipeTypes);
        int maxDn = DefaultPipeConfigFactory.GetMaxDn(pipeType, _pipeTypes);

        var priority = DefaultPipeConfigFactory.CreatePipeTypePriorityWithDefaults(
            1,
            pipeType,
            minDn,
            maxDn,
            segmentType,
            _pipeTypes);

        config.Priorities.Add(priority);
        return config;
    }
}
