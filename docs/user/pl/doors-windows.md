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

Aby przesunąć drzwi lub okno wzdłuż ściany, zaznacz je i przeciągnij. Aby je zmienić, użyj panelu Właściwości: typ, wysokość parapetu i strona otwierania (**Strona**).

![Właściwości drzwi](shots/door-properties.webp)

## Otwory bez drzwi i okien

**Architektura → Otwór w ścianie** (<kbd>WO</kbd>) wycina sam otwór w ścianie tam, gdzie klikniesz: przejście, właz, otwór na drzwi, które pojawią się później. Ustaw jego szerokość, wysokość i parapet w panelu Właściwości przed kliknięciem. Po zaznaczeniu zmienisz tam jego wymiary, położenie wzdłuż ściany i parapet; tak jak drzwi, musi mieścić się w ścianie i nie wchodzić na inne otwory ściany. W IFC jest to otwór drążący ścianę, niczym niewypełniony.

**Zwieńczenie** kształtuje jego górę: **Prostokąt**, **Łuk odcinkowy** (płaski łuk), **Łuk eliptyczny** (połowa elipsy, o wysokości jednej czwartej szerokości, chyba że podasz inaczej), **Półkole** lub **Zaokrąglone narożniki** (płaskie nadproże z zaokrąglonymi górnymi narożnikami). **Strzałka zwieńczenia** ustala, jak wysoko wznosi się łuk odcinkowy lub eliptyczny, a **Promień narożników**, jak bardzo zaokrąglone są narożniki (150 mm, chyba że podasz inaczej, nigdy więcej niż połowa szerokości); puste pole oznacza wartość domyślną danego rodzaju. Otwór w ścianie podąża za tym obrysem w 3D, na elewacjach i przekrojach oraz w IFC; rzut pokazuje jego ościeża jak dotąd.

## Konfigurator

**Architektura → Drzwi i okna** otwiera konfigurator. Po lewej wymienione są wszystkie typy drzwi i okien projektu. Możesz zmienić:

- nazwę i oznaczenie typu (literę w etykietach);
- jego szerokość i wysokość;
- głębokość i szerokość ościeżnicy;
- kolumny i rzędy skrzydeł oraz sposób otwierania każdego skrzydła;
- wykończenie ościeża, opaskę na elewacji, próg i kolory.

Rysunki po prawej pokazują typ tak, jak zostanie narysowany na elewacji, rzucie i przekroju, na bieżąco podczas zmian.

![Konfigurator drzwi i okien](shots/joinery-configurator.webp)

**Zapisz typ** zachowuje zmiany. Wszystkie drzwi lub okna tego typu się do nich stosują. **Umieść tym typem** zamyka konfigurator i zaczyna wstawianie tego typu.

### Okna z większą liczbą kwater

**Kolumny kwater** przyjmuje względne szerokości kwater, jedną liczbę na każdą: `1 1` to dwie równe kwatery, `1 1 1` trzy, `1 2 1` szeroka między dwiema wąskimi. **Rzędy kwater** dzieli okno na wysokość w ten sam sposób, od dołu: `1 1` daje dwa rzędy. Sześć kwater to `1 1 1` w Kolumnach kwater i `1 1` w Rzędach kwater. Następnie w polu **Panele** wybierz, jak otwiera się każda kwatera: stała, rozwierana, rozwierno-uchylna, uchylna, odchylana górą, obrotowa lub przesuwna.

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

**Szprosy** dzielą kwatery: **Siatka (kolonialna)** z polami w poziomie i pionie, **Prairie** ze szprosami przy krawędziach lub **Romby**.

Na elewacji, oprócz opaski, okno może mieć **Zwornik**, **Zwieńczenie nad otworem** (**Gzyms**, **Naczółek**, **Naczółek łukowy** lub **Kapinos**, który podąża za łukiem) oraz **Płycina pod parapetem** z konsolami.

**Zastosuj styl…** ustawia naraz kształt, szprosy i ornamenty: nowoczesny, klasyczny, georgiański, wiktoriański, romański, barokowy, gotycki, Tudor, Prairie, tradycyjny lub okno okrągłe. Wszystko, co ustawia, pozostaje edytowalne.

![Okno barokowe: łuk pełny ze szprosami promienistymi, zwornik i naczółek odcinkowy](shots/joinery-styles.webp)

![Okna łukowe w 3D](shots/window-styles-3d.webp)

### Klamki

**Klamka** wybiera klamkę typu: **Klamka**, **Gałka**, **Pochwyt** lub **Klamka okienna** albo **Brak**. Domyślnie drzwi mają klamkę dźwigniową, a okno klamkę okienną. Drzwi mają klamkę po obu stronach, na wysokości z pola **Wysokość klamki**; **Kolor klamki** ustala jej kolor. Klamki są widoczne w 3D, na przekrojach i elewacjach oraz na rysunkach konfiguratora, gdy zaznaczone jest **Okucia**.

### Parametry

**Parametry · w zestawieniach**, u dołu konfiguratora, zawiera własne parametry typu: **+ Odporność ogniowa** (zapisywana w Pset_DoorCommon lub Pset_WindowCommon IFC jako FireRating), **+ Producent**, **+ Kod produktu**, **+ Strona internetowa** lub dowolny parametr, który nazwiesz. Drzwi i okna tego typu je dziedziczą, a każdy staje się wierszem zestawień stolarki: zobacz [Zestawienia i wykresy](schedules.md).

## Etykiety na rzucie

**Opisz → Etykiety drzwi i okien** stawia znacznik na każdych drzwiach i oknie rzutu: linię w poprzek ściany, oznaczenie na zewnątrz, a wymiary i parapet wewnątrz. Aby zmienić wygląd etykiet, przejdź do **Zarządzaj → Standardy rysunkowe → Etykiety**.
