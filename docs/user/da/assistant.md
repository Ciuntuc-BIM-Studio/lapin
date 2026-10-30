---
title: Modelassistenten
order: 127
summary: Spørg en lokal AI-model om dit projekt, få korte rapporter, og lad den foreslå egenskabsværdier, som du godkender. Kun i skrivebordsprogrammet.
---

**Assistent** besvarer spørgsmål om det åbne projekt med dine egne ord: hvor mange vægge hver etage har, og hvor lange de er, hvilke døre der er bredere end 1 m, hvilke der stadig ikke har nogen brandklasse. Den laver korte rapporter som tabeller, du kan eksportere, og den kan **foreslå** egenskabsværdier (en brandklasse, en status, dine egne parametre), som du gennemgår og anvender.

Den kører en sprogmodel **på din computer** gennem [Ollama](https://ollama.com). Intet om dit projekt forlader maskinen, og intet sendes til os eller nogen anden.

> Assistenten findes kun i **skrivebordsprogrammet**. Browserversionen af Lapin har ingen assistent.

## Hvad den kan og ikke kan

Den **kan**:

- læse projektet: etager, elementer, deres typer, materialer, egenskabssæt og mængder, og skemaerne;
- tælle, summere og tage gennemsnit (længder, arealer, volumener, enhver mængde), grupperet efter etage, type eller en hvilken som helst egenskab. Lapin beregner tallene; modellen rapporterer dem kun;
- foreslå egenskabsværdier, i IFC's standardsæt (`Pset_DoorCommon.FireRating`, `Pset_WallCommon.IsExternal`…) eller i dine egne.

Den **kan ikke**:

- tegne, flytte, omforme, oprette eller slette noget. Den ændrer ikke geometrien;
- køre kommandoer eller skrive kode;
- anvende noget af sig selv. Hver værdi, den foreslår, venter på dig.

En lokal model kan misforstå et spørgsmål. Læs dens svar, og kontrollér de foreslåede værdier, før du anvender dem.

## Opsætning

1. Installér Ollama fra [ollama.com](https://ollama.com), og start det (Ollama-appen, eller `ollama serve` i en terminal).
2. Installér en model. Den, vi har testet bedst, er **qwen3-coder:30b** (hurtig og pålidelig med Lapins værktøjer, kræver omkring 20 GB hukommelse):

   ```sh
   ollama pull qwen3-coder:30b
   ```

   På en computer med mindre hukommelse virker **gemma4:12b** også, men langsommere.
3. Åbn **Assistent** i Lapin: fanen **Assistent** ved siden af **Egenskaber** til højre, **Vis → Assistent**, eller <kbd>Ctrl</kbd>+<kbd>K</kbd> og skriv *Assistant*.

Lapin finder Ollama på `http://127.0.0.1:11434` og vælger den bedste model, du har. Tandhjulsknappen øverst i panelet viser **Ollama-adresse**, den **Model**, der skal bruges, og om Ollama kører. Kun en Ollama på denne computer accepteres.

## Spørg

Skriv et spørgsmål, og tryk på <kbd>Enter</kbd> (<kbd>Shift</kbd>+<kbd>Enter</kbd> for en ny linje). Spørg på dansk, engelsk eller et andet sprog: svaret kommer på samme sprog.

![Assistenten: en rapport pr. etage og en brandklasse foreslået for en dør, der venter på godkendelse](shots/assistant.webp)

Mens den arbejder, viser panelet hvert trin på én linje, for eksempel *Listede 12 af 12 · IfcDoor* eller *Beregnede 2 gruppe(r) over 40 element(er)*. Hold musen over et trin for at se præcis, hvad den spurgte om. **Stop** afslutter svaret; **Ny samtale** starter forfra.

Lister og totaler vises også som **Tabeller** under svaret. Downloadknappen på en tabel gemmer den som en CSV-fil til et regneark.

Nogle spørgsmål at starte med:

- *Hvor mange vægge er der pr. etage, og hvad er deres samlede længde?*
- *Vis vinduerne på første sal med deres bredde og højde.*
- *Hvilke døre har ingen brandklasse?*

## Godkend de værdier, den foreslår

Spørg for eksempel *Døre bredere end 1 m: giv dem FireRating EI30.* Assistenten finder dørene og foreslår værdierne. De vises under **Foreslåede ændringer**: hvert element, egenskabssættet og navnet, dets værdi nu og den nye, med et afkrydsningsfelt.

- Fjern fluebenet ved det, du ikke vil have; feltet i overskriften sætter eller fjerner alle flueben.
- Klik på et elements navn for at markere det i visningerne.
- **Anvend** skriver de afkrydsede værdier, alle sammen, som **ét** trin: <kbd>Ctrl</kbd>+<kbd>Z</kbd> tager dem alle tilbage.
- **Afvis** forkaster dem.

Værdierne går ind i elementernes parametre, vist i panelet **Egenskaber** med deres sæt, og ind i IFC-eksporten: en standardegenskab kommer i sit sæt (`Pset_DoorCommon`), dine egne kommer i et sæt for sig.

De regler, assistenten følger (og Lapin kontrollerer): et standard-IFC-sæt (der starter med `Pset_`) tager kun sine egne egenskabsnavne, på de elementer det er beregnet til; et sæt af dine egne starter aldrig med `Pset_`.
