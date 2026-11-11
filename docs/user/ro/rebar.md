---
title: Armare simplă
order: 52
summary: Armătura desenată pe secțiuni și planuri, secțiuni transversale tăiate unde apeși, și extrasul de armare.
---

Tabul **Armare simplă** desenează armătura așa cum se face un extras: bare ca forme cu dimensiunile lor, etrieri distribuiți pe o grindă, mărci în baloane și un tabel care le adună. Merge pe orice vedere: o secțiune, un plan, o fațadă.

![Tabul Armare simplă](shots/ribbon-rebar.webp)

## O secțiune acolo unde lucrezi

Armarea are nevoie de secțiunea transversală lângă cea longitudinală. **Secțiune aici** le face pe amândouă: alegi o grindă, un stâlp sau un perete în vederea în care ești, iar acolo unde apeși se taie o secțiune *transversală*, perpendiculară pe axa lui, desenată lângă punctul apăsat, în aceeași vedere, cu o jumătate de metru din ce e în jur (placa, stâlpul). Un stâlp se taie orizontal, la înălțimea apăsată. Apasă înăuntrul tăieturii elementului, nu doar pe muchia ei: într-o secțiune sau o fațadă, ca în plan, clicul ia elementul a cărui tăietură îl cuprinde.

Vederea inserată urmărește elementul: muți grinda și secțiunea se mută cu ea. Tragi inserarea de gripul ei; proprietățile îi dau un factor de scară și un chenar. **Secțiune transversală** face aceeași secțiune ca vedere de sine stătătoare, iar **Vedere inserată** pune orice vedere existentă în cea în care ești, la mărime reală și la scara gazdei, ca barele desenate peste ea să se potrivească pe beton.

![Secțiunea longitudinală a unei grinzi cu secțiunea transversală inserată și armătura ei](shots/rebar-section.webp)

## Bare

**Bară dreaptă**, **Bară în L**, **Bară în U**, **Etrier**, **Agrafă** și **Bară polilinie** se pun cu un clic (polilinia prin vârfurile ei). Fiecare e o formă cu parametri: tragi de un grip și dimensiunea lui urmează; o scrii în panoul de proprietăți, în milimetri. Barele se desenează cu grosimea lor, într-o culoare pentru fiecare diametru (Setări armare), și se îndoaie la raza cerută de SR EN 1992-1-1: dorn de 4Ø până la Ø16, 7Ø peste.

Ciocurile se dau la fiecare capăt: 90°, 135° sau 180°, cu lungimea dreaptă din practică (12Ø, 10Ø, 5Ø, niciodată sub 50 mm), pe cealaltă parte când ceri. Un etrier se naște cu două ciocuri de 135° întoarse înăuntru.

**Gripurile de ancoraj.** O bară selectată, văzută din față, arată, pe lângă gripurile ei, romburi portocalii care stau *în afara* ei la distanța **acoperirii**: unul la origine și câte unul la fiecare capăt liber (un capăt cu cioc nu are). Ele țin locul feței cofrajului: trage unul pe tăietura elementului — colțul, fața; desenul prinde acolo — și bara se oprește la acoperire înăuntru. Ancorajul capătului dă lungimea barei, cel al originii mută toată bara. Acoperirea e a barei, scrisă în câmpul **Acoperire** din proprietăți (golește-l ca să revii), altfel acoperirea etrierilor de pe tipul gazdei plus etrierul, altfel cea din Setări armare; câmpul spune de unde vine.

**Bară în secțiune** e o bară văzută din capăt: un punct de diametrul ei, cu lungimea pe care o are de-a lungul barei. **Aceeași bară în secțiune** pune bara selectată ca punct în altă parte, fără s-o numere de două ori. **Repetare**, în proprietăți, repetă o bară la câțiva milimetri, ca barele unui rând dintr-o secțiune transversală.

Balonul mărcii arată numărul mărcii și ce e bara: `4Ø12 L=2450`. Tragi balonul unde se citește mai bine.

Stilul **etichetei** spune ce scrie și unde stă: jetoanele ei în ordine (număr, diametru, pas, lungime, marcă, oțel, text liber), balonul, dacă textul merge orizontal, pe direcția barelor sau la un unghi, pe ce parte a barelor stă și la câți milimetri de ele, linia de indicație. Se dă în **Setări armare** pentru fiecare fel (bare, etrierii de pe tip, barele stâlpului, distribuții), pe un tip lângă etrierii lui și pe o bară în proprietățile ei, cel mai specific câștigând. Un balon nu se desenează niciodată fără marcă. Etichetele etrierilor de pe tip, ale barelor stâlpului și ale unei bare văzute în altă vedere au și ele gripuri: tragi una și își ține locul pe acea vedere (**Pune eticheta la loc** din proprietăți o întoarce); până atunci, etichetele care s-ar întâlni se înșiră una sub alta în ordinea mărcilor.

O **distribuție** — etrierii de pe tip, etrierii pe traseu — se marchează ca un **șir de cotă** fără valori: o linie paralelă cu barele, în afara lor, cu câte o liniuță scurtă acolo unde o taie fiecare bară și la capete, eticheta deasupra liniei pe direcția ei, balonul lipit de capătul de început. Trage balonul unde se citește mai bine; trage mijlocul șirului ca să-l depărtezi de bare. **Marcată prin** din stilul etichetei oprește șirul și lasă textul cu balonul lângă bare. **Linia de indicație** poate lipsi, poate fi una singură sau câte una la fiecare bară văzută ca punct (implicit la barele din secțiunea transversală: o bară, o linie; peste șaisprezece, doar la capete și la mijloc). Trage mijlocul unei linii de indicație ca să o îndoi acolo; cotul are apoi gripul lui, iar Ctrl/⌘-clic pe el îl scoate.

Lungimile sunt cele nominale, măsurate pe axa barei între colțuri, cu ciocurile; lungimea desfășurată, pe îndoituri, e spusă în panoul de proprietăți.

**Taie bara** taie o bară longitudinală mai lungă decât lungimea la care se livrează (12 m sau 6 m, în **Setări armare**). Unde se suprapun cele două bucăți e decizia ta, nu a lui Lapin: o bară de jos se înnădește lângă reazeme, una de sus departe de ele. Așa că zona de suprapunere o dai tu: apeși pe bară la un capăt al suprapunerii, apoi la celălalt; sau apeși unde se termină prima bară și scrii lungimea suprapunerii (800 pentru 800 mm), măsurată pe bară; Enter sau Escape după primul clic termină **lungimea de suprapunere** din setări (50Ø dacă nu e dată) la acel clic. În proprietățile barei aceeași zonă se scrie ca **Suprapunerea de la … până la …** în milimetri de la începutul barei, cu lungimea suprapunerii alături (dacă o schimbi, „până la” rămâne și se mută „de la”); prima bară se termină la *până la*, a doua începe la *de la*, iar cele două lungimi se arată în timp ce scrii, înainte de **Taie bara**. Prima păstrează marca și ciocul de la început, a doua primește marca următoare și ciocul de la capăt, iar extrasul le numără ca două mărci cu lungimile lor. A doua bucată se desenează puțin lângă prima, ca suprapunerea să se citească. O bară peste lungimea de livrare e semnalată în proprietăți, iar **Taie la lungimea de livrare** termină suprapunerea la lungimea de livrare, ca prima bucată să fie exact atât de lungă.

## Etrierii pe tipul de grindă sau de stâlp

Etrierii nu se desenează unul câte unul: țin de **tip**. În Structură → Tipuri de structură, un tip dreptunghiular de grindă sau de stâlp primește **Etrieri pe acest tip**: diametrul, acoperirea de beton și modul de așezare — un pas pe toată lungimea sau **trei zone**, fiecare cu partea ei din lungime (implicit o treime) și pasul ei, mai des la capete. Fiecare element al tipului îi arată apoi în orice secțiune după cum stă vederea față de axă: dreptunghiul din interiorul acoperirii în secțiunea transversală, câte o linie pentru fiecare etrier în cea longitudinală, linii peste grindă în planul de structură; cu eticheta `41Ø8/150` și balonul mărcii. Intră în extras sub marca tipului. Etrierii unui **stâlp** pot fi doar perimetrali, **cu un romb înscris** (colțurile la mijlocul laturilor) sau **cu doi interiori, câte unul pe direcție**, fiecare fel cu marca lui. Stâlpul se armează pe tronson de etaj, așa că **barele lui longitudinale** stau tot pe tip: câte atâtea pe fiecare față cu colțurile comune, diametrul lor și **mustățile** care ies peste nivelul de sus (implicit 1 m) pentru stâlpul de deasupra; puncte în tăietura orizontală și în planul de structură, linii în secțiunea verticală. **Sus** spune cum se termină: **După stâlpul de deasupra** (implicit) le continuă ca mustăți acolo unde pe stâlp stă alt stâlp, iar unde nu stă niciunul (ultimul etaj) le oprește sub fața de sus, în acoperire, cu un cioc de 90° (12Ø) îndoit spre interior; **Mustăți peste nivelul de sus** și **Ciocuri de 90° sub fața de sus** aleg una dintre ele orice ar fi deasupra. O bară cu cioc e o bară în L în extras, cu ciocul socotit în lungime.

**Barele longitudinale** ale grinzii nu stau pe tip (merg peste mai multe deschideri): le desenezi în secțiunea longitudinală sau în plan, cu ciocurile lor. O bară desenată în tăietura unei grinzi ține de grinda aceea și stă în spațiu: **Peste desen** o așază de atâtea ori pe direcția vederii, **după acoperire** (prima și ultima la fețe, în interiorul acoperirii și al etrierului, restul la distanțe egale) sau **la un pas**. Secțiunea transversală arată apoi barele ca puncte acolo unde sunt, planul de structură ca linii, iar extrasul le numără pe toate: `3Ø16 L=6400`. La fel pentru o bară desenată în tăietura unui stâlp, a unui perete, a unei plăci, a unei fundații sau a unei scări. La scară barele se așază între fețele de beton ale rampelor și podestelor, nu ale balustradelor sau ale finisajului.

**Bare în 3D** (Vedere → Bare în 3D, sau butonul din acest tab) arată sau ascunde categoria **Armături** a vederii 3D, la fel ca rândul ei din Vizibilitate / Grafică (<kbd>VV</kbd>): fiecare bară din spațiu ca un tub de diametrul ei măturat pe forma ei îndoită, în culoarea ei, etrierii tipului la stațiile lor, barele stâlpului, barele desenate de tine cu copiile lor. Orice vedere 3D lasă barele deoparte până le arăți, oricare i-ar fi stilul; o grindă ascunsă își ia etrierii cu ea. Barele unui IFC de structură legat sau importat (IfcReinforcingBar) sunt în aceeași categorie: se arată și se ascund cu acestea, comune unde se repetă, așa că mii de bare se rotesc fluent, iar planurile și secțiunile le desenează doar la LOD 400 (al stilului de structură). Barele acestui tab sunt un desen pentru ecran, cerut motorului cât se văd: nu se selectează, nu se exportă, iar peste două milioane de triunghiuri se desenează doar primele. Barele ca elemente proprii în model, generate din tipuri, cu optimizatoare și configuratoare, sunt ale variantei premium Rebar BIM.

## Etrieri pe o grindă

**Etrieri pe traseu** ia cele două capete ale unui traseu; etrierii se întind așa încât unul să stea la fiecare capăt, la pasul ales (150 mm implicit). **Zonele** schimbă pasul pe parcurs: **Treimi** pune la fiecare capăt un pas de două ori mai des, cum cere o grindă seismică; eticheta spune `25Ø8/100-200`. Îi arăți pe toți, doar capetele și mijlocul cu o linie întreruptă între ele, sau unul.

## Extrasul

**Extras de armare** face un tabel cu fiecare bară pe mărci: diametru, formă, număr, lungimea uneia, lungimea totală și masa, cu totalurile pe diametre. O marcă ale cărei bare diferă la diametru sau lungime e notată. E un tabel ca celelalte: pe planșe, în CSV și XLSX.

## Partiții

O **partiție** împarte barele în seturi separate, ca în Revit: etrierii grinzilor, barele longitudinale ale grinzilor, barele pereților, un etaj. E un text pe fiecare bară sau distribuție (**Partiție** în Proprietăți).

- **Desenat într-una:** bara uneltelor de desenare are o casetă **Partiție**. Barele noi intră în partiția scrisă acolo, iar caseta arată partițiile pe care le are deja modelul.
- **Mărci:** fiecare partiție își numerotează barele de la 1, deci marca 1 poate exista în mai multe.
- **Pe tipuri:** etrierii și barele stâlpilor setate pe un tip au fiecare partiția lor, în dialogul tipurilor de structură.
- **Extrasul:** cu partiții, primește o coloană **Partiție**, listează mărcile fiecăreia separat și adaugă masa ei după ele. Din bara lui de unelte arată toate partițiile sau una.
- **Vederi:** un filtru de vedere cu o regulă de parametru pe **Partition** (Manage → View filters) ascunde sau colorează barele unei partiții, ca orice alt filtru.

![Extrasul de armare cu două partiții](shots/rebar-partitions.webp)

**Setări armare** ține alegerile proiectului: o culoare pentru fiecare diametru, mărimile textului și ale balonului pe hârtie, oțelul, acoperirea, lungimile segmentelor scrise pe bare și stilul etichetelor pentru fiecare fel.
