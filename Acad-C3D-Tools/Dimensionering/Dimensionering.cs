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
using System.Windows.Forms;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using MoreLinq;
using GroupByCluster;
using IntersectUtilities.UtilsCommon;
using Microsoft.Office.Interop.Excel;
using ChunkedEnumerator;
using Schema;
using BBRtoAddress;
using FolderSelect;

using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;
using static IntersectUtilities.UtilsCommon.UtilsODData;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using Newtonsoft.Json;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;

namespace IntersectUtilities.Dimensionering
{
    /// <summary>
    /// Class for intersection tools.
    /// </summary>
    public class DimensioneringExtension : IExtensionApplication
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
        }

        public void Terminate()
        {
        }
        #endregion
        [CommandMethod("DIMIMPORTBBRBLOCKS")]
        public void dimimportbbrblocks()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

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
            if (dialog.ShowDialog() == DialogResult.OK)
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
                    int j = 0;
                    foreach (ImportFraBBR.Feature feature in BBR.features)
                    {
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
                        bbrPsm.GetOrAttachPropertySet(bbrBlock);
                        foreach (PropertyInfo pinfo in properties)
                        {
                            if (dict.ContainsKey(pinfo.Name))
                            {
                                var value = TryGetValue(pinfo, feature.properties);
                                try
                                {
                                    bbrPsm.WritePropertyObject(dict[pinfo.Name] as PSetDefs.Property, value);
                                }
                                catch (System.Exception)
                                {
                                    prdDbg($"Property not found: {(dict[pinfo.Name] as PSetDefs.Property).Name}");
                                    throw;
                                }
                            }
                        }
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

            dimanalyzeduplicateaddr();

            prdDbg("Finished!");

        }

        //[CommandMethod("TESTRXCLASS")]
        public void testrxclass()
        {
            prdDbg(RXClass.GetClass(typeof(Polyline)).Name);
            prdDbg(RXClass.GetClass(typeof(MPolygon)).Name);
        }

        [CommandMethod("DIMINTERSECTAREAS")]
        public void dimintersectareas()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;

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
                                        bbrPsm.GetOrAttachPropertySet(br);
                                        bbrPsm.WritePropertyString(bbrDef.DistriktetsNavn, etapeName);
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
                                        bbrPsm.GetOrAttachPropertySet(br);
                                        bbrPsm.WritePropertyString(bbrDef.DistriktetsNavn, etapeName);
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
                                    omrPsm.GetOrAttachPropertySet(newPline);
                                    omrPsm.WritePropertyString(omrDef.Område, etapeName);
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

        [CommandMethod("DIMADRESSERDUMP")]
        public void dimadresserdump()
        {
            Dimensionering.dimadressedump();
        }

        [CommandMethod("DIMPOPULATEGRAPH")]
        public void dimpopulategraph()
        {
            Dimensionering.dimpopulategraph();
        }
        [CommandMethod("DIMCONNECTHUSNR")]
        public void dimconnecthusnr()
        {
            Dimensionering.dimconnecthusnr();
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

        [CommandMethod("DIMWRITEEXCEL")]
        public void dimwriteexcel()
        {
            Dimensionering.dimwriteexcel();
        }

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
                    if (dialog.ShowDialog() == DialogResult.OK)
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
                            graphPsm.GetOrAttachPropertySet(line);
                            string chStr = graphPsm.ReadPropertyString(graphDef.Children);
                            string firstChild = chStr.Split(';')[0];
                            if (firstChild.IsNoE()) throw new System.Exception(
                                $"Line {line.Handle} does not have a child specified!");
                            return localDb.Go<Entity>(firstChild);
                        }

                        //Get the original building block
                        graphPsm.GetOrAttachPropertySet(husNrLine);
                        string lineParentStr = graphPsm.ReadPropertyString(graphDef.Parent);
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
                        string childrenStr = graphPsm.ReadPropertyString(graphDef.Children);
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

        [CommandMethod("DIMWRITEALL")]
        public void dimwriteall()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;

            PropertySetManager.UpdatePropertySetDefinition(localDb, PSetDefs.DefinedSets.BBR);

            #region Choose one or all : NOT WORKING YET
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
            if (dialog.ShowDialog() == DialogResult.OK)
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

                    var groups = brs.GroupBy(x => bbrPsm.ReadPropertyString(x, bbrDef.DistriktetsNavn));
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

                        Dimensionering.dimadressedumptoexcel(wb, group, plLookup[group.Key], dimAfkøling);

                        Dimensionering.dimwriteallexcel(wb, group.Key, basePath);

                        #region Close workbook
                        oXL.Calculation = XlCalculation.xlCalculationAutomatic;
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

                }
                tx.Commit();
            }
        }

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
            PipeSchedule.PipeSeriesEnum pipeSeries =
                (PipeSchedule.PipeSeriesEnum)Enum.Parse(typeof(PipeSchedule.PipeSeriesEnum), series);
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
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        if (dialog.FileName.IsNoE()) return;
                        fileNames.Add(dialog.FileName);
                    }
                    else return;
                    break;
                case kwd2: //Alle
                    FolderSelectDialog fsd = new FolderSelectDialog()
                    {
                        Title = "Choose folder with excel workbooks to import:",
                        InitialDirectory = @"c:\"
                    };
                    if (fsd.ShowDialog(IntPtr.Zero))
                    {
                        string folder = fsd.FileName;
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
            string dimDbFilename = basePath + "Fjernvarme Fremtidig.dwg";
            #endregion

            using (Database dimDb = new Database(false, true))
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Prepare sideloaded database
                    dimDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\Templates\acadiso.dwt",
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
                        bbrPsm.GetOrAttachPropertySet(building);
                        string anvendelsesTekst = bbrPsm.ReadPropertyString(bbrDef.BygningsAnvendelseNyTekst);
                        string anvendelsesKode = bbrPsm.ReadPropertyString(bbrDef.BygningsAnvendelseNyKode);
                        anvendelsesKoder.Add((anvendelsesKode, anvendelsesTekst));
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
                        bbrPsm.GetOrAttachPropertySet(br);
                        string anvendelse = bbrPsm.ReadPropertyString(bbrDef.BygningsAnvendelseNyTekst);

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

        [CommandMethod("GASDATAANALYZE")]
        public void gasdataanalyze()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //string curEtapeName = Dimensionering.dimaskforarea();
            //if (curEtapeName.IsNoE()) return;

            string basePath = @"X:\022-1245 - Dimensionering af alle etaper - Dokumenter\01 Intern\04 Projektering\" +
                                      @"02 Forbrugsdata + sammenligning\Forbrugsdata\CSV\";

            string AreaName = "0240 Egedal";
            string BasePath = @"X:\AutoCAD DRI - QGIS\BBR UDTRÆK";
            string husnummerPath = $"{BasePath}\\{AreaName}\\DAR_husnummer.json";
            string baseDARHusnumre = File.ReadAllText(husnummerPath);
            List<DARHusnummer> husnumre = JsonConvert.DeserializeObject<List<DARHusnummer>>(baseDARHusnumre);

            string gasdataPath = basePath + "gasdata.json";
            string baseGasdata = File.ReadAllText(gasdataPath);
            var gasdatas = JsonConvert.DeserializeObject<List<GasData>>(baseGasdata)
                .Where(x => x.ConflictResolution != GasData.ConflictResolutionEnum.Ignore);

            System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(
                @"X:\AutoCAD DRI - QGIS\CSV TIL REST HENTER\AnvendelsesKoderPrivatErhverv.csv", "PrivatErhverv");

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    #region dump af anvendelseskoder
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        //.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        //    x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        //.Where(x => ReadStringParameterFromDataTable(
                        //    PropertySetManager.ReadNonDefinedPropertySetString(
                        //        x, "BBR", "BygningsAnvendelseNyKode"),
                        //    dt, "Type", 0) == "Erhverv")
                        .ToHashSet();

                    var groupedGasDatasByHusnr = gasdatas.GroupBy(x => x.HusnummerId);

                    var husNrJoin = groupedGasDatasByHusnr.Join(husnumre,
                        gd => gd.Key,
                        husNr => husNr.id_lokalId,
                        (a, b) => new
                        {
                            GasData = a,
                            Husnummer = b
                        });

                    //the husnrjoingroup can be retreived by adgangtilbygning key
                    var husNrJoinGroupedByAdgangTilBygning = husNrJoin.GroupBy(x => x.Husnummer.adgangTilBygning);

                    var manyToMany = new HashSet<(HashSet<GasData> GasData, HashSet<Handle> BR)>();

                    int count = 0;
                    foreach (var item in husNrJoinGroupedByAdgangTilBygning)
                    {
                        count++;
                        if (count % 200 == 0) { prdDbg($"Iteration: {count}"); System.Windows.Forms.Application.DoEvents(); }

                        HashSet<GasData> gasDatas = new HashSet<GasData>();
                        foreach (var group in item) gasDatas.UnionWith(group.GasData.ToHashSet());

                        HashSet<Handle> BRs = new HashSet<Handle>();
                        BlockReference direct = brs.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                            x, "BBR", "id_lokalId") == item.Key).FirstOrDefault();
                        if (direct != null) BRs.Add(direct.Handle);

                        //Now add references from the other grouped husnr
                        foreach (var group in item)
                        {
                            var additional = brs.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                                x, "BBR", "id_husnummerid") == group.Husnummer.id_lokalId);
                            foreach (var byg in additional) BRs.Add(byg.Handle);
                        }

                        manyToMany.Add((gasDatas, BRs));
                    }

                    prdDbg($"Count: {manyToMany.Count} after {count} loop(s).");

                    JsonSerializerSettings options = new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Formatting = Formatting.Indented
                    };
                    string manyToManyString = JsonConvert.SerializeObject(manyToMany, options);
                    Utils.OutputWriter(basePath + "manyToManyResult.json", manyToManyString, true);

                    #region To find the last missing joins
                    //var atLastJoinedList = missingBuildingJoin.SelectMany(x => x.gasdata);

                    //var findMissingJoin = noBuildings.GroupJoin(atLastJoinedList,
                    //    nob => nob.ggd.Key,
                    //    last => last.HusnummerId,
                    //    (a, b) => new
                    //    {
                    //        GasDataGroup = a,
                    //        LastJoinedGd = b.Select(x => x)
                    //    }); 
                    #endregion

                    #region Old code
                    //StringBuilder sb = new StringBuilder();

                    //prdDbg($"Test last join: {findMissingJoin.Count(x => x.LastJoinedGd.Count() == 0)}");
                    //foreach (var item in findMissingJoin.Where(x => x.LastJoinedGd.Count() == 0))
                    //{
                    //    prdDbg(item.GasDataGroup.ggd.Key);
                    //}

                    //Utils.OutputWriter(basePath + "missingJoin.txt", sb.ToString(), true);

                    //var nonJoinedGas = noBuildings.SelectMany(x => x.ggd);
                    //string nonJoinedGasDataPath = @"X:\022-1245 - Dimensionering af alle etaper - Dokumenter\01 Intern\04 Projektering\" +
                    //                     @"02 Forbrugsdata + sammenligning\Forbrugsdata\CSV\nonJoinedGasData.json";
                    //JsonSerializerSettings options = new JsonSerializerSettings()
                    //{
                    //    NullValueHandling = NullValueHandling.Ignore,
                    //    Formatting = Formatting.Indented
                    //};
                    //string gdString = JsonConvert.SerializeObject(nonJoinedGas.OrderBy(x => x.Adresse), options);
                    //Utils.OutputWriter(nonJoinedGasDataPath, gdString, true);

                    //Build file name
                    //string dbFilename = localDb.OriginalFileName;
                    //string path = System.IO.Path.GetDirectoryName(dbFilename);
                    //string dumpExportFileName = path + "\\anvendelsesstats.txt";

                    //Utils.ClrFile(dumpExportFileName);
                    //Utils.OutputWriter(dumpExportFileName, string.Join(Environment.NewLine, dict.Select(x => x.Key + ";" + x.Value.ToString()))); 
                    #endregion

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

        [CommandMethod("GASDATAUPDATE")]
        public void gasdataupdate()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

            //string curEtapeName = Dimensionering.dimaskforarea();
            //if (curEtapeName.IsNoE()) return;

            string basePath = @"X:\022-1245 - Dimensionering af alle etaper - Dokumenter\01 Intern\04 Projektering\" +
                                      @"02 Forbrugsdata + sammenligning\Forbrugsdata\CSV\";
            string baseResultdata = File.ReadAllText(basePath + "manyToManyResult.json");
            List<(List<GasData>, List<Handle>)> result =
                JsonConvert.DeserializeObject<List<(List<GasData>, List<Handle>)>>(baseResultdata);
            //List<ManyToManyResult> result = JsonConvert.DeserializeObject<List<ManyToManyResult>>(baseResultdata);0

            System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(
                @"X:\AutoCAD DRI - QGIS\CSV TIL REST HENTER\AnvendelsesKoderPrivatErhverv.csv", "PrivatErhverv");

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                    PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                    #region update af gasdata
                    HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    brs = brs
                        //.Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        //    x, "BBR", "Distriktets_navn") == curEtapeName)
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(
                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "Type")))
                        //.Where(x => ReadStringParameterFromDataTable(
                        //    PropertySetManager.ReadNonDefinedPropertySetString(
                        //        x, "BBR", "BygningsAnvendelseNyKode"),
                        //    dt, "Type", 0) == "Erhverv")
                        .ToHashSet();

                    double energiforbrugFørOpdatering = brs.Sum(
                        x => PropertySetManager.ReadNonDefinedPropertySetDouble(x, "BBR", "EstimeretVarmeForbrug"));

                    prdDbg($"Antal resultater: {result.Count}");
                    StringBuilder sb = new StringBuilder();
                    double eks = 0;
                    double frem = 0;
                    Dictionary<string, double> dictAreaCons = new Dictionary<string, double>();
                    foreach ((List<GasData> GasData, List<Handle> BRhandles) tuple in result)
                    {
                        var numUniques = 1;
                        var sameResoultion = tuple.GasData
                            .Select(x => x.ConflictResolution)
                            .Distinct()
                            .Count() == numUniques;

                        if (!sameResoultion)
                        {
                            prdDbg($"Different conflict resolution!\nSkipping iteration.\n{tuple.GasData.First()}");
                            continue;
                        }
                        else
                        {
                            GasData.ConflictResolutionEnum cr = tuple.GasData.First().ConflictResolution;
                            switch (cr)
                            {
                                case GasData.ConflictResolutionEnum.None:
                                case GasData.ConflictResolutionEnum.GasDataCombined:
                                    var BRs = GetBrs(localDb, tuple.BRhandles);
                                    var anvKoder = BRs.Select(x =>
                                        PropertySetManager.ReadNonDefinedPropertySetString(
                                            x, "BBR", "BygningsAnvendelseNyKode"));
                                    if (anvKoder.Distinct().Count() == 1)
                                    {
                                        if (ReadStringParameterFromDataTable(
                                                PropertySetManager.ReadNonDefinedPropertySetString(
                                                    BRs.First(), "BBR", "BygningsAnvendelseNyKode"),
                                                dt, "Type", 0) == "Erhverv")
                                        {
                                            double newConsumption = tuple.GasData.Sum(x => x.ForbrugOmregnet);
                                            double originalConsumption = BRs.Sum(x =>
                                                PropertySetManager.ReadNonDefinedPropertySetDouble(
                                                x, "BBR", "EstimeretVarmeForbrug"));

                                            int antalBygninger = BRs.Count;
                                            double perBygning = newConsumption / (double)antalBygninger;

                                            foreach (BlockReference br in BRs)
                                            {
                                                bbrPsm.GetOrAttachPropertySet(br);
                                                string area = bbrPsm.ReadPropertyString(bbrDef.DistriktetsNavn);

                                                if (!dictAreaCons.ContainsKey(area)) dictAreaCons.Add(area, 0.0);
                                                double temp = dictAreaCons[area];
                                                temp += perBygning;
                                                dictAreaCons[area] = temp;

                                                bbrPsm.WritePropertyObject(bbrDef.EstimeretVarmeForbrug, perBygning);
                                            }

                                            frem += newConsumption;
                                            eks += originalConsumption;
                                        }
                                    }
                                    else
                                    {
                                        //Filter out any non-erhvervsbygninger, if any left dump
                                        //all gas on these
                                        var erhverv = BRs.Where(x => ReadStringParameterFromDataTable(
                                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR", "BygningsAnvendelseNyKode"),
                                                dt, "Type", 0) == "Erhverv").ToList();
                                        if (erhverv.Count == 0) continue;
                                        double newConsumption = tuple.GasData.Sum(x => x.ForbrugOmregnet);
                                        double originalConsumption = erhverv.Sum(x =>
                                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                                                x, "BBR", "EstimeretVarmeForbrug"));

                                        int antalBygninger = erhverv.Count;
                                        double perBygning = newConsumption / (double)antalBygninger;

                                        foreach (BlockReference br in erhverv)
                                        {
                                            bbrPsm.GetOrAttachPropertySet(br);
                                            string area = bbrPsm.ReadPropertyString(bbrDef.DistriktetsNavn);

                                            if (!dictAreaCons.ContainsKey(area)) dictAreaCons.Add(area, 0.0);
                                            double temp = dictAreaCons[area];
                                            temp += perBygning;
                                            dictAreaCons[area] = temp;

                                            bbrPsm.WritePropertyObject(bbrDef.EstimeretVarmeForbrug, perBygning);
                                        }

                                        frem += newConsumption;
                                        eks += originalConsumption;
                                    }
                                    break;
                                case GasData.ConflictResolutionEnum.Manual:
                                    foreach (GasData gasData in tuple.GasData)
                                    {
                                        string targetBygId = gasData.BygId;
                                        BlockReference targetByg = brs.FirstOrDefault(
                                            x => PropertySetManager.ReadNonDefinedPropertySetString(
                                                x, "BBR", "id_lokalId") == targetBygId);
                                        double originalConsumption = PropertySetManager
                                            .ReadNonDefinedPropertySetDouble(
                                                targetByg, "BBR", "EstimeretVarmeForbrug");
                                        double newConsumption = gasData.ForbrugOmregnet;

                                        bbrPsm.GetOrAttachPropertySet(targetByg);
                                        string area = bbrPsm.ReadPropertyString(bbrDef.DistriktetsNavn);

                                        if (!dictAreaCons.ContainsKey(area)) dictAreaCons.Add(area, 0.0);
                                        double temp = dictAreaCons[area];
                                        temp += newConsumption;
                                        dictAreaCons[area] = temp;

                                        bbrPsm.WritePropertyObject(bbrDef.EstimeretVarmeForbrug, newConsumption);

                                        frem += newConsumption;
                                        eks += originalConsumption;
                                    }
                                    break;
                                case GasData.ConflictResolutionEnum.Ignore:
                                    break;
                                case GasData.ConflictResolutionEnum.Distribute:
                                    {
                                        var BRs2 = GetBrs(localDb, tuple.BRhandles);
                                        var erhverv2 = BRs2.Where(x => ReadStringParameterFromDataTable(
                                            PropertySetManager.ReadNonDefinedPropertySetString(x, "BBR",
                                                "BygningsAnvendelseNyKode"),
                                            dt, "Type", 0) == "Erhverv").ToList();
                                        if (erhverv2.Count == 0) continue;
                                        double newConsumption2 = tuple.GasData.Sum(x => x.ForbrugOmregnet);
                                        double originalConsumption2 = erhverv2.Sum(x =>
                                            PropertySetManager.ReadNonDefinedPropertySetDouble(
                                                x, "BBR", "EstimeretVarmeForbrug"));

                                        int antalBygninger = erhverv2.Count;
                                        double perBygning = newConsumption2 / (double)antalBygninger;

                                        foreach (BlockReference br in BRs2)
                                        {
                                            bbrPsm.GetOrAttachPropertySet(br);
                                            string area = bbrPsm.ReadPropertyString(bbrDef.DistriktetsNavn);

                                            if (!dictAreaCons.ContainsKey(area)) dictAreaCons.Add(area, 0.0);
                                            double temp = dictAreaCons[area];
                                            temp += perBygning;
                                            dictAreaCons[area] = temp;

                                            bbrPsm.WritePropertyObject(bbrDef.EstimeretVarmeForbrug, perBygning);
                                        }

                                        frem += newConsumption2;
                                        eks += originalConsumption2;
                                    }
                                    break;
                                default:
                                    throw new System.Exception("Unexpected ConflictResolutionEnum!");
                            }
                        }

                        List<BlockReference> GetBrs(Database database, List<Handle> handles)
                        {
                            List<BlockReference> newList = new List<BlockReference>();
                            foreach (var handle in handles)
                                newList.Add(handle.Go<BlockReference>(database));
                            return newList;
                        }
                    }

                    prdDbg($"Eks: {eks}, Fremtid: {frem}, Forskel: {frem - eks}");

                    double energiforbrugEfterOpdatering = brs.Sum(
                        x => PropertySetManager.ReadNonDefinedPropertySetDouble(x, "BBR", "EstimeretVarmeForbrug"));
                    prdDbg($"Total estimeret energiforbrug før opdatering: {energiforbrugFørOpdatering}");
                    prdDbg($"Total energiforbrug efter opdatering: {energiforbrugEfterOpdatering}");
                    prdDbg($"Total forskel mellem før og efter opdatering: {energiforbrugEfterOpdatering - energiforbrugFørOpdatering}");

                    sb.AppendLine("Område;Erhvervsforbrug");
                    foreach (var kvp in dictAreaCons.OrderBy(x => x.Key))
                    {
                        sb.AppendLine($"{kvp.Key};{kvp.Value}");
                    }

                    sb.AppendLine($"Alle;{dictAreaCons.Sum(x => x.Value)}");

                    Utils.OutputWriter(basePath + "UpdateErhverv.txt", sb.ToString(), true);

                    //JsonSerializerSettings options = new JsonSerializerSettings()
                    //{
                    //    NullValueHandling = NullValueHandling.Ignore,
                    //    Formatting = Formatting.Indented
                    //};
                    //string manyToManyString = JsonConvert.SerializeObject(manyToMany, options);
                    //Utils.OutputWriter(basePath + "manyToManyResult.json", manyToManyString, true);

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

        [CommandMethod("FINDSTRAY")]
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
                        .Where(x => Dimensionering.AcceptedBlockTypes.Contains(
                            bbrPsm.ReadPropertyString(x, bbrDef.Type)))
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
                    sb.AppendLine("Adresse;Energiforbrug;Antal ejendomme;Antal boliger med varmtvandsforbrug;Stik længde (tracé) [m];Dim. afkøling");

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
                        string handleString = graphPsm.ReadPropertyString(building, graphDef.Parent);
                        Handle parent;
                        try
                        {
                            parent = new Handle(Convert.ToInt64(handleString, 16));
                        }
                        catch (System.Exception)
                        {
                            prdDbg($"Reading parent handle failed for block: {building.Handle}");
                            throw;
                        }
                        Line line = parent.Go<Line>(localDb);

                        string stikLængde = line.GetHorizontalLength().ToString("0.##");
                        string adresse = bbrPsm.ReadPropertyString(building, bbrDef.Adresse);
                        int duplicateNr = bbrPsm.ReadPropertyInt(building, bbrDef.AdresseDuplikatNr);
                        string duplicateNrString = duplicateNr == 0 ? "" : " " + duplicateNr.ToString();
                        string estVarmeForbrug = (bbrPsm.ReadPropertyDouble(
                            building, bbrDef.EstimeretVarmeForbrug) * 1000.0).ToString("0.##");
                        string anvKodeTekst = bbrPsm.ReadPropertyString(building, bbrDef.BygningsAnvendelseNyTekst);

                        sb.AppendLine($"{adresse + duplicateNrString};{estVarmeForbrug};{antalEjendomme};{antalBoligerOsv};{stikLængde};{dimAfkøling};;{anvKodeTekst}");
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
            Editor editor = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;

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
                    int antalBoligerOsv = 1;

                    Worksheet ws1 = (Worksheet)wb.Worksheets["Forbrugeroversigt"];
                    Worksheet ws2 = (Worksheet)wb.Worksheets["Stikledninger"];
                    int forbrugerRow = 101;
                    int stikRow = 4;

                    //Columns:
                    //1: Adresse + number
                    //2: Energiforbrug
                    //3: Antal ejendomme
                    //4: Antal boliger med varmtvandsforbrug
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

                    foreach (BlockReference building in allBrs.OrderByAlphaNumeric(x => ds(x)))
                    {
                        string handleString = graphPsm.ReadPropertyString(building, graphDef.Parent);
                        Handle parent;
                        try
                        {
                            parent = new Handle(Convert.ToInt64(handleString, 16));
                        }
                        catch (System.Exception)
                        {
                            prdDbg($"Reading parent handle failed for block: {building.Handle}");
                            throw;
                        }
                        Line line = parent.Go<Line>(localDb);

                        double stikLængde = line.GetHorizontalLength();
                        string adresse = bbrPsm.ReadPropertyString(building, bbrDef.Adresse);
                        int duplicateNr = bbrPsm.ReadPropertyInt(building, bbrDef.AdresseDuplikatNr);
                        string duplicateNrString = duplicateNr == 0 ? "" : " " + duplicateNr.ToString();
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
                    editor.WriteMessage("\n" + ex.ToString());
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

            string curEtapeName = dimaskforarea();
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
                        graphPsm.GetOrAttachPropertySet(entryPline);
                        graphPsm.WritePropertyString(graphDef.Parent, $"Entry");

                        #region Traverse system using stack
                        //Using stack traversing strategy
                        Stack<Node> stack = new Stack<Node>();
                        Node startNode = new Node();
                        startNode.Self = entryPline;
                        stack.Push(startNode);
                        int subGroupCounter = 0;
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

                            graphPsm.GetOrAttachPropertySet(building);
                            string children = graphPsm.ReadPropertyString(graphDef.Children);
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
                            var nearest = res.MinBy(x => x.Dist).FirstOrDefault();
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

                            //Write parent data if it is not entry
                            graphPsm.GetOrAttachPropertySet(curPline);

                            if (graphPsm.ReadPropertyString(graphDef.Parent) != "Entry")
                                graphPsm.WritePropertyString(
                                    graphDef.Parent, nd.Parent.Self.Handle.ToString());

                            //Write strækning id to property
                            fjvFremPsm.GetOrAttachPropertySet(curPline);
                            fjvFremPsm.WritePropertyString(fjvFremDef.Bemærkninger,
                                $"Strækning {nd.GroupNumber}.{nd.PartNumber}");

                            //Write the children data
                            foreach (Node child in nd.ConnectionChildren)
                            {
                                string curChildrenString = graphPsm.ReadPropertyString(graphDef.Children);
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
                                graphPsm.GetOrAttachPropertySet(curPline);
                                string curChildrenString = graphPsm.ReadPropertyString(graphDef.Children);
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
                                pts.Add(nd.Self.GetPoint3dAt(i).To2D());
                            }
                        }

                        Polyline circum = PolylineFromConvexHull(pts);
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
                        fjvFremPsm.GetOrAttachPropertySet(entryElement);
                        string strNr = fjvFremPsm.ReadPropertyString(
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
                            fjvFremPsm.GetOrAttachPropertySet(curItem);
                            string strNrString = fjvFremPsm.ReadPropertyString(fjvFremDef.Bemærkninger);
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

                            graphPsm.GetOrAttachPropertySet(curItem);
                            string parentHandleString = graphPsm.ReadPropertyString(graphDef.Parent);
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
                                    fjvFremPsm.GetOrAttachPropertySet(pline);
                                    string childStrNr = fjvFremPsm.ReadPropertyString(fjvFremDef.Bemærkninger).Replace("Strækning ", "");
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

                        int maxSize = ordered.MaxBy(x => x.Data.Count).FirstOrDefault().Data.Count;
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
                        fjvFremPsm.GetOrAttachPropertySet(entryElement);
                        string strNr = fjvFremPsm.ReadPropertyString(
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
                            fjvFremPsm.GetOrAttachPropertySet(curItem);
                            string strNrString = fjvFremPsm.ReadPropertyString(fjvFremDef.Bemærkninger);
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
                        fjvFremPsm.GetOrAttachPropertySet(line);
                        if (fjvFremPsm.ReadPropertyString(fjvFremDef.Distriktets_navn) == $"{curEtapeName}{HusnrSuffix}")
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
                        if (dialog.ShowDialog() == DialogResult.OK)
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
                            graphPsm.GetOrAttachPropertySet(buildingBlock);
                            string children = graphPsm.ReadPropertyString(graphDef.Children);
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

                        var nearest = res.MinBy(x => x.Dist).FirstOrDefault();
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
                            psManGraph.GetOrAttachPropertySet(pline);
                            string currentChildren = psManGraph.ReadPropertyString(driDimGraphDef.Children);
                            currentChildren += connection.Handle.ToString() + ";";
                            psManGraph.WritePropertyString(driDimGraphDef.Children, currentChildren);
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
                    if (dialog.ShowDialog() == DialogResult.OK)
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
                            fjvFremPsm.GetOrAttachPropertySet(node.Self);
                            string strNrString = fjvFremPsm.ReadPropertyString(fjvFremDef.Bemærkninger);
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
                                .MaxBy(x => x.NodeLevel)
                                .FirstOrDefault();
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
                                "|", sheet.SheetParts.Select(x => x.Name).ToArray());

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
                                .MaxBy(x => x.NodeLevel)
                                .FirstOrDefault();
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
                                "|", sheet.SheetParts.Select(x => x.Name).ToArray());

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
            PipeSchedule.PipeSeriesEnum pipeSeries
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
                //Settings
                PropertySetManager fjvFremPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.FJV_fremtid);
                PSetDefs.FJV_fremtid fjvFremDef = new PSetDefs.FJV_fremtid();

                PropertySetManager graphPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.DriDimGraph);
                PSetDefs.DriDimGraph graphDef = new PSetDefs.DriDimGraph();

                PropertySetManager bbrPsm = new PropertySetManager(localDb, PSetDefs.DefinedSets.BBR);
                PSetDefs.BBR bbrDef = new PSetDefs.BBR();

                PropertySetManager piplPsm = new PropertySetManager(dimDb, PSetDefs.DefinedSets.DriPipelineData);
                PSetDefs.DriPipelineData piplDef = new PSetDefs.DriPipelineData();

                void wr(string inp) => editor.WriteMessage(inp);

                StringBuilder sb = new StringBuilder();

                //Collection to store pipes that were succesfully identified
                HashSet<Handle> seenHandles = new HashSet<Handle>();

                try
                {
                    #region Read excel workbook data into a list: dimList<(string Name, int Dim)>
                    Worksheet ws;
                    IEnumerable<int> sheetRange = Enumerable.Range(1, 100);
                    Handle zeroHandle = new Handle(Convert.ToInt64("0", 16));
                    var dimList = new List<DimEntry>();
                    foreach (int sheetNumber in sheetRange)
                    {
                        ws = wb.Sheets[sheetNumber.ToString()];
                        wr(" " + sheetNumber.ToString());
                        System.Windows.Forms.Application.DoEvents();

                        Array namesArray = (System.Array)ws.Range["E60:E109"].Cells.Value;
                        Array dimsArray = (System.Array)ws.Range["U4:U53"].Cells.Value;

                        var namesList = new List<string>();
                        var dimsList = new List<int>();

                        foreach (var item in namesArray)
                        {
                            if (item == null || item.ToString().IsNoE() || item.ToString() == "-") continue;
                            namesList.Add(item.ToString());
                        }

                        foreach (var item in dimsArray)
                        {
                            if (item == null || item.ToString().IsNoE() || item.ToString() == "-") continue;
                            dimsList.Add(Convert.ToInt32(item.ToString().Remove(0, 2)));
                        }

                        var zip = namesList.Zip(dimsList, (x, y) => new { name = x, dim = y });
                        foreach (var item in zip) dimList.Add(new DimEntry(item.name, item.dim));
                    }
                    #endregion

                    #region Find pipes that the buildings belong to
                    foreach (var item in dimList)
                    {
                        if (!brDict.ContainsKey(item.Name))
                        {
                            //prdDbg($"WARNING! Entry {item.Name} could not find corresponding BlockReference!");
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
                        //Add handle to seen handles list
                        seenHandles.Add(item.Pipe);
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
                        foreach (var dim in group)
                        {
                            try
                            {
                                if (sheetNumberRegex.IsMatch(dim.Name)) continue;
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
                        IOrderedEnumerable<IGrouping<int, DimEntry>> dims = group.GroupBy(x => x.Dim).OrderByDescending(x => x.Key);
                        IGrouping<int, DimEntry>[] dimAr = dims.ToArray();
                        for (int i = 0; i < dimAr.Length; i++)
                        {
                            int dn = dimAr[i].Key;
                            double start = 0.0; double end = 0.0;
                            double kod = dn < 250 ?
                                PipeSchedule.GetTwinPipeKOd(dn, pipeSeries) :
                                PipeSchedule.GetBondedPipeKOd(dn, pipeSeries);
                            //Determine start
                            if (i != 0) start = dimAr[i - 1].MaxBy(x => x.Station).First().Station;
                            //Determine end
                            if (i != dimAr.Length - 1) end = dimAr[i].MaxBy(x => x.Station).First().Station;
                            else end = originalPipe.Length;
                            sizes.Add(new SizeEntry(dn, start, end, kod));
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
                        //prdDbg($"{fjvFremPsm.ReadPropertyString(originalPipe, fjvFremDef.Bemærkninger)}:");
                        //prdDbg(sizeArray.ToString()); 
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

                            PipeSchedule.PipeTypeEnum pipeType =
                                sizeArray[0].DN < 250 ?
                                PipeSchedule.PipeTypeEnum.Twin :
                                PipeSchedule.PipeTypeEnum.Frem;

                            //Determine layer
                            string layerName = string.Concat(
                                "FJV-", pipeType.ToString(), "-", "DN" + sizeArray[0].DN.ToString()).ToUpper();

                            CheckOrCreateLayerForPipe(dimDb, layerName, pipeType);

                            newPipe.Layer = layerName;
                            newPipe.ConstantWidth = sizeArray[0].Kod / 1000.0;

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
                                    SizeEntry curSize = sizeArray[i];
                                    Polyline curChunk = objs[i] as Polyline;

                                    newPipe = new Polyline(curChunk.NumberOfVertices);
                                    newPipe.SetDatabaseDefaults(dimDb);
                                    newPipe.AddEntityToDbModelSpace(dimDb);
                                    for (int j = 0; j < curChunk.NumberOfVertices; j++)
                                        newPipe.AddVertexAt(newPipe.NumberOfVertices, curChunk.GetPoint2dAt(j), 0, 0, 0);

                                    PipeSchedule.PipeTypeEnum pipeType =
                                        sizeArray[i].DN < 250 ?
                                        PipeSchedule.PipeTypeEnum.Twin :
                                        PipeSchedule.PipeTypeEnum.Frem;

                                    //Determine layer
                                    string layerName = string.Concat(
                                        "FJV-", pipeType.ToString(), "-", "DN" + sizeArray[i].DN.ToString()).ToUpper();

                                    CheckOrCreateLayerForPipe(dimDb, layerName, pipeType);

                                    newPipe.Layer = layerName;
                                    newPipe.ConstantWidth = sizeArray[i].Kod / 1000.0;

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
                        void CheckOrCreateLayerForPipe(Database db, string layerName, PipeSchedule.PipeTypeEnum pipeType)
                        {
                            Transaction localTx = db.TransactionManager.TopTransaction;
                            LayerTable lt = db.LayerTableId.Go<LayerTable>(localTx);
                            Oid ltId;
                            if (!lt.Has(layerName))
                            {
                                LinetypeTable ltt = db.LinetypeTableId.Go<LinetypeTable>(localTx);

                                LayerTableRecord ltr = new LayerTableRecord();
                                ltr.Name = layerName;
                                switch (pipeType)
                                {
                                    case PipeSchedule.PipeTypeEnum.Twin:
                                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
                                        break;
                                    case PipeSchedule.PipeTypeEnum.Frem:
                                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                                        break;
                                    case PipeSchedule.PipeTypeEnum.Retur:
                                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 5);
                                        break;
                                    default:
                                        break;
                                }
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

                        #region Determine and find missing parts
                        //Network parts with no clients cannot be detected by reading the excel
                        //To find such missing parts, spatial analysis must be performed
                        //Try to locate polies at ends of successfully found plines
                        //And draw them also, if found



                        #endregion

                        System.Windows.Forms.Application.DoEvents();
                    }

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
            public Handle Pipe { get; set; } = new Handle(Convert.ToInt64("0", 16));
            public double Station { get; set; }
            public string Strækning { get; set; } = "";
            public DimEntry(string name, int dim) { Name = name; Dim = dim; }
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
            TestValidity();
            return CurrentSheetNumber;
        }
        internal int SheetOffset { get; private set; } = 0;
        private void TestValidity()
        {
            if ((CurrentSheetNumber + SheetOffset) > 100)
                throw new System.Exception("Total number of sheets needed has exceeded 58!");
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
