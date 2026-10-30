---
title: Projektmappe og udgivelser
order: 125
summary: Udgiv ark og modellen, som ISO 19650 kræver, beholdt i projektfilen og, når du har en, i en projektmappe, navngivet efter konventionen, med følgebrev, efter de kontroller en tegnestue laver.
---

## Projektmappen

**Manage → Project folder** angiver den mappe, projektet også udgives til: en mappe på din computer, et netværksdrev eller en SharePoint- eller OneDrive-mappe synkroniseret til disken. Mappen er **valgfri**: hver udgivelse og dens filer beholdes i selve projektfilen, så et projekt uden mappe udgiver lige så godt, i desktop-appen og i browseren. Lapin beder ikke om nogen konto og taler ikke med nogen server: den skriver filer, og hvad der synkroniserer mappen, er dit kontors sag. Stien gemmes i projektet, så den rejser med filen; en mappe, der ikke er på denne computer, er en advarsel, ikke en fejl. At vælge mappen kræver desktop-appen.

![Projektmappe: mappen, containerne, navngivningen](shots/project-folder.webp)

I mappen opretter Lapin ved første udgivelse de fire ISO 19650-containere: **WIP**, **Shared**, **Published** og **Archived**. Statuskoden foreslår containeren: S0 går til WIP, S1–S7 til Shared, A, B og CR til Published.

## Navngivning

Filer navngives efter ISO 19650-2: **Projekt-Afsender-Volumen-Niveau-Type-Rolle-Nummer**, derefter status og revision, for eksempel `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. I dialogen Project folder sætter du projektkoden, afsenderen (dit kontor), volumenet samt standardniveau og -rolle. Resten kommer fra projektet:

- et arks **niveau** er den etage, dets planer tegner (`00` ved ±0,00, `01` over, `B1` under, `ZZ` når det tegner flere eller ingen);
- dets **rolle** er dets disciplins (arkitektur A, konstruktion S, MEP M, anlæg C);
- dets **nummer** er cifrene i arknummeret (`A-101` bliver `0101`);
- modellen er `M3`, følgesedlen `IE`.

## Kontroller

**Manage → Check** kører, over hele projektet, de regler et tegnekontor kontrollerer med øjet før en udgivelse. Et **blokerende** fund stopper udgivelsen; en **advarsel** kan tilsidesættes. Klik på et fund for at gå til arket, visningen eller elementet.

![Kontrol: fundene efter regel](shots/cde-check.webp)

Blokerende: et tegningshoved uden nummer, titel eller tegner; en visning uden målestok; en visning uden for rammen eller over tegningshovedet; et ark, der er ændret siden sidste udgivelse, men stadig står på den revision; et arknummer, der ikke giver et filnummer. Advarsler: kontrollant, dato og fase tomme; en plan farvet efter et rumskema, eller med mange skraveringer, uden signaturforklaring; en plan, et snit eller en facade på intet ark; en situationsplan uden nordpil; elementer uden etage, type eller materiale; døre og vinduer uden mærke; rum uden nummer; de påkrævede IFC-egenskaber (IsExternal, LoadBearing, FireRating på døre) tomme.

## Udgivelse

**Manage → Issue…** laver en udgivelse, beholdt i projektfilen og, når projektet har en mappe på denne computer, også skrevet i dens container:

![Udgivelse: arkene, revisionen, status, kontrollerne](shots/cde-issue.webp)

1. Giv udgivelsen et navn, dens **revision** (`P01`, `P02`… mens den er foreløbig, `C01`… når den er kontraktlig; den næste foreslås ud fra sidste udgivelse), dens **status** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) og **containeren**.
2. Sæt flueben ved de ark, der skal med, modellen som IFC, hvert ark også som DXF eller alle ark også i én PDF. Hver fils navn vises, som det bliver skrevet. IFC’en beholdes kun i projektfilen, når du sætter flueben ved **Behold IFC’en i projektfilen** (en stor model fylder titusinder af MB pr. udgivelse); uden går den til projektmappen, og projektfilen beholder dens navn og hash. **Også i projektmappen** skriver filerne i containeren også (valgt som standard, når projektet har en mappe).
3. Kontrollerne kører, mens du vælger. Med et blokerende fund kan udgivelsen ikke sendes; med advarsler alene, sæt kryds ved **Udgiv alligevel**.
4. **Udgiv** laver hvert ark som PDF, IFC’en og resten, navngivet efter konventionen, hasher hver fil (SHA-256), laver **følgebrevet** (en PDF og en CSV: projekt, udgivelse, dato, revision, status, filerne) og et **manifest** (JSON, med alle hashes), beholder dem alle i projektfilen og registrerer udgivelsen i projektet. Hvert udskrevet ark bærer udgivelsens revision og status i sit titelfelt og udgivelsens række i sin revisionstabel, som udgivet.
5. I samme trin gemmes projektet som en **version** opkaldt efter udgivelsen (se [Filer, versioner og gendannelse](files.md)), så en udgivelse kan genåbnes, gendannes eller sammenlignes med projektet, som det er nu, og projektet er gemt uden ugemte ændringer. Dialogen viser derefter udgivelsen, som den gik ud — navn, revision og filer, hver til at åbne — indtil du lukker den eller starter en **Ny udgivelse**.

![Udgivelsesdialogen efter en udgivelse: udgivelsen, som den gik ud, dens filer](shots/cde-issued.webp)

## Udgivelserne i projektfilen

Udgivelserne står nederst i dialogen Project folder, med hvad de fylder i projektfilen (ens filer beholdes én gang, uanset hvor mange udgivelser der deler dem). Klik på en udgivelse for at se dens filer:

![Udgivelserne i projektfilen: hvad de fylder, en udgivelse åbnet med sine filer](shots/cde-issues.webp)

- klik på en fil for at **åbne** den fra projektfilen (desktoppen åbner den med dens program; browseren downloader den), eller **Gem en kopi…**;
- **Download som zip** giver alle en udgivelses beholdte filer i én zip, opkaldt efter dens følgebrev: det, du sender, når der ikke er en fælles mappe;
- **Udgiv til mappe…** (desktop-appen) skriver en tidligere udgivelse til projektmappens container eller til en mappe, du vælger: udgiv først, vælg mappen senere;
- **Slet gemte filer** lader en gammel udgivelses filer forlade projektfilen ved næste gem, så filen forbliver lille; udgivelsens registrering, dens filnavne og deres hashes bliver;
- **Åbn mappe** viser den container, udgivelsen blev skrevet i.

## I browseren

CDE’en virker også i browseren: Kontrol, Udgivelse, udgivelsernes filer (åbn, Download som zip) og begge slags bemærkninger. Projektfilen rummer det hele og kommer tilbage til dig som den fil, du gemmer. En projektmappe, at udgive til den og at gennemsøge den kræver desktop-appen.

## Bemærkninger (BCF)

**Manage → Bemærkninger til modellen…** gemmer bemærkningerne til modellen – dine og en kontrollants – som BCF-emner (BIM Collaboration Format, som BIMcollab, Solibri, Navisworks og Revit udveksler), i projektet, aldrig i IFC’en. En bemærkning har en titel, en beskrivelse, en type, en status (Open, In progress, Resolved, Closed), en prioritet, en ansvarlig, en frist og mærkater; sine kommentarer; og sine synspunkter – 3D-kameraet, de markerede elementer, snitboksen som snitplaner og et øjebliksbillede.

![Bemærkninger: listen med sine filtre, en bemærkning med øjebliksbillede og kommentarer](shots/remarks.webp)

- **Ny bemærkning** laver en fra 3D-visningen forrest: kameraet som det er, de markerede elementer, snitboksen og et billede taget fra visningen. Udfyld felterne og **Opret**.
- Listen filtreres efter en tekst, en status, en prioritet, den ansvarlige og **Kun åbne**. Vælg en bemærkning for at se og ændre dens felter, læse dens kommentarer og svare i feltet (**Kommentér**; sæt kryds ved **Med 3D-visningen** for at vedhæfte visningen, som den er, til svaret).
- **Vis i 3D** åbner bemærkningens synspunkt: 3D-visningen går til dens kamera, dens elementer markeres (dem projektet har, efter deres IFC-GlobalId), og dens snitplaner bliver snitboksen. En kommentar med sin egen visning har **Vis** ved siden af.
- **Tag igen fra 3D-visningen** erstatter bemærkningens synspunkt med visningen, som den er nu.
- **Importér BCF…** læser en `.bcfzip`- (BCF 2.1) eller `.bcf`-fil (BCF 3.0): et emne, projektet allerede har (samme GUID), opdateres – filens felter vinder, og kommentarer lavet her, som filen ikke har, bliver –, de andre tilføjes. **Eksportér alle**, **Eksportér åbne** eller **Eksportér valgt** skriver en BCF-fil, 2.1 medmindre du vælger 3.0, til at give kontrollanten eller lægge i projektmappens container Shared. Emnerne beholder deres GUID’er, så en tur gennem et andet værktøj kommer tilbage til de samme bemærkninger.

![En bemærkning åbnet ved sit synspunkt i 3D-visningen](shots/remark-viewpoint.webp)

Dit navn på det, du skriver, sættes i dialogens felt **Dit navn** (det bliver på denne computer).
## Bemærkninger på tegningsark

En kontrollant åbner en udgivet PDF i Acrobat, Bluebeam, Preview eller Drawboard og markerer den: noter, tekst, fremhævninger og gennemstregninger, penstrøg, rektangler, cirkler, pile, skyer, stempler, mål, svar. **Manage → Bemærkninger på tegninger…** læser disse markeringer tilbage i projektet som bemærkninger på arket:

![Bemærkninger: listen pr. ark, en læst PDF](shots/cde-remarks.webp)

- **Læs PDF-bemærkninger…** vælger filen. Det ark, den hører til, kommer fra udgivelsen (manifestet ved siden af filen eller projektets egen fortegnelse over udgivelser), ellers fra filnavnets konvention; passer intet, vælger du arket. Siderne i en sæt-PDF går til dens ark i rækkefølge. Det fundne vises — sider, annotationer, hvordan arket blev matchet, eventuelle advarsler — og **Importér** henter bemærkningerne ind. Læses samme fil igen, opdateres dens bemærkninger i stedet for at blive fordoblet; en bemærkning, du har løst, forbliver løst. En fil, der er omdøbt undervejs, matches alligevel: de udgivne bytes er dem, kontrollantens fil begynder med.
- **Gennemsøg projektmappen** (desktop-appen) lister de udgivne PDF’er i Shared og Published, hvilke af dem der er ændret efter udgivelsen, og om deres bemærkninger er læst siden.
- Listen er grupperet pr. ark og filtreret efter status: **åbne**, **løste**, **lukkede**. Et klik åbner arket med bemærkningen forrest. Hver bemærkning kan løses, lukkes, genåbnes eller slettes; kontrollantens status „Completed“ eller „Accepted“ ankommer som løst.

![Et ark med sine bemærkninger ovenpå](shots/cde-remarks-sheet.webp)

På arket tegnes bemærkningerne over papiret på deres plads: omridset i kontrolfarven, et mærke med forfatterens initialer, teksten i et værktøjstip; den forreste har sit kort med teksten, hvem og hvornår, svarene og status. Knappen i arkets værktøjslinje skjuler eller viser dem, og projektbrowseren tæller de åbne ved siden af arket. De er et lag kun for skærmen: et ark udskrives og udgives uden sine bemærkninger.

Lapin læser PDF’en selv, uden andet program: klassiske filer og filer gemt med krydsreferencestrømme, inkrementelle gemninger (det, kontrollantens program tilføjer), tekst i enhver kodning. En fil, den ikke kan læse helt, giver, hvad den kan, med en advarsel. Lapin skriver aldrig i PDF’en.
