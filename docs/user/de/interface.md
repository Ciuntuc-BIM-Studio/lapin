---
title: Die Oberfläche und Tastenkürzel
order: 15
summary: Die Modi Komfortabel, Kompakt und Fokus, das Einklappen der Seitenbereiche und die Tastenkürzel, die Sie ändern können.
---

<!-- video interface -->

## Arbeitsbereiche

Ein Arbeitsbereich zeigt nur die Registerkarten des Menübands, die Ihre Arbeit braucht, in der Reihenfolge, in der sie sie braucht. Sie wählen ihn über die Schaltfläche neben **Datei**, die den Arbeitsbereich nennt, in dem Sie sind, oder auf dem Startbildschirm, oben in **View → Interface…** oder über **Commands…** (Workspace: …): **Alle** (jede Registerkarte Ihrer Edition), **Architektur**, **Tragwerk und Bewehrung**, **TGA**, **Gelände und Baustelleneinrichtung** oder **Zeichnen 2D**. Das Projekt bleibt eines: die Werkzeuge einer ausgeblendeten Registerkarte sind weiter in Commands… und den Tastenkürzeln, und ein Modell aus einem Arbeitsbereich öffnet sich vollständig in einem anderen.

![Die Arbeitsbereiche in View → Interface…](shots/workspaces.webp)

Sind die Werkzeuge des Menübands breiter als das Fenster, scrollt das Mausrad über ihnen sie seitwärts.

Die Registerkarten bleiben in einer Zeile. Ist das Fenster für alle zu schmal, wandern die letzten unter **Mehr** am Ende der Zeile, und die Registerkarte, auf der Sie sind, bleibt immer sichtbar.

Die Registerkarten sind nach der Arbeit gruppiert, der sie dienen. **Innenausbau** enthält den Ausbau und die Möbel. **Gelände** enthält das Gelände, die Außenanlagen und die Baustelleneinrichtung (Gerüste und Schalung). **Ausführung** enthält die Terminplanung und die Kostenberechnung (4D und 5D).

Zeigen, verbergen oder verschieben Sie unter **Tabs** im selben Dialog eine Registerkarte, und die Wahl wird **Mein Arbeitsbereich**, auf diesem Computer für das nächste Mal gespeichert; die anderen Arbeitsbereiche bleiben zum Wechseln da. **View** bleibt immer, denn Interface… liegt darin.

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

Unter den Teilen **Registerkarten**: Entfernen Sie das Häkchen bei den Registerkarten des Menübands, die Sie nicht brauchen (ein Architekt kann Structure, Einfache Bewehrung und MEP ausblenden, ein Ingenieur Architecture und Site). View bleibt immer, denn dort liegt Oberfläche…. Die Werkzeuge einer ausgeblendeten Registerkarte bleiben in Befehle… und in den Tastenkürzeln.

Lapin speichert Ihre Wahl auf diesem Computer, für Sie. Die Desktop-App und der Browser speichern ihre jeweils getrennt. Im Browser startet Lapin im Modus Kompakt, wenn das Fenster weniger als 800 Pixel hoch ist, bis Sie eine Wahl treffen.

## Schnellwerkzeuge

Die Schnellwerkzeuge sind die nummerierten Plätze am linken Rand der Ansicht, direkt am Projektbrowser, sodass sie mit ihm wandern; klein und durchscheinend, bis der Mauszeiger darüber kommt (View → Interface… kann sie stattdessen unten in der Ansicht schweben lassen); die Tasten <kbd>1</kbd>–<kbd>9</kbd> starten sie. Jeder Arbeitsbereich hat seine eigenen: Sie beginnen so, wie Ihre Edition sie mitbringt, und Sie wählen sie selbst:

- Ziehen Sie ein Werkzeug aus dem Menüband auf einen Platz. Ein Werkzeug steht nur auf einem Platz, also verlässt es den bisherigen;
- ziehen Sie einen Platz auf einen anderen, um sie zu tauschen;
- Rechtsklick auf einen Platz: **Werkzeug wählen…**, **Diesen Platz leeren** oder **Schnellwerkzeuge zurücksetzen**;
- **View → Quick tools…** legt die neun Plätze über Listen fest, und **Die der Edition** holt die Ihrer Edition zurück.
- ein Werkzeug, das Elemente eines Typs setzt (Wand, Tür, Decke, Treppe…), kann einen der Typen des Projekts mitnehmen: Wählen Sie ihn im Kontextmenü des Platzes oder neben dem Werkzeug in **View → Quick tools…**. Der Platz zeigt dann den Namen des Typs und startet das Werkzeug mit diesem Typ, sodass ein Platz die Ziegelwände und ein anderer die Trennwände halten kann. Der Typ wird über seinen Namen gemerkt: Der Platz funktioniert in jedem Projekt mit einem Typ dieses Namens, und in einem ohne ihn nimmt das Werkzeug seinen aktuellen Typ.

Unten schwebend zeigt jeder Platz den Namen seines Werkzeugs, und ein leerer Platz nach dem letzten wartet auf ein Werkzeug. Lapin speichert Ihre Schnellwerkzeuge auf diesem Computer, für Sie.

Der Pfeil am Ende der Leiste blendet die Schnellwerkzeuge aus; an ihrer Stelle bleibt ein kleiner Griff **Schnellwerkzeuge**, der sie zurückholt. Sie werden in den Modellansichten (Grundrisse, 3D, Schnitte) und in Plänen und Listen getrennt ausgeblendet, sodass sie bei den Dokumenten aus und bei den Zeichnungen an bleiben können.

![Die Schnellwerkzeuge, die Treppe auf Platz 7 und Platz 2 leer](shots/quick-tools.webp)

![View → Quick tools…](shots/quick-tools-dialog.webp)

## Meine Werkzeuge

**Meine Werkzeuge** ist ein eigener Reiter des Menübands, der erste in der Reiterzeile: Du legst darin nur die Werkzeuge ab, die Du benutzt, aus beliebigen Reitern, in Gruppen mit Deinen Namen.

- **Werkzeuge hineinlegen.** Rechtsklick auf ein Werkzeug im Menüband → **Zu Meine Werkzeuge hinzufügen**, **Zu Meine Werkzeuge hinzufügen in…** eine Gruppe, oder **Zu einer neuen Gruppe in Meine Werkzeuge hinzufügen**. Oder ziehe ein Werkzeug aus einem anderen Reiter auf den Reiter **Meine Werkzeuge**: kurz darüber gehalten, öffnet er sich, und Du lässt das Werkzeug dort fallen, wo eine Linie zeigt, dass es landet.
- **Im Menüband selbst ordnen.** Ziehe ein Werkzeug vor oder hinter ein anderes, in seiner Gruppe oder in eine andere Gruppe; ziehe eine Gruppe an ihrem Namen, um sie zwischen den Gruppen zu verschieben. Eine Linie zeigt, wo es landet, die anderen rücken beiseite, und Ablegen an anderer Stelle ändert nichts. Doppelklick auf den Namen einer Gruppe benennt sie um. Rechtsklick auf ein Werkzeug: **Klein machen** oder **Groß machen**, **Weiter nach vorn**, **Weiter nach hinten** und **Aus Meine Werkzeuge entfernen**; Rechtsklick auf den Namen einer Gruppe: **Gruppe umbenennen**, **Gruppe löschen**, **Neue Gruppe** und **Letzte Änderung rückgängig machen**. Mit der Tastatur verschiebt <kbd>Alt</kbd> <kbd>←</kbd> oder <kbd>→</kbd> das fokussierte Werkzeug, <kbd>Entf</kbd> entfernt es.
- **Meine Werkzeuge anpassen…** (das Zahnrad am Ende des Reiters, View → Interface oder Commands…) zeigt links alle Werkzeuge der Anwendung, durchsuchbar und nach Reitern gruppiert, rechts Deinen Reiter. Ziehe Werkzeuge hinüber oder drücke **+** (oder <kbd>Enter</kbd>), um eines hinzuzufügen; ziehe auf der rechten Seite, um zu ordnen; ziehe ein Werkzeug zurück nach links, um es herauszunehmen. Jede Zeile hat außerdem Schaltflächen zum Verschieben nach oben oder unten, zum Anzeigen als großes oder kleines Werkzeug, eine Liste zum Verschieben in eine andere Gruppe und **×** zum Entfernen. Mit der Tastatur verschieben <kbd>Alt</kbd> <kbd>↑</kbd> <kbd>↓</kbd> das fokussierte Werkzeug (auch über die Enden seiner Gruppe), <kbd>Alt</kbd> <kbd>←</kbd> <kbd>→</kbd> in die Gruppe davor oder danach, <kbd>Entf</kbd> entfernt es und <kbd>Strg</kbd> <kbd>Z</kbd> macht die letzte Änderung rückgängig. **Rückgängig** und **Zurücksetzen** stehen unten, ein falsches Ablegen kostet also nichts.
- **Exportieren…** und **Importieren…** speichern den Reiter als kleine Datei, die Du auf einen anderen Rechner mitnehmen oder einem Kollegen geben kannst.

Lapin behält Meine Werkzeuge auf diesem Rechner, für Dich (Desktop-App und Browser getrennt). Ein Werkzeug, das Deine Edition nicht hat, ist ausgeblendet, nicht verloren: Es kommt mit seinem Modul zurück.

![Meine Werkzeuge](shots/my-tools-empty.webp)

![Meine Werkzeuge](shots/my-tools-menu.webp)

![Meine Werkzeuge: zwei Gruppen, Wände und Öffnungen](shots/my-tools-tab.webp)

![View → Interface → Meine Werkzeuge anpassen…](shots/my-tools-dialog.webp)

## Der Katalog

Die Registerkarte **Katalog** im Projektbrowser zeigt die Typen des Projekts für jedes Werkzeug, das Elemente mit Typ setzt: Wände, Türen, Fenster, Decken, Vorhangfassaden, Treppen, Rampen, Unterdecken und Sockelleisten. Jeder Typ ist klein gezeichnet (die Schichten einer Wand als Grundrissschnitt, die einer Decke im Schnitt, die Ansicht einer Tür oder eines Fensters, das Profil einer Sockelleiste), dazu in wenigen Worten, was er ist.

- Ein Klick auf eine Karte setzt Elemente dieses Typs: Das Werkzeug startet damit;
- ziehen Sie eine Karte auf einen Schnellplatz, um dort das Werkzeug mit diesem Typ zu behalten;
- **Typen bearbeiten…** öffnet den Dialog, in dem diese Typen angelegt und geändert werden.

![Der Katalog: die Wandtypen](shots/catalog.webp)

![Die Fenstertypen, nach ihrer Ansicht gezeichnet](shots/catalog-windows.webp)

## Die Seitenbereiche einklappen

In jedem Modus klappen der Projektbrowser und der Eigenschaftenbereich zu einer schmalen Leiste an ihrer Seite ein:

- die Schaltfläche oben rechts im Bereich klappt ihn ein, und der Pfeil auf seiner Leiste klappt ihn aus;
- doppelklicken Sie auf den Rand zwischen dem Bereich und der Ansicht;
- <kbd>Strg</kbd> <kbd>B</kbd> klappt den Browser ein oder aus, <kbd>Strg</kbd> <kbd>⇧</kbd> <kbd>B</kbd> die Eigenschaften und <kbd>Tab</kbd> beide.

Klicken Sie auf ein Symbol auf der Leiste eines eingeklappten Bereichs, um den Bereich über der Ansicht zu öffnen, ohne ihn auszuklappen. Er verschwindet mit <kbd>Esc</kbd> oder einem Klick an eine andere Stelle; seine Pinnnadel hält ihn offen. Ziehen Sie am Rand eines Bereichs, um ihn breiter zu machen; Lapin merkt sich die Breite.

![Ein eingeklappter Browser, von seiner Leiste aus über der Ansicht geöffnet](shots/browser-overlay.webp)

## Bereiche auf beiden Seiten

**Ansicht → Bereiche** (View → Panels) stellt den Projektbrowser und das Eigenschaftenfenster dorthin, wo Sie sie wollen, wie in Revit: **Browser links, Eigenschaften rechts** (Standard), **Browser rechts, Eigenschaften links**, **Beide Bereiche links** oder **Beide Bereiche rechts**. Stehen beide auf einer Seite, liegen sie übereinander; ziehen Sie die Trennlinie dazwischen, um die Höhe zu teilen (Doppelklick halbiert), und den Rand der Seite, um sie zu verbreitern. Jeder Bereich lässt sich weiter einzeln einklappen: ein eingeklappter ist eine schmale Leiste im Stapel, oder eine Leiste an der Seite, wenn beide eingeklappt sind. Der Assistenten-Reiter (Desktop) bleibt bei den Eigenschaften. Lapin merkt sich die Anordnung auf diesem Rechner.

![Browser und Eigenschaften links übereinander](shots/panel-layouts.webp)

## Hell und dunkel

Der Mond am rechten Ende des Menübands schaltet die Oberfläche dunkel (Graphit), die Sonne wieder hell (Papier). Im dunklen Thema liegen Grundrisse, Schnitte und Ansichten auf dem dunklen Hintergrund, ihre schwarzen Stifte in heller Tinte; Pläne bleiben Papier. Die kleinen Zeichnungen der Konfiguratoren (Grundriss, Schnitt und Ansicht einer Tür oder eines Fensters, eines Dachs, eines Balkons, eines Aufzugs, einer Halle, einer Treppe…) und die Vorschauen von Beschriftungen, Schriftfeldern und Materialien werden in beiden Themen auf einer hellen Papierkarte gezeichnet, sodass ihre Linien, Texte und Maße wie auf einer gedruckten Zeichnung lesbar sind.

## Mehrere Elemente auf einmal

Wählen Sie mehrere Elemente (Umschalt-Klick oder ein Rahmen), und das Eigenschaftenfenster zeigt das zuletzt gewählte, mit **N ausgewählt** neben seiner Klasse. Ein gemeinsamer Wert wird angezeigt; ein abweichender erscheint als *variiert*. Ändern Sie ein Feld, und jedes ausgewählte Element, das es hat, übernimmt den neuen Wert, in einem Schritt, den **Rückgängig** zurücknimmt: Höhe und Basisversatz von Wänden, der Typ jeder ausgewählten Wand, Tür oder jedes Trägers, die Brüstung von Fenstern, die Phasen jedes ausgewählten Elements gleich welcher Klasse, Layer und Farbe gezeichneter Linien, der Stil von Texten. Was einem Element allein gehört, bleibt seins: die Nummer eines Raums, die Länge einer Wand, der Platz einer Tür entlang ihrer Wand.

## Eigenschaften übertragen

**Ändern → Eigenschaften übertragen** (<kbd>MA</kbd>, oder `MATCHPROP` in der Befehlszeile wie in AutoCAD; in Revit heißt es Match Type) gibt anderen Elementen, was ein Element hat. Wählen Sie das Element, von dem kopiert wird, oder starten Sie den Befehl und klicken Sie es zuerst an; klicken Sie dann jedes Element an, das seine Eigenschaften übernehmen soll. Das Element unter dem Cursor leuchtet vor dem Klick auf, in Grundrissen, Schnitten und 3D. Jeder Klick ist ein Schritt, den **Rückgängig** zurücknimmt. **Auf die Auswahl anwenden** gibt sie allen anderen ausgewählten Elementen auf einmal. Drücken Sie <kbd>Enter</kbd> oder <kbd>Esc</kbd>, wenn Sie fertig sind.

Das Zahnrad in der Leiste der Befehlszeile listet auf, was übertragen wird, und Lapin merkt sich Ihre Wahl: **Typ** (an ein Element derselben Klasse), **Maße und Einstellungen** (Höhe, Versätze, Materialien, Schnittpriorität, der Stil eines Textes, Rahmenlage und Brüstung einer Tür oder eines Fensters; an eine andere Klasse nur die Felder, die sie auch hat), **Parameter** (Ihre eigenen; die eines Standardsatzes nur an dieselbe Klasse), **Phasen**, **Layer, Farbe und Schraffur** (gezeichnete Formen) und **Name** (anfangs aus). Wo ein Element steht, seine Form und seine GlobalId werden nie übertragen. Ein fixiertes Element übernimmt sie trotzdem: Fixieren hält ein Element an seinem Platz, nicht seine Einstellungen.

![Eigenschaften übertragen im Grundriss: die Wand, von der kopiert wird, die Wand unter dem Cursor hervorgehoben, und was übertragen wird](shots/match-properties.webp)

## Tastenkürzel

**Ansicht → Tastenkürzel…** (<kbd>KS</kbd>) listet jeden Befehl, den die Tastatur erreichen kann, nach Registerkarte. Jede Zeile zeigt die Tastenkürzel des Befehls.

![Die Tastenkürzel, gesucht nach „wall“](shots/shortcuts-dialog.webp)

- **Suchen** Sie nach dem Namen eines Befehls oder nach einer Taste.
- **+** fügt ein Tastenkürzel hinzu: Drücken Sie eine Kombination (<kbd>Strg</kbd> <kbd>⇧</kbd> <kbd>W</kbd>, <kbd>F4</kbd>), oder geben Sie bis zu drei Buchstaben ein und drücken Sie <kbd>Enter</kbd>. <kbd>Esc</kbd> bricht ab.
- Hat ein anderer Befehl das Tastenkürzel bereits, sagt Lapin, welcher. **Neu zuweisen** nimmt es diesem Befehl weg, **Beide behalten** überlässt es beiden (der erste in der Liste gewinnt), **Abbrechen** ändert nichts.
- **×** entfernt ein Tastenkürzel. Der Pfeil am Ende einer Zeile setzt diesen Befehl so zurück, wie ihn die Vorlage hat, und **Alles zurücksetzen** tut das für jeden Befehl.
- **Vorlage** wählt den Satz, von dem Sie ausgehen: **Revit-Stil** (die Voreinstellung: die üblichen Buchstaben von Revit, wo Lapin den Befehl hat) oder **Lapin klassisch** (Lapins eigene Buchstaben von früher).
- **Exportieren…** schreibt Ihre Tastenkürzel in eine Datei, und **Importieren…** liest eine ein, für einen anderen Computer oder einen Kollegen.

QuickInfos, die Menüs und die Befehlspalette (<kbd>Strg</kbd> <kbd>K</kbd>) zeigen die Tastenkürzel immer so, wie sie gerade sind.

### Buchstaben und die Befehlszeile

Buchstaben wie <kbd>WA</kbd> funktionieren, wenn kein Feld den Fokus hat und die Befehlszeile leer ist: Geben Sie sie über der Ansicht ein. Der Befehl wird sofort ausgeführt. Beginnt ein Befehl der Befehlszeile mit denselben Buchstaben (<kbd>DI</kbd> und <kbd>DIMADD</kbd>), wartet Lapin einen Moment: Tippen Sie weiter, um den längeren Befehl zu schreiben, oder drücken Sie <kbd>Enter</kbd> oder <kbd>Leertaste</kbd>, um den kürzeren sofort auszuführen. Während ein Befehl nach einem Punkt oder einem Wert fragt, gehen Buchstaben und Ziffern an ihn. Befehle der Befehlszeile wie <kbd>L</kbd>, <kbd>M</kbd> oder <kbd>TRIM</kbd>, gefolgt von <kbd>Enter</kbd>, funktionieren in jeder Vorlage. <kbd>Esc</kbd> bricht immer ab.

Wenn sich die beiden in die Quere kommen, wählen Sie, wie sie sich die Tastatur teilen:

- **Buchstabenkürzel (wie in Revit)**, in diesem Dialog und unter **Einstellungen → Allgemein → Tastatur**, ist standardmäßig an. Schalten Sie es aus, bleibt nur die Befehlszeile: Jeder Buchstabe geht dorthin, und <kbd>Enter</kbd> oder <kbd>Leertaste</kbd> führt einen Befehl, einen Ihrer Aliase oder, wenn der Text keines von beiden ist, den Befehl mit diesen Buchstaben aus (<kbd>WA</kbd> <kbd>Enter</kbd> startet weiterhin die Wand). Tastenkombinationen (<kbd>Strg</kbd> + Taste, Funktionstasten, <kbd>Entf</kbd>) und die Zifferntasten der Schnellwerkzeuge funktionieren weiter.
- Die Taste <kbd>`</kbd> (links von <kbd>1</kbd>, an ihrem Platz erkannt, auf jedem Layout, und ein Kürzel wie die anderen: **Befehlszeile: alle Eingaben übernehmen**) gibt der Befehlszeile alle Eingaben, ob die Buchstaben an oder aus sind. Die Zeile wird umrandet und sagt es; kein Buchstabe startet ein Werkzeug, keine Ziffer wählt ein Schnellwerkzeug. <kbd>Enter</kbd> führt das Getippte aus und gibt die Eingabe zurück, außer ein Befehl fragt danach nach einem Punkt oder Wert: Dann bleibt der Modus, bis der Befehl endet. <kbd>Esc</kbd> oder dieselbe Taste verlässt ihn.

### Befehlsaliase

Die Seite **Befehlsaliase** des Dialogs enthält eigene Namen für Befehle. Tippen Sie 1 bis 12 Buchstaben oder Ziffern (der erste ein Buchstabe), suchen Sie den Befehl in der Liste (einen Befehl der Befehlszeile wie <kbd>LINE</kbd> oder irgendeinen Befehl des Menübands oder der App) und drücken Sie **Alias hinzufügen**. Tippen Sie den Alias in die Befehlszeile und drücken Sie <kbd>Enter</kbd> oder <kbd>Leertaste</kbd>. Ihre gehen den eingebauten Namen vor. Die Seite sagt, was ein Alias ersetzt: einen eingebauten Namen der Befehlszeile, ein Kürzel mit denselben Buchstaben oder ein Kürzel, das er warten lässt (bei eingeschalteten Buchstaben wartet ein Kürzel, das ein Alias oder der Anfang eines Alias ist, einen Moment, damit Sie weitertippen und <kbd>Enter</kbd> drücken können). Aliase werden mit Ihren Kürzeln gespeichert, auch in **Exportieren…** und **Importieren…**.

![Befehlsaliase, mit dem, was ein Alias überdeckt](shots/shortcuts-aliases.webp)

### Die Standard-Tastenkürzel

Dies sind die Tastenkürzel im Revit-Stil, wie Lapin sie jetzt hat:

<!-- shortcuts -->

Die Zifferntasten <kbd>1</kbd>–<kbd>9</kbd> wählen die Schnellwerkzeuge.

## Einen Dialog verschieben

Jeder Dialog lässt sich an seiner Titelleiste verschieben wie ein Fenster: auf den Titel drücken und ziehen, mit der Maus oder dem Finger. Er bleibt im Fenster, öffnet sich wieder dort, wo man ihn gelassen hat, solange das Programm offen ist, und ein Doppelklick auf den Titel setzt ihn zurück in die Mitte. So lässt er sich vom bearbeiteten Teil des Modells wegschieben, oder man sieht das Modell dahinter.
