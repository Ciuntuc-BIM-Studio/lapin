---
title: L’assistente del modello
order: 127
summary: Fai domande sul tuo progetto a un modello di IA locale, ottieni brevi report e lascia che proponga valori di proprietà che approvi tu. Solo nell’applicazione desktop.
---

L’**Assistente** risponde a domande sul progetto aperto, poste con parole tue: quanti muri ha ogni piano e quanto sono lunghi, quali porte sono più larghe di 1 m, quali non hanno ancora una classe di resistenza al fuoco. Produce brevi report sotto forma di tabelle che puoi esportare, e può **proporre** valori di proprietà (una classe di resistenza al fuoco, uno stato, i tuoi parametri) che esamini e applichi.

Esegue un modello linguistico **sul tuo computer**, tramite [Ollama](https://ollama.com). Nulla del tuo progetto lascia la macchina, e nulla viene inviato a noi o a chiunque altro.

> L’assistente è disponibile solo nell’**applicazione desktop**. La versione di Lapin per browser non ha l’assistente.

## Cosa può e cosa non può fare

**Può**:

- leggere il progetto: piani, elementi, i loro tipi, materiali, gruppi di proprietà e quantità, e gli abachi;
- contare, sommare e fare medie (lunghezze, superfici, volumi, qualsiasi quantità), raggruppati per piano, tipo o qualsiasi proprietà. I numeri li calcola Lapin; il modello si limita a riportarli;
- proporre valori di proprietà, nei gruppi standard di IFC (`Pset_DoorCommon.FireRating`, `Pset_WallCommon.IsExternal`…) o nei tuoi.

**Non può**:

- disegnare, spostare, rimodellare, creare o eliminare nulla. Non modifica la geometria;
- eseguire comandi o scrivere codice;
- applicare nulla da solo. Ogni valore che propone aspetta te.

Un modello locale può fraintendere una domanda. Leggi la sua risposta e controlla i valori proposti prima di applicarli.

## Configurarlo

1. Installa Ollama da [ollama.com](https://ollama.com) e avvialo (l’app Ollama, oppure `ollama serve` in un terminale).
2. Installa un modello. Quello che nei nostri test ha dato i risultati migliori è **qwen3-coder:30b** (veloce e affidabile con gli strumenti di Lapin, richiede circa 20 GB di memoria):

   ```sh
   ollama pull qwen3-coder:30b
   ```

   Su un computer con meno memoria funziona anche **gemma4:12b**, più lentamente.
3. In Lapin, apri l’**Assistente**: la scheda **Assistente** accanto a **Proprietà** a destra, **Vista → Assistente**, oppure <kbd>Ctrl</kbd>+<kbd>K</kbd> e digita *Assistente*.

Lapin trova Ollama all’indirizzo `http://127.0.0.1:11434` e sceglie il miglior modello che hai. Il pulsante a forma di ingranaggio in cima al pannello mostra l’**Indirizzo di Ollama**, il **modello** da usare e se Ollama è in esecuzione. È accettato solo un Ollama su questo computer.

## Chiedere

Digita una domanda e premi <kbd>Enter</kbd> (<kbd>Shift</kbd>+<kbd>Enter</kbd> per andare a capo). Chiedi in italiano, in inglese, in rumeno o in un’altra lingua: la risposta arriva nella stessa lingua.

![L’assistente: un report per piano e una classe di resistenza al fuoco proposta per una porta, in attesa di approvazione](shots/assistant.webp)

Mentre lavora, il pannello mostra ogni passo su una riga, per esempio *Ha elencato 12 di 12 · IfcDoor* o *Ha calcolato 2 gruppi su 40 elementi*. Passa il mouse su un passo per vedere esattamente cosa ha chiesto. **Interrompi** termina la risposta; **Nuova conversazione** ricomincia da capo.

Gli elenchi e i totali compaiono anche come **tabelle** sotto la risposta. Il pulsante di download di una tabella la salva come file CSV, per un foglio di calcolo.

Alcune domande per iniziare:

- *Quanti muri ci sono per piano, e qual è la loro lunghezza totale?*
- *Elenca le finestre del primo piano con la loro larghezza e altezza.*
- *Quali porte non hanno una classe di resistenza al fuoco?*

## Approvare i valori che propone

Chiedi, per esempio, *Porte più larghe di 1 m: assegna FireRating EI30.* L’assistente trova le porte e propone i valori. Compaiono sotto **Modifiche proposte**: ogni elemento, il gruppo di proprietà e il nome, il valore attuale e quello nuovo, con una casella di spunta.

- Togli la spunta da ciò che non vuoi; la casella nell’intestazione spunta o toglie la spunta a tutto.
- Fai clic sul nome di un elemento per selezionarlo nelle viste.
- **Applica** scrive i valori spuntati, tutti insieme, come **un unico** passo: <kbd>Ctrl</kbd>+<kbd>Z</kbd> li annulla tutti.
- **Rifiuta** li scarta.

I valori vanno nei parametri degli elementi, mostrati nel pannello **Proprietà** con il loro gruppo, e nell’esportazione IFC: una proprietà standard entra nel suo gruppo (`Pset_DoorCommon`), le tue vanno in un gruppo a parte.

Le regole che l’assistente segue (e che Lapin verifica): un gruppo IFC standard (che inizia con `Pset_`) accetta solo i propri nomi di proprietà, sugli elementi a cui è destinato; un gruppo tuo non inizia mai con `Pset_`.
