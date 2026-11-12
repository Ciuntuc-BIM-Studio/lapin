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

## Bezpieczeństwo

Skrypt może zrobić wszystko, co ty możesz na tym komputerze: działa w twoim Pythonie, z twoimi uprawnieniami. Uruchamiaj skrypty napisane przez siebie albo takie, którym ufasz.

Gość wspólnej sesji nie może uruchamiać skryptów na twoim komputerze, a skrypt łączy się tylko z Lapinem na własnym komputerze.

## Dla programistów

To, co oferuje `lapin` (polecenia, zapytania, partie, model, zdarzenia), opisuje przewodnik dla programistów *Lapin from Python* (`docs/python.md` w repozytorium Lapina); pakiet jest w PyPI jako `lapinbim`.
