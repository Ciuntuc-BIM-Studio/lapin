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

Ca să schimbi conturul, selectează placa și trage de colțuri. **Gol în placă** (<kbd>SH</kbd>) taie o gaură în placă, pentru o scară sau un ghen.

O placă desenată în afara pereților, în fața unei uși, e o terasă. Pentru treptele până la ea, vezi [Scări](stairs.md).

Un strat al plăcii poate și să treacă de contur, ori să se oprească înaintea lui, pe tot conturul sau la o latură: vezi [Prelungirile straturilor](walls.md#prelungirile-straturilor).

### Planșee din grinzi și blocuri

Un tip de placă poate fi din **grinzi și blocuri** în loc de straturi simple: grinzi prefabricate (de pildă POT) puse una lângă alta la o distanță, blocuri ceramice de umplutură (MIAKO) între ele și o suprabetonare turnată peste. În **Tipuri de plăci**, alege la **Fel** **Grinzi și blocuri** și completează:

- **Presetare.** Câteva sisteme de la care să pornești (POT + MIAKO 19/62.5, 19/50, 23/62.5, 25/50). Sunt orientative: verifică tabelele producătorului și schimbă orice dimensiune; după ce schimbi o dimensiune, presetarea arată **Dimensiuni proprii**.
- **Grindă**: **Lățimea** și **Înălțimea tălpii**, **Lățimea inimii**, **Înălțimea** grinzii, **Distanța** ax la ax și **Materialul**.
- **Bloc de umplutură**: **Înălțimea**, **Lungimea** de-a lungul grinzilor și **Materialul**. Lățimea lui e golul dintre tălpile grinzilor.
- **Suprabetonare**: **Grosimea**, **Materialul** și **Plasa** din ea (un nume, măsurată după aria plăcii; goală, fără plasă).
- **Deschidere**: grinzile merg pe latura scurtă a plăcii, după x, după y sau sub un unghi; **Rezemarea** e cât intră ele în reazeme dincolo de marginea plăcii (125 mm implicit), deci desenează placa până la fețele reazemelor.

**Adâncimea** se calculează: blocul și suprabetonarea. E miezul tipului, setat singur când salvezi tipul; straturile de deasupra (o șapă, un finisaj) rămân cum sunt. Lângă câmpuri, motorul așază o placă de probă cu un gol, în plan și în secțiune, cum o va construi pe a ta, și îi numără grinzile și blocurile.

![Un tip de placă din grinzi și blocuri, cu planul și secțiunea lui](shots/beam-block-type.webp)

O placă de acest tip e construită din grinzile ei (fiecare de la o margine la alta și în reazeme), blocuri întregi de la începutul fiecărui rând și unul tăiat la capăt, și betonul turnat: suprabetonarea, nervurile peste tălpile grinzilor, fâșiile de pe margini și, în jurul unui gol, centurile care preiau grinzile oprite înaintea lui. Proprietățile ei au propria **Direcție de descărcare**: a tipului, sau perpendicular pe una dintre margini (marginea pe care reazemă grinzile). Planul desenează săgeata de descărcare și, într-un plan de detaliu, axul fiecărei grinzi; secțiunea taie grinzile, blocurile și betonul, fiecare hașurat după materialul lui.

**Planșee din grinzi și blocuri** din liste arată, pe placă, grinzile după lungimea comandată (rotunjită în sus la 250 mm), blocurile întregi și tăiate, betonul turnat și aria plasei; cantitățile plăcii le au și ele. IFC scrie placa ca IfcSlab cu părțile ei: grinzile (IfcBeam JOIST), blocurile și betonul (IfcBuildingElementPart), fiecare cu materialul lui; o parte își păstrează GlobalId-ul când placa e reconstruită.

Catalogul tău vine dintr-o foaie de calcul: **Importă catalogul…** citește un CSV (un rând un sistem, dimensiuni în milimetri; **Șablon CSV** dă coloanele) și face câte un tip de placă din fiecare rând, sau îl schimbă pe cel cu același nume, într-un singur pas de anulare.

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

![Un acoperiș plat în secțiune: placa de beton și învelitoarea peste ea, fiecare hașurată după materialul ei](shots/roof-deck-section.webp)

**Configuratorul de acoperiș** (butonul de lângă stil, sau **Architecture → Roof configurator** cu un acoperiș selectat) adună totul într-un loc: stilurile în stânga, apoi forma și laturile, învelitoarea și apele, șarpanta și golurile, deasupra unui plan al acoperișului (clic pe o latură o face auto, streașină sau fronton) și, la șarpantă, o secțiune peste coamă cu ferma sau perechea de căpriori de la mijloc. Pornești de la un stil și schimbi orice câmp de mână; **Apply** face totul un singur pas de Undo. **Salvează ca stil…** păstrează acoperișul așa cum e configurat ca stil al proiectului, listat la **Project** deasupra celor din catalog (și în lista din panoul de proprietăți), pentru alte acoperișuri și pentru cele noi; **Actualizează stilul** salvează din nou schimbările în el, iar toate acoperișurile stilului le preiau. Un stil al proiectului se poate redenumi sau șterge; acoperișurile lui rămân cum sunt.

![Configuratorul de acoperiș](shots/roof-configurator.webp)

**Jgheaburi și burlane.** În **Învelitoare și ape** din configurator, jgheabul are mărimea, distanța în afara streșinii, coborârea sub învelitoare, panta spre guri (mm/m) și capacele de capăt; tabelul laturilor pornește sau oprește jgheabul fiecărei streșini. Burlanele pornesc ca ale acoperișului, câte unul la 10 m; **Fă-le editabile** le face elemente, iar **Architecture → Downpipe** adaugă unul cu un clic pe jgheab. Gripul unui burlan îl mută doar de-a lungul jgheabului; în proprietăți îi dai diametrul, cotul de la bază și traseul, un lanț de segmente, fiecare cu lungimea și unghiul lui față de verticală (spre perete), ultimul până la sol. În IFC jgheabul e IfcPipeSegment GUTTER, iar burlanul un segment rigid numit Downpipe.

Pereții de pe nivelul acoperișului se opresc singuri sub el și urcă la frontoane, și sub un acoperiș boltit. Un perete de pe alt nivel urmează acoperișul după **Arhitectură → Atașează la acoperiș** (<kbd>AR</kbd>): selectezi pereții, apoi dai clic pe acoperiș. Aceeași unealtă primește orice alt corp de deasupra pereților: un solid din Studio, o placă, o grindă, un element importat din IFC. Pereții urcă până la fața lui de jos și sunt tăiați după ea, oricare i-ar fi forma. **Detașează de acoperiș** anulează asta.

**Fereastră de acoperiș** și **Lucarnă** adaugă goluri în acoperiș: dai clic pe o pantă. Ele țin de acoperiș. Selectezi acoperișul și fiecare își arată mânerele în plan. Tragi de mânerul din mijloc ca s-o muți pe acoperiș. Tragi de mânerele de pe marginile sau colțurile unei ferestre de acoperiș ca s-o redimensionezi pe pantă (marginea opusă rămâne pe loc; lungimea o poți scrie în timp ce tragi). Lățimea e de-a lungul streașinii, iar lungimea pe pantă, măsurată pe pantă. O lucarnă are câte un mâner pe fiecare latură pentru lățime. O schimbare care n-ar încăpea pe acoperiș nu se lasă. Ca s-o scoți, dai clic dreapta pe un mâner al ei și alegi **Scoate fereastra de acoperiș** (sau **Scoate lucarna**), apeși **Delete** cu cursorul deasupra mânerului sau dai **Ctrl/⌘-clic** pe mânerul din mijloc. Proprietățile acoperișului le listează și ele: acolo scrii lățimea și lungimea unei ferestre sau o scoți cu **×**. Acoperișul unei lucarne e acoperit cu materialul învelitorii și măsurat cu ea: cantitățile acoperișului dau separat **învelitoarea lucarnelor** (aria pantelor ei, numărul lucarnelor), deci antemăsurătoarea după material, devizul și o sarcină de planificare pe învelitoare numără pantele acoperișului (fără decupajele lucarnelor) și acoperișurile lucarnelor, fiecare o dată.

![Mânerul unei ferestre de acoperiș pe acoperișul selectat](shots/roof-window-grip.webp)

### Șarpanta din lemn

**Structură** spune cum e făcută șarpanta:

- **rafters**: căpriori pe pane, cu grindă de coamă.
- **collar**: perechi de căpriori cu un clește orizontal la **Clești: partea din înălțimea acoperișului** (implicit două treimi), fără pane.
- **trusses**: ferme prefabricate de-a lungul coamei principale, la **Distanța între căpriori**: tălpile superioare pe pante, talpa inferioară pe cosoroabe, diagonale în W (fermă într-o apă la acoperișul într-o apă). Capetele unui acoperiș în patru ape își păstrează căpriorii.
- **none**: o placă, fără lemn.

**Căpriori și tălpi** primește secțiunile vechi în cm sau pe cele nordice în mm (45×95 până la 45×245); alături, **Diagonalele fermelor** și **Clasa lemnului** (C14 până la C30, lemn lamelar). Căpriorii stau pe cosoroabă printr-o crestătură (**Așezarea căpriorilor pe cosoroabă**) și se termină la streașină vertical sau în echer (**Capătul căpriorilor la streașină**). Stilurile **Swedish truss roof** și **Nordic collar-tie roof** își aduc șarpanta cu ele.

Fiecare piesă e o parte a acoperișului: extrasul **Roof timber** le dă ca listă de debitare (piesa, secțiunea, clasa, lungimea, bucățile, volumul), iar în IFC fiecare e un IfcMember cu tipul lui, fiecare fermă un IfcElementAssembly.

## Tavane false

**Interior → Tavan** (<kbd>CG</kbd>) desenează un tavan fals pe un contur pe care îl dai prin clicuri; **Tavan într-o cameră** umple camera pe care dai clic, după conturul ei. Un tavan stă la **Înălțimea** lui peste etaj (2700 mm, dacă nu o schimbi în proprietăți).

Tipul lui, din **Interior → Tipuri de tavan**, alege **Sistemul**: **Plăci pe structură aparentă** (lățimea, lungimea și grosimea plăcii, lățimea vizibilă, adâncimea și rotirea structurii, tijele de suspendare până la structură și distanța dintre ele) sau **Gips-carton, continuu**. Structura e centrată pe tavan, așa că plăcile tăiate de la margini ies egale.

**Corp de iluminat** și **Anemostat** pun unul în caseta pe care dai clic; un clic pe aceeași casetă cu cealaltă unealtă îl schimbă. În panoul de proprietăți, **Așază** pune corpuri de iluminat sau anemostate la fiecare câteva casete, iar **Golește** le scoate. Extrasele numără plăcile întregi, corpurile de iluminat și anemostatele, iar tavanul iese în IFC ca IfcCovering de tip CEILING.

![Proprietățile unui tavan: tipul, înălțimea și echipamentele](shots/ceiling-properties.webp)

![Tavane casetate cu corpuri de iluminat și anemostate, în 3D](shots/ceilings-3d.webp)

## Plinte

**Interior → Plintă în cameră** (<kbd>SK</kbd>) pune o plintă de jur împrejurul camerei pe care dai clic, pe fețele pereților, întreruptă la fiecare ușă (orice gol care coboară până la pardoseală); cu un clic între pereți fără cameră, îi urmează la fel. Plinta unei camere urmează camera: muți un perete sau o ușă și plinta merge după ei. **Plintă** desenează una de-a lungul unei linii pe care o dai prin clicuri pe fața unui perete (Enter termină); plinta se pune pe partea dinspre cameră, iar **Schimbă partea** din proprietăți o întoarce.

Tipul ei, din **Interior → Tipuri de plintă**, alege **Profilul** — **Dreaptă (pătrată)**, **Teșită (MDF)**, **Profilată (lemn)**, **Cu racord curb (metal)**, **Rotunjită sus (creion)** sau **Cu muchie rotunjită**, desenat alături la scară —, **Înălțimea** (40–300 mm), **Grosimea** (5–40 mm), **Materialul** și culoarea lui. **Din bibliotecă** aduce în proiect tipuri gata făcute: MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 și Bullnose 70. Cât timp o unealtă de plintă e pornită, panoul de proprietăți arată tipul pe care îl pune, de ales înainte de clic.

În proprietăți, o plintă are tipul, un **Decalaj la bază** peste pardoseală și **Întreruptă la uși** (debifat, trece peste ele). **Extras de plinte** (sau **Plinte** în lista de extrase noi din browserul proiectului) le listează pe tipuri, cu bucățile și lungimea, fără golurile ușilor. Plinta e îmbinată la 45° la colțuri; planul îi desenează muchia din față, iar în IFC o plintă iese ca IfcCovering de tip SKIRTINGBOARD, al unui IfcCoveringType de același tip.

![Tipurile de plintă: profilul desenat la scară, biblioteca](shots/skirting-types.webp)

![Proprietățile unei plinte: tipul, decalajul la bază și golurile de la uși](shots/skirting-properties.webp)
