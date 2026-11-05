---
title: Dokumente
order: 105
summary: Die schriftlichen Teile des Projekts — der Erläuterungsbericht, die Leistungsbeschreibung, das Planverzeichnis — aus Vorlagen erstellt, aus dem Modell befüllt und ohne weiteres Programm als DOCX, ODT und PDF exportiert.
---

## Was ein Dokument ist

Ein Projekt besteht nicht nur aus Plänen: Es hat auch seine schriftlichen Teile, einen Erläuterungsbericht, eine Leistungsbeschreibung, ein Planverzeichnis. In Lapin sind das **Dokumente**: Jedes beginnt mit einer Vorlage und wird aus dem Projekt befüllt — seinen Angaben, seinen Flächen, seinen Geschossen und Räumen, seinen Wandtypen, seinen Listen und seinen Plänen. Was nur Sie wissen (das Tragwerk, den Brandschutz, die Baustelleneinrichtung), schreiben Sie in die Eingaben des Dokuments. Ein Dokument folgt dem Modell: Ändern Sie einen Wandtyp oder fügen Sie einen Raum hinzu, und das Dokument sagt es beim nächsten Öffnen.

Lapin schreibt die Dateien selbst, als DOCX, ODT und PDF, ohne Word oder LibreOffice auf dem Computer, in der Desktop-App und im Browser.

## Projektinformationen

**Verwalten → Projektinformationen…** enthält, was die Dokumente vom Projekt brauchen: Name, Nummer und Bauphase, den Bauherrn und seine Adresse, die Adresse des Grundstücks, die Flurstücksnummer und das Grundbuch, den Planer und seine Adresse, den leitenden Architekten, wer gezeichnet und wer geprüft hat. Schreiben Sie sie einmal: Jedes Dokument liest sie, und die Schriftfelder der Pläne nehmen von hier, wer gezeichnet und geprüft hat, wenn ein Plan es nicht selbst angibt. Jedes Feld wird gespeichert, wenn Sie es verlassen.

![Projektinformationen: Nummer, Bauphase, Bauherr, Grundstück und Planer](shots/project-info.webp)

Dieselben Felder stehen im Reiter **Projektinformationen** des Dialogs Dokumente.

## Ein Dokument erstellen

**Verwalten → Dokumente…** (oder das **+** neben **Dokumente** im Projektbrowser) öffnet die Dokumente des Projekts. Links stehen die bereits erstellten Dokumente und unter **Neu aus Vorlage** die Vorlagen; ein Klick auf eine erstellt das neue Dokument und befüllt es sofort.

![Dokumente: die Dokumente des Projekts und die Vorlagen, das Dokument auf seinen Seiten, seine Eingaben und was noch zu ergänzen ist](shots/documents.webp)

In der Mitte steht das Dokument so, wie es gedruckt wird, Seite für Seite, mit Inhaltsverzeichnis, Tabellen und Zahlen. Ein Feld, das das Projekt noch nicht hat, ist dort markiert, wo es steht. Der Projektbrowser listet die Dokumente unter **Dokumente**; ein Klick öffnet den Dialog auf einem davon.

## Eingaben

Der Reiter **Eingaben** enthält die Texte, die nur der Planer schreiben kann, gruppiert wie die Kapitel des Dokuments. In den rumänischen Vorlagen beginnt jeder mit einem Hinweis, markiert mit **[De completat]** (zu ergänzen), der sagt, was hier hingehört. Überschreiben Sie ihn: Die Vorschau folgt einen Moment, nachdem Sie aufhören zu tippen. **Auf Standard zurücksetzen** holt den Text der Vorlage zurück.

![Eingaben: das Tragwerk über den Hinweis der Vorlage geschrieben](shots/documents-inputs.webp)

## Zu ergänzen

**Zu ergänzen** listet, was dem Dokument noch fehlt: ein leeres Projektfeld, ein Wert, den das Modell nicht hat. Ein Klick zeigt, wo es im Dokument steht, und führt zu dem Feld, das es füllt (ein Projektfeld öffnet den Reiter **Projektinformationen**). Die Liste leert sich, während Sie sie abarbeiten.

![Zu ergänzen: die Felder, die dem Dokument noch fehlen](shots/documents-missing.webp)

## Aktualisieren, umbenennen, kopieren

- **Aus Vorlage aktualisieren** übernimmt Text und Aufbau der Vorlage erneut, für ein Dokument, das vor einer Änderung der Vorlage erstellt wurde. Ihre Eingaben bleiben erhalten.
- **Umbenennen**, **Duplizieren** und **Löschen** wirken auf das angezeigte Dokument. Ein Löschen ist ein Rückgängig-Schritt.

## Exportieren

- **DOCX exportieren** und **ODT exportieren** schreiben eine Datei, die Sie in Word oder LibreOffice öffnen und bearbeiten können, mit den Überschriften als eigene Formatvorlagen des Programms, Inhaltsverzeichnis und Seitenzahlen als Felder und den Bildern des Projekts.
- **PDF exportieren** schreibt das von Lapin gesetzte Dokument, bereit zum Drucken oder Versenden.

Ein noch leeres Feld bleibt in den Dateien leer; prüfen Sie also **Zu ergänzen** vor dem Export.

## Dokumente ausgeben

**Verwalten → Ausgeben…** nimmt die Dokumente mit den Plänen und dem Modell mit (siehe [Projektordner und Ausgaben](cde.md)). Unter **Dokumente, jedes als PDF:** ist jedes Dokument zunächst angehakt; haken Sie **Auch als DOCX** an, um zusätzlich eine bearbeitbare Kopie mitzugeben. Jede Datei wird nach der Konvention von ISO 19650 benannt (`RP` für einen Bericht, `SP` für eine Leistungsbeschreibung), gehasht und im Übergabeschein aufgeführt, und das Dokument übernimmt Revision und Status der Ausgabe.

![Ausgeben: die Dokumente mit den Plänen und dem Modell](shots/issue-documents.webp)

## Die Vorlagen

Lapin bringt drei Vorlagen mit, vorerst auf Rumänisch:

- **Memoriu tehnic de arhitectură** (der Erläuterungsbericht Architektur, nach dem rumänischen Gesetz 50/1991): die allgemeinen Angaben aus den Projektinformationen; die Geschossigkeit (zum Beispiel `P+1E`) und die größte Höhe; die Flächen — bebaute Fläche, Bruttogeschossfläche, Nutzfläche und Kellerfläche — insgesamt und je Geschoss; die Räume jedes Geschosses mit ihren Flächen; die Wände nach Typ mit ihren Schichten; die Decken und Dächer; die Tür- und Fensterliste; die Raumoberflächen; Eingaben für den Bestand, die Nutzung, das Tragwerk, die Fassaden, die Haustechnik, den Brandschutz, die Barrierefreiheit, die Energieeffizienz und die Baustelleneinrichtung.
- **Caiet de sarcini: tâmplărie și finisaje** (die Leistungsbeschreibung für Fenster, Türen und Oberflächen): die Materialien des Projekts nach Kategorie, die Tür- und Fensterliste mit Stückzahlen, die Oberflächen Raum für Raum und Eingaben für Allgemeines, Anforderungen, Ausführung und Abnahme, mit Standardtexten zum Anpassen.
- **Borderou de piese scrise și desenate** (das Verzeichnis der schriftlichen und gezeichneten Teile): die Dokumente und Pläne des Projekts mit Nummer, Titel, Maßstab und Revision.

Ein visueller Editor für eigene Vorlagen folgt später.
