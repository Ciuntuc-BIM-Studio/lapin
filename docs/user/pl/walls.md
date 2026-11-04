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
- **Ściana osłonowa** (<kbd>CW</kbd>) rysuje przeszkloną ścianę ze słupkami i panelami.
- Ściany łączą się same tam, gdzie się spotykają: narożnik na ucios, ściana kończąca się na innej jako T, jedna krzyżująca drugą jako X. W połączeniu T lub X warstwy przechodzą jedna w drugą tak, jak w budowie: rdzeń kończącej się ściany przechodzi przez wykończenia drugiej aż do jej rdzenia, a jej wykończenia zatrzymują się na licu drugiej, spotykając swoje odpowiedniki, więc rzut pokazuje jeden układ warstw bez linii w poprzek. **Połącz** (<kbd>JW</kbd>), **Na ukos** i **Na styk** zmieniają sposób, w jaki spotykają się dwie ściany, gdy nie jest to to, czego chcesz; **Nowe narożniki na ukos** ustawia na tym komputerze połączenie na styk jako domyślne. Tak czy inaczej, każda warstwa skręca w narożniku osobno: wykończenia spotykają swoje odpowiedniki i biegną dookoła bez przerwy, a rdzenie spotykają się po przekątnej (na ucios) albo jeden rdzeń biegnie do zewnętrznego lica drugiego (na styk), więc rzut ścian warstwowych pokazuje czyste narożniki. Każda warstwa wchodzi w drugą ścianę przez jej warstwy o niższym **Priorytecie** i zatrzymuje się na pierwszej o równym lub wyższym (zobacz **Typy ścian** niżej), więc domyślnie rdzenie spotykają się z rdzeniami, a wykończenia kończą się na licach; w narożniku rdzeń o wyższym priorytecie przechodzi przez rdzeń drugiej ściany, na ucios czy na styk. **Priorytet cięcia** ściany nigdy nie wprowadza jej w ścianę, z którą jest połączona: między połączonymi ścianami decyduje tylko o tym, który rdzeń przechodzi, gdy oba rdzenie mają ten sam priorytet (w narożniku na styk lub na skrzyżowaniu).
- **Zamień** zmienia, która z dwóch ścian przechodzi na wskroś.

**Linia, po której rysujesz.** Gdy narzędzie Ściana jest włączone, lista na pasku wiersza poleceń wybiera linię, którą klikasz: **Oś rdzenia** (oś, jak dotąd), **Rdzeń, lico zewnętrzne**, **Rdzeń, lico wewnętrzne**, **Lico zewnętrzne** lub **Lico wewnętrzne**. Strona zewnętrzna to prawa strona ściany w kierunku rysowania (rysuj dookoła budynku przeciwnie do ruchu wskazówek zegara, aby była na zewnątrz). Oś ściany pozostaje na jej rdzeniu, odsunięta od linii, a ściany rysowane jedna po drugiej na tym samym licu spotykają się w narożniku tego lica.

## Zmiana ściany

Kliknij ścianę, aby ją zaznaczyć. Pojawią się jej uchwyty. Przeciągnij końce, aby ją wydłużyć lub skrócić, albo przeciągnij krzyżyk pośrodku, aby ją przesunąć.

![Zaznaczona ściana z uchwytami](shots/wall-selected.webp)

Panel Właściwości pokazuje zaznaczoną ścianę. U góry panelu są jej typ i warstwy. Poniżej znajdują się jej długość, wysokość, odsunięcie podstawy, kondygnacja i faza. Zmień dowolną wartość, a model podąży za nią.

![Właściwości ściany](shots/wall-properties.webp)

Niżej sekcja **Zależności** pokazuje, co jest zbudowane ze ściany i z czego jest zbudowana ściana. **Używany przez** wymienia elementy, które zmieniają się razem ze ścianą: ściany, z którymi się łączy, jej drzwi i okna, ścianę do niej dołączoną. Kliknij nazwę, aby zaznaczyć element. **Zbudowany z** wymienia elementy, od których zależy ściana, a potem jej typ i materiały. Gdy usuwasz element, od którego zależą inne, komunikat wymienia te, które zostały przebudowane; <kbd>Ctrl</kbd> <kbd>Z</kbd> przywraca wszystko.

**Odwróć** przenosi warstwy ściany na drugą stronę. Możesz też kliknąć małe strzałki widoczne obok zaznaczonej ściany na rzucie.

## Typy ścian

Typ ściany to zestaw warstw, od lica po prawej stronie kierunku, w którym rysowana jest ściana (zewnętrznego, gdy rysujesz wokół budynku przeciwnie do ruchu wskazówek zegara), do lica po jej lewej. Każda warstwa ma materiał i grubość, a jedna warstwa jest **rdzeniem**. Linia ściany biegnie wzdłuż rdzenia. Aby edytować typy, wybierz **Architektura → Typy ścian**. Warstwa inna niż rdzeń może mieć własną wysokość: **Od** i **Do** określają, jak wysoko sięga ponad podstawę ściany (lamperia z płytek do 1500, okładzina od 300 w górę); gdy są puste, warstwa biegnie przez całą wysokość.

Kolumna **Priorytet** (od 0 do 100) mówi, jak warstwa spotyka się z warstwami innej ściany tam, gdzie ściany się łączą: przechodzi przez warstwy drugiej ściany o niższym priorytecie i zatrzymuje się na pierwszej o równym lub wyższym. Pusta — warstwa bierze priorytet swojego materiału z **Zarządzaj → Kolejność cięcia**, a w jego braku 50 dla rdzenia i 0 dla pozostałych warstw (liczba pokazana na szaro). Dzięki temu rdzeń betonowy o priorytecie 100 nie jest przerywany przez rdzeń z profili o 99 i przechodzi przez ściankę, a twardy tynk o 60 zatrzymuje rdzeń ściany, która się na nim kończy. Eksport IFC zapisuje go jako Priority warstwy, a import odczytuje go z powrotem.

Warstwa inna niż rdzeń może też zmieniać materiał na wysokości: przycisk **warstwy** na końcu jej wiersza dodaje fragment (płytki, domyślnie do 2100), od dołu warstwy w górę; dodaj kolejne, aby uzyskać pas (farba do 900, płytki do 1500), a powyżej ostatniego fragmentu warstwa biegnie dalej we własnym materiale aż do góry. Każdy fragment jest rysowany, przecinany na rzutach i przekrojach oraz liczony we własnym materiale.

Warstwa inna niż rdzeń może mieć grubość 0, dla materiału zbyt cienkiego, by go modelować, który jednak trzeba policzyć: hydroizolacji, paroizolacji, farby. Nie tworzy żadnej bryły; rzuty i przekroje rysują ją grubą linią przerywaną tam, gdzie leży między dwiema warstwami (na licu linia lica jest jej linią), rysunek Układów warstw typów nazywa ją jak pozostałe, a liczy się ją powierzchnią: lica warstwy, na której leży, bez otworów, w ilościach ściany i w zestawieniu ilości według materiału.

![Warstwa tynku z płytkami ceramicznymi do 2100 i tynkiem powyżej](shots/wall-layer-parts.webp)

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

### Profile

**Zarządzaj → Profile** przechowuje profile projektu, kontury, wzdłuż których przeciąga się profile ścian, bruzdy i ławy fundamentowe. **Nowy profil** zaczyna od katalogu z jego wymiarami: prostokąt, gzyms fazowany, gzyms, półwałek, nos parapetu, gzyms z kapinosem; bruzda V, prostokątna lub okrągła; dla ław prostokątna, schodkowa z 2 lub 3 stopniami, ze skośną górą, mimośrodowa, ze ścianą fundamentową. Albo wybierz **Wpisane wierzchołki**: każdy wierzchołek u v w milimetrach, oddzielone średnikiem, a trzecia liczba zamienia bok wychodzący z tego wierzchołka w łuk. **Z narysowanego konturu** odczytuje zamkniętą polilinię lub prostokąt narysowany narzędziami Rysowania i zaznaczony w swoim widoku. Okrąg na podglądzie to punkt wstawienia: dla profilu ściany leży na licu ściany na wysokości profilu, a u biegnie na zewnątrz lica (w głąb ściany dla bruzdy). Gdy zmienisz profil, wszystko, co go używa, zmienia się razem z nim.

![Okno Profile](shots/profiles.webp)

### Okładziny

Okładziny modeluje się warstwami samej ściany, nie profilami. Dodaj warstwę wykończeniową po zewnętrznej stronie typu ściany (kamień, deski, płyty włóknocementowe, ETICS) z jej grubością i materiałem. **Od** i **Do** nadają jej własną wysokość (kamienny cokół do 600, tynk powyżej), a przycisk **warstwy** tej warstwy zmienia jej materiał pasami na wysokości. Dla okładziny odsuniętej od ściany narysuj osobną ścianę na licu (opcja linii **Lico zewnętrzne** narzędzia ściany) z typem zbudowanym z warstw okładziny. Okładzina układana deska po desce (deski pionowe lub poziome, łaty) nie jest jeszcze modelowana.

## Ściany pod dachem

Ściany na tym samym poziomie co dach same zatrzymują się pod nim i wznoszą się do jego szczytów. Dla ściany na innym poziomie zaznacz ją i wybierz **Architektura → Dołącz do dachu** (<kbd>AR</kbd>), a następnie kliknij dach — lub dowolną inną bryłę, do której ściana ma się wznosić: bryłę Studio, zaimportowany element. Zobacz [Stropy i dachy](floors-roofs.md).
