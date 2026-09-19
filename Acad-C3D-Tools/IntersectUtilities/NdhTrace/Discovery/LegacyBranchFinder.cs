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
/// </summary>
internal static class LegacyBranchFinder
{
    public static void Read(
        IReadOnlyList<LegacyComponent> parts, Database fjvDb, Transaction tx, PropertySetHelper psh,
        LegacyDrawing drawing)
    {
        Dictionary<string, LegacyPipelineTrace> traces = drawing.Traces.Traces.ToDictionary(t => t.Name, StringComparer.Ordinal);

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
                    Add(part, part.BelongsTo, part.BranchesOffTo, fjvDb, tx, psh, traces, drawing);
                    continue;
                case LegacyPartRole.Stud:
                    //A svanehals within 15 degrees of its main's axis was filed
                    //with BelongsTo = the main itself: that names no branch.
                    string branch = part.BelongsTo == part.BranchesOffTo ? "" : part.BelongsTo;
                    Add(part, part.BranchesOffTo, branch, fjvDb, tx, psh, traces, drawing);
                    continue;
                default:
                    //Materialeskift branches are found where two pipelines meet
                    //(LegacyNetwork); everything else is no branch part.
                    continue;
            }
        }
    }

    private static void Add(
        LegacyComponent part, string mainName, string branchName,
        Database fjvDb, Transaction tx, PropertySetHelper psh,
        Dictionary<string, LegacyPipelineTrace> traces, LegacyDrawing drawing)
    {
        Point2d port = part.BranchPort;
        string namedBy = "BranchesOffToAlignment/BelongsToAlignment";

        if (mainName.IsNoE())
        {
            drawing.Problems.Add(new LegacyBranchProblem(
                $"{part.Navn} ({part.Handle})", port,
                $"NDHFROMFJV: legacy part '{part.Navn}' names no main pipeline; not connected.",
                "den gamle del angiver ingen hovedledning"));
            return;
        }

        if (branchName.IsNoE())
        {
            branchName = BranchFromGraph(part, mainName, fjvDb, tx, psh);
            namedBy = "DriGraph (BRANCH-port)";
        }
        if (branchName.IsNoE())
        {
            drawing.Problems.Add(new LegacyBranchProblem(
                $"{part.Navn} ({part.Handle}) på {mainName}", port,
                $"NDHFROMFJV: legacy part '{part.Navn}' on '{mainName}' names no branch pipeline, " +
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
            mainName, branchName, part.Navn, part.Handle, port, site, system, type, namedBy));
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
