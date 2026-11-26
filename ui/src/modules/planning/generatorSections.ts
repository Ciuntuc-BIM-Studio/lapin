/**
 * Sections other modules add to Planning → Generate schedule… (docs/plan-extension-modules.md §6.3: an extension's
 * procurement chains), apart from React so it can be tested. A section is a tab of the configurator before Calendar,
 * editing its part of the spec (`spec[id]` by default: the key of its engine contributor, IGeneratorContributor.Key, which
 * the generator keeps as it came); `defaults` is what the engine's contributor starts it from (generatorOptions'
 * `contributors[id]`), `preview` the engine's preview, so the section can show its own counts and notes.
 */

import type { ComponentType } from 'react';
import type { GeneratorOptions, GenSpec, PreviewDto } from './generator';

export interface GeneratorSectionProps<T = unknown> {
  value: T | undefined;
  onChange(next: T | undefined): void;
  spec: GenSpec;
  defaults: unknown;
  options: GeneratorOptions | null;
  preview: PreviewDto | null;
}

export interface GeneratorSection<T = unknown> {
  /** Its tab's id, and its part of the spec unless `read` and `write` say otherwise. */
  id: string;
  label: string;
  component: ComponentType<GeneratorSectionProps<T>>;
  read?(spec: GenSpec): T | undefined;
  write?(spec: GenSpec, value: T | undefined): GenSpec;
}

const sections: GeneratorSection[] = [];

/** A section added (one of the same id replaced); the function returned takes it away again. */
export function addGeneratorSection<T>(section: GeneratorSection<T>): () => void {
  const s = section as unknown as GeneratorSection;
  const at = sections.findIndex((x) => x.id === s.id);
  if (at >= 0) sections[at] = s; else sections.push(s);
  return () => { const i = sections.indexOf(s); if (i >= 0) sections.splice(i, 1); };
}

/** The sections added, in the order they came. */
export function generatorSections(): readonly GeneratorSection[] { return sections; }

/** A section's value in a spec: its `read`, else the spec's key of its id. */
export function sectionValue(section: GeneratorSection, spec: GenSpec): unknown {
  return section.read ? section.read(spec) : (spec as unknown as Record<string, unknown>)[section.id];
}

/** The spec with a section's value set: its `write`, else the key of its id (taken out when undefined). */
export function withSectionValue(section: GeneratorSection, spec: GenSpec, value: unknown): GenSpec {
  if (section.write) return section.write(spec, value);
  const next = { ...spec } as unknown as Record<string, unknown>;
  if (value === undefined) delete next[section.id]; else next[section.id] = value;
  return next as unknown as GenSpec;
}
