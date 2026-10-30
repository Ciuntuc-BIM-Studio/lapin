---
title: Materialer, teksturer og skraveringer
order: 90
summary: Indstil hvert materiales farve, skraveringer og fuger, læg dit eget billede på det, og hent dine egne .pat-skraveringsmønstre ind.
---

**Administrer → Materialer** viser projektets materialer. Vælg et for at ændre det.

![Dialogen Materialer](shots/materials-dialog.webp)

## Hvad et materiale indstiller

- **Farve (3D)** og de flader, der tegnes på det i 3D. Lapin gætter dem ud fra navnet, for eksempel en murstens skifter eller en flises gitter. **Ender** og **Top og bund** kan se anderledes ud end fladerne.
- **Skravering** for hvert sted, det tegnes: skåret i et snit, skåret i en plan og set i en facade. Hver har sin skala, vinkel, justering, udfyldning og pen.
- **Kategori**, for eksempel overflade, isolering eller konstruktion. Visningsstilene bruger den til at udelade overfladerne i en konstruktionsvisning.
- **Membran** tegner et tyndt lag som en stiplet linje i stedet for et bånd.
- **Tegn fuger.** Som standard løber et materiale videre fra ét element ind i det næste uden en linje, som to vægge på linje eller puds over to etager. Sæt flueben for at tegne fugen.

## Dit eget billede på et materiale

Et materiale kan bære dit eget billede: et foto af den rigtige mursten, det rigtige træ eller den rigtige sten.

1. Vælg materialet, og klik på **Eget billede → Importér billede…**. Vælg en PNG- eller JPEG-fil.
2. Indstil **W** og **H**: hvor bred og hvor høj, i meter, én kopi af billedet er på modellen. Billedet gentages hen over fladerne.

![Et materiales eget billede, lagt på i 1.2 × 1.2 m](shots/own-image.webp)

Billedet vises i 3D-visningen og gemmes inde i projektfilen. Det kommer også med i 3D-eksporterne: **GLB**, **glTF** og **USDZ** bærer det. **DAE** og **USDA** er enkeltfiler uden plads til billeder, så de beholder materialets farve.

![Det samme hus med materialets eget billede i 3D](shots/own-image-3d.webp)

Billeder større end 2048 pixels formindskes til den størrelse, når du importerer dem. Skraldespandsknappen fjerner billedet fra materialet.

## Dine egne skraveringsmønstre (.pat)

**Importér .pat** læser en AutoCAD-fil med skraveringsmønstre. Hvert mønster i den føjes til projektet, klar til at blive valgt som et materiales skravering.

Lapin gemmer også filen under **Mine mønstre** på din computer. I ethvert andet projekt henter **Føj til dette projekt** de mønstre ind igen. Et projekt beholder de mønstre, det bruger, så det åbner ens på en anden computer.

![Mine mønstre: de .pat-filer, du har importeret, gemt til alle projekter](shots/my-patterns.webp)
