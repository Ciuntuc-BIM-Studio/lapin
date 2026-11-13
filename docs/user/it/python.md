---
title: Script Python
order: 127
summary: Guidare Lapin con Python dal pannello Script, un extra per sviluppatori: una console e un editor che eseguono il tuo Python, ogni Esecuzione un solo passo di annullamento.
---

## Attiva gli Extra per sviluppatori

Lapin si può guidare con Python: ogni strumento è un comando, e uno script invia gli stessi comandi. Il pannello **Script** è un *extra per sviluppatori*, disattivato finché non lo attivi; altrimenti non se ne vede nulla.

Apri **File → Preferenze…** e, in **Generale**, spunta **Extra per sviluppatori**.

![Preferenze: Extra per sviluppatori](shots/python-preferences.webp)

- **Python**: il Python che il pannello esegue. Vuoto, Lapin ne cerca uno su questo computer (`python3`, `py -3` su Windows, quello di python.org e quello di Homebrew). Deve essere **Python 3.11 o successivo**; Lapin non installa Python. Scaricalo da [python.org](https://www.python.org/downloads/) se non ce l’hai, oppure scrivi il percorso di quello che vuoi (quello di un ambiente virtuale, per esempio).
- **Editor esterno**: l’editor che usa **Apri nell’editor esterno**, come comando (`code` per Visual Studio Code). Vuoto, l’editor di testo del sistema.

Python funziona solo nell’app desktop: in Lapin per il browser il pannello lo dice.

## Il pannello Script

Apri **Vista → Script**. Il pannello si apre lungo il bordo inferiore della finestra; trascina il suo bordo superiore per renderlo più alto. La sua intestazione dice quale Python ha trovato, o perché non ne ha trovato nessuno.

![Il pannello Script dopo un’esecuzione: quattro muri, un solo passo di annullamento](shots/python-panel.webp)

- **Miei**, a sinistra: i tuoi script, normali file `.py` tenuti nella cartella di Lapin su questo computer. Fai clic su uno per aprirlo; **Nuovo script** parte da un esempio.
- **L’editor**, al centro: Python con i suoi colori e il suo rientro. Scrivi `lapin.` e ti propone ciò che Lapin ha (i comandi di tutti i moduli della tua edizione e le sue chiamate); `lapin.architecture.` propone i comandi di architettura con i loro argomenti. Dai un nome allo script e **salvalo** in Miei; **Apri nell’editor esterno** apre il suo file nel tuo editor, e ciò che salvi lì torna nel pannello.
- **La console**, a destra: ciò che gli script stampano, i loro errori con la riga in cui si sono fermati (segnata nell’editor) e una riga in cui scrivere Python. **Invio** la esegue, **Maiusc+Invio** inizia una nuova riga, **Su** e **Giù** richiamano le righe scritte prima. Un nome definito nella console resta per la riga successiva e per gli script che esegui.

Nel pannello, `lapin` è il pacchetto `lapinbim`, già caricato. Lo stesso script si esegue da un terminale con `import lapinbim as lapin`, mentre Lapin è aperto.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Le lunghezze sono in metri e gli angoli in gradi, qualunque sia l’unità mostrata dal progetto.

## Esegui, Interrompi, Riavvia, Annulla

- **Esegui** (**Ctrl+Invio** nell’editor) esegue lo script. Tutto ciò che fa è **un solo passo di annullamento**, con il nome dello script: un solo **Annulla** toglie tutto.

  ![Un solo Annulla ha tolto i quattro muri dell’esecuzione](shots/python-undo.webp)

- Mentre uno script è in esecuzione, il modello è dello script: uno strumento usato nella finestra viene rifiutato con *A script is editing the model*; viste, selezione e abachi continuano a funzionare.
- Uno script che fallisce non conserva **nulla** di ciò che ha fatto: il modello è com’era prima di **Esegui**, e la console mostra l’errore e la sua riga.
- **Interrompi** ferma lo script; non si conserva nulla di ciò che ha fatto. Se uno script non si ferma entro due secondi, il suo Python viene chiuso.
- **Riavvia** avvia di nuovo Python: i nomi che la console conservava vengono dimenticati.

## Registra quello che fai come script

La scheda **Registratore**, accanto alla console, scrive quello che fai nella finestra come Python, una riga per passo.

1. Fai clic su **Registra**. Un punto rosso sulla scheda indica che si sta registrando.
2. Lavora come al solito: disegna muri, cambia una proprietà, elimina qualcosa. Ogni passo compare subito come una riga, per esempio `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Fai clic su **Interrompi**, poi su **Copia** (per incollare lo script altrove) o su **Salva come script** (va in Miei e si apre nell'editor).

![Il registratore dopo aver disegnato due muri](shots/python-recorder.webp)

Lo script è scritto in modo che funzioni anche in un altro progetto:
- ciò che un passo ha creato viene tenuto in una variabile (`wall1`), che i passi successivi usano (`wall1.id`);
- il livello su cui hai disegnato si trova dal nome, altrimenti dalla quota (`lapin.model.storey("Ground floor", 0)`), e un tipo dal nome (`lapin.model.find("IfcWallType", "Brick 250")`);
- un altro elemento che era già nel progetto è indicato dal suo id (`lapin.model["…"]`), quindi quella riga funziona solo in questo progetto;
- con **Selezione come lapin.selection()** spuntata, un comando su ciò che avevi selezionato si scrive `lapin.selection()`: lo script lavora allora su ciò che è selezionato quando lo esegui.

Un **Annulla** durante la registrazione toglie l'ultimo passo dallo script, e **Ripeti** lo rimette, così lo script contiene ciò che hai fatto alla fine. Ciò che fa uno script non viene mai registrato, né le domande che non cambiano nulla (una tabella letta, una misura).

![Lo script registrato, eseguito in un progetto nuovo: gli stessi due muri](shots/python-recorder-new.webp)

## Gli script del progetto

Uno script può essere conservato **nel progetto**, così viaggia con il file: aprilo o scrivilo nell'editor e fai clic su **Salva nel progetto**. Compare sotto **Del progetto**, a sinistra. Salvare, rinominare ed eliminare uno script del progetto sono ciascuno un solo passo di annullamento, e le versioni del progetto conservano i suoi script. Nel file `.lapin` ognuno è anche un semplice file di testo, `scripts/<name>.py`, quindi si può leggere senza Lapin.

**Uno script del progetto non si esegue mai da solo**, né all'apertura del progetto né al salvataggio. La prima volta che lo esegui, e di nuovo ogni volta che il suo codice è cambiato, Lapin chiede:
- **Esegui una volta**: si esegue questa volta, e la prossima esecuzione chiede di nuovo;
- **Fidati**: si esegue, e questo codice viene ricordato come fidato su questo computer (una sua modifica chiede di nuovo);
- **Annulla**: non si esegue nulla.

Uno script che salvi tu stesso nel progetto è fidato dal momento in cui lo salvi. I tuoi script (Miei) non chiedono mai.

## Che cosa è selezionato

`lapin.selection()` restituisce ciò che è selezionato nella finestra, come elementi:

```python
for e in lapin.selection():
    print(e.name, e.type)
```

I campi di un elemento si leggono per nome e si cambiano per assegnazione: `wall.height = 3` cambia il muro (un solo passo di annullamento, o parte dell'Esecuzione). `with lapin.step("Name"):` rende più comandi un solo passo di annullamento, e ciascuno risponde comunque subito, così uno successivo può usare ciò che ha creato uno precedente.

## Uno script come strumento

Uno script può essere uno **strumento**: un pulsante in **I miei strumenti** che lo esegue, per chiunque, con gli Extra per sviluppatori attivi o no. Dillo nelle prime righe dello script, la sua *intestazione*:

```python
# lapin: name="Perimeter walls", version="1.0.0", tool="Perimeter walls", icon="square", run="perimeter"
# lapin: tooltip="Four walls round a rectangle, one undo step"

def perimeter():
    v = lapin.ui.ask("Perimeter walls", {
        "width": lapin.ui.number(8.0, unit="m", min=0.5),
        "depth": lapin.ui.number(6.0, unit="m", min=0.5),
        "storey": lapin.ui.storey(),
    })
    if v is None:          # Annulla
        return
    w, d = v["width"], v["depth"]
    corners = [(0, 0), (w, 0), (w, d), (0, d)]
    for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
        lapin.architecture.add_wall(storey_id=v["storey"].id, x1=x1, y1=y1, x2=x2, y2=y2)
```

- **tool**: il nome del pulsante; **run**: la funzione che chiama (senza, gira l'intero script); **icon**: una delle icone di Lapin (`wall`, `door`, `stair`…) o una comune (`square`, `ruler`, `layers`, `grid`, `star`…), altrimenti una lettera (`icon="P"`, o la prima del nome); **tooltip**: ciò che il pulsante dice quando ci passi sopra con il mouse.
- Salva lo script in **Miei** (o nel progetto): il suo strumento compare in **I miei strumenti**, in un gruppo **Script**, senza che lo script venga eseguito. Spostalo, rendilo piccolo o toglilo come qualsiasi strumento; lo trova anche **Comandi…** (Ctrl+K).

![I miei strumenti con lo strumento di uno script](shots/python-tools-tab.webp)

Un clic esegue lo script come una sola **Esecuzione**: tutto ciò che fa è **un solo passo di annullamento**, con il nome dello strumento. `lapin.ui.ask` mostra una finestra di Lapin stesso, fatta con i campi che lo script nomina: un numero con la sua unità, un testo, una scelta, una casella, un elemento (quello che intanto selezioni nel modello: **Usa la selezione**), un piano, un tipo. **OK** dà i valori allo script; **Annulla** non gli dà nulla.

![La domanda dello strumento: le misure e il piano](shots/python-tools-ask.webp)

![I quattro muri dello strumento; un solo Annulla li toglie](shots/python-tools-walls.webp)

Uno script può anche creare strumenti mentre gira: `@lapin.ui.tool("Name", icon="ruler")` sopra una funzione, eseguito una volta dal pannello, mette quello strumento in I miei strumenti.

Da dove vengono gli strumenti:
- **Miei**: sempre;
- **gli script del progetto**: solo quando ti sei fidato del loro codice su questo computer, così un progetto di qualcun altro non mette mai un pulsante nel tuo ribbon; **Dimentica la fiducia** nel pannello li toglie;
- **Lapin Extensions**: uno script che installi mostra il suo strumento; il primo clic chiede **Esegui una volta / Fidati / Annulla**, e **Esegui script** in **Estensioni → Installate** lo esegue anch'esso.

Uno script eliminato, disinstallato o di cui non ci si fida più toglie i suoi strumenti da I miei strumenti.

## Forme create da uno script

Uno script, o un programma sul tuo computer (uno script Python in VS Code, per esempio), può creare elementi di cui calcola la forma: una capriata sopra un locale, un pannello di facciata, una forma progettata da un altro strumento. Sono elementi normali: compaiono in pianta, sezione e 3D, si misurano negli abachi, si esportano in IFC, e li sposti, ruoti, copi, nascondi o elimini come gli altri. Le proprietà dicono **Creato da uno script:** e quale. La loro forma è dello script: ciò che la cambierebbe qui viene rifiutato.

Uno script può seguire ciò di cui è fatto. Eseguito fuori da Lapin, aggiorna da sé i suoi elementi quando cambi il locale o il muro che osserva. Uno script del progetto che osserva elementi viene rieseguito da Lapin dopo la tua modifica, solo se ti fidi di lui su questo computer, come passo di annullamento a sé, *«<script> regenerated»*. Uno script non attendibile non viene eseguito; il pannello Script lo dice.

## Sicurezza

Uno script può fare tutto ciò che puoi fare tu su questo computer: gira nel tuo Python, con i tuoi diritti. Esegui script scritti da te o di cui ti fidi; uno script del progetto e uno installato chiedono prima.

Un ospite di una sessione condivisa non può eseguire script sul tuo computer, né metterne uno nel tuo progetto, né eliminare, rinominare o spostare uno script del progetto; uno script si collega solo al Lapin del proprio computer, e le sue domande si fanno solo nella tua finestra.

## Per gli sviluppatori

Ciò che offre `lapin` (i comandi, le interrogazioni, i lotti, il modello, gli eventi) è descritto nella guida per sviluppatori, *Lapin from Python* (`docs/python.md` nel repository di Lapin); il pacchetto è su PyPI come `lapinbim`.
