---
title: Niveauer og planer
order: 60
summary: Tilføj niveauer, indstil deres højder, og se et andet niveau under den plan, du tegner på.
---

<!-- video levels -->

## Niveauer

Projektbrowseren viser niveauerne øverst, det højeste først. **+** ved siden af **Niveauer** tilføjer et niveau over det højeste.

Markér et niveau for at se dets **Basisniveau** og **Topniveau**. Begge kan ligge over eller under ±0.00. Højden er afstanden mellem dem. Når du flytter basen, bliver toppen, hvor den er. Et nyt projekt starter med Foundations, Ground floor og First floor. Et niveau er højst 100 m højt og ligger inden for ±10 km af ±0.00: en værdi skrevet i den forkerte enhed afvises. En trappe, der ville stige mere end 50 m til niveauet ovenover, stopper der og siger hvorfor.

![Et niveaus egenskaber: basisniveau, topniveau og højde](shots/level-properties.webp)

Hvert niveau har sin egen plan under **Etageplaner**. Dobbeltklik på en plan for at tegne på det niveau. Det niveau, du tegner på, vises i feltet øverst til venstre i visningen.

I snit og facader markeres hvert niveau til højre med navn og kote. **Administrer → Objektstile → Skjulte linjer** sætter også **niveaumærket**: en åben trekant, en trekant med venstre halvdel sort (som den rumænske standard tegner den), en sort trekant eller en streg, og dens størrelse på papir.

Koter skrives i projektets enheder: i meter (+3.00) i et metrisk projekt, som tegninger skriver dem, og i fod og tommer (+9'-10 1/8") i et imperialt. Browseren, boksen øverst til venstre i visningen, niveaumærkerne i snit og facader og kotemålene følger med.

## At se niveauet nedenunder

Vil du se et andet niveau, mens du tegner, så vælg det under **Underlag** i planens bjælke. Det niveaus plan tegnes nedenunder, falmet og gråt. Du kan fange til det, men du kan ikke markere det.

![Første sal tegnet over stueetagen, vist som underlag](shots/underlay.webp)

Underlaget hjælper dig kun med at tegne; det udskrives ikke. Vil du udskrive andre niveauers elementer på en plan, så åbn planvisningens egenskaber og vælg dem under **Viste etager**. Du kan vise nogle niveauer eller dem alle, i halvtone hvis du vil.

## Kopiér til andre etager

For at gentage vægge, dæk eller en hel plan på andre etager skal du markere dem (vægge med deres døre og vinduer, dæk, rum, en gruppe…) og vælge **Redigér → Kopiér til etager**. Du kan også skrive `COPYSTOREY` på kommandolinjen eller højreklikke på markeringen i en plan eller i 3D-visningen. Markér de etager, der skal kopieres til. **Alle ovenover** og **Alle nedenunder** markerer alle etager over eller under markeringens, og et Skift-klik markerer alle etager mellem den klikkede og den sidst klikkede. Den etage, markeringen står på, kan ikke markeres.

![Kopiér til etager: to etager markeret og indstillingerne](shots/copy-to-storeys.webp)

- **Relativ** (standard): hver kopi står i samme højde over sin nye etage som originalen over sin egen. En væg tegnet i stueetagen står på 1. sal.
- **Absolut**: hver kopi bliver, hvor originalen er i rummet, på den nye etage. Dens baseforskydning udligner forskellen.
- **Også værtsbårne elementer**: døre og vinduer følger deres vægge og åbninger det, de skærer i. Mærker og mål for de kopierede elementer flytter fra deres etages plan til den nye etages plan. Et rum tager sine overflader med og en trappe rummet under den. Fjern markeringen for at kopiere væggene uden døre og vinduer.
- **Genopbyg relationerne mellem kopierne**: vægge samlet i et hjørne eller et T samles på samme måde på hver ny etage. En væg fastgjort til et dæk eller et tag, der kopieres med den, fastgøres til kopien. En gruppe bliver en gruppe af kopierne, og en samling kopieres hel. En trappe går op til etagen over sin nye og skærer sin egen åbning i dækket der.
- **Spring dubletter over**: en etage, der allerede har det samme element samme sted, får ikke et til. Beskeden nederst siger, hvor mange der blev sprunget over.

Kopierne bruger de samme typer som originalerne og får nye IFC-GlobalIds. Alle kopierne er ét fortryd-trin og forbliver markeret bagefter, så du kan flytte eller ændre dem samlet. I en delt session fortryder kun gæsten sine egne kopier.

![Stueetagens vægge og dæk kopieret til de to etager ovenover, i 3D](shots/copy-to-storeys-3d.webp)

For i stedet at flytte markeringen til en anden etage skal du vælge **Redigér → Flyt til etage**. Elementerne beholder deres højder over den nye etage eller bliver, hvor de er i rummet.

## Et elements kote

Et element på et niveau står i niveauets kote og med en forskydning fra den. **Egenskaber → Placering** viser dets **Etage**, dets **Forskydning fra niveau** og dets **Kote** over ±0,00: fod af en væg eller søjle, overside af et dæk eller en bjælke (en bjælkes forskydning er fra niveauets top), underside af et fundament (dets types underkote under niveauet plus forskydningen). Skriv det ene tal, og det andet følger: elementet flyttes i alle visninger i ét fortrydelsestrin. For at sætte et element på et andet niveau ændrer du dets **Etage**. Det, du tegner i en plan, står på den plans niveau.
