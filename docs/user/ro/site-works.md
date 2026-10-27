---
title: Organizare de șantier
order: 55
summary: Schele de fațadă și cofraje pentru plăci și grinzi, desfășurate din model de configuratoarele lor.
---

Tabul **Organizare de șantier** modelează lucrările provizorii de pe șantier: schelele de fațadă și cofrajele plăcilor și grinzilor. Lapin le desfășoară din model și numără ce le trebuie. Când modelul se schimbă, ele îl urmează.

![Tabul Organizare de șantier](shots/ribbon-site-works.webp)

Schelele și cofrajele sunt **temporare**: se montează și se demontează în faza **Execuție**. Prima pe care o așezi adaugă faza, dacă proiectul nu o are, și o face faza activă. Vederile fazelor de dinainte nu le arată. Vezi **Gestionează → Faze**.

![O schelă în jurul casei și cofrajul planșeului de peste parter, pe popi](shots/site-works-3d.webp)

## Schele

- **Schelă pe pereți** (<kbd>SW</kbd>): selectezi pereții (Shift-clic pentru mai mulți), apoi apeși butonul. Schela merge pe fețele lor exterioare, la distanța tipului de perete, până la partea de sus a pereților. Își închide singură colțurile. Muți un perete sau îl faci mai înalt, și schela îl urmează.
- **Schelă** (<kbd>SC</kbd>): îi desenezi linia în plan, de-a lungul fațadei, și apeși Enter ca să termini. Schela stă la stânga liniei. Partea cealaltă o alegi în proprietăți.

Fiecare tronson drept e împărțit în traveile tipului, cât mai puține câte ajung până la capăt. Nivelurile urcă până când podina de sus ajunge aproape de cota de lucru, iar parapetul stă deasupra ei. Tălpile stau pe teren, pe tălpi reglabile. Dacă terenul coboară mai mult decât preiau tălpile, Lapin îți spune.

![Proprietățile unei schele](shots/scaffold-properties.webp)

În proprietăți poți da **cota de lucru**: o lași goală pentru partea de sus a pereților. Mai poți alege dacă tălpile stau **pe teren** sau **pe etaj**, sau poți **desprinde** schela de pereți.

**Tipuri de schelă…** deschide configuratorul. În el alegi sistemul (modular sau cu cadre), lungimile de travee, lățimea și înălțimea nivelului, distanța de perete, podinele, parapetele și bordurile, ancorele, tălpile reglabile, traveile de acces și plasa de protecție. Vederea arată un tronson de lungimea și înălțimea alese, cu ce numără.

![Configuratorul de schele](shots/scaffold-configurator.webp)

## Cofraje

- **Cofraje** (<kbd>FW</kbd>): selectezi plăci și grinzi, apoi apeși butonul. Fiecare primește cofrajul tuturor fețelor care se cofrează.
- **Cofraj pe față** (<kbd>FF</kbd>): alegi o singură față. În 3D, fața de sub cursor se colorează și îi apare numele: intradosul plăcii, fața de jos a grinzii sau una dintre fețele ei laterale. În plan, dai clic pe marginea unei plăci sau în interiorul conturului ei. O față aleasă mai târziu intră în același cofraj.

**Cofrajul plăcii** acoperă intradosul cu placaj pe grinzi secundare, pe grinzi principale, pe popi. Grinzile de sub placă nu se cofrează a doua oară, și nici pereții și stâlpii pe care stă sau golurile ei. Distanțele vin din tabelul tipului, după grosimea plăcii. Popii ajung până la placa de dedesubt, la teren sau la etajul de sub ea. Se cofrează și marginile libere, cât grosimea plăcii plus o supraînălțare. În proprietăți alegi ce fețe se cofrează și direcția grinzilor principale.

**Cofrajul grinzii** e fața de jos, pe grinzi de rezemare și popi, plus fețele laterale. Fiecare față laterală e cât grinda sub placa turnată odată cu ea. Stâlpii și grinzile mai înalte o întrerup. Poți pune baghete de șanfren pe muchiile de jos.

**Tipuri de cofraj…** deschide configuratorul, pentru plăci și pentru grinzi. Pentru plăci alegi sistemul (grinzi de lemn și popi, sau panouri), placajul, grinzile și lungimile lor din catalog, tabelul de distanțe, marginile, popii și refolosirile. Pentru grinzi alegi grinzile de rezemare, popii, jugurile după înălțimea grinzii, șanfrenul și refolosirile. Secțiunea arată straturile și popul ales de motor pentru înălțimea dată.

![Configuratorul de cofraje](shots/formwork-configurator.webp)

## Planul de cofraj

**Plan de cofraj** face un plan al etajului activ în faza Execuție, secționat la un metru deasupra plăcii. Popii apar secționați, iar cofrajul de deasupra lor e desenat cu linie întreruptă, până la nivelul de sus: grinzile principale și secundare, placajul și marginile.

![Planul de cofraj al parterului](shots/formwork-plan.webp)

## Ce numără

Fiecare schelă și fiecare cofraj își arată cantitățile în proprietăți:

- la schelă: aria, traveile, nivelurile, montanții, podinele și ancorele;
- la cofraj: aria de contact pe fiecare față, popii pe mărimi și grinzile pe lungimile din catalog.

**Extras de cofraje** și **Extras de schele** își pun tabelele în browserul proiectului, la Extrase, ca să le pui pe planșă sau să le exporți.

În IFC se scriu ca ansambluri (`IfcElementAssembly` FORMWORK și SCAFFOLD) cu statusul TEMPORARY. Piesele sunt bare, plăci și accesorii. Fiecare are resursa ei de construcție, iar cofrajul e legat de elementul pe care îl cofrează.
