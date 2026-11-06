---
title: Importazione ed esportazione
order: 110
summary: Scambia il modello in IFC, porta dentro disegni DXF, collega file ed esporta modelli 3D per il rendering e la RA.
---

Tutto ciò che entra o esce si trova nel menu **File**.

![Il menu File](shots/file-menu.webp)

## IFC e Fragments

**Esporta IFC o Fragments…** scrive il modello in IFC. Ogni muro, solaio, porta e tetto è un vero elemento IFC con i suoi strati, il suo tipo e le sue proprietà, così il file si apre come un modello in altri strumenti BIM. Porte e finestre dicono come si aprono: la mano di ogni anta e di ogni battente, il verso in cui ruota, e i pannelli, nel modo in cui li definisce IFC, così gli altri programmi disegnano le stesse aperture.

L’esportazione chiede lo schema (**IFC4**, letto ovunque, oppure **IFC4X3**), il dettaglio (le parti come elementi a sé, oppure un corpo per elemento), la fase e come vengono scritti i corpi. **Geometria** è un **B-rep sfaccettato** (esatto, ciò che gli altri programmi si aspettano più spesso) oppure **insiemi di facce**: le stesse facce in un file molto più piccolo. **Semplifica** scrive meno triangoli dove un corpo è una mesh fine, come le canalizzazioni e le tubazioni del file di un altro programma, mai più lontano da esso della **tolleranza** che digiti (2 mm all’inizio). I corpi che Lapin costruisce da sé (muri, solai, porte) conservano le loro facce esatte, e la semplificazione cambia solo la forma di un corpo, mai le sue proprietà.

![Esporta IFC](shots/export-ifc.webp)

**Importa IFC…** legge l’IFC di un altro programma. Prima che entri qualsiasi cosa, Lapin legge il file e mostra che cosa contiene: i suoi elementi per disciplina (architettura, struttura, impianti, arredi e attrezzature, sito), ogni disciplina per categoria con il numero di elementi, e i suoi piani con quanti elementi stanno su ciascuno. Togli la spunta a ciò che non ti serve, per esempio impianti e arredi quando lavori sulla struttura, oppure tieni solo i piani su cui lavori. **Come** porta dentro gli elementi riconosciuti come elementi Lapin, tutti come riferimenti, oppure collegati. Mentre il file viene letto e importato, Lapin mostra che cosa sta facendo e a che punto è; un file grande richiede un po’ di tempo.

![Importa IFC](shots/import-ifc.webp)

Alcuni file portano del 2D oltre al loro modello: simboli in pianta, impronte, retini e testi disegnati per un elemento, oppure elementi disegnati solo in 2D (linee, testi, simboli senza corpo). Quando un file ne ha, la finestra di dialogo li conta in **Disegnato in 2D**. Togli la spunta a **il 2D proprio degli elementi** e una pianta disegna quegli elementi con il loro taglio, come fa con quelli di Lapin; togli la spunta a **elementi disegnati solo in 2D** e restano fuori. Entrambi entrano, a meno che tu non tolga la spunta.

![Disegnato in 2D](shots/import-ifc-2d.webp)

**Pulizia e ottimizzazione** raccoglie le regole dell’importazione, le stesse per qualsiasi file, conservate come tue per l’importazione successiva. **Conserva una sola volta un corpo ripetuto** (attiva, a meno che tu non tolga la spunta) conserva una sola volta un corpo che il file ripete sotto altri nomi (un bullone, una flangia, un supporto), con i suoi elementi che lo condividono, senza perdere nulla. Le altre escludono, quando spuntate, ciò che un altro programma si è portato dietro: i **corpi piani** (testi, linee e retini trasformati in triangoli), gli **elementi più lontani dal resto** della distanza che digiti (i testi di un disegno, un cartiglio), i **duplicati** nella stessa posizione, e gli **attacchi e aperture scritti come oggetti**. **Semplifica le mesh** scrive meno triangoli entro la tolleranza che digiti; **leggi i numeri scritti come testo** trasforma `DoubleLength:0,01` in 0.01 m e `1.15395 kg` in una massa (un numero intero da solo, un codice, resta testo). In **Cosa importare**, **Insiemi di proprietà** elenca gli insiemi del file con quanti elementi portano ciascuno; quelli **tecnici** (la trasformazione di Navisworks, gli id interni, le statistiche della geometria) restano fuori a meno che tu non li spunti. Il messaggio dell’importazione dice che cosa ha fatto ogni regola.

![Pulizia e ottimizzazione](shots/import-ifc-rules.webp)

Ciò che il file posiziona entra come un unico gruppo con il nome del file, che puoi aprire e riordinare, oppure entra sciolto se togli la spunta a **Come un unico gruppo** (vedi [Gruppi e pulizia](groups.md)).

**Tutto modificabile**, una scelta di **Come**, va oltre il riconoscimento: ciò che resta un riferimento diventa un solido Studio fatto dei suoi stessi triangoli, lo stesso elemento con la sua classe, le sue proprietà, il suo piano e il suo gruppo, che accetta subito spingi/tira, raccordi e booleane (vedi [Studio](studio.md)). Un corpo di oltre 5.000 triangoli, come una pompa o un raccordo con una mesh fine, resta un riferimento, e il messaggio dell’importazione dice quanti e perché. **Importa Fragments…** ha lo stesso **Come**: un file Fragments contiene solo triangoli, eppure i suoi muri (con le loro porte e finestre), solette e pilastri vengono riconosciuti da essi come elementi Lapin, mentre il resto è conservato come riferimenti oppure, con **Tutto modificabile**, trasformato in solidi Studio. Gli elementi di un file IFC che il riconoscimento non è riuscito a leggere dal file (un muro con le aperture ricavate nel suo corpo) vengono provati anche a partire dai loro triangoli. **Importa mesh** chiede **Come: modificabili**. I riferimenti importati in precedenza si convertono con Modifica → **Converti in nativo** (NV): riconosciuti dove possibile, il resto trasformato in solidi Studio, e un gruppo selezionato vale per i suoi membri. Un riferimento importato in precedenza si rende modificabile uno alla volta con Studio → **Rendi solido Studio**, che lo mantiene nel suo gruppo e sul suo piano.

Lapin riconosce muri, solai, pilastri, travi, fondazioni, porte e finestre e li trasforma in elementi Lapin che puoi modificare. Ciò che non riesce a riconoscere entra come riferimento, disegnato ma non modificabile. Porte e finestre entrano aprendosi come dice il file: la loro mano, il lato verso cui si aprono e, per le finestre, come si apre ogni pannello.

Le finiture che un altro programma ha esportato come muri a sé (un intonaco esterno di 2,5 cm su una facciata, un intonaco interno) diventano uno strato del muro che rivestono, quando aderiscono alla sua faccia, alte quanto esso e lungo esso: il muro riceve un tipo con quello strato (*Masonry 350 + Lime render*), i suoi strati restano dov’erano, e il messaggio dell’importazione dice quanti. Una finitura staccata dal suo muro, o che ne riveste solo una parte, resta un muro sottile. Un pilastro che il file dà come un corpo tagliato in sommità da una pendenza (sotto una trave o un tetto inclinati) entra come pilastro della sua sezione, fino al suo punto più alto.

**Associa tipi importati** (Gestisci) elenca i tipi creati da un’importazione (tipi di muri, solai, porte, finestre, pilastri, travi e fondazioni), ciascuno con il suo file e con quanti elementi ha. Scegli in **Diventa** uno dei tipi del progetto della stessa categoria e premi **Associa**: i suoi elementi prendono il tipo del progetto, un muro mantiene il suo nucleo dov’era, e il tipo importato sparisce. Con **Ricorda** spuntato, un’importazione successiva di un tipo di quella categoria e di quel nome prende subito quello del progetto, e il suo messaggio lo dice. Le scelte memorizzate sono elencate nella finestra di dialogo, ciascuna con **Dimentica**.

![Associa tipi importati](shots/map-types.webp)

Gli spazi del file (da Revit, ArchiCAD e altri) entrano come locali di Lapin, ciascuno sul proprio livello, con il loro contorno, numero, nome, altezza, funzione, reparto e pavimentazione: vedi [Locali](rooms.md#locali-da-un-file-ifc). Tutto ciò che un elemento importato aveva nel file (i suoi insiemi di proprietà e le sue quantità) è conservato e mostrato in **Proprietà IFC** nelle sue proprietà. Un riferimento esce da **Esporta IFC…** così come è entrato: con lo stesso GlobalId, la stessa classe e gli stessi attributi (descrizione, tag, tipo predefinito), i suoi insiemi di proprietà e le sue quantità con ogni valore tipizzato come nel file, e il suo tipo, il suo materiale, i suoi attacchi, le sue connessioni e il suo sistema, ciascuno con il suo GlobalId. Porta dentro solo gli impianti di un file ed esportali, e il nuovo file li contiene per intero.

**Importa Fragments…** apre lo stesso pannello per un file `.frag` di That Open: i suoi elementi per disciplina e categoria, i suoi piani e **Come un unico gruppo**. Un file esportato proprio da questo progetto non porta nulla di nuovo quando viene reimportato in esso: i suoi elementi sono già qui.

![Importa Fragments](shots/import-frag.webp)

**Importa Fragments…** porta dentro un file `.frag`: ogni elemento con geometria diventa un riferimento disegnato dal suo corpo, nella classe che il file indica, sul piano su cui il file lo mette (abbinato per nome ai tuoi, creato se manca), con gli attributi del file in **Proprietà IFC**. Gli elementi con lo stesso corpo (la stessa forma, posizionata in molti punti) lo condividono. Importare di nuovo lo stesso file aggiorna ciò che aveva portato prima. Che cosa viene riconosciuto dai suoi triangoli dipende da **Come**, come sopra. I suoi insiemi di proprietà e di quantità, il suo tipo e i suoi materiali arrivano con gli attributi; il vetro resta trasparente; un file fatto dal convertitore di That Open finisce dove lo metterebbe il suo IFC, e le sue facce con fori (un muro attorno alla sua finestra) li conservano.

**Esporta IFC o Fragments…** scrive il modello in IFC o in Fragments, scelti come in un’importazione: gli elementi per disciplina e categoria, i piani, la fase, e solo ciò che mostra la vista 3D. L’IFC esce in IFC4 o IFC4X3, con le parti di un elemento a strati sotto di esso oppure un corpo ciascuna; Fragments dal punto base del progetto, dall’origine interna o dal punto di rilievo. Un file Fragments porta ciò che mostra un visualizzatore IFC: il tipo di ogni elemento, le sue quantità, i tuoi parametri, i suoi materiali e le coordinate cartografiche, come li scrive il convertitore di That Open.

![Esporta IFC o Fragments](shots/export-exchange.webp)

## Ottimizzare un file IFC

**Ottimizza IFC…** (nel menu File o nella schermata iniziale: non serve aprire alcun progetto) crea una copia più leggera di uno o più file IFC senza cambiare ciò che dicono. La copia mantiene ogni GlobalId, nome, set di proprietà, quantità, tipo, materiale e relazione esattamente come il file li ha scritti, e anche la sua intestazione; non si aggiunge nulla di Lapin. Cambia solo la geometria: le mesh (insiemi di facce, B-rep sfaccettati con facce piane) vengono ripulite (vertici duplicati saldati, triangoli degeneri e ripetuti e coordinate inutilizzate rimossi) e semplificate, mai più lontano dall’originale della **Precisione** (Veloce 5 mm, Bilanciato 1 mm, Preciso 0,1 mm o la tua), misurata nei due sensi; i corpi identici vengono scritti una sola volta e condivisi dai loro prodotti; e ciò a cui nulla fa riferimento (punti, stili inutilizzati, rappresentazioni vuote) scompare. Estrusioni, booleane e gli altri corpi esatti restano come sono. Arrotondare le coordinate, scrivere i B-rep come insiemi di facce (solo IFC4 e IFC4X3) e un `.ifczip` compresso ci sono quando li vuoi.

![Ottimizza IFC](shots/optimize-ifc.webp)

La copia viene scritta accanto all’originale, o in una cartella scelta da te, con il nome dell’originale e una **Terminazione** (all’inizio `.optimized`): `casa.ifc` diventa `casa.optimized.ifc`. L’originale non viene mai scritto, e se la copia esiste già Lapin chiede prima di sostituirla. Per ogni file il rapporto dà dimensione, entità e triangoli prima e dopo, i corpi condivisi, gli orfani rimossi, lo scostamento massimo misurato e la durata; **Apri cartella** mostra la copia e **Apri in Lapin** la apre in un’altra finestra. Nel browser la copia viene scaricata. Un file già portato nel progetto conserva la strada del ritorno: **Ottimizza il file sorgente…** sul gruppo di un IFC importato (nelle sue proprietà) o su un collegamento (Gestisci → Collegamenti) fa lo stesso con il file da cui è venuto, finché è ancora sul disco.

![Il rapporto](shots/optimize-ifc-report.webp)

## DXF e DWG

**Importa DXF…** porta un disegno CAD nella vista in primo piano, con i suoi layer, retini, testi e quote. Entra come disegno 2D su cui puoi fare snap e che puoi modificare. Per usare un DWG, salvalo prima come DXF dal tuo programma CAD (AutoCAD, BricsCAD o DraftSight: **Save As → DXF**). L’importazione di PDF non è ancora disponibile. In una pianta, **Posizionato: dalle coordinate della mappa** posa un disegno in coordinate reali (una mappa catastale in Stereo 70) dove lo mette la mappa (vedi [Il sito e i suoi dati](site.md)).

## Collegamenti

**Collega IFC…** mantiene un altro file collegato invece di importarlo. È in sola lettura e viene ricaricato quando cambia. Funziona anche per un DXF. **Gestisci → Collegamenti** mostra, ricarica, converte o rimuove i collegamenti.

## Modelli 3D

**Esporta 3D (glTF, USD, DAE)…** scrive il modello come superfici per il rendering, la visualizzazione e la realtà aumentata (RA):

| Formato | Per |
|---|---|
| GLB, glTF | Blender, three.js, visualizzatori web. Le immagini dei tuoi materiali sono incluse. |
| USDZ | AR Quick Look di Apple, Reality Composer, Omniverse. Immagini incluse. |
| USDA | OpenUSD come testo, un solo file. |
| DAE | COLLADA, per SketchUp e strumenti più vecchi. |
| OBJ + MTL | Uno .zip per Twinmotion, Lumion, 3ds Max e qualsiasi programma di rendering. Con le immagini. |

**Importa mesh (OBJ, STL, glTF)…** porta dentro una mesh da un altro programma 3D (OBJ, STL, glTF o GLB) come riferimenti sul livello attivo. glTF dice la sua unità e quale verso è l’alto; per OBJ e STL Lapin lo chiede. **Rendi solido Studio** di Studio rende modificabile un riferimento (vedi [Studio](studio.md)).

![Esporta 3D](shots/export-3d.webp)

Scegli che cosa esportare, quale fase e l’origine. **Cosa** è **tutto**, oppure il modello **come lo mostra la vista**: la vista 3D in primo piano, senza i suoi elementi e le sue categorie nascosti e senza i piani che non mostra, con gli strati che mostra (senza finiture, o solo i nuclei). Quando quella vista è tagliata (il taglio sopra i piani mostrati, una sezione mostrata in 3D, il box di sezione), **Taglio** taglia anche l’esportazione lì e chiude ogni taglio nei suoi materiali, così il file appare come lo schermo. Una vista 3D che esclude qualcosa viene esportata come la mostra, a meno che tu non scelga diversamente. Ogni elemento conserva la sua classe IFC, il suo tipo e il suo GlobalId. Per scambiare il modello in sé anziché le sue superfici, usa **Esporta IFC**.
