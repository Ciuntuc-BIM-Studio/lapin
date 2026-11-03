# Interfața hibridă: simplă la bază, cu mecanicile jocurilor de construcție

Cerut de utilizator la 03.11.2026: o analiză UI/UX și o machetă, „spre gaming sau și mai simplu”. Analiza și cele patru machete sunt pe pânza https://claude.ai/artifact/AzG2H6m3sYbJUiHC6TQRrs (privată). Utilizatorul a ales direcția **C, hibrid** și **D, prima pornire**, iar bara de unelte de jos configurabilă a numit-o „o idee excelentă”. Decizia 259 în `decisions-pending.md`; etapele 1–3 făcute (deciziile 259–261).

## Ce am găsit

Bine: un limbaj vizual coerent (hârtie, cerneală, un singur accent), trei densități și spații de lucru, Commands… și scurtături editabile, configuratoare cu previzualizare din engine, cardul de selecție.

Unde se împiedică un începător, în ordinea gravității:

1. Ribbonul urmează modulele, nu munca: 12–14 taburi în ediția completă.
2. Nu există „pasul următor”: după primul perete nimic nu sugerează ușa, acoperișul, camerele, planșa.
3. Tipurile se aleg din liste de nume (stiluri de acoperiș, tipuri de pereți), desenul vine după.
4. Uneltele rapide 1–9 n-aveau nume și nu se puteau alege.
5. Proprietățile sunt un tabel uniform, fără esențiale.
6. Etichete MAJUSCULE spațiate (HUD, configuratoare).
7. Bara de stare e densă și negrupată.
8. Cantitățile nu se văd cât modelezi.

## Direcția

Estetica de joc nu, mecanicile da: arată ce poți face, ce urmează și ce ai obținut. Lapin de azi rămâne, ca mod **Pro**; deasupra lui, un mod **Simplu**. Aspectul (hârtie, accent cărămiziu, Inter) nu se schimbă.

## Etape

1. **Uneltele rapide alese de utilizator** — făcută (decizia 259). Nouă locuri, câte un set pe spațiu de lucru, la început cele ale ediției; o unealtă trasă din ribbon pe un loc intră în el (și îl lasă pe cel vechi), un loc tras peste altul le schimbă, clic dreapta pe un loc (Alege o unealtă…, Golește acest loc, Resetează), **View → Quick tools…** cu liste. Plutind jos, fiecare loc are numele uneltei; un loc gol după ultimul așteaptă o unealtă. Păstrate în browser (`lapin.hotbar`), ca setările interfeței. `ui/src/app/hotbar.ts`, `ViewportHud.tsx`, `QuickToolsDialog.tsx`.
2. **Un loc ține unealta și tipul** — făcută (decizia 260), tipul păstrat după nume: „Perete · Zidărie 38”, „Ușă 90×210”. Apăsat, pornește unealta cu tipul acela (`wallTypeId`, `doorTypeId`… din store). Tras din catalog (etapa 4) pe un loc.
3. **Prima pornire** (macheta D) — șabloanele făcute (decizia 261; recentele cu miniatură, misiunile și Simplu / Pro rămân): șabloane (casă parter, P+1, bloc, hală, doar 2D, pornire de la un IFC), recente cu miniatură, domeniul de lucru și modul Simplu / Pro alese de la început. `StartScreen.tsx`; șabloanele sunt proiecte `.lapin` livrate cu ediția.
4. **Catalogul vizual**: în stânga, lângă browser (Catalog | Proiect), tipurile cu secțiunea sau previzualizarea pe care engine-ul o face deja (`roofPreview`, `stairPreview`, desenele tâmplăriei), 2–3 cifre (grosime, U, cost); clic = tipul curent al uneltei, tras pe un loc rapid.
5. **Bara de lângă selecție**: 4–6 acțiuni și esențialele lângă element (înălțime, tip, întoarce, atașează la acoperiș), din cardul de selecție al modului Focus; meniul radial (clic dreapta ținut sau Q) cu 6–8 acțiuni.
6. **Proprietăți cu Esențiale / Toate**: 3–5 câmpuri sus pe fiecare clasă, restul pliat cu un rezumat pe rând („Straturi · 3 · 38 cm”); etichete scrise normal, nu cu majuscule.
7. **Bara de resurse**: arie construită, arie utilă, volume, cost din deviz, live sus, cu diferența ultimei acțiuni; o notă scurtă după fiecare acțiune („Perete 6,20 m · 2,3 m³”).
8. **Pasul următor și misiunile**: sugestii legate de ce există în model (contur închis → camere; camere → planșă), citite din diff-urile sesiunii; misiuni („Prima ta casă”, „Prima planșă”, „Cât costă?”, „Trimite la ingineri”) care se bifează când pasul e făcut în model.
9. **Taburile pe sarcini în modul Simplu**: Modelare (cu disciplina aleasă: Arhitectură, Structură, Instalații, Teren), Adnotare, Planșe, Cantități, Gestiune; ribbonul complet rămâne în Pro.
10. **Fantoma valid / invalid** la plasare: conturul verde când elementul se prinde, roșu când nu încape.

Fiecare etapă își actualizează pagina din ghid și scena ei, ca de obicei.
