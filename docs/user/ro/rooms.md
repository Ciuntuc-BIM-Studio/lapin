---
title: Încăperi și culorile lor
order: 45
summary: Pui încăperi cu un clic între pereți, le dai nume și numere și le colorezi sau hașurezi pe plan după funcțiune sau departament, cu o legendă.
---

## Cum pui o încăpere

Alege **Arhitectură → Încăpere** (<kbd>RM</kbd>) și dă clic într-un spațiu închis de pereți. Lapin găsește conturul încăperii între fețele pereților, ale pereților cortină și ale stâlpilor din jurul punctului. Ușile nu deschid încăperea: conturul se închide peste ele. Un stâlp din încăpere nu intră în aria ei.

Încăperea se ține după pereți. Muți un perete și încăperea crește sau scade odată cu el. Dacă un perete e șters și încăperea nu mai e închisă de nimic, păstrează conturul pe care îl avea, iar eticheta ei se face roșie. **Arhitectură → Actualizează încăperile** salvează conturul pe care îl găsesc încăperile acum.

Pentru un spațiu care nu e închis de pereți peste tot, cum e o terasă sau un capăt al unui spațiu deschis, folosește **Încăpere după contur** și dă clic în colțurile lui. Încăperea aceasta păstrează conturul pe care îl desenezi.

![Un plan cu două încăperi colorate după funcțiune și legenda culorilor](shots/rooms-plan.webp)

## Linii de separare a încăperilor

O bucătărie deschisă spre living, cele două capete ale unui hol, palierul unei galerii: spații fără perete între ele care trebuie totuși să fie încăperi separate. **Arhitectură → Separare încăperi** (<kbd>RS</kbd> pentru o linie) desenează o linie care mărginește încăperile cum ar face-o un perete, fără corp: o **linie** (înlănțuită, fiecare clic e începutul următoarei), o **polilinie** (colțurile ei, apoi <kbd>Enter</kbd>), un **dreptunghi** (două colțuri opuse: zona din interior devine o încăpere a ei), un **arc** (începutul, sfârșitul, un punct prin care trece) sau un **cerc** (centrul, un punct pe el). Desenează-o de la perete la perete; un capăt care se oprește la un fir de fața peretelui se închide totuși pe ea.

![O bucătărie deschisă spre living, despărțite de o linie de separare, fiecare încăpere cu aria ei](shots/room-dividers.webp)

Încăperile din jur își găsesc imediat conturul din nou, fără **Actualizează încăperile**: o încăpere de 6 × 4 m tăiată de o linie pe mijloc devine două încăperi de 3 × 4 m, fiecare cu aria, eticheta și finisajul ei; pune o încăpere de cealaltă parte cu **Încăpere**. Muți linia (o desenezi din nou) sau o ștergi și încăperile urmează: ștearsă, încăperea e din nou întreagă. Încăperile de o parte și de alta se întâlnesc exact pe linie, așa că ariile lor se adună la încăperea întreagă. O linie care se oprește în mijlocul unei încăperi nu mărginește nimic și nu schimbă nimic.

În planurile nivelului ei se desenează ca o linie subțire întreruptă; nu are corp, așa că vederea 3D, secțiunile și cantitățile clădirii o lasă deoparte (lungimea ei e în cantitățile ei). Și desenul 2D devine linii de separare: **Desen → Convertește → Convertește în…**, **Room separation lines**. În IFC este un **IfcVirtualElement** (BOUNDARY în IFC 4.3) cu linia ca o curbă FootPrint, pe care Lapin o citește înapoi ca linie de separare.

## Proprietățile unei încăperi

Selectezi o încăpere cu un clic pe eticheta ei sau oriunde în interiorul ei. În panoul de proprietăți:

- **Nume** și **Număr.** Încăperile noi sunt numerotate pe niveluri: 101, 102… la parter. Numele încăperii e cel al funcțiunii până îi dai unul al tău; golit, revine la funcțiune.
- **Funcțiune**, **Departament** și **Finisajul pardoselii.** Scrii ce vrei sau alegi o valoare pe care o au deja alte încăperi.
  Lăsat gol, **Finisajul pardoselii** unei încăperi finisate este pardoseala finisajului ei (materialul stratului de sus, arătat estompat în câmp): eticheta, formulele și documentele ei citesc `{finishFloor}` astfel până scrii unul al tău.
- **Înălțime.** Gol înseamnă înălțimea nivelului.
- **Culoare proprie** și **Hașură proprie** înlocuiesc schema de culori doar pentru această încăpere.

Cantitățile de dedesubt arată aria netă a încăperii (fără stâlpi; cu un finisaj, la finisaj: vezi [Finisajele încăperilor](#finisajele-incaperilor)), perimetrul și volumul ei. Ca să muți eticheta, trage de punctul de pe ea.

![Proprietățile unei încăperi](shots/room-properties.webp)

## Uși și ferestre din încăpere

Poți alege ușile și ferestrele unei încăperi chiar din încăpere. Bifează **Uși și ferestre din încăpere** în proprietățile ei; implicit e oprită. Un plan mic numerotează laturile încăperii. **Adaugă ușă** sau **Adaugă fereastră** adaugă un rând în care alegi tipul, latura și **Offset**-ul față de mijlocul laturii, iar la fereastră și **Parapetul**.

Fiecare rând face o ușă sau o fereastră obișnuită în peretele de pe acea latură: se selectează, se întoarce, intră în extrase și se exportă ca oricare alta, iar așezarea ușilor pe pereți merge ca înainte. Ușa se deschide spre încăpere. Dacă debifezi opțiunea, încăperea își scoate ușile și ferestrele și păstrează lista; dacă o bifezi din nou, revin. Deocamdată merg doar pe pereți drepți.

Într-un [tip de apartament](groups.md), aceste uși și ferestre merg cu camera: fiecare instanță le are pe ale ei în peretele de pe latura aceea, fie că peretele ține de apartament, fie de clădire. Unde niciun perete nu poate primi una, rândul ei îți spune.

![Ușile și ferestrele unei încăperi, după tip și latură](shots/room-openings.webp)

![Ușa și fereastra în plan: ușa se deschide spre încăpere](shots/room-openings-plan.webp)

## Eticheta încăperii

**Arhitectură → Eticheta încăperii** (sau **Eticheta încăperii…** în proprietățile unei încăperi) alege ce arată eticheta fiecărei încăperi în planuri, rând cu rând: numele, numărul, funcțiunea, departamentul, aria, aria brută, perimetrul, înălțimea, volumul sau finisajul pardoselii, ori un text al tău. Primul rând e îngroșat. **Standarde de desen → Etichete** arată aceeași etichetă ca text: `{name}|{number}|{area}`.

![Dialogul Eticheta încăperii](shots/room-tag.webp)

Un rând poate arăta și parametrii proprii ai încăperii, ai nivelului ei și ai proiectului: lista îi oferă la **Parametri**, **Nivelul lui** și **Proiectul** de îndată ce o încăpere îi are. Parametrul unui nivel e citit de toate încăperile de pe el: dă nivelului un parametru *code* = K1, iar rândul `{storey.code}.{number}` al unei încăperi scrie *K1.1*; schimbă-l în K2 și toate etichetele urmează. Un parametru al încăperii poate fi calculat dintr-o formulă din ele (*Room index* = `{storey.code}.{Hall}.{Flat}.{number}`), iar extrasul încăperilor are câte o coloană pentru fiecare parametru al încăperilor: vezi [Parametri calculați prin formule](interface.md#texte-calculate).

**Desenat ca** un simbol al tău, eticheta arată cum ai desenat-o: casete, linii și texte care țin câmpurile (`{storey.code}.{number}`, `{name}`, `{area}`, `{Floor finish}`). Desenează-l în **Simboluri…**, apoi alege-l la **Desenat ca**; **Rândurile de mai jos** te întoarce la rânduri. Vezi [O etichetă desenată ca simbol](drafting.md#o-etichetă-desenată-ca-simbol).

![Eticheta unei încăperi desenată ca simbol propriu](shots/room-tag-symbol.webp)

## Rotunjirea ariilor

Încăperile își arată aria la sutimea de metru pătrat. **Gestionare → Standarde de desen → Proiect → Rotunjirea ariilor** o rotunjește în schimb la **0,1 m²**, **0,5 m²** sau **1 m²**, jumătatea în sus, cum scriu țările nordice ariile încăperilor (12,27 m² devine 12,5 m² la 0,5 și 12 m² la 1). Se aplică oricărei arii de încăpere pe care o arată Lapin: etichetele din planuri și planșe, legenda culorilor și dialogul Culorile încăperilor, extrasul de încăperi, ariile de pardoseală din cantitățile unei încăperi și ariile pe care le completează un document. Fiecare încăpere se rotunjește separat, iar un total adună încăperile rotunjite, așa că coloana extrasului dă totalul ei. Modelul păstrează aria exactă: fișierul IFC, volumele și celelalte cantități nu se rotunjesc niciodată. Într-un proiect în picioare, pasul e în picioare pătrate.

## Culorile încăperilor

**Vedere → Culorile încăperilor** colorează încăperile unui plan. Alege o schemă, alege în **Colorează după** ce anume colorează (**Funcțiune**, **Nume**, **Departament**, **Număr** sau **Finisajul pardoselii**) și alege schema la **Planul acesta arată**. Fiecare valoare pe care o au încăperile primește un rând cu:

- o **Culoare**,
- o **Hașură** din modelele proiectului, cu scara ei (**Scalează**) și culoarea liniilor (**Cerneală**),
- și o previzualizare a felului în care arată.

**Completează culorile lipsă** dă fiecărei valori fără culoare una dintr-o paletă discretă. **Nou** și **Duplică** fac alte scheme, de exemplu una pentru prezentarea la client și una pe departamente.

![Dialogul Culorile încăperilor](shots/room-colors.webp)

Culorile se desenează sub pereți, sub mobilier și sub deschiderile ușilor, așa că planul rămâne ușor de citit. Se tipăresc pe planșe și intră în exporturile PDF și DXF. Un plan nou nu arată culorile încăperilor până nu îi alegi o schemă, în **Culorile încăperilor** sau în proprietățile planului.

## Legenda culorilor

**Adnotări → Legenda culorilor**: dă clic pe plan unde vrei legenda. Ea listează culorile pe care le folosesc încăperile planului, cu numele lor, și urmează orice schimbare a schemei. O muți cu **Mută**.

## Cotele încăperilor

**Annotate → Room dimensions** (<kbd>ID</kbd>) cotează dintr-o dată interiorul încăperilor de pe un plan. Fiecare latură a unei încăperi primește un lanț de cote aliniat, chiar în interiorul ei. Lanțul are câte un punct la fiecare colț al laturii și la golurile fiecărei uși și ferestre din ea.

- Cu încăperi selectate, se cotează doar acelea. Fără selecție, se cotează toate încăperile nivelului planului.
- Cerută pe o planimetrie, care vede tot modelul de sus și nu taie niciun perete, cotarea se face pe planul nivelului. Înălțimea textului unei cote se poate da separat în Proprietăți (**Text height**, mm pe hârtie; gol: cea a stilului de cotare). Înălțimile textelor sunt pe hârtie, deci pe o planimetrie la 1:500 același text e de cinci ori mai mare în model decât pe un plan la 1:100.
- Punctele se țin de pereți, ca la orice cotă. Muți un perete și lanțurile îl urmează.
- Rulează din nou comanda pe o încăpere ca să-i refaci lanțurile, de exemplu după ce ai adăugat o ușă. Lanțurile vechi sunt înlocuite, nu dublate.
- Laturile foarte scurte, sub 30 cm, sunt lăsate deoparte. Orice lanț se editează sau se șterge ca unul desenat de mână.

![Două încăperi cotate în interior: lanțul fiecărei laturi, cu ușa și ferestrele pe el](shots/room-dimensions.webp)

## Finisajele încăperilor

**Interior → Finisează încăperea** (<kbd>FR</kbd>) pune un finisaj în încăperea pe care dai clic: pardoseala, finisajul pereților și tavanul, fiecare urmând conturul încăperii, ca și încăperea. Muți un perete și finisajele merg cu el. **Interior → Finisajele încăperilor** editează finisajele; **Din bibliotecă** aduce unele gata făcute: Living, Dormitor, Baie, Bucătărie, Parter pe placă și Cameră tehnică. Cât unealta e pornită, panoul de proprietăți arată finisajul pe care îl pune, ca să-l alegi înainte de clic. **Finisajul** unei încăperi, în proprietățile ei, pune altul sau îl scoate.

Un finisaj are:

- **Pardoseala**, straturile ei de la cota pardoselii finite în jos (gresie, adeziv, șapă…). Golurile din placă din încăpere (o casă a scării) sunt scoase. **Prin uși, până la axul peretelui** duce pardoseala mai departe pe sub fiecare ușă, ca pardoseala încăperii de alături s-o întâlnească în axul peretelui.
- **Pereții**, straturile lor de la fața peretelui spre încăpere, și în jurul insulelor încăperii. Fiecare strat își poate schimba materialul pe înălțime, ca stratul unui tip de perete: la Baie, vopsea deasupra faianței până la 2100 (13 mm grosime, faianță și adeziv); la Bucătărie, o bandă de faianță de la 900 la 1500. O bucată fără material nu lasă nimic acolo. Ușile și ferestrele pereților din jurul încăperii sunt decupate, iar glafurile lor îmbrăcate cu aceleași straturi, de la fața camerei până la toc (la un gol fără toc, până la axul peretelui); la o fereastră și partea de jos, dacă nu o acoperă un glaf de pervaz pe partea camerei. **Înălțimea**: goală, până la tavan.
- **Tavanul**, straturile lui de la placa de deasupra încăperii în jos (de la înălțimea încăperii când nu e placă), cu golurile ei scoase.
- **Plinta**: un tip de plintă pus în jurul încăperii, întrerupt la uși (vezi Plintele, la Plăci și acoperișuri).
- **O placă structurală sub pardoseală**: o placă de beton armat a ei sub straturile pardoselii, cu grosimea ei și cât iese dincolo de conturul încăperii (sub pereți). E o placă ca oricare alta, în structură, și în extrasul ei.

**Salvează** schimbă finisajul, iar fiecare încăpere care îl are e refăcută. **Finisează încăperile selectate** îl pune în încăperile selectate.

**Hașura în plan.** Pe planuri, pardoseala unei încăperi finisate e hașurată cum spune finisajul ei, sub pereți, deci planșele, PDF-ul și DXF-ul o tipăresc (layerul FINISH-HATCH). La **Pardoseala** finisajului, **Hașura în plan** alege **după material** (implicit: modelul de suprafață al stratului de deasupra, hașura lui pentru fațade, unde e văzut, din **Gestionare → Materiale**, altfel una sugerată de nume: plăci pentru gresie, scânduri pentru parchet și laminat, o rețea în cruce pentru mochetă, linii pentru vinil, puncte pentru o pardoseală turnată, altfel linii oblice), **model propriu** (orice model al proiectului, cu scara, unghiul și culoarea lui) sau **fără hașură**; scara și unghiul se aplică și modelului materialului. Un plan văzut de jos (un plan de tavan reflectat) hașurează în schimb tavanele, după **Hașura în plan** de la **Tavanul** finisajului, la fel. **Hașură pardoseală**, în proprietățile încăperii, o oprește pentru acea încăpere. **Hașurile finisajelor de pardoseală** ale unui stil de vedere (**Vedere → Setările vederii**, **Gestionare → Stiluri de vedere**) o opresc pentru vederi întregi: e pornită în Architectural, oprită în Structural, MEP și Coordination. Cu culorile încăperilor pe plan, culoarea umple încăperea și hașura se desenează peste ea.

![Un plan cu pardoseala băii hașurată ca plăci și a livingului ca scânduri](shots/room-finish-hatch.webp)

Fiecare finisaj măsoară, pe material, suprafața **brută** (conturul încăperii, nimic scăzut: aria pardoselii, perimetrul pereților înmulțit cu înălțimea fiecărei bucăți) și cea **netă** (ce e construit: ușile, ferestrele și golurile din placă scoase, glafurile și pardoseala de sub uși adăugate; partea glafurilor e în cantități, ca RevealArea), și volumul. **Interior → Extrasul finisajelor** listează fiecare încăpere finisată pe parte și material, cu totalurile pe material la sfârșit; ca orice extras, face grafice, se exportă și merge pe o planșă. În IFC un finisaj e un IfcCovering: FLOORING, CLADDING sau CEILING.

Un finisaj îmbracă ce au deja pereții încăperii: dacă tipurile de pereți au tencuiala lor, dă pereților încăperii doar ce vine peste ea (vopsea, faianță), sau folosește tipuri de pereți doar cu miezul.

**Câmpurile unei încăperi din finisajul ei.** O etichetă, formula unui extras sau textul unui simbol citește ce arată finisajul încăperii: `{finish.name}` numele finisajului, `{finish.floor}` stratul de sus al pardoselii (*Ceramic tiles*), `{finish.walls}` și `{finish.ceiling}` stratul dinspre încăpere. **Eticheta încăperii** le oferă ca **Finisaj**, **Finisaj: pardoseală**, **Finisaj: pereți** și **Finisaj: tavan**. Se calculează de fiecare dată, nu se păstrează: pune alt finisaj și etichetele urmează. O ușă le citește pe ale încăperii ei prin `{room.finish.floor}`.

**O încăpere finisată se măsoară la finisaje.** Aria, perimetrul și volumul ei, în cantități, pe etichetă, în extrasul de încăperi, în legenda culorilor și în aria utilă a unui document, se măsoară la fața finisată a pereților: finisajul pereților de la pardoseală (faianța pe adeziv, de exemplu 1,5 cm) se scade pe fiecare latură, deci o încăpere de 4 × 5 m finisată așa are 3,97 × 4,97 m. Volumul merge până la tavanul finisat (straturile tavanului sub placa de deasupra, altfel sub înălțimea încăperii). Golurile ușilor nu sunt ale încăperii: fața finisată trece peste ele, chiar și când pardoseala trece prin uși. Măsurile la pereți rămân în cantități: aria brută a pardoselii, perimetrul și volumul brut. Schimbi finisajul sau îl scoți și aria încăperii urmează imediat, fără **Actualizează încăperile**.

![Finisajele încăperilor: pardoseala Băii, pereții cu faianță până la 2100 și tavanul](shots/room-finishes.webp)

![Proprietățile unei încăperi cu finisajul ei](shots/room-finish-properties.webp)

## Încăperile în 3D și în IFC

Încăperile sunt ascunse în vederea 3D, ca să nu acopere clădirea. **Vizibilitate / Grafică** în vederea 3D le poate arăta.

La exportul IFC fiecare încăpere e un IfcSpace, cu numărul, numele, funcțiunea, ariile și volumul ei.

## Încăperi dintr-un fișier IFC

**Fișier → Importă IFC…** aduce spațiile fișierului ca încăperi, oricare ar fi programul care le-a scris (Revit, ArchiCAD, Allplan, Bonsai…):

- fiecare ajunge pe **nivelul ei**, cel în care o pune fișierul;
- **conturul** ei vine din forma ei (o extrudare, un B-rep sau o rețea de fețe, ori amprenta desenată lângă ea); îl păstrează, nu urmează pereții;
- **numele** ei e numele lung al spațiului, iar **numărul** e numele lui (așa le scriu Revit și ArchiCAD); **înălțimea** și, când începe deasupra nivelului (o galerie, o pardoseală înălțată), **decalajul la bază** vin din forma ei;
- **funcțiunea**, **departamentul** și **finisajul pardoselii** vin din fișier, acolo unde el le spune (tipul obiectului sau categoria spațiului, o proprietate Department, pardoseala), așa că o schemă de culori după funcțiune sau după departament le colorează imediat;
- tot restul pe care spațiul îl avea în fișier, seturile de proprietăți și cantitățile lui (Pset_SpaceCommon, BaseQuantities, seturile proprii ale programului), se păstrează și e listat, needitabil, la **Proprietăți IFC** în proprietățile ei, cu un filtru când sunt multe.

Mesajul importului spune câte încăperi au intrat. Le găsești pe planurile nivelurilor lor (fiecare își arată eticheta), într-un extras **Încăperi** (**+** de lângă **Extrase** în navigator) sau în tabul IFC al navigatorului; vederea 3D ascunde încăperile.

Un spațiu a cărui formă nu dă un contur intră totuși, cu numele și proprietățile lui, ca încăpere fără contur: apare în extras, dar nu se desenează nicăieri. Selectează-l (din tabul IFC al navigatorului), deschide planul nivelului lui, apoi **Găsește printr-un clic** (clic între pereții lui) sau **Desenează conturul** în proprietățile lui.
