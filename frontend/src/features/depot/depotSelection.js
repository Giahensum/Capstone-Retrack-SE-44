// Chỉ lưu lựa chọn giao diện; quyền kho vẫn lấy từ danh sách server trả về.
export function readDepotSelection(userId, storage) {
  if (!userId) return '';
  try { return (storage ?? window.localStorage).getItem('retrack:depot:' + userId) ?? ''; }
  catch { return ''; }
}
export function saveDepotSelection(userId, depotId, storage) {
  if (!userId || !depotId) return;
  try { (storage ?? window.localStorage).setItem('retrack:depot:' + userId, depotId); }
  catch { /* Storage bị chặn vẫn cho phép chọn kho trong phiên hiện tại. */ }
}
