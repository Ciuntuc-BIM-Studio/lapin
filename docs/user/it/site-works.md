---
title: Opere provvisionali
order: 55
summary: Ponteggi di facciata e casseforme per solai e travi, disposti a partire dal modello dai loro configuratori.
---

La scheda **Opere provvisionali** modella le opere temporanee del cantiere: i ponteggi di facciata e le casseforme di solai e travi. Lapin le dispone a partire dal modello e conta ciò che serve. Quando il modello cambia, lo seguono.

![La scheda Opere provvisionali](shots/ribbon-site-works.webp)

Ponteggi e casseforme sono **temporanei**: vengono montati e smontati nella fase **Esecuzione**. Il primo che posizioni aggiunge questa fase se il progetto non ce l’ha, e la rende la fase attiva. Le viste delle fasi precedenti li escludono. Vedi **Gestisci → Fasi**.

![Un ponteggio attorno alla casa, la cassaforma del primo piano sui suoi puntelli](shots/site-works-3d.webp)

## Ponteggi

- **Ponteggio sui muri** (<kbd>WS</kbd>): seleziona i muri (Shift-clic per aggiungerne altri), poi premilo. Il ponteggio gira attorno alle loro facce esterne, alla distanza dal muro definita dal suo tipo, fino alla sommità dei muri. Chiude da solo i suoi angoli. Sposta un muro o rendilo più alto, e il ponteggio lo segue.
- **Ponteggio** (<kbd>FS</kbd>): disegna la sua linea in pianta lungo la facciata e premi Enter per terminare. Il ponteggio sta a sinistra della linea. Scegli l’altro lato nelle sue proprietà.

Ogni tratto rettilineo viene diviso nelle lunghezze di campata del tipo, con il minor numero di campate che ne raggiunge la fine. Gli impalcati salgono finché l’impalcato superiore non è a portata della quota di lavoro. Il parapetto sta sopra l’impalcato superiore. I piedi poggiano sul terreno, su basette regolabili. Se il terreno scende più di quanto le basette possano compensare, Lapin lo segnala.

![Le proprietà di un ponteggio](shots/scaffold-properties.webp)

Nelle sue proprietà puoi impostare la **quota di lavoro**: lasciala vuota per la sommità dei muri. Puoi anche scegliere se i piedi poggiano **sul terreno** o **sul piano**, oppure **scollega** il ponteggio dai muri.

Ogni ponteggio e cassaforma ha un **tipo**. Lo scegli in cima alle proprietà, oppure dai il tipo di un elemento ad altri con Corrispondenza tipo (MA). Sotto **Parametri del tipo** ci sono i valori che imposta il configuratore: il sistema, le lunghezze di campata, la larghezza, l’altezza dell’impalcato, il compensato, gli interassi, la voce. Puoi modificarli anche lì. Una modifica appartiene al tipo, quindi ogni elemento di quel tipo la segue. Crea simile (CS) disegna un nuovo elemento dello stesso tipo. Un tipo che crei nel configuratore è quello che prende il ponteggio o la cassaforma successivi.

**Tipi di ponteggio…** apre il configuratore. Imposta il sistema (modulare o a telai), le lunghezze di campata, la larghezza e l’altezza dell’impalcato, la distanza dal muro, gli impalcati, i parapetti e le tavole fermapiede, gli ancoraggi, le basette, le campate di accesso e la rete di protezione. Il prospetto mostra un tratto della lunghezza e dell’altezza che scegli, con ciò che conta. Con un ponteggio selezionato, il configuratore si apre sul suo tipo; fai clic su un altro nell’elenco per modificare quello. L’elenco indica quanti ponteggi ha ogni tipo. **Salva tipo** applica la modifica a ogni ponteggio del tipo.

![Il configuratore dei ponteggi](shots/scaffold-configurator.webp)

## Casseforme

- **Casseforme** (<kbd>FW</kbd>): seleziona solai e travi, poi premilo. Ognuno riceve la cassaforma di tutte le sue facce casserabili.
- **Cassaforma su faccia** (<kbd>FF</kbd>): scegli una faccia. In 3D la faccia sotto il cursore si illumina con il suo nome: l’intradosso di un solaio, il fondo di una trave o uno dei suoi lati. In pianta, fai clic sul bordo di un solaio o all’interno del suo contorno. Una faccia scelta in seguito va sulla stessa cassaforma.

La **cassaforma di un solaio** ne copre l’intradosso con compensato su travi secondarie, su travi principali, su puntelli. Le travi sotto il solaio non vengono casserate di nuovo, né i muri e i pilastri su cui poggia o le sue aperture. Gli interassi vengono dalla tabella del tipo, in base allo spessore del solaio. I puntelli scendono fino al solaio sottostante, al terreno o al piano sottostante. Anche i bordi liberi vengono casserati, alti quanto il solaio più un risvolto. Nelle proprietà scegli quali facce casserare e la direzione delle travi principali.

La **cassaforma di una trave** è il suo fondo, su traversi e puntelli, e i suoi lati. Ogni lato è alto quanto la trave sotto il solaio gettato con essa. Pilastri e travi più alte la interrompono. Puoi aggiungere listelli smussati lungo gli spigoli inferiori.

**Tipi di cassaforma…** apre il configuratore, per solai e per travi. Per i solai imposta il sistema (travi in legno e puntelli, oppure pannelli), il compensato, le travi e le loro lunghezze di catalogo, la tabella degli interassi, i bordi, i puntelli e i reimpieghi. Per le travi imposta i traversi, i puntelli, i morsetti in base all’altezza della trave, lo smusso e i reimpieghi. La sezione mostra gli strati e il puntello che il motore sceglie per l’altezza indicata. Con una cassaforma selezionata, si apre sul tipo e sul genere di quella cassaforma.

![Il configuratore delle casseforme](shots/formwork-configurator.webp)

## Muri, pilastri, cordoli e fondazioni

**Casseforme** e **Cassaforma su faccia** funzionano anche su muri, pilastri e fondazioni:

- **Un muro** riceve entrambe le facce e le sue estremità libere, tolte le porte e le finestre. Un muro a strati con un cordolo o una fascia in calcestruzzo riceve solo quelli. Nelle sue proprietà puoi scegliere entrambe le facce, i cordoli o le estremità libere.
- **Un pilastro** riceve i suoi lati, fino al solaio o alla trave gettati sopra di esso.
- **Una fondazione** riceve i lati di ogni gradino di un plinto, oppure i lati della suola e della costola di una trave di fondazione.

Ciò che sta contro una faccia viene escluso: un altro muro, un pilastro. Un solaio gettato sopra ferma la faccia al suo intradosso. Il tipo imposta i pannelli (le loro larghezze e altezze), i tiranti attraverso i muri, i morsetti per pilastri e i puntelli di spinta.

![Il configuratore per muri e pilastri](shots/wall-formwork-configurator.webp)

## Pianta delle casseforme

**Pianta delle casseforme** crea una pianta del piano attivo nella fase Esecuzione, tagliata un metro sopra il pavimento, **vista dal basso**: una pianta del controsoffitto. I puntelli sono tagliati. La cassaforma sopra di essi è disegnata come la vedresti guardando in alto: le travi principali e secondarie, il compensato e i bordi. È orientata come la pianta, così le due coincidono.

**Pianta delle casseforme dall’alto** crea la stessa pianta vista dall’alto, con la cassaforma disegnata tratteggiata sopra il taglio. Qualsiasi pianta può essere commutata tra le due nelle sue proprietà (**Visto da**) o in **Intervallo di vista**.

![La pianta delle casseforme del piano terra](shots/formwork-plan.webp)

## Che cosa contano

Ogni ponteggio e cassaforma elenca le sue quantità nelle proprietà:

- l’area del ponteggio, le campate, gli impalcati, i montanti, le tavole e gli ancoraggi;
- l’area di contatto della cassaforma per faccia, i puntelli per misura e le travi per lunghezza di catalogo.

**Abaco delle casseforme** e **Abaco dei ponteggi** aggiungono le loro tabelle al browser di progetto, sotto Abachi, da mettere su una tavola o esportare.

L’abaco delle casseforme si chiude con una riga per tipo:

- **il set**: la massima quantità di cassaforma gettata in una volta. È per fase di esecuzione quando il lavoro è diviso in più fasi, altrimenti per piano.
- **da acquistare**: il set, oppure il totale diviso per i reimpieghi del tipo, il maggiore dei due.

Ogni tipo ha la sua **voce** per il computo metrico: il codice nel prezzario usato dal tuo preventivo e il suo testo. Li inserisci nel configuratore, e l’abaco li mostra su ogni riga.

In IFC vengono scritti come assiemi (`IfcElementAssembly` FORMWORK e SCAFFOLD) con lo stato TEMPORARY. I pezzi sono membrature, piastre e accessori. Ognuno ha la sua risorsa di prodotto da costruzione, e la cassaforma è collegata all’elemento che cassera.
