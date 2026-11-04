---
title: Locali e colori dei locali
order: 45
summary: Posiziona i locali con un clic all’interno dei muri, dai loro un nome e un numero, e colorali o retinali in pianta per destinazione d’uso o reparto, con una legenda.
---

## Posizionare un locale

Scegli **Architettura → Locale** (<kbd>RM</kbd>) e fai clic all’interno di uno spazio chiuso da muri. Lapin trova il contorno del locale tra le facce dei muri, delle facciate continue e dei pilastri intorno al punto. Le porte non aprono il locale: si chiude attraverso di esse. Un pilastro all’interno del locale viene escluso dalla sua superficie.

Il locale segue i suoi muri. Sposta un muro e il locale si allarga o si restringe con esso. Se un muro viene eliminato e nulla chiude più il locale, questo mantiene il contorno che aveva, e la sua etichetta diventa rossa. **Architettura → Aggiorna locali** memorizza i contorni che i locali trovano ora.

Per uno spazio non chiuso da muri su tutti i lati, come una terrazza o un’estremità di un open space, usa **Locale da contorno** e fai clic sui suoi angoli. Quel locale mantiene il contorno che disegni.

![Una pianta con due locali colorati per destinazione d’uso e una legenda dei colori](shots/rooms-plan.webp)

## Le proprietà di un locale

Seleziona un locale facendo clic sulla sua etichetta o in qualsiasi punto al suo interno. Nel pannello Proprietà:

- **Nome** e **Numero.** I nuovi locali sono numerati per livello: 101, 102… al piano terra. Il nome di un locale è quello della sua funzione finché non gliene dai uno tuo; svuotato, torna alla funzione.
- **Destinazione d’uso**, **Reparto** e **Finitura del pavimento.** Digita qualsiasi cosa, oppure scegli un valore che altri locali hanno già.
- **Altezza.** Vuota significa l’altezza del livello.
- **Colore proprio** e **Retino proprio** sostituiscono lo schema di colori per questo solo locale.

Le quantità qui sotto mostrano la superficie netta del locale (senza i pilastri), il suo perimetro e il suo volume. Trascina la maniglia sull’etichetta per spostarla.

![Le proprietà di un locale](shots/room-properties.webp)

## Etichetta del locale

**Architettura → Etichetta del locale** (o **Etichetta del locale…** nelle proprietà di un locale) sceglie cosa mostra l’etichetta di ogni locale nelle piante, riga per riga: nome, numero, funzione, reparto, superficie, superficie lorda, perimetro, altezza, volume o finitura del pavimento, oppure un testo tuo. La prima riga è in grassetto. **Standard di disegno → Etichette** mostra la stessa etichetta come testo: `{name}|{number}|{area}`.

![La finestra di dialogo Etichetta del locale](shots/room-tag.webp)

## Colori dei locali

**Vista → Colori dei locali** colora i locali di una pianta. Scegli uno schema, scegli in base a cosa colora (**Destinazione d’uso**, **Nome**, **Reparto**, **Numero** o **Finitura del pavimento**), e imposta **Questa pianta mostra** sullo schema. Ogni valore dei locali riceve una riga con:

- un **Colore**,
- un **Retino** tra i motivi del progetto, con la sua **Scala** e il colore dell’**Inchiostro**,
- e un’anteprima del suo aspetto.

**Completa i colori mancanti** dà a ogni valore senza colore uno preso da una tavolozza tenue. **Nuovo** e **Duplica** creano altri schemi, per esempio uno per una presentazione al committente e uno per reparto.

![La finestra di dialogo Colori dei locali](shots/room-colors.webp)

I colori vengono disegnati sotto i muri, gli arredi e le aperture delle porte, così la pianta resta leggibile. Si stampano sulle tavole e finiscono nelle esportazioni PDF e DXF. Una nuova pianta non mostra colori dei locali finché non scegli uno schema per essa, in **Colori dei locali** o nelle proprietà della pianta.

## Legenda dei colori

**Annota → Legenda colori**: fai clic dove va la legenda sulla pianta. Elenca i colori usati dai locali della pianta, con i loro nomi, e segue qualsiasi modifica allo schema. Spostala con **Sposta**.

## Quote dei locali

**Annota → Quote dei locali** (<kbd>ID</kbd>) quota in una volta sola l’interno dei locali di una pianta. Ogni lato di un locale riceve una catena di quote allineata, appena all’interno. La catena ha un punto in ogni angolo del lato e sugli stipiti di ogni porta e finestra che vi si trova.

- Con dei locali selezionati, si quotano solo quelli. Senza selezione, si quotano tutti i locali del livello della pianta.
- Richieste su una planimetria, che vede tutto il modello dall’alto e non taglia alcun muro, le catene vanno nella pianta del livello. L’altezza del testo di una quota si imposta a parte nelle Proprietà (**Altezza del testo**, mm sulla carta; vuoto: quella del suo stile di quota). Le altezze dei testi sono sulla carta: su una planimetria in scala 1:500 lo stesso testo è cinque volte più grande nel modello che su una pianta in scala 1:100.
- I punti restano agganciati ai muri, come in ogni quota. Sposta un muro e le catene lo seguono.
- Eseguilo di nuovo su un locale per rifarne le catene, per esempio dopo aver aggiunto una porta. Le vecchie catene vengono sostituite, non raddoppiate.
- I lati molto corti, sotto i 30 cm, sono esclusi. Modifica o elimina qualsiasi catena come una disegnata a mano.

![Due locali quotati all’interno: la catena di ogni lato, con la porta e le finestre](shots/room-dimensions.webp)

## Finiture dei locali

**Finiture → Rifinisci il locale** (<kbd>FR</kbd>) posa una finitura nel locale in cui fai clic: il suo pavimento, la finitura delle sue pareti e il suo soffitto, ciascuno lungo il contorno del locale come il locale stesso. Sposta un muro e le finiture lo seguono. **Finiture → Finiture dei locali** modifica le finiture; **Dalla libreria** ne porta di già pronte: Soggiorno, Camera da letto, Bagno, Cucina, Piano terra su platea e Locale tecnico. Finché lo strumento è attivo, il pannello delle proprietà mostra la finitura che posa, da scegliere prima del clic. La **Finitura** di un locale, nelle sue proprietà, ne posa un’altra o la toglie.

Una finitura ha:

- **Pavimento**, i suoi strati dal pavimento finito verso il basso (piastrelle, adesivo, massetto…). I fori del solaio nel locale (un vano scala) sono tolti. **Attraverso le porte, fino all’asse del muro** porta il pavimento sotto ogni porta, così che il pavimento del locale accanto lo incontri sull’asse del muro.
- **Pareti**, i loro strati dalla faccia del muro verso il locale, anche attorno alle isole del locale. Ogni strato può cambiare materiale lungo la sua altezza, come lo strato di un tipo di muro: nel Bagno, pittura sopra piastrelle fino a 2100 (13 mm di spessore, piastrelle e adesivo); nella Cucina, una fascia di piastrelle da 900 a 1500. Un pezzo senza materiale non lascia nulla lì. Le porte e le finestre dei muri attorno al locale sono ritagliate, e le loro mazzette rivestite con gli stessi strati, dalla faccia del locale fino al telaio (per un vano senza serramento, fino all’asse del muro); anche il fondo di una finestra, a meno che un davanzale non lo copra dal lato del locale. **Altezza**: vuota, fino al soffitto.
- **Soffitto**, i suoi strati dal solaio sopra il locale verso il basso (dall’altezza del locale quando non c’è solaio), con i suoi fori tolti.
- **Battiscopa**: un tipo di battiscopa posato attorno al locale, interrotto alle sue porte (vedi Battiscopa, in Solai e tetti).
- **Una soletta strutturale sotto il pavimento**: un solaio in calcestruzzo armato a sé sotto gli strati del pavimento, con il suo spessore e quanto va oltre il contorno del locale (sotto i muri). È un solaio come gli altri, nella struttura e nel suo abaco.

**Salva** modifica la finitura, e ogni locale che la ha viene posato di nuovo. **Rifinisci i locali selezionati** la posa nei locali selezionati.

**Retino in pianta.** Nelle piante il pavimento di un locale finito è retinato come dice la sua finitura, sotto i muri, quindi le tavole, il PDF e il DXF lo stampano (layer FINISH-HATCH). Nel **Pavimento** della finitura, **Retino in pianta** sceglie **dal suo materiale** (predefinito: il motivo di superficie dello strato superiore, il suo retino per i prospetti, dove è visto, in **Gestisci → Materiali**, altrimenti quello suggerito dal nome: piastrelle, doghe per parquet e laminato, una croce per la moquette, linee per il vinile, puntini per un pavimento continuo, altrimenti linee oblique), **motivo proprio** (qualsiasi motivo del progetto, con scala, angolo e colore) o **senza retino**; scala e angolo valgono anche per il motivo del materiale. Una pianta vista dal basso (una pianta dei controsoffitti) retina invece i soffitti, secondo il **Retino in pianta** del **Soffitto**, allo stesso modo. **Retino pavimento**, nelle proprietà del locale, lo spegne per quel locale. **Retini delle finiture dei pavimenti** di uno stile di vista (**Vista → Impostazioni di vista**, **Gestisci → Stili di vista**) lo spegne per viste intere: acceso in Architectural, spento in Structural, MEP e Coordination. Con i colori dei locali sulla pianta, il colore riempie il locale e il retino si disegna sopra.

![Una pianta con il pavimento del bagno retinato a piastrelle e quello del soggiorno a doghe](shots/room-finish-hatch.webp)

Ogni finitura misura, per materiale, la sua superficie **lorda** (il contorno del locale, nulla dedotto: l’area del pavimento, il perimetro delle pareti per l’altezza di ogni pezzo) e quella **netta** (ciò che è costruito: porte, finestre e fori del solaio tolti, le mazzette e il pavimento sotto le porte aggiunti; la quota delle mazzette è nelle quantità, come RevealArea), e il suo volume. **Finiture → Abaco delle finiture** elenca ogni locale rifinito per parte e materiale, con i totali per materiale alla fine; come ogni abaco fa grafici, si esporta e va su una tavola. In IFC una finitura è un IfcCovering: FLOORING, CLADDING o CEILING.

Una finitura riveste ciò che i muri del locale hanno già: se i tipi di muro portano il proprio intonaco, dai alle pareti del locale solo ciò che va sopra (pittura, piastrelle), oppure usa tipi di muro con il solo nucleo.

![Le finiture dei locali: il pavimento del Bagno, le pareti con piastrelle fino a 2100 e il soffitto](shots/room-finishes.webp)

![Le proprietà di un locale con la sua finitura](shots/room-finish-properties.webp)

## I locali in 3D e in IFC

I locali sono nascosti nella vista 3D, così non coprono l’edificio. **Visibilità / Grafica** nella vista 3D può mostrarli.

In un’esportazione IFC ogni locale è un IfcSpace, con il suo numero, nome, destinazione d’uso, superfici e volume.

## Locali da un file IFC

**File → Importa IFC…** porta dentro gli spazi del file come locali, qualunque sia il programma che li ha scritti (Revit, ArchiCAD, Allplan, Bonsai…):

- ciascuno va sul **suo livello**, quello in cui lo mette il file;
- il suo **contorno** viene dalla sua forma (un’estrusione, una B-rep o una mesh, oppure l’impronta disegnata accanto); mantiene quel contorno, non segue i muri;
- il suo **nome** è il nome lungo dello spazio e il suo **numero** è il suo nome (come li scrivono Revit e ArchiCAD); la sua **altezza**, e il suo **offset di base** quando inizia sopra il suo livello (un soppalco, un pavimento sopraelevato), vengono dalla sua forma;
- la sua **destinazione d’uso**, il suo **reparto** e la sua **finitura del pavimento** vengono dal file, dove li indica (il tipo di oggetto o la categoria dello spazio, una proprietà Department, il rivestimento del pavimento), così uno schema di colori per destinazione d’uso o per reparto li colora subito;
- tutto il resto che lo spazio aveva nel file, i suoi gruppi di proprietà e le sue quantità (Pset_SpaceCommon, BaseQuantities, i gruppi propri del programma), viene conservato ed elencato, in sola lettura, sotto **Proprietà IFC** nelle sue proprietà, con un filtro quando sono molti.

Il messaggio dell’importazione dice quanti locali sono entrati. Cercali sulle piante dei loro livelli (ognuno mostra la sua etichetta), in un abaco **Locali** (**+** sotto Abachi nel browser) o nella scheda IFC del browser; la vista 3D nasconde i locali.

Uno spazio la cui forma non dà alcun contorno entra comunque, con il suo nome e le sue proprietà, come un locale senza contorno: compare nell’abaco ma non è disegnato da nessuna parte. Selezionalo (dalla scheda IFC del browser), apri la pianta del suo livello, poi usa **Trova con un clic** (fai clic all’interno dei suoi muri) o **Disegna contorno** nelle sue proprietà.
