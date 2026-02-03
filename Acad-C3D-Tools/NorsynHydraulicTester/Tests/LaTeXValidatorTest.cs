using System.Diagnostics;
using System.Text;
using NorsynHydraulicTester.Models;
using WpfMath.Parsers;
using L = NorsynHydraulicTester.Services.LaTeXFormatter;

namespace NorsynHydraulicTester.Tests;

public static class LaTeXValidatorTest
{
    public static (bool isValid, string? error) ValidateLaTeX(string latex)
    {
        try
        {
            var parser = WpfTeXFormulaParser.Instance;
            var formula = parser.Parse(latex);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static void ValidateCalculationResult(CalculationResult result, string resultName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\n=== Validating {resultName} LaTeX Formulas ===");

        int failed = 0;
        int stepNum = 0;
        foreach (var step in result.Steps)
        {
            stepNum++;
            var (symbolicValid, symbolicError) = ValidateLaTeX(step.FormulaSymbolic);
            var (valuesValid, valuesError) = ValidateLaTeX(step.FormulaWithValues);

            if (!symbolicValid || !valuesValid)
            {
                if (!symbolicValid)
                {
                    sb.AppendLine($"FAIL [Step {stepNum}: {step.Name}] FormulaSymbolic:");
                    sb.AppendLine($"  Formula: {step.FormulaSymbolic}");
                    sb.AppendLine($"  Error: {symbolicError}");
                    failed++;
                }
                if (!valuesValid)
                {
                    sb.AppendLine($"FAIL [Step {stepNum}: {step.Name}] FormulaWithValues (Beregning):");
                    sb.AppendLine($"  Formula: {step.FormulaWithValues}");
                    sb.AppendLine($"  Error: {valuesError}");
                    failed++;
                }
            }
            else
            {
                sb.AppendLine($"OK [Step {stepNum}: {step.Name}]");
                if (step.Name.Contains("6."))
                {
                    sb.AppendLine($"  [DEBUG Step 6 FormulaWithValues]: {step.FormulaWithValues}");
                    var step6Path = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "step6_formula.txt");
                    System.IO.File.WriteAllText(step6Path,
                        $"FormulaWithValues:\n{step.FormulaWithValues}\n\n" +
                        $"FormulaSymbolic:\n{step.FormulaSymbolic}\n\n" +
                        $"Length: {step.FormulaWithValues.Length} chars");
                    sb.AppendLine($"  [Saved to: {step6Path}]");
                }
            }
        }

        sb.AppendLine($"=== {resultName}: {result.Steps.Count - failed} passed, {failed} failed ===");
        Debug.WriteLine(sb.ToString());
    }

    public static void TestFormulas()
    {
        var testCases = new[]
        {
            @"\rho = 971.82 \, kg/m^{3}",
            @"T_{retur} = 70 - 35 = 35 \, {}^{\circ}C",
            @"V_{kW,frem} = \frac{3600}{971.82 \, kg/m^{3} \cdot 4.190 \, kJ/(kg \cdot K) \cdot 35.0 \, K} = 0.0253 \, m^{3}/(h \cdot kW)",
            @"Q_{kar,frem} = \frac{15.0 \, MWh/år \cdot 1000}{2000 \, h/år} \cdot 0.0253 \, m^{3}/(h \cdot kW) = 0.19 \, m^{3}/h",
            @"Q_{dim,supply} = 0.1900 \, m^{3}/h \cdot 0.85 = 0.1615 \, m^{3}/h",
            @"v_{supply} = \frac{0.1615 \, m^{3}/h}{3600 \, s/h \cdot 0.001380 \, m^{2}} = 0.033 \, m/s",
            @"Re_{supply} = \frac{971.82 \, kg/m^{3} \cdot 0.033 \, m/s \cdot 0.0419 \, m}{3.55 \cdot 10^{-4} \, Pa \cdot s} = 3789",
            @"\frac{dp}{dx}_{supply} = 0.0412 \cdot \frac{971.82 \, kg/m^{3} \cdot (0.033 \, m/s)^{2}}{2 \cdot 0.0419 \, m} = 0.51 \, Pa/m",
            @"\Delta p = (0.51 + 0.49) \, Pa/m \cdot 100 \, m = 100 \, Pa = 0.0010 \, bar",
            @"Q_{kar,BV,frem} = 1 \cdot 33 \, kW \cdot 1.0 \cdot 0.0253 \, m^{3}/(h \cdot kW) = 0.835 \, m^{3}/h",
            @"Q_{dim,supply} = max(0.15 \, m^{3}/h, \, 0.12 \, m^{3}/h) = 0.15 \, m^{3}/h",
            @"\rho = 971.82 \, kg/m^{3} \; c_p = 4.1900 \, kJ/(kg \cdot K) \; \mu = 3.55 \cdot 10^{-4} \, Pa \cdot s",
            @"DN = 40 \; D_i = 41.9 \, mm \; A = 1380.1 \, mm^{2}",
            @"Q_{kar,heat,frem} = \frac{15.0 \, MWh/år \cdot 1000}{2000 \, h/år} \cdot 0.025300 \, m^{3}/(h \cdot kW) = 0.1900 \, m^{3}/h",
            @"Q_{kar,heat,frem} = \frac{0.0 \, MWh/år \cdot 1000}{2000 \, h/år} \cdot 0.025300 \, m^{3}/(h \cdot kW) = 0.0000 \, m^{3}/h",
            @"test = 5.0 \, m^{3}/(h \cdot kW)",
            @"Q = \frac{- \, MWh/år \cdot 1000}{2000 \, h/år}",
            @"Q = - \, m^{3}/h",
        };

        var sb = new StringBuilder();
        sb.AppendLine("LaTeX Validation Results:");
        sb.AppendLine(new string('=', 80));

        int passed = 0, failed = 0;
        var failures = new List<string>();

        foreach (var latex in testCases)
        {
            var (isValid, error) = ValidateLaTeX(latex);
            if (isValid)
            {
                sb.AppendLine($"OK: {latex}");
                passed++;
            }
            else
            {
                sb.AppendLine($"FAIL: {latex}");
                sb.AppendLine($"      Error: {error}");
                failures.Add($"{latex}\n  -> {error}");
                failed++;
            }
        }

        // Test dynamically generated formula like SL Step 6
        double totalHeatingDemand = 15.0;
        int BN = 2000;
        double volumeFrem = 0.025300;
        double karFlowHeatFrem = 0.1900;

        var dynamicFormula = $@"Q_{{kar,heat,frem}} = {L.Frac($"{L.Val(totalHeatingDemand, "F1")} \\, MWh/år \\cdot 1000", $"{BN} \\, h/år")} \cdot {L.Val(volumeFrem, "F6")} \, m^{{3}}/(h \cdot kW) = {L.Val(karFlowHeatFrem, "F4")} \, m^{{3}}/h";

        sb.AppendLine($"DYNAMIC formula: {dynamicFormula}");
        var (dynValid, dynError) = ValidateLaTeX(dynamicFormula);
        if (dynValid)
        {
            sb.AppendLine($"DYNAMIC OK");
            passed++;
        }
        else
        {
            sb.AppendLine($"DYNAMIC FAIL: {dynError}");
            failures.Add($"DYNAMIC: {dynamicFormula}\n  -> {dynError}");
            failed++;
        }

        sb.AppendLine(new string('=', 80));
        sb.AppendLine($"Results: {passed} passed, {failed} failed");

        Debug.WriteLine(sb.ToString());

        var resultPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
            "latex_test_results.txt");
        System.IO.File.WriteAllText(resultPath, sb.ToString());
        Debug.WriteLine($"Results written to: {resultPath}");
    }
}
