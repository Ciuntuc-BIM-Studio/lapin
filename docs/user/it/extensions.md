---
title: Estensioni
order: 126
summary: Aggiungi arredi, modelli di progetto, profili, motivi di campitura, cartigli e listini prezzi creati da altri dal catalogo Lapin Extensions, e rimuovili di nuovo.
---

## Il catalogo

**Lapin Extensions** è un catalogo gratuito di contenuti creati per Lapin dai suoi utenti e da noi: pacchetti di arredi, modelli di progetto, librerie di profili, motivi di campitura, cartigli e listini prezzi. Ognuno va nella libreria che Lapin ha già per quel tipo, così lo usi come se l'avessi fatto tu.

Apri **File → Estensioni…**. **Sfoglia** mostra il catalogo: cerca per nome, etichetta o per ciò che un'estensione contiene, oppure scegli una categoria.

![Estensioni: Sfoglia, i dettagli di un pacchetto di arredi](shots/extensions-browse.webp)

Fai clic su un'estensione per vederne i dettagli prima di installarla:

- **Autore** e **Licenza**. Ogni estensione ha una licenza, scelta dal suo autore. Una *permissiva* (MIT, CC-BY, CC0…) ti permette di usare ciò che contiene in qualsiasi progetto; per le altre, *leggi le condizioni* prima.
- **Non commerciale**: un'estensione la cui licenza vieta l'uso commerciale (CC-BY-NC, PolyForm Noncommercial…) è segnata così nell'elenco e nei suoi dettagli. Puoi installarla e impararne, ma non usarla in lavori retribuiti.
- **Funziona con**: le versioni di Lapin per cui è stata fatta. Se non va bene per il tuo Lapin, lo dice invece di offrire **Installa**.
- **Dimensione**, il file e il suo **SHA-256**: l'impronta del file esaminato dal catalogo.
- **Contenuto**: i suoi elementi, quando l'autore li elenca.
- **Sito** e **Discussione sul forum**: dove fare domande all'autore.

## Installare, aggiornare, disinstallare

Fai clic su **Installa**. Lapin scarica il file, controlla che dimensione e SHA-256 siano esattamente quelli indicati dal catalogo (un file modificato lungo il percorso viene rifiutato e non si installa nulla), poi lo inserisce nella sua libreria:

| Tipo | Dove va |
|---|---|
| Arredi | **Interni → I miei arredi…** |
| Modelli di progetto | **File → Nuovo da modello…** |
| Profili | le librerie di profili di questo computer |
| Motivi di campitura | I miei motivi, offerti da **Gestisci → Materiali** |
| Cartigli | i cartigli di questo computer |
| Listini prezzi | **Esecuzione → Listini prezzi…**, indicati come orientativi: i prezzi sono dell'autore, confrontali con i tuoi |
| Tipi | il progetto aperto quando la installi, in un passo da annullare (**Gestisci → Pacchetti di tipi…** esporta i tuoi) |

Se la tua libreria ha già qualcosa con lo stesso nome, Lapin chiede prima di sostituirlo. Nulla di ciò che porta un'estensione viene mai eseguito: le estensioni sono contenuto.

Anche il catalogo di Lapin è **firmato** da Lapin Extensions: il catalogo e ciascuno dei suoi file portano una firma che Lapin verifica prima di leggere l'elenco o installare un file, così una copia modificata lungo la strada viene rifiutata. Una riga sopra l'elenco dice **Firmato da Lapin Extensions** quando lo è.

**Installate** elenca ciò che ha questo computer. Ogni riga apre il posto dove è andato (**I miei arredi…**, **Modelli di progetto…**), offre **Aggiorna** quando il catalogo ha una versione più recente, e **Disinstalla**.

![Installate: il pacchetto di arredi, con il pulsante che apre I miei arredi](shots/extensions-installed.webp)

![I tipi di arredo del pacchetto in I miei arredi](shots/extensions-my-furniture.webp)

**Disinstalla** toglie dalle tue librerie ciò che l'estensione ha aggiunto, e solo quello. Ciò che hai già messo nei tuoi progetti resta lì: un tipo di arredo inserito in un progetto, un progetto avviato da un modello.

## Tipi

Un'estensione di **tipi** porta tipi di muri, solai, porte, finestre, pilastri, travi o scale con ciò di cui sono fatti: i loro strati, materiali, profili e parametri. Installata, i suoi tipi entrano nel progetto aperto ora, in un passo da annullare: un materiale con lo stesso nome già nel progetto viene usato, ogni altro viene creato; un tipo già presente esattamente uguale viene riutilizzato, e uno con lo stesso nome ma diverso viene aggiunto accanto con (2) dopo il nome. **Installate → Aggiungi i suoi tipi a questo progetto** li porta in un altro progetto.

![I dettagli di un'estensione di tipi: la sua licenza segnata come non commerciale](shots/extensions-types.webp)

I tuoi tipi vanno in un pacchetto da **Gestisci → Pacchetti di tipi…**: spunta i tipi, dai un nome al pacchetto, e **Esporta tipi…** salva un file `.lapintypes`. **Importa tipi…** nello stesso dialogo ne legge uno, ti lascia scegliere i suoi tipi e li aggiunge allo stesso modo.

![Gestisci → Pacchetti di tipi…: un tipo di muro scelto per l'esportazione](shots/type-packs-export.webp)

## Script

Il catalogo elenca anche **script Python**. Nell'app desktop puoi installarne uno già ora; viene eseguito quando Lapin avrà la sua console Python, e solo quando lo esegui tu. Prima di installarlo, i suoi dettagli dicono cosa chiede (leggere o modificare il progetto aperto, accedere ai file o a internet). Uno script può fare sul tuo computer ciò che puoi fare tu, quindi installa script solo di autori di cui ti fidi. Il browser accetta solo pacchetti di contenuto.

![I dettagli di uno script: cosa chiede, e che non è ancora eseguibile](shots/extensions-script.webp)

## Un altro catalogo

**Catalogo…**, in fondo alla finestra, imposta l'indirizzo del catalogo: un indirizzo https, oppure il percorso di un `index.json` su questo computer o in una cartella condivisa (il catalogo di uno studio). Lascialo vuoto per usare quello di Lapin. La freccia accanto rilegge il catalogo. Un catalogo tuo non deve essere firmato: Lapin dice allora sopra l'elenco che **non è firmato da Lapin Extensions**, quindi installa da esso solo se ti fidi di chi lo gestisce. Un catalogo la cui firma non è valida non viene mai letto.

## Pubblicare le tue

Il catalogo ha un proprio sito, **[store.lapinbim.com](https://store.lapinbim.com)**: una pagina per ogni estensione, con le sue immagini, versioni, licenza e download, e una pagina per ogni autore.

Chiunque può pubblicare un'estensione. Esportala da Lapin (I miei arredi → Esporta…, Salva come modello…, una libreria di profili, un cartiglio), poi:

- **Sullo store**: accedi a store.lapinbim.com con il tuo account del [forum](https://forum.lapinbim.com), apri **Your extensions** (le tue estensioni), compila il modulo e carica il file e qualche schermata. Lo store verifica il file come farà Lapin e lo invia in revisione; la tua pagina mostra la revisione e, una volta pubblicata, quante volte è stata scaricata. Una nuova versione segue lo stesso percorso.
- **Su GitHub**: allega il file a una release di un tuo repository e aggiungi il suo manifest con una pull request su [Ciuntuc-BIM-Studio/lapin-extensions](https://github.com/Ciuntuc-BIM-Studio/lapin-extensions). Il suo README spiega come, e cosa guardano i controlli automatici.

Un pacchetto di tipi viene da **Gestisci → Pacchetti di tipi… → Esporta tipi…**; un listino prezzi da **Listini prezzi…**, con il pulsante di esportazione di un listino di questo computer (un file `.lapinprices`, con i prezzi indicati come orientativi).
