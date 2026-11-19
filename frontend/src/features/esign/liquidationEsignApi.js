import api from '@/lib/axios';

// Ký điện tử hợp đồng đặt cọc thanh lý bằng DocuSign. Dùng chung cho Seller và Chủ kho; id là mã thỏa thuận (agreement).
// Backend: LiquidationEsignController (api/liquidations/esign).
const unwrap = (res) => res.data?.data ?? res.data;

export const getLiquidationEsign = (id) => api.get(`/liquidations/esign/${id}`).then(unwrap);

/** Trả { url }: địa chỉ trang ký DocuSign của bên người dùng; chuyển trình duyệt tới đó. */
export const startLiquidationSigning = (id) => api.post(`/liquidations/esign/${id}/sign`).then(unwrap);

/** Đọc lại trạng thái từ DocuSign; backend ghi mốc ký của từng bên vào thỏa thuận. */
export const refreshLiquidationEsign = (id) => api.post(`/liquidations/esign/${id}/refresh`).then(unwrap);

export async function downloadLiquidationContract(id, fileName) {
  let res;
  try {
    res = await api.get(`/liquidations/esign/${id}/document`, { responseType: 'blob' });
  } catch (error) {
    // Với responseType 'blob' thông báo lỗi của máy chủ nằm trong Blob: đọc ra để người dùng thấy lý do tiếng Việt.
    const blob = error?.response?.data;
    if (blob instanceof Blob) {
      try { error.response.data = JSON.parse(await blob.text()); } catch { /* giữ thông báo mặc định */ }
    }
    throw error;
  }
  const url = URL.createObjectURL(res.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName || 'hop-dong-dat-coc-da-ky.pdf';
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export const liquidationEsignError = (error) => error?.response?.data?.message || error?.message || 'Có lỗi xảy ra, vui lòng thử lại.';
