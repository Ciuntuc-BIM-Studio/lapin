---
title: Møbler
order: 47
summary: Sofaer, hjørnesofaer, lænestole, stole, senge, garderobeskabe, kommoder, reoler, skænke, borde, skriveborde og køkkener i seks indretninger ud fra parametriske typer, stillet op ad vægge med et klik.
---

## Placér møbler

Fanen **Interiør** har, efter overfladerne, et værktøj til hver slags møbel: **Sofa** (<kbd>SO</kbd>), **Hjørnesofa**, **Lænestol** (<kbd>AC</kbd>), **Stol**, **Seng** (<kbd>BD</kbd>), **Natbord**, **Garderobeskab** (<kbd>WD</kbd>), **Kommode**, **Reol**, **Skænk**, **Bord** (<kbd>TB</kbd>), **Sofabord** og **Skrivebord**. Vælg et, og klik i planen.

Klik tæt på en væg, og møblet stilles op ad den. Dets bagside ligger på væggens flade (på pudsen, ikke på kernen), forsiden vender mod rummet, og det glider langs væggen, indtil det ikke længere går ind i væggen på tværs af et hjørne. Klik længere fra alle vægge, og møblet står, hvor du klikkede.

Mens et værktøj er aktivt, viser kommandolinjens bjælke:

- **typen**, værktøjet placerer, valgt blandt projektets typer af den slags (findes der ingen endnu, oprettes den første, når du placerer møblet);
- **drejningen**, 0°, 90°, 180° eller 270°, for et fritstående møbel;
- **Mod vægge**, som du kan slå fra for at placere et møbel præcis, hvor du klikker, også ved siden af en væg.

Egenskabspanelet viser den samme type før klikket, med dens størrelse og **Redigér typer…**.

![En stue, et køkken og et soveværelse møbleret, i plan](shots/furniture-plan.webp)

Planer tegner møbler med deres symbol, efter tegnekonventionen, ikke snittet:

- en sofa med ryglæn, armlæn og hynder;
- en seng med sengegavl, puder og dyne slået tilbage;
- et garderobeskab med bøjlestang og låger;
- et køkken med vask og kogeplade og overskabene stiplet.

Facader og snit tegner kroppene med deres materialers skraveringer. I 3D er møblerne low-poly-kroppe, som Lapin laver.

![Det møblerede hus i 3D](shots/furniture-3d.webp)

## Stole om et bord, og køkkenet

**Stole om et bord:** klik på et bord eller et skrivebord. Stolene sættes langs de lange sider, cirka én for hver 600 mm, og én ved hver ende, når den er bred nok; et rundt bord får dem i en cirkel. Hver stol vender mod bordet. Et skrivebord får kun stole på forsiden. Stolene har den type, der er valgt til værktøjet **Stol**.

**Køkken** (<kbd>KT</kbd>) tegnes med to punkter langs en væg:

- linjens længde er køkkenets længde, og dets bagside ligger på væggens flade;
- underskabene deles ligeligt i cirka 600 mm;
- det har bordplade, vask og kogeplade (hvor de står langs køkkenet, indstilles på typen), overskabe ovenover og et højskab til køleskabet i den ene ende.

Et køkkens **Længde** kan stadig ændres i egenskaberne; skabene deles igen.

### Køkkenets indretninger

En køkkentype har en **Layout** (indretning): **One wall** (den lige række), **Galley** (to parallelle rækker med en gang imellem), **L-shaped**, **U-shaped**, **Island** (en række på bagvæggen og en fritstående ø med sin gang) eller **Peninsula** (et L med en række, der rager ud fra benet, med bordpladen overhængende til barstole). Når du vælger en, starter typen fra indretningens mål, og hvert mål kan redigeres ved siden af forhåndsvisningen: benenes længde fra bagvæggen, gangen mellem rækkerne, den modstående rækkes længde, øens længde, dybde og placering, halvøens længde og overhæng og, for et L eller en halvø, hjørnet til venstre eller højre.

![De seks indretninger i plan: én væg, parallel, L, U, ø og halvø](shots/kitchen-layouts.webp)

**Sink on** og **Hob on** sætter dem på hovedrækken eller på den anden del (benet, den modstående række, øen, halvøen), **Sink at** og **Hob at** siger hvor langs den, og **Tall unit (fridge)** står i en fri ende. Hjørneskabene er blinde; overskabe er kun på rækker mod en væg, ikke på en ø eller en halvø.

![En køkkentype: L-formet](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) tegner enhver indretning med to punkter langs den væg, hovedrækken står mod: linjen er dens længde, ryggen på væggens flade, og et L, en halvø, et U eller et parallelkøkken skubbes ind i det hjørne eller mellem de vægge, det tegnes nær. **Flip** giver et L eller en halvø hjørnet på den anden side. Møbelskemaet nævner indretningen og fodaftrykkets dybde. I IFC forbliver det et IfcFurniture af en brugerdefineret Kitchen-type.

### Køkkenskabe og apparater

En køkkentype kan opregne sine egne **skabe** under sine parametre i **Møbeltyper**, i to rækker:

- **Underskabsrækken**: hovedrækkens skabe fra venstre set forfra (efter et blindt hjørne);
- **Overskabsrækken**: skabene, der hænger over den.

**Tilføj** sætter et skab af den valgte slags for enden af en række. **Typisk række** begynder underskabsrækken med en vask, en opvaskemaskine, et skab med skuffer og en kogeplade over en ovn. Pilene flytter et skab til venstre eller højre, og papirkurven tager det ud.

Hvert skab har en **bredde**. Et skab tager enhver bredde fra 100 til 800 mm: vælg 150, 200, 300, 400, 450, 500, 600 eller 800, eller skriv en anden. Et apparat tager de størrelser, det fås i. Skabene i underskabsrækken er:

- **Understiksskab**, hvis **Front** er en skuffe over en låge, låger (en eller to, med den side, en enkelt låge er **hængslet i**), skuffer (1 til 4), grydeskuffer (en lav skuffe over to dybe) eller åbne hylder;
- **Vaskeskab**: en enkelt vask, en 1½-kummes vask, en dobbeltvask (fra 800) eller en hjørnevask, med en almindelig armatur, en udtrækkelig armatur eller ingen. En hjørnevask rækker ind i det blinde hjørne, når den står ved siden af det.
- **Kogeplade over ovn**, 600, 800 eller 900 bred, induktion eller gas, med eller uden ovnen under;
- **Fritstående komfur**, 500, 600 eller 900 bredt, induktion eller gas. Bordpladen stopper på begge sider af det.
- **Opvaskemaskine**, integreret, 450 eller 600 bred, bag en front i fuld højde, der passer til de andre;
- **Køleskab**: under bordpladen, højt og integreret (en køleskabslåge over en fryserlåge) eller fritstående;
- **Højskab til ovn**, med skuffer under ovnen og en låge over den;
- **Højskab**, med låger eller åbne hylder;
- **Fyldestykke** og **Hul**.

Et højskab, et køleskab eller et ovnskab har sin egen **Højde**. I overskabsrækken har et **Overskab** låger, glaslåger eller åbne hylder, sin egen **Højde** og sin egen højde **Fra gulvet**. Lad en af dem stå tom for at tage rækkens værdier. En **Emhætte** er en skorstensemhætte (650 over bordpladen, med sit rør op til overskabenes top) eller en integreret emhætte i et overskab. Et **Hul** lader væggen stå bar. Overskabe springer forbi et højskab.

Uden en opregnet overskabsrække får hvert underskab et overskab af sin bredde, og en kogeplade eller et komfur får en skorstensemhætte. På en ø eller en halvø hænger **Emhætte over kogepladen der** en emhætte over den kogeplade, der står på den.

Det, skabene efterlader af rækken, fordeles ligeligt som før, og mindre end 100 mm bliver et fyldestykke. Skabe, der ikke passer i rækken, udelades, og forhåndsvisningen og elementet siger det. **Tilpas til skabe** i et køkkens egenskaber sætter dets længde til det, dets skabe behøver: deres egen, plus et blindt hjørne og højskabet for enden. En type uden egne skabe bygges som før.

I plan har hvert skab sit eget symbol:

- underskabslågernes svingning, stiplet og kun ved den fineste detalje;
- vaskens kummer og dens armatur;
- kogepladens zoner eller blus;
- højskabe med kryds;
- **DW** på opvaskemaskinen, **F** på et køleskab og **OV** på et ovnskab;
- overskabe og emhætter oven over.

Opstalter og 3D viser hver front, hvert greb og hvert apparat.

Materialerne har to roller mere: **Sokkel** og **Greb**, ved siden af fronterne, korpus, pladen (bordpladen), metal og glas. En køkkentype lavet før tager til soklen sit korpus' materiale og til grebene sit metals.

![En køkkentypes skabe: underskabsrækken og overskabsrækken, med motorens forhåndsvisning](shots/kitchen-units-types.webp)

![Et køkken af blandede skabe og apparater, i 3D](shots/kitchen-units-3d.webp)

**Interiør → Køkkenskabe** laver et skema over hvert køkkens skabe, fra venstre, underskabsrækken og så overskabsrækken, med deres bredde, højde og front eller apparat. I IFC forbliver køkkenet ét IfcFurniture af sin type, og skabene rejser med typen, så Lapin læser dem tilbage.

## Korpusmøbler og moderne møbler

**Interiør → Korpusmøbler** placerer møbler lavet af plader: garderobeskabe, reoler, skænke, TV-møbler, kommoder, badeværelsesmøbler med håndvask, skriveborde, entrémøbler, senge og natborde. Hvert enkelt sættes op i **Møbeltyper**. Vælg dér **Korpusmøbler** og en **Forudindstilling**, og derefter **Ny type**. Forudindstillingen giver målene, soklen, pladen og korpusserne, og hver værdi kan stadig ændres. Vælger du senere en anden forudindstilling, starter typen forfra med værdierne i den forudindstilling og beholder sine materialer.

Et korpusmøbel er ét eller flere **korpusser** side om side, listet fra venstre, set forfra:

- **Korpus** tilføjer et i enden, delknappen deler et i to halvdele, pilene flytter et, og skraldespanden fjerner det.
- Et korpus med en **Bredde** beholder den. Korpusser uden bredde deler det, de andre efterlader af møblets bredde. **Tilpas bredden** gør møblet lige så bredt som dets korpusser, når alle har en bredde. Korpusser, der ikke kan være der, udelades, og forhåndsvisningen siger det.
- Et korpus med sin egen **Højde** er lavere end møblet, som den lave del af et entrémøbel.
- En **Benplads** er et korpus uden kasse, som pladsen under et skrivebord. Pladen løber hen over den, og en side bærer pladen i enden af møblet.

Hvert korpus rummer **zoner** stablet nedefra, med en skillevæg mellem hver to. Listen viser dem, som de står, den øverste først. Pilen tilføjer en zone over den øverste. En zone har sin egen **Højde** eller en **Andel af resten** og sin **Forside**:

- **Åbne hylder**;
- **Døre**: 1 til 4, en enkelt dør **Hængslet i** venstre eller højre side, **Glasede** eller ej;
- **Skuffer**: 1 til 6, hver med sin kasse bagved. **Højder i forhold, oppefra** giver deres højder: 1:1:2 gør den nederste skuffe dobbelt så høj som de andre. Tom er de alle ens.
- **Klap**, der **løftes op** eller **klappes ned**.

Bag åbne fronter, døre eller en klap kan en zone have **Hylder** (justerbare, medmindre du fjerner markeringen i **Justerbare hylder**) og en **Bøjlestang**. Bag døre kan den også have **Indvendige skuffer** forneden. Skuffer under døre er to zoner i ét korpus: skufferne forneden med deres højde og dørene over dem.

Sektionerne under forudindstillingen giver resten:

- **Konstruktion**: tykkelsen af korpuspladerne, bagpladen, hylderne og fronterne; top og bund **mellem siderne** eller **over siderne**; ryggen **indfældet** (10 mm inde fra bagkanten) eller **lagt på bagsiden**.
- **Sokkel**: en **Sokkel** med sin højde og hvor langt den er sat tilbage; **Ben** (slankt metal, konisk træ eller slædeben) med deres højde; **Hængt på væggen** med højden af sin underside; eller **Ingen**. På sokkel eller ben er møblets højde op til dets plade. Hængt på væggen er højden møblets egen, og **Ophængt i** siger, hvor dets underside er.
- **Plade**: **Ingen** (korpussernes egne toppe), **Overhængende** (ud over forsiden og enderne med sit overhæng) eller en **Bordplade** (kun ud over forsiden) med sin tykkelse. **Udskæring til håndvask i pladen** skærer en plads til håndvasken i et badeværelsesmøbel og tegner den med dens hane.
- **Fronter**: **Glat**, **Med ramme** (stolper og tværstykker rundt om en tilbagetrukket fyldning) eller **Rillet** (lodrette riller skåret i fronten). Grebene er **Ingen** (push-to-open), et **J-profilgreb** fræset i frontens kant, et **Stanggreb**, en **Knop** eller et **Kantgreb**. **Skydedøre over hele bredden** sætter så mange skydedøre på to skinner foran alle korpusserne, som på et garderobeskab.

En seng lavet af forudindstillingen **Seng** har en ramme, en sengegavl lige så høj som typen, en sengebund, madrassen og puder. Den har **Madrassens overside** og skuffer under på begge sider, den venstre eller den højre.

Knapperne under **Stilarter** lægger et moderne udtryk over typens værdier i ét trin:

- **Uden greb, push-to-open**;
- **J-profilgreb**;
- **Slanke metalben**;
- **Svævende, hængt på væggen**;
- **Rillede fronter**;
- **Tofarvet, træ og farve**: et korpus og en plade i eg med salvie-grønne fronter;
- **Fronter med ramme, knopper**.

Materialerne har deres roller: korpus, fronter, plade, bagside, sokkel, ben, greb, glas, metal (stangen, hanen) og sengetøj (en senges madras). De materialer, Lapin starter med, er vejledende: skriv dine egne.

Hver plade er et massivt legeme af sit materiale. I plan viser møblet sin kontur, linjen af sine fronter, samlingerne mellem sine korpusser og, ved den fineste detaljeringsgrad, sine dørs sving stiplet. Et møbel hængt over 1,2 m tegnes som oven over. Opstalter og 3D viser pladerne, fronterne og grebene.

Forhåndsvisningen lister typens **Tilskæringsliste**: hver del (sider, bund, top, bagside, skillevægge, hylder, sokkel, skuffekasser, så fronterne for sig) med dens korpus, dens længde langs fibrene × bredde × tykkelse og hvor mange. **Interiør → Tilskæringsliste** laver tilskæringslisten for alle korpusmøbeltyper i projektet, ganget med deres møbler. Den lister kun plader: skinner, hængsler, ben og greb er beslag.

![Konfiguratoren til korpusmøbler: en forudindstilling, stilarterne, konstruktionen, korpusserne og deres zoner, ved siden af motorens forhåndsvisning og tilskæringsliste](shots/casework-types.webp)

![Korpusmøbler i 3D: en skænk på koniske ben, et rillet TV-møbel hængt på væggen, et garderobeskab og et skrivebord](shots/casework-3d.webp)

I IFC er et korpusmøbel et IfcFurniture af sin type med sin forudindstillings foruddefinerede type (SHELF til en reol, DESK, BED eller USERDEFINED med Wardrobe, Sideboard, TV unit, Bathroom vanity…). Dets korpusser følger med typen, så Lapin læser dem tilbage. De garderobeskabs-, skænk- og kommodetyper, der blev lavet før, forbliver, som de var.

## Et møbels egenskaber

Vælg et møbel for at se:

- **Type**, dets slags og størrelse;
- **Rotation**;
- **Over gulvet**, for et møbel hævet på et podie;
- **Længde**, for et køkken.

**Vend** giver møblet dets anden hånd, hvor det står: chaiselongen i den anden side, køleskabet i den anden ende, hjørnesofaens hjørne i den anden side. Forsiden bliver, hvor den var. **Redigér → Spejl** gør det samme.

Træk i grebet midt på et møbel for at flytte det; træk i grebet foran det for at dreje forsiden mod markøren.

![En sofas egenskaber](shots/furniture-properties.webp)

## Møbeltyper

**Interiør → Møbeltyper** viser projektets typer i grupper (siddemøbler, senge, opbevaring, borde, køkken), hver med antallet af møbler, der bruger den. Vælg en slags og **Ny type**, eller **Duplikér** en type for at ændre en kopi. En type har:

- sit **navn**, sin **bredde**, **dybde** og **højde** (for en sofa ryglænets, for en seng sengegavlens, for et køkken bordpladens);
- sin slags egne parametre:
  - sofaer og lænestole: sæder, armlæn, armlænsbredde, sædehøjde, en chaiselong til venstre eller højre og dens længde, ben;
  - en hjørnesofa: den side, hjørnet sidder på, og den sides længde;
  - en seng: madrassens overside, sengegavl, fodgavl, puder, en dyne slået tilbage;
  - et garderobeskab: låger, hængslede eller skydelåger, skuffer forneden, sokkel;
  - en kommode: rækker og kolonner af skuffer;
  - en reol: hylder og låger forneden op til en højde;
  - en skænk: låger og skuffer;
  - et bord: rektangulært, rundt eller ovalt, på fire ben eller en søjlefod;
  - et skrivebord: en skuffesektion til venstre eller højre;
  - et køkken: underskabsbredde, sokkel, bordplade, placering af vask og kogeplade, overskabe, højskab og dets egne skabe (ovenfor);
- sine **materialer** efter rolle: korpus, polstring, plade, fronter, sengetøj, metal, glas og for et køkken sokkel og greb. Skriv et hvilket som helst materialenavn; et, som projektet ikke har, oprettes, og **Administrer → Materialer** fastlægger dets farve og skraveringer.

Forhåndsvisningen ved siden af felterne viser typens plansymbol og forside, mens du ændrer den. Hvert møbel af en type følger enhver ændring.

![Dialogen Møbeltyper med en sofa og dens chaiselong](shots/furniture-types.webp)

## Mit møbelbibliotek

En type, du har konfigureret godt (et køkken, et garderobeskab, et skrivebord af plader, en sofa), kan gemmes til alle projekter i **Mine møbler**, dit eget bibliotek på denne computer.

**Gem i mit bibliotek…** i **Møbeltyper** gemmer den valgte type. Giv den:

- et **navn**;
- en **kategori**: køkken, stue, soveværelse, badeværelse, kontor, opbevaring eller andet;
- **mærker**, adskilt af kommaer (eg, moderne…);
- en **varekode** og en **vejledende pris**, hvis du vil. Begge er dine egne: Lapin leverer ingen priser.

Miniaturebilledet tegnes ud fra typens forhåndsvisning. Typen tager alt med, hvad den har brug for: sin konfiguration og sine materialer med deres farver, skraveringer og billeder.

![Gem i mit bibliotek: navnet, kategorien, mærkerne, varekoden og prisen ved siden af miniaturebilledet](shots/furniture-save-library.webp)

**Interiør → Mine møbler…** (eller **Mine møbler…** i Møbeltyper) viser biblioteket som miniaturebilleder. Søg i det efter navn, mærke, vare eller art, og filtrér det efter kategori og efter mærke. For hvert element:

- **Indsæt** lægger typen i det åbne projekt og vælger den til sit værktøj. Et materiale, som projektet allerede har med det navn, bruges, som det er, og de andre oprettes. Hvis projektet allerede har en identisk type, bruges den, og der laves ingen kopi. Hver indsættelse er ét trin at fortryde.
- **Placér** indsætter den og starter dens værktøj, så næste klik sætter et stykke ned. Et køkken tegnes langs en væg.
- **Omdøb**, **Mærker**, **Duplikér** og **Slet** ændrer kun biblioteket. Projekter, der allerede har typen, beholder den.
- **Eksportér** skriver elementet som en `.lapinlib`-fil. **Eksportér alle…** skriver hele biblioteket, og **Importér…** læser en tilbage. Import springer et element over, som allerede er i biblioteket uændret. Et element med samme id, men ændret indhold, tilføjes ved siden af det, du har, så intet overskrives.
- **Føj til en skabelon…** lægger typen i en af dine projektskabeloner, så ethvert projekt startet ud fra den har typen.

![Mine møbler: bibliotekets miniaturebilleder, søgt og filtreret, hvert klar til at blive indsat eller placeret](shots/furniture-library.webp)

På skrivebordet gemmes biblioteket i din Lapin-mappe, én fil pr. element. I browseren gemmer browseren det. For at flytte det mellem de to eller til en anden computer eksporterer du det og importerer filen. En projektskabelon beholder møbeltyperne fra det projekt, den gemmes fra. **Gem som skabelon…** kan også tilføje alle typer i Mine møbler (**Føj alle typer i Mine møbler til**).

## Skemaer og IFC

**Møbelskema** viser hver type med dens slags, størrelse, antallet af møbler og de rum, de står i, med totalen. **Møbler pr. rum** viser hvert rum med det, der står i det. Begge ligger i projektbrowseren under **Skemaer** og udskrives og eksporteres som ethvert skema. En møbeltype med en prispost prissættes pr. stk. i overslaget.

I IFC er et møbel et **IfcFurniture** med den foruddefinerede type for dets slags (SOFA, CHAIR, BED, TABLE, DESK, SHELF eller USERDEFINED med Wardrobe, Kitchen…). Dets **IfcFurnitureType** er knyttet med IfcRelDefinesByType, og dets kroppe bærer deres materialer. Lapin læser sine egne møbler tilbage som native møbler, der kan redigeres. Møbler fra andre programmer kommer ind som referencer.
