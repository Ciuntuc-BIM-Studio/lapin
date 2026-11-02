---
title: Lemn
order: 54
summary: Pereți pe cadre din lemn din configurator: montanți pe modul, tălpi, rigle, și fiecare ușă și fereastră încadrată după regulile ei.
---

## Pereți pe cadre din lemn

Un tip de perete poate fi construit pe cadre din lemn: **Wood → Timber frame** deschide configuratorul, cu tipurile de pereți ale proiectului în stânga. Bifezi **Timber frame** și miezul tipului se face din piese în loc de un singur strat: tălpile, montanții pe modulul lor, rândurile de rigle și, în jurul fiecărei uși și ferestre, montanții întregi, montanții de sprijin, buiandrugul și pervazul. Celelalte straturi (placarea, plăcile, gips-cartonul) rămân cum sunt, iar izolația umple golurile dintre piese.

![Configuratorul de cadre din lemn](shots/wood-frame.webp)

- **Presetări**: **nordică** (45 × 145 la 600 mm, C24) sau **românească** (50 × 150 la 625 mm, C24, un rând de rigle la 1,25 m). Orice câmp se poate schimba apoi.
- **Montanți, interax, clasă, tălpi, montanți de capăt, rânduri de rigle**. **Modulul de la** spune de unde pornește modulul montanților: de la începutul fiecărui perete sau de la originea proiectului, ca pereții unei fațade să-l aibă pe același.
- **Portant**: un perete despărțitor nu duce nimic și primește peste goluri o piesă culcată în loc de buiandrug.

## Uși și ferestre

Ușile și ferestrele rămân ale arhitectului: muți una sau îi schimbi tipul, și încadrarea ei o urmează. Cadrul se măsoară de la **golul brut**: ușa sau fereastra plus jocurile de montaj (lateral, sus, sub fereastră; ușa coboară până la placă).

Fiecare gol ia regula **treptei lui de lățime** (tabelul de sub **Openings**: până la 900 mm, până la 1800 mm, peste, implicit): montanții întregi și de sprijin pe fiecare parte, buiandrugul (niciunul, masiv, LVL sau lamelar, secțiunea și bucățile alăturate) și pervazul. În golul brut nu stă niciun montant; cei scurți de deasupra buiandrugului și de sub pervaz păstrează modulul, ca rosturile plăcilor să cadă tot pe montanți. Două goluri mai apropiate decât **spaletul minim** au un buiandrug comun și montanții întregi de la capete. Un gol prea aproape de capătul peretelui apare în proprietățile peretelui, nu se mută.

Selectezi o ușă sau o fereastră: secțiunea **Wood** arată golul brut, regula pe care o ia și ce primește. Bifezi **Regulă proprie** ca doar golul acela să aibă alți montanți, alt buiandrug sau alt pervaz.

## Colțuri, plăci și elevația de cadru

- **Colțuri**: unde alt perete se oprește în capătul acestuia (colț cap la cap), peretele care trece primește **trei montanți** (unul după fața interioară a celuilalt), colț **California** (un montant culcat pe fața interioară, pentru placare) sau **doar montanții de capăt**. Un colț la 45° păstrează montanții de capăt ai ambilor pereți.
- **Îmbinări în T**: unde un perete se oprește în lungul acestuia, acesta primește **câte un montant de fiecare parte** a lui sau **montanți și rigle-scară** între ei.
- **Plăci**: bifezi un strat al alcătuirii (OSB, plăci fibrolemnoase, gips-carton) și îi dai mărimea foii: stratul se pune din foi verticale, cu rosturile pe modulul montanților, tăiate la golurile brute și de-a lungul coronamentului. **Wood → Boards schedule** numără foile fiecărui perete, aria netă și ce se taie.
- **Elevația de cadru**: în secțiunea **Wood** a unui perete pe cadre, butonul face o secțiune de-a lungul peretelui prin mijlocul miezului, la 1:20, cu toate piesele tăiate, cu un lanț de cote prin golurile brute, modulul montanților sub el și mărimea fiecărui gol brut. O pui pe planșă ca pe orice vedere.

## Panouri prefabricate

Bifează **Prefabricated panels** ca structura să fie făcută în atelier: dă **cel mai lung panou** (cât permit masa atelierului și camionul) și **cel mai scurt**. Fiecare perete e tăiat în panouri la cel mai lung rost care încape în lungimea dată, pe modulul montanților unde se poate, niciodată prin montanții întregi, montanții de sprijin, buiandrugul sau pervazul unui gol și nici prin dublura unei intersecții în T. Fiecare panou e încadrat separat, cu montanții de capăt și tălpile lui la rosturi. Un panou care nu poate rămâne în lungime (un gol prea lat pentru el) rămâne întreg, iar peretele o spune.

![Un perete prefabricat în configurator](shots/wood-panels.webp)

Panourile sunt marcate cu numele peretelui și numărul lor de-a lungul lui (W3-P1, W3-P2…). Secțiunea **Wood** a peretelui le arată lungimile, elevația de cadru desenează rosturile și marcajele deasupra peretelui, iar **Wood → Wall panels** listează fiecare panou cu peretele, lungimea, înălțimea, golurile, piesele, volumul de lemn și masa, ca să poată fi comandate, făcute și ridicate pe rând. Plăcile de pe un panou poartă și ele marcajul lui. În IFC fiecare panou e un IfcElementAssembly (Timber frame panel) cu piesele și plăcile lui.

## CLT

În același configurator, un tip de perete sau de placă poate fi construit **din panouri CLT**: dai straturile (grosimea fiecăruia, 3, 5, 7 sau 9), clasa, panoul cel mai lung și cel mai înalt (sau cel mai lat) pe care îl face presa, îmbinarea dintre panouri (cap la cap, falț pe jumătate, lambă), cât stă un gol de marginea panoului și jocul de montaj. Peretele se taie în panouri pe lungimea lui, cu rosturile mutate departe de goluri; fiecare panou e chiar corpul miezului, cu golurile brute decupate. Placa se taie în panouri peste deschiderea ei (pe direcția scurtă, pe x sau pe y). **Wood → CLT panels** dă fiecare panou cu marca, dimensiunile, straturile, clasa, îmbinarea, aria și volumul; în IFC fiecare e un IfcBuildingElementPart numit CLT panel.

- **Îmbinări**: la **suprapunere pe jumătate** (half lap) cele două jumătăți se suprapun cu lungimea dată; o îmbinare cu **lamelă** (spline) are un fălțuit pe o față și o fâșie de placaj în el, ca piesă separată. Amândouă sunt modelate în panouri, deci volumele și tabelul sunt ce taie atelierul.
- **Colțurile golurilor**: o freză nu poate tăia un colț interior drept. **Raza frezei** o lasă în fiecare colț; **degajarea „dog-bone”** taie un cerc în fiecare colț, ca o tâmplărie dreptunghiulară să încapă. Configuratorul desenează conturul tăieturii.
- **Elevația panourilor**: secțiunea **Wood** a unui perete CLT face o secțiune de-a lungul lui la 1:20, cu rosturile și marcajele panourilor și dimensiunile golurilor brute.
- **Acoperișuri CLT**: selectează un acoperiș în pante și bifează **CLT panels** în secțiunea lui **Wood**: pantele lui sunt făcute din plăci CLT în locul căpriorilor sau fermelor (cosoroabele și învelitoarea rămân), cu golurile ferestrelor de acoperiș și lucarnelor tăiate, în fâșii late cât panoul de-a lungul streașinii, îmbinate cap la cap unde panta e mai lungă decât un panou.

## Planșee pe grinzișoare

Un tip de planșeu poate fi făcut **pe grinzișoare**: alege-l în configurator și alege **on joists**. Dă secțiunea, distanța și clasa grinzișoarelor, direcția pe care descarcă (pe latura scurtă sau după x ori y), dacă o **grindă de bordură** închide marginile, **rândurile de distanțieri** și, în jurul fiecărui gol din planșeu (un gol de scară, un ghenă), **grinzile de schimb** de fiecare parte și **straturile grinzii de capăt** la capete. Grinzișoarele întrerupte de gol se opresc în grinda de capăt, iar o grinzișoară în plus se pune unde grinzile de schimb lasă un spațiu mai mare decât distanța dată. **Fill the cavities** pune materialul miezului între grinzișoare. Configuratorul arată un planșeu de probă cu un gol de scară.

![Un tip de planșeu pe grinzișoare](shots/wood-joists.webp)

Grinzișoarele înlocuiesc miezul planșeului; straturile de deasupra și de dedesubt rămân. Piesele lor apar în extrasul de lemn, iar în IFC fiecare e un IfcBeam (JOIST, grindă de bordură sau grindă de capăt după nume) sub planșeu.

## Extrasul de lemn

**Wood → Timber schedule** face lista de debitare a întregului lemn din proiect, piesele pereților și ale șarpantelor: piesa, secțiunea, clasa, lungimea, bucățile, lungimea totală și volumul, cu totalurile pe secțiune. În IFC fiecare piesă e un IfcMember (STUD, PLATE, buiandrugul și pervazul cu numele lor) sub peretele ei, iar izolația un IfcCovering INSULATION.
