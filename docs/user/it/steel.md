---
title: Acciaio
order: 56
summary: Aste in acciaio con estremità configurabili — prolungate o arretrate, tagliate secondo un piano, a quartabuono, adattate a un’altra asta, intagliate — fatte anche da un contorno disegnato; profili cavi e formati a freddo.
---

## La scheda Acciaio

![La scheda Acciaio](shots/ribbon-steel.webp)

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

## IFC e quantità

Un’asta si esporta come **IfcMember** con il suo **IfcMemberType**. Il suo corpo è scritto come gli altri programmi scrivono l’acciaio (Tekla, Advance Steel, Revit): la sezione **estrusa** (IfcExtrudedAreaSolid), **tagliata** dai piani delle estremità (IfcBooleanClippingResult con IfcHalfSpaceSolid), e ogni intaglio un **IfcOpeningElement** che la svuota. Una trave o un pilastro in acciaio con estremità si scrive allo stesso modo. Le quantità di ogni asta danno la sua lunghezza tra i tagli, l’area della sezione, il volume e il peso netto (7850 kg/m³).
