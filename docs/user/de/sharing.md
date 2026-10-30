---
title: Zusammenarbeiten
order: 125
summary: Teilen Sie das geöffnete Projekt und arbeiten Sie live mit anderen darin, aus Lapin oder aus einem Browser.
---

<!-- video sharing -->

Mehrere Personen können gleichzeitig im selben Projekt arbeiten, so wie Spieler einem Spielserver beitreten. Eine Person teilt das Projekt aus Lapin auf ihrem Computer: Sie ist der **Gastgeber**. Die anderen treten als **Gäste** bei, aus Lapin auf ihrem eigenen Computer oder aus einem Browser, mit dem Link, den der Gastgeber ihnen sendet.

Das Projekt bleibt auf dem Computer des Gastgebers, und das Lapin des Gastgebers entscheidet über jede Änderung. Gäste sehen das Modell, während es sich ändert, zeichnen mit denselben Werkzeugen und sehen, wo die anderen sind.

## Das Projekt teilen

**Datei → Sitzung teilen…**, geben Sie Ihren Namen ein, wählen Sie, wie Gäste beitreten, und klicken Sie dann auf **Teilen**:

- **Bearbeiter** modellieren wie Sie;
- **Betrachter** schauen zu und bewegen sich, ändern aber nichts.

Lapin zeigt einen **Link** und einen **Code**. Senden Sie den Link an alle, die aus einem Browser beitreten, und den Code an alle, die aus Lapin beitreten. Wer sie hat, kann beitreten, bis Sie auf **Teilen beenden** klicken. Bei jedem Teilen sind Link und Code neu.

![Teilen: der Link, der Code und wer in der Sitzung ist](shots/share-dialog.webp)

Im selben Dialog können Sie einen Gast zum Bearbeiter oder Betrachter machen oder ihn **zum Verlassen auffordern**. Schließen Sie den Dialog, und die Sitzung läuft weiter. Das Personensymbol neben den Teilnehmern in der oberen Leiste öffnet ihn wieder.

Solange das Projekt geteilt ist und eine Datei hat, speichert Lapin es alle 5 Minuten. Nur der Gastgeber speichert; Gäste können IFC- und 3D-Dateien für sich selbst exportieren.

## Einer Sitzung beitreten

- **Aus einem Browser**: Öffnen Sie den Link, geben Sie Ihren Namen ein und klicken Sie auf **Verbinden**. Nichts zu installieren.
- **Aus Lapin**: Wählen Sie auf dem Startbildschirm oder im Menü **Datei** den Befehl **Sitzung beitreten…**, fügen Sie den Link oder den Code ein und geben Sie Ihren Namen ein.

![Beitreten aus Lapin](shots/join-dialog.webp)

Eine Leiste unten zeigt, in wessen Sitzung Sie sind. **Verlassen** bringt Sie zurück zu Ihrem eigenen Lapin. Wenn der Gastgeber das Teilen beendet oder Lapin schließt, teilt Lapin Ihnen das mit, und Sie kehren mit **Zurück zu Lapin** zurück.

## Wer ist wo

Die Teilnehmer stehen in der oberen Leiste, jeder in seiner Farbe. Klicken Sie auf einen, um zu der Ansicht zu wechseln, die er gerade betrachtet.

![Die Teilnehmer](shots/participants.webp)

Im Grundriss sehen Sie die Cursor der anderen mit ihren Namen und das, was sie ausgewählt haben, in ihrer Farbe umrandet.

![Cursor und Auswahl eines Gastes im Grundriss des Gastgebers](shots/sharing-plan.webp)

## Rückgängig gilt nur für Ihre eigenen Änderungen

<kbd>Strg Z</kbd> macht Ihre letzte Änderung rückgängig, nicht die eines anderen. Wenn jemand das Element nach Ihnen geändert hat, lässt Lapin es, wie es ist, und sagt Ihnen, wer es war. Ihr Rückgängig geht dann nicht weiter zurück als bis zu dieser Änderung.

## Sicher durch Bauweise

- Alles zwischen Gastgeber und Gästen läuft über Lapins Relay, **Ende-zu-Ende-verschlüsselt** mit dem Schlüssel der Einladung. Das Relay leitet es weiter und kann es nicht lesen.
- Ein Gast kann auf dem Computer des Gastgebers keine Dateien öffnen, speichern oder lesen und dort auch kein anderes Projekt öffnen.
- Lapin öffnet nichts zum Netzwerk hin, bis Sie teilen, und hört auf, wenn Sie das Teilen beenden.
