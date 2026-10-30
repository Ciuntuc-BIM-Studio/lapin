---
title: Materialien, Texturen und Schraffuren
order: 90
summary: Legen Sie für jedes Material Farbe, Schraffuren und Fugen fest, legen Sie Ihr eigenes Bild darauf und bringen Sie Ihre eigenen .pat-Schraffurmuster mit.
---

**Verwalten → Materialien** listet die Materialien des Projekts auf. Wählen Sie eines aus, um es zu ändern.

![Der Dialog Materialien](shots/materials-dialog.webp)

## Was ein Material festlegt

- **Farbe (3D)** und die Flächenzeichnung darauf in 3D. Lapin leitet sie aus dem Namen ab, zum Beispiel die Steinlagen eines Ziegels oder das Raster einer Fliese. **Enden** sowie **Oben und unten** können anders aussehen als die Flächen.
- **Schraffur** für jeden Ort, an dem es gezeichnet wird: geschnitten in einem Schnitt, geschnitten in einem Grundriss und gesehen in einer Ansicht. Jede hat ihren Maßstab, Winkel, ihre Ausrichtung, Füllung und ihren Stift.
- **Kategorie**, zum Beispiel Bekleidung, Dämmung oder Tragwerk. Die Ansichtsstile verwenden sie, um die Bekleidungen in einer Tragwerksansicht wegzulassen.
- **Folie** zeichnet eine dünne Schicht als gestrichelte Linie statt als Band.
- **Fugen zeichnen.** Standardmäßig läuft ein Material ohne Linie von einem Element ins nächste weiter, etwa bei zwei Wänden in einer Flucht oder bei Putz über zwei Geschosse. Aktivieren Sie die Option, um die Fuge zu zeichnen.

## Ihr eigenes Bild auf einem Material

Ein Material kann ein eigenes Bild tragen: ein Foto des echten Ziegels, Holzes oder Steins.

1. Wählen Sie das Material aus und klicken Sie auf **Eigenes Bild → Bild importieren…**. Wählen Sie eine PNG- oder JPEG-Datei.
2. Legen Sie **W** und **H** fest: wie breit und wie hoch, in Metern, eine Kopie des Bildes auf dem Modell ist. Das Bild wiederholt sich über die Flächen.

![Das eigene Bild eines Materials, aufgelegt mit 1.2 × 1.2 m](shots/own-image.webp)

Das Bild erscheint in der 3D-Ansicht und wird in der Projektdatei gespeichert. Es geht auch in die 3D-Exporte ein: **GLB**, **glTF** und **USDZ** nehmen es mit. **DAE** und **USDA** sind Einzeldateien ohne Platz für Bilder, daher behalten sie die Farbe des Materials.

![Dasselbe Haus mit dem eigenen Bild des Materials in 3D](shots/own-image-3d.webp)

Bilder, die größer als 2048 Pixel sind, werden beim Import auf diese Größe verkleinert. Die Papierkorb-Schaltfläche entfernt das Bild vom Material.

## Ihre eigenen Schraffurmuster (.pat)

**.pat importieren** liest eine AutoCAD-Schraffurmusterdatei. Jedes darin enthaltene Muster wird dem Projekt hinzugefügt und kann als Schraffur eines Materials gewählt werden.

Lapin bewahrt die Datei außerdem unter **Meine Muster** auf Ihrem Computer auf. In jedem anderen Projekt holt **Zu diesem Projekt hinzufügen** diese Muster wieder herein. Ein Projekt behält die Muster, die es verwendet, sodass es auf einem anderen Computer genauso aussieht.

![Meine Muster: die importierten .pat-Dateien, aufbewahrt für jedes Projekt](shots/my-patterns.webp)
