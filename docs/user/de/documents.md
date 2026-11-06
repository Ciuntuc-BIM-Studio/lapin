---
title: Dokumente
order: 105
summary: Die schriftlichen Teile des Projekts — der Erläuterungsbericht, die Leistungsbeschreibung, das Planverzeichnis — aus Vorlagen erstellt, aus dem Modell befüllt und ohne weiteres Programm als DOCX, ODT und PDF exportiert.
---

## Was ein Dokument ist

Ein Projekt besteht nicht nur aus Plänen: Es hat auch seine schriftlichen Teile, einen Erläuterungsbericht, eine Leistungsbeschreibung, ein Planverzeichnis. In Lapin sind das **Dokumente**: Jedes beginnt mit einer Vorlage und wird aus dem Projekt befüllt — seinen Angaben, seinen Flächen, seinen Geschossen und Räumen, seinen Wandtypen, seinen Listen und seinen Plänen. Was nur Sie wissen (das Tragwerk, den Brandschutz, die Baustelleneinrichtung), schreiben Sie in die Eingaben des Dokuments. Ein Dokument folgt dem Modell: Ändern Sie einen Wandtyp oder fügen Sie einen Raum hinzu, und das Dokument sagt es beim nächsten Öffnen.

Lapin schreibt die Dateien selbst, als DOCX, ODT und PDF, ohne Word oder LibreOffice auf dem Computer, in der Desktop-App und im Browser.

## Projektinformationen

**Verwalten → Projektinformationen…** enthält, was die Dokumente vom Projekt brauchen: Name, Nummer und Bauphase, den Bauherrn und seine Adresse, die Adresse des Grundstücks, die Flurstücksnummer und das Grundbuch, den Planer und seine Adresse, den leitenden Architekten, wer gezeichnet und wer geprüft hat. Schreiben Sie sie einmal: Jedes Dokument liest sie, und die Planköpfe der Pläne nehmen von hier, wer gezeichnet und geprüft hat, wenn ein Plan es nicht selbst angibt. Jedes Feld wird gespeichert, wenn Sie es verlassen.

![Projektinformationen: Nummer, Bauphase, Bauherr, Grundstück und Planer](shots/project-info.webp)

Dieselben Felder stehen im Reiter **Projektinformationen** des Dialogs Dokumente.

Da ein Plankopf mehr fragt als ein Dokument, sind die Felder gruppiert: das Projekt, der Bauherr, das Grundstück und seine Baugenehmigung, der Planer (Büro, Adresse, Registernummer, Telefon, E-Mail) und die Unterzeichnenden (Projektleiter, Chefarchitekt, Tragwerks- und TGA-Planer, gezeichnet, geprüft und freigegeben von). Was noch fehlt, kommt unter **Eigene Parameter des Projekts**: ein eindeutiger Schlüssel, eine Bezeichnung, ein Typ (Text, Zahl oder Datum), eine Gruppe und ein Wert. Ein Plankopf liest ihn als `{project.schlüssel}`, ein Dokument als `{{project.schlüssel}}`, und IFC exportiert alles mit dem Projekt im Eigenschaftssatz `Lapin_ProjectInformation` (und liest es zurück).

![Eigene Parameter des Projekts: Schlüssel, Bezeichnung, Typ, Gruppe und Wert](shots/project-info-parameters.webp)

## Ein Dokument erstellen

**Verwalten → Dokumente…** (oder das **+** neben **Dokumente** im Projektbrowser) öffnet die Dokumente des Projekts. Links stehen die bereits erstellten Dokumente und unter **Neu aus Vorlage** die Vorlagen; ein Klick auf eine erstellt das neue Dokument und befüllt es sofort.

![Dokumente: die Dokumente des Projekts und die Vorlagen, das Dokument auf seinen Seiten, seine Eingaben und was noch zu ergänzen ist](shots/documents.webp)

In der Mitte steht das Dokument so, wie es gedruckt wird, Seite für Seite, mit Inhaltsverzeichnis, Tabellen und Zahlen. Ein Feld, das das Projekt noch nicht hat, ist dort markiert, wo es steht. Der Projektbrowser listet die Dokumente unter **Dokumente**; ein Klick öffnet den Dialog auf einem davon.

## Eingaben

Der Reiter **Eingaben** enthält die Texte, die nur der Planer schreiben kann, gruppiert wie die Kapitel des Dokuments. In den rumänischen Vorlagen beginnt jeder mit einem Hinweis, markiert mit **[De completat]** (zu ergänzen), der sagt, was hier hingehört. Überschreiben Sie ihn: Die Vorschau folgt einen Moment, nachdem Sie aufhören zu tippen. **Auf Standard zurücksetzen** holt den Text der Vorlage zurück.

![Eingaben: das Tragwerk über den Hinweis der Vorlage geschrieben](shots/documents-inputs.webp)

## Zu ergänzen

**Zu ergänzen** listet, was dem Dokument noch fehlt: einen leeren Projektfeld, ein Wert, den das Modell nicht hat. Ein Klick zeigt, wo es im Dokument steht, und führt zu dem Feld, das es füllt (ein Projektfeld öffnet den Reiter **Projektinformationen**). Die Liste leert sich, während Sie sie abarbeiten.

![Zu ergänzen: die Felder, die dem Dokument noch fehlen](shots/documents-missing.webp)

## Aktualisieren, umbenennen, kopieren

- **Aus Vorlage aktualisieren** übernimmt Text und Aufbau der Vorlage erneut, für ein Dokument, das vor einer Änderung der Vorlage erstellt wurde. Ihre Eingaben bleiben erhalten.
- **Umbenennen**, **Duplizieren** und **Löschen** wirken auf das angezeigte Dokument. Ein Löschen ist ein Rückgängig-Schritt.

## Ein Dokument bearbeiten

**Bearbeiten**, unten im Dialog Dokumente, macht die Seite in der Mitte zu einem Editor für den eigenen Text des Dokuments; **Vorschau** zeigt es wieder auf seinen Seiten. Was Sie ändern, gehört nur zu diesem Dokument: Die Vorlage bleibt, wie sie war (**Als Vorlage speichern…**, weiter unten, behält Ihre Änderungen als Vorlage).

![Ein Dokument bearbeiten: die Felder als Marken mit ihren Werten, eine Tabelle des Projekts ausgewählt und ihre Einstellungen rechts](shots/documents-editor.webp)

- **Die Werkzeugleiste** hat die Stile des Textes: **Absatz**, **Überschrift 1**–**3**, fett, kursiv, Aufzählung und nummerierte Liste, Rückgängig und Wiederholen. Die üblichen Tasten gehen auch (Strg+B, Strg+I, Strg+Z im Editor).
- **Felder** erscheinen als Marken mit ihrem Namen und ihrem jetzigen Wert, zum Beispiel der Bauherr, *Popescu Ion și Maria*. Ein Feld, das das Projekt noch nicht füllt, ist orange.
- **Die Blöcke des Projekts** — eine Tabelle, ein Bild, das Inhaltsverzeichnis — werden mit ihrem echten Inhalt gezeigt. Ein Klick wählt einen aus: Seine Einstellungen erscheinen rechts, anstelle der Reiter.

Gespeichert wird von selbst: einen Moment, nachdem Sie aufhören zu tippen, und wenn Sie zur **Vorschau** zurückgehst; jede Änderung ist ein Rückgängig-Schritt des Projekts.

### Einfügen

**Einfügen** setzt an den Cursor:

- **Feld…**: ein Feld des Projekts, nach seinem Namen gesucht; die Liste zeigt den Wert jedes Felds. In einer Wiederholung werden auch die Felder ihrer Elemente angeboten (der Name eines Geschosses, die Fläche eines Raums).

  ![Einfügen → Feld…: „client“ gesucht, die Felder mit ihren Werten](shots/documents-insert-field.webp)

- **Tabelle aus dem Projekt…**: eine Liste oder ein Verzeichnis des Projekts (die Fenster- und Türliste, die Räume, die Materialien…). In ihren Einstellungen: die **Quelle**, der **Titel**, die gezeigten Spalten und die Summenzeile.
- **Bild einer Ansicht…**: ein Grundriss, Schnitt, eine Ansicht oder eine 3D-Ansicht. In seinen Einstellungen: **Maßstab 1:** oder **Breite (mm)** (nie breiter als der Text) und eine **Bildunterschrift**.
- **Schlüssel–Wert-Felder**: eine zweispaltige Tabelle mit Bezeichnungen und Werten; ein Wert darf Felder enthalten, wie `{{project.name}}`.
- **Wiederholen für…**: Der Text darin wird einmal für jedes Element einer Sammlung geschrieben: die Geschosse, die Räume, die Dächer, die Materialien, die Pläne, die Dokumente… Ein Band darüber sagt **Für jedes** und wie viele Elemente es gibt; seine Pfeile **‹ ›** wechseln das Element, das der Editor beim Schreiben zeigt. In den Einstellungen behält **Nur die Elemente, wo** einige davon, und **Text, wenn es keine gibt** fügt hinzu, was geschrieben wird, wenn es keine gibt.
- **Bedingung…**: Der Text darin wird nur geschrieben, wenn ein Feld einen Wert hat (oder, angehakt, nur wenn es leer ist). **Sonst: Text, wenn sie nicht zutrifft** fügt einen zweiten Teil hinzu, der an seiner Stelle geschrieben wird.
- **Zu ergänzender Text…**: eine neue Eingabe mit Bezeichnung, Gruppe und Standardtext; ihren Text schreiben Sie im Reiter **Eingaben**, in der Vorschau.
- **Inhaltsverzeichnis** und **Seitenumbruch**.

![Eine Wiederholung über die Geschosse, eine Bedingung darin und die Räume des gezeigten Geschosses, gewechselt mit ‹ ›](shots/documents-repeat.webp)

In den Einstellungen eines Blocks entfernt **Entfernen** ihn; bei einer Wiederholung oder Bedingung nimmt **Nur den Inhalt behalten** das Band weg und behält, was darin steht.

## Eigene Vorlagen

**Als Vorlage speichern…** behält Text, Layout, Eingaben und Stil des Dokuments als Vorlage. Geben Sie ihr einen Titel und eine Beschreibung und wählen Sie, wo sie bleibt:

- **Dieses Projekt**: Sie geht mit der Projektdatei, zu jedem, der sie öffnet.
- **Die Bibliothek dieses Computers**: Jedes Projekt auf diesem Computer bietet sie an.
- **Beides**.

**Speichern als** kann eine früher gespeicherte Vorlage ersetzen; daraus erstellte Dokumente behalten ihren Text bis **Aus Vorlage aktualisieren**. Die neue Vorlage steht sofort unter **Neu aus Vorlage**.

![Als Vorlage speichern: Titel, Beschreibung und wo sie bleibt](shots/documents-save-template.webp)

**Verwalten → Dokumentvorlagen…** zeigt die Vorlagen in drei Gruppen: **Eingebaut** (Lapins), **Dieses Projekt** und **Dieser Computer**. Für die gewählte:

- **Umbenennen** und **Löschen** (das Löschen einer Projektvorlage ist ein Rückgängig-Schritt; daraus erstellte Dokumente behalten ihren Text).
- **Ins Projekt duplizieren** oder **In die Bibliothek duplizieren**: Eine eingebaute Vorlage wird nur über eine Kopie geändert.
- **In die Bibliothek kopieren** oder **Ins Projekt kopieren**, um sie von einem Ort zum anderen zu bringen.
- **Exportieren…** schreibt die Vorlage als `.json`-Datei; **Importieren…** liest eine in dieses Projekt oder in die Bibliothek dieses Computers. So teilen Kollegen die Vorlagen einer Firma.

![Dokumentvorlagen: die eingebauten, die des Projekts und die des Computers, die gewählte mit ihrem Stil](shots/document-templates.webp)

## Firmenstil

Ein Dokument, und eine Vorlage des Projekts oder der Bibliothek, hat einen eigenen Stil: der Reiter **Stil** im Dialog Dokumente für das gezeigte Dokument, der Teil **Stil** in Verwalten → Dokumentvorlagen… für eine Vorlage. Eingebaute Vorlagen behalten Lapins Aussehen: Duplizieren Sie eine, um ihr Ihres zu geben.

- **Schrift (DOCX, ODT)**: Arial (Lapins), Calibri, Times New Roman, Georgia, Verdana oder Liberation Sans.
- **PDF-Schrift**: Helvetica oder Times, die eigenen Schriften des PDF; standardmäßig die, die der Schrift oben am nächsten kommt.
- **Textgröße (pt)**: die Größe des Fließtexts, 6 bis 24 pt.
- **Farbe der Überschriften**.
- **Logo in der Kopfzeile**: **Logo wählen…** nimmt ein PNG oder JPEG (unter 1 MB); legen Sie seine **Breite (mm)** fest und ob es **Links** oder **Rechts** steht. Es wird in DOCX, ODT und PDF gleich geschrieben.

Ein Muster unter den Feldern zeigt den Kopf der Seite, wie die Dateien ihn zeigen werden. **Lapins eigenes Aussehen** nimmt den Stil weg. Jede Änderung ist ein Rückgängig-Schritt.

![Der Reiter Stil: die Farbe der Überschriften und ein Logo in der Kopfzeile, mit dem Muster](shots/documents-style.webp)

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

Gehen Sie von einer davon aus, ändern Sie sie im Editor und dann **Als Vorlage speichern…**, um Ihre eigene zu haben.

## Für KI-Assistenten

Ein über Lapins MCP-Brücke verbundener Assistent kann ein Dokument erstellen, seine Eingaben schreiben und es mit `lapin_document_export` exportieren: siehe [Lapin and AI agents](https://github.com/Ciuntuc-BIM-Studio/lapin-pro/blob/main/docs/mcp.md#documents) (auf Englisch).
