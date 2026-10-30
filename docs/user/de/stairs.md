---
title: Treppen
order: 42
summary: Platzieren Sie eine Treppe zwischen Geschossen, richten Sie sie in ihrem Typ ein und erstellen Sie die wenigen Stufen hinauf zu einem Vorplatz oder einer Tür.
---

## Treppen

Wählen Sie **Architektur → Treppe** (<kbd>ST</kbd>). Klicken Sie auf den Antritt der Treppe (die Mitte ihrer ersten Steigung) und dann auf einen Punkt in der Richtung, in die sie ansteigt. Bei einer L- oder U-Treppe legt ein dritter Klick links oder rechts des Laufs fest, in welche Richtung sie sich wendet. Die Treppe steigt von ihrem Geschoss bis zum Geschoss darüber. Lapin ermittelt die Anzahl der Steigungen, die Steigungshöhe und den Auftritt aus der Höhe und den Regeln des Typs.

Wählen Sie die Treppe aus, um sie im Eigenschaftenbereich zu ändern:

- **Treppentyp.** Die Form, die Regeln, die Konstruktion und das Geländer stammen aus dem Typ, der in **Architektur → Treppentypen** eingerichtet wird.
- **Breite**, **Wendung**, **Versatz unten** und **Versatz oben.**
- **Höhenunterschied.** Lassen Sie das Feld leer, und die Treppe steigt bis zum Geschoss darüber. Ein Wert gibt der Treppe einen eigenen Höhenunterschied, unabhängig von den Geschossen. Verwenden Sie ihn für einige Stufen zwischen zwei Höhen auf demselben Geschoss.
- **Fest.** Steigungen, Steigungshöhe, Auftritt und Podestlänge, wenn Sie sie festlegen möchten. Lassen Sie sie leer, damit die Regeln entscheiden.
- **Prüfungen.** Jeder Wert im Vergleich zu den Regeln des Typs, in Grün oder Rot.

Eine Treppe verlangt von der Decke darüber eine Öffnung über sich. Entfernen Sie das Häkchen bei **Öffnung in der Decke darüber**, wenn die Decke geschlossen bleiben soll.

## Eingangsstufen (ein Vorplatz)

Die Stufen hinauf zu einer Haustür haben einen eigenen Höhenunterschied: vom Gelände, einige Stufen unterhalb des Erdgeschosses, hinauf bis ±0.00. Lapin hat dafür einen fertigen Treppentyp, **Eingangsstufen**: 2 m breit, drei Steigungen zu 150 mm, massiver Beton, auf dem Erdreich betoniert, ohne Geländer.

![Eingangsstufen vor einem Vorplatz, im Grundriss](shots/entrance-steps-plan.webp)

1. **Der Vorplatz.** Zeichnen Sie das Podest als Decke: Wählen Sie **Architektur → Decke** (<kbd>SB</kbd>) und klicken Sie ihren Umriss außerhalb der Wand vor der Tür. Die Oberkante einer Decke liegt auf der Höhe ihres Geschosses (±0.00 im Erdgeschoss), und ihr Typ bestimmt ihre Dicke. Um die Dicke zu ändern, erstellen Sie in **Architektur → Deckentypen** einen Deckentyp für Vorplätze, zum Beispiel 150 mm Beton.
2. **Die Stufen.** Wählen Sie **Architektur → Eingangsstufen**. Klicken Sie auf die Mitte der Kante des Vorplatzes und dann auf die Seite, zu der die Stufen hinausführen. Die Stufen reichen bis an die Kante des Vorplatzes und enden bei ±0.00. Sie beginnen 450 mm tiefer: Ihr **Versatz unten** beträgt −450 mm.
3. **Die Höhe.** Wählen Sie die Stufen aus. Geben Sie in **Höhenunterschied** den tatsächlichen Unterschied zwischen dem Gelände und dem Vorplatz ein, zum Beispiel 300. Die Oberkante bleibt am Vorplatz und der Antritt wandert nach oben: Der Versatz unten wird zu −300 mm, und die Stufen haben zwei Steigungen. **Breite** macht sie so breit wie den Vorplatz, zum Beispiel 2400.

![Die Eigenschaften der Eingangsstufen: Höhenunterschied, Versatz unten, Ebenen](shots/entrance-steps-properties.webp)

**Ebenen** zeigt, wo die Stufen beginnen und enden, zum Beispiel −0.45 → ±0.00. Mit einer Steigung (einem Höhenunterschied von 150 mm) ist die Stufe die Kante des Vorplatzes selbst, und es gibt keinen Auftritt zu bauen. Geben Sie den Stufen zwei oder mehr Steigungen, um Auftritte vor dem Vorplatz zu erhalten.

Der Typ wird dem Projekt hinzugefügt, wenn Sie ihn zum ersten Mal verwenden. Um ihn zu ändern, öffnen Sie **Architektur → Treppentypen** und wählen Sie **Eingangsstufen**:

- **Form → eigener Höhenunterschied.** Der Höhenunterschied des Typs; jede Treppe kann ihn mit **Höhenunterschied** weiterhin ändern.
- **Regeln → Regelsatz → Eingangsstufen.** Steigungen von 120–170 mm und Auftritte von 300–400 mm, wobei eine einzelne Steigung erlaubt ist.
- **Konstruktion → Ausführung.** **Massiv, auf dem Erdreich betoniert** füllt die Stufen bis zu ihrer Unterseite aus. **Laufplatte unter den Stufen** baut sie wie eine Innentreppe.

![Der Typ Eingangsstufen im Treppenkonfigurator](shots/stair-types.webp)

Damit ein anderer Treppentyp einen eigenen Höhenunterschied überwindet, setzen Sie auf seiner Registerkarte **Form** das Häkchen bei **eigener Höhenunterschied**. Um eine platzierte Treppe in Eingangsstufen umzuwandeln, wählen Sie **Eingangsstufen** als ihren **Treppentyp**: Sie wandert nach unten, sodass ihre Oberkante auf der Höhe ihres Geschosses liegt.

![Das Haus mit seinem Vorplatz und den Eingangsstufen in 3D](shots/entrance-steps-3d.webp)

In IFC sind die Stufen eine IfcStair vom Typ STRAIGHT_RUN_STAIR. Ihr Lauf ist ein IfcStairFlight mit NumberOfRisers, NumberOfTreads, RiserHeight und TreadLength, und Pset_StairCommon trägt dieselben Werte. Der Vorplatz ist ein IfcSlab.
