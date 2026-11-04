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
- I muri si uniscono da soli dove si incontrano: un angolo a quartabuono, un muro che termina su un altro a T, uno che ne attraversa un altro a X. A un T o a un X gli strati si compenetrano come si costruiscono: il nucleo del muro che termina prosegue attraverso le finiture dell’altro fino al suo nucleo, e le sue finiture si fermano alla faccia dell’altro, dove incontrano le loro simili, così la pianta mostra un’unica stratigrafia senza una linea in mezzo. **Unisci** (<kbd>JW</kbd>), **A quartabuono** e **Di testa** cambiano il modo in cui due muri si incontrano quando non è quello che vuoi; **Nuovi angoli a quartabuono** rende la giunzione di testa quella predefinita su questo computer. In entrambi i casi, ogni strato gira l’angolo per conto suo: le finiture incontrano le loro simili e proseguono senza interruzioni, e i nuclei si incontrano sulla diagonale (quartabuono) oppure un nucleo prosegue fino alla faccia esterna dell’altro (di testa), così una pianta di muri a strati mostra angoli puliti.
- **Scambia** cambia quale dei due muri passa.

**La linea su cui disegni.** Mentre lo strumento Muro è attivo, l’elenco nella barra della riga di comando sceglie la linea su cui fai clic: **Asse del nucleo** (l’asse, come prima), **Nucleo, faccia esterna**, **Nucleo, faccia interna**, **Faccia esterna** o **Faccia interna**. L’esterno è la destra del muro nel senso in cui disegni (disegna intorno a un edificio in senso antiorario per averlo all’esterno). L’asse del muro resta sul suo nucleo, scostato dalla linea, e i muri disegnati uno dopo l’altro sulla stessa faccia si incontrano sull’angolo di quella faccia.

## Modificare un muro

Fai clic su un muro per selezionarlo. Compaiono le sue maniglie. Trascina le estremità per allungarlo o accorciarlo, oppure trascina la croce al centro per spostarlo.

![Un muro selezionato con le sue maniglie](shots/wall-selected.webp)

Il pannello Proprietà mostra il muro selezionato. La parte superiore del pannello contiene il suo tipo e i suoi strati. Sotto ci sono la lunghezza, l’altezza, l’offset di base, il piano e la fase. Modifica un valore qualsiasi e il modello si aggiorna.

![Le proprietà di un muro](shots/wall-properties.webp)

Più in basso, **Dipendenze** mostra cosa è costruito dal muro e da cosa è costruito il muro. **Usato da** elenca gli elementi che cambiano quando cambia il muro: i muri a cui si unisce, le sue porte e finestre, un muro collegato a esso. Fai clic su un nome per selezionare l’elemento. **Costruito da** elenca gli elementi da cui dipende il muro, poi il suo tipo e i suoi materiali. Quando elimini un elemento da cui altri dipendono, un messaggio nomina quelli ricostruiti; <kbd>Ctrl</kbd> <kbd>Z</kbd> riporta tutto com’era.

**Capovolgi** porta gli strati del muro dall’altro lato. Puoi anche fare clic sulle piccole frecce mostrate accanto a un muro selezionato in pianta.

## Tipi di muro

Un tipo di muro è un insieme di strati, dalla faccia esterna alla faccia interna. Ogni strato ha un materiale e uno spessore, e uno strato è il **nucleo**. La linea del muro corre lungo il nucleo. Per modificare i tipi, scegli **Architettura → Tipi di muro**. Uno strato diverso dal nucleo può avere un’altezza propria: **Da** e **Fino a** stabiliscono fin dove arriva sopra la base del muro (uno zoccolo piastrellato fino a 1500, un rivestimento da 300 in su); se vuoti, lo strato corre per tutta l’altezza.

Uno strato diverso dal nucleo può anche cambiare materiale lungo la sua altezza: il pulsante **strati** alla fine della sua riga aggiunge un pezzo (piastrelle, 2100 per impostazione predefinita), dalla base dello strato in su; aggiungine altri per una fascia (pittura fino a 900, piastrelle fino a 1500), e sopra l’ultimo pezzo lo strato prosegue nel suo materiale fino in cima. Ogni pezzo è disegnato, tagliato in pianta e in sezione e misurato nel suo materiale.

Uno strato diverso dal nucleo può avere spessore 0, per un materiale troppo sottile da modellare ma che va misurato: una guaina impermeabile, una barriera al vapore, la pittura. Non forma alcun solido; piante e sezioni lo disegnano come una linea spessa tratteggiata dove sta tra due strati (su una faccia, la linea della faccia è la sua), il disegno delle Stratigrafie dei tipi lo nomina come gli altri, e si misura a superficie: la faccia dello strato su cui poggia, al netto delle aperture, nelle quantità del muro e nel computo per materiale.

![Uno strato di intonaco con piastrelle fino a 2100 e l’intonaco sopra](shots/wall-layer-parts.webp)

![Tipi di muro: gli strati di un muro in laterizio](shots/wall-types.webp)

- **+ Strato** aggiunge uno strato. Le frecce spostano uno strato in su o in giù.
- **Zone in altezza** divide il nucleo dal basso verso l’alto, per esempio un primo corso, la muratura e un cordolo.
- **Architravi** sceglie gli architravi posti sopra porte e finestre: prefabbricati o gettati in opera, con il loro appoggio e la loro altezza.
- **Disegna con questo** chiude la finestra di dialogo e avvia il disegno di muri di questo tipo.

Una modifica a un tipo cambia tutti i muri di quel tipo.

**Importa…**, **Esporta CSV** ed **Esporta JSON**, sotto l’elenco dei tipi, portano i tipi di muro e di solaio da un progetto a un altro, oppure in un foglio di calcolo e ritorno: il CSV ha una riga per strato (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), le righe di un tipo una dopo l’altra, e vengono letti anche il punto e virgola e la virgola decimale di Excel. All’importazione, un tipo con lo stesso nome prende gli strati del file e gli altri vengono creati, in un unico passo da annullare; un materiale che manca al progetto viene creato con il suo nome. L’abaco **Stratigrafie dei tipi** (Abachi → + → Stratigrafie dei tipi) elenca ogni tipo di muro e di solaio con la sua sigla, il suo spessore totale, quanti elementi lo usano e i suoi strati in ordine.

## Muri sotto un tetto

I muri sullo stesso livello di un tetto si fermano da soli sotto di esso e salgono fino ai suoi timpani. Per un muro su un altro livello, selezionalo e scegli **Architettura → Collega al tetto** (<kbd>AR</kbd>), poi fai clic sul tetto, o su qualsiasi altro corpo fino a cui il muro deve salire: un solido di Studio, un elemento importato. Vedi [Solai e tetti](floors-roofs.md).
