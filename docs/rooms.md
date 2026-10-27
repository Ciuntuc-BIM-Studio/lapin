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
- **3D:** categoria „spaces” e ascunsă implicit în 3D (și în exportul 3D); Visibility/Graphics o poate arăta. Exportul 3D al modelului întreg le lasă afară.

## Schemele de culori

Proiectul le ține în `colorSchemes`; până se schimbă una, sunt cele implicite: **By function** (culori pastelate pentru funcțiunile de mai sus) și **By department** (goală). O schemă: `{id, name, by, entries: [{value, color, hatch, hatchScale, hatchColor}]}`; `by` e câmpul: `function`, `name`, `department`, `number`, `finishFloor` (sau alt atribut). O vedere de plan numește schema ei în `colorScheme` (implicit niciuna: planul rămâne curat). Culoarea sau hașura proprie a unei încăperi bate schema (doar într-o vedere care arată o schemă).

Comenzile (`architecture.*`): `createColorScheme {name?, by?, from?}` (intrările se completează din valorile încăperilor), `updateColorScheme {id, name?, by?, entries?, fill?}`, `setColorSchemeEntry {id, value, color?, hatch?, hatchScale?, hatchColor?, remove?}` (null șterge câmpul), `fillColorScheme {id}` (valorile fără intrare primesc culori din paletă, intrările fără aspect o culoare), `deleteColorScheme {id}`, `viewColorScheme {viewId, schemeId|null}`, `addColorLegend {viewId, x, y, schemeId?, showArea?}`; interogarea `colorSchemes {viewId?}` dă schemele, câmpurile, valorile încăperilor (număr, arie), paleta și schema vederii.

Paleta: 16 culori pastelate `#rrggbb` (PDF și DXF nu au transparență), care se citesc sub linii negre și se tipăresc bine.

### Configuratorul rapid

**View → Room colours** (și butonul din proprietățile unei vederi de plan și ale unei încăperi): alegi sau faci o schemă, „Colour by”, ce arată planul din față, apoi tabelul: fiecare valoare cu numărul de încăperi și aria, culoarea, hașura (din biblioteca de hașuri și modelele proiectului), scara și culoarea hașurii, cu o mostră. „Fill missing colours” completează tot; „Place a legend” pune legenda cu un clic.

### Legenda

**Annotate → Colour legend**: un `IfcAnnotation` de tip `LEGEND` pe vedere (`at`, `scheme?`, `showArea`), desenat de modulul arhitectură: numele schemei, apoi pentru fiecare intrare folosită de încăperile vederii (în ordinea schemei) o mostră (umplere și hașură) și valoarea, cu aria totală dacă se cere. Urmează schimbările schemei; se mută cu Move.

## IFC

- **Export:** `IFCSPACE` agregată în etaj cu `IFCRELAGGREGATES` (nu conținută); corpul: `IFCEXTRUDEDAREASOLID` al conturului (`IFCARBITRARYCLOSEDPROFILEDEF`, sau `…WITHVOIDS` cu insulele), cât înălțimea; **Name = numărul, LongName = numele** (cum scriu Revit și ArchiCAD), `ObjectType` = funcțiunea, `PredefinedType` SPACE, `CompositionType` ELEMENT; `Pset_SpaceCommon` (`Reference` = numărul, `IsExternal` = false), `Qto_SpaceBaseQuantities`, `Lapin_Intent` cu toate atributele.
- **Import:** o încăpere Lapin revine din intenție (numele din LongName). Un `IfcSpace` străin devine încăpere (cu conturul păstrat): conturul din baza celei mai mari prisme verticale (golurile ei = insulele), altfel din conturul fețelor de jos ale corpului; numele din LongName, numărul din Name (doar Name: numele), funcțiunea din ObjectType, înălțimea din prismă. Modul „reference” le lasă afară, ca înainte.

## Ce urmează

- Linii de separare a încăperilor (Revit: room separation lines) — azi: conturul desenat.
- Scheme și pe secțiuni (umplerea încăperilor tăiate).
- Insulele unei încăperi desenate (azi doar cele găsite).
- Gruparea pe departamente ca `IfcZone` în IFC.
