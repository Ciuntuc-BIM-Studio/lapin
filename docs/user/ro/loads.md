---
title: Încărcări
order: 51
summary: Cazuri de încărcare, încărcări pe suprafață, liniare și punctuale, încărcările utile ale încăperilor, încărcările derivate și combinațiile EN 1990, pentru un calcul structural.
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
- **Finisajele planșeelor**, din straturile lor, și **pereții neportanți**, ca încărcări liniare, în cazul care enumeră finisajele și pereții despărțitori (G2).
- **Încăperile**: încărcarea utilă a categoriei unei încăperi pe aria ei, în cazul acelei categorii (Q-A, Q-B…).
- **Zăpada** și **vântul**, de îndată ce dai valorile amplasamentului (vezi [Zăpadă și vânt](#zăpadă-și-vânt)).

Un material fără densitate e lăsat pe dinafară, iar Rezumatul spune care.

## Încărcarea utilă a unei încăperi

Selectează o încăpere: proprietățile ei au o secțiune **Încărcare utilă**. **Categoria** ei e **Din funcție** (un living, un dormitor sau o bucătărie e A, un birou B, un depozit E1, un garaj F) dacă nu alegi alta, sau **None**. Dedesubt e **qk** dat de categorie.

![Încărcarea utilă a unei încăperi în proprietățile ei](shots/loads-room.webp)

## Tabelul încărcărilor utile

Fila **Încărcări utile** enumeră fiecare categorie cu folosința ei și **qk** în kN/m², cum recomandă EN 1991-1-1. Scrie în câmp valoarea anexei tale naționale; **Modificat** revine la valoarea implicită.

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

Fila **Combinații** enumeră combinațiile cazurilor, după EN 1990, cele recomandate, orientative: pentru starea limită ultimă 1,35 pe acțiunile permanente și 1,5 pe cea variabilă conducătoare, celelalte cu ψ0 al lor; pentru starea limită de serviciu cele caracteristice, frecvente și cvasipermanente. Un caz care nu conține nimic nu participă.

![Fila Combinații](shots/loads-combinations.webp)

## Calculul

**Rulează calculul** (în același grup și în dialogul Încărcări) calculează cadrul sub aceste încărcări: vezi [Calcul structural](analysis.md).

## În fișierul IFC

**Exportă IFC, Fragments sau BOS…** are **Structural Analysis View (model analitic și încărcări)**: fișierul poartă atunci modelul analitic al pereților portanți, planșeelor, stâlpilor și grinzilor, cu cazurile de încărcare, încărcările și combinațiile, ca IFC Structural Analysis View. Vezi [Import și export](import-export.md).
