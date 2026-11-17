---
title: Tegningsark, PDF og DXF
order: 100
summary: Sæt visninger op på tegningsark med et tegningshoved, og udskriv dem til PDF eller DXF.
---

<!-- video sheets -->

## At lave et tegningsark

Klik på **+** ved siden af **Tegningsark** i projektbrowseren. Vil du placere en visning, så træk den fra browseren over på arket, eller vælg den under **Placér visning…** i arkets bjælke. Hver visning beholder sin egen skala og forbliver forbundet med modellen, så arket ændres, når modellen gør. På arket trækker du en visning for at flytte den: kun den flytter sig, med sin titel, mens rammen, tegningshovedet og de andre visninger bliver; den fanger rammens kanter og midte og de andre visninger, og piletasterne rykker den 1 mm (10 mm med Shift).

![Et A3-ark med en plan og et snit](shots/sheet.webp)

Markér arket for at udfylde dets **Tegningshoved** i egenskabspanelet: projektet, bygherren, hvem der har tegnet det, hvem der har kontrolleret det, datoen og revisionen. Dets nummer, papir og retning indstilles også der. Papiret er et A-format (A0 til A4), et af ISO’s lange ark (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), et nordamerikansk format (Letter, Tabloid, ARCH D, ARCH E) eller **brugerdefineret** med dets **Papirbredde** og **Papirhøjde** i millimeter. Efterlades projektfeltet tomt, viser det projektets navn: det navn, projektet har fået, ellers dets filnavn.

## Hvad en visning viser

En visning på tegningsarket viser det, du vælger, uden at du skal trække den i blinde:

- **Beskær den på arket.** Klik på en visning: der kommer håndtag i hjørnerne og på kanterne. Træk i dem, og rammen tager mere eller mindre af visningen, mens tegningen bliver, hvor den er på papiret. **Skift**-træk inde i visningen skubber tegningen i rammen.
- **Tegn dens grænse i visningen.** **Annotate → View boundary** (<kbd>VB</kbd>): tegn et rektangel på en plan, et snit eller en facade og giv det et navn, entydigt i projektet. I **Place view…** tilbydes visningen så med hver af sine grænser (»Ground floor plan › Østfløj«), og en visning placeret sådan viser netop det rektangel og følger det, når du flytter eller ændrer det i visningen. Samme visning kan stå på flere ark, én grænse hver. En placeret visning skifter mellem sine grænser, eller til fri beskæring, i listen på dens ramme. Grænser ses kun på skærmen, aldrig på papir.
- **Fastgør den.** Nålen på rammen holder visningen på plads: den kan vælges, men flyttes eller beskæres ikke ved en fejl.

Rammen og titelfeltet hører til papiret. De vælges eller flyttes aldrig, og et klik tager den mindste visning under markøren, så en visning lagt over titelfeltet altid kan vælges.

![En visning valgt på arket: dens beskæringshåndtag, dens grænse, nålen](shots/sheet-view-boundary.webp)

## Dit eget tegningshoved

**Administrer → Tegningshoveder…** viser tegningshovederne: Lapins egne (**Lapin standard**, som alle tegninger har haft hidtil, det rumænske **cartuș** og et **ISO 7200**-hoved), projektets og dem i denne computers bibliotek, som alle projekter på den tilbyder. Klik på et for at se det med projektets værdier. **Dupliker til projektet** laver en kopi, du kan redigere, **Ny** et tomt; **Gør til projektets standard** giver det til alle tegninger, der ikke vælger deres eget, og en tegning vælger sit eget under **Tegningshoved** i sine egenskaber.

![Tegningshoveder: Lapins, projektets og denne computers, det valgte med projektets værdier](shots/title-blocks.webp)

**Rediger…** åbner designeren. Hovedet er en boks i papirmillimeter, der sidder i et hjørne af rammen (nederst til højre som standard), så det samme hoved passer fra A4 til A0, stående eller liggende; rammens margener indstilles her. Tegn med **Linje**, **Rektangel**, **Polylinje**, **Tekst**, **Billede** (dit logo, PNG eller JPEG) og **Revisionstabel**; et rektangel eller en lukket polylinje får en udfyldning eller skravering, en tekst en højde eller en tekststil fra Tegnestandarder. Klik fanger elementernes ender, hjørner og midtpunkter og gitteret; **Shift** holder linjen lige. Træk et element for at flytte det, dets håndtag for at omforme det; pilene skubber det, **Delete** sletter det, **Ctrl+Z** fortryder.

Listen **Felter** har den levende tekst: projektets (navn, nummer, bygherre, grund, tilladelse, projekterende, personer og dine egne parametre), tegningens (nummer, navn, målestok, papir, revision, status, dato, tegnet, kontrolleret og godkendt af, fase, tegning x af y) og systemets (udskriftsdato, filnavn). Et klik sætter feltet i den valgte tekst eller placerer det som ny tekst; en tekst kan blande ord og felter, `Projekt: {project.name}`. Designeren viser hvert felts værdi på en eksempeltegning; et felt uden værdi vises gråt med sit navn, et Lapin ikke kender med rødt (det udskrives som `<felt>`, og **Administrer → Kontrol** melder det). **Gem** beholder hovedet som ét trin at fortryde.

![Designeren: et tegningshoved i finsk stil, felterne med deres værdier, et felt tilføjet under tegningsnummeret](shots/title-block-designer.webp)

Tegningerne, deres PDF, Udskriv og udgivelsen tegner det med hver tegnings værdier. I DXF er det en blok med attributter, én pr. levende tekst (`PROJECT_NAME`, `SHEET_NUMBER`…), som AutoCADs egne tegningshoveder. **Eksporter…** gemmer et hoved som `.json`-fil med dets billeder, og **Importer…** læser det ind i projektet eller biblioteket, så en tegnestue kan flytte sine hoveder fra én computer til den næste. Gamle projekter beholder Lapin standard og tegner præcis som før.

![Tegningen med sit eget tegningshoved](shots/sheet-title-block.webp)

## Tegning på arket

Noter, markeringer og forklaringer, der hører til arket og ikke til en af dets visninger, tegnes på selve arket. Klik på **Annotér** i arkets bjælke, eller dobbeltklik på papiret uden for visningerne (**Annotér → Annotér ark** gør det samme). Arket åbnes i tegneruden som sit papir, i 1:1, med visningerne, rammen og tegningshovedet under, og bjælken siger *tegning på arket*. Alle værktøjer under **Tegn** og **Annotér** virker der som på en visning, i papir-millimeter: skriv 50, og en linje er 50 mm lang på papiret, og en teksts højde er dens højde på papiret. Punkter fanges på rammen, tegningshovedet, visningernes rammer (blå stiplet) og det, visningerne tegner. **Tegningsark** i bjælken går tilbage til at arrangere visningerne.

- **Tekster** kan indeholde levende felter, som et tegningshoveds: `Ark {sheet.number} af {sheet.count}`, `{project.name}`, `{sheet.date}`. På en visning læses en tekst, som den er skrevet.
- **Note med henvisningslinje** (Annotér → Tekst): klik, hvor pilen peger, derefter, hvor teksten skal stå, og skriv den. Flyttes noten, tager den pilen med; **Henvisningslinje → fjern** i dens egenskaber fjerner pilen. Markeret har pilens spids og hvert knæk deres egne greb: træk i ét for at flytte det alene.
- **Linjer, polylinjer, rektangler, cirkler, buer, skraveringer, symboler og billeder** er tegneværktøjernes egne, med deres greb, lag og ændringsværktøjer. En nordpil er et af Lapins symboler (**Annotér → Symbol**). Symboler placeret med **Placér symbol…** i arkets bjælke vælges også her med et klik og flyttes i papir-millimeter.
- **Revisionssky** (Tegn → Fyld, Annotér → Tegningsark eller REVCLOUD på kommandolinjen): to hjørner, eller **P** for en polygon, der lukkes med Enter. **Revisionssky** i egenskaberne for enhver lukket polylinje eller cirkel gør den til en sky eller tilbage igen og angiver dens bue på papiret.
- **Signaturforklaring** (Annotér → Tegningsark): en levende liste over det, arkets visninger tegner: de materialer, deres snit er skraveret og udfyldt med, eller, med **Lister** i dens egenskaber, deres linjer efter rolle, type og tykkelse. Den ændrer sig med visningerne og modellen, og dens titel tager også felter. Dens egne ord (overskriften, linjernes roller og typer) skrives på det sprog, brugerfladen havde, da den blev placeret; **Sprog** i dens egenskaber ændrer dem.
- **Målestokslinje** (Annotér → Tegningsark): i målestokken for arkets første visning eller den, dens egenskaber angiver, en rund længde på omkring 80 mm eller en, du vælger. På en plan, et snit eller en facade tager en målestokslinje visningens målestok.

Det, der er tegnet på arket, udskrives på det, oven på visningerne, præcis som vist: i dets PDF, ved udskrivning og i dets DXF. Det hører til arket: **Duplikér tegningsarket** (dets knap i projektbrowseren) kopierer det sammen med de symboler, der er placeret på arket, og sletning af arket sletter det. Hver tegning er ét fortryd-trin. Ark lavet tidligere har intet tegnet på sig og udskrives som før. En udgivelse tæller det med: et ark, der kun er ændret af det, der er tegnet på papiret, eller af dets symboler, meldes ændret siden sidste udgivelse (Administrer → Kontrol, CDE).

![Tegning på et ark: en note med henvisningslinje, en revisionssky, en levende tekst, en signaturforklaring over planens materialer og en målestokslinje](shots/sheet-annotations.webp)

![Det samme ark, som det udskrives](shots/sheet-annotated.webp)

## Udskrivning

Knapperne **PDF** og **DXF** i arkets bjælke eksporterer arket. En PDF er klar til at blive udskrevet eller sendt. En DXF åbner i AutoCAD og andre CAD-programmer med sine lag, linjer og skraveringer; cirkler, buer og ellipser (tegnede, eller de buede kanter på dæk og andre omrids på en plan) skrives som rigtige CIRCLE-, ARC- og ELLIPSE-elementer, ikke korte linjer, og kommer tilbage som kurver, når DXF'en importeres igen. **Udskriv** sender arket til en printer eller plotter i dets egen papirstørrelse, 1:1, som vektorlinjer: vælg plotter og papir i udskriftsdialogen. På en mindre printer vælger du tilpas til siden.
