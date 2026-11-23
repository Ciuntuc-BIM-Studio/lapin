# Plan: Atrium ca CDE al lui Lapin, legat ca git (commit, push, pull)

Hotărât de utilizator la 23.11.2026 (decizia 793). Documentul e **arhitectura**, nu implementarea: nu conține cod. Pasul următor e prototipul S1 (§11).

- **Atrium devine mediul comun de date (CDE) al lui Lapin**, cu o legătură ca în git: *commit*, *push*, *pull*, spre un **depozit la distanță** (*remote*) care e fie **un folder** (local, un NAS, un disc, un folder sincronizat), fie **un server** (Atrium Server), autogăzduit sau în cloud. Primul test se face pe serverul Hetzner al firmei.
- **Licențierea: nucleu deschis, plus extra premium.** Protocolul, clientul din Lapin, aplicația Atrium și Atrium Server autogăzduit sunt deschise (Lapin e MIT, Atrium e MIT-0). Premium sunt găzduirea în cloudul firmei, echipele și permisiunile avansate, SharePoint și seturile de reguli ale firmei, ca la Speckle.

**Ce înlocuiește.** `docs/plan-cde.md` (decizia 185) spune „fără server, folderul e schimbul”. Asta rămâne adevărat **cât timp proiectul nu are un depozit la distanță**: folderul proiectului, emiterile în fișier, verificările și observațiile BCF merg exact ca azi. Planul de față adaugă depozitul la distanță ca opțiune. Unde e ales, el devine locul comun al istoricului; folderul rămâne implicit.

---

## 1. Ce există și pe ce se clădește

| Ce | Unde | Ce dă planului |
|---|---|---|
| Pachetul `.lapin`, formatul 3 | `Lapin.Package/LapinPackage.cs`, `docs/format-lapin.md` | zip cu `manifest.json`: fiecare parte cu calea, SHA-256 (al octeților necomprimați) și mărimea; părțile `model/<modul>/<clasă>/<etaj>.arrow`, `model/relations.arrow`, `project/document.json`, textele (`scripts/`, `graphs/`) |
| Versiunile | `PackageVersions.cs`, `docs/versions.md` | o versiune = un `PackageManifest` + număr, nume, notă, moment, revizii; blob-urile în `versions/blobs/<sha>` o singură dată; `ModelCompare` (diff pe GlobalId, pe părți), `Apply` (restaurare într-un pas de undo) |
| Emiterile | `PackageIssues.cs`, `Cde/CdeIssues.cs` | `LapinIssue` (revizia ISO 19650, statusul, containerul, fișierele cu SHA-256); `issues/blobs/<sha>`; o emitere își salvează versiunea |
| Părțile străine | `PackageForeign.cs` | o intrare necunoscută de la rădăcina zip-ului e păstrată octet cu octet de orice Lapin, deci și de unul mai vechi |
| Observațiile | `Cde/Remarks/` (BCF 2.1/3.0, `LapinRemark` cu id = GUID-ul subiectului) | import cu îmbinare după GUID |
| Lucrul împreună | `docs/plan-multiplayer.md`, decizia 153a | `Author` pe `Transaction`, `AllowedRemote`, roluri, rezervări (planificate, etapa 2), releul |
| Atrium Server | `Atrium/src/Atrium.Server` | OpenCDE Foundation 1.1 (OAuth2: parolă + refresh, tokenuri opace, doar SHA-256 lor în SQLite), BCF 3.0 complet (subiecte, comentarii, puncte de vedere, OData), roluri viewer/editor/owner pe proiect, un fișier SQLite într-un folder de date, CORS deschis |
| Aplicația Atrium | `Atrium/src/Atrium.Core/Format/IfcReader.cs`, `AtriumFile.cs` | deschide IFC și **`.lapin` doar în formatul 2** (`manifest.Models` cu IFC-uri în zip); recenzia `.atrium` ține sursa prin cale + SHA-256; jurnalul de editări (`EditJournal`: hide/demolish/delete, transform, clone, place) |
| Serverul Hetzner | `deploy/store/README.md`, `deploy/relay/README.md` | `/opt/lapin/docker-compose.yml`; tunelul Cloudflare trimite numele de gazdă la nginx-ul din `lapin-web` (127.0.0.1:3461), care face proxy spre containere pe rețeaua compose |

Două constatări care schimbă planul:

1. **Atrium nu poate deschide azi un `.lapin` de format 3.** Cititorul lui caută IFC-uri în zip (formatul 2). Formatul 3 ține intenția (tabele Arrow), iar geometria e derivată de motorul lui Lapin. Atrium are deci nevoie de o **copie de vizualizare** (§5.1).
2. **Adresarea după conținut există deja în fișier.** Un commit e aproape o versiune: același manifest, plus părinți și autor. Planul nu inventează un format nou, ci dă versiunilor un graf și un transport.

---

## 2. Modelul obiectelor

### 2.1 Obiectele

Totul e un **obiect adresat după SHA-256-ul octeților lui** (necomprimați, ca în manifestul de azi). Sunt patru feluri, deosebite de cine le numește, nu de un antet:

| Fel | Ce e | Cum arată |
|---|---|---|
| **blob** | o parte a pachetului (`model/…arrow`, `project/document.json`, `scripts/x.py`) sau un fișier al unei emiteri (PDF, CSV, DXF, IFC) | octeții, exact cum stau în zip |
| **arbore** (*tree*) | manifestul unei stări a proiectului: `PackageManifest` de azi, **fără `foreign`** (ca la versiuni) | JSON canonic (RFC 8785, JCS), deci același hash în C#, Rust sau JS |
| **commit** | o stare cu istoria ei | JSON canonic, mai jos |
| **etichetă** (*tag*) | o emitere ISO 19650, sau numele unei versiuni | JSON canonic, mai jos |

Commit-ul:

```json
{
  "format": "lapin-commit", "v": 1,
  "tree": "<sha256 al manifestului>",
  "parents": ["<sha256>"],
  "author": { "id": "atrium:7f3c…", "name": "Ionuț Ciuntuc" },
  "at": "2026-11-23T10:42:00Z",
  "message": "Pereții etajului 2, varianta B",
  "app": "0.1.1612", "product": "lapin",
  "entities": 12840,
  "viewer": { "ifc": "<sha256>", "schema": "IFC4" }
}
```

- `parents`: zero pentru primul commit, unul de obicei, doi pentru o îmbinare (S4).
- `author.id` e id-ul contului de pe server (`atrium:<id>`) sau, pe un folder, `local:<id aleator al instalării>`. **Fără e-mail**: fișierul circulă, iar numele ajunge.
- `viewer` e **opțional** și nu face parte din arbore: e copia derivată pe care o deschide Atrium (§5.1). Lipsa ei nu strică nimic; poate fi ștearsă de GC și refăcută.
- Hash-ul commit-ului e SHA-256 al JSON-ului canonic. Un câmp necunoscut e păstrat și intră în hash: o versiune mai nouă poate adăuga câmpuri fără să rupă istoria.

Eticheta unei emiteri:

```json
{
  "format": "lapin-tag", "v": 1,
  "commit": "<sha256>",
  "kind": "issue",
  "issue": "<GlobalId al LapinIssue>",
  "revision": "P02", "status": "S2", "container": "Shared",
  "files": [{ "name": "PRJ-ORG-ZZ-00-DR-A-0101_S2_P02.pdf", "sha256": "…", "size": 412331 }],
  "author": { "id": "…", "name": "…" }, "at": "…", "note": "…"
}
```

Fișierele emiterii sunt blob-uri numite de etichetă, deci urcă și coboară cu ea. O etichetă e **imuabilă**: o emitere nu se mută pe alt commit (ISO 19650: o revizie emisă rămâne ce a fost).

### 2.2 Referințele

| Ref | Ce numește |
|---|---|
| `refs/heads/main` | ramura principală, implicită; capul proiectului pe depozit |
| `refs/heads/wip/<autor>/<nume>` | o ramură personală (S1: „păstrează-mi lucrul ca ramură” când pull-ul nu se poate face, §4) |
| `refs/tags/issue/<n>-<revizie>` | o emitere (`issue/7-P02`) |
| `refs/tags/v/<număr>` | o versiune numită (§2.3) |
| `refs/proposals/<id>` | o propunere de editare venită din Atrium (S3, §5.4) |

Local, proiectul mai ține `HEAD` (ramura pe care e) și, pentru fiecare depozit, ultima stare văzută a ref-urilor lui (`remotes/<nume>/…`), ca să poată spune „înainte cu 2, în urmă cu 1” fără rețea.

### 2.3 Commit-urile și versiunile de azi

**O versiune devine un commit numit.** Save version face un commit (dacă proiectul diferă de ultimul) și pune eticheta `v/<n>` pe el. Dialogul Manage → Versions arată exact ce arată azi (numărul, numele, nota, revizile), iar comenzile `project.saveVersion`, `restoreVersion`, `compareVersions`, `exportVersion` merg neschimbate.

- **`versions/index.json` se scrie în continuare**, cu fiecare versiune ca manifest, cum o citește un Lapin mai vechi. E o vedere derivată din etichetele `v/*`, nu o a doua sursă.
- **Revizia unei versiuni** („Update to current”) mută eticheta `v/<n>` pe un commit nou. Revizia veche rămâne commit-ul pe care îl numea eticheta înainte (jurnalul ref-ului o ține).
- **O emitere** face, ca azi, înregistrarea `LapinIssue` și versiunea, iar în plus commit-ul și eticheta `issue/<n>-<rev>`. Pe un depozit la distanță, eticheta urcă la următorul push. Emiterea nu se poate face cât timp depozitul a primit commit-uri pe care proiectul nu le are (§4.1): o revizie emisă trebuie să fie pe istoria comună.
- **Restore** a unui commit oarecare (nu doar al unei versiuni) folosește `ModelCompare.Apply`: un pas de undo, proiectul nesalvat, istoria neatinsă.

### 2.4 Unde stă istoria locală: în fișier

Ca versiunile, istoria stă **în fișierul `.lapin`** (fișierul se trimite cu tot cu ea):

```
history/HEAD                     "refs/heads/main"
history/refs.json                ref-urile locale și ultima stare văzută a fiecărui depozit
history/commits/<sha>            commit-urile și etichetele (JSON canonic, mici)
history/objects/<sha>            blob-urile și arborii pe care nu le ține deja fișierul
history/remotes.json             depozitele: nume, fel (folder | server), adresa, id-ul proiectului (fără tokenuri)
```

Regula de o singură dată se păstrează: un blob stă în fișier **o dată**, în prima dintre: partea vie, `versions/blobs/` (dacă o versiune o numește), `history/objects/`.

**Un Lapin mai vechi** deschide fișierul ca înainte: `history/` nu e între numele rezervate ale lui, deci îl ia drept parte străină și îl scrie înapoi octet cu octet (`PackageForeign`). Ce pierde: editările făcute în el nu sunt commit-uite (Lapin-ul nou le vede la deschidere ca „modificări necommit-uite”, adică partea vie diferă de arborele lui `HEAD`) și, dacă șterge o versiune, blob-urile ei pleacă din `versions/blobs/`. Lapin-ul nou verifică la deschidere că tot ce numește istoria există (*fsck*). Ce lipsește se aduce din depozit, iar dacă depozitul nu îl are, commit-ul e marcat incomplet: se vede în istorie, dar nu se poate restaura. Formatul rămâne 3; `history` intră în numele rezervate ale versiunii noi.

---

## 3. Protocolul

Un singur protocol, **neutru față de limbaj**: JSON canonic, SHA-256 și zip. Același pe un folder și pe server, ca să se poată scrie oricând un client sau un server în altă limbă (§9). Specificația lui va sta în `docs/spec-lapin-remote.md` (MIT), cu **vectori de conformitate** (fișiere JSON de intrare și ieșire așteptată), pe care îi rulează și testele lui Lapin, și ale lui Atrium Server.

### 3.1 Operațiile

- **fetch**: citește ref-urile depozitului, apoi commit-urile pe care proiectul nu le are (mergând înapoi de la cap până la commit-uri cunoscute), apoi obiectele lipsă din arborii lor. Nu schimbă modelul.
- **pull** = fetch + avans rapid (*fast-forward*): dacă `HEAD` e strămoș al capului depozitului, modelul e adus la arborele nou prin `ModelCompare.Apply` (un pas de undo, ca Restore), `HEAD` se mută, fișierul se salvează. Altfel: §4.
- **push**:
  1. obiectele de urcat: tot ce e accesibil din commit-urile locale care lipsesc din ultima stare văzută a depozitului;
  2. **negocierea pe liste de hash-uri**: clientul trimite lista, depozitul răspunde cu cele pe care nu le are (loturi de 10 000);
  3. se urcă doar acelea (cele mari, în bucăți reluabile);
  4. **actualizarea atomică a ref-ului cu compare-and-swap**: `{ref, old, new}`. Dacă ref-ul nu mai e `old`, actualizarea e refuzată cu capul curent, iar clientul face întâi pull.
- **clone** („Deschide de pe depozit”): fetch într-un proiect nou, apoi pull; fișierul local se alege la prima salvare.

### 3.2 Integritatea

- **Serverul nu crede hash-urile clientului.** Fiecare obiect primit e hash-uit de server cât se scrie (pe flux, fără să fie ținut în memorie); un hash diferit e refuzat (400) și aruncat.
- **Conectivitatea**, la actualizarea unui ref: commit-ul există și e un JSON valid (schema, câmpurile obligatorii), arborele lui există și e un manifest valid, fiecare parte numită de arbore există cu mărimea declarată, părinții există. Căile din manifest trec regulile lui `PackageForeign` (fără `..`, `\`, segmente goale; numai numele rezervate ale lui Lapin). Altfel 422, cu lista ce lipsește.
- **Nimic nu se execută** pe server sau în Atrium: scripturile proiectului sunt text.
- Clientul verifică și el fiecare obiect coborât (hash) înainte de a-l scrie în fișier.

### 3.3 Mărimi, reluare, compresie

- **Obiect mic** (≤ 16 MiB): un singur `PUT`.
- **Obiect mare**: o sesiune de încărcare, cu bucăți de 16 MiB (sub limita de 100 MB pe cerere a lui Cloudflare), reluabilă după offset (după modelul tus 1.0, simplificat). La ultima bucată serverul verifică hash-ul și abia atunci obiectul există. Sesiunile neterminate se șterg după 24 h.
- **Limite implicite**, configurabile pe server: obiect ≤ 2 GiB, un push ≤ 4 GiB, cotă pe proiect (doar în găzduirea premium, §8). Răspunsurile: 413 (prea mare), 507 (cota).
- **Compresia** e a transportului (`Content-Encoding: gzip`, sau zstd când îl au ambele părți), nu a obiectului: hash-ul e mereu al octeților necomprimați. Pe un folder, obiectele se scriu comprimate gzip (`objects/ab/<sha>.gz`).
- **Coborârea în lot**: `POST …/objects/pack` cu o listă de hash-uri dă un **zip** (intrările numite după hash), trimis pe flux. Zip-ul e deja formatul lui Lapin, deci nu e nevoie de un format de pachet nou.

### 3.4 Curățenia (GC)

- Un obiect e viu dacă e accesibil dintr-un ref, dintr-o etichetă sau dintr-o intrare a jurnalului de ref-uri mai nouă de 30 de zile.
- Se șterge doar ce e mort și mai vechi de 24 h (ca un push în curs să nu-și piardă obiectele).
- **Etichetele emiterilor nu se șterg niciodată** prin GC (arhiva ISO 19650), ci doar de owner, explicit, cu jurnal.
- Copiile de vizualizare (`viewer`) sunt regenerabile: GC le păstrează pentru capetele ramurilor, pentru etichete și pentru ultimele 10 commit-uri de pe `main`.
- Pe server, GC rulează zilnic. Pe un folder, la cerere (Manage → Remotes → Clean up), sub lacăt.

### 3.5 Depozitul folder

Aceeași structură ca stocarea serverului, ca să se poată muta un proiect de pe un folder pe un server prin copiere:

```
<folder>/lapin-remote.json                    {"format":"lapin-remote","v":1}
<folder>/<id proiect>/project.json            numele, ramura implicită
<folder>/<id proiect>/objects/ab/<sha>.gz
<folder>/<id proiect>/commits/<sha>.json
<folder>/<id proiect>/refs/heads/main/<seq>-<commit>-<autor>.ref     {old, new, author, at}
<folder>/<id proiect>/lock                     lacătul (creat exclusiv)
```

- **Compare-and-swap pe un disc sau un NAS (SMB/NFS)**: lacăt creat exclusiv (`CreateNew`), citirea ref-ului, compararea, scrierea, ștergerea lacătului. Un lacăt mai vechi de 10 minute e spart, cu avertisment.
- **Pe un folder sincronizat** (OneDrive, SharePoint sync, Dropbox), crearea exclusivă nu e atomică între calculatoare. De aceea un ref nu e un fișier rescris, ci un **jurnal numai-adăugare**: fiecare actualizare e un fișier nou care spune `{old, new}`. Capul e capătul lanțului. Două fișiere cu același `old` sunt o ramificare, detectată de oricine face fetch. Pierde cel cu momentul mai târziu: ref-ul lui devine automat `wip/<autor>/…`, iar autorul vede „depozitul s-a mișcat, fă pull”. Pe un folder sincronizat, CAS-ul e deci **detectat după fapt**, nu prevenit, dar **nu se pierde nimic**.

---

## 4. Îmbinarea

### 4.1 S1: doar avans rapid

| Situația | Ce se întâmplă |
|---|---|
| Proiectul local e în urmă, fără commit-uri proprii, fără modificări | pull = avans rapid |
| Proiectul local are modificări necommit-uite, depozitul s-a mișcat | pull refuzat: „Fă commit întâi” |
| Commit-uri locale, depozitul neschimbat | push |
| Commit-uri locale **și** depozitul s-a mișcat (divergență) | push refuzat (409): „Ana a urcat 2 commit-uri la 10:42. Fă pull întâi.” Pull-ul nu poate îmbina în S1, așa că dialogul oferă: **Păstrează lucrul meu ca ramură** (push pe `wip/<eu>/<data>`, apoi pull pe `main`, deci proiectul ajunge la `main`, iar lucrul meu e sigur pe server) sau **Renunță la commit-urile mele** (rămân în fișier ca ramură locală, restaurabile). |

### 4.2 S4: îmbinarea pe părți, apoi pe entități

Îmbinare în trei căi: baza e strămoșul comun cel mai apropiat.

1. **Pe părți** (modul × clasă × etaj, apoi `project/document.json`, relațiile, textele): o parte schimbată de o singură parte se ia de acolo. Cele mai multe îmbinări reale (doi oameni pe etaje sau discipline diferite) se termină aici, fără să citească nicio tabelă.
2. **Pe entități**, pentru o parte schimbată de ambii: rândurile Arrow după GlobalId. O entitate schimbată de un singur om se ia. Schimbată de ambii: se compară **atribut cu atribut**. Atribute diferite se îmbină; același atribut cu valori diferite e un **conflict**. Ștergere de o parte și modificare de cealaltă e tot conflict.
3. **Relațiile obiectivate** după GlobalId-ul lor; membrii unei relații de conținere ca mulțime (adăugări și scoateri îmbinate, cum compensează `plan-multiplayer` membrii la undo).
4. **`project/document.json`** (vederi, planșe, stiluri): îmbinare JSON după id-urile obiectelor.
5. **Textele** (scripturi, grafuri): îmbinare pe linii (diff3). Un conflict se rezolvă alegând o parte, niciodată cu marcaje lăsate în fișier.
6. **Părțile unui modul pe care ediția nu-l are** se îmbină doar pe părți: schimbate de ambii, sunt conflict cu alegerea „a mea / a lor”.
7. **Ce se derivă nu se îmbină.** Îmbinările de pereți, golurile, geometria se refac din intenție după îmbinare, ca la orice comandă; GlobalId-urile rămân stabile.

**Vederea de conflicte** din Lapin: lista pe clasă și etaj (ca dialogul Compare), fiecare conflict cu baza, „a mea” și „a lor”, atributul care diferă și alegerea pe entitate sau pe atribut. În 3D, conflictele colorate (diff-ul vizual rămas din `docs/versions.md`). Rezultatul e un commit cu doi părinți, făcut într-un singur pas de undo.

### 4.3 Rezervările, ca alt mod de lucru

Pentru echipele care vor să evite conflictele, nu să le rezolve, rezervările din `plan-multiplayer.md` (etapa 2) se pun pe server: `POST …/reservations` cu elemente, un etaj, o clasă sau o planșă. Lapin le citește la fetch, iar `CommandContext.EnsureEditable` avertizează (implicit) sau refuză (opțiune pe proiect) o editare pe ce a rezervat altul. Sunt **consultative**: offline se poate lucra oricum, iar ce se ciocnește iese la îmbinare. Rezervările simple sunt deschise; regulile pe echipe sunt premium (§8).

---

## 5. Rolul lui Atrium

### 5.1 Deschide orice commit

- **Copia de vizualizare.** La push, Lapin atașează commit-urilor de pe capul ramurii și etichetelor un export IFC4 (aceleași GlobalId-uri, aceeași rezolvare `VisibilityGraphics` ca exportul de azi), ca obiect `viewer.ifc`. Atrium îl deschide cu cititorul IFC pe care îl are deja. Exportul costă timp la push (de măsurat în S1 pe Bloc-P-4) și spațiu, dar se deduplică doar când nu se schimbă nimic. De aceea GC-ul de la §3.4 îl tratează ca regenerabil.
- **Mai târziu (după S3)**, în locul IFC-ului: părți de plasă derivate (`derived/<hash kernel>/<modul>/<clasă>/<etaj>.arrow`, cache-ul rezervat de `docs/format-lapin.md`). Atrium le-ar citi pe etaje, la cerere, fără să parseze STEP.
- **Din folder sau de pe server**, Atrium:
  - cere `GET …/commits/<sha>/package`: un `.lapin` asamblat de server (arborele + `derived/ifc/<sha>.ifc` când există copia);
  - sau citește direct obiectele (arborele, apoi `viewer.ifc`).
  
  Cititorul lui Atrium învață formatul 3 (manifestul `lapin-project`, versiunea 3) lângă formatul 2.
- **Recenzia `.atrium`** ține sursa ca `{remote, project, commit}` în loc de cale + SHA-256. Commit-ul e deja un hash, deci „modelul s-a schimbat” devine „există un commit mai nou pe `main`”.

### 5.2 Compară două commit-uri

- **Pe părți**: diferența dintre doi arbori, fără să citească nimic altceva.
- **Pe entități**: tabelele Arrow citite cu `Apache.Arrow` (Apache-2.0, deja în Lapin), după GlobalId, pe schema tabelelor descrisă în `docs/spec-lapin-remote.md`.

Rezultatul se colorează pe copia de vizualizare (adăugat, șters, modificat), după GlobalId-urile IFC, care sunt aceleași.

### 5.3 Datele recenziei, prin BCF 3.0

- **Comentariile** lui Atrium devin subiecte BCF pe Atrium Server (API-ul există). Fiecare subiect e legat de commit-ul pe care a fost făcut printr-o etichetă `lapin-commit:<sha>` în `labels` și un `reference_link`.
- **Coliziunile** devin subiecte cu `topic_type: "Clash"`, câte unul pe grup (Atrium exportă deja coliziuni ca BCF), cu componentele după IfcGuid.
- **Măsurătorile** devin subiecte cu `topic_type: "Measurement"`: segmentele ca `lines` ale punctului de vedere (BCF 3.0 le are), valoarea în descriere.
- **În Lapin**, un client BCF-API (lângă `BcfReader`/`BcfWriter`) aduce subiectele la fetch ca `LapinRemark`, după GUID, cu regula de azi a importului (câmpurile serverului câștigă, comentariile și punctele de vedere se îmbină după GUID). Observațiile făcute în Lapin urcă la push. Pe un depozit server, **serverul e sursa observațiilor**, iar entitățile `LapinRemark` sunt copia lor locală.

### 5.4 Editările din Atrium devin propuneri

Atrium nu are motorul lui Lapin, deci nu poate face un commit (n-ar putea reface tabelele). În schimb:

- urcă **jurnalul** lui (`EditJournal`, intrări după GlobalId) ca obiect și deschide o **propunere**: `POST …/proposals {base: <commit>, journal: <sha>, message}` → `refs/proposals/<id>`;
- Lapin listează propunerile (Manage → Remote → Proposals), le arată peste model și le aplică ca **comenzi**, într-un singur pas de undo:

| Atrium | Lapin |
|---|---|
| transform | mutare și rotire (`element.move`, `element.rotate`) |
| hide / demolish | faza „demolat” (modulul `phases`) |
| hide / delete | ștergere (core) |
| clone | copiere |
| place (tip de familie Revit) | refuzat, listat („nu are echivalent în Lapin”) |

- urmează un commit „Propunerea lui X aplicată”, iar propunerea se închide cu commit-ul ei. O propunere pe o bază veche se aplică pe GlobalId-uri; ce nu mai există e listat ca neaplicat.

---

## 6. Interfața din Lapin

- **Manage → Remote…** (lângă Manage → Versions și Manage → Project folder):
  - depozitul (Folder… cu alegerea folderului; Server… cu adresa, Sign in, alegerea sau crearea proiectului pe server);
  - Clone / Open from remote… (și în File);
  - Clean up (GC pe folder).
  
  Un proiect poate avea un singur depozit în S1–S3.
- **Bara de stare** (desktop și browser), când proiectul are depozit: ramura, `↑2 ↓1` (înainte / în urmă, din ultimul fetch), cine a urcat ultimul și când („Ana, 10:42”), iar offline „offline, 3 de urcat”. Un clic deschide panoul cu **Commit…** (mesajul; arată ce părți s-au schimbat, pe zone, ca Compare), **Push**, **Pull**, **Fetch**.
- **Manage → Versions** primește fila **History**: commit-urile ramurii (autor, moment, mesaj, câte entități s-au schimbat), versiunile și emiterile ca etichete pe ele, Compare între oricare două, Restore, Export. Fila Versions rămâne cum e.
- **Fetch** se face la deschidere și la 5 minute când e rețea (setare). Nu schimbă modelul, doar numerele.
- **Offline**: commit merge mereu (e local), push și pull așteaptă rețeaua. Nimic nu blochează lucrul.
- **Conflictele**: în S1, dialogul din §4.1; din S4, vederea de conflicte (§4.2).
- **Comenzile motorului**: `cde.remote.set`, `cde.remote.status`, `cde.commit`, `cde.fetch`, `cde.pull`, `cde.push`, `cde.clone`, `cde.history`, `cde.proposals`, `cde.applyProposal`. Toate sunt **refuzate oaspeților** unei sesiuni comune (nu intră în `EngineDispatcher.AllowedRemote`): doar gazda vorbește cu depozitul.
- **Browserul**: depozitul server merge (fetch cu CORS, pe care Atrium Server îl permite deja). Folderul e doar al desktopului.
- **Șirurile noi** intră în toate limbile din `ui/src/i18n`; ghidul primește o pagină și o scenă (`docs/user`, `scripts/user-docs/scenes.mjs`) de la S2.

---

## 7. Securitatea și confidențialitatea

- **Tokenurile nu intră niciodată în proiect.** `history/remotes.json` ține adresa și id-ul proiectului, atât. Tokenul de refresh stă în seiful sistemului: Keychain, Credential Manager, Secret Service, prin crate-ul `keyring` (MIT OR Apache-2.0) în shell-ul Tauri, care e deja Rust. În browser, doar în memoria filei (o nouă autentificare la reîncărcare). Tokenul de acces (1 h) stă doar în memorie.
- **Autentificarea** e OAuth2-ul existent al lui Atrium Server (Foundation 1.1): S1 cu acordarea prin parolă (cum face Atrium), S2 cu **authorization code + PKCE** prin browserul sistemului și redirecționare pe loopback (RFC 8252). `Tokens` are deja felul `code`.
- **Accesul e pe proiect**: rolurile de azi.
  - **viewer**: fetch, clone, citire BCF;
  - **editor**: push pe ramuri, propuneri, subiecte;
  - **owner**: ramura `main` protejată (implicit, doar owner sau editor; setare), etichete de emitere, ștergeri, GC.

  Un proiect neîngăduit dă 404, nu 403, ca să nu-i trădeze existența.
- **Serverul verifică tot** (§3.2): hash-urile, schema, căile, conectivitatea. Limite de rată (ca la store: pe utilizator și pe adresă).
- **Jurnalul ref-urilor** (cine a mutat ce, când, de la ce la ce) e păstrat pe server și nu se șterge. E și pista de audit pe care o cere ISO 19650.
- **Copiile de rezervă**:
  - zilnic, `sqlite3 .backup` (consecvent, cu serverul pornit) și folderul de obiecte;
  - obiectele sunt imuabile și numite după hash, deci copia incrementală e trivială;
  - spre un Hetzner Storage Box, criptat (de exemplu restic, BSD-2-Clause, ca unealtă, nu ca dependență);
  - **proba restaurării** pe un container gol face parte din acceptarea lui S1.
- **TLS**: pe Hetzner, tunelul Cloudflare termină TLS-ul la marginea Cloudflare, deci Cloudflare vede traficul în clar (întrebarea Î13). Un server autogăzduit își pune TLS-ul propriu (Caddy sau nginx).
- **Ce nu face serverul**: nu rulează scripturile proiectului, nu deschide IFC-uri în S1–S4 (conversiile pe server sunt premium, mai târziu), nu trimite e-mailuri.

---

## 8. Deschis și premium

| Deschis | Unde, licența |
|---|---|
| Protocolul (`docs/spec-lapin-remote.md`) și vectorii lui de conformitate | lapin-pro, MIT |
| Clientul din Lapin: istoria în fișier, folderul, serverul, îmbinarea (S4), propunerile, BCF-API | lapin-pro, MIT |
| Aplicația Atrium: deschiderea commit-urilor, diff-ul, recenzia prin BCF, propunerile | Atrium, MIT-0 |
| Atrium Server autogăzduit: conturi, proiecte cu roluri viewer/editor/owner, ref-uri cu CAS, obiecte, încărcări reluabile, GC, BCF 3.0, propuneri, rezervări simple, `.lapin` asamblat | Atrium, MIT-0 |

| Premium (depozit privat, nimic în lapin-pro în afară de cârlige) |
|---|
| Găzduirea în cloudul firmei: multi-chiriaș, cote, copii de rezervă și SLA |
| Echipe și organizații; permisiuni avansate: ramuri protejate pe roluri, drepturi de scriere pe modul sau etaj, fluxul de aprobare S → A cu semnătură, exportul pistei de audit |
| Conturile platformei (Google, Microsoft) ca identitate a serverului găzduit |
| SharePoint prin Graph: containerele ISO 19650 și emiterile oglindite într-o bibliotecă de documente (etapa 4 din `plan-cde.md`) |
| Seturile de reguli ale firmei: denumiri, câmpuri cerute, reguli blocante, rulate la emitere **și la push** |
| Mai târziu: conversii grele și îmbinări pe server |

**Cârligele din depozitul deschis:**

- **În Lapin** (`Lapin.Package/Remote`, `Lapin.Platform/Cde`):
  - `RemoteHooks.Providers`: un fel de depozit nou după schema adresei (premium: `lapin-cloud://…`, cu autentificarea platformei);
  - `IRemoteAuth`: cum se obține tokenul;
  - `RemoteHooks.BeforePush`: reguli `ICdeRule` rulate înaintea unui push, blocante sau nu;
  - `CdeHooks`, cel de azi: regulile și denumirea.
- **În Atrium Server**, deschise, cu implementarea simplă implicită:
  - `IAccessPolicy`: rolul unei cereri; implicit, rolurile proiectului;
  - `IObjectStorage`: implicit, discul; premium, stocare compatibilă S3;
  - `IIdentityProvider`: implicit, conturile proprii; premium, tokenurile platformei;
  - `IServerExtension`: endpoint-uri și politici dintr-un assembly pus în `extensions/`.
- Un modul deschis nu depinde niciodată de cele premium.

---

## 9. Rust: unde ar aduce viteză (hotărât după dovezi)

Cererea utilizatorului (23.11.2026): să se porteze în Rust părți din Atrium, sau tot, acolo unde ajută performanța. Se hotărăște pe măsurători, nu din principiu.

### 9.1 Ce se știe deja

- **Parserul lui Atrium** (`Atrium.Ifc/Step/StepFile.cs`):
  - face `File.ReadAllBytes`, decodează tot fișierul într-un `string` (UTF-16, de două ori mărimea) și construiește fiecare entitate ca `StepEntity` cu o `List<object?>` de valori împachetate (*boxed*);
  - e exact proiectarea pe care Lapin o avea înainte de 29.10.2026. Măsurat atunci în Lapin (`docs/plan-instancing.md`) pe exportul Revit de 1,2 GB (`02767_0_IDP_EJE_ARQ+EST+MEP.ifc`, 20,9 milioane de entități):
    - **nu se putea citi deloc** (un `string` .NET are cel mult 1 GB);
    - parsat întreg, avea **7,6 GB de obiecte**.
  - Lapin a rezolvat asta **în C#**: fișierul ținut ca octeți, un index (unde începe fiecare entitate și ce tip are) făcut pe toate nucleele, entitățile parsate la cerere și nepăstrate. Rezultatul: parserul ține „fișierul și câteva sute de MB”, iar importul întreg a scăzut de la 233 s la **19 s** (M1 Max, Release).
  - Problema nr. 1 din foaia de drum a lui Atrium are deci **o soluție C# deja măsurată**, în același stil de cod (cele două `StepFile` au aceeași origine).
- **Teselarea** lui Atrium (`Atrium.Ifc/Geometry/Tessellator.cs`, `IfcImport.cs`) rulează pe un singur fir. Lapin construiește elementele pe toate nucleele.
- **Booleenele** sunt deja native (manifold, C++, prin API-ul C) în ambele.
- **Coliziunile**: `ClashTree` (faza largă), faza fină deja paralelă (`Parallel.For`).
- **Rendererul lui Atrium**:
  - Silk.NET OpenGL 2.23 pe macOS, unde OpenGL e depreciat din 10.14, oprit la 4.1, fără compute shadere, tradus peste Metal;
  - măsurat: < 11 ms pe cadru în locul de test (Y-D2).
  - Riscul real e platforma (Apple poate scoate OpenGL), nu limbajul.
- **Hash-urile și diff-ul** pentru push/pull:
  - SHA-256 în .NET folosește criptografia sistemului, cu instrucțiunile SHA ale ARMv8 / SHA-NI (de ordinul GB/s);
  - părțile unui proiect sunt de ordinul sutelor sau miilor, de KB–MB;
  - push și pull sunt limitate de rețea, nu de CPU.
- **Serverul**: pentru câteva birouri, încărcarea e de I/O (fluxuri de octeți pe disc). Kestrel o duce fără probleme.

### 9.2 Pe părți

| Partea | Câștigul Rust față de C#/.NET 10 bine scris (span-uri, SIMD prin `Vector128/256` și `SearchValues`, NativeAOT) | Costul | Recomandarea | Când | Dovada |
|---|---|---|---|---|---|
| Parserul IFC/STEP al lui Atrium | estimat 1,2–2× pe parsarea brută; zero pe memorie față de soluția indexată din Lapin | două limbi, FFI, CI pe trei platforme | **optimizat în C#**: soluția indexată a lui Lapin portată în Atrium | pistă Atrium separată (A-P1), înainte de S3 | Bloc-P-4 (2,9 MB), BURKWIL_DX_B (29 MB) și exportul de 1,2 GB: timp și memorie maximă, înainte / după; ținta: fișierul de 1,2 GB se deschide, cu memoria parserului ≤ 2× fișierul |
| Teselarea | la fel ca C# paralel | la fel | **optimizată în C#**: produsele pe toate nucleele | A-P1 | aceleași trei fișiere, timpul până la prima imagine |
| BVH și faza largă a coliziunilor | mic (bucle numerice: JIT-ul .NET 10 vectorizează) | la fel | **păstrată în C#**; tablouri de structuri SoA pentru cutii, dacă profilul o cere | A-P2, la nevoie | toate-cu-toate pe BURKWIL și pe exportul de 1,2 GB: timpul fazei largi |
| Rendererul lui Atrium | Rust/wgpu nu e mai rapid decât același wgpu chemat din C# | rescrierea a ~30 de fișiere de randare și a UI-ului de joc | **trecut pe WebGPU din C#**: `wgpu-native` (Apache-2.0) prin `Silk.NET.WebGPU` (MIT, 2.23.0, aceeași versiune Silk), shadere WGSL; Metal, Vulkan, DX12 fără OpenGL | pistă Atrium separată (A-R), după S3 | timpul de cadru în locul de test (< 11 ms azi) și pe Bloc-P-4 și exportul de 1,2 GB, GL față de WebGPU, pe M1 Max și pe un GPU integrat |
| Hash-urile, negocierea, diff-ul | ~0 (rețeaua domină) | nejustificat | **păstrate în C#** | — | push de Bloc-P-4 și al exportului de 1,2 GB: timpul CPU al clientului ≤ 10 % din timpul total |
| Serverul (stocarea obiectelor) | Rust ar fi potrivit (`axum` MIT, `tokio` MIT, `object_store` MIT/Apache-2.0), dar nu e nevoie azi | un al doilea server de întreținut, OAuth2 și BCF de rescris | **păstrat în C#** (Atrium Server); protocolul rămâne neutru, deci un server Rust care trece vectorii de conformitate se poate scrie dacă găzduirea premium o cere | doar dacă dovada o cere | 20 de clienți în paralel pe Hetzner: p95 la `objects/missing` și la `PUT` ≤ 200 ms, CPU ≤ 50 %; peste prag se reconsideră |
| Tokenurile în seiful sistemului | — | crate-ul `keyring` în shell-ul Tauri, deja Rust | **Rust, acolo unde e deja Rust** | S1 | — |

**Concluzia: nicio portare în Rust acum.** Câștigurile mari (memoria, paralelismul, platforma grafică) vin din proiectare, nu din limbă, iar fiecare are o cale C# măsurată sau directă. Rust rămâne o opțiune cu prag de dovadă, nu o direcție.

### 9.3 Licențele (verificate pe crates.io la 23.11.2026)

- **Permise**:
  - IFC/STEP și geometrie: `ruststep` 0.4 (Apache-2.0, ultima în 2024), `truck-*` 0.6 (Apache-2.0, 2024), `ifc_rs` 0.1-alpha (MIT, alfa, 2024), `parry3d` (Apache-2.0), `bvh` (MIT), `earcutr` (ISC), `i_overlay` (MIT/Apache-2.0), `manifold3d` și `manifold-rs` (MIT/Apache-2.0);
  - grafică și denoise: `wgpu` 30 (MIT/Apache-2.0), `oidn` (legăturile, MIT; biblioteca, Apache-2.0);
  - date, hash-uri și server: `arrow` (Apache-2.0), `zip` (MIT), `sha2` (MIT/Apache-2.0), `blake3` (CC0/Apache-2.0), `axum` (MIT), `tokio` (MIT), `object_store` (MIT/Apache-2.0), `rusqlite` (MIT);
  - utilitare: `keyring` (MIT/Apache-2.0), `csbindgen` (MIT), `memmap2` și `rayon` (MIT/Apache-2.0).
- **Excluse**: `ifc-lite-core` (MPL-2.0, și regula fork-ului), crate-ul `ifc` (MPL-2.0) și **`uniffi` (MPL-2.0)**. FFI-ul C# ↔ Rust, dacă va fi vreodată, trece prin API-ul C cu `csbindgen` (MIT), nu prin uniffi.
- O eventuală parte Rust intră în `pnpm check:licenses` prin `cargo deny check licenses` (cargo-deny, MIT/Apache-2.0), pe tot arborele de dependențe, nu doar pe crate-urile directe.
- **Codul portat din Lapin (MIT) în Atrium (MIT-0)**: utilizatorul e autorul ambelor. Fișierele lui proprii le poate da sub MIT-0 în Atrium; un fișier cu alt contribuitor își păstrează notița MIT.

---

## 10. Rust și randarea realistă a lui Lapin

Cererea utilizatorului (23.11.2026): evaluarea să cuprindă și randarea realistă din `docs/plan-render.md` (S1–S4: texturi PBR, lumina artificială și noaptea, sondele de reflexie, urmărirea razelor cu `three-gpu-pathtracer` și denoiser-ul Intel Open Image Denoise lângă motor, `render.denoise`, `Lapin.Host/Denoiser.cs`).

### 10.1 Ce e azi și ce s-a măsurat

- **Vederea live**:
  - three.js 0.187 pe WebGL2, în WebView-ul Tauri și în browser;
  - Realist, noaptea, 1756 × 1426 px, M1 Max: 8,7 ms fără lumini, 14 ms cu 10 lumini, 25 ms cu 50;
  - sufrageria cu sonde: 12–13 ms.
- **Urmărirea razelor**:
  - `three-gpu-pathtracer` 0.0.28 (MIT) pe WebGL2, scena construită de `pathTrace.ts` exact din ce desenează vederea (rezolvitorul unic);
  - casa de cărămidă (3 496 de triunghiuri): **1080p, 256 de eșantioane în 60 s**, plus OIDN ~3 s;
  - sufrageria noaptea: **198 s** (peste ținta de 1–3 min din `plan-render.md` §5).
  - **Bloc-P-4 și exportul de 1,2 GB nu s-au măsurat.**
- **Denoise**: OIDN 2.5.1 (Apache-2.0) ca program separat pe desktop (fișiere PFM); în browser, filtrul à-trous al lui Lapin.

### 10.2 Variantele

| Varianta | Pentru | Împotrivă |
|---|---|---|
| **a. Urmăritor de raze Rust/wgpu pe desktop** (program separat, ca OIDN, sau în shell-ul Tauri) | compute shadere în loc de fragment shadere: estimat 2–5× pe scenele mari (BVH mai bun, wavefront, fără limitele texturilor WebGL); OIDN legat direct, fără PFM | un al doilea renderer de întreținut, cu materiale și lumini de ținut identice; urmărirea razelor hardware din wgpu (*ray query*) e încă experimentală și nu pe toate backend-urile (de verificat la momentul deciziei); imaginea progresivă trebuie trimisă înapoi în WebView |
| **b. Același, compilat în wasm/WebGPU pentru browser** | un singur urmăritor pe ambele | două stive grafice în pagină (three + wgpu-wasm), mărimea pachetului web, WebGPU încă inegal între browsere; three are deja `WebGPURenderer` și TSL, deci aceeași trecere se poate face în JS |
| **c. Un nucleu de randare Rust comun lui Lapin și Atrium** | un singur cod | vederea lui Lapin e three.js, cu tot ce stă pe ea (unelte, alegere, suprapuneri); mutarea ei ar fi o rescriere a viewportului. Atrium e o aplicație C# nativă. Singura piesă cu adevărat comună ar fi urmăritorul offline. |
| **d. Rămâne three.js** (WebGL azi, `WebGPURenderer` când three îl face matur), plus trimiterea în Blender (S6 din `plan-render.md`) pentru imaginile grele | un singur renderer; browserul merge neschimbat; Cycles (cu OIDN-ul lui) face calitatea de producție fără cod de randare nou | imaginile mari depind de Blender, instalat de utilizator |

**Regula care decide**: rezolvitorul unic (`VisibilityGraphics`) hotărăște ce arată o vedere. Orice urmăritor nou trebuie să primească **exact scena pe care o construiește `pathTrace.ts`**, serializată (plasele, materialele, luminile, tăieturile). Nu are voie să-și facă singur scena, altfel ar exista două adevăruri.

### 10.3 Recomandarea, pe părți

| Partea | Recomandarea | Dovada |
|---|---|---|
| Vederea live (Desen / Mediu / Realist) | **rămâne three.js** în UI; trecerea pe `WebGPURenderer` e o decizie separată, când three o face stabilă | timpul de cadru pe Bloc-P-4 și pe exportul de 1,2 GB, Realist, la zi și noaptea, M1 Max și un GPU integrat |
| Urmărirea razelor | **rămâne `three-gpu-pathtracer`**. Întâi se măsoară pe modelele mari. **Prag**: Bloc-P-4 la 1080p cu 256 de eșantioane + denoise peste 5 min pe M1 Max, sau exportul de 1,2 GB nerandabil (limitele texturilor BVH). Peste prag se face întâi **trimiterea în Blender (S6)**, iar abia apoi, dacă tot e nevoie, un urmăritor **Rust/wgpu offline, doar desktop**, ca program separat lângă OIDN, care primește scena serializată de `pathTrace.ts` | **timpul până la o imagine convergentă** (1080p, 256 de eșantioane + denoise; și timpul până la o varianță pe pixel sub prag) pe Bloc-P-4 și pe exportul de 1,2 GB, înainte / după; aceeași scenă verificată prin diferența de imagine la convergență față de three-gpu-pathtracer; acceptat doar la ≥ 2× mai repede |
| Denoise | **rămâne OIDN ca program separat** pe desktop și filtrul propriu în browser; legat direct doar dacă există urmăritorul Rust (crate-ul `oidn`, MIT) | timpul OIDN pe 1080p și 4K (~3 s azi) |
| Browserul | **rămâne three** (WebGL2, apoi WebGPU prin three). Nicio stivă wasm de randare în paralel | ediția web trece testele ei (`build:web`) neschimbate |
| Un nucleu comun Lapin–Atrium | **nu**. Ce se împarte e **limbajul shaderelor (WGSL)**, prin trecerea lui Atrium pe WebGPU (§9.2) și a lui Lapin pe `WebGPURenderer`, nu un renderer | — |

**Unde intră**: o **pistă separată, R (randarea)**, independentă de CDE. Nu ține de nicio etapă S1–S5 și nu o întârzie. Primul ei pas e doar măsurarea (R0: Bloc-P-4 și exportul de 1,2 GB deschise în vederea 3D printr-o scenă a ghidului, cu un import IFC fără dialog). Abia cu cifrele se decide dacă există R1.

---

## 11. Găzduirea pe Hetzner, pentru test

- **Containerul** `lapin-cde`, lângă `lapin-web`, `lapin-feedback`, `lapin-store` și `lapin-relay`, în `/opt/lapin/docker-compose.yml`:
  - imaginea `mcr.microsoft.com/dotnet/aspnet:10.0` (sau Atrium.Server publicat self-contained pentru linux-x64, ca releul);
  - `ATRIUM_DATA=/data`, `--urls http://0.0.0.0:8080`, `./cde-data:/data`, `read_only: true`, `mem_limit: 512m`, utilizatorul 1000:1000;
  - un port pe loopback ales liber (de verificat cu `ss -ltnp`, ca la store).
- **nginx** (din `lapin-web`): un bloc `server` pentru `cde.lapinbim.com`, cu `proxy_pass` spre `lapin-cde:8080` pe rețeaua compose, `client_max_body_size 20m` (bucățile au 16 MiB), `proxy_request_buffering off` și `proxy_buffering off` (fluxuri), timeouts de 10 min.
- **TLS**: cel al tunelului Cloudflare, ca la `store.lapinbim.com`. nginx primește HTTP pe loopback.
- **Ce face utilizatorul în Cloudflare** (doar el; nimic în DNS de mână):
  1. Zero Trust → Networks → Tunnels → tunelul serverului → Public Hostname → Add: subdomeniul `cde`, domeniul `lapinbim.com`, serviciul **HTTP** spre nginx-ul lui `lapin-web` (127.0.0.1:3461, ca store-ul). Cloudflare face singur înregistrarea CNAME.
  2. Security → Bots: Bot Fight Mode să nu provoace `cde.lapinbim.com`: o regulă WAF *skip* pentru acest nume. Clientul desktop nu poate trece o provocare JavaScript.
  3. Caching: nimic. Serverul trimite `Cache-Control: private, no-store` pe tot ce e autentificat; obiectele, deși imuabile, sunt private.
- **Stocarea**: `/opt/lapin/cde-data/`, cu `atrium.db` și `objects/<proiect>/ab/<sha>`. Copia de rezervă e zilnică (§7), cu restaurarea încercată o dată, ca parte din acceptarea lui S1.
- **Conturile**: `docker exec -it lapin-cde ./Atrium.Server user add <e-mail> --admin`. Parola o tastează utilizatorul în terminalul lui, niciodată în chat.
- **Scriptul**: `deploy/cde/deploy.sh` + `deploy/cde/README.md` în lapin-pro (ca `deploy/relay`): construiește Atrium.Server din depozitul Atrium, îl copiază, repornește containerul, verifică `/foundation/versions`.

---

## 12. API-ul serverului

**Alături de OpenCDE, nu în interiorul lui.**

- **Documents API 1.0** e un flux de documente: fișiere întregi cu versiuni și metadate, ales din interfața web a CDE-ului printr-o fereastră de browser. Nu are adresare după conținut, transfer doar al părților lipsă, compare-and-swap pe ref-uri sau un graf de commit-uri. Ar trebui strâmbat ca să le poarte.
- Protocolul stă deci sub `/lapin-remote/1.0/` și se anunță în `/foundation/versions` ca o intrare `api_id: "lapin-remote"` (clienții OpenCDE ignoră ce nu cunosc).
- Autentificarea e **aceeași** (OAuth2 din Foundation 1.1, `Authorization: Bearer`), iar proiectele sunt **aceleași** ca în BCF (un id de proiect pentru istorie și pentru subiecte).
- **Documents API se adaugă mai târziu (S3b), doar în citire**, pentru ce e cu adevărat document: emiterile (etichetele `issue/*` cu fișierele lor). Așa, alte unelte OpenCDE pot lua planșele emise.

Bază: `/lapin-remote/1.0/projects/{projectId}`.

| Metoda și calea | Corpul | Răspunsul | Coduri |
|---|---|---|---|
| `GET /` | — | `{id, name, defaultRef, role, limits: {maxObject, chunk, maxBatch}}` | 200, 401, 404 |
| `GET /refs` | — | `{refs: [{name, commit, updatedAt, updatedBy}]}`, `ETag` | 200 |
| `POST /refs/update` | `{updates: [{name, old, new}]}`; toate sau niciuna | `{refs: […]}` | 200; 409 `{name, current}` (s-a mișcat: pull); 422 `{missing: […]}`; 403 (rolul sau ramura protejată) |
| `GET /refs/log?name=` | — | `{entries: [{old, new, by, at}]}` | 200 |
| `POST /objects/missing` | `{hashes: […]}` (≤ 10 000) | `{missing: […]}` | 200, 400 |
| `PUT /objects/{sha256}` | octeții (≤ 16 MiB; `Content-Encoding: gzip` acceptat) | — | 201 creat, 200 exista, 400 hash greșit, 413 |
| `POST /uploads` | `{sha256, size}` | `{id, offset, chunk}` | 201; 200 cu `{exists: true}` |
| `HEAD /uploads/{id}` | — | `Upload-Offset` | 200, 404 (expirată) |
| `PATCH /uploads/{id}` | o bucată; `Upload-Offset` | `{offset}`; la ultima: obiectul verificat | 204, 201, 400 (hash), 409 (offset greșit) |
| `GET /objects/{sha256}` | — | octeții; `ETag: "<sha>"` | 200, 404 |
| `POST /objects/pack` | `{hashes: […]}` | `application/zip` pe flux, intrările numite după hash | 200 |
| `GET /commits?from={sha}&stop={sha,…}&limit=` | — | `{commits: [{id, …commit}], more}` | 200 |
| `GET /commits/{sha}` | — | commit-ul (JSON canonic) | 200, 404 |
| `GET /commits/{sha}/package` | — | un `.lapin` asamblat (arborele + `derived/ifc/` când există copia) | 200, 404 |
| `POST /proposals` (S3) | `{base, journal, message}` | `{id, ref}` | 201 |
| `GET /proposals`, `POST /proposals/{id}/close` (S3) | `{commit?}` | | 200 |
| `GET/POST/DELETE /reservations` (S4) | `{targets: […]}` | `{reservations: […]}` | 200, 409 (rezervat de altul) |

Erorile au corpul Foundation (`{message}`), plus `code` stabil (`ref_moved`, `object_missing`, `hash_mismatch`, `too_large`, `quota`). 401 vine cu `WWW-Authenticate: Bearer`, iar 429 cu `Retry-After`.

**În Atrium Server:**

- `Remote/RemoteEndpoints.cs`;
- `Data/Refs.cs`: ref-urile în SQLite; CAS-ul e `UPDATE refs SET commit=$new WHERE project=$p AND name=$n AND commit=$old`, iar 0 rânduri înseamnă 409; jurnalul se scrie în aceeași tranzacție;
- `Data/ObjectStore.cs`: discul; scriere în fișier temporar, hash pe flux, `rename`;
- `Data/Uploads.cs`; `Remote/Gc.cs`.

**În Lapin:**

- `Lapin.Package/Remote/`: `Commit`, `Tag`, `CanonicalJson`, `LocalHistory`, `IRemote` cu `FolderRemote`, `HttpRemote` și `MemoryRemote` (pentru teste), `Negotiation`;
- comenzile în `EngineDispatcher`;
- UI-ul în `ui/src/modules/core/cde/remote/`.

---

## 13. Etapele

Fiecare etapă are teste offline (xUnit, fără rețea) și teste pe Hetzner. Vectorii de conformitate ai protocolului (`docs/spec-lapin-remote.md`) se rulează în ambele depozite.

### S1. Prototipul: commit, push și pull cu avans rapid, Atrium deschide un commit

**Ce cuprinde:**

- **Lapin**:
  - istoria în fișier (§2.4), versiunile ca etichete (§2.3);
  - `HttpRemote`, clone, commit, fetch, pull cu avans rapid, push cu negociere, încărcări reluabile, refuzul 409 cu dialogul din §4.1;
  - copia de vizualizare IFC la push;
  - Manage → Remote (server și sign in cu parolă), bara de stare;
  - tokenul în seiful sistemului.
- **Atrium Server**: endpoint-urile din §12, fără propuneri și rezervări; GC; `.lapin` asamblat.
- **Atrium**: File → Open from server… (sign in, proiectul, lista commit-urilor de pe `main`, deschidere prin `viewer.ifc`); cititorul formatului 3.
- **Local întâi** (răspunsul lui Ionuț, 23.11.2026): Atrium Server pornit pe Mac (`dotnet run` pe `http://127.0.0.1`, date într-un folder temporar), Lapin desktop și Atrium legate de el; Hetzner (§11) abia după ce proba locală trece, ca S1b.

**Teste offline (xUnit):**

- **Obiectele**: JSON-ul canonic dă aceleași hash-uri ca vectorii; un câmp necunoscut e păstrat.
- **Istoria în fișier**:
  - un fișier cu istorie, deschis și salvat de citirea formatului 3 fără `history` în numele rezervate (simularea unui Lapin vechi), își păstrează `history/` octet cu octet;
  - blob-urile apar o singură dată;
  - fsck găsește un obiect lipsă.
- **Versiunile**: Save version face eticheta `v/n`, iar `versions/index.json` rămâne identic cu cel de azi pentru aceeași stare; `VersionTests` trec neschimbate.
- **`MemoryRemote`** (și `WebApplicationFactory` pentru Atrium Server, în depozitul lui):
  - push, apoi clone într-un proiect nou: `ModelCompare` dă zero diferențe;
  - al doilea push urcă doar părțile schimbate (numărate);
  - push concurent: al doilea primește 409, iar după pull trece;
  - un obiect cu hash greșit dă 400 și nu se scrie;
  - un ref spre un commit cu parte lipsă dă 422;
  - o încărcare întreruptă la jumătate se reia de la offset;
  - viewer: push dă 403, iar fără token dă 401.
- **Pull cu avans rapid**: un singur pas de undo; un pull cu modificări locale e refuzat.

**Local, apoi pe Hetzner (manual, cu lista în `deploy/cde/README.md`; întâi cu serverul pe `127.0.0.1`, două proiecte sau două conturi pe același Mac, apoi S1b pe Hetzner):**

- Bloc-P-4 importat ca proiect nativ, commit-uit și urcat de pe Mac (A).
- Clonat pe al doilea calculator, sau în a doua instanță cu alt cont (B); B editează și urcă; A face pull, iar modelele sunt egale.
- A urcă fără pull: refuzat, apoi „păstrează ca ramură”.
- Atrium deschide de pe server commit-ul lui B.
- Se măsoară: mărimea și timpul primului push și al celui de-al doilea (doar părțile), timpul exportului copiei de vizualizare.
- Copia de rezervă: restaurată într-un container gol, aceleași ref-uri.

### S2. Depozitul folder și istoria în interfață

- `FolderRemote` cu lacătul și jurnalul numai-adăugare (§3.5);
- fila History din Versions; Compare și Restore pe commit-uri; ramurile `wip/*`;
- emiterile ca etichete urcate;
- sign in cu authorization code + PKCE; depozitul server în ediția web;
- GC pe folder; ghidul (pagina și scena).

**Teste offline:**

- folder pe disc temporar: CAS-ul cu lacăt sub două procese;
- folderul sincronizat simulat (două copii ale folderului, reunite apoi): ramificarea e detectată, iar perdantul devine `wip/…` fără pierderi;
- mutarea unui proiect de pe folder pe server prin copiere dă aceleași hash-uri;
- PKCE.

**Pe Hetzner:** autentificarea din browser; ediția web face clone, commit și push.

**Plus:** un folder pe un share SMB din birou, cu două calculatoare.

### S3. Datele recenziei și BCF dus-întors

- clientul BCF-API în Lapin (observațiile la fetch și la push);
- comentariile, coliziunile și măsurătorile lui Atrium ca subiecte legate de commit;
- propunerile (§5.4) și aplicarea lor în Lapin;
- diff-ul a două commit-uri în Atrium.
- **S3b**: Documents API în citire pentru emiteri.

**Teste offline:**

- un subiect făcut pe server apare ca `LapinRemark`, iar o modificare în Lapin urcă (după GUID, ambele sensuri);
- maparea fiecărei operații a jurnalului Atrium pe comenzi Lapin, un pas de undo;
- propunerea pe o bază veche listează ce nu s-a aplicat;
- diff-ul pe entități din Arrow e egal cu `ModelCompare`.

**Pe Hetzner:** o recenzie completă: Atrium comentează, face coliziuni și propune o demolare; Lapin primește, aplică și urcă; Atrium vede commit-ul nou și închide subiectele.

### S4. Îmbinarea pe părți și pe entități

- îmbinarea în trei căi (§4.2) și vederea de conflicte;
- rezervările pe server (§4.3);
- push-ul după o îmbinare.

**Teste offline:**

- etaje diferite: îmbinare doar pe părți, nicio tabelă citită;
- același perete, atribute diferite: îmbinat; același atribut: conflict;
- ștergere contra modificare;
- membrii unei relații de conținere;
- `document.json` după id-uri; textele cu diff3;
- partea unui modul absent;
- GlobalId-urile stabile după îmbinare;
- o rezervare a altuia face `EnsureEditable` să avertizeze sau să refuze.

**Pe Hetzner:** doi utilizatori pe Bloc-P-4, unul pe parter, altul pe etajul 2, apoi amândoi pe același perete.

### S5. Găzduirea premium și echipele

În depozitul privat:

- multi-chiriaș, cote, organizații și echipe, ramuri protejate pe roluri, drepturi pe modul sau etaj;
- conturile platformei ca identitate;
- oglinda SharePoint; regulile firmei la push.

**Teste**: în depozitul privat. În lapin-pro doar cârligele, cu furnizori falși (un `IRemoteProvider` și un `BeforePush` de test).

**Pistele separate (nu țin de S1–S5):**

- **A-P1**: parserul indexat și teselarea paralelă în Atrium, §9;
- **A-R**: Atrium pe WebGPU, §9;
- **R0/R1**: măsurarea randării și eventualul urmăritor offline, §10.

---

## 14. Întrebările pentru Ionuț

**Răspuns (23.11.2026): toate cele 17, după recomandare.** Prima probă se face local (serverul pe Mac), Hetzner după (S1b).

1. **Istoria locală în fișierul `.lapin`** (`history/`), ca versiunile, sau într-un folder alături? *Recomandarea: în fișier; fișierul circulă cu istoria lui.*
2. **Istorie completă sau scurtată** în fișier când există un server (obiectele vechi doar pe server, aduse la cerere)? *Recomandarea: completă implicit; „scurtată” ca opțiune după S2, pentru proiectele mari.*
3. **Versiunile ca etichete pe commit-uri**, cu `versions/index.json` scris mai departe pentru Lapin-urile vechi? *Recomandarea: da; dialogul Versions rămâne, History se adaugă.*
4. **Commit explicit sau automat la salvare?** *Recomandarea: explicit. Save rămâne Save. O emitere face întotdeauna commit și etichetă.*
5. **Copia de vizualizare IFC** la fiecare push, sau doar la cerere? *Recomandarea: la capul ramurii și la etichete, păstrată pentru ultimele 10 commit-uri de pe `main`; părțile de plasă derivate o înlocuiesc mai târziu.*
6. **Autorul unui commit**: numele și id-ul contului, fără e-mail? *Recomandarea: da.*
7. **Obiectele pe server deduplicate pe proiect sau global?** *Recomandarea: pe proiect (ștergerea unui proiect e ștergerea unui folder; nicio scurgere între proiecte prin „există deja”).*
8. **Baza serverului**: SQLite (cum are Atrium Server) sau PostgreSQL (cum prevede planul premium pentru platformă)? *Recomandarea: SQLite în varianta deschisă; găzduirea premium decide singură, în spatele cârligelor.*
9. **Părțile străine în commit-uri?** *Recomandarea: nu, ca la versiuni.*
10. **Ramurile în interfață până la S4**: doar `main` și `wip/*`? *Recomandarea: da; ramuri libere abia cu îmbinarea.*
11. **Numele de gazdă**: `cde.lapinbim.com` sau `atrium.lapinbim.com`? *Recomandarea: `cde.lapinbim.com` (spune ce face; Atrium e numele aplicației).*
12. **Conturile**: Atrium Server autogăzduit își ține conturile lui, iar găzduirea premium folosește conturile platformei (`account.lapinbim.com`, Google, Microsoft)? *Recomandarea: da, prin `IIdentityProvider`.*
13. **Cloudflare vede traficul în clar** (TLS-ul se termină la el). E acceptabil pentru test? *Recomandarea: da, pentru test, cu proiectele firmei. Pentru clienți se hotărăște înainte de S5: Cloudflare trecut ca împuternicit, sau TLS direct pe server pentru `cde`.*
14. **Codul lui Lapin portat în Atrium** (parserul, §9) sub MIT-0? *Recomandarea: da, pentru fișierele scrise doar de tine; cu notița MIT unde există alți contribuitori.*
15. **Nicio portare în Rust acum**, doar praguri de dovadă (§9, §10)? *Recomandarea: da.*
16. **Randarea: întâi măsurarea R0, apoi Blender (S6 din `plan-render.md`) înaintea unui urmăritor Rust?** *Recomandarea: da.*
17. **Ordinea serviciilor în cloud**: planul premium a ales serverul de sesiune comună ca primul serviciu. Testul CDE de pe Hetzner e o instanță deschisă, autogăzduită de noi, deci nu schimbă ordinea. Rămâne așa? *Recomandarea: da; găzduirea CDE premium vine cu S5.*
