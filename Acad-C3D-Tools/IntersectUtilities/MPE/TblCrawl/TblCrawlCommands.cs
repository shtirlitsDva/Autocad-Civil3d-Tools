using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using CSharpFunctionalExtensions;

using IntersectUtilities.MPE.NSAlignmentCrawl;
using IntersectUtilities.MPE.TblCrawl;
using IntersectUtilities.Tbl;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NetTopologySuite.Geometries;

using Norsyn.AecPropertySets;

using CadColor = Autodesk.AutoCAD.Colors.Color;
using PipeSchedule = IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using Result = CSharpFunctionalExtensions.Result;

namespace IntersectUtilities;

public partial class Intersect
{
    // The tags of the last stretch, offered again on the next TBLCRAWL in this session.
    private static Maybe<string> s_tblLastBelægning = Maybe<string>.None;
    private static Maybe<string> s_tblLastVejklasse = Maybe<string>.None;

    /// <command>TBLINIT</command>
    /// <summary>
    /// Prepares a drawing for TBL stretches: creates a layer per Belægning (0-TBL-STRÆK-VEJBANE and so on,
    /// each with its own colour) and the DriOmråder property set definition when they are missing, and
    /// checks that an FJV Fremtid xref is attached. Running it again changes nothing; a layer that exists
    /// keeps the colour it has.
    /// </summary>
    /// <category>Tilbudsliste</category>
    [CommandMethod("TBLINIT")]
    public void TblInit()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteTblInit(document);
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "TBLINIT", exception);
        }
    }

    /// <command>TBLCRAWL</command>
    /// <summary>
    /// Draws a TBL stretch: asks for Belægning and Vejklasse (the last ones are offered again), then a start
    /// and an end on the FJV network of the Fremtid xref, as NSALIGNMENTCRAWL takes them. The path is drawn
    /// on the Belægning's layer (made by TBLINIT) with its tags in DriOmråder. Where the path runs on frem or
    /// retur, the partner pipe is crawled too and drawn as its own stretch with the same tags and layer.
    /// Older stretches on any TBL layer are cut where a new one lies on them, so no two stretches share pipe.
    /// </summary>
    /// <category>Tilbudsliste</category>
    [CommandMethod("TBLCRAWL")]
    public void TblCrawl()
    {
        Document? document = GetActiveDocument();
        if (document is null)
        {
            return;
        }

        try
        {
            ExecuteTblCrawl(document);
        }
        catch (System.Exception exception)
        {
            HandleCommandException(document, "TBLCRAWL", exception);
        }
    }

    private static void ExecuteTblInit(Document document)
    {
        Editor editor = document.Editor;
        Database db = document.Database;
        using DocumentLock documentLock = document.LockDocument();
        using Transaction tx = db.TransactionManager.StartTransaction();

        LayerTable layers = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);
        List<TblBelægning> missingLayers = TblBelægninger.All.Where(b => !layers.Has(b.Layer)).ToList();
        if (missingLayers.Count > 0)
        {
            layers.UpgradeOpen();
        }

        foreach (TblBelægning belægning in missingLayers)
        {
            LayerTableRecord layer = new()
            {
                Name = belægning.Layer,
                Color = CadColor.FromColorIndex(ColorMethod.ByAci, belægning.ColorIndex),
            };
            layers.Add(layer);
            tx.AddNewlyCreatedDBObject(layer, true);
        }

        if (missingLayers.Count > 0)
        {
            editor.WriteMessage(
                $"\nLag oprettet: {string.Join(", ", missingLayers.Select(b => $"{b.Layer} (farve {b.ColorIndex})"))}.");
        }

        if (missingLayers.Count < TblBelægninger.All.Count)
        {
            editor.WriteMessage(
                $"\nLag findes: {string.Join(", ", TblBelægninger.All.Except(missingLayers).Select(b => b.Layer))}.");
        }

        string setName = PSetDefs.DefinedSets.DriOmråder.ToString();
        DictionaryPropertySetDefinitions definitions = new(db);
        bool hadSet = definitions.Has(setName, tx);
        _ = new PropertySetManager(db, PSetDefs.DefinedSets.DriOmråder);
        PSetDefs.DriOmråder def = new();
        string[] needed = [def.Vejklasse.Name, def.Belægning.Name];
        PropertySetDefinition setDefinition = (PropertySetDefinition)tx.GetObject(definitions.GetAt(setName), OpenMode.ForRead);
        HashSet<string> defined = setDefinition.Definitions.Cast<PropertyDefinition>().Select(p => p.Name).ToHashSet();
        string[] missing = needed.Where(n => !defined.Contains(n)).ToArray();
        editor.WriteMessage(hadSet
            ? $"\nPS-definitionen {setName} findes."
            : $"\nPS-definitionen {setName} oprettet.");
        if (missing.Length > 0)
        {
            editor.WriteMessage($"\nADVARSEL: {setName} mangler {string.Join(", ", missing)}: TBLCRAWL kan ikke skrive dem.");
        }

        Maybe.From(NSAlignmentCrawlNetworkReader.FindXref(db, tx, NSAlignmentCrawlConstants.XrefName)).Match(
            xref =>
            {
                BlockTableRecord btr = (BlockTableRecord)tx.GetObject(xref.BlockTableRecord, OpenMode.ForRead);
                editor.WriteMessage($"\nFremtid-xref: {btr.Name}, {btr.XrefStatus}, {btr.PathName}");
            },
            () => editor.WriteMessage(
                $"\nADVARSEL: ingen FJV Fremtid-xref (navn eller fil med \"{NSAlignmentCrawlConstants.XrefName}\") er vedhæftet. " +
                "TBLCRAWL tegner på xref'ens rør, så vedhæft den først."));

        tx.Commit();
    }

    private sealed record StretchTags(TblBelægning Belægning, string Vejklasse);

    private static void ExecuteTblCrawl(Document document)
    {
        Editor editor = document.Editor;
        Database db = document.Database;
        if (!TblBelægninger.All.Any(b => HasLayer(db, b.Layer)))
        {
            editor.WriteMessage($"\nIngen TBL-lag ({TblBelægninger.BareLayer}-*) i tegningen. Kør TBLINIT først.");
            return;
        }

        NSAlignmentCrawlSnapshot snapshot;
        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            bool ok = NSAlignmentCrawlNetworkReader.TryRead(
                db, tr, NSAlignmentCrawlConstants.XrefName, out snapshot, out string readError);
            tr.Commit();
            if (!ok)
            {
                editor.WriteMessage($"\n{readError}");
                return;
            }
        }

        if (snapshot.Pipes.Count == 0)
        {
            editor.WriteMessage("\nIngen FJV-rør fundet i xref'en.");
            return;
        }

        AskTags(editor).Match(
            tags =>
            {
                if (!HasLayer(db, tags.Belægning.Layer))
                {
                    editor.WriteMessage($"\nLag {tags.Belægning.Layer} findes ikke. Kør TBLINIT først.");
                    return;
                }

                using CrawlNetwork net = NSAlignmentCrawlGraphBuilder.Build(snapshot);
                CrawlPinSet pins = CrawlPinSet.FromSnapshot(snapshot);
                PickStretch(document, net, pins).Match(
                    vertices => BakeStretches(document, snapshot, net, pins, vertices, tags),
                    error => editor.WriteMessage($"\n{error.Message}"));
            },
            () => editor.WriteMessage("\nAnnulleret."));
    }

    private static bool HasLayer(Database db, string name)
    {
        using Transaction tr = db.TransactionManager.StartOpenCloseTransaction();
        return ((LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead)).Has(name);
    }

    private static Maybe<StretchTags> AskTags(Editor editor) =>
        AskKeyword(editor, "\nAngiv belægning", TblBelægninger.Keywords, s_tblLastBelægning)
            .Bind(belægning => AskKeyword(editor, "\nAngiv vejklasse", TblStretch.Vejklasser, s_tblLastVejklasse)
                .Map(vejklasse =>
                {
                    s_tblLastBelægning = Maybe.From(belægning);
                    s_tblLastVejklasse = Maybe.From(vejklasse);
                    return new StretchTags(TblBelægninger.FromKeyword(belægning), vejklasse);
                }));

    // Enter takes the last value when there is one.
    private static Maybe<string> AskKeyword(Editor editor, string message, string[] keywords, Maybe<string> last)
    {
        PromptKeywordOptions options = new(message);
        foreach (string keyword in keywords)
        {
            options.Keywords.Add(keyword);
        }

        last.Execute(keyword =>
        {
            options.Keywords.Default = keyword;
            options.AllowNone = true;
        });

        PromptResult result = editor.GetKeywords(options);
        return result.Status switch
        {
            PromptStatus.OK => Maybe.From(result.StringResult),
            PromptStatus.None => last,
            _ => Maybe<string>.None,
        };
    }

    /// <summary>The start and end picks with NSALIGNMENTCRAWL's start X and live preview.</summary>
    private static Result<List<(Point2d Pt, double OutBulge)>, AecError> PickStretch(
        Document document, CrawlNetwork net, CrawlPinSet pins)
    {
        Editor editor = document.Editor;
        PromptPointResult startResult;
        using (NSAlignmentCrawlStartMarker startCursor = new(CadColor.FromRgb(255, 255, 0)))
        {
            void OnStartMove(object? sender, PointMonitorEventArgs args)
            {
                try
                {
                    if (CrawlSession.TrySnapToNetwork(net, args.Context.ComputedPoint, out Point2d snapped))
                    {
                        startCursor.Show(document, snapped);
                    }
                    else
                    {
                        startCursor.Clear();
                    }
                }
                catch
                {
                    // A PointMonitor handler must never throw: a bad tick simply shows no marker.
                }
            }

            editor.PointMonitor += OnStartMove;
            try
            {
                startResult = editor.GetPoint(new PromptPointOptions("\nVælg startpunkt på røret (X følger nettet): "));
            }
            finally
            {
                editor.PointMonitor -= OnStartMove;
            }
        }

        if (startResult.Status != PromptStatus.OK)
        {
            return Result.Failure<List<(Point2d Pt, double OutBulge)>, AecError>(AecError.Drawing("Annulleret."));
        }

        CrawlSession? session = CrawlSession.Create(net, startResult.Value, out string startError);
        return Maybe.From(session)
            .ToResult(AecError.Drawing(startError))
            .Bind(started => PickEnd(document, started, pins));
    }

    private static Result<List<(Point2d Pt, double OutBulge)>, AecError> PickEnd(
        Document document, CrawlSession session, CrawlPinSet pins)
    {
        List<(Point2d Pt, double OutBulge)>? BuildPreview(Point3d cursor)
            => session.TryBuildPath(cursor, out List<(Point2d Pt, double OutBulge)> seg) && seg.Count >= 2
                ? seg
                : null;

        using NSAlignmentCrawlPreviewManager preview = new();
        using NSAlignmentCrawlStartMarker startMarker = new(CadColor.FromRgb(255, 255, 0));
        using NSAlignmentCrawlPointTracker tracker = new(document, preview, BuildPreview, pins);
        startMarker.Show(document, session.StartPosition);

        PromptPointResult endResult = document.Editor.GetPoint(
            new PromptPointOptions("\nVælg slutpunkt (forhåndsvisning følger markøren): "));
        if (endResult.Status != PromptStatus.OK)
        {
            return Result.Failure<List<(Point2d Pt, double OutBulge)>, AecError>(AecError.Drawing("Annulleret."));
        }

        return session.TryBuildPath(endResult.Value, out List<(Point2d Pt, double OutBulge)> vertices)
            ? Result.Success<List<(Point2d Pt, double OutBulge)>, AecError>(vertices)
            : Result.Failure<List<(Point2d Pt, double OutBulge)>, AecError>(AecError.Drawing("Ingen sti fundet mellem de to punkter."));
    }

    private static void BakeStretches(
        Document document, NSAlignmentCrawlSnapshot snapshot, CrawlNetwork net, CrawlPinSet pins,
        List<(Point2d Pt, double OutBulge)> vertices, StretchTags tags)
    {
        Editor editor = document.Editor;
        Database db = document.Database;
        string layer = tags.Belægning.Layer;
        Maybe<Polyline> built = Maybe.From(NSAlignmentCrawlPolylineBuilder.Build(vertices, layer, pins));
        built.Match(
            main =>
            {
                LineString mainLine = TblStretchGeometry.ToLineString(main);
                List<(PipeTypeEnum Type, LineString Line)> pipes = snapshot.Pipes
                    .Select(p => (PipeSchedule.GetPipeType(p.Layer), TblStretchGeometry.ToLineString(p.Vertices)))
                    .ToList();
                List<PartnerLine> partners = [];
                foreach (EnkeltRun run in TblPartner.EnkeltRuns(mainLine, pipes))
                {
                    TblPartner.Crawl(run, mainLine, pipes, net, pins, layer).Match(
                        partners.Add,
                        error => editor.WriteMessage($"\nPartner til {run} sprunget over: {error.Message}."));
                }

                using DocumentLock documentLock = document.LockDocument();
                using Transaction tx = db.TransactionManager.StartTransaction();
                PropertySetManager psm = new(db, PSetDefs.DefinedSets.DriOmråder);
                PSetDefs.DriOmråder def = new();
                List<LineString> newLines = [mainLine, .. partners.Select(p => TblStretchGeometry.ToLineString(p.Polyline))];
                int cut = TblStretchCutter.CutUnder(db, tx, newLines, psm, def);

                BlockTableRecord modelSpace = db.GetModelspaceForWrite();
                foreach (Polyline polyline in partners.Select(p => p.Polyline).Prepend(main))
                {
                    modelSpace.AppendEntity(polyline);
                    tx.AddNewlyCreatedDBObject(polyline, true);
                    psm.WritePropertyString(polyline, def.Belægning, tags.Belægning.Name);
                    psm.WritePropertyString(polyline, def.Vejklasse, tags.Vejklasse);
                }

                tx.Commit();

                string tagText = $"{tags.Belægning.Name}, vejklasse {tags.Vejklasse}, lag {layer}";
                editor.WriteMessage(
                    $"\nTBL-stræk {main.Handle}: {main.Length:0.000} m, {tagText}; " +
                    $"partnerlinjer {partners.Count}; skårne stykker af ældre stræk {cut}.");
                foreach (PartnerLine partner in partners)
                {
                    editor.WriteMessage(
                        $"\nTBL-stræk {partner.Polyline.Handle}: {partner.Polyline.Length:0.000} m, {tagText}; " +
                        $"partner på {partner.Run.Partner} til {partner.Run} " +
                        $"(stykket {partner.RunLength:0.000} m, middelafstand {partner.MeanDistance:0.00} m).");
                }
            },
            () => editor.WriteMessage("\nFor få punkter til at tegne."));
    }
}
