---
title: Desen și linia de comandă
order: 80
summary: Scrii comenzi ca în CAD, te prinzi de puncte, muți și copiezi, desenezi linii și adnotezi cu cote, texte și etichete.
---

## Linia de comandă

Scrie oriunde în fereastră literele unei unelte, apoi apasă <kbd>Enter</kbd>. De exemplu <kbd>WA</kbd> pentru perete, <kbd>CO</kbd> pentru copiere, <kbd>M</kbd> pentru mutare sau <kbd>L</kbd> pentru linie. Linia de comandă de jos arată ce îi trebuie uneltei mai departe. Poți da clic pe un punct sau poți scrie o lungime, un unghi sau coordonate.

Coordonatele tastate sunt în unitatea proiectului, ca lungimile de lângă cursor: într-un proiect în milimetri, `@4000,4000` după primul colț al unui dreptunghi îl face de 4 m pe 4 m, iar `4000<90` înseamnă 4 m drept în sus.

![Copierea unui perete: după punctul de bază, linia de comandă și căsuțele arată lungimea și unghiul până la mouse](shots/command-line.webp)

Fiecare unealtă își arată literele când ții mouse-ul peste ea; le poți schimba în **Vedere → Scurtături de tastatură…** (vezi [Interfața și scurtăturile](interface.md)). Acestea sunt cele mai folosite:

| Model | | Desen | |
|---|---|---|---|
| Perete | <kbd>WA</kbd> | Linie | <kbd>L</kbd> |
| Ușă, fereastră | <kbd>DR</kbd>, <kbd>WN</kbd> | Polilinie | <kbd>PL</kbd> |
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

**Grila.** Grila planului e la început **adaptivă**: pasul ei urmează zoomul. În **Preferințe → Grilă și snap** alege **Fixă** și scrie pasul (de exemplu 100 mm sau 1 m) și la câte linii una e desenată mai groasă. Grila păstrează atunci acel pas la orice zoom, iar cursorul face snap la ea unde niciun snap la obiecte nu ia punctul. Când pasul e prea fin pentru zoom, se desenează doar liniile ei mai groase. Grila de la sol a vederii 3D ia același pas.

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

### În secțiuni și fațade

**Mută** (<kbd>M</kbd>), **Copiază** (<kbd>CO</kbd>) și tragerea unui element selectat merg și într-o secțiune sau o fațadă, în planul vederii. De-a lungul vederii, elementul se mută în plan de-a lungul liniei secțiunii; în sus sau în jos, urcă sau coboară cu decalajul lui față de nivel (decalajul bazei unui perete sau stâlp, decalajul de sus al unei grinzi, decalajul unei plăci). Cât tragi, **Decalaj față de nivel** din proprietăți îl urmează. O ușă sau o fereastră alunecă în continuare de-a lungul peretelui ei, iar liniile desenate pe vedere se mută în vedere. **Oglindește** ia acolo o linie verticală și oglindește elementele față de planul care trece prin ea, perpendicular pe vedere. Elementele nu se **Rotesc** într-o secțiune sau o fațadă, pentru că s-ar răsturna: rotește-le într-un plan sau în 3D. Liniile desenate se rotesc și se oglindesc oricum. Un element fixat rămâne unde e.

![O grindă trasă în sus în fațada de sud: fantoma ei unde era, deplasarea lângă mouse și decalajul față de nivel urmând-o în proprietăți](shots/section-drag.webp)

### Fixarea

Fixează ce nu trebuie să se miște din greșeală: un model importat, caroiajul, un desen de releveu. Selectează-l și alege **Modifică → Fixează**, sau bifează **Fixat** la **Amplasare** în proprietățile lui. Un element fixat nu se mută, nu se rotește, nu se oglindește, nu se ridică, nu trece pe alt nivel și nu se șterge, și nu are grip-uri. Celelalte câmpuri ale lui se schimbă în continuare: tipul, înălțimea sau materialul. Se poate totuși copia, iar copia nu e fixată. Un perete legat de el care se mută lasă capetele peretelui fixat unde sunt. Fixarea unui grup fixează membrii lui; un grup cu un membru fixat nu se mută. **Modifică → Eliberează** (<kbd>UP</kbd>) îl lasă din nou liber. Orice element poate fi fixat: un perete, o referință dintr-un fișier importat, o linie desenată.

## Desen

Tabul **Desen** desenează linii, polilinii, dreptunghiuri, cercuri și arce. Le editează cu paralelă, taie, prelungește, racordare, teșire și îmbinare, umple suprafețe cu hașuri și le organizează pe **Layere**. **Importă DXF** aduce un desen CAD ca linii de care te poți prinde și pe care le poți edita.

![Tabul Desen](shots/ribbon-draft.webp)

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

O cotă aliniată sau liniară este implicit una singură: dai clic pe cele două puncte ale ei, apoi pe locul liniei, și comanda cere următoarea cotă. **Esc** o încheie. Bifează **Continuu** în bara de comenzi (sau scrie **C**; se ține minte) și cota continuă ca lanț: după primele două puncte și linia ei, fiecare clic adaugă punctul următor pe aceeași linie, iar valoarea fiecărui segment se vede cât desenezi. Ca să închei lanțul, apasă **Enter**, dă clic dreapta, apasă butonul **Finalizează lanțul** din bară sau dă clic din nou pe ultimul punct (merge și dublu-clic). **U** scoate ultimul punct. **Esc**, o altă unealtă sau o altă comandă păstrează lanțul desenat până atunci și o spune; anularea îl scoate. O cotă liniară păstrează direcția dată (orizontală sau verticală, după partea punctelor pe care pui linia) până ești clar de cealaltă parte; **Tab** o răstoarnă, iar bara arată care este. Un lanț se poartă ca o polilinie: un clic îl selectează tot și se mută, se copiază și se șterge ca un tot. Un lanț care măsoară pereți, muchii sau axe de rețea rămâne prins de ele: tras sau mutat singur, doar linia de cotă alunecă, perpendicular pe lanț, deci o scăpare a mouse-ului nu-l lasă să măsoare în gol; mutat împreună cu ce măsoară, merge cu ele. Grip-urile lui stau pe fiecare punct măsurat (tragi ca să-l muți, Ctrl/⌘-clic ca să-l scoți), la mijlocul fiecărui segment (tragi ca să muți linia de cotă prin punct; Ctrl/⌘-tragere adaugă un punct unde îl lași, Ctrl/⌘-clic adaugă unul la jumătate) și la capetele liniei de cotă (tragi ca să muți linia). Ca să pui puncte noi într-un lanț, ca în ArchiCAD, selectează-l și apasă **Adnotări → Adaugă puncte** (sau scrie **DIMADD**, ori **Adaugă puncte** în proprietățile lui): fiecare punct pe care dai clic intră în lanț acolo unde cade pe linia lui, între punctele vecine sau dincolo de un capăt, ca nou capăt, și se prinde de peretele, muchia sau axa pe care cade; **Enter** sau **Esc** încheie. Ca să scoți puncte, Ctrl/⌘-clic pe grip-urile lor, sau apasă **Adnotări → Scoate puncte** (**DIMDEL**, ori **Scoate puncte** în proprietăți) și dă clic lângă fiecare punct; segmentele de o parte și de alta devin unul. Și valoarea fiecărui segment are un grip: trage-l ca să muți valoarea din locul ei acolo unde segmentele sunt scurte și valorile s-ar suprapune; când iese de lângă segmentul ei, o linie de indicație subțire o leagă înapoi. Ctrl/⌘-clic pe grip, sau **Textele înapoi pe linie** în proprietăți, o pune la loc. O valoare mutată rămâne cu segmentul ei când se mută punctele și se rotește odată cu lanțul. Dublu-clic pe un segment ca să-i scrii valoarea: elementul măsurat se mută, și odată cu el tot ce urmează în lanț. Proprietățile arată valoarea și textul fiecărui segment, despart un lanț în cote simple și unesc cotele simple care se continuă pe aceeași linie.

Adnotările de pe un plan (cote, texte, linii desenate, etichete) pot trece pe alt plan, în același loc, pentru că planurile au aceleași coordonate: selectează-le și alege planul în proprietăți, la **View** ca să le muți sau la **Copy to** ca să le copiezi. Așa ajung și cotele făcute pe o planimetrie pe planul nivelului. Punctele unei cote se agață apoi de ce taie noul plan în acel loc: pereți, muchii, axe.

**Înălțimea golurilor.** Un segment al unui lanț de cote care merge de la un glaf la celălalt al unei uși, ferestre sau al unui gol scrie lățimea deasupra liniei și înălțimea golului sub ea, cum se face în desenele românești și europene. E citită din model de fiecare dată, deci urmează golul când e redimensionat, mutat sau primește alt tip; un segment peste care ai scris un text nu scrie înălțimea. **Gestionare → Standarde de desen → Dimensiuni → Înălțimea golului** o alege pentru fiecare stil de cotă: oprită, `150`, `h=150`, `h=150` cu parapetul `p=90` sub el, sau `150/90`, în unitatea stilului. Merge în planuri, secțiuni și fațade, iar planșele, PDF-ul și DXF-ul o tipăresc. Proiectele noi o au pornită (doar înălțimea); un proiect făcut înainte își păstrează cotele cum erau până o pornești acolo.

![Înălțimile ușii și ferestrei sub segmentele lor](shots/dimension-opening-height.webp)

![Înălțimea golului în stilurile de cotă](shots/dimension-opening-height-style.webp)

**Cote pe o linie.** Un punct de cotă pus pe o axă din grilă sau pe axa unui perete se ține de linie, nu de un punct al ei. Trage capătul axei de-a lungul ei ca să încapă bula în vedere, sau lungește ori scurtează peretele: cota măsoară mai departe unde ai pus-o, iar linia ei nu se rotește. Dacă axa sau peretele se mută de-a curmezișul, punctul merge cu ea, în același loc de-a lungul. La fel rămâne, când peretele e întins pe lungime, o cotă pusă pe fața lui. Cotele făcute înainte se țin de o axă din grilă la fel; una pe axa unui perete se atașează din nou dând clic pe punctul ei.

### Etichete

**Adnotări → Etichetă** (<kbd>TG</kbd>) pune pe o vedere o casetă cu ce spune un element despre el, cu o linie de indicație până la el. Cât timp unealta e pornită, bara de sub vedere alege ce arată eticheta: marca, numele, clasa sau tipul, **Rezistență la foc**, **Izolare acustică**, **Transmitanță termică** sau **Alt parametru…** după nume; ori un tip de etichetă din standardele de desen; iar **INDICAȚIE** pornește sau oprește linia de indicație. Dă clic pe element, apoi unde merge eticheta (tastează întâi **E** ca să frângi linia într-un cot). **Adnotări → Etichetează un parametru** cere numele parametrului și pornește unealta cu el.

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

### Axe

**Adnotări → Axă** (<kbd>GR</kbd>) desenează o axă după cele două capete; ia următoarea etichetă a direcției ei. **Adnotări → Rețea de axe…** pune o rețea întreagă dintr-odată: distanțele pe **Transversal** (axele numerotate, de la stânga la dreapta) și pe **Longitudinal** (axele cu litere, de jos în sus), în unitățile proiectului, ca `6000 6000 4500` sau `3*6000`; **Originea** unde se intersectează primele axe, un **Unghi**, cât trec axele **Dincolo de rețea**, **Primele etichete** și **Tipul de axă**. Cu **înlocuiește axele rețelei** bifat, axele desenate până atunci dispar.

![Dialogul Rețea de axe](shots/grid-dialog.webp)

Dă clic pe o axă ca s-o selectezi. Proprietățile ei arată **Începutul** și **Sfârșitul**, **Lungimea**, **Unghiul** și distanța față de axa paralelă dinaintea ei (↔). Scrie o distanță nouă și axa se mută; se mută și axele de după ea, dacă nu debifezi **se mută și axele de după ea**. **Buline** alege capetele care au bulină. **Stiluri de axe…** deschide **Standarde de desen → Axe**, unde se stabilesc bulina, mărimea ei, tipul de linie și prima etichetă. Selectează toată rețeaua ca să vezi axele și distanțele dintre ele.

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

**Gestionare → Simboluri…** arată simbolurile (mai jos). **Nou** deschide unul gol în editor, iar **Editează…** deschide unul făcut de tine. Editorul e cel al designerului de cartușe: unelte pentru linii, dreptunghiuri, polilinii, cercuri, arce și texte pe o grilă, cu o cruce roșie la punctul de inserție și o săgeată pentru direcție, scara, categoria și descrierea simbolului în dreapta și o previzualizare desenată de motor așa cum se va tipări. **Oglindire X**, **Oglindire Y** și butonul de rotire întorc previzualizarea, ca să vezi cum se citește în orice poziție. **Salvează** păstrează schimbările: toate copiile așezate se actualizează într-un singur pas de anulare.

![Editorul de simboluri](shots/symbol-editor.webp)

**Atributele** sunt valorile cerute când se așază un simbol. Dă fiecăruia o cheie (`no`), o etichetă și o valoare implicită; un text care spune `{attr.no}` arată apoi valoarea fiecărei copii. Se completează în bara liniei de comandă la așezare și se schimbă după aceea în Proprietăți.

**Text viu.** Un text poate arăta ce știe modelul, scris între acolade. Lista de sub previzualizare le oferă (cu o casetă de căutare): de exemplu `{view.scale}` (scara vederii), `{level.name}` și `{level.elevation}` (nivelul), `{point.elevation}` (înălțimea punctului unde stă simbolul: în plan, cea a nivelului; în secțiuni și fațade, înălțimea acelui punct), `{mark.name}` și `{mark.number}` (ale unui marcaj de vedere). O **formulă** începe cu `=` și poate avea un format după `|`: `{= attr.w * 2 | 0.00}` dublează atributul `w` și scrie două zecimale. Formulele folosesc numere, operatorii `+ - * / %`, comparații, funcții ca `min`, `max`, `round`, `if`, `clamp`, `sin` și nume; nu rulează niciodată cod. Scrie un text în caseta de încercare de sub listă ca să vezi ce dă.

### Așezarea unui simbol

Alege **Adnotări → Simboluri → Simbol** (sau scrie **SYMBOL**, **SYM**) într-un plan, o secțiune, o fațadă sau o vedere de desen. O fantomă a simbolului urmează cursorul; dă clic ca să-l așezi și continuă pentru alte copii. **Esc** încheie unealta. Bara liniei de comandă are simbolul de așezat, butonul de rotire, cele două butoane de oglindire și câte o casetă pentru fiecare atribut. **Spațiu** rotește simbolul un sfert; scrie **R** și **Enter** ca să-l rotești, **X** sau **Y** și **Enter** ca să-l oglindești stânga-dreapta sau sus-jos. Oricum ai roti sau oglindi, textele rămân lizibile: de la stânga la dreapta, sau de jos în sus când s-au rotit un sfert, niciodată cu susul în jos sau în oglindă.

![Așezarea simbolului: opțiunile lui în linia de comandă](shots/symbol-place.webp)

![Trei copii: așezată ca desenată, oglindită, rotită](shots/symbol-placed.webp)

Un simbol așezat se selectează ca orice element. În **Proprietăți → Simbol** îl poți schimba cu alt simbol, îi poți da **Unghiul** și **Factor de mărime**, **Oglindire X** și **Oglindire Y**, schimba valoarea fiecărui atribut, alege stratul și culoarea, **Editează simbolul** (simbolul însuși: toate copiile îl urmează) și **Explodează**, care înlocuiește copia cu linii, forme și texte simple, pe care le editezi pe rând. Mută, Rotește, Oglindește, Copiază și celelalte unelte de desen merg pe un simbol ca pe orice element desenat. Un simbol aparține vederii lui, ca o cotă, iar planșele, PDF-ul și DXF-ul îl tipăresc așa cum îl vezi (în DXF e un bloc).

![Proprietățile unui simbol așezat](shots/symbol-properties.webp)

### Simbolurile Lapin și ale tale

**Gestionare → Simboluri…** arată trei locuri, pe categorii. **Încorporate** sunt ale Lapin: săgețile de nord, capul de secțiune și marcajul de fațadă cu numerele lor, callout-ul, marcajul de revizie, linia de rupere, cota de nivel, marcajele de nivel (pe jumătate negru și deschis) și bulinele axelor. Nu pot fi schimbate, dar **Duplică** face o copie a unuia în proiectul tău, de schimbat. **Acest proiect** ține ce ai făcut sau importat; călătorește în fișierul proiectului. **Acest calculator** e biblioteca ta: **Salvează pe acest calculator** pune acolo o copie, iar orice proiect deschis pe acest calculator o poate folosi (**Adaugă în proiect** aduce unul). Alege un simbol ca să-l vezi mare și **Așază pe o vedere**, **Editează…**, **Redenumește**, **Duplică** sau **Șterge** (un simbol așezat întreabă ce se face cu copiile lui).

![Gestionare → Simboluri](shots/symbols-dialog.webp)

### Schimbul de simboluri

**Exportă…** scrie simbolul ca fișier **.lapinsymbols**; **Importă…** citește unul, în proiect sau în biblioteca acestui calculator (caseta de lângă buton). Unde proiectul are deja un simbol cu același nume, o listă arată care sunt la fel și care diferă, cu o alegere pentru fiecare: **păstrează**, **actualizează** (copiile așezate îl urmează), **păstrează-le pe amândouă** sau **sari**.

### Simboluri pe planșe

Pe o planșă, **Așază simbol…** din bara de unelte listează simbolurile; alege unul și dă clic unde stă pe hârtie (Esc anulează). E desenat la mărimea lui pe hârtie, oricare ar fi scara vederilor de pe planșă, și se tipărește așa cum îl vezi.
