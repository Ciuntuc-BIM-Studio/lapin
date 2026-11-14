---
title: Tavane false
order: 41
summary: Atârni un tavan fals într-o cameră, alegi grila cu plăci sau gips-cartonul continuu, pui corpuri de iluminat și anemostate în casetele lui și le numeri.
---

Un tavan fals atârnă sub placa de deasupra, la o înălțime pe care o stabilești tu. Se desenează pe un contur sau într-o cameră, iar **tipul** lui spune din ce e făcut: plăci așezate pe o structură aparentă sau gips-carton continuu. Corpurile de iluminat și anemostatele stau în casetele grilei, iar cantitățile le numără.

## Cum desenezi un tavan

Uneltele sunt în **Interior → Tavan**:

- **Tavan** (<kbd>CG</kbd>) desenează un tavan pe un contur: dai clic pe colțuri, apoi din nou pe primul sau apeși <kbd>Enter</kbd>.
- **Tavan într-o cameră** umple camera pe care dai clic, după conturul ei dintre fețele pereților. Acolo trebuie să fie o încăpere ([Încăperi](rooms.md)); dacă nu e, Lapin spune asta.

Cât timp o unealtă e activă, panoul de proprietăți arată **Element nou** cu tipul de tavan pe care îl pune, de ales înainte de clic. Tavanul se așază pe nivelul activ și atârnă la **Înălțimea** lui peste el: 2700 mm, dacă nu o schimbi. Selectează tavanul și proprietățile lui arată **Tip de tavan** și **Înălțime**; le schimbi acolo, iar gripurile de pe colțuri îi modifică conturul.

Odată desenat, tavanul nu mai urmează nimic: dacă muți un perete, mută și colțurile tavanului.

## Tipuri de tavan

**Interior → Tipuri de tavan** (sau **Tipuri de tavan…** în proprietățile unui tavan) arată în stânga tipurile proiectului, cu numărul de tavane care le folosesc. Alege unul ca să-l modifici sau pornește de la **Tavan casetat nou** ori **Tavan nou din gips-carton**. **Salvează tipul** schimbă deodată toate tavanele tipului; **Pune cu el** face ca uneltele să-l folosească de acum încolo. Proiectul primește primul tip, un tavan casetat, când desenezi primul tavan.

![Dialogul tipurilor de tavan: placa, structura și tijele unui tavan casetat](shots/ceilings-types.webp)

**Sistemul** alege ce se construiește:

- **Plăci pe structură aparentă.** Implicit, **Grid ceiling 600×600**: plăci așezate pe o grilă de profile T.
  - **Lățimea plăcii** și **Lungimea plăcii** (600 × 600), **Grosime** (15 mm), **Materialul** și **Culoarea** plăcii.
  - **Structură**: **Lățimea vizibilă** (24 mm) și **Adâncimea** (38 mm) profilelor, **Rotirea (°)** întregii grile, **Materialul structurii** și **Culoarea structurii**. Profilele merg în ambele sensuri la fiecare linie de casetă, cu un cornier pe marginea tavanului.
  - **Tije de suspendare până la structură**, la **Distanța între tije** (1200 mm): tije de la profile până sus, la planșeul etajului.
- **Gips-carton, continuu.** Implicit, **Plasterboard ceiling** e o singură placă cât conturul tavanului, groasă de 12,5 mm, fără grilă și fără tije. Lățimea și lungimea modulului fac totuși casetele pe care le folosesc corpurile.

**În casete** alege culorile corpurilor de iluminat și ale anemostatelor.

Grila e centrată pe tavan, așa că plăcile tăiate de la margini ies egale de ambele părți. Rotești grila din **Rotire (°)**, iar casetele se rotesc în jurul mijlocului tavanului.

## Corpuri de iluminat și anemostate

**Corp de iluminat** și **Anemostat**, lângă uneltele tavanului, pun câte unul în caseta pe care dai clic. Un corp ocupă o casetă întreagă: placa de acolo face loc, corpul de iluminat umple caseta, iar anemostatul e o placă cu gâtul lui pătrat. O casetă tăiată de marginea tavanului nu primește nimic (Lapin o spune). Un clic pe o casetă care are deja un echipament, cu cealaltă unealtă, îl schimbă.

Ca să umpli un tavan dintr-o dată, selectează-l și, la **Așază** în proprietăți, scrie **La fiecare** *n* **casete** și apasă **Corpuri de iluminat** sau **Anemostate**. Fiecare pune un echipament de felul lui în fiecare a *n*-a casetă întreagă, în ambele sensuri, și le înlocuiește pe cele de același fel puse înainte. Corpurile de iluminat încep la o casetă de colț, iar anemostatele chiar la colț, deci cu același *n* nimeresc în casete diferite. **Golește** scoate toate echipamentele.

Panoul de proprietăți numără corpurile de iluminat și anemostatele.

![Proprietățile unui tavan: tipul, înălțimea și echipamentele](shots/ceiling-properties.webp)

![Tavane casetate cu corpuri de iluminat și anemostate, în 3D](shots/ceilings-3d.webp)

Văzut de jos în 3D: un tavan cu grilă de 600 × 1200, cu un corp de iluminat și un anemostat la fiecare a treia casetă, și un tavan din gips-carton cu corpuri de iluminat în camera de alături:

![Un tavan casetat văzut de jos, cu corpuri de iluminat și anemostate](shots/ceilings-grid-3d.webp)

## În planuri

Într-un plan, tavanul se desenează ca conturul a ce se află deasupra planului de secționare, ca celelalte elemente de deasupra, dacă stilul vederii arată elementele de deasupra (**Vedere → Setările vederii**). Ca să vezi tavanele de jos, pune **Văzut din** al planului pe **jos (plan de plafon)** în **Vedere → Setările vederii**: un plan de tavan reflectat. Vezi [Secțiuni, fațade și 3D](views.md).

![Tavanele într-un plan de tavan reflectat](shots/ceilings-plan.webp)

## Cantități și IFC

Selectează un tavan și **Cantități** îi listează:

- **aria brută** și **netă** (a conturului) și grosimea, ca la cantitățile IFC ale unei îmbrăcăminți;
- **perimetrul**;
- **plăcile întregi** (casetele întregi ale unui tavan casetat, fără cele pe care le iau echipamentele; plăcile tăiate nu se numără);
- **corpurile de iluminat** și **anemostatele**.

**Execuție → Antemăsurătoare** adună tavanele pe tip, iar devizul le pune la finisaje ([Extrase](schedules.md), [Deviz](cost.md)).

În IFC un tavan e un **IfcCovering** de tip predefinit **CEILING**, al unui **IfcCoveringType** (tipul lui de tavan), cu plăcile, structura, tijele, corpurile de iluminat și anemostatele ca formă a îmbrăcămintei. Echipamentele nu sunt elemente IFC separate.

**Finisajul de tavan** al unei încăperi ([Încăperi](rooms.md)) e altceva: straturi lipite sub placă. Tavanul fals e pentru un sistem atârnat, cu echipamente; folosește finisajul când ai nevoie doar de un strat.
