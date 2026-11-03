---
title: Import i eksport
order: 110
summary: Wymieniaj model jako IFC, wczytuj rysunki DXF, łącz pliki i eksportuj modele 3D do renderingu i AR.
---

Wszystko, co wchodzi lub wychodzi, znajduje się w menu **Plik**.

![Menu Plik](shots/file-menu.webp)

## IFC i Fragments

**Eksportuj IFC lub Fragments…** zapisuje model jako IFC. Każda ściana, strop, drzwi i dach to prawdziwy element IFC z warstwami, typem i właściwościami, więc plik otwiera się jako model w innych narzędziach BIM. Drzwi i okna mówią, jak się otwierają: stronność każdego skrzydła, kierunek otwierania i panele, tak jak definiuje je IFC, więc inne programy rysują te same otwarcia.

Eksport pyta o schemat (**IFC4**, czytany wszędzie, lub **IFC4X3**), szczegółowość (części jako osobne elementy lub jedna bryła na element), fazę i sposób zapisu brył. **Geometria** to **fasetowy B-rep** (dokładny, najczęściej oczekiwany przez inne programy) lub **zbiory powierzchni**: te same powierzchnie w znacznie mniejszym pliku. **Uprość** zapisuje mniej trójkątów tam, gdzie bryła jest gęstą siatką, jak kanały i rury z pliku innego programu, nigdy dalej od niej niż wpisana **tolerancja** (na początek 2 mm). Bryły, które Lapin buduje sam (ściany, stropy, drzwi), zachowują dokładne powierzchnie, a upraszczanie zmienia tylko kształt bryły, nigdy jej właściwości.

![Eksport IFC](shots/export-ifc.webp)

**Importuj IFC…** wczytuje IFC z innego programu. Zanim cokolwiek zostanie wprowadzone, Lapin czyta plik i pokazuje, co zawiera: elementy według branż (architektura, konstrukcja, instalacje, meble i wyposażenie, teren), każdą branżę według rodzajów z ich liczbą oraz kondygnacje z liczbą elementów na każdej. Odznacz to, czego nie potrzebujesz, na przykład instalacje i meble, gdy pracujesz nad konstrukcją, albo zostaw tylko kondygnacje, nad którymi pracujesz. **Jak** wprowadza elementy rozpoznane jako elementy Lapina, wszystkie jako odniesienia lub jako połączone. Podczas czytania i importu pliku Lapin pokazuje, co robi i jak daleko zaszedł; duży plik wymaga chwili.

![Import IFC](shots/import-ifc.webp)

Niektóre pliki oprócz modelu zawierają 2D: symbole na rzucie, obrysy, kreskowania i teksty narysowane dla elementu albo elementy narysowane tylko w 2D (linie, teksty, symbole bez bryły). Gdy plik je ma, okno dialogowe liczy je w sekcji **Narysowane w 2D**. Odznacz **własne 2D elementów**, a rzut narysuje te elementy według ich przekroju, tak jak elementy Lapina; odznacz **elementy narysowane tylko w 2D**, a zostaną pominięte. Oba są wprowadzane, chyba że je odznaczysz.

![Narysowane w 2D](shots/import-ifc-2d.webp)

**Czyszczenie i optymalizacja** zawiera reguły importu, takie same dla każdego pliku, zapamiętywane jako twoje na następny import. **Zachowaj powtarzaną bryłę raz** (włączone, dopóki tego nie odznaczysz) zachowuje raz bryłę, którą plik powtarza pod innymi nazwami (śrubę, kołnierz, wspornik), a jego elementy ją współdzielą, bez żadnej straty. Pozostałe, gdy są zaznaczone, pomijają to, co przyniósł ze sobą inny program: **płaskie bryły** (teksty, linie i kreskowania zamienione na trójkąty), **elementy dalsze od reszty** niż wpisana odległość (teksty rysunku, tabliczka rysunkowa), **duplikaty** w tym samym miejscu oraz **porty i otwory zapisane jako obiekty**. **Uprość siatki** zapisuje mniej trójkątów w granicach wpisanej tolerancji; **czytaj liczby zapisane jako tekst** zamienia `DoubleLength:0,01` na 0.01 m, a `1.15395 kg` na masę (sama liczba całkowita, kod, pozostaje tekstem). W sekcji **Co przenieść** pole **Zestawy właściwości** wymienia zestawy z pliku wraz z liczbą elementów, które je mają; **techniczne** (transformacja Navisworksa, wewnętrzne identyfikatory, statystyki geometrii) są pomijane, chyba że je zaznaczysz. Komunikat importu mówi, co zrobiła każda reguła.

![Porządkowanie i optymalizacja](shots/import-ifc-rules.webp)

To, co umieszcza plik, wchodzi jako jedna grupa nazwana od niego, którą możesz otworzyć i uporządkować, albo luzem, gdy odznaczysz **Jako jedna grupa** (zobacz [Grupy i porządkowanie](groups.md)).

**Wszystko edytowalne**, jedna z opcji pola **Jak**, idzie dalej niż rozpoznawanie: to, co pozostaje odniesieniem, staje się bryłą Studio z własnych trójkątów, tym samym elementem z klasą, właściwościami, kondygnacją i grupą, który od razu przyjmuje wypychanie/wciąganie, zaokrąglenia i operacje logiczne (zobacz [Studio](studio.md)). Bryła z więcej niż 5000 trójkątów, jak gęsto zasiatkowana pompa lub kształtka, pozostaje odniesieniem, a komunikat importu mówi, ile ich jest i dlaczego. **Importuj Fragments…** ma to samo pole **Jak**: plik Fragments zawiera tylko trójkąty, a jednak jego ściany (z drzwiami i oknami), stropy i słupy są z nich rozpoznawane jako elementy Lapina, reszta zostaje odniesieniami lub, z opcją **Wszystko edytowalne**, staje się bryłami Studio. Elementy pliku IFC, których rozpoznawanie nie mogło odczytać z pliku (ściana z otworami wyciętymi w bryle), są również próbowane na podstawie trójkątów. **Importuj siatkę** pyta o **Jako: edytowalne**. Wcześniej wprowadzone odniesienia konwertuje się poleceniem Modyfikuj → **Konwertuj na natywne** (NV): rozpoznane tam, gdzie się da, reszta zamieniona na bryły Studio, a w przypadku zaznaczonej grupy jej członkowie. Wcześniej wprowadzone odniesienie staje się edytowalne pojedynczo poleceniem Studio → **Utwórz bryłę Studio**, które zachowuje je w jego grupie i na jego kondygnacji.

Lapin rozpoznaje ściany, stropy, słupy, belki, fundamenty, drzwi i okna i zamienia je na elementy Lapina, które możesz edytować. To, czego nie umie rozpoznać, wchodzi jako odniesienie, narysowane, ale nieedytowalne. Drzwi i okna wchodzą otwierane tak, jak podaje plik: ich stronność, strona, na którą się otwierają, a w przypadku okien sposób otwierania każdego panelu.

Wykończenia, które inny program wyeksportował jako osobne ściany (tynk 2,5 cm na elewacji, tynk wewnętrzny), stają się warstwą ściany, którą pokrywają, gdy przylegają do jej lica, są tak wysokie jak ona i biegną wzdłuż niej: ściana dostaje typ z tą warstwą (*Mur 350 + Tynk wapienny*), jej własne warstwy zostają na miejscu, a komunikat importu mówi, ile ich było. Wykończenie odsunięte od ściany lub pokrywające tylko jej część pozostaje cienką ścianą. Słup, który plik podaje jako bryłę ściętą u góry przez spadek (pod pochyłą belką lub dachem), wchodzi jako słup o swoim przekroju, aż do najwyższego punktu.

**Mapuj typy importowane** (Zarządzaj) wymienia typy utworzone przez import (typy ścian, stropów, drzwi, okien, słupów, belek i fundamentów), każdy z plikiem i liczbą elementów. Wybierz w polu **Staje się** jeden z typów projektu tego samego rodzaju i naciśnij **Mapuj**: jego elementy przyjmują typ projektu, ściana zachowuje rdzeń tam, gdzie był, a zaimportowany typ znika. Gdy zaznaczysz **Zapamiętaj**, późniejszy import typu tego rodzaju i o tej nazwie od razu przyjmuje typ projektu, a jego komunikat to mówi. Zapamiętane wybory są wymienione w oknie dialogowym, każdy z przyciskiem **Zapomnij**.

![Mapuj zaimportowane typy](shots/map-types.webp)

Przestrzenie z pliku (z Revita, ArchiCAD-a i innych) wchodzą jako pomieszczenia Lapina, każde na swoim poziomie, z obrysem, numerem, nazwą, wysokością, funkcją, działem i wykończeniem podłogi: zobacz [Pomieszczenia](rooms.md#rooms-from-an-ifc-file). Wszystko, co zaimportowany element miał w pliku (zestawy właściwości i ilości), jest zachowane i pokazane w sekcji **Właściwości IFC** w jego właściwościach. Odniesienie opuszcza **Eksportuj IFC…** takie, jakie weszło: z tym samym GlobalId, klasą i atrybutami (opis, znacznik, typ predefiniowany), zestawami właściwości i ilościami z każdą wartością zapisaną tak, jak miał ją plik, oraz typem, materiałem, portami, połączeniami i systemem, każdy z własnym GlobalId. Wprowadź z pliku tylko instalacje i wyeksportuj je, a nowy plik przeniesie je w całości.

**Importuj Fragments…** otwiera ten sam panel dla pliku That Open `.frag`: jego elementy według branż i rodzajów, jego kondygnacje oraz **Jako jedna grupa**. Plik wyeksportowany z tego samego projektu nie wnosi niczego nowego, gdy zaimportujesz go z powrotem: jego elementy już tu są.

![Import Fragments](shots/import-frag.webp)

**Importuj Fragments…** wprowadza plik `.frag`: każdy obiekt z geometrią staje się odniesieniem rysowanym z jego bryły, w klasie podanej przez plik, na kondygnacji, na której umieszcza go plik (dopasowanej po nazwie do twoich, utworzonej, gdy jej brak), z atrybutami pliku w sekcji **Właściwości IFC**. Obiekty o tej samej bryle (ten sam kształt, umieszczony w wielu miejscach) ją współdzielą. Ponowny import tego samego pliku aktualizuje to, co wprowadził wcześniej. To, co zostaje rozpoznane z jego trójkątów, zależy od pola **Jak**, jak powyżej.

**Eksportuj IFC lub Fragments…** zapisuje model jako IFC lub jako Fragments, wybierany tak jak przy imporcie: elementy według branż i rodzajów, kondygnacje, faza i tylko to, co pokazuje widok 3D. IFC wychodzi w IFC4 lub IFC4X3, z częściami elementu warstwowego pod nim lub z jedną bryłą na każdy; Fragments od punktu bazowego projektu, początku wewnętrznego lub punktu pomiarowego.

![Eksportuj IFC lub Fragments](shots/export-exchange.webp)

## DXF i DWG

**Importuj DXF…** wprowadza rysunek CAD do widoku na pierwszym planie, z warstwami, kreskowaniami, tekstami i wymiarami. Wchodzi jako rysunek, do którego możesz się przyciągać i który możesz edytować. Aby użyć pliku DWG, najpierw zapisz go jako DXF w swoim programie CAD (AutoCAD, BricsCAD lub DraftSight: **Zapisz jako → DXF**). Import PDF nie jest jeszcze dostępny. Na rzucie opcja **Umieszczony: według współrzędnych mapy** kładzie rysunek w rzeczywistych współrzędnych (plan katastralny w Stereo 70) tam, gdzie umieszcza go mapa (zobacz [Teren](site.md)).

## Łącza

**Połącz IFC…** utrzymuje inny plik jako połączony zamiast go importować. Jest tylko do odczytu i jest ponownie wczytywany, gdy się zmieni. Działa to także dla DXF. **Zarządzaj → Łącza** pokazuje, ponownie wczytuje, konwertuje lub usuwa łącza.

## Modele 3D

**Eksportuj 3D (glTF, USD, DAE)…** zapisuje model jako powierzchnie do renderingu, wizualizacji i rzeczywistości rozszerzonej (AR):

| Format | Do czego |
|---|---|
| GLB, glTF | Blender, three.js, przeglądarki internetowe. Obrazy twoich materiałów są dołączone. |
| USDZ | AR Quick Look Apple, Reality Composer, Omniverse. Obrazy dołączone. |
| USDA | OpenUSD jako tekst, jeden plik. |
| DAE | COLLADA, dla SketchUpa i starszych narzędzi. |

**Importuj siatkę (OBJ, STL, glTF)…** wprowadza siatkę z innego programu 3D (OBJ, STL, glTF lub GLB) jako odniesienia na aktywnym poziomie. glTF podaje swoją jednostkę i kierunek do góry; dla OBJ i STL Lapin o to pyta. Polecenie Studio **Utwórz bryłę Studio** czyni odniesienie edytowalnym (zobacz [Studio](studio.md)).

![Eksport 3D](shots/export-3d.webp)

Wybierasz, co eksportować, jaką fazę i początek układu. **Co** to **wszystko** albo model **tak, jak pokazuje go widok**: widok 3D na pierwszym planie, bez ukrytych elementów i kategorii oraz kondygnacji, których nie pokazuje, z warstwami, które pokazuje (bez wykończeń lub tylko rdzenie). Gdy ten widok jest przecięty (cięcie nad pokazanymi kondygnacjami, przekrój pokazany w 3D, przekrój prostopadłościenny), **Cięcie** przecina tam także eksport i zamyka każde cięcie w jego materiałach, więc plik wygląda jak ekran. Widok 3D, który coś pomija, jest eksportowany tak, jak to pokazuje, chyba że wybierzesz inaczej. Każdy element zachowuje swoją klasę IFC, typ i GlobalId. Aby wymienić sam model, a nie jego powierzchnie, użyj **Eksportuj IFC**.
