---
title: Constrângerile schiței
order: 88
summary: Ține împreună formele unei schițe cu constrângeri (orizontală, tangentă, egală, fixă…) și cote, vezi cât de liberă mai este și lasă o cotă schimbată să miște solidul construit din ea.
---

## Ce sunt constrângerile

O schiță este mulțimea formelor desenate pe o vedere: un plan, o secțiune, o fațadă sau un plan de referință. O **constrângere** este o regulă despre acele forme, care rămâne adevărată cât le editezi: *latura asta e orizontală*, *astea două sunt egale*, *punctul acesta stă pe cercul acela*. O **cotă** este o constrângere cu un număr: *latura asta are 4 m*. Motorul rezolvă schița la fiecare schimbare, așa că ce leagă regulile se mișcă laolaltă, iar un solid, o baleiere sau o piesă construite din schiță o urmează.

Constrângerile aparțin schiței și se salvează cu proiectul. Nu se scriu în IFC; se scriu formele, așa cum au fost rezolvate.

## Uneltele Constrângeri

**Studio → Constrângeri** (al treilea grup din fila Studio, deci se vede întreg la 1440 px fără să derulezi panglica) are câte o unealtă pentru fiecare fel. Fiecare cere în vedere ce îi trebuie: clic pe un punct, o muchie, un cerc sau un arc. Partea de sub cursor se luminează și își spune numele (*vertex*, *start*, *end*, *centru*, *muchie*, *curbă*) și se prinde ca la desenare. Când alegerile sunt complete, constrângerea se face, iar unealta rămâne pornită pentru următoarea; **Esc** o oprește.

![O unealtă luminează muchia de sub cursor și o numește](shots/sketch-constraints-pick.webp)

![Grupul Constrângeri din fila Studio](shots/sketch-constraints-ribbon.webp)

| Unealta | Alegi | Rezultat |
|---|---|---|
| Coincidentă | două puncte | un singur punct |
| Punct pe obiect | un punct, apoi o dreaptă, un cerc sau un arc | punctul rămâne pe el |
| Orizontală, Verticală | o dreaptă sau două puncte | la nivel, la plumb |
| Paralelă, Perpendiculară | două drepte | |
| Tangentă | o dreaptă și un arc, două arce sau două capete | se ating, nu se taie |
| Egală | două drepte sau două cercuri ori arce | aceeași lungime sau rază |
| Simetrică | două puncte, apoi o dreaptă sau un punct | oglindite față de ea |
| Mijloc | un punct, apoi o dreaptă | la mijlocul ei |
| Fixează | un punct, o dreaptă, un cerc sau o polilinie întreagă | ținut unde este |

Dacă a doua alegere se poate lua în mai multe feluri (două forme se întâlnesc într-un colț), clicul înseamnă prima parte a primei forme care se potrivește; clic din nou pe cealaltă ca s-o iei pe ea. O alegere care nu poate face constrângerea e refuzată în linia de comandă, cu ce primește unealta.

Merge și tastând comanda: `CNHORIZONTAL`, `CNFIX`, `CDIM`… sunt în indiciul fiecărei unelte.

## Cotele

**Cotă** este unealta deșteaptă: ce alegi spune ce măsoară. Două puncte, sau un punct și o dreaptă: distanța. O dreaptă: lungimea ei (apeși **Enter**), sau o a doua dreaptă pentru unghiul dintre ele. Un cerc sau un arc: raza. Apoi tastezi valoarea:

- un număr, cu unitatea dacă vrei (`4 m`, `250 mm`, `45°`);
- `=formulă`, în parametrii piesei a cărei rețetă folosește schița (`=W / 2`);
- **Enter** ca să păstrezi mărimea pe care o are forma acum;
- `Driven`, pentru o **referință**: măsoară și nu conduce.

Cuvintele-cheie după o alegere schimbă felul: `X` și `Y` pentru distanța orizontală sau verticală, `D` pentru diametru. **Distanță orizontală**, **Distanță verticală**, **Rază**, **Diametru** și **Unghi** sunt aceeași unealtă pentru câte un fel.

O cotă se desenează ca o cotă de desen, cu valoarea ei. Una **condusă de alta** (referință) e în paranteze și punctată. Cota cu formulă își arată valoarea, iar formula când treci cu cursorul peste ea. Unghiurile au semn, antiorar, în grade.

## Simboluri, selectare și editare

Fiecare altă constrângere are un simbol mic lângă ce ține: **H**, **V**, **∥**, **⊥**, un lacăt pentru fix și desene pentru coincidentă, punct pe obiect, tangentă și simetrică. Simbolurile aflate împreună se desfac în evantai ca să nu se ascundă unul pe altul.

- Clic pe un simbol sau pe o cotă o selectează (Shift adaugă); **Delete** șterge selecția, ca un singur pas de anulare.
- **Dublu clic** pe o cotă îi editează valoarea pe loc, în câmpul care se deschide: un număr sau `=formulă`. O cotă de referință devine conducătoare când îi dai o valoare, dacă prin asta schița nu e supra-constrânsă.
- **Trage** o cotă de valoare sau de linie ca s-o așezi: urmează cursorul și rămâne unde îi dai drumul, ca un singur pas de anulare. O cotă pe care n-ai așezat-o stă în afara formelor ei, de partea dinspre exterior. Una așezată își păstrează locul față de ce măsoară și se mută, se rotește și se oglindește cu formele ei.
- Ochiul de pe insigna de jos ascunde constrângerile în această vedere și le arată din nou. Se ține minte pentru vedere.

![O cotă editată pe loc](shots/sketch-constraints-edit.webp)

## Cât de liberă e schița

O insignă în josul vederii spune ce a găsit rezolvitorul, cât timp vederea are constrângeri sau o unealtă de constrângeri e pornită:

- **N grade de libertate**: schița se mai poate mișca sau schimba în N feluri. Formele care se mai pot mișca sunt albastre, iar punctele lor libere au câte un punct albastru.
- **Complet constrâns**: nimic nu se mai mișcă. Formele sunt desenate cu cerneală.
- **Constrângeri în conflict**: două reguli nu pot fi adevărate amândouă. Formele și simbolurile implicate sunt roșii, iar ultima constrângere încercată a fost refuzată cu un mesaj care le numește.
- **Constrângeri redundante**: o regulă pe care alta o face deja adevărată; simbolul ei e chihlimbar. Nu strică și poate fi ștearsă.

![Schița după Orizontală și Verticală: mai rămân patru grade de libertate, iar formele ei libere sunt albastre](shots/sketch-constraints-free.webp)

![Insigna](shots/sketch-constraints-chip.webp)

![Complet constrâns: simbolurile, cele două cote și insigna](shots/sketch-constraints-fixed.webp)

În arborele din **Studio → Piesă**, fiecare schiță arată numărul ei de constrângeri și aceleași cuvinte.

## O cotă conduce solidul

Schimbi o cotă și schița o urmează, la fel tot ce e construit din ea: aici lățimea trece de la 4 m la 6 m, iar blocul extrudat odată cu ea.

![Aceeași schiță, lată de 6 m](shots/sketch-constraints-wide.webp)

![Solidul construit din ea](shots/sketch-constraints-3d.webp)

## Tragerea

Trage un punct al unei forme constrânse de mânerul lui: restul schiței urmează regulile cât muți, arătat pe măsură ce tragi, iar rezultatul este un singur pas de anulare la eliberare. Dacă regulile nu lasă punctul acolo (un punct fix, o cotă care s-ar strica), tragerea se arată în roșu cu motivul și e refuzată. Celelalte editări (mutare, tăiere, prelungire, decalare, racordare) se rezolvă la fel. O regulă care nu poate urma o editare (forma ei a fost tăiată) se scoate cu o notificare care spune care și de ce; nu rămâne niciodată greșită.

## Constrângeri în timp ce desenezi

Într-o schiță (o vedere ale cărei forme construiesc un solid Studio, o vedere care are deja constrângeri sau când desenezi cu un rol, de pildă *profile*), ce găsește inferența pe măsură ce faci clic se păstrează ca restricții, implicit pornit:

- o prindere de **capăt** sau de **centru** face punctul coincident cu el; o prindere de **mijloc** îl face mijlocul acelei muchii; **pe muchie**, **cadran** și **intersecție** pun punctul pe obiect; **perpendicular** îl pune pe muchie și face latura perpendiculară pe ea;
- o latură trasă pe linia **ortho** sau pe una **polară** este orizontală sau verticală;
- o linie trasă lângă o altă linie desenată, într-o direcție la câteva grade de a ei, se fixează pe ea și arată indiciul **Paralelă**: cele două laturi devin paralele. O linie începută pe un arc sau pe un cerc și trasă pe tangenta lui, sau care se termină unde ar atinge doar cercul, arată indiciul **Tangentă** și devine tangentă la el (capăt la capăt când începe la un capăt al arcului). Niciuna nu se oferă cu ortho pornit;
- o lungime sau un unghi tastat devine o cotă conducătoare; o rază tastată, o rază; un dreptunghi tastat ca `@lățime,înălțime` păstrează ambele dimensiuni ca distanțe conducătoare;
- un dreptunghi are două laturi orizontale și două verticale, iar o linie începută unde s-a sfârșit precedenta se leagă de ea.

Ține apăsat **Ctrl** (⌘ pe Mac) la clic ca să pui punctul acela fără constrângeri. Comutatorul **CNSTR** din linia de comandă pornește și oprește totul. Forma și constrângerile ei sunt un singur pas: un **Undo** le scoate pe amândouă. O constrângere care ar intra în conflict cu schița e lăsată deoparte; forma se desenează oricum.

![O linie trasă din colț pe linia ortho](shots/sketch-constraints-drawing.webp)

## Mai departe

Pagina Piese spune cum devine o schiță o piesă cu parametri, pe care îi folosesc formulele din cote.
