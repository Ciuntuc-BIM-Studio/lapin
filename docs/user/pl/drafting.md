---
title: Rysowanie i wiersz poleceń
order: 80
summary: Wpisuj polecenia jak w CAD, przyciągaj do punktów, przesuwaj i kopiuj, rysuj linie i opisuj rysunek wymiarami, tekstami i etykietami.
---

## Wiersz poleceń

Wpisz litery narzędzia w dowolnym miejscu okna, a potem naciśnij <kbd>Enter</kbd>. Na przykład wpisz <kbd>WA</kbd> dla ściany, <kbd>CO</kbd>, aby kopiować, <kbd>M</kbd>, aby przesunąć, lub <kbd>L</kbd> dla linii. Wiersz poleceń u dołu pokazuje, czego narzędzie potrzebuje dalej. Możesz kliknąć punkt albo wpisać długość, kąt lub współrzędne.

Wpisane współrzędne są w jednostce projektu, tak jak długości obok kursora: w projekcie w milimetrach `@4000,4000` po pierwszym narożniku prostokąta daje prostokąt 4 m na 4 m, a `4000<90` to 4 m prosto w górę.

![Kopiowanie ściany: po punkcie bazowym wiersz poleceń i dynamiczne wprowadzanie pokazują długość i kąt do kursora](shots/command-line.webp)

Każde narzędzie pokazuje swoje litery w podpowiedzi; zmienisz je w **Widok → Skróty klawiszowe…** (zobacz [Interfejs i skróty](interface.md)). Oto najczęściej używane:

| Model | | Rysowanie | |
|---|---|---|---|
| Ściana | <kbd>WA</kbd> | Linia | <kbd>L</kbd> |
| Drzwi, okno | <kbd>DR</kbd>, <kbd>WN</kbd> | Polilinia | <kbd>PL</kbd> |
| Strop | <kbd>SB</kbd> | Prostokąt, okrąg, łuk | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Dach | <kbd>RF</kbd> | Przesuń, kopiuj | <kbd>M</kbd>, <kbd>CO</kbd> |
| Schody, pomieszczenie | <kbd>ST</kbd>, <kbd>RM</kbd> | Obróć, odbij lustrzanie, skaluj | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Słup, belka | <kbd>CL</kbd>, <kbd>BM</kbd> | Odsuń, utnij, wydłuż | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Przekrój | <kbd>SE</kbd> | Zaokrąglenie, fazowanie | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Mierz | <kbd>ME</kbd> | Kreskowanie, tekst | <kbd>H</kbd>, <kbd>T</kbd> |
| Dopasuj wszystko | <kbd>ZE</kbd> | Wymaż | <kbd>E</kbd> |

## Przyciąganie i pomoce

Lapin przyciąga do końców, środków i przecięć tego, co narysowane, oraz do siatki. <kbd>F3</kbd> włącza lub wyłącza przyciąganie, <kbd>F8</kbd> orto, a <kbd>F10</kbd> śledzenie biegunowe. Ich stan widać w prawym dolnym rogu widoku.

## Zaznaczanie

Kliknij element, aby go zaznaczyć; <kbd>Shift</kbd>+kliknięcie dodaje do zaznaczenia. Aby zaznaczyć kilka naraz, przeciągnij lewym przyciskiem tam, gdzie nic nie jest zaznaczone. Przeciągnięta w prawo ramka jest niebieska i bierze to, co leży całkowicie w jej wnętrzu (okno); przeciągnięta w lewo jest zielona i przerywana i bierze wszystko, czego dotyka (przecięcie), jak w AutoCAD. Naciśnij <kbd>Space</kbd> podczas przeciągania, a ramka zmieni się w lasso, które podąża za myszą i zachowuje tryb ramki; ponowne <kbd>Space</kbd> wraca do ramki. W wierszu poleceń, gdy polecenie prosi o wybranie obiektów, wpisz **WP** lub **CP** i kliknij narożniki wieloboku, a potem <kbd>Enter</kbd>: wielobok okna lub wielobok przecięcia.

![Lasso na rzucie: ramka przeciągnięta w prawo, potem Space i pętla wokół dwóch ścian, które bierze](shots/select-lasso.webp)

## Kopiowanie i przesuwanie

Aby skopiować, zaznacz element i wpisz <kbd>CO</kbd>. Kliknij punkt bazowy, a potem drugi punkt. Lapin kopiuje dalej, dopóki nie naciśniesz <kbd>Enter</kbd> lub <kbd>Esc</kbd>. **Przesuń** (<kbd>M</kbd>) działa tak samo.

Podczas przesuwania lub kopiowania na punkcie bazowym zostaje znacznik, a od niego do myszy biegnie przerywana linia; wyblakły zarys tego, co przesuwasz, zostaje w starym miejscu, dopóki nie klikniesz lub nie naciśniesz <kbd>Esc</kbd>. Linia i znacznik zmieniają kolor zależnie od przyciągania: zielony na przyciągniętym punkcie, niebieski, gdy orto lub śledzenie biegunowe trzyma kierunek, kolor akcentu na linii śledzenia, szary, gdy ruch jest swobodny. Obok myszy Lapin pokazuje, o ile przesunąłeś: Δx, Δy, odległość i kąt, w jednostkach projektu. Możesz tam wpisać odległość i kąt, jak podczas rysowania. W interfejsie Kompaktowym te wartości są w wierszu stanu. Przeciąganie uchwytu pokazuje to samo, tak samo przeciąganie samego zaznaczonego elementu, od punktu, w którym nacisnąłeś przycisk; naciśnij <kbd>Esc</kbd>, zanim puścisz, a element zostanie na miejscu.

![Przesuwanie ściany: znacznik na punkcie bazowym, przerywana linia do myszy, cień ściany w starym miejscu i przesunięcie przy myszy](shots/move-trace.webp)

Możesz też po prostu przeciągać. Przeciągnij krzyżyk na środku zaznaczonego elementu, aby go przesunąć. Przytrzymaj <kbd>Ctrl</kbd> (<kbd>⌘</kbd> na Macu) podczas przeciągania, aby zostawić kopię.

Drzwi, okno lub otwór w ścianie przesuwa się i kopiuje wzdłuż swojej ściany: liczy się tylko część przesunięcia wzdłuż ściany, więc `500` wpisane po punkcie bazowym przesuwa go o 500 wzdłuż ściany; przesunięty do innej ściany przechodzi do niej. Gdy zaznaczona jest też jego ściana, przesuwa się razem ze ścianą.

Oś siatki przesuwa się, kopiuje, obraca i odbija lustrzanie sama albo razem z innymi elementami. Kopia jest nową osią tej samej siatki, nazwaną według ostatniej osi biegnącej w tym samym kierunku: przy osiach 1, 2 i 3 kopia każdej z nich to 4.

## Rysowanie

Karta **Rysunek** rysuje linie, polilinie, prostokąty, okręgi i łuki. Edytuje je poleceniami odsuń, utnij, wydłuż, zaokrąglenie, fazowanie i połącz, wypełnia obszary kreskowaniem i porządkuje je na warstwach (**Warstwy**). **Importuj DXF** wczytuje rysunek CAD jako linie, do których możesz przyciągać i które możesz edytować.

![Karta Rysuj](shots/ribbon-draft.webp)

**Odsuń** (<kbd>O</kbd>) kopiuje linię, polilinię, prostokąt, okrąg lub łuk w odległości. Wpisz odległość w jednostce projektu (w projekcie w milimetrach `500` to pół metra; `2m` lub `1'6"` mówią swoją jednostkę), kliknij kształt, a potem stronę, na którą idzie; pyta o kolejny kształt, aż naciśniesz <kbd>Enter</kbd> lub <kbd>Esc</kbd>. Wpisz zamiast tego **T** (Through), a każda kopia przejdzie przez kliknięty punkt. Narożniki polilinii zostają ostre (na ucios), jak w CAD. Odsunięcie działa na rzutach, przekrojach, elewacjach, na płaszczyznach odniesienia i w widoku 3D.

**Rozbij** (<kbd>X</kbd>, lub **EXPLODE**) to droga powrotna od **Połącz**: zaznacz polilinie lub prostokąty, a każda z nich staje się swoimi liniami, po jednej na bok, na tej samej warstwie, tym samym piórem i kolorem i w tej samej grupie, w jednym kroku cofania. Okręgi, łuki, kreskowania i łańcuchy wymiarów zostają, jakie były, a zaznaczenie bez niczego do rozbicia mówi o tym. Działa w rzutach, przekrojach, elewacjach, na płaszczyznach odniesienia i na płaszczyźnie roboczej w widoku 3D.

Każda długość, o którą pyta wiersz poleceń, jest w jednostce projektu, a monit mówi, w jakiej: `Depth (mm)`, `Height (ft-in)`. Kąty, współczynniki i liczności to zwykłe liczby: `90` po **Obróć** to 90°, bez względu na jednostkę.

### Odrysowywanie PDF lub obrazu

**Plik → Importuj PDF lub obraz…** albo **Rysunek → PDF lub obraz** wprowadza rysunek do odrysowania: zeskanowany rzut, zdjęcie elewacji, PDF z innego biura. Trafia do rzutu, przekroju lub elewacji na wierzchu, pod ich rysunek.

- **PDF**: wybierz **Stronę** i skalę, w jakiej wykonano jego rysunek (**Narysowany w 1:**). Milimetr papieru mierzy wtedy tu tyle milimetrów, więc rzut 1:100 wchodzi w swojej rzeczywistej wielkości. Linie strony wchodzą jako rysunek, do którego możesz się przyciągać, który możesz edytować i odrysowywać, a jej obrazy (skan w PDF) jako obrazy pod nimi. Jedno i drugie można pominąć. Strona wchodzi jako jedna grupa nazwana od pliku, więc **Przesuń**, **Obróć** i **Skaluj** biorą ją całą; kliknij ją dwukrotnie, aby pracować na jej częściach.
- **Obraz** (PNG lub JPEG): podaj jego **Szerokość**; wysokość wynika z pikseli. **Rysunek → Skaluj obraz** ustala ją potem dokładnie: zaznacz obraz, kliknij na nim dwa punkty o znanej odległości i wpisz tę odległość.

Obraz jest przezroczysty (jego **Krycie**, domyślnie 0,5, w jego właściwościach) i nigdy nie należy do modelu: drukuje się na arkuszach tylko wtedy, gdy zostawisz go w widoku.

![Rzut wydrukowany do PDF, wprowadzony na inną kondygnację do odrysowania](shots/trace-pdf.webp)

### Rysowanie w widoku 3D

**Linia**, **Polilinia**, **Prostokąt**, **Okrąg**, **Łuk** i edycje z Rysunku działają też w widoku 3D, z wierszem poleceń na dole. Gdy polecenie trwa, etykieta u góry mówi, na czym rysujesz: na poziomie aktywnej kondygnacji lub na zaznaczonej płaszczyźnie odniesienia. **Wybierz ścianę bryły** w etykiecie, a potem kliknięcie ściany modelu, tworzy na niej płaszczyznę odniesienia i rysuje na niej. Punkty przyciągają się jak na rzucie: do linii przekroju rzutu (narożników i lic ścian) i do tego, co narysowano na tej samej płaszczyźnie, z orto i liniami śledzenia. To, co rysujesz, jest tym samym elementem rysunku co narysowany na rzucie i widać go też tam.

![Rysowanie w widoku 3D: etykieta mówi płaszczyznę, linia idzie za myszą na poziomie kondygnacji](shots/draft-3d.webp)

### Rysunki w widoku 3D

To, co narysujesz na rzucie (linie, polilinie, prostokąty, okręgi, łuki), stoi też w widoku 3D jako linie na poziomie kondygnacji (na samym poziomie, nie na wysokości cięcia); to, co narysujesz na płaszczyźnie odniesienia, przekroju lub elewacji, stoi na tej płaszczyźnie. Kliknij jeden w 3D, aby go zaznaczyć, a potem przesuń, skopiuj, odsuń lub usuń. **Widok → Rysunki w 3D**, także w **Widok → Ustawienia widoku** widoku 3D, pokazuje lub ukrywa je wszystkie; jest włączone, dopóki go nie wyłączysz. Wymiary, teksty, etykiety, kreskowania i linie pomocnicze (rola draft) zostają w swoich widokach. DXF wczytany do widoku leży też w 3D, na płaszczyźnie tego widoku, z własnym przełącznikiem (zob. [Import i eksport](import-export.md)).

### Kolejność rysowania

Tam, gdzie narysowane elementy się nakładają, ten narysowany później leży na wierzchu. Aby to zmienić, zaznacz je (linie, polilinie, prostokąty, okręgi, łuki, kreskowania i wypełnienia, teksty, wymiary, etykiety, kształty z DXF), kliknij prawym przyciskiem i wybierz **Przesuń na wierzch**, **Przesuń do przodu**, **Przesuń do tyłu** lub **Przesuń na spód**; **Rysunek → Kolejność** i **Opisz → Kolejność** mają to samo, z <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>[</kbd> i <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>[</kbd> (⌘ na Macu). Do przodu i do tyłu mijają następny element, na który nachodzi zaznaczenie. Rysunek 2D leży nad rysunkiem modelu; **Przesuń za model** kładzie go pod przekrój ścian i linie widoczne (kreskowanie pod ścianą), a kliknięcie tam wybiera wtedy ścianę; **Przesuń na wierzch** lub **Przesuń na spód** przenoszą go znów nad model. W wierszu poleceń **DRAWORDER** (**DRO**) pyta o obiekty, potem **Above objects** lub **Under objects** (kliknij elementy, nad lub pod które mają trafić, potem <kbd>Enter</kbd>), **Front**, **Back** albo za model, **Model**. Kolejność należy do widoku, cofa się jednym krokiem, a arkusze, PDF i DXF drukują ją tak, jak ją widzisz; to, co narysujesz później, trafia na wierzch.

![Prawy klik na zaznaczonym wypełnieniu: kolejność rysowania](shots/draw-order-menu.webp)

![Przesunięte na wierzch szare wypełnienie zakrywa tekst](shots/draw-order-front.webp)

## Opisywanie

Karta **Opisz** zawiera wymiary, teksty, osie siatki, poziomy i etykiety. Wymiar odczytuje model, więc aktualizuje się, gdy ściana się przesunie. Wygląd tekstów, wymiarów, osi i etykiet ustawisz w **Zarządzaj → Standardy rysunkowe**.

Wymiar wyrównany lub liniowy wstawiasz jako łańcuch: po pierwszych dwóch punktach i jego linii każde kliknięcie dodaje kolejny punkt na tej samej linii, a **Enter** (lub **Esc**) robi z całego łańcucha jeden wymiar (**U** cofa ostatni punkt; wpisz najpierw **S**, aby uzyskać pojedynczy wymiar). Łańcuch zachowuje się jak polilinia: kliknięcie zaznacza go całego, a przesuwa się, kopiuje i usuwa jako całość. Łańcuch mierzący ściany, krawędzie lub osie siatki zostaje na nich: przeciągnięty lub przesunięty sam, przesuwa tylko swoją linię wymiarową, prostopadle do łańcucha, więc omsknięcie myszy nigdy nie zostawi go mierzącego pustą przestrzeń; przesunięty razem z tym, co mierzy, idzie razem z tym. Jego uchwyty leżą na każdym mierzonym punkcie (przeciągnij, aby go przesunąć, Ctrl/⌘+kliknięcie go usuwa), na środku każdego segmentu (przeciągnij, aby dodać punkt, Ctrl/⌘+kliknięcie dodaje go w połowie) i na końcach linii wymiarowej (przeciągnij, aby przesunąć linię). Aby dodać więcej punktów do łańcucha, jak w ArchiCAD, zaznacz go i kliknij **Opisz → Dodaj punkty** (albo wpisz **DIMADD** lub użyj **Dodaj punkty** w jego właściwościach): każdy kliknięty punkt trafia do łańcucha tam, gdzie wypada na jego linii, między sąsiednimi punktami lub za końcem jako nowy koniec, i trzyma się ściany, krawędzi lub osi, na którą trafi; **Enter** lub **Esc** kończy. Aby usunąć punkty, kliknij z Ctrl/⌘ ich uchwyty albo kliknij **Opisz → Usuń punkty** (**DIMDEL** lub **Usuń punkty** we właściwościach) i kliknij blisko każdego punktu; segmenty po obu stronach łączą się w jeden. Wartość każdego segmentu też ma uchwyt: przeciągnij go, aby odsunąć wartość z jej miejsca tam, gdzie segmenty są krótkie, a wartości by się nakładały; gdy wartość stoi z dala od segmentu, łączy ją z nim cienka linia odniesienia. Ctrl/⌘+kliknięcie uchwytu albo **Teksty z powrotem na linii** we właściwościach przywraca ją na miejsce. Przesunięta wartość zostaje przy swoim segmencie, gdy punkty się przesuwają, i obraca się razem z łańcuchem. Kliknij dwukrotnie segment, aby wpisać jego wartość: mierzony element przesuwa się, a z nim wszystko, co jest za nim w łańcuchu. Właściwości pokazują wartość i tekst każdego segmentu, dzielą łańcuch na pojedyncze wymiary i łączą pojedyncze wymiary, które leżą jeden za drugim na jednej linii.

Opisy na rzucie (wymiary, teksty, narysowane linie, etykiety) mogą przejść na inny rzut w to samo miejsce, bo rzuty mają te same współrzędne: zaznacz je i wybierz rzut we właściwościach, w **Widok**, by je przenieść, lub w **Kopiuj do**, by je skopiować. Tak również wymiary zrobione na planie sytuacyjnym trafiają na rzut poziomu. Punkty wymiaru trzymają się wtedy tego, co nowy rzut przecina w tym miejscu: ścian, krawędzi, osi.

**Wysokości otworów.** Segment łańcucha wymiarów, który biegnie od jednego ościeża drzwi, okna lub otworu do drugiego, pisze szerokość nad linią, a wysokość otworu pod nią, jak w rysunkach rumuńskich i europejskich. Jest odczytywana z modelu za każdym razem, więc idzie za otworem, gdy zmienia rozmiar, jest przesuwany lub dostaje inny typ; segment, nad którym wpisano własny tekst, jej nie pisze. **Zarządzaj → Standardy rysunkowe → Wymiary → Wysokość otworu** ustawia ją dla każdego stylu wymiaru: wyłączona, `150`, `h=150`, `h=150` z parapetem `p=90` pod nim albo `150/90`, w jednostce stylu. Działa w rzutach, przekrojach i elewacjach, a arkusze, PDF i DXF ją drukują. Nowe projekty mają ją włączoną (sama wysokość); projekt zrobiony wcześniej zachowuje wymiary, jakie były, dopóki jej tam nie włączysz.

![Wysokości drzwi i okna pod ich segmentami](shots/dimension-opening-height.webp)

![Wysokość otworu w stylach wymiarów](shots/dimension-opening-height-style.webp)

**Wymiary na linii.** Punkt wymiaru kliknięty na osi siatki lub na osi ściany trzyma się linii, a nie punktu na niej. Przeciągnij koniec osi wzdłuż niej samej, aby jej bąbel zmieścił się w widoku, albo wydłuż lub skróć ścianę: wymiar nadal mierzy tam, gdzie go postawiono, a jego linia się nie obraca. Jeśli oś lub ściana przesunie się w poprzek, punkt idzie z nią, w tym samym miejscu wzdłuż. Wymiar na licu ściany również zostaje tam, gdzie mierzył, gdy ścianę rozciągnięto wzdłuż. Wymiary zrobione wcześniej trzymają się osi siatki tak samo; ten na osi ściany przypina się na nowo przez ponowne kliknięcie jego punktu.

### Etykiety

**Opisz → Etykieta** (<kbd>TG</kbd>) umieszcza na widoku ramkę z tym, co element mówi o sobie, i linię odniesienia do niego. Gdy narzędzie jest włączone, pasek pod widokiem wybiera, co pokazuje etykieta: znak, nazwę, klasę lub typ, **Odporność ogniowa**, **Izolacyjność akustyczna**, **Współczynnik przenikania ciepła** albo **Inny parametr…** po nazwie; albo typ etykiety ze standardów rysunkowych; a **ODNOŚNIK** włącza lub wyłącza linię odniesienia. Kliknij element, potem miejsce etykiety (najpierw wpisz **E**, aby załamać linię). **Opisz → Oznacz parametr** pyta o nazwę parametru i uruchamia z nim narzędzie.

![Pasek narzędzia Etykieta pod widokiem](shots/tag-options.webp)

Etykieta odczytuje dowolny parametr: elementu, jego typu (wtedy pokazują go wszystkie elementy typu), właściwość standardową, np. `Pset_WallCommon.FireRating`, albo taką, z którą przyszedł element zaimportowany. Nazwa jest znajdowana także bez spacji, więc **Fire rating** i `FireRating` to to samo. Aby ją wypełnić, użyj **Parametry** we właściwościach elementu lub typu: wpisz nazwę (proponowane są Fire rating, Sound rating, Moisture resistance i Thermal resistance) albo wybierz **Dodaj właściwość standardową…** dla właściwości IFC jego klasy. Etykieta podąża, gdy tylko jest wartość, a długości są zapisywane w jednostkach projektu.

Zaznaczona etykieta ma uchwyty. Przeciągnij ramkę, aby przesunąć etykietę: linia odniesienia wskazuje to samo miejsce. Etykieta idzie za swoim elementem: gdy element zostanie przesunięty, obrócony, odbity lub zmieniony uchwytami, etykieta podąża za nim, zachowując swoje miejsce obok niego, a linia odniesienia pozostaje na nim; etykieta przesunięta przez Ciebie zachowuje nadane jej przesunięcie. Przeciągnij środek linii, aby ją załamać (Ctrl/⌘-klik na załamaniu prostuje ją), a jej koniec, aby wskazywała inny punkt (Ctrl/⌘-klik: z powrotem na środek elementu). We właściwościach zmieniasz, co etykieta **Pokazuje**, dajesz jej własny **Tekst** (`{mark}|{FireRating}` pisze dwa wiersze), **Typ etykiety** albo wyłączasz **Linię odniesienia**. Etykiety rysuje silnik, więc arkusze i PDF drukują je tak, jak je widzisz. Typy etykiet w **Zarządzaj → Standardy rysunkowe → Etykiety** odczytują te same pola w swoich szablonach; nowy projekt ma wśród nich Odporność ogniową, Izolacyjność akustyczną i Współczynnik przenikania ciepła.

**Odnośniki jak etykiety ArchiCAD.** Domyślnie odnośnik etykiety jest *łamany*: ukośna linia od elementu do kolana, potem ramię biegnące pod tekstem i podkreślające jego pierwszy wiersz, bez ramki wokół tekstu. Po kliknięciu elementu następne kliknięcie to kolano, a tekst staje obok. Przeciągnij uchwyt kolana: ukośna linia trzyma 30°, 45°, 60° lub 90° od swojego końca (przytrzymaj <kbd>Shift</kbd> dla dowolnego kąta), a przeciągnięta za koniec przenosi tekst na tę stronę, wyrównany do kolana. Przeciągnij tekst, aby przesunąć go razem z kolanem. W **Zarządzaj → Standardy rysunkowe → Etykiety** przycisk **Odnośnik…** przy typie etykiety (i edytor warstw przegrody) ustawia **Kształt odnośnika** (łamany lub prosty z ramki), co podkreśla ramię (**Podkreślenie**: pierwszy wiersz, każdy wiersz, ostatni wiersz lub nic, gdy kończy się przy tekście) oraz **Przedłużenie mm**, o ile dalej ramię sięga od tekstu, obok podglądu silnika. Przy jednym odnośniku na warstwę każdy kończy się pod swoim wierszem, a kolana leżą w jednej kolumnie; przeciągnięcie kolana przesuwa kolumnę. **Kształt odnośnika** we właściwościach etykiety ma pierwszeństwo przed typem; etykieta, której odnośnik załamano w wolnym punkcie, zachowuje prosty odnośnik. Etykiety w projektach utworzonych wcześniej zachowują prostą linię odniesienia w ramce.

![Załamana linia odniesienia etykiety z jej uchwytami](shots/tag-leader.webp)

![Właściwości etykiety](shots/tag-properties.webp)

Znacznik drzwi lub okna edytuje się część po części: każdy tekst przesunięty lub obrócony, każdy koniec jego linii wydłużony; zob. [Drzwi i okna](doors-windows.md).

**Obracanie etykiety.** We właściwościach etykiety **Obrót** obraca jej tekst i półkę lub ramkę o kąt w stopniach od poziomu, a **Wyrównaj do elementu** każe jej podążać za kierunkiem elementu, który opisuje (osią ściany lub belki, najdłuższą krawędzią czegokolwiek innego) i obracać się razem z nim; obrót dodaje się wtedy do tego kierunku. Etykieta jest zawsze rysowana czytelnie, nigdy do góry nogami. Zaznaczona etykieta ma nad tekstem uchwyt obrotu: przeciągaj go co 15° (w tym 45° i 90°; z <kbd>Shift</kbd> dowolny kąt), a Ctrl/⌘-klik ustawia tekst znowu poziomo. Linia odniesienia nadal sięga elementu. Etykiety zwykłe i układu warstw obracają się tak samo, na rzutach, przekrojach i elewacjach, a arkusze, PDF i DXF drukują je tak, jak je widzisz.

**Co opisuje etykieta.** Gdy narzędzie **Etykieta** czeka na element, ten pod kursorem jest podświetlony kolorem akcentu; najedź na postawioną etykietę albo ją zaznacz, a element, który opisuje, zostanie podświetlony.

![Etykieta wyrównana do swojej ściany, z uchwytem obrotu, i opisywana ściana podświetlona](shots/tag-rotate.webp)

#### Etykieta układu warstw

Etykieta układu warstw pisze warstwy elementu jako notatkę architektoniczną: nagłówek (*EXTERIOR*), wiersz dla każdej warstwy, stopkę (*INTERIOR*). Nowy projekt ma **Build-up (exterior → interior)**: każdy wiersz to opis materiału (jego nazwa, gdy nie ma opisu), jego λ, jeśli je ma, i grubość warstwy w mm. Wybierz ją na pasku narzędzia **Etykieta** i kliknij ścianę, strop, dach lub wykończenie pomieszczenia; inne elementy dostają wybrane pole. Tekst jest czytany z modelu przy każdym rysowaniu: zmień warstwę, opis materiału lub jego λ, a etykieta podąży za zmianą.

![Dwie etykiety układu warstw na ścianie elewacyjnej: jeden odnośnik i odnośnik do każdej warstwy](shots/buildup-tag.webp)

Jej formę ustala się w **Zarządzaj → Standardy rysunkowe → Etykiety**: ustaw **Kształt** typu etykiety na *układ warstw* albo kliknij **Układ warstw…** przy jednym z nich, aby go edytować obok podglądu na jednym z typów ścian lub stropów projektu.

- **Nagłówek** i **Stopka**: pisane nad i pod warstwami (puste: brak; `|` zaczyna nowy wiersz).
- **Każda warstwa**: wiersz pisany raz dla każdej warstwy. Czyta pola warstwy, `{description}`, `{material}`, `{thickness}` (w jednostce wybranej w **Grubość w**), `{note}` (uwaga warstwy w jej typie), `{function}` (rdzeń, wykończenie, izolacja…) i `{index}`; dowolny parametr materiału po nazwie lub symbolu, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (zob. [Dane](materials.md#dane-opis-parametry-sposob-pomiaru) materiału); oraz pola elementu, `{name}`, `{type}`, `{mark}`. **Wstaw pole…** wstawia jedno w miejscu kursora i pokazuje parametry, które mają materiały projektu.
- **Nawiasy kwadratowe** czynią część opcjonalną: `[, λ = {λ} W/mK]` pisze się tylko tam, gdzie materiał ma λ; część znika w całości, gdy pole w niej jest puste. **[ ]** robi z zaznaczenia taką część. Aby napisać sam nawias, wpisz `\[` lub `\]`.
- **Kolejność**: od zewnątrz do wnętrza ściany (strona jej pierwszej warstwy jest jej zewnętrzem, więc odbita ściana nadal zaczyna się tam), od góry do dołu stropu lub dachu; albo odwrotnie. Wybierz kolejność, w jakiej warstwy leżą obok notatki, aby odnośniki do warstw się nie krzyżowały.
- **Warstwy bez grubości** (membrany, folie, farba) są pisane, chyba że odznaczysz **widoczne**. Warstwa zmieniająca materiał na wysokości pisze wiersz dla każdego materiału.
- **Odnośniki**: jeden, do elementu, albo **jeden na warstwę**, każdy od swojego wiersza do punktu wewnątrz swojej warstwy, tam gdzie widok rozróżnia warstwy (ściana w rzutach i przekrojach, strop w przekrojach; gdzie indziej etykieta zachowuje jeden). Przeciągnij koniec odnośnika wzdłuż elementu, aby przeciąć go w innym miejscu.
- **Wiersze zaczynają się od** punktora, myślnika, swojego numeru lub niczego; łamią się przy **Szerokość mm** na papierze, z wcięciem po punktorze, a wartość zostaje przy swoim symbolu i jednostce (`λ = 0.035 W/mK`).

![Edytor typu etykiety układu warstw obok jego podglądu](shots/buildup-tag-type.webp)

Jak każda etykieta, etykieta układu warstw podąża za swoim elementem, a arkusze i pliki PDF drukują ją tak, jak ją widzisz, łącznie z λ, μ i ρ. Własny **Tekst** w jej właściwościach zastępuje wiersz każdej warstwy tylko dla tej etykiety.

### Osie siatki

**Opisz → Oś siatki** (<kbd>GR</kbd>) rysuje jedną oś przez jej dwa końce; dostaje kolejną etykietę swojego kierunku. **Opisz → Siatka osi…** wstawia od razu całą siatkę: rozstawy **W poprzek** (osie numerowane, od lewej do prawej) i **Wzdłuż** (osie literowane, od dołu do góry) w jednostkach projektu, np. `6000 6000 4500` lub `3*6000`; **Początek**, gdzie przecinają się pierwsze osie, **Kąt**, jak daleko osie wychodzą **Poza siatkę**, **Pierwsze etykiety** i **Typ osi**. Gdy zaznaczysz **zastąp osie siatki**, dotychczasowe osie znikają.

![Okno Siatka osi](shots/grid-dialog.webp)

Kliknij oś, aby ją zaznaczyć. Jej właściwości pokazują **Początek** i **Koniec**, **Długość**, **Kąt** i rozstaw od poprzedniej równoległej osi (↔). Wpisz nowy rozstaw, a oś się przesunie; kolejne osie też się przesuną, chyba że odznaczysz **kolejne osie też się przesuwają**. **Kółka** wybiera, które końce mają kółko. **Style osi…** otwiera **Standardy rysunkowe → Osie siatki**, gdzie ustawia się kółko, jego rozmiar, rodzaj linii i pierwszą etykietę. Zaznacz całą siatkę, aby zobaczyć jej osie i rozstawy.

![Właściwości osi](shots/axis-properties.webp)

Wszystkie te długości podążają za jednostkami projektu. W stopach i calach końce i rozstawy mają postać 19'-8 1/4", a poziomy też są zapisywane w stopach i calach.

![Ta sama oś w stopach i calach](shots/axis-properties-imperial.webp)

![Karta Opisz](shots/ribbon-annotate.webp)
