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

### Cote temporare

Selectează în plan o ușă, o fereastră sau un gol din perete și Lapin desenează **cote temporare** de-a lungul peretelui, pe fața în care vin pereții care îl întâlnesc: de la fiecare muchie a golului la cea mai apropiată față a unui perete care îi întâlnește sau îi traversează peretele (sau la capătul peretelui însuși) și, când un alt gol din același perete e mai aproape, încă una, punctată, până la acel gol. Urmăresc golul cât timp îl tragi și se arată și când pui unul cu **Ușă** sau **Fereastră**, înainte de clic.

- **Dă clic pe o valoare** ca s-o tastezi, în unitatea proiectului (merg și sume ca `1200+300`), și apasă **Enter**: golul alunecă pe perete până când cota arată valoarea, celălalt capăt rămânând pe loc. Un singur pas de anulare. **Escape** o lasă cum era. Dacă golul n-ar mai încăpea (dincolo de capătul peretelui sau peste alt gol), nu se mută nimic, iar mesajul spune de ce.
- **Dă clic pe punctul de la capătul golului** ca să măsori de la mijlocul lui în loc de muchie.
- **Dă clic pe punctul de la celălalt capăt** ca să măsori până la următorul lucru de acolo: fața apropiată a unui perete, fața depărtată, fețele miezului (la un perete în straturi), axul; muchia apropiată a altui gol, mijlocul lui, muchia depărtată.
- **Dă clic pe lacăt**, lângă o valoare, ca s-o păstrezi ca o cotă permanentă a planului, legată de ce se află la capetele ei, ca una desenată cu **Adnotări → Aliniată**. Nu blochează distanța: golul se poate muta în continuare.

Așadar „colțul acestei ferestre la 1500 de fața acelui perete” înseamnă: selectează fereastra, dă clic pe valoarea spre perete, tastează `1500`, Enter. „Axul acestei ferestre la 2500 de axul celeilalte ferestre” înseamnă: dă clic pe punctul de la capătul ferestrei și pe cel de la capătul celeilalte ferestre până când amândouă măsoară mijloacele, apoi tastează `2500`.

La un **perete curb** cotele merg pe arc, concentrice cu el, și arată lungimea de-a lungul feței pe care sunt desenate (fața exterioară e mai lungă decât cea interioară); o valoare tastată mută golul pe arc. Lacătul lor face o cotă de lungime de arc a planului, care își păstrează valoarea așa cum a fost desenată: nu urmărește peretele.

Într-o **secțiune sau fațadă** care vede peretele golului din față (peretele merge de-a lungul vederii, în limita a 10°), golul selectat are cotele de-a lungul peretelui deasupra lui, până la aceiași pereți și goluri, pe fața pe care o vede vederea, și două pe lângă el, în sus: de la cota nivelului până la partea lui de jos (**înălțimea parapetului**) și până la partea de sus (**înălțimea buiandrugului**). Tastarea oricăreia mută golul în sus sau în jos; cealaltă urmează. Un perete tăiat oblic sau văzut din profil, un perete curb și o secțiune frântă sau curbă nu arată cote.

![O fereastră selectată, cotată până la fața interioară a colțului și până la fereastra următoare](shots/temp-dims.webp)

![După ce s-a tastat 1500: colțul ei la 1500 de fața peretelui](shots/temp-dims-typed.webp)

![Axul celeilalte ferestre la 2500 de axul primei](shots/temp-dims-axes.webp)

![O fereastră dintr-un perete curb, cotată pe arc, la 1000 de fața peretelui care îl traversează](shots/temp-dims-curved.webp)

![O fereastră selectată în fațada de sud: de-a lungul peretelui și înălțimile parapetului și buiandrugului](shots/temp-dims-elevation.webp)

## Goluri fără ușă sau fereastră

**Architecture → Wall opening** (<kbd>WO</kbd>) taie un gol simplu prin perete acolo unde dai clic: o trecere, un chepeng, un gol pentru o ușă care vine mai târziu. Lățimea, înălțimea și parapetul le pui în panoul de proprietăți înainte de clic. Selectat, i se schimbă acolo dimensiunile, locul de-a lungul peretelui și parapetul; ca o ușă, trebuie să încapă în perete și să nu atingă celelalte goluri ale lui. În IFC e un gol care taie peretele, fără nimic care să-l umple.

Selectat, un gol are mânere ca o fereastră: trage de mijlocul lui ca să-l muți de-a lungul peretelui sau de o ambrasură ca să-l lărgești sau să-l îngustezi, cealaltă rămânând pe loc; lungimea de lângă mouse se poate scrie, ca la desen. Se alege cu un clic în spațiul lui din plan sau în gaura lui din vederea 3D, unde i se aprinde conturul. **Mutare** (<kbd>M</kbd>) îl mută de-a lungul peretelui cu o distanță scrisă, iar înălțimea și parapetul sunt în panoul de proprietăți.

**De-a lungul peretelui**, în panoul de proprietăți (al unui gol, al unei uși sau al unei ferestre), e o distanță cu două alegeri alături: **Măsurat de la** (începutul peretelui, fața celui mai apropiat perete care îl întâlnește sau fața miezului acelui perete) și **Măsurat până la** (începutul, centrul sau sfârșitul golului). O valoare tastată mută golul astfel încât distanța să o arate, referința rămânând pe loc, într-un singur pas de anulare. Alegerile schimbă doar felul în care e citită aceeași poziție, deci nu se păstrează nimic în model; Lapin ține minte ultima ta alegere.

![De-a lungul peretelui al unei ferestre în proprietăți: distanța, măsurată de la și până la](shots/along-field.webp)

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

**Lățimea unui montant sau a unei traverse.** Fă clic pe un montant sau pe o traversă în elevație (un clic, nu tragere) și este ales: apare **Lățime**, cu **Fără montant** (**Fără traversă**) ca să îl scoți și cele două ochiuri să se întâlnească, și **Implicit (al tocului)** ca să primească din nou lățimea comună a montanților. Fiecare își păstrează lățimea când adaugi, împarți sau unești coloane și rânduri.

### Uși duble și canate care se întâlnesc

**Ușă dublă nouă** pornește o pereche de canate fără montant între ele (1500 × 2100 mm). Oricare două canate de ușă, unul cu balamalele la stânga și unul la dreapta, se întâlnesc când montantul dintre ele este 0; luminatoarele laterale de lângă ele își păstrează montanții.

Unde se întâlnesc două canate, **Canate care se întâlnesc** spune cum:

- **Cap la cap**: canatele doar se ating.
- **Cu falț**: un montant de întâlnire, canatul activ acoperind canatul pasiv cu **Suprapunere falț**.
- **Șină de acoperire**: o șină (astragal) peste rost, cu **lățime** și **grosime**.

**Canatul activ** (**Stânga** sau **Dreapta**, așa cum se citește grila, cel stâng având balamalele la stânga, văzut din exterior) se deschide primul și are mânerul; **Zăvoare îngropate în canatul pasiv** pune zăvoare în celălalt. O singură setare servește toate perechile tipului, iar canatele care se deschid în ambele sensuri se întâlnesc mereu cap la cap. Un tip făcut înainte păstrează canatele cap la cap, ambele cu mânere.

![O ușă dublă: fără montant între canate, cu falț, canatul drept activ](shots/joinery-double-door.webp)

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

### Sticlă fără toc

**Sticlă fără toc** face o ușă sau o fereastră doar din sticlă securizată, fără toc și fără cercevea; stilul **Sticlă fără toc** o face dintr-odată, cu o foaie pe pivoți. La o ușă, **Prindere** spune ce ține foaia: **Pivoți și feronerie de colț** (un amortizor în pardoseală și un pivot sus, câte o piesă de colț pe partea balamalelor și o broască de colț jos, în partea opusă) sau **Balamale de sticlă** (două, trei la o foaie mai înaltă de 2,30 m, prinse de sticlă și fixate de perete sau de panoul de alături). **Grosimea sticlei** e între 6 și 25 mm (12 mm implicit), **Rostul în jurul sticlei** e jocul față de gol și dintre panouri, **Rostul sub foaie** cel deasupra pardoselii. O foaie se deschide batant, în ambele sensuri sau culisează; un panou de lângă ea sau de deasupra e fix, stând într-un profil U pe pardoseală și sub buiandrug. O foaie culisantă atârnă de o șină pe toată lățimea golului, în fața panourilor fixe, spre cameră, și trece cu 50 mm peste ele, ca la un perete despărțitor de sticlă sau la un paravan de duș. Mânerul e o bară pe ambele fețe, dacă nu alegi altul. O fereastră fără toc e un vitraj fix. Planurile desenează foaia deschisă ca sticla ei, elevațiile feroneria și șina; în IFC tipul nu are toc (grosimea lui e a sticlei).

![O ușă de sticlă fără toc, pe pivoți cu feronerie de colț, în configurator](shots/joinery-frameless-door.webp)

**Arc de pardoseală (balama de pardoseală)** prinde foaia batantă pe un arc îngropat în pardoseală sub pivotul ei, cu un pivot sus și o feronerie de colț deasupra; **Ușă nouă din sticlă fără toc pe arc de pardoseală** pornește una (1000 × 2100, pendulară, blocată deschisă la 90°). **Arc de pardoseală** alege modelul după cea mai grea și mai lată foaie pe care o duce și forța de închidere (EN 1154); **Automat (cel mai ușor care duce foaia)** alege pentru dumneavoastră (sticla de 12 mm cântărește 30 kg/m²), iar un model prea slab pentru foaie este refuzat. **Sensul de batere** este **Batere într-un sens** sau **Batere în ambele sensuri (pendulară)**, cu ambele deschideri desenate în plan; **Pivotul de la margine** este 55 mm implicit (30 până la 150 mm); **Feroneria de jos** este o **Feronerie de colț jos** sau o **Clemă de călcâi**; **Blocare deschisă** este niciuna, 80°, 90°, 105° sau 120°. Cutia arcului stă sub pardoseala finită: planurile îi desenează capacul și rotesc foaia în jurul pivotului, fațadele desenează cutia întreruptă și axa pivotului. Tabelul ușilor spune cum e prinsă foaia, iar IFC o scrie în setul `Lapin_FloorSpring` (modelul, greutatea foii, distanța pivotului, feroneria, unghiul de blocare). Modelele sunt orientative: verificați datele producătorului.

![O ușă de sticlă fără toc pe arc de pardoseală, pendulară, cu clemă de călcâi, în configurator](shots/joinery-floor-spring.webp)

**Baghete** împart ochiurile: **Grilă (colonial)** cu ochiurile pe lățime și pe înălțime, **Prairie** cu bare lângă margini, sau **Romburi**.

Pe fațadă, pe lângă ancadrament, o fereastră poate avea **Cheie de boltă**, **Coronament peste gol** (**Cornișă**, **Fronton**, **Fronton în arc** sau **Ancadrament de ploaie**, care urmează un cap în arc) și **Panou sub glaf** cu consolele lui.

**Aplică un stil…** pune deodată forma, baghetele și ornamentele: Modern, Clasic, Georgian, Victorian, Romanic, Baroc, Gotic, Tudor, Prairie, Tradițional sau o fereastră rotundă. Tot ce pune rămâne de modificat. Lista arată apoi stilul pe care îl are tipul; după ce schimbi ceva din ce a pus stilul scrie *Georgian (modificat)*, iar alegerea stilului din nou îl aplică din nou.

![O fereastră barocă: arc în plin cintru cu evantai, cheie de boltă și fronton în arc](shots/joinery-styles.webp)

![Ferestre cu arc, în 3D](shots/window-styles-3d.webp)

### Uși de garaj

**New garage door** (Ușă de garaj nouă) pornește o ușă secțională basculantă, 2400 × 2100, felul de ușă din panouri de oțel care se ridică. La **Garage door**, **Kind** (Fel) o face ușă **Secțională basculantă** (panouri orizontale care urcă și merg înapoi sub tavan pe două șine), ușă **Rulou** (o perdea de lamele care se rulează pe un tambur într-o cutie deasupra golului), ușă **Basculantă** (un singur panou care se ridică) sau ușă **Secțională culisantă lateral** spre stânga sau spre dreapta (panouri verticale care trec după colț, de-a lungul unui perete lateral). **Common size** (Mărime uzuală) oferă mărimile Australiei, 2400, 2700, 3000, 4800 și 5400 × 2100, și ale Europei, 2375 × 2000 și 2500, 3000 și 5000 × 2125; orice altă mărime se scrie la **Width** și **Height**. **Finish** (Finisaj) este **Nervurat**, **Plan**, **Cu caneluri în V** sau **Panoul de sus vitrat**; **Panels** (Panouri) se ia din înălțimea ușii (cam 610 mm fiecare) dacă nu-l scrieți. O ușă secțională are o **Șină** (**Standard**, **Spațiu redus deasupra** sau **Ridicare înaltă**, care urcă întâi o **Ridicare suplimentară**) și poate avea o **Ușă de acces pietonal** (600 până la 1200 mm lățime, la stânga, la mijloc sau la dreapta, văzut din interior); **Cutia ruloului** unei uși rulou este **în interior** sau **în exterior**; **Mecanismul** unei uși basculante este **cu copertină** (o parte rămâne afară când e deschisă) sau **Retractabil** (merge înapoi pe șine). **Spațiul deasupra golului**, **Adâncimea necesară** (cât intră în garaj, de la perete) și **Spațiul lateral** se calculează după fel; scrieți-le pe ale producătorului, iar **Auto** le aduce înapoi. **Tracks in 3D** (Șinele în 3D) arată sau ascunde corpurile șinelor.

O ușă de garaj se montează în spatele golului: fața interioară a tocului pe finisajul interior al peretelui (**Position in the wall** → **De la finisajul interior**). Planurile o desenează ca bibliotecile CAD: perdeaua în gol și, întrerupt deasupra, cele două șine ale unei uși secționale până în adâncime, cu ușa deschisă sub tavan, cutia unui rulou, panoul deschis al unei uși basculante, șina unei uși culisante laterale după colț și ușa strânsă de-a lungul peretelui lateral. Fațadele desenează rosturile panourilor, nervurile, ferestrele și ușa de acces; secțiunile perdeaua tăiată și șinele văzute. Tabelul ușilor spune felul deschiderii și, în rândul despre prindere, panourile, finisajul, șina sau cutia și spațiul de care are nevoie ușa; cantitățile adaugă setul `Lapin_GarageDoor` (aria perdelei, panourile, spațiul deasupra, adâncimea și spațiul lateral). În IFC este un IfcDoor al cărui OperationType este cel mai potrivit din fiecare schemă: ROLLINGUP pentru o ușă rulou în amândouă; LIFTING_HORIZONTAL pentru o ușă secțională sau basculantă în IFC4X3, USERDEFINED cu SECTIONAL_OVERHEAD sau UP_AND_OVER în IFC4; USERDEFINED cu SECTIONAL_SIDE_LEFT sau …_RIGHT pentru una culisantă lateral. Lapin își citește înapoi fișierul întreg, iar ușa care se rulează sau se ridică dintr-un fișier străin o citește ca ușă de garaj. **New side-hinged garage doors** (Uși de garaj batante noi) face o pereche de canate de mărimea unui garaj (2400 × 2100), cu falț la întâlnire. Mărimile și spațiile sunt orientative: verificați la producător.

![O ușă de garaj secțională, 2700 × 2100 cu ușă de acces pietonal, în configurator](shots/joinery-garage-door.webp)

![Ușa de garaj în plan: șinele întrerupte până în adâncime](shots/garage-door-plan.webp)

![Ușa de garaj secțională în 3D](shots/garage-door-3d.webp)

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

### Poziția tocului și solbancurile

**Position in the wall** (poziția în perete) stabilește unde stă tocul pe grosimea peretelui ca valoare, nu doar centrat: alege **From the exterior finish**, **From the core** sau **From the wall’s centre**, apoi **Set back**, cât de în spatele acelei referințe e fața exterioară a tocului (de la centru, cât de departe e centrul tocului). *From the exterior finish* și 50 mm pune tocul la 50 mm în spatele tencuielii. Tocul, șpaleții, solbancurile și simbolurile de deschidere se mută odată cu el, în planuri, secțiuni, 3D și IFC. Un tip făcut fără poziție rămâne **Centred in the wall**. În panoul de proprietăți, o ușă sau o fereastră are **Frame in the wall** ca să-și dea o poziție proprie (**As the type** o lasă pe a tipului) și **Set back** pentru distanța ei.

![Configuratorul cu tocul la 50 mm în spatele finisajului exterior](shots/joinery-position.webp)

**Exterior sill** (solbancul exterior) are o **Slope** (pantă) în grade (5° la început; 15° pentru un solbanc metalic). Un solbanc înclinat coboară dinspre toc și scoate din perete tot ce e deasupra lui: tencuiala, miezul și glaful sunt tăiate după fața lui înclinată, în 3D, în secțiuni și fațade și în cantități.

### Date: valoarea U, rezistența la foc, feronerie

**Data…**, sub tip în configurator (și în panoul de proprietăți, pentru ușile și ferestrele selectate), deschide o casetă de text liber: **U-value**, **g-value**, **Safety glass** (laminată pe interior, securizată pe ambele fețe…), **Fire rating** (EI 30, EI2 60-C5…) și **Fittings**; **More data** adaugă oricâte ale tale, de exemplu *Sauna window* sau *Frosted glass*. Nimic nu se alege dintr-o listă. Valoarea proprie a unei uși sau ferestre are prioritate față de cea a tipului; un câmp gol revine la a tipului (arătată șters).

![Caseta de date a unui tip de fereastră](shots/joinery-data.webp)

Sunt parametrii tipului: extrasele de uși și ferestre arată fiecare pe un rând, etichetele le citesc ca `{U-value}`, `{g-value}`, `{Safety glass}`, `{Fire rating}` și `{Fittings}`, iar IFC le păstrează. Rezistența la foc este `FireRating` din `Pset_WindowCommon` sau `Pset_DoorCommon`; o valoare U care se citește ca număr (0.9 sau *0,9 W/m²K*) este acolo `ThermalTransmittance`, cu `GlazingAreaFraction` pentru fereastră; restul e în setul `Lapin_Joinery`.

![Datele proprii ale unei ferestre în panoul de proprietăți](shots/joinery-data-properties.webp)

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
