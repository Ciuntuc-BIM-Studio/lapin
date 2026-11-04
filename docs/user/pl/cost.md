---
title: Kosztorys
order: 98
summary: Kosztorys z modelu: twoje cenniki zaimportowane z CSV lub Excela, pozycje na typach i reguły na materiałach, ilości z przedmiaru razy ceny jednostkowe z podziałem na materiał, robociznę, sprzęt i transport, koszty pośrednie, zysk i VAT, w RON lub EUR; rumuńskie formularze F3, F2 i od C6 do C9 na arkuszach, w Excelu i PDF; harmonogramy kosztów IFC.
---

## Karta Koszty

![Karta Koszty](shots/ribbon-cost.webp)

Karta, która nazywała się **Ilości**, nazywa się teraz **Koszty**. Zachowuje ilości i dodaje kosztorys:

- **Przedmiar**, **Zestawienia…** i **Eksportuj XLSX**: ilości z modelu (zobacz [Zestawienia i wykresy](schedules.md)).
- **Cenniki…**: twoje ceny, zaimportowane z pliku CSV lub Excel (poniżej).
- **Przypisz pozycje…**: która pozycja wycenia każdy typ i każdy materiał (poniżej).
- **Kosztorys…**: otwiera kosztorys pod widokiem (za pierwszym razem go tworzy).
- **Formularz na arkuszu**, **Kosztorys XLSX** i **Kosztorys PDF**: kosztorys i jego formularze na arkuszu, w skoroszycie albo w PDF (poniżej).
- **Koszt w czasie**: kosztorys położony na harmonogramie robót, krzywa S i tabela miesięczna (poniżej).

Kosztorys jest liczony z modelu przy każdej jego zmianie. Ściana, którą teraz narysujesz, od razu jest w kosztorysie, ze swoimi ilościami. Zapisywane są tylko ceny, pozycje na typach, reguły i twoje własne wiersze.

## Cenniki

![Import cennika: znaczenie każdej kolumny odgadnięte z jej nagłówka](shots/price-import.webp)

Ceny są twoje: Lapin nie dostarcza żadnego katalogu cen ani norm. **Koszty → Cenniki… → Importuj…** wczytuje plik CSV lub XLSX, na przykład eksport z twojego programu kosztorysowego albo cennik dostawcy. Lapin pokazuje pierwsze wiersze pliku pod ich nagłówkami. Nad każdą kolumną jest to, co według Lapina zawiera, odgadnięte z nagłówka, po angielsku lub po rumuńsku:

- **Kod** i **Opis** (*Cod*, *Simbol*, *Denumire*), **Jednostka** (*U.M.*: m³ lub mc, m² lub mp, m, pcs lub buc, kg, t, 100 m²).
- Cena jednostkowa z podziałem na kolumny **Materiał**, **Robocizna**, **Sprzęt** i **Transport** (*Material*, *Manoperă*, *Utilaj*, *Transport*) albo jedna **Cena jednostkowa (bez podziału)**, która liczy się wtedy jako materiał.
- Opcjonalnie: **Roboczogodziny na jednostkę** (*Normă de timp*) i **Zawód** (*Meserie*), do zestawienia robocizny (C6).
- Opcjonalnie: zasoby pozycji, wiersze, które podają pozycję, do której należą (**Pozycja, do której należy**), ich **Rodzaj zasobu** (M, Man, U, T) i ich **Zużycie na jednostkę**. Pozycja podana tylko przez swoje zasoby jest przez nie wyceniana, a zestawienia zasobów (od C7 do C9) wymieniają je jeden po drugim.

Zmień każdą źle odgadniętą kolumnę, wybierz wiersz z nagłówkami i to, czy ceny są w RON czy w EUR, potem kliknij **Importuj**. Cennik trafia do projektu. Gdy zaznaczone jest **Zachowaj też w bibliotece tego komputera**, zostaje też na tym komputerze: pod **Na tym komputerze** przycisk **Dodaj do projektu** wnosi go do twojego następnego projektu.

**Szablon** zapisuje plik CSV z kolumnami i kilkoma wierszami przybliżonych wartości Lapina, oznaczonych jako *orientacyjne*: wpisz w nim swoje ceny i go zaimportuj. **Użyj wartości orientacyjnych** wstawia te przybliżone wartości do projektu jako cennik, więc możesz wypróbować kosztorys, zanim będziesz mieć swoje. Zastąp je, zanim komukolwiek wyślesz kosztorys.

## Pozycje na typach, reguły na materiałach

![Przypisz pozycje: każdy typ i materiał ze swoją pozycją](shots/map-articles.webp)

Wiersz kosztorysu pochodzi z **pozycji**: kodu z twojego cennika. Elementom można dać pozycję na dwa sposoby:

- **Na typie.** Każdy element typu jest przez nią wyceniany. Pozycja mówi, co mierzy: cały element albo jego część (**rdzeń** typu warstwowego albo jego **wykończenia**) i według jakiej miary (według jednostki ceny albo objętość netto lub brutto, powierzchnia netto lub brutto, długość, liczba, ciężar). Może też dodać mnożnik i odpad (%).
- **Na materiale (reguła).** Każda warstwa tego materiału, w dowolnym typie, jest przez nią wyceniana: jedna reguła dla tynku wapiennego wycenia tynk wszystkich twoich ścian.

Pozycja typu ma pierwszeństwo. Warstwa, którą mierzą już pozycje jej typu, nie jest wyceniana ponownie przez regułę. Pozycja na całym elemencie wycenia go w całości, więc reguły ustępują. Ścianie warstwowej daj na typie pozycję na **rdzeń** (mur według objętości) i reguły dla jej wykończeń (tynk według powierzchni). Ściana jest wtedy wyceniana warstwa po warstwie: mur pod **Ściany**, tynk pod **Wykończenia i izolacje**.

**Koszty → Przypisz pozycje…** wymienia typy projektu, których używają elementy, i materiały ich warstw. Każdy wiersz zaczyna się od pozycji, którą ma, albo od tej, którą wybrałeś dla typu lub materiału o tej samej nazwie we wcześniejszym projekcie, albo od tej, którą podpowiada Lapin: kodu zapisanego w nazwie albo pozycji mającej z nią najwięcej wspólnych słów (*zidărie*, *BCA*, *tencuială*, *C25/30*). Dla typu warstwowego proponuje rdzeń. Wpisz lub wybierz kod, wybierz część i miarę, zaznacz wiersze, które chcesz, i kliknij **Zastosuj**: jeden krok do cofnięcia. Twoje wybory są zapamiętywane na tym komputerze na następne projekty.

Typy szalunków i rusztowań z karty **Roboty pomocnicze** miały już kod pozycji (decyzja 152): to pozycja na typie, mierzona według jednostki ceny.

Fazy się liczą: element, który był przed robotami (utworzony w fazie *istniejącej*), nie jest wyceniany. Rozebrany jest wyceniany tylko pozycją swojego typu, której częścią jest **Jego rozbiórka (tylko gdy rozebrany)**, w rozdziale **Rozbiórka**.

## Kosztorys

![Kosztorys: działy, wiersze, stawki i sumy](shots/estimate.webp)

**Koszty → Kosztorys…** otwiera kosztorys pod widokiem, jak wykres Gantta. Jego wiersze są pogrupowane w **działy** według rodzaju robót: roboty ziemne, fundamenty, konstrukcja, ściany, dach, drzwi i okna, wykończenia i izolacje, instalacje, roboty pomocnicze, inne. Pozycja może wskazać własny dział. **Według kondygnacji i rodzaju robót** stawia kondygnację przed każdym działem.

Każdy wiersz pokazuje kod, opis i jednostkę z cennika, **ilość** z modelu (tę samą co w przedmiarze), cenę jednostkową z podziałem na kolumny **Materiał**, **Robocizna**, **Sprzęt** i **Transport**, cenę jednostkową i **wartość**. Kliknij wiersz, aby zaznaczyć jego elementy w widokach. Zaznacz elementy w widoku, a ich wiersze zostaną wyróżnione w kosztorysie.

Pod wierszami są sumy, ułożone tak jak w rumuńskich kosztorysach:

1. **Koszty bezpośrednie**: suma wierszy.
2. **Składki od robocizny (CAM)**: domyślnie 2,25 % robocizny.
3. **Koszty pośrednie** (*cheltuieli indirecte*): domyślnie 10 % kosztów bezpośrednich ze składkami.
4. **Zysk**: domyślnie 5 % kosztów bezpośrednich i pośrednich.
5. **Razem bez VAT**, **VAT** (domyślnie 21 %) i **Razem z VAT**.

Każdą stawkę zmienisz na pasku narzędzi. Każda wartość jest zaokrąglana do dwóch miejsc po przecinku, tak jak drukuje ją kosztorys.

Kosztorys jest w **RON** albo w **EUR**. **1 EUR =** to kurs projektu: cennik w drugiej walucie jest według niego przeliczany, a pole obok pokazuje każdą wartość także w drugiej walucie.

Pasek ostrzega też o tym, co nie ma ceny: **Typy bez ceny: N** (kliknij, aby otworzyć Przypisz pozycje) i kody, których nie ma żaden cennik. **Własny wiersz** dodaje robotę, której nie ma w modelu (ogrodzenie budowy, rezerwę): wpisz jej kod, opis, jednostkę, ilość i ceny. Wiersz z kodem, a bez cen, bierze je z cennika.

## Rumuńskie formularze

![Wykaz ilości robót (F3) tego samego kosztorysu](shots/estimate-f3.webp)

Lista nad kosztorysem pokazuje sam kosztorys albo jeden z jego formularzy. To zgrupowania tego samego kosztorysu, więc dają te same sumy. Mają nagłówki po rumuńsku, tak jak składa się je w Rumunii:

- **Wykaz ilości robót (F3)**: *Lista cu cantitățile de lucrări pe categorii de lucrări*, dział po dziale, ceny jednostkowe i wartości z podziałem na materiał, robociznę, sprzęt i transport, a pod nimi sumy.
- **Kosztorys obiektu (F2)**: *Deviz pe obiect*, wartość każdego działu bez VAT, jego VAT i jego wartość z VAT.
- **Robocizna (C6)**: godziny i wartość według zawodu (z zawodu i roboczogodzin w cenniku albo z jego zasobów robocizny).
- **Materiały (C7)**, **Sprzęt (C8)** i **Transport (C9)**: zasoby z ich ilościami, cenami jednostkowymi i wartościami. Pozycja bez zasobów liczy się jako jeden własny zasób.

Formularze są zgodne z powszechną praktyką według metodologii Rozporządzenia 863/2008. Sprawdź je z tym, czego wymaga twój inwestor albo program finansowania: napisz do nas, jeśli potrzebujesz innego układu.

**Umieść na arkuszu** umieszcza pokazaną tabelę na nowym arkuszu, gotową do druku razem z innymi. **XLSX** zapisuje kosztorys i każdy formularz w jednym skoroszycie, każdy na osobnym arkuszu. **PDF** zapisuje je jako PDF, A4 poziomo, gotowy do druku.

## Koszt w czasie

![Koszt w czasie: krzywa S nad słupkami miesięcy, tabela miesięczna pod nią](shots/cost-time.webp)

Gdy projekt ma harmonogram robót (karta **Harmonogram**), kosztorys kładzie się na nim bez żadnego własnego powiązania. Wartość każdej pozycji dzieli się między jej elementy, a udział każdego elementu idzie do zadań, które budują część mierzoną przez pozycję: pozycja murów do zadania, które bierze rdzeń ścian, pozycja tynku do tego, które bierze ich wykończenia. Pozycja na całym elemencie, budowanym w dwóch zadaniach, dzieli się po równo. Własna pozycja idzie do zadania wybranego obok jej opisu (domyślnie **W żadnym zadaniu**). Koszt zadania rozkłada się równo na jego dni robocze: nic nie jest wydawane w weekend, w święto ani, dla zadania mokrego, w przerwie zimowej.

**Koszty → Koszt w czasie** otwiera pod widokiem:

- **krzywą S**: planowany koszt narastająco w czasie harmonogramu, koszt **planu bazowego** (przerywany), gdy jest zapisany, i **wykonane roboty** (grubo) do daty stanu: koszt każdego zadania razy jego % wykonania, rozłożony od jego rzeczywistego rozpoczęcia;
- słupki miesięcy, na prawej skali: planowane i wykonane;
- nad nią **budżet na zakończenie** (koszty bezpośrednie kosztorysu), **koszt do dziś** i to, co **niezaplanowane**, jeśli jest: pozycje, których elementów nie buduje żadne zadanie (kliknięcie otwiera kosztorys);
- pod nią **tabelę miesięczną**: koszt każdego rozdziału w miesiącu, niezaplanowane i jego sumę.

Wartości to koszty bezpośrednie, jak pozycje kosztorysu. Gantt pokazuje koszt każdego zadania w kolumnie **Koszt** (zob. [Harmonogram](planning.md)).

![Krzywa S na arkuszu](shots/cost-curve-sheet.webp)

**Umieść na arkuszu** kładzie krzywą na nowym arkuszu, narysowaną wektorowo z legendą. **Tabela miesięczna na arkuszu** umieszcza tabelę miesięczną, która jest też formularzem **Koszt w miesiącach** na liście kosztorysu: rozdziały miesiąc po miesiącu, potem koszty bezpośrednie, sumy bez VAT i z VAT (rozdzielone proporcjonalnie), koszty narastająco i, z postępem, wykonane roboty. Każdy wiersz sumuje się do sumy kosztorysu. **XLSX** zapisuje krzywą dzień po dniu, tabelę miesięczną i koszty zadań w skoroszycie; **PDF** zapisuje krzywą i tabelę miesięczną.

**Zarządzaj → Wersje → Porównaj** mówi, obok elementów i harmonogramu, co zmieniło się w kosztorysie: suma z VAT, koszty bezpośrednie i różnica, każdy rozdział, którego koszt się zmienił, oraz pozycje dodane, usunięte lub zmienione (ilość i wartość).

Miesięczne rozliczenia robót (*situații de lucrări*), wartość wypracowana i wiele planów bazowych należą do edycji premium.

## IFC

**Plik → Eksportuj IFC…** zapisuje kosztorys jako **IfcCostSchedule**: jego działy i wiersze jako **IfcCostItem**, ilość każdego wiersza i jego ceny jednostkowe jako cztery **IfcCostValue** (Material, Labour, Equipment, Transport), elementy mierzone przez każdy wiersz przypisane do niego (**IfcRelAssignsToControl**), walutę jako **IfcMonetaryUnit** projektu. Zaimportowany kosztorys zachowuje wiersze tak, jak ma je plik, z tymi samymi sumami. Zaznacz **Wiersze z modelu**, aby dodać wiersze, które daje twój model.

Z harmonogramem robót każda pozycja jest też przypisana do zadań, w których jest wykonywana (**IfcRelAssignsToControl** do ich **IfcTask**). Zaimportowane pozycje zachowują swoje zadania, więc krzywa S wraca taka sama.
