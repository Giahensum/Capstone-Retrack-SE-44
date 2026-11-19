import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { inputClass, Field } from '@/features/admin/components/AdminUI';
import { moneyAdminApi } from '../moneyAdminApi';
import { roleLabel } from '../moneyLabels';
import { useDebounced } from '../moneyUi';

/** Bộ lọc người nhận: ô tìm theo tên + danh sách chọn. value là userId hoặc ''. */
export default function PayeeFilter({ value, onChange, label = 'Người nhận' }) {
  const [search, setSearch] = useState('');
  const debounced = useDebounced(search);
  const q = useQuery({
    queryKey: ['money-admin', 'payees', 'filter', debounced],
    queryFn: () => moneyAdminApi.listPayees({ search: debounced || undefined, page: 1, pageSize: 30 }),
    placeholderData: (p) => p,
  });
  const items = q.data?.items ?? [];
  return (
    <div className="flex flex-wrap items-end gap-2">
      <Field label={`${label}: tìm tên`}><input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Gõ tên" className={`${inputClass} w-44`} /></Field>
      <Field label={label}>
        <select value={value} onChange={(e) => onChange(e.target.value)} className={`${inputClass} w-56 cursor-pointer`}>
          <option value="">Tất cả</option>
          {value && !items.some((p) => p.userId === value) && <option value={value}>Đang lọc (đã chọn)</option>}
          {items.map((p) => <option key={p.userId} value={p.userId}>{p.fullName} ({roleLabel(p.role)})</option>)}
        </select>
      </Field>
    </div>
  );
}
