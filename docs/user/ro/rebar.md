---
title: Armare 2D
order: 52
summary: Armătura desenată pe secțiuni și planuri, secțiuni transversale tăiate unde apeși, și extrasul de armare.
---

Tabul **Armare 2D** desenează armătura așa cum se face un extras: bare ca forme cu dimensiunile lor, etrieri distribuiți pe o grindă, mărci în baloane și un tabel care le adună. Merge pe orice vedere: o secțiune, un plan, o fațadă.

![Tabul Armare 2D](shots/ribbon-rebar.webp)

## O secțiune acolo unde lucrezi

Armarea are nevoie de secțiunea transversală lângă cea longitudinală. **Secțiune aici** le face pe amândouă: alegi o grindă, un stâlp sau un perete în vederea în care ești, iar acolo unde apeși se taie o secțiune *transversală*, perpendiculară pe axa lui, desenată lângă punctul apăsat, în aceeași vedere, cu o jumătate de metru din ce e în jur (placa, stâlpul). Un stâlp se taie orizontal, la înălțimea apăsată.

Vederea inserată urmărește elementul: muți grinda și secțiunea se mută cu ea. Tragi inserarea de gripul ei; proprietățile îi dau un factor de scară și un chenar. **Secțiune transversală** face aceeași secțiune ca vedere de sine stătătoare, iar **Vedere inserată** pune orice vedere existentă în cea în care ești, la mărime reală și la scara gazdei, ca barele desenate peste ea să se potrivească pe beton.

![Secțiunea longitudinală a unei grinzi cu secțiunea transversală inserată și armătura ei](shots/rebar-section.webp)

## Bare

**Bară dreaptă**, **Bară în L**, **Bară în U**, **Etrier**, **Agrafă** și **Bară polilinie** se pun cu un clic (polilinia prin vârfurile ei). Fiecare e o formă cu parametri: tragi de un grip și dimensiunea lui urmează; o scrii în panoul de proprietăți, în milimetri. Barele se desenează cu grosimea lor, într-o culoare pentru fiecare diametru (Setări armare), și se îndoaie la raza cerută de SR EN 1992-1-1: dorn de 4Ø până la Ø16, 7Ø peste.

Ciocurile se dau la fiecare capăt: 90°, 135° sau 180°, cu lungimea dreaptă din practică (12Ø, 10Ø, 5Ø, niciodată sub 50 mm), pe cealaltă parte când ceri. Un etrier se naște cu două ciocuri de 135° întoarse înăuntru.

**Bară în secțiune** e o bară văzută din capăt: un punct de diametrul ei, cu lungimea pe care o are de-a lungul barei. **Aceeași bară în secțiune** pune bara selectată ca punct în altă parte, fără s-o numere de două ori. **Repetare**, în proprietăți, repetă o bară la câțiva milimetri, ca barele unui rând dintr-o secțiune transversală.

Balonul mărcii arată numărul mărcii și ce e bara: `4Ø12 L=2450`. Tragi balonul unde se citește mai bine. Lungimile sunt cele nominale, măsurate pe axa barei între colțuri, cu ciocurile; lungimea desfășurată, pe îndoituri, e spusă în panoul de proprietăți.

## Etrierii pe tipul de grindă sau de stâlp

Etrierii nu se desenează unul câte unul: țin de **tip**. În Structură → Tipuri de structură, un tip dreptunghiular de grindă sau de stâlp primește **Etrieri pe acest tip**: diametrul, acoperirea de beton și modul de așezare — un pas pe toată lungimea sau **trei zone**, fiecare cu partea ei din lungime (implicit o treime) și pasul ei, mai des la capete. Fiecare element al tipului îi arată apoi în orice secțiune după cum stă vederea față de axă: dreptunghiul din interiorul acoperirii în secțiunea transversală, câte o linie pentru fiecare etrier în cea longitudinală, linii peste grindă în planul de structură; cu eticheta `41Ø8/150` și balonul mărcii. Intră în extras sub marca tipului. **Barele longitudinale** nu stau pe tip (merg peste mai multe deschideri): le desenezi în secțiunea longitudinală sau în plan, cu ciocurile lor. O bară desenată în tăietura unei grinzi ține de grinda aceea și stă în spațiu: **Peste desen** o așază de atâtea ori pe direcția vederii, **după acoperire** (prima și ultima la fețe, în interiorul acoperirii și al etrierului, restul la distanțe egale) sau **la un pas**. Secțiunea transversală arată apoi barele ca puncte acolo unde sunt, planul de structură ca linii, iar extrasul le numără pe toate: `3Ø16 L=6400`. Corpurile din vederea 3D nu sunt în această ediție.

## Etrieri pe o grindă

**Etrieri pe traseu** ia cele două capete ale unui traseu; etrierii se întind așa încât unul să stea la fiecare capăt, la pasul ales (150 mm implicit). **Zonele** schimbă pasul pe parcurs: **Treimi** pune la fiecare capăt un pas de două ori mai des, cum cere o grindă seismică; eticheta spune `25Ø8/100-200`. Îi arăți pe toți, doar capetele și mijlocul cu o linie întreruptă între ele, sau unul.

## Extrasul

**Extras de armare** face un tabel cu fiecare bară pe mărci: diametru, formă, număr, lungimea uneia, lungimea totală și masa, cu totalurile pe diametre. O marcă ale cărei bare diferă la diametru sau lungime e notată. E un tabel ca celelalte: pe planșe, în CSV și XLSX.

**Setări armare** ține alegerile proiectului: o culoare pentru fiecare diametru, mărimile textului și ale balonului pe hârtie, oțelul, acoperirea și lungimile segmentelor scrise pe bare.
