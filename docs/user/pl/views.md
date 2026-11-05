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

Każdy przekrój leży na **warstwie znaczników** (na początku **Przekroje**; **Warstwa znacznika** we właściwościach przekroju ją zmienia), a każdy widok wybiera, co oznacza. **Widok → Przekroje…** wyświetla przekroje projektu według warstw dla widoku na pierwszym planie, z polem wyboru dla każdej warstwy i każdego przekroju tylko dla tego widoku, warstwą każdego przekroju (istniejącą lub **Nowa warstwa…**) oraz liczbą widoków, w których każdy jest ukryty. Arkusz oznacza to, co oznaczają jego widoki.

Przekrój poprzeczny przez belkę lub słup (**Przekrój tutaj**, **Przekrój poprzeczny** na karcie Zbrojenie 2D) też ma uchwyty na rzucie: przeciągnij dowolny koniec wzdłuż linii, aby dać tej stronie większy lub mniejszy margines, przeciągnij środek wzdłuż elementu, aby przesunąć pikietaż, w którym tnie. Okno przekroju podąża za nim.

![Widok → Przekroje… dla elewacji południowej](shots/sections-dialog.webp)

## Elewacje

**+** obok **Elewacje** tworzy od razu cztery elewacje budynku.

![Elewacja południowa](shots/elevation-view.webp)

## Widoki 3D

<!-- video section-3d -->

Kliknij dwukrotnie **3D**, aby otworzyć widok 3D. Przeciągnij lewym przyciskiem, aby obracać się wokół modelu. Plan kluczowy w prawym górnym rogu pokazuje, skąd patrzysz. **Rzut**, **Persp** i **Dopasuj** zmieniają kamerę. Aby zachować kamerę, powiel widok 3D.

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

## Co pokazuje widok

Karta **Widok** ustala, co pokazuje widok na pierwszym planie i jak wygląda.

**Siatka** (<kbd>F7</kbd>) ukrywa lub pokazuje siatkę tła rzutu, a **Cienkie linie** (<kbd>TL</kbd>) rysuje każdą linię o grubości jednego piksela, aby łatwiej czytać gęsty rysunek. Oba ustawienia dotyczą tylko ekranu: arkusze, PDF i DXF zachowują swoje pióra, a przyciąganie do siatki pozostaje.

![Karta Widok](shots/ribbon-view.webp)

- **Ustawienia widoku.** Branża i styl widoku, jego zakres (wysokość i głębokość cięcia) oraz jego grafika.
- **Style widoku.** Architektoniczny, Konstrukcyjny (same rdzenie), MEP lub twój własny. Każdy styl wybiera rysowane warstwy (wszystkie, bez wykończeń lub rdzeń) i sposób łączenia materiałów.
- **Symbole 2D IFC.** Elementy zaimportowane lub połączone z pliku IFC, które przynoszą własny rysunek rzutu (obrys, łuk otwierania drzwi, symbol urządzenia, z kolorami, wypełnieniami i tekstami pliku), rysują go na rzutach zamiast swojego cięcia. Odznacz to w Ustawieniach widoku (lub w stylu widoku), aby zamiast tego rysować ich cięcie 3D.
- **Widoczność** (<kbd>VV</kbd>) i **Grafika** (<kbd>VG</kbd>). Pokazują lub ukrywają kategorie i ustawiają ich pióra, rodzaje linii i wypełnienia.
- **Podziel** i **Okno.** Pokazują dwa widoki obok siebie lub otwierają widok w osobnym oknie.
- **Jakość wyświetlania.** Jak renderuje się widok 3D (**Rysunek**, **Średni** lub **Realistyczny**, zapisane na tym komputerze: zob. *Realistyczny, ze słońcem*) oraz jak widok 3D rysuje części z wieloma trójkątami (zaimportowaną roślinę, meble, rury), gdy są daleko. **Zrównoważona** rysuje daleką część zgrubniej tylko tam, gdzie różnica jest mniejsza niż piksel. **Szybka** pozwala jej odbiegać do 3 pikseli, dla dużych modeli i wolniejszych komputerów. **Precyzyjna** rysuje każdą część taką, jaka jest. Ustawienie należy do projektu. Model, jego rysunki, eksporty i ilości nigdy nie są upraszczane: krzywa składa się z prostych odcinków, które odbiegają od niej najwyżej o milimetr.
- **Słońce i cienie.** Słońce projektu w danym dniu o danej godzinie, oświetlające widok 3D z jego cieniami (zob. *Realistyczny, ze słońcem*).

![Jakość wyświetlania](shots/display-quality.webp)
