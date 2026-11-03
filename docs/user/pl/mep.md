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

## MEP Pro

Obliczenia inżynierskie sieci — zapotrzebowanie przenoszone przez każdy przewód i dobrany do niego rozmiar, sprawdzenia (otwarte końce, ciasne kolana, spadki kanalizacji, przeciążone źródła), zasady prowadzenia według materiału (miedź w krokach co 45°, PVC skręcające najwyżej o 45°), grzejniki proponowane pod oknami, trasy sugerowane wzdłuż ścian, profil przewodu i aksonometria systemu — znajdują się w edycji **Lapin MEP**, która dodaje do tej karty grupy **Inżynieria** i **Asystenci**.
