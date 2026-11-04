---
title: Abachi e grafici
order: 95
summary: Elenca porte, finestre, architravi, locali e tutto ciò che un modulo conta; aggiungi righe e colonne tue, scrivi sopra i valori del modello e trai un grafico dalla tabella.
---

## Ottenere le quantità

Tutto ciò che Lapin modella è misurato dalla sua geometria: la lunghezza, l’altezza, l’area laterale lorda e netta e il volume di un muro, l’area e il volume di ogni strato, le aree e il volume di un solaio, l’area e il perimetro di un locale, il numero delle porte. Ci sono tre vie:

- **Un elemento.** Selezionalo: la sezione **Quantità** del pannello delle proprietà mostra ciò che misura, poi ogni suo strato o parte.
- **Tutto il modello.** **Quantità → Computo delle quantità** crea (o apre) il computo: tutti gli elementi che il modello misura, sommati riga per riga, con il numero, la lunghezza, l’area lorda e netta, il volume lordo e netto e il peso. La sua barra sceglie cosa sommare insieme: **Per categoria e tipo** (predefinito: i muri di ogni tipo di muro, i solai di ogni tipo…), **Per materiale** (ogni strato sotto il suo materiale: il laterizio, l’intonaco e l’isolante dei muri a parte, il calcestruzzo degli architravi), per piano, o combinati. **Tutti gli elementi** sceglie quali categorie elenca (solo muri e solai, per esempio). Il computo segue il modello come ogni abaco e accetta righe e colonne proprie (un accantonamento, un prezzo unitario).
- **Qualsiasi altro elenco.** **Quantità → Abachi…** crea qualsiasi abaco offerto dall’edizione: locali, finiture, serramenti, stratigrafie dei tipi, legno, armature, casseforme, acciaio… Lo stesso elenco è sotto **+** accanto ad **Abachi** nel browser del progetto.

![Il computo raggruppato per categoria e materiale](shots/quantity-takeoff.webp)

**Quantità → Esporta XLSX** salva subito il computo come cartella di lavoro (in un browser viene scaricato).

## Le quantità su una tavola

Un abaco va su una tavola come una vista ed è disegnato lì con tutte le sue righe e i suoi valori, i totali e i grafici:

- **Metti su tavola…** nella barra dell’abaco: **Nuova tavola** crea una tavola A3 con la tabella e la apre, oppure scegli una tavola che hai.
- Oppure trascina l’abaco dal browser del progetto su una tavola aperta: finisce dove lo lasci. Piante, sezioni e prospetti si trascinano allo stesso modo.
- Oppure **Posiziona vista…** nella barra della tavola.

Sulla tavola, trascina la tabella per spostarla; una tabella più lunga della cornice continua su altre pagine (la tavola lo dice, e **mettila su una nuova tavola** la continua). La tabella segue il modello: aggiungi un muro e i numeri della tavola cambiano. Si stampa con **Stampa** e va nel PDF e nel DXF della tavola esattamente com’è disegnata.

![Il computo messo su una nuova tavola](shots/takeoff-sheet.webp)

## Creare un abaco

Un abaco è una tabella che il modello riempie: porte e finestre, architravi, locali, armature, legname, casseforme e altro. Fai clic su **+** accanto ad **Abachi** nel browser di progetto e scegli cosa elenca, oppure usa lo strumento di un modulo (Legno → Abaco del legname, Opere provvisionali → Abaco delle casseforme…). Fai doppio clic su di esso nel browser per aprirlo in una scheda. Un abaco segue il modello: sposta un muro, aggiungi una finestra, e la tabella cambia con loro.

Gli abachi dei **serramenti**, delle **porte**, delle **finestre** e degli **architravi** mostrano una scheda per ogni tipo, con il suo disegno (la vista interna per impostazione predefinita) e i suoi valori riga per riga. Gli altri abachi sono una tabella semplice, una riga per elemento e una riga dei totali alla fine.

Una porta o una finestra invertita sull’altro verso del suo tipo (**Verso**, o una sola freccia di inversione; invertita in entrambi i sensi è solo ruotata) è l’immagine speculare del suo tipo e ha una scheda propria: la sua sigla finisce con **-M** (D-M), il suo nome con «(mirrored)», e la sua riga **Opening**, il disegno e i conteggi sono quelli dell’altro verso. Invertila di nuovo e torna sulla scheda del suo tipo. Sinistra e destra sono quelle di IFC: le cerniere viste da chi la spinge per aprirla. I disegni mostrano ogni porta com’è nel modello, vista dall’interno, il lato verso cui si apre (quindi le cerniere di una porta *swing left* sono a destra). Una nota sotto le schede lo dice.

![Un abaco dei serramenti con le righe della resistenza al fuoco e del produttore](shots/joinery-schedule.webp)

## Resistenza al fuoco, produttori e altri parametri

I parametri di una porta o di una finestra entrano da soli nell’abaco dei serramenti:

- **Resistenza al fuoco.** La riga **Resistenza al fuoco** legge un parametro chiamato Fire rating, Fire Rated, FireRating o Fire resistance. Mettilo sul tipo, così che ogni porta del tipo lo abbia. In **Architettura → Porte e finestre**, **Parametri · negli abachi** → **+ Resistenza al fuoco** aggiunge il FireRating del tipo a Pset_DoorCommon (Pset_WindowCommon per una finestra), dove IFC lo tiene. Si vede anche un parametro che aggiungi tu a una sola porta, nelle sue proprietà.
- **Qualsiasi altro parametro.** **+ Produttore**, **+ Codice prodotto**, **+ Sito web**, o un parametro tuo, diventa una riga a sé in ogni abaco dei serramenti. Il valore proprio di una porta si vede accanto a quello del suo tipo, quando le porte di un tipo differiscono.
- **Scritto nell’abaco.** Un valore scritto nella riga di un parametro (in corsivo) va nel tipo, e ogni porta del tipo lo segue. Svuotalo per togliere il parametro dal tipo.

![I parametri del tipo nel configuratore di porte e finestre](shots/joinery-params.webp)

**Tipi non usati.** Un tipo che nessuna porta o finestra del modello usa resta fuori dall’abaco. **Tipi non usati** nella barra degli strumenti dell’abaco li mostra di nuovo, con quanti sono.

## Righe e colonne tue

Ogni abaco si può completare a mano, come un foglio di calcolo:

- **Riga** aggiunge una riga tua. In un abaco dei serramenti ha un’etichetta, e ne scrivi il valore per ogni tipo (Fornitore, Data di consegna…). In qualsiasi altro abaco è una riga intera, scritta cella per cella (una terrazza non modellata, un accantonamento); sta sopra la riga dei totali.
- **Colonna** aggiunge una colonna tua. In un abaco dei serramenti è una scheda senza alcun tipo dietro, con ogni valore scritto (una porta da garage ordinata a parte). In qualsiasi altro abaco è una colonna dopo quelle del modello (Finitura del pavimento, Note…).
- **Qualsiasi cella** della tabella sotto il disegno si può sovrascrivere. Un valore scritto sopra quello del modello è segnato; **↺** riporta il valore del modello. Ciò che scrivi resta con la sua riga: le note di un locale lo seguono quando l’elenco viene ordinato o il locale rinominato. In un abaco **Rooms**, un numero, nome, funzione, reparto, finitura del pavimento o altezza scritti nella riga di un locale vanno nel locale stesso, come scritti nelle sue proprietà; l’etichetta in pianta e ogni altro abaco seguono.
- **×** accanto a una riga o a una colonna tua la toglie, con ciò che vi era scritto.
- Le frecce nell’intestazione di una tabella ne **ordinano** le righe per quella colonna: una volta in ordine crescente, di nuovo decrescente, una terza volta nell’ordine del modello. La riga dei totali resta l’ultima.

Tutto si stampa: su una tavola, nel PDF e nel DXF, e nelle esportazioni CSV e XLSX.

## Grafici

**Grafici** nella barra degli strumenti dell’abaco apre i grafici accanto alla tabella. **Nuovo grafico** ne crea uno dalla tabella:

- **Colonne**, **Barre**, **Torta** o **Linea**.
- **Categorie da**: la colonna che dà il nome a ogni barra o fetta (Destinazione d’uso, Nome, Contrassegno, Piano…).
- **Come**: **Somma**, **Media**, **Minimo** o **Massimo** di una colonna (Area, Totale, Volume…), oppure **Numero di righe**.
- **Al massimo**: tiene solo le categorie più grandi e somma il resto come Altro.
- **Titolo**: un titolo suo, o quello che compone da sé (Somma di Area per Destinazione d’uso).

Il motore disegna ogni grafico dopo le tabelle, quindi si vede nell’abaco, su una tavola che posiziona l’abaco e nel PDF, esattamente uguale. Segue il modello e ciò che hai scritto: cambia l’area di un locale o aggiungi una riga tua, e il grafico cambia con loro. **✎** modifica un grafico, **×** lo toglie.

![Un abaco dei locali con una riga sua e un grafico a torta delle aree per destinazione d’uso](shots/schedule-chart.webp)

## Stratigrafie dei tipi

L'abaco **Stratigrafie dei tipi** (**+** → **Stratigrafie dei tipi**) elenca ogni tipo di muro e di solaio: il suo contrassegno, lo spessore totale, quanti elementi lo usano, poi i suoi strati in ordine con materiali e spessori.

**Disegni delle stratigrafie**, nella sua barra, disegna ogni tipo dopo la tabella, sezionato attraverso i suoi strati alla scala dell'abaco (1:50 se non ne scegli un'altra): un muro come lo taglia una pianta, un solaio come lo taglia una sezione, lo strato 1 in alto. Ogni strato è riempito e campito come il suo materiale nelle sezioni, le facce del nucleo sono disegnate più spesse, lo spessore totale è quotato e ogni strato è nominato accanto come lo nomina la tabella. I disegni si stampano con la tabella, su una tavola e nel PDF. Un nuovo abaco delle stratigrafie li disegna; togli la spunta a **Disegni delle stratigrafie** per tenere solo la tabella.

I disegni **Su ogni scheda** (vista interna, vista esterna, pianta, sezione), nelle **Impostazioni** di un abaco, appartengono alle schede degli abachi dei serramenti; una tabella semplice non ha schede, quindi lì non sono proposti.

![L'abaco delle stratigrafie dei tipi con la stratigrafia di ogni tipo](shots/type-assemblies.webp)

## Esportare

**CSV** e **XLSX** nella barra esportano l'abaco come foglio di calcolo (un XLSX ha un foglio per ogni tabella). Nell'app desktop Lapin chiede dove salvare il file e propone il nome dell'abaco; in un browser il file viene scaricato. I tipi di muro e di solaio esportati si salvano allo stesso modo.
