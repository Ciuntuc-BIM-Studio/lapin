---
title: Schnitte, Ansichten und 3D
order: 70
summary: Erstellen Sie Schnitte und Ansichten, öffnen Sie 3D-Ansichten und legen Sie fest, was jede Ansicht zeigt und wie.
---

Alle Ansichten stehen im Projektbrowser, abgelegt unter ihrer Disziplin: Architektur, Tragwerk oder TGA. Doppelklicken Sie auf eine Ansicht, um sie in einer Registerkarte zu öffnen.

![Der Projektbrowser mit einem Schnitt und einer Ansicht](shots/browser.webp)

## Schnitte

Wählen Sie **Ansicht → Schnitt** (<kbd>SE</kbd>) oder klicken Sie im Browser auf **+** neben **Schnitte**. Zeichnen Sie dann die Schnittlinie in einem Grundriss mit zwei Klicks und klicken Sie ein drittes Mal auf die Seite, in die der Schnitt blickt. Der Schnitt wird aus dem Modell gezeichnet und bleibt aktuell, während Sie das Modell ändern.

![Schnitt A-A durch das Haus](shots/section-view.webp)

Ein Schnitt muss keine einzelne gerade Linie sein. **Versetzter Schnitt** (<kbd>SJ</kbd>) zeichnet ihn als Polylinie, Klick für Klick, <kbd>Enter</kbd> schließt ab: jedes gerade Stück ist ein ebener Schnitt, der nur zwischen seinen eigenen Enden sieht, und die Stücke werden nebeneinander in ihrer wahren Länge gezeichnet, eine feine gestrichelte Linie markiert jeden Versatz. **Gekrümmter Schnitt** (<kbd>SC</kbd>) zeichnet einen Bogen durch drei Punkte (Anfang, Ende, ein Punkt darauf) und wickelt ihn genauso ab, sodass eine gekrümmte Fassade in ihrer wahren Länge zu lesen ist. **Schnitt entlang** (<kbd>SA</kbd>) wählt ein Element mit einer Linie im Grundriss, eine Straße oder einen Weg, einen Graben, eine Stützwand, eine Wand, einen Träger, ein Fundament oder eine gezeichnete Polylinie, und folgt ihm, wie es jetzt ist; unten schreibt er die Stationen (0+020, alle Zehntel des Maßstabs, oder den Abstand, den Sie unter **Stationen** in den Einstellungen der Ansicht setzen). Im Grundriss sind die Punkte eines Schnitts Griffe: einen ziehen, die Mitte eines Stücks ziehen, um einen Versatz einzufügen, Strg/⌘-Klick auf einen Punkt, um ihn zu entfernen. In 3D gezeigt, schneidet ein abgewickelter Schnitt das Modell entlang seines ersten Stücks und legt seine Zeichnung Stück für Stück.

![Ein versetzter Schnitt durch das Haus: drei Stücke nebeneinander, die Versätze markiert](shots/section-jogged.webp)

Wählen Sie den Schnitt im Browser aus, um im Eigenschaftenbereich seinen Maßstab, seine Tiefe, seine Disziplin und seinen Stil festzulegen.

## Ansichten

**+** neben **Ansichten** erstellt die vier Ansichten des Gebäudes auf einmal.

![Die Südansicht](shots/elevation-view.webp)

## 3D-Ansichten

<!-- video section-3d -->

Doppelklicken Sie auf **3D**, um die 3D-Ansicht zu öffnen. Ziehen Sie mit der linken Maustaste, um das Modell zu umkreisen. Der Übersichtsplan oben rechts zeigt, von wo aus Sie blicken. **Grundriss**, **Persp** und **Einpassen** ändern die Kamera. Um eine Kamera zu behalten, duplizieren Sie die 3D-Ansicht.

### Ein Schnitt in 3D

Klicken Sie im Browser mit der rechten Maustaste auf einen Schnitt und wählen Sie **In 3D anzeigen**. Die 3D-Ansicht schneidet das Modell entlang der Schnittlinie. Sie behält die Seite, in die der Schnitt blickt, bis zur Tiefe des Schnitts, falls er eine hat. Jedes geschnittene Element wird dort geschlossen, wo es geschnitten wird, Schicht für Schicht, jede Schicht in ihrem eigenen Material: Eine Wand zeigt ihren Ziegelkern und ihren Putz, so massiv wie die Wand ist. **Schraffuren** auf dem Chip unten in der 3D-Ansicht legt stattdessen die Zeichnung des Schnitts auf die Schnittfläche, mit den Schnittfüllungen und Schraffuren, die ein Plan druckt. Die Bemaßungen, Höhenkoten, Beschriftungen und Texte des Schnitts liegen in derselben Ebene, in derselben Größe und an derselben Stelle wie im Schnitt. Linien behalten ihre Stiftstärken und ihre vollständigen Strichmuster. Was neben dem Gebäude gezeichnet ist, steht neben der Schnittfläche in der Luft. **Beschriftungen** auf dem Chip unten in der 3D-Ansicht blendet sie aus oder wieder ein. **In 3D anzeigen** bei einer Ansicht dreht die Kamera so, dass sie in dieselbe Richtung blickt wie die Ansicht.

![Das Kontextmenü des Schnitts im Browser](shots/section-3d-menu.webp)

![Schnitt B-B in 3D gesehen](shots/section-3d.webp)

Um wieder das ganze Modell zu sehen, klicken Sie auf das **×** auf dem Chip **Schnitt** unten in der 3D-Ansicht oder wählen Sie erneut **In 3D anzeigen**.

![Schnitt B-B mit eingeschalteten Schraffuren: die Schnittfläche, wie der Plan sie druckt](shots/section-3d-hatches.webp)

### Der Schnittbereich

**Ansicht → Schnittbereich** (Kürzel BX) schneidet die 3D-Ansicht auf einen Quader zu. Der Quader umschließt zu Beginn das ganze Modell. Jede Fläche hat in ihrer Mitte einen Pfeil: Ziehen Sie ihn, um diese Fläche zu verschieben. Jedes Element, das der Quader schneidet, wird an dieser Fläche geschlossen, Schicht für Schicht, in seinen Materialien, und die Flächen treffen sich sauber an den Kanten des Quaders. **Am Grundriss schneiden** setzt die Oberseite des Quaders auf die Grundrissschnitthöhe der aktiven Ebene (ihren Ansichtsbereich), sodass sich die Ebene in 3D so liest, wie ihr Grundriss sie schneidet. **Einpassen** legt den Quader wieder um das ganze Modell. Um den Quader zu drehen, ziehen Sie den runden Griff an seiner oberen Ecke (gradweise; halten Sie Umschalt für 15°-Schritte) oder wählen Sie eine Wand aus und klicken Sie auf **Ausrichten**: Der Quader dreht sich in die Richtung, in der die Wand verläuft. **Schraffuren** zeichnet die Schnitte des Quaders wie ein Plan, jedes Material mit seiner Schnittfüllung und Schraffur, statt der Materialien. Eine gespeicherte 3D-Ansicht behält ihren Schnittbereich: Speichern Sie die Ansicht mit eingeschaltetem Quader, oder öffnen Sie eine gespeicherte Ansicht und legen Sie ihren Quader fest, und der Quader ist jedes Mal da, wenn die Ansicht geöffnet wird. Das **×** auf dem Chip **Schnittbereich** schaltet den Quader aus.

![Der Schnittbereich am Grundriss des Erdgeschosses geschnitten, seine Ostfläche nach innen gezogen](shots/section-box.webp)

![Derselbe Quader mit eingeschalteten Schraffuren: die Schnitte, wie ein Plan sie zeichnet](shots/section-box-hatches.webp)

### Ein Geschoss nach dem anderen

Um ins Innere des Gebäudes zu blicken, zeigen Sie nur einige seiner Geschosse an. Klicken Sie im Projektbrowser mit der rechten Maustaste auf eine Ebene und wählen Sie **Nur diese Ebene in 3D anzeigen** oder **Diese Ebene und darunter in 3D anzeigen**, um das Gebäude auf dieser Ebene geschnitten zu sehen. Die Ebene wird zur aktiven Ebene, und die 3D-Ansicht folgt der aktiven Ebene: Klicken Sie im Browser auf eine andere Ebene, um ein Geschoss nach oben oder unten zu gehen.

![Das Kontextmenü der Ebene im Browser](shots/storeys-3d-menu.webp)

![Das Erdgeschoss allein in 3D](shots/storeys-3d.webp)

**Ansicht → Angezeigte Geschosse** oder die Eigenschaften der 3D-Ansicht legen den Rest fest:

- **Anzeigen**: alle Geschosse, die angehakten Geschosse, nur das aktive Geschoss oder das aktive Geschoss und die darunter.
- **Auf keinem Geschoss**: Was auf keinem Geschoss steht (das Grundstück, das Gelände, verknüpfte Dateien), wird angezeigt oder ausgeblendet.
- **Über seiner Ebene schneiden**: Das Modell wird so weit über dem höchsten angezeigten Geschoss geschnitten, anfangs 1,20 m, wie ein Grundriss es schneidet. Jedes dort geschnittene Element wird an der Schnittfläche geschlossen, Schicht für Schicht, in seinen Materialien, wie bei einem in 3D angezeigten Schnitt.

![Das Erdgeschoss bei 1,20 m geschnitten: seine Wände geschlossen, Schicht für Schicht](shots/storeys-3d-cut.webp)

Die ausgeblendeten Geschosse werden nicht gezeichnet, und Sie können sie weder auswählen noch auf sie einpassen noch auf ihnen gehen. Was ein Schnitt wegnimmt (der Schnitt über den angezeigten Geschossen, ein in 3D angezeigter Schnitt, der Schnittbereich), ist auch bei einer Begehung nicht da: Sie gehen an dem oberen Teil einer Wand vorbei, den der Schnitt entfernt hat, und die Abdeckungen an der Schnittfläche schließen, was er öffnet. **Datei → 3D exportieren…** übernimmt die 3D-Ansicht so, wie sie sie zeigt (siehe [Import und Export](import-export.md)). Eine gespeicherte 3D-Ansicht behält ihre eigene Auswahl. Der Chip **Geschosse** unten in der 3D-Ansicht sagt, was angezeigt wird; sein **×** zeigt wieder alle Geschosse.

### Ohne Bekleidungen oder nur die Kerne

Eine 3D-Ansicht kann das Tragwerk des Gebäudes allein zeigen, wie ein Tragwerksplan. In den Eigenschaften der 3D-Ansicht (wenn nichts ausgewählt ist) oder in **Ansicht → Ansichtseinstellungen**, während die 3D-Ansicht vorne liegt, zeigt **Schichten**:

- **alle Schichten**: die Wände, Decken und Fundamente, wie sie gebaut sind;
- **ohne Bekleidungen**: ohne die Schichten, deren Material eine Bekleidung ist (Putz, Außenputz, Anstrich, Fliesen, Platten, Bodenbeläge oder ein Material, dessen Kategorie in Verwalten → Materialien Bekleidung ist); die Dämmung bleibt;
- **nur der Kern**: nur die als Kern markierte Schicht (der Ziegel, die Betondecke).

![Ein Haus in 3D nur mit dem Kern: Die Wände sind ihr Ziegel, die Decken ihre Platte](shots/layers-3d-core.webp)

Jede beibehaltene Schicht bleibt ein geschlossener Körper in ihrem eigenen Material, sodass ein Schnitt oder der Schnittbereich sie wie zuvor schließt. Türen und Fenster bleiben in ihren Öffnungen; eine mit einer Bekleidung ausgekleidete Laibung gehört zu den Bekleidungen der Wand. Eine 3D-Ansicht übernimmt ihre Schichten aus ihrem Stil, wie ein Grundriss: Setzen Sie ihre **Disziplin** auf Tragwerk, und sie zeigt nur die Kerne; stellen Sie **Schichten** ein, um anders zu wählen, und die Schaltfläche daneben kehrt zu den Schichten des Stils zurück. Der Chip **Angezeigte Schichten** unten in der 3D-Ansicht sagt, was weggelassen wird; sein **×** zeigt wieder alle Schichten. Die ausgeblendeten Schichten werden nicht ausgewählt und halten Sie bei einer Begehung nicht auf, und **Datei → 3D exportieren…** exportiert, so wie die Ansicht es zeigt, dieselben Schichten.

### In einen Raum gehen

Klicken Sie in einem Grundriss mit der rechten Maustaste in einen Raum und wählen Sie **In 3D begehen**. Die 3D-Ansicht stellt Sie in den Raum, 1,60 m über seinem Fußboden, mit Blick entlang seiner längsten Seite.

![Das Kontextmenü eines Raums im Grundriss](shots/room-walk-menu.webp)

![Im Wohnzimmer stehend](shots/room-walk.webp)

- Ziehen Sie mit der linken Maustaste, um sich umzusehen.
- <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> oder die Pfeiltasten gehen. Halten Sie <kbd>Umschalt</kbd>, um zu laufen. <kbd>Leertaste</kbd> springt.
- Das Mausrad bewegt Sie schrittweise vor und zurück.

Sie gehen, wie ein Mensch gehen würde. Wände, Stützen, Möbel und geschlossene Fenster halten Sie auf, und Sie gleiten an ihnen entlang. Sie stehen auf Decken, gehen Treppen und Rampen hinauf und hinunter und fallen, wenn Sie über eine Kante gehen. Türen stehen für Sie offen: Gehen Sie durch ihre Öffnungen. Nur was die 3D-Ansicht zeichnet, hält Sie auf: Ausgeblendete Elemente, die Geschosse, die sie nicht zeigt, und was ihre Schnitte wegnehmen, sind Ihnen nicht im Weg.

![Zur Tür des Schlafzimmers gehen](shots/room-walk-door.webp)

![Die Treppe hinauf](shots/room-walk-stair.webp)

**Fliegen** auf dem Chip der Begehung oder <kbd>F</kbd> lässt Sie stattdessen fliegen: ohne Schwerkraft, und Sie gehen durch Wände und Decken hindurch. <kbd>E</kbd> und <kbd>Q</kbd> steigen und sinken. Drücken Sie erneut <kbd>F</kbd>, um zu gehen; Sie landen auf dem Boden unter Ihnen.

<kbd>Esc</kbd> oder **Begehung beenden** stellt die Kamera wieder her, die Sie vorher hatten.

## Was eine Ansicht zeigt

Die Registerkarte **Ansicht** legt fest, was die vorne liegende Ansicht zeigt und wie sie aussieht.

![Die Registerkarte Ansicht](shots/ribbon-view.webp)

- **Ansichtseinstellungen.** Die Disziplin und der Stil der Ansicht, ihr Bereich (die Schnitthöhe und -tiefe) und ihre Grafiken.
- **Ansichtsstile.** Architektur, Tragwerk (nur die Kerne), TGA oder Ihr eigener. Jeder Stil wählt die gezeichneten Schichten (alle, ohne Bekleidungen oder den Kern) und wie Materialien sich verbinden.
- **IFC-2D-Symbole.** Aus einer IFC-Datei importierte oder verknüpfte Elemente, die ihre eigene Grundrisszeichnung mitbringen (eine Grundfläche, den Aufschlag einer Tür, das Symbol eines Objekts, mit den Farben, Füllungen und Texten der Datei), zeichnen diese in Grundrissen statt ihres Schnitts. Entfernen Sie das Häkchen in den Ansichtseinstellungen (oder in einem Ansichtsstil), um stattdessen ihren 3D-Schnitt zu zeichnen.
- **Sichtbarkeit** (<kbd>VV</kbd>) und **Grafiken** (<kbd>VG</kbd>). Sie blenden Kategorien ein oder aus und legen ihre Stifte, Linientypen und Füllungen fest.
- **Teilen** und **Fenster.** Sie zeigen zwei Ansichten nebeneinander oder öffnen eine Ansicht in einem eigenen Fenster.
