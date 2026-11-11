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

Ein Schnitt muss keine einzelne gerade Linie sein. **Versetzter Schnitt** (<kbd>SJ</kbd>) zeichnet ihn als Polylinie, Klick für Klick, <kbd>Enter</kbd> schließt ab: jedes gerade Stück ist ein ebener Schnitt, der nur zwischen seinen eigenen Enden sieht, und die Stücke werden nebeneinander in ihrer wahren Länge gezeichnet, eine feine gestrichelte Linie markiert jeden Versatz. **Gekrümmter Schnitt** (<kbd>SV</kbd>) zeichnet einen Bogen durch drei Punkte (Anfang, Ende, ein Punkt darauf) und wickelt ihn genauso ab, sodass eine gekrümmte Fassade in ihrer wahren Länge zu lesen ist. **Schnitt entlang** (<kbd>SA</kbd>) wählt ein Element mit einer Linie im Grundriss, eine Straße oder einen Weg, einen Graben, eine Stützwand, eine Wand, einen Träger, ein Fundament oder eine gezeichnete Polylinie, und folgt ihm, wie es jetzt ist; unten schreibt er die Stationen (0+020, alle Zehntel des Maßstabs, oder den Abstand, den Sie unter **Stationen** in den Einstellungen der Ansicht setzen). Im Grundriss sind die Punkte eines Schnitts Griffe: einen ziehen, die Mitte eines Stücks ziehen, um einen Versatz einzufügen, Strg/⌘-Klick auf einen Punkt, um ihn zu entfernen. In 3D gezeigt, schneidet ein abgewickelter Schnitt das Modell entlang seines ersten Stücks und legt seine Zeichnung Stück für Stück.

![Ein versetzter Schnitt durch das Haus: drei Stücke nebeneinander, die Versätze markiert](shots/section-jogged.webp)

Wählen Sie den Schnitt im Browser aus, um im Eigenschaftenbereich seinen Maßstab, seine Tiefe, seine Disziplin und seinen Stil festzulegen.

### Wo ein Schnitt markiert wird

Die Linie eines Schnitts wird in jedem Grundriss gezeichnet, und der Schnitt wird in jedem anderen Schnitt und jeder Ansicht markiert, die seine Ebene kreuzt: eine feine gestrichelte Linie, wo die Ebene die Ansicht schneidet, mit dem Namen des Schnitts und einem Pfeil in seine Blickrichtung oben, auf dem Papier so groß wie im Grundriss. Ein Schnitt wird nie in sich selbst markiert. Ein Klick auf einen Marker wählt den Schnitt aus, ein Doppelklick öffnet ihn.

**Markergröße.** **Verwalten → Zeichnungsstandards → Projekt** legt fest, wie groß die Marker gezeichnet werden, in Papiermillimetern, für Schnitte und Ansichten getrennt: **Schnittmarkengröße** (die Pfeile eines Schnitts sind 0,7 davon) und **Schnittmarkentext**, **Ansichtsmarkengröße** (der Kreis der Ansicht im Durchmesser) und **Ansichtsmarkentext**; 10 mm und die Höhe des Standard-Textstils, bis Sie sie festlegen. Ein Projekt, das eine einzige Markergröße hatte, behält sie für beide. Ein Schnitt oder eine Ansicht mit eigener Größe hat **Markergröße (mm)** und **Markertext (mm)** in den Eigenschaften; leer übernimmt sie die des Projekts. Die Engine zeichnet die Marker, daher folgen Grundrisse, die Schnitte, die sie kreuzen, Pläne, das PDF und die DXF-Datei.

**Einen Schnitt verschieben.** Wählen Sie den Schnitt in einem Grundriss aus und verschieben Sie ihn mit **Verschieben** (oder ziehen Sie ihn an seiner Linie): Wie in Revit schneidet er dort, wo Sie ihn hinsetzen, und gedreht oder gespiegelt blickt er in die gedrehte oder gespiegelte Richtung. Jeder Kopf (sein Pfeil und sein Name) hat einen eigenen Griff: Ziehen Sie ihn, um den Kopf vom Ende der Linie wegzurücken, etwa weg von einer Bemaßung, und eine feine Hinweislinie verbindet ihn wieder; der Schnitt schneidet unverändert. <kbd>Strg</kbd>/<kbd>⌘</kbd>-Klick auf den Griff setzt den Kopf zurück. Ein Schnitt quer oder längs durch ein Element folgt dem Element und wird mit ihm verschoben.

Jeder Schnitt liegt auf einem **Marker-Layer** (zuerst **Sections**; **Marker-Layer** in den Eigenschaften des Schnitts ändert ihn), und jede Ansicht wählt, was sie markiert. **Ansicht → Schnitte…** listet die Schnitte des Projekts nach Layer für die vordere Ansicht, mit einem Kontrollkästchen je Layer und je Schnitt nur für diese Ansicht, dem Layer jedes Schnitts (ein vorhandener oder **Neuer Layer…**) und in wie vielen Ansichten jeder ausgeblendet ist. Ein Plan markiert, was seine Ansichten markieren.

Ein Schnitt quer durch einen Balken oder eine Stütze (**Schnitt hier**, **Querschnitt** auf der Registerkarte Einfache Bewehrung) hat auch im Grundriss Griffe: Ziehen Sie ein Ende entlang der Linie, um dieser Seite mehr oder weniger Rand zu geben, und die Mitte entlang des Elements, um die Station zu verschieben, an der er schneidet. Das Fenster des Schnitts folgt.

![Ansicht → Schnitte… für die Südansicht](shots/sections-dialog.webp)

## Ansichten

**+** neben **Ansichten** erstellt die vier Ansichten des Gebäudes auf einmal.

![Die Südansicht](shots/elevation-view.webp)

Jeder Grundriss markiert jede Ansicht vor dem Gebäude, frei von den Köpfen der Schnitte dort: eine feine Strichpunktlinie quer zu ihrer Blickrichtung und in ihrer Mitte ein Kreis mit einem schwarzen Pfeil, der auf das Gebäude zeigt, dahinter der Name der Ansicht. Ein Klick auf den Marker wählt die Ansicht aus, ein Doppelklick öffnet sie. **Ansichtsmarken** in **Sichtbarkeit / Grafiken** (<kbd>VV</kbd>) blendet sie in einer Ansicht aus.

**Wie weit eine Ansicht reicht.** Wählen Sie die Ansicht in einem Grundriss aus: Ihre Linie hat an jedem Ende einen Griff. Ziehen Sie einen entlang der Linie, um die Ansicht auf dieser Seite zu verlängern oder zu verkürzen; die Ansicht zeichnet dann nur, was zwischen den beiden Enden liegt, und ihre Pläne und das PDF folgen. Jedes Ziehen ist ein Rückgängig-Schritt. Solange Sie sie nicht ziehen, umfasst die Linie das ganze Gebäude und wächst mit ihm.

**Den Marker einer Ansicht verschieben.** Ziehen Sie den Griff des Kreises, oder wählen Sie die Ansicht aus und verwenden Sie **Verschieben**: Der Marker geht dorthin, wo Sie ihn hinsetzen, weiter vom Gebäude weg oder entlang seiner Linie, in einem Rückgängig-Schritt. Wie in Revit ist die Linie des Markers dort, wo die Ansicht zu sehen beginnt. Bleibt sie außerhalb des Gebäudes, wo der Marker einer neuen Ansicht steht, sieht sie das ganze Gebäude. In das Gebäude verschoben, zeigt sie nur, was hinter ihrer Linie liegt, und was die Linie schneidet, wird geschnitten gezeichnet, mit den Schnittschraffuren der Materialien, wie in einem Schnitt: eine Innenansicht. Ihre Reichweite entlang der Linie behält eigene Griffe. Eine Ansicht blickt nach Norden, Süden, Osten oder Westen und wird daher weder gedreht noch gespiegelt. Eine fixierte Ansicht oder ein fixierter Schnitt (**Ändern → Fixieren**) wird nicht verschoben, bis er gelöst wird.

![Der Marker der Südansicht ins Haus verschoben: die Ansicht von dort, die Innenwand geschnitten](shots/elevation-clipped.webp)

![Der Marker einer Ansicht im Grundriss, vom Haus weggerückt, ihre Linie verkürzt, der Kopf eines Schnitts vom Ende weggerückt](shots/elevation-marker.webp)

### Sonnenschatten

Eine Ansicht, ein Schnitt oder ein Lageplan kann die Schatten zeichnen, die die Sonne auf das wirft, was er sieht, gefüllt unter seinen Linien, so dass sie auf Plänen, in PDF und DXF gedruckt werden. Gewählt in den Eigenschaften der Ansicht, unter **Sonnenschatten**:

- **eigene Sonne**: eine Sonne der Ansicht. Standard ist die Zeichenkonvention: die Sonne über der linken Schulter, die Strahlen entlang der Diagonale eines Würfels (aus 45°, 35,26° hoch), so ist ein Schatten so tief, wie das Werfende vorsteht. **Sonne aus** und **Sonnenhöhe** drehen sie; **Zeichenkonvention** stellt sie zurück.
- **Projektsonne**: die Sonne am Ort des Projekts (Verwalten → Standort) an einem Tag und zu einer Uhrzeit der Ansicht. **Wie in Sonne und Schatten** übernimmt Tag und Uhrzeit aus Ansicht → Sonne und Schatten.

Ein Schnitt wirft und empfängt nur Schatten dessen, was hinter seiner Schnittebene liegt. Ein Geschossgrundriss zeichnet keine Schatten: seine auf Grundrisshöhe geschnittenen Wände würden sie zu kurz werfen.

![Die Südansicht mit dem Schatten des Daches](shots/elevation-shadows.webp)

![Die Sonnenschatten einer Ansicht](shots/view-shadows.webp)

### Oberflächenschraffuren

Eine Ansicht oder ein Schnitt schraffiert jede Fläche, die sie sieht, mit der **Elevation**-Schraffur ihres Materials (**Verwalten → Materialien**, Registerkarte **2D graphics**): Muster, Maßstab, Winkel, Stift und Füllung. Schraffiert wird nur der Teil einer Fläche, den kein näheres Element verdeckt, so zeigt sich der Putz oder Ziegel einer Fassade um ihre Türen und Fenster, und ein Schnitt schraffiert, was er jenseits seiner Schnittebene sieht, nicht hinter den Elementen, die er schneidet. Eine mehrschichtige Wand zeigt das Material ihrer äußersten Schicht. Die Schraffur liegt unter den Linien, wird also auf Plänen, im PDF und im DXF gedruckt (Layer SURFACE-HATCH und SURFACE-FILL).

Mit **Alignment** *view* läuft das Muster von einer Fläche zur nächsten weiter; mit *layer* liegt es auf der Fläche selbst, entlang ihrer Waagerechten ab ihrer unteren Ecke, und eine schräg gesehene Fläche zeigt es verkürzt. Ein Material, das in einer Ebene über mehrere Elemente weiterläuft, wird als eines schraffiert, sofern **Join materials** im Ansichtsstil eingeschaltet ist.

Bis Sie eine wählen, kommt die Ansichtsschraffur eines Materials **aus dem Namen**, wie seine Schnittschraffur, und **Verwalten → Materials** zeigt welche (*aus dem Namen (BRICK)*): Ziegel und Mauerwerk BRICK, Naturstein STONE, Holz BOARDS, Metallverkleidung VERTICAL, Dachziegel, Schindeln und Schiefer HORIZONTAL, Fliesen TILES, Kies und Erde wie im Schnitt. Dämmung, Bahnen, Putz, Farbe und Beton bekommen keine: das BATT der Dämmung ist eine Schnittschraffur und wird für Flächen nie vorgeschlagen (von Hand gewählt, wird es gezeichnet). Wählen Sie **none**, damit nichts gezeichnet wird. Ein abgewickelter Schnitt (versetzt, gekrümmt oder entlang eines Elements) schraffiert jedes seiner Stücke zwischen dessen Enden und zeichnet dort ebenso die Sonnenschatten. **Surface hatches** im Ansichtsstil (**Ansicht → View settings**, **Verwalten → View styles**) schaltet sie für ganze Ansichten aus: an in Architectural, aus in Structural, MEP und Coordination. Grundrisse behalten ihre eigenen Schnitt- und Grundrissschraffuren. Die Deckung eines Daches wird nach ihrem Material schraffiert: Ein Ziegeldach zeigt seine Reihen auf den Dachflächen, die die Ansicht sieht, und keine auf den abgewandten.

![Die Südansicht, der Verblender nach seinem Namen schraffiert](shots/elevation-surface-hatch.webp)

![Ein versetzter Schnitt schraffiert die Gartenmauer, die sein letztes Stück sieht](shots/section-developed-surface-hatch.webp)

![Oberflächenschraffuren in den Einstellungen der Ansicht](shots/view-surface-hatch.webp)

## 3D-Ansichten

<!-- video section-3d -->

Doppelklicken Sie auf **3D**, um die 3D-Ansicht zu öffnen. Ziehen Sie mit der linken Maustaste, um das Modell zu umkreisen. Der Übersichtsplan oben rechts zeigt, von wo aus Sie blicken. **Grundriss**, **Persp** und **Einpassen** ändern die Kamera. Um eine Kamera zu behalten, duplizieren Sie die 3D-Ansicht.

Das Mausrad zoomt zum Punkt des Modells unter dem Cursor, der auf dem Bildschirm an seiner Stelle bleibt; der Zoom hält kurz vor diesem Punkt an und geht nie durch ihn hindurch. Ziehen mit der linken Maustaste dreht die Ansicht um die Mitte der ausgewählten Elemente, die auf dem Bildschirm an ihrer Stelle bleibt, sodass Sie sie von allen Seiten sehen; ist nichts ausgewählt, dreht sie sich um den Punkt des Modells unter dem Cursor, an dem das Ziehen beginnt, und um die Mitte der Ansicht, wenn unter dem Cursor nichts ist. **Ansicht → Zoom zum Cursor** und **Ansicht → Um Auswahl orbitieren** schalten beides einzeln aus (das Mausrad zoomt dann zur Mitte der Ansicht, und die Ansicht dreht sich um ihre Mitte), ebenso die Kontrollkästchen in den Eigenschaften einer 3D-Ansicht. Die Wahl bleibt auf diesem Computer gespeichert.

### Röntgen, oder eine durchsichtige Ansicht

View → **Visual style** schaltet durch schattiert, schattiert mit Kanten, **X-ray** und Drahtmodell; View → **X-ray** schaltet das Röntgenbild direkt ein und aus. Im Röntgenbild sind alle Flächen des Modells durchsichtig gezeichnet, die Kanten bleiben, und man sieht durch Wände und Decken, was dahinter liegt. Was ausgewählt ist, bleibt deckend und hervorgehoben, und ein Klick wählt weiter das Element unter dem Cursor. Der Stil gehört zur 3D-Ansicht, nur zum Ansehen: Modell, Zeichnungen und Exporte ändern sich nicht. Sollen nur einige Kategorien durchsichtig werden (das Dach, die Decken, die Wände), stellen Sie ihre Transparenz je Kategorie in **Visibility / Graphics** (<kbd>VV</kbd>) ein; sie reicht auch in Pläne und Exporte.

![Ein Haus im Röntgenbild, eine Wand ausgewählt](shots/view-xray.webp)

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

### Realistisch, mit der Sonne

Ansicht → **Anzeigequalität** legt fest, wie die 3D-Ansicht dargestellt wird, auf diesem Rechner:

- **Zeichnung**: die Farben des Plans und gleichmäßiges Licht, wie Lapin immer gezeichnet hat. Schnell auf jedem Rechner.
- **Mittel**: die Schatten der Sonne, Schatten in den Ecken, wo Flächen zusammenstoßen, und Licht vom Himmel.
- **Realistisch**: der Himmel hinter dem Modell und ein Gelände darunter (wo das Projekt kein Gelände hat), und Glas, durch das Licht fällt. Zum Präsentieren, mit einer guten Grafikkarte.

![Ein Haus in der realistischen Ansicht](shots/view-realistic.webp)

Die Sonne ist die des Projekts: wo sie für den Ort des Projekts (Verwalten → **Standort**) an einem Tag zu einer Uhrzeit steht. Ansicht → **Sonne und Schatten** legt den Tag (Tagundnachtgleiche und Sonnenwenden mit einem Klick) und die Uhrzeit fest, spielt den Tag ab und schaltet die Sonne auch in der Zeichnungsansicht ein. Ohne Koordinatensystem und Vermessungspunkt ist der Ort Bukarest. Die Zeitzone folgt der Länge des Ortes, mit europäischer Sommerzeit in Europa.

![Sonne und Schatten](shots/sun-dialog.webp)

Die realistische Ansicht übernimmt einige Zahlen (den Schatten in Ecken, die Schattenqualität, die Farbe der Sonne) von [BimGo](https://github.com/aussieBIMguru/BimGo) von Aussie BIM Guru (MIT-0).

## Modellieren in 3D

Die 3D-Ansicht ist ein Ort zum Modellieren, nicht nur zum Ansehen: Elemente werden dort wie im Grundriss verschoben und kopiert und rasten an Ecken, Kanten und Flächen der anderen Elemente ein.

### Verschieben und Kopieren in 3D

Wählen Sie die Elemente aus, dann **Move** (M) oder **Copy** (CO), während die 3D-Ansicht vorne ist. Klicken Sie den Basispunkt an: eine Ecke, eine Kante oder eine Fläche dessen, was Sie verschieben. Der Cursor zeigt, woran er einrastet: **Endpoint** (Endpunkt), **Midpoint** (Mittelpunkt), **On edge** (auf Kante), **On face** (auf Fläche) oder **On level** (auf der Ebene) im leeren Raum; was hinter der Fläche unter dem Cursor liegt, wird nicht genommen. Auch die Linien einer importierten DXF, die in 3D gezeigt wird, fangen. Klicken Sie dann, wohin es geht, etwa auf die Ecke einer Decke: Die Auswahl folgt dem Cursor in 3D, auch nach oben und unten, und wird dort abgesetzt. Vom Basispunkt aus werden die rote, grüne und blaue Achse erkannt, wenn der Cursor einer folgt; → ← ↑ sperren die rote, grüne oder blaue Achse (dieselbe Taste löst sie), Umschalt hält die erkannte Achse. Bei gesperrter Achse wird eine Ecke unter dem Cursor auf die Achse gebracht, damit die Verschiebung mit ihr fluchtet, ohne dorthin zu gehen.

Geben Sie jederzeit einen Wert ein, ohne in ein Feld zu klicken: Eine Länge geht in Richtung des Cursors (1200), dx,dy oder dx,dy,dz ist die Verschiebung selbst (0,0,450 hebt um 450 mm). Enter wendet sie an, Esc bricht ab. Copy kopiert vom selben Basispunkt weiter bis Enter oder Esc.

Was eine eigene Höhe hat, geht mit der Verschiebung hoch oder runter: der Fuß einer Wand, einer Stütze oder eines Objekts, Fuß und Kopf einer Treppe (ihre Steigung bleibt), die Oberkante eines Trägers, der obere Versatz einer Decke, die Fußpfette eines Dachs; ein Studio-Körper erhält einen Verschiebeschritt. Eine Tür oder ein Fenster gleitet entlang seiner Wand.

Was auf seiner Ebene steht (eine Wand, eine Treppe, eine Stütze, eine Decke, ein Träger, ein Geländer, ein Dach, ein Möbel, eine Tür oder ein Fenster), behält seine Ebene, wenn die Verschiebung von einem Punkt zu einem anderen einrastet: Es geht nur im Grundriss dorthin, sodass eine Treppe, die an ihrer oberen Ecke auf die Ecke einer Decke verschoben wird, nicht um die wenigen Zentimeter zwischen der Treppenkonstruktion und dem Belag der Decke steigt (der Treppenkopf liegt schon bündig mit dem Belag der Ebene, zu der sie führt). Um sie nach oben oder unten zu bewegen, folgen oder sperren Sie die blaue Achse (↑) oder geben dx,dy,dz ein; der Chip sagt, wann die Ebene gehalten wird. Ist nichts ausgewählt, fragen Move und Copy nach den Elementen: Klicken Sie sie in 3D an und drücken Sie Enter.

Ein Ziehen in 3D dreht immer die Ansicht, auch wenn der Cursor auf einem Element steht. Um ein ausgewähltes Element durch Ziehen zu verschieben, drücken Sie darauf und halten kurz still (der Cursor wird zum Verschiebe-Cursor), dann ziehen Sie: Es wird ebenso verschoben, vom gedrückten Punkt aus, eingerastet, und dort abgesetzt, wo Sie loslassen. **View → Drag to move in 3D** schaltet das aus, sodass Ziehen auf der Auswahl die Ansicht auch nach dem Halten dreht.

![Eine Treppe in 3D an der Ecke der obersten Stufe auf die Ecke der Decke verschoben](shots/model-3d-move.webp)

### Elemente in 3D platzieren

Jedes Werkzeug, das im Grundriss Punkte nimmt, funktioniert auch in der 3D-Ansicht: Wände, Vorhangfassaden, Decken, Dächer, abgehängte Decken, Deckenöffnungen, Räume, Stützen, Träger, Fundamente, Geländer, Sockelleisten, Möbel, Aufzüge, Straßen, Pflanzen und die übrigen. Die Punkte liegen auf der aktiven Ebene: Der Cursor rastet am Modell ein wie beim Verschieben (Ecke einer Decke, Ende eines Trägers, Mitte einer Kante, Linien einer DXF), und der Punkt wird senkrecht darunter auf der Ebene genommen; eine gestrichelte Linie zeigt wo, wenn der Fang darüber oder darunter liegt. Die Punkte und ihr Umriss werden beim Zeichnen auf der Ebene dargestellt: die Linie einer Wand, der Umriss einer Decke, der sich zum ersten Punkt schließt, ein Bogen durch seinen dritten Punkt. Vom letzten Punkt aus werden die rote und grüne Achse erkannt; → und ← sperren sie.

Geben Sie jederzeit einen Wert ein: Eine Länge geht vom letzten Punkt in Richtung des Cursors (3000, oder 3000<30 mit Winkel), Breite,Höhe eines Rechtecks von seiner ersten Ecke. Enter wendet sie an, schließt eine Decke oder beendet ein Geländer; die Rücktaste nimmt den letzten Punkt zurück; Esc verwirft die Punkte und beendet beim zweiten Mal das Werkzeug. Ziehen dreht weiterhin die Ansicht, sodass Sie zwischen zwei Klicks um das Modell drehen können. Der Chip unten zeigt, auf welche Ebene die Punkte gehen.

Türen und Fenster kommen dorthin, wo Sie in 3D auf die Fläche einer Wand klicken, an die angeklickte Stelle entlang der Wand.

![Eine Wand in 3D an der Ecke der Decke begonnen, die grüne Achse vom Anfang erkannt](shots/model-3d-wall.webp)

### Griffe und Bauteile in 3D

Die Griffe eines ausgewählten Elements (die Enden einer Wand, die Ecken einer Decke) werden in 3D wie im Grundriss gezogen und rasten wie bei Move ein: zuerst eine Ecke, dann eine Mitte, dann eine Kante in Reichweite, dann die rote und grüne Achse durch den Ausgangspunkt des Griffs, dann eine Fläche. Der Punkt wird am Cursor markiert und benannt und in der Höhe des Griffs genommen. Geben Sie beim Ziehen eine Länge oder einen Winkel ein, wie im Grundriss; Enter setzt den Griff dorthin.

Ziehen Sie eine Karte aus dem **Catalog** oder ein Werkzeug aus dem Menüband auf die 3D-Ansicht: Sein Werkzeug startet mit diesem Typ. Ein Werkzeug, das mit einem Klick platziert (eine Stütze, ein Einzelfundament, Möbel, ein Aufzug, eine Pflanze), wird sofort dort abgesetzt, wo Sie es loslassen, am Modell eingerastet. Auf eine Decke eines anderen Geschosses gezogen oder geklickt, kommt das Element auf dieses Geschoss: Ein Tisch auf der Decke des Obergeschosses gehört zum Obergeschoss, welche Ebene auch aktiv ist.

## Was eine Ansicht zeigt

Die Registerkarte **Ansicht** legt fest, was die vorne liegende Ansicht zeigt und wie sie aussieht.

**Grid** (<kbd>F7</kbd>) blendet das Hintergrundraster des Grundrisses aus oder ein, und **Dünne Linien** (<kbd>TL</kbd>) zeichnet jede Linie ein Pixel dick, um eine dichte Zeichnung zu lesen. Beides gilt nur für den Bildschirm: Pläne, PDF und DXF behalten ihre Stifte, und das Fangen am Raster bleibt. Das Bodenraster der 3D-Ansicht folgt demselben Schalter.

![Die Registerkarte Ansicht](shots/ribbon-view.webp)

- **Ansichtseinstellungen.** Die Disziplin und der Stil der Ansicht, ihr Bereich (die Schnitthöhe und -tiefe) und ihre Grafiken.
- **Ansichtsstile.** Architektur, Tragwerk (nur die Kerne), TGA oder Ihr eigener. Jeder Stil wählt die gezeichneten Schichten (alle, ohne Bekleidungen oder den Kern) und wie Materialien sich verbinden.
- **IFC-2D-Symbole.** Aus einer IFC-Datei importierte oder verknüpfte Elemente, die ihre eigene Grundrisszeichnung mitbringen (eine Grundfläche, den Aufschlag einer Tür, das Symbol eines Objekts, mit den Farben, Füllungen und Texten der Datei), zeichnen diese in Grundrissen statt ihres Schnitts. Entfernen Sie das Häkchen in den Ansichtseinstellungen (oder in einem Ansichtsstil), um stattdessen ihren 3D-Schnitt zu zeichnen.
- **Sichtbarkeit** (<kbd>VV</kbd>) und **Grafiken** (<kbd>VG</kbd>). Sie blenden Kategorien ein oder aus und legen ihre Stifte, Linientypen und Füllungen fest.
- **Teilen** und **Fenster.** Sie zeigen zwei Ansichten nebeneinander oder öffnen eine Ansicht in einem eigenen Fenster.
- **Anzeigequalität.** Wie die 3D-Ansicht dargestellt wird (**Zeichnung**, **Mittel** oder **Realistisch**, auf diesem Rechner gespeichert: siehe *Realistisch, mit der Sonne*) und wie die 3D-Ansicht Teile mit vielen Dreiecken (eine importierte Anlage, Möbel, Rohre) zeichnet, wenn sie weit weg sind. **Ausgewogen** zeichnet ein entferntes Teil nur dort gröber, wo der Unterschied unter einem Pixel liegt. **Schnell** lässt es bis zu 3 Pixel abweichen, für große Modelle und langsamere Rechner. **Genau** zeichnet jedes Teil so, wie es ist. Die Einstellung gehört zum Projekt. Das Modell, seine Zeichnungen, Exporte und Mengen werden nie gröber: Eine Kurve besteht aus geraden Stücken, die höchstens einen Millimeter von ihr abweichen.
- **Sonne und Schatten.** Die Sonne des Projekts an einem Tag zu einer Uhrzeit, die die 3D-Ansicht mit ihren Schatten beleuchtet (siehe *Realistisch, mit der Sonne*).

![Anzeigequalität](shots/display-quality.webp)

### Sichtbarkeit / Grafiken

**Sichtbarkeit** (<kbd>VV</kbd>) oder **Grafiken** (<kbd>VG</kbd>) öffnet Sichtbarkeit / Grafiken der vorderen Ansicht: ein Grundriss, ein Schnitt, eine Ansicht oder eine 3D-Ansicht. Was sie ausblendet, ist auch auf den Plänen der Ansicht, in ihrem PDF und DXF und in einem 3D-Export ausgeblendet.

- **Modellkategorien.** Jede Kategorie wird ein- oder ausgeblendet, mit Linienfarbe, Linienstärke, Füllung und Schnittschraffur. Der Pfeil neben einer Kategorie öffnet ihre Unterkategorien: **Kern**, **Ausbau**, **Dämmung**, **Bahnen** und **Andere Schichten** einer Wand, einer Decke oder eines Dachs (nach dem Material jeder Schicht und seiner Kategorie unter Verwalten → Materialien), die **Laibungen** einer Tür oder eines Fensters. Entfernen Sie **Ausbau** unter **Wände**, und der Putz verschwindet, das Mauerwerk bleibt. Bewehrung, Raumbekleidungen, Schalung, Gerüste, TGA, Stahlanschlüsse, Aufzüge und Sonnenschutz sind eigene Kategorien.
- **Beschriftungskategorien.** Bemaßungen, Texte, Beschriftungen, Raumbeschriftungen, Raster, Ebenen, Schnitt- und Ansichtsmarken, Detailausschnitte, Referenzebenen, Ansichtsgrenzen und Legenden, jeweils in dieser Ansicht ein- oder ausgeblendet.
- **Importierte Kategorien.** Jede in die Ansicht importierte DXF, nach ihrer Datei: angezeigt, halbtonig und ihre Layer einzeln (der Pfeil daneben). Ein PDF oder Bild zum Nachzeichnen wird hier ebenfalls ein- oder ausgeblendet.
- **Filter.** Regelbasiert, wie in Revit: **Neuer Filter**, dann seine Regeln (IFC-Klasse, Kategorie, Typ, Name, Material, Geschoss, Gruppe, Verknüpfung, Erstellungsphase, vordefinierter Typ oder ein Parameter wie *Feuerwiderstand* oder *Pset_WallCommon.IsExternal*; ist gleich, enthält, beginnt mit, größer, kleiner, hat einen Wert…), alle oder eine davon. Die Ansicht verwendet die Filter ihrer Liste der Reihe nach; der erste, der ein Element erfasst, entscheidet, ob es angezeigt wird. **Verwalten → Ansichtsfilter** öffnet diese Registerkarte.
- **Verknüpfungen.** Jede verknüpfte Datei, mit **Sichtbar** und **Halbton** nur für diese Ansicht. **Anzeige** ist **Wie Hostansicht** (die Verknüpfung übernimmt die Kategorien dieser Ansicht) oder **Benutzerdefiniert**: dann gibt **Kategorien…** der Verknüpfung in dieser Ansicht eigene Kategorien, Unterkategorien und IFC-Klassen. Der Grundriss der Architektin blendet die Bewehrung der Tragwerksverknüpfung aus; der des Ingenieurs den Ausbau der Architekturverknüpfung.
- **IFC-Klassen.** Die Klassen der eigenen Elemente des Projekts, ein- oder ausgeblendet.
- **Grafiküberschreibungen.** Eine Kategorie erhält außerdem ein **Muster** für ihre Schnitt- und Projektionslinien, **Halbton** und eine **Transparenz** (in 3D, von 0 deckend bis 95); eine Beschriftungskategorie eine **Farbe** und **Halbton**; ein Filter eine **Farbe**, ein **Muster**, **Halbton** und eine **Transparenz** für das, was er erfasst, wobei der erste Filter der Liste jedes Feld vorgibt. **Detaillierungsgrad** (grob, mittel, fein) legt fest, wie viel die Symbole eines Grundrisses zeichnen, sonst nach dem Maßstab.
- **Detaillierungsgrad (LOD).** Eine Sichtweise, nie vom Maßstab abgeleitet: eine Ansicht zeichnet auf dem Grad, den ihr Stil ihr gibt (Verwalten → Ansichtsstile: 350, ein Tragwerksstil 400), oder auf ihrem eigenen (Ansicht → Ansichtseinstellungen, oder oben in Sichtbarkeit / Grafik, auch für eine 3D-Ansicht). **LOD 100** (Volumen) zeichnet Wände, Decken und Dächer als einen Umriss, schraffiert wie ihr Kern, Stahlprofile als eine Linie auf ihrer Achse und eine Tür oder ein Fenster als Öffnung und Aufschlag; **LOD 200** (Schichten) fügt die Schichten hinzu und Rahmen und Flügel von Türen und Fenstern vereinfacht (ohne Sprossen, ohne Beschläge); **LOD 300** (Details) ihre vollen Profile und Details (Laibungen, Fensterbänke, Faschen, Schwellen, Innenfensterbänke, Beschläge, Läden, Gitter) und die Auskleidung der Öffnungen; **LOD 350** (Anschlüsse) Bleche, Schrauben und Anker der Stahlanschlüsse; **LOD 400** (Fertigung) die Bewehrungsstäbe eines verknüpften Tragwerksmodells und den wirklichen Querschnitt der Stahlprofile: Ausrundungen, Flanschspitzenradien, geneigte Flansche. Ein früher erstelltes Projekt behält seine Zeichnungen: jede Ansicht erhält den Grad, den ihr Maßstab ihr damals gab (1:50 LOD 400, 1:100 LOD 200, 1:200 LOD 100), eine 3D-Ansicht LOD 400. Wände und Decken einer verknüpften oder importierten IFC werden nach den Schichtaufbauten geteilt, die ihre Datei ihnen gibt, sodass ihre **Bekleidungen**, **Dämmung** und ihr **Kern** wie bei einer nativen Wand ausgeblendet werden und die Nur-Kern-Darstellung des Tragwerk-Stils sie erreicht; der Putz einer Wand als eigenes Bauteil oder eine Bekleidung aus einem Material gehört zu **Bekleidungen**. Jede 3D-Ansicht lässt die **Bewehrung** weg, bis sie eingeblendet wird.

Die Einstellungen gehören der Ansicht oder einem benannten Satz, den mehrere Ansichten teilen (**Als Satz speichern…**). Ein Ansichtsstil kann einen Satz nennen (Verwalten → Ansichtsstile): eine Ansicht ohne eigene Einstellungen übernimmt den Satz ihres Stils, wie Revits Ansichtsvorlagen; **Zu eigenen dieser Ansicht machen** beginnt ihre eigenen daraus.

![Sichtbarkeit / Grafiken: der Ausbau einer Wand ausgeblendet](shots/vg-categories.webp)

![Ein verknüpftes Tragwerk, benutzerdefiniert in einer Ansicht](shots/vg-links.webp)

![Ein Filter, der ausblendet, was seine Regeln erfassen](shots/vg-filters.webp)

![Beschriftungskategorien](shots/vg-annotations.webp)

![Ein DXF-Import nach Layern](shots/vg-imports.webp)

![Farbe und Muster eines Filters im Grundriss](shots/vg-overrides.webp)

![Der Grundriss mit seinem Filter](shots/vg-plan.webp)

![Ein Grundriss auf LOD 350 seines Stils: die Schichten der Wände und die Details des Fensters](shots/detail-fine.webp)

![Derselbe Grundriss auf LOD 100: ein Umriss, schraffiert wie der Kern](shots/detail-coarse.webp)

### Punktwolken in Ansichten

Eine verknüpfte Punktwolke (*Import und Export*, Punktwolken) erscheint in jeder Ansicht. Ein **Grundriss** zeichnet die Scheibe, die sein Schnitt nimmt, 10 cm um die Schnittebene, als Punkte unter den Wänden; ein **Schnitt** die Scheibe direkt hinter seiner Ebene; eine **Ansicht** jeden Punkt, auf den sie blickt, den nächsten vorn. Die Punkte tragen die Farben des Scans, abgedunkelt, damit sie auf Papier lesbar sind, und werden auf Plänen und im PDF so gedruckt, wie der Bildschirm sie zeigt. Ihre Punkte sind Fangpunkte: Wände über der Scheibe eines Scans gezeichnet landen auf seinen Flächen. In der **3D-Ansicht** fangen die Platzierwerkzeuge und Move die Punkte der Wolke wie Ecken. Ein neuer Grundriss öffnet sich auf das Modell eingepasst, oder auf die Scheibe der Wolke, wenn er sonst nichts hat; gezeichnet wird nur der Teil der Scheibe auf dem Bildschirm, sodass der Scan eines ganzen Viertels beim Zoom auf ein Gebäude schnell bleibt.

**Visibility / Graphics** (<kbd>VV</kbd>) hat einen Reiter **Point clouds**: je Wolke, nur für diese Ansicht, **Shown**, **Halftone**, ihre **Colours** (wie ihre Verknüpfung, RGB, Intensität, Höhe oder Klassifizierung), die **Slice**, die ein Grundriss oder ein Schnitt zeichnet (cm, bis 100 m: die Fassaden eines Luftscans brauchen eine dicke Scheibe), und **Classes…**: die ASPRS-Klassen ihrer Punkte (Boden, Vegetation, Gebäude, Rauschen…), jede sichtbar oder ausgeblendet. Der Reiter einer 3D-Ansicht bestimmt, was das 3D zeichnet. **Export 3D…** nimmt eine Stichprobe der sichtbaren Wolken (höchstens 5 Millionen Punkte) als Punkte in GLB und glTF, wenn **Point clouds** angehakt ist; kein anderer Export trägt sie.

![Die Scheibe eines Scans im Grundriss, eine Wand darüber gezeichnet](shots/point-cloud-plan.webp)

![Visibility / Graphics: die Vegetation einer Wolke in einer Ansicht ausgeblendet](shots/point-cloud-vg.webp)
