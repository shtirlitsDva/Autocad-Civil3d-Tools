using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.DataShortcuts;
using Autodesk.Aec.PropertyData;
using Autodesk.Aec.PropertyData.DatabaseServices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Windows.Forms;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using Microsoft.Office.Interop.Excel;

using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.PlanDetailing;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
using static IntersectUtilities.UtilsCommon.Utils;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Newtonsoft.Json;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.Dimensionering.Forms;
using Schema.Datafordeler;
using DimensioneringV2;
using Microsoft.Win32;

namespace IntersectUtilities.Dimensionering
{
    /// <summary>
    /// Class for intersection tools.
    /// </summary>
    public partial class DimensioneringExtension : IExtensionApplication
    {
        #region IExtensionApplication members
        public void Initialize()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                SystemObjects.DynamicLinker.LoadModule(
                    "AcMPolygonObj" + Application.Version.Major + ".dbx", false, false);
            }

            doc.Editor.WriteMessage("\n-> Import SYMBOL blocks ved opstart på ny tegning: DIMPREPAREDWG");
            doc.Editor.WriteMessage("\n-> Import BBR data to blocks: DIMIMPORTBBRBLOCKS");
            doc.Editor.WriteMessage("\n-> Assign areas to BBR blocks and delete the rest: DIMINTERSECTAREAS");
            doc.Editor.WriteMessage("\n-> Select objects by type and area: DIMSELECTOBJ");
            doc.Editor.WriteMessage("\n-> Connect building to multiple addresses: DIMCONNECTHUSNR");
            doc.Editor.WriteMessage("\n-> Remove unwanted husnr blocks from database: DIMREMOVEHUSNR");
            doc.Editor.WriteMessage("\n-> Populate property data based on geometry: DIMPOPULATEGRAPH");
            doc.Editor.WriteMessage("\n-> Dump all addresses to file: DIMADRESSERDUMP");
            doc.Editor.WriteMessage("\n-> Write data to excel: DIMWRITEEXCEL");
            doc.Editor.WriteMessage("\n-> 1) Prepare 2) Import BBR 3) Intersect 4) Husnr 5) Populate 6) Dump adresser 7) Write excel");

#if DEBUG
            AppDomain.CurrentDomain.AssemblyResolve +=
                new ResolveEventHandler(MissingAssemblyLoader.Debug_AssemblyResolve);
#endif
        }

        public void Terminate()
        {
        }
        #endregion
        /// <command>DIMIMPORTBBRBLOCKS</command>
        /// <summary>
        /// Imports BBR features from a selected GeoJSON file as block references and writes their
        /// attributes into the BBR property set. Intended to populate the drawing with building points
        /// carrying BBR data for subsequent graph and reporting workflows.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMIMPORTBBRBLOCKS")]
        public void dimimportbbrblocks()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            PropertySetManager.UpdatePropertySetDefinition(
                Application.DocumentManager.MdiActiveDocument.Database,
                PSetDefs.DefinedSets.BBR);

            #region Dialog box for selecting the geojson file
            string dbFilename = localDb.OriginalFileName;
            string path = System.IO.Path.GetDirectoryName(dbFilename);
            string fileName = string.Empty;
            string bbrString = "";
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = "Choose BBR file:",
                DefaultExt = "geojson",
                Filter = "Geojson files (*.geojson)|*.geojson|All files (*.*)|*.*",
                FilterIndex = 0
            };
            if (dialog.ShowDialog() == true)
            {
                fileName = dialog.FileName;
                bbrString = File.ReadAllText(fileName);
            }
            else { throw new System.Exception("Cannot find BBR file!"); }

            ImportFraBBR.FeatureCollection BBR = JsonConvert.DeserializeObject<ImportFraBBR.FeatureCollection>(bbrString);
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                try
                {
                    var pm = new ProgressMeter();
                    pm.Start("Importing BBR features...");
                    pm.SetLimit(BBR.features.Count);
                    foreach (ImportFraBBR.Feature feature in BBR.features)
                    {
                        try
                        {
                            var test = feature.geometry.coordinates as IEnumerable;
                        }
                        catch (System.Exception ex)
                        {
                            prdDbg("Feature " + feature.properties.id_lokalId + " mangler geometry!1");
                            throw;
                        }
                        var source = feature.geometry.coordinates as IEnumerable;
                        double[] coords = new double[2];
                        int i = 0;
                        if (source != null)
                        {
                            foreach (double d in source)
                            {
                                if (i == 0) coords[0] = d;
                                else coords[1] = d;
                                i++;
                            }
                        }

                        Point3d position = new Point3d(coords[0], coords[1], 0);

                        BlockReference bbrBlock = null;
                        try
                        {
                            bbrBlock = localDb.CreateBlockWithAttributes(feature.properties.Type, position);
                        }
                        catch (System.Exception ex)
                        {
                            if (ex.Message == "eKeyNotFound") prdDbg("Tegningen mangler BBR blokke!");
                            else prdDbg("Failed block name: " + feature.properties.Type + "\n" + ex.ToString());
                            tx.Abort();
                            return;
                        }

                        var properties = feature.properties.GetType().GetRuntimeProperties()
                            .Where(p => p.GetMethod != null && p.GetMethod.IsPublic && p.GetMethod.IsStatic == false)
                            .ToList();

                        #region Debug
                        //j++;
                        //if (j != 1) { tx.Abort(); return; }
                        //foreach (var property in properties)
                        //{
                        //    var value = TryGetValue(property, feature.properties);

                        //    prdDbg($"{property.Name} -> {value} -> {value.GetType().Name}");
                        //}
                        #endregion

                        var dict = bbrDef.ToPropertyDictionary();
                        foreach (PropertyInfo pinfo in properties)
                        {
                            if (dict.ContainsKey(pinfo.Name))
                            {
                                var value = TryGetValue(pinfo, feature.properties);
                                try
                                {
                                    bbrPsm.WritePropertyObject(bbrBlock, dict[pinfo.Name] as PSetDefs.Property, value);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg($"Property not found: {(dict[pinfo.Name] as PSetDefs.Property).Name}");
                                    throw;
                                }
                            }
                        }

                        pm.MeterProgress();
                    }
                    pm.Stop();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }

            dimanalyzeduplicateaddr();

            prdDbg("Finished!");

        }

        /// <command>DIMINTERSECTAREAS</command>
        /// <summary>
        /// Assigns an area name to BBR blocks based on spatial containment within selected POLYLINE or
        /// MPOLYGON boundaries, writing the chosen property to each contained block and removing blocks
        /// not claimed. Intended to prepare per-area datasets for downstream analysis and exports.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMINTERSECTAREAS")]
        public void dimintersectareas()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            #region Select sample object
            PromptEntityOptions peo1 = new PromptEntityOptions(
                "\nVælg objekt som repræsenterer udklipsobjekter (skal være POLYLINE eller MPOLYGON): ");
            peo1.SetRejectMessage("\n Ikke en POLYLINE eller MPOLYGON!");
            peo1.AddAllowedClass(typeof(Polyline), true);
            peo1.AddAllowedClass(typeof(MPolygon), true);
            PromptEntityResult res = editor.GetEntity(peo1);
            if (res.Status != PromptStatus.OK) return;
            Oid pickedId = res.ObjectId;
            #endregion

            #region Ask for what type of data storage
            const string kwd1 = "Od";
            const string kwd2 = "Ps";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nHvilken type data opslag skal bruges? ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd1;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            if (pKeyRes.Status != PromptStatus.OK) return;

            string dataType = pKeyRes.StringResult;
            #endregion

            string psName = Dimensionering.dimaskforpropertysetname();
            string pName = Dimensionering.dimaskforpropertyname();

            if (psName.IsNoE() || pName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                PropertySetManager omrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_område);
                PSetDefs.FJV_område omrDef = new PSetDefs.FJV_område();

                try
                {
                    HashSet<Entity> entities = new HashSet<Entity>();

                    switch (pickedId.ObjectClass.Name)
                    {
                        case "AcDbPolyline":
                            HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                            plines = plines.Where(x => IsDataNotNoE(x)).ToHashSet();
                            prdDbg("Nr. of plines " + plines.Count().ToString());
                            entities.UnionWith(plines);
                            break;
                        case "AcDbMPolygon":
                            HashSet<MPolygon> polygons = localDb.HashSetOfType<MPolygon>(tx, true);
                            polygons = polygons.Where(x => IsDataNotNoE(x)).ToHashSet();
                            prdDbg("Nr. of polygons " + polygons.Count().ToString());
                            entities.UnionWith(polygons);
                            break;
                        default:
                            tx.Abort();
                            prdDbg("Non defined object type received!");
                            return;
                    }

                    bool IsDataNotNoE(Entity ent)
                    {
                        string value = "";

                        switch (dataType)
                        {
                            case kwd1: //ObjectData
                                value = ReadStringPropertyValue(tables, ent.Id, psName, pName);
                                break;
                            case kwd2: //PSet
                                value = PropertySetManager.ReadNonDefinedPropertySetString(ent, psName, pName);
                                break;
                            default:
                                break;
                        }

                        return value.IsNotNoE();
                    }

                    if (entities.Count() == 0) { tx.Abort(); prdDbg("No entities found!"); return; }

                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => Dimensionering.AllBlockTypes.Contains(x.RealName()))
                        .ToHashSet();
                    prdDbg("Nr. of blocks " + brs.Count().ToString());

                    ObjectIdCollection claimedIds = new ObjectIdCollection();

                    foreach (Entity ent in entities)
                    {
                        string etapeName = "";

                        switch (dataType)
                        {
                            case kwd1: //ObjectData
                                etapeName = ReadStringPropertyValue(tables, ent.Id, psName, pName);
                                break;
                            case kwd2: //PSet
                                etapeName = PropertySetManager.ReadNonDefinedPropertySetString(ent, psName, pName);
                                break;
                            default:
                                break;
                        }

                        double tolerance = Tolerance.Global.EqualPoint;

                        switch (ent)
                        {
                            case Polyline pline:
                                if (pline.Closed == false) { prdDbg($"Pline {pline.Handle} is not closed!"); continue; }
                                using (MPolygon mpg = new MPolygon())
                                {
                                    mpg.AppendLoopFromBoundary(pline, true, tolerance);
                                    var query = brs.Where(x => mpg.IsPointInsideMPolygon(x.Position, tolerance).Count == 1);
                                    if (query.Count() == 0) continue;
                                    foreach (BlockReference br in query)
                                    {
                                        //Protect against erasure
                                        claimedIds.Add(br.Id);
                                        //Write area name
                                        bbrPsm.WritePropertyString(br, bbrDef.DistriktetsNavn, etapeName);
                                    }
                                }
                                break;
                            case MPolygon mpg:
                                {
                                    var query = brs.Where(x => mpg.IsPointInsideMPolygon(x.Position, tolerance).Count == 1);
                                    if (query.Count() == 0) continue;
                                    foreach (BlockReference br in query)
                                    {
                                        //Protect against erasure
                                        claimedIds.Add(br.Id);
                                        //Write area name
                                        bbrPsm.WritePropertyString(br, bbrDef.DistriktetsNavn, etapeName);
                                    }

                                    DBObjectCollection objs = new DBObjectCollection();
                                    mpg.Explode(objs);
                                    Oid id = Oid.Null;
                                    foreach (DBObject obj in objs)
                                        if (obj is Polyline pline) id = pline.AddEntityToDbModelSpace(localDb);

                                    mpg.CheckOrOpenForWrite();
                                    mpg.Erase(true);

                                    if (id == Oid.Null) continue;
                                    Polyline newPline = id.Go<Polyline>(tx);
                                    omrPsm.WritePropertyString(newPline, omrDef.Område, etapeName);
                                }
                                break;
                            default:
                                break;
                        }

                    }

                    //Now delete all non-claimed blocks
                    foreach (var item in brs.ExceptWhere(x => claimedIds.Contains(x.Id)))
                    {
                        item.CheckOrOpenForWrite();
                        item.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        internal static object TryGetValue(PropertyInfo property, object element)
        {
            object value;
            try
            {
                value = property.GetValue(element);

                if (value == null)
                {
                    switch (property.PropertyType.Name)
                    {
                        case nameof(String):
                            value = "";
                            break;
                        case nameof(Boolean):
                            value = false;
                            break;
                        case nameof(Double):
                            value = 0.0;
                            break;
                        case nameof(Int32):
                            value = 0;
                            break;
                        default:
                            value = "";
                            break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                value = $"{{{ex.Message}}}";
            }

            return value;
        }

        /// <command>DIMADRESSERDUMP</command>
        /// <summary>
        /// Exports addresses and related BBR data for the selected area to a CSV file. Intended for
        /// quick review, QA, and as input to external tools or spreadsheets.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMADRESSERDUMP")]
        public void dimadresserdump()
        {
            Dimensionering.dimadressedump();
        }

        /// <command>DIMPOPULATEGRAPH</command>
        /// <summary>
        /// Builds the connection graph for the selected area: clears previous graph data, creates
        /// connection lines from buildings to pipes, assigns parents/children in property sets, and
        /// labels pipe segments. Intended to structure the network for designing and reporting.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMPOPULATEGRAPH")]
        public void dimpopulategraph()
        {
            Dimensionering.dimpopulategraph();
        }
        /// <command>DIMCONNECTHUSNR</command>
        /// <summary>
        /// Connects buildings to multiple address points (husnumre) by creating blocks and lines,
        /// sets graph and BBR properties, and then analyzes duplicate addresses. Intended to map
        /// multi-address buildings accurately before graph population and Excel exports.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMCONNECTHUSNR")]
        public void dimconnecthusnr()
        {
            Dimensionering.dimconnecthusnr();
            dimanalyzeduplicateaddr();
        }

        //[CommandMethod("DIMDUMPGRAPH")]
        public void dimdumpgraph()
        {
            Dimensionering.dimdumpgraph();
        }

        //[CommandMethod("DIMWRITEGRAPH")]
        public void dimwritegraph()
        {
            Dimensionering.dimwritegraph();
        }

        //[CommandMethod("DIMWRITEEXCEL")]
        public void dimwriteexcel()
        {
            Dimensionering.dimwriteexcel();
        }

        /// <command>DIMSELECTOBJS</command>
        /// <summary>
        /// Selects objects in the drawing by area and type (polylines, blocks, or all) and places them
        /// into the implied selection set. Intended to speed up batch edits and QA for a specific area.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMSELECTOBJS")]
        public void dimselectobjs()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            #region Ask for region name
            string curEtapeName = "";
            PromptStringOptions pStrOpts = new PromptStringOptions("\nOmråde navn: ");
            pStrOpts.AllowSpaces = true;
            PromptResult pStrRes = editor.GetString(pStrOpts);
            if (pStrRes.Status != PromptStatus.OK) return;
            curEtapeName = pStrRes.StringResult;
            #endregion

            #region Ask for what type of objects
            const string kwd1 = "Polylinjer";
            const string kwd2 = "Blokke";
            const string kwd3 = "Alt";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nHvilken type objekter skal vælges? ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.Keywords.Add(kwd3);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd3;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            if (pKeyRes.Status != PromptStatus.OK) return;

            string objectType = pKeyRes.StringResult;
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Find objekter or vælg dem
                    HashSet<Entity> pds = new HashSet<Entity>();
                    if (objectType != null && objectType == kwd3)
                    {//Vælg alt
                        AddBrs(pds);
                        AddPlines(pds);
                    }
                    else if (objectType != null && objectType == kwd2)
                    {//Vælg blokke
                        AddBrs(pds);
                    }
                    else if (objectType != null && objectType == kwd1)
                    {//Vælg polylinjer
                        AddPlines(pds);
                    }

                    void AddBrs(HashSet<Entity> entsCol)
                    {
                        HashSet<Entity> brs = localDb.HashSetOfType<BlockReference>(tx, true)
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                            x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .Cast<Entity>()
                        .ToHashSet();
                        prdDbg("Nr. of blocks " + brs.Count().ToString());
                        entsCol.UnionWith(brs);
                    }

                    void AddPlines(HashSet<Entity> entsCol)
                    {
                        HashSet<Entity> plines = localDb.HashSetOfType<Polyline>(tx, true)
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                            x, "FJV_fremtid", "Distriktets_navn") == curEtapeName)
                        .Cast<Entity>()
                        .ToHashSet();
                        prdDbg("Nr. of plines " + plines.Count().ToString());
                        entsCol.UnionWith(plines);
                    }

                    System.Windows.Forms.Application.DoEvents();
                    editor.SetImpliedSelection(pds.Select(x => x.Id).ToArray());
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        /// <command>DIMREMOVEHUSNR</command>
        /// <summary>
        /// Interactively removes selected husnummer blocks and their lines, updates the in-memory address
        /// list, and supports saving changes back to GeoJSON. Intended to curate and correct address
        /// points associated with buildings.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMREMOVEHUSNR")]
        public void dimremovehusnr()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            string curEtapeName = "";

            #region Dialog box for selecting the geojson file
            FeatureCollection LoadHusNumre(string EtapeName)
            {
                string dbFilename = localDb.OriginalFileName;
                string path = System.IO.Path.GetDirectoryName(dbFilename);
                string nyHusnumreFilename = path + $"\\husnumre_{EtapeName}.geojson";
                string husnumreStr;
                if (File.Exists(nyHusnumreFilename))
                {
                    husnumreStr = File.ReadAllText(nyHusnumreFilename);
                }
                else
                {
                    string fileName = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose husnumre file:",
                        DefaultExt = "geojson",
                        Filter = "Geojson files (*.geojson)|*.geojson|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        fileName = dialog.FileName;
                        husnumreStr = File.ReadAllText(fileName);
                    }
                    else { throw new System.Exception("Cannot find husnumre file!"); }
                }

                return JsonConvert.DeserializeObject<FeatureCollection>(husnumreStr);
            }
            FeatureCollection husnumre = default;
            #endregion

            while (true)
            {
                #region Select pline3d
                const string kwdSave = "Save";
                PromptEntityOptions peo1 = new PromptEntityOptions(
                    "\nSelect husnummer block to remove or :");
                peo1.SetRejectMessage("\n Not a block!");
                peo1.AddAllowedClass(typeof(BlockReference), true);
                peo1.Keywords.Add(kwdSave);
                PromptEntityResult res = editor.GetEntity(peo1);
                Oid pickedId = Oid.Null;

                if (res.Status == PromptStatus.OK)
                {
                    prdDbg($"\nPicked entity: {res.ObjectId.ToString()}");
                    pickedId = res.ObjectId;
                }
                else if (res.Status == PromptStatus.Keyword)
                {
                    prdDbg("Saving...");
                    if (res.StringResult == kwdSave)
                    {
                        if (curEtapeName.IsNoE() && husnumre == null)
                        {
                            prdDbg("Ingen adresser fjernet endnu! Fortsætter.");
                            continue;
                        }

                        //Build file name
                        string dbFilename = localDb.OriginalFileName;
                        string path = System.IO.Path.GetDirectoryName(dbFilename);
                        string nyHusnumreFilename = path + $"\\husnumre_{curEtapeName}.geojson";

                        JsonSerializerSettings settings = new JsonSerializerSettings();
                        settings.NullValueHandling = NullValueHandling.Ignore;
                        if (husnumre == default) return;
                        string outputHusnr = JsonConvert.SerializeObject(husnumre, settings);

                        Utils.ClrFile(nyHusnumreFilename);
                        Utils.OutputWriter(nyHusnumreFilename, outputHusnr);
                        return;
                    }
                    else
                    {
                        prdDbg("Wrong keyword, continuing...");
                        continue;
                    }
                }
                else
                {
                    prdDbg("\nInvalid pick or cancelled");
                    return;
                }

                #endregion

                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                    PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                    HashSet<Line> husNrLines = localDb.ListOfType<Line>(tx)
                        .Where(x => x.Layer == "0-HUSNUMMER_LINE")
                        .ToHashSet();

                    try
                    {
                        BlockReference selectedBlock = pickedId.Go<BlockReference>(tx);

                        //Check if block is in correct layer
                        if (selectedBlock.Layer != "0-HUSNUMMER_BLOCK")
                        {
                            prdDbg("Wrong block selected!");
                            tx.Abort();
                            continue;
                        }

                        string id_lokalId = PropertySetManager
                            .ReadNonDefinedPropertySetString(selectedBlock, "BBR", "id_lokalId");

                        Line husNrLine = husNrLines
                            .Where(x => getFirstChild(x)
                            .Handle == selectedBlock.Handle)
                            .FirstOrDefault();

                        if (husNrLine == default) throw new System.Exception(
                            $"Cannot find husNr line for block {selectedBlock.Handle}!");

                        //used to get the line by comparing line's child to block's handle
                        Entity getFirstChild(Line line)
                        {                            
                            string chStr = graphPsm.ReadPropertyString(line, graphDef.Children);
                            string firstChild = chStr.Split(';')[0];
                            if (firstChild.IsNoE()) throw new System.Exception(
                                $"Line {line.Handle} does not have a child specified!");
                            return localDb.Go<Entity>(firstChild);
                        }

                        //Get the original building block
                        string lineParentStr = graphPsm.ReadPropertyString(husNrLine, graphDef.Parent);
                        BlockReference buildingBlock = localDb.Go<BlockReference>(lineParentStr);

                        //Load the husnumre
                        //Cache the current area name
                        curEtapeName = PropertySetManager.ReadNonDefinedPropertySetString(
                            buildingBlock, "BBR", "Distriktets_navn");

                        if (husnumre == default) husnumre = LoadHusNumre(curEtapeName);

                        //Remove the address from address list
                        int removed = husnumre.features.RemoveAll(
                            x => x.properties.id_lokalId.ToUpper() == id_lokalId.ToUpper());
                        prdDbg($"Antal adresser fjernet: {removed}.");

                        //Remove the reference to the connection line object
                        string childrenStr = graphPsm.ReadPropertyString(husNrLine, graphDef.Children);
                        var split = childrenStr.Split(';').ToList();
                        split.RemoveAll(x => x == husNrLine.Handle.ToString().ToUpper());
                        childrenStr = String.Join(";", split);
                        graphPsm.WritePropertyString(graphDef.Children, childrenStr);

                        //Erase line and husnr object
                        husNrLine.CheckOrOpenForWrite();
                        husNrLine.Erase(true);
                        selectedBlock.CheckOrOpenForWrite();
                        selectedBlock.Erase(true);
                    }
                    catch (System.Exception ex)
                    {
                        tx.Abort();
                        editor.WriteMessage("\n" + ex.ToString());
                        return;
                    }
                    finally
                    {

                    }
                    tx.Commit();
                }
            }
        }

        /// <command>DIMPREPAREDWG</command>
        /// <summary>
        /// Prepares the current drawing for dimensionering by importing missing building symbol blocks
        /// and creating required layers (future FJV and no-cross). Intended as the first setup step
        /// before data import and graph operations.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMPREPAREDWG")]
        public void dimpreparedwg()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PropertySetManager fjvOmrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_område);

                try
                {
                    #region Import building blocks if missing
                    BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    var nonExistingBlocks = Dimensionering.AllBlockTypes.ExceptWhere(x => bt.Has(x));

                    if (nonExistingBlocks.Count() > 0)
                    {
                        ObjectIdCollection idsToClone = new ObjectIdCollection();

                        Database blockDb = new Database(false, true);
                        blockDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\DynBlokke\\Symboler.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction blockTx = blockDb.TransactionManager.StartTransaction();

                        Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                        Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);

                        BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                        foreach (string blockName in nonExistingBlocks)
                        {
                            prdDbg($"Importing block {blockName}.");
                            idsToClone.Add(sourceBt[blockName]);
                        }

                        IdMapping mapping = new IdMapping();
                        blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        blockTx.Commit();
                        blockTx.Dispose();
                        blockDb.Dispose();
                    }
                    #endregion

                    #region Create layer for dim plines
                    string layerName = "0-FJV_fremtid";
                    localDb.CheckOrCreateLayer(layerName, 6);

                    string noCrossLayName = "0-NOCROSS_LINE";
                    localDb.CheckOrCreateLayer(noCrossLayName, 1);
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        /// <command>DIMWRITEEXCEL</command>
        /// <summary>
        /// Generates Excel workbooks from BBR and FJV data for one area or all areas using a selected
        /// template, including address and connection-length tables. Intended to produce excel files for
        /// design work.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMWRITEEXCEL")]
        public void dimwriteall()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            #region Choose one or all
            const string kwd1 = "Enkelt";
            const string kwd2 = "Alle";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nVælg at udskrive ét område eller alle på én gang: ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd1;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            if (pKeyRes.Status != PromptStatus.OK) return;
            string workingDrawing = pKeyRes.StringResult;
            #endregion

            #region Enter area name if Enkelt
            string curEtapeName = "";
            if (workingDrawing == kwd1)
            {
                curEtapeName = Dimensionering.dimaskforarea();
                if (curEtapeName.IsNoE()) return;
            }
            #endregion

            #region Dialog box for selecting the excel file
            string fileName = string.Empty;
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = "Choose excel template file:",
                DefaultExt = "xlsm",
                Filter = "Excel files (*.xlsm)|*.xlsm|All files (*.*)|*.*",
                FilterIndex = 0
            };
            if (dialog.ShowDialog() == true)
            {
                fileName = dialog.FileName;
            }
            else { return; }
            if (fileName.IsNoE()) return;
            #endregion

            #region Get dimensionerende afkøling
            PromptIntegerResult result = editor.GetInteger("\nAngiv dimensionerende afkøling: ");
            if (((PromptResult)result).Status != PromptStatus.OK) return;
            int dimAfkøling = result.Value;
            #endregion

            #region Build base path
            //Build base path
            string dbFilename = localDb.OriginalFileName;
            string basePath = System.IO.Path.GetDirectoryName(dbFilename);
            Directory.CreateDirectory(basePath + "\\DIM");
            basePath += "\\DIM\\"; //Modify basepath to include subfolder 
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Prepare BlockReferences and group by area: groups
                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    HashSet<BlockReference> brs =
                        localDb
                        .HashSetOfType<BlockReference>(tx)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(bbrPsm.ReadPropertyString(x, bbrDef.Type)))
                        .Where(x => bbrPsm.ReadPropertyString(x, bbrDef.DistriktetsNavn).IsNotNoE())
                        .ToHashSet();

                    var groups = brs.GroupBy(x => bbrPsm.ReadPropertyString(x, bbrDef.DistriktetsNavn))
                        .Where(x => !x.Key.EndsWith(Dimensionering.HusnrSuffix));
                    #endregion

                    #region Prepare polylines and group by area and make a lookup
                    PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                    PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                    var polies =
                        localDb
                        .ListOfType<Polyline>(tx)
                        .Where(x => Dimensionering.isLayerAcceptedInFjv(x.Layer))
                        .GroupBy(x => fjvFremPsm.ReadPropertyString(x, fjvFremDef.Distriktets_navn));

                    var plLookup = polies.ToLookup(x => x.Key);
                    #endregion

                    #region Preapare Excel objects
                    Workbook wb;
                    Sheets wss;
                    Worksheet ws;
                    Microsoft.Office.Interop.Excel.Application oXL;
                    object misValue = System.Reflection.Missing.Value;
                    oXL = new Microsoft.Office.Interop.Excel.Application();
                    oXL.Visible = false;
                    oXL.DisplayAlerts = false;
                    #endregion

                    foreach (IGrouping<string, BlockReference> group in groups.OrderBy(x => x.Key))
                    {
                        if (workingDrawing == kwd1) if (group.Key != curEtapeName) continue;
                        prdDbg($"Writing to Excel area: {group.Key}");

                        //Copy the template to destination
                        string newFileName = basePath + group.Key + ".xlsm";
                        if (File.Exists(newFileName))
                            File.Delete(newFileName);
                        File.Copy(fileName, newFileName, true);

                        #region Open workbook
                        wb = oXL.Workbooks.Open(newFileName,
                                        0, false, 5, "", "", false, XlPlatform.xlWindows, "", true, false,
                                        0, false, false, XlCorruptLoad.xlNormalLoad);
                        oXL.Calculation = XlCalculation.xlCalculationManual;
                        #endregion

                        try
                        {
                            Dimensionering.dimadressedumptoexcel(wb, group, plLookup[group.Key], dimAfkøling);

                            Dimensionering.dimwriteallexcel(wb, group.Key, basePath);
                        }
                        catch (System.Exception ex)
                        {
                            wb.Close();
                            oXL.Quit();
                            throw;
                        }

                        #region Close workbook
                        oXL.Calculation = XlCalculation.xlCalculationAutomatic;
                        oXL.Calculate();
                        wb.Save();
                        wb.Close();
                        #endregion
                    }

                    //Quit excel application
                    oXL.Quit();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {
                    prdDbg("Done!");
                }
                tx.Commit();
            }
        }

        /// <command>DIMIMPORTDIMS</command>
        /// <summary>
        /// Imports pipeline dimensions from Excel workbooks (single or batch), maps addresses to pipes
        /// via the graph, and creates sized pipe polylines in a side-loaded DWG using the selected
        /// series. Intended to turn Excel dimensioning into modeled pipelines.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMIMPORTDIMS")]
        public void dimimportdims()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            #region Choose one or all
            const string kwd1 = "Enkelt";
            const string kwd2 = "Alle";

            PromptKeywordOptions pKeyOpts = new PromptKeywordOptions("");
            pKeyOpts.Message = "\nVælg at importere ét område eller alle på én gang: ";
            pKeyOpts.Keywords.Add(kwd1);
            pKeyOpts.Keywords.Add(kwd2);
            pKeyOpts.AllowNone = true;
            pKeyOpts.Keywords.Default = kwd1;
            PromptResult pKeyRes = editor.GetKeywords(pKeyOpts);
            if (pKeyRes.Status != PromptStatus.OK) return;
            string workingDrawing = pKeyRes.StringResult;
            #endregion

            #region Choose series in which to draw pipes
            const string ser1 = "S1";
            const string ser2 = "S2";
            const string ser3 = "S3";

            PromptKeywordOptions pKeyOpts2 = new PromptKeywordOptions("");
            pKeyOpts2.Message = "\nVælg serie for rør: ";
            pKeyOpts2.Keywords.Add(ser1);
            pKeyOpts2.Keywords.Add(ser2);
            pKeyOpts2.Keywords.Add(ser3);
            pKeyOpts2.AllowNone = true;
            pKeyOpts2.Keywords.Default = ser3;
            PromptResult pKeyRes2 = editor.GetKeywords(pKeyOpts2);
            if (pKeyRes2.Status != PromptStatus.OK) return;
            string series = pKeyRes2.StringResult;
            PipeSeriesEnum pipeSeries =
                (PipeSeriesEnum)Enum.Parse(typeof(PipeSeriesEnum), series);
            #endregion

            #region Get file paths
            HashSet<string> fileNames = new HashSet<string>();

            switch (workingDrawing)
            {
                case kwd1: //Enkelt
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose excel file to import dimensions:",
                        DefaultExt = "xlsm",
                        Filter = "Excel files (*.xlsm)|*.xlsm|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        if (dialog.FileName.IsNoE()) return;
                        fileNames.Add(dialog.FileName);
                    }
                    else return;
                    break;
                case kwd2: //Alle
                    var folderDialog = new OpenFolderDialog
                    {
                        Title = "Choose folder with excel workbooks to import: ",
                    };

                    if (folderDialog.ShowDialog() == true)
                    {
                        string folder = folderDialog.FolderName;
                        if (string.IsNullOrEmpty(folder)) return;
                        var files = Directory.EnumerateFiles(folder, "*.xlsm");
                        fileNames.UnionWith(files);
                    }
                    else return;
                    break;
                default:
                    break;
            }
            #endregion

            #region Build base path
            //Build base path
            string dbFilename = localDb.OriginalFileName;
            string basePath = System.IO.Path.GetDirectoryName(dbFilename);
            basePath += "\\"; //Modify basepath to include backslash
            string dimDbFilename = basePath + "Fjernvarme DIM.dwg";
            #endregion

            using (Database dimDb = new Database(false, true))
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Prepare sideloaded database
                    dimDb.ReadDwgFile(@"X:\AutoCAD DRI - SETUP\Templates\acadiso.dwt",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                    #endregion

                    #region Prepare BlockReferences and group by area: groups
                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    HashSet<BlockReference> brs =
                        localDb
                        .HashSetOfType<BlockReference>(tx)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(bbrPsm.ReadPropertyString(x, bbrDef.Type)))
                        .Where(x => bbrPsm.ReadPropertyString(x, bbrDef.DistriktetsNavn).IsNotNoE())
                        .ToHashSet();
                    #endregion

                    #region Prepare polylines and group by area and make a lookup
                    PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                    PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                    var polies =
                        localDb
                        .ListOfType<Polyline>(tx)
                        .Where(x => Dimensionering.isLayerAcceptedInFjv(x.Layer))
                        .GroupBy(x => fjvFremPsm.ReadPropertyString(x, fjvFremDef.Distriktets_navn));

                    var plLookup = polies.ToLookup(x => x.Key);
                    #endregion

                    #region Preapare Excel objects
                    Workbook wb;
                    Sheets wss;
                    Worksheet ws;
                    Microsoft.Office.Interop.Excel.Application oXL;
                    object misValue = System.Reflection.Missing.Value;
                    oXL = new Microsoft.Office.Interop.Excel.Application();
                    oXL.Visible = false;
                    oXL.DisplayAlerts = false;
                    oXL.ScreenUpdating = false;
                    #endregion

                    var fileNameDict = new Dictionary<string, string>();

                    foreach (string fileName in fileNames)
                        fileNameDict.Add(System.IO.Path.GetFileNameWithoutExtension(fileName), fileName);

                    #region Data preparation
                    var groups = brs
                        .GroupBy(x => bbrPsm.ReadPropertyString(x, bbrDef.DistriktetsNavn));

                    var areaNames = groups
                        .Select(x => x.Key)
                        .Distinct();

                    //Check to see if any addresses are duplicates
                    var dupQuery = brs.GroupBy(x => da(x));
                    if (dupQuery.Any(x => x.Count() > 1))
                    {
                        prdDbg("ADVARSEL! Dublikatadresser fundet! Skal rettes før det virker.");
                        foreach (var dupGroup in dupQuery.Where(x => x.Count() > 1))
                        {
                            prdDbg($"Dublikat: {dupGroup.Key}");
                        }
                        throw new System.Exception("Dublikatadresser!");
                    }

                    //Prepare dicts and lookups for processing
                    var brDict = brs.ToDictionary(x => da(x));
                    #endregion

                    foreach (var areaName in areaNames.OrderBy(x => x))
                    {
                        if (!fileNameDict.ContainsKey(areaName)) continue;

                        prdDbg($"Importing area: {areaName}");

                        #region Open workbook
                        wb = oXL.Workbooks.Open(fileNameDict[areaName],
                                        0, true, 5, "", "", false, XlPlatform.xlWindows, "", true, false,
                                        0, false, false, XlCorruptLoad.xlNormalLoad);
                        oXL.Calculation = XlCalculation.xlCalculationManual;
                        #endregion

                        Dimensionering.dimimportdim(wb, dimDb, areaName, brDict, plLookup, pipeSeries);

                        #region Close workbook
                        wb.Close();
                        #endregion
                    }

                    //Quit excel application
                    oXL.Quit();

                    string da(BlockReference br)
                    {
                        string adresse = bbrPsm.ReadPropertyString(br, bbrDef.Adresse);
                        int duplicateNr = bbrPsm.ReadPropertyInt(br, bbrDef.AdresseDuplikatNr);
                        string duplicateNrString = duplicateNr == 0 ? "" : " " + duplicateNr.ToString();
                        return adresse + duplicateNrString;
                    }
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
                dimDb.SaveAs(dimDbFilename, DwgVersion.Current);
            }
        }

        //[CommandMethod("LISTANVENDELSEALL")]
        public void listanvendelseall()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //string curEtapeName = Dimensionering.dimaskforarea();
            //if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    #region dump af anvendelseskoder
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .ToHashSet();

                    var anvendelsesKoder = new HashSet<(string kode, string tekst)>();

                    foreach (BlockReference building in brs)
                    {
                        var bbr = new BBR(building);
                        anvendelsesKoder.Add((bbr.BygningsAnvendelseNyKode, bbr.BygningsAnvendelseNyTekst));
                    }

                    //Build file name
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string dumpExportFileName = path + "\\anvendelsedump.txt";

                    Utils.ClrFile(dumpExportFileName);
                    Utils.OutputWriter(dumpExportFileName, string.Join(Environment.NewLine, anvendelsesKoder.Select(x => $"{x.kode}; {x.tekst}")));
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        //[CommandMethod("ANVENDELSESSTATS")]
        public void anvendelsesstats()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //string curEtapeName = Dimensionering.dimaskforarea();
            //if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(
                        @"X:\AutoCAD DRI - QGIS\CSV TIL REST HENTER\AnvendelsesKoderPrivatErhverv.csv", "PrivatErhverv");

                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    #region dump af anvendelseskoder
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        //.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        //    x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .Where(x => ReadStringParameterFromDataTable(
                            PropertySetManager.ReadNonDefinedPropertySetString(
                                x, "BBR", "BygningsAnvendelseNyKode"), dt, "Type", 0) == "Erhverv")
                        .ToHashSet();

                    var dict = new Dictionary<string, int>();

                    foreach (BlockReference br in brs)
                    {
                        var bbr = new BBR(br);
                        string anvendelse = bbr.BygningsAnvendelseNyTekst;

                        if (dict.ContainsKey(anvendelse))
                        {
                            int curCount = dict[anvendelse];
                            curCount++;
                            dict[anvendelse] = curCount;
                        }
                        else
                        {
                            dict.Add(anvendelse, 1);
                        }
                    }

                    //Build file name
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string dumpExportFileName = path + "\\anvendelsesstats.txt";

                    Utils.ClrFile(dumpExportFileName);
                    Utils.OutputWriter(dumpExportFileName, string.Join(Environment.NewLine, dict.Select(x => x.Key + ";" + x.Value.ToString())));
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        /// <command>DIMANALYZEDUPLICATEADDR</command>
        /// <summary>
        /// Analyzes buildings with identical addresses, assigns or repairs duplicate numbers in the BBR
        /// property set to ensure unique address labels, and exports a CSV report. Intended to normalize
        /// duplicate addresses before Excel generation and other downstream processes.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMANALYZEDUPLICATEADDR")]
        public void dimanalyzeduplicateaddr()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    HashSet<BlockReference> brs =
                        localDb
                        .HashSetOfType<BlockReference>(tx)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(bbrPsm.ReadPropertyString(x, bbrDef.Type)))
                        .ToHashSet();

                    var groups = brs.GroupBy(x => bbrPsm.ReadPropertyString(x, bbrDef.Adresse))
                        .Where(x => x.Count() > 1);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Adresse;Område;Forbrug;Ny adresse");

                    foreach (var group in groups)
                    {
                        //Handle a case where a building is added with same address after an analyzis have already been carried out
                        if (group.All(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr) == 0))
                        {
                            int count = 0;
                            foreach (var br in group.OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y))
                            {
                                count++;
                                sb.AppendLine(
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.DistriktetsNavn)};" +
                                    $"{bbrPsm.ReadPropertyDouble(br, bbrDef.EstimeretVarmeForbrug)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)} {count}");

                                bbrPsm.WritePropertyObject(br, bbrDef.AdresseDuplikatNr, count);
                            }
                        }
                        else if (group.All(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr) != 0))
                        {
                            foreach (var br in group.OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y))
                            {
                                sb.AppendLine(
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.DistriktetsNavn)};" +
                                    $"{bbrPsm.ReadPropertyDouble(br, bbrDef.EstimeretVarmeForbrug)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)} {bbrPsm.ReadPropertyInt(br, bbrDef.AdresseDuplikatNr)}");
                            }
                        }
                        else if (group.Any(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr) != 0))
                        {
                            List<int> existingNumbers =
                                group.Select(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr))
                                .OrderBy(x => x).ToList();

                            foreach (var br in group
                                .Where(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr) == 0)
                                .OrderBy(x => x.Position.X).ThenBy(x => x.Position.Y))
                            {
                                int nextNumber = 0;
                                var missing = FindMissing(existingNumbers);
                                if (missing.Count() > 0) nextNumber = missing.Min();
                                else nextNumber = existingNumbers.Max() + 1;
                                existingNumbers.Add(nextNumber);

                                bbrPsm.WritePropertyObject(br, bbrDef.AdresseDuplikatNr, nextNumber);
                            }

                            foreach (var br in group.OrderBy(x => bbrPsm.ReadPropertyInt(x, bbrDef.AdresseDuplikatNr)))
                            {
                                sb.AppendLine(
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.DistriktetsNavn)};" +
                                    $"{bbrPsm.ReadPropertyDouble(br, bbrDef.EstimeretVarmeForbrug)};" +
                                    $"{bbrPsm.ReadPropertyString(br, bbrDef.Adresse)} {bbrPsm.ReadPropertyInt(br, bbrDef.AdresseDuplikatNr)}");
                            }
                        }
                        sb.AppendLine();
                    }

                    IEnumerable<int> FindMissing(IEnumerable<int> values)
                    {
                        HashSet<int> myRange = new HashSet<int>(Enumerable.Range(values.Min(), values.Max()));
                        myRange.ExceptWith(values);
                        return myRange;
                    }

                    //Build file name
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string dumpExportFileName = path + "\\duplikater.csv";

                    Utils.OutputWriter(dumpExportFileName, sb.ToString(), true);
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        //[CommandMethod("FINDSTRAY")]
        public void findstray()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //string curEtapeName = Dimensionering.dimaskforarea();
            //if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    var plines = localDb.HashSetOfType<Polyline>(tx);

                    PropertySetManager fjvPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                    PSetDefs.FJV_fremtid fjvDef = new PSetDefs.FJV_fremtid();

                    var find = plines
                        .Where(x => fjvPsm.ReadPropertyString(x, fjvDef.Distriktets_navn) == "Område 4")
                        .Where(x => fjvPsm.ReadPropertyString(x, fjvDef.Bemærkninger) == "Strækning 1.1")
                        ;

                    prdDbg(find.Count().ToString());

                    editor.SetImpliedSelection(find.Select(x => x.Id).ToArray());
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }

        /// <command>DIMENHEDERLIST</command>
        /// <summary>
        /// Builds an HTML report listing building units by building and address from BBR datasets for a
        /// selected path. Intended to review unit types and distributions for QA and planning.
        /// Report is written to: C:\Temp\enheder.html
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMENHEDERLIST")]
        public void dimenhederlist()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            #region Get path
            ChoosePath cp = new ChoosePath();
            cp.ShowDialog();
            cp.Close();
            if (cp.Path.IsNoE()) return;
            #endregion

            #region Read data
            string basePath = @"X:\AutoCAD DRI - QGIS\BBR UDTRÆK";
            string path = basePath + "\\" + cp.Path + "\\";

            string bygningerPath = path + "BBR_bygning.json";
            if (!File.Exists(bygningerPath)) { prdDbg("BBR_bygning.json does not exist!"); return; }
            string enhederPath = path + "BBR_enhed.json";
            if (!File.Exists(enhederPath)) { prdDbg("BBR_enhed.json does not exist!"); return; }
            string adresserPath = path + "DAR_adresse.json";
            if (!File.Exists(adresserPath)) { prdDbg("DAR_adresse.json does not exist!"); return; }

            var bygninger = UtilsCommon.Json.Deserialize<HashSet<BBRBygning.Bygning>>(bygningerPath)
                .Where(x => x.status == "6");
            var adresser = UtilsCommon.Json.Deserialize<HashSet<DARAdresse.Adresse>>(adresserPath)
                .Where(x => x.status == "3");
            var enheder = UtilsCommon.Json.ReadJson(enhederPath);
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                var enhKoder = Csv.EnhKoder;
                Dictionary<string, string> enhKoderDict = new Dictionary<string, string>();
                foreach (var row in enhKoder.Rows)
                {
                    string key = EnhKoder.Col(row, EnhKoder.Columns.Nr);
                    string value = EnhKoder.Col(row, EnhKoder.Columns.Kode);
                    if (!enhKoderDict.ContainsKey(key))
                        enhKoderDict.Add(key, value);
                }

                var brs = localDb.HashSetOfType<BlockReference>(tx, true);

                var pm = new ProgressMeter();
                pm.Start("Processing...");
                pm.SetLimit(brs.Count);

                var list = new List<(string bygning, string adresse, string enhedstype)>();
                try
                {
                    foreach (var br in brs)
                    {
                        string brId = bbrPsm.ReadPropertyString(br, bbrDef.id_lokalId).ToLower();
                        if (brId.IsNoE())
                        {
                            if (br.BlockTableRecord.Go<BlockTableRecord>(tx).IsFromExternalReference)
                                continue;

                            prdDbg($"WARNING! id_lokalId is empty for block {br.Handle}!");
                        }

                        var buildings = bygninger.Where(x => x.id_lokalId.ToLower() == brId);

                        if (buildings.Count() == 0)
                        {
                            prdDbg($"WARNING! No building found for block {br.Handle}!");
                            prdDbg(brId);
                            prdDbg(br.Handle);
                        }

                        foreach (var building in buildings)
                        {
                            var units = enheder.Where(
                                x => x.GetProperty("bygning").GetString().ToLower() ==
                                building.id_lokalId.ToLower());

                            foreach (var unit in units)
                            {
                                var addrs = adresser.Where(x =>
                                {
                                    if (unit.TryGetProperty("adresseIdentificerer", out JsonElement id))
                                        return x.id_lokalId == id.GetString();
                                    return false;
                                });

                                foreach (var addr in addrs)
                                {
                                    if (unit.TryGetProperty("enh020EnhedensAnvendelse", out JsonElement anvStr))
                                    {
                                        if (!enhKoderDict.ContainsKey(anvStr.GetString()))
                                        {
                                            prdDbg($"Anvendelseskode {anvStr.GetString()} not found in anvendelseskoder.csv!");
                                            continue;
                                        }

                                        list.Add((
                                        building.id_lokalId,
                                        addr?.adressebetegnelse,
                                        enhKoderDict[anvStr.GetString()]));
                                    }
                                }
                            }
                        }

                        pm.MeterProgress();
                    }

                    pm.Stop();
                    pm.Dispose();
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    pm.Stop();
                    pm.Dispose();
                    return;
                }
                finally
                {

                }
                tx.Commit();

                string html = ConvertToHtmlTree(list);
                string htmlPath = "C:\\Temp\\" + "enheder.html";
                File.WriteAllText(htmlPath, html);
            }

            prdDbg("Finished!");
        }

        /// <command>DIMENHEDERANALYZE</command>
        /// <summary>
        /// Analyzes BBR unit data to count residential units per building and writes results back to
        /// the drawing (e.g., AntalEnheder). Intended to quantify demand per building for planning.
        /// </summary>
        /// <category>Dimensionering</category>
        [CommandMethod("DIMENHEDERANALYZE")]
        public void dimenhederanalyze()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            #region Get path
            ChoosePath cp = new ChoosePath();
            cp.ShowDialog();
            cp.Close();
            if (cp.Path.IsNoE()) return;
            #endregion

            #region Read data
            string basePath = @"X:\AutoCAD DRI - QGIS\BBR UDTRÆK";
            string path = basePath + "\\" + cp.Path + "\\";

            string enhederPath = path + "BBR_enhed.json";
            if (!File.Exists(enhederPath))
            { prdDbg("BBR_enhed.json does not exist! Download med RestHenter!"); return; }

            var enheder = UtilsCommon.Json.Deserialize<HashSet<BBREnhed.Enhed>>(enhederPath)
                .Where(x => x.status == "6" || x.status == "7");

            var enhedsDict = enheder
                .Where(x => x.status.IsNotNoE())
                .Where(x => x.enh020EnhedensAnvendelse.IsNotNoE())
                .GroupBy(x => x.bygning)
                .ToDictionary(x => x.Key, x => x.ToHashSet());
            #endregion

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                var enhKoder = Csv.EnhKoder;
                var enhKoderDict = new Dictionary<string, bool>();
                foreach (var row in enhKoder.Rows)
                {
                    //Column "Nr."
                    string key = EnhKoder.Col(row, EnhKoder.Columns.Nr);
                    //Column "Beboelse"
                    bool result = int.TryParse(EnhKoder.Col(row, EnhKoder.Columns.Beboelse), out int val) && val == 1;
                    if (!enhKoderDict.ContainsKey(key))
                        enhKoderDict.Add(key, result);
                }

                //PrintTable(
                //    new string[] { "Anvendelseskode", "Beboelse" },
                //    enhKoderDict.Select(x => new object[] { x.Key, x.Value } as IEnumerable<object>)
                //    );

                var brs = localDb.HashSetOfType<BlockReference>(tx, true)
                    .Where(x => !x.BlockTableRecord.Go<BlockTableRecord>(tx).IsFromExternalReference);

                var pm = new ProgressMeter();
                pm.Start("Processing...");
                pm.SetLimit(brs.Count());

                try
                {
                    //Test to see if there are multiple bygninger with same id_lokalId
                    var test = brs.GroupBy(x => bbrPsm.ReadPropertyString(x, bbrDef.id_lokalId))
                        .Where(x => x.Count() > 1);

                    if (test.Count() > 0)
                    {
                        prdDbg(
                            $"Buildings with non-unique id_lokalId present!" +
                            $"Analysis cannot continue!");
                        prdDbg(string.Join("\n", test.Select(x => x.Key)));

                        prdDbg("Bygninger med samme bygningsnummer:");
                        var groupByCount = test
                            .Select(g => g.Count())
                            .GroupBy(count => count)
                            .Select(g => new { Antal = g.Key, Forekomst = g.Count() })
                            .OrderBy(x => x.Antal);

                        PrintTable(
                            ["Antal", "Forekomster"],
                            groupByCount.Select(g => new object[] {g.Antal, g.Forekomst} as IEnumerable<object>)
                            );

                        docCol.MdiActiveDocument.Editor.SetImpliedSelection(
                            test.SelectMany(x => x.Select(y => y.Id)).ToArray()
                            );

                        throw new System.Exception(
                            $"Buildings with non-unique id_lokalId present!" +
                            $"Analysis cannot continue!");
                    }

                    var results = new HashSet<(string, int)>();

                    foreach (var br in brs)
                    {
                        string brId = bbrPsm.ReadPropertyString(br, bbrDef.id_lokalId).ToLower();
                        if (brId.IsNoE())
                        {
                            if (br.BlockTableRecord.Go<BlockTableRecord>(tx).IsFromExternalReference)
                                continue;

                            prdDbg($"WARNING! id_lokalId is empty for block {br.Handle}!");
                        }

                        if (enhedsDict.TryGetValue(brId, out HashSet<BBREnhed.Enhed> units))
                        {
                            int count = units.Count(x => enhKoderDict[x.enh020EnhedensAnvendelse]);
                            if (count > 1)
                                bbrPsm.WritePropertyObject(br, bbrDef.AntalEnheder, count);

                            results.Add((brId, count == 0 ? 1 : count));
                        }

                        pm.MeterProgress();
                    }

                    pm.Stop();
                    pm.Dispose();

                    var analysis = results
                        .GroupBy(tuple => tuple.Item2)
                        .Select(group => new { Value = group.Key, Count = group.Select(x => x.Item1).Count() })
                        .OrderByDescending(x => x.Count);

                    PrintTable(
                        ["Antal enheder", "Antal forekomster"],
                        analysis.Select(x => new object[] {x.Value, x.Count} as IEnumerable<object>)
                        );
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    pm.Stop();
                    pm.Dispose();
                    return;
                }
                finally
                {

                }
                tx.Commit();

                //string html = ConvertToHtmlTree(list);
                //string htmlPath = "C:\\Temp\\" + "enheder.html";
                //File.WriteAllText(htmlPath, html);
            }

            prdDbg("Finished!");
            prdDbg("ADVARSEL: ER IKKE TESTET SAMMEN MED DIMCONNECTHUSNR!!!!!!!!!");
        }

#if DEBUG
        //[CommandMethod("DIMPROPEENERGY")]
        public void DimPropeEnergy()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PromptEntityOptions peo = new PromptEntityOptions("\n Select root polyline of tree: ");
                peo.SetRejectMessage("\n Not a polyline");
                peo.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult per = editor.GetEntity(peo);
                ObjectId plObjId = per.ObjectId;

                Polyline rootPolyline = tx.GetObject(plObjId, OpenMode.ForRead, false) as Polyline;

                // WIP - DML

            }
        } 
#endif

        public static string ConvertToHtmlTree(List<(string bygning, string adresse, string enhedstype)> tuples)
        {
            var sb = new StringBuilder();
            sb.Append("<ul>");  // Start of the top-level list

            // Group by the first property (bygning)
            var bygningGroups = tuples.GroupBy(t => t.bygning);

            foreach (var bygningGroup in bygningGroups)
            {
                sb.AppendFormat("<li>{0}", bygningGroup.Key);  // Each bygning becomes a list item
                sb.Append("<ul>");  // Start a new list for addresses under this bygning

                // Group by the second property (adresse) within the current bygning group
                var adresseGroups = bygningGroup.GroupBy(t => t.adresse);

                foreach (var adresseGroup in adresseGroups)
                {
                    sb.AppendFormat("<li>{0}", adresseGroup.Key);  // Each adresse becomes a list item
                    sb.Append("<ul>");  // Start a new list for enhedstype under this adresse

                    // List each enhedstype under the current adresse
                    foreach (var item in adresseGroup)
                    {
                        sb.AppendFormat("<li>{0}</li>", item.enhedstype);  // Each enhedstype becomes a list item
                    }

                    sb.Append("</ul></li>");  // Close the enhedstype list and adresse list item
                }

                sb.Append("</ul></li>");  // Close the adresse list and bygning list item
            }

            sb.Append("</ul>");  // Close the top-level list
            return sb.ToString();
        }
    }

    internal class Stik
    {
        internal double Dist;
        internal Oid ParentId;
        internal Oid ChildId;
        internal Point3d NearestPoint;

        internal Stik(double dist, Oid parentId, Oid childId, Point3d nearestPoint)
        {
            Dist = dist;
            ParentId = parentId;
            ChildId = childId;
            NearestPoint = nearestPoint;
        }
    }
    internal class POI
    {
        internal Oid OwnerId { get; }
        internal Point3d Point { get; }
        internal EndTypeEnum EndType { get; }
        internal POI(Oid ownerId, Point3d point, EndTypeEnum endType)
        { OwnerId = ownerId; Point = point; EndType = endType; }
        internal bool IsSameOwner(POI toCompare) => OwnerId == toCompare.OwnerId;

        internal enum EndTypeEnum
        {
            Start,
            End
        }
    }
    internal static class Dimensionering
    {
        /// <summary>
        /// 1) Husnr 2) Populate 3) Dump adresser 4) Write excel
        /// Husnumre assigns husnr line as parent
        /// Populate checks if building has any children
        /// if it has overwrites husnumres parent to be that of the conline
        /// </summary>

        //Bruges til behandling af husnumre til forbindelse til bygninger
        internal static HashSet<string> AcceptedAnvCodes =
            new HashSet<string>() { "130" };
        internal static string HusnrSuffix = "-Husnr";
        internal static GlobalSheetCount GlobalSheetCount { get; set; }
        internal static readonly string PRef = "Pref:";
        internal static HashSet<string> AcceptedBlockTypes =
            new HashSet<string>() { "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet" };
        internal static HashSet<string> AllBlockTypes =
            new HashSet<string>() { "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet", "Fjernvarme", "Ingen", "UDGÅR" };
        internal static HashSet<string> AcceptedLayerNamesForFJV =
            new HashSet<string>() { "0-FJV_fremtid", "0-FJV_eks_dim" };
        internal static bool isLayerAcceptedInFjv(string s)
        {
            return (AcceptedLayerNamesForFJV.Contains(s) || AcceptedLayerNamesForFJV.Any(x => s.StartsWith(x)));
        }
        internal static void dimadressedump()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            string curEtapeName = dimaskforarea();
            if (curEtapeName.IsNoE()) return;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                    PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    #region dump af adresser
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => bbrPsm.ReadPropertyString(x, bbrDef.DistriktetsNavn) == curEtapeName)
                        //.Where(x => Dimensionering.AcceptedBlockTypes.Contains(bbrPsm.ReadPropertyString(x, bbrDef.Type)))
                        .ToHashSet();

                    List<BlockReference> allBrs = new List<BlockReference>();
                    foreach (BlockReference br in brs)
                    {
                        string childrenString = graphPsm.ReadPropertyString(br, graphDef.Children);
                        if (childrenString.IsNoE()) allBrs.Add(br);
                        else
                        {
                            HashSet<Entity> children = new HashSet<Entity>();
                            GatherChildren(br, localDb, graphPsm, children);
                            allBrs.AddRange(children.Where(x => x is BlockReference).Cast<BlockReference>());
                        }
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Adresse;DuplicateNr;Energiforbrug;Antal ejendomme;Antal boliger med varmtvandsforbrug;Dim. afkøling;Anvendelseskode;Opvarmningsmiddel;Type;Installation");

                    string antalEjendomme = "1";
                    string antalBoligerOsv = "1";
                    string dimAfkøling = "35";

                    string ds(BlockReference br)
                    {
                        string adresse = bbrPsm.ReadPropertyString(br, bbrDef.Adresse);
                        int duplicateNr = bbrPsm.ReadPropertyInt(br, bbrDef.AdresseDuplikatNr);
                        string duplicateNrString = duplicateNr == 0 ? "" : " " + duplicateNr.ToString();
                        return adresse + duplicateNrString;
                    }

                    foreach (BlockReference building in allBrs.OrderByAlphaNumeric(x => ds(x)))
                    {
                        //string handleString = graphPsm.ReadPropertyString(building, graphDef.Parent);
                        //Handle parent;
                        //try
                        //{
                        //    parent = new Handle(Convert.ToInt64(handleString, 16));
                        //}
                        //catch (System.Exception)
                        //{
                        //    prdDbg($"Reading parent handle failed for block: {building.Handle}");
                        //    throw;
                        //}
                        //Line line = parent.Go<Line>(localDb);

                        //string stikLængde = line.GetHorizontalLength().ToString("0.##");
                        string adresse = bbrPsm.ReadPropertyString(building, bbrDef.Adresse);
                        int duplicateNr = bbrPsm.ReadPropertyInt(building, bbrDef.AdresseDuplikatNr);
                        string duplicateNrString = duplicateNr == 0 ? "" : " " + duplicateNr.ToString();
                        string estVarmeForbrug = (bbrPsm.ReadPropertyDouble(
                            building, bbrDef.EstimeretVarmeForbrug) * 1000.0).ToString("0.##");
                        string anvKodeTekst = bbrPsm.ReadPropertyString(building, bbrDef.BygningsAnvendelseNyTekst);
                        string opvarmningsmiddel = bbrPsm.ReadPropertyString(building, bbrDef.OpvarmningsMiddel);
                        string type = bbrPsm.ReadPropertyString(building, bbrDef.Type);
                        string varmeinstallation = bbrPsm.ReadPropertyString(building, bbrDef.VarmeInstallation);

                        sb.AppendLine($"{adresse};{duplicateNrString};{estVarmeForbrug};{antalEjendomme};{antalBoligerOsv};{dimAfkøling};{anvKodeTekst};{opvarmningsmiddel};{type};{varmeinstallation}");
                    }

                    //Build file name
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string dumpExportFileName = path + "\\dimaddressdump.csv";

                    Utils.ClrFile(dumpExportFileName);
                    Utils.OutputWriter(dumpExportFileName, sb.ToString());

                    System.Windows.Forms.Application.DoEvents();

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void dimadressedumptoexcel(
            Workbook wb,
            IGrouping<string, BlockReference> group,
            IEnumerable<IGrouping<string, Polyline>> plinesGroup,
            int dimAfkøling)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                    PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                    PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                    #region dump af adresser
                    int antalEjendomme = 1;

                    Worksheet ws1 = (Worksheet)wb.Worksheets["Forbrugeroversigt"];
                    Worksheet ws2 = (Worksheet)wb.Worksheets["Stikledninger"];
                    int forbrugerRow = 101;
                    int stikRow = 4;

                    //Columns:
                    //1: Adresse + number
                    //2: Energiforbrug
                    //3: Antal ejendomme
                    //4: Antal boliger med varmtvandsforbrug <- Enheder kommer her
                    //5: Stik længde i m
                    //6: Dim afkøling = 35

                    string ds(BlockReference br)
                    {
                        string adresse = bbrPsm.ReadPropertyString(br, bbrDef.Adresse);
                        int duplicateNr = bbrPsm.ReadPropertyInt(br, bbrDef.AdresseDuplikatNr);
                        string duplicateNrString = duplicateNr == 0 ? "" : " " + duplicateNr.ToString();
                        return adresse + duplicateNrString;
                    }

                    List<BlockReference> allBrs = new List<BlockReference>();
                    foreach (BlockReference br in group)
                    {
                        string childrenString = graphPsm.ReadPropertyString(br, graphDef.Children);
                        if (childrenString.IsNoE()) allBrs.Add(br);
                        else
                        {
                            HashSet<Entity> children = new HashSet<Entity>();
                            GatherChildren(br, localDb, graphPsm, children);
                            allBrs.AddRange(children.Where(x => x is BlockReference).Cast<BlockReference>());
                        }
                    }

                    //Check if count of clients does not exceed 5000
                    if (allBrs.Count > 5000) throw new System.Exception(
                        $"FEJL! Antallet af kunder for etape {group.Key} er mere end 5.000 ({allBrs.Count})!\nOpdel etapen i flere dele.");

                    foreach (BlockReference building in allBrs.OrderByAlphaNumeric(x => ds(x)))
                    {
                        string handleString = graphPsm.ReadPropertyString(building, graphDef.Parent);
                        Handle parent;
                        Line line;
                        try
                        {
                            parent = new Handle(Convert.ToInt64(handleString, 16));
                        }
                        catch (System.Exception)
                        {
                            prdDbg($"Reading parent handle failed for block: {building.Handle}");
                            throw;
                        }
                        try
                        {
                            line = parent.Go<Line>(localDb);
                        }
                        catch (System.Exception)
                        {
                            prdDbg($"Getting parent entity failed for block: {building.Handle}");
                            throw;
                        }

                        double stikLængde = line.GetHorizontalLength();
                        string adresse = bbrPsm.ReadPropertyString(building, bbrDef.Adresse);
                        int duplicateNr = bbrPsm.ReadPropertyInt(building, bbrDef.AdresseDuplikatNr);
                        string duplicateNrString = duplicateNr == 0 ? "" : " " + duplicateNr.ToString();
                        int antalBoligerOsv = bbrPsm.ReadPropertyInt(building, bbrDef.AntalEnheder);
                        double estVarmeForbrugHusnr = (bbrPsm.ReadPropertyDouble(
                            building, bbrDef.EstimeretVarmeForbrug) * 1000.0);
                        string anvKodeTekst = bbrPsm.ReadPropertyString(building, bbrDef.BygningsAnvendelseNyTekst);

                        //Write row

                        forbrugerRow++;
                        ws1.Cells[forbrugerRow, 1] = adresse + duplicateNrString;
                        ws1.Cells[forbrugerRow, 2] = estVarmeForbrugHusnr;
                        ws1.Cells[forbrugerRow, 3] = antalEjendomme;
                        ws1.Cells[forbrugerRow, 4] = antalBoligerOsv;
                        ws1.Cells[forbrugerRow, 5] = stikLængde;
                        ws1.Cells[forbrugerRow, 6] = dimAfkøling;
                        ws1.Cells[forbrugerRow, 8] = anvKodeTekst;

                        stikRow++;
                        ws2.Cells[stikRow, 5] = adresse + duplicateNrString;
                    }

                    HashSet<Polyline> toAddPlines = new HashSet<Polyline>();

                    //Determine if polyline has no clients
                    foreach (Polyline pline in plinesGroup.First())
                    {
                        //Determine if the pline has no client children
                        //It is achieved by looking if the poly
                        //Has any Line children

                        string childrenString = graphPsm.ReadPropertyString(pline, graphDef.Children);
                        //Takes care of plines with no children -> seldom case -> wrong setup in drawing
                        if (childrenString.IsNoE()) { toAddPlines.Add(pline); continue; }
                        var splitArray = childrenString.Split(';');
                        bool toAdd = true;
                        foreach (var childString in splitArray)
                        {
                            if (childString.IsNoE()) continue;
                            Entity child = localDb.Go<Entity>(childString);
                            switch (child)
                            {
                                case Polyline pl:
                                    break;
                                case BlockReference br:
                                    toAdd = false;
                                    break;
                                case Line line:
                                    toAdd = false;
                                    break;
                                default:
                                    throw new System.Exception($"Unexpected type {child.GetType().Name}!");
                            }
                        }
                        if (toAdd) toAddPlines.Add(pline);
                    }

                    //Write polylines with no clients to excel
                    foreach (Polyline pline in toAddPlines)
                    {
                        double stikLængde = 0.0;
                        string adresse = fjvFremPsm.ReadPropertyString(pline, fjvFremDef.Bemærkninger);
                        if (adresse.IsNoE()) continue;
                        double estVarmeForbrugHusnr = 0.0;

                        //Write row
                        forbrugerRow++;
                        ws1.Cells[forbrugerRow, 1] = adresse;
                        ws1.Cells[forbrugerRow, 2] = estVarmeForbrugHusnr;
                        ws1.Cells[forbrugerRow, 3] = 0;
                        ws1.Cells[forbrugerRow, 4] = 0;
                        ws1.Cells[forbrugerRow, 5] = stikLængde;
                        ws1.Cells[forbrugerRow, 6] = dimAfkøling;

                        //stikRow++;
                        //ws2.Cells[stikRow, 5] = adresse + duplicateNrString;
                    }

                    System.Windows.Forms.Application.DoEvents();
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void dimpopulategraph()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            string curEtapeName = dimaskforareaps();
            if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Manage layer to contain connection lines
                    LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                    string conLayName = "0-CONNECTION_LINE";
                    if (!lt.Has(conLayName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = conLayName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);

                        //Make layertable writable
                        lt.CheckOrOpenForWrite();

                        //Add the new layer to layer table
                        Oid ltId = lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }

                    string noCrossLayName = "0-NOCROSS_LINE";
                    if (!lt.Has(noCrossLayName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = noCrossLayName;
                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                        ltr.LineWeight = LineWeight.LineWeight030;

                        //Make layertable writable
                        lt.CheckOrOpenForWrite();

                        //Add the new layer to layer table
                        Oid ltId = lt.Add(ltr);
                        tx.AddNewlyCreatedDBObject(ltr, true);
                    }

                    #endregion

                    #region Gather elements
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines
                        .Where(x => isLayerAcceptedInFjv(x.Layer))
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                            x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .ToHashSet();
                    prdDbg("Nr. of blocks " + brs.Count().ToString());

                    HashSet<Line> lines = localDb.HashSetOfType<Line>(tx, true);
                    lines = lines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        x, "FJV_fremtid", "Distriktets_navn") == curEtapeName)
                        .Where(x => x.Layer == conLayName)
                        .ToHashSet();
                    prdDbg("Nr. of lines " + lines.Count().ToString());

                    HashSet<Line> noCross = localDb.HashSetOfType<Line>(tx, true)
                        .Where(x => x.Layer == "0-NOCROSS_LINE")
                        .ToHashSet();
                    #endregion

                    #region Clear previous data
                    HashSet<Entity> entities = new HashSet<Entity>();
                    entities.UnionWith(plines);
                    entities.UnionWith(brs);
                    entities.UnionWith(lines);
                    #endregion

                    #region Clear previous data
                    foreach (Entity entity in entities)
                    {
                        graphPsm.GetOrAttachPropertySet(entity);
                        graphPsm.WritePropertyString(graphDef.Parent, "");
                        //Protect children written in husnr connection
                        if (entity is BlockReference) continue;
                        graphPsm.WritePropertyString(graphDef.Children, "");
                    }
                    #endregion

                    #region Manage layer for labels
                    string labelLayerName = $"0-FJV_Strækning_label_{curEtapeName}";
                    localDb.CheckOrCreateLayer(labelLayerName);
                    #endregion

                    #region Delete old labels
                    var labels = localDb
                        .HashSetOfType<DBText>(tx)
                        .Where(x => x.Layer == labelLayerName);

                    foreach (DBText label in labels)
                    {
                        label.CheckOrOpenForWrite();
                        label.Erase(true);
                    }
                    #endregion

                    #region Delete old debug plines
                    foreach (Polyline pline in localDb.ListOfType<Polyline>(tx))
                    {
                        if (pline.Layer != "0-FJV_Debug") continue;
                        pline.CheckOrOpenForWrite();
                        pline.Erase(true);
                    }
                    #endregion

                    #region Delete previous stiks
                    HashSet<Line> eksStik = localDb.HashSetOfType<Line>(tx)
                        .Where(x => x.Layer == conLayName)
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName)
                        .ToHashSet();
                    foreach (Entity entity in eksStik)
                    {
                        entity.CheckOrOpenForWrite();
                        entity.Erase(true);
                    }
                    #endregion

                    #region Traverse system and build graph
                    #region Determine entry points
                    HashSet<POI> allPoiCol = new HashSet<POI>();
                    foreach (Polyline item in plines)
                    {
                        allPoiCol.Add(new POI(item.Id, item.StartPoint, POI.EndTypeEnum.Start));
                        allPoiCol.Add(new POI(item.Id, item.EndPoint, POI.EndTypeEnum.End));
                    }

                    var entryPoints = allPoiCol
                            .GroupByCluster((x, y) => x.Point.DistanceHorizontalTo(y.Point), 0.005)
                            .Where(x => x.Count() == 1 && x.Key.EndType == POI.EndTypeEnum.Start);
                    prdDbg($"Entry points count: {entryPoints.Count()}");
                    #endregion

                    int groupCounter = 0;
                    foreach (IGrouping<POI, POI> entryPoint in entryPoints)
                    {
                        //Debug
                        HashSet<Node> nodes = new HashSet<Node>();

                        groupCounter++;

                        //Get the entrypoint element
                        Polyline entryPline = entryPoint.Key.OwnerId.Go<Polyline>(tx);

                        //Mark the element as entry point
                        graphPsm.WritePropertyString(entryPline, graphDef.Parent, $"Entry");

                        #region Traverse system using stack
                        //Using stack traversing strategy
                        Stack<Node> stack = new Stack<Node>();
                        Node startNode = new Node();
                        startNode.Self = entryPline;
                        stack.Push(startNode);
                        int subGroupCounter = 0;
                        int dbgCount = 0;
                        while (stack.Count > 0)
                        {
                            subGroupCounter++;

                            Node curNode = stack.Pop();
                            nodes.Add(curNode);

                            //Write group and subgroup numbers
                            curNode.GroupNumber = groupCounter;
                            curNode.PartNumber = subGroupCounter;

                            //Find next connections
                            var query = allPoiCol.Where(
                                x => x.Point.DistanceHorizontalTo(curNode.Self.EndPoint) < 0.005 &&
                                x.EndType == POI.EndTypeEnum.Start);

                            foreach (POI poi in query)
                            {
                                Polyline connectedItem = poi.OwnerId.Go<Polyline>(tx);

                                Node childNode = new Node();
                                childNode.Self = connectedItem;
                                childNode.Parent = curNode;
                                curNode.ConnectionChildren.Add(childNode);

                                //Push the child to stack for further processing
                                stack.Push(childNode);
                                //prdDbg(dbgCount++.ToString());
                                //System.Windows.Forms.Application.DoEvents();
                                dbgCount++;
                                if (dbgCount > 10000)
                                {
                                    prdDbg($"Muligvis uendelig løkke for objekt: {curNode.Self.Handle}.");
                                    throw new System.Exception("Muligvis uendelig løkke!");
                                }
                            }
                        }
                        #endregion

                        #region Find clients for this strækning
                        //Use stack to get all the stik
                        //Because we need to push husnr objects in to collection
                        //And ingore their "Parent" buildings
                        Stack<BlockReference> buildings = new Stack<BlockReference>();
                        foreach (var br in brs) buildings.Push(br);

                        while (buildings.Count > 0)
                        {
                            BlockReference building = buildings.Pop();

                            string children = graphPsm.ReadPropertyString(building, graphDef.Children);
                            var split = children.Split(';');
                            //It is assumed that blocks with children only occur
                            //When they are connected to husnumre
                            //And thus should not be considered as a building
                            if (split[0].IsNotNoE())
                            {
                                HashSet<Entity> husnrChildren = new HashSet<Entity>();
                                GatherChildren(building, localDb, graphPsm, husnrChildren);
                                foreach (Entity child in husnrChildren)
                                {
                                    if (child is BlockReference block)
                                    {
                                        buildings.Push(block);
                                    }
                                }
                                //Continue here and do not add the parent block to the executing collection
                                continue;
                            }

                            BlockReference br = building;
                            List<Stik> res = new List<Stik>();
                            foreach (Polyline pline in plines)
                            {
                                Point3d closestPoint = pline.GetClosestPointTo(br.Position, false);

                                if (noCross.Count == 0)
                                {
                                    res.Add(new Stik(br.Position.DistanceHorizontalTo(closestPoint),
                                        pline.Id, br.Id, closestPoint));
                                }
                                else
                                {
                                    int intersectionsCounter = 0;
                                    foreach (Line noCrossLine in noCross)
                                    {
                                        using (Line testLine = new Line(br.Position, closestPoint))
                                        using (Point3dCollection p3dcol = new Point3dCollection())
                                        {
                                            noCrossLine.IntersectWith(
                                                testLine, 0, new Plane(), p3dcol, new IntPtr(0), new IntPtr(0));
                                            if (p3dcol.Count > 0) intersectionsCounter += p3dcol.Count;
                                        }
                                    }
                                    if (intersectionsCounter == 0)
                                        res.Add(new Stik(br.Position.DistanceHorizontalTo(
                                            closestPoint), pline.Id, br.Id, closestPoint));
                                }
                            }

                            //Find and add the client blocks to node collection
                            Stik? nearest = res.MinBy(x => x.Dist);
                            if (nearest == default) continue;

                            if (nodes.Any(x => x.Self.Id == nearest.ParentId))
                            {
                                nodes.Where(x => x.Self.Id == nearest.ParentId)
                                    .ToList().ForEach(x => x.ClientChildren
                                    .Add(nearest.ChildId.Go<BlockReference>(tx)));
                            }
                        }
                        #endregion

                        var ordered = nodes.OrderBy(x => x.PartNumber);

                        #region Write node data
                        foreach (Node nd in ordered)
                        {
                            Polyline curPline = nd.Self;                            

                            if (graphPsm.ReadPropertyString(curPline, graphDef.Parent) != "Entry")
                                graphPsm.WritePropertyString(curPline,
                                    graphDef.Parent, nd.Parent.Self.Handle.ToString());

                            //Write strækning id to property
                            fjvFremPsm.GetOrAttachPropertySet(curPline);
                            fjvFremPsm.WritePropertyString(curPline, fjvFremDef.Bemærkninger,
                                $"Strækning {nd.GroupNumber}.{nd.PartNumber}");

                            //Write the children data
                            foreach (Node child in nd.ConnectionChildren)
                            {
                                string curChildrenString = graphPsm.ReadPropertyString(curPline, graphDef.Children);
                                string childHandle = child.Self.Handle.ToString() + ";";
                                if (!curChildrenString.Contains(childHandle)) curChildrenString += childHandle;
                                graphPsm.WritePropertyString(graphDef.Children, curChildrenString);
                            }

                            foreach (BlockReference client in nd.ClientChildren)
                            {
                                //Create connection line
                                Line connection = new Line();
                                connection.SetDatabaseDefaults();
                                connection.Layer = conLayName;
                                connection.StartPoint = curPline
                                    .GetClosestPointTo(client.Position, false);
                                connection.EndPoint = client.Position;
                                connection.AddEntityToDbModelSpace(localDb);

                                //Add connection as parent to client
                                graphPsm.GetOrAttachPropertySet(client);
                                graphPsm.WritePropertyString(graphDef.Parent, connection.Handle.ToString());

                                //Populate connection props
                                graphPsm.GetOrAttachPropertySet(connection);
                                graphPsm.WritePropertyString(graphDef.Children, client.Handle.ToString() + ";");
                                graphPsm.WritePropertyString(graphDef.Parent, curPline.Handle.ToString());

                                //Write area data to connection
                                fjvFremPsm.GetOrAttachPropertySet(connection);
                                fjvFremPsm.WritePropertyString(fjvFremDef.Distriktets_navn, curEtapeName);
                                fjvFremPsm.WritePropertyString(fjvFremDef.Bemærkninger, "Stik");

                                //Add connection to pline's children                                
                                string curChildrenString = graphPsm.ReadPropertyString(curPline, graphDef.Children);
                                string childHandle = connection.Handle.ToString() + ";";
                                if (!curChildrenString.Contains(childHandle)) curChildrenString += childHandle;
                                graphPsm.WritePropertyString(graphDef.Children, curChildrenString);
                            }

                            //Write label
                            string strækningsNr = $"{nd.GroupNumber}.{nd.PartNumber}";

                            //Place label to mark the strækning
                            Point3d midPoint = curPline.GetPointAtDist(curPline.Length / 2);
                            DBText text = new DBText();
                            text.SetDatabaseDefaults();
                            text.TextString = strækningsNr;
                            text.Height = 2.5;
                            text.Position = midPoint;
                            text.Layer = labelLayerName;
                            text.AddEntityToDbModelSpace(localDb);
                        }
                        #endregion

                        #region Debug
                        //Debug
                        List<Point2d> pts = new List<Point2d>();
                        foreach (Node nd in nodes)
                        {
                            for (int i = 0; i < nd.Self.NumberOfVertices; i++)
                            {
                                pts.Add(nd.Self.GetPoint3dAt(i).To2d());
                            }
                        }

                        Polyline circum = IntersectUtilities.Utils.PolylineFromConvexHull(pts);
                        circum.AddEntityToDbModelSpace(localDb);
                        localDb.CheckOrCreateLayer("0-FJV_Debug");
                        circum.Layer = "0-FJV_Debug";
                        #endregion
                    }

                    System.Windows.Forms.Application.DoEvents();
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void dimdumpgraph()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            string curEtapeName = dimaskforarea();
            if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                HashSet<Strækning> strækninger = new HashSet<Strækning>();

                try
                {
                    #region Traverse system and build graph
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    var entryElements = plines.Where(x => graphPsm.FilterPropetyString(x, graphDef.Parent, "Entry"));
                    prdDbg($"Nr. of entry elements: {entryElements.Count()}");

                    //StringBuilder to dump the text
                    StringBuilder sb = new StringBuilder();

                    foreach (Polyline entryElement in entryElements)
                    {
                        //Write group number                        
                        string strNr = fjvFremPsm.ReadPropertyString(entryElement,
                            fjvFremDef.Bemærkninger).Replace("Strækning ", "");

                        sb.AppendLine($"****** Rørgruppe nr.: {strNr.Split('.')[0]} ******");
                        sb.AppendLine();

                        //Using stack traversing strategy
                        Stack<Polyline> stack = new Stack<Polyline>();
                        stack.Push(entryElement);
                        while (stack.Count > 0)
                        {
                            Polyline curItem = stack.Pop();

                            //Write group and subgroup numbers
                            string strNrString = fjvFremPsm.ReadPropertyString(curItem, fjvFremDef.Bemærkninger);
                            sb.AppendLine($"--> {strNrString} <--");

                            strNr = strNrString.Replace("Strækning ", "");

                            //Get the children
                            HashSet<Entity> children = new HashSet<Entity>();
                            Dimensionering.GatherChildren(curItem, localDb, graphPsm, children);

                            //Populate strækning with data
                            Strækning strækning = new Strækning();
                            strækninger.Add(strækning);
                            strækning.GroupNumber = Convert.ToInt32(strNr.Split('.')[0]);
                            strækning.PartNumber = Convert.ToInt32(strNr.Split('.')[1]);
                            strækning.Self = curItem;

                            string parentHandleString = graphPsm.ReadPropertyString(curItem, graphDef.Parent);
                            try
                            {
                                if (parentHandleString != "Entry")
                                {
                                    strækning.Parent = localDb.Go<Entity>(parentHandleString);
                                }
                            }
                            catch (System.Exception)
                            {
                                prdDbg(parentHandleString);
                                throw;
                            }

                            //First print connections
                            foreach (Entity child in children)
                            {
                                if (child is Polyline pline)
                                {                                    
                                    string childStrNr = fjvFremPsm.ReadPropertyString(
                                        pline, fjvFremDef.Bemærkninger).Replace("Strækning ", "");
                                    sb.AppendLine($"{strNr} -> {childStrNr}");

                                    //Push the polyline in to stack to continue iterating
                                    stack.Push(pline);

                                    //Populate strækning with child data
                                    strækning.ConnectionChildren.Add(pline);
                                }
                            }

                            foreach (Entity child in children)
                            {
                                if (child is BlockReference br)
                                {
                                    string vejnavn = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                                    string husnummer = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");

                                    Point3d np = curItem.GetClosestPointTo(br.Position, false);
                                    double st = curItem.GetDistAtPoint(np);

                                    sb.AppendLine($"{vejnavn} {husnummer} - {st.ToString("0.##")}");

                                    //Populate strækning with client data
                                    strækning.ClientChildren.Add(br);
                                }
                            }

                            sb.AppendLine();
                        }
                    }

                    System.Windows.Forms.Application.DoEvents();

                    //Build file name
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string dumpExportFileName = path + "\\dimgraphdump.txt";

                    Utils.ClrFile(dumpExportFileName);
                    Utils.OutputWriter(dumpExportFileName, sb.ToString());
                    #endregion

                    #region Write graph to csv
                    var groups = strækninger.GroupBy(x => x.GroupNumber);
                    foreach (var group in groups)
                    {
                        var ordered = group.OrderBy(x => x.PartNumber);
                        //int newCount = 0;
                        //foreach (var item in ordered)
                        //{
                        //    newCount++;
                        //    item.PartNumber = newCount;
                        //}

                        foreach (var item in ordered) item.PopulateData();

                        int maxSize = ordered.MaxBy(x => x.Data.Count).Data.Count;
                        prdDbg(maxSize.ToString());
                        foreach (var item in ordered) item.PadLists(maxSize);

                        StringBuilder sbG = new StringBuilder();

                        for (int i = 0; i < maxSize; i++)
                        {
                            foreach (var item in ordered)
                            {
                                sbG.Append(item.Data[i] + ";");
                                sbG.Append(item.Distances[i] + ";");
                            }
                            sbG.AppendLine();
                        }

                        dumpExportFileName = path + $"\\Strækning {group.Key}.csv";

                        Utils.ClrFile(dumpExportFileName);
                        Utils.OutputWriter(dumpExportFileName, sbG.ToString());
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void dimwritegraph()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            string curEtapeName = dimaskforarea();
            if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //Settings
                string arrowShape = " -> ";

                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Traverse system and build graph
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    var entryElements = plines.Where(x => graphPsm.FilterPropetyString(x, graphDef.Parent, "Entry"));
                    prdDbg($"Nr. of entry elements: {entryElements.Count()}");

                    //StringBuilder to dump the text
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("digraph G {");

                    foreach (Polyline entryElement in entryElements)
                    {
                        //Collection to store subgraph clusters
                        HashSet<Subgraph> subgraphs = new HashSet<Subgraph>();

                        //Write group number
                        string strNr = fjvFremPsm.ReadPropertyString(entryElement,
                            fjvFremDef.Bemærkninger).Replace("Strækning ", "");

                        string subGraphNr = strNr.Split('.')[0];

                        sb.AppendLine($"subgraph G_{strNr.Split('.')[0]} {{");
                        //sb.AppendLine($"label = \"Delstrækning nr. {subGraphNr}\"");
                        sb.AppendLine("node [shape=record];");

                        //Using stack traversing strategy
                        Stack<Polyline> stack = new Stack<Polyline>();
                        stack.Push(entryElement);
                        while (stack.Count > 0)
                        {
                            Polyline curItem = stack.Pop();

                            //Write group and subgroup numbers
                            string strNrString = fjvFremPsm.ReadPropertyString(curItem, fjvFremDef.Bemærkninger);
                            strNr = strNrString.Replace("Strækning ", "");

                            string curNodeHandle = curItem.Handle.ToString();

                            //Write label
                            sb.AppendLine(
                                $"\"{curNodeHandle}\" " +
                                $"[label = \"{{{strNrString}|{curNodeHandle}}}\"];");

                            //Get the children
                            HashSet<Entity> children = new HashSet<Entity>();
                            Dimensionering.GatherChildren(curItem, localDb, graphPsm, children);

                            //First print connections
                            foreach (Entity child in children)
                            {
                                if (child is Polyline pline)
                                {
                                    string childHandle = child.Handle.ToString();
                                    //psManFjvFremtid.GetOrAttachPropertySet(pline);
                                    //string childStrNr = psManFjvFremtid.ReadPropertyString
                                    //      (fjvFremtidDef.Bemærkninger).Replace("Strækning ", "");
                                    sb.AppendLine($"\"{curNodeHandle}\"{arrowShape}\"{childHandle}\"");

                                    //Push the polyline in to stack to continue iterating
                                    stack.Push(pline);
                                }
                            }

                            //Invoke only subgraph if clients are present
                            if (children.Any(x => x is BlockReference))
                            {
                                Subgraph subgraph = new Subgraph(localDb, curItem, strNrString);
                                subgraphs.Add(subgraph);

                                foreach (Entity child in children)
                                {
                                    if (child is BlockReference br)
                                    {
                                        string childHandle = child.Handle.ToString();
                                        sb.AppendLine($"\"{curNodeHandle}\"{arrowShape}\"{childHandle}\"");
                                        //sb.AppendLine($"{vejnavn} {husnummer} - {st.ToString("0.##")}");

                                        subgraph.Nodes.Add(child.Handle);
                                    }
                                }
                            }
                        }

                        //Write subgraphs
                        int subGraphCount = 0;
                        foreach (Subgraph sg in subgraphs)
                        {
                            subGraphCount++;
                            sb.Append(sg.WriteSubgraph(subGraphCount));
                        }

                        //Close subgraph curly braces
                        sb.AppendLine("}");
                    }

                    //Close graph curly braces
                    sb.AppendLine("}");

                    System.Windows.Forms.Application.DoEvents();

                    //Build file name
                    if (!Directory.Exists(@"C:\Temp\"))
                        Directory.CreateDirectory(@"C:\Temp\");

                    //Write the collected graphs to one file
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\DimGraph.dot"))
                    {
                        file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
                    }

                    System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    cmd.StartInfo.Arguments = @"/c ""dot -Tpdf DimGraph.dot > DimGraph.pdf""";
                    cmd.Start();
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void dimconnecthusnr()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            string curEtapeName = dimaskforarea();
            if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //Settings
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Manage Husnummer line layer
                    string husNrLineLayerName = "0-HUSNUMMER_LINE";
                    localDb.CheckOrCreateLayer(husNrLineLayerName, 90);

                    string husNrBlockLayerName = "0-HUSNUMMER_BLOCK";
                    localDb.CheckOrCreateLayer(husNrBlockLayerName, 90);
                    #endregion

                    #region Delete previous blocks and lines
                    HashSet<Line> linesToDelete = localDb.ListOfType<Line>(tx)
                        .Where(x => x.Layer == husNrLineLayerName)
                        .ToHashSet();
                    foreach (Line line in linesToDelete)
                    {
                        if (fjvFremPsm.ReadPropertyString(line, fjvFremDef.Distriktets_navn) == $"{curEtapeName}{HusnrSuffix}")
                        {
                            line.CheckOrOpenForWrite();
                            line.Erase(true);
                        }
                    }

                    HashSet<BlockReference> blocksToDelete = localDb.ListOfType<BlockReference>(tx)
                        .Where(x => x.Layer == husNrBlockLayerName).ToHashSet();
                    foreach (BlockReference br in blocksToDelete)
                    {
                        string område = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Distriktets_navn");
                        if (område == $"{curEtapeName}{HusnrSuffix}")
                        {
                            br.CheckOrOpenForWrite();
                            br.Erase(true);
                        }

                    }
                    #endregion

                    #region Dialog box for selecting the geojson file
                    string dbFilename = localDb.OriginalFileName;
                    string path = System.IO.Path.GetDirectoryName(dbFilename);
                    string nyHusnumreFilename = path + $"\\husnumre_{curEtapeName}.geojson";
                    string husnumreStr;
                    if (File.Exists(nyHusnumreFilename))
                    {
                        husnumreStr = File.ReadAllText(nyHusnumreFilename);
                    }
                    else
                    {
                        string fileName = string.Empty;
                        OpenFileDialog dialog = new OpenFileDialog()
                        {
                            Title = "Choose husnumre file:",
                            DefaultExt = "geojson",
                            Filter = "Geojson files (*.geojson)|*.geojson|All files (*.*)|*.*",
                            FilterIndex = 0
                        };
                        if (dialog.ShowDialog() == true)
                        {
                            fileName = dialog.FileName;
                            husnumreStr = File.ReadAllText(fileName);
                        }
                        else { throw new System.Exception("Cannot find husnumre file!"); }
                    }

                    FeatureCollection husnumre = JsonConvert.DeserializeObject<FeatureCollection>(husnumreStr);
                    #endregion

                    #region Connect bygning to husnumre
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                            x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .ToHashSet();

                    prdDbg($"Number of bygninger: {brs.Count}.");

                    #region Join husnumre with bygninger
                    //First filter bygninger til kun at indeholde ønskede anvendelseskoder
                    brs = brs.Where(x => AnvFilter(x)).ToHashSet();

                    bool AnvFilter(BlockReference block)
                    {
                        string anvKode = PropertySetManager.ReadNonDefinedPropertySetString(
                            block, "BBR", "BygningsAnvendelseNyKode");
                        return Dimensionering.AcceptedAnvCodes.Contains(anvKode);
                    }

                    //Then filter husnumre til kun at indeholde dem der henviser til brs
                    //var filteredHusnumre = husnumre.features.Where(x => brs
                    //    .Any(y => x?.properties?.adgangTilBygning?.ToUpper() == GetIdLokalId(y)));

                    var join = husnumre.features.Join(
                        brs,
                        nr => nr?.properties?.adgangTilBygning?.ToUpper(),
                        br => GetIdLokalId(br),
                        (nr, br) => nr);

                    string GetIdLokalId(BlockReference block)
                    {
                        string id = PropertySetManager.ReadNonDefinedPropertySetString(
                            block, "BBR", "id_lokalId").ToUpper();
                        return id;
                    }

                    var groupedHusnumre = join.GroupBy(x => x.properties.adgangTilBygning.ToUpper());

                    foreach (var group in groupedHusnumre)
                    {
                        if (group.Count() < 2) continue;
                        BlockReference buildingBlock = brs.Where(x => GetIdLokalId(x) == group.Key).FirstOrDefault();

                        //Reset children for the bygblock
                        graphPsm.WritePropertyString(buildingBlock, graphDef.Children, "");

                        //Draw lines
                        foreach (Feature husnr in group)
                        {
                            Point3d husNrLocation = new Point3d(
                                husnr.geometry.coordinates[0], husnr.geometry.coordinates[1], 0.0);

                            //Create block for husnr
                            BlockReference husNrBlock = localDb
                                .CreateBlockWithAttributes(buildingBlock.RealName(), husNrLocation);
                            husNrBlock.ScaleFactors = new Scale3d(0.4);
                            husNrBlock.Layer = husNrBlockLayerName;

                            //Create line to connect byg and husnr
                            Line conLine = new Line(buildingBlock.Position, husNrLocation);
                            conLine.Layer = husNrLineLayerName;
                            conLine.AddEntityToDbModelSpace(localDb);

                            //Populate graph values of objects
                            //Write values to parent block
                            string children = graphPsm.ReadPropertyString(buildingBlock, graphDef.Children);
                            children += conLine.Handle.ToString() + ";";
                            graphPsm.WritePropertyString(graphDef.Children, children);
                            //Prepare values to write to husnr block
                            double estimeretForbrug = PropertySetManager
                                .ReadNonDefinedPropertySetDouble(buildingBlock, "BBR", "EstimeretVarmeForbrug") /
                                group.Count();
                            string id_localId = PropertySetManager
                                .ReadNonDefinedPropertySetString(buildingBlock, "BBR", "id_lokalId").ToUpper();
                            string type = PropertySetManager
                                .ReadNonDefinedPropertySetString(buildingBlock, "BBR", "Type");

                            //Write graph values for connection line
                            graphPsm.GetOrAttachPropertySet(conLine);
                            graphPsm.WritePropertyString(graphDef.Parent, buildingBlock.Handle.ToString());
                            graphPsm.WritePropertyString(graphDef.Children, husNrBlock.Handle.ToString() + ";");
                            //Write area values for connection line
                            fjvFremPsm.GetOrAttachPropertySet(conLine);
                            fjvFremPsm.WritePropertyString(fjvFremDef.Distriktets_navn, $"{curEtapeName}{HusnrSuffix}");
                            fjvFremPsm.WritePropertyString(fjvFremDef.Bemærkninger, "Husnr forbindelse");
                            //Write values to husnr block
                            graphPsm.GetOrAttachPropertySet(husNrBlock);
                            graphPsm.WritePropertyString(graphDef.Parent, conLine.Handle.ToString());
                            PropertySetManager.AttachNonDefinedPropertySet(localDb, husNrBlock, "BBR");
                            PropertySetManager.WriteNonDefinedPropertySetDouble(
                                husNrBlock, "BBR", "EstimeretVarmeForbrug", estimeretForbrug);
                            PropertySetManager.WriteNonDefinedPropertySetString(
                                husNrBlock, "BBR", "Adresse", husnr.properties.Adresse);
                            PropertySetManager.WriteNonDefinedPropertySetString(
                                husNrBlock, "BBR", "Distriktets_navn", $"{curEtapeName}{HusnrSuffix}");
                            PropertySetManager.WriteNonDefinedPropertySetString(
                                husNrBlock, "BBR", "id_lokalId", husnr.properties.id_lokalId);
                            PropertySetManager.WriteNonDefinedPropertySetString(
                                husNrBlock, "BBR", "Type", type);
                        }
                    }
                    #endregion

                    #region Write graph to file
                    //Build file name
                    //if (!Directory.Exists(@"C:\Temp\"))
                    //    Directory.CreateDirectory(@"C:\Temp\");

                    ////Write the collected graphs to one file
                    //using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\AutoDimGraph.dot"))
                    //{
                    //    file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
                    //}

                    //System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                    //cmd.StartInfo.FileName = "cmd.exe";
                    //cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    //cmd.StartInfo.Arguments = @"/c ""dot -Tpdf AutoDimGraph.dot > AutoDimGraph.pdf""";
                    //cmd.Start();
                    #endregion

                    System.Windows.Forms.Application.DoEvents();
                    #endregion
                }
                #region Catch and finally
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
                #endregion
            }
        }
        internal static string dimaskforarea()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            #region Ask for region name
            string curEtapeName = "";
            PromptStringOptions pStrOpts = new PromptStringOptions("\nOmråde navn: ");
            pStrOpts.AllowSpaces = true;
            PromptResult pStrRes = editor.GetString(pStrOpts);
            if (pStrRes.Status != PromptStatus.OK) return "";
            curEtapeName = pStrRes.StringResult;
            return curEtapeName;
            #endregion
        }
        internal static string dimaskforareaps()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Document doc = docCol.MdiActiveDocument;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                HashSet<string> names = new();
                var brs = localDb.HashSetOfType<BlockReference>(tx);
                foreach (BlockReference br in brs)
                {
                    string område = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Distriktets_navn");
                    names.Add(område);
                }
                var propertyName = StringGridFormCaller.Call(names.OrderBy(x => x), "Select district: ");

                tx.Abort();

                return propertyName;
            }
        }
        internal static string dimaskforpropertysetname()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            #region Ask for property set name
            string curEtapeName = "";
            PromptStringOptions pStrOpts = new PromptStringOptions("\nProperty set (Ps) eller table (Od) navn: ");
            pStrOpts.AllowSpaces = true;
            PromptResult pStrRes = editor.GetString(pStrOpts);
            if (pStrRes.Status != PromptStatus.OK) return "";
            curEtapeName = pStrRes.StringResult;
            return curEtapeName;
            #endregion
        }
        internal static string dimaskforpropertyname()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            #region Ask for property name
            string curEtapeName = "";
            PromptStringOptions pStrOpts = new PromptStringOptions("\nProperty navn: ");
            pStrOpts.AllowSpaces = true;
            PromptResult pStrRes = editor.GetString(pStrOpts);
            if (pStrRes.Status != PromptStatus.OK) return "";
            curEtapeName = pStrRes.StringResult;
            return curEtapeName;
            #endregion
        }
        private static void TraversePath(ExcelNode node, int pathId)
        {
            node.PathId = pathId;
            if (node.Parent != null && node.Parent.PathId == 0) TraversePath(node.Parent, pathId);
        }
        private static IEnumerable<string> BFS(ExcelNode root)
        {
            var queue = new Queue<Tuple<string, ExcelNode>>();
            queue.Enqueue(new Tuple<string, ExcelNode>(root.Name, root));

            while (queue.Any())
            {
                var node = queue.Dequeue();
                if (node.Item2.ConnectionChildren.Any())
                {
                    foreach (var child in node.Item2.ConnectionChildren)
                    {
                        queue.Enqueue(new Tuple<string, ExcelNode>(node.Item1 + "-" + child.Name, child));
                    }
                }
                else
                {
                    yield return node.Item1;
                }
            }
        }
        /// <summary>
        /// Obsolete
        /// </summary>
        internal static void dimstikautogen()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            string curEtapeName = dimaskforarea();
            if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Manage layer to contain connection lines
                LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;

                string conLayName = "0-CONNECTION_LINE";
                if (!lt.Has(conLayName))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = conLayName;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 2);

                    //Make layertable writable
                    lt.CheckOrOpenForWrite();

                    //Add the new layer to layer table
                    Oid ltId = lt.Add(ltr);
                    tx.AddNewlyCreatedDBObject(ltr, true);
                }

                string noCrossLayName = "0-NOCROSS_LINE";
                if (!lt.Has(noCrossLayName))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = noCrossLayName;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                    ltr.LineWeight = LineWeight.LineWeight030;

                    //Make layertable writable
                    lt.CheckOrOpenForWrite();

                    //Add the new layer to layer table
                    Oid ltId = lt.Add(ltr);
                    tx.AddNewlyCreatedDBObject(ltr, true);
                }

                #endregion

                #region Delete previous stiks
                HashSet<Line> eksStik = localDb.HashSetOfType<Line>(tx)
                    .Where(x => x.Layer == conLayName)
                    .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName)
                    .ToHashSet();
                foreach (Entity entity in eksStik)
                {
                    entity.CheckOrOpenForWrite();
                    entity.Erase(true);
                }
                #endregion

                PropertySetManager psManFjvFremtid = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremtidDef = new PSetDefs.FJV_fremtid();

                PropertySetManager psManGraph = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph driDimGraphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Stik counting
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg($"Number of plines: {plines.Count}");

                    HashSet<string> acceptedTypes = new HashSet<string>() { "El", "Naturgas", "Varmepumpe", "Fast brændsel", "Olie", "Andet" };

                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => acceptedTypes.Contains(PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        .ToHashSet();
                    prdDbg($"Number of blocks: {brs.Count}");

                    //Collection to hold no cross lines
                    HashSet<Line> noCross = localDb.HashSetOfType<Line>(tx, true)
                        .Where(x => x.Layer == "0-NOCROSS_LINE")
                        .ToHashSet();

                    foreach (BlockReference br in brs)
                    {
                        List<Stik> res = new List<Stik>();
                        foreach (Polyline pline in plines)
                        {
                            Point3d closestPoint = pline.GetClosestPointTo(br.Position, false);

                            if (noCross.Count == 0)
                            {
                                res.Add(new Stik(br.Position.DistanceHorizontalTo(closestPoint), pline.Id, br.Id, closestPoint));
                            }
                            else
                            {
                                int intersectionsCounter = 0;
                                foreach (Line noCrossLine in noCross)
                                {
                                    using (Line testLine = new Line(br.Position, closestPoint))
                                    using (Point3dCollection p3dcol = new Point3dCollection())
                                    {
                                        noCrossLine.IntersectWith(testLine, 0, new Plane(), p3dcol, new IntPtr(0), new IntPtr(0));
                                        if (p3dcol.Count > 0) intersectionsCounter += p3dcol.Count;
                                    }
                                }
                                if (intersectionsCounter == 0) res.Add(new Stik(br.Position.DistanceHorizontalTo(closestPoint), pline.Id, br.Id, closestPoint));
                            }
                        }

                        var nearest = res.MinBy(x => x.Dist);
                        if (nearest == default) continue;

                        #region Create line
                        Line connection = new Line();
                        connection.SetDatabaseDefaults();
                        connection.Layer = conLayName;
                        connection.StartPoint = br.Position;
                        connection.EndPoint = nearest.NearestPoint;
                        connection.AddEntityToDbModelSpace(localDb);
                        //Write area data
                        psManFjvFremtid.GetOrAttachPropertySet(connection);
                        psManFjvFremtid.WritePropertyString(fjvFremtidDef.Distriktets_navn, curEtapeName);
                        psManFjvFremtid.WritePropertyString(fjvFremtidDef.Bemærkninger, "Stik");
                        //Write graph data
                        psManGraph.GetOrAttachPropertySet(connection);
                        psManGraph.WritePropertyString(driDimGraphDef.Children, br.Handle.ToString());
                        psManGraph.WritePropertyString(driDimGraphDef.Parent, nearest.ParentId.Go<Entity>(tx).Handle.ToString());
                        #endregion

                        #region Write BR parent data
                        psManGraph.GetOrAttachPropertySet(br);
                        psManGraph.WritePropertyString(driDimGraphDef.Parent, connection.Handle.ToString());
                        #endregion

                        #region Write PL children data
                        {
                            Polyline pline = nearest.ParentId.Go<Polyline>(tx);
                            string currentChildren = psManGraph.ReadPropertyString(pline, driDimGraphDef.Children);
                            currentChildren += connection.Handle.ToString() + ";";
                            psManGraph.WritePropertyString(pline, driDimGraphDef.Children, currentChildren);
                        }
                        #endregion
                    }

                    System.Windows.Forms.Application.DoEvents();

                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.Message);
                    return;
                }
                finally
                {

                }
                tx.Commit();
            }
        }
        internal static void GatherChildren(
            Entity ent, Database db, PropertySetManager psmGraph, HashSet<Entity> children)
        {
            PSetDefs.DriDimGraph defGraph = new PSetDefs.DriDimGraph();

            string childrenString = psmGraph.ReadPropertyString(ent, defGraph.Children);

            var splitArray = childrenString.Split(';');

            foreach (var childString in splitArray)
            {
                if (childString.IsNoE()) continue;

                Entity child = db.Go<Entity>(childString);

                switch (child)
                {
                    case Polyline pline:
                        children.Add(child);
                        break;
                    case BlockReference br:
                        children.Add(child);
                        break;
                    case Line line:
                        GatherChildren(child, db, psmGraph, children);
                        break;
                    default:
                        throw new System.Exception($"Unexpected type {child.GetType().Name}!");
                }
            }
        }
        internal static void GatherChildren(
            Entity ent, Database db, PropertySetManager psmGraph,
            HashSet<ExcelNode> plineChildren, HashSet<BlockReference> blockChildren)
        {
            HashSet<Entity> children = new HashSet<Entity>();
            GatherChildren(ent, db, psmGraph, children);

            foreach (Entity child in children)
            {
                switch (child)
                {
                    case Polyline pline:
                        ExcelNode newNode = new ExcelNode();
                        newNode.Self = pline;
                        plineChildren.Add(newNode);
                        break;
                    case BlockReference br:
                        blockChildren.Add(br);
                        break;
                    default:
                        throw new System.Exception($"Unexpected type {child.GetType().Name}!");
                }
            }
        }
        internal static void GatherChildren(ExcelNode node, Database db)
        {
            string childrenString =
                PropertySetManager.ReadNonDefinedPropertySetString(node.Self, "DriDimGraph", "Children");

            var splitArray = childrenString.Split(';');

            foreach (var childString in splitArray)
            {
                if (childString.IsNoE()) continue;

                Entity child = db.Go<Entity>(childString);

                switch (child)
                {
                    case Polyline pline:
                        ExcelNode childNode = new ExcelNode();
                        childNode.Self = pline;
                        node.ConnectionChildren.Add(childNode);
                        break;
                    case BlockReference br:
                        node.ClientChildren.Add(br);
                        break;
                    case Line line:
                        GatherChildren(line, node);
                        break;
                    default:
                        throw new System.Exception($"Unexpected type {child.GetType().Name}!");
                }
            }
        }
        internal static void GatherChildren(Line line, ExcelNode node)
        {
            string childrenString =
                PropertySetManager.ReadNonDefinedPropertySetString(line, "DriDimGraph", "Children");

            var splitArray = childrenString.Split(';');

            foreach (var childString in splitArray)
            {
                if (childString.IsNoE()) continue;

                BlockReference child = line.Database.Go<BlockReference>(childString);

                node.ClientChildren.Add(child);
            }
        }
        internal static void dimwriteexcel()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            string curEtapeName = dimaskforarea();
            if (curEtapeName.IsNoE()) return;

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //Settings
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Init excel objects
                    #region Dialog box for selecting the excel file
                    string fileName = string.Empty;
                    OpenFileDialog dialog = new OpenFileDialog()
                    {
                        Title = "Choose excel file:",
                        DefaultExt = "xlsm",
                        Filter = "Excel files (*.xlsm)|*.xlsm|All files (*.*)|*.*",
                        FilterIndex = 0
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        fileName = dialog.FileName;
                    }
                    else { tx.Abort(); return; }
                    #endregion

                    Workbook wb;
                    Sheets wss;
                    Worksheet ws;
                    Microsoft.Office.Interop.Excel.Application oXL;
                    object misValue = System.Reflection.Missing.Value;
                    oXL = new Microsoft.Office.Interop.Excel.Application();
                    oXL.Visible = false;
                    oXL.DisplayAlerts = false;
                    wb = oXL.Workbooks.Open(fileName,
                        0, false, 5, "", "", false, XlPlatform.xlWindows, "", true, false,
                        0, false, false, XlCorruptLoad.xlNormalLoad);
                    oXL.Calculation = XlCalculation.xlCalculationManual;
                    #endregion

                    #region Traverse system and build graph
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines
                        .Where(x => isLayerAcceptedInFjv(x.Layer))
                        .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        x, "FJV_fremtid", "Distriktets_navn") == curEtapeName).ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    var entryElements = plines.Where(x => graphPsm.FilterPropetyString(
                        x, graphDef.Parent, "Entry"));
                    prdDbg($"Nr. of entry elements: {entryElements.Count()}");

                    Dimensionering.GlobalSheetCount = new GlobalSheetCount();

                    //Write graph docs
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("digraph G {");

                    foreach (Polyline entryElement in entryElements)
                    {
                        #region Build graph nodes
                        HashSet<ExcelNode> nodes = new HashSet<ExcelNode>();
                        ExcelNode seedNode = new ExcelNode();
                        seedNode.Self = entryElement;
                        seedNode.NodeLevel = 1;

                        //Using stack traversing strategy
                        Stack<ExcelNode> stack = new Stack<ExcelNode>();
                        stack.Push(seedNode);
                        while (stack.Count > 0)
                        {
                            ExcelNode node = stack.Pop();
                            nodes.Add(node);

                            //Write group and subgroup numbers
                            string strNrString = fjvFremPsm.ReadPropertyString(node.Self, fjvFremDef.Bemærkninger);
                            node.SetGroupAndPartNumbers(strNrString);

                            //Get the children
                            Dimensionering.GatherChildren(node, localDb);

                            //First print connections
                            foreach (ExcelNode child in node.ConnectionChildren)
                            {
                                child.Parent = node;
                                child.NodeLevel = node.NodeLevel + 1;

                                //Push the childExcelNode in to stack to continue iterating
                                stack.Push(child);
                            }
                        }
                        #endregion

                        #region Find paths
                        int pathId = 0;
                        while (nodes.Any(x => x.PathId == 0))
                        {
                            pathId++;
                            var curNode = nodes
                                .Where(x => x.PathId == 0)
                                .MaxBy(x => x.NodeLevel);
                            TraversePath(curNode, pathId);
                        }

                        //Organize paths
                        //Order is important -> using a list
                        List<Path> paths = new List<Path>();
                        var groups = nodes.GroupBy(x => x.PathId).OrderBy(x => x.Key);
                        foreach (var group in groups)
                        {
                            var ordered = group.OrderByDescending(x => x.NodeLevel);
                            prdDbg(string.Join("->", ordered.Select(x => x.Name.Replace("ækning", ""))));
                            Path path = new Path();
                            paths.Add(path);
                            path.PathNumber = group.Key;
                            path.NodesOnPath = ordered.ToList();
                        }
                        #endregion

                        #region Populate path with sheet data
                        foreach (Path path in paths) path.PopulateSheets();
                        #endregion

                        #region Replace path references with sheet numbers
                        List<ExcelSheet> sheets = new List<ExcelSheet>();
                        foreach (Path path in paths) sheets.AddRange(path.Sheets);
                        var orderedSheets = sheets.OrderBy(x => x.SheetNumber);
                        prdDbg($"Number of sheets total: {sheets.Count}");

                        foreach (ExcelSheet sheet in sheets)
                        {
                            for (int i = 0; i < sheet.Adresser.Count; i++)
                            {
                                string current = sheet.Adresser[i];
                                if (current.Contains(PRef))
                                {
                                    current = current.Replace(PRef, "");
                                    int pathRef = Convert.ToInt32(current);

                                    Path refPath = paths
                                        .Where(x => x.PathNumber == pathRef)
                                        .FirstOrDefault();

                                    ExcelSheet refSheet = refPath.Sheets.Last();

                                    sheet.Adresser[i] =
                                        (refSheet.SheetNumber).ToString();
                                }
                            }
                        }
                        #endregion

                        #region Populate excel file
                        foreach (ExcelSheet sheet in orderedSheets)
                        {
                            prdDbg($"Writing sheet: {sheet.SheetNumber}");
                            System.Windows.Forms.Application.DoEvents();

                            ws = (Worksheet)wb.Worksheets[sheet.SheetNumber.ToString()];
                            //Write addresses
                            int row = 60; int col = 5;
                            for (int i = 0; i < sheet.Adresser.Count; i++)
                            {
                                ws.Cells[row, col] = sheet.Adresser[i];
                                row++;
                            }
                            row = 60; col = 17;
                            for (int i = 0; i < sheet.Længder.Count; i++)
                            {
                                ws.Cells[row, col] = sheet.Længder[i];
                                row++;
                            }
                        }
                        #endregion

                        #region Write graph documentation
                        int subGraphNr = paths.Select(x => x.NodesOnPath.First().GroupNumber).First();
                        sb.AppendLine(
                            $"subgraph G_{subGraphNr} {{");
                        sb.AppendLine("node [shape=record]");

                        foreach (ExcelSheet sheet in orderedSheets)
                        {
                            string strækninger = string.Join(
                                "|", sheet.SheetParts.Select(x => x.Name.Replace("Strækning ", "")).ToArray());

                            Regex regex = new Regex(@"^\d{1,3}");
                            foreach (string s in sheet.Adresser)
                            {
                                if (regex.IsMatch(s)) sb.AppendLine(
                                    $"\"{sheet.SheetNumber}\" -> \"{s}\"");
                            }

                            sb.AppendLine(
                                $"\"{sheet.SheetNumber}\" " +
                                $"[label = \"{{{sheet.SheetNumber}|{strækninger}}}\"]; ");
                        }

                        //close subgraph
                        sb.AppendLine("}");
                        #endregion
                    }

                    //close graph
                    sb.AppendLine("}");

                    #region Write graph to file
                    //Build file name
                    if (!Directory.Exists(@"C:\Temp\"))
                        Directory.CreateDirectory(@"C:\Temp\");

                    //Write the collected graphs to one file
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\AutoDimGraph.dot"))
                    {
                        file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
                    }

                    System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    cmd.StartInfo.Arguments = @"/c ""dot -Tpdf AutoDimGraph.dot > AutoDimGraph.pdf""";
                    cmd.Start();
                    #endregion

                    oXL.Calculation = XlCalculation.xlCalculationAutomatic;
                    wb.Save();
                    wb.Close();
                    oXL.Quit();

                    System.Windows.Forms.Application.DoEvents();
                    #endregion
                }
                #region Catch and finally
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                tx.Commit();
                #endregion
            }
        }
        internal static void dimwriteallexcel(Workbook wb, string curEtapeName, string basePath)
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            void wr(string inp) => editor.WriteMessage(inp);

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                //Settings
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                try
                {
                    #region Init excel objects
                    Worksheet ws;
                    #endregion

                    #region Traverse system and build graph
                    HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx, true);
                    plines = plines
                        .Where(x => isLayerAcceptedInFjv(x.Layer))
                        .Where(x => fjvFremPsm.ReadPropertyString(x, fjvFremDef.Distriktets_navn) == curEtapeName)
                        .ToHashSet();
                    prdDbg("Nr. of plines " + plines.Count().ToString());

                    var entryElements = plines.Where(x => graphPsm.FilterPropetyString(
                        x, graphDef.Parent, "Entry"));
                    prdDbg($"Nr. of entry elements: {entryElements.Count()}");

                    Dimensionering.GlobalSheetCount = new GlobalSheetCount();

                    //Write graph docs
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("digraph G {");

                    foreach (Polyline entryElement in entryElements)
                    {
                        #region Build graph nodes
                        HashSet<ExcelNode> nodes = new HashSet<ExcelNode>();
                        ExcelNode seedNode = new ExcelNode();
                        seedNode.Self = entryElement;
                        seedNode.NodeLevel = 1;

                        //Using stack traversing strategy
                        Stack<ExcelNode> stack = new Stack<ExcelNode>();
                        stack.Push(seedNode);
                        while (stack.Count > 0)
                        {
                            ExcelNode node = stack.Pop();
                            nodes.Add(node);

                            //Write group and subgroup numbers
                            string strNrString = fjvFremPsm.ReadPropertyString(node.Self, fjvFremDef.Bemærkninger);
                            node.SetGroupAndPartNumbers(strNrString);

                            //Get the children
                            Dimensionering.GatherChildren(node, localDb);

                            //First print connections
                            foreach (ExcelNode child in node.ConnectionChildren)
                            {
                                child.Parent = node;
                                child.NodeLevel = node.NodeLevel + 1;

                                //Push the childExcelNode in to stack to continue iterating
                                stack.Push(child);
                            }
                        }
                        #endregion

                        #region Find paths
                        int pathId = 0;
                        while (nodes.Any(x => x.PathId == 0))
                        {
                            pathId++;
                            var curNode = nodes
                                .Where(x => x.PathId == 0)
                                .MaxBy(x => x.NodeLevel);
                            TraversePath(curNode, pathId);
                        }

                        //Organize paths
                        //Order is important -> using a list
                        List<Path> paths = new List<Path>();
                        var groups = nodes.GroupBy(x => x.PathId).OrderBy(x => x.Key);
                        foreach (var group in groups)
                        {
                            var ordered = group.OrderByDescending(x => x.NodeLevel);
                            //prdDbg(string.Join("->", ordered.Select(x => x.Name.Replace("ækning", ""))));
                            Path path = new Path();
                            paths.Add(path);
                            path.PathNumber = group.Key;
                            path.NodesOnPath = ordered.ToList();
                        }
                        #endregion

                        #region Populate path with sheet data
                        foreach (Path path in paths) path.PopulateSheets();
                        #endregion

                        #region Replace path references with sheet numbers
                        List<ExcelSheet> sheets = new List<ExcelSheet>();
                        foreach (Path path in paths) sheets.AddRange(path.Sheets);
                        var orderedSheets = sheets.OrderBy(x => x.SheetNumber);
                        if (sheets.Count > 100)
                        {
                            throw new System.Exception($"FEJL! For mange sheets: {sheets.Count}. Skal være mindre eller lig med 100.");
                        }
                        else prdDbg($"Number of sheets total: {sheets.Count}");

                        foreach (ExcelSheet sheet in sheets)
                        {
                            for (int i = 0; i < sheet.Adresser.Count; i++)
                            {
                                string current = sheet.Adresser[i];
                                if (current.Contains(PRef))
                                {
                                    current = current.Replace(PRef, "");
                                    int pathRef = Convert.ToInt32(current);

                                    Path refPath = paths
                                        .Where(x => x.PathNumber == pathRef)
                                        .FirstOrDefault();

                                    ExcelSheet refSheet = refPath.Sheets.Last();

                                    sheet.Adresser[i] =
                                        (refSheet.SheetNumber).ToString();
                                }
                            }
                        }
                        #endregion

                        #region Populate excel file
                        prdDbg("Writing sheet: ");
                        foreach (ExcelSheet sheet in orderedSheets)
                        {
                            wr($" {sheet.SheetNumber}");
                            System.Windows.Forms.Application.DoEvents();

                            ws = (Worksheet)wb.Worksheets[sheet.SheetNumber.ToString()];
                            //Write addresses
                            int row = 60; int col = 5;
                            for (int i = 0; i < sheet.Adresser.Count; i++)
                            {
                                ws.Cells[row, col] = sheet.Adresser[i];
                                row++;
                            }
                            row = 60; col = 17;
                            for (int i = 0; i < sheet.Længder.Count; i++)
                            {
                                ws.Cells[row, col] = sheet.Længder[i];
                                row++;
                            }
                        }
                        #endregion

                        #region Write graph documentation
                        int subGraphNr = paths.Select(x => x.NodesOnPath.First().GroupNumber).First();
                        sb.AppendLine(
                            $"subgraph G_{subGraphNr} {{");
                        sb.AppendLine("node [shape=record]");

                        foreach (ExcelSheet sheet in orderedSheets)
                        {
                            string strækninger = string.Join(
                                "|", sheet.SheetParts.Select(x => x.Name.Replace("Strækning ", "")).ToArray());

                            Regex regex = new Regex(@"^\d{1,3}");
                            foreach (string s in sheet.Adresser)
                            {
                                if (regex.IsMatch(s)) sb.AppendLine(
                                    $"\"{sheet.SheetNumber}\" -> \"{s}\"");
                            }

                            sb.AppendLine(
                                $"\"{sheet.SheetNumber}\" " +
                                $"[label = \"{{{sheet.SheetNumber}|{strækninger}}}\"]; ");
                        }

                        //close subgraph
                        sb.AppendLine("}");
                        #endregion
                    }

                    //close graph
                    sb.AppendLine("}");

                    #region Write graph to file
                    //Build file name
                    if (!Directory.Exists(@"C:\Temp\"))
                        Directory.CreateDirectory(@"C:\Temp\");

                    //Write the collected graphs to one file
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter($"C:\\Temp\\AutoDimGraph.dot"))
                    {
                        file.WriteLine(sb.ToString()); // "sb" is the StringBuilder
                    }

                    System.Diagnostics.Process cmd = new System.Diagnostics.Process();
                    cmd.StartInfo.FileName = "cmd.exe";
                    cmd.StartInfo.WorkingDirectory = @"C:\Temp\";
                    cmd.StartInfo.Arguments = @"/c ""dot -Tpdf AutoDimGraph.dot > AutoDimGraph.pdf""";
                    cmd.Start();
                    cmd.WaitForExit();

                    if (File.Exists(basePath + curEtapeName + ".pdf"))
                        File.Delete(basePath + curEtapeName + ".pdf");
                    File.Move("C:\\Temp\\AutoDimGraph.pdf", basePath + curEtapeName + ".pdf");
                    #endregion

                    System.Windows.Forms.Application.DoEvents();
                    #endregion
                }
                #region Catch and finally
                catch (System.Exception ex)
                {
                    tx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                tx.Commit();
                #endregion
            }
        }
        internal static void dimimportdim(
            Workbook wb,
            Database dimDb,
            string areaName,
            Dictionary<string, BlockReference> brDict,
            ILookup<string, IGrouping<string, Polyline>> plLookup,
            PipeSeriesEnum series
            )
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            using (Transaction dimTx = dimDb.TransactionManager.StartTransaction())
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                #region Linetype Generation preparation
                //Modify the standard text style to have Arial font
                TextStyleTable tst = dimDb.TextStyleTableId.Go<TextStyleTable>(dimTx);
                if (tst.Has("Standard"))
                {
                    TextStyleTableRecord tsr = tst["Standard"]
                        .Go<TextStyleTableRecord>(dimTx, OpenMode.ForWrite);
                    tsr.FileName = "arial.ttf";
                }

                //Preapare for linetype creation
                LinetypeTable ltt = dimDb.LinetypeTableId.Go<LinetypeTable>(dimTx, (OpenMode)1); 
                #endregion

                #region PropertyData setup
                //Settings
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                PropertySetManager piplPsm = new PropertySetManager(dimDb, PSetDefs.DefinedSets.DriPipelineData);
                PSetDefs.DriPipelineData piplDef = new PSetDefs.DriPipelineData();

                #endregion

                void wr(string inp) => editor.WriteMessage(inp);

                StringBuilder sb = new StringBuilder();

                try
                {
#if DEBUG
                    HashSet<string> sysDns = new HashSet<string>();
#endif
                    #region Read excel workbook data into a list: dimList<(string Name, int Dim)>
                    //Determine the number of numeric sheets
                    //This change is because the number of sheets is not fixed
                    var query = wb.Sheets
                        .Cast<Worksheet>()
                        .Where(x => int.TryParse(x.Name, out _))
                        .Select(x => int.Parse(x.Name));
                    IEnumerable<int> sheetRange = Enumerable.Range(query.Min(), query.Max());
                    Handle zeroHandle = new Handle(Convert.ToInt64("0", 16));
                    var dimList = new List<DimEntry>();
                    Worksheet ws;
                    foreach (int sheetNumber in sheetRange)
                    {
                        ws = wb.Sheets[sheetNumber.ToString()];
                        wr(" " + sheetNumber.ToString());
                        System.Windows.Forms.Application.DoEvents();

                        Array namesArray = (System.Array)ws.Range["E60:E109"].Cells.Value;
                        Array dimsArray = (System.Array)ws.Range["U4:U53"].Cells.Value;

                        var namesList = new List<string>();
                        var dimsList = new List<string>();

                        foreach (var item in namesArray) namesList.Add(item?.ToString() ?? "");
                        foreach (var item in dimsArray) dimsList.Add(item?.ToString() ?? "");

                        var zip = namesList.Zip(
                            dimsList, (x, y) => new { name = x, dim = y })
                            .Where(x => x.name.IsNotNoE());

                        foreach (var item in zip)
                        {
                            try
                            {
                                int dim;
                                PipeSystemEnum system;
                                if (item.dim == "-") { dim = 25; system = PipeSystemEnum.Stål; }
                                else
                                {
                                    if (item.dim.StartsWith("DN"))
                                    {
                                        system = PipeSystemEnum.Stål;
                                        dim = Convert.ToInt32(item.dim.Remove(0, 2));
                                    }
                                    else if (item.dim.StartsWith("PF"))
                                    {
                                        system = PipeSystemEnum.PertFlextra;
                                        dim = Convert.ToInt32(item.dim.Remove(0, 2));
                                    }
                                    else if (item.dim.StartsWith("AT"))
                                    {
                                        system = PipeSystemEnum.AquaTherm11;
                                        if (item.dim == "AT32* SDR9") dim = 32;
                                        else dim = Convert.ToInt32(item.dim.Remove(0, 2));
                                    }
                                    else { system = PipeSystemEnum.Stål; dim = 25; }
                                }

                                dimList.Add(new DimEntry(item.name, dim, system));
                            }
                            catch (System.Exception)
                            {
                                prdDbg($"Item.name: {item.name}, Item.dim: {item.dim}");
                                throw;
                            }
                        }
                    }
                    #endregion

                    #region Find pipes that the buildings belong to
                    //Prepare area polylines
                    IGrouping<string, Polyline> plines = plLookup[areaName].First();
                    var strækningDict = plines.ToDictionary(x =>
                        fjvFremPsm.ReadPropertyString(x, fjvFremDef.Bemærkninger));

                    foreach (DimEntry item in dimList)
                    {
                        //Junction that takes care of non-building elements
                        //Skips sheet and wb references
                        //Populates strækninger items
                        if (!brDict.ContainsKey(item.Name))
                        {
                            //Find strækninger which are not buildings
                            if (item.Name.StartsWith("Strækning"))
                            {
                                item.Pipe = strækningDict[item.Name].Handle;
                                item.Strækning = item.Name;
                            }

                            continue;
                        }
                        var br = brDict[item.Name];

                        #region Use parent connection to traverse through stik line to corresponding path
                        //Get con line
                        string lineHandle = graphPsm.ReadPropertyString(br, graphDef.Parent);
                        if (lineHandle.IsNoE()) { prdDbg($"WARNING! Parent Handle for entry {item.Name} is NoE!"); continue; }
                        var conLine = localDb.Go<Line>(lineHandle);
                        //Get pipe
                        string pipeHandle = graphPsm.ReadPropertyString(conLine, graphDef.Parent);
                        if (pipeHandle.IsNoE()) { prdDbg($"WARNING! Parent Handle for conLine {conLine.Handle} is NoE!"); continue; }
                        //Store pipe handle in dim tuple
                        item.Pipe = new Handle(Convert.ToInt64(pipeHandle, 16));
                        #endregion
                    }
                    #endregion

                    #region Draw new polylines with dimensions
                    #region Built sizeArray for the pipeline
                    //Guard against references
                    Regex sheetNumberRegex = new Regex(@"^\d{1,3}");

                    var pipeGroups = dimList.GroupBy(x => x.Pipe);

                    //This foreach and next are split in two to make sorting by
                    //Strækning possible
                    foreach (var group in pipeGroups)
                    {
                        if (group.Key == default || group.Key == zeroHandle) continue;
                        Polyline originalPipe = group.Key.Go<Polyline>(localDb);
                        foreach (DimEntry dim in group)
                        {
                            try
                            {
                                if (sheetNumberRegex.IsMatch(dim.Name)) continue;
                                if (dim.Name.StartsWith("Strækning")) continue;
                                dim.Station = DistToStart(brDict[dim.Name], originalPipe);
                                dim.Strækning = fjvFremPsm.ReadPropertyString(originalPipe, fjvFremDef.Bemærkninger);
                            }
                            catch (System.Exception)
                            {
                                prdDbg(dim.Name);
                                prdDbg(originalPipe.Handle.ToString());
                                throw;
                            }
                        }
                    }

                    foreach (var group in pipeGroups.OrderByAlphaNumeric(x => x.First().Strækning))
                    {
                        if (group.Key == default || group.Key == zeroHandle) continue;
                        Polyline originalPipe = group.Key.Go<Polyline>(localDb);
                        List<SizeEntry> sizes = new List<SizeEntry>();

                        //OrderByAlphanumeric is trying to sort the sizes from largest to smallest
                        //But! this is from the time when there was only one system! Stål
                        //Now we have multiple systems and we need to take into account the system
                        //ASSUMPTION: Stål comes always before PertFlextra
                        //ALWAYS REVIEW THIS CODE IF INPUT DATA CHANGES

                        IEnumerable<IGrouping<string, DimEntry>> dims =
                            group.GroupBy(x => x.SystemDN)
                            .OrderBySpecial(x => x.Key)
                            .Reverse();
                        IGrouping<string, DimEntry>[] dimAr = dims.ToArray();
#if DEBUG
                        prdDbg(string.Join(", ", dimAr.Select(x => x.First().Dim)));
                        sysDns.UnionWith(dimAr.Select(x => x.Key));
#endif
                        for (int i = 0; i < dimAr.Length; i++)
                        {
                            int dn = dimAr[i].First().Dim;
                            double start = 0.0; double end = 0.0;

                            var system = dimAr[i].First().System;
                            PipeTypeEnum type;
                            if (system == PipeSystemEnum.Stål)
                            {
                                if (dn < 250) type = PipeTypeEnum.Twin;
                                else type = PipeTypeEnum.Frem;
                            }
                            else if (system == PipeSystemEnum.PertFlextra) type = PipeTypeEnum.Twin;
                            else if (system == PipeSystemEnum.AquaTherm11)
                            { 
                                if (dn < 160) type = PipeTypeEnum.Twin;
                                else type = PipeTypeEnum.Frem;
                            }
                            else type = PipeTypeEnum.Twin;

                            double kod = PipeScheduleV2.PipeScheduleV2.GetPipeKOd(system, dn, type, series);

                            //Determine start
                            if (i != 0) start = dimAr[i - 1].MaxBy(x => x.Station).Station;
                            //Determine end
                            if (i != dimAr.Length - 1) end = dimAr[i].MaxBy(x => x.Station).Station;
                            else end = originalPipe.Length;
                            sizes.Add(new SizeEntry(dn, start, end, kod, system, type, series));
                        }

                        #region Consolidate sizes -> remove 0 length sizes
                        //Consolidate sizes -> ie. remove sizes with 0 length
                        List<int> idxToRemove = new List<int>();
                        for (int i = 0; i < sizes.Count; i++)
                            if (sizes[i].EndStation - sizes[i].StartStation < 0.000001) idxToRemove.Add(i);
                        //Reverse to avoid mixing up indici and removing wrong ones
                        idxToRemove.Reverse();
                        foreach (int idx in idxToRemove) sizes.RemoveAt(idx);
                        #endregion

                        //Create sizeArray
                        PipelineSizeArray sizeArray = new PipelineSizeArray(sizes.ToArray());
#if DEBUG
                        prdDbg($"{fjvFremPsm.ReadPropertyString(originalPipe, fjvFremDef.Bemærkninger)}:");
                        prdDbg(sizeArray.ToString());
#endif
                        #endregion

                        #region Create pipes in sideloaded db
                        Polyline newPipe;
                        if (sizeArray.Length == 1)
                        {
                            newPipe = new Polyline(originalPipe.NumberOfVertices);
                            newPipe.SetDatabaseDefaults(dimDb);
                            newPipe.AddEntityToDbModelSpace(dimDb);
                            for (int i = 0; i < originalPipe.NumberOfVertices; i++)
                                newPipe.AddVertexAt(newPipe.NumberOfVertices, originalPipe.GetPoint2dAt(i), 0, 0, 0);

                            var entry = sizeArray[0];
                            string systemString = PipeScheduleV2.PipeScheduleV2.GetSystemString(entry.System);

                            //Determine layer
                            string layerName = string.Concat(
                                "FJV-", entry.Type, "-", systemString, entry.DN).ToUpper();

                            CheckOrCreateLayerForPipe(dimDb, layerName, entry.System, entry.Type);

                            newPipe.Layer = layerName;
                            newPipe.ConstantWidth = sizeArray[0].Kod / 1000.0;

                            string lineTypeText =
                                PipeScheduleV2.PipeScheduleV2.GetLineTypeLayerPrefix(entry.System) +
                                entry.DN.ToString();
                            string lineTypeName = "LT-" + lineTypeText;

                            if (!ltt.Has(lineTypeName))
                                PlanDetailing.LineTypes.LineTypes.createltmethod(
                                    lineTypeName, lineTypeText, "Standard", dimDb);

                            newPipe.LinetypeId = ltt[lineTypeName];
                            newPipe.Plinegen = true;

                            piplPsm.WritePropertyString(newPipe, piplDef.EtapeNavn, areaName);
                        }
                        else if (sizeArray.Length == 0) continue;
                        else
                        {
                            List<double> splitPts = new List<double>();
                            for (int i = 0; i < sizeArray.Length - 1; i++)
                                splitPts.Add(originalPipe.GetParameterAtDistance(sizeArray[i].EndStation));
                            if (splitPts.Count == 0) throw new System.Exception("Getting split points failed!");
                            splitPts.Sort();
                            try
                            {
                                DBObjectCollection objs = originalPipe
                                    .GetSplitCurves(new DoubleCollection(splitPts.ToArray()));

                                for (int i = 0; i < objs.Count; i++)
                                {
                                    SizeEntry entry = sizeArray[i];
                                    Polyline curChunk = objs[i] as Polyline;

                                    newPipe = new Polyline(curChunk.NumberOfVertices);
                                    newPipe.SetDatabaseDefaults(dimDb);
                                    newPipe.AddEntityToDbModelSpace(dimDb);
                                    for (int j = 0; j < curChunk.NumberOfVertices; j++)
                                        newPipe.AddVertexAt(newPipe.NumberOfVertices, curChunk.GetPoint2dAt(j), 0, 0, 0);

                                    string systemString = PipeScheduleV2.PipeScheduleV2.GetSystemString(entry.System);

                                    //Determine layer
                                    string layerName = string.Concat(
                                        "FJV-", entry.Type, "-", systemString, entry.DN).ToUpper();

                                    CheckOrCreateLayerForPipe(dimDb, layerName, entry.System, entry.Type);

                                    newPipe.Layer = layerName;
                                    newPipe.ConstantWidth = sizeArray[i].Kod / 1000.0;

                                    string lineTypeText = 
                                        PipeScheduleV2.PipeScheduleV2.GetLineTypeLayerPrefix(entry.System) +
                                        entry.DN.ToString();
                                    string lineTypeName = "LT-" + lineTypeText;

                                    if (!ltt.Has(lineTypeName))
                                        PlanDetailing.LineTypes.LineTypes.createltmethod(
                                            lineTypeName, lineTypeText, "Standard", dimDb);

                                    newPipe.LinetypeId = ltt[lineTypeName];
                                    newPipe.Plinegen = true;

                                    piplPsm.WritePropertyString(newPipe, piplDef.EtapeNavn, areaName);
                                }
                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                            {
                                Application.ShowAlertDialog(ex.Message + "\n" + ex.StackTrace);
                                throw new System.Exception("Splitting of pline failed!");
                            }
                        }
                        #endregion

                        #region Local method to create correct Pipe layer
                        //Avoid side effects with local methods!
                        //Use only passed arguments, do not operate on variables out of method's scope
                        void CheckOrCreateLayerForPipe(Database db, string layerName, PipeSystemEnum localSystem, PipeTypeEnum localType)
                        {
                            Transaction localTx = db.TransactionManager.TopTransaction;
                            LayerTable lt = db.LayerTableId.Go<LayerTable>(localTx);
                            Oid ltId;
                            if (!lt.Has(layerName))
                            {
                                LinetypeTable ltt = db.LinetypeTableId.Go<LinetypeTable>(localTx);

                                LayerTableRecord ltr = new LayerTableRecord();
                                ltr.Name = layerName;
                                short color = PipeScheduleV2.PipeScheduleV2.GetLayerColor(localSystem, localType);
                                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, color);
                                Oid continuous = ltt["Continuous"];
                                ltr.LinetypeObjectId = continuous;
                                ltr.LineWeight = LineWeight.ByLineWeightDefault;

                                //Make layertable writable
                                lt.CheckOrOpenForWrite();

                                //Add the new layer to layer table
                                ltId = lt.Add(ltr);
                                localTx.AddNewlyCreatedDBObject(ltr, true);
                            }
                            else ltId = lt[layerName];
                        }
                        #endregion

                        System.Windows.Forms.Application.DoEvents();
                    }

#if DEBUG
                    prdDbg(string.Join(", ", sysDns));
#endif
                    double DistToStart(BlockReference br, Polyline pline)
                    {
                        Point3d closestPt = pline.GetClosestPointTo(br.Position, false);
                        return pline.GetDistAtPoint(closestPt);
                    }
                    #endregion
                }
                #region Catch and finally
                catch (System.Exception ex)
                {
                    tx.Abort();
                    dimTx.Abort();
                    editor.WriteMessage("\n" + ex.ToString());
                    return;
                }
                finally
                {

                }
                tx.Commit();
                dimTx.Commit();
                #endregion
            }
        }

        private class DimEntry
        {
            public string Name { get; set; }
            public int Dim { get; set; }
            public PipeSystemEnum System { get; set; }
            public string SystemDN { get; set; }
            public Handle Pipe { get; set; } = new Handle(Convert.ToInt64("0", 16));
            public double Station { get; set; }
            public string Strækning { get; set; } = "";
            public DimEntry(string name, int dim, PipeSystemEnum system)
            {
                Name = name; Dim = dim; System = system;
                SystemDN = PipeScheduleV2.PipeScheduleV2.GetSystemString(system) + dim;
            }
        }

        public static bool IsPointInside(this Polyline pline, Point3d point)
        {
            double tolerance = Tolerance.Global.EqualPoint;
            using (MPolygon mpg = new MPolygon())
            {
                mpg.AppendLoopFromBoundary(pline, true, tolerance);
                return mpg.IsPointInsideMPolygon(point, tolerance).Count == 1;
            }
        }
    }
    internal class SheetPart
    {
        internal string Name { get; set; }
        internal List<string> Adresser { get; } = new List<string>();
        internal List<double> Distancer { get; } = new List<double>();

    }
    internal class Path
    {
        internal int PathNumber { get; set; } = 0;
        /// <summary>
        /// Order is important. Nodes must be ordered from max level to min level (descending).
        /// </summary>
        internal List<ExcelNode> NodesOnPath { get; set; }
        internal List<ExcelSheet> Sheets { get; } = new List<ExcelSheet>();
        internal void PopulateSheets()
        {
            string pRef = Dimensionering.PRef;
            Queue<SheetPart> parts = new Queue<SheetPart>();

            //Write data
            //Assume nodes in list sorted descending (max to min) by node level
            //foreach (ExcelNode node in NodesOnPath)
            for (int nodeIdx = 0; nodeIdx < NodesOnPath.Count; nodeIdx++)
            {
                ExcelNode node = NodesOnPath[nodeIdx];

                SheetPart part = new SheetPart();
                part.Name = node.Name;
                parts.Enqueue(part);

                List<BlockReference> sortedClients = null;
                if (node.ClientChildren.Count > 0)
                {
                    sortedClients = node.ClientChildren
                        .OrderByDescending(x => node.Self
                        .GetDistAtPoint(node.Self.GetClosestPointTo(x.Position, false))).ToList();
                }

                //First write children connections to nodes NOT on path
                //Leaf nodes have no children connections
                if (node.ConnectionChildren.Count() > 0)
                {
                    var foreignChildren = node.ConnectionChildren
                        .Where(x => x.PathId != this.PathNumber).ToList();

                    //For loop is to account for possibility of two or more children at a node
                    for (int i = 0; i < foreignChildren.Count; i++)
                    {
                        part.Adresser.Add($"{pRef}{foreignChildren[i].PathId}");

                        //On last iteration
                        if (i == foreignChildren.Count - 1)
                        {
                            //two cases: client children present or not
                            if (sortedClients == null)
                            {
                                //Code to gather consequent strækninger
                                //Collection to hold found strækninger
                                List<Polyline> strækninger = new List<Polyline>();

                                //Gather length of all Strækninger
                                int lookForwardIdx = nodeIdx;
                                while (true)
                                {
                                    lookForwardIdx++;
                                    if (lookForwardIdx == NodesOnPath.Count) { break; }

                                    ExcelNode nextNode = NodesOnPath[lookForwardIdx];
                                    if (nextNode.ClientChildren.Count == 0 && nextNode.ConnectionChildren.Count == 1)
                                        strækninger.Add(nextNode.Self);
                                    else break;
                                }

                                double dist = 0.0;
                                dist += node.Self.Length;
                                foreach (var item in strækninger) dist += item.Length;
                                part.Distancer.Add(dist);
                                //part.Distancer.Add(node.Self.Length);
                            }
                            else
                            {
                                part.Distancer.Add(
                                    node.Self.EndPoint.DistanceHorizontalTo(
                                        node.Self.GetClosestPointTo(
                                            sortedClients.First().Position, false)));
                            }
                        }
                        else part.Distancer.Add(0.0);
                    }
                }

                //Then write client nodes if any
                if (sortedClients != null)
                {
                    if (sortedClients.Count > 1)
                    {
                        for (int i = 0; i < sortedClients.Count - 1; i++)
                        {
                            BlockReference br = sortedClients[i];
                            string adresse = AddrWithDup(br);
                            part.Adresser.Add(adresse);

                            double currentDist = node.Self.GetDistAtPoint(
                                node.Self.GetClosestPointTo(br.Position, false));

                            double previousDist = node.Self.GetDistAtPoint(
                                node.Self.GetClosestPointTo(sortedClients[i + 1].Position, false));
                            part.Distancer.Add(currentDist - previousDist);

                            //Handle the last case
                            if (i == sortedClients.Count - 2)
                            {
                                br = sortedClients[i + 1];
                                adresse = AddrWithDup(br);
                                part.Adresser.Add(adresse);

                                currentDist = node.Self.GetDistAtPoint(
                                    node.Self.GetClosestPointTo(br.Position, false));

                                part.Distancer.Add(currentDist);
                            }
                        }
                    }
                    else
                    {
                        BlockReference br = sortedClients.First();
                        part.Adresser.Add(AddrWithDup(br));

                        double currentDist = node.Self.GetDistAtPoint(
                            node.Self.GetClosestPointTo(br.Position, false));

                        part.Distancer.Add(currentDist);
                    }
                }
                //If the node does not have any client children
                //Then write a reference to zeronode
                else
                {
                    part.Adresser.Add(node.Name);
                    part.Distancer.Add(0.0);
                }
            }

            string AddrWithDup(BlockReference br)
            {
                string adresse = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Adresse");
                int duplicateNr = PropertySetManager.ReadNonDefinedPropertySetInt(br, "BBR", "AdresseDuplikatNr");
                string duplicateNrString = duplicateNr == 0 ? "" : " " + duplicateNr.ToString();
                return adresse + duplicateNrString;
            }

            //Split sheets and write them
            int numberOfLines = 0;
            List<SheetPart> tempList = new List<SheetPart>();
            while (parts.Count > 0)
            {
                SheetPart part = parts.Dequeue();
                numberOfLines += part.Adresser.Count;
                tempList.Add(part);

                if (parts.Count != 0 && parts.Peek().Adresser.Count + numberOfLines < 50) continue;
                else if (parts.Count != 0)
                {
                    ExcelSheet excelSheet = new ExcelSheet();
                    excelSheet.SheetNumber = Dimensionering.GlobalSheetCount.GetNextNumber();
                    foreach (SheetPart sheetPart in tempList) excelSheet.Adresser.AddRange(sheetPart.Adresser);
                    foreach (SheetPart sheetPart in tempList) excelSheet.Længder.AddRange(sheetPart.Distancer);
                    excelSheet.SheetParts.AddRange(tempList);
                    this.Sheets.Add(excelSheet);
                    if (this.Sheets.Count > 1)
                    {
                        excelSheet.Adresser.Insert(0, (excelSheet.SheetNumber - 1).ToString());
                        excelSheet.Længder.Insert(0, 0.0);
                    }
                    tempList = new List<SheetPart>();
                    numberOfLines = 0;
                }
            }

            ExcelSheet lastSheet = new ExcelSheet();
            lastSheet.SheetNumber = Dimensionering.GlobalSheetCount.GetNextNumber();
            foreach (SheetPart sheetPart in tempList) lastSheet.Adresser.AddRange(sheetPart.Adresser);
            foreach (SheetPart sheetPart in tempList) lastSheet.Længder.AddRange(sheetPart.Distancer);
            lastSheet.SheetParts.AddRange(tempList);
            this.Sheets.Add(lastSheet);
            if (this.Sheets.Count > 1)
            {
                lastSheet.Adresser.Insert(0, (lastSheet.SheetNumber - 1).ToString());
                lastSheet.Længder.Insert(0, 0.0);
            }
        }
    }
    internal class ExcelSheet
    {
        internal int SheetNumber { get; set; } = 0;
        internal List<SheetPart> SheetParts { get; } = new List<SheetPart>();
        internal List<string> Adresser { get; set; } = new List<string>();
        internal List<double> Længder { get; set; } = new List<double>();
    }
    internal class ExcelNode
    {
        internal int GroupNumber { get; set; } = 0;
        internal int PartNumber { get; set; } = 0;
        internal string Name { get => $"Strækning {GroupNumber}.{PartNumber}"; }
        internal int NodeLevel { get; set; } = 0;
        internal int PathId { get; set; } = 0;
        internal ExcelNode Parent { get; set; }
        internal Polyline Self { get; set; }
        internal List<ExcelNode> ConnectionChildren { get; set; } = new List<ExcelNode>();
        internal HashSet<BlockReference> ClientChildren { get; set; }
            = new HashSet<BlockReference>();
        internal void SetGroupAndPartNumbers(string input)
        {
            input = input.Replace("Strækning ", "");
            GroupNumber = Convert.ToInt32(input.Split('.')[0]);
            PartNumber = Convert.ToInt32(input.Split('.')[1]);
        }
        public override bool Equals(object a)
        {
            if (ReferenceEquals(null, a)) return false;
            ExcelNode excelNode = a as ExcelNode;
            return excelNode.GroupNumber == this.GroupNumber && excelNode.PartNumber == this.PartNumber;
        }
        public override int GetHashCode()
        {
            return this.Name.GetHashCode() ^ this.GroupNumber.GetHashCode() ^ this.PartNumber.GetHashCode();
        }
    }
    internal class GlobalSheetCount
    {
        internal int CurrentSheetNumber { get; private set; } = 0;
        internal int GetNextNumber()
        {
            CurrentSheetNumber++;
            //TestValidity();
            return CurrentSheetNumber;
        }
        internal int SheetOffset { get; private set; } = 0;
        private void TestValidity()
        {
            if ((CurrentSheetNumber + SheetOffset) > 100)
                throw new System.Exception("Total number of sheets needed has exceeded 100!");
        }
    }
    internal class Node
    {
        internal int GroupNumber { get; set; }
        internal int PartNumber { get; set; }
        internal Node Parent { get; set; }
        internal Polyline Self { get; set; }
        internal HashSet<Node> ConnectionChildren { get; set; } = new HashSet<Node>();
        internal HashSet<BlockReference> ClientChildren { get; set; }
            = new HashSet<BlockReference>();
        internal void SetGroupAndPartNumbers(string input)
        {
            input = input.Replace("Strækning ", "");
            GroupNumber = Convert.ToInt32(input.Split('.')[0]);
            PartNumber = Convert.ToInt32(input.Split('.')[1]);
        }
    }
    /// <summary>
    /// Creates subgraphs from blockreferenes
    /// </summary>
    internal class Subgraph
    {
        private Database Database { get; }
        internal string StrækningsNummer { get; }
        internal Polyline Parent { get; }
        internal HashSet<Handle> Nodes { get; } = new HashSet<Handle>();
        internal Subgraph(Database database, Polyline parent, string strækningsNummer)
        {
            Database = database; Parent = parent; StrækningsNummer = strækningsNummer;
        }

        internal string WriteSubgraph(int subgraphIndex, bool subGraphsOn = true)
        {
            StringBuilder sb = new StringBuilder();
            if (subGraphsOn) sb.AppendLine($"subgraph cluster_{subgraphIndex} {{");
            foreach (Handle handle in Nodes)
            {
                //Gather information about element
                DBObject obj = handle.Go<DBObject>(Database);
                if (obj == null) continue;
                //Write the reference to the node
                sb.Append($"\"{handle}\" ");

                switch (obj)
                {
                    case Polyline pline:
                        //int dn = PipeSchedule.GetPipeDN(pline);
                        //string system = GetPipeSystem(pline);
                        //sb.AppendLine($"[label=\"{{{handle}|Rør L{pline.Length.ToString("0.##")}}}|{system}\\n{dn}\"];");
                        break;
                    case BlockReference br:
                        string vejnavn = PropertySetManager
                            .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                        string husnummer = PropertySetManager
                            .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                        Point3d np = Parent.GetClosestPointTo(br.Position, false);
                        double st = Parent.GetDistAtPoint(np);
                        sb.AppendLine(
                            $"[label=\"{{{handle}|{vejnavn} {husnummer}}}|{st.ToString("0.00")}\"];");
                        break;
                    default:
                        continue;
                }
            }
            //sb.AppendLine(string.Join(" ", Nodes) + ";");
            if (subGraphsOn)
            {
                sb.AppendLine($"label = \"{StrækningsNummer}\";");
                sb.AppendLine("color=red;");
                //if (isEntryPoint) sb.AppendLine("penwidth=2.5;");
                sb.AppendLine("}");
            }
            return sb.ToString();
        }
    }
    internal class Strækning
    {
        internal int GroupNumber { get; set; }
        internal int PartNumber { get; set; }
        internal Entity Parent { get; set; }
        internal Polyline Self { get; set; }
        internal HashSet<Polyline> ConnectionChildren { get; set; } = new HashSet<Polyline>();
        internal HashSet<BlockReference> ClientChildren { get; set; }
            = new HashSet<BlockReference>();
        internal List<string> Data { get; } = new List<string>();
        internal List<string> Distances { get; } = new List<string>();
        /// <summary>
        /// Do any renumbering BEFORE populating data
        /// </summary>
        internal void PopulateData()
        {
            //Write name
            Data.Add($"Strækning {GroupNumber}.{PartNumber}");
            Distances.Add("Distancer");
            //Write connections
            foreach (Polyline pline in ConnectionChildren)
            {
                string strStr = PropertySetManager.ReadNonDefinedPropertySetString(
                    pline, "FJV_fremtid", "Bemærkninger").Replace("Strækning ", "");
                Data.Add($"-> {strStr}");
                //Pad distances at the same time
                Distances.Add($"{Self.GetDistanceAtParameter(Self.EndParam).ToString("0.00")}");
            }

            //Write kundedata
            var sortedClients = ClientChildren
                .OrderByDescending(x => Self.GetDistAtPoint(
                    Self.GetClosestPointTo(x.Position, false))).ToList();

            for (int i = 0; i < sortedClients.Count - 1; i++)
            {
                BlockReference br = sortedClients[i];
                string vejnavn = PropertySetManager
                    .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                string husnummer = PropertySetManager
                    .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                Data.Add($"{vejnavn} {husnummer}");

                double currentDist = Self.GetDistAtPoint(
                    Self.GetClosestPointTo(br.Position, false));

                double previousDist = Self.GetDistAtPoint(
                    Self.GetClosestPointTo(sortedClients[i + 1].Position, false));
                Distances.Add($"{(currentDist - previousDist).ToString("0.00")}");

                //Handle the last case
                if (i == sortedClients.Count - 2)
                {
                    br = sortedClients[i + 1];
                    vejnavn = PropertySetManager
                    .ReadNonDefinedPropertySetString(br, "BBR", "Vejnavn");
                    husnummer = PropertySetManager
                        .ReadNonDefinedPropertySetString(br, "BBR", "Husnummer");
                    Data.Add($"{vejnavn} {husnummer}");

                    currentDist = Self.GetDistAtPoint(
                        Self.GetClosestPointTo(br.Position, false));

                    Distances.Add($"{(currentDist).ToString("0.00")}");
                }
            }

            //Write parent connection
            if (Parent != default)
            {
                string strStr2 = PropertySetManager.ReadNonDefinedPropertySetString(
                            Parent, "FJV_fremtid", "Bemærkninger").Replace("Strækning ", "");
                Data.Add($"{strStr2} ->");
                //Pad distances at the same time
                Distances.Add("0");
            }
        }
        internal void PadLists(int maxSize)
        {
            int missing = maxSize - Data.Count;
            if (missing > 0)
            {
                for (int i = 0; i < missing; i++)
                {
                    Data.Add("");
                    Distances.Add("");
                }
            }
        }
    }
}
