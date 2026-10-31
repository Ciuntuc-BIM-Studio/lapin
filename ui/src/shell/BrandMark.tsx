import { brand } from '../platform/brand';

/** The product's name as its brand writes it: plain, or after its square mark when it has one (products/<id>.json). */
export function BrandName({ big = false }: { big?: boolean }) {
  if (!brand.mark) return <>{brand.name}</>;
  const [head, ...rest] = brand.name.split(' ');
  return (
    <span className={`brand-lockup${big ? ' big' : ''}`} data-no-i18n>
      <span className="brand-mark" aria-hidden>{brand.mark}</span>
      <span>{head}</span>{rest.length > 0 && <span className="brand-edition">{rest.join(' ')}</span>}
    </span>
  );
}
