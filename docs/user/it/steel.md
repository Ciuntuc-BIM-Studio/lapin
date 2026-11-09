---
title: Acciaio
order: 56
summary: Aste in acciaio con estremità configurabili — prolungate o arretrate, tagliate secondo un piano, a quartabuono, adattate a un’altra asta, intagliate — fatte anche da un contorno disegnato; profili cavi e formati a freddo; capannoni a portali con squadrette.
---

## La scheda Acciaio

![La scheda Acciaio](shots/ribbon-steel.webp)

- **Capannone a portali…**: un intero capannone in acciaio da pochi numeri, nel configuratore di capannoni (più sotto).
- **Asta** (<kbd>SM</kbd>): un’asta in acciaio tra due punti, alla quota superiore del piano, con il lembo superiore della sezione sull’asse, come una trave. Il suo tipo si sceglie in Proprietà prima del primo clic, o dalla scheda **Catalogo**, dove ogni tipo in acciaio è disegnato con la sua sezione.
- **Converti in asta**: i contorni disegnati selezionati diventano aste (più sotto).
- **A quartabuono** e **Adatta**: due aste unite alle estremità (più sotto).
- **Tipi in acciaio**: i tipi di asta del progetto, e altri nuovi.

Un’asta è un IfcMember: il suo asse va ovunque nello spazio (un controvento, un puntone, un montante), la sua sezione viene dal suo tipo, si può ruotare attorno all’asse (**Rotazione**) e l’asse può passare per il centro, il lembo superiore o quello inferiore della sezione (**Punto di inserimento**). Le travi e i pilastri della struttura di profilo in acciaio ricevono le stesse estremità.

## Estremità

Seleziona un’asta, o una trave o un pilastro diritto di profilo in acciaio: la sua sezione **Acciaio** in Proprietà mostra le due estremità, **Inizio** e **Fine**. Ogni estremità è un insieme di regole; il corpo le segue.

![Le estremità di un’asta in Proprietà](shots/steel-ends.webp)

- **Prolungamento**: quanto l’estremità supera il suo punto; negativo, l’estremità è arretrata (un gioco di montaggio).
- **Taglio**: **Perpendicolare all’asse**; **Secondo un piano**, ruotato dal taglio retto **In pianta** (attorno alla verticale della sezione) e **In prospetto** (attorno al suo lato), in gradi; **A quartabuono** con un’altra asta, sul piano che divide a metà l’angolo tra loro; **Adattata a un’asta**, sulla faccia dell’altra asta a cui arriva (l’ala di un pilastro, o il lato della sua anima quando il pilastro è ruotato).
- **Intaglio**: le ali intagliate in posizione **Superiore** e/o **Inferiore**, per la **Lunghezza dell’intaglio** a partire dall’estremità e la **Profondità dell’intaglio** dal bordo della sezione, come per una trave secondaria collegata a una principale.

Il quartabuono e l’adattamento nominano l’altra asta: spostala, e l’estremità la segue; eliminala, e l’estremità torna dritta e lo dice. **A quartabuono** nella scheda unisce a quartabuono le due aste selezionate nell’angolo dove si incontrano; **Adatta** adatta la prima selezionata alla seconda.

## Da uno schizzo a un’asta

Disegna il contorno dell’asta come polilinea chiusa o rettangolo (**Disegno**), poi **Acciaio → Converti in asta** con uno o più contorni selezionati. In un solo passo ogni contorno viene letto e sostituito da un’asta:

![Due contorni schizzati in pianta](shots/steel-sketch.webp)

- i suoi **due lati lunghi paralleli** (entro mezzo grado) danno l’asse, la loro linea mediana da un capo all’altro, e la **larghezza** tra loro;
- il **lato a ogni estremità** dà il taglio di quell’estremità: retto, o secondo un piano con l’inclinazione del lato.

Su una **pianta** il contorno è l’asta vista dall’alto: la sua larghezza è quella delle ali, e un profilo di quella larghezza viene cercato tra i tipi di asta del progetto e nel catalogo (altrimenti il tipo scelto per Asta); l’asta sta alla quota superiore del piano. Su una **sezione** o un **prospetto** il contorno è l’asta vista di lato: la sua larghezza è l’altezza della sezione, un’estremità inclinata è un taglio in prospetto e un **gradino** ad angolo retto a un’estremità è un **intaglio** sopra o sotto; l’asta sta dove è disegnata. Un gradino disegnato in pianta viene rifiutato (un intaglio si vede di lato). Un contorno che non è una fascia (un triangolo, una L, lati non paralleli) resta com’è, e Lapin lo dice.

## Tipi in acciaio

**Acciaio → Tipi in acciaio** elenca i tipi di asta del progetto con il loro peso al metro e ne crea di nuovi: un profilo dal catalogo (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** e i generici formati a freddo **C**, **Z** e **Σ**) o una sezione cava o formata a freddo **con dimensioni proprie** (altezza, larghezza, spessore, labbro). Il nuovo tipo può essere un tipo di asta, o un **tipo di trave** o **tipo di pilastro** della struttura, così anche gli strumenti Trave e Pilastro posano acciaio. Un clic su un tipo dell’elenco lo rende quello che posa Asta.

![La finestra Tipi in acciaio](shots/steel-types.webp)

Ogni altro profilo viene dalle librerie: **Da una libreria di profili…** le cerca tutte (vedi [Librerie di profili](profiles.md)).

I profili cavi sono vuoti all’interno, nella vista 3D, nelle sezioni e nelle quantità. I profili formati a freddo sono forme generiche con dimensioni, a pieghe vive, senza catalogo del produttore.

![Aste in 3D](shots/steel-3d.webp)

## Capannoni a portali

**Acciaio → Capannone a portali…** apre il configuratore di capannoni. Come quello della copertura, ha gli stili a sinistra, i campi del capannone in schede al centro e, a destra, il capannone come Lapin lo costruirebbe: un portale tipo visto lungo il capannone, con le sue squadrette, e un riepilogo di quanto pesa.

![Il configuratore di capannoni](shots/steel-hall.webp)

- **Stili**: i capannoni tipici del catalogo (**Capannone 20 m**, **Capannone 30 m**, una **Tettoia 10 m** a una falda) e quelli del progetto. Uno stile scelto compila tutti i campi; ciascuno si può poi cambiare. **Salva come stile…** conserva il capannone così configurato come stile del progetto; **Aggiorna lo stile** lo salva di nuovo nel suo stile, e tutti i capannoni di quello stile lo seguono.
- **Portale**: la **Luce** (da asse di pilastro ad asse di pilastro), le **Campate** (uguali come `5 × 6`, o ciascuna: `6, 6, 7.5`), l’**Altezza in gronda** (l’estradosso dei traversi sopra la faccia esterna del pilastro), la **Copertura** (a due falde o a una falda) e la sua **Pendenza**, i profili di **Pilastri** e **Traversi**.
- **Squadrette**: la **Squadretta in gronda** e la **Squadretta in colmo**, ciascuna con la sua **Lunghezza** lungo il traverso dalla faccia del pilastro (o dal colmo) e la sua **Altezza** sotto il traverso lì, e come è **Realizzata**: **tagliata dal profilo del traverso** (un I tagliato in obliquo lungo l’anima, una metà saldata sotto il traverso: l’anima e l’ala del traverso stesso) o **in piatti saldati** (un **Piatto d’anima** e un **Piatto d’ala** del loro spessore, e la larghezza dell’ala).

![Le squadrette in piatti saldati](shots/steel-hall-haunches.webp)

- **Testate**: **pilastri di testata sotto un traverso semplice** (pilastri d’angolo e pilastri all’**Interasse** scelto, con l’asse forte perpendicolare alla parete di testata, sotto un traverso più leggero che arriva al filo esterno delle pareti, i pilastri adattati al suo intradosso), oppure **un portale corrente** a ogni estremità.

![Una testata con pilastri](shots/steel-hall-gable.webp)

- **Acciaio secondario**: gli arcarecci, i correnti di parete, i controventi, la trave di gronda e i ritegni (sotto).
- **Posizionamento**: dove si incrociano l’asse A e l’asse 1, e la direzione della luce in pianta (**Rotazione**); l’anteprima mostra la griglia del capannone dall’alto.

**Crea capannone** lo fa in un solo passo, che un annulla riporta indietro: la sua **griglia** (assi 1, 2… ai portali, A e B ai pilastri), e a ogni portale due **pilastri** (pilastri della struttura in profilo d’acciaio, con l’anima nel piano del portale e la testa tagliata secondo la pendenza) e due **traversi** a quartabuono al colmo (uno solo, dal pilastro basso a quello alto, con la copertura a una falda): aste in acciaio adattate alle facce dei pilastri, con le loro squadrette. Il capannone sta sul piano corrente; gronda e colmo sono altezze dei suoi portali, non piani nuovi. Il riepilogo dà i portali, le altezze e l’acciaio per tipo: pilastri, traversi, squadrette, pilastri di testata, in totale e per m² di pianta.

![Il capannone in 3D](shots/steel-hall-3d.webp)

Il capannone resta modificabile. Seleziona una delle sue aste: la sua sezione **Acciaio** mostra il capannone e **Modifica capannone…**, che apre il configuratore su di esso; **Applica** ricostruisce il capannone sulle stesse aste, che mantengono la loro identità (un’asta non più necessaria sparisce, una nuova si aggiunge). Lì sono elencate anche le squadrette del traverso, ciascuna cambiata da sola (tipo, lunghezza, altezza, piatti): una squadretta impostata così resta com’è quando il capannone viene ricostruito; **×** la toglie.

![Le squadrette di un traverso nelle Proprietà](shots/steel-hall-props.webp)

## Collegamenti

I portali di un capannone arrivano con i loro **collegamenti** (Steel → Portal frame hall…, scheda **Nodi**): un **piede di pilastro** sotto ogni pilastro e ritto di testata, un **collegamento di gronda** dove ogni traverso arriva al suo pilastro, un **collegamento di colmo** dove si incontrano due traversi e una piastra d'anima sotto il traverso sopra ogni ritto. Ognuno è un elemento a sé che sa quali aste collega; le sue piastre, i bulloni e i tirafondi sono fatti dai suoi parametri e dalle aste come sono, e le seguono quando il capannone cambia.

![La scheda Nodi](shots/steel-nodes-hall.webp)

- **Collegamenti**: attivi o no per tutto il capannone.
- **Basi dei pilastri**: **incernierate** (una piastra di base, due tirafondi ai lati dell'anima o quattro tra le ali) o **incastrate** (una piastra più lunga e spessa, da quattro a otto tirafondi fuori dalle ali, nervature nel piano dell'anima). I **Tirafondi** sono **dritti**, **con uncino** o **con piastra di ancoraggio** al piede; il **Numero di tirafondi** e la **Lunghezza dei tirafondi** (sotto l'estradosso del calcestruzzo) li sceglie il profilo quando restano vuoti. Se sotto il pilastro c'è una fondazione della struttura, i tirafondi vi entrano (e si fermano 50 mm sopra il suo fondo); altrimenti scendono della loro lunghezza sotto la malta.
- **Collegamento di gronda**: una piastra di testa estesa sul traverso imbullonata all'ala del pilastro: il suo **Spessore della piastra**, il **Diametro dei bulloni** e la loro **Classe**, quanto sporge **Sopra il traverso** (con una fila di bulloni lì) e gli **Irrigidimenti del pilastro** in corrispondenza dell'ala superiore del traverso e dell'ala della squadretta.
- **Collegamento di colmo**: le piastre di testa dei due traversi imbullonate tra loro sul taglio a quartabuono, con le loro squadrette.

Ciò che resta vuoto è scelto dai profili: bulloni da M16 a M27 secondo l'altezza dell'asta imbullonata, una piastra spessa quanto i suoi bulloni, distanze dai bordi e passi dai fori. Un collegamento arretra le estremità delle sue aste di ciò che mette tra loro (il traverso della piastra di testa, il pilastro della piastra di base, il ritto di un gioco di 20 mm), tramite le regole d'estremità delle aste stesse.

![Un collegamento di gronda](shots/steel-nodes-eaves.webp)

![Un piede di pilastro incastrato su tirafondi con uncino](shots/steel-nodes-base.webp)

Seleziona un collegamento per vederlo nelle Proprietà: le aste che collega, ogni parametro com'è ora (una misura scelta dal profilo è mostrata come tale) e i suoi pezzi: piastre, bulloni e tirafondi per misura e classe, le saldature per sezione di gola con la loro lunghezza, il peso, la fondazione in cui vanno i tirafondi. Un parametro cambiato lì vale solo per quel collegamento, e il capannone ricostruito lo conserva. **Modifica capannone…** apre il suo capannone.

![Un collegamento nelle Proprietà](shots/steel-nodes-props.webp)

Le aste disegnate a mano ricevono i loro collegamenti con **Steel → Trova collegamenti**: un piede sotto ogni asta verticale, una gronda dove un'asta è adattata a un pilastro, quello di un ritto dove un pilastro è adattato sotto un'asta, un colmo dove due aste sono tagliate a quartabuono tra loro, un giunto dove due proseguono in linea, testa a testa; per le aste selezionate, o tutto il modello quando nulla è selezionato. **Steel → Pezzi in acciaio** crea l'abaco di piastre, bulloni, tirafondi e saldature dei collegamenti. Un collegamento eliminato restituisce alle sue aste le loro estremità; un'asta eliminata si porta via i suoi collegamenti; **Elimina capannone** nel configuratore toglie il capannone con aste, collegamenti e assi, in un solo passo.

## Acciaio secondario

Un capannone viene con il suo **acciaio secondario** (Steel → Portal frame hall…, scheda **Acciaio secondario**), fatto con lui e rifatto con lui quando cambia, ogni asta mantenendo la sua identità:

![La scheda Acciaio secondario](shots/steel-secondary-hall.webp)

- **Acciaio secondario**: attivo o no per tutto il capannone.
- **Arcarecci**: il loro **Profilo** (Z, C e Σ formati a freddo, un U o un I leggero), l’**Interasse degli arcarecci** lungo la falda, quanto vanno **Oltre le testate** e le loro **Lunghezze**: **una per campata** (da portale a portale) o **continua** (tutta la lunghezza del capannone). Poggiano sulla faccia superiore dei puntoni, l’anima perpendicolare alla copertura, da 150 mm all’interno della faccia dei pilastri fino a 200 mm dal colmo, in tante file quante ne chiede l’interasse.
- **Correnti di parete**: il loro **Profilo** e l’**Interasse dei correnti di parete**, dal **Primo corrente a** fin sotto la gronda, uno per campata sulla faccia esterna dei pilastri, l’anima orizzontale e i risvolti in basso; sulle testate, da pilastro a pilastro (o tra i pilastri di un portale di testata), tanto in alto quanto lo permette il puntone sopra. Una **Trave di gronda** prende la sommità delle pareti lunghe (con il suo **Profilo**), i correnti un interasse più in basso.
- **Controventi**: le **Campate controventate** (i loro numeri, dalla prima), il **Profilo** delle aste (tondi da Ø16 a Ø30, angolari o tubi) e dove: **In copertura** (una X nel piano della copertura, sotto la piattabanda superiore dei puntoni, in pannelli lunghi circa quanto la campata) e **Nelle pareti lunghe** (una X nel piano delle pareti, per gli assi dei pilastri, dall’altezza del primo corrente fin sotto la squadretta).
- **Ritegni**: da un arcareccio e un corrente su due fino alla piattabanda inferiore del puntone e a quella interna del pilastro, da entrambi i lati, fuori dalle squadrette.

![L’acciaio secondario in 3D](shots/steel-secondary-3d.webp)

Ogni arcareccio e corrente poggia su una **staffa** su ogni puntone, pilastro o pilastro di testata che attraversa: una piastra saldata sulla faccia dell’asta, accanto all’anima dell’arcareccio (fuori dall’anima di una C, dal lato di una Z opposto alla sua piattabanda inferiore), bullonata attraverso l’anima, due bulloni per estremità (M12 fino a 250 mm di altezza, M16 oltre). Un pilastro d’angolo più stretto dei pilastri riceve una staffa più alta, fino al corrente. Ogni X poggia su **fazzoletti** saldati all’anima dei puntoni o dei pilastri, una diagonale per faccia; un angolare o un tubo vi è bullonato con due bulloni, l’estremità filettata di un tondo prende il suo dado. Un ritegno è bullonato alle due estremità. Sono collegamenti come quelli dei portali: selezionane uno per vedere la sua piastra e i suoi bulloni nelle Proprietà e cambiarli lì. Il riepilogo del configuratore conta e pesa arcarecci, correnti, trave di gronda, controventi e ritegni; l’abaco **Steel parts** ne riporta staffe, fazzoletti e bulloni.

![Un arcareccio sulla sua staffa, un ritegno sotto](shots/steel-secondary-cleat.webp)

![Il controvento di copertura sui suoi fazzoletti](shots/steel-secondary-bracing.webp)

## Abachi e disegni

**Steel → Steel members** crea l’abaco dell’acciaio del progetto: le aste, e le travi e i pilastri di un profilo in acciaio, per capannone (quelli di nessun capannone alla fine), una **Marca** per le aste di un tipo uguali per profilo, lunghezza e tagli d’estremità (C pilastri, R puntoni, GP pilastri di testata, P arcarecci, SR correnti di parete, EB travi di gronda, B controventi, FB ritegni, M altre aste, BM travi), numerate dal profilo più pesante e dalla più lunga. Una riga dà il **Profilo**, la **Lunghezza**, il **Taglio iniziale** e il **Taglio finale** (di quanto il piano dell’estremità ruota dal taglio retto, e un intaglio: gli angoli della distinta di taglio), il **Numero**, il peso al metro, **Cad.** e totale, e la superficie di **Verniciatura**; un totale sotto ogni capannone e uno generale. **Steel parts** elenca piastre, bulloni, tirafondi e saldature dei collegamenti, con staffe e fazzoletti.

![L’abaco delle aste in acciaio](shots/steel-members.webp)

**Steel → Anchor plan** disegna le basi dei pilastri del capannone per le fondazioni: una pianta del piano del capannone in scala 1:50, tagliata appena sopra le piastre di base, ogni tirafondo cerchiato, la sua distanza dall’asse (± quando i tirafondi sono simmetrici rispetto ad esso), ogni base marcata per tipo (**B1**, **B2**…: stessa piastra, tirafondi, classe e malta), le campate e la luce della griglia, e la tabella dei tipi accanto al capannone: numero, tipo, piastra, tirafondi (numero, dimensione, forma), classe e malta. **Steel → Frame elevation** disegna un portale (quello tipico, o quello dell’asta selezionata) visto lungo il capannone in scala 1:50, con la luce e le altezze di gronda e di colmo. Entrambi sono viste del capannone (richieste di nuovo, si apre la stessa) e vanno sulle tavole come le altre; seguono il capannone quando cambia.

![Una pianta dei tirafondi](shots/steel-anchor-plan.webp)

![Il prospetto di un portale](shots/steel-frame-view.webp)

## IFC e quantità

Un’asta si esporta come **IfcMember** con il suo **IfcMemberType**. Il suo corpo è scritto come gli altri programmi scrivono l’acciaio (Tekla, Advance Steel, Revit): la sezione **estrusa** (IfcExtrudedAreaSolid), **tagliata** dai piani delle estremità (IfcBooleanClippingResult con IfcHalfSpaceSolid), e ogni intaglio un **IfcOpeningElement** che la svuota. Una trave o un pilastro in acciaio con estremità si scrive allo stesso modo. Le quantità di ogni asta danno la sua lunghezza tra i tagli, l’area della sezione, il volume e il peso netto (7850 kg/m³).

Un capannone si esporta come **IfcBuildingSystem** (LOADBEARING) a cui sono assegnate le sue aste, con i suoi dati nel set di proprietà `Lapin_SteelHall` e la sua **IfcGrid**. Un traverso con squadrette è un **IfcElementAssembly** del traverso e delle sue squadrette: una squadretta tagliata dal profilo è un **IfcMember**, una in piatti saldati due **IfcPlate**, ciascuna estrusa (IfcExtrudedAreaSolid). Le squadrette non entrano nelle quantità del traverso; il riepilogo del configuratore le pesa. Gli arcarecci si esportano come **IfcMember** PURLIN, i controventi e i ritegni come BRACE, i correnti di parete e la trave di gronda come USERDEFINED (**Side rail**, **Eaves beam**), tutti assegnati al sistema del capannone con i loro collegamenti.

Un collegamento si esporta come **IfcElementAssembly** delle sue piastre (**IfcPlate**), bulloni (**IfcMechanicalFastener** BOLT), tirafondi (ANCHORBOLT) e piastre di ancoraggio (**IfcDiscreteAccessory** ANCHORPLATE), ognuno con misura e classe in `Lapin_SteelPart`; realizza il collegamento delle aste che unisce (**IfcRelConnectsWithRealizingElements**; un piede, tra la fondazione e il suo pilastro). Le saldature restano dati, non corpi.
