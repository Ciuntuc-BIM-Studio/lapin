---
title: Import i eksport
order: 110
summary: Wymieniaj model jako IFC, wczytuj rysunki DXF, łącz pliki i eksportuj modele 3D do renderingu i AR.
---

Wszystko, co wchodzi lub wychodzi, znajduje się w menu **Plik**.

![Menu Plik](shots/file-menu.webp)

## IFC i Fragments

**Eksportuj IFC lub Fragments…** zapisuje model jako IFC. Każda ściana, strop, drzwi i dach to prawdziwy element IFC z warstwami, typem i właściwościami, więc plik otwiera się jako model w innych narzędziach BIM. Drzwi i okna mówią, jak się otwierają: stronność każdego skrzydła, kierunek otwierania i panele, tak jak definiuje je IFC, więc inne programy rysują te same otwarcia.

Eksport pyta o schemat (**IFC4**, czytany wszędzie, lub **IFC4X3**), szczegółowość (części jako osobne elementy lub jedna bryła na element), fazę i sposób zapisu brył. **Geometria** to **fasetowy B-rep** (dokładny, najczęściej oczekiwany przez inne programy) lub **zbiory powierzchni**: te same powierzchnie w znacznie mniejszym pliku. **Uprość** zapisuje mniej trójkątów tam, gdzie bryła jest gęstą siatką, jak kanały i rury z pliku innego programu, nigdy dalej od niej niż wpisana **tolerancja** (na początek 2 mm). Bryły, które Lapin buduje sam (ściany, stropy, drzwi), zachowują dokładne powierzchnie, a upraszczanie zmienia tylko kształt bryły, nigdy jej właściwości.

![Eksport IFC](shots/export-ifc.webp)

Belki, słupy i profile stalowe zapisywane są przez profil parametryczny, z nazwą i wartościami mechanicznymi ([Biblioteki profili](profiles.md)).

**Importuj IFC…** wczytuje IFC z innego programu. Zanim cokolwiek zostanie wprowadzone, Lapin czyta plik i pokazuje, co zawiera: elementy według branż (architektura, konstrukcja, instalacje, meble i wyposażenie, teren), każdą branżę według rodzajów z ich liczbą oraz kondygnacje z liczbą elementów na każdej. Odznacz to, czego nie potrzebujesz, na przykład instalacje i meble, gdy pracujesz nad konstrukcją, albo zostaw tylko kondygnacje, nad którymi pracujesz. **Jak** wprowadza elementy rozpoznane jako elementy Lapina, wszystkie jako odniesienia lub jako połączone. Podczas czytania i importu pliku Lapin pokazuje, co robi i jak daleko zaszedł; duży plik wymaga chwili.

![Import IFC](shots/import-ifc.webp)

Niektóre pliki oprócz modelu zawierają 2D: symbole na rzucie, obrysy, kreskowania i teksty narysowane dla elementu albo elementy narysowane tylko w 2D (linie, teksty, symbole bez bryły). Gdy plik je ma, okno dialogowe liczy je w sekcji **Narysowane w 2D**. Odznacz **własne 2D elementów**, a rzut narysuje te elementy według ich przekroju, tak jak elementy Lapina; odznacz **elementy narysowane tylko w 2D**, a zostaną pominięte. Oba są wprowadzane, chyba że je odznaczysz.

![Narysowane w 2D](shots/import-ifc-2d.webp)

**Czyszczenie i optymalizacja** zawiera reguły importu, takie same dla każdego pliku, zapamiętywane jako twoje na następny import. **Zachowaj powtarzaną bryłę raz** (włączone, dopóki tego nie odznaczysz) zachowuje raz bryłę, którą plik powtarza pod innymi nazwami (śrubę, kołnierz, wspornik), a jego elementy ją współdzielą, bez żadnej straty. Pozostałe, gdy są zaznaczone, pomijają to, co przyniósł ze sobą inny program: **płaskie bryły** (teksty, linie i kreskowania zamienione na trójkąty), **elementy dalsze od reszty** niż wpisana odległość (teksty rysunku, tabliczka rysunkowa), **duplikaty** w tym samym miejscu oraz **porty i otwory zapisane jako obiekty**. **Uprość siatki** zapisuje mniej trójkątów w granicach wpisanej tolerancji; **czytaj liczby zapisane jako tekst** zamienia `DoubleLength:0,01` na 0.01 m, a `1.15395 kg` na masę (sama liczba całkowita, kod, pozostaje tekstem). W sekcji **Co przenieść** pole **Zestawy właściwości** wymienia zestawy z pliku wraz z liczbą elementów, które je mają; **techniczne** (transformacja Navisworksa, wewnętrzne identyfikatory, statystyki geometrii) są pomijane, chyba że je zaznaczysz. Komunikat importu mówi, co zrobiła każda reguła.

![Porządkowanie i optymalizacja](shots/import-ifc-rules.webp)

To, co umieszcza plik, wchodzi jako jedna grupa nazwana od niego, którą możesz otworzyć i uporządkować, albo luzem, gdy odznaczysz **Jako jedna grupa** (zobacz [Grupy i porządkowanie](groups.md)).

**Kondygnacje.** Kondygnacje importu są kondygnacjami pliku: zastępują kondygnacje twojego projektu. Każda kondygnacja pliku wchodzi ze swoją nazwą, swoim GlobalId i swoim poziomem, tak jak podaje go plik, odczytanym w jednostce pliku (milimetrach lub metrach). Kondygnacja twojego projektu na poziomie jednej z kondygnacji pliku (z dokładnością do 5 cm) zostaje z nią scalona: jej obiekty zostają tam, gdzie są, a jej widoki, jej rzut i to, co na nim narysowano, przechodzą na kondygnację pliku, która zachowuje nazwę z pliku. Twoja kondygnacja, która nie leży na żadnym poziomie pliku, zostaje usunięta, gdy nic nie zawiera (żadnego obiektu, nic narysowanego na jej rzucie), a jej widoki przechodzą na najbliższą kondygnację pliku; taka, która coś zawiera, zostaje, a komunikat importu mówi, która. Tak Fundamenty, Parter i Pierwsze piętro nowego projektu ustępują miejsca kondygnacjom pliku. Całość jest jednym krokiem cofania razem z importem. Plik połączony zachowuje swoje kondygnacje dla siebie i nie zmienia żadnej z twoich. Po zakończeniu importu widok 3D kadruje model, chyba że w trakcie przesunąłeś kamerę.

**Wszystko edytowalne**, jedna z opcji pola **Jak**, idzie dalej niż rozpoznawanie: to, co pozostaje odniesieniem, staje się bryłą Studio z własnych trójkątów, tym samym elementem z klasą, właściwościami, kondygnacją i grupą, który od razu przyjmuje wypychanie/wciąganie, zaokrąglenia i operacje logiczne (zobacz [Studio](studio.md)). Bryła z więcej niż 5000 trójkątów, jak gęsto zasiatkowana pompa lub kształtka, pozostaje odniesieniem, a komunikat importu mówi, ile ich jest i dlaczego. **Importuj Fragments…** ma to samo pole **Jak**: plik Fragments zawiera tylko trójkąty, a jednak jego ściany (z drzwiami i oknami), stropy i słupy są z nich rozpoznawane jako elementy Lapina, reszta zostaje odniesieniami lub, z opcją **Wszystko edytowalne**, staje się bryłami Studio. Elementy pliku IFC, których rozpoznawanie nie mogło odczytać z pliku (ściana z otworami wyciętymi w bryle), są również próbowane na podstawie trójkątów. **Importuj siatkę** pyta o **Jako: edytowalne**. Wcześniej wprowadzone odniesienia konwertuje się poleceniem Modyfikuj → **Konwertuj na natywne** (NV): rozpoznane tam, gdzie się da, reszta zamieniona na bryły Studio, a w przypadku zaznaczonej grupy jej członkowie. Wcześniej wprowadzone odniesienie staje się edytowalne pojedynczo poleceniem Studio → **Utwórz bryłę Studio**, które zachowuje je w jego grupie i na jego kondygnacji.

Lapin rozpoznaje ściany, stropy, słupy, belki, fundamenty, drzwi i okna i zamienia je na elementy Lapina, które możesz edytować. To, czego nie umie rozpoznać, wchodzi jako odniesienie, narysowane tak, jak podaje plik: jego kształtu nie da się edytować, ale przesuwa się, obraca, odbija, kopiuje i powiela jak każdy element. Element staje się elementem Lapin tylko wtedy, gdy jest dokładnie tym z pliku: Lapin mierzy każdy rozpoznany element względem bryły, którą daje mu plik, a taki, który różni się o więcej niż 1 mm lub 0,5 % objętości (słup, którego wierzch plik pochyla, schodkowy koniec ściany, które element Lapin wyprostowałby), wchodzi jako odniesienie, którym był; komunikat importu mówi, ile i dlaczego. Drzwi i okna wchodzą otwierane tak, jak podaje plik: ich stronność, strona, na którą się otwierają, a w przypadku okien sposób otwierania każdego panelu. Dach płaski z innego programu wchodzi jako dach Lapina, gdy jego IfcRoof składa się z dwóch lub więcej płyt ułożonych jedna na drugiej albo z jednej bryły z zestawem warstw: płyta (lub warstwa) na górze to pokrycie, z jego materiałem i grubością, te pod nią to płyta dachu, z materiału najgrubszej; obrys jest obrysem płyty, bez okapu, na jej spodzie. Dach spadzisty albo dach z jednej płyty bez warstw zostaje, jak przyszedł. Rozpoznane elementy dostają tylko to, co ma plik: bez nadproży, parapetów, progów ani ościeży Lapin, a każdy otwór ma rozmiar z pliku; włącz je w typach elementów, jeśli ich chcesz.

Wykończenia, które inny program wyeksportował jako osobne ściany (tynk 2,5 cm na elewacji, tynk wewnętrzny), stają się warstwą ściany, którą pokrywają, gdy przylegają do jej lica, są tak wysokie jak ona i biegną wzdłuż niej: ściana dostaje typ z tą warstwą (*Mur 350 + Tynk wapienny*), jej własne warstwy zostają na miejscu, a komunikat importu mówi, ile ich było. Wykończenie odsunięte od ściany lub pokrywające tylko jej część pozostaje cienką ścianą. Słup, który plik podaje jako bryłę ściętą u góry przez spadek (pod pochyłą belką lub dachem), wchodzi jako słup o swoim przekroju, aż do najwyższego punktu.

**Mapuj typy importowane** (Zarządzaj) wymienia typy utworzone przez import (typy ścian, stropów, drzwi, okien, słupów, belek i fundamentów), każdy z plikiem i liczbą elementów. Wybierz w polu **Staje się** jeden z typów projektu tego samego rodzaju i naciśnij **Mapuj**: jego elementy przyjmują typ projektu, ściana zachowuje rdzeń tam, gdzie był, a zaimportowany typ znika. Gdy zaznaczysz **Zapamiętaj**, późniejszy import typu tego rodzaju i o tej nazwie od razu przyjmuje typ projektu, a jego komunikat to mówi. Zapamiętane wybory są wymienione w oknie dialogowym, każdy z przyciskiem **Zapomnij**.

![Mapuj zaimportowane typy](shots/map-types.webp)

Przestrzenie z pliku (z Revita, ArchiCAD-a i innych) wchodzą jako pomieszczenia Lapina, każde na swoim poziomie, z obrysem, numerem, nazwą, wysokością, funkcją, działem i wykończeniem podłogi: zobacz [Pomieszczenia](rooms.md#rooms-from-an-ifc-file). Wszystko, co zaimportowany element miał w pliku (zestawy właściwości i ilości), jest zachowane i pokazane w sekcji **Właściwości IFC** w jego właściwościach. Odniesienie opuszcza **Eksportuj IFC…** takie, jakie weszło: z tym samym GlobalId, klasą i atrybutami (opis, znacznik, typ predefiniowany), zestawami właściwości i ilościami z każdą wartością zapisaną tak, jak miał ją plik, oraz typem, materiałem, portami, połączeniami i systemem, każdy z własnym GlobalId. Wprowadź z pliku tylko instalacje i wyeksportuj je, a nowy plik przeniesie je w całości.

**Importuj Fragments…** otwiera ten sam panel dla pliku That Open `.frag`: jego elementy według branż i rodzajów, jego kondygnacje oraz **Jako jedna grupa**. Plik wyeksportowany z tego samego projektu nie wnosi niczego nowego, gdy zaimportujesz go z powrotem: jego elementy już tu są.

![Import Fragments](shots/import-frag.webp)

**Importuj Fragments…** wprowadza plik `.frag`: każdy obiekt z geometrią staje się odniesieniem rysowanym z jego bryły, w klasie podanej przez plik, na kondygnacji, na której umieszcza go plik (kondygnacje pliku zastępują twoje: zobacz **Kondygnacje** wyżej), z atrybutami pliku w sekcji **Właściwości IFC**. Obiekty o tej samej bryle (ten sam kształt, umieszczony w wielu miejscach) ją współdzielą. Ponowny import tego samego pliku aktualizuje to, co wprowadził wcześniej. To, co zostaje rozpoznane z jego trójkątów, zależy od pola **Jak**, jak powyżej. Jego zestawy właściwości i ilości, typ i materiały przychodzą razem z atrybutami; szkło pozostaje przezroczyste; plik zrobiony przez konwerter That Open trafia tam, gdzie umieściłby go jego IFC, a jego ściany z otworami (ściana wokół okna) je zachowują.

**Eksportuj IFC lub Fragments…** zapisuje model jako IFC lub jako Fragments, wybierany tak jak przy imporcie: elementy według branż i rodzajów, kondygnacje, faza i tylko to, co pokazuje widok 3D. IFC wychodzi w IFC4 lub IFC4X3, z częściami elementu warstwowego pod nim lub z jedną bryłą na każdy; Fragments od punktu bazowego projektu, początku wewnętrznego lub punktu pomiarowego. Plik Fragments niesie to, co pokazuje przeglądarka IFC: typ każdego elementu, jego ilości, Twoje parametry, materiały i współrzędne mapy, tak jak zapisuje je konwerter That Open.

![Eksportuj IFC lub Fragments](shots/export-exchange.webp)

### SDNF (detalowanie konstrukcji stalowych)

SDNF, Steel Detailing Neutral File, to format tekstowy, w którym programy do detalowania konstrukcji stalowych wymieniają konstrukcję (Tekla Structures, Advance Steel, SDS2, ProStructures, E3D): każdy pręt to linia z profilem, gatunkiem stali, punktem przekroju, przez który biegnie linia, i obrotem; blachy; osie. Lapin zapisuje wersję 3.0 i czyta 3.0 oraz 2.0, w wydaniach z modułem stali.

**Eksportuj IFC, Fragments lub BOS…** z zaznaczonym **SDNF** zapisuje słupy i belki (z dowolnego materiału) oraz pręty stalowe z wybranych kondygnacji i rodzajów, blachy ich połączeń i osie, w milimetrach. Każdy pręt zachowuje nazwę profilu (IPE 300, HEA 240, profil z Twojej biblioteki), gatunek i numer związany z jego id, aby plik mógł wrócić. Belki zakrzywione wychodzą jako pręty zakrzywione, a otwory blach i prętów jako otwory, w tym otwory na śruby i kotwy połączeń.

![Eksportuj SDNF](shots/export-sdnf.webp)

**Importuj SDNF…** wczytuje plik jako własne elementy Lapina, w jednym kroku, który można cofnąć: pionowy słup jako słup, poziomą belkę jako belkę, wszystko inne (stężenie, krokiew, pręt ukośny) jako pręt stalowy, każdy na swojej kondygnacji, z profilem z profili projektu i biblioteki Lapina; wchodzą też blachy i osie. Profil, którego nie ma żadna biblioteka (W12X50, gdy nie masz tabeli AISC), zachowuje nazwę na przekroju zastępczym i jest wymieniony, dopóki nie nadasz jego typowi profilu. Plik zapisany przez Lapina albo ten sam plik ponownie, po zmianach detalisty, aktualizuje elementy, z których powstał, zamiast dodawać nowe. Poziomy pręt zakrzywiony wchodzi jako belka zakrzywiona (pręt wygięty w innej płaszczyźnie jest pomijany i zgłaszany); otwory są wycinane w blachach i prętach, przez które przechodzą, i zastępowane, a nie podwajane, gdy plik wraca. SDNF nie przenosi śrub, więc przychodzą tylko ich otwory; zwolnienia na końcach i obciążenia nie są wczytywane.

![Hala wczytana z powrotem z SDNF](shots/import-sdnf-3d.webp)

## JG Structures (obliczenia konstrukcji)

**Eksportuj JG Structures (JSON)…** zapisuje model obliczeniowy projektu w pliku wymiany JG Structures, programu do obliczeń konstrukcji: plik JSON zgodny z jego kontraktem (wersja 1.0.0). Lapin wyprowadza model z tego, co narysowano. Słupy, belki i pręty stalowe stają się prętami na swoich osiach; ściany nośne (Pset_WallCommon.LoadBearing na ścianie lub jej typie) i stropy stają się powłokami w środku swojej warstwy nośnej, bez wykończeń. Mają wspólne węzły: belka niosąca strop leży w płaszczyźnie środkowej stropu, końce słupów i ścian dochodzą do belki lub stropu u góry i u dołu, narożniki stropu przechodzą na słupy i ściany pod nim, a strop dzieli się wzdłuż belek, które go przecinają. Podstawy słupów i ścian na fundamencie są utwierdzone; podstawa słupa stalowego jest przegubowa lub utwierdzona, jak mówi jej połączenie.

Wybierz kondygnacje, rodzaje elementów i jednostki: centymetry i kilogramy-siły kontraktu albo SI (metry i kiloniutony). Materiały biorą wartości ze swoich danych (Zarządzaj → Materiały, Pset_MaterialMechanical w paskalach) lub z klasy wytrzymałości w nazwie, C25/30 lub S355, według Eurokodów; pole, momenty bezwładności, wskaźniki wytrzymałości i stała skręcania przekroju są liczone z jego geometrii. To, czego Lapin nie wie, zostaje w pliku puste (null), nigdy zero. Przed eksportem raport liczy, co wchodzi, sprawdza plik według reguł kontraktu i wymienia, czego jeszcze brakuje do obliczeń: materiału bez klasy, słupa, który na niczym nie stoi.

Tam, gdzie elementy nie spotykają się na swoich osiach (płatew na ryglu ramy, rygiel ścienny na słupie, stężenie na swojej blasze węzłowej, krokwie na murłacie), Lapin umieszcza węzeł na elemencie podpierającym i zachowuje odległość jako mimośród na końcu pręta, więc także konstrukcja drugorzędna hali jest połączona. Ściany zachowują drzwi, okna i otwory, a stropy swoje otwory: ponieważ powłoki kontraktu nie mają otworów, dzieli się je na części wokół nich, a strop w kształcie L na części wypukłe wzdłuż belek, które go podpierają. Ściana pod dachem sięga do niego, a dach też jest eksportowany: jego płyta jako powłoki albo krokwie, płatwie i murłaty więźby drewnianej jako pręty jej klasy drewna (C24 według EN 338). Kątowniki i inne przekroje niesymetryczne są podawane w ich osiach głównych. **Podpory na fundamentach** określa, jak są podparte stopy słupów i ścian stojących na fundamencie: utwierdzone (domyślnie) lub przegubowe. Podstawa słupa stalowego zachowuje swoje własne połączenie.

![Eksportuj JG Structures](shots/export-jg.webp)

## Optymalizacja pliku IFC

**Optymalizuj IFC…** (w menu Plik lub na ekranie startowym: żaden projekt nie musi być otwarty) tworzy lżejszą kopię jednego lub kilku plików IFC, nie zmieniając tego, co mówią. Kopia zachowuje każdy GlobalId, nazwę, zestaw właściwości, ilość, typ, materiał i relację dokładnie tak, jak zapisał je plik, a także jego nagłówek; nic od Lapina nie jest dodawane. Zmienia się tylko geometria: siatki (zestawy ścian, fasetowe B-repy z płaskich ścian) są oczyszczane (zduplikowane wierzchołki zespawane, zdegenerowane i powtórzone trójkąty oraz nieużywane współrzędne usunięte) i upraszczane, nigdy dalej od oryginału niż **Precyzja** (Szybko 5 mm, Zrównoważenie 1 mm, Dokładnie 0,1 mm lub własna), mierzona w obie strony; identyczne bryły są zapisywane raz i współdzielone przez swoje produkty; a to, do czego nic się nie odwołuje (nieużywane punkty, style, puste reprezentacje), znika. Wyciągnięcia, operacje logiczne i inne dokładne bryły pozostają bez zmian. Zaokrąglanie współrzędnych, B-repy zapisane jako zestawy ścian (tylko IFC4 i IFC4X3) i spakowany `.ifczip` są dostępne, gdy ich chcesz.

![Optymalizuj IFC](shots/optimize-ifc.webp)

Kopia jest zapisywana obok oryginału lub w wybranym folderze, pod nazwą oryginału z **Końcówką** (na początek `.optimized`): z `dom.ifc` powstaje `dom.optimized.ifc`. Oryginał nigdy nie jest zapisywany, a gdy kopia już istnieje, Lapin pyta przed jej zastąpieniem. Dla każdego pliku raport podaje rozmiar, encje i trójkąty przed i po, współdzielone bryły, usunięte osierocone, największe zmierzone odchylenie i czas trwania; **Otwórz folder** pokazuje kopię, a **Otwórz w Lapin** otwiera ją w innym oknie. W przeglądarce kopia jest pobierana. Plik już wprowadzony do projektu zachowuje drogę powrotną: **Optymalizuj plik źródłowy…** na grupie zaimportowanego IFC (w jej właściwościach) lub na łączu (Zarządzaj → Łącza) robi to samo z plikiem, z którego pochodzi, dopóki jest on jeszcze na dysku.

![Raport](shots/optimize-ifc-report.webp)

## DXF i DWG

**Importuj DXF…** wprowadza rysunek CAD do widoku na pierwszym planie, z warstwami, kreskowaniami, tekstami i wymiarami. Wchodzi jako rysunek, do którego możesz się przyciągać i który możesz edytować. Aby użyć pliku DWG, najpierw zapisz go jako DXF w swoim programie CAD (AutoCAD, BricsCAD lub DraftSight: **Zapisz jako → DXF**). PDF lub obraz trafia do odrysowania przez **Plik → Importuj PDF lub obraz…** albo **Rysunek → PDF lub obraz**: linie wektorowego PDF jako rysunek w skali, w jakiej go narysowano, jego obrazy oraz każdy PNG lub JPEG jako półprzezroczyste obrazy (zob. [Rysunek](drafting.md)). Na rzucie opcja **Umieszczony: według współrzędnych mapy** kładzie rysunek w rzeczywistych współrzędnych (plan katastralny w Stereo 70) tam, gdzie umieszcza go mapa (zobacz [Teren](site.md)).

Rysunek leży też w **widoku 3D**, na płaszczyźnie widoku, do którego trafił: na rzucie na poziomie jego kondygnacji, na przekroju lub elewacji na płaszczyźnie tego widoku, w kolorach swoich warstw, z bladymi kreskowaniami, jako cienkie linie, które nigdy nie zasłaniają modelu. Warstwa wyłączona w **Rysunek → Warstwy** pozostaje wyłączona w 3D. **Widok → DXF w 3D** (także w **Widok → Ustawienia widoku** widoku 3D) pokazuje lub ukrywa wszystkie importy; pojedynczy import przełącza się polem **W 3D** we właściwościach dowolnego z jego kształtów, połączony DXF polem **3D** w **Zarządzaj → Łącza**. Rysując w 3D na tym poziomie, narzędziami Rysunku lub Szkicem Studio, przyciągasz do jego linii, więc możesz go odrysować w 3D jak na rzucie. Linie, którym plik nadał wysokości, stoją w przestrzeni na tych wysokościach, jak dotąd.

![DXF zagospodarowania w widoku 3D, na poziomie parteru](shots/dxf-3d.webp)

Zaimportowany DXF jest częścią projektu: **File → Save** zapisuje go w pliku `.lapin` z warstwami, kreskowaniami, tekstami i każdą grupą, którą z niego utworzono, a po ponownym otwarciu projektu jest na miejscu, na rzucie kondygnacji. Widok rzutu, do którego trafił, jest odtąd rzutem kondygnacji, tym, który pokazuje przeglądarka projektu, a to, co wcześniej narysowano na kondygnacji, przechodzi na niego. Lapin pyta o zapis przed zamknięciem, gdy są niezapisane zmiany. Aby rysunek pozostał osobnym plikiem, zaznacz **As a link** w oknie importu: pozostaje połączony, tylko do odczytu, a **Manage → Links** wczytuje go ponownie, gdy plik się zmieni.

## Łącza

**Połącz IFC…** utrzymuje inny plik jako połączony zamiast go importować. Jest tylko do odczytu i jest ponownie wczytywany, gdy się zmieni. Działa to także dla DXF. **Zarządzaj → Łącza** pokazuje, ponownie wczytuje, konwertuje lub usuwa łącza. Połączony DXF leży w 3D tak jak zaimportowany; jego pole **3D** w **Zarządzaj → Łącza** to wyłącza. **Widoczność / Grafika** (<kbd>VV</kbd>) ukrywa łącze, wyszarza je lub daje mu własne kategorie tylko w jednym widoku (*Przekroje, elewacje i 3D*, Widoczność / Grafika).

Podłączony IFC zachowuje to, co jego plik mówi o materiałach elementów. Ściany, stropy i dachy z zestawami warstw rysują się w swoich warstwach nawet jako odniesienia, więc inżynier ukrywa wykończenia połączenia architektury (**Ściany ▸ Wykończenia**, **Stropy ▸ Wykończenia**), a styl widoku konstrukcji rysuje tylko ich rdzenie; pręty podłączonego modelu konstrukcji należą do kategorii **Zbrojenie**, którą każdy widok 3D pomija, dopóki jej nie pokażesz. Połączenie utworzone starszą wersją otrzymuje warstwy ścian po ponownym wczytaniu (stropów od razu).

## Chmury punktów

**File → Link point cloud…** łączy skan laserowy lub chmurę z fotogrametrii, tylko w formatach otwartych: **LAS** (1.0–1.4, wszystkie formaty punktów), **LAZ** (skompresowany LAS), **E57**, **PLY** (ASCII i binarny) oraz punkty jako tekst, **XYZ**, **PTS** i **CSV**. Formaty zamknięte (RCP i RCS z Autodesk ReCap, FLS z FARO, LGS z Leica) nie są czytane: najpierw wyeksportuj skan ze swojego programu jako E57 lub LAS.

Lapin pyta, gdzie stoi chmura i w jakiej jednostce:

- **według współrzędnych mapy projektu**: x i y pliku to współrzędne wschodnia i północna, umieszczone przez punkt pomiarowy i kąt do północy z **Manage → Location**, ich wysokości nad rzędną punktu pomiarowego. Lapin proponuje to, gdy plik podaje układ współrzędnych lub jego współrzędne są daleko od zera, a projekt ma lokalizację;
- **początek na początek**: początek pliku na początku projektu;
- **wyśrodkowana na początku projektu**: jej środek na początku, jej najniższy punkt na ±0.00;
- **jej jednostka**: jak podaje plik (LAS i E57 ją podają), inaczej metry; dostępne są centymetry, milimetry, stopy, stopy geodezyjne USA i cale. Gdy chmura wychodzi nieprawdopodobna dla budynku lub terenu (poniżej 20 cm lub ponad 50 km) albo plik podaje inną jednostkę, Lapin ostrzega.

Chmura zostaje w swoim pliku. Nie jest kopiowana do projektu: projekt przechowuje tylko połączenie (ścieżkę pliku, gdzie stoi chmura, jak jest rysowana). Lapin indeksuje ją raz, w tle, w octree kafli we własnym folderze na tym komputerze (nie obok projektu), a **Manage → Links** pokazuje postęp. Potem widok 3D pobiera kafle potrzebne kamerze, najpierw bliskie i coraz dokładniejsze w miarę zbliżania się, w budżecie punktów wyznaczonym przez **View → Display quality** (1,5, 3 lub 6 milionów punktów rysowanych naraz), więc skan stu milionów punktów obraca się tak płynnie jak mały.

![Skan domu połączony jako chmura punktów, w widoku 3D](shots/point-cloud-3d.webp)

W **Manage → Links** chmura ma własny wiersz: jej punkty, postęp indeksu, **Shown**, **Halftone**, **3D**, co koloruje jej punkty (**RGB**, **Intensity**, **Elevation** lub **Classification**, według klas ASPRS: grunt, roślinność, budynek, woda…) i **Size** punktu w pikselach. Przycisk pinezki umieszcza ją ponownie (według mapy, początek na początek lub wyśrodkowana, jej jednostka, przesunięta wzdłuż x, y i z, obrócona); przycisk przeładowania czyta plik ponownie i indeksuje go od nowa tylko wtedy, gdy się zmienił. **Unlink** usuwa połączenie; indeks zostaje w folderze Lapina i znowu służy chmurze, jeśli zostanie ponownie połączona.

![Manage → Links z chmurą punktów](shots/point-cloud-links.webp)

Chmura nie jest eksportowana z modelem: IFC i Fragments niosą model, nie skan, a **Export 3D…** bierze jej próbkę jako punkty w GLB i glTF tylko na życzenie. Rzuty, przekroje i elewacje też ją rysują (*Przekroje, elewacje i 3D*, Chmury punktów w widokach). W przeglądarce można połączyć chmurę do 160 MB; jest indeksowana na stronie i trzeba ją połączyć ponownie po przeładowaniu strony. Większe chmury wymagają aplikacji desktopowej.

## Modele 3D

**Eksportuj 3D (glTF, USD, DAE)…** zapisuje model jako powierzchnie do renderingu, wizualizacji i rzeczywistości rozszerzonej (AR):

| Format | Do czego |
|---|---|
| GLB, glTF | Blender, three.js, przeglądarki internetowe. Obrazy twoich materiałów są dołączone. |
| USDZ | AR Quick Look Apple, Reality Composer, Omniverse. Obrazy dołączone. |
| USDA | OpenUSD jako tekst, jeden plik. |
| DAE | COLLADA, dla SketchUpa i starszych narzędzi. |
| OBJ + MTL | Plik .zip dla Twinmotion, Lumion, 3ds Max i dowolnego programu do renderingu. Z obrazami. |

**Importuj siatkę (OBJ, STL, glTF)…** wprowadza siatkę z innego programu 3D (OBJ, STL, glTF lub GLB) jako odniesienia na aktywnym poziomie. glTF podaje swoją jednostkę i kierunek do góry; dla OBJ i STL Lapin o to pyta. Mebel lub dekoracja wprowadzone w ten sposób są umieszczane jak każdy element: **Przesuń**, **Kopiuj**, **Obróć**, **Lustro** i **Szyk** w Modyfikuj, **Przesuń 3D** w Studio dla jego wysokości. Jego kopie dzielą jego trójkąty, więc rząd krzeseł waży tyle, co jedno. Polecenie Studio **Utwórz bryłę Studio** czyni kształt odniesienia edytowalnym (zobacz [Studio](studio.md)).

![Sześć krzeseł z pliku OBJ wokół stołu, zrobionych przez Modyfikuj → Szyk](shots/reference-array.webp)

![Eksport 3D](shots/export-3d.webp)

Wybierasz, co eksportować, jaką fazę i początek układu. **Co** to **wszystko** albo model **tak, jak pokazuje go widok**: widok 3D na pierwszym planie, bez ukrytych elementów i kategorii oraz kondygnacji, których nie pokazuje, z warstwami, które pokazuje (bez wykończeń lub tylko rdzenie). Gdy ten widok jest przecięty (cięcie nad pokazanymi kondygnacjami, przekrój pokazany w 3D, przekrój prostopadłościenny), **Cięcie** przecina tam także eksport i zamyka każde cięcie w jego materiałach, więc plik wygląda jak ekran. Widok 3D, który coś pomija, jest eksportowany tak, jak to pokazuje, chyba że wybierzesz inaczej. Każdy element zachowuje swoją klasę IFC, typ i GlobalId. Aby wymienić sam model, a nie jego powierzchnie, użyj **Eksportuj IFC**.

## Lapin dla przeglądarki

**Plik → Lapin dla przeglądarki (.html)…** zapisuje projekt jako stronę internetową, jeden plik .html, który możesz wysłać komukolwiek. Otwiera się dwukrotnym kliknięciem w dowolnej przeglądarce (Chrome, Edge, Safari, Firefox), na dowolnym komputerze, bez internetu i bez niczego do instalowania:

- **3D**: model tak, jak pokazuje go widok 3D. Przeciągnij, aby go obrócić, przeciągnij prawym przyciskiem, aby przesunąć, kółko przybliża; **Dopasuj** pokazuje całość. Wybierz kondygnację samą lub razem z tymi poniżej; kliknij element, aby go zaznaczyć (Shift dodaje do zaznaczenia), a potem **Izoluj** go lub **Ukryj**; **Pokaż wszystko** przywraca wszystko.
- **Rysunki**: rzuty, przekroje, elewacje i arkusze, które zaznaczyłeś, narysowane tak, jak drukuje je Lapin, ostre przy każdym powiększeniu. Przeciągnij, aby przesunąć, kółko przybliża, **Drukuj** drukuje pokazany rysunek. Kliknij element na rysunku, aby go zaznaczyć: to ten sam element co w 3D.
- **Kosztorys**, gdy projekt go ma: jego rozdziały i pozycje, sumy, oznaczone jako *orientacyjne*, gdy używa własnych, przybliżonych wartości Lapin. Kliknij nagłówek kolumny, aby sortować, wpisz w **Filtr**, aby znaleźć pozycję, kliknij pozycję, aby zaznaczyć jej elementy.
- **Właściwości**: to, co jest zaznaczone, z jego klasą, typem, kondygnacją, materiałem, właściwościami i ilościami.

![Plik → Lapin dla przeglądarki](shots/html-export-dialog.webp)

W oknie wybierasz, które rysunki wchodzą (domyślnie każdy, który pokazuje element modelu; rzut z samymi osiami lub oznaczeniami jest na liście niezaznaczony, z dopiskiem *(brak elementów)*, i wchodzi, jeśli go zaznaczysz), model 3D, kosztorys i **sam projekt**. Strona pokazuje swój rozmiar, zanim zostanie zapisana. Strona mówi w języku, w którym mówi Lapin podczas eksportu.

![Strona w przeglądarce: model 3D](shots/html-export-3d.webp)

![Rzut na stronie, klikniętą ścianę, która pokazuje swoje właściwości](shots/html-export-drawing.webp)

![Kosztorys na stronie](shots/html-export-costs.webp)

Z projektem w środku strona jest też projektem: **Plik → Otwórz** w Lapin bierze plik .html i otwiera zawarty w nim projekt, niezapisany; **Zapisz** pyta, gdzie go zachować jako .lapin, i nigdy nie zapisuje na stronie. Bez projektu strona pokazuje tylko model, a Lapin jej nie otwiera. Strona internetowa, której Lapin nie napisał, jest odrzucana.

Strona jest migawką: pokazuje projekt z chwili eksportu, a nic, co czytelnik w niej zrobi, nie zmienia projektu. Jest tak duża jak jej części: projekt, model 3D (mniej więcej jak eksport GLB) i rysunki, plus około 0,6 MB na samą przeglądarkę.
