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
- Punkty trzymają się ścian, jak w każdym wymiarze. Przesuń ścianę, a łańcuchy podążą za nią.
- Uruchom polecenie ponownie na pomieszczeniu, aby utworzyć jego łańcuchy od nowa, na przykład po dodaniu drzwi. Stare łańcuchy są zastępowane, nie podwajane.
- Bardzo krótkie boki, poniżej 30 cm, są pomijane. Edytuj lub usuń dowolny łańcuch tak, jak narysowany przez siebie.

![Dwa pomieszczenia zwymiarowane od wewnątrz: łańcuch każdego boku z drzwiami i oknami na nim](shots/room-dimensions.webp)

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
