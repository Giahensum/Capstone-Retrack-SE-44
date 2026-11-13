import { test, expect } from '@playwright/test';
import fs from 'node:fs';

test.describe('Thông báo Depot qua API thật', () => {
  test.skip(!process.env.E2E_NOTICE_FIXTURE || !process.env.E2E_NOTICE_PASSWORD, 'Cần fixture riêng trong DB test.');
  let fixture;
  test.beforeAll(() => { fixture = JSON.parse(fs.readFileSync(process.env.E2E_NOTICE_FIXTURE, 'utf8')); });
  const login = async (page, email) => {
    await page.goto('/login'); await page.getByLabel('Email', { exact: true }).fill(email);
    await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.E2E_NOTICE_PASSWORD);
    await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click(); await expect(page).toHaveURL(/\/depot/, { timeout: 20000 });
  };
  test('Factory/Employee cập nhật, Owner nhận thông báo, đọc và mở đúng chi tiết', async ({ page, request }) => {
    test.setTimeout(120000);
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    const actor = async email => {
      const response = await request.post('/api/auth/login', { data: { email, password: process.env.E2E_NOTICE_PASSWORD } }); expect(response.ok()).toBe(true);
      return { Authorization: `Bearer ${(await response.json()).data.token}` };
    };
    await login(page, fixture.OwnerEmail); await page.goto('/depot/notifications');
    await expect(page.getByRole('article')).toHaveCount(20);
    await page.getByRole('button', { name: 'Sau', exact: true }).click(); await expect(page.getByRole('article')).toHaveCount(5);
    await page.getByRole('button', { name: 'Trước', exact: true }).click();
    await page.getByRole('searchbox').fill('kiểm thử 03'); await page.getByRole('button', { name: 'Tìm thông báo', exact: true }).click();
    await expect(page.getByRole('article')).toHaveCount(1); await page.getByRole('searchbox').fill('');
    await expect(page.getByRole('article')).toHaveCount(20);
    await page.getByLabel('Nhóm thông báo').selectOption('FACTORY'); await expect(page.getByText('Chưa có thông báo phù hợp')).toBeVisible();
    const factory = await actor(fixture.FactoryEmail);
    expect((await request.post(`/api/factory/orders/${fixture.BatchId}/receive`, { headers: factory })).ok()).toBe(true);
    // Đợi poll thực tế, không chèn dữ liệu vào React Query hoặc mock API.
    await expect(page.getByRole('article').filter({ hasText: 'Nhà máy xác nhận hàng đã đến' })).toBeVisible({ timeout: 35000 });
    await page.getByRole('button', { name: /^Mở thông báo/ }).click();
    const quick = page.getByRole('dialog', { name: 'Thông báo nhanh' });
    await quick.getByRole('article').filter({ hasText: 'Nhà máy xác nhận hàng đã đến' }).getByRole('button', { name: 'Xem lô / nhật ký xử lý' }).click();
    await expect(page.getByRole('dialog', { name: /Chi tiết lô/ })).toBeVisible(); await page.getByRole('dialog').getByLabel('Đóng', { exact: true }).click();
    await page.goto('/depot/notifications');
    await page.getByRole('button', { name: 'Đánh dấu tất cả đã đọc' }).click();
    await expect(page.getByRole('button', { name: 'Mở thông báo, 0 chưa đọc' })).toBeVisible();
    await page.reload(); await expect(page.getByRole('button', { name: 'Mở thông báo, 0 chưa đọc' })).toBeVisible();
    await page.getByLabel('Kho đang quản lý').selectOption(fixture.SecondDepotId);
    await expect(page.getByRole('article').filter({ hasText: 'Chỉ kho phụ' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Mở thông báo, 1 chưa đọc' })).toBeVisible();
    await page.getByLabel('Kho đang quản lý').selectOption(fixture.DepotId);
    const employee = await actor(fixture.EmployeeEmail);
    expect((await request.patch(`/api/depot/stock/receipts/${fixture.ReceiptId}/submit?depotId=${fixture.DepotId}`, { headers: employee })).ok()).toBe(true);
    await page.reload(); await page.getByLabel('Nhóm thông báo').selectOption('STOCK');
    await page.getByRole('article').filter({ hasText: 'Phiếu nhập kho đang chờ duyệt' }).getByRole('button', { name: 'Xem phiếu nhập kho' }).click();
    await expect(page.getByRole('dialog', { name: 'Phiếu nhập: Phiếu nhập thông báo fixture' })).toBeVisible();
    await page.getByRole('dialog').getByLabel('Đóng', { exact: true }).click();
    const rival = await actor(fixture.RivalEmail);
    expect((await request.post(`/api/depot/liquidations/${fixture.LotId}/bids?depotId=${fixture.RivalDepotId}`, { headers: rival, data: { operationId: crypto.randomUUID(), amount: 10100000 } })).ok()).toBe(true);
    await page.goto('/depot/notifications'); await page.getByLabel('Nhóm thông báo').selectOption('LIQUIDATION');
    await page.getByRole('article').filter({ hasText: 'Giá của bạn đã bị vượt' }).getByRole('button', { name: 'Xem lô lớn' }).click();
    await expect(page.getByRole('dialog', { name: 'Lô lớn thông báo fixture', exact: true })).toBeVisible();
    await page.getByRole('dialog').getByLabel('Đóng', { exact: true }).click();
    await page.goto('/depot/notifications'); await page.getByLabel('Nhóm thông báo').selectOption('FACTORY');
    await page.getByRole('article').getByRole('button', { name: 'Đánh dấu chưa đọc' }).click();
    await page.getByLabel('Lọc trạng thái đọc').selectOption('UNREAD'); await expect(page.getByRole('article')).toHaveCount(1);
    expect(errors).toEqual([]);
  });
  test('chuông trên mobile không tràn, hỗ trợ Escape và nhấn ngoài để đóng', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 }); await login(page, fixture.OwnerEmail); await page.goto('/depot/notifications');
    const trigger = page.getByRole('button', { name: /^Mở thông báo/ }); await trigger.click();
    const panel = page.getByRole('dialog', { name: 'Thông báo nhanh' }); await expect(panel).toBeVisible();
    const box = await panel.boundingBox(); expect(box.x).toBeGreaterThanOrEqual(0); expect(box.x + box.width).toBeLessThanOrEqual(390);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: test.info().outputPath('chuong-thong-bao-mobile.png'), fullPage: true });
    await page.keyboard.press('Escape'); await expect(panel).toHaveCount(0); await expect(trigger).toBeFocused();
    await trigger.click(); await page.mouse.click(2, 2); await expect(panel).toHaveCount(0);
  });
});
