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

- **Navn** og **Nummer.** Nye rum nummereres efter niveau: 101, 102… på stueetagen. Et rums navn er dets funktions, indtil du giver det et eget; tømt, går det tilbage til funktionen.
- **Funktion**, **Afdeling** og **Gulvbelægning.** Skriv hvad som helst, eller vælg en værdi, som andre rum allerede har.
- **Højde.** Tomt betyder niveauets højde.
- **Egen farve** og **Egen skravering** tilsidesætter farveskemaet for netop dette rum.

Mængderne nedenfor viser rummets nettoareal (uden søjlerne), dets omkreds og dets volumen. Træk i grebet på stemplet for at flytte stemplet.

![Et rums egenskaber](shots/room-properties.webp)

## Rumstempel

**Arkitektur → Rumstempel** (eller **Rumstempel…** i et rums egenskaber) vælger, hvad hvert rums stempel viser på planer, linje for linje: navn, nummer, funktion, afdeling, areal, bruttoareal, omkreds, højde, volumen eller gulvbelægning, eller en egen tekst. Første linje er fed. **Tegningsstandarder → Mærker** viser samme stempel som tekst: `{name}|{number}|{area}`.

![Dialogen Rumstempel](shots/room-tag.webp)

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

## Rummål

**Annotate → Room dimensions** (<kbd>ID</kbd>) måler indersiden af rummene på en plan på én gang. Hver side af et rum får en justeret målekæde lige indenfor. Kæden har et punkt i hvert hjørne af siden og ved lysningerne for hver dør og hvert vindue i den.

- Med rum valgt måles kun de. Uden valg måles alle rum på planens etage.
- Bedt om på en situationsplan, som ser hele modellen ovenfra og ikke skærer nogen væg, kommer kæderne i stedet på etagens plan. En måls teksthøjde kan sættes for sig i Egenskaber (**Text height**, mm på papiret; tom: målestilens). Teksthøjder gælder på papiret, så på en situationsplan i 1:500 er den samme tekst fem gange større i modellen end på en plan i 1:100.
- Punkterne holder fast i væggene som ved ethvert mål. Flyt en væg, og kæderne følger med.
- Kør det igen på et rum for at lave dets kæder på ny, for eksempel efter at have tilføjet en dør. De gamle kæder erstattes, de fordobles ikke.
- Meget korte sider under 30 cm udelades. Enhver kæde redigeres eller slettes som en, du har tegnet selv.

![To rum målt indvendigt: hver sides kæde med døren og vinduerne](shots/room-dimensions.webp)

## Rumoverflader

**Overflader → Beklæd rum** (<kbd>FR</kbd>) lægger en overflade i det rum, du klikker i: dets gulv, dets vægges overflade og dets loft, hver efter rummets omrids, som rummet selv. Flyt en væg, og overfladerne følger med. **Overflader → Rumoverflader** redigerer overfladerne; **Fra biblioteket** henter færdige: Stue, Soveværelse, Badeværelse, Køkken, Stueetage på terrændæk og Teknikrum. Mens værktøjet er aktivt, viser egenskabspanelet den overflade, det lægger, så du kan vælge den før klikket. Et rums **Overflade**, i dets egenskaber, lægger en anden eller fjerner den.

En overflade har:

- **Gulv**, dets lag fra færdigt gulv og ned (fliser, klæber, afretningslag…). Dækkets huller i rummet (et trapperum) trækkes fra. **Gennem dørene, til væggens akse** fører gulvet videre under hver dør, så naborummets gulv møder det i væggens akse.
- **Vægge**, deres lag fra vægfladen ind i rummet, også rundt om rummets øer. Hvert lag kan skifte materiale op ad sin højde, som et lag i en vægtype: i Badeværelset maling over fliser til 2100 (13 mm tykt, fliser og klæber); i Køkkenet et flisebånd fra 900 til 1500. Et stykke uden materiale efterlader intet der. Dørene og vinduerne i væggene rundt om rummet skæres ud, og deres lysninger beklædes med de samme lag, fra rummets flade til karmen (ved en tom åbning til væggens akse); også et vindues bundlysning, medmindre en vinduesplade dækker den på rummets side. **Højde**: tom, op til loftet.
- **Loft**, dets lag fra dækket over rummet og ned (fra rummets højde, når der intet dæk er), dets huller trukket fra.
- **Fodliste**: en fodlistetype lagt rundt om rummet, afbrudt ved dets døre (se Fodlister, under Dæk og tage).
- **Et bærende dæk under gulvet**: et eget armeret betondæk under gulvets lag, med dets tykkelse og hvor langt det går ud over rummets omrids (under væggene). Det er et dæk som alle andre, i konstruktionen og i dets skema.

**Gem** ændrer overfladen, og hvert rum, der har den, lægges igen. **Beklæd valgte rum** lægger den i de valgte rum.

**Skravering i plan.** På planer skraveres et færdigt rums gulv, som dets overflade siger, under væggene, så tegninger, PDF og DXF udskriver det (lag FINISH-HATCH). I overfladens **Gulv** vælger **Skravering i plan** **efter materialet** (standard: det øverste lags overflademønster, dets skravering til facader, hvor det ses, i **Administrer → Materialer**, ellers et som navnet antyder: fliser, brædder for parket og laminat, et kryds for tæppe, linjer for vinyl, prikker for et støbt gulv, ellers skrå linjer), **eget mønster** (ethvert mønster i projektet, med skala, vinkel og farve) eller **ingen skravering**; skala og vinkel gælder også materialets mønster. En plan set nedefra (en spejlet loftsplan) skraverer i stedet lofterne, efter **Skravering i plan** under **Loft**, på samme måde. **Gulvskravering** i rummets egenskaber slår den fra for det rum. **Skraveringer af gulvbelægninger** i en visningsstil (**Vis → Visningsindstillinger**, **Administrer → Visningsstile**) slår den fra for hele visninger: til i Architectural, fra i Structural, MEP og Coordination. Med rumfarver på planen fylder farven rummet, og skraveringen tegnes ovenpå.

![En plan med badeværelsets gulv skraveret som fliser og stuens som brædder](shots/room-finish-hatch.webp)

Hver overflade måler, pr. materiale, sit **brutto**areal (rummets omrids, intet fratrukket: gulvets areal, væggenes omkreds gange hvert stykkes højde) og sit **netto**areal (det, der bygges: døre, vinduer og dækhuller trukket fra, lysningerne og gulvet under dørene lagt til; lysningernes andel står i mængderne, som RevealArea) samt sit volumen. **Overflader → Overfladeskema** viser hvert beklædt rum pr. del og materiale, med totalerne pr. materiale til sidst; som ethvert skema laver det diagrammer, eksporteres og kommer på et tegningsark. I IFC er en overflade en IfcCovering: FLOORING, CLADDING eller CEILING.

En overflade beklæder det, rummets vægge allerede har: hvis vægtyperne har deres egen puds, så giv rummets vægge kun det, der kommer ovenpå (maling, fliser), eller brug vægtyper med kun kernen.

![Rumoverfladerne: Badeværelsets gulv, væggene med fliser til 2100 og loftet](shots/room-finishes.webp)

![Et rums egenskaber med dets overflade](shots/room-finish-properties.webp)

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
