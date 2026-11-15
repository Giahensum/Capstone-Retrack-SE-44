const labels = { PET: 'Nhựa PET', HDPE: 'Nhựa HDPE', PVC: 'Nhựa PVC', PAPER: 'Giấy', CARDBOARD: 'Bìa carton', ALUMINUM: 'Nhôm', IRON: 'Sắt', STEEL: 'Thép', COPPER: 'Đồng', ELECTRONIC_WASTE: 'Rác thải điện tử', OTHER: 'Khác' };
export const materialLabel = (code) => labels[code] ?? code;
