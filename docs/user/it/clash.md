---
title: Rilevamento delle interferenze
order: 58
summary: Trova dove gli elementi del modello si attraversano o si avvicinano troppo: insiemi di ricerca per regole, test duri e di distanza tra loro, interferenze seguite da un’esecuzione all’altra, mostrate in 3D e inviate come BCF.
---

**Gestisci → Rilevamento delle interferenze** trova dove gli elementi del modello si attraversano o si avvicinano più del dovuto. Funziona allo stesso modo sugli elementi di Lapin e sui file IFC e Fragments collegati, quindi coordina anche i modelli che ti mandano le altre discipline.

![Il rilevamento delle interferenze, accanto alla vista 3D](shots/clash-dialog.webp)

## Test e insiemi

Un **test di interferenza** confronta due **insiemi di ricerca**: l’insieme A con l’insieme B, o un insieme con sé stesso. Un progetto parte con tre insiemi (Structure, Services, Architecture) e tre test duri: Structure × Services, Services × Services e Architecture × Services.

- I test **duri** trovano gli elementi che si attraversano. La loro **tolleranza** (5 mm di base) ignora attraversamenti così piccoli, così una finitura posata 3 mm dentro un muro non viene segnalata.
- I test **di distanza** trovano gli elementi più vicini di una distanza (50 mm di base), e anche gli attraversamenti.

Alcune coppie non si testano mai, perché devono incontrarsi: un muro e la porta o la finestra della sua apertura, le parti di uno stesso assieme, i muri uniti in un angolo e gli elementi collegati tra loro (una trave posata su un pilastro, un muro collegato a un tetto).

![Le impostazioni di un test](shots/clash-test.webp)

## Eseguire e seguire le interferenze

**Esegui il test** esegue il test in primo piano; **Esegui tutto** esegue ogni test attivo. Niente parte da solo: i risultati restano nel progetto finché non esegui di nuovo il test, e la finestra dice quando il modello è cambiato da allora.

Ogni interferenza mantiene il suo numero da un’esecuzione all’altra. Un’interferenza trovata per la prima volta è **Nuova**; trovata di nuovo, è **Attiva**. Segnala a mano **Verificata** o **Approvata**, assegnala a qualcuno (**Assegnato a**) e scrivi delle **Note**. Un’interferenza che un’esecuzione non trova più diventa **Risolta**; se ritorna, è di nuovo aperta, con lo stesso numero.

Fai clic su un’interferenza per vederla in 3D. Lapin seleziona i due elementi, mette una scatola di sezione intorno al punto in cui si incontrano e li guarda di lato, così vedi il tubo attraverso il muro e non la faccia del muro. **da soli in 3D** nasconde il resto; le frecce scorrono le interferenze una alla volta. La finestra resta a destra, quindi puoi muovere la camera mentre è aperta.

![Un tubo attraverso un muro, inquadrato dall’interferenza](shots/clash-3d.webp)

**BCF** fa di ogni interferenza aperta (o di quelle selezionate) un’osservazione di tipo Clash con il suo punto di vista e le salva come file BCF per altri strumenti BIM; inviate di nuovo, le stesse osservazioni vengono aggiornate. **CSV** salva l’elenco per un foglio di calcolo.

## Insiemi di ricerca

**Insiemi di ricerca** contiene le regole che scelgono gli elementi di un insieme. Una regola legge un campo dell’elemento (classe, tipo predefinito, tipo, piano, materiale, nome, file collegato o qualsiasi parametro) con un operatore: è, è uno di, non è, non è nessuno di, contiene, non contiene, almeno, al massimo, tra. I numeri si leggono come li scrive la gente: «600 mm» è 600.

Un insieme prende gli elementi che soddisfano una regola, o tutte. Una regola «non» toglie sempre elementi, e un insieme senza regole che aggiungono elementi li prende tutti. Il numero accanto all’insieme mostra quanti elementi prendono le regole mentre scrivi.

![Le regole di un insieme di ricerca](shots/clash-sets.webp)

## Gruppi, rapporti e nuvole

L’elenco raggruppa le sue interferenze **per piano**, **per elemento** (l’elemento presente nel maggior numero di interferenze le prende, poi il successivo), **per classi** o **per stato**. La casella di un gruppo seleziona tutte le sue interferenze, per dar loro uno stato insieme.

![Interferenze raggruppate per elemento](shots/clash-groups.webp)

- **XML** scrive le interferenze come Navisworks scrive i suoi rapporti, nei loro gruppi, perché altri strumenti di coordinamento le leggano.
- **PDF** stampa l’elenco su A4, raggruppato allo stesso modo.
- **HTML** crea una pagina con un’immagine di ogni interferenza aperta, inquadrata a turno nella vista 3D.
- **Segna nella vista** disegna le interferenze aperte nella pianta, sezione o prospetto in primo piano come nuvole rosse con l’etichetta test.interferenza (2.14: il secondo test, interferenza 14), solo sullo schermo. Un clic su una nuvola seleziona l’elemento.

![Interferenze come nuvole in una pianta](shots/clash-marks.webp)

Un progetto parte anche con un quarto test, **Openings for MEP**: la verifica di Gestisci → Verifica interferenze… eseguita come test, così i suoi problemi sono numerati e seguiti come gli altri. La sua profondità è quanto manca a un percorso nella sua apertura.

Un test duro trova anche un elemento interamente dentro un altro, senza facce che si attraversano: un tubo dimenticato dentro un solaio. La sua profondità è la larghezza minore dell’elemento interno.

> I percorsi impiantistici di Lapin possono tagliare da soli le loro aperture in muri e solai (Gestisci → Ordine di taglio). Allora un tubo attraverso un muro non è un’interferenza: il muro ha la sua apertura. **Gestisci → Verifica interferenze…** controlla quelle aperture.

Il rilevamento delle interferenze segue il progetto, i numeri e i test di [BimGo](https://github.com/aussieBIMguru/BimGo), di Aussie BIM Guru (MIT-0), riscritti in Lapin.
