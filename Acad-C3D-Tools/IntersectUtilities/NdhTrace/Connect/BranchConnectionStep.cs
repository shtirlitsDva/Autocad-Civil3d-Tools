using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>A pipeline the import built: its handle and the route it was built from.</summary>
internal sealed record BuiltPipeline(string Handle, NdhRoute Route);

/// <summary>
/// Connects every legacy branch to its main through NDH, with the translated
/// Produkt pinned (the Afgreningsmatrix is never asked), and collects what the
/// drafter must see:
/// - a branch NDH squared by more than <see cref="SilentLimitDeg"/> gets a
///   yellow MLeader describing the change; every correction is in Justeret;
/// - a branch that is not connected - untranslatable, refused by NDH, or not
///   whole in the legacy drawing - gets a yellow circle and an English note.
///
/// Branches are connected root first (by their main's distance from the
/// network's root): a branch that is itself a main is squared onto ITS main
/// before its own branches are placed on it.
///
/// The branch is sent exactly as built from the legacy geometry; the squaring
/// and every geometry law are NDH's. Its connected end is the end of the built
/// route nearest the legacy part's branch port.
/// </summary>
internal static class BranchConnectionStep
{
    /// <summary>Up to this deviation a correction is silent (report only); beyond it, an MLeader too.</summary>
    public const double SilentLimitDeg = 3.0;
    //Below these a squaring changed nothing worth reporting. The connected
    //end always moves (the legacy branch ends at the part's branch port, NDH
    //lands it on the main's centreline, contract D2), so its move alone is no
    //correction.
    private const double DeviationNoiseDeg = 0.01;
    private const double MoveNoiseM = 0.001;

    public static List<ImportMarker> Run(
        LegacyDrawing legacy, MergedTraces merged, IReadOnlyDictionary<string, BuiltPipeline> built,
        INdhConnector connector, NdhImportReport report)
    {
        List<ImportMarker> markers = new List<ImportMarker>();

        foreach (LegacyBranchProblem p in legacy.Problems)
        {
            report.Skipped.Add($"{p.What}: {p.Reason} - ikke forbundet.");
            markers.Add(new ImportMarker(ImportMarkerKind.NotConnected, p.Site, p.Note));
        }

        IEnumerable<LegacyBranch> ordered = legacy.Branches
            .OrderBy(b => legacy.DepthOf(b.MainName))
            .ThenBy(b => b.MainName, StringComparer.Ordinal)
            .ThenBy(b => b.BranchName, StringComparer.Ordinal);
        foreach (LegacyBranch b in ordered)
            Connect(b, merged, built, connector, report, markers);

        foreach ((string main, int count) in legacy.ServiceConnections)
            report.Skipped.Add($"{main}: {count} stik - stikforbindelser importeres ikke endnu, ikke forbundet.");
        foreach (string note in legacy.NetworkNotes)
            report.Skipped.Add(note);

        return markers;
    }

    private static void Connect(
        LegacyBranch b, MergedTraces merged, IReadOnlyDictionary<string, BuiltPipeline> built,
        INdhConnector connector, NdhImportReport report, List<ImportMarker> markers)
    {
        string mainName = merged.NameOf(b.MainName);
        string branchName = merged.NameOf(b.BranchName);
        string what = $"{branchName} → {mainName} ({b.Navn})";
        string noteHead = $"NDHFROMFJV: '{branchName}' not connected to '{mainName}'";

        if (mainName == branchName)
        {
            //The part joins two legacy pipelines that were merged end to end
            //elsewhere: a ring through a tee. NDH cannot branch a pipeline off itself.
            report.Skipped.Add($"{what}: afgrening og hovedledning er sammenlagt til én rørledning - ikke forbundet.");
            markers.Add(new ImportMarker(ImportMarkerKind.NotConnected, b.Site,
                $"{noteHead}: both were merged into one pipeline; a pipeline cannot branch off itself."));
            return;
        }

        BranchTranslation translation = LegacyBranchTranslator.Translate(b);
        if (translation is UntranslatedBranch u)
        {
            report.Skipped.Add($"{what}: {u.Reason} - ikke forbundet.");
            markers.Add(new ImportMarker(ImportMarkerKind.NotConnected, b.Site, $"{noteHead}: {u.Note}."));
            return;
        }
        TranslatedBranch t = (TranslatedBranch)translation;

        if (!built.TryGetValue(mainName, out BuiltPipeline? main) ||
            !built.TryGetValue(branchName, out BuiltPipeline? branch))
        {
            //The pipeline's own refusal is already reported; there is nothing
            //in the drawing to mark.
            string missing = built.ContainsKey(mainName) ? branchName : mainName;
            report.Skipped.Add($"{what}: {missing} blev ikke oprettet - ikke forbundet.");
            return;
        }

        bool atStart = NearestEndIsStart(branch.Route, b.BranchPort);
        NdhConnectOutcome o = connector.Connect(new NdhConnectRequest(
            main.Handle, branch.Handle, atStart, t.Outlet, t.Produkt));

        if (!o.Success)
        {
            report.Refused.Add($"{what}, {t.Produkt}: {o.Status}" +
                (o.MainVertexIndex >= 0 ? $" (hovedledningens punkt {o.MainVertexIndex})" : "") +
                (o.Detail.Length > 0 ? $" - {o.Detail}" : ""));
            markers.Add(new ImportMarker(ImportMarkerKind.NotConnected, b.Site,
                $"{noteHead} ({t.Produkt}, legacy part '{b.Navn}'): {RefusalSentence(o.Status, t.Produkt)}." +
                (o.Detail.Length > 0 ? $" NDH: {o.Detail}" : "")));
            return;
        }

        string outlet = t.Outlet == NdhBranchOutlet.AlongMain ? "langs hovedledningen" : "vinkelret";
        Point2d port = new Point2d(o.PortX, o.PortY);

        string? mismatch = ReadBack(connector, main.Handle, branch.Handle, t.Produkt);
        if (mismatch != null)
        {
            report.Refused.Add($"{what}: forbundet, men {mismatch}");
            markers.Add(new ImportMarker(ImportMarkerKind.NotConnected, port,
                $"NDHFROMFJV: '{branchName}' was connected to '{mainName}', but the connection does not " +
                $"read back as '{t.Produkt}' pinned. Check it."));
            return;
        }

        report.Connected.Add($"{what}: {t.Produkt}, {outlet}");

        if (o.DeviationDeg <= DeviationNoiseDeg && o.LargestMoveM <= MoveNoiseM) return;

        bool loud = o.DeviationDeg > SilentLimitDeg;
        report.Adjusted.Add(
            $"{branchName} → {mainName}: afgreningen rettet {o.DeviationDeg:F2}° " +
            $"({(t.Outlet == NdhBranchOutlet.AlongMain ? "til langs hovedledningen" : "til 90°")}); " +
            $"enden flyttet {o.EndMoveM:F3} m ind på hovedledningens centerlinje, " +
            $"øvrige punkter op til {o.LargestMoveM:F3} m" + (loud ? " - MLeader placeret." : "."));
        if (loud)
            markers.Add(new ImportMarker(ImportMarkerKind.Corrected, port,
                $"NDHFROMFJV squared branch '{branchName}' onto '{mainName}' ({t.Produkt}): " +
                $"the legacy branch stood {o.DeviationDeg:F1}° off " +
                $"{(t.Outlet == NdhBranchOutlet.AlongMain ? "the main's direction" : "90° to the main")}. " +
                $"Its connected end moved {o.EndMoveM:F2} m onto the main's centreline; " +
                $"its other vertices moved up to {o.LargestMoveM:F2} m."));
    }

    /// <summary>Whether the built route's first vertex, rather than its last, is nearer the legacy branch port.</summary>
    private static bool NearestEndIsStart(NdhRoute route, Point2d port)
    {
        NdhRouteVertex first = route.Vertices[0], last = route.Vertices[^1];
        return new Point2d(first.X, first.Y).GetDistanceTo(port) <= new Point2d(last.X, last.Y).GetDistanceTo(port);
    }

    /// <summary>
    /// Null when the main reads back a connection to the branch with the
    /// Produkt pinned; otherwise what is wrong (Danish).
    /// </summary>
    private static string? ReadBack(INdhConnector connector, string mainHandle, string branchHandle, string produkt)
    {
        IReadOnlyList<NdhConnectionRow> rows;
        try
        {
            rows = connector.ReadConnections(mainHandle);
        }
        catch (Exception ex)
        {
            return $"forbindelserne kunne ikke læses tilbage ({ex.Message}).";
        }

        NdhConnectionRow? row = rows
            .Where(r => r.IsHost && string.Equals(r.PartnerHandle, branchHandle, StringComparison.OrdinalIgnoreCase))
            .Cast<NdhConnectionRow?>()
            .FirstOrDefault();
        if (row == null) return "hovedledningen læser ingen forbindelse til afgreningen tilbage.";
        if (row.Value.PinnedProdukt != produkt)
            return $"forbindelsen læses tilbage med '{row.Value.PinnedProdukt}' fastlåst, ikke '{produkt}'.";
        return null;
    }

    private static string RefusalSentence(NdhConnectStatus status, string produkt) => status switch
    {
        NdhConnectStatus.PortOnArc => "the branch point lies on an arc of the main",
        NdhConnectStatus.PortOnCorner => "the branch point lies on a corner vertex of the main",
        NdhConnectStatus.PortOnMainEnd => "the branch point lies on the main's end vertex",
        NdhConnectStatus.PortOccupied => "the main already has a connection at that point",
        NdhConnectStatus.BranchEndOccupied => "the branch's end is already connected",
        NdhConnectStatus.PipeTypeMismatch => "the branch's pipe type cannot meet the main's",
        NdhConnectStatus.NoSuchProdukt => $"'{produkt}' is not a published NDH branch part",
        NdhConnectStatus.ProduktRefused => $"'{produkt}' refuses this junction (outlet, size or construction)",
        NdhConnectStatus.NoLawfulSquare => "no squared branch route obeys the NDH geometry laws",
        NdhConnectStatus.PortOnVerticalElbow => "the branch point lies on a vertical elbow of the main",
        NdhConnectStatus.NotAPipeline => "a pipeline handle named no live pipeline",
        NdhConnectStatus.BadArgs => "the request was malformed (an importer defect)",
        _ => "NDH failed (NDHTRACE has the reason)",
    };
}
