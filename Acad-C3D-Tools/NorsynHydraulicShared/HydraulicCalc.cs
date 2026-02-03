using NorsynHydraulicCalc.LookupData;
using NorsynHydraulicCalc.Pipes;
using NorsynHydraulicCalc.Rules;

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

        #region Properties for reporting
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

        // Shared
        private double afkølingBrugsvand => s.AfkølingBrugsvand; // degree
        private double factorTillægForOpvarmningUdenBrugsvandsprioritering =>
            s.FactorTillægForOpvarmningUdenBrugsvandsprioritering;

        // Fordelingsledninger (Distribution pipes)
        private double tempFrem => s.TempFrem; // degree
        private double afkølingVarme => s.AfkølingVarme; // degree
        private double factorVarmtVandsTillæg => s.FactorVarmtVandsTillæg;

        // Nyttetimer (consolidated from FL/SL)
        /// <summary>
        /// System nyttetimer for 1 consumer (SN1).
        /// </summary>
        private int SN1 => s.SystemnyttetimerVed1Forbruger;
        /// <summary>
        /// System nyttetimer for 50+ consumers (SN50).
        /// </summary>
        private int SN50 => s.SystemnyttetimerVed50PlusForbrugere;
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

            log.Report($"HydraulicCalc {version}.");

            sw.Start();
            CalculateMaxFlowValues();
            sw.Stop();
            if (reportToConsole)
                log.Report($"Initialization time {sw.ElapsedMilliseconds} ms.");
        }
        #endregion

        #region Initialize the max flow values in DnAcceptCriteria
        private void CalculateMaxFlowValues()
        {
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

            // Initialize max flow values in DnAcceptCriteria for each priority
            InitializeMaxFlowForConfig(s.PipeConfigFL, SegmentType.Fordelingsledning);
            InitializeMaxFlowForConfig(s.PipeConfigSL, SegmentType.Stikledning);

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
                double temp = tempSetType == TempSetType.Supply ? tempFrem : tempFrem - afkølingVarme;

                string rowName = $"{dim.DimName} {tempSetType}";
                List<object> data = new List<object>()
                {
                    vmax, dim.InnerDiameter_m, dim.CrossSectionArea, Qmax_velocity_m3hr,
                    dPdx_max(dim, st), dim.Roughness_m / dim.InnerDiameter_m,
                    rho(temp), mu(temp),
                    res.iterations, res.Re, res.Qmax,
                    Math.Min(Qmax_velocity_m3hr, res.Qmax) / 3600 * rho(temp)
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
        /// <summary>
        /// Initializes max flow values and Dim references in DnAcceptCriteria for all priorities in a configuration.
        /// </summary>
        private void InitializeMaxFlowForConfig(PipeTypeConfiguration config, SegmentType segmentType)
        {
            if (config == null || config.Priorities.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No pipe type configuration for {segmentType}. " +
                    "Ensure PipeConfigFL/PipeConfigSL is properly initialized.");
            }

            // Process priorities in order (lowest priority number first = highest priority)
            foreach (var priority in config.Priorities.OrderBy(p => p.Priority))
            {
                // Get pipe instance to retrieve dimension data
                IPipe pipe = pipeTypes.GetPipeType(priority.PipeType);

                // Get dimensions within the configured range, ordered by DN (smallest first)
                var dims = pipe.GetDimsRange(priority.MinDn, priority.MaxDn);

                foreach (var dim in dims)
                {
                    // Find the DnAcceptCriteria for this DN
                    var criteria = priority.GetCriteriaForDn(dim.NominalDiameter);
                    if (criteria == null)
                    {
                        log.Report($"Warning: No accept criteria for {priority.PipeType} DN{dim.NominalDiameter}");
                        continue;
                    }

                    // Calculate and store max flow values
                    criteria.MaxFlowSupply = CalculateMaxFlow(dim, TempSetType.Supply, segmentType);
                    criteria.MaxFlowReturn = CalculateMaxFlow(dim, TempSetType.Return, segmentType);
                    criteria.Dim = dim;
                }
            }
        }
        private (double Qmax, int iterations, double Re) FindQmaxPressure(
            Dim dim, TempSetType tempSetType, SegmentType st, CalcType calc)
        {
            double dp_dx = dPdx_max(dim, st);
            double reynolds = 0, velocity1 = 1, velocity2 = 1.1, newVelocity = 0, error1, error2;
            double temp = tempSetType == TempSetType.Supply ? tempFrem : tempFrem - afkølingVarme;
            double density = rho(temp);
            double viscosity = mu(temp);
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

            //double f1 = CalculateFrictionFactorTkachenkoMileikovskyi(Re, relativeRoughness);
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
        /// <summary>
        /// Gets the max allowed velocity for a dimension by looking up the accept criteria
        /// from the pipe type configuration.
        /// </summary>
        private double Vmax(Dim dim, SegmentType st)
        {
            var criteria = GetAcceptCriteria(dim, st);
            if (criteria == null)
            {
                throw new InvalidOperationException(
                    $"No accept criteria found for {dim.PipeType} DN{dim.NominalDiameter} in {st}. " +
                    "Check PipeTypeConfiguration settings.");
            }
            return criteria.MaxVelocity;
        }

        /// <summary>
        /// Gets the max allowed pressure gradient for a dimension by looking up the accept criteria
        /// from the pipe type configuration.
        /// </summary>
        private double dPdx_max(Dim dim, SegmentType st)
        {
            var criteria = GetAcceptCriteria(dim, st);
            if (criteria == null)
            {
                throw new InvalidOperationException(
                    $"No accept criteria found for {dim.PipeType} DN{dim.NominalDiameter} in {st}. " +
                    "Check PipeTypeConfiguration settings.");
            }
            return criteria.MaxPressureGradient;
        }

        /// <summary>
        /// Looks up accept criteria for a dimension from the pipe type configuration.
        /// </summary>
        private DnAcceptCriteria GetAcceptCriteria(Dim dim, SegmentType st)
        {
            var config = st == SegmentType.Fordelingsledning ? s.PipeConfigFL : s.PipeConfigSL;

            if (config == null)
            {
                throw new InvalidOperationException(
                    $"PipeConfig{(st == SegmentType.Fordelingsledning ? "FL" : "SL")} is not initialized.");
            }

            // Find the priority entry that contains this pipe type
            var priority = config.GetPriorityForPipeType(dim.PipeType);
            if (priority == null)
            {
                // Pipe type not configured for this segment type
                return null;
            }

            // Get the accept criteria for this specific DN
            return priority.GetCriteriaForDn(dim.NominalDiameter);
        }
        #endregion

        #region Properties for instance segment calculations        
        private double tempReturVarme(IHydraulicSegment s) =>
            s.TempDeltaVarme > 0 ? tempFrem - s.TempDeltaVarme : tempFrem - afkølingVarme;
        private double tempReturBV(IHydraulicSegment s) =>
            s.TempDeltaBV > 0 ? tempFrem - s.TempDeltaBV : tempFrem - afkølingBrugsvand;
        /// <summary>
        /// Heating delta temperature.
        /// </summary>
        private double dTHeating(IHydraulicSegment s)
        {
            if (s.TempDeltaVarme > 0) return s.TempDeltaVarme;
            return afkølingVarme;
        }

        /// <summary>
        /// Hot water delta temperature
        /// </summary>
        private double dTBV(IHydraulicSegment s)
        {
            if (s.TempDeltaBV > 0) return s.TempDeltaBV;
            return afkølingBrugsvand;
        }
        /// <summary>
        /// Consolidated to common variable from fordeling and stikledninger.
        /// </summary>
        private double f_b(SegmentType st) => factorVarmtVandsTillæg;
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
            => CalculateClientSegment(segment, null);

        /// <summary>
        /// Performs hydraulic calculation for a given CLIENT segment with optional parent pipe type.
        /// When parentPipeType is provided, rule-based dimension selection is used.
        /// </summary>
        /// <param name="segment">The segment to calculate.</param>
        /// <param name="parentPipeType">The pipe type of the parent FL segment (for rule evaluation).</param>
        public CalculationResultClient CalculateClientSegment(IHydraulicSegment segment, PipeType? parentPipeType)
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
            // SN1 and SN50 are system nyttetimer values (consolidated)
            // BN is building nyttetimer (pre-populated on segment from config lookup)
            int BN = segment.Nyttetimer;
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

            double s_heat = (double)SN1 / (double)SN50 + (1.0 - (double)SN1 / (double)SN50) / (double)numberOfBuildings;
            double s_hw = (51.0 - (double)numberOfUnits) / (50.0 * Math.Sqrt((double)numberOfUnits));
            s_hw = s_hw < 0 ? 0 : s_hw;

            double karFlowHeatFrem = (totalHeatingDemand * 1000.0 / BN) * volume(tempFrem, dTHeating(segment));
            double karFlowBVFrem = numberOfUnits * 33 * f_b(st) * volume(tempFrem, dTBV(segment));

            double dimFlowHeatFrem = karFlowHeatFrem * s_heat;
            double dimFlowBVFrem = karFlowHeatFrem * s_heat * KX + karFlowBVFrem * s_hw;

            //double dimFlow1Frem = (totalHeatingDemand * 1000.0 / BN) * s_heat * volume(Tf(st), dTHeating(segment));
            //double dimFlow2Frem = (totalHeatingDemand * 1000.0 / BN) * s_heat * KX * volume(Tf(st), dTHeating(segment))
            //    + numberOfUnits * 33 * f_b(st) * s_hw * volume(Tf(st), dTBV(segment));

            double karFlowHeatRetur = (totalHeatingDemand * 1000.0 / BN) * volume(tempReturVarme(segment), dTHeating(segment));
            double karFlowBVRetur = numberOfUnits * 33 * f_b(st) * volume(tempReturBV(segment), dTBV(segment));

            double dimFlowHeatRetur = karFlowHeatRetur * s_heat;
            double dimFlowBVRetur = karFlowHeatRetur * s_heat * KX + karFlowBVRetur * s_hw;

            //double dimFlow1Retur = (totalHeatingDemand * 1000.0 / BN) * s_heat * volume(Tr(st), dTHeating(segment));
            //double dimFlow2Retur = (totalHeatingDemand * 1000.0 / BN) * s_heat * KX * volume(Tr(st), dTHeating(segment))
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
                        totalHeatingDemand / BN * 1000.0 / (dTHeating(segment) * 4.231),
                        totalHeatingDemand / BN * 1000.0 / (dTHeating(segment) * 4.231),
                        totalHeatingDemand / BN * 1000.0 / (dTHeating(segment) * 4.231),
                        totalHeatingDemand / BN * 1000.0 / (dTHeating(segment) * 4.231)
                    }),
                    ("Demand ajusted", new List<object>()
                    {
                        (totalHeatingDemand * 1000 / BN),
                        (totalHeatingDemand * 1000 / BN),
                        (totalHeatingDemand * 1000 / BN),
                        (totalHeatingDemand * 1000 / BN),
                    }),
                    ("s_heat", new List<object>() { s_heat, s_heat, s_heat, s_heat }),
                    ("s_hw", new List<object>() { 0, s_hw, 0, s_hw }),
                    ("rho heat", new List<object>() { rho(tempFrem), rho(tempFrem), rho(tempReturVarme(segment)), rho(tempReturVarme(segment)) }),
                    ("rho hw", new List<object>() { rho(tempFrem), rho(tempFrem), rho(tempReturBV(segment)), rho(tempReturBV(segment)) }),
                    ("Cp heat", new List<object>() { cp(tempFrem), cp(tempFrem), cp(tempReturVarme(segment)), cp(tempReturVarme(segment)) }),
                    ("Cp hw", new List<object>() { cp(tempFrem), cp(tempFrem), cp(tempReturBV(segment)), cp(tempReturBV(segment)) }),
                    ("m^3/kW heat", new List<object>() {
                        volume(tempFrem, dTHeating(segment)), volume(tempFrem, dTHeating(segment)), volume(tempReturVarme(segment),
                        dTHeating(segment)), volume(tempReturVarme(segment), dTHeating(segment)) }),
                    ("m^3/kW hw", new List<object>() { 0, volume(tempFrem, dTBV(segment)), 0, volume(tempReturBV(segment), dTBV(segment)) }),
                    ("Flow m³/hr", new List<object>() { dimFlowHeatFrem, dimFlowBVFrem, dimFlowHeatRetur, dimFlowBVRetur }),
                    ("Flow kg/s", new List<object>()
                    {
                        dimFlowHeatFrem * rho(tempFrem) / 3600,
                        dimFlowBVFrem * rho(tempFrem) / 3600,
                        dimFlowHeatRetur * rho(tempReturVarme(segment)) / 3600,
                        dimFlowBVRetur * rho(tempReturBV(segment)) / 3600
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
            else if (parentPipeType.HasValue)
            {
                // Use rule-based dimension selection when parent pipe type is known
                var context = RuleEvaluationContext.ForParent(parentPipeType.Value);
                dim = determineDimWithRules(dimFlowSupply, dimFlowReturn, context);
            }
            else
            {
                // Fallback to sequential priority selection
                var dimSupply = determineDim(dimFlowSupply, TempSetType.Supply, st);
                var dimReturn = determineDim(dimFlowReturn, TempSetType.Return, st);
                dim = new[] { dimSupply, dimReturn }.MaxBy(x => x.OuterDiameter);
            }

            #region Prevent service pipes from exceeding max allowed pressure loss

            (double reynolds, double gradient, double velocity) resSupply =
                CalculateGradientAndVelocity(dimFlowSupply, dim, TempSetType.Supply, segment);
            (double reynolds, double gradient, double velocity) resReturn =
                CalculateGradientAndVelocity(dimFlowReturn, dim, TempSetType.Return, segment);

            double maxPressureLoss = maxPressureLossStikSL * 100000; // bar to Pa

            double plossSupply = resSupply.gradient * length;
            double plossReturn = resReturn.gradient * length;

            // Build flat list of all SL dimensions for iteration
            List<Dim> allSlDims = s.PipeConfigSL.Priorities
                .OrderBy(p => p.Priority)
                .SelectMany(p => p.GetCriteriaInRange())
                .Where(c => c.IsCalculated && c.Dim != null)
                .Select(c => c.Dim)
                .OfType<Dim>()
                .ToList();

            while (plossSupply + plossReturn > maxPressureLoss)
            {
                var idx = allSlDims.FindIndex(x => x == dim);
                if (idx < 0 || idx >= allSlDims.Count - 1) break;

                dim = allSlDims[idx + 1];
                resSupply = CalculateGradientAndVelocity(dimFlowSupply, dim, TempSetType.Supply, segment);
                resReturn = CalculateGradientAndVelocity(dimFlowReturn, dim, TempSetType.Return, segment);

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

            // SN1 and SN50 are system nyttetimer values (consolidated)
            double s_heat = (double)SN1 / (double)SN50 + (1.0 - (double)SN1 / (double)SN50) / (double)numberOfBuildings;
            double s_hw = (51.0 - (double)numberOfUnits) / (50.0 * Math.Sqrt((double)numberOfUnits));
            s_hw = s_hw < 0 ? 0 : s_hw;

            double dimFlowHeatFrem = karFlowHeatSupply * s_heat;
            double dimFlowBVFrem = karFlowHeatSupply * s_heat * KX + karFlowBVSupply * s_hw;

            double dimFlowHeatReturn = karFlowHeatReturn * s_heat;
            double dimFlowBVReturn = karFlowHeatReturn * s_heat * KX + karFlowBVReturn * s_hw;

            if (reportToConsole)
            {
                List<(string, List<object>)> columns = new List<(string, List<object>)>
                {
                    ("s_heat", new List<object>() { s_heat, s_heat, s_heat, s_heat }),
                    ("s_hw", new List<object>() { 0, s_hw, 0, s_hw }),
                    ("rho heat", new List<object>() { rho(tempFrem), rho(tempFrem), rho(tempReturVarme(segment)), rho(tempReturVarme(segment)) }),
                    ("rho hw", new List<object>() { rho(tempFrem), rho(tempFrem), rho(tempReturBV(segment)), rho(tempReturBV(segment)) }),
                    ("Cp heat", new List<object>() { cp(tempFrem), cp(tempFrem), cp(tempReturVarme(segment)), cp(tempReturVarme(segment)) }),
                    ("Cp hw", new List<object>() { cp(tempFrem), cp(tempFrem), cp(tempReturBV(segment)), cp(tempReturBV(segment)) }),
                    ("m^3/kW heat", new List<object>() {
                        volume(tempFrem, dTHeating(segment)), volume(tempFrem, dTHeating(segment)), volume(tempReturVarme(segment),
                        dTHeating(segment)), volume(tempReturVarme(segment), dTHeating(segment)) }),
                    ("m^3/kW hw", new List<object>() { 0, volume(tempFrem, dTBV(segment)), 0, volume(tempReturBV(segment), dTBV(segment)) }),
                    ("Flow m³/hr", new List<object>() { dimFlowHeatFrem, dimFlowBVFrem, dimFlowHeatReturn, dimFlowBVReturn }),
                    ("Flow kg/s", new List<object>()
                    {
                        dimFlowHeatFrem * rho(tempFrem) / 3600,
                        dimFlowBVFrem * rho(tempFrem) / 3600,
                        dimFlowHeatReturn * rho(tempReturVarme(segment)) / 3600,
                        dimFlowBVReturn * rho(tempReturBV(segment)) / 3600
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
                CalculateGradientAndVelocity(dimFlowSupply, dim, TempSetType.Supply, segment);
            (double reynolds, double gradient, double velocity) resReturn =
                CalculateGradientAndVelocity(dimFlowReturn, dim, TempSetType.Return, segment);

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
            double flow, Dim dim, TempSetType tst, IHydraulicSegment s)
        {

#if DEBUG
            List<string> cns = ["flow", "dim", "tst", "st", "V", "rho", "mu", "Re", "f", "gradient"];
            List<string> u = ["", "", "", "", "", "", "", "", "", ""];
            List<(string, List<object>)> vals = new();
#endif

            double temp = tst == TempSetType.Supply ? tempFrem : tempReturVarme(s);
            double velocity = flow / 3600 / dim.CrossSectionArea;
            double reynolds = Reynolds(
                rho(temp),
                velocity,
                dim.InnerDiameter_m,
                mu(temp));

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

            double gradient = f * rho(temp) * velocity * velocity / (2 * dim.InnerDiameter_m);
            //double gradient = f * 951 * velocity * velocity / (2 * dim.InnerDiameter_m);
#if DEBUG
            if (reportToConsole)
            {
                vals.Add(("Values", [flow, dim, tst, s.SegmentType, velocity, rho(temp), mu(temp), reynolds, f, gradient]));
                log.Report(
                    AsciiTableFormatter.CreateAsciiTableRows(
                        "Gradient and velocity", vals, cns, u, "F6"));
            }
#endif
            return (reynolds, gradient, velocity);
        }
        /// <summary>
        /// Determines the pipe dimension for a given flow rate (for FL or SL without rules).
        /// Iterates through priorities in order, finding the smallest DN that can handle the flow.
        /// </summary>
        private Dim determineDim(double flow, TempSetType tst, SegmentType st)
        {
            var config = st == SegmentType.Fordelingsledning ? s.PipeConfigFL : s.PipeConfigSL;

            // Process priorities in order (lowest priority number first = highest priority)
            foreach (var priority in config.Priorities.OrderBy(p => p.Priority))
            {
                // Get criteria in range, ordered by DN (smallest first)
                foreach (var criteria in priority.GetCriteriaInRange())
                {
                    if (!criteria.IsCalculated)
                        continue;

                    double maxFlow = tst == TempSetType.Supply
                        ? criteria.MaxFlowSupply!.Value
                        : criteria.MaxFlowReturn!.Value;

                    if (flow < maxFlow)
                        return criteria.Dim!.Value;
                }
            }

            throw new Exception($"No suitable dimension found for {st} with flow {flow:F2} m³/hr!");
        }

        /// <summary>
        /// Determines the pipe dimension for SL using rules and evaluation context.
        /// Evaluates priorities with rules first - if a matching rule is found, uses that priority.
        /// If no rule matches, falls back to sequential priority order (priorities without rules).
        /// </summary>
        /// <param name="flowSupply">Supply flow rate in m³/hr.</param>
        /// <param name="flowReturn">Return flow rate in m³/hr.</param>
        /// <param name="context">Rule evaluation context containing parent pipe type info.</param>
        /// <returns>The appropriate pipe dimension.</returns>
        private Dim determineDimWithRules(double flowSupply, double flowReturn, IRuleEvaluationContext context)
        {
            var config = s.PipeConfigSL;
            double flow = Math.Max(flowSupply, flowReturn);

            // First pass: Check priorities with rules that match the context
            foreach (var priority in config.Priorities.OrderBy(p => p.Priority))
            {
                // If priority has rules, check if any match
                if (priority.Rules.Count > 0)
                {
                    bool anyRuleMatches = priority.Rules.Any(r => r.Evaluate(context));
                    if (!anyRuleMatches)
                        continue; // Rules exist but don't match - skip this priority
                }

                // Either no rules (fallback) or rules match - try to find a suitable dimension
                foreach (var criteria in priority.GetCriteriaInRange())
                {
                    if (!criteria.IsCalculated)
                        continue;

                    // Check both supply and return max flow
                    if (flowSupply < criteria.MaxFlowSupply!.Value &&
                        flowReturn < criteria.MaxFlowReturn!.Value)
                    {
                        return criteria.Dim!.Value;
                    }
                }
            }

            throw new Exception($"No suitable SL dimension found for flow {flow:F2} m³/hr with context!");
        }

        /// <summary>
        /// Calculates utilization rate for a dimension based on how much of the available flow capacity is used.
        /// </summary>
        private double determineUtilizationRate(
            Dim dim, double flowSupply, double flowReturn, SegmentType st)
        {
            var config = st == SegmentType.Fordelingsledning ? s.PipeConfigFL : s.PipeConfigSL;

            // Build a flat list of all calculated criteria in priority order for utilization calculation
            var allCriteria = config.Priorities
                .OrderBy(p => p.Priority)
                .SelectMany(p => p.GetCriteriaInRange())
                .Where(c => c.IsCalculated)
                .ToList();

            // Find the criteria for this dimension
            var entry = allCriteria.FirstOrDefault(c => c.Dim.HasValue && c.Dim.Value.DimName == dim.DimName);
            if (entry == null)
                return 0; // Dimension not found in configuration

            int idx = allCriteria.IndexOf(entry);

            double minFlowFrem = idx > 0 ? allCriteria[idx - 1].MaxFlowSupply!.Value : 0;
            double minFlowReturn = idx > 0 ? allCriteria[idx - 1].MaxFlowReturn!.Value : 0;

            var res = Math.Max(
                (flowSupply - minFlowFrem) / (entry.MaxFlowSupply!.Value - minFlowFrem),
                (flowReturn - minFlowReturn) / (entry.MaxFlowReturn!.Value - minFlowReturn));

            return res;
        }

        #region Testing API - Exposed for NorsynHydraulicTester
        public double TestingGetVolume(double temp, double deltaT) => volume(temp, deltaT);

        public double TestingGetReynolds(double density, double velocity, double diameter, double viscosity)
            => Reynolds(density, velocity, diameter, viscosity);

        public double TestingGetFrictionFactorCW(double Re, double relativeRoughness, double tolerance = 1e-6)
            => CalculateFrictionFactorColebrookWhite(Re, relativeRoughness, tolerance);

        public (double frictionFactor, List<(int iteration, double value, double error)> iterations)
            TestingGetFrictionFactorCWWithIterations(double Re, double relativeRoughness, double tolerance = 1e-6)
        {
            return CalculateFrictionFactorColebrookWhiteWithIterations(Re, relativeRoughness, tolerance);
        }

        private (double frictionFactor, List<(int iteration, double value, double error)> iterations)
            CalculateFrictionFactorColebrookWhiteWithIterations(double Re, double relativeRoughness, double tolerance)
        {
            var iterations = new List<(int iteration, double value, double error)>();

            double f1 = CalculateFrictionFactorTkachenkoMileikovskyi(Re, relativeRoughness);
            double f2 = f1 + 0.05;

            iterations.Add((0, f1, double.NaN));

            var A = relativeRoughness / 3.7;
            for (int i = 0; i < 100; i++)
            {
                double g1 = 1.0 / Math.Sqrt(f1) + 2.0 * Math.Log10(A + (2.51 / (Re * Math.Sqrt(f1))));
                double g2 = 1.0 / Math.Sqrt(f2) + 2.0 * Math.Log10(A + (2.51 / (Re * Math.Sqrt(f2))));
                double f_new = f2 - (f2 - f1) * g2 / (g2 - g1);
                double error = Math.Abs(f_new - f2);

                iterations.Add((i + 1, f_new, error));

                if (error < tolerance)
                    return (f_new, iterations);

                f1 = f2;
                f2 = f_new;
            }

            return (f2, iterations);
        }

        public double TestingGetFrictionFactorTM(double Re, double relativeRoughness)
            => CalculateFrictionFactorTkachenkoMileikovskyi(Re, relativeRoughness);

        public double TestingGetPressureGradient(double frictionFactor, double density, double velocity, double diameter)
            => frictionFactor * density * velocity * velocity / (2 * diameter);

        public (double s_heat, double s_hw) TestingGetSimultaneityFactors(int numberOfBuildings, int numberOfUnits)
        {
            double s_heat = (double)SN1 / SN50 + (1.0 - (double)SN1 / SN50) / numberOfBuildings;
            double s_hw = (51.0 - numberOfUnits) / (50.0 * Math.Sqrt(numberOfUnits));
            s_hw = s_hw < 0 ? 0 : s_hw;
            return (s_heat, s_hw);
        }

        public double TestingGetRho(double temp) => rho(temp);
        public double TestingGetCp(double temp) => cp(temp);
        public double TestingGetMu(double temp) => mu(temp);

        public double TestingGetDeltaT(IHydraulicSegment segment, bool isHeating)
            => isHeating ? dTHeating(segment) : dTBV(segment);

        public double TestingGetTempReturVarme(IHydraulicSegment segment)
            => tempReturVarme(segment);

        public double TestingGetTempReturBV(IHydraulicSegment segment)
            => tempReturBV(segment);

        public double TestingGetTempFrem() => tempFrem;

        public DnAcceptCriteria? TestingGetAcceptCriteria(Dim dim, SegmentType segmentType)
            => GetAcceptCriteria(dim, segmentType);

        public List<(string PipeType, int DN, double InnerDiameter, double MaxVelocity, int MaxPressureGradient, double? MaxFlowSupply, double? MaxFlowReturn)>
            TestingGetLookupTable(SegmentType segmentType)
        {
            var result = new List<(string, int, double, double, int, double?, double?)>();
            var config = segmentType == SegmentType.Fordelingsledning ? s.PipeConfigFL : s.PipeConfigSL;

            if (config == null) return result;

            foreach (var priority in config.Priorities.OrderBy(p => p.Priority))
            {
                IPipe pipe = pipeTypes.GetPipeType(priority.PipeType);
                var dims = pipe.GetDimsRange(priority.MinDn, priority.MaxDn);

                foreach (var dim in dims)
                {
                    var criteria = priority.GetCriteriaForDn(dim.NominalDiameter);
                    if (criteria != null)
                    {
                        result.Add((
                            priority.PipeType.ToString(),
                            dim.NominalDiameter,
                            dim.InnerDiameter_m * 1000,
                            criteria.MaxVelocity,
                            criteria.MaxPressureGradient,
                            criteria.MaxFlowSupply,
                            criteria.MaxFlowReturn
                        ));
                    }
                }
            }

            return result;
        }

        public Dim TestingDetermineDim(double flow, TempSetType tst, SegmentType st)
            => determineDim(flow, tst, st);

        public Dim TestingDetermineDimForBothFlows(double flowSupply, double flowReturn, SegmentType st)
        {
            var dimSupply = determineDim(flowSupply, TempSetType.Supply, st);
            var dimReturn = determineDim(flowReturn, TempSetType.Return, st);
            return new[] { dimSupply, dimReturn }.MaxBy(x => x.OuterDiameter);
        }

        public (double reynolds, double gradient, double velocity) TestingCalculateGradientAndVelocity(
            double flow, Dim dim, TempSetType tst, IHydraulicSegment segment)
            => CalculateGradientAndVelocity(flow, dim, tst, segment);
        #endregion

        //Debug and testing (legacy)
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