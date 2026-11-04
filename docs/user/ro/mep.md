---
title: Instalații
order: 57
summary: Desenează instalațiile de încălzire, apă, canalizare, electrice, ventilație, gaze și motorină ca scheme pe plan, cu surse, consumatori și traseele dintre ele, alimentarea cu gaz din stradă, și exportă-le ca rețea IFC.
---

Fila **Instalații** desenează instalațiile clădirii așa cum le schițează un inginer: un sistem pentru fiecare serviciu, **sursa** lui (o centrală, un apometru, o coloană, un tablou electric, un ventilator), **consumatorii** (radiatoare, lavoare, WC-uri, prize, corpuri de iluminat, difuzoare) și **traseele** dintre ei. Fiecare nod e un element IFC adevărat, cu porturile lui; fiecare traseu e un segment de conductă, tubulatură sau cablu; conexiunile țin când muți lucrurile.

![Fila Instalații](shots/ribbon-mep.webp)

## Un sistem dintr-un clic

Sub **Sistem nou**, alege serviciul (**Încălzire**, **Apă**, **Apă caldă**, **Canalizare**, **Electrice**, **Ventilație**) și dă clic în plan. Lapin plasează sistemul întreg: sursa unde ai dat clic, un prim consumator la câțiva metri și traseul dintre ei. Sistemul se deschide pentru editare.

![Un sistem de încălzire pe planul parterului: centrala, două radiatoare sub ferestre și traseele dintre ele](shots/mep-plan.webp)

În modul de editare tot ce nu e sistemul e estompat și doar nodurile și traseele lui pot fi alese. Trage sursa și consumatorul unde trebuie: traseul lor urmează. Adaugă consumatori cu **+ Radiator**, **+ Lavoar**… din panoul de proprietăți, sau cu uneltele de sub **Încălzire**, **Sanitare**, **Electrice** și **Aer**: fiecare consumator nou se leagă la sistem printr-un traseu propriu, de la cel mai apropiat port liber al sursei, sau de la cel mai apropiat traseu (un teu). **Gata** (sau <kbd>Esc</kbd>) iese din editare și îți spune câte coturi și teuri face sistemul și ce a rămas liber. **Editează sistemul** (<kbd>ES</kbd>) îl deschide din nou.

## Trasee

**Traseu** (<kbd>RN</kbd>) desenează un traseu punct cu punct; apasă <kbd>Enter</kbd> ca să termini. Un capăt care cade pe un port liber se conectează la el; un capăt care cade pe un traseu al aceluiași sistem se ramifică din el.

**Conectează** leagă capătul unui traseu la un nod sau la alt traseu; **Deconectează** îl eliberează. **Porturi libere** selectează tot ce e încă neconectat.

## Modelarea unui traseu

Selectează un traseu: punctele lui stau pe linie așa cum e desenată, chiar și după ce muți nodurile pe care le leagă.

- **Trage un punct** ca să muți colțul acela liber.
- **Trage mijlocul unui tronson** ca să-l muți lateral; tronsoanele vecine se lungesc, așa că un traseu în unghi drept rămâne în unghi drept. Dacă muți primul sau ultimul tronson, Lapin face acolo un decroș, iar capătul rămâne pe port.
- **Ctrl-clic** pe un punct îl scoate, pe mijlocul unui tronson adaugă unul; **Adaugă punct** pune unul unde dai clic.
- **Ocolire** (<kbd>DU</kbd>) duce un traseu pe lângă ceva care îi stă în cale: dă clic pe traseu unde începe ocolirea, apoi unde se termină, pe același tronson, apoi mergi în partea în care iese și dă clic (sau scrie distanța). Traseul iese, merge paralel și revine, cu patru colțuri noi; conexiunile rămân.
- **Redesenează traseul** (<kbd>RP</kbd>) dă unui traseu un drum nou: selectează-l (sau începe pe el) și desenează drumul punct cu punct, <kbd>Enter</kbd> ca să termini. Traseul își păstrează sistemul, dimensiunea și conexiunile, iar capetele revin pe porturi.

![Traseul unui radiator dus pe lângă un obstacol cu Ocolire: iese, merge paralel și revine](shots/mep-detour.webp)


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

## Gaze și motorină

**Gaz** și **Motorină** sunt servicii separate sub **Sistem nou**. Un sistem de gaz își spune gazul (**Gaz natural** sau **GPL**) și **Treapta de presiune** (joasă în casă, medie pe stradă) în **Sisteme…**; țevile lui sunt implicit din oțel negru, pe pereți, la 2,1 m, și pot fi din cupru, multistrat sau **PE 100** pentru ce se îngroapă. Motorina merge prin cupru.

Instalația de gaz a unei case pornește din **firida de branșament** de pe fațadă. Apasă **Sistem nou → Gaz**: firida se pune unde dai clic, iar o **centrală pe gaz** înăuntru, legată printr-un traseu. Pe drum, uneltele din **Gaz** pun pe traseu **regulatorul de gaz**, **contorul de gaz**, **robinetul general de gaz**, **robinetele de consumator** și **racordurile flexibile**, care îl taie ca orice fiting; **aragazul** și **încălzitorul de apă pe gaz** se leagă de instalație printr-un traseu propriu. Unde o țeavă de gaz sau de motorină trece printr-un perete, trece printr-un **tub de protecție**, desenat și numărat cu traseul. O centrală pe gaz desenată în sistemul de încălzire ca sursă se leagă de instalația de gaz din proprietățile ei (butonul **+** cu numele sistemului de gaz).

![Instalația de gaz a unei case: firida, regulatorul și contorul afară, centrala și aragazul în bucătărie](shots/mep-gas-plan.webp)

Fiecare aparat își păstrează **consumul** (m³/h) și **tipul aparatului** după evacuarea gazelor arse (A fără coș, B cu coș, C etanș), pentru liste și dimensionare.

**Motorină** pune un **rezervor de motorină** (interior, exterior sau îngropat) și o **centrală pe motorină**; **racordul de umplere** și **căciula de aerisire** se leagă de umplerea și aerisirea rezervorului, iar **filtrul de motorină** stă pe tur.

### Alimentarea din stradă

Din **Stradă**, **Conductă de gaz de-a lungul…** (<kbd>GM</kbd>) pozează o conductă de gaz de-a lungul a ce alegi — un drum, un aliniament, o linie desenată, un perete — la **distanța față de linie** și **adâncimea (acoperirea)** din bara liniei de comandă; sub un teren adâncimea se ia de la sol, altfel de la nivel. **Conductă de gaz** o desenează punct cu punct. Conducta intră într-un sistem de gaz de presiune medie, din țeavă de PE, cu **banda de avertizare**.

**Branșament** (<kbd>GS</kbd>): alege conducta, apoi firida. Lapin pune un **teu de branșament** pe conductă, pozează țeava de branșament perpendicular pe ea la adâncimea ei și mai departe până la firidă, și urcă în firidă acolo.

![Branșamentul de la conducta din stradă la firidă, selectat](shots/mep-gas-street.webp)

![Conducta și branșamentul sub pământ, instalația pe fațadă](shots/mep-gas-3d.webp)

![Proprietățile unui branșament: de la teul de branșament la firidă, acoperirea și diametrul](shots/mep-gas-service-properties.webp)

![Valorile implicite ale unui sistem de gaz: gazul și treapta de presiune](shots/mep-gas-systems.webp)

**Antemăsurătoare gaze și motorină** din **Sistem** listează, sistem cu sistem, țevile pe material și diametru cu lungimea lor (și din ea coloanele), coturile, teurile și tuburile de protecție, banda de avertizare, robinetele, contoarele și firidele, și aparatele cu consumul și puterea instalate. În IFC un sistem de gaz este un IfcDistributionSystem de tip GAS, iar unul de motorină FUEL; firida este un IfcDistributionChamberElement, contorul un IfcFlowMeter GASMETER, robinetele IfcValve, aragazul un IfcBurner, rezervorul un IfcTank.

![Antemăsurătoarea de gaze și motorină](shots/mep-gas-list.webp)

Dimensionarea țevilor după consumul aparatelor și verificarea căderii de presiune țin de inginerie, în ediția **Lapin MEP**.

## Instalații Pro

Ingineria pe rețea — cererea pe care o duce fiecare traseu și dimensiunea care o preia, verificările (capete libere, coturi strânse, pante de canalizare, surse supraîncărcate), regulile de pozare pe material (cupru în pași de 45°, PVC cu coturi de cel mult 45°), radiatoarele propuse sub ferestre, traseele sugerate ocolind pereții, profilul unui traseu și izometria unui sistem — e în ediția **Lapin MEP**, care adaugă grupurile **Inginerie** și **Asistenți** în această filă.
