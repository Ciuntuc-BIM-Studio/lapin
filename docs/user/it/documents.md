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

Un editor visuale per i tuoi modelli arriverà più avanti.
