---
title: Brugerfladen og genveje
order: 15
summary: Tilstandene Komfortabel, Kompakt og Fokus, sidepaneler der foldes ind, og de tastaturgenveje, du kan ændre.
---

<!-- video interface -->

## Arbejdsområder

Et arbejdsområde viser kun de faner i båndet, dit arbejde har brug for, i den rækkefølge arbejdet bruger dem. Vælg det på startskærmen, øverst i **View → Interface…** eller fra **Commands…** (Workspace: …): **Alle** (hver fane i din udgave), **Arkitektur**, **Konstruktion og armering**, **Installationer**, **Grund og byggepladsindretning** eller **Tegning 2D**. Projektet forbliver ét: en skjult fanes værktøjer er stadig i Commands… og tastaturgenvejene, og en model lavet i ét arbejdsområde åbner helt i et andet.

![Arbejdsområderne i View → Interface…](shots/workspaces.webp)

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

Under delene **Faner**: fjern fluebenet ved de faner i båndet, du ikke bruger (en arkitekt kan skjule Structure, Rebar 2D og MEP, en ingeniør Architecture og Site). View bliver altid, for der ligger Grænseflade…. En skjult fanes værktøjer bliver i Kommandoer… og i tastaturgenvejene.

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

### Standardgenvejene

Dette er genvejene i Revit-stil, som Lapin har dem nu:

<!-- shortcuts -->

Taltasterne <kbd>1</kbd>–<kbd>9</kbd> vælger hurtigværktøjerne.
