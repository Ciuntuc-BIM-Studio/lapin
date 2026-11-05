---
title: Testy kolizji
order: 58
summary: Znajdź miejsca, w których elementy modelu się przenikają lub zbliżają za bardzo: zestawy wyszukiwania według reguł, testy twarde i odstępów między nimi, kolizje śledzone od uruchomienia do uruchomienia, pokazane w 3D i wysłane jako BCF.
---

**Zarządzaj → Testy kolizji** znajduje miejsca, w których elementy modelu się przenikają lub zbliżają bardziej, niż powinny. Działa tak samo na elementach Lapina i na połączonych plikach IFC i Fragments, więc koordynuje też modele przysyłane przez inne branże.

![Testy kolizji obok widoku 3D](shots/clash-dialog.webp)

## Testy i zestawy

**Test kolizji** porównuje dwa **zestawy wyszukiwania**: zestaw A z zestawem B albo zestaw z samym sobą. Projekt zaczyna z trzema zestawami (Structure, Services, Architecture) i trzema testami twardymi: Structure × Services, Services × Services i Architecture × Services.

- Testy **twarde** znajdują elementy, które się przenikają. Ich **tolerancja** (domyślnie 5 mm) pomija tak płytkie przenikania, żeby wykończenie wchodzące 3 mm w ścianę nie było zgłaszane.
- Testy **odstępów** znajdują elementy bliższe niż zadana odległość (domyślnie 50 mm), a także przenikania.

Niektóre pary nigdy nie są testowane, bo mają się stykać: ściana i drzwi lub okno w jej otworze, części jednego zespołu, ściany połączone w narożniku i elementy przymocowane do siebie (belka oparta na słupie, ściana dołączona do dachu).

![Ustawienia testu](shots/clash-test.webp)

## Uruchamianie i śledzenie kolizji

**Uruchom test** uruchamia test na wierzchu; **Uruchom wszystkie** uruchamia każdy włączony test. Nic nie uruchamia się samo: wyniki zostają w projekcie, dopóki nie uruchomisz testu ponownie, a okno mówi, kiedy model się od tego czasu zmienił.

Każda kolizja zachowuje swój numer między uruchomieniami. Kolizja znaleziona pierwszy raz jest **Nowa**; znaleziona ponownie jest **Aktywna**. Oznacz ją ręcznie jako **Sprawdzoną** lub **Zatwierdzoną**, przypisz ją komuś (**Przypisano do**) i napisz **Notatki**. Kolizja, której uruchomienie już nie znajduje, staje się **Rozwiązana**; jeśli wróci, jest znów otwarta, z tym samym numerem.

Kliknij kolizję, aby zobaczyć ją w 3D. Lapin zaznacza oba elementy, stawia ramkę przekroju wokół miejsca ich spotkania i patrzy na nie z boku, żebyś widział rurę przechodzącą przez ścianę, a nie lico ściany. **same w 3D** ukrywa resztę; strzałki przechodzą przez kolizje jedna po drugiej. Okno zostaje po prawej, więc możesz ruszać kamerą, gdy jest otwarte.

![Rura przez ścianę, w kadrze kolizji](shots/clash-3d.webp)

**BCF** robi z każdej otwartej kolizji (lub zaznaczonych) uwagę typu Clash z jej punktem widzenia i zapisuje je jako plik BCF dla innych narzędzi BIM; wysłane ponownie aktualizują te same uwagi. **CSV** zapisuje listę do arkusza.

## Zestawy wyszukiwania

**Zestawy wyszukiwania** zawiera reguły wybierające elementy zestawu. Reguła czyta pole elementu (klasę, typ predefiniowany, typ, kondygnację, materiał, nazwę, plik połączony lub dowolny parametr) z operatorem: jest, jest jednym z, nie jest, nie jest żadnym z, zawiera, nie zawiera, co najmniej, co najwyżej, pomiędzy. Liczby czyta się tak, jak ludzie je piszą: „600 mm” to 600.

Zestaw bierze elementy spełniające dowolną regułę albo wszystkie reguły. Reguła „nie” zawsze usuwa elementy, a zestaw bez reguły dodającej elementy obejmuje wszystko. Liczba obok zestawu pokazuje w trakcie pisania, ile elementów biorą reguły.

![Reguły zestawu wyszukiwania](shots/clash-sets.webp)

## Grupy, raporty i chmurki

Lista grupuje kolizje **według kondygnacji**, **według elementu** (element w największej liczbie kolizji je bierze, potem następny), **według klas** lub **według statusu**. Pole grupy zaznacza wszystkie jej kolizje, żeby nadać im status razem.

![Kolizje pogrupowane według elementu](shots/clash-groups.webp)

- **XML** zapisuje kolizje tak, jak Navisworks zapisuje raporty kolizji, w ich grupach, żeby inne narzędzia koordynacji je odczytały.
- **PDF** drukuje listę na A4, pogrupowaną tak samo.
- **HTML** tworzy jedną stronę z obrazem każdej otwartej kolizji, po kolei w kadrze widoku 3D.
- **Oznacz w widoku** rysuje otwarte kolizje w rzucie, przekroju lub elewacji na wierzchu jako czerwone chmurki z etykietą test.kolizja (2.14: drugi test, kolizja 14), tylko na ekranie. Kliknięcie chmurki zaznacza element.

![Kolizje jako chmurki na rzucie](shots/clash-marks.webp)

Projekt zaczyna też z czwartym testem, **Openings for MEP**: sprawdzeniem z Zarządzaj → Wykrywanie kolizji… uruchamianym jako test, żeby jego problemy były numerowane i śledzone jak inne. Jego głębokość to brak przewodu w otworze.

Test twardy znajduje też element w całości wewnątrz innego, bez przenikających się ścianek: rurę zapomnianą w stropie. Jego głębokość to najmniejsza szerokość elementu wewnętrznego.

> Instalacje Lapina mogą same wycinać swoje otwory w ścianach i stropach (Zarządzaj → Kolejność cięcia). Wtedy rura przez ścianę nie jest kolizją: ściana ma swój otwór. **Zarządzaj → Wykrywanie kolizji…** sprawdza te otwory.

Testy kolizji opierają się na projekcie, liczbach i testach [BimGo](https://github.com/aussieBIMguru/BimGo) autorstwa Aussie BIM Guru (MIT-0), przepisanych w Lapinie.
