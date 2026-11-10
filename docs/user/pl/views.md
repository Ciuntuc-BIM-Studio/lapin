---
title: Przekroje, elewacje i 3D
order: 70
summary: Twórz przekroje i elewacje, otwieraj widoki 3D i ustalaj, co i jak pokazuje każdy widok.
---

Wszystkie widoki są w przeglądarce projektu, ułożone według branży: Architektura, Konstrukcja lub MEP. Kliknij dwukrotnie widok, aby otworzyć go w karcie.

![Przeglądarka projektu z przekrojem i elewacją](shots/browser.webp)

## Przekroje

Wybierz **Widok → Przekrój** (<kbd>SE</kbd>) lub kliknij **+** obok **Przekroje** w przeglądarce. Następnie narysuj linię cięcia na rzucie dwoma kliknięciami i kliknij trzeci raz po stronie, w którą patrzy przekrój. Przekrój jest rysowany z modelu i pozostaje aktualny, gdy go zmieniasz.

![Przekrój A-A przez dom](shots/section-view.webp)

Przekrój nie musi być jedną prostą linią. **Przekrój łamany** (<kbd>SJ</kbd>) rysuje go jako polilinię, kliknięcie po kliknięciu, a <kbd>Enter</kbd> kończy: każdy prosty odcinek jest płaskim cięciem, które widzi tylko między własnymi końcami, a odcinki są rysowane obok siebie w rzeczywistych długościach, z cienką linią przerywaną w miejscu każdego załamania. **Przekrój łukowy** (<kbd>SV</kbd>) rysuje łuk przez trzy punkty (początek, koniec i punkt na łuku) i rozwija go w ten sam sposób, więc zakrzywiona fasada jest widoczna w prawdziwej długości. **Przekrój wzdłuż** (<kbd>SA</kbd>) wybiera element z linią na rzucie: drogę lub ścieżkę, rów, mur oporowy, ścianę, belkę, fundament lub narysowaną polilinię, i podąża za nim w jego obecnym kształcie; wypisuje pikietaże wzdłuż dołu (0+020, co dziesiątą część skali lub z krokiem ustawionym w polu **Pikietaż** w ustawieniach widoku). Na rzucie punkty przekroju są uchwytami: przeciągnij punkt, przeciągnij środek odcinka, aby dodać załamanie, kliknij punkt z Ctrl/⌘, aby go usunąć. Pokazany w 3D przekrój rozwinięty tnie model wzdłuż swojego pierwszego odcinka i układa swój rysunek odcinek po odcinku.

![Przekrój łamany przez dom: trzy odcinki obok siebie, załamania zaznaczone](shots/section-jogged.webp)

Zaznacz przekrój w przeglądarce, aby ustawić w panelu Właściwości jego skalę, głębokość, branżę i styl.

### Gdzie przekrój jest oznaczony

Linia przekroju jest rysowana na każdym rzucie, a przekrój jest oznaczony w każdym innym przekroju i elewacji, które przecina jego płaszczyzna: cienką linią przerywaną tam, gdzie płaszczyzna tnie widok, z nazwą przekroju i strzałką u góry pokazującą kierunek patrzenia, tej samej wielkości na papierze co na rzucie. Przekrój nigdy nie jest oznaczony w samym sobie. Kliknij znacznik, aby zaznaczyć przekrój, kliknij go dwukrotnie, aby go otworzyć.

**Rozmiar znaczników.** **Zarządzaj → Standardy rysunkowe → Projekt** ustawia, jak duże są rysowane znaczniki przekrojów i elewacji, w milimetrach na papierze: **Rozmiar znaczników** (średnica koła elewacji, strzałki przekroju mają 0,7 tej wartości; na początku 10 mm) i **Tekst znaczników** (wysokość ich nazw; puste: wysokość standardowego stylu tekstu). Przekrój lub elewacja z własnym rozmiarem ma we właściwościach **Rozmiar znacznika (mm)** i **Tekst znacznika (mm)**; gdy są puste, bierze wartości projektu. Silnik rysuje znaczniki, więc rzuty, przekroje, które przecinają, arkusze, PDF i DXF się do nich dostosowują.

Każdy przekrój leży na **warstwie znaczników** (na początku **Przekroje**; **Warstwa znacznika** we właściwościach przekroju ją zmienia), a każdy widok wybiera, co oznacza. **Widok → Przekroje…** wyświetla przekroje projektu według warstw dla widoku na pierwszym planie, z polem wyboru dla każdej warstwy i każdego przekroju tylko dla tego widoku, warstwą każdego przekroju (istniejącą lub **Nowa warstwa…**) oraz liczbą widoków, w których każdy jest ukryty. Arkusz oznacza to, co oznaczają jego widoki.

Przekrój poprzeczny przez belkę lub słup (**Przekrój tutaj**, **Przekrój poprzeczny** na karcie Zbrojenie proste) też ma uchwyty na rzucie: przeciągnij dowolny koniec wzdłuż linii, aby dać tej stronie większy lub mniejszy margines, przeciągnij środek wzdłuż elementu, aby przesunąć pikietaż, w którym tnie. Okno przekroju podąża za nim.

![Widok → Przekroje… dla elewacji południowej](shots/sections-dialog.webp)

## Elewacje

**+** obok **Elewacje** tworzy od razu cztery elewacje budynku.

![Elewacja południowa](shots/elevation-view.webp)

Każdy rzut oznacza każdą elewację przed budynkiem: cienką linią punktową w poprzek kierunku jej patrzenia, a pośrodku kołem z czarną strzałką wskazującą budynek, z nazwą elewacji za nim. Kliknij znacznik, aby zaznaczyć elewację, kliknij go dwukrotnie, aby ją otworzyć. **Znaczniki elewacji** w **Widoczność / Grafika** (<kbd>VV</kbd>) ukrywają je w widoku.

**Jak daleko sięga elewacja.** Zaznacz elewację na rzucie: jej linia ma uchwyt na każdym końcu. Przeciągnij jeden wzdłuż linii, aby wydłużyć lub skrócić elewację z tej strony; elewacja rysuje wtedy tylko to, co leży między oboma końcami, a jej arkusze i PDF podążają za nią. Każde przeciągnięcie to jeden krok cofania. Dopóki ich nie przeciągniesz, linia obejmuje cały budynek i rośnie razem z nim.

![Znacznik elewacji na rzucie, jej linia skrócona do środka domu](shots/elevation-marker.webp)

### Cienie słońca

Elewacja, przekrój lub plan sytuacyjny mogą rysować cienie, które słońce rzuca na to, co widzą, wypełnione pod ich liniami, więc drukują się na arkuszach, w PDF i DXF. Wybiera się je we właściwościach widoku, w **Cienie słońca**:

- **własne słońce**: słońce widoku. Domyślnie konwencja rysunkowa: słońce zza lewego ramienia, promienie wzdłuż przekątnej sześcianu (z 45°, na wysokości 35,26°), więc cień jest tak głęboki, jak wystaje to, co go rzuca. **Słońce z** i **Wysokość słońca** je obracają; **Konwencja rysunkowa** je przywraca.
- **słońce projektu**: słońce w miejscu projektu (Zarządzaj → Lokalizacja) w dniu i o godzinie widoku. **Jak w Słońce i cienie** bierze dzień i godzinę z Widok → Słońce i cienie.

Przekrój rzuca i przyjmuje cienie tylko z tego, co leży za jego cięciem. Rzut kondygnacji nie rysuje cieni: jego ściany, przecięte na wysokości rzutu, rzucałyby je za krótkie.

![Elewacja południowa z cieniem dachu](shots/elevation-shadows.webp)

![Cienie słońca widoku](shots/view-shadows.webp)

### Kreskowania powierzchni

Elewacja lub przekrój kreskuje każdą widzianą ścianę kreskowaniem **Elevation** jej materiału (**Zarządzaj → Materiały**, karta **2D graphics**): wzorem, skalą, kątem, piórem i wypełnieniem. Kreskowana jest tylko ta część ściany, której nie zasłania bliższy element, więc tynk czy cegłę elewacji widać wokół drzwi i okien, a przekrój kreskuje to, co widzi za płaszczyzną cięcia, a nie za elementami, które przecina. Ściana warstwowa pokazuje materiał swojej zewnętrznej warstwy. Kreskowanie rysowane jest pod liniami, więc drukuje się na arkuszach, w PDF i w DXF (warstwy SURFACE-HATCH i SURFACE-FILL).

Przy **Alignment** *view* wzór biegnie dalej z jednej ściany na następną; przy *layer* leży na samej ścianie, wzdłuż jej poziomu od dolnego narożnika, a ściana widziana skośnie pokazuje go skrócony. Materiał biegnący w jednej płaszczyźnie przez kilka elementów jest kreskowany jako jeden, chyba że **Join materials** w stylu widoku jest wyłączone.

Dopóki go nie wybierzesz, kreskowanie elewacyjne materiału pochodzi **z nazwy**, jak jego kreskowanie przekroju, a **Zarządzaj → Materials** pokazuje które (*z nazwy (BRICK)*): cegła i mur BRICK, kamień STONE, drewno BOARDS, okładzina metalowa VERTICAL, dachówki, gonty i łupek HORIZONTAL, płytki TILES, żwir i ziemia jak w przekroju. Izolacja, membrany, tynk, farba i beton nie dostają żadnego: BATT izolacji to kreskowanie przekroju, nigdy nie proponowane dla powierzchni (wybrane ręcznie jest rysowane). Wybierz **none**, aby nic nie rysować. Przekrój rozwinięty (łamany, łukowy lub wzdłuż elementu) kreskuje każdy swój odcinek między jego końcami i tak samo rysuje na nim cienie słońca. **Surface hatches** w stylu widoku (**Widok → View settings**, **Zarządzaj → View styles**) wyłącza je dla całych widoków: włączone w Architectural, wyłączone w Structural, MEP i Coordination. Rzuty zachowują własne kreskowania przekroju i rzutu. Pokrycie dachu jest kreskowane według jego materiału, więc dach z dachówki pokazuje rzędy na połaciach widocznych w widoku i żadnych na odwróconych.

![Elewacja południowa, cegła licowa kreskowana według nazwy](shots/elevation-surface-hatch.webp)

![Przekrój łamany kreskuje mur ogrodowy widziany przez jego ostatni odcinek](shots/section-developed-surface-hatch.webp)

![Kreskowania powierzchni w ustawieniach widoku](shots/view-surface-hatch.webp)

## Widoki 3D

<!-- video section-3d -->

Kliknij dwukrotnie **3D**, aby otworzyć widok 3D. Przeciągnij lewym przyciskiem, aby obracać się wokół modelu. Plan kluczowy w prawym górnym rogu pokazuje, skąd patrzysz. **Rzut**, **Persp** i **Dopasuj** zmieniają kamerę. Aby zachować kamerę, powiel widok 3D.

Kółko myszy powiększa do punktu modelu pod kursorem, który zostaje na miejscu na ekranie; powiększanie zatrzymuje się tuż przed tym punktem i nigdy przez niego nie przechodzi. Przeciągnięcie lewym przyciskiem obraca widok wokół środka zaznaczonych elementów, który zostaje na miejscu na ekranie, więc widzisz je ze wszystkich stron; bez zaznaczenia obraca się wokół punktu modelu pod kursorem, w którym zaczyna się przeciąganie, a wokół środka widoku, gdy pod kursorem nic nie ma. **Widok → Powiększ do kursora** i **Widok → Obrót wokół zaznaczenia** wyłączają każde z nich (kółko powiększa wtedy do środka widoku, a widok obraca się wokół swojego środka), podobnie jak pola wyboru we właściwościach widoku 3D. Wybór zostaje zapisany na tym komputerze.

### Przekrój w 3D

Kliknij prawym przyciskiem przekrój w przeglądarce i wybierz **Pokaż w 3D**. Widok 3D tnie model wzdłuż linii przekroju. Zachowuje stronę, w którą patrzy przekrój, do głębokości przekroju, jeśli ją ma. Każdy przecięty element jest zamknięty w miejscu cięcia, warstwa po warstwie, każda warstwa we własnym materiale: ściana pokazuje swój ceglany rdzeń i tynk, pełna jak sama ściana. **Kreskowania** na plakietce u dołu widoku 3D kładzie zamiast tego na cięciu rysunek przekroju, z wypełnieniami i kreskowaniami cięcia, które drukuje arkusz. Wymiary, rzędne, etykiety i teksty przekroju leżą na tej samej płaszczyźnie, w tej samej wielkości i w tym samym miejscu co w przekroju. Linie zachowują grubości piór i pełne wzory kresek. Te narysowane obok budynku stoją w powietrzu obok cięcia. **Opisy** na plakietce u dołu widoku 3D ukrywa je lub znowu pokazuje. **Pokaż w 3D** elewacji obraca kamerę tak, aby patrzyła tak jak elewacja.

![Menu kontekstowe przekroju w przeglądarce](shots/section-3d-menu.webp)

![Przekrój B-B widziany w 3D](shots/section-3d.webp)

Aby znowu zobaczyć cały model, kliknij **×** na plakietce **Przekrój** u dołu widoku 3D lub ponownie wybierz **Pokaż w 3D**.

![Przekrój B-B z włączonymi Kreskowaniami: cięcie tak, jak drukuje je arkusz](shots/section-3d-hatches.webp)

### Skrzynka przekroju

**Widok → Skrzynka przekroju** (skrót BX) przycina widok 3D do prostopadłościanu. Skrzynka zaczyna się wokół całego modelu. Każda ściana skrzynki ma strzałkę pośrodku: przeciągnij ją, aby przesunąć tę ścianę. Każdy element przecięty przez skrzynkę jest zamknięty na tej ścianie, warstwa po warstwie, w swoich materiałach, a ściany skrzynki stykają się czysto na jej krawędziach. **Przetnij na rzucie** ustawia górę skrzynki na wysokości cięcia rzutu aktywnego poziomu (jego zakresu widoku), więc poziom wygląda w 3D tak, jak tnie go jego rzut. **Dopasuj** ustawia skrzynkę z powrotem wokół całego modelu. Aby obrócić skrzynkę, przeciągnij okrągły uchwyt za jej górnym narożnikiem (co stopień; przytrzymaj Shift, aby obracać co 15°) albo zaznacz ścianę i kliknij **Wyrównaj**: skrzynka obraca się w kierunku biegu ściany. **Kreskowania** rysuje cięcia skrzynki tak jak arkusz, każdy materiał z jego wypełnieniem i kreskowaniem cięcia, zamiast materiałów. Zapisany widok 3D zachowuje swoją skrzynkę przekroju: zapisz widok z włączoną skrzynką albo otwórz zapisany widok i ustaw jego skrzynkę, a skrzynka będzie tam przy każdym otwarciu widoku. **×** na plakietce **Skrzynka przekroju** wyłącza skrzynkę.

![Skrzynka przekroju przycięta na rzucie parteru, z przeciągniętą do środka ścianą wschodnią](shots/section-box.webp)

![Ta sama skrzynka z włączonymi Kreskowaniami: cięcia tak, jak rysuje je arkusz](shots/section-box-hatches.webp)

### Jedna kondygnacja naraz

Aby zajrzeć do wnętrza budynku, pokaż tylko niektóre jego kondygnacje. Kliknij prawym przyciskiem poziom w przeglądarce projektu i wybierz **Pokaż w 3D tylko ten poziom** lub **Pokaż w 3D ten poziom i niższe**, aby zobaczyć budynek przecięty na tym poziomie. Poziom staje się aktywny, a widok 3D podąża za aktywnym poziomem: kliknij inny poziom w przeglądarce, aby przejść piętro wyżej lub niżej.

![Menu kontekstowe poziomu w przeglądarce](shots/storeys-3d-menu.webp)

![Sam parter w 3D](shots/storeys-3d.webp)

**Widok → Pokazane kondygnacje** lub właściwości widoku 3D ustalają resztę:

- **Pokaż**: wszystkie kondygnacje, zaznaczone kondygnacje, tylko aktywną kondygnację albo aktywną kondygnację i te poniżej.
- **Na żadnej kondygnacji**: to, co nie stoi na żadnej kondygnacji (działka, teren, pliki połączone), jest pokazane lub ukryte.
- **Przetnij nad jej poziomem**: model jest cięty tyle nad najwyższą pokazaną kondygnacją, na początku 1,20 m, tak jak tnie go rzut. Każdy element tam przecięty jest zamknięty na cięciu, warstwa po warstwie, w swoich materiałach, tak jak przekrój pokazany w 3D.

![Parter przecięty na 1,20 m: jego ściany zamknięte, warstwa po warstwie](shots/storeys-3d-cut.webp)

Ukryte kondygnacje nie są rysowane i nie możesz ich zaznaczać, dopasowywać do nich widoku ani po nich chodzić. To, co zabiera cięcie (cięcie nad pokazanymi kondygnacjami, przekrój pokazany w 3D, skrzynka przekroju), nie istnieje też podczas spaceru: przechodzisz obok górnej części ściany, którą cięcie usunęło, a zamknięcia na cięciu zamykają to, co ono otwiera. **Plik → Eksportuj 3D…** bierze widok 3D tak, jak go pokazuje (zobacz [Import i eksport](import-export.md)). Zapisany widok 3D zachowuje własny wybór. Plakietka **Kondygnacje** u dołu widoku 3D mówi, co jest pokazane; jej **×** znowu pokazuje wszystkie kondygnacje.

### Bez wykończeń lub same rdzenie

Widok 3D może pokazać samą konstrukcję budynku, tak jak rzut konstrukcyjny. We właściwościach widoku 3D (gdy nic nie jest zaznaczone) lub w **Widok → Ustawienia widoku** z widokiem 3D na pierwszym planie, **Warstwy** pokazuje:

- **wszystkie warstwy**: ściany, stropy i fundamenty takie, jak są zbudowane;
- **bez wykończeń**: bez warstw, których materiał jest wykończeniem (tynk, wyprawa, farba, płytki, płyty, okładziny podłóg lub materiał, którego kategoria to Wykończenie w Zarządzaj → Materiały); izolacja zostaje;
- **tylko rdzeń**: tylko warstwa oznaczona jako rdzeń (cegła, płyta betonowa).

![Dom w 3D z samym rdzeniem: ściany to ich cegła, stropy ich płyta](shots/layers-3d-core.webp)

Każda zachowana warstwa pozostaje zamkniętą bryłą we własnym materiale, więc cięcie lub skrzynka przekroju zamyka ją jak wcześniej. Drzwi i okna zostają w swoich otworach; ościeże wyłożone wykończeniem idzie razem z wykończeniami ściany. Widok 3D bierze swoje warstwy ze swojego stylu, tak jak rzut: ustaw jego **Branża** na Konstrukcja, a pokaże same rdzenie; ustaw **Warstwy**, aby wybrać inaczej, a przycisk obok przywraca ustawienie stylu. Plakietka **Pokazane warstwy** u dołu widoku 3D mówi, co jest pominięte; jej **×** znowu pokazuje wszystkie warstwy. Ukrytych warstw nie można zaznaczyć ani się o nie oprzeć podczas spaceru, a **Plik → Eksportuj 3D…** w trybie „tak, jak pokazuje widok” eksportuje te same warstwy.

### Spacer po pomieszczeniu

Na rzucie kliknij prawym przyciskiem wewnątrz pomieszczenia i wybierz **Spaceruj w 3D**. Widok 3D umieszcza cię w pomieszczeniu, 1,60 m nad jego podłogą, patrzącego wzdłuż jego najdłuższego boku.

![Menu kontekstowe pomieszczenia na rzucie](shots/room-walk-menu.webp)

![Stojąc w salonie](shots/room-walk.webp)

- Przeciągnij lewym przyciskiem, aby się rozejrzeć.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> lub klawisze strzałek służą do chodzenia. Przytrzymaj <kbd>Shift</kbd>, aby biec. <kbd>Space</kbd> skacze.
- Kółko myszy przesuwa cię krokami do przodu i do tyłu.

Chodzisz tak, jak chodziłby człowiek. Ściany, słupy, meble i zamknięte okna zatrzymują cię, a ty się po nich ślizgasz. Stoisz na stropach, wchodzisz i schodzisz po schodach i rampach, a spadasz, jeśli zejdziesz z krawędzi. Drzwi są dla ciebie otwarte: przechodź przez ich otwory. Zatrzymuje cię tylko to, co rysuje widok 3D: ukryte elementy, kondygnacje, których nie pokazuje, i to, co zabierają jego cięcia, nie stoją ci na drodze.

![Idąc do drzwi sypialni](shots/room-walk-door.webp)

![W górę po schodach](shots/room-walk-stair.webp)

**Leć** na plakietce spaceru lub <kbd>F</kbd> pozwala zamiast tego latać: bez grawitacji, przechodząc przez ściany i stropy. <kbd>E</kbd> i <kbd>Q</kbd> wznoszą i opuszczają. Naciśnij ponownie <kbd>F</kbd>, aby chodzić; opadniesz na podłogę pod sobą.

<kbd>Esc</kbd> lub **Zakończ spacer** przywraca kamerę, którą miałeś wcześniej.

### Realistyczny, ze słońcem

Widok → **Jakość wyświetlania** ustala, jak renderuje się widok 3D, na tym komputerze:

- **Rysunek**: paleta arkusza i równe światło, tak jak Lapin rysował zawsze. Szybko na każdym komputerze.
- **Średni**: cienie słońca, cień w narożnikach, gdzie stykają się ściany, i światło nieba.
- **Realistyczny**: niebo za modelem i teren pod nim (tam, gdzie projekt nie ma terenu), i szkło, przez które przechodzi światło. Do prezentacji, na dobrej karcie graficznej.

![Dom w widoku realistycznym](shots/view-realistic.webp)

Słońce jest słońcem projektu: gdzie stoi dla miejsca projektu (Zarządzaj → **Lokalizacja**) w danym dniu o danej godzinie. Widok → **Słońce i cienie** ustala dzień (równonoc i przesilenia są o jedno kliknięcie) i godzinę, odtwarza dzień i włącza słońce także w widoku rysunku. Bez układu współrzędnych i punktu pomiarowego miejscem jest Bukareszt. Strefa czasowa wynika z długości geograficznej miejsca, z europejskim czasem letnim w Europie.

![Słońce i cienie](shots/sun-dialog.webp)

Widok realistyczny bierze część liczb (cień w narożnikach, jakość cieni, kolor słońca) z [BimGo](https://github.com/aussieBIMguru/BimGo) autorstwa Aussie BIM Guru (MIT-0).

## Modelowanie w 3D

Widok 3D to miejsce do modelowania, nie tylko do oglądania: elementy przesuwa się i kopiuje w nim tak jak w rzucie, przyciągane do narożników, krawędzi i ścian innych elementów.

### Przesuwanie i kopiowanie w 3D

Zaznacz elementy, potem **Move** (M) lub **Copy** (CO) z widokiem 3D na wierzchu. Kliknij punkt bazowy: narożnik, krawędź lub ścianę tego, co przesuwasz. Kursor mówi, do czego się przyciąga: **Endpoint** (punkt końcowy), **Midpoint** (środek), **On edge** (na krawędzi), **On face** (na ścianie) lub **On level** (na poziomie) w pustej przestrzeni; to, co leży za ścianą pod kursorem, nie jest brane. Linie zaimportowanego DXF, pokazanego w 3D, także przyciągają. Potem kliknij, dokąd ma trafić, na przykład narożnik stropu: zaznaczenie podąża za kursorem w 3D, także w górę i w dół, i zostaje tam umieszczone. Od punktu bazowego rozpoznawane są osie czerwona, zielona i niebieska, gdy kursor biegnie wzdłuż jednej; → ← ↑ blokują oś czerwoną, zieloną lub niebieską (ten sam klawisz zwalnia), a Shift trzyma rozpoznaną oś. Przy zablokowanej osi narożnik pod kursorem zostaje sprowadzony na oś, aby wyrównać przesunięcie do niego, nie idąc tam.

Wpisz wartość w dowolnej chwili, bez klikania w pole: długość idzie w kierunku kursora (1200), a dx,dy lub dx,dy,dz to samo przesunięcie (0,0,450 podnosi o 450 mm). Enter ją stosuje, Esc anuluje. Copy kopiuje dalej z tego samego punktu bazowego aż do Enter lub Esc.

To, co ma własną wysokość, idzie w górę lub w dół z przesunięciem: podstawa ściany, słupa lub obiektu, podstawa i szczyt schodów (ich wysokość zostaje), wierzch belki, górne odsunięcie stropu, murłata dachu; bryła Studio dostaje krok przesunięcia. Drzwi lub okno przesuwają się wzdłuż swojej ściany.

To, co stoi na swoim poziomie (ściana, schody, słup, strop, belka, balustrada, dach, mebel, drzwi lub okno), zachowuje swój poziom, gdy przesunięcie przyciąga się od punktu do punktu: trafia tam tylko w rzucie, więc schody przesunięte za górny narożnik na narożnik stropu nie podnoszą się o kilka centymetrów między konstrukcją schodów a wykończeniem stropu (góra schodów już wypada w licu wykończenia poziomu, na który prowadzą). Aby przesunąć je w górę lub w dół, podążaj za niebieską osią albo ją zablokuj (↑), albo wpisz dx,dy,dz; etykieta mówi, kiedy poziom jest zachowany. Gdy nic nie jest zaznaczone, Move i Copy pytają o elementy: kliknij je w 3D, potem Enter.

Możesz też nacisnąć zaznaczony element i go przeciągnąć: przesuwa się tak samo, od naciśniętego punktu, przyciągnięty, i zostaje tam, gdzie go puścisz. **View → Drag to move in 3D** to wyłącza, aby przeciąganie po zaznaczeniu obracało widok.

![Schody przesunięte w 3D narożnikiem najwyższego stopnia na narożnik stropu](shots/model-3d-move.webp)

### Umieszczanie elementów w 3D

Każde narzędzie, które w rzucie przyjmuje punkty, działa też w widoku 3D: ściany, ściany osłonowe, stropy, dachy, sufity, otwory w stropie, pomieszczenia, słupy, belki, fundamenty, balustrady, listwy przypodłogowe, meble, windy, drogi, rośliny i pozostałe. Punkty trafiają na aktywny poziom: kursor przyciąga się do modelu jak przy przesuwaniu (narożnik stropu, koniec belki, środek krawędzi, linie DXF), a punkt brany jest dokładnie pod nim, na poziomie; przerywana linia pokazuje gdzie, gdy przyciągnięcie jest wyżej lub niżej. Punkty i kontur, który tworzą, rysują się na poziomie na bieżąco: linia ściany, kontur stropu zamykający się na pierwszym punkcie, łuk przez trzeci punkt. Od ostatniego punktu rozpoznawane są osie czerwona i zielona; → i ← je blokują.

Wpisz wartość w dowolnej chwili: długość idzie od ostatniego punktu w kierunku kursora (3000 lub 3000<30 z kątem), szerokość,wysokość prostokąta od jego pierwszego narożnika. Enter ją stosuje, zamyka strop lub kończy balustradę; Backspace cofa ostatni punkt; Esc porzuca punkty, a za drugim razem kończy narzędzie. Przeciąganie nadal obraca widok, więc między dwoma kliknięciami można obrócić model. Etykieta na dole mówi, na jaki poziom idą punkty.

Drzwi i okna trafiają tam, gdzie klikniesz ścianę ściany w 3D, w klikniętym miejscu wzdłuż ściany.

![Ściana zaczęta w 3D na narożniku stropu, z osią zieloną rozpoznaną od początku](shots/model-3d-wall.webp)

### Uchwyty i komponenty w 3D

Uchwyty zaznaczonego elementu (końce ściany, narożniki stropu) przeciąga się w 3D jak w rzucie i przyciągają się jak przy Move: najpierw narożnik, potem środek, potem krawędź w zasięgu, potem osie czerwona i zielona przez punkt startu uchwytu, potem ściana bryły. Punkt jest zaznaczony i nazwany przy kursorze i brany na wysokości uchwytu. Wpisz długość lub kąt podczas przeciągania, jak w rzucie; Enter stawia tam uchwyt.

Przeciągnij kartę z **Catalog** lub narzędzie ze wstążki na widok 3D: jego narzędzie startuje z tym typem. Narzędzie, które umieszcza jednym kliknięciem (słup, stopa fundamentowa, mebel, winda, roślina), umieszcza element od razu tam, gdzie go upuścisz, przyciągnięty do modelu. Upuszczony lub kliknięty na stropie innej kondygnacji, element trafia na tę kondygnację: stół postawiony na stropie piętra należy do piętra, niezależnie od aktywnego poziomu.

## Co pokazuje widok

Karta **Widok** ustala, co pokazuje widok na pierwszym planie i jak wygląda.

**Siatka** (<kbd>F7</kbd>) ukrywa lub pokazuje siatkę tła rzutu, a **Cienkie linie** (<kbd>TL</kbd>) rysuje każdą linię o grubości jednego piksela, aby łatwiej czytać gęsty rysunek. Oba ustawienia dotyczą tylko ekranu: arkusze, PDF i DXF zachowują swoje pióra, a przyciąganie do siatki pozostaje. Siatka gruntu w widoku 3D idzie za tym samym przełącznikiem.

![Karta Widok](shots/ribbon-view.webp)

- **Ustawienia widoku.** Branża i styl widoku, jego zakres (wysokość i głębokość cięcia) oraz jego grafika.
- **Style widoku.** Architektoniczny, Konstrukcyjny (same rdzenie), MEP lub twój własny. Każdy styl wybiera rysowane warstwy (wszystkie, bez wykończeń lub rdzeń) i sposób łączenia materiałów.
- **Symbole 2D IFC.** Elementy zaimportowane lub połączone z pliku IFC, które przynoszą własny rysunek rzutu (obrys, łuk otwierania drzwi, symbol urządzenia, z kolorami, wypełnieniami i tekstami pliku), rysują go na rzutach zamiast swojego cięcia. Odznacz to w Ustawieniach widoku (lub w stylu widoku), aby zamiast tego rysować ich cięcie 3D.
- **Widoczność** (<kbd>VV</kbd>) i **Grafika** (<kbd>VG</kbd>). Pokazują lub ukrywają kategorie i ustawiają ich pióra, rodzaje linii i wypełnienia.
- **Podziel** i **Okno.** Pokazują dwa widoki obok siebie lub otwierają widok w osobnym oknie.
- **Jakość wyświetlania.** Jak renderuje się widok 3D (**Rysunek**, **Średni** lub **Realistyczny**, zapisane na tym komputerze: zob. *Realistyczny, ze słońcem*) oraz jak widok 3D rysuje części z wieloma trójkątami (zaimportowaną roślinę, meble, rury), gdy są daleko. **Zrównoważona** rysuje daleką część zgrubniej tylko tam, gdzie różnica jest mniejsza niż piksel. **Szybka** pozwala jej odbiegać do 3 pikseli, dla dużych modeli i wolniejszych komputerów. **Precyzyjna** rysuje każdą część taką, jaka jest. Ustawienie należy do projektu. Model, jego rysunki, eksporty i ilości nigdy nie są upraszczane: krzywa składa się z prostych odcinków, które odbiegają od niej najwyżej o milimetr.
- **Słońce i cienie.** Słońce projektu w danym dniu o danej godzinie, oświetlające widok 3D z jego cieniami (zob. *Realistyczny, ze słońcem*).

![Jakość wyświetlania](shots/display-quality.webp)

### Widoczność / Grafika

**Widoczność** (<kbd>VV</kbd>) lub **Grafika** (<kbd>VG</kbd>) otwiera widoczność / grafikę widoku na wierzchu: rzutu, przekroju, elewacji lub widoku 3D. To, co ukrywa, jest ukryte także na arkuszach widoku, w jego PDF i DXF oraz w eksporcie 3D.

- **Kategorie modelu.** Każdą kategorię można pokazać lub ukryć, z kolorem i grubością linii, wypełnieniem i kreskowaniem przekroju. Strzałka obok kategorii otwiera jej podkategorie: **Rdzeń**, **Wykończenia**, **Izolację**, **Membrany** i **Inne warstwy** ściany, stropu lub dachu (według materiału każdej warstwy i jego kategorii w Zarządzaj → Materiały), **Ościeża** drzwi lub okna. Odznacz **Wykończenia** pod **Ściany**, a tynk znika, cegła zostaje. Zbrojenie, okładziny, deskowania, rusztowania, instalacje, połączenia stalowe, windy i osłony przeciwsłoneczne to osobne kategorie.
- **Kategorie opisów.** Wymiary, teksty, etykiety, etykiety pomieszczeń, osie, poziomy, znaczniki przekrojów i elewacji, wywołania, płaszczyzny odniesienia, granice widoku i legendy, każda pokazana lub ukryta w tym widoku.
- **Kategorie importowane.** Każdy DXF zaimportowany do widoku, według pliku: widoczny, wyszarzony i jego warstwy jedna po drugiej (strzałka obok). PDF lub obraz do odrysowania pokazuje się lub ukrywa również tutaj.
- **Filtry.** Oparte na regułach, jak w Revit: **Nowy filtr**, potem jego reguły (klasa IFC, kategoria, typ, nazwa, materiał, kondygnacja, grupa, łącze, faza utworzenia, typ predefiniowany lub parametr, jak *Odporność ogniowa* czy *Pset_WallCommon.IsExternal*; jest równe, zawiera, zaczyna się od, większe, mniejsze, ma wartość…), wszystkie lub dowolna. Widok używa filtrów ze swojej listy, po kolei; pierwszy, który obejmuje element, decyduje, czy jest pokazany. **Zarządzaj → Filtry widoku** otwiera tę kartę.
- **Łącza.** Każdy połączony plik, z **Widoczne** i **Wyszarzenie** tylko dla tego widoku. **Wyświetlanie** to **Jak widok główny** (łącze bierze kategorie tego widoku) lub **Niestandardowe**: wtedy **Kategorie…** daje łączu własne kategorie, podkategorie i klasy IFC w tym widoku. Rzut architekta ukrywa zbrojenie łącza konstrukcyjnego; rzut inżyniera wykończenia łącza architektonicznego.
- **Klasy IFC.** Klasy własnych elementów projektu, pokazane lub ukryte.
- **Nadpisania grafiki.** Kategoria przyjmuje też **Wzór** dla swoich linii przekroju i rzutu, **Wyszarzenie** i **Przezroczystość** (w 3D, od 0 nieprzezroczyste do 95); kategoria opisów **Kolor** i **Wyszarzenie**; filtr **Kolor**, **Wzór**, **Wyszarzenie** i **Przezroczystość** dla tego, co obejmuje, a pierwszy filtr na liście ustala każde pole. **Poziom szczegółowości** (zgrubny, średni, dokładny) określa, ile rysują symbole rzutu, inaczej według skali.
- **Poziom rozwoju (LOD).** Sposób patrzenia, nigdy brany ze skali: widok rysuje na poziomie, który daje mu jego styl (Zarządzaj → Style widoków: 350, styl konstrukcji 400), albo na własnym (Widok → Ustawienia widoku lub nagłówek Widoczności / Grafiki, także widoku 3D). **LOD 100** (bryły) rysuje ściany, stropy i dachy jednym obrysem kreskowanym jak rdzeń, profile stalowe jedną linią na ich osi, a drzwi lub okno jako otwór i kierunek otwierania; **LOD 200** (warstwy) dodaje warstwy oraz ościeżnice i skrzydła drzwi i okien w uproszczeniu (bez szprosów i okuć); **LOD 300** (detale) ich pełne profile i detale (ościeża, parapety, opaski, progi, parapety wewnętrzne, okucia, okiennice, kraty) oraz okładziny otworów; **LOD 350** (połączenia) blachy, śruby i kotwy połączeń stalowych; **LOD 400** (wykonawstwo) pręty zbrojeniowe podłączonego modelu konstrukcji i rzeczywisty przekrój profili stalowych: zaokrąglenia, promienie na końcach półek, pochylone półki. Projekt utworzony wcześniej zachowuje swoje rysunki: każdy widok dostaje poziom, jaki dawała mu skala (1:50 LOD 400, 1:100 LOD 200, 1:200 LOD 100), widok 3D LOD 400. Ściany i stropy podłączonego lub zaimportowanego IFC dzielą się według zestawów warstw, które nadaje im plik, więc ich **Wykończenia**, **Izolacja** i **Rdzeń** ukrywają się jak w natywnej ścianie, a rysunek samych rdzeni stylu Konstrukcja obejmuje i je; tynk ściany przyniesiony jako osobna część albo okładzina z jednego materiału wykończeniowego idzie z **Wykończeniami**. Każdy widok 3D pomija **Zbrojenie**, dopóki go nie pokażesz.

Ustawienia należą do widoku albo do nazwanego zbioru, który dzieli kilka widoków (**Zapisz jako zbiór…**). Styl widoku może wskazać zbiór (Zarządzaj → Style widoków): widok bez własnych ustawień bierze zbiór swojego stylu, jak szablony widoków w Revit; **Uczyń je własnymi tego widoku** zaczyna od niego własne.

![Widoczność / Grafika: wykończenia ściany ukryte](shots/vg-categories.webp)

![Połączona konstrukcja, niestandardowa w jednym widoku](shots/vg-links.webp)

![Filtr ukrywający to, co obejmują jego reguły](shots/vg-filters.webp)

![Kategorie opisów](shots/vg-annotations.webp)

![Import DXF według warstw](shots/vg-imports.webp)

![Kolor i wzór filtra na rzucie](shots/vg-overrides.webp)

![Rzut z filtrem](shots/vg-plan.webp)

![Rzut na LOD 350 swojego stylu: warstwy ścian i detale okna](shots/detail-fine.webp)

![Ten sam rzut na LOD 100: jeden obrys kreskowany jak rdzeń](shots/detail-coarse.webp)

### Chmury punktów w widokach

Połączona chmura punktów (*Import i eksport*, Chmury punktów) jest widoczna we wszystkich widokach. **Rzut** rysuje plaster, który bierze jego przekrój, 10 cm wokół płaszczyzny cięcia, jako kropki pod ścianami; **przekrój** plaster tuż za swoją płaszczyzną; **elewacja** każdy punkt, na który patrzy, najbliższy z przodu. Kropki mają kolory skanu, przyciemnione, by były czytelne na papierze, i drukują się na arkuszach i w PDF tak, jak pokazuje je ekran. Ich punkty są punktami przyciągania: ściany narysowane na plastrze skanu trafiają na jego powierzchnie. W **widoku 3D** narzędzia wstawiania i Move przyciągają do punktów chmury jak do narożników. Nowy rzut otwiera się dopasowany do modelu albo do przekroju chmury, gdy nie ma nic innego; rysowana jest tylko część przekroju widoczna na ekranie, więc skan całej dzielnicy pozostaje szybki przy powiększeniu na jeden budynek.

**Visibility / Graphics** (<kbd>VV</kbd>) ma kartę **Point clouds**: dla każdej chmury, tylko w tym widoku, **Shown**, **Halftone**, jej **Colours** (jak jej połączenie, RGB, intensywność, wysokość lub klasyfikacja), **Slice**, który rysuje rzut lub przekrój (cm, do 100 m: elewacje ze skanowania lotniczego wymagają grubego przekroju), oraz **Classes…**: klasy ASPRS jej punktów (grunt, roślinność, budynek, szum…), każda widoczna lub ukryta. Karta widoku 3D decyduje, co rysuje 3D. **Export 3D…** bierze próbkę widocznych chmur (najwyżej 5 milionów punktów) jako punkty w GLB i glTF, gdy zaznaczone jest **Point clouds**; żaden inny eksport ich nie zawiera.

![Plaster skanu w rzucie, ściana narysowana na nim](shots/point-cloud-plan.webp)

![Visibility / Graphics: roślinność chmury ukryta w jednym widoku](shots/point-cloud-vg.webp)
