---
title: Carichi
order: 51
summary: Casi di carico, carichi di superficie, lineari e puntuali, i carichi variabili dei locali, i carichi derivati e le combinazioni dell'EN 1990, per un'analisi strutturale.
---

Il gruppo **Carichi** della scheda **Struttura** contiene i carichi che prende un'analisi strutturale: **Casi di carico…**, **Carico superficiale**, **Carico lineare** e **Carico puntuale**. I valori sono in kN, kN/m e kN/m².

![Il gruppo Carichi della scheda Struttura](shots/ribbon-loads.webp)

> I valori che Lapin fornisce (i carichi variabili dell'EN 1991-1-1 e i coefficienti dell'EN 1990, quelli raccomandati) sono **indicativi**. Inserisci i valori del tuo annesso nazionale e verificali prima di usare i risultati.

## Casi di carico

**Casi di carico…** apre la finestra Carichi. **Aggiungi i casi standard** crea G1 (peso proprio), G2 (finiture e tramezzi), Q-A (variabile, categoria A), S (neve) e W (vento).

![La scheda Casi della finestra Carichi](shots/loads-cases.webp)

Un caso ha un **Codice**, un **Nome**, un **Tipo** (Permanente, Variabile o Accidentale), una **Origine** (Peso proprio / carico permanente, Finiture, Variabile d'uso, Neve, Vento, Sisma o Definita dall'utente) e, per un caso di carico variabile, una **Categoria**. I tre campi ψ0, ψ1 e ψ2 sono i coefficienti di combinazione; lasciali vuoti per prendere quelli della categoria dalla tabella. Un valore che non è un numero da 0 a 1 viene segnalato e non viene conservato.

**Aggiungi i casi mancanti** crea un caso di carico variabile per ogni categoria usata dai locali che non ne ha uno.

## Inserire i carichi

- **Carico superficiale**: fai clic sugli angoli, poi premi <kbd>Enter</kbd>.
- **Carico lineare**: fai clic sulle sue due estremità.
- **Carico puntuale**: fai clic dove agisce.

Finché uno strumento è attivo, la barra della riga di comando offre il **valore** del carico e il **caso di carico** in cui va (per impostazione **Carico variabile (Q-A)**). Il carico va sul livello attivo.

![Un carico superficiale di 2,5 kN/m² disegnato nel soggiorno](shots/loads-plan.webp)

## Ciò che Lapin calcola per te

Questi non li disegni; Lapin li ricava dal modello a ogni richiesta, e seguono ogni modifica:

- Il **peso proprio** di solai, muri, pilastri e travi, dalle densità dei loro materiali, nel caso la cui origine è il peso proprio (G1).
- Le **finiture dei solai**, dai loro strati, e i **muri non portanti**, come carichi lineari, nel caso che elenca finiture e tramezzi (G2).
- I **locali**: il carico variabile della categoria di un locale sulla sua superficie, nel caso di quella categoria (Q-A, Q-B…).
- La **neve** e il **vento**, una volta che dai i valori del sito (vedi [Neve e vento](#neve-e-vento)).

Un materiale senza densità viene lasciato fuori, e il Riepilogo dice quale.

## Il carico variabile di un locale

Seleziona un locale: le sue proprietà hanno una sezione **Carico variabile**. La sua **Categoria** è **Dalla funzione** (un soggiorno, una camera o una cucina è A, un ufficio B, un deposito E1, un garage F) a meno che tu ne scelga un'altra, oppure **None**. Sotto c'è il **qk** che dà la categoria.

![Il carico variabile di un locale nelle sue proprietà](shots/loads-room.webp)

## La tabella dei carichi variabili

La scheda **Carichi variabili** elenca ogni categoria con il suo uso e il suo **qk** in kN/m², come raccomanda l'EN 1991-1-1. Scrivi nel campo il valore del tuo annesso nazionale; **Modificato** torna al valore predefinito.

![La scheda Carichi variabili](shots/loads-imposed.webp)

## Neve e vento

La scheda **Neve e vento** contiene il clima del sito. Lapin non include le mappe nazionali: leggi i valori del tuo sito da **CR 1-1-3/2012** (neve, la sua figura 3.1 o allegato A) e **CR 1-1-4/2012** (vento, la sua figura 2.1 o allegato A), o dall'annesso nazionale con cui lavori, e scrivili.

![La scheda Neve e vento](shots/loads-climate.webp)

- **Neve**: **Carico neve al suolo s0,k (kN/m²)**, **Esposizione Ce** (piena 0,8, parziale 1,0, riparata 1,2), **Coefficiente termico Ct** (1,0 salvo che la copertura sia vetrata e perda calore) e **Fattore di importanza γIs** (1,0 per gli edifici ordinari). **Applica la neve**.
- **Vento**: **Pressione di riferimento del vento qb (kN/m²)**, **Categoria di terreno** (da 0 a IV, dal mare al centro di una città) e **Fattore di importanza γIw**. **Applica il vento**.

Applicandoli si creano i casi in cui vanno i loro carichi:

- **S**, la neve senza accumulo, e **S+X**, **S-X**, **S+Y**, **S-Y**, la neve accumulata da un vento che soffia verso ogni lato: su una copertura a due falde, la falda esposta al vento ne prende la metà. Un accumulo che qui non cambia nulla (nessuna falda è esposta a quel vento) viene lasciato fuori, e il Riepilogo lo dice.
- **W+X/s**, **W+X/p**, e così via per le quattro direzioni: il vento che soffia verso +X, −X, +Y, −Y, una volta con la **depressione** (/s) delle coperture e una volta con la loro **pressione** (/p).

Le disposizioni della neve e le direzioni del vento sono alternative: una combinazione ne prende solo una per tipo.

Ciò che Lapin ricava da essi, a ogni richiesta:

- **Neve** su ogni falda di copertura, per m² in pianta: s = γIs · μ1 · Ce · Ct · s0,k, con μ1 = 0,8 fino a 30°, poi meno, 0 a 60° e più ripida; su un solaio che nulla copre (una terrazza, una copertura piana) μ1 = 0,8.
- **Vento** su muri e coperture: il contorno dell'edificio è l'inviluppo convesso dei suoi muri e pilastri; le facciate prendono le zone da A a E, le coperture piane da F a I, una copertura a due falde da F a J, secondo i coefficienti dell'EN 1991-1-4, alla pressione dell'altezza dell'edificio per la categoria di terreno. Una facciata passa il suo vento alle travi, ai correnti e ai bordi di solaio lungo di essa, la sua fascia più bassa al suolo; una copertura ai suoi puntoni o arcarecci. Le coperture a padiglione, la pressione interna e gli accumuli di neve ai parapetti non sono ancora ricavati.

> Questi valori sono **indicativi**: i coefficienti sono quelli raccomandati dalle norme. Verificali rispetto al tuo annesso nazionale.

## Riepilogo

La scheda **Riepilogo** mostra, per ogni caso, i carichi che hai disegnato e il totale in kN, complessivo e per ogni livello, poi i carichi derivati uno a uno, con la loro origine. Avvisa dei locali la cui categoria non ha un caso e annota ciò che è stato lasciato fuori.

![La scheda Riepilogo](shots/loads-summary.webp)

## Combinazioni

La scheda **Combinazioni** elenca le combinazioni dei casi, secondo l'EN 1990 con i valori raccomandati, indicative: per lo stato limite ultimo 1,35 sulle azioni permanenti e 1,5 sulla variabile dominante, le altre con il loro ψ0; per lo stato limite di esercizio le combinazioni caratteristica, frequente e quasi permanente. Un caso che non contiene nulla non partecipa.

![La scheda Combinazioni](shots/loads-combinations.webp)

## Analisi

**Esegui l'analisi** (nello stesso gruppo e nella finestra Carichi) analizza il telaio sotto questi carichi: vedi [Analisi strutturale](analysis.md).

## Nel file IFC

**Esporta IFC, Fragments o BOS…** ha **Structural Analysis View (modello analitico e carichi)**: il file porta allora il modello analitico dei muri portanti, solai, pilastri e travi, con i casi di carico, i carichi e le combinazioni, come un IFC Structural Analysis View. Vedi [Importazione ed esportazione](import-export.md).
