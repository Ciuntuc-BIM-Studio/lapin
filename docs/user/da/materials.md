---
title: Materialer, teksturer og skraveringer
order: 90
summary: Indstil hvert materiales farve, skraveringer og fuger, læg dit eget billede på det, og hent dine egne .pat-skraveringsmønstre ind.
---

**Administrer → Materialer** viser projektets materialer. Vælg et for at ændre det.

![Dialogen Materialer](shots/materials-dialog.webp)

## Udseende: hvordan et materiale renderes

Fanen **Udseende** bestemmer, hvordan materialet ser ud i 3D og i de renderprogrammer, du eksporterer til. Fanen **2D-grafik** beholder skraveringer og penne som før.

![Et materiales udseende med forhåndsvisning](shots/material-appearance.webp)

- **Farve** og **Tekstur**: en tegnet tekstur (mursten, træbrædder, fliser…) eller dit eget billede. **W** og **H** angiver, hvor stor én flise ligger på modellen, i meter; **Drejning** drejer den.
- **Ruhed**: 0 er et spejl, 1 er kridt. **Metalliskhed**: 1 for bart metal (anodiseret aluminium, stål, krom), 0 for alt malet eller pulverlakeret, træ og sten.
- **Gennemsigtighed**: hvor meget man ser igennem. **Glas (transmission)** og **IOR** (1,52 for glas) får glas til at bryde lyset i et renderprogram.
- **Lyser** udsender lys, som en lampes diffusor.

Kuglen viser resultatet med det samme. Det, du ikke sætter, kommer fra navnet: et materiale kaldet *glas* er klart, *stål* metallisk, *parket* og *laminat* er træbrædder. **Udseende fra navnet** fjerner dine værdier igen.

## Fra biblioteket

**Fra biblioteket…** giver færdige materialer: metaller (aluminium RAL 7016 antracit, anodiseret, stål, kobber…), glas (klart, blåt, grønt, gråt, matteret, reflekterende), træ som brædder, beton, mursten, puds og maling, fliser og sten, plast og stof. Et klik føjer et til projektet; ret det som ethvert andet.

![Materialebiblioteket](shots/material-library.webp)

## Data: beskrivelse, parametre, hvordan det måles

Fanen **Data** rummer det, materialet siger om sig selv. Du skriver det én gang; hver type materialet er i læser det: lagopbygningsetiketten, mængdeudtaget, overslaget, IFC-eksporten.

![Et materiales data: beskrivelse, parametre, hvordan det måles og katalogtykkelser](shots/material-data.webp)

- **Beskrivelse**: beskrivelsesteksten, over flere linjer (*Mineraluldsplader, λ = 0,035 W/mK, dyblet*). En lagopbygningsetiket skriver den i stedet for navnet.
- **Parametre**: et navn, en slags og en værdi, som på elementer. **Tilføj en almindelig…** tilbyder **Varmeledningsevne λ**, **Dampdiffusionsmodstand μ** og **Densitet**, som går til IFC under deres standardnavne (*Pset_MaterialThermal.ThermalConductivity*, *Pset_MaterialHygroscopic.UpperVaporResistanceFactor*, *Pset_MaterialCommon.MassDensity*, i SI-enheder), og **Reaktion på brand** og **Trykstyrke**, som forbliver dine egne. Dine egne parametre går til IFC som *Lapin_Material*.
- **Målt efter** siger, hvordan **Udførelse → Mængdeudtræk** (efter materiale) og overslaget tæller det: **areal** (m², en række pr. tykkelse: plader, isolering, membraner), **volumen** (m³: beton, grus, afretning), **masse** (kg, ud fra densiteten) eller **stk.** (plader af den størrelse du angiver). Tomt måles det som dets elementer.
- **Katalogtykkelser (mm)**: de tykkelser producenten laver det i, for eksempel *50, 80, 100, 150, 200, 240*. Lagtabellerne i **Vægtyper** og **Dæktyper** tilbyder dem ved tykkelsen (du kan stadig skrive en anden), og et lag af en anden tykkelse markeres med et advarselstegn.

IFC-eksporten skriver beskrivelsen som materialets Description og parametrene som dets egenskabssæt; importen læser dem tilbage, også fra et andet programs fil.

## Ét materiale til alle karme og alt glas

Nye døre og vinduer er lavet af fælles materialer, del for del: **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** og **Hardware**. Ret **Window frame** én gang, og alle vindueskarme ændres.

Vil du bruge andre materialer, så åbn **Arkitektur → Døre og vinduer**, vælg **Karmmateriale**, **Glasmateriale** og de andre for en type, og klik på **Brug til alle vinduer** (eller **Brug til alle døre**). Alle typer af den slags får dem, i ét fortryd-trin. Glasfacader har også et ramme- og et glasmateriale.

![Delenes materialer i dør- og vindueskonfiguratoren](shots/joinery-materials.webp)

## Dupliker, erstat, vælg

- **Dupliker** kopierer et materiale under et nyt navn.
- **Erstat med** et materiale og derefter **Erstat**: hvert lag, hver type, dør, hvert vindue og element, der brugte det første, bruger nu det andet, i ét fortryd-trin. **Erstat og slet** fjerner også det første. Ellers kan et materiale i brug ikke slettes.
- **Vælg elementer** vælger alle elementer med en del i materialet.
- **Søg i materialer** og kategorilisten filtrerer listen.

## Materialer til rendering

Twinmotion, Lumion, Enscape, Blender og andre renderprogrammer bytter materialer ud efter navn. Lapin skriver hvert materiale **én gang, under sit eget navn**, med farve, tekstur, ruhed, metalliskhed og glas, og deler hvert elements net efter materiale.

1. Navngiv dine materialer, som du vil have dem i renderprogrammet, for eksempel *Aluminium RAL 7016 anthracite* til alle karme og *Glass blue tinted* til alt glas.
2. **Filer → 3D-eksport (glTF, USD, DAE)…** og vælg **GLB** (anbefalet: én fil med teksturerne, de præcise navne og glasset), **USDZ** eller **OBJ + MTL** (en .zip med teksturerne; mellemrum i navne bliver til understreger).
3. Importér filen i renderprogrammet. Der deler alle karme ét materiale; erstat det én gang med programmets eget metal, og alle ændres.

![Et hus med mørkegrå karme og blåt glas](shots/materials-render-3d.webp)

IFC-eksporten navngiver også hvert materiales overfladestil efter det, med dets gennemsigtighed og måden, det reflekterer på.

## Hvad et materiale indstiller

- **Farve (3D)** og de flader, der tegnes på det i 3D. Lapin gætter dem ud fra navnet, for eksempel en murstens skifter eller en flises gitter. **Ender** og **Top og bund** kan se anderledes ud end fladerne.
- **Skravering** for hvert sted, det tegnes: skåret i et snit, skåret i en plan og set i en facade. Hver har sin skala, vinkel, justering, udfyldning og pen. En facade eller et snit skraverer de flader, den ser, med facadeskraveringen, hvor intet nærmere skjuler dem (Visninger, Overfladeskraveringer); indtil du giver det en, foreslår dets navn en (mursten BRICK, sten STONE, træ BOARDS, metalbeklædning VERTICAL), aldrig isoleringens BATT.
- **Kategori**, for eksempel overflade, isolering eller konstruktion. Visningsstilene bruger den til at udelade overfladerne i en konstruktionsvisning.
- **Membran** tegner et tyndt lag som en stiplet linje i stedet for et bånd.
- **Tegn fuger.** Som standard løber et materiale videre fra ét element ind i det næste uden en linje, som to vægge på linje eller puds over to etager. Sæt flueben for at tegne fugen.

## Dit eget billede på et materiale

Et materiale kan bære dit eget billede: et foto af den rigtige mursten, det rigtige træ eller den rigtige sten.

1. Vælg materialet, og klik på **Eget billede → Importér billede…**. Vælg en PNG- eller JPEG-fil.
2. Indstil **W** og **H**: hvor bred og hvor høj, i meter, én kopi af billedet er på modellen. Billedet gentages hen over fladerne.

![Et materiales eget billede, lagt på i 1.2 × 1.2 m](shots/own-image.webp)

Billedet vises i 3D-visningen og gemmes inde i projektfilen. Det kommer også med i 3D-eksporterne: **GLB**, **glTF** og **USDZ** bærer det. **DAE** og **USDA** er enkeltfiler uden plads til billeder, så de beholder materialets farve.

![Det samme hus med materialets eget billede i 3D](shots/own-image-3d.webp)

Billeder større end 2048 pixels formindskes til den størrelse, når du importerer dem. Skraldespandsknappen fjerner billedet fra materialet.

## Dine egne skraveringsmønstre (.pat)

**Importér .pat** læser en AutoCAD-fil med skraveringsmønstre. Hvert mønster i den føjes til projektet, klar til at blive valgt som et materiales skravering.

Lapin gemmer også filen under **Mine mønstre** på din computer. I ethvert andet projekt henter **Føj til dette projekt** de mønstre ind igen. Et projekt beholder de mønstre, det bruger, så det åbner ens på en anden computer.

![Mine mønstre: de .pat-filer, du har importeret, gemt til alle projekter](shots/my-patterns.webp)
