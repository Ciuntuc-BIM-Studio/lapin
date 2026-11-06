---
title: Overslag
order: 98
summary: Overslaget ud fra modellen: dine prislister importeret fra CSV eller Excel, poster på typer og regler på materialer, mængder fra mængdeudtrækket gange enhedspriser opdelt i materiale, arbejdsløn, materiel og transport, indirekte omkostninger, avance og moms, RON eller EUR; de rumænske skemaer F3, F2 og C6 til C9 på ark, i Excel og PDF; IFC-omkostningsplaner.
---

## Fanen Udførelse

![Fanen Udførelse](shots/ribbon-cost.webp)

Økonomiens grupper ligger på fanen **Udførelse**, efter tidsplanens (fanen hed **Mængder**, siden **Økonomi**). De beholder mængderne og tilføjer overslaget:

- **Mængdeudtræk**, **Skemaer…** og **Eksportér XLSX**: modellens mængder (se [Skemaer og diagrammer](schedules.md)).
- **Prislister…**: dine priser, importeret fra en CSV- eller Excel-fil (nedenfor).
- **Tildel poster…**: hvilken post der prissætter hver type og hvert materiale (nedenfor).
- **Overslag…**: åbner overslaget under visningen (det laves første gang).
- **Skema på ark**, **Overslag XLSX** og **Overslag PDF**: overslaget og dets skemaer på et ark, i en projektmappe eller i en PDF (nedenfor).
- **Omkostninger over tid**: overslaget lagt på tidsplanen, S-kurven og månedstabellen (nedenfor).

Overslaget regnes ud fra modellen, hver gang den ændres. En væg, du tegner nu, er straks med i overslaget med sine mængder. Kun priserne, posterne på typerne, reglerne og dine egne linjer gemmes.

## Prislister

![Import af en prisliste: hver kolonnes betydning gættet ud fra overskriften](shots/price-import.webp)

Priserne er dine: Lapin leverer intet katalog med priser eller normer. **Udførelse → Prislister… → Importér…** læser en CSV- eller XLSX-fil, for eksempel en eksport fra dit kalkulationsprogram eller en leverandørs liste. Lapin viser filens første rækker under deres overskrifter. Over hver kolonne står det, Lapin har gættet, at den indeholder, ud fra overskriften, på engelsk eller rumænsk:

- **Kode** og **Beskrivelse** (*Cod*, *Simbol*, *Denumire*), **Enhed** (*U.M.*: m³ eller mc, m² eller mp, m, pcs eller buc, kg, t, 100 m²).
- Enhedsprisen opdelt i **Materiale**, **Arbejdsløn**, **Materiel** og **Transport** (*Material*, *Manoperă*, *Utilaj*, *Transport*), eller én **Enhedspris (ikke opdelt)**, som så tæller som materiale.
- Valgfrit: **Mandetimer pr. enhed** (*Normă de timp*) og **Fag** (*Meserie*), til lønlisten (C6).
- Valgfrit: postens ressourcer, rækker, der nævner den post, de hører til (**Den post, den hører til**), deres **Ressourceart** (M, Man, U, T) og deres **Forbrug pr. enhed**. En post, der kun er givet ved sine ressourcer, prissættes efter dem, og ressourcelisterne (C7 til C9) viser dem én for én.

Ret enhver kolonne, der er gættet forkert, vælg den række, overskrifterne står i, og om priserne er i RON eller EUR, og klik så på **Importér**. Listen kommer ind i projektet. Med **Gem også i denne computers bibliotek** afkrydset bliver den også på denne computer: under **På denne computer** henter **Føj til projektet** den ind i dit næste projekt.

**Skabelon** gemmer en CSV med kolonnerne og nogle rækker med Lapins egne omtrentlige værdier, markeret *vejledende*: udfyld den med dine priser, og importér den. **Brug de vejledende værdier** lægger de omtrentlige værdier ind i projektet som en liste, så du kan prøve overslaget, før du har dine egne. Erstat dem, før du sender et overslag til nogen.

## Poster på typer, regler på materialer

![Tildel poster: hver type og hvert materiale med sin post](shots/map-articles.webp)

En linje i overslaget kommer fra en **post**: en kode fra din prisliste. Der er to måder at give elementer en post på:

- **På en type.** Hvert element af typen prissættes efter den. Posten siger, hvad den måler: hele elementet eller en del af det (en lagdelt types **kerne** eller dens **overflader**), og efter hvilket mål (efter prisens enhed, eller netto- eller bruttovolumen, netto- eller bruttoareal, længde, antal, vægt). Den kan også lægge en faktor og spildet (%) til.
- **På et materiale (en regel).** Hvert lag af det materiale, i enhver type, prissættes efter den: én regel for kalkpudsen prissætter pudsen på alle dine vægge.

Typens post kommer først. Et lag, som typens poster allerede måler, prissættes ikke igen af en regel. En post på hele elementet prissætter det hele, så reglerne træder til side. Giv en lagdelt væg en post på typens **kerne** (murværket efter volumen) og regler for dens overflader (pudsen efter areal). Væggen prissættes så lag for lag: murværket under **Vægge**, pudsen under **Overflader og isolering**.

**Udførelse → Tildel poster…** viser de typer i projektet, som elementer bruger, og materialerne i deres lag. Hver række starter med den post, den har, eller den, du valgte for en type eller et materiale med samme navn i et tidligere projekt, eller den, Lapin foreslår: en kode skrevet i navnet, eller den post, der deler flest ord med det (*zidărie*, *BCA*, *tencuială*, *C25/30*). For en lagdelt type foreslår den kernen. Skriv eller vælg en kode, vælg delen og målet, afkryds de rækker, du vil have, og klik på **Anvend**: ét trin at fortryde. Dine valg huskes på denne computer til dine næste projekter.

Forskallings- og stilladstyperne i byggepladsindretningen (fanen **Grund**) havde allerede en postkode (beslutning 152): det er en post på typen, målt efter prisens enhed.

Faserne tæller: et element, der var der før arbejdet (lavet i en *eksisterende* fase), prissættes ikke. Et nedrevet prissættes kun af dets types post, hvis del er **Nedrivningen af det (kun når det nedrives)**, under kapitlet **Nedrivning**.

## Overslaget

![Overslaget: kapitler, linjer, satserne og totalerne](shots/estimate.webp)

**Udførelse → Overslag…** åbner overslaget under visningen, som Gantt. Dets linjer er samlet i **kapitler** efter arbejdsart: jordarbejder, fundamenter, konstruktion, vægge, tag, døre og vinduer, overflader og isolering, installationer, byggepladsindretning, andet. En post kan nævne sit eget kapitel. **Efter etage og arbejdsart** sætter etagen foran hvert kapitel.

Hver linje viser koden, beskrivelsen og enheden fra prislisten, **mængden** fra modellen (den samme som i mængdeudtrækket), enhedsprisen opdelt i **Materiale**, **Arbejdsløn**, **Materiel** og **Transport**, enhedsprisen og **værdien**. Klik på en linje for at vælge dens elementer i visningerne. Vælg elementer i en visning, så markeres deres linjer i overslaget.

Under linjerne står totalerne, sådan som rumænske overslag stiller dem op:

1. **Direkte omkostninger**: summen af linjerne.
2. **Bidrag på arbejdsløn (CAM)**: 2,25 % af arbejdslønnen som standard.
3. **Indirekte omkostninger** (*cheltuieli indirecte*): 10 % af de direkte omkostninger med bidragene som standard.
4. **Avance**: 5 % af de direkte og de indirekte omkostninger som standard.
5. **I alt uden moms**, **Moms** (21 % som standard) og **I alt med moms**.

Ret enhver sats i værktøjslinjen. Hver værdi afrundes til to decimaler, sådan som et overslag udskriver den.

Overslaget er i **RON** eller **EUR**. **1 EUR =** er projektets kurs: en prisliste i den anden valuta omregnes efter den, og feltet ved siden af viser også hver værdi i den anden valuta.

Værktøjslinjen advarer også om det, der ikke er prissat: **N type(r) uden pris** (klik for at åbne Tildel poster) og de koder, ingen prisliste har. **En egen linje** tilføjer arbejde, modellen ikke har (byggepladshegnet, en reserve): skriv dens kode, beskrivelse, enhed, mængde og priser. En linje med en kode og uden priser tager dem fra prislisten.

## De rumænske skemaer

![Tilbudslisten (F3) for det samme overslag](shots/estimate-f3.webp)

Listen over overslaget viser selve overslaget eller et af dets skemaer. De er grupperinger af det samme overslag, så de giver de samme totaler. Deres overskrifter er på rumænsk, sådan som de afleveres i Rumænien:

- **Tilbudsliste (F3)**: *Lista cu cantitățile de lucrări pe categorii de lucrări*, kapitel for kapitel, enhedspriserne og værdierne opdelt i materiale, arbejdsløn, materiel og transport, og totalerne under dem.
- **Overslag pr. objekt (F2)**: *Deviz pe obiect*, hvert kapitels værdi uden moms, dets moms og dets værdi med moms.
- **Arbejdsløn (C6)**: timerne og værdien pr. fag (fra prislistens fag og mandetimer eller dens lønressourcer).
- **Materialer (C7)**, **Materiel (C8)** og **Transport (C9)**: ressourcerne med deres mængder, enhedspriser og værdier. En post uden ressourcer tæller som én ressource for sig selv.

Skemaerne følger almindelig praksis efter metodikken i bekendtgørelse 863/2008. Tjek dem mod det, din bygherre eller støtteprogrammet beder om: spørg os, hvis du har brug for en anden opstilling.

**Placér på ark** lægger den viste tabel på et nyt ark, klar til at blive udskrevet med de andre. **XLSX** gemmer overslaget og hvert skema i én projektmappe, et regneark hver. **PDF** gemmer dem som PDF, A4 på tværs, klar til udskrift.

## Omkostninger over tid

![Omkostninger over tid: S-kurven over månedernes søjler, månedstabellen under den](shots/cost-time.webp)

Når projektet har en tidsplan (fanen **Udførelse**), lægges overslaget på den uden nogen kobling af sin egen. Hver posts værdi deles mellem dens elementer, og hvert elements andel går til de opgaver, der bygger den del, posten måler: murværkets post til opgaven, der tager væggenes kerne, pudsens til den, der tager deres overflader. En post på hele elementet, bygget i to opgaver, deles ligeligt mellem dem. En egen post går til den opgave, du vælger ved dens beskrivelse (**I ingen opgave** som standard). En opgaves omkostning fordeles jævnt over dens arbejdsdage: intet bruges i en weekend, på en helligdag eller, for en våd opgave, i vinterpausen.

**Udførelse → Omkostninger over tid** åbner under visningen:

- **S-kurven**: den planlagte akkumulerede omkostning over tidsplanen, **basisplanens** (stiplet), når en er gemt, og det **udførte arbejde** (tykt) frem til statusdatoen: hver opgaves omkostning gange dens færdiggørelse i %, fordelt fra dens faktiske start;
- månedernes søjler, mod skalaen til højre: planlagt og udført;
- over den **budgettet ved afslutning** (overslagets direkte omkostninger), **omkostningen til dato** og det, der **ikke er planlagt**, hvis noget: poster hvis elementer ingen opgave bygger (et klik åbner overslaget);
- under den **månedstabellen**: hvert kapitels omkostning pr. måned, det ikke planlagte og dets sum.

Værdierne er direkte omkostninger, som overslagets poster. Gantt-diagrammet viser hver opgaves omkostning i kolonnen **Omkostning** (se [Tidsplanlægning](planning.md)).

![S-kurven på et ark](shots/cost-curve-sheet.webp)

**Placér på ark** lægger kurven på et nyt ark, tegnet som vektorer med dens signaturforklaring. **Månedstabel på ark** placerer månedstabellen, som også er skemaet **Omkostninger pr. måned** i overslagets liste: kapitlerne måned for måned, derefter de direkte omkostninger, summerne uden og med moms (fordelt forholdsmæssigt), de akkumulerede omkostninger og, med fremdrift, det udførte arbejde. Hver række summer til overslagets total. **XLSX** gemmer kurven dag for dag, månedstabellen og opgavernes omkostninger i et regneark; **PDF** gemmer kurven og månedstabellen.

**Administrer → Versioner → Sammenlign** siger, ved siden af elementerne og tidsplanen, hvad der er ændret i overslaget: totalen med moms, de direkte omkostninger og forskellen, hvert kapitel hvis omkostning ændrede sig, og posterne tilføjet, fjernet eller ændret (mængde og værdi).

Månedlige acontoopgørelser (*situații de lucrări*), optjent værdi og flere basisplaner hører til premiumudgaven.

## IFC

**Filer → Eksportér IFC…** skriver overslaget som en **IfcCostSchedule**: dets kapitler og linjer som **IfcCostItem**s, hver linjes mængde og dens enhedspriser som fire **IfcCostValue**s (Material, Labour, Equipment, Transport), de elementer, hver linje måler, tildelt den (**IfcRelAssignsToControl**), valutaen som projektets **IfcMonetaryUnit**. Importeret beholder et overslag linjerne, som filen har dem, med de samme totaler. Afkryds **Linjer fra modellen** for at tilføje de linjer, din model giver.

Med en tidsplan tildeles hver post også de opgaver, den udføres i (**IfcRelAssignsToControl** til deres **IfcTask**). Importeret beholder posterne deres opgaver, så S-kurven kommer tilbage den samme.
