---
title: Grunden og dens data
order: 52
summary: Hent opmålingen, matriklen, naboerne og et ortofoto ind i rigtige koordinater, fra DXF, CSV, GeoJSON, CityJSON og et georefereret billede.
---

Fanen **Grund** rummer det terræn, bygningen står på: terrænet, matriklen, nabobygningerne og et luftfoto, hver der hvor de virkelig er. Gruppen **Importér** henter dem fra de filer, landinspektører og matrikelmyndigheden udleverer.

![Fanen Grund](shots/ribbon-site.webp)

## Rigtige koordinater

En opmåling kommer i kortkoordinater: øst- og nordkoordinater, hundredvis af kilometer fra nul. Lapin holder modellen nær sit origo og knytter den til kortet gennem **Opmålingspunkt**. Den første import i et projekt gør dette for dig: opmålingspunktet lægges i planens origo, på filens første koordinater afrundet til hele meter, og alle senere importer lander samme sted.

**Administrer → Placering** viser det **Koordinatsystem**, øst- og nordkoordinaterne er i, og basis- og opmålingspunkterne. Lapin kender Stereo 70 (EPSG:3844, Rumæniens nationale net), UTM-zone 34 og 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) og længde- og breddegrad (WGS 84, EPSG:4326). En fil i et andet af disse systemer omregnes, når den kommer ind: en GeoJSON i længde- og breddegrad lander på Stereo 70-planen, der hvor den hører hjemme. IFC-eksporten skriver systemet og opmålingspunktet, så andre programmer også placerer modellen på kortet.

![Administrer → Placering: koordinatsystemet og opmålingspunktet](shots/site-location.webp)

## Import

- **Terræn fra punkter** læser en CSV- eller tekstfil fra en opmåling: x y z, eller nummer nordkoordinat østkoordinat kote, ét punkt pr. linje.
- **Punkter fra DXF** læser en opmålingstegning: POINT-entiteter med deres højder, blokke indsat i en højde eller som bærer den som en attribut, højder skrevet ved siden af punkter ("245.37" ved siden af sit kryds) og højdekurver i deres kote. Når tegningen har punkter på flere lag, viser Lapin dem med hvor mange punkter hvert lag rummer, og du vælger, hvilke der skal tages med. Punkterne danner et terræn.
- **GeoJSON** henter matrikler, linjer og punkter. En fil med én polygon giver **matrikel**; en med flere giver områder. Matriklen afgrænser også terrænets matrikel, med dens areal. Filer fra matrikelmyndigheden i Stereo 70 siger det i filen, og Lapin læser det; en fil, der intet siger, er i længde- og breddegrad, som GeoJSON-standarden kræver.
- **CityJSON** henter nabobygningerne med deres flader, på deres plads og i deres højde. De er skrivebeskyttede referencer; de vil kaste skygger i solstudiet.
- **Ortofoto** lægger et luftfoto under grunden. Vælg billedet (JPEG eller PNG, op til 8 MB) sammen med dets world-fil (`.jgw`, `.pgw` eller `.wld`): world-filen siger, hvor billedet ligger. Fotoet tegnes under planen og lægges på terrænet i 3D.

![Situationsplanen: ortofotoet, opmålingsterrænets højdekurver, matriklen og naboens fodaftryk](shots/site-plan.webp)

![Samme grund i 3D: fotoet på terrænet, naboen i sin højde](shots/site-3d.webp)

Alt, der importeres, gemmes i projektfilen. Et objekt får sit navn fra filen (en matrikels matrikelnummer, når filen har et) og beholder filens øvrige egenskaber; markér det for at se dets areal eller længde.

## Veje, kurver og CAD-linjer

En **vej** eller en **sti** følger sin akse. I **Korridor…** (CR) giver fanen **Kurver** hvert knækpunkt på aksen en radius med klotoide ind og ud, sådan som en vejprojekterende lægger en kurve; et knækpunkt sat til 0 forbliver et hjørne, og kurver, der ikke kan være mellem deres naboer, afvises med begrundelsen. Stationeringen, længdeprofilet og tværprofilerne følger den krumme akse, og IFC 4.3 skriver den som en linjeføring.

CAD-linjer bliver til terrænelementer: **Vej fra CAD-linjer** (LR) og **Brudlinje fra CAD-linjer** (BL) tager en tegnet linje eller polylinje eller en fra en DXF, følger alle linjer på dens lag, der mødes ende mod ende, og laver en vej langs den eller en brudlinje i terrænet. Når linjerne har koter (en DXF's 3D-linjer), giver de vejens længdeprofil eller brudlinjens koter. Sådanne linjer beholder deres koter og kan vises i 3D (kategorien *linjer*).

## Situationsplaner og oversigtskort

Projektbrowseren har en **Grund** under niveauerne: grundens egne tegninger, som ikke hører til nogen etage. Et nyt projekt starter med to:

- **Situationsplan** (på rumænsk *plan de situație*), i 1:500: hele modellen set ovenfra, alle etager på én gang, tage og det hele, med terrænet, stierne, beplantningen, matriklen og naboerne. Det, der ligger helt under ±0.00 (fundamenterne), udelades, intet skæres, og intet skjult tegnes. Matriklens hjørner nummereres;
- **Oversigtskort** (*plan de încadrare în zonă*), i 1:2000: samme tegning af grunden i sit område, over ortofotoet, uden hjørnernes numre; projektets bygninger fyldes med rødt, og matriklen omrids med rødt, så grunden skiller sig ud fra sine naboer.

**+** ved siden af Grund laver endnu en af hver; kopiknapperne duplikerer en. Begge har en **Nordpil**, der peger mod geografisk nord (Administrer → Placering: vinklen til geografisk nord), en **Målestokslinje** og, når projektet er knyttet til kortet, **Koordinatnet**: kryds hvor runde øst- og nordkoordinater mødes, for hver tiendedel af målestokken (50 m i 1:500, 200 m i 1:2000), med deres værdier. Markér visningen i browseren, og indstil dem i dens egenskaber under **Visningsindstillinger**: hvad den er (situationsplan eller oversigtskort), nettets interval (0: intet), pilen, linjen og **Fremhæv projektet** (den røde udfyldning og det røde omrids, slået til som standard i et oversigtskort, også i en situationsplan, når du vil).

**Kortet under dem.** Ortofotoet tegnes under hver situationsplan og hvert oversigtskort, uanset hvilken etage det blev importeret på, og udskrives med dem på ark og i PDF'en. Til matrikelplanen eller landinspektørens tegning: åbn situationsplanen og brug **Importér DXF…** med **Placeret: efter kortets koordinater**. Lapin foreslår det selv, når filens koordinater ligner et korts, for eksempel Stereo 70. Tegningen lander så der, hvor dens øst- og nordkoordinater siger, gennem projektets opmålingspunkt og dets vinkel til geografisk nord. Hvis projektet endnu ikke er knyttet til kortet, knytter tegningen det. Sæt flueben ved **Som link** for at holde den skrivebeskyttet og genindlæse den, når landinspektøren sender en ny. Importér den én gang i hver tegning, der skal bruge den.

![Et oversigtskort: huset fyldt med rødt, matriklen omridset med rødt, blandt sine naboer](shots/site-location-plan.webp)

![En situationsplan: huset ovenfra på sin matrikel, hjørnerne nummereret, kortets net, nordpilen og målestokslinjen](shots/site-drawings.webp)

![Grund i projektbrowseren](shots/site-browser.webp)

Skemaet **Matrikelkoordinater** (Skemaer → + → Matrikelkoordinater) er matriklens koordinatfortegnelse: hvert hjørne, som situationsplanen nummererer det, dets nord- og østkoordinat i projektets system, siden til det næste hjørne og arealet. Som ethvert skema kan det sættes på et ark ved siden af situationsplanen og eksporteres som CSV eller XLSX.

## Træer og buske

**Plante** (PN) sætter en plante med et klik, **Beplantningszone** (PZ) tilsår en omrids med en blanding af arter i en tæthed; markér dem og vælg arterne i egenskaberne. Arterne kommer fra et bibliotek af planter, der er hjemmehørende eller traditionelle i Rumænien, i grupper: **frugttræer** (æble, pære, blomme, sød- og surkirsebær, abrikos, kvæde), **nåletræer** (ædelgran, rødgran, skovfyr, østrigsk fyr), **løvtræer** (valnød, lind, birk, ahorn), **buske** (hassel, kirsebærkornel, hyld, hunderose, syren, havtorn, ribs, thuja, buksbom) og **bunddække**. Hver har sit latinske navn, sin sædvanlige højde og krone. Beplantningslisten viser det latinske og det rumænske navn ved hver art.

I plan tegnes hver plante oppefra med en tynd streg, så bred som sin krone, kun ved sin kontur: et valnøddetræ eller en lind som en blødt fliget krone, et frugttræ som en cirkel, en ædelgran eller en rødgran som en stjerne, en fyr som en takket ring, en busk som en lille sky, en klippet hækplante som en cirkel, med et lille kryds ved et træs stamme. Facader og snit tegner hver plante som én kontur: et træs krone på dens stamme, en nåletræs kegle i etager, en busks kuppel. Få streger hver, så en situationsplan med hundredvis af træer forbliver let. I 3D er nåletræer kegler, de andre træer har en rund krone på stammen og buske en lav kuppel.

![En lille frugthave i plan: frugttræer, en valnød, nåletræer og buske](shots/planting-symbols.webp)

Sæt visningsstilens **Staffage i 2D** til **detaljeret** for en landskabsplans symboler, i sort: et frugttræ som en fliget krone med gaflede grene, et valnøddetræ eller en lind som en bølget krone om en mørk kerne, en ædelgran eller rødgran som en stjerne af nålegrene, en fyr som en rund dusk af nåle, en busk som en lille sky. Lapin tegner selv hvert symbol, så nabotræer er lidt forskellige og en zone aldrig ser stemplet ud; i 1:1000 og mindre (en oversigtsplan) tegnes kun kronernes konturer.

![Den samme frugthave tegnet detaljeret](shots/planting-symbols-detailed.webp)

## Personer og biler

**Staffage** (EN) sætter en person eller en bil med et klik; vælg hvilken i kommandolinjens bjælke, mens værktøjet er slået til: en **stående mand**, en **gående kvinde**, et **barn**, en **person i telefon**, en **bil** eller en **SUV / varevogn**. **Staffage-række** (ER) sætter en række langs en linje, du tegner: biler for hver 2,5 m, parkeret side om side på tværs af den med fronten mod linjens venstre side, personer for hver 1,2 m, der går langs den. Hver står på terrænet under den, eller på etagens gulv, hvor der ikke er terræn.

![Personer ved døren og biler parkeret ved huset](shots/entourage-3d.webp)

Vælg en for at ændre den i egenskaberne: dens art, dens **rotation**, dens **højde** (som standard artens sædvanlige størrelse), hvor højt den står **over terrænet** (på en terrasse eller en altan) og **farven** på tøjet eller karosseriet. Flyt, drej, spejl og kopiér den som ethvert element.

I 3D er de enkle figurer: personer med lemmer, tøj, hår og sko, biler med ruder, hjul og lygter. Tegningerne holder dem enkle, som en arkitekt tegner staffage, med en tynd streg: planer og situationsplaner oppefra, en persons skuldre og hoved, en bils kontur med afrundede hjørner og dens forrude; facader og snit som silhuetter, én kontur hver, en persons figur, en bils side eller front som visningen ser den, aldrig linjerne i deres legemer.

![I plan](shots/entourage-plan.webp)

![I sydfacaden](shots/entourage-elevation.webp)

![En bils egenskaber](shots/entourage-properties.webp)

Hvordan personer, biler og planter tegnes i 2D, er en indstilling i visningsstilen, **Staffage i 2D** (i visningens egenskaber eller **Vis → Visningsindstillinger**; for en hel stil **Administrer → Visningsstile**): **konturer** (standard), **abstrakt** (en person en cirkel i plan og et hoved over en krop i en facade, en bil en afrundet kasse, et træ en cirkel med sin stamme), **detaljeret** (planternes landskabssymboler og hver linje i legemerne, med visningens solskygger, som Lapin tegnede dem før) eller **ikke tegnet**. **Silhuetfyld** fylder silhuetterne i facader og snit lysegrå eller blegrøn. De tegnes med pen 2 (0,13 mm i 1:100), medmindre **Administrer → Objektstile** giver kategorien **Staffage** en anden, og **Synlighed / Grafik** skjuler kategorien i en visning. Tegningsark, PDF og DXF udskriver dem, som visningen tegner dem; deres 3D-legemer ændres aldrig. Projekter lavet før tegner dem også som konturer.

De giver tegninger og billeder deres målestok, men er ingen del af bygningen. En IFC- eller Fragments-eksport (File → Export…) udelader dem, medmindre du sætter flueben ved **Staffage** i dens liste over arter; IFC skriver dem som IfcBuildingElementProxy »Entourage«. 3D-eksporterne (glTF, USD, COLLADA) tager dem med. Kollisionstest udelader dem.

Figurerne følger personerne og bilerne i [BimGo](https://github.com/aussieBIMguru/BimGo) af Aussie BIM Guru (MIT-0): deres proportioner, stillinger og profiler.

## Planlægningsregler og solen (Lapin Urban)

Udgaven **Lapin Urban** føjer en gruppe **Byplanlægning** til denne fane: matriklens planlægningsregler (POT, CUT, de maksimale højder, afstandene til vejen, siderne, bagskellet og vejens akse), det **tilladte bygningsvolumen** de efterlader, de nøgletal, modellen når op på i forhold til dem, og **solstudie**: solen og dens skygger i 3D for en hvilken som helst dag og time på projektets placering, og skyggeplaner for jævndøgn og solhverv.
