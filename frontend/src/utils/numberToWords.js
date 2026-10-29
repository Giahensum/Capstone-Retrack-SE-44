// Đọc số tiền VND thành chữ tiếng Việt (dùng cho hợp đồng): 1.060.000 → "Một triệu không trăm sáu mươi nghìn đồng".
const DIGITS = ['không', 'một', 'hai', 'ba', 'bốn', 'năm', 'sáu', 'bảy', 'tám', 'chín'];

// Đọc nhóm 3 chữ số (0–999); `full` = đọc cả "không trăm"/"lẻ" khi nhóm đứng sau nhóm cao hơn.
function readTriple(n, full) {
  const hundreds = Math.floor(n / 100), tens = Math.floor((n % 100) / 10), units = n % 10;
  const parts = [];
  if (hundreds > 0 || full) parts.push(`${DIGITS[hundreds]} trăm`);
  if (tens > 1) {
    parts.push(`${DIGITS[tens]} mươi`);
    if (units === 1) parts.push('mốt');
    else if (units === 4) parts.push('tư');
    else if (units === 5) parts.push('lăm');
    else if (units > 0) parts.push(DIGITS[units]);
  } else if (tens === 1) {
    parts.push('mười');
    if (units === 5) parts.push('lăm');
    else if (units > 0) parts.push(DIGITS[units]);
  } else if (units > 0) {
    if (hundreds > 0 || full) parts.push('lẻ');
    parts.push(DIGITS[units]);
  }
  return parts.join(' ');
}

function readInteger(n) {
  if (n === 0) return 'không';
  const billions = Math.floor(n / 1e9);
  const rest = n % 1e9;
  const groups = [Math.floor(rest / 1e6), Math.floor((rest % 1e6) / 1e3), rest % 1e3];
  const labels = ['triệu', 'nghìn', ''];
  const out = [];
  if (billions > 0) out.push(`${readInteger(billions)} tỷ`);
  let started = billions > 0;
  groups.forEach((g, i) => {
    if (g === 0) return;
    out.push(`${readTriple(g, started)} ${labels[i]}`.trim());
    started = true;
  });
  return out.join(' ');
}

export function amountInWords(amount) {
  const n = Math.round(Number(amount));
  if (!Number.isFinite(n) || n < 0) return '';
  const text = `${readInteger(n)} đồng`;
  return text.charAt(0).toUpperCase() + text.slice(1);
}
