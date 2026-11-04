---
title: Planificare
order: 97
summary: Programul lucrărilor într-o diagramă Gantt: sarcini într-un WBS, jaloane, legături cu decalaje, drumul critic calculat pe un calendar cu sărbătorile legale din România; schimb cu MS Project, IFC și Excel.
---

## Tabul Planificare

![Tabul Planificare](shots/ribbon-planning.webp)

- **Generează programul…**: un program întreg făcut din model, dintr-un singur pas (mai jos).
- **Gantt**: deschide programul sub vedere. Ecranul se împarte în două rânduri: modelul sus, programul jos.
- **Calendar…**: zilele și orele lucrătoare, sărbătorile și pauza de iarnă (mai jos).
- **Sarcină** și **Jalon**: o sarcină sau un jalon nou înaintea sarcinii selectate în Gantt (sau la sfârșit).
- **Retrage în dreapta**, **Scoate la stânga**, **Leagă sarcinile** și **Dezleagă sarcinile**: WBS-ul și legăturile sarcinilor selectate.
- **Adaugă la sarcină…** (TA) și **Elemente fără sarcină**: elementele modelului în sarcini (mai jos).
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

Simularea 4D a clădirii care crește în 3D, progresul și liniile de bază, și devizul vin în etapele următoare.

