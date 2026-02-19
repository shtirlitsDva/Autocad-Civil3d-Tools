using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Schema.Datafordeler
{
    public class DARHusnummer
    {
        public class Husnummer
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string adgangsadressebetegnelse { get; set; }
            public string adgangTilBygning { get; set; }
            public Afstemningsområde afstemningsområde { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string geoDanmarkBygning { get; set; }
            public string husnummerretning { get; set; }
            public string husnummertekst { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string jordstykke { get; set; }
            public Kommuneinddeling kommuneinddeling { get; set; }
            public Menighedsrådsafstemningsområde menighedsrådsafstemningsområde { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public Sogneinddeling sogneinddeling { get; set; }
            public string status { get; set; }
            public string vejmidte { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
            public Navngivenvej navngivenVej { get; set; }
            public Adgangspunkt adgangspunkt { get; set; }
            public Vejpunkt vejpunkt { get; set; }
            public Postnummer postnummer { get; set; }
            public Supplerendebynavn supplerendeBynavn { get; set; }
            public string adgangTilTekniskAnlæg { get; set; }
            public string placeretPåForeløbigtJordstykke { get; set; }
            public string Adresse
            {
                get
                {
                    if (this.navngivenVej == null)
                    {
                        Console.WriteLine($"Missing navngivenVej: {this.id_lokalId}");
                        return "";
                    }
                    else return $"{this.navngivenVej.vejnavn} {this.husnummertekst}";
                }
            }
        }
        public class Afstemningsområde
        {
            public string id { get; set; }
            public string afstemningsområdenummer { get; set; }
            public string navn { get; set; }
        }
        public class Kommuneinddeling
        {
            public string id { get; set; }
            public string kommunekode { get; set; }
            public string navn { get; set; }
        }
        public class Menighedsrådsafstemningsområde
        {
            public string id { get; set; }
            public string mrafstemningsområdenummer { get; set; }
            public string navn { get; set; }
        }
        public class Sogneinddeling
        {
            public string id { get; set; }
            public string sognekode { get; set; }
            public string navn { get; set; }
        }
        public class Navngivenvej
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string administreresAfKommune { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public string udtaltVejnavn { get; set; }
            public string vejadresseringsnavn { get; set; }
            public string vejnavn { get; set; }
            public string vejnavnebeliggenhed_oprindelse_kilde { get; set; }
            public string vejnavnebeliggenhed_oprindelse_nøjagtighedsklasse { get; set; }
            public DateTime vejnavnebeliggenhed_oprindelse_registrering { get; set; }
            public string vejnavnebeliggenhed_oprindelse_tekniskStandard { get; set; }
            public string vejnavnebeliggenhed_vejnavnelinje { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
            public Navngivenvejkommunedellist[] navngivenVejKommunedelList { get; set; }
            public string vejnavnebeliggenhed_vejtilslutningspunkter { get; set; }
        }
        public class Navngivenvejkommunedellist
        {
            public string id_lokalId { get; set; }
            public Navngivenvejkommunedel navngivenVejKommunedel { get; set; }
        }
        public class Navngivenvejkommunedel
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string kommune { get; set; }
            public string navngivenVej { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public string vejkode { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
        }
        public class Adgangspunkt
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string oprindelse_kilde { get; set; }
            public string oprindelse_nøjagtighedsklasse { get; set; }
            public DateTime oprindelse_registrering { get; set; }
            public string oprindelse_tekniskStandard { get; set; }
            public string position { get; set; }
        }
        public class Vejpunkt
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string oprindelse_kilde { get; set; }
            public string oprindelse_nøjagtighedsklasse { get; set; }
            public DateTime oprindelse_registrering { get; set; }
            public string oprindelse_tekniskStandard { get; set; }
            public string position { get; set; }
        }
        public class Postnummer
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string navn { get; set; }
            public string postnr { get; set; }
            public string postnummerinddeling { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
        }
        public class Supplerendebynavn
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string navn { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public string supplerendeBynavn { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
        }
    }
}
