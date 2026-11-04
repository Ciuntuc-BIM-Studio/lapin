---
title: Tidsplanlægning
order: 97
summary: Arbejdsplanen i et Gantt-diagram: opgaver i en WBS, milepæle, kæder med forskydninger, den kritiske vej beregnet på en kalender med Rumæniens lovbestemte helligdage; til og fra MS Project, IFC og Excel.
---

## Fanen Tidsplanlægning

![Fanen Tidsplanlægning](shots/ribbon-planning.webp)

- **Gantt**: åbner tidsplanen under visningen. Skærmen deles i to rækker, så du ser modellen øverst og tidsplanen nederst.
- **Kalender…**: arbejdsdage og arbejdstimer, helligdage og vinterpausen (nedenfor).
- **Opgave** og **Milepæl**: en ny opgave eller milepæl før den opgave, der er valgt i Gantt (eller til sidst).
- **Indryk**, **Ryk ud**, **Kæd opgaver** og **Fjern kæder**: WBS og kæderne for de valgte opgaver.
- **Føj til opgave…** (TA) og **Elementer uden opgave**: modellens elementer i opgaverne (nedenfor).
- **Importér MS Project…**, **Eksportér MS Project…**, **Eksportér XLSX** og **Eksportér IFC…**: tidsplanen til og fra andre programmer (nedenfor).

Et projekt har én tidsplan. Den oprettes første gang, du tilføjer en opgave, og starter den næste mandag. Ret starten i Gantts værktøjslinje.

## Gantt

![Gantt under 3D-visningen, en opgave valgt](shots/planning-gantt.webp)

Til venstre er opgavetabellen: rækkenummeret, **WBS**-koden, navnet, **Varighed** i arbejdsdage, **Start** og **Slut**, **Forgængere** og den samlede **Buffer**. Til højre er hver opgave en bjælke på tidsskalaen:

- en **opgave** er en blå bjælke, rød når den ligger på den kritiske vej;
- en **sammenfatning** (en opgave med opgaver under sig) er en mørk klamme fra første opgaves start til sidste opgaves slut;
- en **milepæl** er en rombe.

Dage uden arbejde er skraverede, og i dag er en stiplet linje. **Dage**, **Uger**, **Måneder** og **Kvartaler** ændrer skalaen, det samme gør <kbd>Ctrl</kbd> + musehjulet. Knappen med de fire hjørner viser hele tidsplanen, og **I dag** ruller til i dag. **Kritisk vej** slår det røde til og fra. Pilen ved siden af en sammenfatnings navn folder opgaverne under den sammen og ud.

Gantt er en visning som de andre: den har en fane, og på skrivebordet kan den åbnes i et eget vindue til en ekstra skærm.

### Redigering i tabellen

Dobbeltklik på en celle for at skrive i den. <kbd>Enter</kbd> beholder værdien, og <kbd>Escape</kbd> opgiver den.

- **Varighed**: arbejdsdage; 0 gør opgaven til en milepæl.
- **Start**: opgaven starter tidligst den dag (en begrænsning, se nedenfor).
- **Slut**: varigheden bliver arbejdsdagene fra starten til den dag.
- **Forgængere**: som i MS Project rækkenumre adskilt af kommaer, hver med en type og en forskydning: `3`, `5SS`, `7FF+2`, `2FS-1d`.

Tastaturet virker som i en tabel: <kbd>↑</kbd> og <kbd>↓</kbd> flytter markeringen, <kbd>Insert</kbd> tilføjer en opgave før den valgte (<kbd>Shift</kbd>+<kbd>Insert</kbd> en milepæl), <kbd>Delete</kbd> sletter de valgte opgaver med det, der ligger under dem, <kbd>Tab</kbd> indrykker og <kbd>Shift</kbd>+<kbd>Tab</kbd> rykker ud, <kbd>F2</kbd> omdøber.

### Redigering på diagrammet

- **Træk i en bjælke** for at flytte opgaven. Den starter så tidligst den dag, du slipper den på. Datoen vises, mens du trækker.
- **Træk i en bjælkes ende** for at ændre dens varighed. Arbejdsdagene vises, mens du trækker.
- **Kæd to opgaver**: hold musen over en bjælke, og træk fra den lille cirkel i den ene ende hen på en anden bjælke. De ender, du forbinder, giver typen: fra slut til den andens start er slut-til-start (FS), start til start SS, slut til slut FF, start til slut SF.
- **Klik på en pil** for at ændre kædens type og forskydning (i arbejdsdage; negativ er en overlapning) eller for at fjerne den.

Hver ændring er ét trin at fortryde.

## En opgave i Egenskaber

![En opgave i Egenskaber](shots/planning-task.webp)

Vælg en opgave, og Egenskaber viser den: dens navn, dens IFC-art, dens varighed, om den er en milepæl, dens datoer og:

- **Begrænsning**: **Så tidligt som muligt** (standard), **Start tidligst** en dato eller **Skal starte** en dato. En opgave, der skal starte, før dens forgængere er færdige, tager buffer fra dem. Deres buffer bliver så negativ, og de bliver kritiske.
- **Våde arbejder**: beton, afretning, puds. En våd opgave arbejder ikke i kalenderens vinterpause, når pausen er slået til.
- **Samlet buffer** og **Fri buffer**: hvor mange arbejdsdage opgaven kan skride uden at flytte slutningen, eller uden at flytte det, der følger efter den. **Seneste start** og **Seneste slut** er de seneste datoer, der holder slutningen.
- **Forgængere**: hver indgående kæde, med type og forskydning, der kan ændres, eller som kan fjernes. **Efterfølgere** viser det, der følger efter opgaven.

## Den kritiske vej

Lapin beregner tidsplanen efter hver ændring. En fremadgående gennemgang finder de tidligste datoer og en baglæns de seneste. Opgaverne uden buffer udgør den kritiske vej, og en forsinkelse af en af dem flytter slutningen. En kæde til en sammenfatning holder alle opgaver under den. En kæde fra en sammenfatning starter ved første opgaves start og slutter ved sidste opgaves slut. Lapin afviser en kæde, der ville give en løkke.

## Modellens elementer i opgaverne

![Opgaven Murværk valgt: dens vægge vist markeret i 3D](shots/planning-links.webp)

En opgave tager elementer fra modellen, så tidsplanen ved, hvad der bygges hvornår. Vælg en opgave, og dens elementer vises markeret i 3D og i planerne; dobbeltklik på dens bjælke markerer dem og zoomer ind på dem. Markér elementer i en visning, og rækkerne for deres opgaver markeres i Gantt. Kolonnen **Elementer** tæller hver opgaves elementer (for en sammenfatning med opgaverne under den).

### En regel

![En opgaves elementer i Egenskaber](shots/planning-task-elements.webp)

Under **Elementer** i Egenskaber sætter du flueben ved **Find dem med en regel** og vælger, hvad opgaven tager, som i en listes filter: **Arter**, **Etager**, **Typer**, **Materialer**, **Grupper** og **Faser**, hver værdi med hvor mange elementer der har den. Hver betingelse, du sætter, skal gælde; en tom lader alt gå igennem. Det er reglen, der gemmes, ikke elementerne: en væg tegnet i morgen i stueetagen kommer selv med i opgaven "Murværk stueetage". **Regel ud fra markeringen** laver en regel af de markerede elementers arter og etager.

### En del af elementerne

**Del** siger, hvad opgaven tager af hvert element: **hele elementer**, deres **kerne** (det bærende lag), deres **overflader** (alle lag undtagen kernen), lagene af ét materiale eller af en materialekategori (Overflade, Isolering…). Så en lagdelt væg mures i en opgave Murværk (kernen) og pudses i en opgave Puds (overfladerne). Et element uden lag er sin egen kerne og har ingen overflader.

### I hånden

![Markeringens kort over 3D, en vægs to opgaver markeret i Gantt](shots/planning-element-tasks.webp)

Mens Gantt er åben, siger et kort over modelvisningen, hvor mange elementer der er markeret, og hvilke opgaver de er i. Træk det i håndtaget hen på en opgaves række: elementerne kommer med i opgaven. Hold <kbd>Skift</kbd>, når du slipper, for at tage dem ud af den. **Føj til opgave…** (<kbd>T</kbd> <kbd>A</kbd>, også på fanen Tidsplanlægning) vælger opgaven fra en liste.

![Føj til opgave](shots/planning-assign.webp)

Det, du lægger i eller tager ud i hånden, er opgavens **undtagelser**: reglen forbliver som den er. Egenskaber tæller dem, og × ved hver glemmer dem. Et elements egne Egenskaber viser dets opgaver under **Byggetidsplan**, hver med et × for at tage det ud. Hver ændring er ét trin at fortryde.

### Elementer uden opgave

Så snart en opgave tager elementer, siger Gantts værktøjslinje, hvor mange elementer der ikke har nogen opgave eller har lag, som ingen opgave tager; et klik markerer dem, klar til at trække hen på en opgave. **Tidsplanlægning → Elementer uden opgave** gør det samme. **Administrer → Kontrol** viser dem også, under *Elements with a task*: én advarsel pr. element, et klik markerer det. Eksisterende elementer, der bliver stående, behøver ingen opgave.

### I IFC og MS Project

IFC-eksporten skriver, hvad hver opgave bygger, som IfcRelAssignsToProduct (elementet med de opgaver, der bygger det), og hvad en nedrivnings-, fjernelses- eller demonteringsopgave virker på, som IfcRelAssignsToProcess. Regel, del og undtagelser rejser i `Lapin_Task`, så Lapin læser reglen tilbage; en fil fra et andet program giver opgaver med de elementer, den tildeler dem. MS Project XML bærer dem i feltet Text1, kaldet *Lapin elements*. Projektmappen har kolonnerne **Elements** og **Rule**.

## Kalenderen

![Arbejdskalenderen](shots/planning-calendar.webp)

**Tidsplanlægning → Kalender…** fastsætter:

- ugens **arbejdsdage** (mandag til fredag som standard) og dagens **timer** (8 fra 08:00);
- **helligdagene**: Rumæniens lovbestemte helligdage, beregnet for hvert år (også den ortodokse langfredag, påske og pinse), eller ingen. Listen under dem viser et års helligdage, så du kan kontrollere dem;
- **vinterpausen** for våde arbejder, slået fra som standard: fra 15. december til 1. marts, medmindre du angiver andre dage;
- **undtagelser**: fridage for projektet (en lukket byggeplads) eller arbejdsdage, der ellers ikke ville være det (en lørdag).

Varigheder og forskydninger tæller arbejdsdage i denne kalender.

## Udveksling af tidsplanen

- **Eksportér MS Project…** skriver en MS Project XML-fil. MS Project, ProjectLibre og GanttProject åbner den, og Primavera P6 importerer den. Den indeholder opgaverne med deres WBS, varigheder, datoer, begrænsninger og buffere, kæderne med deres typer og forskydninger og kalenderen med hver helligdag som undtagelse. **Importér MS Project…** læser sådan en fil i stedet for projektets tidsplan. En tidsplan skrevet af Lapin og læst igen beholder sine opgavers id og datoer.
- **Eksportér IFC…** (også under **Filer**) skriver tidsplanen i IFC-filen sammen med modellen: en IfcWorkSchedule, dens IfcTask med deres tider, IfcRelSequence-kæderne og IfcWorkCalendar. Importeres en IFC-fil med en tidsplan, kommer den også med.
- **Eksportér XLSX** skriver en projektmappe: Gantts tabel og kalenderens fridage.

Lapins egne felter, som andre programmer ikke har plads til (en opgaves begrænsning, om den er våd, vinterpausen), rejser i IFC som egenskabssættene `Lapin_Task` og `Lapin_Calendar`. MS Project har ikke våde opgaver, så vinterpausen bliver i Lapin.

## Hvad der kommer

En tidsplan genereret ud fra modellen, 4D-simuleringen af bygningen, der vokser i 3D, fremdrift og basisplaner samt overslaget kommer i de næste trin.

