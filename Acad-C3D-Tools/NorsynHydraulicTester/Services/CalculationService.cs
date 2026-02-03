using NorsynHydraulicCalc;
using NorsynHydraulicShared;
using NorsynHydraulicTester.Models;

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
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }

        return Task.FromResult(result);
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
            FormulaWithValues = $"Ved T = {tempFrem}°C:\nρ = {rhoFrem:F2} kg/m³\ncp = {cpFrem:F4} kJ/(kg·K)\nμ = {muFrem:E4} Pa·s",
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
            FormulaWithValues = $"T_retur = {tempFrem} - {dTHeating} = {tempReturVarme:F1}°C\nVed T = {tempReturVarme:F1}°C:\nρ = {rhoRetur:F2} kg/m³\ncp = {cpRetur:F4} kJ/(kg·K)\nμ = {muRetur:E4} Pa·s",
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
            FormulaWithValues = $"V_kW,frem = 3600 / ({rhoFrem:F2} × {cpFrem:F3} × {dTHeating:F1}) = {volumeFrem:F6}\nV_kW,retur = 3600 / ({rhoRetur:F2} × {cpRetur:F3} × {dTHeating:F1}) = {volumeRetur:F6}",
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
            FormulaWithValues = $"s_heat = {SN1}/{SN50} + (1 - {SN1}/{SN50}) / {numberOfBuildings} = {s_heat:F4}",
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
            FormulaWithValues = $"s_hw = (51 - {numberOfUnits}) / (50 × √{numberOfUnits}) = {s_hw:F4}",
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
            FormulaWithValues = $"Q_kar,heat,frem = ({totalHeatingDemand} × 1000 / {BN}) × {volumeFrem:F6} = {karFlowHeatFrem:F4}\nQ_kar,heat,retur = ({totalHeatingDemand} × 1000 / {BN}) × {volumeRetur:F6} = {karFlowHeatRetur:F4}",
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
            FormulaWithValues = $"Q_kar,BV,frem = {numberOfUnits} × 33 × {f_b} × {volumeBVFrem:F6} = {karFlowBVFrem:F4}\nQ_kar,BV,retur = {numberOfUnits} × 33 × {f_b} × {volumeBVRetur:F6} = {karFlowBVRetur:F4}",
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
            FormulaWithValues = $"Q_dim,heat,frem = {karFlowHeatFrem:F4} × {s_heat:F4} = {dimFlowHeatFrem:F4}\nQ_dim,heat,retur = {karFlowHeatRetur:F4} × {s_heat:F4} = {dimFlowHeatRetur:F4}",
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
            FormulaWithValues = $"Q_dim,BV,frem = {karFlowHeatFrem:F4} × {s_heat:F4} × {KX} + {karFlowBVFrem:F4} × {s_hw:F4} = {dimFlowBVFrem:F4}\nQ_dim,BV,retur = {karFlowHeatRetur:F4} × {s_heat:F4} × {KX} + {karFlowBVRetur:F4} × {s_hw:F4} = {dimFlowBVRetur:F4}",
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
            FormulaWithValues = $"Q_dim,supply = max({dimFlowHeatFrem:F4}, {dimFlowBVFrem:F4}) = {dimFlowSupply:F4}\nQ_dim,return = max({dimFlowHeatRetur:F4}, {dimFlowBVRetur:F4}) = {dimFlowReturn:F4}",
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
            FormulaWithValues = $"DN = {dim.NominalDiameter}, Di = {dim.InnerDiameter_m * 1000:F1} mm, A = {dim.CrossSectionArea * 1e6:F1} mm²",
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
            FormulaWithValues = $"v_supply = {dimFlowSupply:F4} / (3600 × {dim.CrossSectionArea:F6}) = {velocitySupply:F3}\nv_return = {dimFlowReturn:F4} / (3600 × {dim.CrossSectionArea:F6}) = {velocityReturn:F3}",
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
            FormulaWithValues = $"Re_supply = {rhoFrem:F2} × {velocitySupply:F3} × {dim.InnerDiameter_m:F4} / {muFrem:E3} = {reynoldsSupply:F0}\nRe_return = {rhoRetur:F2} × {velocityReturn:F3} × {dim.InnerDiameter_m:F4} / {muRetur:E3} = {reynoldsReturn:F0}",
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
            FormulaWithValues = $"f_supply (Re={reynoldsSupply:F0}) = {fSupply:F6}\nf_return (Re={reynoldsReturn:F0}) = {fReturn:F6}",
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
            FormulaWithValues = $"dp/dx_supply = {fSupply:F6} × {rhoFrem:F2} × {velocitySupply:F3}² / (2 × {dim.InnerDiameter_m:F4}) = {gradientSupply:F2}\ndp/dx_return = {fReturn:F6} × {rhoRetur:F2} × {velocityReturn:F3}² / (2 × {dim.InnerDiameter_m:F4}) = {gradientReturn:F2}",
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
            FormulaSymbolic = @"\Delta p = (dp/dx_{supply} + dp/dx_{return}) \cdot L",
            FormulaWithValues = $"Δp = ({gradientSupply:F2} + {gradientReturn:F2}) × {length} = {totalPressureLoss:F0} Pa = {totalPressureLoss / 100000:F4} bar",
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
            FormulaSymbolic = @"Resultat",
            FormulaWithValues = $"{dim.PipeType} {dim.DimName} (Di = {dim.InnerDiameter_m * 1000:F1} mm)",
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
            FormulaWithValues = $"s_heat = {SN1}/{SN50} + (1 - {SN1}/{SN50}) / {numberOfBuildings} = {s_heat:F4}",
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
            FormulaWithValues = $"s_hw = (51 - {numberOfUnits}) / (50 × √{numberOfUnits}) = {s_hw:F4}",
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
            FormulaWithValues = $"Q_dim,supply = {dimFlowSupply:F4} m³/h\nQ_dim,return = {dimFlowReturn:F4} m³/h",
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
            FormulaWithValues = $"DN = {dim.NominalDiameter}, Di = {dim.InnerDiameter_m * 1000:F1} mm",
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
            FormulaSymbolic = @"Re = \frac{\rho v D}{\mu}",
            FormulaWithValues = $"Fremløb (T={tempFrem}°C): Re = {calcResult.ReynoldsSupply:F0}, dp/dx = {calcResult.PressureGradientSupply:F2} Pa/m\nReturløb (T={tempReturVarme:F1}°C): Re = {calcResult.ReynoldsReturn:F0}, dp/dx = {calcResult.PressureGradientReturn:F2} Pa/m",
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
            FormulaSymbolic = @"Resultat",
            FormulaWithValues = $"{dim.PipeType} {dim.DimName}",
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
}
