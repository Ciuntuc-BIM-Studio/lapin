# Stratificația în etichetă, descrierea materialelor și antemăsurătoarea pe grosimi

Cerut la 05.11.2026, după ideea unui tester: „În Revit mi-am făcut un script care îmi pune toată stratificația peretelui într-un tag. Informația din tag e preluată din stratificația peretelui, descrierea materialelor din proprietățile materialelor și grosimea din grosimea layerului. […] Unele materiale au o grosime specifică, de exemplu polistiren sau vată minerală (se găsesc de cumpărat doar la anumite grosimi) și în antemăsurătoare trebuie date separat, în m², în funcție de grosime; la altele, cum ar fi balast sau beton, e la m³ și nu contează grosimea. Practic tag-ul nu preia valori de parametri simplu, ci creează un tag arhitectural pe baza unui șablon mai complex, parsează text plus parametri.” Aprobat de utilizator la 05.11.2026 („da”), cu împărțirea de mai jos.

## Împărțirea

Ce spune un material despre el ține de **material**; cum arată textul ține de **tipul de etichetă**. Tipul de perete doar ordonează straturile.

| Ce | Unde | De ce |
|---|---|---|
| Descrierea de specificație, proprietățile (λ, μ, rezistențe, reacția la foc) | materialul (Manage → Materials) | un material e în zece tipuri de perete; se scrie o dată, îl citesc eticheta, antemăsurătoarea, devizul, memoriul |
| Cum se măsoară și cum se cumpără | materialul | aceeași regulă pentru toate elementele în care apare |
| O notă despre felul în care e pus în acel tip („în strat continuu, tip Baumit SupraFix”) | stratul tipului de perete, de placă, de acoperiș | ține de alcătuire, nu de material |
| Forma textului (antet, rândul fiecărui strat, final, condiții) | tipul de etichetă (Drafting standards → Tags) | o firmă își face forma o dată și o folosește în toate proiectele |

## 1. Materialul

Pe `IfcMaterial`, în atributele lui Lapin:

- `description`: textul de specificație, mai multe rânduri;
- `params`: parametrii utilizatorului, ca la elemente și tipuri (`Params`: text, număr, lungime, arie…), cu numele lor; cei cu un nume standard IFC sunt scriși în seturile IFC ale materialului (`Pset_MaterialThermal.ThermalConductivity` pentru λ, `Pset_MaterialHygroscopic` pentru μ, `Pset_MaterialMechanical` și celelalte, doar cu numele lor exacte, prin `StandardPsets`); ai utilizatorului merg într-un set propriu, niciodată `Pset_`;
- `measure`: cum se măsoară în antemăsurătoare:
  - `by`: `area` (m², împărțit pe grosimi: plăci, termoizolații, membrane), `volume` (m³: beton, balast, șapă, mortar), `mass` (kg, din densitate), `count` (bucăți, pentru plăci de mărime fixă); lipsă = cum face antemăsurătoarea azi;
  - `thicknesses`: catalogul materialului, grosimile în care îl face producătorul (mm, de exemplu 50, 80, 100, 150, 200, 240). Grosimile corecte le pune utilizatorul în stratificații; Lapin nu descompune un strat. Catalogul servește la două lucruri: celulele de grosime din tabelele de straturi (Wall types, Floor types) oferă grosimile lui (se poate scrie și alta), iar un strat de altă grosime e semnalat: un semn de avertizare în tabelul straturilor, o notă pe rândul lui din antemăsurătoare („not a catalogue thickness”) și o verificare CDE (Manage → Check, regula `catalogueThickness`: „Layer of <material> in <type> is 130 mm, not a catalogue thickness”);
  - `unit`: unitatea în care se cumpără (o etichetă: m², m³, kg, t, buc.); `board`: mărimea unei plăci, `[lungime, lățime]` în metri, pentru `count` (altfel parametrii `Board length` și `Board width`).
- Densitatea: `Pset_MaterialCommon.MassDensity` sau un parametru `Density` (ρ) al materialului.

Dialogul **Manage → Materials** primește un tab **Data**: descrierea, parametrii (adăugare, ștergere, tipul, valoarea), regula de măsurare cu grosimile comerciale. În IFC: `IfcMaterialProperties` cu seturile de mai sus, descrierea în `IfcMaterial.Description`; citite înapoi la import.

Stratul unui tip (`MaterialLayer`) primește `note`: textul despre felul în care e pus în acel tip, editat în tabelul straturilor din Wall types, Floor types, Roof types.

## 2. Antemăsurătoarea pe grosimi

**Cost → Quantity takeoff**, grupat **By material**, ține cont de regula materialului: un material `area` dă un rând pe material × grosime, grosimea fiind a stratului din stratificație (Vată minerală 150 mm: 84,2 m²; Vată minerală 200 mm: 31,0 m²), unul `volume` un singur rând în m³, unul `mass` în kg, unul `count` în bucăți (pe grosime, ca `area`). Coloana **Thickness (mm)** apare când un rând e pe o grosime, coloana **Note** când un rând are o notă (o grosime din afara catalogului, un material `mass` fără densitate, unul `count` fără mărimea plăcii). Același lucru în extrasul de finisaje pe încăperi (materialul scris cu grosimea lui) și în deviz: un articol legat de un material (o regulă pe material sau un articol al tipului pe `material:<id>`, decizia 306) își ia cantitatea după regula materialului, dacă articolul nu-și dă singur măsura, și ține câte un rând pe grosime. Materialele fără regulă rămân cum sunt azi.

## 3. Eticheta de stratificație

O formă nouă a tipului de etichetă, `buildup` (lângă `box`, `opening`, `room`), pentru pereți, plăci, acoperișuri și tavane:

- **Antet** (de exemplu `EXTERIOR`), **rândul unui strat** (repetat pentru fiecare strat), **final** (`INTERIOR`);
- **ordinea straturilor** se alege pe tipul de etichetă: de la exterior spre interior (implicit) sau invers la pereți, de sus în jos (implicit) sau invers la plăci și acoperișuri, cum stă elementul în model, oglindirea inclusă (cerut de tester, 05.11.2026: „să se poată și invers, să opteze utilizatorul”);
- câmpurile rândului: `{index}`, `{material}` (numele), `{description}` (descrierea materialului, altfel numele), `{thickness}` (în unitatea etichetei: mm sau cm), `{note}` (nota stratului), `{function}` (miez, finisaj…), orice parametru al materialului după nume (`{λ}`, `{ThermalConductivity}`, `{Pset_MaterialThermal.ThermalConductivity}`), și ale elementului ca la etichetele de azi;
- **părți condiționate**: `[μ = {μ}]` apare doar când materialul are μ; tot ce e între paranteze drepte cade dacă un câmp din el e gol;
- **straturile de grosime 0** (decizia 291: membrane, vopsea) apar în etichetă (cerut de tester), cu o opțiune pe tip de a le ascunde; straturile cu părți pe înălțime (decizia 264) dau un rând pe material;
- rândurile încep cu o bulină, un număr sau nimic; textul se rupe pe lățimea etichetei, cu aliniere la stânga;
- **săgețile** se aleg pe tipul de etichetă (cerut de tester): o singură săgeată spre element, sau câte una pe strat, fiecare până în stratul ei; eticheta urmează elementul (decizia 320).

Un tip de etichetă `buildup` implicit, **Build-up (exterior → interior)**, ca în exemplul testerului: `EXTERIOR`, apoi `• {description}: {thickness} mm`, apoi `INTERIOR`. **Annotate → Tag** (TG) îl alege singur pe un perete, o placă sau un acoperiș dacă tipul de etichetă activ e de forma asta.

Șablonul folosește aceeași regulă de câmpuri ca etichetele de azi (`TagText`), cu repetarea și condițiile în plus; documentele (decizia 311) citesc tot de aici stratificația, cu descrierile materialelor.

## Etape

1. Materialul (descriere, parametri, regula de măsurare, grosimile comerciale, nota stratului), IFC, antemăsurătoarea pe grosimi, devizul pe regula materialului.
2. Eticheta de stratificație: forma `buildup`, editorul ei în Drafting standards → Tags, desenarea în plan, secțiuni și fațade, pe planșe și în PDF. Făcută la 05.11.2026: `TagType` cu `header`, `footer`, `order` (`out-in`/`in-out`), `zeroLayers`, `leaders` (`one`/`per-layer`), `bullet`, `width`; `BuildUps`, `BuildupTemplate`, `BuildupText` în platformă, `BuildupTags` în drafting; exteriorul unui perete e partea primului strat (regula din `WallReference`); λ, μ, ρ tipărite în PDF cu fontul Symbol; `BuildupTagTests`, scena `buildup-tag`.
3. Ghidul (materials, drafting, schedules) în toate limbile, cu scena lui.

## Săgeata frântă, ca etichetele din ArchiCAD (decizia 331, ideea unui tester, 06.11.2026)

Săgeata oricărei etichete (uși, ferestre în secțiuni și fațade, camere, materiale, stratificație) are două forme, pe tipul de etichetă (`leaderShape`): `straight` (caseta de până acum, cu săgeata din latura ei mai apropiată, frântă într-un cot liber `elbow`) sau `elbow`, implicit: o linie oblică de la element până la cot, apoi un umăr orizontal pe sub text, fără casetă. `underline` spune ce subliniază umărul: `first` (implicit, ca ArchiCAD), `each`, `last` sau `none` (umărul se oprește la marginea textului, la mijlocul primului rând); `extension` (mm pe hârtie) îl prelungește de la text spre cot.

- Cotul e punctul etichetei (`at`), așa că urmărește elementul prin `TagHold` ca orice punct al ei; textul stă de partea cotului opusă capătului săgeții (`TagLeaders.Side`), aliniat spre cot. Tras dincolo de capăt, textul trece pe partea cealaltă.
- În interfață cotul are mânerul lui, ținut la 30°, 45°, 60° sau 90° față de capăt (`snapKnee`, `Grip.constrain`; Shift: liber); mânerul textului mută eticheta cu cot cu tot.
- La stratificație cu câte o săgeată pe strat, fiecare se termină sub rândul ei (`none`: la mijlocul lui), coturile pe o coloană sub cotul etichetei; tras, cotul mută coloana, capetele rămân în straturile lor.
- Proiectele vechi nu se schimbă la deschidere: `TagLeaderUpgrade` dă `leaderShape: straight` etichetelor fără `leaderRev` (ștampila pusă de `addTag` pe cele noi), deci textul nu se mută; un tip fără câmp e `elbow`, pentru etichetele noi. O etichetă își poate alege forma (`leaderShape` pe ea, Properties → Leader shape); trecută la `elbow`, punctul ei devine cotul (textul se mută cu o jumătate de casetă).
- `drafting.tagPreview` desenează tipul pe hârtie cu aceleași funcții (`TagLeaders.Plain`, `TagLeaders.Buildup`, `Tags.Box`), pentru Drafting standards → Tags → Leader… și editorul stratificației. Teste: `TagLeaderTests`, `tagLeader.test.ts`.
