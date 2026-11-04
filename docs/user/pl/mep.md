---
title: Systemy instalacyjne
order: 57
summary: Rysuj na rzucie schematy instalacji grzewczych, wodnych, kanalizacyjnych, elektrycznych i wentylacyjnych, ze źródłami, odbiornikami i przewodami między nimi, i eksportuj je jako sieć IFC.
---

Karta **Instalacje** rysuje instalacje budynku tak, jak szkicuje je inżynier: system dla każdej instalacji, jego **źródło** (kocioł, wodomierz, pion, rozdzielnica, wentylator), jego **odbiorniki** (grzejniki, umywalki, WC, gniazda, oprawy oświetleniowe, nawiewniki) i **przewody** między nimi. Każdy węzeł jest prawdziwym elementem IFC z portami; każdy przewód jest odcinkiem rury, kanału lub kabla; połączenia trzymają się, gdy cokolwiek przesuwasz.

![Karta Instalacje](shots/ribbon-mep.webp)

## System jednym kliknięciem

W sekcji **Nowy system** wybierz instalację (**Ogrzewanie**, **Woda**, **Ciepła woda**, **Kanalizacja**, **Elektryka**, **Wentylacja**) i kliknij na rzucie. Lapin umieszcza cały system: źródło w miejscu kliknięcia, pierwszy odbiornik kilka metrów dalej i przewód między nimi. System otwiera się do edycji.

![System grzewczy na rzucie parteru: kocioł, dwa grzejniki pod oknami i przewody między nimi](shots/mep-plan.webp)

W trybie edycji wszystko, co nie należy do systemu, jest przygaszone i można wybierać tylko węzły i przewody systemu. Przeciągnij źródło i odbiornik tam, gdzie mają stać: ich przewód podąża za nimi. Dodawaj odbiorniki przyciskami **+ Grzejnik**, **+ Umywalka**… w panelu Właściwości albo narzędziami w grupach **Ogrzewanie**, **Instalacje wod.-kan.**, **Elektryka** i **Powietrze**: każdy nowy odbiornik dołącza do systemu własnym przewodem, od najbliższego wolnego portu źródła lub od najbliższego przewodu (trójnik). **Gotowe** (lub <kbd>Esc</kbd>) kończy tryb edycji i mówi, ile kolan i trójników ma system i co jest jeszcze otwarte. **Edytuj system** (<kbd>ES</kbd>) otwiera go ponownie.

## Przewody

**Odcinek** (<kbd>RN</kbd>) rysuje przewód punkt po punkcie; naciśnij <kbd>Enter</kbd>, aby zakończyć. Koniec, który trafi na otwarty port, łączy się z nim; koniec, który trafi na przewód tego samego systemu, tworzy od niego odgałęzienie.

**Połącz** łączy koniec przewodu z węzłem lub innym przewodem; **Rozłącz** go uwalnia. **Wolne przyłącza** zaznacza wszystko, co jest jeszcze niepołączone.

## Zmiana przebiegu przewodu

Zaznacz przewód: jego uchwyty leżą na linii tak, jak jest narysowana, nawet po przesunięciu węzłów, które łączy.

- **Przeciągnij uchwyt**, aby swobodnie przesunąć ten narożnik.
- **Przeciągnij środek odcinka**, aby przesunąć odcinek w bok; sąsiednie odcinki się wydłużają, więc prostokątna trasa pozostaje prostokątna. Przesuń pierwszy lub ostatni odcinek, a Lapin doda tam uskok, aby koniec pozostał na swoim porcie.
- **Kliknij z Ctrl** uchwyt, aby go usunąć, lub środek odcinka, aby dodać nowy; **Dodaj uchwyt** wstawia go w miejscu kliknięcia.
- **Obejście** (<kbd>DU</kbd>) prowadzi przewód dookoła przeszkody: kliknij przewód w miejscu, gdzie zaczyna się obejście, kliknij, gdzie kończy się na tym samym odcinku, a następnie przesuń kursor na stronę, w którą ma iść, i kliknij (lub wpisz odległość). Przewód wychodzi, biegnie wzdłuż i wraca, z czterema nowymi narożnikami; zachowuje swoje połączenia.
- **Narysuj ścieżkę ponownie** (<kbd>RP</kbd>) nadaje przewodowi nową trasę: zaznacz go (lub zacznij na nim) i narysuj trasę punkt po punkcie, <kbd>Enter</kbd>, aby zakończyć. Przewód zachowuje swój system, rozmiar i połączenia, a jego końce wracają na swoje porty.

![Przewód grzejnika poprowadzony dookoła przeszkody poleceniem Obejście: na zewnątrz, wzdłuż i z powrotem](shots/mep-detour.webp)


## Węzły

Każdy węzeł ma **rolę**: kocioł, grzejnik, rozdzielacz, wodomierz, umywalka, zlew, WC, prysznic, wanna, pion, rewizja, rozdzielnica, gniazdo, oprawa oświetleniowa, łącznik, wentylator, nawiewnik, kratka wywiewna, zawór, przejście w górę/w dół, zaślepka. Rola nadaje mu klasę IFC, porty, rozmiar i symbol na rzucie. Zawór, licznik lub łącznik upuszczony na przewód dzieli go i staje pośrodku. **Przejście w górę/w dół** prowadzi przewód na inną wysokość lub na kondygnację wyżej.

![Właściwości grzejnika: jego system, rola i porty, rozmiar i moc](shots/mep-radiator-properties.webp)

W panelu Właściwości węzeł pokazuje swój system, rolę i porty (otwarte lub połączone); przewód pokazuje swoje dwa końce. Oba przyjmują wartości domyślne systemu, chyba że nadasz im własną wartość: rozmiar, wysokość, płaszczyznę, w której biegną (podłoga, sufit, ściana), parę lub pojedynczo, materiał, spadek.

## Systemy

**Systemy…** wyświetla systemy projektu i wartości domyślne bieżącego: rozmiar, płaszczyznę i wysokości każdej płaszczyzny, spadek (kanalizacja), to, czy przewody są rysowane parami (ogrzewanie: zasilanie i powrót) i w jakim odstępie, materiał i izolację. Zmień wartość domyślną, a każdy węzeł i przewód bez własnej wartości podąży za nią.

![Okno dialogowe Systemy](shots/mep-systems.webp)

## Rzuty, wykazy i IFC

**Rzut instalacji** tworzy dla poziomu rzut branży instalacyjnej, umieszczony w przeglądarce w zbiorze Instalacje. **Lista urządzeń** i **Lista odcinków** w grupie **System** są zestawieniami jak każde inne: trafiają na arkusze i eksportują się razem z nimi.

W eksporcie IFC każdy system jest IfcDistributionSystem, każdy węzeł i przewód ma swoje IfcDistributionPorts, a połączenia są zapisywane między portami, więc sieć trafia do innego programu połączona od źródła do ostatniego odbiornika.

## Gaz i olej opałowy

**Gaz** i **Olej opałowy** to osobne media w **Nowy system**. System gazowy podaje w **Systemy…** swój gaz (**Gaz ziemny** lub **LPG**) i **Klasę ciśnienia** (niskie w domu, średnie w ulicy); jego rury to domyślnie stal czarna na ścianach na wysokości 2,1 m, a mogą być miedziane, wielowarstwowe lub z **PE 100** dla tego, co w ziemi. Olej opałowy płynie miedzią.

Instalacja gazowa domu zaczyna się w **szafce gazowej** na elewacji. Kliknij **Nowy system → Gaz**: szafka staje tam, gdzie klikniesz, a **kocioł gazowy** w środku, połączone przewodem. Po drodze narzędzia z **Gaz** kładą na przewodzie **reduktor gazu**, **gazomierz**, **główny zawór gazu**, **zawory przy urządzeniach** i **przyłącza elastyczne**, które dzielą go jak każda kształtka; **kuchenka gazowa** i **podgrzewacz gazowy** łączą się z instalacją własnym przewodem. Gdzie rura gazowa lub olejowa przechodzi przez ścianę, idzie w **tulei ochronnej**, rysowanej i liczonej z przewodem. Kocioł gazowy narysowany w systemie grzewczym jako jego źródło łączy się z instalacją gazową z jego właściwości (przycisk **+** z nazwą systemu gazowego).

![Instalacja gazowa domu: szafka, reduktor i gazomierz na zewnątrz, kocioł i kuchenka w kuchni](shots/mep-gas-plan.webp)

Każde urządzenie zachowuje swoje **zużycie** (m³/h) i **typ urządzenia** według odprowadzenia spalin (A bez odprowadzenia, B z otwartą komorą, C z zamkniętą komorą), dla wykazów i wymiarowania.

**Olej opałowy** stawia **zbiornik oleju opałowego** (wewnątrz, na zewnątrz lub podziemny) i **kocioł olejowy**; **króciec napełniania** i **kołpak odpowietrzający** łączą się z napełnianiem i odpowietrzeniem zbiornika, a **filtr oleju** siedzi na zasilaniu.

### Zasilanie w ulicy

W **Ulica** narzędzie **Gazociąg wzdłuż…** (<kbd>GM</kbd>) układa gazociąg wzdłuż tego, co klikniesz — drogi, osi trasy, narysowanej linii, ściany — w **odległości od linii** i na **głębokości (przykryciu)** z paska linii poleceń; pod terenem głębokość liczy się od gruntu, inaczej od kondygnacji. **Gazociąg** rysuje go punkt po punkcie. Gazociąg trafia do systemu gazowego średniego ciśnienia z rur PE, z **taśmą ostrzegawczą**.

**Przyłącze** (<kbd>GS</kbd>): kliknij gazociąg, potem szafkę. Lapin stawia **trójnik siodłowy** na gazociągu, układa rurę przyłącza prostopadle od niego na jej przykryciu aż do szafki i tam wprowadza ją w górę do szafki.

![Przyłącze od gazociągu w ulicy do szafki gazowej, zaznaczone](shots/mep-gas-street.webp)

![Gazociąg i przyłącze pod ziemią, instalacja na elewacji](shots/mep-gas-3d.webp)

![Właściwości przyłącza: od trójnika siodłowego do szafki, przykrycie i średnica](shots/mep-gas-service-properties.webp)

![Wartości domyślne systemu gazowego: jego gaz i klasa ciśnienia](shots/mep-gas-systems.webp)

**Zestawienie materiałów gazu i oleju** w **System** wymienia, system po systemie, rury według materiału i średnicy z ich długością (a z niej piony), kolana, trójniki i tuleje, taśmę ostrzegawczą, zawory, gazomierze i szafki oraz urządzenia z zainstalowanym zużyciem i mocą. W IFC system gazowy to IfcDistributionSystem typu GAS, a olejowy FUEL; szafka to IfcDistributionChamberElement, gazomierz IfcFlowMeter GASMETER, zawory IfcValve, kuchenka IfcBurner, zbiornik IfcTank.

![Zestawienie materiałów gazu i oleju](shots/mep-gas-list.webp)

Dobór średnic rur ze zużycia urządzeń i sprawdzenie spadku ciśnienia to inżynieria, w edycji **Lapin MEP**.

## Koordynacja: bryły otworów i sprawdzanie kolizji

Każdy otwór w ścianie, stropie lub dachu może być osobną **bryłą otworu**: bryłą tak dużą jak otwór, przez całą grubość elementu nośnego, z wykończeniami. Programy wykrywania kolizji (Navisworks, Solibri, BIMcollab) sprawdzają instalacje względem takich brył regułą „całkowicie wewnątrz”: rura, kanał lub koryto kablowe w całości w bryle jest w porządku; element wystający z niej albo przechodzący przez ścianę bez otworu to kolizja. Lapin wykonuje to samo sprawdzenie.

**Zarządzaj → Otwory a instalacje…** ustala, dla jakich otworów powstają bryły (**Otwory w ścianach**, **Otwory drzwiowe**, **Otwory okienne**, **Otwory w stropach**, **Otwory w dachu**), o ile są większe lub mniejsze od otworów (**Odsunięcie otworu**) i jaki **Luz** element instalacji musi zachować od boków otworu. **Uruchom sprawdzenie** przechodzi przez każdą rurę, kanał i koryto — trasy Lapina, segmenty zaimportowanego modelu instalacji i jego własne zapotrzebowania na otwory — i wypisuje każde miejsce, gdzie któryś przechodzi przez ścianę, strop lub dach:

- **Brak otworu**: przechodzi przez element tam, gdzie otworu nie ma;
- **Za mały otwór**: przechodzi przez otwór, ale z niego wystaje, z luzem; **Brakuje** podaje milimetry po najgorszej stronie;
- **Mieści się** (z **pokaż to, co się mieści**): w całości w bryle, z luzem;
- **Pusty otwór** (z zaznaczonym **Puste otwory**): otwór w ścianie lub stropie, przez który nic nie przechodzi (otwór schodowy nigdy).

Kliknięcie wiersza pokazuje parę w 3D, obie zaznaczone. **Utwórz uwagi** zmienia każdą kolizję w uwagę typu **Clash** z jej punktem widzenia (**Zarządzaj → Uwagi do modelu…**), gotową do BCF; ponownie uruchomione aktualizuje te same uwagi. Kolizje pojawiają się też wśród ostrzeżeń **Zarządzaj → Sprawdź**.

![Otwory a instalacje: dla czego powstają bryły, luz i sprawdzenie trzech rur](shots/mep-voids-check.webp)

Karta **Bryły** wypisuje każdą bryłę z jej znakiem (V1, V2… kondygnacja po kondygnacji), elementem nośnym, drzwiami lub oknem w niej, rozmiarem i głębokością; kliknięcie zaznacza jej otwór.

![Bryły ze znakami i rozmiarami](shots/mep-voids-list.webp)

**Widok → Bryły otworów**, z widokiem 3D z przodu, rysuje je w 3D, półprzezroczyste fioletowe ze znakami; kliknięcie zaznacza otwór.

![Bryły w ścianie wewnętrznej, z rurami przez nie](shots/mep-voids-3d.webp)

Z rzutem, przekrojem lub elewacją z przodu **Widok → Bryły otworów** rysuje je w tym widoku, jak rysunek koordynacyjny pokazuje przewidziane otwory: przecięte tam, gdzie widok je tnie (otwór w ścianie na całej jej grubości, w stropie przez płytę), widziane tam, gdzie je widzi (otwór w stropie poniżej cięcia rzutu, otwór w ścianie na licu, na które patrzy elewacja), przerywane, gdy otwór w ścianie leży pod cięciem, a w stropie nad nim; każdy przekreślony po przekątnych, z oznaczeniem, a pod nim wymiar i rzędna dołu otworu w ścianie (**400×200 +0.90**, **Ø200**). Rysuje je silnik razem z resztą rysunku, więc arkusze, PDF i DXF (warstwa **VOIDS**) drukują je tak, jak pokazuje ekran.

![Rzut z bryłami otworów: przecięte w ścianach, otwór w stropie widziany, każdy przekreślony i oznaczony](shots/mep-voids-plan.webp)

To ustawienie widoku, także w jego właściwościach (**Widok → Ustawienia widoku**): **Bryły otworów** oraz **Wymiary otworów** dla wymiaru pod oznaczeniem. Domyślnie żaden widok ich nie rysuje; styl widoku **Koordynacja** (**Zarządzaj → Style widoku**, wybrany jako **Styl** widoku) rysuje je w każdym widoku, który go przyjmie. Ich linie to kategoria **Bryły**: pióra i kolory w **Zarządzaj → Style obiektów** (linie cięcia, rzutu, ukryte, nad cięciem), pokazywane lub ukrywane w widoku w **Widok → Widoczność / Grafika…**.

Aby przekazać bryły do programu kolizji, zaznacz **Bryły otworów** w **Plik → Eksportuj IFC lub Fragments…** (lub **Eksportuj 3D…**). W IFC każda bryła to **IfcBuildingElementProxy** typu **PROVISIONFORVOID** z **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter dla okrągłej, Depth) i zestawem Lapina **Lapin_OpeningVoid** (znak, rodzaj, element nośny, otwór, odsunięcie), powiązana z elementem nośnym; same otwory pozostają IfcOpeningElement. Każda bryła zachowuje GlobalId między eksportami. W eksporcie siatek tworzą osobną grupę **Opening voids**. Bez zaznaczenia eksporty są jak dotąd.

## MEP Pro

Obliczenia inżynierskie sieci — zapotrzebowanie przenoszone przez każdy przewód i dobrany do niego rozmiar, sprawdzenia (otwarte końce, ciasne kolana, spadki kanalizacji, przeciążone źródła), zasady prowadzenia według materiału (miedź w krokach co 45°, PVC skręcające najwyżej o 45°), grzejniki proponowane pod oknami, trasy sugerowane wzdłuż ścian, profil przewodu i aksonometria systemu — znajdują się w edycji **Lapin MEP**, która dodaje do tej karty grupy **Inżynieria** i **Asystenci**.
