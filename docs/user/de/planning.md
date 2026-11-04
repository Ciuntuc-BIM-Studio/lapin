---
title: Terminplanung
order: 97
summary: Der Bauzeitenplan als Gantt-Diagramm: Vorgänge in einem PSP, Meilensteine, Verknüpfungen mit Zeitabständen, der kritische Pfad auf einem Kalender mit den gesetzlichen Feiertagen Rumäniens; zu und von MS Project, IFC und Excel.
---

## Der Reiter Terminplanung

![Der Reiter Terminplanung](shots/ribbon-planning.webp)

- **Gantt**: öffnet den Terminplan unter der Ansicht. Der Bildschirm teilt sich in zwei Zeilen: oben das Modell, unten der Terminplan.
- **Kalender…**: die Arbeitstage und -stunden, die Feiertage und die Winterpause (unten).
- **Vorgang** und **Meilenstein**: ein neuer Vorgang oder Meilenstein vor dem im Gantt gewählten Vorgang (oder am Ende).
- **Einrücken**, **Ausrücken**, **Vorgänge verknüpfen** und **Verknüpfungen lösen**: der PSP und die Verknüpfungen der gewählten Vorgänge.
- **MS Project importieren…**, **MS Project exportieren…**, **XLSX exportieren** und **IFC exportieren…**: der Terminplan zu und von anderen Programmen (unten).

Ein Projekt hat einen Terminplan. Er wird beim ersten Hinzufügen eines Vorgangs angelegt und beginnt am nächsten Montag. Den Beginn ändern Sie in der Leiste des Gantt.

## Das Gantt

![Das Gantt unter der 3D-Ansicht, ein Vorgang gewählt](shots/planning-gantt.webp)

Links steht die Vorgangstabelle: die Zeilennummer, der **PSP**-Code, der Name, die **Dauer** in Arbeitstagen, **Anfang** und **Ende**, die **Vorgänger** und der gesamte **Puffer**. Rechts ist jeder Vorgang ein Balken auf der Zeitskala:

- ein **Vorgang** ist ein blauer Balken, rot, wenn er auf dem kritischen Pfad liegt;
- ein **Sammelvorgang** (ein Vorgang mit Vorgängen darunter) ist eine dunkle Klammer vom Anfang seines ersten bis zum Ende seines letzten Vorgangs;
- ein **Meilenstein** ist eine Raute.

Arbeitsfreie Tage sind schattiert, und heute ist eine gestrichelte Linie. **Tage**, **Wochen**, **Monate** und **Quartale** ändern die Skala, ebenso <kbd>Strg</kbd> + das Mausrad. Die Schaltfläche mit den vier Ecken zeigt den ganzen Terminplan, und **Heute** springt zu heute. **Kritischer Pfad** schaltet das Rot ein und aus. Der Pfeil neben dem Namen eines Sammelvorgangs klappt die Vorgänge darunter zu und auf.

Das Gantt ist eine Ansicht wie die anderen: Es hat einen Reiter und lässt sich auf dem Desktop in einem eigenen Fenster für einen zweiten Bildschirm öffnen.

### Bearbeiten in der Tabelle

Doppelklicken Sie auf eine Zelle, um darin zu schreiben. <kbd>Enter</kbd> übernimmt den Wert, <kbd>Escape</kbd> verwirft ihn.

- **Dauer**: Arbeitstage; 0 macht den Vorgang zum Meilenstein.
- **Anfang**: der Vorgang beginnt nicht früher als an diesem Tag (eine Einschränkung, siehe unten).
- **Ende**: die Dauer wird zu den Arbeitstagen vom Anfang bis zu diesem Tag.
- **Vorgänger**: wie in MS Project Zeilennummern, durch Kommas getrennt, jede mit Typ und Zeitabstand: `3`, `5SS`, `7FF+2`, `2FS-1d`.

Die Tastatur funktioniert wie in einer Tabelle: <kbd>↑</kbd> und <kbd>↓</kbd> verschieben die Auswahl, <kbd>Insert</kbd> fügt einen Vorgang vor dem gewählten ein (<kbd>Shift</kbd>+<kbd>Insert</kbd> einen Meilenstein), <kbd>Delete</kbd> löscht die gewählten Vorgänge samt dem, was darunter liegt, <kbd>Tab</kbd> rückt ein und <kbd>Shift</kbd>+<kbd>Tab</kbd> rückt aus, <kbd>F2</kbd> benennt um.

### Bearbeiten im Diagramm

- **Einen Balken ziehen**, um den Vorgang zu verschieben. Er beginnt dann nicht früher als an dem Tag, auf dem Sie ihn ablegen. Das Datum ist beim Ziehen zu sehen.
- **Das Ende eines Balkens ziehen**, um seine Dauer zu ändern. Die Arbeitstage sind beim Ziehen zu sehen.
- **Zwei Vorgänge verknüpfen**: Fahren Sie über einen Balken und ziehen Sie vom kleinen Kreis an einem seiner Enden auf einen anderen Balken. Die verbundenen Enden ergeben den Typ: vom Ende zum Anfang des anderen ist Ende-Anfang (FS), Anfang zu Anfang SS, Ende zu Ende FF, Anfang zu Ende SF.
- **Auf einen Pfeil klicken**, um Typ und Zeitabstand der Verknüpfung zu ändern (in Arbeitstagen; negativ ist eine Überlappung) oder sie zu entfernen.

Jede Änderung ist ein einzelner Schritt zum Rückgängigmachen.

## Ein Vorgang in den Eigenschaften

![Ein Vorgang in den Eigenschaften](shots/planning-task.webp)

Wählen Sie einen Vorgang, und die Eigenschaften zeigen ihn: seinen Namen, seine IFC-Art, seine Dauer, ob er ein Meilenstein ist, seine Daten und:

- **Einschränkung**: **So früh wie möglich** (Standard), **Anfang nicht früher als** ein Datum oder **Muss anfangen am** ein Datum. Ein Vorgang, der beginnen muss, bevor seine Vorgänger enden, nimmt diesen Puffer weg. Ihr Puffer wird dann negativ, und sie werden kritisch.
- **Nassarbeiten**: Beton, Estriche, Putz. Ein nasser Vorgang arbeitet nicht in der Winterpause des Kalenders, wenn die Pause eingeschaltet ist.
- **Gesamtpuffer** und **Freier Puffer**: um wie viele Arbeitstage sich der Vorgang verschieben kann, ohne das Ende zu verschieben, bzw. ohne zu verschieben, was ihm folgt. **Spätester Anfang** und **Spätestes Ende** sind die spätesten Daten, die das Ende halten.
- **Vorgänger**: jede eingehende Verknüpfung, mit Typ und Zeitabstand zum Ändern oder zum Entfernen. **Nachfolger** listet auf, was dem Vorgang folgt.

## Der kritische Pfad

Lapin berechnet den Terminplan nach jeder Änderung. Eine Vorwärtsrechnung findet die frühesten Daten und eine Rückwärtsrechnung die spätesten. Die Vorgänge ohne Puffer bilden den kritischen Pfad, und eine Verzögerung eines davon verschiebt das Ende. Eine Verknüpfung zu einem Sammelvorgang hält alle Vorgänge darunter. Eine Verknüpfung von einem Sammelvorgang beginnt am Anfang seines ersten Vorgangs und endet am Ende seines letzten. Lapin lehnt eine Verknüpfung ab, die eine Schleife ergäbe.

## Der Kalender

![Der Arbeitskalender](shots/planning-calendar.webp)

**Terminplanung → Kalender…** legt fest:

- die **Arbeitstage** der Woche (standardmäßig Montag bis Freitag) und die **Stunden** eines Tages (8 ab 08:00);
- die **Feiertage**: die gesetzlichen Feiertage Rumäniens, für jedes Jahr berechnet (einschließlich des orthodoxen Karfreitags, Ostern und Pfingsten), oder keine. Die Liste darunter zeigt die Feiertage eines Jahres zur Kontrolle;
- die **Winterpause** für Nassarbeiten, standardmäßig aus: vom 15. Dezember bis zum 1. März, wenn Sie keine anderen Tage festlegen;
- **Ausnahmen**: freie Tage für das Projekt (eine gesperrte Baustelle) oder gearbeitete Tage, die sonst keine wären (ein Samstag).

Dauern und Zeitabstände zählen Arbeitstage dieses Kalenders.

## Den Terminplan austauschen

- **MS Project exportieren…** schreibt eine MS-Project-XML-Datei. MS Project, ProjectLibre und GanttProject öffnen sie, und Primavera P6 importiert sie. Sie enthält die Vorgänge mit PSP, Dauern, Daten, Einschränkungen und Puffern, die Verknüpfungen mit ihren Typen und Zeitabständen und den Kalender mit jedem Feiertag als Ausnahme. **MS Project importieren…** liest eine solche Datei anstelle des Terminplans des Projekts. Ein von Lapin geschriebener und wieder gelesener Terminplan behält die IDs und Daten seiner Vorgänge.
- **IFC exportieren…** (auch unter **Datei**) schreibt den Terminplan zusammen mit dem Modell in die IFC-Datei: einen IfcWorkSchedule, seine IfcTask mit ihren Zeiten, die IfcRelSequence-Verknüpfungen und den IfcWorkCalendar. Beim Importieren einer IFC-Datei mit Terminplan wird dieser ebenfalls übernommen.
- **XLSX exportieren** schreibt eine Arbeitsmappe: die Tabelle des Gantt und die freien Tage des Kalenders.

Lapins eigene Felder, für die andere Programme keinen Platz haben (die Einschränkung eines Vorgangs, ob er nass ist, die Winterpause), reisen in IFC als die Eigenschaftssätze `Lapin_Task` und `Lapin_Calendar`. MS Project kennt keine nassen Vorgänge, deshalb bleibt die Winterpause in Lapin.

## Was als Nächstes kommt

Die Verknüpfung der Modellelemente mit Vorgängen, ein aus dem Modell erzeugter Terminplan, die 4D-Simulation des in 3D wachsenden Gebäudes, Fortschritt und Basispläne sowie die Kostenschätzung folgen in den nächsten Stufen.
