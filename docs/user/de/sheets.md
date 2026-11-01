---
title: Pläne, PDF und DXF
order: 100
summary: Ordnen Sie Ansichten auf Plänen mit einem Plankopf an und geben Sie sie als PDF oder DXF aus.
---

<!-- video sheets -->

## Einen Plan erstellen

Klicken Sie im Projektbrowser auf **+** neben **Pläne**. Um eine Ansicht zu platzieren, ziehen Sie sie aus dem Browser auf den Plan oder wählen Sie sie in der Leiste des Plans unter **Ansicht platzieren…**. Jede Ansicht behält ihren eigenen Maßstab und bleibt mit dem Modell verknüpft, sodass sich der Plan ändert, wenn sich das Modell ändert. Auf dem Plan ziehen Sie eine Ansicht, um sie zu verschieben: nur sie bewegt sich, mit ihrem Titel, während Rahmen, Schriftfeld und die anderen Ansichten bleiben; sie rastet an den Kanten und der Mitte des Rahmens und an den anderen Ansichten ein, und die Pfeiltasten rücken sie um 1 mm (10 mm mit Umschalt).

![Ein A3-Plan mit einem Grundriss und einem Schnitt](shots/sheet.webp)

Wählen Sie den Plan aus, um im Eigenschaftenbereich seinen **Plankopf** auszufüllen: das Projekt, den Bauherrn, wer ihn gezeichnet hat, wer ihn geprüft hat, das Datum und den Revisionsstand. Seine Nummer, sein Papierformat und seine Ausrichtung werden dort ebenfalls festgelegt. Leer gelassen zeigt das Projektfeld den Namen des Projekts: den ihm gegebenen Namen, sonst den Namen seiner Datei.

## Was eine Ansicht zeigt

Eine Ansicht auf dem Plan zeigt, was Sie wählen, ohne sie blind zu ziehen:

- **Auf dem Plan zuschneiden.** Klicken Sie eine Ansicht an: An Ecken und Kanten erscheinen Griffe. Ziehen Sie sie, und der Rahmen fasst mehr oder weniger der Ansicht, während die Zeichnung auf dem Papier bleibt, wo sie ist. Mit **Umschalt**-Ziehen darin verschieben Sie die Zeichnung im Rahmen.
- **Ihre Grenze in der Ansicht zeichnen.** **Annotate → View boundary** (<kbd>VB</kbd>): Zeichnen Sie ein Rechteck in einen Grundriss, Schnitt oder eine Ansicht und geben Sie ihm einen im Projekt eindeutigen Namen. In **Place view…** steht die Ansicht dann mit jeder ihrer Grenzen („Ground floor plan › Ostflügel“), und eine so gesetzte Ansicht zeigt genau dieses Rechteck und folgt ihm, wenn Sie es in der Ansicht verschieben oder ändern. Dieselbe Ansicht kann auf mehreren Plänen stehen, je eine Grenze. Eine gesetzte Ansicht wechselt in der Liste auf ihrem Rahmen zwischen ihren Grenzen oder zum freien Zuschnitt. Grenzen sind nur am Bildschirm zu sehen, nie auf Papier.
- **Anheften.** Die Pinnnadel auf dem Rahmen hält die Ansicht fest: Sie wird gewählt, aber nicht versehentlich verschoben oder zugeschnitten.

Rahmen und Schriftfeld gehören zum Papier. Sie werden nie gewählt oder verschoben, und ein Klick nimmt die kleinste Ansicht unter dem Zeiger, sodass eine Ansicht über dem Schriftfeld immer wählbar ist.

![Eine auf dem Plan gewählte Ansicht: ihre Zuschnittgriffe, ihre Grenze, die Pinnnadel](shots/sheet-view-boundary.webp)

## Drucken

Die Schaltflächen **PDF** und **DXF** in der Leiste des Plans exportieren den Plan. Ein PDF ist bereit zum Drucken oder Versenden. Ein DXF öffnet sich in AutoCAD und anderen CAD-Programmen mit seinen Layern, Linien und Schraffuren. **Drucken** schickt den Plan in seinem eigenen Papierformat an Drucker oder Plotter, 1:1, als Vektorlinien: Plotter und Papier wählen Sie im Druckdialog. Auf einem kleineren Drucker wählen Sie Anpassen an die Seite.
