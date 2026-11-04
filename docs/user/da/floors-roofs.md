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

**Tagrender og nedløbsrør.** Under **Tagdækning og afvanding** i konfiguratoren får tagrenden sin størrelse, hvor langt den sidder ud fra tagfoden og under tagdækningen, sit fald mod afløbene (mm/m) og sine endebunde; kanttabellen slår hver tagfods tagrende til eller fra. Nedløbsrørene er først tagets egne, ét pr. 10 m; **Gør dem redigerbare** gør dem til elementer, og **Architecture → Downpipe** tilføjer ét med et klik på en tagrende. Et nedløbsrørs håndtag skubber det kun langs tagrenden; i egenskaberne sætter du diameter, udløbsbøjning og forløb, en kæde af stykker med længde og vinkel fra lodret (mod væggen), det sidste ned til terræn. I IFC er en tagrende et IfcPipeSegment GUTTER og et nedløbsrør et stift segment kaldet Downpipe.

Væggene på tagets niveau stopper under det og går selv op til dets gavle, også under et buet tag. En væg på et andet niveau følger taget efter **Arkitektur → Fastgør til tag** (<kbd>AR</kbd>): markér væggene, og klik derefter på taget. Samme værktøj tager ethvert andet legeme over væggene: et Studio-legeme, en plade, en bjælke, et element importeret fra IFC. Væggene går op til dets underside og skæres langs den, uanset formen. **Frigør fra tag** fortryder det.

**Ovenlysvindue** og **Kvist** tilføjer åbninger i et tag: klik på en tagflade. De hører til taget. Markér taget, og hver af dem viser sine greb i plan. Træk i grebet i midten for at flytte den på taget. Træk i et ovenlysvindues kant- eller hjørnegreb for at ændre dets størrelse på tagfladen (den modsatte kant bliver, hvor den er; længden kan du skrive, mens du trækker). Bredden går langs tagfoden og længden op ad tagfladen, målt på fladen. En kvist har et greb i hver side til sin bredde. En ændring, der ikke ville kunne være på taget, afvises. For at fjerne en skal du højreklikke på et af dens greb og vælge **Fjern ovenlysvindue** (eller **Fjern kvist**), trykke på **Delete**, mens markøren er over dens greb, eller **Ctrl/⌘-klikke** på grebet i midten. Tagets egenskaber lister dem også: skriv et vindues bredde og længde der, eller klik på **×** for at fjerne det.

![Et ovenlysvindues greb på det markerede tag](shots/roof-window-grip.webp)

### Trækonstruktion

**Konstruktion** angiver, hvordan taget bæres:

- **rafters**: spær på åse, med kip-ås.
- **collar**: spærpar med et vandret hanebånd ved **Hanebånd: andel af højden** (to tredjedele som standard), uden åse.
- **trusses**: præfabrikerede gitterspær langs hovedkippen med **Spæravstand**: overflanger på tagfladerne, en underflange på remmene, W-gitterstænger (et ensidigt spær på et pulttag). Et valmtags ender beholder deres spær.
- **none**: et dæk, intet træ.

**Spær og over-/underflanger** tager de gamle tværsnit i cm eller de nordiske i mm (45×95 til 45×245); sammen med dem **Gitterstænger i spærene** og **Trækvalitet** (C14 til C30, limtræ). Spærene ligger på remmen med et hak (**Spærenes vederlag på remmen**) og ender ved tagfoden lodret eller vinkelret (**Spærenderne ved tagfoden**). Stilene **Swedish truss roof** og **Nordic collar-tie roof** har deres konstruktion med.

Hvert element er en del for sig: listen **Roof timber** viser dem som en skæreliste (element, tværsnit, kvalitet, længde, stk., volumen), og i IFC er hvert et IfcMember af sin type, hvert gitterspær et IfcElementAssembly.

## Nedhængte lofter

**Overflader → Loft** (<kbd>CG</kbd>) tegner et nedhængt loft over et omrids, du klikker; **Loft i et rum** fylder det rum, du klikker i, efter dets omrids. Et loft hænger i sin **Højde** over etagen (2700 mm, medmindre du ændrer den i egenskaberne).

Dets type i **Overflader → Lofttyper** angiver **Systemet**: **Plader på synligt skinnesystem** (pladens bredde, længde og tykkelse, skinnernes synlige bredde, dybde og drejning, nedhæng op til konstruktionen med deres afstand) eller **Gips, sammenhængende**. Skinnerne centreres på loftet, så de afskårne plader ved kanterne bliver ens.

**Lyspanel** og **Luftindblæsning** sætter et i det felt, du klikker på; et klik på samme felt med det andet værktøj skifter det. I egenskabspanelet sætter **Fordel** lyspaneler eller luftindblæsninger i hvert så mange felt, og **Ryd** fjerner dem. Listerne tæller hele plader, lyspaneler og luftindblæsninger, og loftet går til IFC som IfcCovering af typen CEILING.

![Et lofts egenskaber: type, højde og armaturer](shots/ceiling-properties.webp)

![Systemlofter med lyspaneler og luftindblæsninger i 3D](shots/ceilings-3d.webp)

## Fodlister

**Overflader → Fodliste i rum** (<kbd>SK</kbd>) lægger en fodliste rundt i det rum, du klikker i, langs væggenes flader, afbrudt ved hver dør (enhver åbning, der går ned til gulvet); et klik mellem vægge uden rum følger dem på samme måde. Et rums fodliste følger rummet: flyt en væg eller en dør, og fodlisten går med. **Fodliste** tegner en langs en linje, du klikker på en vægs flade (Enter afslutter); listen kommer på siden væk fra væggen, og **Skift side** i dens egenskaber vender den.

Dens type, under **Overflader → Fodlistetyper**, sætter **Profilet** — **Glat (firkantet)**, **Affaset (MDF)**, **Profileret (træ)**, **Med hulkehl (metal)**, **Halvrund top** eller **Afrundet kant**, tegnet ved siden af i skala —, **Højden** (40 til 300 mm), **Tykkelsen** (5 til 40 mm), **Materialet** og dets farve. **Fra biblioteket** henter færdige typer ind i projektet: MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 og Bullnose 70. Mens et fodlisteværktøj er aktivt, viser egenskabspanelet den type, det lægger, til at vælge før klikket.

I sine egenskaber har en fodliste sin type, en **Fodforskydning** over gulvet og **Huller ved dørene** (slået fra løber den forbi dem). **Fodlisteskema** (eller **Fodlister** i projektbrowserens liste over nye skemaer) viser dem efter type med stykker og længde, uden dørenes huller. Listen er geret i hjørnerne; en plan tegner dens forkant, og en fodliste går til IFC som en IfcCovering af typen SKIRTINGBOARD, af en IfcCoveringType af samme type.

![Fodlistetyperne: profilet tegnet i skala, biblioteket](shots/skirting-types.webp)

![En fodlistes egenskaber: dens type, fodforskydningen og hullerne ved dørene](shots/skirting-properties.webp)
