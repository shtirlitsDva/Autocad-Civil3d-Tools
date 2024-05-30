using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Schema.Datafordeler
{
    public class BBREnhed
    {
        public class Enhed
        {
            public string adresseIdentificerer { get; set; }
            public string bygning { get; set; }
            public DateTime datafordelerOpdateringstid { get; set; }
            public string enh020EnhedensAnvendelse { get; set; }
            public string enh023Boligtype { get; set; }
            public string enh024KondemneretBoligenhed { get; set; }
            public DateTime enh025OprettelsesdatoForEnhedensIdentifikation { get; set; }
            public int enh026EnhedensSamledeAreal { get; set; }
            public int enh027ArealTilBeboelse { get; set; }
            public string enh030KildeTilEnhedensArealer { get; set; }
            public int enh031AntalVærelser { get; set; }
            public string enh032Toiletforhold { get; set; }
            public string enh033Badeforhold { get; set; }
            public string enh034Køkkenforhold { get; set; }
            public string enh035Energiforsyning { get; set; }
            public string enh045Udlejningsforhold { get; set; }
            public string enh048GodkendtTomBolig { get; set; }
            public int enh065AntalVandskylledeToiletter { get; set; }
            public int enh066AntalBadeværelser { get; set; }
            public string etage { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string kommunekode { get; set; }
            public string opgang { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
            public string enh071AdresseFunktion { get; set; }
            public string enh041LovligAnvendelse { get; set; }
            public Ejerlejlighedlist[] ejerlejlighedList { get; set; }
            public string enh051Varmeinstallation { get; set; }
            public string enh052Opvarmningsmiddel { get; set; }
            public int enh063AntalVærelserTilErhverv { get; set; }
            public int enh028ArealTilErhverv { get; set; }
            public int enh060EnhedensAndelFællesAdgangsareal { get; set; }
            public DateTime enh044DatoForDelvisIbrugtagningsTilladelse { get; set; }
            public int enh070ÅbenAltanTagterrasseAreal { get; set; }
            public int enh062ArealAfLukketOverdækningUdestue { get; set; }
            public string enh046OffentligStøtte { get; set; }
            public string enh068FlexboligTilladelsesart { get; set; }
            public DateTime enh101Gyldighedsdato { get; set; }
            public int enh039AndetAreal { get; set; }
            public string enh053SupplerendeVarme { get; set; }
            public int enh067Støjisolering { get; set; }
            public string enh008UUIDTilModerlejlighed { get; set; }
            public DateTime enh047IndflytningDato { get; set; }
            public DateTime enh042DatoForTidsbegrænsetDispensation { get; set; }
        }

        public class Ejerlejlighedlist
        {
            public Ejerlejlighed ejerlejlighed { get; set; }
            public string id_lokalId { get; set; }
        }

        public class Ejerlejlighed
        {
            public int bfeNummer { get; set; }
            public DateTime datafordelerOpdateringstid { get; set; }
            public string ejendommensEjerforholdskode { get; set; }
            public int ejendomsnummer { get; set; }
            public string ejendomstype { get; set; }
            public string ejerlejlighed { get; set; }
            public int ejerlejlighedsnummer { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string kommunekode { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public int tinglystAreal { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
        }

    }
}
