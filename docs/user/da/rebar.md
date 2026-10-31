---
title: Armering 2D
order: 52
summary: Armering tegnet på snit og planer, tværsnit skåret, hvor du klikker, og armeringslisten.
---

Fanen **Armering 2D** tegner armering, som en armeringsliste laves: stænger som former med deres mål, bøjler fordelt langs en bjælke, mærker i cirkler og en liste, der lægger dem sammen. Den virker i enhver visning: et snit, en plan, en facade.

![Fanen Armering 2D](shots/ribbon-rebar.webp)

## Et snit, hvor du arbejder

Armering har brug for tværsnittet ved siden af længdesnittet. **Snit her** gør begge dele: vælg en bjælke, en søjle eller en væg i den visning, du er i, og hvor du klikker, skæres et snit *på tværs*, vinkelret på dens akse, og tegnes ved siden af dit klik, i samme visning, med en halv meter af omgivelserne (dækket, søjlen). En søjle skæres vandret i den højde, du klikkede.

Den indsatte visning følger elementet: flyt bjælken, og snittet flytter med. Træk den indsatte visning i dens greb; dens egenskaber giver den en skalafaktor og en ramme. **Tværsnit** laver samme snit som sin egen visning, og **Indsat visning** sætter enhver eksisterende visning ind i den, du er i, i sand størrelse og i værtsvisningens målestok, så stænger tegnet ovenpå passer til betonen.

![En bjælkes længdesnit med dens tværsnit indsat og dens armering](shots/rebar-section.webp)

## Stænger

**Lige stang**, **L-stang**, **U-stang**, **Bøjle**, **Binder** og **Polylinjestang** sættes med et klik (polylinjen ved dens punkter). Hver er en form med parametre: træk i et greb, og det mål, det står for, følger; skriv det i egenskabspanelet i millimeter. Stængerne tegnes i deres tykkelse, i en farve pr. diameter (Armeringsindstillinger), og bukkes med den radius, SR EN 1992-1-1 kræver: dorn 4Ø til og med Ø16, 7Ø derover.

Kroge sættes i hver ende: 90°, 135° eller 180°, deres lige længde efter praksis (12Ø, 10Ø, 5Ø, aldrig under 50 mm), på den anden side, når du beder om det. En bøjle fødes med to 135°-kroge vendt indad.

**Stang i snit** er en stang set fra enden: en prik i dens diameter, med den længde, den har langs stangen. **Samme stang i snit** sætter den valgte stang som prik et andet sted uden at tælle den to gange. En **Række** i egenskaberne gentager en stang for hver så mange millimeter, som stængerne i et lag i et tværsnit.

Mærkets cirkel viser mærkenummeret, og hvad stangen er: `4Ø12 L=2450`. Træk cirklen, hvor den læses bedst. Længderne er de nominelle, målt på stangens akse mellem dens hjørner, med kroge; den udfoldede længde om bukningerne står i egenskabspanelet.

## Bøjler på bjælke- eller søjletypen

Bøjler tegnes ikke én ad gangen: de hører til **typen**. Under Konstruktion → Konstruktionstyper får en rektangulær bjælke- eller søjletype **Bøjler på denne type**: deres diameter, dæklaget og fordelingen — ét trin hele vejen eller **tre zoner**, hver med sin del af længden (en tredjedel som standard) og sit trin, tættere i enderne. Hvert element af typen viser dem så i ethvert snit, som visningen ligger til aksen: rektanglet inden for dæklaget i tværsnittet, en linje pr. bøjle i længdesnittet, linjer over bjælken i en konstruktionsplan; med etiketten `41Ø8/150` og mærkets cirkel. De tælles med i armeringslisten under typens mærke. **Længdestængerne** sidder ikke på typen (de løber over flere fag): tegn dem i længdesnittet eller i planen, med deres kroge. En stang tegnet inde i en bjælkes snit hører til den bjælke og står i rummet: **På tværs af tegningen** lægger den så mange gange i visningens retning, **efter dæklaget** (den første og den sidste ved fladerne, inden for dæklag og bøjle, resten jævnt fordelt) eller **med et trin**. Tværsnittet viser så stængerne som prikker, hvor de er, en konstruktionsplan som linjer, og listen tæller dem alle: `3Ø16 L=6400`. Legemer i 3D-visningen er ikke med i denne udgave.

## Bøjler langs en bjælke

**Bøjler langs** tager de to ender af en vej; bøjlerne fordeles, så én står i hver ende, med det trin, du sætter (150 mm som standard). **Zoner** ændrer trinnet undervejs: **Tredjedele** sætter i hver ende et halvt så stort trin, som en seismisk bjælke kræver; etiketten siger `25Ø8/100-200`. Vis dem alle, kun enderne og midten med en stiplet linje imellem, eller én.

## Listen

**Armeringsliste** laver en liste over hver stang efter mærke: diameter, form, antal, længde af én, samlet længde og masse, med summer pr. diameter. Et mærke, hvis stænger afviger i diameter eller længde, noteres. Det er en liste som de andre: på ark, i CSV og XLSX.

**Armeringsindstillinger** gemmer projektets valg: en farve pr. diameter, tekst- og cirkelstørrelser på papir, stålet, dæklaget og segmentlængderne skrevet langs stængerne.
