---
title: Studio
order: 85
summary: Modellér frie legemer, skitseret på enhver flade eller ethvert plan, og skub, træk, afrund, udhul, del og gentag dem i 3D.
---

## Fanen Studio

Fanen **Studio** laver legemer, som ingen anden fane bygger: en bænk, et halvtag, en håndliste, et springvand, en sokkel på en facade. Hvert legeme starter fra en skitse eller en grundform. Derefter ændrer du det i 3D med trin, og hvert trin kan stadig redigeres.

![Fanen Studio](shots/ribbon-studio.webp)

Et Studio-legeme har en IFC-klasse som ethvert element. Vælg den i dets egenskaber: en stang, en plade, en beklædning, et rækværk, et dæk eller en anden. Dets mængder og dets eksport følger klassen.

## Hvor du skitserer

Et legemes omrids tegner du med værktøjerne under **Tegn**, som lukkede polylinjer, rektangler og cirkler. Linjer og buer, der mødes ende mod ende, giver også et lukket omrids. En lukket form inde i omridset er et hul.

Du kan skitsere på fire slags visninger:

- **En plan** tegner i sin etages højde.
- **Et snit** tegner på sin snitlinje, lodret.
- **En facade** eller **et referenceplan**: se *Referenceplaner* nedenfor.

## At lave et legeme

- **Ekstrudér** (EXTRUDE) skubber omridset lige ud. Skriv S for begge veje fra skitsen, T for en konicitet, der hælder siderne ind, og Cut for at det skærer de elementer, du valgte før. Vil du føre det op til et andet elements flade, så brug **Op til → en flade…** i dets egenskaber, eller **Ekstrudér til flade**.
- **Sweep** (SWEEP) fører en profil langs en sti. TWist drejer snittet undervejs, SCale gør det større, og RIse løfter det. En cirkel, der stiger, bliver en skruelinje og går TUrns gange rundt.
- **Overgangsform** (LOFT) forbinder profiler tegnet på forskellige visninger. Skriv S for at gå glat gennem dem. En linje valgt med rollen *skinne* bøjer den som styr.
- **Drej** (REVOLVE) drejer en profil om en aksellinje.
- **Grundformer** sætter med ét klik en kasse, en cylinder, en kugle, en kegle, en torus, en kile eller en pyramide. Deres mål og placering ændrer du i egenskaberne.

![En håndliste ført langs en cirkel, der stiger 3 m på to omgange](shots/studio-helix.webp)

Hvert legeme forbliver bundet til sin skitse. Ændr formen, og legemet følger med, med samme GlobalId. Markér et legeme for at ændre dets IFC-klasse, dets foruddefinerede type og dets type i egenskabspanelet. **Redigér skitse** åbner den visning, skitsen ligger på.

## Ring, Kasse og Vægstrimmel

Disse tre værktøjer giver en tegnet form en tykkelse i dens eget plan og derefter en højde væk fra planet:

- **Ring** (<kbd>RING</kbd>) tager en cirkel, en bue eller et lukket omrids og laver en ring eller et rør. Tykkelsen ligger **indvendigt**, **udvendigt** eller i **midte** af formen.
- **Kasse** (<kbd>BOX</kbd>) tager et rektangel, en lukket polylinje eller en cirkel og laver væggene rundt om den. En bund (**Bund**) og et låg (**Top**) er valgfrie, hver med sin egen tykkelse.
- **Vægstrimmel** (<kbd>TWALL</kbd>) tager en linje, en polylinje eller en bue og laver en strimmel væg. Tykkelsen ligger til **venstre**, til **højre** eller i **midte**, set når man går langs linjen i den retning, du tegnede den. Hjørnerne er geringssamlede.

Start værktøjet, og klik på formen. Kommandolinjen tilbyder valgmulighederne som bogstaver: <kbd>I</kbd>, <kbd>O</kbd> og <kbd>C</kbd> (eller <kbd>L</kbd>, <kbd>R</kbd> og <kbd>C</kbd>) for, hvor tykkelsen ligger, og for en kasse <kbd>B</kbd> og <kbd>T</kbd> for at slå bund og låg til eller fra. Skriv derefter tykkelsen og højden i meter, eller tryk på <kbd>Enter</kbd> for at beholde den viste værdi.

**I en plan** laver værktøjerne som standard native vægge, af den vægtype, der er valgt under **Arkitektur**. En lukket form bliver til en sløjfe af vægge samlet i hjørnerne, en polylinje til en kæde af vægge, og en bue eller en cirkel til buede vægge. Hvor tykkelsen ligger, afgør, hvor væggene står: hele vægtypens tykkelse kommer på den side, du valgte. En kasses bund og låg bliver til dæk. Skriv <kbd>S</kbd>, før du vælger formen, for i stedet at lave et Studio-legeme.

**I et snit, en facade eller et referenceplan** laver værktøjerne et Studio-legeme af IFC-klassen IfcWall; en kasses bund og låg er separate plader (IfcSlab). Højden går langs planets normal, mod dig; en negativ højde går væk fra dig, som med **Ekstrudér**. For at give sådan en væg en type skal du markere den og vælge eller lave en under **Type** i egenskabspanelet.

![En ring af native buede vægge rundt om en cirkel, en kasse med bund og låg og en vægstrimmel i stueetagens plan](shots/studio-thick-plan.webp)

Alt forbliver parametrisk. Markér legemet, og ændr dets **Tykkelse**, **Højde** og **Tykkelsen ligger** i egenskabspanelet. For en kasse sætter du flueben ved **Bund** eller **Top** og angiver deres tykkelser: Lapin tilføjer eller fjerner pladerne. En kasse med bund eller låg samles med dem i en samling (IfcElementAssembly). Træk i formens greb, eller redigér den i dens visning, og legemet bygges op igen.

![En kasses egenskaber: tykkelse, højde, bund og låg](shots/studio-box-properties.webp)

## Referenceplaner

Et referenceplan er et plan hvor som helst i modellen, med sin egen visning. Du tegner på det med Tegn, som i et snit, og Studio laver legemer på det. Lav et fra fanen **Studio**:

- **Plan fra visning** (<kbd>REFPLANE</kbd>) ligger parallelt med den plan, det snit, den facade eller det referenceplan, du har foran dig. Skriv, hvor langt det ligger mod dig (en negativ værdi går væk).
- **Plan på flade**: Klik i 3D-visningen på en flade af et vilkårligt element. Planet ligger på fladen og vender ud fra den.
- **Plan gennem 3 punkter**: Klik i 3D-visningen på tre punkter på modellen. Planet går gennem dem.

Det nye plan åbner med det samme. Det står under **Referenceplaner** i projektbrowseren. Dets tegning er et snit på planet, set mod den side, det vender mod.

I 3D-visningen tegnes hvert referenceplan som en gennemsigtig flade med dets navn, så stor som modellen er på det (eller som **Tegnet bredde (3D)** og **Tegnet højde (3D)** siger, mindst 2 × 2 m), fremhævet under markøren. Klik på den for at vælge planet, dobbeltklik for at åbne dets visning. Med planet valgt trækker du pilen i dets midte for at flytte det langs dets normal: forskydningen vises ved pilen, og det, der er tegnet på planet, følger med. **Vis → Referenceplaner** skjuler og viser fladerne; de kommer aldrig med i eksporter, renderinger eller billeder. I planer, snit og facader tegnes et plan, der skærer visningen, som en stiplet linje med dets navn hen over hele visningen: et klik vælger planet, et dobbeltklik åbner det. Et plan parallelt med visningen tegnes ikke dér (det skærer intet af den). Disse linjer er kun til skærmen: tegningsark udskriver dem aldrig.

![Et referenceplan på sydfacaden med en ring tegnet på det](shots/studio-plane.webp)

Markér planet i browseren for at flytte det. **Origo x**, **y** og **z** placerer det. **Vender mod** er den retning i planen, det vender mod, **Hældning**, hvor meget det læner sig bagover (ved 90° ligger det fladt og vender opad), og **Drejning i planet** drejer dets tegning om dets normal. **Flyt langs normalen…** og **Drej om lodret…** gør det samme i ét trin. Det, der er tegnet på planet, og hvert legeme lavet ud fra det, flytter med.

![Et referenceplans egenskaber](shots/studio-plane-properties.webp)

Snit og facader er også planer. Et legeme skitseret i et snit står på snitlinjen og følger linjen, når du flytter den. I et udfoldet snit står det på det stykke, det er tegnet på. En facade får sit eget plan, første gang du laver et legeme på den: foran modellen, med dens x, som den er tegnet nu. Ændr dens **Skitseplanets dybde** i facadens egenskaber for at flytte planet og dets legemer langs visningen.

![Huset med dets Studio-legemer i 3D](shots/studio-3d.webp)

I IFC er en ring, en kasses vægge og en vægstrimmel IfcWall-elementer, en kasses bund og låg IfcSlab-elementer, og en kasse med dem en IfcElementAssembly med IfcRelAggregates; deres typer skrives som IfcWallType og IfcSlabType. Hver 3D-eksport (IFC, Fragments, glTF, USD, COLLADA) skriver et Studio-legeme dér, hvor det står i modellen, uanset hvilket plan det blev skitseret på, med dets huller; en samling er en gruppeknude over sine dele. Åbnes Lapins egen IFC igen, kommer legemerne tilbage som referencer, på deres plads, med klasse, type og samling, men de kan ikke længere redigeres (deres skitser er ikke i filen). Hvert legemes historik gemmes i Lapins eget egenskabssæt, Lapin_Intent. Studio-legemer tager kun simple typer uden lag: det er den frie modellering, de er til; vægge med lag er Lapins native vægge.
## At ændre et legeme i 3D

Disse værktøjer arbejder på legemets flader og kanter, som det er bygget nu. Fladen under musen lyser op. Når et værktøj skal bruge en værdi, åbner en lille boks ved markøren. Skriv værdien og tryk på <kbd>Enter</kbd>, eller <kbd>Esc</kbd> for at droppe den.

- **Skub/Træk** (PU) flytter en flade ud med en afstand, eller ind, når afstanden er negativ. På en ekstrudering ændrer enden dybden, og siden flytter skitsens kant under den. På en væg eller et andet element tilføjer et træk et legeme i fladens materiale, og et skub skærer ind i elementet.
- **Afrund kant** (FE) afrunder kanten nærmest klikket, og **Affas kant** (CE) affaser den. Vælg *hele vejen rundt om fladen* for at tage alle fladens kanter på én gang.
- **Skal** udhuler de valgte legemer med vægge så tykke, som du skriver. Den kan forblive lukket eller være åben i enden, ved skitsen eller i begge.
- **Del** skærer et legeme i to ved den valgte flades plan, så langt ind i fladen som du skriver. Begge dele beholder skitsen. **Afskær** beholder kun én side.
- **Række** gentager legemet på linje, og **Polær række** gentager det om en lodret akse.
- **Rotér 3D** drejer legemet om en kant, du vælger. **Spejl 3D** spejler det i en flades plan. **Flyt 3D** flytter det med x, y og z.

![Skub/Træk: topfladen lyser op, og afstanden skrives ved markøren](shots/studio-push-pull.webp)

![Kanten afrundet: rundingen følger blokken, når dens dybde ændres](shots/studio-fillet.webp)

## Trinene

Et Studio-legemes egenskaber viser dets trin i rækkefølge. Hvert trin kan slås fra, flyttes tidligere eller senere, ændres eller fjernes. Ændr skitsen, og hvert trin udføres igen på det nye legeme. Et trin, der ikke længere finder sin flade eller kant, siger det under **Problemer**.

![En cylinder udhulet til et rør og gentaget tre gange: dens to trin i dens egenskaber](shots/studio-steps.webp)

En polylinjes egenskaber viser dens sider. Skriv en sides længde, og dens ende flytter sig langs den. Siderne efter følger med op til den næste side, der løber samme vej, så et rektangel forbliver et rektangel. Et legeme tegnet på den følger også med.

## Mesh fra andre programmer

**Filer → Importér mesh (OBJ, STL, glTF)…** henter et mesh ind som referencer, du kan se og fange til. En OBJ- eller STL-fil siger hverken sin enhed eller hvilken akse der vender op, så Lapin spørger. Vælg en reference og klik på **Gør til Studio-legeme** for at kunne redigere den. Så tager den skub/træk, afrundinger, boolske operationer og de andre trin. Det samme værktøj virker på vægge, dæk og elementer fra IFC-filer.

## Boolske operationer

**Fratræk**, **Forén** og **Skær** kombinerer vilkårlige elementer, det først valgte er målet. Værktøjerne kan stadig redigeres, og resultatet følger dem. **Vis operander** viser en forenings skjulte værktøjer, så du kan redigere dem. **Fjern operation** skiller elementerne ad igen.
