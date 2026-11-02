---
title: Materiali, texture e retini
order: 90
summary: Imposta il colore, i retini e i giunti di ogni materiale, applicagli un’immagine tua e porta i tuoi motivi di retino .pat.
---

**Gestisci → Materiali** elenca i materiali del progetto. Scegline uno per modificarlo.

![La finestra di dialogo Materiali](shots/materials-dialog.webp)

## Cosa imposta un materiale

- **Colore (3D)** e le facce disegnate su di esso in 3D. Lapin le deduce dal nome, per esempio i corsi di un mattone o la griglia di una piastrella. Le **Estremità** e **Superiore e inferiore** possono apparire diverse dalle facce.
- **Retino** per ogni posto in cui viene disegnato: tagliato in una sezione, tagliato in una pianta e visto in un prospetto. Ognuno ha la sua scala, il suo angolo, il suo allineamento, il suo riempimento e la sua penna.
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
