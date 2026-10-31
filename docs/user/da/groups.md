---
title: Grupper og oprydning
order: 112
summary: Hold elementer sammen i en gruppe, åbn en gruppe for at redigere den, vælg med et område eller en lasso i 3D, og skjul det, der er i vejen.
---

## Grupper

En gruppe holder elementer sammen. Vælg dem og klik på **Redigér → Opret gruppe** (<kbd>GP</kbd>). Et klik på et hvilket som helst medlem vælger så hele gruppen, i planer og i 3D. Gruppen flyttes, kopieres, drejes, spejles og slettes som én, og en kopi er en gruppe for sig. I IFC er en gruppe en IfcGroup, så andre programmer ser den også.

En valgt gruppes egenskaber viser, hvor mange elementer den har, med tre knapper: **Redigér gruppe**, **Opløs gruppe** og **Vælg medlemmer**.

![En valgt gruppe: dens medlemmer fremhævet, dens egenskaber](shots/group-selected.webp)

## At redigere en gruppe

Dobbeltklik på et medlem, eller klik på **Redigér gruppe** (<kbd>EG</kbd>). Resten af modellen tegnes nedtonet og kan ikke vælges, og et klik vælger ét medlem ad gangen. Slet, flyt eller ændr medlemmer som sædvanligt. **Fjern fra gruppe** tager de valgte medlemmer ud. **Færdig**, eller <kbd>Esc</kbd>, lukker gruppen. En gruppe forsvinder, når dens sidste medlem slettes.

![En gruppe åben for redigering: havemuren uden for den nedtonet](shots/group-edit.webp)

## Importerede filer

Hver IFC-, Fragments- eller mesh-fil, du importerer, kommer ind som én gruppe opkaldt efter filen. Fjern fluebenet ved **Som én gruppe** i IFC-importen for at hente elementerne løse. En linket fil grupperes ikke: den er allerede ét link, skrivebeskyttet (se [Import og eksport](import-export.md)). Elementer læst fra en IFC-fil som referencer bliver, hvor filen satte dem: en gruppe af dem kan ryddes op og slettes, ikke flyttes.

## At vælge et område i 3D

**Vis → Vælg område** (<kbd>SR</kbd>) gør et træk med venstre knap i 3D-visningen til et valg, som i planer. Trukket mod højre tager et vindue det, der ligger helt inden i det. Trukket mod venstre tager en krydsning det, den rører. Tryk på <kbd>Mellemrum</kbd> under trækket for at tegne en lasso, og <kbd>Skift</kbd> for at føje til valget. Mens det er slået til, drejer højre knap visningen, og den midterste flytter den. **×** på dets mærke, eller <kbd>SR</kbd> igen, slår det fra.

![En krydsning trukket hen over huset i 3D](shots/select-region-3d.webp)

## At skjule midlertidigt

**Vis → Skjul** (<kbd>HH</kbd>) skjuler valget i 3D-visningen. **Isolér** (<kbd>HI</kbd>) skjuler alt andet. **Vis alt** (<kbd>HR</kbd>), eller **×** på mærket, henter alt tilbage. Intet gemmes eller slettes: det er en måde at nå det, der ligger inde i en importeret model, mens du rydder op i den.
