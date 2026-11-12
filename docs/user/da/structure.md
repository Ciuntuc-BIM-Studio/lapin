---
title: Konstruktion
order: 50
summary: Søjler, bjælker og fundamenter, deres typer, og hvordan de mødes.
---

Fanen **Konstruktion** rummer bærende konstruktion og fundamenter.

![Fanen Konstruktion](shots/ribbon-structure.webp)

## Bærende konstruktion

- **Søjle** (<kbd>CL</kbd>): klik, hvor den står. En søjle går fra sit niveau til niveauet ovenover.
- **Bjælke** (<kbd>BM</kbd>): klik på dens to ender. **Buet bjælke** (<kbd>BC</kbd>) følger en bue.
- **Søjler ved modullinjer** placerer en søjle ved hver krydsning af modullinjerne. Modullinjerne tegner du med **Annotér → Modullinje** (<kbd>GR</kbd>).

## Fundamenter

- **Stribefundament** (<kbd>SF</kbd>) løber under vægge. Dets ydre lag er geringsskåret i hjørnerne.
- **Buet stribefundament** (<kbd>CF</kbd>) følger en bue.
- **Punktfundament** (<kbd>PF</kbd>) placeres under en søjle.

Tegn fundamenter på niveauet **Foundations**, eller giv dem deres egen plan.

**Et stribefundament er kun betonen**, medmindre du beder om muren. Mens **Stribefundament** eller **Buet stribefundament** er slået til, tilbyder kommandolinjens bjælke **Kun fundamentet** (standard: et betonfundament på 600 × 400 mm, en profiltype, der laves første gang) eller **Med mur** (soklen fra en stribefundamenttype ovenpå, med dens lag) og hvilken stribefundamenttype med mur, der bruges. Valget huskes. Et fundament, der allerede er tegnet, ændres med sin type i **Konstruktionstyper**.

![Stribefundamentværktøjets valg i kommandolinjen: kun fundamentet eller med mur](shots/structure-strip-option.webp)

**I planerne.** Et fundament ligger under dækket, så en plan tegner det stiplet i planen for det niveau, det står på, som en bygnings plan viser, hvad der er under den. For at se det snittet og set brug en **fundamentsplan**: i projektbrowseren laver **+** ved siden af **Etageplaner** en til det aktive niveau (snit 0,50 m under det, med udsyn 2,50 m ned).

**At tegne i en fundamentsplan.** Den viser også niveauet under, og det, du tegner dér med et dæk-, væg-, søjle- eller bjælkeværktøj, kommer på det niveau (Fundamenter i et nyt projekt, ved −1,50); fundamentsværktøjerne bevarer planens eget niveau, idet fundamentets underside er dets types under det.

![Søjler, bjælker og punktfundamenter i 3D](shots/structure-3d.webp)

## Typer og hjørner

**Konstruktionstyper** angiver tværsnittene for søjler, bjælker og fundamenter: betontværsnit og stålprofiler. En søjles tværsnit er et **Rektangel**, en **Cirkel** eller en **Polygon**, en regelmæssig givet ved sine **Sider** og sin **Diameter** over hjørnerne (en ottekantet stolpe, som en importeret IFC kan have den). Klik på en type i listen for at ændre dens størrelse eller navn, og derefter **Gem type**: alle elementer af den type følger med; **Ny type** går tilbage til at oprette en.

Bjælker og fundamenter sammenføjes af sig selv, hvor de mødes, et hjørne som geringssamling. En bjælke tegnet fra søjleakse til søjleakse stopper ved søjlernes flader; dens længde, volumen og bøjler tæller den frie spændvidde imellem dem. **Geringssamling**, **Stødsamling** og **Byt om** ændrer, hvordan to bjælker eller to fundamenter mødes. **Overfør egenskaber** (<kbd>MA</kbd>) kopierer ét elements type og indstillinger til andre, og **Opret lignende** (<kbd>CS</kbd>) begynder at tegne et nyt element af samme type.

Enhver anden profil kommer fra bibliotekerne: **Fra et profilbibliotek…** søger i dem alle (se [Profilbiblioteker](profiles.md)).

## Stribefundamenter med profil

En stribefundamenttype er enten standardtypen, en sokkelvæg med sine lag på et fundament, eller en hvilken som helst profil ført langs fundamentets akse, som Revits fundamentprofiler. I **Konstruktionstyper → Strip** vælger du **Form: Profil** og en profil: én fra kataloget (rektangulær, trappet i 2 eller 3 trin, med skrå overside, excentrisk med sokkelvæggen i den ene kant, med sokkelvæg) med sine mål, eller én af projektets (**Administrer → Profiler**, hvor du også kan indtaste dens hjørner eller læse en tegnet kontur). Profilens indsættelsespunkt ligger på fundamentets akse i typens **Bund**-kote.

Fundamenter med profil kan være lige eller buede. To, der mødes i et hjørne, skæres i gering, og ét, der ender mod et andet, fortsætter ind i det og skæres af dets krop, så hvert trin møder det andet fundaments trin. Planer, snit og facader skærer den førte krop. Mængderne giver dens længde, tværsnitsareal og volumen, og dens lodrette sider er de flader, Byggepladsindretning forskaller. I IFC er et lige fundament et IfcFooting STRIP_FOOTING, hvis krop er dets ekstruderede profil (en IfcArbitraryClosedProfileDef), skåret af hjørnernes planer; et buet skrives som sine flader. Vælg **Standard** for at gå tilbage til sokkelvæggen på fundamentet.

![Trappede stribefundamenter under et hus, i gering ved hjørnerne](shots/footing-profile-3d.webp)

![En stribefundamenttype med profil](shots/footing-profile-type.webp)

## En konstruktionsvisning

En visnings fagområde afgør, hvad den viser. En visning med fagområdet **Konstruktion** viser kun de bærende kerner, uden overflader. Du vælger fagområdet i visningens egenskaber. Se [Snit, facader og 3D](views.md).

## Belastninger

Gruppen **Belastninger** rummer det, en statisk beregning skal bruge: lasttilfælde, flade-, linje- og punktlaster samt kombinationerne. Se [Belastninger](loads.md).
