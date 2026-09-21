namespace GDALService.Common;

// The service's vocabulary for "it worked or it did not" and "there is one or
// there is none". Null never means either of these here: an expected absence is
// None, an expected failure is a Fault, and a switch over these unions has no
// `_` arm, so a new case breaks the build where it is not handled yet.

internal sealed record Ok<T>(T Value);

internal enum FaultKind { InvalidArgs, NotFound, NotInitialized, Gdal, Internal }

internal sealed record Fault(FaultKind Kind, string Message);

internal union Result<T>(Ok<T>, Fault);

internal sealed record Some<T>(T Value);

internal sealed record None
{
    public static readonly None Instance = new();
    private None() { }
}

internal union Option<T>(Some<T>, None);

internal static class ResultExtensions
{
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> next) =>
        result switch
        {
            Ok<TIn> ok => next(ok.Value),
            Fault fault => fault,
        };

    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map) =>
        result switch
        {
            Ok<TIn> ok => new Ok<TOut>(map(ok.Value)),
            Fault fault => fault,
        };

    // A side effect per case. A switch statement would not be checked for
    // exhaustiveness, so the expression picks the action and then runs it; this
    // and its Option twin are the only places that pattern is written out.
    public static void Switch<T>(this Result<T> result, Action<T> ok, Action<Fault> fault) =>
        (result switch
        {
            Ok<T> value => (Action)(() => ok(value.Value)),
            Fault failure => () => fault(failure),
        })();
}

internal static class OptionExtensions
{
    public static void Switch<T>(this Option<T> option, Action<T> some, Action none) =>
        (option switch
        {
            Some<T> value => (Action)(() => some(value.Value)),
            None => none,
        })();
}
