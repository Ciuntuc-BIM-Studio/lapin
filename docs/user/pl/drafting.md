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

Każda długość, o którą pyta wiersz poleceń, jest w jednostce projektu, a monit mówi, w jakiej: `Depth (mm)`, `Height (ft-in)`. Kąty, współczynniki i liczności to zwykłe liczby: `90` po **Obróć** to 90°, bez względu na jednostkę.

### Rysowanie w widoku 3D

**Linia**, **Polilinia**, **Prostokąt**, **Okrąg**, **Łuk** i edycje z Rysunku działają też w widoku 3D, z wierszem poleceń na dole. Gdy polecenie trwa, etykieta u góry mówi, na czym rysujesz: na poziomie aktywnej kondygnacji lub na zaznaczonej płaszczyźnie odniesienia. **Wybierz ścianę bryły** w etykiecie, a potem kliknięcie ściany modelu, tworzy na niej płaszczyznę odniesienia i rysuje na niej. Punkty przyciągają się jak na rzucie: do linii przekroju rzutu (narożników i lic ścian) i do tego, co narysowano na tej samej płaszczyźnie, z orto i liniami śledzenia. To, co rysujesz, jest tym samym elementem rysunku co narysowany na rzucie i widać go też tam.

![Rysowanie w widoku 3D: etykieta mówi płaszczyznę, linia idzie za myszą na poziomie kondygnacji](shots/draft-3d.webp)

### Rysunki w widoku 3D

To, co narysujesz na rzucie (linie, polilinie, prostokąty, okręgi, łuki), stoi też w widoku 3D jako linie na poziomie kondygnacji (na samym poziomie, nie na wysokości cięcia); to, co narysujesz na płaszczyźnie odniesienia, przekroju lub elewacji, stoi na tej płaszczyźnie. Kliknij jeden w 3D, aby go zaznaczyć, a potem przesuń, skopiuj, odsuń lub usuń. **Widok → Rysunki w 3D**, także w **Widok → Ustawienia widoku** widoku 3D, pokazuje lub ukrywa je wszystkie; jest włączone, dopóki go nie wyłączysz. Wymiary, teksty, etykiety, kreskowania i linie pomocnicze (rola draft) zostają w swoich widokach, a DXF wczytany na rzut pozostaje rysunkiem 2D, poza liniami z wysokościami.

## Opisywanie

Karta **Opisz** zawiera wymiary, teksty, osie siatki, poziomy i etykiety. Wymiar odczytuje model, więc aktualizuje się, gdy ściana się przesunie. Wygląd tekstów, wymiarów, osi i etykiet ustawisz w **Zarządzaj → Standardy rysunkowe**.

Wymiar wyrównany lub liniowy wstawiasz jako łańcuch: po pierwszych dwóch punktach i jego linii każde kliknięcie dodaje kolejny punkt na tej samej linii, a **Enter** (lub **Esc**) robi z całego łańcucha jeden wymiar (**U** cofa ostatni punkt; wpisz najpierw **S**, aby uzyskać pojedynczy wymiar). Łańcuch zachowuje się jak polilinia: kliknięcie zaznacza go całego, a przesuwa się, kopiuje i usuwa jako całość. Łańcuch mierzący ściany, krawędzie lub osie siatki zostaje na nich: przeciągnięty lub przesunięty sam, przesuwa tylko swoją linię wymiarową, prostopadle do łańcucha, więc omsknięcie myszy nigdy nie zostawi go mierzącego pustą przestrzeń; przesunięty razem z tym, co mierzy, idzie razem z tym. Jego uchwyty leżą na każdym mierzonym punkcie (przeciągnij, aby go przesunąć, Ctrl/⌘+kliknięcie go usuwa), na środku każdego segmentu (przeciągnij, aby dodać punkt, Ctrl/⌘+kliknięcie dodaje go w połowie) i na końcach linii wymiarowej (przeciągnij, aby przesunąć linię). Aby dodać więcej punktów do łańcucha, jak w ArchiCAD, zaznacz go i kliknij **Opisz → Dodaj punkty** (albo wpisz **DIMADD** lub użyj **Dodaj punkty** w jego właściwościach): każdy kliknięty punkt trafia do łańcucha tam, gdzie wypada na jego linii, między sąsiednimi punktami lub za końcem jako nowy koniec, i trzyma się ściany, krawędzi lub osi, na którą trafi; **Enter** lub **Esc** kończy. Aby usunąć punkty, kliknij z Ctrl/⌘ ich uchwyty albo kliknij **Opisz → Usuń punkty** (**DIMDEL** lub **Usuń punkty** we właściwościach) i kliknij blisko każdego punktu; segmenty po obu stronach łączą się w jeden. Wartość każdego segmentu też ma uchwyt: przeciągnij go, aby odsunąć wartość z jej miejsca tam, gdzie segmenty są krótkie, a wartości by się nakładały; gdy wartość stoi z dala od segmentu, łączy ją z nim cienka linia odniesienia. Ctrl/⌘+kliknięcie uchwytu albo **Teksty z powrotem na linii** we właściwościach przywraca ją na miejsce. Przesunięta wartość zostaje przy swoim segmencie, gdy punkty się przesuwają, i obraca się razem z łańcuchem. Kliknij dwukrotnie segment, aby wpisać jego wartość: mierzony element przesuwa się, a z nim wszystko, co jest za nim w łańcuchu. Właściwości pokazują wartość i tekst każdego segmentu, dzielą łańcuch na pojedyncze wymiary i łączą pojedyncze wymiary, które leżą jeden za drugim na jednej linii.

Opisy na rzucie (wymiary, teksty, narysowane linie, etykiety) mogą przejść na inny rzut w to samo miejsce, bo rzuty mają te same współrzędne: zaznacz je i wybierz rzut we właściwościach, w **Widok**, by je przenieść, lub w **Kopiuj do**, by je skopiować. Tak również wymiary zrobione na planie sytuacyjnym trafiają na rzut poziomu. Punkty wymiaru trzymają się wtedy tego, co nowy rzut przecina w tym miejscu: ścian, krawędzi, osi.

### Etykiety

**Opisz → Etykieta** (<kbd>TG</kbd>) umieszcza na widoku ramkę z tym, co element mówi o sobie, i linię odniesienia do niego. Gdy narzędzie jest włączone, pasek pod widokiem wybiera, co pokazuje etykieta: znak, nazwę, klasę lub typ, **Odporność ogniowa**, **Izolacyjność akustyczna**, **Współczynnik przenikania ciepła** albo **Inny parametr…** po nazwie; albo typ etykiety ze standardów rysunkowych; a **ODNOŚNIK** włącza lub wyłącza linię odniesienia. Kliknij element, potem miejsce etykiety (najpierw wpisz **E**, aby załamać linię). **Opisz → Oznacz parametr** pyta o nazwę parametru i uruchamia z nim narzędzie.

![Pasek narzędzia Etykieta pod widokiem](shots/tag-options.webp)

Etykieta odczytuje dowolny parametr: elementu, jego typu (wtedy pokazują go wszystkie elementy typu), właściwość standardową, np. `Pset_WallCommon.FireRating`, albo taką, z którą przyszedł element zaimportowany. Nazwa jest znajdowana także bez spacji, więc **Fire rating** i `FireRating` to to samo. Aby ją wypełnić, użyj **Parametry** we właściwościach elementu lub typu: wpisz nazwę (proponowane są Fire rating, Sound rating, Moisture resistance i Thermal resistance) albo wybierz **Dodaj właściwość standardową…** dla właściwości IFC jego klasy. Etykieta podąża, gdy tylko jest wartość, a długości są zapisywane w jednostkach projektu.

Zaznaczona etykieta ma uchwyty. Przeciągnij ramkę, aby przesunąć etykietę: linia odniesienia wskazuje to samo miejsce. Przeciągnij środek linii, aby ją załamać (Ctrl/⌘-klik na załamaniu prostuje ją), a jej koniec, aby wskazywała inny punkt (Ctrl/⌘-klik: z powrotem na środek elementu). We właściwościach zmieniasz, co etykieta **Pokazuje**, dajesz jej własny **Tekst** (`{mark}|{FireRating}` pisze dwa wiersze), **Typ etykiety** albo wyłączasz **Linię odniesienia**. Etykiety rysuje silnik, więc arkusze i PDF drukują je tak, jak je widzisz. Typy etykiet w **Zarządzaj → Standardy rysunkowe → Etykiety** odczytują te same pola w swoich szablonach; nowy projekt ma wśród nich Odporność ogniową, Izolacyjność akustyczną i Współczynnik przenikania ciepła.

![Załamana linia odniesienia etykiety z jej uchwytami](shots/tag-leader.webp)

![Właściwości etykiety](shots/tag-properties.webp)

Znacznik drzwi lub okna edytuje się część po części: każdy tekst przesunięty lub obrócony, każdy koniec jego linii wydłużony; zob. [Drzwi i okna](doors-windows.md).

### Osie siatki

**Opisz → Oś siatki** (<kbd>GR</kbd>) rysuje jedną oś przez jej dwa końce; dostaje kolejną etykietę swojego kierunku. **Opisz → Siatka osi…** wstawia od razu całą siatkę: rozstawy **W poprzek** (osie numerowane, od lewej do prawej) i **Wzdłuż** (osie literowane, od dołu do góry) w jednostkach projektu, np. `6000 6000 4500` lub `3*6000`; **Początek**, gdzie przecinają się pierwsze osie, **Kąt**, jak daleko osie wychodzą **Poza siatkę**, **Pierwsze etykiety** i **Typ osi**. Gdy zaznaczysz **zastąp osie siatki**, dotychczasowe osie znikają.

![Okno Siatka osi](shots/grid-dialog.webp)

Kliknij oś, aby ją zaznaczyć. Jej właściwości pokazują **Początek** i **Koniec**, **Długość**, **Kąt** i rozstaw od poprzedniej równoległej osi (↔). Wpisz nowy rozstaw, a oś się przesunie; kolejne osie też się przesuną, chyba że odznaczysz **kolejne osie też się przesuwają**. **Kółka** wybiera, które końce mają kółko. **Style osi…** otwiera **Standardy rysunkowe → Osie siatki**, gdzie ustawia się kółko, jego rozmiar, rodzaj linii i pierwszą etykietę. Zaznacz całą siatkę, aby zobaczyć jej osie i rozstawy.

![Właściwości osi](shots/axis-properties.webp)

Wszystkie te długości podążają za jednostkami projektu. W stopach i calach końce i rozstawy mają postać 19'-8 1/4", a poziomy też są zapisywane w stopach i calach.

![Ta sama oś w stopach i calach](shots/axis-properties-imperial.webp)

![Karta Opisz](shots/ribbon-annotate.webp)
