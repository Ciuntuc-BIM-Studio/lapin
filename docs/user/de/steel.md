---
title: Stahl
order: 56
summary: Stahlstäbe mit einstellbaren Enden – verlängert oder zurückgesetzt, nach einer Ebene geschnitten, auf Gehrung, an einen anderen Stab angepasst, ausgeklinkt –, auch aus einem gezeichneten Umriss; Hohl- und kaltgeformte Profile; Rahmenhallen mit Vouten.
---

## Der Reiter Stahl

![Der Reiter Stahl](shots/ribbon-steel.webp)

- **Rahmenhalle…**: eine ganze Stahlhalle aus wenigen Zahlen, im Hallenkonfigurator (unten).
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

Jedes andere Profil kommt aus den Bibliotheken: **Aus einer Profilbibliothek…** durchsucht sie alle (siehe [Profilbibliotheken](profiles.md)).

Hohlprofile sind innen hohl, in der 3D-Ansicht, in den Schnitten und in den Mengen. Die kaltgeformten Profile sind generische Formen mit Maßen, scharf gekantet, ohne Herstellerkatalog.

![Stäbe in 3D](shots/steel-3d.webp)

## Rahmenhallen

**Stahl → Rahmenhalle…** öffnet den Hallenkonfigurator. Wie der des Dachs hat er links die Stile, in der Mitte die Felder der Halle in Reitern und rechts die Halle, wie Lapin sie bauen würde: einen typischen Rahmen entlang der Halle gesehen, mit seinen Vouten, und eine Übersicht dessen, was sie wiegt.

![Der Hallenkonfigurator](shots/steel-hall.webp)

- **Stile**: die typischen Hallen des Katalogs (**Halle 20 m**, **Halle 30 m**, ein **Pultanbau 10 m** mit Pultdach) und die eigenen des Projekts. Ein gewählter Stil füllt alle Felder; jedes lässt sich danach ändern. **Als Stil speichern…** behält die Halle, wie sie konfiguriert ist, als Stil des Projekts; **Stil aktualisieren** speichert sie erneut in ihren Stil, und alle Hallen dieses Stils folgen.
- **Rahmen**: die **Spannweite** (Stützenachse bis Stützenachse), die **Felder** (gleiche als `5 × 6`, oder einzeln: `6, 6, 7.5`), die **Traufhöhe** (die Oberkante der Riegel über der Außenfläche der Stütze), das **Dach** (Sattel- oder Pultdach) und seine **Neigung**, die Profile der **Stützen** und **Riegel**.
- **Vouten**: die **Voute an der Traufe** und die **Voute am First**, jede mit ihrer **Länge** entlang des Riegels ab der Stützenfläche (oder dem First) und ihrer **Tiefe** unter dem Riegel dort, und ihrer **Ausführung**: **aus dem Riegelprofil geschnitten** (ein I schräg entlang des Stegs geschnitten, eine Hälfte unter den Riegel geschweißt: Steg und Flansch des Riegels selbst) oder **aus Blechen geschweißt** (ein **Stegblech** und ein **Flanschblech** eigener Dicke, und die Flanschbreite).

![Die Vouten aus geschweißten Blechen](shots/steel-hall-haunches.webp)

- **Giebel**: **Giebelstützen unter einem einfachen Riegel** (Eckstützen und Stützen im gewählten **Abstand**, ihre starke Achse quer zur Giebelwand, unter einem leichteren Riegel, der bis zur Außenkante der Wände reicht, die Stützen an seine Unterseite angepasst), oder **ein normaler Rahmen** an jedem Ende.

![Ein Giebel mit Stützen](shots/steel-hall-gable.webp)

- **Sekundärstahl**: die Pfetten, Wandriegel, Verbände, der Traufriegel und die Kippstreben (unten).
- **Platzierung**: wo sich Achse A und Achse 1 kreuzen, und die Richtung der Spannweite im Grundriss (**Drehung**); die Vorschau zeigt das Raster der Halle von oben.

**Halle erstellen** baut sie in einem Schritt, den ein Rückgängig zurücknimmt: ihr eigenes **Raster** (Achsen 1, 2… an den Rahmen, A und B an den Stützen), und an jedem Rahmen zwei **Stützen** (Stützen des Tragwerks aus einem Stahlprofil, der Steg in der Rahmenebene, oben nach der Dachneigung geschnitten) und zwei am First auf Gehrung gestoßene **Riegel** (beim Pultdach einer, von der niedrigen zur hohen Stütze): Stahlstäbe, an die Stützenflächen angepasst, mit ihren Vouten. Die Halle steht auf dem aktuellen Geschoss; Traufe und First sind Höhen ihrer Rahmen, keine neuen Geschosse. Die Übersicht nennt die Rahmen, die Höhen und den Stahl nach Art: Stützen, Riegel, Vouten, Giebelstützen, gesamt und pro m² Grundfläche.

![Die Halle in 3D](shots/steel-hall-3d.webp)

Die Halle bleibt bearbeitbar. Wähle einen ihrer Stäbe: sein Abschnitt **Stahl** zeigt seine Halle und **Halle bearbeiten…**, das den Konfigurator darauf öffnet; **Anwenden** baut die Halle über denselben Stäben neu, die ihre Identität behalten (ein nicht mehr nötiger Stab entfällt, ein neuer kommt hinzu). Dort stehen auch die Vouten des Riegels, jede einzeln geändert (Art, Länge, Tiefe, Bleche): eine so eingestellte Voute bleibt, wie sie ist, wenn die Halle neu gebaut wird; **×** entfernt sie.

![Die Vouten eines Riegels in den Eigenschaften](shots/steel-hall-props.webp)

## Anschlüsse

Die Rahmen einer Halle kommen mit ihren **Anschlüssen** (Steel → Portal frame hall…, Registerkarte **Knoten**): ein **Stützenfuß** unter jeder Stütze und Giebelstütze, ein **Traufanschluss**, wo jeder Riegel an seine Stütze kommt, ein **Firstanschluss**, wo sich zwei Riegel treffen, und ein Fahnenblech unter dem Riegel über jeder Giebelstütze. Jeder ist ein eigenes Element, das die Stäbe kennt, die es verbindet; seine Platten, Schrauben und Anker entstehen aus seinen Parametern und aus den Stäben, wie sie sind, und folgen ihnen, wenn die Halle geändert wird.

![Die Registerkarte Knoten](shots/steel-nodes-hall.webp)

- **Anschlüsse**: für die ganze Halle an oder aus.
- **Stützenfüße**: **gelenkig** (eine Fußplatte, zwei Anker beiderseits des Stegs oder vier zwischen den Flanschen) oder **eingespannt** (eine längere, dickere Platte, vier bis acht Anker außerhalb der Flansche, Rippen in der Stegebene). Die **Anker** sind **gerade**, **mit Haken** oder **mit Ankerplatte** am Fuß; **Anzahl der Anker** und **Ankerlänge** (unter der Betonoberkante) wählt das Profil, wenn sie leer bleiben. Steht unter der Stütze ein Fundament der Tragwerksplanung, gehen die Anker hinein (und enden 50 mm über seiner Sohle); sonst reichen sie ihre Länge unter den Unterguss.
- **Traufanschluss**: eine überstehende Stirnplatte am Riegel, an den Stützenflansch geschraubt: ihre **Plattendicke**, **Schraubendurchmesser** und **Schraubenfestigkeitsklasse**, wie weit sie **Über dem Riegel** steht (mit einer Schraubenreihe dort) und die **Stützensteifen** in Höhe des Riegelobergurts und des Vouteflanschs.
- **Firstanschluss**: die Stirnplatten der beiden Riegel auf dem Gehrungsschnitt miteinander verschraubt, mit ihren Vouten.

Was leer bleibt, wählen die Profile: Schrauben M16 bis M27 nach der Höhe des verschraubten Stabs, eine Platte so dick wie ihre Schrauben, Rand- und Lochabstände aus den Löchern. Ein Anschluss setzt die Enden seiner Stäbe um das zurück, was er dazwischen setzt (den Riegel um seine Stirnplatte, die Stütze um ihre Fußplatte, die Giebelstütze um einen Spalt von 20 mm), über die Endregeln der Stäbe selbst.

![Ein Traufanschluss](shots/steel-nodes-eaves.webp)

![Ein eingespannter Stützenfuß auf Hakenankern](shots/steel-nodes-base.webp)

Wähle einen Anschluss, um ihn in den Eigenschaften zu sehen: die Stäbe, die er verbindet, jeden Parameter, wie er jetzt ist (eine vom Profil gewählte Größe wird so angezeigt), und seine Teile: Platten, Schrauben und Anker nach Größe und Festigkeitsklasse, die Schweißnähte nach Nahtdicke mit ihrer Länge, sein Gewicht, das Fundament, in das seine Anker gehen. Ein dort geänderter Parameter gilt nur für diesen Anschluss, und eine neu aufgebaute Halle behält ihn. **Halle bearbeiten…** öffnet seine Halle.

![Ein Anschluss in den Eigenschaften](shots/steel-nodes-props.webp)

Von Hand gezeichnete Stäbe erhalten ihre Anschlüsse mit **Steel → Anschlüsse finden**: ein Fuß unter jedem senkrechten Stab, ein Traufanschluss, wo ein Stab an eine Stütze angepasst ist, der einer Giebelstütze, wo eine Stütze unter einen Stab angepasst ist, ein Firstanschluss, wo zwei Stäbe auf Gehrung geschnitten sind, ein Stoß, wo zwei in einer Linie Ende an Ende laufen; für die gewählten Stäbe, oder das ganze Modell, wenn nichts gewählt ist. **Steel → Stahlteile** erstellt die Liste der Platten, Schrauben, Anker und Schweißnähte der Anschlüsse. Ein gelöschter Anschluss gibt seinen Stäben ihre Enden zurück; ein gelöschter Stab nimmt seine Anschlüsse mit; **Halle löschen** im Konfigurator entfernt die Halle mit ihren Stäben, Anschlüssen und Achsen in einem Schritt.

## Sekundärstahl

Eine Halle kommt mit ihrem **Sekundärstahl** (Steel → Portal frame hall…, Reiter **Sekundärstahl**), mit ihr erzeugt und mit ihr neu erzeugt, wenn sie geändert wird; jeder Stab behält seine Identität:

![Der Reiter Sekundärstahl](shots/steel-secondary-hall.webp)

- **Sekundärstahl**: ein oder aus für die ganze Halle.
- **Pfetten**: ihr **Profil** (kaltgeformte Z, C und Σ, ein U oder ein leichtes I), der **Pfettenabstand** entlang der Neigung, wie weit sie **Über die Giebel hinaus** reichen, und ihre **Längen**: **je Feld eine** (von Rahmen zu Rahmen) oder **durchgehend** (über die ganze Hallenlänge). Sie liegen auf der Oberseite der Sparren, ihr Steg rechtwinklig zum Dach, von 150 mm innerhalb der Stützenaußenseite bis 200 mm vor dem First, in so vielen Reihen, wie der Abstand verlangt.
- **Wandriegel**: ihr **Profil** und der **Wandriegelabstand**, vom **Erster Riegel bei** bis unter die Traufe, je Feld einer an der Außenseite der Stützen, ihr Steg waagerecht, ihre Kanten nach unten; an den Giebeln von Stütze zu Stütze (oder zwischen den Stützen eines Giebelrahmens), so hoch, wie der Sparren darüber es zulässt. Ein **Traufriegel** nimmt den oberen Rand der Längswände ein (mit seinem **Profil**), die Wandriegel einen Abstand darunter.
- **Verbände**: die **Verbandsfelder** (ihre Nummern, ab dem ersten), das **Profil** der Stäbe (Rundstäbe Ø16 bis Ø30, Winkel oder Rohre) und wo: **Im Dach** (ein X in der Dachebene, unter dem Obergurt der Sparren, in Feldern etwa so lang wie das Hallenfeld) und **In den Längswänden** (ein X in der Wandebene durch die Stützenachsen, von der Höhe des ersten Riegels bis unter die Voute).
- **Kippstreben**: von jeder zweiten Pfette und jedem zweiten Wandriegel hinunter zum Untergurt des Sparrens und zum Innengurt der Stütze, beidseitig, außerhalb der Vouten.

![Der Sekundärstahl in 3D](shots/steel-secondary-3d.webp)

Jede Pfette und jeder Riegel sitzt auf einem **Pfettenschuh** an jedem Sparren, jeder Stütze oder Giebelstütze, die er kreuzt: ein auf die Stabfläche geschweißtes Blech neben dem Steg der Pfette (außen am Steg eines C, auf der Seite eines Z gegenüber seinem Untergurt), durch den Steg verschraubt, zwei Schrauben je Pfettenende (M12 bis 250 mm Höhe, M16 darüber). Eine Eckstütze, schmaler als die Stützen, bekommt einen höheren Pfettenschuh bis zum Riegel. Jedes X sitzt auf **Knotenblechen**, an den Steg der Sparren oder Stützen geschweißt, eine Diagonale auf jeder Seite; ein Winkel oder ein Rohr wird mit zwei Schrauben angeschlossen, das Gewindeende eines Rundstabs nimmt seine Mutter. Eine Kippstrebe ist an beiden Enden verschraubt. Sie sind Anschlüsse wie die der Rahmen: einen auswählen, um sein Blech und seine Schrauben in den Eigenschaften zu sehen und dort zu ändern. Die Zusammenfassung des Konfigurators zählt und wiegt Pfetten, Riegel, Traufriegel, Verbände und Kippstreben; die Liste **Steel parts** enthält ihre Pfettenschuhe, Knotenbleche und Schrauben.

![Eine Pfette auf ihrem Pfettenschuh, eine Kippstrebe darunter](shots/steel-secondary-cleat.webp)

![Der Dachverband auf seinen Knotenblechen](shots/steel-secondary-bracing.webp)

## Listen und Zeichnungen

**Steel → Steel members** erstellt die Liste des Stahls im Projekt: die Stäbe sowie die Träger und Stützen aus einem Stahlprofil, nach Halle (die ohne Halle zuletzt), je eine **Marke** für gleiche Stäbe einer Art in Profil, Länge und Endschnitten (C Stützen, R Sparren, GP Giebelstützen, P Pfetten, SR Wandriegel, EB Traufriegel, B Verbände, FB Kippstreben, M andere Stäbe, BM Träger), nummeriert vom schwersten Profil und dem längsten an. Eine Zeile nennt **Profil**, **Länge**, **Schnitt am Anfang** und **Schnitt am Ende** (um wie viel die Ebene des Endes von rechtwinklig abweicht, und eine Ausklinkung: die Winkel der Zuschnittliste), die **Anzahl**, das Gewicht je Meter, **Je Stück** und insgesamt, und die Fläche für den **Anstrich**; eine Summe unter jeder Halle und eine für alles. **Steel parts** listet Bleche, Schrauben, Anker und Schweißnähte der Anschlüsse, mit ihren Pfettenschuhen und Knotenblechen.

![Die Stahlstabliste](shots/steel-members.webp)

**Steel → Anchor plan** zeichnet die Stützenfüße der Halle für die Fundamente: einen Grundriss des Geschosses der Halle im Maßstab 1:50, knapp über den Fußplatten geschnitten, jeder Anker umkreist, sein Abstand zur Achse (± bei symmetrisch angeordneten Ankern), jeder Fuß nach seinem Typ markiert (**B1**, **B2**…: gleiche Platte, Anker, Festigkeitsklasse und Vergussfuge), die Felder und die Spannweite des Rasters, und daneben die Tabelle der Typen: Anzahl, Art, Platte, Anker (Anzahl, Größe, Form), Festigkeitsklasse und Vergussfuge. **Steel → Frame elevation** zeichnet einen Rahmen (den typischen oder den des gewählten Stabs), entlang der Halle gesehen, im Maßstab 1:50, mit Spannweite, Trauf- und Firsthöhe. Beide sind Ansichten der Halle (erneut angefordert, öffnet sich dieselbe) und kommen wie jede andere auf Pläne; sie folgen der Halle, wenn sie geändert wird.

![Ein Ankerplan](shots/steel-anchor-plan.webp)

![Die Ansicht eines Rahmens](shots/steel-frame-view.webp)

## IFC und Mengen

Ein Stab wird als **IfcMember** mit seinem **IfcMemberType** exportiert. Sein Körper wird so geschrieben, wie die anderen Programme Stahl schreiben (Tekla, Advance Steel, Revit): sein Querschnitt **extrudiert** (IfcExtrudedAreaSolid), **beschnitten** von den Ebenen seiner Enden (IfcBooleanClippingResult mit IfcHalfSpaceSolid), und jede Ausklinkung ein **IfcOpeningElement**, das ihn ausspart. Ein Stahlträger oder eine Stahlstütze mit Enden wird genauso geschrieben. Die Mengen jedes Stabs geben seine Länge zwischen den Schnitten, die Fläche seines Querschnitts, sein Volumen und sein Nettogewicht (7850 kg/m³).

Eine Halle wird als **IfcBuildingSystem** (LOADBEARING) exportiert, dem ihre Stäbe zugeordnet sind, mit ihren Angaben im Eigenschaftssatz `Lapin_SteelHall` und ihrem eigenen **IfcGrid**. Ein Riegel mit Vouten ist ein **IfcElementAssembly** aus dem Riegel und seinen Vouten: eine aus dem Profil geschnittene Voute ist ein **IfcMember**, eine aus geschweißten Blechen zwei **IfcPlate**, jede extrudiert (IfcExtrudedAreaSolid). Die Vouten zählen nicht zu den Mengen des Riegels; die Übersicht des Konfigurators wiegt sie. Die Pfetten werden als **IfcMember** PURLIN exportiert, Verbände und Kippstreben als BRACE, Wandriegel und Traufriegel als USERDEFINED (**Side rail**, **Eaves beam**), alle mit ihren Anschlüssen dem System der Halle zugeordnet.

Ein Anschluss wird als **IfcElementAssembly** seiner Platten (**IfcPlate**), Schrauben (**IfcMechanicalFastener** BOLT), Anker (ANCHORBOLT) und Ankerplatten (**IfcDiscreteAccessory** ANCHORPLATE) exportiert, jeweils mit Größe und Festigkeitsklasse in `Lapin_SteelPart`; er realisiert die Verbindung der Stäbe, die er verbindet (**IfcRelConnectsWithRealizingElements**; ein Fuß zwischen Fundament und Stütze). Die Schweißnähte bleiben Daten, keine Körper.
