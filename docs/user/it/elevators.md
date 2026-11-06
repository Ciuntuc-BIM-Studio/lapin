---
title: Ascensori
order: 43
summary: Ascensori da un configuratore di dodici tipi dimensionati secondo EN 81-20 ed EN 81-70, con vano, fossa, testata, locale macchina, porte di cabina e di piano, le aperture nei solai che attraversano, piante su ogni piano, un abaco e IFC.
---

## Inserire un ascensore

**Architettura → Ascensore** (<kbd>LF</kbd>) inserisce un ascensore con un clic nella pianta. Il clic è il centro del vano, e le porte guardano in basso nella pianta (ruotalo con 0°, 90°, 180° o 270° nella barra, o più tardi con la sua maniglia). L’ascensore va dal piano della pianta al piano più alto del progetto e si ferma a ogni piano intermedio. Il suo configuratore si apre subito.

Mentre lo strumento è attivo, la barra della riga di comando e il pannello delle proprietà mostrano **lo stile** che inserisce: uno dei dodici tipi del catalogo, o uno degli stili del progetto.

## Il configuratore

![Il configuratore di ascensore, con lo stile accessibile in sedia a rotelle](shots/elevator-configurator.webp)

Gli stili sono a sinistra. Uno stile compila tutti i campi, e puoi poi cambiare a mano qualsiasi campo. Il catalogo ha questi tipi:

| Stile | Persone | Portata nominale | Cabina (mm) | Azionamento |
|---|---|---|---|---|
| Residenziale piccolo | 4 | 320 kg | 900 × 1000 | senza locale macchina |
| Passeggeri standard | 6 | 450 kg | 1000 × 1250 | senza locale macchina |
| Passeggeri medio | 8 | 630 kg | 1100 × 1400 | a fune, locale macchina in alto |
| Passeggeri grande | 10 | 800 kg | 1350 × 1400 | a fune |
| Passeggeri, traffico intenso | 13 | 1000 kg | 1600 × 1400 | a fune, 1,6 m/s |
| Accessibile in sedia a rotelle | 8 | 630 kg | 1100 × 1400, porta 900 | EN 81-70 tipo 2 |
| Montaletti ospedaliero | 17 | 1275 kg | 1200 × 2300 | a fune, cabina passante |
| Montacarichi | 26 | 2000 kg | 1500 × 2700 | a fune, 0,5 m/s |
| Idraulico (edifici bassi) | 8 | 630 kg | 1100 × 1400 | pistone, locale centralina accanto al vano |
| Senza locale macchina (MRL) | 13 | 1000 kg | 1100 × 2100 | macchina nella testata |
| Panoramico in vetro | 8 | 630 kg | 1100 × 1400 | cabina in vetro in un vano in vetro |
| Piattaforma domestica | 3 | 250 kg | 800 × 950 | 0,15 m/s, fossa ridotta |

A destra, tre schede:

- **Cabina e portata**: l’azionamento (a fune, senza locale macchina, idraulico, domestico), la portata nominale e le persone, la velocità, il tipo accessibile (EN 81-70 tipo 1, 2 o 3), larghezza, profondità e altezza della cabina, pareti di cabina in vetro, un ascensore antincendio, un ascensore di evacuazione. **Portata e persone dalla cabina** imposta la portata nominale minima che EN 81-20 dà all’area della cabina, e le persone che porta.
- **Porte e fermate**: uno o due accessi (una cabina passante), larghezza e altezza della porta, apertura centrale o laterale e le sue ante (due o tre ante scorrono una dietro l’altra), la fermata più alta e i piani a cui si ferma. Un piano che passa senza fermarsi tiene chiusa la parete del vano e non ha porta di piano.
- **Vano**: il lato del contrappeso (o del pistone di un ascensore idraulico), la fossa sotto la fermata più bassa e la testata sopra la più alta (entrambe seguono l’azionamento e la velocità finché non scrivi valori tuoi), il locale macchina (in alto per quelli a fune, accanto al vano in basso per un idraulico, nessuno altrimenti), le pareti del vano (un tipo di muro del progetto, o calcestruzzo dello spessore che indichi), un vano in vetro, la rotazione.

![La sezione attraverso la cabina, dalla fossa al locale macchina](shots/elevator-section.webp)

L’anteprima mostra la pianta alla fermata più bassa e, nelle altre schede, una sezione attraverso la cabina con ogni fermata (le fermate servite in rosso, quelle passate tratteggiate). Sotto, ciò che dice EN 81: una cabina più grande di quanto la sua portata ammetta, più persone di quante ne portino la portata o l’area, una cabina o una porta accessibile troppo piccola, un ascensore idraulico con una corsa oltre circa 18 m. Più sotto, le misure nette ed esterne del vano, l’area della cabina rispetto a quella ammessa dalla portata, le fermate, la corsa e i solai aperti dal vano.

**Applica** modifica l’ascensore in un solo passo di annullamento. **Salva come stile…** lo conserva come stile del progetto; **Aggiorna lo stile** lo salva di nuovo e tutti gli ascensori di quello stile lo seguono. Uno stile del progetto si può rinominare o eliminare (i suoi ascensori restano come sono).

## Nei disegni

![Un ascensore in pianta: il vano sezionato, la cabina con la sua croce, il contrappeso e le porte di piano](shots/elevator-plan.webp)

Il vano apre ogni solaio che attraversa, con un’apertura propria in ciascun solaio. Quando sposti, ruoti o copi l’ascensore, o cambi i suoi piani, le aperture seguono. Per un solaio disegnato dopo l’ascensore, **Architettura → Aperture del vano** lo apre.

Ogni pianta di un piano raggiunto dall’ascensore lo disegna: le pareti del vano sezionate, la cabina con la sua croce diagonale, il contrappeso con la sua diagonale (il pistone di un idraulico come un cerchio) e, ai piani in cui si ferma, le ante della porta di piano e della porta di cabina, tratteggiate dove scorrono quando sono aperte. Sezioni e prospetti disegnano tutto il vano, la fossa, la copertura, il locale macchina, la cabina alla fermata più bassa e le porte di piano. In 3D la cabina sta alla fermata più bassa con le porte chiuse.

![Un ascensore in 3D, con il locale macchina sopra la casa](shots/elevator-3d.webp)

## Proprietà, abaco e IFC

![Le proprietà di un ascensore](shots/elevator-properties.webp)

Seleziona un ascensore per cambiarne la fermata più bassa e quella più alta, spuntare i piani a cui si ferma, scegliere il tipo di muro del vano, leggere gli avvisi di EN 81 e aprire **Configuratore…**. I suoi campi (azionamento, portata, cabina, porte, fossa, testata…) sono sotto; quelli che non si applicano si nascondono (niente locale macchina per un ascensore senza locale macchina, niente lato del contrappeso per uno idraulico). Trascina la maniglia centrale per spostarlo e quella davanti alle porte per ruotarlo. **Sposta**, **Copia**, **Ruota** e **Specchia** funzionano come per ogni elemento; un ascensore specchiato porta il contrappeso sull’altro lato.

**Architettura → Abaco degli ascensori** elenca ogni ascensore con tipo, azionamento, portata nominale, persone, cabina, porte, le fermate servite, i piani fra cui corre, la corsa, la velocità e il vano.

Ogni ascensore ha un tipo ricavato da genere, portata e cabina (Passeggeri medio 630 kg, 1100 × 1400): gli ascensori uguali lo condividono, così una voce di prezzo messa sul tipo (**Costi → Assegna voci…**) li prezza tutti, e la pianificazione li trova dalla loro classe (IfcTransportElement).

In IFC un ascensore è un **IfcTransportElement** con il tipo predefinito **ELEVATOR**, di un **IfcTransportElementType**, con i set di IFC **Pset_TransportElementCommon** (CapacityPeople, CapacityWeight, FireExit) e **Pset_TransportElementElevator** (FireFightingLift, ClearWidth, ClearDepth, ClearHeight). Lapin rilegge il proprio file come un ascensore nativo, con le sue fermate e le aperture nei solai.
