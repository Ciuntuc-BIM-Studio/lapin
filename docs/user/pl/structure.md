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
- **Stopa fundamentowa** (<kbd>PF</kbd>) jest umieszczana pod słupem.

Rysuj fundamenty na poziomie **Fundamenty** albo daj im osobny rzut.

![Słupy, belki i stopy fundamentowe w 3D](shots/structure-3d.webp)

## Typy i narożniki

**Typy konstrukcyjne** ustala przekroje słupów, belek i fundamentów: przekroje betonowe i profile stalowe. Kliknij typ na liście, aby zmienić jego wymiary lub nazwę, a następnie **Zapisz typ**: każdy element tego typu się dostosuje; **Nowy typ** wraca do tworzenia nowego.

Belki i fundamenty łączą się same tam, gdzie się spotykają, narożniki na ucios. Belka narysowana od osi słupa do osi słupa kończy się na licach słupów; jej długość, objętość i strzemiona liczą się w świetle między nimi. **Na ukos**, **Na styk** i **Zamień** zmieniają sposób, w jaki spotykają się dwie belki lub dwa fundamenty. **Dopasuj typ** (<kbd>MA</kbd>) kopiuje typ jednego elementu na inne, a **Utwórz podobny** (<kbd>CS</kbd>) zaczyna rysowanie kolejnego elementu tego samego typu.

## Widok konstrukcyjny

Branża widoku decyduje o tym, co on pokazuje. Widok **Konstrukcja** pokazuje tylko nośne rdzenie, bez wykończeń. Branżę wybierasz we właściwościach widoku. Zobacz [Widoki](views.md).
