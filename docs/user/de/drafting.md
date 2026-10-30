---
title: Zeichnen und die Befehlszeile
order: 80
summary: Geben Sie Befehle wie in CAD ein, fangen Sie Punkte, verschieben und kopieren Sie, zeichnen Sie Linien und beschriften Sie mit Bemaßungen, Texten und Beschriftungen.
---

## Die Befehlszeile

Geben Sie die Buchstaben eines Werkzeugs an beliebiger Stelle im Fenster ein und drücken Sie dann <kbd>Enter</kbd>. Geben Sie zum Beispiel <kbd>WA</kbd> für eine Wand ein, <kbd>CO</kbd> zum Kopieren, <kbd>M</kbd> zum Verschieben oder <kbd>L</kbd> für eine Linie. Die Befehlszeile unten zeigt, was das Werkzeug als Nächstes braucht. Sie können einen Punkt anklicken oder eine Länge, einen Winkel oder Koordinaten eingeben.

![Eine Wand kopieren: Nach dem Basispunkt zeigen die Befehlszeile und die Live-Eingabe Länge und Winkel bis zur Maus](shots/command-line.webp)

Jedes Werkzeug zeigt seine Buchstaben in seinem Tooltip; Sie können sie unter **Ansicht → Tastenkürzel…** ändern (siehe [Die Oberfläche und Tastenkürzel](interface.md)). Diese werden am häufigsten verwendet:

| Modell | | Zeichnen | |
|---|---|---|---|
| Wand | <kbd>WA</kbd> | Linie | <kbd>L</kbd> |
| Tür, Fenster | <kbd>DR</kbd>, <kbd>WN</kbd> | Polylinie | <kbd>PL</kbd> |
| Decke | <kbd>SB</kbd> | Rechteck, Kreis, Bogen | <kbd>REC</kbd>, <kbd>C</kbd>, <kbd>A</kbd> |
| Dach | <kbd>RF</kbd> | Verschieben, Kopieren | <kbd>M</kbd>, <kbd>CO</kbd> |
| Treppe, Raum | <kbd>ST</kbd>, <kbd>RM</kbd> | Drehen, Spiegeln, Skalieren | <kbd>RO</kbd>, <kbd>MI</kbd>, <kbd>SC</kbd> |
| Stütze, Träger | <kbd>CL</kbd>, <kbd>BM</kbd> | Versatz, Stutzen, Dehnen | <kbd>O</kbd>, <kbd>TR</kbd>, <kbd>EX</kbd> |
| Schnitt | <kbd>SE</kbd> | Abrunden, Fase | <kbd>F</kbd>, <kbd>CHA</kbd> |
| Messen | <kbd>ME</kbd> | Schraffur, Text | <kbd>H</kbd>, <kbd>T</kbd> |
| Alles einpassen | <kbd>ZE</kbd> | Löschen | <kbd>E</kbd> |

## Fang und Hilfsmittel

Lapin fängt Endpunkte, Mittelpunkte und Schnittpunkte des Gezeichneten sowie das Raster. <kbd>F3</kbd> schaltet den Fang ein oder aus, <kbd>F8</kbd> Ortho und <kbd>F10</kbd> den polaren Spurfang. Ihr Zustand wird unten rechts in der Ansicht angezeigt.

## Kopieren und Verschieben

Zum Kopieren wählen Sie das Element aus und geben <kbd>CO</kbd> ein. Klicken Sie einen Basispunkt an, dann einen zweiten Punkt. Lapin kopiert weiter, bis Sie <kbd>Enter</kbd> oder <kbd>Esc</kbd> drücken. **Verschieben** (<kbd>M</kbd>) funktioniert genauso.

Während Sie verschieben oder kopieren, bleibt eine Markierung auf dem Basispunkt, und eine gestrichelte Linie führt von dort zur Maus; ein blasser Umriss dessen, was Sie verschieben, bleibt an der alten Stelle, bis Sie klicken oder <kbd>Esc</kbd> drücken. Linie und Markierung wechseln ihre Farbe mit dem Fang: grün auf einem gefangenen Punkt, blau, wenn Ortho oder der polare Spurfang die Richtung hält, in der Akzentfarbe auf einer Spurlinie, grau, wenn frei. Neben der Maus zeigt Lapin, wie weit Sie verschoben haben: Δx, Δy, den Abstand und den Winkel, in den Einheiten des Projekts. Sie können Abstand und Winkel dort eingeben, wie beim Zeichnen. In der Oberfläche Kompakt stehen diese Werte in der Statuszeile. Das Ziehen eines Griffs zeigt dasselbe, ebenso das Ziehen eines ausgewählten Elements selbst, ab dem Punkt, an dem Sie gedrückt haben; drücken Sie <kbd>Esc</kbd>, bevor Sie loslassen, und das Element bleibt, wo es war.

![Eine Wand verschieben: die Markierung auf dem Basispunkt, die gestrichelte Linie zur Maus, das Geisterbild der Wand an der alten Stelle und der Versatz an der Maus](shots/move-trace.webp)

Sie können stattdessen auch ziehen. Ziehen Sie das Kreuz in der Mitte eines ausgewählten Elements, um es zu verschieben. Halten Sie beim Ziehen <kbd>Strg</kbd> (<kbd>⌘</kbd> auf einem Mac) gedrückt, um eine Kopie zurückzulassen.

Eine Rasterachse lässt sich allein oder zusammen mit anderen Elementen verschieben, kopieren, drehen und spiegeln. Eine Kopie ist eine neue Achse desselben Rasters, benannt nach der letzten Achse in ihrer Richtung: Bei den Achsen 1, 2 und 3 ist eine Kopie einer beliebigen von ihnen die 4.

## Zeichnen

Die Registerkarte **Zeichnen** zeichnet Linien, Polylinien, Rechtecke, Kreise und Bögen. Sie bearbeitet sie mit Versatz, Stutzen, Dehnen, Abrunden, Fase und Verbinden, füllt Flächen mit Schraffuren und ordnet sie auf **Layer** an. **Import DXF** holt eine CAD-Zeichnung als Linien herein, die Sie fangen und bearbeiten können.

![Die Registerkarte Zeichnen](shots/ribbon-draft.webp)

## Beschriften

Die Registerkarte **Beschriften** enthält die Bemaßungen, Texte, Rasterachsen, Ebenen und Beschriftungen. Eine Bemaßung liest das Modell, sie aktualisiert sich also, wenn eine Wand verschoben wird. Wie Texte, Bemaßungen, Achsen und Beschriftungen aussehen, legen Sie unter **Verwalten → Zeichnungsstandards** fest.

Eine ausgerichtete oder lineare Bemaßung entsteht als Kette: Nach den ersten beiden Punkten und ihrer Linie fügt jeder Klick den nächsten Punkt auf derselben Linie hinzu, und **Enter** (oder **Esc**) macht die ganze Kette zu einer Bemaßung (**U** nimmt den letzten Punkt zurück; geben Sie zuerst **S** ein für eine Einzelbemaßung). Eine Kette verhält sich wie eine Polylinie: Ein Klick wählt sie ganz aus, und sie wird als Ganzes verschoben, kopiert und gelöscht. Eine Kette, die Wände, Kanten oder Rasterachsen misst, bleibt an ihnen: Allein gezogen oder verschoben, gleitet nur ihre Maßlinie, rechtwinklig zur Kette, sodass ein Ausrutscher der Maus sie nie ins Leere messen lässt; zusammen mit dem Gemessenen verschoben, geht sie mit. Ihre Griffe sitzen auf jedem gemessenen Punkt (ziehen, um ihn zu verschieben, Strg/⌘-Klick, um ihn herauszunehmen), in der Mitte jedes Segments (ziehen, um einen Punkt hinzuzufügen, Strg/⌘-Klick, um einen auf halber Strecke hinzuzufügen) und an den Enden der Maßlinie (ziehen, um die Linie zu verschieben). Um weitere Punkte in eine Kette zu setzen, wie in ArchiCAD, wählen Sie sie aus und klicken auf **Beschriften → Punkte hinzufügen** (oder geben **DIMADD** ein, oder **Punkte hinzufügen** in ihren Eigenschaften): Jeder Punkt, den Sie anklicken, kommt dort in die Kette, wo er entlang ihrer Linie liegt, zwischen die Punkte um ihn herum oder hinter ein Ende als neues Ende, und haftet an der Wand, Kante oder Achse, auf die er trifft; **Enter** oder **Esc** beendet. Um Punkte herauszunehmen, klicken Sie mit Strg/⌘ auf ihre Griffe, oder klicken Sie auf **Beschriften → Punkte entfernen** (**DIMDEL**, oder **Punkte entfernen** in ihren Eigenschaften) und klicken nahe jedem Punkt; die Segmente beiderseits werden zu einem. Auch der Wert jedes Segments hat einen Griff: Ziehen Sie ihn, um den Wert von seinem Platz wegzurücken, wo Segmente kurz sind und Werte sich überlappen würden; sobald er neben seinem Segment steht, verbindet ihn eine dünne Führungslinie damit. Strg/⌘-Klick auf den Griff, oder **Texte zurück auf die Linie** in den Eigenschaften, setzt ihn zurück. Ein verschobener Wert bleibt bei seinem Segment, wenn Punkte sich bewegen, und dreht sich mit der Kette. Doppelklicken Sie auf ein Segment, um seinen Wert einzugeben: Das gemessene Element bewegt sich, und mit ihm alles, was in der Kette danach kommt. Die Eigenschaften zeigen Wert und Text jedes Segments, teilen eine Kette in Einzelbemaßungen auf und verbinden Einzelbemaßungen, die einander auf einer Linie fortsetzen.

![Die Registerkarte Beschriften](shots/ribbon-annotate.webp)
