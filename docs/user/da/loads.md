---
title: Belastninger
order: 51
summary: Lasttilfælde, flade-, linje- og punktlaster, rummenes nyttelaster, de afledte laster og kombinationerne efter EN 1990, til en statisk beregning.
---

Gruppen **Belastninger** på fanen **Konstruktion** rummer de laster, en statisk beregning tager: **Lasttilfælde…**, **Fladelast**, **Linjelast** og **Punktlast**. Værdierne er i kN, kN/m og kN/m².

![Gruppen Belastninger på fanen Konstruktion](shots/ribbon-loads.webp)

> De værdier, Lapin leverer (nyttelasterne fra EN 1991-1-1 og faktorerne fra EN 1990, de anbefalede), er **vejledende**. Indsæt værdierne fra dit nationale anneks, og kontrollér dem, før du bruger resultaterne.

## Lasttilfælde

**Lasttilfælde…** åbner dialogen Belastninger. **Tilføj standardtilfælde** opretter G1 (egenvægt), G2 (overflader og skillevægge), Q-A (nyttelast, kategori A), S (sne) og W (vind).

![Fanen Tilfælde i dialogen Belastninger](shots/loads-cases.webp)

Et tilfælde har en **Kode**, et **Navn**, en **Type** (Permanent, Variabel eller Ulykke), en **Kilde** (Egenvægt / permanent last, Færdiggørelse (overflader), Nyttelast, Sne, Vind, Jordskælv eller Brugerdefineret) og, for et nyttelasttilfælde, en **Kategori**. De tre felter ψ0, ψ1 og ψ2 er kombinationsfaktorerne; lad dem stå tomme for at tage kategoriens fra tabellen. En værdi, der ikke er et tal fra 0 til 1, markeres og gemmes ikke.

**Tilføj manglende tilfælde** opretter ét nyttelasttilfælde for hver kategori, rummene bruger, og som ingen har.

## Placering af laster

- **Fladelast**: klik på hjørnerne, og tryk derefter på <kbd>Enter</kbd>.
- **Linjelast**: klik på dens to ender.
- **Punktlast**: klik, hvor den virker.

Mens et værktøj er aktivt, tilbyder kommandolinjens bjælke lastens **værdi** og det **lasttilfælde**, den hører til (som standard **Nyttelast (Q-A)**). Lasten kommer på det aktive niveau.

![En fladelast på 2,5 kN/m² tegnet i stuen](shots/loads-plan.webp)

## Det, Lapin regner ud for dig

Dem tegner du ikke; Lapin afleder dem af modellen, hver gang du spørger, og de følger enhver ændring:

- **Egenvægten** af dæk, vægge, søjler og bjælker ud fra materialernes densiteter, i det tilfælde, hvis kilde er egenvægt (G1).
- **Dækkenes overflader** ud fra deres lag og de **ikke-bærende vægge** som linjelaster, i det tilfælde, der opregner overflader og skillevægge (G2).
- **Rummene**: nyttelasten for et rums kategori over dets areal, i den kategoris tilfælde (Q-A, Q-B…).
- **Sneen** og **vinden**, så snart du angiver stedets værdier (se [Sne og vind](#sne-og-vind)).

Et materiale uden densitet udelades, og Oversigten siger hvilket.

## Et rums nyttelast

Vælg et rum: dets egenskaber har en sektion **Nyttelast**. Dets **Kategori** er **Fra funktion** (en stue, et soveværelse eller et køkken er A, et kontor B, et lager E1, en garage F), medmindre du vælger en anden, eller **None**. Under den står den **qk**, kategorien giver.

![Et rums nyttelast i dets egenskaber](shots/loads-room.webp)

## Tabellen over nyttelaster

Fanen **Nyttelaster** viser hver kategori med dens anvendelse og dens **qk** i kN/m², som EN 1991-1-1 anbefaler. Skriv dit nationale anneks' værdi i feltet; **Ændret** går tilbage til standardværdien.

![Fanen Nyttelaster](shots/loads-imposed.webp)

## Sne og vind

Fanen **Sne og vind** indeholder stedets klima. Lapin leverer ikke de nationale kort: aflæs værdierne for dit sted i **CR 1-1-3/2012** (sne, figur 3.1 eller bilag A) og **CR 1-1-4/2012** (vind, figur 2.1 eller bilag A), eller i det nationale anneks, du arbejder efter, og skriv dem ind.

![Fanen Sne og vind](shots/loads-climate.webp)

- **Sne**: **Snelast på terræn s0,k (kN/m²)**, **Eksponeringsfaktor Ce** (fuld 0,8, delvis 1,0, beskyttet 1,2), **Termisk koefficient Ct** (1,0, medmindre taget er glasset og taber varme) og **Betydningsfaktor γIs** (1,0 for almindelige bygninger). **Påfør sneen**.
- **Vind**: **Basishastighedstryk qb (kN/m²)**, **Terrænkategori** (0 til IV, fra havet til en bymidte) og **Betydningsfaktor γIw**. **Påfør vinden**.

Når de påføres, oprettes de tilfælde, deres laster kommer i:

- **S**, sneen uden snedrev, og **S+X**, **S-X**, **S+Y**, **S-Y**, sneen med snedrev fra en vind, der blæser mod hver side: på et sadeltag tager den flade, der vender mod vinden, det halve. Et snedrev, der ikke ændrer noget her (ingen flade vender mod den vind), udelades, og Oversigten siger det.
- **W+X/s**, **W+X/p** og så videre for de fire retninger: vinden, der blæser mod +X, −X, +Y, −Y, én gang med tagenes **sug** (/s) og én gang med deres **tryk** (/p).

Snedrevsarrangementerne og vindretningerne er alternativer: en kombination tager kun én af hver.

Det, Lapin regner ud af dem, hver gang du spørger:

- **Sne** på hver tagflade, pr. m² i plan: s = γIs · μ1 · Ce · Ct · s0,k, med μ1 = 0,8 op til 30°, derefter mindre, 0 ved 60° og stejlere; på et dæk, intet dækker (en terrasse, et fladt tag), er μ1 = 0,8.
- **Vind** på vægge og tage: bygningens omrids er det konvekse hylster af dens vægge og søjler; facaderne tager zonerne A til E, de flade tage F til I, et sadeltag F til J, efter koefficienterne i EN 1991-1-4, ved trykket i bygningens højde for terrænkategorien. En facade leder sin vind til bjælkerne, sprosserne og dækkanterne langs den, dens laveste bånd til jorden; et tag til dets spær eller åse. Valmtage, indvendigt tryk og snedrev ved brystværn er ikke afledt endnu.

> Disse værdier er **vejledende**: koefficienterne er normernes anbefalede. Kontrollér dem mod dit nationale anneks.

## Oversigt

Fanen **Oversigt** viser for hvert tilfælde de laster, du har tegnet, og totalen i kN, i alt og på hvert niveau, derefter de afledte laster én for én med deres oprindelse. Den advarer om rum, hvis kategori ikke har noget tilfælde, og noterer, hvad der blev udeladt.

![Fanen Oversigt](shots/loads-summary.webp)

## Kombinationer

Fanen **Kombinationer** viser kombinationerne af tilfældene efter EN 1990 med de anbefalede værdier, vejledende: i brudgrænsetilstanden 1,35 på de permanente laster og 1,5 på den ledende variable, de øvrige med deres ψ0; i anvendelsesgrænsetilstanden den karakteristiske, hyppige og kvasi-permanente. Et tilfælde, der ikke rummer noget, deltager ikke.

![Fanen Kombinationer](shots/loads-combinations.webp)

## Analyse

**Kør analyse** (i samme gruppe og i dialogen Belastninger) analyserer rammen under disse laster: se [Konstruktionsberegning](analysis.md).

## I IFC-filen

**Eksportér IFC, Fragments eller BOS…** har **Structural Analysis View (analytisk model og laster)**: filen bærer så den analytiske model af de bærende vægge, dæk, søjler og bjælker med lasttilfælde, laster og kombinationer, som en IFC Structural Analysis View. Se [Import og eksport](import-export.md).
