---
title: Amplasamentul și datele lui
order: 52
summary: Adu ridicarea topo, parcela, vecinii și un ortofotoplan în coordonate reale, din DXF, CSV, GeoJSON, CityJSON și o imagine georeferențiată.
---

Fila **Teren** ține terenul pe care stă clădirea: suprafața terenului, parcela, clădirile vecine și o fotografie aeriană, fiecare acolo unde este în realitate. Grupul **Import** le aduce din fișierele pe care le predau topograful și cadastrul.

![Fila Teren](shots/ribbon-site.webp)

## Coordonate reale

O ridicare topo vine în coordonate de hartă: estică și nordică, la sute de kilometri de zero. Lapin ține modelul lângă origine și îl leagă de hartă prin **punctul de referință topo**. Primul import într-un proiect face asta singur: punctul de referință ajunge în originea planului, pe primele coordonate din fișier rotunjite la metru, iar fiecare import următor cade în același loc.

**Gestionare → Amplasament** arată **sistemul de coordonate** în care sunt estica și nordica, și punctele de bază și de referință. Lapin cunoaște Stereo 70 (EPSG:3844, sistemul național al României), zonele UTM 34 și 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) și longitudinea și latitudinea (WGS 84, EPSG:4326). Un fișier în alt sistem dintre acestea se convertește la import: un GeoJSON în longitudine și latitudine ajunge pe planul în Stereo 70 acolo unde îi e locul. Lambert-93 (EPSG:2154, grila națională a Franței) e cunoscut și el.

**Latitudine și longitudine.** Punctul de bază poate primi și **Latitudinea** și **Longitudinea** lui (WGS 84, în grade). Cu un sistem de coordonate proiectat și fără punct topografic, ele leagă singure proiectul de hartă: Lapin calculează estul și nordul punctului de bază în acel sistem, iar planurile, cotele de coordonate, DXF-ul în coordonatele hărții și exporturile le folosesc ca pe ale unui punct topografic. Un punct topografic pus de tine are întâietate.

**Pe hartă în alte programe.** Exportul IFC scrie legătura proiectului cu harta așa cum cere ghidul buildingSMART de georeferențiere: un **IfcProjectedCRS** numit după codul EPSG (EPSG:3844…), cu datumul, proiecția, zona și metrul, și un **IfcMapConversion** cu estul, nordul și cota originii interne, rotirea spre nordul geografic și scara 1, așa că un program GIS, Bonsai sau un vizualizator așază modelul unde e. IfcSite poartă latitudinea, longitudinea și cota punctului de bază. Un proiect cu latitudine și longitudine, dar fără sistem proiectat, se scrie în zona UTM a locului lui, iar exportul spune asta; un proiect fără niciuna dintre ele nu scrie nimic despre hartă. Un IFC cu conversie de hartă (IFC4 și IFC4X3, sau ePSet_MapConversion și ePSet_ProjectedCRS din IFC2X3) leagă de hartă, la import, un proiect încă nelegat, iar latitudinea și longitudinea singure ale unui sit devin ale punctului de bază.

![Gestionare → Amplasament: sistemul de coordonate și punctul de referință](shots/site-location.webp)

**Punctele proiectului**, ca în Revit. **Vedere → Punctele de coordonate** (sau **Arată originea internă, punctul de bază și punctul topografic în planuri și în 3D** din Gestionare → Amplasament) desenează trei puncte în planuri și în vederea 3D:

- **originea internă**, 0,0 al planului, care nu se mută niciodată: axa X cu roșu și axa Y cu verde (și Z cu albastru în 3D);
- **punctul de bază al proiectului**, un cerc cu o cruce: cota lui (unde stă ±0,00), coordonatele lui pe hartă când proiectul e legat de hartă și o săgeată spre **nordul geografic**, cu unghiul ei, când proiectul e rotit față de el (**Nordul geografic** din Gestionare → Amplasament);
- **punctul topografic**, un triunghi, cu estul, nordul și cota lui.

Un plan face snap la toate trei. Vederea 3D le desenează la locul lor, la orice zoom, deasupra modelului.

![Originea internă, punctul de bază rotit cu 12,5° față de nordul geografic și punctul topografic în 3D](shots/location-3d.webp)

**Coordonatele la cursor.** Cât timp cursorul e într-un plan, bara de stare arată unde se află. Un clic pe coordonate alege față de ce se citesc: **originea internă**, **punctul de bază al proiectului**, **punctul topografic (hartă)** (est și nord, rotite spre nordul geografic, așa cum le scrie exportul IFC) sau **UCS-ul** (vezi [Snap și ajutoare](drafting.md#snap-și-ajutoare)). Alegerea se păstrează în **Fișier → Preferințe… → Coordonate**.

**Planurile unui proiect georeferențiat.** Modelul se desenează în coordonatele lui, lângă originea internă; cele ale hărții se calculează din punctul topografic, așa că un plan rămâne la fel de precis și de rapid ca oricare altul. Trei lucruri îl pun pe hartă:

- **Cote de coordonate.** **Adnotări → Cote**, pusă pe **Cotă de coordonate** în bara ei (DIMCOORD): un clic pe punct, apoi unde se termină linia de indicație. Scrie **E** și **N** ale punctului, în metri, prin punctul topografic și rotite spre nordul geografic; fără punct topografic, **X** și **Y** față de punctul de bază al proiectului. Tastează întâi **M**, **P** sau **I** ca să alegi harta, punctul de bază sau originea planului. Valorile se calculează la fiecare desenare: mută punctul topografic și toate cotele de coordonate se rescriu.
- **Un plan în coordonatele hărții.** Butonul **DXF** din bara planului îl exportă **în coordonatele planului** (milimetri față de originea internă), **față de punctul de bază al proiectului** (milimetri) sau **în coordonatele hărții** (E și N în metri, rotite spre nordul geografic), ca desenul să se așeze peste planul cadastral sau peste DXF-ul topografului.
- **Nordul geografic în sus.** Când proiectul e rotit față de nordul geografic, butonul cu busola din bara planului rotește planul pe ecran ca nordul geografic să fie în sus, și înapoi. Desenul, snap-urile, grila și UCS-ul se rotesc cu el; alegerea se păstrează pentru fiecare plan, pe acest calculator. Planșele și PDF-ul rămân cu nordul proiectului în sus. Pentru un plan tipărit rotit, folosește în schimb **Rotește vederea → Nordul geografic în sus** (vezi [Rotirea unui plan](views.md#rotirea-unui-plan)): orientarea ține de vedere, se păstrează cu proiectul, iar planșele o tipăresc cum se vede.

![O cotă de coordonate pe planul unui proiect georeferențiat, cu nordul geografic în sus](shots/spot-coordinate.webp)

## Importul

- **Teren din puncte** citește un fișier CSV sau text de la topograf: x y z, sau număr nordică estică cotă, câte un punct pe linie.
- **Teren din LandXML** aduce fiecare suprafață a fișierului ca teren făcut din triunghiurile fișierului (fețele lui), așa că golurile și marginea concavă rămân cum le are fișierul; fețele marcate invizibile sunt lăsate deoparte. O suprafață fără fețe se triangulează din punctele ei.
- **Puncte din DXF** citește un plan topografic: entități POINT cu cotele lor, blocuri inserate la o cotă sau care o au ca atribut, cote scrise lângă puncte („245.37” lângă cruciuliță) și curbe de nivel la cota lor. Când planul are puncte pe mai multe straturi, Lapin le arată cu câte puncte are fiecare, iar tu alegi ce iei. Punctele fac un teren. Un DXF nu numește niciun sistem, deci Lapin ia numerele de mărimea unei hărți după intervalul lor (Stereo 70, Lambert-93, UTM). Numerele unui sistem pe care nu-l cunoaște rămân cum sunt, iar Lapin spune asta: numește sistemul în **Gestionare → Amplasament** sau convertește mai întâi desenul.
- **GeoJSON** aduce parcele, linii și puncte. Un fișier cu un singur poligon dă **parcela**; unul cu mai multe dă suprafețe. Parcela devine și conturul parcelei terenului, cu aria ei. Fișierele de la cadastru, în Stereo 70, spun asta în fișier, iar Lapin citește; un fișier care nu spune nimic e în longitudine și latitudine, cum cere standardul GeoJSON.
- **CityJSON** aduce clădirile vecine cu suprafețele lor, la locul și la înălțimea lor. Sunt referințe care nu se editează; vor arunca umbre în studiul de însorire.
- **Ortofotoplan** pune o fotografie aeriană sub amplasament. Alege imaginea (JPEG sau PNG, cel mult 8 MB) împreună cu fișierul ei de georeferențiere (`.jgw`, `.pgw` sau `.wld`): el spune unde stă imaginea. Fotografia se desenează sub plan și îmbracă terenul în 3D.

![Planul de situație: ortofotoplanul, curbele de nivel ale terenului, parcela și amprenta vecinului](shots/site-plan.webp)

![Același amplasament în 3D: fotografia pe teren, vecinul la înălțimea lui](shots/site-3d.webp)

Tot ce importi se păstrează în fișierul proiectului. Un element își ia numele din fișier (numărul cadastral al parcelei, când fișierul îl are) și păstrează celelalte proprietăți din fișier; selectează-l ca să-i vezi aria sau lungimea.

## Drumuri, curbe și liniatură

Un **drum** sau o **alee** își urmează axa. În **Coridor…** (CR), fila **Curbe** dă fiecărui vârf al axei o rază cu clotoidă la intrare și la ieșire, cum trasează un proiectant de drumuri o curbă; un vârf lăsat la 0 rămâne colț, iar curbele care nu încap între vecinele lor sunt refuzate, cu motivul. Kilometrajul, profilul longitudinal și profilurile transversale urmează axa curbă, iar IFC 4.3 o scrie ca aliniament.

Un drum sau o alee, un zid de sprijin sau un șanț se împart, se taie, se prelungesc și se copiază paralel cu uneltele pereților (**Modifică → Împarte elementul**, **Modifică → Taie/Prelungește elemente**, **Paralelă**). Pozițiile kilometrice continuă: a doua parte începe la poziția unde a fost tăietura, așa că profilul proiectat, secțiunile transversale și rosturile cerute la poziții rămân unde erau. Tăietura trebuie să cadă pe o porțiune dreaptă a axei, nu într-o curbă, iar fiecare vârf își păstrează curba. O axă importată ca segmente de aliniament IFC 4.3 nu se taie aici.

Liniatura CAD devine elemente de teren: **Drum din desen CAD** (LR) și **Linie de rupere din desen CAD** (BL) iau o linie sau o polilinie desenată sau adusă dintr-un DXF, urmează toate liniile de pe stratul ei care se leagă cap la cap și fac un drum de-a lungul ei sau o linie de rupere a terenului. Fiecare deschide **Desen → Convertește în…** pe ținta ei, cu linia aleasă ([Conversia desenului în elemente](drafting.md#conversia-desenului-în-elemente)), unde vezi lanțul înainte de conversie; acolo sunt și celelalte ținte ale terenului: un **Teren** prin curbe de nivel, o **Platformă** pe un contur închis, un **Traseu**. Când liniile au cote (liniile 3D ale unui DXF), ele dau profilul drumului sau cotele liniei de rupere. Asemenea linii se păstrează cu cotele lor și se pot arăta în 3D (categoria *liniatură*).

În 3D, **Vizibilitate / Grafică** ascunde potecile, drumurile, bordurile și pavajele unei vederi odată cu categoria **Topografie**, iar rețelele de axe se ascund și ele prin intrarea lor din Vizibilitate / Grafică; în planuri, în stilurile de obiecte și în seturile de grafică salvate, aceste elemente de amenajare urmează acum și ele categoria Topografie.

## Platforme și parcela

**Site → Platform** (<kbd>PD</kbd>) nivelează terenul în interiorul unui contur la o cotă, cu săpătură și umplutură; **Site → Parcel** (<kbd>PC</kbd>) desenează limita proprietății, linie-punct, cu aria ei. Amândouă își desenează conturul cu aceeași bară ca un planșeu: un **Polygon** ale cărui laturi pot fi arce (<kbd>Space</kbd>), un **Rectangle** sau un **Circle**. Selectate, colțurile și laturile lor au mânere (ale parcelei, pe terenul ei), iar clicul dreapta oferă **Make arc**, **Make straight**, **Fillet corner…** și **Chamfer corner…**, fiecare un pas de anulare. Platforma nivelează terenul după arcele adevărate; aria și perimetrul parcelei se măsoară pe ele, iar inventarul de coordonate îi listează colțurile.

**Modifică → Împarte elementul** taie o platformă în două după o linie de două puncte (fiecare parte nivelată la aceeași cotă, golurile pe partea în care sunt, un gol tăiat de linie refuză), iar **Modifică → Taie/Prelungește elemente** taie o platformă sau parcela după o limită dreaptă, păstrând partea pe care dai clic. Un teren are o singură parcelă: se taie, nu se împarte.

**Taluzuri în jurul platformei.** Proprietățile platformei au **Taluz la săpătură** și **Taluz la umplutură**, ca 1 : N (orizontal pe vertical; 0, implicit, păstrează laturile verticale). Cu taluz, săpătura se lărgește în sus și umplutura se întinde în jos la acea pantă până întâlnesc terenul, așa că platforma se racordează la teren în loc să stea într-o groapă sau pe un mal vertical; săpătura și umplutura ei se măsoară pe volumele cu taluz. Taluzurile urcă și coboară cât terenul din jurul platformei, nu cât întregul teren. Un taluz care ar ieși atât de departe încât s-ar intersecta cu el însuși (un taluz lung în jurul unei crestături înguste a conturului) e refuzat: platforma nu se nivelează, nu i se măsoară nimic, iar terenul spune de ce; dă-i un taluz mai abrupt (un N mai mic) sau un contur mai simplu. O platformă cu goluri își păstrează laturile verticale. Un contur care se intersectează cu el însuși, al unei platforme, al unei parcele, al limitei terenului, al unei subregiuni sau al unei zone plantate, e refuzat.

## Terenul: limită, pante și sistematizare

![Un teren ridicat topografic, decupat după o limită în L, o etichetă de pantă, o subregiune de pietriș și grila săpătură/umplutură peste o tușă ridicată](shots/terrain-tools-plan.webp)

**Limita.** Un teren dintr-o ridicare topografică acoperă conturul exterior al punctelor (înfășurătoarea lor convexă). **Limita terenului** (în grupul **Teren**) desenează conturul pe care îl are terenul cu adevărat, un poligon ale cărui laturi pot fi arce (<kbd>Space</kbd>), un dreptunghi sau un cerc, ca o platformă: terenul din afara lui dispare, cu colțuri concave cu tot, iar curbele de nivel, solidul, secțiunile și cantitățile îl urmează. Colțurile lui au mânere. În proprietățile terenului, **Limită** spune **decupat** sau **înfășurătoare convexă**; **din parcelă** ia conturul parcelei drept limită, iar **scoate** revine la înfășurătoare.

**Un teren pe contur.** **Teren pe contur** (<kbd>TO</kbd>, în grupul **Teren**) face terenul direct după conturul pe care îl are, fără un dreptunghi de decupat după aceea: un poligon ale cărui laturi pot fi arce (<kbd>Space</kbd>), un dreptunghi, un cerc sau, din aceeași bară, interiorul pereților, o formă desenată închisă ori o cameră. Ultima alegere din bară spune la ce cotă: **La o cotă** întreabă cota terenului (±0.00 = 0), **Pe teren** așază fiecare vârf pe terenul de dedesubt. **Teren pe conturul ales** ia conturul a ce alegi cu un clic: o cameră, o parcelă sau o zonă dintr-un fișier GIS (cu cotele aduse de el), parcela unui teren, o platformă (la cota ei) sau o formă desenată închisă (prin **Convertește în… → Teren în interior**, care le convertește pe mai multe deodată). Fiecare vârf al conturului, și punctele de-a lungul unui arc, e un punct al terenului cu mâner, iar terenul se termină exact pe contur; interiorul primește puncte pe o grilă, ca pensula și mânerele să aibă pe ce lucra. Mânerele colțurilor în plan mută conturul, iar punctele lui îl urmează: un colț tras își păstrează cota, un colț adăugat ia cota terenului de acolo.

![Un teren desenat pe un contur cu un arc, cu un mâner pe fiecare vârf al conturului, în 3D](shots/terrain-outline-3d.webp)

Un teren decupat cu **Limita terenului** își păstrează cotele conturului așa cum le dă terenul; **conturul ca puncte**, lângă **Limită**, face din fiecare vârf al conturului un punct al lui, la cota terenului de acolo, ca să aibă mâner și o cotă de scris. Terenul nu se mișcă (doar pe un teren lucrat cu pensula, ale cărui tușe lucrează acum și pe aceste puncte, se poate mișca puțin de-a lungul conturului, și un mesaj o spune). Proiectele vechi rămân cum sunt până ceri.

**Împărțire și unire.** **Împarte terenul** taie un teren în două după o linie de două puncte trasă peste el. Jumătatea mai mare rămâne terenul (își păstrează identitatea, GlobalId-ul din IFC); cealaltă devine un teren nou alături. Platformele, rigolele și aleile merg cu jumătatea în care se află (după mijlocul conturului, o alee după mijlocul ei), la fel cotele punctuale, etichetele de pantă și subregiunile, așa că nimic de pe teren nu se pierde. **Unește terenurile** adună terenurile selectate în primul selectat: punctele, liniile de frângere, tușele de pensulă, cotele punctuale, etichetele, subregiunile, platformele, rigolele și aleile lor trec toate, iar cotele rămân unde sunt chiar dacă terenurile erau citite față de repere diferite. Fiecare jumătate a unei împărțiri păstrează limita de partea ei, iar unirea îmbină limitele terenurilor într-una singură (un teren fără limită contează cu conturul punctelor lui): două jumătăți unite din nou au conturul din care au fost tăiate, terenuri care se suprapun au conturul din jurul amândurora. Când limitele lor nu formează un singur contur (sunt depărtate sau lasă un gol între ele), terenul unit acoperă toate punctele lui și un mesaj o spune; desenează-i din nou limita ca să-l decupezi. Un teren are o singură parcelă: terenul unit o ia pe cea care există, iar când mai multe au câte una o păstrează pe a primului și un mesaj o spune. Fiecare este un pas de anulare.

**Etichete de pantă și cotele curbelor de nivel.** **Etichetă de pantă** scrie panta dintre două puncte pe care dai clic pe teren, în procente și ca 1 : N (5.0 %, 1:20.0), cu o săgeată spre vale. Fiecare curbă de nivel principală își poartă cota la mijloc, de-a lungul liniei, pe un fond curat, ca linia să se oprească sub text. În proprietățile terenului, **Etichete de pantă** le numără, iar **șterge toate** le scoate pe toate.

**Săpătură și umplutură.** Pensula (**Ridică**, **Coboară**, **Nivelează**, **Netezește**) modelează terenul, iar săpătura și umplutura ei se măsoară față de terenul ridicat topografic: cantitățile de săpătură și umplutură ale terenului (*CutVolume*, *FillVolume*) numără sistematizarea făcută cu pensula împreună cu platformele, rigolele și aleile, iar *SculptCut* și *SculptFill* dau doar partea pensulei. **Cantitățile** terenului din proprietăți arată **Săpătura** și **Umplutura** lui lângă **Săpătură netă − umplutură**, apoi săpătura și umplutura fiecărei platforme sub numele ei (*Lapin_EarthworkQuantities*), cum le are fișierul IFC. **Grilă săpătură/umplutură** din proprietăți (2, 5 sau 10 m; **oprită** implicit) scrie diferența în fiecare punct al grilei în plan, albastru unde terenul a fost ridicat (+0.45), roșu unde a fost săpat (−0.30), gri unde e ca înainte. O grilă prea deasă pentru teren (peste 2500 de valori) se scrie la un pas mai mare, un multiplu întreg al celui cerut, iar curbele de nivel care ar fi peste 2000 de linii pe înălțimea terenului se desenează la o echidistanță mai mare; în ambele cazuri terenul o spune. Hărțile de analiză, cotele punctuale, etichetele de pantă și grila săpătură/umplutură citesc terenul proiectat, cum îl are terenul 3D: o platformă plană la cota ei, cu taluzurile în jur, canalul unei rigole cu fundul pe profilul ei proiectat. **Zonă sistematizată → fixează terenul existent** ia terenul așa cum este acum drept teren existent, ca următoarele tușe să se măsoare de acolo; **revino la ridicare** se întoarce la ridicarea topografică.

![Proprietățile terenului: punctele, limita, etichetele de pantă, subregiunile, grila săpătură/umplutură și zona sistematizată](shots/terrain-properties.webp)

**Editarea punctelor în 3D.** **editează în 3D**, lângă **Puncte** ale terenului, pune un mâner pe fiecare punct ridicat în vederea 3D: trage-l în sus sau în jos, ori scrie distanța, și punctul se mută cu atât, câte un pas de anulare. Un punct tăiat de limită nu primește mâner. O ridicare mare (o suprafață LandXML cu mii de puncte) arată mânere pe cele 400 de puncte cele mai apropiate de mijlocul vederii 3D; mută vederea ca să ajungi la celelalte. **gata cu editarea 3D** scoate mânerele.

**O cotă scrisă.** În timpul editării, un clic pe mânerul unui punct (fără să-l tragi) îl alege, desenat plin; mai dă clic ca să alegi mai multe, încă o dată ca să renunți la unul. Sub **Puncte**, proprietățile terenului arată câte sunt **alese** și un câmp pentru cota lor (absolută, ±0.00 = 0; cea de acum când o au la fel): scrie-o, iar **setează cota** (sau <kbd>Enter</kbd>) pune toate punctele alese la acea cotă, un pas de anulare. **alege-le pe ale conturului** alege deodată toate punctele de pe contur, ca să setezi un perimetru întreg; **renunță** nu mai alege niciunul. În plan aceleași puncte au și ele mânere, în timpul editării: un clic dreapta pe unul oferă **Setează cota…** (pentru punctele alese odată cu el, dacă e unul dintre ele) și **Alege punctul**; un colț al conturului care stă pe un punct le oferă lângă comenzile lui de colț.

![Proprietățile terenului cu două puncte alese și cota lor scrisă](shots/terrain-picked-properties.webp)

![Cele două puncte ale arcului la noua lor cotă](shots/terrain-outline-raised.webp)

**O rețea de control.** Pe o ridicare deasă, mutarea punctelor unul câte unul e lentă. Câteva puncte pot deveni **puncte de control** care le modelează pe celelalte, ca o rețea low-poly: la **Rețea de control**, **la fiecare** *10* **m** ia punctul cel mai apropiat de fiecare nod al unei grile de 10 m (și colțurile), **pe contur** ia punctele de pe conturul terenului, iar **șterge toate** le scoate pe toate. În timpul editării în 3D, **Mânere pe** alege ce primește mâner: **toate punctele** (ca înainte), **doar punctele de control**, sau **alege punctele de control**, unde se văd toate punctele, cele de control pline; un clic pe mânerul unui punct îl face punct de control, sau din nou punct liber. Cu **doar punctele de control**, trage un punct de control în sus sau în jos (ori scrie distanța): ajunge exact acolo, iar punctele libere din jur îl urmează lin, cu o parte din mutarea lui care scade cu distanța și se stinge la celelalte puncte de control, care rămân pe loc. Punctele libere își păstrează forma (denivelări, pante) și se mută cu ea, iar readucerea punctului de control pune toate punctele înapoi; toată mutarea e un singur pas de anulare. Un punct mutat singur (cu **toate punctele**) își păstrează mutarea. Un teren fără puncte de control merge ca înainte. Împărțirea unui teren lasă fiecărei jumătăți punctele de control din ea; unirea le adună.

**Subregiuni.** **Subregiune** (în grupul **Terasamente**) desenează o zonă a terenului cu materialul ei (pietriș, implicit): o curte, un gazon, un pat de pietriș. Terenul își păstrează cotele; zona își poartă materialul în 3D, ca o pieliță subțire care urmează chiar triunghiurile terenului, cu liniile de frângere cu tot, și e conturată în plan. Fiecare își dă aria în plan listelor (*Lapin_SubregionQuantities*). **Subregiuni** din proprietățile terenului le numără, iar **șterge toate** le scoate.

![O subregiune de pietriș pe teren, în 3D](shots/terrain-subregion-3d.webp)

## Planul de situație și planul de încadrare

Browserul proiectului are, sub niveluri, **Terenul** (*Site*): desenele amplasamentului, care nu țin de niciun etaj. Un proiect nou pornește cu două:

- **Planul de situație**, la 1:500: tot modelul văzut de sus, toate etajele deodată, cu acoperișuri cu tot, cu terenul, aleile, plantele, parcela și vecinii. Ce stă în întregime sub ±0,00 (fundațiile) rămâne pe dinafară, nimic nu e secționat și nu se desenează linii ascunse. Colțurile parcelei sunt numerotate;
- **Planul de încadrare în zonă**, la 1:2000: același desen pentru amplasament în zona lui, peste ortofotoplan, fără numerele colțurilor; clădirile proiectului sunt umplute cu roșu și parcela e conturată cu roșu, ca amplasamentul să iasă în evidență față de vecini.

**+** de lângă Teren face încă unul din oricare; butoanele de copiere le duplică. Amândouă au **săgeata nordului**, spre nordul geografic (Gestionare → Amplasament: unghiul față de nordul geografic), **scara grafică** și, când proiectul e legat de hartă, **rețeaua de coordonate**: cruci unde se întâlnesc valori rotunde ale coordonatelor E și N, din zecime în zecime de scară (50 m la 1:500, 200 m la 1:2000), cu valorile lor. Săgeata și scara sunt adnotări ale vederii: selecteaz-o pe una ca s-o muți cu mânerul, să-i schimbi mărimea (**Mărime**, în mm) sau **Simbolul de marcaj**, ori s-o ștergi. Până o muți, **Poziție** e *de la sine*: săgeata stă în dreapta sus a desenului, scara sub colțul lui din stânga jos, urmând desenul când crește; **Pune-l înapoi de la sine** o aduce la loc. **Aspectul** scării e *cel Lapin* sau *de plan de situație*, cu **Lungimea**, **Diviziunile** și **Scara** ei. Selectează vederea în browser și le alegi în proprietățile ei, la **Setările vederii**: ce este (plan de situație sau de încadrare), pasul rețelei (0: deloc), săgeata și scara (bifate sau debifate, adaugă sau scot adnotările) și **Evidențiază proiectul** (umplerea și conturul roșu, pornite implicit în planul de încadrare, iar în planul de situație când le vrei).

**Harta de dedesubt.** Ortofotoplanul se desenează sub orice plan de situație și de încadrare, pe orice etaj ar fi fost importat, și se tipărește cu ele pe planșe și în PDF. Pentru planul cadastral sau desenul topografului, deschide planul de situație și folosește **Importă DXF…** cu **Așezat: după coordonatele hărții**. Lapin îți propune singur asta când coordonatele din fișier arată ca ale unei hărți, de exemplu Stereo 70: colțul din stânga jos al desenului la zeci de kilometri de zero, în unitatea fișierului sau în metri. Un plan al tău în milimetri e așezat după originea lui chiar și pe un plan de situație, în unitatea pe care o spune fișierul. Desenul ajunge atunci unde spun coordonatele lui E și N, prin punctul topografic al proiectului și unghiul lui față de nordul geografic. Dacă proiectul nu e încă legat de hartă, desenul îl leagă. Bifează **Ca legătură** ca să rămână doar în citire și să-l reîncarci când topograful trimite unul nou. Importă-l o dată în fiecare desen care are nevoie de el.

![Un plan de încadrare: casa umplută cu roșu, parcela conturată cu roșu, între vecini](shots/site-location-plan.webp)

![Un plan de situație: casa văzută de sus pe parcela ei, colțurile numerotate, rețeaua hărții, săgeata nordului și scara grafică](shots/site-drawings.webp)

![Săgeata nordului selectată pe un plan de situație: proprietățile ei](shots/view-north-arrow.webp)

![Săgeata nordului mutată cu mânerul și făcută mai mare](shots/view-north-arrow-moved.webp)

![Terenul în browserul proiectului](shots/site-browser.webp)

Extrasul **Inventar de coordonate** (Extrase → + → Inventar de coordonate) listează colțurile parcelei așa cum le numerotează planul de situație, cu coordonatele N și E în sistemul proiectului, latura până la colțul următor și aria. Ca orice extras, se pune pe planșă lângă planul de situație și se exportă CSV sau XLSX.

## Arbori și arbuști

**Plantă** (PN) pune o plantă la un clic, **Zonă de plantare** (PZ) însămânțează un contur cu un amestec de specii la o densitate; selectează-le și alege speciile în proprietăți. Speciile vin dintr-o bibliotecă de plante native sau tradiționale în România, pe grupe: **pomi fructiferi** (măr, păr, prun, cireș, vișin, cais, gutui), **conifere** (brad, molid, pin silvestru, pin negru), **foioase** (nuc, tei, mesteacăn, arțar), **arbuști** (alun, corn, soc, măceș, liliac, cătină, coacăz, tuia, cimișir) și **acoperire de sol**. Fiecare are denumirea latină, înălțimea și coroana obișnuite și apare cu numele românesc când interfața e în română. Extrasul de plantare arată denumirile latină și românească lângă fiecare specie.

În plan fiecare plantă se desenează văzută de sus, cu o linie subțire, cât coroana ei, doar prin conturul ei: nucul sau teiul o coroană lobată moale, pomul fructifer un cerc, bradul și molidul o stea, pinul un inel zimțat, arbustul un mic nor, planta de gard viu tunsă un cerc, cu o cruciuliță la trunchiul unui arbore. Fațadele și secțiunile desenează fiecare plantă ca un singur contur: coroana unui arbore pe trunchiul lui, conul în trepte al unui conifer, cupola unui arbust. Câteva linii fiecare, așa că un plan de situație cu sute de arbori rămâne ușor. În 3D coniferele sunt conuri, ceilalți arbori au coroana rotunjită pe trunchi, iar arbuștii o cupolă joasă.

![O livadă mică în plan: pomi fructiferi, un nuc, conifere și arbuști](shots/planting-symbols.webp)

Pune **Anturajul în 2D** al stilului vederii pe **detaliat** pentru simbolurile unui plan de peisagistică, în negru: pomul fructifer o coroană lobată cu ramuri bifurcate, nucul sau teiul o coroană festonată în jurul unui miez închis, bradul și molidul o stea de ramuri cu ace, pinul un smoc rotund de ace, arbustul un mic nor. Lapin desenează singur fiecare simbol, așa că arborii vecini diferă puțin și o zonă nu arată ștampilată; la 1:1000 și mai mic (planul de încadrare) se desenează doar contururile coroanelor.

![Aceeași livadă desenată detaliat](shots/planting-symbols-detailed.webp)

## Oameni și mașini

**Anturaj** (EN) pune un om sau o mașină la un clic; alegi ce în bara liniei de comandă cât timp unealta e pornită: un **bărbat în picioare**, o **femeie mergând**, un **copil**, o **persoană la telefon**, o **mașină** sau un **SUV / dubă**. **Rând de anturaj** (ER) pune un rând de-a lungul unei linii pe care o desenezi: mașini la 2,5 m, parcate una lângă alta de-a curmezișul ei, cu fața spre stânga liniei, oameni la 1,2 m, mergând de-a lungul ei. Fiecare stă pe terenul de sub el, sau pe pardoseala etajului lui unde nu e teren.

![Oameni la ușă și mașini parcate lângă casă](shots/entourage-3d.webp)

Selectează unul ca să-l schimbi în proprietăți: felul, **rotația**, **înălțimea** (implicit mărimea obișnuită a felului său), cât stă **deasupra terenului** (pe o terasă sau un balcon) și **culoarea** hainelor sau a caroseriei. Îl muți, rotești, oglindești și copiezi ca pe orice element.

În 3D sunt figuri simple: oameni cu membre, haine, păr și pantofi, mașini cu geamuri, roți și faruri. Desenele îi păstrează simpli, cum desenează un arhitect anturajul, cu o linie subțire: planurile și planurile de situație de sus, umerii și capul unui om, conturul unei mașini cu colțurile rotunjite și parbrizul ei; fațadele și secțiunile ca siluete, un singur contur fiecare, silueta unui om, latura sau fața unei mașini cum o vede vederea, niciodată liniile corpurilor lor.

![În plan](shots/entourage-plan.webp)

![În fațada sudică](shots/entourage-elevation.webp)

![Proprietățile unei mașini](shots/entourage-properties.webp)

Cum se desenează în 2D oamenii, mașinile și plantele e o setare a stilului vederii, **Anturajul în 2D** (în proprietățile vederii sau **Vedere → Setările vederii**; pentru un stil întreg, **Gestionare → Stiluri de vedere**): **contururi** (implicit), **abstract** (un om un cerc în plan și un cap peste un corp într-o fațadă, o mașină o cutie rotunjită, un arbore un cerc cu trunchiul lui), **detaliat** (simbolurile de peisagistică ale plantelor și fiecare linie a corpurilor, cu umbrele de soare ale vederii, cum le desena Lapin înainte) sau **nedesenat**. **Umplerea siluetelor** umple siluetele din fațade și secțiuni cu gri deschis sau verde pal. Se desenează cu penița 2 (0,13 mm la 1:100), dacă **Gestionare → Stiluri de obiecte** nu dă alta categoriei **Anturaj**, iar **Vizibilitate / Grafică** ascunde categoria într-o vedere. Planșele, PDF-ul și DXF-ul îi tipăresc cum îi desenează vederea; corpurile lor 3D nu se schimbă niciodată. Proiectele făcute înainte îi desenează și ele ca contururi.

Dau desenelor și imaginilor scara, dar nu fac parte din clădire. Un export IFC sau Fragments (File → Export…) îi lasă afară dacă nu bifezi **Anturaj** în lista de feluri; IFC îi scrie ca IfcBuildingElementProxy „Entourage”. Exporturile 3D (glTF, USD, COLLADA) îi iau. Testele de coliziuni îi lasă afară.

### Imaginile tale

**Feluri de anturaj…** (în grupul **Anturaj**) face feluri ale proiectului din imagini: **Adaugă o imagine…** ia un PNG sau un JPEG (cel mult 8 MB; unul mai mare de 2048 de pixeli e micșorat) și face din el un fel, cu numele fișierului, înalt de 1,75 m și lat cât spune proporția imaginii. Numele, **înălțimea**, **lățimea** și **grupul** se schimbă în listă (în unitățile proiectului): un fel din grupul **Mașini** stă într-un rând de anturaj la 2,5 m, ca mașinile. Felurile apar apoi la **Imagini** în bara liniei de comandă și în proprietățile unei figuri.

![Felurile proiectului, fiecare cu înălțimea, lățimea și grupul lui](shots/entourage-kinds.webp)

O figură dintr-un fel cu imagine stă ca un carton subțire, cu imaginea pe ambele fețe în 3D; pixelii transparenți ai unui PNG sunt decupați, și din umbră, așa că un om sau un arbore decupat stă singur. Desenele o arată ca pe felurile încorporate: conturul cartonului în plan, un dreptunghi cât imaginea de lat și de înalt în fațade și secțiuni, nimic când **Anturajul în 2D** e **nedesenat**. O figură dintr-un fel cu imagine ia înălțimea felului, dacă nu-i dai una proprie; culoarea nu i se aplică.

Imaginile și felurile se păstrează în proiect (salvate, anulate, în versiuni) și nu se scriu niciodată într-un IFC; figurile se exportă ca restul anturajului. Un fel folosit de figuri nu se șterge: Lapin spune câte îl folosesc, așa că dă-le întâi alt fel. Un fel șters își ia imaginea cu el, dacă nu o mai folosește un material sau alt fel.

Figurile urmează oamenii și mașinile din [BimGo](https://github.com/aussieBIMguru/BimGo) de Aussie BIM Guru (MIT-0): proporțiile, pozițiile și profilele lor.

## Porți

Grupul **Porți** din fila Teren pune porți cu stâlpii lor: **Poartă batantă**, **Poartă batantă dublă** (<kbd>GT</kbd>), **Poartă culisantă**, **Poartă autoportantă**, **Portiță**, **Poartă cu portiță alăturată** și **Poartă pliantă**. Lapin nu are gard propriu: desenează gardul ca perete (unul scund, de tipul dorit) sau lasă stâlpii porții să stea pe linia parcelei.

Dă clic pe cele două capete ale porții. Cu **Pe linii** pornit în bară (implicit), mijlocul porții se pune pe axul celui mai apropiat perete sau pe linia desenată cea mai apropiată, până la 1,5 m (un perete, latura parcelei, o linie desenată), de-a lungul ei; oprit, poarta stă între cele două puncte. Bara alege și tipul pe care îl pune unealta, dintre stilurile proiectului sau cele incluse în Lapin.

O poartă pusă pe un perete îl taie de la fețele exterioare ale stâlpilor, pe toată înălțimea, deci zidul gardului se oprește la stâlpi.

![O poartă culisantă și o portiță într-un zid de gard, o poartă dublă pe latura parcelei](shots/gates-plan.webp)

Planurile desenează stâlpii secționați, canatele deschise după unghiul lor de deschidere, cu arcul lor, panourile unei porți pliante împăturite; o poartă culisantă închisă, cu o săgeată în sensul în care se deschide, șina ei și zona în care parchează punctată, cât canatul de lungă (canatul unei porți autoportante e mai lung decât deschiderea cu coada de contragreutate, cam 45%). Fațadele, secțiunile și 3D-ul desenează corpurile.

![Porțile în 3D](shots/gates-3d.webp)

### Configuratorul

**Tipuri de porți** deschide configuratorul: stilurile proiectului și cele incluse în Lapin, de adăugat cu un clic (o poartă dublă din fier forjat, una culisantă cu lamele orizontale, una pliantă din oțel, o portiță arcuită, o poartă de acces din lemn, una cu plasă de siguranță, una autoportantă automată, una dublă cu portiță, o poartă de fermă, una culisantă din tablă perforată, o portiță de grădină din lemn). Un tip stabilește, pe grupe, doar ce ia felul lui:

- **Poarta:** lățimea liberă și înălțimea, distanța față de teren, lățimea portiței, panourile unei porți pliante pe fiecare parte, unghiul de deschidere;
- **Canatele:** mărimea ramei, umplutura (bare verticale, lamele orizontale, panou plin, plasă sau tablă perforată), pasul barelor sau lamelelor, vârfuri de suliță pe bare, coronamentul (drept, arcuit sau ascuțit) și săgeata lui;
- **Stâlpii:** din zidărie, din oțel sau fără, mărimea și înălțimea lor, căciula unui stâlp de zidărie (dreaptă sau piramidă);
- **Feroneria:** un motor (un braț pe canatele unei porți batante, o cutie lângă șina unei porți culisante) și mâner cu broască;
- **Materialele** pe roluri: ramă, umplutură, stâlpi, căciuli, feronerie și motor. Un material numit aici pe care proiectul nu-l are se creează.

Previzualizarea e a motorului: planul cu arcul sau zona de parcare sus, fațada jos. Fiecare poartă de un tip urmează o schimbare.

![Configuratorul de porți cu o poartă dublă din fier forjat](shots/gates-types.webp)

Selectează o poartă ca să-i schimbi tipul, rotația, cât stă deasupra terenului, spre ce parte **se deschid** canatele, pe ce mână sunt balamalele (sau unde parchează o poartă culisantă) și cât e desenată deschisă (0: închisă). **Cealaltă parte** și **Cealaltă mână** din proprietăți, sau săgețile de lângă ea în plan, le întorc. **Modify → Mirror** îi dă cealaltă mână.

![Proprietățile unei porți](shots/gates-properties.webp)

### Extras, cost și IFC

**Extrasul de porți** arată porțile pe tipuri: felul, deschiderea liberă, canatele, umplutura, coronamentul, stâlpii, motorul și câte sunt. Un tip nou are articolele de deviz `LAP-GATE` și, cu motor, `LAP-GATEMOT`, numărate pe bucată.

IFC scrie o poartă ca **IfcDoor** cu PredefinedType **GATE**, a unui **IfcDoorType** cu felul ei de deschidere (DOUBLE_DOOR_SINGLE_SWING, SLIDING_TO_LEFT, DOUBLE_DOOR_FOLDING…; una cu portiță USERDEFINED), lățimea și înălțimea liberă ca OverallWidth și OverallHeight și Pset_DoorCommon. Stâlpii fac parte din corpul porții, deci un singur element poartă poarta așa cum a fost configurată, iar Lapin o citește înapoi ca poarta lui.

## Reglementări urbanistice și însorire (Lapin Urban)

Ediția **Lapin Urban** adaugă în această filă grupul **Urbanism**: reglementările parcelei (POT, CUT, înălțimile maxime, retragerile față de stradă, laterale, limita posterioară și axul drumului), **volumul construibil** pe care îl lasă, indicatorii pe care îi atinge modelul față de ele și **studiul de însorire**: soarele și umbrele lui în 3D pentru orice zi și oră în locul proiectului, și planuri de umbre pentru echinocțiu și solstiții.
