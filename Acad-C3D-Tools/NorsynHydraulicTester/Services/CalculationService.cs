using NorsynHydraulicCalc;
using NorsynHydraulicCalc.Pipes;
using NorsynHydraulicShared;
using NorsynHydraulicTester.Models;
using L = NorsynHydraulicTester.Services.LaTeXFormatter;

namespace NorsynHydraulicTester.Services;

public class CalculationService : ICalculationService
{
    public Task<CalculationResult> CalculateClientSegmentAsync(
        TestSegment segment,
        IHydraulicSettings settings)
    {
        var result = new CalculationResult();

        try
        {
            var logger = new StepCaptureLogger();
            var calc = new HydraulicCalc(settings, logger);
            var calcResult = calc.CalculateClientSegment(segment);

            if (calcResult.Dim.DimName == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Beregningen fejlede - ingen dimension fundet.";
                return Task.FromResult(result);
            }

            BuildClientSegmentSteps(result, segment, settings, calcResult, calc);
            BuildSummaryStep(result);
#if DEBUG
            Tests.LaTeXValidatorTest.ValidateCalculationResult(result, "SL (ClientSegment)");
#endif
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return Task.FromResult(result);
    }

    public Task<CalculationResult> CalculateDistributionSegmentAsync(
        TestSegment segment,
        IHydraulicSettings settings)
    {
        var result = new CalculationResult();

        try
        {
            var logger = new StepCaptureLogger();
            var calc = new HydraulicCalc(settings, logger);
            var calcResult = calc.CalculateDistributionSegment(segment);

            if (calcResult.Dim.DimName == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Beregningen fejlede - ingen dimension fundet.";
                return Task.FromResult(result);
            }

            BuildDistributionSegmentSteps(result, segment, settings, calcResult, calc);
            BuildSummaryStep(result);
#if DEBUG
            Tests.LaTeXValidatorTest.ValidateCalculationResult(result, "FL (DistributionSegment)");
#endif
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return Task.FromResult(result);
    }

    public Task<CalculationResult> CalculateDistributionFromHeatDemandAsync(
        TestSegment segment,
        IHydraulicSettings settings)
    {
        var result = new CalculationResult();

        try
        {
            var logger = new StepCaptureLogger();
            var calc = new HydraulicCalc(settings, logger);

            double tempFrem = calc.TestingGetTempFrem();
            double tempReturVarme = calc.TestingGetTempReturVarme(segment);
            double dTHeating = calc.TestingGetDeltaT(segment, isHeating: true);
            if (dTHeating <= 0) dTHeating = 35;

            int numberOfBuildings = segment.NumberOfBuildingsConnected > 0 ? segment.NumberOfBuildingsConnected : 1;
            int numberOfUnits = segment.NumberOfUnitsConnected > 0 ? segment.NumberOfUnitsConnected : 1;
            int BN = segment.Nyttetimer > 0 ? segment.Nyttetimer : 2000;

            double totalHeatingDemand = segment.HeatingDemandConnected;

            var (s_heat, s_hw) = calc.TestingGetSimultaneityFactors(numberOfBuildings, numberOfUnits);

            double volumeFrem = calc.TestingGetVolume(tempFrem, dTHeating);
            double volumeRetur = calc.TestingGetVolume(tempReturVarme, dTHeating);

            double karFlowHeatFrem = (totalHeatingDemand * 1000.0 / BN) * volumeFrem;
            double karFlowHeatRetur = (totalHeatingDemand * 1000.0 / BN) * volumeRetur;

            double dimFlowSupply = karFlowHeatFrem * s_heat;
            double dimFlowReturn = karFlowHeatRetur * s_heat;

            var dim = calc.TestingDetermineDimForBothFlows(
                dimFlowSupply, dimFlowReturn, SegmentType.Fordelingsledning);

            var resSupply = calc.TestingCalculateGradientAndVelocity(
                dimFlowSupply, dim, TempSetType.Supply, segment);
            var resReturn = calc.TestingCalculateGradientAndVelocity(
                dimFlowReturn, dim, TempSetType.Return, segment);

            BuildDistributionFromHeatDemandSteps(
                result, segment, settings, calc,
                tempFrem, tempReturVarme, dTHeating,
                numberOfBuildings, numberOfUnits, BN, totalHeatingDemand,
                s_heat, volumeFrem, volumeRetur,
                karFlowHeatFrem, karFlowHeatRetur,
                dimFlowSupply, dimFlowReturn,
                dim, resSupply, resReturn);
            BuildSummaryStep(result);
#if DEBUG
            Tests.LaTeXValidatorTest.ValidateCalculationResult(result, "FL (HeatDemand)");
#endif
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return Task.FromResult(result);
    }

    private void BuildDistributionFromHeatDemandSteps(
        CalculationResult result,
        TestSegment segment,
        IHydraulicSettings settings,
        HydraulicCalc calc,
        double tempFrem,
        double tempReturVarme,
        double dTHeating,
        int numberOfBuildings,
        int numberOfUnits,
        int BN,
        double totalHeatingDemand,
        double s_heat,
        double volumeFrem,
        double volumeRetur,
        double karFlowHeatFrem,
        double karFlowHeatRetur,
        double dimFlowSupply,
        double dimFlowReturn,
        Dim dim,
        (double reynolds, double gradient, double velocity) resSupply,
        (double reynolds, double gradient, double velocity) resReturn)
    {
        double rhoFrem = calc.TestingGetRho(tempFrem);
        double cpFrem = calc.TestingGetCp(tempFrem);
        double muFrem = calc.TestingGetMu(tempFrem);

        double rhoRetur = calc.TestingGetRho(tempReturVarme);
        double cpRetur = calc.TestingGetCp(tempReturVarme);
        double muRetur = calc.TestingGetMu(tempReturVarme);

        result.Steps.Add(new CalculationStep
        {
            Name = "1. Medieegenskaber ved fremløbstemperatur",
            Description = $"Opslagning af densitet (ρ), varmekapacitet (cp) og dynamisk viskositet (μ) ved {tempFrem}°C fra medietabellen.",
            FormulaSymbolic = @"\rho, c_p, \mu = f(T)",
            FormulaWithValues = $@"\rho = {L.Val(rhoFrem, "F2")} \, kg/m^{{3}}",
            Inputs = { new FormulaValue("T_frem", "Fremløbstemperatur", tempFrem, "°C") }
        });
        result.Steps[^1].Results.Add(new FormulaValue("ρ", "Densitet (frem)", rhoFrem, "kg/m³"));
        result.Steps[^1].Results.Add(new FormulaValue("cp", "Varmekapacitet (frem)", cpFrem, "kJ/(kg·K)"));
        result.Steps[^1].Results.Add(new FormulaValue("μ", "Dyn. viskositet (frem)", muFrem, "Pa·s"));

        result.Steps.Add(new CalculationStep
        {
            Name = "2. Medieegenskaber ved returtemperatur",
            Description = $"Opslagning af densitet (ρ), varmekapacitet (cp) og dynamisk viskositet (μ) ved returtemperatur {tempReturVarme:F1}°C.",
            FormulaSymbolic = @"T_{retur} = T_{frem} - \Delta T",
            FormulaWithValues = $@"T_{{retur}} = {L.Val(tempFrem, "F0")} - {L.Val(dTHeating, "F0")} = {L.Val(tempReturVarme, "F1")} \, {{}}^{{\circ}}C",
            Inputs =
            {
                new FormulaValue("T_frem", "Fremløbstemperatur", tempFrem, "°C"),
                new FormulaValue("ΔT", "Afkøling", dTHeating, "°C")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("T_retur", "Returtemperatur", tempReturVarme, "°C"));
        result.Steps[^1].Results.Add(new FormulaValue("ρ", "Densitet (retur)", rhoRetur, "kg/m³"));
        result.Steps[^1].Results.Add(new FormulaValue("cp", "Varmekapacitet (retur)", cpRetur, "kJ/(kg·K)"));
        result.Steps[^1].Results.Add(new FormulaValue("μ", "Dyn. viskositet (retur)", muRetur, "Pa·s"));

        result.Steps.Add(new CalculationStep
        {
            Name = "3. Volumen per kW (frem og retur)",
            Description = "Beregner volumetrisk flow per kW varmebehov.",
            FormulaSymbolic = @"V_{kW} = \frac{3600}{\rho \cdot c_p \cdot \Delta T}",
            FormulaWithValues = $@"V_{{kW,frem}} = {L.Frac("3600", $"{L.Val(rhoFrem, "F2")} \\, kg/m^{{3}} \\cdot {L.Val(cpFrem, "F3")} \\, kJ/(kg \\cdot K) \\cdot {L.Val(dTHeating, "F1")} \\, K")} = {L.Val(volumeFrem, "F6")} \, m^{{3}}/(h \cdot kW)",
            Inputs =
            {
                new FormulaValue("ρ_frem", "Densitet (frem)", rhoFrem, "kg/m³"),
                new FormulaValue("cp_frem", "Varmekapacitet (frem)", cpFrem, "kJ/(kg·K)"),
                new FormulaValue("ρ_retur", "Densitet (retur)", rhoRetur, "kg/m³"),
                new FormulaValue("cp_retur", "Varmekapacitet (retur)", cpRetur, "kJ/(kg·K)"),
                new FormulaValue("ΔT", "Afkøling", dTHeating, "°C")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("V_kW,frem", "Volumen/kW (fremløb)", volumeFrem, "m³/(h·kW)"));
        result.Steps[^1].Results.Add(new FormulaValue("V_kW,retur", "Volumen/kW (returløb)", volumeRetur, "m³/(h·kW)"));

        int SN1 = settings.SystemnyttetimerVed1Forbruger;
        int SN50 = settings.SystemnyttetimerVed50PlusForbrugere > 0 ? settings.SystemnyttetimerVed50PlusForbrugere : 2800;

        result.Steps.Add(new CalculationStep
        {
            Name = "4. Samtidighedsfaktor for varme",
            Description = "Beregner samtidighedsfaktor for opvarmning baseret på systemnyttetimer og antal bygninger.",
            FormulaSymbolic = @"s_{heat} = \frac{SN_1}{SN_{50}} + \frac{1 - SN_1/SN_{50}}{n_{byg}}",
            FormulaWithValues = $@"s_{{heat}} = {L.Frac(SN1.ToString(), SN50.ToString())} + {L.Frac($"1 - {SN1}/{SN50}", numberOfBuildings.ToString())} = {L.Val(s_heat, "F4")}",
            Inputs =
            {
                new FormulaValue("SN₁", "Nyttetimer (1 forbr.)", SN1, "timer"),
                new FormulaValue("SN₅₀", "Nyttetimer (50+ forbr.)", SN50, "timer"),
                new FormulaValue("n_byg", "Antal bygninger", numberOfBuildings, "")
            },
            ResultValue = s_heat,
            ResultUnit = "[-]"
        });

        result.Steps.Add(new CalculationStep
        {
            Name = "5. Karakteristisk flow for varme",
            Description = "Beregner karakteristisk flow for opvarmning.",
            FormulaSymbolic = @"Q_{kar,heat} = \frac{E_{heat} \cdot 1000}{BN} \cdot V_{kW}",
            FormulaWithValues = $@"Q_{{kar,frem}} = {L.Frac($"{L.Val(totalHeatingDemand, "F1")} \\, MWh/år \\cdot 1000", $"{BN} \\, h/år")} \cdot {L.Val(volumeFrem, "F6")} \, m^{{3}}/(h \cdot kW) = {L.Val(karFlowHeatFrem, "F4")} \, m^{{3}}/h",
            Inputs =
            {
                new FormulaValue("E_heat", "Varmebehov", totalHeatingDemand, "MWh/år"),
                new FormulaValue("BN", "Bygningsnyttetimer", BN, "timer"),
                new FormulaValue("V_kW,frem", "Vol/kW (frem)", volumeFrem, "m³/(h·kW)"),
                new FormulaValue("V_kW,retur", "Vol/kW (retur)", volumeRetur, "m³/(h·kW)")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Q_kar,heat,frem", "Kar. flow varme (fremløb)", karFlowHeatFrem, "m³/h"));
        result.Steps[^1].Results.Add(new FormulaValue("Q_kar,heat,retur", "Kar. flow varme (returløb)", karFlowHeatRetur, "m³/h"));

        result.Steps.Add(new CalculationStep
        {
            Name = "6. Dimensioneringsflow",
            Description = "Karakteristisk flow multipliceret med samtidighedsfaktor.",
            FormulaSymbolic = @"Q_{dim} = Q_{kar,heat} \cdot s_{heat}",
            FormulaWithValues = $@"Q_{{dim,supply}} = {L.Val(karFlowHeatFrem, "F4")} \, m^{{3}}/h \cdot {L.Val(s_heat, "F4")} = {L.Val(dimFlowSupply, "F4")} \, m^{{3}}/h",
            Inputs =
            {
                new FormulaValue("Q_kar,heat,frem", "Kar. flow (frem)", karFlowHeatFrem, "m³/h"),
                new FormulaValue("Q_kar,heat,retur", "Kar. flow (retur)", karFlowHeatRetur, "m³/h"),
                new FormulaValue("s_heat", "Samtidighed varme", s_heat, "[-]")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,supply", "Dim. flow (fremløb)", dimFlowSupply, "m³/h"));
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,return", "Dim. flow (returløb)", dimFlowReturn, "m³/h"));

        result.Steps.Add(new CalculationStep
        {
            Name = "7. Valgt rørdimension",
            Description = $"Baseret på flowkrav vælges {dim.PipeType} {dim.DimName} med fordelingsledning-kriterier (v≤2.0 m/s, ΔP≤150 Pa/m).",
            FormulaSymbolic = @"D_i, A = f(DN)",
            FormulaWithValues = $@"D_i = {L.Val(dim.InnerDiameter_m * 1000, "F1")} \, mm \; A = {L.Val(dim.CrossSectionArea * 1e6, "F1")} \, mm^{{2}}",
            Inputs =
            {
                new FormulaValue("DN", "Nominel diameter", dim.NominalDiameter, ""),
                new FormulaValue("Q_dim,supply", "Dim. flow (frem)", dimFlowSupply, "m³/h"),
                new FormulaValue("Q_dim,return", "Dim. flow (retur)", dimFlowReturn, "m³/h")
            },
            ResultValue = dim.InnerDiameter_m * 1000,
            ResultUnit = "mm"
        });

        result.Steps.Add(new CalculationStep
        {
            Name = "8. Strømningshastighed",
            Description = "Beregner hastigheden baseret på flow og tværsnitsareal.",
            FormulaSymbolic = @"v = \frac{Q}{3600 \cdot A}",
            FormulaWithValues = $@"v_{{supply}} = {L.Frac($"{L.Val(dimFlowSupply, "F4")} \\, m^{{3}}/h", $"3600 \\, s/h \\cdot {L.Val(dim.CrossSectionArea, "F6")} \\, m^{{2}}")} = {L.Val(resSupply.velocity, "F3")} \, m/s",
            Inputs =
            {
                new FormulaValue("Q_supply", "Flow (frem)", dimFlowSupply, "m³/h"),
                new FormulaValue("Q_return", "Flow (retur)", dimFlowReturn, "m³/h"),
                new FormulaValue("A", "Tværsnitsareal", dim.CrossSectionArea, "m²")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("v_supply", "Hastighed (fremløb)", resSupply.velocity, "m/s"));
        result.Steps[^1].Results.Add(new FormulaValue("v_return", "Hastighed (returløb)", resReturn.velocity, "m/s"));

        result.Steps.Add(new CalculationStep
        {
            Name = "9. Reynolds tal",
            Description = "Dimensionsløst tal der beskriver strømningstypen. Bemærk at viskositeten er forskellig pga. temperaturforskellen.",
            FormulaSymbolic = @"Re = \frac{\rho \cdot v \cdot D}{\mu}",
            FormulaWithValues = $@"Re_{{supply}} = {L.Frac($"{L.Val(rhoFrem, "F2")} \\, kg/m^{{3}} \\cdot {L.Val(resSupply.velocity, "F3")} \\, m/s \\cdot {L.Val(dim.InnerDiameter_m, "F4")} \\, m", $"{L.Val(muFrem, "E3")} \\, Pa \\cdot s")} = {L.Val(resSupply.reynolds, "F0")}",
            Inputs =
            {
                new FormulaValue("ρ_frem", "Densitet (frem)", rhoFrem, "kg/m³"),
                new FormulaValue("ρ_retur", "Densitet (retur)", rhoRetur, "kg/m³"),
                new FormulaValue("μ_frem", "Viskositet (frem)", muFrem, "Pa·s"),
                new FormulaValue("μ_retur", "Viskositet (retur)", muRetur, "Pa·s"),
                new FormulaValue("D", "Indre diameter", dim.InnerDiameter_m, "m")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Re_supply", "Reynolds (fremløb)", resSupply.reynolds, "[-]"));
        result.Steps[^1].Results.Add(new FormulaValue("Re_return", "Reynolds (returløb)", resReturn.reynolds, "[-]"));

        double relativeRoughness = dim.Roughness_m / dim.InnerDiameter_m;
        bool isCW = settings.CalculationType == CalcType.CW;

        double fSupply, fReturn;
        List<(int iteration, double value, double error)>? iterationsSupply = null;

        if (isCW)
        {
            var (f1, iters1) = calc.TestingGetFrictionFactorCWWithIterations(resSupply.reynolds, relativeRoughness);
            var (f2, _) = calc.TestingGetFrictionFactorCWWithIterations(resReturn.reynolds, relativeRoughness);
            fSupply = f1;
            fReturn = f2;
            iterationsSupply = iters1;
        }
        else
        {
            fSupply = calc.TestingGetFrictionFactorTM(resSupply.reynolds, relativeRoughness);
            fReturn = calc.TestingGetFrictionFactorTM(resReturn.reynolds, relativeRoughness);
        }

        result.Steps.Add(new CalculationStep
        {
            Name = $"10. Friktionsfaktor ({(isCW ? "Colebrook-White" : "Tkachenko-Mileikovskyi")})",
            Description = isCW
                ? "Colebrook-White ligningen løses iterativt med sekantmetoden. Starter fra TM-approksimation."
                : "Eksplicit approksimation af friktionsfaktoren.",
            FormulaSymbolic = isCW
                ? @"\frac{1}{\sqrt{f}} = -2 \log_{10}\left(\frac{\varepsilon}{3.7 D} + \frac{2.51}{Re \sqrt{f}}\right)"
                : @"f = f(Re, \varepsilon/D)",
            FormulaWithValues = $@"f_{{supply}} = {L.Val(fSupply, "F6")} \; f_{{return}} = {L.Val(fReturn, "F6")}",
            Inputs =
            {
                new FormulaValue("Re_supply", "Reynolds (frem)", resSupply.reynolds, "[-]"),
                new FormulaValue("Re_return", "Reynolds (retur)", resReturn.reynolds, "[-]"),
                new FormulaValue("ε/D", "Relativ ruhed", relativeRoughness, "[-]")
            },
            IsIterative = isCW
        });
        result.Steps[^1].Results.Add(new FormulaValue("f_supply", "Friktion (fremløb)", fSupply, "[-]"));
        result.Steps[^1].Results.Add(new FormulaValue("f_return", "Friktion (returløb)", fReturn, "[-]"));

        if (isCW && iterationsSupply != null)
        {
            foreach (var (iteration, value, error) in iterationsSupply)
            {
                result.Steps[^1].Iterations.Add(new IterationData
                {
                    IterationNumber = iteration,
                    Value = value,
                    Error = error
                });
            }
        }

        result.Steps.Add(new CalculationStep
        {
            Name = "11. Trykgradient (Darcy-Weisbach)",
            Description = "Beregner tryktab per meter rør for begge løb.",
            FormulaSymbolic = @"\frac{dp}{dx} = f \cdot \frac{\rho \cdot v^2}{2 \cdot D}",
            FormulaWithValues = $@"\frac{{dp}}{{dx}}_{{supply}} = {L.Val(fSupply, "F6")} \cdot {L.Frac($"{L.Val(rhoFrem, "F2")} \\, kg/m^{{3}} \\cdot ({L.Val(resSupply.velocity, "F3")} \\, m/s)^{{2}}", $"2 \\cdot {L.Val(dim.InnerDiameter_m, "F4")} \\, m")} = {L.Val(resSupply.gradient, "F2")} \, Pa/m",
            Inputs =
            {
                new FormulaValue("f_supply", "Friktion (frem)", fSupply, "[-]"),
                new FormulaValue("f_return", "Friktion (retur)", fReturn, "[-]"),
                new FormulaValue("ρ_frem", "Densitet (frem)", rhoFrem, "kg/m³"),
                new FormulaValue("ρ_retur", "Densitet (retur)", rhoRetur, "kg/m³"),
                new FormulaValue("D", "Indre diameter", dim.InnerDiameter_m, "m")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("dp/dx_supply", "Gradient (fremløb)", resSupply.gradient, "Pa/m"));
        result.Steps[^1].Results.Add(new FormulaValue("dp/dx_return", "Gradient (returløb)", resReturn.gradient, "Pa/m"));

        double length = segment.Length;
        double totalPressureLoss = (resSupply.gradient + resReturn.gradient) * length;
        result.Steps.Add(new CalculationStep
        {
            Name = "12. Totalt tryktab",
            Description = "Samlet tryktab for frem- og returløb over segmentets længde.",
            FormulaSymbolic = @"\Delta p = \left(\frac{dp}{dx}_{supply} + \frac{dp}{dx}_{return}\right) \cdot L",
            FormulaWithValues = $@"\Delta p = ({L.Val(resSupply.gradient, "F2")} + {L.Val(resReturn.gradient, "F2")}) \, Pa/m \cdot {L.Val(length, "F0")} \, m = {L.Val(totalPressureLoss, "F0")} \, Pa = {L.Val(totalPressureLoss / 100000, "F4")} \, bar",
            Inputs =
            {
                new FormulaValue("dp/dx_supply", "Gradient (frem)", resSupply.gradient, "Pa/m"),
                new FormulaValue("dp/dx_return", "Gradient (retur)", resReturn.gradient, "Pa/m"),
                new FormulaValue("L", "Længde", length, "m")
            },
            ResultValue = totalPressureLoss / 100000,
            ResultUnit = "bar"
        });

        result.Steps.Add(new CalculationStep
        {
            Name = "13. Endelig dimension",
            Description = $"Den valgte rørdimension er {dim.PipeType} {dim.DimName}.",
            FormulaSymbolic = @"DN_{valgt}",
            FormulaWithValues = $@"DN_{{valgt}} = {dim.NominalDiameter} \; D_i = {L.Val(dim.InnerDiameter_m * 1000, "F1")} \, mm",
            Inputs =
            {
                new FormulaValue("v_supply", "Hastighed (frem)", resSupply.velocity, "m/s"),
                new FormulaValue("v_return", "Hastighed (retur)", resReturn.velocity, "m/s"),
                new FormulaValue("dp/dx_supply", "Gradient (frem)", resSupply.gradient, "Pa/m"),
                new FormulaValue("dp/dx_return", "Gradient (retur)", resReturn.gradient, "Pa/m")
            },
            ResultValue = dim.NominalDiameter,
            ResultUnit = "DN"
        });
    }

    private void BuildClientSegmentSteps(
        CalculationResult result,
        TestSegment segment,
        IHydraulicSettings settings,
        CalculationResultClient calcResult,
        HydraulicCalc calc)
    {
        double tempFrem = calc.TestingGetTempFrem();
        double tempReturVarme = calc.TestingGetTempReturVarme(segment);
        double tempReturBV = calc.TestingGetTempReturBV(segment);
        double dTHeating = calc.TestingGetDeltaT(segment, isHeating: true);
        double dTBV = calc.TestingGetDeltaT(segment, isHeating: false);

        if (dTHeating <= 0) dTHeating = 35;
        if (dTBV <= 0) dTBV = 35;

        int numberOfBuildings = segment.NumberOfBuildingsConnected > 0 ? segment.NumberOfBuildingsConnected : 1;
        int numberOfUnits = segment.NumberOfUnitsConnected > 0 ? segment.NumberOfUnitsConnected : 1;
        int BN = segment.Nyttetimer > 0 ? segment.Nyttetimer : 2000;

        double rhoFrem = calc.TestingGetRho(tempFrem);
        double cpFrem = calc.TestingGetCp(tempFrem);
        double muFrem = calc.TestingGetMu(tempFrem);

        double rhoRetur = calc.TestingGetRho(tempReturVarme);
        double cpRetur = calc.TestingGetCp(tempReturVarme);
        double muRetur = calc.TestingGetMu(tempReturVarme);

        result.Steps.Add(new CalculationStep
        {
            Name = "1. Medieegenskaber ved fremløbstemperatur",
            Description = $"Opslagning af densitet (ρ), varmekapacitet (cp) og dynamisk viskositet (μ) ved {tempFrem}°C fra medietabellen.",
            FormulaSymbolic = @"\rho, c_p, \mu = f(T)",
            FormulaWithValues = $@"\rho = {L.Val(rhoFrem, "F2")} \, kg/m^{{3}} \; c_p = {L.Val(cpFrem, "F4")} \, kJ/(kg \cdot K) \; \mu = {L.Val(muFrem, "E4")} \, Pa \cdot s",
            Inputs =
            {
                new FormulaValue("T_frem", "Fremløbstemperatur", tempFrem, "°C")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("ρ", "Densitet (frem)", rhoFrem, "kg/m³"));
        result.Steps[^1].Results.Add(new FormulaValue("cp", "Varmekapacitet (frem)", cpFrem, "kJ/(kg·K)"));
        result.Steps[^1].Results.Add(new FormulaValue("μ", "Dyn. viskositet (frem)", muFrem, "Pa·s"));

        result.Steps.Add(new CalculationStep
        {
            Name = "2. Medieegenskaber ved returtemperatur",
            Description = $"Opslagning af densitet (ρ), varmekapacitet (cp) og dynamisk viskositet (μ) ved returtemperatur {tempReturVarme:F1}°C (= {tempFrem} - {dTHeating}).",
            FormulaSymbolic = @"T_{retur} = T_{frem} - \Delta T",
            FormulaWithValues = $@"T_{{retur}} = {L.Val(tempFrem, "F0")} - {L.Val(dTHeating, "F0")} = {L.Val(tempReturVarme, "F1")} \, {{}}^{{\circ}}C",
            Inputs =
            {
                new FormulaValue("T_frem", "Fremløbstemperatur", tempFrem, "°C"),
                new FormulaValue("ΔT", "Afkøling", dTHeating, "°C")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("T_retur", "Returtemperatur", tempReturVarme, "°C"));
        result.Steps[^1].Results.Add(new FormulaValue("ρ", "Densitet (retur)", rhoRetur, "kg/m³"));
        result.Steps[^1].Results.Add(new FormulaValue("cp", "Varmekapacitet (retur)", cpRetur, "kJ/(kg·K)"));
        result.Steps[^1].Results.Add(new FormulaValue("μ", "Dyn. viskositet (retur)", muRetur, "Pa·s"));

        double volumeFrem = calc.TestingGetVolume(tempFrem, dTHeating);
        double volumeRetur = calc.TestingGetVolume(tempReturVarme, dTHeating);
        result.Steps.Add(new CalculationStep
        {
            Name = "3. Volumen per kW (frem og retur)",
            Description = "Beregner volumetrisk flow per kW varmebehov. Medieegenskaber afhænger af temperaturen.",
            FormulaSymbolic = @"V_{kW} = \frac{3600}{\rho \cdot c_p \cdot \Delta T}",
            FormulaWithValues = $@"V_{{kW,frem}} = {L.Frac("3600", $"{L.Val(rhoFrem, "F2")} \\, kg/m^{{3}} \\cdot {L.Val(cpFrem, "F3")} \\, kJ/(kg \\cdot K) \\cdot {L.Val(dTHeating, "F1")} \\, K")} = {L.Val(volumeFrem, "F6")} \, m^{{3}}/(h \cdot kW)",
            FormulaWithValuesReturn = $@"V_{{kW,retur}} = {L.Frac("3600", $"{L.Val(rhoRetur, "F2")} \\, kg/m^{{3}} \\cdot {L.Val(cpRetur, "F3")} \\, kJ/(kg \\cdot K) \\cdot {L.Val(dTHeating, "F1")} \\, K")} = {L.Val(volumeRetur, "F6")} \, m^{{3}}/(h \cdot kW)",
            Inputs =
            {
                new FormulaValue("ρ_frem", "Densitet (frem)", rhoFrem, "kg/m³"),
                new FormulaValue("cp_frem", "Varmekapacitet (frem)", cpFrem, "kJ/(kg·K)"),
                new FormulaValue("ρ_retur", "Densitet (retur)", rhoRetur, "kg/m³"),
                new FormulaValue("cp_retur", "Varmekapacitet (retur)", cpRetur, "kJ/(kg·K)"),
                new FormulaValue("ΔT", "Afkøling", dTHeating, "°C")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("V_kW,frem", "Volumen/kW (fremløb)", volumeFrem, "m³/(h·kW)"));
        result.Steps[^1].Results.Add(new FormulaValue("V_kW,retur", "Volumen/kW (returløb)", volumeRetur, "m³/(h·kW)"));

        int SN1 = settings.SystemnyttetimerVed1Forbruger;
        int SN50 = settings.SystemnyttetimerVed50PlusForbrugere > 0 ? settings.SystemnyttetimerVed50PlusForbrugere : 2800;

        var (s_heat, s_hw) = calc.TestingGetSimultaneityFactors(numberOfBuildings, numberOfUnits);
        result.Steps.Add(new CalculationStep
        {
            Name = "4. Samtidighedsfaktor for varme (s_heat)",
            Description = "Beregner samtidighedsfaktor for opvarmning baseret på systemnyttetimer og antal bygninger.",
            FormulaSymbolic = @"s_{heat} = \frac{SN_1}{SN_{50}} + \frac{1 - SN_1/SN_{50}}{n_{byg}}",
            FormulaWithValues = $@"s_{{heat}} = {L.Frac(SN1.ToString(), SN50.ToString())} + {L.Frac($"1 - {SN1}/{SN50}", numberOfBuildings.ToString())} = {L.Val(s_heat, "F4")}",
            Inputs =
            {
                new FormulaValue("SN₁", "Nyttetimer (1 forbr.)", SN1, "timer"),
                new FormulaValue("SN₅₀", "Nyttetimer (50+ forbr.)", SN50, "timer"),
                new FormulaValue("n_byg", "Antal bygninger", numberOfBuildings, ""),
                new FormulaValue("n_enh", "Antal enheder", numberOfUnits, "")
            },
            ResultValue = s_heat,
            ResultUnit = "[-]"
        });

        result.Steps.Add(new CalculationStep
        {
            Name = "5. Samtidighedsfaktor for brugsvand (s_hw)",
            Description = "Beregner samtidighedsfaktor for brugsvand baseret på antal enheder. Ved flere end 50 enheder bruges 0.",
            FormulaSymbolic = @"s_{hw} = \frac{51 - n_{enh}}{50 \cdot \sqrt{n_{enh}}}",
            FormulaWithValues = $@"s_{{hw}} = {L.Frac($"51 - {numberOfUnits}", $"50 \\cdot \\sqrt{{{numberOfUnits}}}")} = {L.Val(s_hw, "F4")}",
            Inputs =
            {
                new FormulaValue("n_enh", "Antal enheder", numberOfUnits, "")
            },
            ResultValue = s_hw,
            ResultUnit = "[-]"
        });

        double totalHeatingDemand = segment.HeatingDemandConnected;

        double karFlowHeatFrem = (totalHeatingDemand * 1000.0 / BN) * volumeFrem;
        double karFlowHeatRetur = (totalHeatingDemand * 1000.0 / BN) * volumeRetur;
        result.Steps.Add(new CalculationStep
        {
            Name = "6. Karakteristisk flow for varme (frem og retur)",
            Description = "Beregner karakteristisk flow for opvarmning. Flowet er forskelligt for frem- og returløb pga. temperaturafhængige medieegenskaber.",
            FormulaSymbolic = @"Q_{kar,heat} = \frac{E_{heat} \cdot 1000}{BN} \cdot V_{kW}",
            FormulaWithValues = $@"Q_{{kar,heat,frem}} = {L.Frac($"{L.Val(totalHeatingDemand, "F1")} \\, MWh/år \\cdot 1000", $"{BN} \\, h/år")} \cdot {L.Val(volumeFrem, "F6")} \, m^{{3}}/(h \cdot kW) = {L.Val(karFlowHeatFrem, "F4")} \, m^{{3}}/h",
            FormulaWithValuesReturn = $@"Q_{{kar,heat,retur}} = {L.Frac($"{L.Val(totalHeatingDemand, "F1")} \\, MWh/år \\cdot 1000", $"{BN} \\, h/år")} \cdot {L.Val(volumeRetur, "F6")} \, m^{{3}}/(h \cdot kW) = {L.Val(karFlowHeatRetur, "F4")} \, m^{{3}}/h",
            Inputs =
            {
                new FormulaValue("E_heat", "Varmebehov", totalHeatingDemand, "MWh/år"),
                new FormulaValue("BN", "Bygningsnyttetimer", BN, "timer"),
                new FormulaValue("V_kW,frem", "Vol/kW (frem)", volumeFrem, "m³/(h·kW)"),
                new FormulaValue("V_kW,retur", "Vol/kW (retur)", volumeRetur, "m³/(h·kW)")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Q_kar,heat,frem", "Kar. flow varme (fremløb)", karFlowHeatFrem, "m³/h"));
        result.Steps[^1].Results.Add(new FormulaValue("Q_kar,heat,retur", "Kar. flow varme (returløb)", karFlowHeatRetur, "m³/h"));

        double f_b = settings.FactorVarmtVandsTillæg;
        double volumeBVFrem = calc.TestingGetVolume(tempFrem, dTBV);
        double volumeBVRetur = calc.TestingGetVolume(tempReturBV, dTBV);
        double karFlowBVFrem = numberOfUnits * 33 * f_b * volumeBVFrem;
        double karFlowBVRetur = numberOfUnits * 33 * f_b * volumeBVRetur;
        result.Steps.Add(new CalculationStep
        {
            Name = "7. Karakteristisk flow for brugsvand (frem og retur)",
            Description = "Beregner karakteristisk flow for brugsvand med separate værdier for frem- og returløb.",
            FormulaSymbolic = @"Q_{kar,BV} = n_{enh} \cdot 33 \cdot f_b \cdot V_{kW,BV}",
            FormulaWithValues = $@"Q_{{kar,BV,frem}} = {numberOfUnits} \cdot 33 \, kW \cdot {L.Val(f_b, "F2")} \cdot {L.Val(volumeBVFrem, "F6")} \, m^{{3}}/(h \cdot kW) = {L.Val(karFlowBVFrem, "F4")} \, m^{{3}}/h",
            FormulaWithValuesReturn = $@"Q_{{kar,BV,retur}} = {numberOfUnits} \cdot 33 \, kW \cdot {L.Val(f_b, "F2")} \cdot {L.Val(volumeBVRetur, "F6")} \, m^{{3}}/(h \cdot kW) = {L.Val(karFlowBVRetur, "F4")} \, m^{{3}}/h",
            Inputs =
            {
                new FormulaValue("n_enh", "Antal enheder", numberOfUnits, ""),
                new FormulaValue("f_b", "BV faktor", f_b, ""),
                new FormulaValue("V_kW,BV,frem", "Vol/kW BV (frem)", volumeBVFrem, "m³/(h·kW)"),
                new FormulaValue("V_kW,BV,retur", "Vol/kW BV (retur)", volumeBVRetur, "m³/(h·kW)")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Q_kar,BV,frem", "Kar. flow BV (fremløb)", karFlowBVFrem, "m³/h"));
        result.Steps[^1].Results.Add(new FormulaValue("Q_kar,BV,retur", "Kar. flow BV (returløb)", karFlowBVRetur, "m³/h"));

        double dimFlowHeatFrem = karFlowHeatFrem * s_heat;
        double dimFlowHeatRetur = karFlowHeatRetur * s_heat;
        result.Steps.Add(new CalculationStep
        {
            Name = "8. Dimensioneringsflow for varme (frem og retur)",
            Description = "Karakteristisk flow multipliceret med samtidighedsfaktor.",
            FormulaSymbolic = @"Q_{dim,heat} = Q_{kar,heat} \cdot s_{heat}",
            FormulaWithValues = $@"Q_{{dim,heat,frem}} = {L.Val(karFlowHeatFrem, "F4")} \, m^{{3}}/h \cdot {L.Val(s_heat, "F4")} = {L.Val(dimFlowHeatFrem, "F4")} \, m^{{3}}/h",
            FormulaWithValuesReturn = $@"Q_{{dim,heat,retur}} = {L.Val(karFlowHeatRetur, "F4")} \, m^{{3}}/h \cdot {L.Val(s_heat, "F4")} = {L.Val(dimFlowHeatRetur, "F4")} \, m^{{3}}/h",
            Inputs =
            {
                new FormulaValue("Q_kar,heat,frem", "Kar. flow (frem)", karFlowHeatFrem, "m³/h"),
                new FormulaValue("Q_kar,heat,retur", "Kar. flow (retur)", karFlowHeatRetur, "m³/h"),
                new FormulaValue("s_heat", "Samtidighed varme", s_heat, "[-]")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,heat,frem", "Dim. varme (fremløb)", dimFlowHeatFrem, "m³/h"));
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,heat,retur", "Dim. varme (returløb)", dimFlowHeatRetur, "m³/h"));

        double KX = settings.FactorTillægForOpvarmningUdenBrugsvandsprioritering;
        double dimFlowBVFrem = karFlowHeatFrem * s_heat * KX + karFlowBVFrem * s_hw;
        double dimFlowBVRetur = karFlowHeatRetur * s_heat * KX + karFlowBVRetur * s_hw;
        result.Steps.Add(new CalculationStep
        {
            Name = "9. Dimensioneringsflow for brugsvand (frem og retur)",
            Description = "Kombineret flow med tillægsfaktor for opvarmning uden brugsvandsprioritering.",
            FormulaSymbolic = @"Q_{dim,BV} = Q_{kar,heat} \cdot s_{heat} \cdot K_X + Q_{kar,BV} \cdot s_{hw}",
            FormulaWithValues = $@"Q_{{dim,BV,frem}} = {L.Val(karFlowHeatFrem, "F4")} \, m^{{3}}/h \cdot {L.Val(s_heat, "F4")} \cdot {L.Val(KX, "F2")} + {L.Val(karFlowBVFrem, "F4")} \, m^{{3}}/h \cdot {L.Val(s_hw, "F4")} = {L.Val(dimFlowBVFrem, "F4")} \, m^{{3}}/h",
            FormulaWithValuesReturn = $@"Q_{{dim,BV,retur}} = {L.Val(karFlowHeatRetur, "F4")} \, m^{{3}}/h \cdot {L.Val(s_heat, "F4")} \cdot {L.Val(KX, "F2")} + {L.Val(karFlowBVRetur, "F4")} \, m^{{3}}/h \cdot {L.Val(s_hw, "F4")} = {L.Val(dimFlowBVRetur, "F4")} \, m^{{3}}/h",
            Inputs =
            {
                new FormulaValue("K_X", "Tillægsfaktor", KX, ""),
                new FormulaValue("s_heat", "Samtidighed varme", s_heat, "[-]"),
                new FormulaValue("s_hw", "Samtidighed BV", s_hw, "[-]")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,BV,frem", "Dim. BV (fremløb)", dimFlowBVFrem, "m³/h"));
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,BV,retur", "Dim. BV (returløb)", dimFlowBVRetur, "m³/h"));

        double dimFlowSupply = Math.Max(dimFlowHeatFrem, dimFlowBVFrem);
        double dimFlowReturn = Math.Max(dimFlowHeatRetur, dimFlowBVRetur);
        result.Steps.Add(new CalculationStep
        {
            Name = "10. Valg af dimensioneringsflow (frem og retur)",
            Description = "Det maksimale af varme- og brugsvandsflow bruges til dimensionering for både frem- og returløb.",
            FormulaSymbolic = @"Q_{dim} = max(Q_{dim,heat}, Q_{dim,BV})",
            FormulaWithValues = $@"Q_{{dim,supply}} = max({L.Val(dimFlowHeatFrem, "F4")} \, m^{{3}}/h, \, {L.Val(dimFlowBVFrem, "F4")} \, m^{{3}}/h) = {L.Val(dimFlowSupply, "F4")} \, m^{{3}}/h",
            Inputs =
            {
                new FormulaValue("Q_dim,heat,frem", "Dim. varme (frem)", dimFlowHeatFrem, "m³/h"),
                new FormulaValue("Q_dim,BV,frem", "Dim. BV (frem)", dimFlowBVFrem, "m³/h"),
                new FormulaValue("Q_dim,heat,retur", "Dim. varme (retur)", dimFlowHeatRetur, "m³/h"),
                new FormulaValue("Q_dim,BV,retur", "Dim. BV (retur)", dimFlowBVRetur, "m³/h")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,supply", "Dim. flow (fremløb)", dimFlowSupply, "m³/h"));
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,return", "Dim. flow (returløb)", dimFlowReturn, "m³/h"));

        var dim = calcResult.Dim;
        result.Steps.Add(new CalculationStep
        {
            Name = "11. Valgt rørdimension",
            Description = $"Baseret på flowkrav for både frem- og returløb vælges {dim.PipeType} {dim.DimName}. Dimensionen skal kunne håndtere begge flows.",
            FormulaSymbolic = @"D_i, A = f(DN)",
            FormulaWithValues = $@"DN = {dim.NominalDiameter} \; D_i = {L.Val(dim.InnerDiameter_m * 1000, "F1")} \, mm \; A = {L.Val(dim.CrossSectionArea * 1e6, "F1")} \, mm^{{2}}",
            Inputs =
            {
                new FormulaValue("DN", "Nominel diameter", dim.NominalDiameter, ""),
                new FormulaValue("Q_dim,supply", "Dim. flow (frem)", dimFlowSupply, "m³/h"),
                new FormulaValue("Q_dim,return", "Dim. flow (retur)", dimFlowReturn, "m³/h")
            },
            ResultValue = dim.InnerDiameter_m * 1000,
            ResultUnit = "mm"
        });

        double velocitySupply = dimFlowSupply / 3600 / dim.CrossSectionArea;
        double velocityReturn = dimFlowReturn / 3600 / dim.CrossSectionArea;
        result.Steps.Add(new CalculationStep
        {
            Name = "12. Strømningshastighed (frem og retur)",
            Description = "Beregner hastigheden baseret på flow og tværsnitsareal for begge løb.",
            FormulaSymbolic = @"v = \frac{Q}{3600 \cdot A}",
            FormulaWithValues = $@"v_{{supply}} = {L.Frac($"{L.Val(dimFlowSupply, "F4")} \\, m^{{3}}/h", $"3600 \\, s/h \\cdot {L.Val(dim.CrossSectionArea, "F6")} \\, m^{{2}}")} = {L.Val(velocitySupply, "F3")} \, m/s",
            FormulaWithValuesReturn = $@"v_{{return}} = {L.Frac($"{L.Val(dimFlowReturn, "F4")} \\, m^{{3}}/h", $"3600 \\, s/h \\cdot {L.Val(dim.CrossSectionArea, "F6")} \\, m^{{2}}")} = {L.Val(velocityReturn, "F3")} \, m/s",
            Inputs =
            {
                new FormulaValue("Q_supply", "Flow (frem)", dimFlowSupply, "m³/h"),
                new FormulaValue("Q_return", "Flow (retur)", dimFlowReturn, "m³/h"),
                new FormulaValue("A", "Tværsnitsareal", dim.CrossSectionArea, "m²")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("v_supply", "Hastighed (fremløb)", velocitySupply, "m/s"));
        result.Steps[^1].Results.Add(new FormulaValue("v_return", "Hastighed (returløb)", velocityReturn, "m/s"));

        double reynoldsSupply = calcResult.ReynoldsSupply;
        double reynoldsReturn = calcResult.ReynoldsReturn;
        result.Steps.Add(new CalculationStep
        {
            Name = "13. Reynolds tal (frem og retur)",
            Description = "Dimensionsløst tal der beskriver strømningstypen. Bemærk at viskositeten er forskellig pga. temperaturforskellen.",
            FormulaSymbolic = @"Re = \frac{\rho \cdot v \cdot D}{\mu}",
            FormulaWithValues = $@"Re_{{supply}} = {L.Frac($"{L.Val(rhoFrem, "F2")} \\, kg/m^{{3}} \\cdot {L.Val(velocitySupply, "F3")} \\, m/s \\cdot {L.Val(dim.InnerDiameter_m, "F4")} \\, m", $"{L.Val(muFrem, "E3")} \\, Pa \\cdot s")} = {L.Val(reynoldsSupply, "F0")}",
            FormulaWithValuesReturn = $@"Re_{{return}} = {L.Frac($"{L.Val(rhoRetur, "F2")} \\, kg/m^{{3}} \\cdot {L.Val(velocityReturn, "F3")} \\, m/s \\cdot {L.Val(dim.InnerDiameter_m, "F4")} \\, m", $"{L.Val(muRetur, "E3")} \\, Pa \\cdot s")} = {L.Val(reynoldsReturn, "F0")}",
            Inputs =
            {
                new FormulaValue("ρ_frem", "Densitet (frem)", rhoFrem, "kg/m³"),
                new FormulaValue("ρ_retur", "Densitet (retur)", rhoRetur, "kg/m³"),
                new FormulaValue("μ_frem", "Viskositet (frem)", muFrem, "Pa·s"),
                new FormulaValue("μ_retur", "Viskositet (retur)", muRetur, "Pa·s"),
                new FormulaValue("D", "Indre diameter", dim.InnerDiameter_m, "m")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Re_supply", "Reynolds (fremløb)", reynoldsSupply, "[-]"));
        result.Steps[^1].Results.Add(new FormulaValue("Re_return", "Reynolds (returløb)", reynoldsReturn, "[-]"));

        double relativeRoughness = dim.Roughness_m / dim.InnerDiameter_m;
        bool isCW = settings.CalculationType == CalcType.CW;

        double fSupply, fReturn;
        List<(int iteration, double value, double error)>? iterationsSupply = null;

        if (isCW)
        {
            var (f1, iters1) = calc.TestingGetFrictionFactorCWWithIterations(reynoldsSupply, relativeRoughness);
            var (f2, _) = calc.TestingGetFrictionFactorCWWithIterations(reynoldsReturn, relativeRoughness);
            fSupply = f1;
            fReturn = f2;
            iterationsSupply = iters1;
        }
        else
        {
            fSupply = calc.TestingGetFrictionFactorTM(reynoldsSupply, relativeRoughness);
            fReturn = calc.TestingGetFrictionFactorTM(reynoldsReturn, relativeRoughness);
        }

        result.Steps.Add(new CalculationStep
        {
            Name = $"14. Friktionsfaktor ({(isCW ? "Colebrook-White" : "Tkachenko-Mileikovskyi")})",
            Description = isCW
                ? "Colebrook-White ligningen løses iterativt med sekantmetoden. Starter fra TM-approksimation."
                : "Eksplicit approksimation af friktionsfaktoren.",
            FormulaSymbolic = isCW
                ? @"\frac{1}{\sqrt{f}} = -2 \log_{10}\left(\frac{\varepsilon}{3.7 D} + \frac{2.51}{Re \sqrt{f}}\right)"
                : @"f = f(Re, \varepsilon/D)",
            FormulaWithValues = $@"f_{{supply}} = {L.Val(fSupply, "F6")} \; f_{{return}} = {L.Val(fReturn, "F6")}",
            Inputs =
            {
                new FormulaValue("Re_supply", "Reynolds (frem)", reynoldsSupply, "[-]"),
                new FormulaValue("Re_return", "Reynolds (retur)", reynoldsReturn, "[-]"),
                new FormulaValue("ε/D", "Relativ ruhed", relativeRoughness, "[-]")
            },
            IsIterative = isCW
        });
        result.Steps[^1].Results.Add(new FormulaValue("f_supply", "Friktion (fremløb)", fSupply, "[-]"));
        result.Steps[^1].Results.Add(new FormulaValue("f_return", "Friktion (returløb)", fReturn, "[-]"));

        if (isCW && iterationsSupply != null)
        {
            foreach (var (iteration, value, error) in iterationsSupply)
            {
                result.Steps[^1].Iterations.Add(new IterationData
                {
                    IterationNumber = iteration,
                    Value = value,
                    Error = error
                });
            }
        }

        double gradientSupply = calcResult.PressureGradientSupply;
        double gradientReturn = calcResult.PressureGradientReturn;
        result.Steps.Add(new CalculationStep
        {
            Name = "15. Trykgradient (Darcy-Weisbach)",
            Description = "Beregner tryktab per meter rør for begge løb. Bemærk at densitet og hastighed er forskellige.",
            FormulaSymbolic = @"\frac{dp}{dx} = f \cdot \frac{\rho \cdot v^2}{2 \cdot D}",
            FormulaWithValues = $@"\frac{{dp}}{{dx}}_{{supply}} = {L.Val(fSupply, "F6")} \cdot {L.Frac($"{L.Val(rhoFrem, "F2")} \\, kg/m^{{3}} \\cdot ({L.Val(velocitySupply, "F3")} \\, m/s)^{{2}}", $"2 \\cdot {L.Val(dim.InnerDiameter_m, "F4")} \\, m")} = {L.Val(gradientSupply, "F2")} \, Pa/m",
            FormulaWithValuesReturn = $@"\frac{{dp}}{{dx}}_{{return}} = {L.Val(fReturn, "F6")} \cdot {L.Frac($"{L.Val(rhoRetur, "F2")} \\, kg/m^{{3}} \\cdot ({L.Val(velocityReturn, "F3")} \\, m/s)^{{2}}", $"2 \\cdot {L.Val(dim.InnerDiameter_m, "F4")} \\, m")} = {L.Val(gradientReturn, "F2")} \, Pa/m",
            Inputs =
            {
                new FormulaValue("f_supply", "Friktion (frem)", fSupply, "[-]"),
                new FormulaValue("f_return", "Friktion (retur)", fReturn, "[-]"),
                new FormulaValue("ρ_frem", "Densitet (frem)", rhoFrem, "kg/m³"),
                new FormulaValue("ρ_retur", "Densitet (retur)", rhoRetur, "kg/m³"),
                new FormulaValue("D", "Indre diameter", dim.InnerDiameter_m, "m")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("dp/dx_supply", "Gradient (fremløb)", gradientSupply, "Pa/m"));
        result.Steps[^1].Results.Add(new FormulaValue("dp/dx_return", "Gradient (returløb)", gradientReturn, "Pa/m"));

        double length = segment.Length;
        double totalPressureLoss = (gradientSupply + gradientReturn) * length;
        result.Steps.Add(new CalculationStep
        {
            Name = "16. Totalt tryktab",
            Description = "Samlet tryktab for frem- og returløb over segmentets længde.",
            FormulaSymbolic = @"\Delta p = \left(\frac{dp}{dx}_{supply} + \frac{dp}{dx}_{return}\right) \cdot L",
            FormulaWithValues = $@"\Delta p = ({L.Val(gradientSupply, "F2")} + {L.Val(gradientReturn, "F2")}) \, Pa/m \cdot {L.Val(length, "F0")} \, m = {L.Val(totalPressureLoss, "F0")} \, Pa = {L.Val(totalPressureLoss / 100000, "F4")} \, bar",
            Inputs =
            {
                new FormulaValue("dp/dx_supply", "Gradient (frem)", gradientSupply, "Pa/m"),
                new FormulaValue("dp/dx_return", "Gradient (retur)", gradientReturn, "Pa/m"),
                new FormulaValue("L", "Længde", length, "m")
            },
            ResultValue = totalPressureLoss / 100000,
            ResultUnit = "bar"
        });

        result.Steps.Add(new CalculationStep
        {
            Name = "17. Endelig dimension",
            Description = $"Den valgte rørdimension er {dim.PipeType} {dim.DimName} med en udnyttelsesgrad på {calcResult.UtilizationRate:P0}.",
            FormulaSymbolic = @"DN_{valgt}",
            FormulaWithValues = $@"DN_{{valgt}} = {dim.NominalDiameter} \; D_i = {L.Val(dim.InnerDiameter_m * 1000, "F1")} \, mm",
            Inputs =
            {
                new FormulaValue("v_supply", "Hastighed (frem)", calcResult.VelocitySupply, "m/s"),
                new FormulaValue("v_return", "Hastighed (retur)", calcResult.VelocityReturn, "m/s"),
                new FormulaValue("dp/dx_supply", "Gradient (frem)", gradientSupply, "Pa/m"),
                new FormulaValue("dp/dx_return", "Gradient (retur)", gradientReturn, "Pa/m"),
                new FormulaValue("η", "Udnyttelse", calcResult.UtilizationRate * 100, "%")
            },
            ResultValue = dim.NominalDiameter,
            ResultUnit = "DN"
        });
    }

    private void BuildDistributionSegmentSteps(
        CalculationResult result,
        TestSegment segment,
        IHydraulicSettings settings,
        CalculationResultFordeling calcResult,
        HydraulicCalc calc)
    {
        double tempFrem = calc.TestingGetTempFrem();
        double tempReturVarme = calc.TestingGetTempReturVarme(segment);

        int SN1 = settings.SystemnyttetimerVed1Forbruger;
        int SN50 = settings.SystemnyttetimerVed50PlusForbrugere > 0 ? settings.SystemnyttetimerVed50PlusForbrugere : 2800;
        int numberOfBuildings = segment.NumberOfBuildingsSupplied > 0 ? segment.NumberOfBuildingsSupplied : 1;
        int numberOfUnits = segment.NumberOfUnitsSupplied > 0 ? segment.NumberOfUnitsSupplied : 1;

        double rhoFrem = calc.TestingGetRho(tempFrem);
        double rhoRetur = calc.TestingGetRho(tempReturVarme);
        double muFrem = calc.TestingGetMu(tempFrem);
        double muRetur = calc.TestingGetMu(tempReturVarme);

        var (s_heat, s_hw) = calc.TestingGetSimultaneityFactors(numberOfBuildings, numberOfUnits);

        result.Steps.Add(new CalculationStep
        {
            Name = "1. Samtidighedsfaktor for varme (s_heat)",
            Description = "Beregner samtidighedsfaktor for opvarmning baseret på systemnyttetimer og antal bygninger.",
            FormulaSymbolic = @"s_{heat} = \frac{SN_1}{SN_{50}} + \frac{1 - SN_1/SN_{50}}{n_{byg}}",
            FormulaWithValues = $@"s_{{heat}} = {L.Frac(SN1.ToString(), SN50.ToString())} + {L.Frac($"1 - {SN1}/{SN50}", numberOfBuildings.ToString())} = {L.Val(s_heat, "F4")}",
            Inputs =
            {
                new FormulaValue("SN₁", "Nyttetimer (1 forbr.)", SN1, "timer"),
                new FormulaValue("SN₅₀", "Nyttetimer (50+ forbr.)", SN50, "timer"),
                new FormulaValue("n_byg", "Antal bygninger", numberOfBuildings, "")
            },
            ResultValue = s_heat,
            ResultUnit = "[-]"
        });

        result.Steps.Add(new CalculationStep
        {
            Name = "2. Samtidighedsfaktor for brugsvand (s_hw)",
            Description = "Beregner samtidighedsfaktor for brugsvand baseret på antal enheder. Ved flere end 50 enheder bruges 0.",
            FormulaSymbolic = @"s_{hw} = \frac{51 - n_{enh}}{50 \cdot \sqrt{n_{enh}}}",
            FormulaWithValues = $@"s_{{hw}} = {L.Frac($"51 - {numberOfUnits}", $"50 \\cdot \\sqrt{{{numberOfUnits}}}")} = {L.Val(s_hw, "F4")}",
            Inputs =
            {
                new FormulaValue("n_enh", "Antal enheder", numberOfUnits, "")
            },
            ResultValue = s_hw,
            ResultUnit = "[-]"
        });

        double dimFlowSupply = calcResult.DimFlowSupply;
        double dimFlowReturn = calcResult.DimFlowReturn;

        result.Steps.Add(new CalculationStep
        {
            Name = "3. Dimensioneringsflow (frem og retur)",
            Description = "Flow til dimensionering baseret på aggregerede downstream flows. Flowet er forskelligt for frem- og returløb.",
            FormulaSymbolic = @"Q_{dim} = Q_{kar} \cdot s",
            FormulaWithValues = $@"Q_{{dim,supply}} = {L.Val(dimFlowSupply, "F4")} \, m^{{3}}/h \; Q_{{dim,return}} = {L.Val(dimFlowReturn, "F4")} \, m^{{3}}/h",
            Inputs =
            {
                new FormulaValue("Q_kar,heat,supply", "Kar. flow varme (frem)", segment.KarFlowHeatSupply, "m³/h"),
                new FormulaValue("Q_kar,heat,return", "Kar. flow varme (retur)", segment.KarFlowHeatReturn, "m³/h"),
                new FormulaValue("Q_kar,BV,supply", "Kar. flow BV (frem)", segment.KarFlowBVSupply, "m³/h"),
                new FormulaValue("Q_kar,BV,return", "Kar. flow BV (retur)", segment.KarFlowBVReturn, "m³/h"),
                new FormulaValue("s_heat", "Samtidighed varme", s_heat, "[-]"),
                new FormulaValue("s_hw", "Samtidighed BV", s_hw, "[-]")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,supply", "Dim. flow (fremløb)", dimFlowSupply, "m³/h"));
        result.Steps[^1].Results.Add(new FormulaValue("Q_dim,return", "Dim. flow (returløb)", dimFlowReturn, "m³/h"));

        var dim = calcResult.Dim;
        result.Steps.Add(new CalculationStep
        {
            Name = "4. Valgt rørdimension",
            Description = $"Baseret på flowkrav for både frem- og returløb vælges {dim.PipeType} {dim.DimName}.",
            FormulaSymbolic = @"D_i = f(Q_{dim})",
            FormulaWithValues = $@"DN = {dim.NominalDiameter} \; D_i = {L.Val(dim.InnerDiameter_m * 1000, "F1")} \, mm",
            Inputs =
            {
                new FormulaValue("Q_dim,supply", "Dim. flow (frem)", dimFlowSupply, "m³/h"),
                new FormulaValue("Q_dim,return", "Dim. flow (retur)", dimFlowReturn, "m³/h")
            },
            ResultValue = dim.InnerDiameter_m * 1000,
            ResultUnit = "mm"
        });

        result.Steps.Add(new CalculationStep
        {
            Name = "5. Reynolds og trykgradient (frem og retur)",
            Description = "Beregning af strømningsparametre for den valgte dimension. Medieegenskaber afhænger af temperaturen.",
            FormulaSymbolic = @"Re = \frac{\rho \cdot v \cdot D}{\mu}",
            FormulaWithValues = $@"Re_{{supply}} = {L.Val(calcResult.ReynoldsSupply, "F0")} \; \frac{{dp}}{{dx}}_{{supply}} = {L.Val(calcResult.PressureGradientSupply, "F2")} \, Pa/m",
            Inputs =
            {
                new FormulaValue("Q_supply", "Flow (frem)", dimFlowSupply, "m³/h"),
                new FormulaValue("Q_return", "Flow (retur)", dimFlowReturn, "m³/h"),
                new FormulaValue("T_frem", "Temp. (frem)", tempFrem, "°C"),
                new FormulaValue("T_retur", "Temp. (retur)", tempReturVarme, "°C"),
                new FormulaValue("D", "Diameter", dim.InnerDiameter_m, "m")
            }
        });
        result.Steps[^1].Results.Add(new FormulaValue("Re_supply", "Reynolds (fremløb)", calcResult.ReynoldsSupply, "[-]"));
        result.Steps[^1].Results.Add(new FormulaValue("Re_return", "Reynolds (returløb)", calcResult.ReynoldsReturn, "[-]"));
        result.Steps[^1].Results.Add(new FormulaValue("dp/dx_supply", "Gradient (fremløb)", calcResult.PressureGradientSupply, "Pa/m"));
        result.Steps[^1].Results.Add(new FormulaValue("dp/dx_return", "Gradient (returløb)", calcResult.PressureGradientReturn, "Pa/m"));

        result.Steps.Add(new CalculationStep
        {
            Name = "6. Endelig dimension",
            Description = $"Den valgte rørdimension er {dim.PipeType} {dim.DimName}.",
            FormulaSymbolic = @"DN_{valgt}",
            FormulaWithValues = $@"DN_{{valgt}} = {dim.NominalDiameter} \; D_i = {L.Val(dim.InnerDiameter_m * 1000, "F1")} \, mm",
            Inputs =
            {
                new FormulaValue("v_supply", "Hastighed (frem)", calcResult.VelocitySupply, "m/s"),
                new FormulaValue("v_return", "Hastighed (retur)", calcResult.VelocityReturn, "m/s"),
                new FormulaValue("dp/dx_supply", "Gradient (frem)", calcResult.PressureGradientSupply, "Pa/m"),
                new FormulaValue("dp/dx_return", "Gradient (retur)", calcResult.PressureGradientReturn, "Pa/m"),
                new FormulaValue("η", "Udnyttelse", calcResult.UtilizationRate * 100, "%")
            },
            ResultValue = dim.NominalDiameter,
            ResultUnit = "DN"
        });
    }

    private void BuildSummaryStep(CalculationResult result)
    {
        var summary = new CalculationStep
        {
            Name = "Beregningsoversigt",
            Description = "Komplet beregningssekvens fra start til slut.",
            IsSummary = true
        };

        int stepNum = 0;
        foreach (var step in result.Steps)
        {
            stepNum++;
            string shortName = ExtractShortName(step.Name);

            string formula = $"{step.FormulaSymbolic} \\Rightarrow {step.FormulaWithValues}";
            string? formulaReturn = null;

            if (!string.IsNullOrEmpty(step.FormulaWithValuesReturn))
            {
                formulaReturn = step.FormulaWithValuesReturn;
            }

            summary.SummaryLines.Add(new SummaryLine
            {
                StepNumber = stepNum,
                Label = shortName,
                Formula = formula,
                FormulaReturn = formulaReturn
            });
        }

        result.Steps.Add(summary);
    }

    private static string ExtractShortName(string name)
    {
        int dotIndex = name.IndexOf('.');
        if (dotIndex >= 0 && dotIndex < name.Length - 1)
        {
            string afterDot = name[(dotIndex + 1)..].Trim();
            int parenIndex = afterDot.IndexOf('(');
            if (parenIndex > 0)
                return afterDot[..parenIndex].Trim();
            return afterDot;
        }
        return name;
    }
}
