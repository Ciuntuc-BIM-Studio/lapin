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

Har dit bibliotek allerede en prisliste, et profilbibliotek eller en skraveringsfil med samme navn, som er anderledes, tilbyder **Forhåndsvisning af import** at beholde begge (med (2) efter navnet), erstatte den eller springe over; for en projektskabelon med samme navn spørger Lapin, før det erstatter den. Intet, en udvidelse medbringer, bliver nogensinde kørt: udvidelser er indhold.

Lapins katalog er også **signeret** af Lapin Extensions: kataloget og hver af dets filer bærer en signatur, som Lapin kontrollerer, før det læser listen eller installerer en fil, så en kopi, der er ændret undervejs, afvises. En linje over listen siger **Signeret af Lapin Extensions**, når det er tilfældet.

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

## Betalte udvidelser

Nogle udvidelser **sælges af deres forfattere** gennem deres egen butik (i dag Gumroad). De er markeret **Betalt** i listen med deres pris; prisen er *vejledende*: det er sælgerens pris ved betalingen, der gælder. Lapin Extensions tager ingen provision og er ikke part i salget: sælgeren modtager betalingen, udsteder fakturaen og håndterer refusioner efter de vilkår, som **Sælgerens vilkår** åbner.

![Detaljerne for en betalt udvidelse: solgt af forfatteren, Køb, licensnøglen](shots/extensions-paid.webp)

1. Klik på **Køb**: sælgerens betalingsside åbner i din browser. Efter betalingen får du en **licensnøgle** (på e-mail eller på kvitteringssiden).
2. Tilbage i **Filer → Udvidelser…** indsætter du nøglen i **Licensnøgle** og klikker på **Installér**. Lapin sender nøglen til butikken, som tjekker den hos sælgerens butik og derefter giver Lapin filen; Lapin tjekker dens størrelse, SHA-256 og signatur som for alle andre.

Nøglen gemmes **på denne computer** (i Lapins indstillinger), så du skriver den kun én gang: opdateringer bruger den. Lapin tjekker den igen, når en opdatering tilbydes, eller en gang om ugen; uden internet blokeres intet, og det installerede virker fortsat.

Hvis sælgeren refunderer købet eller deaktiverer nøglen, får udvidelsen **ikke flere opdateringer**, og Installeret siger det; det installerede **bliver**. **Glem licensnøgle** i Installeret fjerner den gemte nøgle (næste opdatering beder om den igen). En betalt udvidelse kan også installeres i browseren, fra butikken, med samme nøgle.

## Et andet katalog

**Katalog…** nederst i dialogen sætter kataloget adresse: en https-adresse eller stien til en `index.json` på denne computer eller i en delt mappe (en virksomheds eget katalog). Lad det stå tomt for Lapins eget. Pilen ved siden af læser kataloget igen. Et katalog af dit eget behøver ikke være signeret: Lapin siger så over listen, at det **ikke er signeret af Lapin Extensions**, så installer kun fra det, hvis du stoler på, hvem der vedligeholder det. Et katalog, hvis signatur ikke holder, bliver aldrig læst.

## Udgiv dine egne

Kataloget har sit eget websted, **[store.lapinbim.com](https://store.lapinbim.com)**: en side for hver udvidelse med dens billeder, versioner, licens og downloads, og en side for hver forfatter.

Alle kan udgive en udvidelse. Eksportér den fra Lapin (Mine møbler → Eksportér, Gem som skabelon…, et profilbibliotek, et tegningshoved), og derefter:

- **I butikken**: log ind på store.lapinbim.com med din konto fra [forummet](https://forum.lapinbim.com), åbn **Your extensions** (dine udvidelser), udfyld formularen, og upload filen og nogle skærmbilleder. Butikken kontrollerer filen, som Lapin vil gøre, og sender den til gennemgang; din side viser gennemgangen og, når den er udgivet, hvor mange gange den er downloadet. En ny version går på samme måde.
- **På GitHub**: vedhæft filen til en release i et af dine repositories, og tilføj dens manifest med en pull request på [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). Dens README fortæller hvordan, og hvad de automatiske tjek ser på.

En typepakke kommer fra **Administrer → Typepakker… → Eksportér typer…**; en prisliste fra **Prislister…**, med eksportknappen for en liste på denne computer (en `.lapinprices`-fil, dens priser markeret vejledende).
