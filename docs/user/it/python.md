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

## Sicurezza

Uno script può fare tutto ciò che puoi fare tu su questo computer: gira nel tuo Python, con i tuoi permessi. Esegui script scritti da te o di cui ti fidi.

Un ospite di una sessione condivisa non può eseguire script sul tuo computer, e uno script si collega solo al Lapin del proprio computer.

## Per gli sviluppatori

Ciò che offre `lapin` (i comandi, le interrogazioni, i lotti, il modello, gli eventi) è descritto nella guida per sviluppatori, *Lapin from Python* (`docs/python.md` nel repository di Lapin); il pacchetto è su PyPI come `lapinbim`.
