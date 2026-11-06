---
title: Arredi
order: 47
summary: Divani, divani angolari, poltrone, sedie, letti, armadi, cassettiere, librerie, credenze, tavoli, scrivanie e cucine lineari, da tipi parametrici, addossati ai muri con un clic.
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
  - una cucina: larghezza delle basi, zoccolo, piano di lavoro, posizione del lavello e del piano cottura, pensili, colonna;
- i suoi **materiali** per ruolo: corpo, imbottitura, piano, frontali, biancheria, metallo, vetro. Scrivi un nome di materiale qualsiasi; uno che il progetto non ha viene creato, e **Gestisci → Materiali** ne imposta il colore e i tratteggi.

L’anteprima accanto ai campi mostra il simbolo in pianta e il fronte del tipo mentre lo modifichi. Ogni arredo di un tipo segue ogni modifica.

![La finestra Tipi di arredo con un divano e la sua chaise longue](shots/furniture-types.webp)

## Abachi e IFC

**Abaco degli arredi** elenca ogni tipo con il suo genere, le dimensioni, il numero di arredi e i locali in cui si trovano, con il totale. **Arredi per locale** elenca ogni locale con ciò che contiene. Entrambi sono nel browser di progetto, sotto **Abachi**, e si stampano ed esportano come ogni abaco. Un tipo di arredo con una voce di prezzo viene computato a pezzo nel computo.

In IFC un arredo è un **IfcFurniture**, con il tipo predefinito del suo genere (SOFA, CHAIR, BED, TABLE, DESK, SHELF, oppure USERDEFINED con Wardrobe, Kitchen…). Il suo **IfcFurnitureType** è collegato con IfcRelDefinesByType, e i suoi corpi portano i loro materiali. Lapin rilegge i propri arredi come arredi nativi e modificabili. Gli arredi di altri programmi entrano come riferimenti.
