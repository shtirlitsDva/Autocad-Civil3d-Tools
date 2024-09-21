using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NorsynHydraulicCalc
{
    public class HydraulicCalc
    {
        #region Static properties for max flow pipe table
        private static HydraulicCalc currentInstance;
        private static List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> maxFlowTableFL;
        private static List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> maxFlowTableSL;
        #endregion

        #region Private properties
        // From client blocks
        private SegmentType segmentType; // Type of segment
        private double totalHeatingDemand; // MWh/year
        private int numberOfClients; // Number of clients
        private int numberOfUnits; // Number of units

        // Shared
        private int hotWaterReturnTemp; // degree
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
        private bool usePertFlextraFL; // boolean
        private int pertFlextraMaxDnFL; // mm

        // Stikledninger (Service pipes)
        private int tempFremSL; // degree
        private int tempReturSL; // degree
        private double factorVarmtVandsTillægSL;
        private int nyttetimerOneUserSL;
        private PipeType pipeTypeSL;
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
            int hotWaterReturnTemp, // degree
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
            bool usePertFlextraFL,
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
            this.hotWaterReturnTemp = hotWaterReturnTemp;
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
            this.usePertFlextraFL = usePertFlextraFL;
            this.pertFlextraMaxDnFL = pertFlextraMaxDnFL;

            // Stikledninger
            this.tempFremSL = tempFremSL;
            this.tempReturSL = tempReturSL;
            this.factorVarmtVandsTillægSL = factorVarmtVandsTillægSL;
            this.nyttetimerOneUserSL = nyttetimerOneUserSL;
            this.pipeTypeSL = (PipeType)Enum.Parse(typeof(PipeType), pipeTypeSL);
            this.acceptVelocityFlexibleSL = acceptVelocityFlexibleSL;
            this.acceptVelocity20_150SL = acceptVelocity20_150SL;
            this.acceptPressureGradientFlexibleSL = acceptPressureGradientFlexibleSL;
            this.acceptPressureGradient20_150SL = acceptPressureGradient20_150SL;
            this.maxPressureLossStikSL = maxPressureLossStikSL;

            Initialize();
        }
        #endregion

        #region Initialize the max flow table
        

        public void Initialize()
        {
            if (currentInstance == null || !AreInstancesEqual(this, currentInstance))
            {
                currentInstance = this;

                CalculateMaxFlowValues();
            }
        }

        private static void CalculateMaxFlowValues()
        {
            maxFlowTableFL = new List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)>();
            maxFlowTableSL = new List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)>();

            #region Populate maxFlowTableFL
            //Populate maxFlowTableFL
            {
                if (currentInstance.usePertFlextraFL)
                {
                    foreach (var dim in PipeTypes.PertFlextra.GetDimsRange(32, currentInstance.pertFlextraMaxDnFL))
                    {
                        maxFlowTableFL.Add((dim,
                            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Fordelingsledning),
                            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Fordelingsledning)));
                    }
                }
                else
                {

                }
            }
            #endregion
        }

        private static double CalculateMaxFlow(Dim dim, TempSetType tempSetType, SegmentType st)
        {
            double vmax = currentInstance.Vmax(dim, st);
            double dPdx_max = currentInstance.dPdx_max(dim, st);

            double D_m = dim.InnerDiameter_mm / 1000;
            double A = dim.CrossSectionArea;

            //Max flow rate based on velocity limit
            double Qmax_velocity_m3s = vmax * A;

            return maxFlow;
        }
        #endregion

        #region Properties aliases to handle different types
        #region Properties for general max flow calculation
        private double Vmax(Dim dim, SegmentType st)
        {
            switch (st)
            {
                case SegmentType.Fordelingsledning:
                    if (dim.NominalDiameter <= 150) return acceptVelocity20_150FL;
                    else return acceptVelocity200_300FL;
                    //TODO: Find out what pipe types are considered flexible?
                    //Here it is assumed that all are flexible except steel
                case SegmentType.Stikledning:
                    if (dim.PipeType !=  PipeType.Stål) return acceptVelocityFlexibleSL;
                    else return acceptVelocity20_150SL;
                default:
                    throw new NotImplementedException();
            }
        }
        private double dPdx_max(Dim dim, SegmentType st)
        {
            switch (st)
            {
                case SegmentType.Fordelingsledning:
                    if (dim.NominalDiameter <= 150) return acceptPressureGradient20_150FL;
                    else return acceptPressureGradient200_300FL;
                case SegmentType.Stikledning:
                    if (dim.PipeType != PipeType.Stål) return acceptPressureGradientFlexibleSL;
                    else return acceptPressureGradient20_150SL;
                default:
                    throw new NotImplementedException();
            }
        }
        #endregion

        #region Properties for instance segment calculations
        // Properties depenedent on segmentType
        private int N1 =>
            segmentType == SegmentType.Fordelingsledning ? nyttetimerOneUserFL : nyttetimerOneUserSL;
        private int N50 => nyttetimer50PlusUsersFL;
        /// <summary>
        /// Heating delta temperature
        /// </summary>
        private int dT1 =>
            segmentType == SegmentType.Fordelingsledning ? tempFremFL - tempReturFL : tempFremSL - tempReturSL;
        /// <summary>
        /// Hot water delta temperature
        /// </summary>
        private int dT2 =>
            segmentType == SegmentType.Fordelingsledning
            ? tempFremFL - hotWaterReturnTemp
            : tempFremSL - hotWaterReturnTemp;
        private int Tf =>
            segmentType == SegmentType.Fordelingsledning ? tempFremFL : tempFremSL;
        private int Tr =>
            segmentType == SegmentType.Fordelingsledning ? tempReturFL : tempReturSL;
        private int Tr_hw => hotWaterReturnTemp;
        private double f_b =>
            segmentType == SegmentType.Fordelingsledning ? factorVarmtVandsTillægFL : factorVarmtVandsTillægSL;
        private double KX => factorTillægForOpvarmningUdenBrugsvandsprioritering; 
        #endregion

        #region Water properties
        private double rho(int T)
        {
            if (LookupData.rho.TryGetValue(T, out double value))
                return value * 1000;
            else if (T > 100 && T < 301)
            {
                int lowerkey = ((T - 100) / 50) * 50 + 100;
                int upperkey = lowerkey + 50;

                double lowerValue = LookupData.rho[lowerkey];
                double upperValue = LookupData.rho[upperkey];
                //Interpolate
                return (lowerValue + (upperValue - lowerValue) * ((T - lowerkey) / (double)(upperkey - lowerkey))
                    ) * 1000;
            }
            throw new ArgumentException($"Temperature out of range for \"rho\": {T}, allowed values: 0 - 300.");
        }
        private double cp(int T)
        {
            if (LookupData.cp.TryGetValue(T, out double value)) return value;
            else if (T > 0 && T < 201)
            {
                int lowerkey = LookupData.cp.Keys.Where(k => k < T).Max();
                int upperkey = LookupData.cp.Keys.Where(k => k > T).Min();

                double lowerValue = LookupData.cp[lowerkey];
                double upperValue = LookupData.cp[upperkey];
                //Interpolate
                return lowerValue + (upperValue - lowerValue) * ((T - lowerkey) / (double)(upperkey - lowerkey));
            }
            throw new ArgumentException($"Temperature out of range for \"cp\": {T}, allowed values: 0 - 200.");
        }
        private double nu(int T)
        {
            if (LookupData.nu.TryGetValue(T, out double value)) return value;
            else if (T > 0 && T < 201)
            {
                int lowerkey = LookupData.nu.Keys.Where(k => k < T).Max();
                int upperkey = LookupData.nu.Keys.Where(k => k > T).Min();

                double lowerValue = LookupData.nu[lowerkey];
                double upperValue = LookupData.nu[upperkey];
                //Interpolate
                return lowerValue + (upperValue - lowerValue) * ((T - lowerkey) / (double)(upperkey - lowerkey));
            }
            throw new ArgumentException($"Temperature out of range for \"nu\": {T}, allowed values: 0 - 200.");
        }
        private double volume(int temp, int deltaT) => 3600 / (rho(temp) * cp(temp) * deltaT); 
        #endregion
        #endregion

        public void Calculate()
        {
            double s_heat = N1 / N50 + (1 - N1 / N50) / numberOfClients;
            double s_hw = (51 - numberOfUnits) / (50 * Math.Sqrt(numberOfUnits));

            double dimFlow1Frem = (totalHeatingDemand * 1000 / N1) * s_heat * volume(Tf, dT1);
            double dimFlow2Frem = (totalHeatingDemand * 1000 / N1) * s_heat * KX * volume(Tf, dT1)
                + numberOfUnits * 33 * f_b * s_hw * volume(Tf, dT2);

            double dimFlow1Retur = (totalHeatingDemand * 1000 / N1) * s_heat * volume(Tr, dT1);
            double dimFlow2Retur = (totalHeatingDemand * 1000 / N1) * s_heat * KX * volume(Tr, dT1)
                + numberOfUnits * 33 * f_b * s_hw * volume(Tr_hw, dT2);

            List<(string, List<double>)> r1 = new List<(string, List<double>)> {
                ("Heating demand", new List<double>()
                {
                    (totalHeatingDemand * 1000 / N1),
                    (totalHeatingDemand * 1000 / N1),
                    (totalHeatingDemand * 1000 / N1),
                    (totalHeatingDemand * 1000 / N1),
                }),
                ("s_heat", new List<double>() { s_heat, s_heat, s_heat, s_heat }),
                ("s_hw", new List<double>() { 0, s_hw, 0, s_hw }),
                ("rho heat", new List<double>() { rho(Tf), rho(Tf), rho(Tr), rho(Tr)}),
                ("rho hw", new List<double>() { rho(Tf), rho(Tf), rho(Tr_hw), rho(Tr_hw)}),
                ("Cp heat", new List<double>() { cp(Tf), cp(Tf), cp(Tr), cp(Tr)}),
                ("Cp hw", new List<double>() { cp(Tf), cp(Tf), cp(Tr_hw), cp(Tr_hw)}),
                ("m^3/kW heat", new List<double>() { volume(Tf, dT1), volume(Tf, dT1), volume(Tr, dT1), volume(Tr, dT1) }),
                ("m^3/kW hw", new List<double>() { 0, volume(Tf, dT2), 0, volume(Tr_hw, dT2) }),
                ("Flow", new List<double>() { dimFlow1Frem, dimFlow2Frem, dimFlow1Retur, dimFlow2Retur }),
            };

            List<string> rowNames = new List<string> { "Frem 1", "Frem 2", "Retur 1", "Retur 2" };

            Console.WriteLine(CreateAsciiTable(r1, rowNames, "F6"));
            Console.WriteLine();


        }

        private bool AreInstancesEqual(HydraulicCalc instance1, HydraulicCalc instance2)
        {
            if (instance1 == null || instance2 == null) return false;

            Type type = typeof(HydraulicCalc);

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                object value1 = property.GetValue(instance1);
                object value2 = property.GetValue(instance2);

                if (value1 == null && value2 == null) continue;

                if (value1 == null || value2 == null) return false;

                if (!value1.Equals(value2)) return false;
            }

            return true;
        }
        public static string CreateAsciiTable(List<(string, List<double>)> columns, List<string> rowNames, string format)
        {
            // Ensure that rowNames match the number of rows
            if (rowNames.Count != columns.First().Item2.Count)
                throw new ArgumentException("Row names count must match the number of rows in the columns.");

            // Determine the maximum width for each column and for the row names
            int rowNameWidth = rowNames.Max(name => name.Length);
            var columnWidths = columns.Select(col =>
                Math.Max(col.Item1.Length, col.Item2.Max(val => val.ToString(format).Length))).ToList();

            // Generate table header with row name column
            string header = "| " + "Name".PadLeft(rowNameWidth) + " | " +
                            string.Join(" | ", columns.Select((col, idx) => col.Item1.PadLeft(columnWidths[idx]))) + " |";

            // Generate separator line
            string separator = "+-" + new string('-', rowNameWidth) + "-+-" +
                               string.Join("-+-", columnWidths.Select(width => new string('-', width))) + "-+";

            // Generate table rows
            int numRows = columns.First().Item2.Count;
            List<string> rows = new List<string>();
            for (int i = 0; i < numRows; i++)
            {
                string row = "| " + rowNames[i].PadLeft(rowNameWidth) + " | " +
                             string.Join(" | ", columns.Select((col, idx) => col.Item2[i].ToString(format).PadLeft(columnWidths[idx]))) + " |";
                rows.Add(row);
            }

            // Combine all parts into the final table
            return separator + "\n" + header + "\n" + separator + "\n" + string.Join("\n", rows) + "\n" + separator;
        }
    }

    public enum SegmentType
    {
        Fordelingsledning,
        Stikledning
    }

    public enum PipeType
    {
        Stål,
        PertFlextra,
        AluPEX,
        Kobber
    }

    public enum TempSetType
    {
        Supply,
        Return
    }
}
