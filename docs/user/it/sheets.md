---
title: Tavole, PDF e DXF
order: 100
summary: Disponi le viste sulle tavole con un cartiglio e stampale in PDF o DXF.
---

<!-- video sheets -->

## Creare una tavola

Fai clic su **+** accanto a **Tavole** nel browser di progetto. Per posizionare una vista, trascinala dal browser sulla tavola, oppure sceglila in **Posiziona vista…** nella barra della tavola. Ogni vista mantiene la propria scala e resta collegata al modello, quindi la tavola cambia quando cambia il modello. Sulla tavola, trascina una vista per spostarla: la vista si sposta da sola, con il suo titolo, mentre la cornice, il cartiglio e le altre viste restano al loro posto; si aggancia ai bordi e al centro della cornice e alle altre viste, e i tasti freccia la spostano di 1 mm (10 mm con Shift).

![Una tavola A3 con una pianta e una sezione](shots/sheet.webp)

Seleziona la tavola per compilare il suo **cartiglio** nel pannello Proprietà: il progetto, il committente, chi l’ha disegnata, chi l’ha controllata, la data e la revisione. Lì si impostano anche il numero, il formato e l’orientamento. Il formato è un A (da A0 ad A4), uno dei fogli allungati ISO (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), un formato nordamericano (Letter, Tabloid, ARCH D, ARCH E) o **personalizzato**, con la sua **Larghezza del foglio** e **Altezza del foglio** in millimetri. Se lo lasci vuoto, il campo del progetto mostra il nome del progetto: il nome dato al progetto, altrimenti il nome del suo file.

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

## Disegnare sulla tavola

Note, segni e legende che appartengono alla tavola e non a una delle sue viste si disegnano sulla tavola stessa. Fai clic su **Annota** nella barra della tavola, oppure fai doppio clic sul foglio fuori dalle viste (**Annota → Annota tavola** fa lo stesso). La tavola si apre nel pannello di disegno come il suo foglio, in scala 1:1, con le viste, la cornice e il cartiglio sotto, e la barra dice *disegno sulla tavola*. Ogni strumento di **Disegno** e **Annota** funziona lì come su una vista, in millimetri di carta: digita 50 e una linea è lunga 50 mm sulla carta, e l'altezza di un testo è la sua altezza sulla carta. I punti si agganciano alla cornice, al cartiglio, ai riquadri delle viste (blu tratteggiato) e a ciò che le viste disegnano. **Tavola** nella barra torna alla disposizione delle viste.

- I **testi** possono contenere campi dinamici, come quelli di un cartiglio: `Tavola {sheet.number} di {sheet.count}`, `{project.name}`, `{sheet.date}`. Su una vista un testo resta come è stato scritto.
- **Nota con richiamo** (Annota → Testo): fai clic dove punta la freccia, poi dove va il testo, e scrivilo. Spostata, la nota porta con sé la freccia; **Linea di richiamo → rimuovi** nelle sue proprietà toglie la freccia. Selezionata, la punta della freccia e ogni piega hanno maniglie proprie: trascinane una per spostarla da sola.
- **Linee, polilinee, rettangoli, cerchi, archi, retini, simboli e immagini** sono quelli degli strumenti di Disegno, con i loro grip, layer e strumenti di modifica. Una freccia del nord è uno dei simboli di Lapin (**Annota → Simbolo**). I simboli posizionati con **Posiziona simbolo…** nella barra della tavola si prendono anche qui con un clic e si spostano in millimetri di carta.
- **Nuvola di revisione** (Disegno → Riempimento, Annota → Tavole o REVCLOUD nella riga di comando): due angoli, oppure **P** per un poligono chiuso con Invio. **Nuvola di revisione** nelle proprietà di qualsiasi polilinea chiusa o cerchio la trasforma in nuvola o la riporta alla forma semplice, e ne fissa l'arco sulla carta.
- **Legenda** (Annota → Tavole): un elenco dinamico di ciò che disegnano le viste della tavola: i materiali con cui sono retinate e riempite le loro sezioni oppure, con **Liste** nelle sue proprietà, le loro linee per ruolo, tipo e spessore. Cambia con le viste e con il modello, e anche il suo titolo accetta campi. Le sue parole (l’intestazione, i ruoli e i tipi delle linee) sono scritte nella lingua che aveva l’interfaccia quando è stata posizionata; **Lingua** nelle proprietà le cambia.
- **Scala grafica** (Annota → Tavole): alla scala della prima vista della tavola o a quella data nelle sue proprietà, con una lunghezza tonda di circa 80 mm o una scelta da te. Su una pianta, una sezione o un prospetto, la scala grafica prende la scala della vista.

Ciò che è disegnato sulla tavola si stampa su di essa, sopra le viste, esattamente come appare: nel suo PDF, in Stampa e nel suo DXF. Appartiene alla tavola: **Duplica la tavola** (il suo pulsante nel browser del progetto) lo copia, insieme ai simboli posati sulla tavola, ed eliminare la tavola lo elimina. Ogni disegno è un passo di annullamento. Le tavole fatte prima non hanno nulla disegnato e si stampano come prima. Un’emissione lo conta: una tavola cambiata solo da ciò che è disegnato sulla sua carta o dai suoi simboli è segnalata come cambiata dall’ultima emissione (Gestisci → Verifica, CDE).

![Disegno su una tavola: una nota con richiamo, una nuvola di revisione, un testo dinamico, una legenda dei materiali della pianta e una scala grafica](shots/sheet-annotations.webp)

![La stessa tavola, come viene stampata](shots/sheet-annotated.webp)

## Stampare

I pulsanti **PDF** e **DXF** nella barra della tavola esportano la tavola. Un PDF è pronto da stampare o da inviare. Un DXF si apre in AutoCAD e in altri programmi CAD con i suoi layer, le sue linee e i suoi retini; cerchi, archi ed ellissi (disegnati, o i bordi curvi di solai e altri contorni in pianta) sono scritti come vere entità CIRCLE, ARC ed ELLIPSE, non come linee corte, e tornano come curve quando il DXF viene reimportato. **Stampa** invia la tavola a una stampante o a un plotter nel suo formato, in scala 1:1, come linee vettoriali: scegli il plotter e il formato nella finestra di stampa. Su una stampante più piccola, scegli l’adattamento alla pagina.
