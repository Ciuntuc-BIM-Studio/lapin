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

Jeśli Twoja biblioteka ma już coś o tej samej nazwie, Lapin pyta przed zastąpieniem. Nic z tego, co przynosi rozszerzenie, nigdy nie jest uruchamiane: rozszerzenia to treść.

**Zainstalowane** pokazuje, co ma ten komputer. Każdy wiersz otwiera miejsce, gdzie to trafiło (**Moje meble…**, **Szablony projektu…**), oferuje **Aktualizuj**, gdy katalog ma nowszą wersję, oraz **Odinstaluj**.

![Zainstalowane: pakiet mebli z przyciskiem otwierającym Moje meble](shots/extensions-installed.webp)

![Typy mebli z pakietu w Moich meblach](shots/extensions-my-furniture.webp)

**Odinstaluj** usuwa z Twoich bibliotek to, co dodało rozszerzenie, i tylko to. To, co już umieściłeś w projektach, w nich zostaje: typ mebla wstawiony do projektu, projekt rozpoczęty z szablonu.

## Skrypty

Katalog zawiera też **skrypty Pythona**. W aplikacji na komputer możesz już jeden zainstalować; uruchomi się, gdy Lapin będzie miał konsolę Pythona, i tylko wtedy, gdy go uruchomisz. Przed instalacją jego szczegóły mówią, o co prosi (odczyt lub zmianę otwartego projektu, dostęp do plików lub internetu). Skrypt może na Twoim komputerze robić to, co Ty, więc instaluj skrypty autorów, którym ufasz. Przeglądarka przyjmuje tylko pakiety treści.

![Szczegóły skryptu: o co prosi i że nie można go jeszcze uruchomić](shots/extensions-script.webp)

## Inny katalog

**Katalog…** na dole okna ustawia adres katalogu: adres https albo ścieżkę do `index.json` na tym komputerze lub we wspólnym folderze (własny katalog firmy). Zostaw puste, aby użyć katalogu Lapina. Strzałka obok czyta katalog ponownie.

## Publikowanie własnych

Każdy może opublikować rozszerzenie: wyeksportuj je z Lapina (Moje meble → Eksportuj, Zapisz jako szablon…, biblioteka profili, tabliczka), dołącz plik do wydania (release) własnego repozytorium GitHub i dodaj jego manifest do katalogu przez pull request w [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). Jego README mówi, jak to zrobić i co sprawdzają automatyczne testy.
