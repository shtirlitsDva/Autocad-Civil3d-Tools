using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;

using LERImporter.Schema;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

using static IntersectUtilities.UtilsCommon.Utils;

using Log = LERImporter.SimpleLogger;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace LERImporter
{
    internal class ConsolidatedCreator
    {
        public static void CreateLerData(Database? Db2d, Database? Db3d, FeatureCollection fc)
        {
            var lagLer = Csv.LagLer;

            HashSet<UtilityOwner> ownersRegister = new HashSet<UtilityOwner>();
            HashSet<LedningType> ledninger = new HashSet<LedningType>();
            HashSet<LedningstraceType> ledningstrace = new HashSet<LedningstraceType>();
            HashSet<LedningskomponentType> ledningskomponenter = new HashSet<LedningskomponentType>();
            HashSet<Graveforesp> graveforesps = new HashSet<Graveforesp>();

            #region Redirect objects to collections
            //Redirect objects to collections
            //int i = 0;
            foreach (FeatureMember fm in fc.featureCollection)
            {
                //i++; prdDbg($"Switching item {i}.");
                System.Windows.Forms.Application.DoEvents();
                switch (fm.item)
                {
                    case UtilityOwner uo:
                        ownersRegister.Add(uo);
                        break;
                    case Graveforesp gvfsp:
                        graveforesps.Add(gvfsp);
                        break;
                    case Ledningspakke lp:
                    case UtilityPackageInfo upi:
                        break;
                    case Kontaktprofil kp:
                        break;
                    case Informationsressource ir:
                        break;
                    case LedningType lt:
                        ledninger.Add(lt);
                        //prdDbg(lt.gmlid);
                        break;
                    case LedningstraceType ltr:
                        ledningstrace.Add(ltr);
                        //prdDbg(ltr.gmlid);
                        break;
                    case LedningskomponentType lk:
                        ledningskomponenter.Add(lk);
                        //prdDbg(lk.gmlid);
                        break;
                    default:
                        prdDbg(fm.item.GMLTypeID);
                        throw new System.Exception($"Unexpected type encountered {fm.item.GetType().Name}!");
                }
            }
            ownersRegister = ownersRegister.DistinctBy(x => x.ledningsejer).ToHashSet();
            #endregion

            #region Copy geometry from ledningstrace to indeholdtledning
            foreach (LedningstraceType ltr in ledningstrace)
            {
                if (ltr.indeholdtLedning == null) continue;

                foreach (var item in ltr.indeholdtLedning)
                {
                    string href = item.href;
                    if (href.StartsWith("#")) href = href.Substring(1);

                    var ledningerByHref = ledninger.Where(x => x.GmlId == href);

                    foreach (var ledn in ledningerByHref)
                    {
                        if (ltr.geometri.MultiCurve.curveMember.Length > 1)
                        {
                            prdDbg($"Ledningstrace {ltr.GmlId} with multiple curves!");
                            throw new System.Exception($"Ledningstrace {ltr.GmlId} with multiple curves!");
                        }

                        var absCurve = ltr.geometri.MultiCurve.curveMember[0].AbstractCurve;
                        if (absCurve is LineStringType ls)
                        {
                            ledn.geometri = new GeometryPropertyType();
                            ledn.geometri.Item = ls;
                        }
                        else throw new System.Exception($"For LedningstraceType {ltr.GmlId} encountered unhandled AbstractCurve type!\n" +
                            $"{absCurve.GetType()}");
                    }
                }
            }
            #endregion

            #region Transform Energinet Ledningstrace to Elledning
            // Energinet Eltransmission A/S uses Ledningstrace incorrectly to represent cables.
            // We transform these to ElledningType.
            const uint EnerginetLedningsejer = 39314878;
            var energinetTraces = ledningstrace
                .Where(x => x.ledningsejer == EnerginetLedningsejer)
                .ToList();

            foreach (var trace in energinetTraces)
            {
                // Extract geometry from the trace's MultiCurve
                if (trace.geometri?.MultiCurve?.curveMember == null ||
                    trace.geometri.MultiCurve.curveMember.Length == 0)
                {
                    Log.log($"Energinet Ledningstrace {trace.GmlId} has no geometry, skipping transformation.");
                    continue;
                }

                if (trace.geometri.MultiCurve.curveMember.Length > 1)
                {
                    Log.log($"WARNING: Energinet Ledningstrace {trace.GmlId} has multiple curves, using first one.");
                }

                var absCurve = trace.geometri.MultiCurve.curveMember[0].AbstractCurve;

                // Create new ElledningType from the trace
                var elledning = new ElledningType
                {
                    // Copy common properties
                    ledningsejer = trace.ledningsejer,
                    indberetningsNr = trace.indberetningsNr,
                    lerid = trace.lerid,
                    gmlid = trace.gmlid,
                    driftsstatus = trace.driftsstatus,
                    noejagtighedsklasse = trace.noejagtighedsklasse,
                    etableringstidspunkt = trace.etableringstidspunkt,
                    registreringFra = trace.registreringFra,
                    vejledendeDybde = trace.vejledendeDybde,
                    indtegningsmetode = trace.indtegningsmetode,
                    sikkerhedshensyn = trace.sikkerhedshensyn,                    

                    // Set Energinet-specific properties
                    spaendingsniveau = new MeasureType { Value = 136, uom = "kV" },
                    fareklasse = new FareklasseTypeType { Value = FareklasseType.megetfarlig },
                    type = "forsyningskabel",

                    // Copy geometry
                    geometri = new GeometryPropertyType { Item = absCurve }
                };

                // Copy company name if already populated
                elledning.LedningsEjersNavn = trace.LedningsEjersNavn;

                // Copy Bemærkning and LerNummer
                elledning.Bemærkning = trace.Bemærkning;
                elledning.LerNummer = trace.LerNummer;

                ledninger.Add(elledning);
                Log.log($"Transformed Energinet Ledningstrace {trace.GmlId} to Elledning.");
            }

            // Remove transformed traces from ledningstrace to avoid duplicate drawing
            foreach (var trace in energinetTraces)
            {
                ledningstrace.Remove(trace);
            }
            #endregion

            #region Draw graveforesp polygon
            string layerNameGFP = "GraveforespPolygon";

            if (Db2d != null)
            {
                Db2d.CheckOrCreateLayer(layerNameGFP);

                foreach (var graveforesp in graveforesps)
                {
                    PolygonType polygon = graveforesp.polygonProperty.Item as PolygonType;
                    LinearRingType lrt = polygon.exterior.Item as LinearRingType;
                    DirectPositionListType dplt = lrt.Items[0] as DirectPositionListType;

                    var points = dplt.Get2DPoints();

                    Point2dCollection points2d = new Point2dCollection();
                    DoubleCollection dc = new DoubleCollection();
                    for (int i = 0; i < points.Length; i++)
                    {
                        points2d.Add(points[i]);
                        dc.Add(0.0);
                    }

                    Hatch hatch = new Hatch();
                    hatch.Normal = new Vector3d(0.0, 0.0, 1.0);
                    hatch.Elevation = 0.0;
                    hatch.PatternScale = 1.0;
                    hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                    Oid hatchId = hatch.AddEntityToDbModelSpace(Db2d);

                    hatch.AppendLoop(HatchLoopTypes.Default, points2d, dc);
                    hatch.EvaluateHatch(true);

                    hatch.Layer = layerNameGFP;
                }
            }

            if (Db3d != null)
            {
                Db3d.CheckOrCreateLayer(layerNameGFP);

                foreach (var graveforesp in graveforesps)
                {
                    PolygonType polygon = graveforesp.polygonProperty.Item as PolygonType;
                    LinearRingType lrt = polygon.exterior.Item as LinearRingType;
                    DirectPositionListType dplt = lrt.Items[0] as DirectPositionListType;

                    var points = dplt.Get2DPoints();
                    Polyline polyline = new Polyline(points.Length - 1);
                    foreach (var point in points.Take(points.Length - 1))
                        polyline.AddVertexAt(polyline.NumberOfVertices, point, 0, 0, 0);
                    polyline.Closed = true;

                    MPolygon mpg = new MPolygon();
                    mpg.AppendLoopFromBoundary(polyline, true, Tolerance.Global.EqualPoint);
                    Oid mpId = mpg.AddEntityToDbModelSpace(Db3d);
                    mpg.Layer = layerNameGFP;
                }
            }
            #endregion

            #region Populate Company Name
            //Populate Company Name
            foreach (LedningType ledning in ledninger)
            {
                var owner = ownersRegister.FirstOrDefault(x => x.ledningsejer == ledning.ledningsejer);
                //if (owner == default) throw new System.Exception($"Ledning {ledning.id} kan ikke finde ejer!");
                if (owner == default) prdDbg($"Ledning {ledning.GmlId} kan ikke finde ejer!");
                else ledning.LedningsEjersNavn = owner.companyName;
            }
            foreach (LedningstraceType trace in ledningstrace)
            {
                var owner = ownersRegister.FirstOrDefault(x => x.ledningsejer == trace.ledningsejer);
                //if (owner == default) throw new System.Exception($"Ledning {trace.id} kan ikke finde ejer!");
                if (owner == default) prdDbg($"Ledning {trace.GmlId} kan ikke finde ejer!");
                else trace.LedningsEjersNavn = owner.companyName;
            }
            foreach (LedningskomponentType komp in ledningskomponenter)
            {
                var owner = ownersRegister.FirstOrDefault(x => x.ledningsejer == komp.ledningsejer);
                //if (owner == default) throw new System.Exception($"Ledning {komp.id} kan ikke finde ejer!");
                if (owner == default) prdDbg($"Ledning {komp.id} kan ikke finde ejer!");
                else komp.LedningsEjersNavn = owner.companyName;
            }
            //Report owners
            Log.log("Ledningsejere indeholdt i Ler 2.0 graveforespørgsel:");
            foreach (var item in ownersRegister)
            {
                Log.log(item.companyName);
            }
            #endregion

            #region Create property sets
            //Dictionary to translate between type name and psName
            Dictionary<string, string> psDict = new Dictionary<string, string>();

            //Create property sets
            {
                HashSet<Type> allUniqueTypes = ledninger.Select(x => x.GetType()).Distinct().ToHashSet();
                allUniqueTypes.UnionWith(ledningstrace.Select(x => x.GetType()).Distinct().ToHashSet());
                allUniqueTypes.UnionWith(ledningskomponenter.Select(x => x.GetType()).Distinct().ToHashSet());
                foreach (Type type in allUniqueTypes)
                {
                    string psName = type.Name.Replace("Type", "");
                    psDict.Add(type.Name, psName);

                    if (Db2d != null)
                    {
                        PropertySetDefinition propSetDef = CreatePropertySetDefinition(Db2d, type);
                        AddPropertySetDefinitionToDb(Db2d, propSetDef, psName);
                    }
                    if (Db3d != null)
                    {
                        PropertySetDefinition propSetDef = CreatePropertySetDefinition(Db3d, type);
                        AddPropertySetDefinitionToDb(Db3d, propSetDef, psName);
                    }
                }
            }            
            #endregion

            #region Create elements
            //List of all (new) layers of new entities
            HashSet<string> layerNames2d = new HashSet<string>();
            HashSet<string> layerNames3d = new HashSet<string>();

            foreach (LedningType ledning in ledninger)
            {
                string psName = psDict[ledning.GetType().Name];
                ILerLedning iLedning = ledning as ILerLedning;
                if (iLedning == null)
                    throw new System.Exception($"Ledning {ledning.GmlId}, {ledning.LerId} har ikke implementeret ILerLedning!");

                //Create 2D
                if (Db2d != null)
                {
                    try
                    {
                        ObjectId entityId = iLedning.DrawEntity2D(Db2d);
                        if (entityId.IsNull) continue;
                        Entity ent = entityId.Go<Entity>(Db2d.TransactionManager.TopTransaction, OpenMode.ForWrite);
                        layerNames2d.Add(ent.Layer);

                        if (ent is Polyline pl) pl.Plinegen = true;

                        //Attach the property set
                        PropertySetManager.AttachNonDefinedPropertySet(Db2d, ent, psName);

                        //Populate the property set
                        var psData = GmlToPropertySet.TranslateGmlToPs(ledning);
                        PropertySetManager.PopulateNonDefinedPropertySet(Db2d, ent, psName, psData);
                        PropertySetManager.WriteNonDefinedPropertySetString(
                            ent, psName, "GmlBemærkning", ledning.Bemærkning);
                        PropertySetManager.WriteNonDefinedPropertySetString(
                            ent, psName, "LerNummer", ledning.LerNummer);
                    }
                    catch (System.Exception)
                    {
                        prdDbg(ObjectDumper.Dump(ledning));
                        throw;
                    }
                }

                //Create 3D
                if (Db3d != null)
                {
                    ObjectId entityId = iLedning.DrawEntity3D(Db3d);
                    if (entityId.IsNull) continue;
                    Entity ent = entityId.Go<Entity>(Db3d.TransactionManager.TopTransaction, OpenMode.ForWrite);
                    layerNames3d.Add(ent.Layer);

                    //Attach the property set
                    PropertySetManager.AttachNonDefinedPropertySet(Db3d, ent, psName);
                    string gmlid = ledning.GmlId;
                    //Populate the property set
                    var psData = GmlToPropertySet.TranslateGmlToPs(ledning);
                    PropertySetManager.PopulateNonDefinedPropertySet(Db3d, ent, psName, psData);
                    PropertySetManager.WriteNonDefinedPropertySetString(
                            ent, psName, "GmlBemærkning", ledning.Bemærkning);
                    PropertySetManager.WriteNonDefinedPropertySetString(
                        ent, psName, "LerNummer", ledning.LerNummer);
                }
            }
            foreach (LedningstraceType trace in ledningstrace)
            {
                string psName = psDict[trace.GetType().Name];
                ILerLedning ledning = trace as ILerLedning;
                if (ledning == null)
                    throw new System.Exception($"Trace {trace.GmlId}, {trace.LerId} har ikke implementeret ILerLedning!");

                //Draw 2d
                if (Db2d != null)
                {
                    ObjectId entityId = ledning.DrawEntity2D(Db2d);
                    if (entityId.IsNull) continue;
                    Entity ent = entityId.Go<Entity>(Db2d.TransactionManager.TopTransaction, OpenMode.ForWrite);
                    layerNames2d.Add(ent.Layer);

                    if (ent is Polyline pl) pl.Plinegen = true;

                    //Attach the property set
                    PropertySetManager.AttachNonDefinedPropertySet(Db2d, ent, psName);

                    //Populate the property set
                    var psData = GmlToPropertySet.TranslateGmlToPs(trace);
                    PropertySetManager.PopulateNonDefinedPropertySet(Db2d, ent, psName, psData);
                    PropertySetManager.WriteNonDefinedPropertySetString(
                            ent, psName, "GmlBemærkning", trace.Bemærkning);
                    PropertySetManager.WriteNonDefinedPropertySetString(
                        ent, psName, "LerNummer", trace.LerNummer);
                }

                //Draw 3d
                if (Db3d != null)
                {
                    ObjectId entityId = ledning.DrawEntity3D(Db3d);
                    if (entityId.IsNull) continue;
                    Entity ent = entityId.Go<Entity>(Db3d.TransactionManager.TopTransaction, OpenMode.ForWrite);
                    layerNames3d.Add(ent.Layer);

                    //Attach the property set
                    PropertySetManager.AttachNonDefinedPropertySet(Db3d, ent, psName);

                    //Populate the property set
                    var psData = GmlToPropertySet.TranslateGmlToPs(trace);
                    PropertySetManager.PopulateNonDefinedPropertySet(Db3d, ent, psName, psData);
                    PropertySetManager.WriteNonDefinedPropertySetString(
                            ent, psName, "GmlBemærkning", trace.Bemærkning);
                    PropertySetManager.WriteNonDefinedPropertySetString(
                        ent, psName, "LerNummer", trace.LerNummer);
                }
            }
            //Create components in 2D
            if (Db2d != null)
            {
                foreach (LedningskomponentType komponent in ledningskomponenter)
                {
                    string psName = psDict[komponent.GetType().Name];
                    ILerKomponent creator = komponent as ILerKomponent;
                    if (creator == null)
                        throw new System.Exception($"Komponent {komponent.GmlId}, {komponent.LerId} har ikke implementeret ILerKomponent!");
                    Oid entityId;
                    try
                    {
                        entityId = creator.DrawComponent(Db2d);
                        if (entityId.IsNull) continue;
                    }
                    catch (System.Exception ex)
                    {
                        prdDbg("Component: " + komponent.gmlid + " threw an exception!");
                        throw;
                    }
                    Entity ent = entityId.Go<Entity>(Db2d.TransactionManager.TopTransaction, OpenMode.ForWrite);

                    //Attach the property set
                    PropertySetManager.AttachNonDefinedPropertySet(Db2d, ent, psName);

                    //Populate the property set
                    var psData = GmlToPropertySet.TranslateGmlToPs(komponent);
                    PropertySetManager.PopulateNonDefinedPropertySet(Db2d, ent, psName, psData);
                    PropertySetManager.WriteNonDefinedPropertySetString(
                            ent, psName, "GmlBemærkning", komponent.Bemærkning);
                    PropertySetManager.WriteNonDefinedPropertySetString(
                        ent, psName, "LerNummer", komponent.LerNummer);
                }
            }
            #endregion

            #region Read and assign layer's color
            //Set 2D colors
            if (Db2d != null)
            {
                //Cache layer table
                LayerTable ltable = Db2d.LayerTableId.Go<LayerTable>(Db2d.TransactionManager.TopTransaction);

                //Set up all LER layers
                foreach (string layerName in layerNames2d)
                {
                    string colorString = lagLer.Farve(layerName) ?? "";

                    Color color;
                    if (colorString.IsNoE())
                    {
                        Log.log($"Ledning with layer name {layerName} could not get a color!");
                        color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                    }
                    else
                    {
                        color = ParseColorString(colorString);
                        if (color == null)
                        {
                            Log.log($"Ledning layer name {layerName} could not parse colorString {colorString}!");
                            color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                        }
                    }

                    LayerTableRecord ltr = ltable[layerName]
                        .Go<LayerTableRecord>(Db2d.TransactionManager.TopTransaction, OpenMode.ForWrite);

                    ltr.Color = color;
                }
            }

            //Set 3D colors
            if (Db3d != null)
            {
                //Cache layer table
                LayerTable ltable = Db3d.LayerTableId.Go<LayerTable>(Db3d.TransactionManager.TopTransaction);

                //Set up all LER layers
                foreach (string layerName in layerNames3d)
                {
                    string tempLayerName = layerName;
                    if (layerName.EndsWith("-3D"))
                        tempLayerName =
                            tempLayerName.Substring(0, tempLayerName.Length - 3);
                    string colorString = lagLer.Farve(tempLayerName) ?? "";

                    Color color;
                    if (colorString.IsNoE())
                    {
                        Log.log($"Ledning with layer name {tempLayerName} could not get a color!");
                        color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                    }
                    else
                    {
                        color = ParseColorString(colorString);
                        if (color == null)
                        {
                            Log.log($"Ledning layer name {tempLayerName} could not parse colorString {colorString}!");
                            color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                        }
                    }

                    LayerTableRecord ltr = ltable[layerName]
                        .Go<LayerTableRecord>(Db3d.TransactionManager.TopTransaction, OpenMode.ForWrite);

                    ltr.Color = color;
                }
            }
            #endregion

            #region Read and assign layer's linetype
            if (Db2d != null)
            {
                LayerTable ltable = Db2d.LayerTableId.Go<LayerTable>(Db2d.TransactionManager.TopTransaction);
                LinetypeTable ltt = (LinetypeTable)Db2d.TransactionManager.TopTransaction
                    .GetObject(Db2d.LinetypeTableId, OpenMode.ForWrite);

                //Check if all line types are present
                HashSet<string> missingLineTypes = new HashSet<string>();
                foreach (string layerName in layerNames2d)
                {
                    string lineTypeName = lagLer.LineType(layerName) ?? "";
                    if (lineTypeName.IsNoE()) continue;
                    else if (!ltt.Has(lineTypeName)) missingLineTypes.Add(lineTypeName);
                }

                if (missingLineTypes.Count > 0)
                {
                    Database ltDb = new Database(false, true);
                    ltDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\Projection_styles.dwg",
                        FileOpenMode.OpenForReadAndAllShare, false, null);
                    Transaction ltTx = ltDb.TransactionManager.StartTransaction();

                    Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(Db2d);

                    LinetypeTable sourceLtt = (LinetypeTable)ltDb.TransactionManager.TopTransaction
                        .GetObject(ltDb.LinetypeTableId, OpenMode.ForRead);
                    ObjectIdCollection idsToClone = new ObjectIdCollection();

                    HashSet<string> errorneousLineTypes = new HashSet<string>();
                    foreach (string missingName in missingLineTypes)
                    {
                        if (!sourceLtt.Has(missingName))
                        {
                            Log.log($"ERROR! Could not find missing linetype {missingName} in Projection_styles.dwg!");
                            errorneousLineTypes.Add(missingName);
                            continue;
                        }
                        idsToClone.Add(sourceLtt[missingName]);
                    }

                    if (errorneousLineTypes.Count > 0)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("The following linetypes could not be found in Projection_styles.dwg:");
                        foreach (var item in errorneousLineTypes) sb.AppendLine(item);
                        throw new System.Exception(sb.ToString());
                    }

                    IdMapping mapping = new IdMapping();
                    ltDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                    ltTx.Commit();
                    ltTx.Dispose();
                    ltDb.Dispose();
                }

                Oid lineTypeId;
                foreach (string layerName in layerNames2d)
                {
                    string lineTypeName = lagLer.LineType(layerName) ?? "";
                    if (lineTypeName.IsNoE())
                    {
                        Log.log($"WARNING! Layer name {layerName} does not have a line type specified!.");
                        //If linetype string is NoE -> CONTINUOUS linetype must be used
                        lineTypeId = ltt["Continuous"];
                    }
                    else
                    {
                        //the presence of the linetype is assured in previous foreach.
                        lineTypeId = ltt[lineTypeName];
                    }
                    LayerTableRecord ltr = ltable[layerName]
                            .Go<LayerTableRecord>(Db2d.TransactionManager.TopTransaction, OpenMode.ForWrite);
                    ltr.LinetypeObjectId = lineTypeId;
                }
            }
            #endregion
        }

        private static PropertySetDefinition CreatePropertySetDefinition(Database db, Type type)
        {
            PropertySetDefinition propSetDef;

            propSetDef = new PropertySetDefinition();
            propSetDef.SetToStandard(db);
            propSetDef.SubSetDatabaseDefaults(db);
            propSetDef.Description = type.FullName;
            bool isStyle = false;
            var appliedTo = new StringCollection()
            {
                RXClass.GetClass(typeof(Polyline)).Name,
                RXClass.GetClass(typeof(Polyline3d)).Name,
                RXClass.GetClass(typeof(DBPoint)).Name,
                RXClass.GetClass(typeof(Hatch)).Name,
            };
            propSetDef.SetAppliesToFilter(appliedTo, isStyle);

            var properties = type.GetProperties();

            foreach (PropertyInfo prop in properties)
            {
                bool include = prop.CustomAttributes.Any(x => x.AttributeType == typeof(Schema.PsInclude));
                if (include)
                {
                    var propDefManual = new PropertyDefinition();
                    propDefManual.SetToStandard(db);
                    propDefManual.SubSetDatabaseDefaults(db);
                    propDefManual.Name = prop.Name;
                    propDefManual.Description = prop.Name;
                    switch (prop.PropertyType.Name)
                    {
                        case nameof(String):
                            propDefManual.DataType = Autodesk.Aec.PropertyData.DataType.Text;
                            propDefManual.DefaultData = "";
                            break;
                        case nameof(System.Boolean):
                            propDefManual.DataType = Autodesk.Aec.PropertyData.DataType.TrueFalse;
                            propDefManual.DefaultData = false;
                            break;
                        case nameof(Double):
                            propDefManual.DataType = Autodesk.Aec.PropertyData.DataType.Real;
                            propDefManual.DefaultData = 0.0;
                            break;
                        case nameof(Int32):
                            propDefManual.DataType = Autodesk.Aec.PropertyData.DataType.Integer;
                            propDefManual.DefaultData = 0;
                            break;
                        default:
                            propDefManual.DataType = Autodesk.Aec.PropertyData.DataType.Text;
                            propDefManual.DefaultData = "";
                            break;
                    }
                    propSetDef.Definitions.Add(propDefManual);
                }
            }

            //Add property for gml bemærkning
            var propDefGmlName = new PropertyDefinition();
            propDefGmlName.SetToStandard(db);
            propDefGmlName.SubSetDatabaseDefaults(db);
            propDefGmlName.Name = "GmlBemærkning";
            propDefGmlName.Description = "The bemærkning for graverforespørgsel.";
            propDefGmlName.DataType = Autodesk.Aec.PropertyData.DataType.Text;
            propDefGmlName.DefaultData = "";
            propSetDef.Definitions.Add(propDefGmlName);

            //Add property for ler nummer
            var propDefLerNummer = new PropertyDefinition();
            propDefLerNummer.SetToStandard(db);
            propDefLerNummer.SubSetDatabaseDefaults(db);
            propDefLerNummer.Name = "LerNummer";
            propDefLerNummer.Description = "Ler nummer.";
            propDefLerNummer.DataType = Autodesk.Aec.PropertyData.DataType.Text;
            propDefLerNummer.DefaultData = "";
            propSetDef.Definitions.Add(propDefLerNummer);

            return propSetDef;
        }
        private static void AddPropertySetDefinitionToDb(
            Database db, PropertySetDefinition propSetDef, string psName)
        {
            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                //check if prop set already exists
                DictionaryPropertySetDefinitions dictPropSetDef =
                    new DictionaryPropertySetDefinitions(db);
                if (dictPropSetDef.Has(psName, tx))
                {
                    tx.Abort();
                    return;
                }
                dictPropSetDef.AddNewRecord(psName, propSetDef);
                tx.AddNewlyCreatedDBObject(propSetDef, true);
                tx.Commit();
            }
        }

        internal static void TestLerData(FeatureCollection gf)
        {
            #region Redirect objects to collections
            HashSet<UtilityOwner> ownersRegister = new HashSet<UtilityOwner>();
            HashSet<LedningType> ledninger = new HashSet<LedningType>();
            HashSet<LedningstraceType> ledningstrace = new HashSet<LedningstraceType>();
            HashSet<LedningskomponentType> ledningskomponenter = new HashSet<LedningskomponentType>();

            foreach (FeatureMember fm in gf.featureCollection)
            {
                System.Windows.Forms.Application.DoEvents();
                switch (fm.item)
                {
                    case UtilityOwner uo:
                        ownersRegister.Add(uo);
                        break;
                    case Graveforesp gvfsp:
                        break;
                    case UtilityPackageInfo upi:
                        break;
                    case Kontaktprofil kp:
                        break;
                    case Informationsressource ir:
                        break;
                    case LedningType lt:
                        ledninger.Add(lt);
                        break;
                    case LedningstraceType ltr:
                        ledningstrace.Add(ltr);
                        break;
                    case LedningskomponentType lk:
                        ledningskomponenter.Add(lk);
                        break;
                    default:
                        prdDbg(fm.item.GMLTypeID);
                        throw new System.Exception($"Unexpected type encountered {fm.item.GetType().Name}!");
                }
            }
            #endregion

            #region Populate Company Name
            //Populate Company Name
            foreach (LedningType ledning in ledninger)
            {
                var owner = ownersRegister.FirstOrDefault(x => x.ledningsejer == ledning.ledningsejer);
                //if (owner == default) throw new System.Exception($"Ledning {ledning.id} kan ikke finde ejer!");
                if (owner == default) prdDbg($"Ledning {ledning.GmlId} kan ikke finde ejer!");
                else ledning.LedningsEjersNavn = owner.companyName;
            }
            foreach (LedningstraceType trace in ledningstrace)
            {
                var owner = ownersRegister.FirstOrDefault(x => x.ledningsejer == trace.ledningsejer);
                //if (owner == default) throw new System.Exception($"Ledning {trace.id} kan ikke finde ejer!");
                if (owner == default) prdDbg($"Ledning {trace.GmlId} kan ikke finde ejer!");
                else trace.LedningsEjersNavn = owner.companyName;
            }
            foreach (LedningskomponentType komp in ledningskomponenter)
            {
                var owner = ownersRegister.FirstOrDefault(x => x.ledningsejer == komp.ledningsejer);
                //if (owner == default) throw new System.Exception($"Ledning {komp.id} kan ikke finde ejer!");
                if (owner == default) prdDbg($"Ledning {komp.id} kan ikke finde ejer!");
                else komp.LedningsEjersNavn = owner.companyName;
            }
            #endregion

            #region Elevation data analysis
            //prdDbg("Analyzing LedningType:");
            //{
            //    var elevations = new HashSet<(double, string)>();
            //    foreach (var item in ledninger)
            //    {
            //        IPointParser parser = item.geometri.AbstractCurve as IPointParser;
            //        var points = parser.Get3DPoints();
            //        foreach (var point in points)
            //        {
            //            elevations.Add(
            //                (Math.Round(point.Z, 2, MidpointRounding.AwayFromZero),
            //                item.LedningsEjersNavn));
            //        }
            //    }

            //    prdDbg($"Number of distinct elevations: {elevations.Count}");
            //    foreach (var elev in elevations.OrderBy(x => x))
            //    {
            //        prdDbg(elev.ToString());
            //    }
            //}

            //prdDbg("\nAnalyzing LedningstraceType:");
            //{
            //    var elevations = new HashSet<(double, string)>();
            //    foreach (var item in ledningstrace)
            //    {
            //        IPointParser parser = item.geometri.MultiCurve as IPointParser;
            //        var points = parser.Get3DPoints();
            //        foreach (var point in points)
            //        {
            //            elevations.Add(
            //                (Math.Round(point.Z, 2, MidpointRounding.AwayFromZero),
            //                item.LedningsEjersNavn));
            //        }
            //    }

            //    prdDbg($"Number of distinct elevations: {elevations.Count}");
            //    foreach (var elev in elevations.OrderBy(x => x))
            //    {
            //        prdDbg(elev.ToString());
            //    }
            //}

            //prdDbg("\nAnalyzing LedningskomponentType:");
            //{
            //    var elevations = new HashSet<(double, string)>();
            //    foreach (var item in ledningskomponenter)
            //    {
            //        IPointParser parser = item.geometri.Item as IPointParser;
            //        var points = parser.Get3DPoints();
            //        foreach (var point in points)
            //        {
            //            elevations.Add(
            //                (Math.Round(point.Z, 2, MidpointRounding.AwayFromZero),
            //                item.LedningsEjersNavn));
            //        }
            //    }

            //    prdDbg($"Number of distinct elevations: {elevations.Count}");
            //    foreach (var elev in elevations.OrderBy(x => x))
            //    {
            //        prdDbg(elev.ToString());
            //    }
            //} 
            #endregion

            #region Id data retreival
            var query = ledninger.Where(x => x.GmlId == "62761113.13998.LEDNING.1724");
            var result = query.FirstOrDefault();
            if (result != default)
            {
                prdDbg(ObjectDumper.Dump(result));
            }
            #endregion
        }
    }
}
