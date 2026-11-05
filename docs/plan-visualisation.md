# Vizualizare realistă, anturaj, studii de lumină și coliziuni

Cerut de utilizator la 05.11.2026: randarea realistă, copaci, oameni și mașini, studiile de lumină și detectarea coliziunilor, după **BimGo** (`~/Documents/GitHub/bimgo`, fork-ul `ionuting/BimGo` al proiectului lui Aussie BIM Guru), adaptate la Lapin și **gratuite**. Decizia 309 în `decisions-pending.md`.

BimGo e sub **MIT-0**: codul, formulele și constantele lui se pot prelua fără obligații. E un vizualizator de plimbare pentru Revit, pe Windows, cu un motor OpenGL propriu în C#. Lapin desenează cu three.js, în desktop și în browser, așa că **se preiau algoritmii, constantele și resursele, nu motorul de randare**. Tot ce urmează e în modulele deschise; nicio dependență nouă: three 0.182 are deja în `examples/jsm` umbrele în cascadă (`CSM`), ocluzia ambientală (`GTAOPass`), strălucirea (`UnrealBloomPass`), cerul (`Sky`), oglinzile (`Reflector`) și sondele de lumină, toate MIT.

## Granița

- **În platformă (deschis)**: poziția soarelui, panoul **Soare** (dată, oră, locul proiectului), vederea realistă și profilele de calitate, texturile, fotografiile, panoramele și video-ul. Fiecare vedere 3D le folosește, indiferent de ediție.
- Modulul deschis **`entourage`** (`requires: core`): copacii, oamenii și mașinile, în 3D și în planuri.
- Modulul deschis **`daylight`** (`requires: core, architecture`): studiile de lumină în camere.
- Modulul deschis **`clash`** (`requires: core`): detectarea coliziunilor între seturi de elemente.
- Rămân în **`urban` (premium)**: planurile de umbre pe teren, umbra volumului construibil și orele de soare pe parcelă și pe fațadele vecinilor (însorirea vecinătăților). Poziția soarelui din `urban/Sun.cs` (NOAA/Meeus, cu refracție) se mută în platformă, ca să o folosească vederea gratuită; `urban` o citește de acolo (întrebarea Î1).

## 1. Vederea realistă

Azi `Viewport3D.tsx` are o lumină de emisferă, un soare fix, o singură hartă de umbră de 2048² (doar când un modul aprinde soarele), fără tone mapping, fără mediu (în afară de `RoomEnvironment` pe metale), fără post-procesare. Sticla e doar transparentă: `Transmission` și `Ior` din `MaterialLook` se văd doar în previzualizarea materialului. Instanțele (`instancing.ts`) au un material alb comun, fără aspect.

**Etapa 1, vederea realistă de bază:**

- **Tone mapping** (Neutral în profilul Mediu, AgX în Realist, cu expunere); aspectul de azi rămâne profilul **Desen** (fără tone mapping, lumină plată), cel realist se alege din View → Display quality.
- **Cerul**: `Sky` (Preetham, după poziția soarelui) ca fundal și ca mediu (`scene.environment` prin PMREM, refăcut când se mută soarele), în locul fundalului plat; culoarea soarelui după înălțime, ca la BimGo (de la (1, .62, .36) la răsărit la (1, .97, .92) la amiază).
- **Soarele adevărat**: poziția din dată, oră și locul proiectului (`Location` + `GeoRef` dau latitudinea și longitudinea; nordul real din `NorthAngle`), ora de vară după regula europeană sau fusul IANA (`Intl` în interfață, `TimeZoneInfo` în engine). Panoul **Soare** din fila View: zi (echinocții, solstiții), oră, rulează ziua; `sunlight.ts` primește în sfârșit o sursă.
- **Umbrele**: o hartă (2048 în Mediu, 4096 în Realist) potrivită pe ce privește camera (cel puțin 30 m, cât modelul dacă e mai mic), nu pe tot modelul. Umbrele în cascadă (`CSM`, treptele lui BimGo: 1 × 2048 la 60 m, 3 × 2048 la 120 m, 4 × 3072 la 200 m) rămân pentru când plimbarea prin modele mari le cere: `CSM` cere fiecărui material un shader schimbat.
- **Terenul**: în profilul Realist, sub model, unde proiectul nu are teren (`civil`), o pajiște care primește umbrele; grila se ascunde.
- **Ocluzia ambientală** (`GTAOPass`, raza 0,6 m ca la BimGo), doar pe lumina ambientală.
- **Strălucirea** doar pe materialele cu `Emissive`, mai târziu: `UnrealBloomPass` pe toată imaginea încețoșează cerul și pereții însoriți (încercat); trebuie o trecere care să înflorească doar ce emite (BimGo scrie culoarea emisivă într-o țintă proprie).
- **Sticla**: `MeshPhysicalMaterial` cu `transmission`, `ior` și `thickness` pentru materialele cu transmisie, în profilul realist; umbrele colorate prin sticlă mai târziu.
- **Instanțele cu aspect**: un `InstancedMesh` pe (parte × material), cu rugozitatea, metalicitatea, emisia și textura materialului, nu un alb comun.
- **Profilele de randare**: View → Display quality capătă, lângă precizie, **Desen** (azi), **Mediu** (soare, umbre, AO, lumina cerului) și **Realist** (cer, teren, sticlă cu transmisie), păstrate **pe calculator**, nu în proiect (un calculator slab desenează proiectul unui coleg cum poate); randarea rămâne la cerere (`invalidate()`). Profilele Mediu și Realist au mereu soare: ziua și ora din View → Sun and shadows (`ui/src/modules/core/sun.ts`, interogarea `sun.at` din platformă), implicit azi la 14:00.
- Browserul primește tot (WebGL2); un calculator slab coboară singur un profil dacă cadrul trece de 50 ms.

**Etapa 2, texturi și reflexii:**

- **Texturi reale CC0** de la ambientCG, ca la BimGo (21 de hărți de culoare, 512 px; lista lui în `THIRD-PARTY-NOTICES.txt`), plus hărțile de normală și de rugozitate, pe care BimGo nu le are. Se leagă de materiale ca azi texturile desenate (`textures.ts`): după categoria materialului și după nume (alias-urile lui `ProxyCatalog.Suggest`, cu mărimea reală a repetiției: cărămida la 0,215 m etc.), cu desenul procedural ca rezervă. Livrate cu aplicația, nu în fișierul `.lapin`; `check:licenses` capătă o listă pentru resurse.
- **Sonde de reflexie** pe camere, după BimGo: una pe `IfcSpace` care are o suprafață reflectantă, la 1,70 m peste pardoseală, o grilă la 8 m în camerele lungi, proiecția pe cutia camerei (parallax), amestecate pe aproximativ 1 m la granițe; coapte câte două fețe pe cadru, cele mai apropiate întâi, refăcute când se mută soarele sau se schimbă modelul.
- **Reflexii exacte în fotografii**: oglinzi plane (`Reflector`) pentru cele mai mari suprafețe de sticlă și de oglindă din câmp, după scorul lui BimGo (arie × orientare / distanță²), doar în fotografii.

## 2. Anturajul: copaci, oameni, mașini

BimGo are 15 billboard-uri **desenate din cod** (`Core/Scene/EntourageArt.cs`: copaci, arbuști, gard viu, iarbă, patru oameni, o mașină și un SUV), deci fără nicio imagine străină; se întorc spre cameră pe axa verticală și, în trecerea de umbră, spre soare.

Lapin e nativ IFC, așa că anturajul e **element**, nu decor:

- **`LapinEntourage`** pe etaj, cu tip: **copacii și arbuștii** sunt `IfcGeographicElement` VEGETATION (IFC 4.3), **oamenii și mașinile** `IfcBuildingElementProxy` cu ObjectType „Entourage”, scriși în IFC doar la cerere (opțiunea exportului, implicit doar vegetația).
- **Tipul** spune: specia sau felul, înălțimea, diametrul coroanei, cu variație aleatoare (±8 % ca la BimGo) și oglindire; vara sau iarna (coroana fără frunze) pentru copacii foioși.
- **În 3D**: billboard-ul (desenul lui BimGo, transpus pe canvas în TypeScript), cu umbră spre soare; mai târziu un corp simplu (trunchi și coroană din volume) pentru exporturi și pentru vederile de sus. Imaginile utilizatorului (PNG cu transparență) intră ca tipuri proprii și se salvează în proiect.
- **În planuri și în planul de situație**: simbolul 2D al tipului (coroana ca cerc cu contur, mașina ca siluetă de sus, omul ca un oval), la scară, tipărit; asta lipsește azi și e util pe planșe chiar fără randare.
- **Așezare**: un clic, sau **de-a lungul unei linii** la un pas (aliniamente de copaci, parcări), sau **împrăștiat într-un contur** (o pădure, un grup de arbuști), cu densitate. Copacii stau pe teren (`civil`) când există.
- Extrasul de vegetație (specii, număr, înălțimi) prin `IScheduleSource`.

## 3. Fotografii, panorame, video

- **Fotografii la rezoluție mare** prin **plăci** (`camera.setViewOffset`): orice mărime, fără limita plăcii video care o oprește pe a lui BimGo (randarea unică la 1–4× fereastra); expunere, profilul realist forțat.
- **Panorame 360°**: șase vederi de 90° (BimGo: 96°, cu suprapunere) cusute într-o imagine echirectangulară de 4K sau 8K, JPEG cu metadatele GPano, ca să se deschidă ca panoramă.
- **Trasee de cameră**: chei cu oprire și accelerare, curbă Catmull-Rom centripetală (`CatmullRomCurve3` din three), editate în 3D; turul punctelor de vedere salvate.
- **Video**: cadru cu cadru, determinist (nu captura în timp real de azi din simularea 4D), prin **WebCodecs** (`VideoEncoder` H.264, în WKWebView, WebView2 și Chrome) și un **multiplexor MP4 scris de Lapin** (ISO BMFF: ftyp, moov, mdat), fără dependențe, cum sunt scrise glTF, USD și Fragments; unde WebCodecs lipsește (WebKitGTK pe Linux), cadrele ca ZIP sau `MediaRecorder` ca azi. 1080p sau 4K, 30 sau 60 fps.
- **Time-lapse cu soarele**: de la răsărit la apus, cu umbrele și cerul care se schimbă; aceeași mașinărie ca video-ul.

## 4. Lumina artificială și noaptea (etapa 5)

- Corpurile de iluminat (`IfcLightFixture` din `mep`, plus orice element cu un material emisiv marcat ca sursă) devin lumini: fluxul în lumeni și temperatura de culoare pe tip (`Lapin_Light`, nu `Pset_`), implicit 1000 lm și 3500 K, ca la BimGo.
- **Profile IES** (LM-63): cititorul lui BimGo (`Core/Scene/LightProfiles.cs`, tip C, 1986–2002) portat în engine, plus cele cinci profile generate din formule (spoturi înguste și medii, reflector larg, iluminare de perete, pendul); `IESSpotLight` și `IESLoader` din three le desenează. Nicio fotometrie de producător livrată.
- Cât se vede live: lumini cu umbre până la un buget pe profil (BimGo: 32 cu umbre, 480 fără), toate în fotografii și video.
- **Noaptea**: stele, lună (poziția după elementele lui Schlyter, `LunarPosition.cs`), cerul de amurg.
- Ploaia, zăpada, iarba animată și apa cu valuri rămân pentru mai târziu; sunt efecte de prezentare, nu de proiectare.

## 5. Studiile de lumină (`daylight`)

În **engine**, nu în interfață: rezultatele intră în extrase, pe planșe și în CSV, se testează și merg la fel în browser (worker-ul). Algoritmii sunt ai lui BimGo (`Scene/SunHours.cs`, `Scene/Daylight.cs`, `Game/SunHoursStudy.cs`), cu constantele lui ca punct de plecare.

**Raza** (comună cu coliziunile): un **BVH pe triunghiuri** în `Lapin.Geometry` (divizare la mediană, frunze de 4 pentru raze și 8 pentru coliziuni, ca la BimGo; interfața are deja o variantă în `walkPhysics.ts`), raza Möller–Trumbore, fiecare triunghi știind dacă e sticlă (din materialul părții: `Transparency` > 0 sau `Transmission` > 0). Se construiește din `IQueryContext.Geometry` (`Tessellate()` sau `Mesh` pentru corpurile mari, niciodată un `Solid` forțat), în paralel pe nuclee (BimGo rulează pe un singur fir).

**Suprafețele**: pardoseala camerei (planul de lucru la 0,70 m, reglabil), pereții ei (fața dinspre cameră) și fețele alese. Grila de 0,10–1 m (implicit 0,25 m) în planul fiecărei fețe, legată de originea planului ca grilele vecine să se potrivească; o celulă ține dacă centrul ei cade pe un triunghi; cel mult 80 000 de celule pe studiu. Camerele vin din `Rooms` (`IfcSpace` cu `contour`, `islands`, `HeightOf`).

**Orele de soare direct**: o zi, un interval, pasul de 5, 10 sau 15 minute, eșantionul la mijlocul pasului; o rază spre soare pe eșantion, până la 1500 m, dacă soarele e peste orizont și nu bate razant (cos > 0,02); sticla lasă soarele (opțiunea „sticla oprește soarele”).

**Factorul de lumină naturală (DF)**: cerul acoperit CIE, L ∝ (1 + 2 sin α)/3, 192 de petice (8 benzi de 11,25° × 24 de 15°), 256 de raze pe celulă (128–512) după punctele Hammersley ponderate cu cosinusul; raza ajunge la cer, la sol (reflectanța 0,2), la o suprafață exterioară (o reflexie, 0,2) sau în cameră; **componenta reflectată interior** după formula BRE (split-flux): IRC = 0,85 W / (A (1 − R)) × (C · Rfw + 5 · Rcw), C = 39, reflectanțele din luminanța culorilor (0,05–0,9) sau standard (tavan 0,7, pereți 0,5, pardoseală 0,2).

**Iluminarea (lux)**: cerul senin CIE scalat la iluminarea difuză orizontală IES (800 + 15 500 √sin α), soarele direct 127 500 · e^(−0,21/sin α) · cos · transmisia sticlei, reflexia soarelui de pe pardoseală.

**Transmisia vizibilă a sticlei**: un câmp nou **`Vlt`** pe material (`MaterialLook`), din producător; implicit 0,7 (al lui BimGo) sau din transmisia de randare, ca să nu mai fie o valoare pentru toată sticla.

**Ținte**, ca preseturi pe care le alege utilizatorul (indicatori de proiectare, nu calcul de conformitate):

- **Ordinul MS 119/2014** (art. 3): cel puțin **1½ ore de soare la solstițiul de iarnă** în camerele de locuit;
- **SR EN 17037**: expunerea la soare (o zi între 1 februarie și 21 martie, minim 1,5 / 3 / 4 ore) și ținta de iluminare (300 lx pe 50 % din suprafață, 50 % din orele de zi);
- DF ≥ 2 % (implicit BimGo), lux ≥ 300 pe o cotă din eșantioane; sau valori proprii.

**Ieșirile**:

- **În 3D**: celulele colorate pe suprafețe, cu gradientul Ladybug în 10 trepte (BimGo: 0–7 h, DF 0–5 %, 0–2000 lx), legenda și statistica (medie, minim, maxim, cota peste 2 și 3 ore), verde sau roșu pentru țintă.
- **În plan**: harta pe pardoseala fiecărei camere desenată de engine, deci pe planșe și în PDF, cu legenda; un **plan de lumină naturală** ca vedere proprie (ca planul de umbre din `urban`).
- **Pe cameră**: media, minimul și dacă trece ținta, coloane noi în **extrasul de camere**.
- **CSV** cu coloanele lui BimGo (celulă, suprafață, element, cameră, X, Y, Z, normala, valoarea, trece), în coordonatele proiectului.
- Studiul se salvează ca **`LapinDaylightStudy`** (setările, suprafețele, rezultatele), arătat ca **învechit** când modelul s-a schimbat (amprenta lui `ClashReports.ModelHash`), refăcut doar la cerere.
- Opțional, o regulă `ICdeRule` (avertisment): o cameră de locuit sub ținta aleasă.

## 6. Coliziunile (`clash`)

Lapin are deja verificarea MEP față de golurile din pereți, plăci și acoperișuri (`Lapin.Platform/Coordination`, deciziile 289 și 293): raport păstrat între rulări, numere stabile, remarci BCF, nori în desene, reparare prin goluri. Modulul `clash` e generalizarea, după BimGo (`Core/Clashes/*`), cu aceeași regulă: **rulează doar la cerere**, un raport, apoi rezolvarea.

**Seturile de căutare** (`SearchSets.cs` portat): reguli pe **clasa IFC**, `PredefinedType`, tip, etaj, material, disciplină, fază, link, sistem MEP, orice parametru sau proprietate; operatorii lui BimGo (este, unul din, nu este, conține, cel puțin, cel mult, între; numărul citit din text, „600 mm” → 600), grupuri imbricate, „oricare” sau „toate”. Seturi gata făcute: **Structură** (stâlpi, grinzi, fundații, plăci), **Instalații** (tubulatură, conducte, trasee de cabluri, echipamente), **Arhitectură** (pereți, uși, ferestre, scări). Seturile sunt utile și în afara coliziunilor (selecție după reguli, filtre în extrase).

**Testele**: A × B sau un set cu el însuși; **dur** (toleranța de 5 mm: o pătrundere mai mică nu contează) sau **de distanță** (implicit 50 mm); cel mult 10 000 de coliziuni pe test. Gata făcute: Structură × Instalații, Instalații × Instalații, Arhitectură × Structură, plus **duplicate** (același element de două ori, frecvent la importuri IFC).

**Algoritmul** (al lui BimGo, în C#, în `Lapin.Geometry` cu BVH-ul de mai sus):

- faza largă: **sweep and prune** pe cutiile elementelor, pe axa unde centrele se întind cel mai mult, cutiile mărite cu jumătate din distanță la testele de distanță; perechile sortate, ca rulările să se repete exact;
- faza îngustă, în paralel: triunghiurile fiecărui element din cutia comună, în coordonate față de centrul perechii; BVH pe element de la 384 de triunghiuri;
- **testul triunghi–triunghi Möller 1997**; triunghiurile coplanare nu se intersectează; fețele care doar se ating nu sunt coliziune;
- distanța triunghi–triunghi după Ericson (șase colțuri față de triunghi, nouă perechi de muchii);
- **adâncimea** după metoda lui BimGo (cea mai îngustă lățime a punctelor de intersecție pe X, Y, Z și pe normalele fețelor intersectate, apoi colțurile îngropate în celălalt element);
- unde BimGo se oprește, Lapin are **booleenele manifold**: pe perechile găsite, la cerere, volumul exact al suprapunerii și **elementul cuprins întreg în altul** (pe care BimGo nu-l raportează).

**Ce nu e coliziune**: un element cu el însuși; un gol cu gazda lui și ce îl umple (`IfcRelVoidsElement`, `IfcRelFillsElement`); părțile aceluiași ansamblu (`IfcRelAggregates`: o fermă, o hală, un panou); **elementele îmbinate de Lapin** (pereții la colțuri, grinzile pe stâlpi, cum le leagă îmbinările), unde atingerea e voită; manșoanele și golurile pentru instalații (ordinea tăierii, `Provisions`, `Sleeves`); elementele demolate în faza vederii și cele ascunse la cerere.

**Identitatea între rulări** (`ClashMerge.cs`): cheia e testul și cele două **GlobalId** în ordine; o coliziune găsită din nou trece din nouă în activă, din rezolvată în activă (redeschisă); revizuită și aprobată rămân; una care lipsește dintr-o rulare **completă** devine rezolvată, una trunchiată nu rezolvă nimic. Responsabilul, notele și remarca BCF rămân. Istoricul rulărilor (găsite, noi, rezolvate, durata), cel mult 200.

**Gruparea** (`ClashGroups.cs`): automat după element (cel cu cele mai multe coliziuni întâi), etaj, categorie, etaj și categorie, axă de grilă, sau într-o sferă sau o cutie; de mână; un grup rezolvat când toate ale lui sunt.

**Arătarea**: cutia de secțiune pe suprapunere plus 1 m (cel puțin 2 m pe fiecare axă), camera la 2,5 m, privind în jos la 20°, aleasă din opt direcții după scorul lui BimGo (vederea liberă, ochiul în afara ambelor elemente, privind de-a curmezișul); cele două elemente izolate sau colorate.

**Rapoartele**: remarci **BCF** (tipul „Clash”, GUID din pereche, ca `OpeningClashes`), **nori numerotați** în planuri, secțiuni și elevații (ca `ClashMarks`), **HTML** cu imaginile (ca al lui BimGo, tipărit pe A4), **XML în stilul Navisworks** (îl citesc alte unelte de coordonare), CSV și PDF.

**Coordonarea modelelor străine**: legăturile IFC și Fragments (`docs/links.md`) intră în teste ca orice element (doar citite: dau remarci, nu se repară), deci Lapin coordonează un model federat de mai multe discipline, fără nimic de modelat.

Verificarea de goluri de azi devine un **tip de test** în același dialog (Manage → Coordination), cu reparațiile ei; numele noi nu se suprapun cu `ClashItem` și `ClashReports` din platformă.

## Etapele

1. Soarele adevărat și vederea realistă de bază: tone mapping, cerul și mediul, umbre în cascadă, AO, strălucire, sticla cu transmisie, instanțele cu aspect, profilele de calitate, panoul Soare.
2. Coliziunile, partea A: seturile de căutare, testele, BVH-ul și motorul, lista cu arătarea, BCF.
3. Coliziunile, partea B: stări între rulări, grupare, rapoartele HTML, XML, CSV și PDF, norii în desene, verificarea de goluri ca tip de test.
4. Anturajul: elementele, tipurile, cele 15 desene portate, simbolurile din plan, așezarea pe linie și în contur, IFC.
5. Studiile de lumină: orele de soare, apoi DF și luxul, țintele, harta pe plan, extrasul de camere, CSV.
6. Fotografii pe plăci, panorame, trasee de cameră, video MP4, time-lapse.
7. Texturile CC0 și sondele de reflexie.
8. Lumina artificială cu IES și noaptea.

Fiecare etapă cu testele ei (în BimGo, `ClashTests`, `DaylightTests`, `SunHoursAndCommentsTests`, `SolarPositionTests` au cazurile și numerele, portate ca specificație: conducta prin grindă dă adâncimea dimensiunii mai mici, o suprapunere de 3 mm a pereților nu contează la 5 mm, o țeavă îngropată 40 mm într-o placă dă 40 mm), pagina din ghid în toate limbile și scena ei.

## Umbre vectoriale în desene

Cerut de utilizator la 05.11.2026, după umbrele vectoriale ale lui Archicad, gratuit. Archicad calculează geometric (raze paralele, proiectate pe fețe), iar direcția o ia fiecare fațadă sau secțiune de la soarele vederii 3D (locul, ziua, ora) sau dintr-un soare propriu (azimut, înălțime); pentru fațadele de predare se folosește de obicei un soare ales.

- **În engine**, `Lapin.Geometry/SunShadows.cs`: pentru fiecare față văzută (spre privitor) și luminată, fețele luminate dintre ea și soare se proiectează pe planul ei; ce cade pe față și nu e ascuns de o față mai apropiată în vedere e umbră; o față întoarsă de la soare e în umbră toată. Fără bibliotecă de booleene 2D: fața, umbrele și fețele din față se taie în trapeze printr-o măturare (felii la capetele și intersecțiile muchiilor), fiecare trapez testat la mijloc, trapezele care continuă cu aceleași muchii unite.
- **Pe vedere** (`ViewShadows.cs`, `view.setShadows`): fără (implicit), **soare propriu** (azimut față de direcția privirii, înălțime; implicit convenția de desen: peste umărul stâng, razele pe diagonala cubului, 45° și 35,26°, deci umbra e cât de adânc iese elementul; pe planul de situație din stânga sus, 135°) sau **soarele proiectului** într-o zi și la o oră ale vederii. Culoarea umbrei pe vedere.
- **Unde**: fațade, secțiuni (doar ce e dincolo de tăietură aruncă și primește) și planuri de situație; nu planurile de nivel (pereții tăiați ar arunca umbre prea scurte). Umplutura stă sub toate liniile, deci se tipărește pe planșe, în PDF și DXF.
- **Viteza**: pe Bloc-P-4 (29.000 de fețe), o fațadă cu umbre în 2,4–2,9 s față de 0,2–0,7 s fără; în paralel pe fețe, sursele trecute întâi printr-un test pe colțurile cutiei.

## Ce s-a făcut

- **Etapa 1 (05.11.2026, decizia 310)**: `Lapin.Platform/Sun.cs` (mutat din `urban`, cu `sun.at`: fusul din longitudine, ora de vară europeană în Europa; `SunTests`), View → **Sun and shadows** (`SunDialog.tsx`, folosit și de studiul de însorire din `urban`), `ui/src/viewport/render3d.ts` (profilele, cerul `Sky` și lumina lui prin PMREM, `GTAOPass` orb la ce e transparent, tone mapping), umbrele potrivite pe vedere, terenul, sticla `MeshPhysicalMaterial` cu transmisie în Realist (`glassThrough`), instanțele cu aspectul materialului lor; ghidul (views.md, *Realistic, with the sun*) în toate limbile și scena `views-realistic`. Amânate: strălucirea și umbrele în cascadă (mai sus).
- **Etapa 2 (05.11.2026, decizia 313)**, coliziunile, partea A și stările din partea B: `Lapin.Geometry/Collisions.cs` (`Tri`, Möller tri-tri, distanța Ericson, raza Möller–Trumbore, punct în interior, `TriangleTree` cu interogare pe cutie și rază), modulul deschis `clash` (`engine/src/Modules/Coordination/Lapin.Clash`: `ClashEngine` după BimGo, sweep and prune, faza îngustă în paralel, adâncimea și colțurile îngropate; `SearchSets` pe câmpurile Lapin; `ClashTests` cu numere stabile și stările new/active/reviewed/approved/resolved; perechile care trebuie să se întâlnească sărite: goluri și ce le umple, ansambluri, relații, id-uri numite în atribute), seturile și testele de început (Structure, Services, Architecture; trei teste dure la 5 mm), remarci BCF, CSV; în interfață Manage → Clash detection (`ui/src/modules/clash`), dialogul ancorat în dreapta, încadrarea din lateralul ambelor elemente cu cutia de secțiune. Măsurat pe Bloc-P-4: 1.364 de elemente, 9.643 de perechi, 0,3 s pentru „totul cu totul”. - **Umbre vectoriale (05.11.2026, decizia 322)**: secțiunea de mai sus, făcută; `SunShadowTests`, secțiunea Sun shadows din ghid în toate limbile și scena `views-shadows`.
- **Etapa 2, partea B (05.11.2026, decizia 317)**: gruparea (pe nivel, pe elementul din cele mai multe coliziuni, cel puțin două pe grupă, restul în „Other clashes”, pe perechea de clase, pe stare; calculată la cerere, nepăstrată), rapoartele XML în stilul Navisworks (cu grupele), PDF și HTML cu o imagine a fiecărei coliziuni deschise încadrate în 3D (primele 60), norii roșii în planuri, secțiuni și fațade cu eticheta test.coliziune (platforma adună acum norii și din alte module, `ClashMarks.ISource`), verificarea de goluri ca tip de test („Openings for MEP”, al patrulea test al unui proiect nou; reparațiile rămân în Clash check…), elementul cuprins întreg în altul găsit fără booleene (cutia lui în a celuilalt și un colț în interior; adâncimea e lățimea lui cea mai mică; BimGo îl lasă afară, `Contained: false` face la fel). Grupele păstrate cu stare proprie (ale lui BimGo) nu sunt făcute: o grupă e o vedere a listei.

## Răspunsurile utilizatorului (05.11.2026)

- **Î1, da**: poziția soarelui se mută din `urban` în platformă; `urban` își păstrează planurile de umbre, volumul construibil și însorirea vecinătăților.
- **Î2, da**: orele de soare în camere și pe fațadele proprii gratuit (`daylight`), însorirea vecinătăților după Ordinul 119/2014 în `urban`.
- **Î3**: ordinea etapelor e bună, dar **studiile de lumină (etapa 5) se amână**, păstrate ca issue-uri: #11 (orele de soare în camere) și #12 (factorul de lumină naturală și luxul). Ordinea de lucru: 1, 2, 3, 4, 6, 7, 8.
- **Î4**, fără răspuns: oamenii și mașinile în IFC doar la cerere (opțiunea exportului), cum s-a propus.
