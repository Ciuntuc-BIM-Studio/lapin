---
title: Import și export
order: 110
summary: Schimbi modelul ca IFC, aduci desene DXF, legi fișiere și exporți modele 3D pentru randare și AR.
---

Tot ce intră sau iese e în meniul **Fișier**.

![Meniul Fișier](shots/file-menu.webp)

## IFC

**Exportă IFC…** scrie modelul ca IFC. Fiecare perete, placă, ușă și acoperiș e un element IFC adevărat, cu straturile, tipul și proprietățile lui, așa că fișierul se deschide ca model în alte programe BIM. Ușile și ferestrele spun cum se deschid: mâna fiecărei foi și cercevele, în ce parte se deschid și panourile, cum le definește IFC, așa că alte programe desenează aceleași deschideri.

Exportul întreabă schema (**IFC4**, citit peste tot, sau **IFC4X3**), detaliul (piesele ca elemente de sine stătătoare sau un corp pe element), faza și cum se scriu corpurile. **Geometrie** e **B-rep fațetat** (exact, ce așteaptă cel mai des alte programe) sau **seturi de fețe**: aceleași fețe, într-un fișier mult mai mic. **Simplifică** scrie mai puține triunghiuri unde un corp e o rețea fină, cum sunt tubulaturile și țevile din fișierul altui program, niciodată mai departe de el decât **toleranța** pe care o scrii (2 mm la început). Corpurile pe care le construiește Lapin (pereți, plăci, uși) își păstrează fețele exacte, iar simplificarea schimbă doar forma corpului, niciodată proprietățile lui.

![Exportă IFC](shots/export-ifc.webp)

**Importă IFC…** citește IFC-ul altui program. Înainte să intre ceva, Lapin citește fișierul și îți arată ce conține: elementele pe discipline (arhitectură, structură, instalații, mobilier și echipamente, sit), fiecare disciplină pe tipuri, cu câte sunt din fiecare, și etajele, cu câte elemente stau pe fiecare. Debifează ce nu-ți trebuie, de exemplu instalațiile și mobilierul când lucrezi la structură, sau păstrează doar etajele la care lucrezi. **Cum** aduce elementele recunoscute ca elemente Lapin, pe toate ca referințe sau legate. Cât timp fișierul se citește și se importă, Lapin arată ce face și cât a mai rămas; un fișier mare durează.

![Import IFC](shots/import-ifc.webp)

Unele fișiere au și 2D pe lângă model: simboluri de plan, amprente, hașuri și texte desenate pentru un element, sau elemente desenate doar în 2D (linii, texte, simboluri fără corp). Când un fișier le are, dialogul le numără la **Desenat în 2D**. Debifează **2D-ul propriu al elementelor** și planul desenează acele elemente prin secțiunea lor, ca pe ale lui Lapin; debifează **elementele desenate doar în 2D** și ele rămân afară. Ambele intră dacă nu le debifezi.

![Desenat în 2D](shots/import-ifc-2d.webp)

**Curățare și optimizare** ține regulile importului, aceleași pentru orice fișier, păstrate ca ale tale pentru importul următor. **Păstrează o singură dată un corp repetat** (bifat, dacă nu-l debifezi) păstrează o dată un corp pe care fișierul îl repetă sub alte nume (un șurub, o flanșă, un suport), elementele lui împărțindu-l, fără nicio pierdere. Celelalte lasă afară, când le bifezi, ce a adus alt program cu el: **corpurile plate** (texte, linii și hașuri făcute triunghiuri), **elementele mai departe de rest** decât distanța pe care o scrii (textele unui desen, un cartuș), **duplicatele** din același loc și **porturile și golurile scrise ca obiecte**. **Simplifică rețelele** scrie mai puține triunghiuri în limita toleranței pe care o scrii; **citește numerele scrise ca text** face din `DoubleLength:0,01` 0,01 m și din `1.15395 kg` o masă (un număr întreg singur, un cod, rămâne text). La **Ce se aduce**, **Seturi de proprietăți** arată seturile fișierului, cu câte elemente are fiecare; cele **tehnice** (transformarea din Navisworks, id-uri interne, statistici de geometrie) rămân afară dacă nu le bifezi. Mesajul importului spune ce a făcut fiecare regulă.

![Curățare și optimizare](shots/import-ifc-rules.webp)

Lapin recunoaște pereții, plăcile, stâlpii, grinzile, fundațiile, ușile și ferestrele și le face elemente Lapin pe care le poți edita. Ce nu poate recunoaște intră ca referință, desenată, dar needitabilă. Ușile și ferestrele intră deschizându-se cum spune fișierul: mâna, partea în care se deschid și, la ferestre, cum se deschide fiecare panou.

Spațiile fișierului (din Revit, ArchiCAD și altele) intră ca încăperi Lapin, fiecare pe nivelul ei, cu conturul, numărul, numele, înălțimea, funcțiunea, departamentul și finisajul pardoselii. Tot ce avea în fișier un element importat (seturile de proprietăți și cantitățile) se păstrează și apare la **Proprietăți IFC** în proprietățile lui. Încăperile se văd în planurile nivelurilor lor (eticheta; o schemă de culori din **Vedere → Culorile încăperilor** le umple); în 3D sunt ascunse până le arată **Vizibilitate / Grafică**. O referință iese din **Exportă IFC…** cum a intrat: cu același GlobalId, aceeași clasă și aceleași atribute (descriere, tag, tip predefinit), cu seturile de proprietăți și cantitățile, fiecare valoare cu tipul din fișier, și cu tipul, materialul, porturile, conexiunile și sistemul ei, fiecare cu GlobalId-ul lui. Dacă aduci doar instalațiile dintr-un fișier și le exporți, fișierul nou le are întregi.

## DXF și DWG

**Importă DXF…** aduce un desen CAD în vederea din față, cu layerele, hașurile, textele și cotele lui. Intră ca desen de care te poți prinde și pe care îl poți edita. Ca să folosești un DWG, salvează-l întâi ca DXF din programul tău CAD (AutoCAD, BricsCAD sau DraftSight: **Save As → DXF**). Importul de PDF nu există încă. Într-un plan, **Așezat: după coordonatele hărții** pune un desen în coordonate reale (un plan cadastral în Stereo 70) unde îl pune harta (vezi [Amplasamentul și datele lui](site.md)).

## Legături

**Leagă IFC…** ține alt fișier legat în loc să-l importe. E doar pentru citire și se reîncarcă atunci când se schimbă. Merge și pentru un DXF. **Gestionare → Legături** arată, reîncarcă, convertește sau scoate legăturile.

## Modele 3D

**Exportă 3D…** scrie modelul ca suprafețe pentru randare, vizualizare și realitate augmentată (AR):

| Format | Pentru |
|---|---|
| GLB, glTF | Blender, three.js, vizualizatoare web. Imaginile proprii ale materialelor sunt incluse. |
| USDZ | AR Quick Look de la Apple, Reality Composer, Omniverse. Imagini incluse. |
| USDA | OpenUSD ca text, un singur fișier. |
| DAE | COLLADA, pentru SketchUp și programe mai vechi. |
| Fragments | `.frag`, formatul That Open Engine, pentru vizualizatoarele BIM web construite pe el (That Open Components, Bubbles). Un singur fișier; corpurile identice o singură dată. |

**Importă Fragments…** aduce un fișier `.frag`: fiecare element cu geometrie devine o referință desenată din corpul lui, în clasa pe care o spune fișierul, pe nivelul pe care îl pune fișierul (potrivit după nume cu ale tale, creat când lipsește), cu atributele fișierului la **Proprietăți IFC**. Elementele cu același corp (aceeași formă, așezată în mai multe locuri) îl împart. Importat din nou, același fișier actualizează ce a adus înainte. Nimic nu e recunoscut ca perete sau placă: un `.frag` ține triunghiuri.

**Importă rețea (OBJ, STL, glTF)…** aduce o rețea dintr-un alt program 3D (OBJ, STL, glTF sau GLB) ca referințe pe nivelul activ. glTF își spune unitatea și care axă e în sus; pentru OBJ și STL, Lapin întreabă. **Fă solid Studio** din Studio face o referință editabilă (vezi [Studio](studio.md)).

![Export 3D](shots/export-3d.webp)

Alegi ce exporți, ce fază și originea. **Ce** este **tot**, sau modelul **cum îl arată vederea**: vederea 3D din față, fără elementele și categoriile ei ascunse și fără etajele pe care nu le arată, cu straturile pe care le arată (fără finisaje sau doar miezurile). Când vederea e tăiată (tăietura de deasupra etajelor afișate, o secțiune arătată în 3D, cutia de secțiune), **Tăietură** taie și exportul acolo și închide fiecare tăietură în materialele ei, ca fișierul să arate ca ecranul. O vedere 3D care lasă ceva deoparte se exportă cum o arată, dacă nu alegi altfel. Fiecare element își păstrează clasa IFC, tipul și GlobalId-ul. Ca să schimbi modelul în sine, nu suprafețele lui, folosește **Exportă IFC**.
