---
title: Stropy i dachy
order: 40
summary: Rysuj stropy i dachy po ich obrysie, wybieraj kształt i krawędzie dachu, wycinaj otwory.
---

## Stropy

Wybierz **Architektura → Strop** (<kbd>SB</kbd>), a następnie kliknij narożniki jego obrysu. Zamknij obrys na pierwszym punkcie albo naciśnij <kbd>Enter</kbd>. Strop również składa się z warstw, ustawianych w **Architektura → Typy stropów**. Warstwa typu stropu może mieć grubość 0 (hydroizolacja, farba): niczego nie buduje, na przekrojach jest rysowana linią i liczona powierzchnią stropu.

Aby zmienić obrys, zaznacz strop i przeciągnij jego narożniki. **Otwór w stropie** (<kbd>SH</kbd>) wycina w stropie otwór na schody lub szyb.

Strop narysowany poza ścianami, przed drzwiami, to ganek. Schody prowadzące na niego opisuje strona [Schody](stairs.md).

## Dachy

Wybierz **Architektura → Dach** (<kbd>RF</kbd>) i kliknij obrys ścian pod nim. Lapin buduje dach nad tym obrysem, na wysokości szczytu jego poziomu.

![Dom z dachem dwuspadowym w widoku 3D](shots/house-3d.webp)

Zaznacz dach, aby zmienić go w panelu Właściwości:

- **Kształt.** Czterospadowy, dwuspadowy, jednospadowy, mansardowy, kolebkowy lub płaski. Dach kolebkowy to zakrzywione sklepienie między dwiema ścianami szczytowymi; **Kolebka: strzałka** ustala, jak wysoko wznosi się w kluczu, najwyżej do połowy rozpiętości.
- **Nachylenie i okap** dla całego dachu.
- **Kierunek kalenicy (dwuspadowy, jednospadowy)**: auto, x lub y. Kalenicą dachu jednospadowego jest jego wyższa krawędź: przy x opada w stronę −y, przy y w stronę −x; **Jednospadowy: odwróć spadek** sprawia, że opada w drugą stronę.
- **Każda krawędź** z własnym rodzajem (okap lub szczyt), nachyleniem i wysunięciem. Dach mansardowy z krawędziami szczytowymi to dach polski (łamany dwuspadowy).
- **Styl.** Style dachów nadają cały wygląd jednym wyborem: pokrycie, okapy, rynny i rury spustowe.
- **Materiał pokrycia.** Pokrycie jest z materiału projektu, domyślnie z materiału wybranego pokrycia (Clay tile, Metal sheet, Slate…), tworzonego w kolorze pokrycia, gdy dach po raz pierwszy go potrzebuje. Wybierz inny we właściwościach lub w **Pokrycie i odwodnienie** konfiguratora; zmiana pokrycia zmienia i jego, chyba że wybrałeś własny. Elewacje i przekroje kreskują pokrycie według tego materiału (dachówka, gont i łupek w poziomych rzędach, blacha w pionowych rąbkach, o ile nie nadasz mu własnego kreskowania **Elevation**), przekrój rysuje przecięte pokrycie kreskowaniem przekroju materiału, a IFC, przedmiar i kosztorys podają pokrycie pod jego materiałem.

![Właściwości dachu](shots/roof-properties.webp)

**Konfigurator dachu** (przycisk obok stylu albo **Architektura → Konfigurator dachu** przy zaznaczonym dachu) zbiera wszystko w jednym miejscu: style po lewej, następnie kształt i jego krawędzie, pokrycie i odwodnienie, więźbę i otwory, nad rzutem dachu (kliknij krawędź, aby ustawić ją jako auto, okap lub szczyt) oraz, dla więźby, przekrojem poprzecznym przez kalenicę z wiązarem lub parą krokwi pośrodku. Wybierz styl na początek i zmień dowolne pole ręcznie; **Zastosuj** robi z tego jeden krok cofania. **Zapisz jako styl…** zachowuje skonfigurowany dach jako styl projektu, wymieniony w grupie **Projekt** nad stylami z katalogu (i na liście w panelu Właściwości) dla innych i nowych dachów; **Aktualizuj styl** ponownie zapisuje w nim zmiany, a każdy dach tego stylu za nimi podąża. Stylowi projektu można zmienić nazwę albo go usunąć; jego dachy zachowują swój wygląd.

![Konfigurator dachu](shots/roof-configurator.webp)

**Rynny i rury spustowe.** W sekcji konfiguratora **Pokrycie i odwodnienie** rynna otrzymuje rozmiar, wysunięcie poza okap i odległość, na jaką wisi pod pokryciem, spadek do wylotów (mm/m) oraz denka; tabela krawędzi włącza lub wyłącza rynnę każdego okapu. Rury spustowe początkowo należą do dachu, jedna co 10 m; **Uczyń je edytowalnymi** zamienia je w elementy, a **Architektura → Rura spustowa** dodaje jedną kliknięciem na rynnie. Uchwyt rury spustowej przesuwa ją tylko wzdłuż jej rynny; jej właściwości ustalają średnicę, kolano wylotowe u dołu i przebieg, czyli łańcuch odcinków, każdy z długością i kątem od pionu (w stronę ściany), przy czym ostatni schodzi do gruntu. W IFC rynna to IfcPipeSegment GUTTER, a rura spustowa to odcinek sztywny o nazwie Downpipe.

Ściany na poziomie dachu zatrzymują się pod nim i same dochodzą do jego szczytów, także pod dachem zakrzywionym. Ściana na innym poziomie podąża za dachem po użyciu **Architektura → Dołącz do dachu** (<kbd>AR</kbd>): zaznacz ściany, a następnie kliknij dach. To samo narzędzie przyjmuje każdą inną bryłę nad ścianami: bryłę Studio, płytę, belkę, element zaimportowany z IFC. Ściany dochodzą do jej spodu i są wzdłuż niego przycinane, niezależnie od jej kształtu. **Odłącz od dachu** to cofa.

**Okno dachowe** i **Lukarna** dodają otwory w dachu: kliknij na połaci. Należą one do dachu. Zaznacz dach, a każde z nich pokaże na rzucie swoje uchwyty. Przeciągnij uchwyt pośrodku, aby przesunąć je po dachu. Przeciągnij uchwyty krawędzi lub narożników okna dachowego, aby zmienić jego rozmiar na połaci (przeciwległa krawędź zostaje na miejscu; długość możesz wpisać podczas przeciągania). Szerokość biegnie wzdłuż okapu, a długość w górę połaci, mierzona na połaci. Lukarna ma uchwyt z każdej strony do zmiany szerokości. Zmiana, która nie zmieściłaby się na dachu, zostanie odrzucona. Aby je usunąć, kliknij prawym przyciskiem jego uchwyt i wybierz **Usuń okno dachowe** (lub **Usuń lukarnę**), naciśnij **Delete**, gdy kursor jest nad jego uchwytem, albo kliknij uchwyt pośrodku z **Ctrl/⌘**. Właściwości dachu też je wymieniają: wpisz tam szerokość i długość okna albo kliknij **×**, aby je usunąć.

![Uchwyt okna dachowego na zaznaczonym dachu](shots/roof-window-grip.webp)

### Więźba drewniana

**Konstrukcja** ustala, jak dach jest zbudowany:

- **krokwie**: krokwie na płatwiach, z płatwią kalenicową.
- **jętki**: pary krokwi z poziomą jętką na wysokości **Jętki: udział wysokości** (domyślnie dwie trzecie), bez płatwi.
- **wiązary**: prefabrykowane wiązary wzdłuż głównej kalenicy, w rozstawie **Rozstaw krokwi**: pasy górne na połaciach, pas dolny na murłatach, krzyżulce w układzie W (wiązar jednospadowy na dachu jednospadowym). Końce dachu czterospadowego zachowują swoje krokwie.
- **brak**: samo poszycie, bez drewna.

**Krokwie i pasy** przyjmują dawne przekroje w cm lub skandynawskie w mm (od 45×95 do 45×245); razem z nimi idą **Krzyżulce wiązarów** i **Klasa wytrzymałości drewna** (od C14 do C30, drewno klejone). Krokwie opierają się na murłacie przez zacięcie (**Oparcie krokwi na murłacie**) i kończą się na okapie pionowo lub prostopadle (**Zakończenia krokwi**). Style **Dach szwedzki z wiązarów** i **Dach skandynawski z jętkami** przynoszą ze sobą swoją więźbę.

Każdy element jest osobną częścią: zestawienie **Drewno dachu** wymienia je jako listę cięć (element, przekrój, klasa, długość, sztuki, objętość), a IFC otrzymuje każdy jako IfcMember swojego typu, a każdy wiązar jako IfcElementAssembly.

## Sufity podwieszane

**Wykończenia → Sufit podwieszany** (<kbd>CG</kbd>) rysuje sufit podwieszany nad klikniętym obrysem; **Sufit podwieszany w pomieszczeniu** wypełnia kliknięte pomieszczenie, podążając za jego obrysem. Sufit wisi na wysokości z pola **Wysokość** nad kondygnacją (2700 mm, chyba że zmienisz ją we właściwościach).

Jego typ, w **Wykończenia → Typy sufitów**, ustala **System**: **Płyty na widocznym ruszcie** (szerokość, długość i grubość płyty, szerokość lica, wysokość i obrót rusztu, wieszaki do konstrukcji w ich rozstawie) lub **Płyty g-k, ciągły**. Ruszt jest wyśrodkowany na suficie, więc docinane płyty przy krawędziach wychodzą równe.

**Panel świetlny** i **Nawiewnik** wstawiają element w kliknięte pole; kliknij to pole ponownie drugim narzędziem, aby je zmienić. W panelu Właściwości **Rozmieść** wstawia panele świetlne lub nawiewniki co określoną liczbę pól, a **Wyczyść** je usuwa. Zestawienia liczą całe płyty, panele świetlne i nawiewniki, a sufit trafia do IFC jako IfcCovering typu CEILING.

![Właściwości sufitu: jego typ, wysokość i wyposażenie](shots/ceiling-properties.webp)

![Sufity rastrowe z panelami świetlnymi i nawiewnikami, w 3D](shots/ceilings-3d.webp)

## Listwy przypodłogowe

**Wykończenia → Listwa w pomieszczeniu** (<kbd>SK</kbd>) układa listwę przypodłogową wokół klikniętego pomieszczenia, wzdłuż lic ścian, przerywaną przy każdych drzwiach (każdym otworze sięgającym do podłogi); kliknięta wewnątrz ścian bez pomieszczenia podąża za nimi w ten sam sposób. Listwa pomieszczenia podąża za pomieszczeniem: przesuń ścianę lub drzwi, a listwa przesunie się razem z nimi. **Listwa przypodłogowa** rysuje listwę wzdłuż linii klikniętej na licu ściany (Enter kończy); listwa leży po stronie odsuniętej od ściany, a **Odwróć stronę** we właściwościach przekłada ją na drugą stronę.

Jej typ, w **Wykończenia → Typy listew**, ustala **Profil** — **Płaska (prostokątna)**, **Fazowana (MDF)**, **Profilowana (drewniana)**, **Z wyokrągleniem u dołu (metalowa)**, **Lekko zaokrąglona** lub **Półokrągła**, narysowany obok w skali — **Wysokość** (od 40 do 300 mm), **Grubość** (od 5 do 40 mm), **Materiał** i jego kolor. **Z biblioteki** wprowadza do projektu gotowe typy: MDF 80 ścięta, Dąb 70 profilowana, Płytka 80 prosta, Aluminium 60 wyoblona, Zaokrąglona 50 i Półokrągła 70. Gdy narzędzie listwy jest włączone, panel Właściwości pokazuje typ, który ono układa, do wybrania przed kliknięciem.

Właściwości listwy zawierają jej typ, jej **Odsunięcie podstawy** nad podłogą (w sekcji Wymiary) oraz **Przerwy przy drzwiach** (wyłączone: biegnie dalej przez nie). **Zestawienie listew przypodłogowych** (lub **Listwy przypodłogowe** na liście nowych zestawień w przeglądarce projektu) wymienia je według typu, z liczbą sztuk i długością, bez przerw przy drzwiach. Listwa jest łączona na ucios w narożnikach; rzut rysuje jej przednią krawędź, a listwa trafia do IFC jako IfcCovering typu SKIRTINGBOARD, należący do IfcCoveringType tego samego typu.

![Typy listew przypodłogowych: profil narysowany w skali, biblioteka](shots/skirting-types.webp)

![Właściwości listwy przypodłogowej: jej typ, odsunięcie podstawy i przerwy przy drzwiach](shots/skirting-properties.webp)
