---
title: Lifturi
order: 43
summary: Lifturi dintr-un configurator cu douăsprezece feluri dimensionate după EN 81-20 și EN 81-70, și lifturi circulare cu vacuum pentru case, cu puțul, groapa, înălțimea de siguranță, camera mașinii, cabina și ușile de palier, golurile din plăcile prin care trec, planuri pe fiecare nivel, extras și IFC.
---

## Așezarea unui lift

**Arhitectură → Lift** (<kbd>LF</kbd>) pune un lift cu un clic în plan. Clicul e mijlocul puțului, iar ușile privesc în josul planului (îl rotești din bara de comandă, 0°, 90°, 180° sau 270°, ori mai târziu din mânerul lui). Liftul merge de la nivelul planului până la cel mai înalt nivel al proiectului și oprește pe fiecare nivel dintre ele. Configuratorul lui se deschide imediat.

Cât timp unealta e pornită, bara liniei de comandă și panoul de proprietăți arată **stilul** pe care îl pune: unul din cele douăsprezece feluri ale catalogului sau un stil propriu al proiectului.

## Configuratorul

![Configuratorul liftului, cu stilul accesibil pentru scaun rulant](shots/elevator-configurator.webp)

Stilurile sunt în stânga. Un stil completează toate câmpurile, iar apoi poți schimba orice câmp de mână. Catalogul are aceste feluri:

| Stil | Persoane | Sarcină nominală | Cabină (mm) | Acționare |
|---|---|---|---|---|
| Rezidențial mic | 4 | 320 kg | 900 × 1000 | fără cameră a mașinii |
| Persoane standard | 6 | 450 kg | 1000 × 1250 | fără cameră a mașinii |
| Persoane mediu | 8 | 630 kg | 1100 × 1400 | cu tracțiune, camera mașinii sus |
| Persoane mare | 10 | 800 kg | 1350 × 1400 | cu tracțiune |
| Persoane, trafic intens | 13 | 1000 kg | 1600 × 1400 | cu tracțiune, 1,6 m/s |
| Accesibil pentru scaun rulant | 8 | 630 kg | 1100 × 1400, ușă 900 | EN 81-70 tip 2 |
| Lift de spital pentru paturi | 17 | 1275 kg | 1200 × 2300 | cu tracțiune, cabină cu trecere |
| Lift de marfă | 26 | 2000 kg | 1500 × 2700 | cu tracțiune, 0,5 m/s |
| Hidraulic (clădiri joase) | 8 | 630 kg | 1100 × 1400 | piston, grupul hidraulic într-o cameră lângă puț |
| Fără cameră a mașinii (MRL) | 13 | 1000 kg | 1100 × 2100 | mașina în înălțimea de siguranță |
| Panoramic din sticlă | 8 | 630 kg | 1100 × 1400 | cabină din sticlă într-un puț din sticlă |
| Lift de casă | 3 | 250 kg | 800 × 950 | 0,15 m/s, groapă mică |

În dreapta sunt trei file:

- **Cabină și sarcină**: acționarea (cu tracțiune, fără cameră a mașinii, hidraulic, de casă), sarcina nominală și persoanele, viteza, tipul accesibil (EN 81-70 tip 1, 2 sau 3), lățimea, adâncimea și înălțimea cabinei, pereții de cabină din sticlă, liftul pentru pompieri, liftul de evacuare. **Sarcina și persoanele după cabină** pune cea mai mică sarcină nominală pe care EN 81-20 o dă ariei cabinei și persoanele pe care le duce.
- **Uși și opriri**: o intrare sau două (cabină cu trecere), lățimea și înălțimea ușii, deschiderea centrală sau laterală și foile ei (două sau trei foi glisează una în spatele alteia), oprirea cea mai de sus și nivelurile la care oprește. Pe un nivel pe lângă care trece fără să oprească, peretele puțului rămâne plin și nu are ușă de palier.
- **Puț**: partea contragreutății (sau a pistonului la un lift hidraulic), groapa sub oprirea cea mai de jos și înălțimea de siguranță peste cea mai de sus (amândouă urmează acționarea și viteza până când scrii valorile tale), camera mașinii (sus la tracțiune, lângă puț jos la un lift hidraulic, altfel deloc), pereții puțului (un tip de perete al proiectului sau beton de grosimea dată), puțul din sticlă, rotația.

![Secțiunea prin cabină, de la groapă la vârful puțului](shots/elevator-section.webp)

Previzualizarea arată planul la oprirea cea mai de jos și, pe celelalte file, o secțiune prin cabină cu fiecare oprire (opririle deservite cu roșu, cele pe lângă care trece întrerupt). Dedesubt, ce spune EN 81: o cabină mai mare decât permite sarcina ei, mai multe persoane decât duc sarcina sau aria, o cabină sau o ușă accesibilă prea mică, un lift hidraulic cu o cursă de peste circa 18 m. Urmează dimensiunile puțului la interior și la exterior, aria cabinei față de cât permite sarcina, opririle, cursa și plăcile pe care le deschide puțul.

**Aplică** schimbă liftul într-un singur pas de anulare. **Salvează ca stil…** îl păstrează ca stil al proiectului; **Actualizează stilul** îl salvează din nou și toate lifturile acelui stil îl urmează. Un stil al proiectului se poate redenumi sau șterge (lifturile lui rămân cum sunt).

## Un lift circular cu vacuum

![Configuratorul unui lift circular cu vacuum: tubul, ușile paliere, fiecare privind în altă parte](shots/vacuum-lift-configurator.webp)

Pentru o casă, un **lift cu vacuum** e un tub transparent în care aerul ridică o cabină: fără puț, fără groapă și fără cameră a mașinii. Trei stiluri din grupul **Vacuum** al barei îl pun:

| Stil | Persoane | Sarcină nominală | Tub (exterior) | Ușă |
|---|---|---|---|---|
| Lift cu vacuum, o persoană | 1 | 160 kg | Ø 750 | glisantă curbă |
| Lift cu vacuum, două persoane | 2 | 205 kg | Ø 933 | glisantă curbă |
| Lift cu vacuum, scaun rulant | 2 | 240 kg | Ø 1300 | batantă |

Valorile sunt orientative: decid datele producătorului și EN 81-41. În configurator:

- **Cabină și sarcină**: diametrul exterior al tubului (butoanele Ø 750, Ø 933 și Ø 1300, sau orice valoare scrii), diametrul cabinei (cu 120 mm mai mic decât tubul până când scrii unul al tău), înălțimea cabinei, tubul din policarbonat sau sticlă, culorile tubului și ale cabinei.
- **Uși și opriri**: ușile de palier (glisante curbe, batante sau deloc, un palier deschis), lățimea, înălțimea și tocul lor, ușa cabinei (glisantă curbă sau deloc), unghiul spre care privesc ușile (0° fața, 90° dreapta). Fiecare oprire deservită are lângă ea lista ei: **Ca ușile** sau un unghi al ei, așa că pe fiecare nivel ușa poate da spre altă cameră.
- **Tub și cadru**: stâlpii cadrului (câți și latura lor; doi stau lângă ușa de jos, ceilalți în jurul tubului, iar unul care ar sta în ușa altui palier e lăsat deoparte), culoarea cadrului, inelul din fiecare placă prin care trece, unitatea de capăt de sus și inelul de bază pe care stă.

Tubul stă pe inelul de bază pe placa de jos și deschide o gaură rotundă în fiecare placă de deasupra, umplută de inelul ei; când unitatea de capăt trece peste placa următoare, se deschide și aceea. În plan tubul și stâlpii sunt secționați, cabina e un cerc cu crucea ei, o ușă glisantă își arată foaia și, întrerupt, locul în care glisează, o ușă batantă foaia deschisă cu arcul ei.

![Un lift cu vacuum în 3D, cu tubul trecând prin placa etajului](shots/vacuum-lift-3d.webp)

## În desene

![Un lift în plan: puțul secționat, cabina cu crucea ei, contragreutatea și ușile de palier](shots/elevator-plan.webp)

Puțul deschide fiecare placă prin care trece, cu un gol propriu în fiecare placă. Când muți, rotești sau copiezi liftul ori îi schimbi nivelurile, golurile îl urmează. O placă desenată după lift: **Arhitectură → Goluri de puț** o deschide.

Fiecare plan al unui nivel la care ajunge liftul îl desenează: pereții puțului secționați, cabina cu crucea pe diagonale, contragreutatea cu diagonala ei (pistonul unui lift hidraulic ca cerc), iar pe nivelurile unde oprește foile ușii de palier și ale ușii cabinei, întrerupt unde glisează când se deschid. Secțiunile și fațadele desenează tot puțul, groapa, capacul, camera mașinii, cabina la oprirea cea mai de jos și ușile de palier. În 3D cabina stă la oprirea cea mai de jos, cu ușile închise.

![Un lift în 3D, deasupra casei](shots/elevator-3d.webp)

## Proprietăți, extras și IFC

![Proprietățile unui lift](shots/elevator-properties.webp)

Selectează un lift ca să-i schimbi oprirea cea mai de jos și cea mai de sus, să bifezi nivelurile la care oprește, să alegi tipul de perete al puțului, să citești avertismentele EN 81 și să deschizi **Configurator…**. Câmpurile lui (acționarea, sarcina, cabina, ușile, groapa, înălțimea de siguranță…) sunt dedesubt; cele care nu se aplică se ascund (fără cameră a mașinii la un lift MRL, fără partea contragreutății la unul hidraulic). Trage mânerul din mijloc ca să-l muți și mânerul din fața ușilor ca să-l rotești. **Mută**, **Copiază**, **Rotește** și **Oglindește** merg ca la orice element; un lift oglindit își mută contragreutatea pe cealaltă parte.

**Arhitectură → Extras de lifturi** listează fiecare lift cu felul, acționarea, sarcina nominală, persoanele, cabina, ușile, opririle deservite, nivelurile între care merge, cursa, viteza și puțul.

Fiecare lift are un tip făcut din felul, sarcina și cabina lui (Persoane mediu 630 kg, 1100 × 1400): lifturile la fel îl împart, deci un articol de preț pus pe tip (**Execuție → Atribuie articole…**) le prețuiește pe toate, iar planificarea le găsește după clasă (IfcTransportElement).

În IFC un lift e un **IfcTransportElement** cu tipul predefinit **ELEVATOR**, al unui **IfcTransportElementType**, cu seturile IFC proprii **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) și **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). Lapin își citește fișierul înapoi ca lift nativ, cu opririle lui și golurile din plăci.
