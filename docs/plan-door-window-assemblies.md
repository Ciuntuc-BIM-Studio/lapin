# Uși și ferestre combinate într-un singur obiect: plan

Cerut pe forum (Robby, subiectul #34, 12.11.2026): „Sper să putem combina ușile și ferestrele într-un singur obiect, iar în setări să specificăm grosimea sticlei, tipul sticlei, valoarea U și alte proprietăți.” Doar planul; nimic implementat încă. Răspunsurile utilizatorului la întrebările de la sfârșit hotărăsc etapele.

## 1. Ce are Lapin azi

- **Configuratorul de tâmplărie** (`JoinerySpec`, `Joinery.cs`, dialogul `JoineryDialog.tsx`): un tip de ușă sau fereastră e o grilă de coloane și rânduri (`Columns`, `Rows`, ponderi relative), cu montanți și traverse de lățimi proprii (decizia 466: `Mullions`, `Transoms`, 0 = fără bară), panouri care ocupă una sau mai multe celule (`JoineryPanel`: col, row, span, operație, umplutură `glass`/`solid`). Un tip de **ușă** poate avea deja foi de ușă (`swing-*`, `double-acting-*`, `door-sliding-*`, `folding-*`) și panouri fixe alături sau deasupra (lumină laterală, supralumină): o ușă cu supralumină și lumini laterale e deja un singur tip, un singur `IfcDoor`. Ce nu poate: un **canat de fereastră care se deschide** (`side-*`, `tilt-turn-*`, `top-hung`…) într-un tip de ușă; panourile fixe ale unei uși au sticla tipului, nu una a lor.
- **Profile** (decizia 370, 417): sisteme PVC, aluminiu, lemn, oțel; profile din bibliotecă pentru toc și cercevea.
- **Foi care se întâlnesc** (decizia 466): falț, baghetă, zăvoare.
- **Uși în pereți-cortină** (raportul #133): o ușă stă într-o celulă a peretelui-cortină (`CurtainCells`), ca `IfcDoor` separat.
- **Sticlă fără toc** (decizia 444; arcul de pardoseală, decizia 488): uși și pereți de sticlă securizată, fără toc.
- **Date** (raportul #76, `JoineryData`): U-value, g-value, sticlă de siguranță, rezistență la foc, feronerie — text liber pe tip sau element; U-value ca număr devine `ThermalTransmittance` în `Pset_DoorCommon`/`Pset_WindowCommon`; `ThermalTransmittance` pe spec (un singur număr pe tip).
- **IFC**: `IfcDoor`/`IfcWindow` cu tipul lor, `IfcDoorLiningProperties`/`IfcDoorPanelProperties`, `IfcWindowLiningProperties`/`IfcWindowPanelProperties`, `OperationType`/`PartitioningType` (`JoineryIfc.cs`).
- **Tabele**: tabelul de uși și ferestre (`ScheduleLayout`): mărime, deschidere, U, foc, buiandrug; desenul fiecărui tip (`JoinerySymbols`).

Deci jumătate din cerere există (ușă cu lumini fixe într-un obiect); lipsesc canatele care se deschid lângă ușă și datele sticlei pe fiecare geam.

## 2. Cum fac alții

- **Revit**: *Storefront* este un tip de perete-cortină (grilă, montanți, panouri; o ușă e un panou-familie înlocuit într-o celulă). Ușile cu lumini laterale/supralumină sunt familii de ușă parametrice („Door-Sidelight”, „Door-Transom”). Ferestrele-ușă combinate sunt familii proprii sau grupuri. Sticla: material + parametri de familie; U-value un parametru de tip „Analytic Construction”/„Heat Transfer Coefficient (U)”.
- **Archicad**: obiectele de bibliotecă **Door-Window** („Ușă-fereastră”, ex. „Door-Window 25”, „Combined Door-Window”) — un singur obiect de ușă care are alături canate de fereastră care se deschid, cu toc comun; plus **Curtain Wall** cu panouri de ușă/fereastră. Exportat ca `IfcDoor` (obiectul e clasificat ușă), cu panourile de fereastră descrise doar parțial în IFC.
- **Vectorworks**: obiectele *Door* și *Window* au „sidelights”/„transom”; pentru combinații, *Window Assembly* (un ansamblu de uși și ferestre într-un cadru comun), exportat ca `IfcElementAssembly` cu ușile și ferestrele în el, sau ca elemente separate.
- **Producătorii și software-ul de tâmplărie** (LogiKal, Orgadata, ClassicLine, WinPVC): un „element” e un cadru cu câmpuri, fiecare câmp fix, canat sau ușă; sticla pe câmp (alcătuire, Ug, greutate); Uw calculat după EN ISO 10077-1. Asta e de fapt cererea lui Robby.
- **BubbleBIM/alții la citire**: un `IfcDoor` cu panouri fixe e citit ca ușă (OperationType dă foile); un `IfcElementAssembly` e citit de majoritatea vizualizatoarelor ca grup, cu ușile și ferestrele lui obișnuite (cantități corecte pe clase); un `IfcWindow` cu `USERDEFINED` e citit doar ca geometrie.

## 3. Variante pentru Lapin

**A. Un singur element de tâmplărie**: tipul de ușă primește și operațiile de fereastră în celulele lui (un canat oscilobatant lângă ușă, o supralumină rabatabilă). Un singur `IfcDoor` (clasa după celula „principală”: are foi de ușă → ușă; altfel fereastră). Avantaje: o singură piesă la producător, un singur toc, desen și tabel unitar, preview-ul motorului deja există, grila și profilele există. Dezavantaje: IFC nu are pentru `IfcDoor` panouri de fereastră (`IfcDoorPanelProperties.PanelOperation` nu are „tilt and turn”); cantitățile de ferestre (arie vitrată pe clasă) se pierd dacă totul e ușă.

**B. Un ansamblu**: o ușă și ferestre separate (fiecare tipul ei) legate într-un `Assembly`, așezate împreună, cu toc comun desenat ca parte a ansamblului. IFC: `IfcElementAssembly` (`PredefinedType` USERDEFINED „door-window”) cu `IfcDoor` și `IfcWindow` în el (`IfcRelAggregates`). Avantaje: clase IFC corecte pentru fiecare parte, cantități corecte, fiecare parte cu `IfcDoorPanelProperties` sau `IfcWindowPanelProperties` adevărate. Dezavantaje: toc comun greu (două tipuri cu toc fiecare, montantul dintre ele dublat sau tăiat), editare în două locuri, golul din perete unul singur pentru mai multe elemente (Lapin are un gol pe element), configuratorul trebuie să știe de mai multe tipuri deodată.

**C. A cu ieșire IFC după B (recomandat)**: în model e un singur tip de tâmplărie (varianta A: celule care pot fi foi de ușă, canate de fereastră sau fixe, sticla pe fiecare celulă); la export, un tip care are și foi de ușă și canate de fereastră se scrie ca `IfcElementAssembly` (tipul `IfcElementAssemblyType`) care agregă un `IfcDoor` (foile de ușă cu celulele lor) și câte un `IfcWindow` pe grupul de canate de fereastră, fiecare cu geometria lui din același corp, plus tocul comun ca `IfcMember`/`IfcBuildingElementPart` al ansamblului. Un tip care are doar foi de ușă și fixe rămâne `IfcDoor` (ca azi); doar canate și fixe rămâne `IfcWindow`. Seturile `Lapin_Intent` păstrează tipul întreg, ca un IFC citit înapoi în Lapin să refacă un singur element. Golul din perete e unul, al elementului.

## 4. Datele sticlei, pe geam

Pe fiecare panou cu sticlă (`JoineryPanel.Glazing`, implicit cel al tipului `JoinerySpec.Glazing`):

- **Alcătuirea**: straturi de sticlă și goluri, scrisă ca „4-16-4” sau „44.2-16-4-16-6” (sticlă laminată 44.2 = 4+4 cu 2 folii PVB); grosimea totală calculată din ea (nu stocată).
- **Tipul fiecărei foi**: float, securizată (ESG/tempered), laminată (VSG), termoizolantă low-e (pe ce față), de control solar, mată/sablată, ornamentală, rezistentă la foc.
- **Gazul** din fiecare gol: aer, argon (90 %), kripton; **distanțierul**: aluminiu sau „warm edge” (dă ψg).
- **Ug** (W/m²K): introdus de utilizator (de pe fișa producătorului) — Lapin nu calculează Ug din straturi (EN 673 cere emisivitățile și ar da o falsă precizie); un tabel orientativ de valori tipice (4-16-4 aer ≈ 2,7; argon low-e ≈ 1,1; tripan ≈ 0,6) e oferit ca punct de plecare, marcat orientativ.
- **g**, **Lt** (transmisie luminoasă), **Rw** (acustic), clasa de siguranță (EN 12600, ex. 1B1), greutatea (kg/m², calculată din grosimi: 2,5 kg/m² pe mm).
- **Uf** (W/m²K) pe sistemul de profile (tipul sau profilul din bibliotecă) și **ψg** (W/mK) pe distanțier.

**Uw** după **EN ISO 10077-1** (formula e publică):

  Uw = (Ag·Ug + Af·Uf + lg·ψg) / (Ag + Af)

cu Ag aria vitrată vizibilă, Af aria tocului și cercevelelor proiectată (deci fracția de toc Af/(Ag+Af)), lg perimetrul vizibil al sticlei. Toate trei se știu din corpul tipului (`Joinery.Boxes`: cutiile `glass`, `frame`, `sash`), deci Uw se **calculează la fiecare cerere**, nu se stochează (principiul „ce se poate deduce se deduce”). Panourile opace (umplutură `solid`) intră cu Up-ul lor (Ap·Up). Valoarea scrisă de mână de utilizator rămâne mai presus de cea calculată (ca azi la `uValue`). Normele și valorile sunt ale utilizatorului; ce oferă Lapin e marcat orientativ.

IFC: `Pset_DoorCommon`/`Pset_WindowCommon.ThermalTransmittance` = Uw, `GlazingAreaFraction`; pe fiecare geam o `IfcMaterialConstituentSet`/`IfcMaterialLayerSet` pentru sticlă (straturile cu grosimea lor: „Glass 4 mm”, „Argon 16 mm”), plus `Pset_DoorWindowGlazingType` (standard IFC: `GlassLayers`, `GlassThickness1..3`, `FillGas`, `GlassColor`, `IsTempered`, `IsLaminated`, `IsCoated`, `IsWired`, `VisibleLightTransmittance`, `SolarHeatGainTransmittance`, `ThermalTransmittanceSummer/Winter`); ce nu are loc acolo în `Lapin_Glazing`.

## 5. Tabele

- Tabelul de uși și ferestre: rânduri noi „Sticlă” (alcătuirea, ex. „4-16-4 low-e Ar”), „Ug / Uf / Uw”, „Greutatea sticlei”; un element combinat apare o dată, cu desenul lui întreg (`JoinerySymbols` desenează deja grila).
- Un tabel nou „Geamuri” (la comanda de sticlă): pe fiecare geam, dimensiunile de debitare (lumina + falțul), alcătuirea, bucăți, m², kg — pe tip × număr de elemente.
- Uw în etichete (`{Uw}`) și în tabelul de cantități.

## 6. Recomandare și etape

Recomandarea: **varianta C** — un singur element în model (ce cere utilizatorul și cum gândesc producătorii), IFC corect pe clase la export.

1. **Datele sticlei pe tip și pe geam** (fără schimbări de geometrie): `Glazing` pe spec și pe panou, câmpurile în configurator (secțiunea Sticlă, la panoul ales în editorul de panouri), Uf pe sistemul de profile, Uw calculat (EN ISO 10077-1) cu teste pe exemple calculate de mână (fereastra de referință 1230 × 1480 mm folosită de producători pentru Uw), tabelul, `Pset_DoorWindowGlazingType`. Folositor imediat și pentru ferestrele de azi.
2. **Canate de fereastră într-un tip de ușă** (și invers, o foaie de ușă într-un „perete vitrat”): operațiile permise pe celulă după poziție (un canat nu stă pe pardoseală decât ca fereastră franceză), cercevelele din profilul ferestrei, simbolurile din plan și fațadă (există), problemele (o foaie de ușă doar în rândul de jos).
3. **Exportul ca ansamblu**: `IfcElementAssembly` cu `IfcDoor` + `IfcWindow` + tocul comun, citirea înapoi într-un singur element, `RecognitionFidelity` pentru fișiere străine cu astfel de ansambluri (rămân referință dacă nu se potrivesc).
4. **Tabelul de geamuri** și eticheta `{Uw}`; presetări („Ușă cu lumină laterală și supralumină”, „Ușă-fereastră de balcon”, „Vitrină de magazin”).

## 7. Întrebări pentru utilizator

1. Varianta C (un element în model, ansamblu în IFC) e bună, sau preferați un `IfcDoor` simplu chiar și cu canate de fereastră (cum face Archicad)?
2. Datele sticlei: pe fiecare geam, sau ajunge pe tip (cu excepții pe geam doar pentru sticla de siguranță, ex. jos)?
3. Ug din fișa producătorului (recomandat) sau calcul aproximativ din straturi?
4. Ce valori tipice livrăm ca orientative (Ug pentru 4-16-4 aer/argon, low-e, tripan; Uf pe PVC/aluminiu/lemn; ψg aluminiu/warm edge) și din ce sursă le luați dumneavoastră?
5. Tabelul de geamuri (debitare) e necesar acum sau mai târziu?
6. O ușă-fereastră într-un perete-cortină: celula ia elementul combinat întreg, sau rămâne cum e (o ușă pe celulă)?
7. Cine e primul utilizator: arhitectul (desene, tabel) sau producătorul (debitare, greutăți)? Asta schimbă ordinea etapelor 1 și 4.
