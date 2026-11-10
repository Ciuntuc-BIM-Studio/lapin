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
- **Perete cortină** (<kbd>CW</kbd>) desenează un perete vitrat cu montanți și panouri; tipul lui le poate lua forma reală dintr-o [bibliotecă de profile](profiles.md).
- Pereții se îmbină singuri unde se întâlnesc: un colț la 45°, un perete care se termină pe altul în T, unul care îl traversează pe altul în X. La un T sau un X straturile intră unul în altul cum se construiesc: miezul peretelui care se termină trece prin finisajele celuilalt până la miezul lui, iar finisajele lui se opresc la fața celuilalt, unde își întâlnesc perechea, așa că planul arată o singură alcătuire, fără linie prin ea. **Îmbină** (<kbd>JW</kbd>), **La 45°** și **Cap la cap** schimbă felul în care se întâlnesc doi pereți când nu e ce vrei; **Colțuri noi la 45°** face cap la cap implicitul pe acest calculator. În ambele cazuri, fiecare strat întoarce colțul singur: finisajele se întâlnesc cu finisajele și merg continuu, iar miezurile se întâlnesc pe diagonală (la 45°) sau un miez merge până la fața exterioară a celuilalt (cap la cap), așa că planul pereților cu straturi arată colțuri curate. Fiecare strat intră în celălalt perete prin straturile lui cu **Prioritate** mai mică și se oprește la primul cu prioritate egală sau mai mare (vezi **Tipurile de pereți** mai jos), așa că implicit miezurile se întâlnesc cu miezuri și finisajele se opresc la fețe; la un colț, un miez cu prioritate mai mare trece prin celălalt, la 45° sau cap la cap. **Prioritatea la tăiere** a unui perete nu-l mai face să intre într-un perete cu care e îmbinat: între pereți îmbinați decide doar care miez trece când ambele miezuri au aceeași prioritate (la un colț cap la cap sau la o încrucișare).
- **Inversează** schimbă care dintre cei doi pereți trece prin colț.

**Linia pe care desenezi.** Cât timp unealta Wall e activă, lista din bara liniei de comandă alege linia pe care dai clic: **Axul miezului** (axa, ca până acum), **Miez, fața exterioară**, **Miez, fața interioară**, **Fața exterioară** sau **Fața interioară**. Exteriorul e dreapta peretelui în sensul desenării (desenează în jurul clădirii în sens invers acelor de ceasornic ca să fie în afară). Axa peretelui rămâne pe miez, decalată față de linie, iar pereții desenați unul după altul pe aceeași față se întâlnesc în colțul acelei fețe.

## Modificarea unui perete

Dă clic pe un perete ca să-l selectezi. Apar mânerele lui. Trage de capete ca să-l lungești sau să-l scurtezi, sau de crucea din mijloc ca să-l muți.

![Un perete selectat, cu mânerele lui](shots/wall-selected.webp)

Panoul de proprietăți arată peretele selectat. Sus sunt tipul și straturile lui. Dedesubt sunt lungimea, înălțimea, decalajul la bază, nivelul și faza. Schimbi orice valoare, și modelul o urmează. Valorile fiecărui perete sunt ale lui. **Grosimea** (Thickness) vine din straturile tipului său: scrie una nouă și pereții selectați primesc tipul cu aceeași alcătuire și acea grosime, miezul redimensionat și finisajele păstrate. Lapin folosește tipul proiectului dacă unul are exact acea alcătuire, altfel face unul numit după al lor (*Partition wall 130mm* devine *Partition wall 150mm*). Ceilalți pereți ai tipului vechi rămân cum sunt, iar <kbd>Ctrl</kbd> <kbd>Z</kbd> anulează totul într-un pas.

![Proprietățile unui perete](shots/wall-properties.webp)

Mai jos, **Dependențe** arată ce e construit din perete și din ce e construit peretele. **Folosit de** enumeră elementele care se schimbă când se schimbă peretele: pereții cu care se îmbină, ușile și ferestrele lui, un perete atașat de el. Dai clic pe un nume ca să selectezi elementul. **Construit din** enumeră elementele de care depinde peretele, apoi tipul și materialele lui. Când ștergi un element de care depind altele, un mesaj le numește pe cele reconstruite; <kbd>Ctrl</kbd> <kbd>Z</kbd> le aduce pe toate înapoi.

**Întoarce** mută straturile peretelui pe partea cealaltă. Poți da clic și pe săgețile mici de lângă un perete selectat în plan.

Un perete recunoscut dintr-un IFC importat al cărui capăt e tăiat oblic în fișier păstrează tăietura: **Unghiul tăieturii la început** și **Unghiul tăieturii la sfârșit** în proprietățile lui, în grade față de capătul drept. O îmbinare la acel capăt ia locul tăieturii.

**Alinierea hașurii** stabilește cum se hașurează în plan straturile acestui perete. **material** urmează setarea fiecărui material. **axis** așază hașura fiecărui strat de-a lungul axului peretelui, pornind de la începutul axului, așa că un zid de cărămidă sub un unghi își arată rândurile de-a lungul lui, continuate de la un strat la altul. **view** pornește fiecare hașură de la originea vederii, fără rotire.

### Un perete după profilul lui

**Profil…** din proprietățile unui perete îi desenează vârful în fațadă, văzut de la început spre sfârșit: un fronton, o pantă, trepte, un perete care urmează o scară. Trage punctele (**Shift** păstrează înălțimea unui punct), fă clic pe linia de sus ca să adaugi unul, clic dreapta pe un punct ca să-l scoți, sau scrie-le în tabel, în milimetri. **Pornește de la** dă un **Fronton**, o **Pantă** sau **Trepte** până la înălțimea pe care o scrii. O treaptă înseamnă două puncte unul deasupra celuilalt. **Aplică** dă peretelui forma; **Vârf drept** îl face din nou drept sus.

Peretele rămâne perete: grosimea, straturile, îmbinările, ușile și ferestrele lui sunt cum erau, iar în IFC se exportă cu forma lui.

![Un fronton desenat în editorul de profil](shots/wall-profile-editor.webp)

![Peretele de vest al casei ca fronton, cu fereastra păstrată](shots/wall-profile-3d.webp)

### Conturul și barele unui perete cortină

**Profil…** din proprietățile unui perete cortină îi desenează conturul în elevație, ca la un perete: un fronton, o pantă, trepte sau orice contur, cu puncte de tras, adăugat sau scos și un tabel în milimetri. Peretele urcă până la cel mai înalt punct. Grila lui primește o linie unde se frânge conturul (o frângere la mai puțin de un montant și jumătate de o linie a grilei e mutată pe ea); fiecare travee păstrează traversele care încap sub punctul ei cel mai de jos; panoul de deasupra ultimei umple până la contur, iar o bară de margine îi urmează linia, înclinată; montanții sunt tăiați după el. Setările tipului rămân, iar **Profil…** deschide din nou conturul ca să-l schimbi, fără reconstruire; **Vârf drept** dă dreptunghiul înapoi. Se vede în 3D, fațade și secțiuni, extrasele măsoară panourile și barele așa cum sunt desenate, iar IFC-ul le poartă ca piese.

![Un fronton desenat ca contur al unui perete cortină](shots/curtain-boundary-editor.webp)

![Peretele cortină sub fronton, în 3D](shots/curtain-boundary-3d.webp)

**Bare…** dă unor montanți și traverse alese un profil propriu. Elevația peretelui arată fiecare bară: dă clic pe una (<kbd>Shift</kbd> sau <kbd>Ctrl</kbd>/<kbd>⌘</kbd> adaugă altele, sau ia toți montanții ori toate traversele), alege un profil din [bibliotecile de profile](profiles.md) sau o cutie simplă, **Rotirea** și **Oglindirea** lui, alinierea de-a curmezișul barei (fața din față sau din spate pe planul ei) și de-a lungul ei (muchia de început sau de sfârșit pe linia grilei sau pe nivel) și decalajele de-a curmezișul și spre exteriorul clădirii; imaginea alăturată arată secțiunea așa cum stă. **Aplică la cele alese** le dă stilul, **Înapoi la cel al tipului** îl ia. Profilul propriu al unui tip are aceleași câmpuri de poziție în **Pereți cortină…**, pentru fiecare perete al tipului. Profilul rămâne o copie în proiect: **Actualizează din bibliotecă** schimbă fiecare bară făcută din el, în acest perete și în altele.

![Bare: un profil așezat pe montanții aleși](shots/curtain-members-dialog.webp)

![Doi montanți cu un profil propriu, în 3D](shots/curtain-members-3d.webp)

**Panouri și uși…** dă celulelor alese un panou propriu sau o ușă. Elevația peretelui arată fiecare celulă așa cum o construiește motorul: dă clic pe una (<kbd>Shift</kbd> sau <kbd>Ctrl</kbd>/<kbd>⌘</kbd> adaugă altele). Un **tip de panou** se salvează o dată în proiect și se dă celulelor oricărui perete cortină: sticlă, plin, o **umplutură în ramă** (o ramă de lățimea și adâncimea ei în jurul celulei, umplutura de sticlă sau plină retrasă față de fața ei exterioară cu retragerea) sau **deschisă** (fără panou); grosimea, decalajul față de axă și materialele lui (ale tipului de perete cortină când nu e ales niciunul). **Dă celor alese** îl aplică, **Panoul tipului** îl scoate; un tip de panou modificat schimbă fiecare celulă a lui. **Ușă în cele alese** înlocuiește fiecare celulă aleasă cu o ușă: o ușă nouă de perete cortină (simplă sau dublă, batantă sau glisantă, lățimea și înălțimea ei, canate vitrate sau pline, profilul tocului din [bibliotecile de profile](profiles.md)) sau orice tip de ușă din proiect. **Umple celula** o face de mărimea celulei; altfel își păstrează mărimea tipului, stă la stânga, la centru sau la dreapta în celulă cu un decalaj și primește o traversă deasupra și un montant alături unde rămâne sticlă, vitrate cu panoul celulei. Pe rândul de jos ușa stă pe pardoseală, iar traversa de jos e tăiată sub ea. Setările se păstrează pe celulă, deci urmează grila când se schimbă: o ușă care umple celula crește sau scade odată cu ea, iar una a cărei celulă a dispărut spune asta și revine odată cu ea. Ușa e o ușă: selecteaz-o ca să-i schimbi mâna sau partea, tipul, sau ca s-o muți în altă celulă (Mută de-a lungul peretelui); etichetele, tabelele de uși (la mărimea la care stă) și exportul IFC (un IfcDoor parte a IfcCurtainWall-ului ei) o numără.

![Panouri și uși: un parapet în ramă și o ușă în celulele alese](shots/curtain-cells-dialog.webp)

![Un perete cortină cu o ușă și parapete în ramă, în 3D](shots/curtain-cells-3d.webp)

## Tipurile de pereți

Un tip de perete e un set de straturi, de la fața din dreapta sensului în care e desenat peretele (exteriorul, când desenezi în jurul clădirii în sens trigonometric) la fața din stânga. Fiecare strat are un material și o grosime, iar unul dintre ele e **miezul**. Linia peretelui merge pe miez. Ca să editezi tipurile, alege **Arhitectură → Tipuri de pereți**. Un strat altul decât miezul poate avea înălțimea lui: **De la** și **Până la** spun cât de sus merge peste baza peretelui (un placaj de faianță până la 1500, un placaj de fațadă de la 300 în sus); gol, merge pe toată înălțimea.

Coloana **Prioritate** (de la 0 la 100) spune cum întâlnește stratul straturile altui perete unde pereții se îmbină: trece prin straturile celuilalt cu prioritate mai mică și se oprește la primul cu prioritate egală sau mai mare. Goală, stratul ia prioritatea materialului său din **Gestionare → Ordinea de tăiere**, altfel 50 pentru miez și 0 pentru celelalte straturi (numărul arătat estompat). Astfel un miez de beton cu 100 nu e întrerupt de un miez de montanți cu 99 și trece prin peretele de gips-carton, iar o tencuială dură cu 60 oprește miezul unui perete care se termină în ea. Exportul IFC o scrie ca Priority a stratului, iar importul o citește înapoi.

Coloana **Notă** spune cum e pus stratul în acest tip (*în strat continuu, Baumit SupraFix*): ține de tip, nu de material, iar o etichetă de stratificație o scrie după strat; IFC o poartă ca Description a stratului. Când materialul stratului are grosimi în catalog (**Gestionare → Materiale → Date**), săgeata mică de lângă grosime le oferă, iar un semn de avertizare marchează un strat de altă grosime.

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

## Profile și canale în pereți

**Arhitectură → Profil de perete** (<kbd>WB</kbd>) duce un profil de-a lungul feței unui perete: o cornișă, un brâu, un soclu, un glaf. **Canal în perete** (<kbd>RV</kbd>) îl taie în perete: un canal, un rost de umbră. Alegi profilul și înălțimea în bara liniei de comandă, apoi dai clic pe fața peretelui. În 3D profilul se pune la înălțimea pe care dai clic; în plan, la înălțimea tastată, pe partea pe care dai clic. **de sus** măsoară înălțimea în jos de la vârful peretelui, pentru o cornișă sub streașină. **Profil pe pereții selectați** și **Canal pe pereții selectați** pun același lucru pe fiecare perete selectat, pe fața lui exterioară.

Un profil merge pe toată lungimea feței și se oprește la ușile și ferestrele peste care ar trece. Acolo unde doi pereți se întâlnesc într-un colț și amândoi au un profil de același fel, la aceeași înălțime, pe fețele care se întâlnesc, cele două se taie pe bisectoare una în alta, ca o singură cornișă care întoarce colțul. Selectezi un profil ca să-l schimbi în Proprietăți: profilul, materialul, fața (**Schimbă partea**), înălțimea, distanța de la față, unde începe și unde se termină de-a lungul peretelui și dacă se oprește la goluri. Proprietățile unui perete arată profilele și canalele pe care le are.

Profilele și canalele merg cu peretele lor când e copiat, mutat pe alt nivel sau șters. Se măsoară pe lungime și pe volum (la un canal, volumul scos). În IFC un profil e un IfcCovering MOLDING, iar un canal un IfcOpeningElement RECESS în peretele lui; amândouă se întorc la import cum erau.

![Cornișe, brâuri și un canal pe pereții exteriori ai unei case](shots/wall-sweeps-3d.webp)

### Profile pe tipul de perete

Un profil pe care trebuie să-l aibă toți pereții unui tip (un brâu de soclu, o cornișă, un rost de umbră) ține de tip. În **Arhitectură → Tipuri de pereți**, sub straturi, **Profile și canale** le arată, câte un rând fiecare: profilul, **Profil de perete** sau **Canal în perete**, fața (D, fața primului strat al tipului, sau S, cum le numește legenda), înălțimea peste baza peretelui (sau sub vârful lui, cu **De sus**), distanța în afară de la față, materialul unui profil și întoarcerile lui. **Profil de perete** și **Canal în perete** de sub tabel adaugă un rând. Salvezi tipul: toți pereții lui le primesc, la fel și un perete desenat cu tipul sau schimbat pe el. Sunt profile ca celelalte: măsurate, în extrasul de profile de perete, tăiate pe bisectoare la colțurile unde se întâlnesc pereți ai tipului, scrise în IFC. Profilele unui perete întors își schimbă fața odată cu straturile lui. **Exportă JSON** de sub lista tipurilor le duce cu el, cu conturul profilelor și materialele după nume (CSV-ul are doar straturile).

Selectezi unul pe un perete: Proprietăți scrie **Din tip**. Îi schimbi înălțimea, distanța sau alt câmp și câmpul acela devine al peretelui (**Proprii:** înălțime): schimbările de mai târziu ale tipului nu-l mai ating. **Înapoi la tip** îi dă din nou valorile tipului. **Oprește pe acest perete**, sau ștergerea lui, lasă profilul tipului oprit pe peretele acela; **Repornește profilele tipului**, în **Profile și canale** ale peretelui, le aduce înapoi.

![Un profil pe tipul de perete, în dialogul Tipuri de pereți](shots/wall-type-sweeps.webp)

### Întoarceri

Un profil care ajunge la un capăt liber al peretelui (fără alt perete îmbinat acolo) se întoarce: tăiat pe bisectoare la capăt, profilul lui ocolește colțul pe capătul peretelui și se oprește în planul feței, așa că ornamentul e închis, nu își arată tăietura. **Întoarceri la capete libere** din Proprietăți alege ambele (implicit pentru un profil), la început, la sfârșit sau niciuna, pentru capete drepte. Un canal nu se întoarce niciodată. La un colț îmbinat cu alt perete profilul nu se întoarce: merge până la colț sau intră în profilul la fel de pe celălalt perete. La uși și ferestre se oprește drept; bifezi **Întoarceri la goluri** ca să se întoarcă și în jurul glafurilor. **Retras de la început** și **Retras de la sfârșit** îl opresc înainte de capetele feței (drept acolo), iar **Profil răsturnat** îi întoarce profilul cu susul în jos.

### Mutarea și copierea unui profil

Selectezi un profil și folosești **MOVE** sau **COPY** ca pentru orice element; peretele rămâne pe loc. Ce merge mutarea de-a lungul peretelui deplasează profilul pe el (un profil pe toată fața primește atunci un început și un sfârșit ale lui); ce merge peste perete e lăsat deoparte. Mutat în sus sau în jos într-o fațadă sau o secțiune, își schimbă înălțimea. O copie ajunge pe același perete, deplasată, sau pe alt perete când ajunge în el, pe fața pe care ajunge. **ROTATE** și **MIRROR** îl țin pe peretele lui: oglindit față de axa peretelui, trece pe cealaltă față. Un profil al tipului pe care îl muți își păstrează noul loc ca al lui; mutat pe alt perete, devine profilul propriu al acelui perete, iar al tipului rămâne oprit pe primul.

### Profile

**Gestionare → Profile** ține profilele proiectului, contururile după care se extrudează profilele de perete, canalele și fundațiile continue. **Profil nou** pornește din catalog, cu dimensiunile lui: un dreptunghi, un brâu teșit, o cornișă, un semirotund, un nas de glaf, un brâu cu lăcrimar; un canal în V, un canal dreptunghiular sau rotund; pentru fundații, dreptunghiulară, în 2 sau 3 trepte, cu fața de sus înclinată, excentrică, cu elevație. Sau alegi **Colțuri tastate**: fiecare colț u v în milimetri, cu punct și virgulă între ele, iar un al treilea număr face din latura care pleacă din colț un arc. **Dintr-un contur desenat** citește o polilinie închisă sau un dreptunghi desenat cu Desen și selectat în vederea lui. Cercul din previzualizare e punctul de inserție: la un profil de perete stă pe fața peretelui, la înălțimea profilului, cu u spre exteriorul feței (spre interiorul peretelui la un canal). Când schimbi un profil, tot ce îl folosește se schimbă cu el.

![Dialogul Profile](shots/profiles.webp)

### Placaje

Placajele se modelează cu straturile peretelui, nu cu profile. Adaugi un strat de finisaj pe exteriorul tipului de perete (piatră, lambriu, plăci fibrociment, un termosistem), cu grosimea și materialul lui. **De la** și **Până la** îi dau o înălțime proprie (un soclu de piatră până la 600, tencuială deasupra), iar butonul **straturi** al stratului îi schimbă materialul în benzi pe înălțime. Pentru un placaj care stă separat de perete, desenezi un perete al lui pe față (opțiunea de linie **Fața exterioară** a uneltei de pereți), cu un tip făcut din straturile placajului. Placajul pus scândură cu scândură (scânduri verticale sau orizontale, șipci) nu se modelează încă.

## Pereți sub acoperiș

Pereții de pe același nivel cu un acoperiș se opresc singuri sub el și urcă până la frontoanele lui. Pentru un perete de pe alt nivel, selectează-l și alege **Arhitectură → Atașează la acoperiș** (<kbd>AR</kbd>), apoi dă clic pe acoperiș, sau pe orice alt corp până la care trebuie să urce peretele: un solid din Studio, un element importat. Vezi [Plăci și acoperișuri](floors-roofs.md).
