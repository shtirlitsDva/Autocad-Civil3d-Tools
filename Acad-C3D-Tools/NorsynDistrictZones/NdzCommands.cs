using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using NorsynDistrictZones.Acad;
using NorsynDistrictZones.Pricing;
using NorsynDistrictZones.Topology;
using NorsynDistrictZones.UI;

using NhsPipeType = NorsynHydraulicCalc.PipeType;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using NsContainer = NorsynObjectsInterop.NorsynContainer;

namespace NorsynDistrictZones;

/// <summary>Command surface for District Zones. Grows phase by phase.</summary>
public sealed class NdzCommands
{
    /// <summary>
    /// Create a zone from a CLOSED polyline: builds a NorsynContainer (transparent
    /// colored MPolygon + number/price labels), prices the Xref pipes inside it, and
    /// consumes the source polyline. The interim command-driven entry to the core slice;
    /// the automatic reactor (P8) will call the same ZoneService primitives.
    /// </summary>
    [CommandMethod("NDZZONE")]
    [CommandSummary("Make a zone from a closed polyline (prices the Xref pipes inside).")]
    public void NdzZone()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        try
        {
            var peo = new PromptEntityOptions("\nSelect a CLOSED polyline to make a zone: ");
            peo.SetRejectMessage("\nMust be a polyline.");
            peo.AddAllowedClass(typeof(AcPolyline), exactMatch: false);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using Transaction tx = db.TransactionManager.StartTransaction();
            if (tx.GetObject(per.ObjectId, OpenMode.ForRead) is not AcPolyline pl || !pl.Closed)
            {
                ed.WriteMessage("\nSelected polyline is not closed.");
                return;
            }

            var polygon = AcadNts.ToPolygon(pl, Matrix3d.Identity);
            if (polygon is null)
            {
                ed.WriteMessage("\nCould not build a valid polygon from the polyline.");
                return;
            }

            var pipes = PipeReader.ReadFromXrefs(db, tx, null);
            var catalog = CatalogStore.GetActive(db);
            var ms = (BlockTableRecord)tx.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

            var face = ZoneService.CreateAndRender(
                db, tx, ms, polygon, catalog, pipes, Guid.NewGuid, new Random());

            pl.UpgradeOpen();
            pl.Erase(); // the source polyline becomes the zone

            tx.Commit();
            ed.WriteMessage($"\nCreated zone #{face.Number} — {pipes.Count} Xref pipe(s) in scope.");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nNDZZONE failed:\n{ex}\n");
        }
    }

    /// <summary>Open the standalone price editor; on OK, persist catalogs and recompute zones.</summary>
    [CommandMethod("NDZPRICES")]
    [CommandSummary("Open the price-catalog editor; on save, recompute all zones.")]
    public void NdzPrices()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        try
        {
            var (cats, active) = CatalogStore.LoadAll(db);
            var vm = new PriceEditorViewModel(cats, active);
            var win = new PriceEditorWindow(vm);
            AcApp.ShowModalWindow(win);
            if (vm.Saved)
            {
                CatalogStore.SaveAll(db, vm.Catalogs, vm.ActiveName);
                int n = ZoneService.RecomputeAll(db);
                ed.WriteMessage($"\nSaved price catalogs; recomputed {n} zone(s) with '{vm.ActiveName}'.\n");
            }
        }
        catch (System.Exception ex) { ed.WriteMessage($"\nNDZPRICES failed:\n{ex}\n"); }
    }

    /// <summary>Rename a zone — pick it, type a name; the name shows above the price and persists.</summary>
    [CommandMethod("NDZRENAME")]
    [CommandSummary("Rename a zone (name shows above the price and persists).")]
    public void NdzRename()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        try
        {
            var peo = new PromptEntityOptions("\nSelect a zone to rename: ");
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using Transaction tx = db.TransactionManager.StartTransaction();
            if (tx.GetObject(per.ObjectId, OpenMode.ForRead) is not Entity ent ||
                ent.GetRXClass().Name != "NorsynContainer")
            {
                ed.WriteMessage("\nThat is not a District Zone.");
                return;
            }

            var face = ZoneXData.ReadFace(ent);
            if (face is null) { ed.WriteMessage("\nThat zone carries no NDZ data."); return; }

            var pso = new PromptStringOptions($"\nName for zone #{face.Number} <{face.Name}>: ") { AllowSpaces = true };
            PromptResult psr = ed.GetString(pso);
            if (psr.Status != PromptStatus.OK) return;
            face.Name = psr.StringResult?.Trim() ?? string.Empty;

            var nc = (NsContainer)tx.GetObject(per.ObjectId, OpenMode.ForWrite);
            var pipes = PipeReader.ReadFromXrefs(db, tx, null);
            string price = ZoneService.PriceFace(face, CatalogStore.GetActive(db), pipes, out _);
            ZoneRenderer.Update(db, tx, nc, face, price);
            tx.Commit();
            Reactors.ZoneSession.For(db).Clear();
            ed.WriteMessage($"\nRenamed zone #{face.Number} to '{face.Name}'.\n");
        }
        catch (System.Exception ex) { ed.WriteMessage($"\nNDZRENAME failed:\n{ex}\n"); }
    }

    /// <summary>
    /// Merge two adjacent zones into one. Pick the zone whose identity (number/name/
    /// colour) survives, then the zone folded into it; the boundary between them is
    /// dissolved (NTS union) and the result is re-priced. Rejects non-adjacent zones.
    /// </summary>
    [CommandMethod("NDZMERGE")]
    [CommandSummary("Merge two adjacent zones into one (the first zone's identity is kept).")]
    public void NdzMerge()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        try
        {
            ObjectId idA = PickZone(ed, "\nSelect the zone to KEEP (its number/name/colour survives): ");
            if (idA.IsNull) return;
            ObjectId idB = PickZone(ed, "\nSelect the zone to MERGE INTO it: ");
            if (idB.IsNull) return;
            if (idA == idB) { ed.WriteMessage("\nPick two different zones."); return; }

            using Transaction tx = db.TransactionManager.StartTransaction();
            ZoneFace? faceA = ReadZoneFace(tx, idA);
            ZoneFace? faceB = ReadZoneFace(tx, idB);
            if (faceA is null || faceB is null)
            {
                ed.WriteMessage("\nBoth selections must be District Zones with NDZ data.");
                return;
            }

            var merged = ZoneGeometryOps.Merge(faceA.Polygon, faceB.Polygon);
            if (merged is null)
            {
                ed.WriteMessage(
                    "\nThose zones are not adjacent (their union is not a single area) — nothing merged.");
                return;
            }
            faceA.Polygon = merged; // keep A's identity, take the dissolved geometry

            var nc = (NsContainer)tx.GetObject(idA, OpenMode.ForWrite);
            var pipes = PipeReader.ReadFromXrefs(db, tx, null);
            string price = ZoneService.PriceFace(faceA, CatalogStore.GetActive(db), pipes, out _);
            ZoneRenderer.Update(db, tx, nc, faceA, price);

            ((Entity)tx.GetObject(idB, OpenMode.ForWrite)).Erase();

            tx.Commit();
            Reactors.ZoneSession.For(db).Clear();
            ed.WriteMessage($"\nMerged zone #{faceB.Number} into #{faceA.Number}.\n");
        }
        catch (System.Exception ex) { ed.WriteMessage($"\nNDZMERGE failed:\n{ex}\n"); }
    }

    private static ObjectId PickZone(Editor ed, string prompt)
    {
        PromptEntityResult per = ed.GetEntity(new PromptEntityOptions(prompt));
        return per.Status == PromptStatus.OK ? per.ObjectId : ObjectId.Null;
    }

    private static ZoneFace? ReadZoneFace(Transaction tx, ObjectId id) =>
        tx.GetObject(id, OpenMode.ForRead) is Entity ent && ent.GetRXClass().Name == "NorsynContainer"
            ? ZoneXData.ReadFace(ent)
            : null;

    /// <summary>Re-price and re-render all zones (e.g. after re-exporting / reloading the Xref).</summary>
    [CommandMethod("NDZRECALC")]
    [CommandSummary("Re-price and re-render all zones (after re-export / Xref reload).")]
    public void NdzRecalc()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        try
        {
            int n = ZoneService.RecomputeAll(doc.Database);
            doc.Editor.WriteMessage($"\nRecomputed {n} zone(s).\n");
        }
        catch (System.Exception ex) { doc.Editor.WriteMessage($"\nNDZRECALC failed:\n{ex}\n"); }
    }

    /// <summary>
    /// Set the zone-label text height, remembered globally for all drawings (enter 0 for
    /// auto). Re-renders the current drawing's zones so the change shows immediately; the
    /// AutoCAD export honours the same size.
    /// </summary>
    [CommandMethod("NDZTEXTSIZE")]
    [CommandSummary("Set zone-label text height, remembered for all drawings (0 = auto).")]
    public void NdzTextSize()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Editor ed = doc.Editor;
        try
        {
            double? cur = GlobalSettings.LabelHeight;
            var pdo = new PromptDoubleOptions(
                $"\nZone label text height (0 = auto) <{(cur is double c ? c.ToString("0.###") : "auto")}>: ")
            {
                AllowNegative = false,
                AllowNone = true,            // Enter keeps the current value
                UseDefaultValue = cur is double,
                DefaultValue = cur ?? 0.0,
            };
            PromptDoubleResult r = ed.GetDouble(pdo);
            if (r.Status != PromptStatus.OK) return;

            GlobalSettings.LabelHeight = r.Value > 0 ? r.Value : (double?)null;
            int n = ZoneService.RecomputeAll(doc.Database);
            ed.WriteMessage(
                $"\nLabel text height = {(r.Value > 0 ? r.Value.ToString("0.###") : "auto")} " +
                $"(global); re-rendered {n} zone(s).\n");
        }
        catch (System.Exception ex) { ed.WriteMessage($"\nNDZTEXTSIZE failed:\n{ex}\n"); }
    }

    /// <summary>
    /// Set the zone fill transparency (0 % = opaque … 90 % = faintest), remembered globally
    /// for all drawings. Re-renders the current drawing's zones so the change shows
    /// immediately. (Only the live zone fill uses it — the AutoCAD export is plain geometry.)
    /// </summary>
    [CommandMethod("NDZTRANSPARENCY")]
    [CommandSummary("Set zone fill transparency 0-90% (0 = opaque), remembered for all drawings.")]
    public void NdzTransparency()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Editor ed = doc.Editor;
        try
        {
            int cur = GlobalSettings.ZoneTransparencyPercent;
            var pio = new PromptIntegerOptions(
                $"\nZone fill transparency in % (0 = opaque, 90 = faintest) <{cur}>: ")
            {
                AllowNegative = false,
                AllowNone = true,            // Enter keeps the current value
                UseDefaultValue = true,
                DefaultValue = cur,
                LowerLimit = 0,
                UpperLimit = 90,
            };
            PromptIntegerResult r = ed.GetInteger(pio);
            if (r.Status != PromptStatus.OK) return;

            GlobalSettings.ZoneTransparencyPercent = r.Value;
            int n = ZoneService.RecomputeAll(doc.Database);
            ed.WriteMessage($"\nZone transparency = {r.Value}% (global); re-rendered {n} zone(s).\n");
        }
        catch (System.Exception ex) { ed.WriteMessage($"\nNDZTRANSPARENCY failed:\n{ex}\n"); }
    }

    /// <summary>Export every zone as plain AutoCAD geometry (polylines + labels) on layer NDZ-EXPORT.</summary>
    [CommandMethod("NDZEXPORTACAD")]
    [CommandSummary("Export zones as plain AutoCAD geometry on layer NDZ-EXPORT.")]
    public void NdzExportAcad()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        try
        {
            using Transaction tx = db.TransactionManager.StartTransaction();
            var ms = (BlockTableRecord)tx.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
            int n = ZoneExporter.ExportToAutoCad(db, tx, ms);
            tx.Commit();
            ed.WriteMessage($"\nExported {n} zone(s) to layer {ZoneExporter.ExportLayer}.\n");
        }
        catch (System.Exception ex) { ed.WriteMessage($"\nNDZEXPORTACAD failed:\n{ex}\n"); }
    }

    /// <summary>Export every zone to a GeoJSON file (polygons + number/name/price/area properties).</summary>
    [CommandMethod("NDZEXPORTGEOJSON")]
    [CommandSummary("Export zones to a GeoJSON file (polygons + number/name/price/area).")]
    public void NdzExportGeoJson()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        try
        {
            string json;
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                json = ZoneExporter.ExportGeoJson(db, tx);
                tx.Commit();
            }

            string dwg = db.Filename;
            string path = string.IsNullOrEmpty(dwg)
                ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ndz_zones.geojson")
                : System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(dwg)!,
                    System.IO.Path.GetFileNameWithoutExtension(dwg) + "_zones.geojson");

            System.IO.File.WriteAllText(path, json);
            ed.WriteMessage($"\nExported zones to {path}\n");
        }
        catch (System.Exception ex) { ed.WriteMessage($"\nNDZEXPORTGEOJSON failed:\n{ex}\n"); }
    }

    /// <summary>
    /// Export a per-area pipe length &amp; price breakdown to a styled Excel workbook: one table
    /// per zone (coloured to match the drawing), with a zone subtotal and a grand total. Prices
    /// are computed live via the same path as the model-space labels, so the grand total matches.
    /// </summary>
    [CommandMethod("NDZEXPORTPRISER")]
    [CommandSummary("Export a per-area pipe length & price breakdown to Excel.")]
    public void NdzExportPriser()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        try
        {
            var zones = new List<NorsynDistrictZones.Export.ZoneBreakdown>();
            string catalogName;
            double grand = 0;

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                var pipes = PipeReader.ReadFromXrefs(db, tx, null);
                var catalog = CatalogStore.GetActive(db);
                catalogName = catalog.Name;
                var warned = new HashSet<(NhsPipeType Type, int Dn)>();
                foreach (var (_, face) in ZoneReader.ReadAll(db, tx))
                {
                    ZoneService.PriceFace(face, catalog, pipes, out var zp, warned);
                    zones.Add(new NorsynDistrictZones.Export.ZoneBreakdown(
                        face.Number, face.Name ?? string.Empty, face.ColorArgb,
                        PriceBreakdown.Rows(zp), zp.Total, zp.AnyProvisional));
                    grand += zp.Total;
                }
                tx.Commit();
            }

            if (zones.Count == 0) { ed.WriteMessage("\nNo zones to export."); return; }

            string dwg = db.Filename;
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel-projektmappe (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                AddExtension = true,
                FileName = string.IsNullOrEmpty(dwg)
                    ? "ndz_priser.xlsx"
                    : System.IO.Path.GetFileNameWithoutExtension(dwg) + "_priser.xlsx",
            };
            if (!string.IsNullOrEmpty(dwg))
                dlg.InitialDirectory = System.IO.Path.GetDirectoryName(dwg);
            if (dlg.ShowDialog() != true) return;

            NorsynDistrictZones.Export.PriceBreakdownWorkbook.Save(dlg.FileName, zones, grand, catalogName);
            ed.WriteMessage($"\nEksporterede prisoverslag for {zones.Count} zone(r) til {dlg.FileName}\n");
        }
        catch (System.Exception ex) { ed.WriteMessage($"\nNDZEXPORTPRISER failed:\n{ex}\n"); }
    }

    /// <summary>Self-test of the pure domain (translator + price catalog). No drawing data required.</summary>
    [CommandMethod("NDZSELFTEST")]
    [CommandSummary("Self-test the pricing domain — translator + catalog (no drawing data).")]
    public void NdzSelfTest()
    {
        Editor? ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
        if (ed is null) return;
        try
        {
            PipePriceCatalog cat = PipePriceCatalog.SeedFromDefaults();
            ed.WriteMessage($"\nNDZ self-test — catalog '{cat.Name}' seeded with {cat.Entries.Count} price rows.");

            PipePriceEntry? steel = cat.Find(NhsPipeType.Stål, 50);
            if (steel is not null)
                ed.WriteMessage($"\n  Stål DN50: {steel.PricePerMeter:N0} DKK/m, fitting {steel.PricePerFitting:N0} DKK.");

            string[] layers =
            {
                "FJV-ENKELT-DN020", "FJV-TWIN-PRTFLEXL050",
                "228-1642|FJV-FREM-ALUPEX032", "FJV-ENKELT-AQTHRM11026",
                "FJV-ENKELT-PE160", "0-NotAPipe",
            };
            foreach (string lay in layers)
            {
                if (PipeTypeTranslator.TryParseLayer(lay, out var sys, out var typ, out int dn))
                    ed.WriteMessage($"\n  '{lay}'  ->  {sys} / {typ} / DN{dn}");
                else
                    ed.WriteMessage($"\n  '{lay}'  ->  (not an FJV pipe layer)");
            }
            ed.WriteMessage("\nNDZ self-test done.\n");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nNDZSELFTEST failed:\n{ex}\n");
        }
    }
}
