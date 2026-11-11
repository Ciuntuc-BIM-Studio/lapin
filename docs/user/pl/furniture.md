---
title: Meble
order: 47
summary: Sofy, narożniki, fotele, krzesła, łóżka, szafy, komody, regały, kredensy, stoły, biurka i kuchnie w sześciu układach z typów parametrycznych, dosuwane do ścian jednym kliknięciem.
---

## Wstawianie mebli

Karta **Wnętrza** ma, po wykończeniach, narzędzie dla każdego rodzaju mebla: **Sofa** (<kbd>SO</kbd>), **Narożnik**, **Fotel** (<kbd>AC</kbd>), **Krzesło**, **Łóżko** (<kbd>BD</kbd>), **Szafka nocna**, **Szafa** (<kbd>WD</kbd>), **Komoda**, **Regał na książki**, **Kredens**, **Stół** (<kbd>TB</kbd>), **Stolik kawowy** i **Biurko**. Wybierz jedno i kliknij w rzucie.

Kliknij blisko ściany, a mebel do niej dosunie się tyłem. Jego tył leży na licu ściany (na tynku, nie na rdzeniu), przód zwraca się do pomieszczenia, a mebel przesuwa się wzdłuż ściany, aż przestanie wchodzić w ścianę w narożniku. Kliknij dalej od ścian, a mebel stanie tam, gdzie kliknięto.

Gdy narzędzie jest włączone, pasek wiersza poleceń pokazuje:

- **typ**, który wstawia narzędzie, wybrany spośród typów tego rodzaju w projekcie (jeśli jeszcze go nie ma, pierwszy powstaje przy wstawianiu mebla);
- **obrót**, 0°, 90°, 180° lub 270°, dla mebla stojącego wolno;
- **Przy ścianach**, które możesz wyłączyć, aby wstawić mebel dokładnie tam, gdzie klikasz, nawet obok ściany.

Panel właściwości pokazuje ten sam typ jeszcze przed kliknięciem, z jego wymiarami i **Edytuj typy…**.

![Umeblowany pokój dzienny, kuchnia i sypialnia w rzucie](shots/furniture-plan.webp)

Rzuty rysują meble ich symbolem, zgodnie z konwencją rysunkową, a nie w przekroju:

- sofa z oparciem, podłokietnikami i poduszkami;
- łóżko z wezgłowiem, poduszkami i odwiniętą kołdrą;
- szafa z drążkiem na wieszaki i drzwiami;
- kuchnia ze zlewem i płytą grzejną, a szafki górne linią przerywaną.

Widoki i przekroje rysują bryły z kreskowaniem ich materiałów. W 3D meble są bryłami low-poly, które tworzy Lapin.

![Umeblowany dom w 3D](shots/furniture-3d.webp)

## Krzesła wokół stołu i kuchnia

**Krzesła wokół stołu:** kliknij stół lub biurko. Krzesła stają wzdłuż dłuższych boków, mniej więcej jedno co 600 mm, i po jednym na każdym końcu, jeśli jest dość szeroki; stół okrągły dostaje je na okręgu. Każde krzesło jest zwrócone do stołu. Biurko dostaje krzesła tylko od przodu. Krzesła mają typ wybrany dla narzędzia **Krzesło**.

**Kuchnia** (<kbd>KT</kbd>) rysuje się dwoma punktami wzdłuż ściany:

- długość linii to długość kuchni, a jej tył leży na licu ściany;
- szafki dolne dzielą się równo, po około 600 mm;
- ma blat roboczy, zlew i płytę grzejną (ich miejsce wzdłuż kuchni ustala się w typie), szafki górne nad nimi i słupek na lodówkę na jednym końcu.

**Długość** kuchni można dalej zmieniać we właściwościach; szafki dzielą się na nowo.

### Układy kuchni

Typ kuchni ma **Layout** (układ): **One wall** (prosty ciąg), **Galley** (dwa równoległe ciągi z przejściem między nimi), **L-shaped**, **U-shaped**, **Island** (ciąg na ścianie tylnej i osobna wyspa z własnym przejściem) lub **Peninsula** (L z ciągiem wychodzącym z ramienia, z blatem wysuniętym na stołki). Wybór układu rozpoczyna typ od jego wymiarów, a każdy z nich można zmieniać obok podglądu: długości ramion od ściany tylnej, przejście między ciągami, długość przeciwległego ciągu, długość, głębokość i miejsce wyspy, długość i nawis półwyspu oraz, w L lub półwyspie, narożnik z lewej lub z prawej.

![Sześć układów w rzucie: jednorzędowy, dwurzędowy, L, U, z wyspą i z półwyspem](shots/kitchen-layouts.webp)

**Sink on** i **Hob on** umieszczają je na ciągu głównym lub na drugiej części (ramię, przeciwległy ciąg, wyspa, półwysep), **Sink at** i **Hob at** mówią, gdzie wzdłuż niej, a **Tall unit (fridge)** stoi na wolnym końcu. Szafki narożne są ślepe; szafki wiszące są tylko na ciągach przy ścianie, nie na wyspie ani półwyspie.

![Typ kuchni: w kształcie L](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) rysuje dowolny układ dwoma punktami wzdłuż ściany, przy której stoi ciąg główny: linia to jego długość, tył na licu ściany, a L, półwysep, U lub kuchnia dwurzędowa wsuwa się w narożnik lub między ściany, przy których jest rysowana. **Flip** daje L lub półwyspowi narożnik po drugiej stronie. Zestawienie mebli nazywa układ i podaje głębokość obrysu. W IFC pozostaje IfcFurniture typu Kitchen zdefiniowanego przez użytkownika.

### Szafki i urządzenia kuchni

Typ kuchni może wymieniać własne **szafki** pod swoimi parametrami w **Typy mebli**, w dwóch liniach:

- **Linia szafek dolnych**: szafki ciągu głównego od lewej, patrząc od frontu (po narożniku ślepym);
- **Linia szafek wiszących**: szafki zawieszone nad nią.

**Dodaj** stawia szafkę wybranego rodzaju na końcu linii. **Typowy ciąg** zaczyna linię szafek dolnych od zlewu, zmywarki, szafki z szufladami i płyty nad piekarnikiem. Strzałki przesuwają szafkę w lewo lub w prawo, a kosz ją usuwa.

Każda szafka ma **szerokość**. Szafka przyjmuje dowolną szerokość od 100 do 800 mm: wybierz 150, 200, 300, 400, 450, 500, 600 lub 800 albo wpisz inną. Urządzenie przyjmuje rozmiary, w jakich jest produkowane. Szafki linii dolnej to:

- **Szafka dolna**, której **Front** to szuflada nad drzwiczkami, drzwiczki (jednoskrzydłowe lub dwuskrzydłowe, ze stroną, po której zawieszone są pojedyncze drzwiczki, **Zawiasy po stronie**), szuflady (od 1 do 4), szuflady na garnki (płytka szuflada nad dwiema głębokimi) albo otwarte półki;
- **Szafka pod zlewozmywak**: zlew jednokomorowy, półtorakomorowy, dwukomorowy (od 800) lub narożny, z baterią zwykłą, wyciąganą albo bez baterii. Zlew narożny sięga w narożnik ślepy, gdy stoi obok niego.
- **Płyta nad piekarnikiem**, o szerokości 600, 800 lub 900, indukcyjna lub gazowa, z piekarnikiem pod nią lub bez;
- **Kuchnia wolnostojąca**, o szerokości 500, 600 lub 900, indukcyjna lub gazowa. Blat kończy się po obu jej stronach.
- **Zmywarka** do zabudowy, o szerokości 450 lub 600, za frontem na całą wysokość, pasującym do pozostałych;
- **Lodówka**: podblatowa, wysoka do zabudowy (drzwi lodówki nad drzwiami zamrażarki) albo wolnostojąca;
- **Szafka wysoka na piekarnik**, z szufladami pod piekarnikiem i drzwiczkami nad nim;
- **Szafka wysoka**, z drzwiczkami lub otwartymi półkami;
- **Wypełnienie** i **Przerwa**.

Szafka wysoka, lodówka lub szafka na piekarnik ma własną **Wysokość**. W linii szafek wiszących **Szafka wisząca** ma drzwiczki, szklane drzwiczki lub otwarte półki, własną **Wysokość** i własną wysokość **Od podłogi**. Pozostaw jedną z nich pustą, aby przyjąć wartości ciągu. **Okap** to okap kominowy (650 nad blatem, z przewodem do góry szafek wiszących) albo okap zintegrowany w szafce wiszącej. **Przerwa** zostawia ścianę gołą. Szafki wiszące omijają szafkę wysoką.

Bez wymienionej linii szafek wiszących każda szafka dolna dostaje szafkę wiszącą o swojej szerokości, a płyta lub kuchnia dostaje okap kominowy. Na wyspie lub półwyspie **Okap nad płytą tam** wiesza okap nad płytą, która na niej stoi.

To, co szafki zostawiają z ciągu, dzieli się po równo jak dawniej, a mniej niż 100 mm staje się wypełnieniem. Szafki, które nie mieszczą się w ciągu, są pomijane, a podgląd i element to zgłaszają. **Dopasuj do szafek** we właściwościach kuchni ustawia jej długość na tyle, ile potrzebują jej szafki: ich własną, plus narożnik ślepy i szafka wysoka na końcu. Typ bez własnych szafek jest budowany jak dawniej.

W rzucie każda szafka ma własny symbol:

- otwieranie dolnych drzwiczek, linią przerywaną i tylko w najdrobniejszym szczególe;
- komory zlewu i jego bateria;
- strefy lub palniki płyty;
- szafki wysokie przekreślone;
- **DW** na zmywarce, **F** na lodówce i **OV** na szafce na piekarnik;
- szafki wiszące i okapy nad nimi.

Elewacje i 3D pokazują każdy front, uchwyt i urządzenie.

Materiały mają dwie role więcej: **Cokół** i **Uchwyty**, obok frontów, korpusu, blatu (płyty), metalu i szkła. Typ kuchni zrobiony wcześniej przyjmuje na cokół materiał swojego korpusu, a na uchwyty materiał swojego metalu.

![Szafki typu kuchni: linia szafek dolnych i wiszących, z podglądem silnika](shots/kitchen-units-types.webp)

![Kuchnia z mieszanych szafek i urządzeń, w 3D](shots/kitchen-units-3d.webp)

**Wnętrza → Szafki kuchenne** tworzy zestawienie szafek każdej kuchni, od lewej, najpierw linia dolna, potem wiszących, z ich szerokością, wysokością i frontem lub urządzeniem. W IFC kuchnia pozostaje jednym IfcFurniture swojego typu, a szafki podróżują z typem, więc Lapin odczytuje je z powrotem.

## Właściwości mebla

Zaznacz mebel, aby zobaczyć:

- **Typ**, jego rodzaj i wymiary;
- **Obrót**;
- **Nad podłogą**, dla mebla podniesionego na podeście;
- **Długość**, dla kuchni.

**Odwróć** daje meblowi drugą stronę w miejscu, gdzie stoi: szezlong po drugiej stronie, lodówkę na drugim końcu, narożnik narożnika po drugiej stronie. Jego przód zostaje tam, gdzie był. **Modyfikuj → Lustro** robi to samo.

Przeciągnij uchwyt na środku mebla, aby go przesunąć; przeciągnij uchwyt przed nim, aby obrócić jego przód w stronę kursora.

![Właściwości sofy](shots/furniture-properties.webp)

## Typy mebli

**Wnętrza → Typy mebli** wymienia typy projektu według grup (siedziska, łóżka, przechowywanie, stoły, kuchnia), każdy z liczbą mebli, które go używają. Wybierz rodzaj i **Nowy typ** albo **Duplikuj** typ, aby zmienić kopię. Typ zawiera:

- swoją **nazwę**, **szerokość**, **głębokość** i **wysokość** (dla sofy oparcia, dla łóżka wezgłowia, dla kuchni blatu roboczego);
- własne parametry swojego rodzaju:
  - sofy i fotele: miejsca, podłokietniki, szerokość podłokietnika, wysokość siedziska, szezlong po lewej lub prawej i jego długość, nóżki;
  - narożnik: strona, po której jest narożnik, i długość tego boku;
  - łóżko: wierzch materaca, wezgłowie, zagłówek w nogach, poduszki, odwinięta kołdra;
  - szafa: drzwi, uchylne lub przesuwne, szuflady na dole, cokół;
  - komoda: rzędy i kolumny szuflad;
  - regał na książki: półki i drzwiczki na dole do pewnej wysokości;
  - kredens: drzwiczki i szuflady;
  - stół: prostokątny, okrągły lub owalny, na czterech nogach lub jednej nodze centralnej;
  - biurko: kontener z szufladami po lewej lub prawej;
  - kuchnia: szerokość szafek dolnych, cokół, blat roboczy, miejsce zlewu i płyty grzejnej, szafki górne, słupek i własne szafki (wyżej);
- swoje **materiały** według roli: korpus, tapicerka, blat, fronty, pościel, metal, szkło, a w kuchni cokół i uchwyty. Wpisz dowolną nazwę materiału; materiał, którego projekt nie ma, zostaje utworzony, a **Zarządzaj → Materiały** ustala jego kolor i kreskowania.

Podgląd obok pól pokazuje symbol typu w rzucie i jego widok z przodu na bieżąco, gdy go zmieniasz. Każdy mebel danego typu podąża za każdą zmianą.

![Okno Typy mebli z sofą i jej szezlongiem](shots/furniture-types.webp)

## Zestawienia i IFC

**Zestawienie mebli** wymienia każdy typ z jego rodzajem, wymiarami, liczbą mebli i pomieszczeniami, w których stoją, wraz z sumą. **Meble według pomieszczeń** wymienia każde pomieszczenie z tym, co w nim stoi. Oba są w przeglądarce projektu, w **Zestawieniach**, i drukują się oraz eksportują jak każde zestawienie. Typ mebla z pozycją kosztorysową jest wyceniany za sztukę w kosztorysie.

W IFC mebel to **IfcFurniture** z typem predefiniowanym dla swojego rodzaju (SOFA, CHAIR, BED, TABLE, DESK, SHELF albo USERDEFINED z Wardrobe, Kitchen…). Jego **IfcFurnitureType** jest przypisany przez IfcRelDefinesByType, a bryły niosą swoje materiały. Lapin odczytuje własne meble z powrotem jako natywne, edytowalne meble. Meble z innych programów wchodzą jako odniesienia.
