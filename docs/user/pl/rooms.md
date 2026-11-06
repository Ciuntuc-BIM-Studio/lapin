---
title: Pomieszczenia i kolory pomieszczeń
order: 45
summary: Umieszczaj pomieszczenia kliknięciem wewnątrz ścian, nazywaj je i numeruj, a na rzucie koloruj lub kreskuj je według funkcji lub działu, z legendą.
---

## Umieszczanie pomieszczenia

Wybierz **Architektura → Pomieszczenie** (<kbd>RM</kbd>) i kliknij wewnątrz przestrzeni zamkniętej ścianami. Lapin znajduje obrys pomieszczenia między licami ścian, ścian osłonowych i słupów wokół punktu. Drzwi nie otwierają pomieszczenia: zamyka się ono w ich poprzek. Słup stojący w pomieszczeniu nie wlicza się do jego powierzchni.

Pomieszczenie nadąża za swoimi ścianami. Przesuń ścianę, a pomieszczenie powiększy się lub zmniejszy razem z nią. Jeśli ściana zostanie usunięta i nic już nie zamyka pomieszczenia, zachowuje ono dotychczasowy obrys, a jego pieczątka robi się czerwona. **Architektura → Aktualizuj pomieszczenia** zapisuje obrysy, które pomieszczenia znajdują teraz.

Dla przestrzeni bez ścian dookoła, takiej jak taras lub jeden koniec otwartej strefy, użyj **Pomieszczenie z obrysu** i kliknij jego narożniki. Takie pomieszczenie zachowuje narysowany przez ciebie obrys.

![Rzut z dwoma pomieszczeniami pokolorowanymi według funkcji i legendą kolorów](shots/rooms-plan.webp)

## Właściwości pomieszczenia

Zaznacz pomieszczenie, klikając jego pieczątkę lub dowolne miejsce w jego wnętrzu. W panelu Właściwości:

- **Nazwa** i **Numer.** Nowe pomieszczenia są numerowane według poziomu: 101, 102… na parterze. Nazwa pomieszczenia to nazwa jego funkcji, dopóki nie nadasz mu własnej; po wyczyszczeniu wraca do funkcji.
- **Funkcja**, **Dział** i **Wykończenie podłogi.** Wpisz cokolwiek lub wybierz wartość, którą mają już inne pomieszczenia.
- **Wysokość.** Puste pole oznacza wysokość poziomu.
- **Własny kolor** i **Własne kreskowanie** zastępują schemat kolorów dla tego jednego pomieszczenia.

Ilości poniżej pokazują powierzchnię netto pomieszczenia (bez słupów), jego obwód i kubaturę. Przeciągnij uchwyt na pieczątce, aby ją przesunąć.

![Właściwości pomieszczenia](shots/room-properties.webp)

## Etykieta pomieszczenia

**Architektura → Etykieta pomieszczenia** (lub **Etykieta pomieszczenia…** we właściwościach pomieszczenia) określa, co pieczątka każdego pomieszczenia pokazuje na rzutach, wiersz po wierszu: nazwę, numer, funkcję, dział, powierzchnię, powierzchnię brutto, obwód, wysokość, kubaturę lub wykończenie podłogi albo własny tekst. Pierwszy wiersz jest pogrubiony. **Standardy rysunkowe → Etykiety** pokazuje tę samą etykietę jako tekst: `{name}|{number}|{area}`.

![Okno dialogowe Etykieta pomieszczenia](shots/room-tag.webp)

## Kolory pomieszczeń

**Widok → Kolory pomieszczeń** koloruje pomieszczenia rzutu. Wybierz schemat, określ, według czego koloruje (**Funkcja**, **Nazwa**, **Dział**, **Numer** lub **Wykończenie podłogi**), i ustaw **Ten rzut pokazuje** na ten schemat. Każda wartość, którą mają pomieszczenia, dostaje wiersz z:

- kolorem (**Kolor**),
- kreskowaniem (**Kreskowanie**) spośród wzorów projektu, z jego **Skalą** i kolorem (**Tusz**),
- oraz podglądem, jak to wygląda.

**Uzupełnij brakujące kolory** nadaje każdej wartości bez koloru kolor z łagodnej palety. **Nowy** i **Duplikuj** tworzą kolejne schematy, na przykład jeden do prezentacji dla klienta i jeden według działów.

![Okno dialogowe Kolory pomieszczeń](shots/room-colors.webp)

Kolory rysowane są pod ścianami, meblami i łukami otwierania drzwi, więc rzut pozostaje czytelny. Drukują się na arkuszach i trafiają do eksportów PDF i DXF. Nowy rzut nie pokazuje kolorów pomieszczeń, dopóki nie wybierzesz dla niego schematu, w **Kolory pomieszczeń** lub we właściwościach rzutu.

## Legenda kolorów

**Opisz → Legenda kolorów**: kliknij miejsce na rzucie, gdzie ma stanąć legenda. Wymienia ona kolory używane przez pomieszczenia rzutu, z ich nazwami, i nadąża za każdą zmianą schematu. Przesuń ją poleceniem **Przesuń**.

## Wymiary pomieszczeń

**Opisz → Wymiary pomieszczeń** (<kbd>ID</kbd>) wymiaruje od razu wnętrza pomieszczeń na rzucie. Każdy bok pomieszczenia dostaje wyrównany łańcuch wymiarowy tuż wewnątrz niego. Łańcuch ma punkt w każdym narożniku boku i na ościeżach każdych drzwi i okna w nim.

- Gdy pomieszczenia są zaznaczone, wymiarowane są tylko one. Gdy nic nie jest zaznaczone, wymiarowane jest każde pomieszczenie poziomu rzutu.
- Wywołane na planie sytuacyjnym, który widzi cały model z góry i nie przecina żadnej ściany, łańcuchy trafiają na rzut poziomu. Wysokość tekstu wymiaru można ustawić osobno we Właściwościach (**Wysokość tekstu**, mm na papierze; puste: ze stylu wymiarowania). Wysokości tekstów są na papierze, więc na planie sytuacyjnym 1:500 ten sam tekst jest w modelu pięć razy większy niż na rzucie 1:100.
- Punkty trzymają się ścian, jak w każdym wymiarze. Przesuń ścianę, a łańcuchy podążą za nią.
- Uruchom polecenie ponownie na pomieszczeniu, aby utworzyć jego łańcuchy od nowa, na przykład po dodaniu drzwi. Stare łańcuchy są zastępowane, nie podwajane.
- Bardzo krótkie boki, poniżej 30 cm, są pomijane. Edytuj lub usuń dowolny łańcuch tak, jak narysowany przez siebie.

![Dwa pomieszczenia zwymiarowane od wewnątrz: łańcuch każdego boku z drzwiami i oknami na nim](shots/room-dimensions.webp)

## Wykończenia pomieszczeń

**Wnętrza → Wykończ pomieszczenie** (<kbd>FR</kbd>) układa wykończenie w klikniętym pomieszczeniu: jego posadzkę, wykończenie ścian i sufit, każde podążające za obrysem pomieszczenia tak jak samo pomieszczenie. Przesuń ścianę, a wykończenia przesuną się razem z nią. **Wnętrza → Wykończenia pomieszczeń** edytuje wykończenia; **Z biblioteki** dodaje gotowe: Salon, Sypialnia, Łazienka, Kuchnia, Parter na płycie i Pomieszczenie techniczne. Gdy narzędzie jest włączone, panel właściwości pokazuje wykończenie, które układa, aby można je było wybrać przed kliknięciem. **Wykończenie** pomieszczenia, w jego właściwościach, układa inne albo je usuwa.

Wykończenie ma:

- **Posadzka**, jej warstwy od posadzki w dół (płytki, klej, wylewka…). Otwory w stropie w obrębie pomieszczenia (klatka schodowa) są wycinane. **Przez drzwi, do osi ściany** prowadzi posadzkę dalej pod każdymi drzwiami, tak że posadzka sąsiedniego pomieszczenia spotyka się z nią w osi ściany.
- **Ściany**, ich warstwy od lica ściany w stronę pomieszczenia, także wokół wysp pomieszczenia. Każda warstwa może zmieniać materiał na wysokości, tak jak warstwa typu ściany: w Łazience farba nad płytkami ceramicznymi do 2100 (13 mm grubości, płytki i klej); w Kuchni pas płytek od 900 do 1500. Fragment bez materiału niczego tam nie zostawia. Drzwi i okna ścian wokół pomieszczenia są wycinane, a ich ościeża obłożone tymi samymi warstwami, od lica pomieszczenia do ościeżnicy (w pustym otworze do osi ściany); także dolne ościeże okna, chyba że od strony pomieszczenia zakrywa je parapet. **Wysokość**: pusta, do sufitu.
- **Sufit**, jego warstwy od płyty nad pomieszczeniem w dół (od wysokości pomieszczenia, gdy nie ma płyty), z wyciętymi otworami.
- **Listwa przypodłogowa**: typ listwy ułożony wokół pomieszczenia, przerwany przy drzwiach (zobacz Listwy przypodłogowe, w Stropy i dachy).
- **Płyta konstrukcyjna pod posadzką**: osobny strop żelbetowy pod warstwami posadzki, z grubością i tym, jak daleko wychodzi poza obrys pomieszczenia (pod ściany). Jest stropem jak każdy inny, w konstrukcji i w jej zestawieniu.

**Zapisz** zmienia wykończenie i każde pomieszczenie, które je ma, jest układane ponownie. **Wykończ zaznaczone pomieszczenia** układa je w zaznaczonych pomieszczeniach.

**Kreskowanie w rzucie.** Na rzutach podłoga wykończonego pomieszczenia jest kreskowana tak, jak mówi jej wykończenie, pod ścianami, więc arkusze, PDF i DXF ją drukują (warstwa FINISH-HATCH). W części **Posadzka** wykończenia **Kreskowanie w rzucie** wybiera **według materiału** (domyślnie: wzór powierzchni górnej warstwy, jej kreskowanie dla elewacji, tam gdzie jest widoczna, w **Zarządzaj → Materiały**, inaczej ten, który podsuwa nazwa: płytki, deski dla parkietu i laminatu, krzyż dla wykładziny dywanowej, linie dla winylu, kropki dla posadzki wylewanej, inaczej linie ukośne), **własny wzór** (dowolny wzór projektu, z jego skalą, kątem i kolorem) lub **bez kreskowania**; skala i kąt dotyczą też wzoru materiału. Rzut widziany od dołu (rzut sufitu) kreskuje zamiast tego sufity, według **Kreskowania w rzucie** w części **Sufit**, tak samo. **Kreskowanie podłogi** we właściwościach pomieszczenia wyłącza je dla tego pomieszczenia. **Kreskowania wykończeń podłóg** stylu widoku (**Widok → Ustawienia widoku**, **Zarządzaj → Style widoku**) wyłącza je dla całych widoków: włączone w Architectural, wyłączone w Structural, MEP i Coordination. Z kolorami pomieszczeń na rzucie kolor wypełnia pomieszczenie, a kreskowanie jest rysowane na nim.

![Rzut z podłogą łazienki kreskowaną jak płytki i salonu jak deski](shots/room-finish-hatch.webp)

Każde wykończenie liczy, według materiału, swoją powierzchnię **brutto** (obrys pomieszczenia, bez odliczeń: powierzchnia posadzki, obwód ścian razy wysokość każdego fragmentu) i **netto** (to, co zostaje wykonane: bez drzwi, okien i otworów w stropie, z ościeżami i posadzką pod drzwiami; udział ościeży jest w ilościach, jako RevealArea), a także swoją objętość. **Wnętrza → Zestawienie wykończeń** wymienia każde wykończone pomieszczenie według części i materiału, z sumami według materiału na końcu; jak każde zestawienie można je pokazać na wykresie, wyeksportować i umieścić na arkuszu. W IFC wykończenie jest elementem IfcCovering: FLOORING, CLADDING lub CEILING.

Wykończenie okłada to, co ściany pomieszczenia już mają: jeśli typy ścian mają własny tynk, daj ścianom pomieszczenia tylko to, co idzie na niego (farba, płytki), albo użyj typów ścian z samym rdzeniem.

![Wykończenia pomieszczeń: posadzka Łazienki, ściany z płytkami do 2100 i sufit](shots/room-finishes.webp)

![Właściwości pomieszczenia z jego wykończeniem](shots/room-finish-properties.webp)

## Pomieszczenia w 3D i w IFC

Pomieszczenia są ukryte w widoku 3D, aby nie zasłaniały budynku. **Widoczność / Grafika** w widoku 3D może je pokazać.

W eksporcie IFC każde pomieszczenie jest IfcSpace, z numerem, nazwą, funkcją, powierzchniami i kubaturą.

## Pomieszczenia z pliku IFC

**Plik → Importuj IFC…** wprowadza przestrzenie z pliku jako pomieszczenia, niezależnie od programu, który je zapisał (Revit, ArchiCAD, Allplan, Bonsai…):

- każde trafia na **swój własny poziom**, ten, na którym umieszcza je plik;
- jego **obrys** pochodzi z jego kształtu (wyciągnięcie, B-rep lub siatka albo narysowany obok rzut); zachowuje ten obrys, nie podąża za ścianami;
- jego **nazwa** to długa nazwa przestrzeni, a **numer** to jej nazwa (tak, jak zapisują je Revit i ArchiCAD); jego **wysokość** oraz **odsunięcie podstawy**, gdy zaczyna się powyżej poziomu (galeria, podniesiona podłoga), pochodzą z jego kształtu;
- jego **funkcja**, **dział** i **wykończenie podłogi** pochodzą z pliku, tam gdzie je podaje (typ obiektu lub kategoria przestrzeni, właściwość Department, pokrycie podłogi), więc schemat kolorów według funkcji lub działu koloruje je od razu;
- wszystko inne, co przestrzeń miała w pliku, jej zestawy właściwości i ilości (Pset_SpaceCommon, BaseQuantities, własne zestawy programu), zostaje zachowane i wymienione, tylko do odczytu, w sekcji **Właściwości IFC** w jej właściwościach, z filtrem, gdy jest ich wiele.

Komunikat importu mówi, ile pomieszczeń wczytano. Szukaj ich na rzutach ich poziomów (każde pokazuje swoją pieczątkę), w zestawieniu **Pomieszczenia** (**+** pod Zestawieniami w przeglądarce) lub na karcie IFC przeglądarki; widok 3D ukrywa pomieszczenia.

Przestrzeń, której kształt nie daje obrysu, i tak zostaje wczytana, z nazwą i właściwościami, jako pomieszczenie bez obrysu: jest wymieniona w zestawieniu, ale nigdzie nie jest rysowana. Zaznacz ją (karta IFC przeglądarki), otwórz rzut jej poziomu, a następnie **Znajdź kliknięciem** (kliknij wewnątrz jej ścian) lub **Rysuj obrys** w jej właściwościach.
