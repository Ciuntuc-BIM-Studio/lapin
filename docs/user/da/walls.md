---
title: Vægge
order: 20
summary: Tegn vægge hjørne for hjørne, ret dem i egenskabspanelet, og opsæt vægtyper med deres lag.
---

<!-- video walls -->

## At tegne vægge

Åbn fanen **Arkitektur**, og vælg **Væg**, eller skriv <kbd>WA</kbd>.

![Fanen Arkitektur](shots/ribbon-architecture.webp)

Klik, hvor væggen starter, og klik derefter ved hvert hjørne. Den næste væg følger musen. Mens du tegner, kan du skrive en længde og en vinkel i felterne over visningen i stedet for at klikke. Tryk på <kbd>Enter</kbd> eller <kbd>Esc</kbd> for at stoppe.

![At tegne vægge: den næste væg følger musen, med længde og vinkel vist](shots/wall-drawing.webp)

Hvor to vægge mødes, samler Lapin dem selv. Deres lag løber ind i hinanden, sådan som de ville blive bygget.

- **Buet væg** (<kbd>WC</kbd>) tegner en væg langs en bue.
- **Facadeglasvæg** (<kbd>CW</kbd>) tegner en glasvæg med sprosser og paneler.
- **Sammenføj** (<kbd>JW</kbd>), **Geringssamling** og **Stødsamling** ændrer, hvordan to vægge mødes.
- **Byt om** ændrer, hvilken af de to vægge der løber igennem.

## At ændre en væg

Klik på en væg for at markere den. Dens greb vises. Træk i enderne for at forlænge eller afkorte den, eller træk i krydset i midten for at flytte den.

![En markeret væg med sine greb](shots/wall-selected.webp)

Egenskabspanelet viser den markerede væg. Øverst i panelet står dens type og lag. Derunder kommer længde, højde, fodforskydning, etage og fase. Ret en hvilken som helst værdi, og modellen følger med.

![En vægs egenskaber](shots/wall-properties.webp)

**Vend** vender væggens lag til den anden side. Du kan også klikke på de små pile, der vises ved siden af en markeret væg i planen.

## Vægtyper

En vægtype er et sæt lag, fra yderside til inderside. Hvert lag har et materiale og en tykkelse, og ét lag er **Kerne**. Væggens linje løber langs kernen. Du redigerer typerne under **Arkitektur → Vægtyper**.

![Vægtyper: lagene i en murstensvæg](shots/wall-types.webp)

- **+ Layer** tilføjer et lag. Pilene flytter et lag op eller ned.
- **Zoner i højden** deler kernen op fra bund til top, for eksempel et første skifte, murværket og en ringbjælke.
- **Overliggere** vælger de overliggere, der sættes over døre og vinduer: præfabrikerede eller støbt på stedet, med deres vederlag og højde.
- **Tegn med den** lukker dialogen og begynder at tegne vægge af denne type.

En ændring af en type ændrer alle vægge af den type.

## Vægge under et tag

Vægge på samme niveau som et tag stopper selv under det og går op til dets gavle. For en væg på et andet niveau: markér den og vælg **Arkitektur → Fastgør til tag** (<kbd>AR</kbd>), og klik derefter på taget — eller et hvilket som helst andet legeme, væggen skal gå op til: et Studio-legeme, et importeret element. Se [Dæk og tage](floors-roofs.md).
