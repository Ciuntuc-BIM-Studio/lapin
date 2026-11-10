---
title: Biblioteki profili
order: 57
summary: Profile potrzebne w Twojej pracy, według dowolnej normy — mały zestaw EN Lapina, Twoje tabele i pliki IFC, foldery firmy; narysowane, z obliczonymi właściwościami i sprawdzonymi wartościami z pliku, kopiowane do projektu w miarę użycia.
---

Lapin nie może zawierać wszystkich profili stalowych świata, więc ma mały zestaw europejski, a resztę czyta z tego, co już masz: katalog jako arkusz (AISC Shapes Database, tabela producenta, tabela Twojej firmy), biblioteka profili IFC (np. Bonsai) lub dowolny model IFC, zamknięty kontur narysowany w Lapinie albo w DXF. **Zarządzaj → Biblioteki profili** zbiera je wszystkie.

![Biblioteki profili](shots/profile-libraries.webp)

## Biblioteki

- **Wbudowana**: *EN steel (Lapin)*: IPE, HEA, HEB, HEM, UPN, UPE, kątowniki równo- i nierównoramienne, wykańczane na gorąco SHS, RHS i CHS w typowych rozmiarach oraz ogólne gięte na zimno C, Z i Σ. Tylko wymiary normowe; wszystkie właściwości oblicza Lapin.
- **Na tym komputerze**: zaimportowane biblioteki, dla każdego projektu. Wersja przeglądarkowa też je przechowuje, w tej przeglądarce.
- **Foldery firmy**: w aplikacji desktopowej dowolnie wiele folderów lokalnych, sieciowych lub synchronizowanych z plikami `.lapinprofiles` (**Dodaj folder…**). Folder jest tylko do odczytu, dopóki nie zaznaczysz **do zapisu**; wtedy przycisk zapisu obok zapisuje tam wybraną bibliotekę, dla kolegów.
- **W projekcie**: kopiowane są tylko używane profile, z nazwą ich biblioteki, by plik otwierał się wszędzie.

Pisz w polu wyszukiwania, aby szukać we wszystkich bibliotekach po nazwie, rodzinie, normie lub rodzaju; wybierz profil, aby zobaczyć jego rysunek, wymiary i właściwości.

## Normy

Każdy profil jest pokazany pod swoją normą: **EN** dla zestawu Lapina i każdego profilu, którego norma jest europejska (EN 10365, DIN EN, SR EN…), **AISC / ASTM** dla tabel amerykańskich (na przykład kolumna *Standard* z ASTM A6), BS, DIN, JIS, AS/NZS i inne pod swoimi nazwami, a biblioteka, której profile nie podają żadnej — biblioteka firmy — pod własną nazwą. Bibliotece na tym komputerze można wskazać, pod którą ma się pokazywać (**Pokazana pod**, nad jej listą).

Listy wyboru pokazują najpierw profile projektu, potem każdą normę pod jej nagłówkiem. Przyciski u góry listy ukrywają lub pokazują normę; twój wybór jest zapamiętany na tym komputerze, dla każdego projektu (także w **Plik → Preferencje → Profile**).

![Lista wyboru pogrupowana według norm, z przyciskami](shots/profile-picker-standards.webp)

Projekt przechowuje tylko profile używane przez jego typy, nigdy całej biblioteki, więc biblioteka, którą masz, nic nie waży w jego pliku. To, co projekt oferuje, jest jego wyborem: **Normy w tym projekcie**, pod bibliotekami, zaznacza normy pokazywane przez jego listy wyboru; profile już w nim zostają. **Plik → Preferencje → Profile** wybiera normy, z którymi zaczyna się nowy projekt (wszystkie, jeśli nie wybierzesz).

![Normy oferowane przez projekt](shots/profile-project-standards.webp)

![Normy oferowane przez nowy projekt](shots/prefs-profiles.webp)

Lapin nie zawiera AISC Shapes Database: AISC pozwala ją dołączać tylko na podstawie własnej umowy licencyjnej, której program MIT nie może przekazać dalej. Pobierz ją z AISC i zaimportuj przez **Tabela…**: jej kolumny są rozpoznawane.

## Właściwości

Z wymiarów Lapin oblicza pole, Iy, Iz, wskaźniki sprężyste i plastyczne, promienie bezwładności, osie główne i ich kąt (kątowniki, Z, każdy kontur niesymetryczny), stałą skręcania It i wycinkowy moment bezwładności Iw tam, gdzie jest wzór, masę na metr z gęstości materiału i powierzchnię malowania na metr. Zgadzają się z publikowanymi tabelami w granicach ok. 1 % (It i Iw walcowanych I do 3 %; Iw ceownika z pochylonymi półkami tylko do 20 %). Dowolny kontur nie ma It ani Iw, chyba że poda je jego plik.

Wartości z pliku są zachowywane i pokazywane obok wartości Lapina. Różnica ponad 2 % (pole, masa), 3 % (momenty bezwładności, wskaźniki) lub 10 % (It, Iw) jest oznaczana i trafia na listę kontrolną importu; o całej kolumnie przesuniętej o potęgę dziesięciu Lapin informuje, że jest w innej jednostce.

![Wartość z pliku różna od wartości Lapina](shots/profile-check.webp)

## Import tabeli

**Tabela…** czyta CSV lub XLSX, jeden wiersz na profil. Znaczenie i jednostka każdej kolumny są odgadywane z nagłówka po angielsku, rumuńsku, niemiecku lub francusku (h, d, Höhe, hauteur, înălțime; tw, s, Stegdicke; A, Iy, Wel, Wpl, It, Iw, G, kg/m…), wraz z jednostkami (mm, cm, in, cm², cm⁴, in⁴, ×10³ cm⁶, lb/ft). Tabela z Ix bez Iz jest czytana po amerykańsku: x to oś mocna, w calach. Popraw w oknie to, co błędne; Lapin zapamięta wybór dla następnego pliku o takim samym układzie.

![Dopasowane kolumny tabeli](shots/profile-table.webp)

Rodzaj wiersza wynika z kolumny typu (W, HP, UPN, HSS, PIPE…) lub z nazwy; dla pozostałych wybierz go w oknie.

## IFC

**IFC…** czyta wszystkie definicje profili z dowolnego pliku IFC: biblioteki albo modelu z Tekli, Revita czy ArchiCAD-a, gdzie profile używane przez elementy biorą nazwy od ich typów. I, I niesymetryczny, U, T, L, C, Z, rury, prostokąt, koło i elipsa zachowują rodzaj i wymiary; dowolne, złożone i pochodne stają się konturami. Przychodzą też właściwości mechaniczne z `Pset_ProfileMechanical` (i właściwości profili IFC2x3); eksportery nie są zgodne co do jednostek ani osi Y, więc Lapin rozstrzyga oba na podstawie geometrii. Profile osiowe i trapezowe są pomijane, o czym Lapin informuje.

![Profile odczytane z pliku IFC](shots/profile-ifc.webp)

**IFC** i **CSV** pod listą zapisują wybraną bibliotekę (lub profile projektu) jako bibliotekę profili IFC4 albo tabelę, którą Lapin odczyta z powrotem.

Także w IFC modelu belki, słupy i profile stalowe zapisywane są przez profil parametryczny (`IfcIShapeProfileDef`, `IfcLShapeProfileDef`, …, prostokąty i koła), z nazwą profilu i `Pset_ProfileMechanical`, więc inne programy widzą IPE 300, a nie polilinię. Obrysy, Σ i Z z wywinięciami pozostają profilami dowolnymi; element przecięty przez inny pozostaje bryłą, którą jest.

## Kontur jako profil

Narysuj przekrój w Rysowaniu jako zamknięte polilinie i okręgi (te wewnątrz innego to otwory), zaznacz je i naciśnij **Z zaznaczenia** albo odczytaj rysunek producenta przez **DXF…**. Nazwij go, wybierz materiał i bibliotekę i zachowaj. Tak trafiają tu profile aluminiowe i PVC, przekroje prefabrykowane i kształty drewniane: ten sam magazyn, z własnym materiałem i gęstością.

![Narysowany kontur jako profil](shots/profile-outline.webp)

**Narysuj kontur** działa odwrotnie: kontur wybranego profilu jako zamknięte polilinie w aktywnym widoku, do wyciągnięcia lub przeciągnięcia w Studio.

**Narysuj profil…** otwiera edytor przekrojów: obrys zewnętrzny i jego komory jako punkty w milimetrach. Przeciągaj punkty (1 mm; <kbd>Shift</kbd> 5 mm), kliknij obrys, aby dodać punkt, kliknij punkt prawym przyciskiem, aby go usunąć, albo wpisz je w tabeli; zacznij od prostokąta, pustej skrzynki, kształtownika T, kątownika lub ceownika o podanych wymiarach i dodaj komory pośrodku. Nazwij go, wybierz aluminium, utwórz go i zapisz jak każdy kontur. **Edytuj jego obrys…** otwiera w edytorze profil, który masz: zapisany znowu pod swoją nazwą, kopia w projekcie jest oznaczana jako nowsza, a **Aktualizuj z biblioteki** przenosi go do każdego typu i każdego pręta ściany osłonowej z niego wykonanego. Wybrane słupki i rygle dostają własny profil, obrócony, wyrównany i przesunięty: zobacz [Ściany](walls.md).

![Edytor przekrojów: pusta skrzynka z komorą](shots/profile-editor.webp)

## Użycie profilu

W **Stal → Typy stalowe**, **Konstrukcja → Typy konstrukcyjne** (słupy i belki) oraz przy profilach hali ramowej wybierz **Z biblioteki profili…** i pisz, aby szukać.

![Wybór przeszukuje wszystkie biblioteki](shots/profile-picker.webp)

Profil jest kopiowany do projektu, a typ go zachowuje. Bryły są lekkie, z ostrymi narożami, na każdym poziomie oprócz jednego: widok na **LOD 400** (rzut, przekrój, elewacja, arkusz lub 3D) rysuje rzeczywisty przekrój z zaokrągleniami, promieniami na końcach półek i pochylonymi półkami. Eksporty i wybór pozostają na bryle z ostrymi narożami, ale ilości, masy, przedmiary, lista elementów stalowych i kosztorysy używają rzeczywistego pola przekroju (IPE 300 ma 53,81 cm², a nie 51,88 jak ostry obrys); przekrój bez promieni się nie zmienia.

![Belka i słup stalowy w widoku 3D na LOD 400: narysowane zaokrąglenia](shots/profile-real-section.webp)

Gdzie indziej profil daje to, co narzędzie może wykorzystać: listwy, bruzdy i ławy fundamentowe biorą jego obrys jako profil 2D (**Z biblioteki profili…** na ich liście), a słupki i rygle ścian osłonowych oraz ościeżnice i skrzydła drzwi i okien jego rzeczywisty obrys, z otworami, wzdłuż każdego pręta, z jego szerokością i głębokością; x profilu biegnie w poprzek pręta (w stronę szyby w ościeżnicy i skrzydle), y na zewnątrz budynku. **Zwykły** usuwa obrys i zachowuje rozmiar.

![Słupki i rygle ściany osłonowej z przekroju aluminiowego](shots/profile-real-mullions.webp)

![Ościeżnica i skrzydło okna z tego samego przekroju](shots/profile-real-frame.webp)

Biblioteka może się zmienić po użyciu. Profil projektu różny od tego o tej samej nazwie w jego bibliotece jest oznaczony **nowszy w bibliotece**; **Aktualizuj z biblioteki** go zastępuje, a wszystkie typy z niego zrobione podążają za nim, jako jeden krok cofania. Nic się nie zmienia bez Twojej prośby. Starsze projekty zachowują swoje typy.
