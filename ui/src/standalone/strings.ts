/**
 * The phrases of the page Lapin exports for the browser (decision 482): the viewer's own, and the labels the engine
 * writes into the page (the estimate's columns and totals, the fields' Yes and No, the hint for whoever opens it). At
 * export the page carries the user's language's phrase book (phraseBook, ui/src/i18n), so the engine's labels (a class's
 * fields) are translated too; a phrase without a translation stays English. Every one here is in each language's file
 * (data.test.ts checks).
 */
export const VIEWER_PHRASES = [
  // The page and its tabs.
  '3D', 'Drawings', 'Properties', 'Open in Lapin',
  'A Lapin project. To work on it, open this file in Lapin: File → Open.',
  'A model exported from Lapin, to look at in a browser.',
  'Exported', 'Light theme', 'Dark theme',
  // The 3D view.
  'Fit', 'Isolate', 'Hide', 'Show all', 'All storeys', 'and below', 'Storey',
  'This page was exported without the 3D model.',
  'Your browser cannot show 3D here (WebGL is off).',
  // The drawings.
  'Floor plans', 'Sections', 'Elevations', 'Sheets', 'Views', 'No drawing was exported with this page.',
  'Fit the drawing', 'Print',
  // The properties.
  'Click an element in 3D, on a drawing or in a table to see its properties.',
  'Name', 'Class', 'Type', 'Material', 'Quantities', 'elements selected', 'Clear',
  // The tables (the estimate, from the cost module).
  'Estimate', 'Code', 'Description', 'Unit', 'Quantity', 'Unit price', 'Value',
  'Direct costs', 'Overheads', 'Profit', 'Total without VAT', 'VAT', 'Total',
  'Worked out from the model when the page was exported.',
  'Indicative: priced by Lapin\'s own rough values, not yours.',
  'Filter', 'Click a row to see its elements.',
  'Yes', 'No',
] as const;
