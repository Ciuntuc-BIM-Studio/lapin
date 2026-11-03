---
title: Roboty pomocnicze
order: 55
summary: Rusztowania elewacyjne i szalunki stropów oraz belek, rozmieszczane na podstawie modelu przez ich konfiguratory.
---

Karta **Roboty pomocnicze** modeluje tymczasowe obiekty budowy: rusztowania elewacyjne oraz deskowanie stropów i belek. Lapin rozmieszcza je na podstawie modelu i liczy, czego potrzebują. Gdy model się zmienia, podążają za nim.

![Karta Roboty pomocnicze](shots/ribbon-site-works.webp)

Rusztowania i deskowania są **tymczasowe**: stawia się je i rozbiera w fazie **Realizacja**. Pierwsze wstawione dodaje tę fazę, jeśli projekt jej nie ma, i czyni ją fazą aktywną. Widoki wcześniejszych faz je pomijają. Zobacz **Zarządzaj → Fazy**.

![Rusztowanie wokół domu, deskowanie stropu nad parterem na podporach](shots/site-works-3d.webp)

## Rusztowania

- **Rusztowanie przy ścianach** (<kbd>WS</kbd>): zaznacz ściany (Shift+kliknięcie, aby dodać kolejne), a potem je naciśnij. Rusztowanie obiega ich zewnętrzne lica, w odległości od ściany ustalonej przez typ, aż do góry ścian. Samo zamyka swoje narożniki. Przesuń ścianę lub ją podwyższ, a rusztowanie podąży za nią.
- **Rusztowanie** (<kbd>FS</kbd>): narysuj jego linię na rzucie wzdłuż elewacji i naciśnij Enter, aby zakończyć. Rusztowanie stoi po lewej stronie linii. Drugą stronę wybierzesz w jego właściwościach.

Każdy prosty odcinek jest dzielony na długości przęseł typu, z jak najmniejszą liczbą przęseł sięgających jego końca. Poziomy rosną, aż górny pomost znajdzie się w zasięgu wysokości roboczej. Poręcz stoi nad górnym pomostem. Stopy stoją na terenie, na podstawkach śrubowych. Jeśli teren opada bardziej, niż podstawki mogą wyrównać, Lapin o tym informuje.

![Właściwości rusztowania](shots/scaffold-properties.webp)

We właściwościach możesz ustawić **wysokość roboczą**: zostaw ją pustą, aby przyjąć górę ścian. Możesz też wybrać, czy stopy stoją **na terenie** czy **na kondygnacji**, albo **odłączyć** rusztowanie od ścian.

Każde rusztowanie i deskowanie ma **typ**. Wybierasz go na górze właściwości albo przenosisz typ jednego elementu na inne poleceniem Dopasuj typ (MA). W sekcji **Parametry typu** są wartości ustawiane przez konfigurator: system, długości przęseł, szerokość, wysokość poziomu, sklejka, rozstawy, pozycja kosztorysowa. Tam też możesz je zmienić. Zmiana dotyczy typu, więc stosuje się do niej każdy element tego typu. Utwórz podobny (CS) rysuje nowy element tego samego typu. Typ utworzony w konfiguratorze przyjmie następne rusztowanie lub deskowanie.

**Typy rusztowań…** otwiera konfigurator. Ustala system (modułowy lub ramowy), długości przęseł, szerokość i wysokość poziomu, odległość od ściany, pomosty, poręcze i krawężniki, kotwy, podstawki śrubowe, przęsła komunikacyjne i siatkę ochronną. Elewacja pokazuje odcinek o wybranej długości i wysokości wraz z tym, co liczy. Gdy rusztowanie jest zaznaczone, konfigurator otwiera się na jego typie; kliknij inny na liście, aby zmienić ten. Lista podaje, ile rusztowań ma każdy typ. **Zapisz typ** stosuje zmianę do każdego rusztowania tego typu.

![Konfigurator rusztowań](shots/scaffold-configurator.webp)

## Szalunek

- **Szalunek** (<kbd>FW</kbd>): zaznacz stropy i belki, a potem je naciśnij. Każdy element dostaje deskowanie wszystkich swoich lic, które da się zadeskować.
- **Szalunek na licu** (<kbd>FF</kbd>): wskaż jedno lico. W 3D lico pod kursorem podświetla się ze swoją nazwą: spód stropu, spód belki lub jeden z jej boków. Na rzucie kliknij krawędź stropu lub wnętrze jego obrysu. Lico wskazane później trafia do tego samego deskowania.

**Deskowanie stropu** pokrywa jego spód sklejką na dźwigarach drugorzędnych, na dźwigarach głównych, na podporach. Belki pod stropem nie są deskowane ponownie, podobnie jak ściany i słupy, na których strop się opiera, ani jego otwory. Rozstawy pochodzą z tabeli typu, według grubości stropu. Podpory sięgają w dół do stropu poniżej, do terenu lub do kondygnacji pod nim. Swobodne krawędzie też są deskowane, na wysokość stropu plus nadstawka. We właściwościach wybierasz, które lica są deskowane, oraz kierunek dźwigarów głównych.

**Deskowanie belki** to jej spód, na poprzecznicach i podporach, oraz jej boki. Każdy bok ma wysokość belki pod betonowanym razem z nią stropem. Słupy i wyższe belki je przerywają. Możesz dodać listwy fazujące wzdłuż dolnych krawędzi.

**Typy szalunków…** otwiera konfigurator, dla stropów i dla belek. Dla stropów ustala system (dźwigary drewniane i podpory albo panele), sklejkę, dźwigary i ich długości katalogowe, tabelę rozstawów, krawędzie, podpory i liczbę użyć. Dla belek ustala poprzecznice, podpory, jarzma według wysokości belki, fazowanie i liczbę użyć. Przekrój pokazuje warstwy i podporę, którą silnik dobiera dla wybranej wysokości. Gdy deskowanie jest zaznaczone, konfigurator otwiera się na typie i rodzaju tego deskowania.

![Konfigurator szalunków](shots/formwork-configurator.webp)

## Ściany, słupy, wieńce i fundamenty

**Szalunek** i **Szalunek na licu** działają też na ścianach, słupach i fundamentach:

- **Ściana** dostaje oba lica i swoje wolne końce, bez drzwi i okien. Ściana warstwowa z wieńcem lub pasem betonowym dostaje tylko je. We właściwościach możesz wybrać oba lica, wieńce lub wolne końce.
- **Słup** dostaje swoje boki, aż do stropu lub belki betonowanej na nim.
- **Fundament** dostaje boki każdego stopnia stopy albo boki podstawy i trzonu ławy.

To, co przylega do lica, jest pomijane: inna ściana, słup. Strop betonowany na górze kończy lico na swoim spodzie. Typ ustala panele (ich szerokości i wysokości), ściągi przez ściany, zamki słupów i podpory ustawcze.

![Konfigurator dla ścian i słupów](shots/wall-formwork-configurator.webp)

## Plan szalunku

**Plan szalunku** tworzy rzut aktywnej kondygnacji w fazie Realizacja, przecięty metr nad stropem, **widziany od dołu**: rzut sufitu. Podpory są przecięte. Deskowanie nad nimi jest rysowane tak, jak widać je patrząc w górę: dźwigary główne i drugorzędne, sklejka i krawędzie. Leży tak, jak leży rzut, więc oba się zgadzają.

**Plan szalunku z góry** tworzy ten sam rzut widziany z góry, z deskowaniem narysowanym przerywaną linią nad przekrojem. Każdy rzut można przełączać między nimi we właściwościach (**Widziane z**) lub w oknie **Zakres widoku**.

![Plan szalunku parteru](shots/formwork-plan.webp)

## Co liczą

Każde rusztowanie i deskowanie podaje swoje ilości we właściwościach:

- powierzchnię rusztowania, przęsła, poziomy, stojaki, pomosty i kotwy;
- powierzchnię kontaktu deskowania według lic, podpory według rozmiaru i dźwigary według długości katalogowej.

**Zestawienie szalunków** i **Zestawienie rusztowań** dodają swoje tabele do przeglądarki projektu, w sekcji Zestawienia, aby umieścić je na arkuszu lub wyeksportować.

Zestawienie szalunków kończy się wierszem dla każdego typu:

- **komplet**: najwięcej deskowania betonowanego naraz. Liczony na fazę realizacji, gdy prace są podzielone na kilka, w przeciwnym razie na kondygnację.
- **do zakupu**: komplet albo całość podzielona przez liczbę użyć typu, zależnie od tego, co większe.

Każdy typ ma swoją **pozycję kosztorysową**: kod w katalogu norm używanym w Twoim kosztorysie i jego opis. Wpisujesz je w konfiguratorze, a zestawienie pokazuje je w każdym wierszu.

W IFC są zapisywane jako zespoły (`IfcElementAssembly` FORMWORK i SCAFFOLD) ze statusem TEMPORARY. Elementy składowe to pręty, płyty i akcesoria. Każdy ma swój zasób produktu budowlanego, a deskowanie jest połączone z elementem, który deskuje.
