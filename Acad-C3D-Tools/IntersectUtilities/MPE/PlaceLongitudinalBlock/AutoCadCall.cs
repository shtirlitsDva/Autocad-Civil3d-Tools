using CSharpFunctionalExtensions;

using Norsyn.AecPropertySets;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// The pipe trim's boundary to AutoCAD calls that throw for an outcome the drawing can cause: what AutoCAD throws
/// becomes an AecError (code AEC_HOST) that ends the trim before anything is written. Any other exception passes.
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
            return Result.Failure<T, AecError>(
                new AecError(AecError.HostCode, $"AutoCAD fejlede ({what}): {exception.Message}"));
        }
    }
}
