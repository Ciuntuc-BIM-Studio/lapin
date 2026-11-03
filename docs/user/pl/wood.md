---
title: Drewno
order: 54
summary: Ściany szkieletowe z konfiguratora: słupki w module, podwaliny i oczepy, rygle, a każde drzwi i okno obramowane według swoich reguł.
---

## Ściany szkieletowe

Typ ściany można zbudować jako szkielet drewniany: **Drewno → Szkielet drewniany** otwiera konfigurator, z typami ścian projektu po lewej. Zaznacz **Szkielet drewniany**, a rdzeń typu zostanie zbudowany z elementów zamiast z jednej warstwy: podwaliny i oczepy, słupki w ich module, rzędy rygli, a wokół każdych drzwi i okna słupki główne, słupki podporowe, nadproże i podwalina podokienna. Pozostałe warstwy (okładzina, poszycie, płyty) pozostają bez zmian, a izolacja wypełnia przestrzenie między elementami.

![Konfigurator szkieletu drewnianego](shots/wood-frame.webp)

- **Gotowe ustawienia**: **Skandynawski** (45 × 145 co 600 mm, C24) lub **Rumuński** (50 × 150 co 625 mm, C24, rząd rygli na 1,25 m). Każde pole można potem zmienić.
- **Słupki, rozstaw, klasa, podwaliny i oczepy, słupki skrajne, rzędy rygli**. **Moduł od** ustala, gdzie zaczyna się moduł słupków: od początku każdej ściany lub od początku układu projektu, tak aby ściany jednej elewacji miały wspólny moduł.
- **Nośna**: ścianka działowa nic nie przenosi i nad otworami otrzymuje płaski element zamiast nadproża.

## Drzwi i okna

Drzwi i okna pozostają w gestii architekta: przesuń je lub zmień ich typ, a ich obramowanie podąży za nimi. Szkielet jest mierzony od **otworu w stanie surowym**: drzwi lub okno wraz z luzami montażowymi (po bokach, u góry, pod oknem; drzwi schodzą do podłogi).

Każdy otwór przyjmuje regułę swojego **zakresu szerokości** (tabela w sekcji **Otwory**: domyślnie do 900 mm, do 1800 mm i powyżej): słupki główne i podporowe z każdej strony, nadproże (brak, lite, LVL lub klejone, jego przekrój i liczba warstw) oraz liczbę warstw podwaliny podokiennej. Słupki nigdy nie stoją w otworze surowym; krótkie słupki nad nadprożem i pod podwaliną podokienną zachowują moduł, więc styki płyt nadal wypadają na słupkach. Dwa otwory bliższe sobie niż **najwęższy filar** mają wspólne nadproże i zewnętrzne słupki główne. Otwór zbyt bliski końca ściany jest zgłaszany we właściwościach ściany, nigdy przesuwany.

Zaznacz drzwi lub okno: jego sekcja **Drewno** pokazuje otwór surowy, przyjętą regułę i to, co otrzymuje. Zaznacz **Własna reguła**, aby dać tylko temu otworowi inne słupki główne lub podporowe, inne nadproże lub podwalinę podokienną.

## Narożniki, płyty i elewacja szkieletu

- **Narożniki**: tam, gdzie inna ściana kończy się na końcu tej ściany (narożnik na styk), ściana przechodząca otrzymuje **trzy słupki** (jeden za wewnętrznym licem drugiej ściany), narożnik **kalifornijski** (słupek położony płasko przy licu wewnętrznym, pod okładzinę) lub **tylko słupki skrajne**. Narożnik na ucios zachowuje słupki skrajne obu ścian.
- **Połączenia T**: tam, gdzie ściana kończy się na tej ścianie w jej biegu, ta otrzymuje **po słupku z każdej strony** lub **słupki i drabinkę z przewiązek** między nimi.
- **Płyty poszycia**: zaznacz warstwę układu (OSB, płyta pilśniowa, płyta gipsowo-kartonowa) i podaj wymiar arkusza: warstwa zostaje ułożona z pionowych arkuszy, ze stykami w module słupków, docinanych przy otworach surowych i wzdłuż góry. **Drewno → Zestawienie płyt poszycia** liczy arkusze potrzebne każdej ścianie, ich powierzchnię netto i odpady.
- **Elewacja szkieletu**: w sekcji **Drewno** ściany szkieletowej przycisk tworzy przekrój wzdłuż ściany przez środek jej rdzenia, w skali 1:20, z każdym elementem przeciętym, z łańcuchem wymiarów przez otwory surowe, modułem słupków pod spodem i wymiarem każdego otworu surowego. Umieść go na arkuszu jak każdy widok.

## Panele prefabrykowane

Zaznacz **Panele prefabrykowane**, aby wykonać szkielet w zakładzie: podaj **najdłuższy panel** (tak długi, jak pozwala stół w zakładzie i ciężarówka) oraz **najkrótszy**. Każda ściana jest dzielona na panele w najdłuższym styku mieszczącym się w tej długości, w module słupków, gdzie to możliwe, nigdy przez słupki główne, podporowe, nadproże ani podwalinę podokienną otworu, ani przez wzmocnienie połączenia teowego. Każdy panel jest obramowany osobno, z własnymi słupkami skrajnymi i podwalinami oraz oczepami na stykach. Panel, którego nie da się zmieścić w długości (otwór zbyt szeroki), pozostaje w całości, a ściana to zgłasza.

![Ściana prefabrykowana w konfiguratorze](shots/wood-panels.webp)

Panele są oznaczane nazwą ściany i ich numerem wzdłuż niej (W3-P1, W3-P2…). Sekcja **Drewno** ściany wymienia ich długości; elewacja szkieletu rysuje styki i oznaczenia na ścianie; **Drewno → Panele ścienne** wymienia każdy panel z jego ścianą, długością, wysokością, otworami, elementami, objętością drewna i masą, aby można je było po kolei zamówić, wykonać i podnieść. Płyty na panelu również noszą jego oznaczenie. W IFC każdy panel to IfcElementAssembly (Timber frame panel) zawierający jego elementy i płyty.

## CLT

W tym samym konfiguratorze typ ściany lub stropu można zbudować **z paneli CLT**: podaj warstwy (grubość każdej, 3, 5, 7 lub 9 warstw), klasę, najdłuższy i najwyższy (lub najszerszy) panel, jaki wykonuje prasa, styk między panelami (na styk, na zakładkę, na wpust z obcą wpustką), odległość otworu od krawędzi panelu i luz montażowy. Ściana jest dzielona na panele wzdłuż swojego biegu, ze stykami odsuniętymi od otworów; każdy panel jest własną bryłą rdzenia, z wyciętymi otworami surowymi. Strop jest dzielony na panele w poprzek rozpiętości (w krótszym kierunku albo wzdłuż x lub y). **Drewno → Płyty CLT** wymienia każdy panel z jego oznaczeniem, wymiarami, warstwami, klasą, stykiem, powierzchnią i objętością; w IFC każdy z nich to IfcBuildingElementPart o nazwie CLT panel.

- **Styki**: dwie połówki **zakładki** zachodzą na siebie na długość zakładki; styk **na wpust z obcą wpustką** ma wręg na jednym licu i włożony w niego pasek sklejki, będący osobną częścią. Oba są modelowane w panelach, więc ich objętości i zestawienie odpowiadają temu, co wycina zakład.
- **Narożniki otworów**: frezarka nie wytnie prostego narożnika wewnętrznego. **Promień frezu** zostaje w każdym narożniku; **podcięcie typu dog-bone** wycina w każdym narożniku okrąg, aby prostokątna ościeżnica nadal się zmieściła. Konfigurator rysuje frezy.
- **Elewacja płyt**: sekcja **Drewno** ściany CLT tworzy przekrój wzdłuż niej w skali 1:20, ze stykami i oznaczeniami paneli oraz wymiarami otworów surowych.
- **Dachy CLT**: zaznacz dach spadzisty i zaznacz **Płyty CLT** w jego sekcji **Drewno**: jego połacie zostają ułożone jako płyty CLT zamiast krokwi lub wiązarów (murłaty i pokrycie pozostają), z wyciętymi oknami dachowymi i lukarnami, w pasach o szerokości panelu wzdłuż okapu, łączonych końcami tam, gdzie połać jest dłuższa niż panel.

## Stropy na belkach

Typ stropu można zbudować **na belkach stropowych**: wybierz go w konfiguratorze i wybierz **na belkach**. Podaj przekrój belek, ich rozstaw i klasę, kierunek rozpiętości (krótszy albo wzdłuż x lub y), czy krawędzie zamyka **belka obwodowa**, **rzędy przewiązek**, a wokół każdego otworu w stropie (klatka schodowa, szyb) **belki wymianowe nośne** z każdej strony oraz **warstwy wymianu** na jego końcach. Belki skrócone przez otwór kończą się na wymianie, a dodatkowa belka wchodzi tam, gdzie belki wymianowe pozostawiają przerwę szerszą niż rozstaw. **Wypełnij przestrzenie** kładzie materiał rdzenia między belkami. Konfigurator pokazuje przykładowy strop z klatką schodową.

![Typ stropu na belkach](shots/wood-joists.webp)

Belki zastępują rdzeń stropu; warstwy nad nim i pod nim pozostają. Ich elementy znajdują się w zestawieniu drewna, a w IFC każda z nich to IfcBeam (JOIST, belka obwodowa lub wymian według nazwy) pod stropem.

## Zestawienie drewna

**Drewno → Zestawienie tarcicy** tworzy listę cięć każdego kawałka drewna w projekcie, elementów ścian i więźby dachów: element, przekrój, klasa, długość, sztuki, długość łączna i objętość, z sumami według przekroju. W IFC każdy element to IfcMember (STUD, PLATE, nadproże lub podwalina podokienna według nazwy) pod swoją ścianą, a izolacja to IfcCovering INSULATION.
