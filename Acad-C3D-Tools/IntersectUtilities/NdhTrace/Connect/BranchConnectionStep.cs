using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>A pipeline the import built: its handle and the route it was built from.</summary>
/// <summary>
/// A pipeline the import built. <paramref name="VertexCauses"/> is the durable
/// id NDH gave each vertex the importer asked for, index-aligned with the
/// route's own vertices - the name a component standing on that vertex is
/// written under. It is carried rather than looked up again because the built
/// route lays extra vertices of its own, so the i-th vertex NDH holds is not
/// the i-th vertex that was asked for.
/// </summary>
internal sealed record BuiltPipeline(
    string Handle, NdhRoute Route, IReadOnlyList<ulong> VertexCauses)
{
    /// <summary>The id of the vertex the importer asked for at this index; 0 when unnamed.</summary>
    public ulong CauseAt(int authoredIndex) =>
        authoredIndex >= 0 && authoredIndex < VertexCauses.Count ? VertexCauses[authoredIndex] : 0UL;
}

/// <summary>
/// Connects every legacy branch to its main through NDH, with the translated
/// Produkt pinned (the Afgreningsmatrix is never asked), and collects what the
/// drafter must see:
/// - a branch NDH squared by more than <see cref="SilentLimitDeg"/> gets a
///   yellow MLeader describing the change; every correction is in Justeret;
/// - a branch connected but not reading back as asked is listed as connected
///   with a warning, and gets an MLeader saying what to check;
/// - a branch that is not connected - untranslatable, refused by NDH, not
///   whole in the legacy drawing, or its main or branch pipeline not created -
///   gets a yellow circle and an English note.
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
            markers.Add(new NotConnectedMarker(p.Site, p.Note));
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
            markers.Add(new NotConnectedMarker(b.Site,
                $"{noteHead}: both were merged into one pipeline; a pipeline cannot branch off itself."));
            return;
        }

        //THE TRANSLATION SAYS WHAT HAPPENS TO IT. Nothing here reads its type:
        //a part that connects connects, a part with no counterpart is marked,
        //and a part NDH deliberately does not model passes in silence - which
        //is a law and not an omission
        //(legacy-fjv-import.md <every-block-settled-2026-09-21>).
        LegacyBranchTranslator.Translate(b).Tell(new BranchAudience(
            b, what, noteHead, mainName, branchName, built, connector, report, markers));
    }

    /// <summary>
    /// The three things that can happen to one legacy branch. It holds what
    /// <see cref="Connect"/> already worked out, so each outcome reads as the
    /// one sentence it is.
    /// </summary>
    private sealed class BranchAudience(
        LegacyBranch b, string what, string noteHead, string mainName, string branchName,
        IReadOnlyDictionary<string, BuiltPipeline> built, INdhConnector connector,
        NdhImportReport report, List<ImportMarker> markers) : IBranchAudience
    {
        public void Connect(string produkt, NdhBranchOutlet outlet) => ConnectTranslated(
            b, what, noteHead, mainName, branchName, produkt, outlet,
            built, connector, report, markers);

        public void CannotConnect(string note, string reason)
        {
            report.Skipped.Add($"{what}: {reason} - ikke forbundet.");
            markers.Add(new NotConnectedMarker(b.Site, $"{noteHead}: {note}."));
        }

        //Silent BY LAW: a mark here would ask the drafter to fix something that
        //is not broken.
        public void Ignore(string why) { }
    }

    private static void ConnectTranslated(
        LegacyBranch b, string what, string noteHead, string mainName, string branchName,
        string produkt, NdhBranchOutlet outletWay,
        IReadOnlyDictionary<string, BuiltPipeline> built, INdhConnector connector,
        NdhImportReport report, List<ImportMarker> markers)
    {
        if (!built.TryGetValue(mainName, out BuiltPipeline? main) ||
            !built.TryGetValue(branchName, out BuiltPipeline? branch))
        {
            //The pipeline's own refusal is reported where it was built; the
            //junction it leaves unconnected is marked here, where the drafter
            //will look for it (review of #319, I1).
            string[] missing = new[] { mainName, branchName }.Where(n => !built.ContainsKey(n)).ToArray();
            report.Skipped.Add($"{what}: {string.Join(" og ", missing)} blev ikke oprettet - ikke forbundet.");
            markers.Add(new NotConnectedMarker(b.Site,
                $"{noteHead} (legacy part '{b.Navn}'): the NDH pipeline " +
                $"{string.Join(" and ", missing.Select(n => $"'{n}'"))} was not created."));
            return;
        }

        bool atStart = NearestEndIsStart(branch.Route, b.BranchPort);
        //THE TEE HOLDS ITS SEAT: the connection stands where JunctionSeating
        //seated it, which is where the main was routed straight and clear.
        NdhConnectOutcome o = connector.Connect(new NdhConnectRequest(
            main.Handle, branch.Handle, atStart, outletWay, produkt, b.Site));

        if (!o.Success)
        {
            report.Refused.Add($"{what}, {produkt}: {o.Status}" +
                (o.MainVertexIndex >= 0 ? $" (hovedledningens punkt {o.MainVertexIndex})" : "") +
                (o.Detail.Length > 0 ? $" - {o.Detail}" : ""));
            markers.Add(new NotConnectedMarker(b.Site,
                $"{noteHead} ({produkt}, legacy part '{b.Navn}'): {RefusalSentence(o.Status, produkt)}." +
                (o.Detail.Length > 0 ? $" NDH: {o.Detail}" : "")));
            return;
        }

        string outletWords = outletWay == NdhBranchOutlet.AlongMain ? "langs hovedledningen" : "vinkelret";
        Point2d port = new Point2d(o.PortX, o.PortY);

        report.Connected.Add($"{what}: {produkt}, {outletWords}");

        //What the drafter must check at this port, gathered into one leader.
        List<string> notes = new List<string>();

        //NDH answered Ok, so the connection stands: a read-back that disagrees
        //is a connection to check, never a refusal (review of #319, I4).
        string? mismatch = ReadBack(connector, main.Handle, branch.Handle, produkt);
        if (mismatch != null)
        {
            report.Connected.Add($"  ADVARSEL: {branchName} → {mainName} er forbundet, men {mismatch}");
            notes.Add($"NDHFROMFJV connected '{branchName}' to '{mainName}', but the connection does not " +
                $"read back as '{produkt}' pinned. Check it.");
        }

        //A corner shifted because no S-offset could be laid is more than drawing
        //noise, so it is always said, and marked (the tee holds its seat).
        bool noRoom = o.Correction == NdhCornerCorrection.ShiftedForWantOfRoom;
        if (o.DeviationDeg > DeviationNoiseDeg || o.LargestMoveM > MoveNoiseM ||
            o.Correction != NdhCornerCorrection.Shifted)
        {
            bool loud = o.DeviationDeg > SilentLimitDeg || noRoom;
            report.Adjusted.Add(
                $"{branchName} → {mainName}: afgreningen rettet {o.DeviationDeg:F2}° " +
                $"({(outletWay == NdhBranchOutlet.AlongMain ? "til langs hovedledningen" : "til 90°")}); " +
                $"enden flyttet {o.EndMoveM:F3} m ind på hovedledningens centerlinje, " +
                $"øvrige punkter op til {o.LargestMoveM:F3} m" + CornerWords(o) +
                (loud ? " - MLeader placeret." : "."));
            if (loud)
                notes.Add($"NDHFROMFJV squared branch '{branchName}' onto '{mainName}' ({produkt}): " +
                    $"the legacy branch stood {o.DeviationDeg:F1}° off " +
                    $"{(outletWay == NdhBranchOutlet.AlongMain ? "the main's direction" : "90° to the main")}. " +
                    $"Its connected end moved {o.EndMoveM:F2} m onto the main's centreline; " +
                    $"its other vertices moved up to {o.LargestMoveM:F2} m." +
                    (noRoom
                        ? $" Its first corner stood {o.CornerOffsetM:F3} m off the square line and was " +
                          "moved: no S-offset could be laid in the first leg (the reason is in NDHTRACE)."
                        : ""));
        }

        //\P is MText's paragraph break.
        if (notes.Count > 0) markers.Add(new ConnectionMarker(port, string.Join("\\P", notes)));
    }

    /// <summary>What the branch did to take the tee's seat, as the report says it.</summary>
    private static string CornerWords(NdhConnectOutcome o) => o.Correction switch
    {
        NdhCornerCorrection.Offset =>
            $"; første ben fik en S-forskydning på {o.CornerOffsetM:F3} m, knækket står som tegnet",
        NdhCornerCorrection.ShiftedForWantOfRoom =>
            $"; knækket flyttet {o.CornerOffsetM:F3} m - en S-forskydning kunne ikke lægges her",
        _ => "",
    };

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
