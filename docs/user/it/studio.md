---
title: Studio
order: 85
summary: Modella solidi a forma libera, schizzati su qualsiasi faccia o piano, poi spingili, tirali, arrotondali, svuotali, dividili e ripetili in 3D.
---

## La scheda Studio

La scheda **Studio** crea solidi che nessun’altra scheda costruisce: una panchina, una pensilina, un corrimano, una fontana, uno zoccolo su una facciata. Ogni solido nasce da uno schizzo o da una primitiva. Poi lo modifichi in 3D per passi, e ogni passo resta modificabile.

![La scheda Studio](shots/ribbon-studio.webp)

Un solido Studio ha una classe IFC, come ogni elemento. Sceglila nelle sue proprietà: un elemento strutturale, una piastra, un rivestimento, un parapetto, una soletta o qualsiasi altra. Le sue quantità e la sua esportazione seguono quella classe.

## Dove schizzare

Schizzi il contorno di un solido con gli strumenti di **Disegno**, come polilinee chiuse, rettangoli e cerchi. Anche linee e archi che si uniscono estremo con estremo formano un contorno chiuso. Una forma chiusa all’interno del contorno è un foro.

Puoi schizzare su quattro tipi di vista:

- **Una pianta** disegna alla quota del livello del suo piano.
- **Una sezione** disegna sulla sua linea di taglio, in verticale.
- **Un prospetto** o **un piano di riferimento**: vedi *Piani di riferimento* più sotto.

## Creare un solido

- **Estrusione** (EXTRUDE) spinge il contorno dritto in fuori. Digita S per entrambi i lati dello schizzo, T per una rastremazione che inclina i lati verso l’interno, e Cut perché tagli gli elementi selezionati prima. Per portarla fino alla faccia di un altro elemento, usa **Fino a → una faccia…** nelle sue proprietà, oppure **Estrudi fino a faccia**. La profondità, le altezze e gli spessori si scrivono nell’unità del progetto, come dice il messaggio (`Depth (mm)`: `2000` è 2 m).
- **Sweep** (SWEEP) fa scorrere un profilo lungo un percorso. TWist ruota la sezione lungo il cammino, SCale la ingrandisce e RIse la solleva. Un cerchio che sale diventa un’elica, che compie TUrns giri.
- **Loft** (LOFT) unisce profili disegnati su viste diverse. Digita S per farlo passare in modo continuo attraverso di essi. Una linea selezionata con il ruolo *secondo percorso* lo curva come guida.
- **Rivoluzione** (REVOLVE) fa ruotare un profilo attorno a una linea d’asse.
- **Le primitive** posano con un clic un parallelepipedo, un cilindro, una sfera, un cono, un toro, un cuneo o una piramide. Cambiane dimensioni e posizione nelle loro proprietà.

![Un corrimano ottenuto con uno sweep lungo un cerchio che sale di 3 m in due giri](shots/studio-helix.webp)

Ogni solido resta legato al suo schizzo. Cambia la forma e il solido la segue, con lo stesso GlobalId. Seleziona un solido per cambiarne la classe IFC, il tipo predefinito e il tipo nel pannello Proprietà. **Modifica schizzo** apre la vista su cui si trova lo schizzo.

Un solido appena creato resta selezionato, così si illumina con le maniglie del suo schizzo. In una pianta, una sezione o un prospetto, dove la vista taglia un solido il suo taglio è riempito di bianco: le linee dello schizzo diventano una forma che si vede piena. Un materiale con un proprio riempimento o retino lo disegna invece con quello.

## Anello, Scatola e Striscia di muro

Questi tre strumenti danno a una forma disegnata uno spessore nel suo stesso piano, poi un’altezza a partire da quel piano:

- **Anello** (<kbd>RING</kbd>) prende un cerchio, un arco o un contorno chiuso e ne fa un anello o un tubo. Il suo spessore va all’**interno**, all’**esterno** o **centrato** sulla forma.
- **Scatola** (<kbd>BOX</kbd>) prende un rettangolo, una polilinea chiusa o un cerchio e crea i muri tutt’attorno. Un fondo (**Inferiore**) e una copertura (**Superiore**) sono facoltativi, ciascuno con il proprio spessore.
- **Striscia di muro** (<kbd>TWALL</kbd>) prende una linea, una polilinea o un arco e ne fa una striscia di muro. Il suo spessore va **a sinistra**, **a destra** o **centrato**, visto percorrendo la linea nel verso in cui l’hai disegnata. I suoi angoli sono tagliati a 45°.

Avvia lo strumento e fai clic sulla forma. La riga di comando propone le opzioni come lettere: <kbd>I</kbd>, <kbd>O</kbd> e <kbd>C</kbd> (oppure <kbd>L</kbd>, <kbd>R</kbd> e <kbd>C</kbd>) per dove va lo spessore, e per una scatola <kbd>B</kbd> e <kbd>T</kbd> per attivare o disattivare il fondo e la copertura. Poi digita lo spessore e l’altezza, in metri, oppure premi <kbd>Enter</kbd> per mantenere il valore mostrato.

**In una pianta**, gli strumenti creano per impostazione predefinita muri nativi, del tipo di muro scelto in **Architettura**. Una forma chiusa diventa un anello di muri uniti ai suoi angoli, una polilinea una catena di muri, e un arco o un cerchio muri curvi. Dove va lo spessore decide dove stanno i muri: l’intero spessore del tipo di muro va dal lato che hai scelto. Il fondo e la copertura di una scatola diventano solai. Digita <kbd>S</kbd> prima di scegliere la forma per creare invece un solido Studio.

**In una sezione, in un prospetto o su un piano di riferimento**, gli strumenti creano un solido Studio della classe IFC IfcWall; il fondo e la copertura di una scatola sono solette separate (IfcSlab). L’altezza va lungo la normale del piano, verso di te; un’altezza negativa va lontano da te, come con **Estrusione**. Per dare un tipo a un muro così, selezionalo e scegline o creane uno in **Tipo** nel pannello Proprietà.

![Un anello di muri curvi nativi attorno a un cerchio, una scatola con il suo fondo e la sua copertura, e una striscia di muro, nella pianta del piano terra](shots/studio-thick-plan.webp)

Tutto resta parametrico. Seleziona il solido e cambia **Spessore**, **Altezza** e **Lo spessore va** nel pannello Proprietà. Per una scatola, spunta **Inferiore** o **Superiore** e imposta i loro spessori: Lapin aggiunge o toglie le solette. Una scatola con un fondo o una copertura è raggruppata con essi in un assemblaggio (IfcElementAssembly). Trascina le maniglie della forma, o modificala nella sua vista, e il solido viene ricostruito.

![Le proprietà di una scatola: spessore, altezza, fondo e copertura](shots/studio-box-properties.webp)

## Piani di riferimento

Un piano di riferimento è un piano in un punto qualsiasi del modello, con una vista tutta sua. Ci disegni sopra con Disegno, come in una sezione, e Studio vi crea solidi. Creane uno dalla scheda **Studio**:

- **Piano dalla vista** (<kbd>REFPLANE</kbd>) è parallelo alla pianta, alla sezione, al prospetto o al piano che hai davanti. Digita a che distanza sta verso di te (un valore negativo lo allontana).
- **Piano su faccia**: nella vista 3D, fai clic su una faccia di un elemento qualsiasi. Il piano si posa sulla faccia, rivolto verso l’esterno di essa.
- **Piano per 3 punti**: nella vista 3D, fai clic su tre punti del modello. Il piano passa per essi.

Il nuovo piano si apre subito. Compare sotto **Piani di riferimento** nel browser di progetto. Il suo disegno è una sezione sul piano, che guarda in senso opposto al lato verso cui è rivolto.

Nella vista 3D ogni piano di riferimento è disegnato come un pannello traslucido con il suo nome, grande quanto il modello su di esso (oppure secondo **Larghezza disegnata (3D)** e **Altezza disegnata (3D)**, almeno 2 × 2 m), evidenziato sotto il cursore. Fai clic per selezionare il piano, doppio clic per aprirne la vista. Con il piano selezionato, trascina la freccia al suo centro per spostarlo lungo la sua normale: l’offset si legge accanto alla freccia, e ciò che è disegnato sul piano lo segue. **Vista → Piani di riferimento** nasconde e mostra i pannelli; non entrano mai in esportazioni, render o immagini. Nelle piante, nelle sezioni e nei prospetti, un piano che taglia la vista è disegnato come una linea tratteggiata con il suo nome, per tutta l’estensione della vista: fai clic per selezionare il piano, doppio clic per aprirlo. Un piano parallelo alla vista non vi è disegnato (non ne taglia nulla). Queste linee valgono solo per lo schermo: le tavole non le stampano mai.

![Un piano di riferimento sulla facciata sud, con un anello disegnato sopra](shots/studio-plane.webp)

Seleziona il piano nel browser per spostarlo. **Origine x**, **y** e **z** lo posizionano. **Facce** è la direzione in pianta verso cui è rivolto, **Inclinazione** quanto si inclina all’indietro (a 90° è orizzontale, rivolto verso l’alto) e **Rotazione nel piano** ruota il suo disegno attorno alla sua normale. **Sposta lungo la normale…** e **Ruota attorno alla verticale…** fanno lo stesso in un solo passo. Ciò che è disegnato sul piano, e ogni solido creato a partire da esso, si sposta con lui.

![Le proprietà di un piano di riferimento](shots/studio-plane-properties.webp)

Anche sezioni e prospetti sono piani. Un solido schizzato su una sezione sta sulla sua linea di taglio, e segue la linea quando la sposti. Su una sezione sviluppata, sta sul tratto su cui è disegnato. Un prospetto prende un piano tutto suo la prima volta che vi crei un solido: sul fronte del modello, con la sua x come è disegnata ora. Cambia la sua **Profondità del piano di schizzo** nelle proprietà del prospetto per spostare il piano, e i suoi solidi, lungo la vista.

![La casa con i suoi solidi Studio in 3D](shots/studio-3d.webp)

In IFC, un anello, i muri di una scatola e una striscia di muro sono elementi IfcWall, il fondo e la copertura di una scatola elementi IfcSlab, e una scatola con essi un IfcElementAssembly con IfcRelAggregates; i loro tipi sono scritti come IfcWallType e IfcSlabType. Ogni esportazione 3D (IFC, Fragments, glTF, USD, COLLADA) scrive un solido Studio dove si trova nel modello, qualunque sia il piano su cui è stato schizzato, con i suoi fori; un assemblaggio è un nodo di gruppo sopra le sue parti. Riaprire l’IFC di Lapin riporta i solidi come riferimenti, al loro posto, con la loro classe, il loro tipo e il loro assemblaggio, ma non più modificabili (i loro schizzi non sono nel file). La storia di ogni solido è conservata nell’insieme di proprietà di Lapin, Lapin_Intent. I solidi Studio accettano solo tipi semplici, senza strati: è la modellazione libera per cui esistono; i muri a strati sono i muri nativi di Lapin.
## Modificare un solido in 3D

Questi strumenti lavorano sulle facce e sugli spigoli del corpo così com’è costruito ora. La faccia sotto il mouse si illumina.

**Spingi/Tira**, **Dividi**, **Seziona** e **Sposta 3D** si usano direttamente sul modello. Fai clic su una faccia, e su di essa compare una freccia: il mouse sposta la faccia lungo la freccia, e ciò che farà il passo viene disegnato mentre procedi. Spingi/Tira disegna in verde ciò che aggiunge e in rosso ciò che toglie. Una divisione o una sezione disegna il suo piano in giallo. La distanza si legge accanto alla freccia. Digita un valore per fissarla esattamente, per esempio 500 o -250. Fai clic o premi <kbd>Enter</kbd> per applicarla, oppure premi <kbd>Esc</kbd> per lasciar perdere; lo strumento resta attivo per la faccia successiva. <kbd>Enter</kbd> senza aver spostato né digitato nulla ripete l’ultima distanza. Un trascinamento procede di centimetro in centimetro, o di 10 cm con <kbd>Shift</kbd>. Il tasto destro e quello centrale continuano a ruotare e spostare la vista. Gli altri strumenti chiedono il loro valore in una piccola casella accanto al cursore.

- **Spingi/Tira** (PU) sposta una faccia verso l’esterno, o verso l’interno quando la distanza è negativa. Su un’estrusione, la sua estremità cambia la profondità e il suo lato sposta lo spigolo dello schizzo che sta sotto. Su un muro o su qualsiasi altro elemento, tirare aggiunge un solido nel materiale della faccia, e spingere scava nell’elemento.
- **Raccorda spigolo** (FE) arrotonda lo spigolo più vicino al tuo clic, e **Smussa spigolo** (CE) lo smussa. Scegli *tutto attorno alla faccia* per trattare in una volta tutti gli spigoli della faccia.
- **Guscio** svuota i solidi selezionati, con pareti dello spessore che digiti. Può restare chiuso, oppure essere lasciato aperto alla sua estremità, al suo schizzo o a entrambi.
- **Dividi** taglia un solido in due con un piano parallelo alla faccia che scegli, tanto in profondità nella faccia quanto trascini o digiti. Entrambe le parti conservano lo schizzo. **Seziona** conserva un solo lato: scegli **Tieni dietro** o **Tieni davanti** nella barra in fondo alla vista.
- **Serie** ripete il solido in fila, e **Serie polare** lo ripete attorno a un asse verticale.
- **Ruota 3D** ruota il solido attorno a uno spigolo che scegli. **Specchia 3D** lo specchia nel piano di una faccia. **Sposta 3D** mette frecce lungo x, y e z sui solidi selezionati: trascinane una, oppure digita una distanza per l’asse mostrato accanto al valore (<kbd>Tab</kbd> passa all’asse successivo).

![Spingi/tira: la faccia superiore tirata verso l’alto, ciò che aggiunge disegnato in verde, e la distanza accanto alla freccia](shots/studio-push-pull.webp)

![Lo spigolo raccordato: il raccordo segue il blocco quando la sua profondità cambia](shots/studio-fillet.webp)

## Disegnare su una faccia

Nella vista 3D puoi disegnare direttamente su una faccia e poi spingere o tirare ciò che hai disegnato. Il gruppo **Su faccia** della scheda **Studio** contiene **Rettangolo su faccia** (FRE), **Cerchio su faccia** (FCI) e **Poligono su faccia** (FPL).

1. Fai clic su una faccia di qualsiasi elemento: un solido Studio, un muro, un solaio o un pilastro. Il clic è il primo punto: l’angolo di un rettangolo, il centro di un cerchio o il primo vertice di un poligono. La faccia resta evidenziata mentre disegni.
2. Muovi il mouse. La forma lo segue sulla faccia, con la sua misura accanto. I punti si agganciano agli angoli della faccia, ai punti medi dei suoi spigoli e al suo centro; altrimenti procedono di centimetro in centimetro, o di 10 cm con <kbd>Shift</kbd>. Fai clic sull’angolo opposto o su un punto del cerchio. Puoi anche digitare la misura: larghezza e altezza di un rettangolo come `1200, 600`, o il raggio di un cerchio. Un poligono prende un clic su ogni vertice, o una lunghezza digitata nella direzione del mouse. Fai clic sul primo vertice o premi <kbd>Enter</kbd> per chiuderlo; <kbd>Backspace</kbd> toglie l’ultimo vertice.
3. Appena la forma è chiusa, su di essa compare una freccia, come con **Spingi/Tira**. Tirala in fuori e diventa un solido appoggiato sulla faccia, nel materiale della faccia. Spingila in dentro e diventa un taglio nell’elemento: una nicchia, una tasca, o un foro quando lo attraversa da parte a parte. Fai clic o premi <kbd>Enter</kbd> per applicarlo. <kbd>Esc</kbd>, o una distanza di 0, lascia la forma sulla faccia come schizzo, da estrudere più tardi.

La forma è uno schizzo su un piano di riferimento che giace sulla faccia. Il piano prende il nome dall’elemento e dalla faccia, per esempio *Extrude 1, north face*, e le forme disegnate sulla stessa faccia lo condividono. La vista 3D non disegna questi piani, perché la faccia mostra già dove sono. Aprine uno dal browser di progetto per cambiare lo schizzo in 2D; il solido o il taglio lo segue. **Spingi/Tira** sull’estremità del solido ne cambia la profondità. **Spingi/Tira** sul fondo di una tasca rende il taglio più o meno profondo, invece di aggiungere all’elemento.

![Un rettangolo disegnato sul fianco di un blocco, con la sua misura accanto al cursore](shots/studio-on-face.webp)

## Schizzare in 3D

Il gruppo **Schizzo** della scheda **Studio** disegna linee e forme direttamente nella vista 3D, in due modi che memorizzano la stessa cosa: uno schizzo sul piano di riferimento della faccia, come sopra.

**Dritto sulla faccia, come in SketchUp.** Senza uno schizzo aperto, **Linea** (KL), **Rettangolo** (KR) e **Cerchio** (KC) disegnano sulla faccia sotto il cursore. La faccia si illumina quando ci passi sopra; nello spazio vuoto disegni alla quota del piano attivo. Il primo clic fissa il piano.

- Il cursore deduce su cosa si trova e lo dice accanto a sé: **Estremo**, **Punto medio**, **Sullo spigolo**, **Intersezione**, **Su faccia**. Dall’ultimo punto segue gli assi del modello che giacciono sulla faccia, disegnati nei loro colori (rosso x, verde y, blu z), e va **Parallelo allo spigolo** o **Perpendicolare allo spigolo** in magenta.
- <kbd>→</kbd> blocca il rosso, <kbd>←</kbd> il verde, <kbd>↑</kbd> il blu, <kbd>↓</kbd> la direzione dello spigolo dedotto per ultimo; lo stesso tasto lo rilascia. Tieni premuto <kbd>Shift</kbd> per mantenere la direzione dedotta ora.
- Digita un valore in qualsiasi momento, senza fare clic in una casella, e premi <kbd>Enter</kbd>: `1200` è la lunghezza del segmento successivo nella direzione in cui punta il mouse (`1000<30` a 30°), `1200,600` la larghezza e l’altezza di un rettangolo, `300` o `r300` il raggio di un cerchio.
- Una linea prosegue punto per punto. Tornando sul primo punto il giro si chiude e la freccia di spingi/tira si posa subito su di esso; <kbd>Esc</kbd> lo tiene come schizzo. <kbd>Enter</kbd> o <kbd>Esc</kbd> prima di allora lasciano la linea aperta, come schizzo sulla faccia. <kbd>Backspace</kbd> riprende l’ultimo punto. Lo strumento resta attivo per la forma successiva; <kbd>Esc</kbd> senza nulla di disegnato lo termina.

![Una linea disegnata sul fianco di un blocco, l’asse rosso dedotto dal suo ultimo punto](shots/studio-sketch-3d.webp)

**Su un piano di lavoro, come in CAD.** **Schizzo su faccia** (KF) e un clic su una faccia fanno di quella faccia il piano di lavoro attivo: si illumina e un’etichetta dice *Schizzo su* quella faccia. **Schizzo su piano** fa lo stesso con il piano di riferimento selezionato. Finché è attivo, **Linea**, **Rettangolo**, **Cerchio** avviano su di esso i LINE, RECTANGLE e CIRCLE propri della riga di comando, e ogni comando di **Disegno** che digiti funziona su di esso nelle coordinate proprie del piano, con gli snap agli oggetti, l’ortogonale e il tracciamento di una pianta, compresi gli spigoli della faccia. **Termina schizzo** (KE, oppure <kbd>Enter</kbd> senza alcun comando in corso) salda le linee disegnate e mette la freccia di spingi/tira sull’ultima forma chiusa disegnata; <kbd>Esc</kbd> esce dallo schizzo e ciò che hai disegnato resta. **Guarda il piano** (KP) ruota la vista perpendicolarmente al piano. Su una faccia, la x del piano va da sinistra a destra come vedi la faccia dall’esterno, orizzontale, e la sua y sale lungo la faccia (su una faccia superiore x è est e y nord), quindi `@1500,0` va a destra e le misure digitate si leggono allo stesso modo nei due modi.

I due archi fanno ciascuno una sola cosa, con o senza schizzo: **Arco per 2 punti** (KA) è quello di SketchUp (sotto), **Arco per 3 punti** esegue l’ARC della riga di comando per tre punti, sul piano di lavoro quando ce n’è uno attivo, altrimenti al livello del piano.

![Schizzo su faccia: il lato superiore di un blocco illuminato come piano di lavoro, un rettangolo disegnato su di esso con la riga di comando](shots/studio-sketch-plane.webp)

**Le linee dividono le facce.** Una linea tracciata attraverso una faccia, da spigolo a spigolo (o un anello chiuso di linee e archi al suo interno), la divide in regioni. La faccia si illumina regione per regione sotto il cursore, e **Spingi/Tira** su una regione spinge o tira solo quella regione: un solido a sé sulla faccia, oppure un taglio in essa. Le linee che si fermano prima di uno spigolo non dividono nulla. Un anello chiuso disegnato all’interno di una faccia (o di una regione) ne è un foro, come in SketchUp: **Spingi/Tira** sulla faccia che lo circonda lascia fuori l’anello, e l’anello si spinge o si tira da solo.

- **Arco per 2 punti** (KA) disegna sulla faccia sotto il cursore, alla maniera di SketchUp: fai clic sull’inizio, sulla fine e poi su quanto si incurva (oppure digita la lunghezza della corda, poi la freccia).
- **Offset** (KO) fa clic su una faccia, o su una regione di essa, e ne ridisegna il contorno verso l’interno o l’esterno attraverso il cursore, o della distanza digitata; la forma chiusa riceve subito la freccia di spingi/tira.
- Con uno schizzo aperto, **Offset**, **Taglia**, **Estendi** e **Raccordo** eseguono sul piano di lavoro gli OFFSET, TRIM, EXTEND e FILLET della riga di comando.
- Una forma disegnata su un piano di riferimento e selezionata nella vista 3D mostra lì i suoi punti. Trascinane uno: si sposta sul piano, con gli snap degli strumenti di schizzo, e il solido creato dallo schizzo lo segue.
- Entrambi i modi fanno snap anche agli spigoli di altri elementi che giacciono nel piano, come il lato di un blocco vicino a filo con la faccia.

![Una linea attraverso il fianco di un blocco lo divide: la regione sotto di essa viene tirata fuori da sola](shots/studio-sketch-region.webp)

**Metro, guide e Seguimi.**

- **Metro** (KM) misura e lascia guide, come quello di SketchUp. Fai clic su uno spigolo, allontanati e fai clic: una linea guida corre parallela allo spigolo attraverso quel punto, oppure alla distanza che digiti. Fai clic invece su un punto, e il secondo clic mostra la distanza e lascia lì un punto guida. Le guide sono tratteggiate, entrambi i modi di schizzare fanno snap su di esse (e dove incrociano la faccia), sono salvate con il progetto e non vengono mai esportate né stampate. **Cancella guide** le toglie tutte.
- **Seguimi** (FM) fa scorrere una faccia lungo un percorso dove si trova: fai clic sulla faccia (o su una regione di essa divisa da linee), poi sul percorso, che sia una linea disegnata, una polilinea, un arco o un cerchio, oppure un’altra faccia di cui segue tutti gli spigoli, come una faccia superiore per una cornice. Gli angoli sono a sbieco. La faccia diventa uno schizzo sul suo piano e il risultato è un unico solido Studio che segue entrambi gli schizzi.
- **Scorciatoie SketchUp** attiva nella vista 3D i tasti a una lettera di SketchUp, per questo computer: <kbd>L</kbd> linea, <kbd>R</kbd> rettangolo, <kbd>C</kbd> cerchio, <kbd>A</kbd> arco, <kbd>P</kbd> spingi/tira, <kbd>F</kbd> offset, <kbd>T</kbd> metro, <kbd>M</kbd> sposta, <kbd>E</kbd> cancella, <kbd>Spazio</kbd> seleziona. Sono disattivati per impostazione predefinita perché prenderebbero il posto delle scorciatoie a due lettere di Lapin che iniziano con quelle lettere nella vista 3D; le scorciatoie a due lettere e la riga di comando funzionano come prima ovunque altrove.

![Una guida a una distanza digitata dallo spigolo del blocco e una cornice fatta scorrere attorno alla parte superiore con Seguimi](shots/studio-sketch-follow.webp)

## I passi

Le proprietà di un solido Studio elencano i suoi passi in ordine. Ogni passo può essere disattivato, spostato prima o dopo, modificato o rimosso. Cambia lo schizzo, e ogni passo viene rifatto sul nuovo corpo. Un passo che non trova più la sua faccia o il suo spigolo lo segnala in **Problemi**.

![Un cilindro svuotato in un tubo e ripetuto tre volte: i suoi due passi nelle sue proprietà](shots/studio-steps.webp)

Le proprietà di una polilinea elencano i suoi lati. Digita la lunghezza di un lato, e il suo estremo si sposta lungo di esso. I lati successivi lo seguono, fino al lato successivo che va nella stessa direzione, così un rettangolo resta un rettangolo. Anche un solido disegnato su di essa la segue.

## Mesh da altri programmi

**File → Importa mesh (OBJ, STL, glTF)…** porta dentro una mesh come riferimenti che puoi vedere e su cui puoi fare snap. Un file OBJ o STL non dice la sua unità né quale verso è l’alto, quindi Lapin lo chiede. Seleziona un riferimento e fai clic su **Rendi solido Studio** per renderlo modificabile. A quel punto accetta spingi/tira, raccordi, booleane e gli altri passi. Lo stesso strumento funziona su muri, solette ed elementi provenienti da file IFC.

## Booleane

**Sottrai**, **Unione** e **Intersezione** combinano elementi qualsiasi, con il primo selezionato come bersaglio. Gli strumenti restano modificabili, e il risultato li segue. **Mostra operandi** mostra gli strumenti nascosti di un’unione per modificarli. **Rimuovi l’operazione** separa di nuovo gli elementi.
