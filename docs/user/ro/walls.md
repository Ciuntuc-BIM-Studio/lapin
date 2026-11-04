---
title: Pereți
order: 20
summary: Desenezi pereții colț cu colț, îi modifici în panoul de proprietăți și îți faci tipuri de pereți cu straturile lor.
---

<!-- video walls -->

## Desenarea pereților

Deschide tabul **Arhitectură** și alege **Perete**, sau scrie <kbd>WA</kbd>.

Cât timp unealta e activă, panoul de proprietăți arată **Element nou**: tipul cu care se desenează pereții. Alege altul acolo înainte de primul clic și vezi straturile lui, grosimea și câți pereți îl folosesc deja; **Editează tipurile…** deschide Wall types. Ușa, fereastra, placa, tavanul și peretele cortină își arată tipurile la fel.

![Tabul Arhitectură](shots/ribbon-architecture.webp)

Dă clic unde începe peretele, apoi la fiecare colț. Peretele următor urmează mouse-ul. În timp ce desenezi, poți scrie o lungime și un unghi în căsuțele de deasupra vederii în loc să dai clic. <kbd>Enter</kbd> sau <kbd>Esc</kbd> oprește desenarea.

![Desenarea pereților: peretele următor urmează mouse-ul, cu lungimea și unghiul afișate](shots/wall-drawing.webp)

Unde se întâlnesc doi pereți, Lapin îi îmbină singur. Straturile lor intră unele în altele așa cum s-ar construi.

- **Perete curb** (<kbd>WC</kbd>) desenează un perete pe un arc.
- **Perete cortină** (<kbd>CW</kbd>) desenează un perete vitrat cu montanți și panouri.
- Pereții se îmbină singuri unde se întâlnesc: un colț la 45°, un perete care se termină pe altul în T, unul care îl traversează pe altul în X. La un T sau un X straturile intră unul în altul cum se construiesc: miezul peretelui care se termină trece prin finisajele celuilalt până la miezul lui, iar finisajele lui se opresc la fața celuilalt, unde își întâlnesc perechea, așa că planul arată o singură alcătuire, fără linie prin ea. **Îmbină** (<kbd>JW</kbd>), **La 45°** și **Cap la cap** schimbă felul în care se întâlnesc doi pereți când nu e ce vrei; **Colțuri noi la 45°** face cap la cap implicitul pe acest calculator. În ambele cazuri, fiecare strat întoarce colțul singur: finisajele se întâlnesc cu finisajele și merg continuu, iar miezurile se întâlnesc pe diagonală (la 45°) sau un miez merge până la fața exterioară a celuilalt (cap la cap), așa că planul pereților cu straturi arată colțuri curate. Fiecare strat intră în celălalt perete prin straturile lui cu **Prioritate** mai mică și se oprește la primul cu prioritate egală sau mai mare (vezi **Tipurile de pereți** mai jos), așa că implicit miezurile se întâlnesc cu miezuri și finisajele se opresc la fețe; la un colț, un miez cu prioritate mai mare trece prin celălalt, la 45° sau cap la cap. **Prioritatea la tăiere** a unui perete nu-l mai face să intre într-un perete cu care e îmbinat: între pereți îmbinați decide doar care miez trece când ambele miezuri au aceeași prioritate (la un colț cap la cap sau la o încrucișare).
- **Inversează** schimbă care dintre cei doi pereți trece prin colț.

**Linia pe care desenezi.** Cât timp unealta Wall e activă, lista din bara liniei de comandă alege linia pe care dai clic: **Axul miezului** (axa, ca până acum), **Miez, fața exterioară**, **Miez, fața interioară**, **Fața exterioară** sau **Fața interioară**. Exteriorul e dreapta peretelui în sensul desenării (desenează în jurul clădirii în sens invers acelor de ceasornic ca să fie în afară). Axa peretelui rămâne pe miez, decalată față de linie, iar pereții desenați unul după altul pe aceeași față se întâlnesc în colțul acelei fețe.

## Modificarea unui perete

Dă clic pe un perete ca să-l selectezi. Apar mânerele lui. Trage de capete ca să-l lungești sau să-l scurtezi, sau de crucea din mijloc ca să-l muți.

![Un perete selectat, cu mânerele lui](shots/wall-selected.webp)

Panoul de proprietăți arată peretele selectat. Sus sunt tipul și straturile lui. Dedesubt sunt lungimea, înălțimea, decalajul la bază, nivelul și faza. Schimbi orice valoare, și modelul o urmează.

![Proprietățile unui perete](shots/wall-properties.webp)

Mai jos, **Dependențe** arată ce e construit din perete și din ce e construit peretele. **Folosit de** enumeră elementele care se schimbă când se schimbă peretele: pereții cu care se îmbină, ușile și ferestrele lui, un perete atașat de el. Dai clic pe un nume ca să selectezi elementul. **Construit din** enumeră elementele de care depinde peretele, apoi tipul și materialele lui. Când ștergi un element de care depind altele, un mesaj le numește pe cele reconstruite; <kbd>Ctrl</kbd> <kbd>Z</kbd> le aduce pe toate înapoi.

**Întoarce** mută straturile peretelui pe partea cealaltă. Poți da clic și pe săgețile mici de lângă un perete selectat în plan.

## Tipurile de pereți

Un tip de perete e un set de straturi, de la fața din dreapta sensului în care e desenat peretele (exteriorul, când desenezi în jurul clădirii în sens trigonometric) la fața din stânga. Fiecare strat are un material și o grosime, iar unul dintre ele e **miezul**. Linia peretelui merge pe miez. Ca să editezi tipurile, alege **Arhitectură → Tipuri de pereți**. Un strat altul decât miezul poate avea înălțimea lui: **De la** și **Până la** spun cât de sus merge peste baza peretelui (un placaj de faianță până la 1500, un placaj de fațadă de la 300 în sus); gol, merge pe toată înălțimea.

Coloana **Prioritate** (de la 0 la 100) spune cum întâlnește stratul straturile altui perete unde pereții se îmbină: trece prin straturile celuilalt cu prioritate mai mică și se oprește la primul cu prioritate egală sau mai mare. Goală, stratul ia prioritatea materialului său din **Gestionare → Ordinea de tăiere**, altfel 50 pentru miez și 0 pentru celelalte straturi (numărul arătat estompat). Astfel un miez de beton cu 100 nu e întrerupt de un miez de montanți cu 99 și trece prin peretele de gips-carton, iar o tencuială dură cu 60 oprește miezul unui perete care se termină în ea. Exportul IFC o scrie ca Priority a stratului, iar importul o citește înapoi.

Un strat altul decât miezul poate și să-și schimbe materialul pe înălțime: butonul **straturi** de la capătul rândului lui adaugă o bucată (faianță, 2100 implicit), de la baza stratului în sus; mai adaugi pentru o bandă (vopsea până la 900, faianță până la 1500), iar deasupra ultimei bucăți stratul continuă în materialul lui până sus. Fiecare bucată e desenată, tăiată în plan și secțiuni și măsurată în materialul ei.

Un strat altul decât miezul poate avea grosimea 0, pentru un material prea subțire ca să fie modelat, dar care trebuie măsurat: o hidroizolație, o barieră de vapori, vopseaua. Nu face niciun solid; planurile și secțiunile îl desenează ca o linie groasă întreruptă acolo unde stă între două straturi (pe o față, linia feței e chiar a lui), desenul din Alcătuirile tipurilor îl numește ca pe celelalte, iar măsurătoarea lui e pe suprafață: fața stratului pe care stă, fără goluri, în cantitățile peretelui și în antemăsurătoarea pe materiale.

![Un strat de tencuială cu faianță până la 2100 și tencuiala deasupra](shots/wall-layer-parts.webp)

![Tipuri de pereți: straturile unui perete de cărămidă](shots/wall-types.webp)

Legenda de deasupra straturilor arată peretele în plan așa cum îl arată un perete selectat: axa întreruptă cu o săgeată în sensul în care a fost desenat, **D** pe fața din dreapta și **S** pe cea din stânga. Straturile sunt numerotate de la 1, pe fața D, până la ultimul, pe fața S, cu aceleași numere ca rândurile tabelului; miezul, pe care merge linia peretelui, e evidențiat. Un perete întors cu **Întoarce** are straturile invers, de la S la D. Mesajul unei salvări refuzate dispare imediat ce schimbi din nou tipul.

- **+ Layer** adaugă un strat. Săgețile mută un strat în sus sau în jos.
- **Zone pe înălțime** împarte miezul de jos în sus, de exemplu un prim rând, zidăria și o centură.
- **Buiandrugi** alege buiandrugii puși peste uși și ferestre: prefabricați sau turnați pe loc, cu rezemarea și înălțimea lor.
- **Desenează cu el** închide fereastra și începe desenarea pereților de acest tip.

O schimbare a tipului schimbă toți pereții de acel tip.

**Import…**, **Export CSV** și **Export JSON**, sub lista de tipuri, scot tipurile de pereți și de plăci dintr-un proiect și le duc în altul, sau într-un tabel Excel și înapoi: CSV-ul are câte un rând pe strat (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), rândurile unui tip unul după altul, iar punctul și virgula și virgula zecimală ale Excelului se citesc și ele. La import, un tip cu același nume primește straturile din fișier, iar celelalte se creează, într-un singur pas de anulat; un material pe care proiectul nu-l are se creează după nume. Extrasul **Alcătuirile tipurilor** (Schedules → + → Type assemblies) arată fiecare tip de perete și de placă cu marca, grosimea totală, câte elemente îl folosesc și straturile lui în ordine.

## Pereți sub acoperiș

Pereții de pe același nivel cu un acoperiș se opresc singuri sub el și urcă până la frontoanele lui. Pentru un perete de pe alt nivel, selectează-l și alege **Arhitectură → Atașează la acoperiș** (<kbd>AR</kbd>), apoi dă clic pe acoperiș, sau pe orice alt corp până la care trebuie să urce peretele: un solid din Studio, un element importat. Vezi [Plăci și acoperișuri](floors-roofs.md).
