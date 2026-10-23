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

## Arhitectura (revizuită 23.10.2026)

**Lapin rămâne un modelator clasic, pe schema IFC.** Modelul lui sunt entitățile și relațiile IFC (IFC4X3):
- `IfcWall` cu `IfcWallType` și `IfcMaterialLayerSetUsage`;
- `IfcOpeningElement` legat prin `IfcRelVoidsElement` și `IfcRelFillsElement`;
- `IfcRelConnectsPathElements` pentru îmbinări;
- seturile de proprietăți și cantitățile.

Așa lucrează și Lapin-ul de azi. Din BubbleBIM se ia **doar motorul de geometrie**, ca bibliotecă, nu modelul lui de graf (pereți care leagă axe).

```
lapin-pro (MIT)
├─ engine/                Lapin.Engine (C#, .NET 10): modelul IFC al Lapin
│  ├─ Lapin.Ifc.Model     entitățile și relațiile IFC4X3, editate prin comenzi (un pas de undo fiecare)
│  ├─ Lapin.Authoring     uneltele: pereți stratificați și îmbinări, plăci, goluri, uși/ferestre,
│  │                      zone pe înălțime, buiandrugi, faze, tipuri
│  ├─ Lapin.Geometry      reprezentările IFC → solide → mesh, prin kernelul BubbleBIM
│  └─ Lapin.Host          WebSocket JSON-RPC, același protocol ca BubbleBIM (doc.execute, diff-uri cu mesh-uri)
├─ ui/                    interfața Lapin (React + three.js), cum e în M0
├─ desktop/               shell-ul Tauri
└─ vendor/bubblebim-pro   doar ca bibliotecă, prin project references:
                          Platform.Geometry (B-rep, tessellator, skeleton),
                          Platform.Booleans (manifold),
                          STEP: Step.cs / StepFile.cs, reprezentări IFC → mesh: IfcModel.cs
```

- **Fișierul de proiect** e IFC (STEP), plus un fișier alăturat pentru ce nu e IFC (vederi, planșe, standarde de desen), ca la Lapin-ul de azi.
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

## Riscuri

- **Motorul Pro e foarte nou** (două zile de commit-uri). Lapin va cere schimbări în el, deci trebuie stabilit cine îl modifică și cum se versionează.
- **manifold e compilat doar pentru macOS arm64.** Windows, și mai târziu Linux pentru web, trebuie adăugate.
- **Mesh-urile circulă ca JSON.** Pe modele mari, un format binar devine necesar.
- **Paritatea de funcții** cu Lapin-ul de azi va dura. Până atunci, aplicația web veche rămâne disponibilă ca previzualizare.
