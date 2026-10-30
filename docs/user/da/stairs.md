---
title: Trapper
order: 42
summary: Placér en trappe mellem etager, opsæt den i dens type, og lav de få trin op til en trappeafsats eller en dør.
---

## Trapper

Vælg **Arkitektur → Trappe** (<kbd>ST</kbd>). Klik på trappens fod (midten af dens første stødtrin), og derefter på et punkt i den retning, den stiger. Til en L- eller U-trappe angiver et tredje klik til venstre eller højre for løbet, hvilken vej den drejer. Trappen stiger fra sin etage til etagen ovenover. Lapin beregner antallet af stigninger, stigningen og grunden ud fra højden og typens regler.

Markér trappen for at ændre den i egenskabspanelet:

- **Trappetype.** Formen, reglerne, konstruktionen og gelænderet kommer fra typen, som opsættes under **Arkitektur → Trappetyper**.
- **Bredde**, **Drej**, **Fodforskydning** og **Topforskydning.**
- **Stigning.** Lad feltet stå tomt, så stiger trappen til etagen ovenover. En værdi giver trappen en egen stigning, uanset etagerne. Brug det til nogle få trin mellem to niveauer på samme etage.
- **Fast.** Antal stigninger, stigning, grund og reposlængde, når du vil have dem faste. Lad dem stå tomme, så reglerne bestemmer.
- **Kontroller.** Hvert tal holdt op mod typens regler, i grønt eller rødt.

En trappe beder dækket ovenover om en åbning over sig selv. Fjern fluebenet ved **Åbning i dækket ovenover**, hvis dækket skal forblive helt.

## Indgangstrin (en trappeafsats)

Trinene op til en hoveddør har en egen stigning: fra terrænet, nogle trin under stueetagen, op til ±0.00. Lapin har en færdig trappetype til dem, **Indgangstrin**: 2 m bred, tre stigninger på 150 mm, massiv beton støbt på terræn, uden gelænder.

![Indgangstrin foran en trappeafsats, i plan](shots/entrance-steps-plan.webp)

1. **Trappeafsatsen.** Tegn afsatsen som et dæk: vælg **Arkitektur → Dæk** (<kbd>SB</kbd>), og klik dens omrids uden for væggen, foran døren. Et dæks overside ligger i dets etages niveau (±0.00 på stueetagen), og typen giver dets tykkelse. Vil du ændre tykkelsen, så lav en dæktype til trappeafsatser under **Arkitektur → Dæktyper**, for eksempel 150 mm beton.
2. **Trinene.** Vælg **Arkitektur → Indgangstrin**. Klik på midten af afsatsens kant, og klik derefter på den side, trinene går ud til. Trinene kommer op til afsatsens kant og ender i ±0.00. De starter 450 mm lavere: deres **Fodforskydning** er −450 mm.
3. **Højden.** Markér trinene. Skriv under **Stigning** den reelle forskel mellem terrænet og afsatsen, for eksempel 300. Toppen bliver ved afsatsen, og foden flytter op: fodforskydningen bliver −300 mm, og trinene har to stigninger. **Bredde** gør dem lige så brede som afsatsen, for eksempel 2400.

![Indgangstrinenes egenskaber: stigning, fodforskydning, niveauer](shots/entrance-steps-properties.webp)

**Niveauer** viser, hvor trinene starter og slutter, for eksempel −0.45 → ±0.00. Med én stigning (en stigning på 150 mm) er trinet selve afsatsens kant, og der er ingen grund at bygge. Giv trinene to stigninger eller flere for at få grunde foran afsatsen.

Typen føjes til projektet, første gang du bruger den. Vil du ændre den, så åbn **Arkitektur → Trappetyper** og vælg **Indgangstrin**:

- **Form → En egen stigning.** Typens stigning; hver trappe kan stadig ændre den med **Stigning**.
- **Regler → Regelsæt → indgangstrin.** Stigninger på 120–170 mm og grunde på 300–400 mm, hvor en enkelt stigning er tilladt.
- **Konstruktion → Bygget som.** **Massive, støbt på terræn** fylder trinene ned til deres bund. **Bundplade under trinene** bygger dem som en indendørs trappe.

![Typen Indgangstrin i trappekonfiguratoren](shots/stair-types.webp)

Vil du have en anden trappetype til at stige en egen stigning, så sæt flueben ved **En egen stigning** på dens fane **Form**. Vil du lave en placeret trappe om til indgangstrin, så vælg **Indgangstrin** som dens **Trappetype**: den flytter ned, så dens top ligger i dens etages niveau.

![Huset med trappeafsats og indgangstrin i 3D](shots/entrance-steps-3d.webp)

I IFC er trinene en IfcStair af typen STRAIGHT_RUN_STAIR. Dens løb er en IfcStairFlight med sine NumberOfRisers, NumberOfTreads, RiserHeight og TreadLength, og Pset_StairCommon bærer de samme tal. Trappeafsatsen er en IfcSlab.
