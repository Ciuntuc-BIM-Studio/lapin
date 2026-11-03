---
title: Schody i pochylnie
order: 42
summary: Umieść schody lub pochylnię między kondygnacjami albo do dowolnej wysokości, skonfiguruj je w typie i zrób kilka stopni do ganku lub drzwi.
---

## Schody

Wybierz **Architektura → Schody** (<kbd>ST</kbd>). Kliknij początek schodów (środek pierwszego podstopnia), a potem punkt w kierunku, w którym wchodzą. Przy schodach w kształcie L lub U trzecie kliknięcie po lewej lub prawej stronie biegu określa, w którą stronę skręcają. Schody wchodzą ze swojej kondygnacji na kondygnację wyżej. Lapin oblicza liczbę podstopni, wysokość podstopnia i głębokość stopnia z wysokości i reguł typu.

Zaznacz schody, aby zmienić je w panelu Właściwości:

- **Typ schodów.** Kształt, reguły, konstrukcja i balustrada pochodzą z typu, ustawianego w **Architektura → Typy schodów**.
- **Szerokość**, **Zwrot**, **Odsunięcie podstawy** i **Odsunięcie góry.**
- **Różnica wysokości.** Pozostaw puste, a schody wejdą na kondygnację wyżej. Wartość nadaje schodom własną wysokość, niezależnie od kondygnacji. Użyj jej dla kilku stopni między dwoma poziomami na tej samej kondygnacji.
- **Stały.** Liczba podstopni, wysokość podstopnia, głębokość stopnia i długość spocznika, gdy chcesz je ustalić. Pozostaw puste, aby decydowały reguły.
- **Sprawdzenia.** Każda liczba porównana z regułami typu, na zielono lub na czerwono.

Schody żądają od stropu powyżej otworu nad sobą. Odznacz **Otwór w stropie powyżej**, jeśli strop ma pozostać cały.

## Kształty schodów i gotowe typy

W **Architektura → Typy schodów** karta **Kształt** oferuje: **Proste**, **Proste ze spocznikiem**, **Kształt L** i **Kształt U** (każde skręcające na spoczniku lub ze **stopniami zabiegowymi**; U bez prześwitu to schody dwubiegowe zwrotne), **Kręcone** (wokół słupa, z polem **Promień słupa**), **Zakrzywione** (z polem **Promień wewnętrzny**) i **Rozdzielone (dwuramienne)**: bieg główny środkiem do spocznika, a potem dwa biegi powrotne po obu stronach. Dla schodów kręconych lub zakrzywionych **Obrót o (°)** określa, jak daleko się obracają; pozostawiony pusty wynika z podstopni i stopnia, a podany wyznacza głębokość stopnia. Każde schody mogą go też zmienić w swoich właściwościach.

Biblioteka dodaje do projektu gotowe typy: schody dwubiegowe zwrotne, schody zabiegowe, schody kręcone, schody zakrzywione, schody wspornikowe i schody rozdzielone.

Na karcie **Budowa** pole **Wykonane jako** wybiera płytę biegową, pełne stopnie na gruncie, **Stopnie wspornikowe (wiszące)** (każdy stopień to płyta utwierdzona w ścianie) lub **Stopnie na policzkach**.

**Balustrada** ma swój **Wzór**: **Słupki i poręcz**, **Tralki** (w odstępie co najwyżej **Rozstaw tralek**), **Panele szklane**, **Linki** lub **Balustrada pełna**; kwadratowa lub okrągła **Poręcz**, jej wysokość, **Odsunięcie od krawędzi**, o ile **Przedłużenie poza końce** oraz jej kolor. W panelu Właściwości pole **Balustrada** umieszcza ją na innych bokach niż w typie, dla jednych schodów.

![Typ schodów kręconych z ponumerowanymi stopniami](shots/stair-spiral.webp)

### Numerowane stopnie

W sekcji **W rzucie** karty **Kształt**: **Numeruj stopnie** wpisuje numer każdego stopnia w kółku, od pierwszego podstopnia w górę; **Etykieta** wpisuje liczbę podstopni, wysokość podstopnia i głębokość stopnia oraz szerokość przy początku; **„UP” u podstawy strzałki** zaznacza, gdzie schody się zaczynają. Rzuty i arkusze je pokazują.

![Numerowane stopnie na rzucie, schody rozdzielone i schody wspornikowe](shots/stair-numbers-plan.webp)

![Schody kręcone, rozdzielone i wspornikowe w widoku 3D](shots/stair-shapes-3d.webp)

## Schody wejściowe (ganek)

Stopnie do drzwi wejściowych mają własną wysokość: od terenu, kilka stopni poniżej parteru, do ±0,00. Lapin ma dla nich gotowy typ schodów, **Schody wejściowe**: szerokość 2 m, trzy podstopnie po 150 mm, pełny beton wylany na gruncie, bez balustrady.

![Schody wejściowe przed gankiem, na rzucie](shots/entrance-steps-plan.webp)

1. **Ganek.** Narysuj podest jako strop: wybierz **Architektura → Strop** (<kbd>SB</kbd>) i wskaż jego obrys na zewnątrz ściany, przed drzwiami. Góra stropu leży na poziomie jego kondygnacji (±0,00 na parterze), a grubość wynika z typu. Aby zmienić grubość, utwórz typ stropu dla ganków w **Architektura → Typy stropów**, na przykład 150 mm betonu.
2. **Stopnie.** Wybierz **Architektura → Schody wejściowe**. Kliknij środek krawędzi ganku, a potem po stronie, w którą wychodzą stopnie. Stopnie dochodzą do krawędzi ganku i kończą się na ±0,00. Zaczynają się 450 mm niżej: ich **Odsunięcie podstawy** wynosi −450 mm.
3. **Wysokość.** Zaznacz stopnie. W polu **Różnica wysokości** wpisz rzeczywistą różnicę między terenem a gankiem, na przykład 300. Góra zostaje przy ganku, a początek przesuwa się w górę: odsunięcie podstawy staje się −300 mm, a stopnie mają dwa podstopnie. **Szerokość** nadaje im szerokość ganku, na przykład 2400.

![Właściwości schodów wejściowych: wysokość, odsunięcie podstawy, poziomy](shots/entrance-steps-properties.webp)

**Poziomy** pokazuje, gdzie stopnie się zaczynają i kończą, na przykład −0,45 → ±0,00. Przy jednym podstopniu (wysokość 150 mm) stopniem jest sama krawędź ganku i nie ma stopnia do zbudowania. Nadaj stopniom dwa podstopnie lub więcej, aby mieć stopnie przed gankiem.

Typ jest dodawany do projektu przy pierwszym użyciu. Aby go zmienić, otwórz **Architektura → Typy schodów** i wybierz **Schody wejściowe**:

- **Kształt → Wznosi się → Własna wysokość.** Wysokość typu, pokonywana **Do poziomu swojej kondygnacji, od dołu (wejście)**; każde schody mogą ją nadal zmienić polem **Różnica wysokości**.
- **Reguły → Zestaw reguł → schody wejściowe.** Podstopnie 120–170 mm i stopnie 300–400 mm, z dopuszczalnym pojedynczym podstopniem.
- **Budowa → Wykonane jako.** **Pełne, betonowane na gruncie** wypełnia stopnie aż do ich podstawy. **Płyta biegu pod stopniami** buduje je jak schody wewnętrzne.

![Typ Schody wejściowe w konfiguratorze schodów](shots/stair-types.webp)

Aby dowolny inny typ schodów wchodził na własną wysokość, wybierz **Wznosi się → Do → Własna wysokość** na jego karcie **Kształt** (zobacz następną sekcję). Aby zmienić umieszczone schody w schody wejściowe, wybierz **Schody wejściowe** jako ich **Typ schodów**: przesuną się w dół tak, by ich góra była na poziomie kondygnacji.

![Dom z gankiem i schodami wejściowymi w widoku 3D](shots/entrance-steps-3d.webp)

W IFC stopnie są obiektem IfcStair typu STRAIGHT_RUN_STAIR. Ich bieg to IfcStairFlight z NumberOfRisers, NumberOfTreads, RiserHeight i TreadLength, a Pset_StairCommon zawiera te same liczby. Ganek jest obiektem IfcSlab.

## Schody na dowolną wysokość

Schody nie muszą prowadzić z jednej kondygnacji na następną. Na karcie **Kształt** typu pole **Wznosi się → Do** wybiera **Kondygnacja powyżej** lub **Własna wysokość**, z polem **Różnica wysokości**, a **Zaczyna się** określa, gdzie się zaczynają:

- **Do poziomu swojej kondygnacji, od dołu (wejście).** Kończą się na poziomie kondygnacji, a zaczynają o wysokość niżej, jak schody wejściowe.
- **Od poziomu swojej kondygnacji w górę (taras, podest).** Zaczynają się na poziomie kondygnacji i pokonują wysokość: na przykład 1,20 m na taras lub rampę załadunkową.

Każde umieszczone schody mogą pokonywać inną wysokość: wpisz ją w polu **Różnica wysokości** w ich właściwościach (puste: wysokość typu). Przy wchodzeniu od poziomu nowa wysokość zachowuje początek w miejscu; przy wchodzeniu od dołu zachowuje górę.

## Pochylnie

Wybierz **Architektura → Pochylnia** (<kbd>RU</kbd>). Kliknij środek dolnego końca pochylni, a potem punkt w kierunku, w którym się wznosi; przy kształcie L lub pochylni zawracającej trzecie kliknięcie po lewej lub prawej stronie biegu określa, w którą stronę skręca. Za pierwszym razem projekt otrzymuje z biblioteki **Pochylnię 6%**: szerokość 1,20 m, nachylenie 6%, biegi najwyżej 9 m ze spocznikami 1,50 m między nimi, płyta żelbetowa pod wykończeniem, pochwyty po obu stronach. Wznosi się na kondygnację wyżej, chyba że jej typ lub **Różnica wysokości** stanowią inaczej.

**Architektura → Pochylnia wejściowa** umieszcza się jak schody wejściowe: kliknij środek krawędzi ganku, a potem stronę, w którą wychodzi pochylnia. Wznosi się o 450 mm od terenu do ±0,00, pełna na gruncie, a **Różnica wysokości** zmienia ją tak jak stopnie.

Typy pochylni tworzy się w tym samym konfiguratorze, **Architektura → Typy schodów**, przyciskiem **Nowy typ pochylni**, albo z bibliotecznych **Pochylnia zawracająca** i **Pochylnia wejściowa**:

- **Kształt.** **Proste**, **Prosta, ze spocznikami**, **Kształt L** lub **Zygzakowata (tam i z powrotem)** z polem **Odstęp między biegami**; **Szerokość**, **Nachylenie** w procentach, **Najdłuższy bieg** (przy większej długości pochylnia dzieli się na biegi równej długości ze spocznikami między nimi) i **Długość spocznika**. Długość w rzucie to wysokość podzielona przez nachylenie: 3 m przy 6% to 50 m. **Wznosi się** działa jak dla schodów.
- **Reguły.** **Maksymalne nachylenie** (domyślnie 8%), **Minimalna szerokość** i **Prześwit**. Sprawdzenia pokazują na czerwono nachylenie większe niż dopuszczają reguły, bieg dłuższy od najdłuższego oraz spocznik krótszy niż 1,50 m lub szerokość pochylni.
- **Budowa.** **Pochyła płyta żelbetowa** o grubości z pola **Grubość płyty** albo **Pełne, betonowane na gruncie**; wykończenie i balustrada jak dla schodów.

![Pochylnia zawracająca w konfiguratorze](shots/ramp-types.webp)

Zaznacz pochylnię, aby zobaczyć jej typ, **Szerokość**, **Zwrot**, **Odsunięcie podstawy** i **Różnica wysokości**, własne **Nachylenie** i **Długość spocznika** (puste: wartości typu), długości jej biegów i sprawdzenia. Rzuty rysują jej biegi i spoczniki, strzałkę w górę i nachylenie w procentach wzdłuż każdego biegu. Pochylnia wznosząca się na kondygnację wyżej żąda od tamtego stropu otworu, tak jak schody.

![Pochylnia zawracająca i pochylnia wejściowa na rzucie, nachylenie wzdłuż każdego biegu](shots/ramps-plan.webp)

![Pochylnie w widoku 3D](shots/ramps-3d.webp)

W IFC pochylnia jest obiektem IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP lub HALF_TURN_RAMP) typu IfcRampType, złożonym z IfcRampFlight, spoczników (IfcSlab LANDING) i IfcRailing.

## Balustrady

**Architektura → Balustrada** (<kbd>RL</kbd>) rysuje samodzielną balustradę wzdłuż krawędzi balkonu, tarasu lub galerii: wskaż jej punkty, Enter kończy. Przyjmuje wzory balustrad schodowych (słupki i pochwyt, tralki, szkło, linki lub pełny panel), a w swoich właściwościach wysokość (domyślnie 1 m), odsunięcie podstawy, profil i wymiary pochwytu oraz rozstaw słupków; jej punkty przeciąga się za uchwyty (Ctrl/⌘-kliknięcie w środek krawędzi dodaje nowy). W IFC jest obiektem IfcRailing (GUARDRAIL).
