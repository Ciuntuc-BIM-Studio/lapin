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

**Versatz** (<kbd>O</kbd>) kopiert eine Linie, eine Polylinie, ein Rechteck, einen Kreis oder einen Bogen in einem Abstand. Geben Sie den Abstand in der Einheit des Projekts ein (in einem Projekt in Millimetern ist `500` ein halber Meter; `2m` oder `1'6"` nennen ihre eigene Einheit), klicken Sie die Form an, dann die Seite, zu der sie geht; es fragt nach der nächsten Form, bis Sie <kbd>Enter</kbd> oder <kbd>Esc</kbd> drücken. Geben Sie stattdessen **T** (Through) ein, geht jede Kopie durch den angeklickten Punkt. Die Ecken einer Polylinie bleiben scharf (auf Gehrung), wie im CAD. Versatz arbeitet in Grundrissen, Schnitten, Ansichten, auf Referenzebenen und in der 3D-Ansicht.

Jede Länge, nach der die Befehlszeile fragt, ist in der Einheit des Projekts, und die Aufforderung nennt sie: `Depth (mm)`, `Height (ft-in)`. Winkel, Faktoren und Anzahlen sind einfache Zahlen: `90` nach **Drehen** sind 90°, gleich welche Einheit.

### Über einem PDF oder einem Bild nachzeichnen

**Datei → PDF oder Bild importieren…** oder **Zeichnen → PDF oder Bild** holt eine Zeichnung zum Nachzeichnen herein: einen gescannten Grundriss, das Foto einer Fassade, ein PDF aus einem anderen Büro. Sie kommt in den Grundriss, den Schnitt oder die Ansicht im Vordergrund, unter deren Zeichnung.

- **Ein PDF**: Wählen Sie die **Seite** und den Maßstab, in dem seine Zeichnung erstellt wurde (**Gezeichnet in 1:**). Ein Papiermillimeter misst dann hier so viele Millimeter, sodass ein Grundriss 1:100 in seiner wahren Größe hereinkommt. Die Linien der Seite kommen als Zeichnung herein, an der Sie fangen, die Sie bearbeiten und nachzeichnen können, ihre Bilder (ein Scan im PDF) als Bilder darunter. Beides lässt sich weglassen. Die Seite kommt als eine Gruppe mit dem Namen der Datei, sodass **Verschieben**, **Drehen** und **Skalieren** sie ganz nehmen; doppelklicken Sie darauf, um an ihren Teilen zu arbeiten.
- **Ein Bild** (PNG oder JPEG): Geben Sie seine **Breite** an; seine Höhe folgt seinen Pixeln. **Zeichnen → Bild skalieren** setzt sie danach genau: Wählen Sie das Bild, klicken Sie auf zwei Punkte darauf in bekanntem Abstand und tippen Sie diesen Abstand ein.

Ein Bild ist durchscheinend (seine **Deckkraft**, standardmäßig 0,5, in seinen Eigenschaften) und gehört nie zum Modell: Auf Plänen wird es nur gedruckt, wenn Sie es in der Ansicht lassen.

![Ein als PDF gedruckter Grundriss, in ein anderes Geschoss geholt, um darüber nachzuzeichnen](shots/trace-pdf.webp)

### Zeichnen in der 3D-Ansicht

**Linie**, **Polylinie**, **Rechteck**, **Kreis**, **Bogen** und die Bearbeitungen aus Zeichnen gehen auch in der 3D-Ansicht, mit der Befehlszeile unten. Solange ein Befehl läuft, sagt das Schild oben, worauf Sie zeichnen: auf der Höhe des aktiven Geschosses oder auf der ausgewählten Referenzebene. **Fläche wählen** im Schild, dann ein Klick auf eine Fläche des Modells, legt auf dieser Fläche eine Referenzebene an und zeichnet darauf. Punkte fangen wie im Grundriss: an den geschnittenen Linien des Grundrisses (Ecken und Flächen der Wände) und an dem, was auf derselben Ebene gezeichnet ist, mit Ortho und den Spurlinien. Was Sie zeichnen, ist dasselbe Zeichenelement wie eines im Grundriss und erscheint auch dort.

![Zeichnen in der 3D-Ansicht: das Schild nennt die Ebene, die Linie folgt der Maus auf der Höhe des Geschosses](shots/draft-3d.webp)

### Zeichnungen in der 3D-Ansicht

Was Sie in einem Grundriss zeichnen (Linien, Polylinien, Rechtecke, Kreise, Bögen), steht auch in der 3D-Ansicht, als Linien auf der Höhe seines Geschosses (die Geschosshöhe selbst, nicht die Schnitthöhe); was Sie auf einer Referenzebene, in einem Schnitt oder einer Ansicht zeichnen, steht auf dieser Ebene. Klicken Sie eines in 3D an, um es auszuwählen, und verschieben, kopieren, versetzen oder löschen Sie es. **Ansicht → Zeichnungen in 3D**, auch in **Ansicht → Ansichtseinstellungen** einer 3D-Ansicht, zeigt oder verbirgt alle; es ist an, solange Sie es nicht ausschalten. Bemaßungen, Texte, Beschriftungen, Schraffuren und Hilfslinien (die Rolle draft) bleiben in ihren Ansichten, und ein DXF in einem Grundriss bleibt eine 2D-Zeichnung, außer seinen Linien mit Höhen.

### Zeichenreihenfolge

Wo gezeichnete Elemente sich überlagern, liegt das später gezeichnete oben. Um das zu ändern, wähle sie aus (Linien, Polylinien, Rechtecke, Kreise, Bögen, Schraffuren und Füllungen, Texte, Bemaßungen, Beschriftungen, die Formen einer DXF), klicke mit rechts und wähle **In den Vordergrund**, **Eine Ebene nach vorne**, **Eine Ebene nach hinten** oder **In den Hintergrund**; **Zeichnen → Reihenfolge** und **Beschriften → Reihenfolge** haben dasselbe, mit <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>]</kbd>, <kbd>Ctrl</kbd>+<kbd>[</kbd> und <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>[</kbd> (⌘ auf dem Mac). Nach vorne und nach hinten gehen am nächsten Element vorbei, das die Auswahl überlagert. Das Gezeichnete liegt über der Zeichnung des Modells; **Hinter das Modell** legt es unter den Schnitt der Wände und die gesehenen Linien (eine Schraffur unter einer Wand), und ein Klick dort nimmt dann die Wand; **In den Vordergrund** oder **In den Hintergrund** holen es wieder über das Modell. In der Befehlszeile fragt **DRAWORDER** (**DRO**) nach den Objekten, dann **Above objects** oder **Under objects** (klicke die Elemente, über oder unter die sie kommen, dann <kbd>Enter</kbd>), **Front**, **Back** oder hinter das Modell, **Model**. Die Reihenfolge gehört der Ansicht, ist ein Rückgängig-Schritt, und Pläne, PDF und DXF drucken sie, wie du sie siehst; was du danach zeichnest, kommt nach oben.

![Rechtsklick auf eine ausgewählte Füllung: die Zeichenreihenfolge](shots/draw-order-menu.webp)

![In den Vordergrund geholt, deckt die graue Füllung den Text](shots/draw-order-front.webp)

## Beschriften

Die Registerkarte **Beschriften** enthält die Bemaßungen, Texte, Rasterachsen, Ebenen und Beschriftungen. Eine Bemaßung liest das Modell, sie aktualisiert sich also, wenn eine Wand verschoben wird. Wie Texte, Bemaßungen, Achsen und Beschriftungen aussehen, legen Sie unter **Verwalten → Zeichnungsstandards** fest.

Eine ausgerichtete oder lineare Bemaßung entsteht als Kette: Nach den ersten beiden Punkten und ihrer Linie fügt jeder Klick den nächsten Punkt auf derselben Linie hinzu, und **Enter** (oder **Esc**) macht die ganze Kette zu einer Bemaßung (**U** nimmt den letzten Punkt zurück; geben Sie zuerst **S** ein für eine Einzelbemaßung). Eine Kette verhält sich wie eine Polylinie: Ein Klick wählt sie ganz aus, und sie wird als Ganzes verschoben, kopiert und gelöscht. Eine Kette, die Wände, Kanten oder Rasterachsen misst, bleibt an ihnen: Allein gezogen oder verschoben, gleitet nur ihre Maßlinie, rechtwinklig zur Kette, sodass ein Ausrutscher der Maus sie nie ins Leere messen lässt; zusammen mit dem Gemessenen verschoben, geht sie mit. Ihre Griffe sitzen auf jedem gemessenen Punkt (ziehen, um ihn zu verschieben, Strg/⌘-Klick, um ihn herauszunehmen), in der Mitte jedes Segments (ziehen, um einen Punkt hinzuzufügen, Strg/⌘-Klick, um einen auf halber Strecke hinzuzufügen) und an den Enden der Maßlinie (ziehen, um die Linie zu verschieben). Um weitere Punkte in eine Kette zu setzen, wie in ArchiCAD, wählen Sie sie aus und klicken auf **Beschriften → Punkte hinzufügen** (oder geben **DIMADD** ein, oder **Punkte hinzufügen** in ihren Eigenschaften): Jeder Punkt, den Sie anklicken, kommt dort in die Kette, wo er entlang ihrer Linie liegt, zwischen die Punkte um ihn herum oder hinter ein Ende als neues Ende, und haftet an der Wand, Kante oder Achse, auf die er trifft; **Enter** oder **Esc** beendet. Um Punkte herauszunehmen, klicken Sie mit Strg/⌘ auf ihre Griffe, oder klicken Sie auf **Beschriften → Punkte entfernen** (**DIMDEL**, oder **Punkte entfernen** in ihren Eigenschaften) und klicken nahe jedem Punkt; die Segmente beiderseits werden zu einem. Auch der Wert jedes Segments hat einen Griff: Ziehen Sie ihn, um den Wert von seinem Platz wegzurücken, wo Segmente kurz sind und Werte sich überlappen würden; sobald er neben seinem Segment steht, verbindet ihn eine dünne Führungslinie damit. Strg/⌘-Klick auf den Griff, oder **Texte zurück auf die Linie** in den Eigenschaften, setzt ihn zurück. Ein verschobener Wert bleibt bei seinem Segment, wenn Punkte sich bewegen, und dreht sich mit der Kette. Doppelklicken Sie auf ein Segment, um seinen Wert einzugeben: Das gemessene Element bewegt sich, und mit ihm alles, was in der Kette danach kommt. Die Eigenschaften zeigen Wert und Text jedes Segments, teilen eine Kette in Einzelbemaßungen auf und verbinden Einzelbemaßungen, die einander auf einer Linie fortsetzen.

Beschriftungen eines Grundrisses (Bemaßungen, Texte, gezeichnete Linien, Tags) können an derselben Stelle in einen anderen Grundriss gehen, da Grundrisse dieselben Koordinaten haben: wähle sie aus und wähle den Grundriss in ihren Eigenschaften, unter **View** zum Verschieben oder **Copy to** zum Kopieren. So kommen auch Bemaßungen aus einem Lageplan in den Grundriss der Ebene. Die Punkte einer Bemaßung halten sich dann an das, was der neue Grundriss dort schneidet: Wände, Kanten, Achsen.

**Öffnungshöhen.** Ein Segment einer Maßkette, das von einer Laibung einer Tür, eines Fensters oder einer Öffnung zur anderen geht, schreibt die Breite über die Linie und die Höhe der Öffnung darunter, wie es rumänische und europäische Zeichnungen tun. Sie wird jedes Mal aus dem Modell gelesen und folgt daher der Öffnung, wenn sie ihre Größe ändert, verschoben wird oder einen anderen Typ bekommt; ein Segment, über das du einen Text geschrieben hast, schreibt keine. **Verwalten → Zeichnungsstandards → Abmessungen → Öffnungshöhe** legt sie für jeden Bemaßungsstil fest: aus, `150`, `h=150`, `h=150` mit der Brüstung `p=90` darunter oder `150/90`, in der Einheit des Stils. Das gilt in Grundrissen, Schnitten und Ansichten, und Pläne, PDF und DXF drucken sie. Neue Projekte haben sie eingeschaltet (nur die Höhe); ein früher erstelltes Projekt behält seine Bemaßungen, bis du sie dort einschaltest.

![Die Höhen der Tür und des Fensters unter ihren Segmenten](shots/dimension-opening-height.webp)

![Die Öffnungshöhe in den Bemaßungsstilen](shots/dimension-opening-height-style.webp)

### Beschriftungen

**Beschriften → Beschriftung** (<kbd>TG</kbd>) setzt in eine Ansicht einen Kasten mit dem, was ein Element über sich sagt, und eine Hinweislinie zu ihm. Solange das Werkzeug aktiv ist, wählt die Leiste unter der Ansicht, was die Beschriftung zeigt: die Marke, den Namen, die Klasse oder den Typ, **Feuerwiderstand**, **Schallschutz**, **Wärmedurchgangskoeffizient** oder **Anderer Parameter…** nach seinem Namen; oder einen Beschriftungstyp der Zeichnungsstandards; und **HINWEISLINIE** schaltet die Hinweislinie ein oder aus. Klicken Sie auf das Element, dann dorthin, wo die Beschriftung hin soll (tippen Sie vorher **E**, um die Linie in einem Knick abzuwinkeln). **Beschriften → Parameter beschriften** fragt nach dem Namen des Parameters und startet das Werkzeug damit.

![Die Leiste des Werkzeugs Beschriftung unter der Ansicht](shots/tag-options.webp)

Eine Beschriftung liest jeden Parameter: den des Elements, den seines Typs (dann zeigen ihn alle Elemente des Typs), eine Standardeigenschaft wie `Pset_WallCommon.FireRating` oder eine, mit der ein importiertes Element kam. Ein Name wird auch ohne Leerzeichen gefunden, **Fire rating** und `FireRating` sind also dasselbe. Zum Ausfüllen dient **Parameter** in den Eigenschaften des Elements oder seines Typs: Geben Sie einen Namen ein (angeboten werden Fire rating, Sound rating, Moisture resistance und Thermal resistance) oder wählen Sie **Standardeigenschaft hinzufügen…** für die IFC-Eigenschaften seiner Klasse. Die Beschriftung folgt, sobald es einen Wert gibt, und Längen werden in den Einheiten des Projekts geschrieben.

Eine ausgewählte Beschriftung hat Griffe. Ziehen Sie den Kasten, um die Beschriftung zu verschieben: Die Hinweislinie zeigt weiter dorthin, wohin sie zeigte. Eine Beschriftung geht mit ihrem Element: Wird das Element verschoben, gedreht, gespiegelt oder über seine Griffe umgeformt, folgt ihm die Beschriftung, behält ihren Platz daneben, und die Hinweislinie bleibt auf ihm; eine Beschriftung, die Sie verschoben haben, behält den Abstand, den Sie ihr gegeben haben. Ziehen Sie die Mitte der Linie, um sie in einem Knick abzuwinkeln (Strg/⌘-Klick auf den Knick macht sie wieder gerade), und ihr Ende, um sie auf einen anderen Punkt zeigen zu lassen (Strg/⌘-Klick: zurück zur Mitte des Elements). In den Eigenschaften ändern Sie, was die Beschriftung **Zeigt**, geben ihr einen eigenen **Text** (`{mark}|{FireRating}` schreibt zwei Zeilen), einen **Beschriftungstyp**, oder schalten die **Hinweislinie** aus. Die Engine zeichnet die Beschriftungen, Pläne und PDFs drucken sie also so, wie Sie sie sehen. Die Beschriftungstypen unter **Verwalten → Zeichnungsstandards → Beschriftungen** lesen dieselben Felder in ihren Vorlagen; ein neues Projekt hat darunter Feuerwiderstand, Schallschutz und Wärmedurchgangskoeffizient.

**Führungslinien wie ArchiCADs Beschriftungen.** Standardmäßig ist die Führungslinie einer Beschriftung *geknickt*: eine schräge Linie vom Element zu einem Knick, dann eine Schulter, die unter dem Text läuft und seine erste Zeile unterstreicht, ohne Rahmen um den Text. Nach dem Klick auf das Element ist der nächste Klick der Knick, und der Text steht daneben. Ziehen Sie den Griff des Knicks: die schräge Linie hält 30°, 45°, 60° oder 90° zu ihrem Ende (<kbd>Umschalt</kbd> gedrückt: beliebiger Winkel), und über das Ende hinaus gezogen wechselt der Text auf diese Seite, zum Knick hin ausgerichtet. Ziehen Sie den Text, um ihn mit seinem Knick zu verschieben. In **Verwalten → Zeichnungsstandards → Beschriftungen** legt **Führungslinie…** an einem Beschriftungstyp (und der Schichtaufbau-Editor) die **Form der Führungslinie** fest (geknickt oder gerade aus einem Rahmen), was die Schulter unterstreicht (**Unterstreichung**: die erste Zeile, jede Zeile, die letzte Zeile oder nichts, dann endet sie an der Textseite) und **Verlängerung mm**, wie viel weiter die Schulter vom Text reicht, neben der Vorschau der Engine. Mit einer Führungslinie pro Schicht endet jede unter ihrer eigenen Zeile, die Knicke in einer Spalte; den Knick ziehen verschiebt die Spalte. **Form der Führungslinie** in den Eigenschaften einer Beschriftung geht der des Typs vor; eine Beschriftung, deren Führungslinie an einem freien Knick gebogen wurde, behält ihre gerade Linie. Beschriftungen in früher erstellten Projekten behalten ihre gerade Hinweislinie im Rahmen.

![Die Hinweislinie einer Beschriftung mit Knick und ihren Griffen](shots/tag-leader.webp)

![Die Eigenschaften einer Beschriftung](shots/tag-properties.webp)

Die Markierung einer Tür oder eines Fensters wird Teil für Teil bearbeitet, jeder Text verschoben oder gedreht und jedes Linienende verlängert: siehe [Türen und Fenster](doors-windows.md).

#### Schichtaufbau-Beschriftung

Eine Schichtaufbau-Beschriftung schreibt die Schichten eines Elements als Architekturhinweis: eine Kopfzeile (*EXTERIOR*), eine Zeile je Schicht, eine Fußzeile (*INTERIOR*). Ein neues Projekt hat **Build-up (exterior → interior)**: jede Zeile ist die Beschreibung des Materials (sein Name, wenn es keine hat), sein λ, wenn es eines hat, und die Dicke der Schicht in mm. Wähle sie in der Leiste des Werkzeugs **Beschriftung** und klicke auf eine Wand, eine Decke, ein Dach oder die Bekleidung eines Raums; andere Elemente erhalten das gewählte Feld. Der Text wird bei jedem Zeichnen aus dem Modell gelesen: ändere eine Schicht, die Beschreibung eines Materials oder sein λ, und die Beschriftung folgt.

![Zwei Schichtaufbau-Beschriftungen an einer Fassadenwand: eine Hinweislinie und eine Hinweislinie je Schicht](shots/buildup-tag.webp)

Ihre Form wird in **Verwalten → Zeichnungsstandards → Beschriftungen** festgelegt: setze die **Form** eines Beschriftungstyps auf *Schichtaufbau* oder klicke bei einem auf **Schichtaufbau…**, um ihn neben einer Vorschau an einem Wand- oder Deckentyp des Projekts zu bearbeiten.

- **Kopfzeile** und **Fußzeile**: über und unter den Schichten geschrieben (leer: keine; `|` beginnt eine neue Zeile).
- **Jede Schicht**: die Zeile, die einmal je Schicht geschrieben wird. Sie liest die Felder der Schicht, `{description}`, `{material}`, `{thickness}` (in der unter **Dicke in** gewählten Einheit), `{note}` (die Notiz der Schicht in ihrem Typ), `{function}` (Kern, Bekleidung, Dämmung…) und `{index}`; jeden Parameter des Materials nach Namen oder Symbol, `{λ}`, `{μ}`, `{ρ}`, `{Reaction to fire}` (siehe die [Daten](materials.md#daten-beschreibung-parameter-wie-es-gemessen-wird) des Materials); und die des Elements, `{name}`, `{type}`, `{mark}`. **Feld einfügen…** setzt eines an die Cursorposition und listet die Parameter der Materialien des Projekts.
- **Eckige Klammern** machen einen Teil optional: `[, λ = {λ} W/mK]` wird nur geschrieben, wo das Material ein λ hat; ein Teil entfällt ganz, wenn ein Feld darin leer ist. **[ ]** macht die Auswahl zu so einem Teil. Um eine Klammer selbst zu schreiben, tippe `\[` oder `\]`.
- **Reihenfolge**: von außen nach innen bei einer Wand (die Seite ihrer ersten Schicht ist ihre Außenseite, eine gespiegelte Wand beginnt also weiterhin dort), von oben nach unten bei einer Decke oder einem Dach; oder umgekehrt. Wähle die Reihenfolge, in der die Schichten neben dem Hinweis liegen, damit sich Hinweislinien je Schicht nie kreuzen.
- **Schichten ohne Dicke** (Bahnen, Folien, Anstriche) werden geschrieben, außer du entfernst den Haken bei **sichtbar**. Eine Schicht, die über ihre Höhe das Material wechselt, schreibt eine Zeile je Material.
- **Hinweislinien**: eine, zum Element, oder **eine je Schicht**, jede von ihrer Zeile zu einem Punkt in ihrer eigenen Schicht, wo die Ansicht die Schichten unterscheidet (eine Wand in Grundrissen und Schnitten, eine Decke in Schnitten; sonst behält die Beschriftung eine). Ziehe das Ende der Hinweislinie am Element entlang, um es an anderer Stelle zu queren.
- **Zeilen beginnen mit** einem Punkt, einem Strich, ihrer Nummer oder nichts; sie brechen bei **Breite mm** auf dem Papier um, hängend nach dem Aufzählungszeichen, und ein Wert bleibt bei seinem Symbol und seiner Einheit (`λ = 0.035 W/mK`).

![Der Editor eines Schichtaufbau-Beschriftungstyps neben seiner Vorschau](shots/buildup-tag-type.webp)

Wie jede Beschriftung folgt die Schichtaufbau-Beschriftung ihrem Element, und Pläne und PDFs drucken sie so, wie du sie siehst, λ, μ und ρ eingeschlossen. Ein eigener **Text** in ihren Eigenschaften ersetzt die Zeile je Schicht nur für diese Beschriftung.

### Rasterachsen

**Beschriften → Rasterachse** (<kbd>GR</kbd>) zeichnet eine Achse über ihre beiden Enden; sie erhält die nächste Bezeichnung ihrer Richtung. **Beschriften → Achsraster…** legt ein ganzes Raster auf einmal an: die Abstände **Quer** (die nummerierten Achsen, von links nach rechts) und **Längs** (die Achsen mit Buchstaben, von unten nach oben) in den Einheiten des Projekts, etwa `6000 6000 4500` oder `3*6000`; den **Ursprung**, wo sich die ersten Achsen kreuzen, einen **Winkel**, wie weit die Achsen **Über das Raster hinaus** laufen, die **Ersten Bezeichnungen** und den **Achstyp**. Mit **die Achsen des Rasters ersetzen** entfallen die bisher gezeichneten Achsen.

![Der Dialog Achsraster](shots/grid-dialog.webp)

Klicken Sie auf eine Achse, um sie auszuwählen. Ihre Eigenschaften zeigen **Anfang** und **Ende**, **Länge**, **Winkel** und den Abstand von der parallelen Achse davor (↔). Geben Sie einen neuen Abstand ein, und die Achse rückt; die Achsen dahinter rücken mit, außer Sie deaktivieren **die Achsen dahinter bewegen sich mit**. **Achsköpfe** wählt, welche Enden einen Achskopf tragen. **Achsstile…** öffnet **Zeichnungsstandards → Rasterachsen**, wo Achskopf, Größe, Linientyp und erste Bezeichnung eingestellt werden. Wählen Sie das ganze Raster, um seine Achsen und ihre Abstände zu sehen.

![Die Eigenschaften einer Rasterachse](shots/axis-properties.webp)

Alle Längen hier folgen den Einheiten des Projekts. In Fuß und Zoll lauten Enden und Abstände 19'-8 1/4", und auch die Ebenen werden in Fuß und Zoll geschrieben.

![Dieselbe Achse in Fuß und Zoll](shots/axis-properties-imperial.webp)

![Die Registerkarte Beschriften](shots/ribbon-annotate.webp)
