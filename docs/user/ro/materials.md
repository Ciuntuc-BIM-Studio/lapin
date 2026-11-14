---
title: Materiale, texturi și hașuri
order: 90
summary: Setezi culoarea, hașurile și rosturile fiecărui material, pui pe el o imagine proprie și îți aduci propriile hașuri .pat.
---

**Gestionare → Materiale** listează materialele proiectului. Alege unul ca să-l modifici.

![Fereastra Materiale](shots/materials-dialog.webp)

## Aspect: cum se randează un material

Fila **Aspect** setează cum arată materialul în 3D și în programele de randare în care exporți. Fila **Grafică 2D** păstrează hașurile și penițele, ca înainte.

![Aspectul unui material, cu previzualizarea lui](shots/material-appearance.webp)

- **Culoare** și **Textură**: o textură desenată (cărămidă, scânduri de lemn, plăci…) sau imaginea ta. **W** și **H** spun cât de mare stă o placă pe model, în metri; **Rotirea** o întoarce.
- **Rugozitate**: 0 e o oglindă, 1 e cretă. **Metalicitate**: 1 pentru metal neacoperit (aluminiu eloxat, oțel, crom), 0 pentru tot ce e vopsit sau vopsit în câmp electrostatic, lemn și piatră.
- **Transparență**: cât se vede prin el. **Sticlă (transmisie)** și **IOR** (1,52 pentru sticlă) fac sticla să refracte într-un program de randare.
- **Strălucește** emite lumină, ca difuzorul unei lămpi.

Sfera arată rezultatul imediat. Ce nu setezi vine din nume: un material numit *sticlă* e transparent, *oțel* e metalic, *parchet* și *laminat* sunt scânduri de lemn. **Aspectul din nume** scoate din nou valorile tale.

## Din bibliotecă

**Din bibliotecă…** oferă materiale gata făcute: metale (aluminiu RAL 7016 antracit, eloxat, oțel, cupru…), sticlă (clară, albastră, verde, gri, mată, reflectorizantă), lemn în scânduri, beton, cărămidă, tencuială și vopsea, plăci și piatră, plastic și textile. Un clic adaugă unul în proiect; îl schimbi ca pe oricare altul.

![Biblioteca de materiale](shots/material-library.webp)

## Date: descriere, parametri, cum se măsoară

Tabul **Date** ține ce spune materialul despre el. Îl scrii o dată; îl citește fiecare tip în care e materialul: eticheta de stratificație, antemăsurătoarea, devizul, exportul IFC.

![Datele unui material: descrierea, parametrii, cum se măsoară și grosimile din catalog](shots/material-data.webp)

- **Descriere**: textul de specificație, pe mai multe rânduri (*Plăci de vată minerală, λ = 0,035 W/mK, prinse cu dibluri*). O etichetă de stratificație îl scrie în locul numelui.
- **Parametri**: un nume, un fel și o valoare, ca la elemente. **Adaugă unul obișnuit…** oferă **Conductivitate termică λ**, **Rezistență la vapori μ** și **Densitate**, care merg în IFC cu numele lor standard (*Pset_MaterialThermal.ThermalConductivity*, *Pset_MaterialHygroscopic.UpperVaporResistanceFactor*, *Pset_MaterialCommon.MassDensity*, în unități SI), și **Reacția la foc** și **Rezistență la compresiune**, care rămân ale tale. Parametrii tăi merg în IFC ca *Lapin_Material*.
- **Măsurat după** spune cum îl numără **Execuție → Antemăsurătoare** (pe material) și devizul: **arie** (m², un rând pe grosime: plăci, termoizolații, membrane), **volum** (m³: beton, balast, șapă), **masă** (kg, din densitate) sau **bucăți** (plăci de mărimea pe care o dai). Lăsat gol, se măsoară cum se măsoară elementele lui.
- **Grosimi din catalog (mm)**: grosimile în care îl face producătorul, de exemplu *50, 80, 100, 150, 200, 240*. Tabelele de straturi din **Tipuri de pereți** și **Tipuri de plăci** le oferă lângă grosime (poți scrie și alta), iar un strat de altă grosime e marcat cu un semn de avertizare.

Exportul IFC scrie descrierea ca Description a materialului și parametrii ca seturile lui de proprietăți; importul le citește înapoi, și din fișierul altui program.

## Un singur material pentru toate tocurile și toată sticla

Ușile și ferestrele noi sunt făcute din materiale comune, parte cu parte: **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** și **Hardware**. Schimbi **Window frame** o dată și se schimbă tocul tuturor ferestrelor.

Ca să folosești alte materiale, deschide **Arhitectură → Uși și ferestre**, alege **Materialul tocului**, **Materialul sticlei** și celelalte pentru un tip și apasă **Folosește pentru toate ferestrele** (sau **Folosește pentru toate ușile**). Toate tipurile de acel fel le primesc, într-un singur pas de anulare. Pereții cortină au și ei un material de cadru și unul de sticlă.

![Materialele părților în configuratorul de uși și ferestre](shots/joinery-materials.webp)

## Duplică, înlocuiește, selectează

- **Duplică** copiază un material sub un nume nou.
- **Înlocuiește cu** un material, apoi **Înlocuiește**: fiecare strat, tip, ușă, fereastră și element care îl folosea pe primul îl folosește acum pe al doilea, într-un singur pas de anulare. **Înlocuiește și șterge** îl scoate și pe primul. Altfel, un material folosit nu se poate șterge.
- **Selectează elementele** selectează toate elementele care au o parte din acel material.
- **Caută materiale** și lista categoriilor filtrează lista.

## Materiale pentru randare

Twinmotion, Lumion, Enscape, Blender și alte programe de randare înlocuiesc materialele după nume. Lapin scrie fiecare material **o singură dată, sub numele lui**, cu culoarea, textura, rugozitatea, metalicitatea și sticla lui, și împarte plasa fiecărui element pe materiale.

1. Numește materialele cum le vrei în programul de randare, de exemplu *Aluminium RAL 7016 anthracite* pentru toate tocurile și *Glass blue tinted* pentru toată sticla.
2. **Fișier → Export 3D (glTF, USD, DAE)…** și alege **GLB** (recomandat: un fișier cu texturile, numele exacte și sticla), **USDZ** sau **OBJ + MTL** (un .zip cu texturile; spațiile din nume devin liniuțe jos).
3. Importă fișierul în programul de randare. Toate tocurile au acolo un singur material; îl înlocuiești o dată cu metalul programului și se schimbă toate.

![O casă cu tocuri gri închis și sticlă albastră](shots/materials-render-3d.webp)

Și exportul IFC numește stilul de suprafață al fiecărui material după el, cu transparența și felul în care reflectă. Ușile și ferestrele duc și materialul fiecărei părți: un set de constituenți de material numiți **Frame** (tocul), **Sash** (cerceveaua), **Glazing** (sticla), **Panel** (foaia), **Board** (glaful) și **Hardware** (feroneria), pe fiecare ușă și fereastră și pe tipul ei, așa că un vizualizator sau un program de randare care citește IFC-ul găsește metalul tocului și sticla pe părți. Când imporți un astfel de fișier (din Lapin sau din alt program care scrie **Lining**, **Framing** și **Glazing** din IFC4), tipurile de uși și ferestre primesc din nou acele materiale.

Un fișier IFC din alt program își aduce sticla transparentă: transparența stilului de suprafață al unui material, sau a stilului de pe un corp, devine **Transparența** materialului (stilul unui corp dă părții lui un material numit după stil). Fișierul importat nu se schimbă niciodată.

## Ce setează un material

- **Culoare (3D)** și fețele desenate pe el în 3D. Lapin le ghicește din nume, de exemplu rândurile unei cărămizi sau rosturile unei plăci de faianță. **Capete** și **Sus și jos** pot arăta altfel decât fețele.
- **Hașura** pentru fiecare loc în care se desenează: secționat într-o secțiune, secționat într-un plan, și văzut într-o fațadă. Fiecare are scara, unghiul, alinierea, umplutura și penița ei. Până nu i-o dai, o tăietură ia modelul pe care îl sugerează numele ei, ca materialele implicite din Revit: cărămidă, zidărie și piatră linii la 45° (MASONRY), blocuri de beton și beton celular o hașură încrucișată în diagonală (BLOCK), beton, turnat pe loc sau armat, punctat (CONCRETE), izolație rigidă (EPS, XPS, PIR) o grilă fină pătrată (RIGID), vată minerală BATT, gips și tencuială un punctat (GYPSUM), nisip unul des (SAND), lemn WOOD, placaj, OSB și CLT WOODCROSS, oțel STEEL, pământ EARTH, pietriș GRAVEL, plăcile și țiglele TILES; sticlă, aer, vopsea, membrane și orice alt nume nimic. Rândurile cărămizii sunt ale unei fațade. Un proiect făcut înainte păstrează hașurile pe care le avea. O fațadă sau o secțiune hașurează fețele pe care le vede cu hașura de fațadă, unde nimic mai apropiat nu le ascunde (Vederi, Hașurile suprafețelor); până nu i-o dai, numele îi propune una (cărămidă BRICK, piatră STONE, lemn BOARDS, placare metalică VERTICAL), niciodată BATT-ul izolației.
- **Categoria**, de exemplu finisaj, izolație sau structură. Stilurile de vedere o folosesc ca să lase finisajele afară dintr-o vedere de structură.
- **Membrană** desenează un strat subțire ca linie întreruptă în loc de bandă.
- **Desenează rosturile.** Implicit, un material trece de la un element la următorul fără linie, ca doi pereți în prelungire sau o tencuială peste două etaje. Bifează ca să se deseneze rostul.
- **Se îmbină cu alte elemente.** Bifat implicit: elementele din acest material se îmbină unde întâlnesc altele. Pereții se îmbină la colțuri și grinzile în stâlpi, ordinea de tăiere lasă un element să taie altul (un stâlp printr-o grindă, o grindă în capul unui perete), iar un desen unește într-o singură suprafață tăieturile elementelor alăturate din același material. Debifează pentru elementele care trebuie să-și păstreze forma, ca grinzile de oțel sau fermele de lemn. Elementele lor nu se mai îmbină automat, nimic nu le taie și nu taie nimic, iar planurile și secțiunile desenează conturul fiecăruia. O grindă se oprește la fețele stâlpilor în care stau capetele ei, dar trece întreagă printr-un stâlp pe care îl traversează. Debifarea scoate îmbinările pereților, grinzilor și fundațiilor continue făcute în principal din acest material; bifarea din nou le îmbină cu ce întâlnesc. Le poți anula pe oricare într-un singur pas. Materialele de oțel și lemn din bibliotecă se îmbină implicit, ca până acum.

## O imagine proprie pe un material

Un material poate purta o imagine a ta: o fotografie a cărămizii, a lemnului sau a pietrei adevărate.

1. Alege materialul și apasă **Imagine proprie → Importă o imagine…**. Alege un fișier PNG sau JPEG.
2. Setează **W** și **H**: cât de lată și cât de înaltă, în metri, stă o copie a imaginii pe model. Imaginea se repetă pe fețe.

![Imaginea proprie a unui material, pusă la 1,2 × 1,2 m](shots/own-image.webp)

Imaginea se vede în vederea 3D și e salvată în fișierul proiectului. Merge și în exporturile 3D: **GLB**, **glTF** și **USDZ** o duc cu ele. **DAE** și **USDA** sunt fișiere unice, fără loc pentru imagini, așa că păstrează culoarea materialului.

![Aceeași casă cu imaginea proprie a materialului în 3D](shots/own-image-3d.webp)

Imaginile mai mari de 2048 de pixeli se micșorează la această mărime când le imporți. Butonul cu coș de gunoi scoate imaginea de pe material.

## Hărți de normale și de relief

Sub imaginea proprie, un material poate primi două hărți de relief, fiecare un PNG sau JPEG al tău:

- **Hartă de normale**: imaginea mov-albăstruie pe care programele de randare și site-urile de texturi o dau cu un material (spațiul tangent, verde în sus, cum o citește glTF). Fețele se umbresc ca și cum ar avea relieful ei: rosturile unei cărămizi, fibra unui lemn, asperitățile unei pietre.
- **Hartă de relief**: o imagine gri, albul sus și negrul jos. Fețele se umbresc ca și cum ar fi ridicate unde e deschisă.

Apasă **Importă o imagine…** lângă una și alege fișierul. Amândouă stau pe model ca imaginea proprie, la **W** × **H** și rotite cu **Rotire**; prima hartă de relief pe un material fără mărime îi dă una de 1 m lățime, înaltă cât proporția imaginii. **Intensitate** spune cât de tare se vede relieful: 1 cum spune imaginea, 0 plat, până la 10. Sfera de previzualizare și vederea 3D le arată pe amândouă, iar butonul cu coș de gunoi scoate una.

În exporturi:

- **GLB** și **glTF** duc harta de normale ca textura de normale a materialului, la intensitatea ei.
- **USDZ** împachetează harta de normale lângă model și o leagă de normala suprafeței.
- **OBJ + MTL** scrie harta de normale ca `norm` și harta de relief ca `map_Bump` cu intensitatea ei (`-bm`). Un material cu hartă de normale și fără hartă de relief scrie harta de normale și ca `map_Bump`, unde o citesc Blender și majoritatea programelor de randare.
- glTF nu are hartă de relief, așa că GLB, glTF și USD o lasă afară; **DAE** și **USDA** nu duc deloc imagini, așa că acolo nu merge niciuna.

## Hașurile tale (.pat)

**Importă .pat** citește un fișier de hașuri AutoCAD. Fiecare model din el intră în proiect, gata să fie ales ca hașură a unui material.

Lapin păstrează fișierul și în **Modelele mele**, pe calculatorul tău. În orice alt proiect, **Adaugă în acest proiect** aduce din nou acele modele. Un proiect își păstrează modelele folosite, așa că se deschide la fel pe alt calculator.

![Modelele mele: fișierele .pat importate, păstrate pentru orice proiect](shots/my-patterns.webp)
