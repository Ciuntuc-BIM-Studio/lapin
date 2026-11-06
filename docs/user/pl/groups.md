---
title: Grupy i porządkowanie
order: 112
summary: Trzymaj elementy razem jako grupę, otwórz grupę, aby ją edytować, zaznaczaj obszarem lub lassem w widoku 3D i ukrywaj to, co przeszkadza.
---

## Grupy

Grupa trzyma elementy razem. Zaznacz je i kliknij **Modyfikuj → Utwórz grupę** (<kbd>GP</kbd>). Kliknięcie dowolnego członka zaznacza wtedy całą grupę, na rzutach i w widoku 3D. Grupa przesuwa się, kopiuje, obraca, odbija lustrzanie i usuwa jako całość, a kopia jest osobną grupą. W IFC grupa jest obiektem IfcGroup, więc inne programy też ją widzą.

Menu prawego przycisku na rzucie, przekroju lub elewacji ma te same polecenia: **Utwórz grupę** dla zaznaczenia, a na grupie **Rozgrupuj** i **Edytuj grupę**. Grupować można też linie, teksty i kreskowania narysowane na widoku, w tym linie zaimportowanego planu DXF, więc cały rysunek przesuwa się jako całość.

![Linie narysowane na rzucie, zgrupowane z menu prawego przycisku](shots/group-drafting.webp)

Właściwości zaznaczonej grupy podają liczbę zawartych w niej elementów i mają trzy przyciski: **Edytuj grupę**, **Rozgrupuj** i **Zaznacz członków**.

![Zaznaczona grupa: jej członkowie podświetleni, jej właściwości](shots/group-selected.webp)

## Edycja grupy

Kliknij dwukrotnie członka albo kliknij **Edytuj grupę** (<kbd>EG</kbd>). Reszta modelu jest rysowana wyblakła i nie można jej zaznaczyć, a kliknięcie zaznacza jednego członka naraz. Usuwaj, przesuwaj lub zmieniaj członków jak zwykle. **Usuń z grupy** wyjmuje zaznaczonych członków. **Gotowe** lub <kbd>Esc</kbd> zamyka grupę. Grupa znika, gdy usunięty zostanie jej ostatni członek.

![Grupa otwarta do edycji: mur ogrodowy poza nią wyblakły](shots/group-edit.webp)

## Wybór według poziomów zespołu

Niektóre elementy są częściami całości: słupy, rygle i płatwie hali stalowej, części zespołu (skrzynka Studio, zespół wczytany z pliku IFC) i członkowie grupy. **Widok → Wybór według poziomów zespołu**, na początku wyłączony, wybiera je tak, jak zbudowana jest całość. Pierwsze kliknięcie części zaznacza całość na samej górze: grupę albo halę. Dopóki jest zaznaczona, kliknięcie w jej wnętrzu zaznacza część o poziom niżej, w drodze do tego, co kliknięto, a każde kolejne kliknięcie schodzi o poziom głębiej, aż do samego elementu. Kliknięcie innej części tej samej całości zaznacza ją na tym samym poziomie. <kbd>Esc</kbd> wraca o poziom w górę, a na górze zwalnia zaznaczenie; kliknięcie w puste miejsce też je zwalnia. <kbd>Ctrl</kbd>+kliknięcie (<kbd>Cmd</kbd>+kliknięcie na Macu) od razu zaznacza sam element, a <kbd>Shift</kbd> dodaje do zaznaczenia całość z góry. Ścieżka nad widokiem nazywa poziomy, od kondygnacji do zaznaczenia: kliknij jeden, aby go zaznaczyć. Dopóki zaznaczona jest część wewnątrz całości, reszta modelu jest rysowana lekko przygaszona. Działa to tak samo w 3D, rzutach, przekrojach i elewacjach. Ramka lub lasso zaznaczenia biorą całości z góry. Dwukrotne kliknięcie nadal otwiera grupę do edycji; w jej wnętrzu schodzenie zaczyna się poniżej grupy. Wybór jest zapamiętywany na tym komputerze i jest też w **Widok → Ustawienia widoku**.

![Wewnątrz hali stalowej: zaznaczony rygiel, ścieżka nad widokiem 3D](shots/drill-select.webp)

## Zaimportowane pliki

Każdy importowany plik IFC, Fragments lub siatki trafia do projektu jako jedna grupa nazwana od pliku. Odznacz **Jako jedna grupa** przy imporcie IFC, aby wczytać elementy luzem. Plik połączony nie jest grupowany: jest już jednym łączem, tylko do odczytu (zobacz [Import i eksport](import-export.md)). Elementy wczytane z pliku IFC jako odniesienia zostają tam, gdzie umieścił je plik: grupę takich elementów można uporządkować i usunąć, ale nie przesunąć.

## Zaznaczanie obszaru w widoku 3D

**Widok → Zaznacz obszar** (<kbd>SR</kbd>) zamienia przeciąganie lewym przyciskiem w widoku 3D w zaznaczanie, jak na rzutach. Przeciągnięte w prawo okno obejmuje to, co leży całkowicie w jego wnętrzu. Przeciągnięte w lewo okno przecinające obejmuje to, czego dotyka. Naciśnij <kbd>Space</kbd> podczas przeciągania, aby rysować lasso, a <kbd>Shift</kbd>, aby dodawać do zaznaczenia. Gdy tryb jest włączony, prawy przycisk obraca widok, a środkowy go przesuwa. **×** na jego plakietce albo ponownie <kbd>SR</kbd> wyłącza tryb.

![Okno przecinające przeciągnięte nad domem w widoku 3D](shots/select-region-3d.webp)

## Tymczasowe ukrywanie

**Widok → Ukryj** (<kbd>HH</kbd>) ukrywa zaznaczenie w widoku 3D. **Izoluj** (<kbd>HI</kbd>) ukrywa wszystko inne. **Pokaż wszystko** (<kbd>HR</kbd>) albo **×** na plakietce przywraca wszystko. Nic nie jest zapisywane ani usuwane: to sposób, by dotrzeć do wnętrza zaimportowanego modelu podczas jego porządkowania.
