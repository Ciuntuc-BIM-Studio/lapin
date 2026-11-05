/**
 * The company's style for a document or a template (docs/plan-documents.md, stage 4; `page.style`): the text's font in DOCX and
 * ODT, the PDF's font (Helvetica or Times, the PDF's standard fonts), the body text's size, the headings' colour and the logo in
 * the header (a PNG or JPEG, its width in mm, left or right), with a sample of the page's top as it will look.
 * `StyleForm` edits a style it is given; `DocumentStyleForm` reads and sets a document's through the engine.
 */

import { useEffect, useRef, useState } from 'react';
import { ImagePlus, RotateCcw, Trash2 } from 'lucide-react';
import { notify } from '../../../app/store';
import { useSession } from '../../../engine/session';
import { pickFile, toBase64 } from '../../../engine/files';
import { documentStyle, setDocumentStyle } from './documents';
import { MAX_LOGO_BYTES, STYLE_FONTS, type DocStyle } from './templateLibrary';

/** The style with a field set or taken away (undefined: Lapin's own); null when nothing is left. */
export function setField<K extends keyof DocStyle>(style: DocStyle, key: K, value: DocStyle[K] | undefined): DocStyle | null {
  const next: DocStyle = { ...style };
  if (value === undefined || value === '') delete next[key]; else next[key] = value;
  return Object.keys(next).length ? next : null;
}

/** A logo's picture as a data URL, by its first bytes (PNG or JPEG). */
const logoUrl = (data: string) => `data:${data.startsWith('/9j/') ? 'image/jpeg' : 'image/png'};base64,${data}`;

/** The page's top as the files will show it, to scale: the logo and the header, a heading, a line of text (A4, Lapin's margins). */
function StyleSample({ style }: { style: DocStyle }) {
  const k = 2.4;                                  // px a mm
  const pt = (v: number) => v * (25.4 / 72) * k;  // a size in points as px at this scale
  const scale = (style.size ?? 10.5) / 10.5;
  const font = `"${style.font ?? 'Arial'}", ${/times|georgia|serif/i.test(style.font ?? '') ? 'serif' : 'sans-serif'}`;
  const logo = style.logo;
  const right = logo?.position === 'right';
  // As the writers fit it: at most half the text width (165 mm), its height within the 10 mm the top margin leaves.
  const [ratio, setRatio] = useState(3);
  const w = logo ? Math.min(logo.w, 82.5, 10 * ratio) : 0;
  return (
    <div className="tpl-sample" data-field="tpl-style-sample" style={{ fontFamily: font, padding: `${8 * k}px ${20 * k}px ${6 * k}px ${25 * k}px` }}>
      <div className="tpl-sample-head" style={{ flexDirection: right ? 'row-reverse' : 'row', gap: 4 * k, minHeight: 10 * k }}>
        {logo && <img src={logoUrl(logo.data)} alt="" style={{ width: w * k }} onLoad={(e) => { const i = e.currentTarget; if (i.naturalHeight) setRatio(i.naturalWidth / i.naturalHeight); }} />}
        <span data-no-i18n style={{ fontSize: pt(9 * scale) }}>Ciuntuc BIM Studio · Memoriu tehnic de arhitectură</span>
      </div>
      <div style={{ marginTop: 2 * k }}>
        <div data-no-i18n style={{ color: style.headingColor ?? '#000', fontWeight: 700, fontSize: pt(16 * scale) }}>1. Date generale</div>
        <p data-no-i18n style={{ fontSize: pt(10.5 * scale), margin: `${1.5 * k}px 0 0` }}>Clădirea propusă are regimul de înălțime P+1E, cu structura din zidărie portantă confinată.</p>
      </div>
    </div>
  );
}

/** The style's fields over a style given; each change handed to `onChange` (null: Lapin's own look). */
export function StyleForm({ style, onChange, disabled }: { style: DocStyle; onChange: (s: DocStyle | null) => void; disabled?: boolean }) {
  const [size, setSize] = useState(style.size != null ? String(style.size) : '');
  const [color, setColor] = useState(style.headingColor ?? '#000000');
  const [width, setWidth] = useState(String(style.logo?.w ?? 30));
  useEffect(() => { setSize(style.size != null ? String(style.size) : ''); }, [style.size]);
  useEffect(() => { setColor(style.headingColor ?? '#000000'); }, [style.headingColor]);
  useEffect(() => { setWidth(String(style.logo?.w ?? 30)); }, [style.logo?.w]);
  // A colour dragged in the picker is sent once it rests.
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  useEffect(() => () => { if (timer.current) clearTimeout(timer.current); }, []);

  const put = <K extends keyof DocStyle>(key: K, value: DocStyle[K] | undefined) => onChange(setField(style, key, value));
  const commitSize = () => {
    const v = Number(size.replace(',', '.'));
    if (!size.trim()) put('size', undefined);
    else if (Number.isFinite(v) && v >= 6 && v <= 24) { if (v !== style.size) put('size', v); }
    else { notify('The text size is between 6 and 24 pt.'); setSize(style.size != null ? String(style.size) : ''); }
  };
  const commitWidth = () => {
    const v = Number(width.replace(',', '.'));
    if (style.logo && Number.isFinite(v) && v > 0 && v <= 120) { if (v !== style.logo.w) put('logo', { ...style.logo, w: v }); }
    else setWidth(String(style.logo?.w ?? 30));
  };
  const pickLogo = async () => {
    const f = await pickFile('Logo (PNG, JPEG)', ['png', 'jpg', 'jpeg']);
    if (!f) return;
    if (f.bytes.length > MAX_LOGO_BYTES) { notify('The logo is over 1 MB: save it smaller (a few hundred pixels wide is enough).'); return; }
    put('logo', { data: toBase64(f.bytes), w: style.logo?.w ?? 30, position: style.logo?.position ?? 'left' });
  };

  return (
    <div className="tpl-style" data-field="tpl-style">
      <div className="tpl-style-grid">
        <label className="doc-info-row"><span className="muted small">Font (DOCX, ODT)</span>
          <select value={style.font ?? ''} disabled={disabled} onChange={(e) => put('font', e.target.value || undefined)} data-field="tpl-style-font">
            <option value="">Arial (Lapin's)</option>
            {STYLE_FONTS.filter((f) => f !== 'Arial').map((f) => <option key={f} value={f} data-no-i18n>{f}</option>)}
            {style.font && !STYLE_FONTS.includes(style.font) && <option value={style.font} data-no-i18n>{style.font}</option>}
          </select>
        </label>
        <label className="doc-info-row"><span className="muted small">PDF font</span>
          <select value={style.pdfFont ?? ''} disabled={disabled} onChange={(e) => put('pdfFont', (e.target.value || undefined) as DocStyle['pdfFont'])} data-field="tpl-style-pdf-font">
            <option value="">As the font (Times for a serif one)</option>
            <option value="helvetica">Helvetica</option>
            <option value="times">Times</option>
          </select>
        </label>
        <label className="doc-info-row"><span className="muted small">Text size (pt)</span>
          <input type="number" min={6} max={24} step={0.5} value={size} placeholder="10.5" disabled={disabled} onChange={(e) => setSize(e.target.value)}
            onBlur={commitSize} onKeyDown={(e) => { if (e.key === 'Enter') commitSize(); }} data-field="tpl-style-size" />
        </label>
        <label className="doc-info-row"><span className="muted small">Headings' colour</span>
          <span className="row tight">
            <input type="color" value={color} disabled={disabled} data-field="tpl-style-color" onChange={(e) => {
              const v = e.target.value;
              setColor(v);
              if (timer.current) clearTimeout(timer.current);
              timer.current = setTimeout(() => put('headingColor', v), 350);
            }} />
            {style.headingColor && <button type="button" className="icon mini" title="Black, as Lapin's" disabled={disabled} onClick={() => put('headingColor', undefined)}><RotateCcw size={12} /></button>}
          </span>
        </label>
      </div>
      <div className="tpl-style-logo">
        <span className="muted small">Logo in the header</span>
        <div className="row">
          <button type="button" className="btn" disabled={disabled} onClick={() => void pickLogo()} data-field="tpl-style-logo"><ImagePlus size={13} />{style.logo ? 'Change logo…' : 'Choose logo…'}</button>
          {style.logo && <>
            <label className="row tight"><span className="muted small">Width (mm)</span>
              <input type="number" min={5} max={120} value={width} disabled={disabled} style={{ width: 64 }} onChange={(e) => setWidth(e.target.value)}
                onBlur={commitWidth} onKeyDown={(e) => { if (e.key === 'Enter') commitWidth(); }} data-field="tpl-style-logo-width" />
            </label>
            <select value={style.logo.position ?? 'left'} disabled={disabled} onChange={(e) => put('logo', { ...style.logo!, position: e.target.value as 'left' | 'right' })} data-field="tpl-style-logo-position">
              <option value="left">On the left</option>
              <option value="right">On the right</option>
            </select>
            <button type="button" className="icon mini" title="Remove the logo" disabled={disabled} onClick={() => put('logo', undefined)}><Trash2 size={12} /></button>
          </>}
        </div>
      </div>
      <StyleSample style={style} />
      {Object.keys(style).length > 0 && (
        <button type="button" className="link small" disabled={disabled} onClick={() => onChange(null)} data-field="tpl-style-reset"><RotateCcw size={11} /> Lapin's own look</button>
      )}
    </div>
  );
}

/** The open document's style, read from the engine and set there (one undo step a change). */
export function DocumentStyleForm({ documentId }: { documentId: string }) {
  const version = useSession((s) => s.version);
  const [style, setStyle] = useState<DocStyle | null>(null);
  useEffect(() => {
    let live = true;
    void documentStyle({ id: documentId }).then((s) => { if (live) setStyle(s); });
    return () => { live = false; };
  }, [documentId, version]);
  if (!style) return null;
  return <StyleForm style={style} onChange={(s) => { setStyle(s ?? {}); void setDocumentStyle({ id: documentId }, s); }} />;
}
