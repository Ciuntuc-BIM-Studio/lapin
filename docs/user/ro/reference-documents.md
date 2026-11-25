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

Formularul ține titlul, tipul, emitentul, numărul, data, valabilitatea, fazele, rolul și folderul, plus câmpurile suplimentare ale tipului. Fișierele se pot previzualiza (un PDF pagină cu pagină, o imagine direct), salva ca o copie, scoate din document sau completa. Un IFC are **Open as link**: se deschide în proiect ca legătură doar pentru citire, ca File → Link IFC….

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
