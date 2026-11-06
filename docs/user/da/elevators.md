---
title: Elevatorer
order: 43
summary: Elevatorer fra en konfigurator med tolv typer dimensioneret efter EN 81-20 og EN 81-70, med skakt, grube, skakttop, maskinrum, stol- og etagedøre, åbningerne i de dæk, de går igennem, planer på hver etage, en liste og IFC.
---

## Placér en elevator

**Arkitektur → Elevator** (<kbd>LF</kbd>) sætter en elevator med et klik i planen. Klikket er skaktens midte, og dørene vender nedad i planen (drej den med 0°, 90°, 180° eller 270° i bjælken, eller senere med dens greb). Elevatoren går fra planens etage til projektets øverste etage og stopper på hver etage imellem. Dens konfigurator åbner med det samme.

Mens værktøjet er aktivt, viser kommandolinjens bjælke og egenskabspanelet **den stil**, det sætter: en af katalogets tolv typer eller en af projektets egne stile.

## Konfiguratoren

![Elevatorkonfiguratoren med den kørestolsegnede stil](shots/elevator-configurator.webp)

Stilene står til venstre. En stil udfylder alle felter, og du kan derefter ændre ethvert felt i hånden. Kataloget har disse typer:

| Stil | Personer | Mærkelast | Stol (mm) | Drift |
|---|---|---|---|---|
| Lille boligelevator | 4 | 320 kg | 900 × 1000 | uden maskinrum |
| Personelevator standard | 6 | 450 kg | 1000 × 1250 | uden maskinrum |
| Personelevator mellem | 8 | 630 kg | 1100 × 1400 | traktion, maskinrum øverst |
| Personelevator stor | 10 | 800 kg | 1350 × 1400 | traktion |
| Personelevator, stor trafik | 13 | 1000 kg | 1600 × 1400 | traktion, 1,6 m/s |
| Kørestolsegnet | 8 | 630 kg | 1100 × 1400, dør 900 | EN 81-70 type 2 |
| Sengeelevator (hospital) | 17 | 1275 kg | 1200 × 2300 | traktion, gennemgående stol |
| Godselevator | 26 | 2000 kg | 1500 × 2700 | traktion, 0,5 m/s |
| Hydraulisk (lav bygning) | 8 | 630 kg | 1100 × 1400 | stempel, aggregatrum ved skakten |
| Uden maskinrum (MRL) | 13 | 1000 kg | 1100 × 2100 | maskinen i skakttoppen |
| Panoramaelevator i glas | 8 | 630 kg | 1100 × 1400 | glasstol i en glasskakt |
| Hjemmelift | 3 | 250 kg | 800 × 950 | 0,15 m/s, lav grube |

Til højre er der tre faner:

- **Stol og last**: driften (traktion, uden maskinrum, hydraulisk, hjemmelift), mærkelast og personer, hastigheden, tilgængelighedstypen (EN 81-70 type 1, 2 eller 3), stolens bredde, dybde og højde, stolevægge af glas, en brandmandselevator, en evakueringselevator. **Last og personer ud fra stolen** sætter den mindste mærkelast, EN 81-20 giver stolens areal, og de personer, den bærer.
- **Døre og stop**: én adgang eller to (en gennemgående stol), dørens bredde og højde, central- eller sideåbning og dens blade (to eller tre blade glider bag hinanden), det øverste stop og de etager, den stopper på. En etage, den passerer uden at stoppe, beholder skaktvæggen lukket og har ingen etagedør.
- **Skakt**: modvægtens side (eller stemplets for en hydraulisk elevator), gruben under nederste stop og skakttoppen over det øverste (begge følger driften og hastigheden, indtil du skriver dine egne), maskinrummet (øverst ved traktion, ved siden af skakten forneden for en hydraulisk, ellers intet), skaktvæggene (en vægtype fra projektet eller beton i en tykkelse, du angiver), en glasskakt, drejningen.

![Snittet gennem stolen, fra gruben til maskinrummet](shots/elevator-section.webp)

Forhåndsvisningen viser planen ved nederste stop og, på de andre faner, et snit gennem stolen med hvert stop (betjente stop i rødt, passerede stiplet). Nedenunder står, hvad EN 81 siger: en stol større end dens mærkelast tillader, flere personer end lasten eller arealet bærer, en tilgængelig stol eller dør for lille, en hydraulisk elevator med mere end ca. 18 m løftehøjde. Derunder skaktens frie og udvendige mål, stolens areal mod det, lasten tillader, stoppene, løftehøjden og de dæk, skakten åbner.

**Anvend** ændrer elevatoren i ét fortryd-trin. **Gem som stil…** gemmer den som en stil i projektet; **Opdatér stilen** gemmer den igen, og alle elevatorer med den stil følger med. En projektstil kan omdøbes eller slettes (dens elevatorer forbliver som de er).

## På tegningerne

![En elevator i plan: skakten snittet, stolen med sit kryds, modvægten og etagedørene](shots/elevator-plan.webp)

Skakten åbner hvert dæk, den går igennem, med sin egen åbning i hvert dæk. Når du flytter, drejer eller kopierer elevatoren eller ændrer dens etager, følger åbningerne med. Et dæk tegnet efter elevatoren åbnes med **Arkitektur → Skaktåbninger**.

Hver plan af en etage, elevatoren når, tegner den: skaktvæggene snittet, stolen med sit diagonale kryds, modvægten med sin diagonal (en hydraulisk elevators stempel som en cirkel), og på de etager, den stopper på, etagedørens og stoldørens blade, stiplet hvor de glider hen, når de er åbne. Snit og facader tegner hele skakten, gruben, toppen, maskinrummet, stolen ved nederste stop og etagedørene. I 3D står stolen ved nederste stop med dørene lukket.

![En elevator i 3D med maskinrummet over huset](shots/elevator-3d.webp)

## Egenskaber, liste og IFC

![En elevators egenskaber](shots/elevator-properties.webp)

Markér en elevator for at ændre dens nederste og øverste stop, afkrydse de etager, den stopper på, vælge skaktens vægtype, læse EN 81's advarsler og åbne **Konfigurator…**. Dens felter (drift, last, stol, døre, grube, skakttop…) står nedenunder; dem, der ikke gælder, skjules (intet maskinrum for en elevator uden maskinrum, ingen modvægtsside for en hydraulisk). Træk det midterste greb for at flytte den og grebet foran dørene for at dreje den. **Flyt**, **Kopiér**, **Rotér** og **Spejl** virker som for ethvert element; en spejlet elevator får modvægten på den anden side.

**Arkitektur → Elevatorliste** viser hver elevator med type, drift, mærkelast, personer, stol, døre, de stop, den betjener, de etager, den går imellem, løftehøjde, hastighed og skakt.

Hver elevator har en type dannet af art, last og stol (Personelevator mellem 630 kg, 1100 × 1400): ens elevatorer deler den, så en prispost sat på typen (**Udførelse → Tildel poster…**) prissætter dem alle, og planlægningen finder dem ud fra deres klasse (IfcTransportElement).

I IFC er en elevator et **IfcTransportElement** med den foruddefinerede type **ELEVATOR**, af en **IfcTransportElementType**, med IFC's egne **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) og **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). Lapin læser sin egen fil tilbage som en native elevator med dens stop og dækkenes åbninger.
