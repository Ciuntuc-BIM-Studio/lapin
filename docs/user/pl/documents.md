---
title: Dokumenty
order: 105
summary: Części opisowe projektu — opis techniczny, specyfikacja, wykaz rysunków — tworzone z szablonów, wypełniane z modelu i eksportowane do DOCX, ODT i PDF bez żadnego innego programu.
---

## Czym jest dokument

Projekt to nie tylko rysunki: ma też części opisowe, opis techniczny, specyfikację, wykaz rysunków. W Lapinie są to **dokumenty**: każdy zaczyna się od szablonu i jest wypełniany z projektu — jego danych, powierzchni, kondygnacji i pomieszczeń, typów ścian, zestawień i arkuszy. To, co wiesz tylko ty (konstrukcję, ochronę przeciwpożarową, organizację budowy), wpisujesz w dane wejściowe dokumentu. Dokument podąża za modelem: zmień typ ściany albo dodaj pomieszczenie, a dokument powie to przy następnym otwarciu.

Lapin sam zapisuje pliki, w DOCX, ODT i PDF, bez Worda ani LibreOffice na komputerze, w aplikacji desktopowej i w przeglądarce.

## Informacje o projekcie

**Zarządzaj → Informacje o projekcie…** przechowuje to, czego dokumenty potrzebują od projektu: nazwę, numer i fazę, inwestora i jego adres, adres działki, numer ewidencyjny i księgę wieczystą, projektanta i jego adres, głównego architekta, kto rysował i kto sprawdził. Wpisz je raz: każdy dokument je czyta, a tabliczki arkuszy biorą stąd, kto rysował i kto sprawdził, gdy arkusz tego nie podaje. Każde pole zapisuje się, gdy je opuszczasz.

![Informacje o projekcie: numer, faza, inwestor, działka i projektant](shots/project-info.webp)

Te same pola są na karcie **Informacje o projekcie** w oknie Dokumenty.

## Tworzenie dokumentu

**Zarządzaj → Dokumenty…** (albo **+** obok **Dokumenty** w przeglądarce projektu) otwiera dokumenty projektu. Po lewej są dokumenty już utworzone, a pod **Nowy z szablonu** szablony; kliknij jeden, a nowy dokument zostanie od razu utworzony i wypełniony.

![Dokumenty: dokumenty projektu i szablony, dokument na swoich stronach, jego dane wejściowe i to, co zostało do uzupełnienia](shots/documents.webp)

Pośrodku jest dokument taki, jaki zostanie wydrukowany, strona po stronie, ze spisem treści, tabelami i liczbami. Pole, którego projekt jeszcze nie ma, jest zaznaczone tam, gdzie stoi. Przeglądarka projektu pokazuje dokumenty pod **Dokumenty**; kliknięcie otwiera okno na jednym z nich.

## Dane wejściowe

Karta **Dane wejściowe** zawiera teksty, które może napisać tylko projektant, pogrupowane jak rozdziały dokumentu. W rumuńskich szablonach każdy zaczyna się od wskazówki oznaczonej **[De completat]** (do uzupełnienia), która mówi, co tam wpisać. Pisz na niej: podgląd nadąża chwilę po tym, jak przestaniesz pisać. **Przywróć domyślne** przywraca tekst szablonu.

![Dane wejściowe: konstrukcja wpisana w miejsce wskazówki szablonu](shots/documents-inputs.webp)

## Do uzupełnienia

**Do uzupełnienia** pokazuje, czego dokumentowi jeszcze brakuje: puste pole projektu, wartość, której model nie ma. Kliknij pozycję, aby zobaczyć, gdzie jest w dokumencie, i przejść do pola, które ją wypełnia (pole projektu otwiera kartę **Informacje o projekcie**). Lista pustoszeje w miarę uzupełniania.

![Do uzupełnienia: pola, których dokumentowi jeszcze brakuje](shots/documents-missing.webp)

## Aktualizacja, zmiana nazwy, kopia

- **Aktualizuj z szablonu** bierze ponownie tekst i układ szablonu, dla dokumentu utworzonego przed zmianą szablonu. Twoje dane wejściowe zostają.
- **Zmień nazwę**, **Duplikuj** i **Usuń** działają na pokazanym dokumencie. Usunięcie to jeden krok cofania.

## Eksport

- **Eksportuj DOCX** i **Eksportuj ODT** zapisują plik, który otworzysz i zmienisz w Wordzie lub LibreOffice, z nagłówkami jako własnymi stylami programu, spisem treści i numerami stron jako polami oraz obrazami projektu.
- **Eksportuj PDF** zapisuje dokument złożony przez Lapina, gotowy do druku lub wysłania.

Pole wciąż puste zostaje puste w plikach, więc przed eksportem sprawdź **Do uzupełnienia**.

## Wydawanie dokumentów

**Zarządzaj → Wydaj…** zabiera dokumenty razem z arkuszami i modelem (zobacz [Folder projektu i wydania](cde.md)). Pod **Dokumenty, każdy jako PDF:** każdy dokument jest zaznaczony od początku; zaznacz **Także jako DOCX**, aby dołączyć też kopię edytowalną. Każdy plik jest nazwany według konwencji ISO 19650 (`RP` dla opisu, `SP` dla specyfikacji), ma obliczony skrót i figuruje w zestawieniu przekazania, a dokument dostaje rewizję i status wydania.

![Wydaj: dokumenty razem z arkuszami i modelem](shots/issue-documents.webp)

## Szablony

Lapin ma trzy szablony, na razie po rumuńsku:

- **Memoriu tehnic de arhitectură** (opis techniczny architektury, według rumuńskiej ustawy 50/1991): dane ogólne z Informacji o projekcie; liczba kondygnacji (na przykład `P+1E`) i największa wysokość; powierzchnie — zabudowy, całkowita, użytkowa i piwnicy — łącznie i na kondygnacjach; pomieszczenia każdej kondygnacji z powierzchniami; ściany według typów, z warstwami; stropy i dachy; zestawienie stolarki; wykończenia pomieszczeń; dane wejściowe dla stanu istniejącego, funkcji, konstrukcji, elewacji, instalacji, ochrony przeciwpożarowej, dostępności, efektywności energetycznej i organizacji budowy.
- **Caiet de sarcini: tâmplărie și finisaje** (specyfikacja stolarki i wykończeń): materiały projektu według kategorii, zestawienie stolarki z liczbą sztuk, wykończenia pomieszczenie po pomieszczeniu oraz dane wejściowe dla postanowień ogólnych, wymagań, wykonania i odbioru, z domyślnymi tekstami do dostosowania.
- **Borderou de piese scrise și desenate** (wykaz części opisowych i rysunkowych): dokumenty i arkusze projektu, z numerem, tytułem, skalą i rewizją.

Wizualny edytor własnych szablonów pojawi się później.
