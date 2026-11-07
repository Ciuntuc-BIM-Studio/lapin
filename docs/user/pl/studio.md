---
title: Studio
order: 85
summary: Modeluj bryły o dowolnych kształtach, szkicowane na dowolnej ścianie lub płaszczyźnie, a potem wypychaj, wciągaj, zaokrąglaj, wydrążaj, dziel i powielaj je w 3D.
---

## Karta Studio

Karta **Studio** tworzy bryły, których nie buduje żadna inna karta: ławkę, zadaszenie, pochwyt, fontannę, cokół na fasadzie. Każda bryła zaczyna się od szkicu lub prymitywu. Potem zmieniasz ją w 3D krokami, a każdy krok pozostaje edytowalny.

![Karta Studio](shots/ribbon-studio.webp)

Bryła Studio ma klasę IFC, jak każdy element. Wybierz ją w jej właściwościach: element prętowy, płyta, okładzina, balustrada, płyta stropowa lub dowolna inna. Jej ilości i eksport podążają za tą klasą.

## Gdzie szkicujesz

Obrys bryły szkicujesz narzędziami **Rysunek**, jako zamknięte polilinie, prostokąty i okręgi. Linie i łuki stykające się końcami też tworzą zamknięty obrys. Zamknięty kształt wewnątrz obrysu jest otworem.

Możesz szkicować na czterech rodzajach widoków:

- **Rzut** rysuje na poziomie swojej kondygnacji.
- **Przekrój** rysuje na swojej linii cięcia, pionowo.
- **Elewacja** lub **płaszczyzna odniesienia**: zobacz *Płaszczyzny odniesienia* poniżej.

## Tworzenie bryły

- **Wyciągnij** (EXTRUDE) wypycha obrys prosto na zewnątrz. Wpisz S, aby wyciągnąć w obie strony od szkicu, T, aby uzyskać zbieżność pochylającą boki do środka, i Cut, aby wycinała elementy zaznaczone wcześniej. Aby doprowadzić ją do ściany innego elementu, użyj **Do → lico…** w jej właściwościach lub **Wyciągnij do lica**. Głębokość, wysokości i grubości wpisuje się w jednostce projektu, jak mówi monit (`Depth (mm)`: `2000` to 2 m).
- **Przeciągnięcie** (SWEEP) prowadzi profil wzdłuż ścieżki. TWist obraca przekrój po drodze, SCale go powiększa, a RIse go podnosi. Okrąg, który się wznosi, staje się helisą, okrążającą TUrns razy.
- **Loft** (LOFT) łączy profile narysowane na różnych widokach. Wpisz S, aby przejście przez nie było gładkie. Linia zaznaczona z rolą *rail* wygina je jako prowadnica.
- **Obrót** (REVOLVE) obraca profil wokół linii osi.
- **Prymitywy** umieszczają jednym kliknięciem prostopadłościan, walec, kulę, stożek, torus, klin lub ostrosłup. Zmień ich wymiary i położenie w ich właściwościach.

![Pochwyt przeciągnięty wzdłuż okręgu, który wznosi się o 3 m w dwóch obrotach](shots/studio-helix.webp)

Każda bryła pozostaje powiązana ze swoim szkicem. Zmień kształt, a bryła podąży za nim, z tym samym GlobalId. Zaznacz bryłę, aby zmienić w panelu Właściwości jej klasę IFC, jej typ predefiniowany i jej typ. **Edytuj szkic** otwiera widok, na którym leży szkic.

Właśnie utworzona bryła jest zaznaczona, więc podświetla się z uchwytami swojego szkicu. Na rzucie, przekroju lub elewacji, tam gdzie widok przecina bryłę, jej przekrój jest wypełniony bielą: linie szkicu stają się kształtem, który widać jako pełny. Jeśli materiał ma własne wypełnienie lub kreskowanie, przekrój rysuje się nim.

## Pierścień, Skrzynia i Pas ściany

Te trzy narzędzia nadają narysowanemu kształtowi grubość w jego własnej płaszczyźnie, a potem wysokość od tej płaszczyzny:

- **Pierścień** (<kbd>RING</kbd>) bierze okrąg, łuk lub zamknięty obrys i tworzy pierścień lub rurę. Jego grubość leży **wewnątrz**, **na zewnątrz** lub **na środku** kształtu.
- **Skrzynia** (<kbd>BOX</kbd>) bierze prostokąt, zamkniętą polilinię lub okrąg i tworzy wokół niego ściany. **Dno** i **wierzch** są opcjonalne, każde z własną grubością.
- **Pas ściany** (<kbd>TWALL</kbd>) bierze linię, polilinię lub łuk i tworzy pas ściany. Jego grubość leży **po lewej**, **po prawej** lub **na środku**, patrząc w kierunku, w którym narysowałeś linię. Jego narożniki są na ucios.

Uruchom narzędzie i kliknij kształt. Wiersz poleceń oferuje opcje jako litery: <kbd>I</kbd>, <kbd>O</kbd> i <kbd>C</kbd> (lub <kbd>L</kbd>, <kbd>R</kbd> i <kbd>C</kbd>) dla położenia grubości, a dla skrzyni <kbd>B</kbd> i <kbd>T</kbd>, aby włączyć lub wyłączyć dno i wierzch. Następnie wpisz grubość i wysokość w metrach lub naciśnij <kbd>Enter</kbd>, aby zachować pokazaną wartość.

**Na rzucie** narzędzia domyślnie tworzą natywne ściany, typu ściany wybranego w **Architektura**. Zamknięty kształt staje się pętlą ścian połączonych w narożnikach, polilinia łańcuchem ścian, a łuk lub okrąg ścianami łukowymi. Położenie grubości decyduje, gdzie stoją ściany: cała grubość typu ściany idzie na wybraną stronę. Dno i wierzch skrzyni stają się stropami. Wpisz <kbd>S</kbd>, zanim wybierzesz kształt, aby zamiast tego utworzyć bryłę Studio.

**Na przekroju, elewacji lub płaszczyźnie odniesienia** narzędzia tworzą bryłę Studio klasy IFC IfcWall; dno i wierzch skrzyni to osobne płyty (IfcSlab). Wysokość idzie wzdłuż normalnej płaszczyzny, w twoją stronę; ujemna wysokość idzie od ciebie, jak przy **Wyciągnij**. Aby nadać takiej ścianie typ, zaznacz ją i wybierz lub utwórz typ w **Typ** w panelu Właściwości.

![Pierścień natywnych ścian łukowych wokół okręgu, skrzynia z dnem i wierzchem oraz pas ściany na rzucie parteru](shots/studio-thick-plan.webp)

Wszystko pozostaje parametryczne. Zaznacz bryłę i zmień jej **Grubość**, **Wysokość** i **Położenie grubości** w panelu Właściwości. Dla skrzyni zaznacz **Dół** lub **Góra** i ustaw ich grubości: Lapin dodaje lub usuwa płyty. Skrzynia z dnem lub wierzchem jest z nimi zgrupowana w zespół (IfcElementAssembly). Przeciągnij uchwyty kształtu lub edytuj go na jego widoku, a bryła zostanie przebudowana.

![Właściwości skrzyni: grubość, wysokość, dno i wierzch](shots/studio-box-properties.webp)

## Płaszczyzny odniesienia

Płaszczyzna odniesienia to płaszczyzna w dowolnym miejscu modelu z własnym widokiem. Rysujesz na niej narzędziami z karty Rysunek, jak na przekroju, a Studio tworzy na niej bryły. Utwórz ją na karcie **Studio**:

- **Płaszczyzna z widoku** (<kbd>REFPLANE</kbd>) leży równolegle do rzutu, przekroju, elewacji lub płaszczyzny przed tobą. Wpisz, jak daleko jest w twoją stronę (wartość ujemna idzie od ciebie).
- **Płaszczyzna na licu**: w widoku 3D kliknij ścianę dowolnego elementu. Płaszczyzna leży na tej ścianie, zwrócona na zewnątrz.
- **Płaszczyzna przez 3 punkty**: w widoku 3D kliknij trzy punkty na modelu. Płaszczyzna przechodzi przez nie.

Nowa płaszczyzna od razu się otwiera. Jest wymieniona w **Płaszczyzny odniesienia** w przeglądarce projektu. Jej rysunek to przekrój na płaszczyźnie, patrzący przeciwnie do strony, w którą jest zwrócona.

W widoku 3D każda płaszczyzna odniesienia jest rysowana jako półprzezroczysta tafla z nazwą, tak duża jak model na niej (lub o wymiarach z pól **Szerokość rysunku (3D)** i **Wysokość rysunku (3D)**, co najmniej 2 × 2 m), podświetlona pod kursorem. Kliknij ją, aby zaznaczyć płaszczyznę, kliknij dwukrotnie, aby otworzyć jej widok. Gdy płaszczyzna jest zaznaczona, przeciągnij strzałkę na jej środku, aby przesunąć ją wzdłuż jej normalnej: odsunięcie widać obok strzałki, a to, co narysowano na płaszczyźnie, przesuwa się razem z nią. **Widok → Płaszczyzny odniesienia** ukrywa i pokazuje tafle; nigdy nie trafiają do eksportów, renderów ani obrazów. Na rzutach, przekrojach i elewacjach płaszczyzna, która przecina widok, jest rysowana jako linia przerywana z nazwą, na całą rozpiętość widoku: kliknij ją, aby zaznaczyć płaszczyznę, kliknij dwukrotnie, aby ją otworzyć. Płaszczyzna równoległa do widoku nie jest na nim rysowana (niczego z niego nie przecina). Te linie są tylko na ekran: arkusze nigdy ich nie drukują.

![Płaszczyzna odniesienia na fasadzie południowej, z narysowanym na niej pierścieniem](shots/studio-plane.webp)

Zaznacz płaszczyznę w przeglądarce, aby ją przesunąć. **Początek x**, **y** i **z** ją umieszczają. **Kierunek** to kierunek na rzucie, w który jest zwrócona, **Pochylenie** to stopień odchylenia do tyłu (90° leży płasko, zwrócona w górę), a **Obrót** obraca jej rysunek wokół jej normalnej. **Przesuń wzdłuż normalnej…** i **Obróć wokół pionu…** robią to samo w jednym kroku. To, co narysowano na płaszczyźnie, i każda bryła z niej utworzona przesuwają się razem z nią.

![Właściwości płaszczyzny odniesienia](shots/studio-plane-properties.webp)

Przekroje i elewacje też są płaszczyznami. Bryła naszkicowana na przekroju stoi na jego linii cięcia i podąża za linią, gdy ją przesuwasz. Na przekroju rozwiniętym stoi na odcinku, na którym jest narysowana. Elewacja dostaje własną płaszczyznę przy pierwszym utworzeniu na niej bryły: z przodu modelu, z osią x taką, jak jest teraz narysowana. Zmień jej **Głębokość płaszczyzny szkicu** we właściwościach elewacji, aby przesunąć płaszczyznę i jej bryły wzdłuż widoku.

![Dom z bryłami Studio w 3D](shots/studio-3d.webp)

W IFC pierścień, ściany skrzyni i pas ściany są elementami IfcWall, dno i wierzch skrzyni elementami IfcSlab, a skrzynia z nimi to IfcElementAssembly z IfcRelAggregates; ich typy są zapisywane jako IfcWallType i IfcSlabType. Każdy eksport 3D (IFC, Fragments, glTF, USD, COLLADA) zapisuje bryłę Studio tam, gdzie stoi w modelu, niezależnie od płaszczyzny, na której ją naszkicowano, z jej otworami; zespół jest węzłem grupy nad swoimi częściami. Ponowne otwarcie własnego IFC Lapin przywraca bryły jako odniesienia, na miejscu, z ich klasą, typem i zespołem, ale już nieedytowalne (ich szkiców nie ma w pliku). Historia każdej bryły jest przechowywana we własnym zestawie właściwości Lapin, Lapin_Intent. Bryły Studio przyjmują tylko proste typy, bez warstw: do takiego swobodnego modelowania służą; ściany z warstwami to natywne ściany Lapin.
## Zmiana bryły w 3D

Te narzędzia działają na ścianach i krawędziach bryły w jej obecnym kształcie. Ściana pod myszą się podświetla.

**Wypchnij/Wciągnij**, **Podziel**, **Przetnij** i **Przesuń 3D** obsługuje się bezpośrednio na modelu. Kliknij ścianę, a stanie na niej strzałka: mysz przesuwa ścianę wzdłuż strzałki, a to, co zrobi krok, rysuje się na bieżąco. Wypchnij/Wciągnij rysuje na zielono to, co dodaje, a na czerwono to, co odejmuje. Podział lub przecięcie rysuje swoją płaszczyznę na żółto. Odległość widać obok strzałki. Wpisz wartość, aby ustawić ją dokładnie, np. 500 lub -250. Kliknij lub naciśnij <kbd>Enter</kbd>, aby ją zastosować, lub naciśnij <kbd>Esc</kbd>, aby zrezygnować; narzędzie pozostaje włączone dla następnej ściany. <kbd>Enter</kbd>, gdy nic nie przesunięto ani nie wpisano, powtarza ostatnią odległość. Przeciąganie idzie co centymetr, a z <kbd>Shift</kbd> co 10 cm. Prawy i środkowy przycisk nadal obracają i przesuwają widok. Pozostałe narzędzia pytają o wartość w małym polu obok kursora.

- **Wypchnij/Wciągnij** (PU) przesuwa ścianę na zewnątrz lub do środka, gdy odległość jest ujemna. Na wyciągnięciu jego koniec zmienia głębokość, a bok przesuwa krawędź szkicu pod nim. Na ścianie lub dowolnym innym elemencie wyciąganie dodaje bryłę w materiale ściany elementu, a wypychanie wcina się w element.
- **Zaokrąglij krawędź** (FE) zaokrągla krawędź najbliższą kliknięciu, a **Fazuj krawędź** (CE) ją fazuje. Wybierz *dookoła ściany*, aby zrobić to naraz ze wszystkimi krawędziami ściany.
- **Skorupa** wydrąża zaznaczone bryły, ze ściankami tak grubymi, jak wpiszesz. Może pozostać zamknięta lub zostać otwarta na końcu, na szkicu lub na obu.
- **Podziel** tnie bryłę na dwie płaszczyzną równoległą do wybranej ściany, tak głęboko w ścianę, jak przeciągniesz lub wpiszesz. Obie części zachowują szkic. **Przetnij** zachowuje tylko jedną stronę: wybierz **Zachowaj z tyłu** lub **Zachowaj z przodu** na pasku u dołu widoku.
- **Szyk** powtarza bryłę w rzędzie, a **Szyk biegunowy** powtarza ją wokół pionowej osi.
- **Obróć 3D** obraca bryłę wokół wybranej krawędzi. **Lustro 3D** odbija ją lustrzanie w płaszczyźnie ściany. **Przesuń 3D** stawia strzałki wzdłuż x, y i z na zaznaczonych bryłach: przeciągnij jedną lub wpisz odległość dla osi pokazanej obok wartości (<kbd>Tab</kbd> przechodzi do następnej osi).

![Wypychanie/wciąganie: górna ściana wyciągnięta w górę, to, co dodaje, narysowane na zielono, a odległość obok strzałki](shots/studio-push-pull.webp)

![Krawędź zaokrąglona: zaokrąglenie podąża za blokiem, gdy zmienia się jego głębokość](shots/studio-fillet.webp)

## Rysowanie na ścianie

W widoku 3D możesz rysować bezpośrednio na ścianie bryły i wypchnąć lub wciągnąć to, co narysujesz. Grupa **Na ścianie** na karcie **Studio** zawiera **Prostokąt na ścianie** (FRE), **Okrąg na ścianie** (FCI) i **Wielokąt na ścianie** (FPL).

1. Kliknij ścianę dowolnego elementu: bryły Studio, ściany budynku, stropu lub słupa. Kliknięcie jest pierwszym punktem: narożnikiem prostokąta, środkiem okręgu lub pierwszym wierzchołkiem wielokąta. Podczas rysowania ściana jest obrysowana.
2. Poruszaj myszą. Kształt podąża za nią po ścianie, a obok widać jego wymiar. Punkty przyciągają się do narożników ściany, środków jej krawędzi i jej środka; poza tym idą co centymetr, a z <kbd>Shift</kbd> co 10 cm. Kliknij przeciwległy narożnik lub punkt na okręgu. Możesz też wpisać wymiar: szerokość i wysokość prostokąta jako `1200, 600` albo promień okręgu. Wielokąt przyjmuje kliknięcie w każdym wierzchołku albo długość wpisaną w kierunku myszy. Kliknij jego pierwszy wierzchołek lub naciśnij <kbd>Enter</kbd>, aby go zamknąć; <kbd>Backspace</kbd> cofa ostatni wierzchołek.
3. Gdy tylko kształt się zamknie, staje na nim strzałka, jak przy **Wypchnij/Wciągnij**. Wyciągnij ją na zewnątrz, a kształt stanie się bryłą stojącą na ścianie, w materiale tej ściany. Wepchnij ją do środka, a stanie się wycięciem w elemencie: niszą, kieszenią albo otworem, gdy przejdzie na wylot. Kliknij lub naciśnij <kbd>Enter</kbd>, aby zastosować. <kbd>Esc</kbd> albo odległość 0 zostawia kształt na ścianie jako szkic do późniejszego wyciągnięcia.

Kształt jest szkicem na płaszczyźnie odniesienia leżącej na ścianie. Płaszczyzna nosi nazwę elementu i ściany, np. *Extrude 1, north face*, a kształty narysowane na tej samej ścianie korzystają z niej wspólnie. Widok 3D nie rysuje tych płaszczyzn, bo ściana i tak pokazuje, gdzie są. Otwórz jedną z przeglądarki projektu, aby zmienić szkic w 2D; bryła lub wycięcie podąża za nim. **Wypchnij/Wciągnij** na końcu bryły zmienia jej głębokość. **Wypchnij/Wciągnij** na dnie kieszeni pogłębia lub spłyca wycięcie, zamiast dodawać coś do elementu.

![Prostokąt narysowany na boku bloku, z wymiarem obok kursora](shots/studio-on-face.webp)

## Szkicowanie w 3D

Grupa **Szkic** na karcie **Studio** rysuje linie i kształty bezpośrednio w widoku 3D, na dwa sposoby, które zapisują to samo: szkic na płaszczyźnie odniesienia ściany, jak wyżej.

**Prosto na ścianie, jak w SketchUp.** Bez otwartego szkicu **Linia** (KL), **Prostokąt** (KR) i **Okrąg** (KC) rysują na ścianie pod kursorem. Ściana rozświetla się, gdy nad nią przesuwasz kursor; w pustej przestrzeni rysujesz na poziomie aktywnej kondygnacji. Pierwsze kliknięcie ustala płaszczyznę.

- Kursor wnioskuje, na czym stoi, i mówi to obok siebie: **Punkt końcowy**, **Punkt środkowy**, **Na krawędzi**, **Przecięcie**, **Na ścianie**. Od ostatniego punktu podąża za osiami modelu leżącymi na ścianie, narysowanymi w ich kolorach (czerwona x, zielona y, niebieska z), i biegnie **Równolegle do krawędzi** lub **Prostopadle do krawędzi** w kolorze magenta.
- <kbd>→</kbd> blokuje czerwoną, <kbd>←</kbd> zieloną, <kbd>↑</kbd> niebieską, <kbd>↓</kbd> kierunek krawędzi wywnioskowanej jako ostatniej; ten sam klawisz zwalnia blokadę. Przytrzymaj <kbd>Shift</kbd>, aby zachować kierunek wywnioskowany teraz.
- Wpisz wartość w dowolnej chwili, bez klikania w pole, i naciśnij <kbd>Enter</kbd>: `1200` to długość następnego odcinka w kierunku, w którym wskazuje mysz (`1000<30` pod kątem 30°), `1200,600` szerokość i wysokość prostokąta, `300` lub `r300` promień okręgu.
- Linię prowadzi się punkt po punkcie. Po powrocie do pierwszego punktu pętla się zamyka i strzałka wypchnij/wciągnij staje na niej od razu; <kbd>Esc</kbd> zostawia ją jako szkic. <kbd>Enter</kbd> lub <kbd>Esc</kbd> wcześniej zostawia linię otwartą, jako szkic na ścianie. <kbd>Backspace</kbd> cofa ostatni punkt. Narzędzie pozostaje włączone dla następnego kształtu; <kbd>Esc</kbd> przy niczym nienarysowanym je kończy.

![Linia narysowana na boku bloku, czerwona oś wywnioskowana z jej ostatniego punktu](shots/studio-sketch-3d.webp)

**Na płaszczyźnie roboczej, jak w CAD.** **Szkic na ścianie** (KF) i kliknięcie ściany czynią tę ścianę aktywną płaszczyzną roboczą: rozświetla się, a znacznik mówi *Szkic na* tej ścianie. **Szkic na płaszczyźnie** robi to samo z zaznaczoną płaszczyzną odniesienia. Gdy jest aktywna, **Linia**, **Prostokąt**, **Okrąg** uruchamiają na niej własne LINE, RECTANGLE i CIRCLE wiersza poleceń, a każde polecenie z grupy **Rysunek**, które wpiszesz, działa na niej we współrzędnych samej płaszczyzny, z przyciąganiem do obiektów, trybem ortogonalnym i śledzeniem jak w rzucie, w tym z krawędziami ściany. **Zakończ szkic** (KE, lub <kbd>Enter</kbd>, gdy nie działa żadne polecenie) zespawa narysowane linie i stawia strzałkę wypchnij/wciągnij na ostatnim narysowanym zamkniętym kształcie; <kbd>Esc</kbd> opuszcza szkic, a to, co narysowałeś, zostaje. **Spójrz na płaszczyznę** (KP) obraca widok prostopadle do płaszczyzny. Na ścianie oś x płaszczyzny biegnie od lewej do prawej, tak jak widzisz ścianę z zewnątrz, poziomo, a oś y w górę ściany (na ścianie górnej x to wschód, a y północ), więc `@1500,0` idzie w prawo, a wpisane wymiary czyta się tak samo w obu sposobach.

Oba łuki robią każdy jedną rzecz, ze szkicem lub bez: **Łuk z 2 punktów** (KA) to ten z SketchUp (niżej), **Łuk z 3 punktów** uruchamia ARC wiersza poleceń przez trzy punkty, na płaszczyźnie roboczej, gdy jest aktywna, a inaczej na poziomie kondygnacji.

![Szkic na ścianie: górna ściana bloku rozświetlona jako płaszczyzna robocza, prostokąt narysowany na niej wierszem poleceń](shots/studio-sketch-plane.webp)

**Linie dzielą ściany.** Linia poprowadzona w poprzek ściany, od krawędzi do krawędzi (albo zamknięta pętla z linii i łuków wewnątrz niej), dzieli ją na obszary. Ściana rozświetla się obszar po obszarze pod kursorem, a **Wypchnij/Wciągnij** na obszarze wypycha lub wciąga tylko ten obszar: osobną bryłę na ścianie albo wycięcie w niej. Linie, które kończą się przed krawędzią, niczego nie dzielą. Zamknięta pętla narysowana wewnątrz ściany (albo obszaru) jest jej otworem, jak w SketchUp: **Wypchnij/Wciągnij** na ścianie dookoła niej pomija pętlę, a pętlę wypycha się lub wciąga osobno.

- **Łuk z 2 punktów** (KA) rysuje na ścianie pod kursorem, na wzór SketchUp: kliknij początek, koniec, a potem to, jak bardzo się wygina (albo wpisz długość cięciwy, potem strzałkę łuku).
- **Odsuń** (KO) klika ścianę lub jej obszar i rysuje jej kontur jeszcze raz do wewnątrz lub na zewnątrz przez kursor albo o wpisaną odległość; zamknięty kształt od razu dostaje strzałkę wypchnij/wciągnij.
- Przy otwartym szkicu **Odsuń**, **Utnij**, **Wydłuż** i **Zaokrąglij** uruchamiają na płaszczyźnie roboczej polecenia OFFSET, TRIM, EXTEND i FILLET wiersza poleceń.
- Kształt narysowany na płaszczyźnie odniesienia i zaznaczony w widoku 3D pokazuje tam swoje punkty. Przeciągnij jeden: przesuwa się po płaszczyźnie, z przyciąganiem jak w narzędziach szkicu, a bryła zrobiona ze szkicu podąża za nim.
- Oba sposoby przyciągają też do krawędzi innych elementów leżących w tej płaszczyźnie, na przykład do boku sąsiedniego bloku zlicowanego ze ścianą.

![Linia w poprzek boku bloku dzieli go: obszar pod nią zostaje wyciągnięty osobno](shots/studio-sketch-region.webp)

**Taśma miernicza, linie pomocnicze i Podążaj.**

- **Taśma miernicza** (KM) mierzy i zostawia linie pomocnicze, jak w SketchUp. Kliknij krawędź, odsuń się od niej i kliknij: linia pomocnicza biegnie równolegle do krawędzi przez ten punkt albo w odległości, którą wpiszesz. Kliknij zamiast tego punkt, a drugie kliknięcie pokaże odległość i zostawi tam punkt pomocniczy. Linie pomocnicze są przerywane, oba sposoby szkicowania przyciągają do nich (i do miejsc, gdzie przecinają ścianę), są zapisywane w projekcie i nigdy nie są eksportowane ani drukowane. **Usuń linie pomocnicze** zabiera wszystkie.
- **Podążaj** (FM) przeciąga ścianę wzdłuż ścieżki tam, gdzie stoi: kliknij ścianę (albo jej obszar podzielony liniami), potem ścieżkę, czyli narysowaną linię, polilinię, łuk lub okrąg, albo inną ścianę, której krawędzie śledzi dookoła, jak górna ściana dla gzymsu. Narożniki są ścięte na ukos. Ściana staje się szkicem na swojej płaszczyźnie, a wynikiem jest jedna bryła Studio podążająca za obydwoma szkicami.
- **Skróty SketchUp** włącza w widoku 3D jednoliterowe klawisze SketchUp, dla tego komputera: <kbd>L</kbd> linia, <kbd>R</kbd> prostokąt, <kbd>C</kbd> okrąg, <kbd>A</kbd> łuk, <kbd>P</kbd> wypchnij/wciągnij, <kbd>F</kbd> odsuń, <kbd>T</kbd> taśma miernicza, <kbd>M</kbd> przesuń, <kbd>E</kbd> usuń, <kbd>Spacja</kbd> zaznacz. Są domyślnie wyłączone, bo zajęłyby miejsce dwuliterowych skrótów Lapin zaczynających się od tych liter w widoku 3D; skróty dwuliterowe i wiersz poleceń działają jak dotąd wszędzie indziej.

![Linia pomocnicza w wpisanej odległości od krawędzi bloku i gzyms poprowadzony dookoła góry za pomocą Podążaj](shots/studio-sketch-follow.webp)

## Kroki

Właściwości bryły Studio wymieniają jej kroki po kolei. Każdy krok można wyłączyć, przesunąć wcześniej lub później, zmienić lub usunąć. Zmień szkic, a każdy krok zostanie wykonany ponownie na nowej bryle. Krok, który nie może już znaleźć swojej ściany lub krawędzi, mówi o tym w **Problemy**.

![Walec wydrążony w rurę i powtórzony trzy razy: jego dwa kroki we właściwościach](shots/studio-steps.webp)

Właściwości polilinii wymieniają jej boki. Wpisz długość boku, a jego koniec przesunie się wzdłuż niego. Boki po nim podążają za nim, aż do następnego boku biegnącego w tym samym kierunku, więc prostokąt pozostaje prostokątem. Bryła na nim narysowana też podąża.

## Siatki z innych programów

**Plik → Importuj siatkę (OBJ, STL, glTF)…** wprowadza siatkę jako odniesienia, które widzisz i do których możesz przyciągać. Plik OBJ lub STL nie mówi, jaka jest jego jednostka ani gdzie jest góra, więc Lapin pyta. Zaznacz odniesienie i kliknij **Utwórz bryłę Studio**, aby stało się edytowalne. Przyjmuje wtedy wypychanie/wciąganie, zaokrąglenia, operacje logiczne i pozostałe kroki. To samo narzędzie działa na ścianach, płytach i elementach z plików IFC.

## Operacje logiczne

**Odejmij**, **Suma** i **Część wspólna** łączą dowolne elementy, a pierwszy zaznaczony jest celem. Narzędzia pozostają edytowalne, a wynik podąża za nimi. **Pokaż operandy** pokazuje ukryte narzędzia połączenia do edycji. **Usuń operację** ponownie rozdziela elementy.
