---
title: Møbler
order: 47
summary: Sofaer, hjørnesofaer, lænestole, stole, senge, garderobeskabe, kommoder, reoler, skænke, borde, skriveborde og køkkener i seks indretninger ud fra parametriske typer, stillet op ad vægge med et klik.
---

## Placér møbler

Fanen **Interiør** har, efter overfladerne, et værktøj til hver slags møbel: **Sofa** (<kbd>SO</kbd>), **Hjørnesofa**, **Lænestol** (<kbd>AC</kbd>), **Stol**, **Seng** (<kbd>BD</kbd>), **Natbord**, **Garderobeskab** (<kbd>WD</kbd>), **Kommode**, **Reol**, **Skænk**, **Bord** (<kbd>TB</kbd>), **Sofabord** og **Skrivebord**. Vælg et, og klik i planen.

Klik tæt på en væg, og møblet stilles op ad den. Dets bagside ligger på væggens flade (på pudsen, ikke på kernen), forsiden vender mod rummet, og det glider langs væggen, indtil det ikke længere går ind i væggen på tværs af et hjørne. Klik længere fra alle vægge, og møblet står, hvor du klikkede.

Mens et værktøj er aktivt, viser kommandolinjens bjælke:

- **typen**, værktøjet placerer, valgt blandt projektets typer af den slags (findes der ingen endnu, oprettes den første, når du placerer møblet);
- **drejningen**, 0°, 90°, 180° eller 270°, for et fritstående møbel;
- **Mod vægge**, som du kan slå fra for at placere et møbel præcis, hvor du klikker, også ved siden af en væg.

Egenskabspanelet viser den samme type før klikket, med dens størrelse og **Redigér typer…**.

![En stue, et køkken og et soveværelse møbleret, i plan](shots/furniture-plan.webp)

Planer tegner møbler med deres symbol, efter tegnekonventionen, ikke snittet:

- en sofa med ryglæn, armlæn og hynder;
- en seng med sengegavl, puder og dyne slået tilbage;
- et garderobeskab med bøjlestang og låger;
- et køkken med vask og kogeplade og overskabene stiplet.

Facader og snit tegner kroppene med deres materialers skraveringer. I 3D er møblerne low-poly-kroppe, som Lapin laver.

![Det møblerede hus i 3D](shots/furniture-3d.webp)

## Stole om et bord, og køkkenet

**Stole om et bord:** klik på et bord eller et skrivebord. Stolene sættes langs de lange sider, cirka én for hver 600 mm, og én ved hver ende, når den er bred nok; et rundt bord får dem i en cirkel. Hver stol vender mod bordet. Et skrivebord får kun stole på forsiden. Stolene har den type, der er valgt til værktøjet **Stol**.

**Køkken** (<kbd>KT</kbd>) tegnes med to punkter langs en væg:

- linjens længde er køkkenets længde, og dets bagside ligger på væggens flade;
- underskabene deles ligeligt i cirka 600 mm;
- det har bordplade, vask og kogeplade (hvor de står langs køkkenet, indstilles på typen), overskabe ovenover og et højskab til køleskabet i den ene ende.

Et køkkens **Længde** kan stadig ændres i egenskaberne; skabene deles igen.

### Køkkenets indretninger

En køkkentype har en **Layout** (indretning): **One wall** (den lige række), **Galley** (to parallelle rækker med en gang imellem), **L-shaped**, **U-shaped**, **Island** (en række på bagvæggen og en fritstående ø med sin gang) eller **Peninsula** (et L med en række, der rager ud fra benet, med bordpladen overhængende til barstole). Når du vælger en, starter typen fra indretningens mål, og hvert mål kan redigeres ved siden af forhåndsvisningen: benenes længde fra bagvæggen, gangen mellem rækkerne, den modstående rækkes længde, øens længde, dybde og placering, halvøens længde og overhæng og, for et L eller en halvø, hjørnet til venstre eller højre.

![De seks indretninger i plan: én væg, parallel, L, U, ø og halvø](shots/kitchen-layouts.webp)

**Sink on** og **Hob on** sætter dem på hovedrækken eller på den anden del (benet, den modstående række, øen, halvøen), **Sink at** og **Hob at** siger hvor langs den, og **Tall unit (fridge)** står i en fri ende. Hjørneskabene er blinde; overskabe er kun på rækker mod en væg, ikke på en ø eller en halvø.

![En køkkentype: L-formet](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) tegner enhver indretning med to punkter langs den væg, hovedrækken står mod: linjen er dens længde, ryggen på væggens flade, og et L, en halvø, et U eller et parallelkøkken skubbes ind i det hjørne eller mellem de vægge, det tegnes nær. **Flip** giver et L eller en halvø hjørnet på den anden side. Møbelskemaet nævner indretningen og fodaftrykkets dybde. I IFC forbliver det et IfcFurniture af en brugerdefineret Kitchen-type.

## Et møbels egenskaber

Vælg et møbel for at se:

- **Type**, dets slags og størrelse;
- **Rotation**;
- **Over gulvet**, for et møbel hævet på et podie;
- **Længde**, for et køkken.

**Vend** giver møblet dets anden hånd, hvor det står: chaiselongen i den anden side, køleskabet i den anden ende, hjørnesofaens hjørne i den anden side. Forsiden bliver, hvor den var. **Redigér → Spejl** gør det samme.

Træk i grebet midt på et møbel for at flytte det; træk i grebet foran det for at dreje forsiden mod markøren.

![En sofas egenskaber](shots/furniture-properties.webp)

## Møbeltyper

**Interiør → Møbeltyper** viser projektets typer i grupper (siddemøbler, senge, opbevaring, borde, køkken), hver med antallet af møbler, der bruger den. Vælg en slags og **Ny type**, eller **Duplikér** en type for at ændre en kopi. En type har:

- sit **navn**, sin **bredde**, **dybde** og **højde** (for en sofa ryglænets, for en seng sengegavlens, for et køkken bordpladens);
- sin slags egne parametre:
  - sofaer og lænestole: sæder, armlæn, armlænsbredde, sædehøjde, en chaiselong til venstre eller højre og dens længde, ben;
  - en hjørnesofa: den side, hjørnet sidder på, og den sides længde;
  - en seng: madrassens overside, sengegavl, fodgavl, puder, en dyne slået tilbage;
  - et garderobeskab: låger, hængslede eller skydelåger, skuffer forneden, sokkel;
  - en kommode: rækker og kolonner af skuffer;
  - en reol: hylder og låger forneden op til en højde;
  - en skænk: låger og skuffer;
  - et bord: rektangulært, rundt eller ovalt, på fire ben eller en søjlefod;
  - et skrivebord: en skuffesektion til venstre eller højre;
  - et køkken: underskabsbredde, sokkel, bordplade, placering af vask og kogeplade, overskabe, højskab;
- sine **materialer** efter rolle: korpus, polstring, plade, fronter, sengetøj, metal, glas. Skriv et hvilket som helst materialenavn; et, som projektet ikke har, oprettes, og **Administrer → Materialer** fastlægger dets farve og skraveringer.

Forhåndsvisningen ved siden af felterne viser typens plansymbol og forside, mens du ændrer den. Hvert møbel af en type følger enhver ændring.

![Dialogen Møbeltyper med en sofa og dens chaiselong](shots/furniture-types.webp)

## Skemaer og IFC

**Møbelskema** viser hver type med dens slags, størrelse, antallet af møbler og de rum, de står i, med totalen. **Møbler pr. rum** viser hvert rum med det, der står i det. Begge ligger i projektbrowseren under **Skemaer** og udskrives og eksporteres som ethvert skema. En møbeltype med en prispost prissættes pr. stk. i overslaget.

I IFC er et møbel et **IfcFurniture** med den foruddefinerede type for dets slags (SOFA, CHAIR, BED, TABLE, DESK, SHELF eller USERDEFINED med Wardrobe, Kitchen…). Dets **IfcFurnitureType** er knyttet med IfcRelDefinesByType, og dets kroppe bærer deres materialer. Lapin læser sine egne møbler tilbage som native møbler, der kan redigeres. Møbler fra andre programmer kommer ind som referencer.
