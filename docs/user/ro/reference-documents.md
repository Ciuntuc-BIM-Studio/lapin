---
title: Documente de referință
order: 127
summary: Ține într-un singur registru documentele pe care le primește proiectul (certificatul de urbanism, avize și acorduri, extrase de carte funciară, studii, modelele unui colaborator), cu folderul, numărul, datele și valabilitatea lor, și lista de avize a certificatului.
---

Un proiect **primește** documente, nu doar le emite: certificatul de urbanism, avizele și acordurile cerute, extrasul de carte funciară, ridicarea topografică, studiul geotehnic, IFC-ul unui colaborator. **Manage → Reference documents…** le ține într-un registru din proiect, lângă emiteri. Nu e un container al fluxului ISO 19650: un document primit nu trece din WIP în Published. Are propria stare.

Registrul merge în aplicația desktop și în browser, cu server sau fără. Documentele și fișierele lor se păstrează în fișierul proiectului.

![Folderul de avize, cu insigna avizului](shots/reference-documents.webp)

## Fereastra

În stânga, **arborele de foldere**, cu un număr pe fiecare. Ultimele două, *Piese scrise emise* și *Piese desenate emise*, sunt estompate: arată ce a emis proiectul și nu se trage nimic acolo. La mijloc, **lista** (tip, titlu, emitent, număr, dată, valabil până la, stare), pe care o filtrezi după text, tip, fază, stare sau *Expiră în N zile*; un clic pe capul unei coloane sortează. În dreapta, documentul ales.

## Adăugarea documentelor

**Trage fișiere oriunde în fereastră** sau folosește **Add files…**. Fiecare fișier devine un document, în folderul pe care îl privești (sau în folderul tipului lui). Un fișier pe care îl ține deja alt document se adaugă oricum, iar fereastra spune asta. Un fișier peste limita de mărime nu se păstrează în fișierul proiectului (doar amprenta lui): fereastra spune care.

**New request** face un document pe care l-ai cerut și nu l-ai primit încă, fără fișier. Când vine fișierul, îl atașezi și documentul devine *Primit*.

Trage un document (sau mai multe, cu Ctrl sau Cmd apăsat) peste un folder ca să-l muți. Când proiectul are un folder (Manage → Project folder), fișierul fiecărui document se copiază și în `Reference/` de acolo, în aceleași foldere, și se mută odată cu documentul.

## Documentul

Formularul ține titlul, tipul, emitentul, numărul, data, valabilitatea, fazele, rolul și folderul, plus câmpurile suplimentare ale tipului. Fișierele se pot previzualiza (un PDF pagină cu pagină, o imagine direct), salva ca o copie, scoate din document sau completa. Un IFC (sau un .ifczip) are **Open as link**: se deschide în proiect ca legătură doar pentru citire, ca File → Link IFC…. Un fișier pe care proiectul nu-l ține (unul peste limita de mărime, pe un server) se aduce întâi de pe server.

**Suggest** citește textul fișierului și propune numărul, data, emitentul, tipul și valabilitatea, fiecare cu cuvintele din care a fost citit. Bifezi ce e corect și aplici; nimic nu se scrie fără tine. Un scan nu are text și nu primește sugestii. O valabilitate care vine doar din perioada obișnuită a tipului e marcată *orientativ*: contează datele scrise pe document.

![Suggest, cu cuvintele din care a fost citită fiecare valoare](shots/reference-suggest.webp)

Nimic nu se șterge direct. **Superseded by…** leagă un document de cel mai nou; **Withdraw…** întreabă de ce și păstrează înregistrarea. **Delete…** e pentru un fișier adăugat din greșeală.

## Lista certificatului

Un document de tipul *Certificat de urbanism* are fila **Checklist**: avizele și studiile pe care le cere. Le bifezi din listă sau le scrii pe ale tale. Legi fiecare punct de documentul care îi răspunde sau apeși **Request** ca să faci un document cerut și să-l legi. Fiecare punct e *lipsă*, *cerut*, *primit* sau *expirat*, iar rezumatul spune câte mai lipsesc.

![Lista: un aviz primit, unul cerut, unul lipsă](shots/reference-checklist.webp)

Lista livrată cu Lapin e orientativă. Normele și perioadele de valabilitate sunt ale tale: Lapin nu pretinde nicio perioadă legală.

## Valabilitatea

Un document primit, cu data *valabil până la* depășită, e **expirat**; unul care se termină în 30 de zile e *pe cale să expire*. Bara de stare le numără, iar un clic deschide fereastra filtrată după ele. Când nu sunt, nu se arată nimic.

![Contorul din bara de stare](shots/reference-expiring.webp)

## Foldere și tipuri

**Folders and types** editează copia proprie a proiectului a arborelui de foldere (redenumești, adaugi, ordonezi, scoți: un folder cu documente rămâne până le muți), limba în care folderele se numesc pe disc, lista tipurilor de document cu folderul, valabilitatea obișnuită și câmpurile lor suplimentare, și mărimea peste care un fișier nu se păstrează în fișierul proiectului. Un proiect făcut într-o interfață în română începe cu folderele numite în română.

## Scan folder

Pe desktop, când proiectul are un folder, **Scan folder** listează fișierele din `Reference/` pe care registrul nu le ține, cu folderul și tipul sugerate de cale, ca să le adaugi. Listează și copiile registrului care lipsesc de acolo sau stau în alt folder. Registrul e adevărul: un fișier dispărut de pe disc e un avertisment, niciodată o ștergere.

## În documentele scrise

Documentele citesc registrul (vezi [Documente](documents.md#documentele-de-referinta-intr-un-document)):

- **Câmpurile** `{{ref.<tip>.number}}`, `.date`, `.issuer`, `.validUntil` și `.title` dau cel mai nou document primit al unui tip care e încă valabil (după data lui): `{{ref.cu.number}}` e numărul certificatului de urbanism. Documentele expirate, înlocuite, retrase sau cerute nu sunt citite niciodată. `{{ref.<tip>.count}}` spune câte documente valabile sunt. Datele se scriu în limba documentului.
- **Repetă pentru** *Documente de referință* (`{{reference.title}}`, `.number`, `.issuer`, `.date`, `.status`, `.folderPath`, `.pages`, câmpurile suplimentare ale tipului…) și pentru *lista certificatului de urbanism* (`{{check.label}}`, `.stateText` și numărul, data și emitentul documentului care îi răspunde).
- **Tabelele**: *Avize și acorduri* (lista certificatului cu documentele care îi răspund) și *Documente de referință* (filtrat după tip, folder și stare).

Certificatul citit e cel mai nou certificat primit și încă valabil.

![Borderoul de avize și acorduri, completat din registru](shots/documents-permits.webp)

## Trimiterea lor cu o emitere

**Gestionare → Emite…** are **Include documentele de referință**. Bifează documentele unul câte unul sau un folder, ca să bifezi documentele din el și din subfolderele lui (un document cerut nu are fișier și nu poate pleca; un folder nu ia documentele înlocuite sau retrase). Fișierele lor pleacă odată cu emiterea, copii sub numele lor (un al doilea fișier cu același nume primește un număr), trecute în borderoul de transmitere (tipul `REF`, cu numărul și titlul documentului) și în manifest, cu SHA-256. Pe un server CDE sunt fișiere ale emiterii, nu documente ale registrului.

![Emite: certificatul și folderul de avize bifate](shots/issue-references.webp)

Un fișier peste limita de mărime nu e în fișierul proiectului. E adus întâi de pe serverul proiectului. Fără server, se folosește copia din folderul proiectului sau fișierul din care a fost adăugat. Când nu e găsit nicăieri, e lăsat în afara emiterii, iar Lapin spune care.

## Cu un server

Într-un proiect ținut pe un server CDE, documentele sunt păstrate și acolo, iar **serverul e adevărul**. **Sincronizează problemele și documentele** (Fișier → Colaborare → Colaborează…) aduce documentele serverului în proiect și le trimite pe ale tale; **Salvează pe server** și **Ia ultima versiune** fac același lucru odată cu problemele. Documentele se potrivesc după id și se îmbină câmp cu câmp: când doi oameni au schimbat același câmp, rămâne valoarea ultimului care a scris, iar Lapin spune câte modificări au întâlnit modificările altcuiva. Ce au schimbat alții se spune pe nume.

- Un fișier peste limita de mărime rămâne pe server, nu în fișierul proiectului. Se aduce când îl deschizi, îl previzualizezi sau salvezi o copie.
- Ștergerea ține de roluri: cititorii citesc, editorii adaugă și schimbă documentele pe care le-au adăugat, proprietarii schimbă orice document, șterg unul și editează folderele și tipurile. O schimbare pe care serverul o refuză (rolul tău nu o permite) revine cum era, iar Lapin spune de ce.
- Același registru e pe pagina proiectului din browser, în fila **Documente**, fără să deschizi proiectul: vezi [Server CDE](cde-server.md). Acolo IFC-ul unui consultant se deschide în 3D-ul din browser doar pentru citire, singur sau legat în ultimul commit al proiectului, cu raportul IDS pe care l-a făcut serverul.
