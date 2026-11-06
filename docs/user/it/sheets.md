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

## Il tuo cartiglio

**Gestisci → Cartigli…** elenca i cartigli: quelli di Lapin (**Lapin standard**, quello che avevano finora tutte le tavole, il **cartuș** rumeno e uno **ISO 7200**), quelli del progetto e quelli della libreria di questo computer, offerti in ogni suo progetto. Clic su uno per vederlo con i valori del progetto. **Duplica nel progetto** ne fa una copia modificabile, **Nuovo** uno vuoto; **Imposta come predefinito del progetto** lo dà a ogni tavola che non ne sceglie uno, e una tavola sceglie il suo in **Cartiglio** nelle proprietà.

![Cartigli: quelli di Lapin, del progetto e di questo computer, quello scelto con i valori del progetto](shots/title-blocks.webp)

**Modifica…** apre l’editor. Il cartiglio è un riquadro in millimetri di carta posto in un angolo della cornice (in basso a destra per impostazione), così lo stesso cartiglio va bene dall’A4 all’A0, verticale o orizzontale; i margini della cornice si impostano qui. Disegna con **Linea**, **Rettangolo**, **Polilinea**, **Testo**, **Immagine** (il tuo logo, PNG o JPEG) e **Tabella delle revisioni**; un rettangolo o una polilinea chiusa prende un riempimento o un tratteggio, un testo un’altezza o uno stile di testo degli Standard di disegno. I clic si agganciano a estremi, angoli e punti medi degli elementi e alla griglia; **Maiusc** tiene dritta la linea. Trascina un elemento per spostarlo, le sue maniglie per rimodellarlo; le frecce lo spostano, **Canc** lo elimina, **Ctrl+Z** annulla.

L’elenco **Campi** contiene il testo vivo: del progetto (nome, numero, committente, sito, permesso, progettista, persone e i tuoi parametri), della tavola (numero, nome, scala, formato, revisione, stato, data, disegnato, verificato e approvato da, fase, tavola x di y) e del sistema (data di stampa, nome del file). Un clic mette il campo nel testo selezionato o lo colloca come nuovo testo; un testo può mescolare parole e campi, `Progetto: {project.name}`. L’editor mostra il valore di ogni campo su una tavola di esempio; un campo senza valore appare grigio con il suo nome, uno che Lapin non conosce in rosso (stampato come `<campo>`, e **Gestisci → Verifica** lo segnala). **Salva** conserva il cartiglio come un solo passo da annullare.

![L’editor: un cartiglio in stile finlandese, i suoi campi con i valori, un campo aggiunto sotto il numero della tavola](shots/title-block-designer.webp)

Le tavole, il loro PDF, Stampa e l’emissione lo disegnano con i valori di ogni tavola. In DXF è un blocco con attributi, uno per testo vivo (`PROJECT_NAME`, `SHEET_NUMBER`…), come i cartigli di AutoCAD. **Esporta…** salva un cartiglio come file `.json`, con le sue immagini, e **Importa…** lo legge nel progetto o nella libreria, così uno studio porta i suoi cartigli da un computer all’altro. I progetti vecchi mantengono Lapin standard e si disegnano esattamente come prima.

![La tavola con il suo cartiglio](shots/sheet-title-block.webp)

## Stampare

I pulsanti **PDF** e **DXF** nella barra della tavola esportano la tavola. Un PDF è pronto da stampare o da inviare. Un DXF si apre in AutoCAD e in altri programmi CAD con i suoi layer, le sue linee e i suoi retini. **Stampa** invia la tavola a una stampante o a un plotter nel suo formato, in scala 1:1, come linee vettoriali: scegli il plotter e il formato nella finestra di stampa. Su una stampante più piccola, scegli l’adattamento alla pagina.
