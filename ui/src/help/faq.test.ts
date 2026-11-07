import { describe, expect, it } from 'vitest';
import { FAQ, ask, normalise } from './faq';

const best = (q: string) => ask(q)[0]?.entry.id;

describe('Ask Lapin', () => {
  it('answers the testers\' reports in their own words', () => {
    expect(best('Needs unit change option (m/Ft)')).toBe('units');
    expect(best('import of dwg/pdf should be allowed')).toBe('import-dwg-pdf');
    expect(best("Can't modify walls: I select a wall and try to drag a copy and nothing happens")).toBe('copy-move');
    expect(best('Material Joints not working in aligned walls')).toBe('material-joints');
    expect(best('convert dxf file into a locked layer for guidance! I imported my dxf file. I want to lock the entire dxf file into a group')).toBe('dxf-locked-guide');
    expect(best('how to create 2 pane, 3 pane , 4 pane windows? I want to edit a 2 pane window to 2,3,4,6 pane windows with openable shutters.')).toBe('window-panels');
    expect(best('Window Safety grilles: it would be very cool to add window safety grilles to my windows as it is common in india')).toBe('window-dressings');
    expect(best('Mansard roof looks odd, gable walls not cut')).toBe('roof-gables');
    expect(best("I can't seem to move the ground from one level to another, even though I changed it's placement in the properties menu. I also tried to move it manually, and didn't work")).toBe('terrain-height');
    expect(best('I expected to be able to edit the column type that I created. I typed a dimension wrong and wanted to corect the mistake.')).toBe('edit-structure-type');
    expect(best("is it possible to edit the text font and size and style of the tags ( matching the studio's styles). i have attached an online reference of what i expected.")).toBe('tag-text-style');
    expect(best('I want to make a custom title block. To look like the official Finnish title block. I cannot do it?')).toBe('title-block');
    expect(best("I can't figure out how to make Levels invisible in 3D view so that I can navigate around in 3D. At the moment I can only watch the whole building at once.")).toBe('storeys-3d');
    expect(best('hope in the next release we can easily copy elements to multiple levels at once')).toBe('copy-to-storeys');
    expect(best('copy elements to several storeys')).toBe('copy-to-storeys');
    // Report #96, in the tester's words.
    expect(best('Different types of balcony: Juliet balcony, loggia balcony, stacked balcony, mezzanine balcony')).toBe('balconies');
    expect(best('how do I make a loggia')).toBe('balconies');
    expect(best('Customizable elevator with a configurator: architects adjust the dimensions by building type, traffic and accessibility')).toBe('elevators');
    expect(best('elevator kinds: small residential, standard passenger, hospital, goods, hydraulic, MRL, panoramic glass, home lift')).toBe('elevators');
    expect(best('cum pun un lift pentru scaun rulant')).toBe('elevators');
    expect(best('Include Bay, Bow and Box windows for better window design customization. Make every component customizable.')).toBe('bay-windows');
    expect(best('various sunshade design types, with customizable shapes, dimensions, materials and projection lengths, to improve façade aesthetics, control sunlight, and energy efficiency')).toBe('sunshades');
  });

  it('answers in Romanian, with or without diacritics', () => {
    expect(best('cum ascund taburile pe care nu le folosesc')).toBe('workspaces');
    expect(best('I only want to see the structure and rebar tabs')).toBe('workspaces');
    expect(best('cum schimb unitățile în picioare')).toBe('units');
    expect(best('cum schimb unitatile')).toBe('units');
    expect(best('copiere perete')).toBe('copy-move');
    expect(best('cum pun obloane la fereastra')).toBe('window-dressings');
    expect(best('fereastra cu trei canate')).toBe('window-panels');
    expect(best('cum blochez desenul dxf ca ghidaj')).toBe('dxf-locked-guide');
    expect(best('cum fac o secțiune')).toBe('sections');
    expect(best('vreau să export pdf')).toBe('sheets-pdf');
    expect(best('cum pun o textură proprie pe un material')).toBe('textures');
    expect(best('import my own .pat hatch patterns')).toBe('pat');
    expect(best('is there a user manual?')).toBe('guide');
    expect(best('unde gasesc documentatia')).toBe('guide');
    expect(best('how do I colour rooms by function')).toBe('rooms');
    expect(best('cum colorez încăperile pe plan')).toBe('rooms');
    expect(best('cum ascund etajele în 3D')).toBe('storeys-3d');
    expect(best('cum copiez elemente pe mai multe etaje')).toBe('copy-to-storeys');
    expect(best('copiere pe mai multe niveluri deodată')).toBe('copy-to-storeys');
    expect(best('cum fac un bovindou')).toBe('bay-windows');
    expect(best('how do I draw a line on a face in 3D like sketchup')).toBe('studio-sketch-3d');
    expect(best('cum desenez o linie pe fata in 3D')).toBe('studio-sketch-3d');
    expect(best('is there a tape measure with guides like sketchup')).toBe('studio-sketch-3d');
    expect(best('parasolar cu lamele peste fereastra')).toBe('sunshades');
    expect(best('how do I orbit the 3D view')).toBe('navigate');
    expect(best('cum cobor terenul')).toBe('terrain-height');
    expect(best('cum modific un tip de stalp')).toBe('edit-structure-type');
    // Report #44, in the tester's words, and the question as the user put it.
    expect(best('How to change the overlap 2D annotation objects')).toBe('draw-order');
    expect(best('how do I change which drawing is on top')).toBe('draw-order');
    expect(best('cum schimb ce desen e deasupra')).toBe('draw-order');
    expect(best('cum trimit hasura in spate')).toBe('draw-order');
    // Report #92.
    expect(best('usually the opening height is displayed below the dimension line')).toBe('opening-height');
    expect(best('cum arat inaltimea golului sub cota')).toBe('opening-height');
    expect(best('pot să-mi fac propriul cartuș?')).toBe('title-block');
    expect(best('can I make my own title block')).toBe('title-block');
    expect(best('cum fac un balcon in consola')).toBe('balconies');
    expect(best('cum pun o logie in perete')).toBe('balconies');
    // Report #108.
    expect(best('3D preview of the window in the configurator')).toBe('joinery-3d-preview');
    expect(best('previzualizare 3D a ferestrei in configurator')).toBe('joinery-3d-preview');
    // Report #107, in the tester's words.
    expect(best('not able to edit wall dimensions individually')).toBe('wall-thickness');
    expect(best('how do I change the thickness of one wall')).toBe('wall-thickness');
    expect(best('cum schimb grosimea unui singur perete')).toBe('wall-thickness');
    // Reports #55 and #57.
    expect(best('a provision for customizable bathroom ventilators and fan like in the reference')).toBe('exhaust-fans');
    expect(best('cum pun un ventilator in baie')).toBe('exhaust-fans');
    expect(best('basic building gate components customizable to the user')).toBe('gates');
    expect(best('how do I draw a sliding gate')).toBe('gates');
    expect(best('cum fac o poarta culisanta la gard')).toBe('gates');
  });

  it('finds the most asked entries in the interface\'s other languages', () => {
    // Three testers' questions, in their own words.
    expect(best('como cambio las unidades')).toBe('units');
    expect(best('como cambio  a unidades en metros')).toBe('units');
    expect(best('¿cómo cambio las unidades?')).toBe('units');
    expect(best('puedo trabajar en pies y pulgadas')).toBe('units');
    expect(best('como mudar as unidades para metros')).toBe('units');
    expect(best('comment changer les unités')).toBe('units');
    expect(best('come cambiare le unità di misura')).toBe('units');
    expect(best('wie ändere ich die Einheiten')).toBe('units');
    expect(best('jak zmienić jednostki')).toBe('units');
    expect(best('hvordan ændrer jeg enheder')).toBe('units');
    expect(best('如何更改单位')).toBe('units');
    expect(best('¿dónde está el manual?')).toBe('guide');
    expect(best('como dibujo un muro')).toBe('walls');
    expect(best('como guardar el proyecto')).toBe('save-open');
    expect(best('cómo hago una sección')).toBe('sections');
    expect(best('如何打印图纸')).toBe('sheets-pdf');
  });

  it('says nothing to what it does not know', () => {
    expect(ask('weather tomorrow in Paris')).toEqual([]);
    expect(ask('can I render photorealistic images with ray tracing?')).toEqual([]);
    expect(ask('')).toEqual([]);
  });

  it('keeps its entries whole: ids unique, both languages, keywords already plain', () => {
    expect(new Set(FAQ.map((e) => e.id)).size).toBe(FAQ.length);
    for (const e of FAQ) {
      expect(e.q.length && e.qRo.length && e.a && e.aRo).toBeTruthy();
      for (const k of e.keywords) expect(normalise(k)).toBe(k);
    }
  });
});
