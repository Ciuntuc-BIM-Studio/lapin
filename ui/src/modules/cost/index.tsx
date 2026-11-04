/**
 * Cost: what the model measures and what it costs. The quantities (decision 287, the tester's report #75): the quantity
 * takeoff, every other schedule the edition makes, the takeoff as a workbook; schedules are the Architecture module's
 * and go on sheets like views. The estimate (docs/plan-4d5d.md, stage 6): the user's price lists, the articles on types
 * and the rules on materials (Map articles), the estimate worked out from the model, its Romanian forms (F3, F2,
 * C6–C9) on sheets, in a workbook and a PDF. The tab was Quantities until the estimate came.
 */

import { Calculator, FileSpreadsheet, FileText, ListChecks, Sheet, Sigma, Table2, Tags } from 'lucide-react';
import type { LapinModule } from '../../platform/module';
import { useUi } from '../../app/store';
import { SchedulesDialog } from './SchedulesDialog';
import { PriceListsDialog } from './PriceListsDialog';
import { MapArticlesDialog } from './MapArticlesDialog';
import { EstimateView } from './EstimateView';
import { exportPdf, exportXlsx, formOnSheet, openEstimate, useEstimate } from './estimate';
import { exportTakeoff, takeoff } from './schedules';

const cost: LapinModule = {
  id: 'cost',
  name: 'Cost',
  summary: 'Quantities and the estimate from the model: the quantity takeoff, every schedule, your price lists, articles on types, the estimate with its Romanian forms, XLSX, PDF and IFC.',
  tabs: [{
    id: 'cost', label: 'Cost', order: 85,
    groups: [
      {
        label: 'Take-off', tools: [
          { id: 'cost.takeoff', label: 'Quantity takeoff', icon: Sigma, run: () => void takeoff() },
          { id: 'cost.schedules', label: 'Schedules…', icon: Table2, run: () => useUi.setState({ dialog: 'schedules' }) },
        ],
        small: [{ id: 'cost.xlsx', label: 'Export XLSX', icon: FileSpreadsheet, run: () => void exportTakeoff() }],
      },
      {
        label: 'Prices', tools: [
          { id: 'cost.prices', label: 'Price lists…', icon: ListChecks, run: () => useUi.setState({ dialog: 'cost.prices' }) },
          { id: 'cost.map', label: 'Map articles…', icon: Tags, run: () => useUi.setState({ dialog: 'cost.map' }) },
        ],
      },
      {
        label: 'Estimate', tools: [
          { id: 'cost.estimate', label: 'Estimate…', icon: Calculator, run: () => void openEstimate() },
        ],
        small: [
          { id: 'cost.formSheet', label: 'Form on sheet', icon: Sheet, run: () => void formOnSheet(useEstimate.getState().form) },
          { id: 'cost.estimateXlsx', label: 'Estimate XLSX', icon: FileSpreadsheet, run: () => void exportXlsx() },
          { id: 'cost.estimatePdf', label: 'Estimate PDF', icon: FileText, run: () => void exportPdf() },
        ],
      },
    ],
  }],
  views: { estimate: { label: 'Estimate', component: EstimateView } },
  dialogs: { schedules: SchedulesDialog, 'cost.prices': PriceListsDialog, 'cost.map': MapArticlesDialog },
};

export default cost;
