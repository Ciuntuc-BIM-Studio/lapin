---
title: Mobilier
order: 47
summary: Canapele, canapele de colț, fotolii, scaune, paturi, dulapuri, comode, biblioteci, bufete, mese, birouri și bucătării în șase dispuneri, din tipuri parametrice, puse lângă pereți cu un clic.
---

## Așezarea mobilierului

Tabul **Interior** are, după finisaje, câte o unealtă pentru fiecare fel de piesă: **Sofa** (canapea, <kbd>SO</kbd>), **Corner sofa** (canapea de colț), **Armchair** (fotoliu, <kbd>AC</kbd>), **Chair** (scaun), **Bed** (pat, <kbd>BD</kbd>), **Bedside table** (noptieră), **Wardrobe** (dulap, <kbd>WD</kbd>), **Chest of drawers** (comodă), **Bookcase** (bibliotecă), **Sideboard** (bufet), **Table** (masă, <kbd>TB</kbd>), **Coffee table** (măsuță de cafea) și **Desk** (birou). Alegi una și dai clic în plan.

Un clic lângă un perete lipește piesa de el. Spatele ei stă pe fața peretelui (pe tencuială, nu pe miez), fața ei privește spre încăpere și alunecă de-a lungul peretelui până nu mai intră în peretele din colț. Un clic mai departe de pereți pune piesa exact unde ai dat clic.

Cât timp unealta e pornită, bara liniei de comandă arată:

- **tipul** pe care îl pune unealta, ales dintre tipurile proiectului de acel fel (dacă nu există încă niciunul, primul se face când pui piesa);
- **rotația**, 0°, 90°, 180° sau 270°, pentru o piesă care stă liberă;
- **Against walls** (lângă pereți), pe care o poți opri ca piesa să stea exact unde dai clic, chiar lângă un perete.

Panoul de proprietăți arată același tip înainte de clic, cu dimensiunile lui și **Edit types…**.

![O cameră de zi, o bucătărie și un dormitor mobilate, în plan](shots/furniture-plan.webp)

Planurile desenează mobilierul prin simbolul lui, ca în convenția de desen, nu tăiat:

- canapeaua cu spătarul, brațele și pernele;
- patul cu tăblia, pernele și pilota întoarsă;
- dulapul cu bara de umerașe și ușile;
- bucătăria cu chiuveta și plita, iar corpurile de sus întrerupte.

Fațadele și secțiunile desenează corpurile, cu hașurile materialelor lor. În 3D piesele sunt corpuri low-poly făcute de Lapin.

![Casa mobilată în 3D](shots/furniture-3d.webp)

## Scaune în jurul mesei și bucătăria

**Chairs round a table** (scaune în jurul mesei): dai clic pe o masă sau pe un birou. Scaunele se pun pe laturile lungi, cam câte unul la 600 mm, și câte unul la fiecare capăt, dacă e destul de lat; masa rotundă le primește pe un cerc. Fiecare scaun privește spre masă. Biroul primește scaune doar în față. Scaunele sunt de tipul ales pentru unealta **Chair**.

**Kitchen** (bucătăria, <kbd>KT</kbd>) se desenează cu două puncte de-a lungul unui perete:

- lungimea liniei e lungimea bucătăriei, iar spatele ei stă pe fața peretelui;
- corpurile de jos se împart egal, la aproximativ 600 mm;
- are blat, chiuvetă și plită (locul lor de-a lungul bucătăriei se alege pe tip), corpuri sus și o coloană pentru frigider la un capăt.

**Length** (lungimea) bucătăriei rămâne de modificat în proprietăți, iar corpurile se împart din nou.

### Dispunerile bucătăriei

Un tip de bucătărie are o **Layout** (dispunere): **One wall** (rândul drept), **Galley** (două rânduri paralele cu un culoar între ele), **L-shaped**, **U-shaped**, **Island** (un rând pe peretele din spate și o insulă separată, cu culoarul ei) sau **Peninsula** (un L cu un rând care iese din braț, cu blatul în consolă pentru scaune înalte). Alegerea ei pornește tipul de la mărimile dispunerii, iar fiecare rămâne editabilă lângă previzualizare: lungimile brațelor de la peretele din spate, culoarul dintre rânduri, lungimea rândului opus, lungimea, adâncimea și locul insulei, lungimea și consola peninsulei și, la un L sau o peninsulă, colțul pe stânga sau pe dreapta.

![Cele șase dispuneri în plan: pe un perete, paralelă, în L, în U, cu insulă și cu peninsulă](shots/kitchen-layouts.webp)

**Sink on** și **Hob on** le pun pe rândul principal sau pe cealaltă parte (braț, rândul opus, insulă, peninsulă), **Sink at** și **Hob at** spun unde de-a lungul ei, iar **Tall unit (fridge)** stă la un capăt liber. Corpurile de colț sunt oarbe; corpurile de sus sunt doar pe rândurile lipite de un perete, nu pe insulă sau pe peninsulă.

![Un tip de bucătărie: în L](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) desenează orice dispunere cu două puncte de-a lungul peretelui pe care stă rândul principal: linia îi dă lungimea, spatele pe fața peretelui, iar un L, o peninsulă, un U sau o bucătărie paralelă se strecoară în colțul sau între pereții lângă care e trasă. **Flip** dă unui L sau unei peninsule colțul pe cealaltă parte. Extrasul de mobilier numește dispunerea și arată adâncimea amprentei. În IFC rămâne un IfcFurniture de un tip Kitchen definit de utilizator.

### Corpurile și aparatele bucătăriei

Un tip de bucătărie își poate înșira propriile **corpuri** sub parametrii lui, în **Furniture types**, pe două rânduri:

- **Base line** (rândul de jos): corpurile rândului principal, de la stânga privind din față (după un colț orb);
- **Wall line** (rândul de sus): corpurile agățate deasupra lui.

**Add** adaugă un corp de felul ales la capătul rândului. **Typical run** pornește rândul de jos de la o chiuvetă, o mașină de spălat vase, un corp cu sertare și o plită peste cuptor. Săgețile mută un corp la stânga sau la dreapta, iar coșul îl scoate.

Fiecare corp are o **lățime**. Un corp de mobilier ia orice lățime între 100 și 800 mm: alegi 150, 200, 300, 400, 450, 500, 600 sau 800 ori scrii alta. Un aparat ia dimensiunile în care se fabrică. Pe rândul de jos sunt:

- **Base unit** (corp de jos), cu **Front** (fața): un sertar peste o ușă, uși (una sau două, cu partea pe care are balamalele o ușă singură, **Hinged on the**), sertare (1–4), sertare pentru oale (un sertar mic peste două adânci) sau rafturi deschise;
- **Sink unit** (corp cu chiuvetă): chiuvetă simplă, cu 1½ cuve, dublă (de la 800) sau de colț, cu baterie obișnuită, cu duș extractibil sau fără. Chiuveta de colț intră în colțul orb când stă lângă el.
- **Hob over an oven** (plită peste cuptor), de 600, 800 sau 900, cu inducție sau pe gaz, cu sau fără cuptor dedesubt;
- **Freestanding cooker** (aragaz de sine stătător), de 500, 600 sau 900, cu inducție sau pe gaz. Blatul se oprește de o parte și de alta a lui.
- **Dishwasher** (mașină de spălat vase) încorporată, de 450 sau 600, în spatele unei fețe pe toată înălțimea, ca celelalte;
- **Fridge** (frigider): sub blat, înalt încorporat (ușa frigiderului peste ușa congelatorului) sau de sine stătător;
- **Oven tall unit** (coloană pentru cuptor), cu sertare sub cuptor și o ușă deasupra;
- **Tall unit** (coloană), cu uși sau rafturi deschise;
- **Filler** (panou de completare) și **Gap** (gol).

O coloană, un frigider sau o coloană pentru cuptor are **Height** (înălțimea) ei. Pe rândul de sus, un **Wall unit** (corp sus) are uși, uși vitrate sau rafturi deschise, **Height** (înălțimea) lui și **From the floor** (înălțimea de la pardoseală). Lăsate goale, iau valorile rândului. **Hood** (hota) e cu coș (la 650 deasupra blatului, cu tubul până sus la corpurile de sus) sau integrată într-un corp sus. **Gap** lasă peretele liber. Corpurile de sus ocolesc coloanele.

Fără un rând de sus înșirat, fiecare corp de jos primește un corp sus de lățimea lui, iar o plită sau un aragaz primește o hotă cu coș. La insulă sau peninsulă, **Hood over the hob there** agață o hotă deasupra plitei de pe ea.

Ce lasă corpurile din rând se împarte egal, ca înainte, iar sub 100 mm devine panou de completare. Corpurile care nu încap în rând sunt lăsate deoparte, iar previzualizarea și elementul o spun. **Fit to units** (potrivește la corpuri), din proprietățile bucătăriei, îi dă lungimea de care au nevoie corpurile: a lor, plus colțul orb și coloana de la capăt. Un tip fără corpuri proprii se construiește ca înainte.

În plan, fiecare corp are simbolul lui:

- deschiderea ușilor de jos, întreruptă și doar la detaliul cel mai fin;
- cuvele chiuvetei și bateria;
- zonele plitei sau arzătoarele;
- coloanele, cu diagonale;
- **DW** pe mașina de spălat vase, **F** pe frigider și **OV** pe coloana pentru cuptor;
- corpurile sus și hotele, ca linii de deasupra.

Fațadele și 3D-ul arată fiecare față, mâner și aparat.

Materialele au două roluri în plus: **Plinth** (soclul) și **Handles** (mânerele), pe lângă fronturi, corp, blat, metal și sticlă. Un tip de bucătărie făcut înainte de asta ia pentru soclu materialul corpului, iar pentru mânere pe cel al metalului.

![Corpurile unui tip de bucătărie: rândul de jos și cel de sus, cu previzualizarea motorului](shots/kitchen-units-types.webp)

![O bucătărie cu corpuri și aparate amestecate, în 3D](shots/kitchen-units-3d.webp)

**Interior → Kitchen units** face un extras cu corpurile fiecărei bucătării, de la stânga, rândul de jos apoi cel de sus, cu lățimea, înălțimea și fața sau aparatul. În IFC bucătăria rămâne un singur IfcFurniture de tipul ei, iar corpurile merg cu tipul, deci Lapin le citește înapoi.

## Proprietățile unei piese

Când selectezi o piesă, vezi:

- **Type** (tipul), felul și dimensiunile ei;
- **Rotation** (rotația);
- **Above the floor** (deasupra pardoselii), pentru o piesă ridicată pe un podest;
- **Length** (lungimea), la bucătărie.

**Flip** (întoarce) dă piesei cealaltă mână, pe loc: șezlongul pe partea cealaltă, frigiderul la celălalt capăt, colțul canapelei pe partea cealaltă. Fața ei rămâne unde era. **Modify → Mirror** face același lucru.

Mânerul din mijlocul piesei o mută; mânerul din fața ei o rotește, cu fața spre cursor.

![Proprietățile unei canapele](shots/furniture-properties.webp)

## Tipurile de mobilier

**Interior → Tipuri de mobilier** arată tipurile proiectului pe grupe (șezut, paturi, depozitare, mese, bucătărie), fiecare cu numărul de piese care îl folosesc. Alegi un fel și **New type** (tip nou), sau **Duplicate** (duplică) un tip ca să modifici o copie. Un tip are:

- **numele**, **lățimea**, **adâncimea** și **înălțimea** (la canapea, a spătarului; la pat, a tăbliei; la bucătărie, a blatului);
- parametrii felului lui:
  - canapelele și fotoliile: locuri, brațe, lățimea brațului, înălțimea șezutului, un șezlong în stânga sau în dreapta cu lungimea lui, picioare;
  - canapeaua de colț: partea pe care e colțul și lungimea acelei laturi;
  - patul: înălțimea saltelei, tăblia, tăblia de la picioare, pernele, pilota întoarsă;
  - dulapul: numărul de uși, batante sau glisante, sertare jos, soclu;
  - comoda: rânduri și coloane de sertare;
  - biblioteca: rafturi și uși jos, până la o înălțime;
  - bufetul: uși și sertare;
  - masa: dreptunghiulară, rotundă sau ovală, pe patru picioare sau pe un picior central;
  - biroul: un corp cu sertare în stânga sau în dreapta;
  - bucătăria: lățimea corpurilor de jos, soclul, blatul, locul chiuvetei și al plitei, corpurile de sus, coloana și propriile corpuri (mai sus);
- **materialele** pe roluri: corp, tapițerie, blat, fronturi, lenjerie, metal, sticlă și, la bucătărie, soclu și mânere. Scrii orice nume de material; unul pe care proiectul nu-l are se face, iar **Manage → Materials** îi stabilește culoarea și hașurile.

Previzualizarea de lângă câmpuri arată simbolul din plan și fața tipului pe măsură ce îl schimbi. Toate piesele unui tip urmează orice schimbare a lui.

![Dialogul Furniture types cu o canapea și șezlongul ei](shots/furniture-types.webp)

## Extrase și IFC

**Furniture schedule** (extrasul de mobilier) arată fiecare tip cu felul, dimensiunile, numărul de piese și încăperile în care stau, plus totalul. **Furniture by room** (mobilierul pe încăperi) arată ce stă în fiecare încăpere. Amândouă sunt în navigatorul proiectului, la **Schedules**, și se tipăresc și se exportă ca orice extras. Un tip de mobilier cu articol de deviz se plătește pe bucată în deviz.

În IFC o piesă e **IfcFurniture**, cu tipul predefinit al felului ei (SOFA, CHAIR, BED, TABLE, DESK, SHELF, sau USERDEFINED cu Wardrobe, Kitchen…). **IfcFurnitureType** e legat prin IfcRelDefinesByType, iar corpurile au materialele lor. Lapin își citește înapoi propriul mobilier ca piese native, care se pot edita. Mobilierul din alte programe intră ca referințe.
