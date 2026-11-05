---
title: Meble
order: 47
summary: Sofy, narożniki, fotele, krzesła, łóżka, szafy, komody, regały, kredensy, stoły, biurka i kuchnie jednorzędowe z typów parametrycznych, dosuwane do ścian jednym kliknięciem.
---

## Wstawianie mebli

Karta **Meble** ma narzędzie dla każdego rodzaju mebla: **Sofa** (<kbd>SO</kbd>), **Narożnik**, **Fotel** (<kbd>AC</kbd>), **Krzesło**, **Łóżko** (<kbd>BD</kbd>), **Szafka nocna**, **Szafa** (<kbd>WD</kbd>), **Komoda**, **Regał na książki**, **Kredens**, **Stół** (<kbd>TB</kbd>), **Stolik kawowy** i **Biurko**. Wybierz jedno i kliknij w rzucie.

Kliknij blisko ściany, a mebel do niej dosunie się tyłem. Jego tył leży na licu ściany (na tynku, nie na rdzeniu), przód zwraca się do pomieszczenia, a mebel przesuwa się wzdłuż ściany, aż przestanie wchodzić w ścianę w narożniku. Kliknij dalej od ścian, a mebel stanie tam, gdzie kliknięto.

Gdy narzędzie jest włączone, pasek wiersza poleceń pokazuje:

- **typ**, który wstawia narzędzie, wybrany spośród typów tego rodzaju w projekcie (jeśli jeszcze go nie ma, pierwszy powstaje przy wstawianiu mebla);
- **obrót**, 0°, 90°, 180° lub 270°, dla mebla stojącego wolno;
- **Przy ścianach**, które możesz wyłączyć, aby wstawić mebel dokładnie tam, gdzie klikasz, nawet obok ściany.

Panel właściwości pokazuje ten sam typ jeszcze przed kliknięciem, z jego wymiarami i **Edytuj typy…**.

![Umeblowany pokój dzienny, kuchnia i sypialnia w rzucie](shots/furniture-plan.webp)

Rzuty rysują meble ich symbolem, zgodnie z konwencją rysunkową, a nie w przekroju:

- sofa z oparciem, podłokietnikami i poduszkami;
- łóżko z wezgłowiem, poduszkami i odwiniętą kołdrą;
- szafa z drążkiem na wieszaki i drzwiami;
- kuchnia ze zlewem i płytą grzejną, a szafki górne linią przerywaną.

Widoki i przekroje rysują bryły z kreskowaniem ich materiałów. W 3D meble są bryłami low-poly, które tworzy Lapin.

![Umeblowany dom w 3D](shots/furniture-3d.webp)

## Krzesła wokół stołu i kuchnia

**Krzesła wokół stołu:** kliknij stół lub biurko. Krzesła stają wzdłuż dłuższych boków, mniej więcej jedno co 600 mm, i po jednym na każdym końcu, jeśli jest dość szeroki; stół okrągły dostaje je na okręgu. Każde krzesło jest zwrócone do stołu. Biurko dostaje krzesła tylko od przodu. Krzesła mają typ wybrany dla narzędzia **Krzesło**.

**Kuchnia** (<kbd>KT</kbd>) rysuje się dwoma punktami wzdłuż ściany:

- długość linii to długość kuchni, a jej tył leży na licu ściany;
- szafki dolne dzielą się równo, po około 600 mm;
- ma blat roboczy, zlew i płytę grzejną (ich miejsce wzdłuż kuchni ustala się w typie), szafki górne nad nimi i słupek na lodówkę na jednym końcu.

**Długość** kuchni można dalej zmieniać we właściwościach; szafki dzielą się na nowo.

## Właściwości mebla

Zaznacz mebel, aby zobaczyć:

- **Typ**, jego rodzaj i wymiary;
- **Obrót**;
- **Nad podłogą**, dla mebla podniesionego na podeście;
- **Długość**, dla kuchni.

**Odwróć** daje meblowi drugą stronę w miejscu, gdzie stoi: szezlong po drugiej stronie, lodówkę na drugim końcu, narożnik narożnika po drugiej stronie. Jego przód zostaje tam, gdzie był. **Modyfikuj → Lustro** robi to samo.

Przeciągnij uchwyt na środku mebla, aby go przesunąć; przeciągnij uchwyt przed nim, aby obrócić jego przód w stronę kursora.

![Właściwości sofy](shots/furniture-properties.webp)

## Typy mebli

**Meble → Typy mebli** wymienia typy projektu według grup (siedziska, łóżka, przechowywanie, stoły, kuchnia), każdy z liczbą mebli, które go używają. Wybierz rodzaj i **Nowy typ** albo **Duplikuj** typ, aby zmienić kopię. Typ zawiera:

- swoją **nazwę**, **szerokość**, **głębokość** i **wysokość** (dla sofy oparcia, dla łóżka wezgłowia, dla kuchni blatu roboczego);
- własne parametry swojego rodzaju:
  - sofy i fotele: miejsca, podłokietniki, szerokość podłokietnika, wysokość siedziska, szezlong po lewej lub prawej i jego długość, nóżki;
  - narożnik: strona, po której jest narożnik, i długość tego boku;
  - łóżko: wierzch materaca, wezgłowie, zagłówek w nogach, poduszki, odwinięta kołdra;
  - szafa: drzwi, uchylne lub przesuwne, szuflady na dole, cokół;
  - komoda: rzędy i kolumny szuflad;
  - regał na książki: półki i drzwiczki na dole do pewnej wysokości;
  - kredens: drzwiczki i szuflady;
  - stół: prostokątny, okrągły lub owalny, na czterech nogach lub jednej nodze centralnej;
  - biurko: kontener z szufladami po lewej lub prawej;
  - kuchnia: szerokość szafek dolnych, cokół, blat roboczy, miejsce zlewu i płyty grzejnej, szafki górne, słupek;
- swoje **materiały** według roli: korpus, tapicerka, blat, fronty, pościel, metal, szkło. Wpisz dowolną nazwę materiału; materiał, którego projekt nie ma, zostaje utworzony, a **Zarządzaj → Materiały** ustala jego kolor i kreskowania.

Podgląd obok pól pokazuje symbol typu w rzucie i jego widok z przodu na bieżąco, gdy go zmieniasz. Każdy mebel danego typu podąża za każdą zmianą.

![Okno Typy mebli z sofą i jej szezlongiem](shots/furniture-types.webp)

## Zestawienia i IFC

**Zestawienie mebli** wymienia każdy typ z jego rodzajem, wymiarami, liczbą mebli i pomieszczeniami, w których stoją, wraz z sumą. **Meble według pomieszczeń** wymienia każde pomieszczenie z tym, co w nim stoi. Oba są w przeglądarce projektu, w **Zestawieniach**, i drukują się oraz eksportują jak każde zestawienie. Typ mebla z pozycją kosztorysową jest wyceniany za sztukę w kosztorysie.

W IFC mebel to **IfcFurniture** z typem predefiniowanym dla swojego rodzaju (SOFA, CHAIR, BED, TABLE, DESK, SHELF albo USERDEFINED z Wardrobe, Kitchen…). Jego **IfcFurnitureType** jest przypisany przez IfcRelDefinesByType, a bryły niosą swoje materiały. Lapin odczytuje własne meble z powrotem jako natywne, edytowalne meble. Meble z innych programów wchodzą jako odniesienia.
