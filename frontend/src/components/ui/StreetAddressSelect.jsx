import { useEffect, useMemo, useState } from 'react';
import SearchableSelect from './SearchableSelect';
import { GOONG_ENABLED, searchPlaces } from '@/lib/goongPlaces';

const MIN_CHARS = 3;
const TYPED = '__typed__';

/**
 * Số nhà, tên đường: gõ để tìm gợi ý từ Goong rồi CHỌN (thu hẹp theo phường/xã + tỉnh/thành đã chọn).
 * Chưa cấu hình khóa Goong thì tự quay về ô nhập thường để vẫn dùng được.
 * - value: chuỗi dòng địa chỉ đã chọn ('' nếu chưa)
 * - area: { province, ward } từ VietnamAreaPicker
 */
export default function StreetAddressSelect({ id, value, onChange, area, className, placeholder }) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [failed, setFailed] = useState(false);
  const areaText = [area?.ward?.name, area?.province?.name].filter(Boolean).join(', ');
  const ready = Boolean(area?.ward && area?.province);

  useEffect(() => {
    if (!GOONG_ENABLED || !ready || query.trim().length < MIN_CHARS) { setResults([]); setSearching(false); setFailed(false); return undefined; }
    const controller = new AbortController();
    setSearching(true);
    const timer = setTimeout(async () => {
      try {
        setResults(await searchPlaces(`${query.trim()}, ${areaText}`, controller.signal));
        setFailed(false);
      } catch (err) {
        if (err.name !== 'AbortError') { setResults([]); setFailed(true); }
      } finally {
        if (!controller.signal.aborted) setSearching(false);
      }
    }, 350);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [query, areaText, ready]);

  const options = useMemo(() => {
    const rows = results.map((r) => ({ value: r.id, label: r.main, hint: r.secondary, line: r.line }));
    // Lối thoát khi Goong không có gợi ý (hẻm nhỏ, địa chỉ mới): cho dùng đúng nội dung đã gõ.
    if (!rows.length && !searching && !failed && query.trim().length >= MIN_CHARS) rows.push({ value: TYPED, label: query.trim(), hint: 'Không có gợi ý — dùng đúng nội dung đã gõ' });
    return rows;
  }, [results, searching, failed, query]);

  if (!GOONG_ENABLED) {
    return <input id={id} className={className} maxLength={500} value={value} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />;
  }

  const emptyText = query.trim().length < MIN_CHARS ? `Gõ ít nhất ${MIN_CHARS} ký tự để tìm gợi ý` : 'Không có gợi ý';

  return (
    <SearchableSelect
      id={id}
      className={className}
      remote
      disabled={!ready}
      placeholder={ready ? 'Gõ số nhà/tên đường rồi chọn gợi ý' : 'Chọn phường/xã trước'}
      value={value ? { value: 'selected', label: value } : null}
      options={options}
      loading={searching && options.length === 0}
      error={failed ? 'Không tải được gợi ý địa chỉ, thử lại sau.' : null}
      emptyText={emptyText}
      onSearch={setQuery}
      onChange={(opt) => { onChange(opt.line ?? opt.label); setQuery(''); }}
    />
  );
}
