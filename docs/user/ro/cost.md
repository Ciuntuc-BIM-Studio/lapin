---
title: Devizul
order: 98
summary: Devizul din model: listele tale de prețuri importate din CSV sau Excel, articole pe tipuri și reguli pe materiale, cantitățile din antemăsurătoare înmulțite cu prețurile unitare defalcate pe material, manoperă, utilaj și transport, cheltuieli indirecte, profit și TVA, în lei sau euro; formularele F3, F2 și C6–C9 pe planșe, în Excel și PDF; devizul în IFC.
---

## Tabul Execuție

![Tabul Execuție](shots/ribbon-cost.webp)

Grupurile de cost sunt pe tabul **Execuție**, după cele ale programului lucrărilor (tabul se numea **Cantități**, apoi **Cost**). Păstrează cantitățile și adaugă devizul:

- **Antemăsurătoare**, **Extrase…** și **Export XLSX**: cantitățile modelului (vezi [Extrase și grafice](schedules.md)).
- **Liste de prețuri…**: prețurile tale, importate dintr-un fișier CSV sau Excel (mai jos).
- **Atribuie articole…**: ce articol prețuiește fiecare tip și fiecare material (mai jos).
- **Deviz…**: deschide devizul sub vedere (prima dată îl și face).
- **Formular pe planșă**, **Deviz XLSX** și **Deviz PDF**: devizul și formularele lui pe o planșă, într-un registru de calcul sau într-un PDF (mai jos).
- **Costul în timp**: devizul pus pe programul lucrărilor, curba S și tabelul lunar (mai jos).
- **Flux de numerar**: plățile după condițiile contractului tău, lună de lună, și necesarul maxim de finanțare (mai jos).

Devizul se calculează din model la fiecare schimbare. Un perete pe care îl desenezi acum intră imediat în deviz, cu cantitățile lui. Se păstrează doar prețurile, articolele de pe tipuri, regulile și rândurile tale.

## Listele de prețuri

![Importul unei liste de prețuri: sensul fiecărei coloane ghicit după antet](shots/price-import.webp)

Prețurile sunt ale tale: Lapin nu livrează niciun catalog de prețuri sau de norme. **Execuție → Liste de prețuri… → Import…** citește un fișier CSV sau XLSX, de exemplu un export din programul tău de devize sau lista unui furnizor. Lapin arată primele rânduri ale fișierului sub antetele lor. Deasupra fiecărei coloane e ce a ghicit Lapin că ține, după antet, în română sau în engleză:

- **Cod** și **Denumire** (*Cod articol*, *Simbol*, *Code*, *Description*), **Unitate** (*U.M.*: mc sau m³, mp sau m², m, buc, kg, t, 100 mp).
- Prețul unitar defalcat pe **Material**, **Manoperă**, **Utilaj** și **Transport**, sau un singur **Preț unitar (nedefalcat)**, care se socotește atunci material.
- Opțional: **Ore-om pe unitate** (*Normă de timp*) și **Meserie**, pentru lista de manoperă (C6).
- Opțional: resursele articolului, rânduri care numesc articolul căruia îi aparțin (**Articolul căruia îi aparține**), cu **Tipul resursei** (M, Man, U, T) și **Consumul pe unitate**. Un articol dat doar prin resursele lui e prețuit după ele, iar listele de resurse (C7–C9) le arată una câte una.

Schimbă coloanele ghicite greșit, alege rândul pe care e antetul și dacă prețurile sunt în lei sau în euro, apoi apasă **Import**. Lista intră în proiect. Cu **Păstrează și în biblioteca acestui calculator** bifat, rămâne și pe calculator: sub **Pe acest calculator**, **Adaugă în proiect** o aduce în proiectul următor.

Tot opțional: **Sursa (furnizorul)**, un **Link** la pagina lui și data la care a fost **Preț verificat la**, păstrate pe fiecare rând.

**Șablon** salvează un CSV cu coloanele și câteva rânduri cu valori orientative scrise de Lapin, marcate *orientativ*: completează-l cu prețurile tale și importă-l. La **Șabloane**, **Adaugă în proiect** pune în proiect o listă gata făcută: **Lapin indicative prices** (valorile orientative ale lui Lapin, ca să încerci devizul înainte să ai prețurile tale) și listele aduse de modulele ediției, de pildă **Bilka – Dedeman (indicative, 24.11.2026)** pentru [panourile de acoperiș](roof-panels.md#cat-costa), fiecare rând cu sursa și data lui. Un șablon intră în proiect doar când ceri și e marcat *orientativ*: înlocuiește-i prețurile înainte să trimiți un deviz cuiva.

## Articole pe tipuri, reguli pe materiale

![Atribuie articole: fiecare tip și material cu articolul lui](shots/map-articles.webp)

Un rând de deviz vine dintr-un **articol**: un cod din lista ta de prețuri. Elementele primesc articole în două feluri:

- **Pe tip.** Fiecare element al tipului e prețuit după el. Articolul spune ce măsoară: elementul întreg sau o parte din el (**miezul** unui tip stratificat sau **finisajele** lui) și după ce măsură (după unitatea prețului, volum net sau brut, arie netă sau brută, lungime, bucăți, masă). Poate adăuga un coeficient și pierderile (%).
- **Pe material (o regulă).** Fiecare strat din acel material, în orice tip, e prețuit după ea: o singură regulă pentru tencuiala de var prețuiește tencuiala tuturor pereților.

O piesă Studio este și ea un tip. Parametrii ei marcați la **Măsurat ca** în tabelul piesei (Lungime, Număr, Arie, Volum) sunt citiți întâi de un articol al acelei măsuri pe piesa întreagă: o piesă de gard cu L = 20 m, prețuită pe metru, dă 20 m (vezi [Piese](studio-parts.md#valorile-piesei-în-extrase-filtre-și-deviz)).

Articolul de pe tip are întâietate. Un strat pe care articolele tipului îl măsoară deja nu mai e prețuit de o regulă. Un articol pe elementul întreg îl prețuiește tot, deci regulile se dau la o parte. Pentru un perete stratificat, dă tipului un articol pe **miez** (zidăria, la volum) și reguli pentru finisaje (tencuiala, la arie). Peretele e atunci prețuit strat cu strat: zidăria la **Pereți**, tencuiala la **Finisaje și izolații**.

**Execuție → Atribuie articole…** listează tipurile proiectului pe care le folosesc elementele și materialele straturilor lor. Fiecare rând pornește cu articolul pe care îl are, sau cu cel ales pentru un tip sau material cu același nume într-un proiect anterior, sau cu cel sugerat de Lapin: un cod scris în numele lui, sau articolul cu cele mai multe cuvinte comune (*zidărie*, *BCA*, *tencuială*, *C25/30*). Pentru un tip stratificat propune miezul. Scrie sau alege un cod, alege partea și măsura, bifează rândurile dorite și apasă **Aplică**: un singur pas de anulat. Alegerile tale sunt ținute minte pe acest calculator pentru proiectele următoare.

Tipurile de cofraj și de schelă din organizarea de șantier (tabul **Teren**) aveau deja un cod de articol (decizia 152): el e un articol pe tip, măsurat după unitatea prețului.

Fazele contează: un element care era acolo înainte de lucrări (făcut într-o fază *existentă*) nu se prețuiește. Unul demolat se prețuiește doar prin articolul tipului său a cărui parte e **Demolarea lui (doar când e demolat)**, la capitolul **Demolare**.

### Articole prețuite după timpul pe șantier

Organizarea de șantier și elementele de siguranță se închiriază adesea după cât timp stau pe șantier: un gard pe metru și lună (*m × lună*), un indicator pe bucată și zi, o macara pe oră. Coloana **× Durată** din Atribuie articole înmulțește cantitatea unui articol (bucăți, lungime, arie…) cu cât timp stă elementul lui pe șantier, luat din programul lucrărilor (vezi [Planificarea](planning.md#costul-sarcinilor)):

- **După unitatea prețului** (implicit): timpul din unitatea prețului, dacă are unul. *m × lună*, *m x month*, *cad × mese* sunt o lungime sau bucăți înmulțite cu luni; *buc/zi* zile calendaristice; *buc/zi lucrătoare* zile lucrătoare; *oră* sau *h* ore. O unitate fără timp nu se înmulțește, ca înainte.
- **Fără durată**: timpul din unitate lăsat deoparte, cantitatea simplă.
- **Luni pe șantier**: lunile calendaristice întregi de la început, restul ca parte din luna lui (1 martie – 31 mai înseamnă 3 luni).
- **Zile lucrătoare pe șantier**: zilele lucrătoare ale calendarului proiectului (zilele săptămânii, sărbătorile și excepțiile lui).
- **Zile calendaristice pe șantier**: toate zilele, cu ambele capete.
- **Ore lucrătoare pe șantier**: zilele lucrătoare înmulțite cu orele unei zile din calendar.

Un element stă pe șantier de la începutul primei sarcini care îl preia până la finalul ultimeia (sarcinile preiau elemente prin regula lor sau de mână, vezi [Planificarea](planning.md#elementele-modelului-în-sarcini)). Se calculează din nou de fiecare dată când e arătat devizul: muți o sarcină sau schimbi calendarul, și cantitatea urmează. Un gard de 20 m într-o sarcină de trei luni, prețuit *m × lună*, dă 60.

Rândul arată durata folosită după descriere (**× 3 luni**; cea mai mică și cea mai mare când elementele lui diferă), în deviz, în formularele lui (XLSX, PDF), în exportul HTML și în IFC. Un element pe care nu-l preia nicio sarcină își păstrează cantitatea, neînmulțită, iar rândul spune asta (**1 element(e) în nicio sarcină a programului**), ca nimic să nu fie prețuit la zero fără o vorbă. Fără program al lucrărilor, rândul spune că proiectul nu are unul; într-o ediție fără modulul Planificare, un articol prețuit după unitatea lui e prețuit ca înainte, iar unul pus pe o durată spune de ce nu are una.

## Devizul

![Devizul: capitole, rânduri, cotele și totalurile](shots/estimate.webp)

**Execuție → Deviz…** deschide devizul sub vedere, ca Gantt-ul. Rândurile lui sunt grupate în **capitole** pe categorii de lucrări: terasamente, fundații, structură, pereți, acoperiș, uși și ferestre, finisaje și izolații, instalații, organizare de șantier, altele. Un articol poate numi un capitol al lui. **Pe etaje și categorii de lucrări** pune etajul înaintea fiecărui capitol.

Fiecare rând arată codul, denumirea și unitatea din lista de prețuri, **cantitatea** din model (aceeași ca în antemăsurătoare), prețul unitar defalcat pe **Material**, **Manoperă**, **Utilaj** și **Transport**, prețul unitar și **valoarea**. Un clic pe rând îi selectează elementele în vederi. Selectează elemente într-o vedere și rândurile lor se marchează în deviz.

Sub rânduri sunt totalurile, cum le așază devizele românești:

1. **Cheltuieli directe**: suma rândurilor.
2. **Contribuția asiguratorie pentru muncă (CAM)**: 2,25 % din manoperă, implicit.
3. **Cheltuieli indirecte**: 10 % din cheltuielile directe cu CAM, implicit.
4. **Profit**: 5 % din cheltuielile directe și indirecte, implicit.
5. **Total fără TVA**, **TVA** (21 % implicit) și **Total cu TVA**.

Orice cotă se schimbă în bara de unelte. Fiecare valoare e rotunjită la două zecimale, cum se tipărește un deviz.

Devizul e în **RON** sau **EUR**. **1 EUR =** e cursul proiectului: o listă de prețuri în cealaltă monedă e convertită la el, iar căsuța de lângă arată fiecare valoare și în cealaltă monedă.

Bara de unelte arată și ce nu e prețuit: **N tip(uri) fără preț** (un clic deschide Atribuie articole) și codurile pe care nu le are nicio listă. **Un rând al tău** adaugă o lucrare care nu e în model (împrejmuirea șantierului, o provizie): scrie-i codul, denumirea, unitatea, cantitatea și prețurile. Un rând cu cod și fără prețuri le ia din lista de prețuri.

### Rânduri legate de timp

Unele costuri țin cât țin lucrările: șeful de șantier, dirigenția, baraca de șantier, o macara închiriată, paza. Un procent de cheltuieli indirecte nu poate spune că o întârziere le face mai scumpe. **Un rând legat de timp (tarif × durată)**, sub rânduri, adaugă unul: un tarif pe **săptămână**, pe **lună**, pe **zi lucrătoare** sau pe **zi calendaristică** (îl alegi lângă descriere) înmulțit cu cât ține sarcina lui. Alege sarcina lângă el: de obicei o [sarcină de nivel de efort](planning.md) (conducerea șantierului de la primele lucrări la predare), orice sarcină sau **Tot proiectul**. Cantitatea lui e timpul acela, calculat din program la fiecare schimbare, iar zilele pe care le acoperă apar lângă descriere: când lucrările întârzie, rândul ține mai mult și costă mai mult. Scrie-i tariful în coloanele de preț (Material pentru un tarif global, Manoperă pentru oamenii plătiți pe șantier, cum îl prețuiești).

Intră în capitolul lui, **Time-related / preliminaries**, dacă nu-i dai altul, și se adaugă la cheltuielile directe ca orice rând. Cheltuielile indirecte, profitul și TVA-ul rămân procente, ca înainte: un rând legat de timp stă lângă ele, nu în locul lor. Fără program (sau fără modulul Planificare), cantitatea lui e cea scrisă, iar o notă spune de ce.

## Formularele

![Lista cantităților de lucrări (F3) a aceluiași deviz](shots/estimate-f3.webp)

Lista de deasupra devizului arată devizul sau unul din formularele lui. Sunt grupări ale aceluiași deviz, deci dau aceleași totaluri:

- **Lista cantităților de lucrări (F3)**: *Lista cu cantitățile de lucrări pe categorii de lucrări*, capitol cu capitol, prețurile unitare și valorile defalcate pe material, manoperă, utilaj și transport, cu totalurile dedesubt.
- **Deviz pe obiect (F2)**: valoarea fiecărui capitol fără TVA, TVA-ul și valoarea cu TVA.
- **Manoperă (C6)**: orele și valoarea pe meserii (din meseria și normele de timp ale listei de prețuri, sau din resursele de manoperă).
- **Materiale (C7)**, **Utilaje (C8)** și **Transport (C9)**: resursele cu cantitățile, prețurile unitare și valorile lor. Un articol fără resurse se socotește o resursă în sine.

Formularele urmează practica obișnuită după metodologia Ordinului 863/2008. Verifică-le cu ce cere beneficiarul sau programul de finanțare: spune-ne dacă ai nevoie de altă machetă.

**Pune pe planșă** așază tabelul arătat pe o planșă nouă, gata de tipărit cu celelalte. **XLSX** salvează devizul și toate formularele într-un registru de calcul, câte o foaie fiecare. **PDF** le salvează ca PDF, A4 orizontal, gata de tipărit.

## Costul în timp

![Costul în timp: curba S peste barele lunilor, tabelul lunar sub ea](shots/cost-time.webp)

Când proiectul are un program al lucrărilor (tabul **Execuție**), devizul se pune pe el fără nicio legare în plus. Valoarea fiecărui rând se împarte pe elementele lui, iar partea fiecărui element merge la sarcinile care construiesc partea măsurată de rând: articolul zidăriei la sarcina care ia miezul pereților, al tencuielii la cea care le ia finisajele. Un articol pe tot elementul, construit în două sarcini, se împarte egal între ele. Un rând propriu merge la sarcina aleasă lângă descrierea lui (implicit **În nicio sarcină**). Costul unei sarcini se întinde egal pe zilele ei lucrătoare, deci nu se cheltuie nimic într-un sfârșit de săptămână, într-o sărbătoare sau, pentru o sarcină umedă, în pauza de iarnă.

**Execuție → Costul în timp** deschide, sub vedere:

- **curba S**: costul cumulat planificat pe durata programului, cel al **liniei de bază** (întrerupt) când e salvată una, și **lucrul realizat** (gros) până la data de stare: costul fiecărei sarcini înmulțit cu procentul ei realizat, întins de la începutul ei real;
- barele lunilor, pe scara din dreapta: planificat și realizat;
- deasupra, **bugetul la terminare** (cheltuielile directe ale devizului), **costul la zi** și ce e **neprogramat**, dacă e ceva: rânduri ale căror elemente nu le construiește nicio sarcină (un clic deschide devizul);
- dedesubt, **tabelul lunar**: costul fiecărui capitol pe lună, ce e neprogramat și totalul lui.

Un rând legat de timp se întinde pe zilele lucrătoare ale sarcinii lui sau, dacă nu numește niciuna, ale întregului proiect; pe tot proiectul, partea lui realizată e partea acelor zile până la data de stare. Valorile sunt cheltuieli directe, ca rândurile devizului. Gantt-ul arată costul fiecărei sarcini în coloana **Cost** (vezi [Planificare](planning.md)).

![Curba S pe o planșă](shots/cost-curve-sheet.webp)

**Pune pe planșă** pune curba pe o planșă nouă, desenată vectorial, cu legenda ei. **Tabelul lunar pe planșă** pune tabelul lunar, care e și formularul **Costul pe luni** din lista devizului: capitolele lună de lună, apoi cheltuielile directe, totalurile fără TVA și cu TVA (împărțite proporțional), costurile cumulate și, cu progres, lucrul realizat. Fiecare rând se adună la totalul devizului. **XLSX** salvează curba zi de zi, tabelul lunar și costurile sarcinilor într-un registru; **PDF** salvează curba și tabelul lunar.

**Gestionare → Versiuni → Compară** spune, pe lângă elemente și program, ce s-a schimbat în deviz: totalul cu TVA, cheltuielile directe și diferența, fiecare capitol al cărui cost s-a schimbat și rândurile adăugate, scoase sau schimbate (cantitatea și valoarea lor).

Situațiile de lucrări lunare, valoarea câștigată și mai multe linii de bază sunt pentru ediția premium.

## Fluxul de numerar

Curba S e costul *făcut*. Fluxul de numerar sunt banii: ce plătește beneficiarul și când, ce plătești tu și când, și cât trebuie să găsești între timp. **Execuție → Flux de numerar** (sau **Flux de numerar** din bara curbei S) îl deschide în locul curbei S; **Curba S** te întoarce.

Merge pe programul **planificat** și se calculează din deviz la fiecare schimbare. Se păstrează doar condițiile tale, pe deviz:

- **Contract**: **valoarea certificată**: **devizul fără TVA** (cheltuieli directe, indirecte și profit), **cheltuielile directe** sau un **preț de contract** pe care îl scrii (prețul beneficiarului; certificatele îl împart proporțional cu costurile planificate). Cu **TVA** bifat, TVA-ul la cota devizului se plătește odată cu fiecare plată, la încasări și la plăți.
- **Avans**: un procent din valoarea contractului, plătit într-o zi aleasă (o dată sau începutul unei sarcini ori al unui jalon; implicit începutul programului). Fiecare certificat îl recuperează proporțional (valoarea lui × avans ÷ valoarea contractului); ultimul recuperează ce a rămas.
- **Garanția de bună execuție (reținerea)**: procentul reținut din fiecare certificat, eliberat în două părți: o parte la **recepția la terminarea lucrărilor** (o dată sau sfârșitul unei sarcini; implicit sfârșitul programului), restul după **perioada de garanție** (luni de la recepție). Fiecare eliberare se plătește cu termenul de plată.
- **Certificate și plată**: **lunar** (într-o zi a lunii; 0 e ultima ei zi) sau **săptămânal** (în fiecare duminică). Un certificat ia lucrul planificat de la cel dinainte; se plătește valoarea lui netă (certificat − reținere − avans recuperat) după **plătit după** zile.
- **Plăți**: bifează **Arată plățile** ca să vezi ce plătești: **cheltuielile directe** sau **costurile cu indirectele** (tot, fără profit), fiecare plătit furnizorilor după numărul de zile ales de la data la care s-a făcut.

Fără nimic setat se arată valorile implicite ale Lapin, marcate **orientative**: practica românească așa cum o citește Lapin (situații de lucrări lunare la sfârșitul lunii, plătite în 30 de zile, garanție 5 % eliberată jumătate la recepție și jumătate după o perioadă de garanție de 24 de luni, fără avans, furnizori plătiți în 30 de zile). Contractul tău decide: schimbă oricare dintre ele; **Valorile implicite Lapin** le pune înapoi. Fiecare schimbare e un pas de anulare.

Graficul arată o bară pe perioadă pentru **fluxul net** (verde când intră mai mult, roșu când iese mai mult), **cumulatul** ca linie și un punct roșu la **necesarul maxim de finanțare**: cel mai jos punct al cumulatului, banii pe care trebuie să-i finanțezi, și luna în care se întâmplă (scris și deasupra graficului). Sub el, tabelul pe luni (sau **pe săptămâni**): costul făcut, certificatul, reținerea, avansul recuperat, încasările, plățile, netul, cumulatul și soldurile încă reținute; apoi fiecare certificat cu data lui, deducerile, valoarea netă și ziua plății, și eliberările garanției.

**Tabel pe planșă** pune tabelul pe o planșă nouă (extrasul **Cash flow**, pe luni). **XLSX** salvează perioadele și certificatele într-un registru, **CSV** perioadele.

Cu toate condițiile la zero (valoarea certificată: cheltuielile directe, fără avans, fără reținere, plătit după 0 zile, furnizori după 0 zile, la plăți cheltuielile directe), încasările sunt costul fiecărei luni, ca în tabelul curbei S. Certificatele lucrului realizat efectiv (situațiile de lucrări din progres) sunt pentru ediția premium; când e prezentă, certificatele ei iau locul celor planificate până la ultimul emis.

## IFC

**Fișier → Exportă → Exportă IFC, Fragments sau BOS…** scrie devizul ca **IfcCostSchedule**: capitolele și rândurile ca **IfcCostItem**, cantitatea fiecărui rând și prețurile lui unitare ca patru **IfcCostValue** (Material, Labour, Equipment, Transport), elementele pe care le măsoară fiecare rând atribuite lui (**IfcRelAssignsToControl**), moneda ca **IfcMonetaryUnit** al proiectului. La import, un deviz își păstrează rândurile cum le are fișierul, cu aceleași totaluri. Bifează **Rânduri din model** ca să adaugi rândurile pe care le dă modelul tău.

Cu un program al lucrărilor, fiecare rând e atribuit și sarcinilor în care se execută (**IfcRelAssignsToControl** spre **IfcTask**-urile lor). La import, rândurile își păstrează sarcinile, deci curba S revine la fel. Un rând prețuit după timpul pe șantier își scrie **Time** și **Duration** în **Lapin_CostItem**. Un rând legat de timp își scrie acolo și unitatea tarifului (**Per**), iar sarcina lui ca sarcina rândului, deci citit înapoi urmează mai departe programul.

Cu un program al lucrărilor, fiecare rând e atribuit și sarcinilor în care se execută (**IfcRelAssignsToControl** spre **IfcTask**-urile lor). La import, rândurile își păstrează sarcinile, deci curba S revine la fel. Un rând prețuit după timpul pe șantier își scrie **Time** și **Duration** în **Lapin_CostItem**. Condițiile fluxului de numerar pe care le-ai setat se scriu în **Lapin_Estimate** (**CashFlow.Advance**, **CashFlow.Retention**, **CashFlow.PaymentDays**…) și se citesc înapoi; o sarcină aleasă pentru avans sau recepție nu, deci data ei revine la cea a programului.
