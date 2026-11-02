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

Una sezione tagliata trasversalmente a una trave o a un pilastro (**Sezione qui**, **Sezione trasversale** nella scheda Armature 2D) ha maniglie anche in pianta: trascina uno dei due estremi lungo la linea per dare a quel lato più o meno margine, trascina il centro lungo l’elemento per spostare la progressiva a cui taglia. La finestra della sezione segue.

![Vista → Sezioni… per il prospetto sud](shots/sections-dialog.webp)

## Prospetti

**+** accanto a **Prospetti** crea in un colpo solo i quattro prospetti dell’edificio.

![Il prospetto sud](shots/elevation-view.webp)

## Viste 3D

<!-- video section-3d -->

Fai doppio clic su **3D** per aprire la vista 3D. Trascina con il tasto sinistro per orbitare attorno al modello. La pianta chiave in alto a destra mostra da dove stai guardando. **Pianta**, **Persp** e **Adatta** cambiano la camera. Per conservare una camera, duplica la vista 3D.

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

## Che cosa mostra una vista

La scheda **Vista** imposta che cosa mostra la vista in primo piano e come appare.

**Griglia** (<kbd>F7</kbd>) nasconde o mostra la griglia di sfondo della pianta, e **Linee sottili** (<kbd>TL</kbd>) disegna ogni linea spessa un pixel, per leggere un disegno fitto. Entrambe valgono solo per lo schermo: tavole, PDF e DXF mantengono le loro penne, e lo snap alla griglia resta.

![La scheda Vista](shots/ribbon-view.webp)

- **Impostazioni di vista.** La disciplina e lo stile della vista, il suo intervallo (l’altezza e la profondità di taglio) e la sua grafica.
- **Stili di vista.** Architettonico, Strutturale (solo i nuclei), Impianti o i tuoi. Ogni stile sceglie gli strati disegnati (tutti, senza finiture, o il nucleo) e come si uniscono i materiali.
- **Simboli 2D IFC.** Gli elementi importati o collegati da un file IFC che portano un proprio disegno in pianta (un’impronta, l’apertura di una porta, il simbolo di un apparecchio, con i colori, i riempimenti e i testi del file) lo disegnano nelle piante al posto del loro taglio. Togli la spunta in Impostazioni di vista (o in uno stile di vista) per disegnare invece il loro taglio 3D.
- **Visibilità** (<kbd>VV</kbd>) e **Grafica** (<kbd>VG</kbd>). Mostrano o nascondono le categorie e ne impostano penne, tipi di linea e riempimenti.
- **Dividi** e **Finestra.** Mostrano due viste affiancate, o aprono una vista in una finestra separata.
- **Qualità di visualizzazione.** Come la vista 3D disegna le parti fatte di molti triangoli (un impianto importato, arredi, tubazioni) quando sono lontane. **Bilanciata** disegna una parte lontana in modo più grossolano solo dove la differenza resta sotto un pixel. **Veloce** le lascia scostarsi fino a 3 pixel, per modelli grandi e computer più lenti. **Precisa** disegna ogni parte così com’è. L’impostazione appartiene al progetto. Il modello, i suoi disegni, le esportazioni e le quantità non sono mai semplificati: una curva è fatta di tratti rettilinei che se ne scostano al massimo di un millimetro.

![Qualità di visualizzazione](shots/display-quality.webp)
