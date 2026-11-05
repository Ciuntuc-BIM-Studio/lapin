---
title: Documenti
order: 105
summary: Gli elaborati scritti del progetto — la relazione tecnica, il capitolato, l’elenco elaborati — fatti da modelli, compilati dal modello BIM ed esportati in DOCX, ODT e PDF senza nessun altro programma.
---

## Che cos’è un documento

Un progetto non è fatto solo di tavole: ha anche i suoi elaborati scritti, una relazione tecnica, un capitolato, un elenco elaborati. In Lapin sono **documenti**: ognuno parte da un modello e si compila dal progetto — i suoi dati, le superfici, i piani e i locali, i tipi di muro, gli abachi e le tavole. Quello che sai solo tu (la struttura, la sicurezza antincendio, l’organizzazione del cantiere) lo scrivi negli input del documento. Un documento segue il modello BIM: cambia un tipo di muro o aggiungi un locale, e il documento lo dirà la prossima volta che lo apri.

Lapin scrive da sé i file, in DOCX, ODT e PDF, senza Word né LibreOffice sul computer, nell’app desktop e nel browser.

## Informazioni sul progetto

**Gestisci → Informazioni sul progetto…** contiene ciò che i documenti chiedono al progetto: nome, numero e fase, il committente e il suo indirizzo, l’indirizzo del sito, il numero catastale e il libro fondiario, il progettista e il suo indirizzo, l’architetto capo, chi ha disegnato e chi ha verificato. Scrivili una volta: ogni documento li legge, e i cartigli delle tavole prendono da qui chi ha disegnato e chi ha verificato quando una tavola non lo dice. Ogni campo si salva quando lo lasci.

![Informazioni sul progetto: numero, fase, committente, sito e progettista](shots/project-info.webp)

Gli stessi campi sono nella scheda **Informazioni sul progetto** della finestra Documenti.

## Creare un documento

**Gestisci → Documenti…** (o il **+** accanto a **Documenti** nel browser di progetto) apre i documenti del progetto. A sinistra ci sono i documenti già fatti e, sotto **Nuovo da modello**, i modelli; fai clic su uno e il nuovo documento viene creato e compilato subito.

![Documenti: i documenti del progetto e i modelli, il documento sulle sue pagine, i suoi input e ciò che resta da completare](shots/documents.webp)

Al centro c’è il documento come verrà stampato, pagina dopo pagina, con l’indice, le tabelle e i numeri. Un campo che il progetto non ha ancora è segnato dove si trova. Il browser di progetto elenca i documenti sotto **Documenti**; un clic apre la finestra su uno di essi.

## Input

La scheda **Input** ha i testi che solo il progettista può scrivere, raggruppati come i capitoli del documento. Nei modelli rumeni ognuno inizia con un’indicazione segnata **[De completat]** (da completare) che dice cosa va scritto. Scrivici sopra: l’anteprima segue un attimo dopo che smetti di digitare. **Ripristina predefinito** riporta il testo del modello.

![Input: la struttura scritta sopra l’indicazione del modello](shots/documents-inputs.webp)

## Da completare

**Da completare** elenca ciò che manca ancora al documento: un campo del progetto vuoto, un valore che il modello BIM non ha. Fai clic su una voce per vedere dove si trova nel documento e andare al campo che la compila (un campo del progetto apre la scheda **Informazioni sul progetto**). L’elenco si svuota man mano che lo completi.

![Da completare: i campi che mancano ancora al documento](shots/documents-missing.webp)

## Aggiornare, rinominare, copiare

- **Aggiorna dal modello** riprende il testo e l’impaginazione del modello, per un documento fatto prima che il modello cambiasse. I tuoi input restano.
- **Rinomina**, **Duplica** ed **Elimina** agiscono sul documento mostrato. Un’eliminazione è un solo passo di annullamento.

## Modificare un documento

**Modifica**, in basso nella finestra Documenti, trasforma la pagina al centro in un editor del testo proprio del documento; **Anteprima** lo mostra di nuovo sulle sue pagine. Quello che cambi appartiene solo a questo documento: il modello resta com’era (**Salva come modello…**, più sotto, tiene le tue modifiche come modello).

![Modificare un documento: i campi come etichette con i loro valori, una tabella del progetto selezionata e le sue impostazioni a destra](shots/documents-editor.webp)

- **La barra degli strumenti** ha gli stili del testo: **Paragrafo**, **Titolo 1**–**3**, grassetto, corsivo, elenchi puntati e numerati, annulla e ripeti. Funzionano anche i tasti consueti (Ctrl+B, Ctrl+I, Ctrl+Z nell’editor).
- **I campi** appaiono come etichette con il loro nome e il valore attuale, per esempio il committente, *Popescu Ion și Maria*. Un campo che il progetto non compila ancora è arancione.
- **I blocchi del progetto** — una tabella, un’immagine, l’indice — sono disegnati con il loro contenuto reale. Un clic ne seleziona uno: le sue impostazioni appaiono a destra, al posto delle schede.

Il salvataggio è automatico: un attimo dopo che smetti di scrivere e quando torni ad **Anteprima**; ogni modifica è un passo di annullamento del progetto.

### Inserisci

**Inserisci** mette al cursore:

- **Campo…**: un campo del progetto, cercato per nome; l’elenco mostra il valore di ciascuno. Dentro una ripetizione sono offerti anche i campi dei suoi elementi (il nome di un piano, la superficie di un locale).

  ![Inserisci → Campo…: “client” cercato, i campi con i loro valori](shots/documents-insert-field.webp)

- **Tabella dal progetto…**: un abaco o un elenco del progetto (l’abaco dei serramenti, i locali, i materiali…). Nelle sue impostazioni: l’origine, il **Titolo**, le colonne mostrate e la riga del totale.
- **Immagine di una vista…**: una pianta, una sezione, un prospetto o una vista 3D. Nelle sue impostazioni: **Scala 1:** o **Larghezza (mm)** (mai più larga del testo) e una **Didascalia**.
- **Campi chiave–valore**: una tabella a due colonne di etichette e valori; un valore può contenere campi, come `{{project.name}}`.
- **Ripeti per…**: il testo all’interno è scritto una volta per ogni elemento di una raccolta: i piani, i locali, le coperture, i materiali, le tavole, i documenti… Una fascia sopra dice **Per ogni** e quanti elementi ci sono; le sue frecce **‹ ›** cambiano l’elemento che l’editor mostra mentre scrivi. Nelle impostazioni, **Solo gli elementi dove** ne tiene alcuni, e **Testo quando non ce n’è nessuno** aggiunge ciò che si scrive quando non ce n’è nessuno.
- **Condizione…**: il testo all’interno è scritto solo quando un campo ha un valore (o, spuntato, solo quando è vuoto). **Altrimenti: testo quando non è soddisfatta** aggiunge una seconda parte, scritta al suo posto.
- **Testo da completare…**: un nuovo input, con etichetta, gruppo e testo predefinito; il suo testo lo scrivi nella scheda **Input**, in anteprima.
- **Indice** e **Interruzione di pagina**.

![Una ripetizione sui piani, una condizione al suo interno e i locali del piano mostrato, cambiato con ‹ ›](shots/documents-repeat.webp)

Nelle impostazioni di un blocco, **Rimuovi** lo toglie; per una ripetizione o una condizione, **Mantieni solo il contenuto** toglie la fascia e tiene ciò che contiene.

## I tuoi modelli

**Salva come modello…** tiene il testo, l’impaginazione, gli input e lo stile del documento come modello. Dagli un titolo e una descrizione e scegli dove conservarlo:

- **Questo progetto**: va con il file del progetto, a chiunque lo apra.
- **La libreria di questo computer**: lo offre ogni progetto su questo computer.
- **Entrambi**.

**Salva come** può sostituire un modello salvato prima; i documenti già fatti da esso tengono il loro testo fino ad **Aggiorna dal modello**. Il nuovo modello compare subito sotto **Nuovo da modello**.

![Salva come modello: il titolo, la descrizione e dove conservarlo](shots/documents-save-template.webp)

**Gestisci → Modelli di documento…** mostra i modelli in tre gruppi: **Integrati** (quelli di Lapin), **Questo progetto** e **Questo computer**. Per quello scelto:

- **Rinomina** ed **Elimina** (eliminare un modello del progetto è un passo di annullamento; i documenti fatti da esso tengono il loro testo).
- **Duplica nel progetto** o **Duplica nella libreria**: un modello integrato si cambia solo attraverso una copia.
- **Copia nella libreria** o **Copia nel progetto**, per spostarlo da un posto all’altro.
- **Esporta…** scrive il modello come file `.json`; **Importa…** ne legge uno in questo progetto o nella libreria di questo computer. Così i colleghi condividono i modelli di uno studio.

![Modelli di documento: gli integrati, quelli del progetto e quelli del computer, quello scelto con il suo stile](shots/document-templates.webp)

## Lo stile dello studio

Un documento, e un modello del progetto o della libreria, ha il suo stile: la scheda **Stile** della finestra Documenti per il documento mostrato, la parte **Stile** di Gestisci → Modelli di documento… per un modello. I modelli integrati tengono l’aspetto di Lapin: duplicane uno per dargli il tuo.

- **Carattere (DOCX, ODT)**: Arial (quello di Lapin), Calibri, Times New Roman, Georgia, Verdana o Liberation Sans.
- **Carattere del PDF**: Helvetica o Times, i caratteri propri del PDF; per impostazione predefinita il più vicino al carattere sopra.
- **Dimensione del testo (pt)**: la dimensione del testo corrente, da 6 a 24 pt.
- **Colore dei titoli**.
- **Logo nell’intestazione**: **Scegli il logo…** prende un PNG o un JPEG (sotto 1 MB); imposta la sua **Larghezza (mm)** e se sta **A sinistra** o **A destra**. È scritto allo stesso modo in DOCX, ODT e PDF.

Un campione sotto i campi mostra la parte alta della pagina come la mostreranno i file. **L’aspetto proprio di Lapin** toglie lo stile. Ogni modifica è un passo di annullamento.

![La scheda Stile: il colore dei titoli e un logo nell’intestazione, con il campione](shots/documents-style.webp)

## Esportare

- **Esporta DOCX** ed **Esporta ODT** scrivono un file che apri e modifichi in Word o LibreOffice, con i titoli come stili propri del programma, l’indice e la numerazione delle pagine come campi, e le immagini del progetto.
- **Esporta PDF** scrive il documento impaginato da Lapin, pronto da stampare o inviare.

Un campo ancora vuoto resta vuoto nei file: controlla quindi **Da completare** prima di esportare.

## Emettere i documenti

**Gestisci → Emetti…** porta i documenti insieme alle tavole e al modello (vedi [Cartella di progetto ed emissioni](cde.md)). Sotto **Documenti, ciascuno come PDF:** ogni documento è spuntato fin dall’inizio; spunta **Anche come DOCX** per avere anche una copia modificabile. Ogni file prende il nome secondo la convenzione ISO 19650 (`RP` per una relazione, `SP` per un capitolato), ne viene calcolato l’hash ed è elencato nella distinta di trasmissione, e il documento prende la revisione e lo stato dell’emissione.

![Emetti: i documenti insieme alle tavole e al modello](shots/issue-documents.webp)

## I modelli

Lapin ha tre modelli, per ora in rumeno:

- **Memoriu tehnic de arhitectură** (la relazione tecnica di architettura, secondo la legge rumena 50/1991): i dati generali da Informazioni sul progetto; il numero di piani (per esempio `P+1E`) e l’altezza massima; le superfici — coperta, lorda, utile e dell’interrato — in totale e per piano; i locali di ogni piano con le loro aree; i muri per tipo, con i loro strati; i solai e le coperture; l’abaco dei serramenti; le finiture dei locali; input per lo stato di fatto, la funzione, la struttura, le facciate, gli impianti, la sicurezza antincendio, l’accessibilità, l’efficienza energetica e l’organizzazione del cantiere.
- **Caiet de sarcini: tâmplărie și finisaje** (il capitolato per serramenti e finiture): i materiali del progetto per categoria, l’abaco dei serramenti con le quantità, le finiture locale per locale, e input per le generalità, i requisiti, l’esecuzione e il collaudo, con testi predefiniti da adattare.
- **Borderou de piese scrise și desenate** (l’elenco degli elaborati scritti e grafici): i documenti e le tavole del progetto, con numero, titolo, scala e revisione.

Parti da uno di essi, cambialo nell’editor, poi **Salva come modello…** per avere il tuo.

## Per gli assistenti IA

Un assistente collegato tramite il ponte MCP di Lapin può creare un documento, scriverne gli input ed esportarlo con `lapin_document_export`: vedi [Lapin and AI agents](https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/mcp.md#documents) (in inglese).
