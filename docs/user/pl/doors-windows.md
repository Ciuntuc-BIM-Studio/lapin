---
title: Drzwi i okna
order: 30
summary: Wstawiaj drzwi i okna w ściany, przesuwaj je i projektuj ich typy w konfiguratorze.
---

<!-- video doors-windows -->

## Wstawianie drzwi lub okna

Wybierz **Architektura → Drzwi** (<kbd>DR</kbd>) lub **Okno** (<kbd>WN</kbd>), a potem kliknij ścianę tam, gdzie ma być otwór. Lapin wycina otwór w każdej warstwie ściany i dodaje nadproże, którego wymaga typ ściany.

Gdy Drzwi lub Okno jest włączone, panel Właściwości pokazuje typ, który zostanie wstawiony, wraz z wymiarami: wybierz tam inny, zanim klikniesz ścianę.

![Rzut z drzwiami i oknami](shots/openings-plan.webp)

Aby przesunąć drzwi lub okno wzdłuż ściany, zaznacz je i przeciągnij. Aby je zmienić, użyj panelu Właściwości: typ, wysokość parapetu i strona otwierania (**Strona**). Gdy zaznaczonych jest kilka drzwi i okien, **Strona zawiasów**, **Strona**, parapet i typ zmieniają je wszystkie w jednym kroku, podobnie strzałka odwracania na rzucie.

Aby przesunąć o dokładną odległość, użyj **Przesuń** (<kbd>M</kbd>) lub **Kopiuj** (<kbd>CO</kbd>) w wierszu poleceń, jak przy każdym elemencie: zaznacz drzwi, okno lub otwór w ścianie, kliknij punkt bazowy, a potem wpisz odległość (`500`) albo kliknij drugi punkt. Liczy się tylko część przesunięcia wzdłuż ściany: otwór przesuwa się wzdłuż swojej ściany i zachowuje parapet. Przesunięty do innej ściany na tej samej kondygnacji przechodzi do tej ściany i nadal otwiera się tak samo do pomieszczenia. Kopia to nowe drzwi lub nowe okno tego samego typu z kolejnym numerem. Jeśli się nie mieści (za końcem ściany albo na innym otworze), nic się nie przesuwa. **Obróć** i **Lustro** umieszczają go tak samo, a odbicie lustrzane zmienia stronę zawiasów.

![Właściwości drzwi](shots/door-properties.webp)

## Otwory bez drzwi i okien

**Architektura → Otwór w ścianie** (<kbd>WO</kbd>) wycina sam otwór w ścianie tam, gdzie klikniesz: przejście, właz, otwór na drzwi, które pojawią się później. Ustaw jego szerokość, wysokość i parapet w panelu Właściwości przed kliknięciem. Po zaznaczeniu zmienisz tam jego wymiary, położenie wzdłuż ściany i parapet; tak jak drzwi, musi mieścić się w ścianie i nie wchodzić na inne otwory ściany. W IFC jest to otwór drążący ścianę, niczym niewypełniony.

Zaznaczony otwór ma uchwyty jak okno: przeciągnij jego środek, aby przesunąć go wzdłuż ściany, albo ościeże, aby go poszerzyć lub zwęzić, podczas gdy drugie zostaje; długość przy myszy można wpisać, jak przy rysowaniu. Wybiera się go kliknięciem w jego przerwę na rzucie lub w jego otwór w widoku 3D, gdzie podświetla się jego obrys. **Przesuń** (<kbd>M</kbd>) przesuwa go wzdłuż ściany o wpisaną odległość, a jego wysokość i parapet są w panelu właściwości.

**Zwieńczenie** kształtuje jego górę: **Prostokąt**, **Łuk odcinkowy** (płaski łuk), **Łuk eliptyczny** (połowa elipsy, o wysokości jednej czwartej szerokości, chyba że podasz inaczej), **Półkole** lub **Zaokrąglone narożniki** (płaskie nadproże z zaokrąglonymi górnymi narożnikami). **Strzałka zwieńczenia** ustala, jak wysoko wznosi się łuk odcinkowy lub eliptyczny, a **Promień narożników**, jak bardzo zaokrąglone są narożniki (150 mm, chyba że podasz inaczej, nigdy więcej niż połowa szerokości); puste pole oznacza wartość domyślną danego rodzaju. Otwór w ścianie podąża za tym obrysem w 3D, na elewacjach i przekrojach oraz w IFC; rzut pokazuje jego ościeża jak dotąd.

## Konfigurator

**Architektura → Drzwi i okna** otwiera konfigurator. Po lewej wymienione są wszystkie typy drzwi i okien projektu. Możesz zmienić:

- nazwę i oznaczenie typu (literę w etykietach);
- jego szerokość i wysokość;
- głębokość i szerokość ościeżnicy;
- kolumny i rzędy skrzydeł oraz sposób otwierania każdego skrzydła;
- wykończenie ościeża, opaskę na elewacji, próg i kolory.

Rysunki po prawej pokazują typ tak, jak zostanie narysowany na elewacji, rzucie i przekroju, na bieżąco podczas zmian. Pod nimi **podgląd 3D** pokazuje typ osadzony w kawałku ściany pierwszego typu ściany projektu, z ościeżem, parapetami, obłożeniem i materiałami, na bieżąco podczas zmian (także okno wykuszowe, łukowe lub skrzynkowe): przeciągnij, aby obrócić, kółkiem powiększ, przeciągnij prawym przyciskiem, aby przesunąć. Na początku widać go od zewnątrz.

**Lewe i prawe.** Kierunek otwierania drzwi nazywa się tak, jak nazywają go architekci: patrz od klamki w stronę zawiasów; skrzydło, które otwiera się na lewo od tej linii, jest lewe, a które otwiera się na prawo, prawe. Wybory konfiguratora dla skrzydła mówią to tak samo, ze stroną zawiasów w polu **Otwieranie** kwatery, widzianej od zewnątrz, od strony, z której pcha się skrzydła: *Otwierane w prawo (zawiasy po lewej)*. Plik IFC nazywa stronę po swojemu (zawiasy widziane w kierunku, w którym otwiera się skrzydło), więc te same drzwi są tam zapisane jako SINGLE_SWING_LEFT; Lapin przekłada jedno na drugie przy zapisie i odczycie IFC. Lewe i prawe okna ocenia się od wewnątrz, od strony, w którą otwierają się skrzydła: *Rozwierne w prawo (zawiasy po lewej)* w konfiguratorze, którego kwatery są widziane od zewnątrz. IFC zapisuje to samo okno jako lewe. Widok jest od wewnątrz, od strony, w którą otwierają się skrzydła, chyba że zaznaczone jest **Od zewnątrz**; tak jak rzut pokazuje drzwi takie, jakie stoją w modelu. Drzwi odwrócone na drugą stronę (**Strona zawiasów** albo strzałka odwracania wzdłuż ściany) są lustrzanym odbiciem swojego typu; zestawienia pokazują je na osobnej karcie (zobacz [Zestawienia](schedules.md)).

![Konfigurator drzwi i okien](shots/joinery-configurator.webp)

**Zapisz typ** zachowuje zmiany. Wszystkie drzwi lub okna tego typu się do nich stosują. **Umieść tym typem** zamyka konfigurator i zaczyna wstawianie tego typu.

### Kwatery, słupki i profile

Drzwi lub okno to siatka kwater. Każda kwatera wybiera, jak się otwiera i czym jest wypełniona, więc jeden typ może mieć dwa skrzydła z doświetlami bocznymi i naświetlem nad nimi albo okno z pełnym panelem pod skrzydłem.

**Kolumny (względne)** i **Rzędy (względne)** szybko dzielą otwór: `1 1` to dwie równe kwatery, `1 1 1` trzy, `2 1` szeroka obok wąskiej. Rzędy liczy się od dołu.

**Szerokości kolumn** i **Wysokości rzędów** podają te same wymiary w milimetrach. Wpisz je, a ostatnia weźmie resztę. W widoku możesz też przeciągnąć słupek lub rygiel; odległość widać podczas przeciągania.

**Szerokość słupka** i **Szerokość rygla** to osobne wymiary, niezależne od pola **Szerokość ramy**. Rama 50 mm ze słupkami 50 mm wygląda wszędzie na 50 mm.

**Kwatery** wymienia każdą kwaterę. Kliknij jedną, na liście lub w widoku, aby ją edytować:

- **Otwieranie**: stała, skrzydło (rozwierne, rozwierno-uchylne, uchylne, odchylane górą, obrotowe, przesuwne) albo, w drzwiach, skrzydło drzwiowe.
- **Wypełnienie**: **Szkło** lub **Panel pełny**. Skrzydła i doświetla boczne drzwi mogą być pełne, tak samo kwatera okna (panel pod skrzydłem, nieprzezroczyste pole).
- **Podziel**: **Na kolumny** lub **Na rzędy**, w odległości w milimetrach od lewej lub dolnej krawędzi kwatery (domyślnie w połowie). Podzielone w poprzek skrzydło drzwi zachowuje dolną część, a górna staje się stałym przeszklonym naświetlem.
- **Scal**: z kwaterą obok, powyżej lub poniżej, gdy mają wspólną całą krawędź. Słupki i rygle, których żadna kwatera już nie potrzebuje, znikają.

![Drzwi sklepowe w jednym typie: dwa skrzydła, doświetla boczne i naświetle na całą szerokość](shots/joinery-panes.webp)

**System profili** zaczyna typ od profilu **PVC**, **Aluminium**, **Tarcica** (drewno) lub **Stal (smukła)**: ustala wymiary ościeżnicy, słupków, rygli i skrzydeł i nadaje profilom dokładniejsze kształty. Ościeżnica ma przylgę (**Przylga**), do której domyka się skrzydło, skrzydło zachodzi na ościeżnicę o **Zakład skrzydła**, **listwy przyszybowe** trzymają szybę, a okno ma okapnik pod dolnym ramiakiem. Każdy wymiar pozostaje edytowalny. **Prosty (gładkie profile)** zachowuje proste prostokątne elementy, jak dotąd.

![Okno PVC: skrzydła zachodzące na ościeżnicę, listwy przyszybowe, pełny panel pod stałą kwaterą](shots/joinery-profiles-3d.webp)

W sekcji **Szczegóły** pola **Parapet wewnętrzny** i **Parapet zewnętrzny** włączają i wyłączają parapety, a **Obróbka ościeży** włącza wykończenie ościeża po bokach i u góry. Gdzie parapety i obróbki nie są potrzebne, odznacz je.

### Okiennice, kraty, skrzynki kwiatowe i daszki

<!-- video window-dressings -->

W sekcji **Na elewacji** typ okna może mieć:

- **Okiennice**: żaluzjowe, płycinowe lub z desek, jedno- lub dwuskrzydłowe, pokazane otwarte (złożone przy ścianie obok okna) lub zamknięte na nim;
- **Krata zabezpieczająca**: w ościeżu od strony pomieszczenia (domyślnie, tam zwykle montuje się kraty), w ościeżu od strony ulicy, na elewacji lub na licu wewnętrznym, z prętami poziomymi, pionowymi lub obydwoma, z ich wymiarem i rozstawem;
- **Skrzynka na kwiaty** pod oknem;
- **Daszek** nad nim, z wysięgiem i spadkiem.

Każdy z nich przylega do rzeczywistych lic ściany w miejscu okna, wyznaczonych z warstw ściany, więc pasuje do każdego typu ściany. Gdy okno ma opaskę, to, co jest na elewacji, odsuwa się od opaski. Każdy możesz przesunąć ręcznie: **Od lica ściany** ustala odsunięcie od ściany (dla kraty w ościeżu **Cofnięta od lica** ustala, jak głęboko siedzi), **Od osi okna** przesuwa go wzdłuż ściany, a **W górę lub w dół** przesuwa go na wysokość. Są widoczne w 3D, na rzutach, elewacjach i przekrojach, a zestawienia je mierzą.

![Okiennice, krata, skrzynka kwiatowa i daszek w konfiguratorze](shots/joinery-dressings.webp)

![Okna z dodatkami w 3D](shots/window-dressings-3d.webp)

### Kształty, style i ornamenty

**Kształt** nadaje drzwiom lub oknu łukowe zwieńczenie (**Łuk półkolisty**, **Łuk odcinkowy**, **Łuk eliptyczny**, **Zaokrąglone narożniki**, **Łuk ostry**, **Zwieńczenie trójkątne**) albo robi z niego **Okrągłe lub owalne** (okrągłe, gdy szerokość i wysokość są równe). Typ łukowy ma kwatery pod ślemieniem na wysokości wezgłowia łuku, a nad nim stały naświetlnik, który podąża za łukiem, ze szprosami, jeśli chcesz: **Promieniste**, **Słupki przedłużone w górę** lub **Krzyż**. **Strzałka zwieńczenia** ustala, jak wysoko wznosi się łuk odcinkowy, eliptyczny, ostry lub trójkątny, a **Promień narożników**, jak bardzo zaokrąglone są narożniki zaokrąglonego zwieńczenia. Otwór w ścianie, ościeżnica, szyba, opaska i rysunki podążają za obrysem.

**Ślemię u nasady łuku**, zaznaczone domyślnie, zostawia to ślemię i stały naświetlnik nad nim. Odznacz je dla okna rozwiernego łukowego: górny rząd kwater wchodzi w łuk, a ościeżnica, skrzydło, szyba, szprosy i symbol otwierania biegną wzdłuż łuku dookoła, bez poprzeczki pod nim. Kwatera otwiera się tak, jak wybierzesz (rozwierne, rozwierno-uchylne, stałe), słupki dochodzą do łuku, a rzuty, przekroje, elewacje, podgląd 3D i IFC pokazują to w ten sposób. Styl **Okno rozwierne łukowe** ustawia od razu łuk odcinkowy z jednym skrzydłem rozwiernym.

![Okno rozwierne łukowe: jedno skrzydło rozwierne biegnące wzdłuż łuku odcinkowego, bez ślemienia pod nim](shots/joinery-arched-casement.webp)

**Szprosy** dzielą kwatery: **Siatka (kolonialna)** z polami w poziomie i pionie, **Prairie** ze szprosami przy krawędziach lub **Romby**.

Na elewacji, oprócz opaski, okno może mieć **Zwornik**, **Zwieńczenie nad otworem** (**Gzyms**, **Naczółek**, **Naczółek łukowy** lub **Kapinos**, który podąża za łukiem) oraz **Płycina pod parapetem** z konsolami.

**Zastosuj styl…** ustawia naraz kształt, szprosy i ornamenty: nowoczesny, klasyczny, georgiański, wiktoriański, romański, barokowy, gotycki, Tudor, Prairie, tradycyjny lub okno okrągłe. Wszystko, co ustawia, pozostaje edytowalne. Lista pokazuje potem styl, który ma typ; gdy zmienisz coś, co ustawił styl, widać *Georgiański (zmieniony)*, a ponowny wybór stylu stosuje go ponownie.

![Okno barokowe: łuk pełny ze szprosami promienistymi, zwornik i naczółek odcinkowy](shots/joinery-styles.webp)

![Okna łukowe w 3D](shots/window-styles-3d.webp)

### Okna wykuszowe, łukowe i skrzynkowe

Typ okna może wystawać ze ściany. W konfiguratorze **Projects** czyni go **Bay** (środek i dwa skośne boki), **Bow** (równe pola na łuku) lub **Box** (boki prostopadłe do ściany), a **New bay window** zaczyna nowy typ jako wykusz 45°. Okno wciąż wypełnia jeden otwór w ścianie, tak szeroki i wysoki jak typ: wstawia się je, przesuwa, kopiuje, odwraca, numeruje i zestawia jak każde okno. **Architecture → Bay window** (<kbd>BW</kbd>) wstawia je w klikniętą ścianę: typ wybrany w konfiguratorze, gdy wystaje, inaczej pierwszy wykusz projektu, inaczej wykusz 45°.

Kolumny siatki to jego ściany. W wykuszu i skrzynce pierwsza i ostatnia kolumna to boki, kolumny pomiędzy to środek; okno łukowe ma jedną ścianę na kolumnę, ich szerokości dzielą łuk. Każda kwatera otwiera się tak, jak mówi jej **Otwieranie**, więc boki mogą się otwierać, a środek być stały. Dalej:

- **Projection**, od zewnętrznego lica ściany do zewnętrznego lica ościeżnic, a w wykuszu kąt **Sides to the wall** (od 15° do 90°);
- **Sides**: boki skrzynki przeszklone lub pełne, z materiałem;
- **Seat inside**: siedzisko przy parapecie, jak daleko wchodzi do pomieszczenia, jego materiał;
- **Sill outside** pod ścianami;
- **Base**: brak (zawieszony na ścianie), **ściany do podłogi**, **płyta wspornikowa** lub **płyta na wspornikach**, z grubością, wysięgiem i materiałem;
- **Head**: dach **czterospadowy**, **jednospadowy** lub **płaski** albo **płyta**, grubość przy okapie, wysięg, spadek i materiał, oraz **obróbka blacharska przy ścianie**.

Ościeżnice, skrzydła, szkło i klamki są typu, więc jego materiały i klamki działają na wszystkich ścianach. **Bay style** ustawia wszystko naraz: wykusz 45° lub płytki 30°, wykusz od podłogi, okno łukowe z pięciu lub czterech pól, skrzynkę z bokami przeszklonymi lub pełnymi, wykusz na wspornikach. Jak przy **Zastosuj styl…**, lista pokazuje styl wykuszu, który ma okno, z *(zmieniony)*, gdy zmienisz to, co ustawił. **Save as bay style…** zachowuje skonfigurowany jako styl projektu, obok stylów Lapina; zmienia się nazwę lub usuwa go na liście pod polami.

W rzucie ściany są przecięte, siedzisko, parapet i podstawa rysowane poniżej cięcia, a dach przerywaną linią nad nimi; widoki, przekroje i 3D pokazują całe ciało. Zestawienie okien ma kartę typu z rozwiniętymi ścianami i wierszem **Projecting** (rodzaj, wysięg, ściany); jego powierzchnia to powierzchnia przeszklonych ścian, a szczegóły mierzą dach, siedzisko i podstawę. W IFC to jedno IfcWindow z podziałem zdefiniowanym przez użytkownika (*Bay 45°, projection 600, 3 faces, 4 panels*), którego ciałem jest cały wykusz, pola ściana po ścianie; Lapin odczytuje wykusz z powrotem.

![Okno łukowe w konfiguratorze](shots/bay-configurator.webp)

![Wykusz, okno łukowe, skrzynka i wykusz na wspornikach w rzucie](shots/bay-plan.webp)

![Wykusz, okno łukowe, skrzynka i wykusz na wspornikach w 3D](shots/bay-3d.webp)

### Klamki

**Klamka** wybiera klamkę typu: **Klamka**, **Gałka**, **Pochwyt** lub **Klamka okienna** albo **Brak**. Domyślnie drzwi mają klamkę dźwigniową, a okno klamkę okienną. Drzwi mają klamkę po obu stronach, na wysokości z pola **Wysokość klamki**; **Kolor klamki** ustala jej kolor. Klamki są widoczne w 3D, na przekrojach i elewacjach oraz na rysunkach konfiguratora, gdy zaznaczone jest **Okucia**.

### Parametry

**Parametry · w zestawieniach**, u dołu konfiguratora, zawiera własne parametry typu: **+ Odporność ogniowa** (zapisywana w Pset_DoorCommon lub Pset_WindowCommon IFC jako FireRating), **+ Producent**, **+ Kod produktu**, **+ Strona internetowa** lub dowolny parametr, który nazwiesz. Drzwi i okna tego typu je dziedziczą, a każdy staje się wierszem zestawień stolarki: zobacz [Zestawienia i wykresy](schedules.md).

## Osłony przeciwsłoneczne

**Architecture → Sunshade** (<kbd>SN</kbd>) wstawia osłonę na kliknięte drzwi lub okno: na wymiar otworu, poza węgarki, nad nadprożem lub przed nim, i podąża za nim, gdy się przesuwa. **Sunshade along a facade** bierze dwa punkty: na ścianie biegnie wzdłuż jej zewnętrznego lica, gdzie indziej wzdłuż linii, w prawo. Pasek obu narzędzi wybiera typ lub styl, z którego powstaje nowy typ.

Rodzaje:

- **Canopy** (daszek): płyta, lamelowy lub szklany, z wysięgiem, grubością, nachyleniem i pasem czołowym, na wspornikach, cięgnach lub słupkach;
- **Vertical fins**: głębokość, grubość, rozstaw i kąt, rygle górą i dołem;
- **Louvres (brise-soleil)**: poziome lamele, płaskie, lotnicze lub skrzynkowe, głębokość, rozstaw i kąt, odsunięte od ściany, między płytami czołowymi;
- **Egg-crate**: płyty poziome i pionowe razem;
- **Perforated screen**: blacha odsunięta od ściany, z otworami okrągłymi, kwadratowymi lub szczelinami, ich rozmiar i podziałka;
- **Awning** (markiza): tkanina w spadku, falbana, kaseta i ramiona łamane;
- **Pergola**: krokwie, belka, listwy na górze i słupki do podłogi;
- **External venetian blind**: lamelki, jak nisko opuszczona, kaseta i prowadnice.

**Sunshade types** (w Types) edytuje typ: rodzaj i każdy parametr, materiały, obok rysunku z zewnątrz, w przekroju i w rzucie. Każda osłona typu podąża za zmianą. Nowy typ zaczyna się od stylu: daszek betonowy, drewniany na wspornikach, szklany na cięgnach, lamelowy, z dachówki, brise-soleil, żebra pionowe, kratownica betonowa, ekran perforowany z Cortenu, markiza materiałowa, pergola drewniana, żaluzja zewnętrzna; **Save as style…** zachowuje typ jako styl projektu.

![Właściwości daszka po obliczeniu dla południa](shots/sunshade-properties.webp)

Zaznaczona osłona pokazuje typ, na czym stoi i własne **Projection** (puste: typu). **Full shade at noon, 21 June** oblicza dla daszka, markizy lub pergoli nad oknem, jak daleko ma wystawać, by całe okno było w cieniu, gdy słońce stoi najwyżej w najdłuższym dniu, w miejscu projektu (**Manage → Location**; Bukareszt, dopóki nie ustawiono), i to ustawia. Na elewacji południowej na 45° N słońce stoi wtedy na 68,4°: okno o wysokości 1,5 m pod daszkiem 150 mm nad nadprożem wymaga 650 mm. Jej uchwyty przeciągają końce wzdłuż ściany lub linii oraz wysięg.

![Typy osłon: lamele, z zewnątrz, w przekroju i w rzucie](shots/sunshade-types.webp)

Rzuty rysują osłonę nad cięciem linią przerywaną, przeciętą tę, która je przecina (żebra, ekran), a widoczną tę poniżej; widoki, przekroje i 3D pokazują jej ciało z kreskowaniem materiału, i rzuca cień razem z resztą (**View → Sun and shadows**). **Sunshade schedule** zestawia je według typów z miejscem, długością i wysięgiem, liczbą i zacienioną powierzchnią; ich ilości (długość, wysięg, powierzchnia, objętość) biorą pozycje kosztorysowe typu. W IFC są to IfcShadingDevice typu IfcShadingDeviceType: markiza AWNING, żaluzja JALOUSIE, lamele LOUVER w IFC 4.3 (zdefiniowane przez użytkownika w IFC4), pozostałe zdefiniowane przez użytkownika (Canopy, Fins, Egg-crate, Screen, Pergola); Lapin odczytuje je z powrotem takie, jakie były.

![Osłony w rzucie](shots/sunshades-plan.webp)

![Daszek, lamele, żebra, markiza, pergola i żaluzja w 3D](shots/sunshades-3d.webp)

## Etykiety na rzucie

**Opisz → Etykiety drzwi i okien** stawia znacznik na każdych drzwiach i oknie rzutu: linię w poprzek ściany, oznaczenie na zewnątrz, a wymiary i parapet wewnątrz. Aby zmienić wygląd etykiet, przejdź do **Zarządzaj → Standardy rysunkowe → Etykiety**.

### Edycja znacznika na ekranie

Każdy znacznik można ułożyć ręcznie, część po części. Kliknij jego linię lub jeden z tekstów, aby go zaznaczyć: każdy tekst ma wtedy kwadratowy uchwyt (przeciągnij, aby przesunąć tekst) i okrągły za jego końcem (przeciągnij, aby obrócić tekst), a każdy koniec linii ma uchwyt (przeciągnij, aby wydłużyć lub skrócić linię; tekst przy tym końcu idzie razem z nim). Ctrl/⌘-klik na uchwycie przywraca tę część. Każde przeciągnięcie to jeden krok cofania. Gdy zaznaczonych jest kilka znaczników tego samego rzutu, część przeciągnięta na jednym zmienia tę samą część we wszystkich.

![Zaznaczony znacznik drzwi: uchwyt dla każdego tekstu, jego uchwyt obrotu i po jednym na każdym końcu linii](shots/marker-grips.webp)

![Oznaczenie przesunięte i obrócone, zewnętrzny koniec linii wydłużony](shots/marker-edited.webp)

Zmiany należą do znacznika, a więc do tego rzutu: te same drzwi mogą być ułożone inaczej na innym rzucie. Są zapisane wzdłuż i w poprzek ściany, więc podążają za drzwiami, gdy się przesuwają, obracają lub przechodzą na drugą stronę. We właściwościach znacznika **Znacznik** wymienia jego części: wpisz obrót tekstu w stopniach lub o ile sięga koniec w milimetrach na papierze, przywróć jedną część przez ↺ lub wszystkie przez **Przywróć etykietę na miejsce**. Silnik rysuje znaczniki tak, jak je zmieniono, więc arkusze, PDF i DXF drukują je tak samo.

![Części znacznika w jego właściwościach](shots/marker-properties.webp)

Czy teksty leżą w poprzek linii znacznika (wzdłuż ściany, domyślnie), czy wzdłuż niej, ustala się dla każdego typu etykiety w **Zarządzaj → Standardy rysunkowe → Etykiety**, kolumna **Kierunek tekstu**: **prostopadle do linii etykiety** lub **wzdłuż linii etykiety**. W obu przypadkach są rysowane czytelnie, nigdy do góry nogami; ręcznie obrócony tekst też pozostaje czytelny.

![Teksty znacznika drzwi wzdłuż linii etykiety](shots/marker-along.webp)
