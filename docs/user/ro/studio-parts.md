---
title: Piese
order: 87
summary: Editează pe loc un solid Studio sau o piesă plasată cu Tab — restul modelului se estompează, iar arborele piesei arată originea, schițele, pașii și parametrii.
---

## Ce este o piesă

O **piesă** este un solid Studio cu o definiție în spate: un solid schițat cu pașii lui, sau un obiect propriu ale cărui corpuri urmează **parametri** (ceea ce era înainte o familie). Piesa se plasează de câte ori ai nevoie, iar fiecare obiect își păstrează propriile valori ale parametrilor.

**Familiile sunt acum piese.** Un proiect salvat cu familii se deschide cu aceleași obiecte ca piese, cu aceleași nume, corpuri, valori și identificatori, iar pagina Familii descrie în continuare cum sunt făcute. **Studio → Familii…** și uneltele ei funcționează ca până acum.

## Editarea unei piese

Selectează în 3D un solid Studio sau o piesă plasată, apoi fă una dintre acestea:

- apasă **Tab**;
- dă **dublu clic** pe piesă în 3D;
- apasă **Editează piesa** în **Studio → Piesă**, sau în secțiunea **Piesă** din Proprietăți.

Piesa intră atunci în **modul de editare**. Restul modelului se estompează și nu poate fi selectat, dar rămâne acolo ca să te prinzi de el. Piesa se desenează ca de obicei, iar bara de deasupra vederii spune ce editezi.

![Masa în model, selectată](shots/studio-part-model.webp)

![Masa în modul de editare: modelul estompat, planul din față al originii afișat, arborele piesei în stânga](shots/studio-part-edit.webp)

Ca să ieși, apasă din nou **Tab** sau **Esc**, ori **Termină piesa** în arbore. Ieșirea nu este un pas de anulare; fiecare modificare făcută înăuntru este un pas de anulare, ca peste tot în Lapin.

Tab nu face nimic special cât timp un câmp de text are focusul, cât timp e deschis un dialog sau cât timp rulează o unealtă: atunci strânge panourile ca înainte.

## Arborele piesei

Panoul din stânga vederii arată din ce e făcută piesa și se citește din nou după fiecare modificare.

![Arborele piesei: origine, schițe, pași și parametri](shots/studio-part-tree.webp)

- **Origine** are cele trei planuri ale piesei: sus (xy), față (xz) și dreapta (yz). Du cursorul pe unul ca să-l vezi în 3D, sau dă clic pe el ca să rămână afișat; încă un clic îl ascunde.
- **Schițe** listează schițele piesei. O schiță a unui solid (extrudare, baleiere, revoluție, loft) se deschide pe vederea ei, ca la **Editează schița**; fiindcă schița trăiește pe vederea ei, modul de editare se încheie când o deschizi. O piesă făcută din corpuri se deschide în editorul ei, cu piesa aleasă.
- **Pași** listează din ce e făcută piesa, în ordine: operația ei sau corpurile, apoi pașii făcuți asupra ei (împinge/trage, racordare, golire, împărțire…). Un pas oprit apare cenușiu; un corp făcut doar când se îndeplinește o condiție o arată (*oprit când nu HasShelf*). La pașii făcuți asupra piesei, butoanele pornesc sau opresc pasul, îl mută mai devreme sau mai târziu ori îl scot. Un clic pe numele unui pas arată piesa în Proprietăți, unde se editează numerele lui.
- **Parametri** listează fiecare parametru cu valoarea, unitatea, valoarea implicită și dacă ține de fiecare obiect (*instanță*) și este *publicat*. Scrie o valoare nouă și apasă Enter: obiectul piesei o urmează. Un parametru diferit de cel implicit apare îngroșat. Marcajele se pot doar citi deocamdată; devin editabile când parametrii primesc formule.

## Mai departe

Pagina Studio spune cum modelezi solide, iar pagina Familii cum fac parametrii și formulele o piesă.

Schițele unei piese pot fi ținute cu **constrângeri** și cote: vezi [Constrângerile schiței](sketch-constraints.md).
