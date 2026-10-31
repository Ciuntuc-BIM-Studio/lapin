---
title: Kom godt i gang
order: 10
summary: Startskærmen, et nyt projekt, vinduets dele, at bevæge sig rundt i en tegning og at vælge dine enheder.
---

## Startskærmen

Når Lapin åbner, viser det startskærmen. **Nyt projekt** starter en tom bygning. **Åbn projekt…** åbner en `.lapin`-fil. De projekter, du har åbnet før, står under **Seneste**.

![Startskærmen](shots/start-screen.webp)

Et nyt projekt har allerede tre niveauer: **Foundations** (−1.50), **Ground floor** (±0.00) og **First floor** (+3.00). Det åbner på stueetagens plan, klar til den første væg.

## Vinduet

![Delene af Lapins vindue](shots/workspace.webp)

1. **Båndets faner.** Tegn, Arkitektur, Overflader, Konstruktion, Grund, Studio, Annotér, Administrer, Mængder og Vis samler hver deres værktøjer. **Filer** åbner, gemmer, importerer og eksporterer. **Fortryd** og **Annuller fortryd** sidder i højre ende af samme bjælke (<kbd>Ctrl</kbd> <kbd>Z</kbd>, <kbd>Ctrl</kbd> <kbd>Y</kbd>).
2. **Båndet.** Det rummer den aktuelle fanes værktøjer. Hold musen over et værktøj for at se dets navn og dets tastaturbogstaver.
3. **Projektbrowseren.** Den viser niveauer, planer, snit, facader, 3D-visninger, ark og skemaer. Dobbeltklik på en for at åbne den i en fane. Et projekt åbner på stueetagens plan med 3D-visningen i en fane ved siden af.
4. **Visningen.** Her tegner du: en plan, et snit, en 3D-visning eller et ark. Du kan åbne flere visninger side om side med de to knapper øverst til højre i visningen.
5. **Hotbaren.** Den lægger de mest brugte værktøjer på taltasterne <kbd>1</kbd>–<kbd>9</kbd>.
6. **Kommandolinjen.** Skriv et værktøjs bogstaver her, og tryk på <kbd>Enter</kbd>. Mens et værktøj kører, fortæller linjen dig, hvad det har brug for som det næste. Se [Tegning og kommandolinjen](drafting.md).
7. **Egenskabspanelet.** Det viser det, der er markeret. Når intet er markeret, viser det visningen forrest.
8. **Ask Me.** Dette er Lapins hjælpeassistent. Se [Hjælp og feedback](help.md).

Vil du have mere plads til tegningen? **Vis → Brugerflade…** vælger et mere kompakt vindue, og sidepanelerne kan foldes væk. Se [Brugerfladen og genveje](interface.md).

## At bevæge sig rundt

- **Zoom.** Drej musehjulet, eller knib på en pegeflade. Lapin zoomer, hvor markøren er.
- **Panorér.** Træk med den midterste eller den højre museknap, eller med to fingre på en pegeflade.
- **Kredsløb i 3D.** Træk med den venstre museknap.
- **Vis alt.** Tryk på knappen **Tilpas**, skriv <kbd>ZE</kbd>, eller dobbeltklik med den midterste knap.

## Enheder

Lapin arbejder i millimeter, medmindre du vælger andet. Du skifter enhederne under **Administrer → Tegningsstandarder → Projekt → Enheder**, eller ved at klikke på **Enheder** i statuslinjen nederst i vinduet. Du kan vælge millimeter, centimeter, meter, fod og tommer eller tommer.

![Tegningsstandarder, fanen Projekt: projektets enheder](shots/standards-project.webp)

Alle længder følger denne indstilling: panelerne, de værdier du skriver, mens du tegner, kommandolinjen og målene. Selve modellen gemmes i meter, så et skift af enheder ændrer aldrig din bygning.

## At gemme

**Filer → Gem** (<kbd>Ctrl</kbd> <kbd>S</kbd>) skriver en `.lapin`-fil. Se [Filer, versioner og gendannelse](files.md).
