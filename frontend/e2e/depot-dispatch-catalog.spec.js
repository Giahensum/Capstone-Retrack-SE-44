import { test, expect } from '@playwright/test';

test('Owner chọn vùng dùng chung, tạo ca không nhập tọa độ và vẫn dùng được vùng cũ', async ({ page }) => {
  const depot = 'd1111111-1111-1111-1111-111111111111';
  const employee = 'd2222222-2222-2222-2222-222222222222';
  const area = 'd3333333-3333-3333-3333-333333333333';
  const saved = [], errors = [];
  let used = false;
  const region = { code: 'DN-HOAQUY', name: 'Hòa Quý – Ngũ Hành Sơn', latitude: 15.9874125, longitude: 108.2562344, radiusMeters: 3500 };
  const paged = items => ({ items, totalCount: items.length, page: 1, pageSize: 20 });
  page.on('pageerror', e => errors.push(e.message));
  await page.addInitScript(() => localStorage.setItem('retrack-auth', JSON.stringify({ state: { token: 'ui-test-token', user: { userId: 'd5555555-5555-5555-5555-555555555555', role: 'DEPOT_OWNER', fullName: 'Chủ kho kiểm thử' }, isAuthenticated: true }, version: 0 })));
  // Mock HTTP chỉ kiểm chứng form và payload; phân quyền/transaction được test PostgreSQL riêng.
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname;
    let data = paged([]);
    if (path.endsWith('/depot/owned')) data = [{ id: depot, name: 'Kho mới' }];
    else if (path.endsWith('/depot/profile')) data = { verificationStatus: 'APPROVED' };
    else if (path.endsWith('/unread-count')) data = 0;
    else if (path.endsWith('/catalog')) data = [region];
    else if (path.endsWith('/catalog/DN-HOAQUY/use')) { used = true; data = { ...region, id: area }; }
    else if (path.endsWith('/summary')) data = { areaCount: used ? 1 : 0, activeShiftCount: saved.length, waitingCount: 0 };
    else if (path.endsWith('/areas')) data = paged(used ? [{ ...region, id: area, isActive: true }] : []);
    else if (path.endsWith('/employees')) data = paged([{ id: employee, userId: employee, fullName: 'Nhân viên cùng kho', phone: '0900000000' }]);
    else if (path.endsWith('/shifts') && route.request().method() === 'POST') { saved.push(route.request().postDataJSON()); data = { id: 'shift-test' }; }
    await route.fulfill({ json: { success: true, data } });
  });
  await page.route('https://tile.openstreetmap.org/**', route => route.abort());
  await page.goto('/depot/dispatch');
  await page.getByRole('button', { name: '＋ Vùng có sẵn', exact: true }).click();
  const catalog = page.getByRole('dialog', { name: 'Chọn vùng Đà Nẵng có sẵn' });
  await catalog.getByRole('combobox', { name: 'Vùng thu gom', exact: true }).selectOption(region.code);
  await expect(catalog).toContainText('15.9874125');
  await catalog.getByRole('button', { name: 'Sử dụng vùng cho kho' }).click();
  await expect(catalog).toHaveCount(0);
  await page.reload();
  await page.getByRole('tab', { name: 'Khu vực', exact: true }).click();
  await expect(page.getByRole('cell', { name: region.name, exact: true })).toBeVisible();
  await page.getByRole('button', { name: '＋ Bố trí ca', exact: true }).click();
  const shift = page.getByRole('dialog', { name: 'Bố trí ca thu gom' });
  await shift.getByRole('combobox', { name: 'Vùng có sẵn', exact: true }).selectOption(region.code);
  await shift.getByRole('combobox', { name: 'Nhân viên thu gom', exact: true }).selectOption(employee);
  await expect(shift.getByLabel('Vĩ độ tâm vùng')).toHaveCount(0);
  await page.setViewportSize({ width: 390, height: 844 });
  const bounds = await shift.boundingBox();
  expect(bounds.x).toBeGreaterThanOrEqual(0); expect(bounds.x + bounds.width).toBeLessThanOrEqual(391);
  await shift.getByRole('button', { name: 'Lưu ca thu gom' }).click();
  await expect(shift).toHaveCount(0);
  expect(saved[0].catalogCode).toBe(region.code);
  expect(saved[0].areaId).toBe('00000000-0000-0000-0000-000000000000');
  await page.getByRole('button', { name: '＋ Bố trí ca', exact: true }).click();
  await shift.getByRole('combobox', { name: 'Nguồn khu vực', exact: true }).selectOption('configured');
  await shift.getByRole('combobox', { name: 'Khu vực thu gom', exact: true }).selectOption(area);
  await shift.getByRole('combobox', { name: 'Nhân viên thu gom', exact: true }).selectOption(employee);
  await shift.getByRole('button', { name: 'Lưu ca thu gom' }).click();
  await expect(shift).toHaveCount(0);
  expect(saved[1].catalogCode).toBeNull(); expect(saved[1].areaId).toBe(area);
  expect(errors).toEqual([]);
});
