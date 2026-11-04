---
title: Abachi e grafici
order: 95
summary: Elenca porte, finestre, architravi, locali e tutto ciò che un modulo conta; aggiungi righe e colonne tue, scrivi sopra i valori del modello e trai un grafico dalla tabella.
---

## Creare un abaco

Un abaco è una tabella che il modello riempie: porte e finestre, architravi, locali, armature, legname, casseforme e altro. Fai clic su **+** accanto ad **Abachi** nel browser di progetto e scegli cosa elenca, oppure usa lo strumento di un modulo (Legno → Abaco del legname, Opere provvisionali → Abaco delle casseforme…). Fai doppio clic su di esso nel browser per aprirlo in una scheda. Un abaco segue il modello: sposta un muro, aggiungi una finestra, e la tabella cambia con loro.

Gli abachi dei **serramenti**, delle **porte**, delle **finestre** e degli **architravi** mostrano una scheda per ogni tipo, con il suo disegno (la vista interna per impostazione predefinita) e i suoi valori riga per riga. Gli altri abachi sono una tabella semplice, una riga per elemento e una riga dei totali alla fine.

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
