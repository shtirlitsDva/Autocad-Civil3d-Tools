using System;

namespace NorsynHydraulicCalc
{
    public class HydraulicCalc
    {
        #region Private properties
        // From client blocks
        private SegmentType segmentType; // Type of segment
        private double totalHeatingDemand; // MWh/year
        private int numberOfClients; // Number of clients
        private int numberOfUnits; // Number of units

        // Shared
        private int deltaTHotWater; // degree
        private double factorTillægForOpvarmningUdenBrugsvandsprioritering;
        private double minDifferentialPressureOverHovedHaner; // bar

        // Fordelingsledninger (Distribution pipes)
        private int tempFremFL; // degree
        private int tempReturFL; // degree
        private double factorVarmtVandsTillægFL;
        private int nyttetimerOneUserFL;
        private int nyttetimer50PlusUsersFL;
        private double acceptVelocity20_150FL; // m/s
        private double acceptVelocity200_300FL; // m/s
        private int acceptPressureGradient20_150FL; // Pa/m
        private int acceptPressureGradient200_300FL; // Pa/m
        private string pipeTypeFL;
        private int pertFlextraMaxDnFL;

        // Stikledninger (Service pipes)
        private int tempFremSL; // degree
        private int tempReturSL; // degree
        private double factorVarmtVandsTillægSL;
        private int nyttetimerOneUserSL;
        private string pipeTypeSL;
        private double acceptVelocityFlexibleSL; // m/s
        private double acceptVelocity20_150SL; // m/s
        private int acceptPressureGradientFlexibleSL; // Pa/m
        private int acceptPressureGradient20_150SL; // Pa/m
        private double maxPressureLossStikSL; // bar 
        #endregion

        // Output
        public string segmentTypeResult { get; private set; }
        public string pipeTypeResult { get; private set; }
        public double pressureGradientResult { get; private set; }
        public double velocityResult { get; private set; }

        #region Constructor
        public HydraulicCalc(
            // Type of segment
            string segmentType,

            // From client blocks
            double totalHeatingDemand, // MWh/year
            int numberOfClients, // Number of clients
            int numberOfUnits, // Number of units

            // Shared
            int deltaTHotWater, // degree
            double factorTillægForOpvarmningUdenBrugsvandsprioritering,
            double minDifferentialPressureOverHovedHaner, // bar

            // Fordelingsledninger
            int tempFremFL, int tempReturFL, // degree
            double factorVarmtVandsTillægFL,
            int nyttetimerOneUserFL,
            int nyttetimer50PlusUsersFL,
            double acceptVelocity20_150FL, // m/s
            double acceptVelocity200_300FL, // m/s
            int acceptPressureGradient20_150FL, // Pa/m
            int acceptPressureGradient200_300FL, // Pa/m
            string pipeTypeFL,
            int pertFlextraMaxDnFL,

            // Stikledninger
            int tempFremSL, int tempReturSL, // degree
            double factorVarmtVandsTillægSL,
            int nyttetimerOneUserSL,
            string pipeTypeSL,
            double acceptVelocityFlexibleSL, // m/s
            double acceptVelocity20_150SL, // m/s
            int acceptPressureGradientFlexibleSL, // Pa/m
            int acceptPressureGradient20_150SL, // Pa/m
            double maxPressureLossStikSL // bar
        )
        {
            //Convert segmentType to enum
            this.segmentType = (SegmentType)Enum.Parse(typeof(SegmentType), segmentType);

            // From client blocks
            this.totalHeatingDemand = totalHeatingDemand;
            this.numberOfClients = numberOfClients;
            this.numberOfUnits = numberOfUnits;

            // Shared
            this.deltaTHotWater = deltaTHotWater;
            this.factorTillægForOpvarmningUdenBrugsvandsprioritering = factorTillægForOpvarmningUdenBrugsvandsprioritering;
            this.minDifferentialPressureOverHovedHaner = minDifferentialPressureOverHovedHaner;

            // Fordelingsledninger
            this.tempFremFL = tempFremFL;
            this.tempReturFL = tempReturFL;
            this.factorVarmtVandsTillægFL = factorVarmtVandsTillægFL;
            this.nyttetimerOneUserFL = nyttetimerOneUserFL;
            this.nyttetimer50PlusUsersFL = nyttetimer50PlusUsersFL;
            this.acceptVelocity20_150FL = acceptVelocity20_150FL;
            this.acceptVelocity200_300FL = acceptVelocity200_300FL;
            this.acceptPressureGradient20_150FL = acceptPressureGradient20_150FL;
            this.acceptPressureGradient200_300FL = acceptPressureGradient200_300FL;
            this.pipeTypeFL = pipeTypeFL;
            this.pertFlextraMaxDnFL = pertFlextraMaxDnFL;

            // Stikledninger
            this.tempFremSL = tempFremSL;
            this.tempReturSL = tempReturSL;
            this.factorVarmtVandsTillægSL = factorVarmtVandsTillægSL;
            this.nyttetimerOneUserSL = nyttetimerOneUserSL;
            this.pipeTypeSL = pipeTypeSL;
            this.acceptVelocityFlexibleSL = acceptVelocityFlexibleSL;
            this.acceptVelocity20_150SL = acceptVelocity20_150SL;
            this.acceptPressureGradientFlexibleSL = acceptPressureGradientFlexibleSL;
            this.acceptPressureGradient20_150SL = acceptPressureGradient20_150SL;
            this.maxPressureLossStikSL = maxPressureLossStikSL;
        }
        #endregion

        // Properties depenedent on segmentType
        private int N1 =>
            segmentType == SegmentType.Fordelingsledning ? nyttetimerOneUserFL : nyttetimerOneUserSL;
        private int N50 => nyttetimer50PlusUsersFL;
        private int dT =>
            segmentType == SegmentType.Fordelingsledning ? tempFremFL - tempReturFL : tempFremSL - tempReturSL;
        private int Tmed =>
            segmentType == SegmentType.Fordelingsledning
            ? (tempFremFL + tempReturFL + 1) / 2
            : (tempFremSL + tempReturSL + 1) / 2;
        private double rho(int T) => LookupData.rho[T];
        private double cp(int T) => LookupData.cp[T];

        public void Calculate()
        {
            double s_heat = N1 / N50 + (1 - N1 / N50) / numberOfClients;
            double s_hw = (51 - numberOfUnits) / (50 * Math.Sqrt(numberOfUnits));

            double dimFlow1 = (totalHeatingDemand * 1000 / N1) * s_heat * 3600 / (rho(Tmed) * cp(Tmed) * Tmed);
        }
    }

    public enum SegmentType
    {
        Fordelingsledning,
        Stikledning
    }
}
