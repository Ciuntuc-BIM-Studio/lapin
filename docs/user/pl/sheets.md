---
title: Arkusze, PDF i DXF
order: 100
summary: Rozmieszczaj widoki na arkuszach z tabliczką rysunkową i drukuj je do PDF lub DXF.
---

<!-- video sheets -->

## Tworzenie arkusza

Kliknij **+** obok **Arkusze** w przeglądarce projektu. Aby umieścić widok, przeciągnij go z przeglądarki na arkusz albo wybierz go w **Umieść widok…** na pasku arkusza. Każdy widok zachowuje własną skalę i pozostaje powiązany z modelem, więc arkusz zmienia się razem z modelem. Na arkuszu przeciągnij widok, aby go przesunąć: widok przesuwa się sam, ze swoim tytułem, a ramka, tabliczka rysunkowa i inne widoki pozostają na miejscu; przyciąga się do krawędzi i środka ramki oraz do innych widoków, a klawisze strzałek przesuwają go o 1 mm (o 10 mm z Shift).

![Arkusz A3 z rzutem i przekrojem](shots/sheet.webp)

Zaznacz arkusz, aby wypełnić jego **tabliczkę rysunkową** w panelu Właściwości: projekt, inwestora, kto rysował, kto sprawdził, datę i rewizję. Tam ustawisz też jego numer, papier i orientację. Pozostawione puste pole projektu pokazuje nazwę projektu: nazwę nadaną projektowi, a w przeciwnym razie nazwę jego pliku.

## Co pokazuje widok

Widok na arkuszu pokazuje to, co wybierzesz, bez przeciągania go na ślepo:

- **Przytnij go na arkuszu.** Kliknij widok: w jego narożnikach i na krawędziach pojawiają się uchwyty. Przeciągnij je, a ramka obejmie większą lub mniejszą część widoku, podczas gdy rysunek zostaje w tym samym miejscu na papierze. Przeciągnij z **Shift** wewnątrz widoku, aby przesunąć rysunek w jego ramce.
- **Narysuj jego granicę w widoku.** **Opisz → Granica widoku** (<kbd>VB</kbd>): narysuj prostokąt na rzucie, przekroju lub elewacji i nadaj mu nazwę, unikalną w projekcie. W **Umieść widok…** widok jest wtedy oferowany z każdą ze swoich granic („Rzut parteru › Skrzydło wschodnie”), a widok umieszczony w ten sposób pokazuje dokładnie ten prostokąt i podąża za nim, gdy przesuwasz go lub zmieniasz jego rozmiar w widoku. Ten sam widok może trafić na kilka arkuszy, każdy z jedną granicą. Umieszczony widok przełącza się między swoimi granicami lub na swobodne przycięcie na liście na swojej ramce. Granice są widoczne tylko na ekranie, nigdy na papierze.
- **Przypnij go.** Pinezka na ramce widoku utrzymuje go w miejscu: nadal można go zaznaczyć, ale nie da się go przypadkiem przesunąć, przesunąć klawiszami ani przyciąć.

Ramka i tabliczka rysunkowa należą do papieru. Nigdy nie są zaznaczane ani przesuwane, a kliknięcie zaznacza najmniejszy widok pod kursorem, więc widok umieszczony na tabliczce rysunkowej zawsze da się zaznaczyć.

![Widok zaznaczony na arkuszu: jego uchwyty przycinania, granica i pinezka](shots/sheet-view-boundary.webp)

## Drukowanie

Przyciski **PDF** i **DXF** na pasku arkusza eksportują arkusz. PDF jest gotowy do druku lub wysłania. DXF otwiera się w AutoCAD i innych programach CAD z warstwami, liniami i kreskowaniami. **Drukuj** wysyła arkusz do drukarki lub plotera w jego własnym rozmiarze papieru, w skali 1:1, jako linie wektorowe: wybierz ploter i papier w oknie drukowania. Na mniejszej drukarce wybierz dopasowanie do strony.
