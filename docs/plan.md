# Lapin pe motorul BubbleBIM Pro (plan, 23.10.2026)

Lapin se rescrie fără ifc-lite. Motorul devine BubbleBIM Pro (C#, .NET 10), iar ce am construit în Lapin se păstrează ca funcționalitate. Fork-ul ifc-lite (`Ciuntuc-BIM-Studio/lapin`) a fost salvat integral înainte de orice ștergere.

## Decizii

1. **Desktop întâi.** Aplicație Tauri, cu motorul C# pornit local, ca în BubbleBIM-Pro. Varianta web vine mai târziu.
2. **Repo separat.** Lapin are repo-ul lui și folosește motorul BubbleBIM Pro ca dependență. Nu e un profil de produs în BubbleBIM-Pro.
3. **Licență MIT pentru codul Lapin**, dacă se poate (vezi mai jos ce înseamnă concret).

## Backup (făcut)

În `~/Documents/GitHub/_backups/lapin-2026-10-23/`:

- `lapin.git`: mirror complet de pe GitHub (8 ramuri, un tag);
- `lapin-all.bundle`: același repo într-un singur fișier;
- `lapin-local-all.bundle`: repo-ul local, inclusiv ramurile care nu sunt pe GitHub (`client/ifcsheet`, `plugins-bubbles`);
- `issues.json` (6 issue-uri), `pulls.json` (niciunul);
- `release-desktop-test/`: build-urile de test pentru Windows și macOS.

Restaurare: `git clone lapin-all.bundle lapin`.

## Licența: ce poate fi MIT

- **Nu poate fi MIT:**
  - codul ifc-lite (MPL-2.0, al LTplus AG);
  - fișierele ifc-lite modificate în Lapin. Acestea rămân MPL, oricine le-ar fi modificat.
- **Poate fi MIT:** fișierele create de la zero în Lapin. Lista e în `lapin-own-files.txt` (1.336 de fișiere, cam 88k linii TypeScript fără teste), iar toate commit-urile de după fork sunt ale tale.
  - Ca titular, le poți relicenția. Ce a fost deja publicat sub MPL rămâne valabil pentru cine l-a preluat.
  - Prudență la fișierele noi care conțin bucăți copiate din fișiere ifc-lite: acolo partea copiată rămâne MPL.
- **Interfața Lapin de azi** (ribbon, layout, panouri, dialogurile de bază, viewer-ul 3D) e aplicația viewer din ifc-lite. Nu intră în repo-ul MIT, deci interfața nouă se scrie din nou.
- **Practic, în repo-ul nou nu se copiază cod din fork.** Logica noastră se rescrie: în C#, ca module ale motorului, sau în TypeScript nou, pentru interfață. Fișierele vechi servesc drept specificație și ca sursă pentru teste.
- **Motorul BubbleBIM Pro** nu are încă licență. El e al tău, deci decizia e separată: poate rămâne închis, chiar dacă Lapin e MIT.
- **Dependențele native** sunt compatibile: manifold e Apache-2.0.
- **Aplicația web veche** de pe lapin.ciuntucbimstudio.ro conține cod MPL. Cât timp rulează, sursa ei trebuie să rămână publică. Repo-ul de pe GitHub se șterge doar după ce aplicația veche și build-urile desktop vechi sunt retrase.

## Arhitectura

```
Lapin (repo nou, MIT)
├─ desktop/         Tauri 2: pornește motorul (sidecar), dialoguri native
├─ ui/              React + TypeScript + three.js: interfața Lapin, scrisă nou
│                   (ribbon, navigator, inspector, dialoguri, tema „Planșă de arhitect”)
├─ engine-modules/  module C# ale Lapin, încărcate de ModuleLoader din BubbleBIM Pro:
│                   pereți stratificați, zone pe înălțime, buiandrugi, faze,
│                   tipuri de tâmplărie, adnotări de desen (axe, cote…)
└─ vendor/bubblebim-pro  motorul BubbleBIM Pro (submodul git sau artefact compilat)
```

- **Comunicarea UI ↔ motor** folosește protocolul existent din Pro: WebSocket JSON-RPC (`doc.execute`, `doc.query`, `doc.undo`…). După fiecare comandă, motorul trimite un diff cu mesh-urile schimbate. Clientul pornește de la `ui/src/platform/engine.ts` din Pro, rescris pentru Lapin.
- **Modelul** e graful BubbleBIM: noduri și relații, iar geometria e derivată. IFC e doar format de export. Fișierele IFC importate se leagă ca referință, cum face deja Pro, potrivit principiului „Lapin = native first”.
- **Fișierul de proiect** e `.bbproj` (JSON). Rămâne de ales o extensie proprie (`.lapin`) peste același format.
- **Renderer-ul** e three.js, pe mesh-urile trimise de motor. Stilurile de vizualizare (linii, X-ray, creion) se fac acolo.

## Ce trebuie adăugat în motor pentru Lapin

| Funcție Lapin (azi, în TypeScript peste IFC) | În BubbleBIM Pro |
|---|---|
| Tipuri de perete stratificate, axa pe miez | lipsesc: pereții au un singur material |
| Îmbinări de perete (cap la cap, mitruit, în T) și tălpi de fundație care se îmbină | există îmbinări din topologie (`Joins.cs`); de verificat mitrul și tălpile |
| Zone pe înălțime (BCA, zidărie, centură) cu cantități | există centuri; zonele lipsesc |
| Buiandrugi din tipul ferestrei sau ușii, îmbinare la colț | lipsesc |
| Faze (existent / construcție / execuție), filtre și suprascrieri | lipsesc |
| Configurator de uși și ferestre (cercevele, panouri) | de verificat ce are Pro |
| Desen: axe cu baloane și aliniere, cote, hașuri, planșe | Drawing există (secțiuni, planșe, DXF, PDF); adnotările de verificat |
| Deviz, cantități | Cost există (deviz românesc, XLSX) |

## Etape

- **M0 – Schelet.** Repo nou (MIT): Tauri, motorul Pro ca submodul, o interfață goală care se conectează la motor, pune un perete și îl vede în three.js. Build pe macOS.
  - Gata când: comanda `building.addWall` din interfață apare în 3D, iar undo funcționează.
- **M1 – Interfața de bază.** Ribbon, navigator de proiect (niveluri, vederi), inspector de proprietăți, tema „Planșă de arhitect”, deschide / salvează proiect.
- **M2 – Pereți Lapin.** Modul C#: tipuri stratificate, axa pe miez, îmbinări, zone pe înălțime, buiandrugi din tipul de tâmplărie. Testele xUnit pornesc de la cazurile din testele TypeScript de azi (`wall-zones*.test.ts`, `wall-lintels*.test.ts`).
- **M3 – Uși, ferestre, configurator.**
- **M4 – Faze.** Modul C# pentru faze și pe element; în interfață, tab-ul „Phases”, filtre și suprascrieri.
- **M5 – Desen 2D și planșe.** Pe modulul Drawing: axe cu aliniere, cote, hașuri, planșe, export DXF și PDF.
- **M6 – Raportare și comunitate.** Pagina de bun venit, „Report an issue” (serviciul de pe Hetzner rămâne) și linkul Skool.
- **M7 – Lansare desktop.** Build-uri macOS și Windows (manifold compilat pentru Windows), linkuri pe ciuntucbimstudio.ro. Apoi aplicația web veche și repo-ul fork se retrag.

## Riscuri

- **Motorul Pro e foarte nou** (două zile de commit-uri). Lapin va cere schimbări în el, deci trebuie stabilit cine îl modifică și cum se versionează.
- **manifold e compilat doar pentru macOS arm64.** Windows, și mai târziu Linux pentru web, trebuie adăugate.
- **Mesh-urile circulă ca JSON.** Pe modele mari, un format binar devine necesar.
- **Paritatea de funcții** cu Lapin-ul de azi va dura. Până atunci, aplicația web veche rămâne disponibilă ca previzualizare.
