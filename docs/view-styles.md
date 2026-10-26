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

- **View → View settings** (vederea din față) și **proprietățile unei vederi**: stilul, straturile, unirea materialelor, overhead-ul, intervalul vederii (planul de secționare, sus, jos, adâncime: View range…) și etajele afișate (decizia 136).
- **Manage → View styles**: stilurile proiectului, cu nume, disciplină, straturi, unire și overhead; duplicare, stil nou, ștergere (vederile lui iau stilul disciplinei).
- Comenzi: `viewStyle.create {from?, name?, …}`, `viewStyle.update {id, …}`, `viewStyle.delete {id}`, `view.setStyle {id, style?, layers?, joins?, overhead?}` (null: înapoi la stil); interogări `viewStyle.list`, `view.style {id}`.

Totul e în desenul motorului (`Views.Gather`, `Views.Spec`, `Views.Render`), deci planșele tipăresc ce arată ecranul. Teste: `ViewStyleTests`.

## Rămase

- Stilul în 3D (miezurile singure în vederea 3D).
- Seturile de grafică (Visibility/Graphics) legate de stil; acum vederea își ține setul ei.
- Intervalul vederii ca parte a stilului (acum e al vederii).
- Categoria pe strat, nu doar pe material (`IfcMaterialLayer.Category`).
