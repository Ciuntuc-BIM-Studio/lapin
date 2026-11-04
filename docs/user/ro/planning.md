---
title: Planificare
order: 97
summary: Programul lucrărilor într-o diagramă Gantt: sarcini într-un WBS, jaloane, legături cu decalaje, drumul critic calculat pe un calendar cu sărbătorile legale din România; simularea 4D a clădirii care crește zi de zi, pe vederi, pe planșe și ca video; progresul marcat pe șantier, și de pe telefon, față de o linie de bază; schimb cu MS Project, IFC și Excel.
---

## Tabul Planificare

![Tabul Planificare](shots/ribbon-planning.webp)

- **Generează programul…**: un program întreg făcut din model, dintr-un singur pas (mai jos).
- **Gantt**: deschide programul sub vedere. Ecranul se împarte în două rânduri: modelul sus, programul jos.
- **Calendar…**: zilele și orele lucrătoare, sărbătorile și pauza de iarnă (mai jos).
- **Sarcină** și **Jalon**: o sarcină sau un jalon nou înaintea sarcinii selectate în Gantt (sau la sfârșit).
- **Retrage în dreapta**, **Scoate la stânga**, **Leagă sarcinile** și **Dezleagă sarcinile**: WBS-ul și legăturile sarcinilor selectate.
- **Adaugă la sarcină…** (TA) și **Elemente fără sarcină**: elementele modelului în sarcini (mai jos).
- **Simulează**, **Planșa de etape…**, **Gantt pe planșă** și **Exportă video…**: simularea 4D, modelul așa cum îl are programul într-o zi (mai jos).
- **Marchează realizat** (MD), **Marchează început** (MS), **Marchează neînceput**, **Salvează linia de bază**, **Șterge linia de bază** și **Importă progresul…**: progresul și linia de bază (mai jos).
- **Importă MS Project…**, **Exportă MS Project…**, **Export XLSX** și **Exportă IFC…**: programul spre și dinspre alte programe (mai jos).

Un proiect are un singur program. Se face prima oară când adaugi o sarcină și începe lunea următoare. Data de start se schimbă din bara Gantt-ului.

## Programul generat din model

![Generează programul: șabloanele, fila Început și previzualizarea](shots/planning-generate.webp)

**Planificare → Generează programul…** (și din bara Gantt-ului) face un program complet din model: o sarcină pentru fiecare pas al lucrărilor și, unde pasul merge etaj cu etaj, câte una pe etaj, fiecare sarcină având deja elementele ei. În stânga sunt **șabloanele**: **Cadre din beton armat**, **Zidărie portantă**, **Hală metalică** și **Structură pe cadre din lemn**. Lapin îl marchează *sugerat* pe cel cerut de model: hala metalică dacă modelul are una, cadrele din lemn dacă pereții sunt pe cadre, zidăria portantă dacă nu are stâlpi, altfel cadrele din beton armat. Deasupra lor sunt stilurile proiectului (**Salvează ca stil…** păstrează ce ai configurat, pentru proiectul acesta și pentru cele făcute din el).

În dreapta e **previzualizarea**: sarcinile pe care le-ar face generatorul, ca bare, fiecare cu cantitatea care i-a dat durata, iar sub ele numărătorile:

- **Elemente legate**: elementele luate de sarcini din toate cele care au nevoie de o sarcină. E cel mai important număr de aici. Când rămân elemente pe dinafară, **Arată elementele fără sarcină** le selectează în vedere, iar **Un pas pentru ele** adaugă un pas care le ia felurile.
- **Durate din norme** și **Durate implicite (fără normă)**: câte sarcini au primit durata dintr-o normă și câte au luat zilele pasului.
- **Cofraje și schele pe sarcinile gazdelor** (mai jos).

**Aplică** scrie programul ca un singur pas de anulat. Ecranul se împarte apoi: modelul sus, Gantt-ul jos.

### Fila Început

- **Început**: prima zi a programului.
- **Împarte pe etaje**: o sarcină pe etaj pentru pașii care merg etaj cu etaj (structura, zidăria, finisajele); debifat, o sarcină pe pas.
- **Întâi demolările**: elementele pe care fazele le demolează intră într-o sarcină **Demolare**, înaintea tuturor (felul ei în IFC: DEMOLITION). Elementele existente care rămân nu sunt ale niciunei sarcini.
- **Cofraje** și **Schele**: dacă aceste elemente temporare trăiesc după sarcinile gazdelor lor (mai jos).
- **Păstrează sarcinile făcute de mână**: când programul e generat din nou (mai jos).

### Fila Secvență

![Fila Secvență: pașii în ordine](shots/planning-generate-sequence.webp)

Pașii șablonului, în ordine. Fiecare poate fi oprit, redenumit și mutat mai sus sau mai jos cu săgețile. Spune dacă se face **o dată** pentru clădire sau **pe etaj**, și cum urmează pasul dinaintea lui pe același etaj: sfârșit–început (FS) sau început–început (SS) cu decalaj, pentru finisajele care urcă după structură. Creionul deschide ce construiește pasul: felurile de elemente, alese dintre cele din model, și **Partea** din ele (întregi, miezul sau finisajele, ca partea unei sarcini).

Legăturile vin din ordine: fiecare sarcină urmează pasul dinainte pe același etaj și același pas pe etajul de dedesubt. La zidăria portantă și la cadrele din lemn, pereții unui etaj așteaptă și placa etajului de dedesubt.

Fiecare sarcină e făcută cu o **regulă** (felurile, etajul), deci își păstrează elementele: un perete desenat după generare intră singur în sarcina lui, fără să mai generezi o dată.

### Fila Durate

![Fila Durate: normele și, pe fiecare pas, norma, echipa și zilele](shots/planning-generate-durations.webp)

Durata unei sarcini vine din cantitatea elementelor ei (din antemăsurătoare: volumul miezului, aria finisajelor, ușile și ferestrele la bucată, oțelul după greutate) și dintr-o **normă de productivitate**:

*durata = cantitatea × orele-om pe unitate ÷ (echipa × orele pe zi)*, în zile lucrătoare întregi. De exemplu 4,5 m³ de zidărie la 4 ore-om pe metru cub, cu o echipă de 2 care lucrează 8 ore, înseamnă 18 ÷ 16 = 1,125, deci 2 zile.

Normele sunt ale tale. Lapin nu livrează niciun indicator de norme. **Importă norme…** citește tabelul tău dintr-un fișier CSV sau XLSX: o coloană pentru cod, una pentru denumire, una pentru unitate (m3, mc, m2, mp, m, ml, buc, kg, t…), una pentru orele-om pe unitate (sau o productivitate, în unități pe oră) și, opțional, una pentru echipă. Antetele pot fi în română sau în engleză. **Șablon (CSV)** salvează coloanele cu câteva rânduri de valori aproximative scrise de Lapin, fiecare marcat *orientativ*. **Folosește valorile orientative ale lui Lapin** pune rândurile acestea în proiect, ca să încerci generatorul; fereastra avertizează atunci că duratele vin din valori orientative, nu dintr-o normă.

Sub norme, fiecare pas își alege **Norma**, **Echipa** (0: a normei) și **Zilele**, folosite când nicio normă nu-i dă durata. Fila Calendar arată calendarul în care se numără duratele; orele lui pe zi împart orele-om.

### Generat din nou

Deschide din nou generatorul și apasă **Aplică**: programul e refăcut peste aceleași sarcini, deci id-urile lor rămân. Ce ai schimbat de mână rămâne:

- o sarcină generată pe care ai editat-o (numele, durata, datele, regula, partea) e **Păstrată cum a fost editată**, bifat în Proprietățile ei; debifează ca s-o refacă din pasul ei;
- elementele adăugate sau scoase de mână dintr-o sarcină rămân excepțiile ei;
- sarcinile și legăturile făcute de tine rămân; debifează **Păstrează sarcinile făcute de mână** ca să rămână doar cele generate.

![Programul generat: modelul sus, Gantt-ul jos](shots/planning-generated.webp)

În Proprietăți, o sarcină generată spune de unde i-a venit durata: cantitatea, norma și echipa, sau zilele pasului când nicio normă nu i-a dat-o.

![O sarcină generată în Proprietăți](shots/planning-generated-task.webp)

### Cofraje și schele

Un cofraj sau o schelă din **Organizare de șantier** nu are sarcina lui: trăiește după sarcinile a ceea ce servește. Cofrajul se montează când începe placa, grinda sau peretele lui și se scoate după ce au trecut zilele spuse de tipul lui de la terminarea lor (**Decofrat după**, în zile lucrătoare: 21 la plăci și grinzi, 7 la pereți, stâlpi și fundații, dacă tipul nu spune altfel). Schela stă de la prima sarcină pe pereții ei până la ultima, cu finisajele lor. Selectează unul și Proprietățile lui spun când e **Pe șantier** și după ce sarcini **Trăiește după**.

## Gantt-ul

![Gantt-ul sub vederea 3D, cu o sarcină selectată](shots/planning-gantt.webp)

În stânga e tabelul sarcinilor: numărul rândului, codul **WBS**, numele, **Durata** în zile lucrătoare, **Începutul** și **Sfârșitul**, **Predecesorii** și **Rezerva** totală. În dreapta, fiecare sarcină e o bară pe scara timpului:

- o **sarcină** e o bară albastră, roșie când e pe drumul critic;
- un **rezumat** (o sarcină cu alte sarcini sub ea) e o acoladă închisă la culoare, de la începutul primei sarcini până la sfârșitul ultimei;
- un **jalon** e un romb.

Zilele nelucrătoare sunt hașurate, iar ziua de azi e o linie punctată. **Zile**, **Săptămâni**, **Luni** și **Trimestre** schimbă scara, la fel <kbd>Ctrl</kbd> + rotița mouse-ului. Butonul cu patru colțuri arată tot programul, iar **Azi** derulează la ziua de azi. **Drumul critic** pornește și oprește roșul. Săgeata de lângă numele unui rezumat strânge și desface sarcinile de sub el.

Gantt-ul e o vedere ca celelalte: are un tab, iar pe desktop se poate deschide într-o fereastră proprie, pentru un al doilea ecran.

### Editarea în tabel

Dublu-clic pe o celulă ca să scrii în ea. <kbd>Enter</kbd> păstrează valoarea, <kbd>Escape</kbd> renunță.

- **Durata**: zile lucrătoare; 0 face din sarcină un jalon.
- **Începutul**: sarcina începe nu mai devreme de ziua aceea (o constrângere, vezi mai jos).
- **Sfârșitul**: durata devine numărul de zile lucrătoare de la început până în ziua aceea.
- **Predecesorii**: ca în MS Project, numere de rând despărțite prin virgulă, fiecare cu tipul și decalajul: `3`, `5SS`, `7FF+2`, `2FS-1d`.

Tastatura merge ca într-un tabel: <kbd>↑</kbd> și <kbd>↓</kbd> mută selecția, <kbd>Insert</kbd> adaugă o sarcină înaintea celei selectate (<kbd>Shift</kbd>+<kbd>Insert</kbd> un jalon), <kbd>Delete</kbd> șterge sarcinile selectate cu tot ce e sub ele, <kbd>Tab</kbd> retrage, <kbd>Shift</kbd>+<kbd>Tab</kbd> scoate la stânga, <kbd>F2</kbd> redenumește.

### Editarea pe diagramă

- **Trage o bară** ca să muți sarcina. Ea începe atunci nu mai devreme de ziua în care o lași. Data se vede cât tragi.
- **Trage de capătul unei bare** ca să-i schimbi durata. Zilele lucrătoare se văd cât tragi.
- **Leagă două sarcini**: du mouse-ul peste o bară, apoi trage din cercul mic de la unul din capete pe altă bară. Capetele pe care le unești dau tipul: din sfârșit pe începutul celeilalte e sfârșit–început (FS), început pe început SS, sfârșit pe sfârșit FF, început pe sfârșit SF.
- **Clic pe o săgeată** ca să schimbi tipul și decalajul legăturii (în zile lucrătoare; negativ înseamnă o suprapunere) sau ca s-o scoți.

Fiecare schimbare e un singur pas de anulare.

## O sarcină în Proprietăți

![O sarcină în Proprietăți](shots/planning-task.webp)

Selectezi o sarcină și Proprietățile o arată: numele, felul ei în IFC, durata, dacă e jalon, datele și:

- **Constrângere**: **Cât mai devreme** (implicit), **Începe nu mai devreme de** o dată sau **Trebuie să înceapă pe** o dată. O sarcină care trebuie să înceapă înainte să se termine predecesorii ei le ia acestora din rezervă. Rezerva lor devine negativă și ei devin critici.
- **Lucrări umede**: betoane, șape, tencuieli. O sarcină umedă nu lucrează în pauza de iarnă a calendarului, când pauza e pornită.
- **Rezervă totală** și **Rezervă liberă**: câte zile lucrătoare poate întârzia sarcina fără să mute finalul, respectiv fără să mute ce urmează după ea. **Început târziu** și **Sfârșit târziu** sunt cele mai târzii date care păstrează finalul.
- **Predecesori**: fiecare legătură care intră, cu tipul și decalajul de schimbat, sau de scos. **Succesori** arată ce urmează după sarcină.

## Drumul critic

Lapin calculează programul după fiecare schimbare. O trecere înainte găsește datele cele mai devreme, una înapoi pe cele mai târzii. Sarcinile fără rezervă fac drumul critic: întârzierea oricăreia mută finalul. O legătură spre un rezumat ține toate sarcinile de sub el. O legătură dintr-un rezumat pornește de la începutul primei lui sarcini și se termină la sfârșitul ultimei. Lapin refuză o legătură care ar face o buclă.

## Elementele modelului în sarcini

![Sarcina Zidărie selectată: pereții ei desenați selectați în 3D](shots/planning-links.webp)

O sarcină ia elemente ale modelului, deci programul știe ce se construiește și când. Selectează o sarcină și elementele ei apar selectate în 3D și în planuri; dublu-clic pe bara ei le selectează și le încadrează. Selectează elemente într-o vedere și rândurile sarcinilor lor sunt marcate în Gantt. Coloana **Elemente** numără elementele fiecărei sarcini (ale unui rezumat, cu ale sarcinilor de sub el).

### O regulă

![Elementele unei sarcini în Proprietăți](shots/planning-task-elements.webp)

La **Elemente** în Proprietăți, bifează **Găsește-le după o regulă** și alege ce ia sarcina, ca la filtrul unui extras: **Feluri**, **Etaje**, **Tipuri**, **Materiale**, **Grupuri** și **Faze**, fiecare valoare cu câte elemente o au. Fiecare condiție pusă trebuie îndeplinită; una lăsată goală lasă totul să treacă. Se păstrează regula, nu elementele: un perete desenat mâine la parter intră singur în sarcina „Zidărie parter”. **Regulă din selecție** face o regulă din felurile și etajele elementelor selectate.

### O parte a elementelor

**Parte** spune ce ia sarcina din fiecare element: **elementele întregi**, **miezul** lor (stratul structural), **finisajele** (toate straturile în afară de miez), straturile unui material sau ale unei categorii de material (Finisaj, Izolație…). Așa, un perete stratificat e zidit într-o sarcină Zidărie (miezul) și tencuit într-o sarcină Tencuieli (finisajele). Un element fără straturi e propriul lui miez și nu are finisaje.

### De mână

![Cartea selecției peste 3D, cele două sarcini ale unui perete marcate în Gantt](shots/planning-element-tasks.webp)

Cât Gantt-ul e deschis, o carte peste vederea modelului spune câte elemente sunt selectate și în ce sarcini sunt. Trage-o de mâner pe rândul unei sarcini: elementele intră în acea sarcină. Ține <kbd>Shift</kbd> când le lași ca să le scoți din ea. **Adaugă la sarcină…** (<kbd>T</kbd> <kbd>A</kbd>, și în fila Planificare) alege sarcina dintr-o listă.

![Adaugă la sarcină](shots/planning-assign.webp)

Ce adaugi sau scoți de mână sunt **excepțiile** sarcinii: regula rămâne cum e. Proprietățile le numără, iar × de lângă fiecare le uită. Proprietățile unui element îi listează sarcinile la **Programul lucrărilor**, fiecare cu un × ca să-l scoți din ea. Fiecare schimbare e un pas de undo.

### Elemente fără sarcină

Odată ce o sarcină ia elemente, bara Gantt-ului spune câte elemente n-au sarcină sau au straturi pe care nu le ia nicio sarcină; un clic le selectează, gata de tras pe o sarcină. **Planificare → Elemente fără sarcină** face la fel. **Gestionare → Verificare** le listează și ea, la *Elements with a task*: câte un avertisment pentru fiecare, un clic îl selectează. Elementele existente care rămân nu au nevoie de sarcină.

### În IFC și MS Project

Exportul IFC scrie ce construiește fiecare sarcină ca IfcRelAssignsToProduct (elementul, cu sarcinile care îl construiesc), iar ce face o sarcină de demolare, îndepărtare sau dezmembrare ca IfcRelAssignsToProcess. Regula, partea și excepțiile merg în `Lapin_Task`, deci Lapin citește regula înapoi; un fișier din alt program dă sarcini cu elementele pe care le atribuie lor. MS Project XML le duce în câmpul Text1, numit *Lapin elements*. Registrul XLSX are coloanele **Elements** și **Rule**.

## Simularea 4D

![Casa într-o zi a programului ei: placa etajului în lucru în cofrajul ei, pereții de deasupra neîncepuți](shots/planning-simulation.webp)

**Planificare → Simulează** (și **Simulează** în bara Gantt-ului) arată modelul așa cum îl are programul într-o zi. Gantt-ul se deschide sub vedere dacă nu e deschis, cu playerul deasupra lui și ziua ca o bandă peste diagramă:

- ce **nu a început** e ascuns (sau arătat ca o fantomă palidă, din setări);
- ce e **în lucru** e desenat în culoarea lui (chihlimbar);
- ce e **construit** e desenat așa cum e;
- ce e **în demolare** e roșu cât durează sarcina lui, apoi dispare;
- **cofrajele și schelele** stau, în culoarea lor (albastru), doar între zilele pe care li le dau sarcinile gazdelor: cofrajul unei plăci de la începutul turnării până la decofrare, o schelă de la prima sarcină pe pereții ei până la ultima.

Un perete stratificat zidit într-o sarcină și tencuit în alta crește în doi pași: întâi miezul, apoi tencuiala. Elementele pe care nu le ia nicio sarcină rămân cum sunt. O cartelă peste vederea modelului spune ziua, ce înseamnă fiecare culoare și câte elemente sunt în fiecare stare.

### Redarea

![Playerul deasupra Gantt-ului](shots/planning-simulation-bar.webp)

- **Pornește** (▶) trece zilele cu viteza aleasă (de la 1 la 60 de zile pe secundă) până la final; **Pauză** o oprește.
- Săgețile din fiecare parte merg înapoi sau înainte cu **o zi** sau **o săptămână**.
- Trage cursorul, scrie o dată sau apasă și trage pe scara timpului a Gantt-ului: banda de peste diagramă și modelul urmează.
- Lângă dată: sarcinile în lucru în ziua aceea.

De la o zi la alta se schimbă doar culorile și vizibilitatea elementelor: nimic nu se reconstruiește, deci un model mare rulează la fel de lin ca unul mic.

### Culorile ei

![Setările simulării](shots/planning-simulation-settings.webp)

Rotița deschide setările simulării, păstrate în proiect: culorile a ce e în lucru, în demolare, ale cofrajelor și schelelor și ale fantomelor; dacă ce **nu a început** e ascuns sau arătat **ca fantome**; dacă **elementele fără sarcină** sunt arătate sau ascunse; și dacă **straturile construite separat** apar separat. **Culorile implicite** le aduce înapoi pe ale lui Lapin.

### Starea la data pe vederi și planșe

![Starea la data a unui plan în proprietățile lui](shots/planning-view-date.webp)

Un plan, o secțiune, o fațadă sau o vedere 3D salvată are în proprietăți **Starea la data**: vederea desenează atunci modelul așa cum îl are programul în ziua aceea, în aceleași culori, iar planșele ei îl tipăresc la fel (PDF-ul arată ce arată ecranul). Cu simularea pornită, **data cursorului** îi ia ziua. O vedere la o dată desenează stările programului, nu ale fazei ei. × de lângă dată readuce modelul așa cum e. O vedere 3D cu o dată arată ziua aceea chiar cu simularea oprită.

### Planșa de etape

![Planșa de etape: datele de la sfârșitul capitolelor](shots/planning-stages-dialog.webp)

**Planificare → Planșa de etape…** (și din player) pune modelul la mai multe date alăturat, pe o planșă nouă. E desenat din **vederea 3D, așa cum arată acum** (o imagine la fiecare dată) sau dintr-un plan, o secțiune ori o fațadă, pe care engine-ul o desenează la fiecare dată. Datele sunt **sfârșitul fiecărui capitol** (fiecare sarcină din vârful WBS-ului), **sfârșitul fiecărei luni** sau date **alese**. Fiecare cadru e etichetat cu data lui și sarcinile în lucru.

![O planșă de etape](shots/planning-stages-sheet.webp)

### Gantt-ul pe planșă

![Gantt-ul pe o planșă](shots/planning-gantt-sheet.webp)

**Planificare → Gantt pe planșă** pune programul pe o planșă nouă: tabelul (rândul, WBS-ul și sarcina, zilele, începutul, finalul) și barele pe o scară a timpului, sarcinile critice cu roșu, rezumatele ca acolade, jaloanele ca romburi, legăturile. Un program mai înalt decât planșa merge pe câte planșe îi trebuie, cu antetul pe fiecare. E desenat vectorial, ca vederile, și se tipărește în PDF.

### Export video

![Exportă video](shots/planning-video.webp)

**Planificare → Exportă video…** (și din player) înregistrează vederea 3D, de unde privește acum, de la o zi la alta, câte un cadru pe **o zi** sau pe **o săptămână**, cu data și sarcinile în lucru scrise dedesubt. Fișierul e un video WebM, sau MP4 unde sistemul oferă doar atât (aplicația de macOS). Un browser care nu înregistrează video salvează în schimb cadrele ca fișiere PNG într-un ZIP.

## Progresul

![Gantt-ul cu progresul: coloana %, partea realizată mai închisă în fiecare bară, linia de bază sub ea și abaterea](shots/planning-progress.webp)

Pe măsură ce lucrările avansează, programul spune cât s-a făcut și mută ce a rămas:

- **Data de stare**, în bara Gantt-ului, e ziua până la care e raportat progresul. Lucrul rămas începe în ziua lucrătoare de după: o sarcină neîncepută nu poate începe mai devreme, iar una în lucru face ce i-a rămas de atunci încolo. Diagrama o desenează ca o linie albastră.
- **% realizat** al unei sarcini se scrie în coloana lui sau în Proprietăți. Fără el, vine din elementele ei marcate pe șantier: cantitatea realizată din cantitatea sarcinii (în unitatea ei, când generatorul i-a dat una), altfel numărul lor. Al unei sarcini rezumat vine din sarcinile ei, ponderat cu duratele lor.
- **Începutul real** și **finalul real** se scriu în Proprietăți. Lăsate goale, vin din marcaje: prima zi în care a fost marcat un element și ziua ultimului element, după ce toate sunt realizate. O sarcină terminată stă pe datele ei reale, în afara drumului critic.
- O sarcină în lucru își păstrează începutul real și face partea din durată încă nerealizată (rotunjită în sus) după data de stare; sarcinile de după ea se mută odată cu ea.

Barele arată partea realizată mai închisă. Proprietățile spun de unde vine procentul și câte zile lucrătoare au rămas.

![Progresul unei sarcini în Proprietăți](shots/planning-progress-task.webp)

### Marcarea elementelor pe șantier

**Planificare → Marchează realizat** (MD) și **Marchează început** (MS) marchează elementele selectate în 3D sau într-un plan, la data de stare (azi, când nu e una), în fiecare sarcină care le ia; **Marchează neînceput** scoate marcajele. Proprietățile unui element selectat îi arată starea pe șantier, cu aceleași trei butoane, iar în fiecare sarcină a lui ziua în care a fost realizat sau început, cu un buton care îl marchează realizat doar în sarcina aceea. Un perete stratificat dintr-o sarcină de zidărie și una de tencuieli se marchează în amândouă, sau într-una, din rândul ei.

![Marcajele unui perete în proprietățile lui](shots/planning-element-marks.webp)

Pe șantier, de pe telefon: intră în sesiunea partajată a proiectului din browser (**Fișier → Partajează sesiunea…** pe calculatorul care îl are deschis). Un invitat care a intrat doar ca să se uite poate totuși marca elemente și scrie progresul unei sarcini: marcarea e singura modificare deschisă oricărui invitat. Fotografiile și rapoartele pe sarcini sunt pentru ediția premium.

### Linia de bază

**Planificare → Salvează linia de bază** păstrează datele și durata fiecărei sarcini așa cum sunt acum; salvată din nou, e înlocuită (o singură linie de bază: mai multe linii de bază și scenarii sunt pentru ediția premium). Gantt-ul o desenează apoi ca o bară gri subțire sub bara fiecărei sarcini (**Linia de bază** din bară o ascunde), adaugă coloana **Abatere** (zilele lucrătoare cu care s-a mutat finalul: pozitiv înseamnă întârziere, rândul marcat cu roșu) și spune în bara lui câte sarcini întârzie și cu cât s-a mutat finalul. Proprietățile dau datele liniei de bază și abaterile la început și la final. **Șterge linia de bază** o scoate.

### Întârziat și înainte în simulare

![Simularea la data de stare: ferestrele întârziate față de linia de bază](shots/planning-progress-simulation.webp)

Cu o linie de bază, simularea compară: un element neconstruit (sau neînceput) într-o zi în care linia de bază îl avea așa e **întârziat** (carmin), iar unul în lucru sau construit înainte ca linia de bază să-l înceapă e **înainte** (verde). Până la data de stare elementele stau pe zilele lor reale: unul marcat realizat e construit în ziua în care a fost marcat. **Comparat cu linia de bază**, din setările simulării, o oprește; tot acolo se aleg și cele două culori. Vederile și planșele la o dată arată la fel.

### Progresul în fișiere

- **Export XLSX** adaugă coloanele **% complete**, **Actual start**, **Actual finish**, **Baseline start**, **Baseline finish**, **Finish variance** și **GlobalId** (registrul e în engleză; un tabel făcut de mână le poate numi și *% realizat*, *Început real*, *Final real*). Completează-le pe șantier și citește-le înapoi cu **Planificare → Importă progresul…**: rândurile sunt găsite după GlobalId, WBS sau ID, iar o *Data de stare* (sau *Status date*) scrisă deasupra tabelului pune data de stare. Merge și un CSV.
- **MS Project XML** poartă data de stare, procentul fiecărei sarcini, începutul și finalul real, durata rămasă și linia de bază (Baseline 0); marcajele elementelor merg cu elementele, în *Lapin elements*.
- **IFC** le scrie în IfcTaskTime-ul fiecărei sarcini (timpul de stare, începutul și finalul real, timpul rămas, gradul de realizare), iar linia de bază ca al doilea IfcWorkSchedule, de tip BASELINE; câmpurile proprii ale lui Lapin (procentul scris, marcajele) merg în `Lapin_Task`. Amândouă revin la aceleași date.
- **Gestionare → Versiuni → Compară** spune, pe lângă elemente, ce s-a schimbat în program: finalul, zilele lucrătoare, data de stare și sarcinile adăugate, șterse, mutate (cu câte zile lucrătoare), cele cu durata sau progresul schimbat.

## Calendarul

![Calendarul de lucru](shots/planning-calendar.webp)

**Planificare → Calendar…** stabilește:

- **zilele lucrătoare** ale săptămânii (de luni până vineri, implicit) și **orele** unei zile (8, de la 08:00);
- **sărbătorile**: sărbătorile legale din România, calculate pentru fiecare an (cu Vinerea Mare, Paștele și Rusaliile ortodoxe), sau niciuna. Lista de dedesubt arată sărbătorile unui an, ca să le verifici;
- **pauza de iarnă** pentru lucrările umede, oprită implicit: de la 15 decembrie la 1 martie, dacă nu alegi alte zile;
- **excepțiile**: zile libere ale proiectului (un șantier închis) sau zile lucrate care altfel n-ar fi (o sâmbătă).

Duratele și decalajele numără zilele lucrătoare ale acestui calendar.

## Schimbul programului

- **Exportă MS Project…** scrie un fișier MS Project XML. Îl deschid MS Project, ProjectLibre și GanttProject, iar Primavera P6 îl importă. Conține sarcinile cu WBS-ul, duratele, datele, constrângerile și rezervele lor, legăturile cu tipurile și decalajele lor și calendarul, cu fiecare sărbătoare ca excepție. **Importă MS Project…** citește un astfel de fișier în locul programului proiectului. Un program scris de Lapin și citit din nou își păstrează id-urile sarcinilor și datele.
- **Exportă IFC…** (și din **Fișier**) scrie programul în fișierul IFC, împreună cu modelul: un IfcWorkSchedule, IfcTask-urile cu timpii lor, legăturile IfcRelSequence și IfcWorkCalendar. Importul unui fișier IFC care are un program îl aduce și pe acesta.
- **Export XLSX** scrie un registru de calcul: tabelul Gantt-ului și zilele libere ale calendarului.

Câmpurile proprii ale lui Lapin pentru care alte programe n-au loc (constrângerea unei sarcini, dacă e umedă, pauza de iarnă) merg în IFC ca seturile de proprietăți `Lapin_Task` și `Lapin_Calendar`. MS Project nu are sarcini umede, deci pauza de iarnă rămâne în Lapin.

## Ce urmează

Devizul vine în etapele următoare.
