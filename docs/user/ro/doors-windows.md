---
title: Uși și ferestre
order: 30
summary: Pui uși și ferestre în pereți, le muți și le proiectezi tipurile în configurator.
---

<!-- video doors-windows -->

## Cum pui o ușă sau o fereastră

Orice gol pornește dintr-o singură unealtă, **Arhitectură → Gol**. Prima ei alegere, în bara liniei de comandă și în capul panoului de proprietăți, e ce pui: într-un perete o **Ușă**, o **Fereastră**, un **Bovindou** sau un **Gol de o formă**, fără nimic în el; într-o placă un **Gol în placă**; într-un acoperiș o **Fereastră de acoperiș** sau o **Lucarnă**. Fiecare fel se pune cum arată această pagină și [Plăci și acoperișuri](floors-roofs.md). Literele vechilor unelte o pornesc deja aleasă: <kbd>DR</kbd> o ușă, <kbd>WN</kbd> o fereastră, <kbd>BW</kbd> un bovindou, <kbd>WO</kbd> un gol în perete, <kbd>SH</kbd> un gol în placă; locurile rapide și Uneltele mele care țin vechile unelte merg mai departe.

![Unealta Gol: ce pune, ales după gazdă, și tipul de ușă pe care îl pune](shots/opening-tool.webp)

Pentru o ușă sau o fereastră, alege la **Gol** **Ușă** (<kbd>DR</kbd>) sau **Fereastră** (<kbd>WN</kbd>), apoi dă clic pe perete unde vrei golul. Lapin taie golul în toate straturile peretelui și adaugă buiandrugul cerut de tipul peretelui.

Cât timp Door sau Window e activă, panoul de proprietăți arată tipul pe care îl va pune, cu dimensiunile lui: alege altul acolo înainte să dai clic pe perete.

Planul arată ușa sau fereastra înainte de clic. **Partea peretelui pe care stă cursorul e partea în care se deschide**, iar cursorul poate sta și lângă perete, nu doar pe el, așa că un perete subțire se nimerește ușor. **Space** mută balamalele pe celălalt toc și rămâne așa până îl apeși din nou. În vederea 3D, fața peretelui pe care dai clic hotărăște partea, iar Space merge la fel, fără previzualizare. Le poți schimba și după, cu **Mâna** și **Partea**.

![O ușă arătată înainte de clic, deschisă în partea cursorului](shots/fill-ghost.webp)

![Următoarea, în afara peretelui, cu cealaltă mână după Space](shots/fill-ghost-other.webp)

![Amândouă ușile puse așa cum au fost arătate](shots/fill-placed.webp)

![Un plan cu o ușă și ferestre](shots/openings-plan.webp)

Ca să muți o ușă sau o fereastră de-a lungul peretelui, selectează-o și trage de ea. Ca s-o schimbi, folosește panoul de proprietăți: tipul, înălțimea parapetului și partea în care se deschide. Cu mai multe uși și ferestre selectate, **Balamale**, **Parte**, parapetul și tipul le schimbă pe toate dintr-un pas, la fel și o săgeată de întoarcere din plan.

**Parapet măsurat de la.** Înălțimea parapetului se măsoară de la **nivelul structural** al peretelui (baza lui) sau de la **pardoseala finită** de deasupra, după cum lucrezi. Selectează o ușă, o fereastră sau un gol și alege **Parapet măsurat de la** în panoul de proprietăți (și în **Editează golul…** și în opțiunile uneltei Gol, înainte să-l pui). Alegerea singură lasă elementul unde este și citește numărul parapetului față de noua referință; dacă scrii un parapet nou, elementul se mută. Pardoseala finită peste baza peretelui este **Pardoseala finită deasupra bazei** a peretelui (0 până la 1 m, în proprietățile lui), altfel cea a tipului de perete (în **Tipuri de pereți…**), altfel 0: cât timp niciuna nu e setată, „Pardoseală finită” dă aceleași înălțimi ca „Nivel structural”, iar o notă o spune. Ușile, ferestrele și golurile noi iau implicitul proiectului, setat în **Gestionare → Standarde de desen → Proiect**, la **Parapetele ușilor și ferestrelor măsurate de la**; apoi fiecare îl păstrează pe al ei. Elementele din IFC importat se măsoară de la nivelul structural, cum le dă fișierul.

**Un tip, multe dimensiuni.** **Lățimea** și **Înălțimea** unei uși sau ferestre din panoul de proprietăți sunt ale ei: tipul ține construcția (tocul, cercevelele, profilele, materialele, datele) și dimensiunea la care se pun cele noi, așa că un singur tip ține toate dimensiunile aceleiași ferestre, 1200 × 1200, 900 × 1500 sau 1800 × 1200. Scrie o lățime sau o înălțime ori trage de una dintre ambrasurile ușii sau ferestrei selectate în plan, cealaltă rămânând pe loc: golul, simbolul din plan, 3D-ul, cantitățile, etichetele și exportul IFC (OverallWidth și OverallHeight) urmează, iar ea trebuie să încapă în perete ca la mutare. Rămâne de tipul ei: o schimbare a tipului (profilele, ochiurile, materialele) ajunge și la ea, iar o schimbare a dimensiunii tipului ajunge doar la cele rămase la dimensiunea tipului. **Mărimea tipului** renunță la dimensiunea proprie. Tabelele listează un tip o dată pentru fiecare dimensiune folosită.

Pentru o distanță exactă, folosește **Mută** (<kbd>M</kbd>) sau **Copiază** (<kbd>CO</kbd>) din linia de comandă, ca la orice element: selectează ușa, fereastra sau golul din perete, dă clic pe un punct de bază, apoi tastează distanța (`500`) sau dă clic pe un al doilea punct. Contează doar partea mutării de-a lungul peretelui: golul alunecă pe perete și își păstrează parapetul. Mutat într-un alt perete de pe același nivel, trece în acel perete și se deschide în continuare la fel în cameră. O copie e o ușă sau o fereastră nouă de același tip, cu numărul următor. Dacă nu încape (dincolo de capătul peretelui sau peste alt gol), nu se mută nimic. **Rotește** și **Oglindește** îl așază la fel, iar oglindirea îi schimbă mâna.

![Proprietățile unei uși](shots/door-properties.webp)

**Deschis sau închis.** Câmpul **Stare** al unei uși sau ferestre din panoul de proprietăți (când e editabilă) alege cum se arată: **Implicit (deschisă în plan, închisă în 3D)**, **Deschisă (plan și 3D)** sau **Închisă (plan și 3D)**; cu mai multe selectate, le schimbă pe toate. Deschisă, o canat pe balamale stă rotită cu un sfert de tură spre interior în 3D, iar una glisantă e împinsă într-o parte, aproape cât lățimea ochiului ei; tocul rămâne. În plan, **Deschisă** desenează canatul deschis, cu arcul lui, iar **Închisă** îl desenează închis, fără arc.

**Ferestre glisante.** Canaturile unei ferestre glisante, când un rând are mai mult de unul, merg pe două șine: unul din două pe cea interioară, celelalte pe cea exterioară, așa că se încrucișează și stau pe planuri diferite în 3D, în secțiune și în plan.

**Deschis sau închis.** Câmpul **Stare** al unei uși sau ferestre din panoul de proprietăți (când e editabilă) alege cum se arată: **Implicit (deschisă în plan, închisă în 3D)**, **Deschisă (plan și 3D)** sau **Închisă (plan și 3D)**; cu mai multe selectate, le schimbă pe toate. Deschisă, o canat pe balamale stă rotită cu un sfert de tură spre interior în 3D, iar una glisantă e împinsă într-o parte, aproape cât lățimea ochiului ei; tocul rămâne. În plan, **Deschisă** desenează canatul deschis, cu arcul lui, iar **Închisă** îl desenează închis, fără arc.

**Ferestre glisante.** Canaturile unei ferestre glisante, când un rând are mai mult de unul, merg pe două șine: unul din două pe cea interioară, celelalte pe cea exterioară, așa că se încrucișează și stau pe planuri diferite în 3D, în secțiune și în plan.

### Cote temporare

Selectează în plan o ușă, o fereastră sau un gol din perete și Lapin desenează **cote temporare** de-a lungul peretelui, pe fața în care vin pereții care îl întâlnesc: de la fiecare muchie a golului la cea mai apropiată față a unui perete care îi întâlnește sau îi traversează peretele (sau la capătul peretelui însuși) și, când un alt gol din același perete e mai aproape, încă una, punctată, până la acel gol. Urmăresc golul cât timp îl tragi și se arată și când pui unul cu **Ușă** sau **Fereastră**, înainte de clic.

- **Dă clic pe o valoare** ca s-o tastezi, în unitatea proiectului (merg și sume ca `1200+300`), și apasă **Enter**: golul alunecă pe perete până când cota arată valoarea, celălalt capăt rămânând pe loc. Un singur pas de anulare. **Escape** o lasă cum era. Dacă golul n-ar mai încăpea (dincolo de capătul peretelui sau peste alt gol), nu se mută nimic, iar mesajul spune de ce.
- **De unde pornesc** e o setare a ta, ca în Revit: **Adnotări → Cote temporare…** → **Uși și ferestre**: **Goluri** (implicit) măsoară de la muchiile golului, **Axe** de la mijlocul lui, la mijlocul altui gol. Capetele lor pe un perete urmează alegerea **Pereți** din aceeași fereastră: fața peretelui, fața miezului (implicit, **Fețele miezului**), centrul miezului sau axa (vezi [Referința cotelor](walls.md#referinta-cotelor-de-la-fata-miezului-la-fata-miezului)).
- **Dă clic pe punctul de la capătul golului** ca să măsori de la mijlocul lui în loc de muchie (sau de la muchie, cu **Axe**).
- **Dă clic pe punctul de la celălalt capăt** ca să măsori până la următorul lucru de acolo: fața apropiată a unui perete, fața depărtată, fețele miezului (la un perete în straturi), axul, centrul miezului (când nu e pe axă); muchia apropiată a altui gol, mijlocul lui, muchia depărtată.
- **Dă clic pe lacăt**, lângă o valoare, ca s-o păstrezi ca o cotă permanentă a planului, legată de ce se află la capetele ei, ca una desenată cu **Adnotări → Cote**. Nu blochează distanța: golul se poate muta în continuare.

Așadar „colțul acestei ferestre la 1500 de fața acelui perete” înseamnă: selectează fereastra, dă clic pe valoarea spre perete, tastează `1500`, Enter. „Axul acestei ferestre la 2500 de axul celeilalte ferestre” înseamnă: dă clic pe punctul de la capătul ferestrei și pe cel de la capătul celeilalte ferestre până când amândouă măsoară mijloacele, apoi tastează `2500`.

La un **perete curb** cotele merg pe arc, concentrice cu el, și arată lungimea de-a lungul feței pe care sunt desenate (fața exterioară e mai lungă decât cea interioară); o valoare tastată mută golul pe arc. Lacătul lor face o cotă de lungime de arc a planului, care își păstrează valoarea așa cum a fost desenată: nu urmărește peretele.

Într-o **secțiune sau fațadă** care vede peretele golului din față (peretele merge de-a lungul vederii, în limita a 10°), golul selectat are cotele de-a lungul peretelui deasupra lui, până la aceiași pereți și goluri, pe fața pe care o vede vederea, și două pe lângă el, în sus: de la cota nivelului până la partea lui de jos (**înălțimea parapetului**) și până la partea de sus (**înălțimea buiandrugului**). Tastarea oricăreia mută golul în sus sau în jos; cealaltă urmează. Un perete tăiat oblic sau văzut din profil, un perete curb și o secțiune frântă sau curbă nu arată cote.

![O fereastră selectată, cotată până la fața interioară a colțului și până la fereastra următoare](shots/temp-dims.webp)

![După ce s-a tastat 1500: colțul ei la 1500 de fața peretelui](shots/temp-dims-typed.webp)

![Axul celeilalte ferestre la 2500 de axul primei](shots/temp-dims-axes.webp)

![O fereastră dintr-un perete curb, cotată pe arc, la 1000 de fața peretelui care îl traversează](shots/temp-dims-curved.webp)

![O fereastră selectată în fațada de sud: de-a lungul peretelui și înălțimile parapetului și buiandrugului](shots/temp-dims-elevation.webp)

## Goluri fără ușă sau fereastră

**Arhitectură → Gol** cu **Gol de o formă** (<kbd>WO</kbd>) taie un gol simplu prin perete acolo unde dai clic: o trecere, un chepeng, un gol pentru o ușă care vine mai târziu. Lățimea, înălțimea și parapetul le pui în panoul de proprietăți înainte de clic. Selectat, i se schimbă acolo dimensiunile, locul de-a lungul peretelui și parapetul; ca o ușă, trebuie să încapă în perete și să nu atingă celelalte goluri ale lui. În IFC e un gol care taie peretele, fără nimic care să-l umple.

Selectat, un gol are mânere ca o fereastră: trage de mijlocul lui ca să-l muți de-a lungul peretelui sau de o ambrasură ca să-l lărgești sau să-l îngustezi, cealaltă rămânând pe loc; lungimea de lângă mouse se poate scrie, ca la desen. Se alege cu un clic în spațiul lui din plan sau în gaura lui din vederea 3D, unde i se aprinde conturul. **Mutare** (<kbd>M</kbd>) îl mută de-a lungul peretelui cu o distanță scrisă, iar înălțimea și parapetul sunt în panoul de proprietăți.

**De-a lungul peretelui**, în panoul de proprietăți (al unui gol, al unei uși sau al unei ferestre), e o distanță cu două alegeri alături: **Măsurat de la** (începutul peretelui, fața celui mai apropiat perete care îl întâlnește sau fața miezului acelui perete) și **Măsurat până la** (începutul, centrul sau sfârșitul golului). O valoare tastată mută golul astfel încât distanța să o arate, referința rămânând pe loc, într-un singur pas de anulare. Alegerile schimbă doar felul în care e citită aceeași poziție, deci nu se păstrează nimic în model; Lapin ține minte ultima ta alegere.

![De-a lungul peretelui al unei ferestre în proprietăți: distanța, măsurată de la și până la](shots/along-field.webp)

**Forma de sus** dă forma capului: **Dreptunghi**, **Arc de segment** (un arc turtit), **Arc eliptic** (o jumătate de elipsă, înaltă cât un sfert din lățime dacă nu spui altfel), **Semicerc** sau **Colțuri rotunjite** (un cap drept cu colțurile de sus rotunjite). **Înălțimea arcului** spune cât se ridică un arc de segment sau eliptic, **Raza colțurilor** cât de rotunde sunt colțurile (150 mm dacă nu spui altfel, niciodată mai mult de jumătate din lățime); goală, cea a formei. Golul din perete urmează conturul în 3D, în fațade și secțiuni și în IFC; planul îi arată spaletele ca înainte.

**Finisajul șpaleților** îmbracă glafurile și partea de sus în interiorul golului: **Tencuială** (15 mm dacă nu spui altfel) sau o **Căptușeală** (20 mm de lemn), cu **Grosimea finisajului** și **Materialul finisajului**; un gol deasupra pardoselii e finisat și dedesubt. Finisajul face parte din corpul peretelui, în 3D, fațade, secțiuni și cantități, iar golul liber e ce rămâne. **Buiandrug** e **Regula peretelui** (buiandrugul cerut de tipul peretelui, ca deasupra unei uși) sau **Niciunul**. Mai multe goluri selectate se schimbă împreună, într-un singur pas de anulare.

**Editează golul…** deschide configuratorul lui: aceleași câmpuri peste un desen al golului în perete, aplicate împreună. **Schimbă în ușă…** și **Schimbă în fereastră…** îl umplu cu o ușă sau o fereastră de tipul ales, acolo unde e: locul, parapetul și GlobalId-ul lui rămân, iar **Potrivește tipul la gol** îi dă un tip de mărimea și forma lui (al proiectului de acea mărime, altfel unul făcut din tipul ales și numit după mărime). Invers, **Schimbă în gol** la o ușă sau fereastră selectată o scoate și lasă gaura ca gol de aceeași mărime și formă. Fiecare e un singur pas de anulare.

![Un gol în perete în configuratorul lui: cap semicircular, glafurile tencuite](shots/wall-opening-dialog.webp)

Un gol poate avea și **conturul lui**, de orice formă: desenează-l închis pe o secțiune sau o fațadă care privește peretele din față (**Draft**: o polilinie, un dreptunghi, un cerc, arce), selectează-l și alege **Draft → Convert to… → Wall opening**. Golul intră în peretele drept din spatele formei: lățimea, înălțimea, locul pe perete și parapetul vin din marginile conturului, iar conturul însuși e tăiat prin perete (arcele pe curba adevărată). Redimensionat, se întinde cu cutia lui; alt cap ales în proprietăți înlocuiește conturul. În IFC e un IfcOpeningElement de acea formă și revine așa cum era.

![Un gol de perete cu conturul lui, desenat pe o secțiune](shots/wall-opening-outline.webp)

### Goluri prin mai mulți pereți

O ușă, o fereastră sau un gol taie fiecare perete prin care trece gaura, nu doar peretele în care se află: un perete așezat pe el (o ușă care pornește într-un perete de beton armat de 1 m și urcă prin zidăria de deasupra), un perete de sub el sau o căptușeală lipită de fața lui. Fiecare perete care stă de-a lungul peretelui golului — paralel cu el, atingându-l sau suprapus peste el — e tăiat acolo unde ajunge gaura în el, pe toată grosimea, în 3D, planuri, secțiuni, fațade și cantități, iar buiandrugul intră în peretele în care e capul golului. Nu se păstrează nimic pentru asta: muți golul sau pereții, iar tăietura urmează; un perete la care gaura nu mai ajunge e din nou întreg. Așa, o ușă poate ieși din peretele ei în pereții pe care îi taie, dar nu în gol.

**Taie și pereții prin care trece**, în panoul de proprietăți al unei uși, ferestre sau al unui gol, e pornit dacă nu-l oprești; oprit, golul își taie doar peretele lui și trebuie să încapă din nou în el. În IFC, fiecare perete tăiat are propriul element de gol. Pereții importați dintr-un fișier IFC făcut de alt program păstrează golurile fișierului și nu sunt tăiați așa. Pereții care se întâlnesc într-un colț, pereții curbi, planșeele, grinzile și stâlpii nu sunt tăiați de el.

## Configuratorul

**Arhitectură → Uși și ferestre** deschide configuratorul. În stânga sunt toate tipurile de uși și ferestre din proiect. Poți schimba:

- numele tipului și marca lui (litera din etichete);
- lățimea și înălțimea;
- adâncimea și lățimea tocului;
- coloanele și rândurile de panouri, și cum se deschide fiecare panou;
- finisajul glafului, ancadramentul de pe fațadă, pragul și culorile.

Pragul unei uși stă sub planul de secționare, așa că planurile nu îl desenează. Bifează **Pragul desenat în plan** la un tip de ușă ca să-l desenezi, văzut de sus (de la LOD 200 în sus): pragul detaliat pe grosimea peretelui, când tipul are unul, altfel traversa de jos a tocului, între montanți. Secțiunile taie pragul așa cum îl are ușa, bifat sau nu.

**Pragul** unei uși (sub **Înălțimea pragului** și **Materialul pragului**) are și **Lățimea pragului**: cât se întinde pe grosimea peretelui, cum are glaful unei ferestre ieșirea lui. Gol, se întinde pe toată grosimea peretelui, ca până acum. Cu o lățime, stă centrat pe toc; **Decalajul pragului** îl mută spre exterior (+) sau spre interior (−) și nu trece niciodată de fețele peretelui. Cât adâncimea tocului, stă doar între montanți; un prag de 200 mm cu decalaj de 50 mm, într-un perete de 300 mm cu tocul la mijloc, ajunge la fața exterioară. Planul, secțiunile și 3D-ul îl urmează; lungimea lui din extrase rămâne lățimea golului. În plan pragul merge între montanții tocului, acolo unde aceștia stau pe el, și pe lățimea golului alături de toc.

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

### Uși și ferestre în același toc

Canatele de ușă și cercevelele de fereastră pot sta în același toc: o ușă de balcon cu fereastra ei, o ușă de intrare cu lumini laterale și supralumină, un portal cu ridicare și glisare. În **Se deschide**, lista e grupată în **Canate de ușă**, **Cercevele de fereastră** și, la o ușă, **Uși de garaj**, iar orice ochi al unei uși sau ferestre poate lua oricare dintre ele. Rămâne un singur obiect, cu un singur marcaj și un singur rând în tabel:

- Cu cel puțin un canat de ușă este **ușă** (IfcDoor). Un tip nou de fereastră căruia îi dați un canat devine ușă; rândul de sub desene spune ce este, **IfcDoor** sau **IfcWindow**.
- Cu cercevele de fereastră și fără canat este **fereastră**. Unul doar cu ochiuri fixe rămâne ce a fost făcut.
- Un tip salvat își păstrează clasa: un tip de fereastră nu poate primi un canat pe loc. **Creați ca tip nou** face ușa (sau fereastra) alături.

O cercevea pe rândul de jos al unei uși stă pe o **traversă inferioară**, cât lățimea tocului peste prag, ca cerceveaua unei ferestre; canatele păstrează pragul. Glafurile (**Glaf interior peste parapet**, **Solbanc peste parapet**) se pun doar peste un **panou plin** la baza unei coloane a ușii (parapetul de sub partea de fereastră), pe traversa de deasupra lui; unde sticla sau un canat ajunge la pardoseală nu sunt. O cercevea dintr-o ușă are mâner de fereastră; canatele păstrează mânerul ușii.

Presetările: **Ușă de balcon + fereastră nouă** (un canat vitrat și, alături, o cercevea oscilobatantă peste un panou plin de parapet, PVC, 4-16-4), **Ușă nouă cu lumini laterale și supralumină**, **Portal nou cu ridicare și glisare (HS)** și **Fereastră franțuzească nouă** (două cercevele cu balamale laterale până la pardoseală, fără glafuri; o fereastră).

Canatele **Ridicare și glisare (HS)** și **Oscilo-glisant (PSK)** alunecă pe șina interioară a tocului, peste ochiul fix de alături, aflat pe cea exterioară; mânerul lor lung e pe montantul de care trageți. Stau pe prag, deci se pun pe rândul de jos; pragul portalului HS e la nivelul pardoselii. Simbolul lor în elevație e săgeata sensului în care alunecă, cu ridicarea (HS) sau basculare (PSK). Nimic nu stă în calea canatului: montantul dintre canat și ochiul fix e tocul ochiului fix, numai pe șina exterioară, iar canatul trece peste el, așa că montanții lor de întâlnire se suprapun, cum îi desenează producătorii, în planuri, secțiuni, 3D și în previzualizarea configuratorului. Cercevelele unei ferestre glisante trec una pe lângă alta la fel: montantul dintre două cercevele de pe cele două șine rămâne în afara amândurora.

![O ușă de balcon și fereastra ei în același toc: un canat, o cercevea oscilobatantă peste un panou de parapet, geam 4-16-4](shots/joinery-balcony-door.webp)

![Un portal cu ridicare și glisare: canatul de pe șina interioară alunecă spre stânga peste ochiul fix](shots/joinery-lift-slide.webp)

**Sticla.** Sub **Sticlă**, **Structura geamului** este geamul așa cum îl scriu producătorii: foile de sticlă și spațiile în milimetri, dinspre exterior spre interior, `4-16-4` dublu, `4-12-4-12-4` triplu, `33.1-16-4` cu o foaie laminată la exterior (33.1 înseamnă două foi de 3 mm și o folie). Ea dă grosimea sticlei în 3D, în secțiuni și în planuri, afișată alături; fără ea, scrieți **Grosimea sticlei**. O structură mai groasă decât încap cerceveaua și tocul este refuzată. Valoarea U, valoarea g și sticla de siguranță sunt [Datele](#date-valoarea-u-rezistenta-la-foc-feronerie) tipului.

**Deschiderea cercevelelor în plan.** Ferestrele franțuzești și ușile de balcon se desenează adesea cu deschiderea cercevelelor, punctat. Lista de sub desene o stabilește pentru tot proiectul: **Deschiderea cercevelelor: punctat până la pardoseală** (implicit: cercevelele cu balamale laterale și oscilobatante a căror bază e la cel mult 150 mm de pardoseală), **deloc**, **toate punctat** sau **toate continuu**. Arcul unui canat de ușă se desenează mereu; cercevelele glisante își păstrează săgeata.

![O fereastră franțuzească: deschiderea cercevelelor punctată în plan](shots/joinery-french-window.webp)

În IFC elementul este un IfcDoor al cărui OperationType este cel mai apropiat pe care îl are IFC (o ușă cu un canat, o ușă cu un ochi fix lateral, o ușă glisantă pentru un portal HS), iar fiecare ochi este un IfcDoorPanelProperties: operația canatului, FIXEDPANEL pentru un ochi fix, USERDEFINED pentru o cercevea. Ce nu poate spune IFC merge în setul `Lapin_Joinery` al tipului: **PanelOperations** (fiecare ochi în termenii IFC pentru ferestre, SIDEHUNGRIGHTHAND, TILTANDTURNLEFTHAND…, sau LIFTANDSLIDE), **Opening** (lift-and-slide) și **Glazing** (structura geamului); sticla este și în `Pset_DoorWindowGlazingType` (foile, grosimile lor, laminată sau nu). Lapin își citește propriul fișier înapoi întreg, iar operațiile ochiurilor din `Lapin_Joinery` dintr-un fișier venit din altă parte.

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

Pentru un tip de **fereastră** salvat, **Desenează conturul…** (sub **Formă** în configurator) deschide fațada tipului pe o planșetă: văzută din exterior, cu partea de jos pe linia pragului, conturul de acum ca o polilinie închisă și linia interioară a tocului punctată. Desenează un singur contur închis (o polilinie, un cerc, o elipsă sau linii și arce care se întâlnesc cap la cap; liniile de construcție pot rămâne) și alege **Termină schița**. Tipul preia acel contur într-un singur pas de anulare: lățimea și înălțimea lui sunt limitele conturului, un toc îl urmează de jur împrejur și înăuntru stă un ochi fix. Toate ferestrele tipului îl urmează, iar lista **Formă** spune apoi **Contur desenat**. Dacă desenezi un dreptunghi, primești dreptunghiul înapoi. Ușile păstrează dreptunghiul și formele capului. Grila formularului rămâne calea implicită.

![Planșeta unui contur de fereastră desenat în configurator](shots/joinery-outline-board.webp)

![Ferestre cu acel contur în 3D](shots/joinery-outline-3d.webp)

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

**Etichetarea glafurilor și solbancurilor și extrasul lor.** Glaful (interior) și solbancul (exterior) unei ferestre (cele detaliate, altfel plăcile simple ale tipului) rămân parte a ferestrei și pot fi etichetate și listate separat, pentru furnizorul lor:

- **Architecture → Extras de glafuri și solbancuri** face un extras de câmpuri din șablonul livrat **Glafuri și solbancuri**: un rând pentru fiecare fel de glaf sau solbanc (latura, materialul, lungimea, adâncimea de-a curmezișul peretelui, grosimea, panta), câte sunt, marcile ferestrelor, lungimea totală și aria. **Configurează…** îl schimbă ca pe orice extras de câmpuri; rândurile lui sunt piesele **Glafuri și solbancuri** (**Rânduri** în editor), fiecare cu câmpurile ei: **sillSide**, **sillMaterial**, **sillLength**, **sillDepth**, **sillProjection** (ieșirea peste fața peretelui), **sillThickness**, **sillOverhang** (depășirea peste fiecare șpalet), **sillSlope**, **sillArea**, **sillCount**.
- **Annotate → Etichetă** pe un glaf sau solbanc: dă clic pe el într-un plan sau într-o secțiune, ori alege **Glaful unei ferestre** sau **Solbancul unei ferestre** în bara uneltei Etichetă și dă clic pe fereastră. Eticheta îl spune în câteva cuvinte (*Exterior sill 1260 × 120 · Granit*), linia ei de indicație arată spre el, iar **Arată** sau **Text** din proprietățile ei citesc câmpurile lui (`{sillMaterial}`, `{sillLength}`) și pe ale ferestrei (`{mark}`). Proprietățile ei numesc **Partea etichetată**.
- Câmpurile ferestrei citesc și ele glaful și solbancul, pentru etichetele și extrasele ei: **sillInteriorMaterial**, **sillInteriorLength**, **sillInteriorDepth**, **sillInteriorThickness** și la fel **sillExterior…** (lungimile în milimetri).

În IFC glafurile și solbancurile rămân în fereastră, ca până acum: în corpul ei, lungimile lor în cantitățile ei (SillInsideLength, SillOutsideLength) și materialul plăcii. IFC4 nu are un element făcut pentru glaful unei ferestre (felurile IfcCovering nu-l au), iar scrise ca elemente separate ar ajunge de două ori la un program care citește fișierul înapoi.

![Extrasul de glafuri și solbancuri: fiecare fel, câte sunt și ferestrele lor](shots/window-sill-schedule.webp)

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

### Dimensiunile pe care le poate scrie o etichetă

Textele unui marcaj (**Text (exterior)**, **Interior**) citesc aceste dimensiuni ale ușii sau ferestrei, în unitatea tipului de etichetă:

- `{width}`, `{height}`: dimensiunea ușii sau a ferestrei, ca înainte.
- `{openingWidth}`, `{openingHeight}`: golul din perete pe care îl umple ușa sau fereastra (golul pe care un fișier importat îl taie în jurul ei, acolo unde diferă).
- `{clearWidth}`, `{clearHeight}`: lumina tocului, cum o dau desenele poloneze și cehe (ușă 800/1970). Lățimea este lățimea minus fața tocului pe ambele laturi; înălțimea este înălțimea minus tocul de sus și, jos, pragul unei uși sau tocul de jos al unei ferestre. La un cap în arc, înălțimea merge până la vârful interior, la mijloc; o fereastră rotundă ia interiorul inelului. O ușă de 900 × 2100 cu toc de 60 mm și prag de 20 mm are lumina 780 × 2020. Unde regula locală lasă pragul inclus (cea cehă: golul minus doar tocul, 2050 cu toc de 50 mm înseamnă 2000), debifează **Gestionare → Standarde de desen → Proiect → Înălțimea liberă a ușii minus pragul**: aceeași ușă are atunci 780 × 2040, la fel în etichete, reguli, extrase și formule. Tocul de jos al unei ferestre se scade oricum.
- `{sill}`: parapetul față de reperul de la care se măsoară; `{sillFinished}`: față de pardoseala finită a peretelui, oricare ar fi reperul.
- `{frameWidth}`, `{frameDepth}`: lățimea feței tocului (cât iau din gol fiecare montant și tocul de sus) și adâncimea lui pe grosimea peretelui. Sticla fără toc nu are față (0) și e cât sticla de adâncă. O formulă scade tocul dintr-o dimensiune: `{= openingHeight - frameWidth}` scrie 2040 pentru o ușă de 2100 cu toc de 60 mm.

Un simbol desenat ca marcaj le citește și el. Regulile de etichetare le citesc în metri (`clearWidth < 0.8`). În rest (etichete cu chenar, etichete de text, extrase, formule) `clearWidth`, `clearHeight`, `openingWidth`, `openingHeight`, `sillFinished`, `frameWidth` și `frameDepth` sunt câmpuri calculate, în milimetri, oferite în listele de câmpuri.

### Marcaje gata făcute și marcajul cu bulă

În **Gestionare → Standarde de desen → Etichete**, **+ Marcaj încorporat…** adaugă un marcaj după o convenție națională, ca tip de etichetă al tău, de modificat:

- **Marcaj de ușă (polonez: bulă, lumina tocului)**: o linie din mijlocul tocului, 60 cm spre exterior, până la o bulă de 30 cm cu marca. Lățimea luminii stă deasupra liniei și înălțimea dedesubt, lățimea mereu sus, oricum s-ar roti ușa. Clasa de rezistență la foc (`{FireRating}`) stă la mijlocul tocului, doar la ușile care au una.
- **Marcaj de fereastră (polonez: bulă, gol în perete, parapet)**: aceeași linie, care intră și în cameră, până la 10 cm dincolo de fața interioară a peretelui. Lățimea golului stă deasupra liniei și înălțimea dedesubt, iar parapetul față de pardoseala finită, la capătul interior al liniei.
- **Marcaj de ușă (ceh: lumina tocului 800/1970)**: marcajul obișnuit, cu marca în afară și lumina în milimetri înăuntru.

Alege tipul în **Adnotări → Etichete uși și ferestre** sau printr-o regulă de etichetare. Adăugat din nou, se face o copie numerotată. Marcajele cu care pornește un proiect rămân cum erau.

![Marcaje poloneze de uși și ferestre: linii din tocuri până la bule cu mărcile, lumina tocului și golul din perete deasupra și dedesubt, parapetele înăuntru](shots/tag-bubble-markers.webp)

**Marcaj…**, lângă tipul de etichetă al unei uși sau ferestre, alege așezarea: **linie peste perete** (cea obișnuită) sau **linie la o bulă**. Pentru bulă alege textul **Deasupra liniei**, **Sub linie** și **La origine**, apoi lungimile, în centimetrii clădirii (desenul le scalează): **Lungimea liniei**, **Decalajul originii liniei** (spre exterior +, spre interior −), **Linie în interior peste perete** (gol: linia pornește din origine), **Diametrul bulei** (0: doar marca), **Decalajul textului de la origine** și **Decalajul textului interior**. **Text (exterior)** intră în bulă, iar **Interior** stă la capătul interior al liniei. Fiecare text își păstrează mânerele în plan.

![Marcaj… pentru marcajul polonez de ușă: așezarea cu bulă, textele și lungimile ei](shots/tag-marker-editor.webp)

### Marcajul modificat pe ecran

Fiecare marcaj se poate aranja de mână, parte cu parte. Dă clic pe linia lui sau pe unul dintre texte ca să-l selectezi: fiecare text are atunci un mâner pătrat (trage-l ca să muți textul) și unul rotund după capătul lui (trage-l ca să rotești textul), iar fiecare capăt al liniei are un mâner (trage-l ca să lungești sau să scurtezi linia; textul de la acel capăt merge cu el). Ctrl/⌘-clic pe un mâner pune acea parte cum era. Fiecare tragere e un pas de anulare. Cu mai multe marcaje din același plan selectate, o parte trasă la unul schimbă aceeași parte la toate.

![Marcajul unei uși selectat: câte un mâner pentru fiecare text, mânerul de rotire și câte unul la fiecare capăt al liniei](shots/marker-grips.webp)

![Marca mutată și rotită, capătul exterior al liniei lungit](shots/marker-edited.webp)

Modificările țin de marcaj, deci de acel plan: aceeași ușă poate fi aranjată altfel în alt plan. Sunt păstrate de-a lungul și de-a curmezișul peretelui, așa că urmează ușa când se mută, se rotește sau se întoarce pe partea cealaltă. În proprietățile marcajului, **Marcaj** îi arată părțile: scrie rotirea unui text în grade sau cât trece un capăt, în milimetri pe hârtie, pune o parte la loc cu ↺ sau pe toate cu **Pune eticheta la loc**. Motorul desenează marcajele așa cum sunt modificate, deci planșele, PDF-ul și DXF-ul le tipăresc la fel.

![Părțile unui marcaj în proprietățile lui](shots/marker-properties.webp)

Dacă textele stau de-a curmezișul liniei marcajului (de-a lungul peretelui, implicit) sau de-a lungul ei se alege pentru fiecare tip de etichetă, în **Gestionare → Standarde de desen → Etichete**, coloana **Direcția textului**: **perpendicular pe linia etichetei** sau **de-a lungul liniei etichetei**. Oricum ar fi, sunt desenate citibil, niciodată cu susul în jos; și un text rotit de mână rămâne citibil.

![Textele marcajului ușii de-a lungul liniei etichetei](shots/marker-along.webp)
