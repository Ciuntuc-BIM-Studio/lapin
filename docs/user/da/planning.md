---
title: Tidsplanlægning
order: 97
summary: Arbejdsplanen i et Gantt-diagram: opgaver i en WBS, milepæle, kæder med forskydninger, den kritiske vej beregnet på en kalender med Rumæniens lovbestemte helligdage; 4D-simuleringen af bygningen, der vokser dag for dag, i visninger, på ark og som video; fremdriften markeret på byggepladsen, også fra en telefon, mod en basisplan; til og fra MS Project, IFC og Excel.
---

## Fanen Tidsplanlægning

![Fanen Tidsplanlægning](shots/ribbon-planning.webp)

- **Generér tidsplan…**: en hel tidsplan lavet ud fra modellen i ét trin (nedenfor).
- **Gantt**: åbner tidsplanen under visningen. Skærmen deles i to rækker, så du ser modellen øverst og tidsplanen nederst.
- **Kalender…**: arbejdsdage og arbejdstimer, helligdage og vinterpausen (nedenfor).
- **Opgave** og **Milepæl**: en ny opgave eller milepæl før den opgave, der er valgt i Gantt (eller til sidst).
- **Indryk**, **Ryk ud**, **Kæd opgaver** og **Fjern kæder**: WBS og kæderne for de valgte opgaver.
- **Føj til opgave…** (TA) og **Elementer uden opgave**: modellens elementer i opgaverne (nedenfor).
- **Simulér**, **Etapeark…**, **Gantt på ark** og **Eksportér video…**: 4D-simuleringen, modellen som tidsplanen har den på en dag (nedenfor).
- **Markér færdig** (MD), **Markér påbegyndt** (MS), **Markér ikke påbegyndt**, **Gem basisplan**, **Ryd basisplan** og **Importér fremdrift…**: fremdriften og basisplanen (nedenfor).
- **Importér MS Project…**, **Eksportér MS Project…**, **Eksportér XLSX** og **Eksportér IFC…**: tidsplanen til og fra andre programmer (nedenfor).

Et projekt har én tidsplan. Den oprettes første gang, du tilføjer en opgave, og starter den næste mandag. Ret starten i Gantts værktøjslinje.

## Tidsplanen genereret ud fra modellen

![Generér tidsplan: skabelonerne, fanen Start og forhåndsvisningen](shots/planning-generate.webp)

**Tidsplanlægning → Generér tidsplan…** (også i Gantt-diagrammets værktøjslinje) laver en hel tidsplan ud fra modellen: en opgave for hvert trin i arbejdet og, hvor trinnet går etage for etage, én pr. etage, hver opgave allerede med sine elementer. Til venstre er **skabelonerne**: **Betonskelet**, **Bærende murværk**, **Stålhal** og **Træskelet**. Lapin markerer den, modellen beder om, som *foreslået*: stålhallen, hvis modellen har en, træskelettet, hvis væggene er rammevægge, bærende murværk, hvis den ikke har søjler, ellers betonskelettet. Over dem er projektets stilarter (**Gem som stil…** gemmer det, du har indstillet).

Til højre er **forhåndsvisningen**: de opgaver, generatoren ville lave, som bjælker, hver med den mængde, der gav dens varighed, og under dem optællingerne:

- **Tilknyttede elementer**: de elementer, opgaverne tager, ud af alle dem, der har brug for en opgave. Det er det vigtigste tal her. Bliver nogle udenfor, vælger **Vis elementerne uden opgave** dem i visningen, og **Et trin til dem** tilføjer et trin med deres typer.
- **Varigheder fra normer** og **Standardvarigheder (ingen norm)**: hvor mange opgaver en norm gav varigheden, og hvor mange der tog deres trins dage.
- **Forskalling og stilladser på deres værters opgaver** (nedenfor).

**Anvend** skriver tidsplanen som ét trin at fortryde. Skærmen deles så: modellen øverst, Gantt nederst.

### Fanen Start

- **Start**: tidsplanens første dag.
- **Opdel efter etage**: en opgave pr. etage for de trin, der går etage for etage (råhus, murværk, overflader); uden flueben en opgave pr. trin.
- **Nedrivning først**: de elementer, faserne river ned, kommer i en opgave **Nedrivning** før alle de andre (IFC-art DEMOLITION). Eksisterende elementer, der bliver, hører ikke til nogen opgave.
- **Forskalling** og **Stillads**: om disse midlertidige elementer lever efter deres værters opgaver (nedenfor).
- **Behold opgaver lavet i hånden**: når tidsplanen genereres igen (nedenfor).

### Fanen Rækkefølge

![Fanen Rækkefølge: trinene i rækkefølge](shots/planning-generate-sequence.webp)

Skabelonens trin i rækkefølge. Hvert kan slås fra, omdøbes og flyttes op eller ned med pilene. Det siger, om det gøres **én gang** for bygningen eller **pr. etage**, og hvordan det følger trinnet før på samme etage: slut–start (FS) eller start–start (SS) med forskydning, for overflader der kravler op efter råhuset. Blyanten åbner, hvad trinnet bygger: elementernes typer, valgt blandt modellens, og **Del** af dem (hele, kernen eller overfladerne).

Kæderne kommer af rækkefølgen: hver opgave følger trinnet før på samme etage og samme trin på etagen nedenunder. I bærende murværk og træskelet venter en etages vægge også på dækket på etagen nedenunder.

Hver opgave laves med en **regel** (dens typer, dens etage) og beholder derfor sine elementer: en væg tegnet efter genereringen kommer selv med i sin opgave.

### Fanen Varigheder

![Fanen Varigheder: normerne og for hvert trin norm, sjak og dage](shots/planning-generate-durations.webp)

En opgaves varighed kommer af dens elementers mængde (fra mængdeopgørelsen: kernens volumen, overfladernes areal, døre og vinduer pr. stk., stål efter vægt) og en **produktivitetsnorm**:

*varighed = mængde × mandetimer pr. enhed ÷ (sjak × timer om dagen)*, i hele arbejdsdage. For eksempel 4,5 m³ murværk til 4 mandetimer pr. kubikmeter med et sjak på 2, der arbejder 8 timer: 18 ÷ 16 = 1,125, altså 2 dage.

Normerne er dine: Lapin leverer ingen normsamling. **Importér normer…** læser din tabel fra en CSV- eller XLSX-fil: en kolonne til koden, én til teksten, én til enheden (m3, m2, m, stk, kg, t…), én til mandetimer pr. enhed (eller en produktivitet i enheder i timen) og eventuelt én til sjakket. **Skabelon (CSV)** gemmer kolonnerne med nogle rækker grove værdier skrevet af Lapin, hver mærket *indicative* (vejledende). **Brug Lapins vejledende værdier** lægger dem i projektet, så du kan prøve generatoren; dialogen advarer så om, at varighederne kommer fra vejledende værdier.

Nedenunder vælger hvert trin sin **Norm**, sit **Sjak** (0: normens eget) og sine **Dage**, der bruges, når ingen norm giver varigheden. Fanen Kalender viser den kalender, varighederne tælles i.

### Genereret igen

Åbn generatoren igen og klik **Anvend**: tidsplanen laves igen over de samme opgaver, som beholder deres id'er. Det, du har ændret i hånden, bliver:

- en genereret opgave, du har redigeret (navn, varighed, datoer, regel, del), er **Beholdt som redigeret**, afkrydset i dens Egenskaber; fjern fluebenet for at få den lavet igen fra dens trin;
- elementer, du har tilføjet eller taget ud i hånden, forbliver dens undtagelser;
- dine egne opgaver og kæder bliver; uden **Behold opgaver lavet i hånden** bliver kun de genererede.

![Den genererede tidsplan: modellen øverst, Gantt nederst](shots/planning-generated.webp)

I Egenskaber siger en genereret opgave, hvor dens varighed kom fra: mængden, normen og sjakket, eller trinnets dage.

![En genereret opgave i Egenskaber](shots/planning-generated-task.webp)

### Forskalling og stilladser

En forskalling eller et stillads fra **Byggepladsindretning** har ingen opgave selv: det lever efter opgaverne på det, det tjener. Forskallingen sættes op, når dens dæk, bjælke eller væg begynder, og tages ned, når typens dage er gået efter, at de er færdige (**Afforskallet efter**, i arbejdsdage: 21 for dæk og bjælker, 7 for vægge, søjler og fundamenter, medmindre typen siger andet). Stilladset står fra den første opgave på dets vægge til den sidste, overfladerne med. Vælg et, og dets Egenskaber siger, hvornår det er **På byggepladsen**, og efter hvilke opgaver det **Lever efter**.

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

## 4D-simuleringen

![Huset på en dag i tidsplanen: førstesalens dæk i gang i sin forskalling, væggene ovenover ikke startet](shots/planning-simulation.webp)

**Tidsplanlægning → Simulér** (også **Simulér** i Gantt-diagrammets værktøjslinje) viser modellen, som tidsplanen har den på en dag. Gantt-diagrammet åbner under visningen, hvis det ikke er åbent, med afspilleren øverst og dagen som et bånd hen over diagrammet:

- det, der **ikke er startet**, er skjult (eller vist som et blegt spøgelse, i indstillingerne);
- det, der er **i gang**, tegnes i sin farve (rav);
- det, der er **bygget**, tegnes, som det er;
- det, der er **under nedrivning**, er rødt, mens dets opgave kører, og er så væk;
- **forskalling og stilladser** står, i deres farve (blå), kun mellem de dage, deres værtselementers opgaver giver dem: et dæks forskalling fra støbningens start til afforskallingen, et stillads fra den første opgave på dets vægge til den sidste.

En lagdelt væg, muret i én opgave og pudset i en anden, vokser i to trin: først kernen, så pudsen. Elementer, som ingen opgave tager, forbliver, som de er. Et kort over modelvisningen viser dagen, hvad hver farve betyder, og hvor mange elementer der er i hver tilstand.

### Afspilning

![Afspilleren øverst i Gantt-diagrammet](shots/planning-simulation-bar.webp)

- **Afspil** (▶) kører dagene med den valgte hastighed (1 til 60 dage i sekundet) frem til slutningen; **Pause** stopper den.
- Pilene i hver side går **en dag** eller **en uge** tilbage eller frem.
- Træk skyderen, skriv en dato, eller klik og træk på Gantt-diagrammets tidsskala: båndet over diagrammet og modellen følger med.
- Ved siden af datoen: opgaverne i gang den dag.

Fra dag til dag ændres kun elementernes farver og synlighed: intet bygges igen, så en stor model afspilles lige så jævnt som en lille.

### Farverne

![Simuleringsindstillingerne](shots/planning-simulation-settings.webp)

Tandhjulet åbner simuleringsindstillingerne, gemt i projektet: farverne for det, der er i gang, under nedrivning, for forskalling og stilladser og for spøgelserne; om det, der **ikke er startet**, er skjult eller vist **som spøgelser**; om **elementer uden opgave** vises eller skjules; og om **lag bygget hver for sig** vises hver for sig. **Standardfarver** henter Lapins tilbage.

### Tilstand på dato i visninger og ark

![En plans Tilstand på dato i dens egenskaber](shots/planning-view-date.webp)

En plan, et snit, en facade eller en gemt 3D-visning har **Tilstand på dato** i sine egenskaber: visningen tegner så modellen, som tidsplanen har den den dag, i de samme farver, og dens ark udskriver den sådan (PDF'en viser det, skærmen viser). Når simuleringen kører, tager **markørens dato** dens dag. En visning på en dato tegner tidsplanens tilstande, ikke dens fases. × ved datoen henter modellen tilbage, som den er. En 3D-visning med en dato viser den dag, også når simuleringen er slået fra.

### Etapearket

![Etapeark: datoerne fra kapitlernes slutninger](shots/planning-stages-dialog.webp)

**Tidsplanlægning → Etapeark…** (også i afspilleren) sætter modellen på flere datoer side om side på et nyt ark. Den tegnes fra **3D-visningen, som den ser ud nu** (et billede på hver dato) eller fra en plan, et snit eller en facade, som motoren tegner på hver dato. Datoerne er **slutningen af hvert kapitel** (hver opgave øverst i WBS'en), **slutningen af hver måned** eller **valgte** datoer. Hvert billede er mærket med sin dato og opgaverne i gang.

![Et etapeark](shots/planning-stages-sheet.webp)

### Gantt-diagrammet på et ark

![Gantt-diagrammet på et ark](shots/planning-gantt-sheet.webp)

**Tidsplanlægning → Gantt på ark** sætter tidsplanen på et nyt ark: tabellen (række, WBS og opgave, dage, start, slut) og bjælkerne på en tidsskala, de kritiske opgaver i rødt, sammenfatningerne som klammer, milepælene som ruder, kæderne. En tidsplan, der er højere end arket, kommer på så mange ark, som den behøver, med sit hoved på hvert. Den tegnes som vektorer, ligesom visningerne, og udskrives i PDF'en.

### Eksportér video

![Eksportér video](shots/planning-video.webp)

**Tidsplanlægning → Eksportér video…** (også i afspilleren) optager 3D-visningen, hvorfra den ser nu, fra én dag til en anden, et billede pr. **en dag** eller **en uge**, med datoen og opgaverne i gang skrevet nedenunder. Filen er en WebM-video, eller MP4, hvor systemet kun tilbyder det (macOS-appen). En browser, der ikke optager video, gemmer i stedet billederne som PNG-filer i en ZIP.

## Fremdriften

![Gantt-diagrammet med fremdrift: kolonnen %, den udførte del mørkere i hver bjælke, basisplanen under den og afvigelsen](shots/planning-progress.webp)

Efterhånden som arbejdet skrider frem, siger tidsplanen, hvor langt det er, og flytter det, der mangler:

- **Statusdatoen** i Gantt-diagrammets værktøjslinje er den dag, fremdriften er meldt til og med. Det resterende arbejde begynder arbejdsdagen efter: en opgave, der ikke er påbegyndt, kan ikke begynde tidligere, og en igangværende gør det, der mangler, derfra. Diagrammet tegner den som en blå linje.
- En opgaves **% færdig** skrives i dens kolonne eller i Egenskaber. Uden det kommer det fra dens elementer markeret på byggepladsen: den udførte mængde af opgavens mængde (i dens enhed, når generatoren gav den en), ellers deres antal. En sammenfatningsopgaves kommer fra dens opgaver, vægtet efter deres varighed.
- **Faktisk start** og **faktisk slut** skrives i Egenskaber. Tomme kommer de fra markeringerne: den første dag et element blev markeret, og det sidste elements dag, når alle er færdige. En afsluttet opgave står på sine faktiske datoer, uden for den kritiske vej.
- En igangværende opgave beholder sin faktiske start og udfører den del af sin varighed, der ikke er gjort endnu (rundet op), efter statusdatoen; opgaverne efter den flytter med.

Bjælkerne viser den udførte del mørkere. Egenskaber siger, hvor en procentdel kommer fra, og hvor mange arbejdsdage der er tilbage.

![En opgaves fremdrift i Egenskaber](shots/planning-progress-task.webp)

### Markering af elementer på byggepladsen

**Tidsplanlægning → Markér færdig** (MD) og **Markér påbegyndt** (MS) markerer de elementer, der er valgt i 3D eller i en plan, på statusdatoen (i dag, når der ikke er nogen), i hver opgave, der omfatter dem; **Markér ikke påbegyndt** fjerner markeringerne. Et valgt elements egenskaber viser dets tilstand på byggepladsen med de samme tre knapper og i hver af dets opgaver den dag, det blev færdigt eller påbegyndt, med en knap, der markerer det færdigt i netop den opgave. En lagdelt væg i en murer- og en pudsopgave markeres i begge eller i én fra dens række.

![En vægs markeringer i dens egenskaber](shots/planning-element-marks.webp)

På byggepladsen, fra en telefon: gå ind i projektets delte session fra browseren (**Filer → Del session…** på den computer, der har det åbent). En gæst, der kun kom for at kigge, kan stadig markere elementer og skrive en opgaves fremdrift: markering er den eneste ændring, der er åben for alle gæster. Fotos og rapporter på opgaver hører til premium-udgaven.

### Basisplanen

**Tidsplanlægning → Gem basisplan** gemmer hver opgaves datoer og varighed, som de er nu; gemt igen erstattes den (én basisplan: flere basisplaner og scenarier hører til premium-udgaven). Gantt-diagrammet tegner den så som en tynd grå bjælke under hver opgaves bjælke (**Basisplan** i værktøjslinjen skjuler den), tilføjer kolonnen **Afvigelse** (de arbejdsdage, slutningen flyttede sig: positiv er forsinket, rækken markeret med rødt) og siger i sin linje, hvor mange opgaver der er forsinkede, og hvor langt slutningen flyttede sig. Egenskaber giver basisplanens datoer og afvigelserne i start og slut. **Ryd basisplan** fjerner den.

### Bagud og foran i simuleringen

![Simuleringen på statusdatoen: vinduerne bagud i forhold til basisplanen](shots/planning-progress-simulation.webp)

Med en basisplan sammenligner simuleringen: et element, der ikke er bygget (eller ikke påbegyndt) en dag, hvor basisplanen havde det sådan, er **bagud** (karmosin), og et, der er i gang eller bygget, før basisplanen lod det begynde, er **foran** (grøn). Frem til statusdatoen står elementerne på deres faktiske dage: et element markeret færdigt er bygget den dag, det blev markeret. **Sammenlignet med basisplanen** i simuleringens indstillinger slår det fra; dens to farver vælges også dér. Visninger og tegningsark på en dato viser det samme.

### Fremdriften i filerne

- **Eksportér XLSX** tilføjer kolonnerne **% complete**, **Actual start**, **Actual finish**, **Baseline start**, **Baseline finish**, **Finish variance** og **GlobalId** (regnearket er på engelsk). Udfyld dem på byggepladsen og læs dem tilbage med **Tidsplanlægning → Importér fremdrift…**: rækkerne findes efter GlobalId, WBS eller ID, og en *Status date* skrevet over tabellen sætter statusdatoen. En CSV virker også.
- **MS Project XML** bærer statusdatoen, hver opgaves procentdel, faktisk start og slut, den resterende varighed og basisplanen (Baseline 0); elementernes markeringer følger elementerne i *Lapin elements*.
- **IFC** skriver dem i hver opgaves IfcTaskTime (statustiden, faktisk start og slut, den resterende tid, færdiggørelsesgraden) og basisplanen som et andet IfcWorkSchedule af typen BASELINE; Lapins egne felter (den skrevne procentdel, markeringerne) står i `Lapin_Task`. Begge kommer tilbage til de samme datoer.
- **Administrer → Versioner → Sammenlign** siger ud over elementerne, hvad der ændrede sig i tidsplanen: slutningen, arbejdsdagene, statusdatoen og de opgaver, der kom til, blev slettet, flyttet (hvor mange arbejdsdage), eller hvis varighed eller fremdrift ændrede sig.

## Opgavernes omkostninger

![Gantt-diagrammet med kolonnen Omkostning](shots/planning-cost.webp)

Med et overslag (**Økonomi → Overslag…**) får Gantt-diagrammet kolonnen **Omkostning**: hver opgaves andel af overslaget, fra de poster der prissætter dens elementer (en sammenfatnings er dens opgavers). Værktøjslinjen viser **budgettet** (overslagets direkte omkostninger) og omkostningen **til dato** (hver opgaves omkostning gange dens færdiggørelse i %); Egenskaber viser en opgaves omkostning og den udførte del. **Økonomi → Omkostninger over tid** tegner S-kurven og månedstabellen (se [Overslag](cost.md)).

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

Omkostninger over tid står i [Overslag](cost.md). Ressourceudjævning, optjent værdi, månedlige acontoopgørelser og flere basisplaner og scenarier hører til premiumudgaven.
