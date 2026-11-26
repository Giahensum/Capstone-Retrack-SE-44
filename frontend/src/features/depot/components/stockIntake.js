const unitLabels = { kg: 'kg', cai: 'cái', bo: 'bộ' };

// Hạng mục mua chỉ gợi ý loại hàng; lượng nhập phải do kho xác nhận sau phân loại.
export function linesFromIntakeSource(source) {
  return source.items.map(item => ({
    key: item.agreementItemId,
    kind: item.kind === 'MATERIAL' ? 'MATERIAL' : 'REUSABLE',
    materialType: item.kind === 'MATERIAL' ? (item.materialCode ?? '') : null,
    name: item.name,
    unit: unitLabels[item.unit],
    quantity: '', referencePrice: '', directCost: '', conditionNote: '',
    sourceHint: 'Seller đăng: ' + Number(item.postedQuantity).toLocaleString('vi-VN') + ' ' + unitLabels[item.unit],
  }));
}
