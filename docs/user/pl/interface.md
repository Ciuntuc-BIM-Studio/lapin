---
title: Interfejs i skróty
order: 15
summary: Tryby Wygodny, Kompaktowy i Skupienie, zwijanie paneli bocznych oraz skróty klawiaturowe, które możesz zmieniać.
---

<!-- video interface -->

## Obszary robocze

Obszar roboczy pokazuje tylko te karty wstążki, których potrzebuje twoja praca, w kolejności, w jakiej z nich korzystasz. Wybierz go przyciskiem obok **Plik**, który pokazuje obszar roboczy, w którym jesteś, albo na ekranie startowym, u góry okna **Widok → Interfejs…** albo z **Polecenia…** (Obszar roboczy: …): **Wszystko** (każda karta twojej edycji), **Architektura**, **Konstrukcja i zbrojenie**, **Instalacje**, **Zagospodarowanie i roboty pomocnicze** lub **Rysunek 2D**. Projekt pozostaje jeden: narzędzia ukrytej karty są nadal dostępne w Polecenia… i w skrótach klawiszowych, a model wykonany w jednym obszarze roboczym otwiera się w całości w innym.

![Obszary robocze w Widok → Interfejs…](shots/workspaces.webp)

Gdy narzędzia wstążki są szersze niż okno, kółko myszy nad nimi przewija je w bok.

Karty mieszczą się w jednym wierszu. Gdy okno jest za wąskie na wszystkie, ostatnie trafiają pod **Więcej** na końcu wiersza, a karta, na której jesteś, zawsze pozostaje widoczna.

Karty są pogrupowane według pracy, której służą. **Wnętrza** obejmują wykończenia i meble. **Zagospodarowanie** obejmuje teren, zieleń i roboty pomocnicze (rusztowania i deskowania). **Realizacja** obejmuje harmonogram robót i kosztorys (4D i 5D).

Pokaż, ukryj lub przesuń kartę w sekcji **Karty** tego samego okna dialogowego, a wybór staje się układem **Mój obszar roboczy**, zapamiętanym na tym komputerze na następny raz; pozostałe obszary robocze nadal są dostępne do przełączenia. Karta **Widok** zostaje zawsze, bo znajduje się w niej Interfejs….

## Trzy tryby

**Widok → Interfejs…** (<kbd>Ctrl</kbd> <kbd>.</kbd>) określa, ile elementów otacza widok. Wybierz jeden z trzech trybów u góry okna dialogowego; zmiana widoczna jest od razu.

![Okno dialogowe Interfejs: trzy tryby i każda część osobno](shots/interface-dialog.webp)

- **Wygodny** to okno takie, jak pokazują je inne strony: wstążka z dużymi narzędziami i ich nazwami, wiersz poleceń pod każdym rysunkiem, a nad widokiem HUD, podpowiedź, pola długości i kąta, plan kluczowy i szybkie narzędzia.
- **Kompaktowy** oddaje widokowi większość okna:

![Kompaktowy: więcej miejsca na rysunek](shots/interface-compact.webp)

1. **Wstążka** to jeden pas ikon. Cienkie linie oddzielają grupy. Najedź na narzędzie, aby zobaczyć jego nazwę, klawisze i grupę.
2. **Przeglądarka projektu** zwija się do wąskiego paska. Jego ikony otwierają na chwilę nad widokiem projekt, poziomy lub drzewo IFC.
3. **Szybkie narzędzia** <kbd>1</kbd>–<kbd>9</kbd> stoją przy lewej krawędzi widoku.
4. **Plan kluczowy** jest przyciskiem. Kliknij go, aby otworzyć mapę, a jej ×, aby ją znów zwinąć.
5. **Jeden wiersz stanu** zawiera nazwę narzędzia i to, o co prosi, to, co wpisujesz, pola długości i kąta podczas rysowania, SNAP, ORTHO i POLAR, poziom i widok, a na końcu projekt.

- **Skupienie** zostawia tylko karty i cienki wiersz poleceń. Kliknij kartę, a jej narzędzia rozwiną się nad widokiem; pas zwija się znowu, gdy wybierzesz narzędzie, po naciśnięciu <kbd>Esc</kbd> lub kliknięciu gdzie indziej. Panele się chowają. Gdy coś jest zaznaczone, mała karta pokazuje, co to jest, i jego pierwsze właściwości; **Wszystkie właściwości…** otwiera panel Właściwości nad widokiem. <kbd>Tab</kbd> przywraca oba panele i znów je chowa.

![Skupienie: zaznaczenie na karcie](shots/interface-focus.webp)

W każdym trybie górny pasek ma na prawym końcu **Cofnij** i **Ponów**: z nazwami w trybie Wygodnym, jako dwie strzałki w trybach Kompaktowym i Skupienie (<kbd>Ctrl</kbd> <kbd>Z</kbd> i <kbd>Ctrl</kbd> <kbd>Y</kbd>; ich podpowiedź mówi, co cofają).

Pod trybami każdą część można ustawić osobno: wstążkę, szybkie narzędzia (lub ich brak: klawisze cyfr nadal działają), wiersz poleceń, HUD, monit narzędzia i jego pola, plan kluczowy, panele i wysokość wierszy właściwości. Zmiana jednej części sprawia, że wybór staje się **Własny**.

Pod częściami znajdują się **Karty**: odznacz karty wstążki, bez których się obywasz (architekt może ukryć Konstrukcję, Zbrojenie proste i Instalacje, inżynier Architekturę i Zagospodarowanie). Widok zostaje zawsze, bo jest w nim Interfejs…. Narzędzia ukrytej karty są nadal dostępne w Polecenia… i w skrótach klawiszowych.

Lapin zapamiętuje twój wybór na tym komputerze, dla ciebie. Aplikacja desktopowa i przeglądarka zapamiętują swoje wybory osobno. W przeglądarce Lapin uruchamia się w trybie Kompaktowym, gdy okno ma mniej niż 800 pikseli wysokości, dopóki sam nie wybierzesz.

## Szybkie narzędzia

Szybkie narzędzia to ponumerowane miejsca przy lewej krawędzi widoku, tuż przy przeglądarce projektu, więc przesuwają się razem z nią; małe i półprzezroczyste, dopóki nie najedzie na nie kursor (View → Interface… może zamiast tego umieścić je na dole widoku); uruchamiają je klawisze <kbd>1</kbd>–<kbd>9</kbd>. Każdy obszar roboczy ma własne: na początku takie, jakie daje Twoja edycja, a potem wybierasz je sam:

- przeciągnij narzędzie ze wstążki na miejsce. Narzędzie stoi tylko w jednym miejscu, więc opuszcza poprzednie;
- przeciągnij jedno miejsce na drugie, aby je zamienić;
- kliknij miejsce prawym przyciskiem: **Wybierz narzędzie…**, **Opróżnij to miejsce** lub **Przywróć szybkie narzędzia**;
- **Widok → Szybkie narzędzia…** ustawia dziewięć miejsc z list, a **Jak w edycji** przywraca te z Twojej edycji;
- narzędzie, które wstawia elementy danego typu (ściana, drzwi, strop, schody…), może trzymać jeden z typów projektu: wybierz go w menu miejsca lub obok narzędzia w **Widok → Szybkie narzędzia…**. Miejsce pokazuje wtedy nazwę typu i uruchamia narzędzie z tym typem. Typ jest zapamiętany po nazwie: miejsce działa w każdym projekcie z typem o tej nazwie, a w projekcie bez niego narzędzie bierze swój bieżący typ.

Na dole każde miejsce pokazuje nazwę swojego narzędzia, a puste miejsce za ostatnim czeka na narzędzie. Lapin zapamiętuje Twoje szybkie narzędzia na tym komputerze.

Strzałka na końcu paska ukrywa szybkie narzędzia; w ich miejscu zostaje mały uchwyt **Szybkie narzędzia**, który je przywraca. Ukrywają się osobno w widokach modelu (rzuty, 3D, przekroje) oraz na arkuszach i w zestawieniach, więc możesz je schować na dokumentach i zostawić na rysunkach.

![Szybkie narzędzia: typ ściany na miejscu 3, schody na 7, miejsce 2 puste](shots/quick-tools.webp)

![Widok → Szybkie narzędzia…](shots/quick-tools-dialog.webp)

## Moje narzędzia

**Moje narzędzia** to Twoja własna karta wstążki, pierwsza w rzędzie kart: wkładasz do niej tylko narzędzia, których używasz, wzięte z dowolnej karty, w grupach, które sam nazywasz.

- **Dodawanie narzędzi.** Kliknij prawym przyciskiem narzędzie na wstążce → **Dodaj do Moich narzędzi**, **Dodaj do Moich narzędzi w…** grupie, albo **Dodaj do nowej grupy w Moich narzędziach**. Możesz też przeciągnąć narzędzie z innej karty na kartę **Moje narzędzia**: przytrzymane chwilę nad nią, karta się otwiera, a Ty upuszczasz narzędzie tam, gdzie linia pokazuje, że wyląduje.
- **Porządkowanie w samej wstążce.** Przeciągnij narzędzie przed lub za inne, w jego grupie albo do innej grupy; przeciągnij grupę za jej nazwę, aby przenieść ją między grupami. Linia pokazuje, gdzie wyląduje, pozostałe się odsuwają, a upuszczenie gdzie indziej nic nie zmienia. Dwukrotne kliknięcie nazwy grupy zmienia jej nazwę. Prawy przycisk na narzędziu: **Zrób małe** lub **Zrób duże**, **Przesuń wcześniej**, **Przesuń później** i **Usuń z Moich narzędzi**; prawy przycisk na nazwie grupy: **Zmień nazwę grupy**, **Usuń grupę**, **Nowa grupa** i **Cofnij ostatnią zmianę**. Z klawiatury <kbd>Alt</kbd> <kbd>←</kbd> lub <kbd>→</kbd> przesuwa narzędzie z fokusem, a <kbd>Delete</kbd> je usuwa.
- **Dostosuj Moje narzędzia…** (koło zębate na końcu karty, View → Interface lub Commands…) pokazuje po lewej wszystkie narzędzia aplikacji, z wyszukiwaniem i pogrupowane według kart, a po prawej Twoją kartę. Przeciągaj narzędzia na drugą stronę albo naciśnij **+** (lub <kbd>Enter</kbd>), aby dodać jedno; przeciągaj po prawej stronie, aby ustawić kolejność; przeciągnij narzędzie z powrotem w lewo, aby je wyjąć. Każdy wiersz ma też przyciski przesuwania w górę lub w dół, pokazywania jako duże lub małe narzędzie, listę do przeniesienia do innej grupy i **×** do usunięcia. Z klawiatury <kbd>Alt</kbd> <kbd>↑</kbd> <kbd>↓</kbd> przesuwają narzędzie z fokusem (także poza końce jego grupy), <kbd>Alt</kbd> <kbd>←</kbd> <kbd>→</kbd> przenoszą je do grupy przed lub po, <kbd>Delete</kbd> je usuwa, a <kbd>Ctrl</kbd> <kbd>Z</kbd> cofa ostatnią zmianę. **Cofnij** i **Resetuj** są na dole, więc błędne upuszczenie niczego nie traci.
- **Eksportuj…** i **Importuj…** zapisują kartę jako mały plik, który możesz zabrać na inny komputer lub dać koledze.

Lapin trzyma Moje narzędzia na tym komputerze, dla Ciebie (aplikacja desktopowa i przeglądarka osobno). Narzędzie, którego nie ma Twoja edycja, jest ukryte, nie utracone: wraca wraz ze swoim modułem.

![Moje narzędzia](shots/my-tools-empty.webp)

![Moje narzędzia](shots/my-tools-menu.webp)

![Moje narzędzia: dwie grupy, Ściany i Otwory](shots/my-tools-tab.webp)

![View → Interface → Dostosuj Moje narzędzia…](shots/my-tools-dialog.webp)

## Katalog

Karta **Katalog** w przeglądarce projektu pokazuje typy projektu dla każdego narzędzia, które wstawia elementy z typem: ściany, drzwi, okna, stropy, ściany osłonowe, schody, rampy, sufity i listwy przypodłogowe. Każdy typ jest narysowany w małej skali (warstwy ściany jak w przekroju rzutu, stropu w przekroju, widok drzwi lub okna, profil listwy), z krótkim opisem.

- kliknięcie karty wstawia elementy tego typu: narzędzie startuje z nim;
- przeciągnij kartę na szybkie miejsce, aby zachować tam narzędzie z tym typem;
- **Edytuj typy…** otwiera okno, w którym te typy się tworzy i zmienia.

![Katalog: typy ścian](shots/catalog.webp)

![Typy okien, narysowane według ich widoku](shots/catalog-windows.webp)

## Zwijanie paneli bocznych

W każdym trybie przeglądarka projektu i panel Właściwości zwijają się do wąskiego paska po swojej stronie:

- przycisk w prawym górnym rogu panelu go zwija, a strzałka na jego pasku go rozwija;
- kliknij dwukrotnie krawędź między panelem a widokiem;
- <kbd>Ctrl</kbd> <kbd>B</kbd> zwija lub rozwija przeglądarkę, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> właściwości, a <kbd>Tab</kbd> oba panele.

Kliknij ikonę na pasku zwiniętego panelu, aby otworzyć panel nad widokiem bez rozwijania go. Znika po naciśnięciu <kbd>Esc</kbd> lub kliknięciu gdzie indziej; pinezka utrzymuje go otwartym. Przeciągnij krawędź panelu, aby go poszerzyć; Lapin zapamiętuje szerokość.

![Zwinięta przeglądarka otwarta nad widokiem z jej paska](shots/browser-overlay.webp)

## Panele po obu stronach

**Widok → Panele** (View → Panels) ustawia przeglądarkę projektu i panel właściwości, gdzie chcesz, jak w Revicie: **Przeglądarka po lewej, Właściwości po prawej** (domyślnie), **Przeglądarka po prawej, Właściwości po lewej**, **Oba panele po lewej** lub **Oba panele po prawej**. Gdy oba są po jednej stronie, leżą jeden pod drugim; przeciągnij separator między nimi, aby podzielić wysokość (dwuklik dzieli na pół), a krawędź strony, aby ją poszerzyć. Każdy panel nadal zwija się osobno: zwinięty to wąski pasek w stosie albo szyna, gdy zwinięte są oba. Karta asystenta (komputer) zostaje przy właściwościach. Lapin zapamiętuje układ na tym komputerze.

![Przeglądarka i właściwości jedno pod drugim po lewej](shots/panel-layouts.webp)

## Jasny i ciemny

Księżyc na prawym końcu wstążki przełącza interfejs na ciemny (grafit); słońce z powrotem na jasny (papier). W ciemnym motywie rzuty, przekroje i elewacje leżą na ciemnym tle, a ich czarne pióra rysują jasnym tuszem; arkusze pozostają papierem. Małe rysunki konfiguratorów (rzut, przekrój i elewacja drzwi lub okna, dachu, balkonu, windy, hali, schodów…) oraz podglądy etykiet, tabliczek rysunkowych i materiałów są rysowane na jasnej karcie papieru w obu motywach, aby ich linie, teksty i wymiary czytały się jak na wydrukowanym rysunku.

## Co zaznacza kliknięcie

Kliknięcie, ramka zaznaczenia lub lasso biorą tylko to, co pokazuje widok, w rzutach, przekrojach, elewacjach, widokach na arkuszach i w 3D. Element, który Widoczność / Grafika widoku ukrywa (jego kategoria, podkategoria, filtr), odłożony przez Ukryj / Izoluj lub przez fazę widoku, poza zakresem lub kadrem widoku, na kondygnacji, w łączu lub grupie, które nie są pokazane, albo odcięty przez skrzynkę przekroju, jest pomijany: kliknięcie bierze to, co narysowane pod nim, a element podświetlony pod kursorem to taki, który widać. Z włączonym **Wybór według poziomów zespołu** kliknięcie w całości nadal wchodzi w jej części. Z liniami modelowania zamiast rysunku (przycisk pióra u góry rzutu) można wziąć wszystko na ekranie. Przeglądarka projektu, zestawienie, łącze w panelu właściwości i zależności nadal zaznaczają element po nazwie, ukryty czy nie; panel właściwości mówi wtedy *Niewidoczny w tym widoku*.

## Kilka elementów naraz

Zaznacz kilka elementów (Shift-klik lub ramka), a panel właściwości pokaże ostatnio wybrany, z **N zaznaczonych** obok jego klasy. Wspólna wartość jest pokazana; różniąca się ma postać *różne*. Zmień pole, a każdy zaznaczony element, który je ma, przyjmie nową wartość, w jednym kroku cofanym przez **Cofnij**: wysokość i odsunięcie podstawy ścian, typ każdej zaznaczonej ściany, drzwi lub belki, parapet okien, fazy każdego zaznaczonego elementu bez względu na klasę, warstwę i kolor rysowanych linii, styl tekstów. To, co należy do danego elementu, pozostaje jego: numer pomieszczenia, długość ściany, miejsce drzwi wzdłuż ich ściany.

## Dopasuj właściwości

**Modyfikuj → Dopasuj właściwości** (<kbd>MA</kbd> lub `MATCHPROP` w wierszu poleceń, jak w AutoCAD; w Revit to Match Type) daje innym elementom to, co ma jeden element. Zaznacz element, z którego kopiujesz, albo uruchom polecenie i najpierw go kliknij; potem klikaj każdy element, który ma przejąć jego właściwości. Element pod kursorem podświetla się przed kliknięciem, na rzutach, w przekrojach i w 3D. Każde kliknięcie to krok cofany przez **Cofnij**. **Zastosuj do zaznaczenia** daje je naraz wszystkim pozostałym zaznaczonym elementom. Naciśnij <kbd>Enter</kbd> lub <kbd>Esc</kbd>, gdy skończysz.

Koło zębate na pasku wiersza poleceń pokazuje, co jest przenoszone, a Lapin zapamiętuje wybór: **Typ** (do elementu tej samej klasy), **Wymiary i ustawienia** (wysokość, odsunięcia, materiały, priorytet cięcia, styl tekstu, położenie ościeżnicy i parapet drzwi lub okna; do innej klasy tylko pola, które też ma), **Parametry** (twoje; z zestawu standardowego tylko do tej samej klasy), **Fazy**, **Warstwa, kolor i kreskowanie** (rysowane kształty) oraz **Nazwa** (na początku wyłączona). Położenie elementu, jego kształt i GlobalId nigdy nie są przenoszone. Przypięty element też je przyjmuje: przypięcie trzyma element w miejscu, nie jego ustawienia.

![Dopasuj właściwości na rzucie: ściana, z której się kopiuje, podświetlona ściana pod kursorem i to, co jest przenoszone](shots/match-properties.webp)

## Skróty klawiszowe

**Widok → Skróty klawiszowe…** (<kbd>KS</kbd>) wyświetla, według kart, każde polecenie dostępne z klawiatury. Każdy wiersz pokazuje skróty polecenia.

![Skróty klawiszowe wyszukane po „wall”](shots/shortcuts-dialog.webp)

- **Szukaj** po nazwie polecenia lub po klawiszu.
- **+** dodaje skrót: naciśnij kombinację (<kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>) albo wpisz do trzech liter i naciśnij <kbd>Enter</kbd>. <kbd>Esc</kbd> przerywa.
- Jeśli inne polecenie ma już ten skrót, Lapin mówi które. **Przypisz ponownie** odbiera go tamtemu poleceniu, **Zachowaj oba** zostawia go obu (wygrywa pierwsze na liście), **Anuluj** niczego nie zmienia.
- **×** usuwa skrót. Strzałka na końcu wiersza przywraca temu poleceniu ustawienie z zestawu, a **Przywróć wszystko** robi to dla każdego polecenia.
- **Schemat** wybiera zestaw, od którego zaczynasz: **Styl Revit** (domyślny: typowe litery Revita, tam gdzie Lapin ma to polecenie) lub **Lapin klasyczny** (dawne litery Lapina).
- **Eksportuj…** zapisuje twoje skróty do pliku, a **Importuj…** wczytuje plik, na inny komputer lub dla kolegi.

Podpowiedzi, menu i paleta poleceń (<kbd>Ctrl</kbd> <kbd>K</kbd>) zawsze pokazują skróty w ich obecnej postaci.

### Litery i wiersz poleceń

Litery takie jak <kbd>WA</kbd> działają, gdy żadne pole nie ma fokusu, a wiersz poleceń jest pusty: wpisz je nad widokiem. Polecenie uruchamia się od razu. Gdy jakieś polecenie wiersza poleceń zaczyna się od tych samych liter (<kbd>DI</kbd> i <kbd>DIMADD</kbd>), Lapin chwilę czeka: pisz dalej, aby wpisać dłuższe polecenie, albo naciśnij <kbd>Enter</kbd> lub <kbd>Space</kbd>, aby od razu uruchomić krótsze. Gdy polecenie prosi o punkt lub wartość, litery i cyfry trafiają do niego. Polecenia wiersza poleceń, takie jak <kbd>L</kbd>, <kbd>M</kbd> czy <kbd>TRIM</kbd>, zakończone <kbd>Enter</kbd> działają w każdym zestawie. <kbd>Esc</kbd> zawsze anuluje.

Gdy te dwa się zderzają, wybierasz, jak dzielą klawiaturę:

- **Skróty literowe (jak w Revicie)**, w tym oknie i w **Preferencje → Ogólne → Klawiatura**, jest domyślnie włączone. Wyłącz, aby zostawić tylko wiersz poleceń: każda litera trafia do niego, a <kbd>Enter</kbd> lub <kbd>Spacja</kbd> uruchamia polecenie, Twój alias albo, gdy tekst nie jest żadnym z nich, polecenie o tych literach (<kbd>WA</kbd> <kbd>Enter</kbd> nadal uruchamia ścianę). Kombinacje (<kbd>Ctrl</kbd> + klawisz, klawisze funkcyjne, <kbd>Delete</kbd>) i cyfry szybkich narzędzi działają dalej.
- Klawisz <kbd>`</kbd> (na lewo od <kbd>1</kbd>, rozpoznawany po miejscu na każdym układzie, i skrót jak inne: **Wiersz poleceń: przejmuje całe pisanie**) oddaje wierszowi poleceń całe pisanie, przy włączonych lub wyłączonych literach. Wiersz dostaje ramkę i o tym mówi; żadna litera nie uruchamia narzędzia, żadna cyfra nie wybiera szybkiego narzędzia. <kbd>Enter</kbd> uruchamia wpisane i oddaje pisanie, chyba że polecenie prosi potem o punkt lub wartość: tryb trwa wtedy do końca polecenia. <kbd>Esc</kbd> lub ten sam klawisz go opuszcza.

### Aliasy poleceń

Strona **Aliasy poleceń** w oknie trzyma Twoje własne nazwy poleceń. Wpisz od 1 do 12 liter lub cyfr (pierwsza to litera), znajdź polecenie na liście (polecenie wiersza poleceń, np. <kbd>LINE</kbd>, albo dowolne polecenie wstążki lub aplikacji) i naciśnij **Dodaj alias**. Wpisz alias w wierszu poleceń i naciśnij <kbd>Enter</kbd> lub <kbd>Spację</kbd>. Twoje mają pierwszeństwo przed wbudowanymi nazwami. Strona mówi, co alias zastępuje: wbudowaną nazwę wiersza poleceń, skrót o tych samych literach lub skrót, który każe czekać (przy włączonych literach skrót będący aliasem lub początkiem aliasu czeka chwilę, byś mógł pisać dalej i nacisnąć <kbd>Enter</kbd>). Aliasy zapisują się razem ze skrótami, także w **Eksportuj…** i **Importuj…**.

![Aliasy poleceń, z tym, co alias przesłania](shots/shortcuts-aliases.webp)

### Domyślne skróty

Oto skróty w stylu Revit, w obecnej postaci w Lapinie:

<!-- shortcuts -->

Klawisze cyfr <kbd>1</kbd>–<kbd>9</kbd> wybierają szybkie narzędzia.

## Przesuwanie okna dialogowego

Każde okno dialogowe przesuwa się za pasek tytułu, jak okno: naciśnij tytuł i przeciągnij myszą lub palcem. Zostaje w obrębie okna, otwiera się ponownie tam, gdzie je zostawiono, dopóki program jest otwarty, a dwukrotne kliknięcie tytułu przywraca je na środek. Dzięki temu można odsunąć okno od fragmentu modelu, nad którym się pracuje, albo zobaczyć model za nim.
