using NorsynHydraulicCalc.LookupData;
using NorsynHydraulicCalc.MaxFlowCalc;
using NorsynHydraulicCalc.Pipes;

using NorsynHydraulicShared;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NorsynHydraulicCalc
{
    public sealed class HydraulicCalc
    {
        public static Version version = new Version(20251203, 0);

        private ILog log { get; set; }

        #region Static properties for max flow pipe table
        private List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> maxFlowTableFL;
        private List<(Dim Dim, double MaxFlowFrem, double MaxFlowReturn)> maxFlowTableSL;
        private List<string> reportingColumnNames;
        private List<string> reportingUnits;
        private List<(string, List<object>)> reportingRowsFL;
        private List<(string, List<object>)> reportingRowsSL;
        #endregion

        #region Private properties
        //Settings
        private IHydraulicSettings s;
        //Pipe types
        private PipeTypes pipeTypes;
        //Lookup data
        private ILookupData ld;
        //MaxFlowCalculations
        private IMaxFlowCalc mfc;

        // Shared
        private double hotWaterReturnTemp => s.HotWaterReturnTemp; // degree
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
        private double tempFremFL => s.TempFremFL; // degree
        private double tempReturFL => s.TempReturFL; // degree
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
        private double tempFremSL => s.TempFremSL; // degree
        private double tempReturSL => s.TempReturSL; // degree
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
        public HydraulicCalc(IHydraulicSettings settings, ILog logger)
        {
            log = logger;

            s = settings;
            pipeTypes = new PipeTypes(s);
            ld = LookupDataFactory.GetLookupData(s.MedieType);
            rho = ld.rho;
            cp = ld.cp;
            mu = ld.mu;
            mfc = MaxFlowCalcFactory.GetMaxFlowCalc(s.MedieType, s);

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

                reportingRowsFL = new List<(string, List<object>)>();
                reportingRowsSL = new List<(string, List<object>)>();
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
                mfc.CalculateMaxFlowTableFL(maxFlowTableFL, CalculateMaxFlow);

                //int steelMinDn = 32;

                //if (usePertFlextraFL)
                //{
                //    foreach (var dim in pipeTypes.PertFlextra.GetDimsRange(
                //        50, pertFlextraMaxDnFL))
                //    {
                //        maxFlowTableFL.Add((dim,
                //            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Fordelingsledning),
                //            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Fordelingsledning)));
                //    }

                //    steelMinDn = translationBetweenMaxPertAndMinStål[pertFlextraMaxDnFL];
                //}

                //foreach (var dim in pipeTypes.Stål.GetDimsRange(steelMinDn, 1000))
                //{
                //    maxFlowTableFL.Add((dim,
                //            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Fordelingsledning),
                //            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Fordelingsledning)));
                //}
            }
            #endregion

            #region Populate maxFlowTableSL
            //Populate maxFlowTableSL
            {
                mfc.CalculateMaxFlowTableSL(maxFlowTableSL, CalculateMaxFlow);

                //switch (pipeTypeSL)
                //{
                //    case PipeType.Stål:
                //        throw new Exception("Stål-stikledninger er ikke tilladt!");
                //    case PipeType.PertFlextra:
                //        foreach (var dim in pipeTypes.PertFlextra.GetDimsRange(25, 75))
                //        {
                //            maxFlowTableSL.Add((dim,
                //                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                //                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                //        }
                //        break;
                //    case PipeType.AluPEX:
                //        foreach (var dim in pipeTypes.AluPex.GetDimsRange(26, 32))
                //        {
                //            maxFlowTableSL.Add((dim,
                //                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                //                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                //        }
                //        break;
                //    case PipeType.Kobber:
                //        foreach (var dim in pipeTypes.Cu.GetDimsRange(22, 28))
                //        {
                //            maxFlowTableSL.Add((dim,
                //                CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                //                CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                //        }
                //        break;
                //    default:
                //        throw new NotImplementedException($"{pipeTypeSL} not Implemented!");
                //}

                //foreach (var dim in pipeTypes.Stål.GetDimsRange(32, 1000))
                //{
                //    maxFlowTableSL.Add((dim,
                //            CalculateMaxFlow(dim, TempSetType.Supply, SegmentType.Stikledning),
                //            CalculateMaxFlow(dim, TempSetType.Return, SegmentType.Stikledning)));
                //}
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
                List<object> data = new List<object>()
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

#if DEBUG
            List<string> cns = ["Re", "rr", "tol", "f1", "f2", "g1", "g2", "f_new", "f_new - f2", "abs(f_new - f2)"];
            List<string> u = ["", "", "", "", "", "", "", "", "", ""];
            List<(string, List<object>)> vals = new();
#endif
            //Constant expression, no need to recalculate each loop
            var A = relativeRoughness / 3.7;
            for (int i = 0; i < 100; i++)
            {
                // Colebrook-White residual function g(f)
                double g1 = 1.0 / Math.Sqrt(f1) + 2.0 * Math.Log10(A + (2.51 / (Re * Math.Sqrt(f1))));
                double g2 = 1.0 / Math.Sqrt(f2) + 2.0 * Math.Log10(A + (2.51 / (Re * Math.Sqrt(f2))));

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

#if DEBUG
                if (reportToConsole)
                {
                    vals.Add((i.ToString(), [Re, relativeRoughness, tolerance, f1, f2, g1, g2, f_new, f_new - f2, Math.Abs(f_new - f2)]));
                }
#endif
            }

            log.Report("Warning: Secant method did not converge.");

#if DEBUG
            if (reportToConsole)
            {
                log.Report(
                    AsciiTableFormatter.CreateAsciiTableRows(
                        "Colebrook-White iterations", vals, cns, u, "F6"));
            }
#endif

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
        private double Temp(TempSetType tst, SegmentType st)
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
        /// Heating delta temperature.
        /// </summary>
        private double dTHeating(IHydraulicSegment s)
        {
            double delta = s.TempDeltaVarme;
            if (delta > 0) return delta;
            return s.SegmentType switch
            {
                SegmentType.Fordelingsledning => tempFremFL - tempReturFL,
                SegmentType.Stikledning => tempFremSL - tempReturSL,
                _ => throw new NotImplementedException()
            };
        }

        /// <summary>
        /// Hot water delta temperature
        /// </summary>
        private double dTBV(IHydraulicSegment s)
        {
            double delta = s.TempDeltaBV;
            if (delta > 0) return delta;
            return s.SegmentType switch
            {
                SegmentType.Fordelingsledning => tempFremFL - Tr_hw,
                SegmentType.Stikledning => tempFremSL - Tr_hw,
                _ => throw new NotImplementedException()
            };
        }
        private double Tf(SegmentType st) =>
            st == SegmentType.Fordelingsledning ? tempFremFL : tempFremSL;
        private double Tr(SegmentType st) =>
            st == SegmentType.Fordelingsledning ? tempReturFL : tempReturSL;
        private double Tr_hw => hotWaterReturnTemp;
        private double f_b(SegmentType st) =>
            st == SegmentType.Fordelingsledning ? factorVarmtVandsTillægFL : factorVarmtVandsTillægSL;
        private double KX => factorTillægForOpvarmningUdenBrugsvandsprioritering;
        #endregion

        #region Medium properties
        private static Func<double, double> rho;
        private static Func<double, double> cp;
        private static Func<double, double> mu;
        private static double volume(double temp, double deltaT) => 3600.0 / (rho(temp) * cp(temp) * deltaT);
        //private static double volume(int temp, int deltaT) => 3600.0 / (951.0 * 4.231 * deltaT);
        #endregion
        #endregion

        /// <summary>
        /// Performs hydraulic calculation for a given CLIENT segment.
        /// The calculation is based on the segment type = STIKLEDNING, heating demand,
        /// number of buildings, number of units supplied by the segment AND temperature delta (if any).
        /// If temp delta is provided, the calculation will take afkøling into account.
        /// </summary>
        public CalculationResultClient CalculateClientSegment(IHydraulicSegment segment)
        {
            #region Set calculation variables

            if (segment.SegmentType != SegmentType.Stikledning)
            {
                log.Report("Client segment calculation can only be performed for STIKLEDNING segments!");
                return new CalculationResultClient();
            }

            SegmentType st = segment.SegmentType;
            double totalHeatingDemand = segment.HeatingDemandConnected;
            int numberOfBuildings = segment.NumberOfBuildingsConnected;
            int numberOfUnits = segment.NumberOfUnitsConnected;
            //Used for restricting total pressure loss in stikledninger
            double length = segment.Length;
            #endregion

#if DEBUG
            if (reportToConsole)
            {
                if (
                    totalHeatingDemand == 0 ||
                    numberOfBuildings == 0 ||
                    numberOfUnits == 0)
                {
                    log.Report("ERROR!!! Zero values in segment!\n" +
                    $"Calculating {st} segment.\n" +
                    $"Total heating demand: {totalHeatingDemand} kW.\n" +
                    $"Number of buildings: {numberOfBuildings}.\n" +
                    $"Number of units: {numberOfUnits}.\n" +
                    $"Length: {length} m.\n");
                }
            }
#endif
            if (
                    totalHeatingDemand == 0 ||
                    numberOfBuildings == 0 ||
                    numberOfUnits == 0)
            {
                return new CalculationResultClient();
            }


            sw.Restart();

            double s_heat = (double)N1(st) / (double)N50 + (1.0 - (double)N1(st) / (double)N50) / (double)numberOfBuildings;
            double s_hw = (51.0 - (double)numberOfUnits) / (50.0 * Math.Sqrt((double)numberOfUnits));
            s_hw = s_hw < 0 ? 0 : s_hw;

            double karFlowHeatFrem = (totalHeatingDemand * 1000.0 / N1(st)) * volume(Tf(st), dTHeating(segment));
            double karFlowBVFrem = numberOfUnits * 33 * f_b(st) * volume(Tf(st), dTBV(segment));

            double dimFlowHeatFrem = karFlowHeatFrem * s_heat;
            double dimFlowBVFrem = karFlowHeatFrem * s_heat * KX + karFlowBVFrem * s_hw;

            //double dimFlow1Frem = (totalHeatingDemand * 1000.0 / N1(st)) * s_heat * volume(Tf(st), dTHeating(segment));
            //double dimFlow2Frem = (totalHeatingDemand * 1000.0 / N1(st)) * s_heat * KX * volume(Tf(st), dTHeating(segment))
            //    + numberOfUnits * 33 * f_b(st) * s_hw * volume(Tf(st), dTBV(segment));

            double karFlowHeatRetur = (totalHeatingDemand * 1000.0 / N1(st)) * volume(Tr(st), dTHeating(segment));
            double karFlowBVRetur = numberOfUnits * 33 * f_b(st) * volume(Tr_hw, dTBV(segment));

            double dimFlowHeatRetur = karFlowHeatRetur * s_heat;
            double dimFlowBVRetur = karFlowHeatRetur * s_heat * KX + karFlowBVRetur * s_hw;

            //double dimFlow1Retur = (totalHeatingDemand * 1000.0 / N1(st)) * s_heat * volume(Tr(st), dTHeating(segment));
            //double dimFlow2Retur = (totalHeatingDemand * 1000.0 / N1(st)) * s_heat * KX * volume(Tr(st), dTHeating(segment))
            //    + numberOfUnits * 33 * f_b(st) * s_hw * volume(Tr_hw, dTBV(segment));

            if (reportToConsole)
            {
                List<(string, List<object>)> columns = new List<(string, List<object>)>
                {
                    ("Heating demand", new List<object>()
                    {
                        totalHeatingDemand,
                        totalHeatingDemand,
                        totalHeatingDemand,
                        totalHeatingDemand
                    }),
                    ("Flow", new List<object>()
                    {
                        totalHeatingDemand / N1(st) * 1000.0 / (dTHeating(segment) * 4.231),
                        totalHeatingDemand / N1(st) * 1000.0 / (dTHeating(segment) * 4.231),
                        totalHeatingDemand / N1(st) * 1000.0 / (dTHeating(segment) * 4.231),
                        totalHeatingDemand / N1(st) * 1000.0 / (dTHeating(segment) * 4.231)
                    }),
                    ("Demand ajusted", new List<object>()
                    {
                        (totalHeatingDemand * 1000 / N1(st)),
                        (totalHeatingDemand * 1000 / N1(st)),
                        (totalHeatingDemand * 1000 / N1(st)),
                        (totalHeatingDemand * 1000 / N1(st)),
                    }),
                    ("s_heat", new List<object>() { s_heat, s_heat, s_heat, s_heat }),
                    ("s_hw", new List<object>() { 0, s_hw, 0, s_hw }),
                    ("rho heat", new List<object>() { rho(Tf(st)), rho(Tf(st)), rho(Tr(st)), rho(Tr(st))}),
                    ("rho hw", new List<object>() { rho(Tf(st)), rho(Tf(st)), rho(Tr_hw), rho(Tr_hw)}),
                    ("Cp heat", new List<object>() { cp(Tf(st)), cp(Tf(st)), cp(Tr(st)), cp(Tr(st)) }),
                    ("Cp hw", new List<object>() { cp(Tf(st)), cp(Tf(st)), cp(Tr_hw), cp(Tr_hw)}),
                    ("m^3/kW heat", new List<object>() {
                        volume(Tf(st), dTHeating(segment)), volume(Tf(st), dTHeating(segment)), volume(Tr(st),
                        dTHeating(segment)), volume(Tr(st), dTHeating(segment)) }),
                    ("m^3/kW hw", new List<object>() { 0, volume(Tf(st), dTBV(segment)), 0, volume(Tr_hw, dTBV(segment)) }),
                    ("Flow m³/hr", new List<object>() { dimFlowHeatFrem, dimFlowBVFrem, dimFlowHeatRetur, dimFlowBVRetur }),
                    ("Flow kg/s", new List<object>()
                    {
                        dimFlowHeatFrem * rho(Tf(st)) / 3600,
                        dimFlowBVFrem * rho(Tf(st)) / 3600,
                        dimFlowHeatRetur * rho(Tr(st)) / 3600,
                        dimFlowBVRetur * rho(Tr(st)) / 3600
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

            double dimFlowSupply = Math.Max(dimFlowHeatFrem, dimFlowBVFrem);
            double dimFlowReturn = Math.Max(dimFlowHeatRetur, dimFlowBVRetur);

            Dim dim;
            if (segment.ManualDim)
            {
                dim = segment.Dim;
            }
            else
            {
                var dimSupply = determineDim(dimFlowSupply, TempSetType.Supply, st);
                var dimReturn = determineDim(dimFlowReturn, TempSetType.Return, st);
                dim = new[] { dimSupply, dimReturn }.MaxBy(x => x.OuterDiameter);
            }

            #region Prevent service pipes from exceeding max allowed pressure loss

            (double reynolds, double gradient, double velocity) resSupply = 
                CalculateGradientAndVelocity(dimFlowSupply, dim, TempSetType.Supply, st);
            (double reynolds, double gradient, double velocity) resReturn = 
                CalculateGradientAndVelocity(dimFlowReturn, dim, TempSetType.Return, st);

            double maxPressureLoss = maxPressureLossStikSL * 100000; // bar to Pa

            double plossSupply = resSupply.gradient * length;
            double plossReturn = resReturn.gradient * length;

            while (plossSupply + plossReturn > maxPressureLoss)
            {
                var idx = maxFlowTableSL.FindIndex(x => x.Dim == dim);

                dim = maxFlowTableSL[idx + 1].Dim;
                resSupply = CalculateGradientAndVelocity(dimFlowSupply, dim, TempSetType.Supply, st);
                resReturn = CalculateGradientAndVelocity(dimFlowReturn, dim, TempSetType.Return, st);

                plossSupply = resSupply.gradient * length;
                plossReturn = resReturn.gradient * length;
            }

            #endregion

            //Utilization rate can result in negative numbers for stikledninger
            //This happens when stikledning is incremented in size to prevent exceeding max pressure loss
            //and so the calculation is done with a larger dimension than the one in the maxFlowTable
            //the larger dimension's minFlow is then larger than the calculated flow
            //and this gives negative utilization rate
            //AS UTIL RATE IS NOT USED FOR ANYTHING CURRENTLY THIS IS NOT CRITICAL
            var utilRate = determineUtilizationRate(dim, dimFlowSupply, dimFlowReturn, st);

            var r = new CalculationResultClient(
                st.ToString(),
                dim,
                resSupply.reynolds,
                resReturn.reynolds,
                karFlowHeatFrem,
                karFlowBVFrem,
                karFlowHeatRetur,
                karFlowBVRetur,
                dimFlowSupply,
                dimFlowReturn,
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

        /// <summary>
        /// Performs hydraulic calculation for a given DISTRIBUTION segment.
        /// The calculation is based on the segment type = FORDELINGSLEDNING, and
        /// calculated flows which are multiplied by the samtidighedsfaktor.        
        /// </summary>
        public CalculationResultFordeling CalculateDistributionSegment(IHydraulicSegment segment)
        {
            #region Set calculation variables

            if (segment.SegmentType != SegmentType.Fordelingsledning)
            {
                log.Report("Client segment calculation can only be performed for FORDELINGSLEDNING segments!");
                return new CalculationResultFordeling();
            }

            SegmentType st = segment.SegmentType;
            int numberOfBuildings = segment.NumberOfBuildingsSupplied;
            int numberOfUnits = segment.NumberOfUnitsSupplied;
            double karFlowHeatSupply = segment.KarFlowHeatSupply;
            double karFlowBVSupply = segment.KarFlowBVSupply;
            double karFlowHeatReturn = segment.KarFlowHeatReturn;
            double karFlowBVReturn = segment.KarFlowBVReturn;

            #endregion

#if DEBUG
            if (reportToConsole)
            {
                if (
                    numberOfBuildings == 0 ||
                    numberOfUnits == 0 ||
                    karFlowHeatSupply == 0 ||
                    karFlowBVSupply == 0 ||
                    karFlowHeatReturn == 0 ||
                    karFlowBVReturn == 0
                    )
                {
                    log.Report("ERROR!!! Zero values in segment!\n" +
                    $"Calculating {st} segment.\n" +
                    $"Number of buildings: {numberOfBuildings}.\n" +
                    $"Number of units: {numberOfUnits}.\n" +
                    $"Heat supply flow: {karFlowHeatSupply}.\n" +
                    $"BV supply flow: {karFlowBVSupply}.\n" +
                    $"Heat return flow: {karFlowHeatReturn}.\n" +
                    $"BV return flow: {karFlowBVReturn}.\n"
                    );
                }
            }
#endif
            if (
                    numberOfBuildings == 0 ||
                    numberOfUnits == 0)
            {
                return new CalculationResultFordeling();
            }

            sw.Restart();

            double s_heat = (double)N1(st) / (double)N50 + (1.0 - (double)N1(st) / (double)N50) / (double)numberOfBuildings;
            double s_hw = (51.0 - (double)numberOfUnits) / (50.0 * Math.Sqrt((double)numberOfUnits));
            s_hw = s_hw < 0 ? 0 : s_hw;

            double dimFlowHeatSupply = karFlowHeatSupply * s_heat;
            double dimFlowBVSupply = karFlowHeatSupply * s_heat * KX + karFlowBVSupply * s_hw;

            double dimFlowHeatReturn = karFlowHeatReturn * s_heat;
            double dimFlowBVReturn = karFlowHeatReturn * s_heat * KX + karFlowBVReturn * s_hw;

            if (reportToConsole)
            {
                List<(string, List<object>)> columns = new List<(string, List<object>)>
                {
                    ("s_heat", new List<object>() { s_heat, s_heat, s_heat, s_heat }),
                    ("s_hw", new List<object>() { 0, s_hw, 0, s_hw }),
                    ("rho heat", new List<object>() { rho(Tf(st)), rho(Tf(st)), rho(Tr(st)), rho(Tr(st))}),
                    ("rho hw", new List<object>() { rho(Tf(st)), rho(Tf(st)), rho(Tr_hw), rho(Tr_hw)}),
                    ("Cp heat", new List<object>() { cp(Tf(st)), cp(Tf(st)), cp(Tr(st)), cp(Tr(st)) }),
                    ("Cp hw", new List<object>() { cp(Tf(st)), cp(Tf(st)), cp(Tr_hw), cp(Tr_hw)}),
                    ("m^3/kW heat", new List<object>() {
                        volume(Tf(st), dTHeating(segment)), volume(Tf(st), dTHeating(segment)), volume(Tr(st),
                        dTHeating(segment)), volume(Tr(st), dTHeating(segment)) }),
                    ("m^3/kW hw", new List<object>() { 0, volume(Tf(st), dTBV(segment)), 0, volume(Tr_hw, dTBV(segment)) }),
                    ("Flow m³/hr", new List<object>() { dimFlowHeatSupply, dimFlowBVSupply, dimFlowHeatReturn, dimFlowBVReturn }),
                    ("Flow kg/s", new List<object>()
                    {
                        dimFlowHeatSupply * rho(Tf(st)) / 3600,
                        dimFlowBVSupply * rho(Tf(st)) / 3600,
                        dimFlowHeatReturn * rho(Tr(st)) / 3600,
                        dimFlowBVReturn * rho(Tr(st)) / 3600
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

            double dimFlowSupply = Math.Max(dimFlowHeatSupply, dimFlowBVSupply);
            double dimFlowReturn = Math.Max(dimFlowHeatReturn, dimFlowBVReturn);

            Dim dim;
            if (segment.ManualDim)
            {
                dim = segment.Dim;
            }
            else
            {
                var dimSupply = determineDim(dimFlowSupply, TempSetType.Supply, st);
                var dimReturn = determineDim(dimFlowReturn, TempSetType.Return, st);
                dim = new[] { dimSupply, dimReturn }.MaxBy(x => x.OuterDiameter);
            }

            (double reynolds, double gradient, double velocity) resSupply =
                CalculateGradientAndVelocity(dimFlowSupply, dim, TempSetType.Supply, st);
            (double reynolds, double gradient, double velocity) resReturn =
                CalculateGradientAndVelocity(dimFlowReturn, dim, TempSetType.Return, st);

            //Utilization rate can result in negative numbers for stikledninger
            //This happens when stikledning is incremented in size to prevent exceeding max pressure loss
            //and so the calculation is done with a larger dimension than the one in the maxFlowTable
            //the larger dimension's minFlow is then larger than the calculated flow
            //and this gives negative utilization rate
            //AS UTIL RATE IS NOT USED FOR ANYTHING CURRENTLY THIS IS NOT CRITICAL
            var utilRate = determineUtilizationRate(dim, dimFlowSupply, dimFlowReturn, st);

            var r = new CalculationResultFordeling(
                st.ToString(),
                dim,
                resSupply.reynolds,
                resReturn.reynolds,
                dimFlowSupply,
                dimFlowReturn,
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

#if DEBUG
            List<string> cns = ["flow", "dim", "tst", "st", "V", "rho", "mu", "Re", "f", "gradient"];
            List<string> u = ["", "", "", "", "", "", "", "", "", ""];
            List<(string, List<object>)> vals = new();
#endif

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
#if DEBUG
            if (reportToConsole)
            {
                vals.Add(("Values", [flow, dim, tst, st, velocity, rho(this.Temp(tst, st)), mu(this.Temp(tst, st)), reynolds, f, gradient]));
                log.Report(
                    AsciiTableFormatter.CreateAsciiTableRows(
                        "Gradient and velocity", vals, cns, u, "F6"));
            }
#endif
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
            var res = Math.Max(
                (flowSupply - minFlowFrem) / (entry.MaxFlowFrem - minFlowFrem),
                (flowReturn - minFlowReturn) / (entry.MaxFlowReturn - minFlowReturn));

            if (res < 0) {; }

            return res;
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