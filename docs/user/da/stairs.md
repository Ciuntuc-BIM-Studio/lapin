---
title: Trapper og ramper
order: 42
summary: Placér en trappe eller en rampe mellem etager eller op til en hvilken som helst højde, indstil den i dens type, og lav de få trin op til en trappeafsats eller en dør.
---

## Trapper

Vælg **Arkitektur → Trappe** (<kbd>ST</kbd>). Klik på trappens fod (midten af dens første stødtrin), og derefter på et punkt i den retning, den stiger. Til en L- eller U-trappe angiver et tredje klik til venstre eller højre for løbet, hvilken vej den drejer. Trappen stiger fra sin etage til etagen ovenover. Lapin beregner antallet af stigninger, stigningen og grunden ud fra højden og typens regler.

Markér trappen for at ændre den i egenskabspanelet:

- **Trappetype.** Formen, reglerne, konstruktionen og gelænderet kommer fra typen, som opsættes under **Arkitektur → Trappetyper**.
- **Bredde**, **Drej**, **Fodforskydning** og **Topforskydning.**
- **Stigning.** Lad feltet stå tomt, så stiger trappen til etagen ovenover. En værdi giver trappen en egen stigning, uanset etagerne. Brug det til nogle få trin mellem to niveauer på samme etage.
- **Fast.** Antal stigninger, stigning, grund og reposlængde, når du vil have dem faste. Lad dem stå tomme, så reglerne bestemmer.
- **Kontroller.** Hvert tal holdt op mod typens regler, i grønt eller rødt.

En trappe beder dækket ovenover om en åbning over sig selv. Fjern fluebenet ved **Åbning i dækket ovenover**, hvis dækket skal forblive helt.

## Trappeformer og færdige typer

I **Arkitektur → Trappetyper** tilbyder fanen **Form**: **Lige**, **Lige med repos**, **L-form** og **U-form** (hver med repos eller med **spiraltrin**; en U uden lysning er en trappe med vending), **Spiraltrappe** (om en spindel, med dens **Spindelradius**), **Buet** (med dens **Indre radius**) og **Delt (to arme)**: et hovedløb op ad midten til en repos, derefter to løb tilbage på hver side. For en spiral- eller buet trappe angiver **Drejer (°)**, hvor langt den drejer; tomt bestemmer stigninger og grund, og angivet følger grunden heraf. Hver trappe kan også ændre det i sine egenskaber.

Biblioteket tilføjer færdige typer til projektet: trappe med vending, trappe med spiraltrin, spiraltrappe, buet trappe, frithængende trappe og delt trappe.

På fanen **Konstruktion** vælger **Bygget som** en trappeplade, massive trin på jorden, **Frithængende trin (svævende)** (hvert trin en plade fastgjort i væggen) eller **Trin på vanger**.

**Gelænderet** har sin **Udformning**: **Stolper og håndliste**, **Balustre** (højst **Afstand mellem balustre** fra hinanden), **Glaspaneler**, **Wirer** eller et **Massivt værn**; en firkantet eller rund **Håndliste**, dens højde, **Ind fra kanten**, hvor langt den **Fortsætter forbi enderne**, og dens farve. I egenskabspanelet sætter **Gelænder** det på andre sider end typen for én trappe.

![Spiraltrappens type med nummererede trin](shots/stair-spiral.webp)

### Nummererede trin

Under **I plan** på fanen **Form**: **Nummerér trinene** skriver hvert trins nummer i en cirkel fra første stigning; **Mærke** skriver stigningerne, stigning og grund og bredden ved foden; **„UP“ ved pilens fod** viser, hvor den begynder. Planer og tegninger viser dem.

![Nummererede trin i plan, en delt og en frithængende trappe](shots/stair-numbers-plan.webp)

![En spiral-, en delt og en frithængende trappe i 3D](shots/stair-shapes-3d.webp)

## Indgangstrin (en trappeafsats)

Trinene op til en hoveddør har en egen stigning: fra terrænet, nogle trin under stueetagen, op til ±0.00. Lapin har en færdig trappetype til dem, **Indgangstrin**: 2 m bred, tre stigninger på 150 mm, massiv beton støbt på terræn, uden gelænder.

![Indgangstrin foran en trappeafsats, i plan](shots/entrance-steps-plan.webp)

1. **Trappeafsatsen.** Tegn afsatsen som et dæk: vælg **Arkitektur → Dæk** (<kbd>SB</kbd>), og klik dens omrids uden for væggen, foran døren. Et dæks overside ligger i dets etages niveau (±0.00 på stueetagen), og typen giver dets tykkelse. Vil du ændre tykkelsen, så lav en dæktype til trappeafsatser under **Arkitektur → Dæktyper**, for eksempel 150 mm beton.
2. **Trinene.** Vælg **Arkitektur → Indgangstrin**. Klik på midten af afsatsens kant, og klik derefter på den side, trinene går ud til. Trinene kommer op til afsatsens kant og ender i ±0.00. De starter 450 mm lavere: deres **Fodforskydning** er −450 mm.
3. **Højden.** Markér trinene. Skriv under **Stigning** den reelle forskel mellem terrænet og afsatsen, for eksempel 300. Toppen bliver ved afsatsen, og foden flytter op: fodforskydningen bliver −300 mm, og trinene har to stigninger. **Bredde** gør dem lige så brede som afsatsen, for eksempel 2400.

![Indgangstrinenes egenskaber: stigning, fodforskydning, niveauer](shots/entrance-steps-properties.webp)

**Niveauer** viser, hvor trinene starter og slutter, for eksempel −0.45 → ±0.00. Med én stigning (en stigning på 150 mm) er trinet selve afsatsens kant, og der er ingen grund at bygge. Giv trinene to stigninger eller flere for at få grunde foran afsatsen.

Typen føjes til projektet, første gang du bruger den. Vil du ændre den, så åbn **Arkitektur → Trappetyper** og vælg **Indgangstrin**:

- **Form → Stiger → En egen højde.** Typens stigning, besteget **Op til etagens kote, nedefra (en indgang)**; hver trappe kan stadig ændre den med **Stigning**.
- **Regler → Regelsæt → indgangstrin.** Stigninger på 120–170 mm og grunde på 300–400 mm, hvor en enkelt stigning er tilladt.
- **Konstruktion → Bygget som.** **Massive, støbt på terræn** fylder trinene ned til deres bund. **Bundplade under trinene** bygger dem som en indendørs trappe.

![Typen Indgangstrin i trappekonfiguratoren](shots/stair-types.webp)

Vil du have en hvilken som helst anden trappetype til at stige en egen højde, så vælg **Stiger → Op til → En egen højde** på dens fane **Form** (se næste afsnit). Vil du lave en placeret trappe om til indgangstrin, så vælg **Indgangstrin** som dens **Trappetype**: den flytter ned, så dens top ligger i dens etages niveau.

![Huset med trappeafsats og indgangstrin i 3D](shots/entrance-steps-3d.webp)

I IFC er trinene en IfcStair af typen STRAIGHT_RUN_STAIR. Dens løb er en IfcStairFlight med sine NumberOfRisers, NumberOfTreads, RiserHeight og TreadLength, og Pset_StairCommon bærer de samme tal. Trappeafsatsen er en IfcSlab.

## En trappe op til en hvilken som helst højde

En trappe behøver ikke gå fra én etage til den næste. På typens fane **Form** vælger **Stiger → Op til** **Etagen ovenover** eller **En egen højde** med dens **Stigning**, og **Starter** siger, hvor den begynder:

- **Op til etagens kote, nedefra (en indgang).** Den ender i etagens kote og begynder stigningen længere nede, som indgangstrin.
- **Fra etagens kote og op (en terrasse, en platform).** Den begynder i etagens kote og stiger op: for eksempel 1,20 m op til en terrasse eller en læsserampe.

Hver placeret trappe kan stige en anden højde: skriv den i **Stigning** i dens egenskaber (tom: typens). Stiger den fra koten, holder en ny stigning foden på plads; kommer den nedefra, holder den toppen.

## Rummet under trappen

Rummet under et trappeløb kan indrettes: et opbevaringsskab, et kontor i skab, en læsekrog, en entré med bænk og knager, et legehus til småbørn eller en hundeseng. Vælg en trappe med lige løb, og klik på **Rummet under trappen…** nederst i dens egenskaber (eller **Arkitektur → Rum under trappen**).

![Konfiguratoren for rummet under trappen: læsekrogen under løbet](shots/under-stair-dialog.webp)

Til venstre står de færdige stile: **Opbevaringsskab**, **Kontor i skab**, **Læsekrog**, **Entré og bryggers**, **Legehus til småbørn** og **Hundehus**, og over dem projektets egne stile. Vælg en, og ret den i midten:

- **Placering.** **Under løbet** (på en L- eller U-trappe), den **Åbne side** (som standard væk fra drejet), hvor det **Begynder ved** og **Slutter ved** langs løbet (tom: fra hvor undersiden er 300 mm over gulvet til sidste stødtrin) og dets **Dybde** (tom: så bred som trappen).
- **Fronten.** **Den lave del** lukket med **Skabslåger** (hver omtrent **Lågebredde**, med **Hylder bag lågerne**), et **Lukket panel** eller ladt åben; en **Reol i den høje ende**.
- **Hovedelement.** Et **Skrivebord med en hylde** (og en lampe), en **Bænk med hynder** (skuffer i fronten), en **Entrébænk og knager** (rum til sko under den), en **Legehusdør** (og et **Vindue ved siden af døren**) eller en **Hundeseng** bag en indrammet åbning, hver med sine mål.
- **Materialer.** **Pladetykkelse** og materialerne til **Korpus**, **Fronter** og **Stof**, blandt projektets.

Til højre viser forhåndsvisningen rummet set fra den åbne side under løbets underside og oppefra, dets længde, dybde og højder og dets dele. Hver del skæres af skråningen; en del uden plads (et skrivebord, hvor trappen er for lav) udelades, og forhåndsvisningen siger det. **Opret** eller **Anvend** er ét fortryd-trin. **Gem som stil…** gemmer indretningen som en stil i projektet; **Opdatér stilen** gemmer den igen, og alle stilens rum følger med.

![Rummet under trappen i egenskaberne](shots/under-stair-properties.webp)

Rummet er et element for sig, båret af sin trappe. Når trappen ændres (bredde, drejning, trin, type), bygges rummet igen under den, og det slettes sammen med den. Placeringsfelterne står i egenskaberne, og **Indret…** åbner konfiguratoren igen.

![De seks stile under seks trapper](shots/under-stair-3d.webp)

Planer, snit, opstalter og tegningsark tegner det som ethvert andet element: skåret hvor planen skærer, set nedenunder.

![Læsekrogen i plan, de andre ved siden af](shots/under-stair-plan.webp)

![Et snit langs løbet gennem læsekrogen](shots/under-stair-section.webp)

**Arkitektur → Liste over rum under trapper** viser hvert rums dele med antal, materiale og volumen og totaler pr. materiale. I IFC er rummet en IfcElementAssembly ("Under-stair space") af IfcFurniture (hylder, skrivebord, bænk, hynder, seng), IfcDoor, IfcPlate, IfcDiscreteAccessory (greb, knager) og IfcLightFixture, forbundet med sin IfcStair.

## Ramper

Vælg **Arkitektur → Rampe** (<kbd>RU</kbd>). Klik på midten af rampens nederste ende og derefter på et punkt i den retning, den stiger; for en L-rampe eller en rampe i zigzag angiver et tredje klik til venstre eller højre for løbet, hvilken vej den drejer. Første gang får projektet bibliotekets **Ramp 6 %**: 1,20 m bred med 6 % hældning, i løb på højst 9 m med reposer på 1,50 m imellem, en plade af armeret beton under en belægning og håndlister i begge sider. Den stiger op til etagen ovenover, medmindre dens type eller dens **Stigning** siger noget andet.

**Arkitektur → Indgangsrampe** placeres som indgangstrin: klik på midten af trappeafsatsens kant og derefter på den side, rampen går ud til. Den stiger 450 mm fra terrænet op til ±0,00, massiv på terrænet, og **Stigning** ændrer den som ved trinene.

Rampetyper laves i den samme konfigurator, **Arkitektur → Trappetyper**, med **Ny rampetype** eller fra biblioteket: **Switchback ramp** (i zigzag) og **Entrance ramp** (indgang):

- **Form.** **Lige**, **Lige, med reposer**, **L-form** eller **I zigzag (frem og tilbage)** med sin **Afstand mellem løb**; **Bredde**, **Hældning** i procent, **Længste løb** (er rampen længere, deles den i lige lange løb med reposer imellem) og **Reposlængde**. Længden i plan er højden divideret med hældningen: 3 m ved 6 % er 50 m. **Stiger** virker som for trapper.
- **Regler.** **Største hældning** (8 % som standard), **Mindste bredde** og **Fri højde**. Kontrollerne viser med rødt en hældning, der er stejlere end reglerne tillader, et løb, der er længere end det længste, og en repos, der er kortere end 1,50 m eller rampens bredde.
- **Konstruktion.** **En skrå plade af armeret beton** med sin **Pladetykkelse**, eller **Massive, støbt på terræn**; belægning og rækværk som for trapper.

![Zigzagrampen i konfiguratoren](shots/ramp-types.webp)

Vælg en rampe for at se dens type, **Bredde**, **Drej**, **Fodforskydning** og **Stigning**, en egen **Hældning** og **Reposlængde** (tomme: typens), længderne af dens løb og dens kontroller. Planer tegner dens løb og reposer, pilen op og hældningen i procent langs hvert løb. En rampe, der stiger op til etagen ovenover, beder om en åbning i dens dæk, som en trappe gør.

![Zigzagrampen og indgangsrampen i plan, med hældningen langs hvert løb](shots/ramps-plan.webp)

![Ramperne i 3D](shots/ramps-3d.webp)

I IFC er en rampe en IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP eller HALF_TURN_RAMP) med en IfcRampType, lavet af IfcRampFlight, reposer (IfcSlab LANDING) og IfcRailing.

## Rækværker

**Architecture → Railing** (<kbd>RL</kbd>) tegner et rækværk for sig langs kanten af en altan, en terrasse eller et galleri: klik dets punkter, Enter afslutter. Det har trapperækværkernes udformninger (stolper og håndliste, balustre, glas, wirer eller et lukket panel), sin højde (1 m som standard), basisforskydning, håndlistens profil og størrelser og stolpeafstand i egenskaberne; dets punkter trækkes i deres håndtag (Ctrl/⌘-klik på et styks midte tilføjer ét). I IFC er det et IfcRailing (GUARDRAIL).
