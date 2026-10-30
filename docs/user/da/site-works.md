---
title: Byggepladsindretning
order: 55
summary: Facadestilladser og forskalling til dæk og bjælker, lagt ud fra modellen af deres konfiguratorer.
---

Fanen **Byggepladsindretning** modellerer byggepladsens midlertidige arbejder: facadestilladser og forskallingen af dæk og bjælker. Lapin lægger dem ud fra modellen og tæller, hvad de kræver. Når modellen ændres, følger de med.

![Fanen Byggepladsindretning](shots/ribbon-site-works.webp)

Stilladser og forskalling er **Midlertidig**: de opstilles og nedtages i fasen **Udførelse**. Den første, du placerer, tilføjer den fase, hvis projektet ikke har den, og gør den til den aktive fase. Visninger af tidligere faser udelader dem. Se **Administrer → Faser**.

![Et stillads rundt om huset, første sals forskalling på sine stempler](shots/site-works-3d.webp)

## Stilladser

- **Stillads på vægge** (<kbd>SW</kbd>): markér væggene (Shift-klik for flere), og tryk derefter på den. Stilladset går rundt om deres ydersider, i typens afstand fra væggen, op til væggenes top. Det lukker selv sine hjørner. Flyt en væg, eller gør den højere, og stilladset følger med.
- **Stillads** (<kbd>SC</kbd>): tegn dets linje i plan langs facaden, og tryk på Enter for at afslutte. Stilladset står til venstre for linjen. Vælg den anden side i dets egenskaber.

Hvert lige stykke deles op i typens faglængder, med så få fag som muligt for at nå enden. Etagerne stiger, indtil det øverste dæk er inden for rækkevidde af arbejdshøjden. Rækværket står over det øverste dæk. Fødderne står på terrænet, på fodspindler. Hvis terrænet falder mere, end spindlerne kan optage, siger Lapin det.

![Et stillads' egenskaber](shots/scaffold-properties.webp)

I dets egenskaber kan du indstille **arbejdshøjde**: lad den stå tom for væggenes top. Du kan også vælge, om fødderne står **på terrænet** eller **på etagen**, eller løsne det fra væggene med **frigør**.

Hvert stillads og hver forskalling har en **Type**. Du vælger den øverst i egenskaberne eller giver ét elements type til andre med Matchtype (MA). Under **Typeparametre** står de værdier, konfiguratoren indstiller: systemet, faglængderne, bredden, etagehøjden, krydsfinéren, afstandene, varen. Du kan også ændre dem der. En ændring hører til typen, så alle elementer af den type følger den. Opret lignende (CS) tegner et nyt af samme type. En type, du opretter i konfiguratoren, er den, det næste stillads eller den næste forskalling tager.

**Stilladstyper…** åbner konfiguratoren. Den indstiller systemet (modulært eller rammer), faglængderne, bredden og etagehøjden, afstanden fra væggen, dækkene, rækværkerne og fodlisterne, forankringerne, fodspindlerne, adgangsfagene og beskyttelsesnettet. Opstalten viser et stykke af den længde og højde, du vælger, med det, den tæller. Med et stillads valgt åbner konfiguratoren på dets type; klik på en anden i listen for at ændre den i stedet. Listen viser, hvor mange stilladser hver type har. **Gem type** anvender ændringen på alle stilladser af typen.

![Stilladskonfiguratoren](shots/scaffold-configurator.webp)

## Forskalling

- **Forskalling** (<kbd>FW</kbd>): markér dæk og bjælker, og tryk derefter på den. Hvert element får forskallingen af alle sine flader, der kan forskalles.
- **Forskalling på flade** (<kbd>FF</kbd>): vælg én flade. I 3D lyser fladen under markøren op med sit navn: et dæks underside, en bjælkes bund eller en af dens sider. I plan: klik på et dæks kant eller inde i dets omrids. En flade, der vælges senere, kommer på samme forskalling.

Et **dæks forskalling** dækker dets underside med krydsfinér på sekundære bjælker, på primære bjælker, på stempler. Bjælkerne under dækket forskalles ikke igen, og det gør de vægge og søjler, det hviler på, eller dets åbninger heller ikke. Afstandene kommer fra typens tabel, efter dækkets tykkelse. Stemplerne når ned til dækket nedenunder, terrænet eller etagen under det. De frie kanter forskalles også, lige så højt som dækket plus en opkant. I egenskaberne vælger du, hvilke flader der forskalles, og retningen af de primære bjælker.

En **bjælkes forskalling** er dens bund, på bærebjælker og stempler, og dens sider. Hver side er lige så høj som bjælken under det dæk, der støbes sammen med den. Søjler og dybere bjælker afbryder den. Du kan tilføje trekantlister langs bundkanterne.

**Forskallingstyper…** åbner konfiguratoren, til dæk og til bjælker. Til dæk indstiller den systemet (træbjælker og stempler, eller paneler), krydsfinéren, bjælkerne og deres kataloglængder, afstandstabellen, kanterne, stemplerne og genbrugene. Til bjælker indstiller den bærebjælkerne, stemplerne, åggene efter bjælkens dybde, affasningen og genbrugene. Snittet viser lagene og det stempel, motoren vælger til den højde, du vælger. Med en forskalling valgt åbner den på den forskallings type og slags.

![Forskallingskonfiguratoren](shots/formwork-configurator.webp)

## Vægge, søjler, ringbjælker og fundamenter

**Forskalling** og **Forskalling på flade** virker også på vægge, søjler og fundamenter:

- **En væg** får begge flader og sine frie ender, minus sine døre og vinduer. En lagdelt væg med en ringbjælke eller et betonbånd får kun dem. I dens egenskaber kan du vælge begge flader, ringbjælkerne eller de frie ender.
- **En søjle** får sine sider, op til det dæk eller den bjælke, der støbes på den.
- **Et fundament** får siderne af hvert trin i et punktfundament, eller siderne af et stribefundaments sål og stamme.

Det, der står op mod en flade, udelades: en anden væg, en søjle. Et dæk støbt ovenpå stopper fladen ved dets underside. Typen indstiller panelerne (deres bredder og højder), forskallingsbåndene gennem vægge, søjleklemmerne og skråstiverne.

![Konfiguratoren til vægge og søjler](shots/wall-formwork-configurator.webp)

## Forskallingsplan

**Forskallingsplan** laver en plan af den aktive etage i fasen Udførelse, skåret en meter over gulvet, **set nedefra**: en loftsplan. Stemplerne er skåret. Forskallingen over dem tegnes, som du ville se den, når du kigger op: de primære og sekundære bjælker, krydsfinéren og kanterne. Den ligger, som planen ligger, så de to passer sammen.

**Forskallingsplan ovenfra** laver samme plan set ovenfra, med forskallingen tegnet stiplet over snittet. Enhver plan kan skiftes mellem de to i dens egenskaber (**Set fra**) eller under **Visningsområde**.

![Stueetagens forskallingsplan](shots/formwork-plan.webp)

## Hvad de tæller

Hvert stillads og hver forskalling viser sine mængder i sine egenskaber:

- stilladsets areal, fag, etager, spir, dæk og forankringer;
- forskallingens kontaktareal pr. flade, stempler efter størrelse og bjælker efter kataloglængde.

**Forskallingsskema** og **Stilladsskema** føjer deres tabeller til projektbrowseren under Skemaer, så de kan sættes på et ark eller eksporteres.

Forskallingsskemaet slutter med en linje pr. type:

- **sættet**: den største mængde forskalling, der støbes på én gang. Det er pr. udførelsesfase, når arbejdet er delt i flere, ellers pr. etage.
- **hvad der skal købes**: sættet, eller det samlede divideret med typens genbrug, alt efter hvad der er størst.

Hver type har sin **vare** til tilbudslisten: koden i de normer, dit overslag bruger, og dens tekst. Du indtaster dem i konfiguratoren, og skemaet viser dem på hver linje.

I IFC skrives de som samlinger (`IfcElementAssembly` FORMWORK og SCAFFOLD) med status TEMPORARY. Delene er members, plates og accessories. Hver følger med sin byggevareressource, og forskallingen er forbundet med det element, den forskaller.
