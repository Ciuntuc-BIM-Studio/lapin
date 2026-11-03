---
title: Pierwsze kroki
order: 10
summary: Ekran startowy, nowy projekt, części okna, poruszanie się po rysunku i wybór jednostek.
---

## Ekran startowy

Po uruchomieniu Lapin pokazuje ekran startowy. **Nowy projekt** zaczyna pusty budynek. **Otwórz projekt…** otwiera plik `.lapin`. Projekty otwierane wcześniej są wymienione w sekcji **Ostatnie**.

**Zacznij od** daje nowy projekt przygotowany do swojej pracy: **Dom parterowy** (fundamenty i parter), **Dom piętrowy** (oraz piętro), **Budynek wielorodzinny** (parter i cztery piętra), **Hala lub szkielet** (jedna kondygnacja wysoka na 6 m, w obszarze roboczym konstrukcji), **Tylko rysunek** (jeden poziom, w obszarze roboczym rysunku 2D) i **Zacznij od IFC** (nowy projekt, a potem plik IFC do zaimportowania). Każdy wybiera też swój obszar roboczy; poziomy można potem zmieniać, dodawać i usuwać jak w każdym projekcie.

![Ekran startowy](shots/start-screen.webp)

Nowy projekt ma już trzy poziomy: **Fundamenty** (−1.50), **Parter** (±0.00) i **Piętro** (+3.00). Otwiera się na rzucie parteru, gotowy na pierwszą ścianę.

## Okno

![Części okna Lapina](shots/workspace.webp)

1. **Karty wstążki.** Rysunek, Architektura, Wykończenia, Konstrukcja, Zagospodarowanie, Studio, Opisz, Zarządzaj, Ilości i Widok grupują swoje narzędzia. **Plik** otwiera, zapisuje, importuje i eksportuje. **Cofnij** i **Ponów** są na prawym końcu tego samego paska (<kbd>Ctrl</kbd> <kbd>Z</kbd>, <kbd>Ctrl</kbd> <kbd>Y</kbd>).
2. **Wstążka.** Zawiera narzędzia bieżącej karty. Najedź na narzędzie, aby zobaczyć jego nazwę i litery skrótu.
3. **Przeglądarka projektu.** Wymienia poziomy, rzuty, przekroje, elewacje, widoki 3D, arkusze i zestawienia. Kliknij dwukrotnie pozycję, aby otworzyć ją w karcie. Projekt otwiera się na rzucie parteru, z widokiem 3D w karcie obok.
4. **Widok.** Tu rysujesz: rzut, przekrój, widok 3D lub arkusz. Kilka widoków obok siebie otworzysz dwoma przyciskami w prawym górnym rogu widoku.
5. **Pasek szybkiego dostępu.** Daje najczęściej używane narzędzia pod klawiszami cyfr <kbd>1</kbd>–<kbd>9</kbd>.
6. **Wiersz poleceń.** Wpisz tu litery narzędzia i naciśnij <kbd>Enter</kbd>. Gdy narzędzie działa, wiersz mówi, czego potrzebuje dalej. Zobacz [Rysowanie i wiersz poleceń](drafting.md).
7. **Panel Właściwości.** Pokazuje to, co jest zaznaczone. Gdy nic nie jest zaznaczone, pokazuje widok na wierzchu.
8. **Zapytaj mnie.** To asystent pomocy Lapina. Zobacz [Pomoc i opinie](help.md).

Potrzebujesz więcej miejsca na rysunek? **Widok → Interfejs…** wybiera bardziej zwarte okno, a panele boczne można zwinąć. Zobacz [Interfejs i skróty](interface.md).

## Poruszanie się

- **Powiększanie.** Obróć kółko myszy lub zsuń palce na gładziku. Lapin powiększa w miejscu kursora.
- **Przesuwanie widoku.** Przeciągnij środkowym lub prawym przyciskiem myszy albo dwoma palcami na gładziku.
- **Obracanie w 3D.** Przeciągnij lewym przyciskiem myszy.
- **Pokaż wszystko.** Naciśnij przycisk **Dopasuj**, wpisz <kbd>ZE</kbd> lub kliknij dwukrotnie środkowym przyciskiem.

## Jednostki

Lapin pracuje w milimetrach, chyba że wybierzesz inaczej. Aby zmienić jednostki, otwórz **Zarządzaj → Standardy rysunkowe → Projekt → Jednostki** lub kliknij **Jednostki** na pasku stanu u dołu okna. Możesz wybrać milimetry, centymetry, metry, stopy i cale albo cale.

![Standardy rysunkowe, karta Projekt: jednostki projektu](shots/standards-project.webp)

Każda długość stosuje się do tego ustawienia: panele, wartości wpisywane podczas rysowania, wiersz poleceń i wymiary. Sam model jest przechowywany w metrach, więc zmiana jednostek nigdy nie zmienia budynku.

## Zapisywanie

**Plik → Zapisz** (<kbd>Ctrl</kbd> <kbd>S</kbd>) zapisuje plik `.lapin`. Zobacz [Pliki, wersje i odzyskiwanie](files.md).
