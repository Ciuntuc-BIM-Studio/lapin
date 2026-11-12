---
title: Struttura
order: 50
summary: Pilastri, travi e fondazioni, i loro tipi e come si incontrano.
---

La scheda **Struttura** contiene il telaio e le fondazioni.

![La scheda Struttura](shots/ribbon-structure.webp)

## Telaio

- **Pilastro** (<kbd>CL</kbd>): fai clic dove si trova. Un pilastro va dal suo livello al livello superiore.
- **Trave** (<kbd>BM</kbd>): fai clic sulle sue due estremità. **Trave curva** (<kbd>BC</kbd>) segue un arco.
- **Pilastri sulla griglia** mette un pilastro in ogni incrocio degli assi della griglia. Per disegnare gli assi, usa **Annota → Asse di griglia** (<kbd>GR</kbd>).

## Fondazioni

- **Trave di fondazione** (<kbd>SF</kbd>) corre sotto i muri. I suoi strati esterni sono tagliati a quartabuono agli angoli.
- **Trave di fondazione curva** (<kbd>CF</kbd>) segue un arco.
- **Plinto** (<kbd>PF</kbd>): fai clic sotto un pilastro. Finché lo strumento è attivo, aggancia i centri dei pilastri del livello della pianta e del livello sopra.
- **Plinti sotto i pilastri** mette un plinto sotto ogni pilastro selezionato che non ne ha, nel suo centro e ruotato come esso. Senza pilastri selezionati, prende i pilastri del livello della pianta (in una pianta delle fondazioni, quelli del livello sopra).

Disegna le fondazioni sul livello **Foundations**, oppure dai loro una pianta propria.

**Una fondazione continua è solo il calcestruzzo**, a meno che non chiedi il muro. Con **Trave di fondazione** o **Trave di fondazione curva** attivo, la barra della riga di comando offre **Solo la fondazione** (predefinito: una fondazione in calcestruzzo di 600 × 400 mm, un tipo a profilo creato la prima volta) o **Con muro** (il colletto di un tipo di fondazione continua sopra, con i suoi strati) e quale tipo con muro usare. La scelta viene ricordata. Una fondazione già disegnata cambia con il suo tipo in **Tipi strutturali**.

![L'opzione dello strumento fondazione continua nella riga di comando: solo la fondazione o con muro](shots/structure-strip-option.webp)

**Nelle piante.** Una fondazione sta sotto il solaio, quindi una pianta la disegna tratteggiata nella pianta del livello su cui poggia, come la pianta di un edificio mostra ciò che c'è sotto. Per vederla tagliata e vista, usa una **pianta delle fondazioni**: nel browser di progetto il **+** accanto a **Piante** ne crea una per il livello attivo (taglio 0,50 m sotto, vista 2,50 m verso il basso).

**Disegnare in una pianta delle fondazioni.** Mostra anche il livello sotto, e ciò che vi disegni con uno strumento per solaio, muro, pilastro o trave va su quel livello (le Fondazioni di un nuovo progetto, a −1,50); gli strumenti per le fondazioni mantengono il livello della pianta. Un plinto disegnato lì sotto un pilastro sale alla base del pilastro; il fondo di una trave di fondazione è quello del suo tipo sotto il livello.

**Un plinto sale al suo pilastro.** Un plinto il cui punto cade nella sezione di un pilastro (al millimetro) ha la sommità alla base del pilastro: il livello del pilastro più il suo **Offset di base**, i gradoni sotto. Sposta il pilastro, cambiane il livello o l'offset di base, e il plinto lo segue. Sotto pilastri posati uno sull'altro segue il più basso; di due ugualmente bassi, quello col centro più vicino. Un plinto senza pilastro sopra resta sul suo livello, col **Fondo** del suo tipo sotto. **Plinti sotto i pilastri** mette ogni plinto nel livello più alto a o sotto il suo fondo (le Fondazioni di un nuovo progetto), altrimenti nel più basso. Un plinto importato da un file IFC resta dove lo mette il file.

In **Proprietà** un plinto mostra **Superiore** e **Inferiore** come quote e il pilastro che segue. Scrivi un **Superiore** per dargli una quota propria: non segue più il pilastro. **Alla base del pilastro** lo ricollega.

![La quota di un plinto in Proprietà: alla base del suo pilastro](shots/structure-pad-level.webp)

![Pilastri, travi e plinti in 3D](shots/structure-3d.webp)

## Tipi e angoli

**Tipi strutturali** imposta le sezioni di pilastri, travi e fondazioni: sezioni in calcestruzzo e profili in acciaio. La sezione di un pilastro è un **Rettangolo**, un **Cerchio** o un **Poligono**, regolare, dato dai **Lati** e dal **Diametro** tra i vertici (un montante ottagonale, come può averlo un IFC importato). Fai clic su un tipo nell’elenco per cambiarne la dimensione o il nome, poi **Salva tipo**: tutti gli elementi di quel tipo si adeguano; **Nuovo tipo** torna alla creazione di un tipo.

Travi e fondazioni si uniscono da sole dove si incontrano, con un angolo a quartabuono. Una trave disegnata da asse di pilastro ad asse di pilastro si ferma alle facce dei pilastri; la sua lunghezza, il suo volume e le sue staffe contano la luce netta tra di essi. **A quartabuono**, **Di testa** e **Scambia** cambiano il modo in cui si incontrano due travi o due fondazioni. **Corrispondenza proprietà** (<kbd>MA</kbd>) copia il tipo e le impostazioni di un elemento su altri, e **Crea simile** (<kbd>CS</kbd>) avvia il disegno di un altro elemento dello stesso tipo.

Ogni altro profilo viene dalle librerie: **Da una libreria di profili…** le cerca tutte (vedi [Librerie di profili](profiles.md)).

## Travi di fondazione con profilo

Un tipo di trave di fondazione è quello standard, un muretto con i suoi strati su una fondazione, oppure qualsiasi profilo estruso lungo l'asse della fondazione, come i profili di fondazione di Revit. In **Tipi strutturali → Strip**, scegli **Forma: Profilo** e un profilo: uno del catalogo (rettangolare, a 2 o 3 gradoni, con la faccia superiore inclinata, eccentrica con il muretto su un bordo, con muretto) con le sue misure, oppure uno del progetto (**Gestisci → Profili**, dove puoi anche digitarne i vertici o leggere un contorno disegnato). Il punto di inserimento del profilo va sull'asse della fondazione, alla quota **Inferiore** del tipo.

Le fondazioni con profilo sono diritte o curve. Due che si incontrano in un angolo si tagliano a quartabuono, e una che termina contro un'altra vi entra ed è tagliata dal suo corpo, così ogni gradone incontra il gradone dell'altra. Piante, sezioni e prospetti tagliano il corpo estruso. Le quantità danno la sua lunghezza, l'area della sezione e il volume, e le sue facce verticali sono quelle che le Opere provvisionali casserano. In IFC una diritta è un IfcFooting STRIP_FOOTING il cui corpo è il profilo estruso (un IfcArbitraryClosedProfileDef), tagliato dai piani degli angoli; una curva va come le sue facce. Scegli **Standard** per tornare al muretto su fondazione.

![Travi di fondazione a gradoni sotto una casa, a quartabuono negli angoli](shots/footing-profile-3d.webp)

![Un tipo di trave di fondazione con profilo](shots/footing-profile-type.webp)

## Una vista strutturale

La disciplina di una vista decide cosa mostra. Una vista **Struttura** mostra solo i nuclei portanti, senza le finiture. Scegli la disciplina nelle proprietà della vista. Vedi [Viste](views.md).

## Carichi

Il gruppo **Carichi** contiene ciò che serve a un'analisi strutturale: casi di carico, carichi di superficie, lineari e puntuali, e le combinazioni. Vedi [Carichi](loads.md).
