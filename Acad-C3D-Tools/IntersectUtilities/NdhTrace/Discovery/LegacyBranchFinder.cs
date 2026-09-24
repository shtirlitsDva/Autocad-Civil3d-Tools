using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using System;
using System.Collections.Generic;
using System.Linq;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// Finds the legacy branches from their parts. A tee names its main in
/// BelongsToAlignment and its branch in BranchesOffToAlignment; a stud or a
/// svanehals the other way round (see <see cref="LegacyPartRole"/>). Where the
/// part names no branch - a legacy drawing the network never repaired, or a
/// svanehals whose BelongsToAlignment is its main - the branch is the pipeline
/// of whatever the legacy connection graph (DriGraph, freshly written) meets at
/// the part's BRANCH port.
///
/// One junction is one branch, however many parts draw it: a junction on a
/// bonded main is one part per carrier (live run 2026-09-19, F1: every T
/// ENKELT and SH LIGE on a bonded main was sent to NDH twice, and the second
/// attempt refused BranchEndOccupied).
/// </summary>
internal static class LegacyBranchFinder
{
    //How far apart the parts of one junction may stand: the carriers of a
    //bonded main are up to 2 x 0.73 m apart centre to centre (DN600), so the
    //branch ports of the pair are well within this. Two separate junctions of
    //the same pipelines, through the same part, are a whole branch apart.
    private const double JunctionReach = 3.0;

    /// <summary>One legacy part, with the pipelines it joins as the reading resolved them.</summary>
    private sealed record Candidate(LegacyComponent Part, string MainName, string BranchName, string NamedBy);

    public static void Read(
        IReadOnlyList<LegacyComponent> parts, Database fjvDb, Transaction tx, PropertySetHelper psh,
        LegacyDrawing drawing)
    {
        Dictionary<string, LegacyPipelineTrace> traces = drawing.Traces.Traces.ToDictionary(t => t.Name, StringComparer.Ordinal);

        List<Candidate> candidates = new List<Candidate>();
        foreach (LegacyComponent part in parts)
        {
            switch (part.Role)
            {
                case LegacyPartRole.ServiceConnection:
                    //Out of scope (#319): counted per main, never connected.
                    string main = part.BelongsTo.IsNoE() ? "(ingen rørledning)" : part.BelongsTo;
                    drawing.ServiceConnections[main] =
                        drawing.ServiceConnections.TryGetValue(main, out int n) ? n + 1 : 1;
                    continue;
                case LegacyPartRole.Tee:
                    candidates.Add(Resolve(part, part.BelongsTo, part.BranchesOffTo, fjvDb, tx, psh));
                    continue;
                case LegacyPartRole.Stud:
                    candidates.Add(Resolve(part, part.BranchesOffTo, part.BelongsTo, fjvDb, tx, psh));
                    continue;
                default:
                    //Materialeskift branches are found where two pipelines meet
                    //(LegacyNetwork); everything else is no branch part.
                    continue;
            }
        }

        foreach (List<Candidate> junction in Junctions(candidates))
            Add(junction, traces, drawing);
    }

    /// <summary>
    /// The pipelines one part joins. A part naming the SAME pipeline as its
    /// main and its branch names no branch: a svanehals within 15 degrees of
    /// its main's axis is filed that way, and a tee filed that way is a legacy
    /// drawing error. Either way the branch is what the connection graph meets
    /// at the part's BRANCH port, if anything other than the main.
    /// </summary>
    private static Candidate Resolve(
        LegacyComponent part, string mainName, string branchName, Database fjvDb, Transaction tx, PropertySetHelper psh)
    {
        if (branchName.IsNotNoE() && branchName != mainName)
            return new Candidate(part, mainName, branchName, "BranchesOffToAlignment/BelongsToAlignment");
        if (mainName.IsNoE())
            return new Candidate(part, mainName, "", "");

        string fromGraph = BranchFromGraph(part, mainName, fjvDb, tx, psh);
        //Kept as the part filed it when the graph finds nothing: the report
        //then says what the part names.
        return fromGraph.IsNotNoE()
            ? new Candidate(part, mainName, fromGraph, "DriGraph (BRANCH-port)")
            : new Candidate(part, mainName, branchName, "");
    }

    /// <summary>
    /// The branch a candidate resolved: "" when it names none, or names its
    /// own main (the svanehals / drawing-error filing <see cref="Add"/> sorts out).
    /// </summary>
    private static string ResolvedBranch(Candidate c) =>
        c.BranchName.IsNotNoE() && c.BranchName != c.MainName ? c.BranchName : "";

    /// <summary>
    /// The candidates grouped into junctions: the same part on the same main,
    /// their branch ports within <see cref="JunctionReach"/> of the group's
    /// first, and no two DIFFERENT branches named. The branch is not part of
    /// the key: the two carriers of a bonded junction are two blocks, and one
    /// may resolve its branch while the other does not (review of #319, I3 -
    /// keyed on the branch too, such a pair split into one junction connected
    /// and one marked "names no branch").
    /// </summary>
    private static List<List<Candidate>> Junctions(List<Candidate> candidates)
    {
        List<List<Candidate>> junctions = new List<List<Candidate>>();
        foreach (Candidate c in candidates)
        {
            string branch = ResolvedBranch(c);
            List<Candidate>? same = junctions.FirstOrDefault(j =>
                j[0].Part.Navn == c.Part.Navn &&
                j[0].MainName == c.MainName &&
                j[0].Part.BranchPort.GetDistanceTo(c.Part.BranchPort) <= JunctionReach &&
                (branch.IsNoE() || j.All(o => ResolvedBranch(o).IsNoE() || ResolvedBranch(o) == branch)));
            if (same != null) same.Add(c);
            else junctions.Add(new List<Candidate> { c });
        }
        return junctions;
    }

    private static void Add(
        List<Candidate> junction, Dictionary<string, LegacyPipelineTrace> traces, LegacyDrawing drawing)
    {
        //The part that resolved the branch speaks for the junction; with none
        //resolved, the first says what it names.
        Candidate first = junction.FirstOrDefault(c => ResolvedBranch(c).IsNotNoE()) ?? junction[0];
        string navn = first.Part.Navn;
        string handles = string.Join(", ", junction.Select(c => c.Part.Handle));
        string mainName = first.MainName, branchName = first.BranchName;
        Point2d port = new Point2d(
            junction.Average(c => c.Part.BranchPort.X), junction.Average(c => c.Part.BranchPort.Y));

        if (mainName.IsNoE())
        {
            drawing.Problems.Add(new LegacyBranchProblem(
                $"{navn} ({handles})", port,
                $"NDHFROMFJV: legacy part '{navn}' names no main pipeline; not connected.",
                "den gamle del angiver ingen hovedledning"));
            return;
        }
        //A svanehals filed with its main on both sides is the legacy way of
        //filing one within 15 degrees of the main's axis: it names no branch.
        if (branchName == mainName && first.Part.Role == LegacyPartRole.Stud) branchName = "";
        if (branchName == mainName)
        {
            //Live run 2026-09-19, F4: the T ENKELT pair at the start of 057 is
            //filed with 057 as both its main and its branch, and its main-run
            //ports meet no pipe - the legacy drawing does not say what 057
            //leaves. That is the legacy drawing's error, and the note says so.
            drawing.Problems.Add(new LegacyBranchProblem(
                $"{navn} ({handles}) på {mainName}", port,
                $"NDHFROMFJV: legacy part '{navn}' names '{mainName}' as both its main and its branch, " +
                "and nothing else meets its branch port - an error in the legacy drawing " +
                "(fix BelongsToAlignment/BranchesOffToAlignment there); not connected.",
                $"den gamle del angiver '{mainName}' som både hovedledning og afgrening - " +
                "fejl i den gamle tegning"));
            return;
        }
        if (branchName.IsNoE())
        {
            drawing.Problems.Add(new LegacyBranchProblem(
                $"{navn} ({handles}) på {mainName}", port,
                $"NDHFROMFJV: legacy part '{navn}' on '{mainName}' names no branch pipeline, " +
                "and nothing meets its branch port; not connected.",
                "den gamle del angiver ingen afgrening, og intet møder dens afgreningsport"));
            return;
        }

        Point2d site = port;
        PipeSystemEnum system = PipeSystemEnum.Ukendt;
        PipeTypeEnum type = PipeTypeEnum.Ukendt;
        if (traces.TryGetValue(mainName, out LegacyPipelineTrace? mainTrace))
            (site, system, type) = SiteOn(mainTrace, port);

        drawing.Branches.Add(new LegacyBranch(
            mainName, branchName, navn, handles, port, site, system, type, first.NamedBy));
    }

    /// <summary>
    /// The pipeline of what the legacy connection graph says meets the part's
    /// BRANCH port, other than the main; "" when nothing does.
    /// </summary>
    private static string BranchFromGraph(
        LegacyComponent part, string mainName, Database fjvDb, Transaction tx, PropertySetHelper psh)
    {
        string conString = psh.Graph.ReadPropertyString(part.Block, psh.GraphDef.ConnectedEntities);
        if (conString.IsNoE() || !Con.ConRegex.IsMatch(conString)) return "";

        foreach (Con con in Con.ParseConString(conString).Where(c => c.OwnEndType == EndType.Branch))
        {
            if (!fjvDb.TryGetObjectId(con.ConHandle, out ObjectId id)) continue;
            if (tx.GetObject(id, OpenMode.ForRead) is not Entity other) continue;
            string name = psh.Pipeline.ReadPropertyString(other, psh.PipelineDef.BelongsToAlignment);
            if (name.IsNotNoE() && name != mainName) return name;
        }
        return "";
    }

    /// <summary>
    /// The point on the main's centreline nearest the branch port, and the
    /// main's legacy identity there.
    /// </summary>
    internal static (Point2d Site, PipeSystemEnum System, PipeTypeEnum Type) SiteOn(
        LegacyPipelineTrace main, Point2d port)
    {
        Point3d onMain = main.Centreline.GetClosestPointTo(new Point3d(port.X, port.Y, 0.0), false);
        double dist = main.Centreline.GetDistAtPoint(onMain);
        LegacyIdentitySpan span = main.Spans.FirstOrDefault(s => dist >= s.StartDist && dist <= s.EndDist);
        if (span == default) span = main.Spans[^1];
        return (new Point2d(onMain.X, onMain.Y), span.System, span.Type);
    }
}
