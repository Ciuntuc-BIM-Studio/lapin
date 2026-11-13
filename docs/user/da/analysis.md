---
title: Konstruktionsberegning
order: 52
summary: Kør en lineær statisk analyse af rammen under dine laster, læs diagrammerne for kræfter, momenter og deformation i 3D og indstil frigørelserne i stængernes ender.
---

**Kør analyse** i gruppen **Belastninger** på fanen **Konstruktion** (og i dialogen Belastninger) analyserer modellens stænger, plader og vægge under lasterne og kombinationerne fra [Belastninger](loads.md) og viser kræfterne i dem.

![Gruppen Belastninger med Kør analyse](shots/ribbon-analysis.webp)

> Resultaterne er **vejledende**. Laster, kombinationer og analysen skal du selv kontrollere, før du bruger dem.

## Hvad der analyseres

- **Stænger** (søjler, bjælker, stål- og træelementer) indgår med deres egen stivhed ud fra materiale og tværsnit. En stang, hvis materiale ikke har nogen elasticitetsmodul, udelades og opføres på en liste.
- **Dæk og plader af beton** analyseres som **skaller**: hver deles i små elementer (et net, som standard 0,5 m), der bøjer og strækkes og deler deres knuder med de bjælker, søjler og vægge, de møder. Pladen fører selv sine laster til det, der bærer den, ved sin og deres stivhed. En bjælke under en plade beholder sin egen akse nedenunder og virker sammen med pladen som en T-bjælke.
- **Bærende vægge af beton** er også skaller: de står på deres fundamenter (en væg uden noget modelleret under sig holdes langs sin fod) og bærer dækkene på dem samt vind og andre sidelaster i deres plan.
- **Murede vægge** og vægge af andre materialer er understøtninger: de holder pladerne og stængerne på dem, men analyseres ikke som vægge.
- **Dæk af andre materialer** (et trædæk) leder deres laster til stængerne og væggene under dem efter **45°-reglen**: hjørnernes halveringslinjer deler pladen i de områder, hver side bærer, og hvert område bliver en trapez- eller trekantlast på stangen nedenunder.
- **Tage med konstruktion** leder lasten til hvert spær efter dens **tilhørende bredde**, halvvejs til naboerne; sne tages pr. vandret projektion.
- **Rundstål og stag** (en massiv rund stang, der afstiver, et stag eller et kabel) virker aksialt: de er charnierled i enderne, og deres vægt føres til enderne, så de ikke bøjer ned som bjælker. Feltet **Aksialt belastet stang** i en stangs egenskaber under **Analyse** ændrer det: **Efter tværsnit og anvendelse**, **Ja (et rundstål eller stag)** eller **Nej (en bjælke)**.
- **Bjælker i forskellig højde**, der mødes ved en søjle, beholder hver sin kote; søjlen har ét knudepunkt, og bjælkerne er bundet til det med en stiv arm.
- **Vind på facaderne** går til bjælkerne, sprosserne og dækkanterne langs dem; båndet ved deres fod går direkte til jorden og opføres for sig, adskilt fra stængernes summer.
- Stængernes **egenvægt** tages langs dem, og de laster, du har tegnet, og de afledte går derhen, hvor de finder noget under sig. En last, der intet finder, udelades og meldes.

## Kørsel

Vælg **Kør analyse**. Dialogen **Analyseresultater** åbnes:

![Dialogen Analyseresultater](shots/analysis-results.webp)

- Sammenfatningen: beregningens stænger, skalelementer, knuder og ubekendte og hvor længe den tog.
- For hvert tilfælde **summen af dets laster** over for **summen af reaktionerne** og om de er i **ligevægt** (et flueben) eller ej (et kryds: stol ikke på de resultater). Ikke fordelte laster angives i kN.
- Hvad der blev **udeladt** af analysen, og hvorfor.
- De **største og mindste** kræfter og momenter, den største flytning og nedbøjning (med forholdet til spændet, L/300) for det valgte tilfælde, den valgte kombination eller envelope; under dem de største og mindste værdier på **plader og vægge** og hvor.
- **Skaller**: hvilke plader og vægge der analyseres som skaller (**Plader og vægge**, **Kun plader**, **Kun vægge** eller **Ingen**: pladerne efter 45°-reglen og væggene som understøtninger) og **Net**tets størrelse i meter. Et finere net er mere nøjagtigt og langsommere; et smalt stykke får altid mindst fire elementer på tværs. En ændring er ét fortryd-trin; **Kør igen** tager den med.

**Vis diagrammerne** lukker dialogen og viser resultaterne i 3D-visningen.

## Diagrammerne i 3D

Et kort over 3D-visningen vælger, hvad der tegnes:

![Bøjningsmomentet for envelopen i brudgrænsetilstand](shots/analysis-moments.webp)

- **Hvad**: et lasttilfælde, en kombination eller **envelopen** af kombinationerne i brudgrænsetilstand (BGT) eller anvendelsesgrænsetilstand (AGT): største og mindste værdi i hvert punkt.
- **Størrelse**: **N** normalkraft (træk positiv), **Vy** og **Vz** forskydningskræfter, **T** torsion, **My** og **Mz** bøjningsmomenter (**Mz** er en vandret bjælkes stærke akse, positivt med træk i undersiden) eller den **Deformeret** form.
- **Skala**: diagrammets størrelse på skærmen; selve modellen ændres ikke.
- **Understøtningskræfter**: pile ved understøtningerne med deres værdier i kN.

Hver stang viser sin basislinje, kurven, skravering imellem og værdierne ved yderpunkterne; rød er positiv, blå negativ. Momenter tegnes på den side, hvor stangen er trukket.

![Den deformerede form](shots/analysis-deformed.webp)

## Plader og vægge

Når plader eller vægge analyseres som skaller, har kortet en anden række knapper, **Plader og vægge**, og skallerne farves i bånd efter den størrelse, du vælger (et nyt klik på den fjerner farverne):

![Dækkets moment my, i bånd](shots/analysis-slab.webp)

- **w**: flytningen på tværs af pladen eller væggen (opad positiv på en plade).
- **mx**, **my**: bøjningsmomenterne i pladens x- og y-retning (global X og Y på en plade; langs og op på en væg), i kN·m pr. meter. På en plade betyder **positiv, at dens underside er trukket** (i marken), negativ dens overside (over en understøtning); på en væg den side, der er til højre for dens retning.
- **mxy**: vridningsmomentet; **nx**, **ny**, **nxy**: kræfterne i pladens plan (træk positiv), i kN pr. meter; **vx**, **vy**: forskydningskræfterne på tværs.

Signaturforklaringen på kortet giver hvert bånds værdier og enheden:

![Signaturforklaringen til konturerne](shots/analysis-legend.webp)

De samme bånd tegnes på plantegningen af pladens etage, til skærmen; tegningsark udskriver dem endnu ikke. For en envelope viser hvert punkt den af sin mindste og største værdi, der er størst i størrelse.

## Frigørelser i enderne

Vælg en søjle, en bjælke eller et element: dets egenskaber har et afsnit **Analyse** med frigørelserne **Start (i)** og **Slut (j)**.

![Afsnittet Analyse for en bjælke](shots/analysis-bar.webp)

- **Standard**: hvad forbindelsen siger (en lasker, en knudeplade eller et vindkryds gør elementet charnieret), et trætagelement charnieret om sin stærke akse, enhver anden stang indspændt.
- **Indspændt**: enden optager momenter. **Charnieret**: den drejer frit om begge bøjningsakser.
- **Brugerdefineret**: seks felter, ét for hver frihedsgrad (flytning langs stangen, langs hver forskydningsakse, torsion, rotation om hver bøjningsakse); en afkrydset frihedsgrad er frigjort.

En ændring er ét fortryd-trin.

## Når modellen ændres

Resultaterne beregnes ud fra modellen, som den er, og gemmes aldrig. Når du ændrer modellen, forsvinder diagrammerne fra visningen, og kortet siger det:

![Kortet efter en ændring](shots/analysis-pick.webp)

**Kør igen** analyserer den nye model.

## En mekanisme

Hvis konstruktionen kan bevæge sig uden modstand (for mange frigørelser eller en del, som intet holder), stopper analysen og siger, hvor:

![En mekanisme](shots/analysis-mechanism.webp)

Den nævner knuden, frihedsgraden og de berørte stænger; **Markér de involverede elementer** markerer dem, så du kan rette dem, som regel ved at indspænde en ende eller tilføje en understøtning.
