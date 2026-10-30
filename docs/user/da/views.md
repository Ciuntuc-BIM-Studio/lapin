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

Markér snittet i browseren for at indstille dets skala, dybde, fagområde og stil i egenskabspanelet.

## Facader

**+** ved siden af **Facader** laver bygningens fire facader på én gang.

![Sydfacaden](shots/elevation-view.webp)

## 3D-visninger

<!-- video section-3d -->

Dobbeltklik på **3D** for at åbne 3D-visningen. Træk med venstre knap for at kredse om modellen. Oversigtsplanen øverst til højre viser, hvorfra du kigger. **Plan**, **Persp** og **Tilpas** ændrer kameraet. Vil du gemme et kamera, så duplikér 3D-visningen.

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

## Hvad en visning viser

Fanen **Vis** indstiller, hvad visningen forrest viser, og hvordan den ser ud.

![Fanen Vis](shots/ribbon-view.webp)

- **Visningsindstillinger.** Visningens fagområde og stil, dens område (snithøjde og -dybde) og dens grafik.
- **Visningsstile.** Architectural, Structural (kun kernerne), MEP eller dine egne. Hver stil vælger de lag, der tegnes (alle, uden overflader eller kernen), og hvordan materialer samles.
- **IFC 2D-symboler.** Elementer importeret eller linket fra en IFC-fil, som har deres egen plantegning med (et fodaftryk, en dørs slag, et inventars symbol, med filens farver, udfyldninger og tekster), tegner den i planer i stedet for deres snit. Fjern fluebenet under Visningsindstillinger (eller i en visningsstil) for i stedet at tegne deres 3D-snit.
- **Synlighed** (<kbd>VV</kbd>) og **Grafik** (<kbd>VG</kbd>). De viser eller skjuler kategorier og indstiller deres penne, linjetyper og udfyldninger.
- **Opdel** og **Vindue.** De viser to visninger side om side eller åbner en visning i et separat vindue.
