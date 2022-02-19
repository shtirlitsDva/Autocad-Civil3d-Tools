using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
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
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Data;
using System.Reflection;
using System.Xml.Serialization;
//using MoreLinq;
//using GroupByCluster;
using IntersectUtilities;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

//using static IntersectUtilities.Enums;
//using static IntersectUtilities.HelperMethods;
//using static IntersectUtilities.Utils;
//using static IntersectUtilities.PipeSchedule;

using static IntersectUtilities.UtilsCommon.UtilsDataTables;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Log = LERImporter.SimpleLogger;

namespace LERImporter.Schema
{
    public partial class GraveforespoergselssvarType
    {
        private System.Data.DataTable DtKrydsninger;
        private System.Data.DataTable EjerRegister;
        public GraveforespoergselssvarType()
        {
            string ejerRegisterCsv = "X:\\AutoCAD DRI - 01 Civil 3D\\LedningsejerRegisterLer2.0.csv";
            EjerRegister = CsvReader.ReadCsvToDataTable(ejerRegisterCsv, "Krydsninger");

            string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
            DtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");

            if (DtKrydsninger == null) throw new System.Exception("Krydsninger could not be read!");
            if (EjerRegister == null) throw new System.Exception("Ejer register could not be read!");
        }
        public string Owner
        {
            get
            {
                string email = this.kontaktprofilTilTekniskeSpoergsmaal?.Kontaktprofil?.mailadresse;
                return ReadStringParameterFromDataTable(email, EjerRegister, "Ejer", 1);
            }
        }
        public Database WorkingDatabase { get; set; }
        public void CreateLerData()
        {
            if (this.ledningMember == null) this.ledningMember =
                    new GraveforespoergselssvarTypeLedningMember[0];
            if (this.ledningstraceMember == null) this.ledningstraceMember =
                    new GraveforespoergselssvarTypeLedningstraceMember[0];
            if (this.ledningskomponentMember == null) this.ledningskomponentMember =
                    new GraveforespoergselssvarTypeLedningskomponentMember[0];

            Log.log($"Number of ledningMember -> {this.ledningMember?.Length.ToString()}");
            Log.log($"Number of ledningstraceMember -> {this.ledningstraceMember?.Length.ToString()}");
            Log.log($"Number of ledningskomponentMember -> {this.ledningskomponentMember?.Length.ToString()}");

            #region Create property sets
            //Dictionary to translate between type name and psName
            Dictionary<string, string> psDict = new Dictionary<string, string>();

            //Create property sets
            HashSet<Type> allUniqueTypes = ledningMember.Select(x => x.Item.GetType()).Distinct().ToHashSet();
            allUniqueTypes.UnionWith(ledningskomponentMember.Select(x=> x.Item.GetType()).Distinct().ToHashSet());
            foreach (Type type in allUniqueTypes)
            {
                string psName = type.Name.Replace("Type", "");
                //Store the ps name in dictionary referenced by the type name
                //PS name is not goood! It becomes Elledning which is not unique
                //But it is unique!!
                //Data with different files will still follow the class definition in code
                //Which assures that all pssets are the same
                psDict.Add(type.Name, psName);

                PropertySetDefinition propSetDef = new PropertySetDefinition();
                propSetDef.SetToStandard(WorkingDatabase);
                propSetDef.SubSetDatabaseDefaults(WorkingDatabase);

                propSetDef.Description = type.FullName;
                bool isStyle = false;
                var appliedTo = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(Polyline3d)).Name
                };
                propSetDef.SetAppliesToFilter(appliedTo, isStyle);

                var properties = type.GetProperties();

                foreach (PropertyInfo prop in properties)
                {
                    bool include = prop.CustomAttributes.Any(x => x.AttributeType == typeof(Schema.PsInclude));
                    if (include)
                    {
                        var propDefManual = new PropertyDefinition();
                        propDefManual.SetToStandard(WorkingDatabase);
                        propDefManual.SubSetDatabaseDefaults(WorkingDatabase);
                        propDefManual.Name = prop.Name;
                        propDefManual.Description = prop.Name;
                        switch (prop.PropertyType.Name)
                        {
                            case nameof(String):
                                propDefManual.DataType = Autodesk.Aec.PropertyData.DataType.Text;
                                propDefManual.DefaultData = "";
                                break;
                            case nameof(Boolean):
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

                using (Transaction tx = WorkingDatabase.TransactionManager.StartTransaction())
                {
                    //check if prop set already exists
                    DictionaryPropertySetDefinitions dictPropSetDef = new DictionaryPropertySetDefinitions(WorkingDatabase);
                    if (dictPropSetDef.Has(psName, tx))
                    {
                        tx.Abort();
                        continue;
                    }
                    dictPropSetDef.AddNewRecord(psName, propSetDef);
                    tx.AddNewlyCreatedDBObject(propSetDef, true);
                    tx.Commit();
                }
            }
            #endregion

            //Debug list of all types in collections
            HashSet<string> names = new HashSet<string>();

            //List of all (new) layers of new entities
            HashSet<string> layerNames = new HashSet<string>();

            foreach (GraveforespoergselssvarTypeLedningMember member in ledningMember)
            {
                if (member.Item == null)
                {
                    Log.log($"ledningMember is null! Some enity has not been deserialized correct!");
                    continue;
                }

                string psName = psDict[member.Item.GetType().Name];
                ILerLedning ledning = member.Item as ILerLedning;
                Oid entityId = ledning.DrawEntity2D(WorkingDatabase);
                Entity ent = entityId.Go<Entity>(WorkingDatabase.TransactionManager.TopTransaction, OpenMode.ForWrite);
                layerNames.Add(ent.Layer);

                //Attach the property set
                PropertySetManager.AttachNonDefinedPropertySet(WorkingDatabase, ent, psName);

                //Populate the property set
                var psData = GmlToPropertySet.TranslateGmlToPs(member.Item);
                PropertySetManager.PopulateNonDefinedPropertySet(WorkingDatabase, ent, psName, psData);

                names.Add(member.Item.ToString());
            }

            foreach (GraveforespoergselssvarTypeLedningstraceMember item in ledningstraceMember)
            {
                if (item.Ledningstrace == null)
                {
                    Log.log($"ledningstraceMember is null! Some enity has not been deserialized correct!");
                    continue;
                }
                names.Add(item.Ledningstrace.ToString());
            }

            foreach (GraveforespoergselssvarTypeLedningskomponentMember member in ledningskomponentMember)
            {
                if (member.Item == null)
                {
                    Log.log($"ledningskomponentMember is null! Some enity has not been deserialized correct!");
                    continue;
                }

                string psName = psDict[member.Item.GetType().Name];
                ILerKomponent creator = member.Item as ILerKomponent;
                Oid entityId = creator.DrawComponent(WorkingDatabase);
                Entity ent = entityId.Go<Entity>(WorkingDatabase.TransactionManager.TopTransaction, OpenMode.ForWrite);
                //Layer names are not analyzed for components currently
                //layerNames.Add(ent.Layer);

                //Attach the property set
                PropertySetManager.AttachNonDefinedPropertySet(WorkingDatabase, ent, psName);

                //Populate the property set
                var psData = GmlToPropertySet.TranslateGmlToPs(member.Item);
                PropertySetManager.PopulateNonDefinedPropertySet(WorkingDatabase, ent, psName, psData);

                names.Add(member.Item.ToString());
            }

            #region Read and assign layer's color
            //Regex to parse the color information
            Regex colorRegex = new Regex(@"^(?<R>\d+)\*(?<G>\d+)\*(?<B>\d+)");

            //Cache layer table
            LayerTable lt = WorkingDatabase.LayerTableId.Go<LayerTable>(WorkingDatabase.TransactionManager.TopTransaction);

            //Set up all LER layers
            foreach (string layerName in layerNames)
            {
                string colorString = ReadStringParameterFromDataTable(layerName, DtKrydsninger, "Farve", 0);

                Color color;
                if (colorString.IsNoE())
                {
                    Log.log($"Ledning with layer name {layerName} could not get a color!");
                    color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                }
                else if (colorRegex.IsMatch(colorString))
                {
                    Match match = colorRegex.Match(colorString);
                    byte R = Convert.ToByte(int.Parse(match.Groups["R"].Value));
                    byte G = Convert.ToByte(int.Parse(match.Groups["G"].Value));
                    byte B = Convert.ToByte(int.Parse(match.Groups["B"].Value));
                    //prdDbg($"Set layer {name} to color: R: {R.ToString()}, G: {G.ToString()}, B: {B.ToString()}");
                    color = Color.FromRgb(R, G, B);
                }
                else
                {
                    Log.log($"Ledning layer name {layerName} could not parse colorString {colorString}!");
                    color = Color.FromColorIndex(ColorMethod.ByAci, 0);
                }

                LayerTableRecord ltr = lt[layerName]
                    .Go<LayerTableRecord>(WorkingDatabase.TransactionManager.TopTransaction, OpenMode.ForWrite);

                ltr.Color = color;
            }
            #endregion

            #region Read and assign layer's linetype
            LinetypeTable ltt = (LinetypeTable)WorkingDatabase.TransactionManager.TopTransaction
                .GetObject(WorkingDatabase.LinetypeTableId, OpenMode.ForWrite);

            //Check if all line types are present
            HashSet<string> missingLineTypes = new HashSet<string>();
            foreach (string layerName in layerNames)
            {
                string lineTypeName = ReadStringParameterFromDataTable(layerName, DtKrydsninger, "LineType", 0);
                if (lineTypeName.IsNoE()) continue;
                else if (!ltt.Has(lineTypeName)) missingLineTypes.Add(lineTypeName);
            }

            if (missingLineTypes.Count > 0)
            {
                Database ltDb = new Database(false, true);
                ltDb.ReadDwgFile("X:\\AutoCAD DRI - 01 Civil 3D\\Projection_styles.dwg",
                    FileOpenMode.OpenForReadAndAllShare, false, null);
                Transaction ltTx = ltDb.TransactionManager.StartTransaction();

                Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(WorkingDatabase);

                LinetypeTable sourceLtt = (LinetypeTable)ltDb.TransactionManager.TopTransaction
                    .GetObject(ltDb.LinetypeTableId, OpenMode.ForRead);
                ObjectIdCollection idsToClone = new ObjectIdCollection();

                foreach (string missingName in missingLineTypes) idsToClone.Add(sourceLtt[missingName]);

                IdMapping mapping = new IdMapping();
                ltDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                ltTx.Commit();
                ltTx.Dispose();
                ltDb.Dispose();
            }

            Oid lineTypeId;
            foreach (string layerName in layerNames)
            {
                string lineTypeName = ReadStringParameterFromDataTable(layerName, DtKrydsninger, "LineType", 0);
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
                LayerTableRecord ltr = lt[layerName]
                        .Go<LayerTableRecord>(WorkingDatabase.TransactionManager.TopTransaction, OpenMode.ForWrite);
                ltr.LinetypeObjectId = lineTypeId;
            }
            #endregion
        }
        public void TestPs()
        {
            if (this.ledningMember == null) this.ledningMember =
                    new GraveforespoergselssvarTypeLedningMember[0];
            if (this.ledningstraceMember == null) this.ledningstraceMember =
                    new GraveforespoergselssvarTypeLedningstraceMember[0];
            if (this.ledningskomponentMember == null) this.ledningskomponentMember =
                    new GraveforespoergselssvarTypeLedningskomponentMember[0];

            Log.log($"Number of ledningMember -> {this.ledningMember?.Length.ToString()}");
            Log.log($"Number of ledningstraceMember -> {this.ledningstraceMember?.Length.ToString()}");
            Log.log($"Number of ledningskomponentMember -> {this.ledningskomponentMember?.Length.ToString()}");





            foreach (GraveforespoergselssvarTypeLedningMember member in ledningMember)
            {
                if (member.Item == null)
                {
                    Log.log($"ledningMember is null! Some enity has not been deserialized correct!");
                    continue;
                }

                //ILerLedning ledning = member.Item as ILerLedning;
                //Oid entityId = ledning.DrawEntity2D(WorkingDatabase);

                //prdDbg(ObjectDumper.Dump(member.Item));

                //GmlToPropertySet gps = new GmlToPropertySet();
                //prdDbg(gps.TestTranslateGml(member.Item));
            }

            GmlToPropertySet gps = new GmlToPropertySet();
            prdDbg(gps.TestTranslateGml(ledningMember[56].Item));
        }

        #region Archive
        //public GraveforespoergselssvarTypeLedningMember[] getLedningMembers()
        //{
        //    if (this.ledningMember != null) 
        //        this.ledningMember?.Where(x => x != null && x.Item != null).ToArray();
        //    return new GraveforespoergselssvarTypeLedningMember[0];
        //}
        //public GraveforespoergselssvarTypeLedningstraceMember[] getLedningstraceMembers()
        //{
        //    if (this.ledningstraceMember != null) 
        //        return this.ledningstraceMember?.Where(x => x != null && x.Ledningstrace != null).ToArray();
        //    return new GraveforespoergselssvarTypeLedningstraceMember[0];
        //}
        //public GraveforespoergselssvarTypeLedningskomponentMember[] getLedningskomponentMembers()
        //{
        //    if (this.ledningskomponentMember != null)
        //        return this.ledningskomponentMember?.Where(x => x != null && x.Item != null).ToArray();
        //    return new GraveforespoergselssvarTypeLedningskomponentMember[0];
        //}
        #endregion
    }
}
