---
title: Udvidelser
order: 126
summary: Tilføj møbler, projektskabeloner, profiler, skraveringsmønstre, tegningshoveder og prislister lavet af andre fra kataloget Lapin Extensions, og fjern dem igen.
---

## Kataloget

**Lapin Extensions** er et gratis katalog over ting, der er lavet til Lapin af dets brugere og af os: møbelpakker, projektskabeloner, profilbiblioteker, skraveringsmønstre, tegningshoveder og prislister. Hver af dem kommer i det bibliotek, Lapin allerede har til den, så du bruger den, som om du selv havde lavet den.

Åbn **Filer → Udvidelser…**. **Gennemse** viser kataloget: søg på navn, tag eller det, en udvidelse indeholder, eller vælg en kategori.

![Udvidelser: Gennemse, detaljerne for en møbelpakke](shots/extensions-browse.webp)

Klik på en udvidelse for at se dens detaljer, før du installerer den:

- **Forfatter** og **Licens**. Hver udvidelse har en licens, valgt af dens forfatter. En *permissiv* (MIT, CC-BY, CC0…) lader dig bruge det, den indeholder, i ethvert projekt; ved en anden skal du *læse vilkårene* først.
- **Ikke-kommerciel**: en udvidelse, hvis licens forbyder kommerciel brug (CC-BY-NC, PolyForm Noncommercial…), er markeret sådan i listen og i dens detaljer. Du må installere den og lære af den, men ikke bruge den i betalt arbejde.
- **Virker med**: de Lapin-versioner, den er lavet til. Hvis den ikke passer til din Lapin, står det der i stedet for **Installér**.
- **Størrelse**, filen og dens **SHA-256**: fingeraftrykket af den fil, kataloget har gennemgået.
- **Indhold**: dens elementer, når forfatteren oplister dem.
- **Hjemmeside** og **Forumemne**: hvor du kan spørge forfatteren.

## Installér, opdatér, afinstallér

Klik på **Installér**. Lapin henter filen, tjekker at dens størrelse og SHA-256 er præcis dem, kataloget opgiver (en fil, der er ændret undervejs, afvises, og intet installeres), og lægger den så i sit bibliotek:

| Art | Hvor den havner |
|---|---|
| Møbler | **Interiør → Mine møbler…** |
| Projektskabeloner | **Filer → Ny fra skabelon…** |
| Profiler | profilbibliotekerne på denne computer |
| Skraveringsmønstre | Mine mønstre, som **Administrer → Materialer** tilbyder |
| Tegningshoveder | tegningshovederne på denne computer |
| Prislister | **Udførelse → Prislister…**, markeret som vejledende: priserne er forfatterens, sammenlign dem med dine egne |
| Typer | det projekt, der er åbent, når du installerer den, i ét trin at fortryde (**Administrer → Typepakker…** eksporterer dine egne) |

Har dit bibliotek allerede noget med samme navn, spørger Lapin, før det erstatter det. Intet, en udvidelse medbringer, bliver nogensinde kørt: udvidelser er indhold.

**Installeret** viser, hvad denne computer har. Hver række åbner det sted, hvor det havnede (**Mine møbler…**, **Projektskabeloner…**), tilbyder **Opdatér**, når kataloget har en nyere version, og **Afinstallér**.

![Installeret: møbelpakken, med knappen der åbner Mine møbler](shots/extensions-installed.webp)

![Pakkens møbeltyper i Mine møbler](shots/extensions-my-furniture.webp)

**Afinstallér** fjerner fra dine biblioteker det, udvidelsen tilføjede, og kun det. Det, du allerede har lagt i dine projekter, bliver i dem: en møbeltype indsat i et projekt, et projekt startet fra en skabelon.

## Typer

En **type**-udvidelse bringer væg-, dæk-, dør-, vindues-, søjle-, bjælke- eller trappetyper med det, de er lavet af: deres lag, materialer, profiler og parametre. Installeret kommer dens typer ind i det projekt, der er åbent nu, i ét trin at fortryde: et materiale med samme navn i projektet bruges, ethvert andet oprettes; en helt ens type, der allerede findes, genbruges, og en med samme navn, der er forskellig, tilføjes ved siden af med (2) efter navnet. **Installerede → Tilføj dens typer til dette projekt** bringer dem ind i et andet projekt.

![Detaljerne for en typeudvidelse: dens licens markeret ikke-kommerciel](shots/extensions-types.webp)

Dine egne typer kommer i en pakke fra **Administrer → Typepakker…**: sæt flueben ved typerne, giv pakken et navn, og **Eksportér typer…** gemmer en `.lapintypes`-fil. **Importér typer…** i samme dialog læser en, lader dig vælge dens typer og tilføjer dem på samme måde.

![Administrer → Typepakker…: en vægtype valgt til eksport](shots/type-packs-export.webp)

## Scripts

Kataloget oplister også **Python-scripts**. I skrivebordsprogrammet kan du installere et nu; det kører, når Lapin har sin Python-konsol, og kun når du kører det. Før du installerer det, siger dets detaljer, hvad det beder om (at læse eller ændre det åbne projekt, at nå filer eller internettet). Et script kan gøre på din computer det, du selv kan, så installér kun scripts fra forfattere, du har tillid til. Browseren tager kun indholdspakker.

![Detaljerne for et script: hvad det beder om, og at det ikke kan køres endnu](shots/extensions-script.webp)

## Et andet katalog

**Katalog…** nederst i dialogen sætter kataloget adresse: en https-adresse eller stien til en `index.json` på denne computer eller i en delt mappe (en virksomheds eget katalog). Lad det stå tomt for Lapins eget. Pilen ved siden af læser kataloget igen.

## Udgiv dine egne

Alle kan udgive en udvidelse: eksportér den fra Lapin (Mine møbler → Eksportér, Gem som skabelon…, et profilbibliotek, et tegningshoved), vedhæft filen til en release i et af dine GitHub-repositories, og tilføj dens manifest til kataloget med en pull request på [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). Dens README fortæller hvordan, og hvad de automatiske tjek ser på.

En typepakke kommer fra **Administrer → Typepakker… → Eksportér typer…**; en prisliste fra **Prislister…**, med eksportknappen for en liste på denne computer (en `.lapinprices`-fil, dens priser markeret vejledende).
