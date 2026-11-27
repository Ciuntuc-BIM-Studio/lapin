---
title: Grafica materialelor
order: 91
summary: Setezi cum se hașurează, se umple și se conturează fiecare material în secțiuni, planuri și fațade, la mărime reală și după scară; alegi, editezi și partajezi standarde de materiale; seturi de grafică pe stil de vedere; hașuri desenate de tine.
---

Un desen e bun cât sunt de bune materialele lui. Fiecare material spune cum se desenează, iar un **standard de materiale** spune asta pentru toate materialele care nu spun nimic: hașura cărămizii, punctele betonului, buclele izolației. Poți seta totul de mână, poți alege un standard sau le poți combina.

## Cele trei feluri de vedere

Fiecare material are grafica lui de trei ori, în **Gestionare → Materiale → Grafică 2D**:

- **Secționat (secțiune)**: ce arată o secțiune și straturile tăiate ale unui perete sau ale unei plăci oriunde.
- **Secționat (plan)**: la fel, într-un plan. Pornește de la cel din secțiune și schimbă doar câmpurile setate de tine.
- **Fațadă**: fețele pe care le vede o fațadă sau o secțiune dincolo de tăietură.

![Grafica unui material: cele trei feluri de vedere, previzualizarea și hașura](shots/material-graphics-hatch.webp)

Ce lași nesetat vine de la standardul materialului, apoi din nume, ca până acum. Un câmp setat are eticheta îngroșată și o săgeată pentru întoarcerea la valoarea implicită; valoarea implicită apare ca text gri în câmp.

![Un tip de perete tăiat într-o secțiune: cărămida hașurată la 0,35 mm, cu un al doilea model dedesubt](shots/material-graphics-section.webp)

## Hașura

- **Model**, **Scară** și **Unghi**. **Aliniere**: *Cu elementul* (într-un plan hașura se rotește cu fiecare perete: un perete la 30° are liniile la 45° ale cărămizii la 75°, asizele de-a lungul lui, iar hașurile straturilor continuă de la un strat la altul), *Fixă față de vedere* (hașura își păstrează unghiul pe planșă oricum ar fi peretele și continuă de la un element la altul) sau *Fiecare strat* (începe la fiecare bandă, de-a lungul celei mai lungi laturi). **Unghiul** o rotește în plus. Un proiect făcut din noiembrie 2026 aliniază cu elementul, dacă materialul nu spune altfel; un proiect salvat înainte își păstrează hașurile fixe față de vedere, cum le desena, până setezi un material pe *Cu elementul*. Plăcile, stâlpii și tot ce e într-o secțiune nu au axă în vedere și desenează *Cu elementul* ca *Fixă față de vedere*; hașura unui perete curb merge de-a lungul coardei lui.
- **Izolația** (modelul BATT) e simbolul vatei minerale: un șir de lacrimi, una cu capul rotund lipit de o față a stratului, următoarea de cealaltă față. Capetele de pe aceeași față se ating, iar cele două laturi drepte ale fiecărei lacrimi traversează stratul până la un vârf ascuțit acolo unde se ating cele două capete de pe fața opusă, așa că laturile fac un zigzag de perechi paralele. Capetele au 0,4 din grosimea stratului, iar lățimea buclei se potrivește ca un număr întreg de bucle să umple stratul de la un capăt la altul, fără jumătăți de buclă la capete. Se desenează numai bucle întregi. Unde straturile a doi pereți din aceeași izolație și de aceeași grosime se întâlnesc la un colț (teșit sau cap la cap, de la o frântură ușoară până la un colț ascuțit de 45°), vata trece după colț dintr-o bucată: șirul fiecărui perete merge până la colțul interior, iar buclele se rotesc în jurul lui, cu vârfurile în jurul colțului interior și capetele pe fețele exterioare, laturile fiecărei bucle paralele cu ale vecinelor. La un unghi drept asta înseamnă o buclă pe diagonală, cu capul umplând colțul exterior. Unde stratul e tăiat de un gol, se termină liber, întâlnește alt perete într-un T ori un strat de altă grosime sau un colț mai ascuțit, șirul se termină pe o buclă întreagă, cât mai aproape de capăt cât încape. Într-un plan merge de-a lungul axei fiecărui perete, la orice unghi și în jurul unui perete curb (capetele urmează arcul), între fețele stratului oricum i-ar fi tăiate capetele. Primul cap stă pe fața stângă a stratului, privind de la începutul peretelui (pe o bandă de sine stătătoare, pe fața de sus, sau pe cea stângă când banda urcă pe pagină); un **unghi** al hașurii de 180 îl mută pe cealaltă față (la un colț, întoarcerea hotărăște pe ce față stau capetele), iar **scara** hașurii lărgește buclele (până la 1,15) sau le îngustează (până la 0,5). În IFC se scrie ca hașura cu linii cea mai apropiată, numită BATT, pe care Lapin o citește înapoi.
- **Alte modele de izolație** se pot alege din lista hașurilor (niciunul nu e dat de numele unui material; vata minerală rămâne BATT, EPS, XPS și PIR RIGID): FOAMBOARD (o grilă de cercuri), REFLECTIVE (rânduri de zigzaguri), BLANKET (o rețea deasă de romburi), FIBERGLASS (rânduri de solzi), BLOWNIN (un fagure), RIGIDFOAM (pătrate rotunjite cu mici romburi la colțuri), ACOUSTIC (rânduri întrepătrunse de ovale ascuțite), RIGIDFIBER (benzi de linii încrucișate la unghiuri mici) și COTTONFIBER (o rețea de lentile lungi). Scara le dă mărimea, ca oricărei hașuri. Cercurile din FOAMBOARD și pătratele și romburile din RIGIDFOAM se desenează întregi sau deloc unde se termină stratul, niciodată tăiate pe jumătate.
- **Culoare**: automată e culoarea categoriei.
- **Grosime, mm**: lățimea liniilor hașurii pe hârtie, din seria ISO (0,13; 0,18; 0,25; 0,35; 0,5; 0,7; 1,0) sau orice valoare de la 0,05 la 5. Are prioritate față de **Peniță**.
- **Tip de linie**: întrerupt, punctat sau unul al tău.
- **Opacitate** și **Halftone**: hașura într-o nuanță mai deschisă, ca fundal.

![Fundalul: umplere și un al doilea model](shots/material-graphics-background.webp)

## Fundal, contur și linii de separare

- **Umplere**: *niciuna*, *culoarea materialului* sau o culoare a ta, cu **opacitatea** ei. O umplere fără hașură e o nuanță plină.
- **Al doilea model**: o hașură sub prima, cu scara, unghiul, culoarea și grosimea ei, ca fundalul și prim-planul din Revit.
- **Conturul tăieturii** și **Linii de separare**: culoarea, grosimea și tipul liniei din jurul unui element tăiat și al liniei dintre două straturi. Unde se întâlnesc două materiale care setează amândouă o grosime, câștigă cea mai mare.

![Conturul tăieturii și liniile de separare](shots/material-graphics-lines.webp)

**Previzualizarea** de sus desenează materialul între doi vecini simpli (**Fâșie**) sau ca o bandă între straturi (**Bandă**), la scara aleasă, cu același cod ca o vedere.

![Previzualizarea, la 1:50](shots/material-graphics-preview.webp)

## Mai multe materiale și copiere

Bifează căsuțele de lângă mai multe materiale și **Modificările merg la toate**, într-un singur pas de anulare. **Copiază grafica de la…** duce grafica proprie a altui material (secțiune, plan și fațadă) pe materialul curent sau pe cele bifate.

![Două materiale bifate: o modificare merge la amândouă](shots/material-multi.webp)

![Copiază grafica de la un alt material](shots/material-copy-graphics.webp)

Același editor se deschide lângă mostra de hașură a unui strat din **Tipuri de pereți** și **Tipuri de plăci**, deci schimbi materialul acolo unde îl alegi.

![Editorul de grafică lângă mostra unui strat](shots/layer-swatch-graphics.webp)

## Mărime reală și hașuri de desen

Un model e în milimetri pe **hârtie** (*Desen*: aceeași distanță la orice scară, ca la tăietura unei secțiuni) sau în milimetri **reali** (*Mărime reală*: un rând de cărămizi are 63 mm înălțime la 1:20, 1:50 și 1:100, deci devine mai fin când scara se micșorează). **Unități** din grafica materialului alege; *a modelului* urmează modelul. La mărime reală, **Scară** 1 e dimensiunea obiectului real, 2 de două ori mai mare.

Deci o hașură care trebuie să-și păstreze proporția față de desen când se schimbă scara vederii (pusă la 1:50, același model pe obiect la 1:100 și la 1:10) e una la **Mărime reală**; una de **Desen** își păstrează în schimb aspectul pe hârtie, aceeași distanță în milimetri la orice scară. O hașură pe care o desenezi tu (**Desen → Hașură**) are aceleași **Unități** în proprietățile ei: alegând altele, arată la fel ca acum la scara vederii ei (scara 1 în unități de desen la 1:50 devine 50 la mărime reală), iar de acum urmează scara cum ai ales.

Lapin vine cu modele în mărime reală: cărămidă aparentă în trei formate (240 × 63, 240 × 71 și 215 × 65), blocuri, plăci de 300 și 600 mm, scânduri, piatră, țigle și un acoperiș metalic cu falț. Fațadele și finisajele de pardoseală ale unui proiect nou le iau după numele materialelor; un proiect făcut înainte păstrează hașurile de desen pe care le avea.

**Distanța minimă a hașurii** (**Setările vederii**, **Gestionare → Stiluri de vedere**) împiedică o hașură în mărime reală să devină o pată de cerneală: liniile mai apropiate decât atât pe hârtie (implicit 0,7 mm) nu se desenează, iar suprafața apare ca un contur cu umplere simplă. 0 desenează toate liniile.

### Originea hașurii

Un model în mărime reală își începe rândurile la baza elementului, deci cărămizile unui perete continuă în următorul. Ca să le începi în altă parte, selectează elementul: **Originea hașurii** din panoul de proprietăți primește două lungimi, de-a lungul elementului și în sus de la bază; **automată** înseamnă baza.

![Rândul Originea hașurii din panoul de proprietăți](shots/hatch-origin-panel.webp)

Într-un plan, o fațadă sau o secțiune, un element selectat arată și un **mâner al originii hașurii**: trage-l unde trebuie să înceapă un rând.

![Mânerul originii hașurii pe un perete selectat](shots/hatch-origin-grip.webp)

## După scară

Un desen la 1:200 nu trebuie să arate ca unul la 1:20. **După scară**, la baza grafică, ține pași; fiecare e un interval (*de la 1:*, *până la 1:*) și câmpurile pe care le schimbă. Primul pas care acoperă scara vederii câștigă și schimbă doar câmpurile pe care le setează. **Simplifică la scări mici** adaugă doi pași (umplere gri simplă de la 1:100, plin de la 1:200) pe care îi poți edita. Ochiul de la un pas arată previzualizarea la scara lui. Pentru liniile miezului și ale placajului unui tip de perete, separat, oricare le-ar fi materialele, stratul tipului are propriile **Penițe după scară** în [Tipuri de pereți](walls.md#tipurile-de-pereti).

![Pași după scară](shots/material-graphics-steps.webp)

### Tăietura la scară mică

Pentru o vedere întreagă, **Tăietura la scară mică** din **Setările vederii** (și fiecare stil din **Stiluri de vedere**) alege ce desenează elementele tăiate de la o scară în sus (**de la 1:**, implicit 1:200):

- **Ca materialul**: nimic nu se schimbă.
- **Plin**: fiecare element o singură suprafață de o culoare.
- **Doar contur**: fără hașură și fără umplere, doar conturul elementului.
- **Doar miezul**: stratul de bază umplut, celelalte straturi goale, toate liniile păstrate.

![Setările vederii: distanța minimă a hașurii, tăietura la scară mică și setul de grafică a materialelor](shots/view-material-graphics.webp)

![La fel în Stiluri de vedere](shots/view-styles-material-graphics.webp)

## Standarde de materiale

Un standard e o listă de reguli; fiecare potrivește nume de materiale (în mai multe limbi sau o categorie) și dă grafică. **Gestionare → Standarde de materiale…** (și **Materiale → Standarde de materiale…**) le listează.

![Standardele: cel al Lapin și patru norme](shots/material-standards.webp)

- **Incluse:** *Lapin* (1.2, aspectul implicit, care citește și nume în italiană și nu mai desenează ca membrană sau ca beton asfaltul și reazemele din elastomer numite în italiană, spaniolă, franceză, germană, portugheză sau poloneză; 1.0 e ce păstrează proiectele făcute înainte de 18.11.2026), *ISO 128-50*, *DIN 1356*, *România (SR ISO 128-50)* și *BS 8888 / BS 1192*. Normele sunt desenate de Lapin din principiile lor generale și marcate **Orientativ**: verifică-le față de standardul pe care trebuie să-l urmeze desenele tale. Un standard nu e ales niciodată pentru tine după limba sau țara ta.
- **Alegerea la un proiect nou:** **Standard de materiale** pe ecranul de start și în **Preferințe → General → Proiecte noi** pentru fiecare proiect nou. Un proiect făcut înainte desenează ca până acum.

![Alegerea standardului pe ecranul de start](shots/material-standard-start.webp)

![În Preferințe](shots/preferences-material-standard.webp)

- **Aplică…** arată o previzualizare: pentru fiecare material aspectul în secțiune, plan și fațadă înainte și după. Fără **Suprascrie grafica setată manual**, un material cu grafică proprie și-o păstrează, iar standardul dă doar materialelor care n-au. **Arată doar ce se schimbă** ascunde restul. Un pas de anulare desface totul.

![Aplicarea unui standard, cu previzualizare](shots/material-standard-apply.webp)

- **Duplică** un standard inclus ca să-l editezi (cele incluse sunt doar pentru citire). Editorul listează regulile (prima care se potrivește câștigă; se trag, se mută în sus sau în jos, se duplică, se șterg), fiecare cu numele pe limbi, un filtru de categorie și grafica pe care o dă, cu aceleași câmpuri ca un material. Un standard poartă și propriile modele **.pat**.

![Regulile unui standard](shots/material-standard-editor.webp)

- **Salvează grafica proiectului ca standard…** face un standard din ce ai setat manual pe materialele proiectului.

![Salvarea graficii proiectului ca standard](shots/material-standard-from-project.webp)

- **Exportă** și **Importă…** mută un standard ca fișier; fișierul **.lapinstandards** îl poartă împreună cu celelalte standarde ale biroului.
- **Din magazin**: caută în catalog după țară și normă; **Previzualizează** desenează planșa lui de referință, **Instalează** îl păstrează pe acest calculator, **Actualizează** ia o versiune mai nouă (proiectul nu se schimbă până nu-l folosești), **Folosește-l** îl aduce în proiect și deschide previzualizarea. **Publică în magazin…** din editorul unui standard salvează fișierele și deschide pagina autorului.

![Fila magazinului](shots/material-standard-store.webp)

## Seturi de grafică pe stil de vedere

Un **set de grafică a materialelor** spune cum se desenează materialele în vederile care îl iau, peste ce spun materialele însele: beton gri plin într-o vedere de structură, finisaje fără hașură, izolație în halftone. **Gestionare → Seturi de grafică a materialelor** le listează. *Structură* și *MEP* sunt incluse; fă-le pe ale tale cu **Set nou**, **Importă…**, **Redenumește**, duplică, exportă sau șterge.

![Seturile de grafică a materialelor](shots/material-graphics-sets.webp)

Fiecare intrare potrivește un material, o expresie de nume sau o categorie și dă grafică pentru cele trei feluri de vedere; câștigă cea mai specifică. Un stil de vedere își numește setul (**Grafica materialelor** în **Stiluri de vedere**), iar o vedere poate numi altul sau *niciunul* (**Setările vederii**). Stilurile Structural și MEP ale proiectelor noi folosesc *Structură* și *MEP*.

## Subcategorii și filtre în Vizibilitate / Grafică

În **Vizibilitate / Grafică**, desfă o categorie (un perete) ca să vezi **subcategoriile** ei: miez, finisaje, izolație, membrane și alte straturi. Fiecare are **Culoarea liniei**, **Grosimea liniei**, **Umplutură**, **Hașura tăieturii** și **Halftone**, deci o vedere poate desena finisajele fără hașură sau izolația mai deschisă.

![Subcategoriile unui perete cu grafica lor](shots/vg-subcategory-graphics.webp)

În fila **Filtre**, un filtru setează și **Hașura tăieturii**, **Umplutura tăieturii**, **Culoarea hașurii**, **Halftone-ul hașurii**, **Linia tăieturii** și, pentru fațade, **Hașura suprafeței** și **Umplutura suprafeței**. Primul filtru din listă care ia un element dă fiecare câmp.

![Un filtru care schimbă hașura și umplutura](shots/vg-filter-hatch.webp)

Câștigă cel mai puternic: ce are elementul însuși, filtrele vederii, subcategoria, setul de grafică al vederii, tăietura la scară mică, stratul tipului de perete sau de placă ([penițele lui după scară](walls.md#tipurile-de-pereti)), pasul materialului, materialul, apoi standardul.

Ca să schimbi hașura unui singur element într-o singură vedere (scara hașurii unei fundații continue, hașura scoasă dintr-o fațadă), selectează-l și folosește **Graphics in this view…** (clic dreapta sau Properties → **Graphics in view**): e cel mai puternic nivel dintre toate. Vezi [Grafică în această vedere](views.md#grafică-în-această-vedere).

## Hașuri

**Gestionare → Hașuri** listează modelele incluse, cele ale **Acestui proiect** și **Hașurile mele**. Pe lângă **Importă .pat** și **Exportă .pat**, **Model nou** face unul din numerele lui: o familie de linii e un **Unghi**, o **Origine** (x, y), un **Delta** (x, y: decalajul liniei următoare) și **Liniuțe** (**+ Liniuță**, **+ Pauză**, **+ Punct**); **Adaugă o familie** încrucișează alta. Previzualizarea și distanța reală apar pe măsură ce scrii, iar **Unități** și **Scară** spun cum se așază. **Fă modelul** îl salvează; redenumirea unui model se propagă în fiecare material, standard, set și filtru care îl numește. Un model folosit nu se șterge fără să te întrebe.

![Un model făcut din numerele lui](shots/hatch-patterns-family.webp)

Dă clic pe numele unui model ca să-l deschizi. Modelele **Acestui proiect** se editează pe loc: schimbă unghiul, originea, decalajele și liniuțele, numele, descrierea sau unitățile, apoi **Salvează**. Cele incluse sunt fixe (câmpurile lor sunt gri); ca să schimbi unul, pornește de la el un model al tău:

- **Duplică** (orice model cu linii sau cu o celulă, inclus sau al tău) deschide un model nou care pornește ca o copie a lui, numit *NUME-COPY*; schimbă-l, apoi **Fă modelul**. Până atunci nu se salvează nimic.
- **Model nou → Pornește de la** face același lucru din listă: alege modelul de la care pornești, sau *Gol* pentru o linie la 45°.

![Un model nou pornit de la unul inclus](shots/hatch-patterns-start-from.webp)

**Șterge** (în editor, sau coșul de lângă un model al proiectului în listă) scoate un model al proiectului, după ce te întreabă. Un model folosit spune în câte locuri e numit: dacă îl ștergi oricum, materialele, standardele și seturile pierd hașura aceea, iar hașurile desenate iau liniile la 45° (*LINES45*); elementele rămân cum sunt, iar **Anulează** aduce totul înapoi într-un singur pas. Modelele incluse nu se pot șterge.

### Salvează ca model de hașură

Desenează modelul cu linii, polilinii, arce și cercuri în orice vedere (o țesătură de cărămizi, un rost de placă), selectează-le și alege **Desen → Umplutură → Salvează ca model de hașură** (sau scrie **SAVEHATCH**). Fă clic pe două colțuri pentru celulă (sau <kbd>Enter</kbd> pentru limitele desenului), apoi dă-i un nume și alege unitățile **Desen** sau **Mărime reală**.

![Salvarea liniilor desenate ca model](shots/save-hatch.webp)

Liniile drepte și punctele care se repetă devin familii de linii exacte, pe care le poartă un fișier **.pat** pentru alt program. Arcele și cercurile fac un model-dală, pe care Lapin îl desenează, DXF-ul îl poartă ca linii, iar IFC-ul ca cea mai apropiată hașurare.

![Modelul e în proiect](shots/save-hatch-result.webp)

**Deschide în editor** îl arată, cu familiile lui.

![Modelul salvat în listă](shots/hatch-patterns-saved.webp)

Vezi și [Materiale, texturi și hașuri](materials.md), [Vederi și secțiuni](views.md) și [Desenul](drafting.md).
