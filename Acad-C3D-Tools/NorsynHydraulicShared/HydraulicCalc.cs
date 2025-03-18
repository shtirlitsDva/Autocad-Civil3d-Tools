using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NorsynHydraulicCalc
{
    public class HydraulicCalc
    {
        public static Version version = new Version(20250304, 0);

        private ILog log { get; set; }

        #region Static properties for max flow pipe table
        private List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> maxFlowTableFL;
        private List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> maxFlowTableSL;
        private List<string> reportingColumnNames;
        private List<string> reportingUnits;
        private List<(string, List<double>)> reportingRowsFL;
        private List<(string, List<double>)> reportingRowsSL;
        #endregion

        #region Private properties
        //Settings
        private HydraulicSettings s;
        //Pipe types
        private PipeTypes pipeTypes;

        // Shared
        private int hotWaterReturnTemp => s.HotWaterReturnTemp; // degree
        private double factorTillægForOpvarmningUdenBrugsvandsprioritering =>
            s.FactorTillægForOpvarmningUdenBrugsvandsprioritering;
        
        //Not used for Hydraulic calculations, only used when calculating critical path
        //private double minDifferentialPressureOverHovedHaner =>
        //    s.MinDifferentialPressureOverHovedHaner; // bar

        //Roughness settings are not used in Hydraulic calculations from here
        //But they are supplied by the pipe types.
        //private double ruhedSteel => s.RuhedSteel; // mm
        //private double ruhedPertFlextra => s.RuhedPertFlextra; // mm
        //private double ruhedAluPEX => s.RuhedAluPEX; // mm
        //private double ruhedCu => s.RuhedCu; // mm

        // Fordelingsledninger (Distribution pipes)
        private int tempFremFL => s.TempFremFL; // degree
        private int tempReturFL => s.TempReturFL; // degree
        private double factorVarmtVandsTillægFL => s.FactorVarmtVandsTillægFL;
        private int nyttetimerOneUserFL => s.NyttetimerOneUserFL;
        private int nyttetimer50PlusUsersFL => s.Nyttetimer50PlusUsersFL;
        private double acceptVelocity20_150FL => s.AcceptVelocity20_150FL; // m/s
        private double acceptVelocity200_300FL => s.AcceptVelocity200_300FL; // m/s
        private double acceptVelocity350PlusFL => s.AcceptVelocity350PlusFL;
        private int acceptPressureGradient20_150FL => s.AcceptPressureGradient20_150FL; // Pa/m
        private int acceptPressureGradient200_300FL => s.AcceptPressureGradient200_300FL; // Pa/m
        private int acceptPressureGradient350PlusFL => s.AcceptPressureGradient350PlusFL; // Pa/m
        private bool usePertFlextraFL => s.UsePertFlextraFL; // boolean
        private int pertFlextraMaxDnFL => s.PertFlextraMaxDnFL; // mm

        // Stikledninger (Service pipes)
        private int tempFremSL => s.TempFremSL; // degree
        private int tempReturSL => s.TempReturSL; // degree
        private double factorVarmtVandsTillægSL => s.FactorVarmtVandsTillægSL;
        private int nyttetimerOneUserSL => s.NyttetimerOneUserSL;
        private PipeType pipeTypeSL => s.PipeTypeSL;
        private double acceptVelocityFlexibleSL => s.AcceptVelocityFlexibleSL; // m/s
        private double acceptVelocity20_150SL => s.AcceptVelocity20_150SL; // m/s
        private int acceptPressureGradientFlexibleSL => s.AcceptPressureGradientFlexibleSL; // Pa/m
        private int acceptPressureGradient20_150SL => s.AcceptPressureGradient20_150SL; // Pa/m
        private double maxPressureLossStikSL => s.MaxPressureLossStikSL; // bar

        //Calculation settings
        private CalcType calcType => s.CalculationType;
        private bool reportToConsole => s.ReportToConsole;
        #endregion

        #region Timing
        private static Stopwatch sw = new Stopwatch();
        #endregion

        #region Constructor
        public HydraulicCalc(HydraulicSettings settings, ILog logger)
        {
            log = logger;

            s = settings;
            pipeTypes = new PipeTypes(s);

            log.Report($"HydraulicCalc {version}.");

            sw.Start();
            CalculateMaxFlowValues();
            sw.Stop();
            if (reportToConsole)
                log.Report($"Initialization time {sw.ElapsedMilliseconds} ms.");
        }
        #endregion

        #region Initialize the max flow table
        private void CalculateMaxFlowValues()
        {
            maxFlowTableFL = new List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)>();
            maxFlowTableSL = new List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)>();

            #region Setup reporting
            //Setup reporting
            if (reportToConsole)
            {
                reportingColumnNames = new List<string>()
                {
                    "v max", "Di", "Tv.sn.A", "Q max v", "dPdx max", "Rel. ruhed", "Densitet", "Dyn. Visc.",
                    "iters", "Re", "Q max dPdx", "Dim flow"

                };
                reportingUnits = new List<string>()
                {
                    "[m/s]", "[m]", "[m²]", "[m³/hr]", "[Pa/m]", "[]", "[kg/m³]", "[kg/(m * s)]",
                    "[n]", "[]", "[m³/hr]", "[kg/s]"

                };

                reportingRowsFL = new List<(string, List<double>)>();
                reportingRowsSL = new List<(string, List<double>)>();
            }
            #endregion

            var translationBetweenMaxPertAndMinStål = new Dictionary<int, int>()
            {
                //{ 75, 65 },
                //{ 63, 50 },
                //{ 50, 40 },
                //{ 40, 32 },
                //{ 32, 25 }

                { 75, 65 },
                { 63, 50 },
                { 50, 40 },
            };

            #region Populate maxFlowTableFL
            //Populate maxFlowTableFL
            {
                int steelMinDn = 32;

                if (usePertFlextraFL)
                {
                    foreach (var dim in pipeTypes.PertFlextra.GetDimsRange(
                        50, pertFlextraMaxDnFL))
                    {
                        maxFlowTableFL.Add((dim,
                            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Fordelingsledning),
                            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Fordelingsledning)));
                    }

                    steelMinDn = translationBetweenMaxPertAndMinStål[pertFlextraMaxDnFL];
                }

                foreach (var dim in pipeTypes.Stål.GetDimsRange(steelMinDn, 1000))
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
                switch (pipeTypeSL)
                {
                    case PipeType.Stål:
                        throw new Exception("Stål-stikledninger er ikke tilladt!");
                    case PipeType.PertFlextra:
                        foreach (var dim in pipeTypes.PertFlextra.GetDimsRange(25, 75))
                        {
                            maxFlowTableSL.Add((dim,
                                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                        }
                        break;
                    case PipeType.AluPEX:
                        foreach (var dim in pipeTypes.AluPex.GetDimsRange(26, 32))
                        {
                            maxFlowTableSL.Add((dim,
                                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                        }
                        break;
                    case PipeType.Kobber:
                        foreach (var dim in pipeTypes.Cu.GetDimsRange(22, 28))
                        {
                            maxFlowTableSL.Add((dim,
                                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                        }
                        break;
                    default:
                        throw new NotImplementedException($"{pipeTypeSL} not Implemented!");
                }

                foreach (var dim in pipeTypes.Stål.GetDimsRange(32, 1000))
                {
                    maxFlowTableSL.Add((dim,
                            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                }
            }
            #endregion

            #region Reporting
            if (reportToConsole)
            {
                //Print report
                log.Report(
                    AsciiTableFormatter.CreateAsciiTableRows(
                        "Fordelingsledninger", reportingRowsFL, reportingColumnNames, reportingUnits, "F6"));
                log.Report();
                log.Report(
                    AsciiTableFormatter.CreateAsciiTableRows(
                        "Stikledninger", reportingRowsSL, reportingColumnNames, reportingUnits, "F6"));
                log.Report();
            }
            #endregion
        }
        private double CalculateMaxFlow(Dim dim, TempSetType tempSetType, SegmentType st)
        {
            double vmax = Vmax(dim, st);

            double A = dim.CrossSectionArea;

            //Max flow rate based on velocity limit
            double Qmax_velocity_m3s = vmax * A;
            double Qmax_velocity_m3hr = Qmax_velocity_m3s * 3600; // m^3/hr

            //Max flow rate based on pressure gradient limit
            var res = FindQmaxPressure(dim, tempSetType, st, calcType);

            #region Reporting
            if (reportToConsole)
            {
                string rowName = $"{dim.DimName} {tempSetType}";
                List<double> data = new List<double>()
                {
                    vmax, dim.InnerDiameter_m, dim.CrossSectionArea, Qmax_velocity_m3hr,
                    dPdx_max(dim, st), dim.Roughness_m / dim.InnerDiameter_m,
                    rho(Temp(tempSetType, st)), mu(Temp(tempSetType, st)),
                    res.iterations, res.Re, res.Qmax,
                    Math.Min(Qmax_velocity_m3hr, res.Qmax)/3600*rho(Temp(tempSetType, st))
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
            }
            #endregion

            return Math.Min(Qmax_velocity_m3hr, res.Qmax);
        }
        private (double Qmax, int iterations, double Re) FindQmaxPressure(
            Dim dim, TempSetType tempSetType, SegmentType st, CalcType calc)
        {
            double dp_dx = dPdx_max(dim, st);
            double reynolds = 0, velocity1 = 1, velocity2 = 1.1, newVelocity = 0, error1, error2;
            double density = rho(Temp(tempSetType, st));
            double viscosity = mu(Temp(tempSetType, st));
            double tolerance = 1e-6;
            int maxIterations = 100;
            int iteration = 0;
            double relativeRoughness = dim.Roughness_m / dim.InnerDiameter_m;

            // Initialize errors for secant method
            error1 = CalculateVelocityError(velocity1, dp_dx, dim.InnerDiameter_m, relativeRoughness, density, viscosity, calc);
            error2 = CalculateVelocityError(velocity2, dp_dx, dim.InnerDiameter_m, relativeRoughness, density, viscosity, calc);

            while (iteration < maxIterations)
            {
                iteration++;

                // Secant update for velocity
                newVelocity = velocity2 - error2 * (velocity2 - velocity1) / (error2 - error1);

                // Calculate the error for the new velocity
                double newError = CalculateVelocityError(newVelocity, dp_dx, dim.InnerDiameter_m, relativeRoughness, density, viscosity, calc);

                // Check for convergence
                if (Math.Abs(newError) < tolerance)
                    break;

                // Update previous velocities and errors for the next iteration
                velocity1 = velocity2;
                velocity2 = newVelocity;
                error1 = error2;
                error2 = newError;
            }

            // Return the final flow rate (in m^3/h), iteration count, and Reynolds number
            reynolds = Reynolds(density, newVelocity, dim.InnerDiameter_m, viscosity);
            return (newVelocity * dim.CrossSectionArea * 3600, iteration, reynolds);
        }
        private double Reynolds(double density, double velocity, double diameter, double viscosity)
        {
            return density * velocity * diameter / viscosity;
            //return velocity * diameter / 0.000000365;
        }
        // Helper method to calculate the velocity error
        private double CalculateVelocityError(
            double velocity, double dp_dx, double diameter, double relativeRoughness,
            double density, double viscosity, CalcType calc)
        {
            // Calculate Reynolds number based on the current velocity
            double reynolds = Reynolds(density, velocity, diameter, viscosity);

            // Calculate the friction factor based on the selected method
            double f;
            switch (calc)
            {
                case CalcType.CW:
                    f = CalculateFrictionFactorColebrookWhite(reynolds, relativeRoughness, 1e-6);
                    break;
                case CalcType.TM:
                    f = CalculateFrictionFactorTkachenkoMileikovskyi(reynolds, relativeRoughness);
                    break;
                default:
                    throw new NotImplementedException($"{calc} is not implemented!");
            }

            // Calculate the new velocity using the Darcy-Weisbach equation
            double newVelocity = Math.Sqrt((2 * dp_dx * diameter) / (f * density));

            // Return the error as the difference between new and current velocity
            return newVelocity - velocity;
        }
        private double CalculateFrictionFactorColebrookWhite(double Re, double relativeRoughness, double tolerance)
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

            double f1 = CalculateFrictionFactorTkachenkoMileikovskyi(Re, relativeRoughness);
            double f2 = f1 + 0.05;

            for (int i = 0; i < 100; i++)
            {
                // Colebrook-White residual function g(f)
                double g1 = 1.0 / Math.Sqrt(f1) + 2.0 * Math.Log10((relativeRoughness / 3.7) + (2.51 / (Re * Math.Sqrt(f1))));
                double g2 = 1.0 / Math.Sqrt(f2) + 2.0 * Math.Log10((relativeRoughness / 3.7) + (2.51 / (Re * Math.Sqrt(f2))));

                // Update using the secant method
                double f_new = f2 - (f2 - f1) * g2 / (g2 - g1);

                // Check for convergence
                if (Math.Abs(f_new - f2) < tolerance)
                {
                    //if (currentInstance.reportToConsole)
                    //    Console.WriteLine("CW iterations: " + i);
                    return f_new;
                }

                // Update guesses
                f1 = f2;
                f2 = f_new;
            }

            log.Report("Warning: Secant method did not converge.");
            return f2;


            //return f; // Return friction factor
        }
        private double CalculateFrictionFactorTkachenkoMileikovskyi(double Re, double relativeRoughness)
        {
            //double f0inverseHalf = -0.79638 * Math.Log(relativeRoughness / 8.208 + 7.3357 / Re);
            //double f0 = Math.Pow(f0inverseHalf, -2);

            //double a1 = Re * relativeRoughness + 9.3120665 * f0inverseHalf;

            //double term1 = (8.128943 + a1) / (8.128943 * f0inverseHalf - 0.86859209 * a1 * Math.Log(a1 / (3.7099535 * Re)));
            //double f = Math.Pow(term1, 2);

            double A0 = -0.79638 * Math.Log(relativeRoughness / 8.208 + 7.3357 / Re);
            double A1 = Re * relativeRoughness + 9.3120665 * A0;
            double f = Math.Pow((8.128943 + A1) / (8.128943 * A0 - 0.86859209 * A1 * Math.Log(A1 / (3.7099535 * Re))), 2);

            return f;
        }
        private double CalculateFrictionFactorLaminar(double Re) => 64.0 / Re;
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
        private int N1(SegmentType st) =>
            st == SegmentType.Fordelingsledning ? nyttetimerOneUserFL : nyttetimerOneUserSL;
        private int N50 => nyttetimer50PlusUsersFL;
        /// <summary>
        /// Heating delta temperature
        /// </summary>
        private int dT1(SegmentType st) =>
            st == SegmentType.Fordelingsledning ? tempFremFL - tempReturFL : tempFremSL - tempReturSL;
        /// <summary>
        /// Hot water delta temperature
        /// </summary>
        private int dT2(SegmentType st) =>
            st == SegmentType.Fordelingsledning
            ? tempFremFL - hotWaterReturnTemp
            : tempFremSL - hotWaterReturnTemp;
        private int Tf(SegmentType st) =>
            st == SegmentType.Fordelingsledning ? tempFremFL : tempFremSL;
        private int Tr(SegmentType st) =>
            st == SegmentType.Fordelingsledning ? tempReturFL : tempReturSL;
        private int Tr_hw => hotWaterReturnTemp;
        private double f_b(SegmentType st) =>
            st == SegmentType.Fordelingsledning ? factorVarmtVandsTillægFL : factorVarmtVandsTillægSL;
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
        private static double volume(int temp, int deltaT) => 3600.0 / (rho(temp) * cp(temp) * deltaT);
        //private static double volume(int temp, int deltaT) => 3600.0 / (951.0 * 4.231 * deltaT);
        #endregion
        #endregion

        public CalculationResult CalculateHydraulicSegment(IHydraulicSegment segment)
        {
            #region Set calculation variables
            //Convert segmentType to enum
            SegmentType st = segment.SegmentType;
            double totalHeatingDemand = segment.HeatingDemandSupplied;
            int numberOfBuildings = segment.NumberOfBuildingsSupplied;
            int numberOfUnits = segment.NumberOfUnitsSupplied;
            //Used for restricting total pressure loss in stikledninger
            double length = segment.Length;
            #endregion

            sw.Restart();

            double s_heat = (double)N1(st) / (double)N50 + (1.0 - (double)N1(st) / (double)N50) / (double)numberOfBuildings;
            double s_hw = (51.0 - (double)numberOfUnits) / (50.0 * Math.Sqrt((double)numberOfUnits));
            s_hw = s_hw < 0 ? 0 : s_hw;

            double dimFlow1Frem = (totalHeatingDemand * 1000.0 / N1(st)) * s_heat * volume(Tf(st), dT1(st));
            double dimFlow2Frem = (totalHeatingDemand * 1000.0 / N1(st)) * s_heat * KX * volume(Tf(st), dT1(st))
                + numberOfUnits * 33 * f_b(st) * s_hw * volume(Tf(st), dT2(st));

            double dimFlow1Retur = (totalHeatingDemand * 1000.0 / N1(st)) * s_heat * volume(Tr(st), dT1(st));
            double dimFlow2Retur = (totalHeatingDemand * 1000.0 / N1(st)) * s_heat * KX * volume(Tr(st), dT1(st))
                + numberOfUnits * 33 * f_b(st) * s_hw * volume(Tr_hw, dT2(st));

            if (reportToConsole)
            {
                List<(string, List<double>)> columns = new List<(string, List<double>)>
                {
                    ("Heating demand", new List<double>()
                    {
                        totalHeatingDemand,
                        totalHeatingDemand,
                        totalHeatingDemand,
                        totalHeatingDemand
                    }),
                    ("Flow", new List<double>()
                    {
                        totalHeatingDemand / N1(st) * 1000.0/(dT1(st) * 4.231),
                        totalHeatingDemand / N1(st) * 1000.0 /(dT1(st) * 4.231),
                        totalHeatingDemand / N1(st) * 1000.0 /(dT1(st) * 4.231),
                        totalHeatingDemand / N1(st) * 1000.0 /(dT1(st) * 4.231)
                    }),
                    ("Demand ajusted", new List<double>()
                    {
                        (totalHeatingDemand * 1000 / N1(st)),
                        (totalHeatingDemand * 1000 / N1(st)),
                        (totalHeatingDemand * 1000 / N1(st)),
                        (totalHeatingDemand * 1000 / N1(st)),
                    }),
                    ("s_heat", new List<double>() { s_heat, s_heat, s_heat, s_heat }),
                    ("s_hw", new List<double>() { 0, s_hw, 0, s_hw }),
                    ("rho heat", new List<double>() { rho(Tf(st)), rho(Tf(st)), rho(Tr(st)), rho(Tr(st))}),
                    ("rho hw", new List<double>() { rho(Tf(st)), rho(Tf(st)), rho(Tr_hw), rho(Tr_hw)}),
                    ("Cp heat", new List<double>() { cp(Tf(st)), cp(Tf(st)), cp(Tr(st)), cp(Tr(st)) }),
                    ("Cp hw", new List<double>() { cp(Tf(st)), cp(Tf(st)), cp(Tr_hw), cp(Tr_hw)}),
                    ("m^3/kW heat", new List<double>() { 
                        volume(Tf(st), dT1(st)), volume(Tf(st), dT1(st)), volume(Tr(st), dT1(st)), volume(Tr(st), dT1(st)) }),
                    ("m^3/kW hw", new List<double>() { 0, volume(Tf(st), dT2(st)), 0, volume(Tr_hw, dT2(st)) }),
                    ("Flow m³/hr", new List<double>() { dimFlow1Frem, dimFlow2Frem, dimFlow1Retur, dimFlow2Retur }),
                    ("Flow kg/s", new List<double>()
                    {
                        dimFlow1Frem * rho(Tf(st)) / 3600,
                        dimFlow2Frem * rho(Tf(st)) / 3600,
                        dimFlow1Retur * rho(Tr(st)) / 3600,
                        dimFlow2Retur * rho(Tr(st)) / 3600
                        //dimFlow1Frem * 951 / 3600,
                        //dimFlow2Frem * 951 / 3600,
                        //dimFlow1Retur * 951 / 3600,
                        //dimFlow2Retur * 951 / 3600
                    })
                };

                List<string> rowNames = new List<string> { "Frem 1", "Frem 2", "Retur 1", "Retur 2" };

                log.Report(AsciiTableFormatter.CreateAsciiTableColumns(columns, rowNames, "F6"));
                log.Report();
            }

            double flowSupply = Math.Max(dimFlow1Frem, dimFlow2Frem);
            double flowReturn = Math.Max(dimFlow1Retur, dimFlow2Retur);

            var dimSupply = determineDim(flowSupply, TempSetType.Supply, st);
            var dimReturn = determineDim(flowReturn, TempSetType.Return, st);
            var dim = new[] { dimSupply, dimReturn }.MaxBy(x => x.OuterDiameter);

            (double reynolds, double gradient, double velocity) resSupply;
            (double reynolds, double gradient, double velocity) resReturn;

            #region Prevent service pipes from exceeding max allowed pressure loss
            if (st == SegmentType.Stikledning)
            {
                resSupply = CalculateGradientAndVelocity(flowSupply, dim, TempSetType.Supply, st);
                resReturn = CalculateGradientAndVelocity(flowReturn, dim, TempSetType.Return, st);

                double maxPressureLoss = maxPressureLossStikSL * 100000; // bar to Pa

                double plossSupply = resSupply.gradient * length;
                double plossReturn = resReturn.gradient * length;

                while (plossSupply + plossReturn > maxPressureLoss)
                {
                    var idx = maxFlowTableSL.FindIndex(x => x.Dim == dim);

                    dim = maxFlowTableSL[idx + 1].Dim;
                    resSupply = CalculateGradientAndVelocity(flowSupply, dim, TempSetType.Supply, st);
                    resReturn = CalculateGradientAndVelocity(flowReturn, dim, TempSetType.Return, st);

                    plossSupply = resSupply.gradient * length;
                    plossReturn = resReturn.gradient * length;
                }
            }
            else
            {
                resSupply =
                CalculateGradientAndVelocity(flowSupply, dim, TempSetType.Supply, st);
                resReturn =
                    CalculateGradientAndVelocity(flowReturn, dim, TempSetType.Return, st);
            }
            #endregion

            var utilRate = determineUtilizationRate(dim, flowSupply, flowReturn, st);

            var r = new CalculationResult(
                st.ToString(),
                dim,
                resSupply.reynolds,
                resReturn.reynolds,
                flowSupply,
                flowReturn,
                resSupply.gradient,
                resReturn.gradient,
                resSupply.velocity,
                resReturn.velocity,
                utilRate
            );

            if (reportToConsole)
            {
                //Now report these five values to console
                log.Report(
                    $"Segment type: {r.SegmentType}\n" +
                    $"Pipe type: {r.Dim.PipeType}\n" +
                    $"Dim name: {r.Dim.DimName}\n" +
                    $"Pressure gradient, supply: {r.PressureGradientSupply} Pa/m\n" +
                    $"Pressure gradient, return: {r.PressureGradientReturn} Pa/m\n" +
                    $"Velocity, supply: {r.VelocitySupply} m/s\n" +
                    $"Velocity, return: {r.VelocityReturn} m/s\n" +
                    $"Utilization rate: {utilRate}"
                    );
            }

            sw.Stop();
            if (reportToConsole)
            {
                log.Report($"Calculation time {sw.ElapsedMilliseconds} ms.");
            }

            return r;
        }
        private (double reynolds, double gradient, double velocity) CalculateGradientAndVelocity(
            double flow, Dim dim, TempSetType tst, SegmentType st)
        {
            double velocity = flow / 3600 / dim.CrossSectionArea;
            double reynolds = Reynolds(
                rho(this.Temp(tst, st)),
                velocity,
                dim.InnerDiameter_m,
                mu(this.Temp(tst, st)));

            double f;
            switch (this.calcType)
            {
                case CalcType.CW:
                    f = CalculateFrictionFactorColebrookWhite(reynolds, dim.Roughness_m / dim.InnerDiameter_m, 1e-6);
                    break;
                case CalcType.TM:
                    f = CalculateFrictionFactorTkachenkoMileikovskyi(reynolds, dim.Roughness_m / dim.InnerDiameter_m);
                    break;
                default:
                    throw new NotImplementedException();
            }

            double gradient = f * rho(this.Temp(tst, st)) * velocity * velocity / (2 * dim.InnerDiameter_m);
            //double gradient = f * 951 * velocity * velocity / (2 * dim.InnerDiameter_m);
            return (reynolds, gradient, velocity);
        }
        private Dim determineDim(double flow, TempSetType tst, SegmentType st)
        {
            switch (st)
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
        private double determineUtilizationRate(
            Dim dim, double flowSupply, double flowReturn, SegmentType st)
        {
            List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> table;

            switch (st)
            {
                case SegmentType.Fordelingsledning:
                    table = maxFlowTableFL;
                    break;
                case SegmentType.Stikledning:
                    table = maxFlowTableSL;
                    break;
                default:
                    throw new NotImplementedException();
            }

            var entry = table
                .Where(x => x.Dim.DimName == dim.DimName)
                .FirstOrDefault();

            int idx = table.IndexOf(entry);

            double minFlowFrem = idx > 0 ? table[idx - 1].MaxFlowFrem : 0;
            double minFlowReturn = idx > 0 ? table[idx - 1].MaxFlowReturn : 0;

            //Console.WriteLine(
            //    $"{(flowSupply / entry.MaxFlowFrem * 100).ToString("F2")}, " +
            //    $"{(flowReturn / entry.MaxFlowReturn * 100).ToString("F2")}");
            return Math.Max(
                (flowSupply - minFlowFrem) / (entry.MaxFlowFrem - minFlowFrem),
                (flowReturn - minFlowReturn) / (entry.MaxFlowReturn - minFlowReturn));
        }
        
        //Debug and testing
        public double f(double reynolds, double relativeRoughness, double tol)
        {
            double f = CalculateFrictionFactorColebrookWhite(reynolds, relativeRoughness, tol);
            log.Report("f: " + f);

            return f;
        }
        public double pdx(double f, double rho, double velocity, double dia)
        {
            double dpdx = f * rho * velocity * velocity / (2 * dia);
            return dpdx;
        }
    }
}