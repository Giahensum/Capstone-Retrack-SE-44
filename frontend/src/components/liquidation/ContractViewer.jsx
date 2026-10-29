import { useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Printer, FileDown, FileText, Loader2, Info } from 'lucide-react';
import { Modal, Notice } from '@/components/liquidation/LiquidationUI';
import ContractDocument from './ContractDocument';

// Xem trước hợp đồng, in hoặc tải PDF. Bản in/PDF dựng từ một bản "không lề" đặt ngoài màn hình (#contract-print-root).
export default function ContractViewer({ contract, onClose }) {
  const printRef = useRef(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const downloadPdf = async () => {
    setBusy(true);
    setError('');
    try {
      const html2pdf = (await import('html2pdf.js')).default;
      await html2pdf().set({
        margin: [15, 15, 15, 15],
        filename: `${contract.number}.pdf`,
        image: { type: 'jpeg', quality: 0.98 },
        html2canvas: { scale: 2, useCORS: true, backgroundColor: '#ffffff' },
        jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' },
        pagebreak: { mode: ['css', 'legacy'], avoid: ['.contract-avoid'] },
      }).from(printRef.current).save();
    } catch {
      setError('Không tạo được file PDF. Hãy bấm "In hợp đồng" rồi chọn "Lưu dưới dạng PDF" ở máy in.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Modal
        size="xl" icon={FileText} tone="teal" title="Hợp đồng đặt cọc" subtitle={contract.number} onClose={onClose} busy={busy}
        footer={(
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <p className="text-xs text-d-on-surface-variant">Khổ A4 • in một mặt • có thể ký tay bổ sung trên bản in</p>
            <div className="flex flex-col gap-2 sm:flex-row">
              <button type="button" onClick={() => window.print()} disabled={busy} className="inline-flex items-center justify-center gap-2 rounded-full border border-d-border-subtle bg-white px-5 py-2.5 text-sm font-bold text-d-on-surface hover:bg-d-surface-container disabled:opacity-60"><Printer size={16} />In hợp đồng</button>
              <button type="button" onClick={downloadPdf} disabled={busy} className="inline-flex items-center justify-center gap-2 rounded-full bg-d-primary px-6 py-2.5 text-sm font-bold text-white shadow-sm transition active:scale-95 disabled:opacity-60">
                {busy ? <Loader2 size={16} className="animate-spin" /> : <FileDown size={16} />}{busy ? 'Đang tạo PDF…' : 'Tải file PDF'}
              </button>
            </div>
          </div>
        )}
      >
        <Notice tone="amber" icon={Info}>Văn bản được lập tự động từ dữ liệu phiên trả giá và các xác nhận trên nền tảng. Mục để trống (CCCD, địa chỉ…) hai bên tự điền khi in. Nên tham vấn pháp lý trước khi áp dụng cho giao dịch giá trị lớn.</Notice>
        {error && <p role="alert" className="text-sm font-semibold text-d-error">{error}</p>}
        <div className="overflow-auto rounded-2xl bg-[#d4d4d8] p-3 sm:p-6">
          <div className="mx-auto w-[210mm] bg-white shadow-lg"><ContractDocument contract={contract} /></div>
        </div>
      </Modal>
      {createPortal(<div id="contract-print-root"><div ref={printRef} style={{ width: "180mm", background: "#fff" }}><ContractDocument contract={contract} bare /></div></div>, document.body)}
    </>
  );
}
