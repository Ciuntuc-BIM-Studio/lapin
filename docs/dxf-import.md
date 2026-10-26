# Importul DXF

Un DXF intră într-o vedere (plan, secțiune, fațadă) ca desen nativ Lapin: straturi de desen, linii, polilinii, cercuri, arce, hașuri, texte și cote, editabile ca orice formă desenată în Lapin. Nu rămâne nimic „imagine” sau „referință”. Comanda este `drafting.importDxf`, rezumatul dinainte de import `drafting.dxfInfo`; în aplicație: File → Import DXF…, sau butonul Import DXF din tab-ul Draft. Importul e un singur pas de undo.

## Ce devine ce

| DXF | Lapin |
|---|---|
| tabela LAYER (culoare ACI sau true color, grosime, tip de linie, înghețat/stins, blocat) | straturi de desen ale proiectului (`draftLayers`): culoare CSS, peniță în mm, `continuous`/`dashed`/`dotted`/`dashdot` după elementele LTYPE, vizibil, blocat. Un strat Lapin cu același nume rămâne cum era. |
| LINE | linie |
| LWPOLYLINE, POLYLINE (2D) | polilinie (închisă sau nu); segmentele cu bulge devin arce, iar runde drepte dintre ele polilinii |
| CIRCLE, ARC | cerc, arc |
| ELLIPSE, SPLINE (NURBS evaluat de Boor; punctele fit când nu are control points) | polilinie |
| TEXT, MTEXT (formatarea `\P \f \H {}` scoasă, `%%d %%p %%c \U+XXXX` traduse; fiecare paragraf MTEXT un text) | text cu înălțimea pe hârtie (înălțimea din desen ÷ scara vederii), aliniat după punctul de ancorare, mereu citibil |
| HATCH | hașură Lapin pe contururile tesselate (linii, arce, elipse, spline, polilinii cu bulge; text boxes sărite). SOLID → `SOLID`. Dacă fișierul poartă definiția liniilor pattern-ului (53/43–46/49), aceasta se normalizează (scara și unghiul hașurii scoase, offsetul adus pe direcția liniei, ca în `.pat`) și se înregistrează o singură dată în proiect sub numele din DXF (`ANSI31`, …; `-2` la coliziune cu altă definiție; `-M` dacă vederea o oglindește și pattern-ul nu e simetric). Fără definiție: `ANSI31→LINES45`, `ANSI37→CROSS`, `AR-CONC→CONCRETE`, `INSUL→BATT`, … altfel `LINES45`. |
| SOLID, TRACE | hașură SOLID |
| INSERT (+ ATTRIB) | blocul explodat: translație, rotație, scară (și negativă = oglindire), coloane/rânduri; entitățile de pe stratul `0` iau stratul insertului; culoarea BYBLOCK ia culoarea insertului; atributele vizibile devin texte |
| DIMENSION liniară (rotated) și aligned | cotă Lapin (`a`, `b`, `at`, `dir`), nu asociată elementelor; celelalte feluri (și toate, dacă se cere „geometry”) aduc liniile și textele din blocul `*D` |
| LEADER | polilinie deschisă |
| POINT, IMAGE, WIPEOUT, 3DFACE, REGION, MLEADER, paper space… | lăsate afară și numărate în rezultat |

Culoarea unei entități (ACI sau true color) devine culoarea proprie a formei; BYLAYER înseamnă culoarea stratului. Tipul de linie și grosimea pe entitate nu se păstrează (sunt ale stratului).

## Unități, poziție, rotație

- `units`: `auto` (implicit), `mm`, `cm`, `m`, `in`, `ft` sau factorul numeric (metri pe unitate). `auto` ia `$INSUNITS`; dacă lipsește, ghicește din întindere (peste 2000 de unități → mm, peste 300 → cm, altfel m). Dacă fișierul declară o unitate care ar da un desen sub 1 m (ArchiCAD scrie metri sub `$INSUNITS = 4`), se ia ghicirea și rezultatul spune `unitsGuessed`.
- `at` [x, y] în metri în vedere: unde ajunge originea DXF-ului (implicit 0, 0). `rotation` în grade. `scale` factor suplimentar.
- `layers`: lista straturilor DXF de adus (implicit toate). `layerPrefix`: prefix pentru numele straturilor create. `texts`, `hatches` (implicit true). `dimensions`: `native` (implicit), `geometry`, `skip`.

## Secțiuni și fațade: planul DXF pe planul de tăiere

Formele desenate pe o secțiune sau fațadă trăiesc în coordonatele vederii: `u` de-a lungul liniei de secțiune (de la start la end), `v` = cota. Un DXF desenat în planul x–y se proiectează pe acest plan vertical: x → u, y → v, deci `y = 0` din DXF cade pe ±0.00.

Convenția de semn: o secțiune sau fațadă care privește spre +X sau +Y (componenta dominantă a direcției de privire pozitivă) primește desenul așa cum e. Una care privește spre −X sau −Y primește desenul oglindit după axa y a DXF-ului (x → −x), ca ceea ce fișierul a desenat la stânga să rămână la stânga privitorului. O secțiune privește la stânga liniei ei (desenată de la start la end); fațada de sud privește spre +Y, cea de nord spre −Y, cea de vest spre +X, cea de est spre −X. Regula se poate suprascrie cu `flip: true/false`; dialogul o arată („by the view: mirrored / as drawn”). Oglindirea păstrează punctele arcelor (li se schimbă capetele), hașurile (familiile oglindite) și lasă textele citibile.

## Join

`drafting.join` (în linia de comandă `JOIN` sau `J`, și butonul Join din grupul Edit) sudează liniile și poliliniile deschise selectate, cap la cap, în polilinii: capetele aflate la mai puțin de `tolerance` (implicit 0,5 mm) se ating; lanțul se oprește acolo unde se întâlnesc trei sau mai multe bucăți (nu se ghicește) și se închide când revine la început. Polilinia rămâne pe entitatea primei bucăți (stratul și culoarea ei); celelalte dispar. Arcele și cercurile nu intră (polilinia Lapin are doar segmente drepte). Dacă o polilinie e închisă sau nu se vede și se schimbă din panoul de proprietăți (Closed).

## Testat pe

`plan fundatii.dxf` (ArchiCAD, AC1021, 4 MB, 4127 entități: 2818 polilinii, 1014 hașuri, 285 texte, 3 blocuri; declară mm, e în metri): 185 ms importul, 10 straturi, desenul în plan corect (`DxfImportSvgTests`, cu `LAPIN_DXF_SAMPLE` și `LAPIN_DXF_SVG_DIR`).

## Rămase

- DIMENSION unghiulare, radiale, ordonate ca cote native (acum: geometria din blocul lor).
- Tipul de linie și grosimea pe entitate (acum ale stratului).
- MLEADER, IMAGE/WIPEOUT, XLINE/RAY.
- Plasarea prin clic în vedere (acum: punctul `at` din dialog).
- Polilinii cu arce ca o singură entitate (ar cere bulge în polilinia Lapin).
