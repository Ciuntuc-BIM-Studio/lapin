---
title: Gruppi e pulizia
order: 112
summary: Tieni insieme gli elementi in un gruppo, apri un gruppo per modificarlo, seleziona per regione o con il lazo in 3D e nascondi ciò che è d’intralcio.
---

## Gruppi

Un gruppo tiene insieme degli elementi. Selezionali e fai clic su **Modifica → Crea gruppo** (<kbd>GP</kbd>). Da quel momento un clic su qualsiasi membro seleziona l’intero gruppo, nelle piante e in 3D. Il gruppo si sposta, si copia, si ruota, si specchia e si elimina come un tutt’uno, e una copia è un gruppo a sé. In IFC un gruppo è un IfcGroup, quindi lo vedono anche gli altri programmi.

Le proprietà di un gruppo selezionato indicano il numero di elementi che contiene, con tre pulsanti: **Modifica gruppo**, **Separa gruppo** e **Seleziona membri**.

![Un gruppo selezionato: i suoi membri evidenziati, le sue proprietà](shots/group-selected.webp)

## Modificare un gruppo

Fai doppio clic su un membro, oppure fai clic su **Modifica gruppo** (<kbd>EG</kbd>). Il resto del modello viene disegnato sbiadito e non si può selezionare, e un clic seleziona un membro alla volta. Elimina, sposta o modifica i membri come al solito. **Rimuovi dal gruppo** toglie dal gruppo i membri selezionati. **Fatto**, oppure <kbd>Esc</kbd>, chiude il gruppo. Un gruppo scompare quando viene eliminato il suo ultimo membro.

![Un gruppo aperto per la modifica: il muro del giardino fuori dal gruppo è sbiadito](shots/group-edit.webp)

## File importati

Ogni file IFC, Fragments o mesh che importi entra come un unico gruppo con il nome del file. Togli la spunta da **Come un unico gruppo** nell’importazione IFC per portare dentro gli elementi sciolti. Un file collegato non viene raggruppato: è già un unico collegamento, in sola lettura (vedi [Importazione ed esportazione](import-export.md)). Gli elementi letti da un file IFC come riferimenti restano dove li ha messi il file: un loro gruppo si può ripulire ed eliminare, non spostare.

## Selezionare una regione in 3D

**Vista → Seleziona regione** (<kbd>SR</kbd>) trasforma un trascinamento con il tasto sinistro nella vista 3D in una selezione, come nelle piante. Trascinata verso destra, una finestra prende ciò che sta interamente al suo interno. Trascinata verso sinistra, una selezione intersecante prende ciò che tocca. Premi <kbd>Space</kbd> mentre trascini per disegnare un lazo, e <kbd>Shift</kbd> per aggiungere alla selezione. Finché è attiva, il tasto destro ruota la vista e quello centrale la sposta. La **×** sulla sua etichetta, oppure di nuovo <kbd>SR</kbd>, la disattiva.

![Una selezione intersecante trascinata sulla casa in 3D](shots/select-region-3d.webp)

## Nascondere per ora

**Vista → Nascondi** (<kbd>HH</kbd>) nasconde la selezione nella vista 3D. **Isola** (<kbd>HI</kbd>) nasconde tutto il resto. **Mostra tutto** (<kbd>HR</kbd>), oppure la **×** sull’etichetta, fa riapparire tutto. Nulla viene salvato o eliminato: è un modo per raggiungere ciò che sta all’interno di un modello importato mentre lo ripulisci.
