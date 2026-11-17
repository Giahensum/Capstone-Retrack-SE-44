import { test, expect } from '@playwright/test';
const depot = 'd1111111-1111-1111-1111-111111111111';
const pickup = 'd2222222-2222-2222-2222-222222222222';
const future = 'd3333333-3333-3333-3333-333333333333';
const shift = 'd4444444-4444-4444-4444-444444444444';
test('Điều phối: bộ lọc ngoại lệ, bản đồ, phân công và đơn ngày sau trên desktop/mobile', async ({ page }) => {
  test.setTimeout(90000);
  const errors = [], statuses = [], candidates = [], saved = [];
  page.on('pageerror', error => errors.push(error.message));
  const pending = { id: pickup, sellerName: 'Seller kiểm thử', sellerPhone: '0901234567', address: 'Ngũ Hành Sơn, Đà Nẵng', latitude: 16.02, longitude: 108.25, preferredDatetime: null, status: 'PENDING', queueReason: 'Không còn nhân viên nhận đơn', hasCheckIn: false };
  const nextDay = { ...pending, id: future, address: 'Đơn hẹn ngày sau', latitude: null, longitude: null, preferredDatetime: new Date(Date.now() + 2 * 86400000).toISOString() };
  const paged = items => ({ items, totalCount: items.length, page: 1, pageSize: 20 });
  await page.addInitScript(() => localStorage.setItem('retrack-auth', JSON.stringify({ state: { token: 'ui-test-token', user: { userId: 'd5555555-5555-5555-5555-555555555555', role: 'DEPOT_OWNER', fullName: 'Chủ kho kiểm thử' }, isAuthenticated: true }, version: 0 })));
  // API giả chỉ kiểm chứng UI; quyền, SQL và trạng thái có bài PostgreSQL riêng.
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()), path = url.pathname;
    let data;
    if (path.endsWith('/depot/owned')) data = [{ id: depot, name: 'Kho kiểm thử' }];
    else if (path.endsWith('/depot/profile')) data = { verificationStatus: 'APPROVED' };
    else if (path.endsWith('/unread-count')) data = 0;
    else if (path.endsWith('/summary')) data = { areaCount: 1, activeShiftCount: 1, waitingCount: 1 };
    else if (path.endsWith('/pickups')) { statuses.push(url.searchParams.get('status')); data = paged(url.searchParams.get('status') === 'UPCOMING' ? [nextDay] : [pending]); }
    else if (path.endsWith('/events')) data = paged([]);
    else if (path.endsWith('/assign')) { saved.push(route.request().postDataJSON()); pending.status = 'SCHEDULED'; pending.employeeName = 'Nhân viên đã nhận'; pending.employeeId = 'd6666666-6666-6666-6666-666666666666'; pending.queueReason = 'ASSIGNED_MANUAL'; data = 'Đã lưu'; }
    else if (path.endsWith(`/pickups/${pickup}`)) data = pending;
    else if (path.endsWith(`/pickups/${future}`)) data = nextDay;
    else if (path.endsWith('/shifts')) { candidates.push(url.searchParams.get('pickupId')); data = paged(pending.employeeId ? [] : [{ id: shift, employeeName: 'Nhân viên phù hợp', areaName: 'Ngũ Hành Sơn', assignedCount: 0, maxPendingOrders: 10, endsAt: new Date(Date.now() + 4 * 3600000).toISOString() }]); }
    else data = paged([]);
    await route.fulfill({ json: { success: true, data } });
  });
  // Mất nền bản đồ vẫn phải giữ điểm, địa chỉ và các thao tác.
  if (process.env.E2E_MAP_NETWORK !== 'true') await page.route('https://tile.openstreetmap.org/**', route => route.abort());
  await page.goto('/depot/dispatch');
  await page.getByLabel('Trạng thái đơn').selectOption('EXCEPTIONS');
  await expect.poll(() => statuses.includes('EXCEPTIONS')).toBe(true);
  await expect(page.getByRole('region', { name: 'Vị trí trên trang đang xem' }).locator('.leaflet-container')).toBeVisible();
  if (process.env.E2E_MAP_NETWORK !== 'true') await expect(page.getByText('Chưa tải được nền bản đồ.', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'Chi tiết / phân công', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Chi tiết và phân công thu gom' });
  await expect(dialog.getByRole('region', { name: 'Vị trí thu gom' }).locator('.leaflet-container')).toBeVisible();
  if (process.env.E2E_MAP_NETWORK === 'true') await expect.poll(() => dialog.locator('.leaflet-tile-loaded').count()).toBeGreaterThan(0);
  for (const width of [390, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`ban-do-${width}.png`), fullPage: true });
  }
  await expect.poll(() => candidates.includes(pickup)).toBe(true);
  await dialog.getByLabel('Ca nhận đơn').selectOption(shift);
  await dialog.getByLabel('Lý do phân công / chuyển người').fill('Đã liên hệ nhân viên và Seller để xử lý ngoại lệ');
  await dialog.getByRole('button', { name: 'Xác nhận phân công' }).click();
  await expect(dialog).toContainText('Đã lưu phân công và thông báo');
  expect(saved).toHaveLength(1); expect(saved[0].shiftId).toBe(shift);
  await page.reload(); await expect(dialog).toContainText('Nhân viên đã nhận');
  await dialog.getByLabel('Đóng', { exact: true }).click();
  await page.getByLabel('Trạng thái đơn').selectOption('UPCOMING');
  await page.getByRole('button', { name: 'Chi tiết / phân công', exact: true }).click();
  await expect(dialog).toContainText('Đơn hẹn từ ngày mai');
  await expect(dialog.getByLabel('Ca nhận đơn')).toHaveCount(0);
  await expect(dialog.getByRole('region', { name: 'Vị trí thu gom' })).toContainText('Chưa có tọa độ hợp lệ');
  for (const width of [390, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    const bounds = await dialog.boundingBox(); expect(bounds.x).toBeGreaterThanOrEqual(0); expect(bounds.x + bounds.width).toBeLessThanOrEqual(width);
    await page.screenshot({ path: test.info().outputPath(`ngoai-le-${width}.png`), fullPage: true });
  }
  expect(errors).toEqual([]);
});
