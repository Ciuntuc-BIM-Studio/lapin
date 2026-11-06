---
title: Solai e tetti
order: 40
summary: Disegna solai e tetti dal loro contorno, scegli la forma e i bordi di un tetto e taglia le aperture.
---

## Solai

Scegli **Architettura → Solaio** (<kbd>SB</kbd>), poi fai clic sugli angoli del suo contorno. Chiudi il contorno sul primo punto, oppure premi <kbd>Enter</kbd>. Anche un solaio è fatto di strati, impostati in **Architettura → Tipi di solaio**. Uno strato di un tipo di solaio può avere spessore 0 (un’impermeabilizzazione, la pittura): non costruisce nulla, è disegnato come una linea nelle sezioni ed è misurato sulla superficie del solaio.

Per cambiare il contorno, seleziona il solaio e trascinane gli angoli. **Foro nel solaio** (<kbd>SH</kbd>) taglia un foro in un solaio, per una scala o un cavedio.

Un solaio disegnato fuori dai muri, davanti a una porta, è un portico. Per i gradini che vi salgono, vedi [Scale](stairs.md).

## Tetti

Scegli **Architettura → Tetto** (<kbd>RF</kbd>) e fai clic sul contorno dei muri sottostanti. Lapin costruisce il tetto su quel contorno, alla sommità del suo livello.

![Una casa con un tetto a due falde nella vista 3D](shots/house-3d.webp)

Seleziona il tetto per modificarlo nel pannello Proprietà:

- **Forma.** A padiglione, a due falde, a una falda, a mansarda, a botte o piano. Un tetto a botte è una volta curva tra due timpani; **Volta a botte: freccia in chiave** stabilisce quanto sale in chiave, al massimo metà della sua luce.
- **Pendenza e sporto** per l’intero tetto.
- **Direzione del colmo (a due falde, a una falda)**: auto, x o y. Il colmo di un tetto a una falda è il suo lato alto: su x scende verso −y, su y verso −x; **Una falda: inverti la pendenza** lo fa scendere dall’altra parte.
- **Ogni bordo** con il proprio tipo (gronda o timpano), pendenza e sporto. Una mansarda con bordi a timpano è un tetto alla olandese (gambrel).
- **Stile.** Gli stili di tetto danno un intero aspetto con una sola scelta: la copertura, le gronde, le grondaie e i pluviali.
- **Materiale del manto.** Il manto di copertura è di un materiale del progetto, per impostazione quello del manto scelto (Clay tile, Metal sheet, Slate…), creato nel colore del manto la prima volta che un tetto ne ha bisogno. Sceglietene un altro nelle proprietà o in **Copertura e smaltimento acque** del configuratore; cambiando il manto cambia anche lui, a meno che non ne abbiate scelto uno. Prospetti e sezioni retinano il manto secondo quel materiale (coppi, scandole e ardesia a corsi orizzontali, lamiera a giunti verticali, se non gli date un retino **Elevation** proprio), una sezione disegna il manto tagliato con il retino di sezione del materiale, e l’IFC, il computo e il preventivo elencano il manto sotto il suo materiale.
- **Spessore del manto.** Vuoto, il manto è spesso quanto quello del catalogo (una membrana 2 cm, la lamiera 3 cm, le tegole 5 cm…); scrivine uno per dargli il suo. Un tetto importato da un altro programma mantiene quello del file.
- **Solaio.** Un tetto piano, una volta a botte e un tetto con **Structure** none hanno un solaio sotto il manto, una parte a sé: **Materiale del solaio** (per impostazione Reinforced concrete, creato la prima volta che un tetto ne ha bisogno) e **Spessore del solaio** (per impostazione 16 cm), nelle proprietà o in **Orditura** nel configuratore. Il manto vi poggia sopra, quindi il tetto resta spesso quanto i due insieme. Una sezione disegna due fasce, ciascuna tratteggiata secondo il suo materiale; le finestre da tetto attraversano entrambe; un muro agganciato a un tetto piano sale fino all’intradosso del solaio; l’IFC scrive solaio e manto come due solette di copertura dei loro materiali, e il computo e la stima misurano il solaio (superficie e volume) e il manto (superficie), ciascuno sotto il suo materiale. Le proprietà mostrano questi due campi solo su un tetto senza orditura, e i campi dei travetti (sezioni, interasse, classe, arcarecci, teste) solo su uno con orditura; un campo proprio di una sola forma (quello della mansarda, la freccia della volta, l'inversione del tetto a una falda) compare solo su quella forma.

![Le proprietà di un tetto](shots/roof-properties.webp)

![Un tetto piano in sezione: il solaio in calcestruzzo e il manto sopra, ciascuno tratteggiato secondo il suo materiale](shots/roof-deck-section.webp)

**Configuratore del tetto** (il pulsante accanto allo stile, oppure **Architettura → Configuratore del tetto** con un tetto selezionato) riunisce tutto in un unico posto: gli stili a sinistra, poi la forma e i suoi bordi, la copertura e lo smaltimento delle acque, l’orditura e le aperture, sopra una pianta del tetto (fai clic su un bordo per renderlo auto, gronda o timpano) e, per l’orditura, una sezione trasversale al colmo con la capriata o la coppia di puntoni al centro. Scegli uno stile da cui partire e modifica a mano qualsiasi campo; **Applica** ne fa un unico passo da annullare. **Salva come stile…** conserva il tetto così come è configurato come stile del progetto, elencato sotto **Progetto** sopra quelli del catalogo (e nell’elenco del pannello Proprietà) per altri tetti e per quelli nuovi; **Aggiorna lo stile** vi salva di nuovo le modifiche, e tutti i tetti di quello stile si adeguano. Uno stile del progetto si può rinominare o eliminare; i suoi tetti mantengono il loro aspetto.

![Il configuratore del tetto](shots/roof-configurator.webp)

**Grondaie e pluviali.** In **Copertura e smaltimento acque** del configuratore, la grondaia prende la sua dimensione, quanto sporge dalla gronda e quanto pende sotto la copertura, la sua pendenza verso gli scarichi (mm/m) e i suoi tappi di testata; la tabella dei bordi attiva o disattiva la grondaia di ogni gronda. I pluviali nascono come parte del tetto, uno ogni 10 m; **Rendili modificabili** li trasforma in elementi, e **Architettura → Pluviale** ne aggiunge uno con un clic su una grondaia. La maniglia di un pluviale lo fa scorrere solo lungo la sua grondaia; le sue proprietà impostano il diametro, il gomito di scarico al piede e il percorso, una catena di segmenti ciascuno con la sua lunghezza e il suo angolo dalla verticale (verso il muro), l’ultimo fino a terra. In IFC una grondaia è un IfcPipeSegment GUTTER e un pluviale un segmento rigido chiamato Downpipe.

I muri sul livello del tetto si fermano sotto di esso e salgono da soli fino ai suoi timpani, anche sotto un tetto curvo. Un muro su un altro livello segue il tetto dopo **Architettura → Collega al tetto** (<kbd>AR</kbd>): seleziona i muri, poi fai clic sul tetto. Lo stesso strumento accetta qualsiasi altro corpo sopra i muri: un solido di Studio, una soletta, una trave, un elemento importato da IFC. I muri salgono fino al suo intradosso e vengono tagliati lungo di esso, qualunque sia la sua forma. **Scollega dal tetto** lo annulla.

**Finestra da tetto** e **Abbaino** aggiungono aperture a un tetto: fai clic su una falda. Appartengono al tetto. Seleziona il tetto, e ognuno mostra le sue maniglie in pianta. Trascina la maniglia al centro per spostarlo sul tetto. Trascina le maniglie dei bordi o degli angoli di una finestra da tetto per ridimensionarla sulla falda (il bordo opposto resta dov'è; puoi digitare la lunghezza mentre trascini). La larghezza corre lungo la gronda e la lunghezza su per la falda, misurata sulla falda. Un abbaino ha una maniglia su ogni lato per la sua larghezza. Una modifica che non ci starebbe sul tetto è rifiutata. Per toglierlo, fai clic destro su una sua maniglia e scegli **Rimuovi finestra da tetto** (o **Rimuovi abbaino**), premi **Canc** con il cursore sopra la sua maniglia, oppure fai **Ctrl/⌘-clic** sulla maniglia al centro. Anche le proprietà del tetto li elencano: scrivi lì larghezza e lunghezza di una finestra, oppure fai clic su **×** per toglierla. Il tetto di un abbaino è coperto con il materiale del manto e misurato con esso: le quantità del tetto danno a parte il **manto degli abbaini** (l'area delle sue falde, il numero di abbaini), così il computo per materiale, il preventivo e un'attività di programma sul manto contano le falde del tetto (meno i tagli degli abbaini) e i tetti degli abbaini, ognuno una volta.

![La maniglia di una finestra da tetto sul tetto selezionato](shots/roof-window-grip.webp)

### Orditura in legno

**Struttura** stabilisce come è realizzata l’orditura del tetto:

- **rafters**: puntoni su arcarecci, con una trave di colmo.
- **collar**: coppie di puntoni con una catena alta orizzontale a **Catene alte: frazione dell’altezza** (due terzi per impostazione predefinita), senza arcarecci.
- **trusses**: capriate prefabbricate lungo il colmo principale, all’**Interasse dei puntoni**: correnti superiori sulle falde, un corrente inferiore sui dormienti, aste di parete a W (una capriata a una falda su un tetto a una falda). Le testate di un tetto a padiglione mantengono i loro puntoni.
- **nessuno**: un tavolato, senza legname.

**Puntoni e correnti** accetta le vecchie sezioni in cm o quelle nordiche in mm (da 45×95 a 45×245); con esse vanno **Aste di parete delle capriate** e **Classe di resistenza del legno** (da C14 a C30, lamellare). I puntoni appoggiano sul dormiente con un intaglio a bocca di lupo (**Appoggio dei puntoni sul dormiente**) e terminano in gronda con un taglio a piombo o a squadra (**Terminazione dei puntoni in gronda**). Gli stili **Swedish truss roof** e **Nordic collar-tie roof** portano con sé la loro orditura.

Ogni elemento è una parte a sé: l’abaco **Roof timber** li elenca come distinta di taglio (elemento, sezione, classe, lunghezza, pezzi, volume), e in IFC ciascuno è un IfcMember del suo tipo, ogni capriata un IfcElementAssembly.

## Controsoffitti

**Finiture → Controsoffitto** (<kbd>CG</kbd>) disegna un controsoffitto su un contorno di cui fai clic sugli angoli; **Controsoffitto in un locale** riempie il locale su cui fai clic, seguendone il contorno. Un controsoffitto è sospeso alla sua **Altezza** sopra il piano (2700 mm, a meno che tu non la cambi nelle sue proprietà).

Il suo tipo, in **Finiture → Tipi di controsoffitto**, imposta il **Sistema**: **Quadrotti su orditura a vista** (larghezza, lunghezza e spessore del quadrotto, larghezza in vista, altezza e rotazione dell’orditura, pendini fino alla struttura con il loro interasse) oppure **Cartongesso, continuo**. L’orditura è centrata sul controsoffitto, così i quadrotti tagliati ai bordi risultano uguali.

**Pannello luminoso** e **Diffusore d’aria** ne mettono uno nella cella su cui fai clic; fai di nuovo clic sulla cella con l’altro strumento per cambiarlo. Nel pannello Proprietà, **Distribuisci** mette pannelli luminosi o diffusori d’aria ogni tot celle, e **Svuota** li toglie. Gli abachi contano i quadrotti interi, i pannelli luminosi e i diffusori, e il controsoffitto va in IFC come un IfcCovering di tipo CEILING.

![Le proprietà di un controsoffitto: il suo tipo, l’altezza e i corpi installati](shots/ceiling-properties.webp)

![Controsoffitti a quadrotti con pannelli luminosi e diffusori d’aria, in 3D](shots/ceilings-3d.webp)

## Battiscopa

**Finiture → Battiscopa nel locale** (<kbd>SK</kbd>) posa un battiscopa tutt’intorno al locale cliccato, lungo le facce dei muri, interrotto a ogni porta (qualsiasi apertura che scende fino al pavimento); un clic tra muri senza locale li segue allo stesso modo. Il battiscopa di un locale segue il locale: sposta un muro o una porta e il battiscopa va con loro. **Battiscopa** ne disegna uno lungo una linea cliccata sulla faccia di un muro (Invio termina); il battiscopa va dal lato opposto al muro, e **Cambia lato** nelle sue proprietà lo gira.

Il suo tipo, in **Finiture → Tipi di battiscopa**, fissa il **Profilo** — **Liscio (squadrato)**, **Smussato (MDF)**, **Sagomato (legno)**, **Con sguscia (metallo)**, **Bordo a matita** o **Bordo arrotondato**, disegnato accanto in scala —, l’**Altezza** (da 40 a 300 mm), lo **Spessore** (da 5 a 40 mm), il **Materiale** e il suo colore. **Dalla libreria** porta nel progetto tipi già pronti: MDF 80 bevelled, Oak 70 moulded, Tile 80 flush, Aluminium 60 cove, Pencil 50 e Bullnose 70. Finché uno strumento battiscopa è attivo, il pannello delle proprietà mostra il tipo che posa, da scegliere prima del clic.

Nelle sue proprietà un battiscopa ha il suo tipo, un **Offset di base** sopra il pavimento e **Interrotto alle porte** (disattivato, passa davanti). **Abaco dei battiscopa** (o **Battiscopa** nell’elenco dei nuovi abachi del browser di progetto) li elenca per tipo con i pezzi e la lunghezza, senza i vuoti delle porte. Il battiscopa è tagliato a 45° negli angoli; una pianta ne disegna il bordo anteriore, e un battiscopa va in IFC come un IfcCovering di tipo SKIRTINGBOARD, di un IfcCoveringType dello stesso tipo.

![I tipi di battiscopa: il profilo disegnato in scala, la libreria](shots/skirting-types.webp)

![Le proprietà di un battiscopa: il suo tipo, l’offset di base e i vuoti alle porte](shots/skirting-properties.webp)
