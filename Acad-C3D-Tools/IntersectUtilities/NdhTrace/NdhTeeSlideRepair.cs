using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// THE REPAIR PASS: it slides a tee the last fraction of a millimetre onto its
/// neighbour, so the model says what the legacy drawing said.
///
/// Authority: NorsynDrawingTools docs/shared-understanding/legacy-fjv-import.md
/// &lt;the-repair-pass-settled-2026-09-20&gt;. The laws it implements, all four the
/// owner's:
///
/// <list type="bullet">
/// <item>CONTACT IS EXACT. A footprint that lands a fraction of a millimetre
/// inside its neighbour is a part in the wrong place, not a part close enough
/// to be called touching. The repair moves the steel; it never widens what
/// counts as contact.</item>
/// <item>IT RUNS AFTER THE IMPORT, OVER THE LEDGER. The complaints are the
/// record of what needed repairing, and every repair is made against the
/// complaint that asked for it - never quietly inside the derivation, where
/// nothing would ever have said that a part moved.</item>
/// <item>THE DOWNSTREAM PART GIVES. Of the two parts in an overlap the one at
/// the HIGHER station moves and the other stands. Which of the two actually
/// drifted is not knowable from the complaint, so the rule is positional and
/// the repair reads the same every time.</item>
/// <item>FIVE CENTIMETRES IS THE CAP. More than that is not drawing
/// imprecision but a design conflict, and the drafter sees it as the complaint
/// it already is.</item>
/// </list>
///
/// A BRANCH SLIDES; AN ELBOW DOES NOT. Sliding is what a branch VERTEX does -
/// it moves along the main, the main's own route unchanged, and the child
/// follows its connected end. Two elbows that do not fit between their corners
/// are a different repair with a different answer (their LEGS are overridden;
/// NDHELBOWCUSTOMIZE already holds that logic), so this pass touches an
/// overlap only when the complaining part is a branch port, and leaves every
/// other overlap standing as the complaint it is.
/// </summary>
internal sealed class NdhTeeSlideRepair
{
    /// <summary>The largest correction that is still drawing imprecision (owner, 2026-09-20).</summary>
    public const double MaxSlideM = 0.05;

    /// <summary>NDH's own word for the complaint this pass repairs.</summary>
    private const string OverlapCode = "FootprintOverlap";

    //The complaint's station and the connection's station are the SAME number
    //out of the same derivation, marshalled twice. This only guards the
    //comparison from being written as ==.
    private const double SamePortM = 1e-9;

    private readonly INdhPipelineIssues _issues;
    private readonly INdhConnector _connector;
    private readonly INdhPipelineModifier _modifier;

    public NdhTeeSlideRepair(
        INdhPipelineIssues issues, INdhConnector connector, INdhPipelineModifier modifier)
    {
        _issues = issues;
        _connector = connector;
        _modifier = modifier;
    }

    /// <summary>
    /// Repairs what it can on every pipeline the import built, and says what it
    /// did. It never throws: a pipeline whose ledger or ports cannot be read
    /// says so on its own line and the pass carries on to the next.
    /// </summary>
    public List<string> Repair(IReadOnlyList<(string Name, string Handle)> built)
    {
        List<string> said = new List<string>();
        foreach ((string name, string handle) in built)
        {
            try
            {
                RepairOne(name, handle, said);
            }
            catch (Exception ex)
            {
                said.Add($"{name}: kunne ikke rettes ({ex.Message}).");
            }
        }
        return said;
    }

    private void RepairOne(string name, string handle, List<string> said)
    {
        List<NdhIssueRow> overlaps = _issues.Read(handle)
            .Where(r => r.CodeName == OverlapCode && r.MeasureM is > 0.0)
            .ToList();
        if (overlaps.Count == 0) return;

        //Ports in station order, so "the next one along the route" is the next
        //index and the downstream of a pair is the larger one.
        List<NdhConnectionRow> ports = _connector.ReadConnections(handle)
            .Where(c => c.IsHost)
            .OrderBy(c => c.Station)
            .ToList();

        //What each vertex has been asked to give, and who asked. A vertex asked
        //twice is not slid at all: two complaints on one tee pull it both ways,
        //and a pass that answered one of them would be choosing which neighbour
        //matters.
        Dictionary<int, List<(NdhIssueRow Issue, double AlongM)>> asks =
            new Dictionary<int, List<(NdhIssueRow, double)>>();

        foreach (NdhIssueRow issue in overlaps)
        {
            double overlapM = issue.MeasureM!.Value;
            int at = PortAt(ports, issue.Station);
            if (at < 0) continue;  //not a branch port: an elbow's overlap, not ours

            int other = NearestNeighbour(ports, at);
            if (other < 0) continue;

            if (overlapM > MaxSlideM)
            {
                said.Add($"{name} [{Where(issue)}]: {Mm(overlapM)} er for meget at skubbe " +
                    $"(grænsen er {Mm(MaxSlideM)}) - står som bemærkning.");
                continue;
            }

            //The downstream of the pair gives, whichever of the two complained.
            int downstream = Math.Max(at, other);
            if (!asks.TryGetValue(downstream, out List<(NdhIssueRow, double)>? rows))
                asks[downstream] = rows = new List<(NdhIssueRow, double)>();
            rows.Add((issue, overlapM));
        }

        List<(int Vertex, double AlongM)> slides = new List<(int, double)>();
        foreach ((int index, List<(NdhIssueRow Issue, double AlongM)> rows) in asks)
        {
            if (rows.Count > 1)
            {
                said.Add($"{name} [{Where(rows[0].Issue)}]: {rows.Count} overlap på samme T-stykke - " +
                    "det kan ikke skubbes begge veje, står som bemærkninger.");
                continue;
            }
            slides.Add((ports[index].RouteIndex, rows[0].AlongM));
        }
        if (slides.Count == 0) return;

        //One list is ONE edit: one gesture, one solve, one undo.
        NdhModifyOutcome outcome = _modifier.SlideVertices(handle, slides);
        if (!outcome.Success)
        {
            said.Add($"{name}: {slides.Count} T-stykke(r) kunne ikke skubbes " +
                $"({outcome.Status}). {outcome.Detail}".TrimEnd());
            return;
        }

        foreach ((int vertex, double alongM) in slides)
            said.Add($"{name}: T-stykket ved vertex {vertex} skubbet {Mm(alongM)} frem ad ruten.");

        //MEASURED, NOT ASSUMED. The pass asked for microns; the ledger is what
        //says whether the steel now touches. A complaint that survived its own
        //repair is reported, because a silent one would read as a success.
        List<NdhIssueRow> left = _issues.Read(handle)
            .Where(r => r.CodeName == OverlapCode && r.MeasureM is > 0.0)
            .ToList();
        int repaired = overlaps.Count - left.Count;
        if (repaired < slides.Count)
            said.Add($"{name}: {slides.Count - repaired} overlap overlevede rettelsen " +
                "- se bemærkningerne.");
    }

    /// <summary>The port standing at this station, or -1 when no port does.</summary>
    private static int PortAt(List<NdhConnectionRow> ports, double station)
    {
        for (int i = 0; i < ports.Count; i++)
            if (Math.Abs(ports[i].Station - station) < SamePortM) return i;
        return -1;
    }

    /// <summary>
    /// The port nearest the one at <paramref name="at"/>, which is the one it
    /// can be overlapping: two parts that share steel are nearer each other
    /// than either is to anything else on the run. -1 when the run carries no
    /// other port.
    ///
    /// The complaint names the other part in its sentence and not as a number,
    /// so this is inferred rather than read. It is why the repair re-reads the
    /// ledger afterwards instead of trusting itself.
    /// </summary>
    private static int NearestNeighbour(List<NdhConnectionRow> ports, int at)
    {
        int best = -1;
        double bestGap = double.MaxValue;
        for (int i = 0; i < ports.Count; i++)
        {
            if (i == at) continue;
            double gap = Math.Abs(ports[i].Station - ports[at].Station);
            if (gap >= bestGap) continue;
            bestGap = gap;
            best = i;
        }
        return best;
    }

    private static string Where(NdhIssueRow issue) =>
        issue.Run.Length > 0 ? $"{issue.Run} {issue.Station:F1} m" : $"{issue.Station:F1} m";

    /// <summary>Millimetres, the way NDH's own complaint says them.</summary>
    private static string Mm(double metres) => $"{metres * 1000.0:F3} mm";
}
