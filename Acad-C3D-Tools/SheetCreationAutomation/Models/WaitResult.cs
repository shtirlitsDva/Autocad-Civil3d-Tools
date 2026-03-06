namespace SheetCreationAutomation.Models
{
    internal enum WaitOutcome { Completed, Cancelled, TimedOut }

    internal readonly record struct WaitResult
    {
        public WaitOutcome Outcome { get; init; }
        public string? Message { get; init; }
        public bool IsCompleted => Outcome == WaitOutcome.Completed;
        public bool IsCancelled => Outcome == WaitOutcome.Cancelled;
        public bool IsTimedOut => Outcome == WaitOutcome.TimedOut;
        public static WaitResult Completed => new() { Outcome = WaitOutcome.Completed };
        public static WaitResult Cancelled => new() { Outcome = WaitOutcome.Cancelled };
        public static WaitResult TimedOut(string message) => new() { Outcome = WaitOutcome.TimedOut, Message = message };
    }

    internal readonly record struct WaitResult<T>
    {
        public T Value { get; init; }
        public WaitOutcome Outcome { get; init; }
        public string? Message { get; init; }
        public bool IsCompleted => Outcome == WaitOutcome.Completed;
        public bool IsCancelled => Outcome == WaitOutcome.Cancelled;
        public bool IsTimedOut => Outcome == WaitOutcome.TimedOut;
        public static WaitResult<T> Of(T value) => new() { Value = value, Outcome = WaitOutcome.Completed };
        public static WaitResult<T> Cancel() => new() { Outcome = WaitOutcome.Cancelled };
        public static WaitResult<T> TimedOut(string message) => new() { Outcome = WaitOutcome.TimedOut, Message = message };
        public static WaitResult<T> From(WaitResult source) => new() { Outcome = source.Outcome, Message = source.Message };
        public WaitResult Discard() => new() { Outcome = Outcome, Message = Message };
    }
}
