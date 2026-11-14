---
title: Studio
order: 85
summary: Modelează solide libere, schițate pe orice față sau plan, apoi împinge, trage, racordează, golește, împarte și repetă-le în 3D.
---

## Fila Studio

Fila **Studio** face solide pe care nu le construiește nicio altă filă: o bancă, o copertină, o mână curentă, o fântână, un soclu pe o fațadă. Fiecare solid pornește de la o schiță sau de la o primitivă. Apoi îl schimbi în 3D prin pași, și fiecare pas rămâne editabil.

![Fila Studio](shots/ribbon-studio.webp)

Un solid Studio are o clasă IFC, ca orice element. O alegi în proprietățile lui: element de structură, placă, finisaj, balustradă, placă de planșeu sau oricare alta. Cantitățile și exportul lui urmează clasa aleasă.

## Unde schițezi

Conturul unui solid îl desenezi cu uneltele din **Desen**, ca polilinii închise, dreptunghiuri și cercuri. Liniile și arcele care se întâlnesc cap la cap fac și ele un contur închis. O formă închisă aflată în interiorul conturului e un gol.

Poți schița pe patru feluri de vederi:

- **Un plan** desenează la cota etajului lui.
- **O secțiune** desenează pe linia ei de tăiere, vertical.
- **O elevație** sau **un plan de referință**: vezi *Planuri de referință* mai jos.

## Cum faci un solid

- **Extrudare** (EXTRUDE) împinge conturul drept în afară. Scrie S pentru ambele sensuri față de schiță, T pentru o înclinare care strânge laturile spre interior, și Cut ca să taie elementele selectate înainte. Ca să meargă până la fața altui element, folosește **Până la → o față…** din proprietățile lui, sau **Extrudează până la față**. Adâncimea, înălțimile și grosimile se scriu în unitatea proiectului, cum spune mesajul (`Depth (mm)`: `2000` înseamnă 2 m).
- **Măturare** (SWEEP) duce un profil de-a lungul unui traseu. TWist rotește secțiunea pe drum, SCale o crește, iar RIse o ridică. Un cerc care urcă devine elice și face TUrns ture.
- **Loft** (LOFT) unește profile desenate pe vederi diferite. Scrie S ca să treacă neted prin ele. O linie selectată cu rolul *al doilea traseu* îl îndoaie, ca ghidaj.
- **Revoluție** (REVOLVE) rotește un profil în jurul unei linii-axă.
- **Primitivele** așază cu un clic un cub, un cilindru, o sferă, un con, un tor, o pană sau o piramidă. Dimensiunile și poziția lor le schimbi în proprietăți.

![O mână curentă măturată pe un cerc care urcă 3 m în două ture](shots/studio-helix.webp)

Fiecare solid rămâne legat de schița lui. Schimbi forma și solidul o urmează, cu același GlobalId. Selectează un solid ca să-i schimbi clasa IFC, tipul predefinit și tipul în panoul de proprietăți. **Editează schița** deschide vederea pe care stă schița.

Un solid abia făcut e selectat, deci se luminează cu mânerele schiței lui. Într-un plan, o secțiune sau o fațadă, acolo unde vederea taie un solid, tăietura lui e umplută cu alb: liniile schiței devin o formă care se vede că e plină. Un material cu umplutură sau hașură proprie o desenează cu ele, în locul albului.

**Material.** Alege materialul unui solid la **Material** în panoul de proprietăți, dintre materialele proiectului, sau fă unul nou cu **Material nou…**; cu mai multe solide selectate, se aplică tuturor. Materialul colorează solidul în 3D, îi dă tăieturii hașura și umplutura materialului și după el se fac antemăsurătoarea, costul și ordinea de tăiere pe materiale; exportul IFC îl asociază elementului. Și un tip de solid are **Material**: solidele de acel tip care nu au unul propriu îl iau și îl urmează când se schimbă. Materialul propriu al unui solid are mereu întâietate față de cel al tipului; alege **niciunul** ca să revii la cel al tipului. La o piesă plasată, **Materialul** ei propriu se folosește pentru toate corpurile ei; **Materialul** tipului de piesă se folosește pentru corpurile care nu au material în definiția piesei.

## Inel, Cutie și Fâșie de perete

Aceste trei unelte dau unei forme desenate o grosime în planul ei, apoi o înălțime față de acel plan:

- **Inel** (<kbd>RING</kbd>) ia un cerc, un arc sau un contur închis și face un inel sau un tub. Grosimea lui e la **interior**, la **exterior** sau **pe mijloc** față de formă.
- **Cutie** (<kbd>BOX</kbd>) ia un dreptunghi, o polilinie închisă sau un cerc și face pereții din jurul lui. Fundul (**Jos**) și capacul (**Sus**) sunt opționale, fiecare cu grosimea lui.
- **Fâșie de perete** (<kbd>TWALL</kbd>) ia o linie, o polilinie sau un arc și face o fâșie de perete. Grosimea ei e **la stânga**, **la dreapta** sau **pe mijloc**, privind în lungul liniei în sensul în care ai desenat-o. Colțurile ei sunt îmbinate pe bisectoare.

Pornește unealta și dă clic pe formă. Linia de comandă îți dă opțiunile ca litere: <kbd>I</kbd>, <kbd>O</kbd> și <kbd>C</kbd> (sau <kbd>L</kbd>, <kbd>R</kbd> și <kbd>C</kbd>) pentru unde e grosimea, iar la o cutie <kbd>B</kbd> și <kbd>T</kbd> ca să pornești sau să oprești fundul și capacul. Apoi scrie grosimea și înălțimea, în metri, sau apasă <kbd>Enter</kbd> ca să păstrezi valoarea arătată.

**Într-un plan**, uneltele fac implicit pereți nativi, de tipul de perete ales în **Arhitectură**. O formă închisă devine o buclă de pereți îmbinați la colțuri, o polilinie un șir de pereți, iar un arc sau un cerc pereți curbi. Unde e grosimea hotărăște unde stau pereții: toată grosimea tipului de perete merge pe partea aleasă. Fundul și capacul unei cutii devin plăci. Scrie <kbd>S</kbd> înainte să alegi forma ca să faci în schimb un solid Studio.

**Într-o secțiune, o fațadă sau un plan de referință**, uneltele fac un solid Studio de clasa IFC IfcWall; fundul și capacul unei cutii sunt plăci separate (IfcSlab). Înălțimea merge pe normala planului, spre tine; o înălțime negativă merge în partea opusă, ca la **Extrudare**. Ca să dai un tip unui astfel de perete, selectează-l și alege sau fă unul la **Tip** în panoul de proprietăți.

![Un inel de pereți curbi nativi în jurul unui cerc, o cutie cu fundul și capacul ei și o fâșie de perete, în planul parterului](shots/studio-thick-plan.webp)

Totul rămâne parametric. Selectează solidul și schimbă-i **Grosime**, **Înălțime** și **Grosimea e** în panoul de proprietăți. La o cutie, bifează **Jos** sau **Sus** și pune-le grosimile: Lapin adaugă sau scoate plăcile. O cutie cu fund sau capac e grupată cu ele într-un ansamblu (IfcElementAssembly). Trage grip-urile formei sau editeaz-o în vederea ei, și solidul se reface.

![Proprietățile unei cutii: grosimea, înălțimea, fundul și capacul](shots/studio-box-properties.webp)

## Planuri de referință

Un plan de referință e un plan oriunde în model, cu o vedere a lui. Desenezi pe el cu Desen, ca într-o secțiune, iar Studio face solide pe el. Fă unul din tabul **Studio**:

- **Plan din vedere** (<kbd>REFPLANE</kbd>) stă paralel cu planul, secțiunea, fațada sau planul de referință din față. Scrie cât de departe e spre tine (o valoare negativă merge în partea opusă).
- **Plan pe față**: în vederea 3D, dă clic pe o față a oricărui element. Planul stă pe față, orientat spre exteriorul ei.
- **Plan prin 3 puncte**: în vederea 3D, dă clic pe trei puncte ale modelului. Planul trece prin ele.

Planul nou se deschide imediat. Apare la **Planuri de referință** în navigator. Desenul lui e o secțiune pe plan, privind dinspre partea spre care e orientat.

În vederea 3D, fiecare plan de referință e desenat ca o foaie translucidă cu numele lui, cât e modelul pe el (sau cât spun **Lățime desenată (3D)** și **Înălțime desenată (3D)**, cel puțin 2 × 2 m), luminată sub cursor. Dă clic pe ea ca să selectezi planul, dublu clic ca să-i deschizi vederea. Cu planul selectat, trage săgeata din mijlocul lui ca să-l muți pe normala lui: decalajul se citește lângă săgeată, iar ce e desenat pe plan merge cu el. **Vedere → Planuri de referință** ascunde și arată foile; ele nu intră niciodată în exporturi, randări sau imagini. În planuri, secțiuni și fațade, un plan care taie vederea e desenat ca o linie întreruptă cu numele lui, pe toată întinderea vederii: clic pe ea selectează planul, dublu clic îl deschide. Un plan paralel cu vederea nu e desenat acolo (nu taie nimic din ea). Liniile acestea sunt doar pentru ecran: planșele nu le tipăresc niciodată.

![Un plan de referință pe fațada de sud, cu un inel desenat pe el](shots/studio-plane.webp)

Selectează planul în navigator ca să-l muți. **Origine x**, **y** și **z** îl așază. **Orientat** e direcția din plan spre care privește, **Înclinare** cât se lasă pe spate (la 90° stă culcat, cu fața în sus), iar **Rotire în plan** îi rotește desenul în jurul normalei. **Mută pe normala lui…** și **Rotește în jurul verticalei…** fac același lucru dintr-un pas. Ce e desenat pe plan și fiecare solid făcut din el se mută odată cu el.

![Proprietățile unui plan de referință](shots/studio-plane-properties.webp)

Și secțiunile și fațadele sunt planuri. Un solid schițat într-o secțiune stă pe linia ei de secționare și o urmează când o muți. Într-o secțiune desfășurată, stă pe bucata pe care e desenat. O fațadă își ia un plan al ei prima dată când faci un solid pe ea: în fața modelului, cu x-ul cum e desenat acum. Schimbă **Adâncimea planului de schiță** în proprietățile fațadei ca să muți planul, și solidele lui, în lungul vederii.

![Casa cu solidele ei Studio în 3D](shots/studio-3d.webp)

În IFC, un inel, pereții unei cutii și o fâșie de perete sunt elemente IfcWall, fundul și capacul unei cutii elemente IfcSlab, iar o cutie cu ele un IfcElementAssembly cu IfcRelAggregates; tipurile lor se scriu ca IfcWallType și IfcSlabType. Fiecare export 3D (IFC, Fragments, glTF, USD, COLLADA) scrie un solid Studio acolo unde stă în model, oricare ar fi planul pe care a fost schițat, cu golurile lui; un ansamblu e un nod-grup peste părțile lui. Deschis din nou, IFC-ul propriu al lui Lapin aduce solidele înapoi ca referințe, la locul lor, cu clasa, tipul și ansamblul lor, dar nu mai pot fi editate (schițele lor nu sunt în fișier). Istoria fiecărui solid e păstrată în setul de proprietăți al lui Lapin, Lapin_Intent. Solidele Studio iau doar tipuri simple, fără straturi: asta e modelarea liberă pentru care sunt făcute; pereții cu straturi sunt pereții nativi ai lui Lapin.
## Cum schimbi un solid în 3D

Uneltele acestea lucrează pe fețele și muchiile corpului așa cum e construit acum. Fața de sub mouse se luminează.

**Împinge/Trage**, **Împarte**, **Retează** și **Mută 3D** se lucrează direct pe model. Dai clic pe o față și pe ea apare o săgeată: mouse-ul mută fața de-a lungul săgeții, iar ce va face pasul se desenează pe loc. Împinge/Trage desenează cu verde ce adaugă și cu roșu ce scoate. O împărțire sau o retezare își desenează planul cu galben. Distanța se citește lângă săgeată. Scrie o valoare ca s-o dai exact, de exemplu 500 sau -250. Dai clic sau apeși <kbd>Enter</kbd> ca s-o aplici, sau <kbd>Esc</kbd> ca să renunți; unealta rămâne pornită pentru fața următoare. <kbd>Enter</kbd> fără nimic mutat sau scris repetă ultima distanță. Când tragi, pasul e de un centimetru, sau de 10 cm cu <kbd>Shift</kbd>. Butonul din dreapta și cel din mijloc rotesc și mută vederea în continuare. Celelalte unelte își cer valoarea într-o casetă mică lângă cursor.

- **Împinge/Trage** (PU) mută o față în afară, sau înăuntru când distanța e negativă. La o extrudare, capătul îi schimbă adâncimea, iar latura mută muchia schiței de sub ea. La un perete sau la orice alt element, tragerea adaugă un solid în materialul feței, iar împingerea taie în element.
- **Racordează muchia** (FE) rotunjește muchia cea mai apropiată de clic, iar **Teșește muchia** (CE) o teșește. Alege *de jur împrejurul feței* ca să faci toate muchiile feței deodată.
- **Coajă** golește solidele selectate, cu pereți groși cât scrii. Poate rămâne închisă sau poate fi lăsată deschisă la capăt, la schiță sau la ambele.
- **Împarte** taie un solid în două după un plan paralel cu fața aleasă, cât de adânc în față tragi sau scrii. Ambele părți păstrează schița. **Retează** păstrează doar o parte: alege **Păstrează în spate** sau **Păstrează în față** în bara de jos a vederii.
- **Repetare** repetă solidul pe un rând, sau în rețea când **Rânduri** e mai mult de unu (copiile sunt atunci coloanele, iar pasul între rânduri merge de la un rând la următorul), iar **Matrice polară** îl repetă în jurul unei verticale. Cu orice altceva selectat (o referință, un grup, un perete), fac în schimb copii ale lui, ca Modifică → **Repetare**: **Pe un șir**, un pas pe x, y și z, sau **În jurul unui centru**, unghiul pe care îl umplu copiile (360° le împarte pe o tură întreagă).
- **Rotește 3D** rotește solidul în jurul unei muchii alese. **Oglindește 3D** îl oglindește în planul unei fețe. **Mută 3D** pune săgeți pe x, y și z pe selecție (solide, referințe, grupuri sau orice element): tragi de una sau scrii o distanță pentru axa arătată lângă valoare (<kbd>Tab</kbd> trece la axa următoare).

![Împinge/Trage: fața de sus trasă în sus, ce adaugă desenat cu verde și distanța lângă săgeată](shots/studio-push-pull.webp)

![Muchia racordată: rotunjirea urmează blocul când i se schimbă adâncimea](shots/studio-fillet.webp)

## Desen pe o față

În vederea 3D poți desena direct pe o față și apoi împingi sau tragi ce ai desenat. Grupul **Pe față** din fila **Studio** are **Dreptunghi pe față** (FRE), **Cerc pe față** (FCI) și **Poligon pe față** (FPL).

1. Dă clic pe o față a oricărui element: un solid Studio, un perete, o placă sau un stâlp. Clicul e primul punct: colțul dreptunghiului, centrul cercului sau primul colț al poligonului. Fața rămâne conturată cât timp desenezi.
2. Mișcă mouse-ul. Forma îl urmează pe față, cu dimensiunea ei alături. Punctele se prind de colțurile feței, de mijloacele muchiilor ei și de mijlocul ei; altfel merg din centimetru în centimetru, sau din 10 în 10 cm cu <kbd>Shift</kbd>. Dă clic pe colțul opus sau pe un punct al cercului. Poți și să scrii dimensiunea: lățimea și înălțimea dreptunghiului ca `1200, 600`, sau raza cercului. Un poligon ia câte un clic la fiecare colț, sau o lungime scrisă pe direcția mouse-ului. Dă clic pe primul colț sau apasă <kbd>Enter</kbd> ca să-l închizi; <kbd>Backspace</kbd> ia înapoi ultimul colț.
3. Imediat ce forma e închisă, pe ea apare o săgeată, ca la **Împinge/Trage**. Trage-o în afară și devine un solid care stă pe față, în materialul feței. Împinge-o înăuntru și devine o tăietură în element: o nișă, un buzunar sau o gaură când trece dintr-o parte în alta. Dă clic sau apasă <kbd>Enter</kbd> ca s-o aplici. <kbd>Esc</kbd>, sau o distanță de 0, lasă forma pe față ca schiță, s-o extrudezi mai târziu.

Forma e o schiță pe un plan de referință care stă pe față. Planul poartă numele elementului și al feței, de exemplu *Extrude 1, north face*, iar formele desenate pe aceeași față îl folosesc pe același. Vederea 3D nu desenează aceste planuri, pentru că fața arată deja unde sunt. Deschide unul din navigator ca să schimbi schița în 2D; solidul sau tăietura o urmează. **Împinge/Trage** pe capătul solidului îi schimbă adâncimea. **Împinge/Trage** pe fundul unui buzunar face tăietura mai adâncă sau mai puțin adâncă, în loc să adauge la element.

![Un dreptunghi desenat pe latura unui bloc, cu dimensiunea lângă cursor](shots/studio-on-face.webp)

## Schițare în 3D

Grupul **Schiță** din fila **Studio** desenează linii și forme direct în vederea 3D, în două feluri care păstrează același lucru: o schiță pe planul de referință al feței, ca mai sus.

**Direct pe față, ca în SketchUp.** Fără nicio schiță deschisă, **Linie** (KL), **Dreptunghi** (KR) și **Cerc** (KC) desenează pe fața de sub cursor. Fața se luminează când treci peste ea; în spațiul gol desenezi la cota etajului activ. Primul clic fixează planul.

- Cursorul deduce pe ce se află și spune asta lângă el: **Capăt**, **Mijloc**, **Pe muchie**, **Intersecție**, **Pe față**. De la ultimul punct urmărește axele modelului care stau pe față, desenate în culorile lor (roșu x, verde y, albastru z), și merge **Paralel cu muchia** sau **Perpendicular pe muchie** în magenta.
- <kbd>→</kbd> blochează roșu, <kbd>←</kbd> verde, <kbd>↑</kbd> albastru, <kbd>↓</kbd> direcția muchiei dedusă ultima oară; aceeași tastă îl eliberează. Ține apăsat <kbd>Shift</kbd> ca să păstrezi direcția dedusă acum.
- Scrie o valoare oricând, fără să dai clic într-o casetă, și apasă <kbd>Enter</kbd>: `1200` e lungimea segmentului următor pe direcția în care arată mouse-ul (`1000<30` la 30°), `1200,600` lățimea și înălțimea unui dreptunghi, `300` sau `r300` raza unui cerc.
- O linie merge punct cu punct. Înapoi pe primul punct, bucla se închide și săgeata de împinge/trage stă imediat pe ea; <kbd>Esc</kbd> o păstrează schiță. <kbd>Enter</kbd> sau <kbd>Esc</kbd> înainte de asta lasă linia deschisă, ca schiță pe față. <kbd>Backspace</kbd> ia înapoi ultimul punct. Unealta rămâne pornită pentru forma următoare; <kbd>Esc</kbd> fără nimic desenat o oprește.

![O linie desenată pe latura unui bloc, axa roșie dedusă din ultimul ei punct](shots/studio-sketch-3d.webp)

**Pe un plan de lucru, ca în CAD.** **Schiță pe față** (KF) și un clic pe o față fac din acea față planul de lucru activ: se luminează, iar o etichetă spune *Schiță pe* fața respectivă. **Schiță pe plan** face la fel cu planul de referință selectat. Cât e activ, **Linie**, **Dreptunghi**, **Cerc** pornesc pe el LINE, RECTANGLE și CIRCLE ale liniei de comandă, iar orice comandă din **Desen** pe care o scrii merge pe el, în coordonatele proprii ale planului, cu prinderile la obiecte, ortogonalul și urmărirea dintr-un plan, între ele și muchiile feței. **Termină schița** (KE, sau <kbd>Enter</kbd> fără nicio comandă în curs) sudează liniile desenate și pune săgeata de împinge/trage pe ultima formă închisă desenată; <kbd>Esc</kbd> iese din schiță, iar ce ai desenat rămâne. **Privește planul** (KP) întoarce vederea drept spre plan. Pe o față, x-ul planului merge de la stânga la dreapta cum vezi fața din afară, orizontal, iar y-ul urcă pe față (pe o față de sus x e spre est și y spre nord), deci `@1500,0` merge spre dreapta, iar dimensiunile scrise se citesc la fel în ambele căi.

Cele două arce fac fiecare un singur lucru, cu schiță sau fără: **Arc din 2 puncte** (KA) este cel din SketchUp (mai jos), **Arc din 3 puncte** pornește ARC al liniei de comandă prin trei puncte, pe planul de lucru când e unul activ, altfel la nivelul etajului.

![Schiță pe față: fața de sus a unui bloc luminată ca plan de lucru, cu un dreptunghi desenat pe ea din linia de comandă](shots/studio-sketch-plane.webp)

**Liniile împart fețele.** O linie trasă peste o față, de la o muchie la alta (sau o buclă închisă de linii și arce în interiorul ei), o împarte în regiuni. Fața se luminează regiune cu regiune sub cursor, iar **Împinge/Trage** pe o regiune o împinge sau o trage doar pe ea: un solid separat pe față, sau o tăietură în ea. Liniile care se opresc înainte de o muchie nu împart nimic. O buclă închisă trasă în interiorul unei fețe (sau al unei regiuni) este o gaură a ei, ca în SketchUp: **Împinge/Trage** pe fața din jurul ei lasă bucla deoparte, iar bucla se împinge sau se trage separat. Conturul unei regiuni împinse sau trase rămâne schița solidului pe planul feței, unde îl editezi (din vederea acelui plan sau din browserul proiectului); vederea 3D nu îl desenează, așa că nimic nu rămâne atârnat deasupra unei părți împinse. Liniile trase de tine rămân desenate.

- **Arc din 2 puncte** (KA) desenează pe fața de sub cursor, ca în SketchUp: dai clic pe început, pe sfârșit, apoi cât se curbează (sau scrii lungimea coardei, apoi curbura).
- **Paralelă** (KO) dă clic pe o față sau pe o regiune a ei și îi desenează din nou conturul spre interior sau spre exterior prin cursor, sau cu distanța scrisă; forma închisă primește imediat săgeata de împinge/trage.
- Cu o schiță deschisă, **Paralelă**, **Taie**, **Prelungește** și **Racordare** rulează pe planul de lucru comenzile OFFSET, TRIM, EXTEND și FILLET ale liniei de comandă.
- O formă desenată pe un plan de referință și selectată în vederea 3D își arată punctele acolo. Trage unul: se mută pe plan, cu prinderile uneltelor de schiță, iar solidul făcut din schiță îl urmează.
- Ambele variante prind și de muchiile altor elemente aflate în plan, cum e latura unui bloc vecin la același nivel cu fața.

![O linie peste latura unui bloc o împarte: regiunea de sub ea e trasă singură](shots/studio-sketch-region.webp)

**Ruleta, ghidajele și Urmează calea.**

- **Ruletă** (KM) măsoară și lasă ghidaje, ca în SketchUp. Dă clic pe o muchie, depărtează-te de ea și dă clic: o linie de ghidaj trece paralel cu muchia prin acel punct, sau la distanța pe care o scrii. Dă clic pe un punct în schimb, iar al doilea clic arată distanța și lasă acolo un punct de ghidaj. Ghidajele sunt punctate, ambele feluri de a schița le prind (și unde se încrucișează cu fața), se păstrează în proiect și nu se exportă și nu se tipăresc niciodată. **Șterge ghidajele** le ia pe toate.
- **Urmează calea** (FM) trage o față de-a lungul unei căi chiar unde stă: dă clic pe față (sau pe o regiune a ei împărțită de linii), apoi pe cale, fie o linie desenată, o polilinie, un arc sau un cerc, fie o altă față ale cărei muchii le urmează de jur împrejur, cum e o față de sus pentru o cornișă. Colțurile sunt în unghi. Fața devine o schiță pe planul ei, iar rezultatul e un singur solid Studio care urmează ambele schițe.
- **Scurtături SketchUp** pornește în vederea 3D tastele de o literă ale SketchUp, pentru acest calculator: <kbd>L</kbd> linie, <kbd>R</kbd> dreptunghi, <kbd>C</kbd> cerc, <kbd>A</kbd> arc, <kbd>P</kbd> împinge/trage, <kbd>F</kbd> paralelă, <kbd>T</kbd> ruletă, <kbd>M</kbd> mută, <kbd>E</kbd> șterge, <kbd>Spațiu</kbd> selectează. Sunt oprite implicit, fiindcă ar lua locul scurtăturilor de două litere ale Lapin care încep cu acele litere în vederea 3D; scurtăturile de două litere și linia de comandă merg ca înainte oriunde în alt loc.

![Un ghidaj la o distanță scrisă de muchia blocului și o cornișă trasă pe sus cu Urmează calea](shots/studio-sketch-follow.webp)

## Pașii

Proprietățile unui solid Studio îi arată pașii în ordine. Fiecare pas poate fi oprit, mutat mai devreme sau mai târziu, schimbat sau scos. Schimbă schița, și fiecare pas se face din nou pe corpul nou. Un pas care nu-și mai găsește fața sau muchia spune asta la **Probleme**.

![Un cilindru golit într-o țeavă și repetat de trei ori: cei doi pași ai lui în proprietăți](shots/studio-steps.webp)

Proprietățile unei polilinii îi arată laturile. Scrie lungimea unei laturi, și capătul ei se mută de-a lungul ei. Laturile de după o urmează, până la următoarea latură paralelă cu ea, așa că un dreptunghi rămâne dreptunghi. Un solid desenat pe ea urmează și el.

## Rețele din alte programe

**Fișier → Importă rețea (OBJ, STL, glTF)…** aduce o rețea ca referințe pe care le vezi și la care te prinzi. Un fișier OBJ sau STL nu spune în ce unitate e și care axă e în sus, așa că Lapin întreabă. O referință se mută, se rotește, se oglindește și se copiază așa cum e, ca orice element. Ca să-i schimbi forma, selecteaz-o și apasă **Fă solid Studio**. Primește apoi împinge/trage, racordări, booleene și ceilalți pași. Aceeași unealtă merge pe pereți, plăci și elemente din fișiere IFC.

## Booleene

**Scade**, **Unește** și **Intersectează** combină orice elemente, primul selectat fiind ținta. Uneltele rămân editabile, iar rezultatul le urmează. **Arată operanzii** arată uneltele ascunse ale unei uniri, ca să le editezi. **Anulează operația** separă din nou elementele.

## Familii

O **familie** este un solid propriu cu parametri, așezat de câte ori ai nevoie. Vezi pagina Familii. Familiile sunt acum piese: selectează una și apasă Tab ca s-o editezi pe loc (pagina Piese).

## Piese

Selectează în 3D un solid sau o piesă plasată și apasă **Tab**, dă dublu clic pe ea sau folosește **Studio → Piesă → Editează piesa**: restul modelului se estompează, iar arborele piesei arată originea, schițele, pașii și parametrii. Vezi pagina Piese.
