---
title: Harmonogram
order: 97
summary: Harmonogram robót na wykresie Gantta: zadania w strukturze WBS, kamienie milowe, powiązania z opóźnieniami, ścieżka krytyczna liczona na kalendarzu z rumuńskimi świętami ustawowymi; do i z MS Project, IFC i Excela.
---

## Karta Harmonogram

![Karta Harmonogram](shots/ribbon-planning.webp)

- **Gantt**: otwiera harmonogram pod widokiem. Ekran dzieli się na dwa wiersze: model u góry, harmonogram na dole.
- **Kalendarz…**: dni i godziny robocze, święta i przerwa zimowa (poniżej).
- **Zadanie** i **Kamień milowy**: nowe zadanie lub kamień milowy przed zadaniem zaznaczonym w Gantcie (albo na końcu).
- **Zwiększ wcięcie**, **Zmniejsz wcięcie**, **Połącz zadania** i **Rozłącz zadania**: WBS i powiązania zaznaczonych zadań.
- **Dodaj do zadania…** (TA) i **Elementy bez zadania**: elementy modelu w zadaniach (poniżej).
- **Importuj MS Project…**, **Eksportuj MS Project…**, **Eksportuj XLSX** i **Eksportuj IFC…**: harmonogram do i z innych programów (poniżej).

Projekt ma jeden harmonogram. Powstaje przy pierwszym dodaniu zadania i zaczyna się w następny poniedziałek. Jego początek zmienisz na pasku Gantta.

## Gantt

![Gantt pod widokiem 3D, zaznaczone zadanie](shots/planning-gantt.webp)

Po lewej jest tabela zadań: numer wiersza, kod **WBS**, nazwa, **Czas trwania** w dniach roboczych, **Początek** i **Koniec**, **Poprzedniki** oraz całkowity **Zapas**. Po prawej każde zadanie jest paskiem na osi czasu:

- **zadanie** to niebieski pasek, czerwony, gdy leży na ścieżce krytycznej;
- **zadanie podsumowujące** (zadanie z zadaniami pod nim) to ciemny nawias od początku jego pierwszego zadania do końca ostatniego;
- **kamień milowy** to romb.

Dni niepracujące są zacieniowane, a dzisiaj to linia przerywana. **Dni**, **Tygodnie**, **Miesiące** i **Kwartały** zmieniają skalę, tak samo jak <kbd>Ctrl</kbd> + kółko myszy. Przycisk z czterema rogami pokazuje cały harmonogram, a **Dziś** przewija do dzisiaj. **Ścieżka krytyczna** włącza i wyłącza czerwień. Strzałka obok nazwy zadania podsumowującego zwija i rozwija zadania pod nim.

Gantt jest widokiem jak inne: ma kartę, a na pulpicie można go otworzyć we własnym oknie, na drugi ekran.

### Edycja w tabeli

Kliknij komórkę dwukrotnie, aby w niej pisać. <kbd>Enter</kbd> zachowuje wartość, a <kbd>Escape</kbd> z niej rezygnuje.

- **Czas trwania**: dni robocze; 0 zmienia zadanie w kamień milowy.
- **Początek**: zadanie zaczyna się nie wcześniej niż tego dnia (ograniczenie, patrz niżej).
- **Koniec**: czas trwania staje się liczbą dni roboczych od początku do tego dnia.
- **Poprzedniki**: jak w MS Project, numery wierszy oddzielone przecinkami, każdy z typem i opóźnieniem: `3`, `5SS`, `7FF+2`, `2FS-1d`.

Klawiatura działa jak w tabeli: <kbd>↑</kbd> i <kbd>↓</kbd> przesuwają zaznaczenie, <kbd>Insert</kbd> dodaje zadanie przed zaznaczonym (<kbd>Shift</kbd>+<kbd>Insert</kbd> kamień milowy), <kbd>Delete</kbd> usuwa zaznaczone zadania wraz z tym, co jest pod nimi, <kbd>Tab</kbd> zwiększa wcięcie, a <kbd>Shift</kbd>+<kbd>Tab</kbd> je zmniejsza, <kbd>F2</kbd> zmienia nazwę.

### Edycja na wykresie

- **Przeciągnij pasek**, aby przesunąć zadanie. Zaczyna się ono wtedy nie wcześniej niż w dniu, na którym je upuścisz. Data widoczna jest podczas przeciągania.
- **Przeciągnij koniec paska**, aby zmienić jego czas trwania. Dni robocze widać podczas przeciągania.
- **Połącz dwa zadania**: najedź na pasek, a potem przeciągnij od małego kółka na jednym z jego końców na inny pasek. Łączone końce wyznaczają typ: z końca na początek drugiego to zakończenie–rozpoczęcie (FS), początek na początek SS, koniec na koniec FF, początek na koniec SF.
- **Kliknij strzałkę**, aby zmienić typ i opóźnienie powiązania (w dniach roboczych; ujemne oznacza nakładanie się) albo je usunąć.

Każda zmiana to jeden krok do cofnięcia.

## Zadanie we Właściwościach

![Zadanie we Właściwościach](shots/planning-task.webp)

Zaznacz zadanie, a Właściwości je pokażą: jego nazwę, rodzaj w IFC, czas trwania, czy jest kamieniem milowym, jego daty oraz:

- **Ograniczenie**: **Jak najwcześniej** (domyślnie), **Rozpocznij nie wcześniej niż** data albo **Musi rozpocząć się** w dniu. Zadanie, które musi się zacząć, zanim skończą się jego poprzedniki, zabiera im zapas. Ich zapas staje się wtedy ujemny i stają się krytyczne.
- **Roboty mokre**: beton, wylewki, tynki. Zadanie mokre nie pracuje w przerwie zimowej kalendarza, gdy przerwa jest włączona.
- **Zapas całkowity** i **Zapas swobodny**: o ile dni roboczych zadanie może się opóźnić bez przesuwania końca, albo bez przesuwania tego, co po nim następuje. **Najpóźniejszy początek** i **Najpóźniejszy koniec** to najpóźniejsze daty, które zachowują koniec.
- **Poprzedniki**: każde powiązanie wchodzące, z typem i opóźnieniem do zmiany lub usunięcia. **Następniki** wymieniają to, co następuje po zadaniu.

## Ścieżka krytyczna

Lapin liczy harmonogram po każdej zmianie. Przejście w przód znajduje daty najwcześniejsze, a przejście wstecz najpóźniejsze. Zadania bez zapasu tworzą ścieżkę krytyczną, a opóźnienie któregokolwiek z nich przesuwa koniec. Powiązanie z zadaniem podsumowującym trzyma wszystkie zadania pod nim. Powiązanie od zadania podsumowującego zaczyna się na początku jego pierwszego zadania i kończy na końcu ostatniego. Lapin odrzuca powiązanie, które utworzyłoby pętlę.

## Elementy modelu w zadaniach

![Zaznaczone zadanie Murowanie: jego ściany pokazane jako zaznaczone w 3D](shots/planning-links.webp)

Zadanie bierze elementy modelu, więc harmonogram wie, co i kiedy jest budowane. Zaznacz zadanie, a jego elementy zostaną pokazane jako zaznaczone w 3D i na rzutach; dwukrotne kliknięcie jego paska zaznacza je i kadruje. Zaznacz elementy w widoku, a wiersze ich zadań zostaną oznaczone w Gantcie. Kolumna **Elementy** liczy elementy każdego zadania (zadania sumarycznego razem z elementami zadań pod nim).

### Reguła

![Elementy zadania we Właściwościach](shots/planning-task-elements.webp)

W sekcji **Elementy** we Właściwościach zaznacz **Znajdź je regułą** i wybierz, co bierze zadanie, jak w filtrze zestawienia: **Rodzaje**, **Kondygnacje**, **Typy**, **Materiały**, **Grupy** i **Fazy**, każda wartość z liczbą elementów, które ją mają. Każdy ustawiony warunek musi być spełniony; pusty przepuszcza wszystko. Zapisywana jest reguła, nie elementy: ściana narysowana jutro na parterze sama trafi do zadania „Murowanie parteru”. **Reguła z zaznaczenia** tworzy regułę z rodzajów i kondygnacji zaznaczonych elementów.

### Część elementów

**Część** mówi, co zadanie bierze z każdego elementu: **całe elementy**, ich **rdzeń** (warstwę konstrukcyjną), ich **wykończenia** (wszystkie warstwy poza rdzeniem), warstwy jednego materiału albo jednej kategorii materiału (Wykończenie, Izolacja…). Tak ściana warstwowa jest murowana w zadaniu Murowanie (rdzeń) i tynkowana w zadaniu Tynki (wykończenia). Element bez warstw jest swoim własnym rdzeniem i nie ma wykończeń.

### Ręcznie

![Karta zaznaczenia nad widokiem 3D, dwa zadania ściany oznaczone w Gantcie](shots/planning-element-tasks.webp)

Gdy Gantt jest otwarty, karta nad widokiem modelu mówi, ile elementów jest zaznaczonych i w jakich zadaniach są. Przeciągnij ją za uchwyt na wiersz zadania: elementy trafią do tego zadania. Przytrzymaj <kbd>Shift</kbd> przy upuszczaniu, aby je z niego usunąć. **Dodaj do zadania…** (<kbd>T</kbd> <kbd>A</kbd>, także na karcie Harmonogram) wybiera zadanie z listy.

![Dodaj do zadania](shots/planning-assign.webp)

To, co dodajesz lub usuwasz ręcznie, to **wyjątki** zadania: reguła pozostaje bez zmian. Właściwości je liczą, a × obok każdego je zapomina. Właściwości elementu wymieniają jego zadania w sekcji **Harmonogram robót**, każde z × do usunięcia go z zadania. Każda zmiana to jeden krok do cofnięcia.

### Elementy bez zadania

Gdy tylko jakieś zadanie bierze elementy, pasek Gantta mówi, ile elementów nie ma zadania albo ma warstwy, których nie bierze żadne zadanie; kliknięcie je zaznacza, gotowe do przeciągnięcia na zadanie. **Harmonogram → Elementy bez zadania** robi to samo. **Zarządzaj → Sprawdź** też je wymienia, pod *Elements with a task*: jedno ostrzeżenie na element, kliknięcie go zaznacza. Istniejące elementy, które zostają, nie potrzebują zadania.

### W IFC i MS Project

Eksport IFC zapisuje to, co buduje każde zadanie, jako IfcRelAssignsToProduct (element z zadaniami, które go budują), a to, na co działa zadanie rozbiórki, usunięcia lub demontażu, jako IfcRelAssignsToProcess. Reguła, część i wyjątki są zapisywane w `Lapin_Task`, więc Lapin odczytuje regułę z powrotem; plik z innego programu daje zadania z elementami, które im przypisuje. MS Project XML przenosi je w polu Text1 o nazwie *Lapin elements*. Skoroszyt ma kolumny **Elements** i **Rule**.

## Kalendarz

![Kalendarz pracy](shots/planning-calendar.webp)

**Harmonogram → Kalendarz…** ustawia:

- **dni robocze** tygodnia (domyślnie od poniedziałku do piątku) i **godziny** dnia (8 od 08:00);
- **święta**: rumuńskie święta ustawowe, liczone dla każdego roku (w tym prawosławny Wielki Piątek, Wielkanoc i Zielone Świątki), albo żadne. Lista pod nimi pokazuje święta danego roku do sprawdzenia;
- **przerwę zimową** dla robót mokrych, domyślnie wyłączoną: od 15 grudnia do 1 marca, chyba że ustawisz inne dni;
- **wyjątki**: dni wolne dla projektu (zamknięta budowa) albo dni przepracowane, które inaczej by nimi nie były (sobota).

Czasy trwania i opóźnienia liczą dni robocze tego kalendarza.

## Wymiana harmonogramu

- **Eksportuj MS Project…** zapisuje plik XML MS Project. Otwierają go MS Project, ProjectLibre i GanttProject, a Primavera P6 go importuje. Zawiera zadania z ich WBS, czasami trwania, datami, ograniczeniami i zapasami, powiązania z ich typami i opóźnieniami oraz kalendarz z każdym świętem jako wyjątkiem. **Importuj MS Project…** wczytuje taki plik w miejsce harmonogramu projektu. Harmonogram zapisany przez Lapin i wczytany ponownie zachowuje identyfikatory i daty swoich zadań.
- **Eksportuj IFC…** (także w menu **Plik**) zapisuje harmonogram w pliku IFC razem z modelem: IfcWorkSchedule, jego IfcTask z czasami, powiązania IfcRelSequence i IfcWorkCalendar. Import pliku IFC z harmonogramem wprowadza również jego.
- **Eksportuj XLSX** zapisuje skoroszyt: tabelę Gantta i dni wolne kalendarza.

Własne pola Lapin, dla których inne programy nie mają miejsca (ograniczenie zadania, to, czy jest mokre, przerwa zimowa), podróżują w IFC jako zestawy właściwości `Lapin_Task` i `Lapin_Calendar`. MS Project nie ma zadań mokrych, więc przerwa zimowa zostaje w Lapin.

## Co dalej

Harmonogram wygenerowany z modelu, symulacja 4D budynku rosnącego w 3D, postęp i plany bazowe oraz kosztorys przyjdą w kolejnych etapach.

