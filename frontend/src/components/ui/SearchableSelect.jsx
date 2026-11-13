import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { normalizeText } from '@/lib/vnAdministrative';

const MAX_RENDERED = 200;

/**
 * Combobox chọn từ danh sách và gõ để tìm kiếm (không cho nhập giá trị tự do).
 * Mẫu WAI-ARIA combobox + listbox; hỗ trợ bàn phím: ↑ ↓ Enter Esc Tab.
 * - value: option đang chọn { value, label } hoặc null
 * - options: [{ value, label }]
 */
export default function SearchableSelect({
  id, value, options, onChange, placeholder = 'Chọn…', disabled = false, loading = false,
  error = null, emptyText = 'Không có kết quả phù hợp', className = '', onRetry,
  remote = false, onSearch,
}) {
  const reactId = useId();
  const inputId = id ?? `ss-${reactId}`;
  const listId = `${inputId}-list`;
  const wrapRef = useRef(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [active, setActive] = useState(0);

  const filtered = useMemo(() => {
    // remote: danh sách do nơi gọi tìm sẵn (API), không lọc lại ở đây.
    if (remote) return options.slice(0, MAX_RENDERED);
    const q = normalizeText(query);
    const rows = q ? options.filter((o) => normalizeText(o.label).includes(q)) : options;
    return rows.slice(0, MAX_RENDERED);
  }, [options, query, remote]);

  useEffect(() => {
    const close = (e) => { if (wrapRef.current && !wrapRef.current.contains(e.target)) { setOpen(false); setQuery(''); } };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  useEffect(() => { setActive(0); }, [query, options]);

  const choose = (option) => { onChange(option); setOpen(false); setQuery(''); };
  const optionId = (i) => `${listId}-${i}`;

  const onKeyDown = (e) => {
    if (disabled) return;
    if (e.key === 'ArrowDown') { e.preventDefault(); setOpen(true); setActive((i) => Math.min(i + 1, filtered.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(i - 1, 0)); }
    else if (e.key === 'Enter' && open) { e.preventDefault(); if (filtered[active]) choose(filtered[active]); }
    else if (e.key === 'Escape') { setOpen(false); setQuery(''); }
    else if (e.key === 'Tab') { setOpen(false); setQuery(''); }
  };

  const shown = open ? query : (value?.label ?? '');
  const status = loading ? 'Đang tải…' : error ? error : null;

  return (
    <div ref={wrapRef} className="relative">
      <input
        id={inputId}
        type="text"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && filtered[active] ? optionId(active) : undefined}
        aria-invalid={error ? true : undefined}
        autoComplete="off"
        disabled={disabled}
        className={`${className} pr-9 ${disabled ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'}`}
        value={shown}
        placeholder={loading ? 'Đang tải…' : placeholder}
        onFocus={() => !disabled && setOpen(true)}
        onClick={() => !disabled && setOpen(true)}
        onChange={(e) => { setQuery(e.target.value); setOpen(true); onSearch?.(e.target.value); }}
        onKeyDown={onKeyDown}
      />
      <span aria-hidden="true" className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-d-on-surface-variant text-xs">▾</span>
      {open && !disabled && (
        <ul
          id={listId}
          role="listbox"
          className="absolute z-30 mt-1 max-h-64 w-full overflow-y-auto rounded-xl border border-d-border-subtle bg-d-surface-container-lowest py-1 shadow-lg"
        >
          {status && (
            <li role="presentation" className="px-4 py-2 text-sm text-d-on-surface-variant">
              {status}
              {error && onRetry && <button type="button" className="ml-2 font-semibold text-d-primary underline" onClick={onRetry}>Thử lại</button>}
            </li>
          )}
          {!status && filtered.length === 0 && <li role="presentation" className="px-4 py-2 text-sm text-d-on-surface-variant">{emptyText}</li>}
          {!status && filtered.map((o, i) => (
            <li
              key={o.value}
              id={optionId(i)}
              role="option"
              aria-selected={value?.value === o.value}
              onMouseEnter={() => setActive(i)}
              onMouseDown={(e) => { e.preventDefault(); choose(o); }}
              className={`cursor-pointer px-4 py-2 text-sm ${i === active ? 'bg-d-primary-container text-d-on-primary-container' : 'text-d-on-surface'} ${value?.value === o.value ? 'font-semibold' : ''}`}
            >
              {o.label}
              {o.hint && <span className="block text-xs opacity-70">{o.hint}</span>}
            </li>
          ))}
          {!status && options.length > MAX_RENDERED && filtered.length === MAX_RENDERED && (
            <li role="presentation" className="px-4 py-2 text-xs text-d-on-surface-variant">Gõ thêm để thu hẹp kết quả…</li>
          )}
        </ul>
      )}
    </div>
  );
}
