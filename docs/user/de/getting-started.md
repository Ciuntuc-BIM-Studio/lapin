---
title: Erste Schritte
order: 10
summary: Der Startbildschirm, ein neues Projekt, die Teile des Fensters, die Navigation in einer Zeichnung und die Wahl Ihrer Einheiten.
---

## Der Startbildschirm

Wenn Lapin öffnet, zeigt es den Startbildschirm. **Neues Projekt** beginnt ein leeres Gebäude. **Projekt öffnen…** öffnet eine `.lapin`-Datei. Die Projekte, die Sie zuvor geöffnet haben, stehen unter **Zuletzt verwendet**.

![Der Startbildschirm](shots/start-screen.webp)

Ein neues Projekt hat bereits drei Ebenen: **Foundations** (−1.50), **Ground floor** (±0.00) und **First floor** (+3.00). Es öffnet sich im Grundriss des Erdgeschosses, bereit für die erste Wand.

## Das Fenster

![Die Teile des Lapin-Fensters](shots/workspace.webp)

1. **Die Registerkarten des Menübands.** Zeichnen, Architektur, Tragwerk, Gelände, Studio, Beschriften, Verwalten, Mengen und Ansicht gruppieren jeweils ihre Werkzeuge. **Datei** öffnet, speichert, importiert und exportiert. **Rückgängig** und **Wiederholen** befinden sich am rechten Ende derselben Leiste (<kbd>Strg</kbd> <kbd>Z</kbd>, <kbd>Strg</kbd> <kbd>Y</kbd>).
2. **Das Menüband.** Es enthält die Werkzeuge der aktuellen Registerkarte. Fahren Sie mit der Maus über ein Werkzeug, um seinen Namen und seine Tastaturbuchstaben zu sehen.
3. **Der Projektbrowser.** Er listet die Ebenen, Grundrisse, Schnitte, Ansichten, 3D-Ansichten, Pläne und Bauteillisten auf. Doppelklicken Sie auf einen Eintrag, um ihn in einer Registerkarte zu öffnen. Ein Projekt öffnet sich im Grundriss des Erdgeschosses, mit der 3D-Ansicht in einer Registerkarte daneben.
4. **Die Ansicht.** Hier zeichnen Sie: in einem Grundriss, einem Schnitt, einer 3D-Ansicht oder auf einem Plan. Mit den beiden Schaltflächen oben rechts in der Ansicht können Sie mehrere Ansichten nebeneinander öffnen.
5. **Die Schnellwerkzeuge.** Sie legen die meistgenutzten Werkzeuge auf die Zifferntasten <kbd>1</kbd>–<kbd>9</kbd>.
6. **Die Befehlszeile.** Geben Sie hier die Buchstaben eines Werkzeugs ein und drücken Sie <kbd>Enter</kbd>. Während ein Werkzeug läuft, sagt Ihnen die Zeile, was es als Nächstes braucht. Siehe [Zeichnen und die Befehlszeile](drafting.md).
7. **Der Eigenschaftenbereich.** Er zeigt, was gerade ausgewählt ist. Ist nichts ausgewählt, zeigt er die vorne liegende Ansicht.
8. **Ask Me.** Das ist der Hilfeassistent von Lapin. Siehe [Hilfe und Feedback](help.md).

Sie möchten mehr Platz für die Zeichnung? **Ansicht → Oberfläche…** wählt ein kompakteres Fenster, und die Seitenbereiche lassen sich einklappen. Siehe [Die Oberfläche und Tastenkürzel](interface.md).

## Navigieren

- **Zoomen.** Drehen Sie das Mausrad oder ziehen Sie auf einem Trackpad zwei Finger auseinander oder zusammen. Lapin zoomt dorthin, wo der Cursor steht.
- **Verschieben.** Ziehen Sie mit der mittleren oder der rechten Maustaste oder mit zwei Fingern auf einem Trackpad.
- **Orbit in 3D.** Ziehen Sie mit der linken Maustaste.
- **Alles anzeigen.** Klicken Sie auf die Schaltfläche **Einpassen**, geben Sie <kbd>ZE</kbd> ein oder doppelklicken Sie mit der mittleren Taste.

## Einheiten

Lapin arbeitet in Millimetern, sofern Sie nichts anderes wählen. Um die Einheiten zu ändern, öffnen Sie **Verwalten → Zeichnungsstandards → Projekt → Units** oder klicken Sie in der Statusleiste unten im Fenster auf **Units**. Sie können Millimeter, Zentimeter, Meter, Fuß und Zoll oder nur Zoll wählen.

![Zeichnungsstandards, Registerkarte Projekt: die Einheiten des Projekts](shots/standards-project.webp)

Jede Länge folgt dieser Einstellung: die Bereiche, die Werte, die Sie beim Zeichnen eingeben, die Befehlszeile und die Bemaßungen. Das Modell selbst wird in Metern geführt, sodass eine Änderung der Einheiten Ihr Gebäude nie verändert.

## Speichern

**Datei → Speichern** (<kbd>Strg</kbd> <kbd>S</kbd>) schreibt eine `.lapin`-Datei. Siehe [Dateien, Versionen und Wiederherstellung](files.md).
