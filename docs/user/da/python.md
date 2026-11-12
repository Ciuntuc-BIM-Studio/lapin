---
title: Python-scripts
order: 127
summary: Styr Lapin med Python fra Script-panelet, et udviklerekstra: en konsol og en editor, der kører din egen Python, hver kørsel ét fortryd-trin.
---

## Slå Udviklerekstra til

Lapin kan styres med Python: hvert værktøj er en kommando, og et script sender de samme kommandoer. **Script**-panelet er et *udviklerekstra*, slået fra, indtil du slår det til, så ellers ses intet af det.

Åbn **Filer → Præferencer…**, og sæt under **Generelt** flueben ved **Udviklerekstra**.

![Præferencer: Udviklerekstra](shots/python-preferences.webp)

- **Python**: den Python, panelet kører. Tom: Lapin leder efter en på denne computer (`python3`, `py -3` på Windows, den fra python.org og den fra Homebrew). Det skal være **Python 3.11 eller nyere**; Lapin installerer ikke Python. Hent den fra [python.org](https://www.python.org/downloads/), hvis du ikke har nogen, eller skriv stien til den, du vil bruge (f.eks. et virtuelt miljøs).
- **Ekstern editor**: den editor, **Åbn i ekstern editor** bruger, som en kommando (`code` for Visual Studio Code). Tom: systemets teksteditor.

Python kører kun i desktop-appen: i Lapin til browseren siger panelet det.

## Script-panelet

Åbn **Vis → Script**. Panelet åbner langs vinduets nederste kant; træk i dets øverste kant for at gøre det højere. Dets overskrift siger, hvilken Python det fandt, eller hvorfor det ikke fandt nogen.

![Script-panelet efter en kørsel: fire vægge, ét fortryd-trin](shots/python-panel.webp)

- **Mine**, til venstre: dine egne scripts, almindelige `.py`-filer i Lapins mappe på denne computer. Klik på et for at åbne det; **Nyt script** starter fra et eksempel.
- **Editoren**, i midten: Python med farver og indrykning. Skriv `lapin.`, og den foreslår, hvad Lapin har (kommandoerne fra alle modulerne i din udgave og dens egne kald); `lapin.architecture.` foreslår arkitekturkommandoerne med deres argumenter. Giv scriptet et navn, og **gem** det i Mine; **Åbn i ekstern editor** åbner dets fil i din editor, og det, du gemmer der, kommer tilbage i panelet.
- **Konsollen**, til højre: det, scripts udskriver, deres fejl med den linje, de stoppede ved (markeret i editoren), og en linje til at skrive Python i. **Enter** kører den, **Skift+Enter** starter en ny linje, **Op** og **Ned** henter tidligere linjer frem. Et navn, du sætter i konsollen, bliver til næste linje og til de scripts, du kører.

I panelet er `lapin` pakken `lapinbim`, der allerede er indlæst. Det samme script kører fra en terminal med `import lapinbim as lapin`, mens Lapin er åben.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Længder er i meter og vinkler i grader, uanset hvilken enhed projektet viser.

## Kør, Stop, Genstart, Fortryd

- **Kør** (**Ctrl+Enter** i editoren) kører scriptet. Alt, hvad det gør, er **ét fortryd-trin**, opkaldt efter scriptet: én **Fortryd** fjerner det hele.

  ![Én Fortryd fjernede kørslens fire vægge](shots/python-undo.webp)

- Mens et script kører, tilhører modellen scriptet: et værktøj, du bruger i vinduet, afvises med *A script is editing the model*; visninger, markering og skemaer fortsætter.
- Et script, der fejler, beholder **intet** af det, det gjorde: modellen er som før **Kør**, og konsollen viser fejlen og dens linje.
- **Stop** stopper scriptet; intet af det, det gjorde, bevares. Et script, der ikke stopper inden for to sekunder, får sin Python lukket.
- **Genstart** starter Python igen: de navne, konsollen huskede, glemmes.

## Optag, hvad du gør, som et script

Fanen **Optager** ved siden af konsollen skriver det, du gør i vinduet, som Python, én linje pr. trin.

1. Klik på **Optag**. En rød prik på fanen viser, at der optages.
2. Arbejd som sædvanlig: tegn vægge, ret en egenskab, slet noget. Hvert trin vises straks som en linje, for eksempel `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Klik på **Stop** og derefter på **Kopiér** (for at indsætte scriptet et andet sted) eller **Gem som script** (det kommer ind under Mine og åbner i editoren).

![Optageren efter to vægge er tegnet](shots/python-recorder.webp)

Scriptet skrives, så det også virker i et andet projekt:
- det, et trin lavede, gemmes i en variabel (`wall1`), og senere trin bruger den (`wall1.id`);
- den etage, du tegnede på, findes ud fra sit navn, ellers ud fra sin kote (`lapin.model.storey("Ground floor", 0)`), og en type ud fra sit navn (`lapin.model.find("IfcWallType", "Brick 250")`);
- et andet element, der allerede var i projektet, angives ved sit id (`lapin.model["…"]`), så den linje virker kun i dette projekt;
- med **Markering som lapin.selection()** afkrydset skrives en kommando på det, du havde markeret, som `lapin.selection()`: scriptet arbejder så på det, der er markeret, når du kører det.

Et **Fortryd** under optagelsen tager det sidste trin ud af scriptet, og **Annuller fortryd** sætter det tilbage, så scriptet rummer det, du endte med at gøre. Det, et script gør, optages aldrig, og det gør spørgsmål, der ikke ændrer noget (en oversigt der læses, et mål), heller ikke.

![Det optagede script kørt i et nyt projekt: de samme to vægge](shots/python-recorder-new.webp)

## Projektets scripts

Et script kan gemmes **i projektet**, så det følger med filen: åbn eller skriv det i editoren, og klik på **Gem i projektet**. Det vises under **Projektets** til venstre. At gemme, omdøbe og slette et projekts script er hver især ét fortryd-trin, og projektets versioner bevarer dets scripts. I `.lapin`-filen er hvert script også en almindelig tekstfil, `scripts/<name>.py`, så det kan læses uden Lapin.

**Et projekts script kører aldrig af sig selv**, hverken når projektet åbnes, eller når det gemmes. Første gang du kører det, og igen hver gang dets kode er ændret, spørger Lapin:
- **Kør én gang**: det kører denne gang, og næste kørsel spørger igen;
- **Stol på**: det kører, og denne kode huskes som troværdig på denne computer (en ændring af den spørger igen);
- **Annuller**: intet kører.

Et script, du selv gemmer i projektet, er troværdigt, i det øjeblik du gemmer det. Dine egne scripts (Mine) spørger aldrig.

## Hvad der er markeret

`lapin.selection()` giver det, der er markeret i vinduet, som elementer:

```python
for e in lapin.selection():
    print(e.name, e.type)
```

Et elements felter læses ved deres navne og ændres ved tildeling: `wall.height = 3` ændrer væggen (ét fortryd-trin, eller en del af Kør). `with lapin.step("Name"):` gør flere kommandoer til ét fortryd-trin, mens hver stadig svarer med det samme, så en senere kan bruge det, en tidligere lavede.

## Sikkerhed

Et script kan alt, hvad du kan på denne computer: det kører i din egen Python med dine rettigheder. Kør scripts, du selv har skrevet eller stoler på; et projekts script spørger først.

En gæst i en delt session kan hverken køre scripts på din computer eller lægge et i dit projekt, og et script forbinder kun til Lapin på sin egen computer.

## For udviklere

Hvad `lapin` tilbyder (kommandoer, forespørgsler, batches, modellen, hændelser), beskrives i udviklervejledningen *Lapin from Python* (`docs/python.md` i Lapins repository); pakken findes på PyPI som `lapinbim`.
