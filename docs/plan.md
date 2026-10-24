# Lapin pe motorul BubbleBIM Pro (plan, 23.10.2026)

Lapin se rescrie fără ifc-lite. Motorul devine BubbleBIM Pro (C#, .NET 10), iar ce am construit în Lapin se păstrează ca funcționalitate. Fork-ul ifc-lite (`Ciuntuc-BIM-Studio/lapin`) a fost salvat integral înainte de orice ștergere.

## Decizii

1. **Desktop întâi.** Aplicație Tauri, cu motorul C# pornit local, ca în BubbleBIM-Pro. Varianta web vine mai târziu.
2. **Repo separat.** Lapin are repo-ul lui și folosește motorul BubbleBIM Pro ca dependență. Nu e un profil de produs în BubbleBIM-Pro.
3. **Licență MIT**, doar MIT.
4. **Formatul `.lapin` se păstrează.** Formatul 3 e un zip de bucăți adresate după conținut, cu modelul în tabele Arrow (`docs/format-lapin.md`). Citirea proiectelor vechi (formatul 2) vine la M6.
5. **Modelul Unity / Unreal.** Aplicația lucrează local. Cloud-ul aduce conturile, actualizările și bibliotecile (tipuri, materiale, tâmplărie, hașuri). Mai târziu aduce și sincronizarea, versiunile și lucrul în echipă. Modelarea nu se face pe web.
6. **Edițiile sunt combinații de module** (24.10.2026). Modulele nu sunt neapărat „premium”: scopul e ca un client să primească în producție, curat, exact combinația de care are nevoie. Exemple: doar arhitectură și structură, doar ce trebuie pentru civile, doar ce trebuie pentru MEP.
   - Un produs (`products/<id>.json`) numește modulele. Fiecare modul își declară dependențele (`requires` în `module.json`), iar acestea vin automat.
   - Un modul pe care produsul nu-l are nu există în pachet, nici în motor, nici în interfață.
7. **Cod refolosit** (24.10.2026). Se poate folosi cod din BubbleBIM Pro și din Atrium, proiectele C# ale utilizatorului. Tiparele de platformă (module încărcate din foldere, sesiune, undo, protocolul, împachetarea pe produs) vin din BubbleBIM Pro, adaptate pe modelul IFC.

## Backup (făcut)

În `~/Documents/GitHub/_backups/lapin-2026-10-23/`:

- `lapin.git`: mirror complet de pe GitHub (8 ramuri, un tag);
- `lapin-all.bundle`: același repo într-un singur fișier;
- `lapin-local-all.bundle`: repo-ul local, inclusiv ramurile care nu sunt pe GitHub (`client/ifcsheet`, `plugins-bubbles`);
- `issues.json` (6 issue-uri), `pulls.json` (niciunul);
- `release-desktop-test/`: build-urile de test pentru Windows și macOS.

Restaurare: `git clone lapin-all.bundle lapin`.

## Licența

- **Lapin e MIT, doar MIT** (decis 23.10.2026). Licența ifc-lite nu mai e o preocupare a proiectului.
- **Consecința practică:** în lapin-pro nu intră cod din fork. Logica se rescrie în C#, ca module ale motorului, sau în TypeScript nou, pentru interfață. Fișierele vechi rămân specificația și sursa testelor.
- **Dependențele** sunt doar permisive, verificate de `pnpm check:licenses`. manifold e Apache-2.0.
- **Motorul BubbleBIM Pro** e un proiect separat, cu termenii lui.

## Arhitectura (revizuită 23.10.2026)

**Lapin rămâne un modelator clasic, pe schema IFC.** Modelul lui sunt entitățile și relațiile IFC (IFC4X3):
- `IfcWall` cu `IfcWallType` și `IfcMaterialLayerSetUsage`;
- `IfcOpeningElement` legat prin `IfcRelVoidsElement` și `IfcRelFillsElement`;
- `IfcRelConnectsPathElements` pentru îmbinări;
- seturile de proprietăți și cantitățile.

Așa lucrează și Lapin-ul de azi. Din BubbleBIM se ia **doar motorul de geometrie**, ca bibliotecă, nu modelul lui de graf (pereți care leagă axe).

```
lapin-pro (MIT)
├─ engine/                        motorul Lapin (C#, .NET 10), engine/Lapin.slnx
│  ├─ src/Model/Lapin.Model        entitățile și relațiile IFC4X3, tranzacții (un pas de undo fiecare),
│  │                               GlobalId-uri, regulile straturilor (miezul pe axă)
│  ├─ src/Platform/Lapin.Platform  contractul modulelor și încărcarea lor, modulul „core” (structura spațială,
│  │                               materialele, comenzile comune), sesiunea, diff-urile, geometria
│  ├─ src/Platform/Lapin.Package   pachetul .lapin formatul 3 (zip, SHA-256, tabele Arrow)
│  ├─ src/Modules/<Grup>/Lapin.<X> un modul: clasele IFC pe care le scrie, comenzile, geometria (module.json)
│  ├─ src/Host/Lapin.Host          WebSocket JSON-RPC, același protocol ca BubbleBIM (doc.execute, diff-uri cu mesh-uri)
│  └─ tests/Lapin.Engine.Tests     xUnit, cu testele portate din fork
├─ ui/                            interfața Lapin (React + three.js); ui/src/modules/<id> e jumătatea UI a unui modul
├─ products/                      edițiile: listele de module
├─ desktop/                       shell-ul Tauri
└─ vendor/bubblebim-pro           doar ca bibliotecă, prin project references:
                                  Platform.Geometry (B-rep, tessellator, skeleton),
                                  mai târziu Platform.Booleans (manifold) și citirea IFC (Step.cs, IfcModel.cs)
```

Un modul are o jumătate de motor (`engine/src/Modules/…`, cu `module.json`), o jumătate de interfață (`ui/src/modules/<id>`) sau amândouă. Modulele de motor se construiesc în `engine/artifacts/modules/<id>/`, iar host-ul le încarcă după produs. `pnpm build:product <id>` pune în aplicație doar modulele produsului.

- **Fișierul de proiect** e pachetul `.lapin`, cu intenția modelului, vederile, planșele și standardele (`docs/format-lapin.md`). IFC e formatul de schimb, la import și la export.
- **Protocolul** cu interfața rămâne cel din M0. Se schimbă doar motorul din spate: în M0 a fost host-ul BubbleBIM, de acum e host-ul Lapin.

### Compatibilitatea cu BubbleBIM: prin IFC, cu intenția de proiect

**Azi,** Bubbles nu trimite geometrie, ci intenția: un JSON `bubbles-house` cu axele pereților, golurile, contururile și acoperișul. Lapin construiește din el elemente native, cu uneltele lui.

**Îmbunătățirea propusă:** schimbul se face printr-un fișier IFC care poartă și intenția.
1. **Fișierul se poate deschide oriunde.** Elementele au geometria lor IFC, deci fișierul merge în orice program IFC.
2. **Intenția e scrisă tot în IFC:**
   - reprezentarea `Axis` a pereților;
   - `IfcMaterialLayerSetUsage` pentru straturi;
   - parametrii fiecărui element, într-un set de proprietăți comun, de exemplu `Pset_BubbleBIM_Intent`: axe, contururi, pantă, regulile golurilor.
   Lapin reconstruiește din ele elemente native, cum face azi cu `bubbles-house`. Ce nu are intenție (un IFC de la alt program) intră ca referință.
3. **GlobalId-uri stabile.** Un al doilea export din BubbleBIM al aceleiași case **actualizează** elementele din Lapin, după GlobalId, în loc să le dubleze. Ce ai modificat deja în Lapin e păstrat sau semnalat.
4. **Și invers.** BubbleBIM poate lega IFC-ul Lapin ca referință, cum leagă deja IFC-uri, sau, mai târziu, să-i citească intenția.

Formatul `bubbles-house` de azi devine astfel un caz particular al aceluiași IFC, iar cele două programe rămân independente ca model intern.

## Etapele (replanificate 23.10.2026, propunere)

Numerotarea veche avea două variante: cea din commit-ul M0 (M4 faze, M6 raportare, M7 lansare) și cea din `transfer.md` (M7 MEP). Aceasta le înlocuiește pe amândouă.

- **M0 – Schelet** (făcut, `d09bd60`): Tauri, motorul ca submodul, un perete văzut în three.js. Mock-ul interfeței: `6a5b582`.
- **M1 – Motorul Lapin și formatul** (făcut pe 24.10.2026: modelul, platforma, modulul Architecture cu pereții stratificați și flip-ul, `.lapin` formatul 3, host-ul, edițiile și `build:product`, mesh-urile binare, jurnalul de recuperare, interfața legată de motor):
  - `Lapin.Ifc.Model` (entități și relații IFC4X3, comenzi, undo) și `Lapin.Host`, pe același protocol;
  - tipul de perete stratificat: layer set, usage, axa pe miez, flip;
  - `.lapin` formatul 3: salvare, deschidere, jurnal;
  - mesh-urile trimise binar;
  - interfața din mock legată la motor: navigator, inspector, deschide și salvează.

  Formatul vine acum, nu la final, pentru că el fixează forma modelului.
- **M2 – Pereții:** îmbinări, mitru, zone pe înălțime, straturi de finisaj, buiandrugi, priorități de tăiere, fundații.
  - Făcut (24.10.2026): îmbinările L, T și în prelungire, butt sau mitre, cu prioritate (ca elemente pe traseu, în platformă); zonele pe înălțime și piesele de finisaj, cu cantitățile lor; buiandrugii și centura coborâtă; plăcile; prioritățile de tăiere; modulul Structure (stâlpi, grinzi, fundații continue și izolate).
  - Rămas: îmbinarea straturilor după funcție (la M5); îmbinarea buiandrugilor la colțuri; goluri în plăci; profilele I, L, T, U.
- **M3 – Configuratorul de uși și ferestre.**
  - Făcut (24.10.2026): golurile în pereți, ușile și ferestrele cu tipurile lor, configuratorul (portat cu testele), simbolurile de bază în plan.
  - Rămas: simbolurile complete și maparea pe modele importate (M5), extrasul de tâmplărie (M6).
- **Acoperișurile** (aprobate cu M1–M7, făcut 24.10.2026): `IfcRoof` portat din BubbleBIM Pro cu testele lui (forme, stiluri, reguli pe laturi, șarpantă, jgheaburi, burlane, parazăpezi, cantități), pereții care urcă la fronton și se opresc sub cosoroabă sau sub pante, unealta și inspectorul. Rămas: ferestrele de acoperiș și lucarnele.
- **M4 – Materiale și standarde de desen.**
  - Făcut (24.10.2026): biblioteca `Lapin.Drawing` cu hașurile (`.pat`, biblioteca, umplerea, regiunea, portate cu testele), materialele pe tip de vedere (pe `IfcMaterial`, aspectul pentru IFC), standardele de desen (stiluri de text, cote, axe, etichete, layere, grupuri, citite tolerant), comenzile lor în `core` și dialogurile Materials și Drafting standards.
  - Rămas: texturile 3D ale materialelor; folosirea lor în desen vine cu M5.
- **M5 – Desenul 2D:** generatorul, domeniul vederii, penițele, simbolurile, cotele.
  - Făcut (24.10.2026): generatorul pe liniile ascunse exacte din BubbleBIM (tăietură, văzut, ascuns, deasupra, dincolo), domeniul vederii, tabelul de penițe și stilurile de linii, desenul pe materiale (hașuri, îmbinări în același material, membrane), vederile ca entități (planuri, planuri de fundații, secțiuni, fațade), marcajele de secțiune și nivelurile, axele, cotele asociative aliniate și textele, cu uneltele lor.
  - Rămas: unirea liniilor coliniare, eliminarea liniilor parazite, simbolurile complete de tâmplărie după specificație, cote liniare și lanțuri, etichete, editările de linii.
- **M6 – Faze, planșe, DXF și PDF, citirea `.lapin` formatul 2.**
  - Făcut (24.10.2026): planșele (A0–A4, cartuș, vederi la scara lor), PDF și DXF R2000, extrasele de tâmplărie și de buiandrugi (fișe cu desenul tipului, rânduri, celule, tabele, verificare, CSV, paginare, așezare pe planșă).
  - Făcut și fazele (modelul portat cu testele, vederile și 3D-ul după fază și filtru).
  - Făcut și citirea formatului 2 (proiectele vechi se reconstruiesc nativ).
  - Rămas: XLSX, continuarea automată pe planșe, secțiunile și planșele proiectelor vechi.
- **M7 – Lansarea desktop:**
  - macOS și Windows (manifold pentru Windows), cu semnare și notarizare;
  - pagina de bun venit, „Report an issue”, linkul Skool;
  - apoi se retrag aplicația web veche și fork-ul.
- **M8 – Hub-ul cloud:**
  - conturi;
  - canalul de actualizări pe ediție (updater-ul Tauri);
  - bibliotecile `.lapinlib`;
  - mai târziu, sincronizarea și versiunile.
- **M9 – MEP rezidențial,** ca modul `mep`, cu ediția lui: rețeaua și traseul, încălzirea, apa, canalizarea, electricul, ventilația.
- **Ulterior:** timber și CLT, infrastructură.

La fiecare etapă, testele-specificație din `transfer.md` se portează înaintea codului.

## Riscuri

- **Motorul Pro e foarte nou** (două zile de commit-uri). Lapin va cere schimbări în el, deci trebuie stabilit cine îl modifică și cum se versionează.
- **manifold e compilat doar pentru macOS arm64.** Windows, și mai târziu Linux pentru web, trebuie adăugate.
- **Mesh-urile circulă ca JSON.** Pe modele mari, un format binar devine necesar.
- **Paritatea de funcții** cu Lapin-ul de azi va dura. Până atunci, aplicația web veche rămâne disponibilă ca previzualizare.
