using System.Text;
using NorsynHydraulicShared;

namespace NorsynHydraulicTester.Services;

public class StepCaptureLogger : ILog
{
    private readonly StringBuilder _logBuffer = new();
    private readonly StringBuilder _reportBuffer = new();

    public IReadOnlyList<string> LogMessages => _logMessages;
    public IReadOnlyList<string> ReportMessages => _reportMessages;

    private readonly List<string> _logMessages = new();
    private readonly List<string> _reportMessages = new();

    public void Log(object obj)
    {
        var message = obj?.ToString() ?? string.Empty;
        _logMessages.Add(message);
        _logBuffer.AppendLine(message);
    }

    public void Log(string message)
    {
        _logMessages.Add(message);
        _logBuffer.AppendLine(message);
    }

    public void Report(object obj)
    {
        var message = obj?.ToString() ?? string.Empty;
        _reportMessages.Add(message);
        _reportBuffer.AppendLine(message);
    }

    public void Report(string message)
    {
        _reportMessages.Add(message);
        _reportBuffer.AppendLine(message);
    }

    public void Report()
    {
        _reportMessages.Add(string.Empty);
        _reportBuffer.AppendLine();
    }

    public string GetFullLog() => _logBuffer.ToString();
    public string GetFullReport() => _reportBuffer.ToString();

    public void Clear()
    {
        _logMessages.Clear();
        _reportMessages.Clear();
        _logBuffer.Clear();
        _reportBuffer.Clear();
    }
}
