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

## Przestrzeń pod schodami

Przestrzeń pod biegiem schodów można urządzić: szafa na rzeczy, biuro w szafie, kącik do czytania, wejście z ławką i wieszakami, domek do zabawy dla maluchów albo legowisko dla psa. Zaznacz schody o prostych biegach i kliknij **Przestrzeń pod schodami…** na dole ich właściwości (lub **Architektura → Przestrzeń pod schodami**).

![Konfigurator przestrzeni pod schodami: kącik do czytania pod biegiem](shots/under-stair-dialog.webp)

Po lewej są gotowe style: **Szafa na rzeczy**, **Biuro w szafie**, **Kącik do czytania**, **Wejście i przedsionek**, **Domek do zabawy dla maluchów** i **Buda dla psa**, a nad nimi style projektu. Wybierz jeden i zmień go pośrodku:

- **Położenie.** **Pod biegiem** (przy schodach w kształcie L lub U), **Strona otwarta** (domyślnie z dala od zakrętu), gdzie **Zaczyna się od** i **Kończy się na** wzdłuż biegu (puste: od miejsca, gdzie spód jest 300 mm nad podłogą, do ostatniego podstopnia) i jej **Głębokość** (pusta: tak szeroko jak schody).
- **Front.** **Niska część** zamknięta **Drzwiami szafy** (każde mniej więcej o **Szerokości drzwi**, z **Półkami za drzwiami**), **Zamkniętym panelem** albo zostawiona otwarta; **Regał na książki przy wysokim końcu**.
- **Główny element.** **Biurko z półką** (i lampą), **Ławka z poduszkami** (szuflady z przodu), **Ławka w przedpokoju i wieszaki** (przegródki na buty pod spodem), **Drzwi domku do zabawy** (i **Okno obok drzwi**) albo **Legowisko dla psa** za obramowanym otworem, każdy ze swoimi wymiarami.
- **Materiały.** **Grubość płyty** oraz materiały **Korpusu**, **Frontów** i **Tkaniny**, spośród materiałów projektu.

Po prawej podgląd pokazuje przestrzeń od strony otwartej pod spodem biegu i z góry, jej długość, głębokość i wysokości oraz części. Każda część jest przycinana przez skos; część, która się nie mieści (biurko tam, gdzie schody są za niskie), zostaje pominięta, a podgląd o tym mówi. **Utwórz** lub **Zastosuj** to jeden krok cofania. **Zapisz jako styl…** zachowuje wyposażenie jako styl projektu; **Aktualizuj styl** zapisuje go ponownie, a wszystkie przestrzenie stylu za nim podążają.

![Przestrzeń pod schodami we właściwościach](shots/under-stair-properties.webp)

Przestrzeń jest osobnym elementem, osadzonym w swoich schodach. Gdy schody się zmieniają (szerokość, obrót, stopnie, typ), przestrzeń jest budowana pod nimi na nowo i jest usuwana razem z nimi. Pola położenia są we właściwościach, a **Konfiguruj…** ponownie otwiera konfigurator.

![Sześć stylów pod sześcioma schodami](shots/under-stair-3d.webp)

Rzuty, przekroje, widoki i arkusze rysują ją jak każdy inny element: przeciętą tam, gdzie tnie rzut, widzianą poniżej.

![Kącik do czytania w rzucie, pozostałe obok](shots/under-stair-plan.webp)

![Przekrój wzdłuż biegu przez kącik do czytania](shots/under-stair-section.webp)

**Architektura → Zestawienie przestrzeni pod schodami** podaje części każdej przestrzeni z liczbą sztuk, materiałem i objętością oraz sumy według materiału. W IFC przestrzeń to IfcElementAssembly ("Under-stair space") z IfcFurniture (półki, biurko, ławka, poduszki, legowisko), IfcDoor, IfcPlate, IfcDiscreteAccessory (uchwyty, wieszaki) i IfcLightFixture, połączony ze swoim IfcStair.

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

## Balkony

**Architecture → Balcony** (<kbd>BY</kbd>) wiesza balkon na ścianie: wybierz styl na pasku wiersza poleceń i kliknij poza licem ściany, tam gdzie ma być; staje po tej stronie ściany na poziomie podłogi kondygnacji. **Balkon francuski** (Juliet) kliknięty przy drzwiach lub oknie centruje się na nich, 200 mm szerszy. Gdzie w pobliżu nie ma ściany, balkon staje tam, gdzie klikniesz. **Balcony by outline** rysuje dowolny kształt: kliknij narożniki, pierwsza krawędź przylega do budynku. **Gallery edge** rysuje krawędź antresoli nad przestrzenią o podwójnej wysokości dwoma punktami (pustka po lewej, antresola po prawej).

Wbudowane style obejmują rodzaje, o które się pyta:

- **Juliet balcony**: balustrada z tralek przed drzwiami balkonowymi nad małą kamienną półką na dwóch wspornikach.
- **Cantilevered slab, glass**: płyta betonowa wysunięta ze stropu przez łącznik termoizolacyjny, wyłożona płytkami ze spadkiem, kapinos pod krawędzią, szkło z trzech stron.
- **Stacked, on posts**: płyta na stalowych słupach z przodu, które stoją na balkonie poniżej (lub na terenie); powtarzana kondygnacja nad kondygnacją.
- **On steel brackets**, **Hung from tie rods** (cięgna od przednich narożników do ściany), **Rounded corners**, **Curved front** z pełną attyką.
- **Loggia**: wcięta w elewację. Wycina swoją wnękę w ścianie (szeroką jak loggia i wysoką jak jej **Recess height**), buduje ściany boczne i płytę sufitu za licem i stawia własną **Back wall** (typu ściany, tej samej wysokości) na głębokości loggii, tam gdzie idą drzwi. Balustrada jest w licu.
- **Corner balcony**: wzdłuż ściany i wokół narożnika budynku, z powrotem wzdłuż ściany bocznej; kliknięty przy końcu ściany, obejmuje go.
- **Mezzanine gallery edge**: płyta antresoli za krawędzią, czoło nad krawędzią i balustrada z prętów.

![Balkony na piętrze: francuski przy drzwiach, na słupach, narożny, loggia, łukowy, zaokrąglony i podwieszony; balkon kondygnacji powyżej linią przerywaną](shots/balconies-plan.webp)

Rzut rysuje płytę, wykończenie i balustradę balkonu tak, jak je tnie lub widzi, a balkony kondygnacji powyżej linią przerywaną. Przekroje i elewacje rysują płytę, balustradę, słupy i wsporniki.

### Konfigurator

**Architecture → Balcony configurator** (lub zębatka obok **Style** we właściwościach balkonu) działa jak konfigurator dachu: style po lewej (najpierw projektu, potem wbudowane), pola balkonu na kartach po prawej, nad jego rzutem lub przekrojem przez środek, tak jak zbudowałby je szkic:

- **Shape**: **Kind**, **Outline** (prostokąt, zaokrąglone narożniki z **Corner radius**, łukowy front z **Front bulge** lub narysowany wielokąt), **Width** i **Depth**, powrót balkonu narożnego, płyta i **Fascia height** antresoli.
- **Slab and finish**: **Slab thickness** i materiał, **Edge** (prosta, z **drip** — kapinosem —, z **nosing** wykończenia lub **tapered** do **Edge thickness**), grubość i materiał wykończenia oraz jego **Fall (%)** ku przodowi, podsufitka i **Thermal break** (80 mm między płytą a stropem wewnątrz; mówią o tym zestawienie i IFC).
- **Railing**: wzory balustrad (słupki i pochwyt, tralki, szkło, linki, pełny panel) lub **parapet** o własnej grubości i materiale, albo brak; wysokość, tylko z przodu lub także po bokach, odsunięcie od krawędzi, pochwyt, słupki i kolor.
- **Supports**: **Carried by** nic (wspornik), **posts** (okrągłe lub kwadratowe, rozmiar i rozstaw), **brackets** (głębokość, wysokość i rozstaw) lub **hangers** (wysokość zamocowania cięgien nad płytą) oraz ich materiał.
- **Loggia**: wysokość wnęki, grubość ścian bocznych, płyta poza lico, płyta sufitu i ściana tylna.

![Konfigurator balkonu: style, kształt i rzut](shots/balcony-configurator.webp)

![Jego podparcia nad przekrojem przez balkon](shots/balcony-configurator-section.webp)

**Apply** zmienia balkon w jednym kroku. **Save as style…** zachowuje go jako styl projektu (typ balkonu), oferowany na pasku narzędzia i we właściwościach; **Update style** zapisuje go ponownie w jego stylu i każdy balkon stylu go przejmuje; obok nazwy stylu zmienia się jego nazwę lub go usuwa (jego balkony zostają, jakie są). Styl jest typem: jego pozycje kosztorysowe (Cost → Map articles) mierzą jego balkony.

![Właściwości balkonu: jego styl i pola jego rodzaju](shots/balcony-properties.webp)

We właściwościach balkon pokazuje tylko pola, których używa jego rodzaj. Jego uchwyty przesuwają go wzdłuż ściany, środek frontu ustawia głębokość (w loggii — wnęki), środki boków szerokość; narysowany przeciąga się za narożniki. Wnęka i ściana tylna loggii podążają za nią i znikają razem z nią.

**Stack on storeys above** (w konfiguratorze, we właściwościach lub **Architecture → Stack on storeys above**) kopiuje zaznaczone balkony na każdą kondygnację nad ich własną w jednym kroku, przez Copy to storeys: kopia wisi na ścianie swojej kondygnacji stojącej tam, gdzie jej ściana, powielona loggia przecina ścianę tej kondygnacji, słupy stoją na balkonie poniżej.

![Elewacja południowa w 3D: balkon francuski, powielone balkony na słupach, narożny i podwieszony na cięgnach](shots/balconies-3d.webp)

![Od północy: loggia wcięta w ścianę, łukowy balkon z attyką i zaokrąglony](shots/balconies-3d-north.webp)

**Architecture → Balcony schedule** wymienia każdy balkon z kondygnacją, stylem, rodzajem, szerokością, głębokością, powierzchnią, długością i wzorem balustrady, podparciem i łącznikiem termoizolacyjnym, z sumami. W IFC balkon to IfcElementAssembly (USERDEFINED, „Balcony”) typu IfcElementAssemblyType, złożony z części: płyta to IfcSlab (FLOOR), wykończenie i podsufitka IfcCovering (FLOORING, CEILING), balustrada IfcRailing (GUARDRAIL) lub attyka IfcWall (PARAPET), słupy IfcColumn, wsporniki IfcDiscreteAccessory (BRACKET), cięgna IfcMember, łącznik termoizolacyjny IfcBuildingElementPart; wnęka loggii to IfcOpeningElement w jej ścianie. Lapin wczytuje swoje balkony z powrotem jako balkony.
