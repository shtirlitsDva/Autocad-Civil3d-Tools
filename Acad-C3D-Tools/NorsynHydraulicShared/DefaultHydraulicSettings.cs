using NorsynHydraulicCalc.Pipes;

namespace NorsynHydraulicCalc
{
    /// <summary>
    /// Minimal IHydraulicSettings implementation for metadata queries.
    /// Used by MediumPipeTypeRules to instantiate PipeTypes for discovering
    /// which pipe types support which segment types and mediums.
    /// </summary>
    public class DefaultHydraulicSettings : IHydraulicSettings
    {
        #region General Settings
        public MediumTypeEnum MedieType { get; set; } = MediumTypeEnum.Water;
        public double AfkølingBrugsvand { get; set; } = 35;
        public double FactorTillægForOpvarmningUdenBrugsvandsprioritering { get; set; } = 0.6;
        public double MinDifferentialPressureOverHovedHaner { get; set; } = 0.5;
        public double TempFrem { get; set; } = 110;
        public double AfkølingVarme { get; set; } = 35;
        public double FactorVarmtVandsTillæg { get; set; } = 1.0;
        #endregion

        #region Roughness Settings
        public double RuhedSteel { get; set; } = 0.1;
        public double RuhedPertFlextra { get; set; } = 0.01;
        public double RuhedAluPEX { get; set; } = 0.01;
        public double RuhedCu { get; set; } = 0.01;
        public double RuhedPe { get; set; } = 0.01;
        public double RuhedAquaTherm11 { get; set; } = 0.01;
        #endregion

        #region Calculation Settings
        public CalcType CalculationType { get; set; } = CalcType.CW;
        public bool ReportToConsole { get; set; } = false;
        #endregion

        #region Nyttetimer Settings
        public int SystemnyttetimerVed1Forbruger { get; set; } = 2000;
        public int SystemnyttetimerVed50PlusForbrugere { get; set; } = 2800;
        public int BygningsnyttetimerDefault { get; set; } = 2000;
        #endregion

        #region Pipe Type Configuration
        private PipeTypes? _pipeTypes;
        private PipeTypeConfiguration? _pipeConfigFL;
        private PipeTypeConfiguration? _pipeConfigSL;

        private PipeTypes GetPipeTypes()
        {
            return _pipeTypes ??= new PipeTypes(this);
        }

        public PipeTypeConfiguration PipeConfigFL
        {
            get
            {
                return _pipeConfigFL ??= DefaultPipeConfigFactory.CreateDefaultFL(MedieType, GetPipeTypes());
            }
        }

        public PipeTypeConfiguration PipeConfigSL
        {
            get
            {
                return _pipeConfigSL ??= DefaultPipeConfigFactory.CreateDefaultSL(MedieType, GetPipeTypes());
            }
        }

        public double MaxPressureLossStikSL { get; set; } = 0.3;
        #endregion
    }
}
