namespace SheetCreationAutomation.Models
{
    internal readonly record struct WaitResult
    {
        public bool IsCompleted { get; init; }
        public bool IsCancelled => !IsCompleted;
        public static WaitResult Completed => new() { IsCompleted = true };
        public static WaitResult Cancelled => new() { IsCompleted = false };
    }

    internal readonly record struct WaitResult<T>
    {
        public T Value { get; init; }
        public bool IsCompleted { get; init; }
        public bool IsCancelled => !IsCompleted;
        public static WaitResult<T> Of(T value) => new() { Value = value, IsCompleted = true };
        public static WaitResult<T> Cancel() => new() { IsCompleted = false };
    }
}
