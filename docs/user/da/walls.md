---
title: Vægge
order: 20
summary: Tegn vægge hjørne for hjørne, ret dem i egenskabspanelet, og opsæt vægtyper med deres lag.
---

<!-- video walls -->

## At tegne vægge

Åbn fanen **Arkitektur**, og vælg **Væg**, eller skriv <kbd>WA</kbd>.

Mens værktøjet er aktivt, viser egenskabspanelet **Nyt element**: den type, væggene tegnes med. Vælg en anden der før det første klik, og se dens lag, dens tykkelse og hvor mange vægge der allerede bruger den; **Redigér typer…** åbner Wall types. Dør, vindue, dæk, loft og glasfacade viser deres typer på samme måde.

![Fanen Arkitektur](shots/ribbon-architecture.webp)

Klik, hvor væggen starter, og klik derefter ved hvert hjørne. Den næste væg følger musen. Mens du tegner, kan du skrive en længde og en vinkel i felterne over visningen i stedet for at klikke. Tryk på <kbd>Enter</kbd> eller <kbd>Esc</kbd> for at stoppe.

![At tegne vægge: den næste væg følger musen, med længde og vinkel vist](shots/wall-drawing.webp)

Hvor to vægge mødes, samler Lapin dem selv. Deres lag løber ind i hinanden, sådan som de ville blive bygget.

- **Buet væg** (<kbd>WC</kbd>) tegner en væg langs en bue.
- **Facadeglasvæg** (<kbd>CW</kbd>) tegner en glasvæg med sprosser og paneler; dens type kan tage deres virkelige form fra et [profilbibliotek](profiles.md).
- Vægge sammenføjes af sig selv, hvor de mødes: et hjørne som geringssamling, en væg, der ender på en anden, som T, en, der krydser en anden, som X. Ved et T eller et X løber lagene ind i hinanden, som de bygges: den endende vægs kerne fortsætter gennem den andens overflader ind til dens kerne, og dens overflader stopper ved den andens flade, hvor de møder deres lige, så planen viser én opbygning uden en linje igennem. **Sammenføj** (<kbd>JW</kbd>), **Geringssamling** og **Stødsamling** ændrer, hvordan to vægge mødes, når det ikke er det, du vil; **Nye hjørner som gering** gør stødsamling til standard på denne computer. Uanset hvad går hvert lag selv rundt om hjørnet: overfladerne mødes med deres egne og løber ubrudt videre, og kernerne mødes på diagonalen (gering) eller den ene kerne løber ud til den andens yderside (stød), så en plan af vægge med lag viser rene hjørner. Hvert lag går ind i den anden væg gennem dens lag med lavere **Prioritet** og stopper ved det første med samme eller højere (se **Vægtyper** nedenfor), så som standard møder kerner kerner, og beklædninger stopper ved fladerne; i et hjørne løber en kerne med højere prioritet gennem den andens, hvad enten det er gering eller stød. En vægs **Snitprioritet** fører den aldrig ind i en væg, den er sammenføjet med: mellem sammenføjede vægge afgør den kun, hvilken kerne der løber igennem, når begge kerner har samme prioritet (ved et stødhjørne eller en krydsning).
- **Byt om** ændrer, hvilken af de to vægge der løber igennem.

**Linjen du tegner på.** Mens værktøjet Wall er aktivt, vælger listen i kommandolinjens bjælke den linje, du klikker på: **Kernens midte** (aksen, som før), **Kerne, yderside**, **Kerne, inderside**, **Yderside** eller **Inderside**. Ydersiden er væggens højre side, som du tegner (tegn mod uret rundt om bygningen, så den vender ud). Væggens akse bliver på kernen, forskudt fra linjen, og vægge tegnet efter hinanden på samme side mødes i den sides hjørne.

## At ændre en væg

Klik på en væg for at markere den. Dens greb vises. Træk i enderne for at forlænge eller afkorte den, eller træk i krydset i midten for at flytte den.

![En markeret væg med sine greb](shots/wall-selected.webp)

Egenskabspanelet viser den markerede væg. Øverst i panelet står dens type og lag. Derunder kommer længde, højde, fodforskydning, etage og fase. Ret en hvilken som helst værdi, og modellen følger med. Hver vægs værdier er dens egne. Dens **tykkelse** (Thickness) kommer fra dens types lag: skriv en ny, og de valgte vægge får typen med samme opbygning og den tykkelse, kernen ændret og overfladerne bevaret. Lapin bruger projektets type, hvis en har præcis den opbygning, ellers laver den en, opkaldt efter deres (*Partition wall 130mm* bliver *Partition wall 150mm*). De andre vægge af den gamle type forbliver, som de er, og <kbd>Ctrl</kbd> <kbd>Z</kbd> fortryder det i ét trin.

![En vægs egenskaber](shots/wall-properties.webp)

Længere nede viser **Afhængigheder**, hvad der er bygget af væggen, og hvad væggen er bygget af. **Bruges af** viser de elementer, der ændres, når væggen ændres: de vægge, den samles med, dens døre og vinduer, en væg fastgjort til den. Klik på et navn for at vælge elementet. **Bygget af** viser de elementer, væggen afhænger af, og derefter dens type og materialer. Når du sletter et element, som andre afhænger af, nævner en besked dem, der blev genopbygget; <kbd>Ctrl</kbd> <kbd>Z</kbd> bringer det hele tilbage.

**Vend** vender væggens lag til den anden side. Du kan også klikke på de små pile, der vises ved siden af en markeret væg i planen.

En væg genkendt fra en importeret IFC, hvis ende filen skærer skråt, beholder snittet: **Startsnitvinkel** og **Slutsnitvinkel** i dens egenskaber, i grader fra lige. En samling ved den ende træder i stedet.

**Skraveringsretning** bestemmer, hvordan denne vægs lag skraveres i plan. **material** følger hvert materiales egen indstilling. **axis** lægger hvert lags skravering langs væggens akse fra aksens start, så en skrå murstensvæg viser sine skifter langs væggen, og de fortsætter fra lag til lag. **view** starter hver skravering i visningens origo, uden drejning.

### En væg efter sin profil

**Profil…** i en vægs egenskaber tegner dens top i opstalt, set fra dens start til dens slutning: en gavl, en skråning, trin, en væg der følger en trappe. Træk punkterne (**Shift** beholder et punkts højde), klik på toplinjen for at tilføje et, højreklik på et punkt for at fjerne det, eller skriv dem i tabellen i millimeter. **Start fra** giver et **Gavl**, en **Hældning** eller **Trin** op til den højde, du skriver. Et trin er to punkter over hinanden. **Anvend** former væggen; **Plan top** gør den plan igen.

Væggen forbliver en væg: dens tykkelse, dens lag, dens samlinger og dens døre og vinduer er som før, og den eksporteres til IFC med sin form.

![En gavl tegnet i profileditoren](shots/wall-profile-editor.webp)

![Husets vestvæg som gavl, med vinduet bevaret](shots/wall-profile-3d.webp)

### En facadeglasvægs kant og stænger

**Profil…** i en facadeglasvægs egenskaber tegner dens kant i opstalt, som for en væg: en gavl, en hældning, trin eller et vilkårligt omrids, med punkter, der kan trækkes, tilføjes eller fjernes, og en tabel i millimeter. Væggen går op til sit højeste punkt. Dens raster får en linje, hvor kanten knækker (et knæk inden for halvanden sprosse fra en rasterlinje flyttes ind på den); hvert felt beholder de tværposte, der er plads til under dets laveste punkt; panelet over den sidste fylder op til kanten, og en kantstang følger den, skrå; sprosserne skæres til den. Typens indstillinger bliver, og **Profil…** åbner kanten igen, så du kan ændre den, uden genopbygning; **Plan top** giver rektanglet tilbage. Den ses i 3D, opstalter og snit, lister måler paneler og stænger, som de er tegnet, og IFC bærer dem som dele.

![En gavl tegnet som kant på en facadeglasvæg](shots/curtain-boundary-editor.webp)

![Facadeglasvæggen under sin gavl, i 3D](shots/curtain-boundary-3d.webp)

**Stænger…** giver valgte sprosser og tværposte en egen profil. Væggens opstalt viser hver stang: klik på en (<kbd>Shift</kbd> eller <kbd>Ctrl</kbd>/<kbd>⌘</kbd> tilføjer flere, eller tag alle sprosser eller alle tværposte), vælg en profil fra [profilbibliotekerne](profiles.md) eller en enkel kasse, dens **Rotation** og **Spejl**, dens justering på tværs af stangen (for- eller bagside på dens plan) og langs den (start- eller slutkant på rasterlinjen eller niveauet) og dens forskydninger på tværs og ud af bygningen; billedet ved siden af viser tværsnittet, som det sidder. **Anvend på de valgte** giver dem stilen, **Tilbage til typens** fjerner den. En types egen profil har de samme placeringsfelter i **Facadeglasvægge…**, for hver væg af typen. Profilen forbliver en kopi i projektet: **Opdatér fra biblioteket** ændrer hver stang lavet af den, i denne væg og i andre.

![Stænger: en profil placeret på de valgte sprosser](shots/curtain-members-dialog.webp)

![To sprosser med egen profil, i 3D](shots/curtain-members-3d.webp)

## Vægtyper

En vægtype er et sæt lag, fra siden til højre for den retning, væggen tegnes i (ydersiden, når du tegner rundt om en bygning mod uret), til siden til venstre. Hvert lag har et materiale og en tykkelse, og ét lag er **Kerne**. Væggens linje løber langs kernen. Du redigerer typerne under **Arkitektur → Vægtyper**. Et andet lag end kernen kan have sin egen højde: **Fra** og **Op til** angiver, hvor højt det går over væggens fod (fliser op til 1500, en beklædning fra 300); tomt går det i hele højden.

Kolonnen **Prioritet** (0 til 100) angiver, hvordan laget møder en anden vægs lag, hvor væggene sammenføjes: det går gennem den andens lag med lavere prioritet og stopper ved det første med samme eller højere. Tom tager laget sit materiales prioritet i **Administrer → Snitrækkefølge**, ellers 50 for kernen og 0 for de andre lag (tallet vist i gråt). Så en betonkerne på 100 afbrydes ikke af en stolpekerne på 99 og løber gennem skillevæggen, og en hård puds på 60 stopper kernen i en væg, der ender på den. IFC-eksporten skriver den som lagets Priority, og importen læser den tilbage.

Kolonnen **Note** siger, hvordan laget lægges i denne type (*i et sammenhængende lag, Baumit SupraFix*): den hører til typen, ikke til materialet, og en lagopbygningsetiket skriver den efter laget; IFC bærer den som lagets Description. Når lagets materiale har katalogtykkelser (**Administrer → Materialer → Data**), tilbyder den lille pil ved tykkelsen dem, og et advarselstegn markerer et lag af en anden tykkelse.

Et lag, der ikke er kernen, kan også skifte materiale op ad sin højde: knappen **lag** for enden af dets række tilføjer et stykke (fliser, 2100 som standard), fra lagets bund og op; tilføj flere for et bånd (maling til 900, fliser til 1500), og over det sidste stykke fortsætter laget i sit eget materiale til toppen. Hvert stykke tegnes, snittes i plan og snit og måles i sit eget materiale.

Et andet lag end kernen kan have tykkelsen 0, for et materiale, der er for tyndt til at modellere, men skal måles: en fugtspærre, en dampspærre, maling. Det danner intet legeme; planer og snit tegner det som en kraftig stiplet linje, hvor det ligger mellem to lag (på en flade er fladens linje dets egen), tegningen i Typeopbygninger navngiver det som de andre, og det måles i areal: siden af det lag, det ligger på, fratrukket åbninger, i væggens mængder og i mængdeudtræk efter materiale.

![Et pudslag med fliser til 2100 og pudsen ovenover](shots/wall-layer-parts.webp)

![Vægtyper: lagene i en murstensvæg](shots/wall-types.webp)

Signaturforklaringen over lagene viser væggen i plan, sådan som en markeret væg viser den: den stiplede akse med en pilespids i den retning, den blev tegnet, **H** på dens højre side og **V** på dens venstre. Lagene er nummereret fra 1 på H-siden til det sidste på V-siden med de samme numre som tabellens rækker; kernen, som væggens linje løber langs, er fremhævet. En væg vendt med **Vend** har lagene den anden vej, fra V til H. Beskeden fra en afvist gemning forsvinder, så snart du ændrer typen igen.

- **+ Layer** tilføjer et lag. Pilene flytter et lag op eller ned.
- **Zoner i højden** deler kernen op fra bund til top, for eksempel et første skifte, murværket og en ringbjælke.
- **Overliggere** vælger de overliggere, der sættes over døre og vinduer: præfabrikerede eller støbt på stedet, med deres vederlag og højde.
- **Tegn med den** lukker dialogen og begynder at tegne vægge af denne type.

En ændring af en type ændrer alle vægge af den type.

**Import…**, **Export CSV** og **Export JSON** under typelisten flytter væg- og dæktyperne fra ét projekt til et andet eller til et regneark og tilbage: CSV-filen har én række pr. lag (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), en types rækker efter hinanden, og Excels semikolonner og decimalkommaer læses også. Ved import får en type med samme navn filens lag, og de øvrige oprettes, i ét trin at fortryde; et materiale, projektet mangler, oprettes ud fra sit navn. Skemaet **Typeopbygninger** (Schedules → + → Type assemblies) viser hver væg- og dæktype med mærke, samlet tykkelse, hvor mange elementer der bruger den, og dens lag i rækkefølge.

## Vægprofiler og vægnoter

**Arkitektur → Vægprofil** (<kbd>WB</kbd>) fører en profil langs en vægs flade: en gesims, et bånd, en sokkel, en sålbænk. **Vægnot** (<kbd>RV</kbd>) skærer den ind i væggen: en not, en skyggefuge. Vælg profilen og højden i kommandolinjens bjælke, og klik så på væggens flade. I 3D sættes profilen i den højde, du klikker; i en plan i den indtastede højde, på den side, du klikker. **fra toppen** måler højden nedad fra væggens top, til en gesims under tagudhænget. **Profil på valgte vægge** og **Not i valgte vægge** sætter det samme på hver valgt væg, på dens yderside.

En profil løber i fladens fulde længde og afbrydes ved de døre og vinduer, den ville krydse. Hvor to vægge mødes i et hjørne, og begge har en profil af samme form i samme højde på de flader, der mødes, føres de to i gering ind i hinanden, én gesims rundt om hjørnet. Vælg en profil for at ændre den i Egenskaber: dens profil, dens materiale, dens flade (**Skift side**), dens højde, dens afstand fra fladen, hvor den begynder og slutter langs væggen, og om den afbrydes ved åbninger. En vægs egenskaber viser de profiler og noter, den har.

Profiler og noter følger deres væg, når den kopieres, flyttes til en anden etage eller slettes. De måles i længde og volumen (for en not det fjernede volumen). I IFC er en profil en IfcCovering MOLDING og en not en IfcOpeningElement RECESS i sin væg; begge kommer tilbage, som de var.

![Gesimser, bånd og en not på et hus' ydervægge](shots/wall-sweeps-3d.webp)

### Profiler på vægtypen

En profil, som alle vægge af en type skal have (et soklebånd, en gesims, en skyggefuge), hører til typen. I **Arkitektur → Vægtyper** viser **Profiler og noter** dem under lagene, én række hver: profilen, **Vægprofil** eller **Vægnot**, siden (H, siden med typens første lag, eller V, som forklaringen kalder dem), højden over væggens fod (eller under dens top med **Fra toppen**), forskydningen ud fra siden, en profils materiale og dens returer. **Vægprofil** og **Vægnot** under tabellen tilføjer en række. Gem typen: alle dens vægge får dem, og det gør en væg tegnet med typen eller skiftet til den også. De er profiler som de andre: målt, med i vægprofilskemaet, geringsskåret i hjørnerne, hvor vægge af typen mødes, skrevet til IFC. En vendt vægs profiler skifter side med dens lag. **Eksportér JSON** under typelisten tager dem med, med profilernes omrids og materialerne efter navn (CSV'en har kun lagene).

Vælg en på en væg: Egenskaber siger **Fra typen**. Ændrer du dens højde, forskydning eller et andet felt, bliver det felt væggens eget (**Egne:** højde): senere ændringer af typen rører det ikke. **Tilbage til typen** giver den typens værdier igen. **Slå fra på denne væg**, eller at slette den, lader typens profil være slået fra på den væg; **Slå typens profiler til igen** under væggens **Profiler og noter** bringer dem tilbage.

![En profil på vægtypen, i dialogen Vægtyper](shots/wall-type-sweeps.webp)

### Returer

En profil, der når en fri ende af sin væg (ingen væg er samlet der), laver retur: geringsskåret i enden fører den sin profil rundt om hjørnet ind på væggens ende og stopper i sidens plan, så profilen er lukket i stedet for at vise sit snit. **Returer ved frie ender** i Egenskaber vælger begge (standard for en profil), ved start, ved slut eller ingen, for lige ender. En not laver aldrig retur. Ved et hjørne samlet med en anden væg laver profilen ikke retur: den går til hjørnet eller ind i en ens profil på den anden væg. Ved døre og vinduer stopper den lige; sæt hak i **Returer ved åbninger** for også at få retur rundt om lysningerne. **Tilbagerykket fra start** og **Tilbagerykket fra slut** stopper den før sidens ender (lige der), og **Profil på hovedet** vender dens profil.

### Flyt og kopiér en profil

Vælg en profil og brug **MOVE** eller **COPY** som for ethvert element; dens væg bliver stående. Det, flytningen går langs væggen, skubber profilen langs den (en profil langs hele siden får så sin egen start og slut); det, den går på tværs af væggen, ses der bort fra. Flyttet op eller ned i en facade eller et snit ændres dens højde. En kopi lander på samme væg, forskudt, eller på en anden væg, når den lander inde i den, på den side, den lander på. **ROTATE** og **MIRROR** holder den på sin væg: spejlet om væggens akse går den over på den anden side. En profil fra typen, som du flytter, beholder sin nye plads som sin egen; flyttet over på en anden væg bliver den den vægs egen profil, og typens slås fra på den første.

### Profiler

**Administrer → Profiler** rummer projektets profiler, de omrids som vægprofiler, noter og stribefundamenter føres med. **Ny profil** starter fra kataloget med sine mål: et rektangel, et affaset bånd, en gesims, en halvrund, en sålbænksnæse, et bånd med drypnæse; en V-not, en rektangulær eller en rund not; til fundamenter rektangulært, trappet i 2 eller 3 trin, med skrå overside, excentrisk, med sokkelvæg. Eller vælg **Indtastede hjørner**: hvert hjørne u v i millimeter, semikolon imellem, og et tredje tal gør siden fra det hjørne til en bue. **Fra en tegnet kontur** læser en lukket polylinje eller et rektangel tegnet med Tegning og valgt i sin visning. Cirklen i forhåndsvisningen er indsættelsespunktet: for en vægprofil ligger den på væggens flade i profilens højde, med u ud af fladen (ind i væggen for en not). Ændrer du en profil, følger alt, der bruger den.

![Dialogen Profiler](shots/profiles.webp)

### Beklædning

Beklædning modelleres med væggens egne lag, ikke med profiler. Tilføj et overfladelag på ydersiden af vægtypen (natursten, træbeklædning, fibercement, en efterisolering med puds), med dets tykkelse og materiale. **Fra** og **Op til** giver det en egen højde (en stensokkel op til 600, puds over den), og lagets knap **lag** skifter dets materiale i bånd op ad højden. Til en beklædning, der står fri af væggen, tegner du en væg for sig på fladen (vægværktøjets linjevalg **Yderside**) med en type af beklædningens lag. Beklædning lagt bræt for bræt (lodrette eller vandrette brædder, lægter) modelleres endnu ikke.

## Vægge under et tag

Vægge på samme niveau som et tag stopper selv under det og går op til dets gavle. For en væg på et andet niveau: markér den og vælg **Arkitektur → Fastgør til tag** (<kbd>AR</kbd>), og klik derefter på taget — eller et hvilket som helst andet legeme, væggen skal gå op til: et Studio-legeme, et importeret element. Se [Dæk og tage](floors-roofs.md).
