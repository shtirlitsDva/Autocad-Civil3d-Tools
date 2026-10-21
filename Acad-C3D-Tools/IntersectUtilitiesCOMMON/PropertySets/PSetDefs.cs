#if BRICSCAD
using Teigha.DatabaseServices;
using Teigha.Runtime;
using BlockReference = Teigha.DatabaseServices.BlockReference;
using Entity = Teigha.DatabaseServices.Entity;
using ObjectIdCollection = Teigha.DatabaseServices.ObjectIdCollection;
using Oid = Teigha.DatabaseServices.ObjectId;
#else
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
#endif

using IntersectUtilities.UtilsCommon;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;

using static IntersectUtilities.PSetDefs;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities
{
    public partial class PSetDefs
    {
        /// <summary>
        /// A property's data type, numbered as AEC's
        /// Autodesk.Aec.PropertyData.DataType numbers it: the definitions are
        /// read where AEC is absent, and AEC converts by number.
        /// </summary>
        public enum PsDataType
        {
            Integer = 0,
            Real = 1,
            Text = 2,
            TrueFalse = 3,
            AutoIncrement = 4,
            AlphaIncrement = 5,
            List = 6,
            Graphic = 7,
        }

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
            Forsyningsområde,
            Supplypoint,
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

        public abstract partial class PSetDef
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
        }

        public partial class Property
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
        }

    }
}
