namespace NorsynHydraulicTester.Models;

public class CalculationResult
{
    public List<CalculationStep> Steps { get; } = new();
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CalculationStep
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FormulaSymbolic { get; set; } = string.Empty;
    public string FormulaWithValues { get; set; } = string.Empty;
    public List<FormulaValue> Inputs { get; } = new();
    public List<FormulaValue> Intermediates { get; } = new();
    public List<FormulaValue> Results { get; } = new();
    public double ResultValue { get; set; }
    public string ResultUnit { get; set; } = string.Empty;
    public bool IsIterative { get; set; }
    public List<IterationData> Iterations { get; } = new();
}

public class FormulaValue
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double? Limit { get; set; }
    public bool? PassesLimit { get; set; }

    public FormulaValue() { }

    public FormulaValue(string symbol, string name, double value, string unit)
    {
        Symbol = symbol;
        Name = name;
        Value = value;
        Unit = unit;
    }

    public FormulaValue(string symbol, string name, double value, string unit, double limit)
    {
        Symbol = symbol;
        Name = name;
        Value = value;
        Unit = unit;
        Limit = limit;
        PassesLimit = value <= limit;
    }
}

public class IterationData
{
    public int IterationNumber { get; set; }
    public double Value { get; set; }
    public double Error { get; set; }
}
