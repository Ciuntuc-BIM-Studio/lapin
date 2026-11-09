---
title: Stål
order: 56
summary: Stålstænger med indstillelige ender – forlænget eller trukket tilbage, skåret efter et plan, i gering, tilpasset til en anden stang, udklinket – også lavet ud fra et tegnet omrids; hulprofiler og koldformede profiler; rammehaller med vouter.
---

## Fanen Stål

![Fanen Stål](shots/ribbon-steel.webp)

- **Rammehal…**: en hel stålhal ud fra få tal, i halkonfiguratoren (nedenfor).
- **Stang** (<kbd>SM</kbd>): en stålstang mellem to punkter, ved etagens overkant med profilets overside på aksen, som en bjælke. Dens type vælges i Egenskaber før første klik, eller fra fanen **Katalog**, hvor hver ståltype er tegnet med sit profil.
- **Konvertér til stang**: de valgte tegnede omrids bliver stænger (nedenfor).
- **Geringssamling** og **Tilpas**: to stænger samlet ved deres ender (nedenfor).
- **Ståltyper**: projektets stangtyper, og nye.

En stang er et IfcMember: dens akse går hvor som helst i rummet (et skråstag, et spær, en stolpe), dens profil kommer fra dens type, den kan drejes om sin akse (**Rotation**), og aksen kan gå gennem profilets midte, overside eller underside (**Indsættelsespunkt**). Konstruktionens bjælker og søjler af et stålprofil får de samme ender.

## Ender

Vælg en stang, eller en lige bjælke eller søjle af et stålprofil: dens sektion **Stål** i Egenskaber viser dens to ender, **Start** og **Slut**. Hver ende er et sæt regler; legemet følger dem.

![En stangs ender i Egenskaber](shots/steel-ends.webp)

- **Forlængelse**: hvor langt enden når forbi sit punkt; negativ er enden trukket tilbage (en monteringsfuge).
- **Snit**: **Vinkelret på aksen**; **Efter et plan**, drejet fra det lige snit **I plan** (om profilets lodrette akse) og **I opstalt** (om dets side), i grader; **Geringssamling** med en anden stang, på planet der halverer hjørnet mellem dem; **Tilpas til en stang**, på den flade af den anden stang, den kommer til (en søjles flange, eller siden af dens krop, når søjlen er drejet).
- **Udklinkning**: flangerne udklinket i **Top** og/eller **Bund**, med **Udklinkningens længde** tilbage fra enden og **Udklinkningens dybde** fra profilets kant, som for en bjælke, der føres ind på en hovedbjælke.

Gering og tilpasning nævner den anden stang: flyt den, og enden følger med; slet den, og enden er lige igen og siger det. **Geringssamling** på fanen samler de to valgte stænger i gering i det hjørne, hvor de mødes; **Tilpas** tilpasser den første valgte til den anden.

## Fra skitse til stang

Tegn stangens omrids som en lukket polylinje eller et rektangel (**Tegn**), og vælg så **Stål → Konvertér til stang** med et eller flere omrids valgt. I ét trin læses hvert omrids og erstattes af en stang:

![To omrids skitseret på planen](shots/steel-sketch.webp)

- dets **to lange parallelle kanter** (inden for en halv grad) giver aksen, deres midterlinje fra ende til ende, og **bredden** mellem dem;
- **kanten over hver ende** giver den endes snit: lige, eller efter et plan med kantens hældning.

På en **plan** er omridset stangen set ovenfra: dens bredde er flangernes, og et profil med den bredde findes blandt projektets stangtyper og i kataloget (ellers den type, der er valgt til Stang); stangen ligger ved etagens overkant. På et **snit** eller en **opstalt** er omridset stangen set fra siden: dens bredde er profilets højde, en skrå ende er et snit i opstalt, og et retvinklet **hak** ved en ende er en **udklinkning** foroven eller forneden; stangen ligger, hvor den er tegnet. Et hak tegnet på en plan afvises (en udklinkning ses fra siden). Et omrids, der ikke er et bånd (en trekant, et L, kanter der ikke er parallelle), bliver som det er, og Lapin siger det.

## Ståltyper

**Stål → Ståltyper** viser projektets stangtyper med deres vægt pr. meter og laver nye: et profil fra kataloget (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** og de generiske koldformede **C**, **Z** og **Σ**) eller et hulprofil eller koldformet profil **med egne mål** (højde, bredde, tykkelse, læbe). Den nye type kan være en stangtype, eller en **bjælketype** eller **søjletype** i konstruktionen, så værktøjerne Bjælke og Søjle også sætter stål. Et klik på en type i listen gør den til den, Stang sætter.

![Dialogen Ståltyper](shots/steel-types.webp)

Enhver anden profil kommer fra bibliotekerne: **Fra et profilbibliotek…** søger i dem alle (se [Profilbiblioteker](profiles.md)).

Hulprofiler er hule indvendigt, i 3D-visningen, i snittene og i mængderne. De koldformede profiler er generiske former med mål, skarpt bukkede, uden en producents katalog.

![Stænger i 3D](shots/steel-3d.webp)

## Rammehaller

**Stål → Rammehal…** åbner halkonfiguratoren. Som tagets har den stilene til venstre, hallens felter i faner i midten og til højre hallen, som Lapin ville bygge den: en typisk ramme set langs hallen, med sine vouter, og en oversigt over, hvad den vejer.

![Halkonfiguratoren](shots/steel-hall.webp)

- **Stile**: katalogets typiske haller (**Hal 20 m**, **Hal 30 m**, et **Halvtag 10 m** med ensidig hældning) og projektets egne. En valgt stil udfylder alle felter; ethvert felt kan derefter ændres. **Gem som stil…** gemmer hallen, som den er konfigureret, som en stil i projektet; **Opdatér stilen** gemmer den igen i dens stil, og alle haller med stilen følger med.
- **Ramme**: **Spændet** (fra søjleakse til søjleakse), **Fagene** (lige store som `5 × 6`, eller hver for sig: `6, 6, 7.5`), **Højden ved tagfod** (spærenes overside over søjlens yderside), **Taget** (sadeltag eller ensidig hældning) og dets **Hældning**, profilerne for **Søjler** og **Spær**.
- **Vouter**: **Voute ved tagfod** og **Voute ved kip**, hver med sin **Længde** langs spæret fra søjlens flade (eller kippen) og sin **Dybde** under spæret der, og hvordan den er **Udført**: **skåret af spærets profil** (et I skåret skråt langs kroppen, den ene halvdel svejst under spæret: spærets egen krop og flange) eller **svejst af plader** (en **Kropsplade** og en **Flangeplade** i egen tykkelse, og flangens bredde).

![Vouterne svejst af plader](shots/steel-hall-haunches.webp)

- **Gavle**: **gavlsøjler under et simpelt spær** (hjørnesøjler og søjler i den valgte **Afstand**, deres stærke akse på tværs af gavlvæggen, under et lettere spær, der går ud til væggenes yderside, søjlerne tilpasset dets underside), eller **en almindelig ramme** i hver ende.

![En gavl med søjler](shots/steel-hall-gable.webp)

- **Sekundært stål**: åsene, facadereglerne, afstivningen, tagfodsbjælken og kipstiverne (nedenfor).
- **Placering**: hvor akse A og akse 1 krydser hinanden, og spændets retning i plan (**Rotation**); forhåndsvisningen viser hallens akser ovenfra.

**Opret hal** laver den i ét trin, som én fortrydelse tager tilbage: dens egne **akser** (akse 1, 2… ved rammerne, A og B ved søjlerne), og ved hver ramme to **søjler** (konstruktionens søjler af et stålprofil, kroppen i rammens plan, toppen skåret efter tagets hældning) og to **spær** i gering ved kippen (ét, fra den lave søjle til den høje, ved ensidig hældning): stålstænger tilpasset søjlernes flader, med deres vouter. Hallen står på den aktuelle etage; tagfod og kip er højder i dens rammer, ikke nye etager. Oversigten giver rammerne, højderne og stålet efter art: søjler, spær, vouter, gavlsøjler, i alt og pr. m² gulv.

![Hallen i 3D](shots/steel-hall-3d.webp)

Hallen kan stadig redigeres. Vælg en af dens stænger: dens afsnit **Stål** viser hallen og **Redigér hal…**, som åbner konfiguratoren på den; **Anvend** bygger hallen igen over de samme stænger, som beholder deres identitet (en stang, der ikke længere skal bruges, forsvinder, en ny tilføjes). Spærets vouter står også der, hver ændret for sig (art, længde, dybde, plader): en voute indstillet sådan bliver, som den er, når hallen bygges igen; **×** fjerner den.

![Et spærs vouter i Egenskaber](shots/steel-hall-props.webp)

## Samlinger

En hals rammer kommer med deres **samlinger** (Steel → Portal frame hall…, fanen **Knuder**): en **søjlefod** under hver søjle og gavlsøjle, en **tagfodssamling** hvor hvert spær møder sin søjle, en **kipsamling** hvor to spær mødes, og en knudeplade under spæret over hver gavlsøjle. Hver er sit eget element, der kender de stænger, den samler; dens plader, bolte og ankre laves af dens parametre og af stængerne, som de er, og følger dem, når hallen ændres.

![Fanen Knuder](shots/steel-nodes-hall.webp)

- **Samlinger**: til eller fra for hele hallen.
- **Søjlefødder**: **charnier** (en fodplade, to ankre på hver side af kroppen eller fire mellem flangerne) eller **indspændt** (en længere, tykkere plade, fire til otte ankre uden for flangerne, ribber i kroppens plan). **Ankrene** er **lige**, **med krog** eller **med ankerplade** forneden; **Antal ankre** og **Ankerlængde** (under betonens overside) vælges efter profilet, når de står tomme. Står der et fundament fra konstruktionen under søjlen, går ankrene ned i det (og stopper 50 mm over dets bund); ellers går de deres længde under understøbningen.
- **Tagfodssamling**: en udragende endeplade på spæret, boltet til søjlens flange: dens **Pladetykkelse**, **Boltdiameter** og **Boltklasse**, hvor langt den står **Over spæret** (med en boltrække der) og **Søjleafstivningerne** ud for spærets overflange og voutens flange.
- **Kipsamling**: de to spærs endeplader boltet sammen på geringen, med deres vouter.

Det, der står tomt, vælges efter profilerne: bolte fra M16 til M27 efter højden af den boltede stang, en plade så tyk som dens bolte, kant- og rækkeafstande efter hullerne. En samling trækker sine stængers ender tilbage med det, den sætter imellem dem (spæret med sin endeplade, søjlen med sin fodplade, gavlsøjlen med en spalte på 20 mm), gennem stængernes egne enderegler.

![En tagfodssamling](shots/steel-nodes-eaves.webp)

![En indspændt søjlefod på ankre med krog](shots/steel-nodes-base.webp)

Vælg en samling for at se den i Egenskaber: de stænger, den samler, hver parameter som den er nu (en størrelse valgt efter profilet vises som sådan) og dens dele: plader, bolte og ankre efter størrelse og klasse, svejsningerne efter a-mål med deres længde, dens vægt, det fundament ankrene går ned i. En parameter ændret der gælder kun den samling, og en hal bygget igen beholder den. **Redigér hal…** åbner dens hal.

![En samling i Egenskaber](shots/steel-nodes-props.webp)

Stænger tegnet i hånden får deres samlinger med **Steel → Find samlinger**: en fod under hver lodret stang, en tagfodssamling hvor en stang er tilpasset en søjle, en gavlsøjles hvor en søjle er tilpasset under en stang, en kipsamling hvor to stænger er geringsskåret med hinanden, et stød hvor to løber i forlængelse, ende mod ende; for de valgte stænger, eller hele modellen når intet er valgt. **Steel → Ståldele** laver listen over samlingernes plader, bolte, ankre og svejsninger. En slettet samling giver sine stænger deres ender tilbage; en slettet stang tager sine samlinger med; **Slet hal** i konfiguratoren fjerner hallen med dens stænger, samlinger og akser i ét trin.

## Sekundært stål

En hal kommer med sit **sekundære stål** (Steel → Portal frame hall…, fanen **Sekundært stål**), lavet med den og igen med den, når den ændres; hver stang beholder sin identitet:

![Fanen Sekundært stål](shots/steel-secondary-hall.webp)

- **Sekundært stål**: til eller fra for hele hallen.
- **Åse**: deres **Profil** (koldformede Z, C og Σ, en U eller en let I), **Åsafstanden** langs taghældningen, hvor langt de går **Ud over gavlene**, og deres **Længder**: **én pr. fag** (fra ramme til ramme) eller **fuldt optrukket** (hele hallens længde). De ligger på spærenes overside med kroppen vinkelret på taget, fra 150 mm inden for søjlernes yderside til 200 mm fra kippen, i så mange rækker, som afstanden kræver.
- **Facaderigler**: deres **Profil** og **Afstanden mellem facaderigler**, fra **Første rigel ved** op til under tagfoden, én pr. fag på søjlernes yderside, kroppen vandret og læberne nedad; på gavlene fra søjle til søjle (eller mellem en gavlrammes søjler), så højt som spæret over dem tillader. En **Tagfodsbjælke** tager toppen af langvæggene (med sin **Profil**), reglerne en afstand under den.
- **Afstivning**: de **Afstivede fag** (deres numre, fra det første), stængernes **Profil** (rundstål Ø16 til Ø30, vinkler eller rør) og hvor: **I taget** (et X i tagets plan under spærenes overflange, i felter omtrent så lange som faget) og **I langvæggene** (et X i væggenes plan gennem søjlernes akser, fra den første rigels højde til under vouten).
- **Kipstivere**: fra hver anden ås og facaderigel ned til spærets underflange og søjlens indre flange, på begge sider, uden for vouterne.

![Det sekundære stål i 3D](shots/steel-secondary-3d.webp)

Hver ås og rigel sidder på en **åseholder** på hvert spær, hver søjle eller gavlsøjle den krydser: en plade svejst på stangens flade ved siden af åsens krop (uden for kroppen på en C, på den side af en Z, der vender væk fra dens underflange), boltet gennem kroppen, to bolte pr. åseende (M12 op til 250 mm højde, M16 derover). En hjørnesøjle, der er smallere end søjlerne, får en højere åseholder op til riglen. Hvert X sidder på **knudeplader** svejst på spærenes eller søjlernes krop, en diagonal på hver side; en vinkel eller et rør boltes med to bolte, rundstålets gevindende tager sin møtrik. En kipstiver er boltet i begge ender. De er samlinger som rammernes: vælg en for at se dens plade og bolte i Egenskaber og ændre dem dér. Konfiguratorens oversigt tæller og vejer åse, regler, tagfodsbjælke, afstivning og kipstivere; listen **Steel parts** har deres åseholdere, knudeplader og bolte.

![En ås på sin åseholder, en kipstiver under den](shots/steel-secondary-cleat.webp)

![Tagafstivningen på sine knudeplader](shots/steel-secondary-bracing.webp)

## Lister og tegninger

**Steel → Steel members** laver listen over projektets stål: stængerne og bjælker og søjler af et stålprofil, efter hal (dem uden hal til sidst), et **Mærke** for stænger af én slags, der er ens i profil, længde og endesnit (C søjler, R spær, GP gavlsøjler, P åse, SR facaderigler, EB tagfodsbjælker, B afstivning, FB kipstivere, M andre stænger, BM bjælker), nummereret fra det tungeste profil og den længste. En række giver **Profil**, **Længde**, **Snit ved start** og **Snit ved slut** (hvor meget endens plan drejer fra vinkelret, og en udklinkning: tilskæringslistens vinkler), **Antal**, vægten pr. meter, **Pr. stk.** og i alt, og arealet til **Maling**; en sum under hver hal og én for det hele. **Steel parts** viser samlingernes plader, bolte, ankre og svejsninger, med deres åseholdere og knudeplader.

![Stålstangslisten](shots/steel-members.webp)

**Steel → Anchor plan** tegner hallens søjlefødder til fundamenterne: en plan af hallens etage i 1:50, snittet lige over fodpladerne, hvert anker med en cirkel, dets afstand til aksen (± når ankrene står symmetrisk om den), hver fod mærket efter sin type (**B1**, **B2**…: samme plade, ankre, styrkeklasse og understøbning), rastrets fag og spænd, og tabellen over typerne ved siden af hallen: antal, art, plade, ankre (antal, størrelse, form), styrkeklasse og understøbning. **Steel → Frame elevation** tegner en ramme (den typiske eller den valgte stangs) set langs hallen i 1:50 med spænd, tagfods- og kiphøjde. Begge er visninger af hallen (bedt om igen åbnes den samme) og kommer på tegningsark som alle andre; de følger hallen, når den ændres.

![En ankerplan](shots/steel-anchor-plan.webp)

![En rammes opstalt](shots/steel-frame-view.webp)

## IFC og mængder

En stang eksporteres som **IfcMember** med sin **IfcMemberType**. Dens legeme skrives, som de andre programmer skriver stål (Tekla, Advance Steel, Revit): dens profil **ekstruderet** (IfcExtrudedAreaSolid), **beskåret** af endernes planer (IfcBooleanClippingResult med IfcHalfSpaceSolid), og hver udklinkning et **IfcOpeningElement**, der skærer den ud. En stålbjælke eller -søjle med ender skrives på samme måde. Hver stangs mængder giver dens længde mellem snittene, profilets areal, dens volumen og dens nettovægt (7850 kg/m³).

En hal eksporteres som et **IfcBuildingSystem** (LOADBEARING), som dens stænger er tildelt, med dens data i egenskabssættet `Lapin_SteelHall` og sit eget **IfcGrid**. Et spær med vouter er en **IfcElementAssembly** af spæret og dets vouter: en voute skåret af profilet er en **IfcMember**, en svejst af plader to **IfcPlate**, hver ekstruderet (IfcExtrudedAreaSolid). Vouterne tælles ikke med i spærets mængder; konfiguratorens oversigt vejer dem. Åsene eksporteres som **IfcMember** PURLIN, afstivning og kipstivere som BRACE, facaderegler og tagfodsbjælke som USERDEFINED (**Side rail**, **Eaves beam**), alle tildelt hallens system med deres samlinger.

En samling eksporteres som en **IfcElementAssembly** af dens plader (**IfcPlate**), bolte (**IfcMechanicalFastener** BOLT), ankre (ANCHORBOLT) og ankerplader (**IfcDiscreteAccessory** ANCHORPLATE), hver med størrelse og klasse i `Lapin_SteelPart`; den realiserer forbindelsen mellem de stænger, den samler (**IfcRelConnectsWithRealizingElements**; en fod, mellem fundamentet og dens søjle). Svejsningerne forbliver data, ikke legemer.
