---
title: Amplasamentul și datele lui
order: 52
summary: Adu ridicarea topo, parcela, vecinii și un ortofotoplan în coordonate reale, din DXF, CSV, GeoJSON, CityJSON și o imagine georeferențiată.
---

Fila **Teren** ține terenul pe care stă clădirea: suprafața terenului, parcela, clădirile vecine și o fotografie aeriană, fiecare acolo unde este în realitate. Grupul **Import** le aduce din fișierele pe care le predau topograful și cadastrul.

![Fila Teren](shots/ribbon-site.webp)

## Coordonate reale

O ridicare topo vine în coordonate de hartă: estică și nordică, la sute de kilometri de zero. Lapin ține modelul lângă origine și îl leagă de hartă prin **punctul de referință topo**. Primul import într-un proiect face asta singur: punctul de referință ajunge în originea planului, pe primele coordonate din fișier rotunjite la metru, iar fiecare import următor cade în același loc.

**Gestionare → Amplasament** arată **sistemul de coordonate** în care sunt estica și nordica, și punctele de bază și de referință. Lapin cunoaște Stereo 70 (EPSG:3844, sistemul național al României), zonele UTM 34 și 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) și longitudinea și latitudinea (WGS 84, EPSG:4326). Un fișier în alt sistem dintre acestea se convertește la import: un GeoJSON în longitudine și latitudine ajunge pe planul în Stereo 70 acolo unde îi e locul. Exportul IFC scrie sistemul și punctul de referință, așa că și alte programe așază modelul pe hartă.

![Gestionare → Amplasament: sistemul de coordonate și punctul de referință](shots/site-location.webp)

## Importul

- **Teren din puncte** citește un fișier CSV sau text de la topograf: x y z, sau număr nordică estică cotă, câte un punct pe linie.
- **Puncte din DXF** citește un plan topografic: entități POINT cu cotele lor, blocuri inserate la o cotă sau care o au ca atribut, cote scrise lângă puncte („245.37” lângă cruciuliță) și curbe de nivel la cota lor. Când planul are puncte pe mai multe straturi, Lapin le arată cu câte puncte are fiecare, iar tu alegi ce iei. Punctele fac un teren.
- **GeoJSON** aduce parcele, linii și puncte. Un fișier cu un singur poligon dă **parcela**; unul cu mai multe dă suprafețe. Parcela devine și conturul parcelei terenului, cu aria ei. Fișierele de la cadastru, în Stereo 70, spun asta în fișier, iar Lapin citește; un fișier care nu spune nimic e în longitudine și latitudine, cum cere standardul GeoJSON.
- **CityJSON** aduce clădirile vecine cu suprafețele lor, la locul și la înălțimea lor. Sunt referințe care nu se editează; vor arunca umbre în studiul de însorire.
- **Ortofotoplan** pune o fotografie aeriană sub amplasament. Alege imaginea (JPEG sau PNG, cel mult 8 MB) împreună cu fișierul ei de georeferențiere (`.jgw`, `.pgw` sau `.wld`): el spune unde stă imaginea. Fotografia se desenează sub plan și îmbracă terenul în 3D.

![Planul de situație: ortofotoplanul, curbele de nivel ale terenului, parcela și amprenta vecinului](shots/site-plan.webp)

![Același amplasament în 3D: fotografia pe teren, vecinul la înălțimea lui](shots/site-3d.webp)

Tot ce importi se păstrează în fișierul proiectului. Un element își ia numele din fișier (numărul cadastral al parcelei, când fișierul îl are) și păstrează celelalte proprietăți din fișier; selectează-l ca să-i vezi aria sau lungimea.

## Planul de situație și planul de încadrare

Browserul proiectului are, sub niveluri, **Terenul** (*Site*): desenele amplasamentului, care nu țin de niciun etaj. Un proiect nou pornește cu două:

- **Planul de situație**, la 1:500: tot modelul văzut de sus, toate etajele deodată, cu acoperișuri cu tot, cu terenul, aleile, plantele, parcela și vecinii. Ce stă în întregime sub ±0,00 (fundațiile) rămâne pe dinafară, nimic nu e secționat și nu se desenează linii ascunse. Colțurile parcelei sunt numerotate;
- **Planul de încadrare în zonă**, la 1:2000: același desen pentru amplasament în zona lui, peste ortofotoplan, fără numerele colțurilor; clădirile proiectului sunt umplute cu roșu și parcela e conturată cu roșu, ca amplasamentul să iasă în evidență față de vecini.

**+** de lângă Teren face încă unul din oricare; butoanele de copiere le duplică. Amândouă au **săgeata nordului**, spre nordul geografic (Gestionare → Amplasament: unghiul față de nordul geografic), **scara grafică** și, când proiectul e legat de hartă, **rețeaua de coordonate**: cruci unde se întâlnesc valori rotunde ale coordonatelor E și N, din zecime în zecime de scară (50 m la 1:500, 200 m la 1:2000), cu valorile lor. Selectează vederea în browser și le alegi în proprietățile ei, la **Setările vederii**: ce este (plan de situație sau de încadrare), pasul rețelei (0: deloc), săgeata, scara și **Evidențiază proiectul** (umplerea și conturul roșu, pornite implicit în planul de încadrare, iar în planul de situație când le vrei).

![Un plan de încadrare: casa umplută cu roșu, parcela conturată cu roșu, între vecini](shots/site-location-plan.webp)

![Un plan de situație: casa văzută de sus pe parcela ei, colțurile numerotate, rețeaua hărții, săgeata nordului și scara grafică](shots/site-drawings.webp)

![Terenul în browserul proiectului](shots/site-browser.webp)

Extrasul **Inventar de coordonate** (Extrase → + → Inventar de coordonate) listează colțurile parcelei așa cum le numerotează planul de situație, cu coordonatele N și E în sistemul proiectului, latura până la colțul următor și aria. Ca orice extras, se pune pe planșă lângă planul de situație și se exportă CSV sau XLSX.

## Reglementări urbanistice și însorire (Lapin Urban)

Ediția **Lapin Urban** adaugă în această filă grupul **Urbanism**: reglementările parcelei (POT, CUT, înălțimile maxime, retragerile față de stradă, laterale, limita posterioară și axul drumului), **volumul construibil** pe care îl lasă, indicatorii pe care îi atinge modelul față de ele și **studiul de însorire**: soarele și umbrele lui în 3D pentru orice zi și oră în locul proiectului, și planuri de umbre pentru echinocțiu și solstiții.
