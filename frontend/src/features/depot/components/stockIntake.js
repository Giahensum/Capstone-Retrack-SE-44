const unitLabels = { kg: 'kg', cai: 'cái', bo: 'bộ' };

// Hạng mục mua chỉ gợi ý loại hàng; lượng nhập phải do kho xác nhận sau phân loại.
export function linesFromIntakeSource(source) {
  return source.items.map(item => ({
    key: item.agreementItemId, agreementItemId: item.agreementItemId, sourceAmount: item.finalAmount, sourceName: item.name,
    kind: item.kind === 'MATERIAL' ? 'MATERIAL' : 'REUSABLE',
    materialType: item.kind === 'MATERIAL' ? (item.materialCode ?? '') : null,
    name: item.name,
    unit: unitLabels[item.unit],
    quantity: item.confirmedQuantity ?? '', referencePrice: '', directCost: '', conditionNote: '',
    sourceHint: item.confirmedQuantity != null ? 'Đã chốt bàn giao: ' + Number(item.confirmedQuantity).toLocaleString('vi-VN') + ' ' + unitLabels[item.unit] : 'Seller đăng: ' + Number(item.postedQuantity).toLocaleString('vi-VN') + ' ' + unitLabels[item.unit],
  }));
}

export function needsReferencePrice(lines, line) {
  return line.agreementItemId ? lines.filter(x=>x.agreementItemId===line.agreementItemId).length>1 : lines.length>1;
}
