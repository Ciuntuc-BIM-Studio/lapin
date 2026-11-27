---
title: Module de extensie
order: 126.5
summary: Instalezi un modul de extensie din Lapin Extensions în aplicația desktop: ce poate face, licența și acordul de licență, repornirea, oprirea, actualizarea și dezinstalarea lui, și de ce Lapin poate refuza să încarce unul.
---

## Ce este un modul de extensie

Un **modul de extensie** adaugă unelte în Lapin însuși, nu doar conținut în bibliotecile tale: file și butoane în panglică, vederi, dialoguri și comenzi, cu codul din spatele lor. Un modul are o parte în motor (cod .NET compilat), o parte de interfață (un plug-in) sau amândouă. Pachetele de conținut (mobilier, tipuri, hașuri…) sunt doar citite de Lapin; un modul **rulează**.

De aceea un modul poate face pe calculatorul tău **tot ce poți face tu**: să-ți citească și să-ți schimbe fișierele, să folosească rețeaua, să pornească programe. Lapin spune asta oriunde arată unul: **Rulează cod cu acces complet**. Din acest motiv modulele sunt tratate altfel decât restul catalogului:

- doar autorii pe care îi listează cel care administrează catalogul pot publica unul, iar **întreaga lui sursă** este citită de un verificator și aprobată înainte să apară în catalog;
- catalogul **reconstruiește pachetul din acea sursă** și îl listează doar dacă rezultatul este identic, octet cu octet, cu pachetul trimis;
- catalogul îl **semnează**, iar Lapin verifică semnătura și **fiecare fișier** al unui modul instalat la fiecare pornire.

Modulele rulează **doar în aplicația desktop**. Lapin în browser le arată în catalog, dar nu oferă **Instalează**.

## Înainte de instalare

Deschide **Fișier → Extensii…**, găsește modulul în **Răsfoiește** și dă clic pe el. Detaliile lui spun, pe lângă autor, licență și versiunile Lapin cu care merge:

- că *un modul de extensie rulează cod cu tot ce poți face pe acest calculator*, că Lapin Extensions i-a verificat sursa și l-a semnat, și că merge doar pe desktop;
- **Cere să**: ce face cu acest acces, așa cum îl declară autorul (să schimbe proiectul deschis, să ajungă la internet…);
- ce conține, când autorul enumeră.

Pagina lui din magazin, [store.lapinbim.com](https://store.lapinbim.com), spune același lucru mai pe larg, plus modulele Lapin de care are nevoie (o ediție fără ele nu-l poate încărca), commit-ul sursei din care e construit pachetul, textul licenței și **acordul de licență**. Ca să descarci fișierul de acolo, trebuie să accepți întâi acordul; nu ai nevoie de fișier, Lapin îl descarcă pentru tine.

## Instalare

1. Dă clic pe **Instalează**. Lapin descarcă pachetul și îi verifică mărimea, SHA-256-ul și semnătura catalogului; un fișier schimbat pe drum este refuzat și nu se instalează nimic.
2. Lapin arată **Rulează cod cu acces complet** împreună cu licența modulului: **OK** continuă, **Anulează** oprește.
3. Când modulul are un **acord de licență**, Lapin îl arată apoi, cu versiunea lui: acceptă-l ca să instalezi. Dacă anulezi oricare dintre ele, modulul nu se instalează (*Neinstalat: condițiile nu au fost acceptate.*).
4. **Instalate** îl arată acum cu *Se încarcă la repornirea Lapin.*, iar sus în listă scrie *Schimbările modulelor de extensie au efect la repornirea Lapin.* Dă clic pe **Repornește acum**: Lapin îți salvează lucrul (sau întreabă), se oprește și pornește din nou.

După repornire, uneltele modulului sunt unde le-a pus autorul (un grup al lui într-o filă a panglicii, un panou, un dialog). Lapin ține minte versiunea acordului pe care ai acceptat-o; o actualizare al cărei acord are o versiune nouă te întreabă din nou.

## Instalate: pornit, oprit, actualizare, dezinstalare

Fiecare modul din **Instalate** arată **Rulează cod cu acces complet**, un comutator **Pornit** și ce a făcut din el această pornire. Orice schimbare are efect la pornirea următoare, iar **Repornește acum** apare imediat ce una așteaptă:

- **Pornit** debifat: *Se oprește la repornirea Lapin.*, apoi *Oprit.* Modulul rămâne instalat și nu se încarcă. Îl pornești la loc la fel.
- **Actualizează**, când catalogul are o versiune mai nouă: versiunea nouă se descarcă și se verifică lângă cea care rulează (*Versiunea nouă se încarcă la repornirea Lapin:* cu numărul ei); cea veche se șterge la pornirea de după.
- **Dezinstalează**: *Se șterge la repornirea Lapin.* Fișierele lui se șterg la pornirea următoare, înainte să se încarce ceva.

Un proiect în care ai folosit un modul se deschide și fără el, într-o ediție care nu-l are sau după ce l-ai dezinstalat: Lapin păstrează în proiect ce a făcut modulul, așa cum este, și îl salvează neschimbat, deci nu se pierde nimic când modulul este instalat din nou.

## Când Lapin nu încarcă un modul

Lapin verifică fiecare modul instalat la fiecare pornire, iar unul care nu trece o verificare nu se încarcă: Lapin pornește fără el, iar **Instalate** spune *Neîncărcat:* cu motivul. Motivele obișnuite:

| Ce spune | Ce faci |
|---|---|
| un fișier al lui *a fost schimbat* sau *lipsește* | instalează-l din nou (dezinstalează, repornește, instalează) |
| *are nevoie* de un modul *pe care această ediție nu îl are* | folosește o ediție Lapin cu acel modul (Planificare și Costuri, pentru un modul de planificare) |
| a fost făcut pentru un Lapin mai nou | actualizează Lapin (Ajutor → Caută actualizări) |
| *Nu a putut fi încărcat* sau *Nu a putut porni*, cu o eroare | a fost construit pentru o versiune a motorului Lapin care s-a schimbat: așteaptă-i actualizarea sau scrie-i autorului |

Când partea lui de interfață nu poate rula (cere un Lapin mai nou sau se oprește cu o eroare la încărcare), Lapin pornește fără acea parte, spune *Extensii: interfețele lor nu au fost încărcate:* cu numele lor, iar **Instalate** arată *Interfața lui a fost lăsată deoparte:* cu motivul.

O versiune pe care Lapin Extensions o retrage după ce a publicat-o nu mai este oferită în catalog.

Vezi și [Extensii](extensions.md) pentru catalog, pachetele de conținut, scripturi și extensiile plătite.
