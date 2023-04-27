using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using MoreLinq;
using System.Data;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Aec.PropertyData.DatabaseServices;
using static IntersectUtilities.UtilsCommon.Utils;

using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using PsDataType = Autodesk.Aec.PropertyData.DataType;
using IntersectUtilities.UtilsCommon;
using System.Diagnostics;

namespace IntersectUtilities
{
    public class PropertySetManager
    {
        private Database Db { get; }
        private DictionaryPropertySetDefinitions DictionaryPropertySetDefinitions { get; set; }
        private PropertySetDefinition propertySetDefinition;
        private PropertySetDefinition PropertySetDefinition
        {
            get
            {
                if (propertySetDefinition == null)
                {
                    prdDbg("PropertySetDefinition is null! Have you remembered to GetOrAttachPropertySet?");
                    throw new System.Exception("PropertySetDefinition is null! Have you remembered to GetOrAttachPropertySet?");
                }
                return propertySetDefinition;
            }
            set => propertySetDefinition = value;
        }
        private PropertySet CurrentPropertySet { get; set; }
        public PropertySetManager(Database database, PSetDefs.DefinedSets propertySetName)
        {
            //1
            Db = database;
            //2.1
            DictionaryPropertySetDefinitions = new DictionaryPropertySetDefinitions(Db);
            //2.2
            if (Db.TransactionManager.TopTransaction == null)
            {
                prdDbg("PropertySetManager: Must be instantiated within a Transaction!");
                throw new System.Exception("PropertySetManager: Must be instantiated within a Transaction!");
            }
            //3
            PropertySetDefinition = GetOrCreatePropertySetDefinition(propertySetName);
        }
        private PropertySetDefinition GetOrCreatePropertySetDefinition(PSetDefs.DefinedSets propertySetName)
        {
            if (PropertySetDefinitionExists(propertySetName))
            {
                //List<PSetDefs.Property> missingProperties = CheckPropertySetDefinition(propertySetName);
                //if (missingProperties.Count == 0) return GetPropertySetDefinition(propertySetName);
                //else
                //{
                //    UpdatePropertySetDefinition(propertySetName, missingProperties);
                return GetPropertySetDefinition(propertySetName);
                //}
            }
            else return CreatePropertySetDefinition(propertySetName);
        }
        private PropertySetDefinition CreatePropertySetDefinition(PSetDefs.DefinedSets propertySetName)
        {
            string setName = propertySetName.ToString();
            prdDbg($"Defining PropertySet {propertySetName}.");

            //General properties
            PropertySetDefinition propSetDef = new PropertySetDefinition();
            propSetDef.SetToStandard(Db);
            propSetDef.SubSetDatabaseDefaults(Db);
            propSetDef.Description = setName;
            bool isStyle = false;

            PSetDefs pSetDefs = new PSetDefs();
            PSetDefs.PSetDef currentDef = pSetDefs.GetRequestedDef(propertySetName);

            propSetDef.SetAppliesToFilter(currentDef.GetAppliesTo(), isStyle);

            foreach (PSetDefs.Property property in currentDef.ListOfProperties())
            {
                var propDefManual = new PropertyDefinition();
                propDefManual.SetToStandard(Db);
                propDefManual.SubSetDatabaseDefaults(Db);

                propDefManual.Name = property.Name;
                propDefManual.Description = property.Description;
                propDefManual.DataType = property.DataType;
                propDefManual.DefaultData = property.DefaultValue;
                propSetDef.Definitions.Add(propDefManual);
            }

            using (Transaction defTx = Db.TransactionManager.StartTransaction())
            {
                DictionaryPropertySetDefinitions.AddNewRecord(setName, propSetDef);
                defTx.AddNewlyCreatedDBObject(propSetDef, true);
                defTx.Commit();
            }

            return propSetDef;
        }
        private bool PropertySetDefinitionExists(PSetDefs.DefinedSets propertySetName)
        {
            string setName = propertySetName.ToString();
            if (DictionaryPropertySetDefinitions.Has(setName, Db.TransactionManager.TopTransaction))
            {
                //prdDbg($"Property Set {setName} already defined.");
                return true;
            }
            else
            {
                prdDbg($"Property Set {setName} is not defined. Creating...");
                return false;
            }
        }
        private PropertySetDefinition GetPropertySetDefinition(PSetDefs.DefinedSets propertySetName, Transaction tx = null)
        {
            if (Db == null) throw new System.Exception("Database is null!");
            if (tx == null && Db.TransactionManager.TopTransaction == null)
                throw new System.Exception("GetPropertySetDefinition: Usage outside of transaction!");
            return DictionaryPropertySetDefinitions
                .GetAt(propertySetName.ToString())
                .Go<PropertySetDefinition>(tx ?? Db.TransactionManager.TopTransaction);
        }
        public void GetOrAttachPropertySet(Entity ent)
        {
            ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(ent);

            if (propertySetIds.Count == 0)
            {
                CurrentPropertySet = AttachPropertySet(ent);
            }
            else
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(Db.TransactionManager.TopTransaction);
                    if (ps.PropertySetDefinitionName == this.PropertySetDefinition.Name)
                    { this.CurrentPropertySet = ps; return; }
                }
                //Property set not attached
                CurrentPropertySet = AttachPropertySet(ent);
            }
        }
        private PropertySet AttachPropertySet(Entity ent)
        {
            try
            {
                ent.CheckOrOpenForWrite();
            }
            catch (System.Exception)
            {
                prdDbg(ent.Handle);
                throw;
            }
            PropertyDataServices.AddPropertySet(ent, PropertySetDefinition.Id);

            return PropertyDataServices.GetPropertySet(ent, this.PropertySetDefinition.Id)
                .Go<PropertySet>(Db.TransactionManager.TopTransaction);
        }
        /// <summary>
        /// OBSOLETE!!!
        /// </summary>
        [Obsolete("Use ReadPropertyString(Entity ent, PSetDefs.Property property)")]
        public string ReadPropertyString(PSetDefs.Property property)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return "";
            else return value.ToString();
        }
        public string ReadPropertyString(Entity ent, PSetDefs.Property property)
        {
            GetOrAttachPropertySet(ent);
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return "";
            else return value.ToString();
        }
        public double ReadPropertyDouble(Entity ent, PSetDefs.Property property)
        {
            GetOrAttachPropertySet(ent);
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return 0.0;
            else return Convert.ToDouble(value);
        }
        [Obsolete("Use ReadPropertyString(Entity ent, PSetDefs.Property property)")]
        public int ReadPropertyInt(PSetDefs.Property property)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return 0;
            else return (int)value;
        }
        public int ReadPropertyInt(Entity ent, PSetDefs.Property property)
        {
            GetOrAttachPropertySet(ent);
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return 0;
            else return (int)value;
        }
        [Obsolete("Use ReadPropertyString(Entity ent, PSetDefs.Property property)")]
        public void WritePropertyString(PSetDefs.Property property, string value)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            this.CurrentPropertySet.CheckOrOpenForWrite();
            this.CurrentPropertySet.SetAt(propertyId, value);
            this.CurrentPropertySet.DowngradeOpen();
        }
        public void WritePropertyString(Entity ent, PSetDefs.Property property, string value)
        {
            GetOrAttachPropertySet(ent);
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            this.CurrentPropertySet.CheckOrOpenForWrite();
            this.CurrentPropertySet.SetAt(propertyId, value);
            this.CurrentPropertySet.DowngradeOpen();
        }
        [Obsolete("Use ReadPropertyString(Entity ent, PSetDefs.Property property)")]
        public void WritePropertyObject(PSetDefs.Property property, object value)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            this.CurrentPropertySet.CheckOrOpenForWrite();
            this.CurrentPropertySet.SetAt(propertyId, value);
            this.CurrentPropertySet.DowngradeOpen();
        }
        public void WritePropertyObject(Entity ent, PSetDefs.Property property, object value)
        {
            GetOrAttachPropertySet(ent);
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            this.CurrentPropertySet.CheckOrOpenForWrite();
            this.CurrentPropertySet.SetAt(propertyId, value);
            this.CurrentPropertySet.DowngradeOpen();
        }
        public bool FilterPropetyString(Entity ent, PSetDefs.Property property, string value)
        {
            GetOrAttachPropertySet(ent);
            ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(ent);
            PropertySet set = default;

            if (propertySetIds.Count == 0)
            {
                set = AttachPropertySet(ent);
            }
            else
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(Db.TransactionManager.TopTransaction);
                    if (ps.PropertySetDefinitionName == this.PropertySetDefinition.Name)
                    { set = ps; }
                }
                //Property set not attached
                set = AttachPropertySet(ent);
            }

            int propertyId = set.PropertyNameToId(property.Name);
            object storedValue = set.GetAt(propertyId);
            return value == storedValue.ToString();
        }
        public static void CopyAllProperties(Entity source, Entity target)
        {
            //Only works within drawing
            //ToDo: implement copying from drawing to drawing
            try
            {
                List<PropertySet> sourcePss = source.GetPropertySets();
                DictionaryPropertySetDefinitions sourcePropDefDict
                    = new DictionaryPropertySetDefinitions(source.Database);
                DictionaryPropertySetDefinitions targetPropDefDict
                    = new DictionaryPropertySetDefinitions(target.Database);

                foreach (PropertySet sourcePs in sourcePss)
                {
                    PropertySetDefinition sourcePropSetDef =
                        sourcePs.PropertySetDefinition.Go<PropertySetDefinition>(source.GetTopTx());
                    //Check to see if table is already attached
                    if (!target.GetPropertySets().Contains(sourcePs, new PropertySetNameComparer()))
                    {
                        //If target entity does not have property set attached -> attach
                        //Here can creating the property set definition in the target database be implemented
                        target.CheckOrOpenForWrite();
                        PropertyDataServices.AddPropertySet(target, sourcePropSetDef.Id);
                    }

                    PropertySet targetPs = target.GetPropertySets()
                        .Find(x => x.PropertySetDefinitionName == sourcePs.PropertySetDefinitionName);

                    if (targetPs == null)
                    {
                        prdDbg("PropertySet attachment failed in PropertySetCopyFromEntToEnt!");
                        throw new System.Exception();
                    }

                    foreach (PropertyDefinition pd in sourcePropSetDef.Definitions)
                    {
                        int sourceId = sourcePs.PropertyNameToId(pd.Name);
                        object value = sourcePs.GetAt(sourceId);

                        int targetId = targetPs.PropertyNameToId(pd.Name);
                        targetPs.CheckOrOpenForWrite();
                        targetPs.SetAt(targetId, value);
                        targetPs.DowngradeOpen();
                    }
                }
            }
            catch (System.Exception ex)
            {
                prdDbg(ex.ToString());
                throw;
            }
        }
        /// <summary>
        /// Iterates all PSS attached to source and tries to get value of property
        /// </summary>
        public static bool TryReadProperty(Entity source, string propertyName, out object value)
        {
            try
            {
                List<PropertySet> sourcePss = source.GetPropertySets();
                DictionaryPropertySetDefinitions sourcePropDefDict
                    = new DictionaryPropertySetDefinitions(source.Database);
                
                foreach (PropertySet sourcePs in sourcePss)
                {
                    int propertyId = -1;

                    try
                    {
                        propertyId = sourcePs.PropertyNameToId(propertyName);
                        if (propertyId == -1) continue;
                        else
                        {
                            value = sourcePs.GetAt(propertyId);
                            return true;
                        }

                    }
                    catch (System.Exception)
                    {
                        continue;
                    }
                }

                value = null;
                return false;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                throw;
            }
        }
        public static object ReadNonDefinedPropertySetObject(Entity ent, string propertySetName, string propertyName)
        {
            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
            List<PropertySet> pss = new List<PropertySet>();

            using (OpenCloseTransaction tx = ent.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (Oid oid in psIds) pss.Add(oid.Go<PropertySet>(tx));

                foreach (PropertySet ps in pss)
                {
                    if (ps.PropertySetDefinitionName == propertySetName)
                    {
                        int id = ps.PropertyNameToId(propertyName);
                        object value = ps.GetAt(id);
                        tx.Commit();
                        return value;
                    }
                }

                tx.Commit();
            }
            //Fall through
            //If no PS found return null
            prdDbg($"WARNING: PS {propertySetName} not found!");
            return null;
        }
        private static object WriteNonDefinedPropertySetObject(Entity ent, string propertySetName, string propertyName, object value)
        {
            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
            List<PropertySet> pss = new List<PropertySet>();
            foreach (Oid oid in psIds) pss.Add(oid.Go<PropertySet>(ent.GetTopTx(), OpenMode.ForWrite));

            foreach (PropertySet ps in pss)
            {
                if (ps.PropertySetDefinitionName == propertySetName)
                {
                    try
                    {
                        int id = ps.PropertyNameToId(propertyName);
                        ps.SetAt(id, value);
                        return value;
                    }
                    catch (System.Exception)
                    {
                        return null;
                    }
                }
            }
            //Fall through
            //If no PS found return null
            return null;
        }
        public static double ReadNonDefinedPropertySetDouble(Entity ent, string propertySetName, string propertyName)
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            return Convert.ToDouble(value);
        }
        public static int ReadNonDefinedPropertySetInt(Entity ent, string propertySetName, string propertyName)
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            int number = 0;
            try
            {
                if (value == null) return number;
                if (value.ToString().IsNoE()) return number;
                number = Convert.ToInt32(value);
            }
            catch (System.Exception ex)
            {
                prdDbg(ent.Handle.ToString());
                prdDbg(propertySetName);
                prdDbg(propertyName);
                prdDbg(value.ToString());
                number = 0;
            }

            return number;
        }
        public static string ReadNonDefinedPropertySetString(Entity ent, string propertySetName, string propertyName)
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            return Convert.ToString(value);
        }
        public static void WriteNonDefinedPropertySetString(Entity ent, string propertySetName, string propertyName, string value)
        {
            WriteNonDefinedPropertySetObject(ent, propertySetName, propertyName, value);
        }
        public static void WriteNonDefinedPropertySetDouble(Entity ent, string propertySetName, string propertyName, double value)
        {
            WriteNonDefinedPropertySetObject(ent, propertySetName, propertyName, value);
        }
        public static void AttachNonDefinedPropertySet(Database database, Entity ent, string propertySetName)
        {
            var dictPropSetDefs = new DictionaryPropertySetDefinitions(database);
            Oid psId = dictPropSetDefs.GetAt(propertySetName);
            if (psId == Oid.Null) throw new System.Exception($"No property set named {propertySetName} was found!");
            //Add property set to the entity
            ent.CheckOrOpenForWrite();
            PropertyDataServices.AddPropertySet(ent, psId);
        }
        public static void PopulateNonDefinedPropertySet(
            Database database, Entity ent, string propertySetName, Dictionary<string, object> psData)
        {
            ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(ent);

            if (propertySetIds.Count == 0)
            {
                throw new System.Exception($"Property set {propertySetName} have not been attached correctly!");
            }
            else
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(database.TransactionManager.TopTransaction);
                    if (ps.PropertySetDefinitionName == propertySetName)
                    {
                        int i = 0;
                        ps.CheckOrOpenForWrite();
                        foreach (KeyValuePair<string, object> pair in psData)
                        {
                            i++;
                            int propertyId = ps.PropertyNameToId(pair.Key);
                            try
                            {
                                ps.SetAt(propertyId, pair.Value);
                            }
                            catch (System.Exception)
                            {
                                prdDbg($"{i} Data: {pair.Key}: {pair.Value}");
                                throw;
                            }
                        }
                        ps.DowngradeOpen();
                        return;
                    }
                }
                //Property set not attached
                throw new System.Exception($"Property set {propertySetName} could not been found attached to entity!");
            }
        }
        public static void UpdatePropertySetDefinition(Database db, PSetDefs.DefinedSets propertySetName)
        {
            if (db.TransactionManager.TopTransaction != null) throw new System.Exception(
                "UpdatePropertySetDefinition must not run inside another Transaction!");

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                var DictionaryPropertySetDefinitions = new DictionaryPropertySetDefinitions(db);

                if (!DictionaryPropertySetDefinitions.Has(propertySetName.ToString(), tx)) return;
                PropertySetDefinition propSetDef =
                    DictionaryPropertySetDefinitions.GetAt(propertySetName.ToString()).Go<PropertySetDefinition>(tx);

                List<PSetDefs.Property> missingProperties = new List<PSetDefs.Property>();
                PropertyDefinitionCollection col = propSetDef.Definitions;

                PSetDefs defs = new PSetDefs();
                var setDef = defs.GetRequestedDef(propertySetName);
                var propDict = setDef.ToPropertyDictionary();
                List<string> propDefNames = new List<string>();
                foreach (PropertyDefinition propDef in col) propDefNames.Add(propDef.Name);
                foreach (KeyValuePair<string, object> kvp in propDict)
                    if (kvp.Value is PSetDefs.Property prop)
                        if (!propDefNames.Contains(prop.Name))
                            missingProperties.Add(prop);

                if (missingProperties.Count == 0) return;

                foreach (PSetDefs.Property property in missingProperties)
                {
                    var propDefManual = new PropertyDefinition();
                    propDefManual.SetToStandard(db);
                    propDefManual.SubSetDatabaseDefaults(db);

                    propDefManual.Name = property.Name;
                    propDefManual.Description = property.Description;
                    propDefManual.DataType = property.DataType;
                    propDefManual.DefaultData = property.DefaultValue;

                    propSetDef.CheckOrOpenForWrite();
                    propSetDef.Definitions.Add(propDefManual);
                }
                tx.Commit();
            }
        }
        private static (string PsSetName, string PropertyName) AskForSetAndProperty(Database db)
        {
            if (db.TransactionManager.TopTransaction == null)
            {
                prdDbg("AskForSetAndProperty(Database db) has to run inside a transaction!");
                return default;
            }
            DictionaryPropertySetDefinitions dpsdict = new DictionaryPropertySetDefinitions(db);
            string psName = GetKeywords("Select property set: ", dpsdict.NamesInUse.ToList());
            if (psName == null) return default;
            PropertySetDefinition psDef = dpsdict.GetAt(psName).Go<PropertySetDefinition>(db.TransactionManager.TopTransaction);
            PropertyDefinitionCollection propDefs = psDef.Definitions;
            List<string> propDefNames = new List<string>();
            foreach (PropertyDefinition propDef in propDefs) propDefNames.Add(propDef.Name);
            string propName = GetKeywords("Select property name: ", propDefNames);
            if (propName == null) return default;

            return (psName, propName);
        }
        private static string AskForSetName(Database db)
        {
            if (db.TransactionManager.TopTransaction == null)
            {
                prdDbg("AskForSetName(Database db) has to run inside a transaction!");
                return default;
            }
            DictionaryPropertySetDefinitions dpsdict = new DictionaryPropertySetDefinitions(db);
            string psName = GetKeywords("Select property set: ", dpsdict.NamesInUse.ToList());
            if (psName == null) return default;
            return psName;
        }
        public static Oid[] SelectByPsValue(
            Database db, MatchTypeEnum matchType, string valueToFind = null)
        {
            IEnumerable<Entity> ents;
            Transaction tx = db.TransactionManager.TopTransaction;
            var sNp = AskForSetAndProperty(db);

            switch (matchType)
            {
                case MatchTypeEnum.Exact:
                    ents = db
                    .ListOfType<Entity>(tx, true)
                    .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        x, sNp.PsSetName, sNp.PropertyName) == valueToFind);
                    break;
                case MatchTypeEnum.Contains:
                    ents = db
                    .ListOfType<Entity>(tx, true)
                    .Where(x => PropertySetManager.ReadNonDefinedPropertySetString(
                        x, sNp.PsSetName, sNp.PropertyName)
                    .Contains(valueToFind, StringComparison.OrdinalIgnoreCase));
                    break;
                default:
                    throw new System.Exception($"MatchTypeEnum {matchType} undefined!");
            }
            return ents.Select(x => x.Id).ToArray();
        }
        public static void ListUniquePsData(Database db)
        {
            var names = AskForSetAndProperty(db);
            if (names == default)
            {
                prdDbg("Set and property names are null!");
                return;
            }
            Transaction tx = db.TransactionManager.TopTransaction;
            var dpsd = new DictionaryPropertySetDefinitions(db);
            Oid psDefId = dpsd.GetAt(names.PsSetName);

            var setIdsCol = PropertyDataServices.GetAllPropertySetsUsingDefinition(psDefId, false);
            
            HashSet<string> values = new HashSet<string>();
            foreach (Oid oid in setIdsCol)
            {
                PropertySet ps = oid.Go<PropertySet>(tx);
                int id = ps.PropertyNameToId(names.PropertyName);
                PropertySetData data = ps.GetPropertySetDataAt(id);
                values.Add(data.GetData()?.ToString());
            }

            if (values.Contains("")) { values.Remove(""); values.Add("<empty>"); };

            foreach (var item in values.OrderBy(x => x)) prdDbg(item);
            
        }
        public static HashSet<string> AllPropertyNames(Database db)
        {
            var name = AskForSetName(db);
            if (name == default)
            {
                prdDbg("Set name is null!");
                return null;
            }
            Transaction tx = db.TransactionManager.TopTransaction;
            var dpsd = new DictionaryPropertySetDefinitions(db);
            Oid psDefId = dpsd.GetAt(name);

            HashSet<string> values = new HashSet<string>();

            PropertySetDefinition psDef = psDefId.Go<PropertySetDefinition>(tx);
            PropertyDefinitionCollection defs = psDef.Definitions;

            for (int i = 0; i < defs.Count; i++)
            {
                PropertyDefinition def = defs[i];
                values.Add(def.Name);
            }

            return values;

        }
        public static HashSet<(string, string)> AllPropertyNamesAndDataType(Database db)
        {
            var name = AskForSetName(db);
            if (name == default)
            {
                prdDbg("Set name is null!");
                return null;
            }
            Transaction tx = db.TransactionManager.TopTransaction;
            var dpsd = new DictionaryPropertySetDefinitions(db);
            Oid psDefId = dpsd.GetAt(name);

            HashSet<(string, string)> values = new HashSet<(string, string)>();

            PropertySetDefinition psDef = psDefId.Go<PropertySetDefinition>(tx);
            PropertyDefinitionCollection defs = psDef.Definitions;

            for (int i = 0; i < defs.Count; i++)
            {
                PropertyDefinition def = defs[i];
                values.Add((def.Name, def.DataType.ToString()));
            }

            return values;

        }
        public static bool IsPropertySetAttached(Entity ent, string propertySetName)
        {
            var propertySetIds = PropertyDataServices.GetPropertySets(ent);
            if (propertySetIds.Count == 0) return false;

            bool foundPs = false;
            using (var tx = ent.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (Oid oid in propertySetIds)
                {

                    PropertySet ps = oid.Go<PropertySet>(
                        ent.Database.TransactionManager.TopTransaction);
                    if (ps.PropertySetDefinitionName == propertySetName)
                        foundPs = true;
                }
            }
            return foundPs;
        }
        public static bool IsPropertySetAttached(Entity ent, PSetDefs.DefinedSets propertySet)
        {
            return IsPropertySetAttached(ent, propertySet.ToString());
        }
        public static bool IsPropertySetAttached(Entity ent, string propertySetName, MatchTypeEnum matchType)
        {
            var propertySetIds = PropertyDataServices.GetPropertySets(ent);
            if (propertySetIds.Count == 0) return false;

            bool foundPs = false;
            using (var tx = ent.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(
                        ent.Database.TransactionManager.TopTransaction);
                    switch (matchType)
                    {
                        case MatchTypeEnum.Exact:
                            if (ps.PropertySetDefinitionName == propertySetName)
                                foundPs = true;
                            break;
                        case MatchTypeEnum.Contains:
                            if (ps.PropertySetDefinitionName.Contains(propertySetName))
                                foundPs = true;
                            break;
                        default:
                            break;
                    }
                    
                }
            }
            return foundPs;
        }
        public enum MatchTypeEnum
        {
            Exact,
            Contains
        }
    }

    public class PSetDefs
    {
        public enum DefinedSets
        {
            None,
            DriPipelineData,
            DriSourceReference,
            DriCrossingData,
            DriGasDimOgMat,
            DriOmråder,
            DriComponentsGisData,
            DriGraph,
            DriDimGraph,
            FJV_fremtid,
            FJV_område,
            BBR
        }
        public class FJV_område : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.FJV_område;
            public Property Område { get; } = new Property(
                "Område",
                "Navnet på det område polylinjen omgrænser",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name
                };
        }
        public class FJV_fremtid : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.FJV_fremtid;
            public Property Bemærkninger { get; } = new Property(
                "Bemærkninger",
                "Bemærkninger",
                PsDataType.Text,
                "");
            public Property Distriktets_navn { get; } = new Property(
                "Distriktets_navn",
                "Distriktets_navn.",
                PsDataType.Text,
                "");
            public Property Length { get; } = new Property(
                "Length",
                "Stores the length of the entity.",
                PsDataType.Real,
                0);
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(Line)).Name
                };
        }
        public class DriCrossingData : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriCrossingData;
            public Property Diameter { get; } = new Property(
                "Diameter",
                "Stores crossing pipe's diameter.",
                PsDataType.Integer,
                0);
            public Property Alignment { get; } = new Property(
                "Alignment",
                "Stores crossing alignment name.",
                PsDataType.Text,
                "");
            public Property SourceEntityHandle { get; } = new Property(
                "SourceEntityHandle",
                "Stores the handle of the crossing entity.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(CogoPoint)).Name
                };
        }
        public class DriSourceReference : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriSourceReference;
            public Property SourceEntityHandle { get; } = new Property(
                "SourceEntityHandle",
                "Handle of the source entity which provided information for this entity.",
                PsDataType.Text,
                "");
            public Property AlignmentStation { get; } = new Property(
                "AlignmentStation",
                "The station at which referenced object is situated.",
                PsDataType.Real,
                99999.9);
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class DriPipelineData : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriPipelineData;
            public Property BelongsToAlignment { get; } = new Property(
                "BelongsToAlignment",
                "Name of the alignment the component belongs to.",
                PsDataType.Text,
                "");
            public Property BranchesOffToAlignment { get; } = new Property(
                "BranchesOffToAlignment",
                "Name of the alignment the component branches off to.",
                PsDataType.Text,
                "");
            public Property EtapeNavn { get; } = new Property(
                "EtapeNavn",
                "Name of the area the pipe belongs to.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class DriGasDimOgMat : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriGasDimOgMat;
            public Property Dimension { get; } = new Property(
                "Dimension",
                "Dimension of the gas pipe.",
                PsDataType.Integer,
                0);
            public Property Material { get; } = new Property(
                "Material",
                "Material of the gas pipe.",
                PsDataType.Text,
                "");
            public Property Bemærk { get; } = new Property(
                "Bemærk",
                "Bemærkning til ledning.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(Polyline3d)).Name,
                    RXClass.GetClass(typeof(Line)).Name
                };
        }
        public class DriOmråder : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriOmråder;
            public Property Nummer { get; } = new Property(
                "Nummer",
                "Number of pipeline.",
                PsDataType.Text,
                "");
            public Property Vejnavn { get; } = new Property(
                "Vejnavn",
                "Name of street.",
                PsDataType.Text,
                "");
            //public Property Ejerskab { get; } = new Property(
            //    "Ejerskab",
            //    "Owner type of street.",
            //    PsDataType.Text,
            //    "");
            public Property Vejklasse { get; } = new Property(
                "Vejklasse",
                "Street/road class.",
                PsDataType.Text,
                "");
            public Property Belægning { get; } = new Property(
                "Belægning",
                "Pavement type.",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                };
        }
        public class DriComponentsGisData : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriComponentsGisData;
            public Property BlockName { get; } = new Property("BlockName", "Name of source block", PsDataType.Text, "");
            public Property DN1 { get; } = new Property("DN1", "Main run dimension", PsDataType.Integer, 0);
            public Property DN2 { get; } = new Property("DN2", "Secondary run dimension", PsDataType.Integer, 0);
            public Property Flip { get; } = new Property("Flip", "Describes block's mirror state", PsDataType.Text, "");
            public Property Height { get; } = new Property("Height", "Height of symbol", PsDataType.Real, 0);
            public Property OffsetX { get; } = new Property("OffsetX", "X offset from Origo to CL", PsDataType.Real, 0);
            public Property OffsetY { get; } = new Property("OffsetY", "Y offset from Origo to CL", PsDataType.Real, 0);
            public Property Rotation { get; } = new Property("Rotation", "Rotation of the symbol", PsDataType.Real, 0);
            public Property Serie { get; } = new Property("Serie", "Insulation series of pipes", PsDataType.Text, "");
            public Property System { get; } = new Property("System", "Twin or single", PsDataType.Text, "");
            public Property Type { get; } = new Property("Type", "Type of the component", PsDataType.Text, "");
            public Property Width { get; } = new Property("Width", "Width of symbol", PsDataType.Real, 0);
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class DriGraph : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriGraph;
            public Property ConnectedEntities { get; } = new Property(
                "ConnectedEntities",
                "Lists connected entities",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class DriDimGraph : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.DriDimGraph;
            public Property Parent { get; } = new Property(
                "Parent",
                "Lists parent entity",
                PsDataType.Text,
                "");
            public Property Children { get; } = new Property(
                "Children",
                "Lists children entities",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(Line)).Name,
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class BBR : PSetDef
        {
            public DefinedSets SetName { get; } = DefinedSets.BBR;
            public Property id_lokalId { get; } = new Property(
                "id_lokalId",
                "id_lokalId",
                PsDataType.Text,
                "");
            public Property id_husnummerid { get; } = new Property(
                "id_husnummerid",
                "id_husnummerid",
                PsDataType.Text,
                "");
            public Property Name { get; } = new Property(
                "Name",
                "Name",
                PsDataType.Text,
                "");
            public Property Bygningsnummer { get; } = new Property(
                "Bygningsnummer",
                "Bygningsnummer",
                PsDataType.Integer,
                0);
            public Property BygningsAnvendelseNyTekst { get; } = new Property(
                "BygningsAnvendelseNyTekst",
                "BygningsAnvendelseNyTekst",
                PsDataType.Text,
                "");
            public Property BygningsAnvendelseNyKode { get; } = new Property(
                "BygningsAnvendelseNyKode",
                "BygningsAnvendelseNyKode",
                PsDataType.Text,
                "");
            public Property BygningsAnvendelseGlTekst { get; } = new Property(
                "BygningsAnvendelseGlTekst",
                "BygningsAnvendelseGlTekst",
                PsDataType.Text,
                "");
            public Property BygningsAnvendelseGlKode { get; } = new Property(
                "BygningsAnvendelseGlKode",
                "BygningsAnvendelseGlKode",
                PsDataType.Text,
                "");
            public Property Opførelsesår { get; } = new Property(
                "Opførelsesår",
                "Opførelsesår",
                PsDataType.Integer,
                0);
            public Property SamletBygningsareal { get; } = new Property(
                "SamletBygningsareal",
                "SamletBygningsareal",
                PsDataType.Integer,
                0);
            public Property SamletBoligareal { get; } = new Property(
                "SamletBoligareal",
                "SamletBoligareal",
                PsDataType.Integer,
                0);
            public Property SamletErhvervsareal { get; } = new Property(
                "SamletErhvervsareal",
                "SamletErhvervsareal",
                PsDataType.Integer,
                0);
            public Property BebyggetAreal { get; } = new Property(
                "BebyggetAreal",
                "BebyggetAreal",
                PsDataType.Integer,
                0);
            public Property KælderAreal { get; } = new Property(
                "KælderAreal",
                "KælderAreal",
                PsDataType.Integer,
                0);
            public Property VarmeInstallation { get; } = new Property(
                "VarmeInstallation",
                "VarmeInstallation",
                PsDataType.Text,
                "");
            public Property OpvarmningsMiddel { get; } = new Property(
                "OpvarmningsMiddel",
                "OpvarmningsMiddel",
                PsDataType.Text,
                "");
            public Property Status { get; } = new Property(
                "Status",
                "Status",
                PsDataType.Text,
                "");
            public Property Vejnavn { get; } = new Property(
                "Vejnavn",
                "Vejnavn",
                PsDataType.Text,
                "");
            public Property Husnummer { get; } = new Property(
                "Husnummer",
                "Husnummer",
                PsDataType.Text,
                "");
            public Property Postnr { get; } = new Property(
                "Postnr",
                "Postnr",
                PsDataType.Text,
                "");
            public Property By { get; } = new Property(
                "By",
                "By",
                PsDataType.Text,
                "");
            public Property Beholdes { get; } = new Property(
                "Beholdes",
                "Beholdes",
                PsDataType.TrueFalse,
                true);
            public Property SpecifikVarmeForbrug { get; } = new Property(
                "SpecifikVarmeForbrug",
                "SpecifikVarmeForbrug",
                PsDataType.Real,
                0.0);
            public Property EstimeretVarmeForbrug { get; } = new Property(
                "EstimeretVarmeForbrug",
                "EstimeretVarmeForbrug",
                PsDataType.Real,
                0.0);
            public Property Adresse { get; } = new Property(
                "Adresse",
                "Adresse",
                PsDataType.Text,
                "");
            public Property AdresseDuplikatNr { get; } = new Property(
                "AdresseDuplikatNr",
                "AdresseDuplikatNr",
                PsDataType.Integer,
                0);
            public Property InstallationOgBrændsel { get; } = new Property(
                "InstallationOgBrændsel",
                "InstallationOgBrændsel",
                PsDataType.Text,
                "");
            public Property Type { get; } = new Property(
                "Type",
                "Type",
                PsDataType.Text,
                "");
            public Property DistriktetsNavn { get; } = new Property(
                "Distriktets_navn",
                "Distriktets_navn",
                PsDataType.Text,
                "");
            public StringCollection AppliesTo { get; } = new StringCollection()
                {
                    RXClass.GetClass(typeof(BlockReference)).Name
                };
        }
        public class PSetDef
        {
            public List<Property> ListOfProperties()
            {
                var propDict = ToPropertyDictionary();
                List<Property> list = new List<Property>();
                foreach (var prop in propDict)
                    if (prop.Value is Property) list.Add((Property)prop.Value);

                return list;
            }
            //public Property GetPropertyByName(string propertyName) 
            public Dictionary<string, object> ToPropertyDictionary()
            {
                var dictionary = new Dictionary<string, object>();
                foreach (var propertyInfo in this.GetType().GetProperties())
                    dictionary[propertyInfo.Name] = propertyInfo.GetValue(this, null);
                return dictionary;
            }
            public DefinedSets PSetName()
            {
                var propDict = ToPropertyDictionary();
                return (DefinedSets)propDict["SetName"];
            }
            public StringCollection GetAppliesTo()
            {
                var propDict = ToPropertyDictionary();
                return (StringCollection)propDict["AppliesTo"];
            }
        }
        public class Property
        {
            public string Name { get; }
            public string Description { get; }
            public PsDataType DataType { get; }
            public object DefaultValue { get; }
            public Property(string name, string description, PsDataType dataType, object defaultValue)
            {
                Name = name;
                Description = description;
                DataType = dataType;
                DefaultValue = defaultValue;
            }
        }
        public List<PSetDef> GetPSetClasses()
        {
            var type = this.GetType();
            var types = type.Assembly.GetTypes();
            return types
                .Where(x => x.BaseType != null && x.BaseType.Equals(typeof(PSetDef)))
                .Select(x => Activator.CreateInstance(x))
                .Cast<PSetDef>()
                .ToList();
        }
        public PSetDef GetRequestedDef(DefinedSets requestedSet)
        {
            var list = GetPSetClasses();

            return list.Where(x => x.PSetName() == requestedSet).First();
        }
    }

    public class PropertySetNameComparer : IEqualityComparer<PropertySet>
    {
        public bool Equals(PropertySet x, PropertySet y)
            => x.PropertySetDefinitionName == y.PropertySetDefinitionName;
        public int GetHashCode(PropertySet obj)
            => obj.PropertySetDefinitionName.GetHashCode();
    }
}
