---
title: Primi passi
order: 10
summary: La schermata iniziale, un nuovo progetto, le parti della finestra, come muoversi in un disegno e come scegliere le unità.
---

## La schermata iniziale

All’apertura, Lapin mostra la schermata iniziale. **Nuovo progetto** avvia un edificio vuoto. **Apri progetto…** apre un file `.lapin`. I progetti che hai già aperto sono elencati sotto **Recenti**.

**Parti da** dà un nuovo progetto preparato per il suo lavoro: **Casa a un piano** (fondazioni e piano terra), **Casa a due piani** (e un primo piano), **Condominio** (piano terra e quattro piani), **Capannone o telaio** (un piano alto 6 m, nell’area di lavoro Strutture e armature), **Solo disegno** (un livello, nell’area di lavoro Disegno 2D) e **Parti da un IFC** (un nuovo progetto, poi il file IFC da importare). Ognuno sceglie anche la sua area di lavoro; i livelli si possono poi rinominare, aggiungere o eliminare come in ogni progetto.

![La schermata iniziale](shots/start-screen.webp)

Un nuovo progetto ha già tre livelli: **Foundations** (−1.50), **Ground floor** (±0.00) e **First floor** (+3.00). Si apre sulla pianta del piano terra, pronto per il primo muro.

## La finestra

![Le parti della finestra di Lapin](shots/workspace.webp)

1. **Le schede della barra multifunzione.** Disegno, Architettura, Finiture, Struttura, Sito, Studio, Annota, Gestisci, Costi e Vista raggruppano ciascuna i propri strumenti. **File** apre, salva, importa ed esporta. **Annulla** e **Ripeti** si trovano all’estremità destra della stessa barra (<kbd>Ctrl</kbd> <kbd>Z</kbd>, <kbd>Ctrl</kbd> <kbd>Y</kbd>).
2. **La barra multifunzione.** Contiene gli strumenti della scheda corrente. Passa il mouse su uno strumento per vederne il nome e le lettere da tastiera.
3. **Il browser di progetto.** Elenca i livelli, le piante, le sezioni, i prospetti, le viste 3D, le tavole e gli abachi. Fai doppio clic su uno di essi per aprirlo in una scheda. Un progetto si apre sulla pianta del piano terra, con la vista 3D in una scheda accanto.
4. **La vista.** È qui che disegni: una pianta, una sezione, una vista 3D o una tavola. Puoi aprire più viste affiancate con i due pulsanti in alto a destra della vista.
5. **La barra rapida.** Mette gli strumenti più usati sui tasti numerici <kbd>1</kbd>–<kbd>9</kbd>.
6. **La riga di comando.** Digita qui le lettere di uno strumento e premi <kbd>Enter</kbd>. Mentre uno strumento è in esecuzione, la riga ti dice di cosa ha bisogno dopo. Vedi [Disegno e riga di comando](drafting.md).
7. **Il pannello Proprietà.** Mostra ciò che è selezionato. Quando non è selezionato nulla, mostra la vista in primo piano.
8. **Chiedimi.** È l’assistente di aiuto di Lapin. Vedi [Aiuto e segnalazioni](help.md).

Ti serve più spazio per il disegno? **Vista → Interfaccia…** sceglie una finestra più compatta, e i pannelli laterali si riducono. Vedi [L’interfaccia e le scorciatoie](interface.md).

## Muoversi nel disegno

- **Zoom.** Ruota la rotellina del mouse o avvicina due dita sul trackpad. Lapin ingrandisce dove si trova il cursore.
- **Panoramica.** Trascina con il tasto centrale o destro del mouse, oppure con due dita sul trackpad.
- **Orbita in 3D.** Trascina con il tasto sinistro del mouse. Con elementi selezionati, la vista ruota attorno a loro; altrimenti attorno al punto del modello sotto il cursore.
- **Mostra tutto.** Premi il pulsante **Adatta**, digita <kbd>ZE</kbd>, oppure fai doppio clic con il tasto centrale.

## Unità

Lapin lavora in millimetri, a meno che tu non scelga altrimenti. Per cambiare le unità, apri **Gestisci → Standard di disegno → Progetto → Unità**, oppure fai clic su **Unità** nella barra di stato in fondo alla finestra. Puoi scegliere millimetri, centimetri, metri, piedi e pollici, oppure pollici.

![Standard di disegno, scheda Progetto: le unità del progetto](shots/standards-project.webp)

Ogni lunghezza segue questa impostazione: i pannelli, i valori che digiti mentre disegni, la riga di comando e le quote. Il modello stesso è conservato in metri, quindi cambiare le unità non modifica mai il tuo edificio. Anche i livelli e gli assi della griglia la seguono.

In millimetri, centimetri e metri, le lunghezze sono conservate e mostrate al decimo di millimetro: una lastra da 12,5 mm resta 12.5 mm (1.25 cm), e i valori interi si scrivono senza decimali. Piedi e pollici vanno al 1/16" più vicino. Le quote dei disegni sono arrotondate come dice il loro stile di quotatura.

## Salvare

**File → Salva** (<kbd>Ctrl</kbd> <kbd>S</kbd>) scrive un file `.lapin`. Vedi [File, versioni e ripristino](files.md).
