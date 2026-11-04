# 4D și 5D: programul lucrărilor (Gantt) și devizul din model

Cerut de utilizator la 04.11.2026: „Mă gândesc la funcții 4D și 5D, inclusiv o diagramă Gantt, dar cu cel mai prietenos flux UX. Poți face un plan?”. Decizia 300 în `decisions-pending.md`: aprobată de utilizator la 04.11.2026 („ok, start implementing”), cu răspunsurile de mai jos. Etapa 1 făcută (decizia 301), etapa 2 făcută (decizia 302). Planul pornește de la ce e deja hotărât: fazele (`Lapin.Phases`), organizarea de șantier cu elementele ei temporare și articolele de deviz pe tipuri (deciziile 151–152), antemăsurătoarea (decizia 287), versiunile (decizia 134), CDE și BCF (deciziile 180–183), și de la planul vechi `Lapin/docs/pereti-compusi-si-executie.md` (partea 3: rețetele tehnologice; partea 4: catalogul de articole editabil ca tabel, cu import și export; „4D: sarcini generate pe etaj × categorie, cu durate din norme și echipe”).

## Ce există deja

| Ce | Unde | Ce aduce pentru 4D/5D |
|---|---|---|
| Fazele proiectului, cu starea fiecărui element (nou, existent, demolat, temporar) și filtrele vederilor | `Lapin.Phases`, `PhaseSettings.State`, `IViewElementFilter` | modelul stărilor și desenarea lor în vederi și pe planșe; 4D le folosește pe aceleași, doar că pe o dată, nu pe o fază |
| Elementele temporare în faza Execution (cofraje, schele), cu seturi și refolosiri | `Lapin.SiteWorks`, `IPhaseService`, `IPhaseFollowers` | ciclul de viață al temporarelor: apar la turnare, dispar la decofrare |
| Articolul de deviz pe tipul de cofraj (`article`, `articleText`) | `Formwork.cs` | primul „articol pe tip”; aici devine regula generală |
| Antemăsurătoarea: cantitățile fiecărui element, pe tip, nivel și material (stratul, zona, finisajul) | `QuantityTakeoff`, `IQueryContext.Quantities` | cantitățile pentru durate (cantitate × normă) și pentru deviz (cantitate × preț) |
| Extrasele de modul: încăperi și finisaje (brut și net pe material), lemn, oțel cu greutăți, armătură, cofraje, schele, MEP | `IScheduleSource` | cantități deja măsurate pentru articolele lor |
| Versiunile, cu comparația element cu element | `project.compareVersions`, `ModelCompare` | „ce s-a schimbat la cost și la termen între v3 și v4” |
| Emiterea, verificările, observațiile BCF | `Lapin.Platform/Cde` | programul și devizul intră într-o emitere; o regulă „elemente fără sarcină” |
| Panourile (split, ferestre detașate), extrasele pe planșă, configuratoarele cu previzualizare aplicate ca un pas | `ui/src/app/pane.tsx`, `splitPane`, `sheet.addViewport` | Gantt-ul e o vedere ca oricare; generatorul e un configurator |
| Lucrul împreună (oaspeți prin relay, din browser) | `EngineDispatcher.AllowedRemote` | actualizarea progresului de pe șantier, de pe telefon |

Lapin-ul vechi avea un Gantt și un panou de costuri, dar erau ale ifc-lite (MPL): **nu se portează**. Din fișierele proprii ale fork-ului rămân doar traducerile (`gantt-work-calendar.ro.ts`, `cost-panel.ro.ts`), ca listă de termeni. Totul se scrie din nou.

## Granița

- **Modulul deschis `planning`** (4D), în acest depozit, `engine/src/Modules/Construction/Lapin.Planning` și `ui/src/modules/planning`, `requires: core, phases`; în ediția `lapin`. Tabul **Planning**. Programul lucrărilor (sarcini, legături, calendar, drumul critic), Gantt-ul, legarea elementelor prin reguli, generatorul din model, simularea 4D, progresul cu o linie de bază, IFC și MS Project XML.
- **Modulul deschis `cost`** (5D de bază): modulul UI `cost` există (tabul Quantities); primește partea lui de engine, `engine/src/Modules/Construction/Lapin.Cost`, `requires: core, architecture`. Listele de prețuri ale utilizatorului (import CSV/XLSX), articolele pe tipuri și regulile de articole, devizul (capitole, articole, resurse: material, manoperă, utilaj, transport), cotele, formularele, IFC cost schedule, curba costului în timp (când există și `planning`, prin cârlige, fără dependență).
- **Modulul premium `planning-pro`**, într-un depozit privat alăturat (`../lapin-planning`, `requires: planning, cost`, legat prin cârlige ca `mep-pro`): resursele încărcate și nivelate (histograme, echipe, utilaje, seturile de cofraj ca resursă limitată), Primavera P6 (XER, XML), valoarea câștigată (PV, EV, AC, SPI, CPI), situațiile de lucrări lunare din progres, mai multe linii de bază și scenarii comparate, aplicația de șantier cu fotografii pe sarcini.
- Ca la `steel` și `wood`: **se stochează regulile, legăturile sunt derivate.** O sarcină ține un filtru (etaj, clase, tipuri, material, grup, fază) și excepțiile (elemente adăugate sau scoase de mână); elementele ei se calculează din model, deci un perete desenat azi intră singur în sarcina „Zidărie etaj 1”. La fel un articol pe tip: cantitatea e citită din model la fiecare calcul, nu copiată.
- **Normele și prețurile sunt ale utilizatorului.** Indicatoarele de norme și cataloagele de prețuri sunt date proprietare: Lapin nu le livrează. Livrează un șablon de tabel (coloanele, câteva rânduri exemplu cu valori orientative scrise de noi) și importul listelor utilizatorului; o listă importată stă în biblioteca aplicației, deci se refolosește în proiectele următoare.

## Modelul

Totul e IFC nativ, ca restul lui Lapin: entitățile sunt cele din IFC 4, cu atributele lui Lapin pe ele.

### Programul (4D)

- **`IfcWorkSchedule`**: un program al proiectului (de obicei unul; PredefinedType `PLANNED`), cu data de start, calendarul implicit și data de stare (*data date*), declarat pe proiect (`IfcRelDeclares`).
- **`IfcTask`**: o sarcină sau un rezumat (WBS). Ierarhia prin `IfcRelNests` (Lapin o ține ca `parent` + ordinea). `IfcTaskTime`: `ScheduleStart`, `ScheduleFinish`, `ScheduleDuration` (zile lucrătoare), `ActualStart`, `ActualFinish`, `Completion`, `IsCritical`, `FreeFloat`, `TotalFloat`. Atributele lui Lapin: `rule` (filtrul), `add` / `remove` (excepțiile, după GlobalId), `part` (ce parte a elementelor: tot, miezul, finisajele, un material; vezi mai jos), `crew` (mărimea echipei), `norm` (codul normei), `own` (editată de mână: generatorul nu o mai rescrie), `baseline` (start, final).
- **`IfcRelSequence`**: legătura dintre două sarcini, `SequenceType` `FINISH_START` / `START_START` / `FINISH_FINISH` / `START_FINISH`, `TimeLag` ca `IfcLagTime` (zile, poate fi negativ).
- **`IfcWorkCalendar`**: zilele și orele lucrătoare (`IfcWorkTime` cu `IfcRecurrencePattern`), sărbătorile legale din România calculate (inclusiv Paștele și Rusaliile ortodoxe, după algoritmul calendarului iulian), excepțiile proiectului; opțional o **perioadă de iarnă** în care sarcinile marcate „umede” (betoane, tencuieli) nu lucrează. Un calendar pe program, altul pe sarcină dacă vrea.
- **Partea unui element.** Un perete stratificat e zidit într-o sarcină și tencuit în alta. Legătura poate lua doar o parte: miezul, finisajele (după funcția stratului), un material sau o categorie de material, zonele și centurile (pereții compuși), un buiandrug. Simularea arată atunci peretele crescând în două trepte. Fără `part`, elementul întreg.
- **Elementele temporare.** Cofrajul unei plăci și schela unei fațade nu au sarcina lor: `sitework` oferă `ITaskLife` (un serviciu prin platformă, ca `IPhaseFollowers`), care spune, din sarcinile gazdelor, când apare și când dispare fiecare: cofrajul de la începutul turnării până la decofrare (`stripAfter` pe tipul de cofraj, zile, implicit după element: 7 la pereți și stâlpi, 21 la plăci și grinzi), schela de la prima sarcină de anvelopă de pe fața ei până la ultima de finisaj exterior.
- **Demolările.** Un element cu `phaseDemolished` intră într-o sarcină de demolare (generatorul o face prima); în simulare e roșu cât durează sarcina, apoi dispare.
- **Drumul critic** (CPM, înainte și înapoi, pe calendar) e calculat de engine la fiecare schimbare (`Schedule.Solve`), nu de interfață; rezultatul (date, rezerve, critic) e derivat, nu stocat, și e scris în `IfcTaskTime` doar la export.

### Costurile (5D)

- **Lista de prețuri** (`LapinPriceList`): un tabel importat, nu entități: cod, denumire, unitate, preț unitar defalcat pe **material, manoperă, utilaj, transport**, opțional norma de timp (ore-om pe unitate) și consumurile de resurse. În `.lapin` e o parte Arrow a modulului (`cost/pricelists/<id>`), cum sunt tabelele pe clase; poate avea mii de rânduri fără să încarce modelul. Moneda pe listă (RON implicit, EUR cu cursul proiectului).
- **Articolul pe tip** (`articles` pe orice tip: perete, placă, ușă, finisaj de încăpere, cofraj, profil metalic…): o listă de articole, fiecare cu codul, **baza de măsură** (una din cantitățile pe care le dă elementul: volum net, arie netă pe o față, lungime, bucăți, masă; sau a unui strat sau material din antemăsurătoare), un coeficient și pierderile (%). Articolul pe cofraj din decizia 152 devine un caz al acesteia.
- **Regulile de articole** (`LapinArticleRule` pe proiect): „orice strat din categoria *tencuială* → articolul X pe arie netă”, ca să acopere dintr-un singur rând toate tipurile care folosesc un material. Articolul de pe tip are întâietate față de regulă.
- **Devizul** (`IfcCostSchedule`, PredefinedType `COSTPLAN` sau `PRICEDBILLOFQUANTITIES`): capitolele (categoriile de lucrări; implicit după disciplină și tip de element, sau după WBS-ul programului) și articolele ca **`IfcCostItem`** imbricate (`IfcRelNests`), fiecare cu `CostQuantities` (cantitatea, din model) și `CostValues` (**`IfcCostValue`** pe categorie: Material, Labour, Equipment, Transport; `UnitBasis`). Elementele măsurate și sarcinile legate prin **`IfcRelAssignsToControl`**. Liniile devizului sunt derivate din articole × cantități; un rând adăugat de mână (o lucrare care nu e în model) e păstrat ca atare.
- **Cotele**: cheltuieli indirecte (%), profit (%), contribuțiile pe manoperă, TVA (cota în vigoare, configurabilă), pe deviz, cum le cere structura românească.
- **Costul în timp**: fiecare linie de deviz se împarte pe sarcinile elementelor ei (pe durata lor, liniar sau după datele de terminare ale elementelor), deci curba S și tabelul lunar ies din program fără nicio legare în plus.

### Ce iese din model, pe scurt

```
model ──► antemăsurătoare (cantități pe element, strat, material)
   │            │
   │            ├─► × normă / echipă ──► durata sarcinii ──► program (CPM) ──► 4D
   │            │
   │            └─► × preț (articol pe tip / regulă) ──► deviz ──► 5D
   │                                                          │
   └─► sarcina elementului (din regula ei) ◄──────────────────┘ costul pe sarcină, curba S
```

## UX: drumul utilizatorului

Principiile: **nimic nu e obligatoriu** (un proiect fără program e un proiect ca azi); **un clic dă un rezultat complet** (generatorul face un program cu toate elementele legate, apoi îl ajustezi); **orice legătură automată se vede și se poate schimba**; fiecare operație e un pas de undo; **aceeași dată** e în Gantt, în 3D, în planuri și pe planșe; selecția e sincronizată în ambele sensuri (sarcină → elemente, elemente → sarcini).

### Pasul 1: Planning → Generate schedule… (2 clicuri)

Configuratorul, ca al acoperișului și al halei: stilurile în stânga, filele la mijloc, previzualizarea engine-ului în dreapta (`planning.preview`), aplicat ca un singur pas.

```
┌ Generate schedule ─────────────────────────────────────────────────────────────────────────────┐
│ TEMPLATES          │ [Start] [Sequence] [Durations] [Calendar]   │ PREVIEW                       │
│ ● Concrete frame   │                                             │ ▇▇▇ Site set-up               │
│ ○ Load-bearing     │  Start    [ 10.11.2026 ]  (next Monday)     │   ▇▇▇▇ Excavation             │
│   masonry          │  Split by [x] storey  [ ] zone              │      ▇▇▇▇ Foundations         │
│ ○ Steel hall       │  Phases   [x] demolition first (3 elem.)    │         ▇▇▇ Structure GF      │
│ ○ Timber frame     │  Temporary[x] formwork  [x] scaffolding     │            ▇▇▇ Structure 1    │
│ ○ My: Bloc P+4     │                                             │               ▇▇▇▇ Roof       │
│ ○ Empty            │  SEQUENCE (drag to reorder)                 │                ▇▇▇▇▇ Envelope │
│                    │  ≡ Earthworks       IfcEarthworks…  once    │                   ▇▇▇▇▇ Finish│
│ [Save as style…]   │  ≡ Foundations      Footing, Slab(b) once   │ ───────────────────────────── │
│                    │  ≡ Structure        Column,Beam,Slab per st.│ 42 tasks · 187 working days   │
│                    │  ≡ Masonry          Wall core       per st. │ finish 21.08.2027             │
│                    │  ≡ Roof             Roof, Member    once    │ 1 284 / 1 284 elements linked │
│                    │  ≡ Envelope         windows, ext. finishes  │ durations: 38 from norms,     │
│                    │  ≡ Finishes         room finishes   per st. │   4 by default (no norm) ⚠    │
│                    │                                             │                    [ Apply ]  │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Șablonul e ales singur** după model: hală metalică dacă există `LapinSteelHall`, lemn dacă pereții au `construction: timberFrame`, zidărie portantă dacă nu există stâlpi, altfel cadre din beton armat. Utilizatorul doar confirmă.
- Un pas de secvență: numele, **ce elemente** (clase, tipuri, partea: miez sau finisaje, un filtru ca în extrase), **cum se împarte** (o dată, pe etaj, pe zonă), cu ce se leagă (FS cu pasul anterior pe același etaj; același pas pe etajul următor după cel de dedesubt; SS cu decalaj pentru finisaje, care urcă după structură).
- **Durata** = cantitatea sarcinii (din antemăsurătoare) × norma (ore-om pe unitate) ÷ (echipa × orele zilei). Norma vine din lista de prețuri (coloana normei), din tabelul de norme al utilizatorului sau din valoarea implicită a pasului; previzualizarea spune câte durate au venit din norme și câte nu (⚠, cu lista).
- **Contorul „elemente legate”** e cel mai important număr din fereastră: ce rămâne nelegat e listat, cu un clic arătat în 3D, și poate primi un pas nou.
- Generat din nou, programul păstrează sarcinile editate de mână (`own`) și legăturile trase de utilizator; cele generate sunt refăcute pe aceleași id-uri (ca nodurile halei).
- Stilurile proiectului (*Save as style…*), ca stilurile de acoperiș: o firmă își face o dată șablonul ei.

### Pasul 2: ecranul se împarte, ▶ (1 clic)

După Apply, panoul se împarte singur pe orizontală: 3D sus, Gantt jos (`splitPane('column')`, Gantt-ul fiind o vedere de tipul `gantt`). Cursorul de timp stă la data de start; ▶ pornește simularea.

```
┌ Ribbon: Planning ─ Generate · Gantt · Link · Baseline · Progress · Simulate · Export ───────────┐
├─────────────┬──────────────────────────────────────────────────────────────────┬────────────────┤
│ Browser     │  3D: elements by status at 14.03.2027                            │ Properties     │
│  Work sched.│      ░ to build (hidden or ghost)   ▓ in progress (amber)        │ Task           │
│   ▸ Main    │      █ done   ▒ demolished (red)    ◌ temporary (formwork)       │  Structure 1   │
│  Cost       │                                                                  │  Start 02.03.27│
│   ▸ Deviz   │                     [3D view of the building at that date]       │  Dur.  12 d    │
│             │                                                                  │  Crew  6       │
│             │  ◀◀  ▶  ▶▶   1 day/s ▾   ━━━━━━━━━━●━━━━━━━━━━━━  14.03.2027      │  Rule  Column, │
│             ├──────────────────────────────────────────────────────────────────┤   Beam, Slab;  │
│             │ Task               Start     Dur  % │ Mar 2027      Apr 2027       │   storey = 1   │
│             │ ▾ Building                         │ │  │  │  │  │  │  │  │  │    │  +2 / −1 el.  │
│             │   Foundations      10.11.26  24  100│ ██                           │  Elements 214  │
│             │ ▾ Structure                         │                              │  Cost 412 300  │
│             │   Structure GF     15.12.26  14  100│  ███                         │  Predecessors  │
│             │   Structure 1      02.03.27  12   40│     ▓▓▓▓░░░──┐  (critical)   │   Struct. GF FS│
│             │   Masonry GF       17.03.27  10    0│              └►░░░░░          │                │
│             │   Finishes GF      30.03.27  15    0│                  ░░░░░░░░     │                │
│             │                                     ●  ← date cursor, same as 3D   │                │
└─────────────┴─────────────────────────────────────┴───────────────────────────────┴────────────────┘
```

### Pasul 3: ajustarea, direct pe Gantt

- **Mută** o bară trăgând-o, **lungește** de capăt (zile lucrătoare, după calendar); valorile apar în timp ce tragi, ca lungimile la desenat; tastate în coloană sau în Proprietăți.
- **Leagă** trăgând din cercul de la capătul unei bare pe altă bară: din capăt spre început FS, din început spre început SS (după capetele atinse, deci fără meniu); un clic pe săgeată schimbă tipul și decalajul.
- **Selecția e una singură**: o sarcină selectată își arată elementele în 3D și în plan; elementele selectate își arată sarcinile în Gantt (rânduri marcate). Dublu-clic pe bară: zoom în 3D pe elementele ei.
- **Atribuirea manuală**: selectezi în 3D și **tragi selecția pe rândul sarcinii** (cartea selecției din colț se trage), sau comanda `TA` (*assign to task*) cu sarcina aleasă din listă, sau meniul contextual *Add to task ▸*. Shift scoate. Excepțiile intră în `add` / `remove`; regula rămâne.
- **Regula se editează** în Proprietăți, ca filtrul unui extras; numărul elementelor se actualizează pe loc.
- Tastatura ca într-un tabel (decizia: panoul de proprietăți ca foaie de calcul): Enter coboară, Tab trece la coloana următoare, Insert adaugă o sarcină, Alt+← / Alt+→ schimbă nivelul în WBS.
- **Zoom** pe zile, săptămâni, luni, trimestre (rotița cu Ctrl; *Fit*); WBS-ul se strânge și se desface; drumul critic e roșu, comutabil; coloanele alese (start, final, durată, %, rezervă, cost, elemente).
- **Gantt-ul e o vedere**: în panou, într-o fereastră detașată (al doilea monitor), pe o planșă (desenat vectorial de engine, ca extrasele: *graficul de execuție* pentru documentație).

### Pasul 4: costurile (o dată pe firmă, apoi un clic pe proiect)

1. **Quantities → Price lists… → Import**: un CSV sau XLSX; o fereastră potrivește coloanele (cod, denumire, UM, material, manoperă, utilaj, transport, normă), ghicite după antet, cu primele rânduri arătate. Lista intră în bibliotecă și în proiect.
2. **Quantities → Map articles…**: un tabel cu tipurile proiectului (și materialele, pentru reguli), pe fiecare rând articolul sugerat (după cod sau după cuvintele din denumire: „zidărie”, „BCA”, „tencuială”, „C25/30”) și baza de măsură propusă; utilizatorul confirmă, schimbă sau caută. Ca *Map imported types*: alegerile se țin minte pentru proiectele următoare.
3. **Quantities → Estimate** deschide devizul: o vedere ca un extras.

```
┌ Estimate: Bloc P+4 ─ group [By chapter ▾] ─ list [Prețuri 2026 ▾] ─ RON ─ [Place on sheet] [XLSX] ┐
│ Code      Description                     UM    Qty      Mat.     Man.    Util.  Transp.   Value │
│ ▾ 1 Structure                                                                         1 284 100 │
│   CB02A1  Beton C25/30 în stâlpi           m³    48,20   410,00   95,00   22,00   18,00   26 943 │
│   CB04A1  Cofraj placaj stâlpi             m²   386,00    35,00   48,00    4,00    2,00   34 740 │
│ ▾ 2 Masonry                                                                             612 400 │
│   CC02F1  Zidărie BCA 30 cm                m³   212,40   380,00   85,00    6,00   14,00  103 584 │
│ ...                                                                                             │
│ Subtotal 4 212 000 · Indirect 10 % · Profit 5 % · VAT · Total 5 541 000 RON   ⚠ 3 types unpriced │
└─────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- Un rând al devizului selectat arată în 3D elementele care îl dau; un element selectat arată rândurile lui (ca la program).
- **Tipurile fără preț** sunt numărate și listate, ca elementele fără sarcină.
- Formularele românești (lista cantităților de lucrări pe categorii, listele de resurse: materiale, manoperă, utilaj, transport) sunt grupări ale aceluiași deviz, exportate XLSX și PDF și puse pe planșă.
- Când există un program, Gantt-ul primește coloana **Cost** și vederea **Cost în timp**: curba S (cumulat) și barele lunare.

### Pasul 5: pe planșe și în emitere

- O vedere (plan, secțiune, 3D) are în proprietăți **State at date** (sau *the cursor's date*): desenează modelul cum e la acea dată, ca filtrul de fază; pe planșă se tipărește așa.
- **Planning → Stages sheet…**: o planșă cu vederi 3D la datele alese (sfârșitul fiecărui capitol, sau la fiecare lună), etichetate cu data și sarcinile în lucru.
- **Simulate → Export video**: cadrele cursorului pe un interval, într-un fișier video (MP4 sau WebM, după ce știe vizualizatorul sistemului; altfel o serie de PNG).
- **Manage → Issue…** poate include programul (PDF, XLSX, MS Project XML, IFC) și devizul (PDF, XLSX).

### Pasul 6: progresul

- **Planning → Progress**: data de stare, procentele și datele reale, în Gantt sau în Proprietăți.
- **Mark done** pe o selecție în 3D, la data de stare: elementele primesc data terminării; procentul sarcinii e cantitatea gata ÷ cantitatea ei. Pe șantier, același lucru de pe telefon, intrând în sesiunea partajată din browser (oaspetele are voie doar la `planning.markDone` și la observații).
- **Baseline**: *Save baseline* copiază datele planificate; Gantt-ul arată bara de bază subțire sub cea curentă; rândurile întârziate sunt marcate, cu zilele.
- O versiune salvată ține și programul și devizul; **Compare** între două versiuni spune, pe lângă elemente, **diferența de cost** pe capitol și articol și **diferența de termen** (data de final, sarcinile mutate).

## Simularea 4D, tehnic

- `planning.timeline` dă interfeței, o dată pe schimbare, pentru fiecare element (și parte), intervalele lui: când apare, cât e în lucru, când e gata, când dispare (temporarele, demolările). Interfața aplică starea la data cursorului fără să întrebe engine-ul la fiecare cadru: vizibilitatea și culoarea pe mesh, pe instanță la `InstancedMesh` (decizia 166), deci simularea merge la 60 fps și pe modele mari.
- Culorile stărilor sunt ale proiectului, ca suprascrierile fazelor; opțional „ce urmează” ca fantome translucide.
- Planurile, secțiunile și planșele folosesc același calcul în engine (`IViewElementFilter`), ca fazele, deci PDF-ul arată ce arată ecranul.

## Schimbul

| Format | Ce | Unde |
|---|---|---|
| IFC 4 | `IfcWorkSchedule`, `IfcTask` + `IfcTaskTime`, `IfcRelSequence` + `IfcLagTime`, `IfcWorkCalendar`, `IfcRelNests`; elementele legate ca **ieșiri** ale sarcinii (`IfcRelAssignsToProduct`), temporarele și resursele ca **intrări** (`IfcRelAssignsToProcess`); la import se citesc ambele relații (programele scriu una sau alta). `IfcCostSchedule`, `IfcCostItem`, `IfcCostValue`, `IfcRelAssignsToControl`, `IfcMonetaryUnit`. Citit înapoi la aceleași date și valori. | `planning`, `cost` (`IIfcProcesses`, `IIfcControls` în platformă) |
| MS Project XML (MSPDI) | sarcini, WBS, legături cu decalaj, calendar, procente, date reale, linia de bază; legătura cu elementele printr-un câmp text (GlobalId-urile sau regula) | `planning` |
| CSV / XLSX | Gantt-ul ca tabel, devizul, formularele, listele de resurse; listele de prețuri și normele la import | `planning`, `cost` |
| Primavera P6 (XER, XML) | sarcini, legături, calendare, resurse | `planning-pro` |
| BCF | o observație pe o sarcină sau pe un rând de deviz (cu elementele și camera la data cursorului) | CDE existent |
| `.lapin` | sarcinile, legăturile, calendarele, devizul sunt entități, deci intră în tabelele Arrow pe modul × clasă (partea proiectului, fără etaj); listele de prețuri ca parte proprie a modulului `cost` | pachetul, format 3 |

## Etape

1. **Programul și Gantt-ul** (făcută, decizia 301) — modulul `planning`: `IfcWorkSchedule`, `IfcTask`, legăturile FS/SS/FF/SF cu decalaj, WBS, calendarul cu sărbătorile românești, drumul critic în engine; vederea `gantt` (în panou, detașată), editarea prin tragere, tastatura, sarcina în Proprietăți; IFC (procesele) și MS Project XML în ambele sensuri, XLSX.
2. **Legarea elementelor** (făcută, decizia 302) — regula pe sarcină și excepțiile, părțile elementelor (miez, finisaje, material, categorie de material), tragerea selecției pe un rând, `TA`, selecția sincronizată, contorul elementelor nelegate și regula CDE „elemente fără sarcină”; IFC (`IfcRelAssignsToProduct` pentru ce construiește sarcina, `IfcRelAssignsToProcess` pentru ce demolează, regula în `Lapin_Task`), MS Project (câmpul Text1 „Lapin elements”), XLSX. O parte fără entitate IFC proprie (stratul unui perete) se scrie ca elementul întreg, partea în `Lapin_Task.Part`.
3. **Generatorul** — configuratorul cu șabloanele (cadre, zidărie, hală, lemn), alegerea șablonului după model, secvența pe etaje, duratele din cantități × norme (tabelul de norme al utilizatorului), stilurile proiectului, regenerarea care păstrează editările; temporarele din `sitework` (`ITaskLife`, `stripAfter`) și demolările din faze.
4. **Simularea 4D** — `planning.timeline`, stările colorate în 3D, cursorul și redarea, *State at date* în vederi și pe planșe, planșa de etape, Gantt-ul pe planșă, exportul video.
5. **Progresul** — data de stare, procente și date reale, *Mark done* în 3D (și de pe telefon, prin sesiunea partajată), linia de bază, întârzierile, comparația versiunilor la termen.
6. **Devizul** — modulul `cost` în engine: importul listelor de prețuri (potrivirea coloanelor, biblioteca), articolele pe tipuri și regulile pe materiale, *Map articles*, devizul cu capitole și resurse, cotele, formularele, XLSX și PDF, pe planșă, IFC cost schedule în ambele sensuri.
7. **Costul în timp** — costul pe sarcină, coloana Cost în Gantt, curba S și tabelul lunar, comparația versiunilor la cost (pe capitol și articol).
8. **Premium (`planning-pro`)** — resursele și nivelarea (histograme, echipe, utilaje, seturile de cofraj limitate), P6, valoarea câștigată, situațiile de lucrări lunare din progres, mai multe linii de bază și scenarii, aplicația de șantier cu fotografii.

Fiecare etapă: teste în engine (`PlanningTests`, `CostTests`: CPM pe exemple cunoscute, calendarul pe anii 2026–2030 cu Paștele ortodox, duratele din norme, IFC și MSPDI citite înapoi la aceleași cifre, devizul egal cu antemăsurătoarea × prețurile), teste UI (Gantt: tragerea, legarea, tastatura), paginile ghidului (`docs/user/planning.md`, `docs/user/cost.md`, în toate limbile) și scenele lor (`planning`, `planning-simulation`, `cost-estimate`).

## Răspunsuri

Utilizatorul a aprobat planul („ok, start implementing”, 04.11.2026) și a acceptat fără obiecții răspunsurile recomandate:

1. **Granița**: ca propus. `planning` și `cost` deschise, în ediția `lapin`; `planning-pro` premium. Curba S liberă; situațiile de lucrări lunare din progres premium.
2. **Formularele devizului**: deschis (etapa 6); un deviz real de reprodus e încă cerut.
3. **Programele de deviz** cu care se schimbă date: deschis (etapa 6).
4. **Normele**: doar importul, plus un mic șablon cu valori orientative scrise de Lapin; un tabel real de productivități e încă cerut utilizatorului, pentru teste.
5. **Părți de elemente într-o sarcină**: da, din etapa 2.
6. **Calendarul**: 5 zile × 8 ore și sărbătorile legale din România implicit; pauza de iarnă pentru lucrările umede există, oprită implicit.
7. **Progresul de pe șantier**: marcarea simplă liberă, prin sesiunea partajată; fotografiile și rapoartele premium.
8. **Moneda**: RON implicit, EUR la cursul proiectului; devizele în două monede rămân deschise.
9. **Taburile**: două, **Planning** și Quantities redenumit **Cost** (redenumirea poate aștepta etapa 6).
10. **Fazele** rămân separate; 4D folosește datele.
