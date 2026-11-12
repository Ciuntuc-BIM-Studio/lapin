---
title: Tragwerk
order: 50
summary: Stützen, Träger und Fundamente, ihre Typen und wie sie aufeinandertreffen.
---

Die Registerkarte **Tragwerk** enthält das Skelett und die Gründung.

![Die Registerkarte Tragwerk](shots/ribbon-structure.webp)

## Skelett

- **Stütze** (<kbd>CL</kbd>): Klicken Sie dort, wo sie steht. Eine Stütze reicht von ihrer Ebene bis zur Ebene darüber.
- **Träger** (<kbd>BM</kbd>): Klicken Sie seine beiden Enden an. **Gebogener Träger** (<kbd>BC</kbd>) folgt einem Bogen.
- **Stützen am Raster** setzt eine Stütze an jeden Schnittpunkt der Rasterachsen. Die Achsen zeichnen Sie mit **Beschriften → Rasterachse** (<kbd>GR</kbd>).

## Gründung

- **Streifenfundament** (<kbd>SF</kbd>) verläuft unter Wänden. Seine äußeren Schichten werden an den Ecken auf Gehrung gestoßen.
- **Gebogenes Streifenfundament** (<kbd>CF</kbd>) folgt einem Bogen.
- **Einzelfundament** (<kbd>PF</kbd>) kommt unter eine Stütze.

Zeichnen Sie die Gründung auf der Ebene **Foundations**, oder geben Sie ihr einen eigenen Grundriss.

**Ein Streifenfundament ist nur der Beton**, solange Sie die Wand nicht verlangen. Ist **Streifenfundament** oder **Gebogenes Streifenfundament** aktiv, bietet die Leiste der Befehlszeile **Nur das Fundament** (Standard: ein Betonfundament 600 × 400 mm, ein Profiltyp, der beim ersten Mal angelegt wird) oder **Mit Wand** (der Sockel eines Streifenfundament-Typs darauf, mit seinen Schichten) und welchen Streifenfundament-Typ mit Wand es nimmt. Die Wahl bleibt erhalten. Ein schon gezeichnetes Fundament ändert sich mit seinem Typ in **Tragwerkstypen**.

![Die Option des Streifenfundament-Werkzeugs in der Befehlszeile: nur das Fundament oder mit Wand](shots/structure-strip-option.webp)

**In den Grundrissen.** Ein Fundament liegt unter der Decke, darum zeichnet ein Grundriss es gestrichelt im Grundriss der Ebene, auf der es steht, wie ein Gebäudegrundriss zeigt, was darunter liegt. Um es geschnitten und gesehen zu sehen, nehmen Sie einen **Fundamentgrundriss**: im Projektbrowser legt das **+** neben **Grundrisse** einen für die aktive Ebene an (0,50 m darunter geschnitten, 2,50 m tief gesehen).

**Zeichnen in einem Fundamentgrundriss.** Er zeigt auch die Ebene darunter, und was Sie dort mit einem Decken-, Wand-, Stützen- oder Trägerwerkzeug zeichnen, kommt auf diese Ebene (die Gründung eines neuen Projekts, bei −1,50); die Fundamentwerkzeuge behalten die Ebene des Grundrisses, die Unterkante eines Fundaments ist die seines Typs darunter.

![Stützen, Träger und Einzelfundamente in 3D](shots/structure-3d.webp)

## Typen und Ecken

**Tragwerkstypen** legt die Querschnitte von Stützen, Trägern und Fundamenten fest: Betonquerschnitte und Stahlprofile. Der Querschnitt einer Stütze ist ein **Rechteck**, ein **Kreis** oder ein **Polygon**, ein regelmäßiges, gegeben durch seine **Seiten** und seinen **Durchmesser** über die Ecken (ein achteckiger Pfosten, wie ihn ein importiertes IFC haben kann). Klicken Sie auf einen Typ in der Liste, um seine Größe oder seinen Namen zu ändern, dann **Typ speichern**: alle Elemente dieses Typs folgen; **Neuer Typ** führt zurück zum Anlegen eines Typs.

Träger und Fundamente verbinden sich von selbst, wo sie aufeinandertreffen, eine Ecke als Gehrung. Ein von Stützenachse zu Stützenachse gezeichneter Träger endet an den Stützenflächen; seine Länge, sein Volumen und seine Bügel zählen die lichte Weite dazwischen. **Gehrung**, **Stumpf** und **Tauschen** ändern, wie zwei Träger oder zwei Fundamente aufeinandertreffen. **Eigenschaften übertragen** (<kbd>MA</kbd>) überträgt Typ und Einstellungen eines Elements auf andere, und **Ähnliches erstellen** (<kbd>CS</kbd>) beginnt, ein weiteres Element desselben Typs zu zeichnen.

Jedes andere Profil kommt aus den Bibliotheken: **Aus einer Profilbibliothek…** durchsucht sie alle (siehe [Profilbibliotheken](profiles.md)).

## Streifenfundamente mit Profil

Ein Streifenfundamenttyp ist entweder der Standardtyp, eine Sockelwand mit ihren Schichten auf einem Fundament, oder ein beliebiges Profil, das entlang der Fundamentachse geführt wird, wie die Fundamentprofile in Revit. Wählen Sie in **Tragwerkstypen → Strip** **Form: Profil** und ein Profil: eines aus dem Katalog (rechteckig, in 2 oder 3 Stufen, mit geneigter Oberseite, exzentrisch mit der Sockelwand an einem Rand, mit Sockelwand) mit seinen Maßen, oder eines des Projekts (**Verwalten → Profile**, wo Sie auch seine Eckpunkte eingeben oder eine gezeichnete Kontur einlesen können). Der Einfügepunkt des Profils liegt auf der Fundamentachse in der Höhe **Unten** des Typs.

Fundamente mit Profil verlaufen gerade oder gekrümmt. Zwei, die sich in einer Ecke treffen, werden auf Gehrung geschnitten, und eines, das an einem anderen endet, läuft in dieses hinein und wird von dessen Körper geschnitten, sodass jede Stufe auf die Stufe des anderen trifft. Grundrisse, Schnitte und Ansichten schneiden den geführten Körper. Die Mengen geben seine Länge, seine Querschnittsfläche und sein Volumen an, und seine senkrechten Seiten sind die Flächen, die die Baustelleneinrichtung einschalt. In IFC ist ein gerades Fundament ein IfcFooting STRIP_FOOTING, dessen Körper sein extrudiertes Profil ist (ein IfcArbitraryClosedProfileDef), von den Ebenen der Ecken geschnitten; ein gekrümmtes wird als seine Flächen geschrieben. Wählen Sie **Standard**, um zur Sockelwand auf dem Fundament zurückzukehren.

![Gestufte Streifenfundamente unter einem Haus, an den Ecken auf Gehrung](shots/footing-profile-3d.webp)

![Ein Streifenfundamenttyp mit Profil](shots/footing-profile-type.webp)

## Eine Tragwerksansicht

Die Disziplin einer Ansicht entscheidet, was sie zeigt. Eine Ansicht der Disziplin **Tragwerk** zeigt nur die tragenden Kerne, ohne die Bekleidungen. Die Disziplin wählen Sie in den Eigenschaften der Ansicht. Siehe [Schnitte, Ansichten und 3D](views.md).

## Lasten

Die Gruppe **Lasten** enthält, was eine Tragwerksberechnung braucht: Lastfälle, Flächen-, Linien- und Punktlasten und die Kombinationen. Siehe [Lasten](loads.md).
