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

## Wentylatory wyciągowe

Grupa **Wentylatory** na karcie Instalacje wstawia wentylatory wyciągowe łazienek i kuchni jako osobne elementy, każdy według typu, który konfigurujesz:

- **Wentylator ścienny** (<kbd>VF</kbd>): wentylator osiowy na ścianie, wydmuchujący prosto przez nią;
- **Wentylator sufitowy**: wentylator pod sufitem z kanałem wychodzącym z boku;
- **Wentylator kanałowy**: wentylator rurowy w przestrzeni nad sufitem, na kanale;
- **Wentylator okienny**: wentylator osadzony w szybie okna;
- **Wentylator sufitowy z oświetleniem**: wentylator sufitowy z lampą w kratce;
- **Wentylator z panelem żaluzjowym**: panel ścienny z żaluzją obok wentylatora i osłoną pod nim.

Wybierz narzędzie i kliknij. Wentylator ścienny, okienny lub z panelem żaluzjowym trafia na lico ściany najbliższej kliknięciu (do 600 mm), przodem do pomieszczenia, środkiem 2,1 m nad podłogą (okienny 1,9 m). Wentylator sufitowy trafia tam, gdzie klikniesz, pod sufit pomieszczenia; kanałowy 250 mm nad nim. Pasek wiersza poleceń wybiera typ, który wstawia narzędzie, spośród typów projektu lub wbudowanych typów Lapin (wentylator ścienny Ø100 z czujnikiem wilgotności, sufitowy z oświetleniem i czujnikiem ruchu, kanałowy na dwie łazienki…).

Wentylator ścienny wycina w swojej ścianie okrągły otwór, o 20 mm szerszy od swojego kanału: przepust ściany, jak przewód instalacji (na rzutach widać go jako bryłę otworu, a w IFC trafia jako otwór ściany). Jest wyznaczany z położenia wentylatora, więc wentylator przesunięty wzdłuż ściany zabiera otwór ze sobą. Wentylator okienny siedzi w szybie i niczego nie wycina.

![Łazienka z wentylatorem ściennym, wentylatorem sufitowym z oświetleniem wyprowadzonym przez ścianę i wentylatorem z panelem żaluzjowym](shots/fans-plan.webp)

Rzuty rysują wentylator jego symbolem instalacyjnym: okrąg z łopatkami, przód na ścianie, tuleję przerywaną przez ścianę i strzałkę w kierunku przepływu powietrza; kratkę wentylatora sufitowego przerywaną, bo jest nad przekrojem. Rzuty sufitu, przekroje, elewacje i 3D rysują bryły.

**Kanał do wylotu** wyprowadza powietrze z wentylatora sufitowego lub kanałowego: kliknij wentylator, a potem miejsce, gdzie kanał wychodzi. Kanał jest przewodem systemu projektu **Wyciąg łazienkowy** (tworzonego z pierwszym kanałem; przecina ściany, przez które przechodzi), tak szerokim jak kanał wentylatora, na wysokości jego króćca. Kończy się kratką na zewnętrznym licu ściany albo, gdy na pasku włączono **Przez dach**, rurą wznoszącą się do wywiewki. Przesunięty lub podniesiony wentylator zabiera ze sobą początek kanału.

![Wentylatory w 3D: kanał do kratki na zewnątrz, kratka wentylatora ściennego na elewacji](shots/fans-3d.webp)

### Typy wentylatorów

**Typy wentylatorów** otwiera typy: projektu oraz wbudowane, dodawane jednym kliknięciem. Typ określa:

- swój **montaż** (taki, jaki dopuszcza jego rodzaj) i **średnicę kanału** (Ø100, 125, 150 lub 200 mm);
- **kształt** kratki (okrągła lub kwadratowa), **rozmiar**, **wzór** (żaluzje, szczeliny, pierścienie lub gładka) i **wykończenie** (biały, srebrny, czarny, stal nierdzewna lub chrom);
- **wydajność** (m³/h) i **moc** (W);
- **sterowanie**: wyłącznik, czujnik wilgotności, wyłącznik czasowy, oba lub czujnik ruchu;
- **klapę zwrotną**, a także **oświetlenie** wentylatora sufitowego, długość kanałowego, szerokość i wysokość panelu żaluzjowego, widoczne tylko tam, gdzie mają zastosowanie.

Podgląd obok pochodzi z silnika: symbol na rzucie u góry, widok z przodu u dołu. Każdy wentylator danego typu podąża za zmianą.

![Typy wentylatorów z podglądem panelu żaluzjowego](shots/fans-types.webp)

Zaznacz wentylator, aby zmienić jego typ, **wysokość nad podłogą**, obrót oraz, gdy ma kanał, jego **wylot** (ściana lub dach) i to, jak wysoko wylot wznosi się nad kanałem. Przeciągnij uchwyt pośrodku, aby go przesunąć, a uchwyt przed nim, aby go obrócić.

![Właściwości wentylatora sufitowego](shots/fans-properties.webp)

### Zestawienia, koszty i IFC

**Zestawienie wentylatorów** wymienia każdy wentylator z pomieszczeniem, rodzajem, rozmiarem, wydajnością, montażem, sterowaniem, wysokością i wylotem oraz łączną wydajność. **Kontrola wyciągu w pomieszczeniach** wymienia każdą łazienkę, WC i kuchnię (i każde pomieszczenie z wentylatorem) z kubaturą, potrzebnym wyciągiem i tym, co dają jego wentylatory: łazienka 8 wymian powietrza na godzinę i co najmniej 54 m³/h, WC 6 i 25 m³/h, kuchnia 6 i 108 m³/h. To wartości orientacyjne; sprawdź je z normą, według której projektujesz.

Nowy typ ma pozycję kosztową `LAP-FAN`, liczoną na sztukę (**Koszty → Przypisz pozycje…** ją zmienia).

IFC zapisuje wentylator jako **IfcFan** typu **IfcFanType** z jego typem predefiniowanym (PROPELLORAXIAL dla ściennego lub okiennego, CENTRIFUGALRADIAL dla sufitowego, TUBEAXIAL dla kanałowego) i **Pset_FanTypeCommon** (NominalAirFlowRate, NominalPowerRate); otwór w ścianie jako IfcOpeningElement. Lapin wczytuje je z powrotem jako własne wentylatory.

## Koordynacja: bryły otworów i sprawdzanie kolizji

Każdy otwór w ścianie, stropie lub dachu może być osobną **bryłą otworu**: bryłą tak dużą jak otwór, przez całą grubość elementu nośnego, z wykończeniami. Programy wykrywania kolizji (Navisworks, Solibri, BIMcollab) sprawdzają instalacje względem takich brył regułą „całkowicie wewnątrz”: rura, kanał lub koryto kablowe w całości w bryle jest w porządku; element wystający z niej albo przechodzący przez ścianę bez otworu to kolizja. Lapin wykonuje to samo sprawdzenie.

**Zarządzaj → Wykrywanie kolizji…** uruchamia sprawdzenie tylko na żądanie: nic nie uruchamia go samo, ani edycja, ani **Zarządzaj → Sprawdź**. Jego karta **Ustawienia** ustala, dla jakich otworów powstają bryły (otwory w ścianach, drzwi, okna, stropy i dachy), o ile są większe lub mniejsze od swoich otworów (**Odsunięcie bryły**) oraz **Luz**, jaki element instalacji musi zachować od boków bryły. **Uruchom wykrywanie kolizji** przechodzi raz przez każdą rurę, kanał i korytko kablowe — ciągi Lapin i segmenty zaimportowanego modelu instalacji tak samo, a także przepusty własne modelu instalacji — i zachowuje to, co znalazło, jako **raport kolizji** projektu z datą uruchomienia, dopóki nie zostanie uruchomione ponownie. Gdy model zmieni się potem, raport mówi, że jest **nieaktualny**. Jego problemy są pogrupowane według rodzaju i ponumerowane, a każdy numer zostaje ten sam od uruchomienia do uruchomienia:

- **Brak otworu**: przechodzi przez element nośny tam, gdzie nie ma otworu;
- **Nieprzecięte: nośne lub kolejność cięcia**: brak otworu, bo własne otwory ciągów (niżej) nie przecinają tego elementu;
- **Otwór za mały**: przechodzi przez otwór, ale z niego wystaje, z luzem; **Brakuje** mówi, o ile milimetrów po najgorszej stronie;
- **Pusta bryła** (z zaznaczonymi **Pustymi otworami** w **Ustawieniach**): otwór w ścianie lub stropie, przez który nic nie przechodzi (otwór schodowy nigdy nim nie jest).

![Raport kolizji: brak otworu, otwór za mały i pusta bryła, każde ponumerowane](shots/mep-voids-check.webp)

Każdy problem jest **Do poprawy**, **Rozwiązana** lub **Zignorowana**, z komentarzem. Kliknięcie wiersza pokazuje parę w 3D, oba zaznaczone (**same w 3D** ukrywa na razie resztę); strzałki przechodzą do poprzedniej i następnej. **Zaznacz w widoku** (także **Widok → Znaczniki kolizji**) rysuje każdy otwarty problem w rzucie, przekroju lub elewacji na wierzchu jako czerwoną chmurkę z jego numerem, tylko na ekranie.

![Rzut z zaznaczonymi kolizjami: czerwone chmurki z ich numerami](shots/mep-voids-clouds.webp)

**Wykonaj otwór** wycina prosty otwór wokół elementu w ścianie (okrągły dla rury lub kabla, prostokątny dla kanału) lub w stropie, z bokami w odległości luzu i wymiarem zaokrąglonym w górę według reguły instalacji z kolejności cięcia; **Powiększ otwór** powiększa za mały otwór. **Popraw zaznaczone** robi to dla każdego zaznaczonego wiersza, **Ignoruj** i **Otwórz ponownie** zmieniają ich stan; każde to jeden krok do cofnięcia. Raport mówi wtedy, że jest nieaktualny: uruchom go ponownie, a poprawione pojawi się jako **Rozwiązana** (**pokaż rozwiązane**). Jej wiersz mówi wtedy, przy którym uruchomieniu uznano ją za rozwiązaną i jak jest teraz: otwór, przez który przechodzi, i rozmiar ciągu w nim (**Resolved at the run of 04.11.2026 15:20: opening V1 200 mm, run 160 mm.**), albo że ciąg już przez niego nie przechodzi; każde kolejne uruchomienie aktualizuje rozmiary.

![Uruchomione ponownie po poprawkach: obie kolizje rozwiązane](shots/mep-voids-fixed.webp)

**BCF** zamienia otwarte kolizje w uwagi typu **Clash** z ich punktami widzenia (**Zarządzaj → Uwagi do modelu…**) i zapisuje je jako plik BCF; uruchomione ponownie aktualizuje te same uwagi. **CSV** i **PDF** zapisują raport jako listę. **Zarządzaj → Sprawdź** tylko czyta raport: jego otwarte kolizje są ostrzeżeniami, tak jak nieaktualny raport.

### Ciągi, które same wycinają swoje otwory

Kolejność cięcia (**Zarządzaj → Kolejność cięcia**) obejmuje też ciągi instalacyjne, ponad ścianami, stropami i dachami. Z włączonym **Ciągi przecinają ściany, stropy i dachy** ciąg, który przechodzi przez ścianę, strop lub dach, wycina tam własny otwór: okrągły dla rury lub kabla przechodzącego prostopadle, prostokątny dla kanału lub ciągu ukośnego, tak duży jak zewnętrzna strona ciągu (jego izolacja, rura osłonowa przewodu gazowego) plus **Luz** dookoła, zaokrąglony w górę do kroku, nigdy mniejszy niż **Najmniejszy otwór**. Otwór nie jest przechowywany osobno: przesuwa się i rośnie razem z ciągiem i znika, gdy ciąg znika lub już nie przechodzi. Jest bryłą jak każdy otwór (rysowaną, listowaną, sprawdzaną), a eksport IFC zapisuje go jako **IfcOpeningElement** z **Lapin_ProvisionOpening** (i jako przepust z zaznaczonymi **Bryłami otworów**), więc ponowne sprawdzenie nie ma nic do zgłoszenia. Ściana, strop lub dach ustawione nad ciągami w kolejności cięcia (według klasy lub materiału) nie są przecinane, ani nośne (LoadBearing w ich Pset_…Common), chyba że zaznaczono **przecinaj także je**: wykrywanie kolizji zgłasza je jako **Nieprzecięte**. Ciąg wyburzony nic nie przecina, a tam, gdzie ciąg przechodzi już przez otwór, żaden nie jest wycinany. System może zdecydować inaczej w **Instalacje → Systemy…** (**Ciągi przecinają ściany, stropy i dachy**: jak kolejność cięcia, tak, nie). W nowym projekcie jest włączone; projekt zrobiony wcześniej ma je wyłączone, dopóki się go nie włączy.

W ścianie łukowej otwór wypada tam, gdzie ciąg spotyka łuk, a jego szerokość mierzy się wzdłuż łuku; prostokątny ma boki promieniowe, jak otwór drzwi lub okna. W dachu wycina się go w połaci, przez którą przechodzi ciąg: pionowo dla stojącej rury (wywiewki), wydłużony wzdłuż przebiegu dla ciągu ukośnego, jak w stropie; płatwie i płatew kalenicowa pod połacią nie są przecinane, a wykrywanie kolizji zgłasza ciąg, który przez nie przechodzi. Przesunięcie lub zmiana rozmiaru ciągu przebudowuje tylko ściany, stropy i dachy, które przecinał lub przecina teraz, niezależnie od wielkości modelu. Własny IFC Lapina wczytany z powrotem nie bierze tych otworów ponownie, gdy przychodzą też ich ciągi (ciągi wycinają je na nowo, nigdy dwa razy); bez swojego ciągu (pominiętego w imporcie albo w projekcie, którego ciągi nic nie przecinają) każdy wchodzi jako otwór swojego elementu, więc nic nie ginie.

![Ciągi instalacyjne w kolejności cięcia](shots/mep-voids-cut-order.webp)

Karta **Bryły** wypisuje każdą bryłę z jej znakiem (V1, V2… kondygnacja po kondygnacji), elementem nośnym, drzwiami lub oknem w niej, rozmiarem i głębokością; kliknięcie zaznacza jej otwór.

![Bryły ze znakami i rozmiarami](shots/mep-voids-list.webp)

**Widok → Bryły otworów**, z widokiem 3D z przodu, rysuje je w 3D, półprzezroczyste fioletowe ze znakami; kliknięcie zaznacza otwór.

![Bryły w ścianie wewnętrznej, z rurami przez nie](shots/mep-voids-3d.webp)

Z rzutem, przekrojem lub elewacją z przodu **Widok → Bryły otworów** rysuje je w tym widoku, jak rysunek koordynacyjny pokazuje przewidziane otwory: przecięte tam, gdzie widok je tnie (otwór w ścianie na całej jej grubości, w stropie przez płytę), widziane tam, gdzie je widzi (otwór w stropie poniżej cięcia rzutu, otwór w ścianie na licu, na które patrzy elewacja), przerywane, gdy otwór w ścianie leży pod cięciem, a w stropie nad nim; każdy przekreślony po przekątnych, z oznaczeniem, a pod nim wymiar i rzędna dołu otworu w ścianie (**400×200 +0.90**, **Ø200**). Rysuje je silnik razem z resztą rysunku, więc arkusze, PDF i DXF (warstwa **VOIDS**) drukują je tak, jak pokazuje ekran.

![Rzut z bryłami otworów: przecięte w ścianach, otwór w stropie widziany, każdy przekreślony i oznaczony](shots/mep-voids-plan.webp)

Etykieta otworu stoi obok niego, w poprzek ściany, tam gdzie nic innego nie jest narysowane: zakres otwierania drzwi, znacznik drzwi lub okna, inne etykiety. Gdy obie strony ściany są zajęte, przesuwa się wzdłuż ściany za otwór, a jeśli i to nie, dalej na zewnątrz.

![Otwór drzwi: jego etykieta z dala od zakresu otwierania i od znacznika drzwi](shots/void-label.webp)

Aby przesunąć etykietę, kliknij ją (lub otwór): zaznacza to otwór, którego etykieta ma kwadratowy uchwyt do przesuwania i okrągły do obracania. Ctrl/⌘-klik na uchwycie przywraca etykietę na domyślne miejsce lub znów poziomo. Przesuniętą etykietę zapamiętuje ten widok, wzdłuż i w poprzek ściany od środka otworu, więc podąża za otworem, gdy ściana się przesuwa; arkusze i PDF drukują ją tam, gdzie ją postawiono.

![Etykieta otworu przesunięta uchwytem](shots/void-label-moved.webp)

To ustawienie widoku, także w jego właściwościach (**Widok → Ustawienia widoku**): **Bryły otworów** oraz **Wymiary otworów** dla wymiaru pod oznaczeniem. Domyślnie żaden widok ich nie rysuje; styl widoku **Koordynacja** (**Zarządzaj → Style widoku**, wybrany jako **Styl** widoku) rysuje je w każdym widoku, który go przyjmie. Ich linie to kategoria **Bryły**: pióra i kolory w **Zarządzaj → Style obiektów** (linie cięcia, rzutu, ukryte, nad cięciem), pokazywane lub ukrywane w widoku w **Widok → Widoczność / Grafika…**.

Aby przekazać bryły do programu kolizji, zaznacz **Bryły otworów** w **Plik → Eksportuj IFC lub Fragments…** (lub **Eksportuj 3D…**). W IFC każda bryła to **IfcBuildingElementProxy** typu **PROVISIONFORVOID** z **Pset_ProvisionForVoid** (Shape, Width, Height, Diameter dla okrągłej, Depth) i zestawem Lapina **Lapin_OpeningVoid** (znak, rodzaj, element nośny, otwór, odsunięcie), powiązana z elementem nośnym; same otwory pozostają IfcOpeningElement. Każda bryła zachowuje GlobalId między eksportami. W eksporcie siatek tworzą osobną grupę **Opening voids**. Bez zaznaczenia eksporty są jak dotąd.

## MEP Pro

Obliczenia inżynierskie sieci — zapotrzebowanie przenoszone przez każdy przewód i dobrany do niego rozmiar, sprawdzenia (otwarte końce, ciasne kolana, spadki kanalizacji, przeciążone źródła), zasady prowadzenia według materiału (miedź w krokach co 45°, PVC skręcające najwyżej o 45°), grzejniki proponowane pod oknami, trasy sugerowane wzdłuż ścian, profil przewodu i aksonometria systemu — znajdują się w edycji **Lapin MEP**, która dodaje do tej karty grupy **Inżynieria** i **Asystenci**.
