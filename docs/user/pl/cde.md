---
title: Folder projektu i wydania
order: 125
summary: Wydawaj arkusze i model tak, jak wymaga ISO 19650, przechowywane w pliku projektu oraz, jeśli go masz, w folderze projektu, nazwane według konwencji, z protokołem przekazania, po sprawdzeniach, jakie wykonuje biuro projektowe.
---

## Folder projektu

**Zarządzaj → Folder projektu** ustawia folder, do którego projekt jest także wydawany: folder na twoim komputerze, udział sieciowy albo folder SharePoint lub OneDrive synchronizowany z dyskiem. Folder jest **opcjonalny**: każde wydanie i jego pliki są przechowywane w samym pliku projektu, więc projekt bez folderu wydaje się tak samo, w aplikacji desktopowej i w przeglądarce. Lapin nie wymaga konta i nie łączy się z żadnym serwerem: zapisuje pliki, a to, co synchronizuje folder, należy do twojego biura. Ścieżka jest zapisana w projekcie, więc podróżuje razem z plikiem; folder, którego nie ma na tym komputerze, to ostrzeżenie, a nie błąd. Ustawienie folderu wymaga aplikacji desktopowej.

![Folder projektu: folder, kontenery, nazewnictwo](shots/project-folder.webp)

Przy pierwszym wydaniu Lapin tworzy w folderze cztery kontenery ISO 19650: **WIP**, **Shared**, **Published** i **Archived**. Kod statusu podpowiada kontener: S0 trafia do WIP, S1–S7 do Shared, A, B i CR do Published.

## Nazewnictwo

Pliki są nazywane według ISO 19650-2: **Projekt-Autor-Wolumen-Poziom-Typ-Rola-Numer**, a następnie status i rewizja, na przykład `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. W oknie dialogowym Folder projektu ustawiasz kod projektu, autora (twoje biuro), wolumen oraz domyślny poziom i rolę. Reszta pochodzi z projektu:

- **poziom** arkusza to kondygnacja, którą rysują jego rzuty (`00` na ±0.00, `01` powyżej, `B1` poniżej, `ZZ`, gdy rysuje kilka lub żadnej);
- jego **rola** to rola jego branży (architektura A, konstrukcja S, instalacje M, inżynieria lądowa C);
- jego **numer** to cyfry numeru arkusza (`A-101` staje się `0101`);
- model to `M3`, protokół przekazania `IE`.

## Sprawdzenia

**Zarządzaj → Sprawdź** uruchamia na całym projekcie reguły, które biuro projektowe sprawdza wzrokowo przed wydaniem. Ustalenie **blokujące** zatrzymuje wydanie; **ostrzeżenie** można pominąć. Kliknij ustalenie, aby przejść do arkusza, widoku lub elementu.

![Sprawdzenie: ustalenia według reguł](shots/cde-check.webp)

Blokujące: tabliczka rysunkowa bez numeru, tytułu lub autora rysunku; widok bez skali; widok poza ramką lub na tabliczce rysunkowej; arkusz zmieniony od ostatniego wydania, ale wciąż z tą samą rewizją; numer arkusza, z którego nie wynika numer pliku. Ostrzeżenia: puste pola sprawdzającego, daty i fazy; rzut pokolorowany schematem pomieszczeń albo z wieloma kreskowaniami, bez legendy; rzut, przekrój lub elewacja na żadnym arkuszu; plan zagospodarowania bez strzałki północy; elementy bez kondygnacji, bez typu lub bez materiału; drzwi i okna bez etykiety; pomieszczenia bez numeru; puste wymagane właściwości IFC (IsExternal, LoadBearing, FireRating na drzwiach).

## Wydawanie

**Zarządzaj → Wydaj…** tworzy wydanie, przechowywane w pliku projektu, a gdy projekt ma folder na tym komputerze, zapisywane także w jego kontenerze:

![Wydanie: arkusze, rewizja, status, sprawdzenia](shots/cde-issue.webp)

1. Nadaj wydaniu nazwę, jego **rewizję** (`P01`, `P02`… na etapie wstępnym, `C01`… po etapie umownym; kolejna jest proponowana na podstawie ostatniego wydania), jego **status** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) i **kontener**.
2. Zaznacz arkusze do wydania, model jako IFC, każdy arkusz także jako DXF albo wszystkie arkusze także w jednym pliku PDF. Nazwa każdego pliku jest pokazana tak, jak zostanie zapisana. IFC jest przechowywany w pliku projektu tylko wtedy, gdy zaznaczysz **Zachowaj IFC w pliku projektu** (duży model to dziesiątki MB na wydanie); bez zaznaczenia trafia do folderu projektu, a plik projektu zachowuje jego nazwę i skrót. **Także do folderu projektu** zapisuje pliki również w kontenerze (domyślnie włączone, gdy projekt ma folder). Wydawane są też dokumenty opisowe projektu, każdy jako PDF, a na życzenie także jako DOCX (zobacz [Dokumenty](documents.md)).
3. Sprawdzenia wykonują się w trakcie wybierania. Przy ustaleniu blokującym wydanie nie może pójść; przy samych ostrzeżeniach zaznacz **Wydaj mimo to**.
4. **Wydanie** tworzy każdy arkusz jako PDF, IFC i resztę, nazwane według konwencji, oblicza skrót każdego pliku (SHA-256), tworzy **protokół przekazania** (PDF i CSV: projekt, wydanie, data, rewizja, status, pliki) i **manifest** (JSON, z każdym skrótem), przechowuje je wszystkie w pliku projektu i rejestruje wydanie w projekcie. Każdy wydrukowany arkusz nosi rewizję i status wydania w tabliczce rysunkowej oraz wiersz wydania w tabeli rewizji, tak jak został wydany.
5. W tym samym kroku projekt jest zapisywany jako **wersja** nazwana od wydania (zobacz [Pliki, wersje i odzyskiwanie](files.md)), więc wydanie można ponownie otworzyć, przywrócić lub porównać z projektem w obecnym stanie, a projekt jest zapisany, nie zostaje z niezapisanymi zmianami. Okno dialogowe pokazuje następnie wydanie tak, jak wyszło — jego nazwę, rewizję i pliki, każdy do otwarcia — dopóki go nie zamkniesz lub nie zaczniesz **Nowe wydanie**.

![Okno dialogowe Wydaj po wydaniu: wydanie tak, jak wyszło, i jego pliki](shots/cde-issued.webp)

## Wydania w pliku projektu

Wydania są wymienione na dole okna dialogowego Folder projektu, razem z tym, ile zajmują w pliku projektu (identyczne pliki są przechowywane raz, niezależnie od tego, ile wydań je współdzieli). Kliknij wydanie, aby zobaczyć jego pliki:

![Wydania w pliku projektu: ile zajmują, wydanie otwarte z plikami](shots/cde-issues.webp)

- kliknij plik, aby go **otworzyć** z pliku projektu (aplikacja desktopowa otwiera go w jego programie; przeglądarka go pobiera), albo **Zapisz kopię…**;
- **Pobierz jako zip** daje wszystkie przechowywane pliki wydania w jednym archiwum zip, nazwanym od jego protokołu przekazania: to, co wysyłasz, gdy nie ma wspólnego folderu;
- **Opublikuj w folderze…** (aplikacja desktopowa) zapisuje wcześniejsze wydanie do kontenera folderu projektu albo do dowolnie wybranego folderu: najpierw wydaj, folder wybierz później;
- **Usuń przechowywane pliki** pozwala usunąć pliki starego wydania z pliku projektu przy następnym zapisie, by plik był mały; zapis wydania, nazwy jego plików i ich skróty pozostają;
- **Otwórz folder** pokazuje kontener, do którego zapisano wydanie.

## W przeglądarce

CDE działa także w przeglądarce: Sprawdź, Wydaj, pliki wydań (otwieranie, Pobierz jako zip) i oba rodzaje uwag. Plik projektu zawiera wszystko i wraca do ciebie jako plik, który zapisujesz. Folder projektu, publikowanie do niego i jego skanowanie wymagają aplikacji desktopowej.

## Uwagi (BCF)

**Zarządzaj → Uwagi do modelu…** przechowuje uwagi zgłoszone do modelu — twoje i recenzenta — jako tematy BCF (BIM Collaboration Format, którym wymieniają się BIMcollab, Solibri, Navisworks i Revit), w projekcie, nigdy w IFC. Uwaga ma tytuł, opis, typ, status (Otwarta, W toku, Rozwiązana, Zamknięta), priorytet, osobę, której jest przypisana, termin i etykiety; swoje komentarze; oraz swoje punkty widzenia — kamerę 3D, zaznaczone elementy, przekrój prostopadłościenny jako płaszczyzny tnące i zrzut ekranu.

![Uwagi: lista z filtrami, uwaga ze zrzutem ekranu i komentarzami](shots/remarks.webp)

- **Nowa uwaga** tworzy uwagę z widoku 3D na pierwszym planie: kamerę taką, jaka jest, zaznaczone elementy, przekrój prostopadłościenny i obraz wykonany z widoku. Wypełnij pola i kliknij **Utwórz**.
- Listę filtruje się tekstem, statusem, priorytetem, osobą przypisaną i opcją **Tylko otwarte**. Wybierz uwagę, aby zobaczyć i zmienić jej pola, przeczytać komentarze i odpowiedzieć w polu (**Skomentuj**; zaznacz **Z widokiem 3D**, aby dołączyć do odpowiedzi widok taki, jaki jest).
- **Pokaż w 3D** otwiera punkt widzenia uwagi: widok 3D przechodzi do jej kamery, jej elementy zostają zaznaczone (te, które ma projekt, według ich IFC GlobalId), a jej płaszczyzny tnące stają się przekrojem prostopadłościennym. Komentarz z własnym widokiem ma obok przycisk **Pokaż**.
- **Odśwież z widoku 3D** zastępuje punkt widzenia uwagi widokiem w obecnym stanie.
- **Importuj BCF…** wczytuje plik `.bcfzip` (BCF 2.1) lub `.bcf` (BCF 3.0): temat, który projekt już ma (ten sam GUID), jest aktualizowany — pola z pliku wygrywają, a komentarze dodane tutaj, których plik nie ma, pozostają — pozostałe są dodawane. **Eksportuj wszystkie**, **Eksportuj otwarte** lub **Eksportuj zaznaczoną** zapisują plik BCF, w wersji 2.1, chyba że wybierzesz 3.0, do przekazania recenzentowi lub wrzucenia do kontenera Shared folderu projektu. Tematy zachowują swoje GUID, więc po przejściu przez inne narzędzie wracają jako te same uwagi.

![Uwaga otwarta w swoim punkcie widzenia w widoku 3D](shots/remark-viewpoint.webp)

Twoje imię przy tym, co piszesz, ustawiasz w polu **Twoje imię** okna dialogowego (jest zapisywane na tym komputerze).
## Uwagi na arkuszach

Recenzent otwiera wydany PDF w Acrobacie, Bluebeamie, Podglądzie lub Drawboardzie i nanosi na nim oznaczenia: notatki, tekst, wyróżnienia i przekreślenia, pociągnięcia pióra, prostokąty, okręgi, strzałki, chmurki, pieczątki, pomiary, odpowiedzi. **Zarządzaj → Uwagi do arkuszy…** wczytuje te oznaczenia z powrotem do projektu jako uwagi na arkuszu:

![Uwagi: lista według arkuszy, wczytany PDF](shots/cde-remarks.webp)

- **Wczytaj uwagi z PDF…** wybiera plik. Arkusz, do którego należy, wynika z wydania (manifest obok pliku lub zapis wydań w samym projekcie), a w przeciwnym razie z konwencji nazwy pliku; gdy nic nie pasuje, wybierasz arkusz sam. Strony zbiorczego PDF trafiają po kolei do jego arkuszy. Pokazywane jest, co znaleziono — strony, adnotacje, sposób dopasowania arkusza, ewentualne ostrzeżenia — a **Importuj** wprowadza uwagi. Ponowne wczytanie tego samego pliku aktualizuje jego uwagi zamiast je podwajać; uwaga, którą rozwiązałeś, pozostaje rozwiązana. Plik, którego nazwę zmieniono po drodze, nadal zostaje dopasowany: wydane bajty są początkiem pliku recenzenta.
- **Przeszukaj folder projektu** (aplikacja desktopowa) wymienia wydane pliki PDF w Shared i Published, które z nich zmieniły się po wydaniu i czy ich uwagi zostały od tego czasu wczytane.
- Lista jest pogrupowana według arkuszy i filtrowana według statusu: **otwarte**, **rozwiązane**, **zamknięte**. Kliknięcie otwiera arkusz z uwagą na pierwszym planie. Każdą uwagę można rozwiązać, zamknąć, otworzyć ponownie lub usunąć; stan recenzenta „Completed” lub „Accepted” przychodzi jako rozwiązana.

![Arkusz z naniesionymi uwagami](shots/cde-remarks-sheet.webp)

Na arkuszu uwagi są rysowane na papierze w swoim miejscu: obrys w kolorze recenzji, znacznik z inicjałami autora, tekst w podpowiedzi; uwaga na pierwszym planie ma swoją kartę z tekstem, autorem i datą, odpowiedziami i statusem. Przycisk na pasku narzędzi arkusza ukrywa je lub pokazuje, a przeglądarka projektu liczy otwarte uwagi obok arkusza. Są nakładką tylko na ekran: arkusz drukuje się i wydaje bez swoich uwag.

Lapin sam czyta PDF, bez żadnego innego programu: klasyczne pliki i te zapisane ze strumieniami odsyłaczy, zapisy przyrostowe (to, co dopisuje program recenzenta), tekst w dowolnym kodowaniu. Z pliku, którego nie umie przeczytać w całości, wydobywa, co się da, z ostrzeżeniem. Lapin nigdy nie zapisuje niczego do pliku PDF.
