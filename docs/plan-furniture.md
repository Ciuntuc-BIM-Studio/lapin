# Mobilier

Cerut de utilizator la 05.11.2026 („dezvoltăm și funcții pentru mobilier?”; „ok. apoi implementează tot modulul fără să te oprești — inclusiv canapele, fotolii, paturi, dulapuri”). Lapin avea doar mobilierul de sub scară (decizia 286) și solidele Studio clasate `IfcFurnishingElement`; un plan de locuință fără mobilă nu e un plan de predat.

## Granița

- Modulul deschis **`furniture`**, în acest depozit, `requires: core, architecture` (se lipește de pereți, se numără pe încăperi), în ediția `lapin`. Tab propriu, **Furniture**.
- Clasele lui: **`IfcFurniture`** (elementul) și **`IfcFurnitureType`** (tipul), cele ale IFC4; Studio rămâne cu `IfcFurnishingElement`, mobilierul de sub scară cu scara.
- Ca la uși și ferestre: **tipul ține forma** (felul, dimensiunile, parametrii felului, materialele pe roluri), elementul ține locul (`at`, centrul amprentei; `rotation`; `offset` deasupra nivelului; `mirror`). Corpul și simbolul de plan sunt derivate, niciodată stocate.
- Fără modele 3D sau biblioteci luate de altundeva: corpurile sunt construite de Lapin din blocuri cu colțuri rotunjite, cilindri și perne, low-poly, ca anturajul (decizia 324). Licența rămâne MIT curată.
- Obiectele sanitare rămân la MEP (terminale), nu aici.

## Felurile

| Grupă | Fel | IFC PredefinedType | Parametrii felului |
|---|---|---|---|
| Șezut | **Canapea** (`sofa`) | SOFA | locuri, brațe (ambele, stânga, dreapta, niciunul), lățimea brațului, înălțimea șezutului și a spătarului, șezlong (niciunul, stânga, dreapta) și lungimea lui, picioare |
| | **Canapea de colț** (`cornerSofa`) | SOFA | latura scurtă (stânga, dreapta) și lungimea ei, locuri, brațe |
| | **Fotoliu** (`armchair`) | CHAIR | ca o canapea de un loc, cu brațe late |
| | **Scaun** (`chair`) | CHAIR | înălțimea șezutului și a spătarului, brațe |
| Paturi | **Pat** (`bed`) | BED | saltea (lățimea e a tipului: 90, 140, 160, 180), tăblie (înălțime), tăblie la picioare, perne (1 sau 2), cuvertură întoarsă |
| | **Noptieră** (`bedside`) | USERDEFINED „Bedside table” | sertare |
| Depozitare | **Dulap** (`wardrobe`) | USERDEFINED „Wardrobe” | uși (număr), fel (batante sau glisante), sertare jos, soclu |
| | **Comodă** (`chest`) | USERDEFINED „Chest of drawers” | sertare (rânduri), coloane |
| | **Bibliotecă** (`bookcase`) | SHELF | rafturi, deschisă sau cu uși jos |
| | **Bufet / comodă TV** (`sideboard`) | USERDEFINED „Sideboard” | uși, sertare, picioare |
| Mese | **Masă** (`table`) | TABLE | formă (dreptunghiulară, rotundă, ovală), picioare (patru, central), grosimea blatului |
| | **Măsuță de cafea** (`coffeeTable`) | TABLE | formă, raft jos |
| | **Birou** (`desk`) | DESK | corp cu sertare (niciunul, stânga, dreapta) |
| Bucătărie | **Bucătărie liniară** (`kitchen`) | USERDEFINED „Kitchen” | lungimea pe element; corpuri jos de 60 cm (restul împărțit), blat, chiuvetă și plită (poziții), corpuri sus (înălțime, adâncime, la ce cotă), coloană pentru frigider la un capăt |
| | ↳ **Corpurile proprii** (decizia 472) | — | `units` (rândul de jos: corp de jos, chiuvetă, plită peste cuptor, aragaz, mașină de spălat vase 45/60, frigider, coloană cuptor, coloană, panou, gol) și `upper` (corp sus, hotă, gol) pe tip; corpuri 100–800 mm, aparatele în dimensiunile lor; restul rândului împărțit ca înainte |

Materialele pe roluri, ale proiectului (Manage → Materials), create după nume când lipsesc: **body** (lemnul corpului), **upholstery** (stofa), **top** (blatul), **fronts** (fronturile), **linen** (lenjeria și salteaua), **metal** (picioare, mânere), **glass**; la bucătărie și **plinth** (soclul) și **handles** (mânerele), care la un tip mai vechi iau materialul corpului și al metalului (decizia 472). Corpurile au culoarea materialului lor, se hașurează pe fațade după materialul lor și ies în IFC cu materialele lor.

## Așezarea

- **Un clic** pune piesa cu tipul ales (în bara liniei de comandă și în Properties, ca la pereți); **lângă un perete** (la cel mult jumătate din adâncime plus 60 cm de fața lui) piesa se lipește cu spatele de fața peretelui, cu fața spre încăpere, pe stratul de finisaj; altfel stă cu centrul în punctul ales, rotită cum spune bara (0°, 90°, 180°, 270°). Opțiunea **Against walls** din bară o oprește.
- **Bucătăria liniară** se desenează cu două puncte de-a lungul unui perete: lungimea e distanța, spatele pe fața peretelui de partea liniei.
- **Scaune în jurul mesei**: o masă aleasă primește scaune pe laturile lungi (și pe capete, dacă încap), câte unul la 60 cm, cu fața spre masă; la masa rotundă, pe cerc.
- Mută, rotește, oglindește și copiază ca orice element; o oglindire schimbă partea (canapeaua de colț, șezlongul, biroul cu corp, frigiderul bucătăriei) fără să întoarcă piesa cu fața la perete. Mânere: mutare (centrul) și rotire (în fața piesei).

## Desenul

- **În plan**: simbolul convențional al felului, nu tăietura (ca ușile): canapeaua cu spătarul, brațele și pernele; patul cu tăblia, pernele și cuvertura întoarsă; dulapul cu bara de umerașe și împărțirea ușilor (batante: arcele; glisante: cele două șine); comoda și bufetul cu fronturile; masa cu conturul ei; bucătăria cu blatul, chiuveta, plita cu patru ochiuri, iar corpurile de sus întrerupte (deasupra planului de secțiune).
- **În fațade și secțiuni**: corpurile, cu hașurile materialelor lor (decizia 325).
- **În 3D**: corpurile low-poly.

## Tipurile

**Furniture → Furniture types**: tipurile proiectului grupate pe fel; un tip nou dintr-un fel (cu valorile lui de pornire) sau duplicat; numele, dimensiunile, parametrii felului și materialele pe roluri, cu **previzualizarea motorului** (planul și vederea din față). O schimbare a tipului se vede pe toate piesele lui. Tipul poate avea articole de deviz (modulul `cost`: pe bucată).

## Extrasul și IFC

- **Furniture schedule** (tipul `furniture`): pe tip, felul, dimensiunile, numărul și încăperile în care stă; **Furniture by room** (`furnitureRooms`): pe încăpere, ce are.
- **IFC**: `IfcFurniture` cu PredefinedType-ul felului, `IfcFurnitureType` (AssemblyPlace NOTDEFINED) prin `IfcRelDefinesByType`, corpurile pe materiale; `Lapin_Intent` le aduce înapoi native la reimport (mecanismul general). Mobilierul străin din IFC rămâne referință, cum era.

## Etape

1. Motorul: felurile, corpurile, simbolurile, tipurile, așezarea lângă pereți, scaunele în jurul mesei, bucătăria, oglindirea, extrasele, IFC; testele.
2. Interfața: tabul Furniture (o unealtă pe fel), bara cu tipul, rotația și lipirea de pereți, Properties pentru piesă, dialogul de tipuri cu previzualizarea.
3. Ghidul (pagina Furniture în toate limbile, cu scena ei) și traducerile.

Mai târziu, dacă e cerut: verificarea spațiilor libere în fața pieselor (accesibilitate), corpuri de colț la bucătărie (L, U), mobilier de birou în rânduri, recunoașterea mobilierului străin din IFC în tipuri native.

## Făcut

05.11.2026 (decizia 330): etapele 1–3. Motorul în `engine/src/Modules/Interior/Lapin.Furniture` (`FurnitureKinds`, `FurnitureBodies`, `FurnitureModule`, `FurnitureSchedules`), testele în `FurnitureTests`; interfața în `ui/src/modules/furniture`; ghidul `docs/user/furniture.md` în toate limbile, cu scena `furniture`. Scurtăturile: SO (canapea), AC (fotoliu), BD (pat), WD (dulap), TB (masă), KT (bucătărie).

11.11.2026 (decizia 475): etapa 1 a configuratorului flexibil, mobilierul din plăci (mai jos).

## Configuratorul flexibil

Cerut de utilizator la 11.11.2026, după bucătăria cu corpuri proprii (decizia 472): un configurator de mobilier flexibil și modern, în trei etape. Plan aprobat; întrebările deschise s-au hotărât după cea mai bună variantă și sunt scrise aici.

### Etapa 1: corpuri de mobilier din plăci (`casework`), făcută (decizia 475)

- **Un fel nou, `casework`** (grupa Casework, unealta Interior → Casework), un singur motor (`Casework.cs`) pentru tot mobilierul din plăci; felurile vechi (dulap, comodă, bufet, bibliotecă, noptieră, birou) rămân neschimbate, ca proiectele vechi să se deschidă la fel (fără `IProjectUpgrade`: specul felurilor vechi nu se schimbă, iar cel nou doar adaugă chei).
- **Piesa = corpuri alăturate** (`carcasses`, de la stânga privit din față). Un corp cu `width` și-o păstrează; cele fără lățime împart restul lățimii piesei (cel puțin 100 mm fiecare); ce nu încape e lăsat deoparte cu avertisment (ca la bucătărie), ce rămâne liber devine panou de umplutură. Un corp `gap` e spațiu pentru picioare (birou): blatul trece peste el, iar la capătul piesei o laterală (gable) susține blatul. Un corp poate avea `height` proprie, mai mică.
- **Zonele** unui corp, de jos în sus, cu separator (raft fix) între ele: înălțime fixă (`height`) sau parte din rest (`share`); fața: deschis, uși (1–4, mâna, vitrate), sertare (1–6, `ratios` de sus în jos), clapetă (sus/jos); interiorul: rafturi (reglabile implicit: 1 mm joc pe laterale, 20 mm retrase din față), bară de umerașe (rafturile sub ea doar dacă rămâne 1 m de agățat), sertare interioare în spatele ușilor (retrase 30 mm de balamale). „Sertare sub uși” = două zone. Etajarea pe verticală se face prin zone, nu prin corpuri suprapuse (hotărâre: un model mai simplu, care acoperă cazurile cerute).
- **Construcția**: grosimile plăcilor (corp 18, spate 8, rafturi 18, fronturi 18 implicit), capac și fund între laterale sau peste ele, spatele încastrat (la 10 mm de muchie) sau aplicat. Sertarele au cutie (laterale 16, capete, fund din materialul spatelui), adâncimea în pași de 50 mm ai glisierelor, 13 mm joc de fiecare parte.
- **Baza**: soclu retras (plăci față și spate), picioare (metal subțire, lemn conic — trunchi de piramidă —, sanie din platbandă) la capetele fiecărui șir de corpuri și cel mult la 1 m între ele, suspendat pe perete la o cotă (`mount`) sau nimic. Înălțimea tipului e până la blat pe sol; suspendat, e înălțimea piesei, iar `mount` cota feței de jos (hotărâre: ca o comodă TV „de 400, la 300”). Peste 1,2 m piesa se desenează „overhead”.
- **Blatul**: niciunul (capacele corpurilor), cu consolă (față și capete), blat de lucru (doar față); decupaj pentru lavoar (substituent: gaură în blat, conturul și bateria în plan). Fronturile: placă, cu ramă (montanți și traverse în jurul unei tăblii retrase; vitrate: ramă + sticlă), canelate (caneluri verticale 14 × 4 mm la 30 mm, geometrie). Mânerele: niciunul (push-to-open, fără solide), profil J (frezat în front: frontul din două solide, buza retrasă; nu e un solid de mâner), bară, buton, mâner pe muchie. Uși glisante (`sliding`) peste toate corpurile, pe două șine.
- **Patul** e o formă proprie (`form: bed`): laterale, tăblie la picioare, tăblie cât tipul, placă de bază, lonjeron central, salteaua și pernele, sertare pe laterale (stânga, dreapta, ambele).
- **Presetări** (catalogul `furniture.casework`, aplicate ca stilurile tâmplăriei: valorile presetării peste ale felului, materialele rămân): bufet, dulap cu uși batante, dulap cu uși glisante, bibliotecă, comodă TV suspendată, comodă, mobilier de baie cu lavoar, birou cu corp de sertare, mobilier de hol, pat, noptieră. **Stiluri moderne** (petice pe tip, un pas): fără mânere, profil J, picioare subțiri de metal, suspendat, fronturi canelate, bicolor (stejar + salvie), fronturi cu ramă și butoni.
- **Materiale pe roluri**: body (corpul), fronts, top, back (nou), plinth, legs (nou), handles, glass, metal, linen; implicitele sunt orientative.
- **Plăcile**: fiecare placă e un solid al rolului ei și o linie în lista de debitare (`BoardRow`: piesa, corpul, rolul, lungimea pe fibră, lățimea, grosimea, axa fibrei, front sau nu). Extrasul **Cut list** (`cutList`): pe tip, plăcile apoi fronturile, la milimetru, înmulțite cu piesele; feroneria (glisiere, balamale, picioare, mânere) nu e listată.
- **IFC**: `IfcFurniture` al unui `IfcFurnitureType`, PredefinedType după presetare (SHELF, DESK, BED, altfel USERDEFINED cu numele ei); schimbarea presetării schimbă tipul IFC al tipului și al pieselor. Specul (cu corpurile) merge cu tipul și se citește înapoi.
- **Interfața**: în Tipuri de mobilier, la un tip casework, editorul `CaseworkEditor` (presetare, stiluri, secțiunile construcției, corpurile cu zonele lor, adăugare/împărțire/mutare/scoatere, Fit width), previzualizarea motorului (plan, față) cu lista de debitare; fiecare schimbare e un `updateType`.
- **Idei de îmbinări**: atelierul Woodworking din FreeCAD (dprojects, MIT după README-ul lui, https://github.com/dprojects/Woodworking) — doar idei (lista de debitare pe plăci, fibra, construcția corpului), rescrise; niciun cod preluat.

### Etapa 2: mobilier moale din modele CC0 întinse pe zone (de făcut)

- Canapele de 2/3 locuri, mese mai lungi, scaune: un model mesh CC0 tăiat în zone (capete fixe, mijloc întins), ca o piesă se poate lungi fără să-și deformeze brațele sau picioarele.
- Biblioteca: Kenney Furniture Kit (CC0, https://kenney.nl/assets/furniture-kit) și Poly Haven (CC0, de verificat pe polyhaven.com/license înainte de folosire).
- Licențele: fiecare model cu sursa și licența lui într-un registru verificat de un script în stilul `pnpm check:licenses` (un model fără licență permisivă înregistrată nu intră în build).

### Etapa 3: configurații salvate, făcută (decizia 476)

- O configurație bună devine un `IfcFurnitureType` în biblioteca utilizatorului și în șabloanele de proiect (decizia 470), ca să fie refolosită între proiecte.
- **Biblioteca mea de mobilier**: pe desktop un fișier JSON pe element în `<user-dir>/furniture/` (`FurnitureLibrary`, Lapin.Protocol), în browser IndexedDB; un element are nume, categorie, etichete, cod de articol, preț orientativ (al utilizatorului), miniatură (fața din previzualizarea motorului) și tipul cu materialele lui după valoare (`furniture.libraryItem`).
- **Inserarea** (`furniture.insertFromLibrary`, un pas): materialele după nume (folosite sau create), un tip identic refolosit, altfel un tip nou cu GlobalId nou. Specul e păstrat opac: un tip din modele mesh (etapa 2) se salvează la fel, iar o ediție care nu are felul îl refuză cu numele lui.
- **Fișierul `.lapinlib`**: JSON `{format: "lapin-furniture-library", version: 1, items}`, un element sau toată biblioteca; la import nimic nu e suprascris (același conținut sărit, alt conținut sub un id nou).
- **Șabloanele**: păstrează deja tipurile proiectului; Salvează ca șablon… poate adăuga toate tipurile bibliotecii, iar un element se poate adăuga într-un șablon existent.

Rămas din etapa 1, dacă e cerut: corpuri suprapuse ca elemente separate, colțuri (L) pentru corpuri, cant (benzi de margine) și feronerie în listă, prețuri pe placă în deviz, export al listei de debitare pentru optimizare.

