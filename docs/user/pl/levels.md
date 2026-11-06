---
title: Poziomy i rzuty
order: 60
summary: Dodawaj poziomy, ustawiaj ich wysokości i oglądaj inny poziom pod rzutem, na którym rysujesz.
---

<!-- video levels -->

## Poziomy

Przeglądarka projektu wyświetla poziomy na górze, najwyższy jako pierwszy. **+** obok **Poziomy** dodaje poziom nad najwyższym.

Zaznacz poziom, aby zobaczyć jego **poziom dolny** i **poziom górny**. Każdy z nich może leżeć powyżej lub poniżej ±0.00. Wysokość to odległość między nimi. Gdy przesuwasz dolny, górny zostaje na miejscu. Nowy projekt zaczyna się od kondygnacji Fundamenty, Parter i Piętro 1. Poziom ma najwyżej 100 m wysokości i leży w granicach ±10 km od ±0.00: wartość wpisana w złej jednostce jest odrzucana. Schody, które wznosiłyby się o ponad 50 m do poziomu powyżej, kończą się tam i mówią dlaczego.

![Właściwości poziomu: poziom dolny, poziom górny i wysokość](shots/level-properties.webp)

Każdy poziom ma własny rzut w grupie **Rzuty**. Kliknij dwukrotnie rzut, aby rysować na tym poziomie. Poziom, na którym rysujesz, jest pokazany w polu w lewym górnym rogu widoku.

Na przekrojach i elewacjach każdy poziom jest oznaczony po prawej stronie swoją nazwą i rzędną. **Zarządzaj → Style obiektów → Linie ukryte** ustawia też **znacznik poziomu**: pusty trójkąt, trójkąt z zaczernioną lewą połową (tak jak rysuje go norma rumuńska), czarny trójkąt lub kreskę, oraz jego wielkość na papierze.

Rzędne są zapisywane w jednostkach projektu: w metrach (+3.00) w projekcie metrycznym, tak jak zapisują je rysunki, i w stopach i calach (+9'-10 1/8") w imperialnym. Podążają za tym przeglądarka, ramka w lewym górnym rogu widoku, znaczniki poziomów w przekrojach i elewacjach oraz wymiary rzędnych.

## Widok poziomu poniżej

Aby widzieć inny poziom podczas rysowania, wybierz go w polu **Podkład** na pasku rzutu. Rzut tego poziomu jest rysowany pod spodem, wyblakły i szary. Możesz się do niego przyciągać, ale nie możesz go zaznaczyć.

![Piętro 1 narysowane nad parterem, pokazanym jako podkład](shots/underlay.webp)

Podkład pomaga tylko w rysowaniu; nie jest drukowany. Aby wydrukować na rzucie elementy innych poziomów, otwórz właściwości widoku rzutu i wybierz je w polu **Pokazane kondygnacje**. Możesz pokazać niektóre poziomy lub wszystkie, jeśli chcesz, w półtonie.

## Kopiowanie na inne kondygnacje

Aby powtórzyć ściany, stropy lub cały rzut na innych poziomach, zaznacz je (ściany z ich drzwiami i oknami, stropy, pomieszczenia, grupę…) i wybierz **Modyfikuj → Kopiuj na kondygnacje**. Możesz też wpisać `COPYSTOREY` w wierszu poleceń albo kliknąć zaznaczenie prawym przyciskiem na rzucie lub w widoku 3D. Zaznacz poziomy, na które kopiujesz. **Wszystkie powyżej** i **Wszystkie poniżej** zaznaczają każdy poziom nad lub pod poziomem zaznaczenia, a Shift-kliknięcie zaznacza wszystkie poziomy między klikniętym a ostatnio klikniętym. Poziomu, na którym stoi zaznaczenie, nie można zaznaczyć.

![Kopiuj na kondygnacje: dwa poziomy zaznaczone i opcje](shots/copy-to-storeys.webp)

- **Względnie** (domyślnie): każda kopia stoi na tej samej wysokości nad swoim nowym poziomem, co oryginał nad swoim. Ściana narysowana na parterze stoi na pierwszym piętrze.
- **Bezwzględnie**: każda kopia zostaje tam, gdzie oryginał jest w przestrzeni, na nowym poziomie. Jej odsunięcie podstawy wyrównuje różnicę.
- **Także elementy osadzone**: drzwi i okna idą ze swoimi ścianami, a otwory z tym, co przecinają. Etykiety i wymiary kopiowanych elementów przechodzą z rzutu ich poziomu na rzut nowego poziomu. Pomieszczenie zabiera swoje wykończenia, a schody przestrzeń pod nimi. Odznacz opcję, aby skopiować ściany bez drzwi i okien.
- **Odtwórz relacje między kopiami**: ściany połączone w narożniku lub w T łączą się tak samo na każdym nowym poziomie. Ściana dowiązana do stropu lub dachu kopiowanego razem z nią zostaje dowiązana do jego kopii. Grupa staje się grupą kopii, a zespół jest kopiowany w całości. Schody wchodzą na poziom nad nowym i wycinają tam własny otwór w stropie.
- **Pomiń duplikaty**: poziom, który ma już ten sam element w tym samym miejscu, nie dostaje drugiego. Komunikat na dole mówi, ile pominięto.

Kopie używają tych samych typów co oryginały i dostają nowe identyfikatory IFC GlobalId. Wszystkie kopie to jeden krok cofania i pozostają potem zaznaczone, więc możesz je razem przesunąć lub zmienić. We wspólnej sesji tylko gość cofa własne kopie.

![Ściany i strop parteru skopiowane na dwa poziomy powyżej, w 3D](shots/copy-to-storeys-3d.webp)

Aby zamiast tego przenieść zaznaczenie na inny poziom, wybierz **Modyfikuj → Przenieś na kondygnację**. Elementy zachowują wysokości nad nowym poziomem albo zostają tam, gdzie są w przestrzeni.
