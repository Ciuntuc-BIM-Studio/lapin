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

## Extrasul de lemn

**Wood → Timber schedule** face lista de debitare a întregului lemn din proiect, piesele pereților și ale șarpantelor: piesa, secțiunea, clasa, lungimea, bucățile, lungimea totală și volumul, cu totalurile pe secțiune. În IFC fiecare piesă e un IfcMember (STUD, PLATE, buiandrugul și pervazul cu numele lor) sub peretele ei, iar izolația un IfcCovering INSULATION.
