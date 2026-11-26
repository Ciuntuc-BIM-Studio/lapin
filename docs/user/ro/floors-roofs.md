---
title: Plăci și acoperișuri
order: 40
summary: Desenezi plăci și acoperișuri după contur, alegi forma și streașinile acoperișului și tai goluri.
---

## Plăci

Alege **Arhitectură → Placă** (<kbd>SB</kbd>), apoi dă clic în colțurile conturului. Închide conturul pe primul punct sau apasă <kbd>Enter</kbd>. Și placa e făcută din straturi, setate în **Arhitectură → Tipuri de plăci**. Un strat al unui tip de placă poate avea grosimea 0 (o hidroizolație, vopseaua): nu construiește nimic, e desenat ca o linie în secțiuni și e măsurat pe suprafața plăcii.

În **Tipuri de plăci**, straturile merg de sus în jos. Săgețile de la capătul rândului unui strat îl mută cu un loc în sus sau în jos (sunt butoane, accesibile cu <kbd>Tab</kbd>), iar **Inversează straturile** pune toată alcătuirea în ordine inversă, deci stratul de sus ajunge dedesubt; întreabă mai întâi, fiindcă toate plăcile tipului se răstoarnă după ce tipul e salvat.

![Straturile unui tip de placă, cu săgețile și Inversează straturile](shots/floor-types.webp)

În **Tipuri de plăci**, fiecare strat are o **Prioritate** (0 la 100) și o **Categorie**, ca straturile unui tip de perete. Un strat de perete care coboară în placă (**În plăci** în **Tipuri de pereți**) trece prin straturile plăcii cu prioritate mai mică și se oprește pe primul cu prioritate cel puțin egală. Goală, prioritatea e a materialului din **Gestionare → Ordinea de tăiere**, altfel 50 pentru miez și 0 pentru celelalte straturi. Un strat marcat **Finisaj** dispare când o vedere lasă deoparte finisajele pardoselilor.

Ca să schimbi conturul, selectează placa și trage de colțuri. Ca să adaugi un colț, ține apăsat <kbd>Ctrl/⌘</kbd> și dă clic pe romburile mici din mijlocul unei laturi, sau dă clic dreapta pe placa selectată lângă o latură și alege **Adaugă colț aici**: colțul nou apare unde ai dat clic. Ca să scoți unul, dă <kbd>Ctrl/⌘</kbd>-clic pe el sau clic dreapta pe el și alege **Elimină colțul** (un contur păstrează cel puțin trei colțuri). Fiecare e un singur pas de anulare, și la fel merge la goluri în placă, acoperișuri, camere și balcoane. **Gol în placă** (<kbd>SH</kbd>) taie o gaură în placă, pentru o scară sau un ghen.

Ca să mărești sau să micșorezi tot conturul deodată, fiecare latură cu aceeași distanță (*Offset all edges* din Archicad), ține apăsat <kbd>Shift</kbd> și trage de rombul din mijlocul oricărei laturi: conturul urmează cursorul pe loc, în plan sau în 3D, iar distanța o poți scrie în timp ce tragi (<kbd>Enter</kbd> o pune). Sau dă clic dreapta pe placa selectată, alege **Decalează conturul…** și scrie distanța: pozitivă în afară, negativă înăuntru. Laturile drepte rămân paralele și se întâlnesc iar în colțurile lor; o latură curbă rămâne un arc în jurul aceluiași centru, iar unde un arc întâlnește altă latură într-un colț, la mărire colțul se rotunjește. Un decalaj care ar face să dispară o latură sau conturul să se intersecteze cu el însuși nu se lasă, iar motivul apare lângă cursor. Merge la plăci, goluri în placă, tavane false, acoperișuri (fiecare latură își păstrează regula de streașină sau fronton), camere și balcoane, și la mai multe selectate deodată; fiecare e un singur pas de anulare.

![Conturul unei plăci decalat trăgând cu Shift de mijlocul unei laturi](shots/floor-offset-outline.webp)

### Laturi curbe, racordări și plăci rotunde

Bara uneltei **Placă**, din linia de comandă, desenează conturul ca **Poligon** (îi dai clic pe colțuri), **Dreptunghi** (două colțuri opuse), **Cerc** (centrul, apoi un punct pe el) sau **În interiorul pereților** (un clic în interiorul pereților care o închid; placa ajunge până la fețele lor). Într-un poligon, **Linii (Spațiu)** devine **Arce (Spațiu)**: laturile următoare sunt arce, fiecare continuând tangent din latura dinainte, până apeși iar <kbd>Spațiu</kbd>. **Gol în placă** și **Tavan** au aceeași bară, fără **În interiorul pereților**.

![Unealta Placă desenând un cerc lângă o placă](shots/floor-shapes.webp)

Aceeași bară servește toate uneltele cu contur (placă, gol în placă, tavan, gol în contur, încăpere și balcon după contur, platformă, parcelă), iar trei dintre alegeri iau tot conturul dintr-un clic: **Inside walls** (pereții care se închid în jurul punctului), **From drawn shape** (cea mai mică formă desenată închisă din jurul punctului: o polilinie închisă, un dreptunghi sau un cerc, cercul rămânând rotund) și **From room** (încăperea din jurul punctului, cu laturile ei curbe).

![Bara uneltelor cu contur, cu From drawn shape ales](shots/outline-sources.webp)

### Trasează clădirea

Ca să pui o singură placă sub tot nivelul, alege **Trasează clădirea** în bara uneltei **Placă** și dă clic oriunde în plan (în interiorul clădirii sau pe unul din pereții ei). Lapin găsește pereții nivelului activ care închid clădirea și pune conturul plăcii pe **fața exterioară a miezului** lor: straturile din afara miezului (termoizolația, tencuiala, placajul) rămân pe dinafară, iar un tip de perete fără strat marcat ca miez folosește stratul cel mai gros. Rămân deoparte și pereții care nu închid nimic: un ciot de perete ieșit dintr-un perete, un gard, un zid de grădină liber. Pereții curbi dau laturi curbe, iar pereții de grosimi diferite se întâlnesc în colțurile propriilor miezuri. Placa e una obișnuită, de tipul ales în unealtă, un singur pas de anulare, editată apoi ca oricare alta.

Când pereții lasă un gol (două capete de perete la mai puțin de 1,5 m unul de altul, sau un perete care se oprește înaintea altuia), nu se face nimic: mesajul numește golul și locul lui, ca să-l închizi sau să îmbini pereții. Un gol în interiorul clădirii (o trecere lângă un perete despărțitor) nu contează. Cu două clădiri pe același nivel, se trasează cea în care dai clic; un clic în afara amândurora o ia pe cea mai mare. O curte interioară închisă de pereți nu devine gol: nu se poate deosebi de o încăpere; taie-o apoi cu **Gol în contur**.

![O placă trasată pe o clădire în L: ciotul de perete și zidul liber rămân deoparte](shots/floor-trace-building.webp)

Pentru placă, gol în placă, tavan și încăpere după contur, bara oferă și **Ellipse**: centrul, capătul unei semiaxe, apoi cealaltă. Laturile conturului merg pe elipsa adevărată (patru sferturi), măsurate exact; un colț mutat de pe elipsă îi face cele două laturi drepte, iar un colț lângă o latură eliptică nu se racordează. IFC o duce ca IfcEllipse. **From drawn shape** ia la fel o elipsă desenată. Un gol propriu al plăcii sau al tavanului (**Hole in the outline**) ia și el o elipsă. Decalarea întregului contur (Shift-tragere, **Offset outline…**) face dintr-o latură eliptică curba adevărată de la acea distanță, din laturi drepte scurte (curba aflată la o distanță de o elipsă nu e o elipsă); spre interior se oprește înainte ca curba să se plieze la cea mai strânsă curbură a elipsei.

![O placă eliptică desenată lângă o elipsă din Desen](shots/ellipse-floor.webp)

Un gol prin placă (o casă a scării, un ghene) e **Arhitectură → Gol** cu **Gol în placă** (<kbd>SH</kbd>): conturul lui desenat în placă, ca un poligon cu laturi care pot fi arce, un dreptunghi sau un cerc.

Orice latură a unei plăci, a unui gol în placă sau a unui tavan poate fi curbă. Dă clic dreapta pe placa selectată lângă o latură și alege **Fă arc**: latura se bombează în afară cu o optime din lungimea ei, iar la mijlocul arcului apare un mâner rotund. Trage de el ca să curbezi arcul prin punctul dorit (rombul, acum la mijlocul coardei, mută în continuare latura). **Fă dreaptă** o face din nou linie. Un colț între două laturi drepte poate fi rotunjit sau teșit: dă clic dreapta pe el și alege **Racordează colțul…** (tastezi raza) sau **Teșește colțul…** (tastezi cât se retrage pe fiecare latură). Colțul devine două, unde arcul sau teșitura întâlnește laturile; o rază prea mare pentru laturi e refuzată. Fiecare e un singur pas de anulare.

![O placă cu o latură curbă și un colț rotunjit](shots/floor-curved-edge.webp)

Placa e construită de-a lungul arcelor, nicio coardă mai departe de 1 mm de ele, în 3D, planuri și secțiuni; aria și perimetrul ei se măsoară pe arcele adevărate. Un strat care trece de o latură curbă (vezi [Prelungirile straturilor](walls.md#layer-extents)) urmează arcul. Și un gol poate avea laturi curbe, iar o placă curbă poate avea goluri. În IFC placa își poartă conturul cu arcele adevărate ca amprentă (un IfcIndexedPolyCurve cu segmente de arc), pe lângă corpul ei. Și laturile unui acoperiș pot fi curbe (vezi *Laturi curbe și colțuri rotunjite* la Acoperișuri): fiecare își păstrează regula de streașină sau fronton pe tot arcul.

O placă sau un tavan poate avea **goluri proprii**: insule tăiate din contur, parte a elementului, nu goluri separate. Alege **Hole in the outline** în unealta **Opening** (la *In a floor*) și desenează-l în interiorul plăcii (sau al plăcii ori tavanului selectat) cu aceeași bară: un poligon ale cărui laturi pot fi arce, un dreptunghi sau un cerc. Selectată, placa arată colțurile și laturile fiecărui gol ca mânere, cu același clic dreapta (**Make arc**, **Fillet corner…**, **Chamfer corner…**); crucea din mijlocul golului îl mută, iar clicul dreapta pe ea oferă **Remove hole**. Placa se construiește în jurul golurilor, iar aria le lasă afară, măsurate pe arcele adevărate; un gol trebuie să stea în interiorul conturului, separat de celelalte, iar un contur schimbat astfel încât nu-l mai cuprinde e refuzat. În **Edit sketch** golurile sunt și ele bucle ale schiței: desenează altă buclă închisă înăuntru și **Finish sketch** o face gol (bucla cea mai mare e conturul). În IFC golurile sunt curbe în plus ale amprentei plăcii, citite înapoi ca goluri. O platformă (**Site → Hole in platform**) primește goluri la fel: terenul din interiorul golului își păstrează cotele.

![O placă cu un gol dreptunghiular și unul rotund ale ei, selectată](shots/floor-holes.webp)

#### Editează schița

Pentru mai mult decât un colț sau un arc, dă clic dreapta pe placa selectată (sau pe golul în placă, sau pe tavan) și alege **Editează schița** (și **Studio → Editează schița**). Conturul ei se deschide ca schiță pe un plan al ei, câte o linie sau un arc pe latură, cu colțurile ținute împreună, iar placa însăși e ascunsă acolo; o bară deasupra planului spune a cui e schița și ține uneltele din Desen și uneltele de constrângere (aceeași bară ca la orice schiță: a unei piese, pe o față). Desenează și schimbă cu ele sau cu fila **Desen** (Linie, Arc, Racordare, Taie, Prelungește, Paralelă…), constrânge și cotează cu **Studio → Constrângeri** (vezi [Constrângerile schiței](sketch-constraints.md)): schimbi o cotă și conturul o urmează. **Termină schița** o scrie înapoi în placă într-un singur pas de anulare, placa păstrându-și identitatea (GlobalId-ul); **Renunță** lasă placa așa cum era. Schița trebuie să fie un singur contur închis (laturile cap la cap, sau un cerc singur); altfel Termină spune ce e deschis.

![Conturul unei plăci editat ca schiță: o racordare și o cotă](shots/floor-sketch-edit.webp)

Schița, cu constrângerile și cotele ei, rămâne pe placă (merge în IFC cu intenția de proiectare a plăcii, în Lapin_Intent), așa că **Editează schița** o redeschide cum ai lăsat-o. Dacă între timp conturul a fost schimbat din mânere, schița pornește din nou de la contur, fără constrângeri. O placă recunoscută dintr-un fișier IFC primește o schiță doar când o editezi; una păstrată ca referință nu se editează așa.

![Placa după Termină schița](shots/floor-sketch-done.webp)

O placă desenată în afara pereților, în fața unei uși, e o terasă. Pentru treptele până la ea, vezi [Scări](stairs.md).

Un strat al plăcii poate și să treacă de contur, ori să se oprească înaintea lui, pe tot conturul sau la o latură, scris în proprietățile ei sau tras în plan ori în secțiune cu **Editează straturile**, iar un tip de placă le poate da plăcilor lui prelungirile: vezi [Prelungirile straturilor](walls.md#prelungirile-straturilor). Acoperișurile nu au încă un set de straturi (învelitoarea, placa și șarpanta lor sunt părți proprii), deci nu au prelungiri de straturi.

### Planșee din grinzi și blocuri

Un tip de placă poate fi din **grinzi și blocuri** în loc de straturi simple: grinzi prefabricate (de pildă POT) puse una lângă alta la o distanță, blocuri ceramice de umplutură (MIAKO) între ele și o suprabetonare turnată peste. În **Tipuri de plăci**, alege la **Fel** **Grinzi și blocuri** și completează:

- **Presetare.** Câteva sisteme de la care să pornești (POT + MIAKO 19/62.5, 19/50, 23/62.5, 25/50 și o grindă autoportantă în I + bloc 25/70). Sunt orientative: verifică tabelele producătorului și schimbă orice dimensiune; după ce schimbi o dimensiune, presetarea arată **Dimensiuni proprii**.
- **Grindă**: **Lățimea** și **Înălțimea tălpii**, **Lățimea inimii**, **Înălțimea** grinzii, **Distanța** ax la ax și **Materialul**. O grindă autoportantă în I (grinzile prefabricate folosite fără cofraj, la un planșeu ventilat peste sol) are și o **Lățime a tălpii superioare** și o **Înălțime a tălpii superioare** la partea ei de sus: nu mai lată decât talpa de jos, mai lată decât inima; 0 (implicit) e T-ul întors.
- **Bloc de umplutură**: **Înălțimea**, **Lungimea** de-a lungul grinzilor și **Materialul**. Lățimea lui e golul dintre tălpile grinzilor.
- **Suprabetonare**: **Grosimea**, **Materialul** și **Plasa** din ea (un nume, măsurată după aria plăcii; goală, fără plasă).
- **Deschidere**: grinzile merg pe latura scurtă a plăcii, după x, după y sau sub un unghi; **Rezemarea** e cât intră ele în reazeme dincolo de marginea plăcii (125 mm implicit), deci desenează placa până la fețele reazemelor.

**Adâncimea** se calculează: blocul și suprabetonarea. E miezul tipului, setat singur când salvezi tipul; straturile de deasupra (o șapă, un finisaj) rămân cum sunt. Lângă câmpuri, motorul așază o placă de probă cu un gol, în plan și în secțiune, cum o va construi pe a ta, și îi numără grinzile și blocurile.

![Un tip de placă din grinzi și blocuri, cu planul și secțiunea lui](shots/beam-block-type.webp)

O placă de acest tip e construită din grinzile ei (fiecare de la o margine la alta și în reazeme), blocuri întregi de la începutul fiecărui rând și unul tăiat la capăt, și betonul turnat: suprabetonarea, nervurile peste tălpile grinzilor, fâșiile de pe margini și, în jurul unui gol, centurile care preiau grinzile oprite înaintea lui. Proprietățile ei au propria **Direcție de descărcare**: a tipului, sau perpendicular pe una dintre margini (marginea pe care reazemă grinzile). Planul desenează săgeata de descărcare și, într-un plan de detaliu, axul fiecărei grinzi; secțiunea taie grinzile, blocurile și betonul, fiecare hașurat după materialul lui.

**Planșee din grinzi și blocuri** din liste arată, pe placă, grinzile după lungimea comandată (rotunjită în sus la 250 mm), blocurile întregi și tăiate, betonul turnat și aria plasei; cantitățile plăcii le au și ele. IFC scrie placa ca IfcSlab cu părțile ei: grinzile (IfcBeam JOIST), blocurile și betonul (IfcBuildingElementPart), fiecare cu materialul lui; o parte își păstrează GlobalId-ul când placa e reconstruită. Cantitățile ei IFC (Qto_SlabBaseQuantities) sunt ale plăcii, în conturul ei: rezemările grinzilor în reazeme sunt lăsate afară din NetVolume și NetArea, deci nu le depășesc niciodată pe cele brute; **Lapin_BeamBlockQuantities** ale plăcii păstrează tot ce construiește, și rezemările. Pentru programele care sar peste o placă fără corp propriu, vezi **Plăci din părți** în [Import și export](import-export.md).

Catalogul tău vine dintr-o foaie de calcul: **Importă catalogul…** citește un CSV (un rând un sistem, dimensiuni în milimetri; **Șablon CSV** dă coloanele) și face câte un tip de placă din fiecare rând, sau îl schimbă pe cel cu același nume, într-un singur pas de anulare.

**Modifică → Împarte elementul** pe o placă sau pe un plafon cere două puncte ale unei linii de tăiere: linia continuă dincolo de ele și trebuie să taie conturul exact de două ori. Placa devine două plăci; partea pe care ai apăsat păstrează id-ul (GlobalId-ul din IFC), arcele de pe margini rămân arce (un arc tăiat rămâne arc), iar golul din placă merge la partea în care se află. Un gol pe care îl taie linia refuză. **Modifică → Taie/Prelungește elemente** primește o limită dreaptă (un perete, o grindă sau o linie desenată) și taie placa sau plafonul acolo, păstrând partea pe care ai apăsat.

![O placă cu o margine curbă împărțită de o linie: partea din dreapta selectată](shots/floor-split.webp)

Golurile proprii ale unui planșeu merg la partea în care sunt; un strat care trece de o latură curbă continuă așa pe ambele părți; grila unui tavan fals rămâne unde era, fiecare corp de iluminat sau difuzor în partea în care îi cade celula. Un planșeu cu o latură eliptică nu se taie după o linie.

Un **acoperiș** se împarte și se taie la fel. Fiecare parte păstrează regulile laturilor ei (streașină sau fronton, pantă, consolă), iar tăietura devine un fronton fără consolă; tăietura e permisă doar unde lasă acoperișul cum era, cu pantele fiecărei părți unde erau ale acoperișului, ca peste coama unui acoperiș în două ape sau de-a lungul ei. În rest e refuzată (*would change its slopes*): pantele unei părți ar urca spre coame noi. Ferestrele de acoperiș, lucarnele și burlanele merg la partea lor. Un acoperiș în boltă nu se taie.

![Un acoperiș în două ape împărțit peste coamă: două acoperișuri, aceleași pante](shots/roof-split.webp)

**Hașura plăcii în plan.** Un plan hașurează fața de sus a fiecărei plăci pe care o vede sub planul de secționare cu modelul de suprafață al materialului stratului de sus al plăcii: modelul pe care i-l dă **Gestionare → Materiale** pentru fațade (hașura, scara, unghiul și peniță lui), cel pe care fațadele îl desenează pe fețele pe care le văd. Hașura umple conturul plăcii așa cum îl arată planul, fără golurile ei, sub pereți, stâlpi și tot ce taie planul, pe layerul ei (FLOOR-HATCH), deci planșele, PDF-ul și DXF-ul o tipăresc. O placă pe care planul o taie (cu fața de sus peste planul de secționare) nu se hașurează așa, nici una ascunsă în vedere. Unde o încăpere are finisaj de pardoseală, finisajul stă pe placă și acolo se desenează hașura lui (vezi [Încăperi](rooms.md)). Un material fără model de suprafață (betonul, implicit) nu desenează nimic. **Hașurile suprafeței planșeelor** din stilul vederii (**Vedere → Setările vederii**, **Gestionare → Stiluri de vedere**) le pornesc sau le opresc: pornite în Architectural, oprite în Structural, MEP și Coordination. Un proiect făcut înainte își păstrează planurile cum erau, cu ele oprite în fiecare stil: bifează-le ca să hașurezi plăcile.

![Un plan cu o placă de parchet hașurată după materialul ei, sub pereți, fără golul scării](shots/floor-plan-hatch.webp)

### Pante și scurgeri

O placă poate cădea spre un sifon, poate avea pantă de la o margine spre o rigolă sau poate trece prin puncte și linii de cotă, rămânând o singură placă cu straturile tipului ei. Selectează o placă: secțiunea **Pante** din proprietăți pornește orizontală și oferă uneltele; ele sunt și în **Arhitectură → Pante**. Fiecare clic e un pas de anulare, iar **Fă-o din nou orizontală** le scoate pe toate.

- **Sifon de pardoseală**: clic pe placă și scrie cât de jos stă sifonul sub pardoseala din jur (20 mm implicit). Conturul rămâne la cota plăcii, iar placa coboară de la fiecare margine spre sifon. În proprietăți un sifon poate primi un **grătar** (un pătrat la adâncimea sifonului).
- **Rigolă de pardoseală**: două clicuri de-a lungul ei; placa coboară spre ea din ambele părți.
- **Pantă de la margine**: clic lângă o margine și scrie panta (`1.5 %`, `1:50`). Coboară dinspre margine; cu minus (`-1.5 %`) coboară spre margine, care devine linia rigolei. În proprietăți o pantă se poate opri la o abatere (**până pe partea opusă** implicit), după care placa rămâne orizontală. Mai multe margini care coboară dinspre ele se întâlnesc în dolii, mai multe care coboară spre ele în coame. O limită lucrează doar lângă marginea ei: pe partea de placă pe care o acoperă panta ei, până la dolia sau coama unde se întâlnește cu panta marginii vecine. Acolo placa se oprește la limită și rămâne orizontală, apoi coboară spre acea dolie (sau urcă spre acea coamă) cum vine spre ea cealaltă pantă, în oglindă; pantele celorlalte margini nu sunt scurtate de ea, iar doliile și coamele rămân unde erau. O terasă cu două rigole păstrează limita uneia lângă rigola ei, iar cealaltă urcă mai departe de la a ei. O margine cu limită fără altă pantă alături rămâne orizontală dincolo de limită peste tot.
- **Punct de cotă** și **Linie de cotă**: placa trece prin ele, la o abatere de la fața plăcii (`-20`) sau la o cotă (`=3.18`, care rămâne pe loc când se mută nivelul sau placa). O linie e o coamă sau o dolie dreaptă între capetele ei.
- **Săgeată de pantă a plăcii**: toată fața ca un singur plan, de la capătul de sus la cel de jos.
- **Zonă a plăcii**: un contur desenat în interiorul plăcii, cu abaterea lui (o cădiță de duș coborâtă) și cu pantele și sifoanele lui. Face treaptă față de rest pe conturul lui. O zonă trebuie să stea în interiorul plăcii; ca o parte care ajunge la marginea plăcii să aibă panta ei, împarte placa (**Modifică → Împarte elementul**): fiecare parte își păstrează pantele de pe partea ei, iar o linie de cotă peste tăietură e tăiată acolo, la cota ei.
- **Pantă pentru această încăpere**: clic într-o încăpere. Placa de sub ea primește o zonă pe conturul încăperii, cu un sifon la mijloc; zona urmează încăperea când se mută pereții. O încăpere care acoperă toată placa dă forma plăcii însăși.

![O baie care coboară spre un sifon: săgețile cu pantele și cota sifonului](shots/floor-slopes-bathroom-plan.webp)

![Aceeași baie la 1:100: sifonul și conturul zonei, fără săgeți și cote](shots/floor-slopes-bathroom-plan-100.webp)

**Realizată prin** spune cum urmează placa pantele:

- **sculptarea unui strat** (implicit când tipul are un strat peste miez): un strat preia pantele în grosimea lui, o șapă la pantă, iar straturile de deasupra îl urmează cu grosimea lor; miezul și ce e sub el rămân orizontale. **Stratul sculptat** e implicit șapa de peste miez (altfel stratul de imediat deasupra lui); se poate alege orice strat cu grosime. **Grosime minimă** (30 mm pentru o șapă, altfel 10 mm) nu e niciodată depășită în jos: unde pantele ar coborî mai mult, stratul rămâne la minimul lui, iar proprietățile spun pe ce arie (*Screed: held at its 30 mm minimum over 8.0 m²*).
- **înclinarea întregii plăci**: fiecare strat urmează panta cu grosimea lui, și fața de dedesubt, ca planul miezului unei plăci înclinate. Grosimile se măsoară pe verticală, deci volumele rămân arie × grosime; la câteva procente diferența față de grosimea perpendiculară pe pantă e sub 0,05 %. Un planșeu din grinzi și blocuri nu se înclină: sculptează-i șapa.

![O secțiune prin sifonul băii: șapa mai subțire spre sifon, placa orizontală sub ea](shots/floor-slopes-bathroom-section.webp)

Pantele sunt exacte: fiecare pliu unde se întâlnesc două plane e o muchie a corpului plăcii, așa că secțiunile taie stratul sculptat ca o pană, vederea 3D arată fața reală, iar cantitățile sunt ale solidelor. Un plan al nivelului plăcii desenează sifoanele și rigolele, liniile de cotă și contururile zonelor, iar la **1:50 și mai mare** o săgeată pe fiecare plan, cu panta lui, și cotele sifoanelor, ale punctelor și ale capetelor liniilor (și punctele cel mai sus și cel mai jos ale unei plăci înclinate); la 1:100 și mai mic săgețile și cotele lipsesc. **Marcajele pantelor plăcilor** din **Gestionare → Standarde de desen** schimbă scara (1:20, 1:50, 1:100, 1:200 sau orice scară). O săgeată care ar acoperi eticheta unei încăperi se mută lângă ea.

![O terasă care coboară 1,5 % de la casă spre linia rigolei](shots/floor-slopes-terrace.webp)

![O terasă cu două rigole: panta de la sud se oprește la 1 m și rămâne orizontală, cea de la est urcă mai departe de la rigola ei până la coamă](shots/floor-slopes-two-gutters.webp)

Cantitățile plăcii primesc, pe fiecare strat, **grosimea medie** (volumul pe aria în plan), **grosimea cea mai mică** și **cea mai mare**, iar pentru placă **aria suprafeței (pe pantă)**, **căderea** (cel mai sus minus cel mai jos) și, la un singur plan, **panta**; volumul brut e al alcătuirii cu formă. În IFC placa rămâne un IfcSlab cu IfcMaterialLayerSetUsage; corpurile straturilor sunt solidele cu formă, iar pantele merg în `Lapin_Intent`, deci Lapin le citește înapoi ca pante.

![Secțiunea Pante a unei plăci: realizată prin sculptarea șapei, minimul ei, un sifon și o pantă de la margine](shots/floor-slopes-properties.webp)

## Acoperișuri

Alege **Arhitectură → Acoperiș** (<kbd>RF</kbd>) și dă clic pe conturul pereților de dedesubt. Lapin construiește acoperișul peste acel contur, la partea de sus a nivelului.

![O casă cu acoperiș în două ape, în vederea 3D](shots/house-3d.webp)

Selectează acoperișul ca să-l schimbi în panoul de proprietăți:

- **Formă.** În patru ape, în două ape, într-o apă, mansardă, boltit sau terasă. Acoperișul boltit e un arc între două frontoane; **Boltă: săgeata la cheie** spune cât urcă la mijloc, cel mult jumătate din deschidere.
- **Panta și consola streașinii**, pentru tot acoperișul.
- **Direcția coamei (în două ape, într-o apă)**: auto, x sau y. La acoperișul într-o apă coama e latura de sus: pe x apa curge spre −y, pe y spre −x; **Într-o apă: inversează panta** o întoarce.
- **Fiecare latură** cu felul ei (streașină sau fronton), panta și consola ei. O mansardă cu laturi fronton dă un acoperiș frânt.
- **Stil.** Stilurile de acoperiș dau tot aspectul dintr-o alegere: învelitoarea, streașinile, jgheaburile și burlanele.
- **Materialul învelitorii.** Învelitoarea e făcută dintr-un material al proiectului, implicit cel al învelitorii alese (Clay tile, Metal sheet, Slate…), creat în culoarea învelitorii prima dată când un acoperiș are nevoie de el. Alegeți altul în proprietăți sau în **Învelitoare și ape** din configurator; schimbarea învelitorii îl schimbă și pe el, dacă nu ați ales unul. Fațadele și secțiunile hașurează învelitoarea după acest material (țigla, șindrila și ardezia în asize orizontale, tabla în fălțuiri verticale, dacă nu îi dați o hașură **Elevation** proprie), o secțiune desenează învelitoarea tăiată cu hașura de secțiune a materialului, iar IFC-ul, antemăsurătoarea și devizul trec învelitoarea sub materialul ei.
- **Grosimea învelitorii.** Goală, învelitoarea e groasă cât cea din catalog (membrană 2 cm, tablă 3 cm, țiglă 5 cm…); scrie una ca să aibă grosimea ei. Un acoperiș importat din alt program o păstrează pe cea din fișier.
- **Placa.** Un acoperiș plat, o boltă și un acoperiș cu **Structure** none au o placă sub învelitoare, o parte a lor: **Materialul plăcii** (implicit Reinforced concrete, creat prima dată când un acoperiș are nevoie de el) și **Grosimea plăcii** (implicit 16 cm), în proprietăți sau la **Șarpantă** în configurator. Învelitoarea stă pe ea, așa că acoperișul rămâne gros cât amândouă la un loc. O secțiune desenează două benzi, fiecare hașurată după materialul ei; ferestrele de acoperiș trec prin amândouă; un perete atașat de un acoperiș plat urcă până sub placă; IFC-ul scrie placa și învelitoarea ca două plăci de acoperiș din materialele lor, iar antemăsurătoarea și devizul măsoară placa (suprafață și volum) și învelitoarea (suprafață), fiecare sub materialul ei. Proprietățile arată aceste două câmpuri doar la un acoperiș fără șarpantă, iar câmpurile căpriorilor (secțiuni, interax, clasă, pane, capete) doar la unul cu șarpantă; un câmp care ține de o singură formă (al mansardei, săgeata bolții, întoarcerea pantei unice) apare doar la acea formă.

![Proprietățile unui acoperiș](shots/roof-properties.webp)

**Țiglă metalică și panouri.** Secțiunea **Panouri** a unui acoperiș îl învelește cu un profil de țiglă metalică sau de panou: Lapin așază panourile pe ape, spune ce se cumpără pe lungimi și le desenează ca învelitoare. Vezi [Panouri de acoperiș](roof-panels.md).

![Un acoperiș plat în secțiune: placa de beton și învelitoarea peste ea, fiecare hașurată după materialul ei](shots/roof-deck-section.webp)

**Configuratorul de acoperiș** (butonul de lângă stil, sau **Architecture → Roof configurator** cu un acoperiș selectat) adună totul într-un loc: stilurile în stânga, apoi forma și laturile, învelitoarea și apele, șarpanta și golurile, deasupra unui plan al acoperișului (clic pe o latură o face auto, streașină sau fronton) și, la șarpantă, o secțiune peste coamă cu ferma sau perechea de căpriori de la mijloc. Pornești de la un stil și schimbi orice câmp de mână; **Apply** face totul un singur pas de Undo. **Salvează ca stil…** păstrează acoperișul așa cum e configurat ca stil al proiectului, listat la **Project** deasupra celor din catalog (și în lista din panoul de proprietăți), pentru alte acoperișuri și pentru cele noi; **Actualizează stilul** salvează din nou schimbările în el, iar toate acoperișurile stilului le preiau. Un stil al proiectului se poate redenumi sau șterge; acoperișurile lui rămân cum sunt.

**Tipuri de acoperiș.** Un acoperiș poate fi de un tip (un IfcRoofType), ca un perete sau un planșeu: îl alegi la **Type** sus în proprietățile acoperișului, sau **No type**. **+** de lângă el face un tip nou din acoperiș: păstrează alcătuirea acoperișului (învelitoarea și materialul ei, placa, straturile tale, șarpanta cu secțiunile ei, jgheaburile și parazăpezile), dar nu forma, pantele, streșinile sau conturul, care rămân ale fiecărui acoperiș. Un acoperiș căruia îi dai tipul îi preia alcătuirea; după ce schimbi un acoperiș, **Save** de lângă tip pune din nou alcătuirea lui în tip și toate acoperișurile tipului o preiau. La **Type parameters** în proprietăți stau parametrii pe care îi au toate acoperișurile tipului (cei din Pset_RoofCommon, ca IsExternal sau FireRating, și seturile tale, de pildă un cod), așa că se dau o dată, nu pe fiecare acoperiș. IFC scrie tipul ca IfcRoofType cu seturile lui de proprietăți, iar Lapin îl citește înapoi. Stilurile de acoperiș ale proiectului rămân cum sunt, lângă tipuri.

![Configuratorul de acoperiș](shots/roof-configurator.webp)

**Jgheaburi și burlane.** În **Învelitoare și ape** din configurator, jgheabul are mărimea, distanța în afara streșinii, coborârea sub învelitoare, panta spre guri (mm/m) și capacele de capăt; tabelul laturilor pornește sau oprește jgheabul fiecărei streșini. Burlanele pornesc ca ale acoperișului, câte unul la 10 m de jgheab continuu, la capetele lui (o streașină curbă, împreună cu streșinile drepte în care continuă un colț rotunjit, e un singur jgheab; unde două jgheaburi se întâlnesc într-un colț, folosesc același burlan); **Fă-le editabile** le face elemente, iar **Architecture → Downpipe** adaugă unul cu un clic pe jgheab. Gripul unui burlan îl mută doar de-a lungul jgheabului; în proprietăți îi dai diametrul, cotul de la bază și traseul, un lanț de segmente, fiecare cu lungimea și unghiul lui față de verticală (spre perete), ultimul până la sol. În IFC jgheabul e IfcPipeSegment GUTTER, iar burlanul un segment rigid numit Downpipe.

**Colțuri tăiate la unghi.** Jgheaburile streașinilor care se întâlnesc într-un colț, la aceeași înălțime, sunt tăiate la unghiul colțului și îmbinate, fără capace acolo; un jgheab care merge înainte în linie dreaptă e o singură rampă.

Pereții de pe nivelul acoperișului se opresc singuri sub el și urcă la frontoane, și sub un acoperiș boltit. Un perete de pe alt nivel urmează acoperișul după **Arhitectură → Atașează la acoperiș** (<kbd>AR</kbd>): selectezi pereții, apoi dai clic pe acoperiș. Aceeași unealtă primește orice alt corp de deasupra pereților: un solid din Studio, o placă, o grindă, un element importat din IFC. Pereții urcă până la fața lui de jos și sunt tăiați după ea, oricare i-ar fi forma. **Detașează de acoperiș** anulează asta.

**Unde se întâlnesc două acoperișuri.** Un acoperiș aflat sub altul (un șopron sau o aripă cu un nivel mai jos) se oprește acolo unde ajunge la fața de jos a streașinii acoperișului de sus sau la fruntea ei; unde rămâne sub ele, sub o streașină adâncă, merge mai departe până la perete, cum se execută. Două acoperișuri la aceeași înălțime (o casă în L cu două coame) se întâlnesc acolo unde li se intersectează învelitorile: rămâne cel mai înalt, celălalt se retrage, așa că streașina acoperișului de sus se oprește la aripa peste care trece. Ca o coamă mai joasă să intre în acoperișul mai înalt, desenează conturul acoperișului de jos mai departe de perete, în acoperișul mai înalt: se oprește pe dolii. Se retrage toată grosimea acoperișului (învelitoare, placă, căpriori, cosoroabe, jgheaburi), iar ariile și lungimile din antemăsurătoare urmează. Un acoperiș importat din IFC nu este tăiat niciodată.

**Arhitectură → Gol** cu **Fereastră de acoperiș** sau **Lucarnă** adaugă un gol în acoperiș: dai clic pe o pantă. Ele țin de acoperiș. Selectezi acoperișul și fiecare își arată mânerele în plan. Tragi de mânerul din mijloc ca s-o muți pe acoperiș. Tragi de mânerele de pe marginile sau colțurile unei ferestre de acoperiș ca s-o redimensionezi pe pantă (marginea opusă rămâne pe loc; lungimea o poți scrie în timp ce tragi). Lățimea e de-a lungul streașinii, iar lungimea pe pantă, măsurată pe pantă. O lucarnă are câte un mâner pe fiecare latură pentru lățime. O schimbare care n-ar încăpea pe acoperiș nu se lasă. Ca s-o scoți, dai clic dreapta pe un mâner al ei și alegi **Scoate fereastra de acoperiș** (sau **Scoate lucarna**), apeși **Delete** cu cursorul deasupra mânerului sau dai **Ctrl/⌘-clic** pe mânerul din mijloc. Proprietățile acoperișului le listează și ele: acolo scrii lățimea și lungimea unei ferestre sau o scoți cu **×**. Acoperișul unei lucarne e acoperit cu materialul învelitorii și măsurat cu ea: cantitățile acoperișului dau separat **învelitoarea lucarnelor** (aria pantelor ei, numărul lucarnelor), deci antemăsurătoarea după material, devizul și o sarcină de planificare pe învelitoare numără pantele acoperișului (fără decupajele lucarnelor) și acoperișurile lucarnelor, fiecare o dată.

![Mânerul unei ferestre de acoperiș pe acoperișul selectat](shots/roof-window-grip.webp)

### Pante, curbe și straturi

**Marcajele pantelor.** Selectează un acoperiș în plan și fiecare latură arată un marcaj puțin în interior: o săgeată cu panta (de exemplu 30°) la o streașină, o bară cu **G** la un fronton. Un clic pe marcaj face latura fronton sau streașină; încă un clic o duce înapoi la alegerea automată a acoperișului. Un marcaj desenat în culoarea de accent e o latură stabilită de mână. Un acoperiș plat sau boltit nu are marcaje.

**Laturi curbe și colțuri rotunjite.** Conturul unui acoperiș primește arce și colțuri rotunjite ca la o placă (vezi *Laturi curbe, racordări și plăci rotunde* mai sus): trage de grip-urile laturilor și colțurilor sau scrie un **Arc** în tabelul laturilor din proprietăți (săgeata față de coardă, în m; gol sau 0 înseamnă dreaptă). Clic dreapta pe o latură, sau pe rombul ori pe grip-ul rotund al unei laturi, oferă **Fă arc** sau **Fă dreaptă**. Regulile laturilor urmează laturile, și la fel ce poartă acoperișul: cosoroaba de pe o streașină curbă și fiecare rând de pane de deasupra ei sunt câte o singură piesă care urmează curba, căpriorii stau la distanța lor de-a lungul curbei și în jurul unui colț rotunjit, între pantele aceleiași curbe nu apar coame înclinate (nici linii de coamă în plan), jgheabul trece în jurul curbei dintr-o bucată, iar indicatorul pantei stă pe arc.

**Straturi.** În configurator, fila **Layere** construiește învelitoarea acoperișului ca un teanc al tău, de la interior până deasupra căpriorilor sau planului de bază: fiecare strat are un **Rol** (structură, izolație, astereală, membrană, finisaj), un **Material** și o **Grosime**; le muți în sus sau în jos, scoți unul sau **Șterge layerele proprii** ca să revii la învelitoarea și structura stilului. Secțiunea alăturată le arată. La **Extinderi de layer deblocate**, un strat se poate opri înainte de streașină sau o poate depăși: alegi stratul, o latură (sau **Pe tot conturul**) și distanța. Proprietățile arată grosimea totală a teancului, cu un link **Editează…**.

**Șabloane pentru alte proiecte.** **Salvează ca șablon…** din configurator păstrează acoperișul așa cum e stabilit, în biblioteca acestui calculator, separat de stilurile proiectului: forma, învelitoarea, apele și șarpanta cu secțiunile tuturor pieselor, straturile proprii și prelungirile lor, și laturile (regula și arcul fiecăreia). Pe un acoperiș cu tot atâtea colțuri cât cel din care a fost salvat, șablonul pune și laturile; pe oricare altul pune restul, iar prelungirea unui strat dată pe o latură rămâne deoparte. Un șablon salvat de o versiune mai veche pune ce punea atunci. Șabloanele sunt listate ca **Șabloane proprii** în configurator, deasupra stilurilor, și în lista de stiluri a acoperișului; alegi unul ca să-l pui pe acoperiș, iar butoanele de lângă el îl redenumesc, îl exportă într-un fișier JSON, îl șterg sau importă șabloane dintr-un fișier.

**Explodează.** **Explodează**, în proprietățile acoperișului și în meniul lui de clic dreapta, face din acoperiș solide editabile din Studio, câte unul pe parte (învelitoare, căpriori, cosoroabe etc.), cu numele și materialele lor, într-un singur pas de Undo. Acoperișul dispare, odată cu burlanele pe care le purta.

**Învelitoarea desfășurată.** **Învelitoare desfășurată** din proprietățile acoperișului face o vedere de desen a învelitorii întinse la 1:50: fiecare apă în mărime reală (acoperișul boltit desfăcut), cu lungimile laturilor, golurile decupate și aria fiecărei ape și a întregii învelitori. Se deschide pe loc și rămâne în browserul proiectului.

### Șarpanta din lemn

**Structură** spune cum e făcută șarpanta:

- **rafters**: căpriori pe pane, cu grindă de coamă.
- **collar**: perechi de căpriori cu un clește orizontal la **Clești: partea din înălțimea acoperișului** (implicit două treimi), fără pane.
- **trusses**: ferme prefabricate de-a lungul coamei principale, la **Distanța între căpriori**: tălpile superioare pe pante, talpa inferioară pe cosoroabe, diagonale în W (fermă într-o apă la acoperișul într-o apă). Capetele unui acoperiș în patru ape își păstrează căpriorii.
- **none**: o placă, fără lemn.

**Căpriori și tălpi** primește secțiunile vechi în cm sau pe cele nordice în mm (45×95 până la 45×245); alături, **Diagonalele fermelor** și **Clasa lemnului** (C14 până la C30, lemn lamelar). Căpriorii stau pe cosoroabă printr-o crestătură (**Așezarea căpriorilor pe cosoroabă**) și se termină la streașină vertical sau în echer (**Capătul căpriorilor la streașină**). Stilurile **Swedish truss roof** și **Nordic collar-tie roof** își aduc șarpanta cu ele.

**Secțiunile celorlalte piese.** Fila **Șarpantă** a configuratorului stabilește și **Secțiunea panei**, **Secțiunea grinzii de coamă** și **Secțiunea cosoroabei** (și **Secțiunea cleștelui** la acoperișurile cu clești), fiecare din listele de secțiuni. Lângă fiecare secțiune, o bifă pune piesa pe lat, rotită cu 90° (**Cosoroaba pusă pe lat**, **Grinda de coamă pusă pe lat**, **Panele puse pe lat**, **Cleștii puși pe lat**): o cosoroabă de 45×145 mm are atunci 145 mm lățime și 45 mm înălțime. Modelul 3D, lista de debitare, modelul analitic și IFC o iau așa cum e pusă; o cosoroabă pusă pe lat e mai joasă, deci căpriorii stau pe ea mai jos cu diferența (100 mm la acea cosoroabă), iar coama coboară odată cu ei. Bifele sunt debifate implicit, iar stilurile proiectului și șabloanele le păstrează. **Capătul căpriorilor la streașină** are șase tăieturi: **verticale**, **drepte** (tăiat perpendicular pe căprior), **orizontale** (o mică treaptă tăiată orizontal), **teșite** (o teșitură înclinată sub capăt), **în trepte** (două trepte mici) și **curb** (o rotunjire). Unealta **Șarpanta acoperișului** din fila **Lemn** (selectează întâi un acoperiș) și **Configurează șarpanta acoperișului…** din proprietățile **Lemn** ale acoperișului deschid configuratorul pe această filă.

Fiecare piesă e o parte a acoperișului: extrasul **Roof timber** le dă ca listă de debitare (piesa, secțiunea, clasa, lungimea, bucățile, volumul), iar în IFC fiecare e un IfcMember cu tipul lui, fiecare fermă un IfcElementAssembly.

## Rezistența termică și valoarea U

Un planșeu sau un acoperiș, și tipul lui, arată **R** și **U** în **Termic**, în proprietăți, calculate din straturi ca la un perete (EN ISO 6946). Cazul fluxului de căldură este **Planșeu peste aer exterior** pentru un planșeu și **Acoperiș** pentru un acoperiș; alege **Planșeu pe pământ**, **Planșeu peste spațiu neîncălzit** sau **Plafon sub spațiu neîncălzit** unde e cazul. Vezi [Rezistența termică și valoarea U](walls.md#rezistența-termică-și-valoarea-u) pentru λ, rezistențele superficiale, câmpuri și exportul IFC.

## Tavane false

**Interior → Tavan** (<kbd>CG</kbd>) desenează un tavan fals pe un contur pe care îl dai prin clicuri; **Tavan într-o cameră** umple camera pe care dai clic, după conturul ei. Un tavan atârnă chiar sub placa de deasupra (cu **Adâncimea plenumului** tipului sub ea), sau la **Înălțimea** lui peste etaj (2700 mm) unde nu e placă deasupra, sau peste pardoseala finită când așa spune tipul lui; îl schimbi în proprietăți ([Tavane false](ceilings.md)).

Tipul lui, din **Interior → Tipuri de tavan**, alege **Sistemul**: **Plăci pe structură aparentă** (lățimea, lungimea și grosimea plăcii, lățimea vizibilă, adâncimea și rotirea structurii, tijele de suspendare până la structură și distanța dintre ele) sau **Gips-carton, continuu**. Structura e centrată pe tavan, așa că plăcile tăiate de la margini ies egale.

**Corp de iluminat** și **Anemostat** pun unul în caseta pe care dai clic; un clic pe aceeași casetă cu cealaltă unealtă îl schimbă. În panoul de proprietăți, **Așază** pune corpuri de iluminat sau anemostate la fiecare câteva casete, iar **Golește** le scoate. Extrasele numără plăcile întregi, corpurile de iluminat și anemostatele, iar tavanul iese în IFC ca IfcCovering de tip CEILING.

![Proprietățile unui tavan: tipul, înălțimea și echipamentele](shots/ceiling-properties.webp)

![Tavane casetate cu corpuri de iluminat și anemostate, în 3D](shots/ceilings-3d.webp)

## Plinte

**Interior → Plintă în cameră** (<kbd>SK</kbd>) pune o plintă de jur împrejurul camerei pe care dai clic, pe fețele pereților, întreruptă la fiecare ușă (orice gol care coboară până la pardoseală); cu un clic între pereți fără cameră, îi urmează la fel. Plinta unei camere urmează camera: muți un perete sau o ușă și plinta merge după ei. **Plintă** desenează una de-a lungul unei linii pe care o dai prin clicuri pe fața unui perete (Enter termină); plinta se pune pe partea dinspre cameră, iar **Schimbă partea** din proprietăți o întoarce.

Tipul ei, din **Interior → Tipuri de plintă**, alege **Profilul** — **Dreaptă (pătrată)**, **Teșită (MDF)**, **Profilată (lemn)**, **Cu racord curb (metal)**, **Rotunjită sus (creion)** sau **Cu muchie rotunjită**, desenat alături la scară —, **Înălțimea** (40–300 mm), **Grosimea** (5–40 mm), **Materialul** și culoarea lui. **Din bibliotecă** aduce în proiect tipuri gata făcute: MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 și Bullnose 70. Cât timp o unealtă de plintă e pornită, panoul de proprietăți arată tipul pe care îl pune, de ales înainte de clic.

În proprietăți, o plintă are tipul, un **Decalaj la bază** peste pardoseală și **Întreruptă la uși** (debifat, trece peste ele). **Extras de plinte** (sau **Plinte** în lista de extrase noi din browserul proiectului) le listează pe tipuri, cu bucățile și lungimea, fără golurile ușilor. Plinta e îmbinată la 45° la colțuri; planul îi desenează muchia din față, iar în IFC o plintă iese ca IfcCovering de tip SKIRTINGBOARD, al unui IfcCoveringType de același tip.

Încă două alegeri așază plinta: **Începe de la** este **Nivelul etajului** (ca înainte) sau **Pardoseala finită a încăperii**, nivelul încăperii în care stă (**Decalajul la bază** al ei, sub care se află finisajul pardoselii), plus decalajul plintei; **Stă pe** este **Fața peretelui** (ca înainte) sau **Finisajul peretelui**, fața finisajului pereților încăperii (tencuiala, faianța), ca plinta să nu fie îngropată în șapă și nici în spatele tencuielii. O plintă desenată de-a lungul unui perete le ia de la încăperea de pe partea pe care stă, așa că fiecare parte a unui perete despărțitor între două încăperi cu pardoseli diferite o are pe a ei.

![Tipurile de plintă: profilul desenat la scară, biblioteca](shots/skirting-types.webp)

![Proprietățile unei plinte: tipul, decalajul la bază și golurile de la uși](shots/skirting-properties.webp)
