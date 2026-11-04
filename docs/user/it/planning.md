---
title: Pianificazione
order: 97
summary: Il programma dei lavori in un diagramma di Gantt: attività in una WBS, milestone, collegamenti con ritardi, il percorso critico calcolato su un calendario con le festività legali della Romania; da e verso MS Project, IFC ed Excel.
---

## La scheda Pianificazione

![La scheda Pianificazione](shots/ribbon-planning.webp)

- **Gantt**: apre il programma sotto la vista. Lo schermo si divide in due righe: il modello sopra, il programma sotto.
- **Calendario…**: i giorni e le ore lavorative, le festività e la pausa invernale (più sotto).
- **Attività** e **Milestone**: una nuova attività o milestone prima dell'attività selezionata nel Gantt (o in fondo).
- **Rientra**, **Riduci rientro**, **Collega le attività** e **Scollega le attività**: la WBS e i collegamenti delle attività selezionate.
- **Aggiungi all'attività…** (TA) ed **Elementi senza attività**: gli elementi del modello nelle attività (più avanti).
- **Importa MS Project…**, **Esporta MS Project…**, **Esporta XLSX** e **Esporta IFC…**: il programma da e verso altri programmi (più sotto).

Un progetto ha un solo programma. Si crea la prima volta che aggiungi un'attività e inizia il lunedì successivo. Cambia il suo inizio nella barra del Gantt.

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

### A mano

![La scheda della selezione sopra il 3D, le due attività di un muro segnate nel Gantt](shots/planning-element-tasks.webp)

Finché il Gantt è aperto, una scheda sopra la vista del modello dice quanti elementi sono selezionati e in quali attività si trovano. Trascinala per la sua maniglia sulla riga di un'attività: gli elementi entrano in quell'attività. Tieni <kbd>Maiusc</kbd> al rilascio per toglierli. **Aggiungi all'attività…** (<kbd>T</kbd> <kbd>A</kbd>, anche nella scheda Pianificazione) sceglie l'attività da un elenco.

![Aggiungi all'attività](shots/planning-assign.webp)

Ciò che aggiungi o togli a mano sono le **eccezioni** dell'attività: la regola resta com'è. Le Proprietà le contano, e la × accanto a ciascuna le dimentica. Le Proprietà di un elemento elencano le sue attività in **Cronoprogramma dei lavori**, ognuna con una × per toglierlo. Ogni modifica è un passo da annullare.

### Elementi senza attività

Appena un'attività prende elementi, la barra del Gantt dice quanti elementi non hanno un'attività o hanno strati che nessuna attività prende; un clic li seleziona, pronti da trascinare su un'attività. **Pianificazione → Elementi senza attività** fa lo stesso. **Gestisci → Verifica** li elenca anche, in *Elements with a task*: un avviso per ciascuno, un clic lo seleziona. Gli elementi esistenti che restano non hanno bisogno di un'attività.

### In IFC e MS Project

L'esportazione IFC scrive ciò che ogni attività costruisce come IfcRelAssignsToProduct (l'elemento, con le attività che lo costruiscono), e ciò su cui agisce un'attività di demolizione, rimozione o smontaggio come IfcRelAssignsToProcess. La regola, la parte e le eccezioni viaggiano in `Lapin_Task`, così Lapin rilegge la regola; un file di un altro programma dà attività con gli elementi che assegna loro. MS Project XML le porta nel campo Text1, chiamato *Lapin elements*. La cartella di lavoro ha le colonne **Elements** e **Rule**.

## Il calendario

![Il calendario di lavoro](shots/planning-calendar.webp)

**Pianificazione → Calendario…** imposta:

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

Un cronoprogramma generato dal modello, la simulazione 4D dell'edificio che cresce in 3D, l'avanzamento e le linee di base, e il computo metrico estimativo arrivano nelle prossime fasi.

