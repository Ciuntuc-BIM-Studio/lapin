---
title: Planificare
order: 97
summary: Programul lucrărilor într-o diagramă Gantt: sarcini într-un WBS, jaloane, legături cu decalaje, drumul critic calculat pe un calendar cu sărbătorile legale din România; schimb cu MS Project, IFC și Excel.
---

## Tabul Planificare

![Tabul Planificare](shots/ribbon-planning.webp)

- **Gantt**: deschide programul sub vedere. Ecranul se împarte în două rânduri: modelul sus, programul jos.
- **Calendar…**: zilele și orele lucrătoare, sărbătorile și pauza de iarnă (mai jos).
- **Sarcină** și **Jalon**: o sarcină sau un jalon nou înaintea sarcinii selectate în Gantt (sau la sfârșit).
- **Retrage în dreapta**, **Scoate la stânga**, **Leagă sarcinile** și **Dezleagă sarcinile**: WBS-ul și legăturile sarcinilor selectate.
- **Importă MS Project…**, **Exportă MS Project…**, **Export XLSX** și **Exportă IFC…**: programul spre și dinspre alte programe (mai jos).

Un proiect are un singur program. Se face prima oară când adaugi o sarcină și începe lunea următoare. Data de start se schimbă din bara Gantt-ului.

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

Legarea elementelor modelului de sarcini, un program generat din model, simularea 4D a clădirii care crește în 3D, progresul și liniile de bază, și devizul vin în etapele următoare.
