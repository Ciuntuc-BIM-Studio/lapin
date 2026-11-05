---
title: Disegno e riga di comando
order: 80
summary: Digita i comandi come in CAD, aggancia i punti con lo snap, sposta e copia, disegna linee e annota con quote, testi ed etichette.
---

## La riga di comando

Digita le lettere di uno strumento in qualsiasi punto della finestra, poi premi <kbd>Enter</kbd>. Per esempio, digita <kbd>WA</kbd> per un muro, <kbd>CO</kbd> per copiare, <kbd>M</kbd> per spostare o <kbd>L</kbd> per una linea. La riga di comando in basso mostra ciò di cui lo strumento ha bisogno dopo. Puoi fare clic su un punto o digitare una lunghezza, un angolo o delle coordinate.

Le coordinate digitate sono nell’unità del progetto, come le lunghezze accanto al cursore: in un progetto in millimetri, `@4000,4000` dopo il primo angolo di un rettangolo lo rende di 4 m per 4 m, e `4000<90` è 4 m dritto verso l’alto.

![Copia di un muro: dopo il punto base, la riga di comando e l’input dinamico mostrano la lunghezza e l’angolo fino al mouse](shots/command-line.webp)

Ogni strumento mostra le sue lettere nel suo suggerimento; puoi cambiarle in **Vista → Scorciatoie da tastiera…** (vedi [L’interfaccia e le scorciatoie](interface.md)). Queste sono le più usate:

| Modello | | Disegno | |
|---|---|---|---|
| Muro | <kbd>WA</kbd> | Linea | <kbd>L</kbd> |
| Porta, finestra | <kbd>DR</kbd>, <kbd>WN</kbd> | Polilinea | <kbd>PL</kbd> |
| Solaio | <kbd>SB</kbd> | Rettangolo, cerchio, arco | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Tetto | <kbd>RF</kbd> | Sposta, copia | <kbd>M</kbd>, <kbd>CO</kbd> |
| Scala, locale | <kbd>ST</kbd>, <kbd>RM</kbd> | Ruota, specchia, scala | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Pilastro, trave | <kbd>CL</kbd>, <kbd>BM</kbd> | Offset, taglia, estendi | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Sezione | <kbd>SE</kbd> | Raccordo, smusso | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Misura | <kbd>ME</kbd> | Retino, testo | <kbd>H</kbd>, <kbd>T</kbd> |
| Adatta tutto | <kbd>ZE</kbd> | Cancella | <kbd>E</kbd> |

## Snap e aiuti

Lapin aggancia con lo snap le estremità, i punti medi e le intersezioni di ciò che è disegnato, e la griglia. <kbd>F3</kbd> attiva o disattiva lo snap, <kbd>F8</kbd> l’orto e <kbd>F10</kbd> il puntamento polare. Il loro stato compare in basso a destra nella vista.

## Selezione

Fai clic su un elemento per selezionarlo; <kbd>Shift</kbd>-clic lo aggiunge alla selezione. Per selezionarne diversi in una volta, trascina con il tasto sinistro dove non c’è nulla di selezionato. Trascinato verso destra, il riquadro è blu e prende ciò che si trova interamente al suo interno (finestra); trascinato verso sinistra, è verde e tratteggiato e prende tutto ciò che tocca (intersezione), come in AutoCAD. Premi <kbd>Space</kbd> mentre trascini e il riquadro diventa un lazo che segue il mouse, mantenendo la modalità del riquadro; <kbd>Space</kbd> di nuovo torna al riquadro. Nella riga di comando, mentre un comando ti chiede di selezionare oggetti, digita **WP** o **CP** e fai clic sui vertici di un poligono, poi <kbd>Enter</kbd>: un poligono finestra o un poligono intersezione.

![Un lazo nella pianta: il riquadro trascinato verso destra, poi Space, e il contorno attorno ai due muri che prende](shots/select-lasso.webp)

## Copiare e spostare

Per copiare, seleziona l’elemento e digita <kbd>CO</kbd>. Fai clic su un punto base, poi su un secondo punto. Lapin continua a copiare finché non premi <kbd>Enter</kbd> o <kbd>Esc</kbd>. **Sposta** (<kbd>M</kbd>) funziona allo stesso modo.

Mentre sposti o copi, un indicatore resta sul punto base e una linea tratteggiata va da esso al mouse, con un contorno sbiadito di ciò che sposti lasciato dov’era finché non fai clic o premi <kbd>Esc</kbd>. La linea e l’indicatore cambiano colore con lo snap: verde su un punto agganciato, blu quando l’orto o il puntamento polare fissano la direzione, il colore di evidenziazione su una linea di puntamento, grigio quando è libero. Accanto al mouse, Lapin mostra di quanto ti sei spostato: Δx, Δy, la distanza e l’angolo, nelle unità del progetto. Puoi digitare lì la distanza e l’angolo, come quando disegni. Nell’interfaccia Compatta, questi valori sono nella riga di stato. Trascinare una maniglia mostra lo stesso, e così trascinare direttamente un elemento selezionato, dal punto in cui hai premuto; premi <kbd>Esc</kbd> prima di rilasciare e l’elemento resta dov’era.

![Spostamento di un muro: l’indicatore sul punto base, la linea tratteggiata fino al mouse, la sagoma del muro dov’era e lo spostamento accanto al mouse](shots/move-trace.webp)

Puoi anche trascinare. Trascina la croce al centro di un elemento selezionato per spostarlo. Tieni premuto <kbd>Ctrl</kbd> (<kbd>⌘</kbd> su Mac) mentre trascini per lasciare una copia.

Una porta, una finestra o un’apertura nel muro si sposta e si copia lungo il suo muro: conta solo la parte dello spostamento lungo il muro, quindi digitare `500` dopo il punto base la fa scorrere di 500 lungo il muro; spostata in un altro muro, passa in quel muro. Se è selezionato anche il suo muro, va con il muro.

Un asse della griglia si sposta, si copia, ruota e si specchia da solo o insieme ad altri elementi. Una copia è un nuovo asse della stessa griglia, chiamato in base all’ultimo asse che corre nella sua direzione: con gli assi 1, 2 e 3, una copia di uno qualsiasi di essi è 4.

## Disegno

La scheda **Disegno** disegna linee, polilinee, rettangoli, cerchi e archi. Li modifica con offset, taglia, estendi, raccordo, smusso e unisci, riempie le aree con retini e li organizza in **Layer**. **Importa DXF** porta dentro un disegno CAD come linee che puoi agganciare con lo snap e modificare.

![La scheda Disegno](shots/ribbon-draft.webp)

**Offset** (<kbd>O</kbd>) copia una linea, una polilinea, un rettangolo, un cerchio o un arco a una distanza. Scrivi la distanza nell’unità del progetto (in un progetto in millimetri `500` è mezzo metro; `2m` o `1'6"` dicono la propria unità), fai clic sulla forma, poi sul lato verso cui va; chiede la forma successiva finché premi <kbd>Invio</kbd> o <kbd>Esc</kbd>. Scrivi invece **T** (Through) e ogni copia passa per il punto su cui fai clic. Gli spigoli di una polilinea restano vivi (a quartabuono), come in CAD. L’offset funziona in piante, sezioni, prospetti, su piani di riferimento e nella vista 3D.

Ogni lunghezza che la riga di comando chiede è nell’unità del progetto, e il messaggio dice quale: `Depth (mm)`, `Height (ft-in)`. Angoli, fattori e conteggi sono numeri semplici: `90` dopo **Ruota** è 90°, qualunque sia l’unità.

### Disegnare nella vista 3D

**Linea**, **Polilinea**, **Rettangolo**, **Cerchio**, **Arco** e le modifiche di Disegno funzionano anche nella vista 3D, con la riga di comando in basso. Mentre un comando è in corso, l’etichetta in alto dice dove disegni: alla quota del piano attivo o sul piano di riferimento selezionato. **Scegli una faccia** nell’etichetta, poi un clic su una faccia del modello, crea su quella faccia un piano di riferimento e disegna su di esso. I punti si agganciano come in pianta: alle linee tagliate della pianta (spigoli e facce dei muri) e a ciò che è disegnato sullo stesso piano, con l’orto e le linee di tracciamento. Ciò che disegni è lo stesso elemento di disegno di uno fatto in pianta, e si vede anche lì.

![Disegnare nella vista 3D: l’etichetta dice il piano, la linea segue il mouse alla quota del piano](shots/draft-3d.webp)

### I disegni nella vista 3D

Ciò che disegni in una pianta (linee, polilinee, rettangoli, cerchi, archi) compare anche nella vista 3D, come linee alla quota del suo piano (la quota stessa, non l’altezza di taglio); ciò che disegni su un piano di riferimento, una sezione o un prospetto sta su quel piano. Fai clic su uno in 3D per selezionarlo, poi spostalo, copialo, fanne l’offset o cancellalo. **Vista → Disegni in 3D**, anche in **Vista → Impostazioni di vista** di una vista 3D, li mostra o li nasconde tutti; è attivo finché non lo spegni. Quote, testi, etichette, retini e linee di costruzione (il ruolo draft) restano nelle loro viste, e un DXF portato su una pianta resta un disegno 2D, tranne le sue linee con quote.

## Annotazioni

La scheda **Annota** contiene le quote, i testi, gli assi della griglia, i livelli e le etichette. Una quota legge il modello, quindi si aggiorna quando un muro si sposta. L’aspetto di testi, quote, assi ed etichette si imposta in **Gestisci → Standard di disegno**.

Una quota allineata o lineare si posa come una catena: dopo i primi due punti e la sua linea, ogni clic aggiunge il punto successivo sulla stessa linea, e **Enter** (o **Esc**) rende l’intera catena una sola quota (**U** riprende l’ultimo punto; digita prima **S** per una quota singola). Una catena si comporta come una polilinea: un clic la seleziona tutta, e si sposta, si copia e si elimina come un unico oggetto. Una catena che misura muri, spigoli o assi della griglia resta agganciata a essi: trascinata o spostata da sola, scorre solo la sua linea di quota, perpendicolare alla catena, così che un movimento sbagliato del mouse non la lasci mai a misurare il vuoto; spostata insieme a ciò che misura, li segue. Le sue maniglie stanno su ogni punto misurato (trascina per spostarlo, Ctrl/⌘-clic per toglierlo), al centro di ogni segmento (trascina per aggiungere un punto, Ctrl/⌘-clic per aggiungerne uno a metà) e alle estremità della linea di quota (trascina per spostare la linea). Per inserire altri punti in una catena, come in ArchiCAD, selezionala e fai clic su **Annota → Aggiungi punti** (oppure digita **DIMADD**, o usa **Aggiungi punti** nelle sue proprietà): ogni punto su cui fai clic entra nella catena dove cade lungo la sua linea, tra i punti che lo circondano oppure oltre un’estremità come nuova estremità, e resta agganciato al muro, allo spigolo o all’asse su cui cade; **Enter** o **Esc** termina. Per togliere punti, fai Ctrl/⌘-clic sulle loro maniglie, oppure fai clic su **Annota → Rimuovi punti** (**DIMDEL**, o **Rimuovi punti** nelle sue proprietà) e fai clic vicino a ogni punto; i segmenti ai due lati diventano uno solo. Anche il valore di ogni segmento ha una maniglia: trascinala per spostare il valore dalla sua posizione dove i segmenti sono corti e i valori si sovrapporrebbero; quando si allontana dal suo segmento, una sottile linea di richiamo lo ricollega. Ctrl/⌘-clic sulla maniglia, oppure **Testi di nuovo sulla linea** nelle proprietà, lo rimette al suo posto. Un valore spostato resta con il suo segmento quando i punti si muovono, e ruota con la catena. Fai doppio clic su un segmento per digitarne il valore: l’elemento che misura si sposta, e con esso tutto ciò che lo segue nella catena. Le proprietà mostrano il valore e il testo di ogni segmento, dividono una catena in quote singole e uniscono le quote singole che si continuano l’una nell’altra su una stessa linea.

Le annotazioni di una pianta (quote, testi, linee disegnate, etichette) possono passare a un’altra pianta nello stesso punto, poiché le piante condividono le coordinate: selezionale e scegli la pianta nelle loro proprietà, in **Vista** per spostarle o in **Copia in** per copiarle. Così anche le quote fatte su una planimetria arrivano alla pianta del livello. I punti di una quota si agganciano poi a ciò che la nuova pianta taglia in quel punto: muri, spigoli, assi.

### Etichette

**Annota → Etichetta** (<kbd>TG</kbd>) mette su una vista un riquadro con ciò che un elemento dice di sé, e una linea di richiamo fino a esso. Mentre lo strumento è attivo, la barra sotto la vista sceglie cosa mostra l’etichetta: la sigla, il nome, la classe o il tipo, **Resistenza al fuoco**, **Isolamento acustico**, **Trasmittanza termica** o **Altro parametro…** per nome; oppure un tipo di etichetta degli standard di disegno; e **RICHIAMO** attiva o disattiva la linea di richiamo. Fai clic sull’elemento, poi dove va l’etichetta (digita prima **E** per piegare la linea in un gomito). **Annota → Etichetta un parametro** chiede il nome del parametro e avvia lo strumento con esso.

![La barra dello strumento Etichetta sotto la vista](shots/tag-options.webp)

Un’etichetta legge qualsiasi parametro: quello dell’elemento, quello del suo tipo (allora lo mostrano tutti gli elementi del tipo), una proprietà standard come `Pset_WallCommon.FireRating` o una con cui è arrivato un elemento importato. Un nome si trova anche senza spazi, quindi **Fire rating** e `FireRating` sono lo stesso. Per compilarlo usa **Parametri** nelle proprietà dell’elemento o del suo tipo: scrivi un nome (sono proposti Fire rating, Sound rating, Moisture resistance e Thermal resistance) oppure scegli **Aggiungi una proprietà standard…** per le proprietà IFC della sua classe. L’etichetta segue non appena c’è un valore, e le lunghezze si scrivono nelle unità del progetto.

Un’etichetta selezionata ha delle maniglie. Trascina il riquadro per spostare l’etichetta: la linea di richiamo continua a puntare dove puntava. Trascina il centro della linea per piegarla in un gomito (Ctrl/⌘-clic sul gomito la raddrizza) e la sua estremità per farla puntare altrove (Ctrl/⌘-clic: di nuovo al centro dell’elemento). Le proprietà cambiano ciò che l’etichetta **Mostra**, le danno un **Testo** proprio (`{mark}|{FireRating}` scrive due righe), un **Tipo di etichetta**, o disattivano la **Linea di richiamo**. Il motore disegna le etichette, così tavole e PDF le stampano come le vedi. I tipi di etichetta in **Gestisci → Standard di disegno → Etichette** leggono gli stessi campi nei loro modelli; un nuovo progetto ha tra questi Resistenza al fuoco, Isolamento acustico e Trasmittanza termica.

![La linea di richiamo di un’etichetta piegata, con le sue maniglie](shots/tag-leader.webp)

![Le proprietà di un’etichetta](shots/tag-properties.webp)

Il contrassegno di una porta o di una finestra si modifica parte per parte, ogni testo spostato o ruotato e ogni estremità della sua linea allungata: vedi [Porte e finestre](doors-windows.md).

### Assi della griglia

**Annota → Asse di griglia** (<kbd>GR</kbd>) disegna un asse dalle sue due estremità; prende l’etichetta successiva della sua direzione. **Annota → Griglia di assi…** posa un’intera griglia in una volta: gli interassi **In orizzontale** (gli assi numerati, da sinistra a destra) e **In verticale** (gli assi con lettere, dal basso in alto) nelle unità del progetto, come `6000 6000 4500` o `3*6000`; l’**Origine** dove si incrociano i primi assi, un **Angolo**, quanto gli assi proseguono **Oltre la griglia**, le **Prime etichette** e il **Tipo di asse**. Con **sostituisci gli assi della griglia** spuntato, gli assi disegnati finora spariscono.

![La finestra Griglia di assi](shots/grid-dialog.webp)

Fai clic su un asse per selezionarlo. Le sue proprietà mostrano **Inizio** e **Fine**, **Lunghezza**, **Angolo** e l’interasse dall’asse parallelo precedente (↔). Scrivi un nuovo interasse e l’asse si sposta; si spostano anche gli assi successivi, a meno che tu non tolga la spunta da **si spostano anche gli assi successivi**. **Bolle** sceglie quali estremità hanno una bolla. **Stili degli assi…** apre **Standard di disegno → Assi della griglia**, dove si impostano la bolla, la sua dimensione, il tipo di linea e la prima etichetta. Seleziona l’intera griglia per vederne gli assi e gli interassi.

![Le proprietà di un asse](shots/axis-properties.webp)

Tutte queste lunghezze seguono le unità del progetto. In piedi e pollici, estremità e interassi si leggono 19'-8 1/4", e anche i livelli si scrivono in piedi e pollici.

![Lo stesso asse in piedi e pollici](shots/axis-properties-imperial.webp)

![La scheda Annota](shots/ribbon-annotate.webp)
