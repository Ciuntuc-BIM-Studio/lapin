---
title: Kollisionsdetektion
order: 58
summary: Find hvor modellens elementer krydser hinanden eller kommer for tæt på: søgesæt efter regler, hårde og afstandstest mellem dem, kollisioner fulgt fra kørsel til kørsel, vist i 3D og sendt som BCF.
---

**Administrer → Kollisionsdetektion** finder, hvor modellens elementer krydser hinanden eller kommer tættere på, end de skal. Den virker ens på Lapins egne elementer og på linkede IFC- og Fragments-filer, så den koordinerer også de modeller, andre fag sender dig.

![Kollisionsdetektion ved siden af 3D-visningen](shots/clash-dialog.webp)

## Test og sæt

En **kollisionstest** sammenligner to **søgesæt**: sæt A med sæt B eller et sæt med sig selv. Et projekt starter med tre sæt (Structure, Services, Architecture) og tre hårde test: Structure × Services, Services × Services og Architecture × Services.

- **Hårde** test finder elementer, der krydser hinanden. Deres **tolerance** (5 mm som standard) ser bort fra så små krydsninger, så en beklædning lagt 3 mm ind i en væg ikke meldes.
- **Afstands**test finder elementer, der er tættere end en afstand (50 mm som standard), og også krydsninger.

Nogle par testes aldrig, fordi de skal mødes: en væg og døren eller vinduet i dens åbning, delene af én samling, vægge samlet i et hjørne og elementer fastgjort til hinanden (en bjælke på en søjle, en væg fastgjort til et tag).

![En tests indstillinger](shots/clash-test.webp)

## Kør og følg kollisioner

**Kør test** kører testen forrest; **Kør alle** kører hver test, der er slået til. Intet kører af sig selv: resultaterne bliver i projektet, til du kører testen igen, og dialogen siger, når modellen er ændret siden.

Hver kollision beholder sit nummer fra kørsel til kørsel. En kollision fundet første gang er **Ny**; fundet igen er den **Aktiv**. Marker den selv som **Gennemgået** eller **Godkendt**, giv den til nogen (**Tildelt**) og skriv **Noter**. En kollision, som en kørsel ikke længere finder, bliver **Løst**; kommer den igen, er den åben igen med samme nummer.

Klik på en kollision for at se den i 3D. Lapin vælger de to elementer, lægger en snitkasse om stedet, hvor de mødes, og ser på dem fra siden, så du ser røret gennem væggen og ikke væggens flade. **alene i 3D** skjuler resten; pilene går gennem kollisionerne én ad gangen. Dialogen bliver til højre, så du kan flytte kameraet, mens den er åben.

![Et rør gennem en væg, indrammet af kollisionen](shots/clash-3d.webp)

**BCF** gør hver åben kollision (eller de valgte) til en bemærkning af typen Clash med sit synspunkt og gemmer dem som en BCF-fil til andre BIM-programmer; sendes de igen, opdateres de samme bemærkninger. **CSV** gemmer listen til et regneark.

## Søgesæt

**Søgesæt** rummer reglerne, der vælger et sæts elementer. En regel læser et felt af elementet (klasse, foruddefineret type, type, etage, materiale, navn, linket fil eller en hvilken som helst parameter) med en operator: er, er en af, er ikke, er ingen af, indeholder, indeholder ikke, mindst, højst, mellem. Tal læses, som folk skriver dem: "600 mm" er 600.

Et sæt tager elementerne, der opfylder en regel, eller alle regler. En "ikke"-regel tager altid elementer ud, og et sæt uden en regel, der tilføjer elementer, rummer alt. Tallet ved sættet viser, hvor mange elementer reglerne tager, mens du skriver.

![Et søgesæts regler](shots/clash-sets.webp)

## Grupper, rapporter og skyer

Listen grupperer sine kollisioner **efter etage**, **efter element** (elementet i flest kollisioner tager dem, så det næste), **efter klasser** eller **efter status**. En gruppes afkrydsning vælger alle dens kollisioner, så de kan få en status sammen.

![Kollisioner grupperet efter element](shots/clash-groups.webp)

- **XML** skriver kollisionerne, som Navisworks skriver sine kollisionsrapporter, i deres grupper, så andre koordineringsværktøjer kan læse dem.
- **PDF** udskriver listen på A4, grupperet på samme måde.
- **HTML** laver én side med et billede af hver åben kollision, indrammet i 3D-visningen efter tur.
- **Markér i visningen** tegner de åbne kollisioner i planen, snittet eller facaden forrest som røde skyer mærket test.kollision (2.14: den anden test, kollision 14), kun på skærmen. Et klik på en sky vælger elementet.

![Kollisioner som skyer i en plan](shots/clash-marks.webp)

Et projekt starter også med en fjerde test, **Openings for MEP**: kontrollen fra Administrer → Kollisionskontrol… kørt som en test, så dens problemer nummereres og følges som de andre. Dens dybde er, hvor meget en føring mangler i sin åbning.

En hård test finder også et element helt inde i et andet, hvor ingen flader krydser: et rør glemt inde i et dæk. Dybden er det inderste elements mindste bredde.

> Lapins egne installationsføringer kan selv skære deres åbninger i vægge og dæk (Administrer → Snitrækkefølge). Så er et rør gennem en væg ikke en kollision: væggen har sin åbning. **Administrer → Kollisionskontrol…** kontrollerer de åbninger.

Kollisionsdetektionen følger design, tal og test fra [BimGo](https://github.com/aussieBIMguru/BimGo) af Aussie BIM Guru (MIT-0), skrevet om i Lapin.
