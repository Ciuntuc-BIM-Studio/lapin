---
title: Asistentul modelului
order: 127
summary: Întrebi un model AI local despre proiect, primești rapoarte scurte și îl lași să propună valori de proprietăți pe care le aprobi tu. Doar în aplicația desktop.
---

**Asistentul** răspunde, în cuvintele tale, la întrebări despre proiectul deschis: câți pereți are fiecare etaj și ce lungime au, care uși sunt mai late de 1 m, care nu au încă rezistență la foc. Face rapoarte scurte, ca tabele pe care le poți exporta, și poate **propune** valori de proprietăți (o rezistență la foc, un stadiu, parametrii tăi) pe care le verifici și le aplici.

Folosește un model de limbaj **pe calculatorul tău**, prin [Ollama](https://ollama.com). Nimic din proiect nu pleacă de pe calculator și nimic nu ne este trimis nouă sau altcuiva.

> Asistentul există doar în **aplicația desktop**. Versiunea din browser a Lapin nu are asistent.

## Ce poate și ce nu poate

**Poate**:

- să citească proiectul: etajele, elementele, tipurile lor, materialele, seturile de proprietăți și cantitățile, extrasele;
- să numere, să adune și să facă medii (lungimi, arii, volume, orice cantitate), grupate pe etaj, pe tip sau după orice proprietate. Numerele le calculează Lapin; modelul doar le raportează;
- să propună valori de proprietăți, în seturile standard IFC (`Pset_DoorCommon.FireRating`, `Pset_WallCommon.IsExternal`…) sau în ale tale.

**Nu poate**:

- să deseneze, să mute, să modifice forma, să creeze sau să șteargă ceva. Nu schimbă geometria;
- să ruleze comenzi sau să scrie cod;
- să aplice ceva singur. Fiecare valoare propusă te așteaptă pe tine.

Un model local poate înțelege greșit o întrebare. Citește răspunsul și verifică valorile propuse înainte să le aplici.

## Pregătirea

1. Instalează Ollama de pe [ollama.com](https://ollama.com) și pornește-l (aplicația Ollama sau `ollama serve` într-un terminal).
2. Instalează un model. Cel mai bine testat este **qwen3-coder:30b** (rapid și sigur cu uneltele Lapin, cere cam 20 GB de memorie):

   ```sh
   ollama pull qwen3-coder:30b
   ```

   Pe un calculator cu mai puțină memorie merge și **gemma4:12b**, mai încet.
3. În Lapin, deschide **Asistentul**: fila **Asistent** de lângă **Proprietăți**, în dreapta, **Vedere → Asistent** sau <kbd>Ctrl</kbd>+<kbd>K</kbd> și tastezi *Asistent*.

Lapin găsește Ollama la `http://127.0.0.1:11434` și alege cel mai bun model pe care îl ai. Butonul cu rotiță din capul panoului arată **adresa Ollama**, **modelul** folosit și dacă Ollama rulează. E acceptat doar un Ollama de pe acest calculator.

## Întrebările

Scrie întrebarea și apasă <kbd>Enter</kbd> (<kbd>Shift</kbd>+<kbd>Enter</kbd> pentru rând nou). Întreabă în română, engleză sau altă limbă: răspunsul vine în aceeași limbă.

![Asistentul: un raport pe etaje și o rezistență la foc propusă pentru o ușă, care așteaptă aprobarea](shots/assistant.webp)

Cât lucrează, panoul arată fiecare pas pe un rând, de exemplu *A listat 12 din 12 · IfcDoor* sau *A calculat 2 grupe din 40 elemente*. Ține mouse-ul pe un pas ca să vezi exact ce a cerut. **Oprește** încheie răspunsul; **Conversație nouă** o ia de la capăt.

Listele și totalurile apar și ca **tabele** sub răspuns. Butonul de descărcare al unui tabel îl salvează ca fișier CSV, pentru un tabel de calcul.

Câteva întrebări de început:

- *Câți pereți sunt pe fiecare etaj și ce lungime totală au?*
- *Listează ferestrele de la etajul 1 cu lățimea și înălțimea lor.*
- *Care uși nu au rezistență la foc?*

## Aprobarea valorilor propuse

Cere, de exemplu, *Ușile mai late de 1 m: dă-le FireRating EI30.* Asistentul găsește ușile și propune valorile. Ele apar la **Modificări propuse**: fiecare element, setul și numele proprietății, valoarea de acum și cea nouă, cu o căsuță bifată.

- Debifează ce nu vrei; căsuța din capul tabelului le bifează sau le debifează pe toate.
- Un clic pe numele unui element îl selectează în vederi.
- **Aplică** scrie valorile bifate, toate odată, ca **un singur** pas: <kbd>Ctrl</kbd>+<kbd>Z</kbd> le anulează pe toate.
- **Respinge** le renunță.

Valorile intră în parametrii elementelor, arătați în panoul **Proprietăți** cu setul lor, și în exportul IFC: o proprietate standard intră în setul ei (`Pset_DoorCommon`), ale tale într-un set al lor.

Regulile pe care le urmează asistentul (și le verifică Lapin): un set standard IFC (care începe cu `Pset_`) primește doar numele proprietăților lui, pe elementele pentru care e făcut; un set al tău nu începe niciodată cu `Pset_`.
