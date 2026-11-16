---
title: Dæk og tage
order: 40
summary: Tegn dæk og tage efter deres omrids, vælg et tags form og kanter, og skær åbninger.
---

## Dæk

Vælg **Arkitektur → Dæk** (<kbd>SB</kbd>), og klik derefter på hjørnerne i omridset. Luk omridset på det første punkt, eller tryk på <kbd>Enter</kbd>. Et dæk består også af lag, som indstilles under **Arkitektur → Dæktyper**. Et lag i en dæktype kan have tykkelsen 0 (en fugtspærre, maling): det bygger intet, tegnes som en linje i snit og måles efter dækkets areal.

I **Dæktyper** har hvert lag en **Prioritet** (0 til 100) og en **Kategori**, som lagene i en vægtype. Et væglag, der går ned i dækket (**Ind i dæk** i **Vægtyper**), går gennem dækkets lag med lavere prioritet og stopper på det første med samme eller højere. Tom er prioriteten materialets i **Administrer → Snitrækkefølge**, ellers 50 for kernen og 0 for de øvrige lag. Et lag markeret **Overflade** forsvinder, når en visning udelader dækkenes overflader.

Vil du ændre omridset, så markér dækket og træk i dets hjørner. Hold <kbd>Ctrl/⌘</kbd> nede og klik på den lille rombe midt på en kant for at tilføje et hjørne, eller højreklik på det markerede dæk nær en kant og vælg **Tilføj hjørne her**: det nye hjørne kommer, hvor du klikkede. Du fjerner et hjørne med <kbd>Ctrl/⌘</kbd>-klik på det, eller ved at højreklikke på det og vælge **Fjern hjørne** (et omrids beholder mindst tre hjørner). Hver ændring er ét trin at fortryde, og det virker også på dækåbninger, tage, rum og altaner. **Dækåbning** (<kbd>SH</kbd>) skærer et hul i et dæk, til en trappe eller en skakt.

Vil du gøre hele omridset større eller mindre på én gang, alle kanter lige langt (Archicads *Offset all edges*), så hold <kbd>Shift</kbd> nede og træk i romben midt på en vilkårlig kant: omridset følger markøren live, i en plan eller i 3D, og afstanden kan skrives, mens du trækker (<kbd>Enter</kbd> sætter den). Eller højreklik på det markerede dæk, vælg **Forskyd omrids…**, og skriv afstanden: positiv går udad, negativ indad. Rette kanter forbliver parallelle og mødes igen i deres hjørner; en buet kant forbliver en bue om sit eget centrum, og hvor en bue møder en anden kant i et hjørne, runder en forøgelse det hjørne. En forskydning, der ville få en kant til at forsvinde eller omridset til at skære sig selv, afvises, med årsagen ved markøren. Det virker på dæk, dækåbninger, lofter, tage (hver kant beholder sin regel for udhæng eller gavl), rum og altaner, og på flere markerede på én gang; hver ændring er ét trin at fortryde.

![Et dæks omrids forskudt ved at Shift-trække i midten af en kant](shots/floor-offset-outline.webp)

### Buede kanter, afrundinger og runde dæk

Værktøjet **Dæk** har en bjælke i kommandolinjen, der tegner omridset som **Polygon** (klik på hjørnerne), **Rektangel** (to modstående hjørner), **Cirkel** (centrum, derefter et punkt på den) eller **Inden for vægge** (et klik inden for de vægge, der lukker om den; dækket går ud til deres flader). I en polygon bliver **Linjer (Mellemrum)** til **Buer (Mellemrum)**: de næste kanter er buer, der hver fortsætter tangentielt fra kanten før, indtil du trykker <kbd>Mellemrum</kbd> igen. **Dækåbning** og **Loft** har samme bjælke, uden **Inden for vægge**.

![Værktøjet Dæk tegner en cirkel ved siden af et dæk](shots/floor-shapes.webp)

Den samme bjælke gælder for alle omridsværktøjer (dæk, dækåbning, loft, hul, rum og altan efter omrids, platform, matrikel), og tre af dens valg tager hele omridset med ét klik: **Inside walls** (væggene, der lukker sig om punktet), **From drawn shape** (den mindste lukkede tegnede form om punktet: en lukket polylinje, et rektangel eller en cirkel, der forbliver rund) og **From room** (rummet om punktet, med dets buede kanter).

![Omridsværktøjernes bjælke med From drawn shape valgt](shots/outline-sources.webp)

For dæk, dækåbning, loft og rum efter omrids giver bjælken også **Ellipse**: centrum, enden af en halvakse, så den anden. Omridsets kanter følger den sande ellipse (fire kvarte kanter), målt nøjagtigt; et hjørne flyttet bort fra ellipsen gør dets to kanter lige, og et hjørne ved en elliptisk kant afrundes ikke. IFC bærer den som IfcEllipse. **From drawn shape** tager en tegnet ellipse på samme måde. Et dæks eller lofts eget hul (**Hole in the outline**) tager også en ellipse. Et omrids med en elliptisk kant forskydes ikke som helhed (Shift-træk, **Offset outline…**): kurven i en afstand fra en ellipse er ingen ellipse.

![Et elliptisk dæk tegnes ved siden af en tegnet ellipse](shots/ellipse-floor.webp)

Enhver kant af et dæk, en dækåbning eller et loft kan være buet. Højreklik på det markerede dæk nær en kant og vælg **Gør til bue**: kanten buer ud med en ottendedel af sin længde, og et rundt greb vises midt på buen. Træk i det for at bøje buen gennem det ønskede punkt (romben, nu midt på korden, flytter stadig kanten). **Gør lige** gør den til en linje igen. Et hjørne mellem to lige kanter kan afrundes eller affases: højreklik på det og vælg **Afrund hjørne…** (skriv radius) eller **Affas hjørne…** (skriv hvor langt tilbage langs hver kant). Hjørnet bliver til to, hvor buen eller affasningen møder kanterne; en radius, der er for stor til kanterne, afvises. Hver er ét fortryd-trin.

![Et dæk med en buet kant og et afrundet hjørne](shots/floor-curved-edge.webp)

Dækket bygges langs sine buer, ingen korde mere end 1 mm fra dem, i 3D, planer og snit; dets areal og omkreds måles på de sande buer. Et lag, der går forbi en buet kant (se [Lagudstrækninger](walls.md#layer-extents)), følger buen. En åbning kan også have buede kanter, og et buet dæk kan have åbninger. I IFC bærer dækket sit omrids med de sande buer som fodaftryk (en IfcIndexedPolyCurve med buesegmenter) ved siden af sit legeme. Tage beholder lige kanter, fordi et tags kantregler (tagfod, gavl) sættes pr. lige kant.

Et dæk eller et loft kan have **egne huller**: øer skåret ud af omridset, en del af elementet, ikke selvstændige åbninger. Vælg **Hole in the outline** i værktøjet **Opening** (under *In a floor*), og tegn det inden i dækket (eller det valgte dæk eller loft) med samme bjælke: en polygon, hvis kanter kan være buer, et rektangel eller en cirkel. Valgt viser dækket hvert huls hjørner og kanter som greb med samme højreklik (**Make arc**, **Fillet corner…**, **Chamfer corner…**); krydset midt i et hul flytter det, og dets højreklik giver **Remove hole**. Dækket bygges rundt om hullerne, og arealet udelader dem, målt på de sande buer; et hul skal ligge inden for omridset og adskilt fra de andre, og et omrids ændret, så det ikke længere rummer et hul, afvises. I **Edit sketch** er hullerne også løkker i skitsen: tegn endnu en lukket løkke indeni, og **Finish sketch** gør den til et hul (den største løkke er omridset). I IFC er hullerne flere kurver i dækkets fodaftryk og læses tilbage som huller. En platform (**Site → Hole in platform**) får huller på samme måde: terrænet i hullet beholder sine koter.

![Et dæk med et rektangulært og et rundt hul i sit eget omrids, valgt](shots/floor-holes.webp)

#### Redigér skitse

For mere end et hjørne eller en bue: højreklik på det markerede dæk (eller dækåbning eller loft) og vælg **Redigér skitse** (også **Studio → Rediger skitse**). Omridset åbner som en skitse på sin egen plan, én linje eller bue pr. kant, hjørnerne holdt sammen, og selve dækket skjult der; en bjælke over planen siger, hvis skitse det er. Tegn og ret med fanen **Tegning** (Linje, Bue, Afrunding, Trim, Forlæng, Forskyd…), og bind og målsæt med **Studio → Geometriske bånd** (se [Skitsebindinger](sketch-constraints.md)): ændr et mål, og omridset følger med. **Afslut skitse** skriver den tilbage til dækket i ét fortryd-trin, og dækket beholder sin identitet (sin GlobalId); **Annuller** lader dækket være som før. Skitsen skal være ét lukket omrids (kanterne ende mod ende, eller en cirkel alene); ellers siger Afslut, hvad der er åbent.

![Et dæks omrids redigeret som skitse: en afrunding og et mål](shots/floor-sketch-edit.webp)

Skitsen med sine bindinger og mål bliver på dækket (den går til IFC med dækkets designhensigt i Lapin_Intent), så **Redigér skitse** åbner den igen, som du forlod den. Er omridset siden ændret med grebene, starter skitsen forfra fra omridset, uden sine bindinger. Et dæk genkendt fra en IFC-fil får kun en skitse, når du redigerer det; et dæk holdt som reference redigeres ikke sådan.

![Dækket efter Afslut skitse](shots/floor-sketch-done.webp)

Et dæk tegnet uden for væggene, foran en dør, er en trappeafsats. Om trinene op til den, se [Trapper](stairs.md).

Et lag i et dæk kan også gå forbi dets omrids eller stoppe før det, hele vejen rundt eller ved én kant, skrevet i dets egenskaber eller trukket i plan eller snit med **Rediger lag**, og en dæktype kan give sine dæk deres forlængelser: se [Lagforlængelser](walls.md#lagforl-ngelser). Tage har endnu ingen lagopbygning (deres tagdækning, plade og konstruktion er egne dele), så de har ingen lagforlængelser.

**Redigér → Opdel element** på et dæk eller et loft beder om to punkter på en delelinje: den fortsætter forbi dem og skal skære konturen præcis to gange. Dækket bliver til to dæk; den del, du klikkede på, beholder id'et (GlobalId i IFC), buer på kanterne forbliver buer (en skåret bue forbliver en bue), og en åbning i dækket følger den del, den ligger i. En åbning, som linjen skærer, afvises. **Redigér → Trim/forlæng elementer** tager en ret grænse (en væg, en bjælke eller en tegnet linje) og skærer dækket eller loftet dér og beholder den side, du klikker på.

![Et dæk med en buet kant delt langs en linje: delen til højre er valgt](shots/floor-split.webp)

## Tage

Vælg **Arkitektur → Tag** (<kbd>RF</kbd>), og klik på omridset af væggene under det. Lapin bygger taget over det omrids, øverst på dets niveau.

![Et hus med sadeltag i 3D-visningen](shots/house-3d.webp)

Markér taget for at ændre det i egenskabspanelet:

- **Form.** Valmtag, sadeltag, pulttag, mansardtag, buetag eller fladt tag. Et tøndetag er en buet hvælving mellem to gavle; **Tøndetag: pilhøjde ved toppen** angiver, hvor højt det rejser sig ved toppen, højst halvdelen af spændet.
- **Hældning og udhæng** for hele taget.
- **Kiprichtning (sadeltag, pulttag)**: auto, x eller y. På et pulttag er kippen den høje side: på x falder det mod −y, på y mod −x; **Pulttag: vend hældningen** får det til at falde den anden vej.
- **Hver kant** med sin egen art (tagfod eller gavl), hældning og udhæng. Et mansardtag med gavlkanter er et gambreltag.
- **Stil.** Tagstilene giver et helt udtryk med ét valg: tagdækningen, tagfødderne, tagrenderne og nedløbsrørene.
- **Tagdækningens materiale.** Tagdækningen er af et af projektets materialer, som standard tagdækningens eget (Clay tile, Metal sheet, Slate…), oprettet i tagdækningens farve første gang et tag har brug for det. Vælg et andet i egenskaberne eller under konfiguratorens **Tagdækning og afvanding**; skifter du tagdækning, skifter det med, medmindre du har valgt et. Facader og snit skraverer tagdækningen efter materialet (tegl, spån og skifer i vandrette skifter, metalplader i lodrette false, medmindre du giver det sin egen **Elevation**-skravering), et snit tegner den skårne tagdækning med materialets snitskravering, og IFC, mængdeudtrækket og overslaget fører tagdækningen under dens materiale.
- **Tagdækningens tykkelse.** Tom er tagdækningen så tyk som katalogets (en membran 2 cm, metalplader 3 cm, tegl 5 cm…); skriv en for at give den sin egen. Et tag importeret fra et andet program beholder filens.
- **Dæk.** Et fladt tag, en tøndehvælving og et tag med **Structure** none har et dæk under tagdækningen, en del for sig: **Dækkets materiale** (som standard Reinforced concrete, oprettet første gang et tag har brug for det) og **Dækkets tykkelse** (som standard 16 cm), i egenskaberne eller under **Konstruktion** i konfiguratoren. Tagdækningen ligger på det, så taget forbliver lige så tykt som de to tilsammen. Et snit tegner to bånd, hvert skraveret efter sit materiale; tagvinduer går gennem begge; en væg fastgjort til et fladt tag går op til dækkets underside; IFC skriver dækket og tagdækningen som to tagplader af deres materialer, og mængdeudtrækket og overslaget måler dækket (areal og volumen) og tagdækningen (areal) hver under sit materiale. Egenskaberne viser kun disse to felter på et tag uden tagkonstruktion, og spærenes felter (tværsnit, afstand, styrkeklasse, åse, spærender) kun på et med; et felt, der kun gælder én form (mansardens, hvælvingens pilhøjde, ensidig taghældnings vending), vises kun på den form.

![Et tags egenskaber](shots/roof-properties.webp)

![Et fladt tag i snit: betondækket og tagdækningen over det, hver skraveret efter sit materiale](shots/roof-deck-section.webp)

**Tagkonfigurator** (knappen ved siden af stilen, eller **Architecture → Roof configurator** med et tag valgt) samler det hele: stilene til venstre, så formen og kanterne, tagdækning og afvanding, konstruktionen og åbningerne, over en plan af taget (et klik på en kant gør den auto, tagfod eller gavl) og, for konstruktionen, et snit på tværs af kippen med gitterspæret eller spærparret på midten. Start fra en stil og ret ethvert felt i hånden; **Apply** gør det til ét fortryd-trin. **Gem som stil…** gemmer taget, som det er konfigureret, som en stil i projektet, vist under **Project** over katalogets (og i egenskabspanelets liste) til andre og nye tage; **Opdatér stilen** gemmer ændringerne i den igen, og alle tage med stilen følger med. En projektstil kan omdøbes eller slettes; dens tage forbliver, som de er.

![Tagkonfiguratoren](shots/roof-configurator.webp)

**Tagrender og nedløbsrør.** Under **Tagdækning og afvanding** i konfiguratoren får tagrenden sin størrelse, hvor langt den sidder ud fra tagfoden og under tagdækningen, sit fald mod afløbene (mm/m) og sine endebunde; kanttabellen slår hver tagfods tagrende til eller fra. Nedløbsrørene er først tagets egne, ét pr. 10 m; **Gør dem redigerbare** gør dem til elementer, og **Architecture → Downpipe** tilføjer ét med et klik på en tagrende. Et nedløbsrørs håndtag skubber det kun langs tagrenden; i egenskaberne sætter du diameter, udløbsbøjning og forløb, en kæde af stykker med længde og vinkel fra lodret (mod væggen), det sidste ned til terræn. I IFC er en tagrende et IfcPipeSegment GUTTER og et nedløbsrør et stift segment kaldet Downpipe.

Væggene på tagets niveau stopper under det og går selv op til dets gavle, også under et buet tag. En væg på et andet niveau følger taget efter **Arkitektur → Fastgør til tag** (<kbd>AR</kbd>): markér væggene, og klik derefter på taget. Samme værktøj tager ethvert andet legeme over væggene: et Studio-legeme, en plade, en bjælke, et element importeret fra IFC. Væggene går op til dets underside og skæres langs den, uanset formen. **Frigør fra tag** fortryder det.

**Ovenlysvindue** og **Kvist** tilføjer åbninger i et tag: klik på en tagflade. De hører til taget. Markér taget, og hver af dem viser sine greb i plan. Træk i grebet i midten for at flytte den på taget. Træk i et ovenlysvindues kant- eller hjørnegreb for at ændre dets størrelse på tagfladen (den modsatte kant bliver, hvor den er; længden kan du skrive, mens du trækker). Bredden går langs tagfoden og længden op ad tagfladen, målt på fladen. En kvist har et greb i hver side til sin bredde. En ændring, der ikke ville kunne være på taget, afvises. For at fjerne en skal du højreklikke på et af dens greb og vælge **Fjern ovenlysvindue** (eller **Fjern kvist**), trykke på **Delete**, mens markøren er over dens greb, eller **Ctrl/⌘-klikke** på grebet i midten. Tagets egenskaber lister dem også: skriv et vindues bredde og længde der, eller klik på **×** for at fjerne det. En kvists tag er dækket af tagets tagdækningsmateriale og måles med det: tagets mængder viser **kvistenes tagdækning** for sig (arealet af dens flader, antallet af kviste), så mængdeopgørelsen efter materiale, overslaget og en planlægningsopgave på tagdækningen tæller tagets flader (uden kvistenes udskæringer) og kvistenes tage, hver én gang.

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

**Interiør → Loft** (<kbd>CG</kbd>) tegner et nedhængt loft over et omrids, du klikker; **Loft i et rum** fylder det rum, du klikker i, efter dets omrids. Et loft hænger i sin **Højde** over etagen (2700 mm, medmindre du ændrer den i egenskaberne).

Dets type i **Interiør → Lofttyper** angiver **Systemet**: **Plader på synligt skinnesystem** (pladens bredde, længde og tykkelse, skinnernes synlige bredde, dybde og drejning, nedhæng op til konstruktionen med deres afstand) eller **Gips, sammenhængende**. Skinnerne centreres på loftet, så de afskårne plader ved kanterne bliver ens.

**Lyspanel** og **Luftindblæsning** sætter et i det felt, du klikker på; et klik på samme felt med det andet værktøj skifter det. I egenskabspanelet sætter **Fordel** lyspaneler eller luftindblæsninger i hvert så mange felt, og **Ryd** fjerner dem. Listerne tæller hele plader, lyspaneler og luftindblæsninger, og loftet går til IFC som IfcCovering af typen CEILING.

![Et lofts egenskaber: type, højde og armaturer](shots/ceiling-properties.webp)

![Systemlofter med lyspaneler og luftindblæsninger i 3D](shots/ceilings-3d.webp)

## Fodlister

**Interiør → Fodliste i rum** (<kbd>SK</kbd>) lægger en fodliste rundt i det rum, du klikker i, langs væggenes flader, afbrudt ved hver dør (enhver åbning, der går ned til gulvet); et klik mellem vægge uden rum følger dem på samme måde. Et rums fodliste følger rummet: flyt en væg eller en dør, og fodlisten går med. **Fodliste** tegner en langs en linje, du klikker på en vægs flade (Enter afslutter); listen kommer på siden væk fra væggen, og **Skift side** i dens egenskaber vender den.

Dens type, under **Interiør → Fodlistetyper**, sætter **Profilet** — **Glat (firkantet)**, **Affaset (MDF)**, **Profileret (træ)**, **Med hulkehl (metal)**, **Halvrund top** eller **Afrundet kant**, tegnet ved siden af i skala —, **Højden** (40 til 300 mm), **Tykkelsen** (5 til 40 mm), **Materialet** og dets farve. **Fra biblioteket** henter færdige typer ind i projektet: MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 og Bullnose 70. Mens et fodlisteværktøj er aktivt, viser egenskabspanelet den type, det lægger, til at vælge før klikket.

I sine egenskaber har en fodliste sin type, en **Fodforskydning** over gulvet og **Huller ved dørene** (slået fra løber den forbi dem). **Fodlisteskema** (eller **Fodlister** i projektbrowserens liste over nye skemaer) viser dem efter type med stykker og længde, uden dørenes huller. Listen er geret i hjørnerne; en plan tegner dens forkant, og en fodliste går til IFC som en IfcCovering af typen SKIRTINGBOARD, af en IfcCoveringType af samme type.

![Fodlistetyperne: profilet tegnet i skala, biblioteket](shots/skirting-types.webp)

![En fodlistes egenskaber: dens type, fodforskydningen og hullerne ved dørene](shots/skirting-properties.webp)
