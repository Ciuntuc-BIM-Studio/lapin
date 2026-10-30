---
title: Wände
order: 20
summary: Zeichnen Sie Wände Ecke für Ecke, ändern Sie sie im Eigenschaftenbereich und legen Sie Wandtypen mit ihren Schichten an.
---

<!-- video walls -->

## Wände zeichnen

Öffnen Sie die Registerkarte **Architektur** und wählen Sie **Wand**, oder geben Sie <kbd>WA</kbd> ein.

![Die Registerkarte Architektur](shots/ribbon-architecture.webp)

Klicken Sie dort, wo die Wand beginnt, und dann an jeder Ecke. Die nächste Wand folgt der Maus. Während Sie zeichnen, können Sie statt zu klicken eine Länge und einen Winkel in die Felder über der Ansicht eingeben. Drücken Sie <kbd>Enter</kbd> oder <kbd>Esc</kbd>, um aufzuhören.

![Wände zeichnen: die nächste Wand folgt der Maus, Länge und Winkel werden angezeigt](shots/wall-drawing.webp)

Wo zwei Wände aufeinandertreffen, verbindet Lapin sie selbst. Ihre Schichten laufen so ineinander, wie sie gebaut würden.

- **Gebogene Wand** (<kbd>WC</kbd>) zeichnet eine Wand entlang eines Bogens.
- **Vorhangfassade** (<kbd>CW</kbd>) zeichnet eine verglaste Wand mit Pfosten, Riegeln und Paneelen.
- Wände verbinden sich von selbst, wo sie aufeinandertreffen: eine Ecke als Gehrung, eine Wand, die auf einer anderen endet, als T. **Verbinden** (<kbd>JW</kbd>), **Gehrung** und **Stumpf** ändern, wie zwei Wände aufeinandertreffen, wenn das nicht gewollt ist; **Neue Ecken auf Gehrung** macht Stumpf zur Vorgabe auf diesem Rechner.
- **Tauschen** ändert, welche der beiden Wände durchläuft.

## Eine Wand ändern

Klicken Sie auf eine Wand, um sie auszuwählen. Ihre Griffe erscheinen. Ziehen Sie die Enden, um sie zu verlängern oder zu verkürzen, oder ziehen Sie das Kreuz in der Mitte, um sie zu verschieben.

![Eine ausgewählte Wand mit ihren Griffen](shots/wall-selected.webp)

Der Eigenschaftenbereich zeigt die ausgewählte Wand. Oben im Bereich stehen ihr Typ und ihre Schichten. Darunter folgen Länge, Höhe, Versatz unten, Geschoss und Bauphase. Ändern Sie einen beliebigen Wert, und das Modell folgt.

![Die Eigenschaften einer Wand](shots/wall-properties.webp)

**Umkehren** dreht die Schichten der Wand auf die andere Seite. Sie können auch auf die kleinen Pfeile klicken, die im Grundriss neben einer ausgewählten Wand angezeigt werden.

## Wandtypen

Ein Wandtyp ist eine Folge von Schichten, von der Außenseite zur Innenseite. Jede Schicht hat ein Material und eine Dicke, und eine Schicht ist der **Kern**. Die Achse der Wand verläuft entlang des Kerns. Um die Typen zu bearbeiten, wählen Sie **Architektur → Wandtypen**.

![Wandtypen: die Schichten einer Ziegelwand](shots/wall-types.webp)

- **+ Schicht** fügt eine Schicht hinzu. Die Pfeile verschieben eine Schicht nach oben oder unten.
- **Zonen in der Höhe** teilt den Kern von unten nach oben, zum Beispiel in eine erste Steinlage, das Mauerwerk und einen Ringbalken.
- **Stürze** wählt die Stürze über Türen und Fenstern: Fertigteil oder Ortbeton, mit ihrem Auflager und ihrer Höhe.
- **Damit zeichnen** schließt den Dialog und beginnt, Wände dieses Typs zu zeichnen.

Eine Änderung an einem Typ ändert jede Wand dieses Typs.

## Wände unter einem Dach

Wände auf derselben Ebene wie ein Dach enden von selbst unter ihm und steigen bis zu seinen Giebeln. Bei einer Wand auf einer anderen Ebene wählen Sie sie aus und wählen **Architektur → An Dach anbinden** (<kbd>AR</kbd>), dann klicken Sie auf das Dach – oder auf einen anderen Körper, bis zu dem die Wand reichen soll: einen Studio-Volumenkörper, ein importiertes Element. Siehe [Decken und Dächer](floors-roofs.md).
