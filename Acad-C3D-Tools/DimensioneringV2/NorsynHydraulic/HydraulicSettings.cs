using CommunityToolkit.Mvvm.ComponentModel;

using DimensioneringV2.Services;

using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Linq;

public partial class HydraulicSettings : ObservableObject, IVersionedSettings, IHydraulicSettings
{
    #region PipeTypes Access
    private PipeTypes? _pipeTypes;
    
    /// <summary>
    /// Gets a PipeTypes instance for accessing pipe information.
    /// Created lazily using this settings instance.
    /// </summary>
    public PipeTypes GetPipeTypes()
    {
        return _pipeTypes ??= new PipeTypes(this);
    }
    #endregion

    #region Versioning
    /// <summary>
    /// Current version of the HydraulicSettings schema.
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Version of this settings instance. Used for migration.
    /// Legacy files without this property are treated as version 1.
    /// </summary>
    [ObservableProperty]
    private int version = CurrentVersion;
    #endregion

    #region General Settings
    [ObservableProperty]
    private MediumTypeEnum medieType = MediumTypeEnum.Water;

    [ObservableProperty]
    private double afkølingBrugsvand = 35;

    [ObservableProperty]
    private bool useBrugsvandsprioritering = false;

    [ObservableProperty]
    private double factorTillægForOpvarmningUdenBrugsvandsprioritering = 0.6;

    [ObservableProperty]
    private double tempFrem = 110;

    [ObservableProperty]
    private double afkølingVarme = 35;

    [ObservableProperty]
    private double factorVarmtVandsTillæg = 1.0;

    private double? previousFactorValue = null;

    partial void OnUseBrugsvandsprioriteringChanged(bool value)
    {
        if (value)
        {
            // Store the current value before setting to 0.0
            if (FactorTillægForOpvarmningUdenBrugsvandsprioritering != 0.0)
            {
                previousFactorValue = FactorTillægForOpvarmningUdenBrugsvandsprioritering;
            }
            FactorTillægForOpvarmningUdenBrugsvandsprioritering = 0.0;
        }
        else
        {
            // Restore the previous value, or use default 0.6 if none was stored
            FactorTillægForOpvarmningUdenBrugsvandsprioritering = previousFactorValue ?? 0.6;
        }
    }

    [ObservableProperty]
    private double minDifferentialPressureOverHovedHaner = 0.5;
    #endregion

    #region Roughness Settings
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
    private double ruhedAquaTherm11 = 0.01;
    #endregion

    #region Calculation Settings
    [ObservableProperty]
    private int procentTillægTilTryktab = 0;

    [ObservableProperty]
    private double tillægTilHoldetrykMVS = 13;

    [ObservableProperty]
    private int timeToSteinerTreeEnumeration = 20; // in seconds

    [ObservableProperty]
    private CalcType calculationType = CalcType.CW; // "CW" or "TM"

    [ObservableProperty]
    private bool reportToConsole = false;

    [ObservableProperty]
    private bool cacheResults = false;

    [ObservableProperty]
    private int cachePrecision = 4;
    #endregion

    #region Nyttetimer Settings
    /// <summary>
    /// System nyttetimer for 1 consumer calculation.
    /// </summary>
    [ObservableProperty]
    private int systemnyttetimerVed1Forbruger = 2000;

    /// <summary>
    /// System nyttetimer for 50+ consumers calculation.
    /// </summary>
    [ObservableProperty]
    private int systemnyttetimerVed50PlusForbrugere = 2800;

    /// <summary>
    /// Default building nyttetimer when anvendelseskode is unknown or not found.
    /// </summary>
    [ObservableProperty]
    private int bygningsnyttetimerDefault = 2000;
    #endregion

    #region Pipe Type Configuration - Per Medium Storage
    /// <summary>
    /// Stores FL configurations per medium type.
    /// This allows switching mediums without losing custom configurations.
    /// </summary>
    private Dictionary<MediumTypeEnum, PipeTypeConfiguration> pipeConfigsFL = new();

    /// <summary>
    /// Stores SL configurations per medium type.
    /// This allows switching mediums without losing custom configurations.
    /// </summary>
    private Dictionary<MediumTypeEnum, PipeTypeConfiguration> pipeConfigsSL = new();

    /// <summary>
    /// Pipe type configuration for Fordelingsledninger (FL) for the current medium.
    /// </summary>
    public PipeTypeConfiguration PipeConfigFL
    {
        get
        {
            if (!pipeConfigsFL.TryGetValue(MedieType, out var config))
            {
                config = DefaultPipeConfigFactory.CreateDefaultFL(MedieType, GetPipeTypes());
                pipeConfigsFL[MedieType] = config;
            }
            return config;
        }
        set
        {
            pipeConfigsFL[MedieType] = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Pipe type configuration for Stikledninger (SL) for the current medium.
    /// </summary>
    public PipeTypeConfiguration PipeConfigSL
    {
        get
        {
            if (!pipeConfigsSL.TryGetValue(MedieType, out var config))
            {
                config = DefaultPipeConfigFactory.CreateDefaultSL(MedieType, GetPipeTypes());
                pipeConfigsSL[MedieType] = config;
            }
            return config;
        }
        set
        {
            pipeConfigsSL[MedieType] = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// All FL configurations keyed by medium (for serialization).
    /// </summary>
    public Dictionary<MediumTypeEnum, PipeTypeConfiguration> AllPipeConfigsFL
    {
        get => pipeConfigsFL;
        set => pipeConfigsFL = value ?? new();
    }

    /// <summary>
    /// All SL configurations keyed by medium (for serialization).
    /// </summary>
    public Dictionary<MediumTypeEnum, PipeTypeConfiguration> AllPipeConfigsSL
    {
        get => pipeConfigsSL;
        set => pipeConfigsSL = value ?? new();
    }

    /// <summary>
    /// Maximum allowed pressure loss in service lines (bar).
    /// </summary>
    [ObservableProperty]
    private double maxPressureLossStikSL = 0.3;
    #endregion

    #region Block Type Filter Settings
    /// <summary>
    /// Filter toggles for block types. When true, that type is INCLUDED in calculations.
    /// Stored as individual bools to minimize DWG storage (7 bools vs string collection).
    /// </summary>
    [ObservableProperty]
    private bool filterEl = true;

    [ObservableProperty]
    private bool filterNaturgas = true;

    [ObservableProperty]
    private bool filterVarmepumpe = true;

    [ObservableProperty]
    private bool filterFastBrændsel = true;

    [ObservableProperty]
    private bool filterOlie = true;

    [ObservableProperty]
    private bool filterFjernvarme = true;

    /// <summary>
    /// Combined filter for "Andet", "Ingen", and "UDGÅR" types.
    /// </summary>
    [ObservableProperty]
    private bool filterAndetIngenUdgår = false;

    /// <summary>
    /// Gets the set of accepted block types based on filter bool settings.
    /// Generated on-the-fly to avoid storing string collections in DWG.
    /// If no filters are active, returns all block types (no filtering).
    /// </summary>
    public HashSet<string> GetAcceptedBlockTypes()
    {
        var accepted = new HashSet<string>();

        if (FilterEl) accepted.Add("El");
        if (FilterNaturgas) accepted.Add("Naturgas");
        if (FilterVarmepumpe) accepted.Add("Varmepumpe");
        if (FilterFastBrændsel) accepted.Add("Fast brændsel");
        if (FilterOlie) accepted.Add("Olie");
        if (FilterFjernvarme) accepted.Add("Fjernvarme");
        if (FilterAndetIngenUdgår)
        {
            accepted.Add("Andet");
            accepted.Add("Ingen");
            accepted.Add("UDGÅR");
        }

        // If nothing is selected, accept all (no filtering)
        if (accepted.Count == 0)
            return DimensioneringV2.CommonVariables.AllBlockTypes;

        return accepted;
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Ensures pipe configurations are initialized with defaults if null.
    /// Call this after loading/deserializing settings.
    /// </summary>
    public void EnsureInitialized()
    {
        // Access the properties to trigger lazy initialization if needed
        _ = PipeConfigFL;
        _ = PipeConfigSL;
    }

    partial void OnMedieTypeChanged(MediumTypeEnum value)
    {
        // Notify that pipe configs may have changed (they're per-medium)
        OnPropertyChanged(nameof(PipeConfigFL));
        OnPropertyChanged(nameof(PipeConfigSL));
    }
    #endregion

    #region Copy Support
    internal void CopyFrom(HydraulicSettings src)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));

        foreach (var p in typeof(HydraulicSettings)
                          .GetProperties(System.Reflection.BindingFlags.Instance |
                                         System.Reflection.BindingFlags.Public)
                          .Where(pr => pr.CanRead && pr.CanWrite && pr.GetIndexParameters().Length == 0))
        {
            p.SetValue(this, p.GetValue(src));
        }
    }
    #endregion
}
