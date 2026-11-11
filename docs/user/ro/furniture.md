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

## Corpuri de mobilier și mobilier modern

**Interior → Corpuri de mobilier** pune mobilier făcut din plăci: dulapuri, biblioteci, bufete, comode TV, comode, mobilier de baie cu lavoar, birouri, mobilier de hol, paturi și noptiere. Fiecare se configurează în **Tipuri de mobilier**. Alege acolo **Corpuri de mobilier** și o **Presetare**, apoi **Tip nou**. Presetarea dă dimensiunile, baza, blatul și corpurile, iar fiecare valoare rămâne de modificat. Dacă alegi mai târziu altă presetare, tipul repornește din valorile ei și își păstrează materialele.

O piesă e făcută din unul sau mai multe **corpuri** alăturate, de la stânga, privit din față:

- **Corp** adaugă unul la capăt, butonul de împărțire îl taie în două jumătăți, săgețile îl mută, iar coșul îl scoate.
- Un corp cu **Lățime** și-o păstrează. Corpurile fără lățime împart ce lasă celelalte din lățimea piesei. **Potrivește lățimea** face piesa cât corpurile ei de lată, când toate au lățime. Corpurile care nu încap sunt lăsate deoparte, iar previzualizarea o spune.
- Un corp cu **Înălțime** proprie e mai jos decât piesa, ca partea joasă a unui mobilier de hol.
- Un **Spațiu pentru picioare** e un corp fără cutie, ca locul de sub un birou. Blatul trece peste el, iar la capătul piesei o laterală susține blatul.

Fiecare corp are **zone** suprapuse de jos în sus, cu un separator între fiecare două. Lista le arată așa cum stau, cea de sus prima. Săgeata adaugă o zonă peste cea de sus. O zonă are **Înălțimea** ei sau o **Parte din rest**, și **Fața**:

- **Rafturi deschise**;
- **Uși**: de la 1 la 4, o singură ușă cu **Balamale pe partea** stângă sau dreaptă, **Vitrate** sau nu;
- **Sertare**: de la 1 la 6, fiecare cu cutia lui în spate. **Înălțimi în proporție, de sus** le dă înălțimile: 1:1:2 face sertarul de jos de două ori mai înalt decât celelalte. Gol, sunt toate la fel.
- **Clapetă**, care **se ridică** sau **se coboară**.

În spatele rafturilor deschise, al ușilor sau al unei clapete, o zonă poate avea **Rafturi** (reglabile, dacă nu debifezi **Rafturi reglabile**) și o **Bară de umerașe**. În spatele ușilor poate avea și **Sertare interioare**, jos. Sertarele sub uși sunt două zone în același corp: sertarele jos, cu înălțimea lor, și ușile deasupra.

Secțiunile de sub presetare dau restul:

- **Construcție**: grosimea plăcilor corpului, a panoului din spate, a rafturilor și a fronturilor; blatul și fundul corpului **între laterale** sau **peste laterale**; spatele **încastrat** (la 10 mm de muchia din spate) sau **aplicat pe spate**.
- **Bază**: un **Soclu**, cu înălțimea și retragerea lui; **Picioare** (metal subțire, lemn conic sau o sanie), cu înălțimea lor; **Suspendat pe perete**, cu înălțimea feței de jos; sau **Niciunul**. Pe soclu sau pe picioare, înălțimea piesei e până la blatul ei. Suspendată, înălțimea e a piesei, iar **Agățat la** spune unde e fața ei de jos.
- **Blat**: **Niciunul** (capacele corpurilor), **Cu consolă** (depășește fața și capetele cu consola lui) sau un **Blat de lucru** (depășește doar fața), cu grosimea lui. **Decupaj pentru lavoar în blat** taie locul lavoarului unui mobilier de baie și îl desenează cu bateria lui.
- **Fronturi**: **Placă**, **Cu ramă** (montanți și traverse în jurul unei tăblii retrase) sau **Canelat** (caneluri verticale tăiate în front). Mânerele pot fi **Niciunul** (push-to-open), un **Profil J** frezat în muchia fronturilor, o **Bară**, un **Buton** sau un **Mâner pe muchie**. **Uși glisante pe toată lățimea** pune atâtea uși glisante pe două șine, în fața tuturor corpurilor, ca la un dulap.

Un pat făcut din presetarea **Pat** are cadrul, o tăblie cât tipul de înaltă, o placă de bază, salteaua și pernele. Are **Înălțimea saltelei** și sertare dedesubt, pe ambele părți, pe stânga sau pe dreapta.

Butoanele din **Stiluri** aplică un aspect modern peste valorile tipului, într-un singur pas:

- **Fără mânere, push-to-open**;
- **Profil J (mâner integrat)**;
- **Picioare subțiri de metal**;
- **Suspendat, prins de perete**;
- **Fronturi canelate**;
- **Bicolor, lemn și culoare**: corpul și blatul de stejar, fronturile verde salvie;
- **Fronturi cu ramă, butoni**.

Materialele au rolurile lor: corpul, fronturile, blatul, spatele, soclul, picioarele, mânerele, sticla, metalul (bara, bateria) și lenjeria (salteaua patului). Materialele cu care pornește Lapin sunt orientative: numește-le pe ale tale.

Fiecare placă e un solid din materialul ei. În plan piesa își arată conturul, linia fronturilor, îmbinările dintre corpuri și, la detaliul cel mai fin, deschiderea ușilor, întreruptă. O piesă agățată mai sus de 1,2 m se desenează ca element de deasupra planului de secțiune. Fațadele și 3D-ul arată plăcile, fronturile și mânerele.

Previzualizarea arată **Lista de debitare** a tipului: fiecare piesă (laterale, fund, capac, spate, separatoare, rafturi, soclu, cutiile sertarelor, apoi fronturile separat) cu corpul ei, lungimea pe direcția fibrei × lățimea × grosimea și câte sunt. **Interior → Listă de debitare** face lista de debitare a tuturor tipurilor de corpuri din proiect, înmulțită cu piesele lor. Listează doar plăcile: glisierele, balamalele, picioarele și mânerele sunt feronerie.

![Configuratorul de corpuri de mobilier: presetarea, stilurile, construcția, corpurile și zonele lor, lângă previzualizarea motorului și lista de debitare](shots/casework-types.webp)

![Corpuri de mobilier în 3D: un bufet pe picioare conice, o comodă TV canelată suspendată, un dulap și un birou](shots/casework-3d.webp)

În IFC o piesă de mobilier din corpuri e un IfcFurniture al tipului ei, cu tipul predefinit al presetării (SHELF pentru o bibliotecă, DESK, BED sau USERDEFINED cu Wardrobe, Sideboard, TV unit, Bathroom vanity…). Corpurile ei călătoresc cu tipul, așa că Lapin le citește înapoi. Felurile dulap, bufet și comodă făcute înainte rămân cum erau.

## Mobilier din modele

**Interior → Dintr-un model** pune mobilier moale și cu forme, făcut din modele gata: canapele, o canapea cu șezlong, secțiuni de canapea de colț, un taburet, fotolii, un fotoliu reclinabil, scaune, un scaun de birou, scaune de bar, bănci, mese de dining, rotunde și de cafea, o măsuță laterală, lămpi de podea, de masă, suspendate și aplice, plante în ghiveci și covoare. Modelele sunt rețele de triunghiuri simple, sub licența CC0 (Furniture Kit de la Kenney), livrate cu Lapin, cu sursa și licența fiecăruia înregistrate.

Un model se **întinde pe zone**: pe fiecare axă are o bandă care se întinde, iar ce e înainte și după ea își păstrează mărimea. Dacă lățești o canapea, brațele și picioarele rămân cum sunt și crește șezutul, așa că o canapea de două locuri devine una de trei. Dacă faci o masă mai înaltă, cresc doar picioarele, iar blatul își păstrează grosimea. Mesele rotunde, abajururile lămpilor și plantele se scalează întregi pe orizontală. Fiecare model are o mărime minimă și una maximă; o mărime dincolo de ele rămâne la limită.

În **Tipuri de mobilier**, alege **Dintr-un model** și un model, apoi **Tip nou**. Un tip dintr-un model are:

- **galeria** de modele pe grupe (canapele, fotolii, scaune, mese, lămpi, plante, covoare): dă clic pe alt model ca să schimbi modelul tipului, iar tipul ia mărimea și materialele acelui model;
- **lățimea**, **adâncimea** și **înălțimea**, fiecare între limitele modelului (ține mouse-ul pe un câmp ca să le vezi). Previzualizarea de alături e a motorului, așa că arată întinderea pe măsură ce scrii: planul văzut de sus și fața în culorile tipului;
- **Agățat la**, pentru o lampă suspendată, o aplică sau o lampă de masă: înălțimea feței de jos;
- **Stilurile** (Nordic, Închis, Cald, Delicat), care pun materialele tuturor părților dintr-un singur pas, și câte un rând de culori pentru fiecare parte: tapițeria, accentul (bordura unui covor, a doua țesătură), corpul, blatul, picioarele, metalul, sticla, abajurul, frunzișul și ghiveciul. Materialele sunt orientative: scrie-le pe ale tale în materialele de dedesubt.

În plan o piesă dintr-un model e corpul ei văzut de sus, desenat de motor fără liniile ascunse; o canapea își arată și locurile, la 1:50 și mai fin. O lampă agățată mai sus de 1,2 m se desenează ca element de deasupra planului de secțiune. Mesele, scaunele, covoarele și lămpile de sine stătătoare stau unde dai clic; canapelele, fotoliile și măsuțele laterale se lipesc de cel mai apropiat perete, ca restul mobilierului.

![Dialogul Tipuri de mobilier cu o canapea dintr-un model: galeria, mărimea, stilurile și culorile, lângă previzualizarea motorului](shots/furniture-models-types.webp)

![O cameră de zi mobilată din modele: o canapea întinsă la 2,6 m, fotolii, o măsuță de cafea pe un covor, o lampă de podea și plante](shots/furniture-models-3d.webp)

În IFC o piesă dintr-un model e un IfcFurniture al tipului ei, cu tipul predefinit al modelului (SOFA, CHAIR, TABLE sau USERDEFINED cu Floor lamp, Plant, Rug…), iar corpul ei e rețeaua întinsă, ca B-rep fațetat sau ca set de fețe, după cum cere exportul. Modelul și mărimea călătoresc cu tipul, așa că Lapin le citește înapoi. Extrasul de mobilier le listează pe grupe și modele.

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

## Biblioteca mea de mobilier

Un tip configurat bine (o bucătărie, un dulap, un birou din plăci, o canapea) poate fi păstrat pentru orice proiect în **Mobilierul meu**, biblioteca ta de pe acest calculator.

**Salvează în biblioteca mea…** din **Tipuri de mobilier** păstrează tipul ales. Îi dai:

- un **nume**;
- o **categorie**: bucătărie, living, dormitor, baie, birou, depozitare sau altele;
- **etichete**, despărțite prin virgulă (stejar, modern…);
- un **cod de articol** și un **preț orientativ**, dacă vrei. Amândouă sunt ale tale: Lapin nu vine cu prețuri.

Miniatura e desenată din previzualizarea tipului. Tipul pleacă cu tot ce îi trebuie: configurația și materialele lui, cu culorile, hașurile și imaginile lor.

![Salvează în biblioteca mea: numele, categoria, etichetele, codul de articol și prețul, lângă miniatură](shots/furniture-save-library.webp)

**Interior → Mobilierul meu…** (sau **Mobilierul meu…** din Tipuri de mobilier) arată biblioteca în miniaturi. O cauți după nume, etichetă, articol sau fel și o filtrezi după categorie și după etichetă. Pentru fiecare element:

- **Inserează** pune tipul în proiectul deschis și îl alege pentru unealta lui. Un material pe care proiectul îl are deja cu acel nume e folosit așa cum e, iar celelalte se creează. Dacă proiectul are deja un tip identic, se folosește acela și nu se face o copie. Fiecare inserare e un singur pas de anulare.
- **Așază** îl inserează și pornește unealta lui, ca următorul clic să pună o piesă. O bucătărie se desenează de-a lungul unui perete.
- **Redenumește**, **Etichete**, **Duplică** și **Șterge** schimbă doar biblioteca. Proiectele care au deja tipul îl păstrează.
- **Exportă** scrie elementul ca fișier `.lapinlib`. **Exportă tot…** scrie toată biblioteca, iar **Importă…** citește un fișier înapoi. Importul sare peste un element care e deja în bibliotecă neschimbat. Un element cu același id, dar cu alt conținut, se adaugă lângă cel pe care îl ai, ca nimic să nu fie suprascris.
- **Adaugă într-un șablon…** pune tipul într-unul din șabloanele tale de proiect, ca orice proiect pornit din el să aibă tipul.

![Mobilierul meu: miniaturile bibliotecii, căutate și filtrate, fiecare gata de inserat sau de așezat](shots/furniture-library.webp)

Pe desktop biblioteca e păstrată în dosarul tău Lapin, câte un fișier pentru fiecare element. În browser o păstrează browserul. Ca s-o muți între cele două sau pe alt calculator, o exporți și imporți fișierul. Un șablon de proiect păstrează tipurile de mobilier ale proiectului din care e salvat. **Salvează ca șablon…** poate adăuga și toate tipurile din Mobilierul meu (**Adaugă toate tipurile din Mobilierul meu**).

## Extrase și IFC

**Furniture schedule** (extrasul de mobilier) arată fiecare tip cu felul, dimensiunile, numărul de piese și încăperile în care stau, plus totalul. **Furniture by room** (mobilierul pe încăperi) arată ce stă în fiecare încăpere. Amândouă sunt în navigatorul proiectului, la **Schedules**, și se tipăresc și se exportă ca orice extras. Un tip de mobilier cu articol de deviz se plătește pe bucată în deviz.

În IFC o piesă e **IfcFurniture**, cu tipul predefinit al felului ei (SOFA, CHAIR, BED, TABLE, DESK, SHELF, sau USERDEFINED cu Wardrobe, Kitchen…). **IfcFurnitureType** e legat prin IfcRelDefinesByType, iar corpurile au materialele lor. Lapin își citește înapoi propriul mobilier ca piese native, care se pot edita. Mobilierul din alte programe intră ca referințe.
