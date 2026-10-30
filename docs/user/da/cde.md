---
title: Projektmappe og udgivelser
order: 125
summary: Udgiv ark og modellen i en projektmappe, som ISO 19650 kræver, navngivet efter konventionen, med følgeseddel, efter de kontroller et tegnekontor gør.
---

## Projektmappen

**Manage → Project folder** vælger mappen, projektet udgives i: en mappe på din computer, et netværksshare eller en SharePoint- eller OneDrive-mappe synkroniseret til disken. Lapin beder ikke om nogen konto og taler ikke med nogen server: den skriver filer, og hvad der synkroniserer mappen, er dit kontors sag. Stien gemmes i projektet, så den rejser med filen; en mappe, der ikke er på denne computer, er en advarsel, ikke en fejl.

![Projektmappe: mappen, containerne, navngivningen](shots/project-folder.webp)

I mappen opretter Lapin ved første udgivelse de fire ISO 19650-containere: **WIP**, **Shared**, **Published** og **Archived**. Statuskoden foreslår containeren: S0 går til WIP, S1–S7 til Shared, A, B og CR til Published.

## Navngivning

Filer navngives efter ISO 19650-2: **Projekt-Afsender-Volumen-Niveau-Type-Rolle-Nummer**, derefter status og revision, for eksempel `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. I dialogen Project folder sætter du projektkoden, afsenderen (dit kontor), volumenet samt standardniveau og -rolle. Resten kommer fra projektet:

- et arks **niveau** er den etage, dets planer tegner (`00` ved ±0,00, `01` over, `B1` under, `ZZ` når det tegner flere eller ingen);
- dets **rolle** er dets disciplins (arkitektur A, konstruktion S, MEP M, anlæg C);
- dets **nummer** er cifrene i arknummeret (`A-101` bliver `0101`);
- modellen er `M3`, følgesedlen `IE`.

## Kontroller

**Manage → Check** kører, over hele projektet, de regler et tegnekontor kontrollerer med øjet før en udgivelse. Et **blokerende** fund stopper udgivelsen; en **advarsel** kan tilsidesættes. Klik på et fund for at gå til arket, visningen eller elementet.

![Kontrol: fundene efter regel](shots/cde-check.webp)

Blokerende: et tegningshoved uden nummer, titel eller tegner; en visning uden målestok; en visning uden for rammen eller over tegningshovedet; et ark, der er ændret siden sidste udgivelse, men stadig står på den revision; et arknummer, der ikke giver et filnummer. Advarsler: kontrollant, dato og fase tomme; en plan farvet efter et rumskema, eller med mange skraveringer, uden signaturforklaring; en plan, et snit eller en facade på intet ark; en situationsplan uden nordpil; elementer uden etage, type eller materiale; døre og vinduer uden mærke; rum uden nummer; de påkrævede IFC-egenskaber (IsExternal, LoadBearing, FireRating på døre) tomme.

## Udgivelse

**Manage → Issue…** skriver en udgivelse ind i en container (kun i skrivebordsappen, da den skriver filer):

![Udgivelse: arkene, revisionen, status, kontrollerne](shots/cde-issue.webp)

1. Giv udgivelsen et navn, dens **revision** (`P01`, `P02`… mens den er foreløbig, `C01`… når den er kontraktlig; den næste foreslås ud fra sidste udgivelse), dens **status** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) og **containeren**.
2. Sæt kryds ved de ark, der sendes, modellen som IFC, hvert ark også som DXF, eller alle ark også i én PDF. Hver fils navn vises, som det bliver skrevet.
3. Kontrollerne kører, mens du vælger. Med et blokerende fund kan udgivelsen ikke sendes; med advarsler alene, sæt kryds ved **Udgiv alligevel**.
4. **Udgiv** skriver hvert ark som PDF, IFC'en og resten ind i containeren, navngivet efter konventionen, beregner hash (SHA-256) for hver fil, skriver **følgesedlen** (en PDF og en CSV: projekt, udgivelse, dato, revision, status, filerne) og et **manifest** (JSON, med alle hashes) ved siden af, og registrerer udgivelsen i projektet. Hvert udskrevet ark får udgivelsens revision og status i sit tegningshoved, og dets revisionstabel viser de udgivelser, det er sendt i.
5. Projektet gemmes derefter som en **version** opkaldt efter udgivelsen (se [Filer, versioner og gendannelse](files.md)), så en udgivelse kan genåbnes, gendannes eller sammenlignes med projektet, som det er nu.

Udgivelserne står nederst i dialogen Project folder, hver med sine filer og **Åbn mappe**.
