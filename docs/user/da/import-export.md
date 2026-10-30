---
title: Import og eksport
order: 110
summary: Udveksl modellen som IFC, hent DXF-tegninger ind, link filer, og eksportér 3D-modeller til rendering og AR.
---

Alt, hvad der går ind eller ud, ligger i menuen **Filer**.

![Menuen Filer](shots/file-menu.webp)

## IFC

**Eksportér IFC…** skriver modellen som IFC. Hver væg, hvert dæk, hver dør og hvert tag er et rigtigt IFC-element med sine lag, sin type og sine egenskaber, så filen åbner som en model i andre BIM-værktøjer. Døre og vinduer siger, hvordan de åbner: hængslingen af hver dørfløj og vinduesramme, hvilken vej den slår, og rammerne, sådan som IFC definerer dem, så andre programmer tegner de samme slag.

Eksporten spørger om skemaet (**IFC4**, der læses overalt, eller **IFC4X3**), detaljen (dele som selvstændige elementer eller ét legeme pr. element), fasen og hvordan legemerne skrives. **Geometri** er **facetteret B-rep** (nøjagtig, det andre programmer oftest forventer) eller **fladesæt**: de samme flader i en meget mindre fil. **Forenkl** skriver færre trekanter, hvor et legeme er et fint net, som kanalerne og rørene i et andet programs fil, aldrig længere fra det end den **tolerance**, du skriver (2 mm til at begynde med). Legemer, Lapin selv bygger (vægge, dæk, døre), beholder deres nøjagtige flader, og forenkling ændrer kun et legemes form, aldrig dets egenskaber.

![Eksportér IFC](shots/export-ifc.webp)

**Importér IFC…** læser et andet programs IFC. Før noget kommer ind, læser Lapin filen og viser, hvad den indeholder: dens elementer efter fagområde (arkitektur, konstruktion, installationer, møbler og udstyr, grund), hvert fagområde efter art med antal, og dens etager med hvor mange elementer der står på hver. Fjern fluebenet ved det, du ikke har brug for, for eksempel installationer og møbler, når du arbejder med konstruktionen, eller behold kun de etager, du arbejder på. **Hvordan** henter elementerne genkendt som Lapin-elementer, dem alle som referencer, eller linket. Mens filen læses og importeres, viser Lapin, hvad det laver, og hvor langt det er nået; en stor fil tager et stykke tid.

![Importér IFC](shots/import-ifc.webp)

Lapin genkender vægge, dæk, søjler, bjælker, fundamenter, døre og vinduer og gør dem til Lapin-elementer, du kan redigere. Det, det ikke kan genkende, kommer ind som en reference, tegnet men ikke redigerbar. Døre og vinduer kommer ind og åbner, som filen siger: deres hængsling, den side de åbner mod og, for vinduer, hvordan hver ramme åbner.

Filens rum (fra Revit, ArchiCAD og andre) kommer ind som Lapin-rum, hvert på sit eget niveau, med deres omrids, nummer, navn, højde, funktion, afdeling og gulvbelægning: se [Rum og rumfarver](rooms.md#rum-fra-en-ifc-fil). Alt, hvad et importeret element havde i filen (dets egenskabssæt og mængder), bevares og vises under **IFC-egenskaber** i dets egenskaber. En reference forlader **Eksportér IFC…**, som den kom ind: med samme GlobalId, klasse og attributter (beskrivelse, mærke, foruddefineret type), dens egenskabs- og mængdesæt med hver værdi af den type, filen gav den, og dens type, materiale, porte, forbindelser og system, hver med sit GlobalId. Henter du kun installationerne fra en fil og eksporterer dem, har den nye fil dem hele.

## DXF og DWG

**Importér DXF…** henter en CAD-tegning ind i visningen forrest, med dens lag, skraveringer, tekster og mål. Den kommer ind som tegning, du kan fange til og redigere. Vil du bruge en DWG, så gem den først som DXF fra dit CAD-program (AutoCAD, BricsCAD eller DraftSight: **Save As → DXF**). Import af PDF er endnu ikke tilgængelig. I en plan lægger **Placeret: efter kortets koordinater** en tegning i rigtige koordinater (en matrikelplan i Stereo 70) der, hvor kortet placerer den (se [Grunden og dens data](site.md)).

## Links

**Tilknyt IFC…** holder en anden fil linket i stedet for at importere den. Den er skrivebeskyttet og genindlæses, når den ændres. Det virker også for en DXF. **Administrer → Links** viser, genindlæser, konverterer eller fjerner linkene.

## 3D-modeller

**Eksportér 3D…** skriver modellen som flader til rendering, visualisering og udvidet virkelighed (AR):

| Format | Til |
|---|---|
| GLB, glTF | Blender, three.js, webfremvisere. Dine materialers egne billeder kommer med. |
| USDZ | Apples AR Quick Look, Reality Composer, Omniverse. Billeder kommer med. |
| USDA | OpenUSD som tekst, én fil. |
| DAE | COLLADA, til SketchUp og ældre værktøjer. |
| Fragments | `.frag`, That Open Engines format, til BIM-webvisere bygget på den (That Open Components, Bubbles). Én fil; ens kroppe gemmes én gang. |

**Importér Fragments…** henter en `.frag`-fil ind: hvert element med geometri bliver en reference tegnet fra sin krop, i den klasse filen nævner, på den etage filen sætter det på (matchet på navn med dine, oprettet når den mangler), med filens attributter under **IFC-egenskaber**. Elementer med samme krop (samme form sat mange steder) deler den. Importeres samme fil igen, opdateres det, den bragte før. Intet genkendes som væg eller dæk: en `.frag` rummer trekanter.

![Eksportér 3D](shots/export-3d.webp)

Du vælger, hvad der skal eksporteres, hvilken fase og origo. **Hvad** er **alt**, eller modellen **som visningen viser det**: 3D-visningen forrest, uden dens skjulte elementer og kategorier og de etager, den ikke viser, med de lag den viser (uden overflader, eller kun kernerne). Når den visning er skåret (snittet over de viste etager, et snit vist i 3D, snitboksen), skærer **Snit** også eksporten der og lukker hvert snit i sine materialer, så filen ser ud som skærmen. En 3D-visning, der udelader noget, eksporteres, som den viser det, medmindre du vælger andet. Hvert element beholder sin IFC-klasse, type og GlobalId. Vil du udveksle selve modellen frem for dens flader, så brug **Eksportér IFC…**.
