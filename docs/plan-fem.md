# Calcul structural (FEM) în Lapin: studiu

Cerut de utilizator (12.11.2026): să studiem cum ar putea Lapin să aibă și un motor de calcul cu elemente finite, ținând cont de ce există open source.

**Hotărât de utilizator (12.11.2026, „facem solver intern C# — începem cu S0”):** varianta C, un solver propriu în C#, MIT, în etape; exportul (JG Structures, IFC SAV) rămâne. Prima etapă e S0, specificată la §8. Întrebările 3–4 de la §7 rămân deschise.

**Hotărât de utilizator (20.11.2026, „punem pe free și S4 și S5”):** toate etapele, S0–S5, sunt în modulul deschis `analysis`, MIT, gratuite: și verificările după normă (S4), și P-Δ, reazemele elastice, legăturile doar la compresiune, etapele de execuție și neliniarul (S5). Nimic din calculul structural nu merge într-un depozit premium. Răspunde la întrebarea 2 de la §7 (decizia 742). S5 nu mai e „poate niciodată”: se face cu cod propriu, ca restul (fără xara, OpenSees sau Code_Aster ca dependențe; acestea rămân doar verificări la dezvoltare).

## 1. Ce avem deja

Partea grea a unui calcul integrat în BIM nu e solverul. E **modelul analitic**: să faci din pereți, grinzi și plăci un model de bare și plăci cu noduri comune, reazeme și excentricități. Lapin îl are deja:

- **`Lapin.Platform/Analysis`** (deciziile 357, 362; `docs/jg-structures.md`): `AnalyticalModel.Build` derivă din modelul fizic barele pe axe, plăcile pe planul median al miezului, nodurile (1 cm), reazemele de pe fundații, legăturile excentrice ca `end_offsets`, plăcile tăiate în bucăți convexe fără goluri (`ShellPieces`), pereții sub acoperiș (`WallTops`).
- **`SectionProperties`**: A, I, W, Wpl, J și axele principale din geometrie (IPE 300 la 1 % de tabel).
- **`StructuralMaterials`**: EN 1992-1-1, EN 1993-1-1, EN 338 / EN 14080, cu `null` unde nu știm.
- **Exportul JG Structures** (JSON, validat cu schemă). Rezultatele încă nu se citesc înapoi.
- **Un experiment în BubbleBIM-Standalone** (`src/lib/fem/`, ~1.200 de linii TS, privat): model de bare și plăci cu 3 noduri pe solverul **Awatif** (MIT), greutatea proprie, încărcări utile pe camere după SR EN 1991-1-1, vizualizare deformată. Lecții de acolo: reacțiile trebuie calculate ca R = K·U − F (funcția lui Awatif pierde contribuția plăcilor); orice placă fără reazem face matricea singulară.

**Ce lipsește pentru un calcul:** încărcările (cazuri, grupe, combinații după EN 1990 / CR 0), discretizarea plăcilor în elemente, eliberările la capete, diafragmele, solverul, rezultatele și afișarea lor, apoi verificările după normă (care sunt altceva decât FEM).

## 2. Ce există open source

Licențele verificate pe 12.11.2026 (GitHub API și fișierele LICENSE). Regula lui Lapin: dependențele de producție sunt permisive (`pnpm check:licenses`).

| Proiect | Licență | Limbaj | Ce face | Pentru Lapin |
|---|---|---|---|---|
| **OpenSees** (UC Berkeley) | proprie UC: gratuit doar necomercial sau intern; comercial cu licență de la UC | C++ / Tcl / Python | referința în neliniar și seismic | **Exclus ca dependență.** Bun doar ca verificare, la dezvoltare |
| **xara / OpenSeesRT** (STAIRlab, Berkeley) | BSD-2-Clause | C++ / Python | neliniar, pe arhitectura OpenSees | Permisiv pe hârtie; de verificat cât cod vine din OpenSees înainte de orice folosire |
| **Kratos Multiphysics** (CIMNE) | BSD cu clauza de publicitate („This product includes Kratos…”) | C++ / Python | multifizică, aplicație de mecanica structurilor | Permisiv, dar foarte mare, greu de pus în wasm, clauza de publicitate |
| **MFEM** (LLNL) | BSD-3-Clause | C++ | bibliotecă FEM generală, de nivel jos | Fără bare și plăci gata făcute; prea generală |
| **Code_Aster** (EDF) | GPL-3.0 | Fortran / C / Python | complet, industrial | Doar ca program extern instalat de utilizator |
| **CalculiX** | GPL-2.0 | C / Fortran | format Abaqus, folosit de FreeCAD FEM | Doar ca program extern instalat de utilizator |
| **Pynite** | MIT (784 ★, activ) | Python | cadre 3D, plăci quad, P-Δ, combinații | Referință excelentă; bun pentru verificarea solverului nostru la dezvoltare |
| **Awatif** | MIT | TypeScript | bare și plăci cu 3 noduri, liniar static, în browser | Ce a folosit BubbleBIM-Standalone; limitat, cu defectul reacțiilor |
| **BriefFiniteElement.NET** | LGPL-3.0 | C# | liniar static, bare și plăci | Cel mai apropiat tehnic, dar LGPL: nu trece regula de licențe |
| **anaStruct** | LGPL-3.0 | Python | cadre 2D | Prea puțin |
| **FEniCS (dolfinx)** | LGPL-3.0 | C++ / Python | EDP generale | Nepotrivit |

**Rezolvitoarele de sisteme rare** (inima oricărui solver): MathNet.Numerics (MIT, dar fără un rezolvitor direct rar bun), CSparse.NET (LGPL-2.1), SuiteSparse (LGPL/GPL pe părți), Eigen (MPL-2.0, C++), PARDISO din MKL (gratuit, proprietar). Niciunul nu e și permisiv, și C#, și bun. Un LDLᵀ / Cholesky rar cu reordonare (AMD sau RCM) scris de noi e ~1.000–2.000 de linii și merge și în browser.

**Formate de schimb** (pentru exportul către programele comerciale: SCIA, Robot, ETABS, AxisVM, Consteel):
- **IFC Structural Analysis View** (`IfcStructuralAnalysisModel`, `IfcStructuralCurveMember`, `IfcStructuralSurfaceMember`, `IfcStructuralPointConnection`, `IfcStructuralLoadGroup`, `IfcStructuralLoadCase`): în schema pe care Lapin o folosește deja. Modelul analitic a fost făcut să-l poată scrie (decizia 357).
- **SAF** (Structural Analysis Format, Nemetschek, specificație publică, Excel): citit de mai multe programe de calcul. De verificat termenii specificației înainte.

## 3. Variantele

**A. Doar export (azi).** JG Structures, apoi IFC SAV și eventual SAF. Ieftin, fără răspundere de calcul, Lapin rămâne modelatorul. Nu dă nimic utilizatorului care nu are un program de calcul.

**B. Punte către un program extern instalat de utilizator** (CalculiX, Code_Aster, OpenSees). Lapin scrie fișierul de intrare după formatul public, pornește programul, citește rezultatele. Licența e curată (programe separate, ca la FreeCAD). Dar utilizatorul trebuie să instaleze ceva, nu merge în browser, fiecare program are formatul și ciudățeniile lui, iar OpenSees are problema de licență și pentru utilizatorul care lucrează comercial.

**C. Un solver propriu în C#, MIT** (un modul nou, de pildă `structure-analysis`, `Lapin.StructuralAnalysis`). Scris după manuale și articole, fără cod luat din altă parte, verificat la dezvoltare față de Pynite, OpenSees sau CalculiX (cum folosim biblioteca That Open la Fragments: doar ca verificare). Merge pe desktop și **în browser**, pentru că motorul se compilează deja în wasm. Niciunul dintre solverele C++ nu dă asta ușor.

**D. Un solver C++ permisiv legat nativ** (xara, Kratos, MFEM), prin C API, ca manifold. Puternic, dar greu de construit pe trei platforme plus wasm, iar xara și Kratos au întrebările de licență de mai sus.

### Recomandarea: C, în etape, cu A păstrat

- **Domeniul liniar e bine delimitat.** Bara 3D (12 grade de libertate, Euler-Bernoulli sau Timoshenko, cu excentricități rigide), placa groasă sau subțire (quad MITC4 sau DKQ cu membrană), asamblarea, rezolvitorul rar, modurile proprii (iterație în subspațiu sau Lanczos). Toate sunt matematică de manual, cu soluții închise de comparat.
- **Respectă principiile lui Lapin:** formate și algoritmi după specificații publice, fără dependență; rezultatele **derivate la fiecare cerere**, nestocate (cel mult un cache în `derived/`, nume deja rezervat în `.lapin`); un singur model analitic pentru calcul, JG, IFC SAV și SAF.
- **Exportul rămâne:** proiectantul care calculează în SCIA sau ETABS primește modelul analitic. Solverul lui Lapin e pentru verificări rapide și predimensionare, nu înlocuiește un program de calcul autorizat.
- **Răspunderea:** ca la norme și prețuri, tot ce dă Lapin e marcat **orientativ**. Calculul de rezistență semnat rămâne al inginerului.

## 4. Etapele propuse (varianta C)

| Etapă | Ce | Observații |
|---|---|---|
| **S0. Încărcări și IFC SAV** | Cazuri de încărcare în model (`IfcStructuralLoadGroup` / `IfcStructuralLoadCase`): greutatea proprie derivată; încărcări pe suprafață din camere după categoria de utilizare (SR EN 1991-1-1, ca în BubbleBIM-Standalone); finisaje și pereți nestructurali ca încărcări liniare; zăpada (CR 1-1-3) și vântul (CR 1-1-4) mai târziu. Combinațiile SLU / SLS după CR 0-2012 / EN 1990, derivate. **Exportul IFC Structural Analysis View** cu încărcările. | Util și fără solver: oricine primește IFC SAV are modelul complet. |
| **S1. Static liniar, bare** | Bara 3D cu `end_offsets` și eliberări, rezolvitorul rar, reacțiile R = K·U − F, verificarea echilibrului; rezultatele: deplasări, N / V / M / T pe bare, reacții. Vizualizare: deformata, diagramele, valorile. Etichete pe bare. | Teste: grinda simplu rezemată, consola, cadrul portal, grinda continuă, cu soluții închise; cadrul spațial față de Pynite (dev). |
| **S2. Plăci** | Discretizarea bucăților convexe din `ShellPieces` în quaduri (noduri comune cu barele de pe margini), elementul de placă, momente și eforturi pe plăci, izolinii. Grinda în placă cu `offset_z`. | Teste: placa simplu rezemată și încastrată (Timoshenko), peretele în consolă; convergența la rafinare. |
| **S3. Analiză modală și seismică** | Masa din încărcări (combinația seismică), modurile proprii, spectrul de răspuns după **P100-1/2013** (și EN 1998-1), combinarea modurilor (SRSS / CQC), forțele seismice pe nivel. | Necesită S2 pentru pereții structurali. |
| **S4. Verificări după normă** | Beton (EN 1992-1-1: încovoiere, forfecare, armătura necesară, legată de modulul `rebar`), oțel (EN 1993-1-1: rezistența secțiunii, flambajul, flambajul lateral), lemn (EN 1995-1-1). | Deschis, MIT (decizia 742, 20.11.2026); aici e munca mare și răspunderea: rezultate orientative, verificate. |
| **S5. Mai târziu, poate niciodată** | P-Δ, reazeme elastice (sol), legături doar la compresiune, analiza în etape de execuție, neliniar. | Deschis, MIT, cod propriu (decizia 742, 20.11.2026); xara, OpenSees, Code_Aster doar ca verificări la dezvoltare. |

**Afișarea:** o vedere analitică (3D și plan) cu modelul analitic, încărcările, deformata și diagramele, prin rezolvitorul de vizibilitate (`VisibilityGraphics`), ca să le poată tipări și planșele. Rezultatele unui element apar în Proprietăți.

**Comenzile:** încărcările sunt comenzi (un pas de undo fiecare); calculul e o **interogare** (`analysis.run`), nu schimbă modelul. Pentru proiectele mari rulează în fundal, cu progres.

## 5. Mărime și riscuri

- **S0–S1:** modelul de încărcări, IFC SAV, solverul de bare și afișarea: o etapă de mărimea exportului JG (357 + 362).
- **S2:** elementul de placă e partea cea mai delicată (blocarea la forfecare, membrana, elementele distorsionate). E bine documentată în literatură, dar cere teste de convergență serioase.
- **Performanța:** un bloc de 10 niveluri dă ~50.000–200.000 de grade de libertate cu plăcile. Un LDLᵀ rar bine ordonat le rezolvă în secunde în .NET; în wasm, de câteva ori mai lent. De măsurat pe `Bloc-P-4` după recunoaștere.
- **Răspunderea:** un rezultat greșit într-un program de calcul are alt preț decât o cotă greșită. Rezultatele orientative, testele de echilibru (suma reacțiilor = suma încărcărilor) la fiecare rulare și verificarea încrucișată la dezvoltare sunt obligatorii.
- **Relația cu JG Structures:** un solver în Lapin se suprapune parțial cu partenerul FEA (357). De lămurit cu ei: Lapin face calcul orientativ, JG face calculul de proiect?

## 6. Ce nu facem

- Nu luăm cod din OpenSees, Code_Aster, CalculiX, BriefFiniteElement.NET sau anaStruct (licențe necomerciale, GPL, LGPL).
- Nu folosim Python în motor (Pynite doar ca verificare la dezvoltare).
- Nu facem un mesher general 3D (Gmsh e GPL; nu avem nevoie: plăcile sunt plane și convexe).
- Nu scriem în fișierele IFC existente; IFC SAV e un export nou.

## 7. De hotărât de utilizator

1. **Varianta:** C (solver propriu, în etape) cu exportul păstrat, sau rămânem la A / B?
2. **Deschis sau premium:** S0–S3 în modulul deschis și S4 (verificările după normă) premium, într-un depozit privat ca `planning-pro`? **Răspuns (20.11.2026): totul deschis, S0–S5, MIT (decizia 742).**
3. **JG Structures:** cum se împacă un solver în Lapin cu colaborarea?
4. **Normele:** P100-1/2013 și CR 0 / CR 1-1-3 / CR 1-1-4 înaintea Eurocodurilor cu anexele naționale, sau invers?
5. **Primul pas:** S0 (încărcările și IFC SAV) e util și fără solver. Începem cu el?

## Surse

- Licența OpenSees: https://openseesdocumentation.readthedocs.io/en/latest/developer/license.html ; discuția despre uzul comercial: https://opensees.berkeley.edu/community/viewtopic.php?p=122700
- xara: https://xara.so/ ; STAIRlab: https://stairlab.berkeley.edu/software/opensees
- Awatif: https://github.com/madil4/awatif ; Pynite: https://github.com/JWock82/Pynite
- BriefFiniteElement.NET: https://github.com/BriefFiniteElementNet/BriefFiniteElement.Net
- Kratos: https://github.com/KratosMultiphysics/Kratos (kratos/license.txt) ; MFEM: https://github.com/mfem/mfem ; CalculiX: https://github.com/Dhondtguido/CalculiX
- Experimentul BubbleBIM-Standalone: `~/Documents/GitHub/BubbleBIM-Standalone/src/lib/fem/` și `.claude/skills/fem-module/SKILL.md`

## 8. S0: încărcările, combinațiile și IFC Structural Analysis View (specificația)

Ce face S0: proiectul primește **cazuri de încărcare**, **încărcări puse de utilizator** (pe suprafață, liniare, concentrate) și **încărcări derivate** din model (greutatea proprie, straturile plăcilor, pereții neportanți, camerele după categoria de utilizare); **combinațiile** EN 1990 se derivă la fiecare cerere; exportul IFC poate scrie **Structural Analysis View** (modelul analitic, cazurile, încărcările, combinațiile). Fără solver: S1 îl adaugă peste același model.

### 8.1 Modulul

- Motorul: modulul nou **`analysis`** („Structural analysis”), `engine/src/Modules/Structure/Lapin.Analysis`, `module.json` cu `requires: ["core"]`. Citește camerele, plăcile și pereții după clasă și geometrie, fără să depindă de `architecture` sau `structure` (merge fără ele, cu mai puțin de citit). Adăugat în `products/lapin.json` după `structure`. Solverul din S1 vine tot aici.
- Interfața: `ui/src/modules/analysis/`, cu grupul **Loads** în fila Structure.
- Unitățile din motor: lungimi în metri, forțe în **kN**, kN/m, kN/m² (ca `Units.Si` din JG). Exportul IFC le scrie în N.

### 8.2 Cazurile de încărcare: `IfcStructuralLoadCase` (Kind `resource`)

Atribute:

| Atribut | Valori |
|---|---|
| `code` | eticheta scurtă, unică în proiect: `G1`, `G2`, `Q-A`, `S`, `W` |
| `actionType` | `PERMANENT_G`, `VARIABLE_Q`, `EXTRAORDINARY_A` (IfcActionTypeEnum) |
| `actionSource` | `DEAD_LOAD_G`, `COMPLETION_G1`, `LIVE_LOAD_Q`, `SNOW_S`, `WIND_W`, `EARTHQUAKE_E`, `USERDEFINED` (IfcActionSourceTypeEnum) |
| `selfWeight` | bool: cazul poartă greutatea proprie a elementelor analitice (IFC: `SelfWeightCoefficients (0, 0, -1)`) |
| `derives` | lista încărcărilor derivate pe care le primește: `finishes`, `partitions`, `occupancy` |
| `category` | la un caz variabil: categoria de utilizare (§8.4: `A`, `B`, `C1`…) sau `snow`, `wind`; decide ψ |
| `psi` | opțional, `{ "0": …, "1": …, "2": … }`, peste tabelul de la §8.5 |

Cazurile implicite, create de `analysis.defaultCases` numai când proiectul nu are niciunul, și de orice comandă care are nevoie de un caz (în același pas de undo):

| code | Nume | actionType | actionSource | Altele |
|---|---|---|---|---|
| G1 | Self weight | PERMANENT_G | DEAD_LOAD_G | `selfWeight: true` |
| G2 | Finishes and partitions | PERMANENT_G | COMPLETION_G1 | `derives: [finishes, partitions]` |
| Q-A | Imposed, category A | VARIABLE_Q | LIVE_LOAD_Q | `category: A`, `derives: [occupancy]` |
| S | Snow | VARIABLE_Q | SNOW_S | `category: snow` |
| W | Wind | VARIABLE_Q | WIND_W | `category: wind` |

Comenzile: `analysis.defaultCases {}`; `analysis.addCase {name, code?, actionType, actionSource?, category?, selfWeight?, derives?, psi?}` → `{id}`; `analysis.updateCase {id, …the same}`; `analysis.removeCase {id}`, refuzată cât timp o încărcare a utilizatorului e în caz; `analysis.addMissingCases {}`, câte un caz `Q-<categorie>` (derives occupancy) pentru fiecare categorie a camerelor care nu are caz. Un singur caz poate avea `selfWeight`; un fel derivat poate fi într-un singur caz (refuzat altfel).

### 8.3 Încărcările utilizatorului

Trei clase, Kind `annotation`, ca să nu intre printre elementele exportului IFC (le scrie §8.7), pe un nivel (conținute ca elementele):

| Clasă | Atribute | Geometrie |
|---|---|---|
| `IfcStructuralPlanarAction` | `caseId`, `points` (conturul în plan, ≥ 3), `z` (față de cota nivelului; 0 implicit), `value` (kN/m², > 0), `direction` | planul: conturul cu o hașură rară și valoarea; 3D: conturul la cota lui și câteva săgeți în sensul forței |
| `IfcStructuralCurveAction` | `caseId`, `a`, `b` (puncte în plan), `z`, `value` (kN/m), `direction` | planul: linia cu săgeți scurte și valoarea; 3D: linia și săgeți |
| `IfcStructuralPointAction` | `caseId`, `at`, `z`, `value` (kN), `direction` | planul: un simbol de săgeată și valoarea; 3D: o săgeată |

`direction` ∈ `down` (implicit: −Z), `up`, `+x`, `-x`, `+y`, `-y`. Câmpurile clasei (inspectorul): caz (alegere după cazurile proiectului), valoare, direcție, z.

Comenzile: `analysis.addAreaLoad {storeyId, points, value, caseId?, z?, direction?}`, `analysis.addLineLoad {storeyId, a, b, …}`, `analysis.addPointLoad {storeyId, at, …}` → `{id}`; fără `caseId`, cazul variabil `Q-A` (creat dacă lipsește). Schimbarea se face prin editarea generică a atributelor, cu verificarea valorilor. Ștergerea e cea obișnuită.

### 8.4 Încărcările derivate (calculate la fiecare cerere, niciodată stocate)

1. **Greutatea proprie** (cazul cu `selfWeight`): IFC o lasă solverului prin `SelfWeightCoefficients`. Rezumatul o dă ca Σ volum × densitate × g pe elementele modelului analitic (miezul pereților și plăcilor, barele), cu densitatea din `MaterialData.DensityOf`, altfel din `StructuralMaterials`; un element fără densitate e numărat și spus.
2. **Straturile plăcilor** (`finishes`): pe fiecare placă a modelului analitic, straturile în afara miezului (`LayerSets.BandsOf`), Σ grosime × densitate × g, ca încărcare pe suprafață peste conturul plăcii, la fața ei de sus. Un strat fără densitate e spus.
3. **Pereții neportanți** (`partitions`): fiecare perete care nu intră în modelul analitic ca placă, ca încărcare liniară pe axa lui, la cota bazei lui: greutatea părților corpului (volum × densitatea materialului fiecărei părți) împărțită la lungimea axei. Golurile sunt deja scoase din corp.
4. **Camerele** (`occupancy`): fiecare `IfcSpace` cu contur dă o încărcare pe suprafață peste conturul lui (fără insule), la cota nivelului lui, cu qk după categoria lui, în cazul cu `derives: occupancy` al acelei categorii. Categoria camerei: atributul ei `loadCategory`, altfel derivată din funcție (Living, Bedroom, Kitchen, Dining, Bath, WC, Circulation → `A`; Balcony → `A-balcony`; Office → `B`; Storage → `E1`; Garage → `F`; Technical → `E1`; altceva → `A`); `none` înseamnă fără încărcare. Comanda `analysis.setOccupancy {ids, category}` scrie `loadCategory` pe camere (null o șterge) și creează cazul categoriei dacă lipsește.

Categoriile și qk implicit (kN/m², **orientative**, EN 1991-1-1 Tabelul 6.2 și 6.10, valorile recomandate; proiectul își pune valorile anexei naționale cu `analysis.setImposedLoad {category, qk}`, null revine la implicit, păstrate pe IfcProject în `imposedLoads`):

| Cheie | Ce | qk |
|---|---|---|
| A | locuințe: încăperi | 2,0 |
| A-stairs | locuințe: scări | 2,0 |
| A-balcony | locuințe: balcoane | 2,5 |
| B | birouri | 3,0 |
| C1 | cu mese (școli, cafenele, restaurante) | 3,0 |
| C2 | cu scaune fixe (biserici, teatre, săli de conferință) | 4,0 |
| C3 | fără obstacole (muzee, holuri de clădiri publice) | 5,0 |
| C4 | activități fizice (săli de dans, de gimnastică) | 5,0 |
| C5 | aglomerări (săli de concert, de sport, tribune) | 5,0 |
| D1 | magazine mici | 4,0 |
| D2 | magazine mari | 5,0 |
| E1 | depozitare | 7,5 |
| F | garaje, vehicule ≤ 30 kN | 2,5 |
| G | vehicule 30–160 kN | 5,0 |
| H | acoperișuri necirculabile | 0,4 |

**Hotărât de utilizator (12.11.2026, „păstrăm deocamdată valorile din EN”):** rămân valorile recomandate EN de mai sus; valorile anexei naționale române (SR EN 1991-1-1/NA) și ale CR 0-2012 vin mai târziu, ca set de valori la alegere.

### 8.5 Combinațiile (derivate, `analysis.combinations`)

După EN 1990 Anexa A1 (clădiri), cu valorile recomandate, marcate orientative. ψ după categoria cazului (Tabelul A1.1):

| Categorie | ψ0 | ψ1 | ψ2 |
|---|---|---|---|
| A*, B | 0,7 | 0,5 | 0,3 |
| C*, D* | 0,7 | 0,7 | 0,6 |
| E1 | 1,0 | 0,9 | 0,8 |
| F | 0,7 | 0,7 | 0,6 |
| G | 0,7 | 0,5 | 0,3 |
| H | 0 | 0 | 0 |
| snow (≤ 1000 m) | 0,5 | 0,2 | 0 |
| wind | 0,6 | 0,2 | 0 |
| fără categorie | 0,7 | 0,5 | 0,3 (spus în raport) |

Intră în combinații doar cazurile care au ceva (greutatea proprie, o încărcare derivată sau una a utilizatorului); cele goale sunt spuse.

- **SLU, fundamentală (6.10):** pentru fiecare caz variabil ca principal: Σ 1,35 Gk + 1,5 Qk,1 + Σ 1,5 ψ0,i Qk,i; plus 1,35 Gk singur; dacă există vânt: 1,0 Gk + 1,5 W (permanentele favorabile).
- **SLS caracteristică:** Σ Gk + Qk,1 + Σ ψ0,i Qk,i, pentru fiecare principal.
- **SLS frecventă:** Σ Gk + ψ1,1 Qk,1 + Σ ψ2,i Qk,i, pentru fiecare principal.
- **SLS cvasipermanentă:** Σ Gk + Σ ψ2,i Qk,i.
- Fiecare combinație: `{key, kind: uls|sls-characteristic|sls-frequent|sls-quasi, name, factors: [{caseId, code, factor}]}`, cheia stabilă (`uls/<caseId>`, `uls/permanent`, `uls/wind-uplift`, `slsc/<caseId>`, `slsf/<caseId>`, `slsq`), numele scris din factori: `1.35 G1 + 1.35 G2 + 1.5 Q-A + 0.75 S + 0.9 W`. Fără factorii nuli.
- Lăsat pentru mai târziu, spus în raport: 6.10a/6.10b, simultaneitatea zăpezii cu încărcarea utilă pe acoperișuri (EN 1991-1-1 3.3.2), combinația seismică (cu S3), valorile CR 0-2012 (de confirmat de utilizator). 6.10a/6.10b și CR 0-2012: făcute la §13 (20.11.2026).

### 8.6 Interogările

- `analysis.loads {}` → `{cases: [{id, code, name, actionType, actionSource, category, selfWeight, derives, psi (cel folosit), userLoads (număr), total (kN), empty}], storeys: [{id, name, byCase: {caseId: kN}}], derived: [{kind, caseId, sourceId, sourceName, storeyId, value, unit, total}], missing: [{category, rooms}], notes: [...]}`. Totalurile sunt forța verticală (proiecția pe −Z, în kN).
- `analysis.categories {}` → `[{key, label, qk, default, overridden}]`.
- `analysis.combinations {}` → `{combinations: [...], notes: [...]}`.
- `analysis.preview {caseId}` → încărcările cazului pentru afișare: `{areas: [{points, z, value}], lines: [{a, b, z, value}], points: [{at, z, value}]}`, în coordonatele lumii, cu cele derivate.

### 8.7 IFC Structural Analysis View (export)

Opțiunea `structural: true` din `IfcExportOptions` (dialogul de export: „Structural analysis view (analytical model and loads)”), IFC4 și IFC4X3, din `AnalyticalModel.Build` (toți pereții portanți, picioarele încastrate, ca exportul JG implicit) și dintr-un serviciu de platformă `IStructuralLoads` pe care îl dă modulul `analysis` (cazurile, acțiunile, combinațiile). Fără modulul `analysis`, se scrie modelul analitic fără încărcări.

- `IfcStructuralAnalysisModel` (`LOADING_3D`, `LoadedBy` = cazurile și combinațiile, `SharedPlacement` la origine), legat de clădire cu `IfcRelServicesBuildings`; un subcontext `Reference` (`GRAPH_VIEW`) pentru topologie.
- **Nodurile:** `IfcStructuralPointConnection` cu `IfcTopologyRepresentation` (`Reference`, `Vertex`, un `IfcVertexPoint`); reazemele ca `IfcBoundaryNodeCondition` cu `IFCBOOLEAN`.
- **Barele:** `IfcStructuralCurveMember` (`RIGID_JOINED_MEMBER`, `Axis` = axa locală 2), `Edge` (`IfcEdge` între vârfurile nodurilor); `IfcRelConnectsStructuralMember` la fiecare capăt; secțiunea și materialul prin `IfcRelAssociatesMaterial` cu un `IfcMaterialProfileSet` (profilul ca la elementul fizic).
- **Materialele** (12.11.2026, din verificarea cu ifc_structural_mechanics, §9.8): cu SAV, fiecare material scrie și valorile pe care le ia calculul când nu le are el însuși: `Pset_MaterialMechanical.YoungModulus` și `PoissonRatio` (după clasa din nume, `StructuralMaterials`), `Pset_MaterialCommon.MassDensity`, fiecare cu sursa în descrierea proprietății („Lapin: EN 1992-1-1:2004 Table 3.1, C25/30: Ecm = 31 GPa”). Fără ele un program de calcul nu are E-ul betonului C25/30. Profilul fiecărei bare are și `Pset_ProfileMechanical` (`CrossSectionArea`, `MomentOfInertiaY` = I33, încovoierea pe înălțime, `MomentOfInertiaZ` = I22, `TorsionalConstantX`), valorile cu care calculează Lapin (`SectionProperties`), ca un cititor să nu-l redeseneze altfel (tălpile înclinate ale unui UPN se desenează în mai multe feluri: ifc_structural_mechanics găsea o arie cu 2,6 % mai mică).
- **Plăcile:** `IfcStructuralSurfaceMember` (`SHELL`, `Thickness`), `Face` (`IfcFaceSurface` pe un `IfcPlane`, conturul din muchiile nodurilor), legat de fiecare nod de pe contur.
- Fiecare membru e legat de elementul fizic (`IfcRelAssignsToProduct`); GlobalId-urile derivate stabil din GlobalId-ul elementului și bucată (`GlobalId#n`), ale nodurilor din poziție.
- **Cazurile:** `IfcStructuralLoadCase` (`LOAD_CASE`, ActionType, ActionSource, `SelfWeightCoefficients (0., 0., -1.)` la cazul greutății proprii), GlobalId-ul entității.
- **Acțiunile:** cele ale utilizatorului și cele derivate, ca `IfcStructuralPlanarAction` (`IfcStructuralLoadPlanarForce`), `IfcStructuralCurveAction` (`IfcStructuralLoadLinearForce`) și `IfcStructuralPointAction` (`IfcStructuralLoadSingleForce`), `GLOBAL_COORDS`, `CONST`, în N, N/m și N/m², cu topologia lor; în cazul lor prin `IfcRelAssignsToGroup`; legate de elementul fizic de sub ele (`IfcRelConnectsStructuralActivity`: placa sub o suprafață, bara sau placa sub un punct) când există unul, altfel nelegate; o linie, pe barele de sub ea după regula calculului (§9.2), câte o acțiune pe porțiunea fiecărei bare (o linie peste două grinzi: două acțiuni, fiecare pe grinda ei; pe o singură bară sau pe niciuna rămâne o acțiune, cu GlobalId-ul ei), restul pe placa de sub ea (12.11.2026, §9.8).
- **Combinațiile:** `IfcStructuralLoadGroup` (`LOAD_COMBINATION`), câte un `IfcRelAssignsToGroupByFactor` pe fiecare factor.
- **Unitățile:** `FORCEUNIT` (N), `IfcDerivedUnit` `LINEARFORCEUNIT` (N/m) și `PLANARFORCEUNIT` (N/m²), doar când se scrie SAV.
- Importul unui fișier cu SAV îl ignoră în S0 (cazurile și încărcările nu se citesc înapoi încă; spus în raport).

### 8.8 Testele

`LoadCaseTests` (implicitele, unicitatea codului, un singur caz cu greutate proprie, ștergerea refuzată), `DerivedLoadTests` (placa cu șapă 5 cm × 2200 kg/m³ → 1,079 kN/m²; peretele neportant de 2,5 m din BCA → linia lui; camera de 4 × 5 m categoria A → 40 kN; categoria derivată din funcție; `none`), `CombinationTests` (G1, G2, Q-A, S, W → numărul și factorii combinațiilor SLU și SLS după EN 1990, cheile stabile), `StructuralIfcTests` (exportul cu SAV citit de cititorul STEP al lui Lapin: entitățile, referințele, unitățile, GlobalId-urile stabile la două exporturi, fără SAV când nu se cere).

## 9. S1: static liniar pe bare (specificația)

**Hotărât de utilizator (12.11.2026):** S1 duce încărcările plăcilor pe reazeme după regula de 45° până la S2; calculul (S0–S3) rămâne în modulul deschis, MIT. **Hotărât de utilizator (12.11.2026, „nu mai facem ediție cu altă licență — facem ce putem noi și rezolvăm problemele până la capăt”):** nicio ediție sau modul cu licență copyleft (nici LGPL pentru CSparse.NET sau BriefFiniteElement.NET); rezolvitorul și elementele rămân ale lui Lapin, MIT, și la S2 pe modelele mari. Inspirație: [ifc_structural_mechanics](https://github.com/brunopostle/ifc_structural_mechanics) (Bruno Postle, **GPL-3.0**: nu se ia cod, doar comportamentul și convențiile citite din documentația lui; rulat local ca program separat, doar la dezvoltare, pe exportul nostru SAV).

### 9.1 Modelul de calcul

- Din `AnalyticalModel.Build` (aceleași noduri și bare ca exportul). **Barele** intră cu rigiditatea lor; **plăcile nu** în S1: pereții portanți sunt **reazeme liniare** (nodurile de pe capul lor: translațiile ținute, rotirile libere), plăcile doar duc încărcări. Spus în raport.
- **Elementul:** bara 3D cu 12 grade de libertate, Euler-Bernoulli; Timoshenko când secțiunea dă ariile de forfecare. Torsiunea cu GJ, G = E / 2(1 + ν). Axele locale ca la JG (§ „Frames' local axes” din `docs/jg-structures.md`). **Capetele excentrice** (`end_offsets`) ca brațe rigide prin transformarea matricei.
- **Eliberările:** atributul `analysisReleases` pe element (`{i: [ux, uy, uz, rx, ry, rz], j: […]}`, `true` = eliberat), scris de `analysis.setReleases {ids, i?, j?}` (un pas de undo, ca fazele pe elementele altora). Implicit: ce spune îmbinarea (`AnalyticalLink`: eclisele, guseele, contrafișele → articulat; baza de oțel articulată → articulat), barele de lemn ale șarpantei articulate la capete (panele pe căpriori, cleștii), betonul monolit încastrat. O bară articulată la ambele capete păstrează torsiunea ținută la un capăt (fără mecanism). În SAV: `IfcRelConnectsStructuralMember.AppliedCondition` (`IfcBoundaryNodeCondition` cu rotirile libere), ca în ifc_structural_mechanics.
- **Materialul:** E și ν din `StructuralMaterials`; fără E, bara e refuzată în raport și calculul nu pornește.

### 9.2 Încărcările pe bare

Din `IStructuralLoads` (S0), pe fiecare caz:
- **Greutatea proprie** a barelor: γ·A pe lungimea lor, în −Z. Greutatea plăcilor (miezul) e încărcare de suprafață pe placă.
- **Liniare și concentrate:** pe bara de sub ele (proiecția orizontală până la 0,3 m de axă, până la 1 m sub), pe porțiunea acoperită; altfel pe placa de sub ele (ca mai jos); altfel nelegate, spus.
- **Pe suprafață** (ale utilizatorului, finisajele, camerele, greutatea plăcii): pe fiecare bucată de placă (`ShellPieces`), după **regula de 45°**: bisectoarele colțurilor împart bucata în zone aferente laturilor, fiecare zonă devine o încărcare trapezoidală sau triunghiulară pe latura ei, pe bara sau peretele de sub latură; o latură fără reazem își dă zona celorlalte; o bucată fără nicio latură rezemată se duce pe nodurile ei (stâlpii), după aria aferentă. Suma pe reazeme = încărcarea plăcii, verificat.
- **Liniare pe placă** (pereții despărțitori): echivalentul pe suprafață al bucății (încărcarea / aria bucății), apoi regula de 45°. Spus.

### 9.3 Rezolvarea

- Asamblarea în coordonate globale, numerotarea nodurilor reordonată **Reverse Cuthill-McKee**, matricea în formă de profil (skyline), **LDLᵀ** cu toate cazurile ca membri drepți (o singură factorizare). Fără dependențe; merge și în browser.
- **Un mecanism** (pivot nul sau negativ): calculul se oprește și spune nodul și gradul de libertate, cu elementele legate de el.
- **Reacțiile:** R = K·U − F la nodurile rezemate; **echilibrul** ΣR + ΣF = 0 pe fiecare caz, la 1e-6 relativ, în raport.
- **Eforturile:** la capetele barelor din K_e·u_e − f_e, apoi N, Vy, Vz, T, My, Mz în 11 puncte pe bară cu încărcările din deschidere, și extremele exacte (momentul maxim sub o încărcare uniformă).
- **Combinațiile** (S0, §8.5) prin suprapunere; **înfășurătorile** SLU și SLS (maxim și minim în fiecare punct).
- Rezultatele sunt derivate, ținute în memorie după versiunea modelului (nicio scriere în `.lapin`).

### 9.4 Interogările

- `analysis.run {}` → `{ok, dofs, elements, solveMs, cases: [{id, code, sumLoads: [Fx, Fy, Fz], sumReactions: […], balance}], mechanism?, notes}`.
- `analysis.results {case | combination | envelope: "uls"|"sls", elementId?}` → deplasările nodurilor, reacțiile, eforturile pe bare (stațiile), maximele.
- `analysis.diagram {case | combination | envelope, quantity: N|Vy|Vz|T|My|Mz|deformed, scale?}` → polilinii în coordonatele lumii, pentru vedere.
- `analysis.setReleases {ids, i?, j?}` (comandă).

### 9.5 Testele (cu soluții închise; cifrele din teorie, nu din alte programe)

Consola cu forță la capăt (δ = PL³/3EI, M = PL) pe axa tare și pe cea slabă (bara rotită 90°); grinda simplu rezemată cu încărcare uniformă (5wL⁴/384EI, wL²/8) și cu forță la mijloc (PL³/48EI); grinda continuă pe două deschideri egale (wL²/8 pe reazem, reacțiile 3/8, 10/8, 3/8 wL); cadrul portal încastrat cu forță orizontală (soluția închisă); torsiunea unei console (TL/GJ); o bară articulată la capete într-un cadru; un capăt excentric (momentul din braț); regula de 45° pe o placă dreptunghiulară 6 × 4 m pe patru grinzi (două trapeze, două triunghiuri, suma = q·24 m²); mecanismul semnalat; echilibrul pe casa din scenele ghidului; IPE 300 în consolă cu secțiunea reală. **Verificări de dezvoltare, nu în depozit:** un cadru spațial față de Pynite (MIT, venv local) și exportul SAV citit de ifc_structural_mechanics (GPL, program separat).

### 9.6 Interfața (S1b, după motor)

Butonul **Run analysis** în grupul Loads; o vedere analitică 3D: deformata, diagramele N/V/M pe cazul, combinația sau înfășurătoarea aleasă, reacțiile; eforturile și deplasarea maximă ale barei în Proprietăți; mecanismul și echilibrul în dialogul Loads.

### 9.7 Ce s-a hotărât la implementare (12.11.2026)

- **Lemnul șarpantei** e articulat doar pe axa tare (rz), și numai la capătul unde reazemă pe ceva (un reazem, un perete, o bară continuă: pana, cosoroaba); unde se întâlnesc doar capete de bare, rămân legate rigid. Astereala le ține lateral; S1 n-are încă placa acoperișului. Barele prinse cu eclise, gusee sau contrafișe sunt articulate pe ambele axe. O consolă (un capăt liber) rămâne încastrată la celălalt capăt, iar capătul liber nu e eliberat nici el (o eliberare acolo n-ar avea pe ce se roti; cu brațul unui profil nesimetric, nodul vârfului ar fi un mecanism: așa ieșea hala cu pane UPN, găsit la verificarea din §9.8). Regulile sunt în `FrameReleases` (platformă), folosite și de solver, și de exportul SAV.
- **Acoperișurile pe șarpantă** iau încărcările de suprafață pe fiecare apă reală (`AnalyticalRoof`, dat de modulul de arhitectură din forma acoperișului): fiecare căprior, colțar, dolie sau talpă superioară de fermă primește fâșia până la jumătatea distanței până la vecini, tăiată după apa lui; fără căpriori, panele. Zăpada pe proiecția orizontală (q·w·cos α pe metrul de bară), restul pe aria reală. Ce cade în afara apelor e spus.
- **Timoshenko** implicit, cu ariile de forfecare din geometrie (EN 1993-1-1 6.2.6(3) la profile; 5/6·A la dreptunghi; 0,9·A la cerc); un contur oarecare rămâne Euler-Bernoulli, spus.
- **Profilele nesimetrice** (T, U, L, I nesimetric): solverul le pune axa în centrul de greutate prin brațe rigide. Exportul SAV lasă bara pe linia ei (centrul cutiei, unde un profil parametric IFC își are originea), iar centrul de greutate îl spune profilul; `IfcRelConnectsWithEccentricity` are doar brațele îmbinărilor (`OffI`/`OffJ`), muchia barei mergând atunci între capetele ei, cu vârfuri proprii (schimbat la verificarea din §9.8: înainte exportul scria și centrul de greutate ca excentricitate, de două ori pentru un cititor care le ia pe amândouă).
- **Forțele concentrate și liniare pe o placă fără bară dedesubt** se împart pe colțurile bucății cu ponderi consistente static (baricentrice, biliniare, Wachspress), integrate pe linie; colțurile fără reazem dau mai departe pe laturi după regula pârghiei.
- **Un model fără bare** rulează: plăcile se duc direct în pereți, ca reacții.
- **Rezolvitorul** (RCM + skyline LDLᵀ): 26.460 de grade de libertate în 14 s, 86.490 în 106 s (1,25 GB); peste 400 M de intrări în profil e refuzat. La S2 (plăcile, modelele mari) un Cholesky rar propriu (supernodal, cu reordonare AMD sau disecție imbricată) îl înlocuiește, tot cod al lui Lapin. Făcut la S2 (§10.7): 86.490 de grade de libertate în 4 s, 0,4 GB.
- **Verificat la dezvoltare:** un cadru spațial față de PyNite 3.2.1 (deplasările la 1e-15, reacțiile la 3e-16); vezi și §9.8.

### 9.8 Verificarea cu ifc_structural_mechanics și PyNite (12.11.2026)

Aprobat de utilizator: ifc_structural_mechanics (GPL-3.0, Python, cu IfcOpenShell, Gmsh și CalculiX 2.23) rulat local, ca program separat, pe exportul SAV, și PyNite (MIT) pe problema solverului. Nimic din ele în depozit; ce e al nostru (MIT) e în `scripts/fem-check` (README, `fem_check.py`, `pynite_run.py`) și testul opțional `SavDumpTests` (`LAPIN_SAV_OUT=<folder>`), care scrie modelele (consola, grinda simplu rezemată, cadrul portal, cadrul spațial cu și fără UPN, cadrul din ghid, hala de oțel cu pane UPN și cea implicită) și calculul lui Lapin pentru ele.

Ce a găsit, și ce s-a schimbat în export:
- **E-ul lipsea:** un material cu E dedus din clasă (C25/30) nu-l scria; acum da (§8.7, Materialele).
- **Liniile nu erau legate de bare:** o încărcare liniară pe o grindă nu avea `IfcRelConnectsStructuralActivity` (căuta doar o placă dedesubt), deci un alt program n-o putea pune pe nimic; acum e pe barele de sub ea, porțiune cu porțiune (§8.7).
- **Eliberările erau în alte axe:** IFC ia condiția capătului în axele locale ale barei, cu z pe `Axis` (axa locală 2 a lui Lapin) și y = z × x (−axa 3); exportul scria ry/rz în ordinea lui Lapin, deci o grindă articulată pe axa tare ajungea articulată pe cea slabă. Acum `[ux, u2, u3, rx, r2, r3]` merg pe `[X, Z, Y, RX, RZ, RY]` din IFC, la fel componentele excentricității.
- **Centrul de greutate scris de două ori** (§9.7, Profilele nesimetrice).
- **Secțiunile redesenate de cititor:** acum cu `Pset_ProfileMechanical` (§8.7, Materialele).
- **Solverul:** vârful liber al unei console eliberat dădea mecanism cu profile nesimetrice (§9.7).

Rezultatul (plasa 0,25 m; halele 0,5 m): pe secțiuni dreptunghiulare, suma reacțiilor la 0,034 % (g = 9,81 față de 9,80665), fiecare reazem sub 0,4 %, deplasările sub 0,8 %, eforturile sub 2 %; cu profile deschise (UPN), la fel, în afară de torsiune (dreptunghiul echivalent al lor e de zeci de ori prea rigid la torsiune, spus în README-ul lor); cadrul cu placă diferă cu 10–17 % pe reazeme și momente (ei calculează placa ca placă, S1 o duce pe grinzi după regula de 45°), sumele la fel. PyNite pe aceeași problemă (bare Euler–Bernoulli): sub 1e-9 fără brațe, sub 2e-5 cu brațe (ale lor sunt bare foarte rigide, nu rigide).

Hala cu pane UPN (562 de bare, plasa 0,5 m, ~35 min la ei): suma reacțiilor la 0,034 %, reazemele la 5,7 %, nodurile la 2,7 %; diferențele mari sunt la tiranții Ø20 ai contravântuirilor în X, care sub greutatea lor se încovoaie ca grinzi de 7,6 m (645 mm la Lapin, 590 mm la ei: ei îi leagă rigid unde se încrucișează, `W-JNT-002`, Lapin nu), și la torsiunea profilelor deschise. De hotărât: tiranții ca bare articulate fără încovoiere (sau doar întinși), ca deformata halei să nu fie a lor. PyNite pe aceeași hală: 3,5e-5.

Ce rămâne la ei, nu la noi: profilele Z și C cu buză nu le iau (hala implicită nu se poate verifica cu ei); `check` cere ca tălpile să aibă membru analitic (`W-BLD-005`; la noi sunt reazeme) și vede plăcile de îmbinare ca elemente portante fără membru.

### 9.9 Ce lipsea din S0/S1, închis (12.11.2026)

Aprobat de utilizator (12.11.2026, „mergem pe recomandarea ta - continua - cu lipsurile și cu S2”). Tot cod al lui Lapin, MIT; valorile normelor sunt cele recomandate EN, orientative, ale utilizatorului de schimbat.

**1. Zăpada din normă (CR 1-1-3/2012 = EN 1991-1-3).** Parametrii amplasamentului pe IfcProject (`climate.snow`: `s0k`, `ce`, `ct`, `importance`), puși de `analysis.setClimate` și citiți cu `analysis.climate`. **s0,k îl dă utilizatorul**: Lapin nu are harta de zonare (CR 1-1-3 Figura 3.1, Anexa A cu localitățile); Ce 0,8 / 1,0 / 1,2 (Tabelul 4.1), Ct 1,0, γIs după clasa de importanță (Tabelul 4.2). Pe fiecare apă: s = γIs · μ1(α) · Ce · Ct · s0,k pe m² de plan, μ1 = 0,8 până la 30°, 0,8(60 − α)/30 până la 60°, 0 peste (Tabelul 5.2). Apele: ale șarpantelor (`AnalyticalRoof`), ale acoperișurilor fără șarpantă (placa la planul ei median), ale halelor de oțel (planele peste talpa de sus a riglelor, purtate de pane: `AnalyticalRoof.Members`) și bucățile de placă pe care nu le acoperă nimic (terase, acoperișuri plate; nu cele de pe teren), cu μ1 = 0,8. **Hotărât: aglomerările ca alternative, nu un caz „cel mai defavorabil”** (cel mai defavorabil depinde de bară; la o grindă cu două ape contează tocmai dispunerea nesimetrică): cazul S are dispunerea (i), neaglomerată; cazurile S+X, S−X, S+Y, S−Y dispunerile (ii)/(iii) ale vântului care suflă spre fiecare latură: apele dinspre vânt (la cel mult 60°) iau 0,5 μ1, celelalte μ1. EN §5.3.3 le dă pentru acoperișuri cu două ape; Lapin le ia la fel pe apele unui acoperiș în patru ape (lectură a noastră, spusă); mono-pantă și plat fără aglomerare. O aglomerare care aici nu schimbă nimic (nicio apă spre vântul acela) nu derivă nimic și e spusă. Pe șarpante, pe căpriori/pane după lățimea aferentă (pe proiecția orizontală, ca până acum); pe plăci după regula de 45°; o acțiune pe un plan înclinat își are cota în planul ei (`LoadMapping.Planar`). Nefăcute, spuse: aglomerările la atice și denivelări (§6.2), zăpada peste streașină, încărcările excepționale.

**2. Vântul din normă (CR 1-1-4/2012 = EN 1991-1-4).** `climate.wind`: `qb` (dat de utilizator, din harta CR 1-1-4 Figura 2.1 / Anexa A), categoria de teren 0–IV (z0, zmin după EN Tabelul 4.1), `importance` (γIw, Tabelul 3.3). qp(z) = ce(z)·qb, ce = (1 + 7 Iv)·cr² (cr = kr ln(z/z0), kr = 0,19 (z0/0,05)^0,07, Iv = 1/ln(z/z0), z ≥ zmin, c0 = 1); w = γIw · cpe,10 · qp(ze). Clădirea: înfășurătoarea convexă a pereților (toți) și stâlpilor, înălțimea h de la nodul cel mai de jos până sus. **Hotărât: patru direcții (spre +X, −X, +Y, −Y, axele proiectului) × două seturi pentru acoperișuri** (cele mai mici cpe, sucțiunea: cazurile `W±X/s`, `W±Y/s`; cele mai mari, presiunea: `/p`), 8 cazuri cu `derives: [wind]`, `windDirection`, `roofCpe`; un caz /p egal cu /s-ul lui (fără acoperiș înclinat, sau vântul în lungul coamei) nu derivă nimic și e spus. Cele două combinații „mixte” din nota Tabelului 7.4a nu se fac (de hotărât). Fațadele: laturile înfășurătoarei, de la teren până la ce e deasupra lor (acoperiș, perete, bară); zona D la cea din vânt, E la cea opusă (×0,85 … 1 după h/d, 7.2.2(3)), A/B/C pe laterale de la muchia din vânt (e/5, e; e = min(b, 2h)), cpe,10 după Tabelul 7.1 interpolat liniar în h/d; ze = h pentru h ≤ b, altfel benzile din Figura 7.4. Acoperișurile: plate (< 5°) după Tabelul 7.2 (F, G, H, I, streașini drepte, I ±0,2 în cele două seturi), cu două ape după 7.4a (vântul perpendicular pe coamă: F, G, H pe apa din vânt, J, I pe cealaltă) și 7.4b (în lungul coamei), mono-pantă după 7.3a (θ = 0° și 180°) și 7.3b, zonele de la muchia din vânt, ze = h, interpolare liniară în pantă. Direcția: perpendicular pe suprafață (cpe > 0 apasă). **Unde ajunge:** pe acoperișuri ca zăpada, pe aria reală (`Projected = false`); pe fațade (acțiuni pe fețe verticale, scrise ca atare și în IFC) pe ce merge de-a lungul lor în planul lor, la cel mult 0,75 m (grinzi, rigle de perete, grinzi de streașină, rigla înclinată a frontonului, marginile planșeelor), în fâșii verticale până la jumătatea distanței până la următorul, fâșia de jos în teren (în afara sumelor barelor, spusă, `toGround` în `analysis.run`). Stâlpii nu iau direct (doar prin rigle). În combinații vântul e ca până acum; combinația de ridicare 1,0 G + 1,5 W pentru fiecare caz de sucțiune. Nefăcute, spuse: acoperișurile în patru ape și cu mai multe ape (Tabelul 7.5), presiunea interioară cpi, frecarea, copertinele, factorul dinamic.

**Alternativele în combinații.** Cazurile care derivă zăpada sunt alternative între ele, la fel cele care derivă vântul: fiecare combinație ia cel mult unul din fiecare (o acțiune, mai multe dispuneri). Cheile rămân `uls/<caz>` când nu sunt alternative; cu alternative, `uls/<conducător>+<alternativa aleasă>…` (doar cele cu factor nenul), două alegeri care diferă doar printr-un factor zero sunt una. Pe hala implicită: 87 de combinații.

**3. Tiranții axial.** O bară e axială (`FrameReleases.Axial`): atributul ei `analysisAxial` (`analysis.setAxial {ids, axial: true|false|null}`, în Proprietăți „Axial member”), altfel un tirant sau cablu după tip (TIEBAR, cabluri IFC 4.3), altfel o bară rotundă plină care contravântuiește (BRACE, `rbr:`/`wbr:` la hale). E articulată la capetele ei (torsiunea ținută la i), greutatea proprie ca forțe la cele două capete (jumătate fiecare), fără încărcare în deschidere; în SAV `PIN_JOINED_MEMBER`. Tiranții Ø20 ai halei: săgeata sub greutatea proprie de la 645 mm la 0. **Doar întinși** (compresiunea refuzată) rămâne la S5.

**4. Nodurile unde grinzi de înălțimi diferite ajung la un stâlp.** Înainte, `JoinEnds` muta capetele a două grinzi la o cotă între ele (grinzi înclinate 5 cm pe 5 m) și stâlpul se tăia la fiecare ax (bucăți de 25 mm, `W-BLD-008` la ifc_structural_mechanics). Acum două grinzi orizontale la cote diferite nu se mai unesc așa, iar `JoinAtColumns` adună capetele grinzilor de la un stâlp ale căror secțiuni se suprapun pe înălțime: nodul stâlpului e capătul lui (sau, la mijlocul lui, axa grinzii celei mai înalte), celelalte grinzi rămân la cota lor și se leagă de nod printr-un braț rigid vertical (excentricitatea de capăt existentă: JG `end_offsets`, SAV `IfcRelConnectsWithEccentricity`). Barele înclinate (căpriorii, riglele halelor) se unesc ca înainte.

   **Corectat la verificarea din §9.8 (12.11.2026):** după 512, `space-frame-rect` (și `space-frame`) ieșea în afara toleranțelor: PyNite până la 99 % pe eforturile de capăt, ifc_structural_mechanics 6–54 % pe reazeme (înainte, sub 0,8 %). Solverul nu greșea (brațele și eliberările la capătul barei, nu la nod). Cauza: nodul stâlpului rămânea la capătul lui, pe care `SnapLevels` îl pune la axa cea mai înaltă, deci la diagonala de 30 cm (2,85), iar grinzile de 50 cm atârnau 10 cm sub el pe brațe, cu capetele lor proprii **pe axa stâlpului**. Un cititor al SAV-ului (Gmsh la ifc_structural_mechanics) lipea acolo stâlpul și cele două grinzi într-un singur punct: grinda articulată pe axa tare la (5, 4) își pierdea articulația, iar bucata de 10 cm a stâlpului era scurtcircuitată de braț. La PyNite, din `pynite_run.py`: PyNite taie o bară la orice nod de pe axa ei, deci stâlpul era tăiat la capătul brațului și eforturile lui de capăt erau ale bucății de 10 cm. Acum nodul unei îmbinări e mereu la cota grinzii celei mai înalte (din cele egale, cea de la capătul stâlpului, apoi cea mai de sus), capătul stâlpului din îmbinare (și al stâlpului de deasupra sau de dedesubt care-l întâlnește acolo) se mută la nod, iar grinzile mai puțin înalte urcă pe brațe deasupra capului stâlpului, nu pe axa lui; `pynite_run.py` nu mai taie barele la nodurile de pe ele (problema lui Lapin nu le taie). La o îmbinare la mijlocul stâlpului capetele grinzilor mai puțin înalte rămân pe axa stâlpului (de reluat dacă un cititor le leagă greșit). Rezultatul: `space-frame-rect` reazemele sub 0,34 %, nodurile sub 1,4 %, eforturile sub 1,6 %; `space-frame` reazemele sub 1,3 %, nodurile sub 1,7 %; PyNite pe amândouă sub 1,3e-6; `portal` și `guide-frame` neschimbate. Teste: `BeamLevelTests` (niciun capăt pe braț pe un stâlp; diagonala articulată pe brațe sub forța din mijloc: V = P/2, M = PL/4, zero la capete; cadrul la solver, în ambele așezări, egal cu cel cu brațele desenate ca bare foarte rigide, la 1e-5). **Garda acoperișurilor pe șarpantă:** încărcarea pusă pe barele unui acoperiș e comparată cu q × aria fâșiilor (peste cos α pentru aria reală); peste 1 % rularea o spune („… kN placed on the framed roofs' members where their area calls for … kN”); `FramedRoofLoadTests.An_L_shaped_roof_whose_members_do_not_meet_exactly…` (un acoperiș în L de 132 m² cu căpriori scurți sau lungi cu câțiva mm, în două bucăți care se suprapun sau lasă un gol de 5 mm la y = 8) trece la 1e-6.

**5. SAV după filtrele exportului.** `AnalyticalOptions.Exclude`: ce lasă deoparte exportul (Exclude, și ce nu e construit încă în faza exportată) lasă deoparte membrul, reazemele, legăturile și încărcările derivate din el (`IStructuralLoads.For(query, options)`).

**6. Notele traduse.** Ca la celelalte mesaje ale motorului: interfața le traduce (frazele fixe în dicționar, cele cu numere și nume prin tipare), în toate limbile din `ui/src/i18n`; numele implicite ale cazurilor (în model rămân în engleză) se afișează traduse (`t(name)`); notele din dialogul de rezultate nu mai sunt `data-no-i18n`.

Teste: `ClimateLoadTests` (μ1, ce(10) = 2,3523 la terenul II, cpe după h/d, corelarea; parametrii verificați și cazurile făcute într-un pas de undo; zăpada pe casa cu două ape la 45°, μ1 = 0,4, aglomerată 0,75; placa neacoperită 0,8 și cea acoperită fără; vântul pe cadrul 6 × 4: zonele D, E, A, B, C, F, G, H, I cu numerele calculate de mână, tot pus, jumătate din fațade în teren; hala: Tabelul 7.4a, panele duc zăpada, echilibrul), `AxialMemberTests`, `BeamLevelTests`, `StructuralIfcTests.The_view_leaves_out…`, `notes.test.ts`, `loads.test.ts`.

**De hotărât de utilizator:** (a) combinațiile mixte ale Tabelului 7.4a (presiune pe apa din vânt, sucțiune pe cealaltă) și cpi (+0,2/−0,3): încă 8–16 cazuri de vânt; (b) aglomerarea pe acoperișurile în patru ape (lectura noastră) și vântul pe ele (Tabelul 7.5); (c) direcțiile vântului după axele proiectului sau ale clădirii; (d) valorile CR 1-1-4 pentru kr și ce (aici EN).
## 10. S2: plăci (specificația)

**Aprobat de utilizator (12.11.2026, „mergem pe recomandarea ta - continua - cu lipsurile si cu S2”).** Tot codul e al lui Lapin, MIT: niciun cod și nicio dependență din OpenSees, CalculiX, Code_Aster, BriefFiniteElement.NET, CSparse.NET, anaStruct, SuiteSparse / CHOLMOD, METIS sau Gmsh; elementele, rezolvitorul și mesherul sunt scrise după articolele și cărțile citate mai jos (și în comentariile codului).

S2 are două părți. **S2a** (făcută pe 12.11.2026, în paralel cu lipsurile lui S1): biblioteca din `Lapin.Analysis/Solver/` și testele ei: elementul de placă, rezolvitorul rar, mesherul, netezirea rezultatelor. **S2b** (după ce ambele ramuri intră în `main`): plăcile și pereții modelului analitic în `analysis.run`, încărcările pe ele, rezultatele în interogări, izoliniile în 3D și în plan, ghidul (§10.10).

### 10.1 Ce face S2

Plăcile și pereții portanți devin elemente finite de placă (șase grade de libertate pe nod), pe aceleași noduri cu barele. Regula de 45° (§9.2) se retrage pentru plăcile discretizate: încărcările de suprafață stau pe placă, iar placa le duce singură pe grinzi, pereți și stâlpi, după rigiditatea lor. Pereții nu mai sunt reazeme liniare (§9.1): sunt console în planul lor (pereți de contravântuire) și plăci în afara lui. Rezultatele: deplasările, reacțiile, eforturile pe bare (ca la S1) și eforturile pe plăci (§10.6).

### 10.2 Elementul

**Patrulaterul: MITC4** (Dvorkin și Bathe, „A continuum mechanics based four-node shell element for general nonlinear analysis”, Eng. Comput. 1, 1984; Bathe și Dvorkin, „A four-node plate bending element based on Mindlin/Reissner plate theory and a mixed interpolation”, IJNME 21, 1985). Teoria Reissner–Mindlin, deci plăci groase și subțiri cu același element (o placă de 20 cm pe 4 m, un perete de 25 cm pe 3 m). w, θx, θy biliniare; lunecările transversale se interpolează din valorile lor covariante la mijlocul laturilor, așa că o placă subțire nu se blochează la forfecare, iar elementul nu are moduri parazite. Factorul de forfecare 5/6. De ce MITC4 și nu DKMQ (Katili, „A new discrete Kirchhoff-Mindlin element based on Mindlin-Reissner plate theory and assumed shear strain fields”, IJNME 36, 1993): amândouă sunt fără blocare; MITC4 e referința literaturii, cel mai simplu de scris și de verificat, și trece testul peticului pe elemente distorsionate. DKMQ e ce folosește PyNite: bun ca verificare încrucișată (§10.9), fiind altă formulare. DKT (Batoz 1980) e doar Kirchhoff, deci nu merge la plăci groase.

**Triunghiul: MITC3** (Lee și Bathe, „Development of MITC isotropic triangular shell finite elements”, Comput. Struct. 82, 2004): forfecarea ca o constantă plus o rotire, legată de cele trei laturi. E mai rigid decât MITC4 pe plasă grosieră (tabelul de la §10.8); mesherul îl folosește doar unde triunghiurile nu se împerechează în patrulatere.

**Membrana, cu rotirea de burghiu (drilling) adevărată**: interpolarea lui Allman („A compatible triangular element including vertex rotations for plane elasticity analysis”, Comput. Struct. 19, 1984): deplasarea normală a fiecărei laturi e pătratică, din rotirile de la capetele ei. Rotirea θz e legată de rotirea în plan a câmpului de deplasări, ω = ½(∂v/∂x − ∂u/∂y), printr-o penalizare γ·∫(θz − ω)² dA, ca la Hughes și Brezzi („On drilling degrees of freedom”, CMAME 72, 1989) și la patrulaterul lui Ibrahimbegović, Taylor și Wilson („A robust quadrilateral membrane finite element with drilling degrees of freedom”, IJNME 30, 1990), cu γ = G. Așa, la nodul unde un perete întâlnește o placă, o bară sau alt perete, toate cele șase grade de libertate au rigiditate proprie: nu e nevoie de un resort fictiv pe θz. Peretele în consolă (§10.8) arată că membrana duce și încovoierea în planul ei.

**Integrarea**: 2 × 2 puncte Gauss pe patrulater, trei puncte pe triunghi (exact pentru integranzii lui, pătratici). **Elementul e plan**: nodurile lui sunt luate în planul lor mediu (`Warp` spune cât se abat). Bucățile din `ShellPieces` sunt plane, deci elementele lor sunt plane exact.

**Axele locale**: z e normala (din ordinea nodurilor, invers trigonometric văzut dinspre ea); x e X global proiectat pe planul elementului la plăci (|n·Z| ≥ 0,7) și orizontala Z × n la pereți (y în sus); `ShellElement.Axis` le poate da altfel. Toate elementele unei suprafețe plane au aceleași axe, deci rezultatele lor se pot media direct.

**Convenția eforturilor**: Nx, Ny, Nxy (kN/m), Mx = ∫σx·z dz, My, Mxy (kN·m/m), Qx, Qy (kN/m). Un moment pozitiv întinde fața dinspre normală. La o placă de planșeu cu normala în sus, momentul din câmp (fața de jos întinsă) e negativ; afișarea din S2b îl întoarce (§10.10).

### 10.3 Discretizarea (`ShellMesher`)

Fiecare bucată din `ShellPieces` (convexă, plană, fără goluri: golurile sunt deja scoase ca fâșii în jurul lor) se discretizează separat, dar laturile se taie la fel pentru toate:
1. Fiecare latură a unei bucăți se taie întâi în **punctele obligatorii** de pe ea: nodurile barelor (`hard`) și colțurile celorlalte bucăți (o latură lungă care întâlnește două bucăți, în T).
2. Fiecare parte dintre două puncte obligatorii se taie în segmente egale, nu mai lungi decât **dimensiunea țintă** (`size`). O parte e identificată prin nodurile ei de la capete, deci e tăiată o singură dată, oricare bucată o cere: bucățile vecine și barele de pe latură au aceleași noduri.
3. O bucată cu patru colțuri, cu laturile opuse tăiate în tot atâtea segmente, se discretizează prin **interpolare transfinită** (Coons; Gordon și Hall, „Construction of curvilinear co-ordinate systems and applications to mesh generation”, IJNME 7, 1973): numai patrulatere. Orice altă bucată (triunghiuri, poligoane, patrulatere cu laturile opuse tăiate diferit): o rețea de puncte interioare la dimensiunea țintă, la cel puțin 0,45 din ea de margine; o triangulare Delaunay a lor și a conturului (prin flip-urile lui Lawson, „Software for C¹ surface interpolation”, 1977; conturul fiind convex, nu cere constrângeri); netezire laplaciană care nu întoarce niciun triunghi; apoi triunghiurile **împerecheate** în patrulatere, cel mai bun întâi (după cât de departe de unghiul drept e cel mai rău unghi al patrulaterului; nimic sub 0,35), ca la Lo și Lee („Generating quadrilateral elements on plane and over curved surfaces”, Comput. Struct. 31, 1989). Ce nu se împerechează rămâne triunghi (MITC3).
4. **Fețele** sunt toate în sensul trigonometric față de normala bucății (cea dată, sau a colțurilor), deci axele locale ale elementelor unei suprafețe se potrivesc.

Măsurat: un dreptunghi dă numai patrulatere; un triunghi, un pentagon, un hexagon, un octogon dau 86–91 % din arie în patrulatere. Nu facem un mesher general 3D (§6): plăcile sunt plane și convexe.

### 10.4 Barele și plăcile pe aceleași noduri

- **Nodurile comune**: mesherul primește nodurile modelului analitic (colțurile bucăților și nodurile barelor) și le păstrează indicii; nodurile noi vin după. O bară care stă pe laturi de plăci (o grindă sub marginea plăcii, o grindă de-a lungul unei linii de reazem, care la `ShellPieces` e deja o latură) se taie în bucăți între nodurile plasei de pe linia ei (`ShellMesh.NodesAlong(a, b)`). Eliberările de la capete rămân pe primul și ultimul segment; încărcările din deschidere se împart pe segmente după poziție (S2b).
- **Grinda în placă (`offset_z`)**: barele orizontale sunt deja mutate în planul median al plăcii pe care o duc (decizia 357), cu distanța până la axa lor reală în `AFrame.OffsetZ`. În S2 bara rămâne pe nodurile plăcii și își ține axa reală prin **brațe rigide** verticale (`OffI = OffJ = (0, 0, −offset_z)`, ca excentricitățile de la §9.1). Așa se vede conlucrarea placă–grindă (secțiunea T): grinda întinsă în câmp, placa comprimată deasupra ei (testul de la §10.8).
- **Peretele ca placă**: bucățile peretelui (de-a lungul × în sus, cu golurile scoase) se discretizează la fel; nodurile de la capul lui sunt ale plăcii de deasupra, cele de la talpă ale reazemului (fundația: translațiile și rotirile după `Footings`) sau ale plăcii de dedesubt. Stâlpii care intră în perete se leagă pe nodurile de pe latura comună.
- **Un nod fără nicio rigiditate pe o rotire** (bare articulate care se întâlnesc) e ținut de resort ca la §9.7; la plăci nu se întâmplă, având toate cele șase grade de libertate rigide.

### 10.5 Încărcările pe plăci

- **De suprafață** (ale utilizatorului, finisajele, camerele, zăpada, greutatea plăcii, adică miezul γ·t): pe fiecare element, ca vector global pe m² (`CaseLoads.Surface`), cu cotele consistente ∫Nᵢ dA pe noduri (translațiile; fără momente, rotirile fiind interpolate separat). Zăpada pe proiecția orizontală: q·cos α pe aria reală.
- **Greutatea proprie** a pereților: γ·t pe aria lor, în −Z.
- **Liniare și concentrate** pe o placă: pe nodurile elementului de sub ele, cu ponderile funcțiilor de formă (la S1 erau colțurile bucății, §9.7); o forță pe un nod, direct pe el.
- **Regula de 45°** rămâne doar pentru plăcile pe care utilizatorul nu le vrea calculate ca plăci (o opțiune a rulării) și pentru acoperișurile pe șarpantă (căpriorii duc astereala, §9.7).
- **Echilibrul** se verifică la fiecare caz ca la S1: ΣR + ΣF = 0, cu ΣF incluzând încărcările pe plăci (aria fiecărui element).

### 10.6 Rezultatele

- Pe element: deplasările nodurilor în axele lui; eforturile (§10.2) în centru și la colțuri (patrulaterul: valorile din punctele Gauss extrapolate biliniar la colțuri; triunghiul: calculate la colțuri). `ShellResult.Combine` pentru combinații (suprapunere), ca la bare.
- **La noduri**, pe suprafață (`ShellElement.Group`, elementul fizic): media colțurilor (`ShellSmoothing.AtNodes`) și **recuperarea pe petice** a lui Zienkiewicz și Zhu (`ShellSmoothing.Recovered`; „The superconvergent patch recovery and a posteriori error estimates”, IJNME 33, 1992): un câmp liniar ajustat prin cele mai mici pătrate pe valorile din centrele elementelor din jurul nodului (centrul e punctul cel mai exact al unui element biliniar: Barlow, „Optimal stress locations in finite element models”, IJNME 10, 1976), citit la nod; un nod de pe margine sau din colț ia media peticelor vecinilor. La marginea încastrată media colțurilor rămâne în urmă cu o jumătate de element (−12 % la 32 × 32), recuperarea nu (−0,9 %). Unde două suprafețe se întâlnesc în unghi, fiecare își păstrează valoarea.
- Pentru dimensionare (S4): momentele de calcul după **Wood–Armer** (Wood, „The reinforcement of slabs in accordance with a pre-determined field of moments”, Concrete 2, 1968; Armer, aceeași revistă) din mx, my, mxy, pe fiecare față și direcție. Nu în S2.
- Afișarea: izolinii și benzi colorate pe fiecare mărime (mx, my, mxy, nx, ny, nxy, vx, vy, w), cu valorile extreme etichetate (S2b).

### 10.7 Rezolvitorul rar (`SparseCholesky`)

Înlocuiește skyline-ul de la S1 (§9.7) ca implicit al lui `FrameSolver`; skyline-ul rămâne (`FactorKind.Skyline`), pentru comparație.
- **L·Lᵀ = P·A·Pᵀ** pentru matricea simetrică, pozitiv definită după reazeme. Ecuațiile vin în grupuri (cele șase grade de libertate ale unui nod, mai puțin cele ținute), ordonate împreună.
- **Reordonarea**, pe graful nodurilor: **gradul minim aproximativ** pe graful cât (Amestoy, Davis și Duff, „An approximate minimum degree ordering algorithm”, SIAM J. Matrix Anal. Appl. 17(4), 1996; George și Liu, „Computer Solution of Large Sparse Positive Definite Systems”, Prentice-Hall 1981, cap. 5) și **disecția imbricată** prin structuri de niveluri (George, „Nested dissection of a regular finite element mesh”, SIAM J. Numer. Anal. 10, 1973; George și Liu 1981, cap. 8), cu gradul minim pe părțile sub 200 de noduri. Se ia cea care prezice mai puțin lucru (Σ pe coloane a pătratului numărului de rânduri de sub diagonală, din numărătorile de coloane).
- **Simbolic**: arborele de eliminare (Liu, „The role of elimination trees in sparse factorization”, SIAM J. Matrix Anal. Appl. 11(1), 1990), în postordine; numărătorile de coloane din subarborii rândurilor; supernodurile fundamentale (coloane la rând cu aceeași structură), fiecare un bloc dens, pe rânduri.
- **Numeric**: supernodal, „left-looking” (Ng și Peyton, „Block sparse Cholesky algorithms on advanced uniprocessor computers”, SIAM J. Sci. Comput. 14(5), 1993; Davis, „Direct Methods for Sparse Linear Systems”, SIAM 2006, cap. 4 și 9): fiecare supernod adună actualizările supernodurilor de sub el prin liste legate după următorul lor rând, apoi își factorizează blocul dens. Nucleele: produse scalare vectorizate (`Vector<double>`), două rânduri pe două coloane deodată. Blocurile mari (peste 4·10⁶ de înmulțiri) se împart pe nucleele procesorului; în browser, unde nu sunt fire, rulează pe rând.
- **Un factor, toate cazurile**: `Solve` pentru fiecare membru drept, după o singură factorizare.
- **Mecanismul**: un pivot care nu e clar pozitiv (sub 10⁻¹¹ din diagonala ecuației) se spune cu ecuația lui, ca la skyline; resorturile pentru rotirile fără rigiditate (§9.7) merg la fel (apelul `hold`).
- **Rafinarea iterativă** în `FrameSolver`: reziduul ecuațiilor libere, f − (K + resorturi)·u, din forțele elementelor, rezolvat din nou și adăugat, de până la trei ori cât scade (Higham, „Accuracy and Stability of Numerical Algorithms”, cap. 12). Ține echilibrul la 1e-9 pe modele prost condiționate oricare ar fi ordinea eliminării (portalul de la §9.5 cu A = 1000 m² ieșea la 2,3e-9 fără ea).
- **Limita**: peste 400 M de intrări în factor (3,2 GB) calculul e refuzat, spus, ca la S1.

### 10.8 Testele (soluții închise; cifrele din teorie)

`ShellElementTests`, `ShellMesherTests`, `SparseCholeskyTests`. Raportul la teorie (1 = exact):

| Test | Referința | 8 × 8 | 16 × 16 | 32 × 32 |
|---|---|---|---|---|
| Placă pătrată simplu rezemată, w în centru | 0,00406235 q a⁴/D (Navier; Timoshenko și Woinowsky-Krieger, „Theory of Plates and Shells”, §30, tabelul 8, ν = 0,3) | 0,99537 | 0,99926 | 1,00020 |
| idem, Mx în centru | 0,0478864 q a² | 1,00038 | 1,00006 | 1,00001 |
| Placă 2:1 simplu rezemată, w / Mx / My | 0,01013 q a⁴/D, 0,1017 q a², 0,0464 q a² (tabelul 8) | 0,986 / 0,982 / 0,982 | 0,997 / 0,995 / 0,995 | 0,9993 / 0,9987 / 0,9979 |
| Placă pătrată încastrată, w în centru | 0,00126532 q a⁴/D (tabelul 35; Taylor și Govindjee, Commun. Numer. Meth. Engng 20, 2004) | 0,99038 | 0,99907 | 1,00126 (placa groasă a/t = 100 are cu 0,1 % peste Kirchhoff) |
| idem, M în centru | 0,022905 q a² | 1,01767 | 1,00450 | 1,00125 |
| idem, M la mijlocul marginii (recuperare pe petice) | −0,05133 q a² | | | 0,991 (media colțurilor: 0,876) |
| Placă subțire a/t = 100, 1.000, 10.000, 8 × 8 | Kirchhoff | sub 3 %, fără blocare | | |
| Triunghiuri MITC3, pătrat simplu rezemat, w | 0,00406235 q a⁴/D | 0,731 | 0,970 | 0,996 |
| Peretele în consolă 3 × 12 m, forța în cap, δ | PH³/3EI + PH/(κGA), Timoshenko | 0,9796 (2 × 8) | 0,9922 (4 × 16) | 0,9961 (8 × 32) |
| Acoperișul Scordelis–Lo, w la mijlocul marginii libere | 0,3024 (Scordelis și Lo, J. ACI 61, 1964; MacNeal și Harder, Finite Elem. Anal. Des. 1, 1985) | 0,30401 | 0,30169 | 0,30140 (24 × 24) |

Și: șase moduri de corp rigid și niciun mod parazit (patrulater și triunghi distorsionate, înclinate în spațiu, t = 0,2 și 0,01); testul peticului lui MacNeal și Harder, membrana (cu rotirea de burghiu) și încovoierea cu curbură constantă, pe patrulatere și triunghiuri: exact (1e-12); placa pe patru grinzi pe brațe rigide sub ea, cu noduri comune, rezemată în colțuri: ΣR = ΣF la 1e-9, fiecare colț un sfert, grinzile întinse și placa comprimată în câmp; o placă tăiată pe diagonală în două bucăți triunghiulare, discretizată de mesher (214 patrulatere, 80 de triunghiuri): w la 1,1 %; reacțiile și momentul de răsturnare ale peretelui (P·H) exacte. Mesherul: dreptunghiul numai în patrulatere; trei bucăți în T cu nodul unui stâlp pe latura comună, fără noduri atârnate; fețele întoarse după normala cerută. Rezolvitorul: matrice aleatoare SPD față de factorizarea densă (1e-9), o rețea de plăci în disecție imbricată la reziduul ei (1e-9), o matrice singulară spusă și ținută de un resort, rezultatele cadrului aceleași cu ale skyline-ului (1e-9).

### 10.9 Verificarea cu PyNite (dezvoltare)

`scripts/fem-check/pynite_plate.py` (MIT, rulat cu venv-ul PyNite, ca la §9.8) rezolvă aceleași plăci cu patrulaterele lui PyNite 3.2.1, care sunt **DKMQ** (altă formulare decât MITC4):

| Placa | Plasa | PyNite w / Mx | Lapin w / Mx |
|---|---|---|---|
| simplu rezemată | 8 × 8 | 0,99981 / 1,01111 | 0,99537 / 1,00038 |
| simplu rezemată | 16 × 16 | 1,00027 / 1,00318 | 0,99926 / 1,00006 |
| încastrată | 8 × 8 | 1,04454 / 1,06348 | 0,99038 / 1,01767 |
| încastrată | 16 × 16 | 1,01270 / 1,01661 | 0,99907 / 1,00450 |

Amândouă converg spre teorie, DKMQ de deasupra (mai moale), MITC4 de dedesubt la deplasări; la 16 × 16 Lapin e mai aproape.

### 10.10 S2b: integrarea (planul)

1. **Problema** (`StructuralProblem.Build`, după ce intră lipsurile lui S1): opțiunea `shells: "slabs" | "walls" | "both" | "none"` (implicit „both”) și `meshSize` (implicit 0,5 m; mai mic la plăcile mici: cel puțin 4 elemente pe latura cea mai scurtă a bucății). Bucățile `AShell` devin `MeshPiece` (colțurile prin `NodeIndex`, grupul = GlobalId-ul elementului, normala: în sus la plăci, orizontală la pereți); toate nodurile barelor sunt puncte obligatorii. `ShellMesher.Mesh` → nodurile noi în `FrameProblem`, elementele `ShellElement` cu E, ν din `StructuralMaterials` și grosimea miezului (`AnalyticalShell.Thickness`); un perete sau o placă fără E e lăsat ca la S1 (reazem, regula de 45°), spus.
2. **Barele tăiate** pe nodurile plasei (`ShellMesh.NodesAlong`), cu eliberările la capetele lor reale; brațele rigide din `OffsetZ`; harta bară → bucăți, pentru stații și diagrame (rezultatele unei bare se recompun pe lungimea ei întreagă).
3. **Reazemele**: pereții nu mai sunt reazeme liniare; tălpile lor iau condiția fundației (`Footings`).
4. **Încărcările** (`LoadMapping`): cele de suprafață pe elementele de sub ele (punctul elementului în plan), cele liniare și concentrate pe nodurile elementului cu ponderile funcțiilor de formă; regula de 45° numai pentru plăcile lăsate afară.
5. **Interogările**: `analysis.run` dă și `shells: {elements, nodes, quads, triangles, meshSize}`; `analysis.results {…, shellId?}` dă eforturile pe element și pe noduri (recuperate), extremele pe fiecare suprafață; `analysis.contours {case | combination | envelope, quantity: mx|my|mxy|nx|ny|nxy|vx|vy|w, levels?}` dă benzile și izoliniile în coordonatele lumii, pe fiecare suprafață, ca `analysis.diagram`.
6. **Interfața**: benzile colorate și izoliniile în 3D (cardul `overlays3d` din S1b) și **în plan**, prin rezolvitorul de vizibilitate (`VisibilityGraphics`), ca planșele să le poată tipări; legenda cu scara, valorile extreme etichetate; mx afișat cu semnul inginerului (pozitiv = fața de jos întinsă la plăci, fața dinspre interior la pereți, spus în legendă); eforturile și deplasarea maximă ale unei plăci sau ale unui perete în Proprietăți.
7. **Ghidul**: pagina Analysis în toate limbile, scena ei cu izolinii pe planșeul din ghid.
8. **Testele** S2b: casa din ghid cu placa calculată ca placă (echilibrul, momentele grinzilor față de regula de 45°: mai mici în câmp, cu placa care conlucrează), verificarea față de ifc_structural_mechanics pe `guide-frame` (care calculează placa ca placă: diferențele de 10–17 % de la §9.8 ar trebui să scadă sub 5 %), un bloc mic de pereți (Bloc-P-4 după recunoaștere) cronometrat.

### 10.11 Performanța

Ținta: modele de mărimea lui Bloc-P-4 (100.000–200.000 de grade de libertate cu plăcile) în câteva secunde în .NET. Măsurat pe 12.11.2026 (Mac, Release, 10 nuclee):

| Model | Grade de libertate | Skyline (S1) | Rar (S2) |
|---|---|---|---|
| Cadru 21 × 21 × 10 (`Grid` din teste) | 23.814 | 4,30 s, 143 MB | 0,73 s, 72 MB |
| Cadru 31 × 31 × 15 | 80.724 | 76,4 s, 1.096 MB | 4,04 s, 403 MB |
| Factorizarea singură: cadru 31 × 31 × 15, toate nodurile libere | 86.490 | | 3,75 s, 483 MB, 114 GFlop (disecție imbricată; gradul minim ar fi cerut 217 GFlop) |
| Factorizarea singură: rețea de plăci 150 × 150 | 136.806 | | 0,89 s, 294 MB |
| Factorizarea singură: rețea de plăci 180 × 180 | 196.566 | | 1,38 s, 446 MB |
| Placă încastrată 30 × 30 m, t = 0,2, 150 × 150 patrulatere, tot solverul (elementele, asamblarea, factorizarea, rafinarea, eforturile) | 133.206 | | 3,6 s, 284 MB |
| idem, 180 × 180 | 192.246 | | 3,6 s, 435 MB |

(La S1, §9.7, skyline-ul dădea 26.460 de grade de libertate în 14 s și 86.490 în 106 s, 1,25 GB, pe modelele lor.) Un singur fir face ~8 GFlop/s: în browser (fără fire, wasm), de câteva ori mai lent decât pe desktop.

### 10.12 Hotărât de utilizator (12.11.2026, „sunt de acord”)

1. **Plasa implicită**: 0,5 m, cu cel puțin 4 elemente pe latura cea mai scurtă a unei bucăți; se schimbă din dialog.
2. **Semnul momentelor** în interfață: la plăci, fața de jos întinsă e pozitivă (cum citește proiectantul); motorul păstrează convenția mecanicii, interfața o întoarce.
3. **Pereții de zidărie** rămân reazeme liniare până dă utilizatorul valori din CR 6.

### 10.13 S2b: ce s-a hotărât la implementare (12.11.2026, decizia 514)

- **Opțiunile** pe IfcProject (`analysisOptions: {shells, meshSize}`), scrise de `analysis.setOptions {shells?: both|slabs|walls|none, meshSize?}` (un pas de undo; null înapoi la implicit), citite cu `analysis.options`; în dialogul de rezultate (Shells, Mesh). Plasa implicită 0,5 m, pe fiecare bucată cel mult lățimea ei cea mai mică / 4 (§10.12; nu mai puțin de 5 cm); o latură comună a două bucăți se taie la cea mai mică dintre ele (`MeshPiece.Size`).
- **Ce devine placă**: plăcile și pereții de **beton** cu E (`StructuralMaterials`, Kind „concrete”). O placă de alt material (lemn) rămâne la regula de 45°, un perete de zidărie sau de alt material reazem liniar (§10.12.3); spus în note. O bucată fără arie, neplană sau neconvexă e lăsată afară cu o notă; dacă mesherul eșuează, totul se face ca la S1, spus.
- **Normalele**: placa în sus, peretele la dreapta direcției lui (x de-a lungul peretelui, y în sus). Suprafața (`ShellElement.Group`) e elementul (GlobalId) la plăci, elementul și segmentul liniei lui la pereți.
- **Reazemele**: nodurile plasei de pe un perete care e reazem liniar sunt ținute ca nodurile lui; talpa unui perete-placă ia condiția fundației pe toată lungimea; fără nimic dedesubt (nici fundație, nici placă, nici bară) e reazem liniar (translațiile), spus.
- **Barele**: tăiate la nodurile plasei de-a lungul lor, după încărcări (încărcările se pun pe bara întreagă, apoi se împart pe bucăți după poziție); prima bucată își păstrează indicele, celelalte vin la urmă; eliberările doar la capetele reale, brațul unui capăt excentric interpolat liniar; grinzile sub o placă-element pe brațe rigide `OffsetZ`. Rezultatele, stațiile și diagramele se recompun pe membrul întreg (`StructuralProblem.Members`, `PieceMap`).
- **Încărcările pe plăci**: pe o bucată acoperită toată, încărcare uniformă pe fiecare element (`CaseLoads.Surface`, pe aria reală: q·cos α); acoperită parțial, funcțiile de formă integrate pe partea acoperită (evantai de triunghiuri, trei puncte pe fiecare); liniile pe placă în bucăți de cel mult jumătate din plasă, trei puncte Gauss fiecare; o forță pe elementul de sub ea (și pe marginea plăcii, la 1 cm). Greutatea proprie a plăcilor și pereților-placă pe elementele lor. Vântul pe o fațadă: pe pereții-placă din planul ei (paraleli la 10°, la cel mult 0,75 m), restul pe rigle și margini ca la §9.9, fiecare fâșie micșorată cu ce au luat pereții.
- **Rezultatele**: `analysis.run` dă `shells` (suprafețe, elemente, patrulatere, triunghiuri, noduri noi, plasa, lista suprafețelor) și `settings`; `analysis.results {…, shellId?}` dă extremele fiecărei suprafețe (w, mx, my, mxy, nx, ny, nxy, vx, vy, netezite cu recuperarea pe petice), iar pentru o suprafață cerută nodurile și centrele elementelor; `maxima.shells` cele mai mari peste tot. `analysis.contours {case|combination|envelope, quantity, shellId?}` dă fiecare suprafață cu nodurile ei (plat, x y z), fețele (patru indici, triunghiul cu ultimul repetat), valorile (la înfășurătoare min și max), grosimea, tipul și normala. Valorile pe noduri ale fiecărui caz se calculează o dată și se combină liniar (recuperarea e liniară). Semnul: al mecanicii; interfața întoarce momentele plăcilor (fața de jos întinsă pozitivă, §10.12.2); la înfășurătoare arată în fiecare nod valoarea mai mare ca mărime.
- **Interfața**: în 3D benzi (10, textură cu filtrare nearest, deci marginea benzii cade între noduri) pe ambele fețe ale fiecărei suprafețe, puțin în afara grosimii, fără să ia raze (alegerea vede modelul); contractul `overlays3d` primește acum `scene` și `invalidate`. Legenda pe card (benzile cu valorile, unitatea, semnul). În **plan** benzile plăcilor etajului vederii, ca suprapunere a ecranului (`planOverlays`); **planșele nu le tipăresc încă**: ar cere ca motorul să deseneze rezultatele prin `VisibilityGraphics` (o categorie „Analysis results” și o vedere care ține cazul și mărimea), rămas pentru o etapă viitoare.
- **Exportul SAV**: o grindă așezată în placa pe care o duce (decizia 357) are profilul mutat cu `OffsetZ` pe axa membrului (`Position` al profilului), deci un program care citește excentricitatea profilului o calculează ca Lapin (axa reală sub placă, legată rigid).

**Cifrele** (`ShellAnalysisTests`): placa 6 × 4 m pe patru reazeme liniare față de seria lui Navier: w 1,013, mx 0,973, my 0,985 la plasa de 0,5 m; la 1 / 0,5 / 0,25 m w 0,977 / 1,013 / 1,028 și my 0,897 / 0,985 / 1,012 (converg spre placa groasă a/t = 20 cu marginile libere la torsiune, puțin peste Kirchhoff). Cadrul din ghid cu placa: Mz în câmpul grinzii lungi 16,6 kN·m față de 27,0 cu regula de 45°, N = 72 kN întindere (conlucrarea în T); placa: w de 2,07 ori placa pe reazeme rigide (grinzile se încovoaie), mx 1,57, my 0,80. Placa dală pe patru stâlpi: fiecare stâlp un sfert, exact.

**Față de ifc_structural_mechanics** (`guide-frame`, plasa lor 0,25 m): sumele 0,03 %, reazemele 5–9 %, nodurile 4,6 %, axele deformate 1,9–3,5 %, N 3–6 % (la §9.8 erau 10–17 % pe reazeme și momente). Forțele tăietoare, momentele și torsiunea grinzilor diferă (13–90 %) pentru că ei leagă grinda de placă doar la translație de-a lungul liniei (o articulație, W-JNT-001), iar la Lapin placa e monolită cu grinda (marginea plăcii se rotește cu ea): o diferență de model, arătată, nejudecată în `fem_check.py`. PyNite nu ia plăci: modelele cu plăci se compară doar cu ifc_structural_mechanics.

**Performanța** (Mac, Release): cadru de 10 etaje × 5 × 5 deschideri de 6 m, plăci de 20 cm: 223.260 de grade de libertate, 960 de bare, 36.000 de elemente de placă; `analysis.run` 5,7 s (rezolvarea 4,8 s), `analysis.contours` pe înfășurătoarea SLU 0,3 s și 1,7 MB de JSON, ~1,1 GB ținuți (`LAPIN_SHELL_TIMING=10x5`). Bloc-P-4 nu e în acest depozit: de cronometrat de utilizator.

**De hotărât de utilizator**: (a) planșele cu rezultatele (prin rezolvitorul de vizibilitate); (b) torsiunea grinzilor de margine cu placa monolită (la ei articulată): a lăsa așa sau o opțiune; (c) pereții de zidărie ca plăci după CR 6 (§10.12.3).

**Găsit la verificare, nerezolvat aici**: `space-frame-rect` iese din toleranțe și față de PyNite (deplasările 1–40 %, forțele la capete până la 99 %) și față de ifc_structural_mechanics (reazemele 6–54 %): la fel pe ramura de dinainte de S2b (d49712f2), deci vine de la nodurile grinzilor de înălțimi diferite la stâlpi (§9.9.4, `JoinAtColumns`: grinda diagonală de 20 × 30 pe braț rigid), nu de la plăci; de cercetat (brațul în `pynite_run.py` sau în solver).

## 11. S3: analiza modală și seismică (specificația)

**Hotărât de utilizator (20.11.2026):** (1) norma seismică implicită e **P100-1/2013**, cu **EN 1998-1** (valorile recomandate) la alegere pe proiect; (2) **pereții de zidărie se calculează ca plăci** (varianta b), cu valorile din CR 6-2013, implicite orientative pe tipurile obișnuite de unități și mortar, schimbate de utilizator pe material; (3) **nicio diafragmă rigidă impusă** când planșeele sunt plăci (o dau ele); diafragma rigidă pe nivel doar ca opțiune pentru modelele fără planșee-placă; (4) S3 e în modulul deschis `analysis`, MIT (decizia 742).

Tot codul e al lui Lapin, MIT, scris după literatura citată (Bathe, „Finite Element Procedures”, 1996/2014, §10–11; Bathe și Wilson, „Large eigenvalue problems in dynamic analysis”, J. Eng. Mech. Div. ASCE 98, 1972; Chopra, „Dynamics of Structures”, cap. 10–13; Der Kiureghian, „A response spectrum method for random vibration analysis of MDF systems”, Earthq. Eng. Struct. Dyn. 9, 1981; Wilson, Der Kiureghian și Bayo, „A replacement for the SRSS method in seismic analysis”, EESD 9, 1981). Benchmark-urile din `hse95/nodal` (MIT) au fost doar inspirație pentru cazurile de test; niciun cod de acolo. Toate valorile normelor sunt **orientative**, ale utilizatorului de schimbat; calculul semnat rămâne al inginerului.

S3 are patru părți, în ordinea în care se fac: **S3-0** pereții de zidărie ca plăci (și în calculul static), **S3a** analiza modală, **S3b** spectrul de răspuns și combinația seismică, **S3c** interfața.

### 11.1 S3-0: zidăria ca placă (CR 6-2013 / EN 1996-1-1)

Până acum (§10.12.3, §10.13) un perete de zidărie era reazem liniar. De acum, cu opțiunea `shells` care cuprinde pereții (`both`, `walls`), **orice perete portant de zidărie devine placă** (MITC4/MITC3, ca betonul), cu un material liniar elastic, izotrop, fără limitare la întindere (spus în note: zidăria nu duce întinderi, S3 nu o modelează; neliniarul e în S5).

**Ce e zidărie:** materialul de tip `masonry` din `StructuralMaterials` (numele sau categoria: brick, masonry, block, cărămidă, zidărie, bloc ceramic, BCA, AAC, Porotherm, GVP, Ytong).

**Valorile** (pe material; cele ale proiectului înaintea celor implicite):
1. **E:** `Pset_MaterialMechanical.YoungModulus` al materialului, dacă există; altfel **E = KE · fk** (EN 1996-1-1 §3.7.2, KE = 1000 valoarea recomandată; CR 6-2013 4.1.2.2.1(3): 1000 la argilă, 800 la BCA, confirmat și luat la S4, §12.10).
2. **fk:** `Lapin_Masonry.fk` (MPa) dacă e dat; altfel **fk = K · fb^0,7 · fm^0,3** (EN 1996-1-1 ec. 3.1, mortar de uz general; fm ≤ 2 fb și fm ≤ 20 MPa), cu K după grupa unității (EN 1996-1-1 Tabelul 3.3, din memorie, de confirmat: argilă grupa 1: 0,55; grupa 2: 0,45; BCA: 0,55; beton grupa 1: 0,55; grupa 2: 0,52).
3. **G = 0,4 E** (EN 1996-1-1 §3.7.3). Elementul de placă izotrop ia G = E / 2(1 + ν): **ν = 0,25** (G = 0,4 E exact), spus.
4. **ρ:** densitatea materialului dacă o are; altfel cea implicită a grupei.
5. Parametrii utilizatorului, ca parametri ai materialului (dialogul materialelor; setul propriu al lui Lapin, nu `Pset_`): `Lapin_Masonry.UnitGroup` (`clay1`, `clay2`, `aac`, `concrete1`, `concrete2`), `fb` (MPa, rezistența normalizată a unității), `fm` (MPa, a mortarului), `fk` (MPa), `KE`.

Implicitele, **orientative**, după numele materialului (și grupa cea mai apropiată):

| Unitate (numele) | Grupa | fb | fm | fk = K fb^0,7 fm^0,3 | E = 1000 fk | ρ (kg/m³) |
|---|---|---|---|---|---|---|
| cărămidă plină (brick, cărămidă, solid) | argilă grupa 1 (K 0,55) | 10 | 5 (M5) | 4,47 MPa | 4,47 GPa | 1800 |
| bloc ceramic cu goluri (GVP, Porotherm, bloc ceramic, hollow, block) | argilă grupa 2 (K 0,45) | 10 | 10 (M10) | 4,50 MPa | 4,50 GPa | 1000 |
| BCA (AAC, Ytong, autoclaved) | BCA (K 0,55) | 4 | 5 | 2,35 MPa | 2,35 GPa | 650 |
| bloc de beton (concrete block) | beton grupa 2 (K 0,52) | 10 | 10 | 5,20 MPa | 5,20 GPa | 1400 |
| zidărie fără alt nume (masonry, zidărie) | ca argila grupa 1 | 10 | 5 | 4,47 MPa | 4,47 GPa | 1800 |

Densitățile sunt ale zidăriei (unitate + mortar), valori uzuale; nu le-am găsit tabelate în CR 6 (§11.9).

**Ce se schimbă în rezultatele existente:** o casă cu pereți de zidărie portanți (cu `shells: both`) nu mai are reazeme liniare la capul pereților: planșeele se duc prin pereți (plăci) la fundații, pereții iau și forțele orizontale (vântul) ca diafragme verticale; reacțiile se mută la tălpile pereților. Greutatea proprie a pereților de zidărie (care până acum „mergea direct în reazeme”, în afara sumelor) intră acum în sume. Testul `Masonry_walls_stay_line_supports_under_a_shell_floor` devine `Masonry_walls_are_shells_under_a_shell_floor` (aceeași casă, reacțiile la fundații, echilibrul). Cu `shells: slabs` totul e ca înainte.

**Test din teorie:** peretele de zidărie în consolă (3 × 6 m, 25 cm, E = 4,47 GPa, ν = 0,25), forța în cap în planul lui: δ = PH³/3EI + PH/(κGA) (încovoiere + forfecare, consola adâncă), la 3 %.

### 11.2 S3a: analiza modală

**Masa** (concentrată în noduri): din **combinația seismică a încărcărilor gravitaționale**, Σ Gk,j + Σ ψ2,i · Qk,i (P100-1/2013 §3.3 / CR 0-2012; EN 1998-1 3.2.4: ψE,i = φ · ψ2,i, cu φ = 1, spus). Fiecare caz își dă vectorul de forțe nodale pe care solverul static îl pune deja (cotele consistente ale plăcilor ∫Nᵢ dA, jumătățile barelor, încărcările concentrate), adică exact masa concentrată a acelui caz; componenta verticală în jos a fiecărui nod, ÷ g, e masa nodului, aceeași pe X, Y, Z (fără inerție de rotație: masa e doar pe translații). ψ2 al cazului e cel al combinațiilor (§8.5, schimbabil pe caz cu `psi`), cu o excepție: **zăpada cu P100 ia ψ2 = 0,4** (CR 0-2012, din memorie, de confirmat), cu EN 0 (EN 1990 Tabelul A1.1 sub 1000 m). Vântul nu are masă (ψ2 = 0). Din cazurile alternative ale zăpezii se ia doar dispunerea neaglomerată. **Masa consistentă** nu se face în S3: masa din încărcări e ce cer P100 și EN 1998, iar matricele consistente ar cere masa fiecărui fel de încărcare pe elemente (rămâne o opțiune viitoare, spusă).

**Rigiditatea:** aceeași problemă ca la calculul static (barele, plăcile, reazemele), cu **rigiditatea fisurată** a betonului și a zidăriei: E × 0,5 implicit (EN 1998-1 4.3.1(7), „jumătate din rigiditatea nefisurată”; P100-1/2013 cap. 8 pentru zidărie, la fel 50 %, de confirmat numărul clauzei), pe bare și pe plăci, schimbabil (`cracked: {concrete, masonry}`). Oțelul și lemnul neschimbate.

**Problema proprie:** K φ = ω² M φ, prin **iterația în subspațiu** (Bathe și Wilson 1972; Bathe §11.6) pe factorizarea Cholesky rară a lui K (`SparseCholesky`, o singură factorizare): q = min(2p, p + 8) vectori de start (diagonala lui M și vectori unitari la cele mai mari raporturi mᵢ/kᵢᵢ, Bathe), la fiecare pas K X̄ = M X, proiecția Kr = X̄ᵀ K X̄, Mr = X̄ᵀ M X̄, problema mică rezolvată prin Cholesky pe Mr și Jacobi ciclic, ortogonalizare față de M; convergența când |λᵢ⁽ᵏ⁾ − λᵢ⁽ᵏ⁻¹⁾| / λᵢ⁽ᵏ⁾ < 10⁻⁸ pe primele p. Fără dependențe; merge și în browser. Gradele de libertate fără masă rămân în K (condensate implicit de iterație). Verificarea: reziduul ‖K φ − ω² M φ‖ / ‖K φ‖ < 10⁻⁶ pe fiecare mod, raportat.

**Câte moduri:** p crește (12, 24, 48, …) până când **masa modală efectivă cumulată ≥ 90 % din masa totală pe fiecare direcție orizontală** și niciun mod rămas nu ar avea peste 5 % (P100-1/2013 §4.5.3.3.1, EN 1998-1 §4.3.3.3.1; regula de 90 % / 5 % confirmată din literatură, numărul clauzei P100 de confirmat), sau până la limita utilizatorului (`modes`, implicit 60, cel mult 300). Dacă limita oprește înainte de 90 %, nota spune cât s-a atins (P100 / EN cer atunci și verificarea cu √n moduri, n numărul de niveluri: raportată, nu impusă).

**Rezultatele** (pe fiecare mod k, φ normalizat la masă, φᵀ M φ = 1): ω, f, T; factorii de participare Γx,y,z = φᵀ M r (r vectorul de influență pe direcție); masa efectivă M*ₖ = Γ²ₖ, procentul și cumulul; **verificarea Rayleigh** (ω² = φᵀ K φ, raportată); Σₖ M*ₖ pe toate modurile = masa totală (test). Masa totală pe direcție și masa care cade pe reazeme (nodurile ținute) spuse separat.

**Diafragma rigidă** (opțiunea `diaphragm: "rigid"`, implicit `none`): numai pentru nivelurile care **nu au planșeu-placă** (un planșeu de lemn, de exemplu); nodurile nivelului (cota planșeului, ±0,5 m) se leagă de un nod principal în centrul maselor prin constrângeri prin penalizare (ux, uy, rz ale corpului rigid în plan, α = 10⁴ × cea mai mare rigiditate de pe diagonală), nodul principal ținut pe uz, rx, ry. Un nivel cu planșeu-placă nu primește niciodată diafragmă (decizia 3), spus în note.

### 11.3 S3b: spectrul de răspuns

**Parametrii amplasamentului** pe IfcProject (`seismic`), ai utilizatorului (Lapin nu are harta de zonare, ca la s0,k și qb, §9.9), scriși de `analysis.setSeismic` (un pas de undo; null șterge, absent lasă), citiți cu `analysis.seismicOptions`:

| Cheie | P100-1/2013 (implicit) | EN 1998-1 |
|---|---|---|
| `code` | `P100-1/2013` | `EN 1998-1` |
| `ag` | ag/g pe amplasament (harta P100 Figura 3.1, IMR 225 ani) | agR/g (harta anexei naționale) |
| `tc` | TC ∈ {0,7; 1,0; 1,6} s (harta P100 Figura 3.2) | — |
| `ground`, `spectrum` | — | terenul A–E, tipul 1 sau 2 |
| `importance` | clasa I–IV: γI,e 1,4 / 1,2 / 1,0 / 0,8 | clasa I–IV: γI 0,8 / 1,0 / 1,2 / 1,4 |
| `q` | factorul de comportare (implicit 1,5; cap. 8 pentru zidărie) | idem (Tabelul 9.1 pentru zidărie) |
| `damping` | ξ = 0,05 | idem |
| `modal` | `cqc` (implicit) sau `srss` | idem |
| `directions` | `30%` (implicit) sau `srss` | idem |
| `torsion` | excentricitatea accidentală 0,05 (×L pe nivel) | idem |
| `modes` | limita modurilor (60) | idem |
| `cracked` | `{concrete: 0,5, masonry: 0,5}` | idem |
| `diaphragm` | `none` / `rigid` | idem |
| `drift` | `{sls: 0,005, uls: 0,025, nu: 0,5}` | `{sls: 0,005, nu: 0,5}` |

**Spectrul P100-1/2013** (cap. 3; formele confirmate din literatura care citează norma, numerele ecuațiilor de confirmat): β0 = 2,5; TB = 0,2 TC; TD = 3 s pentru TC = 0,7 și 1,0 s, 2 s pentru TC = 1,6 s (Tabelul 3.1). β(T) = 1 + (β0 − 1) T/TB pe [0, TB], β0 pe [TB, TC], β0 TC/T pe [TC, TD], β0 TC TD/T² peste TD; Se(T) = ag β(T). **Spectrul de proiectare:** Sd(T) = ag [1 + (β0/q − 1) T/TB] pe (0, TB]; Sd(T) = ag β(T)/q peste TB, **cel puțin 0,2 ag**. Forțele: γI,e · Sd(Tk) · M*ₖ.

**Spectrul EN 1998-1** (3.2.2.5, valorile recomandate): S, TB, TC, TD după teren și tip (Tabelele 3.2 și 3.3); ag = γI agR; Sd = ag S [2/3 + T/TB (2,5/q − 2/3)] pe [0, TB]; ag S 2,5/q pe [TB, TC]; ag S 2,5/q · TC/T ≥ 0,2 ag pe [TC, TD]; ag S 2,5/q · TC TD/T² ≥ 0,2 ag peste TD (ec. 3.13–3.16, β = 0,2).

**Răspunsul unui mod** (pe direcția d): uₖ = Γₖ,d · Sd(Tk) · γ / ωₖ² · φₖ; din el, prin aceleași funcții ca la static (fără încărcări pe bare), eforturile pe bare și pe plăci, reacțiile, deplasările.

**Combinarea modurilor:** **CQC** (Der Kiureghian 1981) cu ρij = 8ξ²(1 + r) r^1,5 / ((1 − r²)² + 4ξ² r (1 + r)²), r = Tj/Ti, ξ egal; **SRSS** la alegere. P100 și EN cer CQC când modurile nu sunt independente (EN: Tj ≤ 0,9 Ti; proiectul P100/2025 un criteriu cu ξ); Lapin ia CQC implicit (acoperă ambele cazuri: ρij → 0 pentru moduri depărtate), și spune în note câte perechi de moduri sunt apropiate (Tj > 0,9 Ti).

**Torsiunea accidentală** (P100-1/2013 §4.5.2.2 / EN 1998-1 §4.3.2, e = ±0,05 L): pe fiecare nivel, forța seismică a nivelului (diferența forțelor tăietoare de nivel, combinate) × e (L latura nivelului perpendiculară pe direcție, din înfășurătoarea nodurilor nivelului) dă un moment de torsiune, pus ca forțe orizontale pe nodurile nivelului, proporționale cu masa și cu distanța la centrul maselor (ca la o rotire rigidă), un caz static pe direcție. Efectul lui (în valoare absolută) se adună la răspunsul modal (EN 1998-1 §4.3.3.3.3). Spus.

**Cele două direcții:** EEdx „+” 0,3 EEdy și 0,3 EEdx „+” EEdy (P100 §4.5.3.6 / EN 4.3.3.5.1, regula de 30 %, implicit), sau √(EEdx² + EEdy²) la alegere. Componenta verticală nu se ia în S3 (spus).

**Pe nivel** (nivelurile proiectului; cota fiecăruia, nodurile la ±0,5 m de ea): masa, **forța tăietoare de nivel** pe fiecare direcție (Σ forțele modale de deasupra, combinate CQC pe moduri), forța seismică a nivelului, **deplasarea relativă de nivel** dr,e (media deplasărilor nodurilor nivelului minus a celui de dedesubt, pe fiecare mod, apoi combinată CQC; și cea mai mare a unui nod), cu verificările din Anexa E a P100 (de confirmat din textul normei; aceleași ca în P100-1/2006): **SLS:** dr,SLS = ν · q · dr,e ≤ 0,005 h (ν = 0,5); **ULS:** dr,ULS = c · q · dr,e ≤ 0,025 h, c = 3 − 2,3 T1/TC, 1 ≤ c ≤ 2 (T1 perioada modului fundamental pe direcție). Cu EN: dr = q · dr,e (4.3.4), verificat ν dr ≤ 0,005 h (4.4.3.2(1)a, ν = 0,5). Și forța tăietoare de bază totală, comparată cu forța statică echivalentă Fb = γ Sd(T1) m λ (λ = 0,85 pentru T1 ≤ 2 TC și mai mult de două niveluri, altfel 1), ca verificare.

**Combinația seismică** (CR 0-2012 / EN 1990 6.4.3.4): **Σ Gk,j + Σ ψ2,i Qk,i ± AEd**, cu ψ2 ca la masă; o înfășurătoare nouă, `seismic`, cu min și max în fiecare punct (static ± răspunsul combinat), în `analysis.results` și `analysis.diagram` (`envelope: "seismic"`), și **adăugată la înfășurătoarea `uls`** când parametrii seismici sunt dați (situația de proiectare seismică e una de stare limită ultimă). Cheia combinației: `seismic`.

### 11.4 Interogările și comenzile

- `analysis.setSeismic {code?, ag?, tc?, ground?, spectrum?, importance?, q?, damping?, modal?, directions?, torsion?, modes?, cracked?, diaphragm?, drift?}` (comandă, un pas de undo; `null` pe tot obiectul șterge parametrii; permisă și invitaților, ca `analysis.setOptions`).
- `analysis.seismicOptions {}` → parametrii, implicitele, tabelele (TC, terenurile EN, clasele de importanță), factorii q ai zidăriei după P100 Tabelul 8.10 (orientativ).
- `analysis.modal {}` → `{ok, modes: [{n, omega, f, T, gamma: [x, y, z], mass: [x, y, z], ratio: [x, y, z], cumulative: [x, y, z], rayleigh, residual}], totalMass: [x, y, z], supportMass, dofs, ms, notes}`.
- `analysis.mode {mode, scale?}` → forma modului pentru animație: polilinii pe bare (ca `deformed`) și nodurile plăcilor deplasate (`shells: [{id, nodes, displaced}]`), scara, în coordonatele lumii.
- `analysis.spectrum {}` → `{code, points: [{T, Sd, Se}], TB, TC, TD, ag, q, gamma, modes: [{n, T, Sd}]}`, ca graficul să-l deseneze.
- `analysis.seismic {}` → `{ok, base: {x, y}, equivalent: {x, y}, storeys: [{id, name, z, h, mass, shear: [x, y], force: [x, y], drift: [x, y], driftMax: [x, y], sls: [x, y], uls: [x, y], slsOk, ulsOk}], closeModes, notes}`.
- Calculul e o interogare, derivat, ținut în memorie după versiunea modelului ca `analysis.run`; nimic scris în `.lapin`.

### 11.5 Interfața (S3c)

În dialogul de rezultate, o secțiune **Seismic**: parametrii (norma, ag, TC sau terenul și tipul, clasa de importanță, q, combinarea modurilor, direcțiile, diafragma), tabelul modurilor (T, f, masele efective pe X și Y și cumulul), **graficul spectrului** (Sd și Se, cu perioadele modurilor marcate), tabelul nivelurilor (masa, forța tăietoare, deplasarea relativă, verificările SLS/ULS colorate), și **animația modului** ales în 3D (forma modului oscilând, ca deformata). Înfășurătoarea `seismic` apare printre înfășurători. Toate textele în toate limbile din `ui/src/i18n`; notele traduse ca celelalte (§9.9.6).

### 11.6 Testele (cifrele din teorie)

`ModalTests`, `SpectrumTests`, `SeismicAnalysisTests`, `MasonryShellTests`:
- **Consola** (bară Euler–Bernoulli/Timoshenko subțire, 10 elemente, masa concentrată din greutate): ω1 = 1,8751² √(EI/mL⁴) (cu masa concentrată în 10 noduri, la 1,5 %); **grinda simplu rezemată**: ωn = (nπ)² √(EI/mL⁴), primele trei moduri verticale, la 1 % pe plasa fină.
- **Clădirea cu forfecare** cu trei niveluri (masele m, rigiditățile k, stâlpi foarte rigizi la încovoiere ai grinzilor): perioadele exacte ale matricei 3 × 3 (Chopra, Ex. 10.x), la 1e-6; factorii de participare și masele efective exacte; Σ M*ₖ = masa totală.
- **Rayleigh:** φᵀ K φ = ω² pe fiecare mod (1e-8).
- **Spectrul P100** la TB, TC, TD, la T = 0, sub limita 0,2 ag (TC = 0,7 s, ag = 0,30 g, q = 3): valorile de mână; EN tipul 1 teren C la fel.
- **CQC / SRSS:** două moduri cu răspunsuri cunoscute, perioade depărtate (CQC ≈ SRSS) și apropiate (CQC după formula lui Der Kiureghian, calculată de mână).
- **Răspunsul spectral** al oscilatorului cu un grad de libertate: F = m Sd(T); forța tăietoare de bază a clădirii cu forfecare față de calculul de mână pe moduri.
- **Peretele de zidărie** în consolă: δ = PH³/3EI + PH/(κGA), la 3 %.
- **Cadrul din ghid**: modurile, echilibrul combinației seismice, înfășurătoarea `seismic` între min și max.
- **Comenzile**: `analysis.setSeismic` un pas de undo, valorile refuzate în afara domeniului.

### 11.7 Ce s-a hotărât la implementare

Decizia 744 (20.11.2026, ramura `feat/fem-s3`):

- **Materialul zidăriei** (`Lapin.Platform/Analysis/StructuralMaterials.cs`, `Masonry`): grupa unității din nume (BCA/AAC/Ytong → `aac`; bolțar, bloc de beton → `concrete2`; GVP, Porotherm, bloc ceramic, hollow → `clay2`; brick, cărămidă → `clay1`; masonry, zidărie, block, bloc → `clay1`), valorile proprii ale materialului înainte (`Lapin_Masonry.UnitGroup/fb/fm/fk/KE`, sau `Pset_MaterialMechanical.YoungModulus`), fm limitat la min(2 fb, 20). Detecția zidăriei vine acum înaintea celei generice „concrete/beton”, ca „beton celular” (BCA) să fie zidărie. ν = 0,25 (G = 0,4 E). Sursa fiecărei valori e scrisă (ca la beton), deci și exportul SAV/JG scrie E-ul zidăriei.
- **Rezultatele schimbate:** cu `shells: both` pereții de zidărie sunt plăci; testul S2b care cerea reazeme liniare e acum `Masonry_walls_are_shells_under_a_shell_floor` (aceeași casă 6 × 4: reacțiile la tălpile pereților, Q-A 48 kN echilibrat, greutatea pereților în sume). Restul suitei (4773 de teste înainte de S3, 4787 cu S3) a trecut neschimbat: niciun alt test nu avea pereți de zidărie portanți calculați.
- **Peretele de zidărie în consolă** 3 × 6 m, 25 cm (cărămidă, E = 4,47 GPa): δ / (PH³/3EI + PH/κGA) = 0,9917 la plasa de 0,25 m.
- **Solverul** (`FrameSystem`): asamblarea și factorizarea scoase din `FrameSolver.Solve` într-un sistem păstrat (rezolvă un caz, un vector de încărcări, rezultatele unei deplasări fără încărcări, K·u, diagonala), folosit de analiza modală; `FrameProblem.Links` (cuplaje prin matrice, pentru diafragmă). Rezultatele statice identice (toată suita trece).
- **Masa** din vectorii de forțe nodale ai cazurilor (aceiași pe care îi rezolvă calculul static), deci cotele consistente ale plăcilor și jumătățile barelor; nodurile cu încărcare netă în sus n-au masă (spus).
- **Iterația în subspațiu**: proiecțiile Kr = X̄ᵀ M X, Mr = X̄ᵀ M X̄ pe gradele cu masă, Cholesky pe Mr (vectorii dependenți scoși), Jacobi; când modurile cresc (12 → 24 → 48 …), pornește de la cele găsite. Toleranța 10⁻⁸ pe valorile proprii (spec). Reziduul modurilor înalte ~10⁻⁶–10⁻⁵ (vectorii converg mai încet decât valorile), raportat pe fiecare mod.
- **Analiza seismică e o interogare separată** (`SeismicRun`, ținută după versiunea modelului ca `AnalysisRun`); cere calculul static întâi. Combinația seismică: valorile statice ale încărcărilor gravitaționale (rigiditatea nefisurată) ± AEd (rigiditatea fisurată), în fiecare punct și pe fiecare mărime separat (două „seturi” `seismic/+`, `seismic/-` în înfășurători); și pe plăci (valorile recuperate ale fiecărui mod, combinate în noduri). Valorile din centrele elementelor de placă (`analysis.results {shellId}`) nu au încă partea seismică.
- **Oprirea analizei seismice:** `ag: null` (nu un obiect `null` întreg, cum spunea §11.4).
- **Nivelurile:** înălțimea nivelului e de la cota lui la a următorului; masa unui nod merge la nivelul în care cade cota lui (placa la 2,9 m e a parterului de 3 m); deplasarea unei cote e media nodurilor celor mai apropiate de ea (sub 0,5 m: planul median al plăcii).
- **Diafragma rigidă:** prin penalizare (α = 10⁴ × cea mai mare rigiditate axială EA/L sau E·t), un nod principal în centrul maselor nivelului; pe cadrul din ghid cu planșeu de lemn, capetele stâlpilor se mișcă împreună pe X la 10⁻³.
- **Verificări în teste** (`ModalTests`, `SpectrumTests`, `SeismicAnalysisTests`, `ShellAnalysisTests`): clădirea cu forfecare cu trei niveluri, ω exact la 10⁻⁸; consola 0,99886 (ω1) și 0,99603 (ω2) cu 20 de mase; grinda simplu rezemată 1,00000 pe primele trei; Rayleigh 1 la 10⁻⁸; Σ M* = masa totală; spectrul P100 la T = 0, TB, TC, 1,4 s, TD (unde limita 0,2 ag dă 0,06 g în loc de 0,0583 g), spectrul EN tip 1 teren C; ρ(r = 0,9, ξ = 5 %) = 0,473028; pe cadrul din ghid masa = (G1 + 0,3 Q-A)/g la 10⁻⁴, forța tăietoare de bază = CQC al M*ₖ Sd(Tₖ), perioadele × √2 cu rigiditatea fisurată 0,5.
- **Performanța** (de măsurat mai departe): casa de zidărie 6 × 4 cu pereții-placă (1.902 grade de libertate) cere 48 de moduri pentru 90 % (modurile locale ale pereților în afara planului au masă mică) și ~8 s în Debug, cu mașina încărcată; de grăbit (Jacobi pe tablouri plate, deplasarea spectrală / Lanczos) înainte de modele mari.

### 11.8 Surse

- P100-1/2013: formele spectrului, β0 = 2,5, TB/TC/TD (Tabelul 3.1), Sd ≥ 0,2 ag și regula 90 % / 5 % citite din Savu, Romanian J. Transport Infrastructure 2020 (https://reference-global.com/download/article/10.2478/rjti-2020-0007.pdf), care citează norma; factorii q ai zidăriei (Tabelul 8.10) din teza lui Partene, UPT 2018 (https://dspace.upt.ro/jspui/bitstream/123456789/213/3/BUPT_TD_Partene.pdf). Textul normei: https://mobee.infp.ro/images/content-images/Despre-cutremure/Efectele-cutremurelor/P100-1-2013.pdf (nu l-am putut deschide: certificatul serverului nu se potrivește).
- EN 1996-1-1 §3.7.2 (E = 1000 fk), §3.7.3 (G = 0,4 E); EN 1998-1 §4.3.1(7) (rigiditatea fisurată, jumătate); P100-1 proiectul 2025 (https://technical-regulation-information-system.ec.europa.eu/ro/notification/26527/text/D/RO) pentru criteriul de independență a modurilor și rigiditatea fisurată a zidăriei (50 %).

### 11.9 De hotărât de utilizator

1. **Valorile de confirmat din textul normelor** (n-am putut citi P100-1/2013 și CR 6-2013): γI,e (1,4 / 1,2 / 1,0 / 0,8), ψ2 al zăpezii în combinația seismică (0,4), formulele Anexei E (ν = 0,5, 0,005 h, c = 3 − 2,3 T1/TC, 0,025 h), numerele clauzelor; KE al CR 6 (1000?), K-urile lui fk (sau tabelele CR 6 ale lui fk după fb și mortar), αu/α1 pentru zidărie, densitățile zidăriei.
2. **Rigiditatea fisurată a betonului** în analiza seismică: 0,5 (EN 1998-1 4.3.1(7)) ca implicit, sau 1,0?
3. **q implicit** când utilizatorul nu-l dă: 1,5 (cel mai mic pentru zidăria nearmată) sau obligatoriu de dat?
4. **ψE = φ ψ2** cu φ < 1 (EN 4.2.4: 0,5 / 0,8 pe niveluri) sau φ = 1 (acum)?
5. **P100-1/2025** (notificat UE în decembrie 2024): când intră, trecem pe el?
6. **Grupa unității după nume** (§11.7): potrivirea e bună pentru numele obișnuite (cărămidă, GVP, BCA); un material cu alt nume trebuie să aibă `Lapin_Masonry.UnitGroup`. Vreți o listă mai lungă sau o alegere în dialogul materialului?
7. **Forțele pe pereții de zidărie** (forța tăietoare și momentul fiecărui perete, pentru verificarea după CR 6) rămân la S4, sau le vreți deja în tabelul nivelurilor?
8. **Performanța modelelor mari** cu pereți-placă (multe moduri locale): acceptați ca limita implicită să rămână 60 de moduri, cu nota când 90 % nu e atins?

**Răspunsul utilizatorului (20.11.2026, „mergem pe recomandările tale”):** (1) valorile rămân cum sunt, marcate orientative, până le confirmă utilizatorul din textul normelor; (2) betonul fisurat 0,5; (3) q implicit 1,5, cu nota că e cel mai mic; (4) φ = 1, de partea siguranței; (5) P100-1/2025 când intră în vigoare; (6) grupa după nume rămâne, cu `Lapin_Masonry.UnitGroup` pentru celelalte; alegerea în dialogul materialului mai târziu; (7) forțele pe pereții de zidărie la S4; (8) 60 de moduri, cu nota.

### 11.10 Corecturi după filmare

Patru probleme găsite la filmarea tutorialului de calcul seismic (20.11.2026), corectate pe ramura `fix/fem-display`:

1. **Forma modului se citea greu**: era doar un contur portocaliu, iar modelul gri nedeformat era desenat peste el, așa că oscilația unei case mici de zidărie abia se vedea. Acum plăcile și pereții deformați sunt suprafețe umbrite (opace, cu rețeaua de elemente schițată), barele tuburi umbrite (raza 0,4 % din diagonala modelului, cel puțin 2,5 cm); modelul în repaus e desenat transparent (opacitate 0,12) în spatele formei, sau ascuns (**Arată nedeformat** debifat). Amplitudinea se dimensionează în interfață: deplasarea cea mai mare a formei = 7 % din diagonala cutiei modelului (`MODE_FRACTION`, între 5 și 10 %), înmulțită cu **Scalează** de pe card (×0,25 … ×4, ca la diagrame). Forma statică deformată (Deformed) nu are cod comun cu ea: e o diagramă SVG desenată peste model, care nu poate fi acoperită, așa că rămâne cum e.
2. **Secțiunea Seismic**: acțiunile (Rulează din nou, Arată diagramele, Închide fără diagrame, și modul de animat cu **Animă**) stau într-un subsol fix al dialogului (`dialog-foot`), la îndemână fără derulare; notele calculului și cele ale analizei modale și seismice sunt strânse în câte un **Note (n)** închis, cele de verificat primele (cu roșu; ce cere privirea inginerului: refuzat, lăsat afară, neechilibrat, 90 % neatins, pereți luați ca portanți…).
3. **Stop ștergea cardul analizei**: animarea ascundea diagramele și oprirea nu le mai aducea. Acum oprirea readuce cardul cum era când a pornit animarea (afișat sau nu); la fel când motorul nu dă forma.
4. **Pereții de zidărie intrau în calcul doar marcați portanți** (Pset_WallCommon.LoadBearing = true): cine nu bifa nimic nu avea pereți de zidărie în calcul, fără să afle. Regula nouă a modelului analitic (`AnalyticalModel.UnsaidLeftOut`), pentru toți pereții, nu doar zidărie: LoadBearing **false** (pe perete sau pe tip), sau tipul IFC PARTITIONING → nu e structural; **true** → structural, orice material și grosime; **nespus** → structural dacă e din zidărie sau beton (după `StructuralMaterials`) cu miezul de cel puțin **10 cm** (`PartitionThickness`), altfel perete despărțitor (greutatea lui pe planșee, ca înainte). Două note spun ce s-a hotărât: pereții nespuși luați ca portanți (ca utilizatorul să debifeze despărțitorii dintre ei) și cei nespuși sub 10 cm luați ca despărțitori. Pragul de 10 cm: un despărțitor de cărămidă de 7,5 cm sau de BCA de 5–7,5 cm nu poartă; de la 10 cm în sus (BCA 10, cărămidă 11,5) un perete poate purta, iar nota îl arată. Aceeași regulă o folosesc exporturile modelului analitic (JG Structures, IFC Structural Analysis View), fiindcă au același model.

**Ce se schimbă la modelele existente**: un proiect cu pereți de zidărie sau beton de cel puțin 10 cm fără LoadBearing dă alte rezultate: pereții aceia intră acum în calcul (plăci sau reazeme liniare, după opțiuni) în loc să fie încărcări din pereți despărțitori pe planșee, iar exportul JG / IFC structural îi scrie. Un proiect care bifase LoadBearing pe toți pereții portanți și îl lăsase nespus pe despărțitorii groși trebuie să debifeze LoadBearing pe aceia. Testele care se bazau pe vechea regulă au fost schimbate ca să marcheze explicit peretele despărțitor (`DerivedLoadTests.A_partition_is_its_bodys_weight_along_its_axis_at_its_base`, `JgExportTests.Storeys_and_kinds_chosen_leave_the_others_out_and_say_so`); regula nouă e în `ShellAnalysisTests.A_masonry_or_concrete_wall_that_says_nothing_of_its_load_bears_it_unless_thinner_than_a_partition`.

## 12. S4: verificările după normă (specificația)

**Hotărât de utilizator (20.11.2026):** S4 e în modulul deschis `analysis`, MIT (decizia 742); forțele pe pereții de zidărie (forța tăietoare și momentul fiecărui perete) și verificările CR 6 sunt în S4 (§11.9, răspunsul 7). Implicit practica românească: Eurocodurile cu anexele naționale române (SR EN 1992-1-1/NA, SR EN 1993-1-1/NA, SR EN 1995-1-1/NA) și CR 6-2013 pentru zidărie; unde valoarea anexei nu s-a putut citi, valoarea recomandată EN, spusă (§12.9).

Tot codul e al lui Lapin, MIT, scris după textul normelor și literatura citată la §12.8. **Rezultatele sunt orientative**: interfața o spune pe fiecare ecran cu verificări; calculul semnat rămâne al inginerului.

### 12.1 Ce face S4

Verificările se **derivă la fiecare cerere** din rezultatele calculului (S1–S3), niciodată stocate (ținute în memorie după versiunea modelului, ca `AnalysisRun`). Pentru fiecare membru (bară), placă și perete: fiecare verificare dă un **grad de utilizare** (efect / rezistență), combinația care o guvernează (cheia și numele ei), punctul de pe membru, valorile intermediare și clauza normei. Utilizarea membrului e cea mai mare dintre verificările lui. Combinațiile: toate cele SLU (§8.5) și, când parametrii seismici sunt dați, cele două margini ale combinației seismice (§11.3, `seismic/+`, `seismic/-`) pentru verificările de rezistență; cele SLS pentru săgeți. Eforturile unei bare se citesc în 11 stații și în extremele exacte ale fiecărei combinații.

Materialul decide ce se verifică (`StructuralMaterials`, Kind): `steel` → §12.2, `concrete` → §12.3, `timber` → §12.4, pereții de `masonry` → §12.5. Altceva: spus, neverificat.

### 12.2 Oțel (SR EN 1993-1-1)

Coeficienții parțiali γM0 = γM1 = 1,0, γM2 = 1,25 (valorile recomandate; anexa română le ia, după lista SCIA a anexelor, §12.9). fy după grad (Tabelul 3.1, t ≤ 40 mm; fy-ul materialului dacă îl are), E = 210 GPa, G = 80,77 GPa.

1. **Clasa secțiunii** (Tabelul 5.2), ε = √(235/fy): inima (parte internă) la încovoiere 72ε / 83ε / 124ε, la compresiune 33ε / 38ε / 42ε, între ele prin α (poziția axei neutre plastice sub N, formulele tabelului pentru c/t ≤ 396ε/(13α − 1) și 36ε/α, clasa 3 cu ψ); talpa în consolă comprimată 9ε / 10ε / 14ε; țeava rotundă d/t 50ε² / 70ε² / 90ε²; cornierul doar clasa 3 (h/t ≤ 15ε, (b + h)/2t ≤ 11,5ε). c = h − 2tf − 2r la inimă, (b − tw − 2r)/2 la talpă. Clasa e a combinației (N comprimant schimbă α); **clasa 4 nu e calculată** (lățimile eficace, EN 1993-1-5): verificarea e spusă „clasa 4: neverificat”, cu utilizarea elastică ca orientare.
2. **Rezistența secțiunii** (6.2): Nc,Rd = A fy/γM0 (Nt,Rd la fel), Vpl,Rd = Av (fy/√3)/γM0 pe fiecare axă (Av din `SectionProperties`), Mc,Rd = Wpl fy/γM0 (clasele 1–2) sau Wel fy/γM0 (clasa 3) pe ambele axe. Interacțiunea: **liniară** (6.2.1(7)) N/NRd + My/MRd,y + Mz/MRd,z ≤ 1 ca regulă generală; la profilele I/H clasa 1–2 criteriul plastic 6.2.9.1 (MN,y,Rd = Mpl,y(1 − n)/(1 − 0,5a), MN,z,Rd după n față de a, (My/MN,y)^2 + (Mz/MN,z)^(5n ≥ 1) ≤ 1); forfecarea mare (V > 0,5 Vpl,Rd) reduce fy la (1 − ρ) fy pe aria de forfecare (6.2.8), ρ = (2V/Vpl − 1)².
3. **Flambajul** (6.3.1): χ după curba (Tabelul 6.2: I laminat h/b > 1,2, tf ≤ 40: y–y a, z–z b; h/b ≤ 1,2: b și c; țeava laminată la cald a, formată la rece c; U, T, plin c; cornier b; α = 0,13 / 0,21 / 0,34 / 0,49 / 0,76), λ̄ = √(A fy/Ncr), Ncr = π²EI/Lcr². **Lungimile de flambaj:** Lcr = k · L, L lungimea membrului (bara modelului analitic între două noduri), k din eliberările capetelor pe acea axă: încastrat–încastrat 0,7 (practic, nu 0,5), încastrat–articulat 0,85, articulat–articulat 1,0, consolă 2,0 (un capăt liber); **utilizatorul îl schimbă** pe element (`ky`, `kz` sau `Lcr,y`, `Lcr,z` în metri). Barele cadrelor cu noduri deplasabile au k > 1: Lapin nu-l deduce, spus (de dat de utilizator).
4. **Flambajul lateral** (6.3.2): Mcr = C1 π²EIz/L² √(Iw/Iz + L²GIt/(π²EIz)) (NCCI SN003, k = kw = 1, încărcarea în centrul de forfecare), L lungimea nerezemată lateral (implicit lungimea membrului; `Llt` de utilizator), C1 din forma momentului: liniar după ψ (tabelul SN003: 1,00; 1,14; 1,31; 1,52; 1,77; 2,05; 2,33; 2,57; 2,55 pentru ψ = 1 … −1, interpolat), sub o încărcare uniformă pe deschiderea simplu rezemată 1,127, la forța la mijloc 1,348 (forma recunoscută după raportul momentului din câmp față de cel de la capete). χLT după **6.3.2.3** pentru profile laminate (λ̄LT,0 = 0,4, β = 0,75, curba b pentru h/b ≤ 2, c peste; f cu kc = 1/(1,33 − 0,33ψ) la moment liniar, 0,94 la încărcarea uniformă, 0,90 la forța la mijloc, χLT,mod = χLT/f ≤ 1) și 6.3.2.2 pentru celelalte I (curba c/d sudate, d altele). Doar profilele I/H dublu simetrice; țevile (rotunde și rectangulare) nu flambează lateral; U, T, L, Z, C: spus „flambajul lateral nefăcut pentru această formă”.
5. **Interacțiunea** (6.3.3, ec. 6.61–6.62) cu factorii **Anexei B (metoda 2)**: kyy, kzz, kyz, kzy din Tabelele B.1 (secțiuni nesusceptibile la deformații de torsiune: țevile, și I-urile cu `Llt` = 0) și B.2 (I-urile susceptibile), Cm din Tabelul B.3 (0,6 + 0,4ψ ≥ 0,4 liniar; cu încărcare în deschidere după αh/αs). **Anexa română alege metoda 1 (Anexa A)** după lista SCIA (§12.9): Lapin face metoda 2 deocamdată, spus, de hotărât.

Formele: I/H (simetric), I nesimetric (doar secțiunea și flambajul, fără flambaj lateral), țeava rectangulară și rotundă, U, T, L (secțiunea, flambajul după axele principale, fără flambaj lateral și torsional), formele formate la rece C/Z/SIGMA și conturul oarecare: **neverificate** (EN 1993-1-3), spus.

### 12.3 Beton armat (SR EN 1992-1-1)

γc = 1,5, γs = 1,15, αcc = 1,0 (valoarea recomandată, luată și de anexa română după SCIA, §12.9); fcd = αcc fck/γc; armătura B500 (fyk = 500 MPa) dacă proiectul nu spune altfel; stratul de acoperire 30 mm până la axa etrierului + ½ Ø20, deci d = h − 50 mm, schimbabil pe proiect (`cover`, `bar`, `stirrup`). Diagrama dreptunghiulară λ = 0,8, η = 1 (fck ≤ 50). Totul pe secțiunea dreptunghiulară a barei; altă formă: spus, neverificat.

**Grinzile** (bare orizontale sau înclinate sub 45°, cu momentul pe axa tare dominant):
- **Armătura longitudinală** pentru MEd (pe fiecare față, după semn): K = M/(b d² fck), K' = 0,167 (x/d ≤ 0,45, 5.6.3); K ≤ K': z = d(0,5 + √(0,25 − K/1,134)) ≤ 0,95 d, As = M/(fyd z); K > K': armătură dublă, As2 = (K − K') fck b d²/(fyd (d − d2)), As = K' fck b d²/(fyd z') + As2. Cu As,min = max(0,26 fctm/fyk, 0,0013) b d (9.1N) și As,max = 0,04 Ac (9.2.1.1(3)); fctm = 0,30 fck^(2/3) (Tabelul 3.1). Momentul pe axa slabă: aceeași regulă pe lățime (spus).
- **Forfecarea** (6.2): VRd,c = [CRd,c k (100 ρl fck)^(1/3)] bw d ≥ vmin bw d, CRd,c = 0,18/γc, k = 1 + √(200/d) ≤ 2, vmin = 0,035 k^1,5 fck^0,5, ρl ≤ 0,02 din armătura necesară; VEd > VRd,c → etrierii necesari Asw/s = VEd/(z fywd cot θ) cu cot θ cel mai mare din [1; 2,5] pentru care VEd ≤ VRd,max = bw z ν1 fcd/(cot θ + tan θ), ν1 = 0,6(1 − fck/250), z = 0,9 d; peste VRd,max la cot θ = 1: secțiunea nu ajunge. Minimul ρw,min = 0,08 √fck/fyk (9.5N). Torsiunea: nefăcută, spusă.
- **Săgeata (SLS) prin raportul deschidere/înălțime utilă** (7.4.2, ec. 7.16a/b, K din Tabelul 7.4N: simplu rezemată 1,0, capăt de grindă continuă 1,3, câmp interior 1,5, consolă 0,4), ρ0 = √fck·10⁻³, ρ = As,nec/(b d), ρ′ = As2; sistemul din eliberări și din vecini (un capăt liber: consolă; ambele capete continue: interior), schimbabil. Plus săgeata elastică din calcul față de L/250 (SLS cvasipermanentă) ca informație, nu ca verificare (rigiditatea nefisurată).
- **Legătura cu armătura** (modulul `rebar`): dacă tipul elementului are etrieri (`stirrups`: diametrul, pasul, ramurile după model) sau bare (`bars` la stâlpi: pe față, diametrul), Lapin compară aria pusă cu cea necesară (Asw/s pus față de necesar; As pus față de necesar) și o spune ca verificare în plus („armătura pusă”). Lapin nu pune bare singur.

**Stâlpii** (bare verticale sau înclinate peste 45°): N + Mz + My, cu **excentricitatea minimă** e0 = max(h/30, 20 mm) pe fiecare axă (6.1(4)) și **efectele de ordinul doi prin curbura nominală** (5.8.8) când λ > λlim = 20·A·B·C/√n (5.8.3.1, A = 0,7, B = 1,1, C = 0,7 implicit, n = NEd/(Ac fcd)): l0 = k · L după eliberări (ca la oțel, schimbabil), e2 = (1/r) l0²/10, 1/r = Kr Kφ εyd/(0,45 d), Kr = (nu − n)/(nu − 0,4) ≤ 1, nu = 1 + ω, Kφ = 1 (φef = 0, spus: fluajul nu e luat); M = max(M02, M0e + M2, M01 + 0,5 M2) cu M0e = 0,6 M02 + 0,4 M01 ≥ 0,4 M02 (5.32), pe fiecare axă. Rezistența: secțiunea cu **armătură simetrică** pe patru fețe (bare în colțuri și pe laturi, aria totală As), curba N–M din compatibilitatea deformațiilor (εcu = 3,5 ‰, diagrama dreptunghiulară, oțelul elastic–plastic) pe fiecare axă, apoi **încovoierea oblică** (5.39) (MEdz/MRdz)^a + (MEdy/MRdy)^a ≤ 1, a = 1 … 2 după NEd/NRd (0,1 → 1,0; 0,7 → 1,5; 1,0 → 2,0). **Armătura necesară**: cea mai mică As (în pași de 0,1 % din Ac) pentru care verificarea trece, cu As,min = max(0,10 NEd/fyd, 0,002 Ac) (9.5.2(2)) și As,max = 0,04 Ac. Peste 4 %: secțiunea nu ajunge.

**Plăcile** (plăcile de beton calculate ca plăci, S2): în fiecare nod al suprafeței (valorile recuperate, §10.6) momentele **Wood–Armer** pe fiecare față și direcție (Wood 1968): jos mx* = mx + |mxy|, my* = my + |mxy| (cu semnul inginerului, fața de jos întinsă pozitivă); dacă mx* < 0: mx* = 0, my* = my + |mxy²/mx|; dacă my* < 0: my* = 0, mx* = mx + |mxy²/my|; sus la fel cu semnele schimbate; apoi armătura pe metru As = m*/(fyd z) ca la grindă (b = 1 m, d = h − acoperirea − ½ Ø pe direcția x, încă un Ø pe y), cel puțin As,min. Rezultatul: valorile pe noduri (cm²/m) pe fiecare din cele patru straturi (`asxBottom`, `asyBottom`, `asxTop`, `asyTop`), maximele pe suprafață, și harta ca la izolinii (`analysis.contours` cu `quantity: asxBottom…`). Forfecarea plăcilor (vx, vy) față de VRd,c fără armătură transversală, ca verificare.

**Pereții de beton** (plăci): simplificat, în fiecare nod: armătura verticală pe fiecare față din ny și my (membrană + încovoiere: As = max(0, ny/2 + |my|/z)/fyd pe metru, ny întinderea pozitivă; compresiunea fără armătură dacă σ = −ny/t ≤ fcd, altfel spus), orizontală la fel din nx și mx, plus forfecarea în plan nxy față de VRd,c ca la plăci; cel puțin minimul 9.6.2 (0,002 Ac vertical, 25 % din vertical orizontal). Spus ca simplificare.

### 12.4 Lemn (SR EN 1995-1-1)

Valorile clasei (EN 338:2016 Tabelul 1, EN 14080:2013 Tabelul 5, `StructuralMaterials`): fm,k, ft,0,k, fc,0,k, fv,k, E0,mean, E0,05, ρk. γM = 1,3 lemn masiv, 1,25 lemn lamelat încleiat (Tabelul 2.3, valorile recomandate; anexa română necitită, §12.9). **Clasa de serviciu** 1 implicit (proiectul, schimbabilă pe element); **kmod** (Tabelul 3.1) după clasa de serviciu și **durata** acțiunii celei mai scurte din combinație (3.1.3(2)): permanentă (G) 0,60, lungă (E1 depozitare) 0,70, medie (încărcările utile pe planșee) 0,80, scurtă (zăpada) 0,90, instantanee (vântul, seismul) 1,10 (clasele 1–2; clasa 3: 0,50 / 0,55 / 0,65 / 0,70 / 0,90). Zăpada ca acțiune de scurtă durată e alegerea noastră (EN Tabelul 2.2 o dă și medie, și scurtă; anexa decide), spusă.
- Încovoierea (6.1.6): σm,y,d/fm,y,d + km σm,z,d/fm,z,d ≤ 1 și invers, km = 0,7 la dreptunghi; kh = min((150/h)^0,2; 1,3) la lemnul masiv sub 150 mm (3.2), (600/h)^0,1 ≤ 1,1 la încleiat (3.3).
- Forfecarea (6.1.7): τd = 1,5 V/(kcr b h) ≤ fv,d, kcr = 0,67.
- Întinderea (6.1.2) și compresiunea (6.1.4) în lungul fibrei; **compresiunea cu flambaj** (6.3.2): λrel = λ/π √(fc,0,k/E0,05), k = 0,5(1 + βc(λrel − 0,3) + λrel²), kc = 1/(k + √(k² − λrel²)), βc = 0,2 masiv, 0,1 încleiat; ec. 6.23–6.24 cu încovoierea; lungimile de flambaj ca la oțel.
- **Flambajul lateral** (6.3.3): σm,crit = 0,78 b² E0,05/(h lef) (dreptunghi, lemn de rășinoase), λrel,m = √(fm,k/σm,crit), kcrit = 1 / 1,56 − 0,75 λrel,m / 1/λrel,m²; lef = 0,9 L la încărcare uniformă, 0,8 L la forța la mijloc, 1,0 L la moment constant, 0,5 L și 0,8 L la console (Tabelul 6.1), schimbabil (`Llt`); ec. 6.35 cu compresiunea.
- Întinderea cu încovoierea (6.2.3), compresiunea cu încovoierea (6.2.4).
- **Săgeata (SLS)**: winst din combinația caracteristică (calculul, E0,mean), wfin = winst,G (1 + kdef) + winst,Q (1 + ψ2 kdef) (2.3.2.2, kdef 0,6 / 0,8 / 2,0 după clasa de serviciu), față de L/300 (instantanee) și L/250 (finală) (valorile din EN 1995-1-1 Tabelul 7.2, orientative, de schimbat de proiectant); consola L/150 și L/125.

### 12.5 Zidărie (CR 6-2013 / SR EN 1996-1-1)

**Forțele pe pereți** (masonry și beton calculați ca plăci, §11.1): pe fiecare suprafață a unui perete (elementul și segmentul liniei lui), la **talpa** lui pe fiecare nivel (rândul de noduri cel mai de jos al suprafeței; un perete pe mai multe niveluri e tăiat și la cota fiecărui nivel), din **forțele nodale ale elementelor de deasupra tăieturii** (Kₑ uₑ − fₑ, cu încărcările elementelor), exact în echilibru cu tot ce e deasupra: **N** (compresiunea pozitivă), **V** în planul peretelui, **M** în planul peretelui față de mijlocul tălpii (din forțele verticale × brațul plus momentele nodale pe normala peretelui), **m** în afara planului pe metru (media pe lungime și cea mai mare la un nod, pe lățimea lui aferentă). Pe combinații, prin suprapunere (forțele sunt liniare); combinația seismică ca margini ± AEd (din modurile combinate CQC ale aceleiași mărimi, §11.3).

**Rezistențele** (CR 6-2013; EN 1996-1-1 unde CR 6 nu s-a putut citi):
- fd = fk/γM, **γM = 2,2** (CR 6 2.4.2.3.1, unități de categoria I, mortar de rețetă sau performanță, control normal); fk ca la §11.1 (CR 6 ec. 4.1, K 0,55 / 0,45 / 0,55); E = 1000 fk la argilă, **800 fk la BCA** (CR 6 4.1.2.2.1(3), confirmat: schimbă §11.1, care avea 1000 pentru toate — corectat).
- **Compresiunea** (CR 6 6.16): NRd = Φ A fd, A = l·t (fără goluri: suprafața e piesa plină); la capete Φi = 1 − 2 ei/t, ei = Mi/Ni + ea ≥ 0,05 t (CR 6 ec. 6.18), ea = max(t/30, hef/300, 10 mm); la mijlocul înălțimii Φm din **EN 1996-1-1 Anexa G** (Φm = A1 e^(−u²/2), A1 = 1 − 2 emk/t, u = (λ − 0,063)/(0,73 − 1,17 emk/t), λ = (hef/tef) √(fk/E); CR 6 Tabelul 6.2 nu s-a putut citi, spus), emk = em ≥ 0,05 t, fără fluaj (ek = 0, spus). hef = ρ2 h, ρ2 = 0,75 sub planșee de beton armat, 1,0 altfel (EN 1996-1-1 5.5.1.2); ρ3, ρ4 (pereții legați pe laturi) nu sunt luate, de partea siguranței.
- **Forfecarea în plan** (lunecarea în rosturile orizontale): VRd = fvd t lc, fvk = fvk0 + 0,4 σd ≤ 0,065 fb (CR 6 ec. 4.4a), fvd = fvk/γM, fvk0 = 0,20 MPa (mortar M2,5–M9) / 0,30 (M10–M20) la argilă, 0,15 la BCA (CR 6 Tabelul 4.5, citit parțial), σd = N/(lc t), lc lungimea comprimată (diagrama liniară: l dacă e ≤ l/6, altfel 3(l/2 − e)).
- **Încovoierea în plan cu forță axială**: zona comprimată x = N/(0,85 fd t) (blocul dreptunghiular), MRd = N (l − x)/2; N ≤ 0 (întindere pe tot peretele) → MRd = 0, spus. Formula din CR 6 6.6.3.2 nu s-a putut citi: e echilibrul blocului dreptunghiular (EN 1996-1-1 6.1.2.1 și EN 1998-3 C.4.2.1 folosesc aceeași formă), spus, de confirmat.
- Verificarea în afara planului: Φm deja ia excentricitatea m/N; încovoierea fără forță axială (fxk) nefăcută, spusă.
- **Combinația seismică**: aceleași rezistențe, cu forțele combinației seismice (γM = 2,2; CR 6 permite 1,65 doar la ag ≤ 0,12 g: Lapin ia 1,65 când ag ≤ 0,12, spus). Factorul de suprarezistență, ductilitatea și regulile de alcătuire P100-1/2013 cap. 8 (aria minimă a pereților pe direcție, procentul de goluri, centurile): nefăcute în S4, spuse.

**Tabelul pereților** (`analysis.walls`): pe niveluri, fiecare perete (numele, lungimea, grosimea, materialul) cu N, V, M, m și rezistențele NRd (Φ), VRd, MRd, utilizarea fiecăreia și combinația care o guvernează; la fiecare nivel suma pe fiecare direcție a forțelor tăietoare și a rezistențelor (pereții pe X și pe Y după orientarea lor, ±30°).

### 12.6 Interogările și comenzile

- `analysis.checks {material?, storeyId?}` → `{ok, members: [{id, sourceId, name, material, kind, storeyId, utilisation, governing: {check, combination, name, clause}, checks: [{key, name, clause, utilisation, combination, x, values: {...}}], notes}], surfaces: [{id, sourceId, name, kind, utilisation, …, reinforcement?: {max: {asxBottom, …}}}], notes, indicative: true}`. Fără `id`, rezumatul (cea mai rea verificare a fiecăruia); `analysis.check {id}` dă detaliul unui element (toate verificările, valorile intermediare).
- `analysis.walls {}` → tabelul pereților (§12.5).
- `analysis.reinforcement {id?}` → armătura necesară: pe bare (As jos/sus, Asw/s, la stâlpi As total) și pe plăci (pe noduri), cu ce e pus din tip (`stirrups`, `bars`), pentru modulul `rebar` și interfață.
- `analysis.contours` primește și `asxBottom`, `asyBottom`, `asxTop`, `asyTop` (armătura necesară a plăcilor, cm²/m, pe înfășurătoarea SLU).
- `analysis.setDesign {ids?, ky?, kz?, lcrY?, lcrZ?, llt?, serviceClass?, system?}`: cu `ids`, valorile proprii ale elementelor (atributul `analysisDesign`); fără, ale proiectului (`analysisDesign` pe IfcProject: `serviceClass`, `cover`, `fyk`, `mortar`, `gammaM`…). Un pas de undo; `null` revine la implicit. Permisă invitaților ca `analysis.setOptions`.

### 12.7 Interfața

În dialogul de rezultate o secțiune **Checks**: tabelul membrilor (utilizarea, verificarea și combinația care guvernează), filtrul după material și nivel, culorile pe 3D (și în plan) după utilizare (0–1 verde–galben, peste 1 roșu), tabelul pereților de zidărie pe niveluri; în Proprietăți, la elementul ales, fiecare verificare cu clauza și valorile, și valorile proprii (lungimi de flambaj, lungimea nerezemată lateral, clasa de serviciu) ca comenzi. Mențiunea „orientativ” pe fiecare tabel. Toate textele în toate limbile din `ui/src/i18n`; ghidul (EN și RO) și o scenă în `scenes.mjs`.

### 12.8 Testele și sursele

Cifrele din formulele normelor, calculate de mână în comentariul fiecărui test, cu clauza în nume: clasa și rezistența unui IPE 300 S275 (Tabelul 5.2, 6.2.5), flambajul unui HEB 200 de 4 m (6.3.1.2), Mcr al unui IPE 300 de 6 m (SN003, C1 = 1,0 și 1,127), interacțiunea Anexei B; grinda de beton 30×50 cu M = 150 kN·m (K, z, As), forfecarea (VRd,c, Asw/s, cot θ), raportul L/d; stâlpul cu e0 și curbura nominală; Wood–Armer pe un câmp cunoscut (mxy = 0 → valorile însele; mx = my = mxy); lemnul C24 (kmod, kc, kcrit); peretele de zidărie în consolă (N = 0, V = P, M = P H exact din forțele nodale), Φ, VRd, MRd de mână.

Surse: textele EN 1992-1-1:2004 și EN 1995-1-1:2004+A1 (citite), EN 338:2016 Tabelul 1 (previzualizarea), NCCI SN003a (Mcr, C1) și SN048b (Anexa B), ECCS „Design of Steel Structures” (Simões da Silva, Simões, Gervásio; Tabelele 5.2, 6.2, 6.4–6.6, B.3), SCI AD 552, lista SCIA a anexelor naționale române (EN 1992, EN 1993), CR 6-2013 (textul de pe pdfcoffee: γM, ec. 4.1, 4.4a, Tabelul 4.1, 4.1.2.2.1(3), 6.16, 6.18), EN 14080:2013 prin structuralbasics.com (secundar).

### 12.9 Valorile, confirmate și de confirmat

Confirmate (citite în sursă): Tabelul 5.2; α și Tabelul 6.2; Tabelele 6.4–6.5, λ̄LT,0 = 0,4, β = 0,75, f; Mcr și C1 (SN003a); Anexa B; γc, γs, αcc recomandat, VRd,c, VRd,max, ν, cot θ, ρw,min, As,min, As,max, 7.16a/b și Tabelul 7.4N, 5.8.8, λlim, e0, 5.39; kmod, kdef, γM al lemnului, βc, σm,crit, kcrit, km, kcr, kh; EN 338 C16–C30; γM = 2,2 / 1,65 al CR 6, fk = K fb^0,7 fm^0,3 și K, E = 1000 fk / 800 fk, fvk = fvk0 + 0,4 σd ≤ 0,065 fb, NRd = Φ A fd, ei ≥ 0,05 t cu ea.

**De confirmat de utilizator:** (1) anexele naționale române ca numere (SR EN 1992-1-1/NB, SR EN 1993-1-1/NB, SR EN 1995-1-1/NB): Lapin ia valorile recomandate, pe care lista SCIA le dă ca „implicite EN” pentru beton și oțel; (2) **interacțiunea la oțel: anexa română alege metoda 1 (Anexa A)**, Lapin face metoda 2 (Anexa B, cerută în sarcina S4): o lăsăm, sau facem și metoda 1?; (3) zăpada în clasa de durată scurtă la lemn (EN o dă și medie); (4) CR 6: Tabelul 4.5 (fvk0 pe mortare) citit parțial, Tabelul 6.2 (Φm), formulele 6.6.3.2 (compresiune cu încovoiere în plan) și 6.6.4.1 (lunecare, cedare pe diagonală) necitite: Lapin ia Anexa G a EN 1996-1-1 și blocul dreptunghiular; (5) EN 14080 (GL) din sursă secundară; (6) k = 0,7 la încastrat–încastrat (practica, nu 0,5 teoretic) și lungimile de flambaj ale cadrelor deplasabile (de dat de utilizator).

### 12.10 Ce s-a hotărât la implementare (20.11.2026)

- **Codul**: `Lapin.Analysis/Design/` — `SteelDesign`, `ConcreteDesign` + `ConcreteMembers`, `TimberDesign`, `MasonryDesign`, `WallForces`, `DesignRun` (ținut după versiunea modelului, ca `AnalysisRun`), `DesignSettings`; `AnalysisRun.Design.cs` dă eforturile fiecărui membru pe fiecare combinație SLU (și marginile seismice), cu durata acțiunii celei mai scurte, și săgețile pe cazuri.
- **Utilizarea la beton**, unde rezistența depinde de armătura pe care Lapin o calculează: armătura necesară față de As,max = 0,04 Ac (încovoiere, stâlpi), VEd față de VRd,max la cot θ = 1 (forfecare); când tipul are etrieri sau bare (setările modulului `rebar`: `stirrups`, `bars`), încă o verificare: cea necesară față de cea pusă. Lapin nu pune bare singur.
- **Lungimile de flambaj**: capătul liber (niciun alt element, nicio placă, neținut) → consolă, k = 2; reazem încastrat la ambele capete 0,7, la unul 0,85, altfel 1,0; cadru contravântuit presupus (spus în fiecare verificare). O grindă care are ambele capete pe nodurile unei plăci e rezemată lateral (fără flambaj lateral, spus). Imperfecția l0/400 adăugată momentului de ordinul întâi la stâlpii de beton.
- **Forțele pe pereți** (`WallForces`): trei tăieturi pe fiecare suprafață de perete (talpa, rândul de noduri cel mai apropiat de mijloc, capul), forțele nodale Kₑuₑ − fₑ ale elementelor de pe o parte; exacte: peretele de cărămidă 3 × 6 m dă la talpă N = ρ g t L H, V = P, M = P H la 1e-6 (`DesignRunTests`). Combinația seismică: N ∓ AEd, |V| + AEd, |M| + AEd (partea nefavorabilă), AEd prin `SeismicRun.Combine` pe rigiditatea fisurată.
- **Excentricitatea la capetele pereților de zidărie**: momentul plăcii monolite din calculul elastic dă ușor e > t/2 (Φ = 0); Lapin o limitează la 0,4 t (EN 1996-1-1 Anexa C(4) după cum o citim, din memorie, de confirmat), spus în verificare.
- **Forfecarea plăcilor** se citește în afara liniilor de reazem (nodurile comune cu barele sau cu alte suprafețe): acolo valorile la un reazem punctual sunt singularitatea plasei; străpungerea (6.4) nu se face.
- **E-ul BCA**: KE = 800 (CR 6-2013 4.1.2.2.1(3), confirmat), nu 1000 ca la §11.1; schimbă rigiditatea pereților de BCA (E = 1,88 GPa în loc de 2,35 la valorile implicite). Niciun test existent n-avea BCA portant.
- **Viteza**: casa din ghid (222 de bare, 5 pereți) — verificările în 69 ms după calcul (524 ms), Debug.
- **Interfața**: secțiunea Checks din dialogul de rezultate (membrii, plăcile cu armătura pe straturi, tabelul pereților de zidărie pe niveluri cu ΣV și ΣVRd pe direcții, notele, valorile proiectului), culorile după utilizare în 3D și în plan (opțiunea „Utilisation” pe card), armătura necesară a plăcilor ca izolinii (as,x/as,y jos/sus), secțiunea „Design checks” în Proprietăți cu valorile proprii ale elementului (fiecare schimbare un `analysis.setDesign`).
- **Ghidul**: scenele `analysis` (verificările, culorile, Proprietățile) și `analysis-masonry` (casa de cărămidă 6 × 4 sub un planșeu de beton: tabelul pereților), rulate în engleză și română; scena seismică: poza secțiunii întreagă (fereastra mărită pentru ea), forma modului oprită la capătul oscilației, iar `analysis.mode` o scalează implicit la 15 % din mărimea modelului (era 8 %), ca să se vadă.

### 12.11 Răspunsurile utilizatorului (20.11.2026) și ce s-a făcut

1. **Interacțiunea la oțel: Anexa A (metoda 1) e implicită**, cum o alege anexa națională română; Anexa B (metoda 2) rămâne la alegere pe proiect (`analysis.setDesign {steelInteraction: "A" | "B"}`, un pas de undo). Făcută după Tabelele A.1 și A.2 cu corigendumul (manualul ECCS, Simões da Silva ș.a., Tabelele 3.13–3.15): μy, μz, wy, wz ≤ 1,5, npl, aLT = 1 − IT/Iy, λ0 (flambajul lateral sub moment constant pe lungimea nerezemată), Ncr,T = (G IT + π² E Iw/l²)/i0² (ECCS ec. 3.59), bara nesusceptibilă la deformații de torsiune când IT ≥ Iy sau λ0 ≤ 0,2 √C1 ⁴√((1 − N/Ncr,z)(1 − N/Ncr,T)) (atunci Cmy = Cmy,0, CmLT = 1, χLT = 1), altfel Cmy cu εy și aLT și CmLT = Cmy² aLT/√((1 − N/Ncr,z)(1 − N/Ncr,T)) ≥ 1; Cyy, Cyz, Czy, Czz cu bLT, cLT, dLT, eLT; kij la clasele 1–2 și 3. Cmi,0 după Tabelul A.2: moment liniar 0,79 + 0,21ψ + 0,36(ψ − 0,33) N/Ncr, deschidere simplă cu încărcare uniformă 1 + 0,03 N/Ncr, cu forță la mijloc 1 − 0,18 N/Ncr; altfel 1,0 (formula generală cu săgeata δx nefăcută, spus). Rădăcinile (√ la εy aLT și la Cyz, Czy, a patra la condiția 3.145) sunt citite dintr-un text extras din PDF în care semnele se pierduseră: de confirmat pe textul normei. Test: HEB 200, 4 m, N = 500 kN, My 30 → 0 kN·m: ec. 6.61 = 0,46948, ec. 6.62 = 0,51169 (Anexa B dădea 0,43809 și 0,50617).
2. **Zăpada la lemn: de scurtă durată implicit**, de durată medie pe proiect (`analysis.setDesign {snowDuration: "short" | "medium"}`, de pildă la munte); spusă în nota fiecărei verificări a lemnului.
3. **k = 0,7 la barele încastrate la ambele capete rămâne**; lungimile de flambaj ale cadrelor cu noduri deplasabile le dă utilizatorul. Lapin spune în note când modelul pare necontravântuit pe o direcție: are stâlpi, dar niciun perete (de orice material) și nicio bară înclinată pe direcția X sau Y („No wall or bracing along X …”).

În interfață, sub „Design values of the project”: „Steel interaction” (Anexa A / B) și „Snow on timber” (de scurtă / de durată medie), traduse în toate limbile; ghidul (ambele limbi) le spune.

## 13. Combinațiile configurabile (specificația, 20.11.2026)

Aprobat de utilizator (20.11.2026): coeficienții parțiali pe proiect, cu un set ales pe proiect (**CR 0-2012 implicit la proiectele noi**, EN 1990 Anexa A1 recomandat ca alternativă) și fiecare valoare schimbabilă peste set; combinațiile utilizatorului în tabul Combinations; generatele dezactivabile; 6.10a/6.10b ca opțiune. Ca tot ce ține de normă: valorile sunt ale utilizatorului, cele livrate orientative. Codul vechi rămâne implicitul, cu aceeași ieșire („extend, never break”).

### 13.1 Setul de coeficienți al proiectului

Pe IfcProject, `analysisCombinations`: `{set, gammaGSup, gammaGInf, gammaQ, psi: {<categorie>: {0, 1, 2}}, expression, xi, disabled: [chei]}`, fiecare cheie lipsă = valoarea setului. Schimbat de `analysis.setCombinationOptions` (un pas de undo; `null` aduce o valoare înapoi la set).

| | EN 1990 A1 recomandat (`en`) | CR 0-2012 (`cr0`) | sursa CR 0 |
|---|---|---|---|
| γG,sup / γG,inf / γQ (STR/GEO, gruparea fundamentală) | 1,35 / 1,0 / 1,5 | 1,35 / 1,0 / 1,5 | Tabelul 7.2 |
| A, B (rezidențial, birouri) ψ0 ψ1 ψ2 | 0,7 0,5 0,3 | 0,7 0,5 0,3 | Tabelul 7.1 |
| C, D (adunare, comerț) | 0,7 0,7 0,6 | 0,7 0,7 0,6 | Tabelul 7.1 |
| E1 (depozitare) | 1,0 0,9 0,8 | 1,0 0,9 0,8 | Tabelul 7.1 |
| F (vehicule ≤ 30 kN) | 0,7 0,7 0,6 | 0,7 0,7 0,6 | Tabelul 7.1 |
| G (vehicule 30–160 kN) | 0,7 0,5 0,3 | 0,7 0,5 0,3 | Tabelul 7.1 |
| H (acoperișuri) | 0 0 0 | **0,7** 0 0 | Tabelul 7.1 („Acoperișuri”) |
| zăpadă | 0,5 0,2 0 | **0,7 0,5 0,4** | Tabelul 7.1; CR 1-1-3/2012 Tabelul 4.3 (publicat) |
| vânt | 0,6 0,2 0 | **0,7** 0,2 0 | Tabelul 7.1 |
| 6.10a/6.10b | permise (A1.2.2) | nu există (doar 6.9) | §6.4.3.2 |

Sursele: textul CR 0-2012 trimis la notificare (ugir.ro, aprilie 2012: §6.4.3.2 ec. 6.9–6.11, §6.5.3 ec. 6.13–6.15, Tabelele 7.1–7.5) și CR 1-1-3/2012 (Ordinul 1655/2012, MO 704 bis, Tabelul 4.3, ψ ai zăpezii „din CR 0”). Valorile de mai sus sunt citite din varianta de notificare; **de confirmat pe textul publicat** (Ordinul 1530/2012, MO 647 bis, cu completarea din Ordinul 2411/2013) rândurile vântului (0,7) și acoperișurilor (0,7), pe care publicul nu le-am găsit în altă sursă. Numerotarea diferă de EN: în CR 0 gruparea fundamentală e (6.9), cea accidentală (6.10), cea seismică (6.11).

**Proiectele existente nu se schimbă.** Ele nu au `analysisCombinations`; un proiect fără `set` ia `en`, adică exact combinațiile de azi (CR 0 diferă numeric de EN la zăpadă, vânt și H, deci nu se putea „la fel”). Un proiect nou (ecranul de start, `Session.New`) primește `{set: "cr0"}` scris explicit, printr-un serviciu nou al platformei, `INewProject` (modulul `analysis` îl dă; platforma nu-l cunoaște). Un proiect făcut dintr-un șablon ia ce are șablonul. Nu trebuie `IProjectUpgrade`.

**ψ-ul unui caz:** al lui (`psi` pe caz) > al proiectului pe categorie (`psi.<cat>`) > al setului > al categoriei A din set (fără categorie, spus). La gruparea seismică, zăpada: al cazului > al proiectului dat explicit > `snowPsi2` din parametrii seismici (ca azi; P100 0,4).

### 13.2 6.10a / 6.10b

`expression: "6.10"` (implicit, ca azi) sau `"6.10ab"`. Cu 6.10ab, în locul fiecărei combinații 6.10 cu principal:
- **6.10a** (o dată pentru fiecare alegere a alternativelor): Σ γG,sup Gk + Σ γQ ψ0,i Qk,i, cheia `uls/6.10a` (+ alternativele alese, ca la §9.9);
- **6.10b** (pentru fiecare principal): Σ ξ γG,sup Gk + γQ Qk,1 + Σ γQ ψ0,i Qk,i, ξ = 0,85 recomandat (`xi`, 0,5–1), cheia `uls/<principal>` (aceeași ca la 6.10, deci ce e dezactivat rămâne).
`uls/permanent` (γG,sup G) și ridicarea (γG,inf G + γQ W) rămân. Cu setul `cr0`, 6.10ab rămâne la alegerea utilizatorului; nota spune că CR 0 nu le are.

### 13.3 Combinațiile utilizatorului

Entități `IfcStructuralLoadGroup` (Kind `resource`, ca un caz): nume, `kind` (`uls`, `sls-characteristic`, `sls-frequent`, `sls-quasi`, `seismic`, `accidental`), `factors: [{caseId, factor}]` (factorii nenuli, cazuri diferite). Cheia: `user/<id>`. Comenzile: `analysis.addCombination {name, kind, factors}`, `analysis.updateCombination {id, name?, kind?, factors?}`, `analysis.removeCombination {id}`; fiecare un pas de undo.

| kind | unde intră |
|---|---|
| uls | înfășurătoarea SLU, verificările S4 (durata la lemn: acțiunea cea mai scurtă a cazurilor ei) |
| accidental | la fel ca uls (situație de proiectare la SLU; γM ale situației persistente, conservativ, spus) |
| sls-* | înfășurătoarea SLS; `sls-characteristic` și la săgețile din S4 |
| seismic | factorii ei (partea gravitațională) ± AEd din analiza seismică: înfășurătoarea `seismic` și cea SLU, verificările (instantanee); fără parametri seismici nu intră, spus |

Un caz șters pe care îl numește o combinație a utilizatorului: combinația rămâne în model, dar nu intră în rezultate; lista o arată cu `missing: [caseId]` și notele spun de ce. Un caz gol: factorul lui nu adaugă nimic (combinația intră).

### 13.4 Dezactivarea celor generate

`analysis.setCombinationEnabled {key, enabled}` (un pas de undo) scrie/șterge cheia în `disabled`. O combinație dezactivată rămâne în listă (`enabled: false`), dar nu intră în rezultate, înfășurători, verificări și export. O cheie dezactivată care nu se mai generează (un caz șters) rămâne scrisă și nu face nimic. Combinațiile utilizatorului se dezactivează la fel (cheia `user/<id>`).

### 13.5 Interogarea și exportul

`analysis.combinations` → `{combinations: [{key, kind, name, formula, factors, source: generated|user, enabled, id?, missing?}], options: {set, gammaGSup, gammaGInf, gammaQ, psi, expression, xi, sets: {en, cr0}}, notes}`. `analysis.run` dă doar cele active. Exportul IFC SAV scrie cele active (cele ale utilizatorului cu numele lor; ca la toate, `Description` = cheia, `Purpose` = kind); combinația seismică nu se scrie (AEd nu e un caz de încărcare), spus în note. Exportul JG nu scrie încărcări sau combinații (nimic de schimbat).

### 13.6 Testele

Un proiect fără setări: combinațiile neschimbate (aur pe cadrul din ghid și pe hala de oțel cu zăpadă și vânt, fișiere JSON scrise de codul dinainte); setul `cr0`; γ schimbați; o combinație a utilizatorului în rezultate, înfășurătoare și verificări; una generată dezactivată lipsește din înfășurători; 6.10a/b pe un caz mic, cu numere de mână; undo; SAV scrie combinațiile utilizatorului; un caz șters spus.

## 14. S5: ordinul doi, reazemele elastice și legăturile într-un singur sens (specificația, 21.11.2026)

**Hotărât de utilizator (20.11.2026):** S5 e în modulul deschis `analysis`, MIT, gratuit, cu cod propriu (decizia 742). Tot ce urmează e scris pentru Lapin după literatura citată la §14.9; `hse95/nodal` (MIT) a fost doar inspirație pentru cazurile de test, niciun cod de acolo. Ca la S1–S4: rezultatele sunt **orientative**, calculul semnat rămâne al inginerului.

**Regula de compatibilitate („extend, never break”):** fără opțiunile S5 (implicit toate oprite) un proiect dă **exact** rezultatele de dinainte; testul `SecondOrderGoldenTests` compară ghidul (cadrul cu planșeu, zăpadă, vânt, seism) și hala de oțel cu JSON scris de codul dinaintea S5. Nu trebuie `IProjectUpgrade`: totul e în atribute noi, absente din proiectele vechi.

S5 se face în trei sub-etape, în ordinea în care se sprijină una pe alta, și lasă restul specificat (§14.7):

| Sub-etapă | Ce | De ce în ordinea asta |
|---|---|---|
| **S5a** | Reazemele elastice: resorturi pe grade de libertate la reazemele fundațiilor și ale legăturilor; Winkler sub tălpile izolate și continue și sub plăcile pe sol | Liniar: suprapunerea rămâne; e baza pentru reazemele doar la compresiune |
| **S5b** | Ordinul doi P-Δ (rigiditatea geometrică a barelor, iterată pe combinație) și flambajul liniar global: αcr pe fiecare combinație SLU, clasificarea EN 1993-1-1 5.2.1, forma de flambaj | Ce cer verificările S4 la oțel (5.2.1(3)); folosește solverul și iterația în subspațiu deja făcute |
| **S5c** | Barele doar la întindere / doar la compresiune (contravântuiri, tiranți, șpraițuri) și reazemele doar la compresiune (desprinderea de pe teren), prin aceeași iterație pe combinație ca P-Δ | Neliniar prin stări; aceeași buclă ca P-Δ, deci făcute împreună |

### 14.1 Neliniarul pe combinații (cadrul comun al S5b și S5c)

Un calcul neliniar nu se mai poate suprapune: **fiecare combinație statică** (SLU, SLS, ale utilizatorului, cele active; §8.5, §13) se rezolvă singură, cu vectorul ei de încărcări Σ γ·ψ·Fk (aceleași forțe nodale, încărcări pe bare și pe plăci ca la cazuri, înmulțite cu factorii). **Cazurile rămân liniare** (de ordinul întâi, toate legăturile active): suprapunerea lor nu mai are sens într-un calcul neliniar, iar un caz singur e cel caracteristic; spus în note. Combinația seismică (§11.3) rămâne liniară: AEd e din analiza modală liniară, iar partea gravitațională e suprapunerea cazurilor (efectul P-Δ seismic e coeficientul θ, §14.7).

Bucla neliniară e una pentru toate (Newton–Raphson modificat pe stări; Bathe, „Finite Element Procedures”, §8.4; McGuire, Gallagher și Ziemian, „Matrix Structural Analysis”, ed. 2, §9–10):
1. Pasul 1: problema liniară a combinației (toate barele active, toate reazemele așezate, N = 0 în rigiditatea geometrică).
2. Din rezultat: N pe fiecare bucată de bară (întinderea pozitivă, media capetelor); starea barelor într-un singur sens (o bară doar la întindere comprimată se scoate, una scoasă care s-ar lungi se pune înapoi; invers la compresiune); starea reazemelor doar la compresiune (unul care trage se ridică; unul ridicat care ar coborî în teren se așază).
3. Problema refăcută cu noile stări și, cu P-Δ, cu rigiditatea geometrică din N; asamblată, factorizată (`FrameSystem`), rezolvată.
4. Se oprește când nicio stare nu se mai schimbă și (cu P-Δ) |ΔN| ≤ 10⁻⁴ max|N| + 10⁻⁶ kN; cel mult **30 de iterații**; o stare care se repetă (ciclu) oprește bucla cu nota „nu s-a așezat”.
5. O factorizare care pierde un pivot: fără rigiditatea geometrică e un **mecanism** (de pildă toate contravântuirile unei direcții scoase), spus cu nodul ca la S1; cu ea e **instabilitate** (încărcarea combinației trece de cea critică, αcr < 1), spusă. Combinația aceea **nu intră** în înfășurători și verificări, cu o notă roșie (de verificat).

Rezultatul fiecărei combinații e ținut ca un rezultat în plus lângă cazuri (`AnalysisRun`, aceleași bare, plăci și noduri), deci înfășurătorile, diagramele, izoliniile, forțele pe pereți și verificările S4 îl citesc fără să știe că e neliniar. Totul e **derivat**, ținut în memorie după versiunea modelului, ca `analysis.run`. Bucla rulează doar când e nevoie de ea: `secondOrder` pornit, o bară într-un singur sens sau un reazem doar la compresiune.

### 14.2 S5a: reazemele elastice

**Resorturile unui reazem** (atributul `analysisSupport` pe fundație — `IfcFooting` izolată sau continuă — sau pe bara unei legături cu reazem, ca baza unui stâlp de oțel): `{springs?: [kx, ky, kz, krx, kry, krz], winkler?: ks, compressionOnly?: bool}`, axe globale, kN/m și kN·m/rad, ks în kN/m³.
- `springs`: fiecare număr dat înlocuiește ținerea gradului acela cu un resort de rigiditatea dată (0 = liber); `null` pe o poziție îl lasă cum îl ține reazemul (încastrat sau articulat). Resortul e pus **în fiecare nod** pe care fundația îl ține (o talpă continuă sub un perete ține multe noduri: atunci resortul e pe nod, spus; pentru tălpi continue e mai potrivit Winkler).
- `winkler` (modelul lui Winkler, 1867; Hetényi, „Beams on Elastic Foundation”, 1946): **talpa izolată** (dreptunghiul ei în plan, B × L): kz = ks·B·L, krx = ks·B·L³/12 și kry = ks·L·B³/12 (momentele de inerție ale tălpii față de axele ei prin centru, axele globale), kx, ky și krz ținute; o talpă care ține mai multe noduri le împarte în părți egale. **Talpa continuă** (linia ei, lățimea B): fiecare nod ține lungimea aferentă l (jumătate până la vecini, capetele până la capetele tălpii): kz = ks·B·l, rotirea în jurul axei tălpii k = ks·l·B³/12 (o matrice 3 × 3 de rotire în axe globale când talpa e oblică), restul ținute. Un resort dat în `springs` trece înaintea celui din ks pe același grad.
- **Placa pe sol** (`analysisSupport: {winkler: ks, compressionOnly?}` pe o placă analizată ca placă, §10): fiecare nod al plăcii primește kz = ks·Aᵢ, Aᵢ cota consistentă a ariei elementelor (A/4 la un patrulater, A/3 la un triunghi: ∫Nᵢ dA pe un paralelogram); **ux, uy ținute** în nodurile ei (frecarea și împingerea pământului; fără ele placa ar fi un mecanism în planul ei), spus. Fără plăci (`shells: none` / `walls`), Winkler pe placă nu face nimic, spus.
- **Reacțiile** unui nod pe resorturi sunt forțele resorturilor (R = −K_s·u), adunate cu cele ale gradelor ținute; suma reacțiilor = suma încărcărilor (echilibrul, verificat la fiecare caz, ca la S1).
- **Modal și seismic:** problema modală ia aceleași resorturi (nefisurate: e terenul). Interacțiunea teren–structură mai departe (amortizarea radiației, resorturile dinamice) nu se face.
- Lapin **nu calculează ks**: e al geotehnicianului (studiul geotehnic, sau NP 112-2014 / EN 1997-1); interfața dă doar valori uzuale orientative ca sugestie (Bowles, „Foundation Analysis and Design”, ed. 5, Tabelul 9-1, din memorie, de confirmat: nisip afânat 4,8–16 MN/m³, mediu 9,6–80, îndesat 64–128; argilă qu ≤ 200 kPa 12–24, 200–800 kPa 24–48, peste 800 kPa > 48), marcate orientative.

Comanda: `analysis.setSupport {ids, springs?, winkler?, compressionOnly?}` (un pas de undo; `null` pe o cheie o scoate, absentă o lasă; refuzate: un element care nu e fundație, legătură cu reazem sau placă, numere negative sau nefinite, `springs` cu altă lungime decât 6, `winkler` pe o bară).

### 14.3 S5b: ordinul doi P-Δ și flambajul global

**Rigiditatea geometrică** a unei bare cu efortul axial N (întinderea pozitivă), matricea consistentă a barei Euler–Bernoulli (Przemieniecki, „Theory of Matrix Structural Analysis”, 1968, §15; McGuire ș.a., ec. 9.18), în fiecare plan de încovoiere:

Kg = N/L · [6/5, L/10, −6/5, L/10; L/10, 2L²/15, −L/10, −L²/30; −6/5, −L/10, 6/5, −L/10; L/10, −L²/30, −L/10, 2L²/15] (v, θz, în planul x–y; cu semnele cuplajelor schimbate în planul x–z, ca la rigiditatea elastică)

fără termenul de torsiune (N·Ip/A): modelul nu are rigiditatea de deplanare, iar flambajul prin torsiune și încovoiere-torsiune al unei bare e al verificărilor (EN 1993-1-1 6.3.1.4, 6.3.2), spus. La o bară cu eliberări, Kg se condensează cu aceeași transformare ca rigiditatea elastică (rotirile eliberate urmează din cele ținute): Kg′ = Tcᵀ Kg Tc, deci o bară articolată la capete (o contravântuire) dă exact rigiditatea „de coardă” N/L pe deplasările transversale. Barele Timoshenko își păstrează rigiditatea elastică cu forfecarea; Kg e cea Euler–Bernoulli (diferența e neglijabilă la barele zvelte, spusă). Plăcile nu au rigiditate geometrică în S5 (§14.7), spus când există plăci comprimate.

**P-Δ** (`analysisOptions.secondOrder: true`, implicit `false`): bucla de la §14.1 cu K + Kg(N) pe fiecare combinație statică. Momentele de la capetele barelor sunt de ordinul doi (deplasarea relativă a capetelor, Δ); de-a lungul barei momentul rămâne forma de ordinul întâi între ele: efectul P-δ al barei e în verificarea ei la flambaj (EN 1993-1-1 6.3, curbura nominală la beton, 5.8.8), spus. Ce raportează fiecare combinație: iterațiile, dacă a convers, **amplificarea** = deplasarea orizontală cea mai mare de ordinul doi / cea de ordinul întâi (pasul 1).

**Flambajul liniar** (`analysis.buckling`): (K + αcr Kg(N)) φ = 0 pentru fiecare combinație SLU statică, N din rezultatul ei (de ordinul întâi, sau cel neliniar când bucla rulează; atunci K e cea a stărilor finale, fără Kg). Rezolvat ca problema generalizată G φ = μ K φ, G = −Kg, K pozitiv definită: **iterația în subspațiu** (Bathe și Wilson 1972, ca la §11.2) pe factorizarea lui K, proiecțiile Gr = X̄ᵀ G X̄ și Kr = X̄ᵀ K X̄ = X̄ᵀ G X, problema mică prin Cholesky pe Kr și Jacobi; cele mai mari μ > 0 dau αcr = 1/μ, crescător. Primele 3 valori (`modes`, 1–12) și formele lor. O combinație fără nicio bară comprimată nu are αcr (spus: „nicio compresiune”).

**Clasificarea** (EN 1993-1-1 5.2.1(3) și 5.2.2(5B), pentru analiza elastică): **αcr ≥ 10**: ordinul întâi e suficient; **3 ≤ αcr < 10**: efectele de ordinul doi trebuie luate: prin P-Δ, sau amplificând efectele orizontale (de deplasare laterală) cu **1/(1 − 1/αcr)** (5.2.2(5B), raportat); **1 ≤ αcr < 3**: e cerut calculul de ordinul doi (5.2.2(5B) nu se aplică); **αcr < 1**: structura e instabilă sub combinație. La beton EN 1992-1-1 5.8.2(6) dă alt criteriu (efectele globale de ordinul doi neglijabile sub 10 %, adică αcr ≥ 10 cu rigiditatea fisurată); Lapin raportează aceeași clasificare cu rigiditatea nefisurată, spus.

**Legătura cu S4:** verificările oțelului (§12.2) spun αcr cel mai mic al combinațiilor SLU când e sub 10 și P-Δ e oprit („EN 1993-1-1 5.2.1(3): αcr = … < 10, efectele de ordinul doi trebuie luate: pornește P-Δ”), iar când P-Δ e pornit, că eforturile sunt de ordinul doi și că flambajul barelor (6.3.1) rămâne cu lungimile lor de flambaj. Lungimile de flambaj din αcr (Lcr = π √(EI / (αcr N))) sunt pentru mai târziu (§14.7).

### 14.4 S5c: barele și reazemele într-un singur sens

- **Bara doar la întindere** (`analysisOnly: "tension"`: contravântuiri în X, tiranți, cabluri drepte) sau **doar la compresiune** (`"compression"`: șpraițuri, rezemări simple de contact). Comanda `analysis.setOnly {ids, only: "tension" | "compression" | null}`, un pas de undo; de obicei împreună cu `analysis.setAxial` (bara articulată, lucrând axial). O bară scoasă nu are rigiditate (toate bucățile ei); încărcările pe ea (greutatea ei) se duc tot la capete, ca forțe de capăt; N-ul ei e zero în rezultate. Criteriul: N < −10⁻⁶ max|N| scoate o bară doar la întindere; lungirea capetelor ei (u_j − u_i)·x > 10⁻⁹ m o pune înapoi (invers la compresiune).
- **Reazemul doar la compresiune** (`analysisSupport.compressionOnly: true`, ridicarea de pe teren): reazemul ținut pe uz care ar trage (Rz < 0) se **ridică**: nodul lui e liber pe toate gradele (o talpă desprinsă nu mai ține nimic), resorturile lui scoase; unul ridicat care ar coborî (uz < 0) se așază. Pe resorturi (Winkler), fiecare nod e un resort doar la compresiune: lucrează când uz ≤ 0, scos când uz > 0 (placa pe sol care se desprinde la colțuri). Rotirile unei tălpi pe Winkler nu se desprind parțial (talpa care se înclină pe o muchie): resorturile de rotire rămân cât lucrează cel vertical, spus.
- **Modal și seismic:** problema modală ia toate barele active și toate reazemele așezate (liniar), spus în note când există bare într-un singur sens: contravântuirile în X dau atunci dublul rigidității unei diagonale întinse.

### 14.5 Interogările și comenzile

- `analysis.setOptions {secondOrder?: bool}` (pe lângă `shells`, `meshSize`; `null` înapoi la implicit; un pas de undo; permisă invitaților, ca înainte).
- `analysis.setSupport {ids, springs?, winkler?, compressionOnly?}`, `analysis.setOnly {ids, only}`: un pas de undo fiecare; permise invitaților, ca `analysis.setReleases`.
- `analysis.run` primește `nonlinear: {secondOrder, combinations: [{key, name, ok, iterations, converged, amplification, slack: [{id, sourceId, name}], lifted: [{node, at, name}], message?}]}` când bucla rulează, și în `supports` resorturile puse (`springs`: câte noduri, `winkler`: câte, `compressionOnly`).
- `analysis.buckling {combination?, modes?}` → `{ok, combinations: [{key, name, alpha: [..], class: "first-order" | "amplify" | "second-order" | "unstable" | "none", amplifier, compressed}], lowest: {key, alpha}, notes}`; fără `combination`, toate cele SLU statice.
- `analysis.bucklingMode {combination, mode?, scale?}` → forma de flambaj pentru 3D, ca `analysis.mode` (polilinii pe bare, nodurile plăcilor deplasate), αcr al ei.
- `analysis.supportOptions {id}` → ce are reazemul (fundația, legătura sau placa): resorturile date, ks, compressionOnly, nodurile ținute și rigiditățile calculate pe fiecare (kz = ks·A …), valorile ks uzuale orientative.

### 14.6 Interfața

În dialogul de rezultate: în opțiunile analizei, **Second order (P-Δ)**; o secțiune **Stability**: tabelul combinațiilor SLU cu αcr (primele trei), clasa colorată (verde ≥ 10, galben 3–10, roșu < 3), amplificatorul 1/(1 − 1/αcr), amplificarea P-Δ și iterațiile când e pornit, barele scoase (care s-au destins) și reazemele ridicate pe combinație, notele; **Show buckling mode** în 3D (forma oscilând, ca modul propriu, §11.10). În Proprietăți: la o fundație (și la o placă) secțiunea **Analysis support** (resorturile pe cele șase grade, ks cu valorile uzuale orientative, „compression only”, nodurile și rigiditățile calculate); la o bară **Acts in**: both / tension only / compression only. Toate textele în toate limbile din `ui/src/i18n`; ghidul (EN și RO) și scena lui.

### 14.7 Ce rămâne pentru mai târziu (specificat, nefăcut în S5)

1. **Imperfecțiunile globale** (EN 1993-1-1 5.3.2: φ = φ0 αh αm, φ0 = 1/200; forțele echivalente φ·NEd la fiecare nivel, pe ±X și ±Y, adică de patru ori combinațiile SLU) — fără ele calculul de ordinul doi nu e complet pentru proiectarea la oțel (5.2.2(3)); acum spus în note când P-Δ e pornit.
2. **Coeficientul de sensibilitate θ** la deplasarea relativă de nivel (P100-1/2013 §4.6.2.2, EN 1998-1 §4.4.2.2(2)): θ = Ptot dr / (Vtot h), ≤ 0,1 neglijat, 0,1–0,2 amplificare 1/(1 − θ), > 0,3 refuzat; din tabelul nivelurilor seismic (§11.3).
3. **Lungimile de flambaj din αcr** pentru verificările de oțel la cadre cu noduri deplasabile (Lcr = π √(EI/(αcr NEd)), cu limitele lor), ca alternativă la k-ul dat de utilizator (§12.11, răspunsul 3).
4. **Rigiditatea geometrică a plăcilor** (pereți și planșee comprimate în planul lor), pentru P-Δ și flambaj la clădirile cu pereți-placă.
5. **Etapele de execuție** (construction stages): o listă de etape pe proiect (elementele și încărcările fiecăreia, din fazele modulului `phases`), rezolvate în ordine cu rigiditatea acumulată, efectele adunate; cere o comandă de etape și o legătură cu fazele.
6. **Neliniarul material** (articulații plastice concentrate la capetele barelor de oțel, M–N), **deplasările mari** (formularea corotațională, cablurile cu săgeată), analiza modală pe starea neliniară, P-Δ în combinația seismică.
7. Resorturi pe un nod oarecare (fără fundație: o grindă rezemată pe ceva nemodelat) — Lapin nu are noduri ca elemente; resorturile pe capetele barelor (rigiditățile îmbinărilor semi-rigide, EN 1993-1-8) ar fi locul lor.

### 14.8 Testele (cifrele din teorie)

`SecondOrderTests`, `BucklingTests`, `SpringSupportTests`, `OneWayTests`, `SecondOrderGoldenTests`:
- **Euler:** stâlpul articulat–articulat (8 elemente Euler–Bernoulli) αcr·P = π²EI/L² la 10⁻⁴; consola π²EI/4L²; încastrat–încastrat 4π²EI/L²; încastrat–articulat 20,19 EI/L² (Timoshenko și Gere, „Theory of Elastic Stability”, 1961, §2.1–2.5).
- **Cadrul portal** (stâlpi h = 4 m, grinda L = 6 m, aceeași secțiune, P pe fiecare stâlp), forma deplasabilă: cu baza articulată λ tg λ = 6 Ib h / (Ic L) (= 4: λ = 1,26459, Pcr = 0,6481 π²EI/4h²), cu baza încastrată λ ctg λ = −6 Ib h / (Ic L) (λ = 2,57043, Pcr = 0,66944 π²EI/h²), λ = h √(P/EI); grinda foarte rigidă: π²EI/4h² și π²EI/h²; la 10⁻³.
- **P-Δ pe consolă** (H în vârf, P axial = 0,5 Pcr): săgeata exactă a barei-coloană δ = H(tg kL − kL)/(P k), k = √(P/EI), adică amplificarea 3(tg kL − kL)/(kL)³ = 1,98629 față de ordinul întâi (Timoshenko și Gere §1.11), la 5·10⁻³; aproximația 1/(1 − P/Pcr) = 2 la 1 %. Momentul la bază = H L + P δ exact din echilibru. Iterațiile ≤ 4.
- **Resorturile:** stâlpul pe un resort vertical (δ = P/k); consola pe talpa Winkler sub o forță orizontală: δ = H h³/3EI + H h²/kθ, kθ = ks B L³/12; grinda lungă pe resorturi Winkler cu o forță la mijloc (Hetényi: w0 = Pβ/2k, M0 = P/4β, β = (k/4EI)^¼) la 1 %; placa pe sol sub o încărcare uniformă se așază uniform cu w = q/ks (la 10⁻⁶), reacțiile = încărcarea.
- **Barele într-un singur sens:** cadrul articulat cu două diagonale în X sub H: ambele active, fiecare H/(2 cos θ); doar la întindere, cea comprimată se destinde și cea întinsă ia H/cos θ; cu H întors, invers; cazurile rămân liniare (ambele active).
- **Reazemul doar la compresiune:** grinda pe trei reazeme cu încărcarea pe deschiderea a doua: reazemul de capăt al primei deschideri ar trage; ridicat, rezultatele sunt cele ale grinzii cu consolă (statică).
- **Comenzile:** `setOptions {secondOrder}`, `setSupport`, `setOnly` un pas de undo fiecare; valorile greșite refuzate; analiza recalculată după fiecare.
- **Aurul:** fără opțiuni, ghidul și hala dau exact JSON-ul dinaintea S5 (înfășurătorile SLU și SLS, un caz, modurile, forța tăietoare de bază, verificările).

### 14.9 Surse

Timoshenko și Gere, „Theory of Elastic Stability”, ed. 2, 1961 (cap. 1–2: bara-coloană, Euler, cadrele); Przemieniecki, „Theory of Matrix Structural Analysis”, 1968 (matricea geometrică consistentă); McGuire, Gallagher și Ziemian, „Matrix Structural Analysis”, ed. 2, 2000 (cap. 9–10, ordinul doi și flambajul); Bathe, „Finite Element Procedures”, 2014 (§8.4, §11.6); Hetényi, „Beams on Elastic Foundation”, 1946; Bowles, „Foundation Analysis and Design”, ed. 5, 1996 (Tabelul 9-1, valorile ks); SR EN 1993-1-1 §5.2 (citit: 5.2.1(3) αcr ≥ 10, 5.2.2(5B) amplificatorul pentru αcr ≥ 3); SR EN 1992-1-1 §5.8.2(6); EN 1998-1 §4.4.2.2, P100-1/2013 §4.6.2.2 (pentru θ, §14.7).

### 14.10 De hotărât de utilizator

1. **Cazurile rămân liniare** când bucla neliniară rulează (doar combinațiile sunt neliniare), cum fac programele comerciale. *Recomandare: da.*
2. **Winkler pe placa pe sol ține ux, uy** în nodurile ei (frecarea), altfel placa singură ar fi un mecanism în planul ei. *Recomandare: da, spus în note; „liber orizontal” ca opțiune mai târziu dacă e cerută.*
3. **Resorturile Winkler sunt implicit bilaterale** (trag și împing, liniar, rapid); `compressionOnly` pornește desprinderea. *Recomandare: bilaterale implicit; nota spune când un resort trage sub o combinație SLU, ca utilizatorul să pornească desprinderea.* (Nota e pentru mai târziu dacă e cerută.)
4. **O combinație instabilă sau cu mecanism iese din înfășurători și verificări**, cu o notă roșie. *Recomandare: da (alternativa, ordinul întâi în locul ei, ar ascunde problema).*
5. **Imperfecțiunile globale (EN 1993-1-1 5.3.2)** sunt următorul pas firesc după P-Δ. *Recomandare: le facem imediat după S5, ca S5d, cu ±X/±Y.*
6. **Clasificarea αcr la beton** după criteriul oțelului (10 / 3) cu rigiditatea nefisurată, spus. *Recomandare: da deocamdată; criteriul EN 1992-1-1 5.8.3.3 (forța verticală totală față de rigiditatea la încovoiere a contravântuirilor) mai târziu.*
7. **Valorile ks uzuale** (Bowles) arătate doar ca sugestie orientativă, nicio valoare implicită. *Recomandare: da; ks e al geotehnicianului.*

### 14.11 Ce s-a hotărât la implementare (21.11.2026, decizia 753)

- **Codul** (`Lapin.Analysis/Solver/`): `FrameElement.Geometric` / `GeometricGlobal` (Kg consistentă, condensată cu Tc), `Axial`, `Inactive`, `Only`, `Member`; `Link.Spring` (`Ground`: forța resortului e reacție, `FrameSystem.Result`); `FrameProblem.Uplift`; `Nonlinear` (bucla pe stări, §14.1); `Buckling` (iterația în subspațiu pe G φ = μ K φ); `SupportSprings` (resorturile reazemelor și ale plăcilor pe sol); `AnalysisRun.Nonlinear` (rezultatele combinațiilor neliniare lângă cazuri, `CaseAt`, `Terms`, `Underlying` pentru kmod, flambajul la cerere). `ANode.Support` (platforma) ține reazemul care ține nodul. `SupportSettings` / `OneWay` (atributele `analysisSupport`, `analysisOnly`).
- **Flambajul ia doar barele comprimate** în G (rigidizarea din întindere lăsată deoparte, de partea siguranței): atunci G e pozitiv semidefinită și iterația găsește sigur cele mai mari μ > 0; cu întinderile incluse, la combinațiile cu vânt de ridicare modurile negative mari acopereau pe cele pozitive. P-Δ (bucla) păstrează și întinderile.
- **Modurile locale:** un mod din care un singur element (toate bucățile lui) ține peste 50 % din φᵀ G φ e flambajul acelui element (al verificării lui, 6.3); clasa EN 1993-1-1 5.2.1 se ia după primul mod global; când toate modurile găsite (6 implicit) sunt locale, ultimul αcr e o margine de jos („≥”). La hala de oțel din ghid primul mod e al unei pane Z 200 × 65 × 2 (αcr 1,83, 57 %), primul global 2,08 (pane și contravântuiri, fără tabla care le ține lateral: §14.7). Tabla (rigidizarea laterală a panelor) rămâne pentru mai târziu.
- **Amplificarea** raportată e deplasarea orizontală cea mai mare de ordinul doi față de cea de ordinul întâi; sub 0,1 mm deplasare orizontală de ordinul întâi e 1 (nicio deplasare laterală de amplificat). Pe cadrul ghidului (vânt pe Y): 1,0043, cu αcr = 184,7 (1/(1 − 1/αcr) = 1,0054).
- **Talpa izolată pe Winkler:** dreptunghiul ei în plan (dx pe X, dy pe Y): kz = ks dx dy, krx = ks dx dy³/12, kry = ks dy dx³/12, ux, uy, rz ținute. **Talpa continuă:** pe lângă rotirea în jurul axei (ks l B³/12), și cea transversală ks B l³/12 a bucății aferente, ca un stâlp pe talpă să nu rămână liber la rotire.
- **O bară într-un singur sens se scoate întreagă** (toate bucățile ei, după media N și lungirea totală). Criteriile: N < −10⁻⁶ max|N| − 10⁻⁹; lungirea > 10⁻⁹ m; un reazem trage când Rz < −10⁻⁹ × încărcarea; se așază când uz < −10⁻⁹ m.
- **Verificările de oțel** spun αcr al combinației SLU celei mai grele (cea mai mare încărcare verticală; un singur calcul de flambaj, ~0,3 s pe hală), când e sub 10 și P-Δ e oprit; cu P-Δ pornit spun că eforturile sunt de ordinul doi.
- **Cifrele benchmark-urilor** (raportul calculat / exact): Euler articulat (8 elemente) 1,000033; consola (16) 1,0000001; încastrat–încastrat (12) 1,00010; încastrat–articulat (12) 1,000028; cadrul portal (8 pe bară, bare inextensibile) sub 10⁻³ cu baza articulată și încastrată (cu barele extensibile αcr e cu 0,3 % mai mic: cadrul se balansează pe stâlpi, efect real, absent din formula clasică); P-Δ pe consolă la 0,5 Pcr: amplificarea 1,986288 față de 1,986288 exact (raportul 0,99999994), în 2 iterații; Hetényi la 1 %; placa pe sol w = q/ks la 10⁻⁹.
- **Viteza** (Debug, mașina încărcată): hala de oțel cu 12 contravântuiri doar la întindere, 41 de combinații SLU+SLS neliniare: ~5 s; flambajul tuturor celor 41 de combinații SLU (6 moduri): ~10 s, de aceea interfața îl cere la buton.
- **Aurul** (`SecondOrderGoldenTests`): fișierele scrise de codul dinaintea S5 (commit 5eee1e201) trec neschimbate după S5 (înfășurătorile SLU/SLS ale cadrului, un caz, modurile, forța tăietoare de bază, verificările; hala: maximele, reacțiile, SHA-256 al barelor, verificările).
- **Ce nu s-a făcut din §14:** nota „un resort Winkler bilateral trage” (§14.10.3); restul e în §14.7.

### 14.12 De hotărât de utilizator (în plus față de §14.10)

8. **Contravântuirile din bare rotunde** (tije, `analysisAxial` implicit) ar putea fi implicit doar la întindere. *Recomandare: nu implicit (ar schimba rezultatele proiectelor existente); interfața le arată ca propunere la hală mai târziu.*
9. **Panele fără tablă** dau moduri locale de flambaj cu αcr mic (hala din ghid: 1,8–2,1). *Recomandare: modelăm rigidizarea laterală a panelor de către tablă (un reazem lateral pe talpa comprimată) ca etapă mică după S5d.*

### 14.13 Răspunsurile utilizatorului (21.11.2026)

Utilizatorul a luat recomandările la toate cele 9 întrebări din §14.10 și §14.12 („la cele 9 întrebări mergem pe recomandări”): cazurile rămân liniare și doar combinațiile neliniare; placa pe Winkler ținută orizontal în noduri, cu notă; patul Winkler în ambele sensuri implicit, doar compresiune ca opțiune; combinația instabilă scoasă din înfășurători cu notă roșie; imperfecțiunile globale ±X/±Y urmează ca S5d; cadrele din beton clasificate deocamdată cu limitele 10 / 3 la rigiditate nefisurată; ks doar orientativ, fără valoare implicită (tabelul Bowles de confirmat); contravântuirile rotunde nu trec implicit la doar întindere; rigidizarea panelor de către tablă vine ca etapă mică după S5d.

**Hotărât de utilizator (21.11.2026, mai târziu):** tabelul Bowles al valorilor ks rămâne deocamdată cum e (valori din memorie, marcate orientative); nu se schimbă până nu e confirmat dintr-o sursă.

### 14.14 S5d: imperfecțiunile globale de înclinare (specificația, 21.11.2026)

**Ce cere norma.** SR EN 1993-1-1 5.3.2(3): imperfecțiunea globală de înclinare a cadrelor φ = φ0 · αh · αm, φ0 = 1/200, αh = 2/√h (h înălțimea structurii, m) cu 2/3 ≤ αh ≤ 1, αm = √(0,5 (1 + 1/m)), m numărul stâlpilor dintr-un șir care duc cel puțin 50 % din încărcarea verticală medie pe stâlp în planul vertical considerat. 5.3.2(7): înclinarea se poate înlocui cu forțe orizontale echivalente φ·NEd la capul și la baza fiecărui stâlp, în sensuri opuse (figura 5.4). 5.3.2(4): imperfecțiunile de înclinare se pot neglija când HEd ≥ 0,15 VEd. SR EN 1992-1-1 5.2(5)–(6) dă aceeași formă pentru beton: θi = θ0 · αh · αm, θ0 = 1/200 (anexa națională: valoarea recomandată), αh = 2/√l, 2/3 ≤ αh ≤ 1, αm = √(0,5 (1 + 1/m)), l înălțimea clădirii și m numărul elementelor verticale care contribuie la efect (pentru sistemul de contravântuire). SR EN 1995-1-1 5.4.4(2) dă pentru lemn altă formă: φ = 0,005 pentru h ≤ 5 m și φ = 0,005 √(5/h) peste, fără αm.

**Cum o face Lapin** (`analysisOptions.imperfections: true`; absent = oprit, deci proiectele vechi dau exact ce dădeau):
- **Stâlpii**: bucățile de bară cu axa la cel mult 10° de verticală, comprimate în rezultatul de ordinul întâi al combinației (N < 0, suprapunerea cazurilor; P-Δ își face apoi efectul pe forțele astea). Barele înclinate, grinzile, contravântuirile și pereții-placă nu primesc forțe (pereții: spus în notă; încărcarea lor verticală nu e înclinată, de dat ca încărcare unde contează).
- **h** = cota cea mai înaltă a capetelor stâlpilor − cota cea mai joasă a bazelor lor, a tuturor stâlpilor, comprimați sau nu (înălțimea structurii, aceeași în toate combinațiile); αh = 2/√h în [2/3, 1].
- **m, pe șiruri**: stâlpii de pe aceeași verticală (în plan la 0,1 m) sunt o linie de stâlpi, cu N-ul ei cel mai mare (de obicei baza); pentru înclinarea pe X, liniile cu același Y (la 0,3 m) sunt un șir (planul vertical X–Z), pentru Y cele cu același X. În fiecare șir m = liniile cu N ≥ 50 % din media liniilor comprimate ale șirului (cel puțin 1); un stâlp întins nu intră în șir (la vântul de ridicare un șir poate rămâne cu un singur stâlp comprimat: m = 1). Un stâlp singur pe șirul lui are m = 1 (αm = 1, de partea siguranței). Lectura „pe șir” e cea literală a normei; un număr pe tot nivelul ar da φ mai mic (neconservativ), spus.
- **φ pe material**: oțel, beton, zidărie și celelalte φ0 αh αm (EN 1993 / EN 1992, aceeași formă); lemn 0,005 √(5/h) cu 0,005 sus (EN 1995). Fiecare stâlp după materialul lui.
- **Forțele**: pe fiecare bucată de stâlp comprimată, φ·|N| la nodul de sus în sensul înclinării și −φ·|N| la cel de jos (perechea e în echilibru: suma reacțiilor orizontale nu se schimbă, forța tăietoare de nivel crește cu φ ΣN de deasupra). La un nod de reazem forța de jos trece direct în reazem.
- **Variantele**: fiecare combinație SLU statică (SLU, accidentală; ale utilizatorului la fel) se rezolvă de **patru ori**, cu imperfecțiunea pe +X, −X, +Y, −Y (EN 1993-1-1 5.3.2(8): pe fiecare direcție orizontală, una câte una). Combinațiile SLS și cea seismică nu primesc imperfecțiuni. Cu P-Δ pornit variantele trec prin bucla de la §14.1; fără el, o variantă e un calcul liniar (combinația + forțele ei), iar combinațiile SLS rămân suprapunerea cazurilor.
- **Ce se vede**: o combinație rămâne **un rând** (rezultatele ei = înfășurătoarea celor patru variante, ca la combinația seismică ±AEd; numele variantei „… + φ(+X)” apare unde ea guvernează: în verificări, în extremele înfășurătorii). Înfășurătoarea SLU și verificările iau toate variantele, deci cea mai grea. Tabelul „Stability” spune la fiecare combinație amplificarea cea mai mare și varianta ei; deasupra, φ pe X și pe Y (cel mai mare, cu m-ul lui), h, αh.
- **5.3.2(4)** (*recomandare: aplicate mereu*): imperfecțiunile se pun și când HEd ≥ 0,15 VEd (de partea siguranței, iar efectul lor e atunci mic); nota spune combinațiile și direcțiile în care norma ar permite neglijarea lor (HEd = suma forțelor orizontale ale combinației pe direcția aceea, VEd = suma celor verticale).
- **Pornirea**: `analysis.setOptions {imperfections: true | false | null}`; `secondOrder: true` fără `imperfections`, într-un proiect care n-a spus încă nimic despre imperfecțiuni, le pornește și pe ele (calculul de ordinul doi e complet doar cu ele, 5.2.2(3)); `imperfections: false` rămâne scris, ca o pornire ulterioară a P-Δ să nu le repornească. Imperfecțiunile merg și fără ordinul doi (ordinul întâi cu imperfecțiuni, cum cere 5.2.2(3) și pentru αcr ≥ 10). Nota S5 „imperfecțiunile nu sunt adăugate încă” se schimbă: cu ele pornite spune ce s-a pus, cu P-Δ pornit și ele oprite spune cum se pornesc.
- **Flambajul** (§14.3) unei combinații cu variante se calculează pe varianta +X (stările ei finale și N-ul ei): forțele de imperfecțiune sunt mici și orizontale, N-ul se schimbă puțin; spus.
- **Costul**: de patru ori combinațiile SLU; pe hala din ghid (P-Δ, 25 de combinații SLU) e de măsurat (§14.17).

Interogări: `analysis.run` primește `nonlinear.imperfections: {phi0, h, alphaH, x: {phi, m}, y: {phi, m}, timber, neglectable: [{key, name, direction}]}` și, la fiecare combinație cu variante, `variants: [{variant, key, ok, iterations, converged, amplification, force}]` (rândul combinației: ok = toate, iterațiile cele mai multe, amplificarea cea mai mare cu `governing`). `analysis.options` dă `imperfections`.

**Testele** (`ImperfectionTests`, solverul, și `SecondOrderAnalysisTests`, modelul):
- **Formula**: h = 6 m, m = 2: αh = 2/√6 = 0,816497, αm = √0,75 = 0,866025, φ = 1/(200√2) = 1/282,84 = 0,00353553 (la 10⁻⁹); h = 3 m: αh = 1 (limita de sus); h = 16 m: αh = 2/3 (limita de jos); m = 1: αm = 1; lemn h = 5 m: 0,005, h = 20 m: 0,0025.
- **Șirurile și criteriul de 50 %**: un șir cu patru stâlpi, N = 100, 100, 100, 20 kN: media 80, pragul 40, m = 3; cu un al doilea șir (alt Y) cu doi stâlpi: pe X două șiruri (m = 3 și 2); pe Y șirurile sunt stâlpii de același X: x = 0 și x = 5 câte doi (m = 2), x = 10 și x = 15 câte unul (m = 1). O grindă și un stâlp întins nu primesc forțe.
- **Forțele pe un cadru portal** (stâlpi h = 6 m articulați la bază, grinda 6 m, P = 500 kN pe fiecare cap): φ = 0,00353553; forțele la capete ±φP = 1,76777 kN; ordinul întâi al variantei +X dă exact deplasarea cadrului sub H = 2φP = 3,53553 kN în cap (la 10⁻⁹ relativ); suma reacțiilor orizontale e zero (perechile în echilibru), momentele la capul stâlpilor φP h la fiecare (cadrul articulat: M = H h / 2 pe stâlp).
- **Ordinul doi**: același cadru, P-Δ: deplasarea variantei +X / deplasarea de ordinul întâi ≈ 1/(1 − 1/αcr) (la 5 %, ca la §14.8).
- **Modelul (cadrul din ghid)**: patru stâlpi de 3 m: h = 3 → αh = 1; pe X două șiruri a câte doi stâlpi egali: m = 2, φ = 0,866025/200 = 0,00433013 = 1/230,9; la fel pe Y. Fiecare combinație SLU are patru variante, toate convergente; înfășurătoarea SLU cu imperfecțiuni ≥ cea fără (momentele la baza stâlpilor); suma forțelor de imperfecțiune pe o variantă = φ·ΣN stâlpi; un singur pas de undo; `secondOrder: true` pornește și imperfecțiunile; `imperfections: false` rămâne oprit la o nouă pornire.
- **Aurul**: fără opțiuni rezultatele dinainte (testele `SecondOrderGoldenTests` rămân neschimbate); P-Δ fără imperfecțiuni (`imperfections: false`) dă exact ce dădea S5.

### 14.15 Tabla care ține panele lateral (specificația, 21.11.2026)

**Problema.** La hala din ghid primele moduri de flambaj ale combinațiilor SLU cu vânt pe fronton (αcr 1,83; 2,08; 2,46) sunt ale panelor Z 200 × 65 × 2: pana, comprimată ca montant al contravântuirii acoperișului, flambează lateral **în planul acoperișului** (după axa ei slabă), între rigle, trăgând după ea contrafișele (fly braces). În realitate tabla prinsă de talpa de sus a panei o ține în planul ei (SR EN 1993-1-3 10.1; SR EN 1993-1-1 BB.2.1, rigiditatea la forfecare a tablei), iar flambajul rămas al panei e cel după axa tare, al verificării ei.

**Modelul (recomandare: reținerea simplă).** O bară „ținută de tablă” (`analysisSheeting: true`) rămâne **dreaptă pe direcția ei locală 3** (lățimea secțiunii, în planul tablei: în planul acoperișului la o pană, în planul peretelui la o riglă de perete) între barele pe care reazemă, **față de ele, nu față de teren**:
- Nodurile barei (toate bucățile ei, ordonate pe axa ei) se împart în deschideri de **noduri de rezemare**: capetele ei, nodurile ținute de un reazem și nodurile în care o altă bară o traversează în planul tablei (axa celeilalte la cel mult 30° de direcția locală 3 a barei: rigla sub pană, stâlpul sub rigla de perete). Contrafișele, cu axa aproape în lungul barei sau verticală, nu rup deschiderea.
- În fiecare nod interior al unei deschideri (a, b, nodul i la t = (xi − xa)/(xb − xa)): **deplasarea pe direcția d** (axa locală 3) egală cu cea a coardei: d·(ui − (1 − t) ua − t ub) = 0 (la capete cu brațele rigide ale bucăților: u + θ × off), și **rotirea după axa locală 2** (adâncimea) egală cu a coardei: θi·e2 + d·(ub − ua)/Lab = 0. Cele două legături țin și bucata dintre două noduri interioare (fără ele un singur element între două noduri ținute ar flamba cu rotirile libere, 12EI/L²).
- Legăturile sunt **penalizări** (ca diafragma rigidă, §11.2): k = 10⁴ × max EA/L al bucăților barei, pe vectorul legăturii; intră în rigiditate (static, P-Δ, flambaj, modal); forța lor e a tablei, nespusă. Nu leagă barele între ele: tabla nu e o diafragmă (cadrele nu se balansează împreună; efectul de șaibă al tablei, stressed skin, EN 1993-1-3 10.3, nu se ia), deci nu ascunde flambajul global al cadrelor.
- **Unde se pornește** (*recomandare: pe bară, cu „toate panele acoperișurilor” dintr-o comandă*): `analysis.setSheeting {ids?, purlins?: true, sheeting: true | null}` — barele date și/sau (`purlins: true`) toate barele pe care acoperișurile cadrate le numesc purtătoarele învelitorii (panele halelor de oțel); un pas de undo. Implicit oprit (proiectele vechi neschimbate). În Proprietăți, la o bară: „Braced by sheeting”; în secțiunea „Stability”, când un mod local e al unei pane: butonul „The sheeting braces the purlins”.
- **Verificările** de oțel (§12.2) nu se schimbă: flambajul prin încovoiere-răsucire al panei (6.3.2) își păstrează lungimile din setările de proiectare; spus.

**Testele** (`SheetingTests`):
- **Euler**: stâlpul articulat (8 elemente, IPE 300, Local2 = X) flambează la π²EI22/L² după axa slabă; ținut de tablă (direcția locală 3 = Y), modul acela dispare și primul αcr e π²EI33/L² (axa tare) la 10⁻³, iar deplasările pe Y ale nodurilor interioare în mod sunt sub 10⁻⁶ din cele pe X.
- **Mișcarea de corp rigid** nu e oprită: o translație și o rotire oarecare a barei satisfac legăturile exact (forța lor zero).
- **Static**: o grindă simplu rezemată (axa X, adâncimea Z) cu o forță laterală (pe Y) la mijloc: ținută de tablă, săgeata laterală a mijlocului față de coardă e zero (la 10⁻⁶ din cea fără tablă), reacțiile sunt aceleași (echilibrul).
- **Deschiderile**: o pană pe trei rigle (două deschideri): nodul rigiei din mijloc rupe deschiderea (deplasarea lui după d nu e legată de capete); o contrafișă nu o rupe.
- **Hala din ghid**: cu `purlins: true` modurile locale ale panelor dispar din primele șase ale combinației cu αcr 1,83; αcr cel mai mic și primul global după, raportate (§14.17); comanda un pas de undo; fără ea aurul neschimbat.
