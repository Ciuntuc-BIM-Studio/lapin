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

Powiązanie elementów modelu z zadaniami, harmonogram wygenerowany z modelu, symulacja 4D budynku rosnącego w 3D, postęp i plany bazowe oraz kosztorys pojawią się w następnych etapach.
