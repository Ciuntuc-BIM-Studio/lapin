---
title: Teren i jego dane
order: 52
summary: Wprowadź pomiar, działkę, sąsiednie budynki i ortofotomapę w rzeczywistych współrzędnych, z plików DXF, CSV, GeoJSON, CityJSON i z georeferencyjnego obrazu.
---

Karta **Zagospodarowanie** obejmuje grunt, na którym stoi budynek: teren, działkę, sąsiednie budynki i zdjęcie lotnicze, każde tam, gdzie naprawdę się znajduje. Grupa **Importuj** wczytuje je z plików przekazanych przez geodetów i ewidencję gruntów.

![Karta Zagospodarowanie](shots/ribbon-site.webp)

## Rzeczywiste współrzędne

Pomiar przychodzi we współrzędnych mapy: wschodnich i północnych, setki kilometrów od zera. Lapin trzyma model blisko jego początku układu i wiąże go z mapą przez **punkt geodezyjny**. Pierwszy import do projektu robi to za ciebie: punkt geodezyjny trafia do początku układu rzutu, na pierwsze współrzędne pliku zaokrąglone do metra, a każdy kolejny import ląduje w tym samym miejscu.

**Zarządzaj → Lokalizacja** pokazuje **układ współrzędnych**, w którym podane są współrzędne wschodnie i północne, oraz punkt bazowy i punkt geodezyjny. Lapin zna Stereo 70 (EPSG:3844, państwowy układ Rumunii), strefy UTM 34 i 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) oraz długość i szerokość geograficzną (WGS 84, EPSG:4326). Plik w innym z tych układów jest przeliczany przy wczytaniu: GeoJSON w długości i szerokości geograficznej ląduje na rzucie w Stereo 70 tam, gdzie jego miejsce. Eksport IFC zapisuje układ i punkt geodezyjny, więc inne programy też umieszczają model na mapie. Znany jest także Lambert-93 (EPSG:2154, krajowy układ Francji).

![Zarządzaj → Lokalizacja: układ współrzędnych i punkt geodezyjny](shots/site-location.webp)

## Import

- **Teren z punktów** wczytuje plik CSV lub tekstowy z pomiaru: x y z albo numer, współrzędna północna, wschodnia, rzędna, jeden punkt w wierszu.
- **Punkty z DXF** wczytuje rysunek z pomiaru: obiekty POINT z ich wysokościami, bloki wstawione na wysokości lub niosące ją jako atrybut, rzędne zapisane obok punktów („245.37” obok krzyżyka) oraz warstwice na ich wysokości. Gdy rysunek ma punkty na kilku warstwach, Lapin wypisuje je z liczbą punktów na każdej, a ty wybierasz, które wziąć. Z punktów powstaje teren. Plik DXF nie podaje układu, więc Lapin rozpoznaje liczby wielkości mapy po ich zakresie (Stereo 70, Lambert-93, UTM). Liczby układu, którego nie zna, zostają takie, jakie są, i Lapin to mówi: nazwij układ w **Zarządzaj → Lokalizacja** albo najpierw przekonwertuj rysunek.
- **GeoJSON** wczytuje działki, linie i punkty. Plik z jednym wielobokiem daje **działkę**; plik z kilkoma daje obszary. Działka wyznacza też obrys działki na terenie, z jej powierzchnią. Pliki z ewidencji gruntów w Stereo 70 podają to w pliku, a Lapin to odczytuje; plik, który nic nie mówi, jest w długości i szerokości geograficznej, jak wymaga standard GeoJSON.
- **CityJSON** wczytuje sąsiednie budynki z ich powierzchniami, na ich miejscu i wysokości. Są to odniesienia tylko do odczytu; będą rzucać cienie w analizie nasłonecznienia.
- **Ortofotomapa** kładzie zdjęcie lotnicze pod terenem. Wybierz obraz (JPEG lub PNG, do 8 MB) razem z jego plikiem georeferencji (`.jgw`, `.pgw` lub `.wld`): plik georeferencji mówi, gdzie leży obraz. Zdjęcie jest rysowane pod rzutem, a w 3D okrywa teren.

![Plan zagospodarowania: ortofotomapa, warstwice terenu z pomiaru, działka i obrys budynku sąsiada](shots/site-plan.webp)

![Ten sam teren w 3D: zdjęcie na gruncie, budynek sąsiada na swojej wysokości](shots/site-3d.webp)

Wszystko, co zaimportowane, jest przechowywane w pliku projektu. Obiekt bierze nazwę z pliku (numer ewidencyjny działki, gdy plik go ma) i zachowuje pozostałe właściwości z pliku; zaznacz go, aby zobaczyć jego powierzchnię lub długość.

## Drogi, łuki i linie

**Droga** lub **ścieżka** biegnie wzdłuż swojej osi. W oknie **Korytarz drogowy…** (CR) karta **Łuki** nadaje każdemu wierzchołkowi osi promień z klotoidą na wejściu i wyjściu, tak jak projektant dróg wytycza łuk; wierzchołek z wartością 0 pozostaje narożnikiem, a łuki, które nie mieszczą się między sąsiednimi, są odrzucane z podaniem powodu. Kilometraż, profil podłużny i przekroje poprzeczne podążają za zakrzywioną osią, a IFC 4.3 zapisuje ją jako trasę (alignment).

Linie CAD stają się elementami terenu: **Droga z linii CAD** (LR) i **Linia nieciągłości z linii CAD** (BL) biorą linię lub polilinię narysowaną albo wczytaną z DXF, podążają za każdą linią na jej warstwie, która styka się z nią końcami, i tworzą wzdłuż niej drogę lub linię nieciągłości terenu. Gdy linie mają wysokości (linie 3D z DXF), dają one niweletę drogi lub rzędne linii nieciągłości. Takie linie są zachowywane z wysokościami i mogą być pokazane w 3D (kategoria *linie*).

## Plany zagospodarowania i plany orientacyjne

Przeglądarka projektu ma pod poziomami gałąź **Zagospodarowanie**: własne rysunki terenu, które nie należą do żadnej kondygnacji. Nowy projekt zaczyna się od dwóch:

- **Plan zagospodarowania** (rum. *plan de situație*), w skali 1:500: cały model widziany z góry, wszystkie kondygnacje naraz, łącznie z dachami, z terenem, ścieżkami, roślinami, działką i budynkami sąsiednimi. To, co leży w całości poniżej ±0,00 (fundamenty), jest pomijane, nic nie jest przecinane i nic ukrytego nie jest rysowane. Narożniki działki są numerowane;
- **Plan orientacyjny** (rum. *plan de încadrare în zonă*), w skali 1:2000: ten sam rysunek dla terenu w jego otoczeniu, na ortofotomapie, bez numerów narożników; budynki projektu są wypełnione na czerwono, a działka obrysowana na czerwono, aby teren wyróżniał się spośród sąsiadów.

**+** obok gałęzi Zagospodarowanie tworzy kolejny z nich; przyciski kopiowania powielają istniejący. Oba mają **strzałkę północy** wskazującą północ geograficzną (Zarządzaj → Lokalizacja: kąt do północy geograficznej), **podziałkę liniową** oraz, gdy projekt jest powiązany z mapą, **siatkę współrzędnych**: krzyżyki tam, gdzie spotykają się okrągłe współrzędne wschodnie i północne, co jedną dziesiątą skali (50 m przy 1:500, 200 m przy 1:2000), z ich wartościami. Zaznacz widok w przeglądarce i ustaw je w jego właściwościach, w sekcji **Ustawienia widoku**: czym jest (plan zagospodarowania lub plan orientacyjny), krok siatki (0: brak), strzałkę, podziałkę oraz **Wyróżnij projekt** (czerwone wypełnienie i obrys, domyślnie włączone na planie orientacyjnym, a na planie zagospodarowania także, gdy tego chcesz).

**Mapa pod nimi.** Ortofotomapa jest rysowana pod każdym planem zagospodarowania i planem orientacyjnym, niezależnie od kondygnacji, na którą ją zaimportowano, i drukuje się z nimi na arkuszach oraz w PDF. Dla mapy ewidencyjnej lub rysunku geodety otwórz plan zagospodarowania i użyj **Importuj DXF…** z opcją **Umieszczony: według współrzędnych mapy**. Lapin sam to proponuje, gdy współrzędne pliku wyglądają na współrzędne mapy, na przykład Stereo 70. Rysunek ląduje wtedy tam, gdzie wskazują jego współrzędne wschodnie i północne, poprzez punkt geodezyjny projektu i jego kąt do północy geograficznej. Jeśli projekt nie jest jeszcze powiązany z mapą, rysunek go wiąże. Zaznacz **Jako łącze**, aby zachować go tylko do odczytu i wczytać ponownie, gdy geodeta przyśle nowy. Zaimportuj go raz do każdego rysunku, który go potrzebuje.

![Plan orientacyjny: dom wypełniony na czerwono, działka obrysowana na czerwono, wśród sąsiadów](shots/site-location-plan.webp)

![Plan zagospodarowania: dom z góry na swojej działce, numerowane narożniki, siatka mapy, strzałka północy i podziałka liniowa](shots/site-drawings.webp)

![Gałąź Zagospodarowanie w przeglądarce projektu](shots/site-browser.webp)

Zestawienie **Współrzędne działki** (Zestawienia → + → Współrzędne działki) to wykaz współrzędnych działki: każdy narożnik numerowany tak jak na planie zagospodarowania, jego współrzędna północna i wschodnia w układzie projektu, bok do następnego narożnika oraz powierzchnia. Jak każde zestawienie można je umieścić na arkuszu obok planu zagospodarowania i wyeksportować jako CSV lub XLSX.

## Drzewa i krzewy

**Roślina** (PN) sadzi jedną roślinę jednym kliknięciem, **Strefa nasadzeń** (PZ) obsiewa obrys mieszanką gatunków z zadaną gęstością; zaznacz jedną z nich i wybierz jej gatunek we właściwościach. Lista gatunków to biblioteka roślin rodzimych lub tradycyjnych w Rumunii, w grupach: **drzewa owocowe** (jabłoń, grusza, śliwa, czereśnia i wiśnia, morela, pigwa), **iglaste** (jodła pospolita, świerk pospolity, sosna zwyczajna i czarna), **drzewa liściaste** (orzech włoski, lipa, brzoza, klon), **krzewy** (leszczyna, dereń jadalny, bez czarny, róża dzika, lilak, rokitnik, porzeczka czerwona, żywotnik, bukszpan) oraz **rośliny okrywowe**. Każdy ma swoją nazwę łacińską, zwykłą wysokość i koronę, a gdy interfejs jest po rumuńsku, jest pokazywany pod nazwą rumuńską. Zestawienie nasadzeń podaje obok każdego gatunku nazwę łacińską i rumuńską.

Na rzucie każda roślina jest rysowana z góry cienką linią, tak szeroka jak jej korona, samym konturem: orzech lub lipa jako łagodnie klapowana korona, drzewo owocowe jako okrąg, jodła lub świerk jako gwiazda, sosna jako postrzępiony pierścień, krzew jako mała chmurka, strzyżona roślina żywopłotowa jako okrąg, z małym krzyżykiem przy pniu drzewa. Elewacje i przekroje rysują każdą roślinę jednym konturem: koronę drzewa na pniu, piętrowy stożek iglaka, kopułę krzewu. Po kilka linii, więc plan zagospodarowania z setkami drzew pozostaje lekki. W 3D iglaki są stożkami, inne drzewa mają zaokrągloną koronę na pniu, a krzewy niską kopułę.

![Mały sad na rzucie: drzewa owocowe, orzech, drzewa iglaste i krzewy](shots/planting-symbols.webp)

Ustaw **Sztafaż w 2D** w stylu widoku na **szczegółowo**, by dostać symbole planu zieleni, na czarno: drzewo owocowe jako klapowana korona z rozwidlonymi gałęziami, orzech lub lipa jako ząbkowana korona wokół ciemnego rdzenia, jodła lub świerk jako gwiazda gałązek z igłami, sosna jako okrągła kępka igieł, krzew jako mała chmurka. Lapin sam rysuje każdy symbol, więc sąsiednie drzewa nieco się różnią, a strefa nigdy nie wygląda na odbitą pieczątką; w skali 1:1000 i mniejszej (plan orientacyjny) rysowane są tylko kontury koron.

![Ten sam sad narysowany szczegółowo](shots/planting-symbols-detailed.webp)

## Ludzie i samochody

**Sztafaż** (EN) stawia osobę lub samochód jednym kliknięciem; wybierz, co, na pasku wiersza poleceń, gdy narzędzie jest włączone: **stojącego mężczyznę**, **idącą kobietę**, **dziecko**, **osobę przy telefonie**, **samochód** lub **SUV / furgonetkę**. **Rząd sztafażu** (ER) stawia rząd wzdłuż narysowanej linii: samochody co 2,5 m, zaparkowane obok siebie w poprzek niej, przodem w lewo od linii, ludzi co 1,2 m, idących wzdłuż niej. Każdy stoi na terenie pod sobą, a gdzie nie ma terenu, na posadzce swojej kondygnacji.

![Ludzie przy drzwiach i samochody zaparkowane obok domu](shots/entourage-3d.webp)

Zaznacz jeden, aby zmienić go we właściwościach: rodzaj, **obrót**, **wysokość** (domyślnie zwykły rozmiar rodzaju), jak wysoko stoi **nad terenem** (na tarasie lub balkonie) i **kolor** ubrania lub nadwozia. Przesuwasz, obracasz, odbijasz i kopiujesz go jak każdy element.

W 3D to proste figury: osoby z kończynami, ubraniem, włosami i butami, samochody z szybami, kołami i światłami. Rysunki zachowują je proste, tak jak architekt rysuje sztafaż, cienką linią: rzuty i plany zagospodarowania z góry, ramiona i głowa osoby, kontur samochodu z zaokrąglonymi narożnikami i jego przednia szyba; elewacje i przekroje jako sylwetki, po jednym konturze, postać osoby, bok lub przód samochodu tak, jak widzi go widok, nigdy linie ich brył.

![Na rzucie](shots/entourage-plan.webp)

![Na elewacji południowej](shots/entourage-elevation.webp)

![Właściwości samochodu](shots/entourage-properties.webp)

To, jak osoby, samochody i rośliny rysują się w 2D, jest ustawieniem stylu widoku, **Sztafaż w 2D** (we właściwościach widoku lub **Widok → Ustawienia widoku**; dla całego stylu **Zarządzaj → Style widoku**): **kontury** (domyślnie), **abstrakcyjnie** (osoba jako okrąg na rzucie i głowa nad tułowiem na elewacji, samochód jako zaokrąglone pudełko, drzewo jako okrąg z pniem), **szczegółowo** (symbole zieleni roślin i każda linia brył, z cieniami słonecznymi widoku, tak jak Lapin rysował je wcześniej) albo **nierysowany**. **Wypełnienie sylwetek** wypełnia sylwetki na elewacjach i przekrojach jasnoszarym lub bladozielonym kolorem. Rysują się piórem 2 (0,13 mm w 1:100), chyba że **Zarządzaj → Style obiektów** nada kategorii **Sztafaż** inne, a **Widoczność / Grafika** ukrywa tę kategorię w widoku. Arkusze, PDF i DXF drukują je tak, jak rysuje je widok; ich bryły 3D nigdy się nie zmieniają. Projekty zrobione wcześniej też rysują je jako kontury.

Nadają rysunkom i obrazom skalę, ale nie są częścią budynku. Eksport IFC lub Fragments (File → Export…) pomija je, chyba że zaznaczysz **Sztafaż** na jego liście rodzajów; IFC zapisuje je jako IfcBuildingElementProxy „Entourage”. Eksporty 3D (glTF, USD, COLLADA) je zabierają. Testy kolizji je pomijają.

Postacie idą za ludźmi i samochodami z [BimGo](https://github.com/aussieBIMguru/BimGo) autorstwa Aussie BIM Guru (MIT-0): ich proporcjami, postawami i profilami.

## Bramy

Grupa **Bramy** na karcie Zagospodarowanie wstawia bramy z ich słupkami: **Bramę skrzydłową**, **Bramę dwuskrzydłową** (<kbd>GT</kbd>), **Bramę przesuwną**, **Bramę przesuwną samonośną**, **Furtkę**, **Bramę z furtką obok** i **Bramę składaną**. Lapin nie ma własnego ogrodzenia: narysuj ogrodzenie jako ścianę (niską, wybranego typu) albo pozwól, aby słupki bramy stanęły na granicy działki.

Kliknij oba końce bramy. Gdy na pasku włączone jest **Na liniach** (domyślnie), środek bramy trafia na oś najbliższej ściany lub narysowanej linii w promieniu 1,5 m (ściana, granica działki, narysowana linia), wzdłuż niej; po wyłączeniu brama stoi między dwoma punktami. Pasek wybiera też typ, który wstawia, spośród stylów projektu lub wbudowanych stylów Lapin.

Brama postawiona na ścianie przecina ją od zewnętrznych lic słupków, na całą wysokość, więc ściana ogrodzenia kończy się na słupkach.

![Brama przesuwna i furtka w murze ogrodzenia, brama dwuskrzydłowa na granicy działki](shots/gates-plan.webp)

Rzuty rysują słupki jako przecięte, skrzydła otwarte o kąt otwarcia z łukiem otwierania, panele bramy składanej złożone; bramę przesuwną zamkniętą, ze strzałką w kierunku otwierania, z prowadnicą i przerywaną strefą, w której się chowa, tak długą jak jej skrzydło (skrzydło bramy samonośnej jest dłuższe od otworu o ogon przeciwwagi, około 45%). Elewacje, przekroje i 3D rysują bryły.

![Bramy w 3D](shots/gates-3d.webp)

### Konfigurator

**Typy bram** otwiera konfigurator: style projektu oraz wbudowane style Lapin, dodawane jednym kliknięciem (dwuskrzydłowa brama kuta, przesuwna z poziomych lameli, składana stalowa, łukowa furtka, drewniana brama wjazdowa, brama z siatki ochronnej, automatyczna brama samonośna, dwuskrzydłowa z furtką, brama gospodarska, przesuwna z blachy perforowanej, drewniana furtka ogrodowa). Typ ustala, w grupach, tylko to, co ma jego rodzaj:

- **Brama:** szerokość w świetle i wysokość, prześwit nad terenem, szerokość furtki, panele po każdej stronie bramy składanej, kąt otwarcia;
- **Skrzydła:** rozmiar ramy, wypełnienie (pręty pionowe, lamele poziome, pełny panel, siatka lub blacha perforowana), rozstaw prętów lub lameli, grotowe zakończenia prętów, górna krawędź (prosta, łukowa lub szpiczasta) i jej strzałka;
- **Słupki:** murowane, stalowe lub brak, ich rozmiar i wysokość, czapa słupka murowanego (płaska lub ostrosłupowa);
- **Okucia:** napęd (ramię na skrzydłach bramy skrzydłowej, skrzynka obok prowadnicy bramy przesuwnej) oraz klamka z zamkiem;
- **Materiały** według roli: rama, wypełnienie, słupki, czapy, okucia i napęd. Materiał wymieniony tutaj, którego projekt nie ma, zostaje utworzony.

Podgląd pochodzi z silnika: rzut z łukiem otwierania lub strefą chowania u góry, elewacja u dołu. Każda brama danego typu podąża za zmianą.

![Konfigurator bram z dwuskrzydłową bramą kutą](shots/gates-types.webp)

Zaznacz bramę, aby zmienić jej typ, obrót, wysokość nad terenem, stronę, na którą **otwierają się** skrzydła, rękę, po której są zawiasy (lub gdzie chowa się brama przesuwna), i to, jak szeroko jest narysowana otwarta (0: zamknięta). **Druga strona** i **Druga ręka** we właściwościach albo strzałki obok na rzucie odwracają je. **Modyfikuj → Lustro** daje jej drugą rękę.

![Właściwości bramy](shots/gates-properties.webp)

### Zestawienie, koszty i IFC

**Zestawienie bram** wymienia bramy według typu: rodzaj, wymiar w świetle, skrzydła, wypełnienie, górną krawędź, słupki, napęd i liczbę. Nowy typ ma pozycje kosztowe `LAP-GATE` i, z napędem, `LAP-GATEMOT`, liczone na bramę.

IFC zapisuje bramę jako **IfcDoor** o PredefinedType **GATE**, typu **IfcDoorType** z jej sposobem działania (DOUBLE_DOOR_SINGLE_SWING, SLIDING_TO_LEFT, DOUBLE_DOOR_FOLDING…; brama z furtką USERDEFINED), szerokością w świetle i wysokością jako OverallWidth i OverallHeight oraz Pset_DoorCommon. Słupki są częścią bryły bramy, więc jeden element przenosi bramę tak, jak ją skonfigurowano, a Lapin wczytuje ją z powrotem jako własną bramę.

## Zasady zagospodarowania i słońce (Lapin Urban)

Edycja **Lapin Urban** dodaje do tej karty grupę **Planowanie**: zasady zagospodarowania działki (POT, CUT, maksymalne wysokości, odsunięcia od ulicy, boków, tyłu i osi drogi), **dopuszczalną bryłę zabudowy**, którą one pozostawiają, wskaźniki, jakie model osiąga względem nich, oraz **analizę nasłonecznienia**: słońce i jego cienie w 3D dla dowolnego dnia i godziny w miejscu projektu oraz plany zacienienia dla równonocy i przesileń.
