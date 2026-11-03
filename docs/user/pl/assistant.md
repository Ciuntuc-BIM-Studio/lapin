---
title: Asystent modelu
order: 127
summary: Pytaj lokalny model AI o swój projekt, otrzymuj krótkie raporty i pozwól mu proponować wartości właściwości, które zatwierdzasz. Tylko w aplikacji desktopowej.
---

**Asystent** odpowiada na pytania o otwarty projekt zadane twoimi własnymi słowami: ile ścian ma każda kondygnacja i jaka jest ich długość, które drzwi są szersze niż 1 m, które nadal nie mają odporności ogniowej. Przygotowuje krótkie raporty w postaci tabel, które możesz wyeksportować, i potrafi **proponować** wartości właściwości (odporność ogniową, status, twoje własne parametry), które przeglądasz i stosujesz.

Działa na modelu językowym uruchomionym **na twoim komputerze**, przez [Ollama](https://ollama.com). Nic o twoim projekcie nie opuszcza komputera i nic nie jest wysyłane ani do nas, ani do nikogo innego.

> Asystent jest dostępny tylko w **aplikacji desktopowej**. Przeglądarkowa wersja Lapin nie ma asystenta.

## Co potrafi, a czego nie

**Potrafi**:

- czytać projekt: kondygnacje, elementy, ich typy, materiały, zestawy właściwości i ilości oraz zestawienia;
- liczyć, sumować i uśredniać (długości, powierzchnie, objętości, dowolne ilości), z grupowaniem według kondygnacji, typu lub dowolnej właściwości. Liczby oblicza Lapin; model tylko je przekazuje;
- proponować wartości właściwości w standardowych zestawach IFC (`Pset_DoorCommon.FireRating`, `Pset_WallCommon.IsExternal`…) lub w twoich własnych.

**Nie potrafi**:

- rysować, przesuwać, zmieniać kształtu, tworzyć ani usuwać czegokolwiek. Nie zmienia geometrii;
- uruchamiać poleceń ani pisać kodu;
- stosować niczego samodzielnie. Każda proponowana wartość czeka na ciebie.

Model lokalny może źle zrozumieć pytanie. Przeczytaj jego odpowiedź i sprawdź proponowane wartości, zanim je zastosujesz.

## Konfiguracja

1. Zainstaluj Ollama ze strony [ollama.com](https://ollama.com) i uruchom ją (aplikację Ollama albo `ollama serve` w terminalu).
2. Zainstaluj model. Najlepiej sprawdził się w naszych testach **qwen3-coder:30b** (szybki i niezawodny z narzędziami Lapin, potrzebuje około 20 GB pamięci):

   ```sh
   ollama pull qwen3-coder:30b
   ```

   Na komputerze z mniejszą ilością pamięci działa też **gemma4:12b**, ale wolniej.
3. W Lapin otwórz **Asystent**: kartę **Asystent** obok **Właściwości** po prawej, **Widok → Asystent** albo <kbd>Ctrl</kbd>+<kbd>K</kbd> i wpisz *Asystent*.

Lapin znajduje Ollama pod adresem `http://127.0.0.1:11434` i wybiera najlepszy model, jaki masz. Przycisk z kołem zębatym u góry panelu pokazuje **Adres Ollamy**, **model**, który ma być używany, oraz to, czy Ollama działa. Akceptowana jest tylko Ollama na tym komputerze.

## Pytaj

Wpisz pytanie i naciśnij <kbd>Enter</kbd> (<kbd>Shift</kbd>+<kbd>Enter</kbd>, aby przejść do nowego wiersza). Pytaj po angielsku, po rumuńsku lub w innym języku: odpowiedź przyjdzie w tym samym.

![Asystent: raport według kondygnacji i odporność ogniowa zaproponowana dla drzwi, czekająca na zatwierdzenie](shots/assistant.webp)

Podczas pracy panel pokazuje każdy krok w jednym wierszu, na przykład *Listed 12 of 12 · IfcDoor* lub *Computed 2 group(s) over 40 element(s)*. Najedź na krok kursorem, aby zobaczyć dokładnie, o co zapytał. **Zatrzymaj** kończy odpowiedź; **Nowa rozmowa** zaczyna od nowa.

Listy i sumy pojawiają się też jako **tabele** pod odpowiedzią. Przycisk pobierania przy tabeli zapisuje ją jako plik CSV do arkusza kalkulacyjnego.

Kilka pytań na początek:

- *Ile ścian jest na każdej kondygnacji i jaka jest ich łączna długość?*
- *Wypisz okna na pierwszym piętrze z ich szerokością i wysokością.*
- *Które drzwi nie mają odporności ogniowej?*

## Zatwierdzaj proponowane wartości

Zapytaj na przykład: *Drzwi szersze niż 1 m: nadaj im FireRating EI30.* Asystent znajduje drzwi i proponuje wartości. Pojawiają się one w sekcji **Proponowane zmiany**: każdy element, zestaw i nazwa właściwości, jej obecna i nowa wartość, z polem wyboru.

- Odznacz to, czego nie chcesz; pole w nagłówku zaznacza lub odznacza wszystko.
- Kliknij nazwę elementu, aby zaznaczyć go w widokach.
- **Zastosuj** zapisuje zaznaczone wartości, wszystkie razem, jako **jeden** krok: <kbd>Ctrl</kbd>+<kbd>Z</kbd> cofa je wszystkie.
- **Odrzuć** je porzuca.

Wartości trafiają do parametrów elementów, widocznych w panelu **Właściwości** wraz z ich zestawem, oraz do eksportu IFC: właściwość standardowa dołącza do swojego zestawu (`Pset_DoorCommon`), twoje własne trafiają do osobnego zestawu.

Zasady, których przestrzega asystent (a Lapin sprawdza): standardowy zestaw IFC (zaczynający się od `Pset_`) przyjmuje tylko własne nazwy właściwości, na elementach, dla których jest przeznaczony; twój własny zestaw nigdy nie zaczyna się od `Pset_`.
