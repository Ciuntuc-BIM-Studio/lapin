---
title: Sezioni, prospetti e 3D
order: 70
summary: Crea sezioni e prospetti, apri viste 3D e stabilisci che cosa mostra ogni vista e come.
---

Tutte le viste si trovano nel browser di progetto, ordinate sotto la loro disciplina: Architettura, Struttura o Impianti. Fai doppio clic su una vista per aprirla in una scheda.

![Il browser di progetto con una sezione e un prospetto](shots/browser.webp)

## Sezioni

Scegli **Vista → Sezione** (<kbd>SE</kbd>), oppure fai clic su **+** accanto a **Sezioni** nel browser. Poi traccia la linea di sezione in una pianta con due clic, e fai un terzo clic dal lato verso cui guarda la sezione. La sezione è disegnata a partire dal modello e resta aggiornata mentre lo modifichi.

![La sezione A-A attraverso la casa](shots/section-view.webp)

Una sezione non deve per forza essere una sola linea retta. **Sezione spezzata** (<kbd>SJ</kbd>) la disegna come una polilinea, clic dopo clic, e <kbd>Enter</kbd> la termina: ogni tratto rettilineo è un taglio piano che vede solo tra i propri estremi, e i tratti sono disegnati uno accanto all’altro con la loro lunghezza reale, con una sottile linea tratteggiata a segnare ogni spigolo. **Sezione curva** (<kbd>SV</kbd>) disegna un arco per tre punti (l’inizio, la fine, un punto sull’arco) e lo sviluppa allo stesso modo, così una facciata curva si legge nella sua vera lunghezza. **Sezione lungo** (<kbd>SA</kbd>) sceglie un elemento che ha una linea in pianta, una strada o un percorso, un fosso, un muro di sostegno, un muro, una trave, una fondazione o una polilinea disegnata, e lo segue così com’è ora; scrive le progressive lungo il bordo inferiore (0+020, ogni decimo della scala, o il passo che imposti in **Progressive** nelle impostazioni della vista). In pianta, i punti di una sezione sono maniglie: trascinane uno, trascina il centro di un tratto per aggiungere uno spigolo, fai Ctrl/⌘-clic su un punto per toglierlo. Mostrata in 3D, una sezione sviluppata taglia il modello lungo il suo primo tratto e dispone il suo disegno tratto per tratto.

![Una sezione spezzata attraverso la casa: tre tratti uno accanto all’altro, gli spigoli segnati](shots/section-jogged.webp)

Seleziona la sezione nel browser per impostarne la scala, la profondità, la disciplina e lo stile nel pannello Proprietà.

### Dove è segnata una sezione

La linea di una sezione è disegnata in ogni pianta, e la sezione è segnata in ogni altra sezione e in ogni prospetto che il suo piano attraversa: una sottile linea tratteggiata dove il piano taglia la vista, con il nome della sezione e una freccia, in alto, rivolta dove guarda la sezione, della stessa dimensione sulla carta che in pianta. Una sezione non è mai segnata in sé stessa. Fai clic su un indicatore per selezionare la sezione, doppio clic per aprirla.

Ogni sezione sta su un **layer dell’indicatore** (all’inizio **Sezioni**; **Layer dell’indicatore** nelle proprietà della sezione lo cambia), e ogni vista sceglie che cosa segnare. **Vista → Sezioni…** elenca le sezioni del progetto per layer per la vista in primo piano, con una casella per layer e per sezione valida solo per questa vista, il layer di ogni sezione (uno esistente, o **Nuovo layer…**) e in quante viste ciascuna è nascosta. Una tavola segna ciò che segnano le sue viste.

Una sezione tagliata trasversalmente a una trave o a un pilastro (**Sezione qui**, **Sezione trasversale** nella scheda Armature semplici) ha maniglie anche in pianta: trascina uno dei due estremi lungo la linea per dare a quel lato più o meno margine, trascina il centro lungo l’elemento per spostare la progressiva a cui taglia. La finestra della sezione segue.

![Vista → Sezioni… per il prospetto sud](shots/sections-dialog.webp)

## Prospetti

**+** accanto a **Prospetti** crea in un colpo solo i quattro prospetti dell’edificio.

![Il prospetto sud](shots/elevation-view.webp)

### Ombre del sole

Un prospetto, una sezione o una planimetria possono disegnare le ombre che il sole proietta su ciò che vedono, campite sotto le loro linee, così si stampano sulle tavole, in PDF e DXF. Si scelgono nelle proprietà della vista, in **Ombre del sole**:

- **sole proprio**: un sole della vista. Di base la convenzione di disegno: il sole sopra la spalla sinistra, i raggi lungo la diagonale di un cubo (da 45°, a 35,26° di altezza), così un’ombra è profonda quanto sporge ciò che la proietta. **Sole da** e **Altezza del sole** lo ruotano; **Convenzione di disegno** lo riporta.
- **sole del progetto**: il sole del luogo del progetto (Gestisci → Posizione) in un giorno e a un’ora della vista. **Come in Sole e ombre** prende il giorno e l’ora di Vista → Sole e ombre.

Una sezione proietta e riceve ombre solo da ciò che sta oltre il suo taglio. Una pianta di piano non disegna ombre: i suoi muri, tagliati all’altezza della pianta, le proietterebbero troppo corte.

![Il prospetto sud con l’ombra del tetto](shots/elevation-shadows.webp)

![Le ombre del sole di una vista](shots/view-shadows.webp)

### Retini delle superfici

Un prospetto o una sezione retina ogni faccia che vede con il retino **Elevation** del suo materiale (**Gestisci → Materiali**, scheda **2D graphics**): motivo, scala, angolo, penna e riempimento. Si retina solo la parte di una faccia che nessun elemento più vicino nasconde, quindi l’intonaco o il mattone di una facciata si vede intorno a porte e finestre, e una sezione retina ciò che vede oltre il taglio, non dietro gli elementi che taglia. Un muro a strati mostra il materiale del suo strato più esterno. Il retino è disegnato sotto le linee, quindi si stampa sulle tavole, in PDF e in DXF (layer SURFACE-HATCH e SURFACE-FILL).

Con **Alignment** *view* il motivo prosegue da una faccia all’altra; con *layer* si appoggia sulla faccia stessa, lungo la sua orizzontale dal suo angolo in basso, e una faccia vista di scorcio lo mostra accorciato. Un materiale che prosegue nello stesso piano su più elementi è retinato come uno solo, a meno che **Join materials** dello stile di vista sia spento.

Finché non ne scegli uno, il retino di prospetto di un materiale viene **dal nome**, come il suo retino di sezione, e **Gestisci → Materials** mostra quale (*dal nome (BRICK)*): mattoni e muratura BRICK, pietra STONE, legno BOARDS, rivestimento metallico VERTICAL, tegole, scandole e ardesia HORIZONTAL, piastrelle TILES, ghiaia e terra come in sezione. Isolante, membrane, intonaco, pittura e calcestruzzo non ne ricevono: il BATT dell’isolante è un retino di sezione, mai proposto per una superficie (scelto a mano, viene disegnato). Scegli **none** per non disegnare nulla. Una sezione sviluppata (spezzata, curva o lungo un elemento) retina ognuno dei suoi tratti tra i suoi estremi, e vi disegna allo stesso modo le ombre del sole. **Surface hatches** dello stile di vista (**Vista → View settings**, **Gestisci → View styles**) li spegne per viste intere: acceso in Architectural, spento in Structural, MEP e Coordination. Le piante mantengono i propri retini di taglio e di pianta. Il manto di un tetto è retinato secondo il suo materiale: un tetto di tegole mostra i suoi corsi sulle falde che la vista vede, e nessuno su quelle rivolte altrove.

![Il prospetto sud, il mattone a vista retinato secondo il suo nome](shots/elevation-surface-hatch.webp)

![Una sezione spezzata retina il muro del giardino visto dal suo ultimo tratto](shots/section-developed-surface-hatch.webp)

![I retini delle superfici nelle impostazioni della vista](shots/view-surface-hatch.webp)

## Viste 3D

<!-- video section-3d -->

Fai doppio clic su **3D** per aprire la vista 3D. Trascina con il tasto sinistro per orbitare attorno al modello. La pianta chiave in alto a destra mostra da dove stai guardando. **Pianta**, **Persp** e **Adatta** cambiano la camera. Per conservare una camera, duplica la vista 3D.

La rotella del mouse esegue lo zoom verso il punto del modello sotto il cursore, che resta dov'è sullo schermo; lo zoom si ferma appena prima del punto e non lo attraversa mai. Un trascinamento con il tasto sinistro ruota la vista attorno al centro degli elementi selezionati, che resta dov'è sullo schermo, così li vedi da ogni lato; senza selezione ruota attorno al punto del modello sotto il cursore dove inizia il trascinamento, e attorno al centro della vista se sotto il cursore non c'è nulla. **Vista → Zoom al cursore** e **Vista → Orbita attorno alla selezione** disattivano ciascuno (la rotella esegue allora lo zoom verso il centro della vista, e la vista ruota attorno al suo centro), come le caselle nelle proprietà di una vista 3D. La scelta resta su questo computer.

### Una sezione in 3D

Fai clic destro su una sezione nel browser e scegli **Mostra in 3D**. La vista 3D taglia il modello lungo la linea della sezione. Conserva il lato verso cui guarda la sezione, fino alla profondità della sezione se ne ha una. Ogni elemento tagliato è chiuso dove è tagliato, strato per strato, ogni strato nel proprio materiale: un muro mostra il suo nucleo in laterizio e il suo intonaco, pieno come il muro. **Retini** sull’etichetta in basso nella vista 3D posa invece sul taglio il disegno della sezione, con i riempimenti e i retini di taglio che stampa una tavola. Le quote, i simboli di livello, le etichette e i testi della sezione stanno sullo stesso piano, della stessa dimensione e nello stesso punto che nella sezione. Le linee mantengono i loro spessori di penna e i loro tratteggi completi. Quelle disegnate accanto all’edificio stanno sospese in aria accanto al taglio. **Annotazioni** sull’etichetta in basso nella vista 3D le nasconde o le mostra di nuovo. **Mostra in 3D** di un prospetto gira la camera per guardare nella stessa direzione del prospetto.

![Il menu contestuale della sezione nel browser](shots/section-3d-menu.webp)

![La sezione B-B vista in 3D](shots/section-3d.webp)

Per rivedere tutto il modello, fai clic sulla **×** dell’etichetta **Sezione** in basso nella vista 3D, oppure scegli di nuovo **Mostra in 3D**.

![La sezione B-B con Retini attivo: il taglio come lo stampa la tavola](shots/section-3d-hatches.webp)

### Il box di sezione

**Vista → Box di sezione** (scorciatoia BX) taglia la vista 3D con un box. All’inizio il box racchiude tutto il modello. Ogni faccia ha una freccia al centro: trascinala per spostare quella faccia. Ogni elemento tagliato dal box è chiuso su quella faccia, strato per strato, nei suoi materiali, e le facce si incontrano in modo pulito sugli spigoli del box. **Taglia alla pianta** porta la sommità del box all’altezza di taglio della pianta del livello attivo (il suo intervallo di vista), così il livello si legge in 3D come lo taglia la sua pianta. **Adatta** riporta il box attorno a tutto il modello. Per ruotare il box, trascina la maniglia rotonda oltre il suo angolo superiore (grado per grado; tieni premuto Shift per passi di 15°), oppure seleziona un muro e fai clic su **Allinea**: il box ruota nella direzione del muro. **Retini** disegna i tagli del box come una tavola, ogni materiale con il suo riempimento e il suo retino di taglio, al posto dei materiali. Una vista 3D salvata conserva il suo box di sezione: salva la vista con il box attivo, oppure apri una vista salvata e imposta il suo box, e il box sarà lì ogni volta che la vista si apre. La **×** dell’etichetta **Box di sezione** disattiva il box.

![Il box di sezione tagliato alla pianta del piano terra, con la faccia est trascinata verso l’interno](shots/section-box.webp)

![Lo stesso box con Retini attivo: i tagli come li disegna una tavola](shots/section-box-hatches.webp)

### Un piano alla volta

Per guardare dentro l’edificio, mostra solo alcuni dei suoi piani. Fai clic destro su un livello nel browser di progetto e scegli **Mostra solo questo livello in 3D**, oppure **Mostra questo livello e quelli sottostanti in 3D** per vedere l’edificio tagliato a quel livello. Il livello diventa quello attivo, e la vista 3D segue il livello attivo: fai clic su un altro livello nel browser per salire o scendere di un piano.

![Il menu contestuale del livello nel browser](shots/storeys-3d-menu.webp)

![Il piano terra da solo in 3D](shots/storeys-3d.webp)

**Vista → Piani mostrati**, o le proprietà della vista 3D, impostano il resto:

- **Mostra**: tutti i piani, i piani spuntati, solo il piano attivo, oppure il piano attivo e quelli sottostanti.
- **Su nessun piano**: ciò che non sta su alcun piano (il sito, il terreno, i file collegati) è mostrato o nascosto.
- **Taglia sopra il suo livello**: il modello è tagliato a quella distanza sopra il piano più alto mostrato, all’inizio 1.20 m, come lo taglia una pianta. Ogni elemento tagliato lì è chiuso sul taglio, strato per strato, nei suoi materiali, come una sezione mostrata in 3D.

![Il piano terra tagliato a 1.20 m: i suoi muri chiusi, strato per strato](shots/storeys-3d-cut.webp)

I piani nascosti non sono disegnati, e non puoi selezionarli, adattare la vista a essi né camminarci sopra. Anche ciò che un taglio toglie (il taglio sopra i piani mostrati, una sezione mostrata in 3D, il box di sezione) non c’è durante una camminata: passi oltre la parte superiore di un muro che il taglio ha rimosso, e le chiusure sul taglio chiudono ciò che esso apre. **File → Esporta 3D…** prende la vista 3D come la mostra (vedi [Importazione ed esportazione](import-export.md)). Una vista 3D salvata conserva la propria scelta. L’etichetta **Piani** in basso nella vista 3D dice che cosa è mostrato; la sua **×** mostra di nuovo tutti i piani.

### Senza finiture, o solo i nuclei

Una vista 3D può mostrare la sola struttura dell’edificio, come fa una pianta strutturale. Nelle proprietà della vista 3D (con niente selezionato), oppure in **Vista → Impostazioni di vista** con la vista 3D in primo piano, **Layer** mostra:

- **tutti gli strati**: i muri, i solai e le fondazioni come sono costruiti;
- **senza finiture**: senza gli strati il cui materiale è una finitura (intonaco, intonaco esterno, pittura, piastrelle, lastre, pavimentazioni, o un materiale la cui categoria è Finitura in Gestisci → Materiali); l’isolamento resta;
- **solo il nucleo**: solo lo strato indicato come nucleo (il laterizio, la soletta in calcestruzzo).

![Una casa in 3D con il solo nucleo: i muri sono il loro laterizio, i solai la loro soletta](shots/layers-3d-core.webp)

Ogni strato conservato resta un solido chiuso nel proprio materiale, così un taglio o il box di sezione lo chiudono come prima. Porte e finestre restano nelle loro aperture; una mazzetta rivestita con una finitura va con le finiture del muro. Una vista 3D prende i suoi strati dal suo stile, come una pianta: imposta la sua **Disciplina** su Struttura e mostra solo i nuclei; imposta **Layer** per scegliere diversamente, e il pulsante accanto torna a quelli dello stile. L’etichetta **Strati mostrati** in basso nella vista 3D dice che cosa è escluso; la sua **×** mostra di nuovo tutti gli strati. Gli strati nascosti non si selezionano né ostacolano una camminata, e **File → Esporta 3D…** come lo mostra la vista esporta gli stessi strati.

### Camminare in un locale

In una pianta, fai clic destro all’interno di un locale e scegli **Cammina in 3D**. La vista 3D ti mette dentro il locale, a 1.60 m sopra il pavimento, rivolto lungo il suo lato più lungo.

![Il menu contestuale di un locale nella pianta](shots/room-walk-menu.webp)

![In piedi nel soggiorno](shots/room-walk.webp)

- Trascina con il tasto sinistro per guardarti attorno.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> o i tasti freccia ti fanno camminare. Tieni premuto <kbd>Shift</kbd> per correre. <kbd>Space</kbd> salta.
- La rotellina del mouse fa un passo avanti o indietro.

Cammini come camminerebbe una persona. Muri, pilastri, arredi e finestre chiuse ti fermano, e scivoli lungo di essi. Stai in piedi sui solai, sali e scendi scale e rampe, e cadi se esci da un bordo. Le porte sono aperte per te: passa attraverso le loro aperture. Ti ferma solo ciò che la vista 3D disegna: gli elementi nascosti, i piani che non mostra e ciò che i suoi tagli tolgono non ti sono d’intralcio.

![Camminando verso la porta della camera](shots/room-walk-door.webp)

![In cima alla scala](shots/room-walk-stair.webp)

**Vola** nell’etichetta della camminata, oppure <kbd>F</kbd>, ti fa volare: niente gravità, e attraversi muri e solai. <kbd>E</kbd> e <kbd>Q</kbd> salgono e scendono. Premi di nuovo <kbd>F</kbd> per camminare; ricadi sul solaio sotto di te.

<kbd>Esc</kbd> o **Esci dalla camminata** riporta la camera che avevi prima.

### Realistica, con il sole

Vista → **Qualità di visualizzazione** stabilisce la resa della vista 3D, su questo computer:

- **Disegno**: la tavolozza della tavola e una luce uniforme, come Lapin ha sempre disegnato. Veloce su qualsiasi computer.
- **Media**: le ombre del sole, l’ombra negli angoli dove le facce si incontrano e la luce del cielo.
- **Realistica**: il cielo dietro il modello e un terreno sotto (dove il progetto non ha terreno), e vetro attraversato dalla luce. Per presentare, con una buona scheda grafica.

![Una casa nella vista realistica](shots/view-realistic.webp)

Il sole è quello del progetto: dove si trova per il luogo del progetto (Gestisci → **Posizione**) in un giorno a un’ora. Vista → **Sole e ombre** stabilisce il giorno (l’equinozio e i solstizi sono a un clic) e l’ora, riproduce la giornata e accende il sole anche nella vista disegno. Senza sistema di coordinate e punto di rilievo, il luogo è Bucarest. Il fuso orario segue la longitudine del luogo, con l’ora legale europea in Europa.

![Sole e ombre](shots/sun-dialog.webp)

La vista realistica prende alcuni numeri (l’ombra negli angoli, la qualità delle ombre, il colore del sole) da [BimGo](https://github.com/aussieBIMguru/BimGo), di Aussie BIM Guru (MIT-0).

## Modellare in 3D

La vista 3D è un luogo in cui modellare, non solo da guardare: gli elementi vi si spostano e copiano come in pianta, agganciati a spigoli, bordi e facce degli altri elementi.

### Spostare e copiare in 3D

Seleziona gli elementi, poi **Move** (M) o **Copy** (CO) con la vista 3D davanti. Fai clic sul punto base: uno spigolo, un bordo o una faccia di ciò che sposti. Il cursore dice a cosa si aggancia: **Endpoint** (estremo), **Midpoint** (punto medio), **On edge** (sul bordo), **On face** (sulla faccia) o **On level** (sul livello) nel vuoto; ciò che sta dietro la faccia sotto il cursore non viene preso. Anche le linee di un DXF importato, mostrato in 3D, agganciano. Poi fai clic dove va, per esempio lo spigolo di un solaio: la selezione segue il cursore in 3D, anche in su e in giù, e vi viene posata. Dal punto base si deducono gli assi rosso, verde e blu quando il cursore ne segue uno; → ← ↑ bloccano l'asse rosso, verde o blu (lo stesso tasto lo rilascia), Maiusc mantiene l'asse dedotto. Con un asse bloccato, uno spigolo sorvolato viene portato sull'asse, per allineare lo spostamento con esso senza andarci.

Scrivi un valore in qualsiasi momento, senza fare clic in una casella: una lunghezza va nella direzione del cursore (1200), e dx,dy o dx,dy,dz è lo spostamento stesso (0,0,450 alza di 450 mm). Invio lo applica, Esc annulla. Copy continua a copiare dallo stesso punto base fino a Invio o Esc.

Ciò che ha una quota propria sale o scende con lo spostamento: la base di un muro, di un pilastro o di un oggetto, la base e la sommità di una scala (la sua alzata resta), la sommità di una trave, lo scostamento superiore di un solaio, il dormiente di un tetto; un solido Studio riceve un passo di spostamento. Una porta o una finestra scorre lungo il suo muro.

Puoi anche premere su un elemento selezionato e trascinarlo: si sposta allo stesso modo, dal punto premuto, agganciato, e si posa dove lo rilasci. **View → Drag to move in 3D** lo disattiva, perché un trascinamento sulla selezione faccia orbitare la vista.

![Una scala spostata in 3D dallo spigolo del gradino più alto sullo spigolo del solaio](shots/model-3d-move.webp)

### Posare elementi in 3D

Ogni strumento che prende punti in pianta funziona anche nella vista 3D: muri, facciate continue, solai, tetti, controsoffitti, fori nel solaio, locali, pilastri, travi, fondazioni, parapetti, battiscopa, arredi, ascensori, strade, piante e gli altri. I punti vanno sul livello attivo: il cursore si aggancia al modello come nello spostamento (lo spigolo di un solaio, l'estremo di una trave, il centro di un bordo, le linee di un DXF) e il punto è preso subito sotto, sul livello; una linea tratteggiata mostra dove, quando l'aggancio è sopra o sotto. I punti e il contorno che formano sono disegnati sul livello man mano: la linea di un muro, il contorno di un solaio che si chiude sul primo punto, un arco per il suo terzo punto. Dall'ultimo punto si deducono gli assi rosso e verde; → e ← li bloccano.

Scrivi un valore in qualsiasi momento: una lunghezza parte dall'ultimo punto nella direzione del cursore (3000, o 3000<30 con un angolo), larghezza,altezza di un rettangolo dal primo angolo. Invio lo applica, chiude un solaio o termina un parapetto; Backspace toglie l'ultimo punto; Esc scarta i punti e, la seconda volta, lo strumento. Un trascinamento continua a ruotare la vista, così puoi girare attorno al modello tra due clic. Il chip in basso dice su quale livello vanno i punti.

Porte e finestre vanno dove fai clic sulla faccia di un muro in 3D, nel punto del muro dove hai fatto clic.

![Un muro iniziato sullo spigolo del solaio in 3D, con l'asse verde dedotto dal suo inizio](shots/model-3d-wall.webp)

### Maniglie e componenti in 3D

Le maniglie di un elemento selezionato (gli estremi di un muro, gli angoli di un solaio) si trascinano in 3D come in pianta e si agganciano a spigoli, punti medi e bordi degli altri elementi, con il punto preso all'altezza della maniglia.

Trascina una scheda dal **Catalog** o uno strumento dalla barra multifunzione sulla vista 3D: il suo strumento parte con quel tipo. Uno strumento che posa con un clic (un pilastro, un plinto, un arredo, un ascensore, una pianta) viene posato subito dove lo rilasci, agganciato al modello. Rilasciato, o con un clic, sul solaio di un altro piano, l'elemento va su quel piano: un tavolo posato sul solaio del primo piano appartiene al primo piano, qualunque sia il livello attivo.

## Che cosa mostra una vista

La scheda **Vista** imposta che cosa mostra la vista in primo piano e come appare.

**Griglia** (<kbd>F7</kbd>) nasconde o mostra la griglia di sfondo della pianta, e **Linee sottili** (<kbd>TL</kbd>) disegna ogni linea spessa un pixel, per leggere un disegno fitto. Entrambe valgono solo per lo schermo: tavole, PDF e DXF mantengono le loro penne, e lo snap alla griglia resta. La griglia a terra della vista 3D segue lo stesso interruttore.

![La scheda Vista](shots/ribbon-view.webp)

- **Impostazioni di vista.** La disciplina e lo stile della vista, il suo intervallo (l’altezza e la profondità di taglio) e la sua grafica.
- **Stili di vista.** Architettonico, Strutturale (solo i nuclei), Impianti o i tuoi. Ogni stile sceglie gli strati disegnati (tutti, senza finiture, o il nucleo) e come si uniscono i materiali.
- **Simboli 2D IFC.** Gli elementi importati o collegati da un file IFC che portano un proprio disegno in pianta (un’impronta, l’apertura di una porta, il simbolo di un apparecchio, con i colori, i riempimenti e i testi del file) lo disegnano nelle piante al posto del loro taglio. Togli la spunta in Impostazioni di vista (o in uno stile di vista) per disegnare invece il loro taglio 3D.
- **Visibilità** (<kbd>VV</kbd>) e **Grafica** (<kbd>VG</kbd>). Mostrano o nascondono le categorie e ne impostano penne, tipi di linea e riempimenti.
- **Dividi** e **Finestra.** Mostrano due viste affiancate, o aprono una vista in una finestra separata.
- **Qualità di visualizzazione.** La resa della vista 3D (**Disegno**, **Media** o **Realistica**, salvata su questo computer: vedi *Realistica, con il sole*) e come la vista 3D disegna le parti fatte di molti triangoli (un impianto importato, arredi, tubazioni) quando sono lontane. **Bilanciata** disegna una parte lontana in modo più grossolano solo dove la differenza resta sotto un pixel. **Veloce** le lascia scostarsi fino a 3 pixel, per modelli grandi e computer più lenti. **Precisa** disegna ogni parte così com’è. L’impostazione appartiene al progetto. Il modello, i suoi disegni, le esportazioni e le quantità non sono mai semplificati: una curva è fatta di tratti rettilinei che se ne scostano al massimo di un millimetro.
- **Sole e ombre.** Il sole del progetto in un giorno a un’ora, che illumina la vista 3D con le sue ombre (vedi *Realistica, con il sole*).

![Qualità di visualizzazione](shots/display-quality.webp)

### Visibilità / Grafica

**Visibilità** (<kbd>VV</kbd>) o **Grafica** (<kbd>VG</kbd>) apre visibilità / grafica della vista in primo piano: una pianta, una sezione, un prospetto o una vista 3D. Ciò che nasconde è nascosto anche sulle tavole della vista, nel suo PDF e DXF, e in un'esportazione 3D.

- **Categorie di modello.** Ogni categoria si mostra o si nasconde, con colore e spessore della linea, riempimento e retino di sezione. La freccia accanto a una categoria apre le sue sottocategorie: **Nucleo**, **Finiture**, **Isolamento**, **Membrane** e **Altri strati** di un muro, un solaio o un tetto (secondo il materiale di ogni strato e la sua categoria in Gestisci → Materiali), le **Spallette** di una porta o di una finestra. Togli la spunta a **Finiture** sotto **Muri** e l'intonaco sparisce, il mattone resta. Le armature, i rivestimenti, le casseforme, i ponteggi, gli impianti, i giunti in acciaio, gli ascensori e le schermature solari sono categorie a sé.
- **Collegamenti.** Ogni file collegato, con **Visibile** e **Mezzitoni** solo per questa vista. **Visualizzazione** è **Come la vista ospite** (il collegamento prende le categorie di questa vista) o **Personalizzato**: allora **Categorie…** dà al collegamento le sue categorie, sottocategorie e classi IFC in questa vista. La pianta dell'architetto nasconde le armature del collegamento strutturale; quella dell'ingegnere le finiture del collegamento architettonico.
- **Classi IFC.** Le classi degli elementi propri del progetto, mostrate o nascoste.

Le impostazioni sono della vista, o di un insieme con nome condiviso da più viste (**Salva come insieme…**). Uno stile di vista può nominare un insieme (Gestisci → Stili di vista): una vista senza impostazioni proprie prende l'insieme del suo stile, come i modelli di vista di Revit; **Rendili propri di questa vista** parte da esso per le sue.

![Visibilità / Grafica: le finiture di un muro nascoste](shots/vg-categories.webp)

![Una struttura collegata, personalizzata in una vista](shots/vg-links.webp)
