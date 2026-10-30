---
title: Projektordner und Ausgaben
order: 125
summary: Geben Sie Blätter und das Modell in einen Projektordner aus, wie ISO 19650 es verlangt, nach der Konvention benannt, mit Übergabeschein, nach den Prüfungen eines Zeichenbüros.
---

## Der Projektordner

**Manage → Project folder** legt den Ordner fest, in den das Projekt ausgegeben wird: ein Ordner auf Ihrem Rechner, eine Netzwerkfreigabe oder ein auf die Festplatte synchronisierter SharePoint- oder OneDrive-Ordner. Lapin verlangt kein Konto und spricht mit keinem Server: es schreibt Dateien, und was den Ordner synchronisiert, ist Sache Ihres Büros. Der Pfad wird im Projekt gespeichert und reist mit der Datei; ein Ordner, der nicht auf diesem Rechner ist, ist eine Warnung, kein Fehler.

![Projektordner: der Ordner, die Container, die Benennung](shots/project-folder.webp)

Im Ordner legt Lapin beim ersten Ausgeben die vier ISO-19650-Container an: **WIP**, **Shared**, **Published** und **Archived**. Der Statuscode schlägt den Container vor: S0 geht nach WIP, S1–S7 nach Shared, A, B und CR nach Published.

## Benennung

Dateien werden nach ISO 19650-2 benannt: **Projekt-Urheber-Volumen-Ebene-Typ-Rolle-Nummer**, dann der Status und die Revision, zum Beispiel `PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf`. Im Dialog Project folder setzen Sie den Projektcode, den Urheber (Ihr Büro), das Volumen sowie die Standard-Ebene und -Rolle. Der Rest kommt aus dem Projekt:

- die **Ebene** eines Blatts ist das Geschoss, das seine Grundrisse zeichnen (`00` bei ±0,00, `01` darüber, `B1` darunter, `ZZ` bei mehreren oder keinem);
- seine **Rolle** ist die seiner Disziplin (Architektur A, Tragwerk S, TGA M, Tiefbau C);
- seine **Nummer** sind die Ziffern der Blattnummer (`A-101` wird zu `0101`);
- das Modell ist `M3`, der Übergabeschein `IE`.

## Prüfungen

**Manage → Check** lässt über das ganze Projekt die Regeln laufen, die ein Zeichenbüro vor einer Ausgabe mit dem Auge prüft. Ein **blockierender** Befund stoppt die Ausgabe; eine **Warnung** kann übergangen werden. Ein Klick auf einen Befund führt zum Blatt, zur Ansicht oder zum Element.

![Prüfung: die Befunde nach Regel](shots/cde-check.webp)

Blockierend: ein Schriftfeld ohne Nummer, Titel oder Zeichner; eine Ansicht ohne Maßstab; eine Ansicht außerhalb des Rahmens oder über dem Schriftfeld; ein Blatt, das sich seit der letzten Ausgabe geändert hat, aber noch auf dieser Revision steht; eine Blattnummer, die keine Dateinummer ergibt. Warnungen: Prüfer, Datum und Phase leer; ein Grundriss, der nach einem Raumschema gefärbt ist oder viele Schraffuren hat, ohne Legende; ein Grundriss, Schnitt oder eine Ansicht auf keinem Blatt; ein Lageplan ohne Nordpfeil; Elemente ohne Geschoss, Typ oder Material; Türen und Fenster ohne Beschriftung; Räume ohne Nummer; die Pflicht-IFC-Eigenschaften (IsExternal, LoadBearing, FireRating bei Türen) leer.

## Ausgeben

**Manage → Issue…** schreibt eine Ausgabe in einen Container (nur in der Desktop-App, da Dateien geschrieben werden):

![Ausgabe: die Blätter, die Revision, der Status, die Prüfungen](shots/cde-issue.webp)

1. Geben Sie der Ausgabe einen Namen, ihre **Revision** (`P01`, `P02`… solange vorläufig, `C01`… sobald vertraglich; die nächste wird aus der letzten Ausgabe vorgeschlagen), ihren **Status** (`S0`–`S7`, `A1`–`A9`, `B1`–`B9`, `CR`) und den **Container**.
2. Haken Sie die Blätter an, die gehen, das Modell als IFC, jedes Blatt auch als DXF oder alle Blätter auch in einer PDF. Der Name jeder Datei wird so gezeigt, wie er geschrieben wird.
3. Die Prüfungen laufen, während Sie wählen. Mit einem blockierenden Befund kann die Ausgabe nicht gehen; nur mit Warnungen haken Sie **Trotzdem ausgeben** an.
4. **Ausgeben** schreibt jedes Blatt als PDF, das IFC und den Rest in den Container, nach der Konvention benannt, bildet den Hash (SHA-256) jeder Datei, schreibt den **Übergabeschein** (eine PDF und eine CSV: Projekt, Ausgabe, Datum, Revision, Status, die Dateien) und ein **Manifest** (JSON, mit allen Hashes) daneben, und trägt die Ausgabe im Projekt ein. Jedes gedruckte Blatt übernimmt die Revision und den Status der Ausgabe in sein Schriftfeld, und seine Revisionstabelle listet die Ausgaben, in denen es herausging.
5. Das Projekt wird danach als nach der Ausgabe benannte **Version** gespeichert (siehe [Dateien, Versionen und Wiederherstellung](files.md)), damit eine Ausgabe wieder geöffnet, wiederhergestellt oder mit dem jetzigen Projekt verglichen werden kann.

Die Ausgaben stehen unten im Dialog Project folder, jede mit ihren Dateien und **Ordner öffnen**.

## Anmerkungen (BCF)

**Manage → Anmerkungen am Modell…** hält die Anmerkungen am Modell – deine und die eines Prüfers – als BCF-Themen (das BIM Collaboration Format, das BIMcollab, Solibri, Navisworks und Revit austauschen), im Projekt, nie in der IFC. Eine Anmerkung hat einen Titel, eine Beschreibung, einen Typ, einen Status (Open, In progress, Resolved, Closed), eine Priorität, einen Zuständigen, ein Fälligkeitsdatum und Schlagwörter; ihre Kommentare; und ihre Blickpunkte – die 3D-Kamera, die ausgewählten Elemente, den Schnittkasten als Schnittebenen und einen Schnappschuss.

![Anmerkungen: die Liste mit ihren Filtern, eine Anmerkung mit Schnappschuss und Kommentaren](shots/remarks.webp)

- **Neue Anmerkung** macht eine aus der 3D-Ansicht vorne: die Kamera wie sie ist, die ausgewählten Elemente, der Schnittkasten und ein Bild aus der Ansicht. Felder ausfüllen und **Erstellen**.
- Die Liste wird nach einem Text, einem Status, einer Priorität, dem Zuständigen und **Nur offene** gefiltert. Wähle eine Anmerkung, um ihre Felder zu sehen und zu ändern, ihre Kommentare zu lesen und im Feld zu antworten (**Kommentieren**; **Mit der 3D-Ansicht** hängt die Ansicht wie sie ist an die Antwort).
- **In 3D zeigen** öffnet den Blickpunkt der Anmerkung: die 3D-Ansicht geht an ihre Kamera, ihre Elemente werden ausgewählt (die das Projekt hat, nach ihrer IFC-GlobalId), und ihre Schnittebenen werden der Schnittkasten. Ein Kommentar mit eigener Ansicht hat **Zeigen** daneben.
- **Aus der 3D-Ansicht neu aufnehmen** ersetzt den Blickpunkt der Anmerkung durch die Ansicht wie sie jetzt ist.
- **BCF importieren…** liest eine `.bcfzip`- (BCF 2.1) oder `.bcf`-Datei (BCF 3.0): ein Thema, das das Projekt schon hat (dieselbe GUID), wird aktualisiert – die Felder der Datei gewinnen, und hier gemachte Kommentare, die die Datei nicht hat, bleiben –, die anderen kommen hinzu. **Alle exportieren**, **Offene exportieren** oder **Ausgewählte exportieren** schreiben eine BCF-Datei, 2.1, sofern du nicht 3.0 wählst, zum Übergeben an den Prüfer oder zum Ablegen im Container Shared des Projektordners. Die Themen behalten ihre GUIDs, so kommt ein Weg durch ein anderes Werkzeug zu denselben Anmerkungen zurück.

![Eine Anmerkung, an ihrem Blickpunkt in der 3D-Ansicht geöffnet](shots/remark-viewpoint.webp)

Dein Name an dem, was du schreibst, wird im Feld **Dein Name** des Dialogs gesetzt (er bleibt auf diesem Rechner).
## Anmerkungen auf Plänen

Ein Prüfer öffnet ein ausgegebenes PDF in Acrobat, Bluebeam, Vorschau oder Drawboard und markiert es: Haftnotizen, Text, Hervorhebungen und Durchstreichungen, Stiftstriche, Rechtecke, Kreise, Pfeile, Wolken, Stempel, Messungen, Antworten. **Manage → Anmerkungen auf Plänen…** liest diese Markierungen als Anmerkungen auf dem Plan zurück ins Projekt:

![Anmerkungen: die Liste nach Plan, ein gelesenes PDF](shots/cde-remarks.webp)

- **PDF-Anmerkungen lesen…** wählt die Datei. Der Plan, zu dem sie gehört, kommt aus der Ausgabe (dem Manifest neben der Datei oder dem eigenen Ausgabenverzeichnis des Projekts), sonst aus der Namenskonvention der Datei; passt nichts, wählen Sie den Plan. Die Seiten eines Satz-PDFs gehen der Reihe nach auf seine Pläne. Was gefunden wurde, wird gezeigt — Seiten, Markierungen, wie der Plan zugeordnet wurde, etwaige Warnungen — und **Importieren** holt die Anmerkungen herein. Dieselbe Datei erneut zu lesen aktualisiert ihre Anmerkungen, statt sie zu verdoppeln; eine Anmerkung, die Sie erledigt haben, bleibt erledigt.
- **Projektordner durchsuchen** (die Desktop-App) listet die ausgegebenen PDFs in Shared und Published, welche davon sich nach ihrer Ausgabe geändert haben und ob ihre Anmerkungen seitdem gelesen wurden.
- Die Liste ist nach Plan gruppiert und nach Status gefiltert: **offen**, **erledigt**, **geschlossen**. Ein Klick öffnet den Plan mit der Anmerkung im Vordergrund. Jede Anmerkung kann erledigt, geschlossen, wieder geöffnet oder gelöscht werden; der Status „Completed“ oder „Accepted“ des Prüfers kommt als erledigt an.

![Ein Plan mit seinen Anmerkungen darüber](shots/cde-remarks-sheet.webp)

Auf dem Plan werden die Anmerkungen an ihrer Stelle über das Papier gezeichnet: der Umriss in der Prüffarbe, eine Marke mit den Initialen des Autors, der Text als Tooltip; die im Vordergrund hat ihre Karte mit dem Text, wer und wann, den Antworten und dem Status. Die Schaltfläche in der Leiste des Plans blendet sie aus oder ein, und der Projektbrowser zählt die offenen neben dem Plan. Sie sind eine Überlagerung nur für den Bildschirm: ein Plan wird ohne seine Anmerkungen gedruckt und ausgegeben.

Lapin liest das PDF selbst, ohne ein anderes Programm: klassische Dateien und solche mit Querverweis-Streams, inkrementelle Speicherungen (was das Programm des Prüfers anhängt), Text in jeder Kodierung. Eine Datei, die es nicht ganz lesen kann, liefert, was geht, mit einer Warnung. Lapin schreibt nie in das PDF.
