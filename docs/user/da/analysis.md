---
title: Konstruktionsberegning
order: 52
summary: Kør en lineær statisk analyse af rammen under dine laster, læs diagrammerne for kræfter, momenter og deformation i 3D og indstil frigørelserne i stængernes ender.
---

**Kør analyse** i gruppen **Belastninger** på fanen **Konstruktion** (og i dialogen Belastninger) analyserer modellens stænger under lasterne og kombinationerne fra [Belastninger](loads.md) og viser kræfterne i dem.

![Gruppen Belastninger med Kør analyse](shots/ribbon-analysis.webp)

> Resultaterne er **vejledende**. Laster, kombinationer og analysen skal du selv kontrollere, før du bruger dem.

## Hvad der analyseres

- **Stænger** (søjler, bjælker, stål- og træelementer) indgår med deres egen stivhed ud fra materiale og tværsnit. En stang, hvis materiale ikke har nogen elasticitetsmodul, udelades og opføres på en liste.
- **Bærende vægge** er understøtninger: de holder stængerne på dem, men analyseres ikke som vægge.
- **Dæk og plader** bærer ikke som plader; de leder deres laster til stængerne og væggene under dem efter **45°-reglen**: hjørnernes halveringslinjer deler pladen i de områder, hver side bærer, og hvert område bliver en trapez- eller trekantlast på stangen nedenunder.
- **Tage med konstruktion** leder lasten til hvert spær efter dens **tilhørende bredde**, halvvejs til naboerne; sne tages pr. vandret projektion.
- **Rundstål og stag** (en massiv rund stang, der afstiver, et stag eller et kabel) virker aksialt: de er charnierled i enderne, og deres vægt føres til enderne, så de ikke bøjer ned som bjælker. Feltet **Aksialt belastet stang** i en stangs egenskaber under **Analyse** ændrer det: **Efter tværsnit og anvendelse**, **Ja (et rundstål eller stag)** eller **Nej (en bjælke)**.
- **Bjælker i forskellig højde**, der mødes ved en søjle, beholder hver sin kote; søjlen har ét knudepunkt, og bjælkerne er bundet til det med en stiv arm.
- **Vind på facaderne** går til bjælkerne, sprosserne og dækkanterne langs dem; båndet ved deres fod går direkte til jorden og opføres for sig, adskilt fra stængernes summer.
- Stængernes **egenvægt** tages langs dem, og de laster, du har tegnet, og de afledte går derhen, hvor de finder noget under sig. En last, der intet finder, udelades og meldes.

## Kørsel

Vælg **Kør analyse**. Dialogen **Analyseresultater** åbnes:

![Dialogen Analyseresultater](shots/analysis-results.webp)

- Sammenfatningen: beregningens stænger, knuder og ubekendte og hvor længe den tog.
- For hvert tilfælde **summen af dets laster** over for **summen af reaktionerne** og om de er i **ligevægt** (et flueben) eller ej (et kryds: stol ikke på de resultater). Ikke fordelte laster angives i kN.
- Hvad der blev **udeladt** af analysen, og hvorfor.
- De **største og mindste** kræfter og momenter, den største flytning og nedbøjning (med forholdet til spændet, L/300) for det valgte tilfælde, den valgte kombination eller envelope.

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
