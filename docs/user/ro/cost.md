---
title: Devizul
order: 98
summary: Devizul din model: listele tale de prețuri importate din CSV sau Excel, articole pe tipuri și reguli pe materiale, cantitățile din antemăsurătoare înmulțite cu prețurile unitare defalcate pe material, manoperă, utilaj și transport, cheltuieli indirecte, profit și TVA, în lei sau euro; formularele F3, F2 și C6–C9 pe planșe, în Excel și PDF; devizul în IFC.
---

## Tabul Cost

![Tabul Cost](shots/ribbon-cost.webp)

Tabul care se numea **Cantități** se numește acum **Cost**. Păstrează cantitățile și adaugă devizul:

- **Antemăsurătoare**, **Extrase…** și **Export XLSX**: cantitățile modelului (vezi [Extrase și grafice](schedules.md)).
- **Liste de prețuri…**: prețurile tale, importate dintr-un fișier CSV sau Excel (mai jos).
- **Atribuie articole…**: ce articol prețuiește fiecare tip și fiecare material (mai jos).
- **Deviz…**: deschide devizul sub vedere (prima dată îl și face).
- **Formular pe planșă**, **Deviz XLSX** și **Deviz PDF**: devizul și formularele lui pe o planșă, într-un registru de calcul sau într-un PDF (mai jos).

Devizul se calculează din model la fiecare schimbare. Un perete pe care îl desenezi acum intră imediat în deviz, cu cantitățile lui. Se păstrează doar prețurile, articolele de pe tipuri, regulile și rândurile tale.

## Listele de prețuri

![Importul unei liste de prețuri: sensul fiecărei coloane ghicit după antet](shots/price-import.webp)

Prețurile sunt ale tale: Lapin nu livrează niciun catalog de prețuri sau de norme. **Cost → Liste de prețuri… → Import…** citește un fișier CSV sau XLSX, de exemplu un export din programul tău de devize sau lista unui furnizor. Lapin arată primele rânduri ale fișierului sub antetele lor. Deasupra fiecărei coloane e ce a ghicit Lapin că ține, după antet, în română sau în engleză:

- **Cod** și **Denumire** (*Cod articol*, *Simbol*, *Code*, *Description*), **Unitate** (*U.M.*: mc sau m³, mp sau m², m, buc, kg, t, 100 mp).
- Prețul unitar defalcat pe **Material**, **Manoperă**, **Utilaj** și **Transport**, sau un singur **Preț unitar (nedefalcat)**, care se socotește atunci material.
- Opțional: **Ore-om pe unitate** (*Normă de timp*) și **Meserie**, pentru lista de manoperă (C6).
- Opțional: resursele articolului, rânduri care numesc articolul căruia îi aparțin (**Articolul căruia îi aparține**), cu **Tipul resursei** (M, Man, U, T) și **Consumul pe unitate**. Un articol dat doar prin resursele lui e prețuit după ele, iar listele de resurse (C7–C9) le arată una câte una.

Schimbă coloanele ghicite greșit, alege rândul pe care e antetul și dacă prețurile sunt în lei sau în euro, apoi apasă **Import**. Lista intră în proiect. Cu **Păstrează și în biblioteca acestui calculator** bifat, rămâne și pe calculator: sub **Pe acest calculator**, **Adaugă în proiect** o aduce în proiectul următor.

**Șablon** salvează un CSV cu coloanele și câteva rânduri cu valori orientative scrise de Lapin, marcate *orientativ*: completează-l cu prețurile tale și importă-l. **Folosește valorile orientative** pune aceste valori în proiect ca listă, ca să încerci devizul înainte să ai prețurile tale. Înlocuiește-le înainte să trimiți un deviz cuiva.

## Articole pe tipuri, reguli pe materiale

![Atribuie articole: fiecare tip și material cu articolul lui](shots/map-articles.webp)

Un rând de deviz vine dintr-un **articol**: un cod din lista ta de prețuri. Elementele primesc articole în două feluri:

- **Pe tip.** Fiecare element al tipului e prețuit după el. Articolul spune ce măsoară: elementul întreg sau o parte din el (**miezul** unui tip stratificat sau **finisajele** lui) și după ce măsură (după unitatea prețului, volum net sau brut, arie netă sau brută, lungime, bucăți, masă). Poate adăuga un coeficient și pierderile (%).
- **Pe material (o regulă).** Fiecare strat din acel material, în orice tip, e prețuit după ea: o singură regulă pentru tencuiala de var prețuiește tencuiala tuturor pereților.

Articolul de pe tip are întâietate. Un strat pe care articolele tipului îl măsoară deja nu mai e prețuit de o regulă. Un articol pe elementul întreg îl prețuiește tot, deci regulile se dau la o parte. Pentru un perete stratificat, dă tipului un articol pe **miez** (zidăria, la volum) și reguli pentru finisaje (tencuiala, la arie). Peretele e atunci prețuit strat cu strat: zidăria la **Pereți**, tencuiala la **Finisaje și izolații**.

**Cost → Atribuie articole…** listează tipurile proiectului pe care le folosesc elementele și materialele straturilor lor. Fiecare rând pornește cu articolul pe care îl are, sau cu cel ales pentru un tip sau material cu același nume într-un proiect anterior, sau cu cel sugerat de Lapin: un cod scris în numele lui, sau articolul cu cele mai multe cuvinte comune (*zidărie*, *BCA*, *tencuială*, *C25/30*). Pentru un tip stratificat propune miezul. Scrie sau alege un cod, alege partea și măsura, bifează rândurile dorite și apasă **Aplică**: un singur pas de anulat. Alegerile tale sunt ținute minte pe acest calculator pentru proiectele următoare.

Tipurile de cofraj și de schelă din tabul **Organizare de șantier** aveau deja un cod de articol (decizia 152): el e un articol pe tip, măsurat după unitatea prețului.

## Devizul

![Devizul: capitole, rânduri, cotele și totalurile](shots/estimate.webp)

**Cost → Deviz…** deschide devizul sub vedere, ca Gantt-ul. Rândurile lui sunt grupate în **capitole** pe categorii de lucrări: terasamente, fundații, structură, pereți, acoperiș, uși și ferestre, finisaje și izolații, instalații, organizare de șantier, altele. Un articol poate numi un capitol al lui. **Pe etaje și categorii de lucrări** pune etajul înaintea fiecărui capitol.

Fiecare rând arată codul, denumirea și unitatea din lista de prețuri, **cantitatea** din model (aceeași ca în antemăsurătoare), prețul unitar defalcat pe **Material**, **Manoperă**, **Utilaj** și **Transport**, prețul unitar și **valoarea**. Un clic pe rând îi selectează elementele în vederi. Selectează elemente într-o vedere și rândurile lor se marchează în deviz.

Sub rânduri sunt totalurile, cum le așază devizele românești:

1. **Cheltuieli directe**: suma rândurilor.
2. **Contribuția asiguratorie pentru muncă (CAM)**: 2,25 % din manoperă, implicit.
3. **Cheltuieli indirecte**: 10 % din cheltuielile directe cu CAM, implicit.
4. **Profit**: 5 % din cheltuielile directe și indirecte, implicit.
5. **Total fără TVA**, **TVA** (21 % implicit) și **Total cu TVA**.

Orice cotă se schimbă în bara de unelte. Fiecare valoare e rotunjită la două zecimale, cum se tipărește un deviz.

Devizul e în **RON** sau **EUR**. **1 EUR =** e cursul proiectului: o listă de prețuri în cealaltă monedă e convertită la el, iar căsuța de lângă arată fiecare valoare și în cealaltă monedă.

Bara de unelte arată și ce nu e prețuit: **N tip(uri) fără preț** (un clic deschide Atribuie articole) și codurile pe care nu le are nicio listă. **Un rând al tău** adaugă o lucrare care nu e în model (împrejmuirea șantierului, o provizie): scrie-i codul, denumirea, unitatea, cantitatea și prețurile. Un rând cu cod și fără prețuri le ia din lista de prețuri.

## Formularele

![Lista cantităților de lucrări (F3) a aceluiași deviz](shots/estimate-f3.webp)

Lista de deasupra devizului arată devizul sau unul din formularele lui. Sunt grupări ale aceluiași deviz, deci dau aceleași totaluri:

- **Lista cantităților de lucrări (F3)**: *Lista cu cantitățile de lucrări pe categorii de lucrări*, capitol cu capitol, prețurile unitare și valorile defalcate pe material, manoperă, utilaj și transport, cu totalurile dedesubt.
- **Deviz pe obiect (F2)**: valoarea fiecărui capitol fără TVA, TVA-ul și valoarea cu TVA.
- **Manoperă (C6)**: orele și valoarea pe meserii (din meseria și normele de timp ale listei de prețuri, sau din resursele de manoperă).
- **Materiale (C7)**, **Utilaje (C8)** și **Transport (C9)**: resursele cu cantitățile, prețurile unitare și valorile lor. Un articol fără resurse se socotește o resursă în sine.

Formularele urmează practica obișnuită după metodologia Ordinului 863/2008. Verifică-le cu ce cere beneficiarul sau programul de finanțare: spune-ne dacă ai nevoie de altă machetă.

**Pune pe planșă** așază tabelul arătat pe o planșă nouă, gata de tipărit cu celelalte. **XLSX** salvează devizul și toate formularele într-un registru de calcul, câte o foaie fiecare. **PDF** le salvează ca PDF, A4 orizontal, gata de tipărit.

## IFC

**Fișier → Export IFC…** scrie devizul ca **IfcCostSchedule**: capitolele și rândurile ca **IfcCostItem**, cantitatea fiecărui rând și prețurile lui unitare ca patru **IfcCostValue** (Material, Labour, Equipment, Transport), elementele pe care le măsoară fiecare rând atribuite lui (**IfcRelAssignsToControl**), moneda ca **IfcMonetaryUnit** al proiectului. La import, un deviz își păstrează rândurile cum le are fișierul, cu aceleași totaluri. Bifează **Rânduri din model** ca să adaugi rândurile pe care le dă modelul tău.
