---
title: Træ
order: 54
summary: Træskeletvægge fra konfiguratoren: stolper i modul, remme, noggings, og hver dør og hvert vindue indrammet efter sine regler.
---

## Træskeletvægge

En vægtype kan bygges som træskelet: **Wood → Timber frame** åbner konfiguratoren med projektets vægtyper til venstre. Sæt kryds i **Timber frame**, og typens kerne bygges af dele i stedet for ét lag: remmene, stolperne i deres modul, rækkerne af noggings og omkring hver dør og hvert vindue de gennemgående stolper, bærestolperne, overliggeren og bundstykket. De øvrige lag bliver, som de er, og isoleringen fylder hulrummene mellem delene.

![Træskelet-konfiguratoren](shots/wood-frame.webp)

- **Forudindstillinger**: **nordisk** (45 × 145 ved 600 mm, C24) eller **rumænsk** (50 × 150 ved 625 mm, C24, en række noggings ved 1,25 m). Bagefter kan hvert felt ændres.
- **Stolper, afstand, kvalitet, remme, endestolper, rækker af noggings**. **Modul fra** angiver, hvor stolpernes modul begynder: ved hver vægs start eller ved projektets origo, så en facades vægge deler det.
- **Bærende**: en skillevæg bærer intet og får et liggende stykke over sine åbninger i stedet for en overligger.

## Døre og vinduer

Dørene og vinduerne forbliver arkitektens: flyt et, eller skift dets type, og indramningen følger. Rammen måles fra **råhulmålet**: døren eller vinduet plus monteringsfugerne (i siderne, foroven, under et vindue; en dør går ned til gulvet).

Hver åbning tager reglen for sit **breddetrin** (tabellen under **Openings**: op til 900 mm, op til 1800 mm, derover, som standard): gennemgående stolper og bærestolper i hver side, overliggeren (ingen, massiv, LVL eller limtræ, dens tværsnit og lag) og bundstykkets lag. Ingen stolpe står i et råhul; de korte over overliggeren og under bundstykket holder modulet, så pladernes samlinger stadig rammer stolper. To åbninger tættere end den **mindste murpille** deler én overligger og de yderste gennemgående stolper. En åbning for tæt på væggens ende meldes i væggens egenskaber, flyttes aldrig.

Vælg en dør eller et vindue: dets afsnit **Wood** viser råhulmålet, reglen og hvad det får. Sæt kryds i **Egen regel** for at give netop den åbning andre stolper, en anden overligger eller et andet bundstykke.

## Hjørner, plader og skeletopstalten

- **Hjørner**: hvor en anden væg stopper mod denne vægs ende (stødt hjørne), får den gennemgående væg **tre stolper** (én efter den andens inderside), et **Californien**-hjørne (en liggende stolpe på indersiden, til beklædningen) eller **kun sine endestolper**. Et geringshjørne beholder begge vægges endestolper.
- **T-samlinger**: hvor en væg stopper mod denne i dens forløb, får denne **en stolpe i hver side** eller **stolper og stigeklodser** imellem.
- **Plader**: sæt kryds ved et lag (OSB, træfiberplade, gipsplade) og angiv pladens format: laget lægges som stående plader med samlingerne på stolpemodulet, skåret ved råhullerne og langs toppen. **Wood → Boards schedule** tæller pladerne for hver væg, nettoarealet og spildet.
- **Skeletopstalt**: i en skeletvægs afsnit **Wood** laver knappen et snit langs væggen gennem kernens midte, 1:20, alle dele skåret, med en målkæde gennem råhullerne, stolpemodulet under den og hvert råhuls størrelse. Den placeres på et ark som enhver visning.

## Præfabrikerede elementer

Sæt flueben ved **Prefabricated panels** for at lave skelettet på værkstedet: angiv det **længste element** (så langt som værkstedets bord og lastbilen tillader) og det **korteste**. Hver væg deles i elementer ved den længste samling inden for den længde, i stolpemodulet hvor det kan lade sig gøre, aldrig gennem en åbnings gennemgående stolper, bærestolper, overligger eller bundrem, og aldrig gennem en T-samlings forstærkning. Hvert element bygges for sig, med egne endestolper og remme ved samlingerne. Et element, der ikke kan holdes inden for længden (en for bred åbning), forbliver helt, og væggen siger det.

![En præfabrikeret væg i konfiguratoren](shots/wood-panels.webp)

Elementerne mærkes med væggens navn og deres nummer langs den (W3-P1, W3-P2…). Væggens **Wood**-afsnit viser deres længder, skeletopstalten tegner samlingerne og mærkerne over væggen, og **Wood → Wall panels** viser hvert element med væg, længde, højde, åbninger, dele, trævolumen og masse, så de kan bestilles, laves og løftes i rækkefølge. Pladerne på et element bærer også dets mærke. I IFC er hvert element en IfcElementAssembly (Timber frame panel) med dets dele og plader.

## CLT

I samme konfigurator kan en væg- eller dæktype bygges **af CLT-elementer**: lagene (hvert lags tykkelse, 3, 5, 7 eller 9), kvaliteten, det længste og højeste (eller bredeste) element pressen laver, samlingen mellem elementer (stød, halv overlap, fer), hvor langt en åbning holder sig fra elementkanten og monteringsfugen. En væg deles i elementer på langs, med samlingerne flyttet væk fra åbningerne; hvert element er kernens egen krop med råhullerne skåret ud. Et dæk deles over spændet (den korte vej, langs x eller y). **Wood → CLT panels** viser hvert element med mærke, mål, lag, kvalitet, samling, areal og volumen; i IFC er hvert et IfcBuildingElementPart kaldet CLT panel.

- **Samlinger**: i en **halv-overlapning** (half lap) overlapper de to halvdele med det angivne mål; en **fjedersamling** (spline) har en fals på den ene side og en krydsfinerstrimmel i den, som en del for sig. Begge er modelleret i elementerne, så volumener og skema er det, værkstedet skærer.
- **Åbningernes hjørner**: en fræser kan ikke skære et skarpt indvendigt hjørne. **Fræserens radius** efterlader den i hvert hjørne; en **dog-bone-overskæring** skærer en cirkel i hvert hjørne, så en firkantet karm passer. Konfiguratoren tegner fræsekonturen.
- **Elementopstalt**: en CLT-vægs **Wood**-afsnit laver et snit langs den i 1:20 med elementernes samlinger og mærker og åbningernes råmål.
- **CLT-tage**: vælg et skråt tag og sæt flueben ved **CLT panels** i dets **Wood**-afsnit: dets flader lægges som CLT-plader i stedet for spær (murremme og tagdækning bliver), med ovenlysvinduer og kviste skåret ud, i strimler med elementets bredde langs tagfoden, stødt sammen hvor en flade er længere end et element.

## Bjælkelag

En dæktype kan bygges **på bjælker**: vælg den i konfiguratoren og vælg **on joists**. Angiv bjælkernes tværsnit, afstand og styrkeklasse, hvilken vej de spænder (den korte vej, eller langs x eller y), om en **kantbjælke** lukker kanterne, **rækkerne af afstivere** og omkring hvert hul i dækket (en trappe, en skakt) **udvekslingsbjælkerne** på hver side og **lagene i vekslen** for enderne. Bjælker, som hullet afbryder, stopper ved vekslen, og en ekstra bjælke kommer ind, hvor udvekslingerne efterlader et mellemrum større end afstanden. **Fill the cavities** lægger kernens materiale mellem bjælkerne. Konfiguratoren viser et prøvedæk med trappehul.

![En dæktype på bjælker](shots/wood-joists.webp)

Bjælkerne erstatter dækkets kerne; lagene over og under bliver. Deres dele står i trælisten, og i IFC er hver en IfcBeam (JOIST, kantbjælke eller veksel efter navn) under dækket.

## Trælisten

**Wood → Timber schedule** laver en skæreliste over alt træ i projektet, væggenes dele og tagkonstruktionerne: del, tværsnit, kvalitet, længde, stk., samlet længde og volumen, med summer pr. tværsnit. I IFC er hver del et IfcMember (STUD, PLATE, overligger og bundstykke ved navn) under sin væg, og isoleringen et IfcCovering INSULATION.
