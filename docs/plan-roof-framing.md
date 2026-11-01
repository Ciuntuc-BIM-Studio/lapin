# Șarpanta parametrică din lemn (extinderea acoperișului)

Cerut de utilizator la 01.11.2026, după propunerea unui tester (șarpante scandinave: *takstolar*, profile C24, interax 600/1200 mm, crestătura pe cosoroabă, grupare IFC). Decizia 229 în `decisions-pending.md`. Rebar e pe pauză până atunci.

## Ce exista

Un acoperiș cu `structure: rafters` își deriva singur șarpanta (`RoofParts.Framing`): căpriori pe fiecare pantă la interaxul dat, pane, coama, coame de dolie și astereală la luminatoare, cosoroabe pe pereții cu streașină. Secțiunile: o listă fixă în cm (8×16 … 12×22), căpriorii cutii cu capete în echer, totul într-o singură parte „Timber structure”, exportată ca un singur `IfcMember`.

## Ce adăugăm

### 1. Sistemul de șarpantă (`structure`)

- `rafters` (ca până acum): căpriori pe pane, cu coamă.
- `collar`: **căpriori cu clește** (*hanbjälke*): perechi de căpriori pe fiecare stație, fără pane și fără grindă de coamă, cu un clește orizontal la `collarHeight` din înălțimea acoperișului (implicit 0,66).
- `trusses`: **ferme prefabricate** (*takstolar*) la interax: talpa superioară pe pante, talpa inferioară pe cosoroabe, diagonale în W (Fink) la o fermă în două ape, ferma într-o apă cu montant la capătul înalt și diagonale în N; deschideri sub 3 m primesc doar un montant.
- `none`: placă fără șarpantă (ca până acum).

Stațiile (cleștii și fermele) merg de-a lungul coamei principale (cea mai lungă; la acoperișul într-o apă, de-a lungul streșinii); pantele paralele cu ea sunt cele principale. Unde o stație taie o pantă de șold (capetele unui acoperiș în patru ape), stația rămâne pe căpriori; pantele de șold au mereu căpriori, ca până acum.

### 2. Secțiuni și clasă

- `rafterSection` primește secțiunile nordice în mm: 45×95 … 45×245, 70×220 (scrise „45x195 mm”); cele vechi rămân în cm.
- `webSection`: secțiunea diagonalelor fermelor (implicit 45×95 mm).
- `timberGrade`: C14, C16, C18, C24, C30, GL24h, GL28h, GL30c (implicit C24), în extras și în IFC.
- Interaxul (`rafterSpacing`) rămâne același câmp: 0,6 sau 1,2 m la cele nordice.

### 3. Capetele căpriorilor

- **Crestătura pe cosoroabă** (*bird's mouth*): talpa orizontală pe cosoroabă și călcâiul vertical la fața ei exterioară, cel mult o treime din înălțimea căpriorului (`rafterSeat: birdsmouth | none`).
- **Capătul la streașină** (`rafterTail`): vertical (*plumb*, implicit) sau în echer cu căpriorul (*square*).
- La coamă căpriorii se taie vertical și se întâlnesc pe linia coamei.

### 4. Fiecare piesă e a ei

Fiecare piesă e o parte a acoperișului, cu rolul ei, astfel că intră în cantități și în IFC una câte una:

- IFC: `IfcMember` cu tipul lui (RAFTER, PURLIN, PLATE, COLLAR, CHORD, MEMBER pentru diagonale, USERDEFINED pentru coamă, dolie, astereală), fiecare fermă un `IfcElementAssembly` (TRUSS) cu piesele ei, totul sub `IfcRoof`.
- Setul de proprietăți `Lapin_Timber` pe fiecare piesă: rolul, secțiunea, clasa, lungimea.
- Cantitățile acoperișului (`Lapin_RoofStructureQuantities`): volumul, numărul de căpriori, de ferme, de clești, lungimile pe rol.
- Extrasul **Roof timber** (`IScheduleSource` kind `roofTimber`): acoperiș, rol, secțiune, clasă, lungime (la 10 mm), bucăți, lungimea totală, volumul — o listă de debitare.

### 5. Stiluri

Două stiluri noi în catalog, care își aduc și șarpanta: **Swedish truss roof** (ferme la 1,2 m, tălpi 45×170 mm, diagonale 45×95 mm, C24, 27°, țiglă de beton) și **Nordic collar-tie roof** (căpriori cu clește 45×195 mm la 0,6 m, C24, 38°, tablă). Un stil cu șarpantă o aplică și pe un acoperiș existent; celelalte păstrează șarpanta acoperișului.

## Mai târziu

- Ferme de șold și căpriori de colț cu tăieturi compuse (acum căpriorii se opresc în echer la linia de șold).
- Plăcile multiplex, contravântuirile, căpriorii dubli la goluri, ancorajele (o listă de feronerie).
- Planul șarpantei ca desen propriu, cu mărcile pieselor; verificarea secțiunilor (EC5) e premium.
