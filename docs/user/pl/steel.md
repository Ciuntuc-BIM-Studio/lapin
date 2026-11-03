---
title: Stal
order: 56
summary: Pręty stalowe z konfigurowalnymi końcami — przedłużone lub cofnięte, cięte płaszczyzną, na ukos, dopasowane do innego pręta, podcięte — tworzone także z narysowanego obrysu; profile zamknięte i gięte na zimno.
---

## Karta Stal

![Karta Stal](shots/ribbon-steel.webp)

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

## IFC i ilości

Pręt eksportuje się jako **IfcMember** ze swoim **IfcMemberType**. Jego bryła jest zapisana tak, jak stal zapisują inne programy (Tekla, Advance Steel, Revit): przekrój **wyciągnięty** (IfcExtrudedAreaSolid), **przycięty** płaszczyznami końców (IfcBooleanClippingResult z IfcHalfSpaceSolid), a każde podcięcie jako **IfcOpeningElement**, który go wycina. Stalowa belka lub słup z końcami zapisuje się tak samo. Ilości każdego pręta podają jego długość między cięciami, pole przekroju, objętość i ciężar netto (7850 kg/m³).
