namespace IntersectUtilities.MPE.AutoAmkB;

// The rules AUTOAMKB uses when X:\AutoCAD DRI - 01 Civil 3D\Conf\AutoAmkB.csv does not exist yet.
// This text is a copy of AutoAmkB.csv next to this file, generated from it; change the .csv and
// regenerate this file so the shipped file and the fallback never disagree.
internal static class AmkDefaultRules
{
    public const string Csv =
"""
Nøgle;Værdi;Note
#
# =====================================================================================
#   AUTOAMKB REGLER
# =====================================================================================
#   Kopiér denne fil til X:\AutoCAD DRI - 01 Civil 3D\Conf\AutoAmkB.csv og ret værdierne der.
#   Hver linje er nøgle, værdi og note, adskilt af semikolon. Mellemrum omkring semikolon betyder ikke noget.
#   Decimaltal kan skrives med komma eller punktum.
#   Linjer der starter med # er kommentarer, og tomme linjer springes over.
#   Ændringer gælder fra næste kørsel af AUTOAMKB.
# =====================================================================================


# -------------------------------------------------------------------------------------
#   GENERELT
# -------------------------------------------------------------------------------------
Prøveafstand           ; 0,1       ; m mellem kontrolpunkter langs alignment
Punktsammenlægning     ; 1,0       ; m. Punktfund af samme slags på samme alignment tættere end dette bliver én række. 0 = ingen sammenlægning
Strækningsmellemrum    ; 1,0       ; m. Strækninger med et mellemrum kortere end dette bliver én strækning. Skal være større end prøveafstanden (0,1 m)


# -------------------------------------------------------------------------------------
#   LEDNINGER FRA LER
#   Kun ledninger i drift med kendt spænding eller diameter
# -------------------------------------------------------------------------------------
El.MinSpænding         ; 10        ; kV
Gas.MinDiameter        ; 100       ; mm udvendig diameter
Vand.MinDiameter       ; 100       ; mm udvendig diameter

El.LangsMed            ; Nej       ; Ja = kabler der løber langs renden medtages også som strækninger
Gas.LangsMed           ; Nej       ; Ja = gasledninger der løber langs renden medtages også som strækninger (parkeret: afstanden er ikke afklaret)
Vand.LangsMed          ; Nej       ; Ja = vandledninger der løber langs renden medtages også som strækninger
LangsMed.Afstand       ; 1,0       ; m fra rendekant
LangsMed.MinLængde     ; 10        ; m. Kortere strækninger medtages ikke


# -------------------------------------------------------------------------------------
#   DYB UDGRAVNING
# -------------------------------------------------------------------------------------
Dybde.Grænse           ; 2,5       ; m fra terræn til underkant udgravning
Dybde.Underlag         ; 0,10      ; m fra BUND (underkant rør) til underkant udgravning


# -------------------------------------------------------------------------------------
#   SMAL VEJ
# -------------------------------------------------------------------------------------
SmalVej.MinFriBredde   ; 3,0       ; m fri vejbredde på trafiksiden efter udgravning og afspærring
SmalVej.Afspærring     ; 0,60      ; m (B) ud for rendekanten på trafiksiden: montagehul ved svejsninger og komponenter. Konstant langs hele tracéet indtil videre
SmalVej.Søgeafstand    ; 15        ; m vinkelret på alignment til hver side. Uden vejkant på begge sider springes stationen over
SmalVej.MinLængde      ; 2         ; m. Kortere smal vej-strækninger medtages ikke. Fjerner korte udslag lige under grænsen. 0 = alle medtages
SmalVej.Lag            ; Vejkant   ; lag i grundkortet


# -------------------------------------------------------------------------------------
#   JORDFORURENING
# -------------------------------------------------------------------------------------
Jord.Margin            ; 0         ; m ekstra på hver side af renden
Jord.LagV1             ; DKJORD_V1 ; lag i DKjord-tegningen
Jord.LagV2             ; DKJORD_V2 ; lag i DKjord-tegningen


# -------------------------------------------------------------------------------------
#   VENTILER TIL JOURNALEN
# -------------------------------------------------------------------------------------
Ventil.Typer           ; PræisoleretVentil,PræventilMedUdluftning ; typer fra FJV Dynamiske Komponenter. Engangsventil er udeladt
Ventil.Sammenlægning   ; 1,0       ; m. Ventiler tættere end dette er én placering (fx frem og retur)


# =====================================================================================
#   TEKSTER TIL LOGBOGEN
# =====================================================================================
#   {pladsholdere} udfyldes af værktøjet.
#   Hoved- og underkategori skal stå præcis som i skabelonens lister.
#   Emne, Beskrivelse og kategorierne bruges i logbog pr. fund,
#   KriterieEmne og KriterieBeskrivelse i logbog pr. kriterie.
# =====================================================================================

# --- Kabel ≥ 10 kV, krydsning --------------------------------------------------------
EL_KRYDS.Emne                   ; Krydsning af {kV} kV kabel
EL_KRYDS.Beskrivelse            ; {type} {kV} kV, {ejer}
EL_KRYDS.Hovedkategori          ; 4. Arbejde i nærheden af ledninger
EL_KRYDS.Underkategori          ; 4.2 Kabler i jord som lav- og højspænding
EL_KRYDS.KriterieEmne           ; Krydsning af kabler ≥ 10 kV
EL_KRYDS.KriterieBeskrivelse    ; {antal} fund: {stationer}

# --- Kabel ≥ 10 kV, langs med --------------------------------------------------------
EL_LANGS.Emne                   ; Arbejde langs med {kV} kV kabel
EL_LANGS.Beskrivelse            ; {type} {kV} kV, {ejer}, {længde} m
EL_LANGS.Hovedkategori          ; 4. Arbejde i nærheden af ledninger
EL_LANGS.Underkategori          ; 4.2 Kabler i jord som lav- og højspænding
EL_LANGS.KriterieEmne           ; Arbejde langs med kabler ≥ 10 kV
EL_LANGS.KriterieBeskrivelse    ; {antal} strækninger: {stationer}

# --- Gasledning, krydsning -----------------------------------------------------------
GAS_KRYDS.Emne                  ; Krydsning af større gasledning
GAS_KRYDS.Beskrivelse           ; Gasledning Ø{Ø} ({type}), {ejer}
GAS_KRYDS.Hovedkategori         ; 4. Arbejde i nærheden af ledninger
GAS_KRYDS.Underkategori         ; 4.3 Gasledninger
GAS_KRYDS.KriterieEmne          ; Krydsning af gasledninger ≥ Ø100
GAS_KRYDS.KriterieBeskrivelse   ; {antal} fund: {stationer}

# --- Gasledning, langs med -----------------------------------------------------------
GAS_LANGS.Emne                  ; Arbejde langs med større gasledning
GAS_LANGS.Beskrivelse           ; Gasledning Ø{Ø} ({type}), {ejer}, {længde} m
GAS_LANGS.Hovedkategori         ; 4. Arbejde i nærheden af ledninger
GAS_LANGS.Underkategori         ; 4.3 Gasledninger
GAS_LANGS.KriterieEmne          ; Arbejde langs med gasledninger ≥ Ø100
GAS_LANGS.KriterieBeskrivelse   ; {antal} strækninger: {stationer}

# --- Vandledning, krydsning ----------------------------------------------------------
VAND_KRYDS.Emne                 ; Krydsning af større vandledning
VAND_KRYDS.Beskrivelse          ; Vandledning Ø{Ø}, {ejer}
VAND_KRYDS.Hovedkategori        ; 4. Arbejde i nærheden af ledninger
VAND_KRYDS.Underkategori        ; 4.7 Vandledninger
VAND_KRYDS.KriterieEmne         ; Krydsning af vandledninger ≥ Ø100
VAND_KRYDS.KriterieBeskrivelse  ; {antal} fund: {stationer}

# --- Vandledning, langs med ----------------------------------------------------------
VAND_LANGS.Emne                 ; Arbejde langs med større vandledning
VAND_LANGS.Beskrivelse          ; Vandledning Ø{Ø}, {ejer}, {længde} m
VAND_LANGS.Hovedkategori        ; 4. Arbejde i nærheden af ledninger
VAND_LANGS.Underkategori        ; 4.7 Vandledninger
VAND_LANGS.KriterieEmne         ; Arbejde langs med vandledninger ≥ Ø100
VAND_LANGS.KriterieBeskrivelse  ; {antal} strækninger: {stationer}

# --- Dyb udgravning ------------------------------------------------------------------
DYBDE.Emne                      ; Dyb udgravning
DYBDE.Beskrivelse               ; Udgravning til {maks} m under terræn
DYBDE.Hovedkategori             ; 1. Arbejde, der indebærer særlig alvorlig risiko for at blive begravet, at synke ned eller at styrte ned
DYBDE.Underkategori             ; 1.2 Arbejde i smalle eller dybe udgravninger
DYBDE.KriterieEmne              ; Udgravning dybere end 2,5 m
DYBDE.KriterieBeskrivelse       ; {antal} strækninger: {stationer}

# --- Smal vej ------------------------------------------------------------------------
SMALVEJ.Emne                    ; Smal vej
SMALVEJ.Beskrivelse             ; Fri vejbredde ned til {min} m ved udgravning inkl. afspærring
SMALVEJ.Hovedkategori           ; 11. Arbejde nær trafik
SMALVEJ.Underkategori           ; 11.1 Beskyttelse mod påkørsel
SMALVEJ.KriterieEmne            ; Smalle veje (under 3 m fri vejbredde)
SMALVEJ.KriterieBeskrivelse     ; {antal} strækninger: {stationer}

# --- Jordforurening V1/V2 ------------------------------------------------------------
JORD.Emne                       ; Forurenet jord ({klasse})
JORD.Beskrivelse                ; Ledningstrace i areal kortlagt som {klasse}
JORD.Hovedkategori              ; 2. Arbejde, som udsætter arbejdstagerne for risikofyldte kemiske eller biologiske stoffer og materialer
JORD.Underkategori              ; 2.1 Forurenet jord
JORD.KriterieEmne               ; Jordforurening V1 og V2
JORD.KriterieBeskrivelse        ; {antal} strækninger: {stationer}


# =====================================================================================
#   TEKSTER TIL JOURNALEN
#   Tre rækker pr. ventilplacering, én pr. underkategori
# =====================================================================================
VENTIL.Hovedkategori            ; 13. Fjernvarme
VENTIL.Underkategori1           ; 13.1 Afspærringsventiler i jord
VENTIL.Underkategori2           ; 13.2 Dæksel til afspærringsventiler i jord
VENTIL.Underkategori3           ; 13.3 Afspærring af ventiler i jord
VENTIL.Beskrivelse              ; {betegnelse} DN{dn} {system}
""";
}
