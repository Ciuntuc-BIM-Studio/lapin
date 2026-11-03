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

Un asse della griglia si sposta, si copia, ruota e si specchia da solo o insieme ad altri elementi. Una copia è un nuovo asse della stessa griglia, chiamato in base all’ultimo asse che corre nella sua direzione: con gli assi 1, 2 e 3, una copia di uno qualsiasi di essi è 4.

## Disegno

La scheda **Disegno** disegna linee, polilinee, rettangoli, cerchi e archi. Li modifica con offset, taglia, estendi, raccordo, smusso e unisci, riempie le aree con retini e li organizza in **Layer**. **Importa DXF** porta dentro un disegno CAD come linee che puoi agganciare con lo snap e modificare.

![La scheda Disegno](shots/ribbon-draft.webp)

## Annotazioni

La scheda **Annota** contiene le quote, i testi, gli assi della griglia, i livelli e le etichette. Una quota legge il modello, quindi si aggiorna quando un muro si sposta. L’aspetto di testi, quote, assi ed etichette si imposta in **Gestisci → Standard di disegno**.

Una quota allineata o lineare si posa come una catena: dopo i primi due punti e la sua linea, ogni clic aggiunge il punto successivo sulla stessa linea, e **Enter** (o **Esc**) rende l’intera catena una sola quota (**U** riprende l’ultimo punto; digita prima **S** per una quota singola). Una catena si comporta come una polilinea: un clic la seleziona tutta, e si sposta, si copia e si elimina come un unico oggetto. Una catena che misura muri, spigoli o assi della griglia resta agganciata a essi: trascinata o spostata da sola, scorre solo la sua linea di quota, perpendicolare alla catena, così che un movimento sbagliato del mouse non la lasci mai a misurare il vuoto; spostata insieme a ciò che misura, li segue. Le sue maniglie stanno su ogni punto misurato (trascina per spostarlo, Ctrl/⌘-clic per toglierlo), al centro di ogni segmento (trascina per aggiungere un punto, Ctrl/⌘-clic per aggiungerne uno a metà) e alle estremità della linea di quota (trascina per spostare la linea). Per inserire altri punti in una catena, come in ArchiCAD, selezionala e fai clic su **Annota → Aggiungi punti** (oppure digita **DIMADD**, o usa **Aggiungi punti** nelle sue proprietà): ogni punto su cui fai clic entra nella catena dove cade lungo la sua linea, tra i punti che lo circondano oppure oltre un’estremità come nuova estremità, e resta agganciato al muro, allo spigolo o all’asse su cui cade; **Enter** o **Esc** termina. Per togliere punti, fai Ctrl/⌘-clic sulle loro maniglie, oppure fai clic su **Annota → Rimuovi punti** (**DIMDEL**, o **Rimuovi punti** nelle sue proprietà) e fai clic vicino a ogni punto; i segmenti ai due lati diventano uno solo. Anche il valore di ogni segmento ha una maniglia: trascinala per spostare il valore dalla sua posizione dove i segmenti sono corti e i valori si sovrapporrebbero; quando si allontana dal suo segmento, una sottile linea di richiamo lo ricollega. Ctrl/⌘-clic sulla maniglia, oppure **Testi di nuovo sulla linea** nelle proprietà, lo rimette al suo posto. Un valore spostato resta con il suo segmento quando i punti si muovono, e ruota con la catena. Fai doppio clic su un segmento per digitarne il valore: l’elemento che misura si sposta, e con esso tutto ciò che lo segue nella catena. Le proprietà mostrano il valore e il testo di ogni segmento, dividono una catena in quote singole e uniscono le quote singole che si continuano l’una nell’altra su una stessa linea.

Le annotazioni di una pianta (quote, testi, linee disegnate, etichette) possono passare a un’altra pianta nello stesso punto, poiché le piante condividono le coordinate: selezionale e scegli la pianta nelle loro proprietà, in **Vista** per spostarle o in **Copia in** per copiarle. Così anche le quote fatte su una planimetria arrivano alla pianta del livello. I punti di una quota si agganciano poi a ciò che la nuova pianta taglia in quel punto: muri, spigoli, assi.

![La scheda Annota](shots/ribbon-annotate.webp)
