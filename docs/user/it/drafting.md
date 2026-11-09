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

**Esplodi** (<kbd>X</kbd>, o **EXPLODE**) è la via del ritorno da **Unisci**: seleziona polilinee o rettangoli e ognuno diventa le sue linee, una per lato, sullo stesso livello con la stessa penna e colore e nello stesso gruppo, in un solo passo di annullamento. Cerchi, archi, campiture e catene di quote restano come sono, e una selezione senza niente da dividere lo dice. Funziona in piante, sezioni, prospetti, su piani di riferimento e sul piano di lavoro della vista 3D.

Ogni lunghezza che la riga di comando chiede è nell’unità del progetto, e il messaggio dice quale: `Depth (mm)`, `Height (ft-in)`. Angoli, fattori e conteggi sono numeri semplici: `90` dopo **Ruota** è 90°, qualunque sia l’unità.

### Ricalcare un PDF o un’immagine

**File → Importa PDF o immagine…**, oppure **Disegno → PDF o immagine**, porta dentro un disegno da ricalcare: una pianta scansionata, la foto di una facciata, un PDF di un altro studio. Entra nella pianta, sezione o prospetto in primo piano, sotto il suo disegno.

- **Un PDF**: scegli la **Pagina** e la scala in cui è stato fatto il suo disegno (**Disegnato in 1:**). Un millimetro di carta misura allora qui altrettanti millimetri, così una pianta in 1:100 entra alla sua vera grandezza. Le linee della pagina entrano come disegno a cui agganciarti, da modificare e ricalcare, e le sue immagini (una scansione nel PDF) come immagini sotto di esse. Si può lasciare fuori l’una o l’altra parte. La pagina entra come un unico gruppo con il nome del file, così **Sposta**, **Ruota** e **Scala** la prendono intera; fai doppio clic su di essa per lavorare sulle sue parti.
- **Un’immagine** (PNG o JPEG): dalle la **Larghezza**; l’altezza segue i suoi pixel. **Disegno → Scala immagine** la fissa poi esattamente: seleziona l’immagine, fai clic su due suoi punti a distanza nota e scrivi quella distanza.

Un’immagine è trasparente (la sua **Opacità**, 0,5 di default, nelle sue proprietà) e non fa mai parte del modello: si stampa sulle tavole solo se la lasci nella vista.

![Una pianta stampata in PDF, portata in un altro livello per ricalcarla](shots/trace-pdf.webp)

### Disegnare nella vista 3D

**Linea**, **Polilinea**, **Rettangolo**, **Cerchio**, **Arco** e le modifiche di Disegno funzionano anche nella vista 3D, con la riga di comando in basso. Mentre un comando è in corso, l’etichetta in alto dice dove disegni: alla quota del piano attivo o sul piano di riferimento selezionato. **Scegli una faccia** nell’etichetta, poi un clic su una faccia del modello, crea su quella faccia un piano di riferimento e disegna su di esso. I punti si agganciano come in pianta: alle linee tagliate della pianta (spigoli e facce dei muri) e a ciò che è disegnato sullo stesso piano, con l’orto e le linee di tracciamento. Ciò che disegni è lo stesso elemento di disegno di uno fatto in pianta, e si vede anche lì.

![Disegnare nella vista 3D: l’etichetta dice il piano, la linea segue il mouse alla quota del piano](shots/draft-3d.webp)

### I disegni nella vista 3D

Ciò che disegni in una pianta (linee, polilinee, rettangoli, cerchi, archi) compare anche nella vista 3D, come linee alla quota del suo piano (la quota stessa, non l’altezza di taglio); ciò che disegni su un piano di riferimento, una sezione o un prospetto sta su quel piano. Fai clic su uno in 3D per selezionarlo, poi spostalo, copialo, fanne l’offset o cancellalo. **Vista → Disegni in 3D**, anche in **Vista → Impostazioni di vista** di una vista 3D, li mostra o li nasconde tutti; è attivo finché non lo spegni. Quote, testi, etichette, retini e linee di costruzione (il ruolo draft) restano nelle loro viste. Un DXF portato su una vista sta anche in 3D, sul piano di quella vista, con un interruttore suo (vedi [Importazione ed esportazione](import-export.md)).

### Ordine di disegno

Dove gli elementi disegnati si sovrappongono, quello disegnato dopo sta sopra. Per cambiarlo, selezionali (linee, polilinee, rettangoli, cerchi, archi, retini e riempimenti, testi, quote, etichette, le forme di un DXF), fai clic destro e scegli **Porta in primo piano**, **Porta avanti**, **Porta indietro** o **Porta in secondo piano**; **Disegno → Ordine** e **Annota → Ordine** hanno gli stessi comandi, con <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>[</kbd> e <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>[</kbd> (⌘ su Mac). Avanti e indietro superano l’elemento successivo con cui la selezione si sovrappone. Il disegno 2D sta sopra il disegno del modello; **Manda dietro il modello** lo mette sotto la sezione dei muri e le linee viste (un retino sotto un muro), e un clic lì prende allora il muro; **Porta in primo piano** o **Porta in secondo piano** lo riportano sopra il modello. Nella riga di comando, **DRAWORDER** (**DRO**) chiede gli oggetti, poi **Above objects** o **Under objects** (fai clic sugli elementi sopra o sotto cui vanno, poi <kbd>Invio</kbd>), **Front**, **Back** o dietro il modello, **Model**. L’ordine è della vista, si annulla in un passo, e tavole, PDF e DXF lo stampano come lo vedi; ciò che disegni dopo va sopra.

![Clic destro su un riempimento selezionato: l’ordine di disegno](shots/draw-order-menu.webp)

![Portato in primo piano, il riempimento grigio copre il testo](shots/draw-order-front.webp)

## Annotazioni

La scheda **Annota** contiene le quote, i testi, gli assi della griglia, i livelli e le etichette. Una quota legge il modello, quindi si aggiorna quando un muro si sposta. L’aspetto di testi, quote, assi ed etichette si imposta in **Gestisci → Standard di disegno**.

Una quota allineata o lineare si posa come una catena: dopo i primi due punti e la sua linea, ogni clic aggiunge il punto successivo sulla stessa linea, e **Enter** (o **Esc**) rende l’intera catena una sola quota (**U** riprende l’ultimo punto; digita prima **S** per una quota singola). Una catena si comporta come una polilinea: un clic la seleziona tutta, e si sposta, si copia e si elimina come un unico oggetto. Una catena che misura muri, spigoli o assi della griglia resta agganciata a essi: trascinata o spostata da sola, scorre solo la sua linea di quota, perpendicolare alla catena, così che un movimento sbagliato del mouse non la lasci mai a misurare il vuoto; spostata insieme a ciò che misura, li segue. Le sue maniglie stanno su ogni punto misurato (trascina per spostarlo, Ctrl/⌘-clic per toglierlo), al centro di ogni segmento (trascina per aggiungere un punto, Ctrl/⌘-clic per aggiungerne uno a metà) e alle estremità della linea di quota (trascina per spostare la linea). Per inserire altri punti in una catena, come in ArchiCAD, selezionala e fai clic su **Annota → Aggiungi punti** (oppure digita **DIMADD**, o usa **Aggiungi punti** nelle sue proprietà): ogni punto su cui fai clic entra nella catena dove cade lungo la sua linea, tra i punti che lo circondano oppure oltre un’estremità come nuova estremità, e resta agganciato al muro, allo spigolo o all’asse su cui cade; **Enter** o **Esc** termina. Per togliere punti, fai Ctrl/⌘-clic sulle loro maniglie, oppure fai clic su **Annota → Rimuovi punti** (**DIMDEL**, o **Rimuovi punti** nelle sue proprietà) e fai clic vicino a ogni punto; i segmenti ai due lati diventano uno solo. Anche il valore di ogni segmento ha una maniglia: trascinala per spostare il valore dalla sua posizione dove i segmenti sono corti e i valori si sovrapporrebbero; quando si allontana dal suo segmento, una sottile linea di richiamo lo ricollega. Ctrl/⌘-clic sulla maniglia, oppure **Testi di nuovo sulla linea** nelle proprietà, lo rimette al suo posto. Un valore spostato resta con il suo segmento quando i punti si muovono, e ruota con la catena. Fai doppio clic su un segmento per digitarne il valore: l’elemento che misura si sposta, e con esso tutto ciò che lo segue nella catena. Le proprietà mostrano il valore e il testo di ogni segmento, dividono una catena in quote singole e uniscono le quote singole che si continuano l’una nell’altra su una stessa linea.

Le annotazioni di una pianta (quote, testi, linee disegnate, etichette) possono passare a un’altra pianta nello stesso punto, poiché le piante condividono le coordinate: selezionale e scegli la pianta nelle loro proprietà, in **Vista** per spostarle o in **Copia in** per copiarle. Così anche le quote fatte su una planimetria arrivano alla pianta del livello. I punti di una quota si agganciano poi a ciò che la nuova pianta taglia in quel punto: muri, spigoli, assi.

**Altezze delle aperture.** Un segmento di una catena di quote che va da una spalla all’altra di una porta, una finestra o un’apertura scrive la larghezza sopra la linea e l’altezza dell’apertura sotto, come nei disegni rumeni ed europei. È letta dal modello ogni volta, quindi segue l’apertura quando viene ridimensionata, spostata o riceve un altro tipo; un segmento su cui hai scritto un testo non la scrive. **Gestisci → Standard di disegno → Dimensioni → Altezza dell’apertura** la imposta per ogni stile di quota: spenta, `150`, `h=150`, `h=150` con il davanzale `p=90` sotto, o `150/90`, nell’unità dello stile. Funziona in piante, sezioni e prospetti, e tavole, PDF e DXF la stampano. I progetti nuovi la hanno accesa (solo l’altezza); un progetto fatto prima tiene le sue quote com’erano finché non la accendi lì.

![Le altezze della porta e della finestra sotto i loro segmenti](shots/dimension-opening-height.webp)

![L’altezza dell’apertura negli stili di quota](shots/dimension-opening-height-style.webp)

### Etichette

**Annota → Etichetta** (<kbd>TG</kbd>) mette su una vista un riquadro con ciò che un elemento dice di sé, e una linea di richiamo fino a esso. Mentre lo strumento è attivo, la barra sotto la vista sceglie cosa mostra l’etichetta: la sigla, il nome, la classe o il tipo, **Resistenza al fuoco**, **Isolamento acustico**, **Trasmittanza termica** o **Altro parametro…** per nome; oppure un tipo di etichetta degli standard di disegno; e **RICHIAMO** attiva o disattiva la linea di richiamo. Fai clic sull’elemento, poi dove va l’etichetta (digita prima **E** per piegare la linea in un gomito). **Annota → Etichetta un parametro** chiede il nome del parametro e avvia lo strumento con esso.

![La barra dello strumento Etichetta sotto la vista](shots/tag-options.webp)

Un’etichetta legge qualsiasi parametro: quello dell’elemento, quello del suo tipo (allora lo mostrano tutti gli elementi del tipo), una proprietà standard come `Pset_WallCommon.FireRating` o una con cui è arrivato un elemento importato. Un nome si trova anche senza spazi, quindi **Fire rating** e `FireRating` sono lo stesso. Per compilarlo usa **Parametri** nelle proprietà dell’elemento o del suo tipo: scrivi un nome (sono proposti Fire rating, Sound rating, Moisture resistance e Thermal resistance) oppure scegli **Aggiungi una proprietà standard…** per le proprietà IFC della sua classe. L’etichetta segue non appena c’è un valore, e le lunghezze si scrivono nelle unità del progetto.

Un’etichetta selezionata ha delle maniglie. Trascina il riquadro per spostare l’etichetta: la linea di richiamo continua a puntare dove puntava. Un’etichetta segue il suo elemento: quando l’elemento viene spostato, ruotato, specchiato o rimodellato con le sue maniglie, l’etichetta lo segue mantenendo il suo posto accanto a esso, e la linea di richiamo resta su di esso; un’etichetta che hai spostato mantiene la distanza che le hai dato. Trascina il centro della linea per piegarla in un gomito (Ctrl/⌘-clic sul gomito la raddrizza) e la sua estremità per farla puntare altrove (Ctrl/⌘-clic: di nuovo al centro dell’elemento). Le proprietà cambiano ciò che l’etichetta **Mostra**, le danno un **Testo** proprio (`{mark}|{FireRating}` scrive due righe), un **Tipo di etichetta**, o disattivano la **Linea di richiamo**. Il motore disegna le etichette, così tavole e PDF le stampano come le vedi. I tipi di etichetta in **Gestisci → Standard di disegno → Etichette** leggono gli stessi campi nei loro modelli; un nuovo progetto ha tra questi Resistenza al fuoco, Isolamento acustico e Trasmittanza termica.

**Linee di richiamo come le etichette di ArchiCAD.** Di norma la linea di richiamo di un’etichetta è *a gomito*: una linea obliqua dall’elemento a un gomito, poi una spalla che corre sotto il testo e ne sottolinea la prima riga, senza riquadro attorno al testo. Dopo il clic sull’elemento, il clic successivo è il gomito e il testo va accanto. Trascina la maniglia del gomito: la linea obliqua resta a 30°, 45°, 60° o 90° dalla sua estremità (tieni <kbd>Maiusc</kbd> per qualsiasi angolo), e trascinata oltre l’estremità il testo passa da quel lato, allineato verso il gomito. Trascina il testo per spostarlo con il suo gomito. In **Gestisci → Standard di disegno → Etichette**, **Linea di richiamo…** su un tipo di etichetta (e l’editor della stratigrafia) imposta la **Forma della linea di richiamo** (a gomito, o diritta da un riquadro), cosa sottolinea la spalla (**Sottolineatura**: la prima riga, ogni riga, l’ultima riga o niente, quando si ferma al lato del testo) e **Prolungamento mm**, quanto più la spalla si allunga dal testo, accanto all’anteprima del motore. Con una linea di richiamo per strato, ognuna finisce sotto la propria riga, i gomiti su una colonna; trascinare il gomito sposta la colonna. **Forma della linea di richiamo** nelle proprietà di un’etichetta prevale su quella del tipo; un’etichetta la cui linea di richiamo è stata piegata in un gomito libero conserva la linea diritta. Le etichette dei progetti fatti prima mantengono la linea di richiamo dritta in un riquadro.

![La linea di richiamo di un’etichetta piegata, con le sue maniglie](shots/tag-leader.webp)

![Le proprietà di un’etichetta](shots/tag-properties.webp)

Il contrassegno di una porta o di una finestra si modifica parte per parte, ogni testo spostato o ruotato e ogni estremità della sua linea allungata: vedi [Porte e finestre](doors-windows.md).

#### Etichetta di stratigrafia

Un’etichetta di stratigrafia scrive gli strati di un elemento come una nota architettonica: un’intestazione (*EXTERIOR*), una riga per ogni strato, un piè (*INTERIOR*). Un nuovo progetto ha **Build-up (exterior → interior)**: ogni riga è la descrizione del materiale (il suo nome se non ne ha), il suo λ se ce l’ha e lo spessore dello strato in mm. Sceglila nella barra dello strumento **Etichetta** e fai clic su un muro, un solaio, una copertura o la finitura di un locale; gli altri elementi ricevono il campo scelto. Il testo è letto dal modello ogni volta che viene disegnato: cambia uno strato, la descrizione di un materiale o il suo λ, e l’etichetta segue.

![Due etichette di stratigrafia su un muro di facciata: una linea di richiamo, e una per strato](shots/buildup-tag.webp)

La sua forma si imposta in **Gestisci → Standard di disegno → Etichette**: imposta la **Forma** di un tipo di etichetta su *stratigrafia*, o fai clic su **Stratigrafia…** su uno di essi, per modificarlo accanto a un’anteprima su uno dei tipi di muro o di solaio del progetto.

- **Intestazione** e **Piè**: scritti sopra e sotto gli strati (vuoto: nessuno; `|` inizia una nuova riga).
- **Ogni strato**: la riga scritta una volta per strato. Legge i campi dello strato, `{description}`, `{material}`, `{thickness}` (nell’unità scelta in **Spessore in**), `{note}` (la nota dello strato nel suo tipo), `{function}` (nucleo, finitura, isolante…) e `{index}`; qualsiasi parametro del materiale per nome o simbolo, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (vedi i [Dati](materials.md#dati-descrizione-parametri-come-si-misura) del materiale); e quelli dell’elemento, `{name}`, `{type}`, `{mark}`. **Inserisci un campo…** ne mette uno dove si trova il cursore ed elenca i parametri dei materiali del progetto.
- **Le parentesi quadre** rendono facoltativa una parte: `[, λ = {λ} W/mK]` si scrive solo dove il materiale ha un λ; una parte cade intera quando un campo al suo interno è vuoto. **[ ]** rende la selezione una parte così. Per scrivere una parentesi, digita `\[` o `\]`.
- **Ordine**: dall’esterno all’interno di un muro (il lato del suo primo strato è il suo esterno, quindi un muro ribaltato comincia comunque da lì), dall’alto in basso in un solaio o una copertura; oppure al contrario. Scegli l’ordine in cui gli strati stanno accanto alla nota, così le linee di richiamo per strato non si incrociano.
- **Gli strati senza spessore** (membrane, teli, pittura) vengono scritti, a meno che non togli la spunta a **visibili**. Uno strato che cambia materiale in altezza scrive una riga per materiale.
- **Linee di richiamo**: una, all’elemento, oppure **una per strato**, ognuna dalla sua riga a un punto dentro il proprio strato, dove la vista distingue gli strati (un muro in pianta e in sezione, un solaio in sezione; altrove l’etichetta ne tiene una). Trascina l’estremità della linea di richiamo lungo l’elemento per attraversarlo altrove.
- **Le righe iniziano con** un punto, un trattino, il loro numero o niente; vanno a capo a **Larghezza mm** sulla carta, rientrate dopo il punto elenco, e un valore resta con il suo simbolo e la sua unità (`λ = 0.035 W/mK`).

![L’editor di un tipo di etichetta di stratigrafia accanto alla sua anteprima](shots/buildup-tag-type.webp)

Come ogni etichetta, l’etichetta di stratigrafia segue il suo elemento, e le tavole e i PDF la stampano come la vedi, λ, μ e ρ compresi. Un **Testo** proprio nelle sue proprietà sostituisce la riga per strato solo per quell’etichetta.

### Assi della griglia

**Annota → Asse di griglia** (<kbd>GR</kbd>) disegna un asse dalle sue due estremità; prende l’etichetta successiva della sua direzione. **Annota → Griglia di assi…** posa un’intera griglia in una volta: gli interassi **In orizzontale** (gli assi numerati, da sinistra a destra) e **In verticale** (gli assi con lettere, dal basso in alto) nelle unità del progetto, come `6000 6000 4500` o `3*6000`; l’**Origine** dove si incrociano i primi assi, un **Angolo**, quanto gli assi proseguono **Oltre la griglia**, le **Prime etichette** e il **Tipo di asse**. Con **sostituisci gli assi della griglia** spuntato, gli assi disegnati finora spariscono.

![La finestra Griglia di assi](shots/grid-dialog.webp)

Fai clic su un asse per selezionarlo. Le sue proprietà mostrano **Inizio** e **Fine**, **Lunghezza**, **Angolo** e l’interasse dall’asse parallelo precedente (↔). Scrivi un nuovo interasse e l’asse si sposta; si spostano anche gli assi successivi, a meno che tu non tolga la spunta da **si spostano anche gli assi successivi**. **Bolle** sceglie quali estremità hanno una bolla. **Stili degli assi…** apre **Standard di disegno → Assi della griglia**, dove si impostano la bolla, la sua dimensione, il tipo di linea e la prima etichetta. Seleziona l’intera griglia per vederne gli assi e gli interassi.

![Le proprietà di un asse](shots/axis-properties.webp)

Tutte queste lunghezze seguono le unità del progetto. In piedi e pollici, estremità e interassi si leggono 19'-8 1/4", e anche i livelli si scrivono in piedi e pollici.

![Lo stesso asse in piedi e pollici](shots/axis-properties-imperial.webp)

![La scheda Annota](shots/ribbon-annotate.webp)
