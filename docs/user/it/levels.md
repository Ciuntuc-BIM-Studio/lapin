---
title: Livelli e piante
order: 60
summary: Aggiungi livelli, impostane le altezze e vedi un altro livello sotto la pianta su cui disegni.
---

<!-- video levels -->

## Livelli

Il browser di progetto elenca i livelli in alto, il più alto per primo. **+** accanto a **Livelli** aggiunge un livello sopra il più alto.

Seleziona un livello per vederne il **livello di base** e il **livello superiore**. Ciascuno dei due può stare sopra o sotto ±0.00. La sua altezza è la distanza tra i due. Quando sposti la base, la sommità resta dov’è. Un nuovo progetto parte con Foundations, Ground floor e First floor. Un livello è alto al massimo 100 m e sta entro ±10 km da ±0.00: un valore scritto nell’unità sbagliata viene rifiutato. Una scala che salirebbe più di 50 m fino al livello superiore si ferma lì e dice perché.

![Le proprietà di un livello: livello di base, livello superiore e altezza](shots/level-properties.webp)

Ogni livello ha la sua pianta in **Piante**. Fai doppio clic su una pianta per disegnare su quel livello. Il livello su cui disegni è indicato nella casella in alto a sinistra della vista.

Nelle sezioni e nei prospetti ogni livello è segnato a destra con il suo nome e la sua quota. **Gestisci → Stili oggetto → Linee nascoste** imposta anche il **simbolo di livello**: un triangolo vuoto, un triangolo con la metà sinistra nera (come lo disegna la norma rumena), un triangolo nero o un trattino, e la sua dimensione sulla carta.

Le quote di livello si scrivono nelle unità del progetto: in metri (+3.00) in un progetto metrico, come le scrivono i disegni, e in piedi e pollici (+9'-10 1/8") in uno imperiale. Le seguono il browser, il riquadro in alto a sinistra della vista, i simboli di livello nelle sezioni e nei prospetti e le quote di livello.

## Vedere il livello sottostante

Per vedere un altro livello mentre disegni, sceglilo in **Sottostante** nella barra della pianta. La pianta di quel livello è disegnata sotto, sbiadita e grigia. Puoi fare snap su di essa, ma non puoi selezionarla.

![Il primo piano disegnato sopra il piano terra, mostrato come sottostante](shots/underlay.webp)

Il sottostante serve solo ad aiutarti a disegnare; non viene stampato. Per stampare gli elementi di altri livelli su una pianta, apri le proprietà della vista in pianta e sceglili in **Piani mostrati**. Puoi mostrare alcuni livelli o tutti, in mezzatinta se vuoi.

## Copiare su altri piani

Per ripetere muri, solai o una pianta intera su altri livelli, selezionali (muri con le loro porte e finestre, solai, locali, un gruppo…) e scegli **Modifica → Copia ai piani**. Puoi anche digitare `COPYSTOREY` nella riga di comando, o fare clic destro sulla selezione in una pianta o nella vista 3D. Spunta i livelli su cui copiare. **Tutti sopra** e **Tutti sotto** spuntano tutti i livelli sopra o sotto quello della selezione, e un Maiusc-clic spunta tutti i livelli tra quello cliccato e l’ultimo cliccato. Il livello su cui sta la selezione non si può spuntare.

![Copia ai piani: due livelli spuntati e le opzioni](shots/copy-to-storeys.webp)

- **Relativa** (predefinita): ogni copia sta alla stessa altezza sopra il suo nuovo livello dell’originale sopra il proprio. Un muro disegnato al piano terra sta al primo piano.
- **Assoluta**: ogni copia resta dove l’originale è nello spazio, sul nuovo livello. Il suo offset di base compensa la differenza.
- **Anche gli elementi ospitati**: porte e finestre seguono i loro muri, le aperture ciò che tagliano. Le etichette e le quote degli elementi copiati passano dalla pianta del loro livello a quella del nuovo livello. Un locale porta con sé le sue finiture e una scala lo spazio sotto di essa. Togli la spunta per copiare i muri senza porte e finestre.
- **Ricrea le relazioni tra le copie**: i muri uniti in un angolo o a T si uniscono allo stesso modo su ogni nuovo livello. Un muro collegato a un solaio o a una copertura copiati con esso si collega alla loro copia. Un gruppo diventa un gruppo delle copie, e un assieme si copia intero. Una scala sale fino al livello sopra il nuovo e taglia lì la propria apertura nel solaio.
- **Salta i duplicati**: un livello che ha già lo stesso elemento nello stesso punto non ne riceve un secondo. Il messaggio in basso dice quanti sono stati saltati.

Le copie usano gli stessi tipi degli originali e ricevono nuovi GlobalId IFC. Tutte le copie sono un solo passo di annullamento e restano selezionate dopo, così puoi spostarle o modificarle insieme. In una sessione condivisa, solo l’ospite annulla le proprie copie.

![I muri e il solaio del piano terra copiati sui due livelli sopra, in 3D](shots/copy-to-storeys-3d.webp)

Per spostare invece la selezione su un altro livello, scegli **Modifica → Sposta al piano**. Gli elementi mantengono le loro altezze sopra il nuovo livello, o restano dove sono nello spazio.

## La quota di un elemento

Un elemento su un livello sta alla quota del livello e a uno scostamento da essa. **Proprietà → Posizionamento** mostra il suo **Piano**, il suo **Scostamento dal livello** e la sua **Quota altimetrica** sopra ±0,00: la base di un muro o pilastro, il lato superiore di un solaio o trave (lo scostamento di una trave è dalla sommità del livello), il lato inferiore di una fondazione (il livello inferiore del suo tipo sotto il livello, più lo scostamento). Digita una delle cifre e l'altra la segue: l'elemento si sposta in tutte le viste, in un solo passo di annullamento. Per mettere un elemento su un altro livello, cambia il suo **Piano**. Ciò che disegni in una pianta sta al livello di quella pianta.
