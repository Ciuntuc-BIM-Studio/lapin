---
title: Tegningsark, PDF og DXF
order: 100
summary: Sæt visninger op på tegningsark med et tegningshoved, og udskriv dem til PDF eller DXF.
---

<!-- video sheets -->

## At lave et tegningsark

Klik på **+** ved siden af **Tegningsark** i projektbrowseren. Vil du placere en visning, så træk den fra browseren over på arket, eller vælg den under **Placér visning…** i arkets bjælke. Hver visning beholder sin egen skala og forbliver forbundet med modellen, så arket ændres, når modellen gør. På arket trækker du en visning for at flytte den: kun den flytter sig, med sin titel, mens rammen, tegningshovedet og de andre visninger bliver; den fanger rammens kanter og midte og de andre visninger, og piletasterne rykker den 1 mm (10 mm med Shift).

![Et A3-ark med en plan og et snit](shots/sheet.webp)

Markér arket for at udfylde dets **Tegningshoved** i egenskabspanelet: projektet, bygherren, hvem der har tegnet det, hvem der har kontrolleret det, datoen og revisionen. Dets nummer, papir og retning indstilles også der. Efterlades projektfeltet tomt, viser det projektets navn: det navn, projektet har fået, ellers dets filnavn.

## Hvad en visning viser

En visning på tegningsarket viser det, du vælger, uden at du skal trække den i blinde:

- **Beskær den på arket.** Klik på en visning: der kommer håndtag i hjørnerne og på kanterne. Træk i dem, og rammen tager mere eller mindre af visningen, mens tegningen bliver, hvor den er på papiret. **Skift**-træk inde i visningen skubber tegningen i rammen.
- **Tegn dens grænse i visningen.** **Annotate → View boundary** (<kbd>VB</kbd>): tegn et rektangel på en plan, et snit eller en facade og giv det et navn, entydigt i projektet. I **Place view…** tilbydes visningen så med hver af sine grænser (»Ground floor plan › Østfløj«), og en visning placeret sådan viser netop det rektangel og følger det, når du flytter eller ændrer det i visningen. Samme visning kan stå på flere ark, én grænse hver. En placeret visning skifter mellem sine grænser, eller til fri beskæring, i listen på dens ramme. Grænser ses kun på skærmen, aldrig på papir.
- **Fastgør den.** Nålen på rammen holder visningen på plads: den kan vælges, men flyttes eller beskæres ikke ved en fejl.

Rammen og titelfeltet hører til papiret. De vælges eller flyttes aldrig, og et klik tager den mindste visning under markøren, så en visning lagt over titelfeltet altid kan vælges.

![En visning valgt på arket: dens beskæringshåndtag, dens grænse, nålen](shots/sheet-view-boundary.webp)

## Udskrivning

Knapperne **PDF** og **DXF** i arkets bjælke eksporterer arket. En PDF er klar til at blive udskrevet eller sendt. En DXF åbner i AutoCAD og andre CAD-programmer med sine lag, linjer og skraveringer. **Udskriv** sender arket til en printer eller plotter i dets egen papirstørrelse, 1:1, som vektorlinjer: vælg plotter og papir i udskriftsdialogen. På en mindre printer vælger du tilpas til siden.
