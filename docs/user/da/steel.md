---
title: Stål
order: 56
summary: Stålstænger med indstillelige ender – forlænget eller trukket tilbage, skåret efter et plan, i gering, tilpasset til en anden stang, udklinket – også lavet ud fra et tegnet omrids; hulprofiler og koldformede profiler.
---

## Fanen Stål

![Fanen Stål](shots/ribbon-steel.webp)

- **Stang** (<kbd>SM</kbd>): en stålstang mellem to punkter, ved etagens overkant med profilets overside på aksen, som en bjælke. Dens type vælges i Egenskaber før første klik, eller fra fanen **Katalog**, hvor hver ståltype er tegnet med sit profil.
- **Konvertér til stang**: de valgte tegnede omrids bliver stænger (nedenfor).
- **Geringssamling** og **Tilpas**: to stænger samlet ved deres ender (nedenfor).
- **Ståltyper**: projektets stangtyper, og nye.

En stang er et IfcMember: dens akse går hvor som helst i rummet (et skråstag, et spær, en stolpe), dens profil kommer fra dens type, den kan drejes om sin akse (**Rotation**), og aksen kan gå gennem profilets midte, overside eller underside (**Indsættelsespunkt**). Konstruktionens bjælker og søjler af et stålprofil får de samme ender.

## Ender

Vælg en stang, eller en lige bjælke eller søjle af et stålprofil: dens sektion **Stål** i Egenskaber viser dens to ender, **Start** og **Slut**. Hver ende er et sæt regler; legemet følger dem.

![En stangs ender i Egenskaber](shots/steel-ends.webp)

- **Forlængelse**: hvor langt enden når forbi sit punkt; negativ er enden trukket tilbage (en monteringsfuge).
- **Snit**: **Vinkelret på aksen**; **Efter et plan**, drejet fra det lige snit **I plan** (om profilets lodrette akse) og **I opstalt** (om dets side), i grader; **Geringssamling** med en anden stang, på planet der halverer hjørnet mellem dem; **Tilpas til en stang**, på den flade af den anden stang, den kommer til (en søjles flange, eller siden af dens krop, når søjlen er drejet).
- **Udklinkning**: flangerne udklinket i **Top** og/eller **Bund**, med **Udklinkningens længde** tilbage fra enden og **Udklinkningens dybde** fra profilets kant, som for en bjælke, der føres ind på en hovedbjælke.

Gering og tilpasning nævner den anden stang: flyt den, og enden følger med; slet den, og enden er lige igen og siger det. **Geringssamling** på fanen samler de to valgte stænger i gering i det hjørne, hvor de mødes; **Tilpas** tilpasser den første valgte til den anden.

## Fra skitse til stang

Tegn stangens omrids som en lukket polylinje eller et rektangel (**Tegn**), og vælg så **Stål → Konvertér til stang** med et eller flere omrids valgt. I ét trin læses hvert omrids og erstattes af en stang:

![To omrids skitseret på planen](shots/steel-sketch.webp)

- dets **to lange parallelle kanter** (inden for en halv grad) giver aksen, deres midterlinje fra ende til ende, og **bredden** mellem dem;
- **kanten over hver ende** giver den endes snit: lige, eller efter et plan med kantens hældning.

På en **plan** er omridset stangen set ovenfra: dens bredde er flangernes, og et profil med den bredde findes blandt projektets stangtyper og i kataloget (ellers den type, der er valgt til Stang); stangen ligger ved etagens overkant. På et **snit** eller en **opstalt** er omridset stangen set fra siden: dens bredde er profilets højde, en skrå ende er et snit i opstalt, og et retvinklet **hak** ved en ende er en **udklinkning** foroven eller forneden; stangen ligger, hvor den er tegnet. Et hak tegnet på en plan afvises (en udklinkning ses fra siden). Et omrids, der ikke er et bånd (en trekant, et L, kanter der ikke er parallelle), bliver som det er, og Lapin siger det.

## Ståltyper

**Stål → Ståltyper** viser projektets stangtyper med deres vægt pr. meter og laver nye: et profil fra kataloget (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** og de generiske koldformede **C**, **Z** og **Σ**) eller et hulprofil eller koldformet profil **med egne mål** (højde, bredde, tykkelse, læbe). Den nye type kan være en stangtype, eller en **bjælketype** eller **søjletype** i konstruktionen, så værktøjerne Bjælke og Søjle også sætter stål. Et klik på en type i listen gør den til den, Stang sætter.

![Dialogen Ståltyper](shots/steel-types.webp)

Hulprofiler er hule indvendigt, i 3D-visningen, i snittene og i mængderne. De koldformede profiler er generiske former med mål, skarpt bukkede, uden en producents katalog.

![Stænger i 3D](shots/steel-3d.webp)

## IFC og mængder

En stang eksporteres som **IfcMember** med sin **IfcMemberType**. Dens legeme skrives, som de andre programmer skriver stål (Tekla, Advance Steel, Revit): dens profil **ekstruderet** (IfcExtrudedAreaSolid), **beskåret** af endernes planer (IfcBooleanClippingResult med IfcHalfSpaceSolid), og hver udklinkning et **IfcOpeningElement**, der skærer den ud. En stålbjælke eller -søjle med ender skrives på samme måde. Hver stangs mængder giver dens længde mellem snittene, profilets areal, dens volumen og dens nettovægt (7850 kg/m³).
