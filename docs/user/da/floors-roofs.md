---
title: Dæk og tage
order: 40
summary: Tegn dæk og tage efter deres omrids, vælg et tags form og kanter, og skær åbninger.
---

## Dæk

Vælg **Arkitektur → Dæk** (<kbd>SB</kbd>), og klik derefter på hjørnerne i omridset. Luk omridset på det første punkt, eller tryk på <kbd>Enter</kbd>. Et dæk består også af lag, som indstilles under **Arkitektur → Dæktyper**.

Vil du ændre omridset, så markér dækket og træk i dets hjørner. **Dækåbning** (<kbd>SH</kbd>) skærer et hul i et dæk, til en trappe eller en skakt.

Et dæk tegnet uden for væggene, foran en dør, er en trappeafsats. Om trinene op til den, se [Trapper](stairs.md).

## Tage

Vælg **Arkitektur → Tag** (<kbd>RF</kbd>), og klik på omridset af væggene under det. Lapin bygger taget over det omrids, øverst på dets niveau.

![Et hus med sadeltag i 3D-visningen](shots/house-3d.webp)

Markér taget for at ændre det i egenskabspanelet:

- **Form.** Valmtag, sadeltag, pulttag, mansardtag, buetag eller fladt tag. Et tøndetag er en buet hvælving mellem to gavle; **Tøndetag: pilhøjde ved toppen** angiver, hvor højt det rejser sig ved toppen, højst halvdelen af spændet.
- **Hældning og udhæng** for hele taget.
- **Kiprichtning (sadeltag, pulttag)**: auto, x eller y. På et pulttag er kippen den høje side: på x falder det mod −y, på y mod −x; **Pulttag: vend hældningen** får det til at falde den anden vej.
- **Hver kant** med sin egen art (tagfod eller gavl), hældning og udhæng. Et mansardtag med gavlkanter er et gambreltag.
- **Stil.** Tagstilene giver et helt udtryk med ét valg: tagdækningen, tagfødderne, tagrenderne og nedløbsrørene.

![Et tags egenskaber](shots/roof-properties.webp)

**Tagkonfigurator** (knappen ved siden af stilen, eller **Architecture → Roof configurator** med et tag valgt) samler det hele: stilene til venstre, så formen og kanterne, tagdækning og afvanding, konstruktionen og åbningerne, over en plan af taget (et klik på en kant gør den auto, tagfod eller gavl) og, for konstruktionen, et snit på tværs af kippen med gitterspæret eller spærparret på midten. Start fra en stil og ret ethvert felt i hånden; **Apply** gør det til ét fortryd-trin. **Gem som stil…** gemmer taget, som det er konfigureret, som en stil i projektet, vist under **Project** over katalogets (og i egenskabspanelets liste) til andre og nye tage; **Opdatér stilen** gemmer ændringerne i den igen, og alle tage med stilen følger med. En projektstil kan omdøbes eller slettes; dens tage forbliver, som de er.

![Tagkonfiguratoren](shots/roof-configurator.webp)

Væggene på tagets niveau stopper under det og går selv op til dets gavle, også under et buet tag. En væg på et andet niveau følger taget efter **Arkitektur → Fastgør til tag** (<kbd>AR</kbd>): markér væggene, og klik derefter på taget. Samme værktøj tager ethvert andet legeme over væggene: et Studio-legeme, en plade, en bjælke, et element importeret fra IFC. Væggene går op til dets underside og skæres langs den, uanset formen. **Frigør fra tag** fortryder det.

**Ovenlysvindue** og **Kvist** tilføjer åbninger i et tag.

### Trækonstruktion

**Konstruktion** angiver, hvordan taget bæres:

- **rafters**: spær på åse, med kip-ås.
- **collar**: spærpar med et vandret hanebånd ved **Hanebånd: andel af højden** (to tredjedele som standard), uden åse.
- **trusses**: præfabrikerede gitterspær langs hovedkippen med **Spæravstand**: overflanger på tagfladerne, en underflange på remmene, W-gitterstænger (et ensidigt spær på et pulttag). Et valmtags ender beholder deres spær.
- **none**: et dæk, intet træ.

**Spær og over-/underflanger** tager de gamle tværsnit i cm eller de nordiske i mm (45×95 til 45×245); sammen med dem **Gitterstænger i spærene** og **Trækvalitet** (C14 til C30, limtræ). Spærene ligger på remmen med et hak (**Spærenes vederlag på remmen**) og ender ved tagfoden lodret eller vinkelret (**Spærenderne ved tagfoden**). Stilene **Swedish truss roof** og **Nordic collar-tie roof** har deres konstruktion med.

Hvert element er en del for sig: listen **Roof timber** viser dem som en skæreliste (element, tværsnit, kvalitet, længde, stk., volumen), og i IFC er hvert et IfcMember af sin type, hvert gitterspær et IfcElementAssembly.

## Nedhængte lofter

**Overflader → Loft** (<kbd>CL</kbd>) tegner et nedhængt loft over et omrids, du klikker; **Loft i et rum** fylder det rum, du klikker i, efter dets omrids. Et loft hænger i sin **Højde** over etagen (2700 mm, medmindre du ændrer den i egenskaberne).

Dets type i **Overflader → Lofttyper** angiver **Systemet**: **Plader på synligt skinnesystem** (pladens bredde, længde og tykkelse, skinnernes synlige bredde, dybde og drejning, nedhæng op til konstruktionen med deres afstand) eller **Gips, sammenhængende**. Skinnerne centreres på loftet, så de afskårne plader ved kanterne bliver ens.

**Lyspanel** og **Luftindblæsning** sætter et i det felt, du klikker på; et klik på samme felt med det andet værktøj skifter det. I egenskabspanelet sætter **Fordel** lyspaneler eller luftindblæsninger i hvert så mange felt, og **Ryd** fjerner dem. Listerne tæller hele plader, lyspaneler og luftindblæsninger, og loftet går til IFC som IfcCovering af typen CEILING.

![Et lofts egenskaber: type, højde og armaturer](shots/ceiling-properties.webp)

![Systemlofter med lyspaneler og luftindblæsninger i 3D](shots/ceilings-3d.webp)
