using CSharpFunctionalExtensions;

namespace LERImporter;

internal static class LegacyFaults
{
    /// <summary>
    /// Hands a fault to ConsolidatedCreator's older code, whose failures are exceptions that
    /// the command catches and logs. Used only at that seam.
    /// </summary>
    public static void OrThrowToLegacy(this UnitResult<AecError> result) =>
        result.Match(() => { }, error => throw new System.Exception(error.Message));
}
