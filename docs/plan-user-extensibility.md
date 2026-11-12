# Configuratoare și biblioteci pe care utilizatorii le extind: raport și propuneri

Cerut de utilizator (12.11.2026): analiza configuratoarelor și a bibliotecilor (inclusiv simbolurile 2D) și propuneri ca utilizatorii să-și extindă aplicația **indiferent de țară sau industrie**: simboluri și săgeți desenate în Drafting pentru liniile de secțiune și fațadă, simboluri pe planșe, biblioteci încărcate de utilizator, polilinii transformate în armătură 2D și 3D, încărcări citite din CSV etc. Aprobat de utilizator la 12.11.2026 (§9); în lucru pe ramura `feature/user-extensibility`, etapă cu etapă (§10).

## 0. Pe scurt

Lapin are deja **un model bun de bibliotecă**: bibliotecile de profile (`docs/plan-profile-libraries.md`). Au formate deschise (CSV/XLSX cu coloanele ghicite, IFC, desen, DXF), o bibliotecă încorporată mică, „Pe acest calculator”, folderele firmei, copia în proiect doar a ce se folosește, „mai nou în bibliotecă” cu Actualizează și magazinul de extensii. Restul aplicației nu le urmează încă: **simbolurile 2D sunt desenate din cod**, **configuratoarele sunt formulare scrise de mână peste înregistrări C#**, **normele sunt constante C#**, iar **importurile de tabele sunt trei dialoguri separate**.

Propunerea se reduce la **cinci piese comune**, fiecare construită o dată și folosită de toate modulele:

| # | Piesa | Ce deblochează | Peste ce există |
|---|---|---|---|
| 1 | **Simbolul 2D** (`LapinSymbolDef` + instanțe), desenat în Drafting și salvat | săgețile secțiunilor și fațadelor, capetele de nivel, bulinele axelor, nordul, scara grafică, liniile de rupere, titlurile vederilor, contururile etichetelor, simbolurile de pe planșe, blocurile DXF păstrate | elementele cartușelor (`TitleBlocks/*`), jetoanele `{…}` ale etichetelor (`TagText.cs`), DXF-ul care scrie deja cartușele ca BLOCK |
| 2 | **Biblioteca unificată**: sursele profilelor (încorporată, calculator, firmă, proiect, extensii) pentru *orice* fel | tipuri, simboluri, standarde de desen, tipuri de linie, mobilier, componente, reguli, forme de bare, parametri, clasificări, într-un singur loc | `ProfileLibraries/`, `LibraryMerge.cs` + `LibraryChoices.tsx` (decizia 506), Catalogul (decizia 263, etapa 4b nefăcută), `addContentInstaller` |
| 3 | **Importul de tabele comun**: un singur dialog de potrivire a coloanelor, cu ținte înregistrate de module | încărcările structurale, încăperile, valorile proprietăților (dus-întors cu extrasele), sarcinile planificării, clasificările, cheile de notă (keynotes), listele de armătură | `PriceLists.Guess`, `ProfileTables`, `Tables.cs`, `XlsxReader`, `TableImport.tsx` |
| 4 | **„Convertește desenul în…”**: o comandă cu ținte înregistrate de module | polilinii → pereți, plăci, grinzi, balustrade, fundații, **bare de armătură 2D și 3D**, încăperi, trasee MEP; straturi DXF → elemente | `steel.convertSketch`, Road / Breakline from linework, TWALL/RING/BOX din Studio, conturul → profil |
| 5 | **Configuratoare din schemă + pachete de reguli** | parametrii și normele ca date, alese pe țară, editate de utilizator; formulare generate | `ParamDef`/`GateParam` + `typeConfigurator.tsx` (porțile și ventilatoarele), `StairRules`, `RoofCatalog`, „indicativ” |

Ordinea recomandată e în §8: întâi **simbolul 2D** (cel mai cerut, iar restul adnotărilor se sprijină pe el), apoi **importul de tabele** cu încărcările și proprietățile, apoi **conversia desenului** cu armătura.

## 1. Ce există azi (pe scurt, cu locurile)

### 1.1 Simboluri, adnotări, desen

| Ce | Cum e făcut | Extensibil de utilizator? |
|---|---|---|
| Marcajul secțiunii | cod: `Lapin.Platform/SectionMarkers.cs`; doar mărimea, textul și stratul (`StyleBook.SectionMarkSize`, `markSize` pe vedere) | nu: forma capului și a săgeții e fixă |
| Marcajul fațadei | cod: `ElevationMarkers.cs` (cerc + săgeată plină); mărimea, întinderea, `markerShift` | nu |
| Capul de nivel | `Views.cs:1091` `LevelMarkStyles = ["open","half","filled","tick"]` | doar alegerea dintre 4 |
| Bulina axului | `AxisStyle.Bubble` = circle / square (`Lapin.Drawing/Standards.cs:161`) | stilul da, forma nu |
| Săgeata cotei | `DimStyle.Arrow` = tick / arrow / dot / none (`Standards.cs:148`) | doar alegerea dintre 4 |
| Nordul, scara grafică | bife pe vederile de situație (`SitePlans.cs`) | nu se pot așeza liber, nici pe alte vederi |
| Titlul vederii pe planșă | cod: `Sheets.cs` | nu |
| Callout / detaliu | `LapinInset` (o vedere în vedere); niciun simbol de callout | nu |
| Linia de rupere, cota de nivel punctuală | doar forma de desen `level` (LV); fără linie de rupere | — |
| Etichete (ușă, fereastră, încăpere, alcătuire) | **date**: `TagType` cu șablon `{câmp}`, formă box/opening, săgeată (`Tags.cs`, `TagText.cs`, `BuildupTags.cs`) | textul da; conturul nu |
| Cartușe | **date**: `LapinTitleBlock` (linii, dreptunghiuri, polilinii, umpluturi, text parametric, imagini, tabelul reviziilor), designer, JSON `lapin.titleBlock` v1, bibliotecă pe calculator, fel de extensie | **da**: singurul simbol 2D complet al utilizatorului |
| Hașuri | `.pat` importat, ținut pe IfcProject (decizia 54), „Hașurile mele” | da |
| Tipuri de linie | listă fixă (`LineStyles.cs`: solid, dashed, hidden, center, dotted; straturile au 4) | **nu** (fără `.lin`) |
| Peniță, stiluri de obiect, stiluri de text / cotă / ax, straturi | `StyleBook` pe `IfcProject.attrs["drafting"]` | editabile în proiect; **nicio bibliotecă**, doar prin șablonul de proiect |
| Drafting | linii, polilinii, cercuri, arce, text, săgeți, cote, `level`, hașuri; grupuri | **niciun bloc / simbol refolosibil** |
| DXF | totul devine desen; `INSERT` e **explodat** (`DxfImport.cs:115`); straturile devin straturi de desen | blocurile se pierd |
| Planșe | vederi, cartuș, extrase, imagini | nimic 2D liber pe planșă, fără legende (doar legenda culorilor încăperilor) |
| Simbolurile obiectelor | uși / ferestre (`JoinerySymbols`, DIN 1356…), plante (`PlantSymbols.cs`), anturaj (`Entourage2d`) | procedurale, din cod |

### 1.2 Configuratoare și tipuri

- **Aproape fiecare configurator e o înregistrare C# cu un dialog React scris de mână**: tâmplăria (`Joinery.cs`, `JoineryDialog.tsx`, ~1100 de rânduri), scările (`StairSpec.cs`), acoperișurile, pereții cortină, balcoanele, liftul, mobilierul (`Casework.cs`), nodurile metalice (`NodeParams.cs`), lemnul, finisajele încăperilor. Utilizatorul schimbă valori și salvează tipuri; **nu poate adăuga un fel nou** (un stil de foaie de ușă, o formă de scară, o formă de bară, un fel de nod) fără cod.
- **Excepția, formularul din schemă**: `ParamDef`/`GateParam` (cheie, etichetă, fel, min, max, alegeri, `showIf`) servit de motor și desenat generic de `ui/src/shell/typeConfigurator.tsx`, cu previzualizarea motorului. Doar porțile și ventilatoarele îl folosesc.
- **Tipurile** circulă prin JSON-ul pereților și plăcilor (v2, decizia 506) și prin `.lapintypes` (orice clasă de tip), cu alegerile la conflict (`LibraryMerge.cs`); parametrii de instanță (`TypeDefaults.cs`); parametrii utilizatorului (`Params.cs`: text, număr, lungime, arie, unghi, bifă, alegere) **fără formule**.
- **Stiluri salvate de utilizator** există doar pe alocuri: stilurile proprii de acoperiș (`ProjectRoofStyles.cs`, `own:*`, în proiect), biblioteca de mobilier (`.lapinlib`), șabloanele de proiect. **Componentele și tipurile de apartament stau doar în proiect**, fără export.
- **Mobilierul din modele**: registrul celor 39 de modele GLB CC0 e făcut la build (`furniture-assets.json`); utilizatorul nu-și poate aduce un model propriu cu zonele de întindere.
- **Niciun editor de familii**: Studio face solide din schițe, dar fără parametri sau constrângeri.

### 1.3 Normele și datele naționale

- **În cod**: regulile scărilor (STAS 2965, NP 063, P118 în `StairRules`; valorile se editează pe tip, setul nu), raza de îndoire și diametrele armăturii (SR EN 1992-1-1, 6–32 mm), mărcile oțelului-beton (B500C, B500B, BST 500S, PC52, OB37), clasele șuruburilor (4.6–10.9), pantele minime ale învelitorilor (`RoofCatalog`), specia plantelor (decizia 234), straturile armăturii cu nume românești.
- **Ca date ale utilizatorului**: profilele (AISC importat de utilizator), listele de prețuri și normele de productivitate (indicative), cartușele, hașurile, tipurile, șabloanele de proiect.

### 1.4 Importuri și conversii

- **Importuri cu potrivirea coloanelor**: tabelele de profile (cel mai bun: unitățile din titlu, verificarea față de valorile calculate), listele de prețuri, normele de productivitate, progresul planificării. **Trei dialoguri separate**, fără o piesă comună.
- **Fără import**: încărcările structurale (doar comenzi și derivate; exportul SAV e într-un singur sens, `docs/plan-fem.md`), încăperile din tabel, sarcinile planificării din CSV, valorile proprietăților pe elemente (Mark/GlobalId → valoare), materialele în bloc.
- **Clasificările lipsesc cu totul**: niciun `IfcClassificationReference`, Uniclass, OmniClass, nicio cheie de notă (keynote).
- **Conversii din desen**: Studio (RING, BOX, TWALL, Extrude, Follow-me), `steel.convertSketch`, drum și linie de rupere din linii (civil), conturul → profil. **Nimic pentru pereți, plăci, grinzi, balustrade din polilinii date, și nimic pentru armătură.**
- **Armătura** (`RebarShapes.cs`): `ShapeDef` e „blocul dinamic” al configuratorului, dar vârfurile și gripurile sunt funcții C# (`Func<…>`), deci catalogul (drept, L, U, etrier, agrafă, polilinie, punct) e închis. Fără coduri de formă BS 8666 / ISO 4066, fără cotele A–E pe extras, fără export spre mașinile de fasonat.
- **Automatizare**: Python (`lapinbim`, consola, înregistratorul, uneltele din scripturi, `lapin.ui.ask`), MCP, catalogul de comenzi. Pe desktop, în spatele Developer extras.

## 2. Principiile propunerilor

Toate respectă regulile din `CLAUDE.md`; câteva sunt spuse aici fiindcă decid forma:

1. **Datele, nu codul.** Un fel nou (un simbol, o formă de bară, un set de reguli, o țintă de import) e un fișier JSON citit și verificat fără motor, ca `.lapintypes` (CI-ul catalogului, Node pur).
2. **Ce e încorporat devine prima intrare a bibliotecii, cu desenul de azi.** Săgeata de secțiune de azi devine simbolul încorporat „Lapin standard”, numai de citit și duplicabil, cu aceeași ieșire la pixel (teste de regresie pe desene). Proiectele vechi nu se schimbă, deci nu e nevoie de `IProjectUpgrade` decât unde se mută date.
3. **Proiectul ține o copie a ce folosește**, ca profilele: se deschide oriunde, iar biblioteca spune „mai nou în bibliotecă”.
4. **Un import sau o conversie = un pas de undo**, cu previzualizare și listă de erori pe rând înainte de aplicare.
5. **Previzualizarea e a motorului**, construită de același cod ca elementul.
6. **Normele și prețurile sunt ale utilizatorului**; ce livrează Lapin e marcat indicativ.
7. **Conținutul nu rulează.** Formulele din §5.3 cer o hotărâre (§9, întrebarea 3): un limbaj de expresii declarativ, evaluat de motor, nu cod.
8. **Formate publice, fără dependențe**: `.lin`, DXF BLOCK/INSERT, `IfcAnnotation`, `IfcClassificationReference`, BVBS (după specificația publică, de verificat licența ei).
9. **Ediția web**: tot ce e conținut merge și în browser (IndexedDB); folderele firmei doar pe desktop, ca la profile.

## 3. Simboluri 2D și adnotări

### 3.1 Simbolul: definiția și instanța

**`LapinSymbolDef`** (în proiect) = un desen mic în **mm de hârtie** sau în **metri de model**, cu:

- **elementele** cartușului, refolosite ca atare (linie, polilinie cu arce, cerc, dreptunghi, umplutură, hașură, text, imagine), deci același randator pe ecran, în planșe, în PDF și în DXF;
- **punctul de inserție** și **direcția** (axa x a simbolului), ca un simbol să se poată roti după linia secțiunii;
- **atributele**: textele cu jetoane (`{view.number}`, `{sheet.number}`, `{level.elevation}`, `{param.x}` sau un câmp liber completat la așezare), ca la etichete și cartușe;
- **comportamentul la scară**: *adnotare* (mărime constantă pe hârtie, ca marcajele) sau *model* (mărime reală, ca un obiect sanitar);
- **gripuri de întindere** opționale (o „zonă elastică” între două puncte, de exemplu linia dintre capetele unei linii de rupere), ca `ShapeGrip` de la armătură, dar ca date;
- **vizibilitatea pe scară sau pe nivel de detaliu** (un simbol simplificat sub 1:200), prin `VisibilityGraphics`;
- **straturi pe categorii de peniță** (tăiat, văzut, adnotare), deci urmează stilurile de obiect și suprascrierile vederii;
- **categoria** (marcaj de secțiune, cap de nivel, nord, mobilier 2D, electric, sanitar, incendiu…) și etichetele de căutare.

**Instanța** (`IfcAnnotation` cu reprezentarea `Annotation` în contextul planului, un set propriu `Lapin_Symbol` cu definiția și atributele, GlobalId stabil) se așază în orice vedere sau **direct pe planșă**. La export IFC rămâne o adnotare; la DXF devine `BLOCK` + `INSERT` cu `ATTRIB` (exportul cartușelor face deja asta).

### 3.2 Desenat în Drafting, salvat ca simbol

- **Selecție → Salvează ca simbol…**: se aleg punctul de inserție, direcția, numele, categoria și felul de scară; textele care conțin `{…}` devin atribute. Desenul selectat poate fi înlocuit pe loc de o instanță.
- **Editează simbolul** deschide o vedere de desen a simbolului (aceleași unelte de Drafting, nu un designer separat, cu jetoanele slotului afișate ca text de probă); la închidere, toate instanțele se actualizează într-un pas de undo.
- **Blocurile DXF**: opțiunea „Păstrează blocurile ca simboluri” la import (azi `INSERT` e explodat): fiecare bloc folosit devine o definiție, fiecare inserție o instanță, `ATTRIB` devine atribut. Implicit rămâne explodarea, ca acum.

### 3.3 Sloturile de simbol: marcajele desenate azi din cod

Fiecare marcaj al vederilor devine un **slot** cu un simbol ales în stilul lui, iar slotul spune ce jetoane și ce puncte de ancorare oferă:

| Slot | Jetoane | Ancore / comportament | Unde se alege |
|---|---|---|---|
| Capul secțiunii (stânga, dreapta, separat) | `{view.number}`, `{view.sheet}`, `{view.name}` | pe capătul liniei, x de-a lungul liniei, oglindit după parte | Standarde de desen → Secțiuni; suprascris pe vedere |
| Săgeata / coada secțiunii, cotul secțiunii frânte | — | pe linie, după sensul privirii | idem |
| Marcajul fațadei (cap + săgeți, 1–4 direcții) | idem | centrul, fiecare săgeată rotită spre vederea ei | Standarde → Fațade |
| Callout / detaliu | `{view.number}`, `{view.sheet}` | pe conturul zonei (dreptunghi, cerc, nor) | Standarde → Detalii |
| Capul de nivel | `{level.name}`, `{level.elevation}` | pe capătul liniei nivelului | în locul `LevelMarkStyles` |
| Bulina axului | `{grid.label}` | pe capăt, cu lungimea liniei | în locul `AxisStyle.Bubble` |
| Terminatorul cotei | — | pe capăt, după direcție | în locul `DimStyle.Arrow` |
| Cota de nivel punctuală | `{point.elevation}`, `{point.relative}` | pe punctul ales (plan, secțiune) | unealtă nouă |
| Titlul vederii pe planșă | `{view.number}`, `{view.name}`, `{view.scale}` | sub viewport, lățimea urmează viewportul (zonă elastică) | Standarde → Planșe |
| Conturul etichetei | jetoanele `TagType` | în jurul textului (zonă elastică) | în `TagType.Shape` |
| Norul de revizie + eticheta lui | `{revision.number}` | pe contur | unealtă nouă |

Cele 4 + 2 + 4 variante de azi (capete de nivel, buline, săgeți) devin simboluri încorporate cu aceleași nume, deci un stil vechi care spune `"filled"` găsește simbolul `builtin:level-filled`.

### 3.4 Simboluri de așezat liber

- **Nordul și scara grafică** în orice vedere și pe planșă (azi doar bife pe vederile de situație). Scara grafică are nevoie de un element **calculat**: numărul și lungimea segmentelor din scara vederii. Propunere: un element de simbol de fel `scaleBar` (segmente, subdiviziuni, etichete), nu cod în simbol. Nordul se rotește după nordul real al proiectului.
- **Linia de rupere** (zigzag, două linii, S), cu zona elastică; poate ascunde ce acoperă (o mască, ca `WIPEOUT` din DXF).
- **Simbolurile de domeniu** (electric, sanitar, incendiu, evacuare, accesibilitate, peisagistică): biblioteci de simboluri în pachete de extensii. **Licențele**: simbolurile ISO 7010 și IEC 60617 au drepturi ale organismelor de standardizare. Lapin nu le livrează; utilizatorii le desenează sau le aduc din surse care le permit (§9, întrebarea 5).
- **Legendele**: o *vedere de legendă* care se generează din proiect: simbolurile folosite pe o planșă, tipurile de pereți cu hașura lor, materialele, tipurile de linie, culorile încăperilor (cea existentă devine un caz). Se așază pe planșă ca orice vedere și se derivă la fiecare cerere (nu se stochează), conform principiului.

### 3.5 Simbolul 2D al unui obiect 3D

Un tip de mobilier, echipament MEP, obiect sanitar sau model GLB adus de utilizator își poate alege **reprezentarea în plan**: proiecția (ca acum), silueta simplificată (ca anturajul, `Silhouette`) sau **un simbol din bibliotecă** (de exemplu simbolul românesc al unei prize, al unei căzi, al unui copac). Se aplică per tip și se poate suprascrie per vedere prin nivelul de detaliu. Ce e procedural azi (plantele, ușile după convenție) rămâne implicit.

### 3.6 Tipurile de linie

- **Import `.lin`** (formatul AutoCAD, public): tipurile simple (liniuțe, puncte, goluri); cele complexe cu text și forme se citesc cu textul și formele ca simboluri mici sau se refuză cu un mesaj.
- **Un editor simplu** (liniuță, gol, punct, cu previzualizare la scara peniței) și **„Tipurile mele de linie”**, ca „Hașurile mele”; în proiect pe IfcProject, ca hașurile (decizia 54).
- Straturile, stilurile de obiect, axele și suprascrierile vederii aleg din lista proiectului, nu din cele 4/5 fixe; numele vechi rămân ca tipuri încorporate.
- La DXF, `LTYPE` devine tip de linie al proiectului, nu cel mai apropiat dintre 4.

### 3.7 Standardele de desen ca bibliotecă

`.lapinstandards` (JSON): stilurile de text, cotă și ax, tipurile de etichete, straturile și grupurile lor, tabelul penițelor, stilurile de obiect, tipurile de linie, hașurile, simbolurile sloturilor. **Exportă / Importă** din Gestionare → Standarde de desen, cu alegerile de la `LibraryMerge` (păstrează, actualizează, păstrează-le pe amândouă). Astfel un birou își aduce „standardul firmei” într-un proiect existent, nu doar la crearea lui din șablon. Pachete-exemplu de extensie: „Desen tehnic RO”, „DIN 1356”, „BS 1192 / ISO 13567 (straturi)”. E un fel nou de extensie, `standards`.

## 4. Biblioteca unificată

### 4.1 O singură fereastră a bibliotecilor

Etapa 4b a Catalogului (decizia 263: „o bibliotecă de tipuri gata făcute”) se lărgește: **Catalogul** arată, pe categorii, tot ce se poate aduce în proiect (tipuri, simboluri, mobilier, componente, standarde, tipuri de linie, hașuri, profile, reguli, forme de bare, parametri, clasificări), din **toate sursele**:

```
Încorporate (Lapin)  ·  Pe acest calculator  ·  Folderele firmei  ·  Proiectul  ·  Extensii instalate
```

Fiecare intrare are o miniatură (motorul o desenează deja pentru tipuri, `TypedTool.thumb`), căutare, etichete, licență și sursă, cu „Adaugă în proiect” sau tragere în vedere. Dialogurile existente (Biblioteci de profile, Pachete de tipuri, Cartușe, Hașurile mele, Biblioteca de mobilier) rămân ca editoare și se deschid din Catalog.

### 4.2 Sursele profilelor, pentru orice fel

- **Folderele firmei** (desktop: locale, din rețea sau sincronizate, de citit sau și de scris), azi doar la profile, pentru orice fel de fișier al bibliotecii. E cea mai ieftină formă de „bibliotecă de echipă”, fără server.
- **Copia în proiect + „mai nou în bibliotecă” + Actualizează** (un pas de undo, elementele urmează) pentru tipuri, simboluri, standarde, mobilier.
- **Alegerile la conflict** din decizia 506, peste tot.
- **Componentele și tipurile de apartament exportate** (`.lapincomponent`: membrii, tipurile și materialele lor după valoare, ca `.lapinlib`), ca să treacă dintr-un proiect în altul.

### 4.3 Pachete de mai multe feluri

Fișierele rămân unul pe fel (verificabile separat). Un **`.lapinpack`** (zip cu un manifest și fișierele felurilor) aduce un „kit” într-o singură instalare, de exemplu „Birou românesc: șablon, standarde, cartuș, tipuri de pereți, simboluri, reguli”. Felurile noi de extensie propuse: `symbols`, `lineTypes`, `standards`, `rules`, `barShapes`, `parameters`, `classification`, `tableMapping`, `components`, `models` (GLB cu zonele de întindere, §5.5).

## 5. Configuratoare

### 5.1 Formularele din schemă, peste tot unde se poate

`ParamDef` (porțile, ventilatoarele) se lărgește la **o schemă a configuratorului**: grupuri și file, unități, intervale, `showIf`, valori implicite, ajutor, plus **widgeturi** pentru părțile care nu sunt câmpuri (grila tâmplăriei, editorul de straturi, schița conturului), înregistrate de module. Dialogurile scrise de mână se mută treptat (întâi cele simple: balcoane, finisaje, lift, noduri metalice, lemn), iar cele cu canvas își păstrează canvasul ca widget. Câștigul pentru utilizator: aceeași așezare, căutare în parametri, „resetează la tip”, traduceri dintr-un singur loc, iar un parametru nou adăugat în motor apare fără React nou.

### 5.2 „Salvează în bibliotecă” la fiecare configurator

Peste stilurile proprii de acoperiș și biblioteca de mobilier: **orice configurator** își salvează configurația ca intrare de bibliotecă: stilurile de uși și ferestre, scările, balustradele, balcoanele, sistemele de pereți cortină, lifturile, nodurile metalice, cadrele de lemn, finisajele încăperilor. E deja posibil prin `.lapintypes`; lipsește un buton la locul lucrului și Catalogul din §4.1.

### 5.3 Parametri cu formule (limbaj de expresii, nu cod)

Parametrii utilizatorului (`Params.cs`) primesc **expresii**: `+ − × ÷`, `min`, `max`, `round`, `if`, unități, referințe la alți parametri și la cantitățile elementului (`{Length}`, `{NetArea}`, `{type.Thickness}`). Se evaluează la fiecare cerere (nu se stochează rezultatul), deci apar în etichete, extrase, filtrele `VisibilityGraphics` și deviz. **Etapa a doua**: o expresie conduce un parametru al tipului („lățimea foii = lățimea golului − 2 × tocul”). Geometria condusă de constrângeri rămâne pentru §5.6.

### 5.4 Pachete de reguli (normele ca date)

`.lapinrules` (JSON, mereu `indicative: true` pentru ce livrează Lapin): **regulile scărilor** (pe categorii de clădire: treapta, contratreapta, Blondel, înălțimea liberă, balustrada), **armătura** (diametrele, mărcile oțelului, raza de îndoire după diametru, acoperirile pe clasă de expunere, lungimile de ancoraj și de suprapunere), **șuruburile** (clasele, găurile), **învelitorile** (pantele minime), **accesibilitatea** (rampe, uși), mai târziu **incendiul** și **încărcările** (zăpadă, vânt pe zone, ca date ale utilizatorului).

- Lapin livrează „Romania (indicativ)” și „EN (indicativ)”, reproducând constantele de azi (aceleași rezultate, teste).
- Proiectul alege pachetul (ca standardele profilelor, `profileStandards`); utilizatorul îl duplică și îl editează într-un tabel.
- Configuratoarele **verifică** față de pachet (avertismente în proprietăți și un raport „Verifică proiectul după reguli”), dar nu refuză: utilizatorul decide.

### 5.5 Forme și modele aduse de utilizator

- **Modele GLB/OBJ proprii în mobilier**: registrul (`furniture-assets.json`) e azi făcut la build. Propunerea e să devină și bibliotecă a utilizatorului: modelul importat, zonele de întindere alese într-un dialog (pe x, y, z: ce se întinde, ce rămâne), slot-urile de material, simbolul 2D (§3.5), licența. Același mecanism ajută la echipamentele MEP și sanitare.
- **Forme 2D desenate pentru configuratoare**: conturul capului unei ferestre, desenul unei foi de ușă (panouri, frezări) ca profil sau relief, umplutura unei balustrade ca simbol repetat, profilul nasului treptei și al mâinii curente (bibliotecile de profile o permit deja la unele).

### 5.6 Mai departe: o „familie ușoară” în Studio

Schițele Studio cu **cote legate de parametri** și parametrii tipului ca intrări: un solid parametric al utilizatorului, cu previzualizarea motorului, salvat în bibliotecă. Efortul e mare (un rezolvitor de constrângeri 2D); îl propun după ce §5.3 există, ca formulele să fie aceleași.

## 6. Armătura

### 6.1 Polilinie → bară (2D)

Unealta **Convertește în bară** (o țintă a comenzii din §7.1): poliliniile, liniile și arcele de desen alese într-o secțiune sau într-un plan devin bare. Utilizatorul alege diametrul, marca oțelului, cârligele și partea pe care se aplică acoperirea (linia ca axă, ca față interioară sau exterioară). Lapin **recunoaște forma** când se potrivește cu una din catalog (L, U, etrier), cu parametrii ei și gripurile ei; altfel rămâne bară poligonală. Liniile care se ating se unesc (ca Drafting → Join). Un pas de undo.

### 6.2 Bare în 3D din schițe și muchii

- O polilinie dintr-un plan de lucru 3D (planurile schițelor Studio) devine bară 3D.
- **Pe muchiile unui element**, decalată cu acoperirea: o grindă, o placă, un perete. Alegi muchiile, iar bara le urmează.
- **Distribuția de-a lungul unui drum** (o polilinie sau o muchie): pasul, numărul, primul și ultimul decalaj.

Acestea sunt aproape de `rebar-pro` (premium, `docs/plan-rebar.md`); granița liber / premium e o întrebare (§9, întrebarea 6).

### 6.3 Formele ca date: catalogul de coduri de formă

`ShapeDef` primește o variantă **declarativă**: segmente cu lungimi date de parametri (A, B, C, D, E) sau de expresii (§5.3), unghiuri, direcții, cârligele cu care se naște, gripurile ca perechi punct–parametru. Motorul interpretează forma; cele 7 forme de azi se rescriu ca date, cu aceleași rezultate. Utilizatorii importă **`.lapinbarshapes`**: codurile BS 8666, ISO 4066, cele ale firmei sau ale țării lor. Lapin livrează doar formele de azi și eventual setul de bază ISO 4066, ca indicativ; dreptul de a livra tabelele standardelor trebuie verificat (§9, întrebarea 5).

### 6.4 Extrasul de armătură după standard

- Coloanele A–E și lungimea de debitare după formula standardului ales, **pictograma formei** desenată de motor în fiecare rând, gruparea după marcă și element.
- **Exportul BVBS** pentru mașinile de fasonat, după specificația publică (de verificat licența și versiunea).
- **Importul unui extras** (CSV): diametru, formă, cote, număr → bare de așezat sau doar cantități în deviz.

### 6.5 Normele armăturii

Diametrele, mărcile, razele de îndoire, acoperirile și numele straturilor (azi românești, în cod) trec în pachetul de reguli (§5.4). Un utilizator din altă țară are atunci `B500B` sau `Grade 60`, diametrele lui și straturile lui, fără cod.

## 7. Date tabelare și conversii

### 7.1 Importul de tabele comun

O singură piesă: în motor, `Tables.cs` + `XlsxReader` + ghicirea coloanelor din titluri (azi `PriceLists.Guess`, `ProfileTables`, cu unitățile și limbile lor); în interfață, un `TableImport` comun (din cel al profilelor și al listelor de prețuri). Pașii:

1. **Fișierul**: CSV (separatorul și virgula zecimală ghicite), XLSX (foaia aleasă), lipire din clipboard (din Excel).
2. **Potrivirea**: rândul de titlu, rolul fiecărei coloane (ghicit în engleză, română, germană, franceză…), unitatea ei (din titlu sau aleasă), **cheia** cu care se găsește elementul (GlobalId, Mark, Nume, Tip + Nivel).
3. **Previzualizarea ca diferență**: ce se creează, ce se schimbă (valoarea veche → nouă), ce nu se găsește, erorile pe rânduri. Nimic nu se aplică până la OK.
4. **Aplicarea**: un pas de undo. **Potrivirea se ține minte** pentru aceeași așezare a coloanelor (ca la profile) și se poate exporta ca **`.lapinmapping`**, ca un birou să-și distribuie „formatul nostru de încărcări”.

Modulele își înregistrează **ținte** (`addTableImporter`, ca `addContentInstaller`), fiecare cu rolurile ei de coloană, cheile, validarea și un **șablon CSV** descărcabil.

### 7.2 Țintele propuse

| Țintă | Coloane (exemple) | Modul |
|---|---|---|
| **Cazuri și combinații de încărcări** | nume, fel (permanentă, utilă, zăpadă, vânt, seism), coeficienți ψ / γ, combinația | analysis |
| **Încărcări punctuale, liniare, pe suprafață** | caz, fel, coordonate sau GlobalId / Mark-ul elementului, valoare cu unitate (kN, kN/m, kN/m²), direcție, excentricitate | analysis |
| **Încărcări utile pe funcțiunea încăperii** | funcțiune / categorie (EN 1991-1-1 A–E), qk, Qk | analysis (peste încărcările derivate din încăperi) |
| **Valori de proprietăți pe elemente** | cheie + oricâte coloane = proprietăți (set, nume, tip IFC) | core |
| **Dus-întors cu extrasele** | exportul XLSX al unui extras scrie GlobalId-ul ascuns; reimportul îl citește ca diferență | core (peste `XlsxWriter`) |
| **Încăperi** | număr, nume, funcțiune, finisaj, suprafața cerută (program de arii, comparat cu cea desenată) | architecture |
| **Niveluri și axe** | nume, cotă / poziție, unghi | core |
| **Goluri / tâmplărie din tabel** | perete (Mark), tip, poziție, parapet | architecture |
| **Sarcinile planificării** | WBS, nume, durată, predecesoare, elementele (după cheie) | planning |
| **Materiale** | nume, categorie, densitate, λ, culoare, hașură, preț | core |
| **Clasificări și chei de notă** | cod, titlu, părintele | core (§7.3) |
| **Extrase de armătură** | §6.4 | rebar |
| **Liste de echipamente MEP** | tip, putere, debit, spațiul | mep |
| **Puncte topo** | există deja (`survey.ts`); se mută pe piesa comună | civil |

### 7.3 Clasificări, chei de notă, parametri comuni

- **Clasificările** (golul cel mai clar): `IfcClassification` + `IfcClassificationReference` pe elemente și pe tipuri, importate din tabel (cod, titlu, părinte) ca arbore. Se aleg în proprietăți, se pun în bloc după filtre, se arată în etichete (`{classification.code}`) și extrase, se exportă în IFC. Sistemul îl aduce utilizatorul: Uniclass, OmniClass, CCI, ce cere țara lui (licența fiecăruia e a lui; Lapin nu le livrează).
- **Cheile de notă (keynotes)**: un tabel cod → text (din fișier), o etichetă `TagType` care arată codul și o **legendă a cheilor** generată (§3.4).
- **Parametrii comuni** (`.lapinparams`): definițiile (nume, fel, unitate, set de proprietăți, clasele la care se aplică, un id stabil), aduse în orice proiect cu aceleași nume, ca extrasele și etichetele să meargă din proiect în proiect. E un fel de extensie, `parameters`.

### 7.4 „Convertește desenul în…”

O singură comandă, cu ținte înregistrate de module (`addLineworkConverter`), pe selecția de desen sau pe un strat DXF:

| Din | În | Opțiuni |
|---|---|---|
| linii / polilinii | **pereți** | tipul, linia ca axă / față stângă / dreaptă, nivelul, înălțimea |
| polilinie închisă | **placă**, acoperiș, tavan, încăpere, gol în placă | tipul, cota |
| linii | **grinzi**, fundații continue, ziduri de sprijin | tipul / profilul, cota |
| puncte, cercuri, dreptunghiuri, blocuri | **stâlpi**, fundații izolate | tipul după mărime sau după numele blocului |
| polilinie | **balustradă**, gard, bordură, traseu MEP | tipul |
| polilinii | **bare de armătură** | §6.1 |
| linii (existent) | drum, linie de rupere, bară metalică, profil, perete Studio | ca acum, mutate sub aceeași comandă |
| orice | **simbol** | §3.2 |

Previzualizare, apoi un pas de undo. Elementele sunt native, create la cererea utilizatorului, deci nu încalcă regula recunoașterii IFC (aceea privește importul unui IFC străin).

### 7.5 Straturile DXF → elemente

La importul DXF, un **tabel de potrivire**: stratul (sau numele blocului) → ținta din §7.4, cu tipul. De exemplu `A-WALL` → pereți „Zidărie 25”, `S-COLS` → stâlpi după mărimea dreptunghiului, blocul `USA_90` → ușa 90. Tabelul se ține minte și se exportă (`.lapinmapping`). Ce nu e potrivit rămâne desen, ca acum. Reface fluxul obișnuit „planul din AutoCAD devine model” fără desenare din nou.

## 8. Prioritizarea propusă

Efortul e estimat ca în celelalte planuri (săptămâni de lucru).

| Ordine | Ce | Valoare | Efort | De ce acum |
|---|---|---|---|---|
| **1** | Simbolul 2D: definiția, Salvează ca simbol, editorul, instanțele în vederi și pe planșe, DXF BLOCK în ambele sensuri (§3.1, §3.2) | foarte mare | ~2 | cerut explicit; refolosește randatorul cartușelor |
| **2** | Sloturile: secțiune, fațadă, nivel, ax, cotă, titlul vederii; nordul, scara grafică, linia de rupere, cota de nivel (§3.3, §3.4) | foarte mare | ~2 | marcajele de azi devin simboluri încorporate, cu aceeași ieșire |
| **3** | Importul de tabele comun + încărcările + valorile proprietăților + dus-întorsul extraselor (§7.1, §7.2) | mare | ~2 | încărcările din CSV, cerute explicit; deblochează restul țintelor |
| **4** | Convertește desenul în… + straturile DXF → elemente + polilinie → bară (§7.4, §7.5, §6.1) | mare | ~2 | „din AutoCAD în model”; armătura cerută explicit |
| **5** | Biblioteca unificată: Catalogul cu sursele, folderele firmei pentru orice fel, export de componente (§4) | mare | ~1,5 | după ce există simbolurile, are ce aduna |
| **6** | Tipurile de linie, standardele de desen ca bibliotecă (§3.6, §3.7) | medie | ~1 | completează simbolurile pentru alte țări |
| **7** | Pachetele de reguli (scări, armătură, șuruburi, învelitori) (§5.4, §6.5) | medie–mare | ~1,5 | scoate normele românești din cod |
| **8** | Clasificările, cheile de notă, parametrii comuni (§7.3) | medie–mare | ~1,5 | golul IFC cel mai clar pentru alte industrii |
| **9** | Formulele parametrilor (§5.3), formularele din schemă (§5.1), Salvează în bibliotecă (§5.2) | medie | ~2 | baza familiei ușoare |
| **10** | Formele de bare ca date, extrasul după standard, BVBS, barele 3D (§6.2–§6.4) | medie | ~2 | parțial premium (§9, întrebarea 6) |
| **11** | Modelele GLB ale utilizatorului (§5.5), simbolul 2D al obiectelor (§3.5) | medie | ~1 | |
| **12** | Familia ușoară în Studio (§5.6) | mare | ~4+ | după formule |

Fiecare etapă vine cu testele ei, pagina din ghid în engleză și română, scena ei în `scripts/user-docs/scenes.mjs`, șirurile în toate limbile din `ui/src/i18n`, comenzile în catalog (deci și în Python și MCP) și felul de extensie (validatorul din `lapin-extensions`).

## 9. Întrebări pentru utilizator

**Răspunsurile utilizatorului (12.11.2026): toate recomandările**, cu o schimbare: **armătura din această serie (§6, inclusiv barele 3D, distribuțiile și BVBS) intră toată în modulul deschis `rebar`**. Și o regulă permanentă: *orice se poate da ușor în mâna utilizatorului să configureze — prin desen, prin formule sau text dinamic — se face și așa, pe lângă metoda codată în aplicație; extindem, nu stricăm ce există* (ce e codat rămâne varianta încorporată, cu aceeași ieșire). Tot atunci: **marcajele salvate se pot întoarce pe ambele direcții** (stânga ↔ dreapta, sus ↔ jos), **iar textele lor rămân mereu citibile de la stânga la dreapta sau de jos în sus**.


1. **Simbolurile marcajelor: pe proiect sau pe vedere?** Propun: alese în Standardele de desen ale proiectului, suprascrise pe vedere (ca `markSize`). Alternativa e doar pe proiect.
2. **Simbolurile în IFC**: `IfcAnnotation` cu reprezentarea 2D și setul `Lapin_Symbol` (propunerea), sau nu le exportăm în IFC deloc (rămân doar în desene, DXF și PDF)?
3. **Formulele**: `plan-extensions.md` spune „nicio formulă evaluată” în pachetele de conținut. Acceptăm un **limbaj de expresii declarativ** (fără cod, evaluat de motor, cu limite de timp și adâncime) în parametri, forme de bare și reguli? Fără el, §5.3 și §6.3 rămân mai sărace.
4. **Biblioteca de echipă**: ajung folderele firmei (fără server), sau biblioteca comună e o funcție a platformei Lapin Pro (planul privat)? Propun: folderele firmei în deschis, sincronizarea cloud în Lapin Pro.
5. **Standardele cu drepturi** (ISO 7010, IEC 60617, BS 8666, Uniclass, OmniClass): propun ca Lapin să nu livreze niciunul și să ofere doar importul, plus exemple proprii marcate indicativ. De acord?
6. **Granița liber / premium la armătură**: polilinie → bară 2D și formele ca date în `rebar` (deschis); barele 3D pe muchii, distribuțiile și BVBS în `rebar-pro`? Sau altfel?
7. **Încărcările**: doar import din tabel (§7.2), sau și **citirea înapoi a unui IFC SAV** (azi exportul e într-un singur sens)?
8. **Ordinea din §8**: o păstrăm, sau încep cu altceva (de exemplu importul încărcărilor înaintea simbolurilor)?

## 10. Ce s-a făcut

- **Etapa 1, simbolul 2D (motorul):** `Lapin.Platform/Symbols/` (`SymbolDef`, `SymbolRender`, `Symbols`, `BuiltinSymbols`), instanțele în `Lapin.Drafting/Cad/SymbolInstances.cs`. Definiția `LapinSymbol` (elementele cartușelor plus cercuri și arce, în mm, inserția în origine, scara `annotation` sau `model`, atributele), 12 simboluri încorporate ca date (nord, callout, cap de secțiune, marcaj de fațadă, revizie, linie de rupere, cotă de nivel, capete de nivel, buline de ax), instanțele `IfcAnnotation SYMBOL` pe vederi și pe planșe (`drafting.placeSymbol`, `setSymbol`, `flipSymbol` pe x sau y, `explodeSymbol`), **Salvează ca simbol** (`drafting.toSymbol`: liniile, poliliniile, cercurile, arcele, hașurile și textele unei vederi, un `{attr.x}` devine atribut), mutate, rotite, oglindite și scalate cu `drafting.transform`; textele întoarse rămân citibile (`SymbolRender.LayText`). Fișierul `.lapinsymbols` (`symbols.export`, `readPack`, `importFrom` cu alegerile păstrează / actualizează / amândouă / sari). **Formulele** (`Expressions.cs`, `Expr`, `LiveText`): `{= attr.w * 2 | 0.00}` în orice text viu, un limbaj declarativ (numere, operatori, `min max round if clamp sin…`, nume), mărginit, niciun cod. Teste `SymbolTests`.
- **Etapa 2, sloturile (motorul):** `SymbolSlots.cs`: capul secțiunii, marcajul fațadei, capul de nivel, bulina axului, nordul planurilor de situație, capul callout-ului, titlul vederii pe planșă; simbolul ales pe marcaj (`symbolSlots` pe secțiune, fațadă, nivel, rețea, inset, vedere) sau pe proiect (`symbols.setSlot`, `symbols.slots`); niciunul ales: desenul de până acum, neschimbat. Jetoanele marcajului (`{mark.name}`, `{mark.number}` — locul vederii pe planșă —, `{mark.sheet}`, `{mark.scale}`, `{level.*}`, `{grid.label}`); simbolurile încorporate cu atributele `number`, `sheet`, `name` le citesc. Cota de nivel citită din model (`{point.elevation}`, `{point.z}`: înălțimea punctului în secțiuni și fațade, nivelul în plan). Teste `SymbolSlotTests`.
- **Etapa 3, importul de tabele (motorul):** `Lapin.Platform/TableImport/` (`TableImport`, `TableUnits`, `PropertyTable`) și `Lapin.Analysis/AnalysisTables.cs`. Țintele sunt servicii `TableTarget` ale modulelor; `tables.targets`, `tables.read` (rândurile, rândul de titlu, coloanele ghicite), `tables.preview` (pașii în cuvinte sau eroarea, rând cu rând), `tables.template`, `tables.import` (un pas de undo, prin comenzile modulelor; rândurile greșite refuzate sau lăsate afară cu `skipErrors`). Numerele cu virgulă sau punct și mii grupate; unitățile din titlu sau de pe rând (m, cm, mm, ft, in; kN, N, daN, kgf, tf, kip; pe metru; pe m², kPa, psf…). Ținte: **încărcările** (punct, linie, contur; cazul după cod sau nume, făcut dacă lipsește; nivelul după nume sau ales pentru tabel; o valoare negativă e în sens opus), **cazurile de încărcare**, **valorile proprietăților** (elementul după GlobalId, marcă sau nume; `Pset_….Nume`, `Set.Nume`, parametrii proprii; o coloană Name redenumește). Teste `TableImportTests`.
- **Interfața etapelor 1–4:** Gestionare → Simboluri… (bibliotecile încorporată, a proiectului și a calculatorului; import cu alegeri, export `.lapinsymbols`), editorul de simboluri pe designerul cartușelor (cercuri, arce, atribute, text viu și formule, previzualizare întoarsă pe x și pe y), Salvează ca simbol… din desen, unealta Simbol (R rotește, X și Y întorc), Proprietățile instanței (întoarcere, atribute, strat, explodare), simboluri pe planșe; simbolurile marcajelor în Standardele de desen și în Proprietăți; Import tabel… (încărcări, cazuri, proprietăți, clasificări; coloanele ghicite, unitățile, previzualizarea pe rânduri, potrivirile ținute minte și salvate ca `.lapinmapping`); Convertește desenul în… și Convertește straturile…; blocurile DXF păstrate ca simboluri. Ghidul: secțiunea Simboluri din pagina Desen (engleză și română), scena `symbols`.
- **Etapa 4, conversia desenului (motorul):** `LineworkConversion` (Platform), convertoarele modulelor (`Lapin.Architecture/LineworkConverters.cs`: pereți, plăci, încăperi, balustrade; `Lapin.Structure/LineworkConverters.cs`: grinzi, fundații continue și izolate, stâlpi rotiți după latura cea mai lungă; `Lapin.Rebar/RebarLinework.cs`: bare cu forma recunoscută — dreaptă, L, U, etrier — altfel poligonală), comanda `drafting.convert` (pe forme alese sau pe o hartă de straturi, păstrând atunci desenul ca fundal), `drafting.convertPreview`, `drafting.converters`; importul DXF cu `blocks: "symbols"` (fiecare bloc un simbol la scara modelului, fiecare inserție o instanță rotită și întoarsă ca în fișier). Teste `LineworkConvertTests`, `DxfBlockSymbolTests`.
- **Etapa 6, tipurile de linie și standardele ca bibliotecă:** `drafting.lineTypes` în standardele proiectului; `.lin` citit (`Lapin.Drawing/Import/LinFile.cs`: liniuțe, puncte, goluri, liniuțele tipurilor complexe, inch) și scris; numite de straturile de desen, straturile standardelor, stilurile de ax, stilurile de obiect și liniile editate; redenumite și șterse peste tot (`standards.setLineType`, `removeLineType`, `importLin`, `exportLin`, `lineTypes`). Fișierul `.lapinstandards` (`standards.export`, `readPack`, `importFrom`): stilurile, tipurile de etichete, straturile, tipurile de linie, hașurile, simbolurile, simbolurile marcajelor și, cu `options`, unitățile, scările, mărimile marcajelor, stilurile de obiect și tabelul penițelor, intrare cu intrare cu alegerile actualizează / amândouă / sari. Teste `StandardsLibraryTests`.
- **Etapa 7, pachetele de reguli:** `RulePacks` (Platform) cu secțiunile modulelor (`IRuleSection`) și verificarea (`IRuleCheck`, `rules.check`); pachetul Lapin, „Romania (indicative)”, ține valorile de până acum ale codului; copiile proiectului (`rules.create`, `update`, `setActive`, `export` / `import` `.lapinrules`, mereu indicative). Scările (`architecture.stairRules` pentru un tip nou, `architecture.applyStairRules`, verificarea tipurilor), armătura (mărcile și diametrele oferite, marca implicită, dornul și cozile cârligelor luate de barele noi; `rebar.rules`), acoperișurile (panta minimă a învelitorilor). Nimic nu e refuzat din cauza regulilor: se propune și se avertizează. Șuruburile rămân pe lista de clase de până acum (de făcut). Teste `RulePackTests`.
- **Etapa 8, clasificările:** `Classifications.cs`: sistemele aduse din tabel (ținta `core.classification`), codurile pe elemente și tipuri (`classification.assign`, tabelul `core.classify`), `{classification.code}`, `{classification.title}`, `{classification.<Sistem>}` în etichete (cheile de notă), exportul IFC (`IfcClassification`, `IfcClassificationReference`, `IfcRelAssociatesClassification`; citirea înapoi la import rămâne de făcut); parametrii comuni (`ParamDefs`, `.lapinparams`) care dau tipul și setul unui parametru. Teste `ClassificationTests`.
- **Etapa 9, formulele parametrilor:** `ParamFormulas.cs`: un parametru cu `formula` în loc de valoare, verificată la scriere, calculată la fiecare cerere din parametrii elementului și ai tipului, numerele lui și (prin interogare) cantitățile lui; etichetele și extrasele de tipuri o citesc (`params.evaluate`, `params.tryFormula`). Formularele din schemă (§5.1) și „Salvează în bibliotecă” la fiecare configurator (§5.2) rămân de făcut, în interfață. Teste `ParamFormulaTests`.
- **Etapa 10, armătura ca date (în modulul deschis `rebar`):** `RebarShapeData.cs`: codurile de formă ale utilizatorului (parametri, segmente cu lungimea și întoarcerea date de parametri sau formule, cârlige, strat, diametru), `.lapinbarshapes`, bara care își ține definiția sub un cod după conținut și urmează schimbarea formei; `RebarBvbs.cs`: extrasul după standard (`rebar.bendingSchedule`: codul și cotele A, B, C…) și exportul BVBS (`rebar.exportBvbs`: înregistrări BF2D după ghidul public BVBS 3.1, suma de control verificată pe exemplul ghidului). Barele 3D din desen existau (bara stă pe planul vederii, așezată în gazdă după acoperire), deci poliliniile convertite dau deja bare 3D; barele pe muchiile unui element rămân de făcut. Teste `RebarShapeDataTests`.
- **Etapa 11, modelele utilizatorului și simbolul 2D al obiectelor:** `Lapin.Furniture/UserModels.cs`: un GLB adus cu licența și sursa lui (`furniture.importModel`, `updateModel`, `deleteModel`, `models`), extinderea și materialele citite din fișier, zonele de întindere, rolurile materialelor; id-ul după conținut, purtat de biblioteca de mobilier cu fișierul înăuntru. `planSymbol` pe un tip de mobilier: planul lui e simbolul ales, nu corpul văzut de sus. Teste `UserModelTests`.
- **Etapa 5 (biblioteca unificată: Catalogul cu toate sursele, folderele firmei pentru orice fel) și etapa 12 (familia ușoară în Studio)** nu sunt făcute încă; nici felurile noi de extensie din magazin (`symbols`, `lineTypes`, `standards`, `rules`, `barShapes`, `parameters`, `models`).
