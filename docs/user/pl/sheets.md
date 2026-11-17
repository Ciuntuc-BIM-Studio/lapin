---
title: Arkusze, PDF i DXF
order: 100
summary: Rozmieszczaj widoki na arkuszach z tabliczką rysunkową i drukuj je do PDF lub DXF.
---

<!-- video sheets -->

## Tworzenie arkusza

Kliknij **+** obok **Arkusze** w przeglądarce projektu. Aby umieścić widok, przeciągnij go z przeglądarki na arkusz albo wybierz go w **Umieść widok…** na pasku arkusza. Każdy widok zachowuje własną skalę i pozostaje powiązany z modelem, więc arkusz zmienia się razem z modelem. Na arkuszu przeciągnij widok, aby go przesunąć: widok przesuwa się sam, ze swoim tytułem, a ramka, tabliczka rysunkowa i inne widoki pozostają na miejscu; przyciąga się do krawędzi i środka ramki oraz do innych widoków, a klawisze strzałek przesuwają go o 1 mm (o 10 mm z Shift).

![Arkusz A3 z rzutem i przekrojem](shots/sheet.webp)

Zaznacz arkusz, aby wypełnić jego **tabliczkę rysunkową** w panelu Właściwości: projekt, inwestora, kto rysował, kto sprawdził, datę i rewizję. Tam ustawisz też jego numer, papier i orientację. Papier to format A (od A0 do A4), jeden z wydłużonych arkuszy ISO (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), format północnoamerykański (Letter, Tabloid, ARCH D, ARCH E) lub **własny**, z jego **Szerokością arkusza** i **Wysokością arkusza** w milimetrach. Pozostawione puste pole projektu pokazuje nazwę projektu: nazwę nadaną projektowi, a w przeciwnym razie nazwę jego pliku.

## Co pokazuje widok

Widok na arkuszu pokazuje to, co wybierzesz, bez przeciągania go na ślepo:

- **Przytnij go na arkuszu.** Kliknij widok: w jego narożnikach i na krawędziach pojawiają się uchwyty. Przeciągnij je, a ramka obejmie większą lub mniejszą część widoku, podczas gdy rysunek zostaje w tym samym miejscu na papierze. Przeciągnij z **Shift** wewnątrz widoku, aby przesunąć rysunek w jego ramce.
- **Narysuj jego granicę w widoku.** **Opisz → Granica widoku** (<kbd>VB</kbd>): narysuj prostokąt na rzucie, przekroju lub elewacji i nadaj mu nazwę, unikalną w projekcie. W **Umieść widok…** widok jest wtedy oferowany z każdą ze swoich granic („Rzut parteru › Skrzydło wschodnie”), a widok umieszczony w ten sposób pokazuje dokładnie ten prostokąt i podąża za nim, gdy przesuwasz go lub zmieniasz jego rozmiar w widoku. Ten sam widok może trafić na kilka arkuszy, każdy z jedną granicą. Umieszczony widok przełącza się między swoimi granicami lub na swobodne przycięcie na liście na swojej ramce. Granice są widoczne tylko na ekranie, nigdy na papierze.
- **Przypnij go.** Pinezka na ramce widoku utrzymuje go w miejscu: nadal można go zaznaczyć, ale nie da się go przypadkiem przesunąć, przesunąć klawiszami ani przyciąć.

Ramka i tabliczka rysunkowa należą do papieru. Nigdy nie są zaznaczane ani przesuwane, a kliknięcie zaznacza najmniejszy widok pod kursorem, więc widok umieszczony na tabliczce rysunkowej zawsze da się zaznaczyć.

![Widok zaznaczony na arkuszu: jego uchwyty przycinania, granica i pinezka](shots/sheet-view-boundary.webp)

## Własna tabliczka

**Zarządzaj → Tabliczki…** pokazuje tabliczki: Lapin (**Lapin standard**, którą dotąd miały wszystkie arkusze, rumuński **cartuș** i tabliczkę **ISO 7200**), projektu i z biblioteki tego komputera, dostępne w każdym jego projekcie. Kliknij jedną, aby zobaczyć ją z wartościami projektu. **Duplikuj do projektu** tworzy kopię do edycji, **Nowa** pustą; **Ustaw jako domyślną projektu** daje ją każdemu arkuszowi, który nie wybiera własnej, a arkusz wybiera własną w polu **Tabliczka** we właściwościach.

![Tabliczki: Lapin, projektu i tego komputera, wybrana z wartościami projektu](shots/title-blocks.webp)

**Edytuj…** otwiera tabliczkę na planszy: własnym widoku rysunkowym w milimetrach papieru, z przerywanym prostokątem tabliczki. Tabliczka to prostokąt w rogu ramki, więc ta sama pasuje od A4 do A0, pionowo lub poziomo. Rysuje się ją tymi samymi narzędziami **Rysunek** co w każdym widoku (linia, polilinia, prostokąt, okrąg, łuk, tekst, kreskowanie i narzędzia edycji); przyciąganie i uchwyty działają jak zwykle. Teksty zachowują swoje żywe pola tak, jak zostały napisane, `{project.name}`, `{sheet.number}`. Logo jest obrazem: **Logo lub obraz…** w panelu albo **Rysunek → Importuj obraz**. Tabela rewizji pokazuje się jako swój prostokąt; przesuń go lub zmień jego rozmiar. Panel obok ma **Nazwę**, szerokość i wysokość prostokąta (mm), róg arkusza, **Ramkę wokół arkusza**, miejsce tabeli rewizji i pola do skopiowania. **Zakończ szkic** zapisuje tabliczkę jako jeden krok do cofnięcia, a jej arkusze za nią podążają; bez zmian zostaje dokładnie taka, jaka była. **Anuluj** odrzuca edycję.

Lista **Pola** zawiera żywy tekst: projektu (nazwa, numer, inwestor, działka, pozwolenie, projektant, osoby i własne parametry), arkusza (numer, nazwa, skala, format, rewizja, status, data, rysował, sprawdził i zatwierdził, faza, arkusz x z y) i systemu (data wydruku, nazwa pliku). Kliknięcie kopiuje pole, aby wkleić je do tekstu napisanego narzędziem **Tekst**; tekst może łączyć słowa i pola, `Projekt: {project.name}`. Nieznane Lapin pole drukuje się jako `<pole>`, a **Zarządzaj → Sprawdź** je zgłasza.

![Tabliczka na planszy: jej przerywany prostokąt, rysunek i panel z polami do skopiowania](shots/title-block-designer.webp)

Arkusze, ich PDF, Drukuj i wydanie rysują ją z wartościami każdego arkusza. W DXF jest blokiem z atrybutami, po jednym na żywy tekst (`PROJECT_NAME`, `SHEET_NUMBER`…), jak tabliczki w AutoCAD. **Eksportuj…** zapisuje tabliczkę jako plik `.json` z obrazami, a **Importuj…** wczytuje ją do projektu lub biblioteki, więc biuro przenosi swoje tabliczki z komputera na komputer. Stare projekty zachowują Lapin standard i rysują się dokładnie jak dotąd.

![Arkusz z własną tabliczką](shots/sheet-title-block.webp)

## Rysowanie na arkuszu

Opisy, znaczniki i legendy, które należą do arkusza, a nie do jednego z jego widoków, rysuje się na samym arkuszu. Kliknij **Opisz** na pasku arkusza albo kliknij dwukrotnie papier poza widokami (**Opisz → Opisz arkusz** robi to samo). Arkusz otwiera się w panelu rysunku jako jego papier, w skali 1:1, z widokami, ramką i tabliczką rysunkową pod spodem, a pasek mówi *rysunek na arkuszu*. Każde narzędzie z **Rysunek** i **Opisz** działa tam jak w widoku, w milimetrach papieru: wpisz 50, a linia ma 50 mm na papierze, a wysokość tekstu to jego wysokość na papierze. Punkty przyciągają się do ramki, tabliczki, ramek widoków (niebieska linia przerywana) i do tego, co rysują widoki. **Arkusz** na pasku wraca do układania widoków.

- **Teksty** mogą zawierać pola na żywo, jak w tabliczce rysunkowej: `Arkusz {sheet.number} z {sheet.count}`, `{project.name}`, `{sheet.date}`. W widoku tekst pozostaje taki, jak go wpisano.
- **Opis z odnośnikiem** (Opisz → Tekst): kliknij, gdzie wskazuje strzałka, potem gdzie ma stać tekst, i wpisz go. Przesunięty opis zabiera strzałkę ze sobą; **Linia odniesienia → usuń** w jego właściwościach usuwa strzałkę. Po zaznaczeniu grot strzałki i każde załamanie mają własne uchwyty: przeciągnij jeden, aby przesunąć go osobno.
- **Linie, polilinie, prostokąty, okręgi, łuki, kreskowania, symbole i obrazy** to te z narzędzi rysunku, z ich uchwytami, warstwami i narzędziami modyfikacji. Strzałka północy jest jednym z symboli Lapin (**Opisz → Symbol**). Symbole wstawione przez **Umieść symbol…** na pasku arkusza też wybiera się tu kliknięciem i przesuwa w milimetrach papieru.
- **Chmurka rewizji** (Rysunek → Wypełnienie, Opisz → Arkusze albo REVCLOUD w wierszu poleceń): dwa narożniki albo **P** dla wielokąta zamykanego klawiszem Enter. **Chmurka rewizji** we właściwościach dowolnej zamkniętej polilinii lub okręgu zamienia ją w chmurkę lub z powrotem i ustala jej łuk na papierze.
- **Legenda** (Opisz → Arkusze): żywa lista tego, co rysują widoki arkusza: materiałów, którymi kreskowane i wypełnione są ich przekroje, albo, z **Listy** we właściwościach, ich linii według roli, typu i grubości. Zmienia się razem z widokami i modelem, a jej tytuł też przyjmuje pola. Jej własne słowa (nagłówek, role i rodzaje linii) są w języku, jaki miał interfejs przy wstawianiu; **Język** we właściwościach je zmienia.
- **Podziałka liniowa** (Opisz → Arkusze): w skali pierwszego widoku arkusza albo w skali podanej we właściwościach, o okrągłej długości około 80 mm lub wybranej przez ciebie. Na rzucie, przekroju lub elewacji podziałka przyjmuje skalę widoku.

To, co narysowano na arkuszu, drukuje się na nim, nad widokami, dokładnie tak, jak widać: w PDF, przy drukowaniu i w DXF. Należy do arkusza: **Duplikuj arkusz** (jego przycisk w przeglądarce projektu) kopiuje to razem z symbolami umieszczonymi na arkuszu, a usunięcie arkusza to usuwa. Każdy rysunek to jeden krok cofania. Arkusze zrobione wcześniej nie mają nic narysowanego i drukują się jak dotąd. Wydanie też to liczy: arkusz zmieniony tylko przez to, co narysowano na jego papierze, lub przez jego symbole, jest zgłaszany jako zmieniony od ostatniego wydania (Zarządzaj → Sprawdź, CDE).

![Rysunek na arkuszu: opis z odnośnikiem, chmurka rewizji, tekst na żywo, legenda materiałów rzutu i podziałka liniowa](shots/sheet-annotations.webp)

![Ten sam arkusz, tak jak się drukuje](shots/sheet-annotated.webp)

## Drukowanie

Przyciski **PDF** i **DXF** na pasku arkusza eksportują arkusz. PDF jest gotowy do druku lub wysłania. DXF otwiera się w AutoCAD i innych programach CAD z warstwami, liniami i kreskowaniami; okręgi, łuki i elipsy (narysowane albo zakrzywione krawędzie stropów i innych obrysów na rzucie) są zapisywane jako prawdziwe obiekty CIRCLE, ARC i ELLIPSE, nie krótkie odcinki, i wracają jako krzywe po ponownym imporcie DXF. **Drukuj** wysyła arkusz do drukarki lub plotera w jego własnym rozmiarze papieru, w skali 1:1, jako linie wektorowe: wybierz ploter i papier w oknie drukowania. Na mniejszej drukarce wybierz dopasowanie do strony.
