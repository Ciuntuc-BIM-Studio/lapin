---
title: Analiza konstrukcji
order: 52
summary: Uruchom liniową analizę statyczną ramy pod swoimi obciążeniami, odczytaj wykresy sił, momentów i odkształcenia w 3D oraz ustaw zwolnienia na końcach prętów.
---

**Uruchom analizę** w grupie **Obciążenia** na karcie **Konstrukcja** (i w oknie Obciążenia) analizuje pręty, płyty i ściany modelu pod obciążeniami i kombinacjami z [Obciążeń](loads.md) i pokazuje działające w nich siły.

![Grupa Obciążenia z poleceniem Uruchom analizę](shots/ribbon-analysis.webp)

> Wyniki są **orientacyjne**. Obciążenia, kombinacje i analizę musisz sprawdzić samodzielnie, zanim na nich oprzesz decyzje.

## Co jest analizowane

- **Pręty** (słupy, belki, elementy stalowe i drewniane) wchodzą z własną sztywnością, wynikającą z materiału i przekroju. Pręt, którego materiał nie ma modułu sprężystości, jest pomijany i wymieniony na liście.
- **Stropy i płyty żelbetowe** są analizowane jako **powłoki**: każda jest dzielona na małe elementy (siatkę, domyślnie 0,5 m), które się zginają i rozciągają, a swoje węzły dzielą z belkami, słupami i ścianami, które spotykają. Płyta sama przenosi swoje obciążenia na to, co ją podpiera, dzięki swojej sztywności i ich sztywności. Belka pod płytą zachowuje swoją oś pod nią i pracuje z płytą jako belka teowa.
- **Żelbetowe ściany nośne** są również powłokami: stoją na swoich fundamentach (ściana bez niczego zamodelowanego pod nią jest podparta wzdłuż swojej stopy) i przenoszą spoczywające na nich stropy oraz wiatr i inne obciążenia boczne w ich płaszczyźnie.
- **Ściany murowane** i ściany z innych materiałów są podporami: podtrzymują spoczywające na nich płyty i pręty, ale nie są analizowane jako ściany.
- **Stropy z innych materiałów** (strop drewniany) przekazują obciążenia prętom i ścianom pod nimi według **reguły 45°**: dwusieczne narożników dzielą płytę na pola, które niesie każdy bok, a każde staje się obciążeniem trapezowym lub trójkątnym na pręcie poniżej.
- **Dachy z konstrukcją** przekazują obciążenie na każdą krokiew według jej **szerokości pasma**, do połowy odległości do sąsiadów; śnieg liczony jest na rzut poziomy.
- **Cięgna i ściągi** (pełny pręt okrągły, który usztywnia, ściąg lub lina) pracują osiowo: są przegubowe na końcach, a ich ciężar idzie na końce, więc nie uginają się jak belki. Ustawienie **Pręt osiowo obciążony** we właściwościach pręta w sekcji **Analiza** to zmienia: **Według przekroju i użycia**, **Tak (cięgno lub ściąg)** albo **Nie (belka)**.
- **Belki o różnej wysokości** schodzące się przy słupie zachowują każda swój poziom; słup ma jeden węzeł, a belki są z nim związane sztywnym ramieniem.
- **Wiatr na elewacjach** idzie do belek, rygli i krawędzi stropów wzdłuż nich; pas u ich stóp idzie wprost do gruntu i jest wymieniony osobno od sum prętów.
- **Ciężar własny** prętów liczony jest wzdłuż nich, a obciążenia narysowane przez Ciebie i pochodne trafiają tam, gdzie coś pod sobą znajdą. Obciążenie, które nic nie znajduje, jest pomijane i zgłaszane.

## Uruchamianie

Wybierz **Uruchom analizę**. Otwiera się okno **Wyniki analizy**:

![Okno Wyniki analizy](shots/analysis-results.webp)

- Podsumowanie: pręty, elementy powłokowe, węzły i niewiadome obliczenia oraz czas, jaki zajęło.
- Dla każdego przypadku **suma jego obciążeń** wobec **sumy reakcji** i czy się **równoważą** (ptaszek), czy nie (krzyżyk: nie polegaj na tych wynikach). Obciążenia nieprzypisane podano w kN.
- Co **pominięto** w analizie i dlaczego.
- **Największe i najmniejsze** siły i momenty, największe przemieszczenie i ugięcie (ze stosunkiem do rozpiętości, L/300) wybranego przypadku, kombinacji lub obwiedni; pod nimi największe i najmniejsze wartości na **płytach i ścianach** oraz gdzie występują.
- **Powłoki**: które płyty i ściany są analizowane jako powłoki (**Płyty i ściany**, **Tylko płyty**, **Tylko ściany** lub **Brak**: płyty według reguły 45° i ściany jako podpory) oraz rozmiar **Siatki** w metrach. Drobniejsza siatka jest dokładniejsza i wolniejsza; wąski element zawsze dostaje co najmniej cztery elementy na szerokość. Zmiana to jeden krok cofania; **Uruchom ponownie** ją uwzględnia.

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

## Płyty i ściany

Gdy płyty lub ściany są analizowane jako powłoki, karta ma drugi rząd przycisków, **Płyty i ściany**, a powłoki są kolorowane pasmami według wybranej wielkości (drugie kliknięcie zdejmuje kolory):

![Moment my stropu, w pasmach](shots/analysis-slab.webp)

- **w**: przemieszczenie w poprzek płyty lub ściany (w górę dodatnie na płycie).
- **mx**, **my**: momenty zginające w kierunkach x i y płyty (globalne X i Y na płycie; wzdłuż i w górę na ścianie), w kN·m na metr. Na płycie **dodatni oznacza rozciągany dolny pas** (przęsło), ujemny górny (nad podporą); na ścianie lico po prawej stronie jej kierunku.
- **mxy**: moment skręcający; **nx**, **ny**, **nxy**: siły w płaszczyźnie płyty (rozciąganie dodatnie), w kN na metr; **vx**, **vy**: siły poprzeczne.

Legenda na karcie podaje wartości każdego pasma i jednostkę:

![Legenda warstwic](shots/analysis-legend.webp)

Te same pasma są rysowane na rzucie kondygnacji płyty, na ekranie; arkusze jeszcze ich nie drukują. Dla obwiedni każdy punkt pokazuje tę ze swojej najmniejszej i największej wartości, która ma większy moduł.

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
