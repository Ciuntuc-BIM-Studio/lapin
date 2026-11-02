---
title: Scale
order: 42
summary: Posiziona una scala tra i piani, impostala nel suo tipo e crea i pochi gradini che salgono a un portico o a una porta.
---

## Scale

Scegli **Architettura → Scala** (<kbd>ST</kbd>). Fai clic sul piede della scala (il centro della sua prima alzata), poi su un punto nella direzione in cui sale. Per una scala a L o a U, un terzo clic a sinistra o a destra della rampa stabilisce da che parte gira. La scala sale dal suo piano al piano superiore. Lapin calcola il numero di alzate, l’alzata e la pedata dall’altezza e dalle regole del tipo.

Seleziona la scala per modificarla nel pannello Proprietà:

- **Tipo di scala.** La forma, le regole, la costruzione e il parapetto vengono dal tipo, impostato in **Architettura → Tipi di scala**.
- **Larghezza**, **Svolta**, **Offset di base** e **Offset superiore.**
- **Dislivello.** Lascialo vuoto e la scala sale fino al piano superiore. Un valore dà alla scala un dislivello proprio, indipendentemente dai piani. Usalo per pochi gradini tra due livelli dello stesso piano.
- **Fisso.** Alzate, alzata, pedata e lunghezza del pianerottolo, quando vuoi fissarle. Lasciale vuote perché decidano le regole.
- **Verifiche.** Ogni valore confrontato con le regole del tipo, in verde o in rosso.

Una scala chiede al solaio superiore un’apertura sopra di sé. Togli la spunta da **Apertura nel solaio superiore** se il solaio deve restare intero.

## Forme di scala e tipi pronti

In **Architettura → Tipi di scala**, la scheda **Forma** offre: **Rettilinea**, **Rettilinea con pianerottolo**, **A L** e **A U** (ognuna con svolta su un pianerottolo o con **gradini a ventaglio**; una U senza pozzo è una scala a due rampe), **A chiocciola** (attorno a un palo, con il suo **Raggio del palo**), **Curva** (con il suo **Raggio interno**) e **Imperiale (biforcata)**: una rampa principale al centro fino a un pianerottolo, poi due rampe di ritorno ai due lati. Per una scala a chiocciola o curva, **Rotazione (°)** stabilisce di quanto gira; lasciata vuota, decidono le alzate e la pedata, mentre se indicata la pedata ne deriva. Ogni scala può anche modificarla nelle sue proprietà.

La libreria aggiunge al progetto dei tipi pronti: una scala a due rampe, una scala con gradini a ventaglio, una scala a chiocciola, una scala curva, una scala a sbalzo e una scala imperiale.

Nella scheda **Costruzione**, **Costruita come** sceglie una soletta rampante, gradini pieni sul terreno, **Gradini a sbalzo (sospesi)** (ogni gradino una lastra incastrata nel muro) o **Gradini su cosciali**.

Il **Parapetto** ha il suo **Disegno**: **Montanti e corrimano**, **Balaustri** (al massimo a **Interasse dei balaustri**), **Pannelli di vetro**, **Cavi** o un **Parapetto pieno**; un **Corrimano** quadrato o tondo, la sua altezza, il **Rientro dal bordo**, quanto **Prosegue oltre le estremità** e il suo colore. Nel pannello Proprietà, **Parapetto** lo mette su lati diversi da quelli del tipo per una sola scala.

![Il tipo della scala a chiocciola, con i gradini numerati](shots/stair-spiral.webp)

### Gradini numerati

Sotto **In pianta** nella scheda **Forma**: **Numera i gradini** scrive il numero di ogni gradino in un cerchio, dalla prima alzata in su; **Etichetta** scrive le alzate, l’alzata e la pedata, e la larghezza vicino al piede; **«UP» al piede della freccia** segna dove inizia. Le piante e le tavole li mostrano.

![Gradini numerati in pianta, una scala imperiale e una scala a sbalzo](shots/stair-numbers-plan.webp)

![Una scala a chiocciola, una imperiale e una a sbalzo in 3D](shots/stair-shapes-3d.webp)

## Gradini d’ingresso (un portico)

I gradini che salgono a una porta d’ingresso hanno un dislivello proprio: dal terreno, pochi gradini sotto il piano terra, fino a ±0.00. Lapin ha per loro un tipo di scala pronto, **Gradini d’ingresso**: larghi 2 m, tre alzate da 150 mm, in calcestruzzo pieno gettato sul terreno, senza parapetto.

![Gradini d’ingresso davanti a un portico, in pianta](shots/entrance-steps-plan.webp)

1. **Il portico.** Disegna il pianerottolo come un solaio: scegli **Architettura → Solaio** (<kbd>SB</kbd>) e fai clic sul suo contorno fuori dal muro, davanti alla porta. La faccia superiore di un solaio è al livello del suo piano (±0.00 al piano terra) e il suo tipo ne dà lo spessore. Per cambiare lo spessore, crea un tipo di solaio per i portici in **Architettura → Tipi di solaio**, per esempio 150 mm di calcestruzzo.
2. **I gradini.** Scegli **Architettura → Gradini d’ingresso**. Fai clic al centro del bordo del portico, poi fai clic dal lato verso cui escono i gradini. I gradini salgono fino al bordo del portico e terminano a ±0.00. Iniziano 450 mm più in basso: il loro **Offset di base** è −450 mm.
3. **L’altezza.** Seleziona i gradini. In **Dislivello**, digita la differenza reale tra il terreno e il portico, per esempio 300. La sommità resta al portico e il piede sale: l’offset di base diventa −300 mm e i gradini hanno due alzate. **Larghezza** li rende larghi quanto il portico, per esempio 2400.

![Le proprietà dei gradini d’ingresso: dislivello, offset di base, livelli](shots/entrance-steps-properties.webp)

**Livelli** mostra dove i gradini iniziano e finiscono, per esempio −0.45 → ±0.00. Con una sola alzata (un dislivello di 150 mm), il gradino è il bordo stesso del portico e non c’è alcuna pedata da costruire. Dai ai gradini due o più alzate per avere delle pedate davanti al portico.

Il tipo viene aggiunto al progetto la prima volta che lo usi. Per modificarlo, apri **Architettura → Tipi di scala** e scegli **Gradini d’ingresso**:

- **Forma → Un dislivello proprio.** Il dislivello del tipo; ogni scala può comunque cambiarlo con **Dislivello**.
- **Regole → Insieme di regole → gradini d’ingresso.** Alzate da 120–170 mm e pedate da 300–400 mm, con una sola alzata consentita.
- **Costruzione → Costruita come.** **Piena, gettata sul terreno** riempie i gradini fino alla loro base. **Soletta rampante sotto i gradini** li costruisce come una scala interna.

![Il tipo Gradini d’ingresso nel configuratore delle scale](shots/stair-types.webp)

Perché un altro tipo di scala salga un dislivello proprio, spunta **Un dislivello proprio** nella sua scheda **Forma**. Per trasformare una scala posizionata in gradini d’ingresso, scegli **Gradini d’ingresso** come suo **Tipo di scala**: scende in modo che la sua sommità sia al livello del suo piano.

![La casa con il portico e i gradini d’ingresso in 3D](shots/entrance-steps-3d.webp)

In IFC, i gradini sono un IfcStair di tipo STRAIGHT_RUN_STAIR. La sua rampa è un IfcStairFlight con NumberOfRisers, NumberOfTreads, RiserHeight e TreadLength, e Pset_StairCommon riporta gli stessi valori. Il portico è un IfcSlab.

## Parapetti

**Architettura → Parapetto** (<kbd>RL</kbd>) disegna un parapetto a sé lungo il bordo di un balcone, una terrazza o un ballatoio: fai clic sui suoi punti, Enter termina. Nelle sue proprietà prende i disegni dei parapetti delle scale (montanti e corrimano, balaustri, vetro, cavi o un pannello pieno), la sua altezza (1 m per impostazione predefinita), l’offset di base, il profilo e le dimensioni del corrimano e l’interasse dei montanti; i suoi punti si trascinano con le loro maniglie (Ctrl/⌘-clic al centro di un lato ne aggiunge uno). In IFC è un IfcRailing (GUARDRAIL).
