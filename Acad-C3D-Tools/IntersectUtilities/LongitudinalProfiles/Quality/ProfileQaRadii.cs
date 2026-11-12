using System;
using System.Collections.Generic;
using System.Linq;
using IntersectUtilities.LongitudinalProfiles;

namespace IntersectUtilities.LongitudinalProfiles.Quality;

internal sealed record QaPipeSpan(double Start, double End, int DN, string System, string Type, string Series, double Diameter)
{
    internal string Label => $"{System} {Type} DN{DN} {Series}";
}
internal readonly record struct QaRadiusLimit(double Minimum, double Target, bool HasTarget);

internal static class ProfileQaRadii
{
    // 0.01 mm suppresses roundoff in short bulged arcs at large drawing coordinates.
    internal const double NumericalRadiusTolerance = 0.00001;
    // Sources: X:\Internal - kontorsager - Dokumenter\01 Intern\Fjernvarme\02 Statik og lægningsregler\01 Statik beregning.
    // Read from synced workbooks 2026-11-04. Direction/column interpretation confirmed by the user in this chat.
    // Keep workbook precision (DN50 steel twin vertical is 140.6 m, approximately 141 m). No global safety multiplier.
    private sealed record Row(string System, bool Twin, int DN, string Series, double HMin, double HTarget, double VMin, double VTarget, bool VHasTarget);
    private static readonly IReadOnlyList<Row> Rows = Build();

    private static IReadOnlyList<Row> Build()
    {
        var rows = new List<Row>();
        // Projekteringsbukkeradius og vinkel - Enkelt.xlsx, Ark1!A7:C21.
        int[] dn = { 32, 40, 50, 65, 80, 100, 125, 150, 200, 250, 300, 350, 400, 450, 500 };
        double[] minimum = { 21, 24, 30, 38, 44, 57, 70, 84, 110, 137, 162, 178, 203, 229, 254 };
        // -Twin.xlsx: horizontal A5:C14, vertical A23:B32. Vertical has only a minimum.
        double[] vertical = { 103.8, 115.6, 140.6, 172.2, 202.8, 253.6, 309.4, 376.6, 483.2, 591 };
        for (int i = 0; i < dn.Length; i++)
        {
            rows.Add(new("Stål", false, dn[i], "", minimum[i], minimum[i] * 1.2, minimum[i], minimum[i] * 1.2, true));
            if (i < vertical.Length) rows.Add(new("Stål", true, dn[i], "", minimum[i], minimum[i] * 1.2, vertical[i], 0, false));
        }
        // Projekteringsbukkeradius og vinkel isoalupex10 ISOPLUS.xlsx, Ark1!A4:D8.
        int[] aluDn = { 25, 32, 40, 50, 63 };
        double[] aluMin = { 1.1, 1.3, 1.4, 1.8, 2 };
        double[] aluTarget = { 1.5, 1.7, 1.8, 2.5, 2.6 };
        for (int i = 0; i < aluDn.Length; i++) rows.Add(new("AluPex", true, aluDn[i], "", aluMin[i], aluTarget[i], aluTarget[i] * 2, 0, false));
        // Projekteringsbukkeradius og vinkel PERT Logstor.xlsx, Ark1!A6:C11 and A16:C21.
        int[] pertDn = { 25, 32, 40, 50, 63, 75 };
        double[] pertHMin = { 1, 1, 1.4, 1.8, 1.8, 20.6 };
        double[] pertHTarget = { 1.5, 1.5, 2, 2.5, 2.5, 27 };
        double[] pertVMin = { 5.8, 6.9, 8.3, 9.9, 12, 20.6 };
        double[] pertVTarget = { 8, 9, 11, 13, 16, 27 };
        string[] series = { "S2", "S2", "S2", "S2", "S1", "" };
        for (int i = 0; i < pertDn.Length; i++) rows.Add(new("PertFlextra", true, pertDn[i], series[i], pertHMin[i], pertHTarget[i], pertVMin[i], pertVTarget[i], true));
        return rows;
    }

    internal static Result<QaRadiusLimit> Get(QaPipeSpan pipe, bool vertical)
    {
        bool twin = pipe.Type == "Twin";
        bool single = pipe.Type == "Frem" || pipe.Type == "Retur" || pipe.Type == "Enkelt";
        if (!twin && !single) return Result<QaRadiusLimit>.Failure($"Unknown pipe arrangement: {pipe.Label}");
        if (pipe.System == "PertFlextra" && single)
            return Result<QaRadiusLimit>.Failure("Single-pipe PERT table remains unverified (KEV clarification pending).");
        var matches = Rows.Where(r => r.System == pipe.System && r.Twin == twin && r.DN == pipe.DN && (r.Series.Length == 0 || r.Series == pipe.Series)).ToArray();
        if (matches.Length != 1) return Result<QaRadiusLimit>.Failure($"No verified {(vertical ? "vertical" : "horizontal")} radius rule for {pipe.Label}.");
        var row = matches[0];
        return Result<QaRadiusLimit>.Success(vertical
            ? new(row.VMin, row.VTarget, row.VHasTarget)
            : new(row.HMin, row.HTarget, true));
    }

    internal static string Status(double radius, QaRadiusLimit rule, QaPipeSpan pipe)
    {
        // Steel drawing lookups use whole metres; preserve the workbook values for display.
        var acceptance = pipe.System == "Stål" ? rule with
        {
            Minimum = Math.Round(rule.Minimum, 0, MidpointRounding.AwayFromZero),
            Target = Math.Round(rule.Target, 0, MidpointRounding.AwayFromZero)
        } : rule;
        return Status(radius, acceptance);
    }

    internal static string Status(double radius, QaRadiusLimit rule) =>
        radius + NumericalRadiusTolerance < rule.Minimum ? "FAIL" : rule.HasTarget && radius + NumericalRadiusTolerance < rule.Target ? "REVIEW" : "OK";
}
