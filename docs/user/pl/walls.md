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
- Ściany łączą się same tam, gdzie się spotykają: narożnik na ucios, ściana kończąca się na innej jako T, jedna krzyżująca drugą jako X. W połączeniu T lub X warstwy przechodzą jedna w drugą tak, jak w budowie: rdzeń kończącej się ściany przechodzi przez wykończenia drugiej aż do jej rdzenia, a jej wykończenia zatrzymują się na licu drugiej, spotykając swoje odpowiedniki, więc rzut pokazuje jeden układ warstw bez linii w poprzek. **Połącz** (<kbd>JW</kbd>), **Na ukos** i **Na styk** zmieniają sposób, w jaki spotykają się dwie ściany, gdy nie jest to to, czego chcesz; **Nowe narożniki na ukos** ustawia na tym komputerze połączenie na styk jako domyślne. Tak czy inaczej, każda warstwa skręca w narożniku osobno: wykończenia spotykają swoje odpowiedniki i biegną dookoła bez przerwy, a rdzenie spotykają się po przekątnej (na ucios) albo jeden rdzeń biegnie do zewnętrznego lica drugiego (na styk), więc rzut ścian warstwowych pokazuje czyste narożniki.
- **Zamień** zmienia, która z dwóch ścian przechodzi na wskroś.

**Linia, po której rysujesz.** Gdy narzędzie Ściana jest włączone, lista na pasku wiersza poleceń wybiera linię, którą klikasz: **Oś rdzenia** (oś, jak dotąd), **Rdzeń, lico zewnętrzne**, **Rdzeń, lico wewnętrzne**, **Lico zewnętrzne** lub **Lico wewnętrzne**. Strona zewnętrzna to prawa strona ściany w kierunku rysowania (rysuj dookoła budynku przeciwnie do ruchu wskazówek zegara, aby była na zewnątrz). Oś ściany pozostaje na jej rdzeniu, odsunięta od linii, a ściany rysowane jedna po drugiej na tym samym licu spotykają się w narożniku tego lica.

## Zmiana ściany

Kliknij ścianę, aby ją zaznaczyć. Pojawią się jej uchwyty. Przeciągnij końce, aby ją wydłużyć lub skrócić, albo przeciągnij krzyżyk pośrodku, aby ją przesunąć.

![Zaznaczona ściana z uchwytami](shots/wall-selected.webp)

Panel Właściwości pokazuje zaznaczoną ścianę. U góry panelu są jej typ i warstwy. Poniżej znajdują się jej długość, wysokość, odsunięcie podstawy, kondygnacja i faza. Zmień dowolną wartość, a model podąży za nią.

![Właściwości ściany](shots/wall-properties.webp)

**Odwróć** przenosi warstwy ściany na drugą stronę. Możesz też kliknąć małe strzałki widoczne obok zaznaczonej ściany na rzucie.

## Typy ścian

Typ ściany to zestaw warstw, od lica zewnętrznego do lica wewnętrznego. Każda warstwa ma materiał i grubość, a jedna warstwa jest **rdzeniem**. Linia ściany biegnie wzdłuż rdzenia. Aby edytować typy, wybierz **Architektura → Typy ścian**. Warstwa inna niż rdzeń może mieć własną wysokość: **Od** i **Do** określają, jak wysoko sięga ponad podstawę ściany (lamperia z płytek do 1500, okładzina od 300 w górę); gdy są puste, warstwa biegnie przez całą wysokość.

Warstwa inna niż rdzeń może też zmieniać materiał na wysokości: przycisk **warstwy** na końcu jej wiersza dodaje fragment (płytki, domyślnie do 2100), od dołu warstwy w górę; dodaj kolejne, aby uzyskać pas (farba do 900, płytki do 1500), a powyżej ostatniego fragmentu warstwa biegnie dalej we własnym materiale aż do góry. Każdy fragment jest rysowany, przecinany na rzutach i przekrojach oraz liczony we własnym materiale.

![Warstwa tynku z płytkami ceramicznymi do 2100 i tynkiem powyżej](shots/wall-layer-parts.webp)

![Typy ścian: warstwy ściany ceglanej](shots/wall-types.webp)

- **+ Warstwa** dodaje warstwę. Strzałki przesuwają warstwę w górę lub w dół.
- **Strefy na wysokości** dzieli rdzeń od dołu do góry, na przykład na pierwszą warstwę, mur i wieniec.
- **Nadproża** wybiera nadproża nad drzwiami i oknami: prefabrykowane lub wylewane na miejscu, z ich oparciem i wysokością.
- **Rysuj tym typem** zamyka okno dialogowe i zaczyna rysowanie ścian tego typu.

Zmiana typu zmienia każdą ścianę tego typu.

**Importuj…**, **Eksportuj CSV** i **Eksportuj JSON**, pod listą typów, przenoszą typy ścian i stropów z jednego projektu do drugiego albo do arkusza kalkulacyjnego i z powrotem: CSV ma jeden wiersz na warstwę (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), wiersze typu jeden po drugim, a średniki i przecinki dziesiętne Excela również są odczytywane. Po wczytaniu typ o tej samej nazwie otrzymuje warstwy z pliku, a każdy inny zostaje utworzony, w jednym kroku do cofnięcia; materiał, którego brakuje w projekcie, jest tworzony pod swoją nazwą. Zestawienie **Układy warstw typów** (Zestawienia → + → Zestawy typów) wymienia każdy typ ściany i stropu z jego oznaczeniem, całkowitą grubością, liczbą elementów, które go używają, i jego warstwami po kolei.

## Ściany pod dachem

Ściany na tym samym poziomie co dach same zatrzymują się pod nim i wznoszą się do jego szczytów. Dla ściany na innym poziomie zaznacz ją i wybierz **Architektura → Dołącz do dachu** (<kbd>AR</kbd>), a następnie kliknij dach — lub dowolną inną bryłę, do której ściana ma się wznosić: bryłę Studio, zaimportowany element. Zobacz [Stropy i dachy](floors-roofs.md).
