---
title: Türen und Fenster
order: 30
summary: Türen und Fenster in Wände setzen, verschieben und ihre Typen im Konfigurator gestalten.
---

<!-- video doors-windows -->

## Eine Tür oder ein Fenster platzieren

Wählen Sie **Architektur → Tür** (<kbd>DR</kbd>) oder **Fenster** (<kbd>WN</kbd>) und klicken Sie dann auf eine Wand dort, wo Sie die Öffnung haben möchten. Lapin schneidet die Öffnung in jede Schicht der Wand und fügt den Sturz hinzu, den der Wandtyp verlangt.

![Ein Grundriss mit einer Tür und Fenstern](shots/openings-plan.webp)

Um eine Tür oder ein Fenster entlang seiner Wand zu verschieben, wählen Sie es aus und ziehen Sie es. Um es zu ändern, verwenden Sie den Eigenschaftenbereich: seinen Typ, seine Brüstungshöhe und die Seite, zu der es sich öffnet (**Seite**).

![Die Eigenschaften einer Tür](shots/door-properties.webp)

## Der Konfigurator

**Architektur → Türen und Fenster** öffnet den Konfigurator. Jeder Tür- und Fenstertyp des Projekts ist links aufgelistet. Sie können ändern:

- den Namen und das Kennzeichen des Typs (den Buchstaben in den Beschriftungen);
- seine Breite und Höhe;
- die Tiefe und Breite des Rahmens;
- seine Spalten und Reihen von Flügeln und wie sich jeder Flügel öffnet;
- die Laibungsoberfläche, die Umrahmung an der Fassade, die Schwelle und die Farben.

Die Zeichnungen rechts zeigen den Typ so, wie er in Ansicht, Grundriss und Schnitt gezeichnet wird, während Sie ihn ändern.

![Der Konfigurator für Türen und Fenster](shots/joinery-configurator.webp)

**Typ speichern** übernimmt Ihre Änderungen. Jede Tür und jedes Fenster dieses Typs folgt ihnen. **Damit platzieren** schließt den Konfigurator und beginnt, diesen Typ zu platzieren.

### Fenster mit mehreren Flügeln

**Flügelspalten** nimmt die relativen Breiten der Flügel auf, eine Zahl für jeden: `1 1` sind zwei gleiche Flügel, `1 1 1` drei, `1 2 1` ein breiter zwischen zwei schmalen. **Flügelreihen** teilt das Fenster auf dieselbe Weise in der Höhe, von unten nach oben: `1 1` ergibt zwei Reihen. Sechs Flügel sind `1 1 1` in Flügelspalten und `1 1` in Flügelreihen. Wählen Sie dann unter **Flügel**, wie sich jeder Flügel öffnet: fest, Dreh, Dreh-Kipp, Kipp, Klapp, Schwing oder Schiebe.

### Fensterläden, Gitter, Blumenkästen und Vordächer

<!-- video window-dressings -->

Unter **An der Fassade** kann ein Fenstertyp Folgendes haben:

- **Fensterläden**: mit Lamellen, als Füllung oder aus Brettern, ein- oder zweiflügelig, geöffnet dargestellt (neben dem Fenster an die Wand geklappt) oder davor geschlossen;
- ein **Fenstergitter**: in der Laibung, an der Fassade oder an der Innenseite, mit waagerechten oder senkrechten Stäben oder beidem, deren Querschnitt und Abstand;
- einen **Blumenkasten** unter dem Fenster;
- ein **Vordach** darüber, mit seiner Auskragung und Neigung.

Jedes Teil richtet sich nach den tatsächlichen Flächen der Wand dort, wo das Fenster sitzt, aus den Schichten der Wand, und sitzt daher auf jedem Wandtyp richtig. Hat das Fenster eine Umrahmung, steht das, was an der Fassade ist, vor der Umrahmung. Sie können jedes Teil von Hand verschieben: **Von der Wandfläche** legt fest, wie weit es von der Wand absteht (bei einem Gitter in der Laibung legt **Von der Fläche zurückgesetzt** fest, wie tief es sitzt), **Von der Fensterachse** verschiebt es entlang der Wand und **Nach oben oder unten** in der Höhe. Sie erscheinen in 3D, in Grundrissen, Ansichten und Schnitten, und die Bauteillisten messen sie.

![Fensterläden, ein Gitter, ein Blumenkasten und ein Vordach im Konfigurator](shots/joinery-dressings.webp)

![Die ausgestatteten Fenster in 3D](shots/window-dressings-3d.webp)

## Beschriftungen im Grundriss

**Beschriften → Tür- und Fensterbeschriftungen** setzt eine Markierung an jede Tür und jedes Fenster eines Grundrisses: eine Linie quer durch die Wand, das Kennzeichen außen sowie Größe und Brüstung innen. Um das Aussehen der Beschriftungen zu ändern, gehen Sie zu **Verwalten → Zeichnungsstandards → Beschriftungen**.
