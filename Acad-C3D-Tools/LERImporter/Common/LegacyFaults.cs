namespace LERImporter;

internal static class LegacyFaults
{
    /// <summary>
    /// Hands a fault to ConsolidatedCreator's older code, whose failures are exceptions that
    /// the command catches and logs. Used only at that seam.
    /// </summary>
    public static T OrThrowToLegacy<T>(this Result<T> result) =>
        result.Match(value => value, message => throw new System.Exception(message));
}
