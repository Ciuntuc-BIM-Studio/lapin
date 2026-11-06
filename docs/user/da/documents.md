---
title: Dokumenter
order: 105
summary: Projektets skriftlige dele — den tekniske beskrivelse, arbejdsbeskrivelsen, tegningslisten — lavet ud fra skabeloner, udfyldt fra modellen og eksporteret til DOCX, ODT og PDF uden andre programmer.
---

## Hvad et dokument er

Et projekt er mere end sine tegninger: Det har også sine skriftlige dele, en teknisk beskrivelse, en arbejdsbeskrivelse, en tegningsliste. I Lapin er de **dokumenter**: Hvert starter fra en skabelon og udfyldes fra projektet — dets oplysninger, arealer, etager og rum, vægtyper, lister og tegningsark. Det, kun du ved (konstruktionen, brandsikringen, byggepladsindretningen), skriver du i dokumentets input. Et dokument følger modellen: Ændr en vægtype eller tilføj et rum, og dokumentet siger det, næste gang du åbner det.

Lapin skriver selv filerne, i DOCX, ODT og PDF, uden Word eller LibreOffice på computeren, i skrivebordsappen og i browseren.

## Projektoplysninger

**Administrer → Projektoplysninger…** rummer det, dokumenterne beder projektet om: navn, nummer og fase, bygherren og dennes adresse, byggepladsens adresse, matrikelnummer og tingbog, den projekterende og dennes adresse, den ledende arkitekt, hvem der tegnede, og hvem der kontrollerede. Skriv dem én gang: Alle dokumenter læser dem, og tegningsarkenes tegningshoveder tager herfra, hvem der tegnede og kontrollerede, når et ark ikke selv siger det. Hvert felt gemmes, når du forlader det.

![Projektoplysninger: nummer, fase, bygherre, byggeplads og projekterende](shots/project-info.webp)

De samme felter findes på fanen **Projektoplysninger** i dialogen Dokumenter.

Fordi et tegningshoved spørger om mere end et dokument, er felterne grupperet: projektet, bygherren, grunden og dens byggetilladelse, den projekterende (firma, adresse, registreringsnummer, telefon, e-mail) og dem, der skriver under (projektleder, chefarkitekt, konstruktions- og installationsingeniør, tegnet, kontrolleret og godkendt af). Hvad der stadig mangler, kommer under **Projektets egne parametre**: en entydig nøgle, en etiket, en type (tekst, tal eller dato), en gruppe og en værdi. Et tegningshoved læser den som `{project.nøgle}`, et dokument som `{{project.nøgle}}`, og IFC eksporterer det hele med projektet i egenskabssættet `Lapin_ProjectInformation` (og læser det tilbage).

![Projektets egne parametre: nøgle, etiket, type, gruppe og værdi](shots/project-info-parameters.webp)

## Lav et dokument

**Administrer → Dokumenter…** (eller **+** ved **Dokumenter** i projektbrowseren) åbner projektets dokumenter. Til venstre er de dokumenter, der allerede er lavet, og under **Ny fra skabelon** skabelonerne; klik på en, og det nye dokument laves og udfyldes med det samme.

![Dokumenter: projektets dokumenter og skabelonerne, dokumentet på sine sider, dets input og det, der mangler](shots/documents.webp)

I midten er dokumentet, som det bliver udskrevet, side efter side, med indholdsfortegnelse, tabeller og tal. Et felt, projektet endnu ikke har, er markeret, hvor det står. Projektbrowseren viser dokumenterne under **Dokumenter**; et klik åbner dialogen på et af dem.

## Input

Fanen **Input** har de tekster, kun den projekterende kan skrive, grupperet som dokumentets kapitler. I de rumænske skabeloner starter hver med en vejledning markeret **[De completat]** (udfyldes), som siger, hvad der skal stå. Skriv oven i den: Forhåndsvisningen følger et øjeblik efter, at du holder op med at skrive. **Nulstil til standard** henter skabelonens tekst tilbage.

![Input: konstruktionen skrevet oven i skabelonens vejledning](shots/documents-inputs.webp)

## Mangler

**Mangler** viser, hvad dokumentet stadig mangler: et tomt projektfelt, en værdi, modellen ikke har. Klik på en for at se, hvor den står i dokumentet, og gå til feltet, der udfylder den (et projektfelt åbner fanen **Projektoplysninger**). Listen tømmes, efterhånden som du udfylder.

![Mangler: de felter, dokumentet stadig mangler](shots/documents-missing.webp)

## Opdatér, omdøb, kopiér

- **Opdatér fra skabelon** henter skabelonens tekst og opsætning igen, til et dokument lavet før skabelonen blev ændret. Dine input beholdes.
- **Omdøb**, **Duplikér** og **Slet** virker på det viste dokument. En sletning er ét fortryd-trin.

## Redigér et dokument

**Redigér**, nederst i dialogen Dokumenter, gør siden i midten til en editor for dokumentets egen tekst; **Forhåndsvisning** viser det igen på dets sider. Det, du ændrer, hører kun til dette dokument: Skabelonen forbliver, som den var (**Gem som skabelon…**, længere nede, gemmer dine ændringer som skabelon).

![Redigér et dokument: felterne som mærker med deres værdier, en tabel fra projektet valgt og dens indstillinger til højre](shots/documents-editor.webp)

- **Værktøjslinjen** har tekstens typografier: **Afsnit**, **Overskrift 1**–**3**, fed, kursiv, punktopstilling og nummereret liste, fortryd og gentag. De sædvanlige taster virker også (Ctrl+B, Ctrl+I, Ctrl+Z i editoren).
- **Felter** vises som mærker med deres navn og deres nuværende værdi, for eksempel bygherren, *Popescu Ion și Maria*. Et felt, som projektet endnu ikke udfylder, er orange.
- **Projektets blokke** — en tabel, et billede, indholdsfortegnelsen — tegnes med deres rigtige indhold. Klik på en for at vælge den: Dens indstillinger vises til højre i stedet for fanerne.

Der gemmes automatisk: et øjeblik efter du holder op med at skrive, og når du går tilbage til **Forhåndsvisning**; hver ændring er et fortryd-trin i projektet.

### Indsæt

**Indsæt** sætter ind ved markøren:

- **Felt…**: et felt fra projektet, søgt efter navn; listen viser hvert felts værdi. Inde i en gentagelse tilbydes også dens elementers felter (en etages navn, et rums areal).

  ![Indsæt → Felt…: „client“ søgt, felterne med deres værdier](shots/documents-insert-field.webp)

- **Tabel fra projektet…**: en liste fra projektet (dør- og vinduesliste, rummene, materialerne…). I dens indstillinger: **Kilde**, **Titel**, de viste kolonner og totalrækken.
- **Billede af en visning…**: en plan, et snit, en facade eller en 3D-visning. I dets indstillinger: **Målestok 1:** eller **Bredde (mm)** (aldrig bredere end teksten) og en **Billedtekst**.
- **Nøgle–værdi-felter**: en tabel med to kolonner af etiketter og værdier; en værdi kan indeholde felter, som `{{project.name}}`.
- **Gentag for…**: Teksten indeni skrives én gang for hvert element i en samling: etagerne, rummene, tagene, materialerne, tegningsarkene, dokumenterne… Et bånd over den siger **For hver** og hvor mange elementer der er; dets pile **‹ ›** skifter det element, editoren viser, mens du skriver. I indstillingerne beholder **Kun de elementer, hvor** nogle af dem, og **Tekst når der ingen er** tilføjer det, der skrives, når der ingen er.
- **Betingelse…**: Teksten indeni skrives kun, når et felt har en værdi (eller, afkrydset, kun når det er tomt). **Ellers: tekst når den ikke gælder** tilføjer en anden del, der skrives i stedet.
- **Tekst at udfylde…**: et nyt input med etiket, gruppe og standardtekst; dets tekst skriver du i fanen **Input**, i forhåndsvisning.
- **Indhold** og **Sideskift**.

![En gentagelse over etagerne, en betingelse i den og rummene på den viste etage, skiftet med ‹ ›](shots/documents-repeat.webp)

I en bloks indstillinger fjerner **Fjern** den; for en gentagelse eller en betingelse fjerner **Behold kun indholdet** båndet og beholder det, der står i det.

## Dine skabeloner

**Gem som skabelon…** gemmer dokumentets tekst, layout, input og stil som en skabelon. Giv den en titel og en beskrivelse, og vælg, hvor den skal gemmes:

- **Dette projekt**: Den følger med projektfilen til alle, der åbner den.
- **Denne computers bibliotek**: Alle projekter på denne computer tilbyder den.
- **Begge**.

**Gem som** kan erstatte en skabelon, du har gemt før; dokumenter, der allerede er lavet ud fra den, beholder deres tekst indtil **Opdatér fra skabelon**. Den nye skabelon står straks under **Ny fra skabelon**.

![Gem som skabelon: titlen, beskrivelsen og hvor den gemmes](shots/documents-save-template.webp)

**Administrer → Dokumentskabeloner…** viser skabelonerne i tre grupper: **Indbyggede** (Lapins), **Dette projekt** og **Denne computer**. For den valgte:

- **Omdøb** og **Slet** (at slette en projektskabelon er ét fortryd-trin; dokumenter lavet ud fra den beholder deres tekst).
- **Dupliker ind i projektet** eller **Dupliker ind i biblioteket**: En indbygget skabelon ændres kun gennem en kopi.
- **Kopiér til biblioteket** eller **Kopiér til projektet** for at flytte den fra det ene sted til det andet.
- **Eksportér…** skriver skabelonen som en `.json`-fil; **Importér…** læser en ind i dette projekt eller i denne computers bibliotek. Sådan deler kolleger et firmas skabeloner.

![Dokumentskabeloner: de indbyggede, projektets og computerens, den valgte med sin stil](shots/document-templates.webp)

## Firmaets stil

Et dokument, og en skabelon i projektet eller biblioteket, har sin egen stil: fanen **Stil** i dialogen Dokumenter for det viste dokument, delen **Stil** i Administrer → Dokumentskabeloner… for en skabelon. Indbyggede skabeloner beholder Lapins udseende: Dupliker en for at give den dit.

- **Skrifttype (DOCX, ODT)**: Arial (Lapins), Calibri, Times New Roman, Georgia, Verdana eller Liberation Sans.
- **PDF-skrifttype**: Helvetica eller Times, PDF'ens egne skrifttyper; som standard den, der ligger nærmest skrifttypen ovenfor.
- **Tekststørrelse (pt)**: brødtekstens størrelse, fra 6 til 24 pt.
- **Overskrifternes farve**.
- **Logo i sidehovedet**: **Vælg logo…** tager en PNG eller JPEG (under 1 MB); sæt dens **Bredde (mm)** og om den står **Til venstre** eller **Til højre**. Den skrives ens i DOCX, ODT og PDF.

En prøve under felterne viser sidens top, som filerne vil vise den. **Lapins eget udseende** fjerner stilen. Hver ændring er ét fortryd-trin.

![Fanen Stil: overskrifternes farve og et logo i sidehovedet, med prøven](shots/documents-style.webp)

## Eksport

- **Eksportér DOCX** og **Eksportér ODT** skriver en fil, du kan åbne og redigere i Word eller LibreOffice, med overskrifterne som programmets egne typografier, indholdsfortegnelse og sidetal som felter og projektets billeder.
- **Eksportér PDF** skriver dokumentet sat op af Lapin, klar til udskrift eller afsendelse.

Et felt, der stadig er tomt, står tomt i filerne, så kig på **Mangler**, før du eksporterer.

## Udgiv dokumenter

**Administrer → Udgiv…** tager dokumenterne med sammen med tegningsarkene og modellen (se [Projektmappe og udgivelser](cde.md)). Under **Dokumenter, hvert som PDF:** er hvert dokument valgt fra start; sæt flueben ved **Også som DOCX** for også at få en redigerbar kopi. Hver fil navngives efter ISO 19650-konventionen (`RP` for en beskrivelse, `SP` for en arbejdsbeskrivelse), får sin hash og står i følgesedlen, og dokumentet får udgivelsens revision og status.

![Udgiv: dokumenterne sammen med tegningsarkene og modellen](shots/issue-documents.webp)

## Skabelonerne

Lapin har tre skabeloner, foreløbig på rumænsk:

- **Memoriu tehnic de arhitectură** (den arkitektoniske tekniske beskrivelse, efter den rumænske lov 50/1991): de generelle oplysninger fra Projektoplysninger; etageantallet (for eksempel `P+1E`) og den største højde; arealerne — bebygget, bruttoetageareal, nytteareal og kælder — i alt og pr. etage; hver etages rum med deres arealer; væggene efter type med deres lag; dækkene og tagene; vindues- og dørlisten; rummenes overflader; input til den eksisterende grund, anvendelsen, konstruktionen, facaderne, installationerne, brandsikringen, tilgængeligheden, energieffektiviteten og byggepladsindretningen.
- **Caiet de sarcini: tâmplărie și finisaje** (arbejdsbeskrivelsen for vinduer, døre og overflader): projektets materialer efter kategori, vindues- og dørlisten med antal, overfladerne rum for rum og input til generelle bestemmelser, krav, udførelse og aflevering, med standardtekster at tilpasse.
- **Borderou de piese scrise și desenate** (listen over skriftlige og tegnede dele): projektets dokumenter og tegningsark med nummer, titel, målestok og revision.

Start fra en af dem, ændr den i editoren, og brug så **Gem som skabelon…** for at have din egen.

## Til AI-assistenter

En assistent, der er forbundet gennem Lapins MCP-bro, kan lave et dokument, skrive dets input og eksportere det med `lapin_document_export`: se [Lapin and AI agents](https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/mcp.md#documents) (på engelsk).
