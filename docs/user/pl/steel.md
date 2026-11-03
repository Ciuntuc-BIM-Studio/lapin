---
title: Stal
order: 56
summary: Pręty stalowe z konfigurowalnymi końcami — przedłużone lub cofnięte, cięte płaszczyzną, na ukos, dopasowane do innego pręta, podcięte — tworzone także z narysowanego obrysu; profile zamknięte i gięte na zimno; hale ramowe z wutami.
---

## Karta Stal

![Karta Stal](shots/ribbon-steel.webp)

- **Hala ramowa…**: cała hala stalowa z kilku liczb, w konfiguratorze hali (poniżej).
- **Pręt** (<kbd>SM</kbd>): pręt stalowy między dwoma punktami, na górnym poziomie kondygnacji, górą przekroju na osi, jak belka. Jego typ wybiera się we Właściwościach przed pierwszym kliknięciem albo na karcie **Katalog**, gdzie każdy typ stalowy jest narysowany swoim przekrojem.
- **Zamień na pręt**: zaznaczone narysowane obrysy stają się prętami (poniżej).
- **Na ukos** i **Dopasuj**: dwa pręty połączone końcami (poniżej).
- **Typy stalowe**: typy prętów projektu i nowe.

Pręt to IfcMember: jego oś biegnie gdziekolwiek w przestrzeni (stężenie, krokiew, słupek), przekrój pochodzi z typu, można go obrócić wokół osi (**Obrót**), a oś może przechodzić przez środek, górę lub dół przekroju (**Punkt wstawienia**). Belki i słupy konstrukcji z profilu stalowego otrzymują takie same końce.

## Końce

Zaznacz pręt albo prostą belkę lub słup z profilu stalowego: sekcja **Stal** we Właściwościach pokazuje oba końce, **Początek** i **Koniec**. Każdy koniec to zestaw reguł; bryła za nimi podąża.

![Końce pręta we Właściwościach](shots/steel-ends.webp)

- **Przedłużenie**: jak daleko koniec wychodzi poza swój punkt; ujemne — koniec jest cofnięty (luz montażowy).
- **Cięcie**: **Prostopadle do osi**; **Według płaszczyzny**, obrócone od cięcia prostego **W rzucie** (wokół pionu przekroju) i **W elewacji** (wokół jego boku), w stopniach; **Na ukos** z innym prętem, na płaszczyźnie dzielącej na pół narożnik między nimi; **Dopasowany do pręta**, na ścianie drugiego pręta, do której dochodzi (półka słupa albo bok jego środnika, gdy słup jest obrócony).
- **Podcięcie**: półki podcięte u **Góra** i/lub **Dół**, na **Długość podcięcia** od końca i **Głębokość podcięcia** od krawędzi przekroju, jak w belce dochodzącej do podciągu.

Połączenie na ukos i dopasowanie wskazują drugi pręt: przesuń go, a koniec podąża za nim; usuń go, a koniec znów jest prosty i to mówi. **Na ukos** na karcie łączy na ukos dwa zaznaczone pręty w narożniku, gdzie się spotykają; **Dopasuj** dopasowuje pierwszy zaznaczony do drugiego.

## Ze szkicu do pręta

Narysuj obrys pręta jako zamkniętą polilinię lub prostokąt (**Rysunek**), a potem **Stal → Zamień na pręt** z jednym lub kilkoma zaznaczonymi obrysami. W jednym kroku każdy obrys jest odczytany i zastąpiony prętem:

![Dwa obrysy naszkicowane na rzucie](shots/steel-sketch.webp)

- jego **dwie długie równoległe krawędzie** (z dokładnością do pół stopnia) dają oś, ich linię środkową od końca do końca, oraz **szerokość** między nimi;
- **krawędź w poprzek każdego końca** daje cięcie tego końca: proste albo według płaszczyzny o nachyleniu krawędzi.

Na **rzucie** obrys to pręt widziany z góry: jego szerokość to szerokość półek, a profil o tej szerokości jest szukany wśród typów prętów projektu i w katalogu (w przeciwnym razie typ wybrany dla Pręta); pręt leży na górnym poziomie kondygnacji. Na **przekroju** lub **elewacji** obrys to pręt widziany z boku: jego szerokość to wysokość przekroju, ukośny koniec to cięcie w elewacji, a prostokątny **uskok** na końcu to **podcięcie** u góry lub u dołu; pręt leży tam, gdzie go narysowano. Uskok narysowany na rzucie jest odrzucany (podcięcie widać z boku). Obrys, który nie jest pasem (trójkąt, L, krawędzie nierównoległe), zostaje bez zmian, a Lapin to mówi.

## Typy stalowe

**Stal → Typy stalowe** pokazuje typy prętów projektu z ich ciężarem na metr i tworzy nowe: profil z katalogu (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** oraz ogólne gięte na zimno **C**, **Z** i **Σ**) albo przekrój zamknięty lub gięty na zimno **o własnych wymiarach** (wysokość, szerokość, grubość, odgięcie). Nowy typ może być typem pręta albo **typem belki** lub **typem słupa** konstrukcji, aby narzędzia Belka i Słup też wstawiały stal. Kliknięcie typu na liście czyni go tym, który wstawia Pręt.

![Okno Typy stalowe](shots/steel-types.webp)

Profile zamknięte są puste w środku, w widoku 3D, w przekrojach i w ilościach. Profile gięte na zimno to ogólne kształty z wymiarami, o ostrych zagięciach, bez katalogu producenta.

![Pręty w 3D](shots/steel-3d.webp)

## Hale ramowe

**Stal → Hala ramowa…** otwiera konfigurator hali. Jak konfigurator dachu ma style po lewej, pola hali w kartach pośrodku, a po prawej halę taką, jaką zbudowałby ją Lapin: typową ramę widzianą wzdłuż hali, z jej wutami, i podsumowanie tego, ile waży.

![Konfigurator hali](shots/steel-hall.webp)

- **Style**: typowe hale z katalogu (**Hala 20 m**, **Hala 30 m**, jednospadowa **Wiata 10 m**) i własne style projektu. Wybrany styl wypełnia wszystkie pola; każde można potem zmienić. **Zapisz jako styl…** zachowuje halę tak skonfigurowaną jako styl projektu; **Aktualizuj styl** zapisuje ją ponownie w jej stylu, a wszystkie hale tego stylu za nim podążają.
- **Rama**: **Rozpiętość** (od osi słupa do osi słupa), **Przęsła** (równe jako `5 × 6` lub każde osobno: `6, 6, 7.5`), **Wysokość okapu** (góra rygli nad zewnętrznym licem słupa), **Dach** (dwuspadowy lub jednospadowy) i jego **Nachylenie**, profile **Słupów** i **Rygli**.
- **Wuty**: **Wuta przy okapie** i **Wuta w kalenicy**, każda ze swoją **Długością** wzdłuż rygla od lica słupa (lub od kalenicy) i **Wysokością** pod ryglem w tym miejscu, oraz tym, jak jest **Wykonana**: **wycięta z profilu rygla** (dwuteownik przecięty ukośnie wzdłuż środnika, jedna połowa przyspawana pod ryglem: środnik i pas samego rygla) lub **spawana z blach** (**Blacha środnika** i **Blacha pasa** o własnej grubości oraz szerokość pasa).

![Wuty spawane z blach](shots/steel-hall-haunches.webp)

- **Szczyty**: **słupy szczytowe pod prostym ryglem** (słupy narożne i słupy w wybranym **Rozstawie**, z mocną osią w poprzek ściany szczytowej, pod lżejszym ryglem sięgającym zewnętrznego lica ścian, słupy dopasowane do jego spodu) albo **zwykła rama** na każdym końcu.

![Szczyt ze słupami](shots/steel-hall-gable.webp)

- **Stal drugorzędna**: płatwie, rygle ścienne, stężenia, belka okapowa i zastrzały (niżej).
- **Umiejscowienie**: gdzie krzyżują się oś A i oś 1 oraz kierunek rozpiętości w rzucie (**Obrót**); podgląd pokazuje siatkę osi hali z góry.

**Utwórz halę** tworzy ją w jednym kroku, który jedno cofnięcie odwraca: jej własną **siatkę osi** (osie 1, 2… przy ramach, A i B przy słupach), a przy każdej ramie dwa **słupy** (słupy konstrukcji z profilu stalowego, środnikiem w płaszczyźnie ramy, z głowicą ciętą według spadku dachu) i dwa **rygle** połączone na ukos w kalenicy (przy dachu jednospadowym jeden, od niskiego słupa do wysokiego): pręty stalowe dopasowane do lic słupów, z ich wutami. Hala stoi na bieżącej kondygnacji; okap i kalenica to wysokości jej ram, a nie nowe kondygnacje. Podsumowanie podaje ramy, wysokości i stal według rodzaju: słupy, rygle, wuty, słupy szczytowe, razem i na m² posadzki.

![Hala w 3D](shots/steel-hall-3d.webp)

Hala pozostaje edytowalna. Zaznacz jeden z jej prętów: jego sekcja **Stal** pokazuje jego halę i **Edytuj halę…**, które otwiera na niej konfigurator; **Zastosuj** buduje halę na nowo na tych samych prętach, które zachowują swoją tożsamość (niepotrzebny już pręt znika, nowy jest dodawany). Są tam też wuty rygla, każda zmieniana osobno (rodzaj, długość, wysokość, blachy): tak ustawiona wuta pozostaje bez zmian, gdy hala jest budowana na nowo; **×** ją usuwa.

![Wuty rygla we Właściwościach](shots/steel-hall-props.webp)

## Połączenia

Ramy hali przychodzą ze swoimi **połączeniami** (Steel → Portal frame hall…, karta **Węzły**): **stopa słupa** pod każdym słupem i słupem ściany szczytowej, **węzeł okapowy** tam, gdzie rygiel dochodzi do słupa, **węzeł kalenicowy** tam, gdzie spotykają się dwa rygle, i blacha węzłowa pod ryglem nad każdym słupem szczytowym. Każde jest osobnym elementem, który wie, jakie pręty łączy; jego blachy, śruby i kotwy powstają z jego parametrów i z prętów takich, jakie są, więc idą za nimi, gdy hala się zmienia.

![Karta Węzły](shots/steel-nodes-hall.webp)

- **Połączenia**: włączone lub wyłączone dla całej hali.
- **Podstawy słupów**: **przegubowe** (blacha podstawy, dwie kotwy po obu stronach środnika lub cztery między półkami) lub **utwierdzone** (dłuższa, grubsza blacha, od czterech do ośmiu kotew poza półkami, żebra w płaszczyźnie środnika). **Kotwy** są **proste**, **z hakiem** lub **z płytką kotwiącą** u dołu; **Liczbę kotew** i **Długość kotew** (poniżej wierzchu betonu) dobiera profil, gdy pozostają puste. Gdy pod słupem stoi stopa fundamentowa z konstrukcji, kotwy wchodzą w nią (i kończą się 50 mm nad jej spodem); inaczej sięgają swojej długości pod podlewką.
- **Węzeł okapowy**: wysunięta blacha czołowa na ryglu przykręcona do półki słupa: jej **Grubość blachy**, **Średnica śrub** i **Klasa śrub**, jak daleko wystaje **Nad ryglem** (z rzędem śrub tam) oraz **Żebra słupa** na wysokości górnej półki rygla i półki wuty.
- **Węzeł kalenicowy**: blachy czołowe dwóch rygli skręcone ze sobą na cięciu ukośnym, z ich wutami.

Co zostaje puste, dobierają profile: śruby od M16 do M27 według wysokości przykręcanego pręta, blacha tak gruba jak jej śruby, odległości od krawędzi i rozstawy według otworów. Połączenie cofa końce swoich prętów o to, co między nie wkłada (rygiel o blachę czołową, słup o blachę podstawy, słup szczytowy o szczelinę 20 mm), przez reguły końców samych prętów.

![Węzeł okapowy](shots/steel-nodes-eaves.webp)

![Utwierdzona stopa słupa na kotwach z hakiem](shots/steel-nodes-base.webp)

Zaznacz połączenie, aby zobaczyć je we Właściwościach: pręty, które łączy, każdy parametr taki, jaki jest teraz (rozmiar dobrany przez profil jest tak pokazany), i jego części: blachy, śruby i kotwy według rozmiaru i klasy, spoiny według grubości z ich długością, ciężar, stopę, w którą wchodzą kotwy. Parametr zmieniony tam dotyczy tylko tego połączenia, a odbudowana hala go zachowuje. **Edytuj halę…** otwiera jego halę.

![Połączenie we Właściwościach](shots/steel-nodes-props.webp)

Pręty rysowane ręcznie dostają połączenia przez **Steel → Znajdź połączenia**: stopę pod każdym pionowym prętem, węzeł okapowy tam, gdzie pręt jest dopasowany do słupa, połączenie słupa szczytowego tam, gdzie słup jest dopasowany pod prętem, węzeł kalenicowy tam, gdzie dwa pręty są ucięte ukośnie względem siebie, styk tam, gdzie dwa biegną w linii, koniec do końca; dla zaznaczonych prętów albo całego modelu, gdy nic nie jest zaznaczone. **Steel → Części stalowe** tworzy zestawienie blach, śrub, kotew i spoin połączeń. Usunięte połączenie oddaje prętom ich końce; usunięty pręt zabiera swoje połączenia; **Usuń halę** w konfiguratorze usuwa halę z jej prętami, połączeniami i osiami w jednym kroku.

## Stal drugorzędna

Hala ma swoją **stal drugorzędną** (Steel → Portal frame hall…, karta **Stal drugorzędna**), tworzoną razem z nią i od nowa, gdy się zmienia; każdy pręt zachowuje swoją tożsamość:

![Karta Stal drugorzędna](shots/steel-secondary-hall.webp)

- **Stal drugorzędna**: włączona lub nie dla całej hali.
- **Płatwie**: ich **Profil** (gięte na zimno Z, C i Σ, ceownik lub lekki dwuteownik), **Rozstaw płatwi** wzdłuż spadku, jak daleko wychodzą **Poza szczyty** i ich **Długości**: **jedna na przęsło** (od ramy do ramy) lub **ciągła** (cała długość hali). Leżą na górze krokwi, ze środnikiem prostopadłym do dachu, od 150 mm wewnątrz lica słupów do 200 mm od kalenicy, w tylu rzędach, ile wymaga rozstaw.
- **Rygle ścienne**: ich **Profil** i **Rozstaw rygli ściennych**, od **Pierwszy rygiel na** aż pod okap, jeden na przęsło na zewnętrznym licu słupów, środnik poziomo, krawędzie w dół; w szczytach od słupa do słupa (lub między słupami ramy szczytowej), tak wysoko, jak pozwala krokiew nad nimi. **Belka okapowa** zajmuje górę ścian podłużnych (ze swoim **Profilem**), rygle jeden rozstaw niżej.
- **Stężenia**: **Przęsła stężone** (ich numery, od pierwszego), **Profil** prętów (pręty okrągłe Ø16–Ø30, kątowniki lub rury) i gdzie: **W dachu** (krzyż w płaszczyźnie dachu, pod pasem górnym krokwi, w polach mniej więcej długości przęsła) i **W ścianach podłużnych** (krzyż w płaszczyźnie ścian przez osie słupów, od wysokości pierwszego rygla aż pod wutę).
- **Zastrzały**: od co drugiej płatwi i rygla ściennego do pasa dolnego krokwi i pasa wewnętrznego słupa, po obu stronach, poza wutami.

![Stal drugorzędna w 3D](shots/steel-secondary-3d.webp)

Każda płatew i rygiel leży na **łapie** na każdej krokwi, słupie lub słupie szczytowym, który przecina: blacha przyspawana do lica pręta obok środnika płatwi (na zewnątrz środnika C, po stronie Z przeciwnej do jego dolnej półki), skręcona przez środnik, dwie śruby na koniec płatwi (M12 do 250 mm wysokości, M16 powyżej). Słup narożny węższy od słupów dostaje wyższą łapę, aż do rygla. Każdy krzyż leży na **blachach węzłowych** przyspawanych do środnika krokwi lub słupów, po jednej przekątnej z każdej strony; kątownik lub rura jest przykręcona dwiema śrubami, gwintowany koniec pręta okrągłego bierze swoją nakrętkę. Zastrzał jest przykręcony na obu końcach. To połączenia jak te ram: zaznacz jedno, aby zobaczyć jego blachę i śruby we Właściwościach i tam je zmienić. Podsumowanie konfiguratora liczy i waży płatwie, rygle, belkę okapową, stężenia i zastrzały; zestawienie **Steel parts** zawiera ich łapy, blachy węzłowe i śruby.

![Płatew na swojej łapie, zastrzał pod nią](shots/steel-secondary-cleat.webp)

![Stężenie dachu na blachach węzłowych](shots/steel-secondary-bracing.webp)

## Zestawienia i rysunki

**Steel → Steel members** tworzy zestawienie stali projektu: pręty oraz belki i słupy z profilu stalowego, według hal (te spoza hal na końcu), po jednej **Marce** dla prętów jednego rodzaju o tym samym profilu, długości i cięciach końców (C słupy, R krokwie, GP słupy szczytowe, P płatwie, SR rygle ścienne, EB belki okapowe, B stężenia, FB zastrzały, M inne pręty, BM belki), numerowane od najcięższego profilu i najdłuższego. Wiersz podaje **Profil**, **Długość**, **Cięcie na początku** i **Cięcie na końcu** (o ile płaszczyzna końca odchyla się od prostej, oraz podcięcie: kąty listy cięcia), **Liczbę**, ciężar na metr, **Sztuki** i łączny, oraz powierzchnię do **Malowania**; sumę pod każdą halą i ogólną. **Steel parts** podaje blachy, śruby, kotwy i spoiny połączeń, z łapami i blachami węzłowymi.

![Zestawienie prętów stalowych](shots/steel-members.webp)

**Steel → Anchor plan** rysuje stopy słupów hali dla fundamentów: rzut kondygnacji hali w skali 1:50, cięty tuż nad blachami podstaw, każda kotwa w kółku, jej odległość od osi (± gdy kotwy stoją symetrycznie względem niej), każda stopa oznaczona typem (**B1**, **B2**…: ta sama blacha, kotwy, klasa i podlewka), przęsła i rozpiętość siatki, oraz tabela typów obok hali: liczba, rodzaj, blacha, kotwy (liczba, rozmiar, kształt), klasa i podlewka. **Steel → Frame elevation** rysuje ramę (typową lub ramę zaznaczonego pręta) widzianą wzdłuż hali w skali 1:50, z rozpiętością oraz wysokościami okapu i kalenicy. Oba to widoki hali (ponownie zażądane, otwiera się ten sam) i trafiają na arkusze jak każdy inny; nadążają za halą, gdy się zmienia.

![Plan kotew](shots/steel-anchor-plan.webp)

![Widok ramy](shots/steel-frame-view.webp)

## IFC i ilości

Pręt eksportuje się jako **IfcMember** ze swoim **IfcMemberType**. Jego bryła jest zapisana tak, jak stal zapisują inne programy (Tekla, Advance Steel, Revit): przekrój **wyciągnięty** (IfcExtrudedAreaSolid), **przycięty** płaszczyznami końców (IfcBooleanClippingResult z IfcHalfSpaceSolid), a każde podcięcie jako **IfcOpeningElement**, który go wycina. Stalowa belka lub słup z końcami zapisuje się tak samo. Ilości każdego pręta podają jego długość między cięciami, pole przekroju, objętość i ciężar netto (7850 kg/m³).

Hala jest eksportowana jako **IfcBuildingSystem** (LOADBEARING), do którego przypisane są jej pręty, z jej danymi w zestawie właściwości `Lapin_SteelHall` i własną **IfcGrid**. Rygiel z wutami to **IfcElementAssembly** rygla i jego wut: wuta wycięta z profilu to **IfcMember**, wuta spawana z blach to dwie **IfcPlate**, każda wyciągnięta (IfcExtrudedAreaSolid). Wuty nie wchodzą do ilości rygla; podsumowanie konfiguratora je waży. Płatwie są eksportowane jako **IfcMember** PURLIN, stężenia i zastrzały jako BRACE, rygle ścienne i belka okapowa jako USERDEFINED (**Side rail**, **Eaves beam**), wszystkie przypisane do systemu hali wraz z połączeniami.

Połączenie eksportuje się jako **IfcElementAssembly** jego blach (**IfcPlate**), śrub (**IfcMechanicalFastener** BOLT), kotew (ANCHORBOLT) i płytek kotwiących (**IfcDiscreteAccessory** ANCHORPLATE), każde z rozmiarem i klasą w `Lapin_SteelPart`; realizuje ono połączenie prętów, które łączy (**IfcRelConnectsWithRealizingElements**; stopa — między fundamentem a jej słupem). Spoiny pozostają danymi, nie bryłami.
