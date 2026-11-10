---
title: Profilbiblioteker
order: 57
summary: De profiler, dit arbejde bruger, uanset standard — Lapins lille EN-sæt, dine tabeller og IFC-filer, firmaets mapper; tegnet med beregnede egenskaber og filens værdier kontrolleret, kopieret til projektet, efterhånden som de bruges.
---

Lapin kan ikke levere alle verdens stålprofiler, så det har et lille europæisk sæt med og læser resten fra det, du allerede har: et katalog som regneark (AISC Shapes Database, en producents tabel, firmaets egen), et IFC-profilbibliotek (fx Bonsais) eller en vilkårlig IFC-model, et lukket omrids tegnet i Lapin eller i en DXF. **Administrer → Profilbiblioteker** samler dem alle.

![Profilbiblioteker](shots/profile-libraries.webp)

## Bibliotekerne

- **Indbygget**: *EN steel (Lapin)*: IPE, HEA, HEB, HEM, UPN, UPE, lige- og ulige vinkler, varmfærdige SHS, RHS og CHS i almindelige størrelser samt generiske koldformede C, Z og Σ. Kun standardmål; alle egenskaber beregnes af Lapin.
- **På denne computer**: de biblioteker, du importerer, til alle projekter. Browserversionen gemmer dem også, i den browser.
- **Firmamapper**: i desktop-appen så mange lokale, netværks- eller synkroniserede mapper med `.lapinprofiles`-filer, du vil (**Tilføj en mappe…**). En mappe er skrivebeskyttet, til du sætter flueben ved **skrivbar**; så skriver gem-knappen ved siden af det valgte bibliotek dertil, til dine kolleger.
- **I projektet**: kun de profiler, projektet bruger, kopieres ind, med deres biblioteks navn, så filen kan åbnes overalt.

Skriv i søgefeltet for at søge i alle biblioteker på navn, familie, standard eller slags; vælg en profil for at se den tegnet med mål og egenskaber.

## Standarder

Hver profil vises under sin standard: **EN** for Lapins sæt og enhver profil, hvis standard er en europæisk norm (EN 10365, DIN EN, SR EN…), **AISC / ASTM** for amerikanske tabeller (f.eks. en *Standard*-kolonne med ASTM A6), BS, DIN, JIS, AS/NZS og de andre efter deres navne, og et bibliotek, hvis profiler ikke angiver nogen — firmaets eget — under sit eget navn. Et bibliotek på denne computer kan få at vide, hvilken det vises under (**Vist under**, over dets liste).

Vælgerne viser først projektets egne profiler, derefter hver standard under sin overskrift. Knapperne øverst i listen skjuler eller viser en standard; dit valg gemmes på denne computer, for alle projekter (også i **Filer → Præferencer → Profiler**).

![Vælgeren grupperet efter standard, med sine knapper](shots/profile-picker-standards.webp)

Et projekt beholder kun de profiler, dets typer bruger, aldrig et helt bibliotek, så et bibliotek, du har, vejer intet i dets fil. Hvad et projekt tilbyder, er dets eget valg: **Standarder i dette projekt** under bibliotekerne markerer de standarder, dets vælgere viser; profilerne, der allerede er i det, bliver. **Filer → Præferencer → Profiler** vælger de standarder, et nyt projekt starter med (alle, medmindre du vælger).

![De standarder, et projekt tilbyder](shots/profile-project-standards.webp)

![De standarder, et nyt projekt tilbyder](shots/prefs-profiles.webp)

Lapin leveres ikke med AISC Shapes Database: AISC lader kun udviklere medtage den under en særskilt licensaftale, som et MIT-program ikke kan give videre. Hent den fra AISC, og importér den med **Tabel…**: dens kolonner genkendes.

## Egenskaber

Ud fra målene beregner Lapin areal, Iy, Iz, elastiske og plastiske modstandsmomenter, inertiradier, hovedakser og deres vinkel (vinkler, Z, ethvert usymmetrisk omrids), vridningskonstant It og hvælvingskonstant Iw, hvor der findes en formel, masse pr. meter ud fra materialets densitet og malingsareal pr. meter. De stemmer med de offentliggjorte tabeller inden for ca. 1 % (It og Iw for valsede I-profiler inden for 3 %; Iw for en U-profil med skrå flanger kun inden for 20 %). Et vilkårligt omrids har kun It og Iw, hvis dets fil angiver dem.

En fils værdier bevares og vises ved siden af Lapins. En afvigelse over 2 % (areal, masse), 3 % (inerti- og modstandsmomenter) eller 10 % (It, Iw) markeres og kommer på importens kontrolliste; en hel kolonne, der afviger med en tierpotens, meldes at være i en anden enhed.

![En filværdi, der afviger fra Lapins](shots/profile-check.webp)

## Importér en tabel

**Tabel…** læser en CSV eller XLSX, én række pr. profil. Hver kolonnes betydning og enhed gættes ud fra overskriften på engelsk, rumænsk, tysk eller fransk (h, d, Höhe, hauteur, înălțime; tw, s, Stegdicke; A, Iy, Wel, Wpl, It, Iw, G, kg/m…), inklusive enheder (mm, cm, in, cm², cm⁴, in⁴, ×10³ cm⁶, lb/ft). En tabel med Ix og uden Iz læses på amerikansk: x er den stærke akse, i tommer. Ret det forkerte i dialogen; Lapin husker valget til den næste fil med samme opbygning.

![En tabels kolonner matchet](shots/profile-table.webp)

En rækkes slags kommer fra dens typekolonne (W, HP, UPN, HSS, PIPE…) eller navnet; for de øvrige vælges den i dialogen.

## IFC

**IFC…** læser alle profildefinitioner i en vilkårlig IFC-fil: et bibliotek eller en model fra Tekla, Revit eller ArchiCAD, hvor de profiler, elementerne bruger, får navn efter deres typer. I, asymmetrisk I, U, T, L, C, Z, hulprofiler, rektangel, cirkel og ellipse beholder slags og mål; vilkårlige, sammensatte og afledte profiler bliver omrids. De mekaniske værdier fra `Pset_ProfileMechanical` (og IFC2x3's profilegenskaber) følger med; eksportprogrammer er uenige om enheder og om, hvad Y-aksen er, så Lapin afgør begge dele ud fra geometrien. Centerlinje- og trapezprofiler udelades, og det siges.

![Profiler læst fra en IFC-fil](shots/profile-ifc.webp)

**IFC** og **CSV** under listen skriver det valgte bibliotek (eller projektets profiler) som et IFC4-profilbibliotek eller en tabel, Lapin kan læse igen.

Bjælker, søjler og stålprofiler skrives også i modellens IFC ved deres parametriske profil (`IfcIShapeProfileDef`, `IfcLShapeProfileDef`, …, rektangler og cirkler) med profilens navn og `Pset_ProfileMechanical`, så andre programmer ser en IPE 300 og ikke en polylinje. Konturer, Σ og Z med læber forbliver vilkårlige profiler; et element skåret af et andet forbliver det legeme, det er.

## Et omrids som profil

Tegn tværsnittet med Tegning som lukkede polylinjer og cirkler (dem inden i et andet er huller), markér dem og tryk **Fra markeringen**, eller læs en producents tegning med **DXF…**. Giv det et navn, vælg materiale og bibliotek, og gem det. Sådan kommer aluminiums- og PVC-profiler, præfabrikerede tværsnit og træformer ind: samme lager, med eget materiale og egen densitet.

![Et tegnet omrids gjort til profil](shots/profile-outline.webp)

**Tegn dens omrids** gør det modsatte: den valgte profils omrids som lukkede polylinjer i den forreste visning, så Studio kan ekstrudere eller feje det.

**Tegn en profil…** åbner tværsnitseditoren: ydersiden og dens kamre som punkter i millimeter. Træk punkterne (1 mm; <kbd>Shift</kbd> 5 mm), klik på et omrids for at tilføje et punkt, højreklik på et punkt for at fjerne det, eller skriv dem i tabellen; start fra et rektangel, en hul kasse, et T, en vinkel eller et U med de mål, du angiver, og tilføj kamre i midten. Giv den et navn, vælg aluminium, opret den og gem den som ethvert omrids. **Redigér dens kontur…** åbner en profil, du har, i editoren: gemt igen under sit navn markeres projektets kopi som nyere, og **Opdatér fra biblioteket** bringer den til hver type og hver facadeglasvægstang lavet af den. Valgte sprosser og tværposte tager en egen profil, drejet, justeret og forskudt: se [Vægge](walls.md).

![Tværsnitseditoren: en hul kasse med sit kammer](shots/profile-editor.webp)

## Brug en profil

I **Stål → Ståltyper**, **Konstruktion → Konstruktionstyper** (søjler og bjælker) og rammehallens profiler vælger du **Fra et profilbibliotek…** og skriver for at søge.

![Vælgeren søger i alle biblioteker](shots/profile-picker.webp)

Profilen kopieres til projektet, og typen beholder den. Kroppe er lette, med skarpe hjørner, på alle niveauer undtagen ét: en visning på **LOD 400** (plan, snit, opstalt, tegningsark eller 3D) tegner det virkelige tværsnit med hulkehler, afrundede flangespidser og skrå flanger. Eksporter og valg beholder den skarpe krop, men mængder, vægte, mængdeudtræk, listen over stålelementer og overslag bruger tværsnittets virkelige areal (en IPE 300 er 53,81 cm², ikke den skarpe kontur 51,88); et snit uden radier ændres ikke.

![En stålbjælke og -søjle i en 3D-visning på LOD 400: hulkehler tegnet](shots/profile-real-section.webp)

Andre steder giver profilen det, værktøjet kan bruge: vægprofiler, riller og stribefundamenter tager dens yderside som 2D-profil (**Fra et profilbibliotek…** i deres liste), og sprosser og tværposter i curtain walls samt karme og rammer i døre og vinduer tager dens virkelige kontur, med huller, langs hvert element, med dens bredde og dybde; profilens x går på tværs af elementet (mod glasset i karm og ramme), dens y ud af bygningen. **Enkel** fjerner konturen og beholder størrelsen.

![Sprosser og tværposter i en facadeglasvæg fra et aluminiumssnit](shots/profile-real-mullions.webp)

![Et vindues karm og ramme fra samme snit](shots/profile-real-frame.webp)

Et bibliotek kan ændre sig, efter du har brugt det. En projektprofil, der afviger fra den med samme navn i dens bibliotek, markeres **nyere i biblioteket**; **Opdatér fra biblioteket** erstatter den, og alle typer lavet af den følger med, som ét fortryd-trin. Intet ændres, før du beder om det. Ældre projekter beholder deres typer.
