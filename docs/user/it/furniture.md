---
title: Arredi
order: 47
summary: Divani, divani angolari, poltrone, sedie, letti, armadi, cassettiere, librerie, credenze, tavoli, scrivanie e cucine in sei disposizioni, da tipi parametrici, addossati ai muri con un clic.
---

## Posizionare gli arredi

La scheda **Interni** ha, dopo le finiture, uno strumento per ogni genere di arredo: **Divano** (<kbd>SO</kbd>), **Divano angolare**, **Poltrona** (<kbd>AC</kbd>), **Sedia**, **Letto** (<kbd>BD</kbd>), **Comodino**, **Armadio** (<kbd>WD</kbd>), **Cassettiera**, **Libreria**, **Credenza**, **Tavolo** (<kbd>TB</kbd>), **Tavolino** e **Scrivania**. Scegline uno e fai clic nella pianta.

Fai clic vicino a un muro e l’arredo vi si addossa. Il suo retro si appoggia sulla faccia del muro (sull’intonaco, non sul nucleo), il fronte guarda il locale e scorre lungo il muro finché non entra più nel muro d’angolo. Fai clic lontano da ogni muro e l’arredo resta dove hai fatto clic.

Mentre uno strumento è attivo, la barra della riga di comando mostra:

- **il tipo** che lo strumento posiziona, scelto tra i tipi di quel genere del progetto (se non ce n’è ancora, il primo viene creato quando posizioni l’arredo);
- **la rotazione**, 0°, 90°, 180° o 270°, per un arredo libero;
- **Contro i muri**, che puoi disattivare per posizionare un arredo esattamente dove fai clic, anche accanto a un muro.

Il pannello delle proprietà mostra lo stesso tipo prima del clic, con le sue dimensioni e **Modifica tipi…**.

![Un soggiorno, una cucina e una camera arredati, in pianta](shots/furniture-plan.webp)

Le piante disegnano gli arredi con il loro simbolo, secondo la convenzione di disegno, non sezionati:

- un divano con lo schienale, i braccioli e i cuscini;
- un letto con la testiera, i cuscini e il piumone ripiegato;
- un armadio con l’asta appendiabiti e le ante;
- una cucina con il lavello e il piano cottura, i pensili tratteggiati.

I prospetti e le sezioni disegnano i corpi, con i tratteggi dei loro materiali. In 3D gli arredi sono corpi low-poly creati da Lapin.

![La casa arredata in 3D](shots/furniture-3d.webp)

## Sedie attorno a un tavolo, e la cucina

**Sedie attorno a un tavolo:** fai clic su un tavolo o una scrivania. Le sedie vanno lungo i lati lunghi, circa una ogni 600 mm, e una a ogni estremità quando è abbastanza larga; un tavolo rotondo le riceve in cerchio. Ogni sedia guarda il tavolo. Una scrivania riceve sedie solo sul lato anteriore. Le sedie sono del tipo scelto per lo strumento **Sedia**.

**Cucina** (<kbd>KT</kbd>) si disegna con due punti lungo un muro:

- la lunghezza della linea è quella della cucina, e il suo retro si appoggia sulla faccia del muro;
- le basi vengono divise in parti uguali, di circa 600 mm;
- ha un piano di lavoro, un lavello e un piano cottura (la loro posizione lungo la cucina si imposta sul tipo), pensili sopra e una colonna per il frigorifero a un’estremità.

La **Lunghezza** di una cucina resta modificabile nelle sue proprietà; i mobili vengono divisi di nuovo.

### Disposizioni della cucina

Un tipo di cucina ha una **Layout** (disposizione): **One wall** (la fila dritta), **Galley** (due file parallele con un corridoio in mezzo), **L-shaped**, **U-shaped**, **Island** (una fila sul muro di fondo e un’isola separata con il suo corridoio) o **Peninsula** (una L con una fila che esce dal suo braccio, il piano a sbalzo per gli sgabelli). Sceglierne una fa partire il tipo dalle misure della disposizione, e ciascuna resta modificabile accanto all’anteprima: la lunghezza dei bracci dal muro di fondo, il corridoio tra le file, la lunghezza della fila opposta, lunghezza, profondità e posto dell’isola, lunghezza e sbalzo della penisola e, per una L o una penisola, l’angolo a sinistra o a destra.

![Le sei disposizioni in pianta: su una parete, a corridoio, a L, a U, con isola e con penisola](shots/kitchen-layouts.webp)

**Sink on** e **Hob on** li mettono sulla fila principale o sull’altra parte (il braccio, la fila opposta, l’isola, la penisola), **Sink at** e **Hob at** dicono dove lungo di essa, e la **Tall unit (fridge)** sta a un’estremità libera. I mobili d’angolo sono ciechi; i pensili ci sono solo sulle file contro un muro, non su un’isola o una penisola.

![Un tipo di cucina: a L](shots/kitchen-layout-types.webp)

**Kitchen** (<kbd>KT</kbd>) disegna qualsiasi disposizione con due punti lungo il muro a cui sta la fila principale: la linea ne è la lunghezza, il dorso sulla faccia del muro, e una L, una penisola, una U o una cucina a corridoio scivola nell’angolo o tra i muri vicino a cui si disegna. **Flip** dà a una L o a una penisola l’angolo dall’altra parte. L’abaco dell’arredo nomina la disposizione e dà la profondità dell’ingombro. In IFC resta un IfcFurniture di un tipo Kitchen definito dall’utente.

### Moduli ed elettrodomestici della cucina

Un tipo di cucina può elencare i propri **moduli** sotto i suoi parametri, in **Tipi di arredo**, su due linee:

- la **Linea delle basi**: i moduli della fila principale da sinistra, visti di fronte (dopo un angolo cieco);
- la **Linea dei pensili**: i moduli appesi sopra di essa.

**Aggiungi** mette un modulo del genere scelto in fondo a una linea. **Cucina tipo** avvia la linea delle basi con un lavello, una lavastoviglie, un modulo di cassetti e un piano cottura su un forno. Le frecce spostano un modulo a sinistra o a destra, e il cestino lo toglie.

Ogni modulo ha una **larghezza**. Un mobile prende qualsiasi larghezza da 100 a 800 mm: scegli 150, 200, 300, 400, 450, 500, 600 o 800, oppure scrivine un’altra. Un elettrodomestico prende le misure in cui esiste. I moduli della linea delle basi sono:

- **Base**, il cui **Fronte** è un cassetto sopra uno sportello, sportelli (uno o due, con il lato su cui è incernierato uno sportello singolo, **Cerniere sul lato**), cassetti (da 1 a 4), cassetti per pentole (un cassetto basso sopra due profondi) o ripiani aperti;
- **Base lavello**: un lavello a una vasca, a 1½ vasche, doppio (da 800) o d’angolo, con rubinetto normale, rubinetto estraibile o senza. Un lavello d’angolo entra nell’angolo cieco quando gli sta accanto.
- **Piano cottura su forno**, largo 600, 800 o 900, a induzione o a gas, con o senza il forno sotto;
- **Cucina a libera installazione**, larga 500, 600 o 900, a induzione o a gas. Il piano di lavoro si ferma da una parte e dall’altra.
- **Lavastoviglie** integrata, larga 450 o 600, dietro un frontale a tutta altezza uguale agli altri;
- **Frigorifero**: sottopiano, a colonna integrato (uno sportello del frigorifero sopra uno del congelatore) o a libera installazione;
- **Colonna forno**, con cassetti sotto il forno e uno sportello sopra;
- **Colonna**, con sportelli o ripiani aperti;
- **Fianco di finitura** e **Vuoto**.

Una colonna, un frigorifero o una colonna forno ha la sua **Altezza**. Sulla linea dei pensili, un **Pensile** ha sportelli, sportelli in vetro o ripiani aperti, la sua **Altezza** e la sua altezza **Da terra**. Lascia vuota una delle due per prendere i valori della fila. Una **Cappa** è una cappa a camino (a 650 sopra il piano di lavoro, con il suo condotto fino alla sommità dei pensili) o una cappa integrata in un pensile. Un **Vuoto** lascia nuda la parete. I pensili scavalcano una colonna.

Senza una linea dei pensili elencata, ogni base riceve un pensile della sua larghezza, e un piano cottura o una cucina riceve una cappa a camino. Su un’isola o una penisola, **Cappa sopra il piano cottura lì** appende una cappa sopra il piano cottura che vi sta.

Quello che i moduli lasciano della fila si divide in parti uguali come prima, e meno di 100 mm diventa un fianco di finitura. I moduli che non entrano nella fila sono lasciati fuori, e l’anteprima e l’elemento lo dicono. **Adatta ai moduli**, nelle proprietà di una cucina, porta la sua lunghezza a quello che i suoi moduli richiedono: la loro, più un angolo cieco e la colonna in fondo. Un tipo senza moduli propri è costruito come prima.

In pianta, ogni modulo ha il proprio simbolo:

- l’apertura degli sportelli bassi, tratteggiata e solo al dettaglio più fine;
- le vasche del lavello e il suo rubinetto;
- le zone o i fuochi del piano cottura;
- le colonne, barrate;
- **DW** sulla lavastoviglie, **F** su un frigorifero e **OV** su una colonna forno;
- i pensili e le cappe, sopra.

I prospetti e il 3D mostrano ogni fronte, maniglia ed elettrodomestico.

I materiali hanno due ruoli in più: **Zoccolo** e **Maniglie**, oltre a frontali, corpo, piano (top), metallo e vetro. Un tipo di cucina fatto prima prende per lo zoccolo il materiale del suo corpo, e per le maniglie quello del suo metallo.

![I moduli di un tipo di cucina: la linea delle basi e quella dei pensili, con l’anteprima del motore](shots/kitchen-units-types.webp)

![Una cucina di moduli ed elettrodomestici misti, in 3D](shots/kitchen-units-3d.webp)

**Interni → Mobili cucina** crea un abaco dei moduli di ogni cucina, da sinistra, la linea delle basi poi quella dei pensili, con la loro larghezza, altezza e frontale o elettrodomestico. In IFC la cucina resta un solo IfcFurniture del suo tipo, e i moduli viaggiano con il tipo, così Lapin li rilegge.

## Le proprietà di un arredo

Seleziona un arredo per vedere:

- **Tipo**, il suo genere e le dimensioni;
- **Rotazione**;
- **Sopra il pavimento**, per un arredo rialzato su una pedana;
- **Lunghezza**, per una cucina.

**Capovolgi** dà all’arredo l’altra mano, sul posto: la chaise longue dall’altro lato, il frigorifero all’altra estremità, l’angolo del divano angolare dall’altro lato. Il fronte resta dov’era. **Modifica → Specchia** fa lo stesso.

Trascina la maniglia al centro di un arredo per spostarlo; trascina la maniglia davanti per girare il fronte verso il cursore.

![Le proprietà di un divano](shots/furniture-properties.webp)

## Tipi di arredo

**Interni → Tipi di arredo** elenca i tipi del progetto per gruppo (sedute, letti, contenitori, tavoli, cucina), ciascuno con il numero di arredi che lo usano. Scegli un genere e **Nuovo tipo**, oppure **Duplica** un tipo per modificarne una copia. Un tipo contiene:

- il **nome**, la **larghezza**, la **profondità** e l’**altezza** (dello schienale per un divano, della testiera per un letto, del piano di lavoro per una cucina);
- i parametri propri del suo genere:
  - divani e poltrone: posti, braccioli, larghezza dei braccioli, altezza della seduta, una chaise longue a sinistra o a destra e la sua lunghezza, piedini;
  - un divano angolare: il lato dell’angolo e la lunghezza di quel lato;
  - un letto: il piano del materasso, la testiera, la pediera, i cuscini, un piumone ripiegato;
  - un armadio: ante, a battente o scorrevoli, cassetti in basso, zoccolo;
  - una cassettiera: file e colonne di cassetti;
  - una libreria: ripiani e ante in basso fino a un’altezza;
  - una credenza: ante e cassetti;
  - un tavolo: rettangolare, rotondo o ovale, su quattro gambe o una gamba centrale;
  - una scrivania: una cassettiera a sinistra o a destra;
  - una cucina: larghezza delle basi, zoccolo, piano di lavoro, posizione del lavello e del piano cottura, pensili, colonna, e i suoi moduli (sopra);
- i suoi **materiali** per ruolo: corpo, imbottitura, piano, frontali, biancheria, metallo, vetro, e per una cucina lo zoccolo e le maniglie. Scrivi un nome di materiale qualsiasi; uno che il progetto non ha viene creato, e **Gestisci → Materiali** ne imposta il colore e i tratteggi.

L’anteprima accanto ai campi mostra il simbolo in pianta e il fronte del tipo mentre lo modifichi. Ogni arredo di un tipo segue ogni modifica.

![La finestra Tipi di arredo con un divano e la sua chaise longue](shots/furniture-types.webp)

## Abachi e IFC

**Abaco degli arredi** elenca ogni tipo con il suo genere, le dimensioni, il numero di arredi e i locali in cui si trovano, con il totale. **Arredi per locale** elenca ogni locale con ciò che contiene. Entrambi sono nel browser di progetto, sotto **Abachi**, e si stampano ed esportano come ogni abaco. Un tipo di arredo con una voce di prezzo viene computato a pezzo nel computo.

In IFC un arredo è un **IfcFurniture**, con il tipo predefinito del suo genere (SOFA, CHAIR, BED, TABLE, DESK, SHELF, oppure USERDEFINED con Wardrobe, Kitchen…). Il suo **IfcFurnitureType** è collegato con IfcRelDefinesByType, e i suoi corpi portano i loro materiali. Lapin rilegge i propri arredi come arredi nativi e modificabili. Gli arredi di altri programmi entrano come riferimenti.
