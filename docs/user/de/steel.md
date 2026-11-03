---
title: Stahl
order: 56
summary: Stahlstäbe mit einstellbaren Enden – verlängert oder zurückgesetzt, nach einer Ebene geschnitten, auf Gehrung, an einen anderen Stab angepasst, ausgeklinkt –, auch aus einem gezeichneten Umriss; Hohl- und kaltgeformte Profile.
---

## Der Reiter Stahl

![Der Reiter Stahl](shots/ribbon-steel.webp)

- **Stab** (<kbd>SM</kbd>): ein Stahlstab zwischen zwei Punkten, auf der Oberkante des Geschosses, die Oberkante des Querschnitts auf der Achse, wie ein Träger. Sein Typ wird vor dem ersten Klick in den Eigenschaften gewählt, oder im Reiter **Katalog**, wo jeder Stahltyp mit seinem Querschnitt gezeichnet ist.
- **In Stab umwandeln**: die gewählten gezeichneten Umrisse werden Stäbe (unten).
- **Gehrung** und **Einpassen**: zwei Stäbe an ihren Enden verbunden (unten).
- **Stahltypen**: die Stabtypen des Projekts, und neue.

Ein Stab ist ein IfcMember: seine Achse liegt beliebig im Raum (eine Diagonale, ein Sparren, ein Pfosten), sein Querschnitt kommt von seinem Typ, er lässt sich um seine Achse drehen (**Drehung**), und die Achse kann durch die Mitte, die Oberkante oder die Unterkante des Querschnitts laufen (**Einfügepunkt**). Die Träger und Stützen der Tragwerksplanung aus einem Stahlprofil erhalten dieselben Enden.

## Enden

Wähle einen Stab, oder einen geraden Träger oder eine gerade Stütze aus einem Stahlprofil: der Abschnitt **Stahl** in den Eigenschaften zeigt die zwei Enden, **Anfang** und **Ende**. Jedes Ende ist ein Satz von Regeln; der Körper folgt ihnen.

![Die Enden eines Stabs in den Eigenschaften](shots/steel-ends.webp)

- **Verlängerung**: wie weit das Ende über seinen Punkt reicht; negativ ist das Ende zurückgesetzt (eine Montagefuge).
- **Schnitt**: **Rechtwinklig zur Achse**; **Nach einer Ebene**, gegen den geraden Schnitt gedreht **Im Grundriss** (um die Hochachse des Querschnitts) und **In der Ansicht** (um seine Seite), in Grad; **Gehrung** mit einem anderen Stab, auf der Ebene, die die Ecke zwischen ihnen halbiert; **An einen Stab anpassen**, auf die Fläche des anderen Stabs, zu der er kommt (der Flansch einer Stütze, oder die Seite ihres Stegs, wenn die Stütze gedreht ist).
- **Ausklinkung**: die Flansche **Oben** und/oder **Unten** ausgeklinkt, um die **Länge der Ausklinkung** vom Ende zurück und um die **Tiefe der Ausklinkung** vom Rand des Querschnitts, wie bei einem Nebenträger, der an einen Hauptträger anschließt.

Gehrung und Anpassung nennen den anderen Stab: verschiebe ihn, und das Ende folgt; lösche ihn, und das Ende ist wieder gerade und sagt es. **Gehrung** im Reiter verbindet die zwei gewählten Stäbe auf Gehrung an der Ecke, wo sie sich treffen; **Einpassen** passt den zuerst gewählten an den zweiten an.

## Von der Skizze zum Stab

Zeichne den Umriss des Stabs als geschlossene Polylinie oder Rechteck (**Zeichnen**), dann **Stahl → In Stab umwandeln** mit einem oder mehreren gewählten Umrissen. In einem Schritt wird jeder Umriss gelesen und durch einen Stab ersetzt:

![Zwei im Grundriss skizzierte Umrisse](shots/steel-sketch.webp)

- seine **zwei langen parallelen Kanten** (auf ein halbes Grad genau) ergeben die Achse, ihre Mittellinie von Ende zu Ende, und die **Breite** zwischen ihnen;
- die **Kante über jedem Ende** ergibt den Schnitt dieses Endes: gerade, oder nach einer Ebene mit der Neigung der Kante.

Im **Grundriss** ist der Umriss der Stab von oben gesehen: seine Breite ist die der Flansche, und ein Profil dieser Breite wird unter den Stabtypen des Projekts und im Katalog gesucht (sonst der für Stab gewählte Typ); der Stab liegt auf der Oberkante des Geschosses. In einem **Schnitt** oder einer **Ansicht** ist der Umriss der Stab von der Seite gesehen: seine Breite ist die Höhe des Querschnitts, ein schräges Ende ist ein Schnitt in der Ansicht, und ein rechtwinkliger **Absatz** an einem Ende ist eine **Ausklinkung** oben oder unten; der Stab liegt, wo er gezeichnet ist. Ein im Grundriss gezeichneter Absatz wird abgelehnt (eine Ausklinkung sieht man von der Seite). Ein Umriss, der kein Band ist (ein Dreieck, ein L, nicht parallele Kanten), bleibt, wie er ist, und Lapin sagt es.

## Stahltypen

**Stahl → Stahltypen** listet die Stabtypen des Projekts mit ihrem Metergewicht und macht neue: ein Profil aus dem Katalog (IPE, HEA, HEB, UPN, L, **RHS**, **CHS** und die generischen kaltgeformten **C**, **Z** und **Σ**) oder ein Hohl- oder kaltgeformtes Profil **mit eigenen Maßen** (Höhe, Breite, Dicke, Lippe). Der neue Typ kann ein Stabtyp sein, oder ein **Trägertyp** oder **Stützentyp** der Tragwerksplanung, damit auch die Werkzeuge Träger und Stütze Stahl setzen. Ein Klick auf einen Typ in der Liste macht ihn zu dem, den Stab setzt.

![Der Dialog Stahltypen](shots/steel-types.webp)

Hohlprofile sind innen hohl, in der 3D-Ansicht, in den Schnitten und in den Mengen. Die kaltgeformten Profile sind generische Formen mit Maßen, scharf gekantet, ohne Herstellerkatalog.

![Stäbe in 3D](shots/steel-3d.webp)

## IFC und Mengen

Ein Stab wird als **IfcMember** mit seinem **IfcMemberType** exportiert. Sein Körper wird so geschrieben, wie die anderen Programme Stahl schreiben (Tekla, Advance Steel, Revit): sein Querschnitt **extrudiert** (IfcExtrudedAreaSolid), **beschnitten** von den Ebenen seiner Enden (IfcBooleanClippingResult mit IfcHalfSpaceSolid), und jede Ausklinkung ein **IfcOpeningElement**, das ihn ausspart. Ein Stahlträger oder eine Stahlstütze mit Enden wird genauso geschrieben. Die Mengen jedes Stabs geben seine Länge zwischen den Schnitten, die Fläche seines Querschnitts, sein Volumen und sein Nettogewicht (7850 kg/m³).
