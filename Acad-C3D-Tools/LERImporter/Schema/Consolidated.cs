using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using System.ComponentModel;

namespace LERImporter.Schema
{

    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.opengis.net/wfs")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.opengis.net/wfs", IsNullable = false)]
    public partial class FeatureCollection
    {
        [XmlElement("featureMember", Namespace = "http://www.opengis.net/gml/3.2")]
        public List<FeatureMember> featureMember { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.8.4084.0")]
    [Serializable]
    [DesignerCategoryAttribute("code")]
    [XmlTypeAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
    public class FeatureMember
    {
        [XmlElement("Ledningstrace", typeof(LedningstraceType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Afloebskomponent", typeof(AfloebskomponentType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("AndenKomponent", typeof(AndenKomponentType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Elkomponent", typeof(ElkomponentType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Gaskomponent", typeof(GaskomponentType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Oliekomponent", typeof(OliekomponentType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Telekommunikationskomponent", typeof(TelekommunikationskomponentType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("TermiskKomponent", typeof(TermiskKomponentType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Vandkomponent", typeof(VandkomponentType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("AndenLedning", typeof(AndenLedningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Elledning", typeof(ElledningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("LedningUkendtForsyningsart", typeof(LedningUkendtForsyningsartType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Telekommunikationsledning", typeof(TelekommunikationsledningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Vandledning", typeof(VandledningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("TermiskLedning", typeof(TermiskLedningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Olieledning", typeof(OlieledningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Gasledning", typeof(GasledningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Foeringsroer", typeof(FoeringsroerType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Afloebsledning", typeof(AfloebsledningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement("Roerledning", typeof(RoerledningType), Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
        [XmlElement(typeof(Graveforesp), Namespace = "http://www.ler.dk/ler", ElementName = "Graveforesp")]
        [XmlElement(typeof(Kontaktprofil), Namespace = "http://data.gov.dk/schemas/LER/1/gml", ElementName = "Kontaktprofil")]
        [XmlElement(typeof(UtilityPackageInfo), Namespace = "http://data.gov.dk/schemas/LER/1/gml", ElementName = "UtilityPackageInfo")]
        [XmlElement(typeof(UtilityOwner), Namespace = "http://data.gov.dk/schemas/LER/1/gml", ElementName = "UtilityOwner")]
        [XmlElement(typeof(Informationsressource), Namespace = "http://data.gov.dk/schemas/LER/1/gml", ElementName = "Informationsressource")]
        public AbstractGMLType item { get; set; }
    }

    [XmlInclude(typeof(Informationsressource))]
    [XmlInclude(typeof(Graveforesp))]
    [XmlInclude(typeof(Kontaktprofil))]
    [XmlInclude(typeof(UtilityPackageInfo))]
    [XmlInclude(typeof(UtilityOwner))]
    public abstract partial class AbstractGMLType
    {
        [XmlElement("objectType", Namespace = "")]
        public string objectType { get; set; }
        [XmlElement("ledningsejer", Namespace = "")]
        public uint ledningsejer { get; set; }
        [XmlElement("indberetningsNr", Namespace = "")]
        public string indberetningsNr { get; set; }
        [PsInclude]
        public string LedningsEjersNavn { get; set; }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Informationsressource : AbstractGMLType
    {
        //[System.Xml.Serialization.XmlElementAttribute(Namespace = "http://purl.org/dc/terms/")]
        //public object title { get; set; }
        //public InformationsressourceFormat format { get; set; }
        //public string sti { get; set; }
        //public InformationsressourceGeometri geometri { get; set; }
    }

    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class InformationsressourceFormat
    {
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string type { get; set; }
        [System.Xml.Serialization.XmlTextAttribute()]
        public string Value { get; set; }
    }
    
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    public partial class InformationsressourceGeometri
    {
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public Point Point { get; set; }
    }


    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class Kontaktprofil : AbstractGMLType
    {
        public string navn { get; set; }
        public string telefonnummer { get; set; }
        public string mailadresse { get; set; }
    }

    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class UtilityPackageInfo : AbstractGMLType
    {

        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string gyldigTil { get; set; }
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string typeSupplerendeInfo { get; set; }
        //[System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        //[XmlElement(IsNullable = true)]
        //public System.DateTime forventetAfleveringstidspunkt { get; set; }
    }

    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/1/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/1/gml", IsNullable = false)]
    public partial class UtilityOwner : AbstractGMLType
    {
        public uint cvr { get; set; }
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string companyName { get; set; }
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public string folderName { get; set; }
    }

    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.ler.dk/ler")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.ler.dk/ler", IsNullable = false)]
    public partial class Graveforesp : AbstractGMLType
    {
        public byte fid { get; set; }
        public string graveart_id { get; set; }
        [System.Xml.Serialization.XmlElementAttribute(DataType = "date")]
        public System.DateTime graveperiode_fra { get; set; }
        [System.Xml.Serialization.XmlElementAttribute(DataType = "date")]
        public System.DateTime graveperiode_til { get; set; }
        public string bemaerkning { get; set; }
        public GeometryPropertyType polygonProperty { get; set; }
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "")]
        public uint orderNo { get; set; }
    }
}
