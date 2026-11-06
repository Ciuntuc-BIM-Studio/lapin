---
title: Scări și rampe
order: 42
summary: Pui o scară sau o rampă între etaje sau până la orice înălțime, o configurezi în tipul ei și faci cele câteva trepte până la o terasă sau o ușă.
---

## Scări

Alege **Arhitectură → Scară** (<kbd>ST</kbd>). Dă clic la piciorul scării (mijlocul primei contratrepte), apoi într-un punct spre care urcă. La o scară în L sau în U, al treilea clic, la stânga sau la dreapta rampei, alege încotro întoarce. Scara urcă de la etajul ei la etajul de deasupra. Lapin calculează numărul de contratrepte, înălțimea lor și lățimea treptei din înălțime și din regulile tipului.

Selectează scara ca s-o modifici în panoul de proprietăți:

- **Tip de scară.** Forma, regulile, alcătuirea și balustrada vin din tip, configurat în **Arhitectură → Tipuri de scări**.
- **Lățime**, **Întoarcere**, **Decalaj la bază** și **Decalaj la partea de sus.**
- **Diferența de nivel.** Lasă câmpul gol și scara urcă până la etajul de deasupra. Cu o valoare, scara are o diferență de nivel proprie, oricare ar fi etajele. Folosește-o pentru câteva trepte între două cote de pe același etaj.
- **Fixat.** Contratreptele, înălțimea contratreptei, lățimea treptei și lungimea podestului, când vrei să le fixezi. Lasă-le goale și le hotărăsc regulile.
- **Verificări.** Fiecare valoare comparată cu regulile tipului, cu verde sau cu roșu.

Scara cere un gol în placa de deasupra ei. Debifează **Gol în placa de deasupra** dacă placa trebuie să rămână întreagă.

## Formele scărilor și tipurile gata făcute

În **Arhitectură → Tipuri de scări**, fila **Formă** oferă: **Dreaptă**, **Dreaptă cu podest**, **În L** și **În U** (fiecare cu podest sau cu **trepte balansate**; o scară în U fără gol între rampe e scara „dog-leg”), **În spirală** (în jurul unui stâlp, cu **Raza stâlpului**), **Curbă** (cu **Raza interioară**) și **Bifurcată**: o rampă principală pe mijloc până la un podest, apoi două rampe care se întorc, câte una de fiecare parte. La o scară în spirală sau curbă, **Se rotește cu (°)** spune cât se întoarce; gol, hotărăsc contratreptele și treapta, iar dat, treapta rezultă din el. Fiecare scară îl poate schimba și în proprietățile ei.

Biblioteca adaugă în proiect tipuri gata făcute: scară în U fără gol, scară cu trepte balansate, scară în spirală, scară curbă, scară cu trepte în consolă și scară bifurcată.

În fila **Construcție**, **Alcătuire** alege placa sub trepte, treptele pline pe teren, **Trepte libere (în consolă, „plutitoare”)** (fiecare treaptă o placă prinsă în perete) sau **Trepte pe vange**.

**Balustrada** își are **Modelul**: **Stâlpi și mână curentă**, **Baluștri** (la cel mult **Distanța între baluștri**), **Panouri de sticlă**, **Cabluri** sau **Parapet plin**; **Mâna curentă** pătrată sau rotundă, înălțimea, **Retras de la margine**, cât **Continuă dincolo de capete** și culoarea. În panoul de proprietăți, **Balustradă** o pune pe alte părți decât tipul, pentru o singură scară.

![Tipul scării în spirală, cu treptele numerotate](shots/stair-spiral.webp)

### Treptele numerotate

La **În plan**, în fila **Formă**: **Numerotează treptele** scrie numărul fiecărei trepte într-un cerc, de la prima contratreaptă în sus; **Etichetă** scrie contratreptele, înălțimea și lățimea treptei și lățimea scării lângă picior; **„UP” la piciorul săgeții** arată de unde pornește. Planurile și planșele le arată.

![Trepte numerotate în plan, o scară bifurcată și una cu trepte în consolă](shots/stair-numbers-plan.webp)

![O scară în spirală, una bifurcată și una cu trepte în consolă, în 3D](shots/stair-shapes-3d.webp)

## Trepte de intrare (o terasă)

Treptele până la ușa de la intrare au o diferență de nivel proprie: urcă de la teren, aflat cu câteva trepte sub parter, până la ±0.00. Lapin are pentru ele un tip de scară gata făcut, **Trepte de intrare**: lat de 2 m, trei contratrepte de 150 mm, din beton plin turnat pe teren, fără balustradă.

![Trepte de intrare în fața unei terase, în plan](shots/entrance-steps-plan.webp)

1. **Terasa.** Desenează podestul ca placă: alege **Arhitectură → Placă** (<kbd>SB</kbd>) și dă clic pe conturul ei în afara peretelui, în fața ușii. Fața de sus a plăcii e la cota etajului ei (±0.00 la parter), iar tipul ei îi dă grosimea. Ca să schimbi grosimea, fă un tip de placă pentru terase în **Arhitectură → Tipuri de plăci**, de exemplu 150 mm de beton.
2. **Treptele.** Alege **Arhitectură → Trepte de intrare**. Dă clic pe mijlocul marginii terasei, apoi în partea spre care ies treptele. Treptele urcă până la marginea terasei și se termină la ±0.00. Pornesc cu 450 mm mai jos: **Decalajul la bază** e −450 mm.
3. **Înălțimea.** Selectează treptele. La **Diferența de nivel** scrie diferența reală dintre teren și terasă, de exemplu 300. Capătul de sus rămâne la terasă și piciorul se ridică: decalajul la bază devine −300 mm, iar treptele au două contratrepte. Cu **Lățime** le faci cât terasa de late, de exemplu 2400.

![Proprietățile treptelor de intrare: diferența de nivel, decalajul la bază, cotele](shots/entrance-steps-properties.webp)

**Niveluri** arată unde pornesc și unde se termină treptele, de exemplu −0.45 → ±0.00. Cu o singură contratreaptă (o diferență de nivel de 150 mm), treapta este chiar marginea terasei și nu mai rămâne nicio treaptă de construit. Dă-le două contratrepte sau mai multe ca să ai trepte în fața terasei.

Tipul intră în proiect prima oară când îl folosești. Ca să-l schimbi, deschide **Arhitectură → Tipuri de scări** și alege **Trepte de intrare**:

- **Formă → Urcă → O înălțime proprie.** Diferența de nivel a tipului, urcată **Până la cota etajului, de dedesubt (o intrare)**; fiecare scară o poate schimba în continuare la **Diferența de nivel**.
- **Reguli → Setul de reguli → trepte de intrare.** Contratrepte de 120–170 mm și trepte de 300–400 mm, cu voie pentru o singură contratreaptă.
- **Construcție → Alcătuire.** **Plin, turnat pe teren** umple treptele până la bază. **Placă înclinată sub trepte** le face ca pe o scară interioară.

![Tipul Trepte de intrare în configuratorul de scări](shots/stair-types.webp)

Ca orice alt tip de scară să urce o înălțime proprie, alege **Urcă → Până la → O înălțime proprie** în fila lui **Formă** (vezi secțiunea următoare). Ca să transformi o scară deja pusă în trepte de intrare, alege-i **Trepte de intrare** la **Tip de scară**: coboară astfel încât capătul ei de sus să fie la cota etajului.

![Casa cu terasa și treptele de intrare, în 3D](shots/entrance-steps-3d.webp)

În IFC, treptele sunt un IfcStair de tipul STRAIGHT_RUN_STAIR. Rampa lui e un IfcStairFlight cu NumberOfRisers, NumberOfTreads, RiserHeight și TreadLength, iar Pset_StairCommon are aceleași valori. Terasa e un IfcSlab.

## O scară până la orice înălțime

O scară nu trebuie să meargă neapărat de la un etaj la următorul. În fila **Formă** a tipului, **Urcă → Până la** alege **Etajul de deasupra** sau **O înălțime proprie**, cu **Diferența de nivel**, iar **Pornește** spune de unde pornește:

- **Până la cota etajului, de dedesubt (o intrare).** Se termină la cota etajului și pornește mai jos cu diferența de nivel, ca treptele de intrare.
- **De la cota etajului în sus (o terasă, o platformă).** Pornește de la cota etajului și urcă diferența de nivel: de exemplu 1,20 m până la o terasă sau o rampă de descărcare.

Fiecare scară pusă poate urca altă înălțime: scrie-o la **Diferența de nivel** în proprietățile ei (gol: a tipului). Când urcă de la cotă, o diferență nouă păstrează piciorul pe loc; când vine de dedesubt, păstrează capătul de sus.

## Spațiul de sub scară

Spațiul de sub o rampă se poate mobila: un dulap de depozitare, un birou în dulap, un colț de lectură, un hol de intrare cu bancă și cuiere, o căsuță de joacă pentru cei mici sau un culcuș pentru câine. Selectează o scară cu rampe drepte și dă clic pe **Spațiul de sub scară…** jos în proprietățile ei (sau **Arhitectură → Spațiu sub scară**).

![Configuratorul spațiului de sub scară: colțul de lectură sub rampă](shots/under-stair-dialog.webp)

Configuratorul arată stilurile gata făcute în stânga: **Dulap de depozitare**, **Birou în dulap**, **Colț de lectură**, **Hol de intrare**, **Căsuță de joacă pentru cei mici** și **Culcuș pentru câine**, iar deasupra lor stilurile proiectului. Alege unul și schimbă-l la mijloc:

- **Amplasare.** **Sub rampa** (la o scară în L sau U), **Latura deschisă** (implicit cea opusă întoarcerii), unde **Începe la** și **Se termină la** de-a lungul rampei (gol: de unde intradosul e la 300 mm peste pardoseală, până la ultima contratreaptă) și **Adâncimea** (gol: cât lățimea scării).
- **Fața.** **Partea joasă** închisă cu **Uși de dulap** (fiecare cam de **Lățimea ușii**, cu **Rafturi în spatele ușilor**), un **Panou închis** sau **Lăsat deschis**; o **Bibliotecă la capătul înalt**.
- **Element principal.** Un **Birou cu un raft** (și o lampă), o **Bancă cu perne** (cu sertare în față), o **Bancă de hol și cuiere** (cu compartimente pentru pantofi dedesubt), o **Ușă de căsuță de joacă** (și o **Fereastră lângă ușă**) sau un **Pat de câine** în spatele unui gol ancadrat, fiecare cu dimensiunile lui.
- **Materiale.** **Grosimea plăcii de mobilier** și materialele pentru **Corp**, **Fronturi** și **Țesătură**, din cele ale proiectului.

În dreapta, previzualizarea arată spațiul văzut din latura deschisă, sub intradosul rampei, și de sus, lungimea, adâncimea și înălțimile lui și piesele. Fiecare piesă e tăiată de pantă; o piesă care nu are loc (un birou unde scara e prea joasă) e lăsată deoparte, iar previzualizarea spune asta. **Creează** sau **Aplică** e un singur pas de anulare. **Salvează ca stil…** păstrează mobilierul ca stil al proiectului; **Actualizează stilul** îl salvează din nou, iar toate spațiile stilului îl urmează.

![Spațiul de sub scară în panoul de proprietăți](shots/under-stair-properties.webp)

Spațiul e un element separat, găzduit de scara lui. Când scara se schimbă (lățime, rotire, trepte, tip), spațiul se reface sub ea, iar odată cu scara se șterge și el. Câmpurile de amplasare sunt în panoul de proprietăți, iar **Configurează…** redeschide configuratorul.

![Cele șase stiluri, sub șase scări](shots/under-stair-3d.webp)

Planurile, secțiunile, fațadele și planșele îl desenează ca pe orice alt element: secționat unde îl taie planul, văzut dedesubt.

![Colțul de lectură în plan, celelalte alături](shots/under-stair-plan.webp)

![O secțiune de-a lungul rampei prin colțul de lectură](shots/under-stair-section.webp)

**Arhitectură → Extrasul spațiilor sub scară** listează piesele fiecărui spațiu, cu bucățile, materialul și volumul lor, și totalurile pe material. În IFC, spațiul e un IfcElementAssembly („Under-stair space”) din IfcFurniture (rafturi, birou, bancă, perne, pat), IfcDoor, IfcPlate, IfcDiscreteAccessory (mânere, cuiere) și IfcLightFixture, legat de IfcStair-ul lui.

## Rampe

Alege **Arhitectură → Rampă** (<kbd>RU</kbd>). Dă clic pe mijlocul capătului de jos al rampei, apoi pe un punct în direcția în care urcă; pentru una în L sau în zigzag, un al treilea clic la stânga sau la dreapta tronsonului spune încotro întoarce. Prima dată, proiectul primește din bibliotecă **Ramp 6 %**: lată de 1,20 m, cu panta de 6 %, în tronsoane de cel mult 9 m cu podeste de 1,50 m între ele, o placă de beton armat sub un finisaj, mâini curente pe ambele părți. Urcă până la etajul de deasupra, dacă tipul sau **Diferența de nivel** nu spun altceva.

**Arhitectură → Rampă de intrare** se pune ca treptele de intrare: dă clic pe mijlocul marginii terasei, apoi în partea spre care iese rampa. Urcă 450 mm de la teren până la ±0,00, plină, turnată pe teren, iar **Diferența de nivel** o schimbă ca la trepte.

Tipurile de rampe se fac în același configurator, **Arhitectură → Tipuri de scări**, cu **Tip de rampă nou**, sau din bibliotecă: **Switchback ramp** (în zigzag) și **Entrance ramp** (de intrare):

- **Formă.** **Dreaptă**, **Dreaptă, cu podeste**, **În L** sau **În zigzag (dus-întors)** cu **Ochiul scării** dintre tronsoane; **Lățimea**, **Panta** în procente, **Cea mai lungă rampă** (mai lungă, rampa se împarte în tronsoane egale cu podeste între ele) și **Lungimea podestului**. Lungimea în plan e înălțimea împărțită la pantă: 3 m la 6 % înseamnă 50 m. **Urcă** merge ca la scări.
- **Reguli.** **Panta cea mai mare** (8 % implicit), **Lățimea minimă** și **Înălțimea liberă**. Verificările arată cu roșu o pantă mai mare decât permit regulile, un tronson mai lung decât cel mai lung și un podest mai scurt de 1,50 m sau decât lățimea rampei.
- **Construcție.** **O placă înclinată din beton armat**, cu **Grosimea plăcii**, sau **Plin, turnat pe teren**; finisajul și balustrada ca la scări.

![Rampa în zigzag în configurator](shots/ramp-types.webp)

Selectează o rampă ca să-i vezi tipul, **Lățime**, **Întoarcere**, **Decalaj la bază** și **Diferența de nivel**, o **Pantă** și o **Lungime a podestului** proprii (goale: ale tipului), lungimile tronsoanelor și verificările. Planurile îi desenează tronsoanele și podestele, săgeata de urcare și panta în procente de-a lungul fiecărui tronson. O rampă care urcă până la etajul de deasupra cere un gol în placa acestuia, ca o scară.

![Rampa în zigzag și rampa de intrare în plan, cu panta pe fiecare tronson](shots/ramps-plan.webp)

![Rampele în 3D](shots/ramps-3d.webp)

În IFC o rampă e un IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP sau HALF_TURN_RAMP) de tipul unui IfcRampType, alcătuit din IfcRampFlight, podeste (IfcSlab LANDING) și IfcRailing.

## Balustrade

**Architecture → Railing** (<kbd>RL</kbd>) desenează o balustradă de sine stătătoare pe marginea unui balcon, a unei terase sau a unei galerii: dai clic pe punctele ei, Enter termină. Are modelele balustradelor de scară (stâlpișori și mână curentă, baluștri, sticlă, cabluri sau panou plin), înălțimea (implicit 1 m), decalajul bazei, profilul și mărimile mâinii curente și distanța între stâlpișori în proprietăți; punctele se trag de gripuri (Ctrl/⌘-clic pe mijlocul unei laturi adaugă unul). În IFC e un IfcRailing (GUARDRAIL).

## Balcoane

**Architecture → Balcony** (<kbd>BY</kbd>) prinde un balcon de un perete: alegi stilul în bara liniei de comandă și dai clic în afara feței peretelui, acolo unde vine; balconul stă pe acea parte a peretelui, la cota pardoselii etajului. Un **balcon franțuzesc** (Juliet) pus lângă o ușă sau o fereastră se centrează pe ea, cu 200 mm mai lat. Unde nu e niciun perete aproape, balconul stă unde dai clic. **Balcony by outline** desenează orice formă: dai clic pe colțuri, prima latură e lipită de clădire. **Gallery edge** desenează marginea unui mezanin peste un spațiu cu înălțime dublă, din două puncte (golul la stânga, galeria la dreapta).

Stilurile predefinite acoperă felurile de balcoane cerute:

- **Juliet balcony**: o balustradă cu baluștri în fața unei uși-fereastră, peste o mică placă de piatră pe două console.
- **Cantilevered slab, glass**: o placă de beton în consolă din planșeu, printr-o întrerupere de punte termică, placată cu pantă, cu lăcrimar sub margine, sticlă pe trei laturi.
- **Stacked, on posts**: o placă pe stâlpi de oțel în față, care stau pe balconul de dedesubt (sau pe teren); repetată etaj peste etaj.
- **On steel brackets**, **Hung from tie rods** (tiranți de la colțurile din față până în perete), **Rounded corners**, **Curved front** cu parapet plin.
- **Loggia** (logie): retrasă în fațadă. Își taie golul prin perete (un gol cât logia de lat și cât **Recess height** de înalt), își face pereții laterali și o placă de tavan în spatele feței și își ridică propriul **Back wall** (de tipul peretelui, la fel de înalt) la adâncimea logiei, acolo unde vine ușa. Balustrada e la față.
- **Corner balcony**: de-a lungul peretelui și în jurul colțului clădirii, înapoi pe peretele lateral; pus lângă capătul unui perete, îl înconjoară.
- **Mezzanine gallery edge**: placa galeriei în spatele marginii, o fascie peste margine și o balustradă cu bare.

![Balcoane la etaj: un balcon franțuzesc la ușă, unul pe stâlpi, unul de colț, o logie, unul curb, unul rotunjit și unul suspendat; balconul etajului de deasupra, întrerupt](shots/balconies-plan.webp)

Planul desenează placa, finisajul și balustrada unui balcon cum le taie sau le vede, iar balcoanele etajului de deasupra cu linie întreruptă, deasupra secțiunii. Secțiunile și fațadele desenează placa, balustrada, stâlpii și consolele.

### Configuratorul

**Architecture → Balcony configurator** (sau rotița de lângă **Style** în proprietățile balconului) lucrează ca acela al acoperișului: stilurile în stânga (întâi ale proiectului, apoi cele predefinite), câmpurile balconului pe file în dreapta, peste planul lui sau o secțiune prin mijloc, așa cum le-ar construi ciorna:

- **Shape**: **Kind** (franțuzesc, consolă, stâlpi, console, logie, colț, mezanin), **Outline** (dreptunghi, colțuri rotunjite cu **Corner radius**, front curb cu **Front bulge** sau poligonul desenat), **Width** și **Depth**, întoarcerea pe latură a balconului de colț, placa și **Fascia height** ale galeriei.
- **Slab and finish**: **Slab thickness** și materialul plăcii, **Edge** (dreaptă, cu **drip** — lăcrimar — sub ea, cu **nosing** — finisajul trecut peste — sau **tapered**, subțiată până la **Edge thickness**), grosimea și materialul finisajului și **Fall (%)** spre față (finisajul mai gros la perete), intradosul și **Thermal break** (80 mm între placă și planșeul din interior; o spun tabelul și IFC-ul).
- **Railing**: modelele balustradelor (stâlpișori și mână curentă, baluștri, sticlă, cabluri, panou plin) sau un **parapet** cu grosimea și materialul lui, sau nimic; înălțimea, doar în față sau și pe laturi, cât e retrasă de la margine, mâna curentă și stâlpișorii, culoarea.
- **Supports**: **Carried by** nimic (consolă), **posts** (rotunzi sau pătrați, mărimea și distanța), **brackets** (adâncimea, înălțimea și distanța) sau **hangers** (înălțimea peste placă la care se prind tiranții) și materialul lor.
- **Loggia**: înălțimea golului, grosimea pereților laterali, placa peste fața peretelui, placa de tavan și peretele din spate.

![Configuratorul de balcon: stilurile, forma și planul](shots/balcony-configurator.webp)

![Susținerile, peste o secțiune prin balcon](shots/balcony-configurator-section.webp)

**Apply** schimbă balconul într-un singur pas. **Save as style…** îl păstrează ca stil al proiectului (un tip de balcon), oferit în bara uneltei și în proprietăți; **Update style** îl salvează din nou în stilul lui și toate balcoanele stilului îl iau; lângă numele unui stil îl redenumești sau îl ștergi (balcoanele lui rămân cum sunt). Un stil e un tip: articolele de deviz de pe el (Execuție → Map articles) măsoară balcoanele lui.

![Proprietățile unui balcon: stilul și toate câmpurile felului lui](shots/balcony-properties.webp)

În panoul de proprietăți un balcon arată doar câmpurile pe care felul lui le folosește (golul logiei, adâncimea consolei doar pe console). Gripurile îl trag de-a lungul peretelui, mijlocul frontului dă adâncimea (la logie, cât de adânc intră golul), mijloacele laturilor lățimea; unul desenat se trage de colțuri. Golul și peretele din spate ale logiei o urmează și pleacă odată cu ea când e ștearsă.

**Stack on storeys above** (în configurator, în proprietăți sau **Architecture → Stack on storeys above**) copiază balcoanele selectate pe toate etajele de deasupra într-un singur pas, cu Copy to storeys: copia se prinde de peretele etajului ei care stă unde stă peretele original, o logie suprapusă taie peretele acelui etaj, stâlpii stau pe balconul de dedesubt.

![Fațada de sud în 3D: balconul franțuzesc, balcoanele suprapuse pe stâlpi, cel de colț și cel suspendat în tiranți](shots/balconies-3d.webp)

![Dinspre nord: logia tăiată în perete, balconul curb cu parapetul și cel rotunjit](shots/balconies-3d-north.webp)

**Architecture → Balcony schedule** listează fiecare balcon cu etajul, stilul, felul, lățimea, adâncimea, aria, lungimea și modelul balustradei, ce îl susține și întreruperea de punte termică, cu totalurile. În IFC un balcon e un IfcElementAssembly (USERDEFINED, „Balcony”) de tipul unui IfcElementAssemblyType, alcătuit din părțile lui: placa un IfcSlab (FLOOR), finisajul și intradosul IfcCovering (FLOORING, CEILING), balustrada un IfcRailing (GUARDRAIL) sau parapetul un IfcWall (PARAPET), stâlpii IfcColumn, consolele IfcDiscreteAccessory (BRACKET), tiranții IfcMember, întreruperea de punte termică un IfcBuildingElementPart; golul logiei e un IfcOpeningElement în peretele ei. Lapin își citește înapoi propriile balcoane ca balcoane.
