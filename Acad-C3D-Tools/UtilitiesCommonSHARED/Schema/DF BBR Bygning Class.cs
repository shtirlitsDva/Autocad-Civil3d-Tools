using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Schema.Datafordeler
{
    public class BBRBygning
    {
        public class Bygning
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public int byg007Bygningsnummer { get; set; }
            public string byg021BygningensAnvendelse { get; set; }
            public int byg026Opførelsesår { get; set; }
            public string byg032YdervæggensMateriale { get; set; }
            public string byg033Tagdækningsmateriale { get; set; }
            public string byg037KildeTilBygningensMaterialer { get; set; }
            public int byg041BebyggetAreal { get; set; }
            public string byg053BygningsarealerKilde { get; set; }
            public DateTime byg094Revisionsdato { get; set; }
            public string byg133KildeTilKoordinatsæt { get; set; }
            public string byg134KvalitetAfKoordinatsæt { get; set; }
            public string byg135SupplerendeOplysningOmKoordinatsæt { get; set; }
            public string byg136PlaceringPåSøterritorie { get; set; }
            public string byg404Koordinat { get; set; }
            public string byg406Koordinatsystem { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string grund { get; set; }
            private string _husnummer;
            public string husnummer { get => _husnummer?.ToLower() ?? null; set => _husnummer = value; }
            private string _id_lokalId;
            public string id_lokalId { get => _id_lokalId?.ToLower() ?? null; set => _id_lokalId = value; }
            public string id_namespace { get; set; }
            public string jordstykke { get; set; }
            public string kommunekode { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
            public int byg038SamletBygningsareal { get; set; }
            public int byg039BygningensSamledeBoligAreal { get; set; }
            public int KælderAreal
            {
                get
                {
                    int areal = 0;
                    if (etageList != null && etageList.Length > 0)
                    {
                        for (int i = 0; i < etageList.Length; i++)
                        {
                            Etage etage = etageList[i]?.etage;
                            if (etage == null) continue;
                            int? current = etage?.eta022Kælderareal;

                            if (current == null || current == 0) //Try other method
                                if (etage?.eta006BygningensEtagebetegnelse == "kl")
                                    current = etage?.eta020SamletArealAfEtage;

                            areal += current ?? 0;
                        }
                        return areal;
                    }
                    else return 0;
                }
            }
            public int byg054AntalEtager { get; set; }
            public string byg056Varmeinstallation { get; set; }
            public string byg057Opvarmningsmiddel { get; set; }
            public string byg058SupplerendeVarme { get; set; }
            public Etagelist[] etageList { get; set; }
            public Opganglist[] opgangList { get; set; }
            public int byg027OmTilbygningsår { get; set; }
            public int byg045ArealIndbyggetUdestueEllerLign { get; set; }
            public int byg040BygningensSamledeErhvervsAreal { get; set; }
            public int byg049ArealAfOverdækketAreal { get; set; }
            public DateTime byg122Gyldighedsdato { get; set; }
            public int byg048AndetAreal { get; set; }
            public int byg044ArealIndbyggetUdhus { get; set; }
            public string byg055AfvigendeEtager { get; set; }
            public string byg113Byggeskadeforsikringsselskab { get; set; }
            public int byg042ArealIndbyggetGarage { get; set; }
            public DateTime byg114DatoForByggeskadeforsikring { get; set; }
            public string byg121OmfattetAfByggeskadeforsikring { get; set; }
            public string byg030Vandforsyning { get; set; }
            public string byg031Afløbsforhold { get; set; }
            public Bygningpåfremmedgrundlist[] bygningPåFremmedGrundList { get; set; }
            public string byg034SupplerendeYdervæggensMateriale { get; set; }
            public string byg035SupplerendeTagdækningsMateriale { get; set; }
            public Ejerlejlighed ejerlejlighed { get; set; }
            public int byg043ArealIndbyggetCarport { get; set; }
            public int byg069Sikringsrumpladser { get; set; }
            public string byg070Fredning { get; set; }
            public int byg051Adgangsareal { get; set; }
            public int byg046SamletArealAfLukkedeOverdækningerPåBygningen { get; set; }
            public string byg036AsbestholdigtMateriale { get; set; }
            public DateTime byg029DatoForMidlertidigOpførtBygning { get; set; }
            public int byg130ArealAfUdvendigEfterisolering { get; set; }
            public int byg050ArealÅbneOverdækningerPåBygningenSamlet { get; set; }
            public int byg047ArealAfAffaldsrumITerrænniveau { get; set; }
            public string byg119Udledningstilladelse { get; set; }
            public string byg123MedlemskabAfSpildevandsforsyning { get; set; }
            public string byg052BeregningsprincipCarportAreal { get; set; }
            public DateTime byg140ServitutForUdlejningsEjendomDato { get; set; }
            public string byg128TilladelseTilAlternativBortskaffelseEllerAfledning { get; set; }
            public string byg131DispensationFritagelseIftKollektivVarmeforsyning { get; set; }
            public DateTime byg132DatoForDispensationFritagelseIftKollektivVarmeforsyning { get; set; }
        }
        public class Ejerlejlighed
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public int bfeNummer { get; set; }
            public int ejendomsnummer { get; set; }
            public string ejerlejlighed { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string kommunekode { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
            public string ejendommensEjerforholdskode { get; set; }
        }
        public class Etagelist
        {
            public string id_lokalId { get; set; }
            public Etage etage { get; set; }
        }
        public class Etage
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string bygning { get; set; }
            public string eta006BygningensEtagebetegnelse { get; set; }
            public int eta020SamletArealAfEtage { get; set; }
            public string eta025Etagetype { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string kommunekode { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
            public int eta021ArealAfUdnyttetDelAfTagetage { get; set; }
            public int eta022Kælderareal { get; set; }
            public int eta023ArealAfLovligBeboelseIKælder { get; set; }
            public int eta026ErhvervIKælder { get; set; }
        }
        public class Opganglist
        {
            public string id_lokalId { get; set; }
            public Opgang opgang { get; set; }
        }
        public class Opgang
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public string adgangFraHusnummer { get; set; }
            public string bygning { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string kommunekode { get; set; }
            public string opg020Elevator { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
        }
        public class Bygningpåfremmedgrundlist
        {
            public string id_lokalId { get; set; }
            public Bygningpåfremmedgrund bygningPåFremmedGrund { get; set; }
        }
        public class Bygningpåfremmedgrund
        {
            public DateTime datafordelerOpdateringstid { get; set; }
            public int bfeNummer { get; set; }
            public string bygningPåFremmedGrund { get; set; }
            public string ejendommensEjerforholdskode { get; set; }
            public int ejendomsnummer { get; set; }
            public string forretningshændelse { get; set; }
            public string forretningsområde { get; set; }
            public string forretningsproces { get; set; }
            public string id_lokalId { get; set; }
            public string id_namespace { get; set; }
            public string kommunekode { get; set; }
            public DateTime registreringFra { get; set; }
            public string registreringsaktør { get; set; }
            public string status { get; set; }
            public DateTime virkningFra { get; set; }
            public string virkningsaktør { get; set; }
        }
    }
}
