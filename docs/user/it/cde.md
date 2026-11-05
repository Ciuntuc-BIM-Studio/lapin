---
title: Cartella di progetto ed emissioni
order: 125
summary: Emetti le tavole e il modello come chiede la ISO 19650, conservati nel file di progetto e, quando ne hai una, in una cartella di progetto, denominati secondo la convenzione, con una lettera di trasmissione, dopo le verifiche che fa uno studio tecnico.
---

## La cartella di progetto

**Gestisci → Cartella di progetto** imposta la cartella in cui il progetto viene anche emesso: una cartella sul tuo computer, una condivisione di rete o una cartella SharePoint o OneDrive sincronizzata sul disco. La cartella è **facoltativa**: ogni emissione e i suoi file sono conservati nel file di progetto stesso, quindi un progetto senza cartella si emette lo stesso, nell’app desktop e nel browser. Lapin non chiede alcun account e non comunica con alcun server: scrive file, e ciò che sincronizza la cartella è affare del tuo studio. Il percorso è conservato nel progetto, quindi viaggia con il file; una cartella che non si trova su questo computer è un avviso, non un errore. Per impostare una cartella serve l’app desktop.

![Cartella di progetto: la cartella, i contenitori, la denominazione](shots/project-folder.webp)

Dentro la cartella Lapin crea alla prima emissione i quattro contenitori della ISO 19650: **WIP**, **Shared**, **Published** e **Archived**. Un codice di stato suggerisce il contenitore: S0 va in WIP, S1–S7 in Shared, A, B e CR in Published.

## Denominazione

I file sono denominati secondo la ISO 19650-2: **Progetto-Autore-Volume-Livello-Tipo-Ruolo-Numero**, poi lo stato e la revisione, per esempio `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. Nella finestra di dialogo Cartella di progetto imposti il codice del progetto, l’autore (il tuo studio), il volume, e il livello e il ruolo predefiniti. Il resto viene dal progetto:

- il **livello** di una tavola è il piano che disegnano le sue piante (`00` a ±0.00, `01` sopra, `B1` sotto, `ZZ` quando ne disegna più d’uno o nessuno);
- il suo **ruolo** è quello della sua disciplina (architettura A, struttura S, impianti M, civile C);
- il suo **numero** sono le cifre del suo numero di tavola (`A-101` diventa `0101`);
- il modello è `M3`, la lettera di trasmissione `IE`.

## Verifiche

**Gestisci → Verifica** applica a tutto il progetto le regole che uno studio tecnico controlla a occhio prima di un’emissione. Un problema **bloccante** ferma un’emissione; un **avviso** può essere ignorato. Fai clic su un problema per andare alla tavola, alla vista o all’elemento.

![Verifica: i problemi per regola](shots/cde-check.webp)

Bloccanti: un cartiglio senza numero, senza titolo o senza chi l’ha disegnato; una vista senza scala; una vista fuori dalla cornice o sopra il cartiglio; una tavola che è cambiata dalla sua ultima emissione ma è ancora a quella revisione; un numero di tavola che non dà alcun numero di file. Avvisi: chi ha verificato, la data e la fase lasciati vuoti; una pianta colorata secondo uno schema dei locali, o con molti retini, senza legenda; una pianta, una sezione o un prospetto su nessuna tavola; una planimetria senza freccia del nord; elementi su nessun piano, senza tipo o senza materiale; porte e finestre senza etichetta; locali senza numero; le proprietà IFC richieste (IsExternal, LoadBearing, FireRating sulle porte) lasciate vuote.

## Emettere

**Gestisci → Emetti…** crea un’emissione, conservata nel file di progetto e, quando il progetto ha una cartella su questo computer, scritta anche nel suo contenitore:

![Emissione: le tavole, la revisione, lo stato, le verifiche](shots/cde-issue.webp)

1. Dai all’emissione un nome, la sua **revisione** (`P01`, `P02`… finché è preliminare, `C01`… una volta contrattuale; la successiva viene proposta a partire dall’ultima emissione), il suo **stato** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) e il **contenitore**.
2. Spunta le tavole che partono, il modello come IFC, ogni tavola anche come DXF, oppure anche tutte le tavole in un unico PDF. Il nome di ogni file è mostrato come verrà scritto. L’IFC viene conservato nel file di progetto solo quando spunti **Conserva l’IFC nel file di progetto** (un modello grande pesa decine di MB a emissione); se non è spuntato, va nella cartella di progetto e il file di progetto ne conserva il nome e l’hash. **Anche nella cartella di progetto** scrive i file anche nel contenitore (attivo per impostazione predefinita quando il progetto ha una cartella). Partono anche i documenti scritti del progetto, ciascuno come PDF e, a richiesta, come DOCX (vedi [Documenti](documents.md)).
3. Le verifiche vengono eseguite mentre scegli. Con un problema bloccante l’emissione non può partire; con soli avvisi spunta **Emetti comunque**.
4. **Emissione** produce ogni tavola come PDF, l’IFC e il resto, denominati secondo la convenzione, calcola l’hash di ogni file (SHA-256), crea la **lettera di trasmissione** (un PDF e un CSV: progetto, emissione, data, revisione, stato, i file) e un **manifest** (JSON, con ogni hash), li conserva tutti nel file di progetto e registra l’emissione nel progetto. Ogni tavola stampata porta nel cartiglio la revisione e lo stato dell’emissione e, nella sua tabella delle revisioni, la riga dell’emissione, così come è stata emessa.
5. Nello stesso passo il progetto viene salvato come **versione** con il nome dell’emissione (vedi [File, versioni e ripristino](files.md)), così un’emissione si può riaprire, ripristinare o confrontare con il progetto com’è ora, e il progetto viene salvato, non lasciato con modifiche non salvate. La finestra di dialogo mostra poi l’emissione come è partita (il suo nome, la sua revisione e i suoi file, ciascuno da aprire) finché non la chiudi o avvii una **Nuova emissione**.

![La finestra Emissione dopo un’emissione: l’emissione come è partita, i suoi file](shots/cde-issued.webp)

## Le emissioni nel file di progetto

Le emissioni sono elencate in fondo alla finestra di dialogo Cartella di progetto, con lo spazio che occupano nel file di progetto (i file identici sono conservati una sola volta, per quante emissioni li condividano). Fai clic su un’emissione per vederne i file:

![Le emissioni nel file di progetto: lo spazio che occupano, un’emissione aperta con i suoi file](shots/cde-issues.webp)

- fai clic su un file per **aprirlo** dal file di progetto (il desktop lo apre con il suo programma; il browser lo scarica), oppure **Salva una copia…**;
- **Scarica come zip** dà tutti i file conservati di un’emissione in un unico zip, con il nome della sua lettera di trasmissione: ciò che invii quando non c’è una cartella condivisa;
- **Pubblica in cartella…** (l’app desktop) scrive un’emissione fatta in precedenza nel contenitore della cartella di progetto, o in una cartella qualsiasi a tua scelta: prima emetti, poi scegli la cartella;
- **Elimina i file conservati** fa uscire i file di una vecchia emissione dal file di progetto al prossimo salvataggio, per tenere il file leggero; il registro dell’emissione, i nomi dei suoi file e i loro hash restano;
- **Apri cartella** mostra il contenitore in cui è stata scritta l’emissione.

## Nel browser

Il CDE funziona anche nel browser: Verifica, Emissione, i file delle emissioni (aprire, Scarica come zip) ed entrambi i tipi di osservazioni. Il file di progetto contiene tutto, e ti torna come il file che salvi. Una cartella di progetto, la pubblicazione in essa e il suo esame richiedono l’app desktop.

## Osservazioni (BCF)

**Gestisci → Osservazioni sul modello…** conserva le osservazioni fatte sul modello, tue e di un revisore, come topic BCF (il BIM Collaboration Format che si scambiano BIMcollab, Solibri, Navisworks e Revit), nel progetto, mai nell’IFC. Un’osservazione ha un titolo, una descrizione, un tipo, uno stato (Open, In progress, Resolved, Closed), una priorità, la persona a cui è assegnata, una scadenza ed etichette; i suoi commenti; e i suoi punti di vista: la camera 3D, gli elementi selezionati, il box di sezione come piani di taglio, e un’istantanea.

![Osservazioni: l’elenco con i suoi filtri, un’osservazione con la sua istantanea e i suoi commenti](shots/remarks.webp)

- **Nuova osservazione** ne crea una dalla vista 3D in primo piano: la camera così com’è, gli elementi selezionati, il box di sezione e un’immagine presa dalla vista. Compila i campi e fai clic su **Crea**.
- L’elenco si filtra per testo, stato, priorità, persona a cui è assegnata, e **Solo aperte**. Scegli un’osservazione per vederne e modificarne i campi, leggerne i commenti e rispondere nella casella (**Commenta**; spunta **Con la vista 3D** per allegare alla risposta la vista così com’è).
- **Mostra in 3D** apre il punto di vista dell’osservazione: la vista 3D va alla sua camera, i suoi elementi vengono selezionati (quelli che il progetto ha, tramite il loro GlobalId IFC), e i suoi piani di taglio diventano il box di sezione. Un commento con una vista propria ha accanto **Mostra**.
- **Riprendi dalla vista 3D** sostituisce il punto di vista dell’osservazione con la vista com’è ora.
- **Importa BCF…** legge un file `.bcfzip` (BCF 2.1) o `.bcf` (BCF 3.0): un topic che il progetto ha già (lo stesso GUID) viene aggiornato (vincono i campi del file, e i commenti fatti qui che il file non ha restano), gli altri vengono aggiunti. **Esporta tutte**, **Esporta aperte** o **Esporta selezionata** scrivono un file BCF, 2.1 a meno che tu non scelga 3.0, da consegnare al revisore o da mettere nel contenitore Shared della cartella di progetto. I topic mantengono i loro GUID, quindi un’andata e ritorno attraverso un altro strumento torna alle stesse osservazioni.

![Un’osservazione aperta al suo punto di vista nella vista 3D](shots/remark-viewpoint.webp)

Il tuo nome su ciò che scrivi si imposta nella casella **Il tuo nome** della finestra di dialogo (è conservato su questo computer).
## Osservazioni sulle tavole

Un revisore apre un PDF emesso in Acrobat, Bluebeam, Anteprima o Drawboard e lo annota: note adesive, testo, evidenziazioni e barrature, tratti di penna, rettangoli, cerchi, frecce, nuvolette, timbri, misure, risposte. **Gestisci → Osservazioni sulle tavole…** riporta quelle annotazioni nel progetto come osservazioni sulla tavola:

![Osservazioni: l’elenco per tavola, un PDF letto](shots/cde-remarks.webp)

- **Leggi osservazioni da PDF…** sceglie il file. La tavola a cui appartiene viene dall’emissione (il manifest accanto al file, o il registro delle emissioni del progetto stesso), altrimenti dalla convenzione del nome del file; quando niente corrisponde, scegli tu la tavola. Le pagine di un PDF che raccoglie più tavole vanno alle sue tavole in ordine. Viene mostrato ciò che è stato trovato (pagine, annotazioni, come è stata abbinata la tavola, eventuali avvisi) e **Importa** porta dentro le osservazioni. Rileggere lo stesso file ne aggiorna le osservazioni invece di raddoppiarle; un’osservazione che hai risolto resta risolta. Anche un file rinominato lungo la strada viene abbinato: i byte emessi sono il punto da cui comincia il file del revisore.
- **Esamina la cartella di progetto** (l’app desktop) elenca i PDF emessi in Shared e Published, quali di essi sono cambiati dopo la loro emissione, e se le loro osservazioni sono state lette da allora.
- L’elenco è raggruppato per tavola e filtrato per stato: **aperte**, **risolte**, **chiuse**. Un clic apre la tavola con l’osservazione in primo piano. Ogni osservazione si può risolvere, chiudere, riaprire o eliminare; lo stato "Completed" o "Accepted" di un revisore arriva come risolta.

![Una tavola con le sue osservazioni sopra](shots/cde-remarks-sheet.webp)

Sulla tavola le osservazioni sono disegnate sopra la carta al loro posto: il contorno nel colore di revisione, un indicatore con le iniziali dell’autore, il testo in un tooltip; quella in primo piano ha la sua scheda con il testo, chi e quando, le risposte e lo stato. Il pulsante nella barra degli strumenti della tavola le nasconde o le mostra, e il browser di progetto conta quelle aperte accanto alla tavola. Sono una sovrapposizione solo per lo schermo: una tavola si stampa e si emette senza le sue osservazioni.

Lapin legge il PDF da sé, senza alcun altro programma: file classici e quelli salvati con flussi di riferimenti incrociati, salvataggi incrementali (ciò che aggiunge il programma del revisore), testo in qualsiasi codifica. Un file che non riesce a leggere per intero restituisce ciò che può, con un avviso. Lapin non scrive mai nel PDF.
