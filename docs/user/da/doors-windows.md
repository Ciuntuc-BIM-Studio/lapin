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

**Topform** former dens top: **Rektangel**, **Segmentbue** (en flad bue), **Elliptisk bue** (en halv ellipse, en fjerdedel af bredden høj, medmindre du angiver andet), **Halvcirkel** eller **Afrundede hjørner** (en lige top med afrundede øverste hjørner). **Buens pilhøjde** angiver, hvor højt en segment- eller elliptisk bue rejser sig, **Hjørneradius**, hvor runde hjørnerne er (150 mm, medmindre du angiver andet, aldrig mere end halvdelen af bredden); tom gælder formens egen. Hullet i væggen følger omridset i 3D, i facader og snit og i IFC; planen viser lysningerne som før.

## Konfiguratoren

**Arkitektur → Døre og vinduer** åbner konfiguratoren. Alle projektets dør- og vinduestyper står til venstre. Du kan ændre:

- typens navn og mærke (bogstavet i mærkerne);
- dens bredde og højde;
- karmens dybde og bredde;
- dens kolonner og rækker af rammer, og hvordan hver ramme åbner;
- lysningens overflade, indfatningen på facaden, dørtrinnet og farverne.

Tegningerne til højre viser typen, som den vil blive tegnet i opstalt, plan og snit, mens du ændrer den.

**Venstre og højre.** En dørs eller et vindues hængsling navngives, som IFC navngiver den: set af den, der skubber den op. En dør *swing left* har hængslerne til venstre for den, der skubber den op; set fra det rum, den åbner ind i, sidder de altså til højre. Gitteret **Rammer** er set udefra, fra den side rammerne skubbes fra, så dets venstre er navnenes venstre. Facaden er set indefra, fra den side rammerne åbner mod, medmindre **Udefra** er slået til; ligesom planen viser den døren, som den står i modellen. En dør vendt til den anden hængsling (**Hængsling** eller en vendepil langs væggen) er spejlbilledet af sin type; skemaerne viser den på sit eget kort (se [Skemaer](schedules.md)).

![Konfiguratoren til døre og vinduer](shots/joinery-configurator.webp)

**Gem type** gemmer dine ændringer. Alle døre eller vinduer af den type følger dem. **Placér med den** lukker konfiguratoren og begynder at placere den type.

### Vinduer med flere rammer

**Kolonner af rammer** tager rammernes relative bredder, ét tal for hver: `1 1` er to lige store rammer, `1 1 1` tre, `1 2 1` en bred mellem to smalle. **Rækker af rammer** deler vinduet i højden på samme måde, nedefra og op: `1 1` giver to rækker. Seks rammer er `1 1 1` i Kolonner af rammer og `1 1` i Rækker af rammer. Vælg derefter under **Rammer**, hvordan hver ramme åbner: fast, sidehængt, dreje-kip, bundhængt, tophængt, drejeramme eller skydeparti.

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

**Sprosser** deler ruderne: **Gitter (kolonial)** med ruder i bredden og højden, **Prairie** med sprosser nær kanterne, eller et **Rudemønster**.

På facaden kan et vindue ud over indfatningen have en **Slutsten**, en **Krone over åbningen** (et **Gesims**, en **Fronton**, en **Buet fronton** eller en **Vandnæse**, der følger en buet top) og et **Brystningsfelt under sålbænken** med konsoller.

**Anvend en stil…** sætter form, sprosser og udsmykning på én gang: Moderne, Klassisk, Georgiansk, Victoriansk, Romansk, Barok, Gotisk, Tudor, Prairie, Traditionel eller et rundt vindue. Alt kan stadig ændres.

![Et barokvindue: rundbue med vifte, slutsten og buet fronton](shots/joinery-styles.webp)

![Buede vinduer i 3D](shots/window-styles-3d.webp)

### Greb

**Greb** vælger typens greb: **Dørgreb**, **Knop**, **Trækstang**, **Vinduesgreb** eller intet. Som standard har en dør et dørgreb og et vindue et vinduesgreb. En dør har greb på begge sider i den **Grebshøjde**, du angiver; **Grebets farve** sætter farven. Greb vises i 3D, i snit og opstalter og i konfiguratorens tegninger, når **Beslag** er markeret.

### Parametre

**Parametre · i skemaer**, nederst i konfiguratoren, rummer typens egne parametre: **+ Brandmodstand** (holdt i IFC's Pset_DoorCommon eller Pset_WindowCommon som FireRating), **+ Producent**, **+ Varenummer**, **+ Websted** eller enhver parameter, du navngiver. Typens døre og vinduer arver dem, og hver bliver en række i snedkerskemaerne: se [Skemaer og diagrammer](schedules.md).

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
