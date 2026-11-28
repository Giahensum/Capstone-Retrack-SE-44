export const usesConfirmedQuantity = agreement => agreement.pricingBasis === 'CONFIRMED_HANDOVER_QUANTITY';

// kg có tối đa 3 số lẻ: nhân bằng phần nghìn nguyên rồi làm tròn VND như máy chủ.
export function handoverAmount(value, unitPrice) {
  const qty = Number(value), price = Number(unitPrice);
  if (!Number.isFinite(qty) || qty < 0 || qty > 1000000 || Math.round(qty * 1000) / 1000 !== qty || !Number.isSafeInteger(price) || price < 0) return 0;
  return Number((BigInt(Math.round(qty * 1000)) * BigInt(price) + 500n) / 1000n);
}
