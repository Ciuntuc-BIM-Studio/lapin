---
title: Preventivo dei costi
order: 98
summary: Il preventivo dal modello: i tuoi listini importati da CSV o Excel, voci sui tipi e regole sui materiali, le quantità del computo per i prezzi unitari suddivisi in materiale, manodopera, attrezzature e trasporto, spese generali, utile e IVA, in RON o EUR; i moduli rumeni F3, F2 e da C6 a C9 su tavole, in Excel e PDF; i cost schedule di IFC.
---

## La scheda Esecuzione

![La scheda Esecuzione](shots/ribbon-cost.webp)

I gruppi dei costi sono nella scheda **Esecuzione**, dopo quelli del programma lavori (la scheda si chiamava **Quantità**, poi **Costi**). Mantengono le quantità e aggiungono il preventivo:

- **Computo delle quantità**, **Abachi…** ed **Esporta XLSX**: le quantità del modello (vedi [Abachi e grafici](schedules.md)).
- **Listini prezzi…**: i tuoi prezzi, importati da un file CSV o Excel (più sotto).
- **Assegna voci…**: quale voce prezza ogni tipo e ogni materiale (più sotto).
- **Preventivo…**: apre il preventivo sotto la vista (la prima volta viene creato).
- **Modulo su tavola**, **Preventivo XLSX** e **Preventivo PDF**: il preventivo e i suoi moduli su una tavola, in una cartella di lavoro o in un PDF (più sotto).
- **Costo nel tempo**: il preventivo posato sul programma lavori, la curva a S e la tabella mensile (sotto).

Il preventivo è calcolato dal modello a ogni sua modifica. Un muro che disegni ora entra subito nel preventivo, con le sue quantità. Si salvano solo i prezzi, le voci sui tipi, le regole e le tue righe.

## Listini prezzi

![Importare un listino: il significato di ogni colonna dedotto dalla sua intestazione](shots/price-import.webp)

I prezzi sono tuoi: Lapin non fornisce alcun prezzario né norme. **Esecuzione → Listini prezzi… → Importa…** legge un file CSV o XLSX, per esempio un'esportazione dal tuo programma di computo o il listino di un fornitore. Lapin mostra le prime righe del file sotto le loro intestazioni. Sopra ogni colonna c'è ciò che Lapin ha dedotto che contenga, dall'intestazione, in inglese o in rumeno:

- **Codice** e **Descrizione** (*Cod*, *Simbol*, *Denumire*), **Unità** (*U.M.*: m³ o mc, m² o mp, m, pcs o buc, kg, t, 100 m²).
- Il prezzo unitario suddiviso in **Materiale**, **Manodopera**, **Attrezzature** e **Trasporto** (*Material*, *Manoperă*, *Utilaj*, *Transport*), oppure un solo **Prezzo unitario (non suddiviso)**, che conta allora come materiale.
- Facoltativi: **Ore-uomo per unità** (*Normă de timp*) e **Mestiere** (*Meserie*), per l'elenco della manodopera (C6).
- Facoltative: le risorse della voce, righe che nominano la voce a cui appartengono (**Voce a cui appartiene**), il loro **Tipo di risorsa** (M, Man, U, T) e il loro **Consumo per unità**. Una voce data solo dalle sue risorse è prezzata da esse, e gli elenchi delle risorse (da C7 a C9) le elencano una per una.

Cambia ogni colonna dedotta in modo sbagliato, scegli la riga delle intestazioni e se i prezzi sono in RON o EUR, poi fai clic su **Importa**. Il listino entra nel progetto. Con **Conserva anche nella libreria di questo computer** spuntato, resta anche su questo computer: sotto **Su questo computer**, **Aggiungi al progetto** lo porta nel tuo prossimo progetto.

**Modello** salva un CSV con le colonne e alcune righe dei valori approssimativi di Lapin, segnati come *indicativi*: completalo con i tuoi prezzi e importalo. **Usa i valori indicativi** mette quei valori approssimativi nel progetto come listino, così puoi provare il preventivo prima di avere i tuoi. Sostituiscili prima di mandare un preventivo a chiunque.

## Voci sui tipi, regole sui materiali

![Assegna voci: ogni tipo e materiale con la sua voce](shots/map-articles.webp)

Una riga del preventivo viene da una **voce**: un codice del tuo listino. Ci sono due modi per dare una voce agli elementi:

- **Su un tipo.** Ogni elemento del tipo è prezzato da essa. La voce dice cosa misura: l'elemento intero o una sua parte (il **nucleo** di un tipo stratificato, o le sue **finiture**), e con quale misura (secondo l'unità del prezzo, o volume netto o lordo, area netta o lorda, lunghezza, numero, peso). Può anche aggiungere un fattore e lo sfrido (%).
- **Su un materiale (una regola).** Ogni strato di quel materiale, in qualsiasi tipo, è prezzato da essa: una sola regola per l'intonaco di calce prezza l'intonaco di tutti i tuoi muri.

La voce del tipo viene prima. Uno strato già misurato dalle voci del suo tipo non è prezzato di nuovo da una regola. Una voce sull'elemento intero lo prezza tutto, quindi le regole si fanno da parte. Per un muro stratificato, dai al tipo una voce sul suo **nucleo** (la muratura a volume) e regole per le sue finiture (l'intonaco ad area). Il muro è allora prezzato strato per strato: la muratura sotto **Muri**, l'intonaco sotto **Finiture e isolamenti**.

**Esecuzione → Assegna voci…** elenca i tipi del progetto usati dagli elementi e i materiali dei loro strati. Ogni riga parte con la voce che ha, oppure quella che hai scelto per un tipo o un materiale con lo stesso nome in un progetto precedente, oppure quella che Lapin suggerisce: un codice scritto nel suo nome, o la voce che ha più parole in comune con esso (*zidărie*, *BCA*, *tencuială*, *C25/30*). Per un tipo stratificato propone il nucleo. Scrivi o scegli un codice, scegli la parte e la misura, spunta le righe che vuoi e fai clic su **Applica**: un solo passo da annullare. Le tue scelte sono ricordate su questo computer per i prossimi progetti.

I tipi di casseforme e ponteggi delle opere provvisionali (la scheda **Sito**) avevano già un codice voce (decisione 152): è una voce sul tipo, misurata secondo l'unità del prezzo.

Le fasi contano: un elemento che c'era prima dei lavori (creato in una fase *esistente*) non viene prezzato. Uno demolito viene prezzato solo dalla voce del suo tipo la cui parte è **La sua demolizione (solo se demolito)**, nel capitolo **Demolizione**.

## Il preventivo

![Il preventivo: capitoli, righe, aliquote e totali](shots/estimate.webp)

**Esecuzione → Preventivo…** apre il preventivo sotto la vista, come il Gantt. Le sue righe sono raggruppate in **capitoli** per categoria di lavori: scavi, fondazioni, struttura, muri, copertura, porte e finestre, finiture e isolamenti, impianti, opere provvisionali, altro. Una voce può indicare un capitolo proprio. **Per piano e categoria di lavori** mette il piano prima di ogni capitolo.

Ogni riga mostra il codice, la descrizione e l'unità dal listino, la **quantità** dal modello (la stessa del computo), il prezzo unitario suddiviso in **Materiale**, **Manodopera**, **Attrezzature** e **Trasporto**, il prezzo unitario e il **valore**. Fai clic su una riga per selezionarne gli elementi nelle viste. Seleziona elementi in una vista, e le loro righe sono evidenziate nel preventivo.

Sotto le righe ci sono i totali, disposti come nei preventivi rumeni:

1. **Costi diretti**: la somma delle righe.
2. **Contributi sulla manodopera (CAM)**: il 2,25 % della manodopera, come predefinito.
3. **Spese generali** (*cheltuieli indirecte*): il 10 % dei costi diretti con i contributi, come predefinito.
4. **Utile**: il 5 % dei costi diretti e delle spese generali, come predefinito.
5. **Totale senza IVA**, l'**IVA** (21 % come predefinito) e il **Totale con IVA**.

Cambia qualsiasi aliquota nella barra. Ogni valore è arrotondato a due decimali, come lo stampa un preventivo.

Il preventivo è in **RON** o in **EUR**. **1 EUR =** è il cambio del progetto: un listino nell'altra valuta è convertito con esso, e la casella accanto mostra ogni valore anche nell'altra valuta.

La barra avverte anche di ciò che non ha prezzo: **N tipo/i senza prezzo** (fai clic per aprire Assegna voci) e i codici che nessun listino ha. **Una riga tua** aggiunge un lavoro che il modello non ha (la recinzione di cantiere, un accantonamento): scrivi il suo codice, la descrizione, l'unità, la quantità e i prezzi. Una riga con un codice e senza prezzi li prende dal listino.

## I moduli rumeni

![L'elenco delle quantità (F3) dello stesso preventivo](shots/estimate-f3.webp)

L'elenco sopra il preventivo mostra il preventivo stesso o uno dei suoi moduli. Sono raggruppamenti dello stesso preventivo, quindi danno gli stessi totali. Hanno le intestazioni in rumeno, come si presentano in Romania:

- **Elenco delle quantità di lavori (F3)**: *Lista cu cantitățile de lucrări pe categorii de lucrări*, capitolo per capitolo, i prezzi unitari e i valori suddivisi in materiale, manodopera, attrezzature e trasporto, con i totali sotto.
- **Preventivo per oggetto (F2)**: *Deviz pe obiect*, il valore di ogni capitolo senza IVA, la sua IVA e il suo valore con IVA.
- **Manodopera (C6)**: le ore e il valore per mestiere (dal mestiere e dalle ore-uomo del listino, o dalle sue risorse di manodopera).
- **Materiali (C7)**, **Attrezzature (C8)** e **Trasporti (C9)**: le risorse con le loro quantità, prezzi unitari e valori. Una voce senza risorse conta come una risorsa a sé.

I moduli seguono la pratica comune secondo la metodologia dell'Ordine 863/2008. Verificali con ciò che chiede il tuo committente o il programma di finanziamento: chiedici se ti serve un'altra impaginazione.

**Metti su tavola** mette la tabella mostrata su una nuova tavola, pronta da stampare con le altre. **XLSX** salva il preventivo e ogni modulo in una sola cartella di lavoro, un foglio ciascuno. **PDF** li salva come PDF, A4 orizzontale, pronto da stampare.

## Costo nel tempo

![Costo nel tempo: la curva a S sulle barre dei mesi, la tabella mensile sotto](shots/cost-time.webp)

Con un programma lavori nel progetto (la scheda **Esecuzione**), il preventivo vi viene posato senza alcun collegamento proprio. Il valore di ogni voce si divide tra i suoi elementi, e la parte di ogni elemento va alle attività che costruiscono la parte misurata dalla voce: la voce della muratura all'attività che prende il nucleo dei muri, quella dell'intonaco a quella che prende le finiture. Una voce sull'elemento intero, costruito in due attività, si divide in parti uguali. Una voce propria va all'attività scelta accanto alla descrizione (**In nessuna attività** per impostazione predefinita). Il costo di un'attività si distribuisce in parti uguali sui suoi giorni lavorativi: nulla si spende in un fine settimana, in un festivo o, per un'attività umida, nella pausa invernale.

**Esecuzione → Costo nel tempo** apre, sotto la vista:

- la **curva a S**: il costo cumulato pianificato sul programma, quello della **linea di base** (tratteggiato) quando ne è salvata una, e il **lavoro fatto** (spesso) fino alla data di stato: il costo di ogni attività per la sua % completata, distribuito dal suo inizio effettivo;
- le barre dei mesi, sulla scala di destra: pianificato e fatto;
- sopra, il **budget a completamento** (i costi diretti del preventivo), il **costo a oggi** e ciò che è **non programmato**, se c'è: voci i cui elementi nessuna attività costruisce (un clic apre il preventivo);
- sotto, la **tabella mensile**: il costo di ogni capitolo al mese, il non programmato e il suo totale.

I valori sono costi diretti, come le voci del preventivo. Il Gantt mostra il costo di ogni attività nella colonna **Costo** (vedi [Pianificazione](planning.md)).

![La curva a S su una tavola](shots/cost-curve-sheet.webp)

**Metti su tavola** mette la curva su una nuova tavola, disegnata in vettori con la sua legenda. **Tabella mensile su tavola** posa la tabella mensile, che è anche il modulo **Costo per mese** nell'elenco del preventivo: i capitoli mese per mese, poi i costi diretti, i totali senza IVA e con IVA (ripartiti in proporzione), i costi cumulati e, con l'avanzamento, il lavoro fatto. Ogni riga dà il totale del preventivo. **XLSX** salva la curva giorno per giorno, la tabella mensile e i costi delle attività in una cartella di lavoro; **PDF** salva la curva e la tabella mensile.

**Gestisci → Versioni → Confronta** dice, accanto agli elementi e al programma, cosa è cambiato nel preventivo: il totale con IVA, i costi diretti e la differenza, ogni capitolo il cui costo è cambiato, e le voci aggiunte, tolte o cambiate (quantità e valore).

Gli stati di avanzamento mensili (*situații de lucrări*), il valore guadagnato e più linee di base sono dell'edizione premium.

## IFC

**File → Esporta IFC…** scrive il preventivo come **IfcCostSchedule**: i suoi capitoli e le righe come **IfcCostItem**, la quantità di ogni riga e i suoi prezzi unitari come quattro **IfcCostValue** (Material, Labour, Equipment, Transport), gli elementi che ogni riga misura assegnati a essa (**IfcRelAssignsToControl**), la valuta come **IfcMonetaryUnit** del progetto. Importato, un preventivo mantiene le righe come le ha il file, con gli stessi totali. Spunta **Righe dal modello** per aggiungere le righe che dà il tuo modello.

Con un programma lavori, ogni voce è assegnata anche alle attività in cui si esegue (**IfcRelAssignsToControl** verso le loro **IfcTask**). Importate, le voci mantengono le loro attività, quindi la curva a S torna uguale.
