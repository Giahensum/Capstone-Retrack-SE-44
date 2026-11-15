import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import SearchableSelect from './SearchableSelect';
import { fetchProvinces, fetchWards } from '@/lib/vnAdministrative';

const asOption = (row) => (row ? { value: String(row.code), label: row.name } : null);
const fromOption = (opt) => (opt ? { code: opt.value, name: opt.label } : null);

/**
 * Chọn Tỉnh/Thành phố → Phường/Xã bằng combobox có tìm kiếm, dữ liệu lấy từ API công khai.
 * - value: { province: {code,name}|null, ward: {code,name}|null }
 * - onChange(next): đổi tỉnh sẽ tự xóa phường/xã đã chọn.
 */
export default function VietnamAreaPicker({ idPrefix = 'area', value, onChange, errors = {}, fieldClass, labelClass }) {
  const provinces = useQuery({ queryKey: ['vn-provinces'], queryFn: ({ signal }) => fetchProvinces(signal), staleTime: Infinity, gcTime: Infinity });
  const provinceCode = value.province?.code;
  const wards = useQuery({
    queryKey: ['vn-wards', provinceCode],
    queryFn: ({ signal }) => fetchWards(provinceCode, signal),
    enabled: Boolean(provinceCode),
    staleTime: Infinity,
  });

  const provinceOptions = useMemo(() => (provinces.data ?? []).map(asOption), [provinces.data]);
  const wardOptions = useMemo(() => (wards.data ?? []).map(asOption), [wards.data]);
  const errText = (msg) => (msg ? <p role="alert" className="text-xs text-red-600 mt-1">{msg}</p> : null);
  const loadError = (q) => (q.isError ? 'Không tải được danh mục từ máy chủ địa chỉ.' : null);

  return (
    <>
      <div>
        <label htmlFor={`${idPrefix}-province`} className={labelClass}>Tỉnh / Thành phố</label>
        <SearchableSelect
          id={`${idPrefix}-province`}
          className={fieldClass}
          placeholder="Chọn hoặc gõ để tìm tỉnh/thành"
          value={asOption(value.province)}
          options={provinceOptions}
          loading={provinces.isPending}
          error={loadError(provinces)}
          onRetry={() => provinces.refetch()}
          onChange={(opt) => onChange({ province: fromOption(opt), ward: null })}
        />
        {errText(errors.province)}
      </div>
      <div>
        <label htmlFor={`${idPrefix}-ward`} className={labelClass}>Phường / Xã</label>
        <SearchableSelect
          id={`${idPrefix}-ward`}
          className={fieldClass}
          placeholder={provinceCode ? 'Chọn hoặc gõ để tìm phường/xã' : 'Chọn tỉnh/thành trước'}
          disabled={!provinceCode}
          value={asOption(value.ward)}
          options={wardOptions}
          loading={Boolean(provinceCode) && wards.isPending}
          error={loadError(wards)}
          onRetry={() => wards.refetch()}
          onChange={(opt) => onChange({ ...value, ward: fromOption(opt) })}
        />
        {errText(errors.ward)}
      </div>
    </>
  );
}
