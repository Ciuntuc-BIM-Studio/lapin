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
- **Pânza**: dublu-clic pe un loc gol ca să adaugi un nod: scrie câteva litere (`wall`, `range`, `point`) și apasă **Enter**. Trage de la o ieșire (partea dreaptă a unui nod) la o intrare (partea stângă) ca să le legi. Mută un nod trăgându-l de titlu; Shift-clic ca să alegi mai multe, sau trage un dreptunghi în jurul lor; **Delete** le scoate. Rotița mărește, butonul din mijloc (sau **Space** și tragere) mută pânza. Graful se încadrează în pânză când se deschide și când panoul își schimbă mărimea, până când mărești sau muți pânza tu; butonul **Încadrează** din colțul pânzei (sau **F**, ori **Home**) îl încadrează din nou. Numerele se scriu cu punct zecimal, cum le arată nodurile; o virgulă zecimală scrisă e citită și ea.
- **Inspectorul**, în dreapta: intrările nodului ales care nu au fir (scrie o valoare), ce dă nodul și de ce a căzut, dacă a căzut.

Fiecare schimbare e un pas de anulare: **Ctrl+Z** desface un nod adăugat, mutat sau legat cum desface un perete.

## Porturi și liste

Porturile sunt colorate după ce duc: numerele albastru, textele chihlimbariu, punctele verde, elementele mov, listele gri. Un fir între feluri care nu se potrivesc e refuzat, cu motivul.

Nu există arbori de date. O **listă** legată la o intrare care ia o singură valoare rulează nodul o dată pentru fiecare element: o **Serie** de trei numere în X-ul unui **Punct** dă trei puncte, iar trei puncte în Începutul unui **Perete** dau trei pereți. Mai multe liste merg alături; o listă mai scurtă își repetă ultimul element. Un fir care duce o listă e desenat mai gros, cu numărul elementelor pe el. Unele intrări iau lista întreagă (**Număr**, **Sumă**, **Element din listă**, **Polilinie**). Intrarea de tip listă a unei unelte (elementele pe care lucrează **Fundații izolate sub stâlpi**, punctele unui planșeu) ia lista întreagă într-o singură rulare; o listă de liste o rulează o dată pentru fiecare listă, la fel ca o listă de polilinii legată la punctele unui planșeu: câte un planșeu pentru fiecare contur.

Nodul unei unelte are aceleași intrări ca și comanda ei: un **Perete** are punctele **Început** și **Sfârșit**, o înălțime, un nivel, un tip. Ieșirile lui sunt **Elemente făcute** și **Rezultat** (ce a răspuns comanda).

## Nodurile de liste

Pe lângă **Interval**, **Serie**, **Număr**, **Element din listă**, **Inversează**, **Aplatizează**, **Sumă**, **Cel mai mic și cel mai mare** și **Filtru**, grupul **Liste** are:

- **Listă**: o listă pe care o scrii, cu elementele despărțite prin punct și virgulă sau fiecare pe rândul lui: `3; 4,5; 6`. Virgula sau punctul zecimal merg amândouă. **Fel** **Automat** dă numere când fiecare element e un număr, altfel texte; **Numere** spune care element nu e număr; **Texte** le păstrează pe toate ca text.
- **Unește listele**: listele legate la A, B, C și D, una după alta. O singură valoare contează ca o listă de un element, așa că nodul strânge și câteva valori într-o listă; o intrare lăsată goală e omisă.
- **Sortează**: numerele după mărime, textele alfabetic, cu numerele din ele după mărime (W2 înainte de W10, cu majuscule sau nu). Leagă altă listă la **După** ca să sortezi după valorile ei, loc cu loc: pereții unei clase după înălțime (**Property** `height` în După). **Descrescător** o întoarce. Valorile egale își păstrează ordinea; un element gol merge la sfârșit. **Ordine** spune unde era fiecare element, de la 0.
- **Parte dintr-o listă**: **Număr** elemente de la **Început** (de la 0). Un început negativ numără de la sfârșit; fără număr ia tot până la capăt. Primele trei: început 0, număr 3; ultimele trei: început -3.
- **Unice**: fiecare valoare o dată, în ordinea în care apare prima dată, și **De câte ori** era. Numerele care diferă cu cel mult o miliardime sunt aceleași, punctele la fel; textele se compară cum sunt scrise (majusculele contează), iar numărul 1 nu e textul „1”.
- **Fiecare cu fiecare**: fiecare pereche dintre un element din A și unul din B. Ieșirile A și B sunt cât perechile (fiecare element din A repetat, cele din B reluate), deci nodul următor, legat la amândouă, rulează o dată pentru fiecare pereche: o **Listă** de valori X și una de valori Y prin Fiecare cu fiecare, apoi într-un **Punct**, dau un punct la fiecare încrucișare. Cel mult 10.000 de perechi.

## Nodurile de geometrie

Grupul **Geometrie** face puncte și forme simple: **Punct**, **Coordonatele punctului**, **Mută punctul**, **Distanță**, **Împarte linia**, **Grilă de puncte**, **Puncte pe cerc**, **Linie**, **Polilinie**, **Dreptunghi** și **Colțuri**, plus acestea, care schimbă puncte și polilinii:

- **Rotește**: puncte sau polilinii rotite în jurul verticalei prin **Centru** cu **Unghiul** în grade, în sens trigonometric (fără centru: originea). O listă de unghiuri și un singur contur dau câte un contur rotit pentru fiecare unghi; o listă de contururi și o listă de unghiuri rotesc fiecare contur cu unghiul lui.
- **Scalează**: puncte sau polilinii mărite sau micșorate față de **Centru** cu **Factorul** (2 le dublează, 0.5 le înjumătățește; fără centru: originea). Un **Factor pe Z**, dacă e dat, scalează înălțimile separat de plan.
- **Centru**: mijlocul cutiei din jurul punctelor sau poliliniilor date, toate la un loc: centrul unui dreptunghi, punctul în jurul căruia îl rotești sau îl scalezi.
- **Puncte pe cerc** are un **Unghi de început** în grade: unde e primul punct, măsurat în sens trigonometric de la X (0, implicit, îl pune pe X).

## Un turn răsucit

Exemplul clasic: etaje dintr-o serie, fiecare rotit puțin mai mult decât cel de dedesubt, eventual tot mai mic, cu glisoare pentru numărul de etaje, înălțimea lor, răsucire și mărime.


1. Cinci noduri de intrare: **Număr întreg** „Etaje” (12), **Număr** „Înălțimea etajului” (3.2, metri), **Număr** „Răsucire totală” (90, grade), **Număr** „Lățime” (24, metri), **Număr** „Îngustare” (0.7: mărimea etajului de sus, cea a celui de jos fiind 1).
2. **Serie** „Cote”: Început 0, Pas ← Înălțimea etajului, Număr ← Etaje. Apoi nodul uneltei **Nivel nou**: Cotă ← Cote, Înălțime ← Înălțimea etajului. Face câte un nivel la fiecare cotă.
3. **Dreptunghi**: Lățime și Adâncime ← Lățime. **Centru** ← Conturul dreptunghiului: punctul în jurul căruia se rotesc etajele.
4. Rotirile: **Împarte** A ← Răsucire totală, B ← Etaje; o **Serie** „Rotiri”: Început 0, Pas ← Împarte, Număr ← Etaje.
5. Mărimile: o **Formulă** `(a - 1) / max(b - 1, 1)`, a ← Îngustare, b ← Etaje; o **Serie** „Mărimi”: Început 1, Pas ← Formulă, Număr ← Etaje.
6. **Scalează**: Puncte sau polilinie ← Conturul dreptunghiului, Centru ← Centru, Factor ← Mărimi. Apoi **Rotește**: Puncte sau polilinie ← Scalează, Centru ← Centru, Unghi ← Rotiri: câte un contur pentru fiecare etaj, rotit și scalat.
7. Nodul uneltei **Placă**: Puncte ← Rotește, Nivel ← Elementele făcute de nodul Nivel nou. Câte un planșeu pe fiecare nivel nou.
8. Stâlpi, dacă vrei: un **Scalează** cu Factor 0.85 al contururilor rotite față de Centru, Punctele **Colțurilor** lui în Punctul unui **Stâlp**, Nivel ← nivelurile, Rotire ← Rotiri.

Fă-l unealtă (**Arată în Uneltele mele**), dă celor cinci intrări un minim și un maxim, iar glisoarele paletei rotesc și îngustează turnul pe măsură ce le tragi; **Bake** face nivelurile, planșeele și stâlpii într-un singur pas de anulare. Treizeci de etaje cu stâlpii lor se previzualizează în aproximativ o treime de secundă.

## Citirea modelului

Grupul **Model** citește elementele pe care le alegi (**Elemente alese** și **Folosește selecția**, sau **Elementele unei clase**) ca referințe. Fiecare nod ia un element sau o listă de elemente; un element de alt fel cade doar pe elementul lui și spune de ce, celelalte se citesc. Modelul e citit așa cum e acum: mută un perete și previzualizarea a ce face graful din el se mută și ea. Curbele vin ca puncte după aceeași regulă ca orice curbă din Lapin (nicio coardă la mai mult de un milimetru de arc).

- **Axă**: axul unui perete (și curb), al unei grinzi, al unei fundații continue sau al unei balustrade, ori fiecare ax al unei rețele, ca polilinie, cu **Început** și **Sfârșit**. Al unui perete la baza lui, al unei grinzi la partea ei de sus.
- **Contur**: conturul închis al unui planșeu, al unei încăperi, al unui acoperiș sau al unei zone de teren (o plantație), la cota lui, și **Colțurile** lui.
- **Amplasament**: unde stă un stâlp, o fundație izolată, o piesă de mobilier, o ușă sau o fereastră, și **Rotirea** ei în grade. O ușă sau o fereastră e pe axul peretelui ei, la parapet.
- **Cutia încadratoare**: cutia din jurul corpului oricărui element, așa cum îl construiește Lapin: colțurile cel mai de jos și cel mai de sus, centrul, mărimea pe X și pe Y și înălțimea.
- **Geometria desenului**: o linie, polilinie, dreptunghi, arc sau cerc desenat cu uneltele de desen, ori liniile unui DXF importat, în model: la cota vederii lui (a unui plan la cota nivelului), arcele și cercurile ca puncte. Dă **Linie**, **Puncte**, **Început**, **Sfârșit** și **Centru** pentru un arc sau un cerc. Selectează formele în plan, apasă **Folosește selecția** pe un nod Elemente alese și leagă-l: o polilinie desenată în **Colțuri**, apoi începuturile și capetele segmentelor într-un **Perete**, face un perete pe fiecare latură; o linie desenată în **Împarte linia**, apoi într-un **Stâlp**, pune stâlpi în punctele de împărțire.

## Un graf ca unealtă

Un graf poate fi o unealtă a lui: intrările lui devin câmpurile unei mici **palete** care îl calculează și îl creează fără pânză, la fel în aplicația desktop și în Lapin din browser.

![Rândul de pereți ca unealtă: paleta lui, cu numărul și înălțimea pereților, pereții făcuți și fantoma în plan](shots/graphs-palette.webp)

- **Fă-l unealtă**: dă clic pe un loc gol al pânzei și, sub **Graful ca unealtă** în inspector, bifează **Arată în Uneltele mele**. Butonul lui intră în **Uneltele mele**, în grupul **Grafuri** dacă nu numești altul, cu numele pe care i-l dai (altfel al grafului), o pictogramă (`wall`, `grid`, `star`… sau o literă) și un indiciu. Butonul de paletă de deasupra pânzei deschide și el paleta.
- **Câmpurile** sunt nodurile de intrare ale grafului: **Număr** (cu unitatea lui, metri sau grade; cu un **Minim** și un **Maxim** primește un cursor), **Număr întreg**, **Text**, **Da sau nu**, **Alegere**, **Elemente alese** (cu **Folosește selecția**), **Nivel** (al vederii, dacă nu e ales unul) și **Un tip** (un tip de perete, de placă… pentru Tipul unei unelte). Selectează un nod de intrare și stabilește, sub **În paletă**, dacă se vede acolo, eticheta lui, **Ordinea** (cele fără ordine urmează, de sus în jos ca pe pânză) și minimul, maximul și pasul unui număr.
- **Ce dă**: fiecare nod **Vezi valoarea** își arată valoarea în paletă, sub câmpuri; bifează **Arată-i valorile în paletă** pe orice alt nod (un **Număr** al pereților făcuți, o **Sumă** de lungimi) ca să-l arăți și pe el. O listă își arată primele elemente și câte sunt în total, o listă de liste ca un mic tabel.
- **Folosirea**: schimbă un câmp și fantoma îl urmează imediat, fără să atingă modelul. Când îl lași (eliberezi cursorul, apeși **Enter**, bifezi, alegi), valoarea e scrisă în graf: un pas de anulare, și pânza o arată și ea. **Creează** face ce arată fantoma, într-un pas de anulare.

- **Valorile paletei**, sub **Graful ca unealtă**, spune ale cui sunt valorile:
  - **Comune** (implicit): ce lași în paletă e scris în graf, ca mai sus. Valorile sunt ale grafului, păstrate cu proiectul: un coleg care îl deschide primește aceeași unealtă, aceleași câmpuri și valorile folosite ultima dată.
  - **Ale fiecărui utilizator**: valorile grafului sunt doar cele implicite. Ce schimbi în paletă se păstrează pe calculatorul tău, pentru acest proiect și acest graf, și nu e scris niciodată în graf; un coleg vede valorile grafului, sau pe ale lui. Un punct mic lângă un câmp arată o valoare a ta diferită de a grafului. Fantoma și **Creează** folosesc valorile tale, iar Creează face doar elementele: graful rămâne cum era, iar pasul de anulare e tot „Creare din graf”. **Înapoi la valorile grafului** le uită pe ale tale; **Salvează ca valori ale grafului** le scrie în graf pentru toți, într-un pas de anulare. O valoare a ta care nu se mai potrivește (nodul ei de intrare șters sau schimbat, o alegere care nu mai e oferită, un număr în afara minimului și maximului, un nivel sau un tip care nu mai există) lasă locul valorii grafului.

Într-o sesiune partajată oaspeții nu primesc paleta, cum nu primesc nici graful.

## Grupuri

Selectează câteva noduri și apasă **G** (sau butonul de grup din colțul pânzei) ca să le strângi într-un singur nod: un **grup**. Intrările lui sunt firele care veneau în noduri din afară, ieșirile cele care plecau din ele; graful se calculează la fel. Selectează grupul și apasă **Desface grupul** în inspector ca să ai nodurile înapoi, unde stă grupul. Nodurile de intrare dintr-un grup rămân câmpuri ale paletei.

## Erori

Un nod care cade se face roșu și spune de ce în inspector (și când treci cu mouse-ul peste el): un perete fără lungime, o împărțire la zero. Nodurile de după el spun care intrare nu are nimic; restul grafului se calculează mai departe. Când cad doar câteva elemente ale unei liste, nodul se face chihlimbariu și spune câte.

## Mesaje

Sub canvas, o bandă numită Mesaje arată ce a găsit ultima evaluare: fiecare nod care a eșuat sau a avertizat, cu numele lui și motivul; nodurile rămase fără ce să lucreze pentru că a eșuat o intrare dinaintea lor; limitele atinse de graf; și o linie cu câte comenzi a rulat și cât a durat. O deschizi cu săgeata ei, filtrezi după Erori, Avertismente sau Toate, copiezi rândurile ca text simplu sau le golești. Un clic pe un rând selectează nodul lui și îl aduce în mijlocul canvasului. Fiecare evaluare înlocuiește rândurile celei dinainte, deci editarea nu umple lista; Creează adaugă propriul rând, câte elemente a făcut într-un singur pas de anulare, sau de ce a refuzat. Închisă, banda e o singură linie cu ultimul mesaj și numărul de erori. Înălțimea ei și dacă e deschisă se rețin. Oaspeții nu au panoul Grafuri, deci pentru ei nu se schimbă nimic.

## Creează

**Creează** face ce arată fantoma, într-un singur pas de anulare numit **Creare din graf**. Dacă un nod al uneltelor cade, nu se face nimic, iar Lapin spune care nod a căzut. După creare fantoma se ascunde (ochiul o arată din nou).

![Rândul de pereți creat: trei pereți, un singur pas de anulare](shots/graphs-baked.webp)
 Apeși din nou **Creează** după o schimbare și graful face elemente noi; cele făcute înainte rămân cum sunt (un graf care își ține elementele la zi e planificat).

## Bine de știut

- Un graf nu rulează cod și nu poate citi sau scrie fișiere: uneltele care importă, exportă sau deschid proiecte nu sunt oferite ca noduri.
- Un graf se calculează cel mult 5 secunde și rulează cel mult 2.000 de comenzi deodată; o listă are cel mult 10.000 de elemente.
- Într-o sesiune partajată, grafurile sunt ale gazdei, ca scripturile: un invitat nu le poate deschide, previzualiza, crea din ele, redenumi sau șterge; panoul lui Grafuri spune asta.
