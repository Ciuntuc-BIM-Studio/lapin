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
- **Facadeglasvæg** (<kbd>CW</kbd>) tegner en glasvæg med sprosser og paneler.
- Vægge sammenføjes af sig selv, hvor de mødes: et hjørne som geringssamling, en væg, der ender på en anden, som T, en, der krydser en anden, som X. Ved et T eller et X løber lagene ind i hinanden, som de bygges: den endende vægs kerne fortsætter gennem den andens overflader ind til dens kerne, og dens overflader stopper ved den andens flade, hvor de møder deres lige, så planen viser én opbygning uden en linje igennem. **Sammenføj** (<kbd>JW</kbd>), **Geringssamling** og **Stødsamling** ændrer, hvordan to vægge mødes, når det ikke er det, du vil; **Nye hjørner som gering** gør stødsamling til standard på denne computer. Uanset hvad går hvert lag selv rundt om hjørnet: overfladerne mødes med deres egne og løber ubrudt videre, og kernerne mødes på diagonalen (gering) eller den ene kerne løber ud til den andens yderside (stød), så en plan af vægge med lag viser rene hjørner.
- **Byt om** ændrer, hvilken af de to vægge der løber igennem.

**Linjen du tegner på.** Mens værktøjet Wall er aktivt, vælger listen i kommandolinjens bjælke den linje, du klikker på: **Kernens midte** (aksen, som før), **Kerne, yderside**, **Kerne, inderside**, **Yderside** eller **Inderside**. Ydersiden er væggens højre side, som du tegner (tegn mod uret rundt om bygningen, så den vender ud). Væggens akse bliver på kernen, forskudt fra linjen, og vægge tegnet efter hinanden på samme side mødes i den sides hjørne.

## At ændre en væg

Klik på en væg for at markere den. Dens greb vises. Træk i enderne for at forlænge eller afkorte den, eller træk i krydset i midten for at flytte den.

![En markeret væg med sine greb](shots/wall-selected.webp)

Egenskabspanelet viser den markerede væg. Øverst i panelet står dens type og lag. Derunder kommer længde, højde, fodforskydning, etage og fase. Ret en hvilken som helst værdi, og modellen følger med.

![En vægs egenskaber](shots/wall-properties.webp)

**Vend** vender væggens lag til den anden side. Du kan også klikke på de små pile, der vises ved siden af en markeret væg i planen.

## Vægtyper

En vægtype er et sæt lag, fra yderside til inderside. Hvert lag har et materiale og en tykkelse, og ét lag er **Kerne**. Væggens linje løber langs kernen. Du redigerer typerne under **Arkitektur → Vægtyper**. Et andet lag end kernen kan have sin egen højde: **Fra** og **Op til** angiver, hvor højt det går over væggens fod (fliser op til 1500, en beklædning fra 300); tomt går det i hele højden.

Et lag, der ikke er kernen, kan også skifte materiale op ad sin højde: knappen **lag** for enden af dets række tilføjer et stykke (fliser, 2100 som standard), fra lagets bund og op; tilføj flere for et bånd (maling til 900, fliser til 1500), og over det sidste stykke fortsætter laget i sit eget materiale til toppen. Hvert stykke tegnes, snittes i plan og snit og måles i sit eget materiale.

![Et pudslag med fliser til 2100 og pudsen ovenover](shots/wall-layer-parts.webp)

![Vægtyper: lagene i en murstensvæg](shots/wall-types.webp)

- **+ Layer** tilføjer et lag. Pilene flytter et lag op eller ned.
- **Zoner i højden** deler kernen op fra bund til top, for eksempel et første skifte, murværket og en ringbjælke.
- **Overliggere** vælger de overliggere, der sættes over døre og vinduer: præfabrikerede eller støbt på stedet, med deres vederlag og højde.
- **Tegn med den** lukker dialogen og begynder at tegne vægge af denne type.

En ændring af en type ændrer alle vægge af den type.

**Import…**, **Export CSV** og **Export JSON** under typelisten flytter væg- og dæktyperne fra ét projekt til et andet eller til et regneark og tilbage: CSV-filen har én række pr. lag (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), en types rækker efter hinanden, og Excels semikolonner og decimalkommaer læses også. Ved import får en type med samme navn filens lag, og de øvrige oprettes, i ét trin at fortryde; et materiale, projektet mangler, oprettes ud fra sit navn. Skemaet **Typeopbygninger** (Schedules → + → Type assemblies) viser hver væg- og dæktype med mærke, samlet tykkelse, hvor mange elementer der bruger den, og dens lag i rækkefølge.

## Vægge under et tag

Vægge på samme niveau som et tag stopper selv under det og går op til dets gavle. For en væg på et andet niveau: markér den og vælg **Arkitektur → Fastgør til tag** (<kbd>AR</kbd>), og klik derefter på taget — eller et hvilket som helst andet legeme, væggen skal gå op til: et Studio-legeme, et importeret element. Se [Dæk og tage](floors-roofs.md).
