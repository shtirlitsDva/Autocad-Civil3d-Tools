using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AutoProfileSolver.Alignment;

/// <summary>Loads a compact case JSON (surface_profile / pipe_sizes / utilities / forbidden)
/// into a <see cref="CaseSpec"/> — port of api.case_spec_from_mapping (compact shape).</summary>
public static class CaseIo
{
    public static CaseSpec LoadFile(string path)
        => FromJson(File.ReadAllText(path));

    public static CaseSpec FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        string name = root.TryGetProperty("name", out var nm) ? nm.GetString() ?? "external" : "external";

        // surface profile (Nx2), sorted by station
        var sp = root.GetProperty("surface_profile").EnumerateArray()
            .Select(p => { var a = p.EnumerateArray().ToArray(); return (a[0].GetDouble(), a[1].GetDouble()); })
            .OrderBy(t => t.Item1).ToArray();
        double[] surfaceS = sp.Select(t => t.Item1).ToArray();
        double[] surfaceY = sp.Select(t => t.Item2).ToArray();

        var pipeSizes = root.GetProperty("pipe_sizes").EnumerateArray().Select(e =>
        {
            double sLo = e.GetProperty("s_lo").GetDouble();
            double sHi = e.GetProperty("s_hi").GetDouble();
            double rMin = e.GetProperty("r_min_m").GetDouble();
            double jod = e.TryGetProperty("jod_m", out var j) ? j.GetDouble()
                : e.GetProperty("Kod").GetDouble() / 1000.0;
            return new PipeSize(Math.Min(sLo, sHi), Math.Max(sLo, sHi), rMin, jod);
        }).OrderBy(p => p.SLo).ToList();
        if (pipeSizes.Count == 0) throw new InvalidOperationException("case needs >=1 pipe size");

        var rawUtils = root.TryGetProperty("utilities", out var ut)
            ? ut.EnumerateArray().Select(e =>
              {
                  double sLo = e.GetProperty("s_lo").GetDouble();
                  double sHi = e.GetProperty("s_hi").GetDouble();
                  double yLo = e.GetProperty("y_lo").GetDouble();
                  double yHi = e.GetProperty("y_hi").GetDouble();
                  string kind = e.TryGetProperty("topology_kind", out var k) ? k.GetString() ?? "either" : "either";
                  return new UtilityBox(Math.Min(sLo, sHi), Math.Max(sLo, sHi),
                                        Math.Min(yLo, yHi), Math.Max(yLo, yHi), kind);
              }).ToList()
            : new List<UtilityBox>();

        var forbidden = root.TryGetProperty("forbidden", out var fb)
            ? fb.EnumerateArray().Select(p => { var a = p.EnumerateArray().ToArray();
                  double x = a[0].GetDouble(), y = a[1].GetDouble();
                  return (Math.Min(x, y), Math.Max(x, y)); }).ToList()
            : new List<(double, double)>();

        // Classify utilities (below_only vs either) using the cover target over each box range.
        var provisional = new CaseSpec(name, surfaceS, surfaceY, pipeSizes, rawUtils, forbidden);
        var utilities = rawUtils.Select(b => Classify(b, provisional)).ToList();
        return new CaseSpec(name, surfaceS, surfaceY, pipeSizes, utilities, forbidden);
    }

    private static UtilityBox Classify(UtilityBox box, CaseSpec spec)
    {
        if (box.TopologyKind == "below_only" || box.SHi <= box.SLo) return box;
        double tgtMin = double.PositiveInfinity;
        for (int i = 0; i < 16; i++)
        {
            double s = box.SLo + (box.SHi - box.SLo) * (i / 15.0);
            tgtMin = Math.Min(tgtMin, spec.TargetAt(s));
        }
        string kind = box.YHi >= tgtMin ? "below_only" : "either";
        return box with { TopologyKind = kind };
    }
}
