---
title: Zeichnen und die Befehlszeile
order: 80
summary: Geben Sie Befehle wie in CAD ein, fangen Sie Punkte, verschieben und kopieren Sie, zeichnen Sie Linien und beschriften Sie mit Bemaßungen, Texten und Beschriftungen.
---

## Die Befehlszeile

Geben Sie die Buchstaben eines Werkzeugs an beliebiger Stelle im Fenster ein und drücken Sie dann <kbd>Enter</kbd>. Geben Sie zum Beispiel <kbd>WA</kbd> für eine Wand ein, <kbd>CO</kbd> zum Kopieren, <kbd>M</kbd> zum Verschieben oder <kbd>L</kbd> für eine Linie. Die Befehlszeile unten zeigt, was das Werkzeug als Nächstes braucht. Sie können einen Punkt anklicken oder eine Länge, einen Winkel oder Koordinaten eingeben.

Eingegebene Koordinaten sind in der Einheit des Projekts, wie die Längen neben dem Cursor: in einem Projekt in Millimetern macht `@4000,4000` nach der ersten Ecke ein Rechteck von 4 m mal 4 m, und `4000<90` sind 4 m gerade nach oben.

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

## Auswählen

Klicken Sie auf ein Element, um es auszuwählen; <kbd>Umschalt</kbd>-Klick fügt zur Auswahl hinzu. Um mehrere auf einmal auszuwählen, ziehen Sie mit der linken Maustaste, wo nichts ausgewählt ist. Nach rechts gezogen ist der Rahmen blau und nimmt, was ganz in ihm liegt (Fenster); nach links gezogen ist er grün und gestrichelt und nimmt alles, was er berührt (Kreuzen), wie in AutoCAD. Drücken Sie <kbd>Leertaste</kbd> während des Ziehens, und der Rahmen wird zu einem Lasso, das der Maus folgt und den Modus des Rahmens behält; <kbd>Leertaste</kbd> erneut kehrt zum Rahmen zurück. In der Befehlszeile, wenn ein Befehl nach Objekten fragt, tippen Sie **WP** oder **CP** und klicken die Ecken eines Polygons, dann <kbd>Eingabe</kbd>: ein Fenster- oder ein Kreuzen-Polygon.

![Ein Lasso im Grundriss: der Rahmen nach rechts gezogen, dann Leertaste, und die Schleife um die zwei Wände, die es nimmt](shots/select-lasso.webp)

## Kopieren und Verschieben

Zum Kopieren wählen Sie das Element aus und geben <kbd>CO</kbd> ein. Klicken Sie einen Basispunkt an, dann einen zweiten Punkt. Lapin kopiert weiter, bis Sie <kbd>Enter</kbd> oder <kbd>Esc</kbd> drücken. **Verschieben** (<kbd>M</kbd>) funktioniert genauso.

Während Sie verschieben oder kopieren, bleibt eine Markierung auf dem Basispunkt, und eine gestrichelte Linie führt von dort zur Maus; ein blasser Umriss dessen, was Sie verschieben, bleibt an der alten Stelle, bis Sie klicken oder <kbd>Esc</kbd> drücken. Linie und Markierung wechseln ihre Farbe mit dem Fang: grün auf einem gefangenen Punkt, blau, wenn Ortho oder der polare Spurfang die Richtung hält, in der Akzentfarbe auf einer Spurlinie, grau, wenn frei. Neben der Maus zeigt Lapin, wie weit Sie verschoben haben: Δx, Δy, den Abstand und den Winkel, in den Einheiten des Projekts. Sie können Abstand und Winkel dort eingeben, wie beim Zeichnen. In der Oberfläche Kompakt stehen diese Werte in der Statuszeile. Das Ziehen eines Griffs zeigt dasselbe, ebenso das Ziehen eines ausgewählten Elements selbst, ab dem Punkt, an dem Sie gedrückt haben; drücken Sie <kbd>Esc</kbd>, bevor Sie loslassen, und das Element bleibt, wo es war.

![Eine Wand verschieben: die Markierung auf dem Basispunkt, die gestrichelte Linie zur Maus, das Geisterbild der Wand an der alten Stelle und der Versatz an der Maus](shots/move-trace.webp)

Sie können stattdessen auch ziehen. Ziehen Sie das Kreuz in der Mitte eines ausgewählten Elements, um es zu verschieben. Halten Sie beim Ziehen <kbd>Strg</kbd> (<kbd>⌘</kbd> auf einem Mac) gedrückt, um eine Kopie zurückzulassen.

Eine Tür, ein Fenster oder eine Wandöffnung wird entlang ihrer Wand verschoben und kopiert: Es zählt nur der Anteil der Verschiebung entlang der Wand, sodass `500` nach dem Basispunkt sie 500 entlang der Wand verschiebt; in eine andere Wand verschoben, wechselt sie in diese Wand. Ist auch ihre Wand ausgewählt, geht sie mit der Wand.

Eine Rasterachse lässt sich allein oder zusammen mit anderen Elementen verschieben, kopieren, drehen und spiegeln. Eine Kopie ist eine neue Achse desselben Rasters, benannt nach der letzten Achse in ihrer Richtung: Bei den Achsen 1, 2 und 3 ist eine Kopie einer beliebigen von ihnen die 4.

## Zeichnen

Die Registerkarte **Zeichnen** zeichnet Linien, Polylinien, Rechtecke, Kreise und Bögen. Sie bearbeitet sie mit Versatz, Stutzen, Dehnen, Abrunden, Fase und Verbinden, füllt Flächen mit Schraffuren und ordnet sie auf **Layer** an. **DXF importieren** holt eine CAD-Zeichnung als Linien herein, die Sie fangen und bearbeiten können.

![Die Registerkarte Zeichnen](shots/ribbon-draft.webp)

## Beschriften

Die Registerkarte **Beschriften** enthält die Bemaßungen, Texte, Rasterachsen, Ebenen und Beschriftungen. Eine Bemaßung liest das Modell, sie aktualisiert sich also, wenn eine Wand verschoben wird. Wie Texte, Bemaßungen, Achsen und Beschriftungen aussehen, legen Sie unter **Verwalten → Zeichnungsstandards** fest.

Eine ausgerichtete oder lineare Bemaßung entsteht als Kette: Nach den ersten beiden Punkten und ihrer Linie fügt jeder Klick den nächsten Punkt auf derselben Linie hinzu, und **Enter** (oder **Esc**) macht die ganze Kette zu einer Bemaßung (**U** nimmt den letzten Punkt zurück; geben Sie zuerst **S** ein für eine Einzelbemaßung). Eine Kette verhält sich wie eine Polylinie: Ein Klick wählt sie ganz aus, und sie wird als Ganzes verschoben, kopiert und gelöscht. Eine Kette, die Wände, Kanten oder Rasterachsen misst, bleibt an ihnen: Allein gezogen oder verschoben, gleitet nur ihre Maßlinie, rechtwinklig zur Kette, sodass ein Ausrutscher der Maus sie nie ins Leere messen lässt; zusammen mit dem Gemessenen verschoben, geht sie mit. Ihre Griffe sitzen auf jedem gemessenen Punkt (ziehen, um ihn zu verschieben, Strg/⌘-Klick, um ihn herauszunehmen), in der Mitte jedes Segments (ziehen, um einen Punkt hinzuzufügen, Strg/⌘-Klick, um einen auf halber Strecke hinzuzufügen) und an den Enden der Maßlinie (ziehen, um die Linie zu verschieben). Um weitere Punkte in eine Kette zu setzen, wie in ArchiCAD, wählen Sie sie aus und klicken auf **Beschriften → Punkte hinzufügen** (oder geben **DIMADD** ein, oder **Punkte hinzufügen** in ihren Eigenschaften): Jeder Punkt, den Sie anklicken, kommt dort in die Kette, wo er entlang ihrer Linie liegt, zwischen die Punkte um ihn herum oder hinter ein Ende als neues Ende, und haftet an der Wand, Kante oder Achse, auf die er trifft; **Enter** oder **Esc** beendet. Um Punkte herauszunehmen, klicken Sie mit Strg/⌘ auf ihre Griffe, oder klicken Sie auf **Beschriften → Punkte entfernen** (**DIMDEL**, oder **Punkte entfernen** in ihren Eigenschaften) und klicken nahe jedem Punkt; die Segmente beiderseits werden zu einem. Auch der Wert jedes Segments hat einen Griff: Ziehen Sie ihn, um den Wert von seinem Platz wegzurücken, wo Segmente kurz sind und Werte sich überlappen würden; sobald er neben seinem Segment steht, verbindet ihn eine dünne Führungslinie damit. Strg/⌘-Klick auf den Griff, oder **Texte zurück auf die Linie** in den Eigenschaften, setzt ihn zurück. Ein verschobener Wert bleibt bei seinem Segment, wenn Punkte sich bewegen, und dreht sich mit der Kette. Doppelklicken Sie auf ein Segment, um seinen Wert einzugeben: Das gemessene Element bewegt sich, und mit ihm alles, was in der Kette danach kommt. Die Eigenschaften zeigen Wert und Text jedes Segments, teilen eine Kette in Einzelbemaßungen auf und verbinden Einzelbemaßungen, die einander auf einer Linie fortsetzen.

Beschriftungen eines Grundrisses (Bemaßungen, Texte, gezeichnete Linien, Tags) können an derselben Stelle in einen anderen Grundriss gehen, da Grundrisse dieselben Koordinaten haben: wähle sie aus und wähle den Grundriss in ihren Eigenschaften, unter **View** zum Verschieben oder **Copy to** zum Kopieren. So kommen auch Bemaßungen aus einem Lageplan in den Grundriss der Ebene. Die Punkte einer Bemaßung halten sich dann an das, was der neue Grundriss dort schneidet: Wände, Kanten, Achsen.

### Beschriftungen

**Beschriften → Beschriftung** (<kbd>TG</kbd>) setzt in eine Ansicht einen Kasten mit dem, was ein Element über sich sagt, und eine Hinweislinie zu ihm. Solange das Werkzeug aktiv ist, wählt die Leiste unter der Ansicht, was die Beschriftung zeigt: die Marke, den Namen, die Klasse oder den Typ, **Feuerwiderstand**, **Schallschutz**, **Wärmedurchgangskoeffizient** oder **Anderer Parameter…** nach seinem Namen; oder einen Beschriftungstyp der Zeichnungsstandards; und **HINWEISLINIE** schaltet die Hinweislinie ein oder aus. Klicken Sie auf das Element, dann dorthin, wo die Beschriftung hin soll (tippen Sie vorher **E**, um die Linie in einem Knick abzuwinkeln). **Beschriften → Parameter beschriften** fragt nach dem Namen des Parameters und startet das Werkzeug damit.

![Die Leiste des Werkzeugs Beschriftung unter der Ansicht](shots/tag-options.webp)

Eine Beschriftung liest jeden Parameter: den des Elements, den seines Typs (dann zeigen ihn alle Elemente des Typs), eine Standardeigenschaft wie `Pset_WallCommon.FireRating` oder eine, mit der ein importiertes Element kam. Ein Name wird auch ohne Leerzeichen gefunden, **Fire rating** und `FireRating` sind also dasselbe. Zum Ausfüllen dient **Parameter** in den Eigenschaften des Elements oder seines Typs: Geben Sie einen Namen ein (angeboten werden Fire rating, Sound rating, Moisture resistance und Thermal resistance) oder wählen Sie **Standardeigenschaft hinzufügen…** für die IFC-Eigenschaften seiner Klasse. Die Beschriftung folgt, sobald es einen Wert gibt, und Längen werden in den Einheiten des Projekts geschrieben.

Eine ausgewählte Beschriftung hat Griffe. Ziehen Sie den Kasten, um die Beschriftung zu verschieben: Die Hinweislinie zeigt weiter dorthin, wohin sie zeigte. Ziehen Sie die Mitte der Linie, um sie in einem Knick abzuwinkeln (Strg/⌘-Klick auf den Knick macht sie wieder gerade), und ihr Ende, um sie auf einen anderen Punkt zeigen zu lassen (Strg/⌘-Klick: zurück zur Mitte des Elements). In den Eigenschaften ändern Sie, was die Beschriftung **Zeigt**, geben ihr einen eigenen **Text** (`{mark}|{FireRating}` schreibt zwei Zeilen), einen **Beschriftungstyp**, oder schalten die **Hinweislinie** aus. Die Engine zeichnet die Beschriftungen, Pläne und PDFs drucken sie also so, wie Sie sie sehen. Die Beschriftungstypen unter **Verwalten → Zeichnungsstandards → Beschriftungen** lesen dieselben Felder in ihren Vorlagen; ein neues Projekt hat darunter Feuerwiderstand, Schallschutz und Wärmedurchgangskoeffizient.

![Die Hinweislinie einer Beschriftung mit Knick und ihren Griffen](shots/tag-leader.webp)

![Die Eigenschaften einer Beschriftung](shots/tag-properties.webp)

Die Markierung einer Tür oder eines Fensters wird Teil für Teil bearbeitet, jeder Text verschoben oder gedreht und jedes Linienende verlängert: siehe [Türen und Fenster](doors-windows.md).

### Rasterachsen

**Beschriften → Rasterachse** (<kbd>GR</kbd>) zeichnet eine Achse über ihre beiden Enden; sie erhält die nächste Bezeichnung ihrer Richtung. **Beschriften → Achsraster…** legt ein ganzes Raster auf einmal an: die Abstände **Quer** (die nummerierten Achsen, von links nach rechts) und **Längs** (die Achsen mit Buchstaben, von unten nach oben) in den Einheiten des Projekts, etwa `6000 6000 4500` oder `3*6000`; den **Ursprung**, wo sich die ersten Achsen kreuzen, einen **Winkel**, wie weit die Achsen **Über das Raster hinaus** laufen, die **Ersten Bezeichnungen** und den **Achstyp**. Mit **die Achsen des Rasters ersetzen** entfallen die bisher gezeichneten Achsen.

![Der Dialog Achsraster](shots/grid-dialog.webp)

Klicken Sie auf eine Achse, um sie auszuwählen. Ihre Eigenschaften zeigen **Anfang** und **Ende**, **Länge**, **Winkel** und den Abstand von der parallelen Achse davor (↔). Geben Sie einen neuen Abstand ein, und die Achse rückt; die Achsen dahinter rücken mit, außer Sie deaktivieren **die Achsen dahinter bewegen sich mit**. **Achsköpfe** wählt, welche Enden einen Achskopf tragen. **Achsstile…** öffnet **Zeichnungsstandards → Rasterachsen**, wo Achskopf, Größe, Linientyp und erste Bezeichnung eingestellt werden. Wählen Sie das ganze Raster, um seine Achsen und ihre Abstände zu sehen.

![Die Eigenschaften einer Rasterachse](shots/axis-properties.webp)

Alle Längen hier folgen den Einheiten des Projekts. In Fuß und Zoll lauten Enden und Abstände 19'-8 1/4", und auch die Ebenen werden in Fuß und Zoll geschrieben.

![Dieselbe Achse in Fuß und Zoll](shots/axis-properties-imperial.webp)

![Die Registerkarte Beschriften](shots/ribbon-annotate.webp)
