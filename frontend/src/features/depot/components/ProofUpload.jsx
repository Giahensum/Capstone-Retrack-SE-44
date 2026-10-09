import { useMutation } from '@tanstack/react-query';
import api from '@/lib/axios';
import { useDepot, depotError } from '../DepotContext';

export default function ProofUpload({ onUploaded, onBusyChange, disabled }) {
  const { depotId } = useDepot();
  const upload = useMutation({
    mutationFn: async (file) => {
      if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || !file.size || file.size > 10 * 1024 * 1024)
        throw new Error('Chọn ảnh PNG, JPEG hoặc WebP có dung lượng tối đa 10 MB.');
      const form = new FormData();
      form.append('file', file);
      return (await api.post('/depot/proofs', form, { params: { depotId }, headers: { 'Content-Type': undefined } })).data.data.url;
    },
    onMutate: () => onBusyChange(true),
    onSuccess: onUploaded,
    onSettled: () => onBusyChange(false),
  });
  return <div className="space-y-2">
    <label className="block">Tải ảnh chứng từ
      <input type="file" accept="image/png,image/jpeg,image/webp" className="block w-full mt-2 text-sm text-d-on-surface-variant file:mr-4 file:min-h-11 file:cursor-pointer file:rounded-full file:border file:border-d-border-subtle file:bg-d-surface-container-low file:px-4 file:py-2 file:font-semibold file:text-d-on-surface hover:file:bg-d-surface-container-high disabled:opacity-50"
        disabled={disabled || upload.isPending || !depotId}
        onChange={(event) => { const file = event.target.files?.[0]; if (file) upload.mutate(file); event.target.value = ''; }} />
    </label>
    <p className="text-sm">PNG, JPEG hoặc WebP, tối đa 10 MB. Bạn cũng có thể nhập đường dẫn ảnh bên dưới.</p>
    {upload.isPending && <p role="status">Đang tải ảnh…</p>}
    {upload.isError && <p role="alert" className="text-d-error">{upload.error.response ? depotError(upload.error) : upload.error.message}</p>}
    {upload.isSuccess && <p role="status">Đã tải ảnh. Hãy kiểm tra và gửi xác nhận để ghi nhận chứng từ.</p>}
  </div>;
}
