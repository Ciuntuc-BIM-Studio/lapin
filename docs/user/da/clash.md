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

> Lapins egne installationsføringer kan selv skære deres åbninger i vægge og dæk (Administrer → Snitrækkefølge). Så er et rør gennem en væg ikke en kollision: væggen har sin åbning. **Administrer → Kollisionskontrol…** kontrollerer de åbninger.
