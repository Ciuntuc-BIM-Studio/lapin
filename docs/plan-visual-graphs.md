# Grafuri vizuale în Lapin: plan

Cerut de utilizator (13.11.2026): automatizarea Python să meargă mai departe, într-o **automatizare vizuală în Lapin, ceva ca Grasshopper dar mai prietenos, cu intrările și ieșirile direct în interfață odată ce automatizările sunt făcute**. Răspunsurile utilizatorului la propunere: (1) **ediția gratuită** (un modul deschis, în `products/lapin.json`); (2) **ediția web de la etapa 1** (motorul rulează acolo într-un Web Worker, `engine/src/Host/Lapin.Browser`); (3) **un panou lateral**: pe web împarte ecranul (split), pe desktop poate pluti. Decizia 525 (`docs/decisions-pending.md`).

**Stare (13.11.2026):** **etapa 1 e făcută** pe ramura `feature/visual-graphs` (pornită din `feature/script-ui-panels`, decizia 526, ale cărei palete le refolosește etapa 2): modulul `graphs` (motor și interfață), evaluarea, fantoma, Bake, panoul, testele, ghidul (`docs/user/graphs.md`). Utilizatorul a răspuns la §9 (13.11.2026): grafurile sunt ale gazdei (§7), restul cum e. Hotărâri luate în lucru: un nod-comandă fără nivel lucrează pe nivelul vederii (`storey` trimis de fereastră, ca o unealtă care desenează pe nivelul arătat); după Bake fantoma se ascunde. Rămân pentru etapa 1b: cache-ul pe nod (§3) și construirea ediției web pe o mașină cu `wasm-tools` (aici lipsește; modulul nu are nimic doar pentru desktop). O comandă care cade la jumătate în previzualizare își lasă pe copie ce apucase să facă până la sfârșitul evaluării (copia e dată înapoi oricum; la Bake totul e refuzat).

## 0. Pe scurt

Un **graf** e o schemă de noduri legate prin fire, ținută **în proiect** (o entitate `LapinGraph`, ca `LapinScript`). Îl **evaluează motorul C#**, nu Python: merge fără Python, merge pe web (același motor în worker), e determinist și ieftin de evaluat la fiecare schimbare. Nodurile sunt **comenzile și întrebările din catalog** (`session.catalogue`: fiecare comandă un nod, cu argumentele ei ca porturi) plus un set mic de **noduri proprii** (numere, liste, puncte, geometrie simplă, matematică, text, proprietățile elementelor, filtre). Ce ar face graful se vede ca **fantomă** în 3D și în plan, construită de motor pe o copie a modelului (principiul previzualizării configuratoarelor); **Bake** face elementele de adevărat, într-un pas de undo. Etapa 2 face din nodurile de intrare o **paletă în Uneltele mele**; etapa 3 lasă elementele **conduse** de graf; etapa 4 aduce nodurile Python, înregistratorul → graf și grafurile în magazin.

## 1. Ce are Lapin azi (și ce se refolosește)

| Ce | Unde | Folosit pentru |
|---|---|---|
| Comenzile modulelor, un pas de undo fiecare, cu argumente JSON | `ModuleCatalog.Command(name).Handler(ctx, args)`; `edit.batch` le rulează una după alta în același context (`CoreModule.cs`) | nodurile-comandă; Bake = un singur pas, ca `edit.batch` |
| Catalogul argumentelor, generat la build | `scripts/command-catalogue.mjs` → `engine/src/Platform/Lapin.Protocol/catalogue.json`, servit ca `session.catalogue` (611 comenzi, 239 întrebări) | porturile nodurilor-comandă (nume, tip `float/str/bool/list/dict`) |
| Previzualizarea pe o copie a modelului | `JoineryPreview3D.Of` (`Lapin.Architecture`): un `ModelStore` + `History` de lucru, elementele făcute acolo, geometria lor prin `IGeometryProducers` și `IGeometryFilter`, întoarsă ca `parts: [{positions, indices, edges, color}]` | fantoma grafului, fără să atingă modelul |
| Entitatea de proiect cu text în pachet | `ScriptingModule` (`LapinScript`, `scripting.save/rename/delete`, `IPackageTexts` → `scripts/<nume>.py`, `IDrivenElements` care refuză editările generice) | `LapinGraph` și `graphs/<nume>.json` (folderul adăugat în `LapinPackage.TextFolders`) |
| Elemente conduse | `IDrivenElements` (decizia 465, `docs/overrides.md` „Driven elements”), `Driven.Before/After` | etapa 3 |
| Formulele utilizatorului | `Expr.Eval` (`Expressions.cs`: `+ - * / ^`, comparații, `min max round sqrt sin cos if clamp…`, limitate ca lungime și adâncime) | nodul „Formula” |
| Panourile vederilor | `LapinModule.views` (ca Gantt-ul și devizul): o vedere a modulului într-un panou; `splitPane('row')` pune două alături; fila trasă afară sau butonul „fereastră proprie” o deschide într-o fereastră Tauri (`app/windows.ts`, decizia 445) | panoul grafului: lateral, split pe web, plutitor pe desktop |
| Suprapunerile vederilor | `overlays3d` (scena vederii 3D) și `planOverlays` (SVG-ul planului, în coordonatele modelului) | fantoma în 3D și în plan |
| Paletele scripturilor | `ui/src/modules/scripting/palette.ts`, `PaletteWindow.tsx`, câmpurile lui `ask` (`ask.ts`: număr cu unitate, text, alegere, bifă, element, nivel, tip) | etapa 2 |
| Oaspeții | `EngineDispatcher.AllowedRemote`: un editor rulează orice comandă în afară de `HostOnlyCommands` (`scripting.`); `doc.query` merge și pentru cine doar privește | regulile pentru `graphs.*` (§7) |

## 2. Graful, ca JSON (format 1)

```json
{
  "format": 1,
  "nodes": [
    { "id": "n1", "kind": "number", "x": 40, "y": 60, "values": { "value": 5, "unit": "length" }, "label": "Lungime" },
    { "id": "n2", "kind": "range", "x": 240, "y": 60, "values": { "start": 0, "step": 1 } },
    { "id": "n3", "kind": "command:architecture.addWall", "x": 480, "y": 60, "values": { "y1": 0, "y2": 4, "height": 2.8 }, "whole": [] }
  ],
  "wires": [ { "from": "n1", "out": "value", "to": "n2", "in": "end" }, { "from": "n2", "out": "list", "to": "n3", "in": "x1" } ]
}
```

- **`kind`**: un nod propriu (`number`, `range`, `point`…, lista lor din `graphs.nodes`) sau `command:<nume>` / `query:<nume>` din catalog.
- **`values`**: constantele intrărilor nelegate (inspectorul le scrie); un fir pe o intrare are întâietate.
- **`whole`**: intrările unui nod din catalog care iau o listă întreagă (argumentele de tip `list`/`dict` din catalog, ex. `points`), scrise de interfață când pune nodul (motorul nu citește catalogul, care e al protocolului).
- **Porturi de punct**: unde catalogul are perechi `xN, yN`, interfața arată un port de punct `pt:N` („Start”, „End”), iar motorul desface punctul în `xN, yN`. Așa un perete ia două puncte, nu patru numere.
- **Limite** (verificate la salvare și la evaluare): 500 de noduri, 2 000 de fire, o intrare are cel mult un fir, fără cicluri, 256 KB de JSON; o listă are cel mult 10 000 de elemente; o evaluare face cel mult 2 000 de apeluri de comenzi și ține cel mult 5 s (Bake 60 s); fantoma are cel mult 2 milioane de coordonate (restul tăiat, cu un avertisment).

## 3. Evaluarea

- **Ordinea**: topologică, din fire; un nod fără intrări legate se evaluează din constantele lui. Valorile sunt: număr, text, bifă, punct (`{"$pt": [x, y, z]}` în JSON), linie/polilinie (`{"$line": [...]}`), id de element, listă, obiect JSON (rezultatul unei comenzi).
- **Liste fără arbori de date**: o listă dată unei intrări de o singură valoare **se mapează**: nodul rulează o dată pe element. Mai multe liste pe intrări diferite merg **în paralel, după cea mai lungă** (cele mai scurte își repetă ultimul element, regula implicită din Grasshopper); o valoare singură se repetă. O listă de liste se mapează din nou, un nivel pe rând (ieșirea are aceeași formă). O intrare care ia lista întreagă (`whole`, „Count”, „Sum”, „Item”, „Polyline”) nu mapează. Pe canvas, firul care duce o listă e desenat mai gros, cu numărul elementelor.
- **Erori pe nod**: un nod care cade spune de ce (mesajul comenzii, tradus ca orice mesaj al motorului); nodurile de după el primesc „Lipsește intrarea X”, restul grafului se evaluează mai departe. Într-o mapare, elementele care cad ies `null` și nodul arată „3 din 10 au căzut: …”.
- **Previzualizarea nu atinge modelul**: `graphs.evaluate` e o **întrebare** (`doc.query`). Când graful are noduri-comandă, motorul face o **copie de lucru** a modelului (`ModelStore` + `History`, ca `JoineryPreview3D`), păstrată cât timp modelul nu se schimbă (după `IModelView.Revision`); fiecare evaluare rulează comenzile într-o tranzacție pe copie, citește geometria elementelor făcute sau schimbate (producătorii și filtrele modulelor) și **dă tranzacția înapoi** la sfârșit, așa că copia rămâne a modelului pentru evaluarea următoare. Id-urile din fantomă sunt de unică folosință.
- **Comenzile în previzualizare vs. Bake**: în previzualizare o comandă rulează pe copie, cu aceleași reguli de după comandă ca în sesiune (`Driven.After`, `TypeDefaults.Settle`, `Components.Settle`); ieșirile ei sunt `ids` (entitățile făcute) și `result` (ce întoarce comanda). La **Bake** (`graphs.bake`, o comandă: un pas de undo numit după graf) același evaluator rulează pe contextul real; dacă un nod-comandă cade, Bake e refuzat cu eroarea lui și nu rămâne nimic (tranzacția e atomică). Nodurile-întrebare și cele proprii sunt fără efecte, la fel în amândouă.
- **Comenzi refuzate ca noduri**: cele care citesc sau scriu fișiere ori proiecte (`*import*`, `*export*`, `link.*`, `project.*`, `version.*`), cele care rulează alte grafuri sau scripturi (`graphs.*`, `scripting.*`), `edit.batch` și argumentele care numesc o cale (aceleași chei ca `EngineDispatcher.PathKeys`). Lista e în motor (`GraphLimits.Refused`), deci și un graf scris de mână e oprit.
- **Cache și reevaluare**: etapa 1 refolosește copia modelului între evaluări (scumpă pe modele mari) și evaluează tot graful (ieftin la sute de noduri). Etapa 1b (dacă se măsoară nevoia): rezultatul fiecărui nod fără efecte ținut după semnătura lui (felul, constantele, semnăturile intrărilor, revizia modelului pentru nodurile care citesc modelul), ca o schimbare la un capăt să refacă doar ce urmează; comenzile se rulează din nou mereu (copia e dată înapoi).
- **Rezultatul**: `{nodes: {id: {outputs: {port: valoare scurtată la 100 de elemente}, count?, error?, warning?}}, ghost: {parts: [{positions, indices, edges, color, storey}], lines: [[x,y,z…]], plan: [{storey, points, closed}]}, ms, calls}`.

## 4. Nodurile

**Proprii** (registrul din motor, `graphs.nodes` le dă interfeței cu numele, grupul și porturile lor tipizate): Intrări — Număr (cu unitate: lungime, unghi, fără), Număr întreg, Text, Bifă, Alegere, Punct, Nivel, Elemente alese; Liste — Interval, Serie, Număr de elemente, Element din listă, Inversează, Aplatizează, Sumă, Minim/Maxim; Matematică — Adună, Scade, Înmulțește, Împarte, Formula (`Expr`, intrările `a b c d`), Rotunjește, Compară; Puncte și geometrie — Punct XYZ, Descompune punctul, Mută punctul, Distanța, Împarte linia, Grilă de puncte, Puncte pe cerc, Linie, Polilinie, Dreptunghi; Text — Unește textele, Număr ca text; Model — Elementele clasei, Nivelurile, Proprietatea unui element; Filtre — Filtrează după bifă; Ieșiri — Vezi valoarea.

**Din catalog**: fiecare comandă și întrebare a produsului, cu porturile ei din catalog, numele ei în limba utilizatorului (eticheta comenzii, „Wall”, nu `architecture.addWall`), grupate după modul (Arhitectură, Structură…). Un nod-comandă are mereu ieșirile `ids` și `result`.

**Prietenos**: nume în limbaj obișnuit grupate pe discipline; dublu-clic pe canvas → căutare (după nume, grup, nume tehnic) și nodul pus acolo; porturi colorate după tip (număr, text, bifă, punct, geometrie, element, listă, oricare) și fire refuzate între tipuri care nu se potrivesc; mai târziu (etapa 2) o selecție strânsă într-un singur nod și (etapa 4) înregistratorul care face un graf.

## 5. Panoul

- **O vedere a modulului** (`views.graphs`), ca Gantt-ul: **Graphs** o deschide alături de vederea curentă (`split: 'row'`, panoul din dreapta), deci **pe web împarte ecranul** cu 3D-ul sau planul, iar separatorul îi dă lățimea. Pe **desktop** fila ei se trage afară sau butonul „fereastră proprie” o deschide într-o **fereastră plutitoare** (`openViewWindow`, decizia 445), pe alt ecran; fereastra vorbește cu același motor, deci graful e unul. Fereastra principală desenează fantoma: ce graf e previzualizat trece între ferestre ca evenimentele lor (`graphs-preview`).
- **Conținutul**: bara de sus (graful ales din proiect, Nou, Redenumește, Șterge, Previzualizare pornită/oprită, **Bake**), canvasul (SVG scris de noi, fără dependență: pan cu butonul din mijloc sau spațiu + tragere, zoom cu rotița, noduri, fire, selecție cu dreptunghi și Shift, Delete), inspectorul nodului ales (constantele, eroarea, ieșirile) în dreapta canvasului.
- **Undo prin motor**: fiecare schimbare terminată (un nod pus, mutat la ridicarea mouse-ului, un fir, o constantă) e un `graphs.save`, deci Ctrl+Z o desface ca pe orice pas.

## 6. Proiectul și pachetul

- `LapinGraph` (modulul de motor `graphs`, `engine/src/Modules/Automation/Lapin.Graphs`): numele, `graph` (JSON-ul de la §2), descrierea; `graphs.save`, `graphs.rename`, `graphs.delete` (un pas fiecare), `graphs.list`, `graphs.get`, `graphs.nodes`, `graphs.evaluate`, `graphs.bake`.
- În `.lapin`: entitatea în tabelele Arrow ale modulului, ca orice conținut, plus o copie text `graphs/<nume>.json` (`IPackageTexts`; `graphs` intră în `LapinPackage.TextFolders`). Nicio parte nouă de format, deci niciun `IProjectUpgrade`.
- Versiunile, sesiunile partajate și regula modulului absent (decizia 484) merg fără nimic nou, ca la scripturi.

## 7. Siguranța și oaspeții

- **Un graf nu rulează cod**: nodurile sunt comenzile utilizatorului și funcții proprii fără efecte. De aceea **nu cere încredere** (ca un script al proiectului): Bake face exact ce ar face utilizatorul cu uneltele lui, într-un pas de undo. Nodurile Python (etapa 4) vor cere încrederea scripturilor (după hash).
- **Nu rulează niciodată singur**: un graf deschis nu face nimic până nu e previzualizat (ce nu schimbă modelul) sau copt.
- **Oaspeții** (răspunsul utilizatorului din 13.11.2026: grafurile sunt ale gazdei, ca scripturile): un oaspete nu rulează și nu evaluează niciun graf. `graphs.*` e în `EngineDispatcher.HostOnlyCommands` (singură sau într-un `edit.batch`), întrebările `graphs.*` în `HostOnlyQueries` (și pentru cine doar privește), iar comenzile generice pe un `LapinGraph` (ștergere, redenumire, grupare, copiere, mutare) sunt refuzate unui oaspete în `GraphsModule.Before` (`HostFiles.GuestCall`), ca la `ScriptingModule`. În fereastra unui oaspete, View → Graphs spune de ce nu se deschide, iar o filă Graphs arată același mesaj; fantoma nu se calculează.

## 8. Etape

| Etapa | Ce aduce | Efort |
|---|---|---|
| **1. Canvas, noduri, evaluare, fantomă, Bake** | modulul deschis `graphs` (motor + interfață) în ediția `lapin` și pe web; `LapinGraph` și comenzile lui; registrul nodurilor proprii și cele din catalog; maparea listelor, erorile pe nod, limitele; `graphs.evaluate` pe copia modelului, `graphs.bake`; canvasul, inspectorul, căutarea la dublu-clic, fantoma în 3D și plan; panoul lateral / split / fereastra plutitoare; teste, ghid | ~2 săptămâni |
| **2. Paleta automată** | nodurile de intrare (număr cu unitate și cursor, alegere, bifă, element ales, nivel, tip) devin o paletă în Uneltele mele (`palette.ts`, `PaletteWindow.tsx`, câmpurile lui `ask`); ieșirile (valori, tabele) în paletă; „Bake” pe paletă; o selecție strânsă într-un nod | ~1 săptămână |
| **3. Rezultat condus** | elementele copte rămân ale grafului (`IDrivenElements`): o intrare schimbată le reface cu aceleași GlobalId-uri (perechea nod × index → id), editările generice refuzate sau scrise înapoi | ~1,5 săptămâni |
| **4. Python, înregistrator, magazin** | noduri `@lapin.node` (doar desktop, cu încredere), înregistratorul care face un graf, grafurile ca extensii în magazin | ~2 săptămâni |

- **Teste**: xUnit (`GraphTests`): evaluarea și ordinea, maparea (lungime, repetarea ultimului, liste de liste, `whole`), erorile pe nod și restul evaluat, limitele (noduri, cicluri, liste, apeluri), previzualizarea care lasă modelul neschimbat (revizia și entitățile) și copia refolosită, Bake = un pas de undo (Undo îl scoate), Bake refuzat la o eroare fără să lase nimic, comenzile refuzate ca noduri, salvarea și deschiderea `.lapin` cu `graphs/<nume>.json`; vitest pentru logica pură a canvasului (fire, tipuri, cicluri, căutare, porturile de punct, transformările de vedere).
- **Ghidul**: `docs/user/graphs.md` și `docs/user/ro/graphs.md`, scena `graphs` în `scripts/user-docs/scenes.mjs` (un graf care pune un rând de pereți, cu fantoma și Bake).

## 9. Întrebări pentru utilizator și răspunsurile lui

Răspunsurile (13.11.2026): 1. listele se mapează după cea mai lungă, cum e; 2. Bake refuzat întreg la o eroare, cum e; 3. fără încredere pentru grafuri; 4. **grafurile rămân ale gazdei, ca scripturile** (făcut: comenzile și întrebările `graphs.*` doar ale gazdei, comenzile generice pe un graf refuzate oaspeților, panoul unui oaspete spune de ce; §7); 5. limitele de la §2 sunt bune; 6. numele „Graphs” rămâne.

1. Maparea listelor după **cea mai lungă** (cele scurte își repetă ultimul element), ca în Grasshopper — sau după cea mai scurtă (fără repetări, mai puțin surprinzătoare)?
2. Bake **refuzat întreg** când un nod-comandă cade (nimic pe jumătate) — de acord, sau coace ce a reușit și arată erorile?
3. Grafurile nu cer încredere (nu rulează cod, doar comenzile utilizatorului) — de acord? (Nodurile Python, la etapa 4, o vor cere.)
4. Oaspeții editori pot salva și coace grafuri (ca orice comandă), cei care doar privesc doar previzualizează — de acord, sau grafurile să fie doar ale gazdei, ca scripturile?
5. Limitele de la §2 (500 de noduri, 10 000 de elemente pe listă, 5 s la previzualizare) sunt bune pentru început?
6. Numele în interfață: **Graphs** („Grafuri”) sau altul („Automatizări vizuale”, „Scheme”)?
