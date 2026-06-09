LERCompareTerrain – Sammenligning af LER-rør med terrænoverflade

Hej allesammen, i dag vil jeg præsentere vores værktøj LERCompareTerrain, som sammenligner eksisterende 3D-LER-rør med en terrænflade (TIN) og fortæller dig, hvor dybt hvert rør ligger i forhold til terrænet. Frem for at klikke dig gennem længdeprofiler eller måle koter manuelt, vælger du de 3D-polylinjer, du vil undersøge, peger på en terræn-DWG, angiver en dybde-tærskel — og værktøjet farvelægger hvert rørstykke efter, om det ligger over terræn, inden for tærsklen, dybere end tærsklen, uden for fladens udbredelse, eller om polylinjen slet ikke har højdeinformation. Resultatet kan bages ud til lag-opdelte 3D-polylinjer, så det kan bruges videre i projektet — med rørenes oprindelige metadata (XData, OD, egenskabssæt) bevaret.

Hele værktøjet styres fra en palette (”Compare LER Terrain”). Forhåndsvisningen tegnes oven på rørenes egen geometri — ikke på terrænfladen.


1. Inden du starter

Før LERCompareTerrain virker, skal IntersectUtilities-plugin'et være indlæst i AutoCAD 2025 / Civil 3D 2025. Derudover skal du bruge:

- En aktiv tegning med de 3D-polylinjer (LER-rør), du vil undersøge.
- En terræn-DWG, der indeholder mindst én TIN-flade (Civil 3D TinSurface). Fladen indlæses fra en ekstern fil — terrænet behøver ikke at ligge i den aktive tegning.

Terræn-DWG'en åbnes skrivebeskyttet i baggrunden; den ændres aldrig.


2. LERCOMPARETERRAIN — åbn paletten og kør en sammenligning

Kør LERCompareTerrain. Paletten ”Compare LER Terrain” åbner.

Kerneforløbet er: vælg rør → indlæs terræn → angiv tærskel → beregn og forhåndsvis → (eventuelt) eksportér. Rækkefølgen mellem ”vælg rør” og ”indlæs terræn” er ligegyldig; begge dele skal blot være på plads, før du kan beregne.


2.1 Vælg 3D-polylinjer

Du fortæller paletten, hvilke rør der skal indgå, på én af to måder:

- Load all — indlæser alle synlige 3D-polylinjer i den aktive tegning. Polylinjer på frosne eller slukkede lag, samt usynlige objekter, springes over.
- Select — du udpeger selv polylinjerne i tegningen. Kun ægte 3D-polylinjer medtages; andre objekter i markeringen ignoreres.

Etiketten ”Selected 3D polylines: N” viser, hvor mange der aktuelt er indlæst. Kør Load all eller Select igen når som helst for at ændre udvalget — det nulstiller en eventuel eksisterende forhåndsvisning.


2.2 Indlæs en terrænflade

Klik Browse... og vælg din terræn-DWG. Værktøjet åbner filen og finder alle TIN-flader i den:

- Er der flere flader, vises de i drop-down-listen ”TIN Surface” — vælg den rigtige.
- Klik Load Surface for at indlæse den valgte flade.

Status bekræfter, hvilken flade der er indlæst. Knappen Unload Surface frigiver fladen igen (og rydder samtidig forhåndsvisningen).


2.3 Angiv tærskel (Threshold)

Feltet ”Threshold (m)” angiver dybde-tærsklen i meter (standard 2,5). Tærsklen er den grænse, hvert rørs lodrette afstand til terrænet måles op mod:

- Ligger røret tættere på terrænet end tærsklen → ”inden for tærsklen”.
- Ligger røret dybere end tærsklen → ”dybere end tærsklen”.

Tærsklen indgår også i lagnavnene ved eksport (se afsnit 3), så ”within 2.5m” / ”deeper than 2.5m” afspejler den værdi, du valgte.


2.4 Beregn og forhåndsvis

Klik Compute and Preview (eller Apply ved siden af tærskelfeltet — de gør det samme). Værktøjet analyserer hvert rør mod fladen og tegner en farvelagt forhåndsvisning oven på rørenes geometri.

Analysen er stykbaseret, ikke knudepunktbaseret: et enkelt rørsegment kan blive delt i flere stykker med hver sin farve dér, hvor røret krydser terrænfladens kant eller passerer tærsklen. Det er derfor, ét rør kan fremstå flerfarvet.

Ændrer du tærsklen, så klik Apply igen for at genberegne.


2.5 Klassifikationer og signaturforklaring

Hvert rørstykke klassificeres efter dets lodrette afstand til terrænet:

  Farve     Klassifikation              Betyder
  Violet    Above terrain               Røret ligger over terrænoverfladen
  Grøn/teal Within threshold            Røret ligger ≤ tærsklen under terræn
  Rød       Deeper than threshold       Røret ligger dybere end tærsklen under terræn
  Gul       Outside surface             Rørstykket ligger uden for terrænfladens udbredelse
  Blå       2D polyline (Z = -99)       Polylinjen har ingen reel højde (alle koter = -99)

Signaturforklaringen (”Legend”) ved siden af statusfeltet har et afkrydsningsfelt pr. klassifikation. Fjern fluebenet for at skjule den klasse i forhåndsvisningen — praktisk, hvis du f.eks. kun vil se de rør, der ligger ”deeper than threshold”. Filtreringen påvirker kun visningen, ikke selve beregningen.

Statusfeltet opsummerer optællingen: antal analyserede rør, antal stykker pr. klasse, og eventuelle ”suspect intervals” (steder, hvor flade-opslaget slog fejl midt i et spænd — typisk et hul i TIN'en).


2.6 Ryd forhåndsvisning

Clear Preview fjerner den farvelagte forhåndsvisning fra skærmen. Forhåndsvisningen ryddes også automatisk, hvis du lukker eller skjuler paletten.

▎ Forhåndsvisningen er midlertidig grafik (transients) — den ligger ikke i tegningen og kan ikke vælges eller plottes. Først ved eksport (afsnit 3) skabes der rigtige objekter.


3. Export to Civil — bag resultatet til lag-opdelte polylinjer

Når forhåndsvisningen ser rigtig ud, klik Export to Civil. Værktøjet skaber rigtige 3D-polylinjer i den aktive tegning, ét stykke pr. farvet segment, fordelt på fem lag efter klassifikation:

  Klassifikation          Lag
  Above terrain           0 - above Terrain
  Within threshold        0 - within <tærskel>m
  Deeper than threshold   0 - deeper than <tærskel>m
  Outside surface         0 - Outside segment
  2D polyline             0 - 2D polyline

Lagene oprettes automatisk med den rigtige farve, hvis de ikke findes. Hver bagt polylinje sættes til farve ”ByLayer”.

Vigtigt at vide om eksport:

- Findes der ingen forhåndsvisning, beregnes den automatisk først.
- Rørenes oprindelige metadata kopieres med over: XData, ExtensionDictionary (Xrecord-poster), Object Data (Map 3D), og egenskabssæt (property sets). Mislykkes en kopiering, fortsætter eksporten, og status melder, hvor mange stykker det gik galt for.
- Gentaget eksport rydder først al tidligere eksporteret geometri på de fem lag og bager forfra. Det er sikkert at eksportere igen efter at have justeret tærskel eller udvalg — du får ikke dubletter.

Status bekræfter, hvor mange stykker der blev skabt.


4. Begrænsninger og noter

- Kun TIN-flader. Terræn-DWG'en skal indeholde en Civil 3D TinSurface. Andre fladetyper genkendes ikke.
- 2D-polylinjer markeres, men måles ikke. En polylinje, hvor alle koter er -99, behandles som ”uden højde” og lægges samlet på 2D-laget — den sammenlignes ikke med terrænet.
- ”Outside surface” er ikke en fejl. Det betyder blot, at rørstykket plant ligger uden for terrænfladens udbredelse, så der ikke findes en kote at sammenligne med.
- Suspect intervals. Hvis terrænfladen har huller, kan analysen i sjældne tilfælde ikke afgøre et spænd entydigt. Antallet vises i status som en advarsel; resultatet bør efterses manuelt dér.
- Forhåndsvisning er midlertidig. Den forsvinder, når paletten lukkes — kun Export to Civil skaber blivende objekter.
- Tærsklen gælder hele kørslen. Alle rør måles mod den samme tærskel. Vil du sammenligne ved en anden dybde, så ændr tærsklen og kør Apply / Export igen.


5. Ekstra funktion: Forhåndsvis terræn og dybde

Ud over selve sammenligningen kan paletten vise selve terrænet og dybde-planet som hjælpegrafik. Det er ren visualisering og indgår ikke i klassifikationen — brug det til at forstå, hvad rørene sammenlignes med. Begge knapper er først aktive, når en flade er indlæst.

Preview Terrain — viser TIN-fladen som et gråt net oven på terrænkoterne.
Preview Depth — viser det samme net, men forskudt nedad svarende til tærsklen (det ”dybde-plan”, rør under tærsklen ligger under). Vises i en brun/sand farve.

Fælles for begge:

- Når du klikker, bliver du bedt om at udpege objekter, der afgrænser, hvor terrænet skal vises. Kun de fladetrekanter, der ligger inden for de valgte objekters omrids (med en lille margen), tegnes. Det holder grafikken let, selv på store flader — du behøver ikke at rendere hele terrænet.
- Knappen skifter til Hide Terrain / Hide Depth, mens grafikken er fremme. Klik igen for at skjule.
- Begge net ryddes automatisk, når paletten lukkes eller skjules.


6. Kort reference

  Handling                                   Metode
  Åbn paletten                               LERCompareTerrain
  Indlæs alle synlige 3D-polylinjer          Load all
  Udpeg rør manuelt                          Select, vælg polylinjer
  Vælg terræn-DWG                            Browse..., vælg fil
  Indlæs en TIN-flade                        Vælg i ”TIN Surface”, klik Load Surface
  Frigiv terrænfladen                        Unload Surface
  Sæt dybde-tærskel                          Skriv i ”Threshold (m)”
  Beregn og forhåndsvis                      Compute and Preview (eller Apply)
  Genberegn efter ny tærskel                 Apply
  Skjul/vis en klasse i forhåndsvisningen    Flueben i Legend
  Ryd forhåndsvisningen                      Clear Preview
  Bag resultatet til lag-opdelte polylinjer  Export to Civil
  Vis terrænet som net (ekstra)              Preview Terrain, udpeg område
  Vis dybde-planet som net (ekstra)          Preview Depth, udpeg område
