import api from '@/lib/axios';

// Ký điện tử hợp đồng hợp tác Nhà máy – Vựa bằng DocuSign (sandbox). Dùng chung cho cả hai bên; id là mã quan hệ hợp tác
// (cột `id` của danh sách đối tác ở cả trang Nhà máy lẫn trang Chủ vựa). Backend: ContractEsignController.
const unwrap = (res) => res.data?.data ?? res.data;

export const getEsignStatus = (id) => api.get(`/contracts/esign/${id}`).then(unwrap);

/** Trả { url }: địa chỉ trang ký DocuSign của bên người dùng; chuyển trình duyệt tới đó. */
export const startEsignSigning = (id) => api.post(`/contracts/esign/${id}/sign`).then(unwrap);

/** Đọc lại trạng thái từ DocuSign; ký đủ hai bên thì hợp đồng có hiệu lực. */
export const refreshEsign = (id) => api.post(`/contracts/esign/${id}/refresh`).then(unwrap);

export async function downloadSignedContract(id, fileName) {
  const res = await api.get(`/contracts/esign/${id}/document`, { responseType: 'blob' });
  const url = URL.createObjectURL(res.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName || 'hop-dong-da-ky.pdf';
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export const esignErrorMessage = (error) => error?.response?.data?.message || error?.message || 'Có lỗi xảy ra, vui lòng thử lại.';
