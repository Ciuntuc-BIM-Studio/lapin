---
title: Panouri de acoperiș
order: 41
summary: Așezi țiglă metalică sau panouri cu falț pe apele acoperișului: ce se cumpără pe lungimi, resturile refolosite în dolii, panourile desenate ca învelitoare.
---

## Țiglă metalică și panouri pe un acoperiș

Un acoperiș poate fi învelit cu un profil de țiglă metalică sau de panou (Bilka Balcanic, Bilka Clasic 35 și Bilka Retro Panel vin cu Lapin; datele lor sunt dimensiunile publicate de producător, orientative: verifică-le cu producătorul înainte de comandă). Lapin așază singur panourile pe fiecare apă, din acoperiș așa cum este: schimbi acoperișul, panourile îl urmează.

Selectează un acoperiș în pante și deschide **Panouri** în panoul de proprietăți:

- **Profil.** Profilul cu care e învelit acoperișul; **niciunul** păstrează învelitoarea ca un singur strat, ca înainte.
- **Culoare.** Culorile în care se fabrică profilul (un profil cu o singură culoare o arată).
- **Panoul cel mai lung.** La un profil la comandă care îl oferă (Retro Panel: 4000 sau 7130 mm): un panou maxim mai scurt pune îmbinări pe pantele lungi, cum recomandă ghidul producătorului.

Fiecare schimbare e un singur pas de anulare. Dedesubt secțiunea arată **De cumpărat** (panourile pe lungimi și câte), **Panouri montate**, **Resturi refolosite** (piese de dolie tăiate din ce lasă piesele de coamă oblică din panourile lor, necumpărate), **Tablă cumpărată (m²)** (fiecare panou cumpărat la lungimea lui nominală ori lățimea totală, cum se cotează panourile) și **Avertismentele**: o apă sub panta minimă a profilului, ferestrele de acoperiș și lucarnele (încă nedecupate din panouri).

![Secțiunea Panouri a unui acoperiș: Bilka Balcanic, panourile de cumpărat și resturile refolosite](shots/roof-panels-section.webp)

## Panotajul

**Panotaj…** (în secțiunea Panouri, sau **Arhitectură → Panouri de acoperiș → Panotaj…** cu un acoperiș selectat) arată panotajul: acoperișul **În plan** sau **Desfășurat** (fiecare apă în adevărată mărime), fiecare panou colorat după lungime, resturile refolosite în **portocaliu** cu o linie punctată de la panoul din care sunt tăiate, și muchiile colorate după fel (streșini, frontoane, coame, coame oblice, dolii). Arată spre un panou ca să-i citești codul (apă, coloană, rând: A03-07-01), lungimea și din ce e tăiat.

Lângă desen: **De cumpărat** (lungimi, bucăți, metri), aria acoperișului, acoperirea și tabla cumpărată, **Pe ape** (pantă, arie, panouri, cumpărate, refolosite, tablă), **Muchii** (câte, ce lungime, câte piese de accesorii de lungimea accesoriilor profilului) și **Resturi** (fiecare piesă cu donatorul ei și cât de sus pe panoul donator e tăiată).

Ca să compari, alege alt **Profil** în dialog: panotajul îl arată, iar acoperișul își păstrează profilul; **Folosește acest profil** i-l dă acoperișului (un pas de anulare).

![Panotajul în plan: panourile pe lungimi, piesele de dolie tăiate din resturile de la coamele oblice în portocaliu](shots/roof-panel-layout.webp)

![Același acoperiș desfășurat, fiecare apă în adevărată mărime](shots/roof-panel-layout-flat.webp)

## Panourile în model

Cu un profil, învelitoarea acoperișului **sunt** panourile lui: în 3D fiecare panou e o tablă profilată ca țigla sau panoul (undele, treptele țiglei, nervurile falțului), în culoarea profilului, resturile refolosite în portocaliu. Planurile, fațadele și secțiunile desenează fiecare panou ca o placă de grosimea învelitorii peste ce acoperă, deci un desen arată fiecare îmbinare fără liniile ondulației; un plan la 1:200 sau mai mare desenează conturul panourilor cu celelalte linii de deasupra ale acoperișului. La un acoperiș cu straturi proprii, stratul de sus e înlocuit de panouri.

Acolo unde se întâlnesc două acoperișuri, panourile urmează învelitoarea așa cum e construită: apele unei aripi se opresc în doliile cu acoperișul principal, panourile la distanța de dolie de ele, iar apa principală e panotată în jurul aripii.

Exportul IFC scrie încă învelitoarea ca o singură placă de acoperiș (panourile ca IfcCovering vin într-o etapă următoare), deci GlobalId-urile nu se schimbă.

![Panourile ca învelitoarea acoperișului în 3D](shots/roof-panels-3d.webp)

## Extrasul panourilor de acoperiș

**Arhitectură → Panouri de acoperiș → Panouri de acoperiș** face extrasul **Panouri de acoperiș**, un extras de câmpuri al panourilor fiecărui acoperiș: acoperiș, profil, culoare, lungime, cumpărat sau tăiat dintr-un rest, bucăți, lungime totală și tablă cumpărată, cu un rând de totaluri. Ca orice extras de câmpuri, se poate schimba, sorta și exporta. Fiecare panou e o piesă a acoperișului lui, deci o etichetă de piesă îi scrie codul și lungimea.
