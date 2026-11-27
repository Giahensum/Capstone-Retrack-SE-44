const format = value => Number.isFinite(Number(value)) ? Number(value).toLocaleString('vi-VN', { maximumFractionDigits: 12 }) : value;
const bound = (value, type) => type === 'number' ? format(value)
  : value.replace(/^(\d{4})-(\d{2})-(\d{2})/, '$3/$2/$1').replace('T', ' ');

export function fieldLabel(control) {
  if (control.dataset.fieldLabel) return control.dataset.fieldLabel;
  if (control.getAttribute('aria-label')) return control.getAttribute('aria-label');
  const label = control.labels?.[0]?.cloneNode(true);
  label?.querySelectorAll('input, select, textarea, button, svg, [data-field-error]').forEach(node => node.remove());
  return label?.textContent?.replace(/\s+/g, ' ').trim() || control.name || 'Ô nhập này';
}

export function fieldProblem(control) {
  if (!control.willValidate || control.dataset.validationSkip != null) return null;
  const label = fieldLabel(control);
  const value = control.value ?? '';
  const v = control.validity;
  if (control.dataset.validationError) return control.dataset.validationError;
  if (v.badInput) return `${label}: nhập số hợp lệ, không dùng chữ hoặc ký hiệu khác.`;
  if (v.valueMissing || control.required && !['checkbox', 'radio', 'file'].includes(control.type) && !value.trim())
    return control.type === 'checkbox' ? 'Hãy đánh dấu ô xác nhận trước khi tiếp tục.'
      : control.type === 'file' ? `Chọn tệp cho ${label.toLocaleLowerCase('vi-VN')}.`
      : control.tagName === 'SELECT' ? `Chọn ${label.toLocaleLowerCase('vi-VN')}.` : `Nhập ${label.toLocaleLowerCase('vi-VN')}.`;
  if (!value.trim()) return null;
  if (v.typeMismatch) return control.type === 'email' ? 'Nhập email đúng định dạng, ví dụ ten@example.com.' : 'Nhập đường dẫn đầy đủ, ví dụ https://example.com/anh.png.';
  if (v.patternMismatch) return control.dataset.patternMessage || `${label}: ${control.title || 'kiểm tra lại định dạng đã hướng dẫn'}.`;
  if (control.minLength > 0 && value.trim().length < control.minLength) return `${label}: cần ít nhất ${control.minLength} ký tự.`;
  if (control.maxLength >= 0 && value.length > control.maxLength) return `${label}: tối đa ${control.maxLength} ký tự.`;
  if (v.rangeUnderflow) return `${label}: tối thiểu ${bound(control.min, control.type)}.`;
  if (v.rangeOverflow) return `${label}: tối đa ${bound(control.max, control.type)}.`;
  if (v.stepMismatch) return `${label}: ${control.step === '1' ? 'chỉ nhập số nguyên' : `nhập theo bước ${format(control.step || 1)}`}.`;
  if (!v.valid) return `${label}: kiểm tra lại giá trị đã nhập.`;
  return null;
}

export function collectProblems(form, validate, submitter) {
  const controls = Array.from(form.elements).filter(c => c.matches('input,select,textarea'));
  const problems = controls.flatMap(control => {
    const message = fieldProblem(control);
    return message ? [{ id: control.id, label: fieldLabel(control), message }] : [];
  });
  const additional = validate?.(new FormData(form), form, submitter) ?? {};
  Object.entries(additional).forEach(([name, message]) => {
    if (!message) return;
    const control = controls.find(c => c.name === name || c.id === name);
    if (control?.willValidate && !problems.some(e => e.id === control.id))
      problems.push({ id: control.id, label: fieldLabel(control), message });
  });
  return problems;
}

export function focusField(form, id) {
  const control = Array.from(form.elements).find(c => c.id === id);
  if (!control) return;
  // Trường nâng cao trong details vẫn có thể sai; mở đúng phần trước khi chuyển focus.
  for (let parent = control.parentElement; parent && parent !== form; parent = parent.parentElement)
    if (parent.tagName === 'DETAILS') parent.open = true;
  control.focus();
  control.scrollIntoView({ block: 'nearest' });
}
