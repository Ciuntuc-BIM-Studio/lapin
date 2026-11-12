---
title: Obciążenia
order: 51
summary: Przypadki obciążenia, obciążenia powierzchniowe, liniowe i punktowe, obciążenia użytkowe pomieszczeń, obciążenia pochodne i kombinacje wg EN 1990, do obliczeń konstrukcji.
---

Grupa **Obciążenia** na karcie **Konstrukcja** zawiera obciążenia, które przyjmują obliczenia konstrukcji: **Przypadki obciążenia…**, **Obciążenie powierzchniowe**, **Obciążenie liniowe** i **Obciążenie punktowe**. Wartości są w kN, kN/m i kN/m².

![Grupa Obciążenia na karcie Konstrukcja](shots/ribbon-loads.webp)

> Wartości, które dostarcza Lapin (obciążenia użytkowe z EN 1991-1-1 i współczynniki z EN 1990, zalecane), są **orientacyjne**. Wpisz wartości ze swojego załącznika krajowego i sprawdź je, zanim użyjesz wyników.

## Przypadki obciążenia

**Przypadki obciążenia…** otwiera okno Obciążenia. **Dodaj przypadki standardowe** tworzy G1 (ciężar własny), G2 (wykończenie i ścianki działowe), Q-A (użytkowe, kategoria A), S (śnieg) i W (wiatr).

![Karta Przypadki w oknie Obciążenia](shots/loads-cases.webp)

Przypadek ma **Kod**, **Nazwę**, **Typ** (Stałe, Zmienne lub Wyjątkowe), **Źródło** (Ciężar własny / obciążenie stałe, Wykończenie, Użytkowe, Śnieg, Wiatr, Trzęsienie ziemi lub Zdefiniowane przez użytkownika) oraz, dla przypadku obciążenia użytkowego, **Kategorię**. Trzy pola ψ0, ψ1 i ψ2 to współczynniki kombinacyjne; zostaw je puste, aby przyjąć współczynniki kategorii z tabeli. Wartość, która nie jest liczbą od 0 do 1, jest oznaczana i nie jest zapamiętywana.

**Dodaj brakujące przypadki** tworzy jeden przypadek obciążenia użytkowego dla każdej kategorii używanej przez pomieszczenia, która go nie ma.

## Umieszczanie obciążeń

- **Obciążenie powierzchniowe**: kliknij narożniki, a potem naciśnij <kbd>Enter</kbd>.
- **Obciążenie liniowe**: kliknij jego dwa końce.
- **Obciążenie punktowe**: kliknij tam, gdzie działa.

Gdy narzędzie jest włączone, pasek wiersza poleceń oferuje **wartość** obciążenia i **przypadek obciążenia**, do którego trafia (domyślnie **Obciążenie użytkowe (Q-A)**). Obciążenie trafia na aktywną kondygnację.

![Obciążenie powierzchniowe 2,5 kN/m² narysowane w salonie](shots/loads-plan.webp)

## Co Lapin wylicza za ciebie

Tych nie rysujesz; Lapin wyprowadza je z modelu przy każdym zapytaniu i podążają za każdą zmianą:

- **Ciężar własny** stropów, ścian, słupów i belek, z gęstości ich materiałów, w przypadku, którego źródłem jest ciężar własny (G1).
- **Wykończenie stropów**, z ich warstw, oraz **ściany niekonstrukcyjne**, jako obciążenia liniowe, w przypadku wymieniającym wykończenie i ścianki działowe (G2).
- **Pomieszczenia**: obciążenie użytkowe kategorii pomieszczenia na jego powierzchni, w przypadku tej kategorii (Q-A, Q-B…).
- **Śnieg** i **wiatr**, gdy podasz wartości dla lokalizacji (zob. [Śnieg i wiatr](#śnieg-i-wiatr)).

Materiał bez gęstości jest pomijany, a Podsumowanie mówi który.

## Obciążenie użytkowe pomieszczenia

Zaznacz pomieszczenie: jego właściwości mają sekcję **Obciążenie użytkowe**. Jego **Kategoria** to **Z funkcji** (salon, sypialnia lub kuchnia to A, biuro B, magazyn E1, garaż F), chyba że wybierzesz inną, albo **None**. Poniżej jest **qk**, które daje kategoria.

![Obciążenie użytkowe pomieszczenia w jego właściwościach](shots/loads-room.webp)

## Tabela obciążeń użytkowych

Karta **Obciążenia użytkowe** wymienia każdą kategorię z jej zastosowaniem i **qk** w kN/m², jak zaleca EN 1991-1-1. Wpisz w polu wartość z załącznika krajowego; **Zmienione** wraca do wartości domyślnej.

![Karta Obciążenia użytkowe](shots/loads-imposed.webp)

## Śnieg i wiatr

Karta **Śnieg i wiatr** zawiera klimat lokalizacji. Lapin nie dostarcza map krajowych: odczytaj wartości dla swojej lokalizacji z **CR 1-1-3/2012** (śnieg, jego rysunek 3.1 lub załącznik A) i **CR 1-1-4/2012** (wiatr, jego rysunek 2.1 lub załącznik A) albo z załącznika krajowego, według którego pracujesz, i wpisz je.

![Karta Śnieg i wiatr](shots/loads-climate.webp)

- **Śnieg**: **Obciążenie śniegiem gruntu s0,k (kN/m²)**, **Współczynnik ekspozycji Ce** (pełna 0,8, częściowa 1,0, osłonięta 1,2), **Współczynnik termiczny Ct** (1,0, chyba że dach jest przeszklony i traci ciepło) i **Współczynnik ważności γIs** (1,0 dla zwykłych budynków). **Zastosuj śnieg**.
- **Wiatr**: **Bazowe ciśnienie prędkości wiatru qb (kN/m²)**, **Kategoria terenu** (od 0 do IV, od morza do centrum miasta) i **Współczynnik ważności γIw**. **Zastosuj wiatr**.

Zastosowanie ich tworzy przypadki, do których trafiają ich obciążenia:

- **S**, śnieg bez zasp, i **S+X**, **S-X**, **S+Y**, **S-Y**, śnieg nawiany przez wiatr wiejący w stronę każdego boku: na dachu dwuspadowym połać zwrócona ku wiatrowi bierze połowę. Zaspa, która tu niczego nie zmienia (żadna połać nie jest zwrócona ku temu wiatrowi), jest pomijana, a Podsumowanie to mówi.
- **W+X/s**, **W+X/p** i tak dalej dla czterech kierunków: wiatr wiejący w stronę +X, −X, +Y, −Y, raz z **ssaniem** (/s) dachów i raz z ich **parciem** (/p).

Układy śniegu i kierunki wiatru są alternatywami: kombinacja bierze tylko jeden z każdego rodzaju.

Co Lapin z nich wylicza, przy każdym zapytaniu:

- **Śnieg** na każdej połaci dachu, na m² rzutu: s = γIs · μ1 · Ce · Ct · s0,k, z μ1 = 0,8 do 30°, potem mniej, 0 przy 60° i w stromszych; na stropie, którego nic nie przykrywa (taras, dach płaski), μ1 = 0,8.
- **Wiatr** na ściany i dachy: obrys budynku to otoczka wypukła jego ścian i słupów; elewacje biorą strefy A do E, dachy płaskie F do I, dach dwuspadowy F do J, według współczynników EN 1991-1-4, przy ciśnieniu na wysokości budynku dla kategorii terenu. Elewacja przekazuje swój wiatr belkom, ryglom i krawędziom stropów wzdłuż niej, swój najniższy pas gruntowi; dach jego krokwiom lub płatwiom. Dachy kopertowe, ciśnienie wewnętrzne i zaspy śnieżne przy attykach nie są jeszcze wyprowadzane.

> Te wartości są **orientacyjne**: współczynniki są zalecanymi przez normy. Sprawdź je z załącznikiem krajowym.

## Podsumowanie

Karta **Podsumowanie** pokazuje dla każdego przypadku narysowane przez ciebie obciążenia i sumę w kN, łącznie i na każdej kondygnacji, a potem obciążenia pochodne jedno po drugim, z ich pochodzeniem. Ostrzega o pomieszczeniach, których kategoria nie ma przypadku, i notuje, co pominięto.

![Karta Podsumowanie](shots/loads-summary.webp)

## Kombinacje

Karta **Kombinacje** wymienia kombinacje przypadków wg EN 1990 z wartościami zalecanymi, orientacyjne: w stanie granicznym nośności 1,35 dla oddziaływań stałych i 1,5 dla wiodącego zmiennego, pozostałe z ich ψ0; w stanie granicznym użytkowalności kombinacje charakterystyczna, częsta i quasi-stała. Przypadek, który nic nie zawiera, nie bierze udziału.

![Karta Kombinacje](shots/loads-combinations.webp)

## Analiza

**Uruchom analizę** (w tej samej grupie i w oknie Obciążenia) analizuje ramę pod tymi obciążeniami: zobacz [Analiza konstrukcji](analysis.md).

## W pliku IFC

**Eksportuj IFC, Fragments lub BOS…** ma opcję **Structural Analysis View (model analityczny i obciążenia)**: plik niesie wtedy model analityczny ścian nośnych, stropów, słupów i belek, z przypadkami obciążenia, obciążeniami i kombinacjami, jako IFC Structural Analysis View. Zobacz [Import i eksport](import-export.md).
