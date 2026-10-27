---
title: Import și export
order: 110
summary: Schimbi modelul ca IFC, aduci desene DXF, legi fișiere și exporți modele 3D pentru randare și AR.
---

Tot ce intră sau iese e în meniul **Fișier**.

![Meniul Fișier](shots/file-menu.webp)

## IFC

**Exportă IFC…** scrie modelul ca IFC. Fiecare perete, placă, ușă și acoperiș e un element IFC adevărat, cu straturile, tipul și proprietățile lui, așa că fișierul se deschide ca model în alte programe BIM. Ușile și ferestrele spun cum se deschid: mâna fiecărei foi și cercevele, în ce parte se deschid și panourile, cum le definește IFC, așa că alte programe desenează aceleași deschideri.

**Importă IFC…** citește IFC-ul altui program. Lapin recunoaște pereții, plăcile, stâlpii, grinzile, fundațiile, ușile și ferestrele și le face elemente Lapin pe care le poți edita. Ce nu poate recunoaște intră ca referință, desenată, dar needitabilă. Ușile și ferestrele intră deschizându-se cum spune fișierul: mâna, partea în care se deschid și, la ferestre, cum se deschide fiecare panou.

## DXF și DWG

**Import DXF…** aduce un desen CAD în vederea din față, cu layerele, hașurile, textele și cotele lui. Intră ca desen de care te poți prinde și pe care îl poți edita. Ca să folosești un DWG, salvează-l întâi ca DXF din programul tău CAD (AutoCAD, BricsCAD sau DraftSight: **Save As → DXF**). Importul de PDF nu există încă.

## Legături

**Link IFC…** ține alt fișier legat în loc să-l importe. E doar pentru citire și se reîncarcă atunci când se schimbă. Merge și pentru un DXF. **Gestionare → Legături** arată, reîncarcă, convertește sau scoate legăturile.

## Modele 3D

**Exportă 3D…** scrie modelul ca suprafețe pentru randare, vizualizare și realitate augmentată (AR):

| Format | Pentru |
|---|---|
| GLB, glTF | Blender, three.js, vizualizatoare web. Imaginile proprii ale materialelor sunt incluse. |
| USDZ | AR Quick Look de la Apple, Reality Composer, Omniverse. Imagini incluse. |
| USDA | OpenUSD ca text, un singur fișier. |
| DAE | COLLADA, pentru SketchUp și programe mai vechi. |

![Export 3D](shots/export-3d.webp)

Alegi ce exporți (tot modelul sau ce e selectat), ce fază și originea. Fiecare element își păstrează clasa IFC, tipul și GlobalId-ul. Ca să schimbi modelul în sine, nu suprafețele lui, folosește **Exportă IFC**.
