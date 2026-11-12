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

## Sicherheit

Ein Skript kann alles, was du auf diesem Computer kannst: es läuft in deinem eigenen Python, mit deinen Rechten. Führe Skripte aus, die du geschrieben hast oder denen du vertraust.

Ein Gast einer geteilten Sitzung kann keine Skripte auf deinem Computer ausführen, und ein Skript verbindet sich nur mit dem Lapin auf seinem eigenen Computer.

## Für Entwickler

Was `lapin` bietet (Befehle, Abfragen, Stapel, das Modell, Ereignisse), beschreibt der Entwicklerleitfaden *Lapin from Python* (`docs/python.md` im Repository von Lapin); das Paket ist auf PyPI als `lapinbim`.
