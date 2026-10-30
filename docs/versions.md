# Versiuni (Manage → Versions)

O versiune e proiectul așa cum era când a fost salvat ca versiune: numerotată 1, 2, 3… în proiect, cu nume, notă și moment. Versiunile stau **în fișierul `.lapin`**, nu alături: fișierul se trimite, se copiază, se arhivează cu tot istoricul lui. Specificația vine din vechiul Lapin (`versions.test.ts`, listat în `transfer.md`), rescrisă pe pachetul formatului 3.

## Cum stau în fișier

O versiune e **un manifest**: lista părților proiectului (`project/document.json`, `model/<modul>/<clasă>/<etaj>.arrow`, `model/relations.arrow`), fiecare cu SHA-256 și mărime, exact ca `manifest.json` al proiectului viu.

```
versions/index.json          versiunile: număr, nume, notă, momentele (UTC, ISO 8601), numărul de entități, manifestul, reviziile
versions/blobs/<sha256>      o parte numită de o versiune pe care proiectul viu nu o mai are (o singură dată, oricâte versiuni o împart)
issues/blobs/<sha256>        fișierele emiterilor (docs/plan-cde.md, decizia 185), după aceeași regulă; o emitere își salvează versiunea în același apel
```

Adresarea după conținut face economia de la sine: la fiecare salvare se scriu părțile vii, apoi, dintre părțile numite de versiuni, doar cele al căror hash nu e printre cele vii. O versiune tocmai salvată nu costă nimic (toate părțile ei sunt vii); după editări costă doar chunk-urile care s-au schimbat (de obicei o clasă pe un etaj și relațiile). Părțile pe care nu le mai numește nicio versiune dispar la următoarea salvare.

La citire (`PackageVersions.Of(path | bytes)`) se ia doar indexul; octeții unei părți se caută când e nevoie: `versions/blobs/<sha>`, altfel partea vie cu același hash. Sursa e fișierul din care s-a deschis proiectul, iar după fiecare salvare fișierul tocmai scris (în browser: octeții pachetului, ținuți în memorie).

## Ce se poate face

Dialogul Manage → Versions (și File → Save version…). Fiecare operație care schimbă lista **scrie fișierul proiectului**, ca Save (o versiune trăiește în fișier); pe desktop proiectul are nevoie întâi de un fișier (Save as), în browser fișierul vine înapoi ca octeți și se păstrează ca la salvare.

- **Save version** (`project.saveVersion {name?, note?}`): proiectul de acum, ca versiune nouă; fără nume, „Version n”.
- **Nume și notă** (`project.updateVersion {number, name?, note?}`), editabile în tabel.
- **Update to current** (`project.updateVersion {number, replace: true}`): versiunea devine proiectul de acum; ce ținea rămâne ca **revizie** a ei (rând „Earlier”, cu moment și notă), restaurabilă și exportabilă.
- **Restore** (`project.restoreVersion {number, revision?}`): proiectul adus la versiune **printr-un singur pas de undo**, nu prin reîncărcare: `ModelCompare.Apply` adaugă, înlocuiește și șterge entitățile după GlobalId, apoi așază relațiile; istoricul rămâne, proiectul e nesalvat.
- **Compare** (`project.compareVersions {a, b}`, fiecare un număr sau `"current"`): element cu element (adăugate, șterse, modificate, cu câmpurile care diferă), relațiile numărate, și parte cu parte (ce părți s-au schimbat, pe zone: modulul, `project`, `model`). În dialog listele sunt grupate pe clasă; click selectează elementul, dacă proiectul îl mai are.
- **Export…** (`project.exportVersion {number, revision?, path?}`): versiunea ca pachet de sine stătător, fără versiuni (`<proiect> v<n>.lapin`).
- **Open separately** (desktop): `project.exportVersion {temp: true}` scrie pachetul în folderul temporar, apoi comanda Tauri `open_separately` pornește altă instanță a aplicației cu el.
- **Delete** (`project.deleteVersion {number}`).
- `project.versions` listează: număr, nume, notă, `at`, `updatedAt`, entități, mărime, părți, aplicația, reviziile.

## Unde e codul

- `Lapin.Package/PackageVersions.cs`: `PackageVersion`, `PackageRevision`, `PackageVersionIndex`; `PackageVersions` (imutabil: `Add`, `Update`, `Remove`, `Blobs`, `Content`, `Standalone`, `From`).
- `Lapin.Package/LapinPackage.cs`: `Build` (proiectul ca părți, fără scriere) și `Write(build, versions)` care scrie indexul și blob-urile lipsă.
- `Lapin.Package/ModelCompare.cs`: `Compare`, `Parts`, `Apply`.
- `Lapin.Protocol/EngineDispatcher.cs`: `ProjectFiles` ține versiunile proiectului deschis (citite cu fișierul, purtate în fiecare salvare, inclusiv Save as) și face operațiile; metodele `project.*Version*`.
- `ui/src/engine/files.ts` (`writeVersions`, `exportVersion`, `openVersionSeparately`), `ui/src/modules/core/VersionsDialog.tsx`.
- Teste: `VersionTests` (numerotare și partajarea părților, revizia, pachetul de sine stătător, compararea, restaurarea cu undo, Save as și ștergerea, octeții browserului, protocolul).

## Rămase

- Autor pe versiune (când există conturi).
- Compararea vizuală (colorarea în vederi a ce s-a adăugat, șters, modificat); acum e lista din dialog.
- Un proiect deschis dintr-o versiune în altă instanță nu știe din ce fișier vine: e pachet de sine stătător.
- Sincronizarea și lucrul în echipă peste aceleași manifeste (planul: „mai târziu, cu cloud-ul”).
