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

Materiał bez gęstości jest pomijany, a Podsumowanie mówi który.

## Obciążenie użytkowe pomieszczenia

Zaznacz pomieszczenie: jego właściwości mają sekcję **Obciążenie użytkowe**. Jego **Kategoria** to **Z funkcji** (salon, sypialnia lub kuchnia to A, biuro B, magazyn E1, garaż F), chyba że wybierzesz inną, albo **None**. Poniżej jest **qk**, które daje kategoria.

![Obciążenie użytkowe pomieszczenia w jego właściwościach](shots/loads-room.webp)

## Tabela obciążeń użytkowych

Karta **Obciążenia użytkowe** wymienia każdą kategorię z jej zastosowaniem i **qk** w kN/m², jak zaleca EN 1991-1-1. Wpisz w polu wartość z załącznika krajowego; **Zmienione** wraca do wartości domyślnej.

![Karta Obciążenia użytkowe](shots/loads-imposed.webp)

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
