/**
 * Ask Lapin: questions and answers about using Lapin, a classic FAQ (no AI).
 * Each entry has the questions people ask (English and Romanian), the words
 * that point to it, the answer in both languages, and, where the answer is a
 * place in the interface, a button that opens it.
 *
 * Adding an entry: write the questions the way a user would type them, the
 * keywords (and their Romanian, without diacritics; for an entry many ask, its
 * nouns in Spanish, Portuguese, French, Italian, German, Polish, Danish and
 * Chinese too), the answer, and `source`:
 * "faq", or the tester's report it answers ("report #23"). A report closed as
 * "hard to find in the interface" gets its entry here.
 */

/** What an answer's button does: press a ribbon tool (by its id), show a tab, open a dialog, open Drafting standards on its Project tab, open the report form, or open the user guide. */
export type Action =
  | { kind: 'tool'; id: string }
  | { kind: 'tab'; id: string }
  | { kind: 'dialog'; id: string }
  | { kind: 'standards' }
  | { kind: 'report' }
  | { kind: 'guide' };

export interface Entry {
  id: string;
  /** The questions as people ask them; the first is shown as the title. */
  q: string[];
  qRo: string[];
  /** Words that point to this entry, in English and Romanian (without diacritics); the most asked entries also in the interface's other languages (Chinese as written). */
  keywords: string[];
  a: string;
  aRo: string;
  action?: { label: string; labelRo: string; run: Action };
  source: string;
}

export const FAQ: Entry[] = [
  {
    id: 'units',
    q: ['How do I change the units to metres, centimetres or feet?', 'Can I work in feet and inches?', 'Where is the unit setting?'],
    qRo: ['Cum schimb unitățile în metri, centimetri sau picioare?', 'Pot lucra în inch?', 'Unde se schimbă unitatea de măsură?'],
    keywords: ['unit', 'units', 'metre', 'meter', 'metres', 'meters', 'feet', 'foot', 'ft', 'inch', 'inches', 'imperial', 'metric', 'mm', 'cm', 'millimetres', 'unitate', 'unitati', 'masura', 'metri', 'centimetri', 'milimetri', 'picioare', 'toli',
      // Spanish, Portuguese, French, Italian, German, Polish, Danish, Chinese
      'unidad', 'unidades', 'metros', 'metro', 'centimetros', 'milimetros', 'pies', 'pulgada', 'pulgadas', 'polegada', 'polegadas', 'pes', 'unite', 'unites', 'metres', 'pieds', 'pouces', 'unita', 'piedi', 'pollici', 'einheit', 'einheiten', 'meter', 'zoll', 'fuss', 'jednostka', 'jednostki', 'metry', 'stopy', 'cale', 'enhed', 'enheder', 'tommer', 'fod', '单位', '米', '英尺', '英寸', '毫米', '厘米'],
    a: 'Manage → Drafting standards → Project → Units (or click Units at the bottom of the window): millimetres, centimetres, metres, feet and inches, or inches. Every length in the panels, the live input, the command line and the dimensions follows it; the project keeps metres inside, so changing it never changes the model. A dimension style can also have a unit of its own.',
    aRo: 'Manage → Drafting standards → Project → Units (sau clic pe Units în bara de jos a ferestrei): milimetri, centimetri, metri, picioare și inch sau inch. Toate lungimile din panouri, din introducerea directă, din linia de comandă și din cote o urmează; proiectul ține intern metri, deci schimbarea nu modifică modelul. Un stil de cotă poate avea și o unitate proprie.',
    action: { label: 'Open Drafting standards', labelRo: 'Deschide Drafting standards', run: { kind: 'standards' } },
    source: 'report #23',
  },
  {
    id: 'workspaces',
    q: ['How do I hide the tabs I do not use?', 'Can I see only the structure tabs?', 'How do I change the order of the ribbon tabs?', 'What is a workspace?'],
    qRo: ['Cum ascund taburile pe care nu le folosesc?', 'Pot vedea doar taburile de structură?', 'Cum schimb ordinea taburilor din panglică?', 'Ce este un spațiu de lucru?'],
    keywords: ['workspace', 'workspaces', 'tab', 'tabs', 'ribbon', 'hide', 'order', 'discipline', 'architecture', 'structure', 'mep', 'site', 'drafting', 'spatiu', 'lucru', 'taburi', 'panglica', 'ascund', 'ordinea', 'disciplina'],
    a: 'Choose a workspace on the start screen, at the top of View → Interface… (Ctrl+.) or from Commands… (Workspace: …): All, Architecture, Structure & rebar, MEP, Site & site works or Drafting 2D. Each shows only its tabs, in its order; the project stays one, and a hidden tab\'s tools are still in Commands… and the shortcuts. Show, hide or move a tab under Tabs in the same dialog and it becomes My workspace, kept for next time.',
    aRo: 'Alege un spațiu de lucru pe ecranul de start, sus în View → Interface… (Ctrl+.) sau din Commands… (Workspace: …): All, Architecture, Structure & rebar, MEP, Site & site works sau Drafting 2D. Fiecare arată doar taburile lui, în ordinea lui; proiectul rămâne unul singur, iar uneltele unui tab ascuns sunt tot în Commands… și în scurtături. Arată, ascunde sau mută un tab la Tabs, în același dialog, și devine My workspace, păstrat pentru data viitoare.',
    action: { label: 'Open Interface', labelRo: 'Deschide Interface', run: { kind: 'tool', id: 'app.interface' } },
    source: 'decision 243',
  },
  {
    id: 'ramps',
    q: ['How do I make a ramp?', 'Can a stair go up to a terrace, not to the next storey?', 'How do I set the slope of a ramp?', 'Can I make a wheelchair ramp?'],
    qRo: ['Cum fac o rampă?', 'Poate o scară să urce până la o terasă, nu la etajul următor?', 'Cum setez panta unei rampe?', 'Pot face o rampă pentru scaun cu rotile?'],
    keywords: ['ramp', 'ramps', 'slope', 'wheelchair', 'accessible', 'switchback', 'terrace', 'height', 'rise', 'rampa', 'rampe', 'panta', 'terasa', 'inaltime', 'scara'],
    a: 'Architecture → Ramp (RU) places one: click the middle of its lower end, then the way it climbs. Its type (Architecture → Stair types, New ramp type) sets the shape (straight, with landings, L or switchback), the width, the slope in per cent, the longest flight before a landing and the landing\'s length; the checks show a slope steeper than the rules in red. Entrance ramp comes up from the ground to the storey\'s level. Any stair or ramp may climb a height of its own instead of the storey above: Climbs → A height of its own on the type\'s Shape tab, from below the level (an entrance) or up from it (a terrace), or Rise in its properties.',
    aRo: 'Architecture → Ramp (RU) pune una: dă clic pe mijlocul capătului de jos, apoi în direcția în care urcă. Tipul ei (Architecture → Stair types, New ramp type) dă forma (dreaptă, cu podeste, în L sau în zigzag), lățimea, panta în procente, cel mai lung tronson până la un podest și lungimea podestului; verificările arată cu roșu o pantă mai mare decât permit regulile. Entrance ramp urcă de la teren până la cota etajului. Orice scară sau rampă poate urca o înălțime proprie în loc de etajul de deasupra: Climbs → A height of its own în fila Shape a tipului, de sub cotă (o intrare) sau de la cotă în sus (o terasă), ori Rise în proprietățile ei.',
    action: { label: 'Place a ramp', labelRo: 'Pune o rampă', run: { kind: 'tool', id: 'architecture.ramp' } },
    source: 'decision 252',
  },
  {
    id: 'import-dwg-pdf',
    q: ['Can I import a DWG or a PDF?', 'How do I bring in a CAD drawing?', 'Can I trace over a plan from AutoCAD?'],
    qRo: ['Pot importa un DWG sau un PDF?', 'Cum aduc un desen din AutoCAD?', 'Cum pun un plan CAD ca fundal?'],
    keywords: ['import', 'dwg', 'dxf', 'pdf', 'autocad', 'cad', 'drawing', 'trace', 'underlay', 'background', 'link', 'importa', 'desen', 'fundal', 'plan', 'importar', 'importer', 'importare', 'importieren', 'importuj', '导入'],
    a: 'DXF, yes: Draft → Import DXF brings its lines into the plan as drafting you can snap to and edit, and ticking „As a link” in its dialog keeps the DXF linked read-only (File → Link IFC does the same for an IFC). DWG: save it as DXF from your CAD program (AutoCAD, BricsCAD, DraftSight: Save As → DXF), then import that. PDF or image: File → Import PDF or image… (or Draft → PDF or image) brings it in to trace over; a vector PDF\'s lines come in as drafting at the scale it was drawn to (Drawn at 1:), its pictures and any PNG or JPEG as see-through images, and Draft → Scale image sets an image\'s size from two points.',
    aRo: 'DXF, da: Draft → Import DXF aduce liniile în plan, ca desen pe care faci snap și pe care îl poți edita, iar bifa „As a link” din dialogul lui păstrează DXF-ul legat, needitabil (File → Link IFC face la fel pentru un IFC). DWG: salvează-l ca DXF din programul CAD (AutoCAD, BricsCAD, DraftSight: Save As → DXF), apoi importă-l. PDF sau imagine: File → Import PDF or image… (sau Draft → PDF or image) le aduce pentru calchiat; liniile unui PDF vectorial intră ca desen la scara la care a fost desenat (Drawn at 1:), pozele lui și orice PNG sau JPEG ca imagini translucide, iar Draft → Scale image îi dă imaginii mărimea din două puncte.',
    action: { label: 'Import DXF', labelRo: 'Importă DXF', run: { kind: 'tool', id: 'drafting.importDxf' } },
    source: 'report #23',
  },
  {
    id: 'dxf-locked-guide',
    q: ['How do I lock an imported DXF?', 'How do I turn a DXF into a locked group to use as a guide?', 'I cannot lock the DXF or make it a group'],
    qRo: ['Cum blochez un DXF importat?', 'Cum fac din DXF un grup blocat, ca ghidaj?', 'Nu pot bloca DXF-ul sau să-l fac grup'],
    keywords: ['lock', 'locked', 'locking', 'group', 'guide', 'guideline', 'guidance', 'reference', 'readonly', 'freeze', 'blochez', 'blocat', 'blocare', 'grup', 'ghidaj', 'referinta', 'needitabil', 'ingheti'],
    a: 'Two ways. As a link: File → Import DXF…, tick „As a link”; the drawing comes in read-only, you snap to it but cannot move or change it, and Manage → Links shows, greys (halftone), reloads or removes it as one. Already imported: Draft → Layers, tick Locked on the DXF\'s layers; their lines stay where they are and refuse edits, and you can still snap to them. To bring it in again as a link, undo the import (Ctrl+Z) first.',
    aRo: 'Două căi. Ca legătură: File → Import DXF…, bifează „As a link”; desenul intră needitabil, faci snap pe el dar nu-l poți muta sau schimba, iar Manage → Links îl arată, îl estompează (halftone), îl reîncarcă sau îl scoate dintr-o bucată. Deja importat: Draft → Layers, bifează Locked pe straturile DXF-ului; liniile lor rămân pe loc și refuză editarea, iar snap-ul pe ele merge în continuare. Ca să-l aduci din nou ca legătură, anulează întâi importul (Ctrl+Z).',
    action: { label: 'Open Layers', labelRo: 'Deschide Layers', run: { kind: 'tool', id: 'drafting.layers' } },
    source: 'report #35',
  },
  {
    id: 'dxf-in-3d',
    q: ['Why is my imported DXF not in the 3D view?', 'How do I see the DXF in 3D?', 'Can I trace walls over a DXF in 3D?'],
    qRo: ['De ce nu se vede DXF-ul importat în vederea 3D?', 'Cum văd DXF-ul în 3D?', 'Pot desena pereți peste un DXF în 3D?'],
    keywords: ['dxf', 'cad', 'underlay', 'imported', '3d', 'viewport', 'trace', 'lines', 'plane', 'level', 'vedere', 'importat', 'linii', 'fundal', 'nivel', 'cota'],
    a: 'An imported DXF lies in the 3D view on its view\'s plane: one brought into a plan at its storey\'s level, one brought into a section or an elevation on that view\'s plane, in its layers\' colours, its hatches faint. View → DXF in 3D shows or hides them all; a layer turned off in Draft → Layers stays off in 3D; one import is switched in the properties of any of its shapes (In 3D), a linked DXF in Manage → Links (3D). Drawing in 3D on that level (Draft\'s tools, or Studio\'s Sketch) snaps to its lines.',
    aRo: 'Un DXF importat stă în vederea 3D pe planul vederii lui: cel adus într-un plan la cota nivelului, cel adus într-o secțiune sau fațadă pe planul acelei vederi, în culorile straturilor lui, cu hașurile estompate. View → DXF in 3D le arată sau le ascunde pe toate; un strat stins în Draft → Layers rămâne stins și în 3D; un import anume se comută din proprietățile oricărei forme a lui (In 3D), un DXF legat din Manage → Links (3D). Desenul în 3D pe acel nivel (uneltele din Draft sau Schița din Studio) face snap pe liniile lui.',
    source: 'report #106',
  },
  {
    id: 'window-panels',
    q: ['How do I make a window with 2, 3, 4 or 6 panes?', 'How do I make a window with more sashes that open?', 'How do I edit a 2 pane window?'],
    qRo: ['Cum fac o fereastră cu 2, 3, 4 sau 6 canate?', 'Cum fac o fereastră cu mai multe canate care se deschid?', 'Cum modific o fereastră cu două canate?'],
    keywords: ['pane', 'panes', 'panel', 'panels', 'sash', 'sashes', 'leaf', 'leaves', 'casement', 'openable', 'columns', 'rows', 'mullion', 'transom', 'canat', 'canate', 'ochiuri', 'deschidere', 'coloane', 'randuri', 'montant', 'traversa'],
    a: 'Architecture → Doors & windows opens the configurator. Pick the window type (or + New window), then in Columns of panes type the panes\' relative widths, one number each: 1 1 1 is three equal panes, 1 2 1 a wide one in the middle; Rows of panes splits it in height the same way (1 1 for two rows, bottom first). In Panels, seen from inside, choose for each pane how it opens: fixed, side-hung left or right, tilt-and-turn, bottom-hung (tilt), top-hung, pivoting, sliding. Six panes are 1 1 1 in Columns of panes and 1 1 in Rows of panes. Save the type and every window of that type changes. Shutters, a grille, a window box or a canopy are under On the facade, in the same dialog.',
    aRo: 'Architecture → Doors & windows deschide configuratorul. Alege tipul ferestrei (sau + New window), apoi în Columns of panes (Coloane de canate) scrie lățimile relative ale canatelor, câte un număr pentru fiecare: 1 1 1 înseamnă trei canate egale, 1 2 1 unul lat la mijloc; Rows of panes (Rânduri de canate) o împarte la fel pe înălțime (1 1 pentru două rânduri, de jos în sus). În Panels, văzut din interior, alege pentru fiecare canat cum se deschide: fix, batant stânga sau dreapta, oscilobatant, rabatabil, pe sus, pivotant, culisant. Șase canate înseamnă 1 1 1 pe coloane și 1 1 pe rânduri. Salvează tipul și toate ferestrele de acel tip se schimbă. Obloanele, grilajul, jardiniera sau copertina sunt la On the facade, în același dialog.',
    action: { label: 'Open Doors & windows', labelRo: 'Deschide Doors & windows', run: { kind: 'tool', id: 'architecture.joineryTypes' } },
    source: 'report #38',
  },
  {
    id: 'window-dressings',
    q: ['How do I add shutters to a window?', 'Can I add a safety grille to my windows?', 'How do I add a window box or a canopy?'],
    qRo: ['Cum pun obloane la o fereastră?', 'Pot pune grilaj de siguranță la ferestre?', 'Cum pun o jardinieră sau o copertină?'],
    keywords: ['shutter', 'shutters', 'grille', 'grilles', 'grill', 'bars', 'security', 'safety', 'burglar', 'box', 'flower', 'planter', 'canopy', 'awning', 'decoration', 'obloane', 'oblon', 'grilaj', 'gratii', 'bare', 'jardiniera', 'copertina', 'decoratiuni', 'siguranta'],
    a: 'Architecture → Doors & windows, the window type, then On the facade: Shutters (louvred, panel or planks, one or two leaves, shown open or closed), Safety grille (in the reveal, on the facade or on the interior face; bars horizontal, vertical or both, their size and spacing), Window box and Canopy. Each sits by the wall\'s real faces where the window is (or the surround\'s, when there is one): From the wall face sets its gap, From the window\'s axis moves it along the wall, Up or down moves it in height. They show in 3D, plans, elevations and sections, and are measured in the schedules.',
    aRo: 'Architecture → Doors & windows, tipul ferestrei, apoi On the facade: Shutters (obloane cu lamele, pline sau din scânduri, unul sau două canate, deschise sau închise), Safety grille (grilaj în glaf, pe fațadă sau pe fața interioară; bare orizontale, verticale sau în ambele direcții, grosimea și distanța lor), Window box (jardinieră) și Canopy (copertină). Fiecare se așază după fețele reale ale peretelui unde e fereastra (sau ale ancadramentului, când există): From the wall face îi dă distanța de la perete, From the window\'s axis o mută de-a lungul peretelui, Up or down o mută pe înălțime. Apar în 3D, în planuri, fațade și secțiuni și sunt măsurate în extrase.',
    action: { label: 'Open Doors & windows', labelRo: 'Deschide Doors & windows', run: { kind: 'tool', id: 'architecture.joineryTypes' } },
    source: 'report #39',
  },
  {
    id: 'terrain-height',
    q: ['How do I move the ground to another level?', "I can't move the terrain vertically", 'How do I lower the terrain?'],
    qRo: ['Cum mut terenul pe alt nivel?', 'Nu pot muta terenul pe verticală', 'Cum cobor terenul?'],
    keywords: ['ground', 'terrain', 'vertically', 'vertical', 'lower', 'raise', 'level', 'datum', 'teren', 'terenul', 'verticala', 'cobor', 'ridic', 'nivel', 'cota'],
    a: 'Select the terrain and choose another storey under Placement → Storey (or Modify → Move to storey): it rises or falls by the difference between the two levels, as the other elements do; Keep in place in Move to storey leaves it where it is. To move it by any height, change Datum (±0.00 =) in its properties: the datum is the survey height that sits at ±0.00, so a datum 50 cm higher lowers the ground by 50 cm. Its points keep their real heights.',
    aRo: 'Selectează terenul și alege alt nivel la Placement → Storey (sau Modify → Move to storey): urcă sau coboară cu diferența dintre cele două niveluri, ca celelalte elemente; Keep in place din Move to storey îl lasă unde este. Ca să-l muți cu orice înălțime, schimbă Datum (±0.00 =) în proprietățile lui: datum-ul e cota din ridicare care stă la ±0.00, deci un datum cu 50 cm mai mare coboară terenul cu 50 cm. Punctele își păstrează cotele reale.',
    action: { label: 'Go to the Site tab', labelRo: 'Mergi la tabul Site', run: { kind: 'tab', id: 'site' } },
    source: 'report #43',
  },
  {
    id: 'edit-structure-type',
    q: ['How do I edit a column type?', 'I typed a dimension wrong in a beam type', 'How do I change a footing type?'],
    qRo: ['Cum modific un tip de stâlp?', 'Am greșit o dimensiune la un tip de grindă', 'Cum schimb un tip de fundație?'],
    keywords: ['column', 'columns', 'beam', 'beams', 'footing', 'foundation', 'type', 'types', 'edit', 'change', 'dimension', 'wrong', 'stalp', 'stalpi', 'grinda', 'fundatie', 'tip', 'modific', 'gresit'],
    a: 'Structure → Structure types: click the type in the list, change its size (or its name) and Save type. Every column, beam or footing of that type follows. New type goes back to making a new one.',
    aRo: 'Structure → Structure types: dă clic pe tip în listă, schimbă-i dimensiunea (sau numele) și apasă Save type. Toți stâlpii, grinzile sau fundațiile de acel tip se schimbă. New type te întoarce la crearea unui tip nou.',
    action: { label: 'Open Structure types', labelRo: 'Deschide Structure types', run: { kind: 'tool', id: 'structure.types' } },
    source: 'report #42',
  },
  {
    id: 'tag-text-style',
    q: ['Can I change the font, size and style of the tags?', 'How do I make the window tags bold or italic?', 'Can the tags match my office text style?'],
    qRo: ['Pot schimba fontul, mărimea și stilul etichetelor?', 'Cum fac etichetele ferestrelor bold sau italic?', 'Pot pune etichetelor stilul de text al biroului?'],
    keywords: ['tag', 'tags', 'font', 'size', 'style', 'styles', 'bold', 'italic', 'colour', 'color', 'text', 'marker', 'markers', 'studio', 'eticheta', 'etichete', 'etichetelor', 'marime', 'stil', 'stilul', 'culoare', 'fontul'],
    a: 'Yes. Manage → Drafting standards → Text: add a text style with its height (paper mm), font (sans, serif or mono), B (bold), I (italic) and colour. Then Tags: choose that style in the Text column of the window\'s or door\'s tag type (its template, the unit and how far the marker reaches are there too). Every tag of that type follows, on screen, on sheets, in the PDF and the DXF.',
    aRo: 'Da. Manage → Drafting standards → Text: adaugă un stil de text cu înălțimea (mm pe hârtie), fontul (sans, serif sau mono), B (bold), I (italic) și culoarea. Apoi Tags: alege acel stil în coloana Text a tipului de etichetă al ferestrei sau al ușii (tot acolo sunt șablonul, unitatea și cât iese marcajul). Toate etichetele de acel tip îl urmează, pe ecran, pe planșe, în PDF și în DXF.',
    action: { label: 'Open Drafting standards', labelRo: 'Deschide Drafting standards', run: { kind: 'standards' } },
    source: 'report #58',
  },
  {
    id: 'roof-window-move',
    q: ['How do I move or remove a roof window?', 'How do I delete a dormer?', 'Can I drag a roof window to another place?'],
    qRo: ['Cum mut sau șterg o fereastră de acoperiș?', 'Cum șterg o lucarnă?', 'Pot trage o fereastră de acoperiș în alt loc?'],
    keywords: ['roof window', 'roof', 'window', 'skylight', 'dormer', 'move', 'remove', 'delete', 'drag', 'fereastra', 'acoperis', 'lucarna', 'mansarda', 'velux', 'mut', 'sterg', 'trag'],
    a: 'A roof window or a dormer belongs to its roof. Select the roof: each one shows a grip in plan at its middle. Drag the grip to move it on the roof (refused where it would not fit); Ctrl/⌘-click the grip to remove it. The roof\'s properties list them too, each with × to remove it, and the roof configurator sets their size.',
    aRo: 'O fereastră de acoperiș sau o lucarnă ține de acoperișul ei. Selectează acoperișul: fiecare arată un mâner în plan, la mijlocul ei. Trage mânerul ca s-o muți pe acoperiș (refuzat unde nu încape); Ctrl/⌘-clic pe mâner o șterge. Proprietățile acoperișului le listează și ele, fiecare cu × ca s-o ștergi, iar configuratorul acoperișului le dă mărimea.',
    source: 'tester 0.1.561',
  },
  {
    id: 'schedule-fire-rating',
    q: ['How do I add a fire rating to a door?', 'Why does the fire rating not show in the joinery schedule?', 'How do I put a manufacturer or a website in the schedule?'],
    qRo: ['Cum pun rezistența la foc unei uși?', 'De ce nu apare rezistența la foc în tabelul de tâmplărie?', 'Cum pun producătorul sau site-ul în tabel?'],
    keywords: ['fire', 'rating', 'rated', 'fireproof', 'resistance', 'manufacturer', 'website', 'parameter', 'parameters', 'schedule', 'joinery', 'door', 'foc', 'rezistenta', 'producator', 'parametru', 'parametri', 'tabel', 'tamplarie', 'usa'],
    a: 'Architecture → Doors & windows: pick the type, then Parameters · in schedules → + Fire rating (kept in IFC as Pset_DoorCommon.FireRating) and type its value; + Manufacturer, + Product code, + Website or a parameter of your own work the same way. Every door of the type has it, and the joinery schedule shows it: the fire rating in its Fire rating row, any other parameter as a row of its own. A parameter named Fire rating or Fire Rated on one door shows too, and a value typed in the schedule\'s parameter row goes into the type.',
    aRo: 'Architecture → Doors & windows: alege tipul, apoi Parameters · in schedules → + Fire rating (păstrat în IFC ca Pset_DoorCommon.FireRating) și scrie valoarea; + Manufacturer, + Product code, + Website sau un parametru al tău merg la fel. Toate ușile tipului îl au, iar tabelul de tâmplărie îl arată: rezistența la foc în rândul Fire rating, orice alt parametru pe un rând al lui. Un parametru numit Fire rating sau Fire Rated pe o singură ușă apare și el, iar o valoare scrisă în rândul parametrului din tabel intră în tip.',
    action: { label: 'Open Doors & windows', labelRo: 'Deschide Doors & windows', run: { kind: 'dialog', id: 'joinery' } },
    source: 'tester 0.1.561',
  },
  {
    id: 'quantities',
    q: ['How do I get quantities from the model?', 'Where is the bill of quantities?', 'How do I put quantities on a sheet?', 'How do I export quantities to Excel?'],
    qRo: ['Cum scot cantitățile din model?', 'Unde e antemăsurătoarea?', 'Cum pun cantitățile pe o planșă?', 'Cum export cantitățile în Excel?'],
    keywords: ['quantity', 'quantities', 'takeoff', 'bill', 'boq', 'measure', 'measurements', 'area', 'volume', 'material', 'materials', 'excel', 'xlsx', 'sheet', 'cantitati', 'cantitate', 'antemasuratoare', 'masuratori', 'arie', 'volum', 'materiale', 'plansa', 'extras', 'extrase'],
    a: 'Quantities → Quantity takeoff lists every element the model measures, added up by kind and type (or by material, by storey…: the choice in its toolbar), with the count, length, gross and net area, volume and weight; Quantities → Schedules… makes any other schedule (rooms, finishes, joinery, timber, rebar…). Each follows the model. Place on sheet… in the schedule\'s toolbar, or dragging it from the project browser onto an open sheet, draws the table on the sheet, in Print and in the PDF; CSV and XLSX export it. One element\'s own quantities are in the Properties panel, under Quantities.',
    aRo: 'Quantities → Quantity takeoff listează toate elementele pe care le măsoară modelul, adunate pe tip de element și tip (sau pe material, pe nivel…: alegerea din bara lui), cu numărul, lungimea, aria brută și netă, volumul și masa; Quantities → Schedules… face orice alt extras (încăperi, finisaje, tâmplărie, lemn, armături…). Fiecare urmează modelul. Place on sheet… din bara extrasului, sau tras din navigatorul proiectului pe o planșă deschisă, desenează tabelul pe planșă, la tipărire și în PDF; CSV și XLSX îl exportă. Cantitățile unui singur element sunt în panoul Proprietăți, la Quantities.',
    action: { label: 'Make the quantity takeoff', labelRo: 'Fă antemăsurătoarea', run: { kind: 'tool', id: 'cost.takeoff' } },
    source: 'report #75',
  },
  {
    id: 'schedule-own-rows-charts',
    q: ['Why are types I do not use in the schedule?', 'Can I add my own rows and columns to a schedule?', 'Can I make a chart from a schedule?'],
    qRo: ['De ce apar în tabel tipuri pe care nu le folosesc?', 'Pot adăuga rânduri și coloane proprii într-un tabel?', 'Pot face un grafic dintr-un tabel?'],
    keywords: ['schedule', 'schedules', 'unused', 'row', 'rows', 'column', 'columns', 'chart', 'charts', 'graph', 'pie', 'bar', 'excel', 'table', 'tabel', 'tabele', 'nefolosite', 'rand', 'randuri', 'coloana', 'coloane', 'grafic', 'grafice', 'diagrama'],
    a: 'A schedule leaves out the types nothing in the model uses; Unused types in its toolbar shows them again. Row and Column add rows and columns of your own, typed cell by cell, and any cell of the table can be typed over the model\'s value (↺ gives it back); the arrows in a header sort by that column. Charts → New chart makes columns, bars, a pie or a line from the table (a column for the categories, a sum, average, least, most or a count), drawn after the tables, on sheets and in the PDF too.',
    aRo: 'Un tabel lasă deoparte tipurile pe care nu le folosește nimic din model; Unused types din bara lui le arată din nou. Row și Column adaugă rânduri și coloane proprii, completate celulă cu celulă, iar orice celulă se poate scrie peste valoarea modelului (↺ o readuce); săgețile din antet sortează după acea coloană. Charts → New chart face coloane, bare, o plăcintă sau o linie din tabel (o coloană pentru categorii, o sumă, medie, minim, maxim sau un număr), desenate după tabele, și pe planșe și în PDF.',
    source: 'tester 0.1.561',
  },
  {
    id: 'copy-move',
    q: ['How do I copy a wall?', 'How do I move an element?', 'I drag a wall and nothing happens'],
    qRo: ['Cum copiez un perete?', 'Cum mut un element?', 'Trag de un perete și nu se întâmplă nimic'],
    keywords: ['copy', 'copies', 'duplicate', 'move', 'drag', 'paste', 'clone', 'copiez', 'copiere', 'copie', 'mut', 'mutare', 'muta', 'trag', 'duplic', 'copiar', 'copio', 'kopieren', 'kopiere', 'copier', 'kopier', 'kopierer', 'copiare', 'copia', 'mover', 'deplacer', 'spostare', 'verschieben', 'kopiuj', 'przesun', 'flyt', 'copiar', '复制', '移动'],
    a: 'Select the element, then Copy (or type CO and Enter): click a base point and a second point; it keeps copying until Enter or Esc. Move (M) works the same. Or drag: drag the cross in the middle of a selected element to move it, hold Ctrl (⌘ on a Mac) while dragging it to drop a copy. A press and a release also give a command its two points.',
    aRo: 'Selectează elementul, apoi Copy (sau scrie CO și Enter): un clic pentru punctul de bază și unul pentru al doilea punct; copiază până la Enter sau Esc. Move (M) merge la fel. Sau trage: crucea din mijlocul unui element selectat îl mută, iar cu Ctrl (⌘ pe Mac) ținut lasă o copie. Apăsarea și eliberarea mouse-ului dau și ele cele două puncte ale unei comenzi.',
    action: { label: 'Start Copy', labelRo: 'Pornește Copy', run: { kind: 'tool', id: 'modify.copy' } },
    source: 'report #21',
  },
  {
    id: 'copy-to-storeys',
    q: ['How do I copy elements to several storeys at once?', 'Can I copy the selection to multiple levels?', 'Is there a copy to stories or paste aligned to levels?', 'How do I repeat a floor on the levels above?'],
    qRo: ['Cum copiez elemente pe mai multe etaje deodată?', 'Pot copia selecția pe mai multe niveluri?', 'Există copiere pe niveluri, ca în ArchiCAD?', 'Cum repet un etaj pe nivelurile de deasupra?'],
    keywords: ['storeys', 'storey', 'stories', 'story', 'levels', 'floors', 'multiple', 'several', 'many', 'aligned', 'copystorey', 'etaje', 'etajele', 'niveluri', 'nivelurile', 'multe', 'deodata', 'repet'],
    a: 'Select what to copy (walls with their doors and windows, floors, rooms, a group…), then Modify → Copy to storeys (or type COPYSTOREY, or right-click the selection in a plan or the 3D view). Tick the storeys, or All above / All below; Shift-click ticks a range. Relative keeps each copy at the same height over its new storey (the default), Absolute keeps it where the original is in space. Hosted elements go along (doors, windows, openings, their tags and dimensions, a room\'s finishes), the copies are joined and grouped as the originals, and a storey that already has the same element in the same place is skipped. It is one undo step, and the copies stay selected.',
    aRo: 'Selectează ce copiezi (pereți cu ușile și ferestrele lor, plăci, încăperi, un grup…), apoi Modify → Copy to storeys (sau scrie COPYSTOREY, sau clic dreapta pe selecție într-un plan sau în 3D). Bifează nivelurile, sau All above / All below; Shift-clic bifează un interval. Relative ține fiecare copie la aceeași înălțime peste nivelul ei nou (implicit), Absolute o lasă unde e originalul în spațiu. Elementele găzduite merg cu ele (uși, ferestre, goluri, etichetele și cotele lor, finisajele unei încăperi), copiile se îmbină și se grupează ca originalele, iar un nivel care are deja același element în același loc e sărit. E un singur pas de anulare, iar copiile rămân selectate.',
    action: { label: 'Copy to storeys', labelRo: 'Copiază pe niveluri', run: { kind: 'tool', id: 'modify.copyToStoreys' } },
    source: 'a tester\'s wish (decision 337)',
  },
  {
    id: 'elevators',
    q: ['How do I add an elevator?', 'Is there a customizable elevator or lift with a configurator?', 'How do I size a lift by load and persons (EN 81)?', 'Can I make a wheelchair-accessible, hospital bed or goods lift?'],
    qRo: ['Cum adaug un lift?', 'Există un ascensor configurabil?', 'Cum dimensionez liftul după sarcină și persoane (EN 81)?', 'Pot face un lift pentru scaun rulant, de spital sau de marfă?'],
    keywords: ['elevator', 'elevators', 'lift', 'lifts', 'shaft', 'cabin', 'car', 'en81', 'wheelchair', 'accessible', 'hospital', 'bed', 'goods', 'hydraulic', 'mrl', 'panoramic', 'passenger', 'persons', 'load', 'ascensor', 'lifturi', 'put', 'cabina', 'marfa', 'spital'],
    a: 'Architecture → Elevator (LF), then click in the plan where its shaft goes: it runs from that storey to the highest and its configurator opens. Pick one of twelve kinds (small residential, standard, medium and large passenger, heavy-duty, wheelchair-accessible EN 81-70, hospital bed, goods, hydraulic, machine-room-less, panoramic glass, home lift) and change anything by hand: the drive, rated load and persons, speed, the car, the doors, the stops it serves, the pit, the headroom, the machine room, the shaft walls. EN 81-20\'s table checks the car against its load (Load and persons from the car sets the least load). The shaft opens the floors it passes through; plans of every storey show it; Save as style keeps it for the project; Architecture → Elevator schedule lists them; IFC writes IfcTransportElement ELEVATOR.',
    aRo: 'Architecture → Elevator (LF), apoi clic în plan unde vine puțul: liftul merge de la acel nivel la cel mai înalt și i se deschide configuratorul. Alegi unul din douăsprezece feluri (rezidențial mic, pentru persoane standard, mediu, mare, trafic intens, accesibil pentru scaun rulant EN 81-70, de spital pentru paturi, de marfă, hidraulic, fără cameră a mașinii, panoramic din sticlă, lift de casă) și schimbi orice de mână: acționarea, sarcina nominală și persoanele, viteza, cabina, ușile, opririle deservite, groapa, înălțimea de siguranță, camera mașinii, pereții puțului. Tabelul din EN 81-20 verifică cabina după sarcină (Load and persons from the car pune sarcina minimă). Puțul deschide plăcile prin care trece; planurile fiecărui nivel îl arată; Save as style îl păstrează în proiect; Architecture → Elevator schedule le listează; IFC scrie IfcTransportElement ELEVATOR.',
    action: { label: 'Elevator', labelRo: 'Lift', run: { kind: 'tool', id: 'transport.elevator' } },
    source: 'testers\' reports #54 and #97',
  },
  {
    id: 'material-joints',
    q: ['How do I show the joints between walls of the same material?', 'Why is there no line between two walls?'],
    qRo: ['Cum arăt rosturile dintre pereți din același material?', 'De ce nu e linie între doi pereți?'],
    keywords: ['joint', 'joints', 'seam', 'line', 'between', 'same', 'material', 'aligned', 'coplanar', 'wood', 'clt', 'rost', 'rosturi', 'linie', 'intre', 'acelasi', 'lemn'],
    a: 'By default one material goes on from one element into the next without a line (two walls in line, a render over two storeys). To draw the joint: Manage → Materials, pick the material, tick Draw joints. The joint then shows in plans, elevations and the 3D view.',
    aRo: 'Implicit, un material trece de la un element la altul fără linie (doi pereți în prelungire, o tencuială peste două etaje). Ca să se vadă rostul: Gestionare → Materiale, alegi materialul și bifezi Desenează rosturile. Rostul apare apoi în planuri, fațade și în 3D.',
    action: { label: 'Open Materials', labelRo: 'Deschide Materials', run: { kind: 'tool', id: 'manage.materials' } },
    source: 'report #22',
  },
  {
    id: 'roof-gables',
    q: ['How do I make a gable end on a roof?', 'How do I make a mansard or a gambrel?', 'Why does a wall go through the roof?'],
    qRo: ['Cum fac un fronton la acoperiș?', 'Cum fac o mansardă?', 'De ce iese peretele prin acoperiș?'],
    keywords: ['roof', 'gable', 'gambrel', 'mansard', 'hip', 'eave', 'pitch', 'wall', 'through', 'acoperis', 'fronton', 'mansarda', 'streasina', 'panta', 'perete'],
    a: 'Select the roof: its section in the inspector has the shape (hip, gable, shed, mansard, flat), the pitch, and every edge with its kind (eave or gable), pitch and overhang. A mansard with gable edges is a gambrel. The walls of the roof\'s level stop under it and rise to its gables on their own; a wall of another level follows it after Architecture → Attach to roof (AR).',
    aRo: 'Selectează acoperișul: secțiunea lui din inspector are forma (în patru ape, două ape, o apă, mansardă, terasă), panta și fiecare margine cu felul ei (streașină sau fronton), panta și consola. O mansardă cu margini de fronton e o mansardă cu fronturi. Pereții de pe nivelul acoperișului se opresc singuri sub el și urcă până la fronton; un perete de pe alt nivel îl urmează după Architecture → Attach to roof (AR).',
    action: { label: 'Go to the Architecture tab', labelRo: 'Mergi la tabul Architecture', run: { kind: 'tab', id: 'architecture' } },
    source: 'report #24',
  },
  {
    id: 'draw-order',
    q: ['How to change the overlap of 2D annotation objects?', 'How do I change which drawing is on top?', 'How do I bring a hatch to the front or send it to the back?', 'How do I put a text over a hatch?'],
    qRo: ['Cum schimb suprapunerea obiectelor 2D?', 'Cum schimb ce desen e deasupra?', 'Cum aduc o hașură în față sau o trimit în spate?', 'Cum pun un text peste o hașură?'],
    keywords: ['overlap', 'overlapping', 'order', 'draworder', 'front', 'back', 'top', 'bottom', 'above', 'under', 'behind', 'forward', 'backward', 'hatch', 'fill', 'annotation', 'annotations', '2d', 'text', 'line', 'suprapunere', 'suprapun', 'ordine', 'fata', 'spate', 'deasupra', 'dedesubt', 'peste', 'hasura', 'umplere', 'adnotare', 'adnotari'],
    a: 'Select the drafted elements (lines, hatches and fills, texts, dimensions, tags), right-click and choose Bring to front, Bring forward, Send backward or Send to back; the Draft and Annotate tabs have them in Order, with Ctrl+Shift+], Ctrl+], Ctrl+[ and Ctrl+Shift+[. Forward and backward step past the next element they overlap. Drafting draws over the model; Send behind the model puts a hatch under the walls\' cut. On the command line DRAWORDER (DRO) asks Above objects, Under objects, Front, Back or behind Model. The order is the view\'s own, one undo step, and sheets, the PDF and the DXF print it the same.',
    aRo: 'Selectează elementele desenate (linii, hașuri și umpleri, texte, cote, etichete), clic dreapta și alege Adu în față, Adu mai în față, Trimite mai în spate sau Trimite în spate; tab-urile Desen și Adnotări le au în grupul Ordine, cu Ctrl+Shift+], Ctrl+], Ctrl+[ și Ctrl+Shift+[. Înainte și înapoi trec de următorul element peste care se suprapun. Desenul 2D stă peste model; Trimite în spatele modelului pune o hașură sub secțiunea pereților. În linia de comandă DRAWORDER (DRO) întreabă Above objects, Under objects, Front, Back sau behind Model. Ordinea e a vederii, se anulează dintr-un pas, iar planșele, PDF-ul și DXF-ul o tipăresc la fel.',
    action: { label: 'Go to the Draft tab', labelRo: 'Mergi la tabul Desen', run: { kind: 'tab', id: 'draft' } },
    source: 'report #44',
  },
  {
    id: 'opening-height',
    q: ['How do I show the opening height under the dimension line?', 'How do I write the window height below the dimension?', 'Can a dimension show a door\'s height and the sill?'],
    qRo: ['Cum arăt înălțimea golului sub linia de cotă?', 'Cum scriu înălțimea ferestrei sub cotă?', 'Poate o cotă să arate înălțimea ușii și parapetul?'],
    keywords: ['opening', 'height', 'below', 'under', 'dimension', 'line', 'window', 'door', 'sill', 'parapet', 'gol', 'golului', 'inaltime', 'inaltimea', 'sub', 'cota', 'cotei', 'fereastra', 'ferestrei', 'usa', 'usii'],
    a: 'A dimension segment from one jamb of a door, window or opening to the other writes the width over the line and the height under it, read from the model, so it follows the opening. Manage → Drafting standards → Dimensions → Opening height chooses how: off, 150, h=150, h=150 with p=90 (the sill) under it, or 150/90, in the style\'s unit. New projects have it on; an older project turns it on there.',
    aRo: 'Un segment de cotă de la un glaf la celălalt al unei uși, ferestre sau gol scrie lățimea deasupra liniei și înălțimea sub ea, citită din model, deci urmează golul. Gestionare → Standarde de desen → Dimensiuni → Înălțimea golului alege cum: oprit, 150, h=150, h=150 cu p=90 (parapetul) sub el, sau 150/90, în unitatea stilului. Proiectele noi îl au pornit; într-un proiect mai vechi îl pornești acolo.',
    action: { label: 'Open Drafting standards', labelRo: 'Deschide Drafting standards', run: { kind: 'tool', id: 'manage.standards' } },
    source: 'report #92',
  },
  {
    id: 'textures',
    q: ['How do I put my own texture on a material?', 'Can I use my own image for a material?', 'Do textures go into the 3D export?'],
    qRo: ['Cum pun o textură proprie pe un material?', 'Pot folosi o imagine a mea pentru un material?', 'Texturile ajung în exportul 3D?'],
    keywords: ['texture', 'textures', 'png', 'jpg', 'jpeg', 'glb', 'gltf', 'usdz', 'textura', 'texturi'],
    a: 'Manage → Materials, pick the material, Own image → Import image… (PNG or JPEG). Set how wide and high the picture lies on the model (W and H, metres); it shows in the 3D view, is kept in the project, and goes out with the GLB, glTF and USDZ exports (DAE and USDA are one file without images).',
    aRo: 'Gestionare → Materiale, alegi materialul, Imagine proprie → Importă o imagine… (PNG sau JPEG). Setezi cât de lată și de înaltă stă imaginea pe model (W și H, metri); apare în vederea 3D, se păstrează în proiect și pleacă odată cu exporturile GLB, glTF și USDZ (DAE și USDA sunt un singur fișier, fără imagini).',
    action: { label: 'Open Materials', labelRo: 'Deschide Materials', run: { kind: 'tool', id: 'manage.materials' } },
    source: 'faq',
  },
  {
    id: 'pat',
    q: ['How do I import my own hatch patterns (.pat)?', 'Can I use AutoCAD hatch patterns?'],
    qRo: ['Cum import hașurile mele (.pat)?', 'Pot folosi hașuri din AutoCAD?'],
    keywords: ['hatch', 'hatches', 'pattern', 'patterns', 'pat', 'autocad', 'fill', 'hasura', 'hasuri', 'model', 'modele'],
    a: 'Manage → Materials → Import .pat: every pattern in the file comes into the project and can be picked as a material\'s hatch. The file is also kept in My patterns on this computer: Add to this project brings them into any other project.',
    aRo: 'Gestionare → Materiale → Importă .pat: fiecare model din fișier intră în proiect și poate fi ales ca hașura unui material. Fișierul se păstrează și în Modelele mele pe acest calculator: Adaugă în acest proiect le aduce în orice alt proiect.',
    action: { label: 'Open Materials', labelRo: 'Deschide Materials', run: { kind: 'tool', id: 'manage.materials' } },
    source: 'faq',
  },
  {
    id: 'walls',
    q: ['How do I draw walls?', 'How do I change a wall type?'],
    qRo: ['Cum desenez pereți?', 'Cum schimb tipul unui perete?'],
    keywords: ['wall', 'walls', 'draw', 'type', 'layers', 'thickness', 'perete', 'pereti', 'desenez', 'tip', 'straturi', 'grosime', 'muro', 'muros', 'pared', 'paredes', 'espesor', 'mur', 'murs', 'paroi', 'epaisseur', 'parete', 'pareti', 'spessore', 'wand', 'waende', 'wande', 'mauer', 'dicke', 'sciana', 'sciany', 'grubosc', 'vaeg', 'vaegge', 'tykkelse', 'parede', 'espessura', '墙', '墙体', '厚度'],
    a: 'Architecture → Wall (or type WA): click the start, then each corner; Enter or Esc ends. Corners join on their own. The type (its layers and thickness) is chosen in the bar while drawing, or changed for a selected wall in the inspector; Architecture → Wall types edits the types.',
    aRo: 'Architecture → Wall (sau scrie WA): un clic la început, apoi la fiecare colț; Enter sau Esc termină. Colțurile se îmbină singure. Tipul (straturile, grosimea) se alege din bară în timp ce desenezi sau se schimbă în inspector pentru peretele selectat; Architecture → Wall types editează tipurile.',
    action: { label: 'Draw a wall', labelRo: 'Desenează un perete', run: { kind: 'tool', id: 'architecture.wall' } },
    source: 'faq',
  },
  {
    id: 'levels',
    q: ['How do I add a level?', 'How do I set the height of a storey?', 'Can a level be below zero?'],
    qRo: ['Cum adaug un nivel?', 'Cum setez înălțimea unui etaj?', 'Poate fi un nivel sub cota zero?'],
    keywords: ['level', 'levels', 'storey', 'storeys', 'story', 'floor', 'height', 'elevation', 'basement', 'top', 'base', 'nivel', 'niveluri', 'etaj', 'etaje', 'inaltime', 'cota', 'subsol', 'baza', 'varf', 'nivel', 'niveles', 'planta', 'plantas', 'piso', 'pisos', 'niveau', 'niveaux', 'etage', 'etages', 'livello', 'livelli', 'piano', 'geschoss', 'ebene', 'stockwerk', 'poziom', 'kondygnacja', 'pietro', 'niveauer', 'pavimento', 'andar', 'andares', '楼层', '标高'],
    a: 'In the project browser, Levels → + adds a level above. Select a level to see its base level and top level in the inspector, either above or below ±0.00; its height is what lies between. Moving the base keeps the top where it is.',
    aRo: 'În arborele proiectului, Levels → + adaugă un nivel deasupra. Selectează un nivel ca să-i vezi în inspector cota de bază și cota de sus, oricare peste sau sub ±0,00; înălțimea e diferența. Mutarea bazei ține vârful pe loc.',
    source: 'faq',
  },
  {
    id: 'underlay',
    q: ['How do I see the floor below while drawing?', 'How do I show another level in a plan?'],
    qRo: ['Cum văd etajul de dedesubt când desenez?', 'Cum arăt alt nivel în plan?'],
    keywords: ['underlay', 'below', 'other', 'level', 'floor', 'reference', 'faded', 'snap', 'show', 'dedesubt', 'referinta', 'alt', 'nivel', 'etaj', 'estompat'],
    a: 'In a plan\'s bar, choose Underlay: <level>. That level\'s plan shows beneath, faded and grey: you can snap to it, not select it. To print other levels\' elements on the plan itself, set them in the plan view\'s properties (Storeys shown).',
    aRo: 'În bara planului alegi Underlay: <nivel>. Planul acelui nivel apare dedesubt, estompat și gri: faci snap pe el, dar nu îl selectezi. Ca elementele altor niveluri să apară tipărite în plan, le alegi în proprietățile vederii de plan (nivelurile afișate).',
    source: 'faq',
  },
  {
    id: 'sections',
    q: ['How do I make a section?', 'How do I make the elevations?'],
    qRo: ['Cum fac o secțiune?', 'Cum fac fațadele?'],
    keywords: ['section', 'sections', 'cut', 'elevation', 'elevations', 'facade', 'view', 'sectiune', 'sectiuni', 'fatada', 'fatade', 'vedere', 'seccion', 'secciones', 'corte', 'alzado', 'fachada', 'coupe', 'facade', 'sezione', 'prospetto', 'facciata', 'schnitt', 'ansicht', 'fassade', 'przekroj', 'elewacja', 'snit', 'facade', 'secao', '剖面', '立面'],
    a: 'In the project browser, Sections → + then draw the cut line in a plan (two clicks, then the side it looks to); Elevations → + makes the four elevations. Each opens as a tab and is drawn from the model, kept up to date.',
    aRo: 'În arborele proiectului, Sections → +, apoi desenezi linia de secțiune în plan (două clicuri, apoi partea în care privește); Elevations → + face cele patru fațade. Fiecare se deschide într-un tab și se desenează din model, mereu la zi.',
    source: 'faq',
  },
  {
    id: 'sheets-pdf',
    q: ['How do I print or export a PDF?', 'How do I make a sheet?', 'Can I export DXF?'],
    qRo: ['Cum tipăresc sau export un PDF?', 'Cum fac o planșă?', 'Pot exporta DXF?'],
    keywords: ['print', 'pdf', 'export', 'sheet', 'sheets', 'layout', 'paper', 'dxf', 'tipar', 'tiparesc', 'export', 'plansa', 'planse', 'hartie', 'imprimir', 'imprimer', 'stampare', 'stampa', 'drucken', 'drukuj', 'drukowanie', 'udskriv', 'plano', 'planos', 'lamina', 'feuille', 'tavola', 'arkusz', 'blatt', '打印', '图纸', '导出'],
    a: 'In the project browser, Sheets → + makes a sheet; drag views onto it. In a sheet\'s bar, PDF and DXF export it. Views keep their scale and stay linked to the model.',
    aRo: 'În arborele proiectului, Sheets → + face o planșă; tragi vederile pe ea. Din bara planșei, PDF și DXF o exportă. Vederile își păstrează scara și rămân legate de model.',
    source: 'faq',
  },
  {
    id: 'save-open',
    q: ['How do I save my project?', 'Where is my file?', 'How do I open a project?'],
    qRo: ['Cum salvez proiectul?', 'Unde e fișierul meu?', 'Cum deschid un proiect?'],
    keywords: ['save', 'open', 'file', 'project', 'lapin', 'download', 'lost', 'recover', 'salvez', 'deschid', 'fisier', 'proiect', 'pierdut', 'recuperez', 'guardar', 'abrir', 'archivo', 'proyecto', 'enregistrer', 'ouvrir', 'fichier', 'projet', 'salvare', 'aprire', 'progetto', 'speichern', 'offnen', 'oeffnen', 'datei', 'projekt', 'zapisz', 'otworz', 'plik', 'gem', 'aabn', 'fil', 'salvar', 'arquivo', 'projeto', '保存', '打开', '文件', '项目'],
    a: 'File → Save (Ctrl S) writes a .lapin file. On the desktop it saves next to where you chose; in the browser Chrome and Edge save to the same file, Safari and Firefox download it. Unsaved work is kept as you go: after a crash the start screen offers to recover it.',
    aRo: 'File → Save (Ctrl S) scrie un fișier .lapin. Pe desktop îl salvează unde ai ales; în browser, Chrome și Edge salvează în același fișier, Safari și Firefox îl descarcă. Lucrul nesalvat e păstrat pe parcurs: după o cădere, ecranul de start oferă recuperarea lui.',
    source: 'faq',
  },
  {
    id: 'language',
    q: ['How do I switch to Romanian or Spanish?', 'How do I switch to German, French or Danish?', 'How do I switch to Italian?', 'How do I switch to Polish or Portuguese?', 'Can I change the language?'],
    qRo: ['Cum trec interfața în engleză?', 'Pot schimba limba?'],
    keywords: ['language', 'romanian', 'english', 'spanish', 'translate', 'limba', 'romana', 'engleza', 'spaniola', 'traducere', 'espanol', 'idioma', 'german', 'deutsch', 'french', 'francais', 'danish', 'dansk', 'germana', 'franceza', 'daneza', 'sprache', 'langue', 'sprog', 'lao', 'laotian', 'laotiana', 'italian', 'italiano', 'italiana', 'lingua', 'polish', 'polski', 'polska', 'poloneza', 'jezyk', 'portuguese', 'portugues', 'portugheza', 'brasil', '中文', '语言', '汉语', 'chinese', 'chineza', 'lengua', 'idioma', 'sprak'],
    a: 'Help (?) → English · Română · Español · Deutsch · Français · Dansk · Italiano · Polski · Português · ພາສາລາວ switches the interface; it is remembered on this computer. ? → User guide opens the user guide in the same language (in English for Lao).',
    aRo: 'Help (?) → English · Română · Español · Deutsch · Français · Dansk · Italiano · Polski · Português · ພາສາລາວ schimbă limba interfeței; alegerea se ține minte pe acest calculator. ? → User guide deschide ghidul utilizatorului în aceeași limbă (în engleză pentru laoțiană).',
    source: 'faq',
  },
  {
    id: 'command-line',
    q: ['What are the keyboard shortcuts?', 'How does the command line work?', 'How do I turn ortho or snap off?'],
    qRo: ['Care sunt scurtăturile?', 'Cum funcționează linia de comandă?', 'Cum opresc ortho sau snap?'],
    keywords: ['shortcut', 'shortcuts', 'keyboard', 'command', 'line', 'type', 'ortho', 'snap', 'polar', 'keys', 'scurtaturi', 'tastatura', 'comanda', 'linie'],
    a: 'Type a tool\'s letters anywhere (WA wall, CO copy, M move, L line…) and Enter; the command line at the bottom asks for each point and takes typed lengths and angles. F3 turns object snap on or off, F8 ortho, F10 polar tracking. Each tool shows its letters in its tooltip.',
    aRo: 'Scrie literele unei unelte oriunde (WA perete, CO copiere, M mutare, L linie…) și Enter; linia de comandă de jos cere fiecare punct și primește lungimi și unghiuri scrise. F3 pornește sau oprește snap-ul, F8 ortho, F10 urmărirea polară. Fiecare unealtă își arată literele în tooltip.',
    source: 'faq',
  },
  {
    id: 'navigate',
    q: ['How do I zoom and pan?', 'How do I orbit the 3D view?'],
    qRo: ['Cum fac zoom și pan?', 'Cum rotesc vederea 3D?'],
    keywords: ['zoom', 'pan', 'orbit', 'rotate', 'navigate', 'fit', '3d', 'view', 'mouse', 'trackpad', 'rotesc', 'navighez', 'vedere'],
    a: 'The wheel or a pinch zooms where the cursor is; two fingers on a trackpad, or the middle or right button dragged, pan. In 3D the left button orbits. Fit (or ZE) shows everything; double-clicking the middle button too.',
    aRo: 'Rotița sau ciupitul face zoom unde e cursorul; două degete pe trackpad sau butonul din mijloc ori cel drept, trase, fac pan. În 3D butonul stâng rotește. Fit (sau ZE) arată tot; la fel dublu clic cu butonul din mijloc.',
    source: 'faq',
  },
  {
    id: 'storeys-3d',
    q: ['How do I hide levels in the 3D view?', 'How do I see one storey alone in 3D?', 'How do I look inside the building in 3D, floor by floor?'],
    qRo: ['Cum ascund nivelurile în vederea 3D?', 'Cum văd un singur etaj în 3D?', 'Cum mă uit în interiorul clădirii în 3D, etaj cu etaj?'],
    keywords: ['level', 'levels', 'storey', 'storeys', 'story', 'stories', 'floor', 'floors', 'invisible', 'hide', 'hidden', 'isolate', 'alone', 'only', 'whole', 'building', 'inside', '3d', 'nivel', 'niveluri', 'etaj', 'etaje', 'ascund', 'ascunde', 'invizibil', 'izolez', 'singur', 'interior', 'cladire', 'cladirii'],
    a: 'Right-click a level in the project browser and choose Show only this level in 3D, or Show this level and below in 3D (the building cut at that level). The 3D view then follows the active level: click another level in the browser to move up or down. View → Storeys shown (or the 3D view\'s properties) also lets you tick the storeys, show or hide what stands on no storey (the site, the terrain), and cut the model 1.20 m above the level, as a plan does. The chip at the bottom of the 3D view says what is shown; its × shows every storey again. A saved 3D view keeps its own choice.',
    aRo: 'Clic dreapta pe un nivel în browserul proiectului și alege Show only this level in 3D sau Show this level and below in 3D (clădirea tăiată la acel nivel). Vederea 3D urmează apoi nivelul activ: dă clic pe alt nivel în browser ca să urci sau să cobori. View → Storeys shown (sau proprietățile vederii 3D) îți permite și să bifezi etajele, să arăți sau să ascunzi ce nu e pe niciun etaj (amplasamentul, terenul) și să secționezi modelul la 1,20 m deasupra nivelului, ca un plan. Eticheta din josul vederii 3D spune ce se vede; × de pe ea arată din nou toate etajele. O vedere 3D salvată își păstrează propria alegere.',
    action: { label: 'Show the active level only', labelRo: 'Arată doar nivelul activ', run: { kind: 'tool', id: 'view.3dActiveStorey' } },
    source: 'report #29',
  },
  {
    id: 'updates',
    q: ['How do I update Lapin?', 'Which version do I have?', 'Is the web version slower?'],
    qRo: ['Cum actualizez Lapin?', 'Ce versiune am?', 'Versiunea web e mai lentă?'],
    keywords: ['update', 'updates', 'version', 'new', 'download', 'desktop', 'web', 'browser', 'slow', 'actualizare', 'actualizez', 'versiune', 'lent'],
    a: 'The desktop app looks for a newer build when it starts, and on Help → Check for updates; it asks first. The web version is always the latest. The desktop app is the fastest; the web one runs the same engine in the browser, a little slower.',
    aRo: 'Aplicația desktop caută o versiune nouă la pornire și din Help → Check for updates; întreabă înainte. Versiunea web e mereu cea mai nouă. Aplicația desktop e cea mai rapidă; cea web rulează același motor în browser, ceva mai încet.',
    source: 'faq',
  },
  {
    id: 'guide',
    q: ['Is there a manual or a user guide?', 'Where is the documentation?', 'Are there tutorials?'],
    qRo: ['Există un manual sau un ghid?', 'Unde e documentația?', 'Sunt tutoriale?'],
    keywords: ['manual', 'guide', 'documentation', 'docs', 'tutorial', 'tutorials', 'help', 'learn', 'ghid', 'documentatie', 'tutoriale', 'invat', 'manual', 'guia', 'ayuda', 'tutorial', 'documentacion', 'aide', 'guida', 'aiuto', 'anleitung', 'handbuch', 'hilfe', 'podrecznik', 'pomoc', 'vejledning', 'hjaelp', 'ajuda', '手册', '帮助', '教程', '指南'],
    a: 'The user guide at lapin.ciuntucbimstudio.ro/docs shows, page by page with pictures from the app, how to model walls, doors and windows, floors and roofs, structure, views, sheets and exports, in English, Romanian, Spanish, German, French, Danish, Italian, Polish and Brazilian Portuguese. It is also on ? → User guide, in the language of the interface.',
    aRo: 'Ghidul utilizatorului, la lapin.ciuntucbimstudio.ro/docs, arată pagină cu pagină, cu imagini din aplicație, cum modelezi pereți, uși și ferestre, planșee și acoperișuri, structura, vederile, planșele și exporturile, în engleză, română, spaniolă, germană, franceză, daneză, italiană, poloneză și portugheză (Brazilia). Îl găsești și la ? → User guide, în limba interfeței.',
    action: { label: 'Open the user guide', labelRo: 'Deschide ghidul', run: { kind: 'guide' } },
    source: 'faq',
  },
  {
    id: 'report',
    q: ['How do I report a bug?', 'Where do I send an idea?'],
    qRo: ['Cum raportez o problemă?', 'Unde trimit o idee?'],
    keywords: ['bug', 'report', 'issue', 'problem', 'error', 'crash', 'idea', 'feedback', 'raportez', 'problema', 'eroare', 'idee', 'sugestie'],
    a: 'Help (?) → Report an issue: say what happened and what you expected, add a screenshot if it helps. It reaches us with the app\'s version and its last errors.',
    aRo: 'Help (?) → Report an issue: spune ce s-a întâmplat și ce așteptai, adaugă o captură dacă ajută. Ajunge la noi cu versiunea aplicației și ultimele ei erori.',
    action: { label: 'Report an issue', labelRo: 'Raportează o problemă', run: { kind: 'report' } },
    source: 'faq',
  },
  {
    id: 'rooms',
    q: ['How do I colour or hatch rooms on a plan?', 'How do I place a room?', 'How do I add a colour legend for rooms?'],
    qRo: ['Cum colorez sau hașurez încăperile pe un plan?', 'Cum pun o încăpere?', 'Cum adaug o legendă de culori pentru încăperi?'],
    keywords: ['room', 'rooms', 'space', 'spaces', 'colour', 'color', 'colours', 'colors', 'scheme', 'legend', 'function', 'department', 'area', 'incapere', 'incaperi', 'camera', 'camere', 'spatiu', 'culoare', 'culori', 'colorez', 'schema', 'legenda', 'functiune', 'suprafata', 'arie'],
    a: 'Architecture → Room (RM), then click inside the walls: Lapin finds the room\'s outline and keeps it up with the walls (Room by outline draws one). Give rooms a function or department in their properties, then View → Room colours: pick a scheme, a colour and/or a hatch for each value, and set "This plan shows". Annotate → Colour legend lists the colours the plan uses.',
    aRo: 'Architecture → Room (RM), apoi clic între pereți: Lapin găsește conturul încăperii și îl ține la zi cu pereții (Room by outline desenează unul). Dă încăperilor o funcțiune sau un departament în proprietăți, apoi View → Room colours: alegi o schemă, o culoare și/sau o hașură pentru fiecare valoare și setezi „This plan shows”. Annotate → Colour legend pune legenda culorilor folosite pe plan.',
    action: { label: 'Room colours', labelRo: 'Culorile încăperilor', run: { kind: 'tool', id: 'architecture.roomColors' } },
    source: 'faq',
  },
  {
    id: 'assistant',
    q: ['Can I ask an AI about my model?', 'Is there an AI assistant in Lapin?', 'How do I count the walls per storey or set a property on many elements at once?'],
    qRo: ['Pot întreba un AI despre model?', 'Are Lapin un asistent AI?', 'Cum număr pereții pe etaje sau pun o proprietate pe multe elemente odată?'],
    keywords: ['ai', 'assistant', 'chat', 'llm', 'ollama', 'gpt', 'model', 'report', 'firerating', 'asistent', 'raport', 'intreb', 'inteligenta'],
    a: 'In the desktop app: the Assistant tab beside Properties (View → Assistant). It runs a local model through Ollama on your computer (install Ollama and `ollama pull qwen3-coder:30b`); nothing leaves the machine. It reads the project, counts and sums (Lapin computes the numbers), makes tables you can export as CSV, and proposes property values you tick and apply as one undo step. It never changes geometry or deletes anything. The browser version has no assistant.',
    aRo: 'În aplicația desktop: fila Assistant de lângă Properties (View → Assistant). Folosește un model local prin Ollama, pe calculatorul tău (instalezi Ollama și `ollama pull qwen3-coder:30b`); nimic nu pleacă de pe calculator. Citește proiectul, numără și adună (numerele le calculează Lapin), face tabele pe care le exporți ca CSV și propune valori de proprietăți pe care le bifezi și le aplici ca un singur pas de anulare. Nu schimbă niciodată geometria și nu șterge nimic. Versiunea din browser nu are asistent.',
    action: { label: 'Open the user guide', labelRo: 'Deschide ghidul', run: { kind: 'guide' } },
    source: 'faq',
  },
  {
    id: 'lasso-select',
    q: ['Is there a lasso selection like in AutoCAD?', 'How do I select with a box or a lasso?', 'What is the difference between the blue and the green selection box?'],
    qRo: ['Există selecție lasso ca în AutoCAD?', 'Cum selectez cu o fereastră sau cu un lasou?', 'Care e diferența între chenarul albastru și cel verde?'],
    keywords: ['lasso', 'select', 'selection', 'box', 'window', 'crossing', 'polygon', 'drag', 'marquee', 'wp', 'cp', 'lasou', 'selectie', 'selectez', 'fereastra', 'chenar', 'poligon', 'trag'],
    a: 'In a plan, section, elevation or drafting view, drag with the left button and nothing selected under it. Dragged to the right the box is blue and takes what lies wholly inside it (a window); to the left it is green and dashed and takes everything it touches (crossing). Press Space while you drag and the box becomes a lasso that follows the mouse, keeping the mode it had; Space again goes back to the box. Shift adds to what is selected. On the command line, while a command asks you to select objects, type WP or CP and click the corners of a polygon, then Enter.',
    aRo: 'Într-un plan, o secțiune, o elevație sau un desen, trage cu butonul stâng, fără nimic selectat sub cursor. Tras spre dreapta, chenarul e albastru și ia ce e cuprins în întregime în el (fereastră); spre stânga e verde și punctat și ia tot ce atinge (crossing). Apasă Space în timp ce tragi și chenarul devine un lasou care urmează mouse-ul, cu modul pe care îl avea; Space din nou revine la chenar. Shift adaugă la ce e selectat. Pe linia de comandă, când o comandă cere să selectezi obiecte, scrie WP sau CP și dă clic pe colțurile unui poligon, apoi Enter.',
    source: 'faq',
  },
  {
    id: 'display-quality',
    q: ['The 3D view is slow with a big model: what can I do?', 'Why does a far object look coarser?', 'How precise are curves in Lapin?'],
    qRo: ['Vederea 3D merge greu cu un model mare: ce pot face?', 'De ce arată mai grosier un obiect îndepărtat?', 'Cât de precise sunt curbele în Lapin?'],
    keywords: ['slow', 'fast', 'speed', 'performance', 'lag', 'fps', 'quality', 'precision', 'precise', 'coarse', 'lod', 'detail', 'curve', 'facets', 'greu', 'incet', 'viteza', 'calitate', 'precizie', 'grosier', 'detaliu', 'curba', 'fatete'],
    a: 'View → Display quality chooses how the 3D view draws parts of many triangles (an imported plant, furniture, pipes) when they are far away: Balanced draws a far part coarser only where the difference is under a pixel, Fast lets it stray up to 3 pixels, Precise draws every part as it is. It is the project\'s setting and changes only the screen: the model, its drawings, exports and quantities keep their full precision, and every curve is made of straight pieces at most a millimetre off it. A model of many small elements gains less from it than one with heavy parts; hiding storeys or categories you do not need (View → Visibility) helps there.',
    aRo: 'Vedere → Calitate afișare alege cum desenează vederea 3D piesele cu multe triunghiuri (o instalație importată, mobilier, țevi) când sunt departe: Echilibrat desenează mai grosier o piesă îndepărtată doar unde diferența e sub un pixel, Rapid o lasă să se abată până la 3 pixeli, Precis desenează fiecare piesă așa cum e. E o setare a proiectului și schimbă doar ecranul: modelul, desenele, exporturile și cantitățile își păstrează toată precizia, iar fiecare curbă e făcută din bucăți drepte care se abat de la ea cel mult un milimetru. Un model cu multe elemente mici câștigă mai puțin decât unul cu piese grele; acolo ajută să ascunzi nivelurile sau categoriile de care nu ai nevoie (Vedere → Vizibilitate).',
    source: 'faq',
  },
  {
    id: 'studio-sketch-on-face',
    q: ['How do I sketch on a face or a wall?', 'Can I draw on an elevation or on any plane?', 'What is a reference plane?'],
    qRo: ['Cum schițez pe o față sau pe un perete?', 'Pot desena pe o elevație sau pe orice plan?', 'Ce e un plan de referință?'],
    keywords: ['work', 'plane', 'workplane', 'reference', 'face', 'sketch', 'elevation', 'facade', 'draw', 'plan', 'lucru', 'fata', 'schita', 'elevatie', 'fatada', 'desenez'],
    a: 'Studio → Plane on face, then click a face in 3D: a reference plane opens as a view on that face. Draft a closed outline on it and Extrude, Sweep or Revolve it: the solid stands on the face. Plane from view and Plane by 3 points make one elsewhere. Plans, sections and elevations take sketches too. Reference planes are listed under Reference planes in the project browser; their properties move and turn them.',
    aRo: 'Studio → Plan pe față, apoi clic pe o față în 3D: un plan de referință se deschide ca vedere pe fața aceea. Desenează un contur închis pe el și Extrudează, Mătură sau Rotește-l: solidul stă pe față. Plan din vedere și Plan prin 3 puncte fac unul în altă parte. Planurile, secțiunile și elevațiile primesc și ele schițe. Planurile de referință sunt sub Planuri de referință în navigatorul proiectului; proprietățile lor le mută și le rotesc.',
    source: 'faq',
  },
  {
    id: 'studio-push-pull',
    q: ['Is there push/pull like in SketchUp?', 'How do I fillet or chamfer an edge?', 'How do I hollow a solid?'],
    qRo: ['Există push/pull ca în SketchUp?', 'Cum racordez sau teșesc o muchie?', 'Cum golesc un solid?'],
    keywords: ['push', 'pull', 'pushpull', 'fillet', 'chamfer', 'round', 'bevel', 'edge', 'shell', 'hollow', 'split', 'array', 'sketchup', 'impinge', 'trage', 'racordare', 'tesire', 'muchie', 'coaja', 'golesc', 'imparte', 'matrice'],
    a: 'In 3D, on the Studio tab: Push/Pull (PU) moves the face you click out or in by the distance you type beside the cursor; Fillet edge (FE) and Chamfer edge (CE) round or bevel the edge nearest your click (or every edge round the face); Shell hollows the selected solids; Split, Slice, Array, Polar array, Rotate 3D, Mirror 3D and Move 3D do the rest. Each is a step listed in the solid\'s properties, where it can be switched off, moved, changed or removed, and redone when the sketch changes. On walls and other elements, Push/Pull adds a solid or cuts into them; Make Studio solid lets them take every step.',
    aRo: 'În 3D, în fila Studio: Împinge/Trage (PU) mută fața pe care dai clic în afară sau înăuntru cu distanța scrisă lângă cursor; Racordează muchia (FE) și Teșește muchia (CE) rotunjesc sau teșesc muchia cea mai apropiată de clic (sau toate muchiile feței); Coajă golește solidele selectate; Împarte, Retează, Matrice, Matrice polară, Rotește 3D, Oglindește 3D și Mută 3D fac restul. Fiecare e un pas din proprietățile solidului, unde poate fi oprit, mutat, schimbat sau scos, și se reface când se schimbă schița. Pe pereți și alte elemente, Împinge/Trage adaugă un solid sau taie în ele; Fă solid Studio le face să primească toți pașii.',
    source: 'faq',
  },
  {
    id: 'studio-sketch-3d',
    q: ['How do I draw lines on a face in 3D like in SketchUp?', 'Can I use the Line and Rectangle tools in the 3D view?', 'How do I draw on a work plane in 3D with the command line?'],
    qRo: ['Cum desenez linii pe o față în 3D ca în SketchUp?', 'Pot folosi Linie și Dreptunghi în vederea 3D?', 'Cum desenez pe un plan de lucru în 3D cu linia de comandă?'],
    keywords: ['sketch', 'draw', 'line', 'rectangle', 'face', 'inference', 'axis', 'workplane', 'plane', 'sketchup', 'split', 'region', 'regiune', 'imparte', 'hole', 'island', 'gaura', 'arc', 'tape', 'measure', 'guide', 'follow', 'ruleta', 'ghidaj', 'schita', 'desenez', 'linie', 'dreptunghi', 'fata', 'plan', 'lucru', 'axa'],
    a: 'Studio → Sketch. Without a work plane, Line (KL), Rectangle (KR) and Circle (KC) draw right on the face under the cursor, SketchUp\'s way (on the storey\'s level in empty space): the cursor infers endpoints, midpoints, edges, the red, green and blue axes and edges\' ways (magenta); → ← ↑ lock an axis, ↓ the edge\'s way, Shift holds what is inferred; type a value at any time (1200, 1200,600, r300) and press Enter. A closed shape gets the push/pull arrow at once (Esc keeps it a sketch). The CAD way: Sketch on face (KF) makes the face the active work plane, lit; Line, Rectangle, Circle and the command line\'s commands then draw on it (2-point arc is always SketchUp\'s, 3-point arc always the command line\'s ARC), Finish sketch (Enter) pushes or pulls the closed shape, Look at plane (KP) turns the view square to it. A line across a face splits it: Push/Pull takes the region you click. Tape measure (KM) leaves guides, Follow Me (FM) sweeps a face along a path, and SketchUp shortcuts switches the one-letter keys on.',
    aRo: 'Studio → Schiță. Fără plan de lucru, Linie (KL), Dreptunghi (KR) și Cerc (KC) desenează direct pe fața de sub cursor, ca în SketchUp (pe cota nivelului în gol): cursorul deduce capete, mijloace, muchii, axele roșie, verde și albastră și direcțiile muchiilor (magenta); → ← ↑ blochează o axă, ↓ direcția muchiei, Shift ține ce e dedus; scrie o valoare oricând (1200, 1200,600, r300) și apasă Enter. O formă închisă primește imediat săgeata de împinge/trage (Esc o lasă schiță). Calea CAD: Schiță pe față (KF) face fața plan de lucru activ, luminată; Linie, Dreptunghi, Cerc și comenzile liniei de comandă desenează pe ea (Arc din 2 puncte e mereu cel din SketchUp, Arc din 3 puncte mereu ARC din linia de comandă), Termină schița (Enter) împinge sau trage forma închisă, Privește planul (KP) întoarce vederea drept spre ea. O linie peste o față o împarte: Împinge/Trage ia regiunea pe care dai clic. Ruleta (KM) lasă ghidaje, Urmează calea (FM) mătură o față de-a lungul unei căi, iar Scurtături SketchUp pornește tastele de o literă.',
    action: { label: 'Sketch on face', labelRo: 'Schiță pe față', run: { kind: 'tool', id: 'studio.sketchOnFace' } },
    source: 'faq',
  },
  {
    id: 'import-mesh',
    q: ['Can I import an OBJ, STL or glTF file?', 'How do I bring a SketchUp or Blender model in?'],
    qRo: ['Pot importa un fișier OBJ, STL sau glTF?', 'Cum aduc un model din SketchUp sau Blender?'],
    keywords: ['obj', 'stl', 'gltf', 'glb', 'mesh', 'import', 'blender', 'sketchup', 'model', 'retea', 'importa', 'fisier'],
    a: 'File → Import mesh (OBJ, STL, glTF)… brings it in as references on the active level (export it from SketchUp or Blender as one of those). An OBJ or STL does not say its unit or which way is up, so Lapin asks. Select a reference and use Studio → Make Studio solid to edit it with push/pull, fillets and booleans.',
    aRo: 'Fișier → Importă rețea (OBJ, STL, glTF)… îl aduce ca referințe pe nivelul activ (din SketchUp sau Blender exportă-l într-unul din formatele acestea). Un OBJ sau STL nu spune unitatea și ce axă e în sus, așa că Lapin întreabă. Selectează o referință și folosește Studio → Fă solid Studio ca s-o editezi cu împinge/trage, racordări și booleene.',
    source: 'faq',
  },
  {
    id: 'title-block',
    q: ['Can I make my own title block?', 'I want a custom title block, like the official Finnish one', 'How do I put my logo and company in the title block?'],
    qRo: ['Pot să-mi fac propriul cartuș?', 'Cum fac un cartuș personalizat?', 'Cum pun sigla și firma în cartuș?'],
    keywords: ['titleblock', 'title', 'block', 'custom', 'logo', 'stamp', 'border', 'company', 'finnish', 'nimio', 'cartus', 'indicator', 'sigla', 'firma', 'personalizat', 'propriul'],
    a: 'Yes. Manage → Title blocks… lists Lapin\'s (Lapin standard, the Romanian cartuș, ISO 7200), the project\'s and this computer\'s. Duplicate one or start a New one and Edit it in the designer: draw lines, rectangles, fills, texts and your logo, and click fields (project name, client, sheet number, scale, date, revision…) to insert them as live text that fills itself on every sheet. Set it as the project\'s default, or choose it per sheet in its properties. Anything missing goes in Manage → Project information… as a parameter of your own. Export… saves it as a .json for another computer; PDF, Print and DXF (a block with attributes) draw it.',
    aRo: 'Da. Gestionare → Cartușe… arată cartușele lui Lapin (Lapin standard, cartușul românesc, ISO 7200), ale proiectului și ale acestui calculator. Duplică unul sau pornește unul Nou și editează-l în editor: desenezi linii, dreptunghiuri, umpluturi, texte și sigla, iar câmpurile (numele proiectului, beneficiarul, numărul planșei, scara, data, revizia…) le inserezi cu un clic ca text viu, completat singur pe fiecare planșă. Fă-l implicit pentru proiect sau alege-l pe fiecare planșă din proprietățile ei. Ce lipsește adaugi în Gestionare → Datele proiectului… ca parametru propriu. Exportă… îl salvează ca .json pentru alt calculator; PDF, Tipărire și DXF (un bloc cu atribute) îl desenează.',
    action: { label: 'Title blocks…', labelRo: 'Cartușe…', run: { kind: 'dialog', id: 'titleBlocks' } },
    source: 'report #76',
  },
  {
    id: 'balconies',
    q: ['How do I make a balcony?', 'Can I make a Juliet balcony, a loggia or a mezzanine gallery?', 'How do I stack the same balcony on every floor?', 'How do I put a railing on a balcony?'],
    qRo: ['Cum fac un balcon?', 'Pot face un balcon franțuzesc, o logie sau o galerie de mezanin?', 'Cum pun același balcon pe toate etajele?', 'Cum pun balustradă la balcon?'],
    keywords: ['balcony', 'balconies', 'juliet', 'loggia', 'loggias', 'mezzanine', 'gallery', 'cantilever', 'cantilevered', 'stacked', 'balcon', 'balcoane', 'logie', 'logia', 'mezanin', 'galerie', 'consola', 'frantuzesc'],
    a: 'Architecture → Balcony (BY): choose its style in the command line\'s bar — Juliet, cantilevered with glass, stacked on posts, on brackets, hung from tie rods, rounded, curved with a parapet, loggia, corner or mezzanine gallery — and click by a wall\'s face; a Juliet balcony centres on the door you click by, a loggia cuts its recess into the wall with its side walls, ceiling and back wall. Balcony by outline draws any shape (its first edge against the building), Gallery edge a mezzanine\'s edge over a double-height space. Balcony configurator (or the gear in its properties) changes everything — outline, slab, edge and drip, finish and fall, railing or parapet, posts, brackets or rods, the loggia\'s recess — over a plan and a section, and saves it as a project style; Stack on storeys above copies it onto every storey over its own in one step. Its schedule is Architecture → Balcony schedule.',
    aRo: 'Architecture → Balcony (BY): alege stilul în bara liniei de comandă — franțuzesc (Juliet), în consolă cu sticlă, suprapus pe stâlpi, pe console metalice, suspendat în tiranți, cu colțuri rotunjite, cu front curb și parapet, logie, de colț sau galerie de mezanin — și dă clic lângă fața unui perete; balconul franțuzesc se centrează pe ușa lângă care dai clic, logia își taie golul în perete, cu pereții laterali, tavanul și peretele din spate. Balcony by outline desenează orice formă (prima latură lipită de clădire), Gallery edge marginea unui mezanin peste un spațiu dublu. Balcony configurator (sau rotița din proprietăți) schimbă tot — conturul, placa, muchia și lăcrimarul, finisajul și panta, balustrada sau parapetul, stâlpii, consolele sau tiranții, golul logiei — peste un plan și o secțiune, și îl salvează ca stil al proiectului; Stack on storeys above îl copiază pe toate etajele de deasupra într-un singur pas. Tabelul lui este Architecture → Balcony schedule.',
    action: { label: 'Place a balcony', labelRo: 'Pune un balcon', run: { kind: 'tool', id: 'architecture.balcony' } },
    source: 'report #96',
  },
  {
    id: 'joinery-3d-preview',
    q: ['Can I see the window or door in 3D while I configure it?', 'Is there a 3D preview in the Doors and windows configurator?'],
    qRo: ['Pot vedea fereastra sau ușa în 3D cât o configurez?', 'Există o previzualizare 3D în configuratorul de uși și ferestre?'],
    keywords: ['preview', 'previews', 'configurator', 'configure', 'configuring', 'orbit', 'previzualizare', 'configuratorul', 'configurez', 'vista', 'previa', 'apercu', 'anteprima', 'vorschau', 'podglad'],
    a: 'Yes: in Architecture → Doors & windows, under the drawings on the right, a 3D preview shows the type as it is now, before you save it, standing in a piece of wall of the project\'s first wall type with its reveal, sills, lining and materials. Drag to turn it, use the wheel to zoom, right-drag to pan.',
    aRo: 'Da: în Architecture → Doors & windows, sub desenele din dreapta, o previzualizare 3D arată tipul așa cum e acum, înainte să-l salvezi, într-o bucată de perete de primul tip de perete al proiectului, cu glaful, solbancurile, căptușeala și materialele lui. Trage ca să-l rotești, rotița pentru zoom, butonul drept ca să-l muți.',
    action: { label: 'Open Doors & windows', labelRo: 'Deschide Doors & windows', run: { kind: 'tool', id: 'architecture.joineryTypes' } },
    source: 'report #108',
  },
  {
    id: 'wall-thickness',
    q: ['How do I change the thickness of one wall?', 'Can I edit a wall\'s dimensions on its own?', 'How do I change a wall\'s length?'],
    qRo: ['Cum schimb grosimea unui singur perete?', 'Pot modifica dimensiunile unui perete separat?', 'Cum schimb lungimea unui perete?'],
    keywords: ['wall', 'walls', 'perete', 'peretelui', 'thickness', 'thick', 'thicker', 'thinner', 'dimension', 'dimensions', 'individually', 'individual', 'length', 'grosime', 'grosimea', 'gros', 'dimensiuni', 'dimensiunile', 'separat', 'lungime', 'lungimea', 'espesor', 'epaisseur', 'spessore', 'dicke', 'grubosc', 'espessura', 'tykkelse'],
    a: 'Select the wall: in Properties, Length, Height and Base offset are the wall\'s own. Its thickness comes from its type\'s layers, so typing a new Thickness there gives the selected walls the type with the same build-up and that thickness (the core resized, the finishes kept): the project\'s if it has one, else a new type named after theirs, e.g. Partition wall 150mm. The other walls of the old type stay as they are. One undo step.',
    aRo: 'Selectează peretele: în Properties, Length, Height și Base offset sunt ale peretelui. Grosimea vine din straturile tipului său, așa că o Thickness nouă scrisă acolo dă pereților selectați tipul cu aceeași alcătuire și acea grosime (miezul redimensionat, finisajele păstrate): al proiectului, dacă există, altfel un tip nou numit după al lor, de ex. Partition wall 150mm. Ceilalți pereți ai tipului vechi rămân cum sunt. Un singur pas de anulare.',
    source: 'report #107',
  },
  {
    id: 'bay-windows',
    q: ['How do I make a bay window?', 'Can I make bow and box windows?', 'How do I make a window that projects from the wall, with a seat and a roof?'],
    qRo: ['Cum fac un bovindou?', 'Pot face ferestre în arc sau în cutie?', 'Cum fac o fereastră ieșită din perete, cu banchetă și acoperiș?'],
    keywords: ['bay', 'bow', 'box', 'oriel', 'projecting', 'protruding', 'seat', 'corbel', 'corbels', 'bovindou', 'erker', 'iesita', 'iesind', 'consola', 'console', 'bancheta'],
    a: 'Architecture → Bay window (BW) puts a bay in the wall you click. In Architecture → Doors & windows, New bay window, or Projects on any window type, makes it a Bay (sides at an angle), a Bow (panels on an arc) or a Box (square sides). The grid\'s columns are its faces (a bay\'s first and last columns its sides), each pane opening as Panels says; then the projection, the sides\' angle, the seat inside, the sill, the base (walls to the floor, a slab, a slab on corbels), the head (hipped, lean-to or flat roof, a slab) with its flashing, each with its material. Bay style sets them all at once (45° and 30° bays, a canted bay, bows of 5 or 4 panels, boxes, an oriel); Save as bay style… keeps your own. It stays one window in one opening: scheduled with its faces unfolded and a Projecting row, one IfcWindow in IFC.',
    aRo: 'Architecture → Bay window (BW) pune un bovindou în peretele pe care dai clic. În Architecture → Doors & windows, New bay window, sau Projects la orice tip de fereastră, o face Bay (laturi oblice), Bow (panouri pe un arc) sau Box (laturi în unghi drept). Coloanele grilei sunt fețele ei (prima și ultima coloană ale unui bovindou sunt laturile), fiecare ochi deschizându-se cum spune Panels; apoi ieșirea din perete, unghiul laturilor, bancheta din interior, glaful, baza (pereți până la pardoseală, o placă, o placă pe console), capul (acoperiș în patru ape, într-o apă sau plat, o placă) cu șorțul de tablă, fiecare cu materialul lui. Bay style le pune pe toate deodată (bovindouri la 45° și 30°, unul pe pardoseală, arce de 5 sau 4 panouri, cutii, un erker); Save as bay style… le păstrează pe ale tale. Rămâne o fereastră într-un gol: în extras cu fețele desfășurate și rândul Projecting, un IfcWindow în IFC.',
    action: { label: 'Open Doors & windows', labelRo: 'Deschide Doors & windows', run: { kind: 'tool', id: 'architecture.joineryTypes' } },
    source: 'report #60',
  },
  {
    id: 'sunshades',
    q: ['How do I add a sunshade over a window?', 'Can I model louvres, fins or a brise-soleil?', 'How deep must a canopy be to shade the window in summer?'],
    qRo: ['Cum pun o copertină parasolar peste o fereastră?', 'Pot modela lamele, aripioare sau brise-soleil?', 'Cât de adâncă trebuie să fie copertina ca să umbrească fereastra vara?'],
    keywords: ['sunshade', 'sunshades', 'shade', 'shading', 'sunlight', 'louvre', 'louvres', 'louver', 'louvers', 'brise', 'soleil', 'fins', 'pergola', 'blind', 'blinds', 'jalousie', 'eggcrate', 'perforated', 'overhang', 'facade', 'parasolar', 'umbrire', 'umbra', 'lamele', 'jaluzele', 'jaluzea', 'marchiza', 'aripioare'],
    a: 'Architecture → Sunshade (SN) puts one on the door or window you click, sized to its opening and following it; Sunshade along a facade takes two points (along a wall, or a facade line). The bar picks the type or a style: canopies (a slab, louvred or glass, on brackets, tie rods or posts), vertical fins, louvres (brise-soleil), an egg-crate, a perforated screen, an awning, a pergola, an external venetian blind. Sunshade types edits every parameter of a type with its drawing; Save as style… keeps one for the project. Selected over a window, Full shade at noon, 21 June sets the projection that keeps the whole window in shade when the sun is highest, at the project\'s place. They are drawn in plans (dashed over the cut), elevations, sections and 3D, cast shadows, have their schedule and quantities, and are IfcShadingDevice in IFC.',
    aRo: 'Architecture → Sunshade (SN) pune unul pe ușa sau fereastra pe care dai clic, cât golul ei, și o urmează; Sunshade along a facade ia două puncte (de-a lungul unui perete sau al unei linii de fațadă). Bara alege tipul sau un stil: copertine (placă, cu lamele sau din sticlă, pe console, tiranți sau stâlpi), aripioare verticale, lamele orizontale (brise-soleil), un egg-crate, un ecran perforat, o marchiză, o pergolă, o jaluzea exterioară. Sunshade types editează fiecare parametru al unui tip, cu desenul lui; Save as style… îl păstrează în proiect. Selectat peste o fereastră, Full shade at noon, 21 June pune ieșirea care ține toată fereastra în umbră când soarele e cel mai sus, în locul proiectului. Se desenează în plan (punctat peste secțiune), fațade, secțiuni și 3D, aruncă umbră, au extrasul și cantitățile lor și sunt IfcShadingDevice în IFC.',
    action: { label: 'Sunshade types', labelRo: 'Sunshade types', run: { kind: 'tool', id: 'architecture.sunshadeTypes' } },
    source: 'report #56',
  },
  {
    id: 'exhaust-fans',
    q: ['How do I add a bathroom fan or ventilator?', 'Can I place a customizable exhaust fan in the wall or ceiling?', 'Where are the bathroom ventilators?'],
    qRo: ['Cum pun un ventilator de baie?', 'Pot pune un ventilator de evacuare în perete sau în tavan?', 'Unde găsesc ventilatoarele?'],
    keywords: ['fan', 'fans', 'ventilator', 'ventilators', 'exhaust', 'extract', 'bathroom', 'airflow', 'humidity', 'ventilator', 'ventilatoare', 'ventilatorul', 'evacuare', 'baie', 'aerisire'],
    a: 'MEP tab → Fans: Wall fan (VF), Ceiling fan, Inline duct fan, Window fan, Ceiling fan with light and Fan with louvre panel. Click near a wall\'s face (a wall or window fan sits on it, its round sleeve cut through the wall) or in the room (a ceiling fan under its ceiling). Fan types… configures the duct size, the grille\'s shape, size, pattern and finish, the airflow, power, mounting, control (switch, humidity, timer, motion) and the backdraft shutter, with a preview; Lapin\'s built-in types are added with a click. Duct to outlet takes a ceiling or inline fan\'s air out through a wall or the roof. The Fan schedule lists them; the Room extract check compares each bathroom\'s fans with the extract its volume needs. IFC: IfcFan of an IfcFanType with Pset_FanTypeCommon.',
    aRo: 'Fila Instalații → Ventilatoare: Ventilator de perete (VF), Ventilator de tavan, Ventilator de tubulatură, Ventilator de geam, Ventilator de tavan cu lumină și Ventilator cu panou cu lamele. Dă clic lângă fața unui perete (unul de perete sau de geam stă pe ea, iar manșonul lui rotund taie peretele) sau în încăpere (unul de tavan, sub tavan). Tipuri de ventilatoare… configurează diametrul tubului, forma, mărimea, modelul și finisajul grilei, debitul, puterea, montajul, comanda (întrerupător, umiditate, temporizator, mișcare) și clapeta antiretur, cu previzualizare; tipurile incluse în Lapin se adaugă cu un clic. Tub spre gura de evacuare scoate aerul unui ventilator de tavan sau de tubulatură prin perete sau prin acoperiș. Extrasul de ventilatoare le listează; Verificarea evacuării compară ventilatoarele fiecărei băi cu debitul cerut de volumul ei. IFC: IfcFan al unui IfcFanType cu Pset_FanTypeCommon.',
    action: { label: 'Fan types…', labelRo: 'Tipuri de ventilatoare…', run: { kind: 'tool', id: 'mep.fanTypes' } },
    source: 'report #55',
  },
  {
    id: 'gates',
    q: ['How do I add a gate?', 'Can I design a sliding or swing gate for the fence?', 'Basic building gate components customizable to the user'],
    qRo: ['Cum pun o poartă?', 'Pot face o poartă culisantă sau batantă la gard?', 'Unde găsesc porțile?'],
    keywords: ['gate', 'gates', 'sliding', 'swing', 'cantilever', 'pedestrian', 'wicket', 'driveway', 'fence', 'pier', 'piers', 'poarta', 'porti', 'portita', 'culisanta', 'batanta', 'autoportanta', 'gard', 'stalpi'],
    a: 'Site tab → Gates: Swing gate, Double swing gate (GT), Sliding gate, Cantilever sliding gate, Pedestrian gate, Gate with side pedestrian door and Folding gate. Click the gate\'s two ends: with On lines in the bar it goes onto the nearest wall\'s axis or the parcel\'s edge (a gate on a fence wall cuts it); off, between the two points. Gate types… is the configurator, with Lapin\'s built-in styles and the project\'s own: width, height, gap, leaves (frame, bars, slats, solid, mesh, perforated sheet; flat, arched or pointed top, spears), piers (masonry with caps, steel posts), motor, lock and materials, with the engine\'s preview. Plans show the swing, or a sliding gate\'s parking zone as long as its leaf. IFC: IfcDoor GATE of an IfcDoorType.',
    aRo: 'Fila Teren → Porți: Poartă batantă, Poartă batantă dublă (GT), Poartă culisantă, Poartă autoportantă, Portiță, Poartă cu portiță alăturată și Poartă pliantă. Dă clic pe cele două capete ale porții: cu Pe linii pornit în bară se pune pe axul celui mai apropiat perete sau pe latura parcelei (o poartă pe un zid de gard îl taie); oprit, între cele două puncte. Tipuri de porți… e configuratorul, cu stilurile incluse în Lapin și cele ale proiectului: lățime, înălțime, distanța față de teren, canate (ramă, bare, lamele, plin, plasă, tablă perforată; coronament drept, arcuit sau ascuțit, sulițe), stâlpi (zidărie cu căciuli, oțel), motor, broască și materiale, cu previzualizarea motorului. Planurile arată arcul de deschidere sau zona de parcare a unei porți culisante, cât canatul. IFC: IfcDoor GATE al unui IfcDoorType.',
    action: { label: 'Gate types…', labelRo: 'Tipuri de porți…', run: { kind: 'tool', id: 'architecture.gateTypes' } },
    source: 'report #57',
  },
  {
    id: 'jg-structures',
    q: ['How do I export the structure for analysis in JG Structures?', 'Can I send the model to a structural analysis program?', 'How do I get an analytical model for FEA?'],
    qRo: ['Cum export structura pentru calcul în JG Structures?', 'Pot trimite modelul la un program de calcul structural?', 'Cum obțin modelul analitic pentru calcul cu elemente finite?'],
    keywords: ['jg', 'structures', 'analysis', 'analytical', 'fea', 'etabs', 'frame', 'frames', 'shells', 'nodes', 'calcul', 'structural', 'analitic', 'bare', 'noduri', 'elemente', 'finite'],
    a: 'File → Export JG Structures (JSON)… writes the analytical model in JG Structures\' exchange file: columns, beams and steel members as frames on their axes, load-bearing walls and floors as shells on their cores, on shared nodes, with the footings\' supports, the materials (C25/30, S355… by EN, or your own values) and the sections\' properties worked out from their geometry. Choose the storeys, the kinds and the units (the contract\'s cm and kgf, or SI); the report checks the file against the contract and lists what analysis still needs (a material without a class, a column on nothing). A wall counts when Pset_WallCommon.LoadBearing is on it or its type.',
    aRo: 'Fișier → Exportă JG Structures (JSON)… scrie modelul analitic în fișierul de schimb al lui JG Structures: stâlpii, grinzile și barele de oțel ca bare pe axele lor, pereții portanți și planșeele ca plăci pe miezurile lor, pe noduri comune, cu reazemele fundațiilor, materialele (C25/30, S355… după EN, sau valorile tale) și caracteristicile secțiunilor calculate din geometrie. Alegi etajele, tipurile și unitățile (cm și kgf ale contractului, sau SI); raportul verifică fișierul după contract și arată ce mai lipsește calculului (un material fără clasă, un stâlp care nu stă pe nimic). Un perete intră când are Pset_WallCommon.LoadBearing, el sau tipul lui.',
    action: { label: 'Export JG Structures (JSON)…', labelRo: 'Exportă JG Structures (JSON)…', run: { kind: 'dialog', id: 'exportJg' } },
    source: 'faq',
  },
];

/** Lower case, without diacritics, punctuation as spaces. */
export function normalise(text: string): string {
  return text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/ł/g, 'l').replace(/ø/g, 'o').replace(/æ/g, 'ae').replace(/å/g, 'a').replace(/ß/g, 'ss')
    .replace(/[^a-z0-9#+\p{Script=Han}]+/gu, ' ').trim();
}

/** Chinese is written without spaces: a Chinese keyword counts when the question contains it. */
const HAN = /\p{Script=Han}/u;

const STOP = new Set([
  'a', 'an', 'the', 'i', 'my', 'me', 'do', 'does', 'how', 'can', 'is', 'are', 'to', 'of', 'in', 'on', 'it', 'and', 'or', 'what', 'where', 'why', 'there', 'be', 'for', 'with', 'from', 'this', 'that', 'not', 'no',
  'cum', 'se', 'sa', 'si', 'in', 'la', 'de', 'un', 'o', 'pe', 'cu', 'ce', 'unde', 'de ce', 'este', 'e', 'pot', 'am', 'mi', 'imi', 'nu', 'din', 'care', 'fac',
  // The other interface languages' small words, so a question in Spanish, French… is read by its nouns.
  'como', 'las', 'los', 'el', 'en', 'una', 'uno', 'que', 'por', 'para', 'mis', 'del', 'al', 'es', 'puedo', 'donde', 'cambio', 'cambiar',
  'le', 'les', 'des', 'du', 'et', 'est', 'ou', 'comment', 'je', 'il', 'une', 'mon', 'ma', 'mes', 'peux', 'changer',
  'di', 'per', 'che', 'posso', 'dove', 'cambiare', 'nel', 'della', 'il',
  'die', 'der', 'das', 'und', 'wie', 'ich', 'ein', 'eine', 'kann', 'wo', 'mit', 'auf', 'zu', 'den', 'andern',
  'jak', 'ja', 'nie', 'czy', 'na', 'sie', 'gdzie', 'zmienic', 'moge',
  'hvordan', 'jeg', 'og', 'er', 'det', 'kan', 'hvor', 'til', 'af', 'en', 'et', 'aendre',
  'mudar', 'alterar', 'onde', 'os', 'as', 'um', 'uma', 'meu', 'minha', 'no', 'na',
]);

/** The words of a text that carry meaning. */
export function words(text: string): string[] {
  return normalise(text).split(' ').filter((w) => w.length > 1 && !STOP.has(w));
}

/** A word and another match when they share their start, five letters at least or all but the last two (unit / units, copiez / copiere; not trace / tracing). */
const near = (a: string, b: string) => a === b || (a.length >= 4 && b.length >= 4 && (a.startsWith(b.slice(0, Math.max(5, b.length - 2))) || b.startsWith(a.slice(0, Math.max(5, a.length - 2)))));

export interface Hit { entry: Entry; score: number }

/**
 * The entries that answer a question, best first: a keyword the question uses
 * counts 2, a word shared with one of the entry's questions 1; only entries
 * that reach 2 are answers.
 */
export function ask(question: string, faq: Entry[] = FAQ): Hit[] {
  const ws = words(question);
  if (ws.length === 0) return [];
  const plain = normalise(question);
  return faq
    .map((entry) => {
      const keys = entry.keywords.map(normalise);
      const asked = new Set([...entry.q, ...entry.qRo].flatMap(words));
      let score = 0;
      if (HAN.test(plain)) for (const k of keys) if (HAN.test(k) && plain.includes(k)) score += 2;
      for (const w of ws) {
        if (keys.some((k) => near(w, k))) score += 2;
        else if ([...asked].some((k) => near(w, k))) score += 1;
      }
      return { entry, score };
    })
    .filter((h) => h.score >= 2)
    .sort((a, b) => b.score - a.score);
}
