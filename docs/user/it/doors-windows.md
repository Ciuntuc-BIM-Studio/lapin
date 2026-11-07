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

**Finitura della mazzetta** riveste gli stipiti e la parte superiore dentro il foro: **Intonaco** (15 mm salvo indicazione) o un **Rivestimento** (20 mm di legno), con **Spessore della finitura** e **Materiale della finitura**; un’apertura sopra il pavimento è rivestita anche sotto. La finitura fa parte del corpo del muro, in 3D, prospetti, sezioni e quantità, e la luce netta è ciò che lascia. **Architrave** è **La regola del muro** (l’architrave che il tipo di muro chiede, come sopra una porta) o **Nessuno**. Più aperture selezionate cambiano insieme, in un solo passo di annullamento.

**Modifica l’apertura…** apre il suo configuratore: gli stessi campi sopra un disegno dell’apertura nel suo muro, applicati insieme. **Cambia in porta…** e **Cambia in finestra…** la riempiono con una porta o una finestra del tipo scelto, dov’è: posizione, davanzale e GlobalId restano, e **Adatta il tipo all’apertura** le dà un tipo della sua misura e forma (quello del progetto di quella misura, altrimenti uno fatto dal tipo scelto e chiamato con la misura). Al contrario, **Cambia in apertura** su una porta o finestra selezionata la toglie e lascia il foro come apertura della stessa misura e forma. Ognuno è un solo passo di annullamento.

![Un’apertura nel muro nel suo configuratore: arco a tutto sesto, mazzette intonacate](shots/wall-opening-dialog.webp)

## Il configuratore

**Architettura → Porte e finestre** apre il configuratore. Tutti i tipi di porta e finestra del progetto sono elencati a sinistra. Puoi modificare:

- il nome e la sigla del tipo (la lettera nelle etichette);
- la sua larghezza e altezza;
- la profondità e la larghezza del telaio;
- le sue colonne e righe di pannelli, e come si apre ogni pannello;
- la finitura della mazzetta, la cornice in facciata, la soglia e i colori.

I disegni a destra mostrano il tipo come verrà disegnato in prospetto, in pianta e in sezione, mentre lo modifichi. Sotto, un’**anteprima 3D** mostra il tipo in un pezzo di muro del primo tipo di muro del progetto, con la mazzetta, i davanzali, il rivestimento e i materiali, mentre lo modifichi (anche una finestra a bovindo, ad arco o a cassone): trascina per ruotarla, usa la rotella per lo zoom, trascina con il tasto destro per spostarla. Parte vista dall’esterno.

**Sinistra e destra.** Il verso di apertura di una porta si chiama come lo chiamano gli architetti: guarda dalla maniglia verso le cerniere; un’anta che si apre alla sinistra di questa linea apre a sinistra, una che si apre alla destra apre a destra. Le scelte del configuratore per un’anta lo dicono così, con il lato delle cerniere nell’**Apertura** di una specchiatura, vista dall’esterno, il lato da cui si spingono le ante: *Apertura a destra (cerniere a sinistra)*. Un file IFC chiama il verso a modo suo (le cerniere viste guardando dove si apre l’anta), così la stessa porta vi è scritta SINGLE_SWING_LEFT; Lapin passa dall’uno all’altro quando scrive e legge IFC. Sinistra e destra di una finestra si guardano dall’interno, il lato verso cui si aprono le ante: *Battente a destra (cerniere a sinistra)* nel configuratore, le cui specchiature sono viste dall’esterno. IFC scrive la stessa finestra come sinistra. Il prospetto è visto dall’interno, il lato verso cui si aprono le ante, a meno che non sia spuntato **Dall’esterno**; come la pianta, mostra la porta com’è nel modello. Una porta invertita sull’altro verso (**Verso**, o una freccia di inversione lungo il muro) è l’immagine speculare del suo tipo; gli abachi la mettono su una scheda a sé (vedi [Abachi](schedules.md)).

![Il configuratore di porte e finestre](shots/joinery-configurator.webp)

**Salva tipo** conserva le tue modifiche. Tutte le porte o finestre di quel tipo le seguono. **Posiziona con questo** chiude il configuratore e avvia il posizionamento di quel tipo.

### Specchiature, montanti e profili

Una porta o una finestra è una griglia di specchiature. Ogni specchiatura sceglie come si apre e che cosa la riempie, così un solo tipo può avere due ante con fiancoluce e un sopraluce sopra, o una finestra con un pannello cieco sotto un’anta.

**Colonne (relative)** e **Righe (relative)** dividono il vano in fretta: `1 1` sono due specchiature uguali, `1 1 1` tre, `2 1` una larga accanto a una stretta. Le righe si contano dal basso verso l’alto.

**Larghezze delle colonne** e **Altezze delle righe** danno le stesse misure in millimetri. Scrivile e l’ultima prende il resto. Nel prospetto puoi anche trascinare un montante o un traverso; la distanza si vede mentre trascini.

**Larghezza del montante** e **Larghezza del traverso** hanno misure proprie, a parte la **Larghezza del telaio**. Un telaio di 50 mm con montanti di 50 mm si legge 50 mm ovunque.

**Specchiature** elenca ogni specchiatura. Fai clic su una, nell’elenco o nel prospetto, per modificarla:

- **Apertura**: fissa, un’anta (a battente, ad anta e ribalta, a vasistas, a sporgere, a bilico, scorrevole) o, in una porta, un’anta di porta.
- **Tamponamento**: **Vetro** o **Pannello cieco**. Le ante e i fiancoluce di una porta possono essere ciechi, e così una specchiatura di finestra (un pannello sotto un’anta, una luce opaca).
- **Dividi**: **In colonne** o **In righe**, a una distanza in millimetri dal bordo sinistro o inferiore della specchiatura (a metà, se non indicato). Divisa di traverso, un’anta di porta tiene la parte bassa e quella alta diventa un sopraluce vetrato fisso.
- **Unisci**: con la specchiatura accanto, sopra o sotto, quando hanno in comune un intero lato. I montanti e i traversi che non servono più a nessuna specchiatura spariscono.

![Una porta di negozio in un solo tipo: due ante, fiancoluce e un sopraluce per tutta la larghezza](shots/joinery-panes.webp)

**Sistema di profili** parte da un profilo in **PVC**, **Alluminio**, **Legname** o **Acciaio (sottile)**: imposta le misure di telaio, montanti, traversi e ante e dà ai profili le loro forme fini. Il telaio ha una **Battuta** su cui l’anta chiude, l’anta si sovrappone al telaio per il suo **Sormonto dell’anta**, i **fermavetri** tengono il vetro e una finestra ha un gocciolatoio sotto il traverso inferiore. Ogni misura resta modificabile. **Semplice (profili lisci)** mantiene elementi rettangolari semplici, come prima.

![Una finestra in PVC: le ante che si sovrappongono al telaio, i fermavetri, un pannello cieco sotto una luce fissa](shots/joinery-profiles-3d.webp)

In **Dettagli**, **Davanzale interno** e **Davanzale esterno** attivano e disattivano i davanzali, e **Rivestimento delle spallette** attiva la finitura della mazzetta sulle spalle e sull’architrave. Dove davanzali e rivestimenti non servono, togli la spunta.

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

**Traverso all’imposta dell’arco**, spuntato di serie, tiene quel traverso e il sopraluce fisso sopra di esso. Togli la spunta per una finestra a battente ad arco: la fila superiore delle ante sale nell’arco, e il telaio, l’anta, il vetro, le inglesine e il simbolo d’apertura seguono l’arco tutto intorno, senza barra sotto. L’anta si apre come scegli (a battente, anta-ribalta, fissa), i montanti salgono fino all’arco, e piante, sezioni, prospetti, l’anteprima 3D e l’IFC la mostrano così. Lo stile **Finestra a battente ad arco** mette in una volta un arco ribassato con una sola anta a battente.

![Una finestra a battente ad arco: una sola anta a battente che segue un arco ribassato, senza traverso sotto](shots/joinery-arched-casement.webp)

Le **Inglesine** dividono le ante: **Griglia (coloniale)** con i suoi riquadri in larghezza e in altezza, **Prairie** con barre vicino ai bordi, oppure **Losanghe**.

Sulla facciata, oltre alla cornice, una finestra può avere una **Chiave di volta**, un **Coronamento sull’apertura** (una **Cornice**, un **Timpano**, un **Timpano curvo**, oppure un **Gocciolatoio** che segue una testa ad arco) e un **Pannello sotto il davanzale** con le sue mensole.

**Applica uno stile…** imposta in un colpo solo la forma, le inglesine e gli ornamenti: Moderno, Classico, Georgiano, Vittoriano, Romanico, Barocco, Gotico, Tudor, Prairie, Tradizionale, oppure una finestra tonda. Tutto ciò che imposta resta modificabile. Il menu mostra poi lo stile che ha il tipo; appena cambi qualcosa che lo stile ha impostato, indica *Georgiano (modificato)*, e scegliere di nuovo lo stile lo applica di nuovo.

![Una finestra barocca: un arco a tutto sesto con raggiera, una chiave di volta e un timpano curvo](shots/joinery-styles.webp)

![Finestre ad arco in 3D](shots/window-styles-3d.webp)

### Bow-window, finestre ad arco e a scatola

Un tipo di finestra può sporgere dal muro. Nel configuratore, **Projects** la rende un **Bay** (un centro e due lati inclinati), un **Bow** (pannelli uguali su un arco) o un **Box** (lati a squadra con il muro), e **New bay window** inizia un nuovo tipo come bow-window a 45°. La finestra riempie sempre una sola apertura del muro, larga e alta quanto il tipo: si posa, sposta, copia, ribalta, numera e compare negli abachi come ogni finestra. **Architecture → Bay window** (<kbd>BW</kbd>) ne mette una nel muro cliccato: il tipo scelto nel configuratore se sporge, altrimenti il primo bow-window del progetto, altrimenti un bow-window a 45°.

Le colonne della griglia sono le sue facce. In un bow-window e in una scatola la prima e l'ultima colonna sono i lati, quelle in mezzo il centro; una finestra ad arco ha una faccia per colonna, le loro larghezze che si dividono l'arco. Ogni specchiatura si apre come dice la sua **Apertura**, quindi i lati possono aprirsi e il centro restare fisso. Poi:

- **Projection**, dalla faccia esterna del muro alla faccia esterna dei telai, e per un bow-window l'angolo **Sides to the wall** (da 15° a 90°);
- **Sides**: i lati della scatola vetrati o pieni, con il loro materiale;
- **Seat inside**: la seduta al davanzale, quanto entra nella stanza, il suo materiale;
- **Sill outside** sotto le facce;
- **Base**: nessuna (appeso al muro), **muri fino al pavimento**, **una soletta sporgente** o **una soletta su mensole**, con spessore, sporto e materiale;
- **Head**: un tetto **a padiglione**, **a una falda** o **piano**, o una **soletta**, il suo spessore alla gronda, lo sporto, la pendenza e il materiale, e una **scossalina contro il muro**.

Telai, ante, vetri e maniglie sono quelli del tipo: i suoi materiali e le maniglie valgono per tutte le facce. **Bay style** imposta tutto insieme: bow-window a 45° o poco profondo a 30°, bow-window a terra, finestra ad arco di cinque o quattro pannelli, scatola con lati vetrati o pieni, bovindo su mensole. Come per **Applica uno stile…**, il menu mostra lo stile di bow-window della finestra, con *(modificato)* appena cambi ciò che ha impostato. **Save as bay style…** tiene quello configurato come stile del progetto, accanto a quelli di Lapin; si rinomina o elimina nell'elenco sotto i campi.

In pianta le facce sono tagliate, seduta, davanzale e base disegnati sotto il taglio e il tetto tratteggiato sopra; prospetti, sezioni e 3D mostrano tutto il corpo. L'abaco delle finestre ha una scheda per il tipo, le facce sviluppate, con una riga **Projecting** (genere, sporgenza, facce); la sua area è quella delle facce vetrate e i dettagli misurano tetto, seduta e base. In IFC è un solo IfcWindow a partizione definita dall'utente (*Bay 45°, projection 600, 3 faces, 4 panels*) il cui corpo è tutto il bow-window, i pannelli faccia per faccia; Lapin rilegge il bow-window.

![Una finestra ad arco nel configuratore](shots/bay-configurator.webp)

![Bow-window, finestra ad arco, scatola e bovindo in pianta](shots/bay-plan.webp)

![Bow-window, finestra ad arco, scatola e bovindo in 3D](shots/bay-3d.webp)

### Maniglie

**Maniglia** sceglie la maniglia del tipo: una **Maniglia a leva**, un **Pomolo**, un **Maniglione** o una **Maniglia per finestra**, oppure **Nessuno**. Per impostazione predefinita una porta ha una maniglia a leva e una finestra una maniglia per finestra. Una porta ha la maniglia su entrambe le facce, all’**Altezza della maniglia** che imposti; **Colore della maniglia** ne imposta il colore. Le maniglie si vedono in 3D, nelle sezioni e nei prospetti, e nei disegni del configuratore quando **Ferramenta** è spuntato.

### Parametri

**Parametri · negli abachi**, in fondo al configuratore, contiene i parametri propri del tipo: **+ Resistenza al fuoco** (tenuta in Pset_DoorCommon o Pset_WindowCommon di IFC come FireRating), **+ Produttore**, **+ Codice prodotto**, **+ Sito web**, o qualsiasi parametro a cui dai un nome. Le porte e le finestre del tipo li ereditano, e ognuno diventa una riga degli abachi dei serramenti: vedi [Abachi e grafici](schedules.md).

## Frangisole

**Architecture → Sunshade** (<kbd>SN</kbd>) mette un frangisole sulla porta o finestra cliccata: grande quanto la sua apertura, oltre le spalle, sopra l'architrave o davanti, e la segue quando si sposta. **Sunshade along a facade** prende due punti: su un muro corre lungo la sua faccia esterna, altrove lungo la linea, verso destra. La barra dei due strumenti sceglie il tipo, o uno stile da cui si crea un tipo nuovo.

I generi:

- **Canopy** (pensilina): soletta, a lamelle o in vetro, con sporgenza, spessore, inclinazione e veletta, su mensole, tiranti o montanti;
- **Vertical fins**: profondità, spessore, passo e angolo, correnti sopra e sotto;
- **Louvres (brise-soleil)**: lamelle orizzontali, piatte, alari o scatolari, profondità, passo e angolo, staccate dal muro, tra piastre di testata;
- **Egg-crate**: piastre orizzontali e verticali insieme;
- **Perforated screen**: una lamiera staccata dal muro, a fori tondi o quadrati o asole, loro dimensione e passo;
- **Awning** (tenda da sole): tessuto inclinato, mantovana, cassonetto e bracci snodati;
- **Pergola**: travetti, una trave, listelli sopra e montanti fino a terra;
- **External venetian blind**: stecche, quanto è abbassata, cassonetto e guide.

**Sunshade types** (in Types) modifica un tipo: il genere e ogni parametro, i materiali, accanto al suo disegno da fuori, in sezione e in pianta. Ogni frangisole del tipo segue una modifica. Un nuovo tipo parte da uno stile: pensilina in calcestruzzo, in legno su mensole, in vetro su tiranti, a lamelle, tettoia in coppi, brise-soleil, alette verticali, griglia in calcestruzzo, schermo forato in Corten, tenda in tessuto, pergola in legno, veneziana esterna; **Save as style…** tiene un tipo come stile del progetto.

![Le proprietà di una pensilina dopo il calcolo di mezzogiorno](shots/sunshade-properties.webp)

Selezionato, un frangisole mostra il tipo, su cosa sta e la propria **Projection** (vuota: quella del tipo). **Full shade at noon, 21 June** calcola, per una pensilina, una tenda o una pergola sopra una finestra, quanto deve sporgere perché tutta la finestra sia in ombra quando il sole è più alto nel giorno più lungo, nel luogo del progetto (**Manage → Location**; Bucarest finché non se ne imposta uno), e la imposta. Su una facciata sud a 45° N il sole è allora a 68,4°: una finestra di 1,5 m sotto una pensilina 150 mm sopra l'architrave richiede 650 mm. Le sue maniglie trascinano gli estremi lungo il muro o la linea e la sporgenza.

![I tipi di frangisole: lamelle, da fuori, in sezione e in pianta](shots/sunshade-types.webp)

Le piante disegnano tratteggiato un frangisole sopra il taglio, tagliato uno che lo attraversa (alette, uno schermo) e visto uno sotto; prospetti, sezioni e 3D mostrano il corpo con il retino del materiale, e proietta l'ombra con il resto (**View → Sun and shadows**). **Sunshade schedule** li elenca per tipo con dove stanno, lunghezza e sporgenza, quanti e l'area ombreggiata; le quantità (lunghezza, sporgenza, area, volume) prendono le voci di costo del tipo. In IFC sono IfcShadingDevice di un IfcShadingDeviceType: una tenda AWNING, una veneziana JALOUSIE, le lamelle LOUVER in IFC 4.3 (definite dall'utente in IFC4), gli altri definiti dall'utente (Canopy, Fins, Egg-crate, Screen, Pergola); Lapin li rilegge com'erano.

![Frangisole in pianta](shots/sunshades-plan.webp)

![Una pensilina, lamelle, alette, una tenda, una pergola e una veneziana in 3D](shots/sunshades-3d.webp)

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
