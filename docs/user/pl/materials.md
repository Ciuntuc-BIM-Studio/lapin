---
title: Materiały, tekstury i kreskowania
order: 90
summary: Ustaw kolor, kreskowania i spoiny każdego materiału, nałóż na niego własny obraz i wczytaj własne wzory kreskowania .pat.
---

**Zarządzaj → Materiały** wyświetla materiały projektu. Wybierz jeden, aby go zmienić.

![Okno dialogowe Materiały](shots/materials-dialog.webp)

## Co ustawia materiał

- **Kolor (3D)** i powierzchnie rysowane na nim w widoku 3D. Lapin odgaduje je z nazwy, na przykład warstwy cegieł lub siatkę płytek. **Końce** oraz **Góra i dół** mogą wyglądać inaczej niż powierzchnie.
- **Kreskowanie** dla każdego miejsca, w którym materiał jest rysowany: przecięty w przekroju, przecięty na rzucie i widoczny na elewacji. Każde ma swoją skalę, kąt, wyrównanie, wypełnienie i pióro.
- **Kategoria**, na przykład wykończenie, izolacja lub konstrukcja. Style widoku używają jej, aby pominąć wykończenia w widoku konstrukcyjnym.
- **Membrana** rysuje cienką warstwę jako linię przerywaną zamiast pasa.
- **Rysuj spoiny.** Domyślnie materiał przechodzi z jednego elementu w następny bez linii, jak dwie ściany w jednej linii lub tynk na dwóch kondygnacjach. Zaznacz, aby rysować spoinę.

## Własny obraz na materiale

Materiał może mieć Twój własny obraz: zdjęcie prawdziwej cegły, drewna lub kamienia.

1. Wybierz materiał i kliknij **Własny obraz → Importuj obraz…**. Wybierz plik PNG lub JPEG.
2. Ustaw **W** i **H**: jak szeroka i jak wysoka, w metrach, jest na modelu jedna kopia obrazu. Obraz powtarza się na powierzchniach.

![Własny obraz materiału, nałożony w rozmiarze 1,2 × 1,2 m](shots/own-image.webp)

Obraz jest widoczny w widoku 3D i zapisywany w pliku projektu. Trafia też do eksportów 3D: **GLB**, **glTF** i **USDZ** go zawierają. **DAE** i **USDA** to pojedyncze pliki bez miejsca na obrazy, więc zachowują kolor materiału.

![Ten sam dom z własnym obrazem materiału w widoku 3D](shots/own-image-3d.webp)

Obrazy większe niż 2048 pikseli są przy imporcie zmniejszane do tego rozmiaru. Przycisk kosza usuwa obraz z materiału.

## Własne wzory kreskowania (.pat)

**Importuj .pat** wczytuje plik wzorów kreskowania AutoCAD. Każdy zawarty w nim wzór jest dodawany do projektu, gotowy do wybrania jako kreskowanie materiału.

Lapin przechowuje też plik w folderze **Moje wzory** na Twoim komputerze. W każdym innym projekcie **Dodaj do tego projektu** ponownie wczytuje te wzory. Projekt zachowuje używane wzory, więc otwiera się tak samo na innym komputerze.

![Moje wzory: zaimportowane pliki .pat, przechowywane dla każdego projektu](shots/my-patterns.webp)
