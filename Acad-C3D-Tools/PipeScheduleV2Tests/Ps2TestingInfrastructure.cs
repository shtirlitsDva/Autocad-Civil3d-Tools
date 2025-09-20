using System;

namespace PipeScheduleV2Tests
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class Ps2TestAttribute : Attribute { }

    internal sealed class Ps2SkipException : Exception
    {
        public Ps2SkipException(string message) : base(message) { }
    }

    internal enum Ps2Status
    {
        Passed,
        Failed,
        Error,
        Skipped,
    }

    internal sealed class Ps2Result
    {
        public string Name { get; set; } = string.Empty;
        public Ps2Status Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public string StackTrace { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
    }
}
