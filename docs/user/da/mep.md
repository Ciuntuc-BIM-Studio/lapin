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

## MEP Pro

Ingeniørarbejdet på netværket — den belastning hver strækning bærer og den dimension der kan klare den, kontrollerne (åbne ender, skarpe bøjninger, fald på afløb, overbelastede kilder), føringsreglerne efter materiale (kobber i trin på 45°, PVC der drejer højst 45°), radiatorer foreslået under vinduer, føringer foreslået langs væggene, en strækningsprofil og et systems isometri — ligger i udgaven **Lapin MEP**, som føjer grupperne **Dimensionering** og **Assistenter** til denne fane.
