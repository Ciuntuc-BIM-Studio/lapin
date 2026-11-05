# Optimizarea unui fișier IFC (File → Optimize IFC…)

Cerut pe 05.11.2026 (decizia 318): un caz de folosire pentru IFC-urile importate — modelul curățat și optimizat, salvat ca o **copie** cu o terminație în plus ca să se deosebească, **fără** parametri de intenție Lapin și fără cantități, restul IFC-ului rămânând absolut cum era: GUID-urile și parametrii originali. Regula de bază a utilizatorului rămâne: un IFC existent nu se scrie niciodată, rezultatul e întotdeauna un fișier nou lângă el.

## Ce este și ce nu este

Nu e exportul IFC al lui Lapin (acela scrie din modelul lui Lapin, cu GlobalId-uri calculate și `Lapin_Intent`). E o **trecere de la fișier la fișier** peste STEP-ul original (`engine/src/Platform/Lapin.Ifc/IfcOptimize.cs`):

- fișierul e citit o dată, în ordinea lui (`Scan`): fiecare entitate cu numărul, tipul, unde începe și unde se termină textul ei, dacă are GlobalId (prima valoare un șir de 22 de caractere) și ce referă; entitățile se parsează la cerere cu `StepFile` (același parser ca la import);
- se scriu înapoi **octet cu octet** toate entitățile pe care trecerea nu le schimbă, cu spațiile și rândurile dintre ele; antetul (`FILE_DESCRIPTION`, `FILE_NAME` cu data lui, `FILE_SCHEMA`) e copiat exact; schema rămâne a fișierului (IFC2X3, IFC4, IFC4X3);
- numerele entităților se păstrează; ce e nou (o hartă a unui corp partajat, fețele unui B-rep simplificat) primește numere după ultimul din fișier; ce dispare lasă un gol în numerotare;
- un fișier fără mesh-uri și fără orfani iese identic (testat, și cu rânduri `\r\n`).

**Nu se schimbă niciodată:** nicio entitate cu GlobalId (IfcRoot: produse, tipuri, relații, seturi de proprietăți și de cantități, proiectul, nivelurile), proprietățile și cantitățile (`IfcPropertySingleValue`, `IfcQuantity*`), materialele, unitățile, contextele, plasările produselor, antetul. Nu se adaugă nimic al lui Lapin: niciun `Lapin_*`, nicio cantitate, niciun set nou. Se schimbă doar geometria: corpurile din triunghiuri, reprezentările lor și resursele pe care nu le mai folosește nimic.

## Ce face (fiecare opțiune, cu valoarea ei sigură)

| Opțiune | Implicit | Ce face |
|---|---|---|
| Simplificare | da, 1 mm | `IfcTriangulatedFaceSet`, `IfcPolygonalFaceSet`, `IfcFacetedBrep(WithVoids)`, `IfcShellBasedSurfaceModel`, `IfcFaceBasedSurfaceModel` din fețe plane (`IfcPolyLoop`): colapsarea muchiilor după eroarea cuadrică (`MeshSimplify.Reduce`, același cod ca la LOD-urile #203, în varianta strictă: fiecare plan numărat întreg, costul suma lor); marginile deschise și pliurile peste 20° (`Tessellator.CreaseDeg`) rămân pe loc; un colaps e făcut doar dacă fiecare colț original strâns în el rămâne la cel mult toleranța de triunghiurile rămase; la final abaterea e **măsurată** în ambele sensuri (colțuri, mijloace de muchii, centre, pe grila triunghiurilor) doar acolo unde s-a schimbat ceva; peste toleranță se reia la jumătate, apoi corpul rămâne cum era. Fețele coplanare se reduc la cele câteva triunghiuri care le acoperă. Corpurile analitice (`IfcExtrudedAreaSolid`, `IfcBooleanResult`, profile, suprafețe avansate) nu se ating. Preseturile ca la Display quality: Fast 5 mm, Balanced 1 mm, Precise 0,1 mm, sau toleranța ta (0,001–100 mm). |
| Curățare | da | vârfuri duplicate sudate (la 0,01 mm, sau la toleranță când e mai mică; primul rămâne unde e), triunghiuri degenerate și repetate (aceleași colțuri, în același sens: un triunghi întors e spatele unei fețe și rămâne) scoase, coordonate nefolosite scoase; la un B-rep curățat fără simplificare buclele arată spre punctele sudate și fețele fără arie sau repetate ies din shell, fără entități noi. |
| Corpuri partajate | da | reprezentările `Body` folosite doar de produse, ale căror itemuri sunt corpuri din triunghiuri folosite doar de ele, stilizate la fel și fără layer propriu, care au aceeași formă mutată (colțurile la 0,001 mm, aceleași triunghiuri): prima își mută itemurile într-un `IfcRepresentationMap` nou, fiecare (și prima) devine un `IfcMappedItem` al ei cu un `IfcCartesianTransformationOperator3D` care o mută; `IfcShapeRepresentation`-urile își păstrează numerele, produsele și plasările lor nu se ating. Doar unde merită (mai mulți octeți câștigați decât costă itemurile mapate). Hărțile identice (aceeași plasare, aceleași corpuri) folosite doar de itemuri mapate devin una. Rotațiile nu sunt recunoscute încă: doar translații. |
| Orfani | da | resurse de geometrie și prezentare pe care nu le referă nimic: puncte, direcții, plasări, bucle, fețe, shell-uri, corpuri, reprezentări (și cele fără itemuri, scoase din formele produselor care mai au altele), hărți, stiluri de suprafață, culori. Niciodată o entitate cu GlobalId, niciodată ceva referit; un `IfcStyledItem` pleacă odată cu itemul lui, un `IfcPresentationLayerAssignment` îl lasă din listă. Ce lasă trecerea în urmă (itemurile vechi ale unui corp partajat) se scoate și cu opțiunea oprită. |
| Rotunjirea coordonatelor | nu | coordonatele corpurilor din triunghiuri la 0,01 mm. |
| B-rep → face set | nu | doar IFC4 și IFC4X3: un `IfcFacetedBrep` devine `IfcTriangulatedFaceSet` (același număr), reprezentarea lui `Tessellation`; IFC2X3 își păstrează B-rep-urile. |
| .ifczip | nu | copia arhivată, cu `.ifc`-ul înăuntru. |

Ce rămâne neatins: face set-urile cu normalele lor (`Normals`) sau cu hărți de culori/texturi pe indici (`IfcIndexedColourMap`, `IfcIndexed*TextureMap`), raportate ca „Kept as written”. Un corp e rescris doar când textul nou e mai mic decât cel vechi (un poligonal face set cu fețe mari rămâne poligonal).

## Copia

- Numele: al originalului cu **terminația** înaintea extensiei, implicit `.optimized` (`casa.ifc` → `casa.optimized.ifc`), configurabilă și ținută minte pe calculator cu celelalte opțiuni.
- Locul: lângă original sau în folderul ales. Ținta care ar fi chiar originalul e refuzată; o copie care există deja e suprascrisă doar după întrebare (`ifc.optimize` răspunde `exists` și interfața întreabă).
- Scrierea: într-un `.part` lângă țintă, mutat peste ea la sfârșit; o trecere anulată sau eșuată nu lasă nimic.

## Interfața

- **File → Optimize IFC…** (secțiunea IFC and Fragments) și butonul de pe ecranul de start: nu trebuie proiect deschis. Unul sau mai multe fișiere, presetul sau toleranța, opțiunile, terminația, folderul; **Optimize** le face pe rând, fiecare cu etapele motorului și bara lui, și apoi raportul: mărimea, entitățile și triunghiurile înainte → după, corpurile partajate, itemurile mapate, hărțile unite, orfanii scoși, abaterea cea mai mare măsurată, durata; **Open folder**, **Open in Lapin** (altă fereastră, `open_separately`).
- **Cancel** (desktop): `cancel.long` trece pe lângă coada apelurilor gazdei (`EngineHub.Own`) și oprește trecerea (`CancellationToken`). În motorul din browser nu se poate opri: acolo fișierele sunt oricum în memorie.
- **Optimize the source file…**: pe grupul unui IFC importat după cale (grupul ține fișierul în `source`) și pe o legătură IFC (Manage → Links), cât timp fișierul e încă pe disc.
- Desktop: dialogurile native Tauri; browser: fișierul citit ca octeți, copia descărcată.
- Memoria: fișierul citit o dată ca octeți, plus indexul (numere, deplasamente, referințe); entitățile nu se țin parsate, copia se scrie în flux. Corpurile se lucrează în paralel, cele mari întâi.

## Măsurători (05.11.2026, MacBook, Release)

| Fișier | Mărime | Entități | Triunghiuri | Corpuri partajate | Orfani | Abatere | Timp |
|---|---|---|---|---|---|---|---|
| `Bloc-P-4.ifc` (IFC4, ifc-lite, 359 B-rep-uri) | 2,92 MB → 2,34 MB (−20 %) | 62.051 → 47.392 | 4.944 → 1.032 | 26 corpuri pentru 178 de produse | 16.649 | 0,000 mm | 0,1 s |
| `TPJ…_IFC4_007.ifc` (IFC4, BIMCamel, 8.356 face set-uri în hărți) | 81,8 MB → 72,5 MB (−11 %) | 261.185 → 261.125 | 1.760.908 → 1.725.852 | 12 hărți unite | 60 | 1,000 mm | 5,3 s |
| `test.ifc` (IFC4 scris de Lapin, 14.511 B-rep-uri) | 120,2 MB → 104,6 MB (−13 %) | 1.960.867 → 1.657.306 | 523.986 → 387.030 | 1.222 corpuri pentru 10.465 de produse, 62 hărți unite | 426.192 | 0,64 mm | 1,8–2,7 s |

Pentru `Bloc-P-4` abaterea a fost măsurată și produs cu produs (`IfcModel.Shape`, înainte și după, în ambele sensuri): 0,000 mm. Copia importată în Lapin are aceleași 1.463 de GlobalId-uri ale fișierului ca originalul. Câștigul vine aici din corpurile partajate (ușile și ferestrele repetate) și din resursele rămase fără folos; mesh-urile lui sunt deja minime. La fișierul BIMCamel mesh-urile sunt dese și curbe, deci la 1 mm câștigul e mic: o toleranță mai mare (Fast) scoate mai mult.

`LAPIN_IFC_OPTIMIZE=a.ifc;b.ifc` (și `LAPIN_IFC_OPTIMIZE_OUT=folder`) rulează `IfcOptimizeTests.Measure_when_given` pe fișiere reale: raportul, etapele cu timpii lor, verificarea că tot ce nu e geometrie a rămas octet cu octet, iar sub 20 MB și abaterea produs cu produs și importul în Lapin.

## Teste

`IfcOptimizeTests`: copii IFC4 și IFC4X3 (o placă de 800 de triunghiuri, un acoperiș ondulat, un stâlp de trei ori, o cutie cu colțuri repetate și triunghiuri proaste, un face set cu normale, o extrudare, orfani) și IFC2X3 (B-rep-uri cu puncte comune, cu puncte proprii pe fiecare față, stâlpi identici mutați): fiecare entitate care nu e geometrie identică, antetul identic, aceleași produse cu GlobalId, nume, clasă și tag, aceleași proprietăți și cantități, abaterea măsurată sub toleranță, corpurile partajate, orfanii, nicio referință spre ceva ce lipsește, nimic `Lapin_` adăugat; fișierul fără mesh-uri identic octet cu octet; B-rep → face set doar în IFC4; numele copiei, refuzul de a scrie peste original, `.ifczip`, anularea fără urme; importul copiei în Lapin cu aceleași GlobalId-uri. `ifcOptimize.test.ts`: numele copiei, presetări, opțiunile trimise, ce se ține minte.
