---
title: Brugerfladen og genveje
order: 15
summary: Tilstandene Komfortabel, Kompakt og Fokus, sidepaneler der foldes ind, og de tastaturgenveje, du kan ændre.
---

<!-- video interface -->

## Arbejdsområder

Et arbejdsområde viser kun de faner i båndet, dit arbejde har brug for, i den rækkefølge arbejdet bruger dem. Vælg det med knappen ved siden af **Filer**, som viser det arbejdsområde, du er i, eller på startskærmen, øverst i **View → Interface…** eller fra **Commands…** (Workspace: …): **Alle** (hver fane i din udgave), **Arkitektur**, **Konstruktion og armering**, **Installationer**, **Grund og byggepladsindretning** eller **Tegning 2D**. Projektet forbliver ét: en skjult fanes værktøjer er stadig i Commands… og tastaturgenvejene, og en model lavet i ét arbejdsområde åbner helt i et andet.

![Arbejdsområderne i View → Interface…](shots/workspaces.webp)

Når båndets værktøjer er bredere end vinduet, ruller musehjulet over dem dem sidelæns.

Fanerne bliver på én linje. Når vinduet er for smalt til dem alle, flytter de sidste ind under **Mere** for enden af linjen, og fanen, du er på, bliver altid synlig.

Fanerne er grupperet efter det arbejde, de tjener. **Interiør** samler overfladerne og møblerne. **Grund** samler terrænet, landskabet og byggepladsindretningen (stilladser og forskalling). **Udførelse** samler tidsplanen og overslaget (4D og 5D).

Vis, skjul eller flyt en fane under **Tabs** i samme dialog, og valget bliver **Mit arbejdsområde**, gemt på denne computer til næste gang; de andre arbejdsområder er der stadig at skifte til. **View** bliver altid, da Interface… ligger i den.

## Tre tilstande

**Vis → Brugerflade…** (<kbd>Ctrl</kbd> <kbd>.</kbd>) vælger, hvor meget der står omkring visningen. Vælg en af de tre tilstande øverst i dialogen; ændringen ses med det samme.

![Dialogen Brugerflade: de tre tilstande og hver del for sig](shots/interface-dialog.webp)

- **Komfortabel** er vinduet, som de andre sider viser det: båndet med store værktøjer og deres navne, en kommandolinje under hver tegning, og over visningen HUD'en, tippet, felterne for længde og vinkel, oversigtsplanen og hurtigværktøjerne.
- **Kompakt** giver visningen det meste af vinduet:

![Kompakt: mere plads til tegningen](shots/interface-compact.webp)

1. **Båndet** er ét bånd af ikoner. Tynde streger adskiller grupperne. Hold musen over et værktøj for at se dets navn, dets taster og dets gruppe.
2. **Projektbrowseren** foldes til en skinne. Dens ikoner åbner et øjeblik projektet, niveauerne eller IFC-træet over visningen.
3. **Hurtigværktøjerne** <kbd>1</kbd>–<kbd>9</kbd> står i visningens venstre kant.
4. **Oversigtsplanen** er en knap. Klik på den for at åbne kortet, og på dens × for at folde det ind igen.
5. **Én statuslinje** rummer værktøjets navn og hvad det beder om, det du skriver, felterne for længde og vinkel mens du tegner, Fang, Ortho og POLAR, niveauet og visningen og derefter projektet.

- **Fokus** beholder kun fanerne og en tynd kommandolinje. Klik på en fane, og dens værktøjer folder sig ud over visningen; båndet foldes ind igen, så snart du vælger et værktøj, eller med <kbd>Esc</kbd> eller et klik et andet sted. Panelerne skjules. Når noget er markeret, viser et lille kort, hvad det er, og dets første egenskaber; **Alle egenskaber…** åbner egenskabspanelet over visningen. <kbd>Tab</kbd> henter begge paneler frem igen og skjuler dem igen.

![Fokus: markeringen på et kort](shots/interface-focus.webp)

I alle tilstande har den øverste bjælke **Fortryd** og **Annuller fortryd** i sin højre ende: med deres navne i Komfortabel, som to pile i Kompakt og Fokus (<kbd>Ctrl</kbd> <kbd>Z</kbd> og <kbd>Ctrl</kbd> <kbd>Y</kbd>; deres værktøjstip siger, hvad de fortryder).

Under tilstandene kan hver del indstilles for sig: båndet, hurtigværktøjerne (eller ingen: taltasterne virker stadig), kommandolinjen, HUD'en, værktøjets prompt og felter, oversigtsplanen, panelerne og højden på egenskabsrækkerne. Ændrer du én del, bliver valget **Tilpasset**.

Under delene **Faner**: fjern fluebenet ved de faner i båndet, du ikke bruger (en arkitekt kan skjule Structure, Simpel armering og MEP, en ingeniør Architecture og Site). View bliver altid, for der ligger Grænseflade…. En skjult fanes værktøjer bliver i Kommandoer… og i tastaturgenvejene.

Lapin husker dit valg på denne computer, for dig. Skrivebordsprogrammet og browseren husker hver deres. I browseren starter Lapin i Kompakt, når vinduet er under 800 pixels højt, indtil du selv vælger.

## Hurtigværktøjer

Hurtigværktøjerne er de nummererede pladser på visningens venstre kant, op ad projektbrowseren, så de flytter med den; små og gennemsigtige, indtil markøren kommer hen over dem (View → Interface… kan i stedet lade dem svæve nederst i visningen); tasterne <kbd>1</kbd>–<kbd>9</kbd> starter dem. Hvert arbejdsområde har sine egne: de begynder, som din udgave giver dem, og du vælger dem selv:

- træk et værktøj fra båndet til en plads. Et værktøj står kun på én plads, så det forlader den, det stod på;
- træk en plads hen på en anden for at bytte dem;
- højreklik på en plads: **Vælg et værktøj…**, **Tøm denne plads** eller **Nulstil hurtigværktøjer**;
- **View → Quick tools…** sætter de ni pladser fra lister, og **Udgavens egne** henter din udgaves egne tilbage.
- et værktøj, der sætter elementer af en type (væg, dør, dæk, trappe…), kan holde en af projektets typer: vælg den i pladsens højrekliksmenu eller ved siden af værktøjet i **View → Quick tools…**. Pladsen viser så typens navn og starter værktøjet med den type, så én plads kan holde murstensvæggene og en anden skillevæggene. Typen gemmes ved sit navn: pladsen virker i hvert projekt med en type af det navn, og i et uden den tager værktøjet sin aktuelle type.

Svævende nederst viser hver plads værktøjets navn, og en tom plads efter den sidste venter på et værktøj. Lapin gemmer dine hurtigværktøjer på denne computer, for dig.

Pilen for enden af bjælken skjuler hurtigværktøjerne; et lille **Hurtigværktøjer**-håndtag bliver tilbage og henter dem frem igen. De skjules hver for sig i modellens visninger (planer, 3D, snit) og i tegningsark og skemaer, så du kan have dem skjult på dokumenterne og fremme på tegningerne.

![Hurtigværktøjerne, trappen på plads 7 og plads 2 tom](shots/quick-tools.webp)

![View → Quick tools…](shots/quick-tools-dialog.webp)

## Mine værktøjer

**Mine værktøjer** er en fane i båndet, der er din egen, den første i fanerækken: du lægger kun de værktøjer i den, du bruger, taget fra en hvilken som helst fane, i grupper du selv navngiver.

- **Læg værktøjer i.** Højreklik på et værktøj i båndet → **Føj til Mine værktøjer**, **Føj til Mine værktøjer i…** en gruppe, eller **Føj til en ny gruppe i Mine værktøjer**. Eller træk et værktøj fra en anden fane hen på fanen **Mine værktøjer**: holdt der et øjeblik åbner fanen sig, og du slipper værktøjet, hvor en streg viser, at det lander.
- **Ordn dem i selve båndet.** Træk et værktøj før eller efter et andet, i sin gruppe eller ind i en anden gruppe; træk en gruppe ved dens navn for at flytte den blandt grupperne. En streg viser, hvor det lander, de andre glider til side, og slipper du et andet sted, ændres intet. Dobbeltklik på en gruppes navn for at omdøbe den. Højreklik på et værktøj: **Gør lille** eller **Gør stor**, **Flyt tidligere**, **Flyt senere** og **Fjern fra Mine værktøjer**; højreklik på en gruppes navn: **Omdøb gruppe**, **Slet gruppe**, **Ny gruppe** og **Fortryd den sidste ændring**. Med tastaturet flytter <kbd>Alt</kbd> <kbd>←</kbd> eller <kbd>→</kbd> det fokuserede værktøj, og <kbd>Delete</kbd> fjerner det.
- **Tilpas Mine værktøjer…** (tandhjulet for enden af fanen, View → Interface eller Commands…) viser til venstre alle programmets værktøjer, søgbare og grupperet efter fane, og til højre din fane. Træk værktøjer over, eller tryk **+** (eller <kbd>Enter</kbd>) for at tilføje et; træk på højre side for at ordne; træk et værktøj tilbage til venstre for at tage det ud. Hver række har også knapper til at flytte den op eller ned, til at vise den som stort eller lille værktøj, en liste til at flytte den til en anden gruppe og **×** til at fjerne den. Med tastaturet flytter <kbd>Alt</kbd> <kbd>↑</kbd> <kbd>↓</kbd> det fokuserede værktøj (også over gruppens ender), <kbd>Alt</kbd> <kbd>←</kbd> <kbd>→</kbd> flytter det til gruppen før eller efter, <kbd>Delete</kbd> fjerner det, og <kbd>Ctrl</kbd> <kbd>Z</kbd> fortryder den sidste ændring. **Fortryd** og **Nulstil** står nederst, så et forkert slip mister intet.
- **Eksportér…** og **Importér…** gemmer fanen som en lille fil, du kan tage med til en anden computer eller give til en kollega.

Lapin gemmer Mine værktøjer på denne computer, til dig (skrivebordsprogrammet og browseren hver for sig). Et værktøj, din udgave ikke har, er skjult, ikke tabt: det kommer tilbage med sit modul.

![Mine værktøjer](shots/my-tools-empty.webp)

![Mine værktøjer](shots/my-tools-menu.webp)

![Mine værktøjer: to grupper, Vægge og Åbninger](shots/my-tools-tab.webp)

![View → Interface → Tilpas Mine værktøjer…](shots/my-tools-dialog.webp)

## Kataloget

Fanen **Katalog** i projektbrowseren viser projektets typer for hvert værktøj, der sætter elementer med type: vægge, døre, vinduer, dæk, glasfacader, trapper, ramper, lofter og fodpaneler. Hver type er tegnet lille (en vægs lag som dens plansnit, et dæks i snit, en dørs eller et vindues opstalt, et fodpanels profil) med, hvad den er, i få ord.

- et klik på et kort sætter elementer af den type: værktøjet starter med den;
- træk et kort til en hurtigplads for at beholde værktøjet med den type der;
- **Redigér typer…** åbner dialogen, hvor typerne laves og ændres.

![Kataloget: vægtyperne](shots/catalog.webp)

![Vinduestyperne, tegnet efter deres opstalt](shots/catalog-windows.webp)

## Sidepaneler der foldes ind

I alle tilstande kan projektbrowseren og egenskabspanelet foldes ind til en tynd skinne i deres side:

- knappen øverst til højre i panelet folder det ind, og pilen på dets skinne folder det ud;
- dobbeltklik på kanten mellem panelet og visningen;
- <kbd>Ctrl</kbd> <kbd>B</kbd> folder browseren ind eller ud, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> egenskaberne, og <kbd>Tab</kbd> begge.

Klik på et ikon på et indfoldet panels skinne for at åbne panelet over visningen uden at folde det ud. Det forsvinder med <kbd>Esc</kbd> eller et klik et andet sted; dets nål holder det åbent. Træk i kanten af et panel for at gøre det bredere; Lapin husker bredden.

![En indfoldet browser åbnet over visningen fra sin skinne](shots/browser-overlay.webp)

## Paneler på begge sider

**Vis → Paneler** (View → Panels) placerer projektbrowseren og egenskabspanelet, hvor du vil, som i Revit: **Browser til venstre, Egenskaber til højre** (standard), **Browser til højre, Egenskaber til venstre**, **Begge paneler til venstre** eller **Begge paneler til højre**. Står begge på samme side, ligger de under hinanden; træk i skillelinjen mellem dem for at dele højden (dobbeltklik halverer), og i sidens kant for at gøre den bredere. Hvert panel kan stadig foldes for sig: et foldet panel er en tynd bjælke i stakken, eller en skinne, når begge er foldet. Assistentens fane (skrivebord) bliver ved egenskaberne. Lapin husker opstillingen på denne computer.

![Browser og egenskaber stablet til venstre](shots/panel-layouts.webp)

## Lys og mørk

Månen yderst til højre på båndet gør brugerfladen mørk (grafit); solen gør den lys igen (papir). I det mørke tema ligger planer, snit og facader på den mørke baggrund med de sorte penne i lyst blæk; ark forbliver papir. Konfiguratorernes små tegninger (en dørs eller et vindues plan, snit og facade, et tags, en altans, en elevators, en hals, en trappes…) og forhåndsvisningerne af mærker, tegningshoveder og materialer tegnes på et lyst papirkort i begge temaer, så deres linjer, tekster og mål læses som på en udskrevet tegning.

## Flere elementer på én gang

Vælg flere elementer (Skift-klik eller en ramme), og egenskabspanelet viser det sidst valgte, med **N valgt** ved siden af dets klasse. En værdi, de deler, vises; en, der er forskellig, står som *varierer*. Ændr et felt, og hvert valgt element, der har det, får den nye værdi, i ét trin, som **Fortryd** tager tilbage: højde og basisforskydning for vægge, typen for hver valgt væg, dør eller bjælke, brystningen for vinduer, faserne for hvert valgt element uanset klasse, lag og farve for tegnede linjer, stilen for tekster. Det, der er hvert elements eget, forbliver dets eget: et rums nummer, en vægs længde, en dørs plads langs dens væg.

## Overfør egenskaber

**Redigér → Overfør egenskaber** (<kbd>MA</kbd>, eller `MATCHPROP` på kommandolinjen som i AutoCAD; Revit kalder det Match Type) giver andre elementer det, ét element har. Vælg det element, der kopieres fra, eller start kommandoen og klik først på det; klik derefter på hvert element, der skal have dets egenskaber. Elementet under markøren lyser op før klikket, i planer, snit og 3D. Hvert klik er ét trin, som **Fortryd** tager tilbage. **Anvend på markeringen** giver dem til alle de andre valgte elementer på én gang. Tryk <kbd>Enter</kbd> eller <kbd>Esc</kbd>, når du er færdig.

Tandhjulet i kommandolinjens bjælke viser, hvad der overføres, og Lapin husker dit valg: **Type** (til et element af samme klasse), **Mål og indstillinger** (højde, forskydninger, materialer, snitprioritet, en teksts stil, en dørs eller et vindues karmplacering og brystning; til en anden klasse kun de felter, den også har), **Parametre** (dine egne; et standardsæts kun til samme klasse), **Faser**, **Lag, farve og skravering** (tegnede former) og **Navn** (slået fra til at begynde med). Hvor et element står, dets form og dets GlobalId overføres aldrig. Et fastgjort element får dem alligevel: en fastgørelse holder et element på plads, ikke dets indstillinger.

![Overfør egenskaber i plan: væggen, der kopieres fra, væggen under markøren fremhævet, og hvad der overføres](shots/match-properties.webp)

## Tastaturgenveje

**Vis → Tastaturgenveje…** (<kbd>KS</kbd>) viser alle de kommandoer, tastaturet kan nå, fane for fane. Hver række viser kommandoens genveje.

![Tastaturgenvejene, søgt efter "wall"](shots/shortcuts-dialog.webp)

- **Søg** efter en kommandos navn eller efter en tast.
- **+** tilføjer en genvej: tryk på en kombination (<kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>), eller skriv op til tre bogstaver og tryk på <kbd>Enter</kbd>. <kbd>Esc</kbd> stopper.
- Hvis en anden kommando allerede har genvejen, siger Lapin hvilken. **Flyt** tager den fra den kommando, **Behold begge** lader begge beholde den (den første på listen vinder), **Annuller** ændrer ingenting.
- **×** fjerner en genvej. Pilen for enden af en række sætter kommandoen tilbage, som forudindstillingen har den, og **Nulstil alt** gør det for alle kommandoer.
- **Forudindstilling** vælger det sæt, du starter fra: **Revit-stil** (standard: Revits sædvanlige bogstaver, hvor Lapin har kommandoen) eller **Lapin klassisk** (Lapins egne bogstaver fra før).
- **Eksportér…** skriver dine genveje til en fil, og **Importér…** læser en, til en anden computer eller en kollega.

Værktøjstip, menuerne og kommandopaletten (<kbd>Ctrl</kbd> <kbd>K</kbd>) viser altid genvejene, som de er nu.

### Bogstaver og kommandolinjen

Bogstaver som <kbd>WA</kbd> virker, når intet felt har fokus og kommandolinjen er tom: skriv dem over visningen. Kommandoen køres med det samme. Når en kommando i kommandolinjen starter med de samme bogstaver (<kbd>DI</kbd> og <kbd>DIMADD</kbd>), venter Lapin et øjeblik: skriv videre for at skrive den længere kommando, eller tryk på <kbd>Enter</kbd> eller <kbd>Space</kbd> for at køre den korte nu. Mens en kommando beder om et punkt eller en værdi, går bogstaver og cifre til den. Kommandoer i kommandolinjen som <kbd>L</kbd>, <kbd>M</kbd> eller <kbd>TRIM</kbd> efterfulgt af <kbd>Enter</kbd> virker i alle forudindstillinger. <kbd>Esc</kbd> annullerer altid.

Når de to støder sammen, vælger du, hvordan de deler tastaturet:

- **Bogstavgenveje (som i Revit)**, i denne dialog og i **Indstillinger → Generelt → Tastatur**, er slået til som standard. Slå det fra for kun at beholde kommandolinjen: hvert bogstav går dertil, og <kbd>Enter</kbd> eller <kbd>Mellemrum</kbd> kører en kommando, et alias af dig eller, hvis teksten ikke er nogen af dem, den kommando, bogstaverne er (<kbd>WA</kbd> <kbd>Enter</kbd> starter stadig væggen). Akkorder (<kbd>Ctrl</kbd> + en tast, funktionstasterne, <kbd>Delete</kbd>) og de hurtige værktøjers taltaster virker stadig.
- Tasten <kbd>`</kbd> (den til venstre for <kbd>1</kbd>, fundet efter sin plads på ethvert layout og en genvej som de andre: **Kommandolinje: tag al tastning**) giver kommandolinjen al tastning, med bogstaverne slået til eller fra. Linjen får en ramme og siger det; intet bogstav starter et værktøj, intet tal vælger et hurtigt værktøj. <kbd>Enter</kbd> kører det skrevne og giver tastningen tilbage, medmindre en kommando derefter beder om et punkt eller en værdi: så består tilstanden, til kommandoen er slut. <kbd>Esc</kbd> eller samme tast forlader den.

### Kommandoaliasser

Siden **Kommandoaliasser** i dialogen rummer dine egne navne til kommandoer. Skriv 1 til 12 bogstaver eller cifre (det første et bogstav), find kommandoen på listen (en kommandolinjekommando som <kbd>LINE</kbd> eller en hvilken som helst kommando i båndet eller programmet), og tryk **Tilføj alias**. Skriv aliasset i kommandolinjen og tryk <kbd>Enter</kbd> eller <kbd>Mellemrum</kbd>. Dine kommer før de indbyggede navne. Siden siger, hvad et alias erstatter: et indbygget kommandolinjenavn, en genvej med de samme bogstaver eller en genvej, det får til at vente (med bogstaverne slået til venter en genvej, der er et alias eller begyndelsen på et, et øjeblik, så du kan skrive videre og trykke <kbd>Enter</kbd>). Aliasserne gemmes sammen med genvejene, også i **Eksportér…** og **Importér…**.

![Kommandoaliasser, med hvad et alias skygger for](shots/shortcuts-aliases.webp)

### Standardgenvejene

Dette er genvejene i Revit-stil, som Lapin har dem nu:

<!-- shortcuts -->

Taltasterne <kbd>1</kbd>–<kbd>9</kbd> vælger hurtigværktøjerne.

## Flytte en dialog

Enhver dialog flyttes ved sin titellinje, som et vindue: tryk på titlen og træk, med musen eller en finger. Den bliver inden for vinduet, åbner igen, hvor du lod den, så længe programmet er åbent, og et dobbeltklik på titlen sætter den tilbage i midten. Så kan du flytte en dialog væk fra den del af modellen, du arbejder på, eller se modellen bag den.
