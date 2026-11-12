---
title: Rozszerzenia
order: 126
summary: Dodawaj meble, szablony projektów, profile, wzory kreskowania, tabliczki i cenniki zrobione przez innych z katalogu Lapin Extensions i usuwaj je z powrotem.
---

## Katalog

**Lapin Extensions** to bezpłatny katalog rzeczy zrobionych dla Lapina przez jego użytkowników i przez nas: pakiety mebli, szablony projektów, biblioteki profili, wzory kreskowania, tabliczki i cenniki. Każde trafia do biblioteki, którą Lapin już na to ma, więc używasz go tak, jakbyś zrobił go sam.

Otwórz **Plik → Rozszerzenia…**. **Przeglądaj** pokazuje katalog: szukaj po nazwie, tagu lub tym, co zawiera rozszerzenie, albo wybierz kategorię.

![Rozszerzenia: Przeglądaj, szczegóły pakietu mebli](shots/extensions-browse.webp)

Kliknij rozszerzenie, aby zobaczyć jego szczegóły przed instalacją:

- **Autor** i **Licencja**. Każde rozszerzenie ma licencję wybraną przez autora. *Liberalna* (MIT, CC-BY, CC0…) pozwala używać tego, co zawiera, w każdym projekcie; przy innej najpierw *przeczytaj warunki*.
- **Niekomercyjna**: rozszerzenie, którego licencja zabrania użytku komercyjnego (CC-BY-NC, PolyForm Noncommercial…), jest tak oznaczone na liście i w szczegółach. Możesz je zainstalować i uczyć się z niego, ale nie używać w płatnej pracy.
- **Działa z**: wersje Lapina, dla których je zrobiono. Jeśli nie pasuje do Twojego Lapina, mówi o tym zamiast oferować **Zainstaluj**.
- **Rozmiar**, plik i jego **SHA-256**: odcisk pliku, który sprawdził katalog.
- **Zawartość**: jego elementy, gdy autor je wymienia.
- **Strona** i **Wątek na forum**: gdzie zapytać autora.

## Instalacja, aktualizacja, odinstalowanie

Kliknij **Zainstaluj**. Lapin pobiera plik, sprawdza, czy jego rozmiar i SHA-256 są dokładnie takie, jak podaje katalog (plik zmieniony po drodze zostaje odrzucony i nic się nie instaluje), a potem wkłada go do swojej biblioteki:

| Rodzaj | Gdzie trafia |
|---|---|
| Meble | **Wnętrza → Moje meble…** |
| Szablony projektów | **Plik → Nowy z szablonu…** |
| Profile | biblioteki profili na tym komputerze |
| Wzory kreskowania | Moje wzory, oferowane w **Zarządzaj → Materiały** |
| Tabliczki | tabliczki na tym komputerze |
| Cenniki | **Realizacja → Cenniki…**, oznaczone jako orientacyjne: ceny są autora, porównaj je z własnymi |
| Typy | projekt otwarty w chwili instalacji, w jednym kroku do cofnięcia (**Zarządzaj → Pakiety typów…** eksportuje Twoje) |

Jeśli Twoja biblioteka ma już coś o tej samej nazwie, Lapin pyta przed zastąpieniem. Nic z tego, co przynosi rozszerzenie, nigdy nie jest uruchamiane: rozszerzenia to treść.

Katalog Lapina jest też **podpisany** przez Lapin Extensions: katalog i każdy jego plik niosą podpis, który Lapin sprawdza, zanim odczyta listę lub zainstaluje plik, więc kopia zmieniona po drodze jest odrzucana. Wiersz nad listą mówi **Podpisany przez Lapin Extensions**, gdy tak jest.

**Zainstalowane** pokazuje, co ma ten komputer. Każdy wiersz otwiera miejsce, gdzie to trafiło (**Moje meble…**, **Szablony projektu…**), oferuje **Aktualizuj**, gdy katalog ma nowszą wersję, oraz **Odinstaluj**.

![Zainstalowane: pakiet mebli z przyciskiem otwierającym Moje meble](shots/extensions-installed.webp)

![Typy mebli z pakietu w Moich meblach](shots/extensions-my-furniture.webp)

**Odinstaluj** usuwa z Twoich bibliotek to, co dodało rozszerzenie, i tylko to. To, co już umieściłeś w projektach, w nich zostaje: typ mebla wstawiony do projektu, projekt rozpoczęty z szablonu.

## Typy

Rozszerzenie **typów** przynosi typy ścian, stropów, drzwi, okien, słupów, belek lub schodów z tym, z czego są zrobione: ich warstwami, materiałami, profilami i parametrami. Po instalacji jego typy trafiają do projektu otwartego teraz, w jednym kroku do cofnięcia: materiał o tej samej nazwie, który już jest w projekcie, zostaje użyty, każdy inny jest tworzony; dokładnie taki sam typ, który już jest, zostaje użyty ponownie, a typ o tej samej nazwie, ale inny, jest dodany obok z (2) po nazwie. **Zainstalowane → Dodaj jej typy do tego projektu** przenosi je do innego projektu.

![Szczegóły rozszerzenia typów: jego licencja oznaczona jako niekomercyjna](shots/extensions-types.webp)

Twoje własne typy trafiają do pakietu w **Zarządzaj → Pakiety typów…**: zaznacz typy, nadaj pakietowi nazwę, a **Eksportuj typy…** zapisze plik `.lapintypes`. **Importuj typy…** w tym samym oknie czyta taki plik, pozwala wybrać jego typy i dodaje je w ten sam sposób.

![Zarządzaj → Pakiety typów…: typ ściany wybrany do eksportu](shots/type-packs-export.webp)

## Skrypty

Katalog zawiera też **skrypty Pythona**. W aplikacji na komputer możesz już jeden zainstalować; uruchomi się, gdy Lapin będzie miał konsolę Pythona, i tylko wtedy, gdy go uruchomisz. Przed instalacją jego szczegóły mówią, o co prosi (odczyt lub zmianę otwartego projektu, dostęp do plików lub internetu). Skrypt może na Twoim komputerze robić to, co Ty, więc instaluj skrypty autorów, którym ufasz. Przeglądarka przyjmuje tylko pakiety treści.

![Szczegóły skryptu: o co prosi i że nie można go jeszcze uruchomić](shots/extensions-script.webp)

## Inny katalog

**Katalog…** na dole okna ustawia adres katalogu: adres https albo ścieżkę do `index.json` na tym komputerze lub we wspólnym folderze (własny katalog firmy). Zostaw puste, aby użyć katalogu Lapina. Strzałka obok czyta katalog ponownie. Własny katalog nie musi być podpisany: Lapin mówi wtedy nad listą, że **nie jest podpisany przez Lapin Extensions**, więc instaluj z niego tylko, jeśli ufasz temu, kto go prowadzi. Katalog, którego podpis się nie zgadza, nigdy nie jest odczytywany.

## Publikowanie własnych

Katalog ma własną stronę, **[store.lapinbim.com](https://store.lapinbim.com)**: stronę każdego rozszerzenia z jego obrazami, wersjami, licencją i pobraniami oraz stronę każdego autora.

Każdy może opublikować rozszerzenie. Wyeksportuj je z Lapina (Moje meble → Eksportuj, Zapisz jako szablon…, biblioteka profili, tabliczka), a potem:

- **W sklepie**: zaloguj się na store.lapinbim.com kontem z [forum](https://forum.lapinbim.com), otwórz **Your extensions** (Twoje rozszerzenia), wypełnij formularz i prześlij plik oraz kilka zrzutów ekranu. Sklep sprawdza plik tak, jak zrobi to Lapin, i wysyła go do recenzji; Twoja strona pokazuje recenzję i, po opublikowaniu, ile razy rozszerzenie pobrano. Nowa wersja przechodzi tę samą drogą.
- **Na GitHubie**: dołącz plik do wydania (release) własnego repozytorium i dodaj jego manifest przez pull request w [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). Jego README mówi, jak to zrobić i co sprawdzają automatyczne testy.

Pakiet typów powstaje w **Zarządzaj → Pakiety typów… → Eksportuj typy…**; cennik w **Cenniki…**, przyciskiem eksportu cennika z tego komputera (plik `.lapinprices`, z cenami oznaczonymi jako orientacyjne).
