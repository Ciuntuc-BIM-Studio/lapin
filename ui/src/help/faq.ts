/**
 * Ask Lapin: questions and answers about using Lapin, a classic FAQ (no AI).
 * Each entry has the questions people ask (English and Romanian), the words
 * that point to it, the answer in both languages, and, where the answer is a
 * place in the interface, a button that opens it.
 *
 * Adding an entry: write the questions the way a user would type them, the
 * keywords (and their Romanian, without diacritics), the answer, and `source`:
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
  /** Words that point to this entry, in English and Romanian (without diacritics). */
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
    keywords: ['unit', 'units', 'metre', 'meter', 'metres', 'meters', 'feet', 'foot', 'ft', 'inch', 'inches', 'imperial', 'metric', 'mm', 'cm', 'millimetres', 'unitate', 'unitati', 'masura', 'metri', 'centimetri', 'milimetri', 'picioare', 'toli'],
    a: 'Manage → Drafting standards → Project → Units (or click Units at the bottom of the window): millimetres, centimetres, metres, feet and inches, or inches. Every length in the panels, the live input, the command line and the dimensions follows it; the project keeps metres inside, so changing it never changes the model. A dimension style can also have a unit of its own.',
    aRo: 'Manage → Drafting standards → Project → Units (sau clic pe Units în bara de jos a ferestrei): milimetri, centimetri, metri, picioare și inch sau inch. Toate lungimile din panouri, din introducerea directă, din linia de comandă și din cote o urmează; proiectul ține intern metri, deci schimbarea nu modifică modelul. Un stil de cotă poate avea și o unitate proprie.',
    action: { label: 'Open Drafting standards', labelRo: 'Deschide Drafting standards', run: { kind: 'standards' } },
    source: 'report #23',
  },
  {
    id: 'import-dwg-pdf',
    q: ['Can I import a DWG or a PDF?', 'How do I bring in a CAD drawing?', 'Can I trace over a plan from AutoCAD?'],
    qRo: ['Pot importa un DWG sau un PDF?', 'Cum aduc un desen din AutoCAD?', 'Cum pun un plan CAD ca fundal?'],
    keywords: ['import', 'dwg', 'dxf', 'pdf', 'autocad', 'cad', 'drawing', 'trace', 'underlay', 'background', 'link', 'importa', 'desen', 'fundal', 'plan'],
    a: 'DXF, yes: Draft → Import DXF brings its lines into the plan as drafting you can snap to and edit, and File → Link lets you keep a DXF (or an IFC) linked read-only. DWG: save it as DXF from your CAD program (AutoCAD, BricsCAD, DraftSight: Save As → DXF), then import that. PDF import is not there yet.',
    aRo: 'DXF, da: Draft → Import DXF aduce liniile în plan, ca desen pe care faci snap și pe care îl poți edita, iar File → Link păstrează un DXF (sau un IFC) legat, needitabil. DWG: salvează-l ca DXF din programul CAD (AutoCAD, BricsCAD, DraftSight: Save As → DXF), apoi importă-l. Importul de PDF nu există încă.',
    action: { label: 'Import DXF', labelRo: 'Importă DXF', run: { kind: 'tool', id: 'drafting.importDxf' } },
    source: 'report #23',
  },
  {
    id: 'copy-move',
    q: ['How do I copy a wall?', 'How do I move an element?', 'I drag a wall and nothing happens'],
    qRo: ['Cum copiez un perete?', 'Cum mut un element?', 'Trag de un perete și nu se întâmplă nimic'],
    keywords: ['copy', 'copies', 'duplicate', 'move', 'drag', 'paste', 'clone', 'copiez', 'copiere', 'copie', 'mut', 'mutare', 'muta', 'trag', 'duplic'],
    a: 'Select the element, then Copy (or type CO and Enter): click a base point and a second point; it keeps copying until Enter or Esc. Move (M) works the same. Or drag: drag the cross in the middle of a selected element to move it, hold Ctrl (⌘ on a Mac) while dragging it to drop a copy. A press and a release also give a command its two points.',
    aRo: 'Selectează elementul, apoi Copy (sau scrie CO și Enter): un clic pentru punctul de bază și unul pentru al doilea punct; copiază până la Enter sau Esc. Move (M) merge la fel. Sau trage: crucea din mijlocul unui element selectat îl mută, iar cu Ctrl (⌘ pe Mac) ținut lasă o copie. Apăsarea și eliberarea mouse-ului dau și ele cele două puncte ale unei comenzi.',
    action: { label: 'Start Copy', labelRo: 'Pornește Copy', run: { kind: 'tool', id: 'modify.copy' } },
    source: 'report #21',
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
    keywords: ['wall', 'walls', 'draw', 'type', 'layers', 'thickness', 'perete', 'pereti', 'desenez', 'tip', 'straturi', 'grosime'],
    a: 'Architecture → Wall (or type WA): click the start, then each corner; Enter or Esc ends. Corners join on their own. The type (its layers and thickness) is chosen in the bar while drawing, or changed for a selected wall in the inspector; Architecture → Wall types edits the types.',
    aRo: 'Architecture → Wall (sau scrie WA): un clic la început, apoi la fiecare colț; Enter sau Esc termină. Colțurile se îmbină singure. Tipul (straturile, grosimea) se alege din bară în timp ce desenezi sau se schimbă în inspector pentru peretele selectat; Architecture → Wall types editează tipurile.',
    action: { label: 'Draw a wall', labelRo: 'Desenează un perete', run: { kind: 'tool', id: 'architecture.wall' } },
    source: 'faq',
  },
  {
    id: 'levels',
    q: ['How do I add a level?', 'How do I set the height of a storey?', 'Can a level be below zero?'],
    qRo: ['Cum adaug un nivel?', 'Cum setez înălțimea unui etaj?', 'Poate fi un nivel sub cota zero?'],
    keywords: ['level', 'levels', 'storey', 'storeys', 'story', 'floor', 'height', 'elevation', 'basement', 'top', 'base', 'nivel', 'niveluri', 'etaj', 'etaje', 'inaltime', 'cota', 'subsol', 'baza', 'varf'],
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
    keywords: ['section', 'sections', 'cut', 'elevation', 'elevations', 'facade', 'view', 'sectiune', 'sectiuni', 'fatada', 'fatade', 'vedere'],
    a: 'In the project browser, Sections → + then draw the cut line in a plan (two clicks, then the side it looks to); Elevations → + makes the four elevations. Each opens as a tab and is drawn from the model, kept up to date.',
    aRo: 'În arborele proiectului, Sections → +, apoi desenezi linia de secțiune în plan (două clicuri, apoi partea în care privește); Elevations → + face cele patru fațade. Fiecare se deschide într-un tab și se desenează din model, mereu la zi.',
    source: 'faq',
  },
  {
    id: 'sheets-pdf',
    q: ['How do I print or export a PDF?', 'How do I make a sheet?', 'Can I export DXF?'],
    qRo: ['Cum tipăresc sau export un PDF?', 'Cum fac o planșă?', 'Pot exporta DXF?'],
    keywords: ['print', 'pdf', 'export', 'sheet', 'sheets', 'layout', 'paper', 'dxf', 'tipar', 'tiparesc', 'export', 'plansa', 'planse', 'hartie'],
    a: 'In the project browser, Sheets → + makes a sheet; drag views onto it. In a sheet\'s bar, PDF and DXF export it. Views keep their scale and stay linked to the model.',
    aRo: 'În arborele proiectului, Sheets → + face o planșă; tragi vederile pe ea. Din bara planșei, PDF și DXF o exportă. Vederile își păstrează scara și rămân legate de model.',
    source: 'faq',
  },
  {
    id: 'save-open',
    q: ['How do I save my project?', 'Where is my file?', 'How do I open a project?'],
    qRo: ['Cum salvez proiectul?', 'Unde e fișierul meu?', 'Cum deschid un proiect?'],
    keywords: ['save', 'open', 'file', 'project', 'lapin', 'download', 'lost', 'recover', 'salvez', 'deschid', 'fisier', 'proiect', 'pierdut', 'recuperez'],
    a: 'File → Save (Ctrl S) writes a .lapin file. On the desktop it saves next to where you chose; in the browser Chrome and Edge save to the same file, Safari and Firefox download it. Unsaved work is kept as you go: after a crash the start screen offers to recover it.',
    aRo: 'File → Save (Ctrl S) scrie un fișier .lapin. Pe desktop îl salvează unde ai ales; în browser, Chrome și Edge salvează în același fișier, Safari și Firefox îl descarcă. Lucrul nesalvat e păstrat pe parcurs: după o cădere, ecranul de start oferă recuperarea lui.',
    source: 'faq',
  },
  {
    id: 'language',
    q: ['How do I switch to Romanian or Spanish?', 'Can I change the language?'],
    qRo: ['Cum trec interfața în engleză?', 'Pot schimba limba?'],
    keywords: ['language', 'romanian', 'english', 'spanish', 'translate', 'limba', 'romana', 'engleza', 'spaniola', 'traducere', 'espanol', 'idioma'],
    a: 'Help (?) → English · Română · Español switches the interface; it is remembered on this computer. The user guide is in English and Romanian.',
    aRo: 'Help (?) → English · Română · Español schimbă limba interfeței; alegerea se ține minte pe acest calculator. Ghidul utilizatorului e în engleză și română.',
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
    keywords: ['manual', 'guide', 'documentation', 'docs', 'tutorial', 'tutorials', 'help', 'learn', 'ghid', 'documentatie', 'tutoriale', 'invat'],
    a: 'The user guide at lapin.ciuntucbimstudio.ro/docs shows, page by page with pictures from the app, how to model walls, doors and windows, floors and roofs, structure, views, sheets and exports. It is also on ? → User guide.',
    aRo: 'Ghidul utilizatorului, la lapin.ciuntucbimstudio.ro/docs, arată pagină cu pagină, cu imagini din aplicație, cum modelezi pereți, uși și ferestre, planșee și acoperișuri, structura, vederile, planșele și exporturile. Îl găsești și la ? → User guide.',
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
];

/** Lower case, without diacritics, punctuation as spaces. */
export function normalise(text: string): string {
  return text.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9#+]+/g, ' ').trim();
}

const STOP = new Set([
  'a', 'an', 'the', 'i', 'my', 'me', 'do', 'does', 'how', 'can', 'is', 'are', 'to', 'of', 'in', 'on', 'it', 'and', 'or', 'what', 'where', 'why', 'there', 'be', 'for', 'with', 'from', 'this', 'that', 'not', 'no',
  'cum', 'se', 'sa', 'si', 'in', 'la', 'de', 'un', 'o', 'pe', 'cu', 'ce', 'unde', 'de ce', 'este', 'e', 'pot', 'am', 'mi', 'imi', 'nu', 'din', 'care', 'fac',
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
  return faq
    .map((entry) => {
      const keys = entry.keywords.map(normalise);
      const asked = new Set([...entry.q, ...entry.qRo].flatMap(words));
      let score = 0;
      for (const w of ws) {
        if (keys.some((k) => near(w, k))) score += 2;
        else if ([...asked].some((k) => near(w, k))) score += 1;
      }
      return { entry, score };
    })
    .filter((h) => h.score >= 2)
    .sort((a, b) => b.score - a.score);
}
