# Plan: conectorul Speckle

Hotărârea utilizatorului (23.11.2026, decizia 807): Lapin primește un conector Speckle construit pe **SDK-ul .NET oficial al Speckle** (`Speckle.Sdk`, `Speckle.Objects`), ca excepție notată de la principiul „formatele de schimb scrise după specificația publică, fără dependențe”. Speckle e un protocol de server (GraphQL plus încărcarea obiectelor), nu un format de fișier, iar reproducerea serializării și a identificatorilor lui ar fi fragilă. Modulul e **deschis (MIT)**: aduce utilizatori, nu e premium.

## De ce

- Din Speckle, un model Lapin ajunge în Revit, Rhino/Grasshopper, Power BI, Blender și în viewer-ul web Speckle, fără IFC între ele.
- Nu se bate cap în cap cu Atrium (`docs/plan-atrium-cde.md`): Atrium e CDE-ul Lapin, cu istoricul ca un depozit git; Speckle e poarta spre programele altora.

## Față de Atrium (decizia 793)

Atrium rămâne CDE-ul lui Lapin; Speckle nu îi ține locul și nu e baza lui.

| | Atrium | Speckle |
|---|---|---|
| Ce ține | părțile native ale `.lapin` (tabelele Arrow, intenția), deci un pull dă înapoi proiectul editabil | obiecte convertite (suprafețe și proprietăți), deci întoarcerea în Lapin e ca referințe |
| Istoria | commit-uri cu părinți, ramuri, îmbinare pe părți și pe entități (S4), rezervări | versiuni liniare pe model, fără îmbinare |
| Fluxul ISO 19650 | containere, stări și revizii, emiteri, BCF 3.0 și OpenCDE (Atrium Server le are deja) | comentarii proprii, fără fluxul de emitere |
| Serverul | .NET și un fișier SQLite, ușor de autogăzduit | Postgres, Redis, stocare de fișiere și mai multe servicii |
| Premium | al nostru (echipe, permisiuni, reguli, SharePoint) | funcțiile de echipă ale Speckle sunt închise și ale lor |

Rolurile: **Atrium e locul unde stă proiectul** (istoria, îmbinarea, aprobările); **Speckle e un canal de publicare** spre programele altora, ca IFC-ul, dar viu. Mai târziu, o emitere din Atrium poate publica singură o versiune în Speckle (un cârlig după emitere). Nu găzduim un Speckle al nostru pentru utilizatori; conectorul merge cu serverul public și cu orice server autogăzduit al unei firme.

## Licențele (verificate pe 23.11.2026, `Speckle.Sdk` și `Speckle.Objects` 2026.9.0, .NET 10)

| Pachet | Licența |
|---|---|
| Speckle.Sdk, Speckle.Objects, Speckle.Sdk.Dependencies, Speckle.Sdk.Parquet | Apache-2.0 |
| GraphQL.Client (+ Abstractions, Websocket, Primitives) | MIT |
| Microsoft.Data.Sqlite, Microsoft.Extensions.* | MIT |
| SQLitePCLRaw (bundle, core, lib.e_sqlite3, provider) | Apache-2.0; SQLite însuși e domeniu public |
| Speckle.Newtonsoft.Json | MIT |
| System.Reactive | MIT |
| **Speckle.DoubleNumerics 4.1.0** | **nespecificată în pachet**: e o bifurcație a `Weingartner/System.Numerics.DoubleVectors`, derivată din System.Numerics al .NET (MIT). De confirmat din fișierul LICENSE al depozitului lor și trecută în excepțiile verificate ale `check:licenses` cu textul ei, înainte de a intra în vreo construcție |

`Speckle.Sdk.Dependencies` conține în el Polly (BSD-3-Clause) și bucăți din ASP.NET Core (MIT); de pus în notele de licență ale aplicației.

**De știut:** `lib.e_sqlite3` e o bibliotecă nativă pe platformă (macOS, Windows, Linux). Trebuie verificat că ajunge în pachetul Tauri pe toate trei, inclusiv în construcția Windows făcută pe Mac (`--target windows`). În browser (motorul în WebAssembly) conectorul nu merge la început: doar desktop.

## Etapele

### S1. Trimiterea (Lapin → Speckle), ~1 săptămână

- Modulul `speckle` (deschis), cu comenzile `speckle.send` și interogările `speckle.projects` / `speckle.models`.
- **Contul:** serverul (implicit `app.speckle.systems`, sau unul autogăzduit) și un token personal, introdus în aplicație și ținut în spațiul securizat al sistemului (Keychain pe macOS, Credential Manager pe Windows, Secret Service pe Linux), niciodată în proiect, în setări sau în rapoarte. Autentificarea OAuth a Speckle, mai târziu.
- **Ce trimite:** un arbore Proiect → Clădire → Etaj → Categorie; fiecare element e un obiect cu suprafețele lui (`displayValue`), din aceeași scenă ca exportul GLB, deci tot prin `VisibilityGraphics`; materialul de randare (culoare, transparență); proprietățile: clasa IFC, tipul, etajul, faza, materialele straturilor, seturile de proprietăți și cantitățile. **GlobalId-ul e `applicationId`**, deci o trimitere nouă actualizează aceleași obiecte. Unitățile în metri.
- **Interfața:** Fișier → Speckle → Trimite…: serverul, proiectul și modelul alese (sau unul nou), mesajul versiunii, ce se trimite (tot modelul, vederea curentă, selecția). Progresul și legătura spre versiunea din Speckle.
- **Teste:** conversia unui model mic în obiectele Speckle (fără rețea), applicationId-urile stabile după o reconstruire; un test de integrare cu un server Speckle local (container) doar în dezvoltare.

**Stare S1 (23.11.2026, ramura `feat/speckle-s1`):** făcut.

- **Modulul** `speckle` în `engine/src/Modules/Exchange/Lapin.Speckle` (un grup nou, „Exchange”), cu jumătatea de interfață în `ui/src/modules/speckle`, în ediția `lapin`. Interogările `speckle.accounts`, `speckle.setAccount`, `speckle.forgetAccount`, `speckle.projects`, `speckle.models`, `speckle.preview`, `speckle.send`, `speckle.openVersion`; toate sunt interogări (trimiterea nu schimbă proiectul, deci nu e un pas de undo) și toate sunt doar ale gazdei (`EngineDispatcher.HostOnlyQueries`: contul e al ei). Progresul prin `IQueryContext.Progress` (fereastra de lucru, cu Renunță prin `cancel.long`).
- **Doar desktop:** `"desktopOnly": true` în `module.json` (motor și interfață). `scripts/product.mjs` îl lasă afară, cu tot ce depinde de el, când produsul e rezolvat pentru browser (`{ target: 'web' }`, folosit de `build-web.mjs`), deci SDK-ul nu intră în motorul WebAssembly. Pe desktop, `Directory.Build.targets` copiază și bibliotecile native ale modulului (`runtimes/<rid>/native`, SQLite-ul SDK-ului), `ModuleLoader` le găsește acolo, iar `build-product.mjs` păstrează doar platforma pentru care construiește și le semnează pe macOS.
- **Tokenul** stă în spațiul securizat al sistemului, prin shell-ul Tauri (`secret_read`/`secret_write`/`secret_delete`, crate-ul `keyring` 3.6, MIT/Apache-2.0; pe Linux prin zbus, fără libdbus), doar sub chei `speckle:<server>`. Interfața îl citește când se deschide dialogul și îl dă motorului, care îl ține în memorie cât rulează (`speckle.setAccount`). Nu ajunge în proiect, în `settings.json` (acolo stau doar serverul, ultimul proiect și ultimul model), în rapoarte sau în jurnale. SDK-ul ține un cache al obiectelor trimise (`~/Library/Application Support/Speckle/Projects/<id>.db` pe macOS), fără cont și fără token.
- **Conversia** (`SpeckleConvert`, fără rețea): `RootCollection` (proiectul) → clădirea → etajele → câte o colecție pe clasa IFC, elementele sitului și golurile lângă clădire. Fiecare element e un `DataObject` (modelul v3 al Speckle) cu `applicationId` = GlobalId, `displayValue` = mesh-urile scenei exportului GLB (`MeshScene`, deci prin `VisibilityGraphics`), câte unul pe material, în metri, și proprietățile: clasa IFC, numele, tipul, etajul, fazele, atributele, materialele, straturile (material, grosime, miez), seturile de proprietăți și cantitățile cu unitățile lor (cele adunate de exportul Fragments). Materialele ca `RenderMaterialProxy` (culoare, opacitate, rugozitate, metalicitate), etajele ca `LevelProxy`. `MeshScene.Build` și `FragContext.Of` au primit o variantă peste `IQueryContext`, ca un modul să le poată folosi.
- **Licențele:** `Speckle.DoubleNumerics` 4.1.0 e MIT (fișierul LICENSE al `specklesystems/Speckle.DoubleNumerics`, bifurcația lui Weingartner, commit-ul pachetului fiind pe ramura lui), trecut în excepțiile verificate ale `check-licenses.mjs` pe versiune și commit. `SQLitePCLRaw` luat la 2.1.14 (2.1.11, adus de SDK, are o vulnerabilitate cunoscută). Notele în `Lapin.Speckle/NOTICE.md`, copiat lângă modul.
- **Verificat** cu serverul local (2.31.14): testele xUnit (`SpeckleTests`, cel de integrare doar cu `LAPIN_SPECKLE_TEST_URL` și `LAPIN_SPECKLE_TEST_TOKEN`), apoi din aplicație (`pnpm dev`): testLapin.lapin și o casă cu uși, ferestre, placă și acoperiș, trimise de două ori, cu aceleași applicationId-uri în ambele versiuni; casa se vede în viewer-ul Speckle cu culorile materialelor.

**Rămas din S1:**
- SDK-ul 2026.9 marchează `Send2` și `Version.Create` ca vechi pentru serverele de la 2026.9 încolo (versiunea se naște după ce serverul „ingerează” obiectele) și propune `Send3` cu pachete Parquet (`BundleBuilder`) sau `SendPipeline` + `client.Ingestion`. Serverul local 2.31.14 nu le are; trecerea, cu `Send2` ca rezervă, după o încercare pe un server nou (app.speckle.systems).
- Instanțele (același scaun de mai multe ori) pleacă fiecare cu mesh-urile lui: scena exportului ține triunghiurile fiecărui element la locul lui. `InstanceProxy`/`InstanceDefinitionProxy` cer o scenă cu definiții.
- Texturile și muchiile nu se trimit.
- Neîncercat încă: Windows (Credential Manager, `e_sqlite3.dll` în construcția făcută pe Mac) și Linux (Secret Service) din aplicația construită; semnarea și notarizarea cu `libe_sqlite3.dylib` în pachet.
- Ghidul are secțiunea în engleză și română; celelalte limbi ale ghidului (es, de, fr, da, it, pl, pt) o iau când se traduc paginile lor. Scena `speckle` arată conectarea: ghidul nu are un server Speckle.

### S2. Primirea (Speckle → Lapin), ~1 săptămână

- Fișier → Speckle → Primește…: o versiune intră ca **referințe** (suprafețe) pe etajele potrivite, grupate după colecțiile ei, cu proprietățile păstrate.
- O primire nouă a aceluiași model **actualizează** referințele după `applicationId`, păstrând GlobalId-urile lor.
- Recunoașterea ca elemente native (pereți, plăci) rămâne după aceeași regulă ca la IFC (`RecognitionFidelity`): ce diferă cu mai mult de 1 mm sau 0,5 % rămâne referință.

**Stare S2 (23.11.2026, ramura `feat/speckle-s2`):** făcută primirea ca referințe; recunoașterea ca elemente native nu.

- **Comenzile:** `speckle.receive` (comandă, un pas de undo; `{projectId, modelId, versionId?, asCopy?, origin?, storeyId?, activeStoreyId?}`) și interogările `speckle.versions` (versiunile unui model, cea mai nouă întâi) și `speckle.survey` (ce ar face primirea: obiecte, instanțe, curbe, ce rămâne afară, nivelurile, câte se adaugă/actualizează/șterg, câte sunt elementele acestui proiect). Toate doar ale gazdei: `speckle.` intră și în `EngineDispatcher.HostOnlyCommands`. Versiunea descărcată de `survey` e ținută de modul pentru `receive` care urmează (una singură).
- **Descărcarea:** `Receive2` (marcat vechi de SDK ca și `Send2`, avertismentul suprimat): citește tot ce are un server 2.x și, pe serverele noi, pachetele ca arbore „best-effort”; `Receive3` citește doar pachete (Speckle 2026.9+), deci nu ajută pe 2.31. Versiunea e marcată „primită de lapin” pe server (`Version.Received`), dacă o acceptă.
- **Citirea** (`SpeckleRead`, fără rețea și fără model): orice obiect cu geometrie sub colecții (`DataObject` și rudele lui, obiectele tipate ale conectorilor vechi, un `Mesh` sau `Brep` gol, un `InstanceProxy`); mesh-urile din `displayValue` (fețe [n, i…], n = 0/1 la mesh-urile vechi, în evantai) în metri după `units`; culoarea din `renderMaterialProxies` (mesh, apoi obiectul, apoi strămoșii lui), altfel `renderMaterial` al obiectului, altfel `colorProxies`; nivelul din `levelProxies` (sau `level` al obiectului); clasa IFC din proprietăți („IFC class”, `ifcType`), din categoria Revit (Walls → IfcWall…) sau din colecția numită după o clasă (trimiterea Lapin), altfel proxy; niciodată spațiu, gol sau structură spațială. Proprietățile: setul `Speckle` (applicationId, id-ul și tipul Speckle, colecțiile) și câte un set `Speckle · <grup>` cu nivelurile mai adânci în numele câmpului („Instance Parameters › Dimensions › Length (mm)”); un nume nu începe niciodată cu `Pset_`. Instanțele unei definiții (`InstanceDefinitionProxy`) împart un `LapinSharedBody` (transformarea Speckle: vectori coloană, translația pe ultima coloană, citită și transpusă); cele imbricate sunt coapte în definiție, iar instanțele printre alte geometrii, în corpul obiectului. Liniile, poliliniile, arcele (prin trei puncte, `Chords`), cercurile, policurbele și polilinia unei NURBS devin linii de plan (`Plan2d`) când obiectul nu are altceva; punctele, textele, obiectele fără geometrie se numără ca lăsate afară, după tipul Speckle.
- **Aducerea** (`SpeckleReceive.Apply`): fiecare obiect o referință (`StoredBodies`), cu atributul `speckle` {key, server, projectId, modelId, model, versionId, applicationId, objectId, type, path, at}; culorile transparente primesc un material propriu („Speckle Glass 40%”), ca la Fragments. Etajul: după GlobalId-ul nivelului (trimiterea Lapin), apoi după cotă (5 cm, numele la egalitate), apoi etajul pe care stă (cel mai de jos punct), apoi etajul curent; sau toate pe un etaj ales. Coordonatele de la punctul de bază al proiectului (ca la Trimite) sau de la originea internă. Primul receive al unui model face un grup cu numele lui; ce adaugă unul următor intră în el.
- **Actualizarea:** cheia e server|proiect|model (|copy). Potrivirea după applicationId, altfel după id-ul Speckle; GlobalId-ul, grupul și ce îl numește rămân. Un obiect cu același id Speckle (hash-ul lui) și aceeași origine nu se rescrie. Ce lipsește din versiunea nouă **se șterge**, ca la reîncărcarea unei legături (nu ca la reimportul IFC/Fragments, care doar actualizează): referințele oglindesc un singur model Speckle. Un id nou: applicationId-ul dacă e un GlobalId IFC liber (un model trimis de Lapin sau de un conector IFC păstrează id-urile), altfel unul derivat (MD5) din cheia modelului și applicationId, deci același obiect primește același id oricând.
- **Modelele Lapin care se întorc:** dacă applicationId-urile unor obiecte sunt elemente native ale proiectului (nu referințe ale acestui model), primirea e refuzată, iar fereastra oferă **Primește ca o copie**: id-uri derivate, cheie separată (`|copy`), elementele native neatinse; copia se actualizează la rândul ei. Am ales refuzul implicit (nu copia automată) ca să nu apară pe tăcute dubluri suprapuse peste elementele proiectului.
- **Interfața:** Fișier → Speckle → Primește… (contul comun cu Trimite, `SpeckleAccount.tsx`), proiect, model, versiune (cea mai nouă implicit), etaje, origine; progresul și Renunță prin fereastra de lucru; raportul (adăugate, actualizate, neschimbate, șterse, corpuri comune, doar linii, ce a rămas afară).
- **Verificat:** `SpeckleReceiveTests` (fără rețea: etaje, unități, proprietăți, instanțe, actualizare cu GlobalId-urile păstrate, ștergere, un pas de undo, refuz și copie, aceleași id-uri în alt proiect) și testul de integrare cu serverul local (trimis din Lapin, primit în alt proiect, retrimis schimbat, actualizat). În aplicație (`pnpm dev`): un model „din alt program” (mm, niveluri, materiale, 4 scaune ca instanțe, o linie), primit, apoi o versiune nouă (un perete mai lung, unul mutat): 1 adăugat, 1 actualizat, 9 neschimbate, 1 șters, id-urile păstrate; „house 2” primit în alt proiect, văzut în 3D cu culorile lui; un Undo scoate tot.

**Rămas din S2:**
- Recunoașterea referințelor ca elemente native după `RecognitionFidelity` (cum face importul IFC/Fragments cu `MeshRecognition`): nefăcută; ar cere și ca actualizarea să știe de elementele recunoscute.
- Texturile, muchiile, culorile pe vârfuri și materialul pus pe instanță (o definiție comună are culorile ei) nu se primesc. Fețele cu mai mult de trei colțuri se triangulează în evantai (greșit pentru una concavă).
- `Send3`/`SendPipeline`: neîncercat. Cere o a doua conversie, în `BundleBuilder` (containere, obiecte, geometrie, materiale, niveluri), și un server ≥ 3.0.11 (`CanCreateModelIngestion` aruncă pe 2.x) pe care să fie verificată; serverul local e 2.31. De făcut cu un cont pe app.speckle.systems.
- Celelalte limbi ale ghidului iau secțiunea când li se traduc paginile; scena `speckle` rămâne la conectare (ghidul nu are server).

### S3. Mai târziu

- Trimitere automată la salvare (opțională, pe proiect).
- Planșele și desenele ca obiecte 2D.
- Importul unui model Speckle pornit din alte programe direct ca proiect nou.

## Întrebări deschise

1. ~~S1 pornește acum sau după etapa de simboluri MEP?~~ **După** etapa de simboluri MEP (utilizatorul, 23.11.2026).
2. ~~Primul server de test?~~ **Un Speckle local, în container, pentru dezvoltare**; niciun Speckle găzduit de noi; Atrium rămâne CDE-ul, separat (utilizatorul, 23.11.2026).
