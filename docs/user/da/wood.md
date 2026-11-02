---
title: Træ
order: 54
summary: Træskeletvægge fra konfiguratoren: stolper i modul, remme, noggings, og hver dør og hvert vindue indrammet efter sine regler.
---

## Træskeletvægge

En vægtype kan bygges som træskelet: **Wood → Timber frame** åbner konfiguratoren med projektets vægtyper til venstre. Sæt kryds i **Timber frame**, og typens kerne bygges af dele i stedet for ét lag: remmene, stolperne i deres modul, rækkerne af noggings og omkring hver dør og hvert vindue de gennemgående stolper, bærestolperne, overliggeren og bundstykket. De øvrige lag bliver, som de er, og isoleringen fylder hulrummene mellem delene.

![Træskelet-konfiguratoren](shots/wood-frame.webp)

- **Forudindstillinger**: **nordisk** (45 × 145 ved 600 mm, C24) eller **rumænsk** (50 × 150 ved 625 mm, C24, en række noggings ved 1,25 m). Bagefter kan hvert felt ændres.
- **Stolper, afstand, kvalitet, remme, endestolper, rækker af noggings**. **Modul fra** angiver, hvor stolpernes modul begynder: ved hver vægs start eller ved projektets origo, så en facades vægge deler det.
- **Bærende**: en skillevæg bærer intet og får et liggende stykke over sine åbninger i stedet for en overligger.

## Døre og vinduer

Dørene og vinduerne forbliver arkitektens: flyt et, eller skift dets type, og indramningen følger. Rammen måles fra **råhulmålet**: døren eller vinduet plus monteringsfugerne (i siderne, foroven, under et vindue; en dør går ned til gulvet).

Hver åbning tager reglen for sit **breddetrin** (tabellen under **Openings**: op til 900 mm, op til 1800 mm, derover, som standard): gennemgående stolper og bærestolper i hver side, overliggeren (ingen, massiv, LVL eller limtræ, dens tværsnit og lag) og bundstykkets lag. Ingen stolpe står i et råhul; de korte over overliggeren og under bundstykket holder modulet, så pladernes samlinger stadig rammer stolper. To åbninger tættere end den **mindste murpille** deler én overligger og de yderste gennemgående stolper. En åbning for tæt på væggens ende meldes i væggens egenskaber, flyttes aldrig.

Vælg en dør eller et vindue: dets afsnit **Wood** viser råhulmålet, reglen og hvad det får. Sæt kryds i **Egen regel** for at give netop den åbning andre stolper, en anden overligger eller et andet bundstykke.

## Hjørner, plader og skeletopstalten

- **Hjørner**: hvor en anden væg stopper mod denne vægs ende (stødt hjørne), får den gennemgående væg **tre stolper** (én efter den andens inderside), et **Californien**-hjørne (en liggende stolpe på indersiden, til beklædningen) eller **kun sine endestolper**. Et geringshjørne beholder begge vægges endestolper.
- **T-samlinger**: hvor en væg stopper mod denne i dens forløb, får denne **en stolpe i hver side** eller **stolper og stigeklodser** imellem.
- **Plader**: sæt kryds ved et lag (OSB, træfiberplade, gipsplade) og angiv pladens format: laget lægges som stående plader med samlingerne på stolpemodulet, skåret ved råhullerne og langs toppen. **Wood → Boards schedule** tæller pladerne for hver væg, nettoarealet og spildet.
- **Skeletopstalt**: i en skeletvægs afsnit **Wood** laver knappen et snit langs væggen gennem kernens midte, 1:20, alle dele skåret, med en målkæde gennem råhullerne, stolpemodulet under den og hvert råhuls størrelse. Den placeres på et ark som enhver visning.

## CLT

I samme konfigurator kan en væg- eller dæktype bygges **af CLT-elementer**: lagene (hvert lags tykkelse, 3, 5, 7 eller 9), kvaliteten, det længste og højeste (eller bredeste) element pressen laver, samlingen mellem elementer (stød, halv overlap, fer), hvor langt en åbning holder sig fra elementkanten og monteringsfugen. En væg deles i elementer på langs, med samlingerne flyttet væk fra åbningerne; hvert element er kernens egen krop med råhullerne skåret ud. Et dæk deles over spændet (den korte vej, langs x eller y). **Wood → CLT panels** viser hvert element med mærke, mål, lag, kvalitet, samling, areal og volumen; i IFC er hvert et IfcBuildingElementPart kaldet CLT panel.

## Trælisten

**Wood → Timber schedule** laver en skæreliste over alt træ i projektet, væggenes dele og tagkonstruktionerne: del, tværsnit, kvalitet, længde, stk., samlet længde og volumen, med summer pr. tværsnit. I IFC er hver del et IfcMember (STUD, PLATE, overligger og bundstykke ved navn) under sin væg, og isoleringen et IfcCovering INSULATION.
