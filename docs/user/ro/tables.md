---
title: Importul tabelelor
order: 111
summary: Aduci încărcări, cazuri de încărcare, valori de proprietăți și coduri de clasificare dintr-un tabel sau din celule lipite, cu coloanele ghicite, unitățile citite, o previzualizare rând cu rând și un singur pas de anulat.
---

Adesea datele sunt deja într-un tabel: încărcările din calculul unui inginer, rezistențele la foc dintr-un extras de uși, un sistem de clasificare. **Gestionare → Documente → Importă tabel…** citește un fișier CSV sau Excel (sau celule copiate dintr-un tabel) în ce ține Lapin: încărcări, cazuri de încărcare, parametri pe elemente, elemente și coduri de clasificare. Nimic nu se schimbă până nu apeși **Import**, iar importul întreg este un singur pas de anulat.

![Tabul Gestionare, cu Importă tabel… și Importă valori de proprietăți… în grupul Documente](shots/tables-ribbon.webp)

Același dialog se deschide, deja pe tabelul lui, de unde aparțin datele:

- **Importă valori de proprietăți…**, lângă **Importă tabel…**, și în **Extrase…**, la baza dialogului.
- **Importă încărcări…** și **Importă cazuri de încărcare…** în grupul **Încărcări** din tabul **Structură** și în dialogul **Încărcări** ([Încărcări](loads.md)).
- **Importă elemente dintr-un tabel…** și **Clasifică elementele dintr-un tabel…** în **Gestionare → Clasificări…**, pentru sistemul de clasificare ales acolo.

## Pașii

1. **Tabel** spune ce este fișierul. Alege-l, dacă unealta de la care ai venit nu l-a ales deja. **Descarcă șablonul** salvează un CSV cu coloanele tabelului și câteva rânduri-exemplu, de completat și importat.
2. **Alege un fișier CSV sau Excel…**, sau lipește celule copiate dintr-un tabel, cu antetele primele, în **Sau lipește**, și apasă **Folosește celulele lipite**. Se citesc și punctul și virgula ca separator de coloane, ca în Excel, și virgula zecimală; un număr poate avea miile grupate.
3. Se arată primele rânduri ale fișierului. **Rândul de antet** spune ce rând are antetele, când fișierul începe cu un titlu sau cu rânduri goale.
4. Lapin **ghicește ce este fiecare coloană** din antetul ei, în română, engleză, germană, franceză și alte limbi (*Valoare*, *Value*, *Wert*, *Q*, *Caz*, *Load case*…). Sub fiecare antet e o listă: schimbă ce e greșit sau alege **(omite)** pentru o coloană de care nu ai nevoie. O coloană poate avea un singur sens, așa că dacă îl alegi pentru altă coloană i-l iei pe cea dintâi. O coloană obligatorie e marcată cu asterisc, iar **De corelat încă** le enumeră pe cele pe care nu le are nicio coloană.
5. Unde o coloană are un număr, o a doua listă dă **unitatea**. Lasă-o pe **(unitatea din antet sau din rând)** și Lapin citește unitatea unde o scrie fișierul: în celula însăși (*4,5 kN/m*, *250 mm*), într-o coloană **Unitate** pe fiecare rând (*kN/m*, *kPa*) sau în antet (*Lățime (cm)*). O unitate scrisă în celulă are întâietate față de **Unitatea** rândului și de cea din antet. Lungimile pot fi în m, cm, mm, ft sau in; forțele în kN, N, daN, kgf, tf sau kip, pe metru sau pe metru pătrat (kN/m², kPa, psf…).
6. Ce lipsește fișierului și are nevoie tabelul, dialogul cere: un **Nivel pentru fiecare rând**, când încărcările nu au coloană de nivel.
7. **Previzualizarea** numără rândurile **gata** și pe cele **cu erori** și le descrie pe fiecare în cuvinte, *Încărcare liniară 4,5 kN/m … pe Parter*, sau spune de ce nu se poate citi. Urmează fiecare schimbare a coloanelor.
8. **Import** o face. Un rând cu eroare oprește importul, ca să nu rămână nimic pe jumătate; bifează **Sari peste rândurile cu erori** ca să le lași deoparte și să imporți restul. Mesajul spune câte rânduri au intrat și câte au fost lăsate deoparte.

![Valori de proprietăți lipite dintr-un tabel: coloanele ghicite, previzualizarea rând cu rând, un rând cu eroare](shots/tables-properties.webp)

Lapin ține minte, pe acest calculator, cum ai corelat coloanele unui tabel. Următorul tabel cu aceleași anteturi, în orice ordine, vine cu aceeași corespondență și aceleași unități.

### Corespondențe într-un fișier

**Exportă corespondența…** salvează corespondența ca fișier `.lapinmapping`, iar **Importă corespondența…** aplică una tabelului din dialog. Potrivirea se face după antet, nu după numărul coloanei, deci fișierul unui coleg cu aceleași anteturi în altă ordine se corelează ca al tău, și doar antetele pe care le găsește sunt corelate. Un fișier `.lapinmapping` ține și harta layerelor de la **Convertește layerele…** ([Desen](drafting.md)); o corespondență făcută pentru alt tabel este refuzată, cu o propoziție care o spune.

## Tabelele

### Încărcări

Un rând pe încărcare. **Valoare** este singura coloană obligatorie.

| Coloana | Ce ține |
| --- | --- |
| **Fel** | *area*, *line* sau *point*, în orice limbă. Lipsă, se citește din coordonate: un punct e o încărcare punctuală, două una liniară, un contur una pe suprafață. |
| **Caz de încărcare** | Cazul după cod sau nume. Un caz pe care proiectul nu îl are se face o singură dată pentru tot tabelul, permanent, variabil sau extraordinar după **Case action** (variabil când coloana lipsește). |
| **Nivel** | După nume. Fără coloană, îl dă **Nivel pentru fiecare rând**. |
| **x**, **y**, **x1**, **y1**, **x2**, **y2** | Locul unei încărcări punctuale sau cele două capete ale uneia liniare, implicit în metri. |
| **Outline points** | Colțurile unei încărcări pe suprafață, scrise *x y; x y; …* într-o singură celulă. |
| **Valoare**, **Unitate** | Mărimea, cu unitatea în celulă (*4,5 kN/m*), în antet sau în coloana **Unitate**. O valoare negativă acționează în sens opus. |
| **Direction** | Direcția în care acționează încărcarea: în jos (când e goală), în sus, +x, −x, +y sau −y (*down*, *up*). |
| **Height above the storey** | Înălțimea încărcării deasupra planșeului. |

![Încărcări dintr-un tabel: unitatea scrisă pe fiecare rând, iar o încărcare punctuală fără loc, lăsată deoparte](shots/tables-loads.webp)

### Cazuri de încărcare

**Caz de încărcare** (codul sau numele lui) este obligatoriu. **Descriere**, **Action** (permanent, variabil sau extraordinar), **Sursa** (proprie, finisaje, utilă, zăpadă, vânt, seism sau definită de utilizator) și **Category of use** sunt opționale. Un caz care există este schimbat; oricare altul este făcut.

### Valori de proprietăți

Completează sau schimbă parametri pe elemente. Prima coloană, **Element**, le găsește după **GlobalId**, după **marcă** sau după **nume** (toate elementele cu acel nume). Fiecare altă coloană e un parametru, luat după antetul ei: alege **Ca proprietate** pentru ea. Un antet scris *Pset_WallCommon.FireRating* (un set, un punct, o proprietate) pune valoarea în acel set de proprietăți, iar orice alt antet e un parametru al tău, cu felul (text, număr, da sau nu) citit din valori. O coloană corelată cu **Nume** redenumește elementele. Un rând al cărui element nu e în model este o eroare în previzualizare.

Valorile importate sunt cele pe care le arată [Proprietățile](interface.md), iar un parametru cu formulă se calculează din ele ([Parametri calculați prin formule](interface.md)).

### Elemente și coduri de clasificare

**Importă elemente dintr-un tabel…** (din **Gestionare → Clasificări…**) aduce tabelul unui sistem: un **Cod** și un **Titlu**, un **Parent code** ca să se facă arborele și **Clasificare**, sistemul, când fișierul le ține pe mai multe. **Clasifică elementele dintr-un tabel…** pune acele coduri pe elemente: un **Element** (GlobalId, marcă sau nume; și numele unui tip), **Cod**-ul lui și **Clasificare**. Când coloana lipsește, se folosește sistemul ales în dialog.

## Când ceva nu se citește

Previzualizarea spune de ce un rând nu merge (o unitate pe care Lapin nu o știe, un număr care nu e număr, un nivel sau un element care nu există). Corectează celula în fișier și alege-l din nou, sau schimbă unitatea coloanei în dialog; **Sari peste rândurile cu erori** importă restul. Dacă tot fișierul nu se citește, dialogul spune asta sub alegerea fișierului.
