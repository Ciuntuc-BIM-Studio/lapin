---
title: Încărcări
order: 51
summary: Cazuri de încărcare, încărcări pe suprafață, liniare și punctuale, încărcările utile ale încăperilor, încărcările derivate și combinațiile (CR 0-2012 sau EN 1990, cu coeficienții și combinațiile tale), pentru un calcul structural.
---

Grupul **Încărcări** din tabul **Structură** are încărcările pe care le preia un calcul structural: **Cazuri de încărcare…**, **Încărcare pe suprafață**, **Încărcare liniară** și **Încărcare punctuală**. Valorile sunt în kN, kN/m și kN/m².

![Grupul Încărcări din tabul Structură](shots/ribbon-loads.webp)

> Valorile pe care le livrează Lapin (încărcările utile din EN 1991-1-1 și coeficienții din EN 1990, cele recomandate) sunt **orientative**. Pune valorile anexei tale naționale și verifică-le înainte de a folosi rezultatele.

## Cazuri de încărcare

**Cazuri de încărcare…** deschide dialogul Încărcări. **Adaugă cazurile standard** face G1 (greutate proprie), G2 (finisaje și pereți despărțitori), Q-A (utilă, categoria A), S (zăpadă) și W (vânt).

![Fila Cazuri din dialogul Încărcări](shots/loads-cases.webp)

Un caz are un **Cod**, un **Nume**, un **Tip** (Permanentă, Variabilă sau Accidentală), o **Sursă** (Greutate proprie / încărcare permanentă, Finisaje, Utilă, Zăpadă, Vânt, Seism sau Definită de utilizator) și, pentru un caz de încărcare utilă, o **Categorie**. Cele trei câmpuri ψ0, ψ1 și ψ2 sunt coeficienții de combinare; lasă-le goale ca să se ia cei ai categoriei din tabel. O valoare care nu e un număr de la 0 la 1 e marcată și nu se păstrează.

**Adaugă cazurile lipsă** face un caz de încărcare utilă pentru fiecare categorie folosită de încăperi care nu are unul.

## Așezarea încărcărilor

- **Încărcare pe suprafață**: dă clic pe colțuri, apoi apasă <kbd>Enter</kbd>.
- **Încărcare liniară**: dă clic pe cele două capete.
- **Încărcare punctuală**: dă clic unde acționează.

Cât timp o unealtă e pornită, bara liniei de comandă oferă **valoarea** încărcării și **cazul de încărcare** în care intră (implicit **Încărcare utilă (Q-A)**). Încărcarea intră pe nivelul activ.

![O încărcare pe suprafață de 2,5 kN/m² desenată în living](shots/loads-plan.webp)

## Ce calculează Lapin pentru tine

Pe acestea nu le desenezi; Lapin le deduce din model de fiecare dată când le ceri, și urmează orice schimbare:

- **Greutatea proprie** a planșeelor, pereților, stâlpilor și grinzilor, din densitățile materialelor lor, în cazul a cărui sursă e greutatea proprie (G1).
- **Finisajele planșeelor**, din straturile lor, și **pereții neportanți** (LoadBearing debifat, sau un perete care nu spune nimic și e mai subțire de 10 cm ori nu e din zidărie sau beton: vezi [Calculul structural](analysis.md)), ca încărcări liniare, în cazul care enumeră finisajele și pereții despărțitori (G2).
- **Încăperile**: încărcarea utilă a categoriei unei încăperi pe aria ei, în cazul acelei categorii (Q-A, Q-B…).
- **Zăpada** și **vântul**, de îndată ce dai valorile amplasamentului (vezi [Zăpadă și vânt](#zăpadă-și-vânt)).

Un material fără densitate e lăsat pe dinafară, iar Rezumatul spune care.

## Încărcarea utilă a unei încăperi

Selectează o încăpere: proprietățile ei au o secțiune **Încărcare utilă**. **Categoria** ei e **Din funcție** (un living, un dormitor sau o bucătărie e A, un birou B, un depozit E1, un garaj F) dacă nu alegi alta, sau **None**. Dedesubt e **qk** dat de categorie.

![Încărcarea utilă a unei încăperi în proprietățile ei](shots/loads-room.webp)

## Tabelul încărcărilor utile

Fila **Încărcări utile** enumeră fiecare categorie cu folosința ei și **qk** în kN/m², cum recomandă EN 1991-1-1. Scrie în câmp valoarea anexei tale naționale; **Modificat** revine la valoarea implicită, care e cea a pachetului de reguli în uz (al lui Lapin: cea din EN 1991-1-1; a unui pachet propriu, în fila lui Încărcări, vezi [Reguli](rules.md)).

![Fila Încărcări utile](shots/loads-imposed.webp)

## Zăpadă și vânt

Fila **Zăpadă și vânt** are clima amplasamentului. Lapin nu livrează hărțile naționale: citește valorile amplasamentului tău din **CR 1-1-3/2012** (zăpada, figura 3.1 sau anexa A) și **CR 1-1-4/2012** (vântul, figura 2.1 sau anexa A), ori din anexa națională după care lucrezi, și scrie-le.

![Fila Zăpadă și vânt](shots/loads-climate.webp)

- **Zăpada**: **Încărcarea din zăpadă pe sol s0,k (kN/m²)**, **Expunerea Ce** (completă 0,8, parțială 1,0, redusă 1,2), **Coeficientul termic Ct** (1,0, în afară de cazul unui acoperiș vitrat care pierde căldură) și **Factorul de importanță γIs** (1,0 pentru clădirile obișnuite). **Aplică zăpada**.
- **Vântul**: **Presiunea de referință a vântului qb (kN/m²)**, **Categoria de teren** (de la 0 la IV, de la mare până în centrul unui oraș) și **Factorul de importanță γIw**. **Aplică vântul**.

Aplicarea lor face cazurile în care intră încărcările:

- **S**, zăpada netroienită, și **S+X**, **S-X**, **S+Y**, **S-Y**, zăpada troienită de un vânt care suflă spre fiecare latură: pe un acoperiș în două ape, panta dinspre vânt ia jumătate. Un troian care nu schimbă nimic aici (nicio pantă nu e expusă acelui vânt) e lăsat pe dinafară, iar Rezumatul spune asta.
- **W+X/s**, **W+X/p** și tot așa pentru cele patru direcții: vântul care suflă spre +X, −X, +Y, −Y, o dată cu **aspirația** acoperișurilor (/s) și o dată cu **presiunea** lor (/p).

Aranjamentele zăpezii și direcțiile vântului sunt alternative: o combinație ia doar una din fiecare.

Ce deduce Lapin din ele, de fiecare dată când le ceri:

- **Zăpada** pe fiecare pantă de acoperiș, pe m² în plan: s = γIs · μ1 · Ce · Ct · s0,k, cu μ1 = 0,8 până la 30°, apoi mai puțin, 0 la 60° și mai abrupt; pe un planșeu pe care nu-l acoperă nimic (o terasă, un acoperiș plat) μ1 = 0,8.
- **Vântul** pe pereți și acoperișuri: conturul clădirii e anvelopa convexă a pereților și stâlpilor; fațadele iau zonele A–E, acoperișurile plate F–I, un acoperiș în două ape F–J, după coeficienții din EN 1991-1-4, la presiunea înălțimii clădirii pentru categoria de teren. O fațadă își trece vântul grinzilor, riglelor și marginilor de planșeu de-a lungul ei, fâșia ei cea mai de jos terenului; un acoperiș, căpriorilor sau panelor lui. Acoperișurile în patru ape, presiunea interioară și troienele la parapeți nu sunt încă derivate.

> Aceste valori sunt **orientative**: coeficienții sunt cei recomandați de norme. Verifică-i după anexa ta națională.

## Rezumat

Fila **Rezumat** arată, pentru fiecare caz, încărcările desenate și totalul în kN, în total și pe fiecare nivel, apoi încărcările derivate una câte una, cu sursa lor. Te avertizează de încăperile a căror categorie nu are caz și notează ce a rămas pe dinafară.

![Fila Rezumat](shots/loads-summary.webp)

## Combinații

Fila **Combinații** enumeră combinațiile cazurilor, așa cum le scrie norma, orientative: pentru starea limită ultimă 1,35 pe acțiunile permanente și 1,5 pe cea variabilă conducătoare, celelalte cu ψ0 al lor; pentru starea limită de serviciu cele caracteristice, frecvente și cvasipermanente. Un caz care nu conține nimic nu participă. Grupul stării limită ultime cuprinde și combinațiile accidentale și seismice pe care le adaugi.

![Fila Combinații](shots/loads-combinations.webp)

### Coeficienții

Secțiunea **Coeficienți** din capul filei stabilește coeficienții parțiali γ și coeficienții de combinare ψ pe care îi folosesc combinațiile. Sunt ai tăi de verificat: seturile livrate de Lapin sunt orientative.

- **Set de coeficienți.** Un proiect nou ia **CR 0-2012 (România)**; un proiect făcut înainte de această setare rămâne cu **EN 1990 Anexa A1, valori recomandate**, deci combinațiile lui nu se schimbă. Cele două diferă prin ψ: CR 0-2012 ia zăpada cu 0,7, 0,5, 0,4, vântul cu 0,7, 0,2, 0 și acoperișurile (categoria H) cu ψ0 0,7, acolo unde EN are 0,5, 0,2, 0 la zăpadă, 0,6, 0,2, 0 la vânt și 0 la acoperișuri. Lista oferă și seturile pachetului de reguli în uz (Gestionare → Reguli…, fila lui Încărcări): un set al țării tale, cu γ, ψ, expresia și ξ proprii. Un pachet le poate schimba și pe cele două ale lui Lapin; cu pachetul lui Lapin, sau cu o copie lăsată neschimbată, ele dau exact combinațiile pe care le-au dat mereu. Un proiect care numește un set pe care pachetul în uz nu-l are ia setul EN, iar notele spun asta.
- **Clasa de siguranță.** Apare când setul dă γd pe clase de siguranță: alege clasa proiectului, iar γd-ul ei înmulțește acțiunile nefavorabile ale combinațiilor ultime (acțiunile permanente cu γG,sup, cele variabile cu γQ, în 6.10 sau în 6.10a și 6.10b); acțiunile permanente favorabile ale ridicării din vânt își păstrează γG,inf, iar combinațiile de serviciu nu se schimbă. **Niciuna (γd 1)** lasă coeficienții așa cum sunt. Când setul dă ψ al zăpezii după s0,k, îl alege s0,k al amplasamentului (Zăpadă și vânt), iar notele spun care.
- **γG,sup, γG,inf, γQ.** Fiecare câmp arată valoarea în vigoare. Scrie valoarea ta și câmpul e marcat cu **Resetează**, care readuce valoarea setului.
- **Expresia.** **6.10** (implicit) sau **6.10a și 6.10b**: două expresii, a doua cu ξ (0,85 recomandat, între 0,5 și 1) pe acțiunile permanente. CR 0-2012 are doar expresia ei 6.9 (6.10 din EN), așa că 6.10a și 6.10b rămân alegerea ta.
- **ψ pe categorii.** Deschide tabelul ca să dai ψ0, ψ1 și ψ2 pentru o categorie (categoriile încărcărilor utile, zăpada, vântul); un rând gol ia valorile setului. ψ-ul propriu al unui caz (în fila Cazuri) are prioritate față de cel al categoriei.

![Coeficienții proiectului](shots/loads-combination-factors.webp)

Fiecare schimbare e un pas de undo; rulează din nou calculul ca să vezi efectul.

### Oprirea unei combinații

Caseta **Pornit** de la începutul unui rând oprește sau pornește o combinație. O combinație oprită rămâne în listă, estompată, dar nu participă la rezultate, la înfășurători, la verificări și la export; notele spun câte sunt oprite.

### Combinațiile tale

**Adaugă o combinație** cere un nume, un **fel** și un coeficient pentru fiecare caz de încărcare; un câmp gol lasă cazul deoparte. Felul spune unde merge combinația:

- **Ultimă (SLU)** și **Accidentală**: înfășurătoarea ultimă și verificările (una accidentală se verifică cu γM al situației persistente, în favoarea siguranței).
- **Caracteristică**, **Frecventă** și **Cvasipermanentă (SLS)**: înfășurătoarea de serviciu; cea caracteristică și verificările de săgeată.
- **Seismică (cu AEd)**: coeficienții ei ± AEd, în înfășurătoarele seismică și ultimă, după ce sunt dați parametrii seismici.

În rândul ei o poți redenumi, îi poți schimba felul, îi poți modifica coeficienții (creionul) sau o poți șterge. Dacă un caz de încărcare pe care îl numește a fost șters, rândul arată **Numește un caz de încărcare care nu mai există**, iar combinația rămâne în afara rezultatelor până îi dai din nou coeficienții.

## Încărcări dintr-un tabel

**Importă încărcări…** și **Importă cazuri de încărcare…** (în grup și în dialogul Încărcări) citesc încărcări și cazuri dintr-un fișier CSV sau Excel, ori din celule lipite dintr-un tabel: câte o coloană pentru fel, caz, nivel, coordonate, valoare și unitatea ei. Lapin ghicește coloanele, citește unități ca *4,5 kN/m* sau *2,5 kPa*, arată fiecare rând înainte de import, iar importul întreg este un singur pas de anulat. Vezi [Importul tabelelor](tables.md).

## Calculul

**Rulează calculul** (în același grup și în dialogul Încărcări) calculează cadrul sub aceste încărcări: vezi [Calcul structural](analysis.md).

## În fișierul IFC

**Exportă IFC, Fragments sau BOS…** are **Structural Analysis View (model analitic și încărcări)**: fișierul poartă atunci modelul analitic al pereților portanți, planșeelor, stâlpilor și grinzilor, cu cazurile de încărcare, încărcările și combinațiile, ca IFC Structural Analysis View. Vezi [Import și export](import-export.md).
