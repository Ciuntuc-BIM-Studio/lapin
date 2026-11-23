# Plan: conectorul Speckle

Hotărârea utilizatorului (23.11.2026, decizia 804): Lapin primește un conector Speckle construit pe **SDK-ul .NET oficial al Speckle** (`Speckle.Sdk`, `Speckle.Objects`), ca excepție notată de la principiul „formatele de schimb scrise după specificația publică, fără dependențe”. Speckle e un protocol de server (GraphQL plus încărcarea obiectelor), nu un format de fișier, iar reproducerea serializării și a identificatorilor lui ar fi fragilă. Modulul e **deschis (MIT)**: aduce utilizatori, nu e premium.

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

### S2. Primirea (Speckle → Lapin), ~1 săptămână

- Fișier → Speckle → Primește…: o versiune intră ca **referințe** (suprafețe) pe etajele potrivite, grupate după colecțiile ei, cu proprietățile păstrate.
- O primire nouă a aceluiași model **actualizează** referințele după `applicationId`, păstrând GlobalId-urile lor.
- Recunoașterea ca elemente native (pereți, plăci) rămâne după aceeași regulă ca la IFC (`RecognitionFidelity`): ce diferă cu mai mult de 1 mm sau 0,5 % rămâne referință.

### S3. Mai târziu

- Trimitere automată la salvare (opțională, pe proiect).
- Planșele și desenele ca obiecte 2D.
- Importul unui model Speckle pornit din alte programe direct ca proiect nou.

## Întrebări deschise

1. ~~S1 pornește acum sau după etapa de simboluri MEP?~~ **După** etapa de simboluri MEP (utilizatorul, 23.11.2026).
2. ~~Primul server de test?~~ **Un Speckle local, în container, pentru dezvoltare**; niciun Speckle găzduit de noi; Atrium rămâne CDE-ul, separat (utilizatorul, 23.11.2026).
