---
title: Skrypty w Pythonie
order: 127
summary: Sterowanie Lapinem z Pythona w panelu Script, dodatku dla programistów: konsola i edytor, które uruchamiają twojego Pythona, każde uruchomienie to jeden krok cofania.
---

## Włącz Dodatki dla programistów

Lapinem można sterować z Pythona: każde narzędzie jest poleceniem, a skrypt wysyła te same polecenia. Panel **Script** to *dodatek dla programistów*, wyłączony, dopóki go nie włączysz, więc inaczej nic z niego nie widać.

Otwórz **Plik → Preferencje…** i w **Ogólne** zaznacz **Dodatki dla programistów**.

![Preferencje: Dodatki dla programistów](shots/python-preferences.webp)

- **Python**: Python, który uruchamia panel. Puste: Lapin szuka go na tym komputerze (`python3`, `py -3` w Windows, ten z python.org i ten z Homebrew). Musi to być **Python 3.11 lub nowszy**; Lapin nie instaluje Pythona. Pobierz go z [python.org](https://www.python.org/downloads/), jeśli go nie masz, albo wpisz ścieżkę do tego, którego chcesz (na przykład z wirtualnego środowiska).
- **Zewnętrzny edytor**: edytor, którego używa **Otwórz w zewnętrznym edytorze**, jako polecenie (`code` dla Visual Studio Code). Puste: edytor tekstu systemu.

Python działa tylko w aplikacji na komputer: w Lapinie w przeglądarce panel to mówi.

## Panel Script

Otwórz **Widok → Script**. Panel otwiera się wzdłuż dolnej krawędzi okna; przeciągnij jego górną krawędź, by był wyższy. Jego nagłówek mówi, jakiego Pythona znalazł albo dlaczego nie znalazł żadnego.

![Panel Script po uruchomieniu: cztery ściany, jeden krok cofania](shots/python-panel.webp)

- **Moje**, po lewej: twoje własne skrypty, zwykłe pliki `.py` w folderze Lapina na tym komputerze. Kliknij jeden, by go otworzyć; **Nowy skrypt** zaczyna od przykładu.
- **Edytor**, pośrodku: Python z kolorami i wcięciami. Wpisz `lapin.`, a podpowie, co ma Lapin (polecenia wszystkich modułów twojej edycji i własne wywołania); `lapin.architecture.` podpowiada polecenia architektury z ich argumentami. Nadaj skryptowi nazwę i **zapisz** go w Moich; **Otwórz w zewnętrznym edytorze** otwiera jego plik w twoim edytorze, a to, co tam zapiszesz, wraca do panelu.
- **Konsola**, po prawej: to, co wypisują skrypty, ich błędy z wierszem, na którym się zatrzymały (zaznaczonym w edytorze), oraz wiersz do wpisywania Pythona. **Enter** go uruchamia, **Shift+Enter** zaczyna nowy wiersz, **Strzałka w górę** i **w dół** przywołują wcześniejsze wiersze. Nazwa ustawiona w konsoli zostaje na następny wiersz i dla uruchamianych skryptów.

W panelu `lapin` to już wczytany pakiet `lapinbim`. Ten sam skrypt działa z terminala z `import lapinbim as lapin`, gdy Lapin jest otwarty.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Długości są w metrach, a kąty w stopniach, niezależnie od jednostki, w jakiej pokazuje je projekt.

## Uruchom, Zatrzymaj, Uruchom ponownie, Cofnij

- **Uruchom** (**Ctrl+Enter** w edytorze) uruchamia skrypt. Wszystko, co robi, to **jeden krok cofania**, nazwany jak skrypt: jedno **Cofnij** zabiera wszystko.

  ![Jedno Cofnij zabrało cztery ściany z uruchomienia](shots/python-undo.webp)

- Gdy skrypt działa, model należy do skryptu: narzędzie użyte w oknie zostaje odrzucone z komunikatem *A script is editing the model*; widoki, zaznaczenie i zestawienia działają dalej.
- Skrypt, który się nie powiedzie, nie zachowuje **niczego** z tego, co zrobił: model jest taki jak przed **Uruchom**, a konsola pokazuje błąd i jego wiersz.
- **Zatrzymaj** zatrzymuje skrypt; nic z tego, co zrobił, nie zostaje. Skryptowi, który nie zatrzyma się w ciągu dwóch sekund, zamyka się Pythona.
- **Uruchom ponownie** uruchamia Pythona od nowa: nazwy zapamiętane przez konsolę zostają zapomniane.

## Nagraj to, co robisz, jako skrypt

Karta **Rejestrator**, obok konsoli, zapisuje to, co robisz w oknie, jako Python, jedna linia na krok.

1. Kliknij **Nagraj**. Czerwona kropka na karcie pokazuje, że trwa nagrywanie.
2. Pracuj jak zwykle: rysuj ściany, zmień właściwość, usuń coś. Każdy krok od razu pojawia się jako linia, na przykład `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Kliknij **Zatrzymaj**, a potem **Kopiuj** (aby wkleić skrypt gdzie indziej) albo **Zapisz jako skrypt** (trafia do Moje i otwiera się w edytorze).

![Rejestrator po narysowaniu dwóch ścian](shots/python-recorder.webp)

Skrypt jest zapisany tak, aby działał także w innym projekcie:
- to, co utworzył krok, jest trzymane w zmiennej (`wall1`), której używają kolejne kroki (`wall1.id`);
- kondygnacja, na której rysowałeś, jest znajdowana po nazwie, a jeśli jej brak, po rzędnej (`lapin.model.storey("Ground floor", 0)`), a typ po nazwie (`lapin.model.find("IfcWallType", "Brick 250")`);
- inny element, który już był w projekcie, jest wskazany swoim id (`lapin.model["…"]`), więc ta linia działa tylko w tym projekcie;
- gdy zaznaczone jest **Zaznaczenie jako lapin.selection()**, polecenie na tym, co było zaznaczone, jest zapisane jako `lapin.selection()`: skrypt działa wtedy na tym, co jest zaznaczone w chwili uruchomienia.

**Cofnij** podczas nagrywania zdejmuje ze skryptu ostatni krok, a **Ponów** go przywraca, więc skrypt zawiera to, co ostatecznie zrobiłeś. To, co robi skrypt, nigdy nie jest nagrywane, tak samo jak zapytania, które nic nie zmieniają (odczyt zestawienia, pomiar).

![Nagrany skrypt uruchomiony w nowym projekcie: te same dwie ściany](shots/python-recorder-new.webp)

## Skrypty projektu

Skrypt można przechowywać **w projekcie**, aby wędrował razem z plikiem: otwórz go lub napisz w edytorze i kliknij **Zapisz w projekcie**. Pojawia się pod **Projektu**, po lewej. Zapisanie, zmiana nazwy i usunięcie skryptu projektu to każde jeden krok cofania, a wersje projektu zachowują jego skrypty. W pliku `.lapin` każdy z nich jest też zwykłym plikiem tekstowym, `scripts/<name>.py`, więc można go odczytać bez Lapina.

**Skrypt projektu nigdy nie uruchamia się sam**, ani przy otwarciu projektu, ani przy jego zapisie. Przy pierwszym uruchomieniu i potem za każdym razem, gdy jego kod się zmienił, Lapin pyta:
- **Uruchom raz**: uruchamia się tym razem, a następne uruchomienie znów pyta;
- **Zaufaj**: uruchamia się, a ten kod jest zapamiętany jako zaufany na tym komputerze (jego zmiana znów pyta);
- **Anuluj**: nic się nie uruchamia.

Skrypt, który sam zapisujesz w projekcie, jest zaufany od chwili zapisu. Twoje własne skrypty (Moje) nigdy nie pytają.

## Co jest zaznaczone

`lapin.selection()` zwraca to, co jest zaznaczone w oknie, jako elementy:

```python
for e in lapin.selection():
    print(e.name, e.type)
```

Pola elementu odczytuje się po nazwach, a zmienia przez przypisanie: `wall.height = 3` zmienia ścianę (jeden krok cofania albo część Uruchomienia). `with lapin.step("Name"):` robi z kilku poleceń jeden krok cofania, a każde nadal odpowiada od razu, więc późniejsze może użyć tego, co utworzyło wcześniejsze.

## Bezpieczeństwo

Skrypt może zrobić wszystko, co możesz zrobić na tym komputerze: działa w twoim własnym Pythonie, z twoimi uprawnieniami. Uruchamiaj skrypty, które sam napisałeś lub którym ufasz; skrypt projektu najpierw pyta.

Gość wspólnej sesji nie może uruchamiać skryptów na twoim komputerze ani wstawić żadnego do twojego projektu, a skrypt łączy się tylko z Lapinem na własnym komputerze.

## Dla programistów

To, co oferuje `lapin` (polecenia, zapytania, partie, model, zdarzenia), opisuje przewodnik dla programistów *Lapin from Python* (`docs/python.md` w repozytorium Lapina); pakiet jest w PyPI jako `lapinbim`.
