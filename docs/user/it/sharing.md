---
title: Lavorare insieme
order: 125
summary: Condividi il progetto aperto e lavoraci in diretta con altri, da Lapin o da un browser.
---

<!-- video sharing -->

Più persone possono lavorare nello stesso progetto nello stesso momento, come i giocatori che entrano nel server di un gioco. Uno di voi condivide il progetto da Lapin sul proprio computer: è l’**host**. Gli altri partecipano come **ospiti**, da Lapin sul proprio computer o da un browser, con il collegamento che l’host invia loro.

Il progetto resta sul computer dell’host, e il Lapin dell’host decide ogni modifica. Gli ospiti vedono il modello mentre cambia, disegnano con gli stessi strumenti e vedono dove si trovano gli altri.

## Condividi il progetto

**File → Condividi sessione…**, indica il tuo nome, scegli come partecipano gli ospiti, poi premi **Condividi**:

- gli **editor** modellano come te;
- gli **osservatori** guardano e si muovono, e non cambiano nulla.

Lapin mostra un **collegamento** e un **codice**. Invia il collegamento a chi partecipa da un browser, e il codice a chi partecipa da Lapin. Chiunque li abbia può partecipare finché non premi **Interrompi la condivisione**. Ogni volta che condividi, il collegamento e il codice sono nuovi.

![Condivisione: il collegamento, il codice e chi è nella sessione](shots/share-dialog.webp)

Nella stessa finestra di dialogo puoi rendere un ospite editor o osservatore, oppure **chiedergli di uscire**. Chiudila, e la sessione continua. L’icona delle persone accanto ai partecipanti nella barra superiore la riapre.

Mentre il progetto è condiviso e ha un file, Lapin lo salva ogni 5 minuti. Solo l’host salva; gli ospiti possono esportare file IFC e 3D per sé.

## Partecipa a una sessione

- **Da un browser**: apri il collegamento, indica il tuo nome e premi **Partecipa**. Niente da installare.
- **Da Lapin**: nella schermata iniziale, o in **File**, scegli **Partecipa a una sessione…**, incolla il collegamento o il codice e indica il tuo nome.

![Partecipare da Lapin](shots/join-dialog.webp)

Una barra in basso dice nella sessione di chi ti trovi. **Esci** ti riporta al tuo Lapin. Se l’host interrompe la condivisione o chiude Lapin, Lapin te lo dice, e torni indietro con **Torna a Lapin**.

## Chi è dove

I partecipanti sono nella barra superiore, ciascuno con il suo colore. Fai clic su uno per andare alla vista che sta guardando.

![I partecipanti](shots/participants.webp)

In pianta vedi i cursori degli altri con i loro nomi, e ciò che hanno selezionato, contornato nel loro colore.

![Il cursore e la selezione di un ospite nella pianta dell’host](shots/sharing-plan.webp)

## L’annullamento è solo tuo

<kbd>Ctrl Z</kbd> annulla la tua ultima modifica, non quella di qualcun altro. Se qualcuno l’ha cambiata dopo di te, Lapin la lascia com’è e ti dice chi è stato. Da quel momento il tuo annullamento non va più indietro di quella modifica.

## Sicuro per progettazione

- Tutto ciò che passa tra l’host e gli ospiti attraversa il relay di Lapin, **cifrato end-to-end** con la chiave dell’invito. Il relay lo inoltra e non può leggerlo.
- Un ospite non può aprire, salvare o leggere file sul computer dell’host, né aprire lì un altro progetto.
- Lapin non apre nulla verso la rete finché non condividi, e smette quando smetti.
