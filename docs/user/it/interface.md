---
title: L’interfaccia e le scorciatoie
order: 15
summary: Le modalità Comoda, Compatta e Concentrazione, la riduzione dei pannelli laterali e le scorciatoie da tastiera che puoi modificare.
---

<!-- video interface -->

## Aree di lavoro

Un’area di lavoro mostra solo le schede della barra multifunzione di cui il tuo lavoro ha bisogno, nell’ordine in cui le usa. Sceglila dal pulsante accanto a **File**, che indica l’area di lavoro in cui sei, oppure nella schermata iniziale, in cima a **Vista → Interfaccia…** oppure da **Comandi…** (Area di lavoro: …): **Tutto** (ogni scheda della tua edizione), **Architettura**, **Strutture e armature**, **Impianti**, **Sito e opere provvisionali** o **Disegno 2D**. Il progetto resta uno solo: gli strumenti di una scheda nascosta restano in Comandi… e nelle scorciatoie da tastiera, e un modello creato in un’area di lavoro si apre intero in un’altra.

![Le aree di lavoro in Vista → Interfaccia…](shots/workspaces.webp)

Quando gli strumenti della barra multifunzione sono più larghi della finestra, la rotella del mouse sopra di essi li fa scorrere di lato.

Le schede restano su una sola riga. Quando la finestra è troppo stretta per tutte, le ultime passano sotto **Altro**, in fondo alla riga, e la scheda in cui sei resta sempre in vista.

Le schede sono raggruppate secondo il lavoro che servono. **Interni** riunisce le finiture e gli arredi. **Sito** riunisce il terreno, il paesaggio e le opere provvisionali (ponteggi e casseforme). **Esecuzione** riunisce il programma lavori e il preventivo (4D e 5D).

Mostra, nascondi o sposta una scheda sotto **Schede** nella stessa finestra di dialogo e la scelta diventa **La mia area di lavoro**, conservata su questo computer per la volta successiva; le altre aree di lavoro restano disponibili per passarvi. **Vista** resta sempre, perché contiene Interfaccia….

## Tre modalità

**Vista → Interfaccia…** (<kbd>Ctrl</kbd> <kbd>.</kbd>) sceglie quanto sta attorno alla vista. Scegli una delle tre modalità in cima alla finestra di dialogo; la modifica si vede subito.

![La finestra Interfaccia: le tre modalità, e ogni parte da sola](shots/interface-dialog.webp)

- **Comoda** è la finestra come la mostrano le altre pagine: la barra multifunzione con gli strumenti grandi e i loro nomi, una riga di comando sotto ogni disegno, e sopra la vista l’HUD, il suggerimento, i campi di lunghezza e angolo, la pianta chiave e gli strumenti rapidi.
- **Compatta** dà alla vista la maggior parte della finestra:

![Compatta: più spazio per il disegno](shots/interface-compact.webp)

1. **La barra multifunzione** è un’unica fascia di icone. Sottili linee separano i gruppi. Passa il mouse su uno strumento per vederne il nome, i tasti e il gruppo.
2. **Il browser di progetto** si riduce a una barra. Le sue icone aprono per un momento il progetto, i livelli o l’albero IFC sopra la vista.
3. **Gli strumenti rapidi** <kbd>1</kbd>–<kbd>9</kbd> stanno sul bordo sinistro della vista.
4. **La pianta chiave** è un pulsante. Fai clic per aprire la mappa, e sulla sua × per ridurla di nuovo.
5. **Un’unica riga di stato** contiene il nome dello strumento e ciò che chiede, ciò che digiti, i campi di lunghezza e angolo mentre disegni, SNAP, ORTHO e POLAR, il livello e la vista, poi il progetto.

- **Concentrazione** mantiene solo le schede e una sottile riga di comando. Fai clic su una scheda e i suoi strumenti si aprono sopra la vista; la fascia si richiude appena scegli uno strumento, oppure con <kbd>Esc</kbd> o un clic altrove. I pannelli si nascondono. Quando qualcosa è selezionato, una piccola scheda mostra che cos’è e le sue prime proprietà; **Tutte le proprietà…** apre il pannello Proprietà sopra la vista. <kbd>Tab</kbd> riporta entrambi i pannelli, e li nasconde di nuovo.

![Concentrazione: la selezione in una scheda](shots/interface-focus.webp)

In ogni modalità la barra superiore mantiene **Annulla** e **Ripeti** alla sua estremità destra: con i loro nomi in Comoda, come due frecce in Compatta e Concentrazione (<kbd>Ctrl</kbd> <kbd>Z</kbd> e <kbd>Ctrl</kbd> <kbd>Y</kbd>; il loro suggerimento dice che cosa annullano).

Sotto le modalità, ogni parte si può impostare da sola: la barra multifunzione, gli strumenti rapidi (o nessuno: i tasti numerici funzionano comunque), la riga di comando, l’HUD, il messaggio e i campi dello strumento, la pianta chiave, i pannelli e l’altezza delle righe delle proprietà. Cambiare una parte rende la scelta **Personalizzato**.

Sotto le parti, **Schede**: togli la spunta alle schede della barra multifunzione di cui fai a meno (un architetto può nascondere Struttura, Armature semplici e Impianti, un ingegnere Architettura e Sito). Vista resta sempre, perché Interfaccia… si trova lì. Gli strumenti di una scheda nascosta restano in Comandi… e nelle scorciatoie da tastiera.

Lapin conserva la tua scelta su questo computer, per te. L’app desktop e il browser conservano ciascuno la propria. Nel browser, Lapin parte in Compatta quando la finestra è alta meno di 800 pixel, finché non scegli.

## Strumenti rapidi

Gli strumenti rapidi sono i posti numerati sul bordo sinistro della vista, accanto al browser del progetto, così si spostano con lui; piccoli e semitrasparenti finché il puntatore non ci arriva sopra (View → Interface… può invece farli fluttuare in fondo alla vista); i tasti <kbd>1</kbd>–<kbd>9</kbd> li avviano. Ogni area di lavoro ha i suoi: partono come li dà la tua edizione, e li scegli tu:

- trascina uno strumento dalla barra multifunzione su un posto. Uno strumento sta in un solo posto, quindi lascia quello in cui era;
- trascina un posto su un altro per scambiarli;
- clic destro su un posto: **Scegli uno strumento…**, **Svuota questo posto** o **Ripristina gli strumenti rapidi**;
- **Vista → Strumenti rapidi…** imposta i nove posti da elenchi, e **Quelli dell’edizione** riporta quelli della tua edizione.
- uno strumento che posa elementi di un tipo (muro, porta, solaio, scala…) può tenere uno dei tipi del progetto: sceglilo nel menu contestuale del posto o accanto allo strumento in **Vista → Strumenti rapidi…**. Il posto mostra allora il nome del tipo e avvia lo strumento con quel tipo, così un posto può tenere i muri in laterizio e un altro le tramezze. Il tipo è conservato per nome: il posto funziona in ogni progetto con un tipo di quel nome, e in uno senza lo strumento prende il suo tipo attuale.

Mobili in fondo, ogni posto mostra il nome del suo strumento, e un posto vuoto dopo l’ultimo aspetta uno strumento. Lapin conserva i tuoi strumenti rapidi su questo computer, per te.

La freccia in fondo alla barra nasconde gli strumenti rapidi; al loro posto resta una piccola maniglia **Strumenti rapidi** che li riporta. Si nascondono separatamente nelle viste del modello (piante, 3D, sezioni) e nelle tavole e negli abachi, così puoi tenerli nascosti sui documenti e visibili sui disegni.

![Gli strumenti rapidi, la scala nel posto 7 e il posto 2 vuoto](shots/quick-tools.webp)

![Vista → Strumenti rapidi…](shots/quick-tools-dialog.webp)

## I miei strumenti

**I miei strumenti** è una scheda del nastro tutta tua, la prima della riga delle schede: ci metti solo gli strumenti che usi, presi da qualsiasi scheda, in gruppi che nomini tu.

- **Metterci gli strumenti.** Clic destro su uno strumento del nastro → **Aggiungi a I miei strumenti**, **Aggiungi a I miei strumenti in…** un gruppo, oppure **Aggiungi a un nuovo gruppo di I miei strumenti**. Oppure trascina uno strumento da un’altra scheda sulla scheda **I miei strumenti**: tenuto lì un momento, la scheda si apre e rilasci lo strumento dove una linea mostra che finirà.
- **Ordinarli nel nastro stesso.** Trascina uno strumento prima o dopo un altro, nel suo gruppo o in un altro gruppo; trascina un gruppo dal suo nome per spostarlo tra i gruppi. Una linea mostra dove finirà, gli altri si scostano, e rilasciare altrove non cambia nulla. Doppio clic sul nome di un gruppo per rinominarlo. Clic destro su uno strumento: **Rendi piccolo** o **Rendi grande**, **Sposta prima**, **Sposta dopo** e **Rimuovi da I miei strumenti**; clic destro sul nome di un gruppo: **Rinomina gruppo**, **Elimina gruppo**, **Nuovo gruppo** e **Annulla l’ultima modifica**. Da tastiera, <kbd>Alt</kbd> <kbd>←</kbd> o <kbd>→</kbd> sposta lo strumento attivo e <kbd>Canc</kbd> lo rimuove.
- **Personalizza I miei strumenti…** (l’ingranaggio in fondo alla scheda, View → Interface, o Commands…) mostra a sinistra tutti gli strumenti dell’applicazione, con ricerca e raggruppati per scheda, e a destra la tua scheda. Trascina gli strumenti da una parte all’altra, oppure premi **+** (o <kbd>Enter</kbd>) per aggiungerne uno; trascina nella parte destra per ordinarli; trascina uno strumento a sinistra per toglierlo. Ogni riga ha anche pulsanti per spostarla su o giù, per mostrarla come strumento grande o piccolo, un elenco per spostarla in un altro gruppo e **×** per rimuoverla. Da tastiera, <kbd>Alt</kbd> <kbd>↑</kbd> <kbd>↓</kbd> spostano lo strumento attivo (anche oltre i capi del suo gruppo), <kbd>Alt</kbd> <kbd>←</kbd> <kbd>→</kbd> lo portano nel gruppo prima o dopo, <kbd>Canc</kbd> lo rimuove e <kbd>Ctrl</kbd> <kbd>Z</kbd> annulla l’ultima modifica. **Annulla** e **Ripristina** sono in basso, quindi un rilascio sbagliato non fa perdere nulla.
- **Esporta…** e **Importa…** salvano la scheda in un piccolo file da portare su un altro computer o dare a un collega.

Lapin conserva I miei strumenti su questo computer, per te (l’app desktop e il browser separati). Uno strumento che la tua edizione non ha è nascosto, non perso: torna con il suo modulo.

![I miei strumenti](shots/my-tools-empty.webp)

![I miei strumenti](shots/my-tools-menu.webp)

![I miei strumenti: due gruppi, Muri e Aperture](shots/my-tools-tab.webp)

![View → Interface → Personalizza I miei strumenti…](shots/my-tools-dialog.webp)

## Il catalogo

La scheda **Catalogo** del browser di progetto mostra i tipi del progetto per ogni strumento che posa elementi con tipo: muri, porte, finestre, solai, facciate continue, scale, rampe, controsoffitti e battiscopa. Ogni tipo è disegnato in piccolo (gli strati di un muro come la sua sezione in pianta, quelli di un solaio in sezione, il prospetto di una porta o di una finestra, il profilo di un battiscopa), con quello che è in poche parole.

- un clic su una scheda posa elementi di quel tipo: lo strumento parte con esso;
- trascina una scheda su un posto rapido per tenervi lo strumento con quel tipo;
- **Modifica tipi…** apre la finestra in cui quei tipi si creano e si modificano.

![Il catalogo: i tipi di muro](shots/catalog.webp)

![I tipi di finestra, disegnati dal loro prospetto](shots/catalog-windows.webp)

## Ridurre i pannelli laterali

In ogni modalità, il browser di progetto e il pannello Proprietà si riducono a una sottile barra sul loro lato:

- il pulsante in alto a destra del pannello lo riduce, e la freccia sulla sua barra lo espande;
- fai doppio clic sul bordo tra il pannello e la vista;
- <kbd>Ctrl</kbd> <kbd>B</kbd> riduce o espande il browser, <kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>B</kbd> le proprietà, e <kbd>Tab</kbd> entrambi.

Fai clic su un’icona sulla barra di un pannello ridotto per aprire il pannello sopra la vista senza espanderlo. Si chiude con <kbd>Esc</kbd> o un clic altrove; la sua puntina lo tiene aperto. Trascina il bordo di un pannello per allargarlo; Lapin ricorda la larghezza.

![Un browser ridotto aperto sopra la vista dalla sua barra](shots/browser-overlay.webp)

## Pannelli su entrambi i lati

**Vista → Pannelli** (View → Panels) mette il browser di progetto e il pannello delle proprietà dove vuoi, come in Revit: **Browser a sinistra, Proprietà a destra** (predefinito), **Browser a destra, Proprietà a sinistra**, **Entrambi i pannelli a sinistra** o **Entrambi i pannelli a destra**. Con entrambi sullo stesso lato stanno uno sotto l'altro; trascina il divisore tra i due per dividere l'altezza (doppio clic la dimezza) e il bordo del lato per allargarlo. Ogni pannello si riduce ancora da solo: uno ridotto è una barra sottile nella pila, o una barra laterale quando lo sono entrambi. La scheda dell'assistente (desktop) resta con le proprietà. Lapin ricorda la disposizione su questo computer.

![Browser e proprietà impilati a sinistra](shots/panel-layouts.webp)

## Chiaro e scuro

La luna all’estremità destra della barra multifunzione rende scura l’interfaccia (grafite); il sole la rende di nuovo chiara (carta). Nel tema scuro piante, sezioni e prospetti stanno sullo sfondo scuro, con le penne nere in inchiostro chiaro; le tavole restano carta. I piccoli disegni dei configuratori (pianta, sezione e prospetto di una porta o finestra, di un tetto, un balcone, un ascensore, un capannone, una scala…) e le anteprime di etichette, cartigli e materiali sono disegnati su una scheda di carta chiara in entrambi i temi, così linee, testi e quote si leggono come su un disegno stampato.

## Più elementi insieme

Seleziona più elementi (Maiusc-clic o un riquadro) e il pannello delle proprietà mostra l’ultimo scelto, con **N selezionati** accanto alla sua classe. Un valore che hanno in comune viene mostrato; uno che differisce si legge *varia*. Cambia un campo e ogni elemento selezionato che lo ha prende il nuovo valore, in un solo passo che **Annulla** riporta indietro: l’altezza e l’offset di base dei muri, il tipo di ogni muro, porta o trave selezionato, il davanzale delle finestre, le fasi di ogni elemento selezionato qualunque sia la sua classe, il layer e il colore delle linee disegnate, lo stile dei testi. Ciò che è proprio di ogni elemento resta suo: il numero di un locale, la lunghezza di un muro, la posizione di una porta lungo il suo muro.

## Corrispondenza proprietà

**Modifica → Corrispondenza proprietà** (<kbd>MA</kbd>, o `MATCHPROP` nella riga di comando come in AutoCAD; Revit lo chiama Match Type) dà ad altri elementi ciò che ha un elemento. Seleziona l’elemento da cui copiare, oppure avvia il comando e fai prima clic su di esso; poi fai clic su ogni elemento che deve prenderne le proprietà. L’elemento sotto il cursore si illumina prima del clic, in pianta, in sezione e in 3D. Ogni clic è un passo che **Annulla** riporta indietro. **Applica alla selezione** le dà in una volta a tutti gli altri elementi selezionati. Premi <kbd>Invio</kbd> o <kbd>Esc</kbd> quando hai finito.

L’ingranaggio nella barra della riga di comando elenca cosa viene trasferito, e Lapin ricorda la tua scelta: **Tipo** (a un elemento della stessa classe), **Dimensioni e impostazioni** (altezza, offset, materiali, priorità di taglio, lo stile di un testo, la posizione del telaio e il davanzale di una porta o finestra; a un’altra classe solo i campi che ha anch’essa), **Parametri** (i tuoi; quelli di un set standard solo alla stessa classe), **Fasi**, **Layer, colore e retino** (forme disegnate) e **Nome** (spento all’inizio). La posizione di un elemento, la sua forma e il suo GlobalId non vengono mai trasferiti. Un elemento bloccato li prende comunque: il blocco tiene fermo un elemento, non le sue impostazioni.

![Corrispondenza proprietà in pianta: il muro da cui si copia, il muro sotto il cursore illuminato e cosa viene trasferito](shots/match-properties.webp)

## Scorciatoie da tastiera

**Vista → Scorciatoie da tastiera…** (<kbd>KS</kbd>) elenca ogni comando raggiungibile dalla tastiera, per scheda. Ogni riga mostra le scorciatoie del comando.

![Le scorciatoie da tastiera, cercando "wall"](shots/shortcuts-dialog.webp)

- **Cerca** per nome del comando o per tasto.
- **+** aggiunge una scorciatoia: premi una combinazione (<kbd>Ctrl</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>), oppure digita fino a tre lettere e premi <kbd>Enter</kbd>. <kbd>Esc</kbd> interrompe.
- Se un altro comando ha già la scorciatoia, Lapin dice quale. **Riassegna** la toglie a quel comando, **Mantieni entrambi** la lascia a tutti e due (vince il primo dell’elenco), **Annulla** non cambia nulla.
- **×** rimuove una scorciatoia. La freccia alla fine di una riga riporta quel comando come lo ha lo schema, e **Ripristina tutto** lo fa per ogni comando.
- **Schema** sceglie l’insieme da cui parti: **Stile Revit** (il predefinito: le lettere abituali di Revit, dove Lapin ha il comando) o **Lapin classico** (le lettere proprie di Lapin di prima).
- **Esporta…** scrive le tue scorciatoie in un file, e **Importa…** ne legge uno, per un altro computer o un collega.

I suggerimenti, i menu e la tavolozza dei comandi (<kbd>Ctrl</kbd> <kbd>K</kbd>) mostrano sempre le scorciatoie come sono in quel momento.

### Le lettere e la riga di comando

Le lettere come <kbd>WA</kbd> funzionano quando nessun campo ha il focus e la riga di comando è vuota: digitale sopra la vista. Il comando si esegue subito. Quando un comando della riga di comando inizia con le stesse lettere (<kbd>DI</kbd> e <kbd>DIMADD</kbd>), Lapin attende un attimo: continua a digitare per scrivere il comando più lungo, oppure premi <kbd>Enter</kbd> o <kbd>Space</kbd> per eseguire subito quello più corto. Mentre un comando chiede un punto o un valore, lettere e cifre vanno a esso. I comandi della riga di comando come <kbd>L</kbd>, <kbd>M</kbd> o <kbd>TRIM</kbd> seguiti da <kbd>Enter</kbd> funzionano in ogni schema. <kbd>Esc</kbd> annulla sempre.

Quando i due si scontrano, scegli come si dividono la tastiera:

- **Scorciatoie a lettere (come in Revit)**, in questo dialogo e in **Preferenze → Generale → Tastiera**, è attivo per impostazione predefinita. Disattivalo per tenere solo la riga di comando: ogni lettera va lì, e <kbd>Invio</kbd> o <kbd>Spazio</kbd> esegue un comando, un tuo alias o, se il testo non è nessuno dei due, il comando di cui sono le lettere (<kbd>WA</kbd> <kbd>Invio</kbd> avvia ancora il muro). Gli accordi (<kbd>Ctrl</kbd> + un tasto, i tasti funzione, <kbd>Canc</kbd>) e i numeri degli strumenti rapidi continuano a funzionare.
- Il tasto <kbd>`</kbd> (quello a sinistra di <kbd>1</kbd>, trovato dal suo posto su qualsiasi layout, e una scorciatoia come le altre: **Riga di comando: prende tutta la digitazione**) dà alla riga di comando tutta la digitazione, con le lettere attive o no. La riga è incorniciata e lo dice; nessuna lettera avvia uno strumento, nessun numero sceglie uno strumento rapido. <kbd>Invio</kbd> esegue quanto digitato e restituisce la digitazione, a meno che un comando chieda poi un punto o un valore: allora la modalità resta finché il comando finisce. <kbd>Esc</kbd> o lo stesso tasto la lascia.

### Alias dei comandi

La pagina **Alias dei comandi** del dialogo tiene i tuoi nomi per i comandi. Scrivi da 1 a 12 lettere o cifre (la prima una lettera), trova il comando nell'elenco (un comando della riga di comando come <kbd>LINE</kbd> o un qualsiasi comando del nastro o dell'app) e premi **Aggiungi alias**. Scrivi l'alias nella riga di comando e premi <kbd>Invio</kbd> o <kbd>Spazio</kbd>. I tuoi vengono prima dei nomi incorporati. La pagina dice cosa sostituisce un alias: un nome incorporato della riga di comando, una scorciatoia con le stesse lettere o una scorciatoia che fa attendere (con le lettere attive, una scorciatoia che è un alias, o l'inizio di uno, attende un attimo così puoi continuare a scrivere e premere <kbd>Invio</kbd>). Gli alias si conservano con le tue scorciatoie, anche in **Esporta…** e **Importa…**.

![Alias dei comandi, con ciò che un alias copre](shots/shortcuts-aliases.webp)

### Le scorciatoie predefinite

Queste sono le scorciatoie Stile Revit, come le ha Lapin oggi:

<!-- shortcuts -->

I tasti numerici <kbd>1</kbd>–<kbd>9</kbd> scelgono gli strumenti rapidi.

## Spostare una finestra di dialogo

Ogni finestra di dialogo si sposta dalla barra del titolo, come una finestra: premi sul titolo e trascina, con il mouse o con il dito. Resta dentro la finestra, si riapre dove l'hai lasciata finché il programma è aperto, e un doppio clic sul titolo la riporta al centro. Così puoi scostarla dalla parte del modello su cui lavori o guardare il modello dietro di essa.
