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

En visuel editor til dine egne skabeloner kommer senere.
