using CSharpFunctionalExtensions;

using Norsyn.AecPropertySets;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// The boundary to AutoCAD calls that throw for an outcome the drawing or the session can cause: what AutoCAD
/// throws becomes an AecError (code AEC_HOST). In the pipe trim it ends the trim before anything is written. Any
/// other exception passes.
/// </summary>
internal static class AutoCadCall
{
    internal static Result<T, AecError> Run<T>(string what, Func<T> call)
    {
        try
        {
            return Result.Success<T, AecError>(call());
        }
        catch (Autodesk.AutoCAD.Runtime.Exception exception)
        {
            return Result.Failure<T, AecError>(Failed(what, exception));
        }
    }

    /// <summary>The same boundary for a call that returns nothing.</summary>
    internal static UnitResult<AecError> Do(string what, Action call)
    {
        try
        {
            call();
            return UnitResult.Success<AecError>();
        }
        catch (Autodesk.AutoCAD.Runtime.Exception exception)
        {
            return UnitResult.Failure(Failed(what, exception));
        }
    }

    private static AecError Failed(string what, Autodesk.AutoCAD.Runtime.Exception exception) =>
        new(AecError.HostCode, $"AutoCAD fejlede ({what}): {exception.Message}");
}
