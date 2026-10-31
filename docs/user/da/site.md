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

En **vej** eller en **sti** følger sin akse. I **Korridor…** (CO) giver fanen **Kurver** hvert knækpunkt på aksen en radius med klotoide ind og ud, sådan som en vejprojekterende lægger en kurve; et knækpunkt sat til 0 forbliver et hjørne, og kurver, der ikke kan være mellem deres naboer, afvises med begrundelsen. Stationeringen, længdeprofilet og tværprofilerne følger den krumme akse, og IFC 4.3 skriver den som en linjeføring.

CAD-linjer bliver til terrænelementer: **Vej fra CAD-linjer** (RL) og **Brudlinje fra CAD-linjer** (BL) tager en tegnet linje eller polylinje eller en fra en DXF, følger alle linjer på dens lag, der mødes ende mod ende, og laver en vej langs den eller en brudlinje i terrænet. Når linjerne har koter (en DXF's 3D-linjer), giver de vejens længdeprofil eller brudlinjens koter. Sådanne linjer beholder deres koter og kan vises i 3D (kategorien *linjer*).

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

## Planlægningsregler og solen (Lapin Urban)

Udgaven **Lapin Urban** føjer en gruppe **Byplanlægning** til denne fane: matriklens planlægningsregler (POT, CUT, de maksimale højder, afstandene til vejen, siderne, bagskellet og vejens akse), det **tilladte bygningsvolumen** de efterlader, de nøgletal, modellen når op på i forhold til dem, og **solstudie**: solen og dens skygger i 3D for en hvilken som helst dag og time på projektets placering, og skyggeplaner for jævndøgn og solhverv.
