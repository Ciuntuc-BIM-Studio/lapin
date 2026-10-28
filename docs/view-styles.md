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

- **Straturi**: toate; fără finisaje (straturile din materiale de finisaj); doar miezul. Un element fără miez marcat își păstrează toate straturile. Părțile care nu sunt straturi (talpa fundației, stâlpii, grinzile) se desenează mereu.
- **Finisajele** se recunosc după categoria materialului (`IfcMaterial.Category`, exportată în IFC): Manage → Materials → Category (Structure, Finish, Insulation, Membrane, Other). Fără categorie, un material e finisaj după nume: plaster, render, paint, tile, gypsum, parquet, laminate, carpet, tencuială, glet, vopsea, faianță, gresie, parchet, rigips, finisaj, zugrăveală.
- **Overhead**: dacă planul desenează ce e deasupra planului de secționare, până la marginea de sus a intervalului.

O vedere ia stilul pe care îl numește (`viewStyle`), altfel pe al disciplinei ei. Peste stil, vederea poate seta singură oricare dintre câmpuri (`styleOverrides`); ce setează ea câștigă, iar butonul de lângă câmp o întoarce la stil.

## Unde se setează

- **Disciplina** unei vederi sau planșe, în proprietățile ei (selector `DisciplinePicker`: disciplinele implicite, ale seturilor browserului, ale stilurilor și cele deja folosite; alta se tastează). Browserul o așază sub setul disciplinei; vederea fără stil numit desenează cu stilul disciplinei. Când nu e nimic selectat, panoul de proprietăți arată vederea din față (`FrontViewPanel`).
- **Nivelul de referință** al unui plan (planul altui nivel, de sub sau de deasupra, estompat pe ecran, cu snap, netipărit): pe vederea de plan (`underlay`, `view.setUnderlay`), în secțiunea View settings și în cipul din bara planului. Deschiderea unui plan propriu din navigator îl selectează, deci setările lui sunt în panou cât e selectat.

- **View → View settings** (vederea din față) și **proprietățile unei vederi**: stilul, straturile, unirea materialelor, overhead-ul, simbolurile 2D din IFC (`ifc2d`, decizia 148), intervalul vederii (planul de secționare, sus, jos, adâncime: View range…) și etajele afișate (decizia 136).
- **Manage → View styles**: stilurile proiectului, cu nume, disciplină, straturi, unire, overhead și IFC 2D; duplicare, stil nou, ștergere (vederile lui iau stilul disciplinei).
- Comenzi: `viewStyle.create {from?, name?, …}`, `viewStyle.update {id, …}`, `viewStyle.delete {id}`, `view.setStyle {id, style?, layers?, joins?, overhead?, ifc2d?}` (null: înapoi la stil); interogări `viewStyle.list`, `view.style {id}`.

Totul e în desenul motorului (`Views.Gather`, `Views.Spec`, `Views.Render`), deci planșele tipăresc ce arată ecranul. Teste: `ViewStyleTests`.

## În 3D (decizia 162)

O vedere 3D are și ea disciplină, stil și **straturi**, ca o planșă: {3D} le ține pe proiect (`view3d`: `discipline`, `viewStyle`, `styleOverrides`, lângă `storeys`), o vedere 3D salvată pe ea însăși (duplicarea lui {3D} le copiază). O vedere 3D cu disciplina Structure arată doar miezurile; din proprietățile ei (și ale lui {3D} când e în față), din **View → View settings** cu vederea 3D în față (`view.setStyle {id: "3d" | id, discipline?, style?, layers?}`) sau din eticheta **Layers shown** din josul vederii (× înapoi la toate straturile: alegerea proprie dispare când stilul arată totul, altfel devine „toate”).

- Randarea nu se schimbă pentru planuri sau alte vederi: motorul trimite toate părțile, iar fiecare parte are `hiddenBy`, modurile care o lasă deoparte (`ViewStyles.HiddenBy`: tencuiala `noFinishes` și `core`, izolația `core`). Fiecare strat e un solid închis al lui (pereți drepți și curbi, zonați, planșee, fundații continue), deci ce rămâne e închis. Categoria materialului e citită prin vederea de urmărire a construcției, deci o categorie schimbată reconstruiește elementul.
- Interfața (`viewport/layers3d.ts`, aceeași regulă ca `ViewStyles.Of`) desenează elementul fără acele părți: alegerea, Fit, corpul plimbării și capacele urmează. `view.caps {layers}` închide doar straturile păstrate; `project.exportMesh` cu o vedere 3D ia straturile ei (`MeshExportOptions.Layers`, sau `layers` explicit), „everything” le ia pe toate.
- Ușile și ferestrele rămân în goluri; un șpalet (`reveal:*`) dintr-un material de finisaj dispare cu finisajele peretelui (doar în 3D: planurile îl desenează ca înainte). Pragurile, glafurile și ancadramentele rămân.
- Acoperișurile nu au straturi (învelitoare, șarpantă), deci rămân întregi; pereții cortină nu sunt stratificați.

Teste: `View3dLayersTests`, `layers3d.test.ts`; scena `views-3d-layers`.

## Rămase

- Seturile de grafică (Visibility/Graphics) legate de stil; acum vederea își ține setul ei.
- Intervalul vederii ca parte a stilului (acum e al vederii).
- Categoria pe strat, nu doar pe material (`IfcMaterialLayer.Category`).
