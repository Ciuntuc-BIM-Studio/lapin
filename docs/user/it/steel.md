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

I profili cavi sono vuoti all’interno, nella vista 3D, nelle sezioni e nelle quantità. I profili formati a freddo sono forme generiche con dimensioni, a pieghe vive, senza catalogo del produttore.

![Aste in 3D](shots/steel-3d.webp)

## Capannoni a portali

**Acciaio → Capannone a portali…** apre il configuratore di capannoni. Come quello della copertura, ha gli stili a sinistra, i campi del capannone in schede al centro e, a destra, il capannone come Lapin lo costruirebbe: un portale tipo visto lungo il capannone, con le sue squadrette, e un riepilogo di quanto pesa.

![Il configuratore di capannoni](shots/steel-hall.webp)

- **Stili**: i capannoni tipici del catalogo (**Capannone 20 m**, **Capannone 30 m**, una **Tettoia 10 m** a una falda) e quelli del progetto. Uno stile scelto compila tutti i campi; ciascuno si può poi cambiare. **Salva come stile…** conserva il capannone così configurato come stile del progetto; **Aggiorna lo stile** lo salva di nuovo nel suo stile, e tutti i capannoni di quello stile lo seguono.
- **Portale**: la **Luce** (da asse di pilastro ad asse di pilastro), le **Campate** (uguali come `5 × 6`, o ciascuna: `6, 6, 7.5`), l’**Altezza in gronda** (l’estradosso dei traversi sopra la faccia esterna del pilastro), la **Copertura** (a due falde o a una falda) e la sua **Pendenza**, i profili di **Pilastri** e **Traversi**, e le **Basi dei pilastri** (incernierate o incastrate, conservate per i nodi a venire).
- **Squadrette**: la **Squadretta in gronda** e la **Squadretta in colmo**, ciascuna con la sua **Lunghezza** lungo il traverso dalla faccia del pilastro (o dal colmo) e la sua **Altezza** sotto il traverso lì, e come è **Realizzata**: **tagliata dal profilo del traverso** (un I tagliato in obliquo lungo l’anima, una metà saldata sotto il traverso: l’anima e l’ala del traverso stesso) o **in piatti saldati** (un **Piatto d’anima** e un **Piatto d’ala** del loro spessore, e la larghezza dell’ala).

![Le squadrette in piatti saldati](shots/steel-hall-haunches.webp)

- **Testate**: **pilastri di testata sotto un traverso semplice** (pilastri d’angolo e pilastri all’**Interasse** scelto, con l’asse forte perpendicolare alla parete di testata, sotto un traverso più leggero che arriva al filo esterno delle pareti, i pilastri adattati al suo intradosso), oppure **un portale corrente** a ogni estremità.

![Una testata con pilastri](shots/steel-hall-gable.webp)

- **Acciaio secondario**: l’**Interasse degli arcarecci**, l’**Interasse dei correnti di parete**, le **Campate controventate** (i loro numeri, dalla prima) e una **Trave di gronda**. Sono conservati con il capannone per arcarecci, correnti e controventi a venire; non sono ancora costruiti.
- **Posizionamento**: dove si incrociano l’asse A e l’asse 1, e la direzione della luce in pianta (**Rotazione**); l’anteprima mostra la griglia del capannone dall’alto.

**Crea capannone** lo fa in un solo passo, che un annulla riporta indietro: la sua **griglia** (assi 1, 2… ai portali, A e B ai pilastri), e a ogni portale due **pilastri** (pilastri della struttura in profilo d’acciaio, con l’anima nel piano del portale e la testa tagliata secondo la pendenza) e due **traversi** a quartabuono al colmo (uno solo, dal pilastro basso a quello alto, con la copertura a una falda): aste in acciaio adattate alle facce dei pilastri, con le loro squadrette. Il capannone sta sul piano corrente; gronda e colmo sono altezze dei suoi portali, non piani nuovi. Il riepilogo dà i portali, le altezze e l’acciaio per tipo: pilastri, traversi, squadrette, pilastri di testata, in totale e per m² di pianta.

![Il capannone in 3D](shots/steel-hall-3d.webp)

Il capannone resta modificabile. Seleziona una delle sue aste: la sua sezione **Acciaio** mostra il capannone e **Modifica capannone…**, che apre il configuratore su di esso; **Applica** ricostruisce il capannone sulle stesse aste, che mantengono la loro identità (un’asta non più necessaria sparisce, una nuova si aggiunge). Lì sono elencate anche le squadrette del traverso, ciascuna cambiata da sola (tipo, lunghezza, altezza, piatti): una squadretta impostata così resta com’è quando il capannone viene ricostruito; **×** la toglie.

![Le squadrette di un traverso nelle Proprietà](shots/steel-hall-props.webp)

## IFC e quantità

Un’asta si esporta come **IfcMember** con il suo **IfcMemberType**. Il suo corpo è scritto come gli altri programmi scrivono l’acciaio (Tekla, Advance Steel, Revit): la sezione **estrusa** (IfcExtrudedAreaSolid), **tagliata** dai piani delle estremità (IfcBooleanClippingResult con IfcHalfSpaceSolid), e ogni intaglio un **IfcOpeningElement** che la svuota. Una trave o un pilastro in acciaio con estremità si scrive allo stesso modo. Le quantità di ogni asta danno la sua lunghezza tra i tagli, l’area della sezione, il volume e il peso netto (7850 kg/m³).

Un capannone si esporta come **IfcBuildingSystem** (LOADBEARING) a cui sono assegnate le sue aste, con i suoi dati nel set di proprietà `Lapin_SteelHall` e la sua **IfcGrid**. Un traverso con squadrette è un **IfcElementAssembly** del traverso e delle sue squadrette: una squadretta tagliata dal profilo è un **IfcMember**, una in piatti saldati due **IfcPlate**, ciascuna estrusa (IfcExtrudedAreaSolid). Le squadrette non entrano nelle quantità del traverso; il riepilogo del configuratore le pesa.
