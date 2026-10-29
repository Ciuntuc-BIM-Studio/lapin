---
title: Scări
order: 42
summary: Pui o scară între etaje, o configurezi în tipul ei și faci cele câteva trepte până la o terasă sau o ușă.
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

## Trepte de intrare (o terasă)

Treptele până la ușa de la intrare au o diferență de nivel proprie: urcă de la teren, aflat cu câteva trepte sub parter, până la ±0.00. Lapin are pentru ele un tip de scară gata făcut, **Trepte de intrare**: lat de 2 m, trei contratrepte de 150 mm, din beton plin turnat pe teren, fără balustradă.

![Trepte de intrare în fața unei terase, în plan](shots/entrance-steps-plan.webp)

1. **Terasa.** Desenează podestul ca placă: alege **Arhitectură → Placă** (<kbd>SB</kbd>) și dă clic pe conturul ei în afara peretelui, în fața ușii. Fața de sus a plăcii e la cota etajului ei (±0.00 la parter), iar tipul ei îi dă grosimea. Ca să schimbi grosimea, fă un tip de placă pentru terase în **Arhitectură → Tipuri de plăci**, de exemplu 150 mm de beton.
2. **Treptele.** Alege **Arhitectură → Trepte de intrare**. Dă clic pe mijlocul marginii terasei, apoi în partea spre care ies treptele. Treptele urcă până la marginea terasei și se termină la ±0.00. Pornesc cu 450 mm mai jos: **Decalajul la bază** e −450 mm.
3. **Înălțimea.** Selectează treptele. La **Diferența de nivel** scrie diferența reală dintre teren și terasă, de exemplu 300. Capătul de sus rămâne la terasă și piciorul se ridică: decalajul la bază devine −300 mm, iar treptele au două contratrepte. Cu **Lățime** le faci cât terasa de late, de exemplu 2400.

![Proprietățile treptelor de intrare: diferența de nivel, decalajul la bază, cotele](shots/entrance-steps-properties.webp)

**Niveluri** arată unde pornesc și unde se termină treptele, de exemplu −0.45 → ±0.00. Cu o singură contratreaptă (o diferență de nivel de 150 mm), treapta este chiar marginea terasei și nu mai rămâne nicio treaptă de construit. Dă-le două contratrepte sau mai multe ca să ai trepte în fața terasei.

Tipul intră în proiect prima oară când îl folosești. Ca să-l schimbi, deschide **Arhitectură → Tipuri de scări** și alege **Trepte de intrare**:

- **Formă → O diferență de nivel proprie.** Diferența de nivel a tipului; fiecare scară o poate schimba în continuare la **Diferența de nivel**.
- **Reguli → Setul de reguli → trepte de intrare.** Contratrepte de 120–170 mm și trepte de 300–400 mm, cu voie pentru o singură contratreaptă.
- **Construcție → Alcătuire.** **Plin, turnat pe teren** umple treptele până la bază. **Placă înclinată sub trepte** le face ca pe o scară interioară.

![Tipul Trepte de intrare în configuratorul de scări](shots/stair-types.webp)

Ca un alt tip de scară să urce o diferență de nivel proprie, bifează **O diferență de nivel proprie** în fila lui **Formă**. Ca să transformi o scară deja pusă în trepte de intrare, alege-i **Trepte de intrare** la **Tip de scară**: coboară astfel încât capătul ei de sus să fie la cota etajului.

![Casa cu terasa și treptele de intrare, în 3D](shots/entrance-steps-3d.webp)

În IFC, treptele sunt un IfcStair de tipul STRAIGHT_RUN_STAIR. Rampa lui e un IfcStairFlight cu NumberOfRisers, NumberOfTreads, RiserHeight și TreadLength, iar Pset_StairCommon are aceleași valori. Terasa e un IfcSlab.
