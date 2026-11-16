/**
 * What else drafted shapes can be made into, beside the converters' elements (audit S12): a definition made in its own dialog (a
 * symbol, a family, a profile). Registered by the module that has it, listed in Draft → Convert to… after the elements, and run
 * on the selection. Here in core, so a module registers one without needing the drafting module.
 */

export interface ConvertHandoff { id: string; label: string; description: string; run: () => void }

const handoffs: ConvertHandoff[] = [];

export function registerConvertHandoff(h: ConvertHandoff): void { if (!handoffs.some((x) => x.id === h.id)) handoffs.push(h); }

export const convertHandoffs = (): readonly ConvertHandoff[] => handoffs;
