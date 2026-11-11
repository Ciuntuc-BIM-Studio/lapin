# Încăperi și scheme de culori

Cerut (27.10.2026): „un configurator rapid, cu interfață, pentru colorat și texturat/hașurat încăperile — util pentru planuri". Decizia #150.

## Încăperea (`IfcSpace`)

O încăpere e un element al modulului `architecture`, clasa `IfcSpace`, conținută în etajul ei (în model, ca orice element; în IFC agregată, vezi mai jos).

| Atribut | Ce e |
|---|---|
| numele entității | numele încăperii („Living room”, „Bedroom”) |
| `number` | numărul; implicit următorul liber pe etaj: poziția etajului între etaje, apoi două cifre (`101`, `102`… pe al doilea etaj de jos; `001` pe fundații) |
| `function`, `department`, `finishFloor` | text liber; funcțiunile oferite întâi: Living, Bedroom, Kitchen, Dining, Bath, WC, Circulation, Storage, Technical, Office, Balcony, Garage |
| `height` | înălțimea; lipsă = înălțimea etajului |
| `auto`, `seed` | pusă printr-un clic: punctul ei (`seed`), conturul se găsește în jurul lui |
| `contour`, `islands` | conturul desenat, sau ultimul găsit (cu insulele: stâlpii din ea) |
| `stampAt` | unde a fost mutată eticheta; lipsă = calculată |
| `fillColor`, `fillHatch`, `hatchScale`, `hatchColor` | culoarea și hașura proprii, peste cele ale schemei |

### Cum se pune

- **Clic în interior** (Architecture → Room, `RM`): conturul e regiunea liberă cea mai mică din jurul punctului, între **pereți, pereți cortină și stâlpi**, tăiați la 1,20 m peste cota etajului (`RoomBounds`). Se folosesc **anvelopele** lor (corpul din atribute, fără goluri), deci o ușă nu lasă încăperea să „curgă” afară. Stâlpii din interior devin insule (aria lor se scade). Un punct într-un perete, un punct pe care nu-l închide nimic și un punct dintr-o încăpere care există deja sunt refuzate, cu motivul.
- **Contur desenat** (Room by outline): poligonul se păstrează așa cum e (o terasă, un spațiu deschis).
- Algoritmul (`FreeSpace`, în `Lapin.Drawing/Hatch`): laturile ariilor se taie unde se intersectează, se ating (T) sau se suprapun; rămân bucățile cu material pe o singură parte, orientate cu spațiul liber la stânga, înlănțuite în bucle (cea mai strânsă întoarcere la stânga în fiecare nod). Buclele în sens trigonometric închid spațiu liber, cele în sens orar ocolesc material; regiunea unui punct e cea mai mică buclă trigonometrică ce-l conține, cu buclele orare a căror parte liberă îi aparține ca insule.

### Urmărește pereții

O încăpere pusă printr-un clic își recalculează conturul **la fiecare construire a geometriei** (geometria e în cache-ul sesiunii și se reface când se schimbă un perete, un stâlp sau un perete cortină: `TrackingView` vede ce a citit). Spațiul liber al unui etaj se rezolvă o dată pe revizie a modelului și e împărțit de toate încăperile lui; una care îl ia din cache primește aceleași citiri, deci se reface la fel. Dacă punctul nu mai e închis (un perete șters), încăperea își păstrează conturul stocat și spune asta (diagnostic, etichetă roșie, „no longer enclosed” în proprietăți). **Update rooms** (sau **Update outline** pe o încăpere) scrie conturul găsit acum în `contour`, ca să-l păstreze dacă pereții dispar. Mutarea, copierea, rotirea și oglindirea duc cu ele punctul, conturul, insulele și eticheta.

### Cantități

`Qto_SpaceBaseQuantities`: `NetFloorArea` (fără insule), `GrossFloorArea`, `GrossPerimeter` (conturul), `NetPerimeter` (cu insulele), `Height`, `NetVolume`, `GrossVolume`. Apar în panoul de proprietăți și în extrasul de încăperi (Schedules → Rooms: etaj, număr, nume, funcțiune, departament, arie, perimetru, înălțime, volum, totalul ariei).

### În vederi

- **Planuri:** încăperile se desenează **doar** prin umplerea lor (schema vederii) și eticheta lor; corpul nu se secționează niciodată (`Views.Gather` le lasă afară). Umplerea și hașura sunt marcaje „under” (`ViewMark.Under`): creioanele lor vin **înaintea** creioanelor modelului, deci pereții, mobilierul și simbolurile ușilor stau deasupra; eticheta vine după. Același desen merge pe planșe, în PDF și în DXF (straturile `ROOM-FILL`, `ROOM-HATCH`).
- **Eticheta**: tipul de etichetă cu forma `room` din Drafting standards → Tags (implicit „Room stamp”: `{name}|{number}|{area}`); câmpurile: atributele încăperii, plus `{area}` (netă), `{grossArea}`, `{perimeter}`, `{height}`, `{volume}`, în unitățile proiectului (m² cu două zecimale, ft² cu una în imperial). Un clic pe etichetă selectează încăperea; la fel un clic în interiorul ei (după pereți, înaintea plăcii). Eticheta are un mâner (grip) de mutat; încăperea pusă prin clic are un mâner pe punctul ei (trasă în altă încăpere, o găsește pe aceea); cea desenată are mânerele conturului.
- **Hașura finisajului (decizia 299):** o încăpere cu finisaj (decizia 265) are, pe plan, pardoseala hașurată ca finisajul ei (într-un plan văzut de jos, `look: up`, tavanul), tot un marcaj „under” desenat de `Rooms.Marks` după umplerea schemei: deci umplerea întâi, hașura peste ea (doar linii), amândouă sub modelul secționat; layerul `FINISH-HATCH`, culoarea implicită `#7a7a7a`, creionul de hașură. Regiunile (`RoomFinishes.PlanRegions`): conturul cu insulele și golurile plăcii (pardoseala) sau ale plăcii de deasupra (tavanul) scoase, plus pragurile prin uși când finisajul le are. Modelul (`RoomFinishes.HatchOf`): `floorHatch` / `ceilingHatch` pe specificația tipului (`{mode: material | own | none, pattern?, scale, angle, color?}`; lipsă = `material`); `material` ia, de la fața vizibilă spre interior (pardoseala: stratul de sus; tavanul: cel de jos), primul strat al cărui material are hașură proprie pentru fațade (`elevation.hatch`, suprafața văzută) sau una sugerată de nume (gresie/faianță/piatră → TILES, parchet/laminat/lemn → WOOD, mochetă → CROSS, vinil/linoleum → HORIZONTAL, epoxy/șapă/beton → CONCRETE), altfel LINES45; scara și unghiul tipului se înmulțesc / se adaugă. Se oprește: pe încăpere (`finishHatch: false`, „Show floor finish hatch” în proprietăți), pe stilul vederii (`finishHatch`, pornit în Architectural, oprit în Structural, MEP, Coordination; vezi `docs/view-styles.md`), când elementul finisajului e ascuns în vedere și pe planurile de situație. Teste: `RoomFinishTests` (cele trei de la sfârșit).
- **3D:** categoria „spaces” e ascunsă implicit în 3D (și în exportul 3D); Visibility/Graphics o poate arăta. Exportul 3D al modelului întreg le lasă afară.

## Uși și ferestre din încăpere

Cerut de utilizator (09.11.2026), pentru apartamentele-tip (`docs/plan-units.md`), dar valabil la orice încăpere. Opțiune în proprietățile încăperii, **oprită implicit**; așezarea pe pereți merge în continuare.

- `roomOpenings: {on, items: [{key, kind, type, at, sill?, fill}]}` pe încăpere (`RoomOpenings.cs`). O intrare face o ușă sau o fereastră obișnuită în peretele de pe acea latură, marcată cu `roomOpening` = id-ul încăperii și regăsită după `key`.
- O intrare ține **un punct** pe latură, nu numărul ei: un contur găsit din pereți poate începe oriunde, iar o oglindire îl inversează, pe când punctul se mută cu încăperea (`Placement.Carry`).
- Comenzile: `architecture.setRoomOpenings {id, on?, items?}` (o intrare spune `side` și `offset` față de mijlocul laturii, sau `at`), un singur pas de undo; `architecture.roomOpenings {id}` dă laturile, pereții lor și intrările cum stau acum, inclusiv o ușă mutată de mână.
- Oprirea scoate ce a făcut încăperea și păstrează lista, cu pozițiile de atunci; repornirea le pune la loc. O ușă pusă pe perete cu unealta obișnuită nu e atinsă.
- O ușă se deschide spre încăpere la creare (`flipSide` când încăperea e la dreapta peretelui); o întoarcere făcută apoi de mână rămâne.
- O copie a încăperii, împreună cu pereții ei, preia ușa copiată la pornire, fără să o dubleze (aceeași clasă, sub 5 mm distanță).
- Doar pereți drepți în această versiune; o latură fără perete drept refuză, cu mesaj. Ștergerea încăperii lasă ușile pe loc.
- Într-o instanță de componentă (`docs/plan-units.md`, Limita) lista se aplică din nou unde stă instanța, chiar dacă peretele nu e membru (`RoomOpenings.Settle`): fără refuz, cu `noWall` la intrarea care nu are perete sau loc. O ușă se regăsește după `roomOpeningKey`, iar intrarea ține și direcțiile `hand` și `face`, ca o oglindire să inverseze mâna. Ce se face de mână pe o astfel de ușă se scrie pe intrare și ajunge la toate instanțele (`RoomOpenings.Edited`); o copie a ei, făcută cu peretele, nu mai e a camerei până când camera o preia la loc.

## Schemele de culori

Proiectul le ține în `colorSchemes`; până se schimbă una, sunt cele implicite: **By function** (culori pastelate pentru funcțiunile de mai sus) și **By department** (goală). O schemă: `{id, name, by, entries: [{value, color, hatch, hatchScale, hatchColor}]}`; `by` e câmpul: `function`, `name`, `department`, `number`, `finishFloor` (sau alt atribut). O vedere de plan numește schema ei în `colorScheme` (implicit niciuna: planul rămâne curat). Culoarea sau hașura proprie a unei încăperi bate schema (doar într-o vedere care arată o schemă).

Comenzile (`architecture.*`): `createColorScheme {name?, by?, from?}` (intrările se completează din valorile încăperilor), `updateColorScheme {id, name?, by?, entries?, fill?}`, `setColorSchemeEntry {id, value, color?, hatch?, hatchScale?, hatchColor?, remove?}` (null șterge câmpul), `fillColorScheme {id}` (valorile fără intrare primesc culori din paletă, intrările fără aspect o culoare), `deleteColorScheme {id}`, `viewColorScheme {viewId, schemeId|null}`, `addColorLegend {viewId, x, y, schemeId?, showArea?}`; interogarea `colorSchemes {viewId?}` dă schemele, câmpurile, valorile încăperilor (număr, arie), paleta și schema vederii.

Paleta: 16 culori pastelate `#rrggbb` (PDF și DXF nu au transparență), care se citesc sub linii negre și se tipăresc bine.

### Configuratorul rapid

**View → Room colours** (și butonul din proprietățile unei vederi de plan și ale unei încăperi): alegi sau faci o schemă, „Colour by”, ce arată planul din față, apoi tabelul: fiecare valoare cu numărul de încăperi și aria, culoarea, hașura (din biblioteca de hașuri și modelele proiectului), scara și culoarea hașurii, cu o mostră. „Fill missing colours” completează tot; „Place a legend” pune legenda cu un clic.

### Legenda

**Annotate → Colour legend**: un `IfcAnnotation` de tip `LEGEND` pe vedere (`at`, `scheme?`, `showArea`), desenat de modulul arhitectură: numele schemei, apoi pentru fiecare intrare folosită de încăperile vederii (în ordinea schemei) o mostră (umplere și hașură) și valoarea, cu aria totală dacă se cere. Urmează schimbările schemei; se mută cu Move.

## Rotunjirea ariilor (raportul #76, decizia 471)

Standardele proiectului țin `areaRounding`: 0,1, 0,5 sau 1 (m², ft² într-un proiect imperial), lipsă: aria măsurată, la sutime. `Lapin.Drawing/AreaRounding` e singura regulă: rotunjire la pasul cel mai apropiat, jumătatea în sus (aria mai întâi la 0,0001 m², ca zgomotul coordonatelor să nu treacă 12,25 sub jumătate), scrisă cu o zecimală la 0,1 și 0,5 (12,0 / 12,5) și fără la 1. O folosesc eticheta încăperii (`{area}`, `{grossArea}`), legenda și interogarea schemelor, extrasul de încăperi, câmpurile documentelor (`DocFormat.AreaStep`: aria construită, desfășurată, utilă, ariile pe etaj și pe încăpere), iar în interfață ariile de pardoseală din Cantități (`areaText`, `unitsStore.ts`). Fiecare încăpere se rotunjește separat și un total e suma rândurilor (SFS 5139 rotunjește fiecare încăpere), deci coloana se adună; volumul rămâne aria exactă ori înălțimea. Doar prezentare: modelul, cantitățile, IFC, costurile și planificarea păstrează aria exactă; un extras exportat (CSV, XLSX) e ce arată tabelul. Cantitățile de material (fețe de perete, plăci, cofraje, învelitori) și parametrii de tip arie scriși de utilizator rămân cum erau.

## IFC

- **Export:** `IFCSPACE` agregată în etaj cu `IFCRELAGGREGATES` (nu conținută); corpul: `IFCEXTRUDEDAREASOLID` al conturului (`IFCARBITRARYCLOSEDPROFILEDEF`, sau `…WITHVOIDS` cu insulele), cât înălțimea; **Name = numărul, LongName = numele** (cum scriu Revit și ArchiCAD), `ObjectType` = funcțiunea, `PredefinedType` SPACE, `CompositionType` ELEMENT; `Pset_SpaceCommon` (`Reference` = numărul, `IsExternal` = false), `Qto_SpaceBaseQuantities`, `Lapin_Intent` cu toate atributele.
- **Import:** o încăpere Lapin revine din intenție (numele din LongName), pe etajul ei. Un `IfcSpace` străin (Revit, ArchiCAD, Bonsai…) devine încăpere (cu conturul păstrat, `auto: false`), **pe etajul în care e agregat** (`IFCRELAGGREGATES`) sau conținut:
  - conturul: baza celei mai mari prisme verticale (golurile ei = insulele); altfel reprezentarea **`FootPrint`** (`IfcGeometricCurveSet`, curbele închise, cea mai mare = conturul, cele din ea = insulele); altfel conturul triunghiurilor de la cota cea mai joasă a corpului (B-rep, plasă; și triunghiurile subțiri ale punctelor aproape coliniare, ca laturile lor să se împerecheze);
  - înălțimea din prismă (sau cutia corpului) și **`baseOffset`** = baza corpului − cota etajului, când trece de 1 mm (o galerie, o pardoseală înălțată); corpul încăperii, exportul IFC (plasarea) și panoul de proprietăți („Base offset”) îl folosesc;
  - numele din LongName, numărul din Name; doar Name, sau Name = LongName (cum scrie ifc-lite): numele, iar numărul din `Pset_SpaceCommon.Reference` (sau o proprietate `Number`), altfel următorul liber pe etaj; un text-substituent ca „&lt;Raumnummer&gt;” al ArchiCAD nu e număr;
  - funcțiunea din ObjectType, altfel `Pset_SpaceCommon.Category`, `Pset_SpaceOccupancyRequirements.OccupancyType`, o proprietate `Occupancy` sau `Function`; departamentul din orice proprietate `Department` (Revit: `PSet_Revit_Identity Data`); finisajul pardoselii din `FloorCovering` (`Pset_SpaceCoveringRequirements`, `Pset_SpaceCommon`) sau `Floor Finish`; deci schemele de culori după funcțiune și departament merg direct;
  - toate seturile de proprietăți și cantități ale spațiului rămân în `imported.properties` (set → proprietate → text) și apar, needitabile, în panoul de proprietăți la **IFC properties** (pentru orice element importat, recunoscut sau referință), cu un filtru peste 20;
  - un spațiu fără contur citibil (fără formă, sau o formă din care nu iese niciunul) intră totuși, cu proprietățile lui, ca **încăpere fără contur**: nu se desenează, apare în extras fără arie, iar în proprietăți „Find by a click” / „Draw outline” îi dau unul (`architecture.updateRoom` pe ea, prin uneltele Room și Room by outline); importul o spune într-o notă;
  - nota importului: câte încăperi au venit și unde se văd (planurile etajelor, extrasul Rooms; 3D-ul le ascunde).

  Măsurat (28.10.2026, raportul #27): pe fișierele reale din `~/Documents/GitHub` cu `IfcSpace` (Bloc-P-4 90, BURKWIL ArchiCAD IFC2X3 255, Fertighaus2 Bonsai 17, Fertighaus ArchiCAD 28 15, Casa 3, SketchUp 2), toate 382 sunt încăperi cu contur, pe etajul lor; înainte, 377 cu contur (5 B-rep-uri BURKWIL fără), iar cele agregate (nu conținute) cădeau **toate pe primul etaj** (121 din 382 pe etajul lor): `IfcImport.Apply` căuta spațiile din agregare cu `f.Get(int)`, care nu găsește nimic decât după o `StepRef`. Modul „reference” le lasă afară, ca înainte.

## Ce urmează

- Linii de separare a încăperilor (Revit: room separation lines) — azi: conturul desenat.
- Scheme și pe secțiuni (umplerea încăperilor tăiate).
- Insulele unei încăperi desenate (azi doar cele găsite).
- Gruparea pe departamente ca `IfcZone` în IFC.
