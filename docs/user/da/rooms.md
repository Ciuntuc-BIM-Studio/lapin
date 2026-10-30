---
title: Rum og rumfarver
order: 45
summary: Placér rum med et klik inden for vægge, navngiv og nummerér dem, og farvelæg eller skravér dem på en plan efter funktion eller afdeling, med en farveforklaring.
---

## At placere et rum

Vælg **Arkitektur → Rum** (<kbd>RM</kbd>), og klik inde i et område lukket af vægge. Lapin finder rummets omrids mellem fladerne på vægge, facadeglasvægge og søjler omkring punktet. Døre åbner ikke rummet: det lukker hen over dem. En søjle, der står i rummet, trækkes fra dets areal.

Rummet følger med sine vægge. Flyt en væg, og rummet vokser eller skrumper med den. Hvis en væg slettes, og intet længere lukker rundt om rummet, beholder det det omrids, det havde, og dets stempel bliver rødt. **Arkitektur → Opdatér rum** gemmer de omrids, rummene finder nu.

Til et område uden vægge hele vejen rundt, som en terrasse eller den ene ende af et åbent rum, så brug **Rum efter omrids** og klik på dets hjørner. Det rum beholder det omrids, du tegner.

![En plan med to rum farvelagt efter funktion og en farveforklaring](shots/rooms-plan.webp)

## Et rums egenskaber

Markér et rum ved at klikke på dets stempel eller et vilkårligt sted inde i det. I egenskabspanelet:

- **Navn** og **Nummer.** Nye rum nummereres efter niveau: 101, 102… på stueetagen.
- **Funktion**, **Afdeling** og **Gulvbelægning.** Skriv hvad som helst, eller vælg en værdi, som andre rum allerede har.
- **Højde.** Tomt betyder niveauets højde.
- **Egen farve** og **Egen skravering** tilsidesætter farveskemaet for netop dette rum.

Mængderne nedenfor viser rummets nettoareal (uden søjlerne), dets omkreds og dets volumen. Træk i grebet på stemplet for at flytte stemplet.

![Et rums egenskaber](shots/room-properties.webp)

## Rumfarver

**Vis → Rumfarver** farvelægger rummene i en plan. Vælg et skema, vælg hvad det farvelægger efter (**Funktion**, **Navn**, **Afdeling**, **Nummer** eller **Gulvbelægning**), og sæt **Denne plan viser** til skemaet. Hver værdi, rummene har, får en række med:

- en **Farve**,
- en **Skravering** fra projektets mønstre, med sin **Skalér** og **Blæk**-farve,
- og en forhåndsvisning af, hvordan det ser ud.

**Udfyld manglende farver** giver hver værdi uden farve en fra en blød palet. **Ny** og **Duplikér** laver flere skemaer, for eksempel ét til en kundepræsentation og ét efter afdeling.

![Dialogen Rumfarver](shots/room-colors.webp)

Farverne tegnes under væggene, møblerne og dørslagene, så planen forbliver læsbar. De udskrives på ark og kommer med i eksport til PDF og DXF. En ny plan viser ingen rumfarver, før du vælger et skema til den, under **Rumfarver** eller i planens egenskaber.

## Farveforklaring

**Annotér → Farveforklaring**: klik, hvor forklaringen skal stå på planen. Den viser de farver, planens rum bruger, med deres navne, og følger enhver ændring af skemaet. Flyt den med **Flyt**.

## Rum i 3D og i IFC

Rum er skjult i 3D-visningen, så de ikke dækker bygningen. **Synlighed / Grafik** i 3D-visningen kan vise dem.

Ved eksport til IFC er hvert rum et IfcSpace med sit nummer, navn, funktion, arealer og volumen.

## Rum fra en IFC-fil

**Filer → Importér IFC…** henter filens rum ind som rum, uanset hvilket program der skrev dem (Revit, ArchiCAD, Allplan, Bonsai…):

- hvert rum placeres på **sit eget niveau**, det som filen lægger det i;
- dets **Omrids** kommer fra dets form (en ekstrudering, en B-rep eller et mesh, eller fodaftrykket tegnet ved siden af); det beholder det omrids og følger ikke vægge;
- dets **Navn** er rummets lange navn og dets **Nummer** dets navn (sådan som Revit og ArchiCAD skriver dem); dets **Højde**, og dets **Fodforskydning** når det starter over sit niveau (et galleri, et hævet gulv), kommer fra dets form;
- dets **Funktion**, **Afdeling** og **Gulvbelægning** kommer fra filen, hvor den angiver dem (objekttypen eller rumkategorien, en Department-egenskab, gulvbelægningen), så et farveskema efter funktion eller afdeling farvelægger dem med det samme;
- alt andet, rummet havde i filen, dets egenskabssæt og mængder (Pset_SpaceCommon, BaseQuantities, programmets egne sæt), bevares og vises, skrivebeskyttet, under **IFC-egenskaber** i dets egenskaber, med et filter når der er mange.

Importens besked siger, hvor mange rum der kom ind. Se efter dem på deres niveauers planer (hvert viser sit stempel), i et skema over **Rum** (**+** under Skemaer i browseren) eller på browserens fane IFC; 3D-visningen skjuler rum.

Et rum, hvis form ikke giver noget omrids, kommer stadig ind, med sit navn og sine egenskaber, som et rum uden omrids: det står i skemaet, men tegnes ingen steder. Markér det (browserens fane IFC), åbn dets niveaus plan, og vælg derefter **Find med et klik** (klik inden for dets vægge) eller **Tegn omrids** i dets egenskaber.
