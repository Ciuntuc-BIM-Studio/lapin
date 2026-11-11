---
title: Grupuri și curățare
order: 112
summary: Ține elementele împreună într-un grup, deschide un grup ca să-l editezi, selectează cu o regiune sau un lasou în 3D și ascunde ce îți stă în cale.
---

## Grupuri

Un grup ține elementele împreună. Selectează-le și apasă **Modifică → Creează grup** (<kbd>GP</kbd>). Un clic pe oricare membru selectează apoi tot grupul, în planuri și în 3D. Grupul se mută, se copiază, se rotește, se oglindește și se șterge ca un întreg, iar o copie e un grup al ei. În IFC un grup e un IfcGroup, așa că îl văd și alte programe.

Meniul de clic dreapta dintr-un plan, o secțiune sau o fațadă are aceleași comenzi: **Creează grup** pentru selecție, iar pe un grup **Degrupează** și **Editează grupul**. Se grupează și liniile, textele și hașurile desenate pe o vedere, printre ele liniile unui plan DXF importat, așa că un desen întreg se mută dintr-o bucată.

![Linii desenate pe plan, grupate din meniul de clic dreapta](shots/group-drafting.webp)

Proprietățile unui grup selectat arată câte elemente are, cu trei butoane: **Editează grupul**, **Degrupează** și **Selectează membrii**.

![Un grup selectat: membrii lui luminați, proprietățile lui](shots/group-selected.webp)

## Editarea unui grup

Dă dublu-clic pe un membru sau apasă **Editează grupul** (<kbd>EG</kbd>). Restul modelului se desenează estompat și nu se poate selecta, iar un clic selectează câte un membru. Șterge, mută sau schimbă membrii ca de obicei. **Scoate din grup** scoate membrii selectați. **Gata** sau <kbd>Esc</kbd> închide grupul. Un grup dispare când i se șterge ultimul membru.

![Un grup deschis pentru editare: zidul grădinii, din afara lui, estompat](shots/group-edit.webp)

## Componente

O componentă e un grup ale cărui copii rămân la fel, ca în SketchUp: schimbi una și se schimbă toate. Se potrivește pentru ce repeți de multe ori: o jardinieră, o travee de copertină, o insulă de bucătărie, un luminator, placa unei încăperi tip. Primește **solide Studio, plăci și încăperi**; pereții nu pot face parte dintr-una, pentru că îmbinările și ușile unui perete depind de vecinii lui.

Selectează elementele și alege **Modifică → Fă componentă** (<kbd>CK</kbd>). Apoi o copiezi ca pe orice: **Copy**, **Rotate**, **Copy to storeys**. Fiecare copie e o **instanță**.

- **Dublu clic** pe o instanță o deschide, ca pe un grup. Ce schimbi înăuntru (un contur, o înălțime, un push/pull, un element nou sau șters) se schimbă în toate instanțele, într-un singur pas de undo.
- **Muți și rotești** o instanță separat: celelalte rămân unde sunt, iar o modificare ajunge rotită la o instanță rotită.
- **Fă unică**, din proprietăți, lasă o instanță să o ia pe drumul ei. **Selectează instanțele** le selectează pe toate.
- **Mirror** o păstrează instanță, cu mâna schimbată: ce modifici în oricare ajunge oglindit în cealaltă.

### Tipuri de apartament

O componentă nu poate conține pereți, dar un **tip de apartament** poate. Selectează pereții, încăperile și mobilierul unui apartament și alege **Modificare → Fă tip de apartament** (<kbd>CU</kbd>). Ușile și ferestrele din acei pereți vin cu ei. Copiază-l, rotește-l sau oglindește-l cu **Mirror**, ca apartamentul de pe cealaltă parte a casei scării: o schimbare într-unul ajunge în celelalte, oglindită acolo unde sunt oglindite, iar fiecare element își păstrează identitatea în IFC.

Tipul de apartament ține pereții interiori, ușile dintre camere, camerele și mobilierul. Clădirea ține fațada, pereții dintre apartamente și coridorul. Locul unde se întâlnesc ține de fiecare instanță:

- Un perete interior se oprește în peretele clădirii la care ajunge: capătul lui se taie la fața acelui perete, iar peretele clădirii rămâne cum e. Oprirea se caută din nou de fiecare dată când așezi, muți sau oglindești o instanță și când desenezi sau muți un perete al clădirii în apropiere. Dacă muți un perete al clădirii, se schimbă doar apartamentul care îl atinge.
- Ușile de intrare și ferestrele de pe fațadă țin de cameră, nu de perete: le alegi în proprietățile camerei (vezi [Uși și ferestre din încăpere](rooms.md)). Fiecare instanță are atunci ușa ei în peretele clădirii de pe latura aceea, cu mâna inversă într-o instanță oglindită. Dacă muți sau întorci de mână o astfel de ușă ori îi schimbi tipul, schimbarea apare în toate instanțele. Unde niciun perete nu o poate primi, îți spun proprietățile camerei.

Un tip de apartament are dimensiuni fixe: nu se întinde după un loc mai lat sau mai îngust.

**Copiază pe niveluri** face din copie altă instanță, care se oprește în pereții acelui nivel. În IFC o instanță e un grup de elemente obișnuite, cu setul de proprietăți `Lapin_Unit`; importă fișierul din nou cu **Ca un singur grup** nebifat și e din nou o instanță; cu opțiunea bifată, cum e implicit, elementele lui rămân membri obișnuiți ai grupului fișierului.

![Un tip de apartament în două instanțe, a doua oglindită](shots/units-plan.webp)

**Gestionare → Componente** listează componentele proiectului și câte instanțe are fiecare. Acolo le redenumești sau le selectezi instanțele. Instanțele sunt elemente obișnuite în planuri, secțiuni, liste, cantități și exporturi, fiecare cu GlobalId-ul ei.

![O componentă în patru instanțe, una rotită: conturul schimbat într-una, urmat de toate](shots/components-3d.webp)

![Proprietățile unei instanțe: componenta, Selectează instanțele, Fă unică](shots/component-properties.webp)

## Selecția pe niveluri de ansamblu

Unele elemente sunt părți ale unui întreg: stâlpii, riglele și panele unei hale metalice, părțile unui ansamblu (o cutie din Studio, un ansamblu citit dintr-un fișier IFC) și membrii unui grup. **Vedere → Selecție pe niveluri de ansamblu**, oprită la început, le alege așa cum e construit întregul. Primul clic pe o parte selectează întregul de sus: grupul sau hala. Cât timp e selectat, un clic în interiorul lui selectează partea cu un nivel mai jos, pe drumul spre ce ai atins, iar fiecare clic următor coboară încă un nivel, până la elementul însuși. Un clic pe altă parte a aceluiași întreg o selectează la același nivel. <kbd>Esc</kbd> urcă un nivel, iar sus renunță la selecție; și un clic în gol renunță. <kbd>Ctrl</kbd>+clic (<kbd>Cmd</kbd>+clic pe Mac) selectează direct elementul, iar <kbd>Shift</kbd> adaugă la selecție întregul de sus. Deasupra vederii, un șir de niveluri le numește, de la etaj la selecție: un clic pe unul îl selectează. Cât timp e selectată o parte dintr-un întreg, restul modelului se desenează ușor estompat. Funcționează la fel în 3D, în planuri, secțiuni și fațade. Un dreptunghi sau un lasou de selecție ia întregurile de sus. Dublu-clicul deschide în continuare un grup pentru editare; în interiorul lui, coborârea pornește de sub grup. Alegerea se păstrează pe acest calculator și se află și în **Vedere → Setările vederii**.

![Coborât într-o hală metalică: rigla selectată, șirul de niveluri deasupra vederii 3D](shots/drill-select.webp)

## Fișierele importate

Fiecare fișier IFC, Fragments sau rețea pe care îl imporți intră ca un singur grup, cu numele fișierului. Debifează **Ca un singur grup** la importul IFC ca să aduci elementele libere. Un fișier legat nu se grupează: e deja o legătură, doar pentru citire (vezi [Import și export](import-export.md)). Elementele citite dintr-un fișier ca referințe (dintr-un IFC, dintr-o rețea) se mută, se rotesc, se oglindesc și se copiază împreună cu grupul lor ca orice element; forma lor rămâne cea din fișier.

## Selectarea unei regiuni în 3D

**Vedere → Selectare regiune** (<kbd>SR</kbd>) face din tragerea cu butonul stâng în 3D o selecție, ca în planuri. Trasă spre dreapta, o fereastră ia ce e cuprins în întregime în ea. Trasă spre stânga, un crossing ia tot ce atinge. Apasă <kbd>Space</kbd> în timp ce tragi ca să desenezi un lasou, și <kbd>Shift</kbd> ca să adaugi la selecție. Cât e pornită, butonul drept rotește vederea, iar cel din mijloc o mută. **×** de pe eticheta ei, sau din nou <kbd>SR</kbd>, o oprește.

![Un crossing tras peste casă în 3D](shots/select-region-3d.webp)

## Ascunderea temporară

**Vedere → Ascunde** (<kbd>HH</kbd>) ascunde selecția în vederea 3D. **Izolează** (<kbd>HI</kbd>) ascunde tot restul. **Arată tot** (<kbd>HR</kbd>), sau **×** de pe etichetă, aduce totul înapoi. Nimic nu se salvează și nu se șterge: e un mod de a ajunge la ce e în interiorul unui model importat cât timp îl cureți. Clic dreapta în vederea 3D oferă aceleași intrări (**Ascunde**, **Izolează** și, cât timp ceva e ascuns, **Arată tot**): ce e sub cursor se selectează întâi, dacă nu e deja selectat.
