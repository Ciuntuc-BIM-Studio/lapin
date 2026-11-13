---
title: Reguli
order: 53
summary: Limitele și alegerile de la care pornesc uneltele lui Lapin, păstrate ca date pe care le poți citi și schimba — limitele scărilor, oțelurile, diametrele și cârligele armăturii, pantele minime ale acoperișurilor, clasele șuruburilor; verificate față de proiect, niciodată impuse.
---

Uneltele lui Lapin lucrează cu numere: cât poate fi cel mult o contratreaptă, din ce oțeluri se poate face o bară, care e panta minimă a unei învelitori. **Gestionare → Reguli…** arată aceste numere ca tabele, ca să le vezi, să le schimbi după norma țării tale sau obiceiul biroului și să le ții odată cu proiectul.

![Gestionare → Reguli](shots/rules-dialog.webp)

> Orice valoare din Reguli este **orientativă**. Cele pe care le livrează Lapin sunt scrise de Lapin, nu luate dintr-un indicator de norme; cele pe care le pui tu sunt ale tale. Verifică-le față de norma în vigoare înainte să te bazezi pe ele.

## Nimic nu e refuzat

O regulă e o propunere. Unealta Scară țintește contratreapta pe care o spun regulile, o bară nouă ia oțelul din listă, un acoperiș sub panta minimă a învelitorii lui e semnalat. Poți desena oricând ce vrei: uneltele propun și avertizează, nu refuză.

## Pachetele

Un **pachet** e un set complet de reguli, cu un nume.

- **Romania (indicative)** e pachetul lui Lapin, marcat *Lapin, orientativ*. Ține valorile pe care uneltele le-au folosit mereu (STAS 2965 și NP 063 la scări, SR EN 1992-1-1 la bare). E doar de citit: **Duplică** face în proiect o copie de schimbat.
- **Acest proiect** listează pachetele făcute de tine. Ele călătoresc în fișierul proiectului.

Alege un pachet din lista din stânga ca să-l vezi. **Folosește în acest proiect** îl face pachetul pe care îl citesc uneltele (*În uz* îl marchează). **Duplică** copiază pachetul arătat, **Șterge** scoate unul de-al tău (proiectul revine la cel al lui Lapin dacă era în uz), iar **Nume** și **Descriere** ale pachetelor tale se pot edita.

**Exportă…** scrie pachetul ca fișier `.lapinrules`, iar **Importă…** citește un fișier înapoi ca pachet al proiectului: așa trec între proiecte regulile unei firme sau ale unui coleg.

## Ce ține un pachet

Câte un tab pentru fiecare parte a pachetului pe care o citesc modulele proiectului:

- **Scări.** O coloană pentru fiecare **Set de scări** (residential, public, entrance și ramp, plus seturile adăugate de tine la **Numele setului nou**): contratreapta, treapta și pasul (2 contratrepte + o treaptă), fiecare cu valoarea minimă, țintă și maximă, lățimea minimă, înălțimea liberă, numărul minim și maxim de contratrepte într-o rampă și panta maximă. O valoare minimă peste cea maximă e refuzată, cu explicație. Un tip de scară își ia setul în tabul **Reguli** (vezi [Scări și rampe](stairs.md)).
- **Armare.** **Oțeluri oferite** și **Oțelul unei bare noi**, **Diametre oferite**, **Dorn** (diametrul de îndoire, un factor de Ø după diametrul barei) și **Ciocuri** (coada dreaptă, un factor de Ø după unghiul cârligului), plus **Coada minimă a cârligului**. Listele de oțel și de diametru ale uneltelor de bare și ale Proprietăților sunt acestea; o bară nouă ia de aici oțelul, cozile cârligelor și, când dornul diferă de al lui Lapin, raza de îndoire (vezi [Armare simplă](rebar.md)).
- **Acoperișuri.** **Panta minimă** a fiecărei **Învelitori**: un acoperiș sub ea e semnalat, fiindcă s-ar putea să nu scurgă apa.
- **Șuruburi.** Clasele de șuruburi pe care le poate primi o îmbinare metalică (4.6, 5.6, 8.8 și 10.9 ale lui Lapin; ale tale, ca A325 sau A490, adăugate pachetului). Tabul listează clasele ca o coloană de nume: schimbi un nume direct în câmp, **Scoate această clasă din listă** cu ×-ul (o clasă rămâne mereu), sau scrii o **Clasă nouă** și apeși Enter ori **Adaugă clasa**. Lista de clase a unei îmbinări metalice, în proprietățile ei și în îmbinările halei, este aceasta (o clasă pe care o îmbinare o are deja rămâne oferită pentru ea). Pachetul lui Lapin e doar pentru citire: Duplică-l ca să schimbi clasele.

## Verifică proiectul

**Verifică proiectul** listează ce are proiectul diferit de regulile în uz, pe module: un tip de scară a cărui contratreaptă nu e a pachetului, o bară de un oțel pe care pachetul nu-l oferă, un acoperiș sub panta minimă, o îmbinare metalică cu o clasă de șuruburi pe care pachetul n-o are. Dă clic pe un rând ca să selectezi elementul despre care vorbește. Verificarea nu schimbă nimic.

## Aplică la tipurile de scări

Un tip de scară își păstrează regulile cu care a fost făcut, așa că o schimbare a pachetului nu mută o scară deja desenată. **Aplică la tipurile de scări** copiază regulile în uz în fiecare tip de scară, fiecare păstrându-și setul, într-un singur pas de anulare. Lapin spune câte tipuri s-au schimbat sau că fiecare urmează deja regulile.

![Verifică proiectul](shots/rules-check.webp)
