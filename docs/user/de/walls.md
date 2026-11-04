---
title: Wände
order: 20
summary: Zeichnen Sie Wände Ecke für Ecke, ändern Sie sie im Eigenschaftenbereich und legen Sie Wandtypen mit ihren Schichten an.
---

<!-- video walls -->

## Wände zeichnen

Öffnen Sie die Registerkarte **Architektur** und wählen Sie **Wand**, oder geben Sie <kbd>WA</kbd> ein.

Solange das Werkzeug aktiv ist, zeigt der Eigenschaftenbereich **Neues Bauteil**: den Typ, mit dem die Wände gezeichnet werden. Wählen Sie dort vor dem ersten Klick einen anderen und sehen Sie seine Schichten, seine Dicke und wie viele Wände ihn schon verwenden; **Typen bearbeiten…** öffnet Wall types. Tür, Fenster, Decke, abgehängte Decke und Vorhangfassade zeigen ihre Typen ebenso.

![Die Registerkarte Architektur](shots/ribbon-architecture.webp)

Klicken Sie dort, wo die Wand beginnt, und dann an jeder Ecke. Die nächste Wand folgt der Maus. Während Sie zeichnen, können Sie statt zu klicken eine Länge und einen Winkel in die Felder über der Ansicht eingeben. Drücken Sie <kbd>Enter</kbd> oder <kbd>Esc</kbd>, um aufzuhören.

![Wände zeichnen: die nächste Wand folgt der Maus, Länge und Winkel werden angezeigt](shots/wall-drawing.webp)

Wo zwei Wände aufeinandertreffen, verbindet Lapin sie selbst. Ihre Schichten laufen so ineinander, wie sie gebaut würden.

- **Gebogene Wand** (<kbd>WC</kbd>) zeichnet eine Wand entlang eines Bogens.
- **Vorhangfassade** (<kbd>CW</kbd>) zeichnet eine verglaste Wand mit Pfosten, Riegeln und Paneelen.
- Wände verbinden sich von selbst, wo sie aufeinandertreffen: eine Ecke als Gehrung, eine Wand, die auf einer anderen endet, als T, eine, die eine andere kreuzt, als X. An einem T oder X laufen die Schichten ineinander, wie gebaut: Der Kern der endenden Wand läuft durch die Bekleidungen der anderen bis zu deren Kern, und seine Bekleidungen enden an der Fläche der anderen, wo sie auf ihresgleichen treffen, sodass der Grundriss einen Aufbau ohne Linie hindurch zeigt. **Verbinden** (<kbd>JW</kbd>), **Gehrung** und **Stumpf** ändern, wie zwei Wände aufeinandertreffen, wenn das nicht gewollt ist; **Neue Ecken auf Gehrung** macht Stumpf zur Vorgabe auf diesem Rechner. In beiden Fällen läuft jede Schicht für sich um die Ecke: die Bekleidungen treffen auf ihresgleichen und laufen ungebrochen weiter, die Kerne treffen sich auf der Diagonale (Gehrung) oder ein Kern läuft bis zur Außenseite des anderen (Stumpf), so zeigt der Grundriss mehrschichtiger Wände saubere Ecken. Jede Schicht läuft in die andere Wand durch deren Schichten mit niedrigerer **Priorität** und endet an der ersten mit gleicher oder höherer (siehe **Wandtypen** unten); so treffen standardmäßig Kerne auf Kerne und Bekleidungen enden an den Oberflächen. An einer Ecke läuft ein Kern mit höherer Priorität durch den anderen, ob Gehrung oder Stumpfstoß. Die **Schnittpriorität** einer Wand führt sie nie in eine Wand, mit der sie verbunden ist: Zwischen verbundenen Wänden entscheidet sie nur, welcher Kern durchläuft, wenn beide Kerne dieselbe Priorität haben (an einer stumpfen Ecke oder einer Kreuzung).
- **Tauschen** ändert, welche der beiden Wände durchläuft.

**Die Linie, auf der Sie zeichnen.** Solange das Werkzeug Wall aktiv ist, wählt die Liste in der Leiste der Befehlszeile die Linie, auf die Sie klicken: **Kernmitte** (die Achse, wie bisher), **Kern, Außenseite**, **Kern, Innenseite**, **Außenseite** oder **Innenseite**. Außen ist rechts der Wand in Zeichenrichtung (zeichnen Sie gegen den Uhrzeigersinn um das Gebäude, damit es außen liegt). Die Achse der Wand bleibt auf ihrem Kern, von der Linie versetzt, und nacheinander auf derselben Seite gezeichnete Wände treffen sich an der Ecke dieser Seite.

## Eine Wand ändern

Klicken Sie auf eine Wand, um sie auszuwählen. Ihre Griffe erscheinen. Ziehen Sie die Enden, um sie zu verlängern oder zu verkürzen, oder ziehen Sie das Kreuz in der Mitte, um sie zu verschieben.

![Eine ausgewählte Wand mit ihren Griffen](shots/wall-selected.webp)

Der Eigenschaftenbereich zeigt die ausgewählte Wand. Oben im Bereich stehen ihr Typ und ihre Schichten. Darunter folgen Länge, Höhe, Versatz unten, Geschoss und Bauphase. Ändern Sie einen beliebigen Wert, und das Modell folgt.

![Die Eigenschaften einer Wand](shots/wall-properties.webp)

Weiter unten zeigt **Abhängigkeiten**, was aus der Wand aufgebaut ist und woraus die Wand aufgebaut ist. **Verwendet von** listet die Elemente, die sich mit der Wand ändern: die Wände, an die sie anschließt, ihre Türen und Fenster, eine an ihr befestigte Wand. Ein Klick auf einen Namen wählt das Element aus. **Aufgebaut aus** listet die Elemente, von denen die Wand abhängt, dann ihren Typ und ihre Materialien. Wenn Sie ein Element löschen, von dem andere abhängen, nennt eine Meldung die neu aufgebauten; <kbd>Ctrl</kbd> <kbd>Z</kbd> holt alles zurück.

**Umkehren** dreht die Schichten der Wand auf die andere Seite. Sie können auch auf die kleinen Pfeile klicken, die im Grundriss neben einer ausgewählten Wand angezeigt werden.

## Wandtypen

Ein Wandtyp ist eine Folge von Schichten, von der Seite rechts der Richtung, in der die Wand gezeichnet wird (die Außenseite, wenn Sie gegen den Uhrzeigersinn um ein Gebäude zeichnen), zur Seite links davon. Jede Schicht hat ein Material und eine Dicke, und eine Schicht ist der **Kern**. Die Achse der Wand verläuft entlang des Kerns. Um die Typen zu bearbeiten, wählen Sie **Architektur → Wandtypen**. Eine Schicht außer dem Kern kann eine eigene Höhe haben: **Von** und **Bis** legen fest, wie hoch sie über dem Wandfuß reicht (ein Fliesensockel bis 1500, eine Bekleidung ab 300); leer läuft sie über die ganze Höhe.

Die Spalte **Priorität** (0 bis 100) gibt an, wie die Schicht auf die Schichten einer anderen Wand trifft, wo die Wände sich verbinden: Sie läuft durch deren Schichten mit niedrigerer Priorität und endet an der ersten mit gleicher oder höherer. Leer nimmt die Schicht die Priorität ihres Materials unter **Verwalten → Schnittreihenfolge**, sonst 50 für den Kern und 0 für die übrigen Schichten (die grau angezeigte Zahl). So wird ein Betonkern mit 100 von einem Ständerkern mit 99 nicht unterbrochen und läuft durch die Ständerwand, und ein harter Putz mit 60 hält den Kern einer Wand auf, die an ihm endet. Der IFC-Export schreibt sie als Priority der Schicht, der Import liest sie wieder ein.

Eine Schicht außer dem Kern kann auch über ihre Höhe das Material wechseln: Die Schaltfläche **Schichten** am Ende ihrer Zeile fügt ein Stück hinzu (Fliesen, standardmäßig 2100), von der Unterkante der Schicht aufwärts; fügen Sie weitere für ein Band hinzu (Anstrich bis 900, Fliesen bis 1500), und über dem letzten Stück läuft die Schicht in ihrem eigenen Material bis oben weiter. Jedes Stück wird gezeichnet, in Grundriss und Schnitten geschnitten und in seinem eigenen Material gemessen.

Eine Schicht außer dem Kern kann die Dicke 0 haben, für ein Material, das zu dünn zum Modellieren ist, aber gemessen werden muss: eine Abdichtung, eine Dampfsperre, ein Anstrich. Sie bildet keinen Körper; Grundrisse und Schnitte zeichnen sie als kräftige gestrichelte Linie, wo sie zwischen zwei Schichten liegt (an einer Außenfläche ist die Linie der Fläche ihre eigene), die Zeichnung der Typenaufbauten benennt sie wie die anderen, und sie wird nach Fläche gemessen: die Fläche der Schicht, auf der sie liegt, abzüglich der Öffnungen, in den Mengen der Wand und in der Mengenermittlung nach Material.

![Eine Putzschicht mit Fliesen bis 2100 und dem Putz darüber](shots/wall-layer-parts.webp)

![Wandtypen: die Schichten einer Ziegelwand](shots/wall-types.webp)

Die Legende über den Schichten zeigt die Wand im Grundriss so, wie eine ausgewählte Wand sie zeigt: die gestrichelte Achse mit einer Pfeilspitze in der Richtung, in der sie gezeichnet wurde, **R** auf ihrer rechten und **L** auf ihrer linken Seite. Die Schichten sind von 1 auf der Seite R bis zur letzten auf der Seite L nummeriert, mit denselben Nummern wie die Zeilen der Tabelle; der Kern, auf dem die Wandlinie liegt, ist hervorgehoben. Eine mit **Umkehren** gedrehte Wand hat ihre Schichten andersherum, von L nach R. Die Meldung einer abgelehnten Speicherung verschwindet, sobald Sie den Typ wieder ändern.

- **+ Schicht** fügt eine Schicht hinzu. Die Pfeile verschieben eine Schicht nach oben oder unten.
- **Zonen in der Höhe** teilt den Kern von unten nach oben, zum Beispiel in eine erste Steinlage, das Mauerwerk und einen Ringbalken.
- **Stürze** wählt die Stürze über Türen und Fenstern: Fertigteil oder Ortbeton, mit ihrem Auflager und ihrer Höhe.
- **Damit zeichnen** schließt den Dialog und beginnt, Wände dieses Typs zu zeichnen.

Eine Änderung an einem Typ ändert jede Wand dieses Typs.

**Import…**, **Export CSV** und **Export JSON** unter der Typenliste bringen die Wand- und Deckentypen aus einem Projekt in ein anderes oder in eine Tabellenkalkulation und zurück: die CSV hat eine Zeile je Schicht (Kind, Type, Mark, Layer, Material, Thickness (mm), Core, From, To), die Zeilen eines Typs hintereinander, und auch Excels Semikolons und Dezimalkommas werden gelesen. Beim Import bekommt ein gleichnamiger Typ die Schichten der Datei, die übrigen werden angelegt, in einem Schritt rückgängig zu machen; ein Material, das dem Projekt fehlt, wird nach seinem Namen angelegt. Die Liste **Typenaufbauten** (Schedules → + → Type assemblies) zeigt jeden Wand- und Deckentyp mit Kennzeichen, Gesamtdicke, der Zahl der Bauteile, die ihn verwenden, und seinen Schichten der Reihe nach.

## Wände unter einem Dach

Wände auf derselben Ebene wie ein Dach enden von selbst unter ihm und steigen bis zu seinen Giebeln. Bei einer Wand auf einer anderen Ebene wählen Sie sie aus und wählen **Architektur → An Dach anbinden** (<kbd>AR</kbd>), dann klicken Sie auf das Dach – oder auf einen anderen Körper, bis zu dem die Wand reichen soll: einen Studio-Volumenkörper, ein importiertes Element. Siehe [Decken und Dächer](floors-roofs.md).
