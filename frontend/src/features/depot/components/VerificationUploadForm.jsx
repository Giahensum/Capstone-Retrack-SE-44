import { useEffect, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { FileCheck2, ImagePlus, Paperclip, Trash2, Upload } from 'lucide-react';
import api from '@/lib/axios';
import { buttonClass, inputClass, MutationError } from './DepotUI';
import { depotError } from '../depotError';

export const documentKinds = {
  REGISTRATION: 'Giấy tờ kinh doanh', DEPOT_PHOTO: 'Ảnh kho', SUPPORTING: 'Tài liệu bổ sung', LEGACY: 'Hồ sơ chưa phân loại',
};
export const documentSize = bytes => bytes < 1024 ? bytes + ' byte' : (bytes / 1024).toFixed(1) + ' KB';
function SelectedImage({ file }) {
  const [url, setUrl] = useState('');
  useEffect(() => {
    const next = URL.createObjectURL(file);
    setUrl(next);
    return () => URL.revokeObjectURL(next);
  }, [file]);
  return url && <img src={url} alt={'Xem trước ' + file.name} className="h-20 w-24 rounded-lg object-cover border border-d-border-subtle" />;
}
export default function VerificationUploadForm({ depotId, onSuccess }) {
  const [files, setFiles] = useState([]), [note, setNote] = useState('');
  const [operationId, setOperationId] = useState(() => crypto.randomUUID());
  const [error, setError] = useState('');
  const policy = useQuery({ queryKey: ['verification-upload-policy'], retry: 1, staleTime: 30000,
    queryFn: async () => (await api.get('/depot/verifications/policy')).data.data });
  const upload = useMutation({
    mutationFn: async () => {
      const form = new FormData();
      form.append('operationId', operationId); form.append('note', note);
      files.forEach(({ file, kind }) => { form.append('files', file); form.append('fileKinds', kind); });
      await api.post('/depot/verifications', form, { params: { depotId }, headers: { 'Content-Type': 'multipart/form-data' } });
    },
    onSuccess,
  });
  const changed = () => { setOperationId(crypto.randomUUID()); upload.reset(); setError(''); };
  const addFiles = (event, kind) => {
    const incoming = Array.from(event.target.files ?? []);
    event.target.value = '';
    if (!incoming.length) return;
    if (files.length + incoming.length > 3) { setError('Tổng hồ sơ tối đa 3 tệp. Hãy xóa tệp cũ trước khi thêm.'); return; }
    const types = kind === 'DEPOT_PHOTO' ? ['image/png', 'image/jpeg'] : ['application/pdf', 'image/png', 'image/jpeg'];
    if (incoming.some(file => !file.size || file.size > 2 * 1024 * 1024 || !types.includes(file.type))) {
      setError('Tệp phải đúng định dạng, không rỗng và tối đa 2 MB mỗi tệp.'); return;
    }
    changed(); setFiles(current => [...current, ...incoming.map(file => ({ file, kind, id: crypto.randomUUID() }))]);
  };
  const ready = files.some(item => item.kind === 'REGISTRATION');
  return <form className="space-y-5" onSubmit={event => { event.preventDefault(); if (ready && !upload.isPending && policy.data?.acceptsUploads) upload.mutate(); }}>
    <div className="rounded-xl border border-d-border-subtle p-3 text-sm" role={policy.isError || policy.data?.acceptsUploads === false ? 'alert' : 'status'}>
      {policy.isPending ? 'Đang kiểm tra điều kiện nộp hồ sơ…' : policy.isError
        ? <>{depotError(policy.error)} <button type="button" className="underline font-semibold" onClick={() => policy.refetch()}>Thử lại</button></>
        : policy.data?.message}
    </div>
    <div className="rounded-2xl bg-d-surface-container-low p-4 text-sm space-y-2">
      <p className="font-semibold">Chuẩn bị trước khi nộp</p>
      <ol className="list-decimal pl-5 space-y-1">
        <li>Kiểm tra tên kho, địa chỉ và mã số thuế trên hồ sơ kho.</li>
        <li>Chụp hoặc quét rõ giấy đăng ký kinh doanh / hộ kinh doanh, đủ các góc và thông tin.</li>
        <li>Có thể kèm ảnh mặt tiền, biển hiệu hoặc khu vực lưu hàng để Admin đối chiếu.</li>
      </ol>
      <p>Không tải giấy tờ cá nhân hoặc dữ liệu không liên quan. Kèm giấy phép chuyên ngành nếu hoạt động của kho yêu cầu.</p>
    </div>
    <fieldset disabled={upload.isPending} className="space-y-4 disabled:opacity-60">
      <legend className="font-semibold mb-3">Tài liệu đính kèm · tối đa 3 tệp, 2 MB mỗi tệp</legend>
      <div className="grid gap-3 md:grid-cols-3">
        {[
          ['REGISTRATION', FileCheck2, 'Bắt buộc · PDF, PNG, JPEG'],
          ['DEPOT_PHOTO', ImagePlus, 'Tùy chọn · PNG, JPEG'],
          ['SUPPORTING', Paperclip, 'Tùy chọn · PDF, PNG, JPEG'],
        ].map(([kind, Icon, help]) => <label key={kind} className="min-w-0 rounded-2xl border border-d-border-subtle bg-white p-4 space-y-2 cursor-pointer focus-within:outline-2 focus-within:outline-d-primary">
          <Icon size={22} className="text-d-primary" aria-hidden="true" />
          <span className="block font-semibold">{documentKinds[kind]}</span>
          <span className="block text-xs text-d-on-surface-variant">{help}</span>
          <input aria-label={documentKinds[kind]} type="file" multiple
            accept={kind === 'DEPOT_PHOTO' ? 'image/png,image/jpeg' : 'application/pdf,image/png,image/jpeg'}
            className="sr-only"
            onChange={event => addFiles(event, kind)} />
          <span className="inline-flex items-center gap-2 min-h-11 rounded-full bg-d-surface-container-low px-4 py-2 text-sm font-semibold text-d-primary"><Upload size={16} aria-hidden="true" />Chọn tệp</span>
        </label>)}
      </div>
      {files.length > 0 && <ul className="space-y-2" aria-label="Tệp đã chọn">{files.map(({ file, kind, id }) => <li key={id} className="flex items-center gap-3 rounded-xl border border-d-border-subtle p-3">
        {file.type.startsWith('image/') && <SelectedImage file={file} />}
        <div className="min-w-0 flex-1"><p className="font-medium break-all">{file.name}</p><p className="text-xs text-d-on-surface-variant">{documentKinds[kind]} · {documentSize(file.size)}</p></div>
        <button type="button" aria-label={'Xóa ' + file.name} className="min-h-11 min-w-11 rounded-full border border-d-border-subtle inline-flex items-center justify-center text-d-error"
          onClick={() => { changed(); setFiles(current => current.filter(item => item.id !== id)); }}><Trash2 size={18} aria-hidden="true" /></button>
      </li>)}</ul>}
      {error && <p role="alert" className="text-sm text-d-error">{error}</p>}
      <label className="block">Ghi chú hồ sơ<textarea className={inputClass} rows={3} maxLength={2000} value={note}
        placeholder="Ví dụ: giấy tờ đứng tên hộ kinh doanh, thông tin cần Admin đối chiếu…"
        onChange={event => { changed(); setNote(event.target.value); }} /></label>
    </fieldset>
    <MutationError mutation={upload} />
    <div className="flex flex-wrap items-center gap-3">
      <button className={buttonClass} disabled={upload.isPending || !ready || !policy.data?.acceptsUploads}><Upload size={18} aria-hidden="true" />{upload.isPending ? 'Đang nộp…' : 'Nộp hồ sơ xác minh'}</button>
      <p className="text-xs text-d-on-surface-variant">{files.length}/3 tệp · {ready ? 'Đã có giấy tờ kinh doanh' : 'Cần ít nhất một giấy tờ kinh doanh'}</p>
    </div>
    <p className="text-xs text-d-on-surface-variant">Hồ sơ đã nộp được lưu lịch sử. Nếu cần thay đổi, chờ Admin yêu cầu bổ sung rồi nộp bộ hồ sơ mới.</p>
  </form>;
}
