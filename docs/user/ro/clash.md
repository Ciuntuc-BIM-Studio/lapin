---
title: Detectarea coliziunilor
order: 58
summary: Găsește unde se intersectează elementele modelului sau ajung prea aproape: seturi de căutare după reguli, teste dure și de distanță între ele, coliziuni urmărite de la o rulare la alta, arătate în 3D și trimise ca BCF.
---

**Gestionare → Detectarea coliziunilor** găsește unde elementele modelului se intersectează sau ajung mai aproape decât ar trebui. Lucrează la fel pe elementele Lapin și pe fișierele IFC și Fragments legate, deci coordonează și modelele pe care ți le trimit celelalte specialități.

![Detectarea coliziunilor, lângă vederea 3D](shots/clash-dialog.webp)

## Teste și seturi

Un **test de coliziune** compară două **seturi de căutare**: setul A cu setul B sau un set cu el însuși. Un proiect pornește cu trei seturi (Structure, Services, Architecture) și trei teste dure: Structure × Services, Services × Services și Architecture × Services.

- Testele **dure** găsesc elementele care se intersectează. **Toleranța** lor (implicit 5 mm) ignoră intersecțiile atât de mici, ca un finisaj așezat 3 mm în perete să nu fie raportat.
- Testele **de distanță** găsesc elementele mai apropiate decât o distanță (implicit 50 mm), și intersecțiile.

Unele perechi nu se testează niciodată, pentru că trebuie să se întâlnească: un perete și ușa sau fereastra din golul lui, părțile aceluiași ansamblu, pereții îmbinați la colț și elementele prinse unul de altul (o grindă așezată pe un stâlp, un perete atașat de acoperiș).

![Setările unui test](shots/clash-test.webp)

## Rulare și urmărire

**Rulează testul** rulează testul din față; **Rulează tot** rulează toate testele pornite. Nimic nu rulează singur: rezultatele rămân în proiect până rulezi din nou testul, iar dialogul spune când modelul s-a schimbat între timp.

Fiecare coliziune își păstrează numărul de la o rulare la alta. O coliziune găsită prima dată e **Nouă**; găsită din nou, e **Activă**. O marchezi de mână **Verificată** sau **Aprobată**, o dai cuiva (**Atribuit lui**) și scrii **Note**. O coliziune pe care o rulare nu o mai găsește devine **Rezolvată**; dacă revine, e deschisă din nou, cu același număr.

Clic pe o coliziune ca s-o vezi în 3D. Lapin selectează cele două elemente, pune o cutie de secțiune în jurul locului unde se întâlnesc și le privește dintr-o parte, ca să vezi conducta prin perete, nu fața peretelui. **singure în 3D** ascunde restul; săgețile trec prin coliziuni una câte una. Dialogul stă în dreapta, așa că poți mișca camera cât e deschis.

![O conductă prin perete, încadrată de coliziune](shots/clash-3d.webp)

**BCF** face din fiecare coliziune deschisă (sau din cele selectate) o observație de tip Clash, cu punctul ei de vedere, și le salvează ca fișier BCF pentru alte programe BIM; trimise din nou, aceleași observații se actualizează. **CSV** salvează lista pentru un tabel.

## Seturi de căutare

**Seturi de căutare** ține regulile care aleg elementele unui set. O regulă citește un câmp al elementului (clasa, tipul predefinit, tipul, nivelul, materialul, numele, fișierul legat sau orice parametru) cu un operator: este, este unul din, nu este, nu este niciunul din, conține, nu conține, cel puțin, cel mult, între. Numerele se citesc cum le scriu oamenii: „600 mm” e 600.

Un set ia elementele care îndeplinesc oricare regulă sau toate regulile. O regulă „nu” scoate întotdeauna elemente, iar un set fără nicio regulă care adaugă elemente le ia pe toate. Numărul de lângă set arată câte elemente iau regulile, pe măsură ce scrii.

![Regulile unui set de căutare](shots/clash-sets.webp)

## Grupe, rapoarte și nori

Lista grupează coliziunile **pe niveluri**, **pe elemente** (elementul din cele mai multe coliziuni le ia, apoi următorul), **pe clase** sau **pe stări**. Bifa unei grupe îi selectează toate coliziunile, ca să le dai o stare odată.

![Coliziuni grupate pe elemente](shots/clash-groups.webp)

- **XML** scrie coliziunile cum își scrie Navisworks rapoartele, pe grupe, ca să le citească alte programe de coordonare.
- **PDF** tipărește lista pe A4, grupată la fel.
- **HTML** face o pagină cu o imagine a fiecărei coliziuni deschise, încadrată pe rând în vederea 3D.
- **Marchează în vedere** desenează coliziunile deschise în planul, secțiunea sau fațada din față ca nori roșii cu eticheta test.coliziune (2.14: al doilea test, coliziunea 14), doar pe ecran. Un clic pe nor selectează elementul.

![Coliziuni ca nori într-un plan](shots/clash-marks.webp)

Un proiect pornește și cu al patrulea test, **Openings for MEP**: verificarea din Gestionare → Verificare coliziuni… rulată ca test, ca problemele ei să fie numerotate și urmărite ca celelalte. Adâncimea e cât îi lipsește traseului din gol.

Un test dur găsește și un element cuprins întreg în altul, unde nicio față nu se intersectează: o conductă uitată într-o placă. Adâncimea e cea mai mică lățime a elementului din interior.

> Traseele de instalații Lapin își pot tăia singure golurile în pereți și planșee (Gestionare → Ordinea de tăiere). Atunci o conductă prin perete nu e o coliziune: peretele are golul lui. **Gestionare → Verificare coliziuni…** verifică acele goluri.

Detectarea coliziunilor urmează proiectarea, cifrele și testele lui [BimGo](https://github.com/aussieBIMguru/BimGo), de Aussie BIM Guru (MIT-0), rescrise în Lapin.
