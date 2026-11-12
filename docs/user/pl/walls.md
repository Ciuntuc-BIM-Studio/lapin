---
title: Ściany
order: 20
summary: Rysuj ściany narożnik po narożniku, zmieniaj je w panelu Właściwości i ustawiaj typy ścian z ich warstwami.
---

<!-- video walls -->

## Rysowanie ścian

Otwórz kartę **Architektura** i wybierz **Ściana** albo wpisz <kbd>WA</kbd>.

Gdy narzędzie jest włączone, panel Właściwości pokazuje **Nowy element**: typ, którym rysowane są ściany. Wybierz tam inny przed pierwszym kliknięciem i zobacz jego warstwy, grubość oraz to, ile ścian już go używa; **Edytuj typy…** otwiera Typy ścian. Drzwi, Okno, Strop, Sufit i Ściana osłonowa pokazują swoje typy w ten sam sposób.

![Karta Architektura](shots/ribbon-architecture.webp)

Kliknij tam, gdzie zaczyna się ściana, a potem klikaj w każdym narożniku. Następna ściana podąża za myszą. Podczas rysowania możesz zamiast klikać wpisać długość i kąt w polach nad widokiem. Naciśnij <kbd>Enter</kbd> lub <kbd>Esc</kbd>, aby zakończyć.

![Rysowanie ścian: następna ściana podąża za myszą, z widoczną długością i kątem](shots/wall-drawing.webp)

Tam, gdzie spotykają się dwie ściany, Lapin sam je łączy. Ich warstwy przechodzą jedna w drugą tak, jak zostałyby zbudowane.

- **Ściana łukowa** (<kbd>WC</kbd>) rysuje ścianę wzdłuż łuku.
- **Ściana osłonowa** (<kbd>CW</kbd>) rysuje przeszkloną ścianę ze słupkami i panelami; jej typ może wziąć ich rzeczywisty kształt z [biblioteki profili](profiles.md).
- Ściany łączą się same tam, gdzie się spotykają: narożnik na ucios, ściana kończąca się na innej jako T, jedna krzyżująca drugą jako X. W połączeniu T lub X warstwy przechodzą jedna w drugą tak, jak w budowie: rdzeń kończącej się ściany przechodzi przez wykończenia drugiej aż do jej rdzenia, a jej wykończenia zatrzymują się na licu drugiej, spotykając swoje odpowiedniki, więc rzut pokazuje jeden układ warstw bez linii w poprzek. **Połącz** (<kbd>JW</kbd>), **Na ukos** i **Na styk** zmieniają sposób, w jaki spotykają się dwie ściany, gdy nie jest to to, czego chcesz; **Nowe narożniki na ukos** ustawia na tym komputerze połączenie na styk jako domyślne. Tak czy inaczej, każda warstwa skręca w narożniku osobno: wykończenia spotykają swoje odpowiedniki i biegną dookoła bez przerwy, a rdzenie spotykają się po przekątnej (na ucios) albo jeden rdzeń biegnie do zewnętrznego lica drugiego (na styk), więc rzut ścian warstwowych pokazuje czyste narożniki. Każda warstwa wchodzi w drugą ścianę przez jej warstwy o niższym **Priorytecie** i zatrzymuje się na pierwszej o równym lub wyższym (zobacz **Typy ścian** niżej), więc domyślnie rdzenie spotykają się z rdzeniami, a wykończenia kończą się na licach; w narożniku rdzeń o wyższym priorytecie przechodzi przez rdzeń drugiej ściany, na ucios czy na styk. **Priorytet cięcia** ściany nigdy nie wprowadza jej w ścianę, z którą jest połączona: między połączonymi ścianami decyduje tylko o tym, który rdzeń przechodzi, gdy oba rdzenie mają ten sam priorytet (w narożniku na styk lub na skrzyżowaniu).
- **Zamień** zmienia, która z dwóch ścian przechodzi na wskroś.

**Linia, po której rysujesz.** Gdy narzędzie Ściana jest włączone, lista na pasku wiersza poleceń wybiera linię, którą klikasz: **Oś rdzenia** (oś, jak dotąd), **Rdzeń, lico zewnętrzne**, **Rdzeń, lico wewnętrzne**, **Lico zewnętrzne** lub **Lico wewnętrzne**. Strona zewnętrzna to prawa strona ściany w kierunku rysowania (rysuj dookoła budynku przeciwnie do ruchu wskazówek zegara, aby była na zewnątrz). Oś ściany pozostaje na jej rdzeniu, odsunięta od linii, a ściany rysowane jedna po drugiej na tym samym licu spotykają się w narożniku tego lica.

## Zmiana ściany

Kliknij ścianę, aby ją zaznaczyć. Pojawią się jej uchwyty. Przeciągnij końce, aby ją wydłużyć lub skrócić, albo przeciągnij krzyżyk pośrodku, aby ją przesunąć.

![Zaznaczona ściana z uchwytami](shots/wall-selected.webp)

Panel Właściwości pokazuje zaznaczoną ścianę. U góry panelu są jej typ i warstwy. Poniżej znajdują się jej długość, wysokość, odsunięcie podstawy, kondygnacja i faza. Zmień dowolną wartość, a model podąży za nią. Wartości każdej ściany są jej własne. Jej **grubość** (Thickness) wynika z warstw jej typu: wpisz nową, a zaznaczone ściany otrzymają typ o tym samym układzie i tej grubości, z rdzeniem zmienionym i wykończeniami zachowanymi. Lapin użyje typu projektu, jeśli któryś ma dokładnie taki układ, a w przeciwnym razie utworzy nowy, nazwany od ich typu (*Partition wall 130mm* staje się *Partition wall 150mm*). Pozostałe ściany starego typu zostają bez zmian, a <kbd>Ctrl</kbd> <kbd>Z</kbd> cofa wszystko jednym krokiem.

![Właściwości ściany](shots/wall-properties.webp)

Niżej sekcja **Zależności** pokazuje, co jest zbudowane ze ściany i z czego jest zbudowana ściana. **Używany przez** wymienia elementy, które zmieniają się razem ze ścianą: ściany, z którymi się łączy, jej drzwi i okna, ścianę do niej dołączoną. Kliknij nazwę, aby zaznaczyć element. **Zbudowany z** wymienia elementy, od których zależy ściana, a potem jej typ i materiały. Gdy usuwasz element, od którego zależą inne, komunikat wymienia te, które zostały przebudowane; <kbd>Ctrl</kbd> <kbd>Z</kbd> przywraca wszystko.

**Odwróć** przenosi warstwy ściany na drugą stronę. Możesz też kliknąć małe strzałki widoczne obok zaznaczonej ściany na rzucie.

Ściana rozpoznana z zaimportowanego IFC, której koniec plik tnie ukośnie, zachowuje to cięcie: **Kąt cięcia na początku** i **Kąt cięcia na końcu** w jej właściwościach, w stopniach od prostego. Połączenie na tym końcu zajmuje jego miejsce.

**Wyrównanie kreskowania** określa, jak warstwy tej ściany są kreskowane na rzucie. **material** przyjmuje ustawienie każdego materiału. **axis** układa kreskowanie każdej warstwy wzdłuż osi ściany, od początku osi, więc ukośny mur z cegły pokazuje warstwy cegieł wzdłuż siebie, ciągnące się z warstwy na warstwę. **view** zaczyna każde kreskowanie od początku układu widoku, bez obrotu.

### Ściana według profilu

**Profil…** we właściwościach ściany rysuje jej górę w widoku elewacji, widzianą od początku do końca: szczyt, spadek, uskoki, ścianę idącą za schodami. Przeciągaj punkty (**Shift** zachowuje wysokość punktu), kliknij górną linię, aby dodać punkt, kliknij punkt prawym przyciskiem, aby go usunąć, albo wpisz je w tabeli w milimetrach. **Zacznij od** daje **Szczyt**, **Nachylenie** lub **Schodki** do wysokości, którą wpiszesz. Uskok to dwa punkty jeden nad drugim. **Zastosuj** nadaje ścianie kształt; **Płaska góra** znów czyni ją płaską.

Ściana pozostaje ścianą: jej grubość, warstwy, połączenia oraz drzwi i okna są jak były, a do IFC eksportuje się z kształtem.

![Szczyt narysowany w edytorze profilu](shots/wall-profile-editor.webp)

![Zachodnia ściana domu jako szczyt, z zachowanym oknem](shots/wall-profile-3d.webp)

### Obrys i pręty ściany osłonowej

**Profil…** we właściwościach ściany osłonowej rysuje jej obrys na elewacji, jak dla ściany: szczyt, spadek, stopnie lub dowolny kontur, z punktami do przeciągania, dodawania lub usuwania i tabelą w milimetrach. Ściana sięga swojego najwyższego punktu. Jej siatka zyskuje linię tam, gdzie obrys się załamuje (załamanie bliżej niż półtora słupka od linii siatki jest przenoszone na nią); każde pole zachowuje rygle, które mieszczą się pod jego najniższym punktem; panel nad ostatnim wypełnia aż do obrysu, a pręt brzegowy podąża za nim, skośny; słupki są do niego przycinane. Ustawienia typu pozostają, a **Profil…** otwiera obrys ponownie, by go zmienić, bez przebudowy; **Płaska góra** zwraca prostokąt. Widać go w 3D, na elewacjach i przekrojach, zestawienia mierzą panele i pręty tak, jak narysowane, a IFC niesie je jako części.

![Szczyt narysowany jako obrys ściany osłonowej](shots/curtain-boundary-editor.webp)

![Ściana osłonowa pod swoim szczytem, w 3D](shots/curtain-boundary-3d.webp)

**Pręty…** nadaje wybranym słupkom i ryglom własny profil. Elewacja ściany pokazuje każdy pręt: kliknij jeden (<kbd>Shift</kbd> lub <kbd>Ctrl</kbd>/<kbd>⌘</kbd> dodaje kolejne, albo weź wszystkie słupki lub wszystkie rygle), wybierz profil z [bibliotek profili](profiles.md) albo zwykłą skrzynkę, jego **Obrót** i **Odbicie**, jego wyrównanie w poprzek pręta (przód lub tył na jego płaszczyźnie) i wzdłuż (krawędź początkowa lub końcowa na linii siatki lub poziomie) oraz jego przesunięcia w poprzek i na zewnątrz budynku; obraz obok pokazuje przekrój tak, jak siedzi. **Zastosuj do wybranych** nadaje im styl, **Wróć do ustawień typu** go zdejmuje. Własny profil typu ma te same pola położenia w **Ściany osłonowe…**, dla każdej ściany typu. Profil pozostaje kopią w projekcie: **Aktualizuj z biblioteki** zmienia każdy pręt z niego wykonany, w tej ścianie i w innych.

![Pręty: profil ułożony na wybranych słupkach](shots/curtain-members-dialog.webp)

![Dwa słupki z własnym profilem, w 3D](shots/curtain-members-3d.webp)

**Panele i drzwi…** nadaje wybranym polom własny panel lub drzwi. Widok ściany pokazuje każde pole tak, jak buduje je silnik: kliknij jedno (<kbd>Shift</kbd> lub <kbd>Ctrl</kbd>/<kbd>⌘</kbd> dodaje kolejne). **Typ panelu** zapisuje się raz w projekcie i nadaje polom dowolnej ściany osłonowej: szkło, pełny, **wypełnienie w ramie** (rama o swojej szerokości i głębokości wokół pola, wypełnienie szklane lub pełne cofnięte od jej zewnętrznej powierzchni o cofnięcie) lub **otwarte** (bez panelu); jego grubość, przesunięcie poza oś i materiały (z typu ściany osłonowej, gdy żaden nie jest wybrany). **Nadaj wybranym** stosuje go, **Panel typu** go zdejmuje; zmieniony typ panelu zmienia wszystkie jego pola. **Drzwi w wybranych** zastępuje każde wybrane pole drzwiami: nowymi drzwiami do ściany osłonowej (jedno- lub dwuskrzydłowymi, rozwieranymi lub przesuwnymi, o swojej szerokości i wysokości, z przeszklonymi lub pełnymi skrzydłami, z profilem ościeżnicy z [bibliotek profili](profiles.md)) lub dowolnym typem drzwi z projektu. **Wypełnij pole** nadaje im rozmiar pola; w przeciwnym razie zachowują rozmiar typu, stoją po lewej, pośrodku lub po prawej w polu z przesunięciem i dostają rygiel nad sobą i słupek obok tam, gdzie zostaje szkło, przeszklone panelem pola. W dolnym rzędzie drzwi stoją na podłodze, a dolny rygiel jest pod nimi wycięty. Ustawienia są zapamiętane dla pola, więc podążają za siatką, gdy się zmienia: drzwi wypełniające pole rosną lub maleją razem z nim, a te, których pola już nie ma, mówią o tym i wracają razem z nim. Drzwi są drzwiami: zaznacz je, by zmienić stronę otwierania lub stronę, typ, albo przenieść je do innego pola (Przesuń wzdłuż ściany); etykiety, zestawienia drzwi (w rozmiarze, w jakim stoją) i eksport IFC (IfcDoor będące częścią swojej IfcCurtainWall) je liczą.

![Panele i drzwi: podokiennik w ramie i drzwi w wybranych polach](shots/curtain-cells-dialog.webp)

![Ściana osłonowa z drzwiami i podokiennikami w ramie, w 3D](shots/curtain-cells-3d.webp)

Pod konturem narysowanym przez **Profil…** drzwi wchodzą do przyciętego pola wszędzie tam, gdzie ich prostokąt mieści się pod konturem: słupki obok nich sięgają do konturu, szyby obok słupków wypełniają się do niego, rygiel przechodzi nad ich nadprożem, a szkło nad nimi biegnie wzdłuż spadku. Gdy wypełniają takie pole, ich górna krawędź kończy się rygiel poniżej niższej strony konturu. Gdzie się nie mieszczą, Lapin mówi o ile i co zrobić (obniżyć je, przesunąć ku wyższej stronie lub wybrać wyższe pole). Panel w ramie w przyciętym polu ma ramę wzdłuż całego konturu, także po skośnej stronie. Widok elewacji zwrócony do zewnętrznej strony drzwi rysuje ich symbole otwierania linią przerywaną, jak dla każdych drzwi i okna.

![Drzwi pod skośnym konturem ściany osłonowej, z panelami w ramie wzdłuż spadku, w 3D](shots/curtain-cells-gable.webp)

![Symbol otwierania drzwi na elewacji](shots/curtain-cells-elevation.webp)

## Typy ścian

Typ ściany to zestaw warstw, od lica po prawej stronie kierunku, w którym rysowana jest ściana (zewnętrznego, gdy rysujesz wokół budynku przeciwnie do ruchu wskazówek zegara), do lica po jej lewej. Każda warstwa ma materiał i grubość, a jedna warstwa jest **rdzeniem**. Linia ściany biegnie wzdłuż rdzenia. Aby edytować typy, wybierz **Architektura → Typy ścian**. Warstwa inna niż rdzeń może mieć własną wysokość: **Od** i **Do** określają, jak wysoko sięga ponad podstawę ściany (lamperia z płytek do 1500, okładzina od 300 w górę); gdy są puste, warstwa biegnie przez całą wysokość.

Kolumna **Priorytet** (od 0 do 100) mówi, jak warstwa spotyka się z warstwami innej ściany tam, gdzie ściany się łączą: przechodzi przez warstwy drugiej ściany o niższym priorytecie i zatrzymuje się na pierwszej o równym lub wyższym. Pusta — warstwa bierze priorytet swojego materiału z **Zarządzaj → Kolejność cięcia**, a w jego braku 50 dla rdzenia i 0 dla pozostałych warstw (liczba pokazana na szaro). Dzięki temu rdzeń betonowy o priorytecie 100 nie jest przerywany przez rdzeń z profili o 99 i przechodzi przez ściankę, a twardy tynk o 60 zatrzymuje rdzeń ściany, która się na nim kończy. Eksport IFC zapisuje go jako Priority warstwy, a import odczytuje go z powrotem.

Kolumna **Notatka** mówi, jak warstwa jest ułożona w tym typie (*w ciągłej warstwie, Baumit SupraFix*): należy do typu, nie do materiału, a etykieta przekroju warstw pisze ją po warstwie; IFC przenosi ją jako Description warstwy. Gdy materiał warstwy ma grubości katalogowe (**Zarządzaj → Materiały → Dane**), mała strzałka przy grubości je podpowiada, a znak ostrzeżenia oznacza warstwę o innej grubości.

Warstwa inna niż rdzeń może też zmieniać materiał na wysokości: przycisk **warstwy** na końcu jej wiersza dodaje fragment (płytki, domyślnie do 2100), od dołu warstwy w górę; dodaj kolejne, aby uzyskać pas (farba do 900, płytki do 1500), a powyżej ostatniego fragmentu warstwa biegnie dalej we własnym materiale aż do góry. Każdy fragment jest rysowany, przecinany na rzutach i przekrojach oraz liczony we własnym materiale.

Warstwa inna niż rdzeń może mieć grubość 0, dla materiału zbyt cienkiego, by go modelować, który jednak trzeba policzyć: hydroizolacji, paroizolacji, farby. Nie tworzy żadnej bryły; rzuty i przekroje rysują ją grubą linią przerywaną tam, gdzie leży między dwiema warstwami (na licu linia lica jest jej linią), rysunek Układów warstw typów nazywa ją jak pozostałe, a liczy się ją powierzchnią: lica warstwy, na której leży, bez otworów, w ilościach ściany i w zestawieniu ilości według materiału.

![Warstwa tynku z płytkami ceramicznymi do 2100 i tynkiem powyżej](shots/wall-layer-parts.webp)

**Kreskowania pod ręką.** Obok materiału każdej warstwy próbka pokazuje, jak materiał jest kreskowany tam, gdzie jest przecięty. Kliknij ją, aby ustawić kreskowania materiału na miejscu, bez przechodzenia do **Zarządzaj → Materiały**: jego kreskowanie tam, gdzie tnie go przekrój, gdzie tnie go rzut i gdzie widzi go elewacja, każde ze skalą, kątem, wyrównaniem, wypełnieniem i piórem, jak na karcie **Grafika 2D** Materiałów. Należą do materiału, więc zmienia się też każdy typ i element z niego zrobiony, a każda zmiana to jeden krok cofania. **Typy stropów** mają tę samą próbkę. Kliknięcie poza nią lub <kbd>Esc</kbd> ją zamyka.

![Próbka kreskowania warstwy otwarta w Typach ścian: kreskowania przekroju i elewacji materiału](shots/layer-hatch.webp)

![Typy ścian: warstwy ściany ceglanej](shots/wall-types.webp)

Legenda nad warstwami pokazuje ścianę na rzucie tak, jak pokazuje ją zaznaczona ściana: przerywaną oś z grotem strzałki w kierunku, w którym była rysowana, **P** na jej prawym licu i **L** na lewym. Warstwy są ponumerowane od 1 na licu P do ostatniej na licu L, tymi samymi numerami co wiersze tabeli; rdzeń, wzdłuż którego biegnie linia ściany, jest wyróżniony. Ściana odwrócona przyciskiem **Odwróć** ma warstwy w odwrotnej kolejności, od L do P. Komunikat o odrzuconym zapisie znika, gdy tylko znowu zmienisz typ.

- **+ Warstwa** dodaje warstwę. Strzałki przesuwają warstwę w górę lub w dół.
- **Strefy na wysokości** dzieli rdzeń od dołu do góry, na przykład na pierwszą warstwę, mur i wieniec.
- **Nadproża** wybiera nadproża nad drzwiami i oknami: prefabrykowane lub wylewane na miejscu, z ich oparciem i wysokością.
- **Rysuj tym typem** zamyka okno dialogowe i zaczyna rysowanie ścian tego typu.

Zmiana typu zmienia każdą ścianę tego typu.

**Importuj…**, **Eksportuj CSV** i **Eksportuj JSON**, pod listą typów, przenoszą typy ścian i stropów z jednego projektu do drugiego albo do arkusza kalkulacyjnego i z powrotem: CSV ma jeden wiersz na warstwę (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), wiersze typu jeden po drugim, a średniki i przecinki dziesiętne Excela również są odczytywane. Po wczytaniu typ o tej samej nazwie otrzymuje warstwy z pliku, a każdy inny zostaje utworzony, w jednym kroku do cofnięcia; materiał, którego brakuje w projekcie, jest tworzony pod swoją nazwą. Zestawienie **Układy warstw typów** (Zestawienia → + → Zestawy typów) wymienia każdy typ ściany i stropu z jego oznaczeniem, całkowitą grubością, liczbą elementów, które go używają, i jego warstwami po kolei.

## Profile i bruzdy w ścianach

**Architektura → Profil ściany** (<kbd>WB</kbd>) prowadzi profil wzdłuż lica ściany: gzyms, opaskę, cokół, parapet. **Bruzda w ścianie** (<kbd>RV</kbd>) wycina go w ścianie: bruzdę, fugę cieniową. Wybierz profil i wysokość na pasku wiersza poleceń, a potem kliknij lico ściany. W 3D profil staje na wysokości, którą klikniesz; na rzucie na wpisanej wysokości, po stronie, którą klikniesz. **od góry** mierzy wysokość w dół od korony ściany, dla gzymsu pod okapem. **Profil na zaznaczonych ścianach** i **Bruzda w zaznaczonych ścianach** dodają to samo na każdej zaznaczonej ścianie, na jej zewnętrznym licu.

Profil biegnie przez całą długość lica i przerywa się przy drzwiach i oknach, które by przeciął. Tam, gdzie dwie ściany spotykają się w narożniku i obie mają profil tego samego kształtu na tej samej wysokości na spotykających się licach, oba łączą się na ucios, jeden gzyms wokół narożnika. Zaznacz profil, aby zmienić go we Właściwościach: jego profil, materiał, lico (**Odwróć stronę**), wysokość, odsunięcie od lica, gdzie zaczyna się i kończy wzdłuż ściany i czy przerywa się przy otworach. Właściwości ściany pokazują profile i bruzdy, które na niej są.

Profile i bruzdy idą razem ze swoją ścianą, gdy jest kopiowana, przenoszona na inną kondygnację albo usuwana. Mierzy się je długością i objętością (dla bruzdy objętością wyciętą). W IFC profil to IfcCovering MOLDING, a bruzda to IfcOpeningElement RECESS w jego ścianie; oba wracają takie, jakie były.

![Gzymsy, opaski i bruzda na ścianach zewnętrznych domu](shots/wall-sweeps-3d.webp)

### Profile na typie ściany

Profil, który mają mieć wszystkie ściany danego typu (opaska cokołowa, gzyms, fuga cieniowa), należy do typu. W **Architektura → Typy ścian**, pod warstwami, **Profile i bruzdy** pokazuje je, po jednym wierszu: profil, **Profil ściany** albo **Bruzda w ścianie**, lico (P, lico pierwszej warstwy typu, albo L, jak nazywa je legenda), wysokość nad podstawą ściany (albo pod jej koroną z **Od góry**), odsunięcie na zewnątrz od lica, materiał profilu i jego zawinięcia. **Profil ściany** i **Bruzda w ścianie** pod tabelą dodają wiersz. Zapisz typ: dostają je wszystkie jego ściany, a także ściana narysowana tym typem lub na niego zmieniona. To profile jak inne: mierzone, w zestawieniu profili ścian, docinane na ukos w narożnikach, gdzie spotykają się ściany tego typu, zapisywane do IFC. Profile odwróconej ściany zmieniają lico razem z jej warstwami. **Eksportuj JSON** pod listą typów zabiera je ze sobą, z obrysem profili i materiałami według nazwy (CSV ma tylko warstwy).

Zaznacz jeden na ścianie: Właściwości pokazują **Z typu**. Zmień jego wysokość, odsunięcie albo inne pole, a to pole stanie się własne tej ściany (**Własne:** wysokość): późniejsze zmiany typu go nie ruszają. **Wróć do typu** przywraca mu wartości typu. **Wyłącz na tej ścianie**, albo usunięcie go, zostawia profil typu wyłączony na tej ścianie; **Włącz ponownie profile typu** w **Profile i bruzdy** ściany przywraca je.

![Profil na typie ściany, w oknie Typy ścian](shots/wall-type-sweeps.webp)

### Zawinięcia

Profil, który dochodzi do wolnego końca swojej ściany (nie dołączono tam innej ściany), zawija się: docięty na ukos na końcu, prowadzi swój profil za narożnik na czoło ściany i kończy się w płaszczyźnie lica, więc profil jest zamknięty, zamiast pokazywać przekrój. **Zawinięcia na wolnych końcach** we Właściwościach wybiera oba (domyślnie dla profilu), na początku, na końcu albo żadne, dla prostych końców. Bruzda nigdy się nie zawija. W narożniku połączonym z inną ścianą profil się nie zawija: dochodzi do narożnika albo łączy się z takim samym profilem na drugiej ścianie. Przy drzwiach i oknach kończy się prosto; zaznacz **Zawinięcia przy otworach**, aby zawijał się też wokół ościeży. **Cofnięty od początku** i **Cofnięty od końca** kończą go przed końcami lica (prosto w tym miejscu), a **Profil odwrócony** odwraca jego profil.

### Przesuwanie i kopiowanie profilu

Zaznacz profil i użyj **MOVE** albo **COPY** jak dla każdego elementu; jego ściana zostaje na miejscu. To, co przesunięcie pokonuje wzdłuż ściany, przesuwa profil po niej (profil na całym licu dostaje wtedy własny początek i koniec); to, co pokonuje w poprzek ściany, jest pomijane. Przesunięty w górę lub w dół na elewacji albo w przekroju zmienia wysokość. Kopia trafia na tę samą ścianę, przesunięta, albo na inną ścianę, gdy w nią trafi, na lico, na które trafi. **ROTATE** i **MIRROR** zostawiają go na jego ścianie: odbity względem osi ściany przechodzi na drugie lico. Profil typu, który przesuniesz, zachowuje nowe miejsce jako własne; przesunięty na inną ścianę staje się własnym profilem tej ściany, a profil typu zostaje wyłączony na pierwszej.

### Profile

**Zarządzaj → Profile** przechowuje profile projektu, kontury, wzdłuż których przeciąga się profile ścian, bruzdy i ławy fundamentowe. **Nowy profil** zaczyna od katalogu z jego wymiarami: prostokąt, gzyms fazowany, gzyms, półwałek, nos parapetu, gzyms z kapinosem; bruzda V, prostokątna lub okrągła; dla ław prostokątna, schodkowa z 2 lub 3 stopniami, ze skośną górą, mimośrodowa, ze ścianą fundamentową. Albo wybierz **Wpisane wierzchołki**: każdy wierzchołek u v w milimetrach, oddzielone średnikiem, a trzecia liczba zamienia bok wychodzący z tego wierzchołka w łuk. **Z narysowanego konturu** odczytuje zamkniętą polilinię lub prostokąt narysowany narzędziami Rysowania i zaznaczony w swoim widoku. Okrąg na podglądzie to punkt wstawienia: dla profilu ściany leży na licu ściany na wysokości profilu, a u biegnie na zewnątrz lica (w głąb ściany dla bruzdy). Gdy zmienisz profil, wszystko, co go używa, zmienia się razem z nim.

![Okno Profile](shots/profiles.webp)

### Okładziny

Okładziny modeluje się warstwami samej ściany, nie profilami. Dodaj warstwę wykończeniową po zewnętrznej stronie typu ściany (kamień, deski, płyty włóknocementowe, ETICS) z jej grubością i materiałem. **Od** i **Do** nadają jej własną wysokość (kamienny cokół do 600, tynk powyżej), a przycisk **warstwy** tej warstwy zmienia jej materiał pasami na wysokości. Dla okładziny odsuniętej od ściany narysuj osobną ścianę na licu (opcja linii **Lico zewnętrzne** narzędzia ściany) z typem zbudowanym z warstw okładziny. Okładzina układana deska po desce (deski pionowe lub poziome, łaty) nie jest jeszcze modelowana.

## Ściany pod dachem

Ściany na tym samym poziomie co dach same zatrzymują się pod nim i wznoszą się do jego szczytów. Dla ściany na innym poziomie zaznacz ją i wybierz **Architektura → Dołącz do dachu** (<kbd>AR</kbd>), a następnie kliknij dach — lub dowolną inną bryłę, do której ściana ma się wznosić: bryłę Studio, zaimportowany element. Zobacz [Stropy i dachy](floors-roofs.md).
