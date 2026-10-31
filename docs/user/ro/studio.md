---
title: Solide Studio și planuri de lucru
order: 85
summary: Transformi formele desenate în solide, dai unei forme o grosime cu Inel, Cutie și Fâșie de perete, și desenezi pe planuri de referință oriunde în model.
---

## Solide dintr-o schiță

Tabul **Studio** transformă în solide formele pe care le desenezi. Desenează forma cu **Desen** într-un plan, o secțiune, o fațadă sau un plan de referință, apoi alege-o cu o unealtă Studio:

- **Extrudare** (<kbd>EXT</kbd>) împinge o formă închisă pe o adâncime. Formele închise din interiorul ei devin goluri.
- **Măturare** (<kbd>SW</kbd>) mută un profil închis de-a lungul unui traseu, **Revoluție** (<kbd>REV</kbd>) îl rotește în jurul unei axe, iar **Loft** (<kbd>LO</kbd>) unește profile desenate în vederi diferite.

![Tabul Studio](shots/ribbon-studio.webp)

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

![Un plan de referință pe fațada de sud, cu un inel desenat pe el](shots/studio-plane.webp)

Selectează planul în navigator ca să-l muți. **Origine x**, **y** și **z** îl așază. **Orientat** e direcția din plan spre care privește, **Înclinare** cât se lasă pe spate (la 90° stă culcat, cu fața în sus), iar **Rotire în plan** îi rotește desenul în jurul normalei. **Mută pe normala lui…** și **Rotește în jurul verticalei…** fac același lucru dintr-un pas. Ce e desenat pe plan și fiecare solid făcut din el se mută odată cu el.

![Proprietățile unui plan de referință](shots/studio-plane-properties.webp)

Și secțiunile și fațadele sunt planuri. Un solid schițat într-o secțiune stă pe linia ei de secționare și o urmează când o muți. Într-o secțiune desfășurată, stă pe bucata pe care e desenat. O fațadă își ia un plan al ei prima dată când faci un solid pe ea: în fața modelului, cu x-ul cum e desenat acum. Schimbă **Adâncimea planului de schiță** în proprietățile fațadei ca să muți planul, și solidele lui, în lungul vederii.

![Casa cu solidele ei Studio în 3D](shots/studio-3d.webp)

În IFC, un inel, pereții unei cutii și o fâșie de perete sunt elemente IfcWall, fundul și capacul unei cutii elemente IfcSlab, iar o cutie cu ele un IfcElementAssembly cu IfcRelAggregates. Istoria fiecărui solid e păstrată în setul de proprietăți al lui Lapin, Lapin_Intent.
