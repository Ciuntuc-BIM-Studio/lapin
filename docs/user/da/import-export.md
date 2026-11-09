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

Bjælker, søjler og stålprofiler skrives ved deres parametriske profil, navngivet og med dens mekaniske værdier ([Profilbiblioteker](profiles.md)).

**Importér IFC…** læser et andet programs IFC. Før noget kommer ind, læser Lapin filen og viser, hvad den indeholder: dens elementer efter fagområde (arkitektur, konstruktion, installationer, møbler og udstyr, grund), hvert fagområde efter art med antal, og dens etager med hvor mange elementer der står på hver. Fjern fluebenet ved det, du ikke har brug for, for eksempel installationer og møbler, når du arbejder med konstruktionen, eller behold kun de etager, du arbejder på. **Hvordan** henter elementerne genkendt som Lapin-elementer, dem alle som referencer, eller linket. Mens filen læses og importeres, viser Lapin, hvad det laver, og hvor langt det er nået; en stor fil tager et stykke tid.

![Importér IFC](shots/import-ifc.webp)

Nogle filer har også 2D ud over modellen: plansymboler, fodaftryk, skraveringer og tekster tegnet til et element, eller elementer, der kun er tegnet i 2D (linjer, tekster, symboler uden legeme). Når en fil har dem, tæller dialogen dem under **Tegnet i 2D**. Fjern fluebenet ved **elementernes egen 2D**, og planen tegner disse elementer ved deres snit, som Lapins egne; fjern det ved **elementer, der kun er tegnet i 2D**, og de bliver ude. Begge kommer ind, medmindre du fjerner fluebenet.

![Tegnet i 2D](shots/import-ifc-2d.webp)

**Oprydning og optimering** samler importens regler, de samme for enhver fil, husket som dine egne til næste import. **Behold et gentaget legeme én gang** (slået til, medmindre du fjerner fluebenet) beholder et legeme, som filen gentager under andre navne (en bolt, en flange, et beslag), én gang; dets elementer deler det, intet går tabt. De andre udelader, når de er slået til, hvad et andet program har taget med: **flade legemer** (tekster, linjer og skraveringer gjort til trekanter), **elementer længere fra resten** end den afstand, du skriver (en tegnings tekster, et tegningshoved), **dubletter** samme sted og **porte og huller skrevet som objekter**. **Forenkl net** skriver færre trekanter inden for den tolerance, du skriver; **læs tal skrevet som tekst** gør `DoubleLength:0,01` til 0,01 m og `1.15395 kg` til en masse (et helt tal alene, en kode, forbliver tekst). Under **Hvad skal med** viser **Egenskabssæt** filens sæt med hvor mange elementer, hvert har; de **tekniske** (Navisworks’ transformation, interne id’er, geometristatistik) bliver ude, medmindre du sætter flueben ved dem. Importens besked fortæller, hvad hver regel gjorde.

![Oprydning og optimering](shots/import-ifc-rules.webp)

Det, filen placerer, kommer ind som én gruppe opkaldt efter den, som du kan åbne og rydde op i, eller løst, hvis du fjerner fluebenet ved **Som én gruppe** (se [Grupper og oprydning](groups.md)).

**Etager.** En imports etager er filens: de erstatter dit projekts. Hver af filens etager kommer ind med sit navn, sit GlobalId og sin kote, som filen angiver den, læst i filens enhed (millimeter eller meter). En etage i dit projekt i samme kote som en af filens (inden for 5 cm) lægges sammen med den: dens elementer bliver, hvor de er, og dens visninger, dens plan og det, der er tegnet på den, går over til filens etage, som beholder filens navn. En af dine etager, der ikke ligger i nogen af filens koter, fjernes, når den ikke rummer noget (intet element, intet tegnet på dens plan), og dens visninger går over til filens nærmeste etage; en, der rummer noget, bliver, og importens besked siger hvilken. Et nyt projekts Fundamenter, Stueetage og 1. sal giver således plads for filens egne. Det hele er ét fortrydelsestrin sammen med importen. En linket fil holder sine etager for sig selv og ændrer ingen af dine. Når importen er færdig, indrammer 3D-visningen modellen, medmindre du flyttede kameraet, mens den kom ind.

**Alt redigerbart**, et valg under **Hvordan**, går videre end genkendelsen: det, der ellers ville forblive en reference, bliver et Studio-legeme af sine egne trekanter, det samme element med sin klasse, sine egenskaber, sin etage og sin gruppe, som skub/træk, afrundinger og boolske operationer virker på med det samme (se [Studio](studio.md)). Et legeme med over 5.000 trekanter, for eksempel en fint meshet pumpe eller et fitting, forbliver en reference, og importens besked siger hvor mange og hvorfor. **Importér Fragments…** har samme **Hvordan**: en Fragments-fil indeholder kun trekanter, men dens vægge (med døre og vinduer), dæk og søjler genkendes ud fra dem som Lapin-elementer, resten forbliver referencer eller bliver med **Alt redigerbart** til Studio-legemer. Elementer i en IFC-fil, som genkendelsen ikke kunne læse fra filen (en væg med åbningerne skåret ind i legemet), forsøges også ud fra deres trekanter. **Importér mesh** spørger **Som: redigerbare**. Referencer, der er importeret før, konverteres med Redigér → **Konvertér til native** (NV): genkendt hvor det kan, resten som Studio-legemer; en valgt gruppe står for sine medlemmer. En reference, der er importeret før, gøres redigerbar én ad gangen med Studio → **Gør til Studio-legeme**, som beholder den i sin gruppe og på sin etage.

Lapin genkender vægge, dæk, søjler, bjælker, fundamenter, døre og vinduer og gør dem til Lapin-elementer, du kan redigere. Det, det ikke kan genkende, kommer ind som en reference, tegnet men ikke redigerbar. Et element bliver kun et Lapin-element, når det er filens eget: Lapin måler hvert genkendt element mod det legeme, filen giver det, og et, der afviger mere end 1 mm eller 0,5 % af sit volumen (en søjle, hvis top filen skråner, en vægende, filen hakker, som et Lapin-element ville gøre lige), kommer ind som den reference, det var; importens besked siger hvor mange og hvorfor. Døre og vinduer kommer ind og åbner, som filen siger: deres hængsling, den side de åbner mod og, for vinduer, hvordan hver ramme åbner. Et fladt tag fra et andet program kommer ind som et Lapin-tag, når dets IfcRoof består af to eller flere plader lagt oven på hinanden, eller af ét legeme med et lagsæt: den øverste plade (eller det øverste lag) er tagdækningen med sit materiale og sin tykkelse, dem under den dækket, af den tykkestes materiale; omridset er dækkets, uden udhæng, ved dækkets underside. Et skråt tag eller et tag af én plade uden lag forbliver, som det kom. Genkendte elementer får kun det, filen har: ingen overliggere, sålbænke, dørtrin eller lysninger fra Lapin, og hver åbning har filens størrelse; slå dem til i elementernes typer, hvis du vil have dem.

Overflader, som et andet program har eksporteret som selvstændige vægge (en 2,5 cm puds på facaden, en indvendig puds), bliver et lag i den væg, de dækker, når de ligger tæt på dens flade, er lige så høje som den og følger den: væggen får en type med det lag (*Masonry 350 + Lime render*), dens egne lag bliver, hvor de var, og importens besked siger hvor mange. En overflade, der står med afstand til væggen eller kun dækker en del af den, forbliver en tynd væg. En søjle, som filen giver som et legeme skåret skråt i toppen (under en skrå bjælke eller et skråt tag), kommer ind som en søjle med sit tværsnit, op til sit højeste punkt.

**Tilknyt importerede typer** (Administrér) viser de typer, en import har oprettet (væg-, dæk-, dør-, vindues-, søjle-, bjælke- og fundamenttyper), hver med sin fil og antallet af elementer. Vælg under **Bliver til** en af projektets egne typer af samme slags, og tryk **Tilknyt**: dens elementer får projektets type, en væg beholder sin kerne, hvor den var, og den importerede type forsvinder. Med **Husk** markeret får en senere import af en type af den slags og med det navn straks projektets, og dens besked siger det. De huskede valg står i dialogen, hver med **Glem**.

![Tilknyt importerede typer](shots/map-types.webp)

Filens rum (fra Revit, ArchiCAD og andre) kommer ind som Lapin-rum, hvert på sit eget niveau, med deres omrids, nummer, navn, højde, funktion, afdeling og gulvbelægning: se [Rum og rumfarver](rooms.md#rum-fra-en-ifc-fil). Alt, hvad et importeret element havde i filen (dets egenskabssæt og mængder), bevares og vises under **IFC-egenskaber** i dets egenskaber. En reference forlader **Eksportér IFC…**, som den kom ind: med samme GlobalId, klasse og attributter (beskrivelse, mærke, foruddefineret type), dens egenskabs- og mængdesæt med hver værdi af den type, filen gav den, og dens type, materiale, porte, forbindelser og system, hver med sit GlobalId. Henter du kun installationerne fra en fil og eksporterer dem, har den nye fil dem hele.

**Importér Fragments…** åbner det samme panel for en `.frag`-fil fra That Open: dens elementer efter fagområde og type, dens etager og **Som én gruppe**. En fil eksporteret fra netop dette projekt bringer intet nyt, når den importeres tilbage: dens elementer er her allerede.

![Importér Fragments](shots/import-frag.webp)

**Importér Fragments…** henter en `.frag`-fil ind: hvert element med geometri bliver en reference tegnet fra sin krop, i den klasse filen nævner, på den etage filen sætter det på (filens etager erstatter dine: se **Etager** ovenfor), med filens attributter under **IFC-egenskaber**. Elementer med samme krop (samme form sat mange steder) deler den. Importeres samme fil igen, opdateres det, den bragte før. Hvad der genkendes ud fra dens trekanter, afgør **Hvordan**, som ovenfor. Dens egenskabs- og mængdesæt, dens type og dens materialer kommer med attributterne; glas forbliver gennemsigtigt; en fil lavet af That Opens egen konverter lander, hvor dens IFC ville placere den, og dens flader med huller (en væg om sit vindue) beholder dem.

**Eksportér IFC eller Fragments…** skriver modellen som IFC eller som Fragments, valgt som en import: elementerne efter fagområde og type, etagerne, fasen og kun det, 3D-visningen viser. IFC går ud i IFC4 eller IFC4X3, et lagdelt elements dele under det eller ét legeme hver; Fragments fra projektets basispunkt, det interne origo eller opmålingspunktet. En Fragments-fil bærer det, en IFC-viewer viser: hvert elements type, dets mængder, dine parametre, dets materialer og kortkoordinaterne, sådan som That Opens konverter skriver dem.

![Eksportér IFC eller Fragments](shots/export-exchange.webp)

## JG Structures (statisk beregning)

**Eksportér JG Structures (JSON)…** skriver projektets analytiske model i udvekslingsfilen til JG Structures, et program til statisk beregning: en JSON-fil efter programmets kontrakt (version 1.0.0). Lapin udleder modellen af det, du har tegnet. Søjler, bjælker og stålprofiler bliver stænger på deres akser; bærende vægge (Pset_WallCommon.LoadBearing på væggen eller dens type) og dæk bliver skaller midt i deres bærende lag, uden overflader. De deler knuder: en bjælke, der bærer et dæk, ligger i dækkets midterplan, søjle- og vægender møder bjælken eller dækket i top og fod, et dæks hjørner flyttes til søjlerne og væggene under det, og dækket deles langs de bjælker, der krydser det. Fødderne af søjler og vægge på et fundament er indspændte; en stålsøjlefod er charniereret eller indspændt, som samlingen siger.

Vælg etagerne, elementtyperne og enhederne: kontraktens centimeter og kilopond eller SI (meter og kilonewton). Materialer tager deres værdier fra deres data (Administrer → Materialer, Pset_MaterialMechanical i pascal) eller fra styrkeklassen i navnet, C25/30 eller S355, efter Eurocodes; et tværsnits areal, inertimomenter, modstandsmomenter og vridningskonstant beregnes ud fra dets geometri. Det, Lapin ikke ved, står tomt (null) i filen, aldrig nul. Før eksporten tæller rapporten, hvad der kommer med, kontrollerer filen efter kontraktens regler og viser, hvad en beregning stadig mangler: et materiale uden klasse, en søjle, der ikke står på noget.

Hvor elementer ikke mødes på deres akser (en åse på en rigel, en vægrem på en søjle, et vindkryds på sin knudeplade, spær på en murrem), lægger Lapin knuden på det bærende element og beholder afstanden som stangens endeforskydning, så også en hals sekundære stålkonstruktion er forbundet. Vægge beholder deres døre, vinduer og åbninger, og dæk deres åbninger: da kontraktens skaller ikke har huller, deles de i stykker omkring dem, og et L-formet dæk i konvekse stykker langs bjælkerne under det. En væg under et tag går op til det, og taget eksporteres også: dets dæk som skaller, eller spær, åse og murremme i en tømmerkonstruktion som stænger i dens styrkeklasse (C24 efter EN 338). Vinkelprofiler og andre usymmetriske tværsnit angives om deres hovedakser. **Fødder på fundamenter** vælger, hvordan søjle- og vægfødder på et fundament holdes: indspændt (standard) eller charnier. En stålsøjlefod beholder sin egen samling.

![Eksportér JG Structures](shots/export-jg.webp)

## Optimér en IFC-fil

**Optimér IFC…** (under Fil eller på startskærmen: intet projekt behøver at være åbent) laver en lettere kopi af en eller flere IFC-filer uden at ændre, hvad de siger. Kopien beholder hver GlobalId, hvert navn, egenskabssæt, hver mængde, type, materiale og relation præcis, som filen skrev dem, også dens hoved; intet af Lapins tilføjes. Kun geometrien ændres: net (fladesæt, facetterede B-reps af plane flader) renses (dobbelte punkter svejset, degenererede og gentagne trekanter og ubrugte koordinater fjernet) og forenkles, aldrig længere fra originalen end **Præcisionen** (Hurtig 5 mm, Afbalanceret 1 mm, Præcis 0,1 mm eller din egen), målt begge veje; ens legemer skrives én gang og deles af deres produkter; og det, intet refererer til (ubrugte punkter, stilarter, tomme repræsentationer), forsvinder. Ekstruderinger, booleske legemer og andre eksakte legemer forbliver, som de er. Afrundede koordinater, B-reps skrevet som fladesæt (kun IFC4 og IFC4X3) og en zippet `.ifczip` er der, når du vil have dem.

![Optimér IFC](shots/optimize-ifc.webp)

Kopien skrives ved siden af originalen eller i en mappe, du vælger, under originalens navn med en **Endelse** (`.optimized` til at begynde med): `hus.ifc` giver `hus.optimized.ifc`. Originalen skrives aldrig, og findes kopien allerede, spørger Lapin, før den erstattes. For hver fil giver rapporten størrelse, entiteter og trekanter før og efter, de delte legemer, de fjernede forældreløse, den største målte afvigelse og varigheden; **Åbn mappe** viser kopien, og **Åbn i Lapin** åbner den i et andet vindue. I browseren downloades kopien. En fil, der allerede er i projektet, har stadig vejen tilbage: **Optimér kildefilen…** på et importeret IFC's gruppe (i dens egenskaber) eller på et link (Administrer → Links) gør det samme ved den fil, den kom fra, så længe den stadig ligger på disken.

![Rapporten](shots/optimize-ifc-report.webp)

## DXF og DWG

**Importér DXF…** henter en CAD-tegning ind i visningen forrest, med dens lag, skraveringer, tekster og mål. Den kommer ind som tegning, du kan fange til og redigere. Vil du bruge en DWG, så gem den først som DXF fra dit CAD-program (AutoCAD, BricsCAD eller DraftSight: **Save As → DXF**). En PDF eller et billede kommer ind til at tegne over med **Filer → Importér PDF eller billede…** eller **Tegn → PDF eller billede**: en vektor-PDF's linjer som tegning i det målestoksforhold, den blev tegnet i, dens billeder og enhver PNG eller JPEG som gennemsigtige billeder (se [Tegning](drafting.md)). I en plan lægger **Placeret: efter kortets koordinater** en tegning i rigtige koordinater (en matrikelplan i Stereo 70) der, hvor kortet placerer den (se [Grunden og dens data](site.md)).

Tegningen ligger også i **3D-visningen**, på planet af den visning, den kom ind i: på en plan i etagens kote, på et snit eller en facade på visningens plan, i dens lags farver, dens skraveringer svage, som tynde linjer, der aldrig skjuler modellen. Et lag slået fra i **Tegn → Lag** forbliver slået fra i 3D. **Vis → DXF i 3D** (også i **Vis → Visningsindstillinger** for en 3D-visning) viser eller skjuler alle importer; én import slås til og fra med **I 3D** i egenskaberne for en hvilken som helst af dens former, en linket DXF med **3D** i **Administrer → Links**. Når du tegner i 3D i den kote, med Tegns værktøjer eller Studios Skitse, fanger du dens linjer, så du kan tegne over den i 3D som på planen. Linjer, som filen gav koter, står i rummet i de koter, som før.

![En situations-DXF i 3D-visningen, i stueetagens kote](shots/dxf-3d.webp)

En importeret DXF er en del af projektet: **File → Save** gemmer den i `.lapin`-filen med lag, skraveringer, tekster og enhver gruppe, du har lavet af den, og den er der, når projektet åbnes igen, på etagens plan. Den planvisning, den kom ind i, er etagens plan fra da af, den som projektbrowseren viser, og det, der var tegnet på etagen før, flyttes over på den. Lapin spørger, om du vil gemme, før programmet lukker med ugemte ændringer. Vil du hellere have tegningen som sin egen fil, så sæt flueben i **As a link** i importdialogen: den forbliver linket, skrivebeskyttet, og **Manage → Links** genindlæser den, når filen ændres.

## Links

**Tilknyt IFC…** holder en anden fil linket i stedet for at importere den. Den er skrivebeskyttet og genindlæses, når den ændres. Det virker også for en DXF. **Administrer → Links** viser, genindlæser, konverterer eller fjerner linkene. En linket DXF ligger i 3D, som en importeret gør; dens **3D**-felt i **Administrer → Links** slår det fra. **Synlighed / grafik** (<kbd>VV</kbd>) skjuler et link, viser det i halvtone eller giver det sine egne kategorier i én visning alene (*Snit, facader og 3D*, Synlighed / grafik).

En linket IFC beholder, hvad filen siger om elementernes materialer. Vægge, dæk og tage med lagsæt tegnes i deres lag, også som referencer, så en ingeniør skjuler det arkitektoniske links overflader (**Vægge ▸ Overflader**, **Dæk ▸ Overflader**), og en konstruktionsstil tegner kun deres kerner; en linket konstruktionsmodels stænger er i kategorien **Armering**, som hver 3D-visning udelader, indtil den vises. Et link lavet med en ældre version får sine vægges lag, når det genindlæses (dækkenes med det samme).

## Punktskyer

**File → Link point cloud…** sammenkæder en laserscanning eller en fotogrammetri-sky, kun i åbne formater: **LAS** (1.0–1.4, alle punktformater), **LAZ** (komprimeret LAS), **E57**, **PLY** (ASCII og binær) og punkter som tekst, **XYZ**, **PTS** og **CSV**. Proprietære formater (RCP og RCS fra Autodesk ReCap, FLS fra FARO, LGS fra Leica) læses ikke: eksportér først scanningen fra dens program som E57 eller LAS.

Lapin spørger, hvor skyen står, og i hvilken enhed:

- **efter projektets kortkoordinater**: filens x og y er øst- og nordkoordinater, placeret via opmålingspunktet og vinklen til nord fra **Manage → Location**, dens højder over opmålingspunktets kote. Lapin foreslår det, når filen nævner et koordinatsystem eller dens koordinater ligger langt fra nul, og projektet har en placering;
- **origo på origo**: filens origo på projektets;
- **centreret på projektets origo**: dens midte på origo, dens laveste punkt på ±0.00;
- **dens enhed**: som filen siger (LAS og E57 siger det), ellers meter; centimeter, millimeter, fod, amerikanske landmålerfod og tommer tilbydes. Når skyen bliver usandsynlig for en bygning eller en grund (under 20 cm eller over 50 km), eller filen siger en anden enhed, advarer Lapin.

Skyen bliver i sin fil. Den kopieres ikke ind i projektet: projektet gemmer kun sammenkædningen (filens sti, hvor skyen står, hvordan den tegnes). Lapin indekserer den én gang, i baggrunden, til et octree af fliser i sin egen mappe på denne computer (ikke ved siden af projektet), og **Manage → Links** viser, hvor langt den er. Derefter henter 3D-visningen de fliser, kameraet har brug for, de nære først og finere, jo tættere man kommer, inden for et punktbudget sat af **View → Display quality** (1,5, 3 eller 6 millioner punkter tegnet ad gangen), så en scanning på hundrede millioner punkter drejer lige så jævnt som en lille.

![En scanning af huset sammenkædet som punktsky, i 3D-visningen](shots/point-cloud-3d.webp)

I **Manage → Links** har en sky sin egen række: dens punkter, hvor langt dens indeks er, **Shown**, **Halftone**, **3D**, hvad der farver dens punkter (**RGB**, **Intensity**, **Elevation** eller **Classification**, efter ASPRS-klasserne: terræn, vegetation, bygning, vand…) og et punkts **Size** i pixels. Knappenålsknappen placerer den igen (efter kortet, origo på origo eller centreret, dens enhed, flyttet langs x, y og z, drejet); genindlæsningsknappen læser dens fil igen og indekserer kun igen, hvis den er ændret. **Unlink** fjerner sammenkædningen; indekset bliver i Lapins mappe og tjener skyen igen, hvis den sammenkædes igen.

![Manage → Links med en punktsky](shots/point-cloud-links.webp)

En sky eksporteres ikke med modellen: IFC og Fragments bærer modellen, ikke scanningen, og **Export 3D…** tager kun et udsnit som punkter i GLB og glTF, når man beder om det. Planer, snit og facader tegner den også (*Snit, facader og 3D*, Punktskyer i visninger). I browseren kan en sky på op til 160 MB sammenkædes; den indekseres i siden og skal sammenkædes igen, når siden genindlæses. Større skyer kræver desktop-appen.

## 3D-modeller

**3D-eksport (glTF, USD, DAE)…** skriver modellen som flader til rendering, visualisering og udvidet virkelighed (AR):

| Format | Til |
|---|---|
| GLB, glTF | Blender, three.js, webfremvisere. Dine materialers egne billeder kommer med. |
| USDZ | Apples AR Quick Look, Reality Composer, Omniverse. Billeder kommer med. |
| USDA | OpenUSD som tekst, én fil. |
| DAE | COLLADA, til SketchUp og ældre værktøjer. |
| OBJ + MTL | En .zip til Twinmotion, Lumion, 3ds Max og ethvert renderprogram. Med billeder. |

**Importér mesh (OBJ, STL, glTF)…** henter et mesh fra et andet 3D-program (OBJ, STL, glTF eller GLB) ind som referencer på den aktive etage. glTF siger sin enhed og hvilken akse der vender op; for OBJ og STL spørger Lapin. **Gør til Studio-legeme** i Studio gør en reference redigerbar (se [Studio](studio.md)).

![Eksportér 3D](shots/export-3d.webp)

Du vælger, hvad der skal eksporteres, hvilken fase og origo. **Hvad** er **alt**, eller modellen **som visningen viser det**: 3D-visningen forrest, uden dens skjulte elementer og kategorier og de etager, den ikke viser, med de lag den viser (uden overflader, eller kun kernerne). Når den visning er skåret (snittet over de viste etager, et snit vist i 3D, snitboksen), skærer **Snit** også eksporten der og lukker hvert snit i sine materialer, så filen ser ud som skærmen. En 3D-visning, der udelader noget, eksporteres, som den viser det, medmindre du vælger andet. Hvert element beholder sin IFC-klasse, type og GlobalId. Vil du udveksle selve modellen frem for dens flader, så brug **Eksportér IFC…**.
