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
        public List<FeatureMember> featureCollection { get; set; }
        public string GetGraveForespBemaerkning() =>
            featureCollection.Where(x => x.item is Graveforesp)
            .Select(x => x.item as Graveforesp).FirstOrDefault()?.bemaerkning;
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Xml", "4.8.4084.0")]
    [Serializable]
    [DesignerCategoryAttribute("code")]
    [XmlTypeAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
    public class FeatureMember
    {
        [XmlElement("Ledningstrace", typeof(LedningstraceType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Afloebskomponent", typeof(AfloebskomponentType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("AndenKomponent", typeof(AndenKomponentType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Elkomponent", typeof(ElkomponentType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Gaskomponent", typeof(GaskomponentType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Oliekomponent", typeof(OliekomponentType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Telekommunikationskomponent", typeof(TelekommunikationskomponentType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("TermiskKomponent", typeof(TermiskKomponentType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Vandkomponent", typeof(VandkomponentType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("AndenLedning", typeof(AndenLedningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Elledning", typeof(ElledningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("LedningUkendtForsyningsart", typeof(LedningUkendtForsyningsartType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Telekommunikationsledning", typeof(TelekommunikationsledningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Vandledning", typeof(VandledningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("TermiskLedning", typeof(TermiskLedningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Olieledning", typeof(OlieledningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Gasledning", typeof(GasledningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Foeringsroer", typeof(FoeringsroerType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Afloebsledning", typeof(AfloebsledningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement("Roerledning", typeof(RoerledningType), Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
        [XmlElement(typeof(Graveforesp), Namespace = "http://www.ler.dk/ler", ElementName = "Graveforesp")]
        [XmlElement(typeof(Kontaktprofil), Namespace = "http://data.gov.dk/schemas/LER/2/gml", ElementName = "Kontaktprofil")]
        [XmlElement(typeof(UtilityPackageInfo), Namespace = "http://data.gov.dk/schemas/LER/2/gml", ElementName = "UtilityPackageInfo")]
        [XmlElement(typeof(UtilityOwner), Namespace = "http://data.gov.dk/schemas/LER/2/gml", ElementName = "UtilityOwner")]
        [XmlElement(typeof(Informationsressource), Namespace = "http://data.gov.dk/schemas/LER/2/gml", ElementName = "Informationsressource")]
        [XmlElement(typeof(Ledningspakke), Namespace = "http://www.ler.dk/ler", ElementName = "Ledningspakke")]
        public AbstractGMLType item { get; set; }
    }

    [XmlInclude(typeof(Informationsressource))]
    [XmlInclude(typeof(Graveforesp))]
    [XmlInclude(typeof(Kontaktprofil))]
    [XmlInclude(typeof(UtilityPackageInfo))]
    [XmlInclude(typeof(UtilityOwner))]
    [XmlInclude(typeof(Ledningspakke))]
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

    [Serializable]
    [DesignerCategory("code")]
    [XmlType(AnonymousType = true, Namespace = "http://www.ler.dk/ler")]
    [XmlRoot("Ledningspakke", Namespace = "http://www.ler.dk/ler", IsNullable = false)]
    public partial class Ledningspakke : AbstractGMLType
    {
        // <oprettet_dato>2025-09-26T10:21:13.132</oprettet_dato>
        [XmlElement(DataType = "dateTime")]
        public DateTime oprettet_dato { get; set; }

        // --- aendret_dato with safe empty-element handling ---
        // Backing XML value (can be "", which XmlSerializer will accept as string)
        [XmlElement("aendret_dato")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string aendret_dato_Value
        {
            get => aendret_dato.HasValue
                ? XmlConvert.ToString(aendret_dato.Value, XmlDateTimeSerializationMode.RoundtripKind)
                : null;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    aendret_dato = null;
                }
                else
                {
                    // Try to parse as XML dateTime
                    try
                    {
                        aendret_dato = XmlConvert.ToDateTime(value, XmlDateTimeSerializationMode.RoundtripKind);
                    }
                    catch
                    {
                        // Fallback: best-effort parse without throwing
                        if (DateTime.TryParse(value, out var dt))
                            aendret_dato = dt;
                        else
                            aendret_dato = null;
                    }
                }
            }
        }

        // Your convenient property to use in code
        [XmlIgnore]
        public DateTime? aendret_dato { get; set; }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/2/gml", IsNullable = false)]
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
    public partial class InformationsressourceFormat
    {
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public string type { get; set; }
        [System.Xml.Serialization.XmlTextAttribute()]
        public string Value { get; set; }
    }
    
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
    public partial class InformationsressourceGeometri
    {
        [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.opengis.net/gml/3.2")]
        public Point Point { get; set; }
    }


    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/2/gml", IsNullable = false)]
    public partial class Kontaktprofil : AbstractGMLType
    {
        public string navn { get; set; }
        public string telefonnummer { get; set; }
        public string mailadresse { get; set; }
    }

    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/2/gml", IsNullable = false)]
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
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://data.gov.dk/schemas/LER/2/gml")]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://data.gov.dk/schemas/LER/2/gml", IsNullable = false)]
    public partial class UtilityOwner : AbstractGMLType
    {
        public int cvr { get; set; }
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
