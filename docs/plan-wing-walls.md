# Plan: portaluri de tunel, culee de pod și aripile lor (modulul `civil`)

Cerut pe 23.11.2026 de utilizator: un generator de geometrie pentru pereții tunelurilor și ai podurilor. Un perete drept, vertical, urmat de segmente care fac un unghi cu el în plan și au și o înclinare (fruct); capetele lor coboară de la cota generală a peretelui drept spre o cotă mai joasă la capătul liber. Generate cu SubD și cu tehnici parametrice și de configurator.

## Ținta

- Aripile unui portal de tunel sau ale unei culei: drepte, în U (paralele cu drumul), evazate (în unghi), răsucite (fața trece de la fructul peretelui la panta taluzului).
- Culeea cu aripi; mai târziu căptușeala tunelului (bolta), un profil dus de-a lungul unui aliniament.
- Geometrie exactă, nu tesselare: fețe plane unde sunt plane, iar fața răsucită ca suprafață riglată exactă.
- Cantități: beton, cofraj pe fiecare față; planuri și secțiuni cu fața înclinată tăiată corect; IFC.

## Răspunsurile utilizatorului (23.11.2026)

1. **Unde stă:** în modulul open `civil` (MIT, gratuit), ca extindere a zidului de sprijin existent.
2. **Ce face:** amândouă: portal/culee cu aripi și, într-o etapă ulterioară, căptușeala tunelului (o boltă: un profil transversal dus de-a lungul unui aliniament).
3. **IFC configurabil**, ca setare pe element sau pe tip: aripile ca `IfcWall` RETAININGWALL simplu, sau un ansamblu (IFC 4.3: `IfcBridgePart` ABUTMENT cu aripile; tunelul `IfcTunnelPart` dacă schema scrisă îl are; în IFC4, `IfcWall`/`IfcElementAssembly` ca înlocuitori).

## Proiectul (analiza aprobată)

- **Parametri pe segment:** lungimea și unghiul în plan față de segmentul dinainte; fructul feței și al spatelui (de exemplu 10:1); cota coronamentului la început și la capăt (liniar, mai târziu o curbă); grosimea la coronament și la bază; talpa (consola față, consola spate, pintenul, cota).
- **Exact, nu aproximat:** un segment drept cu fruct constant și coronament liniar are fețe plane, deci se construiește direct ca B-rep. Două segmente cu unghiuri sau fructuri diferite se întâlnesc pe dreapta exactă de intersecție a planelor lor.
- **Aripa răsucită:** fața care trece de la vertical (sau fructul peretelui) la panta taluzului este o suprafață riglată, un petec B-spline exact (F2, `Lapin.Geometry/BSpline.cs`: fețe exacte pe lângă tesselare, IfcAdvancedBrep și STEP AP214; deciziile 780/781). SubD doar pentru tranziții libere, opțional.
- **Configuratorul** (S3): presetări pentru portal cu aripi drepte, aripi în U, aripi evazate, aripi răsucite, culee cu aripi. Previzualizarea e a motorului, făcută de același cod ca elementul. Parametrii pot fi formule (`{= …}`), de exemplu cota capătului liber = cota taluzului + 0,50, lungimea aripii din panta 2:3. Traseul se poate da și desenat (polilinie), iar secțiunea ca profil desenat și salvat, pe lângă cel codat (regula din `docs/plan-user-extensibility.md`).
- **Ieșiri:** volumul de beton; cofrajul pe fiecare față (fața înclinată, spatele, capetele, coronamentul), extinzând `FormworkArea`; planuri și secțiuni; IFC ca mai sus; armarea mai târziu.

## Geometria, cum a fost aleasă (S1–S2)

- **Fructul se măsoară în secțiunea perpendiculară pe segment**, ca în desen: n:1 înseamnă n pe verticală la 1 pe orizontală (0 sau gol: vertical). Intern e „fuga” k = 1/n pe unitatea de înălțime; cel mai culcat fruct acceptat e 1:5.
- **Fețele pleacă din muchiile coronamentului.** Coronamentul își păstrează grosimea pe toată lungimea, iar elevația se îngroașă unde e mai înaltă: w_față(x, z) = −t/2 − (cota(x) − z)·k_față(x), w_spate(x, z) = t/2 + (cota(x) − z)·k_spate(x). Așa arată o aripă reală (subțire la capătul liber, groasă lângă culee). Cu un coronament în pantă și fruct constant, fața e tot un plan; baza ei în plan nu mai e paralelă cu coronamentul.
- **Grosimea la bază** e a doua cale de a da spatele: din ea se calculează fructul spatelui acolo unde elevația e cea mai înaltă.
- **La colțuri, coronamentul rămâne orizontal pe colț**, până unde se întâlnesc fețele, inclusiv tălpile; panta fiecărui segment se întinde între colțuri. Altfel, două coronamente în pantă care se întâlnesc într-un colț cu grosime nu pot fi continue fără trepte (patru plane nu trec printr-un punct). Un colț cu cote diferite de o parte și de alta rămâne o treaptă, iar coronamentul mai înalt acoperă colțul.
- **Îmbinarea:** fiecare segment e construit din bucăți cu fețe plane (sau cu fața răsucită): panta, plus o bucată orizontală peste fiecare colț. Bucata de colț e tăiată de planul feței exterioare a vecinului și oprită unde începe panta vecinului. Bucățile se unesc printr-o singură operație booleană (manifold). Fețele exterioare se întâlnesc pe dreapta lor de intersecție; cele interioare se întretaie exact, prin uniune.
- **Fața răsucită:** fructul variază liniar de la începutul pantei la capătul ei. Fața e riglată între baza de pe talpă (o parabolă în plan) și muchia coronamentului (o dreaptă), deci e exact un petec B-spline de grad 2 × 1 (`BatteredWalls.Patch`). Rețeaua lui se tesselează după regula corzilor (`BSplineSurface.Grid`, plus torsiunea |Suv|, pentru ca un triunghi să nu se depărteze de suprafață cu mai mult de 1 mm). Volumul și aria se calculează pe suprafața adevărată: volumul ca integrală (cubică, Gauss cu 5 puncte, exact), aria prin Gauss 10 × 10.
- **Ce nu se schimbă:** un zid fără fructuri, fără segmente proprii, fără consolă, călcâi sau pinten se construiește ca înainte (`RetainingWalls`): geometrie, cantități și desene identice. Datele noi sunt atribute opționale, deci nu e nevoie de un `IProjectUpgrade`.

## Etape

| Etapa | Ce | Stare |
|---|---|---|
| **S1** | Zidul de sprijin extins: fruct față/spate pe zid și pe segment, grosime la coronament și la bază, coronament liniar pe segment (început/capăt), cota tălpii pe segment, consolă față, consolă spate, pinten; îmbinări exacte; cantități (volum, cofraj pe fețe); plan și secțiune; IFC (planele exacte ca IfcFacetedBrep); câmpurile din panou în toate limbile | făcut (23.11.2026) |
| **S2** | Aripa răsucită: opțiunea „Warped” pe segment, fața de la fructul de început la alt fruct la capăt, petec B-spline exact, tesselat după `Chords`, exact în IFC (IfcAdvancedBrep) și STEP; volumul și cofrajul pe suprafața adevărată | făcut (23.11.2026) |
| **S3** | Configuratorul de portal și culee: presetări (aripi drepte, în U, evazate, răsucite; culee cu aripi), previzualizare de motor, formule `{= …}`, traseul desenat și profilul salvat, alegerea IFC (`IfcWall` simplu sau ansamblu `IfcBridgePart`/`IfcTunnelPart`, înlocuitori în IFC4); pagina din ghid și scena ei | de făcut |
| **S4** | Căptușeala tunelului: profilul boltii (codat sau desenat) dus de-a lungul unui aliniament (codul drumurilor curbe și al aliniamentelor, `Lapin.Alignments`), exact unde se poate (cilindri, conuri, B-spline), cantități, IFC `IfcTunnelPart` | de făcut |
| **S5** | Ghidul complet (EN + RO) și un tutorial video (`../lapin-film`) | de făcut |

Paginile de ghid și scena așteaptă S3: S1 nu schimbă nicio pagină existentă (ghidul nu descrie câmpurile zidului de sprijin). Câmpurile noi apar în panou, iar pagina va fi scrisă odată cu configuratorul.

## Licențe

Nimic nou. Geometria e codul propriu Lapin (MIT); booleanele sunt manifold, deja folosit (Apache-2.0, prin API-ul C).

## Teste

- `WingWallTests` (xUnit, 9):
  - un zid fără fructuri rămâne cum era;
  - fructul feței: fețe plane, volume, cofraj pe fețe și secțiunea, verificate față de valori calculate de mână;
  - coronamentul în pantă: grosimea păstrată, integrala volumului; undo;
  - colțul în L cu fruct: vârfurile pe dreptele de intersecție ale planelor, iar volumul după Pappus (aria secțiunii × drumul centrului ei);
  - aripa care coboară din colț: coronament orizontal pe colț, fără trepte;
  - aripa răsucită: petecul trece prin fața dată de formulă; volumul exact (Simpson pe cubică); aria față de o grilă fină de 400 × 400; corpul exact (1 B-spline, 9 plane);
  - portalul cu două aripi răsucite: închis, exact;
  - IFC4 cu IfcAdvancedBrep, citit înapoi cu volumul lui; zidul plan ca faceted brep, citit înapoi;
  - lungimea și rotirea unui segment, cu cele de după păstrate.
- `ui/src/modules/civil/retainingWall.test.ts` (vitest): lungimile și unghiurile segmentelor, valorile proprii, citirea fructului.

## Ce s-a făcut

**S1 și S2 (23.11.2026, ramura `feat/wing-walls`, decizia 789).**

- **Motorul:**
  - `BatteredWalls.cs` (Civil): segmentele rezolvate (`Leg`), colțurile, bucățile, uniunea, planul, secțiunea, cantitățile, corpul exact.
  - `ExactRegions.cs` (Geometry): corpul exact citit dintr-un poliedru ale cărui fețe stau pe plane și pe câteva suprafețe curbe. Fețele de pe o suprafață se adună într-o singură față, iar muchiile se taie unde se întâlnesc trei fețe. Muchia dintre un petec și un plan e curba prin punctele ei, parametrizată după distanța de-a lungul segmentului, deci parabola bazei e reprodusă exact de o pătratică.
  - `RetainingWallExactBodies` (`IIfcExactBodies`) dă corpul exact pentru IFC și STEP când o față e răsucită.
- **Comenzi:**
  - `civil.updateRetainingWall`/`addRetainingWall` primesc `frontBatter`, `backBatter`, `baseThickness`, `toe`, `heel`, `keyWidth`, `keyDepth`, `keyAt` (null șterge valoarea).
  - `civil.setRetainingWallSegment {id, index, frontBatter, backBatter, frontBatterEnd, backBatterEnd, warp, thickness, baseThickness, topStart, topEnd, footingLevel, length, angle}` este un singur pas de undo. Lungimea și rotirea mută punctele de după, iar segmentele următoare își păstrează lungimile și rotirile.
  - Catalogul de comenzi e reîmprospătat.
- **Cantități:** pe lângă cele vechi, `FormworkFront`, `FormworkBack`, `FormworkEnds`, `FormworkFooting` și `CrownArea`. `FormworkArea` e suma fețelor cofrate. Fața răsucită intră cu aria ei adevărată, iar volumul cu cel exact (fațetele lipsesc sub 0,1 %).
- **Planul:**
  - coronamentul tăiat, cu colțurile pe intersecții;
  - piciorul feței înclinate se vede;
  - piciorul spatelui înclinat și talpa sunt cu linie întreruptă;
  - rosturile apar ca înainte.
- **Secțiunea** de-a lungul zidului (`Corridors.Section`) arată fața înclinată, talpa și pintenul. Secțiunile obișnuite taie corpul.
- **Interfața:** în panoul zidului apar fructul feței, spatele (după fruct sau după grosimea la bază), consola față, consola spate și pintenul. Lista „Segments” deschide fiecare segment cu lungimea, rotirea, coronamentul la început și la capăt, grosimea, fructurile, cota tălpii și „Warped” cu fructurile de la capăt. Textele sunt în toate cele 11 limbi.

**Ce a rămas:**

- S3–S5.
- Coronamentul după linia roșie (profilul proiectat) e luat liniar între capetele fiecărui segment.
- Curbele traseului nu sunt urmate de un zid cu fruct (o notă o spune).
- O consolă dată explicit 0 sub o față răsucită lasă talpa tangentă la piciorul curb (implicit se lasă cel puțin 5 cm).

## Întrebări deschise

1. **Unde pleacă fructul:** din muchia coronamentului, cu grosimea coronamentului constantă (aleasă aici, ca la aripi), sau din bază, cu baza paralelă cu traseul și coronamentul care se lățește unde coboară?
2. **Panta peste colț:** coronamentul rămâne orizontal pe colț, iar panta aripii începe unde se termină colțul (dincolo de spatele tălpii peretelui principal). E bine așa, sau panta trebuie să înceapă din axul colțului, cu o mică treaptă acceptată?
3. **Capătul liber:** acum e vertical și perpendicular pe segment. Trebuie și un capăt înclinat (după taluz) sau rotit (paralel cu drumul)?
4. **Fața răsucită:** acum se răsucește doar pe lungimea pantei, iar pe colț rămâne plană. Trebuie răsucită pe toată lungimea segmentului?
5. **S3, IFC:** ansamblul (`IfcBridgePart` ABUTMENT) ține aripile ca `IfcWall` separate (un element pe segment) sau ca un singur corp?
