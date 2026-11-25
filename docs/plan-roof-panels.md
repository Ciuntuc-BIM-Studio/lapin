# Panotajul învelitorii: țiglă metalică și panouri pe acoperișurile Lapin

Cerut de proprietar la 24.11.2026. Aplicația lui, **Panotaj Bilka** v1.3.3 (Python, `Panotaj Bilka/`, netrackuită, a lui), citește un acoperiș din exportul IFC4 al lui Lapin și așază panouri Bilka (Balcanic, Clasic 35, Retro Panel) pe fiecare apă, refolosește resturile de la coamele oblice în dolii, clasifică muchiile, dimensionează jgheaburile și burlanele, așază tablele plane pe coli, numără prinderile și scrie un IFC al panourilor, un deviz Excel în lei și un raport JSON v1.0 cu coduri comune de piese. Decizia lui: **un modul Lapin deschis (MIT), generic**, „Roof panel layout” (`roofpanels`), pentru orice țiglă metalică sau panou, care lucrează direct pe acoperișurile native, fără drumul prin IFC. Cele trei profile Bilka vin ca primul catalog de profile (date, nu cod), iar prețurile ca o listă de prețuri separată, **orientativă**, a modulului de costuri; niciun preț în modul. Codul aplicației e al proprietarului, deci logica ei se poate porta: e rescrisă în C#, iar comportamentul ei e specificația (testele compară cu numerele ei). Decizia 847 în `decisions-pending.md`.

## 1. Ce există în Lapin

- **Acoperișurile** (`engine/src/Modules/Architecture/Lapin.Architecture/Roofs`): un `IfcRoof` e conturul lui și regulile muchiilor (`Roof.Resolve`: streașină sau fronton, pantă, consolă); restul e derivat la fiecare cerere. `Roof.Solve` dă `RoofShape`: apele (`RoofPlane`, puncte în sens trigonometric văzute de sus, cu consola inclusă, z absolut, fața inferioară a căpriorilor), frontoanele și liniile clasificate (`LineKind`: Ridge, Hip, Valley, Eave, Verge, Break). Învelitoarea e o bandă verticală peste ape (`Roof.Band`, `StructureDepth + Covering.Thickness` măsurat pe verticală), deci fiecare apă are în planul învelitorii exact forma apei; un panou așezat pe apă se ridică pe învelitoare cu o translație. Golurile (luminatoare, lucarne) sunt `RoofOpenings.Place` cu gaura lor pe apă.
- **Ce mai e pe acoperiș**: șarpanta derivată (`RoofParts.Framing`, căpriori la interax, cosoroabe), jgheaburi și burlane (`Drainage`, `RoofParts.Gutters`), parazăpezi, desfășurata învelitorii (`RoofUnfold`: fiecare apă în adevărată mărime, cu goluri și arii), cantitățile (`Roof.Quantities`: `Qto_RoofBaseQuantities`, `Lapin_RoofCoveringQuantities`, `Lapin_RoofDrainageQuantities`, `Lapin_RoofStructureQuantities`), catalogul de învelitori (`RoofCatalog.Coverings`, cu panta minimă, iar regulile proiectului o pot schimba, `RoofRuleSection.MinPitch`).
- **Piese ale unui element**: `IPartSource` (decizia 613) dă unui extras de câmpuri piesele unui element (lemnul șarpantei, plantele), cu puncte în model pentru etichete. `IGeometryFilter` lasă un modul să schimbe geometria unui element construit (așa face `wood` cu pereții pe cadre).
- **Costurile** (`Lapin.Cost`, `docs/plan-4d5d.md`, `docs/user/cost.md`): liste de prețuri (`LapinPriceList`, rânduri cu cod, unitate, material/manoperă/utilaj/transport, resurse), articole pe tipuri și reguli pe materiale, devizul derivat din cantitățile elementelor (`Costing.Measured` citește `q.Quantities`), formularele F3, F2, C6–C9, Excel, PDF, `IfcCostSchedule`. Modulul livrează doar un șablon cu valori orientative marcate.
- **Extrase și desene**: extrasele de câmpuri, tabelele modulelor (`IScheduleSource`), desenele făcute de motor din geometria elementelor (planul, fațadele, secțiunile tipăresc ce arată vederea).
- **IFC**: exportul scrie acoperișul ca `IfcRoof` agregând învelitoarea (`IfcSlab` `ROOF`) și restul pieselor, cu `Lapin_Intent`.

## 2. Cum trece fiecare funcție a aplicației în Lapin

### 2.1 Ce se stochează și ce se derivă

Se stochează doar **cu ce e acoperit acoperișul**: `panels` pe `IfcRoof`, `{profile, color, maxLength}` (comanda `roofpanels.setRoofPanels`, un pas de anulare). Tot restul se calculează la fiecare cerere, ca șarpanta: apele, muchiile și tipurile lor, coloanele, panourile, resturile refolosite, cantitățile, accesoriile. Acoperișul se schimbă, panourile îl urmează. Un cache pe revizia modelului (`RevisionCache`, cum face `RoofTop`) ține rezultatul cât modelul nu se schimbă. Mai târziu se stochează doar excepțiile alese de utilizator (o fază a coloanelor fixată pe o apă, o apă fără panouri), niciodată panourile.

### 2.2 Apele și muchiile (făcut în S1)

`RoofFaces.Read` lucrează pe apele din `RoofShape`, nu pe fețele unui IFC: originea fiecărei ape la începutul celei mai lungi muchii de jos, u de-a lungul ei, v în sus pe pantă, codurile `A01…` în ordinea apelor (aceeași ca în aplicație). Muchiile se taie în fiecare colț care stă pe ele și se clasifică după regulile aplicației (`layout.read_roof`): liberă și orizontală, cu apa deasupra: **streașină** (`eave`); liberă, orizontală, cu apa dedesubt: **semicoamă** (`topEdge`, vârful unui acoperiș într-o apă); liberă, înclinată: **fronton** (`verge`); între două ape, una sub, alta deasupra: **rupere** concavă sau convexă (`concaveBreak`, `convexBreak`); apele se ridică din ea: **dolie** (`valley`, panourile se opresc la 65 mm de ea); altfel **coamă** orizontală (`ridge`) sau oblică (`hip`). Racordurile la pereți (`wallTop`, `wallSide`) vin din pereții nativi (S3): o muchie liberă lângă fața unui perete care urcă peste ea, și vârful unui acoperiș care intră sub alt acoperiș.

Pe planurile native topologia e cea adevărată: la o mansardă, aplicația vede cele două niveluri ca fețe care nu se ating (învelitoarea fiecăruia e ridicată cu grosimea ei pe verticală la panta ei) și găsește patru streșini și patru semicoame în plus; Lapin găsește patru ruperi convexe. Numărul panourilor iese același.

### 2.3 Panotajul (făcut în S1)

`PanelLayout` e `layout.py` + `reuse.py` rescrise, cu aceleași toleranțe: coloane de lățimea utilă, faza încercată din 15 în 15 mm, coloanele cu o fâșie de streașină sub 22 cm refuzate; începutul pe modul; o coloană dintr-o piesă sau din mai multe petrecute pe pantă; la un profil pe modul, fiecare îmbinare pe modul (lungimea minus petrecerea, multiplu de modul), niciodată o piesă mai lungă deasupra uneia mai scurte; acoperișul cu cei mai puțini milimetri de tablă, apoi cele mai puține piese. Un profil la comandă încearcă apoi mai puține lungimi diferite (cel mult 5 % material în plus, nicio piesă în plus). Retro Panel (falț): piesa cea mai lungă cât timp restul e mai lung, la fiecare a doua coloană cu 800 mm mai scurtă (îmbinările decalate), îndoirea de 25 mm la capăt. Resturile: o piesă de dolie tăiată din ce lasă o piesă de coamă oblică din panoul ei, contururile despărțite pe panou cu un joc de 4,9 mm, mutată pe panou cu module întregi; potrivirea bipartită cu cele mai lungi piese de dolie întâi; pe fiecare apă se încearcă vreo 14 faze în 2,35 m de tablă de cea mai ieftină, pentru panotajul care cumpără cel mai puțin după refolosire. Retro Panel nu refolosește resturi (falțul și capătul trebuie verificate).

### 2.4 Cantitățile (S1), accesoriile, prinderile, pluvialul, tablele plane (S3–S5)

| Aplicația | În Lapin | Când |
|---|---|---|
| `purchase_by_length`, `installed_by_length`, ariile | interogarea `roofpanels.layout`: `purchase`, `installed`, `totals` (aria apelor, acoperirea utilă, tabla cumpărată = lungimea nominală × lățimea totală, cum se cotează) | S1 |
| `panels`, `recoveries`, coduri de piese | `panels` (cod, apă, coloană, rând, lungime, cumpărat sau rest, donator, decalaj, muchii, contur în plan); piesele unui extras (`roofPanels`) | S1 |
| `edges` cu petrecerea și piesele de 2 m | `edges` (tip, ape, lungime, petrecere, piese, îmbinări), petrecerile din profil | S1 |
| accesoriile pe muchii, șuruburi de cusătură, de margine, cârlige | articole de accesorii în profil (cod, lungime utilă, petrecere, pe ce tip de muchie), numărate pe muchiile derivate | S3 |
| racordurile la pereți | muchiile libere lângă fețele verticale ale pereților nativi (aceleași toleranțe: 35 mm, 25 mm sub, 60 mm sub vârf) | S3 |
| șipci, contrașipci, folie, prinderi pe suport (cuie 4 × 100 sau șuruburi 6 × 100) | șipcile pe modulul profilului pe ape; contrașipcile din căpriorii șarpantei native (lungimea lor reală, nu o estimare la 0,6 m); folia cu suprapunerile ei | S3 |
| luminatoarele și lucarnele | goluri în ținta panotajului, cu piesele lor de racord | S3 |
| pluvialul 125/90 sau 150/100, 0,05 l/s·m², coborâri | o verificare pe jgheaburile și burlanele native (`Drainage`), cu traseul apelor fără streașină prin dolii și ruperi (`drainage_areas`); recomandarea și minimul pe fiecare jgheab | S5 |
| tablele plane pe coli de 2000 × 1250 | semifabricatele (nod de coamă, cap de dolie, închideri, colțare de jgheab, agrafe) așezate pe coli, extras și desen de debitare | S5 |
| devizul Excel în lei | liniile panourilor și accesoriilor în devizul modulului de costuri, prețurile din lista utilizatorului; formularele și Excelul existente | S4 |
| IFC-ul panourilor | exportul IFC al lui Lapin (§2.6) | S6 |
| raportul JSON v1.0 | interogarea e deja aproape același conținut, în engleză; un export JSON v1.0 compatibil cu aplicația doar dacă proprietarul îl vrea (întrebarea 4) | S6 |

### 2.5 Ce se vede (S2)

Panourile sunt geometria învelitorii, nu elemente de sine stătătoare: un `IGeometryFilter` al modulului înlocuiește, pe un acoperiș care are `panels`, partea „Covering” cu câte o parte pe panou (`panel:A01-02-01`), profilată simplificat (undele și treptele profilului din `waves` și `waveHeight`, cum face `deliver.mesh`), ridicată pe învelitoare, în materialul și culoarea alese; resturile refolosite au o culoare de evidențiere. Planul, fațadele și secțiunile le desenează motorul din aceleași părți, deci planșele arată panotajul fără nimic în plus. Partea fiecărui panou își are punctele, deci o etichetă `part` scrie codul și lungimea lui.

### 2.6 În IFC: părți ale învelitorii, nu cantități doar (S6)

Fiecare panou e un `IfcCovering` `ROOFING` agregat de `IfcRoof` în locul învelitoarei întregi (cum scrie și aplicația), cu `Lapin_RoofPanel` (cod, profil, culoare, lungimea nominală, lățimile, petrecerea, donatorul sau primitorul restului) și `Qto_CoveringBaseQuantities`, grupate pe ape (`IfcGroup`), materialul tablei pe toate. De ce elemente și nu doar cantități: un program de ofertare, de montaj sau un CDE care primește IFC-ul vede piesele cu codul lor, cum le vede devizul; de ce `IfcCovering` și nu `IfcPlate`: în IFC4 învelitoarea de acoperiș e `IfcCovering` cu `ROOFING`, `IfcPlate` e o placă de construcție. GlobalId-urile se derivă din GlobalId-ul acoperișului și codul panoului (stabile cât panotajul nu se schimbă). Importul IFC rămâne fidel: un IFC străin cu panouri le aduce ca referințe, ca acum.

### 2.7 Costurile (S4, făcută: decizia 859)

Modulul nu are prețuri. Fiecare profil și fiecare accesoriu are un **cod de articol** (`bilka-clasic-35/7016`, `ridge-large`…), iar liniile panotajului intră în deviz prin cantitățile acoperișului: un set `Lapin_RoofPanelQuantities` (tabla cumpărată pe culoare, numărul de piese pe lungime, accesoriile pe cod) adăugat acoperișului printr-un cârlig al arhitecturii (`roofs` cer cantitățile modulelor care se înscriu, cum cer geometria filtrelor), deci și extrasul cantităților, și regulile pe material, și `IfcCostSchedule` le văd. Prețurile Bilka din `catalog.json` (Dedeman, Expert Tablă, Hornbach, eAcoperiș, consultate la 24.11.2026) devin o **listă de prețuri orientativă** pe care utilizatorul o ia ca șablon (Construction → Price lists → din șablon), cu data și sursele pe fiecare rând și marcată orientativă, niciodată încărcată singură în proiect.

## 3. Catalogul de profile

Un profil e date (`engine/src/Modules/Roofing/Lapin.RoofPanels/profiles.json`, citit de `PanelProfile.Read`), în milimetri, cum le publică producătorii:

```json
{
  "id": "bilka-clasic-35", "name": "Bilka Clasic 35", "maker": "Bilka", "kind": "tile",
  "grossWidth": 1200, "usefulWidth": 1120, "slopeLap": 150, "module": 350, "joints": "module",
  "order": { "min": 500, "start": 500, "step": 350, "max": 7000 },
  "colors": [{ "code": "7016", "name": "RAL 7016 matt anthracite" }, { "code": "8017", "name": "RAL 8017 matt brown" }],
  "minPitch": 8, "minPitchHipValley": 14, "reuseOffcuts": true, "valleyGap": 65,
  "waves": 6, "waveHeight": 20, "accessoryLength": 2000,
  "accessoryLaps": { "eave": 75, "ridge": 100, "hip": 100, "valley": 150, "verge": 100, "topEdge": 100, "...": 100 },
  "notes": "…", "source": "https://www.bilka.ro/…"
}
```

- `lengths` (stoc: `[1180, 2230, 2930]`) sau `order` (la comandă: cea mai mică, apoi din `start` din `step` în `step` sub cea mai mare, și cea mai mare: Clasic 35 dă 500, 850 … 6800, 7000; Retro Panel 470, 500, 600 … 7100, 7130).
- `joints`: `module` (țiglă: îmbinări pe modul) sau `staggered` (falț: îmbinări decalate cu `stagger`); `endFold`: ce pierde capătul îndoit; `maxLengths`: lungimile maxime dintre care alege utilizatorul (Retro: 4000 sau 7130).
- `minPitch` și `minPitchHipValley` (aplicația: 8°, 14° la apele cu coame oblice sau dolii pentru țiglă): sub ele panotajul se face, cu un avertisment (aplicația refuza).
- În S3 profilul a primit și accesoriile lui (`accessories`: cod, nume, unitate, regula de numărare, pe ce muchii și cu ce pondere), suportul (`support`: scânduri 150 mm cu gol 50 la Retro sau șipcile ghidului anterior, șipci la modul la țiglă), prinderile tablei (`fixings`) și coamele (`ridges`: mare, mică); folia, banda ei, contrașipcile și prinderile lor sunt comune (`common`).
- Un profil al utilizatorului are același format: interogarea primește deja un profil ca obiect (testul `A_profile_of_ones_own…`); în S7 profilele proiectului (o entitate `LapinPanelProfile`, un fișier de bibliotecă al firmei) stau lângă cele livrate, se editează într-un dialog, iar profilul livrat rămâne implicitul, cu aceleași rezultate.

## 4. Interfața (S2)

- **Proprietățile acoperișului** primesc secțiunea **Panels**: profilul (din catalog și ale proiectului; „none”), culoarea profilului, cea mai mare lungime la Retro, și un rezumat: piesele de cumpărat pe lungime, resturile refolosite, tabla cumpărată (m²), avertismentele. O schimbare e o comandă, deci un pas de anulare.
- **Roof → Panel layout…** (în tabul Architecture, grupul acoperișului; modulul UI `ui/src/modules/roofpanels`) deschide panotajul: apele desfășurate cu panourile (culoarea pe lungime, resturile refolosite în portocaliu, codul și lungimea la trecerea cursorului), muchiile colorate pe tip, tabelele „de cumpărat”, „muchii” și „resturi”, și butonul **Use this profile**. Profilul se poate compara înainte de a-l alege (interogarea primește un profil fără să schimbe acoperișul).
- **3D, plan, fațade**: panourile ca geometria învelitorii (§2.5).
- **Extrase**: „Roof panels” ca extras de câmpuri livrat (piesele `roofPanels`), cu totalurile pe lungime.
- **Devizul**: liniile panotajului sub capitolul **Roof** (S4).
- Șirurile noi în toate limbile din `ui/src/i18n`; pagina din ghid (`docs/user/roof-panels.md` și `docs/user/ro/`) cu scena ei din `scripts/user-docs/scenes.mjs`.

## 5. Etape

| Etapa | Ce aduce | Testele |
|---|---|---|
| **S1** (făcută) | Modulul `roofpanels` (`engine/src/Modules/Roofing/Lapin.RoofPanels`, `requires: core, architecture`), în toate edițiile cu arhitectură. Catalogul cu cele trei profile Bilka (date). Apele și muchiile clasificate pe planurile native; panotajul pe modul, la comandă și decalat (Retro, și cu 4000 mm); resturile refolosite; cantitățile (de cumpărat și montate pe lungime, arii, tabla cumpărată), piesele de accesorii pe muchii. `roofpanels.setRoofPanels`, `roofpanels.layout`, catalogul `roofpanels.profiles`, piesele `roofPanels` pentru extrase. | `RoofPanelTests` (27): în două ape, în patru ape, L în două ape, L în patru ape și mansardă, câte trei profile (și Retro la 4000 mm), cu numerele aplicației Python rulate pe IFC-ul exportat de Lapin din același acoperiș: piesele de cumpărat și montate pe lungime, resturile și perechile donator–primitor cu decalajul, ariile, muchiile pe tip și lungime; profilul ca date; un profil al utilizatorului; comanda, anularea, avertismentul de pantă. |
| **S2** (făcută, decizia 855) | Ce se vede: panourile ca geometria învelitorii (3D profilat, plan, fațade, secțiuni), resturile evidențiate; secțiunea Panels în proprietăți, dialogul Panel layout, extrasul livrat; ghidul și scena; i18n. | Geometria: o parte pe panou, aria ei, culoarea restului; planul are conturul panourilor; testele UI ale secțiunii; scena ghidului. Făcute: `RoofPanelGeometryTests` (tabla închisă, volumul plăcilor, culorile, conturul în plan, IFC neschimbat, acoperișul cu straturi, acoperișul tăiat de altul, extrasul), `roofpanels.test.ts`, scena `roof-panels`. Abatere: în desene panoul e o placă de grosimea învelitorii peste ce acoperă, profilul doar în 3D. |
| **S3** (făcută, decizia 856) | Accesoriile din profil (coame mare și mică, dolii, fronton, streașină, racorduri), racordurile la pereții nativi, prinderile (cuie sau șuruburi, de cusătură, de margine), șipcile, contrașipcile din șarpanta nativă, folia; luminatoarele și lucarnele tăiate din panotaj. | Numerele aplicației pe aceleași acoperișuri (foaia Muchii, prinderile, șipcile), un acoperiș lipit de un perete (racordurile). Făcute: `RoofPanelTakeoffTests` (devizul aplicației pe două ape, L în patru ape, două ape lângă un perete, șopron sub un perete, câte trei profile, Retro pe șipci, Clasic cu coame mici și șuruburi; statisticile; racordurile; șopronul sub alt acoperiș; fereastra și lucarna decupate), `roofpanels.test.ts`, scena `roof-panels`. Abateri: șipcile doar cantități (nu 3D); golurile, pe care aplicația le refuză, sunt tăiate pe porțiuni de coloană (sub, lângă, deasupra golului). |
| **S4** (făcută, decizia 859) | Costurile: setul `Lapin_RoofPanelQuantities` prin cârligul acoperișului, codurile de articol ale profilelor și accesoriilor, lista de prețuri Bilka orientativă ca șablon, liniile în deviz (capitolul Roof), alegerea culorii schimbă articolul. | Devizul cu lista orientativă dă subtotalul aplicației (`priced_subtotal_ron`) pe același acoperiș și aceleași opțiuni. Făcute: `RoofPanelCostTests` (21: devizul aplicației rând cu rând pe 16 cazuri — două ape, L în patru ape cu dolie, două ape lângă un perete, șopron sub un perete, câte trei profile, Clasic în 8017, Retro pe șipci, coame mici cu șuruburi —, șablonul adus doar la cerere, fără listă: cantități fără prețuri, fără modulul de costuri, lista proprie întâi), `roofpanels.test.ts`, `estimate.test.ts`, scena `roof-panels`. Abateri: cârligul e al platformei (`IQuantityFilter`, ca `IGeometryFilter`), nu al arhitecturii; subtotalul comparat e al aplicației fără tabla plană și pluvial (S5). |
| **S5** | Pluvialul (sistemul recomandat, coborârile minime pe jgheaburile native, apele fără streașină prin dolii și ruperi) și tablele plane pe coli (extras și desen de debitare). | `pluvial.sizing` și `flat_stock.nest` pe aceleași date: sistemul, minimul de coborâri, numărul de coli. |
| **S6** | IFC: panourile ca `IfcCovering` `ROOFING` agregate de `IfcRoof`, cu pset-ul și grupurile pe ape, GlobalId-uri stabile; exportul JSON compatibil cu v1.0 dacă e cerut. | Exportul: numărul de `IfcCovering`, pset-urile, GlobalId-urile aceleași la un al doilea export, aria. |
| **S7** | Profilele proiectului și ale firmei (entitate, fișier de bibliotecă, dialog), excepțiile pe ape (faza fixată, o apă fără panouri), video. | Un profil al proiectului folosit de un acoperiș; un profil livrat schimbat de utilizator nu schimbă celelalte proiecte. |

## 6. Întrebări pentru proprietar

**Răspuns (24.11.2026):** proprietarul a ales variantele implicite la toate patru: panourile înlocuiesc placa învelitorii în IFC când acoperișul are un profil; lista de prețuri Bilka intră în repo ca șablon orientativ, adus doar la cerere; petrecerea Balcanic rămâne 130 mm; exportul JSON v1.0 nu mai e necesar.

1. **În IFC, panourile înlocuiesc învelitoarea?** Implicit: da, când acoperișul are un profil, fiecare panou e un `IfcCovering` `ROOFING` în locul plăcii învelitoarei (cum scrie aplicația); fără profil, ca acum.
2. **Lista de prețuri Bilka (Dedeman etc., 24.11.2026) intră în repo-ul deschis?** Implicit: da, ca șablon orientativ, cu data și sursa pe fiecare rând, adus în proiect doar la cerere; numele de comercianți rămân în coloana sursei.
3. **Petrecerea Balcanic: 130 mm (aplicația) sau 150 mm (ghidul general Bilka)?** Implicit: 130, cum calculează aplicația, cu nota din profil; utilizatorul o schimbă în profilul lui.
4. **Exportul JSON v1.0 compatibil cu aplicația Python** îți mai trebuie după ce modulul face tot? Implicit: nu, interogarea și extrasele îl înlocuiesc; dacă da, vine în S6.
