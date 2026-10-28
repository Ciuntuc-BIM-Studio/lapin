# Plan: exportul 3D ca mesh (glTF/GLB, Collada, OpenUSD)

Cerut pe 26.10.2026, după întrebarea unui tester despre Collada, SKP și OBJ. Exporturile de acum sunt IFC (modelul), PDF și DXF (desenele). Acestea ar fi exporturi de **suprafețe**, pentru randare, vizualizare, AR și alte programe, nu de model: IFC rămâne formatul care duce elementele, tipurile și intenția.

## Ce există deja

- Motorul are pentru fiecare element părțile lui: solid, material, culoare, nume și cheie (`ElementGeometry.Parts`), iar `Tessellator` le face triunghiuri cu normale și muchii (`TriMesh`). Vederea 3D primește exact asta (`RenderPart`), deci un export vede ce vede ecranul.
- Materialele au culoare, transparență și categorie (`IfcMaterial.Category`). Texturile 3D sunt procedurale, desenate în UI (`textures.ts`, proiecție pe cutie în metri), fără fișiere imagine.
- `Location` dă punctul de bază, punctul de survey și unghiul nordului; `IfcExport` are deja opțiunile de fază și de detaliu, plus filtrarea pe vizibilitate a legăturilor și a vederilor.
- Livrarea fișierelor e comună: pe desktop la calea aleasă, în browser ca descărcare (`Deliver` în `EngineDispatcher`, `exportFile` în `files.ts`).

## Licențe

Regula proiectului: dependențele de producție permisive, verificate de `pnpm check:licenses`. Planul nu adaugă **nicio dependență**: fiecare format e scris de Lapin, după specificația lui publică.

| Format | Specificația | Licența | Ce folosim |
|---|---|---|---|
| glTF 2.0 / GLB | Khronos, publică, fără redevențe | specificația e liberă de implementat | scriitor propriu (JSON + buffer binar) |
| Collada 1.4.1 (.dae) | Khronos, publică | liberă de implementat | scriitor propriu (XML) |
| OpenUSD: USDA, USDZ | AOUSD Core Specification 1.0 (17.12.2025), care definește USDA, USDC și USDZ | specificația e publică | scriitor propriu de USDA, împachetat ca USDZ |
| OBJ + MTL | format Wavefront, documentat public | liber | scriitor propriu, opțional |
| SKP | fără specificație publică | doar SDK-ul proprietar Trimble | **nu**: nu poate intra într-o aplicație MIT |

- **Biblioteca OpenUSD a Pixar** nu intră în aplicație: e C++ nativ, mare, cu licența ei proprie (Apache 2.0 modificată, de verificat în fișierul LICENSE înainte de orice folosire). Scriem formatul text noi; USDC, formatul binar, rămâne pe dinafară.
- **Unelte de verificare doar în dezvoltare**, nu în produs: Khronos glTF-Validator (Apache-2.0), schema XSD Collada 1.4.1, uneltele de conformitate AOUSD și `usdchecker` dacă sunt la îndemână. Dacă vreuna intră în CI, o trecem prin `check:licenses` ca dependență de dezvoltare.
- **Draco** (compresia glTF) nu intră: ar cere biblioteca Google. Fișierele rămân necomprimate, cu buffere binare.

## Arhitectura

Un proiect nou de platformă, `engine/src/Platform/Lapin.Mesh`, managed pur, fără reflecție, ca să meargă și în motorul din browser (AOT, trimming).

1. **Scena** (`MeshScene.Build(session, options)`): ierarhia spațială (proiect, sit, clădire, etaje), apoi elementele vizibile, fiecare cu părțile lui tesselate, materialele folosite (culoare, transparență, categorie) și datele de identitate (GlobalId, clasa IFC, numele, tipul, etajul, faza).
2. **Scriitorii**, unul pe format, toți din aceeași scenă: `GltfWriter` (.gltf + .bin, sau .glb), `ColladaWriter`, `UsdaWriter` (+ `Usdz`), opțional `ObjWriter`.
3. **Protocolul**: `project.exportMesh {format, viewId?, phase?, options}` livrat prin `Deliver`, deci desktop și browser la fel.
4. **UI**: File → Export 3D…, un dialog mic cu formatul și opțiunile, după modelul dialogului de export IFC.

### Decizii de format

- **Unități**: metri peste tot (glTF cere metri; Collada `<unit meter="1">`; USD `metersPerUnit = 1`).
- **Axa în sus**: Lapin e Z în sus. glTF cere Y în sus, deci la scriere se rotește. Collada declară `Z_UP`, iar USD `upAxis = "Z"`, deci rămân neschimbate.
- **Originea**: implicit punctul de bază al proiectului. Opțiune pentru survey (coordonate mari) sau originea internă. Coordonatele mari se pun pe nodul rădăcină, nu în vârfuri, ca precizia float să nu se piardă.
- **Granularitate**: un nod pe element, cu o primitivă sau un sub-mesh pe material. Opțiune „merged by material” pentru motoare de randare care vor puține obiecte.
- **Identitate**: numele nodului e numele elementului. GlobalId-ul, clasa IFC, tipul, etajul și faza merg în `extras` (glTF), `<extra>` (Collada) și `customData` sau atribute `lapin:` (USD).
- **Materiale**: culoarea de bază și transparența. În glTF `pbrMetallicRoughness` cu `baseColorFactor` și `alphaMode BLEND` sub opac. În Collada un `<effect>` Lambert. În USD `UsdPreviewSurface`.
- **Muchii**: opționale. glTF ca primitivă de linii (`mode LINES`) pe nod, Collada ca `<lines>`, USD ca `BasisCurves` liniare. Implicit oprite, pentru că majoritatea programelor nu le folosesc.
- **Ce intră**: ca în exportul IFC (fază, legături ascunse lăsate afară), sau ce arată o vedere 3D aleasă (`viewId`): elementele ascunse, categoriile ascunse în Visibility/Graphics, cutia de secționare dacă va exista.

## Etape

### 1. glTF / GLB (prima, cea mai utilă)

- `MeshScene` cu ierarhia, elementele, materialele, `extras`.
- `GltfWriter`: JSON prin `Utf8JsonWriter`, buffer binar aliniat la 4 octeți, accesori cu `min`/`max` pentru poziții (le cere specificația), indici `UNSIGNED_INT`.
- Variantele `.glb` (un fișier, implicit) și `.gltf` + `.bin`.
- `project.exportMesh`, File → Export 3D…, livrare desktop și browser.
- Teste (`MeshExportTests`): un proiect cu pereți stratificați, ușă cu sticlă, placă și două etaje. Numărul de noduri și primitive, volumul închis al fiecărui mesh egal cu al solidului, rotația Y-sus, transparența sticlei, `extras` cu GlobalId-ul, GLB citit înapoi (antet, chunk-uri, aliniere). Glb-ul trecut prin glTF-Validator, cu zero erori, ca test de dezvoltare.
- Verificare manuală: Blender, viewer-ul Khronos, Windows 3D Viewer.

### 2. Collada 1.4.1

- `ColladaWriter`: `library_effects`, `library_materials`, `library_geometries` (`<triangles>` cu poziții și normale), `library_visual_scenes` cu ierarhia spațială și `<instance_geometry>` pe element. Id-uri sigure în XML, derivate din GlobalId.
- Teste: XML valid față de XSD-ul 1.4.1, același număr de triunghiuri ca în glTF, `Z_UP`, unități.
- Verificare manuală: SketchUp (importă DAE), Blender, Rhino.

### 3. OpenUSD: USDA și USDZ

- `UsdaWriter`: `#usda 1.0`, metadatele stage-ului (`upAxis`, `metersPerUnit`, `defaultPrim`), un `Xform` pe nivel spațial și pe element, `Mesh` cu `points`, `faceVertexCounts`, `faceVertexIndices`, `normals` ca `faceVarying`, materiale `UsdPreviewSurface` legate prin `material:binding`, atributele `lapin:` pe element. Numele prim-urilor sunt identificatori valizi, derivați din nume și GlobalId.
- `Usdz`: zip fără compresie, cu fiecare fișier aliniat la 64 de octeți, cum cere formatul; stratul principal primul.
- Teste: stage-ul citit înapoi de un parser de test (structură, numere, legături de material), alinierea din USDZ, verificare cu uneltele de conformitate AOUSD unde rulează.
- **Risc de verificat întâi**: unele programe, ca AR Quick Look de la Apple, preferă sau cer USDC în USDZ. Dacă un USDZ cu USDA nu se deschide acolo, USDZ rămâne doar pentru programele care citesc USDA (Blender, Omniverse, usdview), iar USDC se discută separat, fiind un format binar mult mai complex.

### 4. Texturi (după primele trei)

- Texturile procedurale din `textures.ts` se portează în motor, desenate în imagini mici (PNG scris cu `System.IO.Compression`, fără bibliotecă). Coordonatele UV vin din aceeași proiecție pe cutie, în metri, ca o cărămidă să aibă aceeași mărime ca pe ecran.
- glTF `baseColorTexture`, Collada `<texture>`, USD `UsdUVTexture`. O opțiune „Textures” în dialog, oprită implicit la început.

### 5. OBJ + MTL (opțional, mic)

- Pentru programele vechi care nu citesc altceva: grupuri pe element, materiale în MTL, Z-sus rotit la Y-sus, fișierele livrate ca zip. Fără ierarhie și fără date de identitate, pe care formatul nu le are.

## Ce nu intră

- **SKP**: drumul către SketchUp e DAE (etapa 2) sau IFC, pe care SketchUp le importă.
- **FBX**: specificația e închisă, iar SDK-ul Autodesk e proprietar.
- **Importul** acestor formate în Lapin: e alt subiect, mesh-urile n-ar deveni elemente native.
- Animații, camere și lumini. Camera unei vederi 3D salvate se poate adăuga ușor în glTF și USD, dacă se cere.

## Stare (26.10.2026)

Hotărât: glTF/GLB întâi, OpenUSD al doilea, Collada după. Pentru restul, variantele din plan: tot modelul implicit (sau ce arată vederea 3D), filtrat pe fază ca IFC-ul; muchiile oprite; originea în punctul de bază.

**Făcut** (etapele 1 și 3), în `engine/src/Platform/Lapin.Mesh`, fără nicio dependență:
- `MeshScene`: proiectul, clădirea, un grup pe etaj (și situl pentru ce nu stă pe un etaj), un element pe element vizibil cu părțile lui unite pe material, materialele (culoare, transparență, categorie), deplasarea rădăcinii la coordonatele reale.
- `GltfWriter`: GLB (implicit) și glTF cu bufferul inclus ca data URI, deci tot un singur fișier. Y în sus, culorile de bază în RGB liniar, `BLEND` unde materialul e transparent, `extras` cu GlobalId, clasa IFC, tipul și etajul; muchiile ca primitivă `LINES`.
- `UsdWriter`: USDA cu `upAxis = "Z"`, `metersPerUnit = 1`, `UsdPreviewSurface`, `MaterialBindingAPI`, `extent` pe fiecare mesh, atributele `lapin:` pe element, nume de prim-uri valide (diacriticele transliterate); USDZ ca zip necomprimat cu datele aliniate la 64 de octeți.
- `project.exportMesh {format: glb|gltf|usdz|usda, viewId?, phase?, edges?, origin: base|internal|survey, path?}`; File → Export 3D….
- (28.10.2026) „Cum o arată vederea”: `hidden?` (id-urile pe care interfața nu le desenează: etajele neafișate după regula din `ui/src/viewport/storeys3d.ts` și ce e ascuns temporar, prin looks) și `planes?` (planele după care taie vederea 3D: tăietura de deasupra etajelor afișate, secțiunea arătată în 3D, cutia de secțiune; `{normal, constant}`, convenția three.js), `caps?` (implicit da). `MeshClip` taie triunghiurile și muchiile, `SectionCaps.Make` închide tăieturile în materialele părților. Regula etajelor rămâne una singură, în interfață, care o folosește și pentru desen; motorul primește rezultatul. Dialogul alege „cum o arată vederea” când vederea lasă ceva deoparte.
- Teste `MeshExportTests` (8): ierarhia și identitatea, o primitivă pe material, volumele egale cu ale solidelor, Y-sus, originea și survey-ul, filtrele (vederea 3D, elementele ascunse), muchiile, glTF cu buffer inclus, USDA (axe, legături de material, numărători), alinierea USDZ, protocolul.

**Verificat cu unelte independente:** Khronos glTF-Validator 2.0.0-dev.3.10, zero erori și zero avertismente pe testLapin.lapin și pe Bloc-P-4.ifc importat (1459 de elemente, 63.508 triunghiuri, GLB de 5,1 MB în 93 ms, USDZ de 7,0 MB în 240 ms); `usdchecker` și `usdchecker --arkit` de pe macOS: „Success” pe USDA și pe USDZ. Riscul din etapa 3 (USDZ cu USDA respins de ARKit) nu s-a confirmat: verificarea ARKit trece. Mesajele „Coding Error … already registered” ale `usdchecker` apar și pe un fișier minimal scris de mână cu mai multe mesh-uri legate, în număr variabil: țin de unealtă, nu de fișier. GLB-ul modelului mare randat în three.js arată clădirea întreagă, dreaptă, cu culorile ei.

**Făcut și Collada** (etapa 2, 26.10.2026): `ColladaWriter`, COLLADA 1.4.1, `Z_UP`, metri, o geometrie pe element cu triunghiurile pe material (efecte Lambert, culoarea așa cum se vede, opacitatea prin `transparent A_ONE`), muchiile ca `lines` în aceeași geometrie, identitatea în `extra` (GlobalId, clasă, tip, numele întreg: numele COLLADA sunt NCName, deci transliterate). Formatul `dae` în `project.exportMesh` și în dialog. Validat cu schema oficială Khronos `collada_schema_1_4_1.xsd` prin `xmllint` (testLapin, cu și fără muchii, și Bloc-P-4: 6,2 MB în 174 ms) și randat cu `ColladaLoader` din three.js, identic cu GLB-ul. Test `MeshExportTests` (9).

**Rămâne:** texturile (etapa 4), OBJ (etapa 5); importul acestor formate, mai târziu, dacă va fi nevoie (analiza: ca referință sau legătură întâi, recunoașterea nativă a prismelor după); deschiderea fișierelor în Blender, Omniverse și AR Quick Look pe un iPhone, de făcut de mână.

## De hotărât (rămas din plan)

1. Ordinea (hotărât: glTF, USD, Collada): glTF, Collada, USD, ca mai sus, sau USD înaintea Collada (dacă AR-ul pe iPhone/iPad contează).
2. Implicit, exportul ia ce arată vederea 3D din față sau tot modelul pe faze, ca IFC-ul.
3. Muchiile implicit oprite.
4. Coordonatele: punctul de bază implicit, survey la cerere.
5. OBJ: făcut sau nu.

Estimare: etapa 1 e cea mai mare, cu scena comună și dialogul. Etapele 2 și 3 refolosesc scena și sunt fiecare mai mici. Textura e o etapă separată, pentru că atinge toate formatele.
