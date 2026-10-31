---
title: Grupuri și curățare
order: 112
summary: Ține elementele împreună într-un grup, deschide un grup ca să-l editezi, selectează cu o regiune sau un lasou în 3D și ascunde ce îți stă în cale.
---

## Grupuri

Un grup ține elementele împreună. Selectează-le și apasă **Modifică → Creează grup** (<kbd>GP</kbd>). Un clic pe oricare membru selectează apoi tot grupul, în planuri și în 3D. Grupul se mută, se copiază, se rotește, se oglindește și se șterge ca un întreg, iar o copie e un grup al ei. În IFC un grup e un IfcGroup, așa că îl văd și alte programe.

Proprietățile unui grup selectat arată câte elemente are, cu trei butoane: **Editează grupul**, **Degrupează** și **Selectează membrii**.

![Un grup selectat: membrii lui luminați, proprietățile lui](shots/group-selected.webp)

## Editarea unui grup

Dă dublu-clic pe un membru sau apasă **Editează grupul** (<kbd>EG</kbd>). Restul modelului se desenează estompat și nu se poate selecta, iar un clic selectează câte un membru. Șterge, mută sau schimbă membrii ca de obicei. **Scoate din grup** scoate membrii selectați. **Gata** sau <kbd>Esc</kbd> închide grupul. Un grup dispare când i se șterge ultimul membru.

![Un grup deschis pentru editare: zidul grădinii, din afara lui, estompat](shots/group-edit.webp)

## Fișierele importate

Fiecare fișier IFC, Fragments sau rețea pe care îl imporți intră ca un singur grup, cu numele fișierului. Debifează **Ca un singur grup** la importul IFC ca să aduci elementele libere. Un fișier legat nu se grupează: e deja o legătură, doar pentru citire (vezi [Import și export](import-export.md)). Elementele citite dintr-un IFC ca referințe rămân unde le-a pus fișierul: un grup de astfel de elemente se poate curăța și șterge, nu muta.

## Selectarea unei regiuni în 3D

**Vedere → Selectare regiune** (<kbd>SR</kbd>) face din tragerea cu butonul stâng în 3D o selecție, ca în planuri. Trasă spre dreapta, o fereastră ia ce e cuprins în întregime în ea. Trasă spre stânga, un crossing ia tot ce atinge. Apasă <kbd>Space</kbd> în timp ce tragi ca să desenezi un lasou, și <kbd>Shift</kbd> ca să adaugi la selecție. Cât e pornită, butonul drept rotește vederea, iar cel din mijloc o mută. **×** de pe eticheta ei, sau din nou <kbd>SR</kbd>, o oprește.

![Un crossing tras peste casă în 3D](shots/select-region-3d.webp)

## Ascunderea temporară

**Vedere → Ascunde** (<kbd>HH</kbd>) ascunde selecția în vederea 3D. **Izolează** (<kbd>HI</kbd>) ascunde tot restul. **Arată tot** (<kbd>HR</kbd>), sau **×** de pe etichetă, aduce totul înapoi. Nimic nu se salvează și nu se șterge: e un mod de a ajunge la ce e în interiorul unui model importat cât timp îl cureți.
