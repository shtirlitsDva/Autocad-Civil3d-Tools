using System.Globalization;

namespace NorsynHydraulicTester.Services;

public static class LaTeXFormatter
{
    public static string Val(double value, string format = "F2")
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "-";

        if (format.StartsWith("E", StringComparison.OrdinalIgnoreCase))
        {
            int decimals = format.Length > 1 ? int.Parse(format.Substring(1)) : 2;
            return FormatScientific(value, decimals);
        }

        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string FormatScientific(double value, int decimals = 2)
    {
        if (value == 0) return "0";

        int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        double mantissa = value / Math.Pow(10, exponent);

        string mantissaStr = mantissa.ToString($"F{decimals}", CultureInfo.InvariantCulture);

        if (exponent == 0)
            return mantissaStr;

        return $"{mantissaStr} \\cdot 10^{{{exponent}}}";
    }

    public static string Eq(string var, string subscript, double value, string format, string unit)
    {
        string sub = string.IsNullOrEmpty(subscript) ? "" : $"_{{{subscript}}}";
        string unitPart = string.IsNullOrEmpty(unit) || unit == "[-]" ? "" : $" \\; {Unit(unit)}";
        return $"{var}{sub} = {Val(value, format)}{unitPart}";
    }

    public static string Calc(string var, string subscript, string calculation, double result, string format, string unit)
    {
        string sub = string.IsNullOrEmpty(subscript) ? "" : $"_{{{subscript}}}";
        string unitPart = string.IsNullOrEmpty(unit) || unit == "[-]" ? "" : $" \\; {Unit(unit)}";
        return $"{var}{sub} = {calculation} = {Val(result, format)}{unitPart}";
    }

    public static string Unit(string unit)
    {
        if (string.IsNullOrWhiteSpace(unit) || unit == "[-]")
            return "";

        return unit
            .Replace("m³/h", "m^{3}/h")
            .Replace("m³/(h·kW)", "m^{3}/(h \\cdot kW)")
            .Replace("m³", "m^{3}")
            .Replace("m²", "m^{2}")
            .Replace("kg/m³", "kg/m^{3}")
            .Replace("kJ/(kg·K)", "kJ/(kg \\cdot K)")
            .Replace("Pa·s", "Pa \\cdot s")
            .Replace("·", " \\cdot ")
            .Replace("°C", "{}^{\\circ}C");
    }

    public static string Frac(string num, string den) => $"\\frac{{{num}}}{{{den}}}";

    public static string Times => " \\times ";
    public static string Cdot => " \\cdot ";

    public static string Sanitize(string latex)
    {
        if (string.IsNullOrEmpty(latex))
            return latex;

        return latex
            .Replace("år", "yr")
            .Replace("År", "Yr")
            .Replace("ø", "o")
            .Replace("Ø", "O")
            .Replace("æ", "ae")
            .Replace("Æ", "AE");
    }
}
