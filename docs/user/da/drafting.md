---
title: Tegning og kommandolinjen
order: 80
summary: Skriv kommandoer som i CAD, fang til punkter, flyt og kopiér, tegn linjer, og annotér med mål, tekst og mærker.
---

## Kommandolinjen

Skriv et værktøjs bogstaver hvor som helst i vinduet, og tryk derefter på <kbd>Enter</kbd>. Skriv for eksempel <kbd>WA</kbd> for en væg, <kbd>CO</kbd> for at kopiere, <kbd>M</kbd> for at flytte eller <kbd>L</kbd> for en linje. Kommandolinjen nederst viser, hvad værktøjet har brug for som det næste. Du kan klikke på et punkt eller skrive en længde, en vinkel eller koordinater.

![At kopiere en væg: efter basispunktet viser kommandolinjen og de direkte felter længden og vinklen hen til musen](shots/command-line.webp)

Hvert værktøj viser sine bogstaver i sit værktøjstip; du kan ændre dem under **Vis → Tastaturgenveje…** (se [Brugerfladen og genveje](interface.md)). Disse er de mest brugte:

| Model | | Tegn | |
|---|---|---|---|
| Væg | <kbd>WA</kbd> | Linje | <kbd>L</kbd> |
| Dør, vindue | <kbd>DR</kbd>, <kbd>WN</kbd> | Polylinje | <kbd>PL</kbd> |
| Dæk | <kbd>SB</kbd> | Rektangel, cirkel, bue | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Tag | <kbd>RF</kbd> | Flyt, kopiér | <kbd>M</kbd>, <kbd>CO</kbd> |
| Trappe, rum | <kbd>ST</kbd>, <kbd>RM</kbd> | Rotér, spejl, skalér | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Søjle, bjælke | <kbd>CL</kbd>, <kbd>BM</kbd> | Forskyd, trim, forlæng | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Snit | <kbd>SE</kbd> | Afrunding, affasning | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Mål op | <kbd>ME</kbd> | Skravering, tekst | <kbd>H</kbd>, <kbd>T</kbd> |
| Tilpas alt | <kbd>ZE</kbd> | Slet | <kbd>E</kbd> |

## Fang og hjælpere

Lapin fanger til enderne, midtpunkterne og skæringerne af det, der er tegnet, og til gitteret. <kbd>F3</kbd> slår fang til eller fra, <kbd>F8</kbd> ortho og <kbd>F10</kbd> polær sporing. Deres tilstand vises nederst til højre i visningen.

## At kopiere og flytte

Vil du kopiere, så markér elementet og skriv <kbd>CO</kbd>. Klik på et basispunkt og derefter på et andet punkt. Lapin bliver ved med at kopiere, indtil du trykker på <kbd>Enter</kbd> eller <kbd>Esc</kbd>. **Flyt** (<kbd>M</kbd>) virker på samme måde.

Mens du flytter eller kopierer, bliver en markør stående på basispunktet, og en stiplet linje løber fra den hen til musen, mens et falmet omrids af det, du flytter, bliver stående, hvor det var, indtil du klikker eller trykker på <kbd>Esc</kbd>. Linjen og markøren skifter farve med fanget: grøn på et fanget punkt, blå når ortho eller polær sporing holder retningen, accentfarven på en sporingslinje, grå når den er fri. Ved siden af musen viser Lapin, hvor langt du har flyttet: Δx, Δy, afstanden og vinklen, i projektets enheder. Du kan skrive afstanden og vinklen der, som når du tegner. I brugerfladen Kompakt står disse værdier i statuslinjen. At trække i et greb viser det samme, og det gør det også at trække i selve et markeret element, fra det punkt hvor du trykkede; tryk på <kbd>Esc</kbd>, før du slipper, så bliver elementet, hvor det var.

![At flytte en væg: markøren på basispunktet, den stiplede linje hen til musen, væggens spøgelse hvor den var, og forskydningen ved musen](shots/move-trace.webp)

Du kan også trække i stedet. Træk i krydset midt på et markeret element for at flytte det. Hold <kbd>Ctrl</kbd> (<kbd>⌘</kbd> på en Mac) nede, mens du trækker, for at efterlade en kopi.

En modullinje flyttes, kopieres, roteres og spejles for sig selv eller sammen med andre elementer. En kopi er en ny modullinje i samme net, opkaldt efter den sidste modullinje, der løber samme vej: med modullinjerne 1, 2 og 3 bliver en kopi af en hvilken som helst af dem til 4.

## Tegning

Fanen **Tegn** tegner linjer, polylinjer, rektangler, cirkler og buer. Den redigerer dem med forskyd, trim, forlæng, afrunding, affasning og sammenføj, fylder områder med skraveringer og organiserer dem på **Lag**. **Import DXF** henter en CAD-tegning ind som linjer, du kan fange til og redigere.

![Fanen Tegn](shots/ribbon-draft.webp)

## Annotering

Fanen **Annotér** har målene, tekst, modullinjer, niveauer og mærker. Et mål læser modellen, så det opdateres, når en væg flyttes. Hvordan tekst, mål, modullinjer og mærker ser ud, indstilles under **Administrer → Tegningsstandarder**.

Et justeret eller lineært mål sættes som en målekæde: efter de første to punkter og dets linje tilføjer hvert klik det næste punkt på samme linje, og **Enter** (eller **Esc**) gør hele kæden til ét mål (**U** tager det sidste punkt tilbage; skriv først **S** for et enkelt mål). En kæde opfører sig som en polylinje: ét klik markerer det hele, og den flyttes, kopieres og slettes som én. En kæde, der måler vægge, kanter eller modullinjer, bliver på dem: trukket eller flyttet for sig selv glider kun dens mållinje, vinkelret på kæden, så en glidning med musen aldrig efterlader den målende tom luft; flyttet sammen med det, den måler, følger den med. Dens greb sidder på hvert målt punkt (træk for at flytte det, Ctrl/⌘-klik for at fjerne det), midt på hvert segment (træk for at tilføje et punkt, Ctrl/⌘-klik for at tilføje et midtvejs) og ved mållinjens ender (træk for at flytte linjen). Vil du sætte flere punkter ind i en kæde, som i ArchiCAD, så markér den og klik på **Annotér → Tilføj punkter** (eller skriv **DIMADD**, eller **Tilføj punkter** i dens egenskaber): hvert punkt, du klikker, går ind i kæden der, hvor det falder langs dens linje, mellem punkterne omkring det eller ud over en ende som den nye ende, og holder fast i den væg, kant eller modullinje, det lander på; **Enter** eller **Esc** afslutter. Vil du tage punkter ud, så Ctrl/⌘-klik på deres greb, eller klik på **Annotér → Fjern punkter** (**DIMDEL**, eller **Fjern punkter** i dens egenskaber) og klik nær hvert punkt; segmenterne på hver side bliver til ét. Hvert segments værdi har også et greb: træk i det for at flytte værdien væk fra sin plads, hvor segmenterne er korte og værdierne ville overlappe; når den står væk fra sit segment, binder en tynd hjælpelinje den tilbage. Ctrl/⌘-klik på grebet, eller **Tekster tilbage på linjen** i egenskaberne, sætter den tilbage. En flyttet værdi bliver ved sit segment, når punkter flyttes, og drejer med kæden. Dobbeltklik på et segment for at skrive dets værdi: det element, det måler, flyttes, og det samme gør alt efter det i kæden. Egenskaberne viser hvert segments værdi og tekst, deler en kæde op i enkelte mål og samler enkelte mål, der fortsætter hinanden på én linje.

![Fanen Annotér](shots/ribbon-annotate.webp)
