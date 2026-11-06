---
title: Zbrojenie proste
order: 52
summary: Zbrojenie rysowane na przekrojach i rzutach, przekroje poprzeczne wycinane tam, gdzie klikniesz, oraz wykaz stali.
---

Karta **Zbrojenie proste** rysuje zbrojenie tak, jak powstaje wykaz prętów: pręty jako kształty z wymiarami, strzemiona rozłożone wzdłuż belki, numery prętów w kółkach i zestawienie, które je sumuje. Działa w każdym widoku: na przekroju, rzucie, elewacji.

![Karta Zbrojenie proste](shots/ribbon-rebar.webp)

## Przekrój tam, gdzie pracujesz

Zbrojenie wymaga przekroju poprzecznego obok podłużnego. **Przekrój tutaj** robi oba: wskaż belkę, słup lub ścianę w widoku, w którym jesteś, a w miejscu kliknięcia zostanie wycięty przekrój *w poprzek* elementu, prostopadle do jego osi, i narysowany obok kliknięcia, w tym samym widoku, z pół metra otoczenia (stropu, słupa). Słup jest przecinany poziomo na wysokości, na której kliknięto. Klikaj wewnątrz przekroju elementu, nie tylko na jego krawędzi: na przekroju lub elewacji, tak jak na rzucie, kliknięcie wybiera element, w którego przekroju się znajduje.

Wstawiony widok podąża za elementem: przesuń belkę, a przekrój przesunie się razem z nią. Przeciągnij wstawkę za jej uchwyt; jej właściwości nadają jej współczynnik skali i ramkę. **Przekrój poprzeczny** tworzy ten sam przekrój jako osobny widok, a **Widok wstawiony** umieszcza dowolny istniejący widok w tym, w którym jesteś, w rzeczywistej wielkości i w skali widoku nadrzędnego, tak aby narysowane na nim pręty pasowały do jego betonu.

![Przekrój podłużny belki ze wstawionym przekrojem poprzecznym i zbrojeniem](shots/rebar-section.webp)

## Pręty

**Pręt prosty**, **Pręt L**, **Pręt U**, **Strzemię**, **Spinka** i **Pręt z polilinii** umieszcza się kliknięciem (pręt łamany przez jego wierzchołki). Każdy jest kształtem z parametrami: przeciągnij uchwyt, a wymiar, który on reprezentuje, podąży za nim; wpisz go w panelu Właściwości w milimetrach. Pręty są rysowane w swojej rzeczywistej grubości, w kolorze przypisanym każdej średnicy (Ustawienia zbrojenia), i gięte na średnicy wymaganej przez SR EN 1992-1-1: trzpień 4Ø do Ø16, 7Ø powyżej.

Haki ustawia się na każdym końcu: 90°, 135° lub 180°, z prostym odcinkiem według praktyki (12Ø, 10Ø, 5Ø, nigdy poniżej 50 mm), na przeciwną stronę na życzenie. Strzemię powstaje z dwoma hakami 135° zwróconymi do wewnątrz.

**Uchwyty zakotwienia.** Zaznaczony pręt widziany z przodu pokazuje obok swoich uchwytów pomarańczowe romby leżące *na zewnątrz* niego w odległości **otuliny**: jeden w jego początku i po jednym na każdym wolnym końcu (koniec z hakiem go nie ma). Reprezentują powierzchnię deskowania: przeciągnij jeden na przekrój elementu — jego narożnik, jego powierzchnię; rysunek tam przyciąga — a pręt zatrzyma się o otulinę wewnątrz. Zakotwienie końca ustala długość pręta, zakotwienie początku przesuwa cały pręt. Otulina to własna otulina pręta, wpisana w polu **Otulina** jego właściwości (wyczyść je, aby wrócić), w przeciwnym razie otulina strzemion typu elementu nadrzędnego plus strzemię, a w przeciwnym razie ta z Ustawień zbrojenia; pole podpowiada, która obowiązuje.

**Pręt w przekroju** to pręt widziany od czoła: kropka o jego średnicy, z długością, jaką ma wzdłuż pręta. **Ten sam pręt w przekroju** umieszcza zaznaczony pręt jako kropkę w innym miejscu, nie licząc go dwukrotnie. **Szyk** we właściwościach powtarza pręt co określoną liczbę milimetrów, jak pręty jednego rzędu w przekroju poprzecznym.

Kółko numeru pokazuje numer pręta i to, czym jest pręt: `4Ø12 L=2450`. Przeciągnij kółko tam, gdzie najlepiej się czyta.

Styl **opisu** określa, co opis zawiera i gdzie stoi: jego części w kolejności (liczba, średnica, rozstaw, długość, numer, stal, dowolny tekst), kółko, czy tekst biegnie poziomo, wzdłuż prętów czy pod kątem, po której stronie prętów stoi i jak daleko od nich, oraz linię odniesienia. Ustawia się go w oknie **Ustawienia zbrojenia** dla każdego rodzaju (pręty, strzemiona w typie, pręty słupa, rozkłady), w typie obok jego strzemion oraz na pręcie w jego właściwościach — wygrywa najbardziej szczegółowy. Kółko nigdy nie jest rysowane bez numeru. Opisy strzemion typu, prętów słupa i pręta widzianego w innym widoku też mają uchwyty: przeciągnij jeden, a zachowa to miejsce w tym widoku (**Przywróć etykietę na miejsce** we właściwościach to cofa); do tego czasu opisy, które by się nałożyły, układają się jeden pod drugim w kolejności numerów.

**Rozkład** — strzemiona w typie, strzemiona wzdłuż ścieżki — jest oznaczany **linią wymiarową** bez wartości: linią równoległą do prętów, na zewnątrz nich, z krótką kreską w miejscu przecięcia z każdym prętem i na obu końcach, opisem nad linią wzdłuż niej i kółkiem dotykającym jej początku. Przeciągnij kółko tam, gdzie najlepiej się czyta; przeciągnij środek linii, aby odsunąć ją od prętów. **Oznaczone przez** w stylu opisu wyłącza linię i zostawia tekst i kółko obok prętów. **Linia odniesienia** może być żadna, jedna lub po jednej do każdego pręta widzianego jako kropka (domyślnie dla prętów w przekroju poprzecznym: jeden pręt, jedna linia; powyżej szesnastu tylko skrajne i środkowy). Przeciągnij środek linii odniesienia, aby ją tam załamać; załamanie ma wtedy własny uchwyt, a Ctrl/⌘-kliknięcie go usuwa.

Długości są nominalne, mierzone na osi pręta między jego narożnikami, z hakami; długość rozwinięta po łukach gięcia jest podana w panelu Właściwości.

**Podziel pręt** tnie pręt podłużny dłuższy niż długość handlowa, w jakiej jest dostarczany (12 m lub 6 m, w oknie **Ustawienia zbrojenia**). Miejsce zakładu dwóch kawałków to Twoja decyzja, nie Lapina: pręt dolny łączy się na zakład przy podporach, pręt górny z dala od nich. Dlatego strefę zakładu podajesz sam: kliknij pręt na jednym końcu zakładu, a potem na drugim; albo kliknij tam, gdzie kończy się pierwszy pręt, i wpisz długość zakładu (800 dla 800 mm), mierzoną wzdłuż pręta; Enter lub Escape po pierwszym kliknięciu kończy **długość zakładu** z ustawień (50Ø, jeśli nie ustawiono) w tym miejscu. We właściwościach pręta ta sama strefa jest wpisywana jako **Zakład od … do …** w milimetrach od początku pręta, z długością zakładu obok (jej zmiana zachowuje *do* i przesuwa *od*); pierwszy pręt kończy się w *do*, drugi zaczyna w *od*, a obie długości są pokazywane podczas wpisywania, przed **Podziel pręt**. Pierwszy zachowuje numer pręta i hak początkowy, drugi otrzymuje następny numer i hak końcowy, a zestawienie liczy je jako dwa numery z własnymi długościami. Drugi kawałek jest rysowany odrobinę obok pierwszego, aby zakład był czytelny. Pręt dłuższy od długości handlowej jest ostrzegany w swoich właściwościach, a **Tnij na długość handlową** kończy zakład na długości handlowej, tak że pierwszy kawałek ma dokładnie tę długość.

## Strzemiona w typie belki lub słupa

Strzemion nie rysuje się pojedynczo: należą do **typu**. W Konstrukcja → Typy konstrukcji prostokątny typ belki lub słupa przyjmuje **Strzemiona tego typu**: ich średnicę, otulinę betonu i sposób rozmieszczenia — jeden rozstaw na całej długości albo **trzy strefy**, każda jako część długości (domyślnie jedna trzecia) z własnym rozstawem, gęstszym przy końcach. Każdy element tego typu pokazuje je wtedy na każdym przekroju zgodnie z ułożeniem widoku względem osi: prostokąt wewnątrz otuliny w przekroju poprzecznym, linię dla każdego strzemienia w podłużnym, linie nad belką na rzucie konstrukcyjnym; z opisem `41Ø8/150` i kółkiem numeru. Są liczone w wykazie stali pod numerem typu. Strzemiona **słupa** mogą być tylko obwodowe, **z rombem wewnątrz** (jego narożniki w środkach boków) lub **z dwoma wewnątrz, po jednym w każdym kierunku**, każdy rodzaj z własnym numerem. Słup zbroi się kondygnacja po kondygnacji, więc jego **pręty podłużne** też są w typie: tyle na każdej ścianie, z narożnikami wspólnymi, ich średnica oraz **wypusty** wychodzące ponad górę (domyślnie 1 m) dla słupa powyżej; kropki w jego przekroju poziomym i na rzucie konstrukcyjnym, linie w przekroju pionowym. **Na górze** określa, jak się kończą: **Wg słupa powyżej** (domyślnie) przedłuża je jako wypusty tam, gdzie na tym słupie stoi inny, a tam, gdzie żaden nie stoi (ostatnia kondygnacja), kończy je pod górą, wewnątrz otuliny, hakiem 90° (12Ø) zagiętym do środka; **Pręty startowe ponad górą** i **Haki 90° pod górą** wybierają jedno z nich niezależnie od tego, co stoi powyżej. Pręt z hakiem jest w zestawieniu prętem L, a jego hak jest wliczony w długość.

**Pręty podłużne** belki nie są w typie (biegną przez kilka przęseł): narysuj je na przekroju podłużnym lub na rzucie, z ich hakami. Pręt narysowany wewnątrz przekroju belki należy do tej belki i istnieje w przestrzeni: **W poprzek rysunku** układa go określoną liczbę razy wzdłuż kierunku widoku, **według otuliny** (pierwszy i ostatni przy powierzchniach, wewnątrz otuliny i strzemienia, pozostałe rozłożone równomiernie) lub **z rozstawem**. Przekrój poprzeczny pokazuje wtedy pręty jako kropki tam, gdzie są, rzut konstrukcyjny jako linie, a zestawienie liczy je wszystkie: `3Ø16 L=6400`. To samo dotyczy pręta narysowanego w przekroju słupa, ściany, płyty, stopy fundamentowej lub schodów. W schodach pręty układa się między betonowymi powierzchniami ich biegów i spoczników, nigdy w balustradach ani wykończeniu.

**Pręty w 3D** (Widok → Pręty w 3D lub przycisk na tej karcie) rysuje każdy pręt w przestrzeni jako rurkę o jego średnicy w widoku 3D, w jego kolorze: strzemiona typu w ich miejscach, pręty słupa, narysowane pręty z ich kopiami. To obraz na ekran, zamawiany u silnika, gdy jest włączony, i zwalniany, gdy jest wyłączony, więc zajmuje pamięć tylko wtedy, gdy patrzysz; nie można go zaznaczyć, nie jest eksportowany, a powyżej dwóch milionów trójkątów rysowane są tylko pierwsze.

## Strzemiona wzdłuż belki

**Strzemiona wzdłuż** przyjmuje dwa końce ścieżki; strzemiona są rozkładane tak, by na każdym końcu stało jedno, z ustawionym rozstawem (domyślnie 150 mm). **Strefy** zmieniają rozstaw po drodze: **Tercje** daje dwukrotnie gęstszy rozstaw przy każdym końcu, jak wymaga belka w strefie sejsmicznej; opis brzmi `25Ø8/100-200`. Pokaż wszystkie, tylko skrajne i środkowe z linią przerywaną między nimi, albo jedno.

## Zestawienie

**Wykaz zbrojenia** tworzy zestawienie wszystkich prętów według numerów: średnica, kształt, liczba, długość jednego, długość całkowita i masa, z sumami według średnic. Numer, którego pręty różnią się średnicą lub długością, jest oznaczany. To zestawienie jak inne: na arkuszach, w CSV i XLSX.

**Ustawienia zbrojenia** zawierają wybory projektu: kolor dla każdej średnicy, wielkości tekstu i kółek na papierze, stal, otulinę, długości odcinków wypisywane wzdłuż prętów i styl opisów dla każdego rodzaju.
