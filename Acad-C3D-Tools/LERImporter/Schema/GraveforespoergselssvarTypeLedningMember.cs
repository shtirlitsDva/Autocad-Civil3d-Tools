using System;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Collections;
using System.Xml.Schema;
using System.ComponentModel;
using System.Xml;

namespace LERImporter.Schema
{
    public partial class GraveforespoergselssvarTypeLedningMember : AbstractFeatureMemberType
    {
        [XmlElement("AndenLedning", typeof(AndenLedningType))]
        [XmlElement("Elledning", typeof(ElledningType))]
        [XmlElement("LedningUkendtForsyningsart", typeof(LedningUkendtForsyningsartType))]
        [XmlElement("Telekommunikationsledning", typeof(TelekommunikationsledningType))]
        [XmlElement("Vandledning", typeof(VandledningType))]
        [XmlElement("TermiskLedning", typeof(TermiskLedningType))]
        [XmlElement("Olieledning", typeof(OlieledningType))]
        [XmlElement("Gasledning", typeof(GasledningType))]
        [XmlElement("Foeringsroer", typeof(FoeringsroerType))]
        [XmlElement("Afloebsledning", typeof(AfloebsledningType))]
        [XmlElement("Roerledning", typeof(RoerledningType))]
        public LedningType Item { get; set; }
    }
}
