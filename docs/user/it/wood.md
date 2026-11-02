---
title: Legno
order: 54
summary: Muri a telaio in legno dal configuratore: montanti su un modulo, correnti, traversi, e ogni porta e finestra intelaiata secondo le sue regole.
---

## Muri a telaio in legno

Un tipo di muro può essere costruito come telaio in legno: **Legno → Telaio in legno** apre il configuratore, con i tipi di muro del progetto a sinistra. Spunta **Telaio in legno** e il nucleo del tipo è fatto di elementi invece che di un solo strato: i correnti, i montanti sul loro modulo, le file di traversi e, attorno a ogni porta e finestra, i montanti interi, i montanti di appoggio, l’architrave e il davanzale. Gli altri strati (rivestimento, pannelli di controventatura, lastre) restano come sono, e l’isolamento riempie le cavità tra gli elementi.

![Il configuratore del telaio in legno](shots/wood-frame.webp)

- **Predefiniti**: **Nordic** (45 × 145 a 600 mm, C24) o **Romanian** (50 × 150 a 625 mm, C24, una fila di traversi a 1,25 m). Ogni campo può poi essere modificato.
- **Montanti, passo, classe, correnti, montanti di estremità, file di traversi**. **Modulo da** stabilisce dove inizia il modulo dei montanti: l’inizio di ogni muro o l’origine del progetto, così che i muri di una facciata lo condividano.
- **Portante**: un tramezzo non porta nulla e riceve sopra le sue aperture un elemento piatto invece di un architrave.

## Porte e finestre

Le porte e le finestre restano quelle dell’architetto: spostane una o cambiane il tipo, e il suo telaio la segue. Il telaio si misura dal **foro grezzo**: la porta o la finestra più i giochi di posa (ai lati, in alto, sotto una finestra; una porta scende fino al pavimento).

Ogni apertura prende la regola della sua **fascia di larghezza** (la tabella sotto **Aperture**: fino a 900 mm, fino a 1800 mm, oltre, per impostazione predefinita): i suoi montanti interi e di appoggio per lato, il suo architrave (nessuno, massiccio, LVL o lamellare, la sua sezione e il numero di elementi) e gli elementi del davanzale. Nessun montante sta mai in un foro grezzo; quelli corti sopra l’architrave e sotto il davanzale (montanti corti) mantengono il modulo, così che i giunti delle lastre cadano comunque sui montanti. Due aperture più vicine del **maschio minimo** condividono un solo architrave e i loro montanti interi esterni. Un’apertura troppo vicina all’estremità del muro viene segnalata nelle proprietà del muro, mai spostata.

Seleziona una porta o una finestra: la sua sezione **Legno** mostra il foro grezzo, la regola che prende e ciò che riceve. Spunta **Regola propria** per dare a quell’apertura sola altri montanti interi o di appoggio, un altro architrave o un altro davanzale.

## Angoli, lastre e prospetto del telaio

- **Angoli**: dove un altro muro si ferma contro l’estremità di questo (un angolo di testa), il muro che prosegue riceve **tre montanti** (uno oltre la faccia interna dell’altro muro), un angolo **California** (un montante posato di piatto sulla faccia interna, per il rivestimento) oppure **solo i montanti di estremità**. Un angolo a quartabuono mantiene i montanti di estremità di entrambi i muri.
- **Incroci a T**: dove un muro si ferma contro questo lungo il suo sviluppo, questo riceve **un montante per lato** oppure **montanti e traversi a scala** tra di essi.
- **Lastre**: spunta uno strato della stratigrafia (OSB, fibra di legno, cartongesso) e indica il formato della lastra: lo strato viene posato in lastre verticali, con i giunti sul modulo dei montanti, tagliate in corrispondenza dei fori grezzi e lungo la sommità. **Legno → Abaco dei pannelli** conta le lastre di ogni muro, la loro area netta e gli sfridi.
- **Prospetto del telaio**: nella sezione **Legno** di un muro a telaio, il pulsante crea una sezione lungo il muro attraverso il centro del suo nucleo, in scala 1:20, con tutti gli elementi tagliati, una catena di quote attraverso i fori grezzi, il modulo dei montanti sotto di essa e la dimensione di ogni foro grezzo. Posizionala su una tavola come qualsiasi vista.

## Pannelli prefabbricati

Spunta **Pannelli prefabbricati** per realizzare il telaio in officina: indica il **pannello più lungo** (quanto lo consentono il banco dell’officina e il camion) e il **più corto**. Ogni muro viene diviso in pannelli in corrispondenza del giunto più lontano entro quella lunghezza, sul modulo dei montanti dove possibile, mai attraverso i montanti interi, i montanti di appoggio, l’architrave o il davanzale di un’apertura, né attraverso il rinforzo di un incrocio a T. Ogni pannello è intelaiato a sé, con i propri montanti di estremità e correnti ai giunti. Un pannello che non può restare entro la lunghezza (un’apertura troppo larga) resta intero e il muro lo segnala.

![Un muro prefabbricato nel configuratore](shots/wood-panels.webp)

I pannelli sono contrassegnati dal nome del muro e dal loro numero lungo di esso (W3-P1, W3-P2…). La sezione **Legno** del muro ne elenca le lunghezze; il prospetto del telaio disegna i giunti e i contrassegni sul muro; **Legno → Pannelli di muro** elenca ogni pannello con il suo muro, lunghezza, altezza, aperture, elementi, volume di legno e massa, così che possano essere ordinati, prodotti e sollevati uno dopo l’altro. Anche le lastre su un pannello portano il suo contrassegno. In IFC ogni pannello è un IfcElementAssembly (Timber frame panel) che contiene i suoi elementi e le sue lastre.

## CLT

Nello stesso configuratore un tipo di muro o di solaio può essere costruito **in pannelli CLT**: indica gli strati (lo spessore di ciascuno, 3, 5, 7 o 9 strati), la classe, il pannello più lungo e il più alto (o il più largo) che la pressa produce, il giunto tra i pannelli (di testa, a mezzo legno, a linguetta), la distanza che un’apertura mantiene dal bordo di un pannello e il suo gioco di posa. Un muro viene diviso in pannelli lungo il suo sviluppo, con i giunti spostati lontano dalle aperture; ogni pannello è il corpo stesso del nucleo, con i suoi fori grezzi ritagliati. Un solaio viene diviso in pannelli trasversalmente alla sua luce (nel senso corto, oppure lungo x o y). **Legno → Pannelli CLT** elenca ogni pannello con il suo contrassegno, dimensione, strati, classe, giunto, area e volume; in IFC ognuno è un IfcBuildingElementPart chiamato CLT panel.

- **Giunti**: le due metà di un giunto **a mezzo legno** si sovrappongono per la lunghezza della sovrapposizione; un giunto **a linguetta** ha una battuta su una faccia e al suo interno una striscia di compensato, un elemento a sé. Entrambi sono modellati nei pannelli, così che i loro volumi e l’abaco corrispondano a ciò che l’officina taglia.
- **Angoli delle aperture**: una fresa non può tagliare un angolo interno vivo. **Il raggio della fresa** lo lascia in ogni angolo; un **sovrataglio a osso di cane** taglia un cerchio in ogni angolo, così che un telaio squadrato ci entri comunque. Il configuratore disegna le frese.
- **Prospetto dei pannelli**: la sezione **Legno** di un muro in CLT crea una sezione lungo di esso in scala 1:20, con i giunti e i contrassegni dei pannelli e le dimensioni dei fori grezzi.
- **Tetti in CLT**: seleziona un tetto a falde e spunta **Pannelli CLT** nella sua sezione **Legno**: le sue falde vengono posate come lastre in CLT al posto dei puntoni o delle capriate (i dormienti e la copertura restano), con le finestre da tetto e gli abbaini ritagliati, in strisce larghe quanto il pannello lungo la gronda, giuntate di testa dove una falda è più lunga di un pannello.

## Solai su travetti

Un tipo di solaio può essere costruito **su travetti**: sceglilo nel configuratore e scegli **su travetti**. Indica la sezione, il passo e la classe dei travetti, in che direzione portano (nel senso corto, oppure lungo x o y), se una **trave di bordo** chiude i bordi, le **file di rompitratta** e, attorno a ogni apertura nel solaio (un vano scala, un cavedio), le **travi di raccordo** su ogni lato e gli **elementi dell’architrave** alle sue estremità. I travetti interrotti da un’apertura si fermano al suo architrave, e si aggiunge un travetto dove le travi di raccordo lasciano uno spazio più largo del passo. **Riempi le cavità** mette il materiale del nucleo tra i travetti. Il configuratore mostra un solaio di esempio con un vano scala.

![Un tipo di solaio su travetti](shots/wood-joists.webp)

I travetti sostituiscono il nucleo del solaio; gli strati sopra e sotto restano. I loro elementi compaiono nell’abaco del legname, e in IFC ognuno è un IfcBeam (JOIST, una trave di bordo o un architrave per nome) sotto il solaio.

## L’abaco del legname

**Legno → Abaco del legname** crea una distinta di taglio di ogni pezzo di legno del progetto, gli elementi dei muri e l’orditura dei tetti: elemento, sezione, classe, lunghezza, pezzi, lunghezza totale e volume, con i totali per sezione. In IFC ogni elemento è un IfcMember (STUD, PLATE, un architrave o un davanzale per nome) sotto il suo muro, e l’isolamento un IfcCovering INSULATION.
