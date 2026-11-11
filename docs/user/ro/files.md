---
title: Fișiere, versiuni și recuperare
order: 120
summary: Salvezi și deschizi proiecte, pornești proiecte noi din șabloanele tale, recuperezi lucrul nesalvat după o cădere și păstrezi versiuni în fișierul proiectului.
---

<!-- video files -->

## Salvare și deschidere

**Fișier → Salvează** (<kbd>Ctrl</kbd> <kbd>S</kbd>) scrie un fișier `.lapin`, iar **Fișier → Deschide…** (<kbd>Ctrl</kbd> <kbd>O</kbd>) deschide unul.

- **Aplicația desktop.** Fișierul se salvează unde alegi.
- **În browser, Chrome și Edge.** Lapin salvează de fiecare dată în același fișier.
- **În browser, Safari și Firefox.** Fiecare salvare descarcă fișierul.

Un fișier `.lapin` ține tot proiectul: modelul, vederile, planșele, materialele cu imaginile lor proprii și versiunile.

## Șabloane de proiect

Un șablon e configurarea unui proiect fără modelul lui, ca tipurile și setările pe care le faci o dată să pornească orice proiect nou. **Fișier → Salvează ca șablon…** păstrează proiectul deschis sub un nume: tipurile de pereți, planșee, uși, ferestre, scări, tavane și structură, materialele și profilele, unitățile și standardele de desen (stiluri de text, de cote și de etichete, straturi), stilurile de vedere, etajele, vederile, planșele și cartușele, iar din informațiile proiectului ce ține de birou (proiectantul, cine semnează). Nu păstrează niciun element (niciun perete, niciun desen, nicio încăpere), nicio problemă sau observație, niciun deviz sau grafic și nici informațiile proprii lucrării (numărul, beneficiarul, amplasamentul și autorizația).

**Fișier → Nou din șablon…** (și pe ecranul de start) arată șabloanele, primul fiind **Implicit Lapin**. **Proiect nou** pe un rând pornește un proiect din el: nu are încă fișier și are GlobalId-uri proprii. Alege un șablon în stânga rândului său sau în **Fișier → Preferințe… → General → Proiecte noi**, iar **Fișier → Proiect nou** pornește de fiecare dată din el; **Implicit Lapin** revine la pornirea proprie a lui Lapin.

![Șabloane de proiect: implicitul Lapin și ale tale](shots/project-templates.webp)

- **Aplicația desktop.** Șabloanele sunt fișiere `.lapin` în dosarul tău Lapin, păstrate împreună cu preferințele tale.
- **În browser.** Șabloanele se păstrează în browser: exportă-le ca să ai un fișier.

**Exportă** salvează un șablon ca fișier `.lapin`, de copiat pe alt calculator sau de împărțit cu un birou, iar **Importă…** îl aduce înapoi. Merge și fișierul `.lapin` al oricărui proiect: modelul lui e lăsat deoparte. **Nou dintr-un fișier…** pornește un proiect dintr-un astfel de fișier fără să-l păstreze.

## Recuperarea lucrului nesalvat

Lapin îți păstrează lucrul nesalvat pe măsură ce lucrezi. Dacă aplicația sau calculatorul se oprește înainte să salvezi, ecranul de start îți oferă data viitoare să-l recuperezi.

## Versiuni

O versiune e proiectul așa cum e în clipa în care îl salvezi ca versiune: înaintea unei întâlniri cu beneficiarul sau la sfârșitul unei faze de proiectare. Deschide **Fișier → Salvează o versiune…** sau **Gestionare → Versiuni**.

![Versiuni: salvezi proiectul așa cum e acum](shots/versions.webp)

Versiunile stau în fișierul proiectului, iar părțile pe care o versiune le are în comun cu proiectul se păstrează o singură dată. Din listă poți:

- redenumi o versiune;
- o aduce la starea curentă, cu o revizie;
- o compara cu proiectul, element cu element;
- o restaura, ca un singur pas pe care îl poți anula;
- o exporta sau deschide separat;
- o șterge.

## Anulare

<kbd>Ctrl</kbd> <kbd>Z</kbd> anulează, iar <kbd>Ctrl</kbd> <kbd>Y</kbd> sau <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>Z</kbd> reface. Butoanele **Anulează** și **Refă** din bara de sus, în dreapta lui **Comenzi…**, fac același lucru în orice mod al interfeței, iar indicația lor spune ce anulează; la fel **Fișier → Anulează** și **Fișier → Refă**, și paleta de comenzi (<kbd>Ctrl</kbd> <kbd>K</kbd>, scrie „anulează”).
