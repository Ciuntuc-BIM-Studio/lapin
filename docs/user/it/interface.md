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

## Chiaro e scuro

La luna all’estremità destra della barra multifunzione rende scura l’interfaccia (grafite); il sole la rende di nuovo chiara (carta). Nel tema scuro piante, sezioni e prospetti stanno sullo sfondo scuro, con le penne nere in inchiostro chiaro; le tavole restano carta. I piccoli disegni dei configuratori (pianta, sezione e prospetto di una porta o finestra, di un tetto, un balcone, un ascensore, un capannone, una scala…) e le anteprime di etichette, cartigli e materiali sono disegnati su una scheda di carta chiara in entrambi i temi, così linee, testi e quote si leggono come su un disegno stampato.

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

### Le scorciatoie predefinite

Queste sono le scorciatoie Stile Revit, come le ha Lapin oggi:

<!-- shortcuts -->

I tasti numerici <kbd>1</kbd>–<kbd>9</kbd> scelgono gli strumenti rapidi.
