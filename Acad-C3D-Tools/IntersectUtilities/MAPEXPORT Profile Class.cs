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
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.ObjectData;
using Autodesk.Gis.Map.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using MoreLinq;
using System.Text;
using System.Text.RegularExpressions;
using static IntersectUtilities.Enums;
using static IntersectUtilities.HelperMethods;
using static IntersectUtilities.Utils;
using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DataType = Autodesk.Gis.Map.Constants.DataType;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
using OpenMode = Autodesk.AutoCAD.DatabaseServices.OpenMode;

namespace IntersectUtilities.Mapexport
{

    // NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class AdMapExportProfile
    {
        private object loadedProfileNameField;

        private AdMapExportProfileStorageOptions storageOptionsField;

        private AdMapExportProfileSelectionOptions selectionOptionsField;

        private AdMapExportProfileTranslationOptions translationOptionsField;

        private AdMapExportProfileTopologyOptions topologyOptionsField;

        private AdMapExportProfileLayerOptions layerOptionsField;

        private AdMapExportProfileFeatureClassOptions featureClassOptionsField;

        private AdMapExportProfileTableDataOptions tableDataOptionsField;

        private AdMapExportProfileCoordSysOptions coordSysOptionsField;

        private AdMapExportProfileTargetNameOptions targetNameOptionsField;

        private object driverOptionsField;

        private byte useUniqueKeyFieldField;

        private string useUniqueKeyFieldNameField;

        private AdMapExportProfileNameValuePair[] expressionFieldMappingsField;

        private string versionField;

        /// <remarks/>
        public object LoadedProfileName
        {
            get
            {
                return this.loadedProfileNameField;
            }
            set
            {
                this.loadedProfileNameField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileStorageOptions StorageOptions
        {
            get
            {
                return this.storageOptionsField;
            }
            set
            {
                this.storageOptionsField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileSelectionOptions SelectionOptions
        {
            get
            {
                return this.selectionOptionsField;
            }
            set
            {
                this.selectionOptionsField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileTranslationOptions TranslationOptions
        {
            get
            {
                return this.translationOptionsField;
            }
            set
            {
                this.translationOptionsField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileTopologyOptions TopologyOptions
        {
            get
            {
                return this.topologyOptionsField;
            }
            set
            {
                this.topologyOptionsField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileLayerOptions LayerOptions
        {
            get
            {
                return this.layerOptionsField;
            }
            set
            {
                this.layerOptionsField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileFeatureClassOptions FeatureClassOptions
        {
            get
            {
                return this.featureClassOptionsField;
            }
            set
            {
                this.featureClassOptionsField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileTableDataOptions TableDataOptions
        {
            get
            {
                return this.tableDataOptionsField;
            }
            set
            {
                this.tableDataOptionsField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileCoordSysOptions CoordSysOptions
        {
            get
            {
                return this.coordSysOptionsField;
            }
            set
            {
                this.coordSysOptionsField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileTargetNameOptions TargetNameOptions
        {
            get
            {
                return this.targetNameOptionsField;
            }
            set
            {
                this.targetNameOptionsField = value;
            }
        }

        /// <remarks/>
        public object DriverOptions
        {
            get
            {
                return this.driverOptionsField;
            }
            set
            {
                this.driverOptionsField = value;
            }
        }

        /// <remarks/>
        public byte UseUniqueKeyField
        {
            get
            {
                return this.useUniqueKeyFieldField;
            }
            set
            {
                this.useUniqueKeyFieldField = value;
            }
        }

        /// <remarks/>
        public string UseUniqueKeyFieldName
        {
            get
            {
                return this.useUniqueKeyFieldNameField;
            }
            set
            {
                this.useUniqueKeyFieldNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("NameValuePair", IsNullable = false)]
        public AdMapExportProfileNameValuePair[] ExpressionFieldMappings
        {
            get
            {
                return this.expressionFieldMappingsField;
            }
            set
            {
                this.expressionFieldMappingsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string version
        {
            get
            {
                return this.versionField;
            }
            set
            {
                this.versionField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileStorageOptions
    {

        private string storageTypeField;

        private string geometryTypeField;

        private object filePrefixField;

        /// <remarks/>
        public string StorageType
        {
            get
            {
                return this.storageTypeField;
            }
            set
            {
                this.storageTypeField = value;
            }
        }

        /// <remarks/>
        public string GeometryType
        {
            get
            {
                return this.geometryTypeField;
            }
            set
            {
                this.geometryTypeField = value;
            }
        }

        /// <remarks/>
        public object FilePrefix
        {
            get
            {
                return this.filePrefixField;
            }
            set
            {
                this.filePrefixField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileSelectionOptions
    {

        private byte useSelectionSetField;

        private byte useAutoSelectionField;

        /// <remarks/>
        public byte UseSelectionSet
        {
            get
            {
                return this.useSelectionSetField;
            }
            set
            {
                this.useSelectionSetField = value;
            }
        }

        /// <remarks/>
        public byte UseAutoSelection
        {
            get
            {
                return this.useAutoSelectionField;
            }
            set
            {
                this.useAutoSelectionField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileTranslationOptions
    {

        private byte treatClosedPolylinesAsPolygonsField;

        private byte explodeBlocksField;

        private AdMapExportProfileTranslationOptionsLayersToLevels layersToLevelsField;

        /// <remarks/>
        public byte TreatClosedPolylinesAsPolygons
        {
            get
            {
                return this.treatClosedPolylinesAsPolygonsField;
            }
            set
            {
                this.treatClosedPolylinesAsPolygonsField = value;
            }
        }

        /// <remarks/>
        public byte ExplodeBlocks
        {
            get
            {
                return this.explodeBlocksField;
            }
            set
            {
                this.explodeBlocksField = value;
            }
        }

        /// <remarks/>
        public AdMapExportProfileTranslationOptionsLayersToLevels LayersToLevels
        {
            get
            {
                return this.layersToLevelsField;
            }
            set
            {
                this.layersToLevelsField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileTranslationOptionsLayersToLevels
    {

        private byte mapLayersToLevelsField;

        private object layerToLevelMappingField;

        /// <remarks/>
        public byte MapLayersToLevels
        {
            get
            {
                return this.mapLayersToLevelsField;
            }
            set
            {
                this.mapLayersToLevelsField = value;
            }
        }

        /// <remarks/>
        public object LayerToLevelMapping
        {
            get
            {
                return this.layerToLevelMappingField;
            }
            set
            {
                this.layerToLevelMappingField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileTopologyOptions
    {

        private byte groupComplexPolygonsField;

        private object topologyNameField;

        /// <remarks/>
        public byte GroupComplexPolygons
        {
            get
            {
                return this.groupComplexPolygonsField;
            }
            set
            {
                this.groupComplexPolygonsField = value;
            }
        }

        /// <remarks/>
        public object TopologyName
        {
            get
            {
                return this.topologyNameField;
            }
            set
            {
                this.topologyNameField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileLayerOptions
    {

        private byte doFilterByLayerField;

        private object layerListField;

        /// <remarks/>
        public byte DoFilterByLayer
        {
            get
            {
                return this.doFilterByLayerField;
            }
            set
            {
                this.doFilterByLayerField = value;
            }
        }

        /// <remarks/>
        public object LayerList
        {
            get
            {
                return this.layerListField;
            }
            set
            {
                this.layerListField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileFeatureClassOptions
    {

        private byte doFilterByFeatureClassField;

        private object featureClassListField;

        /// <remarks/>
        public byte DoFilterByFeatureClass
        {
            get
            {
                return this.doFilterByFeatureClassField;
            }
            set
            {
                this.doFilterByFeatureClassField = value;
            }
        }

        /// <remarks/>
        public object FeatureClassList
        {
            get
            {
                return this.featureClassListField;
            }
            set
            {
                this.featureClassListField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileTableDataOptions
    {

        private string tableDataTypeField;

        private object nameField;

        private byte sQLKeyOnlyField;

        /// <remarks/>
        public string TableDataType
        {
            get
            {
                return this.tableDataTypeField;
            }
            set
            {
                this.tableDataTypeField = value;
            }
        }

        /// <remarks/>
        public object Name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        /// <remarks/>
        public byte SQLKeyOnly
        {
            get
            {
                return this.sQLKeyOnlyField;
            }
            set
            {
                this.sQLKeyOnlyField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileCoordSysOptions
    {

        private byte doCoordinateConversionField;

        private object coordSysNameField;

        /// <remarks/>
        public byte DoCoordinateConversion
        {
            get
            {
                return this.doCoordinateConversionField;
            }
            set
            {
                this.doCoordinateConversionField = value;
            }
        }

        /// <remarks/>
        public object CoordSysName
        {
            get
            {
                return this.coordSysNameField;
            }
            set
            {
                this.coordSysNameField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileTargetNameOptions
    {

        private string formatNameField;

        /// <remarks/>
        public string FormatName
        {
            get
            {
                return this.formatNameField;
            }
            set
            {
                this.formatNameField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class AdMapExportProfileNameValuePair
    {

        private string nameField;

        private string valueField;

        private string datatypeField;

        /// <remarks/>
        public string Name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        /// <remarks/>
        public string Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }

        /// <remarks/>
        public string Datatype
        {
            get
            {
                return this.datatypeField;
            }
            set
            {
                this.datatypeField = value;
            }
        }
    }
}
