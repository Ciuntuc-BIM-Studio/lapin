---
title: Materiale, texturi și hașuri
order: 90
summary: Setezi culoarea, hașurile și rosturile fiecărui material, pui pe el o imagine proprie și îți aduci propriile hașuri .pat.
---

**Gestionare → Materiale** listează materialele proiectului. Alege unul ca să-l modifici.

![Fereastra Materiale](shots/materials-dialog.webp)

## Aspect: cum se randează un material

Fila **Aspect** setează cum arată materialul în 3D și în programele de randare în care exporți. Fila **Grafică 2D** păstrează hașurile și penițele, ca înainte.

![Aspectul unui material, cu previzualizarea lui](shots/material-appearance.webp)

- **Culoare** și **Textură**: o textură desenată (cărămidă, scânduri de lemn, plăci…) sau imaginea ta. **W** și **H** spun cât de mare stă o placă pe model, în metri; **Rotirea** o întoarce.
- **Rugozitate**: 0 e o oglindă, 1 e cretă. **Metalicitate**: 1 pentru metal neacoperit (aluminiu eloxat, oțel, crom), 0 pentru tot ce e vopsit sau vopsit în câmp electrostatic, lemn și piatră.
- **Transparență**: cât se vede prin el. **Sticlă (transmisie)** și **IOR** (1,52 pentru sticlă) fac sticla să refracte într-un program de randare.
- **Strălucește** emite lumină, ca difuzorul unei lămpi.

Sfera arată rezultatul imediat. Ce nu setezi vine din nume: un material numit *sticlă* e transparent, *oțel* e metalic, *parchet* și *laminat* sunt scânduri de lemn. **Aspectul din nume** scoate din nou valorile tale.

## Din bibliotecă

**Din bibliotecă…** oferă materiale gata făcute: metale (aluminiu RAL 7016 antracit, eloxat, oțel, cupru…), sticlă (clară, albastră, verde, gri, mată, reflectorizantă), lemn în scânduri, beton, cărămidă, tencuială și vopsea, plăci și piatră, plastic și textile. Un clic adaugă unul în proiect; îl schimbi ca pe oricare altul.

![Biblioteca de materiale](shots/material-library.webp)

## Un singur material pentru toate tocurile și toată sticla

Ușile și ferestrele noi sunt făcute din materiale comune, parte cu parte: **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** și **Hardware**. Schimbi **Window frame** o dată și se schimbă tocul tuturor ferestrelor.

Ca să folosești alte materiale, deschide **Arhitectură → Uși și ferestre**, alege **Materialul tocului**, **Materialul sticlei** și celelalte pentru un tip și apasă **Folosește pentru toate ferestrele** (sau **Folosește pentru toate ușile**). Toate tipurile de acel fel le primesc, într-un singur pas de anulare. Pereții cortină au și ei un material de cadru și unul de sticlă.

![Materialele părților în configuratorul de uși și ferestre](shots/joinery-materials.webp)

## Duplică, înlocuiește, selectează

- **Duplică** copiază un material sub un nume nou.
- **Înlocuiește cu** un material, apoi **Înlocuiește**: fiecare strat, tip, ușă, fereastră și element care îl folosea pe primul îl folosește acum pe al doilea, într-un singur pas de anulare. **Înlocuiește și șterge** îl scoate și pe primul. Altfel, un material folosit nu se poate șterge.
- **Selectează elementele** selectează toate elementele care au o parte din acel material.
- **Caută materiale** și lista categoriilor filtrează lista.

## Materiale pentru randare

Twinmotion, Lumion, Enscape, Blender și alte programe de randare înlocuiesc materialele după nume. Lapin scrie fiecare material **o singură dată, sub numele lui**, cu culoarea, textura, rugozitatea, metalicitatea și sticla lui, și împarte plasa fiecărui element pe materiale.

1. Numește materialele cum le vrei în programul de randare, de exemplu *Aluminium RAL 7016 anthracite* pentru toate tocurile și *Glass blue tinted* pentru toată sticla.
2. **Fișier → Export 3D (glTF, USD, DAE)…** și alege **GLB** (recomandat: un fișier cu texturile, numele exacte și sticla), **USDZ** sau **OBJ + MTL** (un .zip cu texturile; spațiile din nume devin liniuțe jos).
3. Importă fișierul în programul de randare. Toate tocurile au acolo un singur material; îl înlocuiești o dată cu metalul programului și se schimbă toate.

![O casă cu tocuri gri închis și sticlă albastră](shots/materials-render-3d.webp)

Și exportul IFC numește stilul de suprafață al fiecărui material după el, cu transparența și felul în care reflectă.

## Ce setează un material

- **Culoare (3D)** și fețele desenate pe el în 3D. Lapin le ghicește din nume, de exemplu rândurile unei cărămizi sau rosturile unei plăci de faianță. **Capete** și **Sus și jos** pot arăta altfel decât fețele.
- **Hașura** pentru fiecare loc în care se desenează: secționat într-o secțiune, secționat într-un plan, și văzut într-o fațadă. Fiecare are scara, unghiul, alinierea, umplutura și penița ei.
- **Categoria**, de exemplu finisaj, izolație sau structură. Stilurile de vedere o folosesc ca să lase finisajele afară dintr-o vedere de structură.
- **Membrană** desenează un strat subțire ca linie întreruptă în loc de bandă.
- **Desenează rosturile.** Implicit, un material trece de la un element la următorul fără linie, ca doi pereți în prelungire sau o tencuială peste două etaje. Bifează ca să se deseneze rostul.

## O imagine proprie pe un material

Un material poate purta o imagine a ta: o fotografie a cărămizii, a lemnului sau a pietrei adevărate.

1. Alege materialul și apasă **Imagine proprie → Importă o imagine…**. Alege un fișier PNG sau JPEG.
2. Setează **W** și **H**: cât de lată și cât de înaltă, în metri, stă o copie a imaginii pe model. Imaginea se repetă pe fețe.

![Imaginea proprie a unui material, pusă la 1,2 × 1,2 m](shots/own-image.webp)

Imaginea se vede în vederea 3D și e salvată în fișierul proiectului. Merge și în exporturile 3D: **GLB**, **glTF** și **USDZ** o duc cu ele. **DAE** și **USDA** sunt fișiere unice, fără loc pentru imagini, așa că păstrează culoarea materialului.

![Aceeași casă cu imaginea proprie a materialului în 3D](shots/own-image-3d.webp)

Imaginile mai mari de 2048 de pixeli se micșorează la această mărime când le imporți. Butonul cu coș de gunoi scoate imaginea de pe material.

## Hașurile tale (.pat)

**Importă .pat** citește un fișier de hașuri AutoCAD. Fiecare model din el intră în proiect, gata să fie ales ca hașură a unui material.

Lapin păstrează fișierul și în **Modelele mele**, pe calculatorul tău. În orice alt proiect, **Adaugă în acest proiect** aduce din nou acele modele. Un proiect își păstrează modelele folosite, așa că se deschide la fel pe alt calculator.

![Modelele mele: fișierele .pat importate, păstrate pentru orice proiect](shots/my-patterns.webp)
