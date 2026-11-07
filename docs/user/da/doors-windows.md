---
title: Døre og vinduer
order: 30
summary: Placér døre og vinduer i vægge, flyt dem, og udform deres typer i konfiguratoren.
---

<!-- video doors-windows -->

## At placere en dør eller et vindue

Vælg **Arkitektur → Dør** (<kbd>DR</kbd>) eller **Vindue** (<kbd>WN</kbd>), og klik derefter på en væg, hvor du vil have åbningen. Lapin skærer åbningen i alle væggens lag og tilføjer den overligger, vægtypen beder om.

Mens Door eller Window er aktivt, viser egenskabspanelet den type, det sætter, med dens størrelse: vælg en anden der, før du klikker på væggen.

![En plan med en dør og vinduer](shots/openings-plan.webp)

Vil du flytte en dør eller et vindue langs sin væg, så markér den og træk i den. Du ændrer den i egenskabspanelet: dens type, dens brystningshøjde, og til hvilken side den åbner (**Side**). Med flere døre og vinduer markeret ændrer **Hængsling**, **Side**, brystningen og typen dem alle i ét trin, og det samme gør en vendepil i planen.

Til en nøjagtig afstand bruger du **Flyt** (<kbd>M</kbd>) eller **Kopiér** (<kbd>CO</kbd>) i kommandolinjen, som for ethvert element: markér døren, vinduet eller vægåbningen, klik på et basispunkt, og skriv så afstanden (`500`) eller klik på et andet punkt. Kun den del af flytningen, der ligger langs væggen, tæller: åbningen glider langs sin væg og beholder sin brystning. Flyttes den ind i en anden væg på samme etage, går den over i den væg og åbner stadig på samme måde ind i rummet. En kopi er en ny dør eller et nyt vindue af samme type med det næste nummer. Kan den ikke være der (forbi væggens ende eller over en anden åbning), flytter intet sig. **Rotér** og **Spejl** placerer den på samme måde, og en spejling skifter dens hængsling.

![En dørs egenskaber](shots/door-properties.webp)

## Åbninger uden dør eller vindue

**Architecture → Wall opening** (<kbd>WO</kbd>) skærer en åbning alene gennem en væg, hvor du klikker: en gennemgang, en lem, et hul til en dør, der kommer senere. Dens bredde, højde og brystning indstilles i egenskabspanelet før klikket. Når den er valgt, ændres dens størrelse, dens plads langs væggen og dens brystning der; som en dør skal den passe i væggen og holde sig fri af væggens andre åbninger. I IFC er den en åbning, der udsparer væggen, uden noget, der fylder den.

Valgt har en åbning greb som et vindue: træk i midten for at skubbe den langs væggen, eller i en lysning for at gøre den bredere eller smallere, mens den anden bliver; længden ved musen kan skrives, som når du tegner. Den vælges med et klik i dens hul i planen eller i 3D-visningen, hvor dens omrids lyser op. **Flyt** (<kbd>M</kbd>) skubber den langs væggen med en skrevet afstand, og dens højde og brystning står i egenskaberne.

**Topform** former dens top: **Rektangel**, **Segmentbue** (en flad bue), **Elliptisk bue** (en halv ellipse, en fjerdedel af bredden høj, medmindre du angiver andet), **Halvcirkel** eller **Afrundede hjørner** (en lige top med afrundede øverste hjørner). **Buens pilhøjde** angiver, hvor højt en segment- eller elliptisk bue rejser sig, **Hjørneradius**, hvor runde hjørnerne er (150 mm, medmindre du angiver andet, aldrig mere end halvdelen af bredden); tom gælder formens egen. Hullet i væggen følger omridset i 3D, i facader og snit og i IFC; planen viser lysningerne som før.

**Lysningsoverflade** beklæder dens sider og top inde i hullet: **Puds** (15 mm, medmindre du angiver andet) eller en **Foring** (20 mm træ), med **Belægningens tykkelse** og **Belægningens materiale**; en åbning over gulvet beklædes også forneden. Overfladen er en del af væggens krop, i 3D, facader, snit og mængder, og den fri åbning er det, der er tilbage. **Overligger** er **Væggens regel** (den overligger, vægtypen kræver, som over en dør) eller **Ingen**. Flere valgte åbninger ændres sammen, i ét fortrydelsestrin.

**Rediger åbningen…** åbner dens konfigurator: de samme felter over en tegning af åbningen i væggen, anvendt samlet. **Skift til dør…** og **Skift til vindue…** fylder den med en dør eller et vindue af en valgt type, hvor den er: placering, brystning og GlobalId bliver, og **Tilpas typen til åbningen** giver den en type i dens størrelse og topform (projektets i den størrelse, ellers en lavet ud fra den valgte type og navngivet efter størrelsen). Den anden vej tager **Skift til åbning** på en valgt dør eller et valgt vindue det ud og efterlader hullet som en åbning af samme størrelse og form. Hvert er ét fortrydelsestrin.

![En vægåbning i dens konfigurator: halvcirkelformet top, lysningerne pudset](shots/wall-opening-dialog.webp)

## Konfiguratoren

**Arkitektur → Døre og vinduer** åbner konfiguratoren. Alle projektets dør- og vinduestyper står til venstre. Du kan ændre:

- typens navn og mærke (bogstavet i mærkerne);
- dens bredde og højde;
- karmens dybde og bredde;
- dens kolonner og rækker af rammer, og hvordan hver ramme åbner;
- lysningens overflade, indfatningen på facaden, dørtrinnet og farverne.

Tegningerne til højre viser typen, som den vil blive tegnet i opstalt, plan og snit, mens du ændrer den. Under dem viser en **3D-forhåndsvisning** typen i et stykke væg af projektets første vægtype, med lysning, sålbænke, foring og materialer, mens du ændrer den (også et karnap-, bue- eller kassevindue): træk for at dreje, brug hjulet til at zoome, træk med højre knap for at panorere. Den starter set udefra.

**Venstre og højre.** En dørs slagretning navngives, som arkitekter navngiver den: se fra håndtaget mod hængslerne; et dørblad, der åbner til venstre for den linje, har venstre slag, et, der åbner til højre, højre slag. Konfiguratorens valg for et dørblad siger det sådan, med hængslernes side i et felts **Åbningsmåde**, set udefra, fra den side rammerne skubbes fra: *Slag højre (hængsler til venstre)*. En IFC-fil navngiver hængslingen på sin egen måde (hængslerne set i den retning, dørbladet åbner), så den samme dør skrives der som SINGLE_SWING_LEFT; Lapin oversætter mellem de to, når den skriver og læser IFC. Et vindues venstre og højre ses indefra, fra den side rammerne åbner mod: *Sidehængt højre (hængsler til venstre)* i konfiguratoren, hvis felter ses udefra. IFC skriver det samme vindue som venstre. Facaden er set indefra, fra den side rammerne åbner mod, medmindre **Udefra** er slået til; ligesom planen viser den døren, som den står i modellen. En dør vendt til den anden hængsling (**Hængsling** eller en vendepil langs væggen) er spejlbilledet af sin type; skemaerne viser den på sit eget kort (se [Skemaer](schedules.md)).

![Konfiguratoren til døre og vinduer](shots/joinery-configurator.webp)

**Gem type** gemmer dine ændringer. Alle døre eller vinduer af den type følger dem. **Placér med den** lukker konfiguratoren og begynder at placere den type.

### Felter, sprosser og profiler

En dør eller et vindue er et gitter af felter. Hvert felt vælger, hvordan det åbner, og hvad det fyldes med, så én type kan rumme to dørblade med sidepartier og et overlys over dem, eller et vindue med et tæt panel under en ramme.

**Kolonner (relative)** og **Rækker (relative)** deler åbningen hurtigt: `1 1` er to lige store felter, `1 1 1` tre, `2 1` et bredt ved siden af et smalt. Rækker tælles nedefra og op.

**Kolonnebredder** og **Rækkehøjder** giver de samme mål i millimeter. Skriv dem, og den sidste tager resten. I facaden kan du også trække i en sprosse eller en tværpost; afstanden vises, mens du trækker.

**Sprossebredde** og **Tværpostbredde** er deres egne mål, adskilt fra **Rammebredde**. En karm på 50 mm med sprosser på 50 mm ser ud som 50 mm overalt.

**Felter** viser alle felter. Klik på et, i listen eller i facaden, for at redigere det:

- **Åbningsmåde**: fast, en ramme (sidehængt, dreje-kip, bundhængt, tophængt, drejeramme, skydeparti) eller, i en dør, et dørblad.
- **Fyldning**: **Glas** eller **Tæt panel**. En dørs blade og sidepartier kan være tætte, og det kan et vinduesfelt også (et panel under en ramme, et uigennemsigtigt felt).
- **Opdel**: **I kolonner** eller **I rækker**, i en afstand i millimeter fra feltets venstre kant eller underkant (halvdelen som standard). Delt på tværs beholder et dørblad sin nederste del, og den øverste bliver et fast glaseret overlys.
- **Sammenføj**: med feltet ved siden af, over eller under, når de deler en hel kant. Sprosser og tværposter, som intet felt længere har brug for, forsvinder.

![En butiksdør i én type: to dørblade, sidepartier og et gennemgående overlys](shots/joinery-panes.webp)

**Profilsystem** starter typen fra en profil i **PVC**, **Aluminium**, **Træ** eller **Stål (slank)**: det sætter karm-, sprosse-, tværpost- og rammemål og giver profilerne deres finere former. Karmen har en **Fals**, som rammen lukker mod, rammen dækker karmen med sit **Rammeoverlap**, **glaslister** holder glasset, og et vindue har en drypnæse under bundstykket. Hvert mål kan stadig redigeres. **Enkel (glatte profiler)** beholder enkle rektangulære dele som før.

![Et PVC-vindue: rammerne over karmen, glaslisterne, et tæt panel under et fast felt](shots/joinery-profiles-3d.webp)

Under **Detaljer** slår **Indvendig sålbænk** og **Udvendig sålbænk** sålbænkene til og fra, og **Lysningsbeklædning** slår lysningens afslutning til på siderne og foroven. Hvor sålbænke og beklædninger ikke bruges, så fjern fluebenet.

### Skodder, gitre, blomsterkasser og baldakiner

<!-- video window-dressings -->

Under **På facaden** kan en vinduestype have:

- **Skodder**: med lameller, fyldte eller af brædder, med én eller to fløje, vist åbne (slået ind mod væggen ved siden af vinduet) eller lukkede for det;
- et **Sikkerhedsgitter**: i lysningen indvendigt (standard, hvor sikkerhedsgitre normalt sidder), i lysningen udvendigt, på facaden eller på indersiden, med vandrette eller lodrette tremmer eller begge dele, deres tykkelse og afstand;
- en **Blomsterkasse** under vinduet;
- en **Baldakin** over det, med sit udhæng og sin hældning.

Hver af dem sidder efter væggens virkelige flader, hvor vinduet er, ud fra væggens lag, så den sidder rigtigt på enhver vægtype. Når vinduet har en indfatning, står det, der er på facaden, ud fra indfatningen. Du kan flytte hver af dem i hånden: **Fra vægfladen** angiver, hvor langt den står ud fra væggen (for et gitter i lysningen angiver **Trukket tilbage fra fladen**, hvor langt inde det sidder), **Fra vinduets akse** flytter den langs væggen, og **Op eller ned** flytter den i højden. De vises i 3D, i planer, opstalter og snit, og skemaerne måler dem.

![Skodder, et gitter, en blomsterkasse og en baldakin i konfiguratoren](shots/joinery-dressings.webp)

![Vinduerne med skodder og gitre, i 3D](shots/window-dressings-3d.webp)

### Former, stilarter og udsmykning

**Form** giver en dør eller et vindue en buet top (**Rundbue**, **Segmentbue**, **Elliptisk bue**, **Afrundede hjørner**, **Spidsbue**, **Trekantet top**) eller gør det **Rund eller oval** (rund når bredde og højde er ens). En buet type beholder sine rammer under en losholt ved buens vederlag, og derover et fast overvindue, der følger buen, med sprosser hvis du vil: **Vifte**, **Posterne ført op** eller **Kors**. **Buens pilhøjde** angiver, hvor højt en segment-, elliptisk, spids- eller trekantet top rejser sig, **Hjørneradius**, hvor runde hjørnerne på en top med afrundede hjørner er. Hullet i væggen, karmen, glasset, indfatningen og tegningerne følger omridset.

**Tværpost ved buens vederlag**, sat til som standard, beholder tværposten og det faste overvindue over den. Fjern fluebenet for et buet sidehængt vindue: den øverste række ruder går op i buen, og karmen, rammen, glasset, sprosserne og åbningssymbolet følger buen hele vejen rundt, uden nogen post under den. Ruden åbner, som du vælger (sidehængt, dreje-kip, fast), poster går op til buen, og planer, snit, opstalter, 3D-forhåndsvisningen og IFC viser det sådan. Stilen **Buet sidehængt vindue** sætter en segmentbue med én sidehængt ramme på én gang.

![Et buet sidehængt vindue: én sidehængt ramme der følger en segmentbue, uden tværpost under den](shots/joinery-arched-casement.webp)

**Sprosser** deler ruderne: **Gitter (kolonial)** med ruder i bredden og højden, **Prairie** med sprosser nær kanterne, eller et **Rudemønster**.

På facaden kan et vindue ud over indfatningen have en **Slutsten**, en **Krone over åbningen** (et **Gesims**, en **Fronton**, en **Buet fronton** eller en **Vandnæse**, der følger en buet top) og et **Brystningsfelt under sålbænken** med konsoller.

**Anvend en stil…** sætter form, sprosser og udsmykning på én gang: Moderne, Klassisk, Georgiansk, Victoriansk, Romansk, Barok, Gotisk, Tudor, Prairie, Traditionel eller et rundt vindue. Alt kan stadig ændres. Listen viser derefter typens stil; når du ændrer noget, stilen satte, står der *Georgiansk (ændret)*, og vælger du stilen igen, anvendes den igen.

![Et barokvindue: rundbue med vifte, slutsten og buet fronton](shots/joinery-styles.webp)

![Buede vinduer i 3D](shots/window-styles-3d.webp)

### Karnap-, bue- og kassevinduer

En vinduestype kan springe frem fra væggen. I konfiguratoren gør **Projects** den til en **Bay** (en midte og to skrå sider), en **Bow** (ens felter på en bue) eller en **Box** (sider vinkelret på væggen), og **New bay window** starter en ny type som en 45°-karnap. Vinduet udfylder stadig én åbning i væggen, så bred og høj som typen: det placeres, flyttes, kopieres, vendes, nummereres og listes som ethvert vindue. **Architecture → Bay window** (<kbd>BW</kbd>) sætter et i den væg, du klikker på: typen valgt i konfiguratoren, når den springer frem, ellers projektets første karnap, ellers en 45°-karnap.

Gitterets kolonner er dens flader. I en karnap og en kasse er første og sidste kolonne dens sider og kolonnerne imellem dens midte; et buevindue har én flade pr. kolonne, hvis bredder deler buen. Hvert felt åbner, som dets **Åbningsmåde** siger, så siderne kan åbne og midten være fast. Derefter:

- **Projection**, fra væggens yderside til karmenes yderside, og for en karnap vinklen **Sides to the wall** (15° til 90°);
- **Sides**: kassens sider glasede eller massive, med deres materiale;
- **Seat inside**: sædet ved brystningen, hvor langt det går ind i rummet, dets materiale;
- **Sill outside** under fladerne;
- **Base**: ingen (hænger på væggen), **vægge ned til gulvet**, **en fremspringende plade** eller **en plade på konsoller**, med tykkelse, udhæng og materiale;
- **Head**: et **valm**-, **halv**- eller **fladt tag** eller en **plade**, tykkelsen ved tagfoden, udhæng, hældning og materiale, og en **inddækning mod væggen**.

Karme, rammer, glas og greb er typens, så dens materialer og greb gælder for alle flader. **Bay style** sætter alt på én gang: 45°- eller lav 30°-karnap, karnap på gulvet, buevindue med fem eller fire felter, kasse med glasede eller massive sider, karnap på konsoller. Som ved **Anvend en stil…** viser listen vinduets karnapstil, med *(ændret)* når du ændrer det, den satte. **Save as bay style…** gemmer den konfigurerede som projektets stil ved siden af Lapins; omdøb eller slet den i listen under felterne.

I plan er fladerne snittet, sæde, sålbænk og sokkel tegnet under snittet og taget stiplet over dem; opstalter, snit og 3D viser hele kroppen. Vindueslisten har et kort for typen med fladerne foldet ud og en række **Projecting** (art, fremspring, flader); arealet er de glasede fladers, og detaljerne måler tag, sæde og sokkel. I IFC er det ét IfcWindow med brugerdefineret opdeling (*Bay 45°, projection 600, 3 faces, 4 panels*), hvis krop er hele karnappen, felterne flade for flade; Lapin læser karnappen tilbage.

![Et buevindue i konfiguratoren](shots/bay-configurator.webp)

![Karnap-, bue-, kasse- og konsolkarnapvinduer i plan](shots/bay-plan.webp)

![Karnap-, bue-, kasse- og konsolkarnapvinduer i 3D](shots/bay-3d.webp)

### Greb

**Greb** vælger typens greb: **Dørgreb**, **Knop**, **Trækstang**, **Vinduesgreb** eller intet. Som standard har en dør et dørgreb og et vindue et vinduesgreb. En dør har greb på begge sider i den **Grebshøjde**, du angiver; **Grebets farve** sætter farven. Greb vises i 3D, i snit og opstalter og i konfiguratorens tegninger, når **Beslag** er markeret.

### Parametre

**Parametre · i skemaer**, nederst i konfiguratoren, rummer typens egne parametre: **+ Brandmodstand** (holdt i IFC's Pset_DoorCommon eller Pset_WindowCommon som FireRating), **+ Producent**, **+ Varenummer**, **+ Websted** eller enhver parameter, du navngiver. Typens døre og vinduer arver dem, og hver bliver en række i snedkerskemaerne: se [Skemaer og diagrammer](schedules.md).

## Solafskærmning

**Architecture → Sunshade** (<kbd>SN</kbd>) sætter en solafskærmning på den dør eller det vindue, du klikker på: på størrelse med åbningen, ud over karmsiderne, over overliggeren eller foran, og den følger med, når den flyttes. **Sunshade along a facade** tager to punkter: på en væg løber den langs dens yderside, ellers langs linjen, ud til højre. Begge værktøjers bjælke vælger typen eller en stil, som en ny type laves af.

Arterne:

- **Canopy** (udhæng): plade, med lameller eller af glas, med fremspring, tykkelse, hældning og sternbræt, på konsoller, trækstænger eller stolper;
- **Vertical fins**: dybde, tykkelse, afstand og vinkel, skinner top og bund;
- **Louvres (brise-soleil)**: vandrette lameller, flade, vingeprofil eller kasse, dybde, afstand og vinkel, fri af væggen, mellem endeplader;
- **Egg-crate**: vandrette og lodrette plader sammen;
- **Perforated screen**: en plade fri af væggen, med runde eller firkantede huller eller slidser, størrelse og deling;
- **Awning** (markise): stof på skrå, kappe, kassette og knækarme;
- **Pergola**: spær, en bjælke, lægter ovenpå og stolper ned til gulvet;
- **External venetian blind**: lameller, hvor langt den er rullet ned, kasse og føringsskinner.

**Sunshade types** (under Types) redigerer en type: dens art og hver parameter, materialerne, ved siden af tegningen udefra, i snit og i plan. Hver solafskærmning af typen følger en ændring. En ny type starter fra en stil: betonudhæng, træudhæng på konsoller, glasudhæng på trækstænger, lameludhæng, teglhætte, brise-soleil, lodrette finner, betongitter, perforeret cortenskærm, stofmarkise, træpergola, udvendig persienne; **Save as style…** gemmer en type som projektets stil.

![Et udhængs egenskaber efter middagsberegningen](shots/sunshade-properties.webp)

Valgt viser en solafskærmning sin type, hvad den sidder på, og sit eget **Projection** (tomt: typens). **Full shade at noon, 21 June** beregner for et udhæng, en markise eller en pergola over et vindue, hvor langt det skal springe frem, for at hele vinduet er i skygge, når solen står højest på årets længste dag, på projektets sted (**Manage → Location**; Bukarest, indtil et er sat), og sætter det. På en sydfacade på 45° N står solen da 68,4° højt: et 1,5 m højt vindue under et udhæng 150 mm over overliggeren kræver 650 mm. Dens greb trækker enderne langs væggen eller linjen og fremspringet.

![Solafskærmningstyperne: lameller, udefra, i snit og i plan](shots/sunshade-types.webp)

Planer tegner en solafskærmning over snittet stiplet, en der krydser det snittet (finner, en skærm) og en under det set; opstalter, snit og 3D viser kroppen med materialets skravering, og den kaster sin skygge med resten (**View → Sun and shadows**). **Sunshade schedule** lister dem efter type med placering, længde og fremspring, antal og skygget areal; deres mængder (længde, fremspring, areal, volumen) tager typens prisposter. I IFC er de IfcShadingDevice af en IfcShadingDeviceType: en markise AWNING, en persienne JALOUSIE, lameller LOUVER i IFC 4.3 (brugerdefineret i IFC4), de andre brugerdefinerede (Canopy, Fins, Egg-crate, Screen, Pergola); Lapin læser dem tilbage, som de var.

![Solafskærmning i plan](shots/sunshades-plan.webp)

![Et udhæng, lameller, finner, en markise, en pergola og en persienne i 3D](shots/sunshades-3d.webp)

## Mærker i plan

**Annotér → Dør- og vinduesmærker** sætter et mærke på hver dør og hvert vindue i en plan: en linje på tværs af væggen, mærket udenfor, og størrelsen og brystningen indenfor. Vil du ændre, hvordan mærkerne ser ud, så gå til **Administrer → Tegningsstandarder → Mærker**.

### Rediger et mærke på skærmen

Hvert mærke kan lægges til rette i hånden, del for del. Klik på dets linje eller en af dets tekster for at vælge det: hver tekst har så et firkantet greb (træk for at flytte teksten) og et rundt efter dens ende (træk for at dreje teksten), og hver ende af linjen har et greb (træk for at forlænge eller afkorte linjen; teksten ved den ende følger med). Ctrl/⌘-klik på et greb sætter den del tilbage. Hvert træk er ét fortryd-trin. Er flere mærker i samme plan valgt, ændrer en del trukket på ét den samme del på dem alle.

![En dørs mærke valgt: et greb for hver tekst, dens drejegreb og et ved hver ende af linjen](shots/marker-grips.webp)

![Mærket flyttet og drejet, linjens ydre ende forlænget](shots/marker-edited.webp)

Ændringerne hører til mærket og dermed til den plan: samme dør kan lægges anderledes i en anden plan. De gemmes langs og på tværs af væggen, så de følger døren, når den flyttes, drejes eller vendes til den anden side. I mærkets egenskaber viser **Markør** dets dele: skriv en teksts drejning i grader eller en endes rækkevidde i millimeter på papiret, sæt en del tilbage med ↺, eller alle med **Sæt etiketten tilbage**. Motoren tegner mærkerne som redigeret, så tegninger, PDF og DXF udskriver dem ens.

![Et mærkes dele i dets egenskaber](shots/marker-properties.webp)

Om teksterne ligger på tværs af mærkets linje (langs væggen, som standard) eller langs den vælges for hver mærketype i **Administrer → Tegningsstandarder → Mærker**, kolonnen **Tekstretning**: **vinkelret på mærkelinjen** eller **langs mærkelinjen**. Begge veje tegnes de læselige, aldrig på hovedet; en tekst drejet i hånden holdes også læselig.

![Dørmærkets tekster langs mærkelinjen](shots/marker-along.webp)
