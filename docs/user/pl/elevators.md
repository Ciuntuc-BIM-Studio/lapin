---
title: Windy
order: 43
summary: Windy z konfiguratora dwunastu rodzajów wymiarowanych wg EN 81-20 i EN 81-70, z szybem, podszybiem, nadszybiem, maszynownią, drzwiami kabinowymi i przystankowymi, otworami w stropach, przez które przechodzą, rzutami na każdej kondygnacji, zestawieniem i IFC.
---

## Wstawianie windy

**Architektura → Winda** (<kbd>LF</kbd>) wstawia windę jednym kliknięciem w rzucie. Kliknięcie to środek szybu, a drzwi patrzą w dół rzutu (obróć ją przyciskami 0°, 90°, 180° lub 270° na pasku albo później jej uchwytem). Winda biegnie od kondygnacji rzutu do najwyższej kondygnacji projektu i zatrzymuje się na każdej kondygnacji pomiędzy. Jej konfigurator otwiera się od razu.

Gdy narzędzie jest włączone, pasek wiersza poleceń i panel właściwości pokazują **styl**, który wstawia: jeden z dwunastu rodzajów katalogu albo jeden ze stylów projektu.

## Konfigurator

![Konfigurator windy ze stylem dla wózków inwalidzkich](shots/elevator-configurator.webp)

Style są po lewej. Styl wypełnia wszystkie pola, a potem możesz ręcznie zmienić dowolne z nich. Katalog ma te rodzaje:

| Styl | Osoby | Udźwig | Kabina (mm) | Napęd |
|---|---|---|---|---|
| Mała mieszkaniowa | 4 | 320 kg | 900 × 1000 | bez maszynowni |
| Osobowa standardowa | 6 | 450 kg | 1000 × 1250 | bez maszynowni |
| Osobowa średnia | 8 | 630 kg | 1100 × 1400 | cierna, maszynownia na górze |
| Osobowa duża | 10 | 800 kg | 1350 × 1400 | cierna |
| Osobowa, duży ruch | 13 | 1000 kg | 1600 × 1400 | cierna, 1,6 m/s |
| Dla wózków inwalidzkich | 8 | 630 kg | 1100 × 1400, drzwi 900 | EN 81-70 typ 2 |
| Szpitalna dla łóżek | 17 | 1275 kg | 1200 × 2300 | cierna, kabina przelotowa |
| Towarowa | 26 | 2000 kg | 1500 × 2700 | cierna, 0,5 m/s |
| Hydrauliczna (niskie budynki) | 8 | 630 kg | 1100 × 1400 | siłownik, pomieszczenie agregatu obok szybu |
| Bez maszynowni (MRL) | 13 | 1000 kg | 1100 × 2100 | napęd w nadszybiu |
| Panoramiczna szklana | 8 | 630 kg | 1100 × 1400 | szklana kabina w szklanym szybie |
| Winda domowa | 3 | 250 kg | 800 × 950 | 0,15 m/s, płytkie podszybie |

Po prawej trzy karty:

- **Kabina i udźwig**: napęd (cierna, bez maszynowni, hydrauliczna, domowa), udźwig i liczba osób, prędkość, typ dostępności (EN 81-70 typ 1, 2 lub 3), szerokość, głębokość i wysokość kabiny, szklane ściany kabiny, dźwig dla straży pożarnej, winda ewakuacyjna. **Udźwig i osoby z kabiny** ustawia najmniejszy udźwig, jaki EN 81-20 daje powierzchni kabiny, i liczbę osób, które przewozi.
- **Drzwi i przystanki**: jedno wejście lub dwa (kabina przelotowa), szerokość i wysokość drzwi, otwieranie centralne lub boczne i skrzydła (dwa lub trzy skrzydła przesuwają się jedno za drugim), najwyższy przystanek i kondygnacje, na których się zatrzymuje. Kondygnacja mijana bez zatrzymania ma zamkniętą ścianę szybu i nie ma drzwi przystankowych.
- **Szyb**: strona przeciwwagi (lub siłownika windy hydraulicznej), podszybie pod najniższym przystankiem i nadszybie nad najwyższym (oba wynikają z napędu i prędkości, dopóki nie wpiszesz własnych), maszynownia (na górze dla ciernej, obok szybu na dole dla hydraulicznej, w innych brak), ściany szybu (typ ściany projektu albo beton o podanej grubości), szklany szyb, obrót.

![Przekrój przez kabinę, od podszybia do maszynowni](shots/elevator-section.webp)

Podgląd pokazuje rzut na najniższym przystanku, a na pozostałych kartach przekrój przez kabinę z każdym przystankiem (obsługiwane na czerwono, mijane przerywaną linią). Pod nim to, co mówi EN 81: kabina większa, niż pozwala jej udźwig, więcej osób, niż uniesie udźwig lub powierzchnia, za mała kabina lub za wąskie drzwi dla osób niepełnosprawnych, winda hydrauliczna o wysokości podnoszenia ponad ok. 18 m. Niżej wymiary szybu w świetle i zewnętrzne, powierzchnia kabiny wobec tej, na którą pozwala udźwig, przystanki, wysokość podnoszenia i stropy, które otwiera szyb.

**Zastosuj** zmienia windę w jednym kroku cofania. **Zapisz jako styl…** zachowuje ją jako styl projektu; **Aktualizuj styl** zapisuje ją ponownie i wszystkie windy tego stylu podążają za nim. Styl projektu można przemianować lub usunąć (jego windy zostają, jakie są).

## Na rysunkach

![Winda w rzucie: przecięty szyb, kabina z krzyżem, przeciwwaga i drzwi przystankowe](shots/elevator-plan.webp)

Szyb otwiera każdy strop, przez który przechodzi, własnym otworem w każdym stropie. Gdy przesuwasz, obracasz lub kopiujesz windę albo zmieniasz jej kondygnacje, otwory podążają za nią. Strop narysowany po windzie otwiera **Architektura → Otwory szybu**.

Każdy rzut kondygnacji, do której dociera winda, ją rysuje: przecięte ściany szybu, kabinę z krzyżem po przekątnych, przeciwwagę z jej przekątną (siłownik windy hydraulicznej jako okrąg), a na kondygnacjach, na których się zatrzymuje, skrzydła drzwi przystankowych i kabinowych, przerywane tam, gdzie przesuwają się po otwarciu. Przekroje i elewacje rysują cały szyb, podszybie, strop szybu, maszynownię, kabinę na najniższym przystanku i drzwi przystankowe. W 3D kabina stoi na najniższym przystanku z zamkniętymi drzwiami.

![Winda w 3D, z maszynownią nad domem](shots/elevator-3d.webp)

## Właściwości, zestawienie i IFC

![Właściwości windy](shots/elevator-properties.webp)

Zaznacz windę, aby zmienić jej najniższy i najwyższy przystanek, zaznaczyć kondygnacje, na których się zatrzymuje, wybrać typ ścian szybu, przeczytać ostrzeżenia EN 81 i otworzyć **Konfigurator…**. Jej pola (napęd, udźwig, kabina, drzwi, podszybie, nadszybie…) są poniżej; te, które nie mają zastosowania, są ukryte (brak maszynowni dla windy bez maszynowni, brak strony przeciwwagi dla hydraulicznej). Przeciągnij środkowy uchwyt, aby ją przesunąć, a uchwyt przed drzwiami, aby ją obrócić. **Przesuń**, **Kopiuj**, **Obróć** i **Lustro** działają jak dla każdego elementu; odbita winda przenosi przeciwwagę na drugą stronę.

**Architektura → Zestawienie wind** wymienia każdą windę z rodzajem, napędem, udźwigiem, liczbą osób, kabiną, drzwiami, obsługiwanymi przystankami, kondygnacjami, między którymi kursuje, wysokością podnoszenia, prędkością i szybem.

Każda winda ma typ utworzony z rodzaju, udźwigu i kabiny (Osobowa średnia 630 kg, 1100 × 1400): jednakowe windy go współdzielą, więc pozycja cenowa przypisana typowi (**Koszty → Przypisz pozycje…**) wycenia je wszystkie, a planowanie znajduje je po klasie (IfcTransportElement).

W IFC winda to **IfcTransportElement** o predefiniowanym typie **ELEVATOR**, typu **IfcTransportElementType**, z własnymi zestawami IFC **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) i **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). Lapin wczytuje własny plik z powrotem jako natywną windę, z jej przystankami i otworami w stropach.
