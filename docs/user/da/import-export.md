---
title: Import og eksport
order: 110
summary: Udveksl modellen som IFC, hent DXF-tegninger ind, link filer, og eksportér 3D-modeller til rendering og AR.
---

Alt, hvad der går ind eller ud, ligger i menuen **Filer**.

![Menuen Filer](shots/file-menu.webp)

## IFC og Fragments

**Eksportér IFC eller Fragments…** skriver modellen som IFC. Hver væg, hvert dæk, hver dør og hvert tag er et rigtigt IFC-element med sine lag, sin type og sine egenskaber, så filen åbner som en model i andre BIM-værktøjer. Døre og vinduer siger, hvordan de åbner: hængslingen af hver dørfløj og vinduesramme, hvilken vej den slår, og rammerne, sådan som IFC definerer dem, så andre programmer tegner de samme slag.

Eksporten spørger om skemaet (**IFC4**, der læses overalt, eller **IFC4X3**), detaljen (dele som selvstændige elementer eller ét legeme pr. element), fasen og hvordan legemerne skrives. **Geometri** er **facetteret B-rep** (nøjagtig, det andre programmer oftest forventer) eller **fladesæt**: de samme flader i en meget mindre fil. **Forenkl** skriver færre trekanter, hvor et legeme er et fint net, som kanalerne og rørene i et andet programs fil, aldrig længere fra det end den **tolerance**, du skriver (2 mm til at begynde med). Legemer, Lapin selv bygger (vægge, dæk, døre), beholder deres nøjagtige flader, og forenkling ændrer kun et legemes form, aldrig dets egenskaber.

![Eksportér IFC](shots/export-ifc.webp)

**Importér IFC…** læser et andet programs IFC. Før noget kommer ind, læser Lapin filen og viser, hvad den indeholder: dens elementer efter fagområde (arkitektur, konstruktion, installationer, møbler og udstyr, grund), hvert fagområde efter art med antal, og dens etager med hvor mange elementer der står på hver. Fjern fluebenet ved det, du ikke har brug for, for eksempel installationer og møbler, når du arbejder med konstruktionen, eller behold kun de etager, du arbejder på. **Hvordan** henter elementerne genkendt som Lapin-elementer, dem alle som referencer, eller linket. Mens filen læses og importeres, viser Lapin, hvad det laver, og hvor langt det er nået; en stor fil tager et stykke tid.

![Importér IFC](shots/import-ifc.webp)

Nogle filer har også 2D ud over modellen: plansymboler, fodaftryk, skraveringer og tekster tegnet til et element, eller elementer, der kun er tegnet i 2D (linjer, tekster, symboler uden legeme). Når en fil har dem, tæller dialogen dem under **Tegnet i 2D**. Fjern fluebenet ved **elementernes egen 2D**, og planen tegner disse elementer ved deres snit, som Lapins egne; fjern det ved **elementer, der kun er tegnet i 2D**, og de bliver ude. Begge kommer ind, medmindre du fjerner fluebenet.

![Tegnet i 2D](shots/import-ifc-2d.webp)

**Oprydning og optimering** samler importens regler, de samme for enhver fil, husket som dine egne til næste import. **Behold et gentaget legeme én gang** (slået til, medmindre du fjerner fluebenet) beholder et legeme, som filen gentager under andre navne (en bolt, en flange, et beslag), én gang; dets elementer deler det, intet går tabt. De andre udelader, når de er slået til, hvad et andet program har taget med: **flade legemer** (tekster, linjer og skraveringer gjort til trekanter), **elementer længere fra resten** end den afstand, du skriver (en tegnings tekster, et tegningshoved), **dubletter** samme sted og **porte og huller skrevet som objekter**. **Forenkl net** skriver færre trekanter inden for den tolerance, du skriver; **læs tal skrevet som tekst** gør `DoubleLength:0,01` til 0,01 m og `1.15395 kg` til en masse (et helt tal alene, en kode, forbliver tekst). Under **Hvad skal med** viser **Egenskabssæt** filens sæt med hvor mange elementer, hvert har; de **tekniske** (Navisworks’ transformation, interne id’er, geometristatistik) bliver ude, medmindre du sætter flueben ved dem. Importens besked fortæller, hvad hver regel gjorde.

![Oprydning og optimering](shots/import-ifc-rules.webp)

Det, filen placerer, kommer ind som én gruppe opkaldt efter den, som du kan åbne og rydde op i, eller løst, hvis du fjerner fluebenet ved **Som én gruppe** (se [Grupper og oprydning](groups.md)).

**Alt redigerbart**, et valg under **Hvordan**, går videre end genkendelsen: det, der ellers ville forblive en reference, bliver et Studio-legeme af sine egne trekanter, det samme element med sin klasse, sine egenskaber, sin etage og sin gruppe, som skub/træk, afrundinger og boolske operationer virker på med det samme (se [Studio](studio.md)). Et legeme med over 5.000 trekanter, for eksempel en fint meshet pumpe eller et fitting, forbliver en reference, og importens besked siger hvor mange og hvorfor. **Importér Fragments…** har samme **Hvordan**: en Fragments-fil indeholder kun trekanter, men dens vægge (med døre og vinduer), dæk og søjler genkendes ud fra dem som Lapin-elementer, resten forbliver referencer eller bliver med **Alt redigerbart** til Studio-legemer. Elementer i en IFC-fil, som genkendelsen ikke kunne læse fra filen (en væg med åbningerne skåret ind i legemet), forsøges også ud fra deres trekanter. **Importér mesh** spørger **Som: redigerbare**. Referencer, der er importeret før, konverteres med Redigér → **Konvertér til native** (NV): genkendt hvor det kan, resten som Studio-legemer; en valgt gruppe står for sine medlemmer. En reference, der er importeret før, gøres redigerbar én ad gangen med Studio → **Gør til Studio-legeme**, som beholder den i sin gruppe og på sin etage.

Lapin genkender vægge, dæk, søjler, bjælker, fundamenter, døre og vinduer og gør dem til Lapin-elementer, du kan redigere. Det, det ikke kan genkende, kommer ind som en reference, tegnet men ikke redigerbar. Døre og vinduer kommer ind og åbner, som filen siger: deres hængsling, den side de åbner mod og, for vinduer, hvordan hver ramme åbner.

Filens rum (fra Revit, ArchiCAD og andre) kommer ind som Lapin-rum, hvert på sit eget niveau, med deres omrids, nummer, navn, højde, funktion, afdeling og gulvbelægning: se [Rum og rumfarver](rooms.md#rum-fra-en-ifc-fil). Alt, hvad et importeret element havde i filen (dets egenskabssæt og mængder), bevares og vises under **IFC-egenskaber** i dets egenskaber. En reference forlader **Eksportér IFC…**, som den kom ind: med samme GlobalId, klasse og attributter (beskrivelse, mærke, foruddefineret type), dens egenskabs- og mængdesæt med hver værdi af den type, filen gav den, og dens type, materiale, porte, forbindelser og system, hver med sit GlobalId. Henter du kun installationerne fra en fil og eksporterer dem, har den nye fil dem hele.

**Importér Fragments…** åbner det samme panel for en `.frag`-fil fra That Open: dens elementer efter fagområde og type, dens etager og **Som én gruppe**. En fil eksporteret fra netop dette projekt bringer intet nyt, når den importeres tilbage: dens elementer er her allerede.

![Importér Fragments](shots/import-frag.webp)

**Importér Fragments…** henter en `.frag`-fil ind: hvert element med geometri bliver en reference tegnet fra sin krop, i den klasse filen nævner, på den etage filen sætter det på (matchet på navn med dine, oprettet når den mangler), med filens attributter under **IFC-egenskaber**. Elementer med samme krop (samme form sat mange steder) deler den. Importeres samme fil igen, opdateres det, den bragte før. Intet genkendes som væg eller dæk: en `.frag` rummer trekanter.

**Eksportér IFC eller Fragments…** skriver modellen som IFC eller som Fragments, valgt som en import: elementerne efter fagområde og type, etagerne, fasen og kun det, 3D-visningen viser. IFC går ud i IFC4 eller IFC4X3, et lagdelt elements dele under det eller ét legeme hver; Fragments fra projektets basispunkt, det interne origo eller opmålingspunktet.

![Eksportér IFC eller Fragments](shots/export-exchange.webp)

## DXF og DWG

**Importér DXF…** henter en CAD-tegning ind i visningen forrest, med dens lag, skraveringer, tekster og mål. Den kommer ind som tegning, du kan fange til og redigere. Vil du bruge en DWG, så gem den først som DXF fra dit CAD-program (AutoCAD, BricsCAD eller DraftSight: **Save As → DXF**). Import af PDF er endnu ikke tilgængelig. I en plan lægger **Placeret: efter kortets koordinater** en tegning i rigtige koordinater (en matrikelplan i Stereo 70) der, hvor kortet placerer den (se [Grunden og dens data](site.md)).

## Links

**Tilknyt IFC…** holder en anden fil linket i stedet for at importere den. Den er skrivebeskyttet og genindlæses, når den ændres. Det virker også for en DXF. **Administrer → Links** viser, genindlæser, konverterer eller fjerner linkene.

## 3D-modeller

**3D-eksport (glTF, USD, DAE)…** skriver modellen som flader til rendering, visualisering og udvidet virkelighed (AR):

| Format | Til |
|---|---|
| GLB, glTF | Blender, three.js, webfremvisere. Dine materialers egne billeder kommer med. |
| USDZ | Apples AR Quick Look, Reality Composer, Omniverse. Billeder kommer med. |
| USDA | OpenUSD som tekst, én fil. |
| DAE | COLLADA, til SketchUp og ældre værktøjer. |

**Importér mesh (OBJ, STL, glTF)…** henter et mesh fra et andet 3D-program (OBJ, STL, glTF eller GLB) ind som referencer på den aktive etage. glTF siger sin enhed og hvilken akse der vender op; for OBJ og STL spørger Lapin. **Gør til Studio-legeme** i Studio gør en reference redigerbar (se [Studio](studio.md)).

![Eksportér 3D](shots/export-3d.webp)

Du vælger, hvad der skal eksporteres, hvilken fase og origo. **Hvad** er **alt**, eller modellen **som visningen viser det**: 3D-visningen forrest, uden dens skjulte elementer og kategorier og de etager, den ikke viser, med de lag den viser (uden overflader, eller kun kernerne). Når den visning er skåret (snittet over de viste etager, et snit vist i 3D, snitboksen), skærer **Snit** også eksporten der og lukker hvert snit i sine materialer, så filen ser ud som skærmen. En 3D-visning, der udelader noget, eksporteres, som den viser det, medmindre du vælger andet. Hvert element beholder sin IFC-klasse, type og GlobalId. Vil du udveksle selve modellen frem for dens flader, så brug **Eksportér IFC…**.
