# Grafica materialelor în 2D: hașuri, umpleri, tăieturi

Propus pe 17.11.2026, după cererea unui utilizator: „focusing on fixing materials and hatches, and how they project on cut walls. It doesn't matter how many features you ship if we can't produce the correct looking documentation.”

Stare: **aprobat de utilizator pe 17.11.2026** (răspunsurile în §4). Accentul, în cuvintele lui: „ideea e în principal, în general, să facem configuratorul de hașuri și materiale cât mai flexibil: culori, grosimi de linie, cu sau fără fundal plin”. Fiecare etapă făcută primește o decizie în `docs/decisions-pending.md`.

## 1. Ideea

Nu există un standard mondial pentru hașurile materialelor. ISO 128-50, DIN 1356, normele românești, BS, PN-B, RT și obiceiurile Revit sau ArchiCAD nu se potrivesc între ele. Lapin a schimbat de trei ori tabelul care ghicește hașura după numele materialului (deciziile 325, 456, 460), iar [MaterialGraphics.cs](../engine/src/Platform/Lapin.Drawing/MaterialGraphics.cs) le păstrează pe toate trei. Planul desparte două lucruri:

- **Corectitudinea**, aceeași peste tot: ierarhia liniilor (tăietură > proiecție > hașură), îmbinări curate, densitatea potrivită scării, hașura ținută în contur, membranele, alinierea. Se poate verifica, deci se testează (etapa 0) și se repară întâi.
- **Convenția**, diferită de la o țară sau un birou la altul. Lapin nu alege una pentru toată lumea: livrează câteva standarde ca date, iar utilizatorul le alege, le editează, le desenează și le partajează.

Principiile rămân cele din CLAUDE.md:

- extinzi, nu strici: aspectul de azi rămâne cel implicit, iar un proiect vechi se deschide neschimbat (`IProjectUpgrade` unde e nevoie);
- un singur resolver decide ce desenează o vedere (`VisibilityGraphics`), deci ecranul, planșele, PDF-ul și DXF-ul arată la fel;
- modelele de hașură sunt desenate de Lapin, niciun `.pat` sau fișier Autodesk copiat;
- textele noi intră în toate limbile din `ui/src/i18n`.

## 2. Ce există (17.11.2026)

- **Grafica stă pe material** (decizia 52): `section`, `plan` și `elevation` au fiecare hașură, scară, unghi, aliniere (vedere, strat, axul elementului: decizia 369), umplere (o culoare sau culoarea materialului) și peniță. Se mai dau `membrane`, `drawJoints` și `joins`. Ce nu e dat se ghicește din nume (`Guesses`, `ElevationGuesses`). Se editează din Manage → Materials și din mostra de lângă fiecare strat (decizia 493, `MaterialGraphicsTable`).
- **Modelele de hașură:** 22 incluse ([HatchLibrary.cs](../engine/src/Platform/Lapin.Drawing/Hatch/HatchLibrary.cs)) și BATT, desenat după regula lui. Se pot importa fișiere `.pat`, ținute pe `IfcProject` (decizia 54), cu colecția „Hașurile mele” pe calculator. Hașurile intră și în `.lapinstandards` și în extensii.
- **Tăietura** ([MaterialDrawing.cs](../engine/src/Platform/Lapin.Drawing/Views/MaterialDrawing.cs), [Pens.cs](../engine/src/Platform/Lapin.Drawing/Views/Pens.cs), `docs/view-styles.md`):
  - același material se citește ca o singură bucată peste elemente, iar `SplitAtCorners` taie liniile la colțuri;
  - liniile dintre straturi folosesc peniță de separare;
  - membranele sunt linie groasă întreruptă;
  - o hașură plină ia culoarea umplerii (decizia 587);
  - straturile trec prin plăci după prioritate (decizia 497);
  - afișarea parțială a structurii are patru variante (decizia 500).
- **Pe vedere:**
  - stilurile de vedere (`finishHatch`, `surfaceHatch`, `layers*`);
  - Object styles pe categorie și rol;
  - Visibility/Graphics pe categorie: `fillColor` și `cutHatch`; subcategoriile (miez, finisaje, izolație, membrane) se pot doar ascunde;
  - filtrele schimbă culoarea, semitonul, transparența și liniile, dar nu hașura;
  - nivelul de detaliu e un LOD, nu ține de scară (decizia 404).
- **Fațadele:** hașurile suprafețelor văzute (`SurfaceHatches`, deciziile 325 și 327).
- **Planșa și exporturile:** legenda de materiale pe planșă (decizia 620), IFC (`IfcFillAreaStyle` pe tip de vedere), DXF, stilurile Bonsai (decizia 495).

### Ce lipsește

1. **Toate hașurile sunt la mărime fixă pe hârtie** (`Pens.cs:317`, `SurfaceHatches.cs:127`: un milimetru pe hârtie ori scara). Pentru tăieturi e corect. Pentru fațade și planuri de pardoseală e greșit: asizele de cărămidă, plăcile de 30×30 și scândurile trebuie să aibă mărimea lor reală.
2. **Nicio reprezentare după scară.** Aceeași hașură apare la 1:20 și la 1:200.
3. **Convenția e cod, nu date.** Tabelul de ghicire e o listă de expresii regulate în C#. Utilizatorul nu o poate schimba și nu poate alege alt standard.
4. **Un material arată la fel în toate disciplinele.** Nu poți avea betonul gri plin în structură și punctat în arhitectură fără să schimbi materialul. Subcategoriile nu au grafică proprie.
5. **Regulile (filtrele) nu pot schimba hașura sau umplerea.**
6. **Un singur model de hașură pe material.** Lipsesc al doilea model (fundal) și o culoare a hașurii pe material.
7. **Un model de hașură nou se poate doar importa** dintr-un `.pat`, nu desena.
8. **Nicio verificare vizuală** a desenelor, pe care să o putem arăta.

## 3. Etapele

### Etapa 0: planșe de referință și teste vizuale

O mică bibliotecă de **detalii de referință**, modele construite din cod, fiecare desenat de motor la 1:20, 1:50 și 1:100. Fiecare detaliu e și scenă de ghid, și test:

- perete compus cu tencuieli, izolație și zidărie: colț în L, T, capăt liber, gol cu glaf;
- perete pe placă (strat care coboară, decizia 497);
- placă cu pardoseală și șapă;
- fundație continuă cu hidroizolație;
- stâlp și grindă din beton în perete de zidărie;
- acoperiș cu învelitoare;
- o fațadă cu cărămidă, placare și țiglă.

Testele (`MaterialGraphicsReferenceTests`) verifică:

- regulile de corectitudine: nicio linie de tăietură între benzile aceluiași material, hașura nu iese din bandă, ierarhia penițelor, nicio bandă fără hașură unde materialul are una, membranele pe interfață;
- un **instantaneu** al desenului (pens serializate, nu pixeli), care se schimbă doar intenționat;
- o pagină PDF generată, cu toate detaliile, pentru ochi.

Stare (17.11.2026, decizia 670): **făcută pe motor.** Detaliile sunt în `engine/tests/Lapin.Engine.Tests/ReferenceDetails.cs` (construite din comenzi, deci refolosibile); PDF-ul și proiectele `.lapin` ale detaliilor se scriu cu `LAPIN_REFERENCE_PDF=1` în `build/material-graphics-reference/`, de unde scenele ghidului (etapa 8) le pot deschide. Desenele de azi trec toate regulile; instantaneul e `MaterialGraphicsReference.snap.txt`.

### Etapa 0b: configuratorul flexibil (prioritatea utilizatorului)

Ce era etapa 6, lărgit și adus în față. Pe fiecare material, pentru fiecare fel de vedere (tăietură în secțiune, tăietură în plan, fațadă), tot ce se vede se poate seta. Ce rămâne nesetat vine, ca azi, de la categorie, de la standard sau din nume.

- **Hașura (prim-plan):**
  - modelul, scara, unghiul, alinierea;
  - **culoarea** (`hatchColor`, înlocuiește culoarea categoriei);
  - **grosimea liniei**, în mm (seria ISO 0,13–1,0 sau o valoare liberă), pe lângă peniță: `hatchWeight` câștigă peste `hatchPen`;
  - **tipul de linie** al hașurii (`hatchLineType`, din tipurile de linie ale proiectului);
  - opacitatea și semitonul.
- **Fundalul:**
  - **cu sau fără umplere** (`fill`: nimic, culoarea materialului sau o culoare aleasă), cu opacitatea ei;
  - **al doilea model de hașură** (`backgroundHatch`, cu scara, unghiul, culoarea și grosimea lui), ca prim-planul și fundalul din Revit.
- **Liniile benzii** (ca straturile din ArchiCAD), pe material: **conturul tăieturii** (`cutLine`: culoare, grosime, tip de linie) și **liniile de separare** față de straturile vecine (`separatorLine`). Nesetate, vin de la Object styles, ca azi (`Pens.Separations`).
- **Previzualizarea:** o bandă de probă desenată de motor la scara aleasă (1:20, 1:50, 1:100), deci configuratorul arată ce se tipărește.
- **Lucrul rapid:**
  - copierea graficii de la un material la altul și pe mai multe materiale deodată (`material.update` cu `ids`, un pas de anulare);
  - „Înapoi la implicit” pe fiecare câmp.
- **Exporturile urmează:**
  - IFC: două `IfcFillAreaStyleHatching`, culoarea și grosimea în `IfcCurveStyle`;
  - DXF: două entități HATCH cu culoarea și grosimea lor;
  - PDF, planșe, legenda de materiale (decizia 620), Bonsai.

Stare (17.11.2026, decizia 670): **motorul e făcut; interfața urmează.** Abateri și precizări:

- opacitatea e în culoare (`#rrggbbaa`); DXF o scrie ca transparență (codul 440);
- DXF păstrează hașurile ca linii, cum făcea deja (nu entități HATCH): fundalul are culoarea și grosimea lui pe același strat HATCH;
- IFC scrie `IfcFillAreaStyle` doar pentru materialele cu grafică proprie, ca fișierele proiectelor vechi să rămână la fel;
- o linie de tăietură la întâlnirea a două materiale care setează linii: câștigă cea mai groasă, apoi numele care vine primul;
- `null` readuce un câmp la implicit, dar la `hatch` și `fill` rămâne „niciuna” (ca înainte); `reset: [câmpuri]` readuce orice câmp;
- rămase: capacele secțiunii 3D desenate ca planșă (drum separat) iau doar umplerea și hașura; redenumirea unui tip de linie nu urmează în materiale.

### Etapa 1: hașuri la mărime reală

- Un model de hașură are **unități**: `paper` (hârtie, ca azi) sau `model` (metri reali). În `.pat`, header-ul `;%TYPE=MODEL` (convenția publică a formatului) se citește și se scrie. Modelele de azi rămân `paper`.
- Modele noi la mărime reală, desenate de Lapin:
  - cărămidă 240×63 cu rost de 10 (formatul românesc; `BRICK_NF` 240×71 dacă se cere);
  - plăci 300×300 și 600×600;
  - scânduri de 150;
  - piatră în asize;
  - țiglă;
  - tablă fălțuită la 500.
- Pe material, `hatchUnits` poate suprascrie unitățile modelului (rar folosit).
- Ghicirea pentru fațade trece pe modelele `model`, dar **doar pentru proiecte noi**. Un proiect vechi primește la deschidere vechea hașură pe hârtie (`MaterialHatchUpgrade`, `hatchRev` 3).
- **Originea pe element:** asizele unei fațade pornesc de la baza peretelui sau de la cota nivelului. Un grip mută originea (`hatchOrigin` pe element, un pas de anulare).
- Planurile de pardoseală (`finishHatch`) folosesc modelele `model`, ca plăcile să aibă mărimea lor.

Stare (17.11.2026, decizia 671): **motorul e făcut; interfața urmează.** Abateri și precizări:

- unitățile: `HatchPattern.Units` (`paper` / `model`); `.pat` citește `;%TYPE=MODEL|DRAFTING` (după header sau înaintea lui) și `;%UNITS=MM|INCH` (inch × 25,4), scrie `;%TYPE=MODEL` doar la modelele `model`, deci fișierele de azi se scriu la fel;
- modelele noi, desenate de Lapin, cu rostul ca o singură linie (o linie dublă la 10 mm s-ar contopi pe hârtie): `BRICK_240X63` (implicit), `BRICK_240X71`, `BRICK_215X65`, `BLOCK_400X200`, `BLOCK_600X250` (BCA, adăugat: o fațadă de BCA nu e de cărămidă), `TILE_300X300`, `TILE_600X600`, `BOARDS_150` (2,4 m, rosturi decalate), `STONE_600X300`, `ROOF_TILES` (pas 330, lățime 300), `METAL_SEAM_500`;
- proiectele vechi: în loc să fixeze o hașură pe fiecare material (ca la `hatchRev` 1 și 2), upgrade-ul pune pe proiect `paperSurfaceHatches: true`, iar ghicirea pentru fațade și pardoseli rămâne cea pe hârtie. Motivul: o pardoseală ia hașura de fațadă proprie a materialului înaintea ghicirii, deci o hașură fixată pe material ar fi schimbat planurile vechi (stejarul ar fi trecut de la WOOD la BOARDS);
- pragul de densitate: câmpul de stil `minHatchSpacing` (mm pe hârtie, implicit 0,7; 0 desenează tot), suprascris pe vedere cu `view.setStyle`; se aplică doar modelelor `model`, unde nu se desenează nimic (nici umplere în loc);
- originea: `hatchOrigin` pe element, `[u, v]` în metri în cadrul elementului (la un perete: `u` de-a lungul axei de la start, `v` în sus de la bază în fațade și secțiuni, spre stânga axei în plan); comanda `element.setHatchOrigin {id|ids, origin: [u, v] | null}`, un pas de anulare. Fără origine, un model `model` pornește asizele de la baza elementului (pe orizontală de la originea vederii, ca rosturile să continue de la un perete la altul); un model `paper` rămâne la originea vederii, deci nu trebuie poartă pe `hatchRev`: proiectele vechi nu aveau modele `model`;
- legendele desenează un model `model` la scara vederii în care stau (același drum ca desenul); `hatch.preview` la 1:20 (sau `viewScale`); foaia de stil Bonsai, care n-are scară, la 1:100; IFC scrie `ModelOrDraughting` adevărat pentru un model `model`;
- pardoselile în plan: plăci și piatră `TILE_600X600`, faianța `TILE_300X300`, parchet și scânduri `BOARDS_150`; mocheta, PVC-ul și pardoselile turnate rămân pe hârtie (n-au mărime);
- rămase: hașurile simbolurilor, ale tabelului straturilor (`TypeBuildUps`) și ale cartușelor rămân pe hârtie; importul IFC nu citește încă `ModelOrDraughting`; capetele secțiunii 3D iau unitățile, dar nu și originea elementului.

### Etapa 2: reprezentarea după scară

- Pe material, pe fiecare fel de vedere: `byScale`, o listă de trepte `{ from, to, hatch, fill, pen }`. Exemplu pentru beton:
  - până la 1:50, punctat;
  - de la 1:100, gri 30 % plin;
  - de la 1:200, plin.

  Fără trepte, materialul arată ca azi.
- Pe stilul de vedere: **„Tăieturi la scară mică”**, opțiunea `smallScaleCut` cu pragul ei: `asMaterial` (ca azi), `solid` (plin, în culoarea categoriei sau negru), `outline` (doar conturul) sau `coreOnly` (miezul plin, restul gol). E „coarse scale fill” din Revit, cu prag de scară, nu de LOD.
- Ordinea priorității, ca o singură regulă în resolver: elementul, apoi filtrul (etapa 5), apoi setul de grafică al stilului (etapa 4), apoi treapta de scară a materialului, apoi materialul, apoi standardul (etapa 3).

### Etapa 3: standardele ca date, alese de utilizator

- **Tabelul de reguli al standardului:** „numele sau categoria materialului se potrivește cu X → tăietură Y, plan Z, fațadă W, membrană, categorie”. Expresiile regulate din `Guesses` și `ElevationGuesses` devin primul tabel, „Lapin (asemănător Revit)”, cu aceleași rezultate. Testele de azi trec neschimbate.
- **Pachetele livrate** (fiecare: tabelul, modelele lui, penițele implicite pentru tăietură și hașură, eventual treptele de scară):
  - „Lapin”, cel implicit: un singur aspect, plăcut și recunoscut peste tot (Î8). Pornește de la tabelul de azi (asemănător Revit), revizuit pe planșele etapei 0 pentru echilibrul tonurilor și al grosimilor, nu pentru o țară anume. Proiectele vechi își păstrează aspectul (`hatchRev`);
  - „ISO 128-50”;
  - „DIN 1356”;
  - „România (SR ISO 128-50)” (Î6): ISO 128-50 cu numele materialelor românești în reguli;
  - „BS 8888 / BS 1192”.

  Ce nu facem noi vine prin magazinul de extensii (felul `standards` există): PN-B (Polonia), RT (Finlanda), UNI (Italia).
- **Unde se alege** (niciodată automat după limbă sau țară, Î8):
  - la proiect nou (lângă unități și șablon), implicit „Lapin”;
  - în Manage → Drawing standards → Materials, cu un editor de tabel (rânduri, ordine, previzualizare pe un perete de probă);
  - „Aplică pe proiect”, cu previzualizare și alegerile `LibraryMerge`: ce primește doar materialele fără grafică proprie, ce suprascrie tot.
- Biblioteca de materiale a biroului (folderele firmei) poate duce materialele cu grafica lor, deci un birou își aduce standardul complet.
- **Personalizat** (cerut de utilizator pe 17.11.2026: „standardul să poată fi și personalizat, și încărcat din altă țară, să poată fi făcut și descărcat pachet din store”):
  - orice standard, și cele livrate, se **duplică** și se editează (reguli, modele, culori, grosimi, fundaluri, treptele de scară), cu nume și autor. Cele livrate rămân neschimbate, ca bază la care se poate reveni;
  - **„Salvează standardul din proiect”**: grafica materialelor unui proiect (cea setată de mână în etapa 0b, nu doar regulile) devine un standard nou. Așa un birou își face standardul lucrând, nu completând un tabel;
  - standardul proiectului se schimbă oricând, cu previzualizare înainte și după, un pas de anulare.
- **Un pachet, din orice țară:**
  - un standard se **exportă** ca fișier: felul `standards` (`.lapinstandards`), cu secțiunea nouă `materialGraphics`: reguli, modele `.pat` sau desenate, treptele de scară, seturile pe disciplină. Se **importă** din fișier sau din folderele firmei, cu aceleași alegeri (actualizează, păstrează-le pe amândouă, sari);
  - fișierul își spune țara, limba numelor din reguli, norma pe care o urmează (de exemplu „PN-B-01030”), versiunea și licența. Regulile pot avea nume în mai multe limbi, ca un pachet polonez să recunoască și materialele numite în engleză.
- **Din magazin** (`docs/plan-extensions.md`, felul `standards` există):
  - în aplicație, Catalogul → Standarde de desen arată pachetele magazinului, cu filtre pe țară și normă, o planșă de probă (detaliile etapei 0, desenate cu pachetul) și „Instalează”. Pachetul instalat apare lângă cele livrate și se aplică pe proiect ca ele;
  - **„Publică în magazin”** pornește din dialogul standardului: scrie manifestul (id, nume, țară, normă, licență, autor) și imaginile de previzualizare din detaliile etapei 0, apoi deschide calea obișnuită a autorilor (panoul autorului pe store.lapinbim.com sau pull request în `lapin-extensions`, revizuit și aprobat ca orice extensie). Gratuit sau plătit, după regulile magazinului;
  - un pachet actualizat în magazin e oferit proiectelor care îl folosesc, cu „Actualizează” și previzualizarea diferențelor, niciodată automat.
- Pachetele livrate cu Lapin (Lapin, ISO 128-50, DIN 1356, România SR ISO 128-50, BS) sunt și ele în magazin, ca exemple pentru autori.

Stare (18.11.2026, decizia 673): **motorul e făcut; interfața urmează.** Abateri și precizări:

- standardul e date (`MaterialStandard`, Lapin.Drawing): id, nume, descriere, autor, țară, normă, versiune, licență, limbi, `indicative`, `basedOn`, `fold`; reguli ordonate `{match: {names: {en: […], ro: […], de: […]}, category?}, section?, plan?, elevation?, membrane?, category?}` (fiecare nume e un cuvânt sau o expresie mică, căutat în numele materialului împăturit: litere mici, un spațiu, fără diacritice — după un tabel, motorul rulează fără ICU); modelele lui (`patterns`, text `.pat`, hârtie sau model); penițele lui (`pens: {hatch, cut}`: culoare, grosime, tip de linie, sub reguli). O tăietură ia prima regulă care spune ceva de tăietură (section, plan sau membrane), o fațadă prima care spune ceva de fațadă; aceeași listă le ține pe amândouă;
- „Lapin” e `MaterialStandards/lapin.json`, cu exact rezultatele tabelelor de cod (verificat pe peste 1500 de nume, cu diacritice și fără); tabelele vechi pentru `hatchRev` 0–1 și fațadele de hârtie rămân în cod. Pachetele livrate sunt fișiere JSON încorporate: `iso-128-50`, `din-1356`, `ro-sr-iso-128-50`, `bs-8888`, desenate de Lapin din principiile generale, toate marcate orientative; modelele noi: `ISO_135`, `ISO_AERATED`, `ISO_STONE`, `DIN_PORENBETON`, `DIN_NATURSTEIN`, `BS_BLOCK`, `BS_STONE`;
- ordinea, de jos în sus: standardul, treapta lui de scară, materialul, treapta lui; un material cu modelul lui nu ia scara, unghiul și unitățile standardului; unul cu fundalul lui nu ia fundalul standardului. Membrana unei reguli e a tăieturii (fațada: nu);
- proiectul își numește standardul (`materialStandard`, lipsă = „lapin”), standardele lui sunt în `materialStandards`. „Aplică” pune numele, copiază modelele standardului în ale proiectului (cele lipsă, și pe cele cu același nume desenate altfel) și dă materialelor fără categorie categoria regulii; cu `overwrite` scoate din material câmpurile pe care standardul le setează pentru el (și, cu penițe, culoarea, grosimea, tipul hașurii și linia de tăietură, și steagul de membrană). Importul nu aplică niciodată;
- un pachet doar de standarde (`only: "materialGraphics"`) scrie `drafting: {}`; la citire, un `drafting` gol nu mai e citit ca toate valorile implicite;
- revizia estetică a lui „Lapin” (făcută în decizia 674, vezi etapa 4: „Lapin” 1.1 pentru proiectele noi) **nu era făcută aici**: pe planșele de referință, izolația rigidă (grila RIGID de 2,4 mm) e cel mai închis ton din detalii, mai închis decât zidăria (densitate de cerneală ~0,83 față de ~0,31 mm/mm²), iar betonul (punctat) e mai deschis decât zidăria; propunerea: RIGID la `hatchScale` 1,5 și CONCRETE la 0,75 pentru proiectele noi. Nu am aplicat-o: fiecare proiect de test e „nou”, deci ar schimba instantaneul și multe teste, și e o alegere de gust pe care o lasă utilizatorului (o versiune 1.1 a pachetului „lapin”, marcată pe proiectele noi);
- exemplele pentru magazin sunt în `extensions-packs/standards/<id>/` (manifest, `.lapinstandards`, `screenshot.png` din `standards.previewSheet`), validate cu regulile catalogului; manifestul nu are câmpuri pentru țară și normă (schema are `additionalProperties: false`): sunt în etichete (`country-de`) și în descriere, până când schema primește `country` și `norm`. `LAPIN_WRITE_PACKS=1` le scrie din nou (`MaterialStandardTests`);
- tot aici: `element.hatchOriginPoint` / `element.hatchOriginAt` (mânerul originii hașurii), `hatchOrigin` e deja în `node.props`; redenumirea sau ștergerea unui tip de linie al proiectului trece și prin materiale (`hatchLineType`, `cutLine`, `separatorLine`, în trepte și fundal) și prin standardele proiectului;
- rămase: interfața (alegerea la proiect nou, editorul de tabel, Aplică cu previzualizare, Catalogul → Standarde, „Publică în magazin”), oferirea unei versiuni noi a unui pachet instalat; plăcile de pardoseală ale camerelor (`RoomFinishes`) iau standardul din decizia 674 (proiectele noi).

### Etapa 4: grafica pe disciplină, pe stil și pe subcategorie

- **Seturi de grafică a materialelor** (`materialGraphicsSets` pe proiect): pe material sau pe categoria materialului (Structure, Finish, Insulation…), hașura, umplerea și penița care înlocuiesc ce dă materialul. Un stil de vedere numește un set. Exemplu „Structură”: betonul plin gri, zidăria linii la 45°, finisajele fără hașură.
- **Subcategoriile din Visibility/Graphics** primesc grafică: hașură de tăietură, umplere, culoare, semiton. E ce a rămas din etapa C a `plan-visibility-graphics.md`. Exemplu: izolația semiton, finisajele fără hașură, miezul plin.

Stare (18.11.2026, decizia 674): **motorul e făcut; interfața urmează.** Abateri și precizări:

- **seturile** (`materialGraphicsSets` pe proiect, `MaterialGraphicsSets`, Lapin.Platform): `{id, name, entries: [{match: {material?, materialName?, category?}, section?, plan?, elevation?}]}`, fiecare fel de vedere cu toate câmpurile etapelor 0b, 1 și 2 (și `byScale`), verificate ca la `material.update`. O intrare se potrivește când tot ce dă se potrivește: `material` = id-ul materialului, `materialName` = un cuvânt sau o expresie mică găsită în numele împăturit (litere mici, fără diacritice, ca la standard), `category` = categoria materialului (a lui, altfel cea pe care standardul proiectului o dă numelui); `match: {}` ia orice material. Dintre intrările potrivite câștigă **cea mai particulară** (id > nume > categorie > orice), iar la egalitate prima din listă. Abatere: `materialName` e expresie, nu nume exact, ca setul „Structură” să poată deosebi betonul de zidărie (ambele Structure);
- un stil numește setul (`materialGraphicsSet`, scris doar când e setat); o vedere îl suprascrie cu `view.setStyle {materialGraphicsSet: id | "none"}`, `null` îl readuce pe al stilului (`none` = niciun set peste al stilului). Comenzi, fiecare un pas de anulare: `materialGraphicsSet.list` (interogare: `{sets: [{id, name, entries, builtIn, edited, styles}], categories}`), `.create {name, from?, entries?}` → `{id}`, `.update {id, name?, entries?}`, `.delete {id}` (stilurile care îl numesc rămân fără set, vederile care îl numesc iau al stilului); `viewStyle.create/update {materialGraphicsSet}` verifică id-ul. Redenumirea unui tip de linie trece și prin seturi;
- **ordinea** din `ViewMaterialGraphics`: elementul (cârlig) > filtrele (etapa 5, cârlig) > **subcategoriile din V/G (treapta 2,5)** > **setul (3)** > `smallScaleCut` (4) > treapta materialului > materialul > standardul. Subcategoria e o suprascriere a vederii, deci bate setul stilului; hașura și umplerea **categoriei** din V/G (puse la penițe, ca înainte) bat setul, dar nu și subcategoria cu hașura sau umplerea ei (`KeepsHatch`, `KeepsFill`), fiind mai particulară. Setul se aplică și fațadelor (`SurfaceHatches`), nu și legendelor;
- seturile livrate „Structure” (betonul fără hașură, plin `#999999` = gri 40 %; blocurile, cărămida și piatra `MASONRY` la 45°; finisajele fără hașură și fără umplere; izolația semiton) și „MEP” (fără hașură, fără umplere, semiton, liniile de tăietură și dintre straturi gri `#999999`; fațadele fără hașură) sunt ale **proiectelor noi** (Î10): proiectul nou are semnul `materialLookRev: 1` (pus în `EnsureBuilding`, lângă `hatchRev`); fără semn, `materialGraphicsSets` lipsă = niciun set și stilurile implicite nu numesc niciunul; cu semn, lista lipsă = cele două livrate, iar stilurile Structural și MEP le numesc. Lista scrisă (chiar goală) e a proiectului, deci un set livrat șters nu revine;
- **subcategoriile** (`graphics.categories.<cat>.sub.<sub>`, în vederi, în seturile de grafică și în rândurile proprii ale legăturilor afișate „custom”): `hidden`, `cutHatch` (un model; `none` = fără), `fillColor` (`none` = fără), `lineColor`, `lineWeight` (o peniță ISO; conturul are greutatea, liniile dintre straturi două penițe mai jos, ca la categorie), `halftone` (hașura, umplerea — a subcategoriei, altfel a categoriei, altfel a benzii — și liniile spălate pe jumătate spre alb). `view.graphics` verifică rândul; `view.visibility` dă `subcategoryFields`. În 3D (`view.looks3d`): semitonul ca opacitate 0,35 pe parte, `fillColor` ca culoare a părții. Se aplică benzilor cu material (o bandă fără material nu are subcategorie de strat); membranele rămân linia lor;
- **fișierul `.lapinstandards`** are secțiunea `materialGraphicsSets` (exportul complet scrie seturile proiectului, fără cele livrate nemodificate; un pachet `only: materialGraphics` le scrie doar când `materialGraphicsSets: [ids]` le cere); o intrare cu id de material pleacă cu numele lui (`^nume$`). `standards.readPack` le arată (`materialGraphicsSets: [{id, name, entries, exists, same}]` și rânduri `materialGraphicsSet` în `entries`); importul are aceleași alegeri (update, both: „Nume (2)”, skip) și nu numește setul pe niciun stil;
- **„Lapin” 1.1** (rămas din etapa 3), doar pentru proiectele noi: densitatea de cerneală (lungimea liniei pe mm² × 0,13 mm, plus punctele) a fiecărui model al lui „Lapin” 1.0: RIGID 10,8 %, BLOCK 8,7 %, WOOD 7,6 %, STEEL 6,2 %, RCONCRETE 5,0 %, EARTH 4,3 %, MASONRY 4,1 %, TILES 3,2 %, SAND 1,5 %, GRAVEL 1,1 %, CONCRETE 0,6 %, GYPSUM 0,4 % (BATT depinde de grosime: 3–7 % la 1:20–1:50). În 1.1: RIGID `hatchScale` 1,5 (7,2 %, grila de 3,6 mm, încă vizibilă într-un strat de 2 mm la 1:50), BLOCK 1,25 (7,0 %), CONCRETE 0,75 (1,1 %): niciun model nu mai iese din grupul structural de ~7 %, izolația nu mai e mai închisă decât blocurile și zidăria, betonul aproape dublu; plus suprafețele pardoselilor (parchet și laminat `BOARDS_150`, mochetă `CROSS`, PVC/linoleum/cauciuc `HORIZONTAL`, epoxi, rășină, șapă și beton șlefuit `CONCRETE`, gresie, porțelanat și terrazzo `TILE_600X600`). `lapin.json` e 1.1 (cel din liste, din magazin, al proiectelor noi), `lapin-1.0.json` e 1.0 exact (`MaterialStandard.Lapin`, al proiectelor vechi și al codului fără proiect). Un proiect vechi care importă pachetul 1.1 îl primește ca o copie alături și îl poate aplica. Instantaneul detaliilor (21 de vederi, doar penițele HATCH) și pachetul din magazin (1.1.0, 35 de reguli, captură nouă) sunt rescrise;
- **plăcile de pardoseală** (`RoomFinishes.HatchOf`) iau, în proiectele noi, suprafața materialului din standardul proiectului (grafica de fațadă a materialului, altfel regula standardului, cu scara și unghiul ei), în loc de tabelul propriu; proiectele vechi păstrează tabelul. Diferență față de tabel: plăcile ceramice („Ceramic tiles”) iau `TILE_300X300` (regula generală a plăcilor), nu 600×600;
- rămase: interfața (Manage → Material graphics sets, coloanele subcategoriilor, câmpul în View styles și pe vedere), „Aplică Lapin 1.1” pe un proiect vechi fără import (oferirea unei versiuni noi), grafica subcategoriei pe membrane și pe benzile fără material, legendele cu setul vederii plasate;
- **pentru utilizator:** schema manifestului din magazin (`../lapin-extensions`, nu se editează de aici) are nevoie de câmpurile opționale `country` și `norm`; până atunci țara și norma stau în etichete și în descriere.

### Etapa 5: reguli (filtre) care schimbă hașura și umplerea

Filtrele vederii primesc coloanele **Cut hatch** și **Cut fill**, plus semiton pe hașură. Exemple:

- „pereții portanți au tăietura plină neagră”;
- „pereții cu `FireRating` ≥ EI 60 sunt hașurați roșu”;
- „ce se demolează e punctat galben” (complementar fazelor).

Același resolver, deci ajung în PDF, DXF și planșe.

### Etapa 6: mutată

Prim-planul, fundalul și culoarea hașurii fac acum parte din etapa 0b, cu prioritate.

### Etapa 7: modele de hașură desenate

- **Hașură din desen:** desenezi un motiv (linii, arce, puncte) într-o celulă dreptunghiulară, în editorul de simboluri, apoi „Salvează ca hașură”. Pentru linii drepte se scrie ca `.pat`. Pentru arce sau motive pe care `.pat` nu le poate spune, Lapin repetă celula („tile pattern”: un nou fel de model, cu exportul DXF ca linii explodate).
- **Editor de familii de linii** cu previzualizare la scara aleasă: unghi, origine, deplasare, distanță, liniuțe. E ce face un `.pat`, dar fără să scrii text.
- „Hașurile mele” trec în folderele firmei (biblioteca unificată, etapa 5 din `plan-user-extensibility.md`).

### Etapa 8: ghidul, video-urile, răspunsul

- Pagina nouă din ghid, „Grafica materialelor” (EN și RO): standardul, materialul, treptele de scară, seturile pe stil, filtrele, desenarea unui model. Scenele ei sunt detaliile din etapa 0.
Stare (18.11.2026, decizia 672): **motorul e făcut; interfața urmează.** Abateri și precizări:

- `byScale` stă pe fiecare fel de vedere al materialului (`section.byScale`, `plan.byScale`, `elevation.byScale`): o listă ordonată de trepte `{ from?, to?, …orice câmp al etapelor 0b și 1 }`; `from` și `to` sunt numitorii scării, ambii incluși (`from: 100` = 1:100 și mai mic; `to: 50` = 1:50 și mai mare), cel puțin unul dat, `from ≤ to`; prima treaptă care conține scara vederii își pune câmpurile peste ale materialului, câmp cu câmp (și în `cutLine`, `separatorLine`, `backgroundHatch`). Un plan ia treapta secțiunii, apoi câmpurile lui, apoi treapta lui. Fără scară (IFC, Bonsai, `material.list`) nu se ia nicio treaptă;
- `material.update` verifică treptele ca pe câmpurile 0b (cel mult 20; `null` sau `[]` le scoate, la fel `reset: ["byScale"]`); într-o treaptă, `hatch: null` și `fill: null` înseamnă „niciuna”, alt `null` e lăsat afară; `material.preview` le arată la `scale`, iar `resolved.step` spune treapta luată; `material.list` dă `byScale` pe fiecare fel de vedere;
- `smallScaleCut` pe stilul de vedere: `{ mode: asMaterial | solid | outline | coreOnly, from: 200, color? }`, scris doar când e setat (Î5: implicit nimic); o vedere îl suprascrie câmp cu câmp cu `view.setStyle`, `null` îl readuce pe al stilului. Se aplică doar benzilor tăiate ale pereților, plăcilor, acoperișurilor, stâlpilor, grinzilor, barelor, plăcuțelor (`IfcPlate`), fundațiilor, piloților și coșurilor (sticla unei ferestre își păstrează aspectul), doar în tăieturi (nu pe fețele văzute ale fațadelor);
- liniile, pe moduri: `solid` umple elementul ca o singură zonă (benzile unite ca la detaliul grosier, `MaterialDrawing.Apply(mergeOwn)`, fără linii între straturi, membranele umplute), benzile de aceeași culoare se unesc și între elemente; `outline` fără hașură și fără umplere, doar conturul elementului (benzile unite; elementele de același tip se unesc, un stâlp într-un perete își păstrează conturul); `coreOnly` umple miezul (stratul marcat `core`, altfel cel mai gros; un element fără straturi e tot miez), restul gol, toate liniile și membranele păstrate;
- culoarea pentru `solid` și `coreOnly`: `color`, altfel negru; culoarea de umplere a categoriei din Visibility/Graphics câștigă peste ea, ca peste orice material;
- o singură ordine, într-o singură clasă (`ViewMaterialGraphics`, Lapin.Platform), de la cea mai puternică: suprascrierea pe element (cârlig) > filtrele vederii (etapa 5, cârlig) > setul de grafică al stilului (etapa 4, cârlig) > `smallScaleCut` > treapta materialului > materialul > standardul / ghicirea după nume. Culoarea și hașura categoriei din Visibility/Graphics rămân peste toate, la penițe, ca azi;
- se aplică în planuri, secțiuni, fațade (hașurile suprafețelor, cu treptele `elevation`), planșe (fiecare viewport la scara vederii lui), PDF, DXF, legende (la scara vederii plasate; legenda arată materialul cu treapta lui, fără `smallScaleCut`), capetele secțiunii 3D (treapta, la scara lor) și previzualizare;
- rămase: previzualizarea nu arată `smallScaleCut`; tabelul straturilor (`TypeBuildUps`) și drumurile civile (`Corridors`) iau materialul fără treaptă; interfața (treptele în `MaterialGraphicsTable`, câmpul în View styles) și tutorialul video.

- Un tutorial video „Grafica materialelor: standardul, culorile, grosimile, fundalul”, universal, care înlocuiește ce arătau video-urile despre valorile implicite.
- Nu îi cerem nimic celui care a cerut (Î1); îmbunătățirile vin în notele versiunilor, ca de obicei.

## 4. Răspunsurile utilizatorului (17.11.2026)

- **Î1** (cerem o captură celui care a cerut?): **nu**. „Vreau doar să îmbunătățim și să facem aceste feature-uri cât mai flexibile.”
- **Î2** (lista detaliilor de referință): **cum e propusă**. Ideea principală e un configurator de hașuri și materiale cât mai flexibil: culori, grosimi de linie, cu sau fără fundal plin. De aici etapa 0b, adusă în față.
- **Î6** (norma românească): **SR ISO 128-50**.
- **Î8** (standardul ales automat după limbă sau țară?): **nu**. Un singur implicit, cât mai plăcut estetic și recunoscut peste tot; celelalte standarde se aleg de mână.
- **Standarde personalizate și din magazin** (adăugat de utilizator): un standard se poate personaliza, încărca din altă țară, face ca pachet și descărca din magazin (etapa 3).
- **Î11** (prim-plan și fundal acum?): rezultă din Î2: **da**, în etapa 0b.
- **Î13** (deschis sau premium?): **totul în ediția deschisă**.

Fără răspuns, luate după propunere (se pot schimba oricând înainte de etapa lor):

- **Î3:** cărămida implicită la mărime reală 240×63; 240×71 și 215×65 sunt modele separate.
- **Î4:** o hașură la mărime reală mai deasă decât 0,7 mm pe hârtie nu se mai desenează (fațada rămâne curată la 1:200); pragul se poate seta pe stilul de vedere.
- **Î5:** „Tăieturi la scară mică” e implicit nimic (aspectul de azi); doar opțiunea, cu pragurile alese de utilizator.
- **Î7:** livrăm Lapin, ISO 128-50, DIN 1356, România (SR ISO 128-50) și BS; restul prin magazin.
- **Î9:** aplicarea unui standard pe un proiect existent lasă neatinse materialele cu grafică proprie; o bifă le suprascrie.
- **Î10:** stilurile Structural și MEP primesc un set de grafică doar în proiectele noi; vederile vechi rămân cum sunt.
- **Î12:** hașurile desenate care nu încap într-un `.pat` se repetă ca celulă și ajung în DXF ca linii explodate.

Ordinea: 0 → 0b → 1 → 2 → 3 → 4 → 5 → 7 → 8.

## 5. Ce se schimbă unde (pe scurt)

| Etapă | Motor | Interfață | Teste |
|---|---|---|---|
| 0 | detaliile de referință ca fixtures, serializarea pens | — | `MaterialGraphicsReferenceTests`, scenele ghidului |
| 0b | `ViewGraphics`: `hatchColor`, `hatchWeight`, `hatchLineType`, `backgroundHatch`, `cutLine`, `separatorLine`, opacitate; `Pens`, IFC, DXF, Bonsai | `MaterialGraphicsTable` lărgit, previzualizarea motorului la scară, copierea pe mai multe materiale | `MaterialGraphicsFlexTests`, `SolidHatchColourTests` lărgit |
| 1 | `HatchPattern.Units`, `Pat` (`%TYPE`), `HatchOptions` după unități, `hatchOrigin`, `MaterialHatchUpgrade` rev 3 | coloana Units, gripul originii | `ModelHatchTests` |
| 2 | `byScale` în `MaterialGraphics.Resolve(kind, scale)`, `smallScaleCut` în `ViewStyles` | treptele în `MaterialGraphicsTable`, câmpul în View styles | `HatchByScaleTests` |
| 3 | `MaterialStandard` (tabelul de reguli), secțiunea `materialGraphics` din `.lapinstandards`, `standards.applyMaterials`, `standards.fromProject`, previzualizarea pe detaliile etapei 0 | editorul tabelului, alegerea la proiect nou, duplicare, export/import, Catalogul → Standarde de desen, Publică în magazin | testele de ghicire de azi + `MaterialStandardTests` |
| 4 | `materialGraphicsSets`, `sub` cu grafică în `VisibilityGraphics` | Manage → Material graphics sets, coloane la subcategorii | `VisibilityGraphicsTests` |
| 5 | coloanele filtrelor | tabul Filters | `VisibilityGraphicsTests` |
| 7 | „tile pattern”, `drafting.toPattern` | editorul de familii, Save as hatch | `PatternEditorTests` |
