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
- **Rama**: **Rozpiętość** (od osi słupa do osi słupa), **Przęsła** (równe jako `5 × 6` lub każde osobno: `6, 6, 7.5`), **Wysokość okapu** (góra rygli nad zewnętrznym licem słupa), **Dach** (dwuspadowy lub jednospadowy) i jego **Nachylenie**, profile **Słupów** i **Rygli** oraz **Podstawy słupów** (przegubowe lub utwierdzone, zachowane dla przyszłych węzłów).
- **Wuty**: **Wuta przy okapie** i **Wuta w kalenicy**, każda ze swoją **Długością** wzdłuż rygla od lica słupa (lub od kalenicy) i **Wysokością** pod ryglem w tym miejscu, oraz tym, jak jest **Wykonana**: **wycięta z profilu rygla** (dwuteownik przecięty ukośnie wzdłuż środnika, jedna połowa przyspawana pod ryglem: środnik i pas samego rygla) lub **spawana z blach** (**Blacha środnika** i **Blacha pasa** o własnej grubości oraz szerokość pasa).

![Wuty spawane z blach](shots/steel-hall-haunches.webp)

- **Szczyty**: **słupy szczytowe pod prostym ryglem** (słupy narożne i słupy w wybranym **Rozstawie**, z mocną osią w poprzek ściany szczytowej, pod lżejszym ryglem sięgającym zewnętrznego lica ścian, słupy dopasowane do jego spodu) albo **zwykła rama** na każdym końcu.

![Szczyt ze słupami](shots/steel-hall-gable.webp)

- **Stal drugorzędna**: **Rozstaw płatwi**, **Rozstaw rygli ściennych**, **Przęsła stężone** (ich numery, od pierwszego) i **Belka okapowa**. Są zachowane z halą dla przyszłych płatwi, rygli i stężeń; jeszcze nie są budowane.
- **Umiejscowienie**: gdzie krzyżują się oś A i oś 1 oraz kierunek rozpiętości w rzucie (**Obrót**); podgląd pokazuje siatkę osi hali z góry.

**Utwórz halę** tworzy ją w jednym kroku, który jedno cofnięcie odwraca: jej własną **siatkę osi** (osie 1, 2… przy ramach, A i B przy słupach), a przy każdej ramie dwa **słupy** (słupy konstrukcji z profilu stalowego, środnikiem w płaszczyźnie ramy, z głowicą ciętą według spadku dachu) i dwa **rygle** połączone na ukos w kalenicy (przy dachu jednospadowym jeden, od niskiego słupa do wysokiego): pręty stalowe dopasowane do lic słupów, z ich wutami. Hala stoi na bieżącej kondygnacji; okap i kalenica to wysokości jej ram, a nie nowe kondygnacje. Podsumowanie podaje ramy, wysokości i stal według rodzaju: słupy, rygle, wuty, słupy szczytowe, razem i na m² posadzki.

![Hala w 3D](shots/steel-hall-3d.webp)

Hala pozostaje edytowalna. Zaznacz jeden z jej prętów: jego sekcja **Stal** pokazuje jego halę i **Edytuj halę…**, które otwiera na niej konfigurator; **Zastosuj** buduje halę na nowo na tych samych prętach, które zachowują swoją tożsamość (niepotrzebny już pręt znika, nowy jest dodawany). Są tam też wuty rygla, każda zmieniana osobno (rodzaj, długość, wysokość, blachy): tak ustawiona wuta pozostaje bez zmian, gdy hala jest budowana na nowo; **×** ją usuwa.

![Wuty rygla we Właściwościach](shots/steel-hall-props.webp)

## IFC i ilości

Pręt eksportuje się jako **IfcMember** ze swoim **IfcMemberType**. Jego bryła jest zapisana tak, jak stal zapisują inne programy (Tekla, Advance Steel, Revit): przekrój **wyciągnięty** (IfcExtrudedAreaSolid), **przycięty** płaszczyznami końców (IfcBooleanClippingResult z IfcHalfSpaceSolid), a każde podcięcie jako **IfcOpeningElement**, który go wycina. Stalowa belka lub słup z końcami zapisuje się tak samo. Ilości każdego pręta podają jego długość między cięciami, pole przekroju, objętość i ciężar netto (7850 kg/m³).

Hala jest eksportowana jako **IfcBuildingSystem** (LOADBEARING), do którego przypisane są jej pręty, z jej danymi w zestawie właściwości `Lapin_SteelHall` i własną **IfcGrid**. Rygiel z wutami to **IfcElementAssembly** rygla i jego wut: wuta wycięta z profilu to **IfcMember**, wuta spawana z blach to dwie **IfcPlate**, każda wyciągnięta (IfcExtrudedAreaSolid). Wuty nie wchodzą do ilości rygla; podsumowanie konfiguratora je waży.
