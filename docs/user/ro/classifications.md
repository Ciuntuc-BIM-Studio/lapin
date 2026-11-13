---
title: Clasificări și parametri
order: 91
summary: Uniclass, OmniClass, CCI sau codurile proprii ale biroului pe elemente și tipuri, notele cheie, parametri definiți o dată pentru tot proiectul și cum ajung în IFC și în etichete.
---

O clasificare dă fiecărui element un cod dintr-un sistem folosit de biroul sau de țara ta: Uniclass `Ss_25_10`, un număr OmniClass, un cod CCI, o notă cheie proprie. **Gestionare → Clasificări…** ține sistemele și parametrii partajați ai proiectului.

![Gestionare → Clasificări](shots/classifications-dialog.webp)

> Lapin **nu livrează niciun sistem de clasificare**: licențele lor sunt ale tale. Aduci un sistem dintr-un tabel pe care îl deții sau începi unul propriu.

## Sistemele

Tabul **Sisteme** listează clasificările proiectului, fiecare cu numărul de elemente (items) și câte elemente și tipuri îl folosesc.

1. **Nou** face un sistem gol. Dă-i un nume și completează **Sursa** (a cui este: editorul, biroul) și **Ediția**.
2. **Importă elemente dintr-un tabel…** citește un fișier CSV sau Excel, sau celule lipite: un cod, un titlu și codul părintelui. Vezi *Importă tabel…* pentru cum se potrivesc, se previzualizează și se importă coloanele.
3. Arborele arată elementele sistemului; **Caută un cod sau un titlu** îl caută.
4. **Clasifică elementele dintr-un tabel…** dă coduri multor elemente deodată: un tabel de elemente (după GlobalId, marcă sau nume) și codurile lor în acest sistem.
5. **Exportă CSV…** scrie sistemul înapoi ca CSV pe care Lapin îl citește din nou. **Șterge** scoate un sistem cu codurile lui (te întreabă, spunând câte elemente și tipuri își pierd codul).

## Coduri pe elemente și tipuri

Selectează un element: **Proprietăți → Clasificare** are **Clasifică…**. Alege un sistem, caută codul în arborele lui și dă clic pe el. Fiecare sistem are un singur cod pe un element; **×** de lângă el scoate codul. Mai multe elemente selectate de același fel îl primesc împreună.

Codul unui tip e al tipului: elementele tipului îl poartă, dacă nu au un cod propriu.

![Grupul Clasificare din Proprietăți](shots/classification-properties.webp)

### Notele cheie

Un sistem nu trebuie să vină de la un editor. Când cauți un cod pe care sistemul nu-l are, **Folosește „…” ca un cod nou** îl adaugă sistemului ca element al tău, cu titlul scris după cod (`K12 Rost etanșat la foc`). Notele cheie ale biroului cresc pe măsură ce le folosești.

## În etichete

O etichetă, un tabel sau un text citește codul: `{classification.code}` și `{classification.title}` scriu primul cod al elementului, `{classification.Uniclass}` codul sistemului cu acel nume.

## IFC

Clasificările ies în IFC așa cum le ține IFC: fiecare sistem un `IfcClassification`, fiecare cod un `IfcClassificationReference` legat de elementele și tipurile care îl poartă (referințele părinților, imbricate). Când Lapin deschide un fișier IFC cu ele, fiecare clasificare devine un sistem al proiectului, iar fiecare referință un element al lui, pus pe elementele și tipurile cu care e legată. Un model Lapin își păstrează codurile prin IFC și înapoi.

## Parametri partajați

Tabul **Parametri partajați** ține definițiile de parametri ale proiectului, ca un parametru să însemne un singur lucru peste tot: etichetele, tabelele și tipurile îl găsesc după nume.

O definiție are un **Nume**, un **Fel** (text, number, length, area, angle, bool sau choice), **Setul de proprietăți** în care ajunge în IFC (gol: `Lapin_Parameters`; un set al tău nu începe niciodată cu `Pset_`, pe care IFC și-l păstrează), **Variantele** pentru un parametru de alegere (separate prin virgule) și o **Descriere**. **Nou** deschide formularul, **Adaugă** sau **Salvează** îl păstrează, iar un clic pe un nume îl editează. Scoaterea unei definiții lasă parametrii deja puși pe elemente cum sunt.

Un parametru adăugat în Proprietăți cu numele unei definiții îi ia felul și setul. **Exportă .lapinparams…** și **Importă .lapinparams…** duc definițiile în alt proiect.

![Parametri partajați](shots/classifications-params.webp)
