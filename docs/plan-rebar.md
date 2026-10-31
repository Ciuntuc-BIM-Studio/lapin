# Armarea în Lapin: Rebar 2D (liber) și Rebar BIM (premium)

Sursa: *Configurator Armare* (`~/Documents/GitHub/Plugin Rhino/ConfiguratorArmare`, monorepo pnpm, pachetul `nucleu` fără framework și aplicația `web` pe Konva), analizat la 31.10.2026. Deciziile 209–212 în `decisions-pending.md`.

## Ce reținem din configurator

- **Blocul dinamic** (`DefinitieForma`): parametri → vârfuri nominale → ciocuri (intrinseci + ale utilizatorului) → racordare la raza de îndoire → segmente (linii și arce). Coordonata unui grip e egală cu valoarea parametrului pe axa lui, deci o singură funcție inversă acoperă aproape toate formele.
- **Două geometrii**: cea *nominală* (vârfurile la intersecția axelor; cotele și extrasul) și cea *reală* (racordată; desenul). Lungimea din extras = suma segmentelor nominale + ciocurile; un cioc de 180° adaugă π·r. Aceeași regulă în Lapin (practica românească de extras), cu `trueLength` (lungimea desfășurată pe axă, cu arce) raportată alături.
- **Raza de îndoire** SR EN 1992-1-1 tab. 8.1N: dorn 4Ø (Ø ≤ 16), 7Ø (Ø > 16); raza pe axă = jumătate. Ciocuri: 12Ø la ≤ 90°, 10Ø la ≤ 135°, 5Ø la 180°, minimum 50 mm (practică, editabile).
- **Vederi** ale aceleiași bare: frontală, de sus (o linie de lungimea *a*), laterală (o linie de lungimea *b*), secțiune (un punct de Ø). O copie în altă vedere nu intră în extras (`excludeSchedule`), dar ține minte originalul.
- **Distribuția pe un traseu** (array-path): `n = max(2, ceil(L/pas + 1))`, pasul efectiv `L/(n−1)`; zone (1/3–1/3–1/3 seismic) fiecare cu `ceil(lz/pas)` pași, ultima zonă întinsă până la capăt; eticheta „5Ø8/100”, pasul afișat ca interval „100-200” când diferă.
- **Extrasul**: rânduri pe marcă (numărul însumat; conflict dacă aceeași marcă are alt Ø sau altă lungime), masa π d²/4 · 7850, totaluri pe Ø; simbolul formei cu lungimile în cm.
- **Etichete** din jetoane: `n Ød/pas L=… clasă`, balon cu marca și linie de indicație.
- Culori pe Ø (6 #7c3aed … 32 #78350f), grosimea barei desenate = Ø real.
- Straturi DXF: `Bare_Longitudinale`, `Etrieri`, `Cofraj`, `Cote`, `Adnotatii`, `Axe_Structurale`.

**Slăbiciuni corectate**: etrierul își pierdea parametrii după creare (în Lapin parametrii rămân sursa; vârfurile libere doar la polilinie); punctele de secțiune intrau în extras cu 1000 mm (în Lapin un punct de secțiune e o *vedere* a unei bare, nu o bară); setările de desen erau pe utilizator (în Lapin, pe proiect: `rebarSettings`); cotele în DXF ca linii și texte (în Lapin ies prin pens, ca orice adnotare, și pe planșe/PDF).

## Arhitectura

### Modulul `rebar` (liber, în acest depozit, `engine/src/Modules/Structure/Lapin.Rebar`, UI `ui/src/modules/rebar`)

`requires: core, structure, drafting, architecture` (extrasul e un `LapinSchedule`, prin `IScheduleSource`). Tabul **Rebar 2D** (`order 35`, după Structure).

Entități (clase proprii, `Kind: annotation`, pe o vedere prin `viewId`, în coordonatele ei, metri):

- `LapinRebar` — o bară desenată: `shape` (straight, L, U, stirrup, polyline, tie), `params` (mm: a, b, length, height…), `vertices` (polilinia), `at`, `rotation`, `mirrorX/Y`, `hooks {start, end: {angle, length, flip}}`, `diameter`, `steel`, `mark`, `count`, `view` (front, top, side, section), `excludeSchedule`, `original`, `label {at, leader}`, `layer`, `array {nx, dx, ny, dy}`. Exportă `IfcReinforcingBar`? Nu: un desen 2D nu e un corp; rămâne adnotare (ies pe planșe, PDF, DXF). Barele 3D sunt ale premiumului.
- `LapinRebarRun` — o distribuție pe un traseu: `start`, `end`, (`rise`), `shape` + `params` ale barei repetate, `zones [{length, step}]` sau `step`, `cover`, `orientation`, `mark`, `diameter`, `count` pe poziție, eticheta; desenează pozițiile (toate, prima/mijlocul/ultima, sau una), linia de cotă cu textul „5Ø8/100”.

Motorul (`RebarShapes`): catalogul formelor cu parametrii și gripurile lor; `Nominal(spec)` (vârfuri + ciocuri), `Segments(spec)` (racordate), `Length(spec)` (nominală), `TrueLength`, `MassPerMetre(d)`, `BendRadius(d)`, `HookLength(d, angle)`; `Runs.Positions(length, step, zones)`. Desenul (`RebarMarks : IViewAnnotations`): segmentele și arcele pe stratul `Bare_Longitudinale` / `Etrieri`, grosimea Ø·(1000/scară) mm hârtie (minimum 0,35 mm), culoarea pe Ø (opțional, din setări), balonul mărcii (Ø 7,5 mm hârtie) cu textul etichetei, cotele segmentelor (opțional). Snaps: vârfurile și mijloacele. Extrasul (`RebarSchedule : IScheduleSource`, kind `rebar`): Marcă, Ø, Formă, Buc., L buc (m), L tot (m), Masă (kg), cu totalurile pe Ø. Comenzi: `rebar.add`, `rebar.update`, `rebar.addRun`, `rebar.updateRun`, `rebar.duplicateAs` (altă vedere, fără extras), `rebar.setSettings`; `IElementPlacement` pentru Move/Copy/Rotate/Mirror; ștergerea pe `element.delete`.

### Extensii de platformă (în `core`)

1. **Secțiune transversală pe un element** (`view.create {kind: 'section', across: <id>, station, flip?, margin?, depth?}`): planul perpendicular pe axa elementului la stația dată (grinzi, pereți, fundații continue; pentru stâlpi, un plan orizontal la înălțimea `station` deasupra bazei, privit de sus); fereastra desenului = secțiunea elementului + `margin` (implicit 0,5 m) de fiecare parte, stânga-dreapta (`Span`) și sus-jos (`Rise`, nou în `ViewSpec`/`HiddenLines.Draw`, tăiat la fel ca Span), adâncimea implicit = `margin`. Vederea urmărește elementul (stația se recitește din axa lui, ca `along`). În browser sub Secțiuni, numită „<element> · <stația>”. Orice axă e astfel un plan de secțiune, fără să fie desenată.
2. **Vedere inserată** (`LapinInset`, adnotare a core-ului): `viewId` (gazda), `source` (vederea desenată), `at` (în gazdă), `factor` (implicit scara gazdei / scara sursei, deci aceeași mărime pe hârtie), `frame` (chenar și titlu). `Views.Render(sursa)` → pens → linii, umpleri, texte ale unui `ViewMark` mutate în gazdă; o inserare nu desenează inserările sursei (adâncime 1). Snaps din colțurile tăieturilor, deci barele se desenează pe conturul de beton. Mutabilă (grip la `at`, `IElementPlacement`).
3. Unealta **Secțiune aici** (Rebar 2D → Secțiuni): alegi elementul în vederea curentă (plan sau secțiune longitudinală), stația e punctul apăsat, iar secțiunea transversală apare inserată alături, în același viewport, cu contextul ei (placa, stâlpul) — ce face Allplan, fără dialogurile lui.

### Modulul `rebar-pro` (premium, depozitul frate `../lapin-rebar`, mai târziu)

Armarea *configurată* pe tipuri, cum a cerut utilizatorul: etrierii și barele longitudinale ca parametri ai tipului de grindă (zone de îndesire la capete după regula aleasă), armarea superioară a plăcii pe grinzi, fundațiile din profil cu logica lor, radierul, scările din tipul lor. Motorul generează `IfcReinforcingBar` (corpuri `swept disk`, instanțe), le taie în secțiuni și le desenează automat cu simbolurile de mai sus (marcă, distribuție, extras), regenerate când elementul se schimbă. Se leagă în modulul liber prin `RebarHooks` (motor) și `rebarHooks` (UI), ca `MepHooks`.

## Etrierii pe tip (decizia 215)

În varianta liberă etrierii nu se desenează bară cu bară: se configurează pe tipul de grindă sau de stâlp (secțiune dreptunghiulară), cu diametru, acoperire și modul de distribuție — un pas pe toată lungimea sau trei zone, fiecare cu partea ei din lungime (implicit o treime) și pasul ei. Motorul îi așază pe axă (`TypeStirrups.Of`: dreptunghiul din interiorul acoperirii la fiecare stație) și îi desenează în 2D ca *proiecții* după cum stă vederea față de axă: în secțiunea transversală, dreptunghiul; în cea longitudinală, liniile; în plan, liniile peste grindă (planurile și fațadele doar cu disciplina Structure). Nu există corp 3D în varianta liberă. Intră în extras sub marca tipului. Barele longitudinale nu se pun pe tip (merg peste mai multe deschideri): se desenează de mână în secțiunea longitudinală sau în plan și stau în spațiu (decizia 216, `Rebar3D`), așezate de n ori pe direcția vederii după acoperirea gazdei sau la un pas; secțiunea transversală le arată ca puncte, extrasul le numără. Premiumul adaugă corpurile 3D, optimizatoarele și configuratorul pe secțiuni.

## Etape

1. Motorul `Lapin.Rebar`: formele, îndoirea, ciocurile, lungimile, distribuția, extrasul; testele cu numerele configuratorului.
2. Platforma: secțiunea transversală (`across`, `Rise`), vederea inserată, teste.
3. UI: tabul Rebar 2D, uneltele, gripurile, panoul de proprietăți, setările; traducerile.
4. Ghidul (`docs/user/rebar.md`, toate limbile) și scena lui.
5. Premium: planul de mai sus, după ce liberul e în uz.
