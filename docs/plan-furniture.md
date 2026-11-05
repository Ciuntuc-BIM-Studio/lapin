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

Materialele pe roluri, ale proiectului (Manage → Materials), create după nume când lipsesc: **body** (lemnul corpului), **upholstery** (stofa), **top** (blatul), **fronts** (fronturile), **linen** (lenjeria și salteaua), **metal** (picioare, mânere). Corpurile au culoarea materialului lor, se hașurează pe fațade după materialul lor și ies în IFC cu materialele lor.

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
