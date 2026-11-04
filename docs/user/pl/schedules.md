---
title: Zestawienia i wykresy
order: 95
summary: Zestawiaj drzwi, okna, nadproża, pomieszczenia i wszystko, co liczy dany moduł; dodawaj własne wiersze i kolumny, nadpisuj wartości modelu i twórz wykresy z tabeli.
---

## Tworzenie zestawienia

Zestawienie to tabela wypełniana przez model: drzwi i okna, nadproża, pomieszczenia, zbrojenie, drewno, deskowanie i nie tylko. Kliknij **+** obok **Zestawienia** w przeglądarce projektu i wybierz, co ma zestawiać, albo użyj narzędzia modułu (Drewno → Zestawienie tarcicy, Roboty pomocnicze → Zestawienie szalunków…). Kliknij je dwukrotnie w przeglądarce, aby otworzyć je na karcie. Zestawienie podąża za modelem: przesuń ścianę, dodaj okno, a tabela zmieni się razem z nim.

Zestawienia **Stolarka**, **drzwi**, **okien** i **nadproży** pokazują kartę dla każdego typu, z jego rysunkiem (domyślnie widok od wewnątrz) i jego wartościami wiersz po wierszu. Inne zestawienia są zwykłą tabelą, z jednym wierszem na pozycję i wierszem sum na końcu.

![Zestawienie stolarki z wierszami odporności ogniowej i producenta](shots/joinery-schedule.webp)

## Odporność ogniowa, producenci i inne parametry

Parametry drzwi lub okna same trafiają do zestawienia stolarki:

- **Odporność ogniowa.** Wiersz **Odporność ogniowa** odczytuje parametr o nazwie Fire rating, Fire Rated, FireRating lub Fire resistance. Umieść go w typie, aby miały go wszystkie drzwi tego typu. W **Architektura → Drzwi i okna** przycisk **Parametry · w zestawieniach** → **+ Odporność ogniowa** dodaje FireRating typu do Pset_DoorCommon (Pset_WindowCommon dla okna), gdzie przechowuje go IFC. Parametr, który sam dodasz do jednych drzwi, w ich właściwościach, również się pokazuje.
- **Każdy inny parametr.** **+ Producent**, **+ Kod produktu**, **+ Strona internetowa** lub twój własny parametr staje się osobnym wierszem w każdym zestawieniu stolarki. Własna wartość drzwi pokazuje się obok wartości ich typu, gdy drzwi jednego typu się różnią.
- **Wpisane w zestawieniu.** Wartość wpisana w wierszu parametru (kursywą) trafia do typu, a wszystkie drzwi tego typu podążają za nią. Wyczyść ją, aby usunąć parametr z typu.

![Parametry typu w konfiguratorze drzwi i okien](shots/joinery-params.webp)

**Nieużywane typy.** Typ, którego nie używają żadne drzwi ani okno w modelu, jest pomijany w zestawieniu. **Nieużywane typy** na pasku narzędzi zestawienia pokazuje je ponownie, wraz z ich liczbą.

## Własne wiersze i kolumny

Każde zestawienie można wypełniać ręcznie, jak arkusz kalkulacyjny:

- **Wiersz** dodaje własny wiersz. W zestawieniu stolarki ma on etykietę, a ty wpisujesz jego wartość dla każdego typu (Dostawca, Data dostawy…). W każdym innym zestawieniu jest to cały wiersz, wpisywany komórka po komórce (niezamodelowany taras, rezerwa); stoi nad wierszem sum.
- **Kolumna** dodaje własną kolumnę. W zestawieniu stolarki jest to karta bez typu za nią, z każdą wartością wpisaną ręcznie (brama garażowa zamawiana osobno). W każdym innym zestawieniu jest to kolumna po kolumnach modelu (Wykończenie podłogi, Uwagi…).
- **Każdą komórkę** tabeli pod rysunkiem można nadpisać. Wartość wpisana w miejsce wartości modelu jest oznaczona; **↺** przywraca wartość modelu. To, co wpiszesz, zostaje przy swoim wierszu: uwagi do pomieszczenia podążają za nim, gdy lista jest sortowana lub pomieszczenie zmienia nazwę. W zestawieniu **Pomieszczenia** numer, nazwa, funkcja, dział, wykończenie podłogi lub wysokość wpisane w wierszu pomieszczenia trafiają do samego pomieszczenia, jak gdyby wpisano je w jego właściwościach; pieczątka na rzucie i każde inne zestawienie podążają za tym.
- **×** obok własnego wiersza lub kolumny usuwa go razem z tym, co w nim wpisano.
- Strzałki w nagłówku tabeli **sortują** jej wiersze według tej kolumny: raz rosnąco, drugi raz malejąco, trzeci raz w kolejności modelu. Wiersz sum pozostaje ostatni.

Wszystko to się drukuje: na arkuszu, w PDF i DXF oraz w eksportach CSV i XLSX.

## Wykresy

**Wykresy** na pasku narzędzi zestawienia otwierają wykresy obok tabeli. **Nowy wykres** tworzy wykres z tabeli:

- **Kolumnowy**, **Słupkowy**, **Kołowy** lub **Liniowy**.
- **Kategorie z**: kolumna, która nazywa każdy słupek lub wycinek (Funkcja, Nazwa, Oznaczenie, Kondygnacja…).
- **Jak**: **Suma**, **Średnia**, **Minimum** lub **Maksimum** wartości kolumny (Powierzchnia, Razem, Objętość…) albo **Liczba wierszy**.
- **Najwyżej**: zachowuje tylko największe kategorie, a resztę sumuje jako Inne.
- **Tytuł**: własny tytuł albo ten, który wykres tworzy sam (Suma Powierzchni według Funkcji).

Silnik rysuje każdy wykres za tabelami, więc wygląda dokładnie tak samo w zestawieniu, na arkuszu, który umieszcza zestawienie, i w PDF. Podąża za modelem i za tym, co wpisałeś: zmień powierzchnię pomieszczenia lub dodaj własny wiersz, a wykres zmieni się razem z nimi. **✎** zmienia wykres, **×** go usuwa.

![Zestawienie pomieszczeń z własnym wierszem i wykresem kołowym powierzchni według funkcji](shots/schedule-chart.webp)

## Układy warstw typów

Zestawienie **Układy warstw typów** (**+** → **Układy warstw typów**) wymienia każdy typ ściany i stropu: jego oznaczenie, grubość całkowitą, liczbę elementów, które go używają, a potem jego warstwy po kolei z materiałami i grubościami.

**Rysunki układów warstw** na jego pasku rysuje każdy typ pod tabelą, w przekroju przez jego warstwy, w skali zestawienia (1:50, chyba że wybierzesz inną): ścianę tak, jak tnie ją rzut, strop tak, jak tnie go przekrój, warstwa 1 na górze. Każda warstwa jest wypełniona i zakreskowana jak jej materiał w przekrojach, lica rdzenia są rysowane grubiej, grubość całkowita jest zwymiarowana, a każda warstwa jest nazwana obok tak, jak nazywa ją tabela. Rysunki drukują się razem z tabelą, na arkuszu i w PDF. Nowe zestawienie układów warstw je rysuje; odznacz **Rysunki układów warstw**, aby zostawić samą tabelę.

Rysunki **Na każdej karcie** (widok od wewnątrz, widok od zewnątrz, rzut, przekrój) w **Ustawieniach** zestawienia należą do kart zestawień stolarki; zwykła tabela nie ma kart, więc nie są tam oferowane.

![Zestawienie układów warstw typów z układem każdego typu](shots/type-assemblies.webp)

## Eksport

**CSV** i **XLSX** na pasku eksportują zestawienie jako arkusz kalkulacyjny (XLSX ma jeden arkusz na każdą tabelę). W aplikacji desktopowej Lapin pyta, gdzie zapisać plik, i proponuje nazwę zestawienia; w przeglądarce plik jest pobierany. Wyeksportowane typy ścian i stropów zapisuje się tak samo.
