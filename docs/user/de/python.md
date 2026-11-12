---
title: Python-Skripte
order: 127
summary: Lapin mit Python aus dem Script-Fenster steuern, einem Entwickler-Extra: eine Konsole und ein Editor, die dein eigenes Python ausführen, jede Ausführung ein einziger Rückgängig-Schritt.
---

## Entwickler-Extras einschalten

Lapin lässt sich mit Python steuern: jedes Werkzeug ist ein Befehl, und ein Skript sendet dieselben Befehle. Das **Script**-Fenster ist ein *Entwickler-Extra*, ausgeschaltet, bis du es einschaltest; sonst ist nichts davon zu sehen.

Öffne **Datei → Einstellungen…** und setze unter **Allgemein** das Häkchen bei **Entwickler-Extras**.

![Einstellungen: Entwickler-Extras](shots/python-preferences.webp)

- **Python**: das Python, das das Fenster ausführt. Leer sucht Lapin eines auf diesem Computer (`python3`, `py -3` unter Windows, das von python.org und das von Homebrew). Es muss **Python 3.11 oder neuer** sein; Lapin installiert kein Python. Hol es dir von [python.org](https://www.python.org/downloads/), wenn du keines hast, oder gib den Pfad des gewünschten an (etwa das einer virtuellen Umgebung).
- **Externer Editor**: der Editor, den **Im externen Editor öffnen** verwendet, als Befehl (`code` für Visual Studio Code). Leer der Texteditor des Systems.

Python läuft nur in der Desktop-App: in Lapin für den Browser sagt das Fenster das.

## Das Script-Fenster

Öffne **Ansicht → Script**. Das Fenster öffnet sich am unteren Rand des Programmfensters; zieh an seinem oberen Rand, um es höher zu machen. Seine Kopfzeile sagt, welches Python es gefunden hat oder warum keines.

![Das Script-Fenster nach einer Ausführung: vier Wände, ein einziger Rückgängig-Schritt](shots/python-panel.webp)

- **Meine**, links: deine eigenen Skripte, gewöhnliche `.py`-Dateien in Lapins Ordner auf diesem Computer. Klick auf eines, um es zu öffnen; **Neues Skript** beginnt mit einem Beispiel.
- **Der Editor**, in der Mitte: Python mit seinen Farben und Einrückungen. Tippe `lapin.`, und er bietet an, was Lapin hat (die Befehle aller Module deiner Edition und seine eigenen Aufrufe); `lapin.architecture.` bietet die Architekturbefehle mit ihren Argumenten. Gib dem Skript einen Namen und **speichere** es in Meine; **Im externen Editor öffnen** öffnet seine Datei in deinem Editor, und was du dort speicherst, kommt ins Fenster zurück.
- **Die Konsole**, rechts: was Skripte ausgeben, ihre Fehler mit der Zeile, an der sie angehalten haben (im Editor markiert), und eine Zeile, in die du Python tippst. **Enter** führt sie aus, **Umschalt+Enter** beginnt eine neue Zeile, **Pfeil hoch** und **Pfeil runter** holen frühere Zeilen zurück. Ein Name, den du in der Konsole setzt, bleibt für die nächste Zeile und für die Skripte, die du ausführst.

Im Fenster ist `lapin` das bereits geladene Paket `lapinbim`. Dasselbe Skript läuft aus einem Terminal mit `import lapinbim as lapin`, solange Lapin geöffnet ist.

```python
storey = next(s.id for s in lapin.model.storeys if s.elevation == 0)
corners = [(0, 0), (8, 0), (8, 6), (0, 6)]
for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
    lapin.architecture.add_wall(storey_id=storey, x1=x1, y1=y1, x2=x2, y2=y2, height=2.8)
print(len(lapin.model.walls), "walls")
```

Längen sind in Metern und Winkel in Grad, gleich in welcher Einheit das Projekt sie zeigt.

## Ausführen, Stoppen, Neu starten, Rückgängig

- **Ausführen** (**Strg+Enter** im Editor) führt das Skript aus. Alles, was es tut, ist **ein einziger Rückgängig-Schritt**, nach dem Skript benannt: ein einziges **Rückgängig** nimmt alles zurück.

  ![Ein einziges Rückgängig hat die vier Wände der Ausführung zurückgenommen](shots/python-undo.webp)

- Während ein Skript läuft, gehört das Modell dem Skript: ein Werkzeug, das du im Fenster benutzt, wird mit *A script is editing the model* abgelehnt; Ansichten, Auswahl und Listen funktionieren weiter.
- Ein Skript, das scheitert, behält **nichts** von dem, was es getan hat: das Modell ist wie vor **Ausführen**, und die Konsole zeigt den Fehler und seine Zeile.
- **Stoppen** hält das Skript an; nichts, was es getan hat, bleibt erhalten. Bei einem Skript, das nicht innerhalb von zwei Sekunden anhält, wird sein Python beendet.
- **Neu starten** startet Python neu: die Namen, die die Konsole behalten hat, werden vergessen.

## Aufzeichnen, was du tust, als Skript

Der Reiter **Rekorder** neben der Konsole schreibt, was du im Fenster tust, als Python, eine Zeile pro Schritt.

1. Klicke auf **Aufnehmen**. Ein roter Punkt auf dem Reiter zeigt, dass aufgenommen wird.
2. Arbeite wie gewohnt: Wände zeichnen, eine Eigenschaft ändern, etwas löschen. Jeder Schritt erscheint sofort als Zeile, zum Beispiel `wall1 = lapin.architecture.add_wall(storey_id=storey1, x1=0, y1=0, x2=5, y2=0)`.
3. Klicke auf **Stopp** und dann auf **Kopieren** (um das Skript anderswo einzufügen) oder auf **Als Skript speichern** (es kommt zu Meine und öffnet sich im Editor).

![Der Rekorder nach dem Zeichnen zweier Wände](shots/python-recorder.webp)

Das Skript wird so geschrieben, dass es auch in einem anderen Projekt läuft:
- was ein Schritt erzeugt hat, wird in einer Variablen gehalten (`wall1`), und spätere Schritte benutzen sie (`wall1.id`);
- das Geschoss, auf dem du gezeichnet hast, wird über seinen Namen gefunden, sonst über seine Höhe (`lapin.model.storey("Ground floor", 0)`), ein Typ über seinen Namen (`lapin.model.find("IfcWallType", "Brick 250")`);
- ein anderes Element, das schon im Projekt war, wird mit seiner ID benannt (`lapin.model["…"]`), diese Zeile funktioniert also nur in diesem Projekt;
- ist **Auswahl als lapin.selection()** angehakt, wird ein Befehl auf das, was du ausgewählt hattest, als `lapin.selection()` geschrieben: das Skript arbeitet dann auf dem, was beim Ausführen ausgewählt ist.

Ein **Rückgängig** während der Aufnahme nimmt den letzten Schritt aus dem Skript, und **Wiederholen** setzt ihn wieder ein, sodass das Skript enthält, was du am Ende getan hast. Was ein Skript tut, wird nie aufgenommen, ebenso wenig Abfragen, die nichts ändern (eine gelesene Liste, eine Messung).

![Das aufgenommene Skript in einem neuen Projekt ausgeführt: dieselben zwei Wände](shots/python-recorder-new.webp)

## Die Skripte des Projekts

Ein Skript kann **im Projekt** aufbewahrt werden, sodass es mit der Datei wandert: öffne oder schreibe es im Editor und klicke auf **Im Projekt speichern**. Es erscheint links unter **Des Projekts**. Speichern, Umbenennen und Löschen eines Projektskripts sind jeweils ein Rückgängig-Schritt, und die Versionen des Projekts bewahren seine Skripte. In der `.lapin`-Datei ist jedes außerdem eine einfache Textdatei, `scripts/<name>.py`, sodass es ohne Lapin gelesen werden kann.

**Ein Projektskript läuft nie von selbst**, weder beim Öffnen des Projekts noch beim Speichern. Beim ersten Ausführen und danach jedes Mal, wenn sich sein Code geändert hat, fragt Lapin:
- **Einmal ausführen**: es läuft dieses Mal, und beim nächsten Ausführen wird erneut gefragt;
- **Vertrauen**: es läuft, und dieser Code wird auf diesem Computer als vertrauenswürdig gemerkt (eine Änderung daran fragt erneut);
- **Abbrechen**: nichts läuft.

Ein Skript, das du selbst im Projekt speicherst, gilt ab dem Speichern als vertrauenswürdig. Deine eigenen Skripte (Meine) fragen nie.

## Was ausgewählt ist

`lapin.selection()` liefert, was im Fenster ausgewählt ist, als Elemente:

```python
for e in lapin.selection():
    print(e.name, e.type)
```

Die Felder eines Elements werden über ihre Namen gelesen und durch Zuweisung geändert: `wall.height = 3` ändert die Wand (ein Rückgängig-Schritt oder Teil des Ausführens). `with lapin.step("Name"):` macht mehrere Befehle zu einem Rückgängig-Schritt, wobei jeder weiterhin sofort antwortet, sodass ein späterer nutzen kann, was ein früherer erzeugt hat.

## Ein Skript als Werkzeug

Ein Skript kann ein **Werkzeug** sein: eine Schaltfläche in **Meine Werkzeuge**, die es ausführt, für jeden, mit Entwickler-Extras an oder aus. Sag es in den ersten Zeilen des Skripts, seinem *Header*:

```python
# lapin: name="Perimeter walls", version="1.0.0", tool="Perimeter walls", icon="square", run="perimeter"
# lapin: tooltip="Four walls round a rectangle, one undo step"

def perimeter():
    v = lapin.ui.ask("Perimeter walls", {
        "width": lapin.ui.number(8.0, unit="m", min=0.5),
        "depth": lapin.ui.number(6.0, unit="m", min=0.5),
        "storey": lapin.ui.storey(),
    })
    if v is None:          # Abbrechen
        return
    w, d = v["width"], v["depth"]
    corners = [(0, 0), (w, 0), (w, d), (0, d)]
    for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
        lapin.architecture.add_wall(storey_id=v["storey"].id, x1=x1, y1=y1, x2=x2, y2=y2)
```

- **tool**: der Name der Schaltfläche; **run**: die Funktion, die sie aufruft (ohne sie läuft das ganze Skript); **icon**: eines der Lapin-Symbole (`wall`, `door`, `stair`…) oder ein gängiges (`square`, `ruler`, `layers`, `grid`, `star`…), sonst ein Buchstabe (`icon="P"`, oder der erste des Namens); **tooltip**: was die Schaltfläche sagt, wenn du mit der Maus darüber fährst.
- Speichere das Skript unter **Meine** (oder im Projekt): sein Werkzeug erscheint in **Meine Werkzeuge**, in einer Gruppe **Skripte**, ohne dass das Skript ausgeführt wird. Verschiebe es, mach es klein oder nimm es heraus wie jedes Werkzeug; auch **Befehle…** (Strg+K) findet es.

![Meine Werkzeuge mit dem Werkzeug eines Skripts](shots/python-tools-tab.webp)

Ein Klick führt das Skript als eine einzige **Ausführung** aus: alles, was es tut, ist **ein einziger Rückgängig-Schritt**, nach dem Werkzeug benannt. `lapin.ui.ask` zeigt einen Dialog von Lapin selbst, gebaut aus den Feldern, die das Skript nennt: eine Zahl mit ihrer Einheit, ein Text, eine Auswahl, ein Kontrollkästchen, ein Element (das du währenddessen im Modell auswählst: **Auswahl verwenden**), ein Geschoss, ein Typ. **OK** gibt dem Skript die Werte; **Abbrechen** gibt ihm nichts.

![Die Frage des Werkzeugs: die Maße und das Geschoss](shots/python-tools-ask.webp)

![Die vier Wände des Werkzeugs; ein Rückgängig nimmt sie zurück](shots/python-tools-walls.webp)

Ein Skript kann auch während des Laufs Werkzeuge anlegen: `@lapin.ui.tool("Name", icon="ruler")` über einer Funktion, einmal aus dem Fenster ausgeführt, legt dieses Werkzeug in Meine Werkzeuge.

Woher Werkzeuge kommen:
- **Meine**: immer;
- **die Skripte des Projekts**: nur wenn du ihrem Code auf diesem Computer vertraut hast, sodass ein Projekt von jemand anderem nie eine Schaltfläche in dein Menüband legt; **Vertrauen entziehen** im Fenster nimmt sie heraus;
- **Lapin Extensions**: ein Skript, das du installierst, zeigt sein Werkzeug; der erste Klick fragt **Einmal ausführen / Vertrauen / Abbrechen**, und **Skript ausführen** unter **Erweiterungen → Installiert** führt es ebenfalls aus.

Ein gelöschtes, deinstalliertes oder nicht mehr vertrautes Skript nimmt seine Werkzeuge aus Meine Werkzeuge.

## Sicherheit

Ein Skript kann alles, was du auf diesem Computer kannst: es läuft in deinem eigenen Python mit deinen Rechten. Führe Skripte aus, die du selbst geschrieben hast oder denen du vertraust; ein Projektskript und ein installiertes fragen zuerst.

Ein Gast einer geteilten Sitzung kann weder Skripte auf deinem Computer ausführen noch eines in dein Projekt legen noch ein Skript des Projekts löschen, umbenennen oder verschieben; ein Skript verbindet sich nur mit dem Lapin auf seinem eigenen Computer, und seine Fragen werden nur in deinem Fenster gestellt.

## Für Entwickler

Was `lapin` bietet (Befehle, Abfragen, Stapel, das Modell, Ereignisse), beschreibt der Entwicklerleitfaden *Lapin from Python* (`docs/python.md` im Repository von Lapin); das Paket ist auf PyPI als `lapinbim`.
