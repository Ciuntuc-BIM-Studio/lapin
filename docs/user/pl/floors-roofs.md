---
title: Stropy i dachy
order: 40
summary: Rysuj stropy i dachy po ich obrysie, wybieraj kształt i krawędzie dachu, wycinaj otwory.
---

## Stropy

Wybierz **Architektura → Strop** (<kbd>SB</kbd>), a następnie kliknij narożniki jego obrysu. Zamknij obrys na pierwszym punkcie albo naciśnij <kbd>Enter</kbd>. Strop również składa się z warstw, ustawianych w **Architektura → Typy stropów**. Warstwa typu stropu może mieć grubość 0 (hydroizolacja, farba): niczego nie buduje, na przekrojach jest rysowana linią i liczona powierzchnią stropu.

W **Typy stropów** każda warstwa ma **Priorytet** (od 0 do 100) i **Kategorię**, jak warstwy typu ściany. Warstwa ściany schodząca w strop (**W stropy** w **Typy ścian**) przechodzi przez warstwy stropu o niższym priorytecie i zatrzymuje się na pierwszej o priorytecie co najmniej równym. Pusty priorytet to priorytet materiału w **Zarządzaj → Kolejność cięcia**, w przeciwnym razie 50 dla rdzenia i 0 dla pozostałych warstw. Warstwa oznaczona **Wykończenie** znika, gdy widok pomija wykończenia podłóg.

Aby zmienić obrys, zaznacz strop i przeciągnij jego narożniki. Aby dodać narożnik, przytrzymaj <kbd>Ctrl/⌘</kbd> i kliknij mały romb pośrodku krawędzi albo kliknij prawym przyciskiem zaznaczony strop blisko krawędzi i wybierz **Dodaj narożnik tutaj**: nowy narożnik pojawi się tam, gdzie kliknięto. Aby usunąć narożnik, kliknij go z <kbd>Ctrl/⌘</kbd> albo kliknij prawym przyciskiem i wybierz **Usuń narożnik** (obrys zachowuje co najmniej trzy narożniki). Każda zmiana to jeden krok cofania; to samo działa dla otworów w stropie, dachów, pomieszczeń i balkonów. **Otwór w stropie** (<kbd>SH</kbd>) wycina w stropie otwór na schody lub szyb.

Aby powiększyć lub pomniejszyć cały obrys naraz, każdą krawędź o tę samą odległość (*Offset all edges* z Archicada), przytrzymaj <kbd>Shift</kbd> i przeciągnij romb pośrodku dowolnej krawędzi: obrys podąża za kursorem na żywo, w rzucie lub w 3D, a odległość można wpisać podczas przeciągania (<kbd>Enter</kbd> ją zatwierdza). Albo kliknij prawym przyciskiem zaznaczony strop, wybierz **Odsuń obrys…** i wpisz odległość: dodatnia idzie na zewnątrz, ujemna do środka. Proste krawędzie pozostają równoległe i znów spotykają się w narożnikach; krawędź łukowa pozostaje łukiem wokół własnego środka, a tam, gdzie łuk spotyka w narożniku inną krawędź, powiększanie zaokrągla ten narożnik. Odsunięcie, które sprawiłoby, że krawędź zniknie lub obrys przetnie sam siebie, jest odrzucane, a powód pojawia się przy kursorze. Działa dla stropów, otworów w stropie, sufitów podwieszanych, dachów (każda krawędź zachowuje swoją regułę okapu lub szczytu), pomieszczeń i balkonów oraz dla kilku zaznaczonych naraz; każde odsunięcie to jeden krok cofania.

![Obrys stropu odsunięty przez przeciągnięcie z Shift środka krawędzi](shots/floor-offset-outline.webp)

### Krzywe krawędzie, zaokrąglenia i okrągłe stropy

Pasek narzędzia **Strop** w wierszu poleceń rysuje obrys jako **Wielokąt** (klikane narożniki), **Prostokąt** (dwa przeciwległe narożniki), **Okrąg** (środek, potem punkt na okręgu) lub **Wewnątrz ścian** (kliknięcie wewnątrz ścian, które go zamykają; strop dochodzi do ich lic). W wielokącie **Linie (Spacja)** zmienia się w **Łuki (Spacja)**: kolejne krawędzie są łukami, każdy styczny do poprzedniej krawędzi, dopóki ponownie nie naciśniesz <kbd>Spacja</kbd>. **Otwór w stropie** i **Sufit podwieszany** mają ten sam pasek, bez **Wewnątrz ścian**.

![Narzędzie Strop rysujące okrąg obok stropu](shots/floor-shapes.webp)

Każda krawędź stropu, otworu w stropie lub sufitu może być krzywa. Kliknij prawym przyciskiem zaznaczony strop przy krawędzi i wybierz **Zamień na łuk**: krawędź wygina się na zewnątrz o jedną ósmą swojej długości, a na środku łuku pojawia się okrągły uchwyt. Przeciągnij go, aby wygiąć łuk przez wybrany punkt (romb, teraz na środku cięciwy, nadal przesuwa krawędź). **Wyprostuj** zamienia ją z powrotem w linię. Narożnik między dwiema prostymi krawędziami można zaokrąglić lub sfazować: kliknij go prawym przyciskiem i wybierz **Zaokrąglij narożnik…** (wpisz promień) lub **Sfazuj narożnik…** (wpisz, jak daleko wstecz wzdłuż każdej krawędzi). Narożnik staje się dwoma, tam gdzie łuk lub faza spotyka krawędzie; zbyt duży promień jest odrzucany. Każde z nich to jeden krok cofania.

![Strop z krzywą krawędzią i zaokrąglonym narożnikiem](shots/floor-curved-edge.webp)

Strop jest budowany wzdłuż łuków, żadna cięciwa nie dalej niż 1 mm od nich, w 3D, rzutach i przekrojach; jego pole i obwód mierzone są na prawdziwych łukach. Warstwa wychodząca poza krzywą krawędź (zobacz [Wysunięcia warstw](walls.md#layer-extents)) biegnie wzdłuż łuku. Otwór także może mieć krzywe krawędzie, a krzywy strop może mieć otwory. W IFC strop niesie obrys z prawdziwymi łukami jako ślad (IfcIndexedPolyCurve z segmentami łuku) obok swojej bryły. Dachy zachowują proste krawędzie, bo reguły krawędzi dachu (okap, szczyt) ustala się dla każdej prostej krawędzi.

Strop lub sufit podwieszany może mieć **własne otwory**: wyspy wycięte z obrysu, część elementu, a nie osobne otwory. Wybierz **Hole in the outline** w narzędziu **Opening** (w *In a floor*) i narysuj go wewnątrz stropu (lub zaznaczonego stropu albo sufitu) tym samym paskiem: wielokąt, którego krawędzie mogą być łukami, prostokąt lub okrąg. Po zaznaczeniu strop pokazuje narożniki i krawędzie każdego otworu jako uchwyty, z tym samym prawym klikiem (**Make arc**, **Fillet corner…**, **Chamfer corner…**); krzyżyk w środku otworu go przesuwa, a jego prawy klik oferuje **Remove hole**. Strop jest budowany wokół otworów, a jego powierzchnia je pomija, mierzona na prawdziwych łukach; otwór musi leżeć wewnątrz obrysu i z dala od innych, a obrys zmieniony tak, że go już nie obejmuje, jest odrzucany. W **Edit sketch** otwory są też pętlami szkicu: narysuj kolejną zamkniętą pętlę wewnątrz, a **Finish sketch** zrobi z niej otwór (największa pętla jest obrysem). W IFC otwory są kolejnymi krzywymi obrysu stropu i są wczytywane z powrotem jako otwory. Platforma (**Site → Hole in platform**) przyjmuje otwory tak samo: teren w otworze zachowuje swoje rzędne.

![Zaznaczony strop z prostokątnym i okrągłym otworem we własnym obrysie](shots/floor-holes.webp)

#### Edytuj szkic

Gdy chodzi o więcej niż narożnik czy łuk, kliknij prawym przyciskiem zaznaczony strop (albo otwór w stropie, albo sufit) i wybierz **Edytuj szkic** (także **Studio → Edytuj szkic**). Jego obrys otwiera się jako szkic na własnym rzucie, jedna linia lub łuk na krawędź, narożniki trzymane razem, a sam strop ukryty; pasek nad rzutem mówi, czyj to szkic. Rysuj i zmieniaj kartą **Rysunek** (Linia, Łuk, Zaokrąglenie, Utnij, Wydłuż, Odsunięcie…), a wiąż i wymiaruj w **Studio → Więzy** (zobacz [Więzy szkicu](sketch-constraints.md)): zmień wymiar, a obrys za nim pójdzie. **Zakończ szkic** zapisuje go z powrotem w stropie w jednym kroku cofania, a strop zachowuje tożsamość (swój GlobalId); **Anuluj** zostawia strop bez zmian. Szkic musi być jednym zamkniętym obrysem (krawędzie koniec do końca albo sam okrąg); inaczej Zakończ mówi, co jest otwarte.

![Obrys stropu edytowany jako szkic: zaokrąglenie i wymiar](shots/floor-sketch-edit.webp)

Szkic z więzami i wymiarami zostaje na stropie (trafia do IFC z intencją projektową stropu, w Lapin_Intent), więc **Edytuj szkic** otwiera go ponownie tak, jak go zostawiłeś. Jeśli obrys zmieniono od tego czasu uchwytami, szkic zaczyna się od nowa od obrysu, bez więzów. Strop rozpoznany z pliku IFC dostaje szkic dopiero, gdy go edytujesz; strop zachowany jako odniesienie nie jest tak edytowany.

![Strop po Zakończ szkic](shots/floor-sketch-done.webp)

Strop narysowany poza ścianami, przed drzwiami, to ganek. Schody prowadzące na niego opisuje strona [Schody](stairs.md).

Warstwa stropu może też wychodzić poza jego obrys albo kończyć się przed nim, dookoła lub na jednej krawędzi, wpisana we właściwościach albo przeciągnięta w rzucie lub przekroju przez **Edytuj warstwy**, a typ stropu może dać swoim stropom ich przedłużenia: zobacz [Przedłużenia warstw](walls.md#przed-uzenia-warstw). Dachy nie mają jeszcze układu warstw (pokrycie, płyta i więźba to ich osobne części), więc nie mają przedłużeń warstw.

## Dachy

Wybierz **Architektura → Dach** (<kbd>RF</kbd>) i kliknij obrys ścian pod nim. Lapin buduje dach nad tym obrysem, na wysokości szczytu jego poziomu.

![Dom z dachem dwuspadowym w widoku 3D](shots/house-3d.webp)

Zaznacz dach, aby zmienić go w panelu Właściwości:

- **Kształt.** Czterospadowy, dwuspadowy, jednospadowy, mansardowy, kolebkowy lub płaski. Dach kolebkowy to zakrzywione sklepienie między dwiema ścianami szczytowymi; **Kolebka: strzałka** ustala, jak wysoko wznosi się w kluczu, najwyżej do połowy rozpiętości.
- **Nachylenie i okap** dla całego dachu.
- **Kierunek kalenicy (dwuspadowy, jednospadowy)**: auto, x lub y. Kalenicą dachu jednospadowego jest jego wyższa krawędź: przy x opada w stronę −y, przy y w stronę −x; **Jednospadowy: odwróć spadek** sprawia, że opada w drugą stronę.
- **Każda krawędź** z własnym rodzajem (okap lub szczyt), nachyleniem i wysunięciem. Dach mansardowy z krawędziami szczytowymi to dach polski (łamany dwuspadowy).
- **Styl.** Style dachów nadają cały wygląd jednym wyborem: pokrycie, okapy, rynny i rury spustowe.
- **Materiał pokrycia.** Pokrycie jest z materiału projektu, domyślnie z materiału wybranego pokrycia (Clay tile, Metal sheet, Slate…), tworzonego w kolorze pokrycia, gdy dach po raz pierwszy go potrzebuje. Wybierz inny we właściwościach lub w **Pokrycie i odwodnienie** konfiguratora; zmiana pokrycia zmienia i jego, chyba że wybrałeś własny. Elewacje i przekroje kreskują pokrycie według tego materiału (dachówka, gont i łupek w poziomych rzędach, blacha w pionowych rąbkach, o ile nie nadasz mu własnego kreskowania **Elevation**), przekrój rysuje przecięte pokrycie kreskowaniem przekroju materiału, a IFC, przedmiar i kosztorys podają pokrycie pod jego materiałem.
- **Grubość pokrycia.** Puste: pokrycie jest tak grube jak w katalogu (membrana 2 cm, blacha 3 cm, dachówka 5 cm…); wpisz grubość, by nadać mu własną. Dach zaimportowany z innego programu zachowuje grubość z pliku.
- **Płyta.** Dach płaski, dach kolebkowy i dach z **Structure** none mają pod pokryciem płytę, osobną część: **Materiał płyty stropodachu** (domyślnie Reinforced concrete, tworzony za pierwszym razem, gdy dach go potrzebuje) i **Grubość płyty stropodachu** (domyślnie 16 cm), we właściwościach lub w **Więźba** konfiguratora. Pokrycie leży na niej, więc dach pozostaje tak gruby jak obie razem. Przekrój rysuje dwa pasy, każdy kreskowany według swojego materiału; okna dachowe przechodzą przez oba; ściana dołączona do dachu płaskiego sięga do spodu płyty; IFC zapisuje płytę i pokrycie jako dwie płyty dachowe z ich materiałów, a przedmiar i kosztorys mierzą płytę (powierzchnię i objętość) i pokrycie (powierzchnię), każde pod swoim materiałem. Właściwości pokazują te dwa pola tylko przy dachu bez więźby, a pola krokwi (przekroje, rozstaw, klasa, płatwie, końce) tylko przy dachu z więźbą; pole jednego kształtu (mansardy, strzałka sklepienia, odwrócenie dachu jednospadowego) pojawia się tylko przy tym kształcie.

![Właściwości dachu](shots/roof-properties.webp)

![Dach płaski w przekroju: betonowa płyta i pokrycie na niej, każde kreskowane według swojego materiału](shots/roof-deck-section.webp)

**Konfigurator dachu** (przycisk obok stylu albo **Architektura → Konfigurator dachu** przy zaznaczonym dachu) zbiera wszystko w jednym miejscu: style po lewej, następnie kształt i jego krawędzie, pokrycie i odwodnienie, więźbę i otwory, nad rzutem dachu (kliknij krawędź, aby ustawić ją jako auto, okap lub szczyt) oraz, dla więźby, przekrojem poprzecznym przez kalenicę z wiązarem lub parą krokwi pośrodku. Wybierz styl na początek i zmień dowolne pole ręcznie; **Zastosuj** robi z tego jeden krok cofania. **Zapisz jako styl…** zachowuje skonfigurowany dach jako styl projektu, wymieniony w grupie **Projekt** nad stylami z katalogu (i na liście w panelu Właściwości) dla innych i nowych dachów; **Aktualizuj styl** ponownie zapisuje w nim zmiany, a każdy dach tego stylu za nimi podąża. Stylowi projektu można zmienić nazwę albo go usunąć; jego dachy zachowują swój wygląd.

![Konfigurator dachu](shots/roof-configurator.webp)

**Rynny i rury spustowe.** W sekcji konfiguratora **Pokrycie i odwodnienie** rynna otrzymuje rozmiar, wysunięcie poza okap i odległość, na jaką wisi pod pokryciem, spadek do wylotów (mm/m) oraz denka; tabela krawędzi włącza lub wyłącza rynnę każdego okapu. Rury spustowe początkowo należą do dachu, jedna co 10 m; **Uczyń je edytowalnymi** zamienia je w elementy, a **Architektura → Rura spustowa** dodaje jedną kliknięciem na rynnie. Uchwyt rury spustowej przesuwa ją tylko wzdłuż jej rynny; jej właściwości ustalają średnicę, kolano wylotowe u dołu i przebieg, czyli łańcuch odcinków, każdy z długością i kątem od pionu (w stronę ściany), przy czym ostatni schodzi do gruntu. W IFC rynna to IfcPipeSegment GUTTER, a rura spustowa to odcinek sztywny o nazwie Downpipe.

Ściany na poziomie dachu zatrzymują się pod nim i same dochodzą do jego szczytów, także pod dachem zakrzywionym. Ściana na innym poziomie podąża za dachem po użyciu **Architektura → Dołącz do dachu** (<kbd>AR</kbd>): zaznacz ściany, a następnie kliknij dach. To samo narzędzie przyjmuje każdą inną bryłę nad ścianami: bryłę Studio, płytę, belkę, element zaimportowany z IFC. Ściany dochodzą do jej spodu i są wzdłuż niego przycinane, niezależnie od jej kształtu. **Odłącz od dachu** to cofa.

**Okno dachowe** i **Lukarna** dodają otwory w dachu: kliknij na połaci. Należą one do dachu. Zaznacz dach, a każde z nich pokaże na rzucie swoje uchwyty. Przeciągnij uchwyt pośrodku, aby przesunąć je po dachu. Przeciągnij uchwyty krawędzi lub narożników okna dachowego, aby zmienić jego rozmiar na połaci (przeciwległa krawędź zostaje na miejscu; długość możesz wpisać podczas przeciągania). Szerokość biegnie wzdłuż okapu, a długość w górę połaci, mierzona na połaci. Lukarna ma uchwyt z każdej strony do zmiany szerokości. Zmiana, która nie zmieściłaby się na dachu, zostanie odrzucona. Aby je usunąć, kliknij prawym przyciskiem jego uchwyt i wybierz **Usuń okno dachowe** (lub **Usuń lukarnę**), naciśnij **Delete**, gdy kursor jest nad jego uchwytem, albo kliknij uchwyt pośrodku z **Ctrl/⌘**. Właściwości dachu też je wymieniają: wpisz tam szerokość i długość okna albo kliknij **×**, aby je usunąć. Dach lukarny jest kryty materiałem pokrycia i mierzony razem z nim: ilości dachu podają osobno **pokrycie lukarn** (pole ich połaci, liczbę lukarn), więc przedmiar według materiału, kosztorys i zadanie harmonogramu na pokryciu liczą połacie dachu (bez wycięć lukarn) i dachy lukarn, każde raz.

![Uchwyt okna dachowego na zaznaczonym dachu](shots/roof-window-grip.webp)

### Więźba drewniana

**Konstrukcja** ustala, jak dach jest zbudowany:

- **krokwie**: krokwie na płatwiach, z płatwią kalenicową.
- **jętki**: pary krokwi z poziomą jętką na wysokości **Jętki: udział wysokości** (domyślnie dwie trzecie), bez płatwi.
- **wiązary**: prefabrykowane wiązary wzdłuż głównej kalenicy, w rozstawie **Rozstaw krokwi**: pasy górne na połaciach, pas dolny na murłatach, krzyżulce w układzie W (wiązar jednospadowy na dachu jednospadowym). Końce dachu czterospadowego zachowują swoje krokwie.
- **brak**: samo poszycie, bez drewna.

**Krokwie i pasy** przyjmują dawne przekroje w cm lub skandynawskie w mm (od 45×95 do 45×245); razem z nimi idą **Krzyżulce wiązarów** i **Klasa wytrzymałości drewna** (od C14 do C30, drewno klejone). Krokwie opierają się na murłacie przez zacięcie (**Oparcie krokwi na murłacie**) i kończą się na okapie pionowo lub prostopadle (**Zakończenia krokwi**). Style **Dach szwedzki z wiązarów** i **Dach skandynawski z jętkami** przynoszą ze sobą swoją więźbę.

Każdy element jest osobną częścią: zestawienie **Drewno dachu** wymienia je jako listę cięć (element, przekrój, klasa, długość, sztuki, objętość), a IFC otrzymuje każdy jako IfcMember swojego typu, a każdy wiązar jako IfcElementAssembly.

## Sufity podwieszane

**Wnętrza → Sufit podwieszany** (<kbd>CG</kbd>) rysuje sufit podwieszany nad klikniętym obrysem; **Sufit podwieszany w pomieszczeniu** wypełnia kliknięte pomieszczenie, podążając za jego obrysem. Sufit wisi na wysokości z pola **Wysokość** nad kondygnacją (2700 mm, chyba że zmienisz ją we właściwościach).

Jego typ, w **Wnętrza → Typy sufitów**, ustala **System**: **Płyty na widocznym ruszcie** (szerokość, długość i grubość płyty, szerokość lica, wysokość i obrót rusztu, wieszaki do konstrukcji w ich rozstawie) lub **Płyty g-k, ciągły**. Ruszt jest wyśrodkowany na suficie, więc docinane płyty przy krawędziach wychodzą równe.

**Panel świetlny** i **Nawiewnik** wstawiają element w kliknięte pole; kliknij to pole ponownie drugim narzędziem, aby je zmienić. W panelu Właściwości **Rozmieść** wstawia panele świetlne lub nawiewniki co określoną liczbę pól, a **Wyczyść** je usuwa. Zestawienia liczą całe płyty, panele świetlne i nawiewniki, a sufit trafia do IFC jako IfcCovering typu CEILING.

![Właściwości sufitu: jego typ, wysokość i wyposażenie](shots/ceiling-properties.webp)

![Sufity rastrowe z panelami świetlnymi i nawiewnikami, w 3D](shots/ceilings-3d.webp)

## Listwy przypodłogowe

**Wnętrza → Listwa w pomieszczeniu** (<kbd>SK</kbd>) układa listwę przypodłogową wokół klikniętego pomieszczenia, wzdłuż lic ścian, przerywaną przy każdych drzwiach (każdym otworze sięgającym do podłogi); kliknięta wewnątrz ścian bez pomieszczenia podąża za nimi w ten sam sposób. Listwa pomieszczenia podąża za pomieszczeniem: przesuń ścianę lub drzwi, a listwa przesunie się razem z nimi. **Listwa przypodłogowa** rysuje listwę wzdłuż linii klikniętej na licu ściany (Enter kończy); listwa leży po stronie odsuniętej od ściany, a **Odwróć stronę** we właściwościach przekłada ją na drugą stronę.

Jej typ, w **Wnętrza → Typy listew**, ustala **Profil** — **Płaska (prostokątna)**, **Fazowana (MDF)**, **Profilowana (drewniana)**, **Z wyokrągleniem u dołu (metalowa)**, **Lekko zaokrąglona** lub **Półokrągła**, narysowany obok w skali — **Wysokość** (od 40 do 300 mm), **Grubość** (od 5 do 40 mm), **Materiał** i jego kolor. **Z biblioteki** wprowadza do projektu gotowe typy: MDF 80 ścięta, Dąb 70 profilowana, Płytka 80 prosta, Aluminium 60 wyoblona, Zaokrąglona 50 i Półokrągła 70. Gdy narzędzie listwy jest włączone, panel Właściwości pokazuje typ, który ono układa, do wybrania przed kliknięciem.

Właściwości listwy zawierają jej typ, jej **Odsunięcie podstawy** nad podłogą (w sekcji Wymiary) oraz **Przerwy przy drzwiach** (wyłączone: biegnie dalej przez nie). **Zestawienie listew przypodłogowych** (lub **Listwy przypodłogowe** na liście nowych zestawień w przeglądarce projektu) wymienia je według typu, z liczbą sztuk i długością, bez przerw przy drzwiach. Listwa jest łączona na ucios w narożnikach; rzut rysuje jej przednią krawędź, a listwa trafia do IFC jako IfcCovering typu SKIRTINGBOARD, należący do IfcCoveringType tego samego typu.

![Typy listew przypodłogowych: profil narysowany w skali, biblioteka](shots/skirting-types.webp)

![Właściwości listwy przypodłogowej: jej typ, odsunięcie podstawy i przerwy przy drzwiach](shots/skirting-properties.webp)
