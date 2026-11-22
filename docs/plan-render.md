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
- **Reflexii plane exacte în randări**: oglinzile și cele mai mari geamuri din câmp (scorul lui BimGo: arie × orientare / distanță²) cu `Reflector`, doar în imaginile randate (S5), nu live. *(Decizia 787: nefăcut; orice imagine randată trece azi prin urmărirea razelor din S4, care reflectă exact oglinzile și sticla; vezi §7.)*
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

> **Decizia 785 (22.11.2026)**: după analiza din aceeași zi, S6 nu mai e doar un export, ci o **legătură vie cu Blender**: un add-on Blender al nostru (MIT) legat de motorul Lapin prin WebSocket (protocolul de azi), care primește scena ca glTF și apoi diferențele sesiunii (elementele schimbate, după GlobalId-urile lor, păstrate), ca Blender să rămână la zi cât lucrezi în Lapin. Exportul de mai jos rămâne calea fără add-on. Godot doar dacă se cer plimbări interactive sau VR, mai târziu.

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

### Răspunsurile (Ionuț, 22.11.2026)

Ionuț: „mergi pe varianta cea mai convenabilă deocamdată” pentru întrebări, și „urcă și three.js, dacă nu strică ce avem”. Alegerile de mai jos sunt deci **varianta cea mai convenabilă, cu acordul lui Ionuț**, nu răspunsuri date una câte una; oricare se poate schimba la revizuire.

- **Î1** „Lapin Viz” (`lapin-viz`), cu modulele din tabelul de la §1 (`structure` intră oricum, cerut de `civil`, `sitework` și `mep`).
- **Î2** Biblioteca rămâne mică și livrată (azi opt seturi, cel mult ~20 și sub 5 MB); una mare vine mai târziu, la cerere, din catalogul de pe lapinbim.com.
- **Î3** 2048 px, cât acceptă azi; 4096 doar dacă o cer randările de aproape.
- **Î4** OIDN ca program separat (sidecar): mai simplu de construit și de schimbat; biblioteca nativă doar dacă viteza o cere.
- **Î5** Da, și făcută acum, înaintea lui S2: three 0.182 → 0.187 (vezi §7).
- **Î6** Texturile dintr-un IFC străin rămân afară, ca azi (importul reproduce fișierul; se poate relua).
- **Î7** Ce scrie la S6: dacă Blender e instalat, Lapin îl pornește cu scriptul; altfel salvează GLB-ul și scriptul și spune ce să faci.
- **Î8** Lumeni și kelvini, plus IES opțional; wații (cu lm/W) nu acum, se pot adăuga ca date pentru extrase.

## 7. Ce s-a făcut

- **S1 (22.11.2026, decizia 777)**: hărțile de rugozitate și de ocluzie lângă normală și relief (`Images.MapKeys`, `roughnessImage`, `aoImage`, `aoStrength`), comanda `material.textures` (un pas: imaginile setului copiate în proiect, refolosite când sunt aceleași, setul numit în `textureSet`), cele opt seturi ambientCG (1,8 MB) cu registrul lor verificat de `check:licenses` și `scripts/texture-assets.mjs`, fila Appearance cu setul ales dintr-o grilă de imagini și hărțile proprii (rugozitate, ocluzie) cu previzualizare, vederea (Mediu și Realist; Desen neschimbat) și instanțele, exporturile glTF/USD/OBJ și IFC (`IfcSurfaceStyleWithTextures` cu `IfcBlobTexture`, citite înapoi de Lapin), mobilierul din bibliotecă își duce hărțile. Ce nu s-a făcut din S1: harta de înălțime ca deplasare (rămâne relief, `bumpImage`; deplasarea vine cu S4 și S6), descărcarea la cerere a unei biblioteci mari (Î2), partea de date din S2.
- **three 0.187 (22.11.2026, decizia 778, înaintea lui S2)**: `three` și `@types/three` 0.182 → 0.187 (cea cerută de `three-gpu-pathtracer` 0.0.28; publicată la 30.10.2026). Ce s-a schimbat pentru Lapin: cerul `Sky` și-a pierdut gamma (r183), uniforma `up` (r186, mereu +Y) și a primit nori, deci cerul r182 e păstrat ca o copie a Lapin (`ui/src/viewport/sky.ts`, MIT, three.js), cu Z în sus și aspectul de până acum; `OrbitControls` e generic pe cameră; `PCFSoftShadowMap` devine `PCFShadowMap` (în care cădea deja). Verificat: typecheck, vitest, zece scene 3D ale ghidului (Desen, Mediu, Realist, instanțe, cutia de secțiune, plimbarea, coliziunile, texturile, mobilierul) comparate imagine cu imagine cu 0.182 (37 de imagini, cea mai mare diferență ora unui mesaj trecător și umbrirea din mediu, sub 3 niveluri de gri în medie), `build:web lapin` și testul lui în browser, `check:licenses`.
- **S2 (22.11.2026, decizia 778)**: lumina artificială și noaptea.
  - *Date*: `light` pe un element, pe tipul lui sau pe un material (`Lights.cs`: kind point/spot/area, lumens, kelvin, beam, field, width, length, profile, on, at, aim; elementul peste tip), comanda `light.set` (un pas, verificată; `clear` o scoate), `light.addProfile` (un fișier IES în proiect, clasa `LapinLightProfile`, același text o singură dată), interogările `light.sources`, `light.profile`, `light.profiles`. O lumină MEP pusă de acum primește 1000 lm și 3500 K (BimGo); cele puse înainte și orice element fără date nu luminează. IFC: `Lapin_Light` (pe element, tip și material) și o reprezentare „Lighting” cu `IfcLightSourcePositional` (punct), `IfcLightSourceSpot` (con) sau `IfcLightSourceGoniometric` (panou sau profil: temperatura, fluxul și tabelul de tip C, `IfcLightIntensityDistribution` în cd/klm); Lapin citește datele înapoi din `Lapin_Intent`, iar `Lapin_Light` nu devine parametru al utilizatorului.
  - *IES*: cititorul LM-63 (1986–2002, tip C, simetriile, TILT) și cele cinci profiluri generate, după `LightProfiles` din BimGo (MIT-0), rescrise (`Lighting/LightProfiles.cs`), plus spotul și semisfera generate.
  - *În vedere* (Mediu și Realist; Desen neschimbat): luminile three (`lights3d.ts`) în unități fizice (candela, nits) scalate în lumina vederii (soarele de 3,2 = ~100 000 lx), culoarea după Planck (Krystek), un profil ca spot cu conul lui și, la cele cu umbre, tabelul pus pe lumină ca hartă proiectată (`SpotLight.map`; `IESSpotLight` din three e doar WebGPU), panourile `RectAreaLight`. **Bugetul diferă de §S2** (8/64 și 16/256 nu încap în WebGL: fiecare lumină se calculează pe fiecare pixel, una cu umbre desenează modelul încă o dată, iar placa are 16 unități de textură): Mediu 2 spoturi cu umbre + 8 spoturi + 8 puncte + 2 panouri, Realist 4 + 16 + 16 + 4, fără umbre de punct (șase treceri fiecare), până la 30 m și 60 m; cele mai apropiate de ochi sau de punctul privit și mai puternice întâi; restul doar în randare (S4). Bazinele se fac după câte lumini are modelul (2, 4, 8, 16), umbrele se redesenează doar când se schimbă modelul sau lumina. Colțul vederii spune „12 / 48 lights, 4 with shadows”.
  - *Noaptea*: o oră după apus (View → Sun and shadows, ora acum 0–24) întunecă cerul (albastru de noapte și stele), stinge soarele, iar expunerea automată se deschide (~9,5 trepte la noapte); View → Display quality are, pentru Mediu și Realist, compensarea expunerii (±4 EV) și balansul de alb (automat: lumina zilei ziua, 4000 K noaptea; sau o temperatură), păstrate pe calculator ca profilul. Luna, EV fix și balansul după sursa dominantă nu s-au făcut.
  - *Măsurat* (M1 Max, Chrome headless, 1756 × 1426 px, Realist, noapte): 8,7 ms fără lumini, 14 ms cu 10 lumini (4 cu umbre), 25 ms cu 50 (20 desenate); Mediu cu 50: 11 ms.
  - *Limite văzute*: lumina fără umbre trece prin pereți (un spot lângă perete luminează și afară); fără lumină reflectată (GI), tavanul de deasupra spoturilor rămâne întunecat; corpurile de iluminat nu strălucesc noaptea (strălucirea doar a emisivelor, „bloom”, nu s-a făcut).
- **S4 (22.11.2026, decizia 786; analiza: decizia 785)**: urmărirea razelor în vederea 3D.
  - *Motorul*: `three-gpu-pathtracer` 0.0.28 peste `three-mesh-bvh` 0.9.18 (MIT, în `check:licenses`), pe rendererul WebGL al vederii, încărcat abia la primul **Render**. Scena (`pathTrace.ts`, pură, testată) e exact ce desenează vederea: plasele vizibile ale elementelor (copia cea mai fină a unei părți grele, niciodată cele grosiere), instanțele una câte una în culoarea aspectului, capacele secțiunii, barele 3D, pământul vederii (pajiștea la Realist, unul simplu la Mediu); tăiate de planurile de tăiere ale vederii triunghi cu triunghi (urmăritorul nu are tăiere proprie); camerele (`IfcSpace`), golurile, adnotările și grilele rămân ajutoare ale ecranului. Materialele vederii copiate fără evidențierea selecției: hărțile S1 (normală, rugozitate), sticla cu transmisie și IOR (subțire), emisivele cu strălucirea lor, ambele fețe. Toate luminile S2 (nu bugetul live), în aceleași unități: punctul ca un bec de 4 cm (umbre moi), spotul cu conul lui, profilul IES/inclus ca distribuție (media în jurul axei: urmăritorul are profile simetrice), panoul ca dreptunghi emisiv; corpul de iluminat nu umbrește propria lampă.
  - *Cerul și soarele*: cerul vederii (`sky.ts`, uniforma nouă `sunDisc`) desenat într-un cub și desfăcut echirectangular 2048 × 1024, eșantionat după importanță; soarele un disc de 0,4° rază (adevăratul are 0,27°: umbre cu marginea moale de afară) cu iradianța luminii direcționale a vederii, mărit doar cât să încapă în half float. Fundalul e cerul vederii (Realist) sau culoarea ei (Mediu); expunerea (EV, câștigul nopții) și balansul de alb ale vederii, tone mapping-ul profilului (AgX / Neutral), sRGB.
  - *Vederea*: butonul **Render** (Mediu și Realist) în colțul vederii și View → Render / Render settings; progresiv (o placă de eșantion pe cadru, UI-ul nu se blochează), reluat la orice mișcare a camerei, la modificarea modelului (o cheie a scenei: o selecție nu reia imaginea) sau a adâncimii de câmp; vederea live cât se mișcă și până la primul eșantion; eticheta „128 samples · 0:42”; imaginea live la pixelii CSS (jumătate pe Retina). Dialogul Render: mărimea (fereastra, 1080p, 4K, proprie până la 8192), eșantioane (64–4096) sau timp, denoise, adâncimea de câmp (focus ales cu un clic, prin BVH-ul urmăritorului; f-stop), expunerea și balansul vederii sau proprii, **Save image…** (PNG randat la mărimea lui, offscreen, o placă pe cadru: tilele pe mai multe plăci = S5).
  - *Denoise*: al nostru (`denoise.ts`, MIT): à-trous cu muchii (Dammertz 2010), 5 treceri, pe lumina demodulată de albedo (textura rămâne clară), ghidat de albedo, normalele cu hărțile lor și adâncime, oprirea pe lumină după zgomotul măsurat în jurul pixelului (cel mai liniștit colț 3 × 3 din 5 × 5, ca umbrele să nu fie luate drept zgomot), licuricii scoși; același filtru în GLSL și în TypeScript (testele). Pe desktop imaginea salvată trece prin **Intel Open Image Denoise 2.5.1** (Apache-2.0) ca program separat (Î4): `oidnDenoise` din release-ul oficial, adus de `scripts/fetch-oidn.mjs` (SHA-256 în `engine/native/oidn.json`, doar dispozitivul CPU și Metal, fără SYCL/CUDA/HIP; necomis, ~51 MB), pus de `build-product` în `resources/oidn` (semnat pe macOS), rulat de gazdă (`Lapin.Host/Denoiser.cs`, `render.denoise`: lumina în half float, albedo și normalele în octeți, fișiere PFM); în browser denoiserul nostru.
  - *Măsurat* (M1 Max, Chrome headless): casa de cărămidă (3 496 triunghiuri, BVH + cer ~200 ms) 128 eșantioane în fereastră (718 × 613) în 9 s, **1080p cu 256 eșantioane în 60 s** + OIDN ~3 s; sufrageria noaptea (3 128 triunghiuri, 4 lumini) 256 eșantioane la 718 × 613 în 77 s (cu 6 reflexii; acum 5), 64 în 17 s, 128 în 33 s, **1080p cu 256 în 198 s** (+ OIDN; peste ținta de 1–3 min a §5 pe laptop, la limita ei pe M1 Max); `testLapin.lapin` (108 triunghiuri) 128 în 9 s. Bloc-P-4 nu s-a măsurat (importul IFC cere dialogul).
  - *Ce nu s-a făcut*: HDRI Poly Haven (cerul e al soarelui), tilele peste limita plăcii (S5), coada, deplasarea din harta de înălțime (urmăritorul nu are bump/deplasare: relieful vine din harta de normală), ISO/timp/f-stop ca expunere, profilele IES asimetrice (media în jurul axei), elementele selectate ca instanțe își păstrează nuanța selecției în imagine (deselectează înainte).
- **S3 (22.11.2026, decizia 787)**: reflexiile, pe camere.
  - *Sondele* (`probes.ts`, pur și testat; `probes3d.ts`, partea GPU): o sondă pe fiecare `IfcSpace` în care e o față lucioasă, la 1,70 m peste pardoseală (într-o cameră joasă, 0,6 m sub tavan, niciodată sub jumătatea înălțimii: regula lui BimGo, jumătate din înălțime cel mult, o punea la 1,40 m într-o cameră obișnuită de 2,80 m), o grilă la 8 m într-o cameră mai lungă de 12 m (păstrate celulele cu ceva lucios la cel mult 8 m), după `ReflectionProbes` din BimGo (MIT-0), rescrise. Camera e volumul `IfcSpace` pe care îl desenează vederea (cutia lui și conturul pardoselii, deci o cameră în L își ține sonda înăuntru); fețele lucioase sunt eșantionate pe triunghiuri la 0,5 m (un voxel de 0,5 m). Totul se derivă din model, nu se salvează.
  - *Lucios* = pragul care exista deja (`materialLook.ts` `reflects`, raportul #76): rugozitate sub 0,3 sau metalicitate peste 0,3; opac, fără transmisie (sticla își păstrează aspectul: ar arăta din stradă interiorul camerei). Aceste materiale reflectau până acum un studio fix (`RoomEnvironment`); acum, în Mediu și Realist, într-o cameră reflectă camera, iar în afara camerelor studioul de până acum. Un proiect cu materiale mate arată la fel; Desen e neschimbat.
  - *Coacerea*: fețele cubului desenate cu luminile, soarele, umbrele și (Realist) cerul vederii, două pe cadru (una pe o placă slabă), câte o sondă până la capăt, întâi cea nedesenată cea mai apropiată de ochi, apoi cele învechite (soarele, o lumină, noaptea, modelul; nu selecția), poza veche arătată până atunci; apoi neclaritatea după rugozitate cu `PMREMGenerator` din three (GGX). În captură fețele lucioase nu reflectă nimic (altfel o pardoseală își coace din nou propria poză de zi, noaptea: o dungă albă la baza pereților, văzută și reparată).
  - *O singură textură*: three 0.187 face PMREM-ul ca un cub adevărat cu niveluri (`samplerCube`), deci zece sonde ar fi luat zece unități de textură, din cele 16 pe care S2 le folosește deja pentru lumini. Fiecare sondă e un strat al unei texturi-tablou (`WebGLArrayRenderTarget`, half float): cele șase fețe și nivelurile lor una lângă alta (3 × 4 dimensiunea feței; stratul 0 e studioul de afară). Shaderul materialelor lucioase (`onBeforeCompile`, înlocuiește `envmap_physical_pars_fragment`) citește această textură în locul hărții de mediu, al cărei sampler rămâne nefolosit: **nicio unitate de textură în plus**.
  - *În shader*: raza reflectată proiectată pe cutia camerei (parallax-corrected: fereastra se reflectă în fața ferestrei), sondele amestecate la granițe pe ~1 m (fiecare 1 la o jumătate de metru înăuntru, ½ pe margine; partea camerelor de două ori suma lor, cel mult 1, deci întreagă și peste peretele dintre două camere, în golul ușii; se stinge doar dincolo de un perete exterior); lumina difuză a materialului lucios vine tot din sondă.
  - *Bugetul*: Realist 10 sonde de 256 px (6,3 MB fiecare), Mediu 6 de 128 px (1,6 MB); o placă slabă (Intel HD/UHD, Mali, Adreno, SwiftShader, sau o textură maximă sub 8192) are Realist 6 de 128 px și Mediu 4 cu o față pe cadru. Când sunt mai multe camere decât locuri, locurile le iau cele mai apropiate de ochi (cu o histereză de 2 m: o sondă coaptă nu e dată afară pentru una abia mai aproape).
  - *Măsurat* (M1 Max, Chrome headless, Realist, 1756 × 1426 px, sufrageria de 6 × 4,5 m cu mobilier și 3 lumini): cadrul 12,1–12,9 ms cu pardoseala mată și 12,1–12,9 ms cu cea lucioasă citind sonda (fără diferență măsurabilă); o sondă se coace în trei cadre. Bloc-P-4 nu s-a măsurat (importul IFC cere dialogul).
  - *Reflexiile plane exacte (`Reflector`) nu s-au făcut*: S4 a făcut din orice imagine randată (Render, Save image) o imagine prin urmărirea razelor, care reflectă deja exact oglinzile și sticla; singurele imagini raster rămase (vederea unei observații, fotografiile 4D) nu sunt randări. Un `Reflector` ar fi fost cod mort.
  - *Limite*: vederea live luminează interiorul aproape cât cerul (fără GI, limita S2), deci ferestrele reflectate în pardoseală sunt discrete, nu strălucitoare cum ar fi în realitate (în randare sunt); o față lucioasă nu reflectă alte fețe lucioase; reflexia e exactă doar pe pereții cutiei camerei; mobilierul instanțiat nu cere sonde (o cameră doar cu un robinet cromat nu are), dar le citește.
