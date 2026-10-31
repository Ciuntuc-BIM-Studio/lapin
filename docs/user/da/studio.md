---
title: Studio-legemer og arbejdsplaner
order: 85
summary: Gør tegnede former til legemer, giv en form en tykkelse med Ring, Kasse og Vægstrimmel, og tegn på referenceplaner hvor som helst i modellen.
---

## Legemer ud fra en skitse

Fanen **Studio** gør de former, du tegner, til legemer. Tegn formen med **Tegn** i en plan, et snit, en facade eller et referenceplan, og vælg den derefter med et Studio-værktøj:

- **Ekstrudér** (<kbd>EXT</kbd>) skubber en lukket form ud i en dybde. Lukkede former inden i den bliver til huller.
- **Sweep** (<kbd>SW</kbd>) fører en lukket profil langs en sti, **Drej** (<kbd>REV</kbd>) drejer den om en akse, og **Overgangsform** (<kbd>LO</kbd>) forbinder profiler tegnet i forskellige visninger.

![Fanen Studio](shots/ribbon-studio.webp)

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

![Et referenceplan på sydfacaden med en ring tegnet på det](shots/studio-plane.webp)

Markér planet i browseren for at flytte det. **Origo x**, **y** og **z** placerer det. **Vender mod** er den retning i planen, det vender mod, **Hældning**, hvor meget det læner sig bagover (ved 90° ligger det fladt og vender opad), og **Drejning i planet** drejer dets tegning om dets normal. **Flyt langs normalen…** og **Drej om lodret…** gør det samme i ét trin. Det, der er tegnet på planet, og hvert legeme lavet ud fra det, flytter med.

![Et referenceplans egenskaber](shots/studio-plane-properties.webp)

Snit og facader er også planer. Et legeme skitseret i et snit står på snitlinjen og følger linjen, når du flytter den. I et udfoldet snit står det på det stykke, det er tegnet på. En facade får sit eget plan, første gang du laver et legeme på den: foran modellen, med dens x, som den er tegnet nu. Ændr dens **Skitseplanets dybde** i facadens egenskaber for at flytte planet og dets legemer langs visningen.

![Huset med dets Studio-legemer i 3D](shots/studio-3d.webp)

I IFC er en ring, en kasses vægge og en vægstrimmel IfcWall-elementer, en kasses bund og låg IfcSlab-elementer, og en kasse med dem en IfcElementAssembly med IfcRelAggregates. Hvert legemes historik gemmes i Lapins eget egenskabssæt, Lapin_Intent.
