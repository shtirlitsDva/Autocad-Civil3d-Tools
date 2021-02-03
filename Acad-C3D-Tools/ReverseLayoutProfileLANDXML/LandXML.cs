using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReverseLayoutProfileLANDXML
{
    // NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.landxml.org/schema/LandXML-1.2", IsNullable = false)]
    public partial class Civil3DLandXML
    {

        private LandXMLUnits unitsField;

        private LandXMLCoordinateSystem coordinateSystemField;

        private LandXMLProject projectField;

        private LandXMLApplication applicationField;

        private LandXMLAlignments alignmentsField;

        private System.DateTime dateField;

        private System.DateTime timeField;

        private decimal versionField;

        private string languageField;

        private bool readOnlyField;

        /// <remarks/>
        public LandXMLUnits Units
        {
            get
            {
                return this.unitsField;
            }
            set
            {
                this.unitsField = value;
            }
        }

        /// <remarks/>
        public LandXMLCoordinateSystem CoordinateSystem
        {
            get
            {
                return this.coordinateSystemField;
            }
            set
            {
                this.coordinateSystemField = value;
            }
        }

        /// <remarks/>
        public LandXMLProject Project
        {
            get
            {
                return this.projectField;
            }
            set
            {
                this.projectField = value;
            }
        }

        /// <remarks/>
        public LandXMLApplication Application
        {
            get
            {
                return this.applicationField;
            }
            set
            {
                this.applicationField = value;
            }
        }

        /// <remarks/>
        public LandXMLAlignments Alignments
        {
            get
            {
                return this.alignmentsField;
            }
            set
            {
                this.alignmentsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute(DataType = "date")]
        public System.DateTime date
        {
            get
            {
                return this.dateField;
            }
            set
            {
                this.dateField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute(DataType = "time")]
        public System.DateTime time
        {
            get
            {
                return this.timeField;
            }
            set
            {
                this.timeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public decimal version
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

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string language
        {
            get
            {
                return this.languageField;
            }
            set
            {
                this.languageField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public bool readOnly
        {
            get
            {
                return this.readOnlyField;
            }
            set
            {
                this.readOnlyField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLUnits
    {

        private LandXMLUnitsMetric metricField;

        /// <remarks/>
        public LandXMLUnitsMetric Metric
        {
            get
            {
                return this.metricField;
            }
            set
            {
                this.metricField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLUnitsMetric
    {

        private string areaUnitField;

        private string linearUnitField;

        private string volumeUnitField;

        private string temperatureUnitField;

        private string pressureUnitField;

        private string diameterUnitField;

        private string angularUnitField;

        private string directionUnitField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string areaUnit
        {
            get
            {
                return this.areaUnitField;
            }
            set
            {
                this.areaUnitField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string linearUnit
        {
            get
            {
                return this.linearUnitField;
            }
            set
            {
                this.linearUnitField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string volumeUnit
        {
            get
            {
                return this.volumeUnitField;
            }
            set
            {
                this.volumeUnitField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string temperatureUnit
        {
            get
            {
                return this.temperatureUnitField;
            }
            set
            {
                this.temperatureUnitField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string pressureUnit
        {
            get
            {
                return this.pressureUnitField;
            }
            set
            {
                this.pressureUnitField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string diameterUnit
        {
            get
            {
                return this.diameterUnitField;
            }
            set
            {
                this.diameterUnitField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string angularUnit
        {
            get
            {
                return this.angularUnitField;
            }
            set
            {
                this.angularUnitField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string directionUnit
        {
            get
            {
                return this.directionUnitField;
            }
            set
            {
                this.directionUnitField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLCoordinateSystem
    {

        private string descField;

        private ushort epsgCodeField;

        private string ogcWktCodeField;

        private string horizontalDatumField;

        private string horizontalCoordinateSystemNameField;

        private string fileLocationField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string desc
        {
            get
            {
                return this.descField;
            }
            set
            {
                this.descField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public ushort epsgCode
        {
            get
            {
                return this.epsgCodeField;
            }
            set
            {
                this.epsgCodeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string ogcWktCode
        {
            get
            {
                return this.ogcWktCodeField;
            }
            set
            {
                this.ogcWktCodeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string horizontalDatum
        {
            get
            {
                return this.horizontalDatumField;
            }
            set
            {
                this.horizontalDatumField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string horizontalCoordinateSystemName
        {
            get
            {
                return this.horizontalCoordinateSystemNameField;
            }
            set
            {
                this.horizontalCoordinateSystemNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string fileLocation
        {
            get
            {
                return this.fileLocationField;
            }
            set
            {
                this.fileLocationField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLProject
    {

        private string nameField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
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
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLApplication
    {

        private string nameField;

        private string descField;

        private string manufacturerField;

        private ushort versionField;

        private string manufacturerURLField;

        private System.DateTime timeStampField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
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
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string desc
        {
            get
            {
                return this.descField;
            }
            set
            {
                this.descField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string manufacturer
        {
            get
            {
                return this.manufacturerField;
            }
            set
            {
                this.manufacturerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public ushort version
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

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string manufacturerURL
        {
            get
            {
                return this.manufacturerURLField;
            }
            set
            {
                this.manufacturerURLField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public System.DateTime timeStamp
        {
            get
            {
                return this.timeStampField;
            }
            set
            {
                this.timeStampField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLAlignments
    {

        private LandXMLAlignmentsAlignment alignmentField;

        private string nameField;

        /// <remarks/>
        public LandXMLAlignmentsAlignment Alignment
        {
            get
            {
                return this.alignmentField;
            }
            set
            {
                this.alignmentField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
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
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLAlignmentsAlignment
    {

        private LandXMLAlignmentsAlignmentLine[] coordGeomField;

        private LandXMLAlignmentsAlignmentProfile profileField;

        private string nameField;

        private decimal lengthField;

        private decimal staStartField;

        private string descField;

        /// <remarks/>
        [System.Xml.Serialization.XmlArrayItemAttribute("Line", IsNullable = false)]
        public LandXMLAlignmentsAlignmentLine[] CoordGeom
        {
            get
            {
                return this.coordGeomField;
            }
            set
            {
                this.coordGeomField = value;
            }
        }

        /// <remarks/>
        public LandXMLAlignmentsAlignmentProfile Profile
        {
            get
            {
                return this.profileField;
            }
            set
            {
                this.profileField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
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
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public decimal length
        {
            get
            {
                return this.lengthField;
            }
            set
            {
                this.lengthField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public decimal staStart
        {
            get
            {
                return this.staStartField;
            }
            set
            {
                this.staStartField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string desc
        {
            get
            {
                return this.descField;
            }
            set
            {
                this.descField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLAlignmentsAlignmentLine
    {

        private string startField;

        private string endField;

        private decimal dirField;

        private decimal lengthField;

        /// <remarks/>
        public string Start
        {
            get
            {
                return this.startField;
            }
            set
            {
                this.startField = value;
            }
        }

        /// <remarks/>
        public string End
        {
            get
            {
                return this.endField;
            }
            set
            {
                this.endField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public decimal dir
        {
            get
            {
                return this.dirField;
            }
            set
            {
                this.dirField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public decimal length
        {
            get
            {
                return this.lengthField;
            }
            set
            {
                this.lengthField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLAlignmentsAlignmentProfile
    {

        private LandXMLAlignmentsAlignmentProfileProfAlign[] profAlignField;

        private string nameField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("ProfAlign")]
        public LandXMLAlignmentsAlignmentProfileProfAlign[] ProfAlign
        {
            get
            {
                return this.profAlignField;
            }
            set
            {
                this.profAlignField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
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
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLAlignmentsAlignmentProfileProfAlign
    {

        private object[] itemsField;

        private string nameField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("CircCurve", typeof(LandXMLAlignmentsAlignmentProfileProfAlignCircCurve))]
        [System.Xml.Serialization.XmlElementAttribute("PVI", typeof(string))]
        [System.Xml.Serialization.XmlElementAttribute("ParaCurve", typeof(LandXMLAlignmentsAlignmentProfileProfAlignParaCurve))]
        public object[] Items
        {
            get
            {
                return this.itemsField;
            }
            set
            {
                this.itemsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string name
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
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLAlignmentsAlignmentProfileProfAlignCircCurve
    {

        private decimal lengthField;

        private decimal radiusField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public decimal length
        {
            get
            {
                return this.lengthField;
            }
            set
            {
                this.lengthField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public decimal radius
        {
            get
            {
                return this.radiusField;
            }
            set
            {
                this.radiusField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
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
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.landxml.org/schema/LandXML-1.2")]
    public partial class LandXMLAlignmentsAlignmentProfileProfAlignParaCurve
    {

        private decimal lengthField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public decimal length
        {
            get
            {
                return this.lengthField;
            }
            set
            {
                this.lengthField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
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
    }
}
