using System.Globalization;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.MPE.MapConnections;
using IntersectUtilities.MPE.NSAlignmentCrawl;

using static IntersectUtilities.UtilsCommon.Utils;

using CadColor = Autodesk.AutoCAD.Colors.Color;

namespace IntersectUtilities;

public partial class Intersect
{
    private const double FwdSearchDistance = 10.0;

    /// <command>FWD</command>
    /// <summary>
    /// Hops from one longitudinal profile to the one it connects to. Hover a profile view: a yellow X marks the
    /// nearest branch block or connection within 10 m of the cursor. Click and the view jumps, at the same zoom,
    /// to the connecting LP at the connection station. When several LPs meet there, pick one from the list.
    /// </summary>
    /// <category>Længdeprofiler</category>
    [CommandMethod("FWD")]
    public void Fwd()
    {
        Document? document = Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteFwd(document);
        }
        catch (System.Exception exception)
        {
            prdDbg(exception);
            document.Editor.WriteMessage($"\nFWD fejlede: {exception.Message}");
        }
    }

    private static void ExecuteFwd(Document document)
    {
        Editor editor = document.Editor;
        LpNetworkSnapshot snapshot = LpConnectionResolver.Build(document.Database);
        if (!snapshot.Lines.Values.Any(l => l.View is not null))
        {
            editor.WriteMessage("\nIngen længdeprofiler i tegningen.");
            return;
        }

        PromptPointResult pick;
        using (NSAlignmentCrawlStartMarker marker = new(CadColor.FromRgb(255, 255, 0)))
        {
            void OnMove(object? sender, PointMonitorEventArgs args)
            {
                try
                {
                    Point3d p = args.Context.ComputedPoint;
                    LpHopPoint? hop = snapshot.FindHop(new Point2d(p.X, p.Y), FwdSearchDistance, out _);
                    if (hop is not null)
                    {
                        marker.Show(document, hop.Marker);
                    }
                    else
                    {
                        marker.Clear();
                    }
                }
                catch
                {
                    // A PointMonitor handler must never throw — a bad tick simply shows no marker.
                }
            }

            editor.PointMonitor += OnMove;
            try
            {
                pick = editor.GetPoint(new PromptPointOptions("\nVælg afgreningsblok eller punkt i længdeprofil: "));
            }
            finally
            {
                editor.PointMonitor -= OnMove;
            }
        }

        if (pick.Status != PromptStatus.OK)
        {
            return;
        }

        Point2d picked = new(pick.Value.X, pick.Value.Y);
        LpHopPoint? chosen = snapshot.FindHop(picked, FwdSearchDistance, out LpLine? host);
        if (host is null)
        {
            editor.WriteMessage("\nPunktet ligger ikke i et længdeprofil.");
            return;
        }

        if (chosen is null)
        {
            editor.WriteMessage($"\nIngen forbindelse inden for {FwdSearchDistance:0} m.");
            return;
        }

        if (chosen.Targets.Count == 0)
        {
            editor.WriteMessage(chosen.NaName is not null
                ? $"\n{chosen.NaName} har ikke længdeprofil."
                : "\nIngen forbindelse fundet.");
            return;
        }

        LpHopTarget? target = chosen.Targets.Count == 1 ? chosen.Targets[0] : PickTarget(editor, chosen);
        if (target is null)
        {
            return;
        }

        if (!snapshot.Lines.TryGetValue(target.Lp, out LpLine? line) || line.View is null)
        {
            editor.WriteMessage($"\nLængdeprofil {target.Lp} findes ikke i tegningen.");
            return;
        }

        LpViewJumper.JumpTo(editor, line.View, target.Station);
        editor.WriteMessage(
            $"\n{host.Name} → {target.Lp} st. {target.Station.ToString("0.00", CultureInfo.InvariantCulture)}");
    }

    /// <summary>Several LPs meet at the spot: offer them as keywords, which AutoCAD shows as a list at the cursor.</summary>
    private static LpHopTarget? PickTarget(Editor editor, LpHopPoint hop)
    {
        PromptKeywordOptions options = new("\nFlere længdeprofiler mødes her. Vælg")
        {
            AppendKeywordsToMessage = true,
        };

        Dictionary<string, LpHopTarget> byKeyword = new();
        foreach (LpHopTarget target in hop.Targets)
        {
            string keyword = new(target.Lp.Where(char.IsLetterOrDigit).ToArray());
            if (keyword.Length == 0 || byKeyword.ContainsKey(keyword))
            {
                keyword = $"LP{byKeyword.Count + 1}";
            }

            byKeyword[keyword] = target;
            options.Keywords.Add(keyword, keyword, target.Lp);
        }

        options.Keywords.Default = byKeyword.Keys.First();
        PromptResult result = editor.GetKeywords(options);
        return result.Status == PromptStatus.OK && byKeyword.TryGetValue(result.StringResult, out LpHopTarget? chosen)
            ? chosen
            : null;
    }
}
