---
title: Installationssystemer
order: 57
summary: Tegn varme-, vand-, afløbs-, el- og ventilationssystemer som diagrammer på planen, med kilder, endepunkter og strækningerne mellem dem, og eksportér dem som et IFC-netværk.
---

Fanen **Installationer** tegner bygningens installationer, sådan som en ingeniør skitserer dem: et system pr. installation, dets **kilde** (en kedel, en vandmåler, en faldstamme, en eltavle, en ventilator), dets **endepunkter** (radiatorer, håndvaske, toiletter, stikkontakter, lamper, indblæsningsventiler) og **Strækninger** mellem dem. Hver knude er et rigtigt IFC-element med sine porte; hver strækning er et rør-, kanal- eller kabelsegment; forbindelserne holder, når du flytter ting.

![Fanen Installationer](shots/ribbon-mep.webp)

## Et system med ét klik

Under **Nyt system** vælger du installationen (**Varme**, **Vand**, **Varmt vand**, **Afløb**, **El**, **Ventilation**) og klikker i planen. Lapin placerer hele systemet: kilden, hvor du klikkede, et første endepunkt nogle meter væk og strækningen mellem dem. Systemet åbner til redigering.

![Et varmesystem på stueetagens plan: kedlen, to radiatorer under vinduerne og strækningerne mellem dem](shots/mep-plan.webp)

I redigeringstilstanden er alt, der ikke er systemet, falmet, og kun systemets knuder og strækninger kan vælges. Træk kilden og endepunktet derhen, hvor de skal være: deres strækning følger med. Tilføj endepunkter med **+ Radiator**, **+ Håndvask**… i egenskabspanelet, eller med værktøjerne under **Varme**, **VVS**, **El** og **Luft**: hvert nyt endepunkt kobles på systemet med sin egen strækning, fra kildens nærmeste ledige port eller fra den nærmeste strækning (en T-forgrening). **Færdig** (eller <kbd>Esc</kbd>) forlader redigeringstilstanden og fortæller dig, hvor mange bøjninger og T-stykker systemet har, og hvad der stadig er åbent. **Redigér system** (<kbd>ES</kbd>) åbner det igen.

## Strækninger

**Strækning** (<kbd>RN</kbd>) tegner en strækning punkt for punkt; tryk på <kbd>Enter</kbd> for at afslutte. En ende, der lander på en åben port, forbindes med den; en ende, der lander på en strækning i samme system, forgrener sig fra den.

**Forbind** kobler en strækningsende til en knude eller til en anden strækning; **Afbryd** frigør den. **Åbne porte** markerer alt, der stadig ikke er forbundet.

## At omforme en strækning

Markér en strækning: dens greb sidder på linjen, som den er tegnet, også efter du har flyttet de knuder, den forbinder.

- **Træk i et greb** for at flytte det hjørne frit.
- **Træk i midten af et ben** for at skubbe benet til siden; benene ved siden af strækkes, så en retvinklet føring forbliver retvinklet. Skub det første eller sidste ben, og Lapin tilføjer et forspring der, så enden bliver på sin port.
- **Ctrl-klik** på et greb for at fjerne det, eller på midten af et ben for at tilføje et; **Tilføj greb** sætter et, hvor du klikker.
- **Omvej** (<kbd>DU</kbd>) fører en strækning uden om noget, der er i vejen: klik på strækningen, hvor omvejen starter, klik, hvor den slutter på samme ben, flyt derefter til den side, den skal gå, og klik (eller skriv afstanden). Strækningen går ud, langs og tilbage, med fire nye hjørner; den beholder sine forbindelser.
- **Tegn stien igen** (<kbd>RP</kbd>) giver en strækning en ny føring: markér den (eller start på den), og tegn stien punkt for punkt, <kbd>Enter</kbd> for at afslutte. Strækningen beholder sit system, sin størrelse og sine forbindelser, og dens ender går tilbage på deres porte.

![En radiatorstrækning ført uden om en forhindring med Omvej: ud, langs og tilbage](shots/mep-detour.webp)


## Knuder

Hver knude har en **Rolle**: en kedel, en radiator, en fordeler, en vandmåler, en håndvask, en køkkenvask, et toilet, et brusebad, et badekar, en faldstamme, en renseåbning, en eltavle, en stikkontakt, en lampe, en kontakt, en ventilator, en indblæsningsventil, en udsugningsrist, en ventil, en stigning/fald, en prop. Rollen giver den dens IFC-klasse, dens porte, dens størrelse og dens symbol på planen. En ventil, en måler eller en kontakt, der slippes på en strækning, deler den og sidder i midten. En **Stigning / fald** fører en strækning til en anden kote eller til etagen ovenover.

![En radiators egenskaber: dens system, dens rolle og porte, dens størrelse og effekt](shots/mep-radiator-properties.webp)

I egenskabspanelet viser en knude sit system, sin rolle og sine porte (åbne eller forbundne); en strækning viser sine to ender. Begge tager systemets standardværdier, medmindre du giver dem en værdi: størrelse, kote, det plan de føres i (gulv, loft, væg), parvis eller enkelt, materiale, fald.

## Systemer

**Systemer…** viser projektets systemer og standardværdierne for det aktuelle: størrelsen, planet og koterne for hvert plan, faldet (afløb), om strækninger tegnes parvis (varme: frem- og returløb) og med hvilken afstand, materialet og isoleringen. Ændr en standardværdi, og alle knuder og strækninger uden deres egen værdi følger med.

![Dialogen Systemer](shots/mep-systems.webp)

## Planer, lister og IFC

**Installationsplan** laver en plan med fagområdet installationer for niveauet, ordnet under sættet Installationer i browseren. **Udstyrsliste** og **Strækningsliste** under **System** er skemaer som alle andre: de sættes på ark og eksporteres med dem.

Ved eksport til IFC er hvert system et IfcDistributionSystem, hver knude og strækning har sine IfcDistributionPorts, og forbindelserne skrives mellem portene, så netværket ankommer til et andet program forbundet fra kilden til det sidste endepunkt.

## Gas og fyringsolie

**Gas** og **Fyringsolie** er egne fag under **Nyt system**. Et gassystem angiver sin gas (**Naturgas** eller **F-gas**) og sin **Trykklasse** (lavtryk i huset, mellemtryk i gaden) i **Systemer…**; dets rør er som standard sorte stålrør på væggene i 2,1 m og kan være kobber, flerlagsrør eller **PE 100** til det nedgravede. Fyringsolie løber i kobber.

Et hus' gasinstallation begynder ved **målerskabet** på facaden. Klik **Nyt system → Gas**: målerskabet sættes hvor du klikker og en **gaskedel** indenfor, forbundet af en strækning. Undervejs sætter værktøjerne under **Gas** **gasregulatoren**, **gasmåleren**, **hovedhanen for gas**, **apparathanerne** og **fleksible tilslutninger** på strækningen, som de deler ligesom enhver fitting; **gaskomfuret** og **gasvandvarmeren** forbindes med installationen af deres egen strækning. Hvor et gas- eller olierør går gennem en væg, går det gennem et **foringsrør**, tegnet og talt med strækningen. En gaskedel tegnet i varmesystemet som dets kilde forbindes med gasinstallationen fra sine egenskaber (knappen **+** med gassystemets navn).

![Et hus' gasinstallation: målerskab, regulator og måler udenfor, kedel og komfur i køkkenet](shots/mep-gas-plan.webp)

Hvert apparat beholder sit **forbrug** (m³/h) og sin **apparattype** efter aftrækket (A uden aftræk, B åbent aftræk, C lukket), til listerne og dimensioneringen.

**Fyringsolie** sætter en **olietank** (inde, ude eller nedgravet) og en **oliekedel**; **påfyldningsstudsen** og **udluftningshætten** forbindes med tankens påfyldning og udluftning, og **oliefilteret** sidder på fremløbet.

### Forsyningen i gaden

Under **Gade** lægger **Gasledning langs…** (<kbd>GM</kbd>) en gasledning langs det du klikker — en vej, en linjeføring, en tegnet linje, en væg — med **afstand til linjen** og **dybde (jorddække)** fra kommandolinjens bjælke; under et terræn måles dybden fra jorden, ellers fra etagen. **Gasledning** tegner den punkt for punkt. Ledningen kommer i et mellemtryks-gassystem af PE-rør med sit **advarselsbånd**.

**Stikledning** (<kbd>GS</kbd>): klik ledningen, så målerskabet. Lapin sætter en **anboringsbøjle** på ledningen, lægger stikledningen vinkelret fra den i dens jorddække hen til skabet og fører den op i skabet dér.

![Stikledningen fra gadens gasledning til målerskabet, valgt](shots/mep-gas-street.webp)

![Ledningen og stikledningen under jorden, installationen på facaden](shots/mep-gas-3d.webp)

![En stiklednings egenskaber: fra anboringsbøjlen til målerskabet, jorddække og diameter](shots/mep-gas-service-properties.webp)

![Et gassystems standarder: dets gas og dets trykklasse](shots/mep-gas-systems.webp)

**Mængdeliste for gas og olie** under **System** viser system for system rørene efter materiale og diameter med deres længde (og heraf stigrørene), bøjninger, T-stykker og foringsrør, advarselsbåndet, haner, målere og skabe og apparaterne med installeret forbrug og effekt. I IFC er et gassystem et IfcDistributionSystem af typen GAS og et oliesystem FUEL; målerskabet er et IfcDistributionChamberElement, måleren en IfcFlowMeter GASMETER, hanerne IfcValve, komfuret en IfcBurner, tanken en IfcTank.

![Mængdelisten for gas og olie](shots/mep-gas-list.webp)

At dimensionere rørene ud fra apparaternes forbrug og eftervise tryktabet er ingeniørarbejde, i udgaven **Lapin MEP**.

## Koordinering: åbningsvolumener og kollisionskontrol

Hver åbning i en væg, et dæk eller et tag kan være et **åbningsvolumen** for sig: et legeme så stort som åbningen, gennem hele værtens tykkelse, beklædning medregnet. Kollisionsprogrammer (Navisworks, Solibri, BIMcollab) tester installationer mod sådanne volumener med "helt indeni": et rør, en kanal eller en kabelbakke helt inde i et volumen er i orden; en, der stikker ud, eller som krydser en væg uden åbning, er en kollision. Lapin kører selv den samme kontrol.

**Administrer → Kollisionskontrol…** kører kontrollen kun, når du beder om det: intet starter den af sig selv, hverken en ændring eller **Administrer → Kontrol**. Dens fane **Indstillinger** bestemmer, hvilke åbninger der laves volumener for (åbninger i vægge, døre, vinduer, dæk og tage), hvor meget større eller mindre end åbningerne de er (**Forskydning af volumen**), og den **Frigang** et installationselement skal holde fra et volumens sider. **Kør kollisionskontrol** går én gang gennem hvert rør, hver kanal og hver kabelbakke — Lapins føringer og segmenterne i en importeret installationsmodel ligeså, og en installationsmodels egne udsparinger — og gemmer det fundne som projektets **kollisionsrapport** med datoen for kørslen, indtil den køres igen. Ændres modellen bagefter, siger rapporten, at den er **forældet**. Dens problemer er grupperet efter art og nummereret, og hvert nummer bevares fra den ene kørsel til den næste:

- **Ingen åbning**: den går gennem værten, hvor den ingen har;
- **Ikke skåret: bærende eller snitrækkefølge**: ingen åbning, fordi føringernes egne åbninger (nedenfor) ikke skærer den vært;
- **For lille åbning**: den går gennem en åbning men stikker ud af den, frigangen medregnet; **Mangler** siger med hvor mange millimeter på den værste side;
- **Tomt volumen** (med **Tomme åbninger** afkrydset i **Indstillinger**): en væg- eller dækåbning, som intet går igennem (et trapperum er aldrig et).

![Kollisionsrapporten: ingen åbning, en for lille åbning og et tomt volumen, hver nummereret](shots/mep-voids-check.webp)

Hvert problem er **Skal løses**, **Løst** eller **Ignoreret**, med en kommentar. Et klik på en række viser parret i 3D, begge valgt (**alene i 3D** skjuler resten indtil videre); pilene går til den forrige og den næste. **Markér i visningen** (også **Vis → Kollisionsmærker**) tegner hvert åbent problem i planen, snittet eller facaden forrest som en rød sky med sit nummer, kun på skærmen.

![Planen med kollisionerne markeret: røde skyer med deres numre](shots/mep-voids-clouds.webp)

**Lav åbning** skærer en enkel åbning om elementet i væggen (rund for et rør eller et kabel, rektangulær for en kanal) eller i dækket, med siderne i frigangens afstand og størrelsen rundet op, som snitrækkefølgens installationsregel siger; **Forstør åbning** forstørrer den for lille åbning. **Løs de valgte** gør det for hver afkrydset række, **Ignorer** og **Genåbn** sætter deres status; hver er ét trin at fortryde. Rapporten siger så, at den er forældet: kør den igen, og det løste står som **Løst** (**vis løste**). Dens linje siger så, ved hvilken kørsel den blev fundet løst, og hvordan den er nu: åbningen, den går igennem, og føringens størrelse i den (**Resolved at the run of 04.11.2026 15:20: opening V1 200 mm, run 160 mm.**), eller at føringen ikke længere går igennem; hver kørsel derefter opdaterer størrelserne.

![Kørt igen efter rettelserne: de to kollisioner løst](shots/mep-voids-fixed.webp)

**BCF** gør de åbne kollisioner til bemærkninger af typen **Clash** med deres synspunkter (**Administrer → Bemærkninger til modellen…**) og gemmer dem som en BCF-fil; kørt igen opdaterer den de samme bemærkninger. **CSV** og **PDF** gemmer rapporten som en liste. **Administrer → Kontrol** læser kun rapporten: dens åbne kollisioner er advarsler, og det er en forældet rapport også.

### Føringer der skærer deres egne åbninger

Snitrækkefølgen (**Administrer → Snitrækkefølge**) rummer også installationsføringer, over vægge, dæk og tage. Med **Føringer skærer vægge, dæk og tage** slået til skærer en føring, der krydser en væg, et dæk eller et tag, sin egen åbning der: rund for et rør eller et kabel, der går vinkelret igennem, rektangulær for en kanal eller en skrå føring, så stor som føringens yderside (dens isolering, et gasrørs beskyttelsesrør) plus **Frigangen** rundt om, rundet op til trinnet, aldrig under **Mindste åbning**. Åbningen gemmes ikke for sig: den flytter sig og vokser med føringen og forsvinder, når føringen forsvinder eller ikke længere krydser. Den er et volumen som enhver åbning (tegnet, listet, kontrolleret), og IFC-eksporten skriver den som **IfcOpeningElement** med **Lapin_ProvisionOpening** (og som udsparing med **Åbningsvolumener** afkrydset), så en kontrol kørt igen ikke finder noget at melde. En væg, et dæk eller et tag, der står over føringerne i snitrækkefølgen (efter klasse eller materiale), skæres ikke, og heller ikke en bærende (LoadBearing i dens Pset_…Common), medmindre **skær også dem** er afkrydset: kollisionskontrollen melder dem som **Ikke skåret**. En nedrevet føring skærer intet, og hvor en føring allerede går gennem en åbning, skæres ingen. Et system kan bestemme andet i **Installationer → Systemer…** (**Føringer skærer vægge, dæk og tage**: som snitrækkefølgen, ja, nej). Det er slået til i et nyt projekt; et projekt lavet før holder det slået fra, indtil det slås til.

I en buet væg findes åbningen, hvor føringen møder buen, med bredden målt langs den; en rektangulær har radiale sider, som en dørs eller et vindues. I et tag skæres den i den tagflade, føringen går igennem: lodret for et rør, der står op (en udluftning), trukket ud langs føringens forløb for en skrå, som i et dæk; åse og kip under tagfladen skæres ikke, og kollisionskontrollen melder en føring gennem dem. At flytte en føring eller ændre dens størrelse genopbygger kun de vægge, dæk og tage, den krydsede eller krydser nu, uanset hvor stor modellen er. Lapins egen IFC læst ind igen tager ikke disse åbninger igen, når deres føringer også kommer med (føringerne skærer dem igen, aldrig to gange); uden deres føring (udeladt af importen, eller et projekt hvis føringer intet skærer) kommer hver ind som en åbning i sin bygningsdel, så intet går tabt.

![Installationsføringer i snitrækkefølgen](shots/mep-voids-cut-order.webp)

Fanen **Volumener** viser hvert volumen med sit mærke (V1, V2… etage for etage), værten, døren eller vinduet i det, størrelse og dybde; et klik vælger åbningen.

![Volumenerne med mærker og størrelser](shots/mep-voids-list.webp)

**Vis → Åbningsvolumener** tegner dem med 3D-visningen forrest i 3D, gennemsigtigt violette med deres mærker; et klik vælger åbningen.

![Volumenerne i den indre væg, med rørene igennem](shots/mep-voids-3d.webp)

Med en plan, et snit eller en facade forrest tegner **Vis → Åbningsvolumener** dem i den visning, som en koordineringstegning viser udsparinger: skåret hvor visningen skærer dem (en vægs åbning gennem vægtykkelsen, et dæks gennem pladen), set hvor den ser dem (en dækåbning under planens snit, en vægåbning på den flade en facade ser på), stiplet når en vægåbning ligger under snittet eller en dækåbning over det; hver krydset fra hjørne til hjørne, med sit mærke og under det størrelsen og en vægåbnings underkant (**400×200 +0.90**, **Ø200**). Motoren tegner dem sammen med resten af tegningen, så tegningsark, PDF og DXF (lag **VOIDS**) udskriver dem som skærmen viser dem.

![Planen med udsparingerne: skåret i væggene, dækåbningen set, hver krydset og mærket](shots/mep-voids-plan.webp)

En udsparings mærkat står ved siden af den, på tværs af væggen, hvor intet andet er tegnet: en dørs slagfelt, dørens eller vinduets mærke, andre mærkater. Er begge sider af væggen optaget, flytter den langs væggen forbi åbningen, ellers længere ud.

![En dørs udsparing: dens mærkat fri af slagfeltet og af dørens mærke](shots/void-label.webp)

For at flytte en mærkat klikker du på den (eller på udsparingen): det vælger åbningen, hvis mærkat viser et firkantet greb til at flytte den og et rundt til at dreje den. Ctrl/⌘-klik på et greb sætter mærkaten tilbage, hvor den står som standard, eller vandret igen. En flyttet mærkat gemmes af den visning, langs og på tværs af væggen fra udsparingens midte, så den følger åbningen, når væggen flyttes; tegninger og PDF udskriver den, hvor du satte den.

![Udsparingens mærkat flyttet med sit greb](shots/void-label-moved.webp)

Det er en indstilling på visningen, også i dens egenskaber (**Vis → Visningsindstillinger**): **Åbningsvolumener**, og **Udsparingers størrelser** for størrelsen under mærket. Som standard tegner ingen visning dem; visningsstilen **Koordinering** (**Administrer → Visningsstile**, valgt som visningens **Stil**) tegner dem i hver visning der bruger den. Deres linjer er kategorien **Volumener**: penne og farver i **Administrer → Objektstile** (snit-, projektions-, skjulte og overliggende linjer), vist eller skjult pr. visning i **Vis → Synlighed / Grafik…**.

For at give volumenerne til et kollisionsprogram markeres **Åbningsvolumener** i **Filer → Eksportér IFC eller Fragments…** (eller **Eksportér 3D…**). I IFC er hvert volumen en **IfcBuildingElementProxy** af typen **PROVISIONFORVOID** med **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter for en rund, Depth) og Lapins **Lapin_OpeningVoid** (mærke, slags, vært, åbning og forskydning), knyttet til sin vært; åbningerne forbliver IfcOpeningElement. Hvert volumen beholder sit GlobalId fra eksport til eksport. I en mesh-eksport er de en gruppe for sig, **Opening voids**. Uden markering er eksporterne som før.

## MEP Pro

Ingeniørarbejdet på netværket — den belastning hver strækning bærer og den dimension der kan klare den, kontrollerne (åbne ender, skarpe bøjninger, fald på afløb, overbelastede kilder), føringsreglerne efter materiale (kobber i trin på 45°, PVC der drejer højst 45°), radiatorer foreslået under vinduer, føringer foreslået langs væggene, en strækningsprofil og et systems isometri — ligger i udgaven **Lapin MEP**, som føjer grupperne **Dimensionering** og **Assistenter** til denne fane.
