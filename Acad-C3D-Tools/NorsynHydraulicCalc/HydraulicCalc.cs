using NorsynHydraulicCalc.Pipes;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static List<string> reportingColumnNames;
        private static List<string> reportingUnits;
        private static List<(string, List<double>)> reportingRowsFL;
        private static List<(string, List<double>)> reportingRowsSL;
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
        private double acceptVelocity350PlusFL;
        private int acceptPressureGradient20_150FL; // Pa/m
        private int acceptPressureGradient200_300FL; // Pa/m
        private int acceptPressureGradient350PlusFL; // Pa/m
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
        public string SegmentTypeResult { get; private set; }
        public string PipeTypeResult { get; private set; }
        public string DimNameResult { get; private set; }
        public double PressureGradientResult { get; private set; }
        public double VelocityResult { get; private set; }

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
            double acceptVelocity350PlusFL,
            int acceptPressureGradient20_150FL, // Pa/m
            int acceptPressureGradient200_300FL, // Pa/m
            int acceptPressureGradient350PlusFL, // Pa/m
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
            this.acceptVelocity350PlusFL = acceptVelocity350PlusFL;
            this.acceptPressureGradient20_150FL = acceptPressureGradient20_150FL;
            this.acceptPressureGradient200_300FL = acceptPressureGradient200_300FL;
            this.acceptPressureGradient350PlusFL = acceptPressureGradient350PlusFL;
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
        private static double ColebrookTime_ms = 0.0;
        private static double SwameeJainTime_ms = 0.0;
        private static double TkachenkoMielkovskyi_ms = 0.0;
        private static void CalculateMaxFlowValues()
        {
            maxFlowTableFL = new List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)>();
            maxFlowTableSL = new List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)>();

            #region Setup reporting
            //Setup reporting
            reportingColumnNames = new List<string>()
            {
                "v max", "Di", "Tv.sn.A", "Q max v", "dPdx max", "Rel. ruhed", "Densitet", "Dyn. Visc.",
                "CW iters", "CW Re", "CW Q max dPdx", "TM iters", "TM Re", "TM Q max dPdx",

            };
            reportingUnits = new List<string>()
            {
                "[m/s]", "[m]", "[m²]", "[m³/hr]", "[Pa/m]", "[]", "[kg/m³]", "[kg/(m * s)]",
                "[n]", "[]", "[m³/hr]", "[n]", "[]", "[m³/hr]"

            };

            reportingRowsFL = new List<(string, List<double>)>();
            reportingRowsSL = new List<(string, List<double>)>();

            //Reset calculation timers
            ColebrookTime_ms = 0.0;
            SwameeJainTime_ms = 0.0;
            TkachenkoMielkovskyi_ms = 0.0;
            #endregion

            var translationBetweenMaxPertAndMinStål = new Dictionary<int, int>()
            {
                { 75, 65 },
                { 63, 50 },
                { 50, 40 },
                { 40, 32 },
                { 32, 25 }
            };

            #region Populate maxFlowTableFL
            //Populate maxFlowTableFL
            {
                int steelMinDn = 20;

                if (currentInstance.usePertFlextraFL)
                {
                    foreach (var dim in PipeTypes.PertFlextra.GetDimsRange(32, currentInstance.pertFlextraMaxDnFL))
                    {
                        maxFlowTableFL.Add((dim,
                            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Fordelingsledning),
                            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Fordelingsledning)));
                    }

                    steelMinDn = translationBetweenMaxPertAndMinStål[currentInstance.pertFlextraMaxDnFL];
                }

                foreach (var dim in PipeTypes.Stål.GetDimsRange(steelMinDn, 1000))
                {
                    maxFlowTableFL.Add((dim,
                            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Fordelingsledning),
                            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Fordelingsledning)));
                }
            }
            #endregion

            #region Populate maxFlowTableSL
            //Populate maxFlowTableSL
            {
                switch (currentInstance.pipeTypeSL)
                {
                    case PipeType.Stål:
                        throw new Exception("Stål-stikledninger er ikke tilladt!");
                    case PipeType.PertFlextra:
                        foreach (var dim in PipeTypes.PertFlextra.GetAllDimsSorted())
                        {
                            maxFlowTableSL.Add((dim,
                                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                        }
                        break;
                    case PipeType.AluPEX:
                        foreach (var dim in PipeTypes.AluPex.GetAllDimsSorted())
                        {
                            maxFlowTableSL.Add((dim,
                                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                        }
                        break;
                    case PipeType.Kobber:
                        foreach (var dim in PipeTypes.Cu.GetAllDimsSorted())
                        {
                            maxFlowTableSL.Add((dim,
                                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }

                foreach (var dim in PipeTypes.Stål.GetDimsRange(25, 1000))
                {
                    maxFlowTableSL.Add((dim,
                            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                }
            }
            #endregion

            #region Reporting
            //Print report
            Console.WriteLine(
                AsciiTableFormatter.CreateAsciiTableRows(
                    "Fordelingsledninger", reportingRowsFL, reportingColumnNames, reportingUnits, "F6"));
            Console.WriteLine();
            Console.WriteLine(
                AsciiTableFormatter.CreateAsciiTableRows(
                    "Stikledninger", reportingRowsSL, reportingColumnNames, reportingUnits, "F6"));
            Console.WriteLine();

            Console.WriteLine($"Calculation timers:\n" +
                $"Colebrook-White:       {ColebrookTime_ms} ms\n" +
                //$"Swamee-Jain:           {SwameeJainTime_ms} ms\n" +
                $"Tkachenko-Mielkovskyi: {TkachenkoMielkovskyi_ms} ms");
            #endregion
        }
        private static double CalculateMaxFlow(Dim dim, TempSetType tempSetType, SegmentType st)
        {
            double vmax = currentInstance.Vmax(dim, st);

            double A = dim.CrossSectionArea;

            //Max flow rate based on velocity limit
            double Qmax_velocity_m3s = vmax * A;
            double Qmax_velocity_m3hr = Qmax_velocity_m3s * 3600; // m^3/hr

            //Max flow rate based on pressure gradient
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var resCW = FindQmaxPressure(dim, tempSetType, st, Calc.Colebrook);
            sw.Stop();
            ColebrookTime_ms += sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            var resTM = FindQmaxPressure(dim, tempSetType, st, Calc.TkachenkoMielkovskyi);
            sw.Stop();
            TkachenkoMielkovskyi_ms += sw.Elapsed.TotalMilliseconds;

            #region Reporting
            string rowName = $"{dim.DimName} {tempSetType}";
            List<double> data = new List<double>()
            {
                vmax, dim.InnerDiameter_m, dim.CrossSectionArea, Qmax_velocity_m3hr,
                currentInstance.dPdx_max(dim, st), dim.Roughness_m / dim.InnerDiameter_m,
                rho(currentInstance.Temp(tempSetType, st)), mu(currentInstance.Temp(tempSetType, st)),
                resCW.iterations, resCW.Re, resCW.Qmax, resTM.iterations, resTM.Re, resTM.Qmax
            };
            switch (st)
            {
                case SegmentType.Fordelingsledning:
                    reportingRowsFL.Add((rowName, data));
                    break;
                case SegmentType.Stikledning:
                    reportingRowsSL.Add((rowName, data));
                    break;
                default:
                    break;
            }
            #endregion

            return Math.Min(Qmax_velocity_m3hr, Math.Min(resCW.Qmax, resTM.Qmax));
        }
        private static (double Qmax, int iterations, double Re) FindQmaxPressure(
            Dim dim, TempSetType tempSetType, SegmentType st, Calc calc)
        {
            double dp_dx = currentInstance.dPdx_max(dim, st);
            double reynolds = 0, f, velocity = 1, newVelocity;
            double density = rho(currentInstance.Temp(tempSetType, st));
            double viscosity = mu(currentInstance.Temp(tempSetType, st));
            double tolerance = 1e-6;
            int maxIterations = 100;
            int iteration = 0;
            double relativeRoughness = dim.Roughness_m / dim.InnerDiameter_m;

            while (iteration < maxIterations)
            {
                iteration++;

                reynolds =
                    (density * velocity * dim.InnerDiameter_m) / viscosity;

                switch (calc)
                {
                    case Calc.Colebrook:
                        f = CalculateFrictionFactorColebrookWhite(reynolds, relativeRoughness, tolerance);
                        break;
                    case Calc.SwameeJain:
                        throw new Exception("Swamee-Jain is not implemented!");
                    case Calc.TkachenkoMielkovskyi:
                        f = CalculateFrictionFactorTkachenkoMielkovskyi(reynolds, relativeRoughness);
                        break;
                    default:
                        throw new NotImplementedException($"{calc} is not implemented!");
                }

                newVelocity = Math.Sqrt((2 * dp_dx * dim.InnerDiameter_m)
                    / (f * rho(currentInstance.Temp(tempSetType, st))));

                if (Math.Abs(newVelocity - velocity) < tolerance) break;

                velocity = newVelocity;
            }

            return
                (velocity * dim.CrossSectionArea * 3600,
                iteration,
                reynolds);
        }
        private static double CalculateFrictionFactorColebrookWhite(double Re, double relativeRoughness, double tolerance)
        {
            ////double f = 0.02; // initial guess for friction factor
            //double f = CalculateFrictionFactorTkachenkoMielkovskyi(Re, relativeRoughness);

            //for (int i = 0; i < 100; i++)
            //{
            //    double lhs = 1.0 / Math.Sqrt(f);
            //    double rhs = -2.0 * Math.Log10((
            //        relativeRoughness / 3.7) + (2.51 / (Re * Math.Sqrt(f))));

            //    double f_new = 1.0 / Math.Pow((lhs - rhs), 2);

            //    if (Math.Abs(f_new - f) < tolerance) 
            //    { 
            //        return f_new; 
            //    }

            //    f = f_new;
            //}

            double f1 = CalculateFrictionFactorTkachenkoMielkovskyi(Re, relativeRoughness);
            double f2 = f1 + 0.05;

            for (int i = 0; i < 100; i++)
            {
                // Colebrook-White residual function g(f)
                double g1 = 1.0 / Math.Sqrt(f1) + 2.0 * Math.Log10((relativeRoughness / 3.7) + (2.51 / (Re * Math.Sqrt(f1))));
                double g2 = 1.0 / Math.Sqrt(f2) + 2.0 * Math.Log10((relativeRoughness / 3.7) + (2.51 / (Re * Math.Sqrt(f2))));

                // Update using the secant method
                double f_new = f2 - (f2 - f1) * g2 / (g2 - g1);

                // Check for convergence
                if (Math.Abs(f_new - f2) < tolerance) return f_new;

                // Update guesses
                f1 = f2;
                f2 = f_new;
            }

            Console.WriteLine("Warning: Secant method did not converge.");
            return f2;


            //return f; // Return friction factor
        }
        private static double CalculateFrictionFactorTkachenkoMielkovskyi(double Re, double relativeRoughness)
        {
            double f0inverseHalf = -0.79638 * Math.Log(relativeRoughness / 8.208 + 7.3357 / Re);
            double f0 = Math.Pow(f0inverseHalf, -2);

            double a1 = Re * relativeRoughness + 9.3120665 * f0inverseHalf;

            double term1 = (8.128943 + a1) / (8.128943 * f0inverseHalf - 0.86859209 * a1 * Math.Log(a1 / (3.7099535 * Re)));
            double f = Math.Pow(term1, 2);

            return f;
        }
        private static double CalculateFrictionFactorLaminar(double Re) => 64.0 / Re;
        private enum Calc
        {
            Colebrook,
            SwameeJain,
            TkachenkoMielkovskyi
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
                    else if (dim.NominalDiameter <= 300) return acceptVelocity200_300FL;
                    else return acceptVelocity350PlusFL;
                //TODO: Find out what pipe types are considered flexible?
                //Here it is assumed that all are flexible except steel
                case SegmentType.Stikledning:
                    if (dim.PipeType != PipeType.Stål) return acceptVelocityFlexibleSL;
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
                    else if (dim.NominalDiameter <= 300) return acceptPressureGradient200_300FL;
                    else return acceptPressureGradient350PlusFL;
                case SegmentType.Stikledning:
                    if (dim.PipeType != PipeType.Stål) return acceptPressureGradientFlexibleSL;
                    else return acceptPressureGradient20_150SL;
                default:
                    throw new NotImplementedException();
            }
        }
        private int Temp(TempSetType tst, SegmentType st)
        {
            switch (tst)
            {
                case TempSetType.Supply:
                    if (st == SegmentType.Fordelingsledning) return tempFremFL;
                    else return tempFremSL;
                case TempSetType.Return:
                    if (st == SegmentType.Fordelingsledning) return tempReturFL;
                    else return tempReturSL;
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
        private static double rho(int T)
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
        private static double cp(int T)
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
        private static double nu(int T)
        {
            if (LookupData.nu.TryGetValue(T, out double value)) return value * 10e-6;
            else if (T > 0 && T < 201)
            {
                int lowerkey = LookupData.nu.Keys.Where(k => k < T).Max();
                int upperkey = LookupData.nu.Keys.Where(k => k > T).Min();

                double lowerValue = LookupData.nu[lowerkey];
                double upperValue = LookupData.nu[upperkey];
                //Interpolate
                return (lowerValue + (upperValue - lowerValue) * ((T - lowerkey) / (double)(upperkey - lowerkey))) * 10e-6;
            }
            throw new ArgumentException($"Temperature out of range for \"nu\": {T}, allowed values: 0 - 200.");
        }
        private static double mu(int T)
        {
            if (LookupData.mu.TryGetValue(T, out double value)) return value;
            else if (T > 0 && T < 201)
            {
                int lowerkey = LookupData.mu.Keys.Where(k => k < T).Max();
                int upperkey = LookupData.mu.Keys.Where(k => k > T).Min();

                double lowerValue = LookupData.mu[lowerkey];
                double upperValue = LookupData.mu[upperkey];
                //Interpolate
                return (lowerValue + (upperValue - lowerValue) * ((T - lowerkey) / (double)(upperkey - lowerkey)));
            }
            throw new ArgumentException($"Temperature out of range for \"nu\": {T}, allowed values: 0 - 200.");
        }
        private static double volume(int temp, int deltaT) => 3600 / (rho(temp) * cp(temp) * deltaT);
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

            List<(string, List<double>)> columns = new List<(string, List<double>)> {
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

            Console.WriteLine(AsciiTableFormatter.CreateAsciiTableColumns(columns, rowNames, "F6"));
            Console.WriteLine();

            double flow = new[] { dimFlow1Frem, dimFlow2Frem, dimFlow1Retur, dimFlow2Retur }.Max();

            var dim = determineDim(flow, TempSetType.Return);

            double velocity = flow / 3600 / dim.CrossSectionArea;
            double reynolds = 
                rho(this.Temp(TempSetType.Return, this.segmentType))
                * velocity 
                * dim.InnerDiameter_m
                / mu(this.Temp(TempSetType.Return, this.segmentType));

            double f = CalculateFrictionFactorColebrookWhite(reynolds, dim.Roughness_m / dim.InnerDiameter_m, 1e-6);

            double gradient = f * rho(this.Temp(TempSetType.Return, this.segmentType)) *
                velocity * velocity / (2 * dim.InnerDiameter_m);

            SegmentTypeResult = segmentType.ToString();
            PipeTypeResult = dim.PipeType.ToString();
            DimNameResult = dim.DimName;
            PressureGradientResult = gradient;
            VelocityResult = velocity;

            //Now report these five values to console
            Console.WriteLine(
                $"Segment type: {SegmentTypeResult}\n" +
                $"Pipe type: {PipeTypeResult}\n" +
                $"Dim name: {DimNameResult}\n" +
                $"Pressure gradient: {PressureGradientResult} Pa/m\n" +
                $"Velocity: {VelocityResult} m/s"
                );
        }

        private Dim determineDim(double flow, TempSetType tst)
        {
            switch (this.segmentType)
            {
                case SegmentType.Fordelingsledning:
                    for (int i = 0; i < maxFlowTableFL.Count; i++)
                    {
                        var cur = maxFlowTableFL[i];
                        switch (tst)
                        {
                            case TempSetType.Supply:
                                if (flow >= cur.MaxFlowFrem) continue;
                                return cur.Dim;
                            case TempSetType.Return:
                                if (flow >= cur.MaxFlowReturn) continue;
                                return cur.Dim;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    break;
                case SegmentType.Stikledning:
                    for (int i = 0; i < maxFlowTableSL.Count; i++)
                    {
                        var cur = maxFlowTableSL[i];
                        switch (tst)
                        {
                            case TempSetType.Supply:
                                if (flow >= cur.MaxFlowFrem) continue;
                                return cur.Dim;
                            case TempSetType.Return:
                                if (flow >= cur.MaxFlowReturn) continue;
                                return cur.Dim;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }

            throw new Exception("No suitable dimension found!");
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
