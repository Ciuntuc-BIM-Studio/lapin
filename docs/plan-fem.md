# Calcul structural (FEM) în Lapin: studiu

Cerut de utilizator (12.11.2026): să studiem cum ar putea Lapin să aibă și un motor de calcul cu elemente finite, ținând cont de ce există open source.

**Hotărât de utilizator (12.11.2026, „facem solver intern C# — începem cu S0”):** varianta C, un solver propriu în C#, MIT, în etape; exportul (JG Structures, IFC SAV) rămâne. Prima etapă e S0, specificată la §8. Întrebările 2–4 de la §7 rămân deschise.

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
| **S4. Verificări după normă** | Beton (EN 1992-1-1: încovoiere, forfecare, armătura necesară, legată de modulul `rebar`), oțel (EN 1993-1-1: rezistența secțiunii, flambajul, flambajul lateral), lemn (EN 1995-1-1). | Candidat pentru un modul premium; aici e munca mare și răspunderea. |
| **S5. Mai târziu, poate niciodată** | P-Δ, reazeme elastice (sol), legături doar la compresiune, analiza în etape de execuție, neliniar. | Aici ar avea sens xara sau o punte către OpenSees / Code_Aster. |

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
2. **Deschis sau premium:** S0–S3 în modulul deschis și S4 (verificările după normă) premium, într-un depozit privat ca `planning-pro`?
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
- Lăsat pentru mai târziu, spus în raport: 6.10a/6.10b, simultaneitatea zăpezii cu încărcarea utilă pe acoperișuri (EN 1991-1-1 3.3.2), combinația seismică (cu S3), valorile CR 0-2012 (de confirmat de utilizator).

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
- **Rezolvitorul** (RCM + skyline LDLᵀ): 26.460 de grade de libertate în 14 s, 86.490 în 106 s (1,25 GB); peste 400 M de intrări în profil e refuzat. La S2 (plăcile, modelele mari) un Cholesky rar propriu (supernodal, cu reordonare AMD sau disecție imbricată) îl înlocuiește, tot cod al lui Lapin.
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

**5. SAV după filtrele exportului.** `AnalyticalOptions.Exclude`: ce lasă deoparte exportul (Exclude, și ce nu e construit încă în faza exportată) lasă deoparte membrul, reazemele, legăturile și încărcările derivate din el (`IStructuralLoads.For(query, options)`).

**6. Notele traduse.** Ca la celelalte mesaje ale motorului: interfața le traduce (frazele fixe în dicționar, cele cu numere și nume prin tipare), în toate limbile din `ui/src/i18n`; numele implicite ale cazurilor (în model rămân în engleză) se afișează traduse (`t(name)`); notele din dialogul de rezultate nu mai sunt `data-no-i18n`.

Teste: `ClimateLoadTests` (μ1, ce(10) = 2,3523 la terenul II, cpe după h/d, corelarea; parametrii verificați și cazurile făcute într-un pas de undo; zăpada pe casa cu două ape la 45°, μ1 = 0,4, aglomerată 0,75; placa neacoperită 0,8 și cea acoperită fără; vântul pe cadrul 6 × 4: zonele D, E, A, B, C, F, G, H, I cu numerele calculate de mână, tot pus, jumătate din fațade în teren; hala: Tabelul 7.4a, panele duc zăpada, echilibrul), `AxialMemberTests`, `BeamLevelTests`, `StructuralIfcTests.The_view_leaves_out…`, `notes.test.ts`, `loads.test.ts`.

**De hotărât de utilizator:** (a) combinațiile mixte ale Tabelului 7.4a (presiune pe apa din vânt, sucțiune pe cealaltă) și cpi (+0,2/−0,3): încă 8–16 cazuri de vânt; (b) aglomerarea pe acoperișurile în patru ape (lectura noastră) și vântul pe ele (Tabelul 7.5); (c) direcțiile vântului după axele proiectului sau ale clădirii; (d) valorile CR 1-1-4 pentru kr și ce (aici EN).
