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

## Protecție la incendiu

**Semnalizare incendiu** și **Hidranți** sunt servicii separate sub **Sistem nou**, cu uneltele lor în grupurile **Semnalizare incendiu** și **Hidranți**.

**Sistem nou → Semnalizare incendiu** pune **centrala de detectare și semnalizare incendiu** unde dai clic și un prim **detector de fum** lângă ea, legate prin cablul buclei. Fiecare dispozitiv pus după aceea (**Detector de fum** <kbd>SD</kbd>, **Detector de temperatură** <kbd>HD</kbd>, **Declanșator manual de alarmă** <kbd>MC</kbd>, **Sirenă**, **Sirenă cu flash**) se leagă la capătul liber al buclei, adică la dispozitivul pus înaintea lui. Cablul merge de la un dispozitiv la următorul în ordinea în care le pui. **Închide bucla** aduce cablul de la ultimul dispozitiv înapoi la returul centralei, așa că bucla e alimentată de la ambele capete. Cablul este un cablu rezistent la foc, pe tavan.

![O buclă de semnalizare incendiu: centrala lângă intrare, detectoarele cu acoperirea lor, un declanșator manual și o sirenă, bucla închisă înapoi la centrală](shots/mep-fire-plan.webp)

Fiecare detector își arată **acoperirea** ca un cerc punctat. Implicit raza vine din tabelul de proiectare pentru un tavan plan de până la 6 m: 5,8 m pentru un detector de fum, 3,6 m pentru un detector de temperatură. Dă unui detector propria **Raza de acoperire** în proprietățile lui când încăperea, înălțimea ei sau tavanul cer altă valoare.

**Sistem nou → Hidranți** pune un **grup de pompare pentru incendiu** și un prim **hidrant interior** legat de el printr-o țeavă din oțel zincat (implicit DN 50). Fiecare hidrant pus după el se leagă de rețea printr-un teu. **Rezerva intangibilă de apă** alimentează pompa prin racordul de aspirație al pompei, iar **racordul pentru autospecialele de pompieri** se leagă de mână cu **Traseu**. **Hidrant exterior** și **Sprinkler** completează rețeaua. Un hidrant interior își desenează raza de acțiune ca un cerc punctat: **Lungimea furtunului** (implicit 20 m) plus **Jet compact** (6 m). Pune-le pe amândouă cum cere normativul pentru clădire.

![Hidranți interiori alimentați din grupul de pompare, cu raza lor de acțiune desenată pe plan](shots/mep-fire-hydrants.webp)

![Bucla de semnalizare incendiu și hidranții în 3D](shots/mep-fire-3d.webp)

**Antemăsurătoare protecție la incendiu** din **Sistem** listează, sistem cu sistem:

- centralele, detectoarele, declanșatoarele manuale și sirenele;
- cablul buclei cu lungimea lui și dacă fiecare buclă e închisă;
- rezerva, grupurile de pompare, hidranții și sprinklerele;
- țevile pe material și diametru, cu coturile și teurile lor.

În IFC ambele sisteme sunt de tip FIREPROTECTION. Centrala este un IfcUnitaryControlElement ALARMPANEL, detectoarele IfcSensor (SMOKESENSOR, HEATSENSOR), declanșatoarele manuale și sirenele IfcAlarm, hidranții și sprinklerele IfcFireSuppressionTerminal, iar cablul buclei un IfcCableSegment.

![Antemăsurătoarea instalațiilor de stingere și semnalizare incendiu](shots/mep-fire-list.webp)

Amplasarea detectoarelor încăpere cu încăpere după tabelul de proiectare și verificarea că fiecare încăpere e acoperită de detectoare și hidranți țin de inginerie, în ediția **Lapin MEP**.

## Ventilatoare de evacuare

Grupul **Ventilatoare** din fila Instalații pune ventilatoare de baie și de bucătărie ca elemente ale lor, fiecare de un tip pe care îl configurezi:

- **Ventilator de perete** (<kbd>VF</kbd>): un ventilator axial pe perete, care suflă direct prin el;
- **Ventilator de tavan**: un ventilator sub tavan, cu un tub care iese din lateral;
- **Ventilator de tubulatură**: un ventilator în tub, în plenul tavanului, pe traseul tubului;
- **Ventilator de geam**: un ventilator montat în geamul unei ferestre;
- **Ventilator de tavan cu lumină**: un ventilator de tavan cu o lumină în grilă;
- **Ventilator cu panou cu lamele**: un panou de perete cu lamele lângă ventilator și un capac sub el.

Alege o unealtă și dă clic. Un ventilator de perete, de geam sau cu panou se pune pe fața peretelui cea mai apropiată de clic (până la 600 mm), cu fața spre încăpere și mijlocul la 2,1 m peste pardoseală (cel de geam la 1,9 m). Un ventilator de tavan se pune unde dai clic, sub tavanul încăperii; unul de tubulatură cu 250 mm deasupra lui. Bara liniei de comandă alege tipul pe care îl pune unealta, dintre tipurile proiectului sau cele incluse în Lapin (un ventilator de perete Ø100 cu senzor de umiditate, unul de tavan cu lumină și senzor de mișcare, unul de tubulatură pentru două băi…).

Un ventilator de perete taie prin perete un gol rotund, cu 20 mm mai larg decât tubul lui: golul prevăzut al peretelui, ca al unui traseu de instalații (apare ca volum de gol în planuri și intră în IFC ca gol al peretelui). E găsit după locul în care stă ventilatorul, deci un ventilator mutat de-a lungul peretelui își ia golul cu el. Un ventilator de geam stă în geam și nu taie nimic.

![O baie cu un ventilator de perete, un ventilator de tavan cu lumină scos prin perete și un ventilator cu panou cu lamele](shots/fans-plan.webp)

Planurile desenează ventilatorul prin simbolul lui de instalații: un cerc cu palele, fața pe perete, manșonul punctat prin perete și o săgeată în sensul aerului; grila unui ventilator de tavan e punctată, fiind deasupra secționării. Planurile de tavan, secțiunile, fațadele și 3D-ul desenează corpurile.

**Tub spre gura de evacuare** scoate aerul unui ventilator de tavan sau de tubulatură: dă clic pe ventilator, apoi unde iese tubul. Tubul e un traseu al sistemului **Bathroom extract** al proiectului (făcut odată cu primul tub; taie pereții prin care trece), cât tubul ventilatorului de larg, la înălțimea racordului. Se termină cu o gură pe fața exterioară a peretelui sau, cu **Prin acoperiș** pornit în bară, cu o țeavă care urcă la o căciulă. Mutat sau ridicat, ventilatorul ia începutul tubului cu el.

![Ventilatoarele în 3D: tubul până la gura de afară, grila ventilatorului de perete pe fațadă](shots/fans-3d.webp)

### Tipurile de ventilatoare

**Tipuri de ventilatoare** deschide tipurile: ale proiectului și cele incluse, de adăugat cu un clic. Un tip stabilește:

- **montajul** (cele pe care le ia felul lui) și **diametrul tubului** (Ø100, 125, 150 sau 200 mm);
- **forma** grilei (rotundă sau pătrată), **mărimea**, **modelul** (lamele, fante, inele sau simplă) și **finisajul** (alb, argintiu, negru, inox sau crom);
- **debitul de aer** (m³/h) și **puterea** (W);
- **comanda**: întrerupător, senzor de umiditate, temporizator, amândouă sau senzor de mișcare;
- o **clapetă antiretur**, iar **lumina** unui ventilator de tavan, lungimea corpului unuia de tubulatură, lățimea și înălțimea unui panou cu lamele apar doar unde se aplică.

Previzualizarea de alături e a motorului: simbolul din plan sus, fața jos. Fiecare ventilator de un tip urmează o schimbare.

![Tipurile de ventilatoare, cu previzualizarea unui panou cu lamele](shots/fans-types.webp)

Selectează un ventilator ca să-i schimbi tipul, **înălțimea peste pardoseală**, rotația și, după ce are tub, **gura de evacuare** (perete sau acoperiș) și cât urcă ea deasupra tubului. Trage de mânerul din mijloc ca să-l muți, de cel din fața lui ca să-l rotești.

![Proprietățile unui ventilator de tavan](shots/fans-properties.webp)

### Extrase, cost și IFC

**Extrasul de ventilatoare** arată fiecare ventilator cu încăperea, felul, mărimea, debitul, montajul, comanda, înălțimea și gura de evacuare, plus debitul total. **Verificarea evacuării pe încăperi** arată fiecare baie, WC și bucătărie (și fiecare încăpere cu ventilator) cu volumul, debitul necesar și cât dau ventilatoarele ei: o baie 8 schimburi de aer pe oră și cel puțin 54 m³/h, un WC 6 și 25 m³/h, o bucătărie 6 și 108 m³/h. Sunt valori orientative; verifică-le după normativul după care proiectezi.

Un tip nou are articolul de deviz `LAP-FAN`, numărat pe bucată (Cost → Map articles… îl schimbă).

IFC scrie un ventilator ca **IfcFan** al unui **IfcFanType**, cu felul lui predefinit (PROPELLORAXIAL pentru unul de perete sau de geam, CENTRIFUGALRADIAL pentru unul de tavan, TUBEAXIAL pentru unul de tubulatură) și **Pset_FanTypeCommon** (NominalAirFlowRate, NominalPowerRate); golul din perete ca IfcOpeningElement. Lapin le citește înapoi ca ventilatoarele lui.

## Coordonare: volume de gol și verificarea coliziunilor

Fiecare gol dintr-un perete, o placă sau un acoperiș poate fi un **volum de gol** al lui: un solid cât golul, prin toată grosimea elementului-gazdă, cu finisaje cu tot. Programele de coliziuni (Navisworks, Solibri, BIMcollab) verifică instalațiile față de astfel de volume cu „complet în interior”: o țeavă, o tubulatură sau un jgheab de cabluri aflat în întregime într-un volum trece; unul care iese din el, sau care trece printr-un perete unde nu e niciun gol, e o coliziune. Lapin face aceeași verificare singur.

**Gestionare → Verificare coliziuni…** rulează verificarea doar la cerere: nimic nu o pornește singur, nici o modificare, nici **Gestionare → Verificare**. Fila **Setări** stabilește pentru ce goluri se fac volume (goluri în pereți, de uși, de ferestre, în plăci și în acoperiș), cu cât sunt mai mari sau mai mici decât golurile lor (**Decalajul golului**) și **Distanța liberă** pe care un element de instalații trebuie s-o păstreze față de laturile unui gol. **Rulează verificarea coliziunilor** trece o dată prin fiecare țeavă, tubulatură și jgheab de cabluri — traseele Lapin și segmentele unui model de instalații importat deopotrivă, și golurile prevăzute ale unui model de instalații — și păstrează ce a găsit ca **raportul de coliziuni** al proiectului, cu data rulării, până rulează din nou. Când modelul se schimbă după aceea, raportul spune că e **învechit**. Problemele lui sunt grupate pe feluri și numerotate, fiecare număr păstrat de la o rulare la alta:

- **Fără gol**: trece prin gazdă unde aceasta nu are gol;
- **Netăiate: portante sau ordinea de tăiere**: fără gol, pentru că golurile proprii ale traseelor (mai jos) nu taie acea gazdă;
- **Gol prea mic**: trece printr-un gol dar iese din el, cu distanța liberă inclusă; **Lipsă** spune cu câți milimetri pe partea cea mai rea;
- **Gol neocupat** (cu **Goluri neocupate** bifat în **Setări**): un gol din perete sau placă prin care nu trece nimic (un gol de scară nu e niciodată unul).

![Raportul de coliziuni: fără gol, un gol prea mic și un gol neocupat, fiecare numerotat](shots/mep-voids-check.webp)

Fiecare problemă e **De corectat**, **Rezolvată** sau **Ignorată**, cu un comentariu. Un clic pe un rând arată perechea în 3D, amândouă selectate (**singure în 3D** ascunde restul pentru moment); săgețile duc la cea anterioară și la cea următoare. **Marchează în vedere** (și **Vedere → Marcaje coliziuni**) desenează fiecare problemă deschisă în planul, secțiunea sau fațada din față ca un nor roșu cu numărul ei, doar pe ecran.

![Planul cu coliziunile marcate: nori roșii cu numerele lor](shots/mep-voids-clouds.webp)

**Fă gol** taie un gol simplu în jurul elementului, în perete (rotund pentru o țeavă sau un cablu, dreptunghiular pentru o tubulatură) sau în placă, cu laturile la distanța liberă și dimensiunea rotunjită în sus cum spune regula de instalații din ordinea de tăiere; **Mărește golul** mărește golul prea mic. **Corectează selecția** face asta pentru fiecare rând bifat, **Ignoră** și **Redeschide** le schimbă starea; fiecare e un singur pas de anulat. Raportul spune apoi că e învechit: rulează-l din nou, iar ce s-a corectat apare **Rezolvată** (**arată rezolvatele**). Rândul ei spune atunci la ce rulare a fost găsită rezolvată și cum e acum: golul prin care trece și dimensiunea traseului în el (**Resolved at the run of 04.11.2026 15:20: opening V1 200 mm, run 160 mm.**), sau că traseul nu mai trece; fiecare rulare de după aduce dimensiunile la zi.

![Rulat din nou după corecturi: cele două coliziuni rezolvate](shots/mep-voids-fixed.webp)

**BCF** face din coliziunile deschise observații de tip **Clash** cu punctele lor de vedere (**Gestionare → Observații pe model…**) și le salvează ca fișier BCF; rulat din nou, actualizează aceleași observații. **CSV** și **PDF** salvează raportul ca listă. **Gestionare → Verificare** doar citește raportul: coliziunile lui deschise sunt avertismente, la fel și un raport învechit.

### Trasee care își taie singure golurile

Ordinea de tăiere (**Gestionare → Ordinea de tăiere**) cuprinde și traseele de instalații, deasupra pereților, plăcilor și acoperișurilor. Cu **Traseele taie pereți, plăci și acoperișuri** pornit, un traseu care trece printr-un perete, o placă sau un acoperiș își taie acolo golul lui: rotund pentru o țeavă sau un cablu care trece drept, dreptunghiular pentru o tubulatură sau un traseu oblic, cât exteriorul traseului (izolația, tubul de protecție al unei țevi de gaz) plus **Distanța liberă** în jur, rotunjit în sus la pas, niciodată sub **Cel mai mic gol**. Golul nu e ținut separat: se mută și crește odată cu traseul și dispare când traseul dispare sau nu mai trece. E un volum de gol ca oricare (desenat, listat, verificat), iar exportul IFC îl scrie ca **IfcOpeningElement** cu **Lapin_ProvisionOpening** (și ca gol prevăzut cu **Volume de gol** bifat), așa că o verificare rulată din nou nu mai găsește nimic de raportat. Un perete, o placă sau un acoperiș pus deasupra traseelor în ordinea de tăiere (după clasă sau după material) nu se taie, nici una portantă (LoadBearing în Pset_…Common) dacă **taie-le și pe ele** nu e bifat: verificarea coliziunilor le raportează ca **Netăiate**. Un traseu demolat nu taie nimic, iar unde un traseu trece deja printr-un gol nu se mai taie altul. Un sistem poate hotărî altfel în **Instalații → Sisteme…** (**Traseele taie pereți, plăci și acoperișuri**: ca ordinea de tăiere, da, nu). E pornit într-un proiect nou; un proiect făcut înainte îl ține oprit până e pornit.

Într-un perete curb golul se găsește unde traseul întâlnește arcul, cu lățimea măsurată de-a lungul lui; unul dreptunghiular are laturile radiale, ca al unei uși sau ferestre. Într-un acoperiș se taie în panta prin care trece traseul: vertical pentru o țeavă care stă în picioare (o aerisire), alungit pe direcția traseului pentru una oblică, ca într-o placă; panele și coama de sub pantă nu se taie, iar verificarea coliziunilor raportează un traseu prin ele. Mutarea sau redimensionarea unui traseu reconstruiește doar pereții, plăcile și acoperișurile pe care le traversa sau le traversează acum, oricât de mare e modelul. IFC-ul propriu al lui Lapin, citit înapoi, nu mai ia aceste goluri când vin și traseele lor (traseele le taie din nou, niciodată de două ori); fără traseul lor (lăsat în afara importului, sau un proiect ale cărui trasee nu taie nimic) fiecare intră ca gol al gazdei lui, așa că nu se pierde nimic.

![Traseele de instalații în ordinea de tăiere](shots/mep-voids-cut-order.webp)

Fila **Volume** listează fiecare volum cu marca lui (V1, V2… etaj cu etaj), gazda, ușa sau fereastra din el, dimensiunea și adâncimea; un clic îi selectează golul.

![Volumele listate cu mărcile și dimensiunile lor](shots/mep-voids-list.webp)

**Vedere → Volume de gol** cu vederea 3D în față le desenează în 3D, violet transparent, cu mărcile lor; un clic pe unul îi selectează golul.

![Volumele din peretele interior, cu țevile prin ele](shots/mep-voids-3d.webp)

Cu un plan, o secțiune sau o fațadă în față, **Vedere → Volume de gol** le desenează în acea vedere, cum arată un plan de coordonare golurile prevăzute: secționate unde vederea le taie (golul din perete pe toată grosimea peretelui, cel din placă prin placă), văzute unde le vede (un gol din placă sub planul de secționare, un gol din perete pe fața spre care privește fațada), punctate când un gol din perete e sub secționare sau unul din placă deasupra ei; fiecare cu diagonale, cu marca și, sub ea, dimensiunea și cota de jos a unui gol din perete (**400×200 +0.90**, **Ø200**). Le desenează motorul, odată cu restul desenului, așa că planșele, PDF-ul și DXF-ul (layerul **VOIDS**) le tipăresc cum le arată ecranul.

![Planul cu volumele de gol: secționate prin pereți, golul din placă văzut, fiecare cu diagonale și marcă](shots/mep-voids-plan.webp)

Eticheta unui gol stă lângă el, de-a curmezișul peretelui, unde nu e deja desenat altceva: deschiderea unei uși, marcajul ușii sau al ferestrei, alte etichete. Când ambele părți ale peretelui sunt ocupate, se mută de-a lungul peretelui, după gol, altfel mai departe.

![Golul unei uși: eticheta lui ferită de deschiderea ușii și de marcajul ei](shots/void-label.webp)

Ca să muți o etichetă, dă clic pe ea (sau pe gol): asta selectează golul, a cărui etichetă are un mâner pătrat ca s-o muți și unul rotund ca s-o rotești. Ctrl/⌘-clic pe un mâner pune eticheta înapoi unde stă implicit, sau din nou orizontală. O etichetă mutată e păstrată de acea vedere, de-a lungul și de-a curmezișul peretelui față de mijlocul golului, așa că urmează golul când se mută peretele; planșele și PDF-ul o tipăresc unde ai pus-o.

![Eticheta golului mutată de mânerul ei](shots/void-label-moved.webp)

E o setare a vederii, și în proprietățile ei (**Vedere → Setările vederii**): **Volume de gol**, și **Dimensiunile golurilor** pentru dimensiunea de sub marcă. Implicit nicio vedere nu le desenează; stilul de vedere **Coordonare** (**Gestionare → Stiluri de vedere**, ales ca **Stil** al vederii) le desenează în fiecare vedere care îl ia. Liniile lor sunt categoria **Volume**: creioanele și culorile în **Gestionare → Stiluri de obiecte** (linii secționate, în proiecție, ascunse, deasupra), arătate sau ascunse pe vedere în **Vedere → Vizibilitate / Grafică…**.

Ca să dai volumele unui program de coliziuni, bifează **Volume de gol** în **Fișier → Export IFC sau Fragments…** (sau **Export 3D…**). În IFC fiecare volum e un **IfcBuildingElementProxy** de tip **PROVISIONFORVOID** cu **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter pentru unul rotund, Depth) și setul Lapin **Lapin_OpeningVoid** (marca, felul, gazda, golul și decalajul), legat de gazda lui; golurile înseși rămân IfcOpeningElement ca întotdeauna. Fiecare volum își păstrează GlobalId-ul de la un export la altul, așa că programul de coliziuni îl urmărește. Într-un export de suprafețe 3D ele sunt un grup aparte, **Opening voids**. Fără bifă, exporturile sunt ca înainte.

## Instalații Pro

Ingineria pe rețea — cererea pe care o duce fiecare traseu și dimensiunea care o preia, verificările (capete libere, coturi strânse, pante de canalizare, surse supraîncărcate), regulile de pozare pe material (cupru în pași de 45°, PVC cu coturi de cel mult 45°), radiatoarele propuse sub ferestre, traseele sugerate ocolind pereții, profilul unui traseu și izometria unui sistem — e în ediția **Lapin MEP**, care adaugă grupurile **Inginerie** și **Asistenți** în această filă.
