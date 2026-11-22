# Randare: vizualizarea fotorealistă și ediția Lapin Viz

Cerut de Ionuț la 22.11.2026: vizualizarea realistă mult mai bogată, spre o ediție dedicată randării. Decizia 777 în `decisions-pending.md`.

Răspunsurile date la cerere:

1. **Totul gratuit**: inclusiv urmărirea razelor (path tracing) fotorealistă, toate modulele deschise, MIT. Ediția e doar un fișier de produs care combină module deschise.
2. **Primele ieșiri**: imagini statice (exterior și interior, rezoluție mare) și **„Trimite în Blender”** (scena cu camere, lumini și materiale PBR, care se deschide în Blender/Cycles).
3. Se scrie planul **și** se face etapa 1 acum.

Planul continuă `docs/plan-visualisation.md` (decizia 309), nu îl repetă: de acolo rămân soarele adevărat, profilele Desen / Mediu / Realist (`ui/src/viewport/render3d.ts`), cerul `Sky` cu lumina lui prin PMREM, GTAO, tone mapping, sticla cu transmisie, instanțele cu aspect, anturajul, coliziunile. **Etapele lui 6, 7 și 8 sunt preluate aici**: 6 (fotografii, panorame, video) devine S5 și S7, 7 (texturi CC0 și sonde de reflexie) devine S1 și S3, 8 (lumina artificială cu IES și noaptea) devine S2. În `plan-visualisation.md` sunt marcate ca atare.

## 1. Ținta și ediția

**Ținta**: dintr-un model Lapin, fără alt program, o imagine pe care un arhitect o pune în fața clientului: exterior în lumina zilei sau la amurg, interior cu lumina ferestrelor și a corpurilor de iluminat, la rezoluție de tipar; iar când vrea mai mult, aceeași scenă în Blender, cu camerele, luminile și materialele deja puse.

**Ediția „Lapin Viz”** (`products/lapin-viz.json`, făcut cu S1; numele e întrebarea Î1; nu e încă între edițiile publicate din `publish-update.mjs`): un fișier de produs, ca `lapin-architecture.json`, cu modulele deschise care fac sens pentru vizualizare:

| Modul | De ce |
|---|---|
| `architecture` | pereții, plăcile, acoperișurile, ușile și ferestrele, camerele (sondele de reflexie stau pe camere) |
| `furniture` | mobilierul (modelele CC0, materialele lor) |
| `civil` | terenul, plantările (copacii), anturajul (oameni, mașini) |
| `sitework` | aleile, pavajele, gardurile |
| `mep` | `IfcLightFixture`: corpurile de iluminat devin lumini (S2) |
| `studio` | corpurile libere, obiectele modelate de mână |
| `drafting` | planșele cu imaginile randate (o randare se pune pe planșă ca o vedere) |

`scripts/product.mjs` o completează cu ce cer modulele: `core`, `structure` și `phases` intră singure (cerute de `civil`, `sitework` și `mep`), deci ediția rezolvată are zece module.

Platforma (deschisă, în toate edițiile) ține tot ce e randare: materialele PBR, biblioteca de texturi, luminile, sondele, urmărirea razelor, randarea pe plăci, exportul spre Blender. **Ediția nu deblochează nimic**: aceeași randare există în ediția completă `lapin`; ediția doar alege un set mai mic de module, cu o panglică mai simplă. Nimic din acest plan nu e premium.

## 2. Regulile care nu se schimbă

- **Un singur rezolvitor** decide ce arată o vedere (`VisibilityGraphics`): randarea, imaginea pe plăci, panorama și scena trimisă în Blender arată exact ce arată vederea 3D din care pornesc (fazele, filtrele, straturile, ascunderile, cutia de secțiune). Nimic nu-și face propria selecție.
- **GlobalId-urile neatinse**: randarea doar citește modelul. Luminile, camerele și sondele sunt obiecte de vizualizare ale vederii sau ale proiectului, nu modifică elementele; un `IfcLightFixture` rămâne același element.
- **O regulă pentru curbe** (`Lapin.Geometry/Chords`): urmărirea razelor folosește aceleași triunghiuri ca vederea 3D (copiile grosiere sunt doar pentru ecran; randarea finală ia partea însăși, ca exporturile).
- **Datele pe materiale și tipuri, în IFC**: ce are un câmp în IFC merge acolo (`IfcSurfaceStyleRendering`: culoare, transparență, rugozitate, metalicitate prin specular; `IfcSurfaceStyleWithTextures` cu `IfcBlobTexture` pentru hărți; `IfcLightSource*` unde se potrivește), restul în seturile proprii ale Lapin (`Lapin_Intent`, `Lapin_Light`), **niciodată cu prefixul `Pset_`**.
- **Proiectele vechi arată la fel** până când un material primește hărți sau un corp de iluminat primește fluxul: nimic nu se ghicește peste ce exista.
- **Fără dependențe de Blender**: Lapin scrie fișierele după specificațiile publice (glTF 2.0, USD), ca azi; scriptul de deschidere în Blender e un fișier Python mic, al nostru, MIT.

## 3. Etapele

### S1. Materiale PBR și texturi („texturi 3D”)

**Ce face.** Un material capătă, lângă culoarea și imaginea lui, hărțile unui material fizic: **normală** (relieful, spațiul tangent, verde în sus ca glTF), **rugozitate** (alb aspru), **ocluzie ambientală** (alb deschis) și **înălțime** (alb sus: azi harta de relief `bumpImage`, mâine deplasarea în Blender și în urmărirea razelor), toate puse la aceeași mărime reală a plăcii (`textureSize`, metri) și rotite la fel (`textureRotation`). Puterea normalei și a reliefului existau (raportul #76); se adaugă **puterea ocluziei** (`aoStrength`, 0–1, ca `strength` din glTF). Rugozitatea materialului **înmulțește** harta, ca `roughnessFactor` din glTF; un set ales pune rugozitatea la 1.

**Biblioteca.** Un set de început de la **ambientCG** (CC0, verificat 22.11.2026 pe docs.ambientcg.com/license): cărămidă roșie, beton, tencuială, parchet, plăci albe, pavele de piatră, oțel periat, textil. Fiecare set e descărcarea 1K-JPG adusă la 512 px pe latura mare: **1,8 MB toate opt**, deci **livrate cu aplicația** (`ui/public/textures/ambientcg/<id>/`), nu descărcate la cerere. Decizia după mărime: sub ~5 MB se livrează; o bibliotecă mare (sute de seturi, 1K–2K) se descarcă la cerere din catalogul de pe lapinbim.com (ca extensiile), cu aceeași listă de licențe. Mărimea reală a plăcii e a sursei (cărămida 2,4 × 1,2 m); unde sursa nu o dă, proporția imaginii la 1 m înălțime.

**Ales pe material**, un set se **copiază în proiect** ca imaginile lui (`material.textures`, un singur pas de anulare; o imagine cu aceiași octeți e refolosită). De ce copiat și nu doar numit: proiectul se deschide la fel pe alt calculator, în browser, cu o versiune mai veche, iar exporturile (GLB, USD, OBJ, IFC, Blender) au octeții. Numele setului rămâne pe material (`textureSet`, ex. `ambientcg/Bricks085`) pentru credit și ca fila să arate care e ales. Hărțile proprii ale utilizatorului intră la fel (PNG sau JPEG, cel mult 2048 px).

**Licențele resurselor.** Registrul `ui/src/viewport/texture-library.json` (sursa, licența, verificarea, fiecare fișier cu SHA-256 și mărime), scris de `node scripts/texture-assets.mjs`; `pnpm check:licenses` îl verifică (licența din lista permisă, fișierele neschimbate, niciun fișier livrat nelistat), ca registrul mobilierului (decizia 477). Creditul în `docs/acknowledgements.md`.

**În vedere.** Profilele Mediu și Realist pun hărțile pe `MeshStandardMaterial`/`MeshPhysicalMaterial` (normală în spațiul tangent, derivată în shader din coordonatele proiectate pe cutie; rugozitate; ocluzie pe canalul `uv`); **profilul Desen rămâne cum era** (lumina plată: fără rugozitate și ocluzie; culoarea și normala ca până acum). Instanțele (`instancing.ts`) primesc imaginea și hărțile, cu coordonatele pe plasa comună. Previzualizarea din fila Appearance (sfera) arată toate hărțile.

**Exporturi.** glTF: `metallicRoughnessTexture` (doar la materialele nemetalice: o imagine gri are albastrul egal cu verdele, deci metalicitatea ar ieși greșit la un metal), `occlusionTexture` cu puterea ei, `normalTexture` ca azi; USD: `roughness` și `occlusion` legate de imagini; OBJ: `map_Pr`. IFC: `IfcSurfaceStyleWithTextures` lângă `IfcSurfaceStyleRendering`, câte un `IfcBlobTexture` pe hartă (formatul și octeții în fișier, `Mode` MODULATE / NORMAL / BUMP / ROUGHNESS / OCCLUSION, transformarea = placa în metri), citit înapoi de Lapin (imaginile refăcute cu id-urile lor din `Lapin_Intent`). Un fișier IFC străin cu texturi nu primește hărți (importul reproduce fișierul; texturile străine sunt Î6).

**Teste.** xUnit `MaterialTextureSetTests` (pasul unic, imaginile comune, anularea, verificările, `.lapin`, GLB/USD, IFC dus-întors, IFC fără hărți neschimbat); vitest `textureLibrary.test.ts` (registrul, mărimea, adresele sub baza interfeței, argumentele comenzii, `aoStrength`). **Ghid**: `materials.md` (Texture sets), `views.md` (Realistic, with the sun), în engleză și română; scena `materials-texture-sets` (un zid de cărămidă în soare razant, înainte și după).

### S2. Lumina artificială

- **Corpurile de iluminat ca lumini**: `IfcLightFixture` (modulul `mep`) și orice material emisiv marcat „sursă” devin lumini în profilele Mediu și Realist. Pe **tip**: fluxul în **lumeni**, **temperatura de culoare în kelvini** (culoarea după Planck, 1000–12000 K), unghiul fasciculului și al marginii (spot), forma: punct, spot, **suprafață** (panourile LED: `RectAreaLight`, în urmărirea razelor emisie reală pe suprafață). Datele în `Lapin_Light` pe tip (lumens, kelvin, beam, field, kind, ies), iar în IFC și ca `IfcLightSourceGoniometric` / `IfcLightSourcePositional` pe reprezentarea tipului, unde schema le are (fluxul în `LuminousFlux`, culoarea în `LightColour`). Implicit 1000 lm și 3500 K (BimGo); un tip fără date nu luminează (proiectele vechi nu se schimbă).
- **Profile IES LM-63**: cititorul (tip C, 1986–2002) în engine, testat pe fișiere scrise de noi; desenat cu `IESSpotLight` din three în vedere, iar în urmărirea razelor ca distribuție. Nicio fotometrie de producător livrată; fișierele utilizatorului intră în proiect ca imaginile.
- **Bugetul de lumini, live**: vederea arată câte lumini desenează cu umbre și câte fără (o bară în colț: „12 / 48 lumini, 4 cu umbre”), după profil (Mediu: 8 cu umbre, 64 fără; Realist: 16 cu umbre, 256 fără; laptop: jumătate); cele mai apropiate și mai puternice întâi; în randare toate.
- **Noaptea și amurgul**: cerul după înălțimea soarelui (amurg civil, nautic, noapte), stelele, luna (poziția după Schlyter), **expunerea** (EV, automată sau fixă), **balansul de alb** (în kelvini, implicit după sursa dominantă). Lumina ferestrelor văzută din afară noaptea vine din interior, nu se pictează.
- **Teste**: culoarea din kelvini (puncte cunoscute ale locusului Planck), lumenii în intensitate (candela pentru un spot: Φ / (2π(1 − cos(θ/2)))), cititorul IES pe cazurile lui BimGo, bugetul (alegerea celor mai apropiate). **Ghid**: o pagină „Lights” (o sufragerie la amurg) și scena ei.

### S3. Reflexiile

- **Sonde de reflexie pe camere** (după BimGo): o sondă pe `IfcSpace` cu o suprafață reflectantă, la 1,70 m peste pardoseală, o grilă la 8 m în camerele lungi, proiecția pe cutia camerei (parallax-corrected), amestecate pe ~1 m la granițe; coapte câte două fețe pe cadru, cele apropiate întâi, refăcute când se mută soarele sau se schimbă camera. Camera e cea a lui `Rooms` (un singur adevăr), sonda nu se salvează (se derivă).
- **Reflexii plane exacte în randări**: oglinzile și cele mai mari geamuri din câmp (scorul lui BimGo: arie × orientare / distanță²) cu `Reflector`, doar în imaginile randate (S5), nu live.
- **Teste**: așezarea sondelor (o cameră, o cameră lungă, două camere vecine), scorul planurilor. **Ghid**: un interior cu pardoseală lucioasă, înainte și după.

### S4. Urmărirea razelor (path tracing) fotorealistă

- **Motorul**: `three-gpu-pathtracer` (Garrett Johnson, **MIT**; verificat 22.11.2026: ultima 0.0.28 cere three ≥ 0.187, 0.0.24 merge cu three ≥ 0.180) peste `three-mesh-bvh` (MIT, 0.9.18). Lapin are three 0.182: fie **three urcat la 0.187** (de verificat GTAOPass, Sky, CSM și liniile groase), fie pathtracer 0.0.24 la început. Recomandarea: urcarea three, ca o etapă separată, înaintea lui S4.
- **În vederea 3D**: butonul **Render** (progresiv: imaginea se limpezește cât timp camera stă; orice mișcare o reia de la capăt, fără să blocheze). **Dialogul Render**: rezoluția (fereastra, 1080p, 4K, A3 la 300 dpi, proprie), eșantioanele (64–4096) sau timpul, denoise, adâncimea de câmp (focus pe un clic, diafragma), expunerea camerei (ISO, timp, f-stop sau EV), balansul de alb, cerul (cel al soarelui sau un HDRI CC0 de la Poly Haven, descărcat la cerere).
- **Denoise**: pe desktop **Intel Open Image Denoise** (Apache-2.0) ca bibliotecă nativă lângă manifold (`engine/native/<rid>/`, construită ca manifold) sau ca proces separat (sidecar), cu albedo și normale ca ghid; în browser un denoiser în shader (un filtru bilateral ghidat de albedo/normale/adâncime, al nostru). Î4 alege între bibliotecă și sidecar.
- **Ce randează**: exact geometria și materialele vederii (rezolvitorul unic), sticla cu transmisie și IOR, emisivele și luminile S2 ca surse, deplasarea din harta de înălțime (S1) acolo unde corpul are destule triunghiuri sau prin subdivizare la randare.
- **Teste**: scena se construiește din aceleași părți ca vederea (numărul de triunghiuri, materialele), denoiserul pe o imagine de zgomot cunoscută (eroarea scade), expunerea (EV → factor). **Ghid**: pagina „Render” și scena ei (exterior la amiază, interior la amurg).

### S5. Imagini statice

- **Pe plăci** (`camera.setViewOffset`): orice rezoluție, peste limita plăcii video (8K × 8K și mai mult), cusute fără margini (o margine de suprapunere pentru AO și denoise); pentru urmărirea razelor, fiecare placă cu eșantioanele ei.
- **Coada de randare**: vederile 3D salvate bifate, fiecare cu rezoluția și calitatea ei, rulate una după alta (pe desktop și cu aplicația minimizată; în browser cât timp fila e deschisă), cu timpul estimat; rezultatul PNG (16 biți opțional) sau JPEG, cu metadatele (proiectul, vederea, data, soarele).
- O imagine randată se poate pune pe planșă (o vedere „imagine randată” legată de vederea 3D, refăcută la cerere).
- **Teste**: cusătura plăcilor (o imagine pe plăci egală cu cea întreagă, în afara zgomotului), coada (ordinea, reluarea după o eroare). **Ghid**: pagina „Render” (secțiunea Still images).

### S6. Trimite în Blender

- **Fișierul**: **glTF 2.0** (GLB) cu `KHR_materials_transmission`, `KHR_materials_ior`, `KHR_materials_emissive_strength`, `KHR_texture_transform`, **`KHR_lights_punctual`** (luminile S2: punct, spot cu conul lor, intensitatea în candela / lux cum cere extensia) și **camerele** vederilor 3D salvate (perspectivă, câmpul vizual, ținta); sau **USD** (deja scris: `UsdLux` pentru lumini, `UsdGeomCamera`). glTF e implicit: importatorul glTF al Blender citește toate extensiile de mai sus; USD rămâne pentru Omniverse și Twinmotion.
- **Scriptul**: `lapin_open.py` (MIT, ~200 de linii, al nostru), pus lângă fișier: deschide GLB-ul, pune Cycles (eșantioane, denoise OpenImageDenoise al Blender), cerul (Nishita după soarele vederii: data, ora, locul), expunerea și balansul de alb, camera activă, deplasarea din harta de înălțime (modificatorul sau `Displacement` în material) și lumina corpurilor după IES (prin noduri, unde există). Se rulează `blender --python lapin_open.py` sau din Blender (Scripting → Open). Opțional, un add-on Blender mic care face același lucru din meniu.
- **Fără dependență**: Lapin nu cheamă Blender; dacă îl găsește instalat, butonul „Open in Blender” îl pornește cu scriptul (ca „Open in …” de azi), altfel salvează fișierele și spune ce să faci.
- **Teste**: GLB-ul are camerele, luminile cu unitățile lor, extensiile declarate; scriptul e verificat sintactic (Python 3.11, `ast.parse`) și, în CI, unde există Blender headless, rulat pe o scenă mică. **Ghid**: pagina „Send to Blender”.

### S7. Panorame și video (mai târziu)

Din `plan-visualisation.md` §3: panorame 360° (6 fețe → echirectangular 4K/8K, metadatele GPano), trasee de cameră (Catmull-Rom), video cadru cu cadru prin WebCodecs și un multiplexor MP4 al Lapin, time-lapse cu soarele. Toate refolosesc S5 (plăcile, coada) și S4 (urmărirea razelor pe cadru, cu denoise).

## 4. Licențele

| Ce | Licența | Unde | Stare |
|---|---|---|---|
| Texturile ambientCG (8 seturi, 1,8 MB) | CC0-1.0 | `ui/public/textures/ambientcg`, registrul `texture-library.json` | S1, livrate |
| HDRI Poly Haven (cer, la cerere) | CC0-1.0 | descărcate la cerere, nelivrate | S4 |
| `three` (examples/jsm: `Reflector`, `RectAreaLight`, `IESSpotLight`, `IESLoader`) | MIT | deja dependență | S2, S3 |
| `three-gpu-pathtracer` | MIT | npm, nou | S4 |
| `three-mesh-bvh` | MIT | npm, nou (cerut de pathtracer) | S4 |
| Intel Open Image Denoise | Apache-2.0 | nativ lângă manifold sau sidecar | S4 (Î4) |
| Scriptul Blender `lapin_open.py` | MIT (al Lapin) | lângă fișierul trimis | S6 |
| glTF 2.0, KHR_*, USD | specificații publice | scrise de Lapin, fără bibliotecă | S1, S6 |

`pnpm check:licenses` acoperă npm, NuGet și resursele (mobilierul, texturile); OIDN, dacă intră ca bibliotecă nativă, se adaugă la verificare ca manifold.

## 5. Bugetele de performanță

| Mașina | Vederea live (Realist, ce e azi plus S1–S3) | Urmărirea razelor (S4) |
|---|---|---|
| Laptop cu GPU integrat (Intel Iris Xe, Apple M1) | ≥ 30 fps la 1080p pe o casă (5–20 mii de triunghiuri); hărțile la 512 px; 8 lumini cu umbre; sondele coapte în fundal | 1080p, 256 eșantioane: ~1–3 min; denoise obligatoriu |
| Desktop cu GPU dedicat (RTX 3060 / RX 6700) | ≥ 60 fps la 1440p pe un bloc (Bloc-P-4, ~29 mii de fețe în vederi) | 4K, 1024 eșantioane: ~2–5 min |
| Browserul (aceeași mașină) | ca desktopul cu ~20 % mai puțin (WebGL2), aceeași coborâre automată a profilului la 50 ms pe cadru | urmărirea razelor merge (WebGL2); denoise în shader; plăcile țin fila vie |

Măsurate înainte de fiecare etapă pe `modele/testLapin.lapin` și `modele/Bloc-P-4.ifc`, notate în `history.md`. Texturile: 512 px livrate, mipmap și anizotropie 4; dacă memoria GPU e strâmtă, profilul Mediu poate lua o jumătate de rezoluție.

## 6. Întrebări pentru Ionuț

- **Î1** Numele ediției: „Lapin Viz” (`lapin-viz`)? Alternative: „Lapin Render”, „Lapin Studio Viz”. Și ce module intră: cele de mai sus, sau și `structure` (pentru clădiri complete)?
- **Î2** Biblioteca de texturi: rămâne mică și livrată (8–20 de seturi, sub 5 MB), iar restul **la cerere** din catalogul de pe lapinbim.com? Sau o bibliotecă mare livrată doar cu ediția Viz?
- **Î3** O hartă proprie de utilizator la 2048 px (cât acceptă azi) sau mai mult (4096) pentru randări de aproape? Proiectul crește cu câțiva MB pe hartă.
- **Î4** Denoise pe desktop: OIDN ca bibliotecă nativă în engine (rapid, dar încă o bibliotecă de construit pe trei platforme, ca manifold) sau ca program separat (mai simplu de schimbat)?
- **Î5** Urcarea three de la 0.182 la 0.187 (cerută de `three-gpu-pathtracer` 0.0.28) ca etapă separată înainte de S4, de acord?
- **Î6** Texturile dintr-un IFC străin (`IfcImageTexture`, `IfcBlobTexture` al altui program): se citesc pe materialele recunoscute (ar fi „din fișier”, deci reproduce fișierul) sau rămân afară cum e azi?
- **Î7** „Open in Blender”: căutăm Blender instalat și îl pornim noi, sau doar salvăm GLB-ul și scriptul?
- **Î8** Unitățile luminilor pe tip: lumeni (cum dau producătorii) și kelvini, plus IES opțional; ajunge, sau și puterea în wați cu eficacitatea (lm/W) pentru extrase?

## 7. Ce s-a făcut

- **S1 (22.11.2026, decizia 777)**: hărțile de rugozitate și de ocluzie lângă normală și relief (`Images.MapKeys`, `roughnessImage`, `aoImage`, `aoStrength`), comanda `material.textures` (un pas: imaginile setului copiate în proiect, refolosite când sunt aceleași, setul numit în `textureSet`), cele opt seturi ambientCG (1,8 MB) cu registrul lor verificat de `check:licenses` și `scripts/texture-assets.mjs`, fila Appearance cu setul ales dintr-o grilă de imagini și hărțile proprii (rugozitate, ocluzie) cu previzualizare, vederea (Mediu și Realist; Desen neschimbat) și instanțele, exporturile glTF/USD/OBJ și IFC (`IfcSurfaceStyleWithTextures` cu `IfcBlobTexture`, citite înapoi de Lapin), mobilierul din bibliotecă își duce hărțile. Ce nu s-a făcut din S1: harta de înălțime ca deplasare (rămâne relief, `bumpImage`; deplasarea vine cu S4 și S6), descărcarea la cerere a unei biblioteci mari (Î2), partea de date din S2.
