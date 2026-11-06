---
title: Scale e rampe
order: 42
summary: Posa una scala o una rampa tra i piani o fino a un’altezza qualsiasi, impostala nel suo tipo e fai i pochi gradini fino a un portico o a una porta.
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

- **Forma → Sale → Un’altezza propria.** Il dislivello del tipo, salito **Fino alla quota del suo piano, da sotto (un ingresso)**; ogni scala può comunque cambiarlo con **Dislivello**.
- **Regole → Insieme di regole → gradini d’ingresso.** Alzate da 120–170 mm e pedate da 300–400 mm, con una sola alzata consentita.
- **Costruzione → Costruita come.** **Piena, gettata sul terreno** riempie i gradini fino alla loro base. **Soletta rampante sotto i gradini** li costruisce come una scala interna.

![Il tipo Gradini d’ingresso nel configuratore delle scale](shots/stair-types.webp)

Perché qualsiasi altro tipo di scala salga un’altezza propria, scegli **Sale → Fino a → Un’altezza propria** nella sua scheda **Forma** (vedi la sezione seguente). Per trasformare una scala posizionata in gradini d’ingresso, scegli **Gradini d’ingresso** come suo **Tipo di scala**: scende in modo che la sua sommità sia al livello del suo piano.

![La casa con il portico e i gradini d’ingresso in 3D](shots/entrance-steps-3d.webp)

In IFC, i gradini sono un IfcStair di tipo STRAIGHT_RUN_STAIR. La sua rampa è un IfcStairFlight con NumberOfRisers, NumberOfTreads, RiserHeight e TreadLength, e Pset_StairCommon riporta gli stessi valori. Il portico è un IfcSlab.

## Una scala fino a un’altezza qualsiasi

Una scala non deve per forza andare da un piano al successivo. Nella scheda **Forma** del tipo, **Sale → Fino a** sceglie **Il piano sopra** o **Un’altezza propria**, con il suo **Dislivello**, e **Parte** dice da dove comincia:

- **Fino alla quota del suo piano, da sotto (un ingresso).** Finisce alla quota del piano e comincia il dislivello più in basso, come i gradini d’ingresso.
- **Dalla quota del suo piano in su (una terrazza, una piattaforma).** Parte dalla quota del piano e sale il dislivello: per esempio 1,20 m fino a una terrazza o a una banchina di carico.

Ogni scala posata può salire un’altra altezza: scrivila in **Dislivello** nelle sue proprietà (vuoto: quella del tipo). Se sale dalla quota, un dislivello nuovo tiene fermo il piede; se viene da sotto, tiene ferma la sommità.

## Lo spazio sotto la scala

Lo spazio sotto una rampa si può arredare: un ripostiglio, un ufficio nell’armadio, un angolo lettura, un ingresso con panca e appendiabiti, una casetta dei giochi per i più piccoli o una cuccia. Seleziona una scala a rampe dritte e fai clic su **Spazio sotto la scala…** in fondo alle sue proprietà (o **Architettura → Spazio sotto la scala**).

![Il configuratore dello spazio sotto la scala: l’angolo lettura sotto la rampa](shots/under-stair-dialog.webp)

A sinistra ci sono gli stili pronti: **Ripostiglio**, **Ufficio nell’armadio**, **Angolo lettura**, **Ingresso e guardaroba**, **Casetta dei giochi per bambini** e **Cuccia per il cane**, e sopra gli stili del progetto. Scegline uno e modificalo al centro:

- **Posizionamento.** **Sotto la rampa** (in una scala a L o a U), il **Lato aperto** (di norma opposto alla svolta), dove **Inizia a** e **Finisce a** lungo la rampa (vuoto: da dove l’intradosso è a 300 mm dal pavimento fino all’ultima alzata) e la sua **Profondità** (vuota: larga quanto la scala).
- **Il fronte.** La **Parte bassa** chiusa da **Ante dell’armadio** (ciascuna circa della **Larghezza dell’anta**, con **Ripiani dietro le ante**), un **Pannello chiuso** o lasciata aperta; una **Libreria all’estremità alta**.
- **Elemento principale.** Una **Scrivania con un ripiano** (e una luce), una **Panca con cuscini** (cassetti sul fronte), una **Panca d’ingresso e appendiabiti** (vani per le scarpe sotto), una **Porta della casetta dei giochi** (e una **Finestra accanto alla porta**) o una **Cuccia** dietro un’apertura incorniciata, ciascuno con le sue misure.
- **Materiali.** Lo **Spessore del pannello** e i materiali di **Struttura**, **Frontali** e **Tessuto**, tra quelli del progetto.

A destra l’anteprima mostra lo spazio visto dal lato aperto sotto l’intradosso della rampa e dall’alto, la lunghezza, la profondità e le altezze, e le sue parti. Ogni parte è tagliata dalla pendenza; una parte che non ci sta (una scrivania dove la scala è troppo bassa) è tralasciata e l’anteprima lo dice. **Crea** o **Applica** è un solo passo di annullamento. **Salva come stile…** conserva ciò che riempie lo spazio come stile del progetto; **Aggiorna lo stile** lo salva di nuovo e tutti gli spazi dello stile seguono.

![Lo spazio sotto la scala nelle proprietà](shots/under-stair-properties.webp)

Lo spazio è un elemento a sé, ospitato dalla sua scala. Quando la scala cambia (larghezza, rotazione, gradini, tipo), lo spazio si ricostruisce sotto di essa e viene eliminato con lei. I campi di posizionamento sono nelle proprietà, e **Configura…** riapre il configuratore.

![I sei stili sotto sei scale](shots/under-stair-3d.webp)

Piante, sezioni, prospetti e tavole lo disegnano come ogni altro elemento: sezionato dove la pianta lo taglia, visto sotto.

![L’angolo lettura in pianta, gli altri accanto](shots/under-stair-plan.webp)

![Una sezione lungo la rampa attraverso l’angolo lettura](shots/under-stair-section.webp)

**Architettura → Abaco degli spazi sotto scala** elenca le parti di ogni spazio con numero di pezzi, materiale e volume, e i totali per materiale. In IFC lo spazio è un IfcElementAssembly ("Under-stair space") di IfcFurniture (ripiani, scrivania, panca, cuscini, cuccia), IfcDoor, IfcPlate, IfcDiscreteAccessory (maniglie, appendiabiti) e IfcLightFixture, collegato alla sua IfcStair.

## Rampe

Scegli **Architettura → Rampa** (<kbd>RU</kbd>). Fai clic al centro dell’estremità bassa della rampa, poi su un punto nella direzione in cui sale; per una rampa a L o a zigzag, un terzo clic a sinistra o a destra del tratto stabilisce da che parte gira. La prima volta il progetto riceve dalla libreria **Ramp 6 %**: larga 1,20 m al 6 %, in tratti di al massimo 9 m con pianerottoli di 1,50 m in mezzo, una soletta in cemento armato sotto una finitura e corrimano su entrambi i lati. Sale fino al piano sopra, a meno che il suo tipo o il suo **Dislivello** non dicano altro.

**Architettura → Rampa d’ingresso** si posa come i gradini d’ingresso: fai clic al centro del bordo del portico, poi sul lato verso cui esce la rampa. Sale di 450 mm dal terreno fino a ±0,00, piena sul terreno, e **Dislivello** la cambia come per i gradini.

I tipi di rampa si fanno nello stesso configuratore, **Architettura → Tipi di scala**, con **Nuovo tipo di rampa**, oppure dalla libreria: **Switchback ramp** (a zigzag) e **Entrance ramp** (d’ingresso):

- **Forma.** **Rettilinea**, **Rettilinea, con pianerottoli**, **A L** o **A zigzag (andata e ritorno)** con la sua **Distanza tra le rampe**; la **Larghezza**, la **Pendenza** in percentuale, il **Tratto più lungo** (se è più lunga, la rampa si divide in tratti uguali con pianerottoli in mezzo) e la **Lunghezza del pianerottolo**. La lunghezza in pianta è l’altezza divisa per la pendenza: 3 m al 6 % fanno 50 m. **Sale** funziona come per le scale.
- **Regole.** **Pendenza massima** (8 % di default), **Larghezza minima** e **Altezza libera di passaggio**. I controlli mostrano in rosso una pendenza più ripida di quanto permettono le regole, un tratto più lungo del più lungo e un pianerottolo più corto di 1,50 m o della larghezza della rampa.
- **Costruzione.** **Una soletta inclinata in cemento armato** con il suo **Spessore della soletta**, oppure **Piena, gettata sul terreno**; la finitura e il parapetto come per le scale.

![La rampa a zigzag nel configuratore](shots/ramp-types.webp)

Seleziona una rampa per vederne il tipo, **Larghezza**, **Svolta**, **Offset di base** e **Dislivello**, una **Pendenza** e una **Lunghezza del pianerottolo** proprie (vuote: quelle del tipo), le lunghezze dei suoi tratti e i suoi controlli. Le piante disegnano i suoi tratti e pianerottoli, la freccia di salita e la pendenza in percentuale lungo ogni tratto. Una rampa che sale al piano sopra chiede un foro nel suo solaio, come una scala.

![La rampa a zigzag e la rampa d’ingresso in pianta, con la pendenza lungo ogni tratto](shots/ramps-plan.webp)

![Le rampe in 3D](shots/ramps-3d.webp)

In IFC una rampa è un IfcRamp (STRAIGHT_RUN_RAMP, TWO_STRAIGHT_RUN_RAMP, QUARTER_TURN_RAMP o HALF_TURN_RAMP) tipizzato da un IfcRampType, fatto di IfcRampFlight, pianerottoli (IfcSlab LANDING) e IfcRailing.

## Parapetti

**Architettura → Parapetto** (<kbd>RL</kbd>) disegna un parapetto a sé lungo il bordo di un balcone, una terrazza o un ballatoio: fai clic sui suoi punti, Enter termina. Nelle sue proprietà prende i disegni dei parapetti delle scale (montanti e corrimano, balaustri, vetro, cavi o un pannello pieno), la sua altezza (1 m per impostazione predefinita), l’offset di base, il profilo e le dimensioni del corrimano e l’interasse dei montanti; i suoi punti si trascinano con le loro maniglie (Ctrl/⌘-clic al centro di un lato ne aggiunge uno). In IFC è un IfcRailing (GUARDRAIL).

## Balconi

**Architecture → Balcony** (<kbd>BY</kbd>) appende un balcone a un muro: scegli lo stile nella barra della riga di comando e fai clic fuori dalla faccia del muro, dove va; si posa su quel lato del muro alla quota del pavimento del piano. Un **balcone alla francese** (Juliet) posato vicino a una porta o a una finestra si centra su di essa, 200 mm più largo. Dove non c’è un muro vicino, il balcone sta dove fai clic. **Balcony by outline** disegna qualsiasi forma: fai clic sugli angoli, il primo lato sta contro l’edificio. **Gallery edge** disegna il bordo di un soppalco su uno spazio a doppia altezza con due punti (il vuoto a sinistra, il soppalco a destra).

Gli stili predefiniti coprono i tipi richiesti:

- **Juliet balcony**: un parapetto a balaustri davanti a una portafinestra su una piccola lastra di pietra con due mensole.
- **Cantilevered slab, glass**: una soletta in calcestruzzo a sbalzo dal solaio con taglio termico, piastrellata in pendenza, gocciolatoio sotto il bordo, vetro su tre lati.
- **Stacked, on posts**: una soletta su pilastri in acciaio sul davanti, che poggiano sul balcone sotto (o sul terreno); ripetuta piano su piano.
- **On steel brackets**, **Hung from tie rods** (tiranti dagli angoli anteriori fino al muro), **Rounded corners**, **Curved front** con parapetto pieno.
- **Loggia**: rientrata nella facciata. Taglia il suo incasso nel muro (largo quanto la loggia e alto quanto la sua **Recess height**), costruisce le pareti laterali e una soletta di copertura dietro la faccia e alza il proprio **Back wall** (del tipo del muro, alto uguale) alla profondità della loggia, dove va la porta. Il parapetto è sul filo della facciata.
- **Corner balcony**: lungo il muro e attorno all’angolo dell’edificio, indietro lungo il muro laterale; posato vicino all’estremità di un muro, la avvolge.
- **Mezzanine gallery edge**: la soletta del soppalco dietro il bordo, una fascia sul bordo e un parapetto a bacchette.

![Balconi al primo piano: uno alla francese alla porta, uno su pilastri, uno d’angolo, una loggia, uno curvo, uno arrotondato e uno appeso; il balcone del piano sopra, tratteggiato](shots/balconies-plan.webp)

La pianta disegna soletta, finitura e parapetto di un balcone come li taglia o li vede, e i balconi del piano sopra tratteggiati. Sezioni e prospetti disegnano soletta, parapetto, pilastri e mensole.

### Il configuratore

**Architecture → Balcony configurator** (o l’ingranaggio accanto a **Style** nelle proprietà del balcone) funziona come quello delle coperture: gli stili a sinistra (quelli del progetto, poi i predefiniti), i campi del balcone in schede a destra, sopra la sua pianta o una sezione a metà come la bozza li costruirebbe:

- **Shape**: **Kind**, **Outline** (rettangolo, angoli arrotondati con **Corner radius**, fronte curvo con **Front bulge** o il poligono disegnato), **Width** e **Depth**, il risvolto del balcone d’angolo, la soletta e **Fascia height** del soppalco.
- **Slab and finish**: **Slab thickness** e materiale, **Edge** (dritto, con **drip** — gocciolatoio —, con **nosing** della finitura o **tapered** fino a **Edge thickness**), spessore e materiale della finitura e la sua **Fall (%)** verso il fronte, l’intradosso e **Thermal break** (80 mm tra la soletta e il solaio interno; l’abaco e l’IFC lo dicono).
- **Railing**: i modelli di parapetto (montanti e corrimano, balaustri, vetro, cavi, pannello pieno) o un **parapet** del suo spessore e materiale, o nessuno; altezza, solo davanti o anche sui lati, arretramento dal bordo, corrimano, montanti e colore.
- **Supports**: **Carried by** niente (sbalzo), **posts** (tondi o quadri, dimensione e interasse), **brackets** (profondità, altezza e interasse) o **hangers** (altezza di fissaggio dei tiranti sopra la soletta) e il loro materiale.
- **Loggia**: altezza dell’incasso, spessore delle pareti laterali, soletta oltre la faccia, soletta di copertura e parete di fondo.

![Il configuratore di balconi: gli stili, la forma e la pianta](shots/balcony-configurator.webp)

![I suoi appoggi, sopra una sezione del balcone](shots/balcony-configurator-section.webp)

**Apply** cambia il balcone in un passo. **Save as style…** lo tiene come stile del progetto (un tipo di balcone), offerto dalla barra dello strumento e dalle proprietà; **Update style** lo salva di nuovo nel suo stile e ogni balcone dello stile lo riprende; accanto al nome di uno stile lo si rinomina o lo si elimina (i suoi balconi restano come sono). Uno stile è un tipo: le sue voci di costo (Cost → Map articles) misurano i suoi balconi.

![Le proprietà di un balcone: il suo stile e i campi del suo tipo](shots/balcony-properties.webp)

Nelle proprietà un balcone mostra solo i campi che il suo tipo usa. Le sue maniglie lo fanno scorrere lungo il muro, il centro del fronte fissa la profondità (per una loggia quella dell’incasso), i centri dei lati la larghezza; uno disegnato si trascina dagli angoli. L’incasso e la parete di fondo di una loggia la seguono e se ne vanno con lei.

**Stack on storeys above** (nel configuratore, nelle proprietà o **Architecture → Stack on storeys above**) copia i balconi selezionati su ogni piano sopra il loro in un passo, con Copy to storeys: la copia si appende al muro del suo piano che sta dove sta il suo muro, una loggia impilata taglia il muro di quel piano, i pilastri poggiano sul balcone sotto.

![Il fronte sud in 3D: il balcone alla francese, i balconi impilati su pilastri, quello d’angolo e quello appeso ai tiranti](shots/balconies-3d.webp)

![Da nord: la loggia tagliata nel muro, il balcone curvo con il parapetto e quello arrotondato](shots/balconies-3d-north.webp)

**Architecture → Balcony schedule** elenca ogni balcone con piano, stile, tipo, larghezza, profondità, area, lunghezza e modello del parapetto, appoggio e taglio termico, con i totali. In IFC un balcone è un IfcElementAssembly (USERDEFINED, "Balcony") di un IfcElementAssemblyType, fatto delle sue parti: la soletta un IfcSlab (FLOOR), finitura e intradosso IfcCovering (FLOORING, CEILING), il parapetto un IfcRailing (GUARDRAIL) o il parapetto pieno un IfcWall (PARAPET), pilastri IfcColumn, mensole IfcDiscreteAccessory (BRACKET), tiranti IfcMember, il taglio termico un IfcBuildingElementPart; l’incasso di una loggia è un IfcOpeningElement nel suo muro. Lapin rilegge i suoi balconi come balconi.
