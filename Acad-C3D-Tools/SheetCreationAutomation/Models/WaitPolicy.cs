using System;

namespace SheetCreationAutomation.Models
{
    internal sealed class WaitPolicy
    {
        public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(200);
        public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(1);
        public TimeSpan OverlayThreshold { get; init; } = TimeSpan.FromSeconds(5);
    }
}
