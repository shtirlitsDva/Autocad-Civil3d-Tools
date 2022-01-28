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

namespace IntersectUtilities
{
    public class PropertySetManager
    {
        private Database Db { get; }
        private DictionaryPropertySetDefinitions DictionaryPropertySetDefinitions { get; }
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
                return GetPropertySetDefinition(propertySetName);
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
                prdDbg($"Property Set {setName} already defined.");
                return true;
            }
            else
            {
                prdDbg($"Property Set {setName} is not defined.");
                return false;
            }
        }
        private PropertySetDefinition GetPropertySetDefinition(PSetDefs.DefinedSets propertySetName)
        {
            return DictionaryPropertySetDefinitions
                .GetAt(propertySetName.ToString())
                .Go<PropertySetDefinition>(Db.TransactionManager.TopTransaction);
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
            ent.CheckOrOpenForWrite();
            PropertyDataServices.AddPropertySet(ent, PropertySetDefinition.Id);

            return PropertyDataServices.GetPropertySet(ent, this.PropertySetDefinition.Id)
                .Go<PropertySet>(Db.TransactionManager.TopTransaction);
        }
        public string ReadPropertyString(PSetDefs.Property property)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return "";
            else return value.ToString();
        }
        public int ReadPropertyInt(PSetDefs.Property property)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            object value = this.CurrentPropertySet.GetAt(propertyId);
            if (value == null) return 0;
            else return (int)value;
        }
        public void WritePropertyString(PSetDefs.Property property, string value)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            this.CurrentPropertySet.CheckOrOpenForWrite();
            this.CurrentPropertySet.SetAt(propertyId, value);
            this.CurrentPropertySet.DowngradeOpen();
        }
        public void WritePropertyObject(PSetDefs.Property property, object value)
        {
            int propertyId = this.CurrentPropertySet.PropertyNameToId(property.Name);
            this.CurrentPropertySet.CheckOrOpenForWrite();
            this.CurrentPropertySet.SetAt(propertyId, value);
            this.CurrentPropertySet.DowngradeOpen();
        }
        public bool FilterPropetyString(Entity ent, PSetDefs.Property property, string value)
        {
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
        private static object ReadNonDefinedPropertySetObject(Entity ent, string propertySetName, string propertyName)
        {
            ObjectIdCollection psIds = PropertyDataServices.GetPropertySets(ent);
            List<PropertySet> pss = new List<PropertySet>();
            foreach (Oid oid in psIds) pss.Add(oid.Go<PropertySet>(ent.GetTopTx()));

            foreach (PropertySet ps in pss)
            {
                if (ps.PropertySetDefinitionName == propertySetName)
                {
                    try
                    {
                        int id = ps.PropertyNameToId(propertyName);
                        object value = ps.GetAt(id);
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
            DriComponentsGisData
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
}
