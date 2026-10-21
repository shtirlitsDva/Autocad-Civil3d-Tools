using System.Collections.Immutable;

namespace IntersectUtilities.LerCompare;

// Expected absence is explicit; Match requires both cases at every call site.
public readonly struct CompareOption<T> where T : notnull
{
    private readonly ImmutableArray<T> values;

    private CompareOption(ImmutableArray<T> values)
    {
        this.values = values;
    }

    public static CompareOption<T> None => new(ImmutableArray<T>.Empty);

    public static CompareOption<T> Some(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(ImmutableArray.Create(value));
    }

    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
        => values.IsDefaultOrEmpty ? none() : some(values[0]);
}

// Reset ownership before disposal: callbacks cannot find the resource being destroyed.
// This also repairs external disposal instead of reusing a dead native palette.
internal sealed class OwnedResource<T> where T : notnull
{
    private CompareOption<T> current = CompareOption<T>.None;
    private readonly Func<T, bool> isDisposed;
    private readonly Action<T> dispose;

    public OwnedResource(Func<T, bool> isDisposed, Action<T> dispose)
    {
        this.isDisposed = isDisposed;
        this.dispose = dispose;
    }

    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none)
        => current.Match(some, none);

    public T GetOrCreate(Func<T> create)
        => current.Match(value =>
        {
            if (!isDisposed(value))
                return value;
            Reset();
            return Create(create);
        }, () => Create(create));

    private T Create(Func<T> create)
    {
        var value = create();
        current = CompareOption<T>.Some(value);
        return value;
    }

    public void Reset()
    {
        var previous = current;
        current = CompareOption<T>.None;
        previous.Match(value => { dispose(value); return true; }, () => false);
    }
}
