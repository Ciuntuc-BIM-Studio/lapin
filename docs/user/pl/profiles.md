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

## Użycie profilu

W **Stal → Typy stalowe**, **Konstrukcja → Typy konstrukcyjne** (słupy i belki) oraz przy profilach hali ramowej wybierz **Z biblioteki profili…** i pisz, aby szukać.

![Wybór przeszukuje wszystkie biblioteki](shots/profile-picker.webp)

Profil jest kopiowany do projektu, a typ go zachowuje. Bryły są lekkie, z ostrymi narożami, na każdym poziomie oprócz jednego: widok na **LOD 400** (rzut, przekrój, elewacja, arkusz lub 3D) rysuje rzeczywisty przekrój z zaokrągleniami, promieniami na końcach półek i pochylonymi półkami. Ilości i eksporty pozostają na bryle z ostrymi narożami; zaokrąglenia i pochylone półki liczą się we właściwościach i eksporcie konstrukcji.

Gdzie indziej profil daje to, co narzędzie może wykorzystać: listwy, bruzdy i ławy fundamentowe biorą jego obrys jako profil 2D (**Z biblioteki profili…** na ich liście), a słupki i rygle ścian osłonowych oraz ościeżnice drzwi i okien jego szerokość i głębokość.

Biblioteka może się zmienić po użyciu. Profil projektu różny od tego o tej samej nazwie w jego bibliotece jest oznaczony **nowszy w bibliotece**; **Aktualizuj z biblioteki** go zastępuje, a wszystkie typy z niego zrobione podążają za nim, jako jeden krok cofania. Nic się nie zmienia bez Twojej prośby. Starsze projekty zachowują swoje typy.
