#if BRICSCAD
using Teigha.Colors;
using Teigha.DatabaseServices;
using Color = Teigha.Colors.Color;
#else
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Color = Autodesk.AutoCAD.Colors.Color;
#endif

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;

using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.LER
{
    /// <summary>
    /// The bodies of FIXLERLAYERS and LISTINTLAYCHECKALL, shared by every head that
    /// registers them (IntersectUtilities, NorsynDrawingToolsManaged). No command attribute
    /// here: this file compiles into every project that imports IntersectUtilitiesCOMMON.
    /// </summary>
    public static class LerLayers
    {
        /// <summary>
        /// Checks if all layers of Polyline3d exist in the Krydsninger.csv file.
        /// </summary>
        public static void CheckAll(Database db)
        {
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Gather layer names

                    List<Line> lines = db.ListOfType<Line>(tx);
                    List<Spline> splines = db.ListOfType<Spline>(tx);
                    List<Polyline> plines = db.ListOfType<Polyline>(tx);
                    List<Polyline3d> plines3d = db.ListOfType<Polyline3d>(tx);
                    List<Arc> arcs = db.ListOfType<Arc>(tx);
                    prdDbg($"Nr. of lines: {lines.Count}");
                    prdDbg($"Nr. of splines: {splines.Count}");
                    prdDbg($"Nr. of plines: {plines.Count}");
                    prdDbg($"Nr. of plines3d: {plines3d.Count}");
                    prdDbg($"Nr. of arcs: {arcs.Count}");
                    HashSet<string> layNames = new HashSet<string>();
                    //Local function to avoid duplicate code
                    HashSet<string> LocalListNames<T>(HashSet<string> list, List<T> ents)
                    {
                        foreach (Entity ent in ents.Cast<Entity>())
                        {
                            list.Add(ent.Layer);
                        }
                        return list;
                    }
                    layNames = LocalListNames(layNames, lines);
                    layNames = LocalListNames(layNames, splines);
                    layNames = LocalListNames(layNames, plines);
                    layNames = LocalListNames(layNames, plines3d);
                    layNames = LocalListNames(layNames, arcs);
                    #endregion

                    var krydsninger = Csv.Krydsninger;

                    foreach (string name in layNames)
                    {
                        string? nameInFile = krydsninger.Navn(name);
                        if (nameInFile.IsNoE())
                        {
                            prdDbg($"Definition af ledningslag '{name}' mangler i Krydsninger.csv!");
                        }
                        else
                        {
                            string? typeInFile = krydsninger.Type(name);
                            if (typeInFile == "IGNORE")
                            {
                                prdDbg($"Advarsel: Ledningslag" +
                                        $" '{name}' er sat til 'IGNORE' og dermed ignoreres.");
                            }
                            else
                            {
                                string? layerInFile = krydsninger.Layer(name);
                                if (layerInFile.IsNoE())
                                    prdDbg($"Fejl: Definition af kolonne \"Layer\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");
                                if (typeInFile.IsNoE())
                                    prdDbg($"Fejl: Definition af kolonne \"Type\" for ledningslag" +
                                        $" '{name}' mangler i Krydsninger.csv!");
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex.Message);
                    return;
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// Assigns correct linetypes and colors to Ler polylines(3d).
        /// </summary>
        public static void Fix(Database localDb)
        {
            var krydsninger = Csv.Krydsninger;
            var lagLer = Csv.LagLer;

            // Helper to strip -2D and -3D suffixes from layer names for LagLer lookup
            static string StripSuffix(string? layerName)
            {
                if (string.IsNullOrEmpty(layerName)) return string.Empty;
                if (layerName.EndsWith("-2D")) return layerName[..^3];
                if (layerName.EndsWith("-3D")) return layerName[..^3];
                return layerName;
            }

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    LayerTable lt = localDb.LayerTableId.Go<LayerTable>(localDb.TransactionManager.TopTransaction);
                    //Cache layers in memory
                    HashSet<LayerTableRecord> layers = new HashSet<LayerTableRecord>();
                    foreach (ObjectId lid in lt) layers.Add(lid.Go<LayerTableRecord>(tx));
                    #region Prepare linetypes
                    LinetypeTable ltt = (LinetypeTable)localDb.TransactionManager.TopTransaction
                            .GetObject(localDb.LinetypeTableId, OpenMode.ForWrite);
                    //Lookup linetype specification and link to layer name
                    Dictionary<string, string> layerLineTypeMap = new Dictionary<string, string>();
                    foreach (var layer in layers)
                    {
                        string lookupKey = StripSuffix(layer.Name);
                        string? lineTypeName = lagLer.LineType(lookupKey);
                        if (lineTypeName.IsNoE()) prdDbg($"LineTypeName is missing for {layer.Name}!");
                        layerLineTypeMap.Add(layer.Name, lineTypeName ?? string.Empty);
                    }
                    //Check if all line types are present
                    HashSet<string> missingLineTypes = new HashSet<string>();
                    foreach (var layer in layers)
                    {
                        string lineTypeName = layerLineTypeMap[layer.Name];
                        if (lineTypeName.IsNoE()) continue;
                        if (!ltt.Has(lineTypeName)) missingLineTypes.Add(lineTypeName);
                    }
                    if (missingLineTypes.Count > 0)
                    {
                        Database ltDb = new Database(false, true);
                        ltDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\Projection_styles.dwg",
                            FileOpenMode.OpenForReadAndAllShare, false, null);
                        Transaction ltTx = ltDb.TransactionManager.StartTransaction();
                        ObjectId destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                        LinetypeTable sourceLtt = (LinetypeTable)ltDb.TransactionManager.TopTransaction
                            .GetObject(ltDb.LinetypeTableId, OpenMode.ForRead);
                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        foreach (string missingName in missingLineTypes)
                        {
                            if (sourceLtt.Has(missingName)) idsToClone.Add(sourceLtt[missingName]);
                            else prdDbg($"Missing linetype {missingName} not found in Projection_styles.dwg!");
                        }
                        IdMapping mapping = new IdMapping();
                        ltDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                        ltTx.Commit();
                        ltTx.Dispose();
                        ltDb.Dispose();
                    }
                    #endregion
                    foreach (LayerTableRecord ltr in layers)
                    {
                        string layerName = ltr.Name;
                        if (!krydsninger.HasNavn(layerName))
                        {
                            prdDbg($"UNKNOWN LAYER: {ltr.Name}");
                            continue;
                        }
                        string? type = krydsninger.Type(layerName);
                        if (type == "IGNORE") { prdDbg($"Layer {layerName} IGNORED!"); continue; }

                        string lookupKey = StripSuffix(layerName);
                        if (!lagLer.HasLayer(lookupKey))
                        {
                            prdDbg($"Ler layer {layerName} (lookup: {lookupKey}) not found in LagLer! Tilføj laget i LagLer.csv.");
                            continue;
                        }
                        string? farveString = lagLer.Farve(lookupKey);
                        Color color = ParseColorString(farveString);
                        if (color == null)
                        {
                            prdDbg($"Failed to read color for layer name {layerName} with colorstring {farveString}. Skipping!");
                            continue;
                        }
                        ltr.CheckOrOpenForWrite();
                        ltr.Color = color;
                        #region Read and assign layer's linetype
                        ObjectId lineTypeId;
                        string lineTypeName = layerLineTypeMap[layerName];
                        if (lineTypeName.IsNoE())
                        {
                            prdDbg($"WARNING! Layer name {layerName} does not have a line type specified!.");
                            //If linetype string is NoE -> CONTINUOUS linetype must be used
                            lineTypeId = ltt["Continuous"];
                        }
                        else
                        {
                            //the presence of the linetype is assured in previous section.
                            lineTypeId = ltt[lineTypeName];
                        }
                        ltr.LinetypeObjectId = lineTypeId;
                        #endregion
                    }
                    #region Set objects to byLayer
                    var list = localDb.ListOfType<Entity>(tx);
                    foreach (Entity item in list)
                    {
                        if (item is Polyline || item is Polyline3d)
                        {
                            item.CheckOrOpenForWrite();
                            item.Color = Color.FromColorIndex(ColorMethod.ByAci, 256);
                            item.Linetype = "ByLayer";
                        }
                    }
                    #endregion
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
    }
}
