import { test, expect } from '@playwright/test';

test('Admin chỉ phát hành kỳ đã đóng, xem hóa đơn bổ sung và đối soát đúng số tiền', async ({ page }) => {
  await page.clock.setFixedTime(new Date('2026-11-17T03:00:00Z'));
  await page.addInitScript(() => localStorage.setItem('retrack-auth', JSON.stringify({
    state: { token: 'billing-ui-fixture', user: { userId: '11111111-1111-4111-8111-111111111111', role: 'ADMIN', fullName: 'Admin kiểm thử' }, isAuthenticated: true }, version: 0,
  })));
  const rows = [
    { id: '11111111-1111-4111-8111-111111111112', payerName: 'Kho Đà Nẵng', periodYear: 2026, periodMonth: 10, revision: 0, totalFeeAmount: 50, status: 'PAID' },
    { id: '11111111-1111-4111-8111-111111111113', payerName: 'Kho Đà Nẵng', periodYear: 2026, periodMonth: 10, revision: 1, adjustmentOfId: '11111111-1111-4111-8111-111111111112', totalFeeAmount: 100, status: 'PENDING' },
  ];
  let generated = null, paid = null;
  await page.route('**/api/**', async route => {
    const request = route.request(), path = new URL(request.url()).pathname;
    let data = {};
    if (path.endsWith('/revenue')) data = { totalRevenue: 150, points: [] };
    if (path.endsWith('/invoices')) data = { items: rows, totalCount: rows.length };
    if (path.endsWith('/invoices/generate')) { generated = request.postDataJSON(); data = []; }
    if (path.endsWith('/mark-paid')) { paid = path.split('/').at(-2); rows[1].status = 'PAID'; data = rows[1]; }
    await route.fulfill({ json: { success: true, data } });
  });
  await page.goto('/admin/billing');
  await page.getByRole('tab', { name: 'Hóa đơn phí', exact: true }).click();
  await expect(page.getByLabel('Tháng', { exact: true })).toHaveValue('10');
  await expect(page.getByLabel('Năm', { exact: true })).toHaveValue('2026');
  const issue = page.getByRole('button', { name: 'Phát hành / đối soát', exact: true });
  await page.getByLabel('Tháng', { exact: true }).fill('11'); await expect(issue).toBeDisabled();
  await expect(page.getByRole('status').filter({ hasText: 'Kỳ chưa kết thúc' })).toBeVisible();
  await page.getByLabel('Tháng', { exact: true }).fill('10'); await issue.click();
  await expect.poll(() => generated).toEqual({ year: 2026, month: 10 });
  const supplement = page.getByRole('row').filter({ hasText: 'Bổ sung lần 1' });
  await expect(supplement).toContainText('100');
  await supplement.getByRole('button', { name: 'Đã thanh toán', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Hóa đơn bổ sung lần 1');
  await page.getByRole('button', { name: 'Xác nhận đã thanh toán', exact: true }).click();
  await expect.poll(() => paid).toBe(rows[1].id); expect(rows[0].totalFeeAmount).toBe(50);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: test.info().outputPath('doi-soat-hoa-don-mobile.png'), fullPage: true });
});
