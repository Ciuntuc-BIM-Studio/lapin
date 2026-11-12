---
title: Analiza konstrukcji
order: 52
summary: Uruchom liniową analizę statyczną ramy pod swoimi obciążeniami, odczytaj wykresy sił, momentów i odkształcenia w 3D oraz ustaw zwolnienia na końcach prętów.
---

**Uruchom analizę** w grupie **Obciążenia** na karcie **Konstrukcja** (i w oknie Obciążenia) analizuje pręty modelu pod obciążeniami i kombinacjami z [Obciążeń](loads.md) i pokazuje działające w nich siły.

![Grupa Obciążenia z poleceniem Uruchom analizę](shots/ribbon-analysis.webp)

> Wyniki są **orientacyjne**. Obciążenia, kombinacje i analizę musisz sprawdzić samodzielnie, zanim na nich oprzesz decyzje.

## Co jest analizowane

- **Pręty** (słupy, belki, elementy stalowe i drewniane) wchodzą z własną sztywnością, wynikającą z materiału i przekroju. Pręt, którego materiał nie ma modułu sprężystości, jest pomijany i wymieniony na liście.
- **Ściany nośne** są podporami: podtrzymują spoczywające na nich pręty, ale nie są analizowane jako ściany.
- **Stropy i płyty** nie przenoszą obciążeń jako tarcze; przekazują obciążenia prętom i ścianom pod nimi według **reguły 45°**: dwusieczne narożników dzielą płytę na pola, które niesie każdy bok, a każde staje się obciążeniem trapezowym lub trójkątnym na pręcie poniżej.
- **Dachy z konstrukcją** przekazują obciążenie na każdą krokiew według jej **szerokości pasma**, do połowy odległości do sąsiadów; śnieg liczony jest na rzut poziomy.
- **Ciężar własny** prętów liczony jest wzdłuż nich, a obciążenia narysowane przez Ciebie i pochodne trafiają tam, gdzie coś pod sobą znajdą. Obciążenie, które nic nie znajduje, jest pomijane i zgłaszane.

## Uruchamianie

Wybierz **Uruchom analizę**. Otwiera się okno **Wyniki analizy**:

![Okno Wyniki analizy](shots/analysis-results.webp)

- Podsumowanie: pręty, węzły i niewiadome obliczenia oraz czas, jaki zajęło.
- Dla każdego przypadku **suma jego obciążeń** wobec **sumy reakcji** i czy się **równoważą** (ptaszek), czy nie (krzyżyk: nie polegaj na tych wynikach). Obciążenia nieprzypisane podano w kN.
- Co **pominięto** w analizie i dlaczego.
- **Największe i najmniejsze** siły i momenty, największe przemieszczenie i ugięcie (ze stosunkiem do rozpiętości, L/300) wybranego przypadku, kombinacji lub obwiedni.

**Pokaż wykresy** zamyka okno i pokazuje wyniki w widoku 3D.

## Wykresy w 3D

Karta nad widokiem 3D wybiera, co jest rysowane:

![Moment zginający obwiedni w stanie granicznym nośności](shots/analysis-moments.webp)

- **Co**: przypadek obciążenia, kombinacja lub **obwiednia** kombinacji w stanie granicznym nośności (SGN) lub użytkowalności (SGU): największa i najmniejsza wartość w każdym punkcie.
- **Wielkość**: **N** siła osiowa (rozciąganie dodatnie), **Vy** i **Vz** siły tnące, **T** skręcanie, **My** i **Mz** momenty zginające (**Mz** to oś mocna belki poziomej, dodatni przy rozciąganiu dołem) lub kształt **Odkształcona**.
- **Skala**: rozmiar wykresu na ekranie; sam model się nie zmienia.
- **Reakcje**: strzałki przy podporach z wartościami w kN.

Każdy pręt pokazuje linię bazową, krzywą, kreskowanie między nimi i wartości w ekstremach; czerwony oznacza dodatnie, niebieski ujemne. Momenty rysowane są po stronie, w której pręt jest rozciągany.

![Kształt odkształcony](shots/analysis-deformed.webp)

## Zwolnienia na końcach

Zaznacz słup, belkę lub element: w jego właściwościach jest sekcja **Analiza** ze zwolnieniami **Początek (i)** i **Koniec (j)**.

![Sekcja Analiza belki](shots/analysis-bar.webp)

- **Domyślnie**: to, co mówi połączenie (nakładka, blacha węzłowa lub stężenie daje przegub), drewniany element dachu przegubowy wokół osi mocnej, każdy inny pręt utwierdzony.
- **Utwierdzony**: koniec przenosi momenty. **Przegubowy**: obraca się swobodnie wokół obu osi zginania.
- **Własny**: sześć pól, po jednym na każdy stopień swobody (przemieszczenie wzdłuż pręta, wzdłuż każdej osi ścinania, skręcanie, obrót wokół każdej osi zginania); zaznaczony stopień swobody jest zwolniony.

Zmiana to jeden krok cofania.

## Gdy model się zmienia

Wyniki są liczone z modelu takiego, jaki jest, i nigdy nie są zapisywane. Gdy zmienisz model, wykresy znikają z widoku, a karta o tym informuje:

![Karta po zmianie](shots/analysis-pick.webp)

**Uruchom ponownie** analizuje nowy model.

## Mechanizm

Jeśli konstrukcja może się poruszać bez oporu (za dużo zwolnień lub część, której nic nie trzyma), analiza się zatrzymuje i mówi gdzie:

![Mechanizm](shots/analysis-mechanism.webp)

Wskazuje węzeł, stopień swobody i pręty, których to dotyczy; **Zaznacz elementy, których to dotyczy** zaznacza je, abyś mógł je poprawić, zwykle utwierdzając koniec lub dodając podporę.
