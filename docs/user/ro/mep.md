---
title: Instalații
order: 57
summary: Desenează instalațiile de încălzire, apă, canalizare, electrice și ventilație ca scheme pe plan, cu surse, consumatori și traseele dintre ele, și exportă-le ca rețea IFC.
---

Fila **Instalații** desenează instalațiile clădirii așa cum le schițează un inginer: un sistem pentru fiecare serviciu, **sursa** lui (o centrală, un apometru, o coloană, un tablou electric, un ventilator), **consumatorii** (radiatoare, lavoare, WC-uri, prize, corpuri de iluminat, difuzoare) și **traseele** dintre ei. Fiecare nod e un element IFC adevărat, cu porturile lui; fiecare traseu e un segment de conductă, tubulatură sau cablu; conexiunile țin când muți lucrurile.

![Fila Instalații](shots/ribbon-mep.webp)

## Un sistem dintr-un clic

Sub **Sistem nou**, alege serviciul (**Încălzire**, **Apă**, **Apă caldă**, **Canalizare**, **Electrice**, **Ventilație**) și dă clic în plan. Lapin plasează sistemul întreg: sursa unde ai dat clic, un prim consumator la câțiva metri și traseul dintre ei. Sistemul se deschide pentru editare.

![Un sistem de încălzire pe planul parterului: centrala, două radiatoare sub ferestre și traseele dintre ele](shots/mep-plan.webp)

În modul de editare tot ce nu e sistemul e estompat și doar nodurile și traseele lui pot fi alese. Trage sursa și consumatorul unde trebuie: traseul lor urmează. Adaugă consumatori cu **+ Radiator**, **+ Lavoar**… din panoul de proprietăți, sau cu uneltele de sub **Încălzire**, **Sanitare**, **Electrice** și **Aer**: fiecare consumator nou se leagă la sistem printr-un traseu propriu, de la cel mai apropiat port liber al sursei, sau de la cel mai apropiat traseu (un teu). **Gata** (sau <kbd>Esc</kbd>) iese din editare și îți spune câte coturi și teuri face sistemul și ce a rămas liber. **Editează sistemul** (<kbd>ES</kbd>) îl deschide din nou.

## Trasee

**Traseu** (<kbd>RN</kbd>) desenează un traseu punct cu punct; apasă <kbd>Enter</kbd> ca să termini. Un capăt care cade pe un port liber se conectează la el; un capăt care cade pe un traseu al aceluiași sistem se ramifică din el. Selectează un traseu ca să-l modelezi: trage un punct, trage mijlocul unui tronson ca să-l muți, Ctrl-clic pe un punct îl scoate, pe mijlocul unui tronson adaugă unul; **Adaugă punct** pune unul unde dai clic.

**Conectează** leagă capătul unui traseu la un nod sau la alt traseu; **Deconectează** îl eliberează. **Porturi libere** selectează tot ce e încă neconectat.

## Noduri

Fiecare nod are un **rol**: centrală, radiator, distribuitor, apometru, lavoar, chiuvetă, WC, duș, cadă, coloană, piesă de curățire, tablou electric, priză, corp de iluminat, întrerupător, ventilator, difuzor, grilă de evacuare, robinet, urcare/coborâre, capac. Rolul îi dă clasa IFC, porturile, dimensiunea și simbolul pe plan. Un robinet, un apometru sau un întrerupător lăsat pe un traseu îl taie în două și stă la mijloc. O **urcare/coborâre** duce un traseu la altă cotă sau la nivelul de deasupra.

![Proprietățile unui radiator: sistemul, rolul și porturile, dimensiunea și puterea](shots/mep-radiator-properties.webp)

În panoul de proprietăți un nod arată sistemul, rolul și porturile lui (libere sau conectate); un traseu arată cele două capete. Amândouă iau valorile implicite ale sistemului dacă nu le dai o valoare proprie: dimensiune, cotă, planul în care merg (pardoseală, tavan, perete), pereche sau simplu, material, pantă.

## Sisteme

**Sisteme…** arată sistemele proiectului și valorile implicite ale celui curent: dimensiunea, planul și cota fiecărui plan, panta (canalizare), dacă traseele se desenează în pereche (încălzire: tur și retur) și la ce distanță, materialul și izolația. Schimbă o valoare implicită și fiecare nod și traseu fără o valoare proprie urmează.

![Dialogul Sisteme](shots/mep-systems.webp)

## Planuri, liste și IFC

**Plan instalații** face un plan al disciplinei MEP pentru nivel, pus în setul MEP din navigator. **Lista de echipamente** și **lista de trasee** de sub **Sistem** sunt tabele ca oricare altele: merg pe planșe și se exportă cu ele.

La exportul IFC fiecare sistem e un IfcDistributionSystem, fiecare nod și traseu își are porturile IfcDistributionPort, iar conexiunile se scriu între porturi, așa că rețeaua ajunge în alt program conectată de la sursă la ultimul consumator.

## Instalații Pro

Ingineria pe rețea — cererea pe care o duce fiecare traseu și dimensiunea care o preia, verificările (capete libere, coturi strânse, pante de canalizare, surse supraîncărcate), regulile de pozare pe material (cupru în pași de 45°, PVC cu coturi de cel mult 45°), radiatoarele propuse sub ferestre, traseele sugerate ocolind pereții, profilul unui traseu și izometria unui sistem — e în ediția **Lapin MEP**, care adaugă grupurile **Inginerie** și **Asistenți** în această filă.
