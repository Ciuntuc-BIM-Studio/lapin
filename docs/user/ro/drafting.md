---
title: Desen și linia de comandă
order: 80
summary: Scrii comenzi ca în CAD, te prinzi de puncte, muți și copiezi, desenezi linii și adnotezi cu cote, texte și etichete.
---

## Linia de comandă

Scrie oriunde în fereastră literele unei unelte, apoi apasă <kbd>Enter</kbd>. De exemplu <kbd>WA</kbd> pentru perete, <kbd>CO</kbd> pentru copiere, <kbd>M</kbd> pentru mutare sau <kbd>L</kbd> pentru linie. Linia de comandă de jos arată ce îi trebuie uneltei mai departe. Poți da clic pe un punct sau poți scrie o lungime, un unghi sau coordonate.

Coordonatele tastate sunt în unitatea proiectului, ca lungimile de lângă cursor: într-un proiect în milimetri, `@4000,4000` după primul colț al unui dreptunghi îl face de 4 m pe 4 m, iar `4000<90` înseamnă 4 m drept în sus.

![Copierea unui perete: după punctul de bază, linia de comandă și căsuțele arată lungimea și unghiul până la mouse](shots/command-line.webp)

**Căsuța de lângă cursor.** Cât timp se desenează un segment, căsuța mică de lângă cursor arată **L**ungimea și unghiul **∠** lui, și poți scrie în ea fără să dai clic nicăieri: cifrele (cu unitate, dacă vrei: `240cm`, `2.4m`) merg în câmpul activ, <kbd>Tab</kbd> trece la celălalt, <kbd>Enter</kbd> ia punctul, iar <kbd>Esc</kbd> șterge ce ai scris. Pentru un dreptunghi (**Dreptunghi** din desen, o placă sau orice altă unealtă desenată ca dreptunghi) căsuța poate cere și **L**ățimea și **Î**nălțimea, ca `@2000,1500` pe linia de comandă: apasă <kbd>Space</kbd> (sau butonul de mod din bara de deasupra vederii) ca să treci între *Lungime ∠ Unghi* și *Lățime × Înălțime*. În *Lățime × Înălțime*, <kbd>Tab</kbd> sau o virgulă trece de la lățime la înălțime, iar valorile au semn ca pe linia de comandă: o lățime negativă pune dreptunghiul în stânga primului colț, o înălțime negativă dedesubt. Un câmp lăsat gol urmează mouse-ul. Fiecare unealtă de dreptunghi își ține minte modul. Un clic ia ce ai scris, ca <kbd>Enter</kbd>: scrie `145` după primul punct al unei copieri, apoi dă clic, și copia ajunge la 145 distanță pe direcția clicului (clicul dă doar direcția; la lățime și înălțime, doar semnul unui câmp gol).

![Căsuța dreptunghiului în Lățime × Înălțime: 2000 lățime și 1500 sub primul colț](shots/rectangle-box.webp)

**Rotirea.** **Rotire** din panglică (și **Modificare → Rotire**) merge în trei clicuri: **balamaua**, un **punct de referință** (încotro arată acum obiectul: dă clic pe muchia pe care vrei s-o îndrepți) și un **punct țintă** (încotro trebuie să arate). Selecția se rotește în previzualizare pe măsură ce muți mouse-ul, unghiul se vede la cursor, iar snap-ul și urmărirea polară te ajută să țintești. Rotirea e diferența dintre cele două direcții, oricare ar fi UCS-ul. La orice pas poți scrie în loc: un unghi la pasul de referință e chiar rotirea (`30`), iar un unghi la pasul țintă e direcția nouă, măsurată de la axa X a UCS-ului. <kbd>C</kbd> rotește o copie și lasă originalul. Dacă scrii **ROTATE** (sau <kbd>RO</kbd>) pe linia de comandă, primești varianta AutoCAD: punctul de bază, apoi unghiul (scris, sau un punct măsurat de la X), cu **[Reference/Copy]**; <kbd>R</kbd> acolo e același flux cu referință.

Fiecare unealtă își arată literele când ții mouse-ul peste ea; le poți schimba în **Vedere → Scurtături de tastatură…** (vezi [Interfața și scurtăturile](interface.md)). Acestea sunt cele mai folosite:

| Model | | Desen | |
|---|---|---|---|
| Perete | <kbd>WA</kbd> | Linie | <kbd>L</kbd> |
| Gol ca ușă, fereastră | <kbd>DR</kbd>, <kbd>WN</kbd> | Polilinie | <kbd>PL</kbd> |
| Placă | <kbd>SB</kbd> | Dreptunghi, cerc, arc | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Acoperiș | <kbd>RF</kbd> | Mută, copiază | <kbd>M</kbd>, <kbd>CO</kbd> |
| Scară, încăpere | <kbd>ST</kbd>, <kbd>RM</kbd> | Rotește, oglindește, scalează | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Stâlp, grindă | <kbd>CL</kbd>, <kbd>BM</kbd> | Paralelă, taie, prelungește | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Secțiune | <kbd>SE</kbd> | Racordare, teșire | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Măsoară | <kbd>ME</kbd> | Hașură, text | <kbd>H</kbd>, <kbd>T</kbd> |
| Încadrează tot | <kbd>ZE</kbd> | Șterge | <kbd>E</kbd> |

## Măsurarea

**Măsoară** (<kbd>ME</kbd>, a doua unealtă rapidă, sau **MEASURE** ori **DIST** în linia de comandă) măsoară fără să deseneze ceva: dai clic pe puncte într-un plan, o secțiune sau o fațadă, cu snap, și apeși <kbd>Enter</kbd>. Cât dai clic, câmpul de introducere arată lungimea și unghiul fiecărui segment; la sfârșit, mesajul dă lungimea totală, fiecare segment (la unul singur, Δx, Δy și unghiul) și, de la trei puncte, aria pe care o închid. În vederea 3D punctele se prind de colțurile, muchiile și fețele modelului; când sunt la înălțimi diferite, mesajul dă lungimea adevărată în spațiu, cu Δz.

## Snap și ajutoare

Lapin se prinde de capetele, mijloacele și intersecțiile a ce e desenat, și de grilă. <kbd>F3</kbd> pornește sau oprește snap-ul, <kbd>F8</kbd> ortho și <kbd>F10</kbd> urmărirea polară. Starea lor se vede în dreapta jos a vederii. Snap-ul ia doar ce arată vederea: un element pe care Vizibilitate / Grafică îl ascunde (categoria lui, un filtru, o legătură, o fază) sau care e în afara intervalului vederii nu desenează nimic, deci nu oferă niciun punct de prins, în planuri, secțiuni, fațade și 3D. Cu liniile de modelare arătate în locul desenului (butonul cu peniță din capul planului), se prinde orice linie de pe ecran.

**Ce snap-uri.** Un clic dreapta pe **SNAP** (sau pe ▾ de lângă el) arată snap-urile la obiecte: **Capăt**, **Mijloc**, **Intersecție**, **Centru**, **Cadran**, **Perpendicular** și **Cel mai apropiat**, fiecare pornit sau oprit, apoi **Snap la grilă** și unghiul urmăririi polare (la fiecare 5°, 10°, 15°, 22,5°, 30°, 45° sau 90°). Aceleași alegeri sunt în **Fișier → Preferințe… → Grilă și snap** și se păstrează la următoarea pornire.

![Meniul de snap sub plan](shots/snap-menu.webp)

**Grila.** Grila planului e la început **adaptivă**: pasul ei urmează zoomul. În **Preferințe → Grilă și snap** alege **Fixă** și scrie pasul (de exemplu 100 mm sau 1 m) și la câte linii una e desenată mai groasă. Grila păstrează atunci acel pas la orice zoom, iar cursorul face snap la ea unde niciun snap la obiecte nu ia punctul. Unde snapul **Cel mai apropiat** ține cursorul pe o linie sau o curbă și grila e pornită, punctul alunecă pe ea până la cea mai apropiată linie a grilei, așa că o coordonată vine din obiect și cealaltă din grilă (fără valori ca 1016,18). Când pasul e prea fin pentru zoom, se desenează doar liniile ei mai groase. Grila de la sol a vederii 3D ia același pas.

**UCS.** Un sistem de coordonate al utilizatorului, ca în AutoCAD: scrie **UCS** în linia de comandă, dă clic pe origine, apoi pe un punct de pe axa X (<kbd>Enter</kbd> păstrează unghiul). **W** revine la axele planului (Global), **B** îl pune în punctul de bază al proiectului, **S** în punctul topografic, cu axele spre est și nord, iar **R** îl rotește cu un unghi pe care îl scrii. Chip-ul **UCS** de lângă SNAP arată când e setat unul, iar meniul lui oferă același lucru. Într-un UCS, coordonatele scrise (`x,y`, `@dx,dy`, `d<unghi`), ortho, urmărirea polară și grila urmează axele lui, iar pictograma lui (X roșu, Y verde) stă în origine. UCS-ul se păstrează în proiect, iar setarea lui e un singur pas de undo.

![Un UCS rotit cu 30°: pictograma lui și grila de-a lungul axelor](shots/ucs.webp)

## Selectare

Dă clic pe un element ca să-l selectezi; <kbd>Shift</kbd>-clic adaugă la selecție. Ca să selectezi mai multe deodată, trage cu butonul stâng de unde nu e nimic selectat. Tras spre dreapta, chenarul e albastru și ia ce e cuprins în întregime în el (fereastră); tras spre stânga, e verde și punctat și ia tot ce atinge (crossing), ca în AutoCAD. Apasă <kbd>Space</kbd> în timp ce tragi și chenarul devine un lasou care urmează mouse-ul, cu modul pe care îl avea chenarul; <kbd>Space</kbd> din nou revine la chenar. Pe linia de comandă, când o comandă cere să selectezi obiecte, scrie **WP** sau **CP** și dă clic pe colțurile unui poligon, apoi <kbd>Enter</kbd>: un poligon fereastră sau un poligon crossing.

![Un lasou în plan: chenarul tras spre dreapta, apoi Space, și bucla în jurul celor doi pereți pe care îi ia](shots/select-lasso.webp)

## Copiere și mutare

Ca să copiezi, selectează elementul și scrie <kbd>CO</kbd>. Dă clic pe un punct de bază, apoi pe un al doilea punct. Lapin copiază mai departe până apeși <kbd>Enter</kbd> sau <kbd>Esc</kbd>. **Mută** (<kbd>M</kbd>) merge la fel.

Cât muți sau copiezi, un marcaj rămâne pe punctul de bază și o linie întreruptă merge de la el până la mouse, iar un contur estompat a ceea ce muți rămâne unde era până dai clic sau apeși <kbd>Esc</kbd>. Linia și marcajul își schimbă culoarea după snap: verde pe un punct prins, albastru când ortho sau urmărirea polară țin direcția, culoarea de accent pe o linie de urmărire, gri când e liber. Lângă mouse, Lapin arată cât ai mutat: Δx, Δy, distanța și unghiul, în unitățile proiectului. Poți scrie acolo distanța și unghiul, ca atunci când desenezi. În interfața Compact, valorile acestea sunt în linia de stare. La fel se vede și când tragi de un grip, și când tragi chiar de un element selectat, de la punctul unde ai apăsat; apasă <kbd>Esc</kbd> înainte să-i dai drumul și elementul rămâne unde era.

![Mutarea unui perete: marcajul pe punctul de bază, linia întreruptă până la mouse, fantoma peretelui unde era și deplasarea lângă mouse](shots/move-trace.webp)

Poți și să tragi. Trage de crucea din mijlocul unui element selectat ca să-l muți. Ține <kbd>Ctrl</kbd> (<kbd>⌘</kbd> pe Mac) cât tragi ca să lași o copie.

O ușă, o fereastră sau un gol din perete se mută și se copiază de-a lungul peretelui: contează doar partea mutării de-a lungul lui, așa că `500` tastat după punctul de bază îl alunecă 500 pe perete; mutat într-un alt perete, trece în acel perete. Cu peretele selectat și el, merge odată cu peretele.

O axă se mută, se copiază, se rotește și se oglindește singură sau împreună cu alte elemente. O copie e o axă nouă a aceluiași caroiaj, numită după ultima axă care merge în aceeași direcție: cu axele 1, 2 și 3, copia oricăreia e 4.

**Repetare** (**Desen → Modificare → Repetare** sau `ARRAY`) copiază forme desenate, texte și simboluri pe un șir, în rețea, în jurul unui centru sau pe un traseu și le păstrează o aranjare pe care o schimbi în proprietăți: vezi [Repetări ale desenului](groups.md#repetari-ale-desenului).

### În secțiuni și fațade

**Mută** (<kbd>M</kbd>), **Copiază** (<kbd>CO</kbd>) și tragerea unui element selectat merg și într-o secțiune sau o fațadă, în planul vederii. De-a lungul vederii, elementul se mută în plan de-a lungul liniei secțiunii; în sus sau în jos, urcă sau coboară cu decalajul lui față de nivel (decalajul bazei unui perete sau stâlp, decalajul de sus al unei grinzi, decalajul unei plăci). Cât tragi, **Decalaj față de nivel** din proprietăți îl urmează. O ușă sau o fereastră alunecă în continuare de-a lungul peretelui ei, iar liniile desenate pe vedere se mută în vedere. **Oglindește** ia acolo o linie verticală și oglindește elementele față de planul care trece prin ea, perpendicular pe vedere. Elementele nu se **Rotesc** într-o secțiune sau o fațadă, pentru că s-ar răsturna: rotește-le într-un plan sau în 3D. Liniile desenate se rotesc și se oglindesc oricum. Un element fixat rămâne unde e.

![O grindă trasă în sus în fațada de sud: fantoma ei unde era, deplasarea lângă mouse și decalajul față de nivel urmând-o în proprietăți](shots/section-drag.webp)

### Fixarea

Fixează ce nu trebuie să se miște din greșeală: un model importat, caroiajul, un desen de releveu. Selectează-l și alege **Modifică → Fixează**, sau bifează **Fixat** la **Amplasare** în proprietățile lui. Un element fixat nu se mută, nu se rotește, nu se oglindește, nu se ridică, nu trece pe alt nivel și nu se șterge, și nu are grip-uri. Celelalte câmpuri ale lui se schimbă în continuare: tipul, înălțimea sau materialul. Se poate totuși copia, iar copia nu e fixată. Un perete legat de el care se mută lasă capetele peretelui fixat unde sunt. Fixarea unui grup fixează membrii lui; un grup cu un membru fixat nu se mută. **Modifică → Eliberează** (<kbd>UP</kbd>) îl lasă din nou liber. Orice element poate fi fixat: un perete, o referință dintr-un fișier importat, o linie desenată.

**Elemente.** TRIM și EXTEND primesc și un perete cu clic într-un plan: tăiat la pereții care îl intersectează sau prelungit până la primul perete din față. OFFSET primește și elemente: un perete, o grindă sau o fundație continuă (o copie paralelă, de la axă sau între fețe). Două cuvinte din linia de comandă lucrează pe elemente: **TRIMEXTEND** (**TE**, Modifică → Taie/Prelungește elemente), care taie sau prelungește pereți, grinzi, fundații continue, plăci și plafoane față de o limită aleasă mai întâi, și **SPLITELEMENT** (**SPLIT**, Modifică → Împarte elementul), care taie unul dintre ele în două unde dai clic. Vezi [Tăierea, prelungirea, împărțirea și paralela pereților](walls.md).

## Desen

Tabul **Desen** desenează linii, polilinii, dreptunghiuri, cercuri și arce. Le editează cu paralelă, taie, prelungește, racordare, teșire și îmbinare, umple suprafețe cu hașuri și le organizează pe **Layere**. **Importă DXF** aduce un desen CAD ca linii de care te poți prinde și pe care le poți edita.

![Tabul Desen](shots/ribbon-draft.webp)

**Ellipse** (<kbd>ELL</kbd>) desenează o elipsă adevărată: centrul, capătul unei semiaxe, apoi cealaltă semiaxă (un punct sau lungimea tastată); cea mai lungă e axa mare. Tastează <kbd>A</kbd> la ultimul pas pentru un arc de elipsă, apoi dă clic pe început și pe sfârșit (în sens trigonometric). Rămâne elipsă, așezată la cel mult un milimetru ca orice curbă, și se prinde la centru și la cele patru capete ale axelor; mânerele o mută (centrul), o rotesc și o întind (capetele axei mari) sau îi schimbă cealaltă semiaxă. **Trim** o taie în arce de elipsă, iar o linie o întâlnește exact (**Trim** cu ea, **Extend** până la ea). **Offset** dă o polilinie, nu o elipsă (curba aflată la o distanță de o elipsă nu e o elipsă), așezată la cel mult un milimetru de acea curbă; spre interior cu cât raza ei cea mai strânsă sau mai mult e refuzat. **Extend** duce un arc de elipsă mai departe pe elipsa lui, iar **Fillet** unește o linie și o elipsă cu un arc tangent la amândouă (o elipsă întreagă rămâne întreagă; **Chamfer** ia doar linii). ELLIPSE dintr-un DXF intră ca elipsă, iar exportul DXF o scrie înapoi ca ELLIPSE, cum scrie cercurile și arcele ca CIRCLE și ARC. În **Edit sketch** arcele de elipsă nu se constrâng încă. **Convert to…** face dintr-o elipsă întreagă o placă sau un tavan eliptic.

**Paralelă** (<kbd>O</kbd>) copiază o linie, o polilinie, un dreptunghi, un cerc sau un arc la o distanță. Scrie distanța în unitatea proiectului (într-un proiect în milimetri `500` înseamnă jumătate de metru; `2m` sau `1'6"` își spun unitatea), fă clic pe formă, apoi pe partea în care merge; îți cere forma următoare până apeși <kbd>Enter</kbd> sau <kbd>Esc</kbd>. Scrie **T** (Through) în loc și fiecare copie trece prin punctul pe care faci clic. Colțurile unei polilinii rămân ascuțite (în teșitură), ca în CAD. Paralela merge în planuri, secțiuni, fațade, pe planuri de referință și în vederea 3D.

**Explodează** (<kbd>X</kbd>, sau **EXPLODE**) e drumul înapoi de la **Îmbină**: selectează polilinii sau dreptunghiuri și fiecare devine liniile lui, câte una pe latură, pe același strat, cu aceeași peniță și culoare și în același grup, într-un singur pas de undo. Cercurile, arcele, hașurile și șirurile de cote rămân cum sunt, iar o selecție fără nimic de desfăcut o spune. Merge în planuri, secțiuni, fațade, pe planuri de referință și pe planul de lucru din vederea 3D.

Orice lungime cerută de linia de comandă e în unitatea proiectului, iar mesajul spune care: `Depth (mm)`, `Height (ft-in)`. Unghiurile, factorii și numărătorile sunt numere simple: `90` după **Rotire** înseamnă 90°, oricare ar fi unitatea.

### Calchiat peste un PDF sau o imagine

**Fișier → Importă PDF sau imagine…** sau **Desen → PDF sau imagine** aduce un desen peste care să calchiezi: un plan scanat, o fotografie a unei fațade, un PDF de la alt birou. Intră în planul, secțiunea sau fațada din față, sub desenul ei.

- **Un PDF**: alege **Pagina** și scara la care a fost făcut desenul lui (**Desenat la 1:**). Un milimetru de hârtie măsoară atunci atâția milimetri aici, așa că un plan la 1:100 intră la mărimea lui reală. Liniile paginii intră ca desen de care te poți prinde, pe care îl poți modifica și calchia, iar imaginile ei (o scanare din PDF) ca imagini sub ele. Oricare poate fi lăsată deoparte. Pagina intră ca un singur grup numit după fișier, așa că **Mută**, **Rotire** și **Scalează** o iau întreagă; dublu clic pe ea ca să lucrezi pe părțile ei.
- **O imagine** (PNG sau JPEG): dă-i **Lățimea**; înălțimea urmează pixelii ei. **Desen → Scalează imaginea** o fixează apoi exact: selectează imaginea, fă clic pe două puncte de pe ea aflate la o distanță cunoscută și scrie distanța.

O imagine e transparentă (**Opacitate**, 0,5 implicit, în proprietățile ei) și nu face niciodată parte din model: nu se tipărește pe planșe decât dacă o lași în vedere.

![Un plan tipărit în PDF, adus pe alt nivel ca să fie calchiat](shots/trace-pdf.webp)

### Desen în vederea 3D

**Linie**, **Polilinie**, **Dreptunghi**, **Cerc**, **Arc** și editările din Desen merg și în vederea 3D, cu linia de comandă jos. Cât timp rulează o comandă, eticheta de sus spune pe ce desenezi: la cota nivelului activ sau pe planul de referință selectat. **Alege o față** din etichetă, apoi un clic pe o față a modelului, face pe fața aceea un plan de referință și desenează pe el. Punctele se prind ca într-un plan: pe liniile tăiate ale planului (colțurile și fețele pereților) și pe ce e desenat pe același plan, cu orto și liniile de urmărire. Ce desenezi e același element desenat ca unul făcut pe plan și se vede și acolo.

![Desen în vederea 3D: eticheta spune planul, linia urmează mouse-ul la cota nivelului](shots/draft-3d.webp)

### Desenele în vederea 3D

Ce desenezi pe un plan (linii, polilinii, dreptunghiuri, cercuri, arce) apare și în vederea 3D, ca linii la cota nivelului (cota nivelului, nu înălțimea de tăiere a planului); ce desenezi pe un plan de referință, o secțiune sau o fațadă stă pe acel plan. Fă clic pe una în 3D ca s-o selectezi, apoi mut-o, copiaz-o, fă-i o paralelă sau șterge-o. **Vedere → Desene în 3D**, și în **Vedere → Setările vederii** ale unei vederi 3D, le arată sau le ascunde pe toate; e pornit până îl oprești. Cotele, textele, etichetele, hașurile și liniile ajutătoare (rolul draft) rămân pe vederile lor. Un DXF adus pe o vedere stă și în 3D, pe planul acelei vederi, cu un comutator al lui (vezi [Import și export](import-export.md)).

### Ordinea desenării

Unde elementele desenate se suprapun, cel desenat mai târziu stă deasupra. Ca s-o schimbi, selectează-le (linii, polilinii, dreptunghiuri, cercuri, arce, hașuri și umpleri, texte, cote, etichete, formele unui DXF), clic dreapta și alege **Adu în față**, **Adu mai în față**, **Trimite mai în spate** sau **Trimite în spate**; **Desen → Ordine** și **Adnotări → Ordine** au aceleași comenzi, cu <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>[</kbd> și <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>[</kbd> (⌘ pe Mac). Mai în față și mai în spate trec de următorul element peste care se suprapune selecția. Desenul 2D stă peste desenul modelului; **Trimite în spatele modelului** îl pune sub secțiunea pereților și sub liniile văzute (o hașură sub un perete), iar un clic acolo ia apoi peretele; **Adu în față** sau **Trimite în spate** îl aduc din nou peste model. În linia de comandă, **DRAWORDER** (**DRO**) cere obiectele, apoi **Above objects** sau **Under objects** (dai clic pe elementele deasupra sau sub care merg, apoi <kbd>Enter</kbd>), **Front**, **Back** sau în spatele modelului, **Model**. Ordinea e a vederii, se anulează dintr-un pas, iar planșele, PDF-ul și DXF-ul o tipăresc cum o vezi; ce desenezi după aceea vine deasupra.

![Clic dreapta pe o umplere selectată: ordinea desenării](shots/draw-order-menu.webp)

![Adusă în față, umplerea gri acoperă textul](shots/draw-order-front.webp)

## Adnotări

Tabul **Adnotări** are cotele, textele, axele, cotele de nivel și etichetele. O cotă citește modelul, așa că se actualizează când se mută un perete. Cum arată textele, cotele, axele și etichetele se setează în **Gestionare → Standarde de desen**.

O **Notă cu indicator** (Adnotări → Text) e un text cu săgeată: dă clic unde arată săgeata, apoi unde stă textul, și scrie-l. Un **Nor de revizie** (Desen → Umplutură sau REVCLOUD) desenează un dreptunghi sau un poligon din arce; **Nor de revizie** din proprietățile unei polilinii închise sau ale unui cerc o face nor sau o readuce la loc. O **Scară grafică** (Adnotări → Planșe) se desenează la scara vederii. Merg pe orice vedere și pe planșe, unde li se adaugă o legendă a vederilor și câmpuri vii în texte (vezi Planșe).

### Unealta Cote

**Adnotări → Cote** (<kbd>DI</kbd>, sau scrie **DIM**) e o singură unealtă pentru toate felurile de cote. Pornește *inteligentă* și ia felul cotei după ce dai clic:

- **Două puncte** fac o cotă aliniată (sau una liniară, orizontală ori verticală, când a doua listă din bară spune **Liniară**), apoi clicul pentru locul liniei ei.
- **O linie desenată**, cu clic departe de capete și de mijloc, e cotată de la un capăt la altul (și latura unei polilinii). Dă clic pe **a doua linie** în loc de locul liniei de cotă și primești **unghiul** dintre ele; clicul următor îi pune arcul.
- **Un cerc sau un arc** primește raza, ori diametrul sau lungimea arcului când **Arce** din bară spune așa.
- **Linia unui nivel** pe care dai clic într-o secțiune sau o fațadă scrie acolo cota de nivel (fără Enter); **Enter după primul punct** scrie cota de nivel a oricărui punct.

Bara de sub vedere (a liniei de comandă) le ține pe celelalte. Prima listă alege direct felul: Aliniată, Liniară, Unghi, Rază, Diametru, Lungime de arc, Ordonată, Pantă, Nivel sau Cotă de coordonate; fiecare își cere punctele în linia de comandă, iar unealta cere apoi următoarea cotă. Lista **Câte una / Lanț / Linie de bază (de la primul)** spune cum continuă o cotă după primele două puncte: câte una, ca lanț pe aceeași linie (vechiul **Continuu**, și **C**) sau ca **linie de bază**, unde fiecare punct următor e măsurat de la primul, ca o cotă a lui pe o linie cu un pas (7 mm pe hârtie) mai departe (**B**). **Încăperi** și **Exterior** fac cotele încăperilor și pe cele exterioare, **Adaugă puncte** și **Scoate puncte** editează un lanț, **Reguli…** deschide regulile de cotare. Primele litere ale unui fel scrise la primul punct (**AN** unghi, **RA** rază, **LE** nivel…) trec la el.

![Bara uneltei Cote: felul, două puncte, arcele, modul lanțului și cotele generate](shots/dimension-tool-bar.webp)

![Ce a făcut unealta inteligentă: o linie cotată cap la cap, unghiul dintre două linii, raza unui cerc, două puncte](shots/dimension-smart.webp)

Vechile butoane au plecat de pe panglică, dar merg în continuare: un loc rapid, Uneltele mele, o scurtătură sau literele lui Liniară, Unghi, Rază, Diametru, Lungime de arc, Pantă, Ordonată și Cotă de coordonate pornesc unealta Cote la acel fel, iar **Repere → Nivel** (<kbd>LL</kbd>) o pornește la Nivel. Comenzile scrise (**DIMALIGNED**, **DIMLINEAR**, **DIMANGULAR**, **DIMRADIUS**…) rămân la fel.

O cotă aliniată sau liniară este implicit una singură: dai clic pe cele două puncte ale ei, apoi pe locul liniei, și comanda cere următoarea cotă. **Esc** o încheie. Alege **Lanț** în bara de comenzi (sau scrie **C**; se ține minte) și cota continuă ca lanț: după primele două puncte și linia ei, fiecare clic adaugă punctul următor pe aceeași linie, iar valoarea fiecărui segment se vede cât desenezi. Ca să închei lanțul, apasă **Enter**, dă clic dreapta, apasă butonul **Finalizează lanțul** din bară sau dă clic din nou pe ultimul punct (merge și dublu-clic). **U** scoate ultimul punct. **Esc**, o altă unealtă sau o altă comandă păstrează lanțul desenat până atunci și o spune; anularea îl scoate. O cotă liniară păstrează direcția dată (orizontală sau verticală, după partea punctelor pe care pui linia) până ești clar de cealaltă parte; **Tab** o răstoarnă, iar bara arată care este. Un lanț se poartă ca o polilinie: un clic îl selectează tot și se mută, se copiază și se șterge ca un tot. Un lanț care măsoară pereți, muchii sau axe de rețea rămâne prins de ele: tras sau mutat singur, doar linia de cotă alunecă, perpendicular pe lanț, deci o scăpare a mouse-ului nu-l lasă să măsoare în gol; mutat împreună cu ce măsoară, merge cu ele. Grip-urile lui stau pe fiecare punct măsurat (tragi ca să-l muți, Alt-clic sau Ctrl/⌘-clic ca să-l scoți), la mijlocul fiecărui segment (tragi ca să muți linia de cotă prin punct; Ctrl/⌘-tragere adaugă un punct unde îl lași, Ctrl/⌘-clic adaugă unul la jumătate) și la capetele liniei de cotă (tragi ca să muți linia; Ctrl/⌘-clic ca să continui: clicurile următoare adaugă puncte dincolo de acel capăt, ca la continuarea unui lanț în Revit, până la **Enter** sau **Esc**). Ca să pui puncte noi într-un lanț, ca în ArchiCAD, selectează-l și apasă **Adnotări → Adaugă puncte** (sau scrie **DIMADD**, ori **Adaugă puncte** în proprietățile lui): fiecare punct pe care dai clic intră în lanț acolo unde cade pe linia lui, între punctele vecine sau dincolo de un capăt, ca nou capăt, și se prinde de peretele, muchia sau axa pe care cade; **Enter** sau **Esc** încheie. Ca să scoți puncte, Ctrl/⌘-clic pe grip-urile lor, sau apasă **Adnotări → Scoate puncte** (**DIMDEL**, ori **Scoate puncte** în proprietăți) și dă clic lângă fiecare punct; segmentele de o parte și de alta devin unul. Și valoarea fiecărui segment are un grip: trage-l ca să muți valoarea din locul ei acolo unde segmentele sunt scurte și valorile s-ar suprapune; când iese de lângă segmentul ei, o linie de indicație subțire o leagă înapoi. Ctrl/⌘-clic pe grip, sau **Textele înapoi pe linie** în proprietăți, o pune la loc. O valoare mutată rămâne cu segmentul ei când se mută punctele și se rotește odată cu lanțul. Dublu-clic pe un segment ca să-i scrii valoarea: elementul măsurat se mută, și odată cu el tot ce urmează în lanț. Proprietățile arată valoarea și textul fiecărui segment, despart un lanț în cote simple și unesc cotele simple care se continuă pe aceeași linie. Un clic dreapta pe cotele selectate oferă la fel: **Împarte în cote simple**, iar cu mai multe selectate **Unește cotele selectate** și **Aliniază cotele selectate**, care pune fiecare lanț paralel cu primul pe linia de cotă a primului (celelalte rămân cum sunt), într-un singur pas de anulare.

Adnotările de pe un plan (cote, texte, linii desenate, etichete) pot trece pe alt plan, în același loc, pentru că planurile au aceleași coordonate: selectează-le și alege planul în proprietăți, la **View** ca să le muți sau la **Copy to** ca să le copiezi. Așa ajung și cotele făcute pe o planimetrie pe planul nivelului. Punctele unei cote se agață apoi de ce taie noul plan în acel loc: pereți, muchii, axe.

**Înălțimea golurilor.** Un segment al unui lanț de cote care merge de la un glaf la celălalt al unei uși, ferestre sau al unui gol scrie lățimea deasupra liniei și înălțimea golului sub ea, cum se face în desenele românești și europene. E citită din model de fiecare dată, deci urmează golul când e redimensionat, mutat sau primește alt tip; un segment peste care ai scris un text nu scrie înălțimea. **Gestionare → Standarde de desen → Dimensiuni → Înălțimea golului** o alege pentru fiecare stil de cotă: oprită, `150`, `h=150`, `h=150` cu parapetul `p=90` sub el, sau `150/90`, în unitatea stilului. Merge în planuri, secțiuni și fațade, iar planșele, PDF-ul și DXF-ul o tipăresc. Proiectele noi o au pornită (doar înălțimea); un proiect făcut înainte își păstrează cotele cum erau până o pornești acolo.

![Înălțimile ușii și ferestrei sub segmentele lor](shots/dimension-opening-height.webp)

![Înălțimea golului în stilurile de cotă](shots/dimension-opening-height-style.webp)

**Zerourile de la final.** O cotă cu trei zecimale scrie `2.500` și `3.000`. Bifează **Gestionare → Standarde de desen → Proiect → Suprimă zerourile de la final** și toate cotele le lasă deoparte: `2.5` și `3`, ca în programele CAD. Fiecare stil de cotă poate alege și singur, în coloana **Zerouri de la final**, lângă **Zecimale**: *ca proiectul* (implicit), *păstrează (2.500)* sau *suprimă (2.5)*. Se aplică valorilor cotelor liniare, aliniate, de rază, de diametru, de lungime de arc și de ordonată și înălțimilor golurilor de sub ele, pe ecran, pe planșe, în PDF și în DXF; picioarele și țolii, scriși în fracții, nu au zerouri de lăsat deoparte. E oprit până îl pornești, așa că desenele existente arată la fel.

**Liniile ajutătoare și linia de cotă.** Ca în stilurile de cotare din AutoCAD, fiecare stil din **Gestionare → Standarde de desen → Dimensiuni** spune cum se desenează liniile lui ajutătoare și linia de cotă. **Linii ajutătoare omise** lasă deoparte prima linie ajutătoare a unui lanț, ultima (*fără a doua*) sau pe amândouă, acolo unde fața unui perete sau altă cotă arată deja punctul; o cotă poate alege și singură, în proprietățile ei, la **Linii ajutătoare omise** (*ca stilul* când e gol, sau *toate afișate* ca să le aducă înapoi la un stil care le omite). **Linie ajutătoare fixă mm** dă fiecărei linii ajutătoare aceeași lungime pe hârtie, măsurată înapoi de la linia de cotă spre punct, ca un lanț departe de perete să nu tragă linii lungi peste plan; un punct mai aproape de linia lui decât atât își păstrează linia de la distanță, ca înainte, iar gol înseamnă de la distanță. **Peniță linii ajutătoare mm**, **Culoare linii ajutătoare** și **Linie ajutătoare**, apoi **Peniță linie de cotă mm**, **Culoare linie de cotă** și **Linie de cotă**, dau fiecăreia propria peniță, culoare și tip de linie (o linie ajutătoare întreruptă, o linie de cotă mai groasă); săgețile și liniuțele iau culoarea liniei de cotă, valorile rămân în culoarea stilului. Goale, se desenează ca înainte: peniță de 0,13 mm, culoarea stilului, linie continuă, așa că desenele existente arată la fel. Se aplică cotelor liniare, aliniate, unghiulare și de lungime de arc; le desenează motorul, așa că planul, planșele, PDF-ul și DXF-ul le arată la fel, iar un fișier de standarde salvat le păstrează.

![Un lanț cu liniile ajutătoare de la capete omise și celelalte de lungime fixă, cu peniță proprie](shots/dimension-extension-lines.webp)

![Coloanele liniilor ajutătoare și ale liniei de cotă din stilurile de cotare](shots/dimension-extension-lines-style.webp)

**Cotele de nivel.** Cotele de nivel se scriu în metri, cu două zecimale: `+3.00`, `−0.15`, `±0.00`. **Gestionare → Standarde de desen → Proiect** alege câte, cu **Zecimale la cote de nivel** (de la 0 la 4: `+3`, `+3.0`, `+3.00`, `+3.000`, `+3.0000`), și dacă își lasă deoparte zerourile de la final, cu **Zerouri de la final la cote de nivel**: *ca proiectul* (implicit, deci urmează **Suprimă zerourile de la final**), *păstrează (+3.150)* sau *suprimă (+3.15)*. Cu trei zecimale o cotă de nivel se scrie `+3.150`, suprimată `+3.15`, iar o valoare întreagă `+3.000` devine `+3`. Se aplică oriunde un desen scrie o cotă de nivel: marcajele nivelurilor din secțiuni și fațade (ale lui Lapin și un simbol de nivel al tău, cu `{level.elevation}`), cotele de nivel punctuale (`{point.elevation}`), cotele de nivel ca dimensiuni, punctul de bază și punctul topografic, cotele terenului. O cotă de nivel pusă dintr-un script își poate avea propriile valori (`decimals`, `zeros`). Într-un proiect imperial cotele de nivel rămân în picioare și țoli.

**Cote pe o linie.** Un punct de cotă pus pe o axă din grilă sau pe axa unui perete se ține de linie, nu de un punct al ei. Trage capătul axei de-a lungul ei ca să încapă bula în vedere, sau lungește ori scurtează peretele: cota măsoară mai departe unde ai pus-o, iar linia ei nu se rotește. Dacă axa sau peretele se mută de-a curmezișul, punctul merge cu ea, în același loc de-a lungul. La fel rămâne, când peretele e întins pe lungime, o cotă pusă pe fața lui. Cotele făcute înainte se țin de o axă din grilă la fel; una pe axa unui perete se atașează din nou dând clic pe punctul ei.

**Ce urmează o cotă.** Un punct de cotă pus pe un element se ține de el și îl urmează când elementul e mutat, rotit sau modificat: fața sau axa unui perete, glaful unei uși, al unei ferestre sau al unui gol, o axă din grilă, un stâlp, și ce nu taie planul: conturul unei scări (laturile ei, marginile rampelor și ale podestului), o grindă văzută deasupra, marginea unei plăci sau a unui tavan, o piesă de mobilier, o balustradă, un solid Studio văzut dedesubt. Punctul e păstrat în sistemul elementului, așa că o scară mutată sau rotită își ia cu ea cotele de pe colțuri, iar lărgită, cota de pe latura ei urmează latura. Un punct pus pe nimic sau pe o linie desenată rămâne unde ai dat clic. Cotele făcute înainte pe o scară, un stâlp sau o piesă de mobilier rotite le urmează din clipa în care se deschide proiectul.

### Cote exterioare și reguli de cotare

**Annotate → Exterior dimensions** (<kbd>ED</kbd>, sau tastează **EXTDIM**) cotează dintr-o dată exteriorul clădirii pe un plan. Fiecare fațadă dreaptă primește în exterior lanțuri de cote paralele, unul lângă altul, în ordinea dată de regulile de cotare, începând de la perete. Punctele se țin de pereți, ca la orice cotă.

- Cu pereți selectați, se cotează doar fațadele lor. Fără selecție, se cotează toți pereții exteriori ai nivelului planului.
- Cerută pe o planimetrie, lanțurile se fac pe planul nivelului.
- Rulează din nou comanda și lanțurile făcute de ea pe plan sunt înlocuite, nu dublate. Lanțurile desenate de tine rămân.

**Annotate → Dimension rules…** (Reguli de cotare…) deschide setările pentru Cote exterioare și Cotele încăperilor, în două taburi. Aparțin proiectului și circulă cu standardele de desen (**Gestionare → Standarde de desen**, cu opțiunile). Fiecare lanț are o bifă și butoane **↑ ↓** pentru ordine: la exterior de la perete spre afară, la încăperi de la latură spre interior.

| Lanț | Ce măsoară |
| --- | --- |
| Detalii | fiecare element de pe fațadă: colțuri și ancadramentele golurilor |
| Goluri | de la începutul fațadei până la centrul fiecărei uși și ferestre și până la capătul lor |
| Axe | de la început până la fiecare ax al grilei care intersectează fațada și până la capăt |
| Camere | grosimile pereților care se întâlnesc cu fațada dinspre interior și distanțele dintre ei |
| Total | întreaga fațadă |

Cotele încăperilor au doar **Detalii** (latura, cu ancadramentele golurilor) și **Total** (lungimea întreagă a laturii). Pe lângă lanțuri:

- **Distanța** este măsurată în milimetri pe hârtie de la fața peretelui (sau latura încăperii) până la primul lanț, iar **Spațierea** este distanța dintre lanțuri.
- **Stil de cotare** este unul dintre stilurile de cotare ale proiectului; când e gol, stilul implicit.
- **Măsoară până la** (doar la exterior) spune până la ce se măsoară colțurile și pereții despărțitori: fețele pereților, fețele miezului lor sau axele lor.
- **Omite golurile mai înguste de** lasă deoparte golurile sub lățimea dată, în metri.
- **Resetează la valorile implicite** aduce dialogul la regulile din program; nimic nu se schimbă până la **OK**, care e un singur pas de anulare.

**Plasarea automată a textului.** Unde segmentele sunt scurte, un glaf de 10 cm lângă o fereastră de 2,40 m, valoarea nu încape între liniile ei de cotă. Lapin o mută pentru tine: chiar în afara segmentului, pe latura liberă (capătul lanțului sau un vecin destul de lat ca să-și păstreze valoarea), iar unde ambele laturi sunt ocupate, pe un rând deasupra liniei sau sub ea, pe rând, așa că valorile apropiate se așază în trepte. O valoare nu trece niciodată peste altă valoare sau peste altă linie de cotă din vedere, deci lanțurile unei cotări exterioare, sau ale unei încăperi, se feresc unele de altele. Mutată mai departe de o distanță dată, primește o linie subțire de indicație spre segmentul ei. Valorile care încap rămân exact unde au fost mereu, iar o valoare trasă de tine de mâner își păstrează locul (**Textele înapoi pe linie** o dă înapoi plasării automate). Setarea e la **Plasarea textului**, jos în **Adnotări → Reguli de cotare…**: **Plasare automată a textului** (pornită în orice proiect, nou sau vechi) și **Linie de indicație când e mutată peste**, în milimetri pe hârtie (5 implicit). Fiecare stil de cotă poate alege singur în **Gestionare → Standarde de desen → Dimensiuni**: **Plasarea textului** *ca proiectul*, *pornită* sau *oprită*, și **Indicație peste mm** (gol: ca proiectul). Motorul plasează valorile, deci ecranul, planșele, PDF-ul și DXF-ul le arată la fel; ele se socotesc în milimetri pe hârtie, deci același lanț la 1:50 are nevoie de mai puține mutări decât la 1:100.

![O cotare exterioară cu valorile segmentelor scurte în afara lor și în trepte](shots/dimension-text-placement.webp)

### Etichete

**Adnotări → Etichetă** (<kbd>TG</kbd>) pune pe o vedere o casetă cu ce spune un element despre el, cu o linie de indicație până la el. E o singură unealtă pentru toate felurile de etichete: prima listă din bara de sub vedere spune ce etichetează.

- **Element**: marca, numele, clasa sau tipul, **Rezistență la foc**, **Izolare acustică**, **Transmitanță termică** sau **Alt parametru…** după nume; ori un tip de etichetă din standardele de desen. **TOATE UȘILE ȘI FERESTRELE** etichetează deodată toate ușile și ferestrele planului (ca **Adnotări → Etichete uși și ferestre**).
- **Încăpere**: dă clic în interiorul unei încăperi pentru numele, numărul, aria, funcțiunea, înălțimea sau finisajul pardoselii. **ȘTAMPILELE ÎNCĂPERILOR…** deschide ce arată ștampila fiecărei încăperi (la fel ca **Eticheta încăperii…**).
- **Parametru**: scrie numele parametrului în bară (**Rezistență la foc**, `Pset_WallCommon.FireRating`). **Adnotări → Etichetează un parametru**, plecat acum de pe panglică, pornește în continuare unealta aici.
- **Material / straturi**: un tip de [etichetă de stratificație](#eticheta-de-stratificatie); **TIPURI DE ETICHETE…** deschide editorul lor.
- **Notă de cod**: codul clasificării elementului (sau titlul ei), vezi [Clasificări](classifications.md).

**INDICAȚIE** pornește sau oprește linia de indicație. Dă clic pe element, apoi unde merge eticheta (tastează întâi **E** ca să frângi linia într-un cot). Scrise la cererea elementului, **ELEMENT**, **ROOM**, **PARAMETER**, **MATERIAL** și **KEYNOTE** aleg și ele sursa.

![Unealta Etichetă la sursa Încăpere: aria încăperii](shots/tag-sources.webp)

![Bara uneltei Etichetă sub vedere](shots/tag-options.webp)

O etichetă citește orice parametru: al elementului, al tipului lui (atunci îl arată toate elementele tipului), o proprietate standard ca `Pset_WallCommon.FireRating` sau una cu care a venit un element importat. Numele se găsește și fără spații, deci **Fire rating** și `FireRating` sunt același. Ca să-l completezi, folosește **Parametri** în proprietățile elementului sau ale tipului: scrie un nume (îți sunt propuse Fire rating, Sound rating, Moisture resistance și Thermal resistance) sau alege **Adaugă o proprietate standard…** pentru proprietățile IFC ale clasei lui. Eticheta urmează de îndată ce există o valoare, iar lungimile se scriu în unitățile proiectului.

O etichetă selectată are mânere. Trage de casetă ca s-o muți: linia de indicație arată în continuare unde arăta. O etichetă merge cu elementul ei: când elementul e mutat, rotit, oglindit sau remodelat din mânere, eticheta îl urmează, păstrându-și locul de lângă el, iar linia de indicație rămâne pe el; o etichetă pe care ai mutat-o își păstrează distanța pe care i-ai dat-o. Trage de mijlocul liniei ca s-o frângi într-un cot (Ctrl/⌘-clic pe cot o îndreaptă din nou) și de capătul ei ca s-o îndrepți spre alt punct (Ctrl/⌘-clic: înapoi pe mijlocul elementului). În proprietăți schimbi ce **Arată** eticheta, îi dai un **Text** propriu (`{mark}|{FireRating}` scrie două rânduri), un **Tip de etichetă**, sau oprești **Linia de indicație**. Etichetele le desenează motorul, așa că planșele și PDF-urile le tipăresc cum le vezi. Tipurile de etichetă din **Gestionare → Standarde de desen → Etichete** citesc aceleași câmpuri în șabloanele lor; un proiect nou are printre ele Rezistență la foc, Izolare acustică și Transmitanță termică.

**Săgeți ca etichetele din ArchiCAD.** Implicit, săgeata unei etichete e *frântă*: o linie oblică de la element până la un cot, apoi un umăr care trece pe sub text și subliniază primul lui rând, fără casetă în jurul textului. După clicul pe element, următorul clic e cotul, iar textul stă lângă el. Trage mânerul cotului: linia oblică păstrează 30°, 45°, 60° sau 90° față de capătul ei (ține <kbd>Shift</kbd> pentru orice unghi), iar trasă dincolo de capăt textul trece pe partea aceea, aliniat spre cot. Trage textul ca să-l muți cu cotul lui. În **Gestionare → Standarde de desen → Etichete**, **Săgeată…** pe un tip de etichetă (și editorul de stratificație) setează **Forma săgeții** (frântă sau dreaptă, dintr-o casetă), ce subliniază umărul (**Subliniere**: primul rând, fiecare rând, ultimul rând sau nimic, când se oprește la marginea textului) și **Prelungire mm**, cât se prelungește umărul de la text, lângă previzualizarea motorului. Cu câte o săgeată pe strat, fiecare se termină sub rândul ei, cu coturile pe o coloană; tras, cotul mută coloana. **Forma săgeții** din proprietățile unei etichete o înlocuiește pe a tipului; o etichetă a cărei săgeată a fost frântă într-un cot liber își păstrează săgeata dreaptă. Etichetele din proiectele făcute înainte își păstrează săgeata dreaptă, în casetă.

![Linia de indicație a unei etichete frântă în cot, cu mânerele ei](shots/tag-leader.webp)

![Proprietățile unei etichete](shots/tag-properties.webp)

Marcajul unei uși sau ferestre se modifică parte cu parte, fiecare text mutat sau rotit și fiecare capăt al liniei lungit: vezi [Uși și ferestre](doors-windows.md).

**Rotirea unei etichete.** În proprietățile unei etichete, **Rotire** întoarce textul și umărul sau chenarul ei cu un unghi, în grade față de orizontală, iar **Aliniază la element** o face să urmeze direcția elementului etichetat (axa unui perete sau a unei grinzi, cea mai lungă muchie a altceva) și să se întoarcă odată cu el; rotirea se adaugă atunci la acea direcție. O etichetă e desenată mereu în sens de citit, niciodată cu susul în jos. O etichetă selectată are deasupra textului un mâner de rotire: trage-l din 15° în 15° (și 45°, 90°; ține <kbd>Shift</kbd> pentru orice unghi), iar Ctrl/⌘-clic pune textul din nou orizontal. Linia de indicație ajunge tot la element. Etichetele simple și cele de stratificație se întorc la fel, în planuri, secțiuni și fațade, iar planșele, PDF-ul și DXF-ul le tipăresc ca le vezi.

**Ce etichetează o etichetă.** Cât timp unealta **Etichetă** așteaptă un element, cel de sub cursor e evidențiat cu culoarea de accent; ține cursorul pe o etichetă pusă, sau selecteaz-o, și elementul pe care îl etichetează e evidențiat.

![O etichetă aliniată la peretele ei, cu mânerul de rotire, și peretele etichetat evidențiat](shots/tag-rotate.webp)

**Câmpurile altor elemente.** Textul unei etichete poate citi dincolo de elementul ei, cu un domeniu înainte de punct: `{storey.code}` (parametrul nivelului), `{project.number}`, `{host.FireRating}` (peretele unei uși), `{room.name}`, la o ușă `{fromRoom.number}` și `{toRoom.number}`, iar parametrii proprii ai proiectului doar prin cheia lor. Un câmp poate fi și o formulă: `{= upper(storey.code) & "." & number}`, `{= area * 1.1 | 0.00}`. Lista **Arată** din proprietățile unei etichete le grupează după locul de unde vin (nivelul, proiectul, gazda, încăperea). Vezi [Parametri calculați prin formule](interface.md#parametri-calculați-prin-formule).

#### O etichetă desenată ca simbol

Un tip de etichetă, sau eticheta încăperilor, poate fi un desen al tău în loc de text. Desenează eticheta ca [simbol](#simboluri) (**Gestionare → Simboluri…**): liniile, casetele și cercurile ei cum trebuie să arate și un text oriunde vine o valoare, fiecare cu câmpuri ca un șablon — pentru o încăpere, `{storey.code}.{Hall}.{Flat}.{number}` într-o casetă, `{name}` lângă ea, `{area}` și `{Floor finish}` dedesubt. Lista **Câmpuri** a editorului de simboluri oferă câmpurile elementului etichetat și parametrii nivelurilor tale. Apoi, în **Gestionare → Standarde de desen → Etichete**, alege simbolul lângă tipul de etichetă (**Doar text** e textul simplu), sau în **Eticheta încăperii…**, **Desenat ca**. Fiecare etichetă de acel tip, eticheta fiecărei încăperi, se desenează ca simbolul în punctul ei, cu textele completate din elementul ei; urmează elementul și ce citește el (codul unui nivel schimbat schimbă toate etichetele). O etichetă își păstrează linia de indicație, pornită din caseta simbolului; câmpurile proprii ale simbolului (`{view.scale}`, `{attr.…}`) merg în continuare. Un simbol șters lasă textul tipului.

![Eticheta unei încăperi desenată ca simbol propriu: K1.1 | Living / 24.5 m² | Oak](shots/room-tag-symbol.webp)

Și marcajul unei uși sau al unei ferestre (**Adnotare → Etichete uși și ferestre**) poate fi desenat ca simbol: alege-l lângă tipul marcajului (**Door marker**, **Window marker**). Marcajul își păstrează linia peste perete și textul interior (lățime/înălțime, parapetul), iar simbolul stă dincolo de capătul exterior al liniei în locul textului exterior, rotit cum stau textele (de-a lungul peretelui sau de-a lungul liniei). Textele lui citesc ușa sau fereastra: `{mark}`, `{width}`, `{height}` și `{sill}` cum le scrie marcajul (în unitatea tipului), orice parametru, formule. În plan, simbolul are mânerele textului exterior: trage-l ca să-l muți, rotește-l din mânerul lui; Ctrl/⌘-clic îl pune la loc. Fără simbol, marcajul se desenează ca înainte.

![Marcaje de uși desenate ca simbol propriu și o ușă rezistentă la foc cu marcajul ei, dat de o regulă de etichetare](shots/tag-marker-symbol.webp)

#### Reguli de etichetare

Sub tipurile de etichete, **Reguli de etichetare** alege tipul unei etichete după ce este elementul ei. O regulă are o **Clasă** (IfcDoor, IfcWindow, IfcSpace…), o **Condiție** și **Tipul de etichetă** pe care îl dă. Condiția e o formulă peste câmpurile elementului, cum le citește `{= …}`, scrisă simplu sau între acolade: `width > 1.2` (la uși și ferestre `width`, `height` și `sill` în metri), `FireRating != ""`, `IsExternal`, `storey.code == "P"`. Este îndeplinită când e adevărată (un număr diferit de 0, un text nevid). Pentru fiecare clasă regulile se încearcă în ordinea lor și prima îndeplinită câștigă; săgețile mută o regulă în sus sau în jos printre cele ale clasei ei, coșul o șterge, **Regulă nouă** adaugă una. O regulă îndeplinită își dă tipul fiecărei etichete din clasa elementului, peste cel cu care a fost făcută eticheta — inclusiv marcajele ușilor și ferestrelor și etichetele încăperilor —, la fiecare desenare, așa că o ușă lărgită ia marcajul ușii late. Fără reguli, sau fără una îndeplinită, fiecare etichetă își păstrează tipul. Un tip de etichetă șters își ia regulile cu el. Regulile merg cu standardele (**Exportă standardele…**, un fișier `.lapinstandards`, în care fiecare regulă e o intrare a previzualizării importului) și în proiect.

![Regulile de etichetare sub tipurile de etichete](shots/tag-rules.webp)

O etichetă poate avea totuși tipul ei: alege un **Tip de etichetă** în proprietățile ei (la un marcaj, printre tipurile de marcaj) și regulile îl lasă așa; **După reguli** îl dă înapoi regulilor. Debifează **Vizibil** ca să oprești o singură etichetă: nu se tipărește nicăieri (planșe, PDF, DXF) și rămâne pe ecran ca un contur estompat, ca s-o poți selecta și porni din nou.

#### Eticheta de stratificație

O etichetă de stratificație scrie straturile unui element ca o notă de arhitectură: un antet (*EXTERIOR*), un rând pentru fiecare strat, un final (*INTERIOR*). Un proiect nou are **Build-up (exterior → interior)**: fiecare rând e descrierea materialului (numele lui, dacă nu are descriere), λ-ul lui dacă îl are și grosimea stratului în mm. Alege-o în bara uneltei **Etichetă** și dă clic pe un perete, o placă, un acoperiș sau finisajul unei încăperi; celelalte elemente primesc câmpul ales. Textul se citește din model de fiecare dată când se desenează: schimbi un strat, descrierea unui material sau λ-ul lui, iar eticheta urmează.

![Două etichete de stratificație pe un perete de fațadă: o singură săgeată și câte o săgeată pe strat](shots/buildup-tag.webp)

Forma ei se stabilește în **Gestionare → Standarde de desen → Etichete**: pune **Forma** unui tip de etichetă pe *stratificație* sau dă clic pe **Stratificație…** la unul, ca să-l editezi lângă o previzualizare pe unul dintre tipurile de perete sau de placă ale proiectului.

- **Antet** și **Final**: scrise deasupra și dedesubtul straturilor (gol: niciunul; `|` începe un rând nou).
- **Fiecare strat**: rândul scris o dată pentru fiecare strat. Citește câmpurile stratului, `{description}`, `{material}`, `{thickness}` (în unitatea aleasă la **Grosimea în**), `{note}` (nota stratului în tipul lui), `{function}` (miez, finisaj, izolație…) și `{index}`; orice parametru al materialului după nume sau simbol, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (vezi [Datele](materials.md#date-descriere-parametri-cum-se-masoara) materialului); și pe ale elementului, `{name}`, `{type}`, `{mark}`. **Inserează un câmp…** pune unul unde e cursorul și arată parametrii pe care îi au materialele proiectului.
- **Parantezele drepte** fac o parte opțională: `[, λ = {λ} W/mK]` se scrie doar unde materialul are λ; o parte cade întreagă când un câmp din ea e gol. **[ ]** face din selecție o astfel de parte. Ca să scrii chiar o paranteză, tastează `\[` sau `\]`.
- **Ordine**: de la exteriorul spre interiorul unui perete (partea primului lui strat e exteriorul, deci un perete oglindit începe tot de acolo), de sus în jos la o placă sau un acoperiș; ori invers. Alege ordinea în care straturile stau lângă notă, ca săgețile pe straturi să nu se încrucișeze.
- **Straturile fără grosime** (membrane, folii, vopsea) se scriu, dacă nu debifezi **arătate**. Un strat care își schimbă materialul pe înălțime scrie un rând pentru fiecare material.
- **Săgeți**: una, spre element, sau **câte una pe strat**, fiecare de la rândul ei până într-un punct din stratul ei, acolo unde vederea deosebește straturile (un perete în planuri și secțiuni, o placă în secțiuni; în rest eticheta păstrează una). Trage capătul săgeții de-a lungul elementului ca să-l traverseze în alt loc.
- **Rândurile încep cu** o bulină, o liniuță, numărul lor sau nimic; se rup la **Lățime mm** pe hârtie, aliniate după bulină, iar o valoare rămâne lângă simbolul și unitatea ei (`λ = 0.035 W/mK`).

![Editorul unui tip de etichetă de stratificație, lângă previzualizarea lui](shots/buildup-tag-type.webp)

Ca orice etichetă, eticheta de stratificație își urmează elementul, iar planșele și PDF-urile o tipăresc cum o vezi, cu λ, μ și ρ cu tot. Un **Text** propriu în proprietățile ei înlocuiește rândul fiecărui strat doar pentru eticheta aceea.

### Tabele

**Adnotare → Tabel** (TT) pune un tabel al tău pe un plan, o secțiune, o vedere de desen sau hârtia unei planșe: alege **rândurile**, **coloanele** și aspectul în bara de comandă (**Rând de antet**, **Grilă**, **Minimal**, **Fără linii**), apoi clic unde vine colțul lui din stânga sus. Se deschide editorul; dublu clic pe tabel, sau **Editează tabelul…** în proprietățile lui, îl deschide din nou.

- **Celulele** țin text, pe mai multe rânduri (Shift+Enter în celulă). Scrie într-o celulă sau dă dublu clic pe ea; Enter păstrează textul și coboară, Tab merge la dreapta, Esc o lasă cum era. Celulele copiate dintr-un tabelar se lipesc așa cum sunt.
- **Formulele** se scriu ca într-un cartuș, `{= …}`, și citesc celulele tabelului după adresa lor: `{= B2 * C2}`, `{= sum(D2:D9)}`, `{= average(B2:B5) | 0.0}` (un format după bară), `{= count(A2:A20)}`. O celulă de text care începe cu un număr contează ca acel număr. Adaugi sau scoți un rând ori o coloană, și formulele urmează, ca într-un tabelar; o formulă care își citește propria celulă, sau o celulă scoasă, arată `<…>`, iar editorul spune de ce. Pe o planșă, o celulă citește și câmpurile planșei: `{sheet.number}`, `{project.name}`.
- **Alte tabele.** Dă unui tabel un nume dintr-un cuvânt în editorul lui (**Areas**, sus), și orice tabel îi citește celulele după acest nume: `{= table.Areas.B4 * 2}`, `{= sum(table.Areas.B2:B9)}`, sau `{table.Areas.A1}` ca să arate o celulă așa cum se citește. **Alt tabel**, sub grilă, arată celelalte tabele: clic pe o celulă (Shift+clic pentru un interval), iar **Inserează referința** o pune în celula selectată unde era cursorul în bară, într-o formulă așa cum e, în rest ca un câmp sau ca un total. Un nume se caută întâi pe aceeași planșă, deci o planșă duplicată cu tabelele ei le citește pe ale sale. Redenumești un tabel și ce îl citește urmează; îl ștergi și ce îl citea arată `REF`; rândurile și coloanele adăugate sau scoase din el mută referințele altor tabele la el, ca într-un tabelar. O formulă care se întoarce la propria celulă printr-un alt tabel arată `<…>`, iar ambele tabele spun de ce.
- **Din model.** **Leagă elemente…** ascunde editorul în spatele unei bare: selectează elemente în orice vedere, apoi leagă-le **La celulele selectate**, **Câte un rând fiecare** (de la primul rând selectat: se adaugă rânduri după nevoie și textele primului rând se copiază în jos, deci un rând scris o dată le listează pe toate) sau la **Tot tabelul**; **Dezleagă** scoate legăturile. O celulă legată își citește elementul prin `el.`: `{el.name}`, `{el.FireRating}`, `{el.storey.name}` arată un câmp așa cum îl scrie o etichetă, `{= el.Area * 1.1 | 0.00}` îl citește ca număr (m², m, m³), cu orice câmp pe care îl poate lista un extras. Măsurile se citesc după numele lor obișnuite la orice element: **Area**, **NetArea**, **GrossArea**, **Perimeter**, **Volume**, **Weight** sunt aria netă a pardoselii, perimetrul și volumul unei încăperi, aria netă a feței unui perete, aria netă a unei plăci, aria unei uși sau a unei ferestre, așa cum le numesc cantitățile ei (`{el.NetFloorArea}`, `{el.NetSideArea}` le citesc și ele). **Inserează un câmp…** de sub bară listează câmpurile elementului cu valorile lor de acum, printre ele cantitățile și numele lor obișnuite. O celulă citește și un extras după nume, `{= schedule.Doors.count}`, `{schedule.Rooms.total.Area}` (linia lui de total), `{= schedule.Rooms.sum.Area}` (sau `average`, `min`, `max`), `{schedule.Rooms.B3}` (o celulă așa cum se tipărește, rândul 1 fiind titlurile), și un filtru de vedere din **Vizibilitate / Grafică**, `{= filter.PeretiPortanti.count}`, `{= filter.PeretiPortanti.sum.Length | 0.0}`; un nume cu spații se scrie fără ele („Pereti portanti”: `PeretiPortanti`). Nimic nu se copiază: tabelul citește modelul de fiecare dată când e desenat, deci urmează orice schimbare. Dacă un element legat e șters, celulele lui arată `<el.…>` și editorul spune asta; anularea aduce valoarea înapoi.
- **Sugestii în timp ce scrii.** După `{` sau `{=`, într-o celulă sau în bara de deasupra grilei, o listă propune ce se potrivește unde scrii: funcțiile cu felul în care se apelează (`round(x; decimals)`, `if(condition; then; else)`, `sum(A1:A9; …)`), apoi `el.` și câmpurile elementului legat cu valorile lor, `schedule.` și extrasele proiectului, apoi `count`, `total.`, `sum.`… și coloanele lor, `filter.` și filtrele de vedere, `table.` și celelalte tabele cu celulele lor așa cum se citesc. Ce scrii restrânge lista; ↑ ↓ aleg, Enter sau Tab ia elementul marcat, Esc închide lista. Lista o face motorul, deci ce propune se citește. O formulă scrisă pe jumătate nu e marcată greșită la fiecare tastă: erorile celulei apar când te oprești o clipă din scris sau când îi păstrezi textul.
- **Sumă automată.** **Σ** (sau Alt+=, ca într-un tabelar) cu o celulă goală selectată scrie `{= sum(…)}` al numerelor chiar de deasupra ei, până la un rând de antet, o celulă goală sau un text, altfel al celor chiar din stânga ei; cu mai multe celule selectate, pune un total sub fiecare coloană a lor, în primul rând gol de sub ele, adăugând un rând când nu e niciunul. Lista de lângă **Σ** face la fel cu **Medie**, **Număr**, **Minim** sau **Maxim**. Totalul urmează rândurile: rândurile puse chiar deasupra lui, sau legate mai târziu cu **Câte un rând fiecare**, intră în el, fiindcă rândurile adăugate de o legătură se pun deasupra primului rând care are un text al lui (totalul), nu peste el. Un singur pas de anulare.
- **Unește** celulele selectate (rămâne textul primei) sau **desparte-le** din nou; adaugă rânduri deasupra sau dedesubt și coloane la stânga sau la dreapta, ori șterge-le pe cele selectate.
- **Lățimile și înălțimile** sunt în milimetri de hârtie, așa că un tabel se citește la fel la orice scară. Un rând se potrivește implicit textului, niciodată mai scund decât înălțimea lui; bifează **potrivit textului** pentru coloane cât cel mai lat text al lor (textul împărțit pe rânduri rămâne într-o coloană cu lățimea ei proprie).
- **Text rotit și spațiere.** **Text în sus (90°)** sau **text în jos (270°)** rotește textul celulelor selectate ca să se citească de-a lungul înălțimii rândului, iar un rând care se potrivește textului crește cât lungimea lui (un titlu de coloană peste coloane înguste); într-un rând cu înălțime proprie, textul se împarte pe rânduri la ea. **Spațiere** stabilește spațiul din jurul textului unei celule (gol: al tabelului).
- **Stiluri de tabel.** **Salvează ca stil…** păstrează aspectul tabelului în standardele de desen ale proiectului: liniile, culorile celulelor, alinierea și rotirile, lățimile coloanelor, înălțimile rândurilor și **rândurile de antet** (bifează **rând de antet** pentru rândurile selectate). Dacă ceri să păstreze și celulele, stilul e un șablon: un tabel nou făcut în el pornește ca o copie. Stilurile tale apar lângă aspectele Lapin când faci un tabel și în lista editorului, unde alegerea unuia restilizează un tabel (textele și lățimile rămân). **Gestionare → Standarde de desen → Tabele** le redenumește sau le șterge, iar un fișier de standarde (**Export…**, **Import…**) le duce în alte proiecte. Aspectele Lapin rămân cum erau.
- **Tabele lungi.** **Rupe după** un număr de rânduri, **sau la** o înălțime pe hârtie, iar tabelul continuă în părți lângă el (**părți la dreapta** sau **părți dedesubt**), fiecare cu rândurile de antet din nou (bifează **rând de antet** pentru rândurile pe care le repetă); o celulă unită nu se taie niciodată în două. **Pune o parte…** așază o parte pe altă planșă sau vedere: clic unde vine colțul ei din stânga sus. Citește tabelul așa cum e acum, se mută cu uneltele Modifică, iar ștearsă revine lângă tabel.
- **CSV și Excel.** **Încarcă CSV/XLSX…** aduce celulele unui fișier CSV sau ale primei foi a unui registru în tabel, de la celula selectată încolo, adăugând rânduri și coloane după nevoie, într-un pas de anulare; **CSV** și **XLSX** salvează celulele așa cum se citesc.
- **Prinderi.** Selectează un tabel în vedere: prinderile de pe marginea lui de sus stabilesc lățimile coloanelor, cele de pe marginea stângă înălțimile rândurilor (un rând cu înălțime dată nu se mai potrivește textului).
- **Aldin**, **cursiv**, alinierea, înălțimea textului, **culoarea textului**, **fundalul** și **opacitatea** lui se aplică celulelor selectate; **Borduri** desenează toate liniile, conturul, interiorul, o latură sau niciuna, cu o grosime și o culoare.

Fiecare modificare e un pas de anulare. Motorul desenează tabelul, deci vederea, planșa, PDF-ul și DXF-ul ei arată același lucru; se mută, se copiază și se rotește cu uneltele Modifică.

![Un tabel pe o planșă: un rând de antet, celule unite și un total calculat de o formulă](shots/drafting-table.webp)

![Editorul de tabel: un total calculat de o formulă, două celule unite, antetul colorat](shots/drafting-table-editor.webp)

![Un tabel care citește altul: alegătorul de sub grilă arată celulele celuilalt tabel, iar rezumatul citește totalul lui și numele unei camere](shots/drafting-table-refs.webp)

![Titluri de coloană rotite ca să se citească în sus, tabelul gata să fie salvat ca stil](shots/drafting-table-styles.webp)

![Un tabel ale cărui rânduri citesc pereții de care sunt legate: nume, lungimi și o rezistență la foc, în timp real](shots/drafting-table-model.webp)

![Scriind o formulă într-o celulă: lista propune câmpurile încăperii legate cu valorile lor; totalul de sub încăperi făcut cu suma automată](shots/drafting-table-suggest.webp)

![O etichetă de cameră desenată ca tabel într-un simbol: fiecare copie se potrivește numelui și ariei ei](shots/drafting-table-model-plan.webp)

![Un tabel lung rupt după patru camere, fiecare parte cu antetul din nou, a doua pusă pe o planșă proprie](shots/drafting-table-long.webp)

### Axe

**Adnotări → Axă** (<kbd>GR</kbd>) desenează o axă după cele două capete; ia următoarea etichetă a direcției ei. **Adnotări → Rețea de axe…** pune o rețea întreagă dintr-odată: distanțele pe **Transversal** (axele numerotate, de la stânga la dreapta) și pe **Longitudinal** (axele cu litere, de jos în sus), în unitățile proiectului, ca `6000 6000 4500` sau `3*6000`; **Originea** unde se intersectează primele axe, un **Unghi**, cât trec axele **Dincolo de rețea**, **Primele etichete**, **Ordinea** lor (axele numerotate de la stânga la dreapta sau de la dreapta la stânga, cele cu litere de jos în sus sau de sus în jos) și **Numărarea** (**crescătoare** de la prima etichetă sau **descrescătoare** de la ea: alegând descrescătoare, o primă etichetă 1 sau A devine cea care încheie familia pe ea, 4 pentru patru axe), și **Tipul de axă**. Cu **înlocuiește axele rețelei** bifat, axele desenate până atunci dispar.

![Dialogul Rețea de axe](shots/grid-dialog.webp)

Dă clic pe o axă ca s-o selectezi. Proprietățile ei arată **Începutul** și **Sfârșitul**, **Lungimea**, **Unghiul** și distanța față de axa paralelă dinaintea ei (↔). Scrie o distanță nouă și axa se mută; se mută și axele de după ea, dacă nu debifezi **se mută și axele de după ea**. **Buline** alege capetele care au bulină. **Stiluri de axe…** deschide **Standarde de desen → Axe**, unde se stabilesc bulina, mărimea ei, tipul de linie și prima etichetă. Selectează toată rețeaua ca să vezi axele și distanțele dintre ele.

**Cote temporare.** O axă selectată arată pe plan distanțele până la axele paralele de o parte și de alta, cum fac cotele temporare ale unei uși sau ferestre. Stau lângă punctul în care ai dat clic pe axă (ținut în porțiunea pe care o împarte cu vecinele), deci pe o axă lungă sunt unde lucrezi; dacă axa e aleasă din browserul de proiect, stau la mijlocul acelei porțiuni. Dă clic pe o valoare și scrie o distanță nouă (în unitățile proiectului; merg și sume ca `3000+150`), apoi **Enter**: axa selectată se mută ca cota să arate valoarea, cealaltă axă rămâne, iar cotele ținute de axe le urmează, într-un singur pas de anulare; **Esc** o lasă cum era. Cu **mută și axele de după ea** bifat, lângă cote (aceeași bifă ca în proprietățile ei), axele de dincolo de ea, de partea opusă axei până la care ai măsurat, se mută odată cu ea, deci distanțele dintre ele rămân. Lacătul face din distanță o cotă a planului, ținută de ambele axe. O axă a unei rețele radiale, ale cărei axe pornesc dintr-un centru, arată în schimb unghiurile până la vecinele ei; scrie un unghi în grade și axa se rotește în jurul centrului.

![Cotele temporare ale unei axe selectate până la vecinele ei](shots/grid-temp-dimensions.webp)

**Aliniază axele** face ca axele paralele să înceapă și să se sfârșească pe aceleași linii, luând axa selectată ca referință: cu mai multe axe selectate se aliniază doar acelea la ea; cu una singură, toate axele grilei paralele cu ea.

**Renumerotează axele…** (în **Adnotări**, lângă **Rețea de axe…**, în proprietățile unei axe sau ale rețelei, ori **RENUMEROTEAZĂ…** în bara uneltei de axă) etichetează din nou axele în ordine, oricum au fost desenate: axele care urcă pe plan de la stânga la dreapta, cele care îl traversează de jos în sus, sau doar axele selectate (Ctrl/⌘-clic pe mai multe). **Ordinea** le ia în celălalt sens când vrei: de la dreapta la stânga sau de sus în jos. Alege **numere** sau **litere** (**fără I și O** e bifat, deci după H urmează J), **Începutul** (1, 01, 101, A…) și un **Prefix** opțional (S1, S2…). **Numărarea** **descrescătoare** numără etichetele în jos de la **Început** (10, 9, 8…; E, D, C…); lăsat gol, ele se termină pe 1 sau A. Un început prea mic pentru axe (2 pentru patru axe) e refuzat. O axă a cărei etichetă nouă o are deja altă axă e refuzată, așa că începe după ea sau dă un prefix. Cotele pe axe le măsoară în continuare, cu etichetele noi, la fel și când o axă e redenumită de mână în proprietățile ei. E un singur pas de anulare.

![Proprietățile unei axe](shots/axis-properties.webp)

Toate lungimile de aici urmează unitățile proiectului. În picioare și țoli, capetele și distanțele se citesc 19'-8 1/4", iar nivelurile se scriu tot în picioare și țoli.

![Aceeași axă în picioare și țoli](shots/axis-properties-imperial.webp)

![Tabul Adnotări](shots/ribbon-annotate.webp)

## Simboluri

Un simbol este un desen mic pe care îl refolosești: un marcaj de pilot, o săgeată de nord, eticheta unui nor de revizie, legenda unei uși antifoc. E făcut din linii, polilinii, dreptunghiuri, cercuri, arce, umpleri, hașuri și texte, iar fiecare copie așezată într-o vedere e un singur element pe care îl muți, rotești, oglindești, copiezi și ștergi. Dacă schimbi simbolul, toate copiile îl urmează.

Un simbol are o **scară**. Un simbol de **adnotare** își păstrează mărimea pe hârtie (o săgeată de nord are 12 mm și la 1:50, și la 1:200); un simbol de **model** are mărimea reală, în metri (un vas de toaletă, o mașină), deci crește și scade cu scara vederii. Desenul primului e în milimetri pe hârtie, al celui de-al doilea în metri.

### Un simbol din desenele tale

Desenează simbolul într-un plan, o secțiune sau o vedere de desen, cu uneltele obișnuite (linii, cercuri, texte). Unde un text trebuie să difere de la o copie la alta, scrie în el un jeton viu, de exemplu `{attr.no}`. Selectează desenul, apoi alege **Desen → Simbol → Salvează ca simbol…** (sau scrie **SAVESYMBOL**, sau clic dreapta → **Salvează ca simbol…**). Dă clic pe **punctul de inserție** (punctul care ajunge unde dai clic când îl așezi), apoi pe un punct de-a lungul direcției simbolului (axa lui +x, după care se orientează un cap de secțiune când se rotește) sau apasă **Enter** pentru 0°. O fereastră mică cere **Numele**, **Categoria** și **Scara** (adnotare sau model). Cu **Înlocuiește desenul cu simbolul** bifat, desenul tău devine un simbol așezat unde era; un pas de anulare readuce liniile.

![Salvează ca simbol în tabul Desen](shots/symbol-save-ribbon.webp)

![Fereastra Salvează ca simbol](shots/symbol-save.webp)

### Editorul de simboluri

**Gestionare → Simboluri…** arată simbolurile (mai jos). **Nou**, **Editează…** și **Duplică** pentru un simbol încorporat deschid simbolul pe o planșetă: o vedere de desen proprie, în milimetrii simbolului, la 1:1, cu punctul de inserție ca o cruce roșie și o săgeată pe +x, direcția simbolului. Bara schiței, sus, are uneltele **Desen** (Linie, Polilinie, Dreptunghi, Cerc, Arc, Elipsă, Text, Hașură; Paralelă, Taie, Prelungește, Racordare, Teșire, Șterge), **Termină schița** și **Renunță**. Prinderile și mânerele merg ca pe orice vedere, la fel tabul **Desen** al panglicii și linia de comandă. Un panou alături ține câmpurile simbolului: **Nume**, **Scara** (mărimea pe hârtie / mărimea în model), **Categoria**, layerul pe care merg elementele noi (când simbolul are layere), o previzualizare rotită sau oglindită, făcută de motor, tabelul de atribute și câmpurile. **Termină schița** scrie simbolul într-un singur pas de anulare: toate copiile așezate îl urmează, iar ce nu ai atins rămâne exact cum era. O elipsă devine punctele ei (simbolurile nu au elipse); liniile de construcție sunt doar ajutătoare și nu se păstrează; o cotă sau altă adnotare de pe planșetă trebuie ștearsă înainte de **Termină schița**. **Renunță** abandonează editarea. Simbolurile bibliotecii acestui calculator se editează la fel.

![Simbolul pe planșetă: desenul lui la 1:1, crucea punctului de inserție și panoul cu câmpurile](shots/symbol-editor.webp)

**Tabele într-un simbol.** **Adnotare → Tabel** pe planșetă pune un tabel în simbol (iar **Salvează ca simbol** ia și tabelele dintre desene). Celulele lui citesc valorile fiecărei copii așezate, `{attr.name}`, `{= attr.area | 0.00} m²`, iar în simbolul unei etichete câmpurile elementului, `{number}`, `{= area | 0.00}`: fiecare copie își așază propriile coloane și rânduri, deci o etichetă de cameră desenată ca tabel crește cu numele ei. **Explodează** îl transformă în linii și texte.

**Atributele** sunt valorile cerute când se așază un simbol. Dă fiecăruia o cheie (`no`), o etichetă și o valoare implicită; un text care spune `{attr.no}` arată apoi valoarea fiecărei copii. Se completează în bara liniei de comandă la așezare și se schimbă după aceea în Proprietăți.

**Layere.** Panoul de lângă planșetă, la **Layere**, ține layerele simbolului: **Adaugă layer** și **Șterge layerul**; un clic pe numele unui layer îl face activ, iar ce desenezi nou intră pe el (ce era deja își păstrează layerul). Fiecare layer are o **Vizibilitate** și o **Opacitate** care primesc un număr (0–100; o vizibilitate peste 0 arată layerul), adevărat sau fals, un atribut (`{attr.show}`) sau o formulă (`{= attr.w * 10}`). Se calculează pentru fiecare copie așezată din valorile propriilor atribute, așa că un simbol își poate arăta sau ascunde părțile: o ușă care își arată deschiderea doar când atributul `swing` spune da, o notă desenată la jumătate de intensitate. Planurile, secțiunile, planșele, PDF-ul și SVG-ul desenează fiecare copie așa cum o fac valorile ei.

**Text viu.** Un text poate arăta ce știe modelul, scris între acolade. Panoul le listează (cu o casetă de căutare); un clic copiază unul, ca să-l lipești într-un text scris cu **Text**, iar textele își arată câmpurile așa cum sunt scrise: de exemplu `{view.scale}` (scara vederii), `{level.name}` și `{level.elevation}` (nivelul), `{point.elevation}` (înălțimea punctului unde stă simbolul: în plan, cea a nivelului; în secțiuni și fațade, înălțimea acelui punct), `{mark.name}` și `{mark.number}` (ale unui marcaj de vedere). O **formulă** începe cu `=` și poate avea un format după `|`: `{= attr.w * 2 | 0.00}` dublează atributul `w` și scrie două zecimale. Formulele folosesc numere, operatorii `+ - * / %`, comparații, funcții ca `min`, `max`, `round`, `if`, `clamp`, `sin` și nume; nu rulează niciodată cod.

### Așezarea unui simbol

Alege **Adnotări → Simboluri → Simbol** (sau scrie **SYMBOL**, **SYM**) într-un plan, o secțiune, o fațadă sau o vedere de desen. O fantomă a simbolului urmează cursorul; dă clic ca să-l așezi și continuă pentru alte copii. **Esc** încheie unealta. Bara liniei de comandă are simbolul de așezat, butonul de rotire, cele două butoane de oglindire și câte o casetă pentru fiecare atribut. **Spațiu** rotește simbolul un sfert; scrie **R** și **Enter** ca să-l rotești, **X** sau **Y** și **Enter** ca să-l oglindești stânga-dreapta sau sus-jos. Oricum ai roti sau oglindi, textele rămân lizibile: de la stânga la dreapta, sau de jos în sus când s-au rotit un sfert, niciodată cu susul în jos sau în oglindă.

![Așezarea simbolului: opțiunile lui în linia de comandă](shots/symbol-place.webp)

![Trei copii: așezată ca desenată, oglindită, rotită](shots/symbol-placed.webp)

Un simbol așezat se selectează ca orice element. În **Proprietăți → Simbol** îl poți schimba cu alt simbol, îi poți da **Unghiul** și **Factor de mărime**, **Oglindire X** și **Oglindire Y**, schimba valoarea fiecărui atribut, alege stratul și culoarea, **Editează simbolul** (simbolul însuși: toate copiile îl urmează) și **Explodează**, care înlocuiește copia cu linii, forme și texte simple, pe care le editezi pe rând. Mută, Rotește, Oglindește, Copiază și celelalte unelte de desen merg pe un simbol ca pe orice element desenat. Un simbol aparține vederii lui, ca o cotă, iar planșele, PDF-ul și DXF-ul îl tipăresc așa cum îl vezi (în DXF e un bloc).

![Proprietățile unui simbol așezat](shots/symbol-properties.webp)

### Simbolurile Lapin și ale tale

**Gestionare → Simboluri…** deschide [catalogul de pus](interface.md#catalogul-de-pus) pe simboluri, ca să pui unul; butonul lui **Simboluri…** deschide administratorul simbolurilor, care arată trei locuri, pe categorii. **Încorporate** sunt ale Lapin: săgețile de nord, capul de secțiune și marcajul de fațadă cu numerele lor, callout-ul, marcajul de revizie, linia de rupere, cota de nivel, marcajele de nivel (pe jumătate negru și deschis) și bulinele axelor. Nu pot fi schimbate, dar **Duplică** face o copie a unuia în proiectul tău, de schimbat. **Acest proiect** ține ce ai făcut sau importat; călătorește în fișierul proiectului. **Acest calculator** e biblioteca ta: **Salvează pe acest calculator** pune acolo o copie, iar orice proiect deschis pe acest calculator o poate folosi (**Adaugă în proiect** aduce unul). Alege un simbol ca să-l vezi mare și **Așază pe o vedere**, **Editează…**, **Redenumește**, **Duplică** sau **Șterge** (un simbol așezat întreabă ce se face cu copiile lui).

![Gestionare → Simboluri](shots/symbols-dialog.webp)

### Schimbul de simboluri

**Exportă…** scrie simbolul ca fișier **.lapinsymbols**; **Importă…** citește unul, în proiect sau în biblioteca acestui calculator (caseta de lângă buton). Unde proiectul are deja un simbol cu același nume, desenat altfel, previzualizarea importului îl arată cu o alegere: **Păstrează-le pe amândouă** (implicit: cel din fișier intră ca *Nume (2)*), **Înlocuiește** (copiile așezate îl urmează) sau **Sari peste**; vezi [Importul unui fișier de bibliotecă](interface.md#importul-unui-fisier-de-biblioteca-o-singura-previzualizare-aceleasi-alegeri).

### Marcajele vederilor

Lapin desenează singur marcajele vederilor: capătul unei secțiuni, marcajul unei fațade, marcajul unui nivel, bula unei axe, săgeata nordului dintr-un plan de situație, capătul unui detaliu, titlul unei vederi pe planșă. Poți să le desenezi cu un simbol al tău (sau cu unul al lui Lapin: o bulă pătrată de ax, un marcaj de nivel pe jumătate negru) și să le schimbi pe toate odată.

În **Gestionare → Standarde de desen → Proiect**, lista **Simboluri de marcaj** are câte un selector pentru fiecare: **Capăt de secțiune**, **Marcaj de fațadă**, **Semnul cotei de nivel**, **Bulă de ax**, **Săgeata nordului (planuri de situație)**, **Capăt de detaliu** și **Titlul vederii (pe planșe)**. Deschide un selector ca să vezi simbolurile cu imaginile lor: întâi cele de felul marcajului (bulele de ax pentru bula de ax), apoi celelalte. **Cel propriu al lui Lapin (implicit)** păstrează desenul făcut de Lapin până acum; nimic nu se schimbă până nu alegi.

![Standarde de desen, Proiect: un simbol pentru fiecare marcaj](shots/standards-marker-symbols.webp)

Un marcaj poate avea simbolul lui. Selectează o secțiune, o fațadă, un nivel, o axă, un detaliu sau o vedere și, în **Proprietăți**, **Simbol de marcaj** (o secțiune are și **Titlul vederii**) arată alegerea proiectului; alege alt simbol doar pentru acest marcaj sau **Alegerea proiectului** ca să revii. Mai multe marcaje de același fel selectate se schimbă împreună.

![Planul cu marcajele desenate de simboluri](shots/marker-symbols-plan.webp)

![Proprietățile unei secțiuni: simbolul ei de marcaj](shots/marker-symbol-properties.webp)

Un simbol făcut pentru un marcaj se face ca oricare altul (vezi mai sus). Desenează-l cu **+y** în direcția în care arată marcajul: săgeata capătului de secțiune, a marcajului de fațadă, triunghiul marcajului de nivel. Se așază pe direcția secțiunii sau a fațadei, la capătul axei, pe linia nivelului. Textele vii ale marcajului îl completează: `{mark.name}` și `{mark.number}` (numărul vederii pe planșa ei), `{mark.sheet}` (numărul planșei), `{mark.scale}`, `{level.name}` și `{level.elevation}` pentru un nivel, `{grid.label}` pentru o axă; un simbol cu atributele `number`, `sheet` sau `name` le citește din marcaj. Capătul de secțiune, marcajul de fațadă, detaliul, marcajele de nivel și bulele de ax încorporate în Lapin o fac.

### Simboluri pe planșe

Pe o planșă, **Așază simbol…** din bara de unelte listează simbolurile; alege unul și dă clic unde stă pe hârtie (Esc anulează). E desenat la mărimea lui pe hârtie, oricare ar fi scara vederilor de pe planșă, și se tipărește așa cum îl vezi.

## Conversia desenului în elemente

Liniile desenate într-un plan, un desen adus din DXF sau calchiat dintr-un PDF pot deveni elemente adevărate. Lapin recunoaște ce e fiecare formă și o face cu modulul care o stăpânește.

**Desen → Convertește → Convertește în…** lucrează pe selecția din vederea din față: selectează întâi linii, polilinii, arce sau cercuri desenate (clic dreapta → **Convertește în…** face același lucru). E singurul loc în care formele desenate devin altceva; dialogul listează în ce se pot transforma în ediția ta, grupat pe module:

- **Arhitectură**: **Pereți** (pe linii și contururi, îmbinați; o formă închisă poate avea o placă la bază și una sus), **Placă** și **Tavan** (un contur închis; un cerc face una rotundă, prin arcele lui), **Gol în placă** (un contur închis, tăiat în placa de sub el; un cerc face unul rotund), **Încăpere** (un contur închis), **Acoperiș** (un contur închis, în stilul de acoperiș ales, cu streșinile pe laturile conturului), **Balcon** (un contur închis), **Scară** (de tipul de scară ales: o linie sau o polilinie îi e traseul, cu piciorul în primul punct și urcând spre al doilea, cotind încotro cotește polilinia; prima latură a unui contur închis îi e piciorul și lățimea; rampa și podestele sunt ale tipului), **Linii de separare a încăperilor** (orice linie, polilinie, arc sau cerc: vezi [Linii de separare a încăperilor](rooms.md#linii-de-separare-a-încăperilor)), **Balustradă** (o linie deschisă) și **Plintă** (o linie sau o polilinie, cu placa pe partea dinspre afara peretelui).
- **Structură** și **Armare**: **Grinzi**, **Fundații continue**, **Fundații izolate** și **Stâlpi** (un cerc sau un contur, rotit după latura cea mai lungă) și **Bare de armare** (o formă dreaptă, în L, în U sau un etrier recunoscută, altfel o bară polilinie).
- **Oțel**: **Bare metalice** (fiecare bucată dreaptă, din profilul ales, pe partea de sus a nivelului), **Stâlpi metalici** (unde stă fiecare cerc sau contur, din profilul ales) și **Bare metalice din contururi** (un contur închis desenat ca bandă, cum îl citește [Convertește în bară](steel.md)).
- **Teren**: **Teren** (un singur teren prin toate formele alese: curbele de nivel ale unui DXF își aduc cotele, iar pentru formele desenate fără ele dai tu una), **Platformă** (un contur închis, nivelat în teren), **Parcelă** (un contur închis, limita terenului de sub el, în locul parcelei pe care o avea; cu arcele păstrate, un cerc rotund) și **Drum**, **Traseu** și **Linie de rupere** (de-a lungul liniilor care se unesc cap la cap, cum le urmează [Drum din desen CAD](site.md)).
- **O definiție, în fereastra ei**: **Simbol…**, **Piesă…** și **Profil…** dau selecția mai departe ferestrelor Salvează ca simbol, Fă piesă din schiță și Profile.

![Convertește în… pe o polilinie: o scară de tipul ales, cu piciorul în primul punct, urcând spre al doilea și cotind cum cotește polilinia](shots/convert-stair.webp)

Un acoperiș, un balcon, o plintă și o bară metalică au doar laturi drepte: un arc sau un cerc e refuzat pentru ele, cu motivul, și rămâne cum a fost desenat. Opțiunile conversiei alese (tipul și înălțimea unui perete, stilul unui acoperiș, un profil) sunt dedesubt, iar dialogul spune câte forme **pot fi convertite** și câte **nu**, cu motivul (o placă are nevoie de un contur închis), și listează ce ar deveni fiecare; o formă care face parte din elementul alteia (curbele de nivel ale unui teren, liniile unui drum) spune **Făcut împreună cu rândul** și numărul acelui rând. Planurile iau etajul planului; secțiunile și fațadele iau bare și contururi de oțel. **Păstrează desenul** lasă formele sub elementele noi, ca fundal; fără el sunt înlocuite. Un pas de anulare readuce desenul.

Uneltele care făceau elemente din desen în alte locuri deschid acum acest dialog pe ținta lor: **Oțel → Convertește în bară** pe *Bare metalice din contururi*, **Teren → Drum din desen CAD** și **Linie de rupere din desen CAD** pe *Drum* și *Linie de rupere*. **Inel**, **Cutie** și **Fâșie de perete** din Studio, pe un plan, își fac pereții prin aceeași conversie, păstrând desenul.

![Grupul Convertește din tabul Desen](shots/convert-ribbon.webp)

![Convertește în…: ce devin formele](shots/convert-dialog.webp)

![Convertește în…: un contur devenit acoperiș în stilul ales](shots/convert-roof.webp)

![Convertește în…: curbele de nivel ale unui DXF devenite un singur teren](shots/convert-terrain.webp)

Un desen pe layere se convertește layer cu layer: **Desen → Convertește → Convertește layerele…** listează layerele vederii cu numărul lor de forme și câte o alegere **Devine**: **A-WALL** în pereți, **A-COLS** în stâlpi, **A-BARS** în bare; **(lasă ca desen)** păstrează un layer cum e. Butonul de lângă un layer deschide opțiunile conversiei lui. Previzualizarea numără ce se va face. Aici desenul se păstrează implicit, ca fundal din care s-au făcut elementele. Corespondența se ține pe acest calculator după numele layerului, deci următorul desen al aceluiași birou e deja potrivit; **Exportă corespondența…** și **Importă corespondența…** o scriu într-un fișier `.lapinmapping` și îl citesc înapoi, ca s-o împarți cu biroul. După **Importă DXF…** poți bifa **convertește layerele în elemente…** ca să ajungi direct în acest dialog.

![Convertește layerele…: fiecare layer și ce devine](shots/convert-layers.webp)

### Blocurile DXF ca simboluri

Un DXF cu blocuri (scaune, uși, simbolurile unui birou) le poate păstra: în dialogul de import, **Blocuri → păstrează ca simboluri**. Fiecare bloc devine un simbol al proiectului, la scara modelului, iar fiecare inserție devine o copie așezată, rotită și întoarsă ca în fișier; fără bifă, blocurile sunt descompuse în linii. Blocurile sunt apoi în **Gestionare → Simboluri…** ca orice alt simbol, unde poți edita unul și toate copiile îl urmează, iar o copie se selectează, se mută și se numără ca un singur element. Mesajul de după import numără simbolurile și copiile.

![Trei inserții ale unui bloc, ca simboluri așezate](shots/dxf-blocks.webp)

![Blocul în Gestionare → Simboluri…](shots/dxf-blocks-symbols.webp)

## Tipuri de linie

Liniile întrerupte și punctate vin din **tipurile de linie**. **Gestionare → Standarde de desen → Tipuri de linie** le listează pe cele ale lui Lapin (continuă, întreruptă, axă, ascunsă…) și pe cele ale proiectului, fiecare desenată cum se tipărește. **Nou** face unul; alege-l ca să-i schimbi **Numele**, **Descrierea** și modelul, o **Liniuță mm** și un **Spațiu mm** repetate pe linie, în milimetri pe hârtie, cu o previzualizare deasupra. **Liniuță și spațiu** adaugă o pereche, crucea de lângă o pereche o scoate, și le poți scrie și direct: `6 2 1 2` și **Enter** (o liniuță de 6, un spațiu de 2, un punct, un spațiu de 2). **Duplică** copiază în proiect un tip de linie al lui Lapin, ca să-l schimbi. Redenumirea unuia îl redenumește peste tot unde e folosit: layere, stiluri de axe, stiluri de obiecte, liniile editate. **Elimină** îl șterge, iar ce îl folosea se desenează continuu.

Tipurile de linie ale proiectului sunt oferite oriunde se alege un tip de linie: layerele de desen, linia axelor și a stilurilor de obiecte, liniile editate.

![Standarde de desen, Tipuri de linie](shots/line-types.webp)

**Importă .lin…** citește un fișier de tipuri de linie AutoCAD. Dialogul îi listează tipurile cu modelele lor, **Liniuțele din fișier sunt în** milimetri sau țoli, și o casetă pentru fiecare, ca să-l lași deoparte. Un tip al cărui nume îl are deja proiectul, cu alte liniuțe, întreabă, ca orice import de bibliotecă: **Înlocuiește** (implicit), **Păstrează-le pe amândouă** (ca *Nume (2)*) sau **Sari peste**, ori **Aplică la toate…**. Textul și formele dintr-un tip de linie complex sunt lăsate deoparte; liniuțele lui se păstrează. **Exportă .lin…** scrie tipurile proiectului pentru AutoCAD.

![Importă .lin…: tipurile din fișier, alese înainte să intre](shots/line-types-import.webp)

### Standardele ca bibliotecă

**Exportă standardele…**, sus în dreapta în Standarde de desen, scrie stilurile, tipurile de etichete, layerele, tipurile de linie, hașurile și simbolurile proiectului într-un fișier **.lapinstandards**: standardele biroului, gata pentru alt proiect sau alt calculator. **Importă standarde…** citește unul. Ce lipsește proiectului se adaugă; ce are dar făcut altfel e listat în previzualizarea importului, intrare cu intrare, cu aceleași alegeri ca la orice import de bibliotecă (**Înlocuiește**, implicit, **Păstrează-le pe amândouă**, ca *Nume (2)*, **Sari peste** și **Aplică la toate…**). **Opțiunile** fișierului (unități, scări, mărimi ale marcajelor, stiluri de obiecte, tabelul de penițe, simboluri de marcaj) vin numai dacă bifezi **Și opțiunile proiectului…**. Un mesaj numără ce s-a adăugat, actualizat, ce era la fel și ce s-a sărit, iar un pas de anulare le ia pe toate înapoi.

![Importă standarde…: ce diferă, intrare cu intrare](shots/standards-import.webp)
