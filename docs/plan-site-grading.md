# Plan: sistematizarea terenului (site grading), modulul `civil`

Cerut de Ionuț pe 23.11.2026, după subiectul de forum 230 „More topography sugestions” (benlungu), adunat cu cererile mai vechi: subiectul 144 (benlungu: limite neregulate, curbe de nivel desenate la cote exacte, pante numerice, editarea verticală a punctelor, drumuri care taie terenul), 187 (rețeaua de control, făcută: issue #113, decizia 751) și 157 (Rychlas: pante în pardoseli; issue #96). Dorința lui Ionuț: profilele longitudinale și transversale editate dintr-un grafic, pornind de la planul cu curbe de nivel; cât mai multe tehnici propuse de utilizatori, cât mai robust. Totul în modulul deschis `civil` (MIT, gratuit).

## 1. Ce au cerut utilizatorii și ce există

| Cerere | Sursa | Ce există (23.11.2026) | Ce lipsește |
|---|---|---|---|
| Teren direct dintr-un contur desenat sau ales (linii, arce), nu dreptunghi plat + limită | 230.1 | Teren din puncte, teren plat dreptunghiular, **Terrain boundary** care decupează (arce, colțuri concave; #82) | Terenul făcut direct pe contur → **T1, făcut** |
| Mânere pe vârfurile reale ale conturului; cotă absolută scrisă pentru un punct (3D, plan, proprietăți) | 230.2, 144 | Mânere 3D pe punctele ridicării (#82 T3), tras sau distanță scrisă; `moveTerrainPoint {z}` și `setTerrainPoints {z}` existau în motor, fără interfață | Vârfurile conturului ca puncte; cota absolută din interfață, pentru mai multe puncte → **T1, făcut** |
| Rețea de control (cușcă) | 187 | Făcut (#113, decizia 751, `Cage.cs`) | — |
| Curbe de nivel desenate la o cotă exactă; o curbă existentă trasă cu mouse-ul | 144 | Curbele se calculează din TIN; liniile de rupere (`breaklines`) există, din puncte sau din linework | Linia caracteristică (T2) |
| Pante numerice | 144 | Etichete de pantă (punct, două puncte), platforme cu taluzuri 1:N | Panta impusă unei linii (T2/T3), taluz de la o margine la teren (T5) |
| Subregiune → element **Hardscape** cu tip, straturi, bordură, independent de teren | 230.3 | Subregiuni = material pe teren, fără cotă proprie (#82 T4); aleea `IfcPavement` cu straturi pe traseu | **T4** |
| Petic de teren între două platforme pavate la cote diferite, cu marginile lipite de muchiile lor 3D | 230.4 | — | **T5** (teren între margini alese) |
| Rigole, borduri independente, margini de sprijin, accesorii pe traseu | 230.5 | Șanțul (fund pe profil), bordurile coridorului, zidul de sprijin | **T6** |
| Drumuri care taie terenul | 144 | Coridoarele taie terenul (`Paths.GroundCutters`), profil cu PVI-uri și racordări | Racordarea cu taluzuri la teren (T5, peste T2) |
| Pante în pardoseli | 157 / #96 | — | **T7**, același motor de pante |
| Profile longitudinale și transversale editate din grafic, pornind de la curbele de nivel | Ionuț | Configuratorul de coridor are profil longitudinal cu PVI-uri și secțiuni transversale (decizia 128) | **T3**, generalizat la orice linie |

## 2. Designul convenit: linia caracteristică (feature line)

O **linie caracteristică** e o polilinie 3D (cu arce) cu o cotă pe fiecare vârf sau cu o pantă dată (%, 1:N). Se editează:
- **în plan** (vârfurile, arcele, ca orice contur);
- **în 3D** (mânere verticale, cotă scrisă);
- **într-un grafic de profil** (stație × cotă, cu terenul existent și cel proiectat desenate dedesubt; puncte trase, cotă sau pantă scrisă între puncte, racordări parabolice/PVI ca la drumuri).

Terenul (TIN) folosește liniile caracteristice ca **linii de rupere**: triangulare constrânsă, suprafața trece exact prin ele (`Tin.Delaunay` are deja constrângerile, `Constrained.Apply`; `Terrains.Breaklines` le citește). De aici:
- marginile unui hardscape, bordurile, rigolele, coronamentele zidurilor și marginile platformelor sunt linii caracteristice;
- **editarea unei curbe de nivel** = o linie caracteristică la cotă constantă; terenul se retriangulează în jurul ei, iar punctele dintre curbe se interpolează din nou;
- **profilele longitudinale/transversale** = o linie trasă peste curbele de nivel, al cărei grafic arată terenul și îl editează, cu o lățime de influență laterală;
- **sistematizarea între margini** = un teren triangulat doar din marginile alese.

Pantele pardoselilor (157/#96) folosesc același motor de pante.

Reguli din `CLAUDE.md` care se aplică peste tot: o singură regulă pentru curbe (`Chords`), un singur rezolvitor de vizibilitate, GlobalId-uri stabile la refacere, ce se poate deriva nu se stochează (cotele interpolate, profilele terenului), fiecare comandă un pas de anulare, `IProjectUpgrade` pentru proiecte vechi doar când chiar trebuie, extinde fără să strici, configurabil desenând sau cu formule.

## 3. Etapele, în ordinea aprobată

### T1. Teren direct din contur; vârfurile conturului ca puncte; cota absolută scrisă — **făcut (23.11.2026)**
Vezi §5.

### T2. Liniile caracteristice ca linii de rupere; curbe de nivel desenate și editate
- Element nou `LapinFeatureLine` (sau atribut al terenului? de hotărât, §6 Î1), legat de teren; cote pe vârfuri sau pantă; arce prin `ArcOutline`/`Chords`.
- Terenul le ia ca `breaklines` derivate (nu copiate): `Terrains.Breaklines(e)` + liniile legate.
- **Curbă de nivel desenată la o cotă** (unealtă: polilinie + cotă) = linie caracteristică orizontală.
- **Curbă existentă trasă**: un clic pe o curbă de nivel o ia ca linie caracteristică la cota ei (lanțul `ChainedContours`), apoi se trage; punctele ridicării dintre curba veche și cea nouă se reinterpolează (zonă de influență = până la curbele vecine).
- Teste: suprafața trece exact prin linie (cotă pe orice punct al ei, 1e-9); o curbă mutată mută curba de nivel desenată la aceeași cotă; puncte vechi neatinse în afara zonei; GlobalId neschimbat; un pas de anulare.

### T4. Elementul Hardscape
- `IfcPavement` (4.3) sau `IfcSlab` USERDEFINED „Pavement”/PAVING (IFC4), ales în setările proiectului (§4).
- Tipuri cu straturi (pavaj, pat de nisip, fundație de balast), materiale, grosimi; contur, cotă, pantă (un plan înclinat sau cote pe vârfuri) editabile oricând; **independent** (fără teren) sau **tăiat în teren** (`IfcRelVoidsElement`, ca aleea); bordură pe perimetru (opțională, pe laturi).
- Subregiunile existente se pot transforma în Hardscape la cerere (`civil.subregionToHardscape`); proiectele vechi rămân neschimbate.
- Teste: volume pe straturi, aria, terenul scăzut cu corpul, bordura pe laturile alese, conversia subregiunii cu aceeași arie, IFC 4.3 și IFC4.

### T3. Editorul de profil (grafic) pentru orice linie
- Același grafic pentru o linie caracteristică, o linie de secțiune peste teren și axul unui drum (configuratorul de coridor se refolosește: `CorridorDialog`, stații, PVI-uri, racordări).
- Pe o linie de secțiune peste teren: graficul arată terenul; editarea lui modifică terenul pe o **lățime de influență** laterală (descreștere liniară sau netedă până la terenul neatins).
- Secțiuni transversale la stații, editabile la fel.
- Teste: profilul citit = terenul pe linie (eșantionare pe muchiile TIN, nu pe pas fix); un PVI mutat schimbă terenul doar în lățimea de influență; pantele scrise între PVI-uri dau cotele exacte.

### T5. Sistematizare
- **Teren între margini alese** (230.4): un petic triangulat doar din liniile caracteristice/muchiile 3D alese (marginile a două hardscape-uri la −2,00 și −1,00, cu arce), vârfurile lipite de muchiile lor 3D.
- **Taluz de la o margine la teren**: pantă țintă (1:N) sau cotă țintă, până întâlnește terenul (ca taluzurile platformei, generalizat pe o linie deschisă).
- Săpătură/umplutură pe fiecare operație (`Lapin_EarthworkQuantities`).
- Teste: petic închis fără goluri între margini, cotele marginilor exacte; taluzul întâlnește terenul la pantă; volume față de calculul pe grilă.

### T6. Rigolă, bordură, accesorii
- Rigolă independentă (secțiuni de catalog, radier la o pantă), bordură independentă, accesorii repetate pe un traseu (stâlpi, guri de scurgere, bolarzi), toate legate de linii caracteristice.
- Teste: radierul la panta dată, numărul accesoriilor la pas, cantități.

### T7. Pante în pardoseli (157/#96)
- Același motor de pante pe fața de sus a unei plăci (puncte de cotă, linii de rupere, pante spre sifoane).

### T8. Ghid, scene, video
- Paginile `docs/user/site.md` (EN + RO) la fiecare etapă, scena fiecărei etape, la sfârșit un tutorial video.

## 4. Alegeri IFC

- Terenul: `IfcGeographicElement` TERRAIN, ca acum; punctele conturului sunt puncte ale lui (nimic nou în IFC).
- Linia caracteristică: în IFC 4.3 `IfcAnnotation` cu reprezentare `Axis`/`FootPrint` 3D, sau `IfcAlignment` doar pentru axe de drum; în IFC4 `IfcAnnotation`. Intenția (cote, pante) în `Lapin_Intent`.
- Hardscape: `IfcPavement` (4.3), `IfcSlab` USERDEFINED „Pavement” (IFC4), cu `IfcMaterialLayerSetUsage`; setarea proiectului alege, implicit `IfcPavement` în 4.3.
- Rigola: `IfcEarthworksCut` TRENCH sau `IfcPipeSegment` GUTTER (de hotărât, Î5); bordura: `IfcKerb` (4.3), `IfcBuildingElementProxy` USERDEFINED „Kerb” (IFC4).

## 5. Ce s-a făcut

**T1 (23.11.2026, ramura `feat/site-grading`, decizia 791).**
- Motor, `engine/src/Modules/Civil/Lapin.Civil/TerrainOutlines.cs`:
  - `civil.addTerrainOnOutline {points [[x, y] | [x, y, z]], arcs?, fromId?, elevation?, ground?, spacing?, storeyId?, name?, datum?, interval?, majorEvery?, depth?}`: fiecare vârf al conturului, așa cum e construit (arcele în corzi prin `ArcOutline.Points`, deci regula `Chords`), e un punct al terenului; colțurile își păstrează cota dată, apoi cota terenului de dedesubt (`ground`), apoi `elevation`; un vârf de pe un arc ia cota dintre colțurile laturii lui, după lungime. Limita terenului e conturul, deci terenul se termină exact pe el. Interiorul primește puncte pe o grilă (implicit ca terenul plat: a opta parte din latura scurtă, minim 2 m; 0: fără), niciunul mai aproape de contur decât o treime din pas, cu cotele suprafeței conturului (sau ale terenului de dedesubt). `fromId`: o cameră (`IfcSpace`), o parcelă sau o zonă GIS (`LapinSiteFeature`, cu cotele ei), parcela unui teren, o platformă (la cota ei). Cotele interpolate se rotunjesc la nanometru, ca un teren orizontal să fie exact orizontal (altfel o curbă de nivel la cota lui rătăcea prin el).
  - `civil.outlineToPoints {id}`: la cerere, vârfurile limitei unui teren decupat devin puncte la cota terenului de acolo (fără tușele pensulei); terenul nu se mișcă (testat la 1e-9); pe un teren lucrat cu pensula se poate mișca puțin pe contur și rezultatul spune cât (`moved`). Refuzat pentru un teren păstrat ca triunghiurile fișierului (LandXML).
  - Vârfurile de pe arce n-au mânere implicit și își urmează colțurile (`TerrainOutlines.Settle`, chemat de `moveTerrainPoint`, `setTerrainPoints` și după orice schimbare a limitei): reinterpolate după lungimea arcului între colțuri și vârfurile cu cotă proprie (`outlineOwn`: locurile lor; un vârf de arc setat direct devine unul), în același pas.
  - Atributul `outlinePoints: true` spune că punctele conturului sunt ale terenului. Cât îl are: `setBoundary` (un colț tras își păstrează cota, unul adăugat ia cota terenului, vârfurile dispărute își iau punctele cu ele; scoaterea limitei păstrează punctele și scoate atributul), `splitTerrain` (capetele tăieturii devin puncte pe ambele jumătăți) și `mergeTerrains` (vârfurile conturului unit devin puncte; cele de pe cusătură rămân) îl păstrează. Fără atribut, totul merge ca înainte; niciun `IProjectUpgrade`.
  - Convert to… → **Terrain inside** (`terrainOutline`, contur închis; cercul ca patru sferturi de arc; cotele DXF-ului, altfel cota dată, altfel terenul de dedesubt).
- Interfață (`ui/src/modules/civil/`):
  - **Terrain on outline** (TO): bara de contur (poligon cu arce, dreptunghi, cerc, în pereți, formă desenată, cameră) și **At a height / On the ground**. **Terrain on picked outline**: cameră, parcelă/zonă, parcela unui teren, platformă, formă desenată (prin Convert to…).
  - Mânerele 3D: un punct tăiat de limită nu mai are mâner; cu **all points**, un clic pe mâner alege punctul (desenat plin, fără comandă). În proprietăți, sub **Points**: câte sunt alese, câmpul de cotă (absolută; cea comună dacă o au), **set elevation** (`civil.setTerrainPoints {z}`, un pas), **pick the outline's** (colțurile; toate punctele cu bifa), **let go**, bifa **all outline points** (mânere și pe vârfurile arcelor; implicit oprită). În plan (doar în timpul editării, doar în vederile 2D): mânere pe puncte cu meniul **Set elevation…** / **Pick point**; colțurile limitei care stau pe un punct primesc același meniu lângă comenzile lor de colț (`GripLayer.onContextMenu` le pune acum pe amândouă).
  - **outline as points** lângă **Boundary**.
- Teste: `TerrainOutlineTests` (12: arcul reinterpolat la schimbarea unui colț, cu anulare; vârful cu cotă proprie păstrat; L cu colțuri-puncte și arie exactă, cerc în corzi cu toate vârfurile puncte și fără curbă de nivel la cota lui, cote pe colțuri și arc liniar, pe teren, din cameră și platformă, conversia fără mișcare, terenul fără atribut neschimbat, colț tras/adăugat/cotă scrisă/limită scoasă, împărțire și unire, Convert to…); `pointGrips.test.ts` (+5, între ele mânerele doar pe colțuri implicit și bifa **all outline points**). Ghidul: Site → „A terrain on an outline” și „An elevation typed”, scena `terrain-outline`.

## 6. Întrebări deschise

- **Î1.** Linia caracteristică: element propriu (`LapinFeatureLine`, selectabil, cu GlobalId, exportat) sau atribut al terenului (ca `breaklines`)? Propunere: element propriu, legat de teren printr-o relație, ca platformele.
- **Î2, hotărâtă (23.11.2026, coordonatorul, alegerea lăsată de Ionuț).** Vârfurile de pe arce rămân puncte adevărate ale TIN-ului (terenul se termină tot exact pe contur), dar fără mânere implicit: doar colțurile (la un cerc, cele patru puncte de sfert). Ele își urmează colțurile: când cota unui colț se schimbă (tras, scrisă, `setTerrainPoints`, „pick the outline's”), vârfurile dintre cele două colțuri se reinterpolează după lungimea arcului, în același pas de anulare (`TerrainOutlines.Settle`). Un vârf căruia i s-a dat cota lui (`outlineOwn`) și-o păstrează și împarte interpolarea. Bifa **all outline points** din proprietăți pune mânere pe toate.
- **Î3.** Lățimea de influență la editarea din profil (T3): liniară, netedă (cosinus) sau după rețeaua de control (Shepard, ca `Cage.cs`)?
- **Î4.** Hardscape în IFC4: `IfcSlab` USERDEFINED „Pavement” (ca aleea azi) sau `IfcCovering` FLOORING?
- **Î5.** Rigola independentă: `IfcEarthworksCut` TRENCH (ca șanțul) sau un element de scurgere (`IfcPipeSegment` GUTTER / `IfcChannel`)?
- **Î6.** Teren pe contur „pe teren”: când nu e niciun teren dedesubt într-un vârf, ia `elevation` (azi 0). E bine, sau refuzăm?
