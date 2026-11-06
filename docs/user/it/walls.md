---
title: Muri
order: 20
summary: Disegna i muri angolo per angolo, modificali nel pannello Proprietà e imposta i tipi di muro con i loro strati.
---

<!-- video walls -->

## Disegnare i muri

Apri la scheda **Architettura** e scegli **Muro**, oppure digita <kbd>WA</kbd>.

Mentre lo strumento è attivo, il pannello Proprietà mostra **Nuovo elemento**: il tipo con cui vengono disegnati i muri. Scegline un altro lì prima del primo clic, e vedi i suoi strati, il suo spessore e quanti muri lo usano già; **Modifica tipi…** apre i tipi di muro. Porta, Finestra, Solaio, Controsoffitto e Facciata continua mostrano i loro tipi nello stesso modo.

![La scheda Architettura](shots/ribbon-architecture.webp)

Fai clic dove inizia il muro, poi fai clic su ogni angolo. Il muro successivo segue il mouse. Mentre disegni, invece di fare clic puoi digitare una lunghezza e un angolo nei campi sopra la vista. Premi <kbd>Enter</kbd> o <kbd>Esc</kbd> per fermarti.

![Disegno dei muri: il muro successivo segue il mouse, con la sua lunghezza e il suo angolo](shots/wall-drawing.webp)

Dove due muri si incontrano, Lapin li unisce da solo. I loro strati si compenetrano come verrebbero costruiti.

- **Muro curvo** (<kbd>WC</kbd>) disegna un muro lungo un arco.
- **Facciata continua** (<kbd>CW</kbd>) disegna una parete vetrata con montanti e pannelli.
- I muri si uniscono da soli dove si incontrano: un angolo a quartabuono, un muro che termina su un altro a T, uno che ne attraversa un altro a X. A un T o a un X gli strati si compenetrano come si costruiscono: il nucleo del muro che termina prosegue attraverso le finiture dell’altro fino al suo nucleo, e le sue finiture si fermano alla faccia dell’altro, dove incontrano le loro simili, così la pianta mostra un’unica stratigrafia senza una linea in mezzo. **Unisci** (<kbd>JW</kbd>), **A quartabuono** e **Di testa** cambiano il modo in cui due muri si incontrano quando non è quello che vuoi; **Nuovi angoli a quartabuono** rende la giunzione di testa quella predefinita su questo computer. In entrambi i casi, ogni strato gira l’angolo per conto suo: le finiture incontrano le loro simili e proseguono senza interruzioni, e i nuclei si incontrano sulla diagonale (quartabuono) oppure un nucleo prosegue fino alla faccia esterna dell’altro (di testa), così una pianta di muri a strati mostra angoli puliti. Ogni strato entra nell'altro muro attraverso i suoi strati di **Priorità** minore e si ferma al primo di priorità uguale o maggiore (vedi **Tipi di muro** più sotto), così di norma i nuclei incontrano i nuclei e le finiture si fermano alle facce; in un angolo, un nucleo di priorità maggiore passa attraverso quello dell'altro, a quartabuono o di testa. La **Priorità di taglio** di un muro non lo fa mai entrare in un muro a cui è unito: tra muri uniti decide solo quale nucleo passa quando entrambi i nuclei hanno la stessa priorità (in un angolo di testa o in un incrocio).
- **Scambia** cambia quale dei due muri passa.

**La linea su cui disegni.** Mentre lo strumento Muro è attivo, l’elenco nella barra della riga di comando sceglie la linea su cui fai clic: **Asse del nucleo** (l’asse, come prima), **Nucleo, faccia esterna**, **Nucleo, faccia interna**, **Faccia esterna** o **Faccia interna**. L’esterno è la destra del muro nel senso in cui disegni (disegna intorno a un edificio in senso antiorario per averlo all’esterno). L’asse del muro resta sul suo nucleo, scostato dalla linea, e i muri disegnati uno dopo l’altro sulla stessa faccia si incontrano sull’angolo di quella faccia.

## Modificare un muro

Fai clic su un muro per selezionarlo. Compaiono le sue maniglie. Trascina le estremità per allungarlo o accorciarlo, oppure trascina la croce al centro per spostarlo.

![Un muro selezionato con le sue maniglie](shots/wall-selected.webp)

Il pannello Proprietà mostra il muro selezionato. La parte superiore del pannello contiene il suo tipo e i suoi strati. Sotto ci sono la lunghezza, l’altezza, l’offset di base, il piano e la fase. Modifica un valore qualsiasi e il modello si aggiorna.

![Le proprietà di un muro](shots/wall-properties.webp)

Più in basso, **Dipendenze** mostra cosa è costruito dal muro e da cosa è costruito il muro. **Usato da** elenca gli elementi che cambiano quando cambia il muro: i muri a cui si unisce, le sue porte e finestre, un muro collegato a esso. Fai clic su un nome per selezionare l’elemento. **Costruito da** elenca gli elementi da cui dipende il muro, poi il suo tipo e i suoi materiali. Quando elimini un elemento da cui altri dipendono, un messaggio nomina quelli ricostruiti; <kbd>Ctrl</kbd> <kbd>Z</kbd> riporta tutto com’era.

**Capovolgi** porta gli strati del muro dall’altro lato. Puoi anche fare clic sulle piccole frecce mostrate accanto a un muro selezionato in pianta.

Un muro riconosciuto da un IFC importato la cui estremità il file taglia in obliquo conserva quel taglio: **Angolo di taglio iniziale** e **Angolo di taglio finale** nelle sue proprietà, in gradi rispetto al taglio dritto. Un giunto a quell’estremità ne prende il posto.

**Allineamento del retino** stabilisce come sono retinati in pianta gli strati di questo muro. **material** segue l’impostazione di ogni materiale. **axis** posa il retino di ogni strato lungo l’asse del muro, a partire dall’inizio dell’asse: un muro di mattoni inclinato mostra i suoi corsi lungo di sé, continui da uno strato all’altro. **view** fa partire ogni retino dall’origine della vista, senza rotazione.

### Un muro secondo il suo profilo

**Profilo…** nelle proprietà di un muro ne disegna la sommità in prospetto, vista dall’inizio alla fine: un timpano, una pendenza, gradini, un muro che segue una scala. Trascina i punti (**Shift** mantiene l’altezza di un punto), fai clic sulla linea in alto per aggiungerne uno, clic destro su un punto per toglierlo, oppure scrivili nella tabella in millimetri. **Parti da** dà un **Timpano**, una **Pendenza** o dei **Gradoni** fino all’altezza che scrivi. Un gradino è fatto di due punti uno sopra l’altro. **Applica** dà la forma al muro; **Sommità piana** lo rende di nuovo piano.

Il muro resta un muro: spessore, strati, giunzioni, porte e finestre restano com’erano, ed è esportato in IFC con la sua forma.

![Un timpano disegnato nell’editor del profilo](shots/wall-profile-editor.webp)

![Il muro ovest della casa a timpano, con la finestra conservata](shots/wall-profile-3d.webp)

## Tipi di muro

Un tipo di muro è un insieme di strati, dalla faccia a destra del verso in cui il muro è disegnato (l’esterno, se disegni intorno all’edificio in senso antiorario) alla faccia alla sua sinistra. Ogni strato ha un materiale e uno spessore, e uno strato è il **nucleo**. La linea del muro corre lungo il nucleo. Per modificare i tipi, scegli **Architettura → Tipi di muro**. Uno strato diverso dal nucleo può avere un’altezza propria: **Da** e **Fino a** stabiliscono fin dove arriva sopra la base del muro (uno zoccolo piastrellato fino a 1500, un rivestimento da 300 in su); se vuoti, lo strato corre per tutta l’altezza.

La colonna **Priorità** (da 0 a 100) dice come lo strato incontra gli strati di un altro muro dove i muri si uniscono: attraversa gli strati dell'altro di priorità minore e si ferma al primo di priorità uguale o maggiore. Se la lasci vuota, lo strato prende la priorità del suo materiale in **Gestisci → Ordine di taglio**, altrimenti 50 per il nucleo e 0 per gli altri strati (il numero mostrato in grigio). Così un nucleo di calcestruzzo a 100 non viene interrotto da un nucleo di montanti a 99 e attraversa il tramezzo, e un intonaco duro a 60 ferma il nucleo di un muro che vi termina contro. L'esportazione IFC la scrive come Priority dello strato, e l'importazione la rilegge.

La colonna **Nota** dice come lo strato è posato in questo tipo (*in strato continuo, Baumit SupraFix*): appartiene al tipo, non al materiale, e un’etichetta di stratigrafia la scrive dopo lo strato; IFC la porta come Description dello strato. Quando il materiale dello strato ha spessori di catalogo (**Gestisci → Materiali → Dati**), la piccola freccia accanto allo spessore li propone e un avviso segna uno strato di altro spessore.

Uno strato diverso dal nucleo può anche cambiare materiale lungo la sua altezza: il pulsante **strati** alla fine della sua riga aggiunge un pezzo (piastrelle, 2100 per impostazione predefinita), dalla base dello strato in su; aggiungine altri per una fascia (pittura fino a 900, piastrelle fino a 1500), e sopra l’ultimo pezzo lo strato prosegue nel suo materiale fino in cima. Ogni pezzo è disegnato, tagliato in pianta e in sezione e misurato nel suo materiale.

Uno strato diverso dal nucleo può avere spessore 0, per un materiale troppo sottile da modellare ma che va misurato: una guaina impermeabile, una barriera al vapore, la pittura. Non forma alcun solido; piante e sezioni lo disegnano come una linea spessa tratteggiata dove sta tra due strati (su una faccia, la linea della faccia è la sua), il disegno delle Stratigrafie dei tipi lo nomina come gli altri, e si misura a superficie: la faccia dello strato su cui poggia, al netto delle aperture, nelle quantità del muro e nel computo per materiale.

![Uno strato di intonaco con piastrelle fino a 2100 e l’intonaco sopra](shots/wall-layer-parts.webp)

![Tipi di muro: gli strati di un muro in laterizio](shots/wall-types.webp)

La legenda sopra gli strati mostra il muro in pianta come lo mostra un muro selezionato: l’asse tratteggiato con una punta di freccia nel verso in cui è stato disegnato, **D** sulla faccia destra e **S** sulla faccia sinistra. Gli strati sono numerati da 1, sulla faccia D, all’ultimo, sulla faccia S, con gli stessi numeri delle righe della tabella; il nucleo, lungo cui corre la linea del muro, è evidenziato. Un muro capovolto con **Capovolgi** ha gli strati al contrario, da S a D. Il messaggio di un salvataggio rifiutato scompare appena modifichi di nuovo il tipo.

- **+ Strato** aggiunge uno strato. Le frecce spostano uno strato in su o in giù.
- **Zone in altezza** divide il nucleo dal basso verso l’alto, per esempio un primo corso, la muratura e un cordolo.
- **Architravi** sceglie gli architravi posti sopra porte e finestre: prefabbricati o gettati in opera, con il loro appoggio e la loro altezza.
- **Disegna con questo** chiude la finestra di dialogo e avvia il disegno di muri di questo tipo.

Una modifica a un tipo cambia tutti i muri di quel tipo.

**Importa…**, **Esporta CSV** ed **Esporta JSON**, sotto l’elenco dei tipi, portano i tipi di muro e di solaio da un progetto a un altro, oppure in un foglio di calcolo e ritorno: il CSV ha una riga per strato (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), le righe di un tipo una dopo l’altra, e vengono letti anche il punto e virgola e la virgola decimale di Excel. All’importazione, un tipo con lo stesso nome prende gli strati del file e gli altri vengono creati, in un unico passo da annullare; un materiale che manca al progetto viene creato con il suo nome. L’abaco **Stratigrafie dei tipi** (Abachi → + → Stratigrafie dei tipi) elenca ogni tipo di muro e di solaio con la sua sigla, il suo spessore totale, quanti elementi lo usano e i suoi strati in ordine.

## Modanature e scanalature di muro

**Architettura → Modanatura di muro** (<kbd>WB</kbd>) fa correre un profilo lungo la faccia di un muro: una cornice, una fascia, uno zoccolo, un davanzale. **Scanalatura di muro** (<kbd>RV</kbd>) lo incide nel muro: una scanalatura, una fuga d'ombra. Scegli il profilo e l'altezza nella barra della riga di comando, poi fai clic sulla faccia del muro. In 3D la modanatura va all'altezza su cui fai clic; in pianta all'altezza digitata, dal lato su cui fai clic. **dall'alto** misura l'altezza verso il basso dalla sommità del muro, per una cornice sotto la gronda. **Modanatura sui muri selezionati** e **Scanalatura sui muri selezionati** mettono la stessa cosa su ogni muro selezionato, sulla sua faccia esterna.

Una modanatura corre per tutta la lunghezza della faccia e si interrompe alle porte e alle finestre che attraverserebbe. Dove due muri si incontrano in un angolo ed entrambi portano una modanatura dello stesso profilo alla stessa altezza sulle facce che si incontrano, le due si uniscono a quartabuono, un'unica cornice che gira l'angolo. Seleziona una modanatura per cambiarla nelle Proprietà: il suo profilo, il suo materiale, la sua faccia (**Cambia lato**), la sua altezza, la distanza dalla faccia, dove inizia e finisce lungo il muro e se si interrompe alle aperture. Le proprietà di un muro elencano le modanature e le scanalature che porta.

Modanature e scanalature seguono il loro muro quando viene copiato, spostato su un altro livello o eliminato. Si misurano in lunghezza e volume (per una scanalatura, il volume tolto). In IFC una modanatura è un IfcCovering MOLDING e una scanalatura un IfcOpeningElement RECESS nel suo muro; entrambe tornano com'erano.

![Cornici, fasce e una scanalatura sui muri esterni di una casa](shots/wall-sweeps-3d.webp)

### Modanature sul tipo di muro

Una modanatura che tutti i muri di un tipo devono avere (una fascia di zoccolo, una cornice, una fuga d'ombra) appartiene al tipo. In **Architettura → Tipi di muro**, sotto gli strati, **Modanature e scanalature** le elenca, una riga ciascuna: il profilo, **Modanatura di muro** o **Scanalatura di muro**, la faccia (D, la faccia del primo strato del tipo, o S, come le chiama la legenda), l'altezza sopra la base del muro (o sotto la sua sommità con **Dall'alto**), lo scostamento in fuori dalla faccia, il materiale di una modanatura e i suoi risvolti. **Modanatura di muro** e **Scanalatura di muro** sotto la tabella aggiungono una riga. Salva il tipo: tutti i suoi muri le ricevono, e così un muro disegnato con il tipo o cambiato a esso. Sono modanature come le altre: misurate, nell'abaco delle modanature di muro, tagliate a 45° negli angoli dove si incontrano muri del tipo, scritte in IFC. Le modanature di un muro capovolto cambiano faccia con i suoi strati. **Esporta JSON**, sotto l'elenco dei tipi, le porta con sé, con il contorno dei profili e i materiali per nome (il CSV ha solo gli strati).

Selezionane una su un muro: le Proprietà dicono **Dal tipo**. Se cambi la sua altezza, il suo scostamento o un altro campo, quel campo diventa proprio del muro (**Propri:** altezza): le modifiche successive al tipo non lo toccano. **Torna al tipo** le ridà i valori del tipo. **Spegni su questo muro**, o eliminarla, lascia la modanatura del tipo spenta su quel muro; **Riaccendi le modanature del tipo**, in **Modanature e scanalature** del muro, le riporta.

![Una modanatura sul tipo di muro, nella finestra Tipi di muro](shots/wall-type-sweeps.webp)

### Risvolti

Una modanatura che arriva a un'estremità libera del suo muro (nessun muro collegato lì) fa un risvolto: tagliata a 45° all'estremità, il suo profilo gira l'angolo sulla testata del muro e si ferma nel piano della faccia, così la modanatura è chiusa invece di mostrare il taglio. **Risvolti alle estremità libere**, nelle Proprietà, sceglie entrambi (il predefinito per una modanatura), all'inizio, alla fine o nessuno, per estremità diritte. Una scanalatura non fa mai risvolto. In un angolo collegato a un altro muro una modanatura non fa risvolto: arriva fino all'angolo, o entra in una modanatura uguale sull'altro muro. A porte e finestre si ferma diritta; spunta **Risvolti alle aperture** perché faccia risvolto anche attorno alle mazzette. **Arretrato dall'inizio** e **Arretrato dalla fine** la fermano prima delle estremità della faccia (diritta lì), e **Profilo capovolto** capovolge il suo profilo.

### Spostare e copiare una modanatura

Seleziona una modanatura e usa **MOVE** o **COPY** come per qualsiasi elemento; il suo muro resta fermo. Ciò che lo spostamento percorre lungo il muro la fa scorrere su di esso (una modanatura lungo tutta la faccia riceve allora un inizio e una fine propri); ciò che percorre attraverso il muro viene ignorato. Spostata in su o in giù in un prospetto o in una sezione, cambia la sua altezza. Una copia finisce sullo stesso muro, spostata, o su un altro muro quando ci cade dentro, sulla faccia su cui cade. **ROTATE** e **MIRROR** la tengono sul suo muro: specchiata rispetto all'asse del muro, passa sull'altra faccia. Una modanatura del tipo che sposti tiene il nuovo posto come proprio; spostata su un altro muro, diventa una modanatura propria di quel muro e quella del tipo resta spenta sul primo.

### Profili

**Gestisci → Profili** contiene i profili del progetto, i contorni con cui si estrudono modanature, scanalature e travi di fondazione. **Nuovo profilo** parte dal catalogo con le sue misure: un rettangolo, una fascia smussata, una cornice, un mezzo tondo, un naso di davanzale, una fascia con gocciolatoio; una scanalatura a V, rettangolare o tonda; per le fondazioni, rettangolare, a 2 o 3 gradoni, con la faccia superiore inclinata, eccentrica, con muretto. Oppure scegli **Vertici digitati**: ogni vertice u v in millimetri, separati da un punto e virgola, e un terzo numero per fare del lato che parte da quel vertice un arco. **Da un contorno disegnato** legge una polilinea chiusa o un rettangolo disegnato con Disegno e selezionato nella sua vista. Il cerchio nell'anteprima è il punto di inserimento: per una modanatura sta sulla faccia del muro all'altezza della modanatura, con u verso l'esterno della faccia (dentro il muro per una scanalatura). Se cambi un profilo, tutto ciò che lo usa lo segue.

![La finestra Profili](shots/profiles.webp)

### Rivestimenti

Modella i rivestimenti con gli strati del muro stesso, non con le modanature. Aggiungi uno strato di finitura all'esterno del tipo di muro (pietra, doghe di legno, fibrocemento, un cappotto), con il suo spessore e il suo materiale. **Da** e **Fino a** gli danno un'altezza propria (uno zoccolo in pietra fino a 600, intonaco sopra), e il pulsante **strati** dello strato ne cambia il materiale a fasce lungo l'altezza. Per un rivestimento staccato dal muro, disegna un muro a sé sulla faccia (l'opzione di linea **Faccia esterna** dello strumento muro) con un tipo fatto degli strati del rivestimento. Il rivestimento posato doga per doga (doghe verticali o orizzontali, listelli) non è ancora modellato.

## Muri sotto un tetto

I muri sullo stesso livello di un tetto si fermano da soli sotto di esso e salgono fino ai suoi timpani. Per un muro su un altro livello, selezionalo e scegli **Architettura → Collega al tetto** (<kbd>AR</kbd>), poi fai clic sul tetto, o su qualsiasi altro corpo fino a cui il muro deve salire: un solido di Studio, un elemento importato. Vedi [Solai e tetti](floors-roofs.md).
