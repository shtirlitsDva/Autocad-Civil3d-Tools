using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoSearch.Schema
{

    // NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/wfs")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.opengis.net/wfs", IsNullable = false)]
    public partial class FeatureCollection
    {

        private featureMember[] featureMemberField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("featureMember", Namespace = "http://www.opengis.net/gml/3.2")]
        public featureMember[] featureMember
        {
            get
            {
                return this.featureMemberField;
            }
            set
            {
                this.featureMemberField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class featureMember
    {

        private Vandledning vandledningField;

        private Elkomponent elkomponentField;

        private Ledningstrace ledningstraceField;

        private Informationsressource informationsressourceField;

        private Foeringsroer foeringsroerField;

        private Elledning elledningField;

        private Kontaktprofil kontaktprofilField;

        private UtilityPackageInfo utilityPackageInfoField;

        private UtilityOwner utilityOwnerField;

        private Graveforesp graveforespField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public Vandledning Vandledning
        {
            get
            {
                return this.vandledningField;
            }
            set
            {
                this.vandledningField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public Elkomponent Elkomponent
        {
            get
            {
                return this.elkomponentField;
            }
            set
            {
                this.elkomponentField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public Ledningstrace Ledningstrace
        {
            get
            {
                return this.ledningstraceField;
            }
            set
            {
                this.ledningstraceField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public Informationsressource Informationsressource
        {
            get
            {
                return this.informationsressourceField;
            }
            set
            {
                this.informationsressourceField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public Foeringsroer Foeringsroer
        {
            get
            {
                return this.foeringsroerField;
            }
            set
            {
                this.foeringsroerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public Elledning Elledning
        {
            get
            {
                return this.elledningField;
            }
            set
            {
                this.elledningField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public Kontaktprofil Kontaktprofil
        {
            get
            {
                return this.kontaktprofilField;
            }
            set
            {
                this.kontaktprofilField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public UtilityPackageInfo UtilityPackageInfo
        {
            get
            {
                return this.utilityPackageInfoField;
            }
            set
            {
                this.utilityPackageInfoField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        public UtilityOwner UtilityOwner
        {
            get
            {
                return this.utilityOwnerField;
            }
            set
            {
                this.utilityOwnerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.ler.dk/ler")]
        public Graveforesp Graveforesp
        {
            get
            {
                return this.graveforespField;
            }
            set
            {
                this.graveforespField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Vandledning
    {

        private string objectTypeField;

        private uint ledningsejerField;

        private uint indberetningsNrField;

        private object driftsstatusField;

        private VandledningEtableringstidspunkt etableringstidspunktField;

        private string fareklasseField;

        private string idField;

        private string noejagtighedsklasseField;

        private VandledningGeometri geometriField;

        private bool indeholderLedningerField;

        private bool liggerILedningField;

        private object tvaersnitsformField;

        private VandledningUdvendigBredde udvendigBreddeField;

        private VandledningUdvendigHoejde udvendigHoejdeField;

        private string id1Field;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint indberetningsNr
        {
            get
            {
                return this.indberetningsNrField;
            }
            set
            {
                this.indberetningsNrField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
        public object driftsstatus
        {
            get
            {
                return this.driftsstatusField;
            }
            set
            {
                this.driftsstatusField = value;
            }
        }

        /// <remarks/>
        public VandledningEtableringstidspunkt etableringstidspunkt
        {
            get
            {
                return this.etableringstidspunktField;
            }
            set
            {
                this.etableringstidspunktField = value;
            }
        }

        /// <remarks/>
        public string fareklasse
        {
            get
            {
                return this.fareklasseField;
            }
            set
            {
                this.fareklasseField = value;
            }
        }

        /// <remarks/>
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }

        /// <remarks/>
        public string noejagtighedsklasse
        {
            get
            {
                return this.noejagtighedsklasseField;
            }
            set
            {
                this.noejagtighedsklasseField = value;
            }
        }

        /// <remarks/>
        public VandledningGeometri geometri
        {
            get
            {
                return this.geometriField;
            }
            set
            {
                this.geometriField = value;
            }
        }

        /// <remarks/>
        public bool indeholderLedninger
        {
            get
            {
                return this.indeholderLedningerField;
            }
            set
            {
                this.indeholderLedningerField = value;
            }
        }

        /// <remarks/>
        public bool liggerILedning
        {
            get
            {
                return this.liggerILedningField;
            }
            set
            {
                this.liggerILedningField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
        public object tvaersnitsform
        {
            get
            {
                return this.tvaersnitsformField;
            }
            set
            {
                this.tvaersnitsformField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
        public VandledningUdvendigBredde udvendigBredde
        {
            get
            {
                return this.udvendigBreddeField;
            }
            set
            {
                this.udvendigBreddeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
        public VandledningUdvendigHoejde udvendigHoejde
        {
            get
            {
                return this.udvendigHoejdeField;
            }
            set
            {
                this.udvendigHoejdeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute("id", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.opengis.net/gml/3.2")]
        public string id1
        {
            get
            {
                return this.id1Field;
            }
            set
            {
                this.id1Field = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class VandledningEtableringstidspunkt
    {

        private string indeterminatePositionField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string indeterminatePosition
        {
            get
            {
                return this.indeterminatePositionField;
            }
            set
            {
                this.indeterminatePositionField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute(DataType = "gYearMonth")]
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class VandledningGeometri
    {

        private LineString lineStringField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public LineString LineString
        {
            get
            {
                return this.lineStringField;
            }
            set
            {
                this.lineStringField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class LineString
    {

        private LineStringPosList posListField;

        private string srsNameField;

        private byte srsDimensionField;

        private string idField;

        /// <remarks/>
        public LineStringPosList posList
        {
            get
            {
                return this.posListField;
            }
            set
            {
                this.posListField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string srsName
        {
            get
            {
                return this.srsNameField;
            }
            set
            {
                this.srsNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte srsDimension
        {
            get
            {
                return this.srsDimensionField;
            }
            set
            {
                this.srsDimensionField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified)]
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    public partial class LineStringPosList
    {

        private byte srsDimensionField;

        private bool srsDimensionFieldSpecified;

        private byte countField;

        private bool countFieldSpecified;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte srsDimension
        {
            get
            {
                return this.srsDimensionField;
            }
            set
            {
                this.srsDimensionField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool srsDimensionSpecified
        {
            get
            {
                return this.srsDimensionFieldSpecified;
            }
            set
            {
                this.srsDimensionFieldSpecified = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte count
        {
            get
            {
                return this.countField;
            }
            set
            {
                this.countField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool countSpecified
        {
            get
            {
                return this.countFieldSpecified;
            }
            set
            {
                this.countFieldSpecified = value;
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class VandledningUdvendigBredde
    {

        private string uomField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string uom
        {
            get
            {
                return this.uomField;
            }
            set
            {
                this.uomField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class VandledningUdvendigHoejde
    {

        private string uomField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string uom
        {
            get
            {
                return this.uomField;
            }
            set
            {
                this.uomField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Elkomponent
    {

        private string objectTypeField;

        private uint ledningsejerField;

        private uint indberetningsNrField;

        private string driftsstatusField;

        private ElkomponentEtableringstidspunkt etableringstidspunktField;

        private string fareklasseField;

        private string idField;

        private string noejagtighedsklasseField;

        private ElkomponentGeometri geometriField;

        private string niveauField;

        private string typeField;

        private ElkomponentSpaendingsniveau spaendingsniveauField;

        private string id1Field;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint indberetningsNr
        {
            get
            {
                return this.indberetningsNrField;
            }
            set
            {
                this.indberetningsNrField = value;
            }
        }

        /// <remarks/>
        public string driftsstatus
        {
            get
            {
                return this.driftsstatusField;
            }
            set
            {
                this.driftsstatusField = value;
            }
        }

        /// <remarks/>
        public ElkomponentEtableringstidspunkt etableringstidspunkt
        {
            get
            {
                return this.etableringstidspunktField;
            }
            set
            {
                this.etableringstidspunktField = value;
            }
        }

        /// <remarks/>
        public string fareklasse
        {
            get
            {
                return this.fareklasseField;
            }
            set
            {
                this.fareklasseField = value;
            }
        }

        /// <remarks/>
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }

        /// <remarks/>
        public string noejagtighedsklasse
        {
            get
            {
                return this.noejagtighedsklasseField;
            }
            set
            {
                this.noejagtighedsklasseField = value;
            }
        }

        /// <remarks/>
        public ElkomponentGeometri geometri
        {
            get
            {
                return this.geometriField;
            }
            set
            {
                this.geometriField = value;
            }
        }

        /// <remarks/>
        public string niveau
        {
            get
            {
                return this.niveauField;
            }
            set
            {
                this.niveauField = value;
            }
        }

        /// <remarks/>
        public string type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        /// <remarks/>
        public ElkomponentSpaendingsniveau spaendingsniveau
        {
            get
            {
                return this.spaendingsniveauField;
            }
            set
            {
                this.spaendingsniveauField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute("id", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.opengis.net/gml/3.2")]
        public string id1
        {
            get
            {
                return this.id1Field;
            }
            set
            {
                this.id1Field = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class ElkomponentEtableringstidspunkt
    {

        private string indeterminatePositionField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string indeterminatePosition
        {
            get
            {
                return this.indeterminatePositionField;
            }
            set
            {
                this.indeterminatePositionField = value;
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class ElkomponentGeometri
    {

        private Polygon polygonField;

        private Point pointField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public Polygon Polygon
        {
            get
            {
                return this.polygonField;
            }
            set
            {
                this.polygonField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public Point Point
        {
            get
            {
                return this.pointField;
            }
            set
            {
                this.pointField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class Polygon
    {

        private PolygonExterior exteriorField;

        private string srsNameField;

        private string idField;

        private byte srsDimensionField;

        private bool srsDimensionFieldSpecified;

        /// <remarks/>
        public PolygonExterior exterior
        {
            get
            {
                return this.exteriorField;
            }
            set
            {
                this.exteriorField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string srsName
        {
            get
            {
                return this.srsNameField;
            }
            set
            {
                this.srsNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified)]
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte srsDimension
        {
            get
            {
                return this.srsDimensionField;
            }
            set
            {
                this.srsDimensionField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool srsDimensionSpecified
        {
            get
            {
                return this.srsDimensionFieldSpecified;
            }
            set
            {
                this.srsDimensionFieldSpecified = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    public partial class PolygonExterior
    {

        private PolygonExteriorLinearRing linearRingField;

        /// <remarks/>
        public PolygonExteriorLinearRing LinearRing
        {
            get
            {
                return this.linearRingField;
            }
            set
            {
                this.linearRingField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    public partial class PolygonExteriorLinearRing
    {

        private string posListField;

        /// <remarks/>
        public string posList
        {
            get
            {
                return this.posListField;
            }
            set
            {
                this.posListField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class Point
    {

        private PointPos posField;

        private string srsNameField;

        private byte srsDimensionField;

        /// <remarks/>
        public PointPos pos
        {
            get
            {
                return this.posField;
            }
            set
            {
                this.posField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string srsName
        {
            get
            {
                return this.srsNameField;
            }
            set
            {
                this.srsNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte srsDimension
        {
            get
            {
                return this.srsDimensionField;
            }
            set
            {
                this.srsDimensionField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    public partial class PointPos
    {

        private byte srsDimensionField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte srsDimension
        {
            get
            {
                return this.srsDimensionField;
            }
            set
            {
                this.srsDimensionField = value;
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class ElkomponentSpaendingsniveau
    {

        private string uomField;

        private decimal valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string uom
        {
            get
            {
                return this.uomField;
            }
            set
            {
                this.uomField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
        public decimal Value
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Ledningstrace
    {

        private string objectTypeField;

        private uint ledningsejerField;

        private uint indberetningsNrField;

        private string driftsstatusField;

        private LedningstraceEtableringstidspunkt etableringstidspunktField;

        private string fareklasseField;

        private ushort idField;

        private string indtegningsmetodeField;

        private string noejagtighedsklasseField;

        private LedningstraceGeometri geometriField;

        private string forsyningsartField;

        private string id1Field;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint indberetningsNr
        {
            get
            {
                return this.indberetningsNrField;
            }
            set
            {
                this.indberetningsNrField = value;
            }
        }

        /// <remarks/>
        public string driftsstatus
        {
            get
            {
                return this.driftsstatusField;
            }
            set
            {
                this.driftsstatusField = value;
            }
        }

        /// <remarks/>
        public LedningstraceEtableringstidspunkt etableringstidspunkt
        {
            get
            {
                return this.etableringstidspunktField;
            }
            set
            {
                this.etableringstidspunktField = value;
            }
        }

        /// <remarks/>
        public string fareklasse
        {
            get
            {
                return this.fareklasseField;
            }
            set
            {
                this.fareklasseField = value;
            }
        }

        /// <remarks/>
        public ushort id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }

        /// <remarks/>
        public string indtegningsmetode
        {
            get
            {
                return this.indtegningsmetodeField;
            }
            set
            {
                this.indtegningsmetodeField = value;
            }
        }

        /// <remarks/>
        public string noejagtighedsklasse
        {
            get
            {
                return this.noejagtighedsklasseField;
            }
            set
            {
                this.noejagtighedsklasseField = value;
            }
        }

        /// <remarks/>
        public LedningstraceGeometri geometri
        {
            get
            {
                return this.geometriField;
            }
            set
            {
                this.geometriField = value;
            }
        }

        /// <remarks/>
        public string forsyningsart
        {
            get
            {
                return this.forsyningsartField;
            }
            set
            {
                this.forsyningsartField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute("id", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.opengis.net/gml/3.2")]
        public string id1
        {
            get
            {
                return this.id1Field;
            }
            set
            {
                this.id1Field = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class LedningstraceEtableringstidspunkt
    {

        private string indeterminatePositionField;

        private ushort valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string indeterminatePosition
        {
            get
            {
                return this.indeterminatePositionField;
            }
            set
            {
                this.indeterminatePositionField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
        public ushort Value
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class LedningstraceGeometri
    {

        private MultiCurve multiCurveField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public MultiCurve MultiCurve
        {
            get
            {
                return this.multiCurveField;
            }
            set
            {
                this.multiCurveField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.opengis.net/gml/3.2", IsNullable = false)]
    public partial class MultiCurve
    {

        private MultiCurveCurveMember curveMemberField;

        private string srsNameField;

        private byte srsDimensionField;

        /// <remarks/>
        public MultiCurveCurveMember curveMember
        {
            get
            {
                return this.curveMemberField;
            }
            set
            {
                this.curveMemberField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string srsName
        {
            get
            {
                return this.srsNameField;
            }
            set
            {
                this.srsNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte srsDimension
        {
            get
            {
                return this.srsDimensionField;
            }
            set
            {
                this.srsDimensionField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    public partial class MultiCurveCurveMember
    {

        private MultiCurveCurveMemberLineString lineStringField;

        /// <remarks/>
        public MultiCurveCurveMemberLineString LineString
        {
            get
            {
                return this.lineStringField;
            }
            set
            {
                this.lineStringField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    public partial class MultiCurveCurveMemberLineString
    {

        private MultiCurveCurveMemberLineStringPosList posListField;

        private string srsNameField;

        private byte srsDimensionField;

        /// <remarks/>
        public MultiCurveCurveMemberLineStringPosList posList
        {
            get
            {
                return this.posListField;
            }
            set
            {
                this.posListField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string srsName
        {
            get
            {
                return this.srsNameField;
            }
            set
            {
                this.srsNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte srsDimension
        {
            get
            {
                return this.srsDimensionField;
            }
            set
            {
                this.srsDimensionField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/gml/3.2")]
    public partial class MultiCurveCurveMemberLineStringPosList
    {

        private byte srsDimensionField;

        private byte countField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte srsDimension
        {
            get
            {
                return this.srsDimensionField;
            }
            set
            {
                this.srsDimensionField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte count
        {
            get
            {
                return this.countField;
            }
            set
            {
                this.countField = value;
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Informationsressource
    {

        private string objectTypeField;

        private uint ledningsejerField;

        private uint indberetningsNrField;

        private string titleField;

        private InformationsressourceFormat formatField;

        private string stiField;

        private InformationsressourceGeometri geometriField;

        private string idField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint indberetningsNr
        {
            get
            {
                return this.indberetningsNrField;
            }
            set
            {
                this.indberetningsNrField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://purl.org/dc/terms/")]
        public string title
        {
            get
            {
                return this.titleField;
            }
            set
            {
                this.titleField = value;
            }
        }

        /// <remarks/>
        public InformationsressourceFormat format
        {
            get
            {
                return this.formatField;
            }
            set
            {
                this.formatField = value;
            }
        }

        /// <remarks/>
        public string sti
        {
            get
            {
                return this.stiField;
            }
            set
            {
                this.stiField = value;
            }
        }

        /// <remarks/>
        public InformationsressourceGeometri geometri
        {
            get
            {
                return this.geometriField;
            }
            set
            {
                this.geometriField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.opengis.net/gml/3.2")]
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class InformationsressourceFormat
    {

        private string typeField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class InformationsressourceGeometri
    {

        private Point pointField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public Point Point
        {
            get
            {
                return this.pointField;
            }
            set
            {
                this.pointField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Foeringsroer
    {

        private string objectTypeField;

        private uint ledningsejerField;

        private uint indberetningsNrField;

        private string driftsstatusField;

        private FoeringsroerEtableringstidspunkt etableringstidspunktField;

        private string fareklasseField;

        private uint idField;

        private bool idFieldSpecified;

        private FoeringsroerNoejagtighedsklasse noejagtighedsklasseField;

        private FoeringsroerGeometri geometriField;

        private bool indeholderLedningerField;

        private bool liggerILedningField;

        private FoeringsroerUdvendigDiameter udvendigDiameterField;

        private string tvaersnitsformField;

        private string forsyningsartField;

        private string id1Field;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint indberetningsNr
        {
            get
            {
                return this.indberetningsNrField;
            }
            set
            {
                this.indberetningsNrField = value;
            }
        }

        /// <remarks/>
        public string driftsstatus
        {
            get
            {
                return this.driftsstatusField;
            }
            set
            {
                this.driftsstatusField = value;
            }
        }

        /// <remarks/>
        public FoeringsroerEtableringstidspunkt etableringstidspunkt
        {
            get
            {
                return this.etableringstidspunktField;
            }
            set
            {
                this.etableringstidspunktField = value;
            }
        }

        /// <remarks/>
        public string fareklasse
        {
            get
            {
                return this.fareklasseField;
            }
            set
            {
                this.fareklasseField = value;
            }
        }

        /// <remarks/>
        public uint id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool idSpecified
        {
            get
            {
                return this.idFieldSpecified;
            }
            set
            {
                this.idFieldSpecified = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
        public FoeringsroerNoejagtighedsklasse noejagtighedsklasse
        {
            get
            {
                return this.noejagtighedsklasseField;
            }
            set
            {
                this.noejagtighedsklasseField = value;
            }
        }

        /// <remarks/>
        public FoeringsroerGeometri geometri
        {
            get
            {
                return this.geometriField;
            }
            set
            {
                this.geometriField = value;
            }
        }

        /// <remarks/>
        public bool indeholderLedninger
        {
            get
            {
                return this.indeholderLedningerField;
            }
            set
            {
                this.indeholderLedningerField = value;
            }
        }

        /// <remarks/>
        public bool liggerILedning
        {
            get
            {
                return this.liggerILedningField;
            }
            set
            {
                this.liggerILedningField = value;
            }
        }

        /// <remarks/>
        public FoeringsroerUdvendigDiameter udvendigDiameter
        {
            get
            {
                return this.udvendigDiameterField;
            }
            set
            {
                this.udvendigDiameterField = value;
            }
        }

        /// <remarks/>
        public string tvaersnitsform
        {
            get
            {
                return this.tvaersnitsformField;
            }
            set
            {
                this.tvaersnitsformField = value;
            }
        }

        /// <remarks/>
        public string forsyningsart
        {
            get
            {
                return this.forsyningsartField;
            }
            set
            {
                this.forsyningsartField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute("id", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.opengis.net/gml/3.2")]
        public string id1
        {
            get
            {
                return this.id1Field;
            }
            set
            {
                this.id1Field = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class FoeringsroerEtableringstidspunkt
    {

        private string indeterminatePositionField;

        private ushort valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string indeterminatePosition
        {
            get
            {
                return this.indeterminatePositionField;
            }
            set
            {
                this.indeterminatePositionField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
        public ushort Value
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class FoeringsroerNoejagtighedsklasse
    {

        private string nilReasonField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string nilReason
        {
            get
            {
                return this.nilReasonField;
            }
            set
            {
                this.nilReasonField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class FoeringsroerGeometri
    {

        private LineString lineStringField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public LineString LineString
        {
            get
            {
                return this.lineStringField;
            }
            set
            {
                this.lineStringField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class FoeringsroerUdvendigDiameter
    {

        private string uomField;

        private byte valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string uom
        {
            get
            {
                return this.uomField;
            }
            set
            {
                this.uomField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
        public byte Value
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Elledning
    {

        private string objectTypeField;

        private uint ledningsejerField;

        private uint indberetningsNrField;

        private string driftsstatusField;

        private ElledningEtableringstidspunkt etableringstidspunktField;

        private string fareklasseField;

        private string idField;

        private string indtegningsmetodeField;

        private ElledningNoejagtighedsklasse noejagtighedsklasseField;

        private ElledningGeometri geometriField;

        private string niveauField;

        private bool indeholderLedningerField;

        private bool liggerILedningField;

        private string typeField;

        private string afdaekningField;

        private byte antalKablerField;

        private bool antalKablerFieldSpecified;

        private string kabeltypeField;

        private ElledningSpaendingsniveau spaendingsniveauField;

        private string id1Field;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint indberetningsNr
        {
            get
            {
                return this.indberetningsNrField;
            }
            set
            {
                this.indberetningsNrField = value;
            }
        }

        /// <remarks/>
        public string driftsstatus
        {
            get
            {
                return this.driftsstatusField;
            }
            set
            {
                this.driftsstatusField = value;
            }
        }

        /// <remarks/>
        public ElledningEtableringstidspunkt etableringstidspunkt
        {
            get
            {
                return this.etableringstidspunktField;
            }
            set
            {
                this.etableringstidspunktField = value;
            }
        }

        /// <remarks/>
        public string fareklasse
        {
            get
            {
                return this.fareklasseField;
            }
            set
            {
                this.fareklasseField = value;
            }
        }

        /// <remarks/>
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }

        /// <remarks/>
        public string indtegningsmetode
        {
            get
            {
                return this.indtegningsmetodeField;
            }
            set
            {
                this.indtegningsmetodeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(IsNullable = true)]
        public ElledningNoejagtighedsklasse noejagtighedsklasse
        {
            get
            {
                return this.noejagtighedsklasseField;
            }
            set
            {
                this.noejagtighedsklasseField = value;
            }
        }

        /// <remarks/>
        public ElledningGeometri geometri
        {
            get
            {
                return this.geometriField;
            }
            set
            {
                this.geometriField = value;
            }
        }

        /// <remarks/>
        public string niveau
        {
            get
            {
                return this.niveauField;
            }
            set
            {
                this.niveauField = value;
            }
        }

        /// <remarks/>
        public bool indeholderLedninger
        {
            get
            {
                return this.indeholderLedningerField;
            }
            set
            {
                this.indeholderLedningerField = value;
            }
        }

        /// <remarks/>
        public bool liggerILedning
        {
            get
            {
                return this.liggerILedningField;
            }
            set
            {
                this.liggerILedningField = value;
            }
        }

        /// <remarks/>
        public string type
        {
            get
            {
                return this.typeField;
            }
            set
            {
                this.typeField = value;
            }
        }

        /// <remarks/>
        public string afdaekning
        {
            get
            {
                return this.afdaekningField;
            }
            set
            {
                this.afdaekningField = value;
            }
        }

        /// <remarks/>
        public byte antalKabler
        {
            get
            {
                return this.antalKablerField;
            }
            set
            {
                this.antalKablerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool antalKablerSpecified
        {
            get
            {
                return this.antalKablerFieldSpecified;
            }
            set
            {
                this.antalKablerFieldSpecified = value;
            }
        }

        /// <remarks/>
        public string kabeltype
        {
            get
            {
                return this.kabeltypeField;
            }
            set
            {
                this.kabeltypeField = value;
            }
        }

        /// <remarks/>
        public ElledningSpaendingsniveau spaendingsniveau
        {
            get
            {
                return this.spaendingsniveauField;
            }
            set
            {
                this.spaendingsniveauField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute("id", Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.opengis.net/gml/3.2")]
        public string id1
        {
            get
            {
                return this.id1Field;
            }
            set
            {
                this.id1Field = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class ElledningEtableringstidspunkt
    {

        private string indeterminatePositionField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string indeterminatePosition
        {
            get
            {
                return this.indeterminatePositionField;
            }
            set
            {
                this.indeterminatePositionField = value;
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class ElledningNoejagtighedsklasse
    {

        private string nilReasonField;

        private string valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string nilReason
        {
            get
            {
                return this.nilReasonField;
            }
            set
            {
                this.nilReasonField = value;
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class ElledningGeometri
    {

        private LineString lineStringField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public LineString LineString
        {
            get
            {
                return this.lineStringField;
            }
            set
            {
                this.lineStringField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class ElledningSpaendingsniveau
    {

        private string uomField;

        private decimal valueField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string uom
        {
            get
            {
                return this.uomField;
            }
            set
            {
                this.uomField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
        public decimal Value
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Kontaktprofil
    {

        private string objectTypeField;

        private uint indberetningsNrField;

        private uint ledningsejerField;

        private string navnField;

        private string telefonnummerField;

        private string mailadresseField;

        private string idField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint indberetningsNr
        {
            get
            {
                return this.indberetningsNrField;
            }
            set
            {
                this.indberetningsNrField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        public string navn
        {
            get
            {
                return this.navnField;
            }
            set
            {
                this.navnField = value;
            }
        }

        /// <remarks/>
        public string telefonnummer
        {
            get
            {
                return this.telefonnummerField;
            }
            set
            {
                this.telefonnummerField = value;
            }
        }

        /// <remarks/>
        public string mailadresse
        {
            get
            {
                return this.mailadresseField;
            }
            set
            {
                this.mailadresseField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute(Form = System.Xml.Schema.XmlSchemaForm.Qualified, Namespace = "http://www.opengis.net/gml/3.2")]
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class UtilityPackageInfo
    {

        private string objectTypeField;

        private uint indberetningsNrField;

        private uint ledningsejerField;

        private string idField;

        private string gyldigTilField;

        private string typeSupplerendeInfoField;

        private object forventetAfleveringstidspunktField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint indberetningsNr
        {
            get
            {
                return this.indberetningsNrField;
            }
            set
            {
                this.indberetningsNrField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string id
        {
            get
            {
                return this.idField;
            }
            set
            {
                this.idField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string gyldigTil
        {
            get
            {
                return this.gyldigTilField;
            }
            set
            {
                this.gyldigTilField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string typeSupplerendeInfo
        {
            get
            {
                return this.typeSupplerendeInfoField;
            }
            set
            {
                this.typeSupplerendeInfoField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public object forventetAfleveringstidspunkt
        {
            get
            {
                return this.forventetAfleveringstidspunktField;
            }
            set
            {
                this.forventetAfleveringstidspunktField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class UtilityOwner
    {

        private string objectTypeField;

        private uint ledningsejerField;

        private uint cvrField;

        private string companyNameField;

        private string folderNameField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint ledningsejer
        {
            get
            {
                return this.ledningsejerField;
            }
            set
            {
                this.ledningsejerField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint cvr
        {
            get
            {
                return this.cvrField;
            }
            set
            {
                this.cvrField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string companyName
        {
            get
            {
                return this.companyNameField;
            }
            set
            {
                this.companyNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string folderName
        {
            get
            {
                return this.folderNameField;
            }
            set
            {
                this.folderNameField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.ler.dk/ler")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.ler.dk/ler", IsNullable = false)]
    public partial class Graveforesp
    {

        private string objectTypeField;

        private byte fidField;

        private string graveart_idField;

        private System.DateTime graveperiode_fraField;

        private System.DateTime graveperiode_tilField;

        private string bemaerkningField;

        private GraveforespPolygonProperty polygonPropertyField;

        private uint orderNoField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string objectType
        {
            get
            {
                return this.objectTypeField;
            }
            set
            {
                this.objectTypeField = value;
            }
        }

        /// <remarks/>
        public byte fid
        {
            get
            {
                return this.fidField;
            }
            set
            {
                this.fidField = value;
            }
        }

        /// <remarks/>
        public string graveart_id
        {
            get
            {
                return this.graveart_idField;
            }
            set
            {
                this.graveart_idField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "date")]
        public System.DateTime graveperiode_fra
        {
            get
            {
                return this.graveperiode_fraField;
            }
            set
            {
                this.graveperiode_fraField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(DataType = "date")]
        public System.DateTime graveperiode_til
        {
            get
            {
                return this.graveperiode_tilField;
            }
            set
            {
                this.graveperiode_tilField = value;
            }
        }

        /// <remarks/>
        public string bemaerkning
        {
            get
            {
                return this.bemaerkningField;
            }
            set
            {
                this.bemaerkningField = value;
            }
        }

        /// <remarks/>
        public GraveforespPolygonProperty polygonProperty
        {
            get
            {
                return this.polygonPropertyField;
            }
            set
            {
                this.polygonPropertyField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint orderNo
        {
            get
            {
                return this.orderNoField;
            }
            set
            {
                this.orderNoField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.ler.dk/ler")]
    public partial class GraveforespPolygonProperty
    {

        private Polygon polygonField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public Polygon Polygon
        {
            get
            {
                return this.polygonField;
            }
            set
            {
                this.polygonField = value;
            }
        }
    }


}
