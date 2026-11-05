---
title: Extrase și grafice
order: 95
summary: Listezi uși, ferestre, buiandrugi, încăperi și tot ce numără un modul; adaugi rânduri și coloane proprii, scrii peste valorile modelului și faci un grafic din tabel.
---

## Cum scoți cantitățile

Tot ce modelează Lapin e măsurat din geometrie: lungimea, înălțimea, aria laterală brută și netă și volumul unui perete, aria și volumul fiecărui strat, ariile și volumul unei plăci, aria și perimetrul unei încăperi, numărul ușilor. Ai trei căi:

- **Un singur element.** Selectează-l: secțiunea **Cantități** din panoul Proprietăți arată ce măsoară, apoi fiecare strat sau parte a lui.
- **Tot modelul.** **Cost → Antemăsurătoare** face (sau deschide) antemăsurătoarea: toate elementele pe care le măsoară modelul, adunate rând cu rând, cu numărul, lungimea, aria brută și netă, volumul brut și net și masa. Bara ei alege ce se adună la un loc: **Pe tip de element și tip** (implicit: pereții fiecărui tip de perete, plăcile fiecărui tip de placă…), **Pe material** (fiecare strat la materialul lui: cărămida, tencuiala și izolația pereților separat, betonul buiandrugilor), pe nivel sau combinate. **Toate elementele** alege ce tipuri de elemente listează (doar pereți și plăci, de exemplu). Antemăsurătoarea urmează modelul ca orice extras și primește rânduri și coloane proprii (o rezervă, un preț unitar).
- **Orice altă listă.** **Cost → Extrase…** face orice extras oferă ediția: încăperi, finisaje, tâmplărie, alcătuiri de tipuri, lemn, armături, cofraje, oțel… Aceeași listă e la **+** lângă **Extrase** în navigatorul proiectului.

![Antemăsurătoarea grupată pe tip de element și material](shots/quantity-takeoff.webp)

Pe material, un material măsurat după **arie** (vezi [Materiale](materials.md)) are câte un rând pentru fiecare grosime în care e pus, cu o coloană **Grosime (mm)**: *Vată minerală 150 mm: 84,20 m²*, *Vată minerală 200 mm: 31,00 m²*. Unul măsurat după volum are doar m³, după masă kg, după bucăți **Bucăți**. O coloană **Notă** spune când un strat nu are o grosime din catalogul materialului. Devizul face la fel: un articol pe un material se măsoară cum spune materialul, câte un rând pe grosime. Un material fără regulă se măsoară ca înainte.

**Cost → Export XLSX** salvează direct antemăsurătoarea ca registru de calcul (în browser se descarcă).

## Cantitățile pe planșă

Un extras se pune pe planșă ca o vedere și e desenat acolo cu toate rândurile și valorile, totalurile și graficele lui:

- **Pune pe planșă…** din bara extrasului: **Planșă nouă** face o planșă A3 cu tabelul pe ea și o deschide, sau alege o planșă pe care o ai.
- Sau trage extrasul din navigatorul proiectului pe o planșă deschisă: ajunge unde îl lași. Planurile, secțiunile și fațadele se trag la fel.
- Sau **Pune o vedere…** din bara planșei.

Pe planșă, trage tabelul ca să-l muți; un tabel mai lung decât chenarul continuă pe alte pagini (planșa îți spune, iar **pune-l pe o planșă nouă** îl continuă). Tabelul urmează modelul: adaugi un perete și cifrele de pe planșă se schimbă. Se tipărește cu **Tipărește** și intră în PDF-ul și DXF-ul planșei exact cum e desenat.

![Antemăsurătoarea pusă pe o planșă nouă](shots/takeoff-sheet.webp)

## Cum faci un extras

Un extras e un tabel pe care îl umple modelul: uși și ferestre, buiandrugi, încăperi, armături, lemn, cofraje și altele. Dă clic pe **+** de lângă **Extrase** în navigator și alege ce listează, sau folosește unealta unui modul (Lemn → Extras de lemn, Organizare de șantier → Extras de cofraje…). Dă dublu clic pe el în navigator ca să-l deschizi într-un tab. Extrasul urmează modelul: muți un perete, adaugi o fereastră, și tabelul se schimbă odată cu ele.

Extrasele de **tâmplărie**, de **uși**, de **ferestre** și de **buiandrugi** arată o fișă pentru fiecare tip, cu desenul lui (implicit vederea interioară) și valorile lui rând cu rând. Celelalte extrase sunt un tabel simplu, un rând pe element și un rând de totaluri la sfârșit.

O ușă sau o fereastră întoarsă pe cealaltă mână a tipului ei (**Balamale** sau o singură săgeată de întoarcere; întoarsă pe ambele axe, e doar rotită) e imaginea în oglindă a tipului și primește o fișă a ei: marca se termină în **-M** (D-M), numele în „(mirrored)”, iar rândul **Opening**, desenul și numărătoarea sunt ale celeilalte mâini. Întoarce-o la loc și revine pe fișa tipului ei. Rândul **Opening** al unei uși îi numește deschiderea așa cum o arată planul: privind de la mâner spre balamale, o foaie care se deschide la stânga e *swing left*, una care se deschide la dreapta *swing right*; o pereche de foi e *double swing*. Stânga și dreapta unei ferestre se văd din interior, partea spre care se deschid canatele: *Batant dreapta (balamale în stânga)* în configurator, a cărui grilă se vede din exterior. IFC scrie aceeași fereastră ca stânga. Desenele arată fiecare ușă așa cum stă în model, văzută din interior, partea spre care se deschide. O notă sub fișe spune asta.

![Un extras de tâmplărie cu rândurile de rezistență la foc și producător](shots/joinery-schedule.webp)

## Rezistență la foc, producători și alți parametri

Parametrii unei uși sau ai unei ferestre intră singuri în extrasul de tâmplărie:

- **Rezistența la foc.** Rândul **Rezistență la foc** citește un parametru numit Fire rating, Fire Rated, FireRating sau Fire resistance. Pune-l pe tip, ca toate ușile tipului să-l aibă. În **Arhitectură → Uși și ferestre**, **Parametri · în extrase** → **+ Rezistență la foc** adaugă FireRating-ul tipului în Pset_DoorCommon (Pset_WindowCommon la o fereastră), unde îl ține IFC. Se vede și un parametru pe care îl adaugi tu unei singure uși, în proprietățile ei.
- **Orice alt parametru.** **+ Producător**, **+ Cod produs**, **+ Site web** sau un parametru al tău devine un rând separat în fiecare extras de tâmplărie. Valoarea proprie a unei uși apare lângă cea a tipului, când ușile unui tip diferă.
- **Scris în extras.** O valoare scrisă în rândul unui parametru (cu italice) intră în tip, și toate ușile tipului o urmează. Golește-o ca să scoți parametrul de pe tip.

![Parametrii tipului în configuratorul de uși și ferestre](shots/joinery-params.webp)

**Tipuri nefolosite.** Un tip pe care nu-l folosește nicio ușă sau fereastră din model e lăsat afară din extras. **Tipuri nefolosite** din bara extrasului le arată din nou, cu numărul lor.

## Rânduri și coloane proprii

Orice extras poate fi completat de mână, ca un tabel de calcul:

- **Rând** adaugă un rând al tău. Într-un extras de tâmplărie are o etichetă, și îi scrii valoarea pentru fiecare tip (Furnizor, Data livrării…). În orice alt extras e un rând întreg, scris celulă cu celulă (o terasă nemodelată, o rezervă); stă deasupra rândului de totaluri.
- **Coloană** adaugă o coloană a ta. Într-un extras de tâmplărie e o fișă fără niciun tip în spate, cu toate valorile scrise (o ușă de garaj comandată separat). În orice alt extras e o coloană după cele ale modelului (Finisajul pardoselii, Observații…).
- **Orice celulă** din tabelul de sub desen poate fi scrisă peste. O valoare scrisă peste cea a modelului e marcată; **↺** aduce înapoi valoarea modelului. Ce scrii rămâne la rândul lui: observațiile unei încăperi o urmează când lista e sortată sau încăperea e redenumită. Într-un tabel **Rooms**, un număr, nume, funcțiune, departament, finisaj al pardoselii sau o înălțime scrise pe rândul unei încăperi intră în încăperea însăși, ca și cum ar fi scrise în proprietățile ei; eticheta din plan și celelalte tabele le urmează.
- **×** de lângă un rând sau o coloană a ta o scoate, cu tot ce era scris în ea.
- Săgețile din capul unui tabel îi **sortează** rândurile după acea coloană: o dată crescător, încă o dată descrescător, a treia oară în ordinea modelului. Rândul de totaluri rămâne ultimul.

Toate se tipăresc: pe o planșă, în PDF și DXF, și în exporturile CSV și XLSX.

## Grafice

**Grafice** din bara extrasului deschide graficele lângă tabel. **Grafic nou** face unul din tabel:

- **Coloane**, **Bare**, **Sectoare** sau **Linie**.
- **Categorii din**: coloana care dă numele fiecărei bare sau fiecărui sector (Funcțiune, Nume, Marcă, Nivel…).
- **Cum**: **Sumă**, **Medie**, **Minim** sau **Maxim** dintr-o coloană (Arie, Total, Volum…), sau **Număr de rânduri**.
- **Cel mult**: păstrează doar categoriile cele mai mari și le adună pe celelalte la Altele.
- **Titlu**: un titlu al lui, sau cel pe care îl face singur (Suma Ariei după Funcțiune).

Motorul desenează fiecare grafic după tabele, așa că apare în extras, pe o planșă care pune extrasul și în PDF, exact la fel. Urmează modelul și ce ai scris: schimbi aria unei încăperi sau adaugi un rând al tău, și graficul se schimbă odată cu ele. **✎** schimbă un grafic, **×** îl scoate.

![Un extras de încăperi cu un rând propriu și un grafic cu sectoare al ariilor după funcțiune](shots/schedule-chart.webp)

## Alcătuirile tipurilor

Extrasul **Alcătuirile tipurilor** (**+** → **Alcătuirile tipurilor**) listează fiecare tip de perete și de placă: marca, grosimea totală, câte elemente îl folosesc, apoi straturile lui în ordine, cu materialele și grosimile lor.

**Desenele alcătuirilor**, în bara extrasului, desenează fiecare tip după tabel, secționat prin straturile lui la scara extrasului (1:50 dacă nu alegi alta): peretele așa cum îl taie un plan, placa așa cum o taie o secțiune, stratul 1 sus. Fiecare strat e umplut și hașurat ca materialul lui în secțiuni, fețele miezului se desenează mai groase, grosimea totală e cotată, iar fiecare strat e numit alături așa cum îl numește tabelul. Desenele se tipăresc cu tabelul, pe planșă și în PDF. Un extras nou de alcătuiri le desenează; debifează **Desenele alcătuirilor** ca să rămână doar tabelul.

Desenele **Pe fiecare fișă** (vedere din interior, vedere din exterior, plan, secțiune), din **Setări**, țin de fișele extraselor de tâmplărie; un tabel simplu nu are fișe, așa că acolo nu sunt oferite.

![Extrasul alcătuirilor tipurilor cu alcătuirea fiecărui tip](shots/type-assemblies.webp)

## Profile de perete

Extrasul **Profile de perete** (**Arhitectură → Extras de profile de perete**, sau **Cost → Extrase…** → **Profile de perete**) adună profilele și canalele din pereții proiectului pe profil, fel și material: câte sunt, bucățile lor (un profil tăiat de goluri e mai multe), lungimea de-a lungul fețelor și volumul; la un canal, volumul scos din perete. Un total pentru profile și unul pentru canale încheie tabelul. Bara lui le adună întâi **pe profil**, **pe nivel** sau **pe tip de perete**. Întoarcerile intră în volum, nu în lungime.

## Exportul

**CSV** și **XLSX** din bară exportă extrasul ca tabel de calcul (un XLSX are câte o foaie pentru fiecare tabel). În aplicația desktop, Lapin te întreabă unde să salveze fișierul și propune numele extrasului; în browser fișierul se descarcă. Tipurile de pereți și de plăci exportate se salvează la fel.
