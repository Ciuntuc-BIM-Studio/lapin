---
title: Grafuri vizuale
order: 128
summary: Legi noduri într-o rețetă — numere, liste, puncte și comenzile uneltelor — vezi ce ar face ca o fantomă și o creezi într-un singur pas de anulare.
---

## Ce e un graf

Un **graf** e o rețetă desenată din casete și fire. Fiecare casetă e un **nod**: un număr, un interval de numere, un punct, o sumă sau chiar una dintre unelte (Perete, Placă, Stâlp… orice comandă a ediției tale). Firele duc ce dă un nod în altul. Lapin calculează graful pe măsură ce îl schimbi și arată ce ar face ca o **fantomă** în planuri și în vederea 3D; nimic nu se face până nu apeși **Creează**, și atunci e un singur pas de anulare.

Grafurile nu cer programare și nici Python: le calculează motorul lui Lapin, la fel în aplicația desktop și în Lapin din browser. Se păstrează cu proiectul.

## Panoul Grafuri

Deschide **Vedere → Grafuri**. Panoul se deschide lângă vedere: trage linia dintre ele ca să împarți lățimea. Pe desktop, trage fila **Grafuri** afară din fereastră (sau apasă butonul de fereastră de lângă file) ca panoul să aibă o fereastră a lui, care plutește sau stă pe al doilea ecran; fantoma se vede tot în fereastra principală.

![Un graf care face un rând de pereți: panoul lângă plan, pereții ca fantomă](shots/graphs-panel.webp)

- **Sus**: grafurile proiectului, **Graf nou**, numele grafului (scrie ca să-l redenumești), **Șterge graful**, ochiul care arată sau ascunde fantoma și **Creează**.
- **Pânza**: dublu-clic pe un loc gol ca să adaugi un nod: scrie câteva litere (`wall`, `range`, `point`) și apasă **Enter**. Trage de la o ieșire (partea dreaptă a unui nod) la o intrare (partea stângă) ca să le legi. Mută un nod trăgându-l de titlu; Shift-clic ca să alegi mai multe, sau trage un dreptunghi în jurul lor; **Delete** le scoate. Rotița mărește, butonul din mijloc (sau **Space** și tragere) mută pânza.
- **Inspectorul**, în dreapta: intrările nodului ales care nu au fir (scrie o valoare), ce dă nodul și de ce a căzut, dacă a căzut.

Fiecare schimbare e un pas de anulare: **Ctrl+Z** desface un nod adăugat, mutat sau legat cum desface un perete.

## Porturi și liste

Porturile sunt colorate după ce duc: numerele albastru, textele chihlimbariu, punctele verde, elementele mov, listele gri. Un fir între feluri care nu se potrivesc e refuzat, cu motivul.

Nu există arbori de date. O **listă** legată la o intrare care ia o singură valoare rulează nodul o dată pentru fiecare element: o **Serie** de trei numere în X-ul unui **Punct** dă trei puncte, iar trei puncte în Începutul unui **Perete** dau trei pereți. Mai multe liste merg alături; o listă mai scurtă își repetă ultimul element. Un fir care duce o listă e desenat mai gros, cu numărul elementelor pe el. Unele intrări iau lista întreagă (**Număr**, **Sumă**, **Element din listă**, **Polilinie**).

Nodul unei unelte are aceleași intrări ca și comanda ei: un **Perete** are punctele **Început** și **Sfârșit**, o înălțime, un nivel, un tip. Ieșirile lui sunt **Elemente făcute** și **Rezultat** (ce a răspuns comanda).

## Erori

Un nod care cade se face roșu și spune de ce în inspector (și când treci cu mouse-ul peste el): un perete fără lungime, o împărțire la zero. Nodurile de după el spun care intrare nu are nimic; restul grafului se calculează mai departe. Când cad doar câteva elemente ale unei liste, nodul se face chihlimbariu și spune câte.

## Creează

**Creează** face ce arată fantoma, într-un singur pas de anulare numit **Creare din graf**. Dacă un nod al uneltelor cade, nu se face nimic, iar Lapin spune care nod a căzut. După creare fantoma se ascunde (ochiul o arată din nou).

![Rândul de pereți creat: trei pereți, un singur pas de anulare](shots/graphs-baked.webp)
 Apeși din nou **Creează** după o schimbare și graful face elemente noi; cele făcute înainte rămân cum sunt (un graf care își ține elementele la zi e planificat).

## Bine de știut

- Un graf nu rulează cod și nu poate citi sau scrie fișiere: uneltele care importă, exportă sau deschid proiecte nu sunt oferite ca noduri.
- Un graf se calculează cel mult 5 secunde și rulează cel mult 2.000 de comenzi deodată; o listă are cel mult 10.000 de elemente.
- Într-o sesiune partajată, grafurile sunt ale gazdei, ca scripturile: un invitat nu le poate deschide, previzualiza, crea din ele, redenumi sau șterge; panoul lui Grafuri spune asta.
