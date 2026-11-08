---
title: Import și export
order: 110
summary: Schimbi modelul ca IFC, aduci desene DXF, legi fișiere și exporți modele 3D pentru randare și AR.
---

Tot ce intră sau iese e în meniul **Fișier**.

![Meniul Fișier](shots/file-menu.webp)

## IFC și Fragments

**Exportă IFC, Fragments sau BOS…** scrie modelul ca IFC. Fiecare perete, placă, ușă și acoperiș e un element IFC adevărat, cu straturile, tipul și proprietățile lui, așa că fișierul se deschide ca model în alte programe BIM. Ușile și ferestrele spun cum se deschid: mâna fiecărei foi și cercevele, în ce parte se deschid și panourile, cum le definește IFC, așa că alte programe desenează aceleași deschideri.

Exportul întreabă schema (**IFC4**, citit peste tot, sau **IFC4X3**), detaliul (piesele ca elemente de sine stătătoare sau un corp pe element), faza și cum se scriu corpurile. **Geometrie** e **B-rep fațetat** (exact, ce așteaptă cel mai des alte programe) sau **seturi de fețe**: aceleași fețe, într-un fișier mult mai mic. **Simplifică** scrie mai puține triunghiuri unde un corp e o rețea fină, cum sunt tubulaturile și țevile din fișierul altui program, niciodată mai departe de el decât **toleranța** pe care o scrii (2 mm la început). Corpurile pe care le construiește Lapin (pereți, plăci, uși) își păstrează fețele exacte, iar simplificarea schimbă doar forma corpului, niciodată proprietățile lui.

![Exportă IFC](shots/export-ifc.webp)

**Importă IFC…** citește IFC-ul altui program. Înainte să intre ceva, Lapin citește fișierul și îți arată ce conține: elementele pe discipline (arhitectură, structură, instalații, mobilier și echipamente, sit), fiecare disciplină pe tipuri, cu câte sunt din fiecare, și etajele, cu câte elemente stau pe fiecare. Debifează ce nu-ți trebuie, de exemplu instalațiile și mobilierul când lucrezi la structură, sau păstrează doar etajele la care lucrezi. **Cum** aduce elementele recunoscute ca elemente Lapin, pe toate ca referințe sau legate. Cât timp fișierul se citește și se importă, Lapin arată ce face și cât a mai rămas; un fișier mare durează.

![Import IFC](shots/import-ifc.webp)

Unele fișiere au și 2D pe lângă model: simboluri de plan, amprente, hașuri și texte desenate pentru un element, sau elemente desenate doar în 2D (linii, texte, simboluri fără corp). Când un fișier le are, dialogul le numără la **Desenat în 2D**. Debifează **2D-ul propriu al elementelor** și planul desenează acele elemente prin secțiunea lor, ca pe ale lui Lapin; debifează **elementele desenate doar în 2D** și ele rămân afară. Ambele intră dacă nu le debifezi.

![Desenat în 2D](shots/import-ifc-2d.webp)

**Curățare și optimizare** ține regulile importului, aceleași pentru orice fișier, păstrate ca ale tale pentru importul următor. **Păstrează o singură dată un corp repetat** (bifat, dacă nu-l debifezi) păstrează o dată un corp pe care fișierul îl repetă sub alte nume (un șurub, o flanșă, un suport), elementele lui împărțindu-l, fără nicio pierdere. Celelalte lasă afară, când le bifezi, ce a adus alt program cu el: **corpurile plate** (texte, linii și hașuri făcute triunghiuri), **elementele mai departe de rest** decât distanța pe care o scrii (textele unui desen, un cartuș), **duplicatele** din același loc și **porturile și golurile scrise ca obiecte**. **Simplifică rețelele** scrie mai puține triunghiuri în limita toleranței pe care o scrii; **citește numerele scrise ca text** face din `DoubleLength:0,01` 0,01 m și din `1.15395 kg` o masă (un număr întreg singur, un cod, rămâne text). La **Ce se aduce**, **Seturi de proprietăți** arată seturile fișierului, cu câte elemente are fiecare; cele **tehnice** (transformarea din Navisworks, id-uri interne, statistici de geometrie) rămân afară dacă nu le bifezi. Mesajul importului spune ce a făcut fiecare regulă.

![Curățare și optimizare](shots/import-ifc-rules.webp)

Ce pune fișierul intră ca un singur grup, cu numele lui, pe care îl poți deschide și curăța, sau îl aduci liber debifând **Ca un singur grup** (vezi [Grupuri și curățare](groups.md)).

**Totul editabil**, o alegere din **Cum**, merge mai departe decât recunoașterea: ce rămâne referință devine un solid Studio din propriile triunghiuri, același element, cu clasa, proprietățile, etajul și grupul lui, pe care merg imediat împinge/trage, racordările și operațiile booleene (vezi [Studio](studio.md)). Un corp de peste 5.000 de triunghiuri, cum ar fi o pompă sau un fiting cu rețea fină, rămâne referință, iar mesajul importului spune câte și de ce. **Importă Fragments…** are același **Cum**: un fișier Fragments are doar triunghiuri, totuși pereții lui (cu ușile și ferestrele), plăcile și stâlpii se recunosc din ele ca elemente Lapin, restul rămâne referință sau, cu **Totul editabil**, devine solid Studio. Elementele unui IFC pe care recunoașterea nu le-a putut citi din fișier (un perete cu golurile tăiate în corp) se încearcă și din triunghiuri. **Importă rețea** întreabă **Ca: editabile**. Referințele aduse înainte se convertesc cu Modifică → **Convertește la nativ** (NV): recunoscute unde se poate, restul solide Studio; un grup selectat înseamnă membrii lui. O referință adusă înainte se face editabilă una câte una, cu Studio → **Fă solid Studio**, care o păstrează în grupul și pe etajul ei.

Lapin recunoaște pereții, plăcile, stâlpii, grinzile, fundațiile, ușile și ferestrele și le face elemente Lapin pe care le poți edita. Ce nu poate recunoaște intră ca referință, desenată, dar needitabilă. Un element devine element Lapin doar când e chiar al fișierului: Lapin îl măsoară pe fiecare recunoscut față de corpul pe care i-l dă fișierul, iar unul care diferă cu mai mult de 1 mm sau 0,5 % din volum (un stâlp cu vârful înclinat în fișier, un capăt de perete în trepte, pe care un element Lapin le-ar face drepte) intră ca referința care era; mesajul importului spune câte și de ce. Ușile și ferestrele intră deschizându-se cum spune fișierul: mâna, partea în care se deschid și, la ferestre, cum se deschide fiecare panou. Un acoperiș plat din alt program devine acoperiș Lapin când IfcRoof-ul lui e făcut din două sau mai multe plăci puse una peste alta, sau dintr-un singur corp cu un set de straturi: placa (sau stratul) de sus e învelitoarea, cu materialul și grosimea ei, cele de dedesubt placa acoperișului, din materialul celei mai groase; conturul e al plăcii, fără streașină, la fața ei de jos. Un acoperiș în pante, sau unul dintr-o singură placă fără straturi, rămâne cum a venit. Elementele recunoscute primesc doar ce are fișierul: fără buiandrugi, glafuri, pervaze sau praguri ale lui Lapin, iar fiecare gol are mărimea din fișier; le poți porni din tipurile elementelor, dacă le vrei.

Finisajele pe care alt program le-a exportat ca pereți separați (o tencuială de 2,5 cm pe fațadă, o tencuială interioară) devin strat al peretelui pe care stau, când sunt lipite de fața lui, la fel de înalte și de-a lungul lui. Peretele primește un tip cu acel strat (*Masonry 350 + Lime render*), straturile lui rămân unde erau, iar mesajul importului spune câte au fost. Un finisaj care stă la distanță de perete, sau care acoperă doar o parte din el, rămâne perete subțire. Un stâlp pe care fișierul îl dă ca un corp tăiat la vârf de o pantă (sub o grindă sau un acoperiș înclinat) intră ca stâlp cu secțiunea lui, până la punctul cel mai înalt.

**Mapează tipurile importate** (Gestionează) arată tipurile făcute de import (de pereți, plăci, uși, ferestre, stâlpi, grinzi și fundații), fiecare cu fișierul lui și numărul de elemente. Alege la **Devine** un tip al proiectului de același fel și apasă **Mapează**: elementele lui iau tipul proiectului, un perete își păstrează miezul unde era, iar tipul importat dispare. Cu **Ține minte** bifat, un import ulterior al unui tip de acest fel și nume ia direct tipul proiectului, iar mesajul lui o spune. Alegerile ținute minte apar în dialog, fiecare cu **Uită**.

![Mapează tipurile importate](shots/map-types.webp)

Spațiile fișierului (din Revit, ArchiCAD și altele) intră ca încăperi Lapin, fiecare pe nivelul ei, cu conturul, numărul, numele, înălțimea, funcțiunea, departamentul și finisajul pardoselii. Tot ce avea în fișier un element importat (seturile de proprietăți și cantitățile) se păstrează și apare la **Proprietăți IFC** în proprietățile lui. Încăperile se văd în planurile nivelurilor lor (eticheta; o schemă de culori din **Vedere → Culorile încăperilor** le umple); în 3D sunt ascunse până le arată **Vizibilitate / Grafică**. O referință iese din **Exportă IFC…** cum a intrat: cu același GlobalId, aceeași clasă și aceleași atribute (descriere, tag, tip predefinit), cu seturile de proprietăți și cantitățile, fiecare valoare cu tipul din fișier, și cu tipul, materialul, porturile, conexiunile și sistemul ei, fiecare cu GlobalId-ul lui. Dacă aduci doar instalațiile dintr-un fișier și le exporți, fișierul nou le are întregi.

**Importă Fragments…** deschide același panou pentru un fișier `.frag` That Open: elementele lui pe discipline și tipuri, etajele lui și **Ca un singur grup**. Un fișier exportat chiar din acest proiect nu aduce nimic nou când îl imporți înapoi în el: elementele lui sunt deja aici.

![Importă Fragments](shots/import-frag.webp)

**Importă Fragments…** aduce un fișier `.frag`: fiecare element cu geometrie devine o referință desenată din corpul lui, în clasa pe care o spune fișierul, pe nivelul pe care îl pune fișierul (potrivit după nume cu ale tale, creat când lipsește), cu atributele fișierului la **Proprietăți IFC**. Elementele cu același corp (aceeași formă, așezată în mai multe locuri) îl împart. Importat din nou, același fișier actualizează ce a adus înainte. Ce se recunoaște din triunghiurile lui alegi la **Cum**, ca mai sus. Seturile de proprietăți și de cantități, tipul și materialele vin împreună cu atributele; sticla rămâne transparentă; un fișier făcut de convertorul That Open ajunge unde l-ar pune IFC-ul lui, iar fețele cu goluri (un perete în jurul ferestrei) le păstrează.

**Exportă IFC, Fragments sau BOS…** scrie modelul ca IFC, ca Fragments sau ca BIM Open Schema, ales la fel ca un import: elementele pe discipline și tipuri, etajele, faza și doar ce arată vederea 3D. IFC iese în IFC4 sau IFC4X3, cu părțile unui element stratificat sub el sau câte un corp fiecare; Fragments de la punctul de bază al proiectului, de la originea internă sau de la punctul de topografie. Un fișier Fragments poartă ce arată un vizualizator IFC: tipul fiecărui element, cantitățile, parametrii tăi, materialele și coordonatele pe hartă, cum le scrie convertorul That Open.

![Exportă IFC, Fragments sau BOS](shots/export-exchange.webp)

### BIM Open Schema

[BIM Open Schema](https://github.com/ara3d/bim-open-schema) (`.bos`) e un format deschis al Ara 3D: o arhivă zip cu tabele Apache Parquet (entitățile, parametrii și relațiile lor, plus triunghiurile așezate prin instanțe), ușor de interogat cu instrumente de date precum DuckDB, pandas sau Power BI și citit de vizualizatoarele web ale Ara 3D.

**Importă BIM Open Schema…** deschide același panou ca importul IFC sau Fragments: elementele pe discipline și tipuri, etajele, **Cum** și **Ca un singur grup**. Fiecare element cu geometrie vizibilă devine o referință desenată din corpul lui, în clasa IFC pe care o numește categoria lui (o categorie Revit precum *Walls* sau *Doors* se citește drept clasa ei IFC), pe etajul lui (cel în care e conținut sau, pentru un fișier din Revit, nivelul lui), potrivit după nume cu ale tale și creat când lipsește. Parametrii lui vin ca seturi de proprietăți (atributele IFC la **Attributes**, tipul și materialele împreună cu ele); elementele cu aceeași formă împart un corp; sticla rămâne transparentă. Încăperile și tot ce fișierul marchează ascuns rămân pe dinafară. Importat din nou, același fișier actualizează ce a adus înainte.

![Importă BIM Open Schema](shots/import-bos.webp)

![Casa importată, în 3D](shots/import-bos-3d.webp)

**Exportă IFC, Fragments sau BOS…** cu **BIM Open Schema** bifat scrie modelul cum scrie un fișier convertorul IFC al Ara 3D: proiectul, situl, clădirea și etajele, fiecare element sub clasa lui IFC, cu GlobalId-ul, tipul, materialele, atributele IFC, seturile de proprietăți și de cantități și parametrii tăi, conținut în etajul lui; triunghiurile în zecimi de milimetru, o formă folosită de mai multe ori (un tip de stâlp, o fereastră) păstrată o singură dată. Golurile ies cum le scrie convertorul: câte un element gol pentru fiecare ușă, fereastră și gol simplu, care taie peretele și e umplut de ușa sau fereastra lui, cu dimensiunile lui. Corpul lui e ascuns în vizualizatoare, dar rămâne pentru verificarea coliziunilor. Norii de puncte nu fac parte din format și rămân pe dinafară.

## JG Structures (calcul structural)

**Exportă JG Structures (JSON)…** scrie modelul analitic al proiectului în fișierul de schimb al lui JG Structures, un program de calcul structural: un fișier JSON după contractul lui (versiunea 1.0.0). Lapin face modelul din ce ai desenat. Stâlpii, grinzile și barele de oțel devin bare pe axele lor; pereții portanți (Pset_WallCommon.LoadBearing pe perete sau pe tipul lui) și planșeele devin plăci pe mijlocul stratului lor de rezistență, fără finisaje. Ele au noduri comune: o grindă care poartă un planșeu stă în planul median al planșeului, capetele stâlpilor și ale pereților ajung la grinda sau planșeul de la cap și de la bază, colțurile unui planșeu trec pe stâlpii și pereții de sub el, iar planșeul se împarte de-a lungul grinzilor care îl traversează. Bazele stâlpilor și ale pereților de pe o fundație sunt încastrate; baza unui stâlp de oțel e articulată sau încastrată, cum spune îmbinarea ei.

Alegi etajele, tipurile de elemente și unitățile: centimetrii și kilogramforța contractului, sau SI (metri și kilonewtoni). Materialele își iau valorile din datele lor (Gestionare → Materiale, Pset_MaterialMechanical în pascali) sau din clasa de rezistență din nume, C25/30 sau S355, după Eurocoduri; aria, momentele de inerție, modulele de rezistență și constanta de torsiune ale unei secțiuni se calculează din geometria ei. Ce nu știe Lapin rămâne gol (null) în fișier, niciodată zero. Înainte de export, raportul numără ce intră, verifică fișierul după regulile contractului și arată ce îi mai lipsește calculului: un material fără clasă, un stâlp care nu stă pe nimic.

Unde elementele nu se întâlnesc pe axe (o pană pe o grindă de cadru, o riglă pe un stâlp, o contravântuire pe guseul ei, căpriorii pe cosoroabă), Lapin pune nodul pe elementul care le susține și păstrează distanța ca decalaj la capătul barei, așa că și elementele secundare ale unei hale sunt legate. Pereții își păstrează ușile, ferestrele și golurile, iar planșeele golurile lor: plăcile contractului nu au goluri, așa că se împart în bucăți în jurul lor, iar un planșeu în L se împarte în bucăți convexe de-a lungul grinzilor de sub el. Un perete de sub acoperiș urcă până la el, iar acoperișul se exportă și el: placa lui ca plăci, sau căpriorii, panele și cosoroabele unei șarpante de lemn ca bare din clasa lemnului (C24 după EN 338). Cornierele și celelalte secțiuni nesimetrice se dau după axele lor principale. **Picioare pe fundații** alege cum sunt rezemate picioarele stâlpilor și pereților de pe o fundație: încastrate (implicit) sau articulate. Baza unui stâlp de oțel își păstrează îmbinarea ei.

![Exportă JG Structures](shots/export-jg.webp)

## Optimizarea unui fișier IFC

**Optimizează IFC…** (în Fișier, sau pe ecranul de start: nu trebuie să fie deschis niciun proiect) face o copie mai ușoară a unuia sau mai multor fișiere IFC, fără să schimbe ce spun ele. Copia păstrează fiecare GlobalId, nume, set de proprietăți, cantitate, tip, material și relație exact cum le-a scris fișierul, și antetul lui; nu se adaugă nimic de-al lui Lapin. Se schimbă doar geometria: mesh-urile (seturi de fețe, B-rep-uri fațetate cu fețe plane) sunt curățate (vârfurile duplicate sudate, triunghiurile degenerate și repetate și coordonatele nefolosite eliminate) și simplificate, niciodată mai departe de original decât **Precizia** (Rapid 5 mm, Echilibrat 1 mm, Precis 0,1 mm sau a ta), măsurată în ambele sensuri; corpurile identice se scriu o singură dată și sunt partajate de produsele lor; iar ce nu e referit de nimic (puncte, stiluri nefolosite, reprezentări goale) dispare. Extrudările, operațiile booleene și celelalte corpuri exacte rămân cum sunt. Rotunjirea coordonatelor, B-rep-urile scrise ca seturi de fețe (doar IFC4 și IFC4X3) și un `.ifczip` arhivat sunt acolo când le vrei.

![Optimizează IFC](shots/optimize-ifc.webp)

Copia se scrie lângă original, sau într-un folder ales de tine, cu numele originalului și o **Terminație** (la început `.optimized`): din `casa.ifc` iese `casa.optimized.ifc`. Originalul nu este scris niciodată, iar dacă copia există deja Lapin întreabă înainte s-o înlocuiască. Pentru fiecare fișier raportul dă mărimea, entitățile și triunghiurile înainte și după, corpurile partajate, orfanii eliminați, abaterea cea mai mare măsurată și durata; **Deschide folderul** arată copia, iar **Deschide în Lapin** o deschide în altă fereastră. În browser copia se descarcă. Un fișier deja adus în proiect își păstrează drumul înapoi: **Optimizează fișierul sursă…** pe grupul unui IFC importat (în proprietățile lui) sau pe o legătură (Gestionare → Legături) face același lucru cu fișierul din care a venit, cât timp acesta e încă pe disc.

![Raportul](shots/optimize-ifc-report.webp)

## DXF și DWG

**Importă DXF…** aduce un desen CAD în vederea din față, cu layerele, hașurile, textele și cotele lui. Intră ca desen de care te poți prinde și pe care îl poți edita. Ca să folosești un DWG, salvează-l întâi ca DXF din programul tău CAD (AutoCAD, BricsCAD sau DraftSight: **Save As → DXF**). Un PDF sau o imagine intră pentru calchiat cu **Fișier → Importă PDF sau imagine…** sau **Desen → PDF sau imagine**: liniile unui PDF vectorial ca desen, la scara la care a fost desenat, iar pozele lui și orice PNG sau JPEG ca imagini translucide (vezi [Desen](drafting.md)). Într-un plan, **Așezat: după coordonatele hărții** pune un desen în coordonate reale (un plan cadastral în Stereo 70) unde îl pune harta (vezi [Amplasamentul și datele lui](site.md)).

Desenul stă și în **vederea 3D**, pe planul vederii în care a intrat: pe un plan la cota nivelului, pe o secțiune sau fațadă pe planul acelei vederi, în culorile layerelor lui, cu hașurile estompate, ca linii subțiri care nu ascund niciodată modelul. Un layer stins în **Desen → Layere** rămâne stins și în 3D. **Vedere → DXF în 3D** (și în **Vedere → Setările vederii** ale unei vederi 3D) arată sau ascunde toate importurile; un import anume se comută din **În 3D** în proprietățile oricărei forme a lui, un DXF legat din coloana **3D** din **Gestionare → Legături**. Desenul în 3D pe acel nivel, cu uneltele din Desen sau cu Schița din Studio, face snap pe liniile lui, așa că îl poți calchia în 3D ca pe plan. Liniile cărora fișierul le-a dat cote stau în spațiu la acele cote, ca înainte.

![Un DXF de amplasament în vederea 3D, la cota parterului](shots/dxf-3d.webp)

Un DXF importat face parte din proiect: **File → Save** îl păstrează în fișierul `.lapin` cu straturile, hașurile, textele și orice grup făcut din el, iar la redeschiderea proiectului e acolo, pe planul nivelului. Vederea de plan în care a intrat e de atunci planul nivelului, cea listată în browserul proiectului, iar ce era desenat înainte pe nivel trece pe ea. Lapin întreabă dacă salvezi înainte să se închidă cu modificări nesalvate. Ca desenul să rămână un fișier separat, bifează **As a link** în dialogul de import: rămâne legat, needitabil, iar **Manage → Links** îl reîncarcă atunci când fișierul se schimbă.

## Legături

**Leagă IFC…** ține alt fișier legat în loc să-l importe. E doar pentru citire și se reîncarcă atunci când se schimbă. Merge și pentru un DXF. **Gestionare → Legături** arată, reîncarcă, convertește sau scoate legăturile. Un DXF legat stă în 3D ca unul importat; bifa **3D** din **Gestionare → Legături** îl scoate de acolo. **Vizibilitate / Grafică** (<kbd>VV</kbd>) ascunde o legătură, o estompează sau îi dă categoriile ei doar într-o vedere (*Secțiuni, fațade și 3D*, Vizibilitate / Grafică).

Un IFC legat păstrează ce spune fișierul despre materialele elementelor lui. Pereții, planșeele și acoperișurile cu seturi de straturi se desenează în straturile lor chiar ca referințe, așa că un inginer ascunde finisajele legăturii de arhitectură (**Pereți ▸ Finisaje**, **Plăci ▸ Finisaje**), iar un stil de vedere de structură le desenează doar miezurile; barele unui model de structură legat sunt în categoria **Armături**, pe care orice vedere 3D o lasă deoparte până o arăți. O legătură făcută cu o versiune mai veche își primește straturile pereților când e reîncărcată (ale planșeelor imediat).

## Nori de puncte

**File → Link point cloud…** leagă o scanare laser sau un nor din fotogrammetrie, numai în formate deschise: **LAS** (1.0–1.4, toate formatele de punct), **LAZ** (LAS comprimat), **E57**, **PLY** (ASCII și binar) și puncte ca text, **XYZ**, **PTS** și **CSV**. Formatele proprietare (RCP și RCS din Autodesk ReCap, FLS de la FARO, LGS de la Leica) nu se citesc: exportă întâi scanarea din programul ei ca E57 sau LAS.

Lapin întreabă unde stă norul și în ce unitate:

- **după coordonatele de hartă ale proiectului**: x și y din fișier sunt est și nord, așezate prin punctul de survey și unghiul față de nord din **Manage → Location**, cotele peste cota punctului de survey. Lapin îl propune când fișierul își numește sistemul de coordonate sau coordonatele lui sunt departe de zero și proiectul are o localizare;
- **origine pe origine**: originea fișierului pe a proiectului;
- **centrat pe originea proiectului**: mijlocul lui pe origine, punctul cel mai de jos la ±0.00;
- **unitatea lui**: cum spune fișierul (LAS și E57 o spun), altfel metri; se oferă și centimetri, milimetri, picioare, picioare US survey și inci. Când norul iese nepotrivit pentru o clădire sau un teren (sub 20 cm sau peste 50 km), ori fișierul spune altă unitate, Lapin avertizează.

Norul rămâne în fișierul lui. Nu se copiază în proiect: proiectul păstrează doar legătura (calea fișierului, unde stă norul, cum se desenează). Lapin îl indexează o dată, în fundal, într-un octree de bucăți în folderul lui de pe acest calculator (nu lângă proiect), iar **Manage → Links** arată cât a avansat. Apoi vederea 3D aduce bucățile de care camera are nevoie, întâi pe cele apropiate și tot mai fine pe măsură ce te apropii, într-un buget de puncte dat de **View → Display quality** (1,5, 3 sau 6 milioane de puncte desenate deodată), așa că o scanare de o sută de milioane de puncte se rotește la fel de lin ca una mică.

![O scanare a casei legată ca nor de puncte, în vederea 3D](shots/point-cloud-3d.webp)

În **Manage → Links** un nor are rândul lui: punctele, cât a avansat indexul, **Shown**, **Halftone**, **3D**, ce colorează punctele (**RGB**, **Intensity**, **Elevation** sau **Classification**, după clasele ASPRS: teren, vegetație, clădire, apă…) și **Size**, mărimea unui punct în pixeli. Butonul cu pin îl reașază (după hartă, origine pe origine sau centrat, unitatea, mutat pe x, y și z, rotit); butonul de reîncărcare recitește fișierul și îl reindexează doar dacă s-a schimbat. **Unlink** scoate legătura; indexul rămâne în folderul lui Lapin și servește norul din nou dacă e legat din nou.

![Manage → Links cu un nor de puncte](shots/point-cloud-links.webp)

Un nor nu se exportă cu modelul: IFC și Fragments duc modelul, nu scanarea, iar **Export 3D…** ia un eșantion din el ca puncte în GLB și glTF doar la cerere. Planurile, secțiunile și fațadele îl desenează și ele (*Secțiuni, fațade și 3D*, Norii de puncte în vederi). În browser se poate lega un nor de cel mult 160 MB; e indexat în pagină și trebuie legat din nou după reîncărcarea paginii. Norii mai mari cer aplicația desktop.

## Modele 3D

**Export 3D (glTF, USD, DAE)…** scrie modelul ca suprafețe pentru randare, vizualizare și realitate augmentată (AR):

| Format | Pentru |
|---|---|
| GLB, glTF | Blender, three.js, vizualizatoare web. Imaginile proprii ale materialelor sunt incluse. |
| USDZ | AR Quick Look de la Apple, Reality Composer, Omniverse. Imagini incluse. |
| USDA | OpenUSD ca text, un singur fișier. |
| DAE | COLLADA, pentru SketchUp și programe mai vechi. |
| OBJ + MTL | Un .zip pentru Twinmotion, Lumion, 3ds Max și orice program de randare. Cu imaginile. |

**Importă rețea (OBJ, STL, glTF)…** aduce o rețea dintr-un alt program 3D (OBJ, STL, glTF sau GLB) ca referințe pe nivelul activ. glTF își spune unitatea și care axă e în sus; pentru OBJ și STL, Lapin întreabă. **Fă solid Studio** din Studio face o referință editabilă (vezi [Studio](studio.md)).

![Export 3D](shots/export-3d.webp)

Alegi ce exporți, ce fază și originea. **Ce** este **tot**, sau modelul **cum îl arată vederea**: vederea 3D din față, fără elementele și categoriile ei ascunse și fără etajele pe care nu le arată, cu straturile pe care le arată (fără finisaje sau doar miezurile). Când vederea e tăiată (tăietura de deasupra etajelor afișate, o secțiune arătată în 3D, cutia de secțiune), **Tăietură** taie și exportul acolo și închide fiecare tăietură în materialele ei, ca fișierul să arate ca ecranul. O vedere 3D care lasă ceva deoparte se exportă cum o arată, dacă nu alegi altfel. Fiecare element își păstrează clasa IFC, tipul și GlobalId-ul. Ca să schimbi modelul în sine, nu suprafețele lui, folosește **Exportă IFC**.
