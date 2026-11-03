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

Oś siatki przesuwa się, kopiuje, obraca i odbija lustrzanie sama albo razem z innymi elementami. Kopia jest nową osią tej samej siatki, nazwaną według ostatniej osi biegnącej w tym samym kierunku: przy osiach 1, 2 i 3 kopia każdej z nich to 4.

## Rysowanie

Karta **Rysunek** rysuje linie, polilinie, prostokąty, okręgi i łuki. Edytuje je poleceniami odsuń, utnij, wydłuż, zaokrąglenie, fazowanie i połącz, wypełnia obszary kreskowaniem i porządkuje je na warstwach (**Warstwy**). **Importuj DXF** wczytuje rysunek CAD jako linie, do których możesz przyciągać i które możesz edytować.

![Karta Rysuj](shots/ribbon-draft.webp)

## Opisywanie

Karta **Opisz** zawiera wymiary, teksty, osie siatki, poziomy i etykiety. Wymiar odczytuje model, więc aktualizuje się, gdy ściana się przesunie. Wygląd tekstów, wymiarów, osi i etykiet ustawisz w **Zarządzaj → Standardy rysunkowe**.

Wymiar wyrównany lub liniowy wstawiasz jako łańcuch: po pierwszych dwóch punktach i jego linii każde kliknięcie dodaje kolejny punkt na tej samej linii, a **Enter** (lub **Esc**) robi z całego łańcucha jeden wymiar (**U** cofa ostatni punkt; wpisz najpierw **S**, aby uzyskać pojedynczy wymiar). Łańcuch zachowuje się jak polilinia: kliknięcie zaznacza go całego, a przesuwa się, kopiuje i usuwa jako całość. Łańcuch mierzący ściany, krawędzie lub osie siatki zostaje na nich: przeciągnięty lub przesunięty sam, przesuwa tylko swoją linię wymiarową, prostopadle do łańcucha, więc omsknięcie myszy nigdy nie zostawi go mierzącego pustą przestrzeń; przesunięty razem z tym, co mierzy, idzie razem z tym. Jego uchwyty leżą na każdym mierzonym punkcie (przeciągnij, aby go przesunąć, Ctrl/⌘+kliknięcie go usuwa), na środku każdego segmentu (przeciągnij, aby dodać punkt, Ctrl/⌘+kliknięcie dodaje go w połowie) i na końcach linii wymiarowej (przeciągnij, aby przesunąć linię). Aby dodać więcej punktów do łańcucha, jak w ArchiCAD, zaznacz go i kliknij **Opisz → Dodaj punkty** (albo wpisz **DIMADD** lub użyj **Dodaj punkty** w jego właściwościach): każdy kliknięty punkt trafia do łańcucha tam, gdzie wypada na jego linii, między sąsiednimi punktami lub za końcem jako nowy koniec, i trzyma się ściany, krawędzi lub osi, na którą trafi; **Enter** lub **Esc** kończy. Aby usunąć punkty, kliknij z Ctrl/⌘ ich uchwyty albo kliknij **Opisz → Usuń punkty** (**DIMDEL** lub **Usuń punkty** we właściwościach) i kliknij blisko każdego punktu; segmenty po obu stronach łączą się w jeden. Wartość każdego segmentu też ma uchwyt: przeciągnij go, aby odsunąć wartość z jej miejsca tam, gdzie segmenty są krótkie, a wartości by się nakładały; gdy wartość stoi z dala od segmentu, łączy ją z nim cienka linia odniesienia. Ctrl/⌘+kliknięcie uchwytu albo **Teksty z powrotem na linii** we właściwościach przywraca ją na miejsce. Przesunięta wartość zostaje przy swoim segmencie, gdy punkty się przesuwają, i obraca się razem z łańcuchem. Kliknij dwukrotnie segment, aby wpisać jego wartość: mierzony element przesuwa się, a z nim wszystko, co jest za nim w łańcuchu. Właściwości pokazują wartość i tekst każdego segmentu, dzielą łańcuch na pojedyncze wymiary i łączą pojedyncze wymiary, które leżą jeden za drugim na jednej linii.

Opisy na rzucie (wymiary, teksty, narysowane linie, etykiety) mogą przejść na inny rzut w to samo miejsce, bo rzuty mają te same współrzędne: zaznacz je i wybierz rzut we właściwościach, w **Widok**, by je przenieść, lub w **Kopiuj do**, by je skopiować. Tak również wymiary zrobione na planie sytuacyjnym trafiają na rzut poziomu. Punkty wymiaru trzymają się wtedy tego, co nowy rzut przecina w tym miejscu: ścian, krawędzi, osi.

![Karta Opisz](shots/ribbon-annotate.webp)
