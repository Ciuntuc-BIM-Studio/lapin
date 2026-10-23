# Transferul logicii Lapin din fork-ul ifc-lite în lapin-pro (23.10.2026)

Ce trece din Lapin-ul de azi (fork ifc-lite, `Ciuntuc-BIM-Studio/lapin`, backup în `~/Documents/GitHub/_backups/lapin-2026-10-23/`) în lapin-pro: configuratoarele, logica de straturi și tipuri, desenul 2D (secțiuni, linii ascunse, adâncimea de vedere, ce se vede deasupra planului de tăiere, tipuri și grosimi de linii), materialele, standardele, fazele și formatul de proiect.

Căile din acest document se citesc în fork: `C` = `packages/create/src/in-store`, `V` = `apps/viewer/src`.

## Reguli de transfer

1. **Cod al nostru (OWN).** Fișierele create în fork, listate în `Lapin/docs/lapin-own-files.txt`. Le portăm în lapin-pro și le putem relicenția MIT. Se rescriu în C#, ca module ale motorului, sau în TypeScript nou, pentru interfață; logica și numerele se păstrează.
2. **Cod ifc-lite, modificat sau nu (MOD/UP), sub MPL-2.0.** **Nu se copiază.** Se rescrie după comportamentul descris în acest document și după testele de azi, folosite ca specificație. Unde BubbleBIM Pro are deja echivalentul, pornim de la el.
3. **Testele de azi sunt specificația.** Fiecare zonă de mai jos listează testele care fixează comportamentul. În lapin-pro devin teste xUnit pentru motor sau vitest pentru interfață, cu aceleași cazuri și aceleași numere.
4. **Fără ifc-lite în lapin-pro.** Nicio dependență și niciun fișier. Verificarea de licențe (`pnpm check:licenses`) rămâne obligatorie.

## Harta transferului

| Domeniu | Sursa în fork | Tip | Unde ajunge în lapin-pro | Teste-specificație |
|---|---|---|---|---|
| Catalogul de tipuri (9 tipuri), câmpuri, valori implicite, citire tolerantă | `V/element-types/*`, `C/catalog-type.ts` | OWN | Motor: `Lapin.Authoring.Types` (scrierea tipului în IFC); UI: modulul fiecărui tip (dialogul Element types) | `catalog-type.test.ts`, `removed-type.test.ts`, `grid-column-type.test.ts` |
| Straturi: layer set, usage, miez, axa pe miez, flip | `C/foundation-spec.ts` (`coreIndex`, `stemSection`), `C/layer-usage.ts`, `C/element-flip.ts`, `V/lib/wall-core-axis.ts` | OWN | Motor: `Lapin.Authoring.Layers` | `element-flip.e2e.test.ts`, `foundation.e2e.test.ts` |
| Îmbinări de pereți: L, T, cap la cap, tăieturi, prioritate | `C/wall-join*.ts` | **MOD (ifc-lite)** | Motor: rescris după specificația de mai jos; BubbleBIM are `Joins.cs` pe topologie, dar pentru alt model | `wall-join*.test.ts` (folosite doar ca specificație) |
| Mitrul, preferința de îmbinare, îmbinări liniare | `C/wall-join-mitre-cut.ts`, `V/lib/wall-join-style.ts`, `linear-join*` | OWN | Motor: `Lapin.Authoring.Walls` | `wall-join-mitre.test.ts`, `linear-join.e2e.test.ts` |
| Zone pe înălțime, straturi de finisaj ca piese, cantități | `C/wall-zones*.ts`, `C/wall-parts-frame.ts` | OWN | Motor: `Lapin.Authoring.Walls.Zones` | `wall-zones.test.ts`, `wall-zones.e2e.test.ts` |
| Buiandrugi (reguli, produse, bucăți, armare, centura coborâtă) | `C/wall-lintels*.ts`, `V/joinery/lintel-*` | OWN | Motor: `Lapin.Authoring.Lintels`; UI: câmpurile din configurator, tabelul de produse | `wall-lintels.test.ts`, `lintel-products.test.ts`, `lintel-schedule.test.ts` |
| Fundații continue și izolate | `C/foundation-*.ts`, `C/strip-*.ts`, `C/pad-footing.ts` | OWN | Motor: `Lapin.Authoring.Foundations` (modulul Structure) | `foundation.e2e.test.ts`, `strip-foundation-editing.test.ts`, `foundation-place.test.ts` |
| Timber frame, ferme, CLT | `C/timber-*.ts`, `C/truss*.ts`, `V/project/clt.ts` | OWN | Motor: un modul Timber, ulterior | `timber-frame*.test.ts`, `truss*.test.ts`, `clt.test.ts` |
| Sisteme de acoperiș (straight skeleton ponderat, structură, pereți de fronton, tăierea pereților) | `C/roof-*.ts`, `C/straight-skeleton.ts`, `C/wall-roof-trim.ts` | OWN | Motor: `Lapin.Authoring.Roofs`; BubbleBIM are deja un `WeightedSkeleton` | `roof-system*.test.ts`, `straight-skeleton.test.ts`, `wall-roof-trim*.test.ts` |
| Priorități de tăiere | `C/cut-priority.ts` | OWN | Motor: `Lapin.Authoring.Cuts` | `cut-priority.e2e.test.ts` |
| Configuratorul de uși și ferestre (spec, geometrie, IFC, simboluri, mapare, plasare) | `C/joinery-*.ts`, `V/joinery/*` | OWN | Motor: `Lapin.Authoring.Joinery`; UI: modulul Architecture (configuratorul) | `joinery*.test.ts`, `symbols.test.ts`, `symbol-mapping.test.ts`, `spec-file.test.ts`, `schedule-*.test.ts` |
| Materiale, aspect IFC, hașuri `.pat`, texturi | `V/project/materials.ts`, `material-*.ts`, `C/material-appearance.ts`, `V/drafting/hatch/*` | OWN | Motor: aspectul IFC al materialelor; UI: dialogul Materials, biblioteca de hașuri | `material-appearance.test.ts`, `materials-views.test.ts`, `hatch/*.test.ts` |
| Standarde de desen (text, cote, axe, etichete, layere, grupuri) | `V/drafting/styles.ts`, `V/project/drafting-standards.ts`, `standards-file.ts` | OWN | UI: modulul Drafting; motor: adnotările ca date ale vederii | `styles.test.ts`, `standards-file.test.ts` |
| Faze | `V/phases/*` | OWN | Motor: faza pe element; UI: modulul Phases | `phase-model.test.ts` |
| Generatorul de desen: secțiune, proiecție, benzi, linii ascunse | `packages/drawing-2d/*` | **UP/MOD (ifc-lite)** | Motor: pornind de la `HiddenLines` și `Projection.cs` din BubbleBIM, după specificația de mai jos | testele ifc-lite (doar ca specificație) |
| Domeniul vederii (top / bottom / depth / beyond), adâncimea, liniile ascunse pe tip de vedere | `V/project/view-range.ts`, `view-defaults.ts`, `view-hidden-lines.ts`, `view-plane*.ts`, `view-drawings.ts` | OWN | Motor: parametrii vederii; UI: proprietățile vederii | `view-range.test.ts`, `view-defaults.test.ts`, `view-hidden-lines.test.ts`, `view-plane.test.ts`, `view-drawings.test.ts` |
| Tabelul de penițe, stiluri de obiect, tipuri de linii, editări de linii | `V/project/line-styles.ts`, `linework.ts`, `view-linework.ts` | OWN | Motor + UI: modulul Drafting | `line-styles.test.ts`, `linework.test.ts` |
| Grafica vederii (categorii, clase ascunse, layere, seturi de grafică) | `V/project/view-graphics.ts`, `graphics-sets.ts` | OWN, dar sprijinit pe motorul de suprascrieri ifc-lite | UI + motor: rescris fără `graphic-overrides` din ifc-lite | `view-graphics.test.ts`, `graphics-sets.test.ts` |
| Desenul pe materiale (benzi, îmbinarea straturilor, membrane, seams) | `V/project/material-drawing.ts`, `material-resolver.ts` | OWN | Motor: `Lapin.Drawing.Materials` | `material-drawing.test.ts` |
| Simboluri de goluri, acoperiș în plan, marcaje de secțiune, axe, cote asociative | `V/drafting/opening-symbols.ts`, `V/project/roof-plan.ts`, `section-marks.ts`, `view-grids.ts`, `associative-dims.ts`, `drafting/annotation.ts` | OWN | Motor: `Lapin.Drawing.Symbols`; UI: suprapunerile din vedere | `opening-symbols.test.ts`, `associative-dims.test.ts` |
| Planșe, viewport-uri, penițe pe planșă, DXF R2000, PDF | `V/project/sheets.ts`, `V/components/viewer/sheets/*` | OWN | Motor: DXF și PDF (BubbleBIM are `Dxf.cs`, `Pdf.cs`); UI: editorul de planșe | `sheets*.test.ts`, `dxf-r2000.test.ts`, `viewport-regions.test.ts` |
| Pachetul `.lapin` și documentul de proiect | `V/project/types.ts`, `V/project/package/*`, `project-file*.ts` | OWN | Motor: deschidere și salvare; formatul se păstrează (citește și `.lapin` vechi) | `package-io.test.ts`, `project-file*.test.ts`, `versions.test.ts` |
| **MEP rezidențial** (branch `feature/residential-mep`, 54 de commit-uri, nu e în `lapin`) | vezi secțiunea MEP de mai jos | OWN (fișierele noi); 33 de fișiere ifc-lite atinse doar pentru legături | Motor: `Lapin.Authoring.Mep`; UI: modulul `mep` (tab MEP, dialoguri, bare de opțiuni) | testele de pe branch: `*-mep*`, `hydronic-*`, `heating-*`, `water-*`, `drainage-*`, `ventilation-*`, `electrical-*`, `route-*` |

## MEP rezidențial

Branch-ul `feature/residential-mep` (ultimul commit `8b4835045`, 23.10.2026) e pe GitHub și în backup, și n-a fost unit în `lapin`. Are 258 de fișiere și circa 22.000 de linii. Documentul de proiectare e `docs/architecture/residential-mep.md` de pe branch și se portează ca specificație.

Ce conține:

- **Rețeaua comună:** sistem, echipament, terminal, port, rețea, traseu, piese generate (`apps/viewer/src/mep/*`, `C/mep-equipment-*`, `C/hydronic-connectivity.ts`).
- **Traseul parametric:** polilinie 2,5D/3D cu stații, cote, pantă, rază minimă de curbură, dimensiuni nominale, plan de montaj (pardoseală 0,2 m, tavan 2,4 m, perete 1,1 m) și piese generate (coturi, teuri, reducții, mufe) (`C/route-*`, `C/hydronic-route-*`).
- **Încălzire:** cazan, propuneri de radiatoare (suprafață × W/m², afișată mereu ca estimare), trasee tur/retur pereche, sugestia de traseu principal minim, izometria și profilul traseului.
- **Apă:** tronsoane, armături în linie (robinet, contor), necesar și pierderi de sarcină, tabel.
- **Canalizare:** coloane, ramuri, ventilații, curățitoare, ieșiri, cu păstrarea pantei.
- **Electric:** tablou, circuite, prize, lumini, întrerupătoare, cabluri, tabel.
- **Ventilație:** ventilator, tubulatură circulară și rectangulară, guri de refulare, ramuri, tabel.
- **Planul:** benzi pe discipline, etichete, porturi neconectate, export SVG, DXF, PDF, XLSX.
- **IFC:** IfcBoiler, IfcSpaceHeater, IfcPipeSegment și IfcPipeFitting, IfcValve, IfcFlowMeter, IfcSanitaryTerminal, IfcElectricDistributionBoard, IfcOutlet și restul, cu porturi de distribuție, relații de sistem și `Pset_LapinMEP*`.

Toate fișierele noi sunt create de noi (OWN). Cele 33 de fișiere ifc-lite modificate pe branch (panglica, planul, snap-ul, comenzile, i18n) sunt doar puncte de legătură și nu se portează. În lapin-pro le înlocuiesc contractul de modul și protocolul motorului.

## Specificația părților ifc-lite (pentru rescriere)

### Îmbinări de pereți (`wall-join*.ts`)

- **Peretele:** `{start, end, thickness, alignment center|left|right, offset (+ la stânga), startCut?, endCut?}`. O tăietură de capăt e `{left, right}`: cât trece fiecare față de capătul axei.
- **Îmbinarea:** `{kind L|T|butt, style butt|mitre, point, relating a|b, a, b: {connection ATSTART|ATEND|ATPATH, runsThrough}}`.
- **Opțiuni:** toleranța, implicit de 2 × grosimea mai mare; prioritatea, implicit peretele mai gros, iar la egalitate `a`.
- **L:** peretele prioritar trece până la fața exterioară, celălalt se oprește la fața lui interioară.
- **T:** peretele traversat rămâne neschimbat, cel care se termină se oprește la fața apropiată.
- **Cap la cap:** pereții coliniari se opresc drept în punctul comun.
- **Capetele de axă** îmbinate se aduc exact în punctul de intersecție.
- **Refuzuri:** pereți care se încrucișează (trebuie tăiați întâi), paraleli necoliniari sau care nu se întâlnesc în toleranță.
- **Corpul peretelui:** o tăietură dreaptă păstrează profilul dreptunghiular, una oblică dă un profil cu patru puncte. Nu se folosesc booleene.
- **Mitrul** (doar la L): tăietura merge pe diagonala de la colțul interior la cel exterior, iar niciun perete nu trece prin celălalt. Se scrie în `IfcRelConnectsPathElements` cu Description `Mitre`.

### Generatorul de desen (`packages/drawing-2d`)

- **Tăietura:** fiecare triunghi tăiat de plan dă un segment. Segmentele se grupează în poligoane închise pe element și pe material (cheia culoare + material), deci fiecare strat devine propriul poligon.
- **Proiecția pe benzi:** adâncimea semnată e `d = ±(c − poziția planului)`.
  - **Vizibil**, între `−below` și `0`: linie subțire continuă.
  - **Deasupra (overhead)**, între `0` și `above`: linie întreruptă.
  - Un element care traversează planul se desenează continuu.
  - Ce iese din ambele benzi nu se desenează.
  - Golurile (familia `IfcOpeningElement`) nu se proiectează niciodată.
- **Benzile implicite:**
  - plan restrâns la nivelul lui: `below` = tăietura − cota nivelului, `above` = cota nivelului următor − tăietura;
  - secțiuni și fațade: `below` = 0,5 × extinderea modelului, `above` = 1 mm;
  - adâncime explicită: `below` = adâncimea, cu tăiere exactă a benzilor; adâncime 0 înseamnă doar tăietura.
- **Liniile ascunse:** raster de adâncime cu 1024 px pe latura lungă, 10 eșantioane pe linie, bias 0,001. Doar liniile vizibile pot deveni ascunse.
  - Rescrierea recomandată: eliminarea vectorială exactă din BubbleBIM (`HiddenLines.Draw`), care e mai precisă.
- **Linii parazite:** se elimină liniile de proiecție mai lungi de 1,5 × max(diagonala zonei tăiate, diagonala modelului proiectat).
- **Unirea liniilor coliniare** la final.
- **Grosimi pe ecran (mm):**

  | Linie | Grosime | Culoare | Întrerupere |
  |---|---|---|---|
  | contur tăiat | 0,5 | — | — |
  | proiecție | 0,25 | — | — |
  | siluetă | 0,35 | — | — |
  | muchie | 0,18 | — | — |
  | adnotare | 0,13 | — | — |
  | ascunsă | 0,18 | `#666` | [2, 1] |
  | deasupra | 0,7 × normal | `#888` | [2, 1] |
  | dincolo (beyond) | 0,13 | `#9ca3af` | — |

- **Suprascrieri grafice:**
  - grosimi heavy 0,5, medium 0,35, light 0,25, hairline 0,18;
  - întreruperi: dashed [2, 1], dotted [0,5, 0,5], dashdot [3, 1, 0,5, 1], center [6, 1, 1, 1];
  - regulile se aplică în ordinea priorității;
  - presetări: Default (culorile materialelor), Architectural, Fire safety, Structural, MEP, Monochrome.

### Regulile Lapin de vedere (OWN, de portat ca atare)

- **Planul:** tăietura implicită e la 1,20 m.
  - Planul de fundații: tăietura la −0,50 m, adâncimea 2,50 m.
  - Nivelurile la mai puțin de 0,25 m unul de altul se unesc.
- **Domeniul vederii** (ca în Revit): Top, Bottom și Depth, fiecare legat de nivelul propriu, cel de deasupra, cel de dedesubt sau nelimitat (10.000 m), plus un offset.
  - Regula: Top ≥ tăietura ≥ Bottom ≥ Depth.
  - Banda primară: de la tăietură la Bottom, linie continuă.
  - Deasupra: până la Top, linie întreruptă (grinzile și marginile acoperișului de deasupra).
  - Dincolo: de la Bottom la Depth, gri, 0,13 mm.
- **Liniile ascunse:** afișate implicit în plan și secțiune, ascunse în fațadă. Ordinea de decizie: setarea vederii, apoi cea a proiectului, apoi implicitul.
- **Fațadele:** planul de vedere stă cu 5 cm în interiorul limitelor modelului și privește spre interior.
- **Penițele:** 16 penițe pe grosimi ISO, de la 0,1 la 2 mm.
  - La scările 1:20, 1:50, 1:100 și 1:200, indexul penițelor se decalează cu +2, +1, 0 și −1; de exemplu, penița 5 la 1:100 înseamnă 0,35 mm.
  - Întreruperi pe hârtie (mm): dashed [3, 1,5], hidden [1,5, 1], center [6, 1,5, 1, 1,5], dotted [0,3, 1].
  - Ordinea de decizie: element, categoria din vedere, stilul vechi pe categorie, stilul de obiect al proiectului.
- **Materialele în secțiune:**
  - hașura și umplerea vin, în ordine, din rândul propriu, din aspectul IFC, apoi din numele materialului;
  - un strat sub 2 mm e o membrană, desenată ca linie întreruptă;
  - benzile de același material se unesc, cu toleranța 2 mm și o suprapunere de cel puțin 50%.
- **Simbolurile de uși și ferestre în plan:**
  - Ușa: foaia deschisă la 90°, cu arcul ei, de grosime clamp(adâncime × 0,4, 0,02, 0,05). Peste 1,4 m are două foi.
  - Fereastra: cadrul, plus geamul dublu la ±min(0,012, adâncime / 3).
- **Planșele:** formate A0–A4, scări 1–1000, margine 10 mm, cartuș 180 × 50 mm.
  - Penițe pe planșă: tăiat 0,5, văzut 0,25, ascuns 0,18, hașură 0,13 mm.
  - Exportul DXF e R2000, la 1:1 în mm.

## Ce lipsește în BubbleBIM Pro și scriem noi

- Domeniul vederii cu Top, Bottom și Depth și banda „dincolo”.
- Tabelul de penițe, stilurile de obiect și suprascrierile pe element.
- Îmbinarea straturilor de material și membranele.
- Importul de hașuri `.pat`.
- Presetările de grafică și fazele.
- Simbolurile de uși și ferestre.
- Editările de linii.

BubbleBIM are deja, ca punct de plecare:
- eliminarea vectorială a liniilor ascunse și tăierea secțiunii (`Projection.cs`);
- tipurile de linii (tăiat, vizibil, subțire, ascuns, axă, secțiune);
- penițele PDF, planșele A4–A0, DXF-ul și cinci hașuri.

## Ordinea transferului

1. **M0b – modelul IFC și tipurile cu straturi:** layer set, usage, axa pe miez, flip, scrierea și rescrierea tipurilor.
2. **M2 – pereții:** îmbinări (rescrise), mitru, zone pe înălțime, straturi de finisaj, buiandrugi, priorități de tăiere, fundații.
3. **M3 – configuratorul de uși și ferestre:** specificație, geometrie, IFC, simboluri și mapare.
4. **M4 – materiale și standarde de desen:** aspect IFC, hașuri `.pat`, stiluri de text, cote, axe, etichete, layere.
5. **M5 – desenul 2D:**
   - generatorul, pe BubbleBIM `HiddenLines`, după specificația de mai sus;
   - domeniul vederii, penițele, desenul pe materiale, simbolurile, acoperișul în plan, marcajele de secțiune, axele, cotele.
6. **M6 – fazele, planșele, exportul DXF și PDF, pachetul `.lapin`** (care citește și proiectele vechi).
7. **M7 – MEP rezidențial:** rețeaua și traseul parametric, apoi încălzirea (felia verticală completă), apa, canalizarea, electricul și ventilația. Are nevoie de pereți, goluri, plan și planșe, deci vine după M5–M6.
8. **Ulterior:** acoperișuri, timber și CLT.

La fiecare pas, testele-specificație listate în hartă se portează înaintea codului.
