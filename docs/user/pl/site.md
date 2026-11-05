---
title: Teren i jego dane
order: 52
summary: Wprowadź pomiar, działkę, sąsiednie budynki i ortofotomapę w rzeczywistych współrzędnych, z plików DXF, CSV, GeoJSON, CityJSON i z georeferencyjnego obrazu.
---

Karta **Zagospodarowanie** obejmuje grunt, na którym stoi budynek: teren, działkę, sąsiednie budynki i zdjęcie lotnicze, każde tam, gdzie naprawdę się znajduje. Grupa **Importuj** wczytuje je z plików przekazanych przez geodetów i ewidencję gruntów.

![Karta Zagospodarowanie](shots/ribbon-site.webp)

## Rzeczywiste współrzędne

Pomiar przychodzi we współrzędnych mapy: wschodnich i północnych, setki kilometrów od zera. Lapin trzyma model blisko jego początku układu i wiąże go z mapą przez **punkt geodezyjny**. Pierwszy import do projektu robi to za ciebie: punkt geodezyjny trafia do początku układu rzutu, na pierwsze współrzędne pliku zaokrąglone do metra, a każdy kolejny import ląduje w tym samym miejscu.

**Zarządzaj → Lokalizacja** pokazuje **układ współrzędnych**, w którym podane są współrzędne wschodnie i północne, oraz punkt bazowy i punkt geodezyjny. Lapin zna Stereo 70 (EPSG:3844, państwowy układ Rumunii), strefy UTM 34 i 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) oraz długość i szerokość geograficzną (WGS 84, EPSG:4326). Plik w innym z tych układów jest przeliczany przy wczytaniu: GeoJSON w długości i szerokości geograficznej ląduje na rzucie w Stereo 70 tam, gdzie jego miejsce. Eksport IFC zapisuje układ i punkt geodezyjny, więc inne programy też umieszczają model na mapie.

![Zarządzaj → Lokalizacja: układ współrzędnych i punkt geodezyjny](shots/site-location.webp)

## Import

- **Teren z punktów** wczytuje plik CSV lub tekstowy z pomiaru: x y z albo numer, współrzędna północna, wschodnia, rzędna, jeden punkt w wierszu.
- **Punkty z DXF** wczytuje rysunek z pomiaru: obiekty POINT z ich wysokościami, bloki wstawione na wysokości lub niosące ją jako atrybut, rzędne zapisane obok punktów („245.37” obok krzyżyka) oraz warstwice na ich wysokości. Gdy rysunek ma punkty na kilku warstwach, Lapin wypisuje je z liczbą punktów na każdej, a ty wybierasz, które wziąć. Z punktów powstaje teren.
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

Na rzucie każda roślina jest rysowana z góry tak, jak pokazuje ją rysunek architektury krajobrazu, na czarno, o szerokości swojej korony: drzewo owocowe jako klapowana korona z rozwidlonymi gałęziami, orzech lub lipa jako ząbkowana korona wokół ciemnego środka, jodła lub świerk jako gwiazda gałązek z igłami, sosna jako okrągła kępka igieł, krzew jako mała chmurka. Lapin sam rysuje każdy symbol, więc sąsiednie drzewa nieco się różnią, a strefa nigdy nie wygląda jak ostemplowana. W skali 1:1000 i mniejszej (plan orientacyjny) rysowane są tylko obrysy koron. W 3D drzewa iglaste są stożkami, pozostałe drzewa mają zaokrągloną koronę na pniu, a krzewy niską kopułę.

![Mały sad na rzucie: drzewa owocowe, orzech, drzewa iglaste i krzewy](shots/planting-symbols.webp)

## Ludzie i samochody

**Sztafaż** (EN) stawia osobę lub samochód jednym kliknięciem; wybierz, co, na pasku wiersza poleceń, gdy narzędzie jest włączone: **stojącego mężczyznę**, **idącą kobietę**, **dziecko**, **osobę przy telefonie**, **samochód** lub **SUV / furgonetkę**. **Rząd sztafażu** (ER) stawia rząd wzdłuż narysowanej linii: samochody co 2,5 m, zaparkowane obok siebie w poprzek niej, przodem w lewo od linii, ludzi co 1,2 m, idących wzdłuż niej. Każdy stoi na terenie pod sobą, a gdzie nie ma terenu, na posadzce swojej kondygnacji.

![Ludzie przy drzwiach i samochody zaparkowane obok domu](shots/entourage-3d.webp)

Zaznacz jeden, aby zmienić go we właściwościach: rodzaj, **obrót**, **wysokość** (domyślnie zwykły rozmiar rodzaju), jak wysoko stoi **nad terenem** (na tarasie lub balkonie) i **kolor** ubrania lub nadwozia. Przesuwasz, obracasz, odbijasz i kopiujesz go jak każdy element.

W 3D to proste postacie: ludzie z kończynami, ubraniem, włosami i butami, samochody z szybami, kołami i światłami. Elewacje i przekroje rysują je z ich brył, z cieniami słońca widoku, gdy je ma. Rzuty i plany sytuacyjne rysują je z góry jako symbole: ramiona i głowę osoby, obrys samochodu i jego szyby.

![Na rzucie](shots/entourage-plan.webp)

![Na elewacji południowej](shots/entourage-elevation.webp)

![Właściwości samochodu](shots/entourage-properties.webp)

Nadają rysunkom i obrazom skalę, ale nie są częścią budynku. Eksport IFC lub Fragments (File → Export…) pomija je, chyba że zaznaczysz **Sztafaż** na jego liście rodzajów; IFC zapisuje je jako IfcBuildingElementProxy „Entourage”. Eksporty 3D (glTF, USD, COLLADA) je zabierają. Testy kolizji je pomijają.

Postacie idą za ludźmi i samochodami z [BimGo](https://github.com/aussieBIMguru/BimGo) autorstwa Aussie BIM Guru (MIT-0): ich proporcjami, postawami i profilami.

## Zasady zagospodarowania i słońce (Lapin Urban)

Edycja **Lapin Urban** dodaje do tej karty grupę **Planowanie**: zasady zagospodarowania działki (POT, CUT, maksymalne wysokości, odsunięcia od ulicy, boków, tyłu i osi drogi), **dopuszczalną bryłę zabudowy**, którą one pozostawiają, wskaźniki, jakie model osiąga względem nich, oraz **analizę nasłonecznienia**: słońce i jego cienie w 3D dla dowolnego dnia i godziny w miejscu projektu oraz plany zacienienia dla równonocy i przesileń.
