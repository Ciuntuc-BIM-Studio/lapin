---
title: Plăci și acoperișuri
order: 40
summary: Desenezi plăci și acoperișuri după contur, alegi forma și streașinile acoperișului și tai goluri.
---

## Plăci

Alege **Arhitectură → Placă** (<kbd>SB</kbd>), apoi dă clic în colțurile conturului. Închide conturul pe primul punct sau apasă <kbd>Enter</kbd>. Și placa e făcută din straturi, setate în **Arhitectură → Tipuri de plăci**.

Ca să schimbi conturul, selectează placa și trage de colțuri. **Gol în placă** (<kbd>SH</kbd>) taie o gaură în placă, pentru o scară sau un ghen.

O placă desenată în afara pereților, în fața unei uși, e o terasă. Pentru treptele până la ea, vezi [Scări](stairs.md).

## Acoperișuri

Alege **Arhitectură → Acoperiș** (<kbd>RF</kbd>) și dă clic pe conturul pereților de dedesubt. Lapin construiește acoperișul peste acel contur, la partea de sus a nivelului.

![O casă cu acoperiș în două ape, în vederea 3D](shots/house-3d.webp)

Selectează acoperișul ca să-l schimbi în panoul de proprietăți:

- **Formă.** În patru ape, în două ape, într-o apă, mansardă, boltit sau terasă. Acoperișul boltit e un arc între două frontoane; **Boltă: săgeata la cheie** spune cât urcă la mijloc, cel mult jumătate din deschidere.
- **Panta și consola streașinii**, pentru tot acoperișul.
- **Direcția coamei (în două ape, într-o apă)**: auto, x sau y. La acoperișul într-o apă coama e latura de sus: pe x apa curge spre −y, pe y spre −x; **Într-o apă: inversează panta** o întoarce.
- **Fiecare latură** cu felul ei (streașină sau fronton), panta și consola ei. O mansardă cu laturi fronton dă un acoperiș frânt.
- **Stil.** Stilurile de acoperiș dau tot aspectul dintr-o alegere: învelitoarea, streașinile, jgheaburile și burlanele.

![Proprietățile unui acoperiș](shots/roof-properties.webp)

**Configuratorul de acoperiș** (butonul de lângă stil, sau **Architecture → Roof configurator** cu un acoperiș selectat) adună totul într-un loc: stilurile în stânga, apoi forma și laturile, învelitoarea și apele, șarpanta și golurile, deasupra unui plan al acoperișului (clic pe o latură o face auto, streașină sau fronton) și, la șarpantă, o secțiune peste coamă cu ferma sau perechea de căpriori de la mijloc. Pornești de la un stil și schimbi orice câmp de mână; **Apply** face totul un singur pas de Undo. **Salvează ca stil…** păstrează acoperișul așa cum e configurat ca stil al proiectului, listat la **Project** deasupra celor din catalog (și în lista din panoul de proprietăți), pentru alte acoperișuri și pentru cele noi; **Actualizează stilul** salvează din nou schimbările în el, iar toate acoperișurile stilului le preiau. Un stil al proiectului se poate redenumi sau șterge; acoperișurile lui rămân cum sunt.

![Configuratorul de acoperiș](shots/roof-configurator.webp)

**Jgheaburi și burlane.** În **Învelitoare și ape** din configurator, jgheabul are mărimea, distanța în afara streșinii, coborârea sub învelitoare, panta spre guri (mm/m) și capacele de capăt; tabelul laturilor pornește sau oprește jgheabul fiecărei streșini. Burlanele pornesc ca ale acoperișului, câte unul la 10 m; **Fă-le editabile** le face elemente, iar **Architecture → Downpipe** adaugă unul cu un clic pe jgheab. Gripul unui burlan îl mută doar de-a lungul jgheabului; în proprietăți îi dai diametrul, cotul de la bază și traseul, un lanț de segmente, fiecare cu lungimea și unghiul lui față de verticală (spre perete), ultimul până la sol. În IFC jgheabul e IfcPipeSegment GUTTER, iar burlanul un segment rigid numit Downpipe.

Pereții de pe nivelul acoperișului se opresc singuri sub el și urcă la frontoane, și sub un acoperiș boltit. Un perete de pe alt nivel urmează acoperișul după **Arhitectură → Atașează la acoperiș** (<kbd>AR</kbd>): selectezi pereții, apoi dai clic pe acoperiș. Aceeași unealtă primește orice alt corp de deasupra pereților: un solid din Studio, o placă, o grindă, un element importat din IFC. Pereții urcă până la fața lui de jos și sunt tăiați după ea, oricare i-ar fi forma. **Detașează de acoperiș** anulează asta.

**Fereastră de acoperiș** și **Lucarnă** adaugă goluri în acoperiș.

### Șarpanta din lemn

**Structură** spune cum e făcută șarpanta:

- **rafters**: căpriori pe pane, cu grindă de coamă.
- **collar**: perechi de căpriori cu un clește orizontal la **Clești: partea din înălțimea acoperișului** (implicit două treimi), fără pane.
- **trusses**: ferme prefabricate de-a lungul coamei principale, la **Distanța între căpriori**: tălpile superioare pe pante, talpa inferioară pe cosoroabe, diagonale în W (fermă într-o apă la acoperișul într-o apă). Capetele unui acoperiș în patru ape își păstrează căpriorii.
- **none**: o placă, fără lemn.

**Căpriori și tălpi** primește secțiunile vechi în cm sau pe cele nordice în mm (45×95 până la 45×245); alături, **Diagonalele fermelor** și **Clasa lemnului** (C14 până la C30, lemn lamelar). Căpriorii stau pe cosoroabă printr-o crestătură (**Așezarea căpriorilor pe cosoroabă**) și se termină la streașină vertical sau în echer (**Capătul căpriorilor la streașină**). Stilurile **Swedish truss roof** și **Nordic collar-tie roof** își aduc șarpanta cu ele.

Fiecare piesă e o parte a acoperișului: extrasul **Roof timber** le dă ca listă de debitare (piesa, secțiunea, clasa, lungimea, bucățile, volumul), iar în IFC fiecare e un IfcMember cu tipul lui, fiecare fermă un IfcElementAssembly.

## Tavane false

**Finisaje → Tavan** (<kbd>CG</kbd>) desenează un tavan fals pe un contur pe care îl dai prin clicuri; **Tavan într-o cameră** umple camera pe care dai clic, după conturul ei. Un tavan stă la **Înălțimea** lui peste etaj (2700 mm, dacă nu o schimbi în proprietăți).

Tipul lui, din **Finisaje → Tipuri de tavan**, alege **Sistemul**: **Plăci pe structură aparentă** (lățimea, lungimea și grosimea plăcii, lățimea vizibilă, adâncimea și rotirea structurii, tijele de suspendare până la structură și distanța dintre ele) sau **Gips-carton, continuu**. Structura e centrată pe tavan, așa că plăcile tăiate de la margini ies egale.

**Corp de iluminat** și **Anemostat** pun unul în caseta pe care dai clic; un clic pe aceeași casetă cu cealaltă unealtă îl schimbă. În panoul de proprietăți, **Așază** pune corpuri de iluminat sau anemostate la fiecare câteva casete, iar **Golește** le scoate. Extrasele numără plăcile întregi, corpurile de iluminat și anemostatele, iar tavanul iese în IFC ca IfcCovering de tip CEILING.

![Proprietățile unui tavan: tipul, înălțimea și echipamentele](shots/ceiling-properties.webp)

![Tavane casetate cu corpuri de iluminat și anemostate, în 3D](shots/ceilings-3d.webp)
