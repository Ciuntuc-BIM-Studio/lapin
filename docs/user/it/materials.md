---
title: Materiali, texture e retini
order: 90
summary: Imposta il colore, i retini e i giunti di ogni materiale, applicagli un’immagine tua e porta i tuoi motivi di retino .pat.
---

**Gestisci → Materiali** elenca i materiali del progetto. Scegline uno per modificarlo.

![La finestra di dialogo Materiali](shots/materials-dialog.webp)

## Aspetto: come viene reso un materiale

La scheda **Aspetto** imposta come appare il materiale in 3D e nei programmi di rendering verso cui esporti. La scheda **Grafica 2D** mantiene retini e penne, come prima.

![L’aspetto di un materiale, con l’anteprima dal vivo](shots/material-appearance.webp)

- **Colore** e **Texture**: una texture disegnata (mattoni, doghe di legno, piastrelle…) o la tua immagine. **W** e **H** fissano quanto è grande una piastrella sul modello, in metri; **Rotazione** la ruota.
- **Rugosità**: 0 è uno specchio, 1 è gesso. **Metallicità**: 1 per il metallo nudo (alluminio anodizzato, acciaio, cromo), 0 per tutto ciò che è verniciato o verniciato a polvere, legno e pietra.
- **Trasparenza**: quanto si vede attraverso. **Vetro (trasmissione)** e **IOR** (1,52 per il vetro) fanno rifrangere il vetro in un programma di rendering.
- **Emette luce** come il diffusore di una lampada.

La sfera mostra subito il risultato. Ciò che non imposti viene dal nome: un materiale chiamato *vetro* o *glass* è trasparente, *acciaio* o *steel* metallico, *parquet* e *laminato* sono doghe di legno. **Aspetto dal nome** toglie di nuovo i tuoi valori.

## Dalla libreria

**Dalla libreria…** offre materiali pronti: metalli (alluminio RAL 7016 antracite, anodizzato, acciaio, rame…), vetro (chiaro, blu, verde, grigio, satinato, riflettente), legni a doghe, calcestruzzo, mattoni, intonaco e pittura, piastrelle e pietra, plastica e tessuto. Un clic ne aggiunge uno al progetto; modificalo come qualsiasi altro.

![La libreria dei materiali](shots/material-library.webp)

## Dati: descrizione, parametri, come si misura

La scheda **Dati** contiene ciò che il materiale dice di sé. Lo scrivi una volta; lo legge ogni tipo in cui è il materiale: l’etichetta di stratigrafia, il computo delle quantità, il computo metrico, l’esportazione IFC.

![I dati di un materiale: descrizione, parametri, come si misura e spessori di catalogo](shots/material-data.webp)

- **Descrizione**: il testo del capitolato, su più righe (*Pannelli in lana minerale, λ = 0,035 W/mK, fissati con tasselli*). Un’etichetta di stratigrafia lo scrive al posto del nome.
- **Parametri**: un nome, un tipo e un valore, come sugli elementi. **Aggiungi uno consueto…** propone **Conducibilità termica λ**, **Resistenza al vapore μ** e **Densità**, che vanno in IFC con i loro nomi standard (*Pset_MaterialThermal.ThermalConductivity*, *Pset_MaterialHygroscopic.UpperVaporResistanceFactor*, *Pset_MaterialCommon.MassDensity*, in unità SI), e **Reazione al fuoco** e **Resistenza a compressione**, che restano tuoi. I tuoi parametri vanno in IFC come *Lapin_Material*.
- **Misurato per** dice come lo contano **Esecuzione → Computo delle quantità** (per materiale) e il computo: **area** (m², una riga per spessore: pannelli, isolanti, membrane), **volume** (m³: calcestruzzo, ghiaia, massetto), **massa** (kg, dalla densità) o **pezzi** (pannelli della misura indicata). Vuoto, si misura come i suoi elementi.
- **Spessori di catalogo (mm)**: gli spessori in cui lo produce il fabbricante, per esempio *50, 80, 100, 150, 200, 240*. Le tabelle degli strati di **Tipi di muro** e **Tipi di solaio** li propongono accanto allo spessore (puoi comunque scriverne un altro) e uno strato di altro spessore è segnato da un avviso.

L’esportazione IFC scrive la descrizione come Description del materiale e i parametri come suoi insiemi di proprietà; l’importazione li rilegge, anche dal file di un altro programma.

## Un materiale per tutti i telai e tutto il vetro

Le nuove porte e finestre sono fatte di materiali condivisi, parte per parte: **Window frame**, **Door frame**, **Glass**, **Door leaf**, **Window board** e **Hardware**. Modifica **Window frame** una volta e cambiano tutti i telai delle finestre.

Per usare altri materiali, apri **Architettura → Porte e finestre**, scegli per un tipo il **Materiale del telaio**, il **Materiale del vetro** e gli altri, e fai clic su **Usa per tutte le finestre** (o **Usa per tutte le porte**). Ogni tipo di quel genere li prende, in un solo passo di annullamento. Anche le facciate continue hanno un materiale del telaio e uno del vetro.

![I materiali delle parti nel configuratore di porte e finestre](shots/joinery-materials.webp)

## Duplica, sostituisci, seleziona

- **Duplica** copia un materiale con un nuovo nome.
- **Sostituisci con** un materiale, poi **Sostituisci**: ogni strato, tipo, porta, finestra ed elemento che usava il primo ora usa il secondo, in un solo passo di annullamento. **Sostituisci ed elimina** toglie anche il primo. Altrimenti un materiale in uso non si può eliminare.
- **Seleziona elementi** seleziona tutti gli elementi con una parte in quel materiale.
- **Cerca materiali** e l’elenco delle categorie filtrano la lista.

## Materiali per il rendering

Twinmotion, Lumion, Enscape, Blender e altri programmi di rendering sostituiscono i materiali per nome. Lapin scrive ogni materiale **una sola volta, con il suo nome**, con colore, texture, rugosità, metallicità e vetro, e divide la mesh di ogni elemento per materiale.

1. Dai ai materiali il nome che vuoi nel programma di rendering, per esempio *Aluminium RAL 7016 anthracite* per tutti i telai e *Glass blue tinted* per tutto il vetro.
2. **File → Esporta 3D (glTF, USD, DAE)…** e scegli **GLB** (consigliato: un file con le texture, i nomi esatti e il vetro), **USDZ** oppure **OBJ + MTL** (uno .zip con le texture; gli spazi nei nomi diventano trattini bassi).
3. Importa il file nel programma di rendering. Lì tutti i telai condividono un materiale; sostituiscilo una volta con un metallo del programma e cambiano tutti.

![Una casa con telai grigio scuro e vetro blu](shots/materials-render-3d.webp)

Anche l’export IFC chiama lo stile di superficie di ogni materiale come il materiale, con la sua trasparenza e il modo in cui riflette.

## Cosa imposta un materiale

- **Colore (3D)** e le facce disegnate su di esso in 3D. Lapin le deduce dal nome, per esempio i corsi di un mattone o la griglia di una piastrella. Le **Estremità** e **Superiore e inferiore** possono apparire diverse dalle facce.
- **Retino** per ogni posto in cui viene disegnato: tagliato in una sezione, tagliato in una pianta e visto in un prospetto. Ognuno ha la sua scala, il suo angolo, il suo allineamento, il suo riempimento e la sua penna. Finché non glielo dai, un taglio prende il motivo che il suo nome suggerisce, come i materiali predefiniti di Revit: mattoni, muratura e pietra linee a 45° (MASONRY), blocchi di calcestruzzo e calcestruzzo aerato un tratteggio incrociato diagonale (BLOCK), calcestruzzo, gettato in opera o armato, puntinato (CONCRETE), isolante rigido (EPS, XPS, PIR) una fine griglia quadrata (RIGID), lana minerale BATT, gesso e intonaco una puntinatura (GYPSUM), sabbia una puntinatura fitta (SAND), legno WOOD, compensato, OSB e CLT WOODCROSS, acciaio STEEL, terra EARTH, ghiaia GRAVEL, piastrelle TILES; vetro, aria, pittura, membrane e qualunque altro nome nessuno. I corsi dei mattoni sono quelli di un prospetto. Un progetto fatto prima conserva i retini che aveva. Un prospetto o una sezione retina le facce che vede con il retino di prospetto, dove nulla di più vicino le nasconde (Viste, Retini delle superfici); finché non glielo dai, il suo nome ne propone uno (mattoni BRICK, pietra STONE, legno BOARDS, rivestimento metallico VERTICAL), mai il BATT dell’isolante.
- **Categoria**, per esempio finitura, isolante o struttura. Gli stili di vista la usano per escludere le finiture da una vista strutturale.
- **Membrana** disegna uno strato sottile come una linea tratteggiata invece che come una fascia.
- **Disegna i giunti.** Per impostazione predefinita, un materiale prosegue da un elemento nel successivo senza una linea, come due muri allineati o un intonaco su due piani. Spuntala per disegnare il giunto.

## Un’immagine tua su un materiale

Un materiale può portare un’immagine tua: una foto del mattone, del legno o della pietra veri.

1. Scegli il materiale e fai clic su **Immagine propria → Importa immagine…**. Scegli un file PNG o JPEG.
2. Imposta **W** e **H**: quanto è larga e quanto è alta, in metri, una copia dell’immagine sul modello. L’immagine si ripete sulle facce.

![L’immagine propria di un materiale, applicata a 1.2 × 1.2 m](shots/own-image.webp)

L’immagine appare nella vista 3D ed è salvata dentro il file del progetto. Va anche nelle esportazioni 3D: **GLB**, **glTF** e **USDZ** la portano con sé. **DAE** e **USDA** sono file singoli senza spazio per le immagini, quindi mantengono il colore del materiale.

![La stessa casa con l’immagine propria del materiale in 3D](shots/own-image-3d.webp)

Le immagini più grandi di 2048 pixel vengono ridotte a quella dimensione quando le importi. Il pulsante del cestino rimuove l’immagine dal materiale.

## I tuoi motivi di retino (.pat)

**Importa .pat** legge un file di motivi di retino di AutoCAD. Ogni motivo che contiene viene aggiunto al progetto, pronto per essere scelto come retino di un materiale.

Lapin conserva anche il file in **I miei motivi** sul tuo computer. In qualsiasi altro progetto, **Aggiungi a questo progetto** riporta quei motivi. Un progetto conserva i motivi che usa, quindi si apre allo stesso modo su un altro computer.

![I miei motivi: i file .pat che hai importato, conservati per ogni progetto](shots/my-patterns.webp)
