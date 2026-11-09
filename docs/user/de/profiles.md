---
title: Profilbibliotheken
order: 57
summary: Die Profile, die Ihre Arbeit braucht, nach welcher Norm auch immer — Lapins kleiner EN-Satz, Ihre Tabellen und IFC-Dateien, die Ordner der Firma; gezeichnet, mit berechneten Eigenschaften und geprüften Dateiwerten, beim Gebrauch ins Projekt kopiert.
---

Lapin kann nicht alle Stahlprofile der Welt mitliefern. Es bringt einen kleinen europäischen Satz mit und liest den Rest aus dem, was Sie schon haben: einen Katalog als Tabelle (AISC Shapes Database, die Tabelle eines Herstellers, die Ihrer Firma), eine IFC-Profilbibliothek (etwa die von Bonsai) oder ein beliebiges IFC-Modell, einen geschlossenen Umriss, in Lapin oder in einer DXF gezeichnet. **Verwalten → Profilbibliotheken** hält sie alle.

![Profilbibliotheken](shots/profile-libraries.webp)

## Die Bibliotheken

- **Integriert**: *EN steel (Lapin)*: IPE, HEA, HEB, HEM, UPN, UPE, gleich- und ungleichschenklige Winkel, warmgefertigte SHS, RHS und CHS in üblichen Größen sowie generische kaltgeformte C, Z und Σ. Nur Normmaße; alle Eigenschaften berechnet Lapin.
- **Auf diesem Computer**: die von Ihnen importierten Bibliotheken, für jedes Projekt. Auch die Browserversion behält sie, in diesem Browser.
- **Firmenordner**: in der Desktop-App beliebig viele lokale, Netzwerk- oder synchronisierte Ordner mit `.lapinprofiles`-Dateien (**Ordner hinzufügen…**). Ein Ordner ist schreibgeschützt, bis Sie **beschreibbar** ankreuzen; dann schreibt die Speichern-Schaltfläche daneben die gewählte Bibliothek dorthin, für Ihre Kollegen.
- **Im Projekt**: Nur die verwendeten Profile werden ins Projekt kopiert, mit dem Namen ihrer Bibliothek, damit sich die Datei überall öffnet.

Tippen Sie ins Suchfeld, um alle Bibliotheken zugleich nach Name, Familie, Norm oder Art zu durchsuchen; wählen Sie ein Profil, um es mit Maßen und Eigenschaften zu sehen.

## Eigenschaften

Aus den Maßen berechnet Lapin Fläche, Iy, Iz, elastische und plastische Widerstandsmomente, Trägheitsradien, Hauptachsen und ihren Winkel (Winkel, Z, jeder unsymmetrische Umriss), Torsionskonstante It und Wölbwiderstand Iw, wo es eine Formel gibt, die Masse pro Meter aus der Dichte des Materials und die Anstrichfläche pro Meter. Sie stimmen auf etwa 1 % mit den veröffentlichten Tabellen überein (It und Iw gewalzter I-Profile auf 3 %; Iw eines U-Profils mit geneigten Flanschen nur auf 20 %). Ein beliebiger Umriss hat It und Iw nur, wenn seine Datei sie angibt.

Die Werte einer Datei bleiben erhalten und stehen neben denen von Lapin. Eine Abweichung über 2 % (Fläche, Masse), 3 % (Trägheits- und Widerstandsmomente) oder 10 % (It, Iw) wird markiert und kommt auf die Prüfliste des Imports; bei einer ganzen Spalte, die um eine Zehnerpotenz abweicht, wird eine andere Einheit gemeldet.

![Ein Dateiwert, der von Lapins abweicht](shots/profile-check.webp)

## Eine Tabelle importieren

**Tabelle…** liest eine CSV oder XLSX, eine Zeile je Profil. Bedeutung und Einheit jeder Spalte werden aus der Überschrift auf Englisch, Rumänisch, Deutsch oder Französisch erraten (h, d, Höhe, hauteur, înălțime; tw, s, Stegdicke; A, Iy, Wel, Wpl, It, Iw, G, kg/m…), samt Einheiten (mm, cm, in, cm², cm⁴, in⁴, ×10³ cm⁶, lb/ft). Eine Tabelle mit Ix und ohne Iz wird amerikanisch gelesen: x die starke Achse, in Zoll. Korrigieren Sie im Dialog, was falsch ist; Lapin merkt sich die Wahl für die nächste Datei mit gleichem Aufbau.

![Die Spalten einer Tabelle zugeordnet](shots/profile-table.webp)

Die Art einer Zeile kommt aus ihrer Typspalte (W, HP, UPN, HSS, PIPE…) oder dem Namen; für die übrigen wählen Sie sie im Dialog.

## IFC

**IFC…** liest alle Profildefinitionen einer beliebigen IFC-Datei: eine Bibliothek oder ein Modell aus Tekla, Revit oder ArchiCAD, wo die von Elementen verwendeten Profile nach ihren Typen heißen. I, unsymmetrisches I, U, T, L, C, Z, Hohlprofile, Rechteck, Kreis und Ellipse behalten Art und Maße; beliebige, zusammengesetzte und abgeleitete Profile werden Umrisse. Die mechanischen Werte aus `Pset_ProfileMechanical` (und IFC2x3s Profileigenschaften) kommen mit; Exporteure sind sich über ihre Einheiten und über die Y-Achse nicht einig, deshalb entscheidet Lapin beides nach der Geometrie. Mittellinien- und Trapezprofile bleiben draußen, und das wird gesagt.

![Aus einer IFC-Datei gelesene Profile](shots/profile-ifc.webp)

**IFC** und **CSV** unter der Liste schreiben die gewählte Bibliothek (oder die Profile des Projekts) als IFC4-Profilbibliothek oder als Tabelle, die Lapin wieder liest.

Auch im IFC des Modells werden Träger, Stützen und Stahlprofile über ihr parametrisches Profil geschrieben (`IfcIShapeProfileDef`, `IfcLShapeProfileDef`, …, Rechtecke und Kreise), mit dem Profilnamen und `Pset_ProfileMechanical`, sodass andere Programme ein IPE 300 sehen und keine Polylinie. Konturen, Σ und Z mit Lippen bleiben beliebige Profile; ein von einem anderen geschnittenes Bauteil bleibt der Körper, der es ist.

## Ein Umriss als Profil

Zeichnen Sie den Querschnitt mit Zeichnen aus geschlossenen Polylinien und Kreisen (die in einem anderen sind Löcher), wählen Sie sie und drücken Sie **Aus der Auswahl**, oder lesen Sie die Zeichnung eines Herstellers mit **DXF…**. Benennen Sie ihn, wählen Sie Material und Bibliothek und behalten Sie ihn. So kommen Aluminium- und PVC-Profile, Fertigteilquerschnitte und Holzformen hinein: derselbe Speicher, mit eigenem Material und eigener Dichte.

![Ein gezeichneter Umriss als Profil](shots/profile-outline.webp)

**Umriss zeichnen** macht das Gegenteil: der Umriss des gewählten Profils als geschlossene Polylinien in der vorderen Ansicht, zum Extrudieren oder Sweepen in Studio.

**Profil zeichnen…** öffnet den Querschnittseditor: die Außenkontur und ihre Kammern als Punkte in Millimetern. Ziehen Sie die Punkte (1 mm; <kbd>Umschalt</kbd> 5 mm), klicken Sie auf eine Kontur, um einen Punkt hinzuzufügen, mit Rechtsklick auf einen Punkt entfernen Sie ihn, oder tippen Sie sie in die Tabelle; beginnen Sie mit einem Rechteck, einem Hohlkasten, einem T, einem Winkel oder einem U-Profil mit den Maßen, die Sie angeben, und fügen Sie Kammern in der Mitte hinzu. Benennen Sie es, wählen Sie Aluminium, erstellen und speichern Sie es wie jede Kontur. **Kontur bearbeiten…** öffnet ein vorhandenes Profil im Editor: erneut unter seinem Namen gespeichert, wird die Kopie des Projekts als neuer markiert, und **Aus der Bibliothek aktualisieren** bringt es zu jedem Typ und jedem Vorhangfassadenstab, der daraus gemacht ist. Ausgewählte Pfosten und Riegel nehmen ein eigenes Profil, gedreht, ausgerichtet und versetzt: siehe [Wände](walls.md).

![Der Querschnittseditor: ein Hohlkasten mit seiner Kammer](shots/profile-editor.webp)

## Ein Profil verwenden

Wählen Sie in **Stahl → Stahltypen**, **Tragwerk → Tragwerkstypen** (Stützen und Träger) und bei den Profilen der Rahmenhalle **Aus einer Profilbibliothek…** und tippen Sie, um zu suchen.

![Die Auswahl durchsucht alle Bibliotheken](shots/profile-picker.webp)

Das Profil wird ins Projekt kopiert, und der Typ behält es. Körper sind leicht, mit scharfen Ecken, auf jedem Grad außer einem: eine Ansicht auf **LOD 400** (Grundriss, Schnitt, Ansicht, Plan oder 3D) zeichnet den wirklichen Querschnitt mit Ausrundungen, Flanschspitzenradien und geneigten Flanschen. Exporte und Auswahl behalten den scharfen Körper, aber Mengen, Gewichte, Auszüge, die Stahlbauteilliste und Kostenschätzungen nehmen die wirkliche Querschnittsfläche (ein IPE 300 hat 53,81 cm², nicht die 51,88 der scharfen Kontur); ein Querschnitt ohne Radien ändert sich nicht.

![Stahlträger und -stütze in einer 3D-Ansicht auf LOD 400: Ausrundungen gezeichnet](shots/profile-real-section.webp)

Anderswo gibt das Profil, was das Werkzeug nutzen kann: Wandprofile, Nuten und Streifenfundamente nehmen seine Außenseite als 2D-Profil (**Aus einer Profilbibliothek…** in ihrer Liste), Pfosten und Riegel von Vorhangfassaden sowie Rahmen und Flügel von Türen und Fenstern seine wirkliche Kontur samt Hohlräumen entlang jedes Stabs, mit seiner Breite und Tiefe; das x des Profils läuft quer zum Stab (bei Rahmen und Flügel zum Glas), sein y aus dem Gebäude heraus. **Einfach** nimmt die Kontur weg und behält die Größe.

![Pfosten und Riegel einer Vorhangfassade aus einem Aluminiumquerschnitt](shots/profile-real-mullions.webp)

![Rahmen und Flügel eines Fensters aus demselben Querschnitt](shots/profile-real-frame.webp)

Eine Bibliothek kann sich ändern, nachdem Sie sie benutzt haben. Ein Projektprofil, das von dem gleichnamigen in seiner Bibliothek abweicht, wird mit **neuer in der Bibliothek** markiert; **Aus der Bibliothek aktualisieren** ersetzt es, und alle daraus gemachten Typen folgen, als ein Rückgängig-Schritt. Nichts ändert sich, bevor Sie es verlangen. Ältere Projekte behalten ihre Typen.
