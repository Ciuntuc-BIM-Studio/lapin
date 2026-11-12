# Lapin Extensions: plan

Hotărârea utilizatorului (12.11.2026): un magazin de extensii, **Lapin Extensions**, cu adresa viitoare store.lapinbim.com. Etapa 0 e făcută (decizia 499); etapele 1 și 2 sunt planul de mai jos. Extensiile vin în patru feluri, în ordinea asta:

1. **Pachete de conținut** (fără cod): mobilier (`.lapinlib`), șabloane de proiect (`.lapin`), profile (`.lapinprofiles`), hașuri (`.pat`), cartușe (`.json` al cartușelor), liste de prețuri (`.lapinprices`), tipuri de pereți, uși, ferestre (în etapa 0 printr-un șablon, §4). **Primele.**
2. **Scripturi și unelte Python**, după etapa 4 a planului Python (`docs/plan-python-scripting.md`, decizia 489).
3. **Plug-in-uri de interfață în JS**, mai târziu, pe contractul `ui/src/platform/module.ts` și punctele de înlocuire (`docs/overrides.md`).
4. **Module C#** pentru parteneri, semnate (contractul motorului, `module.json`).

## 1. Hotărârile (12.11.2026)

- **Etapa 0, acum, fără server nou:** un depozit GitHub public `lapin-extensions` cu un index JSON construit din câte un manifest pe extensie (`extensions/<id>/manifest.json`); autorii își adaugă extensia prin pull request; fișierul pachetului stă în release-ul GitHub al autorului, cu SHA-256-ul lui în manifest.
- **În Lapin:** Fișier → Extensii…, cu **Răsfoiește** (căutare, categorii, detalii, licență, autor, versiuni compatibile) și **Instalate**; Instalează (descarcă, verifică SHA-256, pune în biblioteca potrivită), Actualizează, Dezinstalează. Merge pe desktop și în browser (în browser doar pachete de conținut).
- **Manifestul:** id, nume, versiune (semver), autor, licență (**SPDX, obligatorie, arătată utilizatorului**; orice licență e acceptată, cele permisive sunt marcate așa), intervalul de versiuni Lapin, fel, permisiuni cerute, conținut, sha256, pagină, subiect pe forum.
- **Verificarea:** automată pentru conținut, în CI-ul depozitului de catalog (un GitHub Action care validează PR-urile, Node pur, fără dependențe): schema manifestului și schema datelor pachetului. **Codul** (scripturi, mai târziu JS și C#) e citit de Claude, cu aprobarea finală a utilizatorului.
- **0 % comision** în prima fază; extensiile plătite mai târziu, printr-un *merchant of record* (etapa 2); acolo se vor vinde și modulele premium Lapin (mep-pro, urban, rail, planning-pro).
- **Etapa 1, mai târziu:** store.lapinbim.com, conturi prin SSO-ul Discourse al forumului (forum.lapinbim.com), panoul autorului, pachete semnate de Lapin (ca cheia updater-ului).
- **Primele extensii, ale noastre, ca exemple:** un pachet de mobilier, un șablon de proiect românesc, un script Python (manifest + script; se instalează, dar rulează doar când există consola Python, marcat așa).

## 2. Arhitectura (etapa 0)

```
lapin-extensions (GitHub, public)          lapinbim.com                         Lapin
  extensions/<id>/manifest.json   ──CI──▶  /extensions/index.json      ◀──────  Fișier → Extensii… (index)
  tools/validate.mjs, build-index.mjs      /extensions/files/<id>/<v>/… (copii, pentru browser)
release-ul autorului (GitHub)  ◀──────────────────────────────────────────────  pachetul (desktop: prin motor)
```

- **Indexul**: `{format: "lapin-extensions-index", version: 1, generated, extensions: [manifest…]}`; o intrare cu manifest greșit e lăsată afară (se scrie în consolă), nu tot indexul. Adresa: cea scrisă în dialog (Catalog…, `lapin.extensions.index`), altfel `LAPIN_EXTENSIONS_INDEX` dat motorului desktop (un index local în dezvoltare), altfel `https://lapinbim.com/extensions/index.json` (**încă nepublicat**).
- **Descărcarea**: pe desktop prin motor (`extensions.fetch {url}` în `EngineHub`, în afara cozii de apeluri, deci o descărcare nu ține pe loc editările): webview-ul Tauri are voie doar la `127.0.0.1` (CSP), iar fișierele release-urilor GitHub nu răspund browserelor (fără CORS). Doar https oriunde, http doar pe acest calculator, fișiere locale după cale sau `file://`; cel mult 64 MB; oaspeții unei sesiuni partajate sunt refuzați. În browser, `fetch`, iar pachetele din copiile de pe lapinbim.com (`package.mirror`, puse de `build-index.mjs --mirror` la publicare).
- **Verificarea în Lapin**: înainte de orice, versiunea Lapin în intervalul manifestului, modulul felului prezent în ediție, codul doar pe desktop; apoi mărimea și SHA-256-ul exact ca în manifest (WebCrypto), apoi fișierul citit și verificat ca fișier al felului lui (`packs.ts`), apoi importatorul existent. Un pachet greșit nu atinge nimic (nici versiunea instalată, la o actualizare).
- **Instalatoarele, pe fel** (`addContentInstaller`, înregistrate de modulul care are biblioteca, ca la „Salvează ca șablon”): mobilier → Mobilierul meu (`furnitureLibrary.import` / IndexedDB în browser); șablon → șabloanele de proiect (`templates.import` / IndexedDB); profile → bibliotecile de profile ale calculatorului; hașuri → Hașurile mele; cartușe → cartușele calculatorului; liste de prețuri → bibliotecile de prețuri ale calculatorului; scripturi → păstrate ca fișiere (IndexedDB `lapin-extensions`, pe care consola le va citi), **niciodată rulate**. Un element cu același nume deja în bibliotecă e înlocuit doar dacă spune utilizatorul.
- **Ce e instalat** (`lapin.extensions`, cu celelalte setări `lapin.*`, deci și în copia desktopului `settings.json`): manifestul instalat și **chitanța** (ce a adăugat: id-urile mobilierului, id-ul șablonului, numele bibliotecii). Dezinstalarea scoate doar ce e pe chitanță; ce era deja în bibliotecă la fel (sărit la import) rămâne, iar ce s-a pus deja în proiecte rămâne. Actualizarea = descarcă și verifică versiunea nouă, apoi dezinstalează vechea, apoi instalează noua.
- **Nicio dependență nouă**, niciun cod din extensii rulat, motorul schimbat doar în gazdă (`extensions.fetch`, `extensions.source`, legătura `extensionPage` pentru pagini https).

## 3. Manifestul (schema 1)

Schema JSON e în depozitul catalogului (`schema/manifest.schema.json`); regulile sunt scrise de două ori, identic: `ui/src/platform/extensions/manifest.ts` (Lapin) și `tools/lib.mjs` (CI).

| Câmp | Obligatoriu | Ce e |
|---|---|---|
| `schema` | da | `1` |
| `id` | da | litere mici, cifre, `.` și `-`, max. 64; numele folderului; un domeniu inversat sau numele GitHub întâi (`ro.ciuntucbimstudio.mobilier-living`) |
| `name`, `summary` | da | max. 80 / 200 de caractere |
| `description` | nu | text |
| `version` | da | semver; crește la fiecare schimbare (verificat de CI față de `main`) |
| `author` | da | `{name, github?, url?, email?}`; `github` e contul care are voie să schimbe extensia |
| `license` | da | expresie SPDX (`MIT`, `CC-BY-4.0`, `Apache-2.0 OR MIT`); arătată, „permisivă” sau „citește-i condițiile” |
| `lapin` | da | intervalul de versiuni: `>=0.1.860`, `>=0.1.860 <0.3.0`, `^0.1.900`, `~1.2.0`, `0.1.x`, `*`, `||` |
| `kind` | da | `furniture`, `template`, `profiles`, `hatches`, `titleBlock`, `priceList`, `script` |
| `tags`, `languages` | nu | max. 12 etichete; coduri ISO 639-1 |
| `permissions` | nu | doar cod: `model.read`, `model.write`, `files.read`, `files.write`, `network`, `process`; un pachet de conținut nu cere nimic |
| `requires` | nu | module în plus față de al felului (un script: `scripting`) |
| `contents` | da | `{file, items?}`: numele fișierului (extensia felului) și ce conține, arătat înainte de instalare |
| `package` | da | `{url (https, release-ul), sha256, size (≤ 64 MB), mirror? (pus la publicare)}` |
| `homepage`, `forum` | nu | https |
| `indicative` | la prețuri | `true` obligatoriu pentru o listă de prețuri (prețurile și normele sunt ale autorului, orientative) |

Fișierele pachetelor: `.lapinlib` (`lapin-furniture-library` v1, decizia 476), `.lapin` (formatul 3, zip), `.lapinprofiles` (`lapin-profiles` v1), `.pat` (formatul AutoCAD), cartușul (`lapin.titleBlock` v1, Cartușe → Exportă), `.lapinprices` (**nou**: `{format: "lapin-price-list", version: 1, name, currency, indicative: true, rows}`, rândurile ca în biblioteca de prețuri), `.py` (cu antetul `# lapin: name=…, version=…`).

## 4. Etapele

| Etapa | Ce aduce | Stare |
|---|---|---|
| **0. Catalogul pe GitHub** | depozitul `lapin-extensions` (README, schema, manifestele, `validate.mjs`, `build-index.mjs`, `make-examples.mjs`, acțiunea de validare); Fișier → Extensii… (Răsfoiește, Instalate, Instalează, Actualizează, Dezinstalează, adresa catalogului); instalatoarele pe fel; cele trei exemple | **făcută** (decizia 499); de publicat (§8) |
| **0.1** | tipuri de pereți/uși/ferestre ca fel separat (`types`, adăugate în proiectul deschis dintr-un `.lapin`: o comandă nouă în motor, `types.importFrom`, un pas de undo); exportul unei liste de prețuri ca `.lapinprices`; capturi de ecran în manifest (`screenshot.webp` în folder) | de hotărât |
| **1. store.lapinbim.com** | un site (static + API mic) peste același index; conturi prin SSO-ul Discourse (DiscourseConnect, forum.lapinbim.com); panoul autorului (încarcă pachetul, vede descărcările, răspunde la recenzii); recenzii și note; numărul de descărcări; **pachete semnate de Lapin** (o cheie Ed25519 a catalogului, ca updater-ul: Lapin verifică semnătura indexului și a fiecărui pachet, deci și o copie a indexului e de încredere) | mai târziu |
| **2. Extensii plătite** | un *merchant of record* (Paddle, Lemon Squeezy sau FastSpring: TVA-ul UE și facturile le face el), licențe pe utilizator legate de contul forumului, descărcare după cumpărare, comision de stabilit; modulele premium Lapin (mep-pro, urban, rail, planning-pro) vândute tot aici, ca module C# semnate; felurile 3 (JS) și 4 (C#) | mai târziu |

## 5. Siguranța și permisiunile

- **Conținutul nu rulează niciodată.** Un pachet de conținut e citit de importatorii existenți ai Lapin, care verifică deja fiecare intrare (motorul verifică mobilierul, șabloanele trec prin `TemplateLibrary`). Niciun HTML, niciun JS, nicio formulă evaluată.
- **Integritatea**: SHA-256 și mărimea din manifest, verificate în Lapin și în CI; o schimbare a fișierului din release după aprobare face instalarea să eșueze (și CI-ul de pe `main` să pice). În etapa 1, semnătura Lapin peste index.
- **Scripturile** (felul 2): pe desktop un script poate orice poate utilizatorul (`plan-python-scripting.md` §7); permisiunile din manifest sunt **declarate și arătate**, nu impuse, până la plug-in-urile JS (unde interfața le va impune, cu un `iframe` izolat și doar comenzile cerute). Un script instalat nu rulează la instalare, nici la deschiderea unui proiect; rularea va cere încrederea, ca scripturile proiectului. În browser nu se instalează scripturi.
- **Oaspeții** unei sesiuni partajate nu pot citi nimic prin gazdă (`extensions.*` sunt ale gazdei).
- **Revizia**: CI-ul verifică schema, fișierul și versiunea; o extensie existentă e schimbată doar de autorul ei (`author.github` = autorul PR-ului) sau de un maintainer; codul e citit de Claude și aprobat de utilizator înainte de merge.
- **Retragerea**: o extensie scoasă din index nu mai apare; cele instalate rămân, iar Instalate arată că nu mai e în catalog (etapa 1: o listă de pachete revocate, citită la pornire, care le marchează).

## 6. De făcut juridic

Operatorul catalogului: **Ciuntuc BIM Studio SRL** (CUI RO51725860, J2025031471009, Huși, VS).

1. **Termenii autorilor** (contribuția la catalog): autorul garantează că are drepturile asupra conținutului (mobilier, modele, texturi, imagini, prețuri), că licența aleasă îi aparține; acordă catalogului dreptul de a lista, copia (mirror) și afișa; răspunde pentru conținutul lui; acceptă retragerea la încălcări.
2. **Termenii utilizatorilor**: extensiile sunt ale autorilor, sub licențele lor, fără garanție din partea Ciuntuc BIM Studio; prețurile și normele sunt orientative; scripturile rulează pe răspunderea utilizatorului.
3. **Notificare și retragere** (takedown, DSA): o adresă de contact (ionut@ciuntucbimstudio.ro sau o adresă dedicată), procedura (primire, retragere provizorie, răspunsul autorului), termene; un formular pe forum sau un issue template.
4. **Confidențialitate / GDPR**: etapa 0 nu colectează date (indexul și pachetele sunt fișiere statice; jurnalele serverului nginx — de stabilit cât se păstrează); etapa 1: conturi prin SSO-ul forumului, statistici de descărcare (anonime), recenzii — o politică de confidențialitate a magazinului, temeiul (contract/interes legitim), păstrarea, drepturile.
5. **Etapa 2**: contractul cu merchant-of-record-ul, termenii de vânzare, retragerea din contract pentru conținut digital, facturarea (MoR-ul e vânzătorul către cumpărător), contractul de distribuție cu autorii (comisionul).
6. **Mărcile**: „Lapin” și „Lapin Extensions” — de verificat înregistrarea mărcii (OSIM / EUIPO) înainte de etapa 1.

## 7. Exemplele (în `lapin-extensions`)

| id | fel | licență | ce e |
|---|---|---|---|
| `ro.ciuntucbimstudio.mobilier-living` | furniture | CC-BY-4.0 | șase tipuri de living făcute cu configuratorul (comodă, comodă TV, bibliotecă, canapea, fotoliu, măsuță) |
| `ro.ciuntucbimstudio.sablon-romania` | template | CC0-1.0 | niveluri Fundații / Parter / Etaj 1 / Mansardă și parametrii proiectului ceruți de documentația de autorizare (regim de înălțime, suprafață teren, POT, CUT, categoria și clasa de importanță) |
| `ro.ciuntucbimstudio.pereti-perimetrali` | script | MIT | patru pereți în jurul unui dreptunghi, un pas de undo, pe API-ul `lapinbim` planificat; se instalează, rulează când există consola |

Fișierele lor se fac cu `node tools/make-examples.mjs --lapin <lapin-pro>` (un motor Lapin pornit de script), în `dist/`, cu `SHA256SUMS.txt`; manifestele primesc mărimea și SHA-256-ul.

## 8. Ce rămâne de făcut pentru publicare

1. Crearea depozitului public `Ciuntuc-BIM-Studio/lapin-extensions` pe GitHub și împingerea folderului local (fără `dist/`).
2. Câte un release pe exemplu (`mobilier-living-v1.0.0`, `sablon-romania-v1.0.0`, `pereti-perimetrali-v1.0.0`) cu fișierul din `dist/<id>/`, exact cel al cărui SHA-256 e în manifest (`SHA256SUMS.txt`).
3. Publicarea indexului pe lapinbim.com: `node tools/build-index.mjs --mirror https://lapinbim.com/extensions/files/`, apoi `index.json` și copiile fișierelor (verificate cu SHA-256) în `/extensions/` (un vhost pe Hetzner sau Cloudflare Pages; antetul `Access-Control-Allow-Origin` pentru ediția web). Automat la un push pe `main`, mai târziu.
4. Un subiect pe forum pentru catalog și câte unul pe exemplu (`forum` în manifeste).

## 9. Întrebări deschise

1. Licențe: acceptăm orice licență SPDX (și `LicenseRef-…` proprietare) sau doar cele permisive / Creative Commons? (Acum: orice, arătată; cele permisive marcate.)
2. Necomercialele (CC-BY-NC) sunt permise în catalog, cu avertismentul „citește-i condițiile”?
3. Tipurile de pereți/uși/ferestre: un fel separat (comandă nouă `types.importFrom`) sau rămân în șabloane?
4. Formatul `.lapinprices`: îl adăugăm și la exportul din Liste de prețuri…?
5. Unde stă indexul: Hetzner (lângă forum) sau Cloudflare Pages (lapinbim.com e pe Cloudflare)?
6. Copiile pachetelor (mirror) pentru browser: le facem pentru toate extensiile sau cerem autorilor alt loc cu CORS?
7. Cheia de semnare a catalogului (etapa 1): una nouă (recomandat) sau cea a updater-ului?
8. Pragul de mărime (64 MB) e bun pentru șabloane cu imagini și mobilier cu modele mesh?
9. Statistici de descărcare în etapa 0 (jurnalele nginx) sau abia în etapa 1?
