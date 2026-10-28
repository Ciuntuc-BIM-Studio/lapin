# Registrul deciziilor

Aici sunt deciziile luate, cu data și sursa. Cele vechi, din fork-ul ifc-lite, apar doar dacă se aplică și lui lapin-pro. Căile `Lapin/…` se citesc în `~/Documents/GitHub/Lapin/`.

## Produsul

| Data | Decizia | Sursa |
|---|---|---|
| 28.10.2026 | Interfața are trei moduri (Confortabil, Compact, Concentrare) și fiecare parte aleasă separat, per utilizator; panourile laterale se pliază și se deschid temporar peste vedere în orice mod; scurtăturile sunt un registru unic (acorduri și secvențe de litere, cu regula față de linia de comandă), configurabile, cu setul implicit în stil Revit. | `decisions-pending.md` #156, raportul #26 |
| 25.10.2026 | Arborele proiectului are seturi (filtru pe parametrii vederilor și planșelor; implicit Architecture / Structure / MEP după `discipline`); nivelurile, vederile (cu sau fără adnotări), vederile 3D (salvate cu cameră) și planșele se duplică. Configuratorul de tâmplărie arată desenele motorului de simboluri (vedere, plan, secțiune) cu convenția proiectului; planurile și elevațiile le folosesc. Golurile primesc finisaj pe șpaleți din straturile de față ale peretelui (implicit), glaf, solbanc, ancadrament și prag, ca piese cu material, măsurate; peretele e tăiat doar unde o piesă îi ia locul. | `plan-views-and-joinery.md`, #125–127 |
| 25.10.2026 | Cusături vizuale pe verticală (cerut de utilizator): unde suprafața unui material continuă în același plan dintr-un element în altul (tencuiala fațadei peste nivelul următor, peretele următor al unei fațade drepte), muchia dintre ele nu se desenează: în 3D (muchiile părții, tăiate de `Seams` contra fețelor vecinilor cu aceeași cheie de material), în elevații și secțiuni (liniile ascunse unesc doar fețele coplanare cu aceeași cheie; benzile tăiate se uneau deja). Doar afișare: elementele rămân separate la selecție, în inspector și în IFC. Cheia e numele materialului (altfel felul elementului); un material cu `drawJoints` sau o membrană nu se unește cu nimic. Un finisaj diferit deasupra își păstrează linia. | `SeamTests`, `#108` |
| 25.10.2026 | Seturile de proprietăți proprii ale lui Lapin nu mai încep cu `Pset_` (rezervat de IFC seturilor din standard; serviciul de validare bSI le semnalează): `Lapin_Intent`, `Lapin_Phase`, `Lapin_Parameters`. Importul citește și vechiul `Pset_Lapin_Intent`; GlobalId-urile seturilor rămân aceleași. Prima observație a unui tester. | `spec-ifc.md` |
| 13.10.2026 | Lapin optimizează proiectele modelate în Lapin. Documentarea IFC-urilor importate ține de IfcSheet. | memorie `lapin-native-first` |
| 23.10.2026 | Lapin se rescrie fără ifc-lite. Aplicația e desktop întâi (Tauri), într-un repo separat, cu motorul BubbleBIM Pro ca dependență. | `plan.md` |
| 23.10.2026 | Lapin e **doar MIT**. Licența ifc-lite nu mai e o preocupare; codul din fork nu se copiază, ci se rescrie. | `plan.md` |
| 23.10.2026 | Modelul Unity / Unreal: totul se lucrează local. Cloud-ul aduce conturile, actualizările și bibliotecile; mai târziu și sincronizarea și versiunile. | `plan.md`, `vendor/bubblebim-pro/docs/PLAN.md` |
| 24.10.2026 | Edițiile sunt combinații de module, distribuite curat în producție (de exemplu doar arhitectură + structură, doar civile, doar MEP), nu neapărat module premium. Un modul își declară dependențele, iar acestea vin cu el. | `plan.md`, `scripts/product.mjs` |
| 24.10.2026 | Se poate folosi cod din BubbleBIM Pro și Atrium, proiectele C# ale utilizatorului. | `plan.md` |
| 23.10.2026 | Edițiile se compun la build din module (`products/*.json`). Ce nu e în ediție nu există în cod, nici în motor, nici în interfață. | `README.md`, `scripts/build-product.mjs` |
| 23.10.2026 | Plugin-urile Bubbles nu ajung niciodată în repo-uri publice, pe site sau în build-uri. | memorie `lapin-plugins-private` |

## Arhitectura

| Data | Decizia | Sursa |
|---|---|---|
| 23.10.2026 | Lapin e un modelator clasic pe schema IFC4X3: `IfcWall`, tipuri, `IfcMaterialLayerSetUsage`, rels. Modelul îl ține motorul C# Lapin. | `plan.md` |
| 23.10.2026 | Din BubbleBIM se folosește doar geometria (Platform.Geometry, Platform.Booleans, Step.cs, IfcModel.cs), nu și graful lui. | `plan.md` |
| 23.10.2026 | Schimbul cu BubbleBIM se face prin IFC cu intenție: reprezentarea `Axis`, layer set-uri, `Pset_BubbleBIM_Intent` și GlobalId-uri stabile. | `plan.md` |
| 23.10.2026 | Protocolul cu interfața e WebSocket JSON-RPC (`doc.execute`, diff-uri). Interfața nu calculează. | `plan.md` |
| 23.10.2026 | Formatul `.lapin` se păstrează (formatul 3, care citește și formatul 2). | `plan.md`, `format-lapin.md` |
| 24.10.2026 | Stocarea în formatul 3: modelul e un graf în memorie, în motorul C#; pe disc, un zip de bucăți adresate după conținut, cu tabele Arrow IPC pe modul × clasă × etaj. Fără bază de date graf. | `format-lapin.md` |
| 24.10.2026 | Motorul Lapin stă în `engine/` din lapin-pro: Lapin.Model, Lapin.Platform (cu modulul „core”), Lapin.Package, modulele, Lapin.Host. | `plan.md` |
| 14.10.2026 | Se salvează intenția, nu rezultatul. Schema e „în formă de IFC”. GlobalId-urile sunt stabile. Tabelele de lucru sunt Arrow IPC, iar Parquet servește doar analizei. | `Lapin/docs/format-lapin-nativ.md` |
| 14.10.2026 | La deschiderea unui IFC, utilizatorul alege între „Keep as IFC” și „Convert to native”. | `Lapin/docs/format-lapin-nativ.md` |

## Cum se lucrează

| Data | Decizia | Sursa |
|---|---|---|
| 23.10.2026 | Testele din fork sunt specificația și se portează înaintea codului, cu aceleași cazuri și aceleași numere. | `transfer.md` |
| 23.10.2026 | Fișierele OWN se rescriu. Părțile ifc-lite (îmbinările, drawing-2d) se reimplementează după comportamentul descris în `transfer.md`. | `transfer.md` |
| — | Commit-urile nu poartă co-autor Claude. Fișierele se adaugă după cale explicită. | `CLAUDE.md` |
| — | Interfața, codul și commit-urile sunt în engleză; documentele de plan, în română. | `CLAUDE.md` |

## Domenii (specificații de portat)

| Data | Decizia | Sursa |
|---|---|---|
| 17.10.2026 | Pereții se compun și pe înălțime: zone, centuri, buiandrugi, execuție pe tipuri (cofraje, resurse, articole de deviz). | `Lapin/docs/pereti-compusi-si-executie.md` |
| — | Căpriorii se termină în fețe verticale la coamă și la streașină. | memorie `rafter-plumb-ends` |
| 23.10.2026 | MEP-ul rezidențial (`feature/residential-mep`) se portează ca modul `mep`, cu ediția lui (M9). | `transfer.md` |
| 21.10.2026 | Liniile din desene, buiandrugii în configurator, subrapoartele și tabelele de tâmplărie au planuri pe funcție. | `Lapin/docs/linii-in-desene.md`, `buiandrugi-configurator-si-extras.md`, `subrapoarte-si-tabele-tamplarie.md` |

## Deschise

- Etapele M0–M9 din `plan.md` sunt o propunere.
- Cine modifică motorul BubbleBIM Pro și cum se versionează (`plan.md`, Riscuri).
- Din `vendor/bubblebim-pro/docs/PLAN.md`: versionarea modulelor, lucrul în echipă, semnarea aplicației și updater-ul.
