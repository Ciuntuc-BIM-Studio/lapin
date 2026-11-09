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
- **Vorhangfassade** (<kbd>CW</kbd>) zeichnet eine verglaste Wand mit Pfosten, Riegeln und Paneelen; ihr Typ kann deren wirkliche Form aus einer [Profilbibliothek](profiles.md) nehmen.
- Wände verbinden sich von selbst, wo sie aufeinandertreffen: eine Ecke als Gehrung, eine Wand, die auf einer anderen endet, als T, eine, die eine andere kreuzt, als X. An einem T oder X laufen die Schichten ineinander, wie gebaut: Der Kern der endenden Wand läuft durch die Bekleidungen der anderen bis zu deren Kern, und seine Bekleidungen enden an der Fläche der anderen, wo sie auf ihresgleichen treffen, sodass der Grundriss einen Aufbau ohne Linie hindurch zeigt. **Verbinden** (<kbd>JW</kbd>), **Gehrung** und **Stumpf** ändern, wie zwei Wände aufeinandertreffen, wenn das nicht gewollt ist; **Neue Ecken auf Gehrung** macht Stumpf zur Vorgabe auf diesem Rechner. In beiden Fällen läuft jede Schicht für sich um die Ecke: die Bekleidungen treffen auf ihresgleichen und laufen ungebrochen weiter, die Kerne treffen sich auf der Diagonale (Gehrung) oder ein Kern läuft bis zur Außenseite des anderen (Stumpf), so zeigt der Grundriss mehrschichtiger Wände saubere Ecken. Jede Schicht läuft in die andere Wand durch deren Schichten mit niedrigerer **Priorität** und endet an der ersten mit gleicher oder höherer (siehe **Wandtypen** unten); so treffen standardmäßig Kerne auf Kerne und Bekleidungen enden an den Oberflächen. An einer Ecke läuft ein Kern mit höherer Priorität durch den anderen, ob Gehrung oder Stumpfstoß. Die **Schnittpriorität** einer Wand führt sie nie in eine Wand, mit der sie verbunden ist: Zwischen verbundenen Wänden entscheidet sie nur, welcher Kern durchläuft, wenn beide Kerne dieselbe Priorität haben (an einer stumpfen Ecke oder einer Kreuzung).
- **Tauschen** ändert, welche der beiden Wände durchläuft.

**Die Linie, auf der Sie zeichnen.** Solange das Werkzeug Wall aktiv ist, wählt die Liste in der Leiste der Befehlszeile die Linie, auf die Sie klicken: **Kernmitte** (die Achse, wie bisher), **Kern, Außenseite**, **Kern, Innenseite**, **Außenseite** oder **Innenseite**. Außen ist rechts der Wand in Zeichenrichtung (zeichnen Sie gegen den Uhrzeigersinn um das Gebäude, damit es außen liegt). Die Achse der Wand bleibt auf ihrem Kern, von der Linie versetzt, und nacheinander auf derselben Seite gezeichnete Wände treffen sich an der Ecke dieser Seite.

## Eine Wand ändern

Klicken Sie auf eine Wand, um sie auszuwählen. Ihre Griffe erscheinen. Ziehen Sie die Enden, um sie zu verlängern oder zu verkürzen, oder ziehen Sie das Kreuz in der Mitte, um sie zu verschieben.

![Eine ausgewählte Wand mit ihren Griffen](shots/wall-selected.webp)

Der Eigenschaftenbereich zeigt die ausgewählte Wand. Oben im Bereich stehen ihr Typ und ihre Schichten. Darunter folgen Länge, Höhe, Versatz unten, Geschoss und Bauphase. Ändern Sie einen beliebigen Wert, und das Modell folgt. Die Werte jeder Wand sind ihre eigenen. Ihre **Dicke** (Thickness) ergibt sich aus den Schichten ihres Typs: Geben Sie eine neue ein, und die gewählten Wände erhalten den Typ mit demselben Aufbau und dieser Dicke, der Kern angepasst und die Bekleidungen beibehalten. Lapin nimmt den Typ des Projekts, wenn einer genau diesen Aufbau hat, sonst legt es einen an, nach ihrem benannt (*Partition wall 130mm* wird *Partition wall 150mm*). Die anderen Wände des alten Typs bleiben, wie sie sind, und <kbd>Ctrl</kbd> <kbd>Z</kbd> macht es in einem Schritt rückgängig.

![Die Eigenschaften einer Wand](shots/wall-properties.webp)

Weiter unten zeigt **Abhängigkeiten**, was aus der Wand aufgebaut ist und woraus die Wand aufgebaut ist. **Verwendet von** listet die Elemente, die sich mit der Wand ändern: die Wände, an die sie anschließt, ihre Türen und Fenster, eine an ihr befestigte Wand. Ein Klick auf einen Namen wählt das Element aus. **Aufgebaut aus** listet die Elemente, von denen die Wand abhängt, dann ihren Typ und ihre Materialien. Wenn Sie ein Element löschen, von dem andere abhängen, nennt eine Meldung die neu aufgebauten; <kbd>Ctrl</kbd> <kbd>Z</kbd> holt alles zurück.

**Umkehren** dreht die Schichten der Wand auf die andere Seite. Sie können auch auf die kleinen Pfeile klicken, die im Grundriss neben einer ausgewählten Wand angezeigt werden.

Eine aus einem importierten IFC erkannte Wand, deren Ende die Datei schräg schneidet, behält diesen Schnitt: **Schnittwinkel am Anfang** und **Schnittwinkel am Ende** in ihren Eigenschaften, in Grad gegenüber dem rechten Winkel. Ein Anschluss an diesem Ende tritt an seine Stelle.

**Schraffurausrichtung** legt fest, wie die Schichten dieser Wand im Grundriss schraffiert werden. **material** folgt der Einstellung jedes Materials. **axis** legt die Schraffur jeder Schicht entlang der Wandachse, vom Anfang der Achse aus, sodass eine schräge Ziegelwand ihre Lagen entlang der Wand zeigt und sie von Schicht zu Schicht durchlaufen. **view** beginnt jede Schraffur am Ursprung der Ansicht, ungedreht.

### Eine Wand nach ihrem Profil

**Profil…** in den Eigenschaften einer Wand zeichnet ihre Oberkante in der Ansicht, vom Anfang zum Ende gesehen: einen Giebel, eine Schräge, Stufen, eine Wand, die einer Treppe folgt. Ziehen Sie die Punkte (**Umschalt** behält die Höhe eines Punkts), klicken Sie auf die obere Linie, um einen hinzuzufügen, klicken Sie mit rechts auf einen Punkt, um ihn herauszunehmen, oder tippen Sie sie in Millimetern in die Tabelle. **Beginnen mit** gibt ein **Giebel**, eine **Neigung** oder **Stufen** bis zu der Höhe, die Sie eintippen. Eine Stufe sind zwei Punkte übereinander. **Anwenden** formt die Wand; **Gerade Oberkante** macht sie wieder gerade.

Die Wand bleibt eine Wand: ihre Dicke, ihre Schichten, ihre Anschlüsse und ihre Türen und Fenster bleiben, wie sie waren, und sie wird mit ihrer Form nach IFC exportiert.

![Ein Giebel, im Profileditor gezeichnet](shots/wall-profile-editor.webp)

![Die Westwand des Hauses als Giebel, ihr Fenster erhalten](shots/wall-profile-3d.webp)

### Rand und Stäbe einer Vorhangfassade

**Profil…** in den Eigenschaften einer Vorhangfassade zeichnet ihren Rand in der Ansicht, wie bei einer Wand: einen Giebel, eine Schräge, Stufen oder einen beliebigen Umriss, mit Punkten zum Ziehen, Hinzufügen oder Entfernen und einer Tabelle in Millimetern. Die Fassade reicht bis zu ihrem höchsten Punkt. Ihr Raster bekommt eine Linie dort, wo der Rand knickt (ein Knick innerhalb von anderthalb Pfostenbreiten einer Rasterlinie wird auf sie verschoben); jedes Feld behält die Riegel, die unter seinen tiefsten Punkt passen; das Paneel über dem letzten füllt bis zum Rand, und ein Randstab folgt ihm, geneigt; die Pfosten werden auf ihn geschnitten. Die Einstellungen des Typs bleiben, und **Profil…** öffnet den Rand wieder zum Ändern, ohne Neuaufbau; **Gerade Oberkante** gibt das Rechteck zurück. Es zeigt sich in 3D, Ansichten und Schnitten, die Listen messen Paneele und Stäbe wie gezeichnet, und das IFC trägt sie als Teile.

![Ein Giebel als Rand einer Vorhangfassade gezeichnet](shots/curtain-boundary-editor.webp)

![Die Vorhangfassade unter ihrem Giebel, in 3D](shots/curtain-boundary-3d.webp)

**Stäbe…** gibt ausgewählten Pfosten und Riegeln ein eigenes Profil. Die Ansicht der Fassade zeigt jeden Stab: Klicken Sie einen an (<kbd>Umschalt</kbd> oder <kbd>Strg</kbd>/<kbd>⌘</kbd> fügt weitere hinzu, oder nehmen Sie alle Pfosten oder alle Riegel), wählen Sie ein Profil aus den [Profilbibliotheken](profiles.md) oder einen einfachen Quader, seine **Drehung** und **Spiegelung**, seine Ausrichtung quer zum Stab (Vorder- oder Rückseite auf seiner Ebene) und entlang (Anfangs- oder Endkante auf der Rasterlinie oder Höhe) und seine Versätze quer und aus dem Gebäude heraus; das Bild daneben zeigt den Querschnitt, wie er sitzt. **Auf die gewählten anwenden** gibt ihnen den Stil, **Zurück zu dem des Typs** nimmt ihn weg. Das eigene Profil eines Typs hat dieselben Lagefelder in **Vorhangfassaden…**, für jede Fassade des Typs. Das Profil bleibt eine Kopie im Projekt: **Aus der Bibliothek aktualisieren** ändert jeden daraus gemachten Stab, in dieser Fassade und in anderen.

![Stäbe: ein Profil auf den gewählten Pfosten](shots/curtain-members-dialog.webp)

![Zwei Pfosten mit eigenem Profil, in 3D](shots/curtain-members-3d.webp)

## Wandtypen

Ein Wandtyp ist eine Folge von Schichten, von der Seite rechts der Richtung, in der die Wand gezeichnet wird (die Außenseite, wenn Sie gegen den Uhrzeigersinn um ein Gebäude zeichnen), zur Seite links davon. Jede Schicht hat ein Material und eine Dicke, und eine Schicht ist der **Kern**. Die Achse der Wand verläuft entlang des Kerns. Um die Typen zu bearbeiten, wählen Sie **Architektur → Wandtypen**. Eine Schicht außer dem Kern kann eine eigene Höhe haben: **Von** und **Bis** legen fest, wie hoch sie über dem Wandfuß reicht (ein Fliesensockel bis 1500, eine Bekleidung ab 300); leer läuft sie über die ganze Höhe.

Die Spalte **Priorität** (0 bis 100) gibt an, wie die Schicht auf die Schichten einer anderen Wand trifft, wo die Wände sich verbinden: Sie läuft durch deren Schichten mit niedrigerer Priorität und endet an der ersten mit gleicher oder höherer. Leer nimmt die Schicht die Priorität ihres Materials unter **Verwalten → Schnittreihenfolge**, sonst 50 für den Kern und 0 für die übrigen Schichten (die grau angezeigte Zahl). So wird ein Betonkern mit 100 von einem Ständerkern mit 99 nicht unterbrochen und läuft durch die Ständerwand, und ein harter Putz mit 60 hält den Kern einer Wand auf, die an ihm endet. Der IFC-Export schreibt sie als Priority der Schicht, der Import liest sie wieder ein.

Die Spalte **Notiz** sagt, wie die Schicht in diesem Typ verlegt wird (*als durchgehende Schicht, Baumit SupraFix*): sie gehört zum Typ, nicht zum Material, und ein Schichtaufbau-Etikett schreibt sie nach der Schicht; IFC trägt sie als Description der Schicht. Hat das Material der Schicht Katalogdicken (**Verwalten → Materialien → Daten**), bietet der kleine Pfeil neben der Dicke sie an, und ein Warnzeichen markiert eine Schicht anderer Dicke.

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

## Wandprofile und Wandnuten

**Architektur → Wandprofil** (<kbd>WB</kbd>) führt ein Profil an der Fläche einer Wand entlang: ein Gesims, ein Band, einen Sockel, eine Fensterbank. **Wandnut** (<kbd>RV</kbd>) schneidet es in die Wand: eine Nut, eine Schattenfuge. Wählen Sie Profil und Höhe in der Leiste der Befehlszeile und klicken Sie dann auf die Wandfläche. In 3D sitzt das Profil in der angeklickten Höhe; im Grundriss in der eingegebenen Höhe, auf der angeklickten Seite. **von oben** misst die Höhe von der Wandkrone nach unten, für ein Gesims unter der Traufe. **Profil an den ausgewählten Wänden** und **Nut in den ausgewählten Wänden** setzen dasselbe an jede ausgewählte Wand, auf ihre Außenseite.

Ein Profil läuft über die ganze Länge der Fläche und ist an den Türen und Fenstern unterbrochen, die es kreuzen würde. Wo zwei Wände in einer Ecke zusammentreffen und beide auf den zusammentreffenden Flächen ein Profil gleicher Form in gleicher Höhe tragen, werden die beiden auf Gehrung ineinandergeführt, ein Gesims um die Ecke. Wählen Sie ein Profil aus, um es in den Eigenschaften zu ändern: sein Profil, sein Material, seine Fläche (**Seite wechseln**), seine Höhe, seinen Abstand von der Fläche, wo es entlang der Wand beginnt und endet und ob es an Öffnungen unterbrochen ist. Die Eigenschaften einer Wand zeigen die Profile und Nuten, die sie trägt.

Profile und Nuten gehen mit ihrer Wand, wenn sie kopiert, in ein anderes Geschoss verschoben oder gelöscht wird. Sie werden nach Länge und Volumen gemessen (bei einer Nut das herausgenommene Volumen). In IFC ist ein Profil eine IfcCovering MOLDING und eine Nut ein IfcOpeningElement RECESS in seiner Wand; beide kommen so zurück, wie sie waren.

![Gesimse, Bänder und eine Nut an den Außenwänden eines Hauses](shots/wall-sweeps-3d.webp)

### Profile am Wandtyp

Ein Profil, das jede Wand eines Typs tragen soll (ein Sockelband, ein Gesims, eine Schattenfuge), gehört zum Typ. In **Architektur → Wandtypen** zeigt **Profile und Nuten** unter den Schichten sie an, eine Zeile je Profil: das Profil, **Wandprofil** oder **Wandnut**, die Seite (R, die Seite der ersten Schicht des Typs, oder L, wie die Legende sie nennt), die Höhe über dem Wandfuß (oder mit **Von oben** unter der Wandkrone), den Versatz von der Wandseite nach außen, das Material eines Profils und seine Rückläufe. **Wandprofil** und **Wandnut** unter der Tabelle fügen eine Zeile hinzu. Speichern Sie den Typ: Jede Wand des Typs erhält sie, ebenso eine Wand, die mit dem Typ gezeichnet oder auf ihn umgestellt wird. Es sind Profile wie die anderen: gemessen, in der Wandprofilliste aufgeführt, an den Ecken, an denen Wände des Typs zusammentreffen, auf Gehrung geführt, in IFC geschrieben. Die Profile einer umgedrehten Wand wechseln mit ihren Schichten die Seite. **JSON exportieren** unter der Typenliste nimmt sie mit, mit den Umrissen der Profile und den Materialien nach Namen (die CSV enthält nur die Schichten).

Wählen Sie eines an einer Wand aus: Die Eigenschaften zeigen **Vom Typ**. Ändern Sie seine Höhe, seinen Versatz oder ein anderes Feld, wird dieses Feld zum eigenen der Wand (**Eigene:** Höhe): spätere Änderungen am Typ lassen es, wie es ist. **Zurück zum Typ** gibt ihm wieder die Werte des Typs. **An dieser Wand ausschalten**, oder das Löschen, lässt das Profil des Typs an dieser Wand aus; **Profile des Typs wieder einschalten** unter **Profile und Nuten** der Wand holt sie zurück.

![Ein Profil am Wandtyp, im Dialog Wandtypen](shots/wall-type-sweeps.webp)

### Rückläufe

Ein Profil, das ein freies Ende seiner Wand erreicht (dort ist keine Wand angeschlossen), läuft zurück: Am Ende auf Gehrung geschnitten, führt es sein Profil um die Ecke auf die Stirnseite der Wand und endet in der Ebene der Wandseite, sodass das Profil geschlossen ist, statt seinen Schnitt zu zeigen. **Rückläufe an freien Enden** in den Eigenschaften wählt beide (Vorgabe für ein Profil), am Anfang, am Ende oder keine, für gerade Enden. Eine Nut läuft nie zurück. An einer Ecke, die an eine andere Wand angeschlossen ist, läuft ein Profil nicht zurück: Es führt bis zur Ecke oder in ein gleiches Profil auf der anderen Wand. An Türen und Fenstern endet es gerade; aktivieren Sie **Rückläufe an Öffnungen**, damit es auch um die Leibungen zurückläuft. **Rücksprung vom Anfang** und **Rücksprung vom Ende** lassen es vor den Enden der Wandseite enden (dort gerade), und **Profil umgekehrt** stellt sein Profil auf den Kopf.

### Ein Profil verschieben und kopieren

Wählen Sie ein Profil aus und verwenden Sie **MOVE** oder **COPY** wie bei jedem Element; seine Wand bleibt stehen. Was die Verschiebung entlang der Wand zurücklegt, schiebt das Profil an ihr entlang (ein Profil über die ganze Wandseite erhält dann einen eigenen Anfang und ein eigenes Ende); was sie quer zur Wand zurücklegt, bleibt unberücksichtigt. In einer Ansicht oder einem Schnitt nach oben oder unten verschoben, ändert sich seine Höhe. Eine Kopie landet auf derselben Wand, versetzt, oder auf einer anderen Wand, wenn sie in ihr landet, auf der Seite, auf der sie landet. **ROTATE** und **MIRROR** lassen es an seiner Wand: an der Wandachse gespiegelt, wechselt es auf die andere Seite. Ein Profil des Typs, das Sie verschieben, behält seinen neuen Platz als eigenen; auf eine andere Wand verschoben, wird es ein eigenes Profil dieser Wand, und das des Typs bleibt an der ersten ausgeschaltet.

### Profile

**Verwalten → Profile** enthält die Profile des Projekts, die Umrisse, mit denen Wandprofile, Nuten und Streifenfundamente geführt werden. **Neues Profil** beginnt mit einem Katalogprofil und seinen Maßen: ein Rechteck, ein gefastes Band, ein Gesims, ein Halbrund, eine Fensterbanknase, ein Band mit Tropfnase; eine V-Nut, eine Rechteck- oder eine Rundnut; für Fundamente rechteckig, in 2 oder 3 Stufen, mit geneigter Oberseite, exzentrisch, mit Sockelwand. Oder wählen Sie **Eingegebene Eckpunkte**: jeder Eckpunkt u v in Millimetern, dazwischen ein Semikolon, eine dritte Zahl macht die Seite ab diesem Eckpunkt zu einem Bogen. **Aus einer gezeichneten Kontur** liest eine geschlossene Polylinie oder ein Rechteck, mit Zeichnen gezeichnet und in seiner Ansicht ausgewählt. Der Kreis in der Vorschau ist der Einfügepunkt: bei einem Wandprofil liegt er auf der Wandfläche in der Höhe des Profils, u zeigt aus der Fläche heraus (bei einer Nut in die Wand hinein). Ändern Sie ein Profil, folgt alles, was es verwendet.

![Der Dialog Profile](shots/profiles.webp)

### Bekleidungen

Bekleidungen modellieren Sie mit den Schichten der Wand selbst, nicht mit Profilen. Fügen Sie dem Wandtyp außen eine Ausbauschicht hinzu (Naturstein, Holzschalung, Faserzement, ein WDVS), mit ihrer Dicke und ihrem Material. **Von** und **Bis** geben ihr eine eigene Höhe (ein Natursteinsockel bis 600, darüber Putz), und die Schaltfläche **Schichten** der Schicht wechselt ihr Material in Bändern über die Höhe. Für eine Bekleidung, die von der Wand absteht, zeichnen Sie eine eigene Wand auf die Fläche (die Linienoption **Außenseite** des Wandwerkzeugs), mit einem Typ aus den Schichten der Bekleidung. Eine Schalung, Brett für Brett verlegt (senkrechte oder waagerechte Bretter, Latten), wird noch nicht modelliert.

## Wände unter einem Dach

Wände auf derselben Ebene wie ein Dach enden von selbst unter ihm und steigen bis zu seinen Giebeln. Bei einer Wand auf einer anderen Ebene wählen Sie sie aus und wählen **Architektur → An Dach anbinden** (<kbd>AR</kbd>), dann klicken Sie auf das Dach – oder auf einen anderen Körper, bis zu dem die Wand reichen soll: einen Studio-Volumenkörper, ein importiertes Element. Siehe [Decken und Dächer](floors-roofs.md).
