---
title: Uși și ferestre
order: 30
summary: Pui uși și ferestre în pereți, le muți și le proiectezi tipurile în configurator.
---

<!-- video doors-windows -->

## Cum pui o ușă sau o fereastră

Alege **Arhitectură → Ușă** (<kbd>DR</kbd>) sau **Fereastră** (<kbd>WN</kbd>), apoi dă clic pe perete unde vrei golul. Lapin taie golul în toate straturile peretelui și adaugă buiandrugul cerut de tipul peretelui.

Cât timp Door sau Window e activă, panoul de proprietăți arată tipul pe care îl va pune, cu dimensiunile lui: alege altul acolo înainte să dai clic pe perete.

![Un plan cu o ușă și ferestre](shots/openings-plan.webp)

Ca să muți o ușă sau o fereastră de-a lungul peretelui, selectează-o și trage de ea. Ca s-o schimbi, folosește panoul de proprietăți: tipul, înălțimea parapetului și partea în care se deschide. Cu mai multe uși și ferestre selectate, **Balamale**, **Parte**, parapetul și tipul le schimbă pe toate dintr-un pas, la fel și o săgeată de întoarcere din plan.

![Proprietățile unei uși](shots/door-properties.webp)

## Goluri fără ușă sau fereastră

**Architecture → Wall opening** (<kbd>WO</kbd>) taie un gol simplu prin perete acolo unde dai clic: o trecere, un chepeng, un gol pentru o ușă care vine mai târziu. Lățimea, înălțimea și parapetul le pui în panoul de proprietăți înainte de clic. Selectat, i se schimbă acolo dimensiunile, locul de-a lungul peretelui și parapetul; ca o ușă, trebuie să încapă în perete și să nu atingă celelalte goluri ale lui. În IFC e un gol care taie peretele, fără nimic care să-l umple.

**Forma de sus** dă forma capului: **Dreptunghi**, **Arc de segment** (un arc turtit), **Arc eliptic** (o jumătate de elipsă, înaltă cât un sfert din lățime dacă nu spui altfel), **Semicerc** sau **Colțuri rotunjite** (un cap drept cu colțurile de sus rotunjite). **Înălțimea arcului** spune cât se ridică un arc de segment sau eliptic, **Raza colțurilor** cât de rotunde sunt colțurile (150 mm dacă nu spui altfel, niciodată mai mult de jumătate din lățime); goală, cea a formei. Golul din perete urmează conturul în 3D, în fațade și secțiuni și în IFC; planul îi arată spaletele ca înainte.

## Configuratorul

**Arhitectură → Uși și ferestre** deschide configuratorul. În stânga sunt toate tipurile de uși și ferestre din proiect. Poți schimba:

- numele tipului și marca lui (litera din etichete);
- lățimea și înălțimea;
- adâncimea și lățimea tocului;
- coloanele și rândurile de panouri, și cum se deschide fiecare panou;
- finisajul glafului, ancadramentul de pe fațadă, pragul și culorile.

Desenele din dreapta arată tipul așa cum va fi desenat în fațadă, plan și secțiune, chiar în timp ce îl schimbi.

![Configuratorul de uși și ferestre](shots/joinery-configurator.webp)

**Salvează tipul** păstrează schimbările. Toate ușile și ferestrele de acel tip le urmează. **Pune cu el** închide configuratorul și începe plasarea acelui tip.

### Ferestre cu mai multe canate

**Coloane de canate** primește lățimile relative ale canatelor, câte un număr pentru fiecare: `1 1` înseamnă două canate egale, `1 1 1` trei, `1 2 1` unul lat între două înguste. **Rânduri de canate** împarte fereastra la fel pe înălțime, de jos în sus: `1 1` dă două rânduri. Șase canate înseamnă `1 1 1` la Coloane de canate și `1 1` la Rânduri de canate. Apoi, în **Canate**, alege cum se deschide fiecare canat: fix, batant, oscilobatant, rabatabil, pe sus, pivotant sau culisant.

### Obloane, grilaje, jardiniere și copertine

<!-- video window-dressings -->

La **Pe fațadă**, un tip de fereastră poate avea:

- **Obloane**: cu lamele, pline sau din scânduri, cu unul sau două canate, arătate deschise (lipite de perete lângă fereastră) sau închise peste ea;
- un **Grilaj de siguranță**: în glaf la interior (implicit, unde se montează de obicei grilajele de siguranță), în glaf la exterior, pe fațadă sau pe fața interioară, cu bare orizontale, verticale sau în ambele direcții, cu grosimea și distanța lor;
- o **Jardinieră** sub fereastră;
- o **Copertină** deasupra, cu ieșirea și panta ei.

Fiecare se așază după fețele reale ale peretelui unde e fereastra, din straturile lui, deci stă corect pe orice tip de perete. Când fereastra are ancadrament, ce e pe fațadă stă în fața ancadramentului. Le poți muta de mână: **De la fața peretelui** dă distanța față de perete (la grilajul din glaf, **Retras de la față** dă cât de adânc stă), **De la axul ferestrei** o mută de-a lungul peretelui, iar **Sus sau jos** o mută pe înălțime. Apar în 3D, în planuri, fațade și secțiuni, iar extrasele le măsoară.

![Obloane, grilaj, jardinieră și copertină în configurator](shots/joinery-dressings.webp)

![Ferestrele cu decorațiuni, în 3D](shots/window-dressings-3d.webp)

### Forme, stiluri și ornamente

**Formă** dă unei uși sau ferestre un cap în arc (**Arc în plin cintru**, **Arc de segment**, **Arc eliptic**, **Colțuri rotunjite**, **Arc frânt**, **Cap triunghiular**) sau o face **Rotundă sau ovală** (rotundă când lățimea și înălțimea sunt egale). Un tip cu arc își păstrează ochiurile sub un montant orizontal la nașterea arcului, iar deasupra are un ochi fix care urmează arcul, cu bare dacă vrei: **Evantai**, **Montanții continuați în sus** sau **Cruce**. **Înălțimea arcului** spune cât se ridică un arc de segment, eliptic, frânt sau triunghiular, **Raza colțurilor** cât de rotunde sunt colțurile unui cap cu colțuri rotunjite. Golul din perete, rama, sticla, ancadramentul și desenele urmează conturul.

**Baghete** împart ochiurile: **Grilă (colonial)** cu ochiurile pe lățime și pe înălțime, **Prairie** cu bare lângă margini, sau **Romburi**.

Pe fațadă, pe lângă ancadrament, o fereastră poate avea **Cheie de boltă**, **Coronament peste gol** (**Cornișă**, **Fronton**, **Fronton în arc** sau **Ancadrament de ploaie**, care urmează un cap în arc) și **Panou sub glaf** cu consolele lui.

**Aplică un stil…** pune deodată forma, baghetele și ornamentele: Modern, Clasic, Georgian, Victorian, Romanic, Baroc, Gotic, Tudor, Prairie, Tradițional sau o fereastră rotundă. Tot ce pune rămâne de modificat.

![O fereastră barocă: arc în plin cintru cu evantai, cheie de boltă și fronton în arc](shots/joinery-styles.webp)

![Ferestre cu arc, în 3D](shots/window-styles-3d.webp)

### Mânere

**Mâner** alege mânerul tipului: **Clanță**, **Buton**, **Bară de tragere**, **Mâner de fereastră** sau niciunul. Implicit, o ușă are clanță, iar o fereastră mâner de fereastră. O ușă are mâner pe ambele fețe, la **Înălțimea mânerului** pe care o dai; **Culoarea mânerului** îi dă culoarea. Mânerele se văd în 3D, în secțiuni și fațade, și în desenele configuratorului când e bifată **Feronerie**.

### Parametri

**Parametri · în extrase**, în partea de jos a configuratorului, ține parametrii proprii ai tipului: **+ Rezistență la foc** (păstrată în Pset_DoorCommon sau Pset_WindowCommon din IFC ca FireRating), **+ Producător**, **+ Cod produs**, **+ Site web** sau orice parametru căruia îi dai un nume. Ușile și ferestrele tipului îi moștenesc, și fiecare devine un rând al extraselor de tâmplărie: vezi [Extrase și grafice](schedules.md).

## Etichete în plan

**Adnotări → Etichete uși și ferestre** pune un marcaj pe fiecare ușă și fereastră dintr-un plan: o linie peste perete, marca în afară, dimensiunile și parapetul înăuntru. Cum arată etichetele se setează în **Gestionare → Standarde de desen → Etichete**.

### Marcajul modificat pe ecran

Fiecare marcaj se poate aranja de mână, parte cu parte. Dă clic pe linia lui sau pe unul dintre texte ca să-l selectezi: fiecare text are atunci un mâner pătrat (trage-l ca să muți textul) și unul rotund după capătul lui (trage-l ca să rotești textul), iar fiecare capăt al liniei are un mâner (trage-l ca să lungești sau să scurtezi linia; textul de la acel capăt merge cu el). Ctrl/⌘-clic pe un mâner pune acea parte cum era. Fiecare tragere e un pas de anulare. Cu mai multe marcaje din același plan selectate, o parte trasă la unul schimbă aceeași parte la toate.

![Marcajul unei uși selectat: câte un mâner pentru fiecare text, mânerul de rotire și câte unul la fiecare capăt al liniei](shots/marker-grips.webp)

![Marca mutată și rotită, capătul exterior al liniei lungit](shots/marker-edited.webp)

Modificările țin de marcaj, deci de acel plan: aceeași ușă poate fi aranjată altfel în alt plan. Sunt păstrate de-a lungul și de-a curmezișul peretelui, așa că urmează ușa când se mută, se rotește sau se întoarce pe partea cealaltă. În proprietățile marcajului, **Marcaj** îi arată părțile: scrie rotirea unui text în grade sau cât trece un capăt, în milimetri pe hârtie, pune o parte la loc cu ↺ sau pe toate cu **Pune eticheta la loc**. Motorul desenează marcajele așa cum sunt modificate, deci planșele, PDF-ul și DXF-ul le tipăresc la fel.

![Părțile unui marcaj în proprietățile lui](shots/marker-properties.webp)

Dacă textele stau de-a curmezișul liniei marcajului (de-a lungul peretelui, implicit) sau de-a lungul ei se alege pentru fiecare tip de etichetă, în **Gestionare → Standarde de desen → Etichete**, coloana **Direcția textului**: **perpendicular pe linia etichetei** sau **de-a lungul liniei etichetei**. Oricum ar fi, sunt desenate citibil, niciodată cu susul în jos; și un text rotit de mână rămâne citibil.

![Textele marcajului ușii de-a lungul liniei etichetei](shots/marker-along.webp)
