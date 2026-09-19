using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// The traces to build after end-to-end chains are merged. Owns (and
/// disposes) the merged traces it made; the untouched ones stay owned by the
/// legacy reading.
/// </summary>
internal sealed class MergedTraces : IDisposable
{
    private readonly List<LegacyPipelineTrace> _made = new List<LegacyPipelineTrace>();

    public List<LegacyPipelineTrace> Traces { get; } = new List<LegacyPipelineTrace>();
    /// <summary>An absorbed legacy pipeline's name to the NDH pipeline it became part of.</summary>
    public Dictionary<string, string> Alias { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    /// <summary>Per merged pipeline, what it absorbed (Danish, for the report).</summary>
    public List<string> Merged { get; } = new List<string>();
    /// <summary>Joins that were not merged, and why (Danish, for the report).</summary>
    public List<string> Notes { get; } = new List<string>();

    /// <summary>The NDH pipeline a legacy pipeline is part of.</summary>
    public string NameOf(string legacyName) => Alias.TryGetValue(legacyName, out string? n) ? n : legacyName;

    internal void AddMade(LegacyPipelineTrace t)
    {
        _made.Add(t);
        Traces.Add(t);
    }

    public void Dispose()
    {
        foreach (LegacyPipelineTrace t in _made) t.Dispose();
    }
}

/// <summary>
/// Merges legacy pipelines that meet end to end with no branch part into ONE
/// trace, so the chain is built once as one NDH pipeline (legacy-fjv-import.md
/// connections-settled: end-to-end joins are merged). Chains of any length
/// merge. The merged pipeline is named after the member nearest the network's
/// root (ties: the ordinal-first name) and runs from the chain's end nearest
/// the root. Its identity spans are the members' spans end to end: the new
/// pipeline takes its identity boundaries from the pipelines it absorbs.
///
/// An end met end to end by more than one other pipeline is a junction without
/// a part; nothing merges there, and the report says so. A closed ring of joins
/// is opened at its root-nearest member's start.
/// </summary>
internal static class LegacyTraceMerger
{
    private readonly record struct End(string Name, bool AtStart);

    public static MergedTraces Merge(
        IReadOnlyList<LegacyPipelineTrace> traces, IReadOnlyList<LegacyJoin> joins, Func<string, int> depthOf)
    {
        MergedTraces result = new MergedTraces();
        try
        {
            Dictionary<string, LegacyPipelineTrace> byName = traces.ToDictionary(t => t.Name, StringComparer.Ordinal);
            Dictionary<End, End> partner = Partners(joins, byName, result);

            HashSet<string> done = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyPipelineTrace t in traces.OrderBy(x => depthOf(x.Name)).ThenBy(x => x.Name, StringComparer.Ordinal))
            {
                if (done.Contains(t.Name)) continue;

                List<string> component = Component(t.Name, partner);
                done.UnionWith(component);
                if (component.Count == 1)
                {
                    result.Traces.Add(t);
                    continue;
                }

                List<(LegacyPipelineTrace Trace, bool Reversed)> chain = Chain(component, partner, depthOf, byName, result);
                string survivor = component
                    .OrderBy(depthOf).ThenBy(x => x, StringComparer.Ordinal).First();

                result.AddMade(Concatenate(survivor, chain));
                foreach (string absorbed in component.Where(x => x != survivor))
                    result.Alias[absorbed] = survivor;
                result.Merged.Add(
                    $"{survivor}: {string.Join(" + ", chain.Select(x => x.Trace.Name))}");
            }
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>Each pipeline end to the one end meeting it; ends met more than once merge nothing.</summary>
    private static Dictionary<End, End> Partners(
        IReadOnlyList<LegacyJoin> joins, Dictionary<string, LegacyPipelineTrace> byName, MergedTraces result)
    {
        List<LegacyJoin> usable = joins.Where(j => byName.ContainsKey(j.A) && byName.ContainsKey(j.B) && j.A != j.B).ToList();

        Dictionary<End, List<LegacyJoin>> atEnd = new Dictionary<End, List<LegacyJoin>>();
        foreach (LegacyJoin j in usable)
        {
            AddTo(atEnd, new End(j.A, j.AAtStart), j);
            AddTo(atEnd, new End(j.B, j.BAtStart), j);
        }

        HashSet<LegacyJoin> dropped = new HashSet<LegacyJoin>();
        foreach ((End end, List<LegacyJoin> js) in atEnd.Where(x => x.Value.Count > 1))
        {
            dropped.UnionWith(js);
            LegacyJoin first = js[0];
            result.Notes.Add(
                $"{end.Name}: mødes ende mod ende med {js.Count} rørledninger ved " +
                $"({first.At.X:F2}, {first.At.Y:F2}) uden afgreningsdel - ikke sammenlagt.");
        }

        Dictionary<End, End> partner = new Dictionary<End, End>();
        foreach (LegacyJoin j in usable.Where(x => !dropped.Contains(x)))
        {
            partner[new End(j.A, j.AAtStart)] = new End(j.B, j.BAtStart);
            partner[new End(j.B, j.BAtStart)] = new End(j.A, j.AAtStart);
        }
        return partner;
    }

    private static void AddTo(Dictionary<End, List<LegacyJoin>> atEnd, End end, LegacyJoin j)
    {
        if (!atEnd.TryGetValue(end, out List<LegacyJoin>? list)) atEnd[end] = list = new List<LegacyJoin>();
        list.Add(j);
    }

    private static List<string> Component(string start, Dictionary<End, End> partner)
    {
        List<string> seen = new List<string> { start };
        Queue<string> queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string name = queue.Dequeue();
            foreach (bool atStart in new[] { true, false })
            {
                if (!partner.TryGetValue(new End(name, atStart), out End other)) continue;
                if (seen.Contains(other.Name)) continue;
                seen.Add(other.Name);
                queue.Enqueue(other.Name);
            }
        }
        return seen;
    }

    /// <summary>
    /// The component's members in chain order, each with whether it runs
    /// against its own direction, starting from the free end nearest the root.
    /// </summary>
    private static List<(LegacyPipelineTrace, bool)> Chain(
        List<string> component, Dictionary<End, End> partner, Func<string, int> depthOf,
        Dictionary<string, LegacyPipelineTrace> byName, MergedTraces result)
    {
        List<End> freeEnds = component
            .SelectMany(n => new[] { new End(n, true), new End(n, false) })
            .Where(e => !partner.ContainsKey(e))
            .OrderBy(e => depthOf(e.Name)).ThenBy(e => e.Name, StringComparer.Ordinal)
            .ToList();

        End first;
        if (freeEnds.Count > 0) first = freeEnds[0];
        else
        {
            //A ring: open it at the root-nearest member's start.
            string opener = component.OrderBy(depthOf).ThenBy(x => x, StringComparer.Ordinal).First();
            first = new End(opener, true);
            End across = partner[first];
            partner.Remove(first);
            partner.Remove(across);
            result.Notes.Add($"{opener}: rørledningerne {string.Join(", ", component)} danner en ring; " +
                "den er åbnet ved starten af " + opener + ".");
        }

        List<(LegacyPipelineTrace, bool)> chain = new List<(LegacyPipelineTrace, bool)>();
        End entering = first;
        while (true)
        {
            //Entering at its start runs it as drawn; at its end, reversed.
            chain.Add((byName[entering.Name], !entering.AtStart));
            End leaving = new End(entering.Name, !entering.AtStart);
            if (!partner.TryGetValue(leaving, out End next)) break;
            if (chain.Any(x => x.Item1.Name == next.Name)) break;
            entering = next;
        }
        return chain;
    }

    private static LegacyPipelineTrace Concatenate(
        string name, List<(LegacyPipelineTrace Trace, bool Reversed)> chain)
    {
        Polyline centreline = new Polyline();
        try
        {
            List<LegacyIdentitySpan> spans = new List<LegacyIdentitySpan>();
            List<LegacyCorner> corners = new List<LegacyCorner>();

            foreach ((LegacyPipelineTrace t, bool reversed) in chain)
            {
                double offset = centreline.NumberOfVertices > 1 ? centreline.Length : 0.0;
                FjvLegacyPipelineReader.AppendVertices(centreline, t.Centreline, reversed);

                foreach (LegacyIdentitySpan s in Oriented(t, reversed))
                {
                    LegacyIdentitySpan shifted = s with
                    {
                        StartDist = s.StartDist + offset,
                        EndDist = s.EndDist + offset,
                        ChangeDist = s.ChangeDist + offset,
                    };

                    //The same pipe on both sides of the join is one stretch;
                    //anything else changes AT the join, where the legacy
                    //pipelines meet.
                    if (spans.Count > 0 && spans[^1].Identity == shifted.Identity)
                        spans[^1] = spans[^1] with
                        {
                            EndDist = shifted.EndDist,
                            HoldsPipeOrPart = spans[^1].HoldsPipeOrPart || shifted.HoldsPipeOrPart,
                        };
                    else spans.Add(spans.Count == 0 ? shifted : shifted with { ChangeDist = offset });
                }
                corners.AddRange(t.Corners);
            }

            //Merged vertices shorten the chain by up to the chaining tolerance;
            //the spans tile the merged centreline exactly.
            spans[0] = spans[0] with { StartDist = 0.0, ChangeDist = 0.0 };
            for (int i = 1; i < spans.Count; i++)
                spans[i] = spans[i] with { StartDist = spans[i - 1].EndDist };
            spans[^1] = spans[^1] with { EndDist = centreline.Length };

            return new LegacyPipelineTrace(name, centreline, spans, corners);
        }
        catch
        {
            centreline.Dispose();
            throw;
        }
    }

    /// <summary>A trace's spans in the direction it runs in the chain.</summary>
    private static List<LegacyIdentitySpan> Oriented(LegacyPipelineTrace t, bool reversed)
    {
        if (!reversed) return t.Spans.ToList();

        double length = t.Centreline.Length;
        List<LegacyIdentitySpan> original = t.Spans.ToList();
        List<LegacyIdentitySpan> result = new List<LegacyIdentitySpan>(original.Count);
        for (int k = 0; k < original.Count; k++)
        {
            int i = original.Count - 1 - k;
            LegacyIdentitySpan s = original[i];
            double start = length - s.EndDist;
            //The change INTO this stretch, run backwards, is the change the
            //stretch after it made going forwards.
            double change = k == 0 ? start : length - original[i + 1].ChangeDist;
            result.Add(s with { StartDist = start, EndDist = length - s.StartDist, ChangeDist = change });
        }
        return result;
    }
}
