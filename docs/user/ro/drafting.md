---
title: Desen și linia de comandă
order: 80
summary: Scrii comenzi ca în CAD, te prinzi de puncte, muți și copiezi, desenezi linii și adnotezi cu cote, texte și etichete.
---

## Linia de comandă

Scrie oriunde în fereastră literele unei unelte, apoi apasă <kbd>Enter</kbd>. De exemplu <kbd>WA</kbd> pentru perete, <kbd>CO</kbd> pentru copiere, <kbd>M</kbd> pentru mutare sau <kbd>L</kbd> pentru linie. Linia de comandă de jos arată ce îi trebuie uneltei mai departe. Poți da clic pe un punct sau poți scrie o lungime, un unghi sau coordonate.

![Copierea unui perete: după punctul de bază, linia de comandă și căsuțele arată lungimea și unghiul până la mouse](shots/command-line.webp)

Fiecare unealtă își arată literele când ții mouse-ul peste ea. Acestea sunt cele mai folosite:

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

## Snap și ajutoare

Lapin se prinde de capetele, mijloacele și intersecțiile a ce e desenat, și de grilă. <kbd>F3</kbd> pornește sau oprește snap-ul, <kbd>F8</kbd> ortho și <kbd>F10</kbd> urmărirea polară. Starea lor se vede în dreapta jos a vederii.

## Copiere și mutare

Ca să copiezi, selectează elementul și scrie <kbd>CO</kbd>. Dă clic pe un punct de bază, apoi pe un al doilea punct. Lapin copiază mai departe până apeși <kbd>Enter</kbd> sau <kbd>Esc</kbd>. **Mută** (<kbd>M</kbd>) merge la fel.

Poți și să tragi. Trage de crucea din mijlocul unui element selectat ca să-l muți. Ține <kbd>Ctrl</kbd> (<kbd>⌘</kbd> pe Mac) cât tragi ca să lași o copie.

O axă se mută, se copiază, se rotește și se oglindește singură sau împreună cu alte elemente. O copie e o axă nouă a aceluiași caroiaj, numită după ultima axă care merge în aceeași direcție: cu axele 1, 2 și 3, copia oricăreia e 4.

## Desen

Tabul **Desen** desenează linii, polilinii, dreptunghiuri, cercuri și arce. Le editează cu paralelă, taie, prelungește, racordare, teșire și îmbinare, umple suprafețe cu hașuri și le organizează pe **Layere**. **Import DXF** aduce un desen CAD ca linii de care te poți prinde și pe care le poți edita.

![Tabul Desen](shots/ribbon-draft.webp)

## Adnotări

Tabul **Adnotări** are cotele, textele, axele, cotele de nivel și etichetele. O cotă citește modelul, așa că se actualizează când se mută un perete. Cum arată textele, cotele, axele și etichetele se setează în **Gestionare → Standarde de desen**.

O cotă aliniată sau liniară continuă ca lanț: după primele două puncte și linia ei, fiecare clic adaugă punctul următor pe aceeași linie, iar **Enter** (sau **Esc**) face tot lanțul o singură cotă (**U** scoate ultimul punct; scrie întâi **S** pentru o cotă simplă). Un lanț se poartă ca o polilinie: un clic îl selectează tot și se mută, se copiază și se șterge ca un tot. Grip-urile lui stau pe fiecare punct măsurat (tragi ca să-l muți, Ctrl/⌘-clic ca să-l scoți), la mijlocul fiecărui segment (tragi ca să adaugi un punct, Ctrl/⌘-clic adaugă unul la jumătate) și la capetele liniei de cotă (tragi ca să muți linia). Ca să pui puncte noi într-un lanț, ca în ArchiCAD, selectează-l și apasă **Adnotări → Adaugă puncte** (sau scrie **DIMADD**, ori **Adaugă puncte** în proprietățile lui): fiecare punct pe care dai clic intră în lanț acolo unde cade pe linia lui, între punctele vecine sau dincolo de un capăt, ca nou capăt, și se prinde de peretele, muchia sau axa pe care cade; **Enter** sau **Esc** încheie. Dublu-clic pe un segment ca să-i scrii valoarea: elementul măsurat se mută, și odată cu el tot ce urmează în lanț. Proprietățile arată valoarea și textul fiecărui segment, despart un lanț în cote simple și unesc cotele simple care se continuă pe aceeași linie.

![Tabul Adnotări](shots/ribbon-annotate.webp)
