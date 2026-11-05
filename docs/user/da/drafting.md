---
title: Tegning og kommandolinjen
order: 80
summary: Skriv kommandoer som i CAD, fang til punkter, flyt og kopiér, tegn linjer, og annotér med mål, tekst og mærker.
---

## Kommandolinjen

Skriv et værktøjs bogstaver hvor som helst i vinduet, og tryk derefter på <kbd>Enter</kbd>. Skriv for eksempel <kbd>WA</kbd> for en væg, <kbd>CO</kbd> for at kopiere, <kbd>M</kbd> for at flytte eller <kbd>L</kbd> for en linje. Kommandolinjen nederst viser, hvad værktøjet har brug for som det næste. Du kan klikke på et punkt eller skrive en længde, en vinkel eller koordinater.

Indtastede koordinater er i projektets enhed, som længderne ved markøren: i et projekt i millimeter gør `@4000,4000` efter et rektangels første hjørne det 4 m gange 4 m, og `4000<90` er 4 m lige op.

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

## At vælge

Klik på et element for at vælge det; <kbd>Shift</kbd>-klik føjer til valget. For at vælge flere på én gang trækker du med venstre knap, hvor intet er valgt. Trukket mod højre er rammen blå og tager det, der ligger helt inden i den (vindue); trukket mod venstre er den grøn og stiplet og tager alt, hvad den rører (krydsning), som i AutoCAD. Tryk på <kbd>Mellemrum</kbd>, mens du trækker, og rammen bliver til en lasso, der følger musen og beholder rammens tilstand; <kbd>Mellemrum</kbd> igen går tilbage til rammen. På kommandolinjen, når en kommando beder dig vælge objekter, skriver du **WP** eller **CP** og klikker på hjørnerne af en polygon, derefter <kbd>Enter</kbd>: en vinduespolygon eller en krydsningspolygon.

![En lasso i planen: rammen trukket mod højre, så Mellemrum, og løkken rundt om de to vægge, den tager](shots/select-lasso.webp)

## At kopiere og flytte

Vil du kopiere, så markér elementet og skriv <kbd>CO</kbd>. Klik på et basispunkt og derefter på et andet punkt. Lapin bliver ved med at kopiere, indtil du trykker på <kbd>Enter</kbd> eller <kbd>Esc</kbd>. **Flyt** (<kbd>M</kbd>) virker på samme måde.

Mens du flytter eller kopierer, bliver en markør stående på basispunktet, og en stiplet linje løber fra den hen til musen, mens et falmet omrids af det, du flytter, bliver stående, hvor det var, indtil du klikker eller trykker på <kbd>Esc</kbd>. Linjen og markøren skifter farve med fanget: grøn på et fanget punkt, blå når ortho eller polær sporing holder retningen, accentfarven på en sporingslinje, grå når den er fri. Ved siden af musen viser Lapin, hvor langt du har flyttet: Δx, Δy, afstanden og vinklen, i projektets enheder. Du kan skrive afstanden og vinklen der, som når du tegner. I brugerfladen Kompakt står disse værdier i statuslinjen. At trække i et greb viser det samme, og det gør det også at trække i selve et markeret element, fra det punkt hvor du trykkede; tryk på <kbd>Esc</kbd>, før du slipper, så bliver elementet, hvor det var.

![At flytte en væg: markøren på basispunktet, den stiplede linje hen til musen, væggens spøgelse hvor den var, og forskydningen ved musen](shots/move-trace.webp)

Du kan også trække i stedet. Træk i krydset midt på et markeret element for at flytte det. Hold <kbd>Ctrl</kbd> (<kbd>⌘</kbd> på en Mac) nede, mens du trækker, for at efterlade en kopi.

En dør, et vindue eller en vægåbning flyttes og kopieres langs sin væg: kun den del af flytningen, der ligger langs væggen, tæller, så `500` skrevet efter basispunktet skubber den 500 langs væggen; flyttet ind i en anden væg går den over i den væg. Er dens væg også markeret, følger den med væggen.

En modullinje flyttes, kopieres, roteres og spejles for sig selv eller sammen med andre elementer. En kopi er en ny modullinje i samme net, opkaldt efter den sidste modullinje, der løber samme vej: med modullinjerne 1, 2 og 3 bliver en kopi af en hvilken som helst af dem til 4.

## Tegning

Fanen **Tegn** tegner linjer, polylinjer, rektangler, cirkler og buer. Den redigerer dem med forskyd, trim, forlæng, afrunding, affasning og sammenføj, fylder områder med skraveringer og organiserer dem på **Lag**. **Importér DXF** henter en CAD-tegning ind som linjer, du kan fange til og redigere.

![Fanen Tegn](shots/ribbon-draft.webp)

**Forskyd** (<kbd>O</kbd>) kopierer en linje, en polylinje, et rektangel, en cirkel eller en bue i en afstand. Skriv afstanden i projektets enhed (i et projekt i millimeter er `500` en halv meter; `2m` eller `1'6"` siger deres egen enhed), klik på formen og så på den side, den skal til; den beder om den næste form, til du trykker <kbd>Enter</kbd> eller <kbd>Esc</kbd>. Skriv **T** (Through) i stedet, så går hver kopi gennem det punkt, du klikker. En polylinjes hjørner forbliver skarpe (i geringer), som i CAD. Forskyd virker i planer, snit, facader, på referenceplaner og i 3D-visningen.

Hver længde, kommandolinjen beder om, er i projektets enhed, og prompten siger hvilken: `Depth (mm)`, `Height (ft-in)`. Vinkler, faktorer og antal er almindelige tal: `90` efter **Rotér** er 90°, uanset enheden.

### Tegning i 3D-visningen

**Linje**, **Polylinje**, **Rektangel**, **Cirkel**, **Bue** og redigeringerne fra Tegning virker også i 3D-visningen, med kommandolinjen nederst. Mens en kommando kører, siger mærket øverst, hvor du tegner: i den aktive etages kote eller på det valgte referenceplan. **Vælg en flade** i mærket og så et klik på en flade i modellen laver et referenceplan på fladen og tegner på det. Punkter fanges som i en plan: til planens snitlinjer (væggenes hjørner og flader) og til det, der er tegnet på samme plan, med ortho og sporingslinjerne. Det, du tegner, er det samme tegneelement som et tegnet på planen og ses også der.

![Tegning i 3D-visningen: mærket siger planet, linjen følger musen i etagens kote](shots/draft-3d.webp)

### Tegninger i 3D-visningen

Det, du tegner på en plan (linjer, polylinjer, rektangler, cirkler, buer), står også i 3D-visningen som linjer i etagens kote (selve koten, ikke planens snithøjde); det, du tegner på et referenceplan, et snit eller en facade, står på det plan. Klik på en i 3D for at vælge den, og flyt, kopiér, forskyd eller slet den. **Vis → Tegninger i 3D**, også i **Vis → Visningsindstillinger** for en 3D-visning, viser eller skjuler dem alle; den er slået til, til du slår den fra. Mål, tekster, mærker, skraveringer og hjælpelinjer (rollen draft) bliver på deres visninger, og en DXF bragt ind på en plan forbliver en 2D-tegning, bortset fra dens linjer med koter.

## Annotering

Fanen **Annotér** har målene, tekst, modullinjer, niveauer og mærker. Et mål læser modellen, så det opdateres, når en væg flyttes. Hvordan tekst, mål, modullinjer og mærker ser ud, indstilles under **Administrer → Tegningsstandarder**.

Et justeret eller lineært mål sættes som en målekæde: efter de første to punkter og dets linje tilføjer hvert klik det næste punkt på samme linje, og **Enter** (eller **Esc**) gør hele kæden til ét mål (**U** tager det sidste punkt tilbage; skriv først **S** for et enkelt mål). En kæde opfører sig som en polylinje: ét klik markerer det hele, og den flyttes, kopieres og slettes som én. En kæde, der måler vægge, kanter eller modullinjer, bliver på dem: trukket eller flyttet for sig selv glider kun dens mållinje, vinkelret på kæden, så en glidning med musen aldrig efterlader den målende tom luft; flyttet sammen med det, den måler, følger den med. Dens greb sidder på hvert målt punkt (træk for at flytte det, Ctrl/⌘-klik for at fjerne det), midt på hvert segment (træk for at tilføje et punkt, Ctrl/⌘-klik for at tilføje et midtvejs) og ved mållinjens ender (træk for at flytte linjen). Vil du sætte flere punkter ind i en kæde, som i ArchiCAD, så markér den og klik på **Annotér → Tilføj punkter** (eller skriv **DIMADD**, eller **Tilføj punkter** i dens egenskaber): hvert punkt, du klikker, går ind i kæden der, hvor det falder langs dens linje, mellem punkterne omkring det eller ud over en ende som den nye ende, og holder fast i den væg, kant eller modullinje, det lander på; **Enter** eller **Esc** afslutter. Vil du tage punkter ud, så Ctrl/⌘-klik på deres greb, eller klik på **Annotér → Fjern punkter** (**DIMDEL**, eller **Fjern punkter** i dens egenskaber) og klik nær hvert punkt; segmenterne på hver side bliver til ét. Hvert segments værdi har også et greb: træk i det for at flytte værdien væk fra sin plads, hvor segmenterne er korte og værdierne ville overlappe; når den står væk fra sit segment, binder en tynd hjælpelinje den tilbage. Ctrl/⌘-klik på grebet, eller **Tekster tilbage på linjen** i egenskaberne, sætter den tilbage. En flyttet værdi bliver ved sit segment, når punkter flyttes, og drejer med kæden. Dobbeltklik på et segment for at skrive dets værdi: det element, det måler, flyttes, og det samme gør alt efter det i kæden. Egenskaberne viser hvert segments værdi og tekst, deler en kæde op i enkelte mål og samler enkelte mål, der fortsætter hinanden på én linje.

Annoteringer på en plan (mål, tekster, tegnede linjer, mærker) kan flyttes til en anden plan på samme sted, da planer deler koordinater: vælg dem og vælg planen i deres egenskaber, under **View** for at flytte dem eller **Copy to** for at kopiere dem. Sådan kommer mål lavet på en situationsplan også til etagens plan. Et måls punkter holder sig så til det, den nye plan skærer dér: vægge, kanter, akser.

### Mærker

**Annotér → Mærke** (<kbd>TG</kbd>) sætter en boks på en visning med det, et element siger om sig selv, og en henvisningslinje til det. Mens værktøjet er aktivt, vælger bjælken under visningen, hvad mærket viser: mærket, navnet, klassen eller typen, **Brandklasse**, **Lydisolering**, **Varmetransmission** eller **En anden parameter…** efter dens navn; eller en mærketype fra tegningsstandarderne; og **HENVISNING** slår henvisningslinjen til eller fra. Klik på elementet og derefter, hvor mærket skal stå (tast først **E** for at knække linjen). **Annotér → Mærk en parameter** spørger efter parameterens navn og starter værktøjet med den.

![Mærke-værktøjets bjælke under visningen](shots/tag-options.webp)

Et mærke læser enhver parameter: elementets egen, dets types (så viser alle typens elementer den), en standardegenskab som `Pset_WallCommon.FireRating` eller en, som et importeret element kom med. Et navn findes også uden mellemrum, så **Fire rating** og `FireRating` er det samme. Udfyld den under **Parametre** i elementets eller typens egenskaber: skriv et navn (Fire rating, Sound rating, Moisture resistance og Thermal resistance foreslås), eller vælg **Tilføj en standardegenskab…** for klassens IFC-egenskaber. Mærket følger med, så snart der er en værdi, og længder skrives i projektets enheder.

Et valgt mærke har greb. Træk i boksen for at flytte mærket: henvisningslinjen peger fortsat samme sted hen. Et mærke følger sit element: når elementet flyttes, drejes, spejles eller omformes med sine greb, følger mærket med og beholder sin plads ved siden af det, og henvisningslinjen bliver på det; et mærke, du har flyttet, beholder den afstand, du gav det. Træk i linjens midte for at knække den (Ctrl/⌘-klik på knækket retter den ud igen) og i dens ende for at lade den pege på et andet punkt (Ctrl/⌘-klik: tilbage på elementets midte). I egenskaberne ændrer du, hvad mærket **Viser**, giver det sin egen **Tekst** (`{mark}|{FireRating}` skriver to linjer), en **Mærketype**, eller slår **Henvisningslinjen** fra. Motoren tegner mærkerne, så tegningsark og PDF'er udskriver dem, som du ser dem. Mærketyperne under **Administrer → Tegningsstandarder → Mærker** læser de samme felter i deres skabeloner; et nyt projekt har Brandklasse, Lydisolering og Varmetransmission blandt dem.

![Et mærkes henvisningslinje knækket, med dens greb](shots/tag-leader.webp)

![Et mærkes egenskaber](shots/tag-properties.webp)

En dørs eller et vindues mærke redigeres del for del, hver tekst flyttet eller drejet og hver ende af linjen forlænget: se [Døre og vinduer](doors-windows.md).

#### Opbygningsmærke

Et opbygningsmærke skriver et elements lag som en arkitektnote: en overskrift (*EXTERIOR*), en linje for hvert lag, en bundtekst (*INTERIOR*). Et nyt projekt har **Build-up (exterior → interior)**: hver linje er materialets beskrivelse (dets navn, hvis det ingen har), dets λ, hvis det har en, og lagets tykkelse i mm. Vælg det i værktøjet **Mærke**s bjælke og klik på en væg, et dæk, et tag eller et rums overflade; andre elementer får det valgte felt. Teksten læses fra modellen, hver gang den tegnes: ret et lag, et materiales beskrivelse eller dets λ, og mærket følger med.

![To opbygningsmærker på en facadevæg: én henvisningslinje, og én pr. lag](shots/buildup-tag.webp)

Dets form indstilles i **Administrer → Tegningsstandarder → Mærker**: sæt en mærketypes **Form** til *opbygning*, eller klik på **Opbygning…** ud for en, for at redigere den ved siden af en forhåndsvisning på en af projektets væg- eller dæktyper.

- **Overskrift** og **Bundtekst**: skrives over og under lagene (tom: ingen; `|` starter en ny linje).
- **Hvert lag**: linjen, der skrives én gang pr. lag. Den læser lagets felter, `{description}`, `{material}`, `{thickness}` (i den enhed, der er valgt under **Tykkelse i**), `{note}` (lagets note i dets type), `{function}` (kerne, overflade, isolering…) og `{index}`; enhver af materialets parametre efter navn eller symbol, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (se materialets [Data](materials.md#data-beskrivelse-parametre-hvordan-det-males)); og elementets, `{name}`, `{type}`, `{mark}`. **Indsæt et felt…** sætter et ind, hvor markøren er, og viser de parametre, projektets materialer har.
- **Firkantede klammer** gør en del valgfri: `[, λ = {λ} W/mK]` skrives kun, hvor materialet har en λ; en del udgår helt, når et felt i den er tomt. **[ ]** gør markeringen til sådan en del. Skriv `\[` eller `\]` for at skrive selve klammen.
- **Rækkefølge**: fra ydersiden mod indersiden af en væg (siden med dens første lag er dens yderside, så en spejlet væg starter stadig der), fra toppen mod bunden af et dæk eller et tag; eller omvendt. Vælg den rækkefølge, lagene ligger i ved siden af noten, så henvisningslinjer pr. lag aldrig krydser hinanden.
- **Lag uden tykkelse** (membraner, folier, maling) skrives, medmindre du fjerner fluebenet ved **vist**. Et lag, der skifter materiale op ad højden, skriver en linje for hvert materiale.
- **Henvisningslinjer**: én, til elementet, eller **én pr. lag**, hver fra sin linje til et punkt inde i sit eget lag, hvor visningen kan skelne lagene (en væg i planer og snit, et dæk i snit; ellers beholder mærket én). Træk henvisningslinjens ende langs elementet for at krydse det et andet sted.
- **Linjer starter med** et punkt, en tankestreg, deres nummer eller ingenting; de ombrydes ved **Bredde mm** på papiret, hængende efter punkttegnet, og en værdi bliver ved sit symbol og sin enhed (`λ = 0.035 W/mK`).

![Editoren for en opbygningsmærketype ved siden af dens forhåndsvisning](shots/buildup-tag-type.webp)

Som ethvert mærke følger opbygningsmærket sit element, og tegningsark og PDF'er udskriver det, som du ser det, med λ, μ og ρ. En egen **Tekst** i dets egenskaber erstatter linjen pr. lag for netop det mærke.

### Modullinjer

**Annotér → Modullinje** (<kbd>GR</kbd>) tegner én akse ud fra dens to ender; den får den næste betegnelse i sin retning. **Annotér → Aksenet…** lægger et helt net på én gang: afstandene **På tværs** (de nummererede akser, fra venstre mod højre) og **På langs** (akserne med bogstaver, nedefra og op) i projektets enheder, fx `6000 6000 4500` eller `3*6000`; **Origo**, hvor de første akser krydser, en **Vinkel**, hvor langt akserne går **Ud over nettet**, de **Første betegnelser** og **Aksetypen**. Med **erstat nettets akser** markeret forsvinder de akser, der er tegnet indtil da.

![Dialogen Aksenet](shots/grid-dialog.webp)

Klik på en akse for at vælge den. Dens egenskaber viser **Start** og **Slut**, **Længde**, **Vinkel** og afstanden fra den parallelle akse før den (↔). Skriv en ny afstand, og aksen flytter sig; akserne bagved flytter sig også, medmindre du fjerner markeringen i **akserne bagved flytter sig også**. **Bobler** vælger, hvilke ender der har en boble. **Aksestile…** åbner **Tegningsstandarder → Modullinjer**, hvor boblen, dens størrelse, linjetypen og den første betegnelse indstilles. Vælg hele nettet for at se dets akser og deres afstande.

![En akses egenskaber](shots/axis-properties.webp)

Alle længder her følger projektets enheder. I fod og tommer læses ender og afstande som 19'-8 1/4", og niveauerne skrives også i fod og tommer.

![Den samme akse i fod og tommer](shots/axis-properties-imperial.webp)

![Fanen Annotér](shots/ribbon-annotate.webp)
