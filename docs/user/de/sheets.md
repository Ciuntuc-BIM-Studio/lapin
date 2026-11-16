---
title: Pläne, PDF und DXF
order: 100
summary: Ordnen Sie Ansichten auf Plänen mit einem Plankopf an und geben Sie sie als PDF oder DXF aus.
---

<!-- video sheets -->

## Einen Plan erstellen

Klicken Sie im Projektbrowser auf **+** neben **Pläne**. Um eine Ansicht zu platzieren, ziehen Sie sie aus dem Browser auf den Plan oder wählen Sie sie in der Leiste des Plans unter **Ansicht platzieren…**. Jede Ansicht behält ihren eigenen Maßstab und bleibt mit dem Modell verknüpft, sodass sich der Plan ändert, wenn sich das Modell ändert. Auf dem Plan ziehen Sie eine Ansicht, um sie zu verschieben: nur sie bewegt sich, mit ihrem Titel, während Rahmen, Schriftfeld und die anderen Ansichten bleiben; sie rastet an den Kanten und der Mitte des Rahmens und an den anderen Ansichten ein, und die Pfeiltasten rücken sie um 1 mm (10 mm mit Umschalt).

![Ein A3-Plan mit einem Grundriss und einem Schnitt](shots/sheet.webp)

Wählen Sie den Plan aus, um im Eigenschaftenbereich seinen **Plankopf** auszufüllen: das Projekt, den Bauherrn, wer ihn gezeichnet hat, wer ihn geprüft hat, das Datum und den Revisionsstand. Seine Nummer, sein Papierformat und seine Ausrichtung werden dort ebenfalls festgelegt. Das Papier ist ein A-Format (A0 bis A4), eines der langen ISO-Formate (A0×2, A1×3, A2×3, A3×3, A3×4, A4×3), ein nordamerikanisches Format (Letter, Tabloid, ARCH D, ARCH E) oder **benutzerdefiniert**, mit seiner **Papierbreite** und **Papierhöhe** in Millimetern. Leer gelassen zeigt das Projektfeld den Namen des Projekts: den ihm gegebenen Namen, sonst den Namen seiner Datei.

## Was eine Ansicht zeigt

Eine Ansicht auf dem Plan zeigt, was Sie wählen, ohne sie blind zu ziehen:

- **Auf dem Plan zuschneiden.** Klicken Sie eine Ansicht an: An Ecken und Kanten erscheinen Griffe. Ziehen Sie sie, und der Rahmen fasst mehr oder weniger der Ansicht, während die Zeichnung auf dem Papier bleibt, wo sie ist. Mit **Umschalt**-Ziehen darin verschieben Sie die Zeichnung im Rahmen.
- **Ihre Grenze in der Ansicht zeichnen.** **Annotate → View boundary** (<kbd>VB</kbd>): Zeichnen Sie ein Rechteck in einen Grundriss, Schnitt oder eine Ansicht und geben Sie ihm einen im Projekt eindeutigen Namen. In **Place view…** steht die Ansicht dann mit jeder ihrer Grenzen („Ground floor plan › Ostflügel“), und eine so gesetzte Ansicht zeigt genau dieses Rechteck und folgt ihm, wenn Sie es in der Ansicht verschieben oder ändern. Dieselbe Ansicht kann auf mehreren Plänen stehen, je eine Grenze. Eine gesetzte Ansicht wechselt in der Liste auf ihrem Rahmen zwischen ihren Grenzen oder zum freien Zuschnitt. Grenzen sind nur am Bildschirm zu sehen, nie auf Papier.
- **Anheften.** Die Pinnnadel auf dem Rahmen hält die Ansicht fest: Sie wird gewählt, aber nicht versehentlich verschoben oder zugeschnitten.

Rahmen und Schriftfeld gehören zum Papier. Sie werden nie gewählt oder verschoben, und ein Klick nimmt die kleinste Ansicht unter dem Zeiger, sodass eine Ansicht über dem Schriftfeld immer wählbar ist.

![Eine auf dem Plan gewählte Ansicht: ihre Zuschnittgriffe, ihre Grenze, die Pinnnadel](shots/sheet-view-boundary.webp)

## Ein eigener Plankopf

**Verwalten → Planköpfe…** zeigt die Planköpfe: die von Lapin (**Lapin standard**, den bisher jeder Plan hatte, das rumänische **cartuș** und eines nach **ISO 7200**), die des Projekts und die der Bibliothek dieses Computers, die jedes Projekt auf ihm anbietet. Ein Klick zeigt einen mit den Werten des Projekts. **Ins Projekt duplizieren** erstellt eine bearbeitbare Kopie, **Neu** einen leeren; **Als Projektstandard festlegen** gibt es jedem Plan, der kein eigenes wählt, und ein Plan wählt sein eigenes unter **Plankopf** in seinen Eigenschaften.

![Planköpfe: die von Lapin, die des Projekts und die dieses Computers, das gewählte mit den Werten des Projekts](shots/title-blocks.webp)

**Bearbeiten…** öffnet den Designer. Der Plankopf ist ein Rechteck in Papiermillimetern, das in einer Ecke des Rahmens sitzt (standardmäßig unten rechts), so passt dasselbe Feld von A4 bis A0, hoch oder quer; die Ränder des Rahmens werden hier eingestellt. Gezeichnet wird mit **Linie**, **Rechteck**, **Polylinie**, **Text**, **Bild** (das Logo, PNG oder JPEG) und **Änderungstabelle**; ein Rechteck oder eine geschlossene Polylinie erhält eine Füllung oder Schraffur, ein Text eine Höhe oder einen Textstil der Zeichenstandards. Klicks fangen an Enden, Ecken und Mitten der Elemente und am Raster; **Umschalt** hält die Linie gerade. Ein Element ziehen verschiebt es, seine Griffe formen es um; die Pfeiltasten schieben es, **Entf** löscht es, **Strg+Z** macht rückgängig.

Die Liste **Felder** enthält den Live-Text: des Projekts (Name, Nummer, Bauherr, Grundstück, Genehmigung, Planer, Personen und eigene Parameter), des Plans (Nummer, Name, Maßstab, Papier, Änderung, Status, Datum, gezeichnet, geprüft und freigegeben von, Phase, Plan x von y) und des Systems (Druckdatum, Dateiname). Ein Klick setzt das Feld in den gewählten Text oder als neuen Text; ein Text kann Wörter und Felder mischen, `Projekt: {project.name}`. Der Designer zeigt den Wert jedes Felds auf einem Beispielplan; ein Feld ohne Wert erscheint grau mit seinem Namen, ein Lapin unbekanntes rot (gedruckt als `<Feld>`, und **Verwalten → Prüfen** meldet es). **Speichern** behält den Plankopf als einen rückgängig machbaren Schritt.

![Der Designer: ein Plankopf im finnischen Stil, seine Felder mit ihren Werten, ein Feld unter der Plannummer hinzugefügt](shots/title-block-designer.webp)

Die Pläne, ihr PDF, Drucken und die Ausgabe zeichnen es mit den Werten jedes Plans. In DXF ist es ein Block mit Attributen, eines je Live-Text (`PROJECT_NAME`, `SHEET_NUMBER`…), wie die Planköpfe in AutoCAD. **Exportieren…** speichert ein Plankopf als `.json`-Datei mit seinen Bildern, **Importieren…** liest es ins Projekt oder in die Bibliothek, so trägt ein Büro seine Planköpfe von einem Computer zum nächsten. Alte Projekte behalten Lapin standard und zeichnen genau wie bisher.

![Der Plan mit seinem eigenen Plankopf](shots/sheet-title-block.webp)

## Auf dem Plan zeichnen

Hinweise, Markierungen und Legenden, die zum Plan gehören und nicht zu einer seiner Ansichten, werden auf dem Plan selbst gezeichnet. Klicken Sie in der Leiste des Plans auf **Beschriften** oder doppelklicken Sie auf das Papier außerhalb der Ansichten (**Beschriften → Plan beschriften** tut dasselbe). Der Plan öffnet sich im Zeichenbereich als sein Papier, im Maßstab 1:1, mit den Ansichten, dem Rahmen und dem Plankopf darunter, und die Leiste zeigt *Zeichnung auf dem Plan*. Jedes Werkzeug von **Zeichnen** und **Beschriften** arbeitet dort wie in einer Ansicht, in Papier-Millimetern: Tippen Sie 50 ein, ist eine Linie 50 mm lang auf dem Papier, und die Höhe eines Textes ist seine Höhe auf dem Papier. Punkte rasten am Rahmen, am Plankopf, an den Rahmen der Ansichten (blau gestrichelt) und an dem ein, was die Ansichten zeichnen. **Plan** in der Leiste führt zurück zum Anordnen der Ansichten.

- **Texte** können Live-Felder enthalten wie ein Plankopf: `Plan {sheet.number} von {sheet.count}`, `{project.name}`, `{sheet.date}`. In einer Ansicht bleibt ein Text, wie er getippt wurde.
- **Hinweis mit Führungslinie** (Beschriften → Text): Klicken Sie, wohin der Pfeil zeigt, dann, wo der Text steht, und tippen Sie ihn. Verschoben nimmt der Hinweis seinen Pfeil mit; **Hinweislinie → entfernen** in seinen Eigenschaften nimmt den Pfeil weg.
- **Linien, Polylinien, Rechtecke, Kreise, Bögen, Schraffuren, Symbole und Bilder** sind die der Zeichenwerkzeuge, mit ihren Griffen, Layern und Ändern-Werkzeugen. Ein Nordpfeil ist eines der Symbole von Lapin (**Beschriften → Symbol**).
- **Revisionswolke** (Zeichnen → Füllung, Beschriften → Pläne oder REVCLOUD in der Befehlszeile): zwei Ecken, oder **P** für ein Polygon, das mit Enter geschlossen wird. **Revisionswolke** in den Eigenschaften jeder geschlossenen Polylinie oder jedes Kreises macht daraus eine Wolke oder wieder eine einfache Form und legt die Bogenlänge auf dem Papier fest.
- **Legende** (Beschriften → Pläne): eine lebendige Liste dessen, was die Ansichten des Plans zeichnen: die Materialien, mit denen ihre Schnitte schraffiert und gefüllt sind, oder, mit **Listen** in ihren Eigenschaften, ihre Linien nach Rolle, Typ und Stärke. Sie ändert sich mit den Ansichten und dem Modell, und auch ihr Titel nimmt Felder auf.
- **Maßstabsleiste** (Beschriften → Pläne): im Maßstab der ersten Ansicht des Plans oder im Maßstab aus ihren Eigenschaften, eine runde Länge von etwa 80 mm oder eine eigene. In einem Grundriss, Schnitt oder einer Ansicht nimmt eine Maßstabsleiste den Maßstab der Ansicht.

Was auf dem Plan gezeichnet ist, wird darauf gedruckt, über den Ansichten, genau wie angezeigt: im PDF, beim Drucken und im DXF. Es gehört zum Plan: **Plan duplizieren** (seine Schaltfläche im Projektbrowser) kopiert es mit den auf dem Plan platzierten Symbolen, und das Löschen des Plans löscht es. Jede Zeichnung ist ein Rückgängig-Schritt. Früher erstellte Pläne haben nichts darauf gezeichnet und werden wie bisher gedruckt.

![Zeichnen auf einem Plan: ein Hinweis mit Führungslinie, eine Revisionswolke, ein Live-Text, eine Legende der Materialien des Grundrisses und eine Maßstabsleiste](shots/sheet-annotations.webp)

![Derselbe Plan, wie er gedruckt wird](shots/sheet-annotated.webp)

## Drucken

Die Schaltflächen **PDF** und **DXF** in der Leiste des Plans exportieren den Plan. Ein PDF ist bereit zum Drucken oder Versenden. Ein DXF öffnet sich in AutoCAD und anderen CAD-Programmen mit seinen Layern, Linien und Schraffuren; Kreise, Bögen und Ellipsen (gezeichnete, oder die gekrümmten Kanten von Decken und anderen Umrissen im Grundriss) werden als echte CIRCLE-, ARC- und ELLIPSE-Objekte geschrieben, nicht als kurze Linien, und kommen beim erneuten Import des DXF als Kurven zurück. **Drucken** schickt den Plan in seinem eigenen Papierformat an Drucker oder Plotter, 1:1, als Vektorlinien: Plotter und Papier wählen Sie im Druckdialog. Auf einem kleineren Drucker wählen Sie Anpassen an die Seite.
