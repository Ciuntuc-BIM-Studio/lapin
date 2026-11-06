---
title: Mobilier
order: 47
summary: Canapele, canapele de colț, fotolii, scaune, paturi, dulapuri, comode, biblioteci, bufete, mese, birouri și bucătării liniare, din tipuri parametrice, puse lângă pereți cu un clic.
---

## Așezarea mobilierului

Tabul **Interior** are, după finisaje, câte o unealtă pentru fiecare fel de piesă: **Sofa** (canapea, <kbd>SO</kbd>), **Corner sofa** (canapea de colț), **Armchair** (fotoliu, <kbd>AC</kbd>), **Chair** (scaun), **Bed** (pat, <kbd>BD</kbd>), **Bedside table** (noptieră), **Wardrobe** (dulap, <kbd>WD</kbd>), **Chest of drawers** (comodă), **Bookcase** (bibliotecă), **Sideboard** (bufet), **Table** (masă, <kbd>TB</kbd>), **Coffee table** (măsuță de cafea) și **Desk** (birou). Alegi una și dai clic în plan.

Un clic lângă un perete lipește piesa de el. Spatele ei stă pe fața peretelui (pe tencuială, nu pe miez), fața ei privește spre încăpere și alunecă de-a lungul peretelui până nu mai intră în peretele din colț. Un clic mai departe de pereți pune piesa exact unde ai dat clic.

Cât timp unealta e pornită, bara liniei de comandă arată:

- **tipul** pe care îl pune unealta, ales dintre tipurile proiectului de acel fel (dacă nu există încă niciunul, primul se face când pui piesa);
- **rotația**, 0°, 90°, 180° sau 270°, pentru o piesă care stă liberă;
- **Against walls** (lângă pereți), pe care o poți opri ca piesa să stea exact unde dai clic, chiar lângă un perete.

Panoul de proprietăți arată același tip înainte de clic, cu dimensiunile lui și **Edit types…**.

![O cameră de zi, o bucătărie și un dormitor mobilate, în plan](shots/furniture-plan.webp)

Planurile desenează mobilierul prin simbolul lui, ca în convenția de desen, nu tăiat:

- canapeaua cu spătarul, brațele și pernele;
- patul cu tăblia, pernele și pilota întoarsă;
- dulapul cu bara de umerașe și ușile;
- bucătăria cu chiuveta și plita, iar corpurile de sus întrerupte.

Fațadele și secțiunile desenează corpurile, cu hașurile materialelor lor. În 3D piesele sunt corpuri low-poly făcute de Lapin.

![Casa mobilată în 3D](shots/furniture-3d.webp)

## Scaune în jurul mesei și bucătăria

**Chairs round a table** (scaune în jurul mesei): dai clic pe o masă sau pe un birou. Scaunele se pun pe laturile lungi, cam câte unul la 600 mm, și câte unul la fiecare capăt, dacă e destul de lat; masa rotundă le primește pe un cerc. Fiecare scaun privește spre masă. Biroul primește scaune doar în față. Scaunele sunt de tipul ales pentru unealta **Chair**.

**Kitchen** (bucătăria, <kbd>KT</kbd>) se desenează cu două puncte de-a lungul unui perete:

- lungimea liniei e lungimea bucătăriei, iar spatele ei stă pe fața peretelui;
- corpurile de jos se împart egal, la aproximativ 600 mm;
- are blat, chiuvetă și plită (locul lor de-a lungul bucătăriei se alege pe tip), corpuri sus și o coloană pentru frigider la un capăt.

**Length** (lungimea) bucătăriei rămâne de modificat în proprietăți, iar corpurile se împart din nou.

## Proprietățile unei piese

Când selectezi o piesă, vezi:

- **Type** (tipul), felul și dimensiunile ei;
- **Rotation** (rotația);
- **Above the floor** (deasupra pardoselii), pentru o piesă ridicată pe un podest;
- **Length** (lungimea), la bucătărie.

**Flip** (întoarce) dă piesei cealaltă mână, pe loc: șezlongul pe partea cealaltă, frigiderul la celălalt capăt, colțul canapelei pe partea cealaltă. Fața ei rămâne unde era. **Modify → Mirror** face același lucru.

Mânerul din mijlocul piesei o mută; mânerul din fața ei o rotește, cu fața spre cursor.

![Proprietățile unei canapele](shots/furniture-properties.webp)

## Tipurile de mobilier

**Interior → Tipuri de mobilier** arată tipurile proiectului pe grupe (șezut, paturi, depozitare, mese, bucătărie), fiecare cu numărul de piese care îl folosesc. Alegi un fel și **New type** (tip nou), sau **Duplicate** (duplică) un tip ca să modifici o copie. Un tip are:

- **numele**, **lățimea**, **adâncimea** și **înălțimea** (la canapea, a spătarului; la pat, a tăbliei; la bucătărie, a blatului);
- parametrii felului lui:
  - canapelele și fotoliile: locuri, brațe, lățimea brațului, înălțimea șezutului, un șezlong în stânga sau în dreapta cu lungimea lui, picioare;
  - canapeaua de colț: partea pe care e colțul și lungimea acelei laturi;
  - patul: înălțimea saltelei, tăblia, tăblia de la picioare, pernele, pilota întoarsă;
  - dulapul: numărul de uși, batante sau glisante, sertare jos, soclu;
  - comoda: rânduri și coloane de sertare;
  - biblioteca: rafturi și uși jos, până la o înălțime;
  - bufetul: uși și sertare;
  - masa: dreptunghiulară, rotundă sau ovală, pe patru picioare sau pe un picior central;
  - biroul: un corp cu sertare în stânga sau în dreapta;
  - bucătăria: lățimea corpurilor de jos, soclul, blatul, locul chiuvetei și al plitei, corpurile de sus, coloana;
- **materialele** pe roluri: corp, tapițerie, blat, fronturi, lenjerie, metal, sticlă. Scrii orice nume de material; unul pe care proiectul nu-l are se face, iar **Manage → Materials** îi stabilește culoarea și hașurile.

Previzualizarea de lângă câmpuri arată simbolul din plan și fața tipului pe măsură ce îl schimbi. Toate piesele unui tip urmează orice schimbare a lui.

![Dialogul Furniture types cu o canapea și șezlongul ei](shots/furniture-types.webp)

## Extrase și IFC

**Furniture schedule** (extrasul de mobilier) arată fiecare tip cu felul, dimensiunile, numărul de piese și încăperile în care stau, plus totalul. **Furniture by room** (mobilierul pe încăperi) arată ce stă în fiecare încăpere. Amândouă sunt în navigatorul proiectului, la **Schedules**, și se tipăresc și se exportă ca orice extras. Un tip de mobilier cu articol de deviz se plătește pe bucată în deviz.

În IFC o piesă e **IfcFurniture**, cu tipul predefinit al felului ei (SOFA, CHAIR, BED, TABLE, DESK, SHELF, sau USERDEFINED cu Wardrobe, Kitchen…). **IfcFurnitureType** e legat prin IfcRelDefinesByType, iar corpurile au materialele lor. Lapin își citește înapoi propriul mobilier ca piese native, care se pot edita. Mobilierul din alte programe intră ca referințe.
