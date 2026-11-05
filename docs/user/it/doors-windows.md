---
title: Porte e finestre
order: 30
summary: Posiziona porte e finestre nei muri, spostale e progetta i loro tipi nel configuratore.
---

<!-- video doors-windows -->

## Posizionare una porta o una finestra

Scegli **Architettura → Porta** (<kbd>DR</kbd>) o **Finestra** (<kbd>WN</kbd>), poi fai clic su un muro dove vuoi l’apertura. Lapin taglia l’apertura in ogni strato del muro e aggiunge l’architrave richiesto dal tipo di muro.

Mentre Porta o Finestra è attivo, il pannello Proprietà mostra il tipo che verrà posizionato, con le sue dimensioni: scegline un altro lì prima di fare clic sul muro.

![Una pianta con una porta e delle finestre](shots/openings-plan.webp)

Per spostare una porta o una finestra lungo il suo muro, selezionala e trascinala. Per modificarla, usa il pannello Proprietà: il suo tipo, l’altezza del davanzale e il lato verso cui si apre (**Lato**). Con più porte e finestre selezionate, **Verso**, **Lato**, il davanzale e il tipo le cambiano tutte in un solo passo, così come una freccia di inversione in pianta.

Per una distanza esatta, usa **Sposta** (<kbd>M</kbd>) o **Copia** (<kbd>CO</kbd>) nella riga di comando, come per qualsiasi elemento: seleziona la porta, la finestra o l’apertura nel muro, fai clic su un punto base, poi digita la distanza (`500`) o fai clic su un secondo punto. Conta solo la parte dello spostamento lungo il muro: l’apertura scorre lungo il suo muro e mantiene il davanzale. Spostata in un altro muro dello stesso piano, passa in quel muro e si apre ancora allo stesso modo nella stanza. Una copia è una nuova porta o finestra dello stesso tipo, con il numero successivo. Se non ci sta (oltre la fine del muro o sopra un’altra apertura), non si sposta nulla. **Ruota** e **Specchia** la collocano allo stesso modo, e la specchiatura ne cambia il verso.

![Le proprietà di una porta](shots/door-properties.webp)

## Aperture senza porta né finestra

**Architettura → Apertura nel muro** (<kbd>WO</kbd>) taglia una semplice apertura attraverso un muro dove fai clic: un passaggio, una botola, un vano per una porta che arriverà più tardi. Imposta la sua larghezza, altezza e davanzale nel pannello Proprietà prima del clic. Quando è selezionata, lì se ne cambiano le dimensioni, la posizione lungo il muro e il davanzale; come una porta, deve stare nel muro e non interferire con le altre aperture del muro. In IFC è un’apertura che svuota il muro, senza nulla che la riempia.

Selezionata, un’apertura ha maniglie come una finestra: trascina il suo centro per farla scorrere lungo il muro, o uno stipite per allargarla o restringerla mentre l’altro resta fermo; la lunghezza accanto al mouse si può scrivere, come quando disegni. Si sceglie con un clic nel suo vano in pianta, o nel suo foro nella vista 3D, dove si accende il suo contorno. **Sposta** (<kbd>M</kbd>) la fa scorrere lungo il muro di una distanza scritta, e la sua altezza e il davanzale sono nel pannello delle proprietà.

**Forma superiore** dà la forma della sua testa: **Rettangolo**, **Arco ribassato** (un arco poco rialzato), **Arco ellittico** (mezza ellisse, alta un quarto della larghezza se non dici altrimenti), **Semicerchio** o **Angoli arrotondati** (una testa piana con gli angoli superiori arrotondati). **Freccia dell’arco** stabilisce quanto sale una testa ribassata o ellittica, **Raggio degli angoli** quanto sono arrotondati gli angoli (150 mm se non dici altrimenti, mai più di metà della larghezza); vuoto, quello della forma. L’apertura nel muro segue il contorno in 3D, nei prospetti e nelle sezioni e nell’IFC; la pianta ne mostra le spalle come prima.

## Il configuratore

**Architettura → Porte e finestre** apre il configuratore. Tutti i tipi di porta e finestra del progetto sono elencati a sinistra. Puoi modificare:

- il nome e la sigla del tipo (la lettera nelle etichette);
- la sua larghezza e altezza;
- la profondità e la larghezza del telaio;
- le sue colonne e righe di pannelli, e come si apre ogni pannello;
- la finitura della mazzetta, la cornice in facciata, la soglia e i colori.

I disegni a destra mostrano il tipo come verrà disegnato in prospetto, in pianta e in sezione, mentre lo modifichi.

**Sinistra e destra.** Il verso di una porta o di una finestra si chiama come lo chiama IFC: visto da chi la spinge per aprirla. Una porta *swing left* ha le cerniere alla sinistra di chi la spinge, quindi dalla stanza verso cui si apre sono a destra. La griglia **Ante** è vista dall’esterno, il lato da cui si spingono le ante, così la sua sinistra è quella dei nomi. Il prospetto è visto dall’interno, il lato verso cui si aprono le ante, a meno che non sia spuntato **Dall’esterno**; come la pianta, mostra la porta com’è nel modello. Una porta invertita sull’altro verso (**Verso**, o una freccia di inversione lungo il muro) è l’immagine speculare del suo tipo; gli abachi la mettono su una scheda a sé (vedi [Abachi](schedules.md)).

![Il configuratore di porte e finestre](shots/joinery-configurator.webp)

**Salva tipo** conserva le tue modifiche. Tutte le porte o finestre di quel tipo le seguono. **Posiziona con questo** chiude il configuratore e avvia il posizionamento di quel tipo.

### Finestre con più ante

**Colonne di ante** accetta le larghezze relative delle ante, un numero per ciascuna: `1 1` sono due ante uguali, `1 1 1` tre, `1 2 1` una larga tra due strette. **Righe di ante** divide la finestra in altezza nello stesso modo, dal basso verso l’alto: `1 1` dà due righe. Sei ante sono `1 1 1` in Colonne di ante e `1 1` in Righe di ante. Poi, in **Pannelli**, scegli come si apre ogni anta: fissa, a battente, ad anta e ribalta, a vasistas, a sporgere, a bilico o scorrevole.

### Scuri, grate, fioriere e pensiline

<!-- video window-dressings -->

Sotto **Sulla facciata**, un tipo di finestra può avere:

- **Scuri**: a lamelle, a pannello o a doghe, a una o due ante, mostrati aperti (ripiegati contro il muro accanto alla finestra) o chiusi su di essa;
- una **Grata di sicurezza**: nella mazzetta sul lato del locale (l’impostazione predefinita, dove di solito si mettono le grate di sicurezza), nella mazzetta sul lato strada, sulla facciata o sulla faccia interna, con barre orizzontali, verticali o entrambe, la loro dimensione e il loro interasse;
- una **Fioriera** sotto la finestra;
- una **Pensilina** sopra di essa, con il suo aggetto e la sua pendenza.

Ciascuno si posiziona rispetto alle facce reali del muro dove si trova la finestra, ricavate dagli strati del muro, così resta corretto su qualsiasi tipo di muro. Quando la finestra ha una cornice, ciò che sta sulla facciata si scosta dalla cornice. Puoi spostare ciascuno a mano: **Dalla faccia del muro** stabilisce quanto si scosta dal muro (per una grata nella mazzetta, **Arretrata dalla faccia** stabilisce quanto rientra), **Dall’asse della finestra** lo sposta lungo il muro, e **Su o giù** lo sposta in altezza. Si vedono in 3D, nelle piante, nei prospetti e nelle sezioni, e gli abachi li misurano.

![Scuri, una grata, una fioriera e una pensilina nel configuratore](shots/joinery-dressings.webp)

![Le finestre complete di accessori, in 3D](shots/window-dressings-3d.webp)

### Forme, stili e ornamenti

**Forma** dà a una porta o a una finestra una testa ad arco (**Arco a tutto sesto**, **Arco ribassato**, **Arco ellittico**, **Angoli arrotondati**, **Arco a sesto acuto**, **Coronamento triangolare**) oppure la rende una finestra **Tonda o ovale** (tonda quando larghezza e altezza sono uguali). Un tipo ad arco tiene le sue ante sotto un traverso alla linea d’imposta, e sopra di esso un sopraluce fisso che segue la testa, con barre se le vuoi: **A raggiera**, **I montanti prolungati** o una **Croce**. **Freccia dell’arco** stabilisce quanto sale una testa ribassata, ellittica, a sesto acuto o triangolare, **Raggio degli angoli** quanto sono arrotondati gli angoli di una testa ad angoli arrotondati. L’apertura nel muro, il telaio, il vetro, la cornice e i disegni seguono tutti il contorno.

Le **Inglesine** dividono le ante: **Griglia (coloniale)** con i suoi riquadri in larghezza e in altezza, **Prairie** con barre vicino ai bordi, oppure **Losanghe**.

Sulla facciata, oltre alla cornice, una finestra può avere una **Chiave di volta**, un **Coronamento sull’apertura** (una **Cornice**, un **Timpano**, un **Timpano curvo**, oppure un **Gocciolatoio** che segue una testa ad arco) e un **Pannello sotto il davanzale** con le sue mensole.

**Applica uno stile…** imposta in un colpo solo la forma, le inglesine e gli ornamenti: Moderno, Classico, Georgiano, Vittoriano, Romanico, Barocco, Gotico, Tudor, Prairie, Tradizionale, oppure una finestra tonda. Tutto ciò che imposta resta modificabile.

![Una finestra barocca: un arco a tutto sesto con raggiera, una chiave di volta e un timpano curvo](shots/joinery-styles.webp)

![Finestre ad arco in 3D](shots/window-styles-3d.webp)

### Maniglie

**Maniglia** sceglie la maniglia del tipo: una **Maniglia a leva**, un **Pomolo**, un **Maniglione** o una **Maniglia per finestra**, oppure **Nessuno**. Per impostazione predefinita una porta ha una maniglia a leva e una finestra una maniglia per finestra. Una porta ha la maniglia su entrambe le facce, all’**Altezza della maniglia** che imposti; **Colore della maniglia** ne imposta il colore. Le maniglie si vedono in 3D, nelle sezioni e nei prospetti, e nei disegni del configuratore quando **Ferramenta** è spuntato.

### Parametri

**Parametri · negli abachi**, in fondo al configuratore, contiene i parametri propri del tipo: **+ Resistenza al fuoco** (tenuta in Pset_DoorCommon o Pset_WindowCommon di IFC come FireRating), **+ Produttore**, **+ Codice prodotto**, **+ Sito web**, o qualsiasi parametro a cui dai un nome. Le porte e le finestre del tipo li ereditano, e ognuno diventa una riga degli abachi dei serramenti: vedi [Abachi e grafici](schedules.md).

## Etichette in pianta

**Annota → Etichette di porte e finestre** mette un contrassegno su ogni porta e finestra di una pianta: una linea attraverso il muro, la sigla all’esterno, e le dimensioni e il davanzale all’interno. Per cambiare l’aspetto delle etichette, vai in **Gestisci → Standard di disegno → Etichette**.

### Modificare un contrassegno a schermo

Ogni contrassegno si può disporre a mano, parte per parte. Fai clic sulla sua linea o su uno dei suoi testi per selezionarlo: ogni testo ha allora un grip quadrato (trascinalo per spostare il testo) e uno rotondo oltre la sua fine (trascinalo per ruotare il testo), e ogni estremità della linea ha un grip (trascinalo per allungare o accorciare la linea; il testo a quell’estremità la segue). Ctrl/⌘-clic su un grip rimette quella parte com’era. Ogni trascinamento è un passo di annullamento. Con più contrassegni della stessa pianta selezionati, una parte trascinata su uno cambia la stessa parte di tutti.

![Il contrassegno di una porta selezionato: un grip per ogni testo, il suo grip di rotazione e uno a ogni estremità della linea](shots/marker-grips.webp)

![La sigla spostata e ruotata, l’estremità esterna della linea allungata](shots/marker-edited.webp)

Le modifiche appartengono al contrassegno, e quindi a quella pianta: la stessa porta può essere disposta diversamente in un’altra pianta. Sono tenute lungo e attraverso il muro, quindi seguono la porta quando si sposta, ruota o passa dall’altra parte. Nelle proprietà del contrassegno, **Contrassegno** elenca le sue parti: scrivi la rotazione di un testo in gradi o di quanto arriva un’estremità in millimetri su carta, rimetti una parte con ↺, o tutte con **Riporta l’etichetta al suo posto**. Il motore disegna i contrassegni come modificati, quindi tavole, PDF e DXF li stampano uguali.

![Le parti di un contrassegno nelle sue proprietà](shots/marker-properties.webp)

Se i testi stanno attraverso la linea del contrassegno (lungo il muro, come predefinito) o lungo di essa si sceglie per ogni tipo di etichetta, in **Gestisci → Standard di disegno → Etichette**, colonna **Direzione del testo**: **perpendicolare alla linea dell’etichetta** o **lungo la linea dell’etichetta**. In entrambi i casi sono disegnati leggibili, mai capovolti; anche un testo ruotato a mano resta leggibile.

![I testi del contrassegno della porta lungo la linea dell’etichetta](shots/marker-along.webp)
