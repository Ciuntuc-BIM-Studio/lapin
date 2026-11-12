---
title: Konstrukcja
order: 50
summary: Słupy, belki i fundamenty, ich typy oraz sposób, w jaki się łączą.
---

Karta **Konstrukcja** zawiera szkielet i fundamenty.

![Karta Konstrukcja](shots/ribbon-structure.webp)

## Szkielet

- **Słup** (<kbd>CL</kbd>): kliknij miejsce, w którym stoi. Słup biegnie od swojego poziomu do poziomu powyżej.
- **Belka** (<kbd>BM</kbd>): kliknij jej dwa końce. **Belka łukowa** (<kbd>BC</kbd>) biegnie po łuku.
- **Słupy w siatce** wstawia słup na każdym przecięciu osi siatki. Aby narysować osie, użyj **Opisz → Oś siatki** (<kbd>GR</kbd>).

## Fundamenty

- **Ława fundamentowa** (<kbd>SF</kbd>) biegnie pod ścianami. Jej warstwy zewnętrzne są łączone na narożnikach na ucios.
- **Ława fundamentowa łukowa** (<kbd>CF</kbd>) biegnie po łuku.
- **Stopa fundamentowa** (<kbd>PF</kbd>): kliknij pod słupem. Gdy narzędzie jest włączone, przyciąga do środków słupów poziomu rzutu i poziomu nad nim.
- **Stopy fundamentowe pod słupami** stawia stopę pod każdym zaznaczonym słupem, który jej nie ma, w jego środku i obróconą jak on. Bez zaznaczonych słupów bierze słupy poziomu rzutu (w rzucie fundamentów te z poziomu nad nim).

Rysuj fundamenty na poziomie **Fundamenty** albo daj im osobny rzut.

**Ława fundamentowa to sam beton**, chyba że poprosisz o ścianę. Gdy włączona jest **Ława fundamentowa** lub **Ława fundamentowa łukowa**, pasek wiersza poleceń oferuje **Sama ława** (domyślnie: betonowa ława 600 × 400 mm, typ z profilem tworzony za pierwszym razem) lub **Ze ścianą** (cokół typu ławy fundamentowej na niej, z jego warstwami) oraz który typ ze ścianą użyć. Wybór jest zapamiętywany. Już narysowana ława zmienia się wraz ze swoim typem w **Typy konstrukcyjne**.

![Opcja narzędzia ławy fundamentowej w wierszu poleceń: sama ława lub ze ścianą](shots/structure-strip-option.webp)

**W rzutach.** Ława leży pod stropem, więc rzut rysuje ją linią przerywaną w rzucie poziomu, na którym stoi, jak rzut budynku pokazuje to, co jest pod nim. Aby zobaczyć ją przeciętą i widzianą, użyj **rzutu fundamentów**: w przeglądarce projektu **+** obok **Rzuty** tworzy go dla aktywnego poziomu (cięcie 0,50 m pod nim, widok 2,50 m w dół).

**Rysowanie w rzucie fundamentów.** Pokazuje też poziom pod nim, a to, co tam narysujesz narzędziem stropu, ściany, słupa lub belki, trafia na ten poziom (Fundamenty nowego projektu, na −1,50); narzędzia fundamentów zachowują poziom rzutu. Stopa fundamentowa narysowana tam pod słupem podnosi się do podstawy słupa; spód ławy jest spodem jej typu pod poziomem.

**Stopa fundamentowa podnosi się do swojego słupa.** Stopa, której punkt leży w przekroju słupa (z dokładnością do milimetra), ma wierzch u podstawy słupa: poziom słupa plus jego **Odsunięcie podstawy**, stopnie pod nim. Przesuń słup, zmień jego poziom lub odsunięcie podstawy, a stopa podąży za nim. Pod słupami stojącymi jeden na drugim podąża za najniższym; z dwóch równie niskich za tym, którego środek jest bliżej. Stopa bez słupa nad nią stoi na swoim poziomie, ze **Spodem** swojego typu pod nim. **Stopy fundamentowe pod słupami** umieszcza każdą stopę na najwyższym poziomie na lub pod jej spodem (Fundamenty nowego projektu), inaczej na najniższym. Stopa zaimportowana z pliku IFC zostaje tam, gdzie umieszcza ją plik.

We **Właściwościach** stopa pokazuje **Górę** i **Dół** jako rzędne oraz słup, za którym podąża. Wpisz **Górę**, by nadać jej własną: przestaje podążać za słupem. **U podstawy słupa** wiąże ją ponownie.

![Poziom stopy fundamentowej we Właściwościach: u podstawy jej słupa](shots/structure-pad-level.webp)

![Słupy, belki i stopy fundamentowe w 3D](shots/structure-3d.webp)

## Typy i narożniki

**Typy konstrukcyjne** ustala przekroje słupów, belek i fundamentów: przekroje betonowe i profile stalowe. Przekrój słupa to **Prostokąt**, **Okrąg** lub **Wielokąt** foremny, określony przez **Strony** i **Średnicę** po narożach (ośmiokątny słupek, jaki może mieć zaimportowany IFC). Kliknij typ na liście, aby zmienić jego wymiary lub nazwę, a następnie **Zapisz typ**: każdy element tego typu się dostosuje; **Nowy typ** wraca do tworzenia nowego.

Belki i fundamenty łączą się same tam, gdzie się spotykają, narożniki na ucios. Belka narysowana od osi słupa do osi słupa kończy się na licach słupów; jej długość, objętość i strzemiona liczą się w świetle między nimi. **Na ukos**, **Na styk** i **Zamień** zmieniają sposób, w jaki spotykają się dwie belki lub dwa fundamenty. **Dopasuj właściwości** (<kbd>MA</kbd>) kopiuje typ i ustawienia jednego elementu na inne, a **Utwórz podobny** (<kbd>CS</kbd>) zaczyna rysowanie kolejnego elementu tego samego typu.

Każdy inny profil pochodzi z bibliotek: **Z biblioteki profili…** przeszukuje je wszystkie (zob. [Biblioteki profili](profiles.md)).

## Ławy fundamentowe z profilem

Typ ławy fundamentowej to typ standardowy, czyli ściana fundamentowa z warstwami na ławie, albo dowolny profil przeciągnięty wzdłuż osi ławy, jak profile fundamentów w Revit. W **Typy konstrukcyjne → Strip** wybierz **Kształt: Profil** i profil: jeden z katalogu (prostokątna, schodkowa z 2 lub 3 stopniami, ze skośną górą, mimośrodowa ze ścianą przy jednej krawędzi, ze ścianą fundamentową) z jego wymiarami, albo jeden z profili projektu (**Zarządzaj → Profile**, gdzie można też wpisać jego wierzchołki lub odczytać narysowany kontur). Punkt wstawienia profilu leży na osi ławy, na poziomie **Dół** typu.

Ławy z profilem mogą być proste albo łukowe. Dwie spotykające się w narożniku łączą się na ucios, a ława kończąca się na innej wchodzi w nią i jest przycinana jej bryłą, więc każdy stopień spotyka stopień drugiej. Rzuty, przekroje i elewacje przecinają przeciągniętą bryłę. Ilości podają jej długość, pole przekroju i objętość, a jej pionowe boki to lica, które deskują Roboty pomocnicze. W IFC prosta ława to IfcFooting STRIP_FOOTING, którego bryła to wyciągnięty profil (IfcArbitraryClosedProfileDef) przycięty płaszczyznami narożników; łukowa zapisywana jest jako jej ściany. Wybierz **Standardowa**, aby wrócić do ściany fundamentowej na ławie.

![Schodkowe ławy fundamentowe pod domem, na ucios w narożnikach](shots/footing-profile-3d.webp)

![Typ ławy fundamentowej z profilem](shots/footing-profile-type.webp)

## Widok konstrukcyjny

Branża widoku decyduje o tym, co on pokazuje. Widok **Konstrukcja** pokazuje tylko nośne rdzenie, bez wykończeń. Branżę wybierasz we właściwościach widoku. Zobacz [Widoki](views.md).

## Obciążenia

Grupa **Obciążenia** zawiera to, czego potrzebują obliczenia konstrukcji: przypadki obciążenia, obciążenia powierzchniowe, liniowe i punktowe oraz kombinacje. Zobacz [Obciążenia](loads.md).
