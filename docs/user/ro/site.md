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

**Taluzuri în jurul platformei.** Proprietățile platformei au **Taluz la săpătură** și **Taluz la umplutură**, ca 1 : N (orizontal pe vertical; 0, implicit, păstrează laturile verticale). Cu taluz, săpătura se lărgește în sus și umplutura se întinde în jos la acea pantă până întâlnesc terenul, așa că platforma se racordează la teren în loc să stea într-o groapă sau pe un mal vertical; săpătura și umplutura ei se măsoară pe volumele cu taluz. Taluzurile urcă și coboară cât terenul din jurul platformei, nu cât întregul teren. Un taluz care ar ieși atât de departe încât s-ar intersecta cu el însuși (un taluz lung în jurul unei crestături înguste a conturului) e refuzat: platforma nu se nivelează, nu i se măsoară nimic, iar terenul spune de ce; dă-i un taluz mai abrupt (un N mai mic) sau un contur mai simplu. O platformă cu goluri își păstrează laturile verticale. Un contur care se intersectează cu el însuși, al unei platforme, al unei parcele, al limitei terenului, al unei subregiuni sau al unei zone plantate, e refuzat. **Convertește taluzurile în sistematizare** din proprietățile unei platforme (când are taluz la săpătură sau la umplutură) face din taluzurile ei o sistematizare **Taluz la teren** din conturul ei, spre exterior, la același 1 : N, racordată exact la teren și ținută vie cât timp platforma se schimbă (vezi *Sistematizare între margini și taluzuri*); platforma însăși rămâne apoi tăiată vertical, iar sistematizarea nouă e selectată. Un pas de anulare o desface. O platformă neconvertită își păstrează taluzurile ca până acum.

## Portaluri de tunel și culee de pod

![Configuratorul pe presetarea cu aripi răsucite, previzualizarea motorului lângă valori](shots/portal-configurator.webp)

**Portal sau culee…** (<kbd>PO</kbd>, în grupul **Peisagistică**) trasează un perete frontal de-a curmezișul unui drum cu câte o aripă la fiecare capăt, ca un singur zid de sprijin ale cărui colțuri, fructuri și cantități sunt ale zidului de sprijin. Alege o presetare: **Aripi drepte** (portal de tunel, aripile în prelungirea peretelui frontal), **Aripi în U** (de-a lungul drumului), **Aripi evazate** (în unghi față de drum), **Aripi răsucite** (fața lor trece de la fructul peretelui frontal la panta taluzului) sau **Culee cu aripi** (culee de pod, rambleul ținut între aripi). Apoi dă-i valorile pe grupuri: lungimea peretelui frontal, coronamentul și cota tălpii; lungimea aripilor, unghiul lor față de drum și coronamentul la capătul liber; secțiunea (grosimea coronamentului, fructurile față și spate n:1, talpa, consolele); răsucirea; capătul liber; ieșirile. Previzualizarea de alături e a motorului (plan, o secțiune prin aripa stângă, 3D), făcută de același cod ca elementul.

**Formule.** Orice valoare poate fi o formulă: scrie `= embankment + 0.5` (sau `{= embankment + 0.5}`) și valoarea pe care o dă apare lângă ea. O formulă citește celelalte valori ale configuratorului după cheia lor (`crown`, `footingLevel`, `faceLength`, `wingLength`, `wingAngle`, `endCrown`, `embankment`, `slope`, `thickness`, `frontBatter`… și `height`, coronamentul peretelui frontal peste fața de jos a tălpii) și parametrii proiectului după cheia lor. Implicit, coronamentul la capătul liber este `{= embankment + 0.5}`, iar aripa e atât de lungă cât ajunge panta taluzului: `{= (crown - embankment) * slope}`, panta dată ca 1:m (1,5 pentru o pantă 2:3). O formulă care se citește pe sine sau un nume care nu există apar sub valori, iar valoarea implicită rămâne.

**Aripile.** Aripa dreaptă o oglindește pe stânga, dacă nu debifezi **Aripa dreaptă oglindește stânga**; atunci cea dreaptă își ia lungimea, unghiul și coronamentul ei. Stânga și dreapta sunt văzute de la peretele frontal, privind de-a lungul aripilor. Peste fiecare colț coronamentul rămâne orizontal, iar panta aripii începe imediat după el. La **Aripi răsucite**, fața trece de la fructul peretelui frontal la colț la **Fructul la capătul liber** (implicit al taluzului, `1 / slope`), pe lungimea pantei aripii sau, cu **Răsucire pe** *segment*, pe toată aripa. Fața răsucită e exactă: o singură față B-spline în IFC (IfcAdvancedBrep) și STEP, cu aria și volumul măsurate pe suprafața adevărată.

**Capătul liber** este cu capete **drepte** (vertical, perpendicular pe aripă), **înclinat** (după taluz, cu **Fructul capătului n:1**, talpa trecând de piciorul lui) sau **paralel** (rotit în jurul verticalei ca să meargă de-a lungul drumului; **Capătul rotit cu** dă alt unghi, până la 60°). O aripă în U e deja paralelă cu drumul, așa că își păstrează capătul drept.

**Așezarea.** **Așază…** cere două puncte: mijlocul peretelui frontal pe axul drumului, apoi un punct în direcția aripilor. **Pe un drum…** cere un drum sau o alee: portalul se așază de-a curmezișul lui unde dai clic, cu aripile în sensul lui. **Desenează traseul** ia o polilinie desenată în locul trasării din valori: primul și ultimul segment sunt aripile, restul peretele frontal. La **Secțiune**, un profil desenat și salvat ([Profile](profiles.md)) poate fi secțiunea elevației în locul celei codate: e întins în fiecare secțiune între fața de sus a tălpii și coronament, cu lățimea păstrată.

![Un portal de tunel cu aripi drepte, coronamentele coborând spre capetele libere](shots/portal-3d.webp)

**Modificarea.** Selectat, zidul arată **Configuratorul portalului…** în proprietăți: se deschide pe presetarea și valorile zidului (formulele cum le-ai scris), iar **Aplică** reface același element pe loc, într-un singur pas de anulare, cu GlobalId-urile păstrate.

**IFC și cantități.** **IFC** *ansamblu* (implicit) scrie portalul ca IfcElementAssembly (portalul de tunel definit de utilizator; culeea ABUTMENT, iar în IFC 4.3 așezată într-un IfcBridgePart ABUTMENT al podului proiectului, IfcBridge) peste câte un IfcWall RETAININGWALL pentru fiecare aripă și pentru peretele frontal, așa că fiecare parte se cotează, se cofrează și se armează separat. O aripă se întâlnește cu peretele frontal într-un rost perpendicular pe ea, imediat după locul unde începe panta. *perete* îl scrie ca un singur IfcWall RETAININGWALL. Fiecare parte are cantitățile ei (beton, cofraj pe fața din față, pe spate, la capete și la talpă, coronamentul); unde se întâlnesc două părți nu e cofraj, iar zidul adaugă **Beton, peretele frontal**, **Beton, aripi**, **Cofraj, peretele frontal** și **Cofraj, aripi** pentru extrase.

![O culee de pod cu aripi răsucite](shots/abutment-3d.webp)

![O secțiune printr-o aripă răsucită: fața înclinată pe talpă](shots/portal-section.webp)

## Căptușeala tunelului

![Configuratorul pe presetarea în potcoavă, previzualizarea motorului lângă valori](shots/tunnel-configurator.webp)

**Căptușeală de tunel…** (<kbd>TU</kbd>, în grupul **Peisagistică**) duce secțiunea transversală a unui tunel de-a lungul unei axe. Alege o presetare: **Potcoavă (NATM)** (un tunel minat: arcele calotei, ale pereților, ale colțurilor și ale radierului, toate tangente, cu o căptușeală primară de torcret), **Circulară (inel TBM)** (inelul de o singură grosime al unui tunel forat), **Cadru casetat (tranșee acoperită)** (cu vute sub placa superioară) sau **Boltă pe pereți** (o boltă pe pereți verticali, peste un radier plat). Apoi dă-i valorile pe grupuri: secțiunea (lățimea liberă, înălțimea liberă peste drum, razele, radierul sub drum), căptușeala (grosimea la calotă, la pereți și la radier, și căptușeala primară; 0 o lasă deoparte), blocurile (lungimea blocului de betonare sau lățimea inelului) și ieșirile. Orice valoare poate fi o formulă, ca în configuratorul portalului: grosimea pereților și a radierului sunt implicit `{= thickness}`. La **Secțiune**, un profil desenat și salvat ([Profile](profiles.md)) poate fi secțiunea: exteriorul lui și golul liber din el, podeaua golului sub drum cu **Radier sub drum**. Previzualizarea este a motorului (plan, secțiune, 3D), făcută de același cod ca și căptușeala.

**Așezarea.** **Pe o axă…** cere un drum, o alee, un zid de sprijin, o cale ferată sau o linie desenată: clic unde începe căptușeala, apoi unde se termină. Căptușeala preia axa dintre cele două stații, cu curbele și clotoidele ei, și linia ei roșie (una proiectată așa cum este; una care urmează terenul, ajustată prin puncte), și îi păstrează stațiile. **Desenează axa…** ia în schimb o polilinie desenată; colțurile ei se rotunjesc în configuratorul de coridor (**Configurator…** în proprietăți: raze și clotoide la fiecare vârf, PVI-urile liniei roșii, stațiile, secțiunile la stații), ca la un drum. Linia roșie este cota drumului pe axă; secțiunea rămâne verticală și perpendiculară pe axă în plan (fără supraînălțare: panta transversală a drumului din tunel este a lui).

**Legată de axa ei.** O căptușeală pusă cu **Pe o axă…** urmează acel element: muți punctele drumului, îi racordezi colțurile sau îi schimbi linia roșie, iar căptușeala se reface de-a lungul lui în același pas de anulare, fiecare bloc și inel cu GlobalId-ul lui. Proprietățile o spun pe rândul **Axă** (**îl urmează**). Când drumul devine mai scurt decât stațiile căptușelii, căptușeala se oprește la capătul lui și o notă sub rând o spune; ajunge din nou la stațiile ei când drumul crește la loc. **Dezleagă** păstrează axa așa cum e acum, o copie care nu mai urmează: drumul se poate schimba fără ea. **Leagă din nou** urmează același element între aceleași stații; **Leagă de o axă…** alege altul (clic unde începe căptușeala, apoi unde se termină). O căptușeală al cărei drum e șters rămâne unde era, dezlegată, cu o notă; la fel una căreia îi editezi axa ei (linia roșie în configuratorul de coridor, punctele trase). Schimbarea secțiunii, a blocurilor sau a capetelor păstrează legătura. Căptușelile făcute înainte să poată urma rămân copiile care erau.

![Drumul îndoit din nou, căptușeala urmându-l între aceleași stații](shots/tunnel-linked-plan.webp)

![Proprietățile căptușelii: axa ei urmează drumul, cu Dezleagă și Leagă de o axă…](shots/tunnel-linked-properties.webp)

![O căptușeală în potcoavă pe o curbă, în blocurile ei de betonare, cu un portal la intrare](shots/tunnel-3d.webp)

**Blocuri și inele.** Căptușeala se împarte de la început în blocuri de betonare (implicit 12,5 m) sau, pentru TBM, în inele; ultimul este ce rămâne. Fiecare este o parte a ei, cu GlobalId-ul ei, păstrat când schimbi valorile căptușelii (blocurile în plus vin după). În plan, exteriorul căptușelii și golul liber se desenează ascunse, axa ca linie de ax, capetele și rosturile de-a curmezișul, stațiile de-a lungul ei.

**Portaluri.** În proprietățile căptușelii, **Portal** **La început** sau **La capăt** așază un portal (presetarea cu aripi evazate a configuratorului de portal) de-a curmezișul axei la acel capăt: peretele frontal mai lat decât căptușeala, coronamentul cu un metru peste ea, aripile ieșind din tunel. Căptușeala trece prin peretele frontal: zidul este tăiat de exteriorul căptușelii în spatele capătului și de golul liber în fața lui, și o urmează când se schimbă. **Oblic la capăt cu** și **Oblic la început cu** rotesc planul unui capăt în jurul verticalei (portalul făcut atunci se rotește cu el); un capăt oblic cere axa dreaptă, cu pantă constantă, la acel capăt.

![O secțiune transversală prin căptușeală la o stație: căptușeala și căptușeala primară de torcret](shots/tunnel-section.webp)

**Secțiuni.** O secțiune de-a curmezișul căptușelii (din configuratorul de coridor sau desenată) arată straturile cu materialele lor; o secțiune care urmează axa căptușelii este profilul ei longitudinal, cu calota și radierul de-a lungul ei.

![Profilul longitudinal de-a lungul axei căptușelii](shots/tunnel-long-section.webp)

**Geometria exactă, IFC și cantitățile.** Pe un aliniament cu pantă constantă, fiecare față este un plan, un cilindru sau o suprafață riglată; peste o racordare verticală, o suprafață B-spline de translație (parabola exact); pe un arc de cerc în palier, o suprafață de revoluție (tori, conuri). O clotoidă sau un arc în pantă se acoperă prin secțiuni destul de dese încât să rămână în regula corzilor (1 mm). Cu **Suprafețe exacte** bifat ([Import și export](import-export.md)), exportul IFC scrie fiecare bloc ca IfcAdvancedBrep pe aceste suprafețe, iar exportul STEP o face mereu. **IFC** *ansamblu* (implicit) scrie căptușeala ca IfcElementAssembly (definit de utilizator, „Tunnel lining”) peste câte un IfcBuildingElementProxy pe bloc sau inel („Lining block”, „Lining ring”) și pe fiecare bucată a căptușelii primare; IFC 4.3 nu are un element pentru căptușeala de tunel, iar un perete sau o placă ar spune despre boltă și radier ce nu sunt. *corp* scrie un singur proxy. Fiecare bloc are cantitățile lui: betonul, cofrajul feței interioare, fața exterioară, cofrajul capetelor și **Cofraj** (cu fața exterioară când **Fața exterioară cofrată** este bifat, implicit la cadrul casetat și la boltă); căptușeala adaugă aria secțiunii, golul liber și volumul căptușelii primare.

## Terenul: limită, pante și sistematizare

![Un teren ridicat topografic, decupat după o limită în L, o etichetă de pantă, o subregiune de pietriș și grila săpătură/umplutură peste o tușă ridicată](shots/terrain-tools-plan.webp)

**Limita.** Un teren dintr-o ridicare topografică acoperă conturul exterior al punctelor (înfășurătoarea lor convexă). **Limita terenului** (în grupul **Teren**) desenează conturul pe care îl are terenul cu adevărat, un poligon ale cărui laturi pot fi arce (<kbd>Space</kbd>), un dreptunghi sau un cerc, ca o platformă: terenul din afara lui dispare, cu colțuri concave cu tot, iar curbele de nivel, solidul, secțiunile și cantitățile îl urmează. Colțurile lui au mânere. În proprietățile terenului, **Limită** spune **decupat** sau **înfășurătoare convexă**; **din parcelă** ia conturul parcelei drept limită, iar **scoate** revine la înfășurătoare.

**Un teren pe contur.** **Teren pe contur** (<kbd>TO</kbd>, în grupul **Teren**) face terenul direct după conturul pe care îl are, fără un dreptunghi de decupat după aceea: un poligon ale cărui laturi pot fi arce (<kbd>Space</kbd>), un dreptunghi, un cerc sau, din aceeași bară, interiorul pereților, o formă desenată închisă ori o cameră. Ultima alegere din bară spune la ce cotă: **La o cotă** întreabă cota terenului (±0.00 = 0), **Pe teren** așază fiecare vârf pe terenul de dedesubt. **Teren pe conturul ales** ia conturul a ce alegi cu un clic: o cameră, o parcelă sau o zonă dintr-un fișier GIS (cu cotele aduse de el), parcela unui teren, o platformă (la cota ei) sau o formă desenată închisă (prin **Convertește în… → Teren în interior**, care le convertește pe mai multe deodată). Fiecare vârf al conturului, și punctele de-a lungul unui arc, e un punct al terenului, iar terenul se termină exact pe contur; interiorul primește puncte pe o grilă, ca pensula și mânerele să aibă pe ce lucra. Colțurile conturului (cele patru puncte de sfert ale unui cerc) au mânere; punctele de pe un arc nu au și își urmează colțurile: schimbă cota unui colț, iar arcul dintre el și colțul următor merge uniform de la unul la altul, în același pas de anulare. Un punct al arcului căruia i-ai dat o cotă proprie (cu **toate punctele conturului** bifat) și-o păstrează, iar arcul trece prin el. Mânerele colțurilor în plan mută conturul, iar punctele lui îl urmează: un colț tras își păstrează cota, un colț adăugat ia cota terenului de acolo.

![Un teren desenat pe un contur cu un arc, cu mânere pe colțuri, în 3D](shots/terrain-outline-3d.webp)

Un teren decupat cu **Limita terenului** își păstrează cotele conturului așa cum le dă terenul; **conturul ca puncte**, lângă **Limită**, face din fiecare vârf al conturului un punct al lui, la cota terenului de acolo, ca să aibă mâner și o cotă de scris. Terenul nu se mișcă (doar pe un teren lucrat cu pensula, ale cărui tușe lucrează acum și pe aceste puncte, se poate mișca puțin de-a lungul conturului, și un mesaj o spune). Proiectele vechi rămân cum sunt până ceri.

**Împărțire și unire.** **Împarte terenul** taie un teren în două după o linie de două puncte trasă peste el. Jumătatea mai mare rămâne terenul (își păstrează identitatea, GlobalId-ul din IFC); cealaltă devine un teren nou alături. Platformele, rigolele și aleile merg cu jumătatea în care se află (după mijlocul conturului, o alee după mijlocul ei), la fel cotele punctuale, etichetele de pantă și subregiunile, așa că nimic de pe teren nu se pierde. **Unește terenurile** adună terenurile selectate în primul selectat: punctele, liniile de frângere, tușele de pensulă, cotele punctuale, etichetele, subregiunile, platformele, rigolele și aleile lor trec toate, iar cotele rămân unde sunt chiar dacă terenurile erau citite față de repere diferite. Fiecare jumătate a unei împărțiri păstrează limita de partea ei, iar unirea îmbină limitele terenurilor într-una singură (un teren fără limită contează cu conturul punctelor lui): două jumătăți unite din nou au conturul din care au fost tăiate, terenuri care se suprapun au conturul din jurul amândurora. Când limitele lor nu formează un singur contur (sunt depărtate sau lasă un gol între ele), terenul unit acoperă toate punctele lui și un mesaj o spune; desenează-i din nou limita ca să-l decupezi. Un teren are o singură parcelă: terenul unit o ia pe cea care există, iar când mai multe au câte una o păstrează pe a primului și un mesaj o spune. Fiecare este un pas de anulare.

**Etichete de pantă și cotele curbelor de nivel.** **Etichetă de pantă** scrie panta dintre două puncte pe care dai clic pe teren, în procente și ca 1 : N (5.0 %, 1:20.0), cu o săgeată spre vale. Fiecare curbă de nivel principală își poartă cota la mijloc, de-a lungul liniei, pe un fond curat, ca linia să se oprească sub text. În proprietățile terenului, **Etichete de pantă** le numără, iar **șterge toate** le scoate pe toate.

**Săpătură și umplutură.** Pensula (**Ridică**, **Coboară**, **Nivelează**, **Netezește**) modelează terenul, iar săpătura și umplutura ei se măsoară față de terenul ridicat topografic: cantitățile de săpătură și umplutură ale terenului (*CutVolume*, *FillVolume*) numără sistematizarea făcută cu pensula împreună cu platformele, rigolele și aleile, iar *SculptCut* și *SculptFill* dau doar partea pensulei. **Cantitățile** terenului din proprietăți arată **Săpătura** și **Umplutura** lui lângă **Săpătură netă − umplutură**, apoi săpătura și umplutura fiecărei platforme sub numele ei (*Lapin_EarthworkQuantities*), cum le are fișierul IFC. **Grilă săpătură/umplutură** din proprietăți (2, 5 sau 10 m; **oprită** implicit) scrie diferența în fiecare punct al grilei în plan, albastru unde terenul a fost ridicat (+0.45), roșu unde a fost săpat (−0.30), gri unde e ca înainte. O grilă prea deasă pentru teren (peste 2500 de valori) se scrie la un pas mai mare, un multiplu întreg al celui cerut, iar curbele de nivel care ar fi peste 2000 de linii pe înălțimea terenului se desenează la o echidistanță mai mare; în ambele cazuri terenul o spune. Hărțile de analiză, cotele punctuale, etichetele de pantă și grila săpătură/umplutură citesc terenul proiectat, cum îl are terenul 3D: o platformă plană la cota ei, cu taluzurile în jur, canalul unei rigole cu fundul pe profilul ei proiectat. **Zonă sistematizată → fixează terenul existent** ia terenul așa cum este acum drept teren existent, ca următoarele tușe să se măsoare de acolo; **revino la ridicare** se întoarce la ridicarea topografică.

![Proprietățile terenului: punctele, limita, etichetele de pantă, subregiunile, grila săpătură/umplutură și zona sistematizată](shots/terrain-properties.webp)

**Editarea punctelor în 3D.** **editează în 3D**, lângă **Puncte** ale terenului, pune un mâner pe fiecare punct ridicat în vederea 3D: trage-l în sus sau în jos, ori scrie distanța, și punctul se mută cu atât, câte un pas de anulare. Un punct tăiat de limită nu primește mâner. O ridicare mare (o suprafață LandXML cu mii de puncte) arată mânere pe cele 400 de puncte cele mai apropiate de mijlocul vederii 3D; mută vederea ca să ajungi la celelalte. **gata cu editarea 3D** scoate mânerele.

**O cotă scrisă.** În timpul editării, un clic pe mânerul unui punct (fără să-l tragi) îl alege, desenat plin; mai dă clic ca să alegi mai multe, încă o dată ca să renunți la unul. Sub **Puncte**, proprietățile terenului arată câte sunt **alese** și un câmp pentru cota lor (absolută, ±0.00 = 0; cea de acum când o au la fel): scrie-o, iar **setează cota** (sau <kbd>Enter</kbd>) pune toate punctele alese la acea cotă, un pas de anulare. **alege-le pe ale conturului** alege deodată colțurile conturului, ca să setezi un perimetru întreg (toate punctele lui cu **toate punctele conturului** bifat, care pune mânere și pe fiecare punct de pe arce); **renunță** nu mai alege niciunul. În plan aceleași puncte au și ele mânere, în timpul editării: un clic dreapta pe unul oferă **Setează cota…** (pentru punctele alese odată cu el, dacă e unul dintre ele) și **Alege punctul**; un colț al conturului care stă pe un punct le oferă lângă comenzile lui de colț.

![Proprietățile terenului cu două puncte alese și cota lor scrisă](shots/terrain-picked-properties.webp)

![Cele două colțuri ale arcului la noua lor cotă, arcul urmându-le](shots/terrain-outline-raised.webp)

**O rețea de control.** Pe o ridicare deasă, mutarea punctelor unul câte unul e lentă. Câteva puncte pot deveni **puncte de control** care le modelează pe celelalte, ca o rețea low-poly: la **Rețea de control**, **la fiecare** *10* **m** ia punctul cel mai apropiat de fiecare nod al unei grile de 10 m (și colțurile), **pe contur** ia punctele de pe conturul terenului, iar **șterge toate** le scoate pe toate. În timpul editării în 3D, **Mânere pe** alege ce primește mâner: **toate punctele** (ca înainte), **doar punctele de control**, sau **alege punctele de control**, unde se văd toate punctele, cele de control pline; un clic pe mânerul unui punct îl face punct de control, sau din nou punct liber. Cu **doar punctele de control**, trage un punct de control în sus sau în jos (ori scrie distanța): ajunge exact acolo, iar punctele libere din jur îl urmează lin, cu o parte din mutarea lui care scade cu distanța și se stinge la celelalte puncte de control, care rămân pe loc. Punctele libere își păstrează forma (denivelări, pante) și se mută cu ea, iar readucerea punctului de control pune toate punctele înapoi; toată mutarea e un singur pas de anulare. Un punct mutat singur (cu **toate punctele**) își păstrează mutarea. Un teren fără puncte de control merge ca înainte. Împărțirea unui teren lasă fiecărei jumătăți punctele de control din ea; unirea le adună.

**Subregiuni.** **Subregiune** (în grupul **Terasamente**) desenează o zonă a terenului cu materialul ei (pietriș, implicit): o curte, un gazon, un pat de pietriș. Terenul își păstrează cotele; zona își poartă materialul în 3D, ca o pieliță subțire care urmează chiar triunghiurile terenului, cu liniile de frângere cu tot, și e conturată în plan. Fiecare își dă aria în plan listelor (*Lapin_SubregionQuantities*). **Subregiuni** din proprietățile terenului le numără și le listează, **șterge toate** le scoate, iar **în pavaj** lângă una o face pavaj (mai jos).

![O subregiune de pietriș pe teren, în 3D](shots/terrain-subregion-3d.webp)

## Linii caracteristice și curbe de nivel

O **linie caracteristică** este o polilinie în spațiu care ține de un teren: o bordură, un șanț, o creastă, o rigolă, piciorul unui taluz. Are colțuri (drepte sau arce), iar fiecare colț o cotă. Terenul e construit ca să treacă **exact** prin ea: suprafața e triangulată de-a lungul liniei, așa că o săpătură, un val sau o muchie apar exact unde le-ai desenat, iar punctele topografice aflate la mai puțin de **toleranța** ei de linie (5 cm, implicit) sunt lăsate în afara suprafeței, ca niciun punct ridicat să n-o strice. Liniile caracteristice sunt în grupul **Sistematizare** al filei **Teren** (*Site*).

**Desenează o linie caracteristică.** **Linie caracteristică** (<kbd>FL</kbd>) primește clicuri de-a lungul liniei, ca uneltele de contur: **Arce** (sau <kbd>Space</kbd>) face următoarele laturi arce care continuă tangent latura dinainte, **Linie închisă** o închide înapoi la primul colț, <kbd>Enter</kbd> (sau un al doilea clic pe ultimul colț) o termină. Bara alege cum își iau colțurile cotele:

- **La cote**: după ultimul clic o întrebare cere cotele colțurilor în ordine, separate prin `;` (`0.30; ; ; 1.80`), în unitatea de cotă a proiectului cu ±0.00 = 0. Un colț lăsat gol ia cota terenului de acolo.
- **Pe teren**: fiecare colț e așezat pe terenul de sub el, iar linia îl urmează în continuare.

Linia merge pe terenul de sub colțurile ei. Un colț fără cotă proprie și fără teren sub el e refuzat, cu un mesaj care îl numește: nimic nu se pune la zero pe tăcute.

![O linie caracteristică desenată cu un arc, cotele colțurilor scrise, terenul trecând prin ea](shots/feature-line-3d.webp)

În plan, o linie caracteristică are stilul ei de linie (linie-punct-punct, în culoarea ruginie), ca să se citească pe lângă curbele de nivel ale terenului, care se îndoaie în jurul ei.

![Planul cu o linie caracteristică și curbele de nivel îndoite în jurul ei](shots/feature-line-plan.webp)

**Editeaz-o în plan.** Selecteaz-o ca să-i vezi mânerele: trage un colț ca să-l muți (<kbd>Ctrl</kbd>/<kbd>⌘</kbd>-clic îl scoate); trage rombul din mijlocul unei laturi, sau dă <kbd>Ctrl</kbd>/<kbd>⌘</kbd>-clic pe el, ca să pui un colț acolo (linia își păstrează forma: colțul nou ia cota liniei din acel loc); trage mânerul rotund din mijlocul unui arc ca să-l îndoi. Un clic dreapta oferă **Fă arc** și **Fă dreaptă** pe o latură, iar **Setează cota…** și **Pe teren** pe un colț. Fiecare modificare e un pas de anulare.

**Editeaz-o în 3D.** Fiecare colț are un mâner de cotă: trage-l în sus sau în jos (ori scrie distanța), sau dă clic pe el fără să-l tragi și scrie cota colțului (absolută, ±0.00 = 0).

**Cote și pante.** Proprietățile unei linii caracteristice înșiră colțurile cu cotele lor. O cotă pe care colțul o are de la sine e arătată simplu; una luată de la teren sau dintr-o pantă e gri. Scrie o cotă într-un câmp ca să-i dai colțului una proprie; **pe teren** pune colțul înapoi pe teren. Între două colțuri se arată **panta**, care primește o pantă scrisă: `2 %`, `1:50` sau `-1,5 %`; celălalt capăt al laturii o urmează. **O pantă scrisă păstrează** spune ce capăt rămâne pe loc: **începutul ei** (sfârșitul o urmează) sau **sfârșitul ei**. O pantă nu se poate scrie pe o curbă de nivel (toate colțurile ei sunt la aceeași cotă) și nici pe latura de închidere a unei linii închise. Proprietățile mai cuprind **Toleranță**, **Pe teren**, **Linie închisă** și **Cota curbei de nivel**.

![Proprietățile unei linii caracteristice: terenul ei, toleranța și colțurile cu cotele și pantele lor](shots/feature-line-properties.webp)

O linie caracteristică se mută, se rotește și se oglindește ca orice element (o mutare în spațiu ridică și cotele proprii); *Lapin_FeatureLineQuantities* dă listelor lungimea ei în plan, lungimea pe pantă și cota cea mai joasă și cea mai înaltă. Dacă două linii caracteristice ale unui teren se încrucișează la cote diferite, acolo ține cea desenată prima, iar cealaltă se frânge până la ea: planul marchează fiecare astfel de încrucișare cu o cruce roșie și ambele cote (cea care ține / a celeilalte), proprietățile ambelor linii o arată la **Încrucișări**, iar diagnosticul geometriei terenului o numește, cum numără și punctele topografice lăsate deoparte. **Împărțirea unui teren** trimite fiecare linie caracteristică la jumătatea în care stă; o linie peste tăietură e tăiată acolo, fiecare parte mergând la jumătatea ei cu cotele colțurilor (tăietura la cota liniei de acolo; arcele ei așa cum sunt așezate, în corzi). **Unirea terenurilor** dă terenului unit toate liniile lor.

**O curbă de nivel desenată la o cotă.** **Curbă de nivel la cotă** (<kbd>CV</kbd>) desenează o linie caracteristică toată la aceeași cotă: scrie cota în bara ei (gol: e cerută când termini), dă clic pe puncte, <kbd>Enter</kbd>. Terenul e triangulat de-a lungul ei, așa că curba trece exact pe unde ai desenat-o, iar terenul dintre această curbă și vecinele ei e interpolat din nou. Se numește *Contour 1.50*; aceleași opțiuni, **Arce** și **Linie închisă**, sunt în bară. Scrierea unei cote în proprietățile oricărei linii caracteristice o face curbă de nivel; golirea ei dă colțurilor înapoi cotele lor proprii.

![Curba de nivel 1.50 desenată din nou cu un metru spre vest, în partea de nord](shots/feature-line-contour.webp)

**Tragerea unei curbe de nivel.** **Trage curba de nivel** (<kbd>DG</kbd>) mută cu mouse-ul o curbă de nivel a terenului: când cursorul se apropie de una (la 2 m) ea se luminează; apasă pe ea, trage și dă drumul. Partea curbei aflată în **Raza de acțiune** a punctului apăsat devine o linie caracteristică de nivel, mutată cu tragerea; mutarea scade cu distanța de la punctul apucat (toată la el, jumătate la jumătatea razei, nimic la rază; o curbă desenată de tine primește colțuri de-a lungul ei, ca să se îndoaie lin), iar punctele topografice din rază o urmează, așa că curbele vecine se îndoaie lin și nimic dincolo de rază nu se mișcă. Scrie raza în bară cu unitatea ei (`9 m`); numerele simple sunt în unitatea de lungime a proiectului; gol, e cea mai mare dintre 2 m și de trei ori tragerea. Trebuie să fie cel puțin dublul tragerii: o rază mai scurtă ar împături terenul peste el, iar tragerea e refuzată. Cât timp butonul e apăsat, curba trasă se previzualizează; <kbd>Esc</kbd> renunță la tragere. O curbă desenată de tine cu **Curbă de nivel la cotă** se mută așa cum e, ca o singură linie. Un pas de anulare.

![O curbă de nivel luminată sub cursor, înainte de tragere](shots/feature-line-drag-before.webp)

![Curba de nivel trasă: curbele vecine o urmează, scăzând cu distanța](shots/feature-line-drag-after.webp)

Două lucruri decurg din felul în care un teren își ține punctele. **Înlocuirea punctelor topografice ale unui teren pe de-a-ntregul** (**Înlocuiește punctele…**, sau *Change terrain* cu puncte noi) renunță la regula „conturul e punctele lui” și la cotele proprii pe care le aveau vârfurile arcelor lui: punctele noi sunt punctele terenului așa cum vin. Iar **un vârf de arc mutat în plan prin API** (nu prin mânerele conturului) devine un punct obișnuit al terenului, cu o cotă proprie. Liniile caracteristice nu sunt atinse de niciuna: rămân pe teren și continuă să-l modeleze.

## Pavaje (hardscape)

Un **pavaj** (hardscape) e o suprafață pavată cu alcătuirea ei: o piață, un trotuar, o terasă, o parcare, o platformă de pietriș. Se desenează cu **Pavaj** (<kbd>HS</kbd>, în grupul **Peisagistică** din tabul **Teren**) pe un contur, ca o platformă: clicuri (**Arce** sau <kbd>Space</kbd> pentru laturi curbe), un dreptunghi sau un cerc. Bara alege **tipul**, dacă e **Tăiat în teren** sau **Independent** și dacă primește o **Bordură** pe tot conturul. Pornește orizontal, la cota terenului din mijlocul lui (la 0 fără teren dedesubt); totul se schimbă apoi din proprietăți, fără să-l desenezi din nou.

![O piață cu bordură, tăiată într-o ridicare în pantă, în 3D](shots/hardscape-plaza-3d.webp)

**Tipul.** Un tip de pavaj e o alcătuire de straturi de sus în jos, fiecare cu materialul și grosimea lui, ca la un tip de planșeu. Lapin vine cu câteva gata făcute, marcate orientative: **Concrete pavers** (pavele de beton 6 cm, pat de nisip 4 cm, balast 20 cm), **Asphalt** (asfalt), **Gravel** (pietriș), **Grass pavers** (dale înierbate) și **Stone slabs** (dale de piatră); verifică-le față de alcătuirea ta. **Editează tipurile…** din proprietăți deschide **Tipuri de pavaj**: schimbi straturile unui tip, ordinea și materialele lor, felul pavajului (**Rigid** sau **Flexibil**, cum îi spune IFC 4.3), faci unul nou sau aduci unul gata făcut. Toate pavajele tipului urmează când îl salvezi, iar un tip editat e al tău, nu mai e marcat orientativ.

![Dialogul Tipuri de pavaj cu straturile pavelelor](shots/hardscape-types.webp)

**Fața de sus.** **Fața de sus** din proprietăți e una din trei:

- **Orizontală**: toată fața la **Cotă** (±0,00 = 0).
- **Plan înclinat**: un plan care coboară cu o **Pantă** (`2 %`, `1:50`, `0.02`) spre o direcție (**Coboară spre**, în grade de la +x, în sens trigonometric), cota ținând în originea lui. **Săgeată de pantă** (în grupul **Peisagistică**) o dă desenând: clic pe capătul de sus, apoi pe cel de jos, peste pavaj, și scrii panta.
- **După colțuri**: o cotă pentru fiecare colț, scrisă în tabelul colțurilor sau dată cu mânerele de cotă ale colțurilor în 3D. Când colțurile stau într-un plan, fața e acel plan; când nu, fața se triangulează între ele, iar proprietățile spun asta.

O latură în arc ia cotele celor două colțuri ale ei după lungime. Planul își scrie cota în mijloc, sau o săgeată în sensul pantei cu panta ei, sau cotele colțurilor.

![Proprietățile unui pavaj: tipul, alcătuirea, fața de sus, terenul și bordura cu laturile ei](shots/hardscape-properties.webp)

![Un trotuar cu pantă de 2 % în secțiune: pavelele, patul, fundația, bordura, terenul lipit de marginea lui](shots/hardscape-sidewalk-section.webp)

**Pe teren sau independent.** Tăiat într-un teren, marginile de sus ale pavajului sunt liniile de rupere ale terenului, ca liniile caracteristice: terenul se lipește exact de marginile lui, punctele ridicării de sub el ies din teren (terenul spune câte), terenul e tăiat de straturile și bordura lui, iar săpătura i se măsoară în *Lapin_EarthworkQuantities*. Când îi schimbi fața de sus sau conturul, terenul urmează. Curbele de nivel se opresc la conturul lui. **Independent** din proprietăți îl scoate din teren (terenul e din nou ridicarea); **taie în teren** îl pune la loc. Un pavaj independent nu are nevoie de teren: un trotuar în jurul unei clădiri, o terasă pe o placă. O copie a unui pavaj tăiat într-un teren taie același teren.

**Bordura.** Bifează **Bordură** în proprietăți (sau în bara uneltei) pentru o bordură pe laturi, în afara conturului. Are o formă (**Verticală**, **Teșită**, **Rotunjită**, sau un profil al tău desenat în **Gestionare → Profile** cu folosința **Bordură**: latura dinspre pavaj stă pe marginea pavajului, vârful la înălțimea bordurii peste pavaj), înălțimea **Peste pavaj**, o **Lățime**, adâncimea **Sub pavaj** și un material. Tabelul **Laturile bordurii** o pornește sau o oprește latură cu latură și o coboară: scrie o înălțime în **Coborâtă la** (`0` la o trecere) și bordura coboară la acea înălțime peste pavaj pe latura aceea. Bordurile se îmbină în unghi unde două laturi au bordură și se opresc drept unde o latură nu are; pe un pavaj înclinat bordura urmează fața. Bordura e un element al ei (selecteaz-o ca să ajungi la pavajul ei); pleacă odată cu pavajul când acesta e șters.

![Piața în plan: pavajul hașurat, cota lui și bordura](shots/hardscape-plaza-plan.webp)

**O subregiune făcută pavaj.** În proprietățile terenului, **în pavaj** lângă o subregiune o face pavaj, cu același contur și aceeași arie: un tip din materialul subregiunii peste patul și fundația pavelelor, colțurile pe teren acolo unde stătea subregiunea, tăiat în teren. Subregiunea dispare, în același pas de anulare. Subregiunile pe care nu le transformi rămân ce erau.

![O subregiune de pietriș transformată în pavaj](shots/hardscape-converted.webp)

**O alee dintr-un tip de pavaj.** O alee sau un drum își păstrează straturile proprii de uzură și de bază, dacă nu bifezi **Folosește un tip de pavaj** în proprietățile lui: atunci carosabilul e alcătuit din straturile tipului, de sus în jos, în materialele lor (fiecare strat tăiat din teren, cu aria și volumul lui în *Lapin_PavementLayerQuantities*), iar felul pavajului în IFC e al tipului. Alege tipul alături, sau **Editează tipurile…**: un tip editat schimbă deodată toate aleile și pavajele lui. Bordurile, rigolele și trotuarele rămân ale aleii. Debifează și aleea e alcătuită din nou din straturile ei, exact ca înainte; o alee pe care n-ai bifat-o niciodată e cum a fost mereu.

![Proprietățile unei alei cu Folosește un tip de pavaj: alcătuirea pavelelor în locul straturilor ei](shots/path-hardscape-type.webp)

**Editarea, cantitățile și IFC.** În plan conturul are mânerele de contur (colțuri, laturi, arce, racordare și teșire); în 3D fața de sus are mânere de cotă. Se mută, se rotește și se oglindește ca orice element; fiecare schimbare e un pas de anulare. *Lapin_HardscapeQuantities* dă aria pavată, aria feței de sus, perimetrul, grosimea alcătuirii și volumul, iar fiecare strat aria și volumul lui (fără ce scoate din el o rigolă de drenaj sau o bordură independentă care trece prin el, cum arată straturile desenate); *Lapin_KerbQuantities* lungimea bordurii, lungimea coborâtă și volumul; **Extrasul de pavaje** le listează pe toate. În plan pavajul e hașurat cum arată materialul stratului de sus (grafica lui de suprafață, Gestionare → Materiale), iar secțiunile îi taie straturile. În IFC merge ca *IfcPavement* (IFC 4.3) sau ca *IfcSlab* numit *Pavement* (IFC4), cu straturile ca *IfcMaterialLayerSetUsage*, iar bordura ca *IfcKerb* (IFC 4.3) sau ca proxy numit *Kerb* (IFC4); un fișier scris de Lapin revine ca același pavaj și aceeași bordură.

## Profile

Un **profil** este cotele unei linii de-a lungul ei, desenate ca un grafic pe care îl poți edita: stația pe orizontală, cota pe verticală. Merge pe orice linie care are cote: o linie caracteristică, marginea unui pavaj, axul unei alei, al unui drum, al unui zid de sprijin sau al unui șanț, și o **linie de secțiune** trasată peste teren. Îl deschizi din **Profil…** în proprietățile liniei, sau din grupul **Sistematizare** al filei **Teren** (*Site*): **Profil pe o linie** (<kbd>LP</kbd>, apoi clic pe linie în plan) și **Linie de secțiune** (<kbd>SQ</kbd>, o polilinie trasată ca o linie caracteristică; profilul ei se deschide când termini). Profilul este un panou sub plan, deci planul rămâne la vedere.

![O linie de secțiune peste un deal, profilul ei sub plan, stația de sub cursor din grafic marcată în plan](shots/profile-plan-graph-linked.webp)

**Graficul.** **Terenul existent** (ridicarea, citită exact pe triunghiurile terenului) este gri întrerupt, **terenul proiectat** (cu liniile caracteristice și pavajele) verde subțire, iar **profilul** liniei roșu gros, cu punctele lui: un punct plin are o cotă proprie (este proiectat), unul gol stă pe teren, un lăcățel mic marchează un punct blocat, iar un arc sub punct marchează o racordare verticală. **Cote** exagerează scara verticală: 1× este adevărat, 2×, 5× și 10× fac pantele mai ușor de citit. Între punctele proiectate panta e scrisă în %.

**Planul și graficul sunt legate.** Plimbă cursorul pe grafic și planul marchează acea stație pe linie; plimbă-l pe linie în plan și graficul o marchează; un clic în oricare alege stația (o linie verticală plină în grafic, un punct în plan).

**O linie de secțiune peste teren.** O linie de secțiune doar citește terenul, până editezi unul din punctele ei: atunci terenul din ambele părți o urmează, pe **lățimea de influență** a ei, și scade la nimic la marginea ei (aceeași descreștere netedă ca la tragerea unei curbe de nivel). Scrie lățimea în panou; trebuie să fie cel puțin **dublul celei mai mari modificări**, altfel editarea e refuzată cu un mesaj care o spune, iar nimic nu se schimbă. O editare este un pas de anulare.

![Punctul de pe deal tras în jos: curbele de nivel din plan urmează linia de secțiune](shots/profile-section-line.webp)

**Editarea profilului.**

- **Trage un punct** în sus sau în jos; un punct de pe un tronson drept poate aluneca și de-a lungul liniei, între vecinii lui (capetele și punctele unde linia se îndoaie în plan rămân la stația lor). Linia roșie urmează mouse-ul cât tragi, iar o singură comandă pleacă la eliberare.
- **Dă clic pe un punct** ca să-i scrii **cota**, **racordarea verticală** (o curbă parabolică de lungimea aceea în punct; refuzată la capete sau când nu încape între vecini), ca să-l **blochezi** (un punct blocat își păstrează cota și stația, iar o editare care l-ar muta e refuzată) sau ca să-l **ștergi**. **La teren** îi ia din nou cota din teren.
- **Dublu clic pe linie**, sau **Punct** la stația aleasă, adaugă un punct.
- **Dă clic pe un tronson** ca să-i scrii **panta**: `2 %`, `1:50`, `-0,5 %` (virgula zecimală merge). **O pantă scrisă păstrează** începutul sau sfârșitul; celălalt capăt ia cota pe care o dă panta.

![O pantă de 2 % scrisă pe un tronson: punctul de la capătul lui o urmează](shots/profile-grade-typed.webp)

Profilul unui pavaj este muchia lui de sus: cotele colțurilor se pot schimba (fața de sus devine „pe colțuri”), dar nu se pot adăuga, șterge, racorda sau bloca, fiindcă forma o ține planul. O alee, un zid sau un șanț folosește linia roșie a coridorului: când profilul lui urmează terenul, **Proiectează profilul** îl face întâi unul proiectat.

**Secțiuni transversale.** Fila **Secțiuni transversale** arată terenul **peste** linie la anumite stații: scrie un **interval** (din 10 în 10 m) sau alege o stație și apasă **Secțiune aici**; **Lățime pe fiecare parte** spune cât ajunge fiecare parte. Fiecare secțiune este un grafic mic cu terenul existent, terenul proiectat și punctul liniei marcat, și e trasată peste linie în plan. Trage un punct al unei secțiuni sau trage curba terenului unde vrei un punct nou: prima editare la o stație face o scurtă linie de secțiune perpendiculară pe linie acolo, iar terenul de lângă ea o urmează pe lățimea ei de influență; editările următoare îi mută punctele ca la profilul longitudinal. Cotele secțiunilor folosesc aceeași exagerare.

![Secțiuni transversale din 10 în 10 m sub plan](shots/profile-cross-sections.webp)

**Pe o planșă.** **Pune pe o planșă…** face din **profilul longitudinal** (și din secțiunile transversale, când sunt afișate) un desen de sine stătător, la scara aleasă (orizontala 1:500, cotele exagerate ca în panou) și îl pune pe o planșă nouă sau pe una aleasă. Desenul are grila de cote cu planul de referință, ambele terenuri, punctele și racordările numite, iar dedesubt un tabel cu stațiile, ambele terenuri și săpătura sau umplutura; PDF-ul și DXF-ul planșei tipăresc ce arată planșa. Desenele sunt listate sub **Desene de profil** în browserul proiectului și rămân în pas cu linia: schimbi linia, se schimbă planșa.

![Profilul longitudinal pe o planșă](shots/profile-sheet.webp)

## Sistematizare între margini și taluzuri

Două unelte din grupul **Sistematizare** al tabului **Teren** modelează terenul din margini pe care le ai deja și le urmează mereu.

**Sistematizare între margini** (<kbd>GB</kbd>) face un petic verde între două piațete, terase sau linii la cote diferite: de exemplu o piațetă de jos la −2,00 și una de sus la −1,00, cu margini curbe, și un taluz înierbat care le leagă. Dă clic pe prima margine (un pavaj, o linie caracteristică, o platformă sau o alee, un zid ori un șanț), apoi pe a doua: peticul se face imediat. Dintr-un pavaj sau dintr-o platformă se ia doar porțiunea marginii care stă față în față cu cealaltă, deci peticul merge de-a lungul celor două margini și se închide cu legături drepte la capetele lor. Bifează **Mai multe margini** în bară ca să alegi trei sau mai multe, apoi apasă **Fă sistematizarea**; o margine aleasă din nou e lăsată.

![Două piațete la −2,00 și −1,00 cu margini curbe, legate de un taluz verde](shots/grading-plazas-3d.webp)

Suprafața lui e făcută numai din acele margini, deci cotele lor se păstrează exact: marginea peticului e marginea piațetei, la cota ei, fără niciun gol. Între două margini e o suprafață **riglată** (drepte de la fiecare punct al unei margini la punctul aflat la fel de departe de-a lungul celeilalte); din trei sau mai multe margini, sau cu **Triangulată (TIN)**, e triangulată din margini. Peticul **urmează** ce l-a făcut: ridici o piațetă, o muți, schimbi o linie caracteristică, iar peticul și terenul se schimbă odată cu ea, în același pas de anulare.

![Proprietățile sistematizării: ce este, suprafața, materialul și marginile](shots/grading-properties.webp)

**Cote scrise.** O margine care urmează o piațetă are cotele piațetei. Ca marginea peticului să aibă cote proprii, apasă **desprinde** lângă ea în proprietăți: punctele ei devin ale sistematizării, așa cum sunt acum, iar cota fiecăruia se scrie în tabel (gol: a terenului). O margine poate fi și o linie proprie de la început (prin API: `civil.addGrading` cu `{points}`), cu cotele scrise, restul de pe teren.

**Taluz la teren** (<kbd>SG</kbd>) duce un taluz de la o margine până întâlnește terenul: de la o terasă sau o platformă, spre exterior de jur împrejur; de la o linie deschisă (o linie caracteristică, o alee), pe partea pe care dai clic. Scrie în bară pantele de **Săpătură** și **Umplutură** ca `1:2`, `2` sau `50 %` (1:2 înseamnă 2 m pe orizontală pentru 1 m pe verticală): unde terenul e deasupra marginii, taluzul urcă la panta de săpătură; unde e dedesubt, coboară la panta de umplutură. Unde întâlnește terenul e **linia de intersecție cu terenul** (daylight line), calculată exact pe triunghiurile terenului. La un colț exterior taluzul îl ocolește ca un con, la un colț interior cele două taluzuri se întâlnesc pe bisectoarea colțului. Unde nu ajunge la teren în **Cel mult** (20 m dacă nu schimbi) sau iese de pe teren, se oprește acolo și spune. În proprietăți, un taluz poate merge în schimb **Până la** o lățime (întâlnește terenul la acea distanță, cu panta care rezultă) sau o cotă.

![O terasă cu taluzurile ei până la teren, în plan: linia de intersecție punctată, hașurile de taluz de la creasta fiecăruia](shots/grading-plan.webp)

![Taluzurile terasei în 3D](shots/grading-slope-3d.webp)

**Pe teren.** O sistematizare face parte din terenul ei: marginile, generatoarele și linia de intersecție sunt linii de rupere ale terenului, iar punctele ridicării din interior sunt lăsate afară (terenul spune câte), deci terenul acolo e suprafața sistematizării; curbele de nivel continuă peste ea. Secțiunile taie terenul cu ea; analiza săpătură/umplutură și grila terenului o includ. Fără teren, un petic între margini e o suprafață proprie (un taluz are nevoie de un teren de întâlnit).

![O secțiune prin cele două piațete și taluzul verde dintre ele](shots/grading-section.webp)

**Plan, cantități și IFC.** În plan o sistematizare e hașurată cu grafica de suprafață a materialului ei (**Grass**, iarbă, dacă nu alegi altul), iar un taluz are linia de intersecție punctată și **hașurile de taluz**: linii peste taluz de la creastă în jos, lungi și scurte alternativ, ca planul să arate încotro coboară. *Lapin_GradingQuantities* dă aria în plan și aria suprafeței, punctul cel mai jos și cel mai înalt și, la un taluz, lungimea liniei de intersecție și lățimea cea mai mare; *Lapin_EarthworkQuantities* săpătura și umplutura față de terenul existent. În IFC 4.3 merge ca *IfcEarthworksFill* (un taluz *SLOPEFILL*) sau, când e mai mult săpătură, *IfcEarthworksCut*; în IFC4 ca proxy. Geometria terenului o include, iar un fișier scris de Lapin revine ca aceeași sistematizare. Fiecare schimbare e un pas de anulare; o sistematizare ștearsă lasă terenul cum era.

## Rigole

O **rigolă de drenaj** merge de-a lungul unei linii, preia apa de ploaie de pe o platformă sau de pe un drum și o duce la o gură de scurgere. Grupul **Drenaj** din tabul **Teren** are două instrumente pentru ea: **Rigolă de drenaj** (<kbd>RG</kbd>) desenează una pe o polilinie (clicuri; **Arce** sau <kbd>Space</kbd> pentru porțiuni curbe; Enter încheie), iar **Rigolă de drenaj pe o linie** (<kbd>RJ</kbd>) ia o linie pe care o alegi: dă clic lângă marginea unui pavaj pe care să meargă, pe o alee (partea pe care ai dat clic e marginea părții carosabile), pe o linie caracteristică, pe conturul unei platforme, pe o bordură sau pe altă rigolă. O rigolă făcută așa își urmează linia: schimbi linia și rigola se schimbă odată cu ea. Bara alege **secțiunea**, cum merge fundul ei și lățimea și adâncimea.

![O piață cu o rigolă cu grătar peste ea, la 0,5 %, și o gură de scurgere la capătul de jos, în 3D](shots/drainage-plaza-3d.webp)

**Secțiunea.** O **rigolă în U** și una **în V** sunt deschise; o **rigolă cu fantă** e un corp cu o fantă îngustă spre suprafață; o **rigolă cu grătar** e un corp în U cu un grătar de fontă la nivelul suprafeței; un **șanț cu bolovani** e o albie puțin adâncă pavată cu bolovani. O secțiune desenată în **Gestionare → Profile** cu folosirea **Rigolă de drenaj** apare sub **Profile**: conturul ei se pune peste rigolă, la stânga și la dreapta axei, de la fund în sus. Fiecare secțiune are dimensiunile ei în proprietăți (lățime, adâncime, perete, talpă și, la o rigolă de scurgere, grosimea grătarului, lățimea fantei și gâtul ei) și materialul ei.

![Proprietățile unei rigole: secțiunea, radierul, partea de sus, gurile de scurgere și cifrele](shots/drainage-channel-properties.webp)

**Fundul.** **În pantă** dă radierului (fundului) o cădere constantă de la început: scrie `0,5 %`, `1:200` sau `0,005`, pozitivă pentru coborâre de la început, negativă pentru urcare; **Cota de început** e cota fundului la început, sau goală ca să fie partea de sus a rigolei acolo minus adâncimea. **La o adâncime sub partea de sus** ține fundul la aceeași adâncime sub partea de sus pe toată lungimea, deci urmează terenul. O rigolă al cărei fund ar ajunge la partea de sus spune unde și e adâncită la 2 cm acolo; coboară începutul sau panta. **Partea de sus** spune cu ce e la nivel: terenul sau partea de sus a unui pavaj prin care trece (găsit unde se află, așa că pavajul platformei e tăiat pentru ea) sau cotele liniei pe care o urmează.

**Guri de scurgere.** O rigolă cu cădere are o **gură de scurgere** (un sorb) în punctul cel mai de jos. În proprietăți, **Guri de scurgere** le listează după distanța de-a lungul rigolei: scrie alta, **Adaugă o gură de scurgere** pune una la capăt, **scoate** înlătură una (o rigolă poate rămâne fără: atunci se spune că nu are gură de scurgere în punctele joase), **În punctul cel mai jos** revine la cea cu care a fost făcută. Unde radierul are un punct jos al lui (o rigolă care urcă și coboară), proprietățile și planul îl marchează.

![Secțiunea longitudinală a rigolei în panoul Profil: partea de sus pe platformă, fundul coborând spre gura de scurgere](shots/drainage-channel-profile.webp)

**Profile…** în proprietăți deschide secțiunea longitudinală sub plan: partea de sus și radierul de-a lungul ei, doar de citit (panta și cota de început se schimbă în proprietăți). În plan rigola se desenează cu pereții ei, grătarul hașurat, gurile de scurgere ca un pătrat cu cruce, iar căderea și radierele sunt scrise lângă ea (*CR*, cota radierului, și *Punct jos* se scriu în limba desenelor din **Gestionare → Standarde de desen**: engleza, dacă nu alegi alta, caz în care apar ca *IL* și *Low point*).

![Gura de scurgere de la capătul de jos al rigolei](shots/drainage-gully-3d.webp)

**Pe teren.** O rigolă taie terenul de sub ea (excavația ei e măsurată), dacă nu alegi **independent** în proprietăți; **taie în teren** alege din nou un teren, iar o copie a ei taie același teren. Are mânere în plan (mută un colț, adaugă unul la mijlocul unei laturi, curbează o latură) și se mută, rotește și oglindește ca orice element, fiecare schimbare fiind un pas de anulare. *Lapin_ChannelQuantities* dă lungimea, volumul, excavația, lungimea grătarului, radierele de la cele două capete, căderea, adâncimea cea mai mică și cea mai mare și numărul de guri de scurgere; **Extrasul rigolelor de drenaj** le listează. Lapin o numește **rigolă de drenaj** peste tot unde o arată (proprietăți, navigatorul proiectului, extrase, numele uneia noi). În IFC pleacă drept *IfcEarthworksCut* (*TRENCH*), iar o rigolă cu fantă sau cu grătar drept *IfcDistributionChamberElement* (*TRENCH*), cu corpul ei; un fișier scris de Lapin revine ca aceeași rigolă.

## Borduri de sine stătătoare

O bordură nu trebuie să aparțină unui pavaj. **Bordură** (<kbd>BK</kbd>, în grupul **Drenaj**) desenează una de-a lungul unei polilinii pe teren; **Bordură pe o linie** (<kbd>BJ</kbd>) ia marginea unui pavaj, partea unei alei, o linie caracteristică sau conturul unei platforme pe care le alegi și o urmează. Bara alege **forma** (**Verticală**, **Teșită**, **Rotunjită** sau un profil al proiectului), de ce parte a liniei **stă**, privind de-a lungul ei (dreapta, implicit: în jurul unui contur desenat în sens trigonometric asta e în afară, departe de pavaj), **înălțimea** și **cotele**: cele ale liniei sau ale terenului (partea de sus a unui pavaj, unde e unul dedesubt). O bordură desenată ia terenul, una pe o linie ia cotele liniei, dacă nu spui altfel.

![Bordura de pe marginea de sud a pieței, coborâtă unde o traversează o alee](shots/drainage-kerb-3d.webp)

![Proprietățile unei borduri de sine stătătoare: forma, partea, cotele, materialul și coborârile](shots/drainage-kerb-properties.webp)

**Coborâri.** **Adaugă o coborâre** în proprietăți pune o porțiune (de la, până la, ca distanțe de-a lungul bordurii, și **Înălțimea vizibilă**: înălțimea ei deasupra pavajului acolo, `0` la o traversare); schimbă cifrele în tabel sau **scoate-o**. Fiecare porțiune coborâtă e un tronson aparte de bordură, în unghi unde linia se întoarce și tăiat drept unde se oprește. O bordură taie terenul pe care îl are dat, ca o rigolă, și straturile oricărui pavaj prin care trece (găsit unde stă; volumele lor din cantități o scad și ele), și are aceleași mânere în plan pentru punctele ei. *Lapin_KerbQuantities* dă lungimea, lungimea coborâtă și volumul; în IFC pleacă drept *IfcKerb* (IFC 4.3) sau proxy numit *Kerb* (IFC4). Bordura unui pavaj se schimbă cu pavajul (**Laturile bordurii**, mai sus): doar una fără pavaj are aceste proprietăți.

## Accesorii pe o linie

Stâlpi, lămpi, bolarzi, guri de scurgere, bănci sau arbori din câțiva în câțiva metri de-a lungul unei linii sunt o **repetare pe un traseu**, ca până acum, iar acum pot sta pe teren. **Pe o linie** (<kbd>AX</kbd>, în grupul **Drenaj**) e dialogul Repetare deja pus pe **Pe un traseu** cu **Înălțime: Pe teren**: selectează ce vrei să așezi (primul din rând), apasă, pune **Pasul** (sau numărul de copii), apoi dă clic pe linie într-un plan și pe punctul selecției care merge pe ea. Linia poate fi o alee sau un drum, o linie caracteristică, un pavaj (tot perimetrul lui), o bordură, o rigolă, o platformă sau orice linie desenată de tine.

![Stâlpi de iluminat din 6 în 6 m pe o alee, fiecare pe terenul în pantă](shots/drainage-posts-3d.webp)

**Înălțimea** unei repetări pe un traseu e **Ca originalul** (fiecare copie la înălțimea originalului, ca întotdeauna), **Pe teren** (fundul fiecărei copii pe terenul proiectat al terenului sau pe partea de sus a unui pavaj, unde stă) sau **Pe linie** (pe înălțimea proprie a liniei: a unei linii caracteristice, a marginii de sus a unui pavaj, a liniei roșii a unui drum; terenul unde linia nu are), cu o **Ridicare deasupra** în metri pentru o lampă montată pe un perete. Ambele sunt și în proprietățile repetării. Copiile se așază din nou când terenul sau linia se schimbă sub ele.

![Proprietățile unei repetări: pe un traseu, cu înălțimea pe teren](shots/drainage-array-properties.webp)

![Piața în plan: rigola cu gura de scurgere, bordura, aleea și stâlpii](shots/drainage-plan.webp)

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
