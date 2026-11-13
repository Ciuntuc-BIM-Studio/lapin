---
title: Amplasamentul și datele lui
order: 52
summary: Adu ridicarea topo, parcela, vecinii și un ortofotoplan în coordonate reale, din DXF, CSV, GeoJSON, CityJSON și o imagine georeferențiată.
---

Fila **Teren** ține terenul pe care stă clădirea: suprafața terenului, parcela, clădirile vecine și o fotografie aeriană, fiecare acolo unde este în realitate. Grupul **Import** le aduce din fișierele pe care le predau topograful și cadastrul.

![Fila Teren](shots/ribbon-site.webp)

## Coordonate reale

O ridicare topo vine în coordonate de hartă: estică și nordică, la sute de kilometri de zero. Lapin ține modelul lângă origine și îl leagă de hartă prin **punctul de referință topo**. Primul import într-un proiect face asta singur: punctul de referință ajunge în originea planului, pe primele coordonate din fișier rotunjite la metru, iar fiecare import următor cade în același loc.

**Gestionare → Amplasament** arată **sistemul de coordonate** în care sunt estica și nordica, și punctele de bază și de referință. Lapin cunoaște Stereo 70 (EPSG:3844, sistemul național al României), zonele UTM 34 și 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) și longitudinea și latitudinea (WGS 84, EPSG:4326). Un fișier în alt sistem dintre acestea se convertește la import: un GeoJSON în longitudine și latitudine ajunge pe planul în Stereo 70 acolo unde îi e locul. Exportul IFC scrie sistemul și punctul de referință, așa că și alte programe așază modelul pe hartă. Lambert-93 (EPSG:2154, grila națională a Franței) e cunoscut și el.

![Gestionare → Amplasament: sistemul de coordonate și punctul de referință](shots/site-location.webp)

**Punctele proiectului**, ca în Revit. **Vedere → Punctele de coordonate** (sau **Arată originea internă, punctul de bază și punctul topografic în planuri și în 3D** din Gestionare → Amplasament) desenează trei puncte în planuri și în vederea 3D:

- **originea internă**, 0,0 al planului, care nu se mută niciodată: axa X cu roșu și axa Y cu verde (și Z cu albastru în 3D);
- **punctul de bază al proiectului**, un cerc cu o cruce: cota lui (unde stă ±0,00), coordonatele lui pe hartă când proiectul e legat de hartă și o săgeată spre **nordul geografic**, cu unghiul ei, când proiectul e rotit față de el (**Nordul geografic** din Gestionare → Amplasament);
- **punctul topografic**, un triunghi, cu estul, nordul și cota lui.

Un plan face snap la toate trei. Vederea 3D le desenează la locul lor, la orice zoom, deasupra modelului.

![Originea internă, punctul de bază rotit cu 12,5° față de nordul geografic și punctul topografic în 3D](shots/location-3d.webp)

**Coordonatele la cursor.** Cât timp cursorul e într-un plan, bara de stare arată unde se află. Un clic pe coordonate alege față de ce se citesc: **originea internă**, **punctul de bază al proiectului**, **punctul topografic (hartă)** (est și nord, rotite spre nordul geografic, așa cum le scrie exportul IFC) sau **UCS-ul** (vezi [Snap și ajutoare](drafting.md#snap-și-ajutoare)). Alegerea se păstrează în **Fișier → Preferințe… → Coordonate**.

**Planurile unui proiect georeferențiat.** Modelul se desenează în coordonatele lui, lângă originea internă; cele ale hărții se calculează din punctul topografic, așa că un plan rămâne la fel de precis și de rapid ca oricare altul. Trei lucruri îl pun pe hartă:

- **Cote de coordonate.** **Adnotări → Cote → Cotă de coordonate** (DIMCOORD): un clic pe punct, apoi unde se termină linia de indicație. Scrie **E** și **N** ale punctului, în metri, prin punctul topografic și rotite spre nordul geografic; fără punct topografic, **X** și **Y** față de punctul de bază al proiectului. Tastează întâi **M**, **P** sau **I** ca să alegi harta, punctul de bază sau originea planului. Valorile se calculează la fiecare desenare: mută punctul topografic și toate cotele de coordonate se rescriu.
- **Un plan în coordonatele hărții.** Butonul **DXF** din bara planului îl exportă **în coordonatele planului** (milimetri față de originea internă), **față de punctul de bază al proiectului** (milimetri) sau **în coordonatele hărții** (E și N în metri, rotite spre nordul geografic), ca desenul să se așeze peste planul cadastral sau peste DXF-ul topografului.
- **Nordul geografic în sus.** Când proiectul e rotit față de nordul geografic, butonul cu busola din bara planului rotește planul pe ecran ca nordul geografic să fie în sus, și înapoi. Desenul, snap-urile, grila și UCS-ul se rotesc cu el; alegerea se păstrează pentru fiecare plan, pe acest calculator. Planșele și PDF-ul rămân cu nordul proiectului în sus. Pentru un plan tipărit rotit, folosește în schimb **Rotește vederea → Nordul geografic în sus** (vezi [Rotirea unui plan](views.md#rotirea-unui-plan)): orientarea ține de vedere, se păstrează cu proiectul, iar planșele o tipăresc cum se vede.

![O cotă de coordonate pe planul unui proiect georeferențiat, cu nordul geografic în sus](shots/spot-coordinate.webp)

## Importul

- **Teren din puncte** citește un fișier CSV sau text de la topograf: x y z, sau număr nordică estică cotă, câte un punct pe linie.
- **Puncte din DXF** citește un plan topografic: entități POINT cu cotele lor, blocuri inserate la o cotă sau care o au ca atribut, cote scrise lângă puncte („245.37” lângă cruciuliță) și curbe de nivel la cota lor. Când planul are puncte pe mai multe straturi, Lapin le arată cu câte puncte are fiecare, iar tu alegi ce iei. Punctele fac un teren. Un DXF nu numește niciun sistem, deci Lapin ia numerele de mărimea unei hărți după intervalul lor (Stereo 70, Lambert-93, UTM). Numerele unui sistem pe care nu-l cunoaște rămân cum sunt, iar Lapin spune asta: numește sistemul în **Gestionare → Amplasament** sau convertește mai întâi desenul.
- **GeoJSON** aduce parcele, linii și puncte. Un fișier cu un singur poligon dă **parcela**; unul cu mai multe dă suprafețe. Parcela devine și conturul parcelei terenului, cu aria ei. Fișierele de la cadastru, în Stereo 70, spun asta în fișier, iar Lapin citește; un fișier care nu spune nimic e în longitudine și latitudine, cum cere standardul GeoJSON.
- **CityJSON** aduce clădirile vecine cu suprafețele lor, la locul și la înălțimea lor. Sunt referințe care nu se editează; vor arunca umbre în studiul de însorire.
- **Ortofotoplan** pune o fotografie aeriană sub amplasament. Alege imaginea (JPEG sau PNG, cel mult 8 MB) împreună cu fișierul ei de georeferențiere (`.jgw`, `.pgw` sau `.wld`): el spune unde stă imaginea. Fotografia se desenează sub plan și îmbracă terenul în 3D.

![Planul de situație: ortofotoplanul, curbele de nivel ale terenului, parcela și amprenta vecinului](shots/site-plan.webp)

![Același amplasament în 3D: fotografia pe teren, vecinul la înălțimea lui](shots/site-3d.webp)

Tot ce importi se păstrează în fișierul proiectului. Un element își ia numele din fișier (numărul cadastral al parcelei, când fișierul îl are) și păstrează celelalte proprietăți din fișier; selectează-l ca să-i vezi aria sau lungimea.

## Drumuri, curbe și liniatură

Un **drum** sau o **alee** își urmează axa. În **Coridor…** (CR), fila **Curbe** dă fiecărui vârf al axei o rază cu clotoidă la intrare și la ieșire, cum trasează un proiectant de drumuri o curbă; un vârf lăsat la 0 rămâne colț, iar curbele care nu încap între vecinele lor sunt refuzate, cu motivul. Kilometrajul, profilul longitudinal și profilurile transversale urmează axa curbă, iar IFC 4.3 o scrie ca aliniament.

Liniatura CAD devine elemente de teren: **Drum din desen CAD** (LR) și **Linie de rupere din desen CAD** (BL) iau o linie sau o polilinie desenată sau adusă dintr-un DXF, urmează toate liniile de pe stratul ei care se leagă cap la cap și fac un drum de-a lungul ei sau o linie de rupere a terenului. Când liniile au cote (liniile 3D ale unui DXF), ele dau profilul drumului sau cotele liniei de rupere. Asemenea linii se păstrează cu cotele lor și se pot arăta în 3D (categoria *liniatură*).

## Planul de situație și planul de încadrare

Browserul proiectului are, sub niveluri, **Terenul** (*Site*): desenele amplasamentului, care nu țin de niciun etaj. Un proiect nou pornește cu două:

- **Planul de situație**, la 1:500: tot modelul văzut de sus, toate etajele deodată, cu acoperișuri cu tot, cu terenul, aleile, plantele, parcela și vecinii. Ce stă în întregime sub ±0,00 (fundațiile) rămâne pe dinafară, nimic nu e secționat și nu se desenează linii ascunse. Colțurile parcelei sunt numerotate;
- **Planul de încadrare în zonă**, la 1:2000: același desen pentru amplasament în zona lui, peste ortofotoplan, fără numerele colțurilor; clădirile proiectului sunt umplute cu roșu și parcela e conturată cu roșu, ca amplasamentul să iasă în evidență față de vecini.

**+** de lângă Teren face încă unul din oricare; butoanele de copiere le duplică. Amândouă au **săgeata nordului**, spre nordul geografic (Gestionare → Amplasament: unghiul față de nordul geografic), **scara grafică** și, când proiectul e legat de hartă, **rețeaua de coordonate**: cruci unde se întâlnesc valori rotunde ale coordonatelor E și N, din zecime în zecime de scară (50 m la 1:500, 200 m la 1:2000), cu valorile lor. Selectează vederea în browser și le alegi în proprietățile ei, la **Setările vederii**: ce este (plan de situație sau de încadrare), pasul rețelei (0: deloc), săgeata, scara și **Evidențiază proiectul** (umplerea și conturul roșu, pornite implicit în planul de încadrare, iar în planul de situație când le vrei).

**Harta de dedesubt.** Ortofotoplanul se desenează sub orice plan de situație și de încadrare, pe orice etaj ar fi fost importat, și se tipărește cu ele pe planșe și în PDF. Pentru planul cadastral sau desenul topografului, deschide planul de situație și folosește **Importă DXF…** cu **Așezat: după coordonatele hărții**. Lapin îți propune singur asta când coordonatele din fișier arată ca ale unei hărți, de exemplu Stereo 70: colțul din stânga jos al desenului la zeci de kilometri de zero, în unitatea fișierului sau în metri. Un plan al tău în milimetri e așezat după originea lui chiar și pe un plan de situație, în unitatea pe care o spune fișierul. Desenul ajunge atunci unde spun coordonatele lui E și N, prin punctul topografic al proiectului și unghiul lui față de nordul geografic. Dacă proiectul nu e încă legat de hartă, desenul îl leagă. Bifează **Ca legătură** ca să rămână doar în citire și să-l reîncarci când topograful trimite unul nou. Importă-l o dată în fiecare desen care are nevoie de el.

![Un plan de încadrare: casa umplută cu roșu, parcela conturată cu roșu, între vecini](shots/site-location-plan.webp)

![Un plan de situație: casa văzută de sus pe parcela ei, colțurile numerotate, rețeaua hărții, săgeata nordului și scara grafică](shots/site-drawings.webp)

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
