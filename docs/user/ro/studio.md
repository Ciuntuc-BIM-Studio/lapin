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

- **Extrudare** (EXTRUDE) împinge conturul drept în afară. Scrie S pentru ambele sensuri față de schiță, T pentru o înclinare care strânge laturile spre interior, și Cut ca să taie elementele selectate înainte. Ca să meargă până la fața altui element, folosește **Până la → o față…** din proprietățile lui, sau **Extrudează până la față**.
- **Măturare** (SWEEP) duce un profil de-a lungul unui traseu. TWist rotește secțiunea pe drum, SCale o crește, iar RIse o ridică. Un cerc care urcă devine elice și face TUrns ture.
- **Loft** (LOFT) unește profile desenate pe vederi diferite. Scrie S ca să treacă neted prin ele. O linie selectată cu rolul *al doilea traseu* îl îndoaie, ca ghidaj.
- **Revoluție** (REVOLVE) rotește un profil în jurul unei linii-axă.
- **Primitivele** așază cu un clic un cub, un cilindru, o sferă, un con, un tor, o pană sau o piramidă. Dimensiunile și poziția lor le schimbi în proprietăți.

![O mână curentă măturată pe un cerc care urcă 3 m în două ture](shots/studio-helix.webp)

Fiecare solid rămâne legat de schița lui. Schimbi forma și solidul o urmează, cu același GlobalId. Selectează un solid ca să-i schimbi clasa IFC, tipul predefinit și tipul în panoul de proprietăți. **Editează schița** deschide vederea pe care stă schița.

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

Uneltele acestea lucrează pe fețele și muchiile corpului așa cum e construit acum. Fața de sub mouse se luminează. Când o unealtă are nevoie de o valoare, lângă cursor se deschide o casetă mică. Scrie valoarea și apasă <kbd>Enter</kbd>, sau <kbd>Esc</kbd> ca să renunți.

- **Împinge/Trage** (PU) mută o față în afară cu o distanță, sau înăuntru când distanța e negativă. La o extrudare, capătul îi schimbă adâncimea, iar latura mută muchia schiței de sub ea. La un perete sau la orice alt element, tragerea adaugă un solid în materialul feței, iar împingerea taie în element.
- **Racordează muchia** (FE) rotunjește muchia cea mai apropiată de clic, iar **Teșește muchia** (CE) o teșește. Alege *de jur împrejurul feței* ca să faci toate muchiile feței deodată.
- **Coajă** golește solidele selectate, cu pereți groși cât scrii. Poate rămâne închisă sau poate fi lăsată deschisă la capăt, la schiță sau la ambele.
- **Împarte** taie un solid în două după planul feței alese, cât de adânc în față scrii. Ambele părți păstrează schița. **Retează** păstrează doar o parte.
- **Matrice** repetă solidul pe un rând, iar **Matrice polară** îl repetă în jurul unei verticale.
- **Rotește 3D** rotește solidul în jurul unei muchii alese. **Oglindește 3D** îl oglindește în planul unei fețe. **Mută 3D** îl mută cu x, y și z.

![Împinge/Trage: fața de sus luminată și distanța scrisă lângă cursor](shots/studio-push-pull.webp)

![Muchia racordată: rotunjirea urmează blocul când i se schimbă adâncimea](shots/studio-fillet.webp)

## Pașii

Proprietățile unui solid Studio îi arată pașii în ordine. Fiecare pas poate fi oprit, mutat mai devreme sau mai târziu, schimbat sau scos. Schimbă schița, și fiecare pas se face din nou pe corpul nou. Un pas care nu-și mai găsește fața sau muchia spune asta la **Probleme**.

![Un cilindru golit într-o țeavă și repetat de trei ori: cei doi pași ai lui în proprietăți](shots/studio-steps.webp)

Proprietățile unei polilinii îi arată laturile. Scrie lungimea unei laturi, și capătul ei se mută de-a lungul ei. Laturile de după o urmează, până la următoarea latură paralelă cu ea, așa că un dreptunghi rămâne dreptunghi. Un solid desenat pe ea urmează și el.

## Rețele din alte programe

**Fișier → Importă rețea (OBJ, STL, glTF)…** aduce o rețea ca referințe pe care le vezi și la care te prinzi. Un fișier OBJ sau STL nu spune în ce unitate e și care axă e în sus, așa că Lapin întreabă. Selectează o referință și apasă **Fă solid Studio** ca s-o poți edita. Primește apoi împinge/trage, racordări, booleene și ceilalți pași. Aceeași unealtă merge pe pereți, plăci și elemente din fișiere IFC.

## Booleene

**Scade**, **Unește** și **Intersectează** combină orice elemente, primul selectat fiind ținta. Uneltele rămân editabile, iar rezultatul le urmează. **Arată operanzii** arată uneltele ascunse ale unei uniri, ca să le editezi. **Anulează operația** separă din nou elementele.
