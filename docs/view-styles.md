# Stiluri de vedere și îmbinările vizuale

## Îmbinările vizuale (implicite)

Unde același material trece dintr-un element în altul, în secțiune nu se desenează nicio linie între ele: miezurile a doi pereți, două grinzi, fundațiile continue la colț. Elementele rămân separate în model; doar desenul le citește ca un singur material.

- Regula era în `MaterialDrawing` (linia dintre benzile aceluiași material a două elemente dispare), dar o latură lungă acoperită doar în parte de vecin rămânea desenată întreagă. La un colț în butt, miezul care trece prin colț își desena fața până la capăt, peste capătul celuilalt. Acum `Generator.SplitAtCorners` taie liniile de secțiune în punctele unde se sprijină colțurile altor arii, iar porțiunea comună dispare.
- Liniile dintre straturile elementelor de aceeași clasă îmbinate (miezul unei fundații lângă hidroizolația celeilalte, după colț) iau creionul de separare, subțire; conturul păstrează creionul de secțiune (`Pens.Separations`).
- Se dezactivează:
  - pe material: Manage → Materials → Draw joints (rosturile acelui material se desenează mereu);
  - pe vedere sau pe stil: „Join materials” (fiecare element se unește doar cu el însuși).

## Stilurile de vedere

Un stil spune cum desenează un tip de planșă (ca afișarea parțială a structurii din ArchiCAD și șabloanele de vedere din Revit). Sunt ale proiectului (`viewStyles`), cu nume; implicit unul pe disciplină:

| Stil | Disciplină | Straturi | Unește materialele | Overhead |
|---|---|---|---|---|
| Architectural | Architecture | toate | da | da |
| Structural | Structure | doar miezul | da | da |
| MEP | MEP | toate | da | da |
| Coordination | — (ales după nume) | toate | da | da, cu volumele de gol desenate |

- **Straturi**: toate; fără finisaje (straturile din materiale de finisaj); doar miezul. Un element fără miez marcat își păstrează toate straturile. Părțile care nu sunt straturi (talpa fundației, stâlpii, grinzile) se desenează mereu.
- **Finisajele** se recunosc după categoria stratului în tipul lui, dacă o are (`MaterialLayer.Category`, IFC4 `IfcMaterialLayer.Category`, decizia 496: coloana **Category** din Wall types și Floor types), altfel după categoria materialului (`IfcMaterial.Category`, exportată în IFC): Manage → Materials → Category (Structure, Finish, Insulation, Membrane, Other). Fără categorie, un material e finisaj după nume: plaster, render, paint, tile, gypsum, parquet, laminate, carpet, tencuială, glet, vopsea, faianță, gresie, parchet, rigips, finisaj, zugrăveală. O singură regulă, `LayerSets.SubOf`, o citesc stilurile, Visibility/Graphics și 3D-ul.
- **Finisajele pe tip de vedere** (decizia 496, topicul #30 de pe forum, Polonia: planuri fără tencuieli, secțiuni cu finisajele pardoselilor, fără ale pereților): `wallFinishesPlan`, `floorFinishesPlan` (planurile), `wallFinishesSection`, `floorFinishesSection` (secțiunile și fațadele), toate implicit da, și pentru un stil salvat înainte. Le rezolvă Visibility/Graphics (`VisibilityGraphics.FinishesHidden`, din stilul vederii și felul ei), deci planurile, secțiunile, planșele, PDF-ul, DXF-ul și exporturile de vedere spun același lucru; un perete din categoria walls, o placă din slabs, și elementele întregi de finisaj (o referință toată tencuială, finisajul de perete sau de pardoseală al unei încăperi). 3D-ul își păstrează straturile lui. Ce rămâne își desenează conturul secționat la fețele păstrate (fiecare strat e un solid închis), cu îmbinările și colțurile ca înainte; desenul spune interfeței ce categorii și-au pierdut finisajele (`hiddenFinishes`), iar piesele de plan ale peretelui își spun rolul (`PlanItem.Sub`), deci snap-ul merge pe fețele miezului (filtrul deciziei 491, `viewport/cad/shown.ts`). Cantitățile și IFC nu se schimbă.
- **Overhead**: dacă planul desenează ce e deasupra planului de secționare, până la marginea de sus a intervalului.
- **Volume de gol** (`voids`, decizia 290): dacă planurile, secțiunile și fațadele desenează volumele golurilor (`Coordination/VoidDrawings`), secționate sau văzute, cu diagonale, marcă și, cu `voidSizes` (implicit da), dimensiunea și cota de jos a golului din perete; implicit nu, doar stilul Coordination. Categoria lor de linii e `voids` (Object styles, Visibility/Graphics), layerul `VOIDS`.
- **Hașurile finisajelor de pardoseală** (`finishHatch`, decizia 299): dacă planurile hașurează pardoselile încăperilor finisate (un plan văzut de jos, tavanele) cum spun tipurile lor (`docs/rooms.md`); implicit da în Architectural, nu în Structural, MEP și Coordination; un stil salvat înainte de câmp îl ia după disciplină (nu pentru Structure și MEP).
- **Hașurile suprafețelor** (`surfaceHatch`, decizia 325): dacă fațadele și secțiunile hașurează fețele pe care le văd cu hașura de fațadă a materialelor lor, doar partea pe care nu o ascunde nimic mai apropiat (`SurfaceHatches`, vizibilitatea din `Lapin.Geometry/ViewFaces`, aceeași cu a umbrelor); implicit da în Architectural, nu în Structural, MEP și Coordination; un stil salvat înainte de câmp îl ia după disciplină.
- **Anturajul în 2D** (`entourage`, decizia 341): cum desenează planurile, fațadele și secțiunile oamenii, mașinile și plantele (`LapinEntourage`, `LapinPlanting`): `outline` (implicit, și pentru un stil salvat înainte de câmp: simbolul simplu în plan, o siluetă cu un singur contur în fațade și secțiuni, `Lapin.Civil/Entourage2d`, `Silhouette` în `Lapin.Drawing`), `abstract` (cercuri și cutii), `detailed` (simbolurile de peisagistică și muchiile corpurilor, ca înainte) sau `none`; **Umplerea siluetelor** (`entourageFill`): o culoare sau nimic (implicit). Liniile lor au penița 2 dacă Object styles nu dă alta categoriei `entourage`; corpurile 3D nu se schimbă.

O vedere ia stilul pe care îl numește (`viewStyle`), altfel pe al disciplinei ei. Peste stil, vederea poate seta singură oricare dintre câmpuri (`styleOverrides`); ce setează ea câștigă, iar butonul de lângă câmp o întoarce la stil.

## Unde se setează

- **Disciplina** unei vederi sau planșe, în proprietățile ei (selector `DisciplinePicker`: disciplinele implicite, ale seturilor browserului, ale stilurilor și cele deja folosite; alta se tastează). Browserul o așază sub setul disciplinei; vederea fără stil numit desenează cu stilul disciplinei. Când nu e nimic selectat, panoul de proprietăți arată vederea din față (`FrontViewPanel`).
- **Nivelul de referință** al unui plan (planul altui nivel, de sub sau de deasupra, estompat pe ecran, cu snap, netipărit): pe vederea de plan (`underlay`, `view.setUnderlay`), în secțiunea View settings și în cipul din bara planului. Deschiderea unui plan propriu din navigator îl selectează, deci setările lui sunt în panou cât e selectat.

- **View → View settings** (vederea din față) și **proprietățile unei vederi**: stilul, straturile, unirea materialelor, overhead-ul, simbolurile 2D din IFC (`ifc2d`, decizia 148), intervalul vederii (planul de secționare, sus, jos, adâncime: View range…) și etajele afișate (decizia 136).
- **Manage → View styles**: stilurile proiectului, cu nume, disciplină, straturi, unire, overhead și IFC 2D; duplicare, stil nou, ștergere (vederile lui iau stilul disciplinei).
- Comenzi: `viewStyle.create {from?, name?, …}`, `viewStyle.update {id, …}`, `viewStyle.delete {id}`, `view.setStyle {id, style?, layers?, joins?, overhead?, ifc2d?, voids?, voidSizes?, finishHatch?, surfaceHatch?, entourage?, entourageFill?, wallFinishesPlan?, wallFinishesSection?, floorFinishesPlan?, floorFinishesSection?}` (null: înapoi la stil); interogări `viewStyle.list`, `view.style {id}`.

Totul e în desenul motorului (`Views.Gather`, `Views.Spec`, `Views.Render`), deci planșele tipăresc ce arată ecranul. Teste: `ViewStyleTests`.

## În 3D (decizia 162)

O vedere 3D are și ea disciplină, stil și **straturi**, ca o planșă: {3D} le ține pe proiect (`view3d`: `discipline`, `viewStyle`, `styleOverrides`, lângă `storeys`), o vedere 3D salvată pe ea însăși (duplicarea lui {3D} le copiază). O vedere 3D cu disciplina Structure arată doar miezurile; din proprietățile ei (și ale lui {3D} când e în față), din **View → View settings** cu vederea 3D în față (`view.setStyle {id: "3d" | id, discipline?, style?, layers?}`) sau din eticheta **Layers shown** din josul vederii (× înapoi la toate straturile: alegerea proprie dispare când stilul arată totul, altfel devine „toate”).

- Randarea nu se schimbă pentru planuri sau alte vederi: motorul trimite toate părțile, iar fiecare parte are `hiddenBy`, modurile care o lasă deoparte (`ViewStyles.HiddenBy`: tencuiala `noFinishes` și `core`, izolația `core`). Fiecare strat e un solid închis al lui (pereți drepți și curbi, zonați, planșee, fundații continue), deci ce rămâne e închis. Categoria materialului e citită prin vederea de urmărire a construcției, deci o categorie schimbată reconstruiește elementul.
- Interfața (`viewport/layers3d.ts`, aceeași regulă ca `ViewStyles.Of`) desenează elementul fără acele părți: alegerea, Fit, corpul plimbării și capacele urmează. `view.caps {layers}` închide doar straturile păstrate; `project.exportMesh` cu o vedere 3D ia straturile ei (`MeshExportOptions.Layers`, sau `layers` explicit), „everything” le ia pe toate.
- Ușile și ferestrele rămân în goluri; un șpalet (`reveal:*`) dintr-un material de finisaj dispare cu finisajele peretelui (doar în 3D: planurile îl desenează ca înainte). Pragurile, glafurile și ancadramentele rămân.
- Acoperișurile nu au straturi (învelitoare, șarpantă), deci rămân întregi; pereții cortină nu sunt stratificați.

Teste: `View3dLayersTests`, `layers3d.test.ts`; scena `views-3d-layers`.

## Pereții prin straturile plăcilor (decizia 497)

Topicul #30 de pe forum (Polonia; ca funcțiile și prioritățile straturilor din Revit): un strat al unui tip de perete poate continua în jos (`MaterialLayer.ExtendDown`, ↓ în coloana **Into floors** din Wall types) și în sus (`ExtendUp`, ↑).

- **Regula** e cea de la îmbinarea pereților (raportul #81): un strat trece prin straturile celuilalt element cu prioritate mai mică și se oprește la primul cu prioritate cel puțin egală. Prioritățile sunt ale straturilor (0–100: a stratului, altfel a materialului în Cut order, altfel 50 pentru miez și 0 pentru rest, `Layers.Rank`), deci și straturile plăcilor au acum coloana **Priority** în Floor types (înainte, salvarea unui tip de placă o pierdea).
- **În jos**: o placă pe care stă peretele (baza lui la fața de sus a plăcii, sau în ea, la 5 mm): de la bază prin straturile plăcii de rang mai mic, până la fața de sus a primului de rang cel puțin egal (fundul plăcii dacă nu e niciunul). Implicit, miezul de cărămidă (50) trece prin pardoseală și șapă (0) și stă pe beton (50); o tencuială de 10 trece printr-o pardoseală de 0 și stă pe o șapă de 20.
- **În sus**: o placă pe care o atinge capul peretelui (fața de jos între baza și capul lui): de la cap (sau de la fața de jos a plăcii, când placa e mai sus în ordinea de tăiere și îi taie capul) prin straturile de jos ale plăcii de rang mai mic, până la fața de jos a primului de rang cel puțin egal. Golurile dintre perete și placa de deasupra nu se umplu (un parapet rămâne parapet).
- **Geometria** (`Walls.Extensions`, Lapin.Geometry/Lapin.Booleans): pentru fiecare strat bifat, amprenta lui după îmbinări (`Walls.LayoutOf`, aceeași cu a peretelui, deci colțurile și T-urile ies corect), între cele două cote, păstrată în conturul plăcii (intersecție); adăugată după prioritățile de tăiere (placa, de prioritate 60, ar lua-o înapoi) și unită cu partea stratului de care se lipește, deci secțiunea nu desenează nicio linie la fața plăcii. Placa scade aceleași bucăți din straturile ei (`Slabs.WallExtensions`, citit doar când un tip de perete are un astfel de strat). Un miez zonat coboară cu prima zonă și urcă cu ultima. Pereții curbi nu intră încă în plăci.
- **Cantitățile** urmează volumele: miezul peretelui crește, șapa și pardoseala plăcii scad (NetVolume și NetArea pe strat).
- **IFC**: corpurile se scriu cum sunt construite; seturile de straturi rămân (`IfcMaterialLayerSetUsage`), iar fiecare strat bifat primește un `IfcMaterialProperties` „Lapin_LayerJoins” (ExtendToFloorBelow, ExtendToFloorAbove) pe `IfcMaterialLayer`; importul le citește înapoi. `IfcMaterialLayer.Category` scrie categoria proprie a stratului (Core rămâne pentru miez).
- **Proiectele vechi**: nebifat înseamnă nul în fișier, deci un proiect salvat înainte se deschide neschimbat, fără `IProjectUpgrade`. Un tip nou din dialog are miezul bifat în jos; tipurile făcute din comenzi (inclusiv cel implicit) nu.

Teste: `WallFloorLayerJoinTests` (6), `FinishLayerViewTests` (6); `layerFlags.test.ts`, `finishSnaps.test.ts`; scenele `wall-into-floor` și `finishes-by-view`.

## Rămase

- Seturile de grafică (Visibility/Graphics) legate de stil; acum vederea își ține setul ei.
- Intervalul vederii ca parte a stilului (acum e al vederii).
- Pereții curbi prin straturile plăcilor; tăierea prelungirilor de alte elemente (un stâlp, o grindă din grosimea plăcii); prelungirea sub o ușă (acum zidăria trece și pe sub prag).
