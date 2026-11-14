import { useEffect, useState } from 'react';
import { Download, Eye, FileText } from 'lucide-react';
import api from '@/lib/axios';
import { Dialog, buttonClass } from './DepotUI';
import { documentKinds, documentSize } from './VerificationUploadForm';

export default function VerificationDocument({ file }) {
  const [busy, setBusy] = useState(false), [error, setError] = useState(''), [preview, setPreview] = useState(null);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);
  const load = async (show) => {
    setBusy(true); setError('');
    try {
      const response = await api.get('/depot/verifications/documents/' + file.id, { responseType: 'blob' });
      const url = URL.createObjectURL(response.data);
      if (show) setPreview(url);
      else {
        const link = document.createElement('a'); link.href = url; link.download = file.name; link.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
      }
    } catch { setError('Không tải được hồ sơ. Hãy kiểm tra quyền truy cập hoặc thử lại.'); }
    finally { setBusy(false); }
  };
  const image = ['image/png', 'image/jpeg'].includes(file.contentType);
  return <div className="min-w-0 rounded-xl border border-d-border-subtle bg-d-surface p-3 space-y-2 flex-1 basis-64">
    <div className="flex gap-2 items-start"><FileText size={18} className="shrink-0 text-d-primary mt-1" aria-hidden="true" />
      <div className="min-w-0"><p className="font-medium break-all">{file.name}</p><p className="text-xs text-d-on-surface-variant">{documentKinds[file.kind] ?? documentKinds.LEGACY} · {documentSize(file.size)}</p></div>
    </div>
    <div className="flex flex-wrap gap-2">
      {image && <button type="button" className={buttonClass} disabled={busy} onClick={() => load(true)} aria-label={'Xem ' + file.name}><Eye size={16} aria-hidden="true" />Xem ảnh</button>}
      <button type="button" className="min-h-11 rounded-full border border-d-border-subtle px-4 py-2 text-sm font-semibold text-d-primary inline-flex gap-2 items-center disabled:opacity-40"
        disabled={busy} onClick={() => load(false)} aria-label={'Tải ' + file.name}><Download size={16} aria-hidden="true" />{busy ? 'Đang tải…' : 'Tải tệp'}</button>
    </div>
    {error && <p role="alert" className="text-sm text-d-error">{error}</p>}
    {preview && <Dialog title={file.name} wide onClose={() => setPreview(null)}>
      <p className="text-sm text-d-on-surface-variant mb-3">{documentKinds[file.kind] ?? documentKinds.LEGACY} · Chỉ chủ kho và Admin được truy cập.</p>
      <img src={preview} alt={file.name} className="w-full max-h-[60vh] object-contain rounded-xl bg-white" />
    </Dialog>}
  </div>;
}
