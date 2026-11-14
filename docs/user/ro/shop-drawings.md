---
title: Desene de execuție
order: 105
summary: Faci planșa proprie a unui element într-o singură fereastră: vederile lui desenate după axele lui, cu cotele de gabarit și datele lui, mereu vii.
---

## Ce este un desen de execuție

Un desen de execuție e desenul **unui singur element**: un profil de oțel, o piesă Studio, o placă. Are propria planșă cu cartuș, vederile pe care le-ai bifat, desenate după **axele proprii** ale elementului (X de-a lungul piesei, Y peste ea, Z în sus) și nu după cele ale proiectului, cotele de gabarit ale elementului și un tabel cu datele lui (nume, material, profil, greutate și altele) deasupra cartușului.

Desenul e mereu **viu**. Vederile sunt vederi ale modelului ca oricare altele, deci când schimbi elementul se schimbă și planșa; nimic nu e o copie.

## Fă un desen

Selectează **un singur** element, apoi dă clic dreapta pe el și alege **Fă desenul…**, sau apasă **Desen de execuție** în fila **Adnotări**. Dacă nu e selectat nimic, sau sunt mai multe elemente, Lapin îți cere să alegi unul singur.

![Fereastra Fă desenul: piesa cu o bifă pe fiecare față, secțiunile, alegerile și planșa vie](shots/shop-drawing-dialog.webp)

În stânga e elementul însuși, desenat de motor în izometrie, cu axele lui: **X** roșu, **Y** verde, **Z** albastru. Fiecare dintre cele șase fețe are o bifă: **Față**, **Spate**, **Sus**, **Jos**, **Stânga** și **Dreapta**. Bifează o față ca să pui pe planșă vederea ei. Bifa **Izometric** adaugă vederea izometrică.

Dedesubt, **Secțiunile** sunt tăieturi paralele cu fețele. Fiecare axă are un buton **Adaugă**, iar fiecare secțiune adăugată are un glisor pe axa ei, de la un capăt la celălalt al piesei, cu poziția în milimetri față de originea piesei. Adaugă câte ai nevoie; crucea de la capătul rândului scoate una.

În dreapta alegi cum se face planșa:

- **Regulă**: setul de vederi, cote și secțiuni de la care pornește felul elementului. Prima e regula felului lui; altă regulă umple din nou bifele.
- **Scara**: **Automat**, sau de la 1:1 la 1:100.
- **Format**: **Automat**, sau de la A4 la A0. Cu **Automat**, Lapin ia cel mai mic format pe care încap vederile.
- **Proiecția**: **ISO E (prima proiecție)** e felul european: vederea de sus se desenează **sub** vederea din față, cea din stânga la dreapta ei. **A treia proiecție** pune fiecare vedere pe partea dinspre care se vede. Proiecția proiectului e alegerea de pornire.
- **Dimensiuni**: **Total** dă lungimea, lățimea și înălțimea piesei pe vederi; **Niciunul** le lasă deoparte.
- **Preferă**: când nu se pot păstra amândouă, dacă primează formatul sau scara pe care le-ai ales.

**Previzualizarea** de sub alegeri e planșa adevărată, compusă de motor la fiecare schimbare. Dedesubt citești formatul și scara alese. Dacă vederile nu încap, un avertisment o spune: alege un format mai mare sau o scară mai mică.

Alegerile pornesc de la regulă, așa că de cele mai multe ori poți apăsa **Creează** de la început.

## Planșa

**Creează** face desenul într-un singur pas și deschide planșa.

![Planșa piesei: trei vederi, cotele de gabarit și tabelul de date deasupra cartușului](shots/shop-drawing-sheet.webp)

Vederile sunt vederi obișnuite pe o planșă obișnuită: mută-le, schimbă-le scara, completează cartușul. Vederile **arată doar elementul lor**: restul modelului nu se desenează în spatele lui.

## Fă-l din nou

Deschide **Fă desenul…** pe același element și butonul se numește **Actualizează**. Actualizarea păstrează planșa și vederile ei, și modificările tale pe ele (o vedere redenumită, o linie sau o notă desenată pe o vedere, elemente ascunse în ea); vederile se așază din nou pe planșă. Un singur **Anulează** (Undo) scoate tot desenul: planșa, vederile și desenul însuși.

> Marcajele pieselor și cantitățile, precum și desenele de ansamblu cu lista pieselor, vin într-o versiune ulterioară.
