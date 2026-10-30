# Formatul `.lapin` în lapin-pro: Arrow / Parquet sau bază de date graf (analiză, 23.10.2026)

**Decis:** formatul `.lapin` se păstrează.
**Ales (24.10.2026):** recomandarea de mai jos. Ce e deja făcut e la final, în „Stare”.

## Ce trebuie să poată formatul

1. **Să țină intenția, nu rezultatul:** parametri, tipuri, relații IFC, GlobalId-uri stabile. Geometria e derivată. Principiile din `Lapin/docs/format-lapin-nativ.md` (14.10) rămân valabile.
2. **Deschidere și salvare rapide pe modele mari.** La salvare se rescrie doar ce s-a schimbat, iar recuperarea după o cădere se face din jurnal.
3. **Să supraviețuiască edițiilor.** Un proiect deschis într-o ediție fără un modul își păstrează elementele acelui modul, neatinse, ca regula 4 din BubbleBIM Pro.
4. **Să citească proiectele vechi:** `.lapin` v2 (IFC + `overlay.json` + `native/*.arrow`).
5. **Să se potrivească cu modelul Unity / Unreal:** aplicație desktop, cu un cloud pentru conturi, actualizări și biblioteci. Mai târziu și sincronizare, versiuni și lucru în echipă.

## Ce fac Unity și Unreal

| | Sursa (se salvează) | Derivatul (se regenerează) | Cloud |
|---|---|---|---|
| Unity | asset-uri text (YAML) + `.meta` cu GUID stabil | `Library/` (cache, se poate șterge) | Asset Store: pachete de asset-uri; Unity Version Control |
| Unreal | `.uasset` / `.umap`; *One File Per Actor* (un fișier pe obiect, pentru merge și sincronizare) | Derived Data Cache, local sau partajat | Fab / Marketplace: pachete; plugin-uri ca module separate |

Lecțiile pentru Lapin:
- **identitate stabilă** pe obiect (la noi, GlobalId);
- **sursă separată de cache;**
- **bucăți mici** care se pot sincroniza și compara;
- **bibliotecile sunt pachete în același format,** nu un alt sistem.

## Variantele

### A. Bază de date graf (Neo4j, Memgraph, Kùzu…) ca fișier de proiect

- **Pentru:**
  - IFC e un graf (entități + relații obiectivate), iar propagarea (îmbinări, goluri, gazde) e o traversare de graf.
- **Împotrivă:**
  - **Nu există una încorporabilă, MIT, vie, pentru .NET.**
    - Neo4j e server pe JVM, GPLv3 / comercial.
    - Memgraph e server.
    - Kùzu (embedded, MIT) a fost arhivat la 10.10.2025, după achiziția de către Apple, și nu mai primește corecturi.
  - Un fișier de bază de date e opac și legat de versiunea motorului. Nu se poate trimite pe e-mail cu încredere, nu se poate compara, nu se poate sincroniza pe bucăți și nu se poate repara de mână.
  - Graful îl avem oricum **în memorie**: `Lapin.Ifc.Model` în C#, ca `GraphStore` în BubbleBIM. Interogările de dependență au loc acolo, în microsecunde, fără un limbaj de interogare.
- **Unde ar avea sens:** în cloud, mai târziu, pentru interogări peste multe proiecte (portofoliu, biblioteci). Acolo e o decizie de server, nu de format.

### B. Arrow IPC / Parquet peste tot

- **Pentru:**
  - Arrow IPC se citește fără decodare și e deja decizia din 14.10 pentru tabelele de lucru.
  - Biblioteca .NET (`Apache.Arrow`) e Apache-2.0.
  - E același format bun pentru mesh-urile trimise interfeței (riscul „mesh-uri JSON” din `plan.md`).
- **Împotrivă:**
  - Tabelele sunt imutabile, deci o editare mică rescrie tabelul.
  - Parametrii imbricați ajungeau în coloane JSON.
  - Parquet e compact, dar trebuie decodat la deschidere, deci e bun pentru analiză și transfer, nu pentru lucru.

### C. SQLite ca fișier de aplicație

- **Pentru:**
  - salvare tranzacțională și incrementală;
  - rezistență la căderi (WAL);
  - încărcare parțială;
  - un singur fișier.
- **Împotrivă:**
  - nu se poate adresa după conținut, deci sincronizarea cu cloud-ul trebuie construită separat;
  - nu păstrează forma pachetului v2 (zip + manifest);
  - bibliotecile și modulele ar avea alt format decât proiectul.
- **Rămâne rezerva**, dacă salvarea pe chunk-uri se dovedește prea lentă.

## Recomandarea: graf în memorie, pachet pe bucăți adresate după conținut, Arrow pentru tabele, Parquet pentru analiză

```
.lapin  (zip, formatul 3; citește și formatul 2)
manifest.json                      versiunea formatului, a schemei, a kernelului; fiecare parte cu SHA-256 și mărime;
                                   modulele care au scris fiecare parte
project/…                          document, vederi, planșe, desene, standarde, cataloage: JSON, ca în formatul 2
model/<modul>/<clasă>/<etaj>.arrow intenția: un rând pe element (GlobalId, tip, etaj, parametri tipizați ca struct/list Arrow),
                                   împărțit pe modul × clasă × etaj
model/types.arrow, materials.arrow, relations.arrow, properties.arrow
model/opaque/<GlobalId>.ifc        geometrie IFC păstrată exact (elemente importate fără intenție), reemisă identic
journal/ops.jsonl                  operațiile de la ultima compactare: recuperare acum, lucru în echipă mai târziu
derived/<hash kernel+parametri>/   cache de mesh (binar, Arrow), se poate șterge
versions/index.json                versiunile proiectului: fiecare un manifest al ei, cu număr, nume, notă, moment și revizii (docs/versions.md)
versions/blobs/<sha256>            o parte numită de o versiune pe care proiectul viu nu o mai are, o singură dată
issues/index.json                  emiterile și fișierele lor, de citit fără Lapin (sursa e înregistrarea LapinIssue din model)
issues/blobs/<sha256>              un fișier făcut de o emitere (PDF, borderou, CSV, manifest, DXF, IFC la cerere), o singură dată
analytics/*.parquet                tabele pentru BI, doar la versiuni sau la export
```

- **Sursa de adevăr e graful din motor.**
  - Fișierul e doar forma lui pe disc.
  - Salvarea rescrie doar chunk-urile murdare (modul × clasă × etaj). Între salvări, jurnalul ține editările.
- **Proprietarul unei părți e un modul.**
  - O parte a unui modul care lipsește din ediție se copiază înapoi neatinsă.
  - Elementele modulului se afișează din `derived/`.
- **Adresarea după conținut** (SHA-256, care există deja în manifestul v2) dă de la sine:
  - **sincronizarea:** se urcă doar părțile cu hash nou;
  - **versiunile:** o versiune e un manifest, iar părțile neschimbate sunt comune;
  - **diff-ul** pe element, după GlobalId.
- **Același format pentru cloud:**
  - **biblioteca** (`.lapinlib`) e un pachet cu doar `types`, `materials`, catalogul de tâmplărie, hașurile și standardele;
  - **ediția** e un pachet cu doar modulele ei (motor + UI); un proiect deschis într-o ediție fără un modul îi păstrează partea neatinsă.
- **Ce se schimbă față de 14.10:**
  - store-ul nu mai e în TypeScript, ci în C#, în motor;
  - parametrii ca struct-uri Arrow tipizate înlocuiesc coloanele JSON;
  - jurnalul ține operații, nu doar ultima stare, pentru că lucrul în echipă le cere.

## Formatul 2 (proiectele vechi)

La deschidere, modelul se citește din IFC-ul compactat, iar `overlay.json` se aplică peste el. Unde există, tabelele `native/*.arrow` dau parametrii direct. Proiectul se salvează apoi în formatul 3.
Teste-specificație: `package-io.test.ts`, `project-file*.test.ts`, `versions.test.ts`, `native-tables.test.ts`.

## Stare (24.10.2026)

**Făcut**, în `engine/src/Platform/Lapin.Package`, cu teste în `PackageTests`:
- zip-ul formatului 3, cu `manifest.json` (format, versiune, produs, modulele cu versiunea schemei, fiecare parte cu SHA-256 și mărime, clasa → modulul care a scris-o);
- `project/document.json`;
- `model/<modul>/<clasă>/<etaj>.arrow`: un rând pe entitate, cu atributele în coloane tipizate (Float64, Utf8, Boolean, listă de Float64) și restul în JSON, marcat în metadatele câmpului;
- `model/relations.arrow`;
- ordinea de creare, păstrată pe fiecare rând;
- salvarea printr-un fișier temporar, apoi înlocuirea țintei;
- refuzul unei părți deteriorate (hash greșit) și al unui proiect dintr-un Lapin mai nou;
- o ediție fără un modul păstrează elementele lui și le scrie înapoi neatinse.

**Făcut și jurnalul de recuperare** (24.10.2026), dar în afara pachetului: `<app data>/recovery/*.jsonl`, cu fiecare tranzacție, undo și redo, scrise pe disc imediat (`Lapin.Host/Recovery.cs`, teste în `RecoveryTests`).

**Făcut și versiunile** (26.10.2026, `docs/versions.md`): `versions/index.json` ține fiecare versiune ca manifest; la salvare se scriu doar blob-urile părților pe care proiectul viu nu le mai are (adresare după conținut, cum prevedea analiza); restaurare într-un pas de undo, comparare element cu element și parte cu parte, export ca pachet de sine stătător (`PackageVersions`, `ModelCompare`, `VersionTests`).

**Făcute și emiterile în fișier** (31.10.2026, decizia 185, `docs/plan-cde.md`): `issues/blobs/<sha256>` ține fișierele emiterilor, adresate după conținut ca blob-urile versiunilor (aceiași octeți o singură dată, oricâte emiteri îi numesc), iar `issues/index.json` le listează pentru cine deschide zip-ul fără Lapin. Ce se scrie la salvare: hash-urile pe care înregistrările `LapinIssue` din model le țin (`stored: true`); un fișier lăsat (Delete stored files) sau o emitere ștearsă ies la următoarea salvare. Formatul rămâne 3: un fișier mai vechi, fără `issues/`, se deschide ca înainte, iar un Lapin mai vechi ignoră folderul. Blob-urile se citesc la cerere din pachetul din care s-a deschis proiectul (`PackageIssues`), cele noi stau în memorie până la salvare (teste în `CdeStoreTests`).

**Rămâne:**
- cache-ul `derived/` (geometria afișată pentru modulele lipsă);
- salvarea incrementală, care rescrie doar bucățile murdare;
- `analytics/*.parquet`;
- ~~citirea formatului 2~~ făcută la M6 (24.10.2026): `LegacyPackage` citește tabelele native, `LegacyImport` reconstruiește proiectul prin comenzi;
- bibliotecile `.lapinlib`, la M8.
