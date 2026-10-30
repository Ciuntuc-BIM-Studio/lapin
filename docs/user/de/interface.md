---
title: Die Oberfläche und Tastenkürzel
order: 15
summary: Die Modi Komfortabel, Kompakt und Fokus, das Einklappen der Seitenbereiche und die Tastenkürzel, die Sie ändern können.
---

<!-- video interface -->

## Drei Modi

**Ansicht → Oberfläche…** (<kbd>Strg</kbd> <kbd>.</kbd>) legt fest, wie viel um die Ansicht herum steht. Wählen Sie oben im Dialog einen der drei Modi; die Änderung ist sofort zu sehen.

![Der Dialog Oberfläche: die drei Modi und jeder Teil für sich](shots/interface-dialog.webp)

- **Komfortabel** ist das Fenster, wie es die anderen Seiten zeigen: das Menüband mit großen Werkzeugen und ihren Namen, eine Befehlszeile unter jeder Zeichnung sowie über der Ansicht das HUD, der Hinweis, die Felder für Länge und Winkel, der Übersichtsplan und die Schnellwerkzeuge.
- **Kompakt** überlässt den größten Teil des Fensters der Ansicht:

![Kompakt: mehr Platz für die Zeichnung](shots/interface-compact.webp)

1. **Das Menüband** ist eine einzige Symbolleiste. Dünne Linien trennen die Gruppen. Fahren Sie mit der Maus über ein Werkzeug, um seinen Namen, seine Tasten und seine Gruppe zu sehen.
2. **Der Projektbrowser** klappt zu einer Leiste ein. Ihre Symbole öffnen das Projekt, die Ebenen oder den IFC-Baum kurzzeitig über der Ansicht.
3. **Die Schnellwerkzeuge** <kbd>1</kbd>–<kbd>9</kbd> stehen am linken Rand der Ansicht.
4. **Der Übersichtsplan** ist eine Schaltfläche. Klicken Sie darauf, um die Karte zu öffnen, und auf ihr ×, um sie wieder einzuklappen.
5. **Eine einzige Statuszeile** enthält den Namen des Werkzeugs und das, was es abfragt, das, was Sie eingeben, während des Zeichnens die Felder für Länge und Winkel, SNAP, ORTHO und POLAR, die Ebene und die Ansicht, dann das Projekt.

- **Fokus** behält nur die Registerkarten und eine schmale Befehlszeile. Klicken Sie auf eine Registerkarte, und ihre Werkzeuge klappen über der Ansicht auf; das Band klappt wieder ein, sobald Sie ein Werkzeug wählen, oder mit <kbd>Esc</kbd> oder einem Klick an eine andere Stelle. Die Bereiche werden ausgeblendet. Ist etwas ausgewählt, zeigt eine kleine Karte, was es ist, und seine ersten Eigenschaften; **Alle Eigenschaften…** öffnet den Eigenschaftenbereich über der Ansicht. <kbd>Tab</kbd> holt beide Bereiche zurück und blendet sie wieder aus.

![Fokus: die Auswahl in einer Karte](shots/interface-focus.webp)

In jedem Modus behält die obere Leiste **Rückgängig** und **Wiederholen** an ihrem rechten Ende: mit ihren Namen in Komfortabel, als zwei Pfeile in Kompakt und Fokus (<kbd>Strg</kbd> <kbd>Z</kbd> und <kbd>Strg</kbd> <kbd>Y</kbd>; ihre QuickInfo sagt, was sie rückgängig machen).

Unter den Modi lässt sich jeder Teil für sich einstellen: das Menüband, die Schnellwerkzeuge (oder keine: die Zifferntasten funktionieren weiterhin), die Befehlszeile, das HUD, der Hinweis und die Felder des Werkzeugs, der Übersichtsplan, die Bereiche und die Höhe der Eigenschaftszeilen. Wenn Sie einen Teil ändern, wird die Auswahl zu **Benutzerdefiniert**.

Lapin speichert Ihre Wahl auf diesem Computer, für Sie. Die Desktop-App und der Browser speichern ihre jeweils getrennt. Im Browser startet Lapin im Modus Kompakt, wenn das Fenster weniger als 800 Pixel hoch ist, bis Sie eine Wahl treffen.

## Die Seitenbereiche einklappen

In jedem Modus klappen der Projektbrowser und der Eigenschaftenbereich zu einer schmalen Leiste an ihrer Seite ein:

- die Schaltfläche oben rechts im Bereich klappt ihn ein, und der Pfeil auf seiner Leiste klappt ihn aus;
- doppelklicken Sie auf den Rand zwischen dem Bereich und der Ansicht;
- <kbd>Strg</kbd> <kbd>B</kbd> klappt den Browser ein oder aus, <kbd>Strg</kbd> <kbd>⇧</kbd> <kbd>B</kbd> die Eigenschaften und <kbd>Tab</kbd> beide.

Klicken Sie auf ein Symbol auf der Leiste eines eingeklappten Bereichs, um den Bereich über der Ansicht zu öffnen, ohne ihn auszuklappen. Er verschwindet mit <kbd>Esc</kbd> oder einem Klick an eine andere Stelle; seine Pinnnadel hält ihn offen. Ziehen Sie am Rand eines Bereichs, um ihn breiter zu machen; Lapin merkt sich die Breite.

![Ein eingeklappter Browser, von seiner Leiste aus über der Ansicht geöffnet](shots/browser-overlay.webp)

## Tastenkürzel

**Ansicht → Tastenkürzel…** (<kbd>KS</kbd>) listet jeden Befehl, den die Tastatur erreichen kann, nach Registerkarte. Jede Zeile zeigt die Tastenkürzel des Befehls.

![Die Tastenkürzel, gesucht nach „wall“](shots/shortcuts-dialog.webp)

- **Suchen** Sie nach dem Namen eines Befehls oder nach einer Taste.
- **+** fügt ein Tastenkürzel hinzu: Drücken Sie eine Kombination (<kbd>Strg</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>), oder geben Sie bis zu drei Buchstaben ein und drücken Sie <kbd>Enter</kbd>. <kbd>Esc</kbd> bricht ab.
- Hat ein anderer Befehl das Tastenkürzel bereits, sagt Lapin, welcher. **Reassign** nimmt es diesem Befehl weg, **Beide behalten** überlässt es beiden (der erste in der Liste gewinnt), **Abbrechen** ändert nichts.
- **×** entfernt ein Tastenkürzel. Der Pfeil am Ende einer Zeile setzt diesen Befehl so zurück, wie ihn die Vorlage hat, und **Alles zurücksetzen** tut das für jeden Befehl.
- **Vorlage** wählt den Satz, von dem Sie ausgehen: **Revit-Stil** (die Voreinstellung: die üblichen Buchstaben von Revit, wo Lapin den Befehl hat) oder **Lapin klassisch** (Lapins eigene Buchstaben von früher).
- **Exportieren…** schreibt Ihre Tastenkürzel in eine Datei, und **Importieren…** liest eine ein, für einen anderen Computer oder einen Kollegen.

QuickInfos, die Menüs und die Befehlspalette (<kbd>Strg</kbd> <kbd>K</kbd>) zeigen die Tastenkürzel immer so, wie sie gerade sind.

### Buchstaben und die Befehlszeile

Buchstaben wie <kbd>WA</kbd> funktionieren, wenn kein Feld den Fokus hat und die Befehlszeile leer ist: Geben Sie sie über der Ansicht ein. Der Befehl wird sofort ausgeführt. Beginnt ein Befehl der Befehlszeile mit denselben Buchstaben (<kbd>DI</kbd> und <kbd>DIMADD</kbd>), wartet Lapin einen Moment: Tippen Sie weiter, um den längeren Befehl zu schreiben, oder drücken Sie <kbd>Enter</kbd> oder <kbd>Leertaste</kbd>, um den kürzeren sofort auszuführen. Während ein Befehl nach einem Punkt oder einem Wert fragt, gehen Buchstaben und Ziffern an ihn. Befehle der Befehlszeile wie <kbd>L</kbd>, <kbd>M</kbd> oder <kbd>TRIM</kbd>, gefolgt von <kbd>Enter</kbd>, funktionieren in jeder Vorlage. <kbd>Esc</kbd> bricht immer ab.

### Die Standard-Tastenkürzel

Dies sind die Tastenkürzel im Revit-Stil, wie Lapin sie jetzt hat:

<!-- shortcuts -->

Die Zifferntasten <kbd>1</kbd>–<kbd>9</kbd> wählen die Schnellwerkzeuge.
