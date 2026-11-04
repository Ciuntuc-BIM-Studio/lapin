---
title: Materiały, tekstury i kreskowania
order: 90
summary: Ustaw kolor, kreskowania i spoiny każdego materiału, nałóż na niego własny obraz i wczytaj własne wzory kreskowania .pat.
---

**Zarządzaj → Materiały** wyświetla materiały projektu. Wybierz jeden, aby go zmienić.

![Okno dialogowe Materiały](shots/materials-dialog.webp)

## Wygląd: jak materiał się renderuje

Karta **Wygląd** ustala, jak materiał wygląda w 3D i w programach do renderingu, do których eksportujesz. Karta **Grafika 2D** zachowuje kreskowania i pióra jak dotąd.

![Wygląd materiału z podglądem na żywo](shots/material-appearance.webp)

- **Kolor** i **Tekstura**: tekstura rysowana (cegła, deski, płytki…) lub własny obraz. **W** i **H** ustalają, jak duży jest jeden kafel na modelu, w metrach; **Obrót** go obraca.
- **Chropowatość**: 0 to lustro, 1 to kreda. **Metaliczność**: 1 dla gołego metalu (aluminium anodowane, stal, chrom), 0 dla wszystkiego malowanego lub malowanego proszkowo, drewna i kamienia.
- **Przezroczystość**: ile widać przez materiał. **Szkło (transmisja)** i **IOR** (1,52 dla szkła) sprawiają, że szkło załamuje światło w programie do renderingu.
- **Świeci** wydziela światło, jak klosz lampy.

Kula od razu pokazuje wynik. Czego nie ustawisz, wynika z nazwy: materiał o nazwie *szkło* lub *glass* jest przezroczysty, *stal* lub *steel* metaliczna, *parkiet* i *panele laminowane* to deski. **Wygląd z nazwy** usuwa znowu Twoje wartości.

## Z biblioteki

**Z biblioteki…** oferuje gotowe materiały: metale (aluminium RAL 7016 antracyt, anodowane, stal, miedź…), szkło (bezbarwne, niebieskie, zielone, szare, matowe, refleksyjne), drewno w deskach, beton, cegłę, tynk i farbę, płytki i kamień, tworzywo i tkaninę. Kliknięcie dodaje jeden do projektu; zmień go jak każdy inny.

![Biblioteka materiałów](shots/material-library.webp)

## Jeden materiał dla wszystkich ościeżnic i całego szkła

Nowe drzwi i okna są zrobione ze wspólnych materiałów, część po części: **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** i **Hardware**. Zmień raz **Window frame**, a zmienią się wszystkie ościeżnice okien.

Aby użyć innych materiałów, otwórz **Architektura → Drzwi i okna**, wybierz dla typu **Materiał ościeżnicy**, **Materiał szkła** i pozostałe, a potem kliknij **Użyj dla wszystkich okien** (lub **Użyj dla wszystkich drzwi**). Każdy typ tego rodzaju je dostaje, w jednym kroku cofania. Ściany osłonowe też mają materiał ramy i materiał szkła.

![Materiały części w konfiguratorze drzwi i okien](shots/joinery-materials.webp)

## Duplikuj, zastąp, zaznacz

- **Duplikuj** kopiuje materiał pod nową nazwą.
- **Zastąp przez** materiał, potem **Zastąp**: każda warstwa, typ, drzwi, okno i element, które używały pierwszego, używają teraz drugiego, w jednym kroku cofania. **Zastąp i usuń** usuwa też pierwszy. Inaczej materiału w użyciu nie da się usunąć.
- **Zaznacz elementy** zaznacza wszystkie elementy z częścią z tego materiału.
- **Szukaj materiałów** i lista kategorii filtrują listę.

## Materiały do renderingu

Twinmotion, Lumion, Enscape, Blender i inne programy do renderingu podmieniają materiały po nazwie. Lapin zapisuje każdy materiał **raz, pod jego własną nazwą**, z kolorem, teksturą, chropowatością, metalicznością i szkłem, i dzieli siatkę każdego elementu według materiałów.

1. Nazwij materiały tak, jak chcesz je mieć w programie do renderingu, na przykład *Aluminium RAL 7016 anthracite* dla wszystkich ościeżnic i *Glass blue tinted* dla całego szkła.
2. **Plik → Eksportuj 3D (glTF, USD, DAE)…** i wybierz **GLB** (zalecane: jeden plik z teksturami, dokładnymi nazwami i szkłem), **USDZ** lub **OBJ + MTL** (plik .zip z teksturami; spacje w nazwach stają się podkreśleniami).
3. Zaimportuj plik do programu do renderingu. Wszystkie ościeżnice mają tam jeden materiał; podmień go raz na metal z programu, a zmienią się wszystkie.

![Dom z ciemnoszarymi ościeżnicami i niebieskim szkłem](shots/materials-render-3d.webp)

Eksport IFC także nazywa styl powierzchni każdego materiału jego nazwą, z przezroczystością i sposobem odbijania światła.

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
