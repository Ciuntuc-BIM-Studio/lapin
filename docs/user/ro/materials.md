---
title: Materiale, texturi și hașuri
order: 90
summary: Setezi culoarea, hașurile și rosturile fiecărui material, pui pe el o imagine proprie și îți aduci propriile hașuri .pat.
---

**Gestionare → Materiale** listează materialele proiectului. Alege unul ca să-l modifici.

![Fereastra Materiale](shots/materials-dialog.webp)

## Ce setează un material

- **Culoare (3D)** și fețele desenate pe el în 3D. Lapin le ghicește din nume, de exemplu rândurile unei cărămizi sau rosturile unei plăci de faianță. **Capetele** și fețele de **sus și jos** pot arăta altfel decât fețele.
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
