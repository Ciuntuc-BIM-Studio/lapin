---
title: Snit, facader og 3D
order: 70
summary: Lav snit og facader, åbn 3D-visninger, og indstil, hvad hver visning viser og hvordan.
---

Alle visningerne står i projektbrowseren, ordnet under deres fagområde: Arkitektur, Konstruktion eller Installationer. Dobbeltklik på en visning for at åbne den i en fane.

![Projektbrowseren med et snit og en facade](shots/browser.webp)

## Snit

Vælg **Vis → Snit** (<kbd>SE</kbd>), eller klik på **+** ved siden af **Snit** i browseren. Tegn derefter snitlinjen i en plan med to klik, og klik en tredje gang på den side, snittet ser mod. Snittet tegnes ud fra modellen og holdes opdateret, mens du ændrer den.

![Snit A-A gennem huset](shots/section-view.webp)

Et snit behøver ikke være én lige linje. **Knækket snit** (<kbd>SJ</kbd>) tegner det som en polylinje, klik efter klik, og <kbd>Enter</kbd> afslutter: hvert lige stykke er et plant snit, der kun ser mellem sine egne ender, og stykkerne tegnes side om side i deres sande længde, en fin stiplet linje markerer hvert knæk. **Buet snit** (<kbd>SV</kbd>) tegner en bue gennem tre punkter (start, slutning, et punkt på den) og ruller den ud på samme måde, så en buet facade læses i sin sande længde. **Snit langs** (<kbd>SA</kbd>) vælger et element med en linje i planen, en vej eller sti, en grøft, en støttemur, en væg, en bjælke, et fundament eller en tegnet polylinje, og følger det, som det er nu; det skriver stationerne forneden (0+020, for hver tiendedel af målestokken, eller den afstand du sætter under **Stationer** i visningens indstillinger). I planen er et snits punkter greb: træk et, træk midten af et stykke for at tilføje et knæk, Ctrl/⌘-klik på et punkt for at fjerne det. Vist i 3D skærer et udfoldet snit modellen langs sit første stykke og lægger sin tegning stykke for stykke.

![Et knækket snit gennem huset: tre stykker side om side, knækkene markeret](shots/section-jogged.webp)

Markér snittet i browseren for at indstille dets skala, dybde, fagområde og stil i egenskabspanelet.

### Hvor et snit markeres

Et snits linje tegnes i hver plan, og snittet markeres i hvert andet snit og hver facade, som dets plan krydser: en fin stiplet linje, hvor planet skærer visningen, med snittets navn og en pil, der ser den vej, snittet ser, øverst, i samme størrelse på papir som i en plan. Et snit markeres aldrig i sig selv. Klik på en markør for at vælge snittet, dobbeltklik for at åbne det.

Hvert snit ligger på et **markørlag** (**Sections** fra start; **Markørlag** i snittets egenskaber ændrer det), og hver visning vælger, hvad den markerer. **Vis → Snit…** viser projektets snit efter lag for den forreste visning, med et afkrydsningsfelt pr. lag og pr. snit kun for denne visning, hvert snits lag (et eksisterende eller **Nyt lag…**) og i hvor mange visninger hvert er skjult. Et tegningsark markerer, hvad dets visninger markerer.

Et snit skåret på tværs af en bjælke eller en søjle (**Snit her**, **Tværsnit** på fanen Simpel armering) har også greb i planen: træk en af enderne langs linjen for at give den side mere eller mindre margen, og træk midten langs elementet for at flytte stationen, det skærer ved. Snittets vindue følger med.

![Vis → Snit… for sydfacaden](shots/sections-dialog.webp)

## Facader

**+** ved siden af **Facader** laver bygningens fire facader på én gang.

![Sydfacaden](shots/elevation-view.webp)

### Solskygger

En facade, et snit eller en situationsplan kan tegne de skygger, solen kaster på det, de ser, udfyldt under deres streger, så de udskrives på tegninger, i PDF og DXF. Vælg dem i visningens egenskaber under **Solskygger**:

- **egen sol**: en sol for visningen. Som standard tegnekonventionen: solen over venstre skulder, strålerne langs en terninges diagonal (fra 45°, 35,26° høj), så en skygge er lige så dyb, som det, der kaster den, rager ud. **Sol fra** og **Solhøjde** drejer den; **Tegnekonvention** sætter den tilbage.
- **projektets sol**: solen på projektets sted (Administrer → Placering) på en dag og et klokkeslæt for visningen. **Som i Sol og skygger** tager dagen og klokkeslættet fra Vis → Sol og skygger.

Et snit kaster og modtager kun skygger fra det, der ligger bag dets snitplan. En etageplan tegner ingen skygger: dens vægge, skåret i planens højde, ville kaste dem for korte.

![Sydfacaden med tagets skygge](shots/elevation-shadows.webp)

![En visnings solskygger](shots/view-shadows.webp)

### Overfladeskraveringer

En facade eller et snit skraverer hver flade, den ser, med materialets **Elevation**-skravering (**Administrer → Materialer**, fanen **2D graphics**): mønster, skala, vinkel, pen og fyld. Kun den del af en flade, som intet nærmere element skjuler, skraveres, så en facades puds eller tegl vises omkring døre og vinduer, og et snit skraverer det, det ser bag snitplanet, ikke bag de elementer, det skærer. En lagdelt væg viser materialet i sit yderste lag. Skraveringen tegnes under stregerne og udskrives derfor på tegningsark, i PDF og i DXF (lagene SURFACE-HATCH og SURFACE-FILL).

Med **Alignment** *view* fortsætter mønstret fra flade til flade; med *layer* ligger det på selve fladen, langs dens vandrette linje fra det nederste hjørne, og en flade set skråt viser det forkortet. Et materiale, der fortsætter i samme plan over flere elementer, skraveres som ét, medmindre visningsstilens **Join materials** er slået fra.

Indtil du vælger en, kommer et materiales facadeskravering **ud fra navnet**, ligesom dets snitskravering, og **Administrer → Materials** viser hvilken (*ud fra navnet (BRICK)*): mursten og murværk BRICK, sten STONE, træ BOARDS, metalbeklædning VERTICAL, tagsten, spån og skifer HORIZONTAL, fliser TILES, grus og jord som i snit. Isolering, membraner, puds, maling og beton får ingen: isoleringens BATT er en snitskravering og foreslås aldrig for en flade (valgt i hånden tegnes den). Vælg **none** for ingen skravering. Et udfoldet snit (forskudt, buet eller langs et element) skraverer hvert af sine stykker mellem stykkets ender og tegner solskyggerne på samme måde. Visningsstilens **Surface hatches** (**Vis → View settings**, **Administrer → View styles**) slår dem fra for hele visninger: til i Architectural, fra i Structural, MEP og Coordination. Planer beholder deres egne snit- og planskraveringer. Et tags tagdækning skraveres efter dens materiale, så et tegltag viser sine skifter på de tagflader, visningen ser, og ingen på dem, der vender bort.

![Sydfacaden, blankmuren skraveret ud fra sit navn](shots/elevation-surface-hatch.webp)

![Et forskudt snit skraverer havemuren, som dets sidste stykke ser](shots/section-developed-surface-hatch.webp)

![Overfladeskraveringer i visningens indstillinger](shots/view-surface-hatch.webp)

## 3D-visninger

<!-- video section-3d -->

Dobbeltklik på **3D** for at åbne 3D-visningen. Træk med venstre knap for at kredse om modellen. Oversigtsplanen øverst til højre viser, hvorfra du kigger. **Plan**, **Persp** og **Tilpas** ændrer kameraet. Vil du gemme et kamera, så duplikér 3D-visningen.

Musehjulet zoomer mod modellens punkt under markøren, som bliver hvor det er på skærmen; zoomet stopper lige før punktet og går aldrig igennem det. Et træk med venstre knap drejer visningen om midten af de markerede elementer, som bliver hvor den er på skærmen, så du kan se dem fra alle sider; uden markering drejer den om modellens punkt under markøren, hvor trækket begynder, og om visningens midte, hvis der intet er under markøren. **Vis → Zoom til markøren** og **Vis → Kredsløb om markeringen** slår hver af dem fra (hjulet zoomer så mod visningens midte, og visningen drejer om sin midte), ligesom afkrydsningsfelterne i en 3D-visnings egenskaber. Valget gemmes på denne computer.

### Et snit i 3D

Højreklik på et snit i browseren, og vælg **Vis i 3D**. 3D-visningen skærer modellen langs snittets linje. Den beholder den side, snittet ser mod, op til snittets dybde, hvis det har en. Hvert skåret element lukkes, hvor det skæres, lag for lag, hvert lag i sit eget materiale: en væg viser sin murstenskerne og sin puds, lige så massiv som væggen er. **Skraveringer** på chippen nederst i 3D-visningen lægger i stedet snittets tegning på snitfladen, med de snitudfyldninger og skraveringer, et ark udskriver. Snittets mål, kotemærker, mærker og tekster ligger i samme plan, i samme størrelse og på samme sted som i snittet. Linjer beholder deres pentykkelser og deres fulde stregmønstre. De, der er tegnet ved siden af bygningen, står i luften ved siden af snittet. **Annoteringer** på chippen nederst i 3D-visningen skjuler dem eller viser dem igen. En facades **Vis i 3D** drejer kameraet, så det ser samme vej som facaden.

![Snittets højrekliksmenu i browseren](shots/section-3d-menu.webp)

![Snit B-B set i 3D](shots/section-3d.webp)

Vil du se hele modellen igen, så klik på **×** på chippen **Snit** nederst i 3D-visningen, eller vælg **Vis i 3D** igen.

![Snit B-B med Skraveringer slået til: snittet, som arket udskriver det](shots/section-3d-hatches.webp)

### Snitboksen

**Vis → Snitboks** (genvej BX) skærer 3D-visningen til en boks. Boksen starter rundt om hele modellen. Hver flade har en pil i midten: træk i den for at flytte den flade. Hvert element, boksen skærer, lukkes på den flade, lag for lag, i sine materialer, og fladerne mødes rent ved boksens kanter. **Snit ved plan** sætter boksens top ved det aktive niveaus plansnit (dets visningsområde), så niveauet ses i 3D, som dets plan skærer det. **Tilpas** sætter boksen tilbage rundt om hele modellen. Vil du dreje boksen, så træk i det runde håndtag forbi dens øverste hjørne (i hele grader; hold Shift nede for trin på 15°), eller markér en væg og klik på **Justér**: boksen drejer, som væggen løber. **Skraveringer** tegner boksens snit, som et ark gør, hvert materiale med sin snitudfyldning og skravering, i stedet for materialerne. En gemt 3D-visning beholder sin snitboks: gem visningen med boksen slået til, eller åbn en gemt visning og indstil dens boks, så er boksen der, hver gang visningen åbnes. **×** på chippen **Snitboks** slår boksen fra.

![Snitboksen skåret ved stueetagens plan, med østfladen trukket ind](shots/section-box.webp)

![Samme boks med Skraveringer slået til: snittene, som et ark tegner dem](shots/section-box-hatches.webp)

### Én etage ad gangen

Vil du se ind i bygningen, så vis kun nogle af dens etager. Højreklik på et niveau i projektbrowseren, og vælg **Vis kun dette niveau i 3D**, eller **Vis dette niveau og nedenunder i 3D** for at se bygningen skåret ved det niveau. Niveauet bliver det aktive, og 3D-visningen følger det aktive niveau: klik på et andet niveau i browseren for at gå en etage op eller ned.

![Niveauets højrekliksmenu i browseren](shots/storeys-3d-menu.webp)

![Stueetagen alene i 3D](shots/storeys-3d.webp)

**Vis → Viste etager**, eller 3D-visningens egenskaber, indstiller resten:

- **Vis**: alle etager, de afkrydsede etager, kun den aktive etage, eller den aktive etage og dem nedenunder.
- **På ingen etage**: det, der ikke står på nogen etage (grunden, terrænet, linkede filer), vises eller skjules.
- **Snit over sit niveau**: modellen skæres så højt over den højeste viste etage, 1.20 m til at begynde med, som en plan skærer den. Hvert element, der skæres der, lukkes på snitfladen, lag for lag, i sine materialer, ligesom et snit vist i 3D.

![Stueetagen skåret ved 1.20 m: væggene lukket, lag for lag](shots/storeys-3d-cut.webp)

De skjulte etager tegnes ikke, og du kan ikke markere dem, tilpasse til dem eller gå på dem. Det, et snit fjerner (snittet over de viste etager, et snit vist i 3D, snitboksen), er heller ikke der, når du går rundt: du går forbi den øvre del af en væg, som snittet har fjernet, og lågene på snitfladen lukker det, snittet åbner. **Filer → Eksportér 3D…** tager 3D-visningen, som den viser den (se [Import og eksport](import-export.md)). En gemt 3D-visning beholder sit eget valg. Chippen **Etager** nederst i 3D-visningen siger, hvad der vises; dens **×** viser alle etager igen.

### Uden overflader, eller kun kernerne

En 3D-visning kan vise bygningens konstruktion alene, som en konstruktionsplan gør. I 3D-visningens egenskaber (når intet er markeret), eller under **Vis → Visningsindstillinger** med 3D-visningen forrest, viser **Lag**:

- **alle lag**: vægge, dæk og fundamenter, som de er bygget;
- **uden overflader**: uden de lag, hvis materiale er en overflade (puds, filt, maling, fliser, plader, gulvbelægninger, eller et materiale, hvis kategori er Overflade under Administrer → Materialer); isoleringen bliver;
- **kun kernen**: kun det lag, der er markeret som kerne (murstenen, betondækket).

![Et hus i 3D med kun kernen: væggene er deres mursten, dækkene deres plade](shots/layers-3d-core.webp)

Hvert lag, der beholdes, forbliver et lukket legeme i sit eget materiale, så et snit eller snitboksen lukker det som før. Døre og vinduer bliver i deres åbninger; en lysning beklædt med en overflade følger væggens overflader. En 3D-visning tager sine lag fra sin stil, som en plan: sæt dens **Fagområde** til Konstruktion, og den viser kun kernerne; sæt **Lag** for at vælge andet, og knappen ved siden af går tilbage til stilens valg. Chippen **Viste lag** nederst i 3D-visningen siger, hvad der er udeladt; dens **×** viser alle lag igen. De skjulte lag kan ikke markeres eller gås imod, og **Filer → Eksportér 3D…**, som visningen viser det, eksporterer de samme lag.

### At gå ind i et rum

Højreklik i en plan inde i et rum, og vælg **Gå i 3D**. 3D-visningen sætter dig inde i rummet, 1.60 m over gulvet, så du ser langs dets længste side.

![Et rums højrekliksmenu i planen](shots/room-walk-menu.webp)

![Stående i stuen](shots/room-walk.webp)

- Træk med venstre knap for at se dig omkring.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> eller piletasterne går. Hold <kbd>Shift</kbd> nede for at løbe. <kbd>Space</kbd> hopper.
- Musehjulet tager skridt frem og tilbage.

Du går, som et menneske ville. Vægge, søjler, møbler og lukkede vinduer stopper dig, og du glider langs dem. Du står på dæk, går op og ned ad trapper og ramper og falder, hvis du går ud over en kant. Døre står åbne for dig: gå gennem deres åbninger. Kun det, 3D-visningen tegner, stopper dig: skjulte elementer, de etager, den ikke viser, og det, dens snit fjerner, er ikke i vejen for dig.

![På vej hen til soveværelsets dør](shots/room-walk-door.webp)

![Op ad trappen](shots/room-walk-stair.webp)

**Flyv** på gåturens chip, eller <kbd>F</kbd>, lader dig flyve i stedet: ingen tyngdekraft, og du passerer gennem vægge og dæk. <kbd>E</kbd> og <kbd>Q</kbd> går op og ned. Tryk på <kbd>F</kbd> igen for at gå; du falder ned på gulvet under dig.

<kbd>Esc</kbd> eller **Afslut gåtur** henter det kamera tilbage, du havde før.

### Realistisk, med solen

Vis → **Visningskvalitet** bestemmer, hvordan 3D-visningen gengives, på denne computer:

- **Tegning**: tegningens farver og et jævnt lys, som Lapin altid har tegnet. Hurtigt på enhver computer.
- **Mellem**: solens skygger, skygge i hjørnerne, hvor flader mødes, og lys fra himlen.
- **Realistisk**: himlen bag modellen og et terræn under den (hvor projektet ikke har terræn), og glas, lyset går igennem. Til præsentationer, på et godt grafikkort.

![Et hus i den realistiske visning](shots/view-realistic.webp)

Solen er projektets: hvor den står for projektets sted (Administrer → **Placering**) en dag på et klokkeslæt. Vis → **Sol og skygger** bestemmer dagen (jævndøgn og solhverv er et klik væk) og klokkeslættet, afspiller dagen og tænder også solen i tegningsvisningen. Uden koordinatsystem og opmålingspunkt er stedet Bukarest. Tidszonen følger stedets længdegrad, med europæisk sommertid i Europa.

![Sol og skygger](shots/sun-dialog.webp)

Den realistiske visning tager nogle af sine tal (skyggen i hjørner, skyggernes kvalitet, solens farve) fra [BimGo](https://github.com/aussieBIMguru/BimGo) af Aussie BIM Guru (MIT-0).

## Modellering i 3D

3D-visningen er et sted at modellere, ikke kun at se: elementer flyttes og kopieres der som i en plan, fanget af de andre elementers hjørner, kanter og flader.

### Flyt og kopiér i 3D

Markér elementerne, og vælg så **Move** (M) eller **Copy** (CO) med 3D-visningen forrest. Klik på basispunktet: et hjørne, en kant eller en flade af det, du flytter. Markøren siger, hvad den fanger: **Endpoint** (endepunkt), **Midpoint** (midtpunkt), **On edge** (på kant), **On face** (på flade) eller **On level** (på etagen) i det tomme rum; det, der ligger bag fladen under markøren, tages ikke. Linjerne i en importeret DXF, vist i 3D, fanges også. Klik så, hvor det skal hen, for eksempel et dæks hjørne: markeringen følger markøren i 3D, også op og ned, og placeres der. Fra basispunktet udledes den røde, grønne og blå akse, når markøren følger en af dem; → ← ↑ låser den røde, grønne eller blå akse (samme tast slipper), og Skift holder den udledte akse. Med en låst akse bringes et hjørne, du holder over, ind på aksen, så flytningen flugter med det uden at gå derhen.

Skriv en værdi når som helst uden at klikke i et felt: en længde går i markørens retning (1200), og dx,dy eller dx,dy,dz er selve forskydningen (0,0,450 løfter 450 mm). Enter anvender den, Esc annullerer. Copy bliver ved med at kopiere fra samme basispunkt indtil Enter eller Esc.

Det, der har sin egen højde, går op eller ned med flytningen: bunden af en væg, en søjle eller en genstand, en trappes bund og top (dens stigning bevares), en bjælkes top, et dæks topforskydning, et tags murrem; et Studio-legeme får et flyttetrin. En dør eller et vindue glider langs sin væg.

Du kan også trykke på et markeret element og trække det: det flyttes på samme måde fra det punkt, du trykkede på, fanget, og placeres, hvor du slipper. **View → Drag to move in 3D** slår det fra, så et træk på markeringen drejer visningen.

![En trappe flyttet i 3D ved det øverste trins hjørne hen på dækkets hjørne](shots/model-3d-move.webp)

### Placering af elementer i 3D

Alle værktøjer, der tager punkter i en plan, virker også i 3D-visningen: vægge, glasfacader, dæk, tage, lofter, dækhuller, rum, søjler, bjælker, fundamenter, rækværker, fodlister, møbler, elevatorer, veje, planter og resten. Punkterne lægges på den aktive etage: markøren fanger modellen som ved en flytning (et dæks hjørne, en bjælkes ende, midten af en kant, linjerne i en DXF), og punktet tages lodret under den på etagen; en stiplet linje viser hvor, når fangningen ligger over eller under. Punkterne og omridset, de danner, tegnes på etagen undervejs: en vægs linje, et dæks omrids, der lukker tilbage til første punkt, en bue gennem sit tredje punkt. Fra det sidste punkt udledes den røde og grønne akse; → og ← låser dem.

Skriv en værdi når som helst: en længde går fra det sidste punkt i markørens retning (3000, eller 3000<30 med en vinkel), et rektangels bredde,højde fra dets første hjørne. Enter anvender den, lukker et dæk eller afslutter et rækværk; Backspace tager det sidste punkt tilbage; Esc kasserer punkterne og afslutter værktøjet anden gang. Et træk drejer stadig visningen, så du kan dreje rundt om modellen mellem to klik. Chippen nederst siger, hvilken etage punkterne lægges på.

Døre og vinduer placeres, hvor du klikker på en vægs flade i 3D, på det sted langs væggen, du klikkede.

![En væg startet på dækkets hjørne i 3D, den grønne akse udledt fra starten](shots/model-3d-wall.webp)

## Hvad en visning viser

Fanen **Vis** indstiller, hvad visningen forrest viser, og hvordan den ser ud.

**Grid** (<kbd>F7</kbd>) skjuler eller viser planens baggrundsgitter, og **Tynde linjer** (<kbd>TL</kbd>) tegner hver linje én pixel tyk, så en tæt tegning kan læses. Begge gælder kun skærmen: tegningsark, PDF og DXF beholder deres penne, og fang til gitteret bliver. 3D-visningens jordgitter følger samme kontakt.

![Fanen Vis](shots/ribbon-view.webp)

- **Visningsindstillinger.** Visningens fagområde og stil, dens område (snithøjde og -dybde) og dens grafik.
- **Visningsstile.** Arkitektonisk, Konstruktiv (kun kernerne), Installationer eller dine egne. Hver stil vælger de lag, der tegnes (alle, uden overflader eller kernen), og hvordan materialer samles.
- **IFC 2D-symboler.** Elementer importeret eller linket fra en IFC-fil, som har deres egen plantegning med (et fodaftryk, en dørs slag, et inventars symbol, med filens farver, udfyldninger og tekster), tegner den i planer i stedet for deres snit. Fjern fluebenet under Visningsindstillinger (eller i en visningsstil) for i stedet at tegne deres 3D-snit.
- **Synlighed** (<kbd>VV</kbd>) og **Grafik** (<kbd>VG</kbd>). De viser eller skjuler kategorier og indstiller deres penne, linjetyper og udfyldninger.
- **Opdel** og **Vindue.** De viser to visninger side om side eller åbner en visning i et separat vindue.
- **Visningskvalitet.** Hvordan 3D-visningen gengives (**Tegning**, **Mellem** eller **Realistisk**, gemt på denne computer: se *Realistisk, med solen*), og hvordan 3D-visningen tegner dele med mange trekanter (et importeret anlæg, møbler, rør), når de er langt væk. **Afbalanceret** tegner en fjern del grovere kun, hvor forskellen er under en pixel. **Hurtig** lader den afvige op til 3 pixels, til store modeller og langsommere computere. **Præcis** tegner hver del, som den er. Indstillingen hører til projektet. Modellen, dens tegninger, eksporter og mængder bliver aldrig grovere: en kurve består af lige stykker, der højst afviger en millimeter fra den.
- **Sol og skygger.** Projektets sol en dag på et klokkeslæt, der lyser 3D-visningen op med sine skygger (se *Realistisk, med solen*).

![Visningskvalitet](shots/display-quality.webp)
