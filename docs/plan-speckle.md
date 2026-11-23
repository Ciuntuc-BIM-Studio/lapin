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

**Stare S2 (23.11.2026, ramura `feat/speckle-s2`):** făcută primirea ca referințe; recunoașterea pereților și a plăcilor ca elemente native făcută apoi (ramura `feat/speckle-s3`, decizia 812, mai jos).

- **Comenzile:** `speckle.receive` (comandă, un pas de undo; `{projectId, modelId, versionId?, asCopy?, origin?, storeyId?, activeStoreyId?}`) și interogările `speckle.versions` (versiunile unui model, cea mai nouă întâi) și `speckle.survey` (ce ar face primirea: obiecte, instanțe, curbe, ce rămâne afară, nivelurile, câte se adaugă/actualizează/șterg, câte sunt elementele acestui proiect). Toate doar ale gazdei: `speckle.` intră și în `EngineDispatcher.HostOnlyCommands`. Versiunea descărcată de `survey` e ținută de modul pentru `receive` care urmează (una singură).
- **Descărcarea:** `Receive2` (marcat vechi de SDK ca și `Send2`, avertismentul suprimat): citește tot ce are un server 2.x și, pe serverele noi, pachetele ca arbore „best-effort”; `Receive3` citește doar pachete (Speckle 2026.9+), deci nu ajută pe 2.31. Versiunea e marcată „primită de lapin” pe server (`Version.Received`), dacă o acceptă.
- **Citirea** (`SpeckleRead`, fără rețea și fără model): orice obiect cu geometrie sub colecții (`DataObject` și rudele lui, obiectele tipate ale conectorilor vechi, un `Mesh` sau `Brep` gol, un `InstanceProxy`); mesh-urile din `displayValue` (fețe [n, i…], n = 0/1 la mesh-urile vechi, în evantai) în metri după `units`; culoarea din `renderMaterialProxies` (mesh, apoi obiectul, apoi strămoșii lui), altfel `renderMaterial` al obiectului, altfel `colorProxies`; nivelul din `levelProxies` (sau `level` al obiectului); clasa IFC din proprietăți („IFC class”, `ifcType`), din categoria Revit (Walls → IfcWall…) sau din colecția numită după o clasă (trimiterea Lapin), altfel proxy; niciodată spațiu, gol sau structură spațială. Proprietățile: setul `Speckle` (applicationId, id-ul și tipul Speckle, colecțiile) și câte un set `Speckle · <grup>` cu nivelurile mai adânci în numele câmpului („Instance Parameters › Dimensions › Length (mm)”); un nume nu începe niciodată cu `Pset_`. Instanțele unei definiții (`InstanceDefinitionProxy`) împart un `LapinSharedBody` (transformarea Speckle: vectori coloană, translația pe ultima coloană, citită și transpusă); cele imbricate sunt coapte în definiție, iar instanțele printre alte geometrii, în corpul obiectului. Liniile, poliliniile, arcele (prin trei puncte, `Chords`), cercurile, policurbele și polilinia unei NURBS devin linii de plan (`Plan2d`) când obiectul nu are altceva; punctele, textele, obiectele fără geometrie se numără ca lăsate afară, după tipul Speckle.
- **Aducerea** (`SpeckleReceive.Apply`): fiecare obiect o referință (`StoredBodies`), cu atributul `speckle` {key, server, projectId, modelId, model, versionId, applicationId, objectId, type, path, at}; culorile transparente primesc un material propriu („Speckle Glass 40%”), ca la Fragments. Etajul: după GlobalId-ul nivelului (trimiterea Lapin), apoi după cotă (5 cm, numele la egalitate), apoi etajul pe care stă (cel mai de jos punct), apoi etajul curent; sau toate pe un etaj ales. (Înlocuit de decizia 813: etajele care lipsesc se fac din nivelurile versiunii, mai jos.) Coordonatele de la punctul de bază al proiectului (ca la Trimite) sau de la originea internă. Primul receive al unui model face un grup cu numele lui; ce adaugă unul următor intră în el.
- **Actualizarea:** cheia e server|proiect|model (|copy). Potrivirea după applicationId, altfel după id-ul Speckle; GlobalId-ul, grupul și ce îl numește rămân. Un obiect cu același id Speckle (hash-ul lui) și aceeași origine nu se rescrie. Ce lipsește din versiunea nouă **se șterge**, ca la reîncărcarea unei legături (nu ca la reimportul IFC/Fragments, care doar actualizează): referințele oglindesc un singur model Speckle. Un id nou: applicationId-ul dacă e un GlobalId IFC liber (un model trimis de Lapin sau de un conector IFC păstrează id-urile), altfel unul derivat (MD5) din cheia modelului și applicationId, deci același obiect primește același id oricând.
- **Modelele Lapin care se întorc:** dacă applicationId-urile unor obiecte sunt elemente native ale proiectului (nu referințe ale acestui model), primirea e refuzată, iar fereastra oferă **Primește ca o copie**: id-uri derivate, cheie separată (`|copy`), elementele native neatinse; copia se actualizează la rândul ei. Am ales refuzul implicit (nu copia automată) ca să nu apară pe tăcute dubluri suprapuse peste elementele proiectului.
- **Interfața:** Fișier → Speckle → Primește… (contul comun cu Trimite, `SpeckleAccount.tsx`), proiect, model, versiune (cea mai nouă implicit), etaje, origine; progresul și Renunță prin fereastra de lucru; raportul (adăugate, actualizate, neschimbate, șterse, corpuri comune, doar linii, ce a rămas afară).
- **Verificat:** `SpeckleReceiveTests` (fără rețea: etaje, unități, proprietăți, instanțe, actualizare cu GlobalId-urile păstrate, ștergere, un pas de undo, refuz și copie, aceleași id-uri în alt proiect) și testul de integrare cu serverul local (trimis din Lapin, primit în alt proiect, retrimis schimbat, actualizat). În aplicație (`pnpm dev`): un model „din alt program” (mm, niveluri, materiale, 4 scaune ca instanțe, o linie), primit, apoi o versiune nouă (un perete mai lung, unul mutat): 1 adăugat, 1 actualizat, 9 neschimbate, 1 șters, id-urile păstrate; „house 2” primit în alt proiect, văzut în 3D cu culorile lui; un Undo scoate tot.

**Recunoașterea pereților și a plăcilor (23.11.2026, ramura `feat/speckle-s3`, decizia 812):** făcută.

- **Opțiunea:** `speckle.receive` primește `recognize` (implicit nu, ca la Fragments; fereastra o trimite bifată: „Recognise walls and slabs”). Se oferă doar ce e `IfcWall`/`IfcSlab` după clasa citită în S2.
- **Metoda:** aceeași mașinărie ca la Fragments, `MeshRecognition` (triunghiurile citite ca prismă, peretele cu golurile lui, oferite recunoscătorilor modulelor, `IIfcRecognizer`) și poarta de fidelitate `RecognitionFidelity` impusă: ce diferă cu mai mult de 1 mm sau 0,5 % revine referința care era, cu același id. Trei adaosuri ale Speckle (`SpeckleRecognition.cs`):
  - **Indiciile** (`SpeckleHints`): `location` (linia din Revit), parametrii cu `internalDefinitionName` (`WALL_ATTR_WIDTH_PARAM`, `FLOOR_ATTR_THICKNESS_PARAM`, `FLOOR_ATTR_DEFAULT_THICKNESS_PARAM`, `WALL_USER_HEIGHT_PARAM`), straturile trimise de Lapin, altfel parametrii după nume (Thickness, Width, Default Thickness, Unconnected Height), doar cu unitatea lor (și cele ale Revit: „Millimeters”, „Feet and fractional inches”). Nu se construiește nimic din ele: se verifică față de corp (grosimea, linia care trebuie să meargă de-a lungul corpului și în el, înălțimea), iar o contradicție lasă obiectul referință — corpul are dreptate. Înălțimea neconectată din Revit se ia doar la un perete cu vârful neconectat (`WALL_HEIGHT_TYPE`) și neatașat: altfel e o valoare veche (pe snowdon structural respingea 7 pereți buni). Fără indicii: corpul singur.
  - **Mesh-urile pe material:** Revit trimite un perete ca un mesh pe materialul fețelor (fețele vopsite într-unul, capetele și creștetul miezului în altul), niciunul închis singur. Când împreună închid un solid (vârfurile sudate la 0,01 mm), se citesc ca unul; corpul propriu se pune la loc dacă rămâne referință.
  - **Plăcile întâi, apoi pereții**, iar o placă recunoscută primește prioritatea de tăiere a peretelui (50, nu 60): pereții din Revit urcă în planșeul de deasupra așa cum au venit, iar o placă nativă care le-ar tăia capul i-ar face alții decât corpul primit (pe „revit model” poarta respingea astfel 271 de pereți, „box 470 mm at the top”).
  - Pereții cortină (familia, tipul sau categoria „Curtain”) rămân referințe.
- **Ce păstrează un element recunoscut:** GlobalId-ul (cel derivat din S2), grupul, etajul, proprietățile (`properties`, setul `Speckle`), `source` și atributul `speckle`, cu `recognition: native`; o referință încercată poartă `recognition: reference`, ca o primire nouă fără schimbare să n-o mai încerce. Nimic Lapin: nici buiandrugi, glafuri sau îmbinări.
- **Actualizarea:** potrivirile se află întâi; ce a dispărut din versiune și un element recunoscut al cărui obiect s-a schimbat se scot înainte să intre ceva (un perete nativ își ia golurile cu el). Cel schimbat intră din nou ca referință, cu același GlobalId și în grup, și e oferit din nou: nativ dacă trece, referință dacă nu. Neschimbat: rămâne cum e. Totul un singur pas de undo.
- **Raportul:** `recognized`, `native`, `references` și `recognition` {recognized, offered, byClass, kept (de ce, fără cifre), reverted}; fereastra arată Recunoscute, Referințe și „Stayed references: …”, notificarea „… N native walls and slabs, M references”.
- **Măsurat** pe serverul local (modele publice copiate de pe app.speckle.systems, conectorul Revit v3): „revit model” (mm): pereți 448 din 450 nativi, planșee 50 din 58 (8: grosimea din parametru 101,6 mm față de corpul de 152,4 mm; 2 pereți: conturul la bază); cei 79 de pereți cortină nu au corp propriu (panourile și montanții vin ca referințe); primirea cu tot cu descărcarea ~43 s pentru 5 928 de obiecte (Release). „snowdon structural” (ft): pereți 35 din 58, planșee 19 din 27; restul: corpul nu e o prismă (12), lățimea sau grosimea contrazic corpul (13, plăci înclinate și pereți cu pilaștri), volumul diferă (6). testLapin.lapin trimis și primit: 3 din 4 (un perete nu e prismă).
- **Teste:** `SpeckleRecognitionTests` (7): perete drept, placă, perete strâmb / cu o adâncitură de 20 mm (poarta), indiciu contrazis, curtain wall, corp fără indicii, perete în două mesh-uri deschise sub un planșeu, actualizare (mutat, neregulat, șters, readus) cu GlobalId-urile păstrate, model Lapin primit ca copie. Cele trei clase de teste Speckle rulează într-o colecție (tipurile SDK-ului se inițializează o dată).

**Etajele din nivelurile versiunii (23.11.2026, ramura `feat/speckle-s3`, decizia 813):** făcute; înlocuiesc regula S2 „niciun etaj nou”.

- **De ce:** „revit model” (18 niveluri, plus Level -1, 18 și 19) primit într-un proiect cu 2 etaje punea tot pe cele două, deci planurile pe etaje erau greșite.
- **Nivelurile:** `SpeckleRead` le adună pe toate o dată (`levelProxies`, apoi nivelul unui obiect când nu e în proxy), fiecare cu `applicationId`-ul lui (al nivelului Revit, același de la o versiune la alta), după care o primire nouă își găsește etajul. Cota și unitatea pe nivelul însuși (`elevation`, `units`, cum le scrie conectorul Revit v3), altfel în `properties`.
- **Potrivirea** (`SpeckleLevels.Resolve`, în același pas de undo): GlobalId-ul trimis de Lapin; etajul făcut pentru acel nivel de o primire anterioară a modelului; cota (5 cm, același nume întâi); numele. Ce nu se potrivește devine etaj, cu numele și cota nivelului, sub un id derivat din model și nivel (sau GlobalId-ul nivelului, dacă e liber), cu atributul `speckleLevel` {key, level}. Înălțimea unui etaj făcut: până la etajul de deasupra (ultimul 3 m); etajele proiectului rămân cum sunt.
- **Cotele:** ca geometria, după originea aleasă (cota punctului de bază adăugată, sau nu de la originea internă). Datum-ul: când cele mai multe obiecte (peste jumătate, cel puțin 3) stau la aceeași înălțime față de nivelurile lor, la peste 5 cm, nivelurile se mută cu ea (nivelurile Revit date de la punctul de topografie, geometria de la originea internă) și o notă spune cu cât. Pe cele două modele publice: 0.
- **O versiune nouă:** redenumește și mută doar etajele pe care le-a făcut modelul, și doar cât timp nimic străin nu stă pe ele; mutarea și când stă pe el un element nativ al modelului (un perete recunoscut stă pe cota etajului). Un nivel dispărut: etajul se șterge dacă nu stă nimic pe el (niciun element, nimic agregat, nicio vedere păstrată), altfel rămâne, cu notă. Un etaj al proiectului nu e atins niciodată.
- **Opțiunea:** `levels: "create"` (implicit) | `"existing"` (ca S2: un nivel nepotrivit pune obiectele pe etajul de dedesubt); `storeyId` pune tot pe un etaj. Fereastra: „by the version's levels, creating the missing ones” (implicit), „… only the project's storeys”, „all on the current storey”; raportul `levels` {matched, made, renamed, moved, removed, kept, datum} și „Storeys made”.
- **Măsurat** (serverul local, Release, cu recunoașterea, proiect cu Ground floor la 0 și First floor la 4 m): „revit model” (mm): 21 de niveluri, 2 potrivite, 19 făcute, 246–449 de obiecte pe fiecare etaj (Level 19 are unul); pereți 450 din 450 nativi (448 pe un singur etaj), planșee 48 din 58 (50); 26 s. „snowdon structural” (ft): 19 niveluri, 1 potrivit, 18 făcute (L2 și L2 TOS la 15 cm unul de altul, deci etaje separate); pereți 35 din 58, planșee 16 din 27 (19 pe un singur etaj: trei planșee pică acum la conturul în plan, „base outline” 1–5,9 m, necercetat încă); 18 s.
- **Teste:** `SpeckleLevelsTests` (7): 18 niveluri într-un proiect cu 2 etaje (2 potrivite, 16 făcute, înălțimile, un pas de undo, primit din nou fără nimic nou), un perete recunoscut pe etajul făcut, redenumire și mutare (și refuzul lor când un perete al proiectului stă pe etaj), niveluri dispărute (șters gol, păstrat ocupat, etajele proiectului niciodată), ft, datum diferit, „doar etajele proiectului” și originea.

**Rămas din S2:**
- Trei planșee din „snowdon structural” trec poarta pe un singur etaj și pică pe etajele lor (conturul în plan): de aflat ce le taie conturul.
- Recunoașterea altor clase (stâlpi, grinzi, uși și ferestre ca umpluturi ale pereților recunoscuți; `MeshRecognition` le știe, dar Speckle nu le oferă încă). Pereții curbi (linia `location` un arc) nu sunt verificați după grosime, iar recunoscătorul nu-i ia.
- Copiii unui obiect Revit fără corp (pereții cortină) vin ca obiecte separate, dar peretele însuși nu are o intrare; un tip de perete creat la recunoaștere care rămâne nefolosit după o actualizare nu se șterge.
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
