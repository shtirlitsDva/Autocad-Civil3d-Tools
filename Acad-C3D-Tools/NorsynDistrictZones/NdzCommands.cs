using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities.UtilsCommon.Enums;

using NetTopologySuite.Geometries;

using NorsynDistrictZones.Acad;
using NorsynDistrictZones.Model;
using NorsynDistrictZones.Pricing;
using NorsynDistrictZones.UI;

using NhsPipeType = NorsynHydraulicCalc.PipeType;
using NhsSegmentType = NorsynHydraulicCalc.SegmentType;
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

    /// <summary>
    /// No-prompt demo: render a sample 60×40 zone with one synthetic Stål DN50 pipe
    /// crossing it (clipped length 60 m → priced). Used to validate the render path.
    /// </summary>
    [CommandMethod("NDZDEMO")]
    public void NdzDemo()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc is null) return;
        Database db = doc.Database;
        Editor ed = doc.Editor;
        try
        {
            using Transaction tx = db.TransactionManager.StartTransaction();
            var gf = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
            var poly = gf.CreatePolygon(new[]
            {
                new Coordinate(0, 0), new Coordinate(60, 0),
                new Coordinate(60, 40), new Coordinate(0, 40), new Coordinate(0, 0),
            });
            var pipeLine = gf.CreateLineString(new[] { new Coordinate(-10, 20), new Coordinate(70, 20) });
            var pipes = new List<PipeSegment>
            {
                new(PipeSystemEnum.Stål, PipeTypeEnum.Enkelt, 50,
                    NhsSegmentType.Fordelingsledning, true, pipeLine, pipeLine.Length),
            };
            var catalog = PipePriceCatalog.SeedFromDefaults();
            var ms = (BlockTableRecord)tx.GetObject(
                SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

            var face = ZoneService.CreateAndRender(
                db, tx, ms, poly, catalog, pipes, Guid.NewGuid, new Random());
            tx.Commit();
            ed.WriteMessage($"\nNDZDEMO created zone #{face.Number}.");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage($"\nNDZDEMO failed:\n{ex}\n");
        }
    }

    /// <summary>Open the standalone price editor; on OK, persist catalogs and recompute zones.</summary>
    [CommandMethod("NDZPRICES")]
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

    /// <summary>Re-price and re-render all zones (e.g. after re-exporting / reloading the Xref).</summary>
    [CommandMethod("NDZRECALC")]
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

    /// <summary>Export every zone as plain AutoCAD geometry (polylines + labels) on layer NDZ-EXPORT.</summary>
    [CommandMethod("NDZEXPORTACAD")]
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

    /// <summary>Self-test of the pure domain (translator + price catalog). No drawing data required.</summary>
    [CommandMethod("NDZSELFTEST")]
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
