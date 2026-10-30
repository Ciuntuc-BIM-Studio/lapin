---
title: Der Modell-Assistent
order: 127
summary: Fragen Sie ein lokales KI-Modell zu Ihrem Projekt, erhalten Sie kurze Berichte und lassen Sie es Eigenschaftswerte vorschlagen, die Sie freigeben. Nur in der Desktop-App.
---

Der **Assistent** beantwortet Fragen zum geöffneten Projekt in Ihren eigenen Worten: wie viele Wände jedes Geschoss hat und wie lang sie sind, welche Türen breiter als 1 m sind, welche noch keine Feuerwiderstandsklasse haben. Er erstellt kurze Berichte als Tabellen, die Sie exportieren können, und er kann Eigenschaftswerte **vorschlagen** (eine Feuerwiderstandsklasse, einen Status, Ihre eigenen Parameter), die Sie prüfen und anwenden.

Er führt ein Sprachmodell **auf Ihrem Computer** aus, über [Ollama](https://ollama.com). Nichts von Ihrem Projekt verlässt den Rechner, und nichts wird an uns oder an irgendjemand anderen gesendet.

> Der Assistent ist nur in der **Desktop-App** verfügbar. Die Browserversion von Lapin hat keinen Assistenten.

## Was er kann und was nicht

Er **kann**:

- das Projekt lesen: Geschosse, Elemente, ihre Typen, Materialien, Eigenschaftssätze und Mengen sowie die Bauteillisten;
- zählen, summieren und mitteln (Längen, Flächen, Volumen, jede Menge), gruppiert nach Geschoss, Typ oder einer beliebigen Eigenschaft. Lapin berechnet die Zahlen; das Modell gibt sie nur wieder;
- Eigenschaftswerte vorschlagen, in den Standardsätzen von IFC (`Pset_DoorCommon.FireRating`, `Pset_WallCommon.IsExternal`…) oder in Ihren eigenen.

Er **kann nicht**:

- etwas zeichnen, verschieben, umformen, erstellen oder löschen. Er ändert die Geometrie nicht;
- Befehle ausführen oder Code schreiben;
- etwas von selbst anwenden. Jeder Wert, den er vorschlägt, wartet auf Sie.

Ein lokales Modell kann eine Frage missverstehen. Lesen Sie seine Antwort, und prüfen Sie die vorgeschlagenen Werte, bevor Sie sie anwenden.

## Einrichten

1. Installieren Sie Ollama von [ollama.com](https://ollama.com) und starten Sie es (die Ollama-App oder `ollama serve` in einem Terminal).
2. Installieren Sie ein Modell. Dasjenige, das wir am besten getestet haben, ist **qwen3-coder:30b** (schnell und zuverlässig mit den Werkzeugen von Lapin, braucht etwa 20 GB Arbeitsspeicher):

   ```sh
   ollama pull qwen3-coder:30b
   ```

   Auf einem Computer mit weniger Arbeitsspeicher funktioniert auch **gemma4:12b**, langsamer.
3. Öffnen Sie in Lapin den **Assistenten**: die Registerkarte **Assistent** neben **Eigenschaften** rechts, **Ansicht → Assistent**, oder <kbd>Strg</kbd>+<kbd>K</kbd> und geben Sie *Assistent* ein.

Lapin findet Ollama unter `http://127.0.0.1:11434` und wählt das beste Modell, das Sie haben. Die Zahnrad-Schaltfläche oben im Bereich zeigt die **Ollama-Adresse**, das zu verwendende **Modell** und ob Ollama läuft. Nur ein Ollama auf diesem Computer wird akzeptiert.

## Fragen

Geben Sie eine Frage ein und drücken Sie <kbd>Enter</kbd> (<kbd>Umschalt</kbd>+<kbd>Enter</kbd> für eine neue Zeile). Fragen Sie auf Englisch, Rumänisch oder in einer anderen Sprache: Die Antwort kommt in derselben.

![Der Assistent: ein Bericht nach Geschoss und eine für eine Tür vorgeschlagene Feuerwiderstandsklasse, die auf Freigabe wartet](shots/assistant.webp)

Während er arbeitet, zeigt der Bereich jeden Schritt in einer Zeile, zum Beispiel *12 von 12 aufgelistet · IfcDoor* oder *2 Gruppe(n) über 40 Element(e) berechnet*. Fahren Sie mit der Maus über einen Schritt, um genau zu sehen, was er abgefragt hat. **Stopp** beendet die Antwort; **Neuer Chat** fängt von vorne an.

Listen und Summen erscheinen außerdem als **Tabellen** unter der Antwort. Die Download-Schaltfläche an einer Tabelle speichert sie als CSV-Datei, für eine Tabellenkalkulation.

Einige Fragen für den Anfang:

- *Wie viele Wände gibt es pro Geschoss, und wie lang sind sie insgesamt?*
- *Liste die Fenster im ersten Obergeschoss mit ihrer Breite und Höhe auf.*
- *Welche Türen haben keine Feuerwiderstandsklasse?*

## Die vorgeschlagenen Werte freigeben

Fragen Sie zum Beispiel: *Türen breiter als 1 m: gib ihnen FireRating EI30.* Der Assistent findet die Türen und schlägt die Werte vor. Sie erscheinen unter **Vorgeschlagene Änderungen**: jedes Element, der Eigenschaftssatz und der Name, sein aktueller Wert und der neue, mit einem Kontrollkästchen.

- Entfernen Sie das Häkchen bei allem, was Sie nicht möchten; das Kästchen in der Kopfzeile setzt oder entfernt alle Häkchen.
- Klicken Sie auf den Namen eines Elements, um es in den Ansichten auszuwählen.
- **Anwenden** schreibt die angehakten Werte alle zusammen als **einen** Schritt: <kbd>Strg</kbd>+<kbd>Z</kbd> nimmt sie alle zurück.
- **Ablehnen** verwirft sie.

Die Werte gehen in die Parameter der Elemente, angezeigt im Bereich **Eigenschaften** mit ihrem Satz, und in den IFC-Export: Eine Standardeigenschaft kommt in ihren Satz (`Pset_DoorCommon`), Ihre eigenen kommen in einen eigenen Satz.

Die Regeln, denen der Assistent folgt (und die Lapin prüft): Ein Standard-IFC-Satz (beginnend mit `Pset_`) nimmt nur seine eigenen Eigenschaftsnamen auf, an den Elementen, für die er gedacht ist; ein eigener Satz beginnt nie mit `Pset_`.
