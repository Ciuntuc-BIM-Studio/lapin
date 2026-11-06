---
title: Pianificazione
order: 97
summary: Il programma dei lavori in un diagramma di Gantt: attività in una WBS, milestone, collegamenti con ritardi, il percorso critico calcolato su un calendario con le festività legali della Romania; la simulazione 4D dell'edificio che cresce giorno per giorno, su viste, tavole e in video; l’avanzamento segnato in cantiere, anche da un telefono, rispetto a una baseline; da e verso MS Project, IFC ed Excel.
---

## La scheda Esecuzione

![La scheda Esecuzione](shots/ribbon-planning.webp)

- **Genera cronoprogramma…**: un cronoprogramma intero fatto dal modello in un solo passo (più sotto).
- **Gantt**: apre il programma sotto la vista. Lo schermo si divide in due righe: il modello sopra, il programma sotto.
- **Calendario…**: i giorni e le ore lavorative, le festività e la pausa invernale (più sotto).
- **Attività** e **Milestone**: una nuova attività o milestone prima dell'attività selezionata nel Gantt (o in fondo).
- **Rientra**, **Riduci rientro**, **Collega le attività** e **Scollega le attività**: la WBS e i collegamenti delle attività selezionate.
- **Aggiungi all'attività…** (TA) ed **Elementi senza attività**: gli elementi del modello nelle attività (più avanti).
- **Simula**, **Tavola delle tappe…**, **Gantt su tavola** ed **Esporta video…**: la simulazione 4D, il modello come lo ha il programma in un giorno (più avanti).
- **Segna completato** (MD), **Segna iniziato** (MS), **Segna non iniziato**, **Salva la baseline**, **Cancella la baseline** e **Importa l’avanzamento…**: l’avanzamento e la baseline (sotto).
- **Importa MS Project…**, **Esporta MS Project…**, **Esporta XLSX** e **Esporta IFC…**: il programma da e verso altri programmi (più sotto).

Un progetto ha un solo programma. Si crea la prima volta che aggiungi un'attività e inizia il lunedì successivo. Cambia il suo inizio nella barra del Gantt.

## Generare il cronoprogramma dal modello

![Genera cronoprogramma: i modelli, la scheda Inizio e l'anteprima](shots/planning-generate.webp)

**Esecuzione → Genera cronoprogramma…** (anche nella barra del Gantt) fa un cronoprogramma completo dal modello: un'attività per ogni passo dei lavori e, dove il passo va piano per piano, una per piano, ogni attività già con i suoi elementi. A sinistra ci sono i **modelli**: **Telaio in cemento armato**, **Muratura portante**, **Capannone in acciaio** e **Telaio in legno**. Lapin segna come *suggerito* quello che chiede il modello: il capannone se il modello ne ha uno, il telaio in legno se le pareti sono a telaio, la muratura portante se non ha pilastri, altrimenti il telaio in cemento armato. Sopra ci sono gli stili del progetto (**Salva come stile…** conserva ciò che hai impostato).

A destra c'è l'**anteprima**: le attività che farebbe il generatore, come barre, ognuna con la quantità che ne ha dato la durata, e sotto i conteggi:

- **Elementi collegati**: gli elementi presi dalle attività, su tutti quelli che hanno bisogno di un'attività. È il numero più importante. Se ne restano fuori, **Mostra gli elementi senza attività** li seleziona nella vista e **Un passo per loro** aggiunge un passo con le loro classi.
- **Durate dalle rese** e **Durate predefinite (senza resa)**: quante attività ha cronometrato una resa e quante hanno preso i giorni del loro passo.
- **Casseforme e ponteggi sulle attività dei loro supporti** (più sotto).

**Applica** scrive il cronoprogramma come un solo passo da annullare. Lo schermo si divide: il modello sopra, il Gantt sotto.

### La scheda Inizio

- **Inizio**: il primo giorno del cronoprogramma.
- **Dividi per piano**: un'attività per piano per i passi che vanno piano per piano (struttura, murature, finiture); senza spunta, un'attività per passo.
- **Prima le demolizioni**: gli elementi che le fasi demoliscono vanno in un'attività **Demolizione** prima di tutte (tipo IFC DEMOLITION). Gli elementi esistenti che restano non sono di nessuna attività.
- **Casseforme** e **Ponteggi**: se questi elementi provvisori vivono secondo le attività dei loro supporti (più sotto).
- **Mantieni le attività fatte a mano**: quando il cronoprogramma viene generato di nuovo (più sotto).

### La scheda Sequenza

![La scheda Sequenza: i passi in ordine](shots/planning-generate-sequence.webp)

I passi del modello, in ordine. Ognuno si può spegnere, rinominare e spostare su o giù con le frecce. Dice se va **una volta** per l'edificio o **per piano**, e come segue il passo precedente sullo stesso piano: fine-inizio (FS) o inizio-inizio (SS) con un ritardo, per le finiture che salgono dietro la struttura. La matita apre ciò che costruisce il passo: le classi di elementi, scelte fra quelle del modello, e la loro **Parte** (interi, il nucleo o le finiture).

I collegamenti vengono dall'ordine: ogni attività segue il passo precedente sullo stesso piano e lo stesso passo sul piano sottostante. Nella muratura portante e nel telaio in legno, i muri di un piano aspettano anche il solaio del piano sottostante.

Ogni attività è fatta con una **regola** (le sue classi, il suo piano), quindi tiene i suoi elementi: un muro disegnato dopo la generazione entra da solo nella sua attività.

### La scheda Durate

![La scheda Durate: le rese e, per passo, resa, squadra e giorni](shots/planning-generate-durations.webp)

La durata di un'attività viene dalla quantità dei suoi elementi (dal computo: il volume del nucleo, la superficie delle finiture, porte e finestre a pezzo, l'acciaio a peso) e da una **resa di produttività**:

*durata = quantità × ore-uomo per unità ÷ (squadra × ore al giorno)*, in giorni lavorativi interi. Per esempio 4,5 m³ di muratura a 4 ore-uomo al metro cubo, con una squadra di 2 che lavora 8 ore: 18 ÷ 16 = 1,125, cioè 2 giorni.

Le rese sono tue: Lapin non fornisce alcun prezzario. **Importa rese…** legge la tua tabella da un file CSV o XLSX: una colonna per il codice, una per il testo, una per l'unità (m3, m2, m, cad, kg, t…), una per le ore-uomo per unità (o una produttività in unità all'ora) e, se vuoi, una per la squadra. **Modello (CSV)** salva le colonne con alcune righe di valori approssimativi scritti da Lapin, ognuna segnata *indicative* (indicativa). **Usa i valori indicativi di Lapin** le mette nel progetto per provare il generatore; la finestra avverte allora che le durate vengono da valori indicativi.

Sotto, ogni passo sceglie la sua **Resa**, la sua **Squadra** (0: quella della resa) e i suoi **Giorni**, usati quando nessuna resa dà la durata. La scheda Calendario mostra il calendario in cui si contano le durate.

### Generato di nuovo

Riapri il generatore e premi **Applica**: il cronoprogramma viene rifatto sulle stesse attività, che conservano i loro identificativi. Ciò che hai cambiato a mano resta:

- un'attività generata che hai modificato (nome, durata, date, regola, parte) è **Mantenuta come modificata**, spuntato nelle sue Proprietà; togli la spunta per rifarla dal suo passo;
- gli elementi aggiunti o tolti a mano restano le sue eccezioni;
- le attività e i collegamenti fatti da te restano; senza **Mantieni le attività fatte a mano** restano solo quelle generate.

![Il cronoprogramma generato: il modello sopra, il Gantt sotto](shots/planning-generated.webp)

Nelle Proprietà, un'attività generata dice da dove viene la sua durata: la quantità, la resa e la squadra, o i giorni del passo.

![Un'attività generata nelle Proprietà](shots/planning-generated-task.webp)

### Casseforme e ponteggi

Una cassaforma o un ponteggio delle **Opere provvisionali** non ha un'attività sua: vive secondo le attività di ciò che serve. La cassaforma si monta quando inizia il suo solaio, la sua trave o il suo muro, e si disarma passati i giorni del suo tipo dopo la loro fine (**Disarmato dopo**, in giorni lavorativi: 21 per solai e travi, 7 per muri, pilastri e fondazioni, se il tipo non dice altro). Il ponteggio resta dalla prima attività sui suoi muri all'ultima, finiture comprese. Selezionane uno: le sue Proprietà dicono quando è **In cantiere** e secondo quali attività **Vive secondo**.

## Il Gantt

![Il Gantt sotto la vista 3D, con un'attività selezionata](shots/planning-gantt.webp)

A sinistra c'è la tabella delle attività: il numero di riga, il codice **WBS**, il nome, la **Durata** in giorni lavorativi, l'**Inizio** e la **Fine**, i **Predecessori** e il **Margine** totale. A destra ogni attività è una barra sulla scala del tempo:

- un'**attività** è una barra blu, rossa quando è sul percorso critico;
- un'**attività di riepilogo** (un'attività con altre attività sotto) è una parentesi scura dall'inizio della sua prima attività alla fine dell'ultima;
- una **milestone** è un rombo.

I giorni non lavorativi sono ombreggiati e oggi è una linea tratteggiata. **Giorni**, **Settimane**, **Mesi** e **Trimestri** cambiano la scala, come <kbd>Ctrl</kbd> + la rotella del mouse. Il pulsante con i quattro angoli mostra tutto il programma e **Oggi** scorre fino a oggi. **Percorso critico** attiva e disattiva il rosso. La freccia accanto al nome di un'attività di riepilogo comprime ed espande le attività sotto di essa.

Il Gantt è una vista come le altre: ha una scheda e, sul desktop, può aprirsi in una finestra propria per un secondo schermo.

### Modifica nella tabella

Fai doppio clic su una cella per scrivervi. <kbd>Invio</kbd> mantiene il valore ed <kbd>Esc</kbd> lo abbandona.

- **Durata**: giorni lavorativi; 0 fa dell'attività una milestone.
- **Inizio**: l'attività inizia non prima di quel giorno (un vincolo, vedi sotto).
- **Fine**: la durata diventa i giorni lavorativi dall'inizio a quel giorno.
- **Predecessori**: come in MS Project, numeri di riga separati da virgole, ciascuno con un tipo e un ritardo: `3`, `5SS`, `7FF+2`, `2FS-1d`.

La tastiera funziona come in una tabella: <kbd>↑</kbd> e <kbd>↓</kbd> spostano la selezione, <kbd>Ins</kbd> aggiunge un'attività prima di quella selezionata (<kbd>Shift</kbd>+<kbd>Ins</kbd> una milestone), <kbd>Canc</kbd> elimina le attività selezionate con ciò che hanno sotto, <kbd>Tab</kbd> rientra e <kbd>Shift</kbd>+<kbd>Tab</kbd> riduce il rientro, <kbd>F2</kbd> rinomina.

### Modifica sul diagramma

- **Trascina una barra** per spostare l'attività. Inizia allora non prima del giorno in cui la rilasci. La data si vede mentre trascini.
- **Trascina l'estremità di una barra** per cambiarne la durata. I giorni lavorativi si vedono mentre trascini.
- **Collega due attività**: passa il mouse su una barra, poi trascina dal piccolo cerchio a una delle sue estremità su un'altra barra. Le estremità che unisci danno il tipo: dalla fine all'inizio dell'altra è fine-inizio (FS), inizio a inizio SS, fine a fine FF, inizio a fine SF.
- **Fai clic su una freccia** per cambiare il tipo e il ritardo del collegamento (in giorni lavorativi; negativo è una sovrapposizione) o per rimuoverlo.

Ogni modifica è un singolo passo da annullare.

## Un'attività in Proprietà

![Un'attività in Proprietà](shots/planning-task.webp)

Seleziona un'attività e Proprietà la mostra: il suo nome, il suo tipo IFC, la sua durata, se è una milestone, le sue date e:

- **Vincolo**: **Il prima possibile** (predefinito), **Inizio non prima del** una data o **Deve iniziare il** una data. Un'attività che deve iniziare prima che finiscano i suoi predecessori toglie loro margine. Il loro margine diventa allora negativo ed essi diventano critici.
- **Lavorazioni umide**: calcestruzzo, massetti, intonaci. Un'attività umida non lavora nella pausa invernale del calendario, quando la pausa è attiva.
- **Margine totale** e **Margine libero**: di quanti giorni lavorativi l'attività può slittare senza spostare la fine, o senza spostare ciò che la segue. **Inizio al più tardi** e **Fine al più tardi** sono le ultime date che mantengono la fine.
- **Predecessori**: ogni collegamento in ingresso, con il suo tipo e ritardo da cambiare, o da rimuovere. **Successori** elenca ciò che segue l'attività.

## Il percorso critico

Lapin calcola il programma dopo ogni modifica. Un passaggio in avanti trova le date più anticipate e uno all'indietro le più tardive. Le attività senza margine formano il percorso critico, e un ritardo di una qualsiasi di esse sposta la fine. Un collegamento verso un'attività di riepilogo trattiene tutte le attività sotto di essa. Un collegamento da un'attività di riepilogo parte dall'inizio della sua prima attività e finisce alla fine dell'ultima. Lapin rifiuta un collegamento che creerebbe un ciclo.

## Gli elementi del modello nelle attività

![L'attività Muratura selezionata: i suoi muri disegnati selezionati in 3D](shots/planning-links.webp)

Un'attività prende elementi del modello, così il cronoprogramma sa cosa si costruisce e quando. Seleziona un'attività e i suoi elementi appaiono selezionati in 3D e nelle piante; un doppio clic sulla sua barra li seleziona e li inquadra. Seleziona elementi in una vista e le righe delle loro attività vengono segnate nel Gantt. La colonna **Elementi** conta gli elementi di ogni attività (quelli di un riepilogo con quelli delle attività sottostanti).

### Una regola

![Gli elementi di un'attività in Proprietà](shots/planning-task-elements.webp)

In **Elementi** nelle Proprietà, spunta **Trovali con una regola** e scegli cosa prende l'attività, come nel filtro di un abaco: **Classi**, **Piani**, **Tipi**, **Materiali**, **Gruppi** e **Fasi**, ogni valore con quanti elementi lo hanno. Ogni condizione posta deve valere; una lasciata vuota fa passare tutto. Si conserva la regola, non gli elementi: un muro disegnato domani al piano terra entra da solo nell'attività «Muratura piano terra». **Regola dalla selezione** crea una regola con le classi e i piani degli elementi selezionati.

### Una parte degli elementi

**Parte** dice cosa prende l'attività da ogni elemento: gli **elementi interi**, il loro **nucleo** (lo strato strutturale), le loro **finiture** (tutti gli strati tranne il nucleo), gli strati di un materiale o quelli di una categoria di materiale (Finitura, Isolamento…). Così un muro a strati si costruisce in un'attività Muratura (il nucleo) e si intonaca in un'attività Intonaci (le finiture). Un elemento senza strati è il proprio nucleo e non ha finiture.

Anche un tetto si costruisce a strati: la sua struttura (il solaio di un tetto piano, di una volta o di un tetto senza orditura; i travetti o le capriate di uno con orditura) è il suo nucleo, il manto (con i tetti degli abbaini) e i listelli le sue finiture. Un'attività **Struttura del tetto** (nucleo) e una **Manto di copertura** (finiture) prendono ciascuna la sua parte, una regola per materiale trova il solaio dal suo calcestruzzo, e la simulazione e una vista a una data mostrano il manto solo quando è posato. I modelli del generatore costruiscono un tetto in questi due passi, ognuno con la durata dall'area della sua parte, e i pluviali in un passo a sé (**Grondaie e pluviali**); il manto di un capannone in acciaio resta un solo passo.

### A mano

![La scheda della selezione sopra il 3D, le due attività di un muro segnate nel Gantt](shots/planning-element-tasks.webp)

Finché il Gantt è aperto, una scheda sopra la vista del modello dice quanti elementi sono selezionati e in quali attività si trovano. Trascinala per la sua maniglia sulla riga di un'attività: gli elementi entrano in quell'attività. Tieni <kbd>Maiusc</kbd> al rilascio per toglierli. **Aggiungi all'attività…** (<kbd>T</kbd> <kbd>A</kbd>, anche nella scheda Esecuzione) sceglie l'attività da un elenco.

![Aggiungi all'attività](shots/planning-assign.webp)

Ciò che aggiungi o togli a mano sono le **eccezioni** dell'attività: la regola resta com'è. Le Proprietà le contano, e la × accanto a ciascuna le dimentica. Le Proprietà di un elemento elencano le sue attività in **Cronoprogramma dei lavori**, ognuna con una × per toglierlo. Ogni modifica è un passo da annullare.

### Elementi senza attività

Appena un'attività prende elementi, la barra del Gantt dice quanti elementi non hanno un'attività o hanno strati che nessuna attività prende; un clic li seleziona, pronti da trascinare su un'attività. **Esecuzione → Elementi senza attività** fa lo stesso. **Gestisci → Verifica** li elenca anche, in *Elements with a task*: un avviso per ciascuno, un clic lo seleziona. Gli elementi esistenti che restano non hanno bisogno di un'attività.

### In IFC e MS Project

L'esportazione IFC scrive ciò che ogni attività costruisce come IfcRelAssignsToProduct (l'elemento, con le attività che lo costruiscono), e ciò su cui agisce un'attività di demolizione, rimozione o smontaggio come IfcRelAssignsToProcess. La regola, la parte e le eccezioni viaggiano in `Lapin_Task`, così Lapin rilegge la regola; un file di un altro programma dà attività con gli elementi che assegna loro. MS Project XML le porta nel campo Text1, chiamato *Lapin elements*. La cartella di lavoro ha le colonne **Elements** e **Rule**.

## La simulazione 4D

![La casa in un giorno del suo programma: il solaio del primo piano in corso nella sua cassaforma, i muri sopra non iniziati](shots/planning-simulation.webp)

**Esecuzione → Simula** (anche **Simula** nella barra del Gantt) mostra il modello come lo ha il programma in un giorno. Il Gantt si apre sotto la vista se non è aperto, con il lettore in alto e il giorno come una banda sul diagramma:

- ciò che **non è iniziato** è nascosto (o mostrato come un fantasma pallido, nelle impostazioni);
- ciò che è **in corso** è disegnato nel suo colore (ambra);
- ciò che è **costruito** è disegnato com'è;
- ciò che è **in demolizione** è rosso finché dura la sua attività, poi scompare;
- **casseforme e ponteggi** ci sono, nel loro colore (blu), solo tra i giorni che danno loro le attività dei loro elementi: la cassaforma di un solaio dall'inizio del getto al disarmo, un ponteggio dalla prima attività sui suoi muri all'ultima.

Un muro a strati murato in un'attività e intonacato in un'altra cresce in due passi: prima il nucleo, poi l'intonaco. Gli elementi che nessuna attività prende restano come sono. Una scheda sulla vista del modello dice il giorno, cosa vuol dire ogni colore e quanti elementi ci sono in ogni stato.

### La riproduzione

![Il lettore in cima al Gantt](shots/planning-simulation-bar.webp)

- **Riproduci** (▶) fa scorrere i giorni alla velocità scelta (da 1 a 60 giorni al secondo) fino alla fine; **Pausa** la ferma.
- Le frecce ai due lati tornano indietro o vanno avanti di **un giorno** o **una settimana**.
- Trascina il cursore, scrivi una data, o clicca e trascina sulla scala dei tempi del Gantt: la banda sul diagramma e il modello seguono.
- Accanto alla data: le attività in corso quel giorno.

Da un giorno all'altro cambiano solo i colori e la visibilità degli elementi: nulla viene ricostruito, quindi un modello grande scorre fluido come uno piccolo.

### I suoi colori

![Le impostazioni della simulazione](shots/planning-simulation-settings.webp)

L'ingranaggio apre le impostazioni della simulazione, tenute nel progetto: i colori di ciò che è in corso, in demolizione, di casseforme e ponteggi e dei fantasmi; se ciò che **non è iniziato** è nascosto o mostrato **come fantasmi**; se gli **elementi senza attività** sono mostrati o nascosti; e se gli **strati costruiti a parte** si vedono a parte. **Colori predefiniti** riporta quelli di Lapin.

### Stato alla data su viste e tavole

![Lo Stato alla data di una pianta nelle sue proprietà](shots/planning-view-date.webp)

Una pianta, una sezione, un prospetto o una vista 3D salvata ha nelle proprietà **Stato alla data**: la vista disegna allora il modello come lo ha il programma quel giorno, negli stessi colori, e le sue tavole lo stampano così (il PDF mostra ciò che mostra lo schermo). Con la simulazione in corso, **la data del cursore** prende il suo giorno. Una vista a una data disegna gli stati del programma, non quelli della sua fase. La × accanto alla data riporta il modello com'è. Una vista 3D con una data mostra quel giorno anche a simulazione spenta.

### La tavola delle tappe

![Tavola delle tappe: le date dalle fini dei capitoli](shots/planning-stages-dialog.webp)

**Esecuzione → Tavola delle tappe…** (anche nel lettore) mette il modello a più date affiancato su una nuova tavola. È disegnato dalla **vista 3D, com'è ora** (un'immagine a ogni data) o da una pianta, una sezione o un prospetto, che il motore disegna a ogni data. Le date sono **la fine di ogni capitolo** (ogni attività in cima alla WBS), **la fine di ogni mese**, o date **scelte**. Ogni riquadro porta la sua data e le attività in corso.

![Una tavola delle tappe](shots/planning-stages-sheet.webp)

### Il Gantt su una tavola

![Il Gantt su una tavola](shots/planning-gantt-sheet.webp)

**Esecuzione → Gantt su tavola** mette il programma su una nuova tavola: la tabella (riga, WBS e attività, giorni, inizio, fine) e le barre su una scala dei tempi, le attività critiche in rosso, i riepiloghi come parentesi, le milestone come rombi, i collegamenti. Un programma più alto della tavola va su quante tavole servono, con la sua intestazione su ognuna. È disegnato in vettoriale, come le viste, e si stampa nel PDF.

### Esporta video

![Esporta video](shots/planning-video.webp)

**Esecuzione → Esporta video…** (anche nel lettore) registra la vista 3D, da dove guarda ora, da un giorno a un altro, un fotogramma per **un giorno** o per **una settimana**, con la data e le attività in corso scritte sotto. Il file è un video WebM, o MP4 dove il sistema offre solo quello (l'app per macOS). Un browser che non registra video salva invece i fotogrammi come file PNG in uno ZIP.

## L’avanzamento

![Il Gantt con l’avanzamento: la colonna %, la parte fatta più scura in ogni barra, la baseline sotto e lo scostamento](shots/planning-progress.webp)

Man mano che i lavori procedono, il cronoprogramma dice a che punto sono e sposta ciò che resta:

- La **data di stato**, nella barra del Gantt, è il giorno fino al quale è riportato l’avanzamento. Il lavoro rimanente inizia il giorno lavorativo successivo: un’attività non iniziata non può iniziare prima, e una in corso fa ciò che le resta da lì in poi. Il diagramma la disegna come una linea blu.
- La **% completato** di un’attività si scrive nella sua colonna o nelle Proprietà. Senza, viene dai suoi elementi segnati in cantiere: la quantità fatta sulla quantità dell’attività (nella sua unità, quando il generatore gliene ha data una), altrimenti il loro numero. Quella di un’attività di riepilogo viene dalle sue attività, pesata sulle loro durate.
- L’**inizio effettivo** e la **fine effettiva** si scrivono nelle Proprietà. Lasciati vuoti, vengono dai segni: il primo giorno in cui un elemento è stato segnato, e il giorno dell’ultimo elemento quando sono tutti completati. Un’attività finita resta sulle sue date effettive, fuori dal percorso critico.
- Un’attività in corso tiene il suo inizio effettivo e fa la parte della sua durata non ancora fatta (arrotondata per eccesso) dopo la data di stato; le attività successive si spostano con lei.

Le barre mostrano la parte fatta più scura. Le Proprietà dicono da dove viene una percentuale e quanti giorni lavorativi restano.

![L’avanzamento di un’attività nelle Proprietà](shots/planning-progress-task.webp)

### Segnare gli elementi in cantiere

**Esecuzione → Segna completato** (MD) e **Segna iniziato** (MS) segnano gli elementi selezionati in 3D o in una pianta, alla data di stato (oggi, se non ce n’è una), in ogni attività che li comprende; **Segna non iniziato** toglie i segni. Le proprietà di un elemento selezionato mostrano il suo stato in cantiere con gli stessi tre pulsanti e, in ciascuna delle sue attività, il giorno in cui è stato completato o iniziato, con un pulsante che lo segna completato solo in quell’attività. Un muro stratificato in un’attività di muratura e in una di intonaco si segna in entrambe, o in una dalla sua riga.

![I segni di un muro nelle sue proprietà](shots/planning-element-marks.webp)

In cantiere, da un telefono: entra nella sessione condivisa del progetto dal browser (**File → Condividi sessione…** sul computer che lo ha aperto). Un ospite entrato solo per guardare può comunque segnare elementi e scrivere l’avanzamento di un’attività: il segnare è l’unica modifica aperta a ogni ospite. Foto e rapporti sulle attività sono per l’edizione premium.

### La baseline

**Esecuzione → Salva la baseline** conserva le date e la durata di ogni attività come sono ora; salvata di nuovo, viene sostituita (una sola baseline: più baseline e scenari sono per l’edizione premium). Il Gantt la disegna allora come una barra grigia sottile sotto la barra di ogni attività (**Baseline** nella barra degli strumenti la nasconde), aggiunge la colonna **Scostamento** (i giorni lavorativi di cui si è spostata la fine: positivo è ritardo, la riga segnata in rosso) e dice nella sua barra quante attività sono in ritardo e di quanto si è spostata la fine. Le Proprietà danno le date della baseline e gli scostamenti di inizio e fine. **Cancella la baseline** la toglie.

### In ritardo e in anticipo nella simulazione

![La simulazione alla data di stato: le finestre in ritardo sulla baseline](shots/planning-progress-simulation.webp)

Con una baseline, la simulazione confronta: un elemento non costruito (o non iniziato) in un giorno in cui la baseline lo aveva così è **in ritardo** (cremisi), e uno in corso o costruito prima che la baseline lo facesse iniziare è **in anticipo** (verde). Fino alla data di stato gli elementi stanno sui loro giorni effettivi: uno segnato completato è costruito il giorno in cui è stato segnato. **Confrontato con la baseline**, nelle impostazioni della simulazione, lo disattiva; lì si scelgono anche i suoi due colori. Le viste e le tavole a una data mostrano lo stesso.

### L’avanzamento nei file

- **Esporta XLSX** aggiunge le colonne **% complete**, **Actual start**, **Actual finish**, **Baseline start**, **Baseline finish**, **Finish variance** e **GlobalId** (la cartella è in inglese). Compilale in cantiere e rileggile con **Esecuzione → Importa l’avanzamento…**: le righe si trovano per GlobalId, WBS o ID, e una *Status date* scritta sopra la tabella fissa la data di stato. Va bene anche un CSV.
- **MS Project XML** porta la data di stato, la percentuale di ogni attività, l’inizio e la fine effettivi, la durata rimanente e la baseline (Baseline 0); i segni degli elementi viaggiano con gli elementi, in *Lapin elements*.
- **IFC** li scrive nell’IfcTaskTime di ogni attività (l’ora di stato, l’inizio e la fine effettivi, il tempo rimanente, il grado di completamento), e la baseline come un secondo IfcWorkSchedule di tipo BASELINE; i campi propri di Lapin (la percentuale scritta, i segni) vanno in `Lapin_Task`. Entrambi tornano alle stesse date.
- **Gestisci → Versioni → Confronta** dice, oltre agli elementi, cosa è cambiato nel cronoprogramma: la fine, i giorni lavorativi, la data di stato, e le attività aggiunte, eliminate, spostate (di quanti giorni lavorativi), con la durata o l’avanzamento cambiati.

## Il costo delle attività

![Il Gantt con la colonna Costo](shots/planning-cost.webp)

Con un preventivo (**Esecuzione → Preventivo…**), il Gantt riceve la colonna **Costo**: la parte del preventivo di ogni attività, dalle voci che ne prezzano gli elementi (quella di un riepilogo è delle sue attività). La barra degli strumenti mostra il **budget** (i costi diretti del preventivo) e il costo **a oggi** (il costo di ogni attività per la sua % completata); Proprietà mostra il costo di un'attività e la parte fatta. **Esecuzione → Costo nel tempo** disegna la curva a S e la tabella mensile (vedi [Preventivo](cost.md)).

## Il calendario

![Il calendario di lavoro](shots/planning-calendar.webp)

**Esecuzione → Calendario…** imposta:

- i **giorni lavorativi** della settimana (dal lunedì al venerdì per impostazione predefinita) e le **ore** di una giornata (8 dalle 08:00);
- le **festività**: le festività legali della Romania, calcolate per ogni anno (compresi il Venerdì Santo, la Pasqua e la Pentecoste ortodossi), oppure nessuna. L'elenco sotto mostra le festività di un anno per controllarle;
- la **pausa invernale** per le lavorazioni umide, disattivata per impostazione predefinita: dal 15 dicembre al 1° marzo, a meno che tu non indichi altri giorni;
- le **eccezioni**: giorni liberi per il progetto (un cantiere chiuso) o giorni lavorati che altrimenti non lo sarebbero (un sabato).

Le durate e i ritardi contano i giorni lavorativi di questo calendario.

## Scambiare il programma

- **Esporta MS Project…** scrive un file XML di MS Project. MS Project, ProjectLibre e GanttProject lo aprono, e Primavera P6 lo importa. Contiene le attività con la loro WBS, durate, date, vincoli e margini, i collegamenti con i loro tipi e ritardi, e il calendario con ogni festività come eccezione. **Importa MS Project…** legge un file simile al posto del programma del progetto. Un programma scritto da Lapin e riletto conserva gli id e le date delle sue attività.
- **Esporta IFC…** (anche sotto **File**) scrive il programma nel file IFC insieme al modello: un IfcWorkSchedule, i suoi IfcTask con i loro tempi, i collegamenti IfcRelSequence e l'IfcWorkCalendar. Importando un file IFC che ha un programma, viene portato dentro anche questo.
- **Esporta XLSX** scrive una cartella di lavoro: la tabella del Gantt e i giorni liberi del calendario.

I campi propri di Lapin per cui gli altri programmi non hanno posto (il vincolo di un'attività, se è umida, la pausa invernale) viaggiano in IFC come i set di proprietà `Lapin_Task` e `Lapin_Calendar`. MS Project non ha attività umide, quindi la pausa invernale resta in Lapin.

## Cosa viene dopo

Il costo nel tempo è in [Preventivo](cost.md). Il livellamento delle risorse, il valore guadagnato, gli stati di avanzamento mensili e più linee di base e scenari sono dell'edizione premium.
