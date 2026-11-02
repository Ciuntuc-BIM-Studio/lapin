---
title: Tavole, PDF e DXF
order: 100
summary: Disponi le viste sulle tavole con un cartiglio e stampale in PDF o DXF.
---

<!-- video sheets -->

## Creare una tavola

Fai clic su **+** accanto a **Tavole** nel browser di progetto. Per posizionare una vista, trascinala dal browser sulla tavola, oppure sceglila in **Posiziona vista…** nella barra della tavola. Ogni vista mantiene la propria scala e resta collegata al modello, quindi la tavola cambia quando cambia il modello. Sulla tavola, trascina una vista per spostarla: la vista si sposta da sola, con il suo titolo, mentre la cornice, il cartiglio e le altre viste restano al loro posto; si aggancia ai bordi e al centro della cornice e alle altre viste, e i tasti freccia la spostano di 1 mm (10 mm con Shift).

![Una tavola A3 con una pianta e una sezione](shots/sheet.webp)

Seleziona la tavola per compilare il suo **cartiglio** nel pannello Proprietà: il progetto, il committente, chi l’ha disegnata, chi l’ha controllata, la data e la revisione. Lì si impostano anche il numero, il formato e l’orientamento. Se lo lasci vuoto, il campo del progetto mostra il nome del progetto: il nome dato al progetto, altrimenti il nome del suo file.

## Cosa mostra una vista

Una vista su una tavola mostra ciò che scegli, senza doverla trascinare alla cieca:

- **Ritagliala sulla tavola.** Fai clic su una vista: compaiono delle maniglie sui suoi angoli e sui suoi lati. Trascinale e il riquadro prende una parte maggiore o minore della vista, mentre il disegno resta dov’è sulla carta. Trascina con **Shift** all’interno della vista per far scorrere il disegno nel suo riquadro.
- **Disegna il suo limite nella vista.** **Annota → Limite di vista** (<kbd>VB</kbd>): disegna un rettangolo su una pianta, una sezione o un prospetto e dagli un nome, unico nel progetto. In **Posiziona vista…** la vista viene poi proposta con ciascuno dei suoi limiti («Ground floor plan › Ala est»), e una vista posizionata in questo modo mostra esattamente quel rettangolo e lo segue quando lo sposti o lo ridimensioni nella vista. La stessa vista può andare su più tavole, un limite ciascuna. Una vista posizionata passa da un limite all’altro, o a un ritaglio libero, nell’elenco sul suo riquadro. I limiti si vedono solo sullo schermo, mai sulla carta.
- **Fissala.** La puntina sul riquadro di una vista la tiene dov’è: si può ancora selezionare, ma non spostare, scostare o ritagliare per errore.

La cornice e il cartiglio appartengono al foglio. Non vengono mai selezionati né spostati, e un clic seleziona la vista più piccola sotto il puntatore, quindi una vista posizionata sopra il cartiglio si può sempre selezionare.

![Una vista selezionata su una tavola: le maniglie di ritaglio, il suo limite, la puntina](shots/sheet-view-boundary.webp)

## Stampare

I pulsanti **PDF** e **DXF** nella barra della tavola esportano la tavola. Un PDF è pronto da stampare o da inviare. Un DXF si apre in AutoCAD e in altri programmi CAD con i suoi layer, le sue linee e i suoi retini. **Stampa** invia la tavola a una stampante o a un plotter nel suo formato, in scala 1:1, come linee vettoriali: scegli il plotter e il formato nella finestra di stampa. Su una stampante più piccola, scegli l’adattamento alla pagina.
