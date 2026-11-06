---
title: Das Gelände und seine Daten
order: 52
summary: Bringen Sie die Vermessung, das Grundstück, die Nachbarn und ein Orthofoto in realen Koordinaten ein, aus DXF, CSV, GeoJSON, CityJSON und einem georeferenzierten Bild.
---

Die Registerkarte **Gelände** enthält den Boden, auf dem das Gebäude steht: das Gelände, das Grundstück, die Nachbargebäude und ein Luftbild, jedes dort, wo es wirklich ist. Die Gruppe **Importieren** holt sie aus den Dateien, die Vermesser und das Katasteramt übergeben.

![Die Registerkarte Gelände](shots/ribbon-site.webp)

## Reale Koordinaten

Eine Vermessung kommt in Kartenkoordinaten: Rechts- und Hochwerte, Hunderte von Kilometern vom Nullpunkt entfernt. Lapin hält das Modell nahe seinem Ursprung und bindet es über den **Vermessungspunkt** an die Karte. Der erste Import in ein Projekt erledigt das für Sie: Der Vermessungspunkt kommt auf den Ursprung des Grundrisses, auf die ersten Koordinaten der Datei, auf den Meter gerundet, und jeder spätere Import landet an derselben Stelle.

**Verwalten → Standort** zeigt das **Koordinatensystem**, in dem die Rechts- und Hochwerte angegeben sind, sowie den Projektbasispunkt und den Vermessungspunkt. Lapin kennt Stereo 70 (EPSG:3844, das Landeskoordinatensystem Rumäniens), die UTM-Zonen 34 und 35 (EPSG:32634, 32635), Web Mercator (EPSG:3857) sowie geografische Länge und Breite (WGS 84, EPSG:4326). Eine Datei in einem anderen dieser Systeme wird beim Einlesen umgerechnet: Ein GeoJSON in Länge und Breite landet auf dem Stereo-70-Grundriss dort, wo es hingehört. Der IFC-Export schreibt das System und den Vermessungspunkt, sodass auch andere Programme das Modell auf der Karte platzieren.

![Verwalten → Standort: das Koordinatensystem und der Vermessungspunkt](shots/site-location.webp)

## Importieren

- **Gelände aus Punkten** liest eine CSV- oder Textdatei einer Vermessung: x y z, oder Nummer Hochwert Rechtswert Höhe, ein Punkt pro Zeile.
- **Punkte aus DXF** liest eine Vermessungszeichnung: POINT-Entitäten mit ihren Höhen, Blöcke, die auf einer Höhe eingefügt sind oder sie als Attribut tragen, neben Punkte geschriebene Höhen („245.37“ neben seinem Kreuz) und Höhenlinien auf ihrer Höhe. Hat die Zeichnung Punkte auf mehreren Layern, listet Lapin sie mit der Anzahl ihrer Punkte auf, und Sie wählen, welche übernommen werden. Die Punkte ergeben ein Gelände.
- **GeoJSON** bringt Grundstücke, Linien und Punkte. Eine Datei mit einem Polygon ergibt das **Grundstück**; eine mit mehreren ergibt Flächen. Das Grundstück umreißt auch die Grundstücksfläche des Geländes, mit ihrem Flächeninhalt. Dateien des Katasteramts in Stereo 70 geben das in der Datei an, und Lapin liest es; eine Datei, die nichts angibt, ist in Länge und Breite, wie es der GeoJSON-Standard verlangt.
- **CityJSON** bringt die Nachbargebäude mit ihren Oberflächen, an ihrem Ort und in ihrer Höhe. Sie sind schreibgeschützte Referenzen; in der Sonnenstudie werfen sie Schatten.
- **Orthofoto** legt ein Luftbild unter das Gelände. Wählen Sie das Bild (JPEG oder PNG, bis 8 MB) zusammen mit seinem World File (`.jgw`, `.pgw` oder `.wld`): Das World File gibt an, wo das Bild liegt. Das Foto wird unter dem Grundriss gezeichnet und überzieht in 3D das Gelände.

![Der Lageplan: das Orthofoto, die Höhenlinien des vermessenen Geländes, das Grundstück und der Fußabdruck des Nachbarn](shots/site-plan.webp)

![Dasselbe Gelände in 3D: das Foto auf dem Boden, der Nachbar in seiner Höhe](shots/site-3d.webp)

Alles Importierte wird in der Projektdatei gespeichert. Ein Objekt erhält seinen Namen aus der Datei (die Katasternummer eines Grundstücks, wenn die Datei eine hat) und behält die übrigen Eigenschaften der Datei bei sich; wählen Sie es aus, um seine Fläche oder Länge zu sehen.

## Straßen, Kurven und Linienzüge

Eine **Straße** oder ein **Weg** folgt ihrer Achse. In **Korridor…** (CR) gibt der Reiter **Bögen** jedem Scheitel der Achse einen Radius mit Klothoide am Anfang und Ende, wie ein Straßenplaner eine Kurve trassiert; ein Scheitel mit 0 bleibt eine Ecke, und Kurven, die nicht zwischen ihre Nachbarn passen, werden mit Begründung abgelehnt. Stationierung, Längsschnitt und Querprofile folgen der gekrümmten Achse, und IFC 4.3 schreibt sie als Alignment.

CAD-Linien werden zu Geländeelementen: **Straße aus CAD-Linien** (LR) und **Bruchkante aus CAD-Linien** (BL) nehmen eine gezeichnete oder aus einer DXF gebrachte Linie oder Polylinie, folgen jeder Linie ihres Layers, die Ende an Ende anschließt, und machen eine Straße entlang ihr oder eine Bruchkante des Geländes. Tragen die Linien Höhen (die 3D-Linien einer DXF), geben sie das Längsprofil der Straße oder die Höhen der Bruchkante. Solche Linien bleiben mit ihren Höhen erhalten und lassen sich in 3D zeigen (Kategorie *Linienzüge*).

## Lagepläne und Übersichtslagepläne

Der Projektbrowser hat unter den Ebenen einen Eintrag **Gelände**: die eigenen Zeichnungen des Geländes, die zu keinem Geschoss gehören. Ein neues Projekt beginnt mit zweien:

- dem **Lageplan** (rumänisch *plan de situație*), im Maßstab 1:500: das ganze Modell von oben gesehen, alle Geschosse auf einmal, samt Dächern, mit dem Gelände, den Wegen, den Pflanzen, dem Grundstück und den Nachbarn. Was ganz unter ±0.00 liegt (die Gründung), wird ausgelassen, nichts wird geschnitten und nichts Verdecktes gezeichnet. Die Ecken des Grundstücks werden nummeriert;
- dem **Übersichtslageplan** (*plan de încadrare în zonă*), im Maßstab 1:2000: dieselbe Zeichnung für das Grundstück in seiner Umgebung, über dem Orthofoto, ohne die Nummern der Ecken; die Gebäude des Projekts werden rot gefüllt und das Grundstück rot umrandet, sodass sich das Grundstück von seinen Nachbarn abhebt.

**+** neben Gelände erstellt einen weiteren der beiden; die Kopierschaltflächen duplizieren einen. Beide tragen einen **Nordpfeil**, der nach geografisch Nord zeigt (Verwalten → Standort: der Winkel zu geografisch Nord), eine **Maßstabsleiste** und, wenn das Projekt an die Karte gebunden ist, das **Koordinatengitter**: Kreuze, wo sich runde Rechts- und Hochwerte treffen, alle ein Zehntel des Maßstabs (50 m bei 1:500, 200 m bei 1:2000), mit ihren Werten. Wählen Sie die Ansicht im Browser aus und stellen Sie sie in ihren Eigenschaften unter **Ansichtseinstellungen** ein: was sie ist (Lageplan oder Übersichtslageplan), den Abstand des Gitters (0: keines), den Pfeil, die Leiste und **Projekt hervorheben** (die rote Füllung und Umrandung, in einem Übersichtslageplan standardmäßig an, auch für einen Lageplan, wenn Sie es wünschen).

**Die Karte darunter.** Das Orthofoto wird unter jedem Lageplan und Übersichtslageplan gezeichnet, gleich auf welchem Geschoss es importiert wurde, und wird mit ihnen auf Plänen und im PDF gedruckt. Für den Katasterplan oder die Zeichnung des Vermessers öffnen Sie den Lageplan und verwenden **DXF importieren…** mit **Platziert: nach den Kartenkoordinaten**. Lapin schlägt dies selbst vor, wenn die Koordinaten der Datei wie die einer Karte aussehen, zum Beispiel Stereo 70. Die Zeichnung landet dann dort, wo ihre Rechts- und Hochwerte es angeben, über den Vermessungspunkt des Projekts und seinen Winkel zu geografisch Nord. Ist das Projekt noch nicht an die Karte gebunden, bindet die Zeichnung es. Aktivieren Sie **Als Verknüpfung**, um sie schreibgeschützt zu halten und neu zu laden, wenn der Vermesser eine neue schickt. Importieren Sie sie einmal in jede Zeichnung, die sie braucht.

![Ein Übersichtslageplan: das Haus rot gefüllt, das Grundstück rot umrandet, inmitten seiner Nachbarn](shots/site-location-plan.webp)

![Ein Lageplan: das Haus von oben auf seinem Grundstück, die Ecken nummeriert, das Gitter der Karte, der Nordpfeil und die Maßstabsleiste](shots/site-drawings.webp)

![Das Gelände im Projektbrowser](shots/site-browser.webp)

Die Bauteilliste **Grundstückskoordinaten** (Bauteillisten → + → Grundstückskoordinaten) ist das Koordinatenverzeichnis des Grundstücks: jede Ecke so, wie der Lageplan sie nummeriert, ihr Hoch- und Rechtswert im System des Projekts, die Seite zur nächsten Ecke und die Fläche. Wie jede Bauteilliste kommt sie auf einen Plan neben den Lageplan und lässt sich als CSV oder XLSX ausgeben.

## Bäume und Sträucher

**Pflanze** (PN) setzt eine Pflanze per Klick, **Pflanzfläche** (PZ) besät einen Umriss mit einer Artenmischung in einer Dichte; wähle sie aus und bestimme die Arten in den Eigenschaften. Die Arten stammen aus einer Bibliothek der in Rumänien heimischen oder traditionellen Pflanzen, in Gruppen: **Obstbäume** (Apfel, Birne, Pflaume, Süß- und Sauerkirsche, Aprikose, Quitte), **Nadelbäume** (Weißtanne, Fichte, Wald- und Schwarzkiefer), **Laubbäume** (Walnuss, Linde, Birke, Ahorn), **Sträucher** (Hasel, Kornelkirsche, Holunder, Hundsrose, Flieder, Sanddorn, Johannisbeere, Thuja, Buchsbaum) und **Bodendecker**. Jede hat ihren lateinischen Namen, ihre übliche Höhe und Krone. Die Pflanzliste zeigt den lateinischen und den rumänischen Namen neben jeder Art.

Im Grundriss wird jede Pflanze von oben mit einer dünnen Linie gezeichnet, so breit wie ihre Krone, nur als Umriss: ein Walnussbaum oder eine Linde als weich gelappte Krone, ein Obstbaum als Kreis, eine Tanne oder Fichte als Stern, eine Kiefer als gezackter Ring, ein Strauch als kleine Wolke, eine geschnittene Heckenpflanze als Kreis, mit einem kleinen Kreuz am Stamm eines Baumes. Ansichten und Schnitte zeichnen jede Pflanze als einen Umriss: die Krone eines Baumes auf seinem Stamm, den gestuften Kegel eines Nadelbaums, die Kuppel eines Strauchs. Nur wenige Linien je Pflanze, so bleibt ein Lageplan mit Hunderten von Bäumen leicht. In 3D sind Nadelbäume Kegel, die anderen Bäume haben eine runde Krone auf ihrem Stamm und Sträucher eine flache Kuppel.

![Ein kleiner Obstgarten im Grundriss: Obstbäume, ein Walnussbaum, Nadelbäume und Sträucher](shots/planting-symbols.webp)

Stelle **Staffage in 2D** im Ansichtsstil auf **detailliert** für die Symbole eines Landschaftsplans, in Schwarz: ein Obstbaum als gelappte Krone mit gegabelten Ästen, ein Walnussbaum oder eine Linde als gewellte Krone um einen dunklen Kern, eine Tanne oder Fichte als Stern aus Nadelzweigen, eine Kiefer als runder Nadelbüschel, ein Strauch als kleine Wolke. Lapin zeichnet jedes Symbol selbst, sodass benachbarte Bäume sich etwas unterscheiden und eine Zone nie gestempelt aussieht; bei 1:1000 und kleiner (ein Übersichtsplan) werden nur die Umrisse der Kronen gezeichnet.

![Derselbe Obstgarten detailliert gezeichnet](shots/planting-symbols-detailed.webp)

## Personen und Autos

**Staffage** (EN) setzt eine Person oder ein Auto per Klick; welche, wählst du in der Leiste der Befehlszeile, solange das Werkzeug aktiv ist: einen **stehenden Mann**, eine **gehende Frau**, ein **Kind**, eine **Person am Telefon**, ein **Auto** oder einen **SUV / Transporter**. **Staffage-Reihe** (ER) setzt eine Reihe entlang einer Linie, die du zeichnest: Autos alle 2,5 m, nebeneinander quer dazu geparkt, die Front zur linken Seite der Linie, Personen alle 1,2 m, die an ihr entlang gehen. Jedes steht auf dem Gelände darunter, oder auf dem Boden seines Geschosses, wo es kein Gelände gibt.

![Personen an der Tür und Autos neben dem Haus geparkt](shots/entourage-3d.webp)

Wähle eines aus, um es in den Eigenschaften zu ändern: seine Art, seine **Drehung**, seine **Höhe** (standardmäßig die übliche Größe seiner Art), wie weit es **über dem Gelände** steht (auf einer Terrasse oder einem Balkon) und die **Farbe** seiner Kleidung oder Karosserie. Verschieben, drehen, spiegeln und kopieren wie jedes Element.

In 3D sind sie einfache Figuren: Personen mit Gliedmaßen, Kleidung, Haaren und Schuhen, Autos mit Scheiben, Rädern und Lichtern. Zeichnungen halten sie einfach, wie ein Architekt Staffage zeichnet, mit einer dünnen Linie: Grundrisse und Lagepläne von oben, die Schultern und der Kopf einer Person, der Umriss eines Autos mit gerundeten Ecken und seiner Windschutzscheibe; Ansichten und Schnitte als Silhouetten, je ein Umriss, die Figur einer Person, die Seite oder die Front eines Autos, wie die Ansicht es sieht, nie die Linien ihrer Körper.

![Im Grundriss](shots/entourage-plan.webp)

![In der Südansicht](shots/entourage-elevation.webp)

![Die Eigenschaften eines Autos](shots/entourage-properties.webp)

Wie Personen, Autos und Pflanzen in 2D gezeichnet werden, ist eine Einstellung des Ansichtsstils, **Staffage in 2D** (in den Eigenschaften der Ansicht oder **Ansicht → Ansichtseinstellungen**; für einen ganzen Stil **Verwalten → Ansichtsstile**): **Umrisse** (Standard), **abstrakt** (eine Person ein Kreis im Grundriss und ein Kopf über einem Körper in einer Ansicht, ein Auto ein abgerundeter Kasten, ein Baum ein Kreis mit seinem Stamm), **detailliert** (die Landschaftssymbole der Pflanzen und jede Linie der Körper, mit den Sonnenschatten der Ansicht, wie Lapin sie vorher zeichnete) oder **nicht gezeichnet**. **Silhouettenfüllung** füllt die Silhouetten in Ansichten und Schnitten hellgrau oder blassgrün. Sie werden mit Stift 2 gezeichnet (0,13 mm bei 1:100), sofern **Verwalten → Objektstile** der Kategorie **Staffage** keinen anderen gibt, und **Sichtbarkeit / Grafiken** blendet die Kategorie in einer Ansicht aus. Pläne, das PDF und DXF drucken sie, wie die Ansicht sie zeichnet; ihre 3D-Körper ändern sich nie. Auch früher erstellte Projekte zeichnen sie als Umrisse.

Sie geben Zeichnungen und Bildern ihren Maßstab, gehören aber nicht zum Gebäude. Ein IFC- oder Fragments-Export (File → Export…) lässt sie weg, außer du hakst **Staffage** in seiner Liste der Arten an; IFC schreibt sie als IfcBuildingElementProxy „Entourage“. Die 3D-Exporte (glTF, USD, COLLADA) nehmen sie mit. Kollisionsprüfungen lassen sie weg.

Die Figuren folgen den Personen und Autos von [BimGo](https://github.com/aussieBIMguru/BimGo) von Aussie BIM Guru (MIT-0): ihren Proportionen, Haltungen und Profilen.

## Tore

Die Gruppe **Tore** im Register Gelände setzt Tore mit ihren Pfeilern: ein **Drehtor**, ein **zweiflügeliges Drehtor** (<kbd>GT</kbd>), ein **Schiebetor**, ein **freitragendes Schiebetor**, eine **Gartentür**, ein **Tor mit seitlicher Gehtür** und ein **Falttor**. Lapin hat keinen eigenen Zaun: Zeichnen Sie den Zaun als Wand (eine niedrige, vom gewünschten Typ) oder lassen Sie die Pfeiler des Tors auf der Grundstücksgrenze stehen.

Klicken Sie auf die beiden Enden des Tors. Mit **Auf Linien** in der Leiste (Standard) kommt die Mitte des Tors auf die Achse der nächsten Wand oder auf die nächste gezeichnete Linie innerhalb von 1,5 m (eine Wand, die Grundstücksgrenze, eine gezeichnete Linie), entlang dieser; ausgeschaltet steht es zwischen den beiden Punkten. Die Leiste wählt auch den Typ, den das Werkzeug setzt, aus den Stilen des Projekts oder den mitgelieferten von Lapin.

Ein auf eine Wand gesetztes Tor schneidet sie ab den Außenflächen seiner Pfeiler, über die ganze Höhe, sodass die Zaunwand an den Pfeilern endet.

![Ein Schiebetor und eine Gartentür in einer Zaunwand, ein zweiflügeliges Tor an der Grundstücksgrenze](shots/gates-plan.webp)

Grundrisse zeichnen die Pfeiler geschnitten, die Flügel um ihren Öffnungswinkel geöffnet mit ihrem Schwenkbogen, die Paneele eines Falttors gefaltet; ein Schiebetor geschlossen, mit einem Pfeil in Öffnungsrichtung, seiner Laufschiene und der Zone, in die es fährt, gestrichelt und so lang wie sein Flügel (der Flügel eines freitragenden Tors ist um sein Gegengewichtsheck länger als die Öffnung, etwa 45 %). Ansichten, Schnitte und 3D zeichnen die Körper.

![Die Tore in 3D](shots/gates-3d.webp)

### Der Konfigurator

**Tortypen** öffnet den Konfigurator: die Stile des Projekts und die mitgelieferten von Lapin, mit einem Klick hinzuzufügen (ein zweiflügeliges Schmiedeeisentor, ein Schiebetor mit waagerechten Lamellen, ein Stahl-Falttor, eine Gartentür mit Bogen, ein Holz-Einfahrtstor, ein Sicherheitstor mit Gitter, ein automatisches freitragendes Tor, ein zweiflügeliges Tor mit Gehtür, ein Weidetor, ein Schiebetor aus Lochblech, eine Gartentür aus Holz). Ein Typ legt, in Gruppen, nur fest, was seine Art zulässt:

- **Tor:** lichte Breite und Höhe, Bodenabstand, Breite einer Gehtür, Paneele je Seite eines Falttors, Öffnungswinkel;
- **Flügel:** Rahmengröße, Füllung (senkrechte Stäbe, waagerechte Lamellen, eine volle Platte, Gitter oder Lochblech), Abstand der Stäbe oder Lamellen, Speerspitzen auf den Stäben, Abschluss oben (gerade, gebogen oder spitz) und seine Stichhöhe;
- **Pfeiler:** Mauerwerk, Stahlpfosten oder keine, ihre Größe und Höhe, die Abdeckung eines gemauerten Pfeilers (flach oder Pyramide);
- **Beschläge:** ein Antrieb (ein Arm an den Flügeln eines Drehtors, ein Kasten neben der Schiene eines Schiebetors) und Griff und Schloss;
- **Materialien** nach Rolle: Rahmen, Füllung, Pfeiler, Abdeckungen, Beschläge und Antrieb. Ein hier genanntes Material, das das Projekt nicht hat, wird angelegt.

Die Vorschau stammt vom Kern: oben der Grundriss mit dem Schwenkbogen oder der Parkzone, unten die Ansicht. Jedes Tor eines Typs folgt einer Änderung.

![Der Torkonfigurator mit einem zweiflügeligen Schmiedeeisentor](shots/gates-types.webp)

Wählen Sie ein Tor, um seinen Typ, seine Drehung, seine Höhe über dem Gelände, die Seite, zu der seine Flügel **öffnen**, die Hand, auf der seine Bänder sitzen (oder ein Schiebetor parkt), und wie weit es geöffnet gezeichnet wird (0: geschlossen), zu ändern. **Andere Seite** und **Andere Hand** in seinen Eigenschaften oder die Pfeile daneben im Grundriss kehren sie um. **Ändern → Spiegeln** gibt ihm die andere Hand.

![Die Eigenschaften eines Tors](shots/gates-properties.webp)

### Liste, Kosten und IFC

Die **Torliste** führt die Tore nach Typ: Art, lichte Größe, Flügel, Füllung, Abschluss, Pfeiler, Antrieb und Anzahl. Ein neuer Typ hat die Kostenpositionen `LAP-GATE` und, mit Antrieb, `LAP-GATEMOT`, je Tor gezählt.

IFC schreibt ein Tor als **IfcDoor** mit PredefinedType **GATE**, eines **IfcDoorType** mit seiner Bedienung (DOUBLE_DOOR_SINGLE_SWING, SLIDING_TO_LEFT, DOUBLE_DOOR_FOLDING…; ein Tor mit Gehtür USERDEFINED), seiner lichten Breite und Höhe als OverallWidth und OverallHeight und Pset_DoorCommon. Die Pfeiler gehören zum Körper des Tors, sodass ein Element das Tor so trägt, wie es konfiguriert wurde, und Lapin liest es als eigenes Tor zurück.

## Bauleitplanung und Sonne (Lapin Urban)

Die Edition **Lapin Urban** fügt dieser Registerkarte eine Gruppe **Stadtplanung** hinzu: die Bebauungsvorschriften des Grundstücks (POT, CUT, die maximalen Höhen, die Abstände zur Straße, zu den Seiten, nach hinten und zur Straßenachse), das **zulässige Bauvolumen**, das sie übrig lassen, die Kennwerte, die das Modell im Vergleich dazu erreicht, und die **Besonnungsstudie**: die Sonne und ihre Schatten in 3D für jeden Tag und jede Stunde am Ort des Projekts, sowie Schattenpläne für die Tagundnachtgleiche und die Sonnenwenden.
