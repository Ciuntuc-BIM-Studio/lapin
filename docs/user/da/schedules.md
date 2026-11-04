---
title: Skemaer og diagrammer
order: 95
summary: List døre, vinduer, overliggere, rum og alt, hvad et modul tæller; tilføj dine egne rækker og kolonner, skriv oven i modellens værdier, og lav et diagram af tabellen.
---

## Mængder

Alt, hvad Lapin modellerer, måles ud fra geometrien: en væg længde, højde, brutto- og nettosideareal og volumen, hvert lags areal og volumen, et dæks arealer og volumen, et rums areal og omkreds, antallet af døre. Der er tre veje:

- **Ét element.** Vælg det: afsnittet **Mængder** i egenskabspanelet viser, hvad det måler, derefter hvert lag eller hver del.
- **Hele modellen.** **Økonomi → Mængdeudtræk** laver (eller åbner) mængdeudtrækket: alle de elementer, modellen måler, lagt sammen række for række, med antal, længde, brutto- og nettoareal, brutto- og nettovolumen og vægt. Dets værktøjslinje vælger, hvad der lægges sammen: **Efter art og type** (standard: væggene af hver vægtype, dækkene af hver dæktype…), **Efter materiale** (hvert lag under sit materiale: væggenes tegl, puds og isolering hver for sig, overliggernes beton), efter etage eller kombineret. **Alle elementer** vælger, hvilke arter det viser (kun vægge og dæk, for eksempel). Mængdeudtrækket følger modellen som ethvert skema og tager egne rækker og kolonner (et tillæg, en enhedspris).
- **Enhver anden liste.** **Økonomi → Skemaer…** laver ethvert skema, udgaven tilbyder: rum, rumoverflader, døre og vinduer, typeopbygninger, tømmer, armering, forskalling, stål… Den samme liste ligger under **+** ved **Skemaer** i projektbrowseren.

![Mængdeudtrækket grupperet efter art og materiale](shots/quantity-takeoff.webp)

**Økonomi → Eksportér XLSX** gemmer mængdeudtrækket direkte som regneark (i en browser hentes det ned).

## Mængder på et ark

Et skema placeres på et ark som en visning og tegnes der med alle sine rækker og værdier, totaler og diagrammer:

- **Placér på tegningsark…** i skemaets værktøjslinje: **Nyt tegningsark** laver et A3-ark med tabellen og åbner det, eller vælg et ark, du har.
- Eller træk skemaet fra projektbrowseren hen på et åbent ark: det lander, hvor du slipper det. Planer, snit og facader trækkes på samme måde.
- Eller **Placér visning…** i arkets egen værktøjslinje.

På arket trækker du tabellen for at flytte den; en tabel, der er længere end rammen, fortsætter på flere sider (arket siger det, og **placér den på et nyt ark** fortsætter den). Tabellen følger modellen: tilføj en væg, og tallene på arket ændres. Den udskrives med **Udskriv** og kommer med i arkets PDF og DXF, præcis som den er tegnet.

![Mængdeudtrækket placeret på et nyt ark](shots/takeoff-sheet.webp)

## At lave et skema

Et skema er en tabel, som modellen udfylder: døre og vinduer, overliggere, rum, armering, træ, forskalling og mere. Klik på **+** ved siden af **Skemaer** i projektbrowseren, og vælg, hvad det skal liste, eller brug et moduls værktøj (Træ → Træliste, Byggepladsindretning → Forskallingsskema…). Dobbeltklik på det i browseren for at åbne det i en fane. Et skema følger modellen: flyt en væg, tilføj et vindue, og tabellen ændrer sig med dem.

Skemaer for **snedkerarbejder**, **døre**, **vinduer** og **overliggere** viser et kort for hver type, med dens tegning (som standard den indvendige visning) og dens værdier række for række. Andre skemaer er en almindelig tabel, én række pr. element og en totallinje til sidst.

En dør eller et vindue vendt til den anden hængsling af sin type (**Hængsling** eller én vendepil; vendt begge veje er den kun drejet) er spejlbilledet af sin type og får sit eget kort: dens mærke ender på **-M** (D-M), dens navn på "(mirrored)", og dens række **Opening**, tegning og antal er den anden hængslings. Vend den tilbage, så kommer den tilbage på sin types kort. Venstre og højre er IFC's: hængslerne set af den, der skubber den op. Tegningerne viser hver dør, som den står i modellen, set indefra, fra den side den åbner mod (så hængslerne på en dør *swing left* sidder til højre). En note under kortene siger det.

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

## Typeopbygninger

Skemaet **Typeopbygninger** (**+** → **Typeopbygninger**) viser hver væg- og dæktype: dens mærke, dens samlede tykkelse, hvor mange elementer der bruger den, og derefter dens lag i rækkefølge med materialer og tykkelser.

**Opbygningstegninger** i dets værktøjslinje tegner hver type efter tabellen, skåret gennem lagene i skemaets målestok (1:50, medmindre du vælger en anden): en væg som en plan skærer den, et dæk som et snit skærer det, lag 1 øverst. Hvert lag er udfyldt og skraveret som dets materiale i snit, kernens flader er tegnet kraftigere, den samlede tykkelse er målsat, og hvert lag er navngivet ved siden af, som tabellen navngiver det. Tegningerne udskrives med tabellen, på et tegningsark og i PDF'en. Et nyt skema over typeopbygninger tegner dem; fjern fluebenet ved **Opbygningstegninger** for kun at beholde tabellen.

Tegningerne **På hvert kort** (indvendig visning, udvendig visning, plan, snit) i et skemas **Indstillinger** hører til kortene i skemaerne over døre og vinduer; en almindelig tabel har ingen kort, så de tilbydes ikke der.

![Skemaet over typeopbygninger med hver types opbygning](shots/type-assemblies.webp)

## Vægprofiler

Skemaet **Vægprofiler** (**Arkitektur → Vægprofilskema**, eller **Økonomi → Skemaer…** → **Vægprofiler**) lægger projektets vægprofiler og vægnoter sammen efter profil, art og materiale: hvor mange der er, deres stykker (en profil, som åbninger afbryder, er flere), deres længde langs siderne og deres volumen; for en not det volumen, den tager ud af væggen. En sum for profilerne og en for noterne afslutter tabellen. Dens bjælke lægger dem først sammen **efter profil**, **efter etage** eller **efter vægtype**. Returer tæller med i volumen, ikke i længden.

## Eksport

**CSV** og **XLSX** i værktøjslinjen eksporterer skemaet som regneark (en XLSX har ét ark for hver tabel). I skrivebordsappen spørger Lapin, hvor filen skal gemmes, og foreslår skemaets navn; i en browser hentes filen ned. Eksporterede væg- og dæktyper gemmes på samme måde.
