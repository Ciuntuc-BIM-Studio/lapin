---
title: Interfejs i skróty
order: 15
summary: Tryby Wygodny, Kompaktowy i Skupienie, zwijanie paneli bocznych oraz skróty klawiaturowe, które możesz zmieniać.
---

<!-- video interface -->

## Obszary robocze

Obszar roboczy pokazuje tylko te karty wstążki, których potrzebuje twoja praca, w kolejności, w jakiej z nich korzystasz. Wybierz go na ekranie startowym, u góry okna **Widok → Interfejs…** albo z **Polecenia…** (Obszar roboczy: …): **Wszystko** (każda karta twojej edycji), **Architektura**, **Konstrukcja i zbrojenie**, **Instalacje**, **Zagospodarowanie i roboty pomocnicze** lub **Rysunek 2D**. Projekt pozostaje jeden: narzędzia ukrytej karty są nadal dostępne w Polecenia… i w skrótach klawiszowych, a model wykonany w jednym obszarze roboczym otwiera się w całości w innym.

![Obszary robocze w Widok → Interfejs…](shots/workspaces.webp)

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

Pod częściami znajdują się **Karty**: odznacz karty wstążki, bez których się obywasz (architekt może ukryć Konstrukcję, Zbrojenie 2D i Instalacje, inżynier Architekturę i Zagospodarowanie). Widok zostaje zawsze, bo jest w nim Interfejs…. Narzędzia ukrytej karty są nadal dostępne w Polecenia… i w skrótach klawiszowych.

Lapin zapamiętuje twój wybór na tym komputerze, dla ciebie. Aplikacja desktopowa i przeglądarka zapamiętują swoje wybory osobno. W przeglądarce Lapin uruchamia się w trybie Kompaktowym, gdy okno ma mniej niż 800 pikseli wysokości, dopóki sam nie wybierzesz.

## Zwijanie paneli bocznych

W każdym trybie przeglądarka projektu i panel Właściwości zwijają się do wąskiego paska po swojej stronie:

- przycisk w prawym górnym rogu panelu go zwija, a strzałka na jego pasku go rozwija;
- kliknij dwukrotnie krawędź między panelem a widokiem;
- <kbd>Ctrl</kbd> <kbd>B</kbd> zwija lub rozwija przeglądarkę, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> właściwości, a <kbd>Tab</kbd> oba panele.

Kliknij ikonę na pasku zwiniętego panelu, aby otworzyć panel nad widokiem bez rozwijania go. Znika po naciśnięciu <kbd>Esc</kbd> lub kliknięciu gdzie indziej; pinezka utrzymuje go otwartym. Przeciągnij krawędź panelu, aby go poszerzyć; Lapin zapamiętuje szerokość.

![Zwinięta przeglądarka otwarta nad widokiem z jej paska](shots/browser-overlay.webp)

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

### Domyślne skróty

Oto skróty w stylu Revit, w obecnej postaci w Lapinie:

<!-- shortcuts -->

Klawisze cyfr <kbd>1</kbd>–<kbd>9</kbd> wybierają szybkie narzędzia.
