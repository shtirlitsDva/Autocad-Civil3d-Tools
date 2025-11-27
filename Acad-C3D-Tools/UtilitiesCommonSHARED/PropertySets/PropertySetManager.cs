using Autodesk.Aec.DatabaseServices;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using PsDataType = Autodesk.Aec.PropertyData.DataType;

namespace IntersectUtilities
{
    public class PropertySetManager
    {
        private Database Db { get; }
        private DictionaryPropertySetDefinitions DictionaryPropertySetDefinitions { get; set; }
        private PropertySetDefinition? propertySetDefinition;
        private PropertySetDefinition PropertySetDefinition
        {
            get
            {
                if (propertySetDefinition == null)
                {
                    prdDbg(
                        "PropertySetDefinition is null! Have you remembered to GetOrAttachPropertySet?"
                    );
                    throw new System.Exception(
                        "PropertySetDefinition is null! Have you remembered to GetOrAttachPropertySet?"
                    );
                }
                return propertySetDefinition;
            }
            set => propertySetDefinition = value;
        }
        private PropertySet? CurrentPropertySet { get; set; }

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
                throw new System.Exception(
                    "PropertySetManager: Must be instantiated within a Transaction!");
            }
            //3
            PropertySetDefinition = PSetDefs.GetOrCreatePropertySetDefinition(
                Db,
                DictionaryPropertySetDefinitions,
                propertySetName);
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
                    {
                        this.CurrentPropertySet = ps;
                        return;
                    }
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

            return PropertyDataServices
                .GetPropertySet(ent, this.PropertySetDefinition.Id)
                .Go<PropertySet>(Db.TransactionManager.TopTransaction);
        }

        public string ReadPropertyString(Entity ent, PSetDefs.Property property)
        {
            try
            {
                GetOrAttachPropertySet(ent);
                int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
                object value = this.CurrentPropertySet.GetAt(propertyId);
                if (value == null)
                    return "";
                else
                    return value.ToString();
            }
            catch (System.Exception ex)
            {
                prdDbg(
                    $"Unable to read property {property.Name} "
                        + $"from set {this.CurrentPropertySet.Name} for entity {ent.Handle}."
                );
                throw;
            }
        }

        public bool ReadPropertyBool(Entity ent, PSetDefs.Property property)
        {
            try
            {
                GetOrAttachPropertySet(ent);
                int propertyId = this.CurrentPropertySet!.PropertyNameToId(property.Name);
                object value = this.CurrentPropertySet.GetAt(propertyId);
                if (value == null)
                    return false;
                else
                    return value is bool boolValue && boolValue;
            }
            catch (System.Exception)
            {
                prdDbg(
                    $"Unable to read property {property.Name} "
                        + $"from set {this.CurrentPropertySet!.Name} for entity {ent.Handle}."
                );
                throw;
            }
        }

        public double ReadPropertyDouble(Entity ent, PSetDefs.Property property)
        {
            GetOrAttachPropertySet(ent);
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null)
                return 0.0;
            else
                return Convert.ToDouble(value);
        }

        [Obsolete("Use ReadPropertyString(Entity ent, PSetDefs.Property property)")]
        public int ReadPropertyInt(PSetDefs.Property property)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null)
                return 0;
            else
                return (int)value;
        }

        public int ReadPropertyInt(Entity ent, PSetDefs.Property property)
        {
            GetOrAttachPropertySet(ent);
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null)
                return 0;
            else
                return (int)value;
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

        public void WritePropertyInt(Entity ent, PSetDefs.Property property, int value)
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
                    {
                        set = ps;
                    }
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
                DictionaryPropertySetDefinitions sourcePropDefDict =
                    new DictionaryPropertySetDefinitions(source.Database);
                DictionaryPropertySetDefinitions targetPropDefDict =
                    new DictionaryPropertySetDefinitions(target.Database);

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

                    PropertySet targetPs = target
                        .GetPropertySets()
                        .Find(x =>
                            x.PropertySetDefinitionName == sourcePs.PropertySetDefinitionName
                        );

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
                prdDbg(ex);
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
                DictionaryPropertySetDefinitions sourcePropDefDict =
                    new DictionaryPropertySetDefinitions(source.Database);

                foreach (PropertySet sourcePs in sourcePss)
                {
                    int propertyId = -1;

                    try
                    {
                        propertyId = sourcePs.PropertyNameToId(propertyName);
                        if (propertyId == -1)
                            continue;
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

        public static object? ReadNonDefinedPropertySetObject(
            Entity ent,
            string propertySetName,
            string propertyName
        )
        {
            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
            Dictionary<string, PropertySet> pss = new Dictionary<string, PropertySet>();

            using OpenCloseTransaction tx =
                    ent.Database.TransactionManager.StartOpenCloseTransaction();

            foreach (Oid oid in psIds)
            {
                PropertySet ps = oid.Go<PropertySet>(tx);

                if (ps.PropertySetDefinitionName == propertySetName)
                {
                    int id = ps.PropertyNameToId(propertyName);
                    object value = ps.GetAt(id);
                    tx.Commit();
                    return value;
                }
            }

            tx.Commit();
            return null;
        }

        /// <summary>
        /// This method handles reading intended nulls.
        /// Otherwise one cannot know if it is intended null or indication of wrong entity for property set.
        /// True signifies that the property was found and read and if the result value is null it is intended.
        /// False signifies that the property was not found and the null value is not intended.
        /// </summary>
        public static bool TryReadNonDefinedPropertySetObject(
            Entity ent,
            string propertySetName,
            string propertyName,
            out object result
        )
        {
            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
            Dictionary<string, PropertySet> pss = new Dictionary<string, PropertySet>();

            using (
                OpenCloseTransaction tx =
                    ent.Database.TransactionManager.StartOpenCloseTransaction()
            )
            {
                foreach (Oid oid in psIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(tx);
                    pss[ps.PropertySetDefinitionName] = ps;
                }

                if (pss.TryGetValue(propertySetName, out PropertySet matchingPropertySet))
                {
                    int id = matchingPropertySet.PropertyNameToId(propertyName);
                    object value = matchingPropertySet.GetAt(id);
                    tx.Commit();
                    result = value;

                    if (result == null)
                        result = "<null>";
                    if (result is string && result.ToString() == "")
                        result = "<empty string>";
#if DEBUG
                    if (result == null)
                        prdDbg($"Entity {ent.Handle} property value for {propertyName} is null.");
#endif
                    return true;
                }

                tx.Commit();
            }
            result = null;
            return false;
        }

        private static object WriteNonDefinedPropertySetObject(
            Entity ent,
            string propertySetName,
            string propertyName,
            object value
        )
        {
            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
            List<PropertySet> pss = new List<PropertySet>();
            foreach (Oid oid in psIds)
                pss.Add(oid.Go<PropertySet>(ent.GetTopTx(), OpenMode.ForWrite));

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

        public static double ReadNonDefinedPropertySetDouble(
            Entity ent,
            string propertySetName,
            string propertyName
        )
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            return Convert.ToDouble(value);
        }

        public static int ReadNonDefinedPropertySetInt(
            Entity ent,
            string propertySetName,
            string propertyName
        )
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            int number = 0;
            try
            {
                if (value == null)
                    return number;
                if (value.ToString().IsNoE())
                    return number;
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

        public static string ReadNonDefinedPropertySetString(
            Entity ent,
            string propertySetName,
            string propertyName
        )
        {
            object value = ReadNonDefinedPropertySetObject(ent, propertySetName, propertyName);
            return Convert.ToString(value);
        }

        public static void WriteNonDefinedPropertySetString(
            Entity ent,
            string propertySetName,
            string propertyName,
            string value
        )
        {
            WriteNonDefinedPropertySetObject(ent, propertySetName, propertyName, value);
        }

        public static void WriteNonDefinedPropertySetDouble(
            Entity ent,
            string propertySetName,
            string propertyName,
            double value
        )
        {
            WriteNonDefinedPropertySetObject(ent, propertySetName, propertyName, value);
        }

        public static void AttachNonDefinedPropertySet(
            Database database,
            Entity ent,
            string propertySetName
        )
        {
            var dictPropSetDefs = new DictionaryPropertySetDefinitions(database);
            Oid psId = dictPropSetDefs.GetAt(propertySetName);
            if (psId == Oid.Null)
                throw new System.Exception($"No property set named {propertySetName} was found!");
            //Add property set to the entity
            ent.CheckOrOpenForWrite();
            PropertyDataServices.AddPropertySet(ent, psId);
        }

        public static void PopulateNonDefinedPropertySet(
            Database database,
            Entity ent,
            string propertySetName,
            Dictionary<string, object> psData
        )
        {
            ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(ent);

            if (propertySetIds.Count == 0)
            {
                throw new System.Exception(
                    $"Property set {propertySetName} have not been attached correctly!"
                );
            }
            else
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(
                        database.TransactionManager.TopTransaction
                    );
                    if (ps.PropertySetDefinitionName == propertySetName)
                    {
                        int i = 0;
                        ps.CheckOrOpenForWrite();
                        foreach (KeyValuePair<string, object> pair in psData)
                        {
                            if (!PsContainsDef(ps, pair.Key))
                            {
                                prdDbg(
                                    $"For propertyset {propertySetName} property {pair.Key} not found!"
                                );
                                continue;
                            }
                            i++;

                            int propertyId = ps.PropertyNameToId(pair.Key);
                            try
                            {
                                if (pair.Value is string)
                                    ps.SetAt(propertyId, pair.Value.ToString());
                                else
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
                throw new System.Exception(
                    $"Property set {propertySetName} could not been found attached to entity!"
                );
            }

            bool PsContainsDef(PropertySet ps, string name)
            {
                PropertySetDefinition propSetDef =
                    ps.PropertySetDefinition.Go<PropertySetDefinition>(
                        database.TransactionManager.TopTransaction
                    );

                PropertyDefinitionCollection defs = propSetDef.Definitions;

                foreach (PropertyDefinition item in defs)
                {
                    if (item.Name == name)
                        return true;
                }

                return false;
            }
        }

        public static void UpdatePropertySetDefinition(
            Database db,
            PSetDefs.DefinedSets propertySetName)
        {
            if (db.TransactionManager.TopTransaction != null)
            {
                prdDbg(
                    $"Some method tried to update PS Def {propertySetName} inside a transaction!"
                );
                return;
            }

            using (Transaction tx = db.TransactionManager.StartTransaction())
            {
                var DictionaryPropertySetDefinitions = new DictionaryPropertySetDefinitions(db);

                if (!DictionaryPropertySetDefinitions.Has(propertySetName.ToString(), tx))
                    return;
                PropertySetDefinition propSetDef = DictionaryPropertySetDefinitions
                    .GetAt(propertySetName.ToString())
                    .Go<PropertySetDefinition>(tx);

                List<PSetDefs.Property> missingProperties = new List<PSetDefs.Property>();
                PropertyDefinitionCollection col = propSetDef.Definitions;

                PSetDefs defs = new PSetDefs();
                var setDef = defs.GetRequestedDef(propertySetName);
                var propDict = setDef.ToPropertyDictionary();
                List<string> propDefNames = new List<string>();
                foreach (PropertyDefinition propDef in col)
                    propDefNames.Add(propDef.Name);
                foreach (KeyValuePair<string, object> kvp in propDict)
                    if (kvp.Value is PSetDefs.Property prop)
                        if (!propDefNames.Contains(prop.Name))
                            missingProperties.Add(prop);

                if (missingProperties.Count == 0)
                    return;

                foreach (PSetDefs.Property property in missingProperties)
                {
                    propSetDef.CheckOrOpenForWrite();
                    property.AddToDefinition(db, propSetDef);
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

            var psName = StringGridFormCaller.Call(
                dpsdict.NamesInUse.ToList().Order(), "Select property set: ");

            if (psName == null) return default;

            PropertySetDefinition psDef = dpsdict
                .GetAt(psName)
                .Go<PropertySetDefinition>(db.TransactionManager.TopTransaction);
            PropertyDefinitionCollection propDefs = psDef.Definitions;
            List<string> propDefNames = new List<string>();
            foreach (PropertyDefinition propDef in propDefs)
                propDefNames.Add(propDef.Name);

            string propName = StringGridFormCaller.Call(propDefNames.Order(), "Select property name: ");
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
            var psName = StringGridFormCaller.Call(
                dpsdict.NamesInUse.ToList().Order(), "Select property set: ");
            if (psName == null)
                return default;
            return psName;
        }

        public static Oid[] SelectByPsValue(
            Database db,
            MatchTypeEnum matchType,
            string valueToFind = null
        )
        {
            IEnumerable<Entity> ents;
            Transaction tx = db.TransactionManager.TopTransaction;
            var sNp = AskForSetAndProperty(db);

            switch (matchType)
            {
                case MatchTypeEnum.Equals:
                    ents = db.ListOfType<Entity>(tx, true)
                        .Where(x =>
                            PropertySetManager.ReadNonDefinedPropertySetString(
                                x,
                                sNp.PsSetName,
                                sNp.PropertyName
                            ) == valueToFind
                        );
                    break;
                case MatchTypeEnum.Contains:
                    ents = db.ListOfType<Entity>(tx, true)
                        .Where(x =>
                            PropertySetManager
                                .ReadNonDefinedPropertySetString(x, sNp.PsSetName, sNp.PropertyName)
                                .Contains(valueToFind, StringComparison.OrdinalIgnoreCase)
                        );
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

            if (values.Contains(""))
            {
                values.Remove("");
                values.Add("<empty>");
            }
            ;

            foreach (var item in values.OrderBy(x => x))
                prdDbg(item);
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
            if (propertySetIds.Count == 0)
                return false;

            bool foundPs = false;
            using (var tx = ent.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(
                        ent.Database.TransactionManager.TopTransaction
                    );
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

        public static bool IsPropertySetAttached(
            Entity ent,
            string propertySetName,
            MatchTypeEnum matchType
        )
        {
            var propertySetIds = PropertyDataServices.GetPropertySets(ent);
            if (propertySetIds.Count == 0)
                return false;

            bool foundPs = false;
            using (var tx = ent.Database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (Oid oid in propertySetIds)
                {
                    PropertySet ps = oid.Go<PropertySet>(
                        ent.Database.TransactionManager.TopTransaction
                    );
                    switch (matchType)
                    {
                        case MatchTypeEnum.Equals:
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

        public static Dictionary<string, object> DumpAllProperties(Entity ent)
        {
            //Dictionary<string, Dictionary<string, object>> completeData = new Dictionary<string, Dictionary<string, object>>();

            Transaction tx = ent.Database.TransactionManager.TopTransaction;
            if (tx == null)
                throw new System.Exception(
                    "PropertySetManager.DumpAllProperties(Entity ent) called outside Transaction!"
                );

            var propertySetIds = PropertyDataServices.GetPropertySets(ent);
            if (propertySetIds.Count == 0)
                return null;
            if (propertySetIds.Count != 1)
                throw new System.Exception(
                    $"Entity {ent.Handle} cannot dump properties as the method expects only one property set per object!"
                );

            foreach (Oid oid in propertySetIds)
            {
                Dictionary<string, object> data = new Dictionary<string, object>();

                PropertySet ps = oid.Go<PropertySet>(tx);
                //completeData.Add(ps.PropertySetDefinitionName, data);

                PropertySetDefinition psDef = ps.PropertySetDefinition.Go<PropertySetDefinition>(
                    tx
                );
                PropertyDefinitionCollection pDefs = psDef.Definitions;

                data.Add("Ler2Type", ps.PropertySetDefinitionName);

                foreach (PropertyDefinition def in pDefs)
                {
                    string propName = def.Name;
                    int id = ps.PropertyNameToId(propName);
                    PropertySetData storedData = ps.GetPropertySetDataAt(id);
                    data.Add(propName, storedData.GetData());
                }

                return data;
            }

            return null;
            //return completeData;
        }

        public static HashSet<string> GetPropertySetNames(Database db)
        {
            DictionaryPropertySetDefinitions dpsdict = new DictionaryPropertySetDefinitions(db);
            return dpsdict.NamesInUse.ToHashSet();
        }

        public static Dictionary<string, PsDataType> GetPropertyNamesAndDataTypes(
            Database db,
            string propertySetName
        )
        {
            var values = new Dictionary<string, PsDataType>();

            using (OpenCloseTransaction tx = db.TransactionManager.StartOpenCloseTransaction())
            {
                var dpsd = new DictionaryPropertySetDefinitions(db);
                Oid psDefId = dpsd.GetAt(propertySetName);

                PropertySetDefinition psDef = psDefId.Go<PropertySetDefinition>(tx);
                PropertyDefinitionCollection defs = psDef.Definitions;

                for (int i = 0; i < defs.Count; i++)
                {
                    PropertyDefinition def = defs[i];
                    values.Add(def.Name, def.DataType);
                }
            }

            return values;
        }

        public static void DeleteAllPropertySets(Database db)
        {
            using (OpenCloseTransaction tx = db.TransactionManager.StartOpenCloseTransaction())
            {
                try
                {
                    var dpsd = new DictionaryPropertySetDefinitions(db);
                    foreach (var name in dpsd.NamesInUse)
                    {
                        var psDefId = dpsd.GetAt(name);
                        var setIdsCol = PropertyDataServices.GetAllPropertySetsUsingDefinition(
                            psDefId,
                            false
                        );

                        foreach (Oid oid in setIdsCol)
                        {
                            PropertySet ps = oid.Go<PropertySet>(tx);
                            ps.CheckOrOpenForWrite();
                            ps.Erase(true);
                        }

                        PropertySetDefinition psd = psDefId.Go<PropertySetDefinition>(tx);
                        psd.CheckOrOpenForWrite();
                        psd.Erase(true);
                    }
                }
                catch (System.Exception ex)
                {
                    prdDbg(ex);
                    tx.Abort();
                    return;
                }
                tx.Commit();
            }
        }

        public enum MatchTypeEnum
        {
            Equals,
            Contains,
        }
    }
    public class PSM_Pipeline : PropertySetManager
    {
        public PSM_Pipeline(Database database)
            : base(database, PSetDefs.DefinedSets.DriPipelineData) { }
        private PSetDefs.DriPipelineData def => new();
        public string BelongsToAlignment(Entity x) =>
            ReadPropertyString(x, def.BelongsToAlignment);
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
            BBR,
            NtrData,
        }

        public class FJV_område : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.FJV_område;
            public Property Område { get; } =
                new Property(
                    "Område",
                    "Navnet på det område polylinjen omgrænser",
                    PsDataType.Text,
                    ""
                );
            public override StringCollection AppliesTo { get; } =
                new StringCollection() { RXClass.GetClass(typeof(Polyline)).Name };
        }

        public class FJV_fremtid : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.FJV_fremtid;
            public Property Bemærkninger { get; } =
                new Property("Bemærkninger", "Bemærkninger", PsDataType.Text, "");
            public Property Distriktets_navn { get; } =
                new Property("Distriktets_navn", "Distriktets_navn.", PsDataType.Text, "");
            public Property Length { get; } =
                new Property("Length", "Stores the length of the entity.", PsDataType.Real, 0);
            public override StringCollection AppliesTo { get; } =
                new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(Line)).Name,
                };
        }

        public class DriCrossingData : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.DriCrossingData;
            public Property Diameter { get; } =
                new Property("Diameter", "Stores crossing pipe's diameter.", PsDataType.Integer, 0);
            public Property Alignment { get; } =
                new Property("Alignment", "Stores crossing alignment name.", PsDataType.Text, "");
            public Property SourceEntityHandle { get; } =
                new Property(
                    "SourceEntityHandle",
                    "Stores the handle of the crossing entity.",
                    PsDataType.Text,
                    ""
                );
            public Property CanBeRelocated { get; } =
                new Property(
                    "CanBeRelocated",
                    "Indicates if the crossing utility can be relocated.",
                    PsDataType.TrueFalse,
                    false
                );
            public override StringCollection AppliesTo { get; } =
                new StringCollection() { RXClass.GetClass(typeof(CogoPoint)).Name };
        }

        public class DriSourceReference : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.DriSourceReference;
            public Property SourceEntityHandle { get; } =
                new Property(
                    "SourceEntityHandle",
                    "Handle of the source entity which provided information for this entity.",
                    PsDataType.Text,
                    ""
                );
            public Property AlignmentStation { get; } =
                new Property(
                    "AlignmentStation",
                    "The station at which referenced object is situated.",
                    PsDataType.Real,
                    99999.9
                );
            public override StringCollection AppliesTo { get; } =
                new StringCollection() { RXClass.GetClass(typeof(BlockReference)).Name };
        }

        public class DriPipelineData : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.DriPipelineData;
            public Property BelongsToAlignment { get; } =
                new Property(
                    "BelongsToAlignment",
                    "Name of the alignment the component belongs to.",
                    PsDataType.Text,
                    ""
                );
            public Property BranchesOffToAlignment { get; } =
                new Property(
                    "BranchesOffToAlignment",
                    "Name of the alignment the component branches off to.",
                    PsDataType.Text,
                    ""
                );
            public Property EtapeNavn { get; } =
                new Property(
                    "EtapeNavn",
                    "Name of the area the pipe belongs to.",
                    PsDataType.Text,
                    ""
                );
            public override StringCollection AppliesTo { get; } =
                new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(BlockReference)).Name,
                };
        }

        public class DriGasDimOgMat : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.DriGasDimOgMat;
            public Property Dimension { get; } =
                new Property("Dimension", "Dimension of the gas pipe.", PsDataType.Integer, 0);
            public Property Material { get; } =
                new Property("Material", "Material of the gas pipe.", PsDataType.Text, "");
            public Property Bemærk { get; } =
                new Property("Bemærk", "Bemærkning til ledning.", PsDataType.Text, "");
            public override StringCollection AppliesTo { get; } =
                new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(Polyline3d)).Name,
                    RXClass.GetClass(typeof(Line)).Name,
                };
        }

        public class DriOmråder : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.DriOmråder;
            public Property Nummer { get; } =
                new Property("Nummer", "Number of pipeline.", PsDataType.Text, "");
            public Property Vejnavn { get; } =
                new Property("Vejnavn", "Name of street.", PsDataType.Text, "");

            //public Property Ejerskab { get; } = new Property(
            //    "Ejerskab",
            //    "Owner type of street.",
            //    PsDataType.Text,
            //    "");
            public Property Vejklasse { get; } =
                new Property("Vejklasse", "Street/road class.", PsDataType.Text, "");
            public Property Belægning { get; } =
                new Property("Belægning", "Pavement type.", PsDataType.Text, "");
            public override StringCollection AppliesTo { get; } =
                new StringCollection() { RXClass.GetClass(typeof(Polyline)).Name };
        }

        public class DriGraph : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.DriGraph;
            public Property ConnectedEntities { get; } =
                new Property("ConnectedEntities", "Lists connected entities", PsDataType.Text, "");
            public override StringCollection AppliesTo { get; } =
                new StringCollection()
                {
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(BlockReference)).Name,
                };
        }

        public class DriDimGraph : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.DriDimGraph;
            public Property Parent { get; } =
                new Property("Parent", "Lists parent entity", PsDataType.Text, "");
            public Property Children { get; } =
                new Property("Children", "Lists children entities", PsDataType.Text, "");
            public override StringCollection AppliesTo { get; } =
                new StringCollection()
                {
                    RXClass.GetClass(typeof(Line)).Name,
                    RXClass.GetClass(typeof(Polyline)).Name,
                    RXClass.GetClass(typeof(BlockReference)).Name,
                };
        }

        public class BBR : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.BBR;
            public Property id_lokalId { get; } =
                new Property("id_lokalId", "id_lokalId", PsDataType.Text, "");
            public Property id_husnummerid { get; } =
                new Property("id_husnummerid", "id_husnummerid", PsDataType.Text, "");
            public Property Name { get; } = new Property("Name", "Name", PsDataType.Text, "");
            public Property Bygningsnummer { get; } =
                new Property("Bygningsnummer", "Bygningsnummer", PsDataType.Integer, 0);
            public Property BygningsAnvendelseNyTekst { get; } =
                new Property(
                    "BygningsAnvendelseNyTekst",
                    "BygningsAnvendelseNyTekst",
                    PsDataType.Text,
                    ""
                );
            public Property BygningsAnvendelseNyKode { get; } =
                new Property(
                    "BygningsAnvendelseNyKode",
                    "BygningsAnvendelseNyKode",
                    PsDataType.Text,
                    ""
                );
            public Property BygningsAnvendelseGlTekst { get; } =
                new Property(
                    "BygningsAnvendelseGlTekst",
                    "BygningsAnvendelseGlTekst",
                    PsDataType.Text,
                    ""
                );
            public Property BygningsAnvendelseGlKode { get; } =
                new Property(
                    "BygningsAnvendelseGlKode",
                    "BygningsAnvendelseGlKode",
                    PsDataType.Text,
                    ""
                );
            public Property Opførelsesår { get; } =
                new Property("Opførelsesår", "Opførelsesår", PsDataType.Integer, 0);
            public Property SamletBygningsareal { get; } =
                new Property("SamletBygningsareal", "SamletBygningsareal", PsDataType.Integer, 0);
            public Property SamletBoligareal { get; } =
                new Property("SamletBoligareal", "SamletBoligareal", PsDataType.Integer, 0);
            public Property SamletErhvervsareal { get; } =
                new Property("SamletErhvervsareal", "SamletErhvervsareal", PsDataType.Integer, 0);
            public Property BebyggetAreal { get; } =
                new Property("BebyggetAreal", "BebyggetAreal", PsDataType.Integer, 0);
            public Property KælderAreal { get; } =
                new Property("KælderAreal", "KælderAreal", PsDataType.Integer, 0);
            public Property VarmeInstallation { get; } =
                new Property("VarmeInstallation", "VarmeInstallation", PsDataType.Text, "");
            public Property OpvarmningsMiddel { get; } =
                new Property("OpvarmningsMiddel", "OpvarmningsMiddel", PsDataType.Text, "");
            public Property Status { get; } = new Property("Status", "Status", PsDataType.Text, "");
            public Property Vejnavn { get; } =
                new Property("Vejnavn", "Vejnavn", PsDataType.Text, "");
            public Property Vejklasse { get; } =
                new Property("Vejklasse", "Vejklasse", PsDataType.Integer, 0);
            public Property Husnummer { get; } =
                new Property("Husnummer", "Husnummer", PsDataType.Text, "");
            public Property Postnr { get; } = new Property("Postnr", "Postnr", PsDataType.Text, "");
            public Property By { get; } = new Property("By", "By", PsDataType.Text, "");
            public Property Beholdes { get; } =
                new Property("Beholdes", "Beholdes", PsDataType.TrueFalse, true);
            public Property SpecifikVarmeForbrug { get; } =
                new Property("SpecifikVarmeForbrug", "SpecifikVarmeForbrug", PsDataType.Real, 0.0);
            public Property EstimeretVarmeForbrug { get; } =
                new Property(
                    "EstimeretVarmeForbrug",
                    "EstimeretVarmeForbrug",
                    PsDataType.Real,
                    0.0
                );
            public Property Adresse { get; } =
                new Property("Adresse", "Adresse", PsDataType.Text, "");
            public Property AdresseDuplikatNr { get; } =
                new Property("AdresseDuplikatNr", "AdresseDuplikatNr", PsDataType.Integer, 0);
            public Property InstallationOgBrændsel { get; } =
                new Property(
                    "InstallationOgBrændsel",
                    "InstallationOgBrændsel",
                    PsDataType.Text,
                    ""
                );
            public Property Type { get; } = new Property("Type", "Type", PsDataType.Text, "");
            public Property DistriktetsNavn { get; } =
                new Property("Distriktets_navn", "Distriktets_navn", PsDataType.Text, "");
            public Property AntalEnheder { get; } =
                new Property("AntalEnheder", "AntalEnheder", PsDataType.Integer, 1);
            public Property TempFrem { get; } =
                new Property("TempFrem", "Fremløbstemperatur for bygningen", PsDataType.Real, 0.0);
            public Property TempRetur { get; } =
                new Property("TempRetur", "Returtemperatur for bygningen", PsDataType.Real, 0.0);
            public Property TempDelta { get; } =
                new Property("TempDelta", "Afkøling for bygningen. Beregnet som TempFrem - TempRetur.", PsDataType.Real, 0.0);
            public override StringCollection AppliesTo { get; } =
                new StringCollection() { RXClass.GetClass(typeof(BlockReference)).Name };
        }

        public class NtrData : PSetDef
        {
            public override DefinedSets SetName { get; } = DefinedSets.NtrData;
            public Property AfgreningMedSpringDir { get; } =
                new ListProperty.UpDown(
                    "AfgreningMedSpringDir",
                    "Direction of the element branch.");
            public Property VertikalBøjningDir { get; } =
                new ListProperty.UpDown(
                    "VertikalBøjningDir",
                    "Direction of the element vertical bend.");
            public Property BøjningBenRotationAxis { get; } =
                new ListProperty.NearFar(
                    "BøjningBenRotationAxis",
                    "Location of the bends' leg which is the axis of rotation.");
            public Property BøjningRotationVinkel { get; } =
                new Property(
                    "BøjningRotationVinkel",
                    "Rotation angle of the bend.",
                    PsDataType.Real,
                    0.0
                );
            public override StringCollection AppliesTo { get; } =
                new StringCollection()
                {
                    RXClass.GetClass(typeof(BlockReference)).Name,
                };
        }

        public abstract class PSetDef
        {
            public abstract DefinedSets SetName { get; }
            public abstract StringCollection AppliesTo { get; }

            public List<Property> ListOfProperties()
            {
                var propDict = ToPropertyDictionary();
                List<Property> list = new List<Property>();
                foreach (var prop in propDict)
                    if (prop.Value is Property)
                        list.Add((Property)prop.Value);

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
                return SetName;
            }

            public StringCollection GetAppliesTo()
            {
                return AppliesTo;
            }

            public virtual PropertySetDefinition CreatePropertySetDefinition(
                Database database,
                DictionaryPropertySetDefinitions dictionaryPropertySetDefinitions
            )
            {
                // Create the PropertySetDefinition and let each property add itself (polymorphism)
                string setName = SetName.ToString();
                PropertySetDefinition propSetDef = new PropertySetDefinition();
                propSetDef.SetToStandard(database);
                propSetDef.SubSetDatabaseDefaults(database);
                propSetDef.Description = setName;
                bool isStyle = false;

                propSetDef.SetAppliesToFilter(GetAppliesTo(), isStyle);

                foreach (PSetDefs.Property property in ListOfProperties())
                    property.AddToDefinition(database, propSetDef);

                using (Transaction defTx = database.TransactionManager.StartTransaction())
                {
                    dictionaryPropertySetDefinitions.AddNewRecord(setName, propSetDef);
                    defTx.AddNewlyCreatedDBObject(propSetDef, true);
                    defTx.Commit();
                }

                return propSetDef;
            }
        }

        public class Property
        {
            public string Name { get; }
            public string Description { get; }
            public PsDataType DataType { get; }
            public object DefaultValue { get; }

            public Property(
                string name,
                string description,
                PsDataType dataType,
                object defaultValue
            )
            {
                Name = name;
                Description = description;
                DataType = dataType;
                DefaultValue = defaultValue;
            }

            public virtual void AddToDefinition(Database database, PropertySetDefinition propSetDef)
            {
                var propDefManual = new PropertyDefinition();
                propDefManual.SetToStandard(database);
                propDefManual.SubSetDatabaseDefaults(database);

                propDefManual.Name = Name;
                propDefManual.Description = Description;
                propDefManual.DataType = DataType;
                propDefManual.DefaultData = DefaultValue;

                propSetDef.Definitions.Add(propDefManual);
            }
        }

        public class ListProperty : Property
        {
            public string ListName { get; }
            public StringCollection ListAppliesTo { get; }
            public StringCollection ListItems { get; }

            public ListProperty(
                string name,
                string description,
                string listName,
                StringCollection listAppliesTo,
                StringCollection listItems)
                : base(name, description, PsDataType.List, null)
            {
                ListName = listName;
                ListAppliesTo = listAppliesTo;
                ListItems = listItems;
            }

            public override void AddToDefinition(Database database, PropertySetDefinition propSetDef)
            {
                //https://forums.autodesk.com/t5/net-forum/modify-list-definition-for-a-manual-property-definition/td-p/12055826

                if (database == null || database.TransactionManager.TopTransaction == null)
                    throw new System.Exception(
                        "ListProperty.AddToDefinition called outside of transaction!");

                DictionaryListDefinition ldDict = new DictionaryListDefinition(database);

                Oid listDefId = Oid.Null;

                if (!ldDict.Has(ListName, database.TransactionManager.TopTransaction))
                {
                    using (Transaction tr = database.TransactionManager.StartTransaction())
                    {
                        var ld = new ListDefinition();
                        ld.SubSetDatabaseDefaults(database);
                        ld.SetToStandard(database);

                        ldDict.AddNewRecord(ListName, ld);

                        foreach (string item in ListItems)
                        {
                            Oid itemId = ld.AddListItem(item);
                        }

                        ld.Description = $"List for {propSetDef.Description}.{Name}";
                        ld.AppliesToFilter = ListAppliesTo;

                        tr.AddNewlyCreatedDBObject(ld, true);
                        listDefId = ld.Id;
                        tr.Commit();
                    }
                }
                else
                {
                    var ldOid = ldDict.GetAt(ListName);
                    listDefId = ldOid;                    
                }

                var propDefManual = new PropertyDefinition();
                propDefManual.SetToStandard(database);
                propDefManual.SubSetDatabaseDefaults(database);

                propDefManual.Name = Name;
                propDefManual.Description = Description;
                propDefManual.DataType = PsDataType.List;
                propDefManual.ListDefinitionId = listDefId;
                propDefManual.DefaultData = ListItems[0];

                propSetDef.CheckOrOpenForWrite();
                propSetDef.Definitions.Add(propDefManual);
            }

            public class UpDown : ListProperty
            {
                public UpDown(string name, string description)
                    : base(
                        name,
                        description,
                        "UpDown",
                        new StringCollection() { "AecListUserManualPropertyDef" },
                        new StringCollection() { "Up", "Down" }
                    )
                { }
            }
            public class NearFar : ListProperty
            {
                public NearFar(string name, string description)
                    : base(
                        name,
                        description,
                        "NearFar",
                        new StringCollection() { "AecListUserManualPropertyDef" },
                        new StringCollection() { "Near", "Far" }
                    )
                { }
            }
        }

        public List<PSetDef> GetPSetClasses()
        {
            var type = this.GetType();
            var types = type.Assembly.GetTypes();
            return types
                .Where(x =>
                    x.BaseType != null &&
                    !x.IsAbstract &&
                    typeof(PSetDef).IsAssignableFrom(x) &&
                    x != typeof(PSetDef))
                .Select(x => Activator.CreateInstance(x))
                .Cast<PSetDef>()
                .ToList();
        }

        public PSetDef GetRequestedDef(DefinedSets requestedSet)
        {
            var list = GetPSetClasses();

            return list.Where(x => x.PSetName() == requestedSet).First();
        }

        public static PropertySetDefinition GetOrCreatePropertySetDefinition(
            Database database,
            DictionaryPropertySetDefinitions dictionaryPropertySetDefinitions,
            DefinedSets propertySetName)
        {
            if (PropertySetDefinitionExists(database, dictionaryPropertySetDefinitions, propertySetName))
            {
                return GetPropertySetDefinition(database, dictionaryPropertySetDefinitions, propertySetName);
            }
            else
            {
                // Use the specific definition instance to create the property set definition.
                PSetDefs defs = new PSetDefs();
                PSetDefs.PSetDef currentDef = defs.GetRequestedDef(propertySetName);
                return currentDef.CreatePropertySetDefinition(database, dictionaryPropertySetDefinitions);
            }
        }

        private static bool PropertySetDefinitionExists(
            Database database,
            DictionaryPropertySetDefinitions dictionaryPropertySetDefinitions,
            DefinedSets propertySetName)
        {
            string setName = propertySetName.ToString();
            if (dictionaryPropertySetDefinitions.Has(setName, database.TransactionManager.TopTransaction))
            {
                return true;
            }
            else
            {
                prdDbg($"Property Set {setName} is not defined. Creating...");
                return false;
            }
        }

        private static PropertySetDefinition GetPropertySetDefinition(
            Database database,
            DictionaryPropertySetDefinitions dictionaryPropertySetDefinitions,
            DefinedSets propertySetName,
            Transaction tx = null)
        {
            if (database == null)
                throw new System.Exception("Database is null!");
            if (tx == null && database.TransactionManager.TopTransaction == null)
                throw new System.Exception(
                    "GetPropertySetDefinition: Usage outside of transaction!"
                );
            return dictionaryPropertySetDefinitions
                .GetAt(propertySetName.ToString())
                .Go<PropertySetDefinition>(tx ?? database.TransactionManager.TopTransaction);
        }

    }

    public class PropertySetNameComparer : IEqualityComparer<PropertySet>
    {
        public bool Equals(PropertySet x, PropertySet y) =>
            x.PropertySetDefinitionName == y.PropertySetDefinitionName;

        public int GetHashCode(PropertySet obj) => obj.PropertySetDefinitionName.GetHashCode();
    }

    public class PropertySetHelper
    {
        public PropertySetManager Graph;
        public PSM_Pipeline Pipeline;
        public PSetDefs.DriGraph GraphDef;
        public PSetDefs.DriPipelineData PipelineDef;

        public PropertySetHelper(Database db)
        {
            if (db == null)
                throw new System.Exception(
                    "Either ents collection, first element or its' database is null!"
                );

            Graph = new PropertySetManager(db, PSetDefs.DefinedSets.DriGraph);
            GraphDef = new PSetDefs.DriGraph();
            Pipeline = new PSM_Pipeline(db);
            PipelineDef = new PSetDefs.DriPipelineData();
        }
    }

    public class NtrData : PropertySetManager
    {
        private Entity _ent;
        private PSetDefs.NtrData _def = new PSetDefs.NtrData();
        public NtrData(Entity ent)
            : base(ent.Database, PSetDefs.DefinedSets.NtrData)
        {
            _ent = ent;
            GetOrAttachPropertySet(_ent);
        }
        public string AfgreningMedSpringDir
        {
            get => ReadPropertyString(_ent, _def.AfgreningMedSpringDir);
            set => WritePropertyObject(_ent, _def.AfgreningMedSpringDir, value);
        }

        public string VertikalBøjningDir
        {
            get => ReadPropertyString(_ent, _def.VertikalBøjningDir);
            set => WritePropertyObject(_ent, _def.VertikalBøjningDir, value);
        }

        public string BøjningBenRotationAxis
        {
            get => ReadPropertyString(_ent, _def.BøjningBenRotationAxis);
            set => WritePropertyObject(_ent, _def.BøjningBenRotationAxis, value);
        }

        public double BøjningRotationVinkel
        {
            get => ReadPropertyDouble(_ent, _def.BøjningRotationVinkel);
            set => WritePropertyObject(_ent, _def.BøjningRotationVinkel, value);
        }

    }

    public class BBR : PropertySetManager
    {
        private Entity _ent;
        private PSetDefs.BBR _def = new PSetDefs.BBR();

        public BBR(Entity ent)
            : base(ent.Database, PSetDefs.DefinedSets.BBR)
        {
            _ent = ent;
        }

        public string id_lokalId
        {
            get => ReadPropertyString(_ent, _def.id_lokalId);
            set => WritePropertyObject(_ent, _def.id_lokalId, value);
        }
        public string id_husnummerid
        {
            get => ReadPropertyString(_ent, _def.id_husnummerid);
            set => WritePropertyObject(_ent, _def.id_husnummerid, value);
        }
        public string Name
        {
            get => ReadPropertyString(_ent, _def.Name);
            set => WritePropertyObject(_ent, _def.Name, value);
        }
        public int Bygningsnummer
        {
            get => ReadPropertyInt(_ent, _def.Bygningsnummer);
            set => WritePropertyObject(_ent, _def.Bygningsnummer, value);
        }
        public string BygningsAnvendelseNyTekst
        {
            get => ReadPropertyString(_ent, _def.BygningsAnvendelseNyTekst);
            set => WritePropertyObject(_ent, _def.BygningsAnvendelseNyTekst, value);
        }
        public string BygningsAnvendelseNyKode
        {
            get => ReadPropertyString(_ent, _def.BygningsAnvendelseNyKode);
            set => WritePropertyObject(_ent, _def.BygningsAnvendelseNyKode, value);
        }
        public string BygningsAnvendelseGlTekst
        {
            get => ReadPropertyString(_ent, _def.BygningsAnvendelseGlTekst);
            set => WritePropertyObject(_ent, _def.BygningsAnvendelseGlTekst, value);
        }
        public string BygningsAnvendelseGlKode
        {
            get => ReadPropertyString(_ent, _def.BygningsAnvendelseGlKode);
            set => WritePropertyObject(_ent, _def.BygningsAnvendelseGlKode, value);
        }
        public int Opførelsesår
        {
            get => ReadPropertyInt(_ent, _def.Opførelsesår);
            set => WritePropertyObject(_ent, _def.Opførelsesår, value);
        }
        public int SamletBygningsareal
        {
            get => ReadPropertyInt(_ent, _def.SamletBygningsareal);
            set => WritePropertyObject(_ent, _def.SamletBygningsareal, value);
        }
        public int SamletBoligareal
        {
            get => ReadPropertyInt(_ent, _def.SamletBoligareal);
            set => WritePropertyObject(_ent, _def.SamletBoligareal, value);
        }
        public int SamletErhvervsareal
        {
            get => ReadPropertyInt(_ent, _def.SamletErhvervsareal);
            set => WritePropertyObject(_ent, _def.SamletErhvervsareal, value);
        }
        public int BebyggetAreal
        {
            get => ReadPropertyInt(_ent, _def.BebyggetAreal);
            set => WritePropertyObject(_ent, _def.BebyggetAreal, value);
        }
        public int KælderAreal
        {
            get => ReadPropertyInt(_ent, _def.KælderAreal);
            set => WritePropertyObject(_ent, _def.KælderAreal, value);
        }
        public string VarmeInstallation
        {
            get => ReadPropertyString(_ent, _def.VarmeInstallation);
            set => WritePropertyObject(_ent, _def.VarmeInstallation, value);
        }
        public string OpvarmningsMiddel
        {
            get => ReadPropertyString(_ent, _def.OpvarmningsMiddel);
            set => WritePropertyObject(_ent, _def.OpvarmningsMiddel, value);
        }
        public string Status
        {
            get => ReadPropertyString(_ent, _def.Status);
            set => WritePropertyObject(_ent, _def.Status, value);
        }
        public string Vejnavn
        {
            get => ReadPropertyString(_ent, _def.Vejnavn);
            set => WritePropertyObject(_ent, _def.Vejnavn, value);
        }
        public int Vejklasse
        {
            get => ReadPropertyInt(_ent, _def.Vejklasse);
            set => WritePropertyObject(_ent, _def.Vejklasse, value);
        }
        public string Husnummer
        {
            get => ReadPropertyString(_ent, _def.Husnummer);
            set => WritePropertyObject(_ent, _def.Husnummer, value);
        }
        public string Postnr
        {
            get => ReadPropertyString(_ent, _def.Postnr);
            set => WritePropertyObject(_ent, _def.Postnr, value);
        }
        public string By
        {
            get => ReadPropertyString(_ent, _def.By);
            set => WritePropertyObject(_ent, _def.By, value);
        }

        //public bool Beholdes
        //{
        //    get => ReadPropertyBool(_ent, _def.Beholdes);
        //    set => WritePropertyObject(_ent, _def.Beholdes, value);
        //}
        public double SpecifikVarmeForbrug
        {
            get => ReadPropertyDouble(_ent, _def.SpecifikVarmeForbrug);
            set => WritePropertyObject(_ent, _def.SpecifikVarmeForbrug, value);
        }
        public double EstimeretVarmeForbrug
        {
            get => ReadPropertyDouble(_ent, _def.EstimeretVarmeForbrug);
            set => WritePropertyObject(_ent, _def.EstimeretVarmeForbrug, value);
        }
        public string Adresse
        {
            get => ReadPropertyString(_ent, _def.Adresse);
            set => WritePropertyObject(_ent, _def.Adresse, value);
        }
        public int AdresseDuplikatNr
        {
            get => ReadPropertyInt(_ent, _def.AdresseDuplikatNr);
            set => WritePropertyObject(_ent, _def.AdresseDuplikatNr, value);
        }
        public string InstallationOgBrændsel
        {
            get => ReadPropertyString(_ent, _def.InstallationOgBrændsel);
            set => WritePropertyObject(_ent, _def.InstallationOgBrændsel, value);
        }
        public string Type
        {
            get => ReadPropertyString(_ent, _def.Type);
            set => WritePropertyObject(_ent, _def.Type, value);
        }
        public string DistriktetsNavn
        {
            get => ReadPropertyString(_ent, _def.DistriktetsNavn);
            set => WritePropertyObject(_ent, _def.DistriktetsNavn, value);
        }
        public int AntalEnheder
        {
            get => ReadPropertyInt(_ent, _def.AntalEnheder);
            set => WritePropertyObject(_ent, _def.AntalEnheder, value);
        }
        public double TempFrem
        {
            get => ReadPropertyDouble(_ent, _def.TempFrem);
            set => WritePropertyObject(_ent, _def.TempFrem, value);
        }
        public double TempRetur
        {
            get => ReadPropertyDouble(_ent, _def.TempRetur);
            set => WritePropertyObject(_ent, _def.TempRetur, value);
        }
        public double TempDelta
        {
            get => ReadPropertyDouble(_ent, _def.TempDelta);
            set => WritePropertyObject(_ent, _def.TempDelta, value);
        }
    }
}