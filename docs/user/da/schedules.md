---
title: Skemaer og diagrammer
order: 95
summary: List døre, vinduer, overliggere, rum og alt, hvad et modul tæller; tilføj dine egne rækker og kolonner, skriv oven i modellens værdier, og lav et diagram af tabellen.
---

## At lave et skema

Et skema er en tabel, som modellen udfylder: døre og vinduer, overliggere, rum, armering, træ, forskalling og mere. Klik på **+** ved siden af **Skemaer** i projektbrowseren, og vælg, hvad det skal liste, eller brug et moduls værktøj (Træ → Træliste, Byggepladsindretning → Forskallingsskema…). Dobbeltklik på det i browseren for at åbne det i en fane. Et skema følger modellen: flyt en væg, tilføj et vindue, og tabellen ændrer sig med dem.

Skemaer for **snedkerarbejder**, **døre**, **vinduer** og **overliggere** viser et kort for hver type, med dens tegning (som standard den indvendige visning) og dens værdier række for række. Andre skemaer er en almindelig tabel, én række pr. element og en totallinje til sidst.

![Et snedkerskema med rækker for brandmodstand og producent](shots/joinery-schedule.webp)

## Brandmodstand, producenter og andre parametre

En dørs eller et vindues parametre kommer selv med i snedkerskemaet:

- **Brandmodstand.** Rækken **Brandmodstand** læser en parameter ved navn Fire rating, Fire Rated, FireRating eller Fire resistance. Sæt den på typen, så alle typens døre har den. I **Arkitektur → Døre og vinduer** tilføjer **Parametre · i skemaer** → **+ Brandmodstand** typens FireRating til Pset_DoorCommon (Pset_WindowCommon for et vindue), hvor IFC holder den. En parameter, du selv tilføjer til én dør i dens egenskaber, vises også.
- **Enhver anden parameter.** **+ Producent**, **+ Varenummer**, **+ Websted** eller en parameter, du selv navngiver, bliver sin egen række i alle snedkerskemaer. En dørs egen værdi vises ved siden af typens, når døre af samme type er forskellige.
- **Skrevet i skemaet.** En værdi, der skrives i en parameters række (i kursiv), går ind i typen, og alle typens døre følger den. Tøm den for at fjerne parameteren fra typen.

![Typens parametre i konfiguratoren for døre og vinduer](shots/joinery-params.webp)

**Ubrugte typer.** En type, som ingen dør eller intet vindue i modellen bruger, udelades af skemaet. **Ubrugte typer** i skemaets værktøjslinje viser dem igen, med hvor mange der er.

## Dine egne rækker og kolonner

Ethvert skema kan udfyldes i hånden, som et regneark:

- **Række** tilføjer din egen række. I et snedkerskema har den en betegnelse, og du skriver dens værdi for hver type (Leverandør, Leveringsdato…). I ethvert andet skema er den en hel række, skrevet celle for celle (en terrasse, der ikke er modelleret, en reserve); den står over totallinjen.
- **Kolonne** tilføjer din egen kolonne. I et snedkerskema er den et kort uden nogen type bag, hver værdi skrevet (en garageport bestilt for sig). I ethvert andet skema er den en kolonne efter modellens (Gulvbelægning, Noter…).
- **Enhver celle** i tabellen under tegningen kan overskrives. En værdi skrevet oven i modellens er markeret; **↺** giver modellens værdi tilbage. Det, du skriver, bliver ved sin række: et rums noter følger rummet, når listen sorteres eller rummet omdøbes. I en **Rooms**-liste går et nummer, navn, en funktion, afdeling, gulvbelægning eller højde skrevet i et rums række ind i selve rummet, som var det skrevet i dets egenskaber; planens stempel og alle andre lister følger med.
- **×** ved siden af din egen række eller kolonne fjerner den, med det, der var skrevet i den.
- Pilene i en tabels overskrift **sorterer** rækkerne efter den kolonne: én gang stigende, igen faldende, tredje gang i modellens rækkefølge. Totallinjen bliver sidst.

Det hele udskrives: på et tegningsark, i PDF og DXF og i eksporten til CSV og XLSX.

## Diagrammer

**Diagrammer** i skemaets værktøjslinje åbner diagrammerne ved siden af tabellen. **Nyt diagram** laver et ud fra tabellen:

- **Søjler**, **Liggende søjler**, **Cirkel** eller **Linje**.
- **Kategorier fra**: kolonnen, der navngiver hver søjle eller hvert udsnit (Funktion, Navn, Mærke, Etage…).
- **Hvordan**: **Sum**, **Gennemsnit**, **Mindste** eller **Største** af en kolonne (Areal, I alt, Volumen…), eller **Antal rækker**.
- **Højst**: beholder kun de største kategorier og lægger resten sammen som Andet.
- **Titel**: sin egen titel, eller den, det selv laver (Sum af Areal efter Funktion).

Motoren tegner hvert diagram efter tabellerne, så det vises i skemaet, på et tegningsark, der placerer skemaet, og i PDF'en nøjagtigt ens. Det følger modellen og det, du har skrevet: ændr et rums areal, eller tilføj din egen række, og diagrammet ændrer sig med dem. **✎** ændrer et diagram, **×** fjerner det.

![Et rumskema med sin egen række og et cirkeldiagram over arealerne efter funktion](shots/schedule-chart.webp)
