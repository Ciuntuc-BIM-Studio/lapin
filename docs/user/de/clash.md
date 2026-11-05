---
title: Kollisionserkennung
order: 58
summary: Findet, wo sich Elemente des Modells durchdringen oder zu nahe kommen: Suchsätze nach Regeln, harte und Abstandsprüfungen zwischen ihnen, Kollisionen von Lauf zu Lauf verfolgt, in 3D gezeigt und als BCF gesendet.
---

**Verwalten → Kollisionserkennung** findet, wo sich Elemente des Modells durchdringen oder näher kommen als sie sollten. Sie arbeitet gleich mit Lapins eigenen Elementen und mit verknüpften IFC- und Fragments-Dateien, koordiniert also auch die Modelle, die dir andere Fachplaner schicken.

![Die Kollisionserkennung neben der 3D-Ansicht](shots/clash-dialog.webp)

## Tests und Sätze

Ein **Kollisionstest** vergleicht zwei **Suchsätze**: Satz A mit Satz B oder einen Satz mit sich selbst. Ein Projekt beginnt mit drei Sätzen (Structure, Services, Architecture) und drei harten Tests: Structure × Services, Services × Services und Architecture × Services.

- **Harte** Tests finden Elemente, die sich durchdringen. Ihre **Toleranz** (standardmäßig 5 mm) übergeht so flache Durchdringungen, damit eine 3 mm in eine Wand gelegte Bekleidung nicht gemeldet wird.
- **Abstands**-Tests finden Elemente, die näher als ein Abstand sind (standardmäßig 50 mm), und auch Durchdringungen.

Manche Paare werden nie geprüft, weil sie sich treffen sollen: eine Wand und die Tür oder das Fenster in ihrer Öffnung, die Teile einer Baugruppe, an einer Ecke verbundene Wände und aneinander angeschlossene Elemente (ein Träger auf einer Stütze, eine an ein Dach angeschlossene Wand).

![Die Einstellungen eines Tests](shots/clash-test.webp)

## Ausführen und verfolgen

**Test ausführen** führt den vorne stehenden Test aus; **Alle ausführen** jeden eingeschalteten Test. Nichts läuft von selbst: Die Ergebnisse bleiben im Projekt, bis du den Test erneut ausführst, und der Dialog sagt, wenn sich das Modell seitdem geändert hat.

Jede Kollision behält ihre Nummer von Lauf zu Lauf. Eine zum ersten Mal gefundene Kollision ist **Neu**; erneut gefunden, ist sie **Aktiv**. Markiere sie von Hand als **Geprüft** oder **Genehmigt**, weise sie jemandem zu (**Zugewiesen an**) und schreibe **Notizen**. Eine Kollision, die ein Lauf nicht mehr findet, wird **Gelöst**; kommt sie zurück, ist sie wieder offen, mit derselben Nummer.

Klicke auf eine Kollision, um sie in 3D zu sehen. Lapin wählt die beiden Elemente, legt einen Schnittbereich um ihre Begegnung und schaut von der Seite darauf, damit du das Rohr durch die Wand siehst und nicht die Wandfläche. **allein in 3D** blendet den Rest aus; die Pfeile gehen die Kollisionen einzeln durch. Der Dialog bleibt rechts, sodass du die Kamera bewegen kannst, während er offen ist.

![Ein Rohr durch eine Wand, von der Kollision gerahmt](shots/clash-3d.webp)

**BCF** macht aus jeder offenen Kollision (oder den gewählten) eine Anmerkung vom Typ Clash mit ihrem Blickpunkt und speichert sie als BCF-Datei für andere BIM-Programme; erneut gesendet, werden dieselben Anmerkungen aktualisiert. **CSV** speichert die Liste für eine Tabelle.

## Suchsätze

**Suchsätze** enthält die Regeln, die die Elemente eines Satzes wählen. Eine Regel liest ein Feld des Elements (Klasse, vordefinierter Typ, Typ, Geschoss, Material, Name, verknüpfte Datei oder einen beliebigen Parameter) mit einem Operator: ist, ist einer von, ist nicht, ist keiner von, enthält, enthält nicht, mindestens, höchstens, zwischen. Zahlen werden gelesen, wie Menschen sie schreiben: „600 mm“ ist 600.

Ein Satz nimmt die Elemente, die irgendeine Regel erfüllen, oder jede Regel. Eine „nicht“-Regel nimmt immer Elemente heraus, und ein Satz ohne Regel, die Elemente hinzufügt, enthält alles. Die Zahl neben dem Satz zeigt beim Tippen, wie viele Elemente die Regeln nehmen.

![Die Regeln eines Suchsatzes](shots/clash-sets.webp)

> Lapins eigene Installationsleitungen können ihre Öffnungen in Wänden und Decken selbst schneiden (Verwalten → Schnittreihenfolge). Dann ist ein Rohr durch eine Wand keine Kollision: Die Wand hat ihre Öffnung. **Verwalten → Kollisionsprüfung…** prüft diese Öffnungen.
