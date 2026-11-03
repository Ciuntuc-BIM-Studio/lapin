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
