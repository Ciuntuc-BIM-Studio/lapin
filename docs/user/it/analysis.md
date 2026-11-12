---
title: Analisi strutturale
order: 52
summary: Esegui un'analisi statica lineare del telaio sotto i tuoi carichi, leggi i diagrammi di sforzi, momenti e deformata in 3D e imposta gli svincoli alle estremità delle aste.
---

**Esegui l'analisi** del gruppo **Carichi** della scheda **Struttura** (e della finestra Carichi) analizza le aste del modello sotto i carichi e le combinazioni di [Carichi](loads.md) e mostra le sollecitazioni in esse.

![Il gruppo Carichi, con Esegui l'analisi](shots/ribbon-analysis.webp)

> I risultati sono **indicativi**. Carichi, combinazioni e analisi vanno verificati da te prima di farci affidamento.

## Cosa viene analizzato

- Le **aste** (pilastri, travi, elementi in acciaio e in legno) entrano con la propria rigidezza, da materiale e sezione. Un'asta il cui materiale non ha modulo elastico viene esclusa ed elencata.
- I **muri portanti** sono vincoli: sorreggono le aste che vi poggiano, ma non sono analizzati come muri.
- I **solai e le lastre** non portano come piastre; passano i carichi alle aste e ai muri sottostanti con la **regola dei 45°**: le bisettrici degli angoli dividono la lastra nelle aree portate da ogni lato, e ciascuna diventa un carico trapezoidale o triangolare sull'asta sotto.
- Le **coperture con struttura** passano il carico a ogni puntone secondo la sua **larghezza di influenza**, fino a metà strada dai vicini; la neve è presa per proiezione orizzontale.
- I **tondini e i tiranti** (una barra tonda piena che controventa, un tirante o un cavo) lavorano assialmente: sono incernierati alle estremità e il loro peso va alle estremità, quindi non si inflettono come travi. Il campo **Asta con sforzo normale** nelle proprietà **Analisi** di un'asta lo cambia: **Dalla sezione e dall'uso**, **Sì (un tondino o un tirante)** o **No (una trave)**.
- Le **travi di altezze diverse** che arrivano a un pilastro mantengono ciascuna il proprio livello; il pilastro ha un solo nodo, e le travi vi sono vincolate con un braccio rigido.
- Il **vento sulle facciate** va alle travi, ai correnti e ai bordi di solaio lungo di esse; la fascia al loro piede va direttamente al suolo ed è elencata a parte dalle somme delle aste.
- Il **peso proprio** delle aste è preso lungo di esse, e i carichi che hai disegnato e quelli derivati vanno dove trovano qualcosa sotto. Un carico che non trova nulla è escluso e segnalato.

## Esecuzione

Scegli **Esegui l'analisi**. Si apre la finestra **Risultati dell'analisi**:

![La finestra Risultati dell'analisi](shots/analysis-results.webp)

- Il riepilogo: aste, nodi e incognite del calcolo e quanto è durato.
- Per ogni caso, la **somma dei suoi carichi** a confronto con la **somma delle reazioni**, e se si **equilibrano** (una spunta) o no (una croce: non fare affidamento su quei risultati). I carichi non assegnati sono in kN.
- Cosa è stato **escluso** dall'analisi, e perché.
- I valori **massimi e minimi** di sforzi e momenti, lo spostamento e la freccia massimi (con il rapporto con la luce, L/300) del caso, della combinazione o dell'inviluppo scelti.

**Mostra i diagrammi** chiude la finestra e mostra i risultati nella vista 3D.

## I diagrammi in 3D

Una scheda sopra la vista 3D sceglie cosa viene disegnato:

![Il momento flettente dell'inviluppo allo stato limite ultimo](shots/analysis-moments.webp)

- **Cosa**: un caso di carico, una combinazione o l'**inviluppo** delle combinazioni allo stato limite ultimo (SLU) o di esercizio (SLE): il valore massimo e minimo in ogni punto.
- **Grandezza**: **N** sforzo normale (trazione positiva), **Vy** e **Vz** tagli, **T** torsione, **My** e **Mz** momenti flettenti (**Mz** è l'asse forte di una trave orizzontale, positivo con fibra inferiore tesa) o la **Deformata**.
- **Scala**: la dimensione del diagramma sullo schermo; il modello non cambia.
- **Reazioni**: frecce ai vincoli con i loro valori in kN.

Ogni asta mostra la sua linea di base, la curva, tratteggi tra le due e i valori agli estremi; il rosso è positivo, il blu negativo. I momenti sono disegnati dal lato in cui l'asta è tesa.

![La deformata](shots/analysis-deformed.webp)

## Svincoli alle estremità

Seleziona un pilastro, una trave o un elemento: le sue proprietà hanno una sezione **Analisi** con gli svincoli **Inizio (i)** e **Fine (j)**.

![La sezione Analisi di una trave](shots/analysis-bar.webp)

- **Predefinito**: ciò che dice il collegamento (una piastra, un fazzoletto o un controvento rende l'elemento incernierato), un elemento ligneo di copertura incernierato attorno al suo asse forte, ogni altra asta incastrata.
- **Incastrato**: l'estremità assorbe momenti. **Incernierato**: ruota liberamente attorno ai due assi di flessione.
- **Personalizzato**: sei caselle, una per grado di libertà (spostamento lungo l'asta, lungo ciascun asse di taglio, torsione, rotazione attorno a ciascun asse di flessione); un grado spuntato è svincolato.

Una modifica è un solo passo di annullamento.

## Quando il modello cambia

I risultati sono calcolati dal modello così com'è e mai salvati. Quando cambi il modello, i diagrammi escono dalla vista e la scheda lo dice:

![La scheda dopo una modifica](shots/analysis-pick.webp)

**Esegui di nuovo** analizza il nuovo modello.

## Un meccanismo

Se la struttura può muoversi senza resistenza (troppi svincoli, o una parte che nulla trattiene), l'analisi si ferma e dice dove:

![Un meccanismo](shots/analysis-mechanism.webp)

Nomina il nodo, il grado di libertà e le aste coinvolte; **Seleziona gli elementi coinvolti** li seleziona così puoi correggerli, di solito incastrando un'estremità o aggiungendo un vincolo.
