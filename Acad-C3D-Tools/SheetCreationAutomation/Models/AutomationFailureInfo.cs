using System;

namespace SheetCreationAutomation.Models
{
    internal sealed class AutomationFailureInfo
    {
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
        public string DrawingPath { get; init; } = string.Empty;
        public string StepName { get; init; } = string.Empty;
        public TimeSpan Elapsed { get; init; }
        public string Message { get; init; } = string.Empty;
        public string SelectorSnapshot { get; init; } = string.Empty;
    }
}
