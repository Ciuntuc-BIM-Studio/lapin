---
title: Uși și ferestre
order: 30
summary: Pui uși și ferestre în pereți, le muți și le proiectezi tipurile în configurator.
---

<!-- video doors-windows -->

## Cum pui o ușă sau o fereastră

Alege **Arhitectură → Ușă** (<kbd>DR</kbd>) sau **Fereastră** (<kbd>WN</kbd>), apoi dă clic pe perete unde vrei golul. Lapin taie golul în toate straturile peretelui și adaugă buiandrugul cerut de tipul peretelui.

Cât timp Door sau Window e activă, panoul de proprietăți arată tipul pe care îl va pune, cu dimensiunile lui: alege altul acolo înainte să dai clic pe perete.

Planul arată ușa sau fereastra înainte de clic. **Partea peretelui pe care stă cursorul e partea în care se deschide**, iar cursorul poate sta și lângă perete, nu doar pe el, așa că un perete subțire se nimerește ușor. **Space** mută balamalele pe celălalt toc și rămâne așa până îl apeși din nou. În vederea 3D, fața peretelui pe care dai clic hotărăște partea, iar Space merge la fel, fără previzualizare. Le poți schimba și după, cu **Mâna** și **Partea**.

![O ușă arătată înainte de clic, deschisă în partea cursorului](shots/fill-ghost.webp)

![Următoarea, în afara peretelui, cu cealaltă mână după Space](shots/fill-ghost-other.webp)

![Amândouă ușile puse așa cum au fost arătate](shots/fill-placed.webp)

![Un plan cu o ușă și ferestre](shots/openings-plan.webp)

Ca să muți o ușă sau o fereastră de-a lungul peretelui, selectează-o și trage de ea. Ca s-o schimbi, folosește panoul de proprietăți: tipul, înălțimea parapetului și partea în care se deschide. Cu mai multe uși și ferestre selectate, **Balamale**, **Parte**, parapetul și tipul le schimbă pe toate dintr-un pas, la fel și o săgeată de întoarcere din plan.

Pentru o distanță exactă, folosește **Mută** (<kbd>M</kbd>) sau **Copiază** (<kbd>CO</kbd>) din linia de comandă, ca la orice element: selectează ușa, fereastra sau golul din perete, dă clic pe un punct de bază, apoi tastează distanța (`500`) sau dă clic pe un al doilea punct. Contează doar partea mutării de-a lungul peretelui: golul alunecă pe perete și își păstrează parapetul. Mutat într-un alt perete de pe același nivel, trece în acel perete și se deschide în continuare la fel în cameră. O copie e o ușă sau o fereastră nouă de același tip, cu numărul următor. Dacă nu încape (dincolo de capătul peretelui sau peste alt gol), nu se mută nimic. **Rotește** și **Oglindește** îl așază la fel, iar oglindirea îi schimbă mâna.

![Proprietățile unei uși](shots/door-properties.webp)

## Goluri fără ușă sau fereastră

**Architecture → Wall opening** (<kbd>WO</kbd>) taie un gol simplu prin perete acolo unde dai clic: o trecere, un chepeng, un gol pentru o ușă care vine mai târziu. Lățimea, înălțimea și parapetul le pui în panoul de proprietăți înainte de clic. Selectat, i se schimbă acolo dimensiunile, locul de-a lungul peretelui și parapetul; ca o ușă, trebuie să încapă în perete și să nu atingă celelalte goluri ale lui. În IFC e un gol care taie peretele, fără nimic care să-l umple.

Selectat, un gol are mânere ca o fereastră: trage de mijlocul lui ca să-l muți de-a lungul peretelui sau de o ambrasură ca să-l lărgești sau să-l îngustezi, cealaltă rămânând pe loc; lungimea de lângă mouse se poate scrie, ca la desen. Se alege cu un clic în spațiul lui din plan sau în gaura lui din vederea 3D, unde i se aprinde conturul. **Mutare** (<kbd>M</kbd>) îl mută de-a lungul peretelui cu o distanță scrisă, iar înălțimea și parapetul sunt în panoul de proprietăți.

**Forma de sus** dă forma capului: **Dreptunghi**, **Arc de segment** (un arc turtit), **Arc eliptic** (o jumătate de elipsă, înaltă cât un sfert din lățime dacă nu spui altfel), **Semicerc** sau **Colțuri rotunjite** (un cap drept cu colțurile de sus rotunjite). **Înălțimea arcului** spune cât se ridică un arc de segment sau eliptic, **Raza colțurilor** cât de rotunde sunt colțurile (150 mm dacă nu spui altfel, niciodată mai mult de jumătate din lățime); goală, cea a formei. Golul din perete urmează conturul în 3D, în fațade și secțiuni și în IFC; planul îi arată spaletele ca înainte.

**Finisajul șpaleților** îmbracă glafurile și partea de sus în interiorul golului: **Tencuială** (15 mm dacă nu spui altfel) sau o **Căptușeală** (20 mm de lemn), cu **Grosimea finisajului** și **Materialul finisajului**; un gol deasupra pardoselii e finisat și dedesubt. Finisajul face parte din corpul peretelui, în 3D, fațade, secțiuni și cantități, iar golul liber e ce rămâne. **Buiandrug** e **Regula peretelui** (buiandrugul cerut de tipul peretelui, ca deasupra unei uși) sau **Niciunul**. Mai multe goluri selectate se schimbă împreună, într-un singur pas de anulare.

**Editează golul…** deschide configuratorul lui: aceleași câmpuri peste un desen al golului în perete, aplicate împreună. **Schimbă în ușă…** și **Schimbă în fereastră…** îl umplu cu o ușă sau o fereastră de tipul ales, acolo unde e: locul, parapetul și GlobalId-ul lui rămân, iar **Potrivește tipul la gol** îi dă un tip de mărimea și forma lui (al proiectului de acea mărime, altfel unul făcut din tipul ales și numit după mărime). Invers, **Schimbă în gol** la o ușă sau fereastră selectată o scoate și lasă gaura ca gol de aceeași mărime și formă. Fiecare e un singur pas de anulare.

![Un gol în perete în configuratorul lui: cap semicircular, glafurile tencuite](shots/wall-opening-dialog.webp)

## Configuratorul

**Arhitectură → Uși și ferestre** deschide configuratorul. În stânga sunt toate tipurile de uși și ferestre din proiect. Poți schimba:

- numele tipului și marca lui (litera din etichete);
- lățimea și înălțimea;
- adâncimea și lățimea tocului;
- coloanele și rândurile de panouri, și cum se deschide fiecare panou;
- finisajul glafului, ancadramentul de pe fațadă, pragul și culorile.

Desenele din dreapta arată tipul așa cum va fi desenat în fațadă, plan și secțiune, chiar în timp ce îl schimbi. Sub ele, o **previzualizare 3D** arată tipul așezat într-o bucată de perete de primul tip de perete al proiectului, cu glafurile, solbancurile, căptușeala și materialele lui, chiar în timp ce îl schimbi (și o fereastră în bovindou, arc sau cutie): trage ca să-l rotești, rotița pentru zoom, trage cu butonul drept ca să-l muți. Pornește privit din exterior.

**Stânga și dreapta.** Deschiderea unei uși se numește cum o numesc arhitecții: privești de la mâner spre balamale; o foaie care se deschide la stânga acestei direcții are deschidere stânga, una care se deschide la dreapta, deschidere dreapta. Alegerile configuratorului pentru o foaie o spun la fel, cu partea balamalelor în câmpul **Deschidere** al unui ochi, văzut din exterior, partea dinspre care se împing canatele: *Deschidere dreapta (balamale în stânga)*. Un fișier IFC numește mâna în felul lui (balamalele văzute privind în direcția în care se deschide foaia), așa că aceeași ușă e scrisă acolo SINGLE_SWING_LEFT; Lapin le traduce una în alta când scrie și citește IFC. Stânga și dreapta unei ferestre se văd din interior, partea spre care se deschid canatele: *Batant dreapta (balamale în stânga)* în configurator, ale cărui ochiuri se văd din exterior. IFC scrie aceeași fereastră ca stânga. Vederea e din interior, partea spre care se deschid canatele, dacă nu e bifat **Din exterior**; ca și planul, arată ușa așa cum stă în model. O ușă întoarsă pe cealaltă mână (**Balamale** sau o săgeată de întoarcere de-a lungul peretelui) e imaginea în oglindă a tipului ei; extrasele o pun pe o fișă a ei (vezi [Extrase](schedules.md)).

![Configuratorul de uși și ferestre](shots/joinery-configurator.webp)

**Salvează tipul** păstrează schimbările. Toate ușile și ferestrele de acel tip le urmează. **Pune cu el** închide configuratorul și începe plasarea acelui tip.

### Ochiuri, montanți și profile

O ușă sau o fereastră e o grilă de ochiuri. Fiecare ochi alege cum se deschide și ce îl umple, așa că un singur tip poate avea două foi cu ochiuri laterale și o supralumină deasupra lor, sau o fereastră cu un panou plin sub un canat.

**Coloane (relativ)** și **Rânduri (relativ)** împart golul repede: `1 1` înseamnă două ochiuri egale, `1 1 1` trei, `2 1` unul lat lângă unul îngust. Rândurile se numără de jos în sus.

**Lățimile coloanelor** și **Înălțimile rândurilor** dau aceleași dimensiuni în milimetri. Scrie-le, iar ultima ia restul. În vedere poți și trage de un montant sau de o traversă; distanța se afișează cât tragi.

**Lățime montant** și **Lățime traversă** au dimensiunile lor, separat de **Lățimea ramei**. O ramă de 50 mm cu montanți de 50 mm se citește 50 mm peste tot.

**Ochiuri** listează toate ochiurile. Clic pe unul, în listă sau în vedere, ca să-l editezi:

- **Deschidere**: fix, un canat (batant, oscilobatant, rabatabil, pe sus, pivotant, culisant) sau, la o ușă, o foaie.
- **Umplutură**: **Sticlă** sau **Panou plin**. Foile și panourile laterale ale unei uși pot fi pline, la fel un ochi de fereastră (un panou sub un canat, un ochi opac).
- **Împarte**: **În coloane** sau **În rânduri**, la o distanță în milimetri de marginea stângă sau de jos a ochiului (implicit la jumătate). Împărțită pe orizontală, foaia unei uși își păstrează partea de jos, iar partea de sus devine o supralumină fixă vitrată.
- **Unește**: cu ochiul de lângă, de deasupra sau de dedesubt, când au o latură întreagă comună. Montanții și traversele de care nu mai are nevoie niciun ochi dispar.

![O ușă de magazin într-un singur tip: două foi, ochiuri laterale și o supralumină pe toată lățimea](shots/joinery-panes.webp)

**Sistem de profile** pornește tipul de la un profil **PVC**, **Aluminiu**, **Lemn** sau **Oțel (profile subțiri)**: stabilește dimensiunile tocului, montanților, traverselor și cercevelelor și dă profilelor formele lor fine. Tocul are un **Falț** pe care se închide cerceveaua, cerceveaua se suprapune peste toc cu **Suprapunerea cercevelei**, **baghetele de fixare a geamului** țin sticla, iar o fereastră are un lăcrimar sub traversa de jos. Fiecare dimensiune rămâne editabilă. **Simplu (profile drepte)** păstrează profile dreptunghiulare simple, ca până acum.

**Toc dintr-o bibliotecă** și **Cercevea dintr-o bibliotecă** iau un profil din [bibliotecile de profile](profiles.md) (o secțiune de aluminiu sau PVC citită dintr-un DXF, de pildă): barele drepte ale tocului și cercevelei au atunci conturul lui real, cu goluri cu tot, în 3D, în secțiuni și în planuri, cu lățimea și adâncimea lui. Înlocuiește ce e forma lui reală, opritorul, buza și lăcrimarele sistemului; baghetele de geam rămân. Capetele curbe și ferestrele rotunde rămân o bandă simplă, la fel montanții și traversele grilei. **Simplu** se întoarce la un dreptunghi de aceeași mărime.

![O fereastră din PVC: cercevelele suprapuse peste toc, baghetele, un panou plin sub un ochi fix](shots/joinery-profiles-3d.webp)

La **Detalii**, **Glaf interior** și **Solbanc** pornesc și opresc glafurile, iar **Căptușeala glafurilor laterale** pornește finisajul șpaleților pe laterale și sus. Unde nu folosești glafuri și căptușeli, debifează-le.

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

**Traversă la nașterea arcului**, bifată implicit, păstrează traversa și ochiul fix de deasupra ei. Debifată, fereastra devine o cercevea în arc: rândul de sus al ochiurilor urcă în arc, iar tocul, cerceveaua, sticla, baghetele și simbolul de deschidere urmează arcul de jur împrejur, fără bară sub el. Ochiul se deschide cum îl alegi (batant, oscilobatant, fix), montanții urcă până în arc, iar planurile, secțiunile, fațadele, previzualizarea 3D și IFC-ul îl arată așa. Stilul **Cercevea în arc** pune deodată un arc de segment cu un singur canat batant.

![O cercevea în arc: un singur canat batant care urmează un arc de segment, fără traversă sub el](shots/joinery-arched-casement.webp)

**Baghete** împart ochiurile: **Grilă (colonial)** cu ochiurile pe lățime și pe înălțime, **Prairie** cu bare lângă margini, sau **Romburi**.

Pe fațadă, pe lângă ancadrament, o fereastră poate avea **Cheie de boltă**, **Coronament peste gol** (**Cornișă**, **Fronton**, **Fronton în arc** sau **Ancadrament de ploaie**, care urmează un cap în arc) și **Panou sub glaf** cu consolele lui.

**Aplică un stil…** pune deodată forma, baghetele și ornamentele: Modern, Clasic, Georgian, Victorian, Romanic, Baroc, Gotic, Tudor, Prairie, Tradițional sau o fereastră rotundă. Tot ce pune rămâne de modificat. Lista arată apoi stilul pe care îl are tipul; după ce schimbi ceva din ce a pus stilul scrie *Georgian (modificat)*, iar alegerea stilului din nou îl aplică din nou.

![O fereastră barocă: arc în plin cintru cu evantai, cheie de boltă și fronton în arc](shots/joinery-styles.webp)

![Ferestre cu arc, în 3D](shots/window-styles-3d.webp)

### Bovindouri, ferestre în arc și în cutie

Un tip de fereastră poate ieși din perete. În configurator, **Projects** o face **Bay** (un mijloc și două laturi oblice față de perete), **Bow** (panouri egale pe un arc) sau **Box** (laturi în unghi drept cu peretele), iar **New bay window** începe un tip nou ca bovindou la 45°. Fereastra umple tot un singur gol în perete, cât tipul de lată și de înaltă: se pune, se mută, se copiază, se întoarce, se numerotează și intră în extrase ca orice fereastră. **Architecture → Bay window** (<kbd>BW</kbd>) pune una în peretele pe care dai clic: tipul ales în configurator când iese din perete, altfel primul bovindou al proiectului, altfel un bovindou la 45°.

Coloanele grilei sunt fețele ei. La bovindou și la cutie prima și ultima coloană sunt laturile, coloanele dintre ele mijlocul; o fereastră în arc are o față pe coloană, lățimile lor împărțind arcul. Fiecare ochi se deschide cum spune câmpul lui **Deschidere**, deci laturile se pot deschide și mijlocul rămâne fix. Apoi:

- **Projection**, de la fața exterioară a peretelui la fața exterioară a tocurilor, iar la bovindou unghiul **Sides to the wall** (15° la 90°);
- **Sides**: laturile cutiei vitrate sau pline, cu materialul lor;
- **Seat inside**: bancheta de la glaf, cât intră în încăpere, materialul ei;
- **Sill outside**, glaful de sub fețe;
- **Base**: fără (atârnă pe perete), **pereți până la pardoseală**, **o placă în consolă** sau **o placă pe console**, cu grosimea, cât trece de fețe și materialul;
- **Head**: un acoperiș **în patru ape**, **într-o apă** sau **plat**, ori o **placă**, grosimea la streașină, cât iese, panta și materialul, și un **șorț de tablă la perete**.

Tocurile, cercevelele, sticla și mânerele sunt ale tipului, deci materialele și mânerele lui merg pe toate fețele. **Bay style** le pune pe toate deodată: bovindou la 45° sau puțin adânc la 30°, bovindou pe pardoseală, arc de cinci sau patru panouri, cutie cu laturi vitrate sau pline, erker pe console. Ca la **Aplică un stil…**, lista arată stilul de bovindou pe care îl are fereastra, cu *(modificat)* după ce schimbi ce a pus. **Save as bay style…** păstrează ce ai configurat ca stil al proiectului, oferit lângă ale lui Lapin; îl redenumești sau îl ștergi în lista de sub câmpuri.

În plan fețele sunt secționate, bancheta, glaful și baza se desenează sub secțiune, iar acoperișul punctat deasupra; fațadele, secțiunile și 3D arată tot corpul. Extrasul de ferestre are o fișă pentru tip, cu fețele desfășurate și rândul **Projecting** (felul, ieșirea, fețele); aria lui e a fețelor vitrate, iar detaliile măsoară acoperișul, bancheta și baza. În IFC e un singur IfcWindow cu partiționare definită de utilizator (*Bay 45°, projection 600, 3 faces, 4 panels*), al cărui corp e tot bovindoul, cu panourile enumerate față cu față; Lapin citește bovindoul înapoi din el.

![O fereastră în arc în configurator](shots/bay-configurator.webp)

![Bovindou, fereastră în arc, cutie și erker în plan](shots/bay-plan.webp)

![Bovindou, fereastră în arc, cutie și erker în 3D](shots/bay-3d.webp)

### Mânere

**Mâner** alege mânerul tipului: **Clanță**, **Buton**, **Bară de tragere**, **Mâner de fereastră** sau niciunul. Implicit, o ușă are clanță, iar o fereastră mâner de fereastră. O ușă are mâner pe ambele fețe, la **Înălțimea mânerului** pe care o dai; **Culoarea mânerului** îi dă culoarea. Mânerele se văd în 3D, în secțiuni și fațade, și în desenele configuratorului când e bifată **Feronerie**.

### Parametri

**Parametri · în extrase**, în partea de jos a configuratorului, ține parametrii proprii ai tipului: **+ Rezistență la foc** (păstrată în Pset_DoorCommon sau Pset_WindowCommon din IFC ca FireRating), **+ Producător**, **+ Cod produs**, **+ Site web** sau orice parametru căruia îi dai un nume. Ușile și ferestrele tipului îi moștenesc, și fiecare devine un rând al extraselor de tâmplărie: vezi [Extrase și grafice](schedules.md).

## Parasolare

**Architecture → Sunshade** (<kbd>SN</kbd>) pune un parasolar pe ușa sau fereastra pe care dai clic: cât golul ei, trecând de ancadramente, deasupra buiandrugului sau peste ea, și o urmează când se mută. **Sunshade along a facade** ia două puncte: pe un perete merge de-a lungul feței lui exterioare, în altă parte de-a lungul liniei, spre dreapta ei. Bara ambelor unelte alege tipul, sau un stil din care se face un tip nou.

Felurile:

- **Canopy** (copertină): placă, cu lamele sau din sticlă, cu ieșirea, grosimea, înclinarea și un atic, pe console, tiranți sau stâlpi;
- **Vertical fins** (aripioare verticale): adâncimea, grosimea, distanța și unghiul lor, rigle sus și jos;
- **Louvres (brise-soleil)**: lamele orizontale, plate, aerodinamice sau tubulare, adâncimea, distanța și unghiul lor, departe de perete, între plăci de capăt;
- **Egg-crate**: plăci orizontale și verticale împreună;
- **Perforated screen**: o tablă departe de perete, cu găuri rotunde sau pătrate ori fante, mărimea și pasul lor;
- **Awning** (marchiză): pânză în pantă, volan, casetă și brațe articulate;
- **Pergola**: căpriori, o grindă, șipci deasupra și stâlpi până la pardoseală;
- **External venetian blind** (jaluzea exterioară): lamele, cât e coborâtă, caseta și șinele de ghidaj.

**Sunshade types** (în Types) editează un tip: felul și fiecare parametru al lui, materialele, lângă desenul lui din exterior, în secțiune și în plan. Fiecare parasolar de acel tip urmează o schimbare. Un tip nou pornește dintr-un stil: copertină din beton, din lemn pe console, din sticlă pe tiranți, cu lamele, streașină cu țiglă, brise-soleil, aripioare verticale, grilă din beton, ecran perforat din Corten, marchiză din pânză, pergolă din lemn, jaluzea exterioară; **Save as style…** păstrează un tip ca stil al proiectului.

![Proprietățile unei copertine după calculul pentru amiază](shots/sunshade-properties.webp)

Selectat, un parasolar arată tipul, pe ce stă și **Projection** proprie (goală: a tipului). **Full shade at noon, 21 June** calculează, pentru o copertină, o marchiză sau o pergolă peste o fereastră, cât trebuie să iasă ca toată fereastra să fie în umbră când soarele e cel mai sus în cea mai lungă zi, în locul proiectului (**Manage → Location**; Bucureștiul până se pune unul), și o pune. Pe o fațadă sudică la 45° N soarele e atunci la 68,4°: o fereastră de 1,5 m sub o copertină cu 150 mm peste buiandrug are nevoie de 650 mm. Mânerele lui trag capetele de-a lungul peretelui sau liniei și ieșirea.

![Tipurile de parasolare: lamele, din exterior, în secțiune și în plan](shots/sunshade-types.webp)

Planurile desenează un parasolar de deasupra secțiunii punctat, unul care o traversează secționat (aripioare, ecran) și unul de dedesubt văzut; fațadele, secțiunile și 3D arată corpul, cu hașura materialului, și aruncă umbră cu restul (**View → Sun and shadows**). **Sunshade schedule** le enumeră pe tipuri, cu pe ce stau, lungimea și ieșirea, câte sunt și aria umbrită; cantitățile lor (lungime, ieșire, arie, volum) iau articolele de deviz ale tipului. În IFC sunt IfcShadingDevice ale unui IfcShadingDeviceType: marchiza AWNING, jaluzeaua JALOUSIE, lamelele LOUVER în IFC 4.3 (definite de utilizator în IFC4), celelalte definite de utilizator (Canopy, Fins, Egg-crate, Screen, Pergola); Lapin le citește înapoi cum erau.

![Parasolare în plan](shots/sunshades-plan.webp)

![O copertină, lamele, aripioare, o marchiză, o pergolă și o jaluzea în 3D](shots/sunshades-3d.webp)

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
