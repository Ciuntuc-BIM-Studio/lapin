# Lapin Extensions: plan

Hotărârea utilizatorului (12.11.2026): un magazin de extensii, **Lapin Extensions**, cu adresa viitoare store.lapinbim.com. Etapa 0 e făcută (decizia 499); etapa 1 e construită și **publicată pe 12.11.2026** (decizia 504, §10): store.lapinbim.com live, containerul `lapin-store`, DiscourseConnect pornit pe forum, catalogul **semnat** cu cheia magazinului (`2daeb4d5dc6eae92`, în `STORE_KEYS` din același zi); pașii și întreținerea în `deploy/store/README.md`; etapa 2 e hotărâtă în §11. Extensiile vin în patru feluri, în ordinea asta:

1. **Pachete de conținut** (fără cod): mobilier (`.lapinlib`), șabloane de proiect (`.lapin`), profile (`.lapinprofiles`), hașuri (`.pat`), cartușe (`.json` al cartușelor), liste de prețuri (`.lapinprices`), tipuri de pereți, plăci, uși, ferestre, stâlpi, grinzi, scări (`.lapintypes`, felul `types`, §9.1). **Primele.**
2. **Scripturi și unelte Python**, după etapa 4 a planului Python (`docs/plan-python-scripting.md`, decizia 489).
3. **Plug-in-uri de interfață în JS**, mai târziu, pe contractul `ui/src/platform/module.ts` și punctele de înlocuire (`docs/overrides.md`).
4. **Module C#** pentru parteneri, semnate (contractul motorului, `module.json`).

## 1. Hotărârile (12.11.2026)

- **Etapa 0, acum, fără server nou:** un depozit GitHub public `lapin-extensions` cu un index JSON construit din câte un manifest pe extensie (`extensions/<id>/manifest.json`); autorii își adaugă extensia prin pull request; fișierul pachetului stă în release-ul GitHub al autorului, cu SHA-256-ul lui în manifest.
- **În Lapin:** Fișier → Extensii…, cu **Răsfoiește** (căutare, categorii, detalii, licență, autor, versiuni compatibile) și **Instalate**; Instalează (descarcă, verifică SHA-256, pune în biblioteca potrivită), Actualizează, Dezinstalează. Merge pe desktop și în browser (în browser doar pachete de conținut).
- **Manifestul:** id, nume, versiune (semver), autor, licență (**SPDX, obligatorie, arătată utilizatorului**; orice licență e acceptată, cele permisive sunt marcate așa), intervalul de versiuni Lapin, fel, permisiuni cerute, conținut, sha256, pagină, subiect pe forum.
- **Verificarea:** automată pentru conținut, în CI-ul depozitului de catalog (un GitHub Action care validează PR-urile, Node pur, fără dependențe): schema manifestului și schema datelor pachetului. **Codul** (scripturi, mai târziu JS și C#) e citit de Claude, cu aprobarea finală a utilizatorului.
- **0 % comision** în prima fază; extensiile plătite mai târziu, printr-un *merchant of record* (etapa 2); acolo se vor vinde și modulele premium Lapin (mep-pro, urban, rail, planning-pro).
- **Etapa 1, mai târziu:** store.lapinbim.com, conturi prin SSO-ul Discourse al forumului (forum.lapinbim.com), panoul autorului, pachete semnate de Lapin, cu o cheie nouă, separată de a updater-ului (§9.7); statisticile de descărcare (§9.9).
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
- **Instalatoarele, pe fel** (`addContentInstaller`, înregistrate de modulul care are biblioteca, ca la „Salvează ca șablon”): mobilier → Mobilierul meu (`furnitureLibrary.import` / IndexedDB în browser); șablon → șabloanele de proiect (`templates.import` / IndexedDB); profile → bibliotecile de profile ale calculatorului; hașuri → Hașurile mele; cartușe → cartușele calculatorului; liste de prețuri → bibliotecile de prețuri ale calculatorului; tipuri → păstrate pe calculator și aduse în proiectul deschis (`types.importFrom`, §9.1); scripturi → păstrate ca fișiere (IndexedDB `lapin-extensions`, pe care consola le va citi), **niciodată rulate**. Un element cu același nume deja în bibliotecă e înlocuit doar dacă spune utilizatorul.
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
| `screenshots` | nu | **etapa 1**: cel mult 6 capturi pentru pagina din magazin: fișiere din folderul extensiei (`screenshot.webp`, `screenshot-2.png`…, ≤ 2 MB) sau adrese https; Lapin nu le arată |
| `version` | da | semver; crește la fiecare schimbare (verificat de CI față de `main`) |
| `author` | da | `{name, github?, url?, email?, forum?}`; `github` e contul care are voie să schimbe extensia prin GitHub; `forum` numele de pe forum.lapinbim.com (pus de magazin, etapa 1) |
| `license` | da | expresie SPDX (`MIT`, `CC-BY-4.0`, `Apache-2.0 OR MIT`); arătată, „permisivă” sau „citește-i condițiile” |
| `lapin` | da | intervalul de versiuni: `>=0.1.860`, `>=0.1.860 <0.3.0`, `^0.1.900`, `~1.2.0`, `0.1.x`, `*`, `||` |
| `kind` | da | `furniture`, `template`, `types`, `profiles`, `hatches`, `titleBlock`, `priceList`, `script` |
| `tags`, `languages` | nu | max. 12 etichete; coduri ISO 639-1 |
| `permissions` | nu | doar cod: `model.read`, `model.write`, `files.read`, `files.write`, `network`, `process`; un pachet de conținut nu cere nimic |
| `requires` | nu | module în plus față de al felului (un script: `scripting`) |
| `contents` | da | `{file, items?}`: numele fișierului (extensia felului) și ce conține, arătat înainte de instalare |
| `package` | da | `{url (https: release-ul autorului sau adresa magazinului, store.lapinbim.com/packages/…), sha256, size (≤ 64 MB), mirror?, download?, signature?}`; ultimele trei puse la publicare: copia de pe lapinbim.com, adresa care numără descărcarea (store.lapinbim.com/api/download/<id>/<versiune>) și semnătura cheii magazinului (etapa 1) |
| `homepage`, `forum` | nu | https |
| `indicative` | la prețuri | `true` obligatoriu pentru o listă de prețuri (prețurile și normele sunt ale autorului, orientative) |

Fișierele pachetelor: `.lapinlib` (`lapin-furniture-library` v1, decizia 476), `.lapin` (formatul 3, zip), `.lapintypes` (`lapin-type-pack` v1, §9.1), `.lapinprofiles` (`lapin-profiles` v1), `.pat` (formatul AutoCAD), cartușul (`lapin.titleBlock` v1, Cartușe → Exportă), `.lapinprices` (**nou**: `{format: "lapin-price-list", version: 1, name, currency, indicative: true, rows}`, rândurile ca în biblioteca de prețuri), `.py` (cu antetul `# lapin: name=…, version=…`).

## 4. Etapele

| Etapa | Ce aduce | Stare |
|---|---|---|
| **0. Catalogul pe GitHub** | depozitul `lapin-extensions` (README, schema, manifestele, `validate.mjs`, `build-index.mjs`, `make-examples.mjs`, acțiunea de validare); Fișier → Extensii… (Răsfoiește, Instalate, Instalează, Actualizează, Dezinstalează, adresa catalogului); instalatoarele pe fel; cele trei exemple | **făcută** (decizia 499); de publicat (§8) |
| **0.1** | tipurile ca fel separat (`types`, un `.lapintypes` adus în proiectul deschis de `types.importFrom`, un pas de undo; Gestionare → Pachete de tipuri…); licențele necomerciale marcate; exportul unei liste de prețuri ca `.lapinprices` | **făcută** (decizia 499, răspunsurile din §9) |
| **0.2** | capturi de ecran în manifest (făcut în etapa 1: `screenshots`); un exemplu de pachet de tipuri în catalog (`make-examples.mjs`) | exemplul de făcut |
| **1. store.lapinbim.com** | site-ul static generat din catalog (`lapin-extensions/site/`); serviciul magazinului (`deploy/store/`): conturi prin DiscourseConnect (forum.lapinbim.com), panoul autorului (trimite o extensie sau o versiune nouă: formular + fișier + capturi → pull request în catalog; starea reviziei; descărcările), contorul anonim de descărcări, găzduirea catalogului (lapinbim.com/extensions/); **semnarea** (cheia Ed25519 a magazinului, separată de a updater-ului: indexul și fiecare pachet, verificate în Lapin); proiectele juridice (`lapin-extensions/legal/`); recenziile și notele rămân pentru mai târziu | **construită** (decizia 504, §10); de publicat de utilizator (`deploy/store/README.md`) |
| **2. Extensii plătite** | ale autorilor, vândute prin vânzătorul lor (§11); felurile 3 (JS) și 4 (C#) | hotărâtă (§11), de făcut |

## 5. Siguranța și permisiunile

- **Conținutul nu rulează niciodată.** Un pachet de conținut e citit de importatorii existenți ai Lapin, care verifică deja fiecare intrare (motorul verifică mobilierul, șabloanele trec prin `TemplateLibrary`). Niciun HTML, niciun JS, nicio formulă evaluată.
- **Integritatea**: SHA-256 și mărimea din manifest, verificate în Lapin și în CI; o schimbare a fișierului din release după aprobare face instalarea să eșueze (și CI-ul de pe `main` să pice). În etapa 1, semnătura Lapin peste index.
- **Scripturile** (felul 2): pe desktop un script poate orice poate utilizatorul (`plan-python-scripting.md` §7); permisiunile din manifest sunt **declarate și arătate**, nu impuse, până la plug-in-urile JS (unde interfața le va impune, cu un `iframe` izolat și doar comenzile cerute). Un script instalat nu rulează la instalare, nici la deschiderea unui proiect; rularea va cere încrederea, ca scripturile proiectului. În browser nu se instalează scripturi.
- **Oaspeții** unei sesiuni partajate nu pot citi nimic prin gazdă (`extensions.*` sunt ale gazdei).
- **Revizia**: CI-ul verifică schema, fișierul și versiunea; o extensie existentă e schimbată doar de autorul ei (`author.github` = autorul PR-ului) sau de un maintainer; codul e citit de Claude și aprobat de utilizator înainte de merge.
- **Retragerea**: o extensie scoasă din index nu mai apare; cele instalate rămân, iar Instalate arată că nu mai e în catalog (mai târziu: o listă de pachete revocate, citită la pornire, care le marchează).
- **Semnătura** (etapa 1, §10): indexul (`index.json.sig`) și fiecare pachet (`package.signature`) semnate de cheia magazinului; Lapin le verifică și, de la build-ul care are cheia, refuză catalogul Lapin nesemnat; un catalog al utilizatorului poate fi nesemnat, cu un avertisment.

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
3. Publicarea indexului pe lapinbim.com: `node tools/build-index.mjs --mirror https://lapinbim.com/extensions/files/`, apoi `index.json` și copiile fișierelor (verificate cu SHA-256) în `/extensions/` (un vhost pe Hetzner, lângă forum, §9.5; antetul `Access-Control-Allow-Origin` pentru ediția web; găzduirea se face separat). Automat la un push pe `main`, mai târziu.
4. Un subiect pe forum pentru catalog și câte unul pe exemplu (`forum` în manifeste).

## 9. Întrebările, cu răspunsurile utilizatorului (12.11.2026: toate recomandările)

1. **Licențe: orice expresie SPDX e acceptată**, și `LicenseRef-…` proprietare. Cele permisive sunt marcate „permisivă”, celelalte „citește-i condițiile” (ca până acum).
2. **Necomercialele sunt acceptate și marcate clar „Necomercială”** (Non-commercial), în lista din Răsfoiește și în detalii. Detectarea (`isNonCommercial`, aceeași în `manifest.ts` și în `tools/lib.mjs` din catalog): orice identificator SPDX cu `-NC` (CC-BY-NC-*, CC-BY-NC-SA-*, CC-BY-NC-ND-*, cu variantele lor naționale), plus `PolyForm-Noncommercial-1.0.0`, `NCGL-UK-2.0` și un `LicenseRef-…` care spune non-commercial. Expresia e evaluată: `MIT OR CC-BY-NC-4.0` se poate lua sub MIT, deci nu e marcată; `CC-BY-NC-4.0 AND MIT` e. `validate.mjs` scrie la fiecare extensie „permissive”, „read its terms” sau „non-commercial”.
3. **Tipurile sunt un fel separat, `types`, cu fișierul `.lapintypes`** (făcut, §9.1). Comanda motorului `types.importFrom` le aduce în proiectul deschis; Gestionare → Pachete de tipuri… le exportă și le importă.
4. **`.lapinprices` se exportă și din Liste de prețuri…**: butonul de export al fiecărei liste din biblioteca calculatorului scrie fișierul pe care îl citește instalatorul (`writePriceList`), mereu cu `indicative: true`.
5. **Indexul stă pe Hetzner**, lângă forum: un vhost nginx care servește `/extensions/index.json` și `/extensions/files/<id>/<versiune>/<fișier>` sub lapinbim.com (Cloudflare rămâne doar DNS-ul și proxy-ul), cu `Access-Control-Allow-Origin: *` pentru ediția web și `Cache-Control` scurt pe index (5 min), lung pe fișiere (sunt imuabile: o versiune nouă e alt drum). Publicarea: `build-index.mjs --mirror https://lapinbim.com/extensions/files/`, apoi index-ul și copiile verificate cu SHA-256, prin rsync, din CI-ul catalogului la un push pe `main` (o cheie de deploy doar pentru acel folder). **Găzduirea se face separat**; nimic pe server în această etapă.
6. **Fiecare pachet e copiat pe lapinbim.com** (mirror), pentru toate extensiile: ediția web citește copiile (`package.mirror`), desktopul release-ul autorului și, dacă acela nu răspunde, copia (de făcut la etapa 1). `build-index.mjs --mirror` există deja; copia se face doar după ce SHA-256-ul ei e cel din manifest. Autorii acceptă copierea prin termenii autorilor (§6.1).
7. **O cheie de semnare nouă pentru etapa 1, separată de cea a updater-ului**: Ed25519, ținută ca a updater-ului (fișier în afara depozitului, parola în keychain, niciodată în chat), cu semnătura indexului (`index.json.sig`) și a fiecărui pachet în index; Lapin are cheia publică încorporată și o poate roti (o listă de chei valabile). Updater-ul și catalogul nu-și împart cheia: o cheie a catalogului scăpată nu poate semna o actualizare a aplicației.
8. **Pragul de 64 MB rămâne**, pentru orice fel.
9. **Statisticile de descărcare vin în etapa 1**: un contor anonim pe API-ul magazinului (pe extensie și versiune, fără IP păstrat), arătat în panoul autorului; în etapa 0 nimic, iar jurnalele nginx nu se folosesc pentru statistici.

### 9.1 Pachetele de tipuri (`.lapintypes`, felul `types`)

**Formatul ales: JSON, nu un `.lapin` mic** — se citește și se verifică fără motor (CI-ul catalogului, Node pur), se vede ce conține, iar un tip adus nu poartă nimic din restul unui proiect (niveluri, vederi, setări).

```
{ format: "lapin-type-pack", version: 1, name?,
  types:     [{ref, class, name, predefinedType?, attrs, material?}],
  resources: [{ref, class, name, predefinedType?, attrs}] }
```

- `ref` e id-ul entității în proiectul din care a ieșit; orice text din `attrs` egal cu un `ref` arată spre acel element (un set de straturi spre materialele lui, un tip de grindă spre copia profilului). `material` e `ref`-ul a ce îi dă tipului IfcRelAssociatesMaterial (setul lui de straturi sau un material).
- **Exportul** (`types.export {ids, name?}` → `{text, file}`): doar tipuri (clasa descrisă ca `type` de modulul ei: pereți, plăci, uși, ferestre, stâlpi, grinzi, scări, pereți cortină, tavane, plinte…), cu tot ce le trebuie: seturile de straturi, materialele numite prin id sau prin nume sub o cheie care spune material (rama unei uși), copiile de profile și orice altă resursă din `attrs`; niciodată structura spațială sau elemente așezate. Atributele scrise de proiect pentru sine (`added`, `imported`) rămân afară.
- **Importul** (`types.importFrom {text, refs?}`, **un pas de undo**): tipurile alese (fără `refs`: toate) și doar ce le trebuie lor. Un material e al proiectului când are unul cu același nume (fără să-l schimbe), altfel e creat cum îl are pachetul (culoarea, aspectul, hașurile); o resursă identică cu una a proiectului (clasa, numele, atributele) e aceea (o copie de profil din aceeași bibliotecă), altfel e creată; **un tip identic** (clasa, numele, atributele, straturile) **e refolosit**, unul cu același nume dar diferit e adăugat lângă el ca „Nume (2)” (al proiectului nu se schimbă niciodată), oricare altul e creat. Id-urile sunt mereu noi. Un pachet greșit (alt format, o versiune mai nouă, o clasă care nu e tip în această ediție, structura spațială printre resurse) e refuzat întreg.
- `types.readPack {text}` spune ce tipuri are și care au deja un nume în proiect; `types.list` dă tipurile proiectului pentru export.
- **Instalatorul** (`typesInstaller`, înregistrat de core): pachetul e verificat (`readTypePack`), păstrat pe calculator (IndexedDB `lapin-type-packs`) și tipurile lui aduse în proiectul deschis; Instalate → Adaugă-i tipurile în acest proiect le aduce în altul. Dezinstalarea uită pachetul; tipurile deja în proiecte rămân.
- **Gestionare → Pachete de tipuri…**: tipurile proiectului bifate și exportate (Exportă tipuri…), un `.lapintypes` citit, tipurile lui alese și adăugate (Importă tipuri…).

## 10. Etapa 1: ce s-a construit (decizia 504, 12.11.2026)

Aprobată de utilizator la 12.11.2026. Codul și testele sunt făcute; nimic nu e publicat pe server, setările forumului nu sunt schimbate, nicio cheie sau token nu e făcut (pașii, în `deploy/store/README.md`).

- **Site-ul magazinului** (`lapin-extensions/site/build.mjs`, Node fără dependențe; `npm run site`): lista (căutare, filtre pe fel, merge și fără JavaScript), câte o pagină pe extensie la `/extension/<id>/` (descriere, capturi, versiunile din istoricul git al manifestului, licența marcată permisivă / citește-i condițiile / necomercială, versiunile Lapin, mărimea, SHA-256, autorul, pagina, subiectul pe forum, cum se instalează, descărcările numărate de magazin, „Semnat”), paginile autorilor, Publică, proiectele juridice; aspectul Lapin (tokenurile din `theme.css`, hârtie și grafit, după sistem sau ales); textele în `site/strings.mjs`, engleza întâi, gata pentru alte limbi (`--lang`). Tot textul din manifeste e escapat.
- **Legătura „Deschide în Lapin”**: nu e făcută. `lapin://` ar cere pluginul `tauri-plugin-deep-link` (și `single-instance` pe Windows și Linux), o dependență Rust nouă și înregistrarea schemei în pachetele fiecărei platforme: nu e ieftin acum. Pagina spune „În Lapin: Fișier → Extensii…, caută „<nume>”, apoi Instalează” și are un buton care copiază id-ul. De făcut cu un build desktop, separat.
- **Serviciul magazinului** (`deploy/store/`, modelul `deploy/feedback`: Node 22 fără dependențe, fișiere JSON cu blocare și scriere atomică, nu SQLite, containerul `lapin-store` pe `127.0.0.1:3465`): servește paginile, catalogul (`lapinbim.com/extensions/*` rutat aici de tunel) și fișierele încărcate de autori; conectarea prin **DiscourseConnect** (secretul comun din `store.env` pe server; se păstrează id-ul, numele și numele afișat de pe forum, niciodată e-mailul; sesiuni doar în memorie, 14 zile); **panoul autorului** (`/dashboard/`): o extensie nouă sau o versiune nouă (câmpurile manifestului, fișierul verificat cu `checkPackage` ca în CI și în Lapin, cel mult 6 capturi, termenii autorilor acceptați) → **un pull request** în `Ciuntuc-BIM-Studio/lapin-extensions` (ramură, manifest, capturi; tokenul GitHub din `store.env`), starea lui (deschis / verificările / acceptat / refuzat), retragerea, descărcările. **Fișierele stau pe lapinbim.com** (`store.lapinbim.com/packages/<id>/<v>/<fișier>` e `package.url`, servit doar ca descărcare), nu în release-uri GitHub: autorul nu are nevoie de cont GitHub, iar fișierul e exact cel verificat. Un id e al primului autor al cărui pull request e acceptat; doar el (sau un maintainer) îi trimite versiunile. **Descărcările**: `/api/download/<id>/<v>` numără (pe extensie, versiune, lună; nimic despre cine) și trimite la copie; Lapin încearcă întâi adresa asta, apoi fișierul autorului, apoi copia (§9.6, făcut). **Limite** pe vizitator și pe autor; curățenie zilnică (trimiterile refuzate sau retrase, șterse după 90 de zile). CLI: `list`, `stats`, `owner`, `forget` (ștergerea datelor cuiva, GDPR). Teste: `pnpm test:store` (forumul și GitHub simulați).
- **Semnarea**: cheia Ed25519 a magazinului, separată de a updater-ului, făcută o dată de `node scripts/store-key.mjs` (fișierul criptat `~/.lapin/store-key.pem`, parola în keychain `lapin-store-key`; încă nefăcută). `lapin-extensions/tools/publish.mjs` (pe Mac; cheia nu ajunge pe server) construiește indexul cu `mirror` și `download`, copiază pachetele după ce le verifică SHA-256-ul, semnează fiecare pachet (`package.signature`, peste `lapin-extension-package`, `1`, id, versiune, SHA-256 și mărime, câte unul pe rând) și indexul (`index.json.sig`), generează site-ul și, cu `--upload`, le trimite prin rsync. În Lapin (`ui/src/platform/extensions/signature.ts`, `keys.ts`): verificarea Ed25519 prin WebCrypto, sau prin verificatorul propriu (RFC 8032, BigInt) unde WebCrypto nu are Ed25519; cheile publice în `STORE_KEYS` (o listă: rotire); **cât timp lista e goală, semnarea nu e activă** și catalogul Lapin se citește ca în etapa 0; după ce cheia e în listă, catalogul Lapin nesemnat sau cu semnătura greșită nu e citit, iar pachetele lui nesemnate nu se instalează. Un catalog ales de utilizator poate fi nesemnat (Răsfoiește spune „Acest catalog nu e semnat de Lapin Extensions…”), niciodată cu o semnătură greșită. Ordinea: întâi indexul semnat publicat, apoi build-ul cu cheia.
- **Juridic** (`lapin-extensions/legal/`, engleza de referință + copii în română în `legal/ro/`, **PROIECTE, de revizuit de un avocat**): termenii de utilizare, termenii autorilor (licența dată catalogului: listare, copii, semnare; garanțiile autorului; retragerea), nota de confidențialitate (GDPR: Ciuntuc BIM Studio SRL, CUI RO51725860, J2025031471009, Huși, hello@lapinbim.com; identitatea de pe forum, trimiterile, descărcările anonime, datele tehnice doar în memorie; Cloudflare, Hetzner, GitHub; drepturile, ANSPDCP), notificare și acțiune (DSA: punct de contact, conținutul notificării, retragerea provizorie, expunerea de motive, contestarea).
- **Ce trebuie făcut pentru publicare** (`deploy/store/README.md`): tokenul GitHub fin (doar `lapin-extensions`: Contents, Pull requests, Checks); pe forum `enable discourse connect provider` și `discourse connect provider secrets` = `store.lapinbim.com|<secret>`; pe server `store.env`, serviciul din compose, `deploy/store/deploy.sh`; în Cloudflare două rute ale tunelului (`store.lapinbim.com` și `lapinbim.com` cu calea `^/extensions/`, spre `127.0.0.1:3465`); `publish.mjs --unsigned --upload`; apoi, când hotărăște utilizatorul, cheia și build-ul cu ea.

## 11. Etapa 2: hotărârea utilizatorului (12.11.2026)

- **2a, aprobată:** extensiile plătite ale altor autori sunt **vândute de autorii lor, prin propriul vânzător** (Lemon Squeezy, Gumroad, Paddle…), care face TVA-ul și facturile; magazinul doar trimite la pagina de cumpărare a autorului, iar Lapin verifică cheia de licență prin furnizorul autorului. **Comision 0 %.** Câmpul de manifest propus (de implementat mai târziu; încă nu e în schemă, ca să nu fie acceptat fără comportament): **`purchase`**, nu `licence`, ca să nu se confunde cu `license` (licența SPDX, care rămâne obligatorie și spune condițiile de folosire):

  ```
  purchase: { provider: "lemonsqueezy" | "gumroad" | "paddle" | …,
              productId: "<id-ul produsului la furnizor>",
              checkoutUrl: "https://…",     // pagina de cumpărare a autorului
              price?: "29 EUR" }            // doar afișat, orientativ; prețul real e al vânzătorului
  ```

  Un pachet plătit nu are copie publică: `package.url` e adresa de descărcare a vânzătorului sau una a magazinului care cere cheia; Lapin cere cheia de licență la instalare și o verifică prin API-ul public de validare al furnizorului (Lemon Squeezy `/v1/licenses/validate`, Gumroad `/v2/licenses/verify`), pe desktop prin motor; cheia rămâne pe calculator. De stabilit la implementare: furnizorii acceptați, ce se întâmplă fără internet, revocarea.
- **2b, schimbată:** modulele premium Lapin (mep-pro, urban, rail, planning-pro) **nu** se vând ca extensii în magazin. Devin o **platformă premium „ca un SaaS, dar locală + server”** (aplicația desktop plus un server), planificată separat în `docs/plan-premium-platform.md` (**de scris**, în afara acestei etape).
- Felurile 3 (plug-in-uri JS) și 4 (module C# semnate, pentru parteneri) rămân pentru mai târziu.
