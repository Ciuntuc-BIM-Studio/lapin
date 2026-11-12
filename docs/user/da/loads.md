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

Et materiale uden densitet udelades, og Oversigten siger hvilket.

## Et rums nyttelast

Vælg et rum: dets egenskaber har en sektion **Nyttelast**. Dets **Kategori** er **Fra funktion** (en stue, et soveværelse eller et køkken er A, et kontor B, et lager E1, en garage F), medmindre du vælger en anden, eller **None**. Under den står den **qk**, kategorien giver.

![Et rums nyttelast i dets egenskaber](shots/loads-room.webp)

## Tabellen over nyttelaster

Fanen **Nyttelaster** viser hver kategori med dens anvendelse og dens **qk** i kN/m², som EN 1991-1-1 anbefaler. Skriv dit nationale anneks' værdi i feltet; **Ændret** går tilbage til standardværdien.

![Fanen Nyttelaster](shots/loads-imposed.webp)

## Oversigt

Fanen **Oversigt** viser for hvert tilfælde de laster, du har tegnet, og totalen i kN, i alt og på hvert niveau, derefter de afledte laster én for én med deres oprindelse. Den advarer om rum, hvis kategori ikke har noget tilfælde, og noterer, hvad der blev udeladt.

![Fanen Oversigt](shots/loads-summary.webp)

## Kombinationer

Fanen **Kombinationer** viser kombinationerne af tilfældene efter EN 1990 med de anbefalede værdier, vejledende: i brudgrænsetilstanden 1,35 på de permanente laster og 1,5 på den ledende variable, de øvrige med deres ψ0; i anvendelsesgrænsetilstanden den karakteristiske, hyppige og kvasi-permanente. Et tilfælde, der ikke rummer noget, deltager ikke.

![Fanen Kombinationer](shots/loads-combinations.webp)

## I IFC-filen

**Eksportér IFC, Fragments eller BOS…** har **Structural Analysis View (analytisk model og laster)**: filen bærer så den analytiske model af de bærende vægge, dæk, søjler og bjælker med lasttilfælde, laster og kombinationer, som en IFC Structural Analysis View. Se [Import og eksport](import-export.md).
