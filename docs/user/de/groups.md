---
title: Gruppen und Aufräumen
order: 112
summary: Halten Sie Elemente als Gruppe zusammen, öffnen Sie eine Gruppe zum Bearbeiten, wählen Sie in 3D mit einem Bereich oder Lasso aus und blenden Sie aus, was im Weg ist.
---

## Gruppen

Eine Gruppe hält Elemente zusammen. Wählen Sie sie aus und klicken Sie auf **Ändern → Gruppe erstellen** (<kbd>GP</kbd>). Ein Klick auf ein beliebiges Mitglied wählt dann die ganze Gruppe, in Grundrissen und in 3D. Die Gruppe wird als Ganzes verschoben, kopiert, gedreht, gespiegelt und gelöscht, und eine Kopie ist eine eigene Gruppe. In IFC ist eine Gruppe eine IfcGroup, sodass auch andere Programme sie sehen.

Die Eigenschaften einer gewählten Gruppe zeigen, wie viele Elemente sie hat, mit drei Schaltflächen: **Gruppe bearbeiten**, **Gruppierung aufheben** und **Mitglieder auswählen**.

![Eine gewählte Gruppe: ihre Mitglieder hervorgehoben, ihre Eigenschaften](shots/group-selected.webp)

## Eine Gruppe bearbeiten

Doppelklicken Sie auf ein Mitglied oder klicken Sie auf **Gruppe bearbeiten** (<kbd>EG</kbd>). Der Rest des Modells wird blass gezeichnet und lässt sich nicht wählen, und ein Klick wählt jeweils ein Mitglied. Löschen, verschieben oder ändern Sie Mitglieder wie gewohnt. **Aus Gruppe entfernen** nimmt die gewählten Mitglieder heraus. **Fertig** oder <kbd>Esc</kbd> schließt die Gruppe. Eine Gruppe verschwindet, wenn ihr letztes Mitglied gelöscht wird.

![Eine zum Bearbeiten geöffnete Gruppe: die Gartenmauer außerhalb blass](shots/group-edit.webp)

## Importierte Dateien

Jede IFC-, Fragments- oder Netzdatei, die Sie importieren, kommt als eine Gruppe mit dem Namen der Datei. Entfernen Sie beim IFC-Import das Häkchen bei **Als eine Gruppe**, um die Elemente lose zu holen. Eine verknüpfte Datei wird nicht gruppiert: Sie ist bereits eine Verknüpfung, schreibgeschützt (siehe [Import und Export](import-export.md)). Elemente, die als Referenzen aus einer IFC-Datei gelesen werden, bleiben, wo die Datei sie hingesetzt hat: Eine Gruppe davon lässt sich aufräumen und löschen, nicht verschieben.

## Einen Bereich in 3D auswählen

**Ansicht → Bereich auswählen** (<kbd>SR</kbd>) macht aus einem Ziehen mit der linken Taste in der 3D-Ansicht eine Auswahl, wie in Grundrissen. Nach rechts gezogen nimmt ein Fenster, was ganz darin liegt. Nach links gezogen nimmt eine Kreuzung, was sie berührt. Drücken Sie beim Ziehen die <kbd>Leertaste</kbd> für ein Lasso und <kbd>Umschalt</kbd>, um zur Auswahl hinzuzufügen. Solange er an ist, dreht die rechte Taste die Ansicht und die mittlere verschiebt sie. Das **×** auf seinem Chip, oder erneut <kbd>SR</kbd>, schaltet ihn aus.

![Eine Kreuzung über dem Haus in 3D gezogen](shots/select-region-3d.webp)

## Vorübergehend ausblenden

**Ansicht → Ausblenden** (<kbd>HH</kbd>) blendet die Auswahl in der 3D-Ansicht aus. **Isolieren** (<kbd>HI</kbd>) blendet alles andere aus. **Alles zeigen** (<kbd>HR</kbd>), oder das **×** auf dem Chip, bringt alles zurück. Nichts wird gespeichert oder gelöscht: So erreichen Sie, was in einem importierten Modell innen liegt, während Sie es aufräumen.
