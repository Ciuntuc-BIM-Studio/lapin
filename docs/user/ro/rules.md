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

Alege un pachet din lista din stânga ca să-l vezi. **Folosește în acest proiect** îl face pachetul pe care îl citesc uneltele (*În uz* îl marchează). **Duplică** copiază pachetul arătat, **Șterge** scoate unul de-al tău (proiectul revine la cel al lui Lapin dacă era în uz), iar **Nume** și **Descriere** ale pachetelor tale se pot edita, împreună cu ce spune pachetul despre el: **Țară**, **Ediția normelor** din care au fost citite valorile (regulile se schimbă: un pachet o numește pe cea pe care o urmează), **Limba** (un cod de două litere, ca sv) și **Surse, câte una pe rând**. Ele călătoresc cu pachetul în fișierul lui.

**Exportă…** scrie pachetul ca fișier `.lapinrules`, iar **Importă…** citește un fișier înapoi ca pachet al proiectului: așa trec între proiecte regulile unei firme sau ale unui coleg.

## Ce ține un pachet

Câte un tab pentru fiecare parte a pachetului pe care o citesc modulele proiectului:

- **Scări.** O coloană pentru fiecare **Set de scări** (residential, public, entrance și ramp, plus seturile adăugate de tine la **Numele setului nou**): contratreapta, treapta și pasul (2 contratrepte + o treaptă), fiecare cu valoarea minimă, țintă și maximă, lățimea minimă, înălțimea liberă, numărul minim și maxim de contratrepte într-o rampă și panta maximă. O valoare minimă peste cea maximă e refuzată, cu explicație. Un tip de scară își ia setul în tabul **Reguli** (vezi [Scări și rampe](stairs.md)).
- **Armare.** **Oțeluri oferite** și **Oțelul unei bare noi**, **Diametre oferite**, **Dorn** (diametrul de îndoire, un factor de Ø după diametrul barei) și **Ciocuri** (coada dreaptă, un factor de Ø după unghiul cârligului), plus **Coada minimă a cârligului**. Listele de oțel și de diametru ale uneltelor de bare și ale Proprietăților sunt acestea; o bară nouă ia de aici oțelul, cozile cârligelor și, când dornul diferă de al lui Lapin, raza de îndoire (vezi [Armare simplă](rebar.md)).
- **Acoperișuri.** **Panta minimă** a fiecărei **Învelitori**: un acoperiș sub ea e semnalat, fiindcă s-ar putea să nu scurgă apa.
- **Încărcări.** **Seturile de coeficienți** pe care le citesc combinațiile de încărcări (vezi [Încărcări](loads.md)): `en` (EN 1990 Anexa A1, valori recomandate) și `cr0` (CR 0-2012) ale lui Lapin, plus seturile adăugate de tine la **Numele setului nou** (fiecare pornit din cel arătat). Pentru setul ales: **Etichetă**, γG,sup, γG,inf, γQ, ξ și **Expresia** (6.10 sau 6.10a și 6.10b) cu care pornește un proiect; **γd pe clase de siguranță**, pentru regulile care pun clasa de siguranță pe partea încărcărilor (γd înmulțește acțiunile nefavorabile ale combinațiilor ultime; fără clasă, γd 1); **ψ al zăpezii după s0,k al amplasamentului**, pentru regulile care dau ψ al zăpezii după încărcarea din zăpadă (fără zone: ψ din tabel; cu zone, s0,k al amplasamentului ia ultima zonă pe care o atinge); **ψ pe categorii**; și **Încărcări utile, qk** ale fiecărei categorii de folosință. Un proiect își alege setul și clasa de siguranță în Încărcări → Combinații. Cele două seturi ale lui Lapin țin exact valorile folosite mereu de combinații, așa că un proiect dă aceleași combinații cu pachetul lui Lapin ca și cu o copie a lui.

![Fila Încărcări a unui pachet](shots/rules-loads.webp)

- **Climă.** O listă de regiuni, pentru Suedia comuna (kommun), pentru altă țară județul sau localitatea: **Regiunile sunt** le dă numele (Kommun, County), iar fiecare rând dă pentru o regiune **sk** (încărcarea caracteristică din zăpadă pe sol, kN/m²) și **vb,0** (viteza de bază a vântului, m/s; alături se arată qb = ½ · 1.25 kg/m³ · vb,0²) sau, acolo unde regulile tale dau presiunea, **qb** (kN/m²). Adaugi o regiune la **Numele noii regiuni** sau cu **Lipește o listă…** din rânduri copiate dintr-un tabel (nume, sk, vb,0, coloanele separate prin tab-uri sau punct și virgulă; un rând de antet e lăsat deoparte, o regiune deja în listă preia valorile lipite). Un proiect își alege regiunea în Încărcări → Zăpadă și vânt (vezi [Încărcări](loads.md)). Pachetul lui Lapin nu are regiuni.

![Fila Climă a unui pachet](shots/rules-climate.webp)

- **Accesibilitate.** Lățimea liberă minimă a unei uși (în interiorul tocului, cum o scriu etichetele ei cu `{clearWidth}`), lățimea minimă a unui coridor (o încăpere cu funcțiunile date la **Coridoarele sunt încăperile cu funcțiunile**, implicit Circulation, în locul cel mai îngust, până la finisaje), **Cerc de girație, diametru** pe care trebuie să-l cuprindă o încăpere cu funcțiunile numite (implicit camere de zi, dormitoare, bucătării, băi, WC-uri și coridoare) și dacă **Rampele** se verifică după setul ramp din fila Scări (panta lui maximă și lățimea minimă, ca limita să fie scrisă o singură dată). O valoare goală nu se verifică: pachetul lui Lapin nu verifică nimic din acestea, așa că verificarea unui proiect rămâne cum era până când valorile unui pachet spun altfel.

![Fila Accesibilitate a unui pachet](shots/rules-accessibility.webp)

- **Șuruburi.** Clasele de șuruburi pe care le poate primi o îmbinare metalică (4.6, 5.6, 8.8 și 10.9 ale lui Lapin; ale tale, ca A325 sau A490, adăugate pachetului). Tabul listează clasele ca o coloană de nume: schimbi un nume direct în câmp, **Scoate această clasă din listă** cu ×-ul (o clasă rămâne mereu), sau scrii o **Clasă nouă** și apeși Enter ori **Adaugă clasa**. Lista de clase a unei îmbinări metalice, în proprietățile ei și în îmbinările halei, este aceasta (o clasă pe care o îmbinare o are deja rămâne oferită pentru ea). Pachetul lui Lapin e doar pentru citire: Duplică-l ca să schimbi clasele.

## Verifică proiectul

**Verifică proiectul** listează ce are proiectul diferit de regulile în uz, pe module: un tip de scară a cărui contratreaptă nu e a pachetului, o bară de un oțel pe care pachetul nu-l oferă, un acoperiș sub panta minimă, o îmbinare metalică cu o clasă de șuruburi pe care pachetul n-o are și, când Accesibilitatea pachetului o cere, o ușă mai îngustă decât lățimea liberă minimă, o rampă mai abruptă (sau mai îngustă) decât setul ramp, un coridor mai îngust decât lățimea minimă, o încăpere care nu cuprinde cercul de girație. Dă clic pe un rând ca să selectezi elementul despre care vorbește. Verificarea nu schimbă nimic.

## Aplică la tipurile de scări

Un tip de scară își păstrează regulile cu care a fost făcut, așa că o schimbare a pachetului nu mută o scară deja desenată. **Aplică la tipurile de scări** copiază regulile în uz în fiecare tip de scară, fiecare păstrându-și setul, într-un singur pas de anulare. Lapin spune câte tipuri s-au schimbat sau că fiecare urmează deja regulile.

![Verifică proiectul](shots/rules-check.webp)

## Un pachet pentru țara ta

Lapin livrează un singur pachet, al României, iar valorile lui sunt orientative. Ca să lucrezi după regulile altei țări, fă-ți un pachet cu ele:

1. **Duplică** *Romania (indicative)* și numește copia după țară și ediție, de pildă *Sweden, EKS 11*. Completează **Țară**, **Ediția normelor**, **Limba** și **Surse, câte una pe rând** (reglementarea, anul ei, o adresă).
2. Schimbă filele după regulile tale: seturile de scări, rampe și intrări, oțelurile, diametrele și ciocurile, pantele minime ale acoperișurilor, clasele de șuruburi, încărcările (un set de coeficienți al tău, cu γd pe clase de siguranță și ψ al zăpezii după s0,k, încărcările utile), clima (sk și vb,0 pe comune) și accesibilitatea (lățimile ușilor și coridoarelor, cercul de girație, rampele verificate).
3. **Folosește în acest proiect**, apoi alege noul set de coeficienți în Încărcări → Combinații și regiunea amplasamentului în Încărcări → Zăpadă și vânt, și **Verifică proiectul**.

Scrie valorile tu, una câte una, din ediția în vigoare. Nu copia într-un pachet textul unei norme sau tabele întregi: reglementările unor autorități se pot cita liber, dar standardele (EN și edițiile lor naționale) sunt protejate de drepturi de autor, iar un pachet rămâne mic, cu sursele numite. Un pachet e mereu marcat orientativ: valorile lui sunt ale tale de verificat.

Ca să împarți pachetul, **Exportă…** un fișier `.lapinrules` pentru colegi sau oferă-l tuturor utilizatorilor Lapin prin [magazinul de extensii](extensions.md): manifestul lui e de tipul `rules`, spune `"indicative": true`, dă o licență ca `CC0-1.0` sau `CC-BY-4.0` și limbile textelor lui (`"languages": ["sv"]`) și vine printr-un pull request în catalog. E verificat înainte să fie listat.
